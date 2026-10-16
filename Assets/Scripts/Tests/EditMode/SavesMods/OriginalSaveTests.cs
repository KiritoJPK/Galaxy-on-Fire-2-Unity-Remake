// OriginalSaveTests.cs
// Feature: OriginalSave / OriginalRecord, reading and writing the original Android game's save files.
// Expected: the decompiled RecordHandler (recordStoreWrite 0xdf760 / recordStoreRead, writeMission / writeAgent /
// writeWanted, recordStoreWritePreview, addHash 0xded4c / checkHash 0xde618, AEFile::Write's encodings) worked out by hand:
// the byte offsets of the first fields, a string as its length + UTF-16LE, the signature (SHA-256 over the data, addHash's
// 25-byte salt and the key) checked against values computed outside the game's code (Python's hashlib), and the blueprint
// recipe lengths per economy (Status::resetGame's product order; the community gof2edit tool lists the same numbers). No
// real save file is in the repository yet: these check the format as the code writes it, not a phone's file.

using System.IO;
using System.Linq;
using System.Text;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class OriginalSaveTests : EconomyTest
    {
        static readonly int[] AndroidRecipes = { 6, 6, 6, 6, 2, 4, 6, 5, 6, 4, 9, 5, 6, 7, 9, 9, 9, 6, 4, 4, 5, 10, 5, 4, 2 };
        static readonly int[] DefaultRecipes = { 6, 6, 5, 5, 3, 4, 6, 5, 5, 4, 8, 5, 6, 6, 8, 8, 9, 5, 4, 4, 5, 9, 5, 4, 2 };

        static string Hex(byte[] b, int from, int n) => string.Concat(b.Skip(from).Take(n).Select(x => x.ToString("x2")));

        // ---- signature ----

        [Test]
        public void TheSignatureIsSha256OverDataSaltAndKey()
        {
            var data = Encoding.ASCII.GetBytes("Galaxy on Fire 2");
            var file = OriginalSave.Sign(data);
            Assert.AreEqual(data.Length + 32, file.Length);
            Assert.AreEqual("58322cd8b07eba775e9a5d6d8fd7801e42bebfff4b2847529eb62de9a2b2f119", Hex(file, data.Length, 32));
            Assert.AreEqual("6ff49272ca467274aa228d9b3d8f4c8eb63a7c392545256530fefd7cae13c806",
                            Hex(OriginalSave.Sign(data, "0123456789abcdef"), data.Length, 32), "another device's key");
        }

        [Test]
        public void SignatureValidChecksTheDataAndTheKey()
        {
            var file = OriginalSave.Sign(Encoding.ASCII.GetBytes("Galaxy on Fire 2"));
            Assert.IsTrue(OriginalSave.SignatureValid(file));
            Assert.IsFalse(OriginalSave.SignatureValid(file, "0123456789abcdef"), "a save signed for another key");
            file[3] ^= 1;
            Assert.IsFalse(OriginalSave.SignatureValid(file), "a changed byte");
            Assert.IsFalse(OriginalSave.SignatureValid(new byte[20]), "too short to hold a signature");
        }

        // ---- the layout ----

        [Test]
        public void TheFirstFieldsSitWhereRecordStoreWritePutsThem()
        {
            var r = new OriginalRecord { visitedStations = new bool[135], credits = 123456, rating = 7, playingTimeMs = 0x0102030405L, kills = 9,
                                         campaignMission = 44 };
            r.visitedStations[0] = true;
            var b = OriginalSave.Write(r, AndroidRecipes);
            // Galaxy::getVisited: the count 0x87 and 135 bools; then credits, rating, the playing time (8 bytes), kills ...
            Assert.AreEqual(135, System.BitConverter.ToInt32(b, 0));
            Assert.AreEqual(1, b[4]);
            Assert.AreEqual(123456, System.BitConverter.ToInt32(b, 139));
            Assert.AreEqual(7, System.BitConverter.ToInt32(b, 143));
            Assert.AreEqual(0x0102030405L, System.BitConverter.ToInt64(b, 147));
            Assert.AreEqual(9, System.BitConverter.ToInt32(b, 155));
            Assert.AreEqual(44, System.BitConverter.ToInt32(b, 179), "getCurrentCampaignMission, after 6 more counters");
            Assert.AreEqual(-1, System.BitConverter.ToInt32(b, 183), "an empty freelance mission is its type -1 alone");
            Assert.AreEqual(-1, System.BitConverter.ToInt32(b, 187), "and the empty story mission");
            Assert.IsTrue(OriginalSave.SignatureValid(b));
        }

        [Test]
        public void AStringIsItsLengthAndUtf16()
        {
            var r = new OriginalRecord { visitedStations = new bool[0] };
            r.storyMission = new OriginalMission { type = 3, clientName = "Kß" };
            var b = OriginalSave.Write(r, AndroidRecipes);
            // 0 visited (4 bytes), 9 ints + 1 long (44), the empty freelance mission (4), the story mission's type (4).
            int at = 4 + 44 + 4;
            Assert.AreEqual(3, System.BitConverter.ToInt32(b, at));
            Assert.AreEqual(2, System.BitConverter.ToInt32(b, at + 4), "two characters");
            Assert.AreEqual("4b00df00", Hex(b, at + 8, 4), "UTF-16LE");
        }

        // ---- round trips ----

        static OriginalAgent Agent(int index, string name) => new OriginalAgent
        {
            costs = 100, sellSystem = 3, sellBlueprint = -1, eventState = 1, index = index, offer = 2, race = 1, sellItem = 41,
            sellItemPrice = 900, sellItemAmount = 4, station = 12, system = 2, wingmanFriends = 0, male = true, hasReward = false,
            acceptedOffer = true, flag1c = true, imageParts = new[] { 1, 2, 3, 4, 5 }, sellMod = 3, missionText = "Bring it",
            name = name, stationName = "Dis", systemName = "Ginoya", text8 = "", textC = "Ok",
        };

        static OriginalRecord Rich(int[] recipes)
        {
            var r = new OriginalRecord
            {
                visitedStations = Enumerable.Range(0, 135).Select(i => i % 3 == 0).ToArray(),
                credits = 2139166805, rating = 4, playingTimeMs = 123456789012L, kills = 321, missionCount = 12, level = 9, lastXp = 77,
                goodsProduced = 3, stationsVisited = 40, campaignMission = 120,
                jumpgatesUsed = 5, capturedCrates = 6, boughtEquipment = 7, pirateKills = 8, status80 = 70, status7c = 13, voidReturnStation = 98,
                status88 = 2, oreTypesMined = new[] { true, false, true }, coreTypesMined = new[] { false }, courierGoods = 1, oresMined = 900,
                coresMined = 3, boozeBought = 44, boozeTypes = Enumerable.Repeat(true, 22).ToArray(), spaceJunk = 2, statusB4 = new[] { true },
                passengersCarried = 30, cloakMs = 600000, statusC8 = Enumerable.Range(1, 10).ToArray(), medals = Enumerable.Range(0, 45).Select(i => i % 4).ToArray(),
                ship = 37, shipRace = 0, standings = new[] { 40, -12 }, passengers = 2, systemsVisible = Enumerable.Repeat(true, 34).ToArray(),
                status40 = new[] { 1, 2 }, status3c = new[] { 3 }, status48 = new int[0], status44 = new[] { 4, 5, 6 }, status4c = new[] { false, true },
                difficulty = 0.5f, status100 = 987654321L, storyTargets = new[] { 55, 66 }, status10c = 1, status110 = true, kaamoState = 3,
                status111 = true, options35 = true, itemsObtained = Enumerable.Range(0, 233).Select(i => i % 2 == 0).ToArray(),
                extended = true, shipMods = new[] { 0, 1 }, collectedBounties = new[] { 1, 2, 3, 4 }, options37 = true, status174 = 9,
                status58 = new[] { true, true }, status118 = 11,
            };
            for (int h = 8; h < r.hints.Length; h += 3) r.hints[h] = true;
            r.freelanceMission = new OriginalMission
            {
                type = 6, clientName = "Mkkt Bkkt", targetName = "Pal Tyyrt", targetStationName = "Kalun Amir", targetSystemName = "Paréah",
                campaign = false, clientImage = new[] { 9, 8, 7, 6, 5 }, clientRace = 2, costs = 0, bonus = 1500, reward = 30000,
                targetStation = 96, difficulty = 3, goodIndex = -1, goodAmount = 0, status = 1, visible = true,
                agent = Agent(7, "Client"),   // its own mission isn't written again (-1)
            };
            r.storyMission = new OriginalMission { type = 0xab, clientName = "Keith", campaign = true, visible = true };
            r.equipment.Add(new OriginalEquipment { item = 12, amount = 1 });
            r.equipment.Add(new OriginalEquipment());   // an empty slot
            r.equipment.Add(new OriginalEquipment { item = 46, amount = 20, unsaleable = true });
            r.cargo.Add(new OriginalItem { item = 122, amount = 40, price = 1450 });
            r.stations = new OriginalStation[4];
            r.stations[0] = null;
            r.stations[1] = new OriginalStation
            {
                index = 120, items = { new OriginalItem { item = 0, amount = 3, price = 2262 } },
                ships = { new OriginalShip { ship = 49, race = 10, mods = new[] { 2 } }, new OriginalShip { ship = 44, race = 10 } },
                agents = { Agent(21, "Mechanic"), Agent(3, "Visitor") }, attackedFriends = true,
            };
            r.stations[1].agents[1].mission = new OriginalMission { type = 2, clientName = "Visitor", reward = 100 };
            r.stations[2] = new OriginalStation { index = 70 };
            r.stations[3] = new OriginalStation { index = 78, ships = { new OriginalShip { ship = 0, race = 0 } } };
            for (int i = 0; i < 3; i++)
                r.blueprints.Add(new OriginalBlueprint { invested = Enumerable.Range(1, recipes[i]).ToArray(), field4 = i, unlocked = i == 1, fieldC = 2,
                                                         station = i == 1 ? 70 : -1, stationName = i == 1 ? "Dis" : "" });
            r.pendingProducts.Add(new OriginalPendingProduct { a = 85, b = 1, c = 70, name = "Khador Drive" });
            r.wingmen = new OriginalWingmen { names = { "Ace", "Bob" }, status2c = 600, status30 = 3, status28 = new[] { 1, 0, 0, 0, 2 } };
            r.agents.Add(Agent(4, "Lounge"));
            r.parkedShip = new OriginalParkedShip { ship = 10, race = 0, equipment = { new OriginalEquipment { item = 0, amount = 1 } },
                                                    cargo = { new OriginalItem { item = 22, amount = 5, price = 30 } } };
            r.parkedShipMods = new[] { 1 };
            r.kaamoItems.Add(new OriginalItem { item = 50, amount = 1, price = 9000 });
            r.kaamoShips.Add(new OriginalShip { ship = 8, race = 9, mods = new[] { 0, 0, 3 } });
            r.wanted.Add(new OriginalWanted { active = true, location = 33, travelsTo = 34, lastSeen = 3, name = "Pal Tyyrt", index = 0, board = 0,
                                              race = 8, male = true, ship = 45, weapon = 7, hitpoints = 900, loot = 132, lootAmount = 3,
                                              reward = 50000, requiredBounties = 0, requiredMission = 128, wingmen = 2,
                                              imageParts = new[] { 5, 4, 3, 2, 1 } });
            return r;
        }

        [Test]
        public void ARecordWrittenAndReadAgainIsByteForByteTheSame()
        {
            var first = OriginalSave.Write(Rich(AndroidRecipes), AndroidRecipes);
            var read = OriginalSave.Read(first, AndroidRecipes);
            var again = OriginalSave.Write(read, AndroidRecipes);
            CollectionAssert.AreEqual(first, again);
            Assert.IsEmpty(read.trailing, "every byte belongs to a known field");
        }

        [Test]
        public void TheReadRecordHasTheWrittenValues()
        {
            var r = OriginalSave.Read(OriginalSave.Write(Rich(AndroidRecipes), AndroidRecipes), AndroidRecipes);
            Assert.AreEqual(2139166805, r.credits);
            Assert.AreEqual(123456789012L, r.playingTimeMs);
            Assert.AreEqual("Paréah", r.freelanceMission.targetSystemName);
            Assert.AreEqual("Client", r.freelanceMission.agent.name);
            Assert.IsNull(r.freelanceMission.agent.mission);
            Assert.AreEqual(0xab, r.storyMission.type);
            Assert.AreEqual(new[] { 12, -1, 46 }, r.equipment.Select(e => e.item).ToArray());
            Assert.IsTrue(r.equipment[2].unsaleable);
            Assert.IsNull(r.stations[0]);
            Assert.AreEqual(120, r.stations[1].index);
            Assert.AreEqual(new[] { 49, 44 }, r.stations[1].ships.Select(s => s.ship).ToArray());
            Assert.AreEqual(new[] { 2 }, r.stations[1].ships[0].mods);
            Assert.AreEqual(3, r.stations[1].agents[0].sellMod, "a Kaamo mechanic (index 21) has its mod");
            Assert.AreEqual(100, r.stations[1].agents[1].mission.reward);
            Assert.AreEqual(78, r.stations[3].index, "the current station last");
            Assert.AreEqual(new[] { 40, -12 }, r.standings);
            Assert.AreEqual(AndroidRecipes[2], r.blueprints[2].invested.Length);
            Assert.AreEqual("Dis", r.blueprints[1].stationName);
            Assert.AreEqual("Khador Drive", r.pendingProducts[0].name);
            Assert.AreEqual(new[] { "Ace", "Bob" }, r.wingmen.names.ToArray());
            Assert.AreEqual(10, r.parkedShip.ship);
            Assert.AreEqual(3, r.kaamoState);
            Assert.AreEqual(new[] { 0, 0, 3 }, r.kaamoShips[0].mods);
            Assert.IsTrue(r.extended);
            Assert.AreEqual("Pal Tyyrt", r.wanted[0].name);
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, r.collectedBounties);
            Assert.AreEqual(11, r.status118);
            Assert.IsTrue(r.hints[8] && r.hints[0x3a - (0x3a - 8) % 3] && !r.hints[9]);
            Assert.AreEqual(0.5f, r.difficulty);
        }

        [Test]
        public void ARecordWithoutTheMarkerEndsAfterTheItemsObtained()
        {
            var rich = Rich(AndroidRecipes);
            rich.extended = false;
            var file = OriginalSave.Write(rich, AndroidRecipes);
            var r = OriginalSave.Read(file, AndroidRecipes);
            Assert.IsFalse(r.extended);
            Assert.IsEmpty(r.wanted);
            CollectionAssert.AreEqual(file, OriginalSave.Write(r, AndroidRecipes));
            Assert.Less(file.Length, OriginalSave.Write(Rich(AndroidRecipes), AndroidRecipes).Length);
        }

        [Test]
        public void TheDefaultEconomyRecordsItsOwnRecipeLengths()
        {
            var file = OriginalSave.Write(Rich(DefaultRecipes), DefaultRecipes);
            CollectionAssert.AreEqual(file, OriginalSave.Write(OriginalSave.Read(file, DefaultRecipes), DefaultRecipes));
            // Read with the other economy's lengths, the stream goes out of step (blueprint 2 has 6 amounts, not 5).
            bool same;
            try { same = file.SequenceEqual(OriginalSave.Write(OriginalSave.Read(file, AndroidRecipes), AndroidRecipes)); }
            catch (System.Exception) { same = false; }
            Assert.IsFalse(same, "the economy must be known to read a save");
        }

        [Test]
        public void IngredientCountsFollowTheEconomy()
        {
            Session.Economy = Economy.Android;
            CollectionAssert.AreEqual(AndroidRecipes, OriginalSave.IngredientCounts(Database.Load()));
            Session.Economy = Economy.Default;
            CollectionAssert.AreEqual(DefaultRecipes, OriginalSave.IngredientCounts(Database.Load()));
        }

        [Test]
        public void BrokenFilesAreRefused()
        {
            var file = OriginalSave.Write(Rich(AndroidRecipes), AndroidRecipes);
            Assert.Throws<InvalidDataException>(() => OriginalSave.Read(file.Take(400).ToArray(), AndroidRecipes), "cut short");
            var huge = (byte[])file.Clone();
            System.BitConverter.GetBytes(int.MaxValue).CopyTo(huge, 0);   // the visited count
            Assert.Throws<InvalidDataException>(() => OriginalSave.Read(huge, AndroidRecipes), "a corrupt count");
            Assert.Throws<InvalidDataException>(() => OriginalSave.Read(new byte[10], AndroidRecipes), "too short");
        }

        [Test]
        public void ThePcVersionsRecordsAreUnsigned()
        {
            // The PC Full HD version's records end in their last field (a downloaded 2016 save read and wrote back byte for
            // byte this way; it isn't in the repository).
            var rich = Rich(DefaultRecipes);
            rich.extended = false;
            var file = OriginalSave.Write(rich, DefaultRecipes, null);
            CollectionAssert.AreEqual(OriginalSave.Write(rich, DefaultRecipes).Take(file.Length).ToArray(), file, "the same stream, no signature");
            var r = OriginalSave.Read(file, DefaultRecipes, signed: false);
            Assert.IsEmpty(r.trailing);
            CollectionAssert.AreEqual(file, OriginalSave.Write(r, DefaultRecipes, null));
            Assert.IsFalse(OriginalSave.SignatureValid(file));
        }

        [Test]
        public void ThePreviewRoundTrips()
        {
            var p = new OriginalPreview { playingTimeMs = 3600000, credits = 50000, station = "Var Hastra", system = "Mido", campaignMission = 3,
                                          level = 2, difficulty = 1.5f, ship = 10 };
            var b = OriginalSave.WritePreview(p);
            Assert.AreEqual(8 + 4 + 4 + 20 + 4 + 8 + 4 + 4 + 4 + 4, b.Length, "no signature");
            var q = OriginalSave.ReadPreview(b);
            Assert.AreEqual(("Var Hastra", "Mido", 50000, 1.5f, 10), (q.station, q.system, q.credits, q.difficulty, q.ship));
        }
    }
}
