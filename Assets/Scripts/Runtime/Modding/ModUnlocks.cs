// ModUnlocks.cs
// Remake mods: when a mod's content becomes part of the game. items.json / ships.json entries (new ones and overrides) may
// carry "available": a condition in the event graphs' expression language (Events.EventRunner.Condition: campaign, rank,
// credits, visited(station), systemsvisited, won(main | valkyrie | supernova), option(mod_id:option_id), quest(name)...)
// that must hold before the item is stocked by shops or found in loot, or the ship is sold by dealers and lounge sellers.
// ships.json "dealer" { "chance", "systemRace", "minTechLevel" } puts a mod ship in the ordinary dealers' lists
// (AddDealerShips, from Shop.GenerateShips). A condition that can't be read counts as false and warns once on its mod.

using System;
using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModUnlocks
    {
        readonly struct Cond
        {
            public readonly ModInfo mod;
            public readonly string text, where;
            public Cond(ModInfo mod, string text, string where) { this.mod = mod; this.text = text; this.where = where; }
        }

        static readonly Dictionary<int, Cond> items = new Dictionary<int, Cond>(), ships = new Dictionary<int, Cond>();
        static int builtRevision = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { items.Clear(); ships.Clear(); builtRevision = -1; }

        static void Build()
        {
            if (builtRevision == ModManager.Revision) return;
            builtRevision = ModManager.Revision;
            items.Clear();
            ships.Clear();
            foreach (var mod in ModManager.Active)
            {
                var p = ModContent.Parse(mod);
                foreach (var d in p.items)
                {
                    string c = ModJson.Str(d.json, "available");
                    if (string.IsNullOrWhiteSpace(c)) continue;
                    int i = d.key != null ? ModContent.ItemIndexOf(d.key) : ModContent.TryResolveItem(d.overrideRef, out int t) ? t : -1;
                    if (i >= 0) items[i] = new Cond(mod, c, d.where);   // a later mod's condition wins
                }
                foreach (var d in p.ships)
                {
                    string c = ModJson.Str(d.json, "available");
                    if (string.IsNullOrWhiteSpace(c)) continue;
                    int i = d.key != null ? ModContent.ShipIndexOf(d.key) : ModContent.TryResolveShip(d.overrideRef, out int t) ? t : -1;
                    if (i >= 0) ships[i] = new Cond(mod, c, d.where);
                }
            }
        }

        /// <summary>A mod's condition for this game; empty = true. A broken one is false and warns once on its mod.</summary>
        public static bool Holds(ModInfo mod, string condition, string where)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            if (Events.EventRunner.Condition(condition, out string error)) return true;
            if (error != null && mod != null)
            {
                string message = $"{where}: \"available\" / condition \"{condition}\": {error}";
                if (!mod.Warnings.Contains(message)) { mod.Warnings.Add(message); Debug.LogWarning($"Mods: {mod.Id}: {message}"); }
            }
            return false;
        }

        /// <summary>The item may be stocked / looted now (no condition: always).</summary>
        public static bool ItemAvailable(int item)
        {
            Build();
            return !items.TryGetValue(item, out var c) || Holds(c.mod, c.text, c.where);
        }

        /// <summary>The ship may be sold now (no condition: always).</summary>
        public static bool ShipAvailable(int ship)
        {
            Build();
            return !ships.TryGetValue(ship, out var c) || Holds(c.mod, c.text, c.where);
        }

        /// <summary>Shop.GenerateShips' ordinary dealers (not the special yards): each mod ship with a "dealer" entry, at its
        /// chance, in its race's systems from its tech level, when available.</summary>
        public static void AddDealerShips(Database db, int station, int systemRace, List<int> ships)
        {
            var st = db.Stations.Find(s => s.index == station);
            foreach (var c in CustomShips.All)
            {
                var d = c.dealer;
                if (d == null || ships.Contains(c.index) || db.Ship(c.index) == null) continue;
                if (d.systemRace >= 0 && d.systemRace != systemRace) continue;
                if (st != null && st.techLevel < d.minTechLevel) continue;
                if (UnityEngine.Random.value * 100f >= d.chance) continue;
                if (!ShipAvailable(c.index)) continue;
                ships.Add(c.index);
            }
        }
    }
}
