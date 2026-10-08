// GunRig.cs
// The visuals of one Gun (weapons.md 5): a projectile instance per bullet slot (ObjectGun), the muzzle flash on the
// mount, a small pool of impact effects. Plain C#, shared by the player's WeaponSystem and the NPC ships.
// Blasters and thermo guns face the camera; lasers, cannons and rockets point along their flight. Projectiles shrink over
// their last 1000 ms (Gun.VisualScale). Effects never cast shadows or cull by LOD.
// Beams (BeamGun::update 0x1a6b04): one mesh 1 unit long along +Z, scaled (1, 1, beam length) along the direction chosen
// at fire time, following the mount, its animation restarted per shot and hidden when it ends. Mines are drawn at x0.7
// and tumble (MineGun). Scatter shells have no impact mesh (their burst explosion replaces it).
// The guided Liberator (BombGun, attr 15): its deploy animation plays once per launch, starting 500 ms after it.
// The player's rockets, missiles and thermo shots trail smoke, the bombs fire sprites, the fireworks sparks (RocketTrail,
// EnableTrails; NPC guns have none).
// Projectiles, muzzle flashes and impacts fade by their `extra` (opacity) channel: without it an impact's big glow part
// (radius ~3600 units, meant at 20 % and gone after 267 ms) stayed at full brightness and the impact looked far too big.

