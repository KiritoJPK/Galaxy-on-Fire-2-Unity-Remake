// FighterPool.cs
// The fighters each race flies (Globals::getRandomEnemyFighter 0xf9034; races 4-8 and >= 11 fly the pirates'), here in
// GoF2.Game for the shop's dealer ships and the news ticker; NpcTables (the NPC combat tables, GoF2.Space) forwards to it.

using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class FighterPool
    {
        static readonly Dictionary<int, int[]> Fighters = new Dictionary<int, int[]>
        {
            [0] = new[] { 1, 5, 7, 17, 22, 26, 27, 28, 33, 34, 36 },
            [1] = new[] { 9 },
            [2] = new[] { 4, 12, 16, 18, 21, 31, 35 },
            [3] = new[] { 3, 6, 19, 20, 30 },
            [8] = new[] { 2, 11, 23, 24, 25, 29, 32 },
            [9] = new[] { 8 },
            [10] = new[] { 44 },
        };

        /// <summary>Globals::getRandomEnemyFighter.</summary>
        public static int Random(int race) => Modding.ModCampaigns.TrafficShip(race, RandomOriginal(race));   // remake mods: a campaign's own ships

        static int RandomOriginal(int race)
        {
            // Valkyrie won: the Vossk also fly the K'Suukk (41) and the S'Kanarr (39).
            if (race == 1 && Story.Dlc1Won) { int r = UnityEngine.Random.Range(0, 100); return r < 60 ? 9 : r < 85 ? 41 : 39; }
            if (!Fighters.TryGetValue(race, out var pool)) pool = Fighters[8];   // the minor races: pirates
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        /// <summary>Remake (World.CapitalShips' escorts): one of the dearer half of the race's fighters (by ships.json price), as
        /// Globals::getRandomEnemyFighter would pick them otherwise.</summary>
        public static int Strong(Database db, int race)
        {
            int[] pool;
            if (race == 1 && Story.Dlc1Won) pool = new[] { 9, 41, 39 };
            else if (!Fighters.TryGetValue(race, out pool)) pool = Fighters[8];
            var sorted = new List<int>(pool);
            sorted.Sort((a, b) => (db.Ship(b)?.price ?? 0).CompareTo(db.Ship(a)?.price ?? 0));
            int pick = sorted[UnityEngine.Random.Range(0, Mathf.Max(1, (sorted.Count + 1) / 2))];
            return Modding.ModCampaigns.TrafficShip(race, pick);
        }
    }
}
