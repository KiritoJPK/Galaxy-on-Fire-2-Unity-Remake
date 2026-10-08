// Database.cs
// Loads the JSON exported from the original game data (see README) and turns ship + equipment
// into FlightStats for ShipController.
//
// The JSON lives in Assets/Resources/GoF2Data/ and is parsed with Unity's built-in JsonUtility
// (no extra packages needed).   var db = Database.Load();  var betty = db.ShipByName("Betty");

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GoF2Remake.Flight;

namespace GoF2Remake.Data
{
    [System.Serializable] public class ShipSlots { public int primary, secondary, turret, equipment; }

    [System.Serializable] public class ShipData
    {
        public int index;
        public string name, description;
        public int armor, cargo, price;
        public ShipSlots slots;
        public float handling;            // UI value, e.g. 120
        public float handlingMultiplier;  // handling / 100
    }

    [System.Serializable] public class StatEntry { public string key; public int value; }

    [System.Serializable] public class BlueprintPart { public int item; public string name; public int amount; }

    [System.Serializable] public class ItemData
    {
        public int index;
        public string name, description;
        public string type;       // primary | secondary | turret | equipment | commodity
        public string category;   // Laser, Blaster, Shield, Booster, ...
        public int categoryId;
        public int techLevel, occurrence, minPrice, maxPrice, lowestPriceSystem, highestPriceSystem;
        public List<StatEntry> statList = new List<StatEntry>();   // e.g. damage, range, boostSpeed ...
        public List<BlueprintPart> blueprint = new List<BlueprintPart>();

        /// <summary>Original attribute pairs (Item+0x30), from item_attributes.json; see Reference/research/shop.md 2.4.</summary>
        [System.NonSerialized] public int[] attrKeys = new int[0], attrValues = new int[0];

        /// <summary>A mod's item (Modding.ModContent): its "mod:id" key, and the original item it looks and behaves like
        /// (icon, weapon fx, sounds, the special rules the code keys on the item's number). Not modded = its own.</summary>
        [System.NonSerialized] public bool modded;
        [System.NonSerialized] public string modKey;
        [System.NonSerialized] public int lookIndex;
        public int Look => modded ? lookIndex : index;

        public bool HasAttr(int id) => System.Array.IndexOf(attrKeys, id) >= 0;
        public int Attr(int id, int fallback = 0)
        {
            int i = System.Array.IndexOf(attrKeys, id);
            return i >= 0 ? attrValues[i] : fallback;
        }

        /// <summary>Attribute 1: 0 primary, 1 secondary, 2 turret, 3 equipment, 4 commodity.</summary>
        public int TypeId => Attr(1, type switch { "primary" => 0, "secondary" => 1, "turret" => 2, "equipment" => 3, _ => 4 });

        public int Stat(string key, int fallback = 0)
        {
            if (statList != null) foreach (var s in statList) if (s.key == key) return s.value;
            return fallback;
        }
    }

    [System.Serializable] public class MapPos { public int x, y, z; }

    [System.Serializable] public class SystemData
    {
        public int index;
        public string name;
        public int securityLevel;
        public bool initiallyVisible;
        public string race;
        public int raceId;
        public MapPos mapPosition;
        public int jumpgateStation;
        public int textureIndex;
        public List<int> stations, jumpRoutesTo, unknownTriple, forbiddenGoodsOrUnknown;
    }

    [System.Serializable] public class StationData
    {
        public int index;
        public string name;
        public int system;
        public string systemName;
        public int techLevel;
        public int textureIndex;
    }

    /// <summary>Weapon mounts of one ship (weapons_hd.json). slotType 0 = primary, 1 = secondary, 2 = turret,
    /// 3 = engine exhaust points (not a turret, see weapons.md). position_engine is game space, ship-relative.</summary>
    /// <remarks>upsideDown (remake, turret mounts only): the turret hangs under the hull, turned 180 deg about the ship's
    /// length (PlayerTurret); none of the original's mounts set it. glowColor / glowSize (remake, a mod ship's exhaust mounts):
    /// the engine flame's colour (RGB 0..1, ModShipBuilder's tinted glow and the nearest-coloured exhaust particle cell) and
    /// shape (half width, half height, flame length in game units; none = the round glow of CustomShipData.engineGlowRadius).</remarks>
    [System.Serializable] public class WeaponMount
    {
        public int slotType; public int[] position_engine; public float[] turretAngles; public bool upsideDown;
        public float[] glowColor, glowSize;
    }
    [System.Serializable] public class WeaponMountSet { public int ship; public string shipName; public List<WeaponMount> mounts; }

