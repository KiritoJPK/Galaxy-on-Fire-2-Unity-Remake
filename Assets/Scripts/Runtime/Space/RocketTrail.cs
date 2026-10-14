// RocketTrail.cs
// The smoke trail behind a player rocket / missile / thermo shot (RocketGun::setRadar 0x18b2b0 / RocketGun::update
// 0x18b600; ParticleSystemMesh::emitTrail 0x1b6678 / setQuadEdge). The original gives each of the player's bullets its
// own mesh particle system on Level+0x80 (the particles.png manager, like the exhaust): a quad-strip trail whose next
// section starts every 'sectionMs' (emitTrail: +0x94 += dt up to the record's +0x28, with the emitter at least sqrt(6000)
// units from the last section's start; counted in units flown, the thermo trail hid inside its own 40 m projectile), the
// sections in a ring of 'pool' (the oldest reused, so the trail spans at most pool x sectionMs), each fading from its start to its end colour over
// the record's lifetime; two ribbons crossing at 45 deg (flags 0x1000 | 0x2000 | 0x20000), edges at +-size.
// ParticleSettings records (ParticleSettings::ParticleSettings, stride 0x9c):
//   39  rockets (sort 4) and missiles (5): size 100, every 125 ms, 29 sections, 3000 ms, white -> transparent,
//       particles.png (0.752, 0.002)-(0.998, 0.498): the white smoke strip
//   25-27  thermo guns 28 / 29 / 30 (25 also the cluster missiles, sort 40): size 50 / 100 / 150, every 50 ms, 25
//       sections, 1000 ms, the gold / red / purple strips (0, 0.625)-(0.125, 0.875) + 0.125 per record
//   28  SunFire o50 (193): on another manager, Level+0x98 (material 24096 v_projectiles.png, additive): size 70, every
//       50 ms, 25 sections, 1000 ms, its orange flame strip (0.75, 0)-(0.871, 0.5) (ParticleSettings::init 0x1b5fe6).
//   12  SET_MISSILE_TRAIL, every other RocketGun sort (BombGun inherits setRadar: the EMP bombs, AMR nukes, ionizing
//       missiles and the Shock Blast): not a ribbon but one sprite system on Level+0x84 (sprite_fire, additive) at the
//       bullet, flags 0x2000021 like record 42: 60/s, 1250 ms, size 250..299 +250/s, local velocity (0, 0, -6000), 700
//       behind the bullet, the 4 x 4 sheet, emitting while it lives (MissileTrail). The Liberator (179) has the system with
//       its emission off (RocketGun::update 0x18b8c4), so none here.
//   47  Fireworks (232): record 12 copied (0x1b6210) on Level+0x9c (material 27321 sn_sprite_fireworks_rocket_sparks.png,
//       additive), its own 4 x 4 sheet. #45: only the EMP bombs had a trail; the nukes and the fireworks had none.
// A launch resets the system; the bullet's death stops the emission and (sorts 4 / 5 / 40) the trail is drawn 2000 ms more
// (RocketGun+0xd4). NPC guns never get one (setRadar is the player's). Remake: one camera-facing ribbon instead of the
// crossed pair (the same from every side), each section showing the whole strip (mirrored every other section).

