// MiningBeamFx.cs
// Remake: the mining beam's look and sound (Mining's beam mode), after the mining lasers of Everspace 2: beams from the
// ship's wing guns converging on the asteroid, sparks where they cut, and the ore pulled in along them.
//   beams     fixed to the ship: straight from the wing mounts to the point on its heading where it meets the rock (Mining);
//             one per outer primary mount (the leftmost and the rightmost, else the one, else the ship's centre), the
//             beam laser's own projectile mesh (WeaponFx of attr 103, default 228 the Raccoon's green beam; GunRig's beam:
//             1 unit along +Z, scaled to the length): a steady core (its animation looping without the fade, so the
//             texture keeps scrolling, its width steady) and two copies replayed in turn every 450 ms with the
//             fade on (the weapon's own shot: a sheath that widens and fades)
//   contact   the beam laser's impact replayed every 300 ms, and sparks (particles.png's star, cell 61) thrown off the rock
//   ore       every ton cut leaves the rock as a chunk of the asteroid's own mesh with a green glow (particles.png cell 3)
//             and a sparkling trail; it pops off the surface, then is pulled in, spiralling around the beam and speeding
//             up, until it vanishes into the ship (0.7 s + 1 s per 900 m); a class-A core is bigger and glows teal
//   sound     the beam laser's shot as it ignites, the repair beam's hum (2271) while it is on
// The ore goes into the hold when its chunk arrives (Arrived); what is still flying when the level ends is delivered then.

