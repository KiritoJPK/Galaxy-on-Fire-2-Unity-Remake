// ModContent.cs
// What the active mods add to and change in the game's tables, merged by Database.Load (Apply).
//
// items.json (a list; see Modding/README.md): each entry either adds an item or changes one.
//   { "id": "plasma_lance", "base": 12, "name": "Plasma Lance", "description": "...", "minPrice": 20000, ...,
//     "stats": { "damage": 14, "reloadMs": 450 } }
//       a new item "<mod id>:plasma_lance", a copy of its base (an original item's number, or "mod:id" of a modded one)
//       with the fields given changed. It keeps the base's type and category, and looks, sounds and works like it
//       (icon, projectile, shot sound, special behaviour), only with its own numbers and texts.
//   { "override": 12, "maxPrice": 9000, "stats": { "damage": 10 } }
//       changes an original item (or "mod:id", one of an earlier mod's): rebalancing.
// Fields: name, description (a string or { "en": ..., "de": ... }), techLevel, occurrence, minPrice, maxPrice (or
// "price": n / [min, max]), lowestPriceSystem, highestPriceSystem, vosskOnly, alwaysSoldAt, stats { name: value },
// attributes { "id": value } (the original's attribute numbers), defaultEconomy { the same fields, for the Default
// Economy }. Texts: text/<language>.json { "items.<id>.name": "...", "items.<id>.description": "..." } translates them.
//
// Numbers: single player keeps every key's number in ModRegistry (a mod turned off leaves a placeholder at its number,
// so saves stay valid); a multiplayer session numbers its mods afresh in the session's order.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModContent
    {
        /// <summary>The asset pack of the mods' models (AssemblyData.pack; AssembledObject.LoadPrefab hands out the built
        /// templates for it: ModShips, ModStations).</summary>
        public const string Pack = "mod";
        /// <summary>A mod's folder of replacement textures (ModTextures).</summary>
        public const string TexturesFolder = "textures";

        public const string ItemsFile = "items.json";

        /// <summary>One entry of a mod's items.json.</summary>
        public class ItemDef
        {
            public ModInfo mod;
            public JObject json;
            public string where;            // "items.json line N", for messages
            public string localId, key;     // a new item
            public string overrideRef;      // a changed item: "12" or "mod:id"
            public string baseRef;
            public Dictionary<string, string> name, description;
        }

        /// <summary>One entry of a mod's ships.json: a new ship (the PR #34 custom ship format, CustomShipData) or changes to
        /// an existing one ("override": armor, cargo, price, priceDefault, slots, handling, race, hangarHeight).</summary>
        public class ShipDef
        {
            public ModInfo mod;
            public JObject json;
            public string where, localId, key, overrideRef, data;   // data: the entry as CustomShipData JSON (no texts / ids)
            public Dictionary<string, string> name, description;
            public bool hasLounge, hasDealer;
            public object sounds;   // "sounds" (ModShipSounds.ShipSounds, GameHooks.ParseShipSounds), null = none
        }

        /// <summary>The parsed content of one mod (read when the mods are scanned, so a broken file shows in the browser).</summary>
        public class Parsed
        {
            public readonly List<ItemDef> items = new List<ItemDef>();
            public readonly List<ShipDef> ships = new List<ShipDef>();
            public readonly List<ModBlueprints.Def> blueprints = new List<ModBlueprints.Def>();
            public readonly List<ModWorld.Def> systems = new List<ModWorld.Def>(), stations = new List<ModWorld.Def>();
            public readonly Dictionary<string, Dictionary<string, string>> text =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);   // language -> key -> text
        }

        static readonly Dictionary<ModInfo, Parsed> parsed = new Dictionary<ModInfo, Parsed>();

        // The numbering for ModManager.Revision.
        static int mappedRevision = -1;
        static int originalItems = -1;
        static readonly Dictionary<string, int> itemIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        static readonly Dictionary<int, ItemDef> itemDefAt = new Dictionary<int, ItemDef>();
        static readonly Dictionary<int, string> placeholderItems = new Dictionary<int, string>();
        static readonly Dictionary<int, int> itemLook = new Dictionary<int, int>();
        static readonly Dictionary<int, ItemDef> itemRenames = new Dictionary<int, ItemDef>();   // overrides with a name / description
        static readonly Dictionary<int, ModBlueprints.Def> deedAt = new Dictionary<int, ModBlueprints.Def>();   // ship blueprints' items
        static int originalShips = -1;
        static readonly Dictionary<string, int> shipIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        static readonly Dictionary<int, ShipDef> shipDefAt = new Dictionary<int, ShipDef>();
        static readonly Dictionary<int, string> placeholderShips = new Dictionary<int, string>();
        static readonly Dictionary<int, ShipDef> shipRenames = new Dictionary<int, ShipDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            parsed.Clear();
            mappedRevision = -1;
            originalItems = -1;
            originalShips = -1;
        }

        public static void ClearParsed() { parsed.Clear(); mappedRevision = -1; }

        // ---- parsing -------------------------------------------------------------------------------------------------

        /// <summary>Reads a mod's content files; errors go to the mod (and make it unusable).</summary>
        public static Parsed Parse(ModInfo mod)
        {
            if (parsed.TryGetValue(mod, out var p)) return p;
            p = new Parsed();
            parsed[mod] = p;
            if (mod.Manifest == null) return p;
            try
            {
                ParseItems(mod, p);
                ParseShips(mod, p);
                ModBlueprints.Parse(mod, p.blueprints);
                ModWorld.Parse(mod, p.systems, p.stations);
                foreach (var f in mod.Source.FilesIn("text", ".json"))
                {
                    string lang = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (!(ModJson.Read(mod.Source, f) is JObject o)) throw new ModJsonException($"{f}: must be an object {{ \"key\": \"text\" }}");
                    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in o.Properties()) if (prop.Value.Type == JTokenType.String) d[prop.Name] = (string)prop.Value;
                    p.text[lang] = d;
                }
            }
            catch (ModJsonException e) { mod.Errors.Add(e.Message); }
            return p;
        }

        static readonly HashSet<string> ItemFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "override", "base", "name", "description", "techLevel", "occurrence", "minPrice", "maxPrice", "price",
            "lowestPriceSystem", "highestPriceSystem", "vosskOnly", "alwaysSoldAt", "stats", "attributes", "defaultEconomy", "fx", "icon",
            "available",
        };

        static void ParseItems(ModInfo mod, Parsed p)
        {
            var token = ModJson.Read(mod.Source, ItemsFile);
            if (token == null) return;
            if (!(token is JArray list)) throw new ModJsonException($"{ItemsFile}: must be a list [ {{ ... }}, ... ]");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in list)
            {
                string where = ModJson.Where(t, ItemsFile);
                if (!(t is JObject o)) throw new ModJsonException($"{where}: each item must be an object {{ ... }}");
                var d = new ItemDef
                {
                    mod = mod, json = o, where = where,
                    localId = ModJson.Str(o, "id"), overrideRef = ModJson.Str(o, "override"), baseRef = ModJson.Str(o, "base"),
                    name = ModJson.Text(o, "name"), description = ModJson.Text(o, "description"),
                };
                if ((d.localId == null) == (d.overrideRef == null))
                    throw new ModJsonException($"{where}: an item needs either \"id\" (a new item) or \"override\" (change an existing one)");
                if (d.localId != null)
                {
                    if (!ModManifest.ValidId(d.localId)) throw new ModJsonException($"{where}: the id \"{d.localId}\" may only use a-z, 0-9, _ and -");
                    if (!ids.Add(d.localId)) throw new ModJsonException($"{where}: the id \"{d.localId}\" is used twice");
                    if (d.baseRef == null) throw new ModJsonException($"{where}: a new item needs \"base\", the item it is made from (e.g. 12)");
                    d.key = mod.Id + ":" + d.localId;
                }
                foreach (var prop in o.Properties())
                    if (!ItemFields.Contains(prop.Name)) mod.Warnings.Add($"{ModJson.Where(prop, ItemsFile)}: unknown field \"{prop.Name}\" (ignored)");
                CheckFields(o, where, mod);
                if (ModJson.Get(o, "defaultEconomy") is JObject de) CheckFields(de, where + " (defaultEconomy)", mod);
                if (ModJson.Has(o, "icon"))
                {
                    string icon = ModJson.Str(o, "icon");
                    if (GameModels.Is(icon)) { if (GameModels.Icon(icon) == null) mod.Warnings.Add($"{where}: the game has no icon \"{icon}\" (the base item's is used)"); }
                    else if (string.IsNullOrEmpty(icon) || !mod.Source.Exists(icon))
                        mod.Warnings.Add($"{where}: the icon \"{icon}\" isn't in the mod (the base item's is used)");
                }
                p.items.Add(d);
            }
        }

        public const string ShipsFile = "ships.json";

        static readonly HashSet<string> ShipFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "override", "name", "description", "race", "armor", "cargo", "price", "priceDefault", "slots", "handling",
            "hangarHeight", "mounts", "model", "icon", "modelLength", "modelYaw", "modelPitch", "modelRoll", "modelOffset", "engineGlowRadius", "engineGlowColor", "materials", "throttleGlow",
            "extraGlows", "lounge", "dealer", "traffic", "available", "npcTurrets", "flightStyle", "flight", "kit", "kitType", "build",
            "skins", "weaponColors",   // customizations of a ship without a kit (ModShipKits.CustomizationOf)
            "engineFx",   // Galaxy on Fire 3's engine effect (EngineFx)
            "sounds",     // the ship's own engine and boost sounds (ModShipSounds)
            "integrated", // a built-in Khador Drive / cloak (ApplyIntegrated)
        };

        static void ParseShips(ModInfo mod, Parsed p)
        {
            var token = ModJson.Read(mod.Source, ShipsFile);
            if (token == null) return;
            if (!(token is JArray list)) throw new ModJsonException($"{ShipsFile}: must be a list [ {{ ... }}, ... ]");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in list)
            {
                string where = ModJson.Where(t, ShipsFile);
                if (!(t is JObject o)) throw new ModJsonException($"{where}: each ship must be an object {{ ... }}");
                var d = new ShipDef
                {
                    mod = mod, json = o, where = where, localId = ModJson.Str(o, "id"), overrideRef = ModJson.Str(o, "override"),
                    name = ModJson.Text(o, "name"), description = ModJson.Text(o, "description"), hasLounge = ModJson.Has(o, "lounge"),
                    hasDealer = ModJson.Has(o, "dealer"),
                };
                if ((d.localId == null) == (d.overrideRef == null))
                    throw new ModJsonException($"{where}: a ship needs either \"id\" (a new ship) or \"override\" (change an existing one)");
                if (ModJson.Get(o, "integrated") is JToken ig && !(ig is JObject))
                    throw new ModJsonException($"{where}: \"integrated\" must be an object {{ \"khadorDrive\": true, \"cloak\": 95 }}");
                if (ModJson.Get(o, "npcTurrets") is JToken nt && !(nt is JArray))
                    throw new ModJsonException($"{where}: \"npcTurrets\" must be a list of turret items [ 48, \"mod:id\" ]");
                foreach (var prop in o.Properties())
                    if (!ShipFields.Contains(prop.Name)) mod.Warnings.Add($"{ModJson.Where(prop, ShipsFile)}: unknown field \"{prop.Name}\" (ignored)");
                foreach (var f in new[] { "race", "armor", "cargo", "price", "priceDefault", "handling", "hangarHeight" }) ModJson.Int(o, f, 0, ShipsFile);
                ModFlight.CheckShip(o, where, mod, ShipsFile);   // "flightStyle", "flight": per flight style
                d.sounds = GameHooks.ParseShipSounds?.Invoke(o, mod, ShipsFile);
                if (d.localId != null)
                {
                    if (!ModManifest.ValidId(d.localId)) throw new ModJsonException($"{where}: the id \"{d.localId}\" may only use a-z, 0-9, _ and -");
                    if (!ids.Add(d.localId)) throw new ModJsonException($"{where}: the id \"{d.localId}\" is used twice");
                    string model = ModJson.Str(o, "model"), kit = ModJson.Str(o, "kit");
                    if (!string.IsNullOrEmpty(kit))
                    {
                        // A customizable ship (ModShipKits): its parts come from the kit, its default build from "build".
                        if (string.IsNullOrEmpty(ModJson.Str(o, "kitType"))) throw new ModJsonException($"{where}: a kit ship needs \"kitType\" (which parts it may fit)");
                        if (ModJson.Get(o, "build") is JToken bt && !(bt is JObject)) throw new ModJsonException($"{where}: \"build\" must be an object {{ \"wings\": \"part_id\", \"tier\": 1 }}");
                    }
                    else if (string.IsNullOrEmpty(model)) throw new ModJsonException($"{where}: a new ship needs \"model\", its glTF / GLB file or \"gof2:<model>\" (one of the game's), or \"kit\" (a customizable ship)");
                    else if (!GameModels.Is(model) && !mod.Source.Exists(model)) throw new ModJsonException($"{where}: the model \"{model}\" isn't in the mod");
                    d.key = mod.Id + ":" + d.localId;
                }
                else if (ModJson.Has(o, "model"))
                {
                    // An override with a model replaces an original ship's model (ModelOverrides).
                    string model = ModJson.Str(o, "model");
                    if (string.IsNullOrEmpty(model) || !GameModels.Is(model) && !mod.Source.Exists(model)) throw new ModJsonException($"{where}: the model \"{model}\" isn't in the mod");
                }
                // The rest as CustomShipData (JsonUtility reads it; the texts and ids are the mod's own; npcTurrets: ApplyShips).
                var data = (JObject)o.DeepClone();
                foreach (var k in new[] { "id", "override", "name", "description", "npcTurrets", "build", "sounds", "integrated" }) data.Remove(k);
                try { JsonUtility.FromJson<CustomShipData>(data.ToString()); }
                catch (Exception e) { throw new ModJsonException($"{where}: {e.Message}"); }
                d.data = data.ToString();
                p.ships.Add(d);
            }
        }

        static void CheckFields(JObject o, string where, ModInfo mod)
        {
            foreach (var f in new[] { "techLevel", "occurrence", "minPrice", "maxPrice", "lowestPriceSystem", "highestPriceSystem", "alwaysSoldAt" })
                ModJson.Int(o, f, 0, ItemsFile);
            if (ModJson.Get(o, "stats") is JToken st)
            {
                if (!(st is JObject so)) throw new ModJsonException($"{where}: \"stats\" must be an object {{ \"damage\": 12 }}");
                foreach (var prop in so.Properties())
                {
                    if (ItemStats.IdOf(prop.Name) < 0)
                        throw new ModJsonException($"{ModJson.Where(prop, ItemsFile)}: unknown stat \"{prop.Name}\" (see Modding/README.md, or use \"attributes\")");
                    ModJson.Int(so, prop.Name, 0, ItemsFile);
                }
            }
            if (ModJson.Get(o, "attributes") is JToken at)
            {
                if (!(at is JObject ao)) throw new ModJsonException($"{where}: \"attributes\" must be an object {{ \"9\": 12 }}");
                foreach (var prop in ao.Properties())
                {
                    if (!int.TryParse(prop.Name, out int id) || id < 0 || id > 99)
                        throw new ModJsonException($"{ModJson.Where(prop, ItemsFile)}: attribute \"{prop.Name}\" must be a number 0-99");
                    if (id <= 8) mod.Warnings.Add($"{ModJson.Where(prop, ItemsFile)}: attributes 0-8 follow the item's fields (index, type, category, prices); set those instead");
                    ModJson.Int(ao, prop.Name, 0, ItemsFile);
                }
            }
        }

        // ---- numbering -----------------------------------------------------------------------------------------------

        /// <summary>The numbers of the active mods' content (again after ModManager.Revision changes).</summary>
        static void EnsureMapping()
        {
            if (mappedRevision == ModManager.Revision && originalItems >= 0 && originalShips >= 0) return;
            if (originalItems < 0 || originalShips < 0) { Database.Load(); if (originalItems < 0 || originalShips < 0) return; }   // Load sets them
            mappedRevision = ModManager.Revision;
            itemIndex.Clear(); itemDefAt.Clear(); placeholderItems.Clear(); itemLook.Clear(); itemRenames.Clear(); deedAt.Clear();
            bool session = ModManager.InSession;
            int next = originalItems;
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).items)
                {
                    if (d.key == null) continue;
                    int i = session ? next++ : ModRegistry.Assign(ModRegistry.Kind.Item, d.key, originalItems);
                    itemIndex[d.key] = i;
                    itemDefAt[i] = d;
                }
            // A ship blueprint's item (ModBlueprints): numbered like an item, so saves and crates keep it.
            foreach (var mod in ModManager.Active)
                foreach (var b in Parse(mod).blueprints)
                {
                    if (!b.IsShip) continue;
                    int i = session ? next++ : ModRegistry.Assign(ModRegistry.Kind.Item, b.DeedKey, originalItems);
                    itemIndex[b.DeedKey] = i;
                    deedAt[i] = b;
                }
            if (!session)
                foreach (var kv in ModRegistry.All(ModRegistry.Kind.Item))
                    if (!itemIndex.ContainsKey(kv.Key) && kv.Value >= originalItems) placeholderItems[kv.Value] = kv.Key;
            // Looks: a new item looks like its base (recursively: a copy of a copy looks like the original).
            foreach (var kv in itemDefAt) itemLook[kv.Key] = LookOf(kv.Key, 0);
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).items)
                    if (d.overrideRef != null && (d.name != null || d.description != null) && TryResolveItem(d.overrideRef, out int t)) itemRenames[t] = d;
            // Shop icons ("icon"): a new item's own, or an override's for the item it changes (the later mod wins).
            itemIcons.Clear();
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).items)
                {
                    string icon = ModJson.Str(d.json, "icon");
                    if (string.IsNullOrEmpty(icon) || !GameModels.Is(icon) && !mod.Source.Exists(icon)) continue;
                    if (d.key != null ? itemIndex.TryGetValue(d.key, out int i) : TryResolveItem(d.overrideRef, out i)) itemIcons[i] = d;
                }
            // A new item without an icon of its own takes the nearest base's that has one (a copy of a mod item, or of an
            // original item an override gave an icon); without any, the original look's (ItemLook).
            foreach (var kv in itemDefAt)
            {
                if (itemIcons.ContainsKey(kv.Key)) continue;
                int at = kv.Key;
                for (int depth = 0; depth < 16 && itemDefAt.TryGetValue(at, out var def) && TryResolveItem(def.baseRef, out int b) && b != at; depth++)
                {
                    at = b;
                    if (itemIcons.TryGetValue(at, out var src)) { inheritedIcons[kv.Key] = src; break; }
                }
            }
            foreach (var kv in inheritedIcons) itemIcons[kv.Key] = kv.Value;
            inheritedIcons.Clear();

            // Ships: the same, after the original 64.
            shipIndex.Clear(); shipDefAt.Clear(); placeholderShips.Clear(); shipRenames.Clear();
            int nextShip = originalShips;
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).ships)
                {
                    if (d.key == null) continue;
                    int i = session ? nextShip++ : ModRegistry.Assign(ModRegistry.Kind.Ship, d.key, originalShips);
                    shipIndex[d.key] = i;
                    shipDefAt[i] = d;
                }
            if (!session)
                foreach (var kv in ModRegistry.All(ModRegistry.Kind.Ship))
                    if (!shipIndex.ContainsKey(kv.Key) && kv.Value >= originalShips) placeholderShips[kv.Value] = kv.Key;
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).ships)
                    if (d.overrideRef != null && (d.name != null || d.description != null) && TryResolveShip(d.overrideRef, out int t)) shipRenames[t] = d;
        }

        /// <summary>A ship reference ("12" or "mod:id") to its number in the current numbering.</summary>
        public static bool TryResolveShip(string reference, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(reference)) return false;
            if (int.TryParse(reference, out index)) return index >= 0 && (index < originalShips || shipDefAt.ContainsKey(index));
            return shipIndex.TryGetValue(reference, out index);
        }

        /// <summary>A mod ship's entry as CustomShipData (a fresh copy: Database.Load sets prices by economy), null = none.</summary>
        static CustomShipData ShipData(int index, ShipDef d)
        {
            var c = JsonUtility.FromJson<CustomShipData>(d.data);
            c.index = index;
            c.assembly = $"ship_{index:000}_mod";
            if (GameModels.Is(c.model, out string gameModel))
            {
                // One of the game's own models (no file to build): the ship's hangar height and icon follow it unless given.
                c.assembly = gameModel;
                c.gameModelShip = GameModels.ShipOf(gameModel);
                if (!ModJson.Has(d.json, "hangarHeight") && c.gameModelShip >= 0) c.hangarHeight = -1;
            }
            if (!d.hasLounge) c.lounge = null;   // JsonUtility makes one with its defaults
            if (!d.hasDealer) c.dealer = null;
            if (c.throttleGlow != null && string.IsNullOrEmpty(c.throttleGlow.mask)) c.throttleGlow = null;
            if (c.engineFx != null && !c.engineFx.Any) c.engineFx = null;   // (JsonUtility makes one either way)
            ShipText(index, false, out c.name);
            ShipText(index, true, out c.description);
            c.handlingMultiplier = c.handling / 100f;
            c.slots ??= new ShipSlots();
            if (!string.IsNullOrEmpty(c.kit))
            {
                // A customizable ship (ModShipKits): the kit as "mod_id:kit_id", the default build as build text.
                if (c.kit.IndexOf(':') < 0) c.kit = d.mod.Id + ":" + c.kit;
                c.defaultBuild = GameHooks.KitBuildText?.Invoke(ModJson.Get(d.json, "build") as JObject) ?? "";
            }
            else c.kit = null;
            return c;
        }

        /// <summary>Original ships whose model a mod replaces (an "override" with "model", e.g. the Groza from Manticore): their
        /// entries as CustomShipData for ModShips to build (assembly "ship_NNN_mod", which Database.ShipAssembly then hands out
        /// instead of the original's); the original's weapon and exhaust mounts unless the entry has its own "mounts". The
        /// later mod in the load order wins. Not new ships: CustomShips doesn't see them (no lounge sellers, the stats stay the
        /// original's plus the override's).</summary>
        public static List<(ModInfo mod, CustomShipData ship)> ModelOverrides()
        {
            EnsureMapping();
            var l = new List<(ModInfo, CustomShipData)>();
            if (originalShips < 0) return l;
            var byShip = new SortedDictionary<int, ShipDef>();
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).ships)
                    if (d.overrideRef != null && ModJson.Has(d.json, "model") && TryResolveShip(d.overrideRef, out int t) && t < originalShips)
                    {
                        if (GameModels.Is(ModJson.Str(d.json, "model"))) byShip.Remove(t);   // the game's model: nothing to build
                        else byShip[t] = d;
                    }
            if (byShip.Count == 0) return l;
            var db = Database.Load();
            foreach (var kv in byShip)
            {
                var c = JsonUtility.FromJson<CustomShipData>(kv.Value.data);
                c.index = kv.Key;
                c.assembly = ModelOverrideAssembly(kv.Key);
                c.name = db.Ship(kv.Key)?.name ?? c.assembly;
                c.lounge = null;
                if (c.throttleGlow != null && string.IsNullOrEmpty(c.throttleGlow.mask)) c.throttleGlow = null;
                if (c.engineFx != null && !c.engineFx.Any) c.engineFx = null;
                if (c.mounts == null || c.mounts.Count == 0)
                    c.mounts = db.WeaponMounts.Find(w => w.ship == kv.Key)?.mounts ?? new List<WeaponMount>();
                l.Add((kv.Value.mod, c));
            }
            return l;
        }

        /// <summary>The ships' own sounds (ships.json "sounds", ModShipSounds) by ship number: new ships' and overrides', the
        /// later mod in the load order winning.</summary>
        public static Dictionary<int, object> ShipSounds()
        {
            EnsureMapping();
            var r = new Dictionary<int, object>();
            foreach (var kv in shipDefAt) if (kv.Value.sounds != null) r[kv.Key] = kv.Value.sounds;
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).ships)
                    if (d.overrideRef != null && d.sounds != null && TryResolveShip(d.overrideRef, out int t)) r[t] = d.sounds;
            return r;
        }

        /// <summary>The assembly of a mod's model for an original ship (ModelOverrides).</summary>
        public static string ModelOverrideAssembly(int ship) => $"ship_{ship:000}_mod";

        /// <summary>Some active mod brings a ship model (a new ship, or an override's model; ActiveShips / ModelOverrides not
        /// empty), without building their entries: ModShips.Ready asks every frame while the loading screen shows, and
        /// ActiveShips parses every ship's entry again (~200 KB of garbage and 2 ms for a mod of 79 ships, three times a frame).</summary>
        public static bool AnyShipModels()
        {
            EnsureMapping();
            if (shipDefAt.Count > 0) return true;   // a new ship on a game model still has its icon to load (ModShips)
            if (originalShips < 0) return false;
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).ships)
                    if (d.overrideRef != null && ModJson.Has(d.json, "model") && !GameModels.Is(ModJson.Str(d.json, "model"))
                        && TryResolveShip(d.overrideRef, out int t) && t < originalShips)
                        return true;
            return false;
        }

        /// <summary>The active mods' new ships with their numbers (ModShips builds their models, CustomShips serves them).</summary>
        public static List<(ModInfo mod, CustomShipData ship)> ActiveShips()
        {
            EnsureMapping();
            var l = new List<(ModInfo, CustomShipData)>();
            foreach (var kv in shipDefAt.OrderBy(k => k.Key)) l.Add((kv.Value.mod, ShipData(kv.Key, kv.Value)));
            return l;
        }

        public static string ShipKey(int index)
        {
            EnsureMapping();
            return shipDefAt.TryGetValue(index, out var d) ? d.key : placeholderShips.TryGetValue(index, out var k) ? k : null;
        }

        public static bool IsMissingShip(int index) { EnsureMapping(); return placeholderShips.ContainsKey(index); }

        public static bool IsModShip(int index) { EnsureMapping(); return shipDefAt.ContainsKey(index); }

        public static int ShipIndexOf(string key) { EnsureMapping(); return shipIndex.TryGetValue(key, out int i) ? i : -1; }

        public static IEnumerable<KeyValuePair<int, string>> ActiveShipKeys()
        {
            EnsureMapping();
            foreach (var kv in shipDefAt) yield return new KeyValuePair<int, string>(kv.Key, kv.Value.key);
        }

        public static int OriginalShipCount => originalShips;

        /// <summary>A modded (or renamed) ship's name / description in the current language; false = the original text.</summary>
        public static bool ShipText(int index, bool description, out string text)
        {
            EnsureMapping();
            text = null;
            if (placeholderShips.TryGetValue(index, out var missing))
            {
                text = description ? string.Format(Localization.Extra("modMissingShipDesc", "This ship came from the mod \"{0}\", which isn't on."), missing.Split(':')[0])
                    : string.Format(Localization.Extra("modMissingShip", "Missing ship ({0})"), missing);
                return true;
            }
            ShipDef d = shipDefAt.TryGetValue(index, out var nd) ? nd : shipRenames.TryGetValue(index, out var rd) ? rd : null;
            if (d == null) return false;
            string id = d.localId ?? d.overrideRef;
            text = Translated(d.mod, $"ships.{id}.{(description ? "description" : "name")}", description ? d.description : d.name);
            if (text == null && d.key == null) return false;
            text ??= description ? "" : d.key;
            return true;
        }

        static int LookOf(int index, int depth)
        {
            if (!itemDefAt.TryGetValue(index, out var d) || depth > 16) return index;
            return TryResolveItem(d.baseRef, out int b) && b != index ? LookOf(b, depth + 1) : index;
        }

        /// <summary>An item reference ("12" or "mod:id") to its number in the current numbering.</summary>
        public static bool TryResolveItem(string reference, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(reference)) return false;
            // The table's size and the mod numbers first (ModWeapons reads the overrides' fx on the loading screen, before
            // anything else loaded the database: originalItems was still -1 and every override's fx was dropped).
            EnsureMapping();
            if (int.TryParse(reference, out index)) return index >= 0 && (index < originalItems || itemDefAt.ContainsKey(index));
            return itemIndex.TryGetValue(reference, out index);
        }

        /// <summary>The original item a modded one looks and behaves like (icon, weapon fx, sounds, special rules); an
        /// original item is its own.</summary>
        public static int ItemLook(int index)
        {
            EnsureMapping();
            return itemLook.TryGetValue(index, out int l) ? l : index;
        }

        static readonly Dictionary<int, ItemDef> itemIcons = new Dictionary<int, ItemDef>(), inheritedIcons = new Dictionary<int, ItemDef>();

        /// <summary>An item's shop icon from a mod (items.json "icon": a PNG like the originals' 180 x 88, frame included,
        /// kept uncompressed), null = none (the item's own, or its base's: ItemLook).</summary>
        public static Texture2D ItemIcon(int index)
        {
            EnsureMapping();
            if (!itemIcons.TryGetValue(index, out var d)) return null;
            string icon = ModJson.Str(d.json, "icon");
            if (GameModels.Is(icon)) return GameModels.Icon(icon);   // one of the game's own icons ("gof2:item_046")
            var t = ModMaterials.Texture(d.mod, icon, false, true);
            if (t != null) t.wrapMode = TextureWrapMode.Clamp;   // a UI image: no bleeding in from the opposite edge
            return t;
        }

        /// <summary>The "mod:id" key of a modded item (also a placeholder's), null for an original one.</summary>
        public static string ItemKey(int index)
        {
            EnsureMapping();
            return itemDefAt.TryGetValue(index, out var d) ? d.key : placeholderItems.TryGetValue(index, out var k) ? k : null;
        }

        /// <summary>A modded item whose mod isn't on (a placeholder: never sold, worth nothing).</summary>
        public static bool IsMissingItem(int index) { EnsureMapping(); return placeholderItems.ContainsKey(index); }

        /// <summary>The number the key has now, -1 = not active.</summary>
        public static int ItemIndexOf(string key) { EnsureMapping(); return itemIndex.TryGetValue(key, out int i) ? i : -1; }

        /// <summary>Every modded item number in use now (active ones), with its key.</summary>
        public static IEnumerable<KeyValuePair<int, string>> ActiveItemKeys()
        {
            EnsureMapping();
            foreach (var kv in itemDefAt) yield return new KeyValuePair<int, string>(kv.Key, kv.Value.key);
        }

        // ---- texts ---------------------------------------------------------------------------------------------------

        /// <summary>A modded (or renamed) item's name / description in the current language; false = the original text.</summary>
        public static bool ItemText(int index, bool description, out string text)
        {
            EnsureMapping();
            text = null;
            if (deedAt.TryGetValue(index, out var bp))
            {
                // A ship blueprint's item reads as its ship.
                text = TryResolveShip(bp.shipRef, out int ship) ? description ? GameNames.ShipDescription(ship) : GameNames.Ship(ship) : bp.DeedKey;
                return true;
            }
            if (placeholderItems.TryGetValue(index, out var missing))
            {
                text = description ? string.Format(Localization.Extra("modMissingItemDesc", "This item came from the mod \"{0}\", which isn't on."), missing.Split(':')[0])
                    : string.Format(Localization.Extra("modMissingItem", "Missing item ({0})"), missing);
                return true;
            }
            ItemDef d = itemDefAt.TryGetValue(index, out var nd) ? nd : itemRenames.TryGetValue(index, out var rd) ? rd : null;
            if (d == null) return false;
            var t = description ? d.description : d.name;
            string id = d.localId ?? d.overrideRef;
            text = Translated(d.mod, $"items.{id}.{(description ? "description" : "name")}", t);
            if (text == null && d.key == null) return false;   // an override that doesn't change this text
            text ??= description ? "" : d.key;
            return true;
        }

        /// <summary>The mod's text file for the language, else the entry's own text in the language / English / plain.</summary>
        static string Translated(ModInfo mod, string key, Dictionary<string, string> inline)
        {
            string lang = Localization.Language;
            var p = Parse(mod);
            if (inline != null && inline.TryGetValue(lang, out var s)) return s;
            if (p.text.TryGetValue(lang, out var table) && table.TryGetValue(key, out s)) return s;
            if (inline != null && (inline.TryGetValue("en", out s) || inline.TryGetValue("", out s))) return s;
            if (p.text.TryGetValue("en", out table) && table.TryGetValue(key, out s)) return s;
            return inline?.Values.FirstOrDefault();
        }

        // ---- merging -------------------------------------------------------------------------------------------------

        /// <summary>Database.Load: the active mods' changes and new entries into the freshly read tables.</summary>
        public static void Apply(Database db)
        {
            NoteOriginalCounts(db);
            if (ModManager.Active.Count == 0 && ModRegistry.All(ModRegistry.Kind.Item).Count == 0 && ModRegistry.All(ModRegistry.Kind.Ship).Count == 0
                && ModRegistry.All(ModRegistry.Kind.System).Count == 0 && ModRegistry.All(ModRegistry.Kind.Station).Count == 0) return;
            EnsureMapping();
            bool dflt = db.Economy == Economy.Default;
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).items)
                {
                    try
                    {
                        if (d.overrideRef != null)
                        {
                            if (!TryResolveItem(d.overrideRef, out int t) || db.Item(t) == null)
                            { Report(mod, $"{d.where}: \"override\": {d.overrideRef} is no item (a modded one must come from an earlier mod)"); continue; }
                            ApplyFields(db.Item(t), d.json, dflt);
                            continue;
                        }
                        int index = itemIndex[d.key];
                        if (!TryResolveItem(d.baseRef, out int b) || db.Item(b) == null || b == index)
                        { Report(mod, $"{d.where}: \"base\": {d.baseRef} is no item (a modded base must be listed earlier or come from an earlier mod)"); continue; }
                        var item = Clone(db.Item(b));
                        item.index = index;
                        item.modKey = d.key;
                        item.modded = true;
                        item.lookIndex = ItemLook(index);
                        SetAttr(item, 0, index);
                        if (ItemText(index, false, out var n)) item.name = n;
                        if (ItemText(index, true, out var ds)) item.description = ds;
                        ApplyFields(item, d.json, dflt);
                        db.Items.Add(item);
                    }
                    catch (ModJsonException e) { Report(mod, e.Message); }
                }
            foreach (var kv in deedAt)
                if (db.Item(kv.Key) == null) db.Items.Add(Deed(kv.Key, kv.Value));
            foreach (var kv in placeholderItems)
                if (db.Item(kv.Key) == null) db.Items.Add(Placeholder(kv.Key, kv.Value));
            // Gaps (a number given out to a key the registry later lost): placeholders too, so the table stays 0..N-1.
            int max = db.Items.Max(i => i.index);
            for (int i = originalItems; i <= max; i++) if (db.Item(i) == null) db.Items.Add(Placeholder(i, "?:" + i));
            db.Items.Sort((a, c) => a.index.CompareTo(c.index));
            ApplyShips(db, dflt);
            ModBlueprints.Apply(db);   // the recipes, and the ship blueprints' prices (after the ships)
            ModWorld.Apply(db);
        }

        /// <summary>A ship on one of the game's ship models (GameModels) takes that ship's hardpoints (weapons, turrets, exhausts)
        /// unless its entry has its own "mounts"; a model that is no ship's (a capital ship's) brings none.</summary>
        static void CopyModelMounts(Database db, ShipDef d, int ship, string shipName, string model)
        {
            if (ModJson.Has(d.json, "mounts")) return;
            int source = GameModels.ShipOf(model);
            var mounts = source >= 0 ? Database.GameMounts(source) : null;   // the model's own, whatever other mods did to that ship
            if (mounts == null || mounts.Count == 0) return;
            db.WeaponMounts.RemoveAll(w => w.ship == ship);
            db.WeaponMounts.Add(new WeaponMountSet { ship = ship, shipName = shipName, mounts = mounts.ConvertAll(Copy) });
        }

        static WeaponMount Copy(WeaponMount m) => new WeaponMount
        {
            slotType = m.slotType, upsideDown = m.upsideDown, position_engine = (int[])m.position_engine?.Clone(),
            turretAngles = (float[])m.turretAngles?.Clone(), glowColor = (float[])m.glowColor?.Clone(),
            glowSize = (float[])m.glowSize?.Clone(), turretUp = (float[])m.turretUp?.Clone(),
        };

        static void ApplyShips(Database db, bool dflt)
        {
            db.NpcTurrets.Clear();
            db.ShipModels.Clear();
            db.IntegratedDrives = new HashSet<int>(Database.DefaultIntegratedDrives);
            db.IntegratedCloaks = new Dictionary<int, int>(Database.DefaultIntegratedCloaks);
            foreach (var mod in ModManager.Active)
                foreach (var d in Parse(mod).ships)
                {
                    try
                    {
                        if (d.overrideRef != null)
                        {
                            if (!TryResolveShip(d.overrideRef, out int t) || db.Ship(t) == null)
                            { Report(mod, $"{d.where}: \"override\": {d.overrideRef} is no ship (a modded one must come from an earlier mod)"); continue; }
                            var s = db.Ship(t);
                            var o = d.json;
                            s.armor = ModJson.Int(o, "armor", s.armor, ShipsFile);
                            s.cargo = ModJson.Int(o, "cargo", s.cargo, ShipsFile);
                            s.price = ModJson.Int(o, dflt && ModJson.Has(o, "priceDefault") ? "priceDefault" : "price", s.price, ShipsFile);
                            if (ModJson.Has(o, "handling")) { s.handling = ModJson.Int(o, "handling", 0, ShipsFile); s.handlingMultiplier = s.handling / 100f; }
                            if (ModJson.Get(o, "slots") is JObject sl)
                                s.slots = new ShipSlots
                                {
                                    primary = ModJson.Int(sl, "primary", s.slots?.primary ?? 0, ShipsFile), secondary = ModJson.Int(sl, "secondary", s.slots?.secondary ?? 0, ShipsFile),
                                    turret = ModJson.Int(sl, "turret", s.slots?.turret ?? 0, ShipsFile), equipment = ModJson.Int(sl, "equipment", s.slots?.equipment ?? 0, ShipsFile),
                                };
                            ModFlight.ApplyShip(s, o, d.where);
                            if (s is CustomShipData cs)
                            {
                                cs.race = ModJson.Int(o, "race", cs.race, ShipsFile);
                                cs.hangarHeight = ModJson.Int(o, "hangarHeight", cs.hangarHeight, ShipsFile);
                            }
                            if (ShipText(t, false, out var n)) s.name = n;
                            if (GameModels.Is(ModJson.Str(o, "model"), out string gameModel))
                            {
                                // One of the game's own models for this ship (Database.ShipModels; no file to build): its
                                // hardpoints come with it unless the entry has its own.
                                if (GameModels.Find(db, gameModel) == null) { Report(mod, $"{d.where}: \"model\": the game has no model \"{gameModel}\""); }
                                else
                                {
                                    db.ShipModels[t] = gameModel;
                                    CopyModelMounts(db, d, t, s.name, gameModel);
                                }
                            }
                            else if (ModJson.Has(o, "model") && t < originalShips)
                            {
                                // A new model for an original ship (ModelOverrides; Database.ShipAssembly takes this first).
                                string asm = ModelOverrideAssembly(t);
                                if (db.AssemblyByName(asm) == null)
                                    db.Assemblies.Add(new AssemblyData { name = asm, pack = ModContent.Pack, category = "ships", origin = "mod " + mod.Id });
                            }
                            // Its own hardpoints ("mounts": weapon / turret / exhaust points), with a new model or on the
                            // ship's own (more primaries or secondaries need points to fire from; extra ones share them).
                            if (ModJson.Has(o, "mounts"))
                            {
                                var own = JsonUtility.FromJson<CustomShipData>(d.data);
                                if (own.mounts != null && own.mounts.Count > 0)
                                {
                                    db.WeaponMounts.RemoveAll(w => w.ship == t);
                                    db.WeaponMounts.Add(new WeaponMountSet { ship = t, shipName = s.name, mounts = own.mounts });
                                }
                            }
                            ApplyNpcTurrets(db, mod, d, t);
                            ApplyIntegrated(db, mod, d, t);
                            continue;
                        }
                        int index = shipIndex[d.key];
                        var c = ShipData(index, d);
                        if (dflt && c.priceDefault > 0) c.price = c.priceDefault;
                        ModFlight.ApplyShip(c, d.json, d.where);
                        db.Ships.Add(c);
                        if (GameModels.Is(c.model, out string shipModel))
                        {
                            // One of the game's own models: nothing to build, ShipAssembly hands the game's out.
                            if (GameModels.Find(db, shipModel) == null) Report(mod, $"{d.where}: \"model\": the game has no model \"{shipModel}\"");
                            else db.ShipModels[index] = shipModel;
                        }
                        else db.Assemblies.Add(new AssemblyData { name = c.assembly, pack = ModContent.Pack, category = "ships", origin = "mod " + mod.Id });
                        if (c.mounts != null && c.mounts.Count > 0)
                            db.WeaponMounts.Add(new WeaponMountSet { ship = index, shipName = c.name, mounts = c.mounts });
                        else if (GameModels.Is(c.model, out string mountsModel)) CopyModelMounts(db, d, index, c.name, mountsModel);
                        ApplyNpcTurrets(db, mod, d, index);
                        ApplyIntegrated(db, mod, d, index);
                    }
                    catch (ModJsonException e) { Report(mod, e.Message); }
                }
            foreach (var kv in placeholderShips)
                if (db.Ship(kv.Key) == null) db.Ships.Add(PlaceholderShip(kv.Key));
            int max = db.Ships.Max(s => s.index);
            for (int i = originalShips; i <= max; i++) if (db.Ship(i) == null) db.Ships.Add(PlaceholderShip(i));
            db.Ships.Sort((a, c) => a.index.CompareTo(c.index));
        }

        /// <summary>Remake (generic turrets): ships.json "npcTurrets", the turret items (category 8 / 35, numbers or "mod:id") the
        /// NPC ships of this type carry on its turret mounts in space (World.NpcMountedTurrets) and parked in the hangar; the
        /// later mod wins, an empty list takes them off.</summary>
        static void ApplyNpcTurrets(Database db, ModInfo mod, ShipDef d, int ship)
        {
            if (!(ModJson.Get(d.json, "npcTurrets") is JArray list)) return;
            var items = new List<int>();
            foreach (var e in list)
            {
                string reference = e.Type == JTokenType.Integer ? ((int)e).ToString() : e.Type == JTokenType.String ? (string)e : null;
                if (reference == null || !TryResolveItem(reference, out int i) || db.Item(i) == null)
                { Report(mod, $"{d.where}: \"npcTurrets\": {e} is no item"); continue; }
                var it = db.Item(i);
                if (it.categoryId != 8 && it.categoryId != 35) { Report(mod, $"{d.where}: \"npcTurrets\": {it.name} ({i}) is no turret"); continue; }
                items.Add(i);
            }
            if (db.MountsOf(ship, 2).Count < items.Count)
                Report(mod, $"{d.where}: \"npcTurrets\" lists {items.Count} turrets, the ship has {db.MountsOf(ship, 2).Count} turret mounts (the rest is left off)");
            db.NpcTurrets[ship] = items;
        }

        /// <summary>Remake (a modder's request): ships.json "integrated", what a ship has built in like the Kothar ships' Khador
        /// Drive (37 / 38 / 40) and the Specter's / Scimitar's cloak (44 / 49): "khadorDrive" true / false (Database.IntegratedDrives:
        /// GalaxyMap.HasJumpDrive, the Khador entry and its hints), "cloak" true (the U'tool's stats, 95), a cloak item (a number or
        /// "mod:id", category 21) or false (Database.IntegratedCloaks: PlayerCloak, the cloak bay). An override can take an
        /// original ship's away; the later mod wins.</summary>
        static void ApplyIntegrated(Database db, ModInfo mod, ShipDef d, int ship)
        {
            if (!(ModJson.Get(d.json, "integrated") is JObject o)) return;
            foreach (var prop in o.Properties())
            {
                var v = prop.Value;
                if (string.Equals(prop.Name, "khadorDrive", StringComparison.OrdinalIgnoreCase))
                {
                    if (v.Type != JTokenType.Boolean) { Report(mod, $"{d.where}: \"integrated\": \"khadorDrive\" must be true or false"); continue; }
                    if ((bool)v) db.IntegratedDrives.Add(ship); else db.IntegratedDrives.Remove(ship);
                }
                else if (string.Equals(prop.Name, "cloak", StringComparison.OrdinalIgnoreCase))
                {
                    if (v.Type == JTokenType.Boolean)
                    {
                        if ((bool)v) db.IntegratedCloaks[ship] = Database.IntegratedCloakItem; else db.IntegratedCloaks.Remove(ship);
                        continue;
                    }
                    string reference = v.Type == JTokenType.Integer ? ((int)v).ToString() : v.Type == JTokenType.String ? (string)v : null;
                    if (reference == null || !TryResolveItem(reference, out int item) || db.Item(item) == null)
                    { Report(mod, $"{d.where}: \"integrated\": \"cloak\": {v} is no item (true, false or a cloak item)"); continue; }
                    if (db.Item(item).categoryId != 21) { Report(mod, $"{d.where}: \"integrated\": \"cloak\": {db.Item(item).name} ({item}) is no cloak"); continue; }
                    db.IntegratedCloaks[ship] = item;
                }
                else mod.Warnings.Add($"{d.where}: \"integrated\": unknown field \"{prop.Name}\" (ignored; \"khadorDrive\", \"cloak\")");
            }
        }

        /// <summary>A mod ship whose mod isn't on: never sold, can't be flown (ModSaves moves the player out of it).</summary>
        static ShipData PlaceholderShip(int index)
        {
            var s = new ShipData { index = index, slots = new ShipSlots(), armor = 100, cargo = 0, price = 0, handling = 100, handlingMultiplier = 1f };
            ShipText(index, false, out s.name);
            ShipText(index, true, out s.description);
            return s;
        }

        static void Report(ModInfo mod, string message)
        {
            if (mod.Warnings.Contains(message)) return;
            mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }

        static ItemData Clone(ItemData s) => new ItemData
        {
            index = s.index, name = s.name, description = s.description, type = s.type, category = s.category, categoryId = s.categoryId,
            techLevel = s.techLevel, occurrence = s.occurrence, minPrice = s.minPrice, maxPrice = s.maxPrice,
            lowestPriceSystem = s.lowestPriceSystem, highestPriceSystem = s.highestPriceSystem,
            statList = s.statList?.Select(e => new StatEntry { key = e.key, value = e.value }).ToList() ?? new List<StatEntry>(),
            blueprint = new List<BlueprintPart>(),   // a copy is no blueprint product
            attrKeys = (int[])s.attrKeys.Clone(), attrValues = (int[])s.attrValues.Clone(),
            modded = s.modded, lookIndex = s.lookIndex, modKey = s.modKey,
        };

        /// <summary>A ship blueprint's item (ModBlueprints): a commodity no shop or loot ever offers (it is a blueprint
        /// product), named and drawn as its ship.</summary>
        static ItemData Deed(int index, ModBlueprints.Def b) => new ItemData
        {
            index = index, name = b.DeedKey, description = "", type = "commodity", category = "Commodity", categoryId = 22, techLevel = 1,
            modded = true, modKey = b.DeedKey, lookIndex = index, blueprint = new List<BlueprintPart>(),
            statList = new List<StatEntry>(), attrKeys = new[] { 0, 1, 2 }, attrValues = new[] { index, 4, 22 },
        };

        static ItemData Placeholder(int index, string key)
        {
            var p = new ItemData
            {
                index = index, type = "commodity", category = "Commodity", categoryId = 22, modded = true, modKey = key, lookIndex = index,
                attrKeys = new[] { 0, 1, 2 }, attrValues = new[] { index, 4, 22 },
            };
            ItemText(index, false, out p.name);
            ItemText(index, true, out p.description);
            return p;
        }

        static void ApplyFields(ItemData it, JObject o, bool defaultEconomy)
        {
            const string F = ItemsFile;
            it.techLevel = ModJson.Int(o, "techLevel", it.techLevel, F);
            it.occurrence = ModJson.Int(o, "occurrence", it.occurrence, F);
            if (ModJson.Get(o, "price") is JToken pr)
            {
                if (pr is JArray a && a.Count == 2) { it.minPrice = (int)a[0]; it.maxPrice = (int)a[1]; }
                else it.minPrice = it.maxPrice = ModJson.Int(o, "price", it.minPrice, F);
            }
            it.minPrice = ModJson.Int(o, "minPrice", it.minPrice, F);
            it.maxPrice = Math.Max(it.minPrice, ModJson.Int(o, "maxPrice", it.maxPrice, F));
            it.lowestPriceSystem = ModJson.Int(o, "lowestPriceSystem", it.lowestPriceSystem, F);
            it.highestPriceSystem = ModJson.Int(o, "highestPriceSystem", it.highestPriceSystem, F);
            SetAttr(it, 3, it.techLevel); SetAttr(it, 4, it.lowestPriceSystem); SetAttr(it, 5, it.highestPriceSystem);
            SetAttr(it, 6, it.occurrence); SetAttr(it, 7, it.minPrice); SetAttr(it, 8, it.maxPrice);
            if (ModJson.Has(o, "vosskOnly")) SetAttr(it, 60, ModJson.Bool(o, "vosskOnly", false, F) ? 1 : 0);
            if (ModJson.Has(o, "alwaysSoldAt")) SetAttr(it, 61, ModJson.Int(o, "alwaysSoldAt", -1, F));
            if (ModJson.Get(o, "stats") is JObject st)
                foreach (var prop in st.Properties()) SetAttr(it, ItemStats.IdOf(prop.Name), ModJson.Int(st, prop.Name, 0, F));
            if (ModJson.Get(o, "attributes") is JObject at)
                foreach (var prop in at.Properties()) if (int.TryParse(prop.Name, out int id) && id > 8) SetAttr(it, id, ModJson.Int(at, prop.Name, 0, F));
            if (defaultEconomy && ModJson.Get(o, "defaultEconomy") is JObject de) ApplyFields(it, de, false);
        }

        /// <summary>Sets an original attribute and the named stat that mirrors it (items.json's statList).</summary>
        static void SetAttr(ItemData it, int id, int value)
        {
            if (id < 0) return;
            int i = Array.IndexOf(it.attrKeys, id);
            if (i >= 0) it.attrValues[i] = value;
            else
            {
                it.attrKeys = it.attrKeys.Append(id).ToArray();
                it.attrValues = it.attrValues.Append(value).ToArray();
            }
            string stat = ItemStats.StatKeyOf(id);
            if (stat == null) return;
            it.statList ??= new List<StatEntry>();
            var e = it.statList.Find(s => s.key == stat);
            if (e != null) e.value = value;
            else it.statList.Add(new StatEntry { key = stat, value = value });
        }

        /// <summary>Database.Load calls this first: the original tables' sizes (the first modded number).</summary>
        public static void NoteOriginalCounts(Database db)
        {
            if (originalItems < 0) originalItems = db.Items.Count;
            if (originalShips < 0) originalShips = db.Ships.Count;
            ModWorld.NoteOriginalCounts(db);
        }

        public static int OriginalItemCount => originalItems;
    }
}