    /// <summary>A mod's ship (a ships.json entry, Modding.ModContent; the format of PR #34's custom_ships.json): a ships.json
    /// entry plus what the original keeps in its fixed tables (race, hangar height), the weapon mounts and how to build its
    /// model at run time (Modding.ModShipBuilder). Paths are inside the mod.</summary>
    [System.Serializable] public class CustomShipData : ShipData
    {
        public int race = -1;              // 0 Terran, 1 Vossk, 2 Nivelian, 3 Midorian, 8 pirate, 9 void (Shop.ShipRace)
        public int priceDefault;           // the Default Economy's price (0 = the same as 'price')
        public int hangarHeight = 250;     // StationTables.ShipY: pivot height above the hangar floor, game units
        public string assembly;            // set by ModContent: "ship_NNN_mod" (Database.ShipAssembly finds it by number)
        public List<WeaponMount> mounts;   // like weapons_hd.json: slotType 0 primary, 1 secondary, 2 turret, 3 exhaust
        public string model;               // the glTF / GLB file in the mod
        public string icon;                // the shop icon (PNG, 180 x 88 like the originals); none = the Phantom's
        public float modelLength = 1000f;  // nose to tail in game units (0.05 m each) after scaling
        public float modelYaw;             // degrees about Unity y, when the model's nose doesn't face +Z
        public float engineGlowRadius = 24f;   // game units, the glow disc at each exhaust mount
        public float[] engineGlowColor;    // RGB 0..1: every exhaust's flame colour (a mount's glowColor wins); none = the original glow
        public List<CustomShipMaterial> materials;   // replace the model's materials on the renderers / submeshes they name
        public CustomThrottleGlow throttleGlow;   // a glow on part of the hull that follows the throttle (no mask = none)
        public List<CustomThrottleGlow> extraGlows;   // more of them (each its own mask, colour, levels and trail)
        public CustomLoungeSeller lounge;  // a lounge visitor who sells it (AgentGenerator.AddCustomShipSellers); null = none
        public CustomDealer dealer;        // remake mods: ship dealers may stock it (Modding.ModUnlocks.AddDealerShips); null = never
    }

    /// <summary>Remake mods: a mod ship in the ordinary ship dealers' lists (Shop.GenerateShips): each time a station's dealer
    /// list is made, 'chance' % (0.1 steps; 100 = always) at stations of 'systemRace' systems (-1 = any) with a tech level
    /// of at least 'minTechLevel'.</summary>
    [System.Serializable] public class CustomDealer
    {
        public float chance = 2f;
        public int systemRace = -1;
        public int minTechLevel;
    }

    /// <summary>When a lounge may have a visitor selling a custom ship (AgentOffer.SellShip): each time a station's bar is
    /// generated (Generator::createAgents: a station not among the last 3 visited), 'chance' % in systems of 'systemRace'
    /// (-1 = any), from campaign step 'minCampaign' (free play: from rank 'minRank'), unless the player flies or stores it.</summary>
    [System.Serializable] public class CustomLoungeSeller
    {
        public int systemRace = -1;
        public int minCampaign;
        public int minRank;
        public int chance = 10;
    }

    /// <summary>A URP Lit material for the renderers whose name contains 'mesh' (empty = any) and, when 'submesh' >= 0,
    /// only for that submesh (one mesh with several materials); paths inside the mod. 'color' (RGB) tints the
    /// diffuse (or is the colour without one); 'metallic' >= 0 sets the metalness without a mask. 'emission' is an
    /// emission map and / or 'emissionColor' (RGB) its colour, x emissionIntensity (> 1 blooms); 'alphaClip' > 0 cuts the
    /// diffuse's alpha below it (decals). 'detailAlbedo' (linear, 0.5 = neutral) / 'detailNormal' are URP's detail maps,
    /// tiled 'detailTiling' times over the UVs (e.g. brushed metal).</summary>
    [System.Serializable] public class CustomShipMaterial
    {
        public string mesh, diffuse, normal, metallicSmoothness, emission, detailAlbedo, detailNormal;
        public int submesh = -1;
        public float[] color, emissionColor;
        public float smoothness = 1f, metallic = -1f, emissionIntensity = 1f, alphaClip, normalScale = 1f;
        public float detailTiling = 1f, detailNormalScale = 1f;
        public float opacity;   // 0 / 1 = opaque; below 1 a see-through surface (glass: premultiplied, so reflections stay bright)
        public bool doubleSided;   // both faces drawn (models with one-sided panels the camera can see from behind)
    }