using GoF2Remake.Visuals;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class GunRig
    {
        public readonly Gun gun;
        public readonly WeaponFx fx;
        readonly Transform[] projectiles;
        readonly Transform ship;
        float beamMs, beamLength;
        Vector3[] spin;
        bool[] wasActive;
        PartAnimation[][] projAnims;
        readonly bool billboard;
        readonly GameObject muzzle;
        readonly float muzzleLength;
        float muzzleMs;
        readonly GameObject[] impacts;
        readonly float[] impactMs;
        readonly float impactLength;
        int nextImpact;
        readonly Transform fxRoot;
        RocketTrail[] trails;
        ParticleSystem[] flames;   // bombs: record 12, fireworks 47 (RocketTrail.MissileTrail)

        /// <param name="fxRoot">Parent of the projectiles and impacts (world space).</param>
        /// <param name="muzzleParent">The ship (null = no muzzle flash).</param>
        public GunRig(Gun gun, WeaponFx fx, Transform fxRoot, Transform muzzleParent, int impactPool = 4)
        {
            this.gun = gun;
            this.fx = fx;
            this.fxRoot = fxRoot;
            ship = muzzleParent;
            billboard = gun.kind == Gun.Kind.Blaster || gun.kind == Gun.Kind.Thermo;
            projectiles = new Transform[gun.bullets.Length];
            if (fx != null && fx.projectile != null)
                for (int i = 0; i < projectiles.Length; i++)
                {
                    var go = Object.Instantiate(fx.projectile, fxRoot);
                    go.name = $"{fx.projectile.name} {i}";
                    StripForFx(go);
                    EnableFades(go);
                    go.SetActive(false);
                    projectiles[i] = go.transform;
                }
            if (fx != null && fx.muzzleFlash != null && muzzleParent != null)
            {
                muzzle = Object.Instantiate(fx.muzzleFlash, muzzleParent, false);
                muzzle.transform.localPosition = gun.mountLocal;
                StripForFx(muzzle);
                EnableFades(muzzle);
                muzzleLength = Mathf.Max(80f, MaxLength(muzzle));
                muzzle.SetActive(false);
            }
            wasActive = new bool[projectiles.Length];
            projAnims = new PartAnimation[projectiles.Length][];
            for (int i = 0; i < projectiles.Length; i++)
                projAnims[i] = projectiles[i] != null ? projectiles[i].GetComponentsInChildren<PartAnimation>(true) : new PartAnimation[0];
            if (gun.kind == Gun.Kind.Mine)
            {
                // MineGun: a random tumble per mine, (rnd(200) - 100) / 50 per axis (read as rad/s).
                spin = new Vector3[projectiles.Length];
                for (int i = 0; i < spin.Length; i++)
                    spin[i] = new Vector3(Random.Range(0, 200) - 100, Random.Range(0, 200) - 100, Random.Range(0, 200) - 100) / 50f;
            }
            if (gun.isBeam && projectiles.Length > 0 && projectiles[0] != null) beamLength = Mathf.Max(200f, MaxLength(projectiles[0].gameObject));
            if (fx != null && fx.impact != null && impactPool > 0 && gun.kind != Gun.Kind.ScatterGun)
            {
                impacts = new GameObject[impactPool];
                impactMs = new float[impactPool];
                for (int i = 0; i < impactPool; i++)
                {
                    impacts[i] = Object.Instantiate(fx.impact, fxRoot);
                    StripForFx(impacts[i]);
                    EnableFades(impacts[i]);
                    impacts[i].SetActive(false);
                }
                impactLength = Mathf.Max(200f, MaxLength(impacts[0]));
            }
        }

        /// <summary>RocketGun::setRadar (the player's guns, and other players' mirrored shots): one smoke trail per bullet
        /// for rockets, missiles, cluster missiles and thermo guns, a fire-sprite system for EMP bombs.</summary>
        public void EnableTrails()
        {
            if (flames == null && RocketTrail.HasMissileTrail(gun))
            {
                flames = new ParticleSystem[gun.bullets.Length];
                for (int i = 0; i < flames.Length; i++) flames[i] = RocketTrail.MissileTrail(gun, fxRoot);
            }
            var rec = RocketTrail.For(gun);
            if (rec == null || trails != null) return;
            var mat = RocketTrail.MaterialFor(gun);
            if (mat == null) return;
            trails = new RocketTrail[gun.bullets.Length];
            for (int i = 0; i < trails.Length; i++) trails[i] = new RocketTrail(rec, fxRoot, mat);
        }

        public static float MaxLength(GameObject go)
        {
            float l = 0f;
            foreach (var a in go.GetComponentsInChildren<PartAnimation>(true)) l = Mathf.Max(l, a.LengthMs);
            return Mathf.Max(l, Modding.ModFxPart.MaxLength(go));
        }

        /// <summary>The part animations apply their `extra` channel as opacity (PartAnimation.applyMaterialChannels).</summary>
        public static void EnableFades(GameObject go)
        {
            foreach (var a in go.GetComponentsInChildren<PartAnimation>(true)) a.applyMaterialChannels = true;
        }

        public static void StripForFx(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            foreach (var lg in go.GetComponentsInChildren<LODGroup>(true)) lg.enabled = false;
        }

        /// <summary>A shot left the gun: the muzzle flash.</summary>
        public void OnShot()
        {
            if (gun.isBeam && projectiles.Length > 0 && projectiles[0] != null)
            {
                projectiles[0].gameObject.SetActive(true);
                PartAnimation.PlayOnce(projectiles[0].gameObject);
                beamMs = beamLength;
            }
            if (muzzle == null) return;
            muzzle.SetActive(true);
            PartAnimation.PlayOnce(muzzle);
            muzzleMs = muzzleLength;
        }

        /// <summary>A bullet hit something at 'point'.</summary>
        public void ShowImpact(Vector3 point)
        {
            // Gun::calcCharacterCollision: rockets, missiles and cluster missiles have no impact mesh; a hit emits record 11
            // SET_EXPLOSION_MANUALLY_BIG at the bullet (one sprite_explosion particle, 1500 ms, 2000..3000 units, +500/s).
            if (gun.kind == Gun.Kind.Rocket || gun.kind == Gun.Kind.Missile || gun.kind == Gun.Kind.ClusterMissile)
            {
                ShipBurn.ManualBurst(point, 1.5f, 2000f, 3000f, 500f);
                return;
            }
            if (impacts == null) return;
            int i = nextImpact;
            nextImpact = (nextImpact + 1) % impacts.Length;
            var go = impacts[i];
            go.SetActive(true);
            go.transform.position = point;
            PartAnimation.PlayOnce(go);
            impactMs[i] = impactLength;
        }

        public void UpdateVisuals(float dtMs, Camera cam, Vector3 fallbackForward)
        {
            if (gun.isBeam)
            {
                UpdateBeam(dtMs);
                projectilesDone(dtMs, cam);
                return;
            }
            for (int i = 0; i < projectiles.Length; i++)
            {
                var t = projectiles[i];
                if (t == null) continue;
                bool active = gun.IsActive(i);
                if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
                bool launched = active && !wasActive[i];
                if (trails != null)
                {
                    if (launched) trails[i].Restart(gun.bullets[i].position);
                    else if (!active && wasActive[i]) trails[i].Stop();
                }
                if (flames?[i] != null && !active && wasActive[i]) flames[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                wasActive[i] = active;
                if (!active) { trails?[i].Tick(dtMs, false, default, cam); continue; }
                ref var b = ref gun.bullets[i];
                // BombGun::BombGun 0x170e74: the EMP bomb (mesh 0x395c) loops its glow child 0x395d (SetAnimationState 2):
                // the core strobes and the cyan sparks pulse while it flies.
                if (launched && gun.kind == Gun.Kind.EmpBomb)
                    foreach (var a in projAnims[i]) { a.loop = true; a.Restart(); }
                if (gun.Guided)
                {
                    // BombGun::update: restarted on launch (state 3 -> 1), advancing only after the first 500 ms, once.
                    if (launched) PartAnimation.PlayOnce(t.gameObject);
                    float sp = b.age >= 500f ? 1f : 0f;
                    foreach (var a in projAnims[i]) a.speed = sp;
                }
                var rot = billboard && cam != null
                    ? cam.transform.rotation
                    : Quaternion.LookRotation(b.velocity.sqrMagnitude > 1e-9f ? b.velocity : fallbackForward, b.up);
                if (spin != null) rot = Quaternion.Euler(spin[i] * (b.age * 0.001f * Mathf.Rad2Deg));
                t.SetPositionAndRotation(b.position, rot);
                t.localScale = Vector3.one * gun.VisualScale(i) * (spin != null ? 0.7f : 1f);
                trails?[i].Tick(dtMs, true, b.position, cam);
                if (flames?[i] != null)
                {
                    flames[i].transform.SetPositionAndRotation(b.position, rot);
                    if (launched) flames[i].Play(true);
                }
            }
            projectilesDone(dtMs, cam);
        }

        /// <summary>The beam: at the mount, re-aimed every frame, scaled to its length (units).</summary>
        void UpdateBeam(float dtMs)
        {
            var t = projectiles.Length > 0 ? projectiles[0] : null;
            if (t == null) return;
            if (beamMs <= 0f) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); return; }
            beamMs -= dtMs;
            // BeamGun::update: at ship + R * (mount + (0, 0, -100)) when the gun has a mount (5 m behind it).
            var from = ship != null ? ship.TransformPoint(gun.mountLocal == Vector3.zero ? Vector3.zero
                                                          : gun.mountLocal + new Vector3(0f, 0f, -100f * Gun.MetersPerUnit))
                                    : gun.bullets[0].position;
            // Remake: re-aimed every frame, at the locked target while it lives, else straight ahead. The original keeps the
            // fire-time direction and length (Gun::shootAt writes +0x8c / +0x90 once; BeamGun::update only reads them), so
            // its beam trailed off the nose through a turn for the second it shows.
            var dir = gun.BeamDir.sqrMagnitude > 1e-9f ? gun.BeamDir : Vector3.forward;
            float length = gun.BeamLengthUnits;
            var target = gun.BeamTarget;
            if (target != null && target.gameObject.activeInHierarchy)
            {
                var d = target.position - from;
                if (d.sqrMagnitude > 1e-6f) { dir = d.normalized; length = d.magnitude / Gun.MetersPerUnit; }
            }
            else if (ship != null) { dir = ship.forward; length = Gun.BeamRangeUnits; }
            // setDirection(beamDir, up (0, 1, 0)), world up (the crossed planes don't roll with the ship); a beam fired
            // straight up / down takes the ship's up instead.
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.999f && ship != null ? ship.up : Vector3.up;
            t.SetPositionAndRotation(from, Quaternion.LookRotation(dir, up));
            t.localScale = new Vector3(1f, 1f, length);
            if (beamMs <= 0f) t.gameObject.SetActive(false);
        }

        void projectilesDone(float dtMs, Camera cam)
        {
            if (muzzle != null && muzzleMs > 0f)
            {
                muzzleMs -= dtMs;
                if (muzzleMs <= 0f) muzzle.SetActive(false);
                // ObjectGun::update 0x18c2ec: along the ship's nose with the camera's up (turrets: the turret's matrix).
                else if (gun.kind != Gun.Kind.Turret && ship != null && cam != null)
                    muzzle.transform.rotation = Quaternion.LookRotation(ship.forward, cam.transform.up);
            }
            if (impacts != null)
                for (int i = 0; i < impacts.Length; i++)
                {
                    if (impactMs[i] <= 0f) continue;
                    impactMs[i] -= dtMs;
                    if (impactMs[i] <= 0f) impacts[i].SetActive(false);
                    else if (cam != null) impacts[i].transform.rotation = cam.transform.rotation;   // "_lookat" meshes
                }
        }

        /// <summary>Hides every projectile (a dead or sleeping ship).</summary>
        public void HideAll()
        {
            foreach (var t in projectiles) if (t != null) t.gameObject.SetActive(false);
            for (int i = 0; i < gun.bullets.Length; i++) gun.bullets[i].timer = -1e9f;
            if (trails != null) foreach (var tr in trails) tr.Clear();
            if (flames != null) foreach (var f in flames) if (f != null) f.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
