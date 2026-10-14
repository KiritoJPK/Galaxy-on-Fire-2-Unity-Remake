// EmpSparks.cs
// The lightning of an EMP-disabled ship (Reference/research/combat_equipment.md 4.1, prologue_particles.md B.2 / B.4):
// PlayerFighter::update 0xf3138 lets the particle systems +0x130 / +0x134 (records 17 SET_EMP_1 and 18 SET_EMP_2) emit
// while KIPlayer+0x20 is set and stops them on recovery and on death. Both: material 27260 (khador_jump.png, additive),
// one static lightning bolt each (EMP_1 the small bolt (0.877, 0.252)-(0.997, 0.497), EMP_2 the big one
// (0.752, 0.502)-(0.997, 0.747)), pool 13, 8/s, size 200..1600 units +500/s, life 400 / 200 ms, colour white -> black
// on RGB with a 300 ms fade-in (brightness (1 - age/life) * min(1, age/300)), velocity +1 x the ship's, spawned along
// the ship axis -300..+499 with +-600 / +-300 jitter, randomly mirrored.

using UnityEngine;

namespace GoF2Remake.Flight
{
    public class EmpSparks
    {
        const float M = 0.05f;

        readonly ParticleSystem[] systems;
        public bool Emitting { get; private set; }

        public EmpSparks(Transform ship)
        {
            var assets = CombatAssets.Load();
            var material = assets != null ? assets.empSparkMaterial : null;
            // Grid tiles of khador_jump.png (row 0 = top): the small bolt = 8 x 4 tile 15, the big bolt = 4 x 4 tile 11.
            systems = new[]
            {
                Create(ship, "EMP 1", material, 0.4f, 8, 4, 15, 0.33f),
                Create(ship, "EMP 2", material, 0.2f, 4, 4, 11, 0.17f),
            };
        }

        static ParticleSystem Create(Transform ship, string name, Material material, float lifeS, int tilesX, int tilesY, int tile, float peak)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ship, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = lifeS;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(200f * M, 1600f * M);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.maxParticles = 13;
            main.cullingMode = ParticleSystemCullingMode.Pause;

            var em = ps.emission;
            em.rateOverTime = 8f;

            // along dir -300..+499, jitter +-600 (x, z) / +-300 (y) -> box 1200 x 600 x 800 units, centred 100 toward the nose
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(1200f, 600f, 800f) * M;
            sh.position = new Vector3(0f, 0f, 100f * M);

            var iv = ps.inheritVelocity;
            iv.enabled = true;
            iv.mode = ParticleSystemInheritVelocityMode.Initial;
            iv.curve = new ParticleSystem.MinMaxCurve(1f);

            float mean = 900f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, (mean + 500f * lifeS) / mean));

            // Additive: the RGB brightness is the visibility (white -> black times the 300 ms fade-in).
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.white * peak, 0.5f), new GradientColorKey(Color.black, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            col.color = g;

            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = tilesX;
            tsa.numTilesY = tilesY;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve((tile + 0.5f) / (tilesX * tilesY));   // one fixed bolt
            tsa.cycleCount = 1;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.flip = new Vector3(0.5f, 0.5f, 0f);
            r.maxParticleSize = 10f;
            r.sortingOrder = 2;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (material != null) r.sharedMaterial = material;
            return ps;
        }

        public void SetEmitting(bool on)
        {
            if (on == Emitting) return;
            Emitting = on;
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                if (on) ps.Play(true); else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void Clear()
        {
            Emitting = false;
            foreach (var ps in systems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
