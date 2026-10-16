// ModBuilderViewBlueprints.cs
// The Mod Builder's Blueprints kind (part of ModBuilderView), edited in the hangar like the game's Blueprints tab: the
// project's blueprints.json (ProjectBlueprint, Modding/README.md "Blueprints"). Left: the blueprints with their product's
// icon, a new ship or item blueprint, Remove. Right: the product (a ship or an item: the game's, the project's own or
// another mod's, ModRefs: the pick makes that mod a dependency), the ingredients with their amounts, a skin's ship
// ("requiresShip"), autocomplete (the game's price, a price of its own, or none), the condition it waits for ("available"),
// known from the start ("unlocked") and where it is found: a Space Lounge seller, a derelict to hack, drops from kills. A
// ship blueprint puts its ship on the turntable (ModBuilder.PreviewShip), so Test fly flies it.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        VisualElement blueprintList, blueprintPage;
        ProjectBlueprint currentBlueprint;

        static readonly string[] SourceRaces = { "any", "terran", "vossk", "nivelian", "midorian", "pirate", "void", "specter" };
        static readonly string[] SourceRaceNames = { "Any", "Terran", "Vossk", "Nivelian", "Midorian", "Pirate", "Void", "Specter" };

        void BuildBlueprintsPanels(VisualElement left, VisualElement right)
        {
            blueprintList = ListPanel(left, T("mbBlueprints", "Blueprints"));
            Act(left, T("mbNewShipBlueprint", "+ New ship blueprint"), () => NewBlueprint(true), "mbv-add");
            Act(left, T("mbNewItemBlueprint", "+ New item blueprint"), () => NewBlueprint(false), "mbv-add");
            Act(left, T("mbRemoveBlueprint", "Remove blueprint"), AskRemoveBlueprint, "mbv-danger", "mbv-remove");
            blueprintPage = PagePanel(right);
        }

        string BlueprintTitle(ProjectBlueprint b)
        {
            string product = RefLabel(b.IsShip ? RefKind.Ship : RefKind.Item, b.Product, null);
            return product ?? b.Id;
        }

        Texture2D BlueprintIcon(ProjectBlueprint b)
        {
            var e = ModRefs.Find(b.IsShip ? RefKind.Ship : RefKind.Item, b.Product, Project);
            return e != null ? RefIcon(b.IsShip ? RefKind.Ship : RefKind.Item, e) : null;
        }

        void RebuildBlueprints()
        {
            blueprintList.Clear();
            if (Project == null) return;
            if (currentBlueprint != null && !Project.Blueprints.Contains(currentBlueprint)) currentBlueprint = null;
            currentBlueprint ??= Project.Blueprints.FirstOrDefault();
            foreach (var bp in Project.Blueprints)
            {
                var b = bp;
                ListRow(blueprintList, BlueprintTitle(b), b.IsShip ? T("mbShipBlueprint", "Ship blueprint") : T("mbItemBlueprint", "Item blueprint"),
                        b == currentBlueprint, () => { currentBlueprint = b; RebuildBlueprints(); BuildBlueprintPage(); }, BlueprintIcon(b));
            }
            if (Project.Blueprints.Count == 0)
                Note(blueprintList, T("mbNoBlueprints", "No blueprints yet. A blueprint builds a ship or an item from goods, ores and other items, like the game's own in the hangar's Blueprints tab."));
            ShowBlueprintShip();
        }

        /// <summary>A ship blueprint's ship on the turntable (else the project's ship).</summary>
        void ShowBlueprintShip()
        {
            int ship = currentBlueprint != null && currentBlueprint.IsShip ? ModBuilder.ShipNumber(currentBlueprint.Product) : -1;
            if (ship == ModBuilder.PreviewShip) return;
            ModBuilder.PreviewShip = ship;
            ModBuilder.PreviewLoadout = null;
            ModBuilder.ApplyShip();
            boundShip = -2;
        }

        void NewBlueprint(bool ship)
        {
            PickRef(ship ? RefKind.Ship : RefKind.Item, ship ? T("mbBlueprintShipTitle", "The ship it builds") : T("mbBlueprintItemTitle", "The item it builds"), e =>
            {
                var b = Project.AddBlueprint(e.name, ship, e.value);
                currentBlueprint = b;
                MarkDirty();
                RebuildBlueprints();
                BuildBlueprintPage();
                Status(T("mbBlueprintAdded", "Now add its ingredients."), false);
            });
        }

        void AskRemoveBlueprint()
        {
            var b = currentBlueprint;
            if (b == null) return;
            Confirm(T("mbRemoveBlueprint", "Remove blueprint"), string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), BlueprintTitle(b)), () =>
            {
                Project.Blueprints.Remove(b);
                currentBlueprint = null;
                MarkDirty();
                RebuildBlueprints();
                BuildBlueprintPage();
            });
        }

        void BlueprintEdited(bool rebuildList = false)
        {
            MarkDirty();
            if (rebuildList) RebuildBlueprints();
            BuildBlueprintPage();
        }

        void BuildBlueprintPage()
        {
            blueprintPage.Clear();
            var b = currentBlueprint;
            if (b == null) { Note(blueprintPage, T("mbPickBlueprint", "Add a blueprint on the left to start.")); return; }
            JsonButton(blueprintPage, b.Json, () => { BlueprintEdited(true); });
            var rk = b.IsShip ? RefKind.Ship : RefKind.Item;
            Heading(blueprintPage, b.IsShip ? T("mbShipBlueprint", "Ship blueprint") : T("mbItemBlueprint", "Item blueprint"));
            RefField(blueprintPage, b.IsShip ? T("mbBuildsShip", "Builds the ship") : T("mbBuildsItem", "Builds the item"), rk, b.Product,
                     v => { if (v != null) { b.SetProduct(b.IsShip, v); BlueprintEdited(true); } }, clear: false);
            Note(blueprintPage, b.IsShip ? T("mbBlueprintShipNote", "The finished ship waits at the station where it was built and replaces the player's ship there like a purchase.")
                                         : T("mbBlueprintItemNote", "A secondary weapon comes 10 at a time, like the game's. An item that has a blueprint already gets this one instead."));

            Heading(blueprintPage, T("mbIngredients", "Ingredients"));
            var ingredients = b.Ingredients;
            for (int i = 0; i < ingredients.Count; i++)
            {
                int n = i;
                var row = Row(blueprintPage);
                var e = ModRefs.Find(RefKind.Item, ingredients[i].item, Project);
                var icon = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-item-icon");
                var tex = e != null ? RefIcon(RefKind.Item, e) : null;
                if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
                Add(row, new Label(RefLabel(RefKind.Item, ingredients[i].item, "?")), "mbv-inline-label");
                Int(row, "", ingredients[i].amount, 1, 100000, v => { var l = b.Ingredients; l[n] = new ProjectStack(l[n].item, v); b.Ingredients = l; MarkDirty(); });
                Act(row, "✕", () => { var l = b.Ingredients; l.RemoveAt(n); b.Ingredients = l; BlueprintEdited(); }, "mbv-step");
            }
            if (ingredients.Count == 0) Note(blueprintPage, T("mbNoIngredients", "None yet: a blueprint needs at least one."));
            Act(blueprintPage, T("mbAddIngredient", "+ Add an ingredient"), () => PickRef(RefKind.Item, T("mbIngredient", "Ingredient"), e =>
            {
                var l = b.Ingredients;
                if (l.Any(x => x.item == e.value)) { Status(T("mbIngredientThere", "It is an ingredient already: change its amount."), false); return; }
                l.Add(new ProjectStack(e.value, e.type == 4 ? 50 : 1));
                b.Ingredients = l;
                BlueprintEdited();
            }), "mbv-add");

            if (b.IsShip)
            {
                RefField(blueprintPage, T("mbRequiresShip", "Only while flying (a skin)"), RefKind.Ship, b.RequiresShip, v => { b.RequiresShip = v; BlueprintEdited(); },
                         T("mbAnyShip", "any ship"));
                Note(blueprintPage, T("mbRequiresShipNote", "Set: it is built only while flying that ship, which it then replaces (its Kaamo Club upgrades stay): a new look for a ship."));
            }

            Heading(blueprintPage, T("mbAutocomplete", "Autocomplete"));
            int mode = !b.Autocomplete ? 2 : b.AutocompletePrice != null ? 1 : 0;
            Segments(blueprintPage, null, new[] { T("mbAutoGame", "Game's price"), T("mbAutoOwn", "Own price"), T("mbAutoNone", "Not possible") }, mode, m =>
            {
                b.SetAutocomplete(m != 2, m == 1 ? b.AutocompletePrice ?? 1000000 : (int?)null);
                BlueprintEdited();
            });
            if (mode == 1) Int(blueprintPage, T("mbPrice", "Price"), b.AutocompletePrice ?? 0, 0, 999999999, v => { b.SetAutocomplete(true, v); MarkDirty(); });

            Heading(blueprintPage, T("mbWhenKnown", "When the player can have it"));
            var flags = Row(blueprintPage);
            FlagButton(flags, T("mbUnlocked", "Known from the start"), b.Unlocked, v => b.Unlocked = v, () => BuildBlueprintPage());
            ConditionField(blueprintPage, T("mbAvailable", "Available when (a condition)"), b.Available, v => { b.Available = v; MarkDirty(); });

            Heading(blueprintPage, T("mbFoundBy", "Where it is found"));
            // A Space Lounge seller.
            var lounge = b.Lounge;
            FlagButton(blueprintPage, T("mbLoungeSeller", "Sold in the Space Lounges"), lounge != null,
                       v => b.Lounge = v ? new JObject { ["chance"] = 3 } : null, () => BuildBlueprintPage());
            if (lounge != null)
            {
                var row = blueprintPage;
                Float(row, T("mbChance", "Chance %"), lounge["chance"] is JValue c ? (float)c : 3f, 0f, 100f, v => { lounge["chance"] = v; b.Lounge = lounge; MarkDirty(); });
                Int(row, T("mbPrice", "Price"), lounge["price"] is JValue p ? (int)p : 0, 0, 999999999, v => { if (v > 0) lounge["price"] = v; else lounge.Remove("price"); b.Lounge = lounge; MarkDirty(); });
                int race = lounge["systemRace"] is JValue r ? (int)r : -1;
                Segments(blueprintPage, T("mbInSystems", "In systems of"), new[] { "Any", "Terran", "Vossk", "Nivelian", "Midorian" }, race + 1, n =>
                {
                    if (n == 0) lounge.Remove("systemRace"); else lounge["systemRace"] = n - 1;
                    b.Lounge = lounge;
                    BlueprintEdited();
                });
                Note(blueprintPage, T("mbLoungeNote", "The chance each time a bar fills with new visitors; no price = half the product's."));
            }
            // A derelict to hack.
            var derelict = b.Derelict;
            FlagButton(blueprintPage, T("mbDerelict", "In derelict freighters (hacked)"), derelict != null,
                       v => b.Derelict = v ? new JObject { ["chance"] = 2, ["race"] = "any" } : null, () => BuildBlueprintPage());
            if (derelict != null)
            {
                Float(blueprintPage, T("mbChancePerOrbit", "Chance per orbit %"), derelict["chance"] is JValue c ? (float)c : 2f, 0f, 100f, v => { derelict["chance"] = v; b.Derelict = derelict; MarkDirty(); });
                string race = (string)derelict["race"] ?? "any";
                Segments(blueprintPage, T("mbFreighterOf", "A freighter of"), SourceRaceNames.Take(5).ToArray(), Array.IndexOf(SourceRaces, race), n =>
                {
                    derelict["race"] = SourceRaces[n];
                    b.Derelict = derelict;
                    BlueprintEdited();
                });
                // Where (blueprints.json "derelict" "stations" / "position"): one orbit here (Edit as JSON takes several).
                string station = derelict["stations"] is JArray sa && sa.Count > 0 ? (string)sa[0] : null;
                RefField(blueprintPage, T("mbDerelictStation", "Only in the orbit of"), RefKind.Station, station, v =>
                {
                    if (string.IsNullOrEmpty(v)) derelict.Remove("stations"); else derelict["stations"] = new JArray(v);
                    b.Derelict = derelict;
                    BlueprintEdited();
                }, T("mbAnyOrbit", "any orbit"));
                string pos = derelict["position"] is JArray pa && pa.Count == 3 ? $"{(float)pa[0]:0} {(float)pa[1]:0} {(float)pa[2]:0}" : "";
                Text(blueprintPage, T("mbDerelictAt", "At x y z (game units, /pos; empty: somewhere ahead of the station)"), pos, 40, v =>
                {
                    var parts = (v ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    if (parts.Length == 3 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, inv, out float x)
                        && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out float y)
                        && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, inv, out float z))
                        derelict["position"] = new JArray(x, y, z);
                    else if (parts.Length == 0) derelict.Remove("position");
                    else return;
                    b.Derelict = derelict;
                    MarkDirty();
                });
                Note(blueprintPage, T("mbDerelictWhereNote", "With an orbit the chance is per visit there (100 = always)."));
            }
            // Drops from kills.
            Caption(blueprintPage, T("mbDrops", "Left by destroyed ships"));
            var drops = b.Drops;
            for (int i = 0; i < drops.Count; i++)
            {
                int n = i;
                var row = Row(blueprintPage);
                int ri = Math.Max(0, Array.IndexOf(SourceRaces, drops[i].race));
                Act(row, SourceRaceNames[ri], () =>
                {
                    var l = b.Drops;
                    l[n] = (SourceRaces[(Array.IndexOf(SourceRaces, l[n].race) + 1 + SourceRaces.Length) % SourceRaces.Length], l[n].chance);
                    b.Drops = l;
                    BlueprintEdited();
                });
                Float(row, T("mbChancePerKill", "% per kill"), drops[i].chance, 0f, 100f, v => { var l = b.Drops; l[n] = (l[n].race, v); b.Drops = l; MarkDirty(); });
                Act(row, "✕", () => { var l = b.Drops; l.RemoveAt(n); b.Drops = l; BlueprintEdited(); }, "mbv-step");
            }
            Act(blueprintPage, T("mbAddDrop", "+ Add a drop"), () => { var l = b.Drops; l.Add(("pirate", 1f)); b.Drops = l; BlueprintEdited(); }, "mbv-add");
            Note(blueprintPage, T("mbDropsNote", "A ship the player destroys may leave it in a data container (tap the race to change it). Every source stops once the player knows the blueprint."));
        }

        /// <summary>A condition field (the event graphs' expressions: Modding/README.md "Conditions"), checked as it is typed.</summary>
        void ConditionField(VisualElement parent, string label, string value, Action<string> set)
        {
            var f = Text(parent, label, value, 300, v => set(string.IsNullOrWhiteSpace(v) ? null : v.Trim()));
            f.textEdition.placeholder = T("mbAlways", "always");
            Note(parent, T("mbConditionNote", "Empty = always. For example: won(main), systemsvisited >= 2, credits > 100000, visited(Kernstal), quest(my_quest) == 2, option(my_mod:hard_mode)."));
        }
    }
}
