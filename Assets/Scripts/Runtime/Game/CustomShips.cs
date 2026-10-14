// CustomShips.cs
// The mods' ships (ships.json, Modding.ModContent; the format came from PR #34's custom_ships.json), numbered after the
// original 64 and merged into Database.Ships / Assemblies / WeaponMounts by Database.Load. This is the economy-independent
// view for the code that has no Database at hand: names and descriptions (GameNames; the original's text blocks
// 913 + index / 977 + index would read the wrong ship), the maker's race (Shop.ShipRace stops at 63), the hangar height
// (StationTables.ShipY) and who sells them in the lounges (CustomLoungeSeller). Their models: Modding.ModShips.

using System.Collections.Generic;

namespace GoF2Remake.Data
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class CustomShips
    {
        static List<CustomShipData> all;
        static int revision = -1;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all = null; revision = -1; }

        /// <summary>Every active mod ship (the stats here are the Android economy's, use Database.Ship for prices).</summary>
        public static IReadOnlyList<CustomShipData> All
        {
            get
            {
                if (all == null || revision != Modding.ModManager.Revision)
                {
                    all = Modding.ModContent.ActiveShips().ConvertAll(e => e.ship);
                    revision = Modding.ModManager.Revision;
                }
                return all;
            }
        }

        public static CustomShipData Get(int ship)
        {
            foreach (var c in All) if (c.index == ship) return c;
            return null;
        }

        public static bool IsCustom(int ship) => Get(ship) != null;

        /// <summary>Mod ships can be had whenever their mod is on (also in a multiplayer session that runs it).</summary>
        public static bool Available => true;

        /// <summary>The ship may be offered: not the placeholder of a mod that isn't on.</summary>
        public static bool Offered(int ship) => !Modding.ModContent.IsMissingShip(ship);

        public static string ShipName(int ship) => GameNames.Ship(ship);

        public static string ShipDescription(int ship) => GameNames.ShipDescription(ship);
    }
}