using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class RocketTrail
    {
        const float M = 0.05f;

        public sealed class Record
        {
            public float size, sectionMs, lifeMs, afterDeathMs;
            public int pool;
            public Rect uv;   // particles.png, GL (bottom-left) UVs
        }

        static readonly Record Rocket = new Record
            { size = 100f, sectionMs = 125f, pool = 29, lifeMs = 3000f, afterDeathMs = 2000f, uv = Rect.MinMaxRect(0.752f, 0.002f, 0.998f, 0.498f) };

        static Record Thermo(int k, float size, bool cluster) => new Record
            { size = size, sectionMs = 50f, pool = 25, lifeMs = 1000f, afterDeathMs = cluster ? 2000f : 1000f,
              uv = Rect.MinMaxRect(0.002f + 0.125f * k, 0.625f, 0.125f + 0.125f * k, 0.875f) };

        /// <summary>Record 28, the SunFire o50's flame ribbon on v_projectiles.png (MaterialFor).</summary>
        static readonly Record Sunfire = new Record
            { size = 70f, sectionMs = 50f, pool = 25, lifeMs = 1000f, afterDeathMs = 1000f, uv = Rect.MinMaxRect(0.75f, 0f, 0.871f, 0.5f) };

        /// <summary>The ribbon's material: the SunFire's on its own atlas (Level+0x98), every other on particles.png.</summary>
        public static Material MaterialFor(Gun gun)
        {
            var assets = CombatAssets.Load();
            if (assets == null) return null;
            return gun != null && gun.kind == Gun.Kind.Thermo && gun.lookIndex == 193 ? assets.sunfireTrailMaterial : assets.particlesMaterial;
        }

        /// <summary>The record for a player gun, null = no trail (RocketGun::setRadar's cases).</summary>
        public static Record For(Gun gun)
        {
            if (gun == null) return null;
            if (gun.kind == Gun.Kind.Rocket || gun.kind == Gun.Kind.Missile) return Rocket;
            if (gun.kind == Gun.Kind.ClusterMissile) return Thermo(0, 50f, true);
            if (gun.kind == Gun.Kind.Thermo)
                return gun.lookIndex switch { 28 => Thermo(0, 50f, false), 29 => Thermo(1, 100f, false), 30 => Thermo(2, 150f, false),
                                              193 => Sunfire, _ => null };
            return null;
        }

        /// <summary>The BombGun sorts with a record-12 / 47 sprite trail (not the Liberator, whose emission stays off).</summary>
        public static bool HasMissileTrail(Gun gun) =>
            gun != null && (gun.kind == Gun.Kind.EmpBomb || gun.kind == Gun.Kind.Nuke || gun.kind == Gun.Kind.Ionizing || gun.kind == Gun.Kind.ShockBlast)
            && gun.lookIndex != 179;

        /// <summary>Record 12 (setRadar's last case, addSystem(Level+0x84, bullet, 0xc)) for a bomb, record 47 on the fireworks'
        /// sparks for the Fireworks (232); null otherwise. Emission off: Play / Stop per launch, the pose set each frame.</summary>
        public static ParticleSystem MissileTrail(Gun gun, Transform parent)
        {
            var assets = CombatAssets.Load();
            var mat = !HasMissileTrail(gun) || assets == null ? null : gun.lookIndex == 232 ? assets.fireworksSparkMaterial : assets.fireMaterial;
            if (mat == null) return null;
            var ps = ShipSmoke.Create(parent, "Missile trail", mat, 1.25f, 250f, 299f, 76, 0f, Vector3.zero, -700f, 250f, 1);
            var main = ps.main;
            main.startSpeed = -6000f * M;   // +0x6c, along the bullet's -Z (the box shape emits along +Z)
            var em = ps.emission;
            em.rateOverTime = 60f;
            return ps;
        }

        readonly Record rec;
        readonly GameObject go;
        readonly Mesh mesh;
        readonly Vector3[] points;     // ring, oldest at 'first'
        readonly float[] born;         // ms
        int first, count;
        float sinceSection, clock, deadFor = -1f;
        Vector3 last;
        Vector3[] verts;
        Vector2[] uvs;
        Color32[] cols;
        int[] tris;

        public RocketTrail(Record record, Transform parent, Material material)
        {
            rec = record;
            points = new Vector3[rec.pool];
            born = new float[rec.pool];
            go = new GameObject("Rocket trail");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            mesh = new Mesh { name = "Rocket trail" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            int n = rec.pool + 1;   // the sections plus the rocket itself
            verts = new Vector3[n * 2];
            uvs = new Vector2[n * 2];
            cols = new Color32[n * 2];
            tris = new int[(n - 1) * 6];
            for (int i = 0; i < n - 1; i++)
            {
                int v = i * 2, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            go.SetActive(false);
        }

        /// <summary>A launch at 'position' (resetSystem + enableSystemEmit).</summary>
        public void Restart(Vector3 position)
        {
            first = count = 0;
            sinceSection = 0f;
            deadFor = -1f;
            last = position;
            Push(position);
            go.SetActive(true);
        }

        /// <summary>The bullet gone: no new sections, drawn for the record's after-death time.</summary>
        public void Stop() { if (go.activeSelf && deadFor < 0f) deadFor = 0f; }

        public void Clear() { count = 0; deadFor = -1f; go.SetActive(false); }

        void Push(Vector3 p)
        {
            if (count == rec.pool) { first = (first + 1) % rec.pool; count--; }   // the oldest section reused
            int i = (first + count) % rec.pool;
            points[i] = p;
            born[i] = clock;
            count++;
        }

        /// <summary>Per frame: 'alive' with the bullet at 'position'; the ribbon faces 'cam'.</summary>
        public void Tick(float dtMs, bool alive, Vector3 position, Camera cam)
        {
            if (!go.activeSelf) return;
            clock += dtMs;
            if (deadFor >= 0f)
            {
                deadFor += dtMs;
                if (deadFor >= rec.afterDeathMs) { Clear(); return; }
            }
            else if (alive)
            {
                // emitTrail: a new section once +0x94 (dt summed) reaches the record's +0x28 ms and the emitter is at least
                // sqrt(6000) units from the last section's start (+0x80); meanwhile the head follows the bullet (Build).
                sinceSection += dtMs;
                if (sinceSection >= rec.sectionMs && (position - last).sqrMagnitude >= 6000f * M * M)
                {
                    Push(position);
                    last = position;
                    sinceSection = 0f;
                }
            }
            while (count > 0 && clock - born[first] >= rec.lifeMs) { first = (first + 1) % rec.pool; count--; }
            Build(deadFor < 0f && alive ? position : (Vector3?)null, cam);
        }

        void Build(Vector3? head, Camera cam)
        {
            int n = count + (head.HasValue ? 1 : 0);
            if (n < 2 || cam == null) { mesh.Clear(); return; }
            var camPos = cam.transform.position;
            float half = rec.size * M;
            Vector3 P(int k) => k < count ? points[(first + k) % rec.pool] : head.Value;
            float Age(int k) => k < count ? clock - born[(first + k) % rec.pool] : 0f;
            for (int k = 0; k < n; k++)
            {
                var p = P(k);
                var t = P(Mathf.Min(k + 1, n - 1)) - P(Mathf.Max(k - 1, 0));
                var side = Vector3.Cross(t, camPos - p);
                side = side.sqrMagnitude > 1e-8f ? side.normalized * half : Vector3.zero;
                verts[k * 2] = p - side;
                verts[k * 2 + 1] = p + side;
                // the whole strip per section, mirrored every other one; u across, v along
                float v = (k & 1) == 0 ? rec.uv.yMin : rec.uv.yMax;
                uvs[k * 2] = new Vector2(rec.uv.xMin, v);
                uvs[k * 2 + 1] = new Vector2(rec.uv.xMax, v);
                // FFFFFFFF -> 00000000 over the life (additive: the RGB fades too)
                byte c = (byte)(255f * Mathf.Clamp01(1f - Age(k) / rec.lifeMs));
                cols[k * 2] = cols[k * 2 + 1] = new Color32(c, c, c, c);
            }
            for (int k = n; k < verts.Length / 2; k++) verts[k * 2] = verts[k * 2 + 1] = verts[(n - 1) * 2];
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors32 = cols;
            mesh.SetTriangles(tris, 0, (n - 1) * 6, 0);
            mesh.RecalculateBounds();
        }

        public void Destroy()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
        }
    }
}
