// WreckBurn.cs
// The explosions around a big wreck (Reference/research/prologue_particles.md B.3 / B.4), sprite_explosion (material
// 20099, additive), both on the Level+0x74 manager:
//   record 22 SET_EXPLOSION_CARGO        Level+0x50: freighters and other fixed objects; pool 1000, size 4000..6000
//                                        +500/s, 1000 ms, 9/s, jitter +-3000 (x, z) / +-800 (y), along dir -250..+249
//   record 23 SET_EXPLOSION_BATTLESHIP   Level+0x54: the battleship (ship 14) and the Pirate Outpost (0x37a3); pool 16,
//                                        size 4000..5000, jitter +-5000 / +-1500; also LevelScript 0x91's plasma array
//   record 24 SET_EXPLOSION_DEEP_SCIENCE_STATION  Level+0x58 / +0x5c, only in mission 0x50 (Kothar's deep science
//                                        station hit by the battlestation's laser, never switched off): record 23 with
//                                        size 2000..7000, 5/s, jitter +-1000 / +-3000, along dir -250
// PlayerFixedObject::update puts the system on the wreck (systemSetMatrix once) when it dies and switches it off when
// the wreck animation ends (state 4). Plain C#: the owner (NpcShip, a level script) switches it.

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class WreckBurn
    {
        readonly ParticleSystem system;
        public bool Emitting { get; private set; }

        public const int Cargo = 22, Battleship = 23, DeepScience = 24, VosskFreighterSmoke = 40, VosskCargo = 41;

        public WreckBurn(Transform at, bool battleship) : this(at, battleship ? Battleship : Cargo) { }

        public WreckBurn(Transform at, int record)
        {
            var mat = CombatAssets.Load()?.explosionSpriteMaterial;
            switch (record)
            {
                case Battleship:
                    system = ShipSmoke.Create(at, "Battleship burn", mat, 1f, 4000f, 5000f, 16, 1f, new Vector3(10000f, 3000f, 10500f), 0f, 500f, 2);
                    break;
                case DeepScience:
                    system = ShipSmoke.Create(at, "Deep science burn", mat, 1f, 2000f, 7000f, 16, 1f, new Vector3(2000f, 6000f, 2000f), -250f, 500f, 2);
                    break;
                case VosskCargo:
                    // Record 41 SET_EXPLOSION_VOSSK_CARGO (step 41's hit freighter): size 4000..6000 +500/s, 1000 ms, 9/s, jitter
                    // +-1000 / +-800, along the hull -4000..+3999.
                    system = ShipSmoke.Create(at, "Vossk cargo burn", mat, 1f, 4000f, 6000f, 1000, 1f, new Vector3(2000f, 1600f, 8000f), 0f, 500f, 2);
                    break;
                case VosskFreighterSmoke:
                {
                    // Record 40 SET_SMOKE_VOSSK_FREIGHTER: size 1800..2100 +2222/s, 1500 ms, 20/s, fading in over 200 ms and
                    // out to nothing, jitter +-200, +500..+999 along, -2 x the freighter's velocity: the trail behind it.
                    system = ShipSmoke.Create(at, "Vossk freighter smoke", mat, 1.5f, 1800f, 2100f, 30, -2f, new Vector3(400f, 400f, 500f), 750f, 2222f, 2);
                    var col = system.colorOverLifetime;
                    col.enabled = true;
                    var g = new Gradient();
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                              new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.87f, 0.133f), new GradientAlphaKey(0f, 1f) });
                    col.color = g;
                    break;
                }
                default:
                    system = ShipSmoke.Create(at, "Wreck burn", mat, 1f, 4000f, 6000f, 1000, 1f, new Vector3(6000f, 1600f, 6500f), 0f, 500f, 2);
                    break;
            }
            var em = system.emission;
            em.rateOverTime = record == DeepScience ? 5f : record == VosskFreighterSmoke ? 20f : 9f;
        }

        /// <summary>enableSystemEmit: new explosions on / off; the living ones burn out.</summary>
        public void SetEmitting(bool on)
        {
            on &= Settings.QualityEffects;
            if (on == Emitting || system == null) return;
            Emitting = on;
            if (on) system.Play(true); else system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
