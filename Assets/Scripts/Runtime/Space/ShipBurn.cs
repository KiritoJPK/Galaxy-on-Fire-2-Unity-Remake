// ShipBurn.cs
// A dying ship's burn (Reference/research/prologue_particles.md B.2 / B.4): sprite_explosion (material 20099, additive).
//   record 9  SET_EXPLOSION                 PlayerEgo::explode 0xada6c (every frame while dying) / PlayerFighter: 8/s,
//                                           700 ms, size 100..1100 +500/s, white, jitter +-300 world, along the ship axis
//                                           -250..+249, k = +1 (inherits the ship's velocity), 4x4 sheet once; on through the
//                                           death tumble, off at the explosion
//   record 11 SET_EXPLOSION_MANUALLY_BIG    one emitManual at the explosion: one particle (IParticleSystem::emitManual
//                                           0x1b3174 writes a single slot), 1500 ms, size 2000..3000
//                                           +500/s, no jitter
// Plain C#: the owner (PlayerHealth, NpcShip) switches it.

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class ShipBurn
    {
        readonly ParticleSystem burn, burst;
        public bool Emitting { get; private set; }

        public ShipBurn(Transform ship)
        {
            var mat = CombatAssets.Load()?.explosionSpriteMaterial;
            // record 9: along dir -250..+249 plus +-300 jitter -> a 600 x 600 x 1100 unit box at the ship
            burn = ShipSmoke.Create(ship, "Burn", mat, 0.7f, 100f, 1100f, 16, 1f, new Vector3(600f, 600f, 1100f), 0f, 500f, 2);
            var em = burn.emission;
            em.rateOverTime = 8f;
            // record 11: a single burst at the ship
            burst = ShipSmoke.Create(ship, "Burst", mat, 1.5f, 2000f, 3000f, 1, 0f, Vector3.zero, 0f, 500f, 3);
            var bem = burst.emission;
            bem.rateOverTime = 0f;
            var sh = burst.shape;
            sh.enabled = false;
            var main = burst.main;
            main.loop = false;
        }

        /// <summary>Record 9 on / off (the living particles burn out).</summary>
        public void SetBurning(bool on)
        {
            on &= Settings.QualityEffects;
            if (on == Emitting) return;
            Emitting = on;
            if (on) burn.Play(true); else burn.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>IParticleSystem::emitManual 0x1b3174: one sprite_explosion particle (the 4x4 sheet once, additive) at
        /// 'at', size minSize..maxSize units growing growthPerS, for lifeS; e.g. record 0x15 SET_EXPLOSION_MANUALLY_JUNK
        /// (PlayerJunk::update: 1000 ms, 1600 + rnd(200), +500/s).</summary>
        public static void ManualBurst(Vector3 at, float lifeS, float minSize, float maxSize, float growthPerS)
        {
            // Remake: one emitter per kind of burst in the active scene (gone with it), each particle emitted where it went
            // off (world space); every rocket hit built a particle system of its own and destroyed it again.
            var key = (lifeS, minSize, maxSize, growthPerS);
            if (!bursts.TryGetValue(key, out var ps) || ps == null)
            {
                if (burstHolder == null) { burstHolder = new GameObject("Bursts"); bursts.Clear(); }
                var mat = CombatAssets.Load()?.explosionSpriteMaterial;
                ps = ShipSmoke.Create(burstHolder.transform, "Burst", mat, lifeS, minSize, maxSize, BurstParticles - 1, 0f, Vector3.zero, 0f, growthPerS, 3);
                var em = ps.emission;
                em.rateOverTime = 0f;
                var sh = ps.shape;
                sh.enabled = false;
                var main = ps.main;
                main.loop = false;
                bursts[key] = ps;
            }
            ps.Emit(new ParticleSystem.EmitParams { position = at, applyShapeToPosition = false }, 1);
        }

        const int BurstParticles = 64;   // at once per kind (1.5 s each): a cluster salvo's hits and more
        static readonly System.Collections.Generic.Dictionary<(float, float, float, float), ParticleSystem> bursts =
            new System.Collections.Generic.Dictionary<(float, float, float, float), ParticleSystem>();
        static GameObject burstHolder;   // null again once its scene is gone

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { bursts.Clear(); burstHolder = null; }

        /// <summary>Record 11's emitManual burst at the explosion (it stays where it went off).</summary>
        public void Burst()
        {
            if (!Settings.QualityEffects || burst == null) return;
            burst.transform.SetParent(null, true);
            burst.Emit(1);
            Object.Destroy(burst.gameObject, 2f);
        }
    }
}
