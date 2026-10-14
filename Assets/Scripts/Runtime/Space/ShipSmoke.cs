// ShipSmoke.cs
// The smoke and fire sprite systems of a burning ship (Reference/research/prologue_particles.md A, B.2): the prologue's
// broken Phantom (PlayerEgo::setLevel 0xa6f90 / startSmokeEmission 0xadc20) and NPC fighters below a third of their hull
// (PlayerFighter::setLevel 0xf0740 / update 0xf1b0e). Both use the same two ParticleSettings records:
//   15 SET_SMOKE_ENEMY       sprite_smoke (alpha), 20/s, 1500 ms, size 666..887 +2222/s, white alpha 255 -> 0 with a
//                            200 ms fade-in, velocity -2 x the ship's, spawned along the ship axis -250..+250, +-200 world
//   42 SET_SMOKE_FIRE_ENEMY  sprite_fire (additive ONE/ONE: the vertex alpha has no effect), 20/s, 600 ms, size 400..599
//                            +222/s, velocity -1 x the ship's, spawned 200..400 toward the nose, +-100 world
// Both play their 4x4 sprite sheet once over the lifetime, randomly mirrored per particle, in world space
// (ParticleSystemSprite::updateSingle 0x1b3cf0, IParticleSystem::emit 0x1b2790). Sizes are the sprite's full edge.
// The ship-aligned emission boxes stand in for the original's axis line plus world-axis jitter.

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class ShipSmoke
    {
        const float M = 0.05f;

        readonly ParticleSystem smoke, fire;
        public bool Emitting { get; private set; }

        /// <summary>Adds the two systems (emission off) under 'ship', whose +Z is the nose.</summary>
        public ShipSmoke(Transform ship)
        {
            var assets = CombatAssets.Load();
            // record 15: along dir -250..+250, jitter +-200 -> box 400 x 400 x 500 units at the origin
            smoke = Create(ship, "Smoke", assets != null ? assets.smokeMaterial : null, 1.5f, 666f, 887f, 30, -2f,
                           new Vector3(400f, 400f, 500f), 0f, 2222f, 0);
            // record 42: along dir +200..+400, jitter +-100 -> box 200 x 200 x 200 units, 300 toward the nose
            fire = Create(ship, "Fire", assets != null ? assets.fireMaterial : null, 0.6f, 400f, 599f, 12, -1f,
                          new Vector3(200f, 200f, 200f), 300f, 222f, 1);
            // Colour: smoke fades in over 200 ms (0.133 of the life) to the linear 255 -> 0 fade; the fire stays white
            // (ONE/ONE ignores alpha, only its atlas darkens).
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.467f, 0.0667f),
                              new GradientAlphaKey(0.867f, 0.1333f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        internal static ParticleSystem Create(Transform ship, string name, Material material, float lifeS, float minSize, float maxSize,
                                     int pool, float inherit, Vector3 boxUnits, float noseUnits, float growthPerS, int order)
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
            main.startSize = new ParticleSystem.MinMaxCurve(minSize * M, maxSize * M);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.maxParticles = pool + 1;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // the trail keeps going off screen

            var em = ps.emission;
            em.rateOverTime = 20f;

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = boxUnits * M;
            sh.position = new Vector3(0f, 0f, noseUnits * M);

            // v = k x emitter velocity, fixed at spawn
            var iv = ps.inheritVelocity;
            iv.enabled = true;
            iv.mode = ParticleSystemInheritVelocityMode.Initial;
            iv.curve = new ParticleSystem.MinMaxCurve(inherit);

            // size(age) = start + growth x age: a linear multiplier from 1 to (mean start + growth x life) / mean start
            float mean = (minSize + maxSize) * 0.5f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, (mean + growthPerS * lifeS) / mean));

            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = 4;
            tsa.numTilesY = 4;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.cycleCount = 1;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.flip = new Vector3(0.5f, 0.5f, 0f);
            r.maxParticleSize = 10f;
            r.sortingOrder = order;   // Level::render: the smoke before the fire
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (material != null) r.sharedMaterial = material;
            return ps;
        }

        /// <summary>enableSystemEmit: new particles on / off; the living ones burn out.</summary>
        public void SetEmitting(bool on)
        {
            on &= Settings.QualityEffects;   // Quality Low / Medium: "Smoke off" (510 / 511)
            if (on == Emitting) return;
            Emitting = on;
            foreach (var ps in new[] { smoke, fire })
            {
                if (ps == null) continue;
                if (on) ps.Play(true); else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Emission off and every particle gone (the ship vanished or was reset).</summary>
        public void Clear()
        {
            Emitting = false;
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
