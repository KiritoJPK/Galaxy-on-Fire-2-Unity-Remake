// OriginalSave.cs
// Reads and writes the original game's save files (Galaxy on Fire 2 HD for Android): a slot is gof2_save_game_N, its slot
// list entry gof2_save_game_preview_N, in Android/data/net.fishlabs.gof2hdallandroid2012/files (ToJNI.SetDirectories).
//
// Format (RecordHandler::recordStoreWrite 0xdf760 / recordStoreRead, writeMission / writeAgent / writeWanted, AEFile::Write):
// the fields of OriginalRecord one after the other as raw little-endian memory: int 4 bytes, bool 1, long 8, float 4; a
// string is its length in characters (int) and UTF-16LE (AEFile::Write(String, file, true)); lists are a count and their
// entries. A blueprint's invested amounts carry no count: the product's recipe gives it (BluePrint::BluePrint, the products
// in item order, Status::resetGame), and that differs between the Android and the Default economy. Fields after the int
// 0x6e6a78 only exist in later versions (recordStoreRead stops before them when the marker is missing).
//
// Signature (RecordHandler::addHash 0xded4c / checkHash 0xde618): the record ends in SHA-256 over its data, a 25-byte salt
// and a 16-byte key, g_android_origami_super_club, which the Java side sets (GOF2HD2012.SetOrigamiSuperClub). The build the
// remake ports from passes "9506fe987e36474d"; Fishlabs' store build used the device's android_id, so its saves are tied to
// a phone. recordStoreRead refuses a record whose signature doesn't match; reading here doesn't check it (SignatureValid
// says), writing signs with the given key. The preview isn't signed.
//
// The PC "Full HD" version (%APPDATA%\Galaxy On Fire 2 Full HD, the same file names) writes the same stream
// without a signature (Read / Write with signed false / key null) and with its own tables: a 2016 save had 109 stations,
// 196 items, 36 medals and 17 blueprints of the Default economy, no 0x6e6a78 fields; it read and wrote back byte for byte.
// Checked against 965 real Android saves (2026-10-16: XylonDrag's mission and fast-start saves for the community builds,
// GitHub, not in the repository): every one read to its last byte (964 Android economy, 1 Default); the 773 signed with
// the community key wrote back byte for byte; the other 192 (gof2_save_game_-1 / _1-1, no preview: presumably the store
// build's, signed with a device's android_id) only differ in the signature when written; 773 previews byte for byte.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GoF2Remake.Data
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // read-only tables
    public static class OriginalSave
    {
        /// <summary>g_android_origami_super_club as the community Android build sets it.</summary>
        public const string ModdedKey = "9506fe987e36474d";
        public const int HashLength = 32;
        /// <summary>recordStoreWrite: the int before the later versions' fields ("xjn").</summary>
        public const int ExtendedMarker = 0x6e6a78;
        const int MaxCount = 100000;   // a corrupt count, not a real list

        /// <summary>addHash's 25 bytes between the data and the key (0x4c4c3052a7c22b23, 0x33, 0x65745361302872, 0x72, "&_%=$+#").</summary>
        static readonly byte[] Salt =
        {
            0x23, 0x2b, 0xc2, 0xa7, 0x52, 0x30, 0x4c, 0x4c, 0x33, 0x72, 0x28, 0x30, 0x61, 0x53, 0x74, 0x65,
            0x72, 0x26, 0x5f, 0x25, 0x3d, 0x24, 0x2b, 0x23, 0x00,
        };

        // Globals::hints in the order recordStoreWrite writes them (0x22 before 0x21).
        static readonly int[] HintsA = Enumerable.Range(8, 0x21 - 8).Concat(new[] { 0x22, 0x21, 0x23, 0x24 }).ToArray();
        static readonly int[] HintsB = { 0x25, 0x26 };
        static readonly int[] HintsC = { 0x28, 0x29, 0x2c, 0x2a, 0x2b, 0x2e, 0x2f, 0x30 };
        static readonly int[] HintsD = { 0x31, 0x2d, 0x32 };
        static readonly int[] HintsE = { 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a };

        // ---- signature ----------------------------------------------------------------------------------------------

        static byte[] Hash(byte[] data, int length, string key)
        {
            using var sha = SHA256.Create();
            sha.TransformBlock(data, 0, length, null, 0);
            sha.TransformBlock(Salt, 0, Salt.Length, null, 0);
            var k = Encoding.ASCII.GetBytes(key ?? "");
            sha.TransformFinalBlock(k, 0, k.Length);
            return sha.Hash;
        }

        /// <summary>checkHash: the last 32 bytes are the signature of the rest with this key.</summary>
        public static bool SignatureValid(byte[] file, string key = ModdedKey)
        {
            if (file == null || file.Length <= HashLength + 1) return false;
            var h = Hash(file, file.Length - HashLength, key);
            for (int i = 0; i < HashLength; i++) if (h[i] != file[file.Length - HashLength + i]) return false;
            return true;
        }

        /// <summary>addHash: the data with its signature appended.</summary>
        public static byte[] Sign(byte[] data, string key = ModdedKey)
        {
            var h = Hash(data, data.Length, key);
            var file = new byte[data.Length + HashLength];
            Buffer.BlockCopy(data, 0, file, 0, data.Length);
            Buffer.BlockCopy(h, 0, file, data.Length, HashLength);
            return file;
        }

        /// <summary>Each blueprint record's invested-amount count: the original items with a recipe, in item order, with this
        /// database's economy (Android 6, 6, 6, 6, 2, ...; Default 6, 6, 5, 5, 3, ...).</summary>
        public static int[] IngredientCounts(Database db) =>
            db.Items.Where(i => !i.modded && i.blueprint != null && i.blueprint.Count > 0).OrderBy(i => i.index)
                    .Select(i => i.blueprint.Count).ToArray();

        // ---- reading --------------------------------------------------------------------------------------------------

        /// <summary>A save slot's file (signature not checked). 'ingredients': IngredientCounts of the save's economy.</summary>
        public static OriginalRecord Read(byte[] file, IReadOnlyList<int> ingredients, bool signed = true)
        {
            int hash = signed ? HashLength : 0;   // the PC Full HD version's records carry no signature
            if (file == null || file.Length <= hash + 1) throw new InvalidDataException("not a Galaxy on Fire 2 save (too short)");
            var r = new In(file, file.Length - hash);
            var s = new OriginalRecord();
            s.visitedStations = r.Bools();
            s.credits = r.Int(); s.rating = r.Int(); s.playingTimeMs = r.Long();
            s.kills = r.Int(); s.missionCount = r.Int(); s.level = r.Int(); s.lastXp = r.Int(); s.goodsProduced = r.Int();
            s.stationsVisited = r.Int(); s.campaignMission = r.Int();
            s.freelanceMission = ReadMission(r); s.storyMission = ReadMission(r);
            s.jumpgatesUsed = r.Int(); s.capturedCrates = r.Int(); s.boughtEquipment = r.Int(); s.pirateKills = r.Int();
            s.status80 = r.Int(); s.status7c = r.Int(); s.voidReturnStation = r.Int(); s.status88 = r.Int();
            s.oreTypesMined = r.Bools(); s.coreTypesMined = r.Bools();
            s.courierGoods = r.Int(); s.oresMined = r.Int(); s.coresMined = r.Int(); s.boozeBought = r.Int();
            s.boozeTypes = r.Bools();
            s.spaceJunk = r.Int();
            s.statusB4 = r.Bools();
            s.passengersCarried = r.Int();
            s.cloakMs = r.Long();
            for (int i = 0; i < s.statusC8.Length; i++) s.statusC8[i] = r.Int();
            s.medals = r.Ints();
            s.ship = r.Int(); s.shipRace = r.Int();
            s.equipment = ReadEquipment(r);
            s.cargo = ReadItems(r);
            int stack = r.Int();   // getStationStack's length (3), the current station after it; -1 = none at all
            if (stack < -1 || stack > 16) throw new InvalidDataException($"a station stack of {stack}: not a save, or a damaged one");
            s.stations = new OriginalStation[stack + 1];
            for (int i = 0; i <= stack; i++) s.stations[i] = ReadStation(r);
            s.standings = r.Ints();
            int prints = r.Count();
            for (int i = 0; i < prints; i++)
            {
                if (ingredients == null || i >= ingredients.Count) throw new InvalidDataException($"blueprint {i}: no recipe known for it");
                var b = new OriginalBlueprint { invested = new int[ingredients[i]] };
                for (int k = 0; k < b.invested.Length; k++) b.invested[k] = r.Int();
                b.field4 = r.Int(); b.unlocked = r.Bool(); b.fieldC = r.Int(); b.station = r.Int(); b.stationName = r.Str();
                s.blueprints.Add(b);
            }
            int pending = r.Int();   // -1 or 0 = none
            for (int i = 0; i < pending; i++)
                s.pendingProducts.Add(new OriginalPendingProduct { a = r.Int(), b = r.Int(), c = r.Int(), name = r.Str() });
            int wingmen = r.Int();   // -1 = none (recordStoreRead: < 1 reads nothing more)
            if (wingmen > 0)
            {
                s.wingmen = new OriginalWingmen();
                for (int i = 0; i < wingmen; i++) s.wingmen.names.Add(r.Str());
                s.wingmen.status2c = r.Int(); s.wingmen.status30 = r.Int(); s.wingmen.status28 = r.Ints();
            }
            s.passengers = r.Int();
            s.systemsVisible = r.Bools();
            s.status40 = r.Ints(); s.status3c = r.Ints(); s.status48 = r.Ints(); s.status44 = r.Ints();
            s.status4c = r.Bools();
            int agents = r.Count();
            for (int i = 0; i < agents; i++) s.agents.Add(ReadAgent(r));
            foreach (int h in HintsA) s.hints[h] = r.Bool();
            s.difficulty = r.Float(); s.status100 = r.Long();
            foreach (int h in HintsB) s.hints[h] = r.Bool();
            if (r.Int() > 0)
                s.parkedShip = new OriginalParkedShip { ship = r.Int(), race = r.Int(), equipment = ReadEquipment(r), cargo = ReadItems(r) };
            s.storyTargets = r.Ints();
            s.status10c = r.Int(); s.status110 = r.Bool(); s.kaamoState = r.Int(); s.status111 = r.Bool();
            s.kaamoItems = ReadItems(r);
            s.kaamoShips = ReadShips(r);
            s.hints[0x27] = r.Bool(); s.options35 = r.Bool(); s.options36 = r.Bool();
            s.itemsObtained = r.Bools();

            // recordStoreRead reads one more int: the later versions' marker, or the record ends here.
            if (r.Left < 4 || r.PeekInt() != ExtendedMarker) { s.trailing = r.Rest(); return s; }
            r.Int();
            s.extended = true;
            s.shipMods = r.Ints();
            s.parkedShipMods = r.Ints();
            int kaamoMods = r.Count();
            for (int i = 0; i < kaamoMods; i++) { var m = r.Ints(); if (i < s.kaamoShips.Count) s.kaamoShips[i].mods = m; }
            foreach (var st in s.stations)
            {
                if (st == null) continue;
                int n = r.Count();
                for (int i = 0; i < n; i++) { var m = r.Ints(); if (i < st.ships.Count) st.ships[i].mods = m; }
            }
            int wanted = r.Count();
            for (int i = 0; i < wanted; i++) s.wanted.Add(ReadWanted(r));
            for (int i = 0; i < 4; i++) s.collectedBounties[i] = r.Int();
            s.options37 = r.Bool(); s.status174 = r.Int();
            foreach (int h in HintsC) s.hints[h] = r.Bool();
            s.status58 = r.Bools();
            foreach (int h in HintsD) s.hints[h] = r.Bool();
            s.status118 = r.Int();
            foreach (int h in HintsE) s.hints[h] = r.Bool();
            s.trailing = r.Rest();
            return s;
        }

        static OriginalMission ReadMission(In r)
        {
            var m = new OriginalMission { type = r.Int() };
            if (m.Empty) return m;
            m.clientName = r.Str(); m.targetName = r.Str(); m.targetStationName = r.Str(); m.targetSystemName = r.Str();
            m.campaign = r.Bool();
            m.clientImage = r.OptionalInts();
            m.clientRace = r.Int(); m.costs = r.Int(); m.bonus = r.Int(); m.reward = r.Int(); m.targetStation = r.Int();
            m.difficulty = r.Int(); m.goodIndex = r.Int(); m.goodAmount = r.Int(); m.status = r.Int();
            m.visible = r.Bool();
            if (r.Int() > 0) m.agent = ReadAgent(r);
            return m;
        }

        static OriginalAgent ReadAgent(In r)
        {
            var a = new OriginalAgent
            {
                costs = r.Int(), sellSystem = r.Int(), sellBlueprint = r.Int(), eventState = r.Int(), index = r.Int(), offer = r.Int(),
                race = r.Int(), sellItem = r.Int(), sellItemPrice = r.Int(), sellItemAmount = r.Int(), station = r.Int(), system = r.Int(),
                wingmanFriends = r.Int(),
                male = r.Bool(), hasReward = r.Bool(), acceptedOffer = r.Bool(), flag1c = r.Bool(), flag1d = r.Bool(),
            };
            a.imageParts = r.OptionalInts();
            if (a.index > 0x12) a.sellMod = r.Int();   // writeAgent: only the Kaamo Club's mechanics (agents 19+) sell mods
            a.missionText = r.Str(); a.name = r.Str(); a.stationName = r.Str(); a.systemName = r.Str(); a.text8 = r.Str(); a.textC = r.Str();
            if (r.Int() > 0) a.mission = ReadMission(r);
            return a;
        }

        static OriginalWanted ReadWanted(In r)
        {
            var w = new OriginalWanted
            {
                active = r.Bool(), terminated = r.Bool(), location = r.Int(), travelsTo = r.Int(), lastSeen = r.Int(), name = r.Str(),
                index = r.Int(), board = r.Int(), race = r.Int(), male = r.Bool(), ship = r.Int(), weapon = r.Int(), hitpoints = r.Int(),
                loot = r.Int(), lootAmount = r.Int(), reward = r.Int(), requiredBounties = r.Int(), requiredMission = r.Int(), wingmen = r.Int(),
            };
            for (int i = 0; i < 5; i++) w.imageParts[i] = r.Int();   // always 5, no count
            return w;
        }

        static OriginalStation ReadStation(In r)
        {
            int index = r.Int();
            if (index == -1) return null;
            var st = new OriginalStation { index = index, items = ReadItems(r), ships = ReadShips(r) };
            int agents = r.Count();
            for (int i = 0; i < agents; i++) st.agents.Add(ReadAgent(r));
            st.attackedFriends = r.Bool();
            return st;
        }

        static List<OriginalEquipment> ReadEquipment(In r)
        {
            var list = new List<OriginalEquipment>();
            int n = r.Count();
            for (int i = 0; i < n; i++)
            {
                int item = r.Int();
                list.Add(item == -1 ? new OriginalEquipment() : new OriginalEquipment { item = item, amount = r.Int(), unsaleable = r.Bool() });
            }
            return list;
        }

        static List<OriginalItem> ReadItems(In r)
        {
            var list = new List<OriginalItem>();
            int n = r.Count();
            for (int i = 0; i < n; i++) list.Add(new OriginalItem { item = r.Int(), amount = r.Int(), price = r.Int(), unsaleable = r.Bool() });
            return list;
        }

        static List<OriginalShip> ReadShips(In r)
        {
            var list = new List<OriginalShip>();
            int n = r.Count();
            for (int i = 0; i < n; i++) list.Add(new OriginalShip { ship = r.Int(), race = r.Int() });
            return list;
        }

        /// <summary>A preview file (recordStoreWritePreview).</summary>
        public static OriginalPreview ReadPreview(byte[] file)
        {
            var r = new In(file, file?.Length ?? 0);
            return new OriginalPreview
            {
                playingTimeMs = r.Long(), credits = r.Int(), station = r.Str(), system = r.Str(), campaignMission = r.Int(), level = r.Int(),
                difficulty = r.Float(), ship = r.Int(),
            };
        }

        // ---- writing --------------------------------------------------------------------------------------------------

        /// <summary>A save slot's file, signed with 'key' (null: unsigned, the PC Full HD version's records). 'ingredients':
        /// IngredientCounts of the save's economy.</summary>
        public static byte[] Write(OriginalRecord s, IReadOnlyList<int> ingredients, string key = ModdedKey)
        {
            var w = new Out();
            w.Bools(s.visitedStations);
            w.Int(s.credits); w.Int(s.rating); w.Long(s.playingTimeMs);
            w.Int(s.kills); w.Int(s.missionCount); w.Int(s.level); w.Int(s.lastXp); w.Int(s.goodsProduced);
            w.Int(s.stationsVisited); w.Int(s.campaignMission);
            WriteMission(w, s.freelanceMission); WriteMission(w, s.storyMission);
            w.Int(s.jumpgatesUsed); w.Int(s.capturedCrates); w.Int(s.boughtEquipment); w.Int(s.pirateKills);
            w.Int(s.status80); w.Int(s.status7c); w.Int(s.voidReturnStation); w.Int(s.status88);
            w.Bools(s.oreTypesMined); w.Bools(s.coreTypesMined);
            w.Int(s.courierGoods); w.Int(s.oresMined); w.Int(s.coresMined); w.Int(s.boozeBought);
            w.Bools(s.boozeTypes);
            w.Int(s.spaceJunk);
            w.Bools(s.statusB4);
            w.Int(s.passengersCarried);
            w.Long(s.cloakMs);
            for (int i = 0; i < 10; i++) w.Int(i < s.statusC8.Length ? s.statusC8[i] : 0);
            w.Ints(s.medals);
            w.Int(s.ship); w.Int(s.shipRace);
            WriteEquipment(w, s.equipment);
            WriteItems(w, s.cargo);
            var stations = s.stations ?? new OriginalStation[0];
            w.Int(stations.Length - 1);   // 3 (an empty stack: -1)
            foreach (var st in stations) WriteStation(w, st);
            w.Ints(s.standings);
            w.Int(s.blueprints.Count);
            for (int i = 0; i < s.blueprints.Count; i++)
            {
                var b = s.blueprints[i];
                if (ingredients == null || i >= ingredients.Count) throw new InvalidDataException($"blueprint {i}: no recipe known for it");
                for (int k = 0; k < ingredients[i]; k++) w.Int(k < b.invested.Length ? b.invested[k] : 0);
                w.Int(b.field4); w.Bool(b.unlocked); w.Int(b.fieldC); w.Int(b.station); w.Str(b.stationName);
            }
            if (s.pendingProducts.Count == 0) w.Int(-1);
            else
            {
                w.Int(s.pendingProducts.Count);
                foreach (var p in s.pendingProducts) { w.Int(p.a); w.Int(p.b); w.Int(p.c); w.Str(p.name); }
            }
            if (s.wingmen == null || s.wingmen.names.Count == 0) w.Int(-1);
            else
            {
                w.Int(s.wingmen.names.Count);
                foreach (var n in s.wingmen.names) w.Str(n);
                w.Int(s.wingmen.status2c); w.Int(s.wingmen.status30); w.Ints(s.wingmen.status28);
            }
            w.Int(s.passengers);
            w.Bools(s.systemsVisible);
            w.Ints(s.status40); w.Ints(s.status3c); w.Ints(s.status48); w.Ints(s.status44);
            w.Bools(s.status4c);
            w.Int(s.agents.Count);
            foreach (var a in s.agents) WriteAgent(w, a);
            foreach (int h in HintsA) w.Bool(s.hints[h]);
            w.Float(s.difficulty); w.Long(s.status100);
            foreach (int h in HintsB) w.Bool(s.hints[h]);
            if (s.parkedShip == null) w.Int(0);
            else
            {
                w.Int(1); w.Int(s.parkedShip.ship); w.Int(s.parkedShip.race);
                WriteEquipment(w, s.parkedShip.equipment); WriteItems(w, s.parkedShip.cargo);
            }
            w.Ints(s.storyTargets);
            w.Int(s.status10c); w.Bool(s.status110); w.Int(s.kaamoState); w.Bool(s.status111);
            WriteItems(w, s.kaamoItems);
            WriteShips(w, s.kaamoShips);
            w.Bool(s.hints[0x27]); w.Bool(s.options35); w.Bool(s.options36);
            w.Bools(s.itemsObtained);
            if (s.extended)
            {
                w.Int(ExtendedMarker);
                w.Ints(s.shipMods);
                w.Ints(s.parkedShip == null ? new int[0] : s.parkedShipMods);
                w.Int(s.kaamoShips.Count);
                foreach (var k in s.kaamoShips) w.Ints(k.mods);
                foreach (var st in stations)
                {
                    if (st == null) continue;
                    w.Int(st.ships.Count);
                    foreach (var sh in st.ships) w.Ints(sh.mods);
                }
                w.Int(s.wanted.Count);
                foreach (var x in s.wanted) WriteWanted(w, x);
                for (int i = 0; i < 4; i++) w.Int(i < s.collectedBounties.Length ? s.collectedBounties[i] : 0);
                w.Bool(s.options37); w.Int(s.status174);
                foreach (int h in HintsC) w.Bool(s.hints[h]);
                w.Bools(s.status58);
                foreach (int h in HintsD) w.Bool(s.hints[h]);
                w.Int(s.status118);
                foreach (int h in HintsE) w.Bool(s.hints[h]);
            }
            w.Raw(s.trailing);
            return key == null ? w.ToArray() : Sign(w.ToArray(), key);
        }

        static void WriteMission(Out w, OriginalMission m)
        {
            if (m == null || m.Empty) { w.Int(-1); return; }
            w.Int(m.type);
            w.Str(m.clientName); w.Str(m.targetName); w.Str(m.targetStationName); w.Str(m.targetSystemName);
            w.Bool(m.campaign);
            w.OptionalInts(m.clientImage);
            w.Int(m.clientRace); w.Int(m.costs); w.Int(m.bonus); w.Int(m.reward); w.Int(m.targetStation);
            w.Int(m.difficulty); w.Int(m.goodIndex); w.Int(m.goodAmount); w.Int(m.status);
            w.Bool(m.visible);
            if (m.agent == null) w.Int(-1); else { w.Int(1); WriteAgent(w, m.agent); }
        }

        static void WriteAgent(Out w, OriginalAgent a)
        {
            w.Int(a.costs); w.Int(a.sellSystem); w.Int(a.sellBlueprint); w.Int(a.eventState); w.Int(a.index); w.Int(a.offer);
            w.Int(a.race); w.Int(a.sellItem); w.Int(a.sellItemPrice); w.Int(a.sellItemAmount); w.Int(a.station); w.Int(a.system);
            w.Int(a.wingmanFriends);
            w.Bool(a.male); w.Bool(a.hasReward); w.Bool(a.acceptedOffer); w.Bool(a.flag1c); w.Bool(a.flag1d);
            w.OptionalInts(a.imageParts);
            if (a.index > 0x12) w.Int(a.sellMod);
            w.Str(a.missionText); w.Str(a.name); w.Str(a.stationName); w.Str(a.systemName); w.Str(a.text8); w.Str(a.textC);
            if (a.mission == null) w.Int(-1); else { w.Int(1); WriteMission(w, a.mission); }
        }

        static void WriteWanted(Out w, OriginalWanted x)
        {
            w.Bool(x.active); w.Bool(x.terminated); w.Int(x.location); w.Int(x.travelsTo); w.Int(x.lastSeen); w.Str(x.name);
            w.Int(x.index); w.Int(x.board); w.Int(x.race); w.Bool(x.male); w.Int(x.ship); w.Int(x.weapon); w.Int(x.hitpoints);
            w.Int(x.loot); w.Int(x.lootAmount); w.Int(x.reward); w.Int(x.requiredBounties); w.Int(x.requiredMission); w.Int(x.wingmen);
            for (int i = 0; i < 5; i++) w.Int(i < x.imageParts.Length ? x.imageParts[i] : 0);
        }

        static void WriteStation(Out w, OriginalStation st)
        {
            if (st == null) { w.Int(-1); return; }
            w.Int(st.index);
            WriteItems(w, st.items);
            WriteShips(w, st.ships);
            w.Int(st.agents.Count);
            foreach (var a in st.agents) WriteAgent(w, a);
            w.Bool(st.attackedFriends);
        }

        static void WriteEquipment(Out w, List<OriginalEquipment> list)
        {
            w.Int(list.Count);
            foreach (var e in list)
            {
                if (e == null || e.item == -1) { w.Int(-1); continue; }
                w.Int(e.item); w.Int(e.amount); w.Bool(e.unsaleable);
            }
        }

        static void WriteItems(Out w, List<OriginalItem> list)
        {
            w.Int(list.Count);
            foreach (var i in list) { w.Int(i.item); w.Int(i.amount); w.Int(i.price); w.Bool(i.unsaleable); }
        }

        static void WriteShips(Out w, List<OriginalShip> list)
        {
            w.Int(list.Count);
            foreach (var s in list) { w.Int(s.ship); w.Int(s.race); }
        }

        /// <summary>A preview file (recordStoreWritePreview; not signed).</summary>
        public static byte[] WritePreview(OriginalPreview p)
        {
            var w = new Out();
            w.Long(p.playingTimeMs); w.Int(p.credits); w.Str(p.station); w.Str(p.system); w.Int(p.campaignMission); w.Int(p.level);
            w.Float(p.difficulty); w.Int(p.ship);
            return w.ToArray();
        }

        // ---- the byte stream --------------------------------------------------------------------------------------------

        sealed class In
        {
            readonly byte[] b;
            readonly int end;
            int pos;
            public In(byte[] bytes, int length) { b = bytes ?? new byte[0]; end = length; }
            public int Left => end - pos;
            void Need(int n) { if (pos + n > end) throw new InvalidDataException($"the save ends early (at byte {pos}, {n} more needed)"); }
            public int Int() { Need(4); int v = BitConverter.ToInt32(b, pos); pos += 4; return v; }
            public int PeekInt() { Need(4); return BitConverter.ToInt32(b, pos); }
            public long Long() { Need(8); long v = BitConverter.ToInt64(b, pos); pos += 8; return v; }
            public float Float() { Need(4); float v = BitConverter.ToSingle(b, pos); pos += 4; return v; }
            public bool Bool() { Need(1); return b[pos++] != 0; }
            /// <summary>A list count: 0 for a negative one (the game reads -1 as none), a corrupt one refused.</summary>
            public int Count()
            {
                int n = Int();
                if (n > MaxCount) throw new InvalidDataException($"a list of {n} entries at byte {pos - 4}: not a save, or a damaged one");
                return Math.Max(0, n);
            }
            public bool[] Bools() { var a = new bool[Count()]; for (int i = 0; i < a.Length; i++) a[i] = Bool(); return a; }
            public int[] Ints() { var a = new int[Count()]; for (int i = 0; i < a.Length; i++) a[i] = Int(); return a; }
            /// <summary>-1 (or 0) = null, else that many ints (the image parts: 5).</summary>
            public int[] OptionalInts()
            {
                int n = Int();
                if (n <= 0) return null;
                if (n > MaxCount) throw new InvalidDataException($"a list of {n} entries at byte {pos - 4}: not a save, or a damaged one");
                var a = new int[n]; for (int i = 0; i < n; i++) a[i] = Int(); return a;
            }
            public string Str()
            {
                int n = Count();
                Need(n * 2);
                var s = Encoding.Unicode.GetString(b, pos, n * 2);
                pos += n * 2;
                return s;
            }
            public byte[] Rest() { var r = new byte[end - pos]; Buffer.BlockCopy(b, pos, r, 0, r.Length); pos = end; return r; }
        }

        sealed class Out
        {
            readonly MemoryStream m = new MemoryStream();
            public void Raw(byte[] bytes) { if (bytes != null && bytes.Length > 0) m.Write(bytes, 0, bytes.Length); }
            public void Int(int v) => Raw(BitConverter.GetBytes(v));
            public void Long(long v) => Raw(BitConverter.GetBytes(v));
            public void Float(float v) => Raw(BitConverter.GetBytes(v));
            public void Bool(bool v) => m.WriteByte(v ? (byte)1 : (byte)0);
            public void Bools(bool[] a) { a ??= new bool[0]; Int(a.Length); foreach (var v in a) Bool(v); }
            public void Ints(int[] a) { a ??= new int[0]; Int(a.Length); foreach (var v in a) Int(v); }
            public void OptionalInts(int[] a) { if (a == null) { Int(-1); return; } Ints(a); }
            public void Str(string s) { s ??= ""; Int(s.Length); Raw(Encoding.Unicode.GetBytes(s)); }
            public byte[] ToArray() => m.ToArray();
        }
    }
}
