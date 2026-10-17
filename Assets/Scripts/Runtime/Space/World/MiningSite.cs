// MiningSite.cs
// Remake (players' feedback, 2026-10-17: the plant 5-6 km in front of the station felt out of place): an Ore Mining
// mission's site, a remote spot of its orbit about 10 km from the station (the user's pick, 2026-10-17: 40-60 km took too
// long to fly, as a session's fast-forward keeps the time at x1) with the mining plant, its guards and haulers and
// an asteroid field of its own around it (FreelanceOrbit, OrbitBuilder.SpawnField). Everything is drawn from one seed
// (the mission's net id, else its own values, and the orbit's shared seed NetSession.OrbitSeed), so every game of the
// squad builds the same site and field. The player gets its coordinates as the "Mining site" waypoint, an approach
// point short of the plant on the station's side; the field leaves the corridor from there to the plant clear. The
// direction is drawn again (up to 32 times, else the clearest one) until the site's field keeps clear of the orbit's own
// field and of its jumpgates (OrbitLayout.asteroidCentre, jumpgate, hiddenJumpgate).
// Plain C# (tests: MiningSiteTests).

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.World
{
    public sealed class MiningSite
    {
        /// <summary>From the station: 180 000 + rnd(40 000) game units (9-11 km), y +-20 000.</summary>
        public const float MinUnits = 180000f, SpanUnits = 40000f, HeightUnits = 20000f;
        /// <summary>The site's field centre this far from the orbit's field centre (both fields' cubes apart) and the plant
        /// this far from a jumpgate (game units).</summary>
        public const float FieldClearUnits = 140000f, GateClearUnits = 50000f;
        const int Tries = 32;
        /// <summary>The waypoint this far short of the plant, toward the station; the field centre this far past it.</summary>
        public const float ApproachUnits = 12000f, FieldOffsetUnits = 15000f;
        /// <summary>The field: big rocks in a +-20 000 cube, small ones in +-30 000; none within this of the plant or of the
        /// approach corridor (the line from 40 000 before the waypoint to the plant).</summary>
        public const float BigSide = 40000f, SmallSide = 60000f, ClearUnits = 9000f, PlantClearUnits = 15000f;
        /// <summary>Multiplayer: the field's asteroid indices (NetOrbit, NetState's destroyed lists) start at SiteBase + slot *
        /// SlotSize, clear of the orbit's own field (at most 79 asteroids) and of another squad's site in most cases.</summary>
        public const int SiteBase = 1000, SlotSize = 100, Slots = 80;

        public readonly int seed;
        /// <summary>Game units.</summary>
        public readonly Vector3 plant, waypoint, fieldCentre;
        public readonly int bigCount, smallCount;

        /// <summary>A site without anything to keep clear of (tests).</summary>
        public MiningSite(int seed) : this(seed, null, null) { }

        /// <param name="orbitField">The orbit's own asteroid field centre (game units), null = none.</param>
        /// <param name="gates">The orbit's jumpgates (game units), null = none.</param>
        public MiningSite(int seed, Vector3? orbitField, Vector3[] gates)
        {
            this.seed = seed;
            var rnd = new JavaRandom(seed);
            float best = float.MinValue;
            for (int i = 0; i < Tries; i++)
            {
                float yaw = rnd.NextInt(65536) / 65536f * 2f * Mathf.PI;
                float dist = MinUnits + rnd.NextInt((int)SpanUnits);
                float y = rnd.NextInt((int)(2f * HeightUnits)) - HeightUnits;
                var p = new Vector3(dist * Mathf.Sin(yaw), y, dist * Mathf.Cos(yaw));
                // How far past its limit the nearer obstacle is (>= 0: clear).
                float margin = float.MaxValue;
                if (orbitField.HasValue) margin = Mathf.Min(margin, (p + p.normalized * FieldOffsetUnits - orbitField.Value).magnitude - FieldClearUnits);
                if (gates != null) foreach (var g in gates) margin = Mathf.Min(margin, (p - g).magnitude - GateClearUnits);
                if (margin > best) { best = margin; plant = p; }
                if (margin >= 0f) break;
            }
            var away = plant.normalized;
            waypoint = plant - away * ApproachUnits;
            fieldCentre = plant + away * FieldOffsetUnits;
            bigCount = rnd.NextInt(3) + 5;
            smallCount = rnd.NextInt(8) + 18;
        }

        /// <summary>The site's asteroid indices start here (multiplayer).</summary>
        public int IndexBase => SiteBase + (int)((uint)seed % Slots) * SlotSize;

        /// <summary>A rock at 'p' (game units) would block the plant or the way in to it.</summary>
        public bool Blocks(Vector3 p)
        {
            if ((p - plant).sqrMagnitude < PlantClearUnits * PlantClearUnits) return true;
            var dir = (plant - waypoint).normalized;
            var from = waypoint - dir * 40000f;
            float len = (plant - from).magnitude;
            float t = Mathf.Clamp(Vector3.Dot(p - from, dir), 0f, len);
            return (from + dir * t - p).sqrMagnitude < ClearUnits * ClearUnits;
        }

        /// <summary>The mission's site seed: the orbit's shared seed with the squad's mission id (the same in every game of the
        /// squad), else the mission's own values.</summary>
        public static int Seed(FreelanceMission m, int station)
        {
            unchecked
            {
                int h;
                if (m.netId != 0) h = (int)m.netId ^ (int)(m.netId >> 32);
                else
                {
                    h = 17;
                    h = h * 31 + m.target;
                    h = h * 31 + m.clientStation;
                    h = h * 31 + m.good;
                    h = h * 31 + m.amount;
                    h = h * 31 + m.reward;
                    h = h * 31 + m.difficulty;
                }
                return NetSession.OrbitSeed(station) * 486187739 + h;
            }
        }

        /// <summary>The mission's site in that orbit, clear of its own field and gates.</summary>
        public static MiningSite For(FreelanceMission m, OrbitLayout layout)
        {
            var gates = layout.hasJumpgate ? new[] { layout.jumpgate, layout.hiddenJumpgate } : new[] { layout.hiddenJumpgate };
            return new MiningSite(Seed(m, layout.stationIndex), layout.asteroidCount > 0 ? layout.asteroidCentre : (Vector3?)null, gates);
        }
    }
}
