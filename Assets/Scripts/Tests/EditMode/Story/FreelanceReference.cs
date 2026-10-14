// FreelanceReference.cs
// An independent port of the freelance mission formulas of Reference/research/freelance_missions.md §2.4 / §2.5 and
// Reference/tools/missions/mission_tables.py (Generator::createMission 0xa2a7c), for the generator tests. Single precision
// like the original's float code; FreelanceMissionTests checks it against the Python tool's printed results first.

using System;
using GoF2Remake.Data;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class FreelanceReference
    {
        /// <summary>round50: down to 50, except a remainder of exactly 25 rounds up (freelance_missions.md 2.5).</summary>
        public static int Round50(float x)
        {
            int v = (int)x;
            int m = v % 50;
            return m == 0 || m == 25 ? v + m : v - m;
        }

        /// <summary>Galaxy::distance: sqrt(dx² + dy² + (z/10)²) on systems.json mapPosition, z / 10 as an int.</summary>
        public static float Distance(Database db, int systemA, int systemB)
        {
            if (systemA == systemB) return 0f;
            var a = db.Systems.Find(s => s.index == systemA).mapPosition;
            var b = db.Systems.Find(s => s.index == systemB).mapPosition;
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z / 10 - b.z / 10;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static int SystemOf(Database db, int station) => db.Stations.Find(s => s.index == station).system;

        /// <summary>The reward before rounding: base = (int(d/10·5500)+1500)·(dist/1200+1), the type factor, + 10·level³.</summary>
        public static float Reward(int type, int difficulty, float dist, int amount, int maxPrice, int level)
        {
            float r = (int)(difficulty / 10f * 5500f) + 1500;
            r *= dist / 1200f + 1f;
            switch (type)
            {
                case 9: r *= 1.2f; break;
                case 7: r *= 0.7f; break;
                case 3: case 5: r *= 2f; break;
                case 11: r = r * 0.6f + amount * (r * 0.6f / 5f); break;
                case 8: r = amount * maxPrice * 1.7f; break;
            }
            return r + 10 * level * level * level;
        }

        /// <summary>Standing::getMissionBonus: max(0, s/100), s = s0 Terran, -s0 Vossk, s1 Nivelian, -s1 Midorian, else 0.</summary>
        public static float BonusFactor(int race, int s0, int s1)
        {
            int s = race switch { 0 => s0, 1 => -s0, 2 => s1, 3 => -s1, _ => 0 };
            return race >= 0 && race <= 3 ? Math.Max(0f, s / 100f) : 0f;
        }

        /// <summary>§2.4: the amount by type and difficulty (Protection is random 2..5, Purchase 5..19; others 0).</summary>
        public static int Amount(int type, int difficulty)
        {
            float d = difficulty / 10f;
            switch (type)
            {
                case 0: return (int)(d * 95f) + 5;
                case 3: case 5: return (int)(d * 8f) + 2;
                case 11: return (int)(d * 18f) + 2;
                default: return 0;
            }
        }

        /// <summary>DAT_00251e70: stations never used as mission targets (freelance_missions.md 2.2).</summary>
        public static readonly int[] ExcludedTargets =
        {
            1, 10, 11, 15, 22, 27, 29, 30, 33, 38, 40, 47, 48, 55, 56, 58, 60, 65, 66, 70, 74, 76, 79, 80, 81, 82, 83, 85, 86,
            90, 91, 92, 93, 94, 95, 98, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 113, 121, 126, 131,
        };
    }
}
