// PirateBases.cs
// The four pirate bases (Reference/research/npc_combat_specials.md 3): stations 1 Mahasa, 33 Neh Suhnu, 47 Bosmah and
// 86 Hamina (all Nivelian systems), active from the new game until their outpost is destroyed (Status+0x4c, saved).
//   Station::stationHasPirateBase 0xb3e64 / SolarSystem::hasPirateBase 0x180aa4
//   Level::createMission 0xbda70  in a base system: no raiders, 2 pirates near the player (hardcore 4..6); in the base
//                                 station's orbit: a sleeping Pirate Outpost at the table position +-10000, 5 (10)
//                                 sleeping guards within -10000..+29999 of it, the red static fog
//   Level::pirateStationAction 0xd6338  a guard wakes: radio 435-437 (once); the outpost dies: destroyed, reward pending,
//                                 radio 438-440 (once)
//   ModStation::OnInitialize / checkHints  docking at a base station: 434 from Security and a relaunch; the next docking
//                                 anywhere after the kill: 442 from "Nivelian" and +20000 credits
// Plain C#.

using UnityEngine;

namespace GoF2Remake.Data
{
    public static class PirateBases
    {
        /// <summary>DAT_00251f90: the base stations.</summary>
        public static readonly int[] Stations = { 1, 33, 47, 86 };
        /// <summary>DAT_00253724: the outposts' positions (game units, before the +-10000 jitter).</summary>
        public static readonly Vector3[] OutpostPositions =
        {
            new Vector3(-200000, -100000, 50000), new Vector3(50000, 200000, -100000),
            new Vector3(140000, -150000, 7000), new Vector3(170000, -170000, 10000),
        };
        /// <summary>DAT_002543e0: the outpost's crate (item, amount) per base.</summary>
        public static readonly (int item, int amount)[] Loot = { (45, 5), (28, 1), (4, 1), (17, 1) };
        public const int Reward = 20000;
        public const int SecurityName = 1609, NivelianName = 1662;
        public static readonly int[] Portrait = { 2, 0, 0, 0, 0 };   // DAT_0026a1c4

        public static int IndexOf(int station) => System.Array.IndexOf(Stations, station);

        /// <summary>Station::stationHasPirateBase.</summary>
        public static bool StationHasBase(int station)
        {
            int i = IndexOf(station);
            return i >= 0 && !Session.PirateBaseDestroyed[i];
        }

        /// <summary>SolarSystem::hasPirateBase.</summary>
        public static bool SystemHasBase(Database db, int system)
        {
            for (int i = 0; i < Stations.Length; i++)
                if (!Session.PirateBaseDestroyed[i] && (db.Stations.Find(s => s.index == Stations[i])?.system ?? -1) == system) return true;
            return false;
        }

        /// <summary>pirateStationAction(false): the outpost of 'station' destroyed.</summary>
        public static void Destroyed(int station)
        {
            int i = IndexOf(station);
            if (i < 0 || Session.PirateBaseDestroyed[i]) return;
            Session.PirateBaseDestroyed[i] = true;
            Session.PirateBaseRewardPending = true;
        }
    }
}
