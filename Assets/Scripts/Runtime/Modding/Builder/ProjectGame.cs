// ProjectGame.cs
// Remake-only: the Mod Builder's blueprints (blueprints.json), its campaign (campaign.json) and new-game options
// (gameoptions.json) as JObject entries with typed accessors, like ProjectItem (ProjectContent.cs; formats in
// Modding/README.md "Blueprints", "Campaigns", "New-game options"). Plain C#.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    /// <summary>An item and an amount (an ingredient, a campaign's equipment or cargo); item = a number or a key.</summary>
    public struct ProjectStack
    {
        public string item;
        public int amount;
        public ProjectStack(string item, int amount) { this.item = item; this.amount = amount; }

        public static List<ProjectStack> Read(JToken t)
        {
            var l = new List<ProjectStack>();
            if (!(t is JArray a)) return l;
            foreach (var e in a)
            {
                if (e is JObject o) { string it = ProjectEntry.RefText(o["item"]); if (it != null) l.Add(new ProjectStack(it, o["amount"] is JValue v && v.Type == JTokenType.Integer ? (int)v : 1)); }
                else if (ProjectEntry.RefText(e) is string s) l.Add(new ProjectStack(s, 1));
            }
            return l;
        }

        public static JArray Write(IEnumerable<ProjectStack> stacks) =>
            new JArray(stacks.Select(s => (JToken)new JObject { ["item"] = ProjectEntry.RefToken(s.item), ["amount"] = Mathf.Max(1, s.amount) }));
    }

    /// <summary>One blueprints.json entry.</summary>
    public class ProjectBlueprint : ProjectEntry
    {
        public ProjectBlueprint(JObject o) : base(o) { }

        public string Id => S("id");
        public string Ref => "id:" + Id;
        public bool IsShip => json["ship"] != null;
        /// <summary>What it makes: a ship or an item (a number or a key).</summary>
        public string Product => R(IsShip ? "ship" : "item");

        public void SetProduct(bool ship, string reference)
        {
            json.Remove("ship");
            json.Remove("item");
            if (!string.IsNullOrEmpty(reference)) json[ship ? "ship" : "item"] = RefToken(reference);
        }

        public List<ProjectStack> Ingredients { get => ProjectStack.Read(json["ingredients"]); set => json["ingredients"] = ProjectStack.Write(value ?? new List<ProjectStack>()); }
        public string RequiresShip { get => R("requiresShip"); set => SetR("requiresShip", value); }
        /// <summary>autocomplete: true (the game's price), false, or a price (&gt;= 0).</summary>
        public bool Autocomplete { get => !(json["autocomplete"] is JValue v && v.Type == JTokenType.Boolean && !(bool)v); }
        public int? AutocompletePrice => json["autocomplete"] is JValue v && v.Type == JTokenType.Integer ? (int)v : (int?)null;

        public void SetAutocomplete(bool on, int? price)
        {
            if (!on) json["autocomplete"] = false;
            else if (price != null) json["autocomplete"] = Mathf.Max(0, price.Value);
            else json.Remove("autocomplete");
        }

        public string Available { get => S("available"); set => SetS("available", value); }
        public bool Unlocked { get => B("unlocked", false); set => SetB("unlocked", value, false); }

        /// <summary>A source's settings (lounge / derelict), null = that source is off.</summary>
        public JObject Lounge { get => json["lounge"] as JObject; set => Set("lounge", value); }
        public JObject Derelict { get => json["derelict"] as JObject; set => Set("derelict", value); }

        /// <summary>drops: (race word, chance %).</summary>
        public List<(string race, float chance)> Drops
        {
            get => json["drops"] is JArray a
                ? a.OfType<JObject>().Select(o => ((string)o["race"] ?? "any", o["chance"] is JValue v && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer) ? (float)v : 1f)).ToList()
                : new List<(string, float)>();
            set
            {
                if (value == null || value.Count == 0) { json.Remove("drops"); return; }
                json["drops"] = new JArray(value.Select(d => (JToken)new JObject { ["race"] = d.race, ["chance"] = Mathf.Round(d.chance * 100f) / 100f }));
            }
        }
    }

    /// <summary>campaign.json (one per mod).</summary>
    public class ProjectCampaign : ProjectEntry
    {
        public ProjectCampaign(JObject o) : base(o) { }

        public string Name { get => Text("name"); set => SetText("name", value); }
        public string Description { get => Text("description"); set => SetText("description", value); }
        public string Image { get => S("image"); set => SetS("image", value); }
        public string ImageHover { get => S("imageHover"); set => SetS("imageHover", value); }
        public bool ShowTitle { get => B("showTitle", true); set => SetB("showTitle", value, true); }
        public string StartStation { get => R("startStation"); set => SetR("startStation", value); }
        public string StartShip { get => R("startShip"); set => SetR("startShip", value); }
        public int Credits { get => N("credits") ?? 0; set => SetN("credits", value <= 0 ? (int?)null : value); }
        public List<ProjectStack> Equipment { get => ProjectStack.Read(json["equipment"]); set => SetList("equipment", value); }
        public List<ProjectStack> Cargo { get => ProjectStack.Read(json["cargo"]); set => SetList("cargo", value); }
        public string Quest { get => S("quest"); set => SetS("quest", value); }

        void SetList(string k, List<ProjectStack> v) { if (v == null || v.Count == 0) json.Remove(k); else json[k] = ProjectStack.Write(v); }

        /// <summary>[Terran / Vossk, Nivelian / Midorian], default [30, 0].</summary>
        public Vector2Int Standing
        {
            get => json["standing"] is JArray a && a.Count >= 2 ? new Vector2Int((int)a[0], (int)a[1]) : new Vector2Int(30, 0);
            set { if (value == new Vector2Int(30, 0)) json.Remove("standing"); else json["standing"] = new JArray(Mathf.Clamp(value.x, -100, 100), Mathf.Clamp(value.y, -100, 100)); }
        }

        /// <summary>"galaxy" / "items" / "ships": only the mods' (true) or all (false, the default).</summary>
        public bool ModOnly(string k) => S(k) == "mod";
        public void SetModOnly(string k, bool on) => SetS(k, on ? "mod" : null);

        /// <summary>trafficShips: race word -> ships (numbers or keys).</summary>
        public List<string> TrafficShips(string race) =>
            json["trafficShips"] is JObject o && o[race] is JArray a ? a.Select(RefText).Where(x => x != null).ToList() : new List<string>();

        public void SetTrafficShips(string race, List<string> ships)
        {
            if (!(json["trafficShips"] is JObject o)) json["trafficShips"] = o = new JObject();
            if (ships == null || ships.Count == 0) o.Remove(race); else o[race] = new JArray(ships.Select(RefToken));
            if (!o.HasValues) json.Remove("trafficShips");
        }
    }

    /// <summary>One gameoptions.json entry.</summary>
    public class ProjectGameOption : ProjectEntry
    {
        public ProjectGameOption(JObject o) : base(o) { }

        public string Id => S("id");
        public string Ref => "id:" + Id;
        public string Name { get => Text("name"); set => SetText("name", value); }
        public string Description { get => Text("description"); set => SetText("description", value); }
        public string Image { get => S("image"); set => SetS("image", value); }
        public string ImageHover { get => S("imageHover"); set => SetS("imageHover", value); }
        public bool ShowTitle { get => B("showTitle", true); set => SetB("showTitle", value, true); }
        public bool Default { get => B("default", false); set => SetB("default", value, false); }
    }
}