    /// <summary>A mod ship's throttle-driven glow (Modding.ModShipBuilder, ThrottleGlow): the
    /// hull triangles of the renderers whose name contains 'mesh' (empty = any) and of 'submesh' (-1 = all) whose UVs
    /// touch the lit part of 'mask' (a PNG in the mod; a plain white mask takes them all), copied
    /// 'offset' game units out along their normals and drawn additive with the mask, tinted 'color' (RGB); the glow
    /// intensity runs from 'idle' (throttle 0) to 'full' (throttle 100 %) and up to 'boost' while boosting.</summary>
    [System.Serializable] public class CustomThrottleGlow
    {
        public int submesh = -1;
        public string mask, mesh;
        public float[] color;
        public float idle = 0.35f, full = 4f, boost = 7f, offset = 0.5f;
        public float trailWidth;          // game units; > 0: a trail in the glow's colour from the glow's rear ends while
        public float trailTime = 0.6f;    // boosting or travelling (planet jump, jumpgate, Khador Drive), 'trailTime' s long
        public float trailBrightness = 0.3f;   // the trail x the glow's level
        public int trailCount;            // 0 = one trail at each side's rear end; > 0 = that many along the glow's whole
                                          // rear edge, shaped like an ellipse across it: 'trailWidth' / 'trailTime' /
                                          // brightness in the middle, less toward the ends (the Millennium Falcon's band)
    }

    /// <summary>One assembled prefab (assemblies.json): Resources/Assembled/{pack}/{category}/{name}.prefab.</summary>
    [System.Serializable] public class AssemblyData
    {
        public string name, pack, category, origin;
        public int root;
    }

