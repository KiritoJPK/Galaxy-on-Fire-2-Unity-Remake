// ModShipKits.cs
// Remake mods: customizable ships, assembled from parts the player picks in the hangar (EVERSPACE 2's ship modules).
// A mod's shipkits.json lists kits, each a set of parts by slot:
//   [ { "id": "medium", "name": "Medium hulls",
//       "slots": ["base", "cockpit", "body", "rear", "wings"]   (the build's slots, in assembly order; default: the parts')
//       "scale": 1.0,            Unity metres per model unit (the parts share one origin, so one scale for every build)
//       "yaw": 0,                degrees about Unity y when the parts' nose doesn't face +Z
//       "materials": [ ... ],    like a ship's: applied to every part's renderers it names (ModMaterials.FromSpec)
//       "parts": [
//         { "id": "nemesis", "slot": "wings", "name": "Nemesis" | { "en": ..., "de": ... }, "model": "parts/wings_009.glb",
//           "types": ["sentinel"],   which ship types may fit it (the ships' "kitType"; none = every type)
//           "tiers": ["parts/wings_009_t1.glb", ...],   extension meshes shown at build tier 1, 2, ... (none = no extension)
//           "mounts": [ { "slotType": 0, "position_engine": [x, y, z] }, ... ],   the part's weapon / turret / exhaust mounts,
//           "tierMounts": [ [ ... ], ... ],   and its tier extensions' (like a ship's "mounts", ship-relative game units)
//           "anchors": { "cockpit": [x, y, z] },   points this part offers others (model units, the GLB's glTF coordinates)
//           "anchor": "cockpit",   this part (and its tier models) sits on that point of the build's part offering it
//                                  (EVERSPACE 2's bodies carry the canopy at a socket of their own)
//           "stats": { "armor": 10, "cargo": -5, "handling": 8 (%), "equipment": 1 (slots), "topSpeed", "turnRate",
//                      "acceleration", "strafeSpeed" (% flight bonuses, both flight styles) },
//           "slots": { "primary": 2, "secondary": 1, "turret": 1 } }
//     A build adds up its parts' stats (the hull, cargo and handling in % of the ship's own) and, per slot type, its parts'
//     slots (the ship's own count when none of its parts gives that type): BuildEffect; the hull (Shop.BaseHp, PlayerHealth,
//     StationLevel), cargo (Shop.MaxLoad), slots (Hangar.SlotCount / FitToSlots) and flight (Database.BuildFlightStats) of the
//     player's ship follow its build. The tier is cosmetic (its extensions' looks and mounts).
//     A build's mounts of each slot type are its parts' (and its tier extensions'), or the ship's own "mounts" of that type
//     when no part has any (MountsFor; Database.MountsOf: the player's build for the player's ship, else the default build).
//       ] } ]
//       "palettes": { "main": [ { "id": "white", "name": "Snow White", "color": "#F9F9F9", "metallic": 0, "smoothness": 0.5 }, ... ] },
//       "colorSlots": [ { "id": "hull1", "name": "Main colour", "palette": "main", "default": "white",
//                         "targets": [ { "material": "Paint1", "property": "base" | "emission" | "boost" | "engine", "intensity": 1 } ] } ],
//       "presets": [ { "id": "scheme_4", "name": "Scheme 4", "colors": { "hull1": "#699CBF/1/0.6", "lights": "white", ... } } ]
//     The player picks a colour per slot from its palette (or a preset sets them all); each target recolours the materials whose
//     name contains 'material' (the model's own, or a kit "materials" entry's 'material' filter): base = the base colour (x
//     intensity; a value's metallic / smoothness too), emission = the emission colour x intensity, boost = an engine flame's
//     boost colour x intensity (its boost cards; the manoeuvring jets' material is "lateral thruster"), engine = the engine glow.
//     A colour value: a palette entry's id, or "#RRGGBB" with an optional "/metallic/smoothness".
//     A strength slot { "id", "name", "steps": [0, 0.25, 0.5, 0.75, 1], "default": "0.5", "scales": "<colour slot id>" } has no
//     palette: its value (a number) scales that colour slot's emission targets (EVERSPACE 2's Emissive 3 intensity).
//       "hardpoints": { "turret": "...glb", "weapons": [ { "slot", "model", "items", "categories" } ] }: the weapons' and the
//     turret's models on the build's mounts (ModHardpoints).
//       "skins": [ { "id": "gold", "name": "Gold", "types": ["groza"],   (none = every type)
//                    "engineColor": [1, 0.8, 0.5],   the exhausts' glow in this skin (none = the mounts' own)
//                    "materials": [ { "material": "ship_terran_000_body", "diffuse": "skins/groza_gold.jpg",
//                                     "emission": "...png", "metallicSmoothness": "...png", "normal": "...png",
//                                     "color": [r, g, b], "emissionColor": [r, g, b], "emissionIntensity": 1 } ] } ]
//     Paint jobs as texture sets (Galaxy on Fire 3's paint schemes): a build's skin swaps the textures of copies of the
//     materials whose name contains 'material' (the model's own glTF material names; ModShips.ApplySkin); a field left out
//     keeps the model's own. No skin = the model's own look. A skin's textures load only when a build shows it.
//   Every kit ship also has a weapon colour (Build.weaponColor, "weaponcolor=<id or #RRGGBB>"; none = the weapons' own look):
//   the energy weapons' shots in that colour (Flight.WeaponTint), picked from the kit's "weapons" palette, else WeaponPalette.
// Customizations of other ships (the game's own, and mod ships built from one model): a ships.json entry ("override": n or
// "mod:id", or a new ship without "kit") with "skins" (the format above, for that ship) and / or "weaponColors" (a palette list,
// or true for the built-in one). Such a ship gets a kit of its own with no parts (CustomizationOf: the skins of every mod that
// is on, merged), so the Customize screen, builds (saved by BuildKey: "gof2:<n>" for the game's ships), the weapon colour and
// other players' views work as for kit ships; its skins show on a copy of its model (ModShips.SkinnedTemplate).
// With exhaust styles in the mods that are on (ModExhausts, exhausts.json) every ship has a Customize screen: a ship without
// other customizations gets a kit with nothing but them (no weapon colour row). A build's exhaust ("exhaust=mod_id:id",
// "exhaustcolor=", "exhaustcolor2=", "exhaustsize=") is put on the model's instance (ModExhausts.Apply), not into its assembly.
// ships.json: a new ship with "kit": "<kit id>" (this mod's) or "mod_id:kit_id" instead of "model", "kitType": "sentinel",
// and "build": { "wings": "nemesis", "body": "body_a", ..., "tier": 1 } (its default: what the dealers sell and NPCs fly;
// a slot left out takes the first part the type may fit). The player's build of each kit ship is Session.ShipBuilds (by the
// ship's key, saved), shown by PlayerHull.Assembly as the variant assembly "ship_NNN_mod#<build>" (ModShips.Template
// assembles it from the loaded parts the first time). Builds are text: "body=body_a;rear=rear_1;wings=nemesis;tier=1".

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModShipKits
    {
        public const string File = "shipkits.json";

        static readonly HashSet<string> KitFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "id", "name", "slots", "scale", "yaw", "materials", "parts", "palettes", "colorSlots", "presets", "hardpoints", "skins" };
        static readonly HashSet<string> SkinFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "id", "name", "types", "engineColor", "materials" };
        static readonly HashSet<string> SkinMaterialFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "material", "diffuse", "emission", "metallicSmoothness", "normal", "color", "emissionColor", "emissionIntensity" };
        static readonly HashSet<string> PartFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "id", "slot", "name", "model", "types", "tiers", "mounts", "tierMounts", "stats", "slots", "anchors", "anchor" };

        public sealed class Part
        {
            public string id, slot, model;
            public Dictionary<string, string> name;
            public List<string> types = new List<string>(), tiers = new List<string>();
            public List<WeaponMount> mounts = new List<WeaponMount>();
            public List<List<WeaponMount>> tierMounts = new List<List<WeaponMount>>();
            public float armor, cargo, handling, topSpeed, turnRate, acceleration, strafeSpeed;   // % (stats)
            public int equipment;                                                                  // slots (stats)
            public int primarySlots = -1, secondarySlots = -1, turretSlots = -1;                  // -1 = not given
            /// <summary>Named points this part offers others (model units, the GLB's own glTF coordinates), and the anchor
            /// this part sits on (its model and tier models moved to that point of the build's part that offers it).</summary>
            public Dictionary<string, Vector3> anchors = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            public string anchor;
            public string Name => ModManifest.Pick(name) ?? id;
            public bool Allows(string type) => types.Count == 0 || types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A palette entry: a colour with an optional finish (metallic / smoothness, -1 = the material's own).</summary>
        public sealed class ColorChoice
        {
            public string id;
            public Dictionary<string, string> name;
            public Color color = Color.white;
            public float metallic = -1f, smoothness = -1f;
            public string Name => ModManifest.Pick(name) ?? id;
        }

        public sealed class ColorTarget
        {
            public string material, property = "base";
            public float intensity = 1f;
        }

        public sealed class ColorSlot
        {
            public string id, palette, defaultValue;
            public Dictionary<string, string> name;
            public List<ColorTarget> targets = new List<ColorTarget>();
            /// <summary>A strength slot (no palette): the values the player steps through (0..1), scaling the emission
            /// targets of the slot 'scales' names (EVERSPACE 2's Emissive 3 intensity slider).</summary>
            public float[] steps;
            public string scales;
            public bool IsStrength => steps != null && steps.Length > 0;
            public string Name => ModManifest.Pick(name) ?? id;
        }

        public sealed class Preset
        {
            public string id;
            public Dictionary<string, string> name;
            public Dictionary<string, string> colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string Name => ModManifest.Pick(name) ?? id;
        }

        /// <summary>One material's textures in a skin (paths in the mod; null = the model's own).</summary>
        public sealed class SkinMaterial
        {
            public string material, diffuse, emission, metallicSmoothness, normal;
            public float[] color, emissionColor;
            public float emissionIntensity = 1f;
        }

        /// <summary>A paint job: texture sets for the materials it names (ModShips.ApplySkin).</summary>
        public sealed class Skin
        {
            public ModInfo mod;   // the mod whose files it uses (a ship's customizations can come from several)
            public string id;
            public Dictionary<string, string> name;
            public List<string> types = new List<string>();
            public float[] engineColor;
            public List<SkinMaterial> materials = new List<SkinMaterial>();
            public string Name => ModManifest.Pick(name) ?? id;
            public bool Allows(string type) => types.Count == 0 || types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase));

            /// <summary>The image files it uses, with how they are read (linear, normal map).</summary>
            public IEnumerable<(string path, bool linear, bool normal)> Textures()
            {
                foreach (var m in materials)
                {
                    if (!string.IsNullOrEmpty(m.diffuse)) yield return (m.diffuse, false, false);
                    if (!string.IsNullOrEmpty(m.emission)) yield return (m.emission, false, false);
                    if (!string.IsNullOrEmpty(m.metallicSmoothness)) yield return (m.metallicSmoothness, true, false);
                    if (!string.IsNullOrEmpty(m.normal)) yield return (m.normal, true, true);
                }
            }
        }

        public sealed class Kit
        {
            public readonly Dictionary<string, List<ColorChoice>> palettes = new Dictionary<string, List<ColorChoice>>(StringComparer.OrdinalIgnoreCase);
            public readonly List<ColorSlot> colorSlots = new List<ColorSlot>();
            public readonly List<Preset> presets = new List<Preset>();

            public List<ColorChoice> PaletteOf(ColorSlot slot) => slot != null && slot.palette != null && palettes.TryGetValue(slot.palette, out var l) ? l : new List<ColorChoice>();

            public ModInfo mod;
            public string localId;
            public Dictionary<string, string> name;
            public List<string> slots = new List<string>();
            public float scale = 1f, yaw;
            public List<CustomShipMaterial> materials = new List<CustomShipMaterial>();
            public List<Part> parts = new List<Part>();
            public ModHardpoints.Set hardpoints;   // the weapon / turret models (ModHardpoints), null = none
            public readonly List<Skin> skins = new List<Skin>();
            /// <summary>The Customize screen offers the weapon colour (false: a ship whose only customizations are the mods'
            /// exhaust styles).</summary>
            public bool weaponColorRow = true;
            public string Key => mod.Id + ":" + localId;
            public string Name => ModManifest.Pick(name) ?? localId;
            public int MaxTier => parts.Count == 0 ? 0 : parts.Max(p => p.tiers.Count);

            public Part Find(string id) => parts.Find(p => string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase));

            public Skin FindSkin(string id) => string.IsNullOrEmpty(id) ? null : skins.Find(k => string.Equals(k.id, id, StringComparison.OrdinalIgnoreCase));

            /// <summary>The skins a ship type may wear, in the file's order.</summary>
            public List<Skin> SkinsFor(string type) => skins.Where(k => k.Allows(type)).ToList();

            /// <summary>Whether the Customize screen has anything for a ship type: a slot with more than one part, a tier, colours
            /// or skins.</summary>
            public bool HasChoices(string type) => true;   // the weapon colour, on every kit ship

            /// <summary>The weapon colours: the kit's palette "weapons", else the built-in WeaponPalette.</summary>
            public List<ColorChoice> WeaponColors => palettes.TryGetValue("weapons", out var w) && w.Count > 0 ? w : WeaponPalette;

            /// <summary>The parts of a slot a ship type may fit, in the file's order.</summary>
            public List<Part> PartsFor(string slot, string type) =>
                parts.Where(p => string.Equals(p.slot, slot, StringComparison.OrdinalIgnoreCase) && p.Allows(type)).ToList();
        }

        static readonly List<Kit> kits = new List<Kit>();
        static int parsedRevision = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { kits.Clear(); parsedRevision = -1; shipCustom.Clear(); exhaustOnly.Clear(); customRevision = -1; }

        /// <summary>The kits of the mods that are on.</summary>
        public static List<Kit> All()
        {
            if (parsedRevision == ModManager.Revision) return kits;
            parsedRevision = ModManager.Revision;
            kits.Clear();
            foreach (var mod in ModManager.Active)
            {
                try { Read(mod, kits); }
                catch (ModJsonException e) { Warn(mod, e.Message); }
            }
            return kits;
        }

        /// <summary>A kit by "mod_id:kit_id", or by its kit id alone within 'mod' (a ship's own mod).</summary>
        public static Kit Find(string key, ModInfo mod = null)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int colon = key.IndexOf(':');
            foreach (var k in All())
            {
                if (colon > 0 ? string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase)
                              : mod != null && k.mod == mod && string.Equals(k.localId, key, StringComparison.OrdinalIgnoreCase))
                    return k;
            }
            return null;
        }

        static void Read(ModInfo mod, List<Kit> into)
        {
            if (!(ModJson.Read(mod.Source, File) is JToken token)) return;
            if (!(token is JArray list)) throw new ModJsonException($"{File}: must be a list [ {{ ... }}, ... ]");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in list)
            {
                string where = ModJson.Where(t, File);
                if (!(t is JObject o)) throw new ModJsonException($"{where}: each kit must be an object {{ ... }}");
                foreach (var prop in o.Properties())
                    if (!KitFields.Contains(prop.Name)) Warn(mod, $"{ModJson.Where(prop, File)}: unknown field \"{prop.Name}\" (ignored)");
                var k = new Kit
                {
                    mod = mod, localId = ModJson.Str(o, "id"), name = ModJson.Text(o, "name"),
                    scale = ModJson.Float(o, "scale", 1f, File), yaw = ModJson.Float(o, "yaw", 0f, File),
                };
                if (string.IsNullOrEmpty(k.localId) || !ModManifest.ValidId(k.localId))
                    throw new ModJsonException($"{where}: \"id\" is missing or uses more than a-z, 0-9, _ and -");
                if (!ids.Add(k.localId)) throw new ModJsonException($"{where}: the id \"{k.localId}\" is used twice");
                if (k.scale <= 0f) throw new ModJsonException($"{where}: \"scale\" must be above 0");
                if (ModJson.Get(o, "materials") is JToken mt)
                {
                    try { k.materials = JsonUtility.FromJson<MaterialList>("{\"list\":" + mt.ToString() + "}").list ?? new List<CustomShipMaterial>(); }
                    catch (Exception e) { throw new ModJsonException($"{where}: \"materials\": {e.Message}"); }
                }
                if (!(ModJson.Get(o, "parts") is JArray parts)) throw new ModJsonException($"{where}: \"parts\" must be a list of the kit's parts");
                // A kit still being made (the Mod Builder writes one before its first part): left out, not an error.
                if (parts.Count == 0) { Warn(mod, $"{where}: the kit \"{k.localId}\" has no parts yet (left out)"); continue; }
                var partIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pt in parts)
                {
                    string pwhere = ModJson.Where(pt, File);
                    if (!(pt is JObject po)) throw new ModJsonException($"{pwhere}: each part must be an object {{ ... }}");
                    foreach (var prop in po.Properties())
                        if (!PartFields.Contains(prop.Name)) Warn(mod, $"{ModJson.Where(prop, File)}: unknown field \"{prop.Name}\" (ignored)");
                    var p = new Part
                    {
                        id = ModJson.Str(po, "id"), slot = ModJson.Str(po, "slot"), model = ModJson.Str(po, "model"), name = ModJson.Text(po, "name"),
                        types = ModJson.Strings(po, "types") ?? new List<string>(), tiers = ModJson.Strings(po, "tiers") ?? new List<string>(),
                    };
                    if (string.IsNullOrEmpty(p.id) || !ModManifest.ValidId(p.id))
                        throw new ModJsonException($"{pwhere}: \"id\" is missing or uses more than a-z, 0-9, _ and -");
                    p.anchor = ModJson.Str(po, "anchor");
                    if (ModJson.Get(po, "anchors") is JObject an)
                        foreach (var ap in an.Properties())
                            if (ap.Value is JArray av && av.Count >= 3)
                                p.anchors[ap.Name] = new Vector3((float)av[0], (float)av[1], (float)av[2]);
                    if (!partIds.Add(p.id)) throw new ModJsonException($"{pwhere}: the part id \"{p.id}\" is used twice in this kit");
                    if (string.IsNullOrEmpty(p.slot) || !ModManifest.ValidId(p.slot)) throw new ModJsonException($"{pwhere}: \"slot\" is missing (e.g. \"wings\")");
                    if (Build.Reserved(p.slot)) throw new ModJsonException($"{pwhere}: \"{p.slot}\" can't be a slot's name (a build uses it)");
                    if (string.IsNullOrEmpty(p.model)) throw new ModJsonException($"{pwhere}: \"model\" is missing (the part's glTF / GLB)");
                    if (!mod.Source.Exists(p.model)) throw new ModJsonException($"{pwhere}: the model \"{p.model}\" isn't in the mod");
                    foreach (var tier in p.tiers)
                        if (!mod.Source.Exists(tier)) throw new ModJsonException($"{pwhere}: the tier model \"{tier}\" isn't in the mod");
                    p.mounts = ReadMounts(ModJson.Get(po, "mounts"), pwhere);
                    if (ModJson.Get(po, "stats") is JObject st)
                    {
                        p.armor = ModJson.Float(st, "armor", 0f, File); p.cargo = ModJson.Float(st, "cargo", 0f, File);
                        p.handling = ModJson.Float(st, "handling", 0f, File); p.equipment = ModJson.Int(st, "equipment", 0, File);
                        p.topSpeed = ModJson.Float(st, "topSpeed", 0f, File); p.turnRate = ModJson.Float(st, "turnRate", 0f, File);
                        p.acceleration = ModJson.Float(st, "acceleration", 0f, File); p.strafeSpeed = ModJson.Float(st, "strafeSpeed", 0f, File);
                    }
                    if (ModJson.Get(po, "slots") is JObject sl)
                    {
                        p.primarySlots = ModJson.Int(sl, "primary", -1, File); p.secondarySlots = ModJson.Int(sl, "secondary", -1, File);
                        p.turretSlots = ModJson.Int(sl, "turret", -1, File);
                    }
                    if (ModJson.Get(po, "tierMounts") is JArray tm)
                        foreach (var t2 in tm) p.tierMounts.Add(ReadMounts(t2, pwhere));
                    k.parts.Add(p);
                }
                var slots = ModJson.Strings(o, "slots");
                k.slots = slots != null && slots.Count > 0 ? slots : k.parts.Select(p => p.slot).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var p in k.parts)
                    if (!k.slots.Contains(p.slot, StringComparer.OrdinalIgnoreCase))
                        throw new ModJsonException($"{where}: the part \"{p.id}\"'s slot \"{p.slot}\" isn't in \"slots\"");
                ReadColors(o, k, where);
                ReadSkins(o, k, mod, where);
                k.hardpoints = ModHardpoints.Read(o, mod, File, where);
                into.Add(k);
            }
        }

        [Serializable] sealed class MaterialList { public List<CustomShipMaterial> list; }

        static List<WeaponMount> ReadMounts(JToken t, string where)
        {
            var list = new List<WeaponMount>();
            if (t == null) return list;
            if (!(t is JArray arr)) throw new ModJsonException($"{where}: \"mounts\" must be a list of mounts");
            foreach (var e in arr)
            {
                if (!(e is JObject)) continue;
                try { list.Add(JsonUtility.FromJson<WeaponMount>(e.ToString())); }
                catch (Exception ex) { throw new ModJsonException($"{where}: a mount: {ex.Message}"); }
            }
            return list;
        }

        /// <summary>A build's mounts: for each slot type its parts' mounts (base and tier extension, in the kit's slot order),
        /// else the ship's own of that type.</summary>
        public static List<WeaponMount> MountsFor(CustomShipData c, Kit kit, Build build)
        {
            var fromParts = new List<WeaponMount>();
            foreach (string slot in kit.slots)
            {
                if (!build.parts.TryGetValue(slot, out var id)) continue;
                var part = kit.Find(id);
                if (part == null) continue;
                fromParts.AddRange(part.mounts);
                if (build.tier > 0 && build.tier <= part.tierMounts.Count) fromParts.AddRange(part.tierMounts[build.tier - 1]);
            }
            var all = new List<WeaponMount>();
            foreach (int type in new[] { 0, 1, 2, 3, 4 })
            {
                var own = fromParts.Where(m => m.slotType == type).ToList();
                all.AddRange(own.Count > 0 ? own : (c.mounts ?? new List<WeaponMount>()).Where(m => m.slotType == type));
            }
            return all;
        }


        public static KitEffect EffectOf(Kit kit, Build build)
        {
            var e = new KitEffect();
            foreach (string slot in kit.slots)
            {
                if (!build.parts.TryGetValue(slot, out var id)) continue;
                var p = kit.Find(id);
                if (p == null) continue;
                e.armor += p.armor; e.cargo += p.cargo; e.handling += p.handling; e.equipment += p.equipment;
                e.flight.topSpeed += p.topSpeed; e.flight.turnRate += p.turnRate; e.flight.acceleration += p.acceleration; e.flight.strafeSpeed += p.strafeSpeed;
                if (p.primarySlots >= 0) e.primary = Math.Max(0, e.primary) + p.primarySlots;
                if (p.secondarySlots >= 0) e.secondary = Math.Max(0, e.secondary) + p.secondarySlots;
                if (p.turretSlots >= 0) e.turret = Math.Max(0, e.turret) + p.turretSlots;
            }
            return e;
        }

        /// <summary>The build effect of the player's ship (null: not a kit ship); 'ship' another than the player's: its default build.</summary>
        public static KitEffect BuildEffect(int ship)
        {
            var c = KitShip(ship);
            var kit = KitOf(c);
            if (kit == null) return null;
            return EffectOf(kit, ship == Session.ShipIndex ? PlayerBuild(ship) : DefaultBuild(c, kit));
        }

        /// <summary>A ship's hull, cargo, handling and slots with its build: KitRules (GoF2.Game).</summary>
        public static int Armor(ShipData s, int fallback) => KitRules.Armor(s, fallback);
        public static int Cargo(ShipData s, int fallback) => KitRules.Cargo(s, fallback);
        public static float Handling(ShipData s) => KitRules.Handling(s);
        public static ShipSlots Slots(ShipData s) => KitRules.Slots(s);

        /// <summary>The ship as its build makes it (a copy with the hull, cargo, handling and slots adjusted; the ship itself
        /// when it isn't a kit ship): for the stat rows (ItemInfo, ItemInfoWindow).</summary>
        public static ShipData Effective(ShipData s)
        {
            if (s == null || BuildEffect(s.index) == null) return s;
            float handling = Handling(s);
            return new ShipData { index = s.index, name = s.name, description = s.description, price = s.price, armor = Armor(s, s.armor),
                                  cargo = Cargo(s, s.cargo), handling = handling, handlingMultiplier = handling / 100f, slots = Slots(s) };
        }

        /// <summary>Database.MountsOf for a kit ship: the player's build for the player's ship, else its default build; null:
        /// not a kit ship.</summary>
        /// <summary>The mounts of a type a model of 'ship' in 'build' shows (another player's build, a hangar ship's): a kit
        /// ship's for that build (normalized; null = its default build), any other ship's Database.MountsOf.</summary>
        public static List<WeaponMount> MountsShown(Database db, int ship, int slotType, Build build)
        {
            var c = KitShip(ship);
            var kit = KitOf(c);
            if (kit == null) return db.MountsOf(ship, slotType);
            var b = build != null ? Normalize(kit, c.kitType, build, DefaultBuild(c, kit)) : DefaultBuild(c, kit);
            return MountsFor(c, kit, b).Where(m => m.slotType == slotType).ToList();
        }

        /// <summary>The build an assembly shows: "ship_NNN_mod#<build>" (VariantAssembly) -> that build, else null.</summary>
        public static Build BuildOfAssembly(string assembly)
        {
            int hash = assembly?.IndexOf(VariantSeparator) ?? -1;
            return hash > 0 ? Build.Parse(assembly.Substring(hash + 1)) : null;
        }

        /// <summary>Another player's build of their kit ship from its text (NetPlayer.BuildText), normalized against what this
        /// game's kit has (unknown parts: the default build's); the default build for an empty text; null = no kit ship.</summary>
        public static Build RemoteBuild(int ship, string text)
        {
            var kit = CustomizationOf(ship);
            return kit == null ? null : Normalize(kit, TypeOf(ship), string.IsNullOrEmpty(text) ? null : Build.Parse(text), DefaultBuildOf(ship, kit));
        }

        public static List<WeaponMount> BuildMounts(int ship, int slotType)
        {
            var c = KitShip(ship);
            var kit = KitOf(c);
            if (kit == null) return null;
            var build = ship == Session.ShipIndex ? PlayerBuild(ship) : DefaultBuild(c, kit);
            return MountsFor(c, kit, build).Where(m => m.slotType == slotType).ToList();
        }

        internal static List<ColorChoice> ReadPalette(JArray arr, string file)
        {
            var list = new List<ColorChoice>();
            foreach (var e in arr)
            {
                if (!(e is JObject eo)) continue;
                var c = new ColorChoice
                {
                    id = ModJson.Str(eo, "id"), name = ModJson.Text(eo, "name"),
                    metallic = ModJson.Float(eo, "metallic", -1f, file), smoothness = ModJson.Float(eo, "smoothness", -1f, file),
                };
                if (string.IsNullOrEmpty(c.id)) throw new ModJsonException($"{ModJson.Where(e, file)}: a palette colour needs an \"id\"");
                if (!TryHex(ModJson.Str(eo, "color"), out c.color)) throw new ModJsonException($"{ModJson.Where(e, file)}: \"color\" must be \"#RRGGBB\"");
                list.Add(c);
            }
            return list;
        }

        static void ReadColors(JObject o, Kit k, string where)
        {
            if (ModJson.Get(o, "palettes") is JToken pt)
            {
                if (!(pt is JObject po)) throw new ModJsonException($"{where}: \"palettes\" must be an object {{ \"main\": [ ... ] }}");
                foreach (var prop in po.Properties())
                {
                    if (!(prop.Value is JArray arr)) throw new ModJsonException($"{ModJson.Where(prop, File)}: a palette must be a list of colours");
                    k.palettes[prop.Name] = ReadPalette(arr, File);
                }
            }
            if (ModJson.Get(o, "colorSlots") is JArray slots)
                foreach (var t in slots)
                {
                    if (!(t is JObject so)) continue;
                    var slot = new ColorSlot { id = ModJson.Str(so, "id"), palette = ModJson.Str(so, "palette"), defaultValue = ModJson.Str(so, "default"), name = ModJson.Text(so, "name") };
                    if (string.IsNullOrEmpty(slot.id) || !ModManifest.ValidId(slot.id)) throw new ModJsonException($"{ModJson.Where(t, File)}: a colour slot needs an \"id\" (a-z, 0-9, _ and -)");
                    if (ModJson.Get(so, "steps") is JArray st)
                    {
                        // A strength slot: "steps": [0, 0.25, ...], "scales": "<colour slot id>", "default": "0.5".
                        slot.steps = st.Where(x => x.Type == JTokenType.Float || x.Type == JTokenType.Integer).Select(x => Mathf.Clamp((float)x, 0f, 4f)).ToArray();
                        slot.scales = ModJson.Str(so, "scales");
                        if (slot.steps.Length == 0 || string.IsNullOrEmpty(slot.scales)) throw new ModJsonException($"{ModJson.Where(t, File)}: a strength slot needs numbers in \"steps\" and the colour slot it \"scales\"");
                        k.colorSlots.Add(slot);
                        continue;
                    }
                    if (slot.palette == null || !k.palettes.ContainsKey(slot.palette)) throw new ModJsonException($"{ModJson.Where(t, File)}: \"palette\" \"{slot.palette}\" isn't in \"palettes\"");
                    if (ModJson.Get(so, "targets") is JArray ta)
                        foreach (var x in ta)
                            if (x is JObject xo)
                                slot.targets.Add(new ColorTarget
                                {
                                    material = ModJson.Str(xo, "material") ?? "", property = (ModJson.Str(xo, "property") ?? "base").ToLowerInvariant(),
                                    intensity = ModJson.Float(xo, "intensity", 1f, File),
                                });
                    k.colorSlots.Add(slot);
                }
            if (ModJson.Get(o, "presets") is JArray presets)
                foreach (var t in presets)
                {
                    if (!(t is JObject pr)) continue;
                    var preset = new Preset { id = ModJson.Str(pr, "id"), name = ModJson.Text(pr, "name") };
                    if (string.IsNullOrEmpty(preset.id)) throw new ModJsonException($"{ModJson.Where(t, File)}: a preset needs an \"id\"");
                    if (ModJson.Get(pr, "colors") is JObject co)
                        foreach (var prop in co.Properties()) if (prop.Value.Type == JTokenType.String) preset.colors[prop.Name] = (string)prop.Value;
                    k.presets.Add(preset);
                }
        }

        static ColorChoice Choice(string id, string english, string hex)
        {
            TryHex(hex, out var c);
            return new ColorChoice { id = id, name = new Dictionary<string, string> { ["en"] = english }, color = c };
        }

        /// <summary>The built-in weapon colours (bright, so the bolts glow in them).</summary>
        public static readonly List<ColorChoice> WeaponPalette = new List<ColorChoice>
        {
            Choice("red", "Red", "#FF3030"), Choice("orange", "Orange", "#FF8A1A"), Choice("yellow", "Yellow", "#FFE135"),
            Choice("lime", "Lime", "#9CFF2E"), Choice("green", "Green", "#2EFF6A"), Choice("cyan", "Cyan", "#2EF6FF"),
            Choice("blue", "Blue", "#3D7BFF"), Choice("violet", "Violet", "#9B4DFF"), Choice("magenta", "Magenta", "#FF3DE8"),
            Choice("pink", "Pink", "#FF7AB8"), Choice("white", "White", "#F4F8FF"),
        };

        /// <summary>A weapon colour value: a weapon palette id or "#RRGGBB" (false: none, or not one).</summary>
        public static bool ResolveWeaponColor(Kit kit, string value, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(value) || value == Build.NoSkin) return false;
            if (value.StartsWith("#")) return TryHex(value, out color);
            var choice = kit.WeaponColors.Find(c => string.Equals(c.id, value, StringComparison.OrdinalIgnoreCase));
            if (choice == null) return false;
            color = choice.color;
            return true;
        }

        /// <summary>A build's weapon colour (null = the weapons' own look).</summary>
        public static Color? WeaponColor(Kit kit, Build build) =>
            kit != null && build != null && ResolveWeaponColor(kit, build.weaponColor, out var c) ? c : (Color?)null;

        static void ReadSkins(JObject o, Kit k, ModInfo mod, string where, string file = File)
        {
            if (!(ModJson.Get(o, "skins") is JToken st)) return;
            if (!(st is JArray list)) throw new ModJsonException($"{where}: \"skins\" must be a list of skins");
            float[] Rgb(JObject x, string field, string w)
            {
                if (!(ModJson.Get(x, field) is JToken t)) return null;
                if (!(t is JArray a) || a.Count < 3) throw new ModJsonException($"{w}: \"{field}\" must be [r, g, b] (0..1)");
                return a.Take(3).Select(v => (float)v).ToArray();
            }
            foreach (var t in list)
            {
                string swhere = ModJson.Where(t, file);
                if (!(t is JObject so)) throw new ModJsonException($"{swhere}: each skin must be an object {{ \"id\", \"name\", \"materials\" }}");
                foreach (var prop in so.Properties())
                    if (!SkinFields.Contains(prop.Name)) Warn(mod, $"{ModJson.Where(prop, file)}: unknown field \"{prop.Name}\" (ignored)");
                var skin = new Skin { mod = mod, id = ModJson.Str(so, "id"), name = ModJson.Text(so, "name"), types = ModJson.Strings(so, "types") ?? new List<string>(),
                                      engineColor = Rgb(so, "engineColor", swhere) };
                if (string.IsNullOrEmpty(skin.id) || !ModManifest.ValidId(skin.id)) throw new ModJsonException($"{swhere}: a skin's \"id\" is missing or uses more than a-z, 0-9, _ and -");
                if (skin.id == Build.NoSkin) throw new ModJsonException($"{swhere}: \"none\" can't be a skin's id (it means the model's own look)");
                if (k.skins.Exists(x => string.Equals(x.id, skin.id, StringComparison.OrdinalIgnoreCase))) throw new ModJsonException($"{swhere}: the skin id \"{skin.id}\" is used twice in this kit");
                if (ModJson.Get(so, "materials") is JArray ma)
                    foreach (var mt in ma)
                    {
                        string mwhere = ModJson.Where(mt, file);
                        if (!(mt is JObject mo)) throw new ModJsonException($"{mwhere}: each skin material must be an object {{ \"material\", \"diffuse\", ... }}");
                        foreach (var prop in mo.Properties())
                            if (!SkinMaterialFields.Contains(prop.Name)) Warn(mod, $"{ModJson.Where(prop, file)}: unknown field \"{prop.Name}\" (ignored)");
                        var m = new SkinMaterial
                        {
                            material = ModJson.Str(mo, "material"), diffuse = ModJson.Str(mo, "diffuse"), emission = ModJson.Str(mo, "emission"),
                            metallicSmoothness = ModJson.Str(mo, "metallicSmoothness"), normal = ModJson.Str(mo, "normal"),
                            color = Rgb(mo, "color", mwhere), emissionColor = Rgb(mo, "emissionColor", mwhere),
                            emissionIntensity = ModJson.Float(mo, "emissionIntensity", 1f, file),
                        };
                        if (string.IsNullOrEmpty(m.material)) throw new ModJsonException($"{mwhere}: \"material\" is missing (the model's material name, or part of it)");
                        foreach (var path in new[] { m.diffuse, m.emission, m.metallicSmoothness, m.normal })
                            if (!string.IsNullOrEmpty(path) && !mod.Source.Exists(path)) throw new ModJsonException($"{mwhere}: the texture \"{path}\" isn't in the mod");
                        skin.materials.Add(m);
                    }
                if (skin.materials.Count == 0 && skin.engineColor == null) Warn(mod, $"{swhere}: the skin \"{skin.id}\" changes nothing (no \"materials\")");
                k.skins.Add(skin);
            }
        }

        internal static bool TryHex(string text, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrEmpty(text)) return false;
            return ColorUtility.TryParseHtmlString(text.StartsWith("#") ? text : "#" + text, out c);
        }

        /// <summary>A colour value: a palette id of the slot's palette, or "#RRGGBB[/metallic/smoothness]"; a strength slot's
        /// value is a number (the colour then grey at that level).</summary>
        public static bool Resolve(Kit kit, ColorSlot slot, string value, out Color color, out float metallic, out float smoothness)
        {
            color = Color.white; metallic = smoothness = -1f;
            if (string.IsNullOrEmpty(value)) return false;
            if (slot != null && slot.IsStrength)
            {
                if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return false;
                f = Mathf.Clamp(f, 0f, 4f);
                color = new Color(f, f, f, 1f);
                return true;
            }
            if (value.StartsWith("#"))
            {
                var bits = value.Split('/');
                if (!TryHex(bits[0], out color)) return false;
                if (bits.Length > 1) float.TryParse(bits[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out metallic);
                if (bits.Length > 2) float.TryParse(bits[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out smoothness);
                return true;
            }
            var choice = kit.PaletteOf(slot).Find(c => string.Equals(c.id, value, StringComparison.OrdinalIgnoreCase));
            if (choice == null) return false;
            color = choice.color; metallic = choice.metallic; smoothness = choice.smoothness;
            return true;
        }

        /// <summary>The strength a build gives a colour slot's emission (a strength slot that scales it; 1 without one).</summary>
        public static float StrengthOf(Kit kit, Build build, string slotId)
        {
            var s = kit.colorSlots.Find(c => c.IsStrength && string.Equals(c.scales, slotId, StringComparison.OrdinalIgnoreCase));
            if (s == null) return 1f;
            string v = build.colors.TryGetValue(s.id, out var bv) ? bv : s.defaultValue;
            return Resolve(kit, s, v, out var c, out _, out _) ? c.r : 1f;
        }

        static void Warn(ModInfo mod, string message)
        {
            if (!mod.Warnings.Contains(message)) mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }

        // ---- builds ----------------------------------------------------------------------------------------------------

        /// <summary>One ship's choice of parts (slot -> part id) and its tier (which extension meshes show).</summary>
        public sealed class Build
        {
            public readonly SortedDictionary<string, string> parts = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            /// <summary>A colour value per colour slot (written "color.slot=value").</summary>
            public readonly SortedDictionary<string, string> colors = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public int tier;
            /// <summary>The skin's id (written "skin=id"); null (or NoSkin) = the model's own look.</summary>
            public string skin;
            /// <summary>The model's own look chosen where the default build has a skin.</summary>
            public const string NoSkin = "none";
            /// <summary>The weapon colour (written "weaponcolor=id" or "#RRGGBB"; null or "none" = the weapons' own).</summary>
            public string weaponColor;
            /// <summary>The exhaust style (ModExhausts, written "exhaust=mod_id:id"; null or "none" = the ship's own) and its two
            /// colours ("exhaustcolor=" / "exhaustcolor2=": a palette id of the style or "#RRGGBB"; null = the style's own).</summary>
            public string exhaust, exhaustColor, exhaustColor2;
            /// <summary>The exhaust style's size ("exhaustsize=1.2": x its fitted size, ModExhausts.MinSize .. MaxSize; null = 1).</summary>
            public string exhaustSize;
            const string ColorPrefix = "color.", SkinKey = "skin", WeaponColorKey = "weaponcolor";
            const string ExhaustKey = "exhaust", ExhaustColorKey = "exhaustcolor", ExhaustColor2Key = "exhaustcolor2", ExhaustSizeKey = "exhaustsize";

            /// <summary>Names a kit can't use for a slot: the build text's own keys.</summary>
            public static bool Reserved(string slot) => string.Equals(slot, "tier", StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(slot, "ship", StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(slot, SkinKey, StringComparison.OrdinalIgnoreCase)
                                                        || string.Equals(slot, WeaponColorKey, StringComparison.OrdinalIgnoreCase)
                                                        || slot.StartsWith(ExhaustKey, StringComparison.OrdinalIgnoreCase)
                                                        || slot.StartsWith(ColorPrefix, StringComparison.OrdinalIgnoreCase);

            public static Build Parse(string text)
            {
                var b = new Build();
                if (string.IsNullOrEmpty(text)) return b;
                foreach (var pair in text.Split(';'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = pair.Substring(0, eq).Trim(), v = pair.Substring(eq + 1).Trim();
                    if (k == "tier") int.TryParse(v, out b.tier);
                    else if (k == SkinKey) b.skin = v.Length > 0 ? v : null;
                    else if (k == WeaponColorKey) b.weaponColor = v.Length > 0 ? v : null;
                    else if (k == ExhaustKey) b.exhaust = v.Length > 0 ? v : null;
                    else if (k == ExhaustColorKey) b.exhaustColor = v.Length > 0 ? v : null;
                    else if (k == ExhaustColor2Key) b.exhaustColor2 = v.Length > 0 ? v : null;
                    else if (k == ExhaustSizeKey) b.exhaustSize = v.Length > 0 ? v : null;
                    else if (k.StartsWith(ColorPrefix)) { if (v.Length > 0) b.colors[k.Substring(ColorPrefix.Length)] = v; }
                    else if (v.Length > 0) b.parts[k] = v;
                }
                return b;
            }

            /// <summary>ships.json's "build" object.</summary>
            public static Build FromJson(JObject o)
            {
                var b = new Build();
                if (o == null) return b;
                foreach (var prop in o.Properties())
                {
                    if (string.Equals(prop.Name, "tier", StringComparison.OrdinalIgnoreCase)) { b.tier = prop.Value.Type == JTokenType.Integer ? (int)prop.Value : 0; continue; }
                    if (string.Equals(prop.Name, SkinKey, StringComparison.OrdinalIgnoreCase)) { b.skin = prop.Value.Type == JTokenType.String ? (string)prop.Value : null; continue; }
                    if (string.Equals(prop.Name, "weaponColor", StringComparison.OrdinalIgnoreCase)) { b.weaponColor = prop.Value.Type == JTokenType.String ? (string)prop.Value : null; continue; }
                    if (string.Equals(prop.Name, ExhaustKey, StringComparison.OrdinalIgnoreCase)) { b.exhaust = prop.Value.Type == JTokenType.String ? (string)prop.Value : null; continue; }
                    if (string.Equals(prop.Name, "exhaustColor", StringComparison.OrdinalIgnoreCase)) { b.exhaustColor = prop.Value.Type == JTokenType.String ? (string)prop.Value : null; continue; }
                    if (string.Equals(prop.Name, "exhaustColor2", StringComparison.OrdinalIgnoreCase)) { b.exhaustColor2 = prop.Value.Type == JTokenType.String ? (string)prop.Value : null; continue; }
                    if (string.Equals(prop.Name, "exhaustSize", StringComparison.OrdinalIgnoreCase))
                    {
                        b.exhaustSize = prop.Value.Type == JTokenType.Float || prop.Value.Type == JTokenType.Integer
                            ? ((float)prop.Value).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                            : prop.Value.Type == JTokenType.String ? (string)prop.Value : null;
                        continue;
                    }
                    if (string.Equals(prop.Name, "colors", StringComparison.OrdinalIgnoreCase) && prop.Value is JObject co)
                    {
                        foreach (var c in co.Properties()) if (c.Value.Type == JTokenType.String) b.colors[c.Name] = (string)c.Value;
                        continue;
                    }
                    if (prop.Value.Type == JTokenType.String) b.parts[prop.Name] = (string)prop.Value;
                }
                return b;
            }

            public Build Clone()
            {
                var b = new Build { tier = tier, skin = skin, weaponColor = weaponColor, exhaust = exhaust, exhaustColor = exhaustColor, exhaustColor2 = exhaustColor2, exhaustSize = exhaustSize };
                foreach (var kv in parts) b.parts[kv.Key] = kv.Value;
                foreach (var kv in colors) b.colors[kv.Key] = kv.Value;
                return b;
            }

            public override string ToString() => string.Join(";", parts.Select(kv => kv.Key + "=" + kv.Value)
                .Concat(colors.Select(kv => ColorPrefix + kv.Key + "=" + kv.Value))
                .Concat(string.IsNullOrEmpty(skin) ? Enumerable.Empty<string>() : new[] { SkinKey + "=" + skin })
                .Concat(string.IsNullOrEmpty(weaponColor) ? Enumerable.Empty<string>() : new[] { WeaponColorKey + "=" + weaponColor })
                .Concat(string.IsNullOrEmpty(exhaust) ? Enumerable.Empty<string>() : new[] { ExhaustKey + "=" + exhaust })
                .Concat(string.IsNullOrEmpty(exhaustColor) ? Enumerable.Empty<string>() : new[] { ExhaustColorKey + "=" + exhaustColor })
                .Concat(string.IsNullOrEmpty(exhaustColor2) ? Enumerable.Empty<string>() : new[] { ExhaustColor2Key + "=" + exhaustColor2 })
                .Concat(string.IsNullOrEmpty(exhaustSize) ? Enumerable.Empty<string>() : new[] { ExhaustSizeKey + "=" + exhaustSize }).Append("tier=" + tier));
        }

        /// <summary>'b' made valid for this kit and ship type: every slot one part the type may fit ('fallback''s part, else the
        /// slot's first), the tier within what the kit has.</summary>
        public static Build Normalize(Kit kit, string type, Build b, Build fallback = null)
        {
            var n = new Build { tier = Mathf.Clamp(b?.tier ?? fallback?.tier ?? 0, 0, kit.MaxTier) };
            foreach (string slot in kit.slots)
            {
                var allowed = kit.PartsFor(slot, type);
                if (allowed.Count == 0) continue;
                Part pick = null;
                if (b != null && b.parts.TryGetValue(slot, out var id)) pick = allowed.Find(p => string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase));
                if (pick == null && fallback != null && fallback.parts.TryGetValue(slot, out var fid)) pick = allowed.Find(p => string.Equals(p.id, fid, StringComparison.OrdinalIgnoreCase));
                n.parts[slot] = (pick ?? allowed[0]).id;
            }
            foreach (var cs in kit.colorSlots)
            {
                string v = null;
                if (b != null && b.colors.TryGetValue(cs.id, out var bv) && Resolve(kit, cs, bv, out _, out _, out _)) v = bv;
                else if (fallback != null && fallback.colors.TryGetValue(cs.id, out var fv) && Resolve(kit, cs, fv, out _, out _, out _)) v = fv;
                else if (Resolve(kit, cs, cs.defaultValue, out _, out _, out _)) v = cs.defaultValue;
                else if (kit.PaletteOf(cs).Count > 0) v = kit.PaletteOf(cs)[0].id;
                if (v != null) n.colors[cs.id] = v;
            }
            // The skin: 'b''s when the type may wear it, else the fallback's, else none (the model's own look).
            var allowedSkins = kit.SkinsFor(type);
            Skin Allowed(string id) => string.IsNullOrEmpty(id) ? null : allowedSkins.Find(k => string.Equals(k.id, id, StringComparison.OrdinalIgnoreCase));
            // "none" (Build.NoSkin) is the model's own look picked over a fallback that has a skin.
            string fb = Allowed(fallback?.skin)?.id;
            string chosen = b?.skin == Build.NoSkin ? null : Allowed(b?.skin)?.id ?? fb;
            n.skin = chosen ?? (fb != null ? Build.NoSkin : null);
            // The weapon colour the same way ("none" = the weapons' own over a fallback that has one).
            string fw = fallback != null && ResolveWeaponColor(kit, fallback.weaponColor, out _) ? fallback.weaponColor : null;
            string wc = b?.weaponColor == Build.NoSkin ? null : b != null && ResolveWeaponColor(kit, b.weaponColor, out _) ? b.weaponColor : fw;
            n.weaponColor = wc ?? (fw != null ? Build.NoSkin : null);
            // The exhaust style the same way, its colours only with it (a palette id it has, or "#RRGGBB").
            string fe = ModExhausts.Find(fallback?.exhaust) != null ? fallback.exhaust : null;
            string ex = b?.exhaust == Build.NoSkin ? null : ModExhausts.Find(b?.exhaust) != null ? b.exhaust : fe;
            n.exhaust = ex ?? (fe != null ? Build.NoSkin : null);
            var style = ModExhausts.Find(n.exhaust);
            if (style != null)
            {
                var src = ex == b?.exhaust ? b : fallback;
                n.exhaustColor = ModExhausts.ResolveColor(style, src?.exhaustColor, out _) ? src.exhaustColor : null;
                n.exhaustColor2 = ModExhausts.ResolveColor(style, src?.exhaustColor2, out _) ? src.exhaustColor2 : null;
                n.exhaustSize = ModExhausts.SizeText(ModExhausts.ParseSize(src?.exhaustSize));
            }
            return n;
        }

        // ---- the ships -------------------------------------------------------------------------------------------------

        /// <summary>A kit ship's kit (null: not one, or its kit's mod is off).</summary>
        public static Kit KitOf(CustomShipData c) => c == null || string.IsNullOrEmpty(c.kit) ? null : Find(c.kit);   // ModContent made it "mod_id:kit_id"

        /// <summary>The ship number's kit ship data (null: not a kit ship).</summary>
        public static CustomShipData KitShip(int ship) => Database.Shared.Ship(ship) is CustomShipData c && !string.IsNullOrEmpty(c.kit) ? c : null;

        /// <summary>A kit ship's default build (the dealers', the NPCs').</summary>
        public static Build DefaultBuild(CustomShipData c, Kit kit) => Normalize(kit, c.kitType, Build.Parse(c.defaultBuild));

        /// <summary>The player's build of a kit ship (Session.ShipBuilds by its key; else the default), valid for the kit.</summary>
        public static Build PlayerBuild(int ship)
        {
            var kit = CustomizationOf(ship);
            if (kit == null) return null;
            string key = BuildKey(ship);
            var saved = key != null && Session.ShipBuilds.TryGetValue(key, out var text) ? Build.Parse(text) : null;
            return Normalize(kit, TypeOf(ship), saved, DefaultBuildOf(ship, kit));
        }

        /// <summary>The player's new build of a kit ship (the hangar's Customize screen).</summary>
        public static void SetPlayerBuild(int ship, Build b)
        {
            string key = BuildKey(ship);
            if (key == null || b == null) return;
            Session.ShipBuilds[key] = b.ToString();
        }

        public const char VariantSeparator = KitRules.VariantSeparator;

        /// <summary>The assembly that shows 'build' of a kit ship ("ship_NNN_mod#<build>", ModShips.Template assembles it).</summary>
        public static AssemblyData VariantAssembly(int ship, Build build)
        {
            // The weapon colour isn't part of the model (Flight.WeaponTint colours the shots): no variant of its own.
            // Nor the exhaust style (ModExhausts.Apply puts it on the instance).
            if (build != null && (!string.IsNullOrEmpty(build.weaponColor) || !string.IsNullOrEmpty(build.exhaust) || !string.IsNullOrEmpty(build.exhaustColor)
                                  || !string.IsNullOrEmpty(build.exhaustColor2) || !string.IsNullOrEmpty(build.exhaustSize)))
            {
                build = build.Clone();
                build.weaponColor = build.exhaust = build.exhaustColor = build.exhaustColor2 = build.exhaustSize = null;
            }
            if (KitShip(ship) == null)
            {
                // Another ship with customizations: its own model, or with a skin a skinned copy of it
                // ("<model>#ship=N;<build>", ModShips.SkinnedTemplate through AssembledObject.LoadPrefab).
                var own = Database.Shared.ShipAssembly(ship);
                var skin = CustomizationOf(ship)?.FindSkin(build?.skin);
                if (own == null || skin == null) return own;
                return new AssemblyData { name = $"{own.name}{VariantSeparator}{ShipToken}{ship};skin={skin.id}", pack = own.pack, category = own.category,
                                          origin = "skin", root = own.root };
            }
            return new AssemblyData { name = $"ship_{ship:000}_mod{VariantSeparator}{build}", pack = ModContent.Pack, category = "ships", origin = "kit build" };
        }

        public const string ShipToken = "ship=";

        /// <summary>A skinned variant's parts ("<model>#ship=N;skin=id"): the model's own assembly name, the ship and the
        /// skin (false: not one).</summary>
        public static bool ParseSkinned(string assembly, out string model, out int ship, out string skin)
        {
            model = null; ship = -1; skin = null;
            int hash = assembly?.IndexOf(VariantSeparator) ?? -1;
            if (hash <= 0 || string.Compare(assembly, hash + 1, ShipToken, 0, ShipToken.Length, StringComparison.Ordinal) != 0) return false;
            model = assembly.Substring(0, hash);
            foreach (var pair in assembly.Substring(hash + 1).Split(';'))
            {
                if (pair.StartsWith(ShipToken)) int.TryParse(pair.Substring(ShipToken.Length), out ship);
                else if (pair.StartsWith("skin=")) skin = pair.Substring(5);
            }
            return ship >= 0 && !string.IsNullOrEmpty(skin);
        }

        // ---- customizations of ships without a kit ---------------------------------------------------------------------

        static readonly Dictionary<int, Kit> shipCustom = new Dictionary<int, Kit>();
        static int customRevision = -1;

        /// <summary>The key a ship's build is saved under: a mod ship's key, else "gof2:<number>" (the game's own ships).</summary>
        public static string BuildKey(int ship) => ship < 0 ? null : ModContent.ShipKey(ship) ?? "gof2:" + ship;

        /// <summary>The ship type its parts and skins are matched against: a kit ship's kitType, else "" (its own kit).</summary>
        public static string TypeOf(int ship) => KitShip(ship)?.kitType ?? "";

        /// <summary>A ship's default build: a kit ship's, else none (the model's own look).</summary>
        public static Build DefaultBuildOf(int ship, Kit kit)
        {
            var c = KitShip(ship);
            return c != null ? DefaultBuild(c, kit) : Normalize(kit, "", null);
        }

        /// <summary>What the Customize screen offers for a ship: a kit ship's kit, else the customizations the mods give it (a
        /// kit with no parts: their skins and weapon colours), null = none.</summary>
        public static Kit CustomizationOf(int ship)
        {
            var c = KitShip(ship);
            if (c != null) return KitOf(c);
            if (customRevision != ModManager.Revision) ReadShipCustomizations();
            if (shipCustom.TryGetValue(ship, out var k)) return k;
            // The mods' exhaust styles, on any ship: a kit with nothing else (made once per ship and set of mods).
            if (ship < 0 || !ModExhausts.Any) return null;
            if (!exhaustOnly.TryGetValue(ship, out k))
                exhaustOnly[ship] = k = new Kit { mod = ModExhausts.All()[0].mod, localId = "ship_" + ship, weaponColorRow = false,
                                                  name = new Dictionary<string, string> { ["en"] = "ship " + ship } };
            return k;
        }

        static readonly Dictionary<int, Kit> exhaustOnly = new Dictionary<int, Kit>();

        static void ReadShipCustomizations()
        {
            customRevision = ModManager.Revision;
            shipCustom.Clear();
            exhaustOnly.Clear();
            const string ShipsFile = "ships.json";
            foreach (var mod in ModManager.Active)
            {
                try
                {
                    if (!(ModJson.Read(mod.Source, ShipsFile) is JArray list)) continue;
                    foreach (var t in list)
                    {
                        if (!(t is JObject o) || ModJson.Has(o, "kit")) continue;   // a kit ship's skins are its kit's
                        var wc = ModJson.Get(o, "weaponColors");
                        bool skins = ModJson.Get(o, "skins") != null, colors = wc != null && !(wc.Type == JTokenType.Boolean && !(bool)wc);
                        if (!skins && !colors) continue;
                        string where = ModJson.Where(t, ShipsFile);
                        int ship = -1;
                        var ov = ModJson.Get(o, "override");
                        if (ov != null) ship = ov.Type == JTokenType.Integer ? (int)ov : ModContent.ShipIndexOf((string)ov);
                        else if (ModJson.Str(o, "id") is string id) ship = ModContent.ShipIndexOf(mod.Id + ":" + id);
                        if (ship < 0) { Warn(mod, $"{where}: \"skins\" / \"weaponColors\" for a ship that isn't in the game (ignored)"); continue; }
                        if (!shipCustom.TryGetValue(ship, out var kit))
                            shipCustom[ship] = kit = new Kit { mod = mod, localId = "ship_" + ship, name = new Dictionary<string, string> { ["en"] = "ship " + ship } };
                        if (skins)
                        {
                            var own = new Kit { mod = mod, localId = kit.localId };
                            ReadSkins(o, own, mod, where, ShipsFile);
                            foreach (var sk in own.skins)
                            {
                                sk.types.Clear();   // the ship's own
                                if (kit.FindSkin(sk.id) != null) { Warn(mod, $"{where}: the skin \"{sk.id}\" is already given to this ship by another mod (ignored)"); continue; }
                                kit.skins.Add(sk);
                            }
                        }
                        if (ModJson.Get(o, "weaponColors") is JArray pa)
                        {
                            if (!kit.palettes.TryGetValue("weapons", out var pl)) kit.palettes["weapons"] = pl = new List<ColorChoice>();
                            foreach (var choice in ReadPalette(pa, ShipsFile))
                                if (!pl.Exists(x => string.Equals(x.id, choice.id, StringComparison.OrdinalIgnoreCase))) pl.Add(choice);
                        }
                    }
                }
                catch (ModJsonException e) { Warn(mod, e.Message); }
            }
        }
    }
}
