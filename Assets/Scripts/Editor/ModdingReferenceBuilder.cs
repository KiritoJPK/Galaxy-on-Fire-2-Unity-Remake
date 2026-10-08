// ModdingReferenceBuilder.cs
// GoF2 > Build > Modding AI Reference: Modding/ai/gof2-modding/reference.md, the tables of every original item, ship, system
// and station (numbers, names, stats; the Android economy) that the AI modding guide (Modding/ai/gof2-modding/SKILL.md) points
// assistants to. Run it again after changing the game data. Mods' content is left out (only the original numbers).

using UnityEditor;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    public static class ModdingReferenceBuilder
    {
        const string OutputPath = "Modding/ai/gof2-modding/reference.md";

        [MenuItem("GoF2/Build/Modding AI Reference", priority = 210)]
        public static void Build()
        {
            var savedEconomy = GoF2Remake.Data.Session.Economy;
            GoF2Remake.Data.Session.Economy = GoF2Remake.Data.Economy.Android;
            var db = GoF2Remake.Data.Database.Load();
            GoF2Remake.Data.Session.Economy = savedEconomy;
            var sb = new System.Text.StringBuilder();
            string Clean(string s) => (s ?? "").Replace("�", "?").Replace("|", "/").Replace("\n", " ");
            string[] raceNames = { "Terran", "Vossk", "Nivelian", "Midorian", "?", "?", "?", "?", "Pirate", "Void", "Specter" };
            string Race(int r) => r >= 0 && r < raceNames.Length ? raceNames[r] : "-";
            sb.AppendLine("# Galaxy on Fire 2 Remake: game data reference for modders");
            sb.AppendLine();
            sb.AppendLine("Generated from the game's own tables (the Android economy; the Default economy's prices differ). Use these numbers");
            sb.AppendLine("in mod files: `base`, `override`, ingredients, `alwaysSoldAt`, `system`, `looksLike`, `startStation`, `startShip`...");
            sb.AppendLine("Mod content is referred to by key instead (`mod_id:id`).");
            sb.AppendLine();
            sb.AppendLine("## Races");
            sb.AppendLine();
            sb.AppendLine("0 Terran, 1 Vossk, 2 Nivelian, 3 Midorian, 8 Pirate, 9 Void, 10 Specter. Systems and stations belong to 0-3.");
            sb.AppendLine();
            sb.AppendLine("## Items");
            sb.AppendLine();
            sb.AppendLine("Type: primary / secondary / turret / equipment / commodity. Tech = tech level (1-10). Price = min-max (credits). Stats = the item's own stats (copy them when basing an item on it; names as in items.json's statList, which mods accept). BP = has a blueprint recipe in the original.");
            sb.AppendLine();
            sb.AppendLine("| # | Name | Type | Category | Tech | Price | Stats | BP |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var it in db.Items)
            {
                if (it.index >= 233) break;
                var stats = new System.Collections.Generic.List<string>();
                foreach (var s in it.statList) if (!s.key.EndsWith("_guess")) stats.Add(s.key + " " + s.value);
                sb.AppendLine($"| {it.index} | {Clean(GoF2Remake.Data.GameNames.Item(it.index))} | {it.type} | {Clean(GoF2Remake.UI.ItemInfo.Category(it))} | {it.techLevel} | {it.minPrice}-{it.maxPrice} | {Clean(string.Join(", ", stats))} | {(it.blueprint != null && it.blueprint.Count > 0 ? "yes" : "")} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Ships");
            sb.AppendLine();
            sb.AppendLine("Armor = hull points. Handling: 100 = average. Slots = primary / secondary / turret / equipment. Ships 13, 14 and 15 are the freighters and the Terran battleship (not sold).");
            sb.AppendLine();
            sb.AppendLine("| # | Name | Race | Armor | Cargo t | Handling | Price | Slots |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var s in db.Ships)
            {
                if (s.index >= 64) break;
                int race = s.index < GoF2Remake.Data.Shop.ShipRace.Length ? GoF2Remake.Data.Shop.ShipRace[s.index] : -1;
                var sl = s.slots ?? new GoF2Remake.Data.ShipSlots();
                string shipName = s.index == 13 ? "Vossk freighter (not sold)" : s.index == 14 ? "Terran battleship (not sold)" : s.index == 15 ? "freighter (not sold)" : Clean(GoF2Remake.Data.GameNames.Ship(s.index));
                sb.AppendLine($"| {s.index} | {shipName} | {Race(race)} | {s.armor} | {s.cargo} | {s.handling} | {s.price} | {sl.primary}/{sl.secondary}/{sl.turret}/{sl.equipment} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Systems");
            sb.AppendLine();
            sb.AppendLine("Security 0 lawless .. 3 secure. Visible = on the star map from a new game's start. Gates = the systems its jumpgate reaches.");
            sb.AppendLine();
            sb.AppendLine("| # | Name | Race | Security | Visible | Map position | Gates | Stations |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var y in db.Systems)
            {
                if (y.index >= 34) break;
                var st = db.Stations.FindAll(x => x.system == y.index).ConvertAll(x => x.index.ToString());
                string pos = y.mapPosition != null ? $"{y.mapPosition.x}, {y.mapPosition.y}, {y.mapPosition.z}" : "";
                string gates = y.jumpRoutesTo != null ? string.Join(", ", y.jumpRoutesTo) : "";
                sb.AppendLine($"| {y.index} | {Clean(y.name)} | {Race(y.raceId)} | {y.securityLevel} | {(y.initiallyVisible ? "yes" : "")} | {pos} | {gates} | {string.Join(", ", st)} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Stations");
            sb.AppendLine();
            sb.AppendLine("78 Var Hastra (Mido) is where a new game starts. 108 is the Kaamo Club. Tech = which items its shop can sell.");
            sb.AppendLine();
            sb.AppendLine("| # | Name | System | Tech |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var t in db.Stations)
            {
                if (t.index >= 135) break;
                sb.AppendLine($"| {t.index} | {Clean(t.name)} | {t.system} {Clean(t.systemName)} | {t.techLevel} |");
            }
            var path = System.IO.Path.GetFullPath(OutputPath);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"));
            Debug.Log($"GoF2: {path} written");
        }
    }
}