    /// <summary>A Most Wanted criminal (wanted.json = FileRead::loadWanted, Wanted 0x14805c; wingmen_wanted.md 2.1).</summary>
    [System.Serializable] public class WantedData
    {
        public int index;
        public string name;
        public int board, race;
        public bool male;
        public int ship, weapon, hitpoints, loot, lootAmount, reward, requiredBounties, requiredMission, numWingmen;
        public int[] portraitParts;
    }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class Database
    {
        public List<ShipData> Ships = new List<ShipData>();
        public List<ItemData> Items = new List<ItemData>();
        public List<SystemData> Systems = new List<SystemData>();
        public List<StationData> Stations = new List<StationData>();
        public List<AssemblyData> Assemblies = new List<AssemblyData>();

        /// <summary>Globals::getShipGroup: a ship's assembled object, the main pack's "ship_NNN_*" first, else the add-ons'
        /// "v_ship_NNN_*" / "sn_ship_NNN_*" (ships 39-41 and 44+); the variant ending in 'raceName' when there is one.</summary>
        public AssemblyData ShipAssembly(int ship, string raceName = null)
        {
            // A mod's model for this ship (a new mod ship, or an original's replaced: ModContent.ModelOverrides).
            string modName = $"ship_{ship:000}_mod";
            var modded = Assemblies.Find(a => a.pack == "mod" && a.name == modName);
            if (modded != null) return modded;
            string p = $"ship_{ship:000}_";
            bool Match(AssemblyData a, bool main) =>
                a.category == "ships" && (main ? a.name.StartsWith(p) : a.name.StartsWith("v_" + p) || a.name.StartsWith("sn_" + p));
            foreach (bool main in new[] { true, false })
            {
                if (raceName != null) { var r = Assemblies.Find(a => Match(a, main) && a.name.EndsWith(raceName)); if (r != null) return r; }
                var any = Assemblies.Find(a => Match(a, main));
                if (any != null) return any;
            }
            // Globals::getShipGroup's special branches: 13 the Vossk freighter, 14 the Terran battleship (Level::createShip
            // builds battleship_terran, NpcShip), 15 the freighter by race (Vossk = 13's).
            return ship switch
            {
                13 => AssemblyByName("cargo_004_vossk"),
                14 => AssemblyByName("battleship_terran"),
                15 => AssemblyByName(raceName switch
                {
                    "nivelian" => "cargo_002_nivelian", "midorian" => "cargo_001_midorian", "vossk" => "cargo_004_vossk", _ => "cargo_003_terran",
                }),
                _ => null,
            };
        }
        public List<WeaponMountSet> WeaponMounts = new List<WeaponMountSet>();
        public List<WantedData> Wanted = new List<WantedData>();

        [System.Serializable] class ItemAttributes { public int index; public int[] keys, values; }

        /// <summary>economy_default.json: the Default Economy's complete values of every item / ship that differs.</summary>
        [System.Serializable] class EconomyItem
        {
            public int index, techLevel, occurrence, minPrice, maxPrice, lowestPriceSystem, highestPriceSystem;
            public int[] keys, values;
            public List<StatEntry> statList;
            public List<BlueprintPart> blueprint;
        }
        [System.Serializable] class EconomyShip { public int index, armor, cargo, price, primary, secondary, turret, equipment, handling; }
        [System.Serializable] class EconomyFile { public List<EconomyItem> items; public List<EconomyShip> ships; }

        /// <summary>The item and ship tables this instance holds (Session.Economy when it was loaded).</summary>
        public Economy Economy { get; private set; } = Economy.Android;

        /// <summary>The macOS / Windows / iPhone tables (KiritoJPK's Default Economy items.bin / ships.bin) over the Android
        /// ones: prices, tech levels, occurrences, attributes (and so the stats) and blueprint recipes; ship prices and slots.</summary>
        void ApplyDefaultEconomy(string folder)
        {
            var file = Read<EconomyFile>(folder, "economy_default");
            if (file == null || file.items == null) return;
            foreach (var e in file.items)
            {
                var it = Item(e.index);
                if (it == null) continue;
                it.techLevel = e.techLevel; it.occurrence = e.occurrence; it.minPrice = e.minPrice; it.maxPrice = e.maxPrice;
                it.lowestPriceSystem = e.lowestPriceSystem; it.highestPriceSystem = e.highestPriceSystem;
                it.attrKeys = e.keys ?? new int[0]; it.attrValues = e.values ?? new int[0];
                it.statList = e.statList ?? new List<StatEntry>();
                it.blueprint = e.blueprint ?? new List<BlueprintPart>();
            }
            if (file.ships != null)
                foreach (var e in file.ships)
                {
                    var sh = Ship(e.index);
                    if (sh == null) continue;
                    sh.armor = e.armor; sh.cargo = e.cargo; sh.price = e.price;
                    sh.slots = new ShipSlots { primary = e.primary, secondary = e.secondary, turret = e.turret, equipment = e.equipment };
                    sh.handling = e.handling; sh.handlingMultiplier = e.handling / 100f;
                }
            Economy = Economy.Default;
        }

        static Database shared;
        static Economy sharedEconomy;
        static int sharedRevision = -1;

        /// <summary>One loaded game database for read-only lookups (names, stations, systems), made again only when the
        /// economy or the mods change. Load() parses every table again (several MB of garbage, a hitch): callers that only
        /// read and run during play (Discord's status every 2 s, the mods' music on every music change) use this. Never
        /// change what it returns.</summary>
        public static Database Shared
        {
            get
            {
                if (shared == null || sharedEconomy != Session.Economy || sharedRevision != Modding.ModManager.Revision)
                {
                    sharedEconomy = Session.Economy;
                    sharedRevision = Modding.ModManager.Revision;
                    shared = Load();
                }
                return shared;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() { shared = null; sharedRevision = -1; }

        public static Database Load(string resourceFolder = "GoF2Data")
        {
            var db = new Database
            {
                Ships = Read<List<ShipData>>(resourceFolder, "ships"),
                Items = Read<List<ItemData>>(resourceFolder, "items"),
                Systems = Read<List<SystemData>>(resourceFolder, "systems"),
                Stations = Read<List<StationData>>(resourceFolder, "stations"),
                Assemblies = ReadAssemblies(resourceFolder),
                WeaponMounts = Read<List<WeaponMountSet>>(resourceFolder, "weapons_hd"),
                Wanted = Read<List<WantedData>>(resourceFolder, "wanted"),
            };
            // item_attributes.json (Reference/tools/shop/build_item_attributes.py): items.json keeps them in a dictionary.
            foreach (var a in Read<List<ItemAttributes>>(resourceFolder, "item_attributes"))
            {
                var item = db.Items.Find(i => i.index == a.index);
                if (item != null && a.keys != null) { item.attrKeys = a.keys; item.attrValues = a.values; }
            }
            Modding.ModContent.NoteOriginalCounts(db);
            if (Session.Economy == Economy.Default) db.ApplyDefaultEconomy(resourceFolder);
            // The active mods' new and changed entries (Modding.ModContent), after the economy so their prices stand.
            if (resourceFolder == "GoF2Data") Modding.ModContent.Apply(db);
            return db;
        }

        // The search is a loop of its own: a lambda capturing 'index' allocated its closure on every call, the fast path too
        // (the HUD's cargo readout calls these every frame).
        public ItemData Item(int index) => index >= 0 && index < Items.Count && Items[index].index == index ? Items[index] : FindItem(index);
        public ShipData Ship(int index) => index >= 0 && index < Ships.Count && Ships[index].index == index ? Ships[index] : FindShip(index);

        ItemData FindItem(int index)
        {
            foreach (var i in Items) if (i.index == index) return i;
            return null;
        }

        ShipData FindShip(int index)
        {
            foreach (var s in Ships) if (s.index == index) return s;
            return null;
        }

        [System.Serializable] class Wrapper<W> { public W list; }

        static T Read<T>(string folder, string file) where T : new()
        {
            var ta = Resources.Load<TextAsset>(folder + "/" + file);
            if (ta == null)
            {
                Debug.LogWarning("Database: missing Resources/" + folder + "/" + file + ".json");
                return new T();
            }
            return JsonUtility.FromJson<Wrapper<T>>("{\"list\":" + ta.text + "}").list;
        }

        [System.Serializable] class AssemblyFile { public List<AssemblyData> entries; }

        static List<AssemblyData> ReadAssemblies(string folder)
        {
            var ta = Resources.Load<TextAsset>(folder + "/assemblies");
            return ta != null ? JsonUtility.FromJson<AssemblyFile>(ta.text).entries : new List<AssemblyData>();
        }

        public AssemblyData AssemblyByName(string name) => Assemblies.FirstOrDefault(a => a.name == name);

        /// <summary>Mount positions of a slot type for a ship, in the order of Ship::getSlotPos.</summary>
        public List<WeaponMount> MountsOf(int shipIndex, int slotType)
        {
            var set = WeaponMounts.FirstOrDefault(m => m.ship == shipIndex);
            return set != null ? set.mounts.Where(m => m.slotType == slotType).ToList() : new List<WeaponMount>();
        }

        public ShipData ShipByName(string name) => Ships.FirstOrDefault(s => s.name == name);
        public ItemData ItemByName(string name) => Items.FirstOrDefault(i => i.name == name);
        public IEnumerable<StationData> StationsIn(int systemIndex) => Stations.Where(s => s.system == systemIndex);

        /// <summary>
        /// Builds flight stats the same way the original combines Ship + installed equipment
        /// (Ship::refreshValue / PlayerEgo ctor): booster = category "Booster", agility = "Steering nozzle".
        /// </summary>
        public static FlightStats BuildFlightStats(ShipData ship, IEnumerable<ItemData> equipment, int handlingUpgrades = 0)
        {
            var fs = new FlightStats { handling = ship.handling, handlingUpgrades = handlingUpgrades };
            foreach (var item in equipment ?? Enumerable.Empty<ItemData>())
            {
                switch (item.categoryId)
                {
                    case 14: // Booster
                        fs.boostSpeed = item.Stat("boostSpeed");
                        fs.boostDurationMs = item.Stat("boostDurationMs");
                        fs.boostRechargeMs = item.Stat("boostRechargeMs");
                        break;
                    case 16: // Steering nozzle
                        fs.agility = item.Stat("agility");
                        break;
                }
            }
            return fs;
        }
    }
}
