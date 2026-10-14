// StoryAdvanceTests.cs
// Feature: CLAUDE.md "Story" (Advance = Status::nextCampaignMission, StartCampaign, the step side effects), "Valkyrie add-on
// (45-84)" and "Supernova add-on (84-162)".
// Expected values: Reference/research/campaign_flow.md §1.3 (52 -> 54, 128 -> 130; cases 45 / 106 keep the mission), §1.4 (the
// campaign starts), §4 (the step table: type and target per index, the effects column), §8 (rewards: +40 000 at 44 -> 45);
// wingmen_wanted.md §1.1 / §1.8 (case 47 ends the wingmen's contract); CLAUDE.md "Story" / "Valkyrie add-on" for the loaner
// ships' loadouts and the Supernova items. Item numbers per items.json (90 Gunant's Drill, 86 IMT Extract 1.3, 164 Void
// Crystals, 175 Void Essence, 85 Khador Drive, 179 Liberator, 183 Disruptor, 206 Gamma Shield II, 210 Chromo Plasma).

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class StoryAdvanceTests : StoryAreaTestBase
    {
        /// <summary>campaign_flow.md §4: index, type (-1 empty, -2 no creating case), target station (-2 = none given).</summary>
        static readonly int[,] StepTable =
        {
            {0,0x04,78}, {1,0x0b,78}, {2,0x9a,78}, {3,0x0b,78}, {4,0x9a,78}, {5,0x0b,78}, {6,0x9e,78}, {7,0x04,78}, {8,0x0b,78},
            {9,0x0b,78}, {10,0x0b,79}, {11,0x0b,76}, {12,0x0b,79}, {13,0x96,-2}, {14,0x04,79}, {15,0x0b,98}, {16,0x04,98},
            {17,0x0b,98}, {18,0x9c,56}, {19,0x9c,55}, {20,0xbd,55}, {21,0x04,55}, {22,0x0b,55}, {23,0x0b,10}, {24,0x04,48},
            {25,0x9c,-1}, {26,0x04,48}, {27,0x0b,10}, {28,0x04,91}, {29,0x04,-1}, {30,0x9c,91}, {31,0x0b,98}, {32,0x0b,10},
            {33,0x08,10}, {34,0x0b,30}, {35,0x0b,29}, {36,0x0c,27}, {37,0xa0,27}, {38,0x04,22}, {39,0x0b,30}, {40,0xa1,-1},
            {41,0x04,-1}, {42,0xa0,-1}, {43,0x0b,10}, {44,0x0b,10}, {45,-1,-2}, {46,-2,-2}, {47,0x0b,74}, {48,0x0b,58},
            {49,0x9c,58}, {50,0x9c,62}, {51,0x9c,25}, {52,0xa0,25}, {54,0x0b,74}, {55,0x0b,101}, {56,0x04,102}, {57,0x0b,101},
            {58,0xa6,101}, {59,0xa3,101}, {60,0x0b,101}, {61,0xa4,101}, {62,0x0b,100}, {63,0x04,103}, {64,0x04,104},
            {65,0x0b,100}, {66,0x0b,101}, {67,0x04,104}, {68,0x08,66}, {69,0x06,66}, {70,0x06,65}, {71,0x0b,66}, {72,0xa4,101},
            {73,0x0a,81}, {74,0x0b,100}, {75,0x0b,100}, {76,0x0b,100}, {77,0x0b,101}, {78,0x04,101}, {79,0xa5,-1},
            {80,0x01,100}, {81,0x04,-1}, {82,0x0b,100}, {83,0x0b,100}, {84,-1,-2}, {85,0xa4,-2}, {86,0x0b,100}, {87,0x04,10},
            {88,0x0b,10}, {89,0x04,109}, {90,0x0b,10}, {91,0xb8,110}, {92,0xb8,113}, {93,0x0b,114}, {94,0xb8,111},
            {95,0xaa,10}, {96,0x0b,98}, {97,0x04,85}, {98,0x0b,120}, {99,0xaa,10}, {100,0x04,98}, {101,0x0b,98},
            {102,0xb8,113}, {103,0x0b,10}, {104,0xa6,10}, {105,0x04,109}, {106,0x04,111}, {107,-2,-2}, {108,0x0b,10},
            {109,0xaa,114}, {110,0xab,10}, {111,0xab,38}, {112,0xac,38}, {113,0xab,82}, {114,0x04,83}, {115,0xab,82},
            {116,0xab,93}, {117,0x0b,126}, {118,0x08,126}, {119,0xaa,10}, {120,0x04,40}, {121,0x08,93}, {122,0x0b,10},
            {123,0x04,121}, {124,0x0b,121}, {125,0x04,55}, {126,0xaa,120}, {127,0x0b,98}, {128,-1,-2}, {130,-1,-2},
            {131,0x04,112}, {132,0x0b,112}, {133,0xaa,120}, {134,0x0b,22}, {135,0xae,103}, {136,0x0b,112}, {137,0x04,58},
            {138,0x0b,58}, {139,0xa8,131}, {140,0x0b,112}, {141,0x0b,78}, {142,0x04,79}, {143,0x08,112}, {144,0xaa,112},
            {145,0x04,112}, {146,0x0b,112}, {147,0x04,-1}, {148,0xab,96}, {149,-2,-2}, {150,-2,-2}, {151,0xab,96}, {152,0xa5,-1}, {153,0x0b,98},
            {154,0x04,-1}, {155,0xa4,-2}, {156,0x0b,99}, {157,0x04,112}, {158,0x04,111}, {159,0x0b,10}, {160,0xaa,10},
            {161,0xaa,93}, {162,-1,-2},
        };

        static Dictionary<int, (int type, int station)> Table()
        {
            var d = new Dictionary<int, (int, int)>();
            for (int r = 0; r < StepTable.GetLength(0); r++) d[StepTable[r, 0]] = (StepTable[r, 1], StepTable[r, 2]);
            return d;
        }

        // ---- the whole walk ----------------------------------------------------------------------------------

        [Test]
        public void AdvanceWalksEveryStepFromThePrologueToTheEnd()
        {
            AtStep(0);
            var seen = new List<int> { 0 };
            for (int guard = 0; guard < 200 && Story.Index < Story.LastIndex; guard++) seen.Add(Story.Advance(Db));
            Assert.AreEqual(Story.LastIndex, Story.Index);
            CollectionAssert.DoesNotContain(seen, 53, "53 is never current (case 52 runs twice)");
            CollectionAssert.DoesNotContain(seen, 129, "129 is never current (case 128 runs twice)");
            Assert.AreEqual(161, seen.Count, "163 indices minus 53 and 129");
            CollectionAssert.AreEqual(seen.OrderBy(i => i).ToList(), seen, "strictly increasing");
            Assert.AreEqual(seen.Count, seen.Distinct().Count());
        }

        [Test]
        public void EachStepGetsTheResearchTablesMissionOnEntering()
        {
            var table = Table();
            AtStep(0);
            Assert.AreEqual(0x04, Story.Mission.type);
            Assert.AreEqual(78, Story.Mission.station, "Status::resetGame: Mission(4, 0, 78)");
            while (Story.Index < Story.LastIndex)
            {
                int n = Story.Advance(Db);
                Assert.IsTrue(table.ContainsKey(n), "index " + n + " is in the research table");
                var (type, station) = table[n];
                if (type == -2) continue;   // no creating case: checked separately
                Assert.AreEqual(type, Story.Mission.type, "type of step " + n);
                if (station != -2) Assert.AreEqual(station, Story.Mission.station, "target of step " + n);
                if (type == -1) Assert.IsTrue(Story.Mission.IsEmpty, "empty mission at " + n);
            }
        }

        [Test]
        public void SkippedIndices()
        {
            Assert.AreEqual(54, EnterStep(53), "52 -> 54");
            Assert.AreEqual(54, Session.CampaignMission);
            Assert.AreEqual(130, EnterStep(129), "128 -> 130");
            Assert.AreEqual(130, Session.CampaignMission);
        }

        [TestCase(45)]
        [TestCase(84)]
        [TestCase(128)]
        [TestCase(130)]
        [TestCase(162)]
        public void EmptyHiddenMissionSteps(int n)
        {
            if (n == 130) { AtStep(128); Story.Advance(Db); } else EnterStep(n);
            Assert.AreEqual(n, Story.Index);
            Assert.IsTrue(Story.Mission.IsEmpty);
            Assert.IsFalse(StoryTable.Step(n).visible, "hidden (campaign_flow.md §4)");
        }

        [Test]
        public void Step46KeepsTheEmptyMissionOf45()
        {
            AtStep(45);
            var m = Story.Mission;
            Story.Advance(Db);
            Assert.AreEqual(46, Story.Index);
            Assert.AreSame(m, Story.Mission, "no case 45: the campaign mission is unchanged");
        }

        [Test]
        public void Step107KeepsStep106sLevelMission()
        {
            AtStep(106);
            Story.Advance(Db);
            Assert.AreEqual(107, Story.Index);
            Assert.AreEqual(0x04, Story.Mission.type, "no case 106: 106's mission stays");
            Assert.AreEqual(111, Story.Mission.station);
        }

        [TestCase(93)]
        [TestCase(111)]
        [TestCase(143)]
        public void StoryRadioFlagOnEntering(int n)
        {
            Session.StoryRadioPending = false;
            EnterStep(n);
            Assert.IsTrue(Session.StoryRadioPending, "Status+0x178 at 93 / 111 / 143");
        }

        [TestCase(92)]
        [TestCase(110)]
        [TestCase(142)]
        [TestCase(144)]
        public void NoStoryRadioFlagElsewhere(int n)
        {
            Session.StoryRadioPending = false;
            EnterStep(n);
            Assert.IsFalse(Session.StoryRadioPending);
        }

        [Test]
        public void AdvanceRestartsTheStepClock()
        {
            Session.PlaySeconds = 500f;
            EnterStep(30);
            Assert.That(Session.StoryStepStart, Is.GreaterThanOrEqualTo(500f).And.LessThan(510f), "Status+0x100 = playing time");
        }

        [Test]
        public void MilestonePredicates()
        {
            AtStep(44); Assert.IsFalse(Story.GameWon);
            AtStep(45); Assert.IsTrue(Story.GameWon, "gameWon = index > 44");
            AtStep(83); Assert.IsFalse(Story.Dlc1Won);
            AtStep(84); Assert.IsTrue(Story.Dlc1Won, "dlc1Won = index > 83");
        }

        // ---- campaign starts (campaign_flow.md 1.4) -----------------------------------------------------------

        [Test]
        public void MainCampaignStartsWithThePrologueInFlight()
        {
            string scene = Story.StartCampaign(Db, Campaign.GalaxyOnFire2);
            Assert.AreEqual("Space", scene);
            Assert.AreEqual(0, Story.Index);
            Assert.AreEqual(78, Session.StationIndex);
            Assert.AreEqual(10, Session.ShipIndex, "the Phantom");
            Assert.AreEqual(0x04, Story.Mission.type);
            Assert.AreEqual(0, Session.Credits);
        }

        [Test]
        public void ValkyrieStartsDockedAtDimaInTheInflict()
        {
            string scene = Story.StartCampaign(Db, Campaign.Valkyrie);
            Assert.AreEqual("Station", scene);
            Assert.AreEqual(45, Story.Index);
            Assert.AreEqual(5, Session.ShipIndex, "5 Inflict");
            Assert.AreEqual(91, Session.StationIndex, "91 Dima");
            Assert.AreEqual(197, Session.Kills);
            Assert.AreEqual(40000, Session.Credits, "only 44 -> 45 credits anything");
            Assert.IsTrue(Visible(6) && Visible(25), "systems 6 and 25 visible");
            foreach (int item in new[] { 2, 5, 36, 81, 51, 86, 85 })
                Assert.IsTrue(Session.Equipment.Exists(e => e.item == item), "mounted " + item);
            Assert.AreEqual(10, Session.Equipment.Find(e => e.item == 36).amount, "36 x10");
            Assert.AreEqual(3, Session.Medals[23], "medal 23 bronze");
            Assert.AreEqual(1, Session.Medals[30], "medal 30 gold");
            Assert.IsTrue(Session.Hints.Contains(8) && Session.Hints.Contains(0x38), "the tutorial hints shown");
            Assert.IsFalse(Session.Hints.Contains(0x31), "Supernova hints only for Supernova");
        }

        [Test]
        public void SupernovaStartsDockedAtDisInTheCrossXt()
        {
            string scene = Story.StartCampaign(Db, Campaign.Supernova);
            Assert.AreEqual("Station", scene);
            Assert.AreEqual(84, Story.Index);
            Assert.AreEqual(30, Session.ShipIndex, "30 Berger CrossXT");
            Assert.AreEqual(70, Session.StationIndex, "70 Dis");
            Assert.AreEqual(386, Session.Kills);
            Assert.AreEqual(8, Shop.CargoOf(GalaxyMap.EnergyCellItem), "8 Energy Cells");
            Assert.AreEqual(1, Session.Cargo.Count, "nothing else in the hold");
            foreach (int item in new[] { 176, 20, 36, 44, 81, 51, 68, 86, 85, 56 })
                Assert.IsTrue(Session.Equipment.Exists(e => e.item == item), "mounted " + item);
            Assert.IsTrue(Session.Hints.Contains(0x26) && Session.Hints.Contains(0x31) && Session.Hints.Contains(0x39));
            Assert.IsFalse(Session.Unsaleable.Contains(GalaxyMap.KhadorDriveItem), "dlc1Won: the drive saleable");
        }

        // ---- side effects, main campaign ----------------------------------------------------------------------

        [TestCase(4)]
        [TestCase(6)]
        public void CargoClearedOnEntering(int n)
        {
            Shop.AddToCargo(122, 7);
            EnterStep(n);
            Assert.AreEqual(0, Session.Cargo.Count);
        }

        [Test]
        public void Step8MakesEverythingSaleable()
        {
            Session.Unsaleable.Add(90); Session.Unsaleable.Add(81);
            EnterStep(8);
            Assert.AreEqual(0, Session.Unsaleable.Count);
        }

        [Test]
        public void Step10SwapsGunantsDrillForTheImtExtract()
        {
            Session.Equipment = new List<ItemStack> { new ItemStack(90, 1), new ItemStack(81, 1) };
            Shop.AddToCargo(90, 1);
            Session.Unsaleable.Add(90);
            EnterStep(10);
            Assert.IsFalse(Session.Equipment.Exists(e => e.item == 90), "mounted drill replaced");
            Assert.IsTrue(Session.Equipment.Exists(e => e.item == 86), "IMT Extract 1.3 mounted");
            Assert.IsTrue(Session.Equipment.Exists(e => e.item == 81), "the scanner stays");
            Assert.AreEqual(0, Shop.CargoOf(90), "#58: the drill in the hold too");
            Assert.AreEqual(1, Shop.CargoOf(86));
            Assert.IsFalse(Session.Unsaleable.Contains(90));
        }

        [Test]
        public void Step13NeedsOneMoreFreelanceMission()
        {
            Session.FreelanceCompleted = 4;
            EnterStep(13);
            Assert.AreEqual(5, Story.Mission.value, "statusValue = current count + 1");
            Assert.IsFalse(Story.IsComplete(Db, new StoryContext { docked = true, station = 79 }));
            Session.FreelanceCompleted = 5;
            Assert.IsTrue(Story.IsComplete(Db, new StoryContext { docked = true, station = 79 }));
        }

        [TestCase(23, 6)]
        [TestCase(62, 22)]
        [TestCase(63, 24)]
        [TestCase(98, 29)]
        [TestCase(117, 30)]
        [TestCase(139, 31)]
        public void StepRevealsSystem(int n, int system)
        {
            Assert.IsFalse(Visible(system), "hidden at a new game");
            EnterStep(n);
            Assert.IsTrue(Visible(system));
        }

        [Test]
        public void VoidInvasionBookkeeping()
        {
            EnterStep(24);
            Assert.AreEqual(9, Session.VoidInvasionSystem); Assert.AreEqual(48, Session.VoidInvasionStation);
            EnterStep(26);
            Assert.AreEqual(-1, Session.VoidInvasionSystem); Assert.AreEqual(-1, Session.VoidInvasionStation);
            EnterStep(28);
            Assert.AreEqual(18, Session.VoidInvasionSystem); Assert.AreEqual(91, Session.VoidInvasionStation);
            EnterStep(42);
            Assert.AreEqual(-10, Session.VoidInvasionSystem); Assert.AreEqual(-10, Session.VoidInvasionStation);
        }

        [Test]
        public void Step25MakesTheAlienRemainsUnsaleable()
        {
            EnterStep(25);
            Assert.IsTrue(Session.Unsaleable.Contains(131));
        }

        [Test]
        public void Step34TakesTheVoidCrystalsAndUnlocksTheKhadorBlueprint()
        {
            Shop.AddToCargo(164, 60);
            EnterStep(34);
            Assert.AreEqual(10, Shop.CargoOf(164), "50 Void Crystals handed over");
            Assert.IsTrue(Session.UnlockedBlueprints.Contains(85), "Khador Drive blueprint");
        }

        [Test]
        public void GameWonPaysFortyThousand()
        {
            Session.Credits = 1000;
            EnterStep(45);
            Assert.AreEqual(41000, Session.Credits, "+40 000 from 44 -> 45");
            Assert.AreEqual(-10, Session.VoidInvasionStation);
        }

        // ---- Valkyrie: the loaner ships (Status+0x8c) --------------------------------------------------------

        [Test]
        public void ValkyrieLoanersParkAndRestoreTheOwnShip()
        {
            AtStep(47);
            Session.ShipIndex = 10;
            Session.Equipment = new List<ItemStack> { new ItemStack(2, 1), new ItemStack(55, 1) };
            Session.Cargo = new List<ItemStack> { new ItemStack(122, 5) };
            Session.ShipMods = new List<int> { 1 };
            Session.PlayerHull = 40;

            Assert.AreEqual(48, Story.Advance(Db));
            Assert.AreEqual(9, Session.ShipIndex, "Taret Orskk's H'Soc");
            CollectionAssert.AreEquivalent(new[] { 58, 83 }, Session.Equipment.Select(e => e.item));
            Assert.AreEqual(0, Session.Cargo.Count, "a loaner comes with an empty hold");
            Assert.AreEqual(-1, Session.PlayerHull, "a fresh hull");
            Assert.IsNotNull(Session.ParkedShip);
            Assert.AreEqual(10, Session.ParkedShip.ship);
            Assert.AreEqual(5, Session.ParkedShip.cargo.Find(c => c.item == 122).amount);
            Assert.AreEqual(40, Session.ParkedShip.hull);
            Assert.IsFalse(Story.HangarUnlocked, "no hangar in a loaner");

            Assert.AreEqual(49, Story.Advance(Db));
            Assert.AreEqual(41, Session.ShipIndex, "the stolen K'Suukk");
            CollectionAssert.AreEquivalent(new[] { 177, 177, 177, 58, 83, 52 }, Session.Equipment.Select(e => e.item));
            Assert.AreEqual(10, Session.ParkedShip.ship, "still the own ship parked");

            Story.Advance(Db); Story.Advance(Db); Story.Advance(Db);   // 50, 51, 52
            Assert.AreEqual(54, Story.Advance(Db));
            Assert.AreEqual(41, Session.ShipIndex);
            Assert.AreEqual(55, Story.Advance(Db));
            Assert.AreEqual(10, Session.ShipIndex, "own ship back at 55");
            Assert.IsNull(Session.ParkedShip);
            Assert.AreEqual(5, Shop.CargoOf(122), "with its hold");
            CollectionAssert.AreEqual(new[] { 1 }, Session.ShipMods, "and its mods");
            Assert.AreEqual(40, Session.PlayerHull);
            Assert.IsTrue(Visible(23), "Herjaza");

            Assert.AreEqual(56, Story.Advance(Db));
            Assert.AreEqual(39, Session.ShipIndex, "the S'Kanarr for the turret test");
            CollectionAssert.AreEquivalent(new[] { 181, 52, 58, 83 }, Session.Equipment.Select(e => e.item));
            Assert.AreEqual(10, Session.ParkedShip.ship);

            Story.Advance(Db);   // 57
            Assert.AreEqual(58, Story.Advance(Db));
            Assert.AreEqual(10, Session.ShipIndex, "own ship back at 58");
            Assert.IsNull(Session.ParkedShip);
            Assert.IsTrue(Session.UnlockedBlueprints.Contains(179), "Liberator blueprint");
        }

        [Test]
        public void Case47EndsTheWingmensContract()
        {
            // wingmen_wanted.md 1.1 / 1.8: nextCampaignMission case 47 (entering 48, the loaner Vossk ship) sets Status+0x30 = 0,
            // so the wingmen are dismissed at the next docking.
            Session.Wingmen = new List<string> { "A", "B" };
            Session.WingmanContractMs = 300000f;
            EnterStep(48);
            Assert.AreEqual(0f, Session.WingmanContractMs, "contract time 0");
            Assert.IsTrue(Wingmen.Expired);
        }

        [Test]
        public void Step59SetsTheConvoyTargets()
        {
            EnterStep(59);
            CollectionAssert.AreEqual(new[] { 56, 45, 22 }, Session.StoryTargets);
            Assert.AreEqual(0, Story.Mission.value);
            Assert.AreEqual(StoryType.TargetList, Story.Mission.type);
        }

        [Test]
        public void Step59KeepsTheLiberatorBlueprintUnlocked()
        {
            // The original's BluePrint::lock(179) at case 0x3a writes the unlocked flag like unlock (CLAUDE.md "Valkyrie add-on").
            Session.UnlockedBlueprints.Add(179);
            EnterStep(59);
            Assert.IsTrue(Session.UnlockedBlueprints.Contains(179));
        }

        [TestCase(0, 50000, 0)]
        [TestCase(1, 100000, 1)]
        [TestCase(3, 200000, 1)]
        public void Step60RewardCountsTheLiberatorKills(int kills, int reward, int value)
        {
            AtStep(59);
            Story.Mission.value = kills;
            Story.Advance(Db);
            Assert.AreEqual(60, Story.Index);
            Assert.AreEqual(reward, Story.Mission.reward, "50 000 + 50 000 x v");
            Assert.AreEqual(value, Story.Mission.value);
        }

        [Test]
        public void Step67StocksCorneliusMinesAtTheCurrentStation()
        {
            Session.StationIndex = 101;
            var stock = new StationStock { station = 101 };
            stock.items.Add(new ItemStack(60, 2));
            Session.RecentStations.Add(stock);
            EnterStep(67);
            Assert.AreEqual(7, stock.items.Find(s => s.item == 60).amount, "AMR Saber +5");
            Assert.AreEqual(5, stock.items.Find(s => s.item == 61).amount, "Neutha EMP");
            Assert.AreEqual(5, stock.items.Find(s => s.item == 62).amount, "Ksann'k");
        }

        [Test]
        public void Step69TakesTheVoidEssence()
        {
            Shop.AddToCargo(175, 2);
            EnterStep(69);
            Assert.AreEqual(0, Shop.CargoOf(175));
        }

        [Test]
        public void Step72UnlocksTheDisruptor()
        {
            EnterStep(72);
            Assert.IsTrue(Session.UnlockedBlueprints.Contains(183));
        }

        [Test]
        public void Step72GivesOneVoidEssence()
        {
            // campaign_flow.md §4 (72: "+1 Void Essence") and CLAUDE.md ("case 0x47 gives one more with the Disruptor
            // blueprint"): one. Story.ApplyStepEffects adds one and Blueprints.UnlockFromStory(72) adds another.
            EnterStep(72);
            Assert.AreEqual(1, Shop.CargoOf(175));
        }

        [Test]
        public void Step75ClearsTheDealerAtTheCurrentStation()
        {
            Session.StationIndex = 100;
            var stock = new StationStock { station = 100, ships = new List<int> { 5, 9 } };
            Session.RecentStations.Add(stock);
            EnterStep(75);
            Assert.AreEqual(0, stock.ships.Count, "Station::removeShips");
        }

        [Test]
        public void JumpDriveUnsaleableAt77TakenAt78ReturnedAt84()
        {
            Session.Equipment.Add(new ItemStack(GalaxyMap.KhadorDriveItem, 1));
            EnterStep(77);
            Assert.IsTrue(Session.Unsaleable.Contains(GalaxyMap.KhadorDriveItem), "77: the jump drive can't be sold");

            Story.Advance(Db);   // 78: imprisoned
            Assert.IsFalse(Session.Equipment.Exists(e => e.item == GalaxyMap.KhadorDriveItem), "taken");
            Assert.IsFalse(Session.Unsaleable.Contains(GalaxyMap.KhadorDriveItem));

            EnterStep(84);
            Assert.AreEqual(1, Shop.CargoOf(GalaxyMap.KhadorDriveItem), "+1 Khador Drive");
            Assert.IsFalse(Session.Unsaleable.Contains(GalaxyMap.KhadorDriveItem));
        }

        [Test]
        public void Step77WithoutAJumpDriveLocksNothing()
        {
            EnterStep(77);
            Assert.IsFalse(Session.Unsaleable.Contains(GalaxyMap.KhadorDriveItem));
        }

        [Test]
        public void Step78TakesTheDriveFromTheHoldWhenNoneIsMounted()
        {
            Shop.AddToCargo(GalaxyMap.KhadorDriveItem, 2);
            EnterStep(78);
            Assert.AreEqual(1, Shop.CargoOf(GalaxyMap.KhadorDriveItem));
        }

        [Test]
        public void Step78ResetsTheBlueprintsStartedOnValkyrie()
        {
            var recipe = Db.Item(179).blueprint;
            Assume.That(recipe.Count, Is.GreaterThan(0));
            var started = new BlueprintState { item = 179, moneySpent = 500, station = 101, remaining = recipe.Select(p => 0).ToList() };
            var elsewhere = new BlueprintState { item = 85, moneySpent = 700, station = 10, remaining = Db.Item(85).blueprint.Select(p => 0).ToList() };
            Session.Blueprints.Add(started); Session.Blueprints.Add(elsewhere);
            EnterStep(78);
            Assert.AreEqual(0, started.moneySpent);
            Assert.AreEqual(-1, started.station);
            CollectionAssert.AreEqual(recipe.Select(p => p.amount), started.remaining, "what was invested on Valkyrie is lost");
            Assert.AreEqual(700, elsewhere.moneySpent, "another station's blueprint stays");
        }

        // ---- Supernova (cases 0x54-0xa1) ----------------------------------------------------------------------

        [Test]
        public void Step89DeliversTheLuxuryGoodsAndRevealsGinoyaAndTalidor()
        {
            Shop.AddToCargo(104, 12);
            EnterStep(89);
            Assert.AreEqual(2, Shop.CargoOf(104), "10 Luxury removed");
            Assert.IsTrue(Visible(27) && Visible(28));
        }

        [Test]
        public void Step90UnvisitsNaneroh()
        {
            Session.VisitedStations.Add(109);
            EnterStep(90);
            Assert.IsFalse(Session.VisitedStations.Contains(109));
        }

        [TestCase(91)]
        [TestCase(94)]
        [TestCase(139)]
        public void CounterStepsResetTheCounter(int n)
        {
            Session.StoryCounter = 7;
            EnterStep(n);
            Assert.AreEqual(0, Session.StoryCounter);
        }

        [TestCase(94, 205)]
        [TestCase(102, 207)]
        public void StepHandsOverAnItem(int n, int item)
        {
            EnterStep(n);
            Assert.AreEqual(1, Shop.CargoOf(item));
        }

        [TestCase(104, 206)]
        [TestCase(141, 210)]
        public void StepUnlocksABlueprint(int n, int product)
        {
            EnterStep(n);
            Assert.IsTrue(Session.UnlockedBlueprints.Contains(product));
        }

        [Test]
        public void Step113DrinksOneMagnetarJuice()
        {
            Shop.AddToCargo(146, 2);
            EnterStep(113);
            Assert.AreEqual(1, Shop.CargoOf(146));
        }

        [Test]
        public void TheMutagenIsKeptAt119AndHandedOverAt122()
        {
            Shop.AddToCargo(209, 1);
            EnterStep(119);
            Assert.IsTrue(Session.Unsaleable.Contains(209));
            EnterStep(122);
            Assert.AreEqual(0, Shop.CargoOf(209));
            Assert.IsFalse(Session.Unsaleable.Contains(209));
        }

        [Test]
        public void Step119WithoutTheMutagenLocksNothing()
        {
            EnterStep(119);
            Assert.IsFalse(Session.Unsaleable.Contains(209));
        }

        [Test]
        public void Step142HandsOverThePlasmaKit()
        {
            EnterStep(142);
            Assert.AreEqual(15, Shop.CargoOf(197), "15 Ion Lambda Mk1");
            Assert.AreEqual(1, Shop.CargoOf(196), "Spectral Filter SA-1");
            Assert.AreEqual(1, Shop.CargoOf(198), "PE Proton collector");
        }

        [Test]
        public void Step144TakesTheChromoPlasma()
        {
            Shop.AddToCargo(210, 2);
            EnterStep(144);
            Assert.AreEqual(1, Shop.CargoOf(210));
        }
    }
}
