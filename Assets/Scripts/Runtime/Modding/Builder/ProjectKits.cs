// ProjectKits.cs
// Remake-only: the Mod Builder's ship kits (shipkits.json, ModShipKits): customizable ships assembled from parts the player
// picks in the hangar's Customize screen. A kit (ProjectKit) is its slots in assembly order, one scale and turn for every
// part (they share one origin) and its parts (ProjectKitPart: a .glb per part, the slot it fills, the ship types that may
// fit it, its tier extension models, its stats and slot counts, its hardpoints in ship-relative game units, the anchors it
// offers or sits on). The colours, presets, skins, hardpoint models and materials are kept as the kit's JSON (edited there,
// written back unchanged). A ships.json ship flies a kit with "kit", "kitType" and its default "build" (ProjectShip).
// Plain C#.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    /// <summary>One shipkits.json entry of a project.</summary>
    public class ProjectKit : ProjectEntry
    {
        public const string File = "shipkits.json";

        /// <summary>The parts (their JSON is the entries of the kit's "parts" list, the same objects).</summary>
        public readonly List<ProjectKitPart> Parts = new List<ProjectKitPart>();

        public ProjectKit(JObject o) : base(o)
        {
            if (!(json["parts"] is JArray a)) json["parts"] = a = new JArray();
            foreach (var p in a.OfType<JObject>()) Parts.Add(new ProjectKitPart(p));
        }

        public string Id { get => S("id"); set => SetS("id", value); }
        public string Ref => "kit:" + Id;
        public string Name { get => Text("name"); set => SetText("name", value); }
        /// <summary>Unity metres per model unit, for every part.</summary>
        public float Scale { get => F("scale") ?? 1f; set => SetF("scale", value); }
        /// <summary>Degrees about Unity y when the parts' nose doesn't face +Z.</summary>
        public float Yaw { get => F("yaw") ?? 0f; set { if (Mathf.Approximately(value, 0f)) json.Remove("yaw"); else SetF("yaw", value); } }

        /// <summary>The build's slots in assembly order ("slots"; none = the parts' own, in their order).</summary>
        public List<string> Slots
        {
            get
            {
                if (json["slots"] is JArray a && a.Count > 0) return a.Select(t => t.ToString()).ToList();
                return Parts.Select(p => p.Slot).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            set => json["slots"] = new JArray(value ?? new List<string>());
        }

        public List<ProjectKitPart> PartsIn(string slot) =>
            Parts.Where(p => string.Equals(p.Slot, slot, StringComparison.OrdinalIgnoreCase)).ToList();

        public ProjectKitPart Find(string id) => Parts.Find(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>The tier models the parts have at most (the build's tier runs 0..this).</summary>
        public int MaxTier => Parts.Count == 0 ? 0 : Parts.Max(p => p.Tiers.Count);

        /// <summary>A part id unique in this kit.</summary>
        public string PartIdFor(string name)
        {
            return ModProject.UniqueId(name, "part", id => Find(id) != null);
        }

        public ProjectKitPart AddPart(string id, string slot, string name, string model)
        {
            var o = new JObject { ["id"] = id, ["slot"] = slot, ["name"] = name, ["model"] = model };
            ((JArray)json["parts"]).Add(o);
            var p = new ProjectKitPart(o);
            Parts.Add(p);
            var slots = Slots;
            if (!slots.Contains(slot, StringComparer.OrdinalIgnoreCase)) { slots.Add(slot); Slots = slots; }
            return p;
        }

        public void RemovePart(ProjectKitPart p)
        {
            Parts.Remove(p);
            (json["parts"] as JArray)?.Remove(p.Json);
        }

        /// <summary>A slot renamed: the list and its parts.</summary>
        public void RenameSlot(string from, string to)
        {
            Slots = Slots.Select(s => string.Equals(s, from, StringComparison.OrdinalIgnoreCase) ? to : s).ToList();
            foreach (var p in PartsIn(from)) p.Slot = to;
        }

        /// <summary>A slot removed with its parts.</summary>
        public void RemoveSlot(string slot)
        {
            foreach (var p in PartsIn(slot)) RemovePart(p);
            Slots = Slots.Where(s => !string.Equals(s, slot, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>The kit's colour slots ("colorSlots") by id and name, for the builder's list.</summary>
        public List<(string id, string name, string material)> ColorSlots =>
            (json["colorSlots"] as JArray)?.OfType<JObject>().Select(o => ((string)o["id"], (string)o["name"],
                (string)((o["targets"] as JArray)?.OfType<JObject>().FirstOrDefault()?["material"]))).ToList()
            ?? new List<(string, string, string)>();

        /// <summary>The palette every builder-made colour slot uses (made with a few colours when missing).</summary>
        public const string BuilderPalette = "paint";

        /// <summary>A paint slot: the materials whose name contains 'material' take the colour picked (base colour, or the
        /// emission for lights); the kit's palette "paint" made the first time.</summary>
        public void AddColorSlot(string id, string name, string material, bool emission)
        {
            if (!(json["palettes"] is JObject palettes)) json["palettes"] = palettes = new JObject();
            if (palettes[BuilderPalette] == null)
                palettes[BuilderPalette] = new JArray(
                    new JObject { ["id"] = "white", ["name"] = "White", ["color"] = "#F2F2F2" },
                    new JObject { ["id"] = "grey", ["name"] = "Grey", ["color"] = "#7A7E85" },
                    new JObject { ["id"] = "black", ["name"] = "Black", ["color"] = "#1C1D20" },
                    new JObject { ["id"] = "red", ["name"] = "Red", ["color"] = "#B3261E" },
                    new JObject { ["id"] = "orange", ["name"] = "Orange", ["color"] = "#E07B1A" },
                    new JObject { ["id"] = "yellow", ["name"] = "Yellow", ["color"] = "#E8C21C" },
                    new JObject { ["id"] = "green", ["name"] = "Green", ["color"] = "#3E8E41" },
                    new JObject { ["id"] = "blue", ["name"] = "Blue", ["color"] = "#2F6FB5" },
                    new JObject { ["id"] = "cyan", ["name"] = "Cyan", ["color"] = "#2FB8C9" },
                    new JObject { ["id"] = "purple", ["name"] = "Purple", ["color"] = "#7B4BB7" });
            if (!(json["colorSlots"] is JArray slots)) json["colorSlots"] = slots = new JArray();
            slots.Add(new JObject
            {
                ["id"] = id, ["name"] = name, ["palette"] = BuilderPalette, ["default"] = emission ? "cyan" : "white",
                ["targets"] = new JArray(new JObject { ["material"] = material, ["property"] = emission ? "emission" : "base", ["intensity"] = emission ? 2 : 1 }),
            });
        }

        public void RemoveColorSlot(string id)
        {
            if (!(json["colorSlots"] is JArray slots)) return;
            foreach (var o in slots.OfType<JObject>().Where(o => (string)o["id"] == id).ToList()) o.Remove();
            if (slots.Count == 0) json.Remove("colorSlots");
        }

        /// <summary>The part's mounts written into its JSON (ToJson and Json read the same objects).</summary>
        public void Flush() { foreach (var p in Parts) p.Flush(); }

        public new JObject ToJson() { Flush(); return base.ToJson(); }
    }

    /// <summary>One part of a project's kit.</summary>
    public class ProjectKitPart : ProjectEntry
    {
        /// <summary>The part's hardpoints ("mounts", ship-relative game units like a ship's).</summary>
        public readonly List<WeaponMount> Mounts = new List<WeaponMount>();

        public ProjectKitPart(JObject o) : base(o) => ReadMounts();

        /// <summary>The hardpoints read again from the JSON (after Edit as JSON).</summary>
        public void ReadMounts()
        {
            Mounts.Clear();
            if (json["mounts"] is JArray a)
                foreach (var m in a.OfType<JObject>())
                    try { Mounts.Add(JsonUtility.FromJson<WeaponMount>(m.ToString())); } catch { }
        }

        public void Flush()
        {
            if (Mounts.Count == 0) json.Remove("mounts");
            else json["mounts"] = new JArray(Mounts.Select(ProjectShip.MountJson));
        }

        public string Id { get => S("id"); set => SetS("id", value); }
        public string Slot { get => S("slot"); set => SetS("slot", value); }
        public string Name { get => Text("name"); set => SetText("name", value); }
        public string Model { get => S("model"); set => SetS("model", value); }
        public string Anchor { get => S("anchor"); set => SetS("anchor", value); }

        /// <summary>The ship types that may fit it ("types"; none = every type).</summary>
        public List<string> Types
        {
            get => (json["types"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>();
            set { if (value == null || value.Count == 0) json.Remove("types"); else json["types"] = new JArray(value); }
        }

        /// <summary>Extension models shown from build tier 1, 2, ... ("tiers").</summary>
        public List<string> Tiers
        {
            get => (json["tiers"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>();
            set { if (value == null || value.Count == 0) json.Remove("tiers"); else json["tiers"] = new JArray(value); }
        }

        /// <summary>A stat in "stats" (armor, cargo, handling, topSpeed, turnRate, acceleration, strafeSpeed in %; equipment in
        /// slots); 0 = none.</summary>
        public int Stat(string key) => json["stats"] is JObject o && o[key] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? Mathf.RoundToInt((float)v) : 0;

        public void SetStat(string key, int value)
        {
            if (!(json["stats"] is JObject o)) json["stats"] = o = new JObject();
            if (value == 0) o.Remove(key); else o[key] = value;
            if (o.Count == 0) json.Remove("stats");
        }

        /// <summary>A slot count the part gives ("slots": primary / secondary / turret), -1 = none (the ship's own count when
        /// no part of the build gives that type).</summary>
        public int SlotCount(string key) => json["slots"] is JObject o && o[key] is JValue v && v.Type == JTokenType.Integer ? (int)v : -1;

        public void SetSlotCount(string key, int value)
        {
            if (!(json["slots"] is JObject o)) json["slots"] = o = new JObject();
            if (value < 0) o.Remove(key); else o[key] = Mathf.Min(value, 99);
            if (o.Count == 0) json.Remove("slots");
        }

        /// <summary>The points this part offers others (model units).</summary>
        public Dictionary<string, Vector3> Anchors
        {
            get
            {
                var d = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
                if (json["anchors"] is JObject o)
                    foreach (var p in o.Properties())
                        if (p.Value is JArray a && a.Count >= 3) d[p.Name] = new Vector3((float)a[0], (float)a[1], (float)a[2]);
                return d;
            }
        }

        public void SetAnchor(string name, Vector3? at)
        {
            if (!(json["anchors"] is JObject o)) json["anchors"] = o = new JObject();
            if (at == null) o.Remove(name);
            else o[name] = new JArray(Mathf.Round(at.Value.x * 1000f) / 1000f, Mathf.Round(at.Value.y * 1000f) / 1000f, Mathf.Round(at.Value.z * 1000f) / 1000f);
            if (o.Count == 0) json.Remove("anchors");
        }

        public int MountCount(int slotType) => Mounts.Count(m => m.slotType == slotType);
    }
}