using System;
using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Visuals;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class MiningBeamFx : MonoBehaviour
    {
        const float M = Gun.MetersPerUnit;
        const float CoreWidth = 0.6f, PulseMs = 450f, ImpactMs = 300f;
        // Seen from the chase camera hundreds of metres away: smaller chunks were a pixel or two.
        const float ChunkSizeMeters = 9f, ChunkGlowMeters = 26f, TrailSizeMeters = 12f, SpiralMeters = 14f;
        const int SparkCell = 61, OreGlowCell = 3, CoreGlowCell = 2;

        class Beam
        {
            public Vector3 mount;                 // on the banked model (ShipController.visualModel)
            public Transform core;
            public Transform[] pulses;
            public List<(Transform tr, Vector3 scale)> coreParts;
        }

        class Chunk
        {
            public Transform tr, rock, glow;
            public Vector3 start, lift, spinAxis;
            public float t, duration, phase, size, spinSpeed;
            public int item;
            public bool core;
        }

        /// <summary>A chunk reached the ship: (item, is the core).</summary>
        public event Action<int, bool> Arrived;
        /// <summary>Chunks on their way (their tons are spoken for in the hold).</summary>
        public int InFlight => flying.Count;
        public bool On { get; private set; }

        ShipController ship;
        Database db;
        WeaponFx fx;
        Transform root;
        readonly List<Beam> beams = new List<Beam>();
        int beamsFor = -1;
        GameObject[] impacts;
        float[] impactLeft;
        float impactLength;
        int nextImpact;
        float pulseTimer, impactTimer;
        int nextPulse;
        ParticleSystem sparks, trail;
        Material particles;
        readonly List<Chunk> flying = new List<Chunk>();
        readonly List<Chunk> spare = new List<Chunk>();
        AudioSource hum, sfx;

        public void Setup(ShipController controller, Database database, int lookItem)
        {
            ship = controller;
            db = database;
            fx = WeaponFx.Load(lookItem);
            if (fx == null || fx.projectile == null) fx = WeaponFx.Load(228);
            var assets = CombatAssets.Load();
            particles = assets != null ? assets.particlesMaterial : null;
            if (root == null) root = new GameObject("Mining beam fx").transform;
            BuildContact();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            var humClip = assets != null ? assets.repairLoop1 : null;
            if (humClip != null)
            {
                hum = gameObject.AddComponent<AudioSource>();
                hum.clip = Modding.ModSounds.Get(humClip);
                hum.loop = true;
                hum.playOnAwake = false;
                hum.spatialBlend = 0f;
            }
        }

        void OnDestroy()
        {
            // The ore is in the hold from the moment it is cut (Mining.Launch): the chunks still in the air just go. (Delivering
            // them here put ore into whatever state the next scene brought: after a failed mission the reloaded save's.)
            flying.Clear();
            if (root != null) Destroy(root.gameObject);
        }

        // ---- the beams ---------------------------------------------------------------------------------------

        /// <summary>The beams from the outer primary mounts (rebuilt when the hull changes).</summary>
        void BuildBeams()
        {
            if (beamsFor == Session.ShipIndex && beams.Count > 0) return;
            foreach (var b in beams)
            {
                if (b.core != null) Destroy(b.core.gameObject);
                foreach (var p in b.pulses) if (p != null) Destroy(p.gameObject);
            }
            beams.Clear();
            beamsFor = Session.ShipIndex;
            var mounts = db != null ? db.MountsOf(Session.ShipIndex, 0) : new List<WeaponMount>();
            var points = new List<Vector3>();
            if (mounts.Count > 0)
            {
                Vector3 left = WeaponSystem.MountToLocal(mounts[0]), right = left;
                foreach (var m in mounts)
                {
                    var p = WeaponSystem.MountToLocal(m);
                    if (p.x < left.x) left = p;
                    if (p.x > right.x) right = p;
                }
                points.Add(left);
                if ((right - left).sqrMagnitude > 0.25f) points.Add(right);
            }
            else points.Add(Vector3.zero);
            foreach (var p in points)
            {
                var b = new Beam { mount = p, pulses = new Transform[2] };
                if (fx != null && fx.projectile != null)
                {
                    b.core = MakeCore(out b.coreParts);
                    for (int i = 0; i < b.pulses.Length; i++) b.pulses[i] = MakePulse();
                }
                beams.Add(b);
            }
        }

        /// <summary>The steady beam: the projectile's animation looping without its fade (the texture scroll stays), its
        /// width set here every frame (the animation widens it as the shot fades).</summary>
        Transform MakeCore(out List<(Transform, Vector3)> parts)
        {
            var go = Instantiate(fx.projectile, root);
            go.name = "Mining beam";
            GunRig.StripForFx(go);
            var anims = go.GetComponentsInChildren<PartAnimation>(true);
            // The parts the animation scales: their scale at the end differs from the one at 33 ms (full width, full light).
            var all = go.GetComponentsInChildren<Transform>(true);
            var end = new Vector3[all.Length];
            foreach (var a in anims) { a.applyMaterialChannels = false; a.Hold(a.LengthMs); }
            for (int i = 0; i < all.Length; i++) end[i] = all[i].localScale;
            foreach (var a in anims) a.Hold(Mathf.Min(33f, a.LengthMs));
            parts = new List<(Transform, Vector3)>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != go.transform && (all[i].localScale - end[i]).sqrMagnitude > 1e-6f) parts.Add((all[i], all[i].localScale));
            foreach (var a in anims) { a.loop = true; a.loopStartMs = Mathf.Min(33f, a.LengthMs); a.Restart(); }
            go.SetActive(false);
            return go.transform;
        }

        Transform MakePulse()
        {
            var go = Instantiate(fx.projectile, root);
            go.name = "Mining beam pulse";
            GunRig.StripForFx(go);
            GunRig.EnableFades(go);
            go.SetActive(false);
            return go.transform;
        }

        void BuildContact()
        {
            if (fx != null && fx.impact != null && impacts == null)
            {
                impacts = new GameObject[3];
                impactLeft = new float[3];
                for (int i = 0; i < impacts.Length; i++)
                {
                    impacts[i] = Instantiate(fx.impact, root);
                    GunRig.StripForFx(impacts[i]);
                    GunRig.EnableFades(impacts[i]);
                    impacts[i].SetActive(false);
                }
                impactLength = Mathf.Max(200f, GunRig.MaxLength(impacts[0]));
            }
            if (sparks == null && particles != null) sparks = MakeParticles("Mining beam sparks", SparkCell, 200);
            if (trail == null && particles != null) trail = MakeParticles("Mining beam ore trail", OreGlowCell, 600);
        }

        ParticleSystem MakeParticles(string name, int cell, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            // Additive: fading to black fades it out.
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = ShipExhaust.CellQuad(cell);
            r.alignment = ParticleSystemRenderSpace.View;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = particles;
            ps.Play();
            return ps;
        }

        /// <summary>This frame's beam: from the mounts to 'beamEnd' (Unity world; a little inside the rock), the impact and
        /// sparks at 'contact' (its surface). 'reaching' false = out of range: the beams end in empty space.</summary>
        public void Aim(Vector3 beamEnd, Vector3 contact, Vector3 surfaceNormal, bool reaching)
        {
            BuildBeams();
            if (!On)
            {
                On = true;
                pulseTimer = 0f;
                impactTimer = 0f;
                var clip = fx != null ? fx.Shot : null;
                if (clip != null) sfx.PlayOneShot(Modding.ModSounds.Get(clip), 0.6f * Settings.SfxVolume);
                if (hum != null) { hum.volume = 0.55f * Settings.SfxVolume; hum.Play(); }
            }
            var model = ship.visualModel != null ? ship.visualModel : ship.transform;
            float dt = Time.deltaTime * 1000f * TimeExtender.PlayerFactor;
            pulseTimer -= dt;
            bool pulse = pulseTimer <= 0f;
            if (pulse) { pulseTimer = PulseMs; nextPulse = (nextPulse + 1) % 2; }
            foreach (var b in beams)
            {
                var from = model.TransformPoint(b.mount);
                var d = beamEnd - from;
                if (d.sqrMagnitude < 1e-4f) continue;
                var up = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.999f ? model.up : Vector3.up;
                var rot = Quaternion.LookRotation(d, up);
                var scale = new Vector3(1f, 1f, d.magnitude / M);
                if (b.core != null)
                {
                    if (!b.core.gameObject.activeSelf) b.core.gameObject.SetActive(true);
                    b.core.SetPositionAndRotation(from, rot);
                    b.core.localScale = scale;
                }
                foreach (var p in b.pulses)
                {
                    if (p == null) continue;
                    p.SetPositionAndRotation(from, rot);
                    p.localScale = scale;
                }
                if (pulse && b.pulses[nextPulse] != null)
                {
                    b.pulses[nextPulse].gameObject.SetActive(true);
                    PartAnimation.PlayOnce(b.pulses[nextPulse].gameObject);
                }
            }
            if (!reaching) return;
            impactTimer -= dt;
            if (impactTimer <= 0f && impacts != null)
            {
                impactTimer = ImpactMs;
                var go = impacts[nextImpact];
                impactLeft[nextImpact] = impactLength;
                nextImpact = (nextImpact + 1) % impacts.Length;
                go.SetActive(true);
                go.transform.position = contact;
                go.transform.localScale = Vector3.one;
                PartAnimation.PlayOnce(go);
            }
            if (sparks != null && UnityEngine.Random.value < dt / 25f) EmitSparks(contact, surfaceNormal, 2);
        }

        /// <summary>The beam is off (released, out of targets).</summary>
        public void Off()
        {
            if (!On) return;
            On = false;
            foreach (var b in beams)
            {
                if (b.core != null) b.core.gameObject.SetActive(false);
                // The pulses fade out by themselves.
            }
            if (hum != null) hum.Stop();
        }

        void EmitSparks(Vector3 at, Vector3 normal, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = (normal + UnityEngine.Random.insideUnitSphere * 0.9f).normalized;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = dir * UnityEngine.Random.Range(4000f, 12000f) * M,
                    startSize = UnityEngine.Random.Range(300f, 800f) * M,
                    startLifetime = UnityEngine.Random.Range(0.3f, 0.7f),
                    startColor = Color.Lerp(new Color(0.55f, 1f, 0.7f), Color.white, UnityEngine.Random.value),
                }, 1);
            }
        }

        // ---- the ore ------------------------------------------------------------------------------------------

        /// <summary>A ton of 'item' leaves the asteroid at 'contact' (its surface facing the ship along 'normal').</summary>
        public void Launch(Target asteroid, Vector3 contact, Vector3 normal, int item, bool core)
        {
            var c = spare.Count > 0 ? spare[spare.Count - 1] : NewChunk();
            if (spare.Count > 0) spare.RemoveAt(spare.Count - 1);
            SetRock(c, asteroid);
            c.item = item;
            c.core = core;
            c.size = ChunkSizeMeters * (core ? 1.8f : UnityEngine.Random.Range(0.7f, 1.3f));
            float spread = asteroid != null ? asteroid.radius * 0.15f : 2f;
            c.start = contact + UnityEngine.Random.insideUnitSphere * spread;
            // Thrown off the surface first, then pulled in.
            c.lift = (normal + UnityEngine.Random.insideUnitSphere * 0.6f).normalized * UnityEngine.Random.Range(40f, 110f);
            c.t = 0f;
            c.duration = 0.7f + (ship.transform.position - contact).magnitude / 900f;
            c.phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            c.spinAxis = UnityEngine.Random.onUnitSphere;
            c.spinSpeed = UnityEngine.Random.Range(180f, 540f);
            if (c.glow != null)
            {
                c.glow.GetComponent<MeshFilter>().sharedMesh = ShipExhaust.CellQuad(core ? CoreGlowCell : OreGlowCell);
                c.glow.localScale = Vector3.one * ChunkGlowMeters * (core ? 2.2f : 1f);
            }
            c.tr.position = c.start;
            c.tr.gameObject.SetActive(true);
            flying.Add(c);
            if (sparks != null) EmitSparks(contact, normal, core ? 12 : 3);
        }

        Chunk NewChunk()
        {
            var c = new Chunk { tr = new GameObject("Ore chunk").transform };
            c.tr.SetParent(root, false);
            c.rock = new GameObject("Rock").transform;
            c.rock.SetParent(c.tr, false);
            c.rock.gameObject.AddComponent<MeshFilter>();
            var mr = c.rock.gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (particles != null)
            {
                c.glow = new GameObject("Glow").transform;
                c.glow.SetParent(c.tr, false);
                c.glow.gameObject.AddComponent<MeshFilter>();
                var gr = c.glow.gameObject.AddComponent<MeshRenderer>();
                gr.sharedMaterial = particles;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gr.receiveShadows = false;
            }
            return c;
        }

        /// <summary>The chunk takes the asteroid's own mesh and materials (its first mesh: the full-detail LOD).</summary>
        void SetRock(Chunk c, Target asteroid)
        {
            var src = asteroid != null ? asteroid.GetComponentInChildren<MeshFilter>(true) : null;
            var mf = c.rock.GetComponent<MeshFilter>();
            var mr = c.rock.GetComponent<MeshRenderer>();
            if (src == null || src.sharedMesh == null) { mr.enabled = false; return; }
            mf.sharedMesh = src.sharedMesh;
            var srcR = src.GetComponent<MeshRenderer>();
            if (srcR != null) mr.sharedMaterials = srcR.sharedMaterials;
            mr.enabled = true;
            var ext = src.sharedMesh.bounds.extents;
            float big = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
            c.rock.localScale = Vector3.one * (big > 1e-4f ? 0.5f / big : 1f);   // a unit across; the chunk's scale sizes it
            c.rock.localPosition = -c.rock.localScale.x * src.sharedMesh.bounds.center;
        }

        void Update()
        {
            float dt = Time.deltaTime * TimeExtender.PlayerFactor;
            if (impacts != null)
            {
                var cam = Camera.main;
                for (int i = 0; i < impacts.Length; i++)
                {
                    if (impactLeft[i] <= 0f) continue;
                    impactLeft[i] -= dt * 1000f;
                    if (impactLeft[i] <= 0f) impacts[i].SetActive(false);
                    else if (cam != null) impacts[i].transform.rotation = cam.transform.rotation;   // "_lookat" meshes
                }
            }
            if (flying.Count == 0 || dt <= 0f) return;
            var camera = Camera.main;
            var target = ship.transform.position;
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var c = flying[i];
                c.t += dt / c.duration;
                if (c.t >= 1f)
                {
                    c.tr.gameObject.SetActive(false);
                    flying.RemoveAt(i);
                    spare.Add(c);
                    Arrived?.Invoke(c.item, c.core);
                    continue;
                }
                // A quadratic curve from the rock over the lift point into the ship, eased in (pulled ever faster), and a
                // spiral around it that closes as it arrives.
                float u = c.t * c.t;
                var p1 = c.start + c.lift;
                var p = (1f - u) * (1f - u) * c.start + 2f * (1f - u) * u * p1 + u * u * target;
                var axis = (target - c.start).normalized;
                var side = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up).normalized;
                var side2 = Vector3.Cross(axis, side);
                float a = c.phase + c.t * Mathf.PI * 4f, radius = SpiralMeters * Mathf.Sin(Mathf.PI * Mathf.Min(1f, c.t * 1.2f));
                p += (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * radius;
                c.tr.position = p;
                float shrink = c.t > 0.85f ? (1f - c.t) / 0.15f : 1f;
                c.tr.localScale = Vector3.one * c.size * shrink;
                c.rock.localRotation = Quaternion.AngleAxis(c.spinSpeed * dt, c.spinAxis) * c.rock.localRotation;
                if (c.glow != null && camera != null)
                {
                    c.glow.rotation = camera.transform.rotation;
                    c.glow.localScale = Vector3.one * ChunkGlowMeters * (c.core ? 2.2f : 1f) / Mathf.Max(0.01f, c.size);
                }
                if (trail != null && UnityEngine.Random.value < 0.7f)
                    trail.Emit(new ParticleSystem.EmitParams
                    {
                        position = p,
                        velocity = UnityEngine.Random.insideUnitSphere * 2f,
                        startSize = TrailSizeMeters * (c.core ? 1.8f : 1f) * shrink,
                        startLifetime = 0.35f,
                        startColor = c.core ? new Color(0.6f, 0.9f, 1f) : new Color(0.7f, 1f, 0.8f),
                    }, 1);
            }
        }

        void LateUpdate()
        {
            if (!On) return;
            // The core's width, steady: the animation would widen it with the shot's fade.
            foreach (var b in beams)
                if (b.coreParts != null)
                    foreach (var (tr, scale) in b.coreParts) if (tr != null) tr.localScale = new Vector3(scale.x * CoreWidth, scale.y * CoreWidth, scale.z);
        }
    }
}
