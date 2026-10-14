// FreelanceMissionGenerationTests.cs
// Feature: CLAUDE.md "Bar and freelance" (AgentGenerator.CreateMission = Generator::createMission: target, type rotation,
// difficulty, parameters, reward / bonus / costs) and "Multiplayer" (type 15 Ore Mining only in sessions).
// Expected values: Reference/research/freelance_missions.md §2.2-2.5 and Reference/tools/missions/mission_tables.py (run:
// `mission_tables.py reward --type T --difficulty D --from A --to B --level L [--race R --s1 S --amount N]`, outputs pinned
// below); FreelanceReference ports the formulas and is checked against those outputs; the generated missions are then
// checked against it. The Purchase case's 1540 is the Android Energy Cells max price (shop.md: 1400-1540).

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class FreelanceMissionGenerationTests : StoryAreaTestBase
    {
        // ---- the reference port against the Python tool ------------------------------------------------------

        // type, difficulty, from station, to station, level, race, s0, s1, amount, maxPrice -> reward, bonus
        [TestCase(0, 5, 55, 78, 5, 0, 30, 0, 0, 0, 5600, 1650)]
        [TestCase(9, 10, 55, 10, 0, 0, 30, 0, 0, 0, 8500, 2550)]
        [TestCase(7, 3, 55, 55, 20, 0, 30, 0, 0, 0, 82200, 24650)]
        [TestCase(3, 7, 70, 98, 3, 0, 30, 0, 0, 0, 11050, 3300)]
        [TestCase(11, 4, 55, 78, 2, 1, 30, 0, 9, 0, 6450, 0)]
        [TestCase(8, 4, 55, 55, 2, 0, 30, 0, 10, 1540, 26250, 0)]
        [TestCase(12, 6, 55, 55, 4, 0, 30, 0, 0, 0, 5400, 0)]
        [TestCase(4, 2, 55, 78, 1, 2, 30, 50, 0, 0, 2650, 1300)]
        public void ReferenceFormulaMatchesThePythonTool(int type, int d, int from, int to, int level, int race, int s0, int s1,
                                                         int amount, int maxPrice, int reward, int bonus)
        {
            float dist = FreelanceReference.Distance(Db, FreelanceReference.SystemOf(Db, from), FreelanceReference.SystemOf(Db, to));
            float r = FreelanceReference.Reward(type, d, dist, amount, maxPrice, level);
            Assert.AreEqual(reward, FreelanceReference.Round50(r));
            int b = type == 8 || type == 12 ? 0 : FreelanceReference.Round50(r * FreelanceReference.BonusFactor(race, s0, s1));
            Assert.AreEqual(bonus, b);
        }

        [TestCase(55, 78, 31.32f)]
        [TestCase(55, 10, 21.28f)]
        [TestCase(70, 98, 13.75f)]
        [TestCase(55, 55, 0f)]
        public void GalaxyDistanceMatchesThePythonTool(int a, int b, float expected)
        {
            int sa = FreelanceReference.SystemOf(Db, a), sb = FreelanceReference.SystemOf(Db, b);
            Assert.AreEqual(expected, AgentGenerator.Distance(Db, sa, sb), 0.01f);
            Assert.AreEqual(AgentGenerator.Distance(Db, sa, sb), AgentGenerator.Distance(Db, sb, sa), 1e-4f, "symmetric");
        }

        [TestCase(1000f, 1000)]
        [TestCase(1024.9f, 1000)]
        [TestCase(1025f, 1050)]
        [TestCase(1049f, 1000)]
        [TestCase(1050f, 1050)]
        [TestCase(1074f, 1050)]
        [TestCase(1075f, 1100)]
        [TestCase(5611.4f, 5600)]
        [TestCase(0f, 0)]
        public void Round50RoundsDownExceptTwentyFive(float x, int expected)
        {
            Assert.AreEqual(expected, AgentGenerator.Round50(x));
        }

        // ---- the generator ------------------------------------------------------------------------------------

        static Agent Client(int station = 55, int race = 0) =>
            new Agent { name = "Test Client", race = race, station = station, male = true, portrait = new int[5] };

        void FreePlayRank(int rank, int campaign = 20)
        {
            SetRankFreePlay(rank);
            Session.FreePlay = false;   // story rules; the rank stays (XP counts the campaign index then)
            Session.Kills -= campaign;
            Session.CampaignMission = campaign;
            Assert.AreEqual(rank, Session.Rank);
        }

        [Test]
        public void GeneratedMissionsFollowTheFormulas()
        {
            FreePlayRank(5);
            Session.Standing = new[] { 30, -40 };
            var agent = Client();
            int level = Session.Rank;
            for (int seed = 0; seed < 400; seed++)
            {
                AgentGenerator.Rng = new System.Random(seed);
                var m = AgentGenerator.CreateMission(Db, agent, 55, false);
                string at = "seed " + seed + " type " + m.type;
                Assert.That(m.type, Is.InRange(0, 14), at);
                Assert.AreNotEqual(MissionType.Purchase, m.type, "purchase only through offer 5");
                Assert.That(m.difficulty, Is.InRange(1, 9), at);
                Assert.AreEqual(55, m.clientStation);
                Assert.AreEqual(0, m.clientRace);
                Assert.AreEqual("Test Client", m.clientName);

                int expAmount = FreelanceReference.Amount(m.type, m.difficulty);
                if (m.type == MissionType.Protection) Assert.That(m.amount, Is.InRange(2, 5), at);
                else Assert.AreEqual(expAmount, m.amount, at);
                if (m.type == MissionType.Courier) Assert.That(m.good, Is.InRange(0, 6), at + ": cargo description 813 + good");
                if (m.type == MissionType.Recovery) Assert.AreEqual(116, m.good, at);
                if (m.type == MissionType.Salvage) Assert.AreEqual(117, m.good, at);
                if (m.type == MissionType.Wanted) Assert.IsFalse(string.IsNullOrEmpty(m.targetName), at);

                float dist = FreelanceReference.Distance(Db, FreelanceReference.SystemOf(Db, 55), FreelanceReference.SystemOf(Db, m.target));
                float r = FreelanceReference.Reward(m.type, m.difficulty, dist, m.amount, 0, level);
                Assert.AreEqual(FreelanceReference.Round50(r), m.reward, at);
                int bonus = m.type == MissionType.Challenge ? 0 : FreelanceReference.Round50(r * FreelanceReference.BonusFactor(0, 30, -40));
                Assert.AreEqual(bonus, m.bonus, at);
                Assert.AreEqual(0, m.costs % 50, at);
                Assert.That(m.costs, Is.InRange(m.reward / 10 - 50, m.reward / 5 + 50), at + ": costs = reward/10 + rnd(reward/10)");
            }
        }

        [Test]
        public void TargetsFollowGenerateStationIndex()
        {
            FreePlayRank(3);
            var agent = Client();
            var visible = GalaxyMap.Visibility(Db);
            for (int seed = 0; seed < 400; seed++)
            {
                AgentGenerator.Rng = new System.Random(seed);
                var m = AgentGenerator.CreateMission(Db, agent, 55, false);
                string at = "seed " + seed + " type " + m.type + " target " + m.target;
                if (m.type == MissionType.Challenge) { Assert.AreEqual(55, m.target, at + ": the agent's own station"); continue; }
                CollectionAssert.DoesNotContain(FreelanceReference.ExcludedTargets, m.target, at);
                Assert.IsFalse(m.target >= 109 && m.target <= 113, at);
                var sys = Db.Systems.Find(s => s.index == FreelanceReference.SystemOf(Db, m.target));
                Assert.IsTrue(visible[sys.index], at + ": a visible system");
                Assert.IsTrue(sys.jumpRoutesTo.Count > 0 || sys.index == FreelanceReference.SystemOf(Db, 55), at);
                if (m.type == MissionType.Courier || m.type == MissionType.Passenger || m.type == MissionType.StolenGoods)
                    Assert.AreNotEqual(55, m.target, at + ": never the station you are at");
                if (m.type == MissionType.Informer) Assert.AreEqual(0, sys.raceId, at + ": a station of the agent's race");
            }
        }

        [Test]
        public void EveryTypeOnceBeforeRepeats()
        {
            FreePlayRank(3);
            var agent = Client();
            var all = Enumerable.Range(0, 15).Where(t => t != MissionType.Purchase).ToList();
            for (int round = 0; round < 3; round++)
            {
                var types = new List<int>();
                for (int k = 0; k < 14; k++) types.Add(AgentGenerator.CreateMission(Db, agent, 55, false).type);
                CollectionAssert.AreEquivalent(all, types, "round " + round + ": the 14 types once each (Status+0x50)");
            }
        }

        [Test]
        public void TheRotationIsPerGameNotPerAgent()
        {
            FreePlayRank(3);
            var types = new List<int>();
            for (int k = 0; k < 14; k++) types.Add(AgentGenerator.CreateMission(Db, Client(k % 2 == 0 ? 55 : 57), 55, false).type);
            Assert.AreEqual(14, types.Distinct().Count());
            Assert.AreEqual(14, Session.UsedMissionTypes.Count(u => u), "the 14 flags set");
        }

        [Test]
        public void NoInterceptForNonFactionRaces()
        {
            // The 13 types a Cyborg can roll, once each. (Once those are used the original's 1000-try loop runs out and keeps
            // its last roll, whatever it is: mission_tables.py pick_type does the same, so that isn't pinned here.)
            FreePlayRank(3);
            var types = new List<int>();
            for (int k = 0; k < 13; k++) types.Add(AgentGenerator.CreateMission(Db, Client(55, 5), 55, false).type);
            CollectionAssert.DoesNotContain(types, MissionType.Intercept);
            Assert.AreEqual(13, types.Distinct().Count());
        }

        [Test]
        public void EarlyCampaignOnlyOffersTheEarlyTypes()
        {
            Session.CampaignMission = 12;
            var early = new[] { 11, 0, 7, 4, 12 };
            for (int seed = 0; seed < 200; seed++)
            {
                AgentGenerator.Rng = new System.Random(seed);
                var m = AgentGenerator.CreateMission(Db, Client(), 55, false);
                CollectionAssert.Contains(early, m.type, "seed " + seed);
                Assert.That(m.difficulty, Is.InRange(1, 2), "campaign < 16: nextInt(2) + 1");
            }
        }

        [Test]
        public void MidoTargetsAreMidosFirstStations()
        {
            Session.CampaignMission = 20;
            for (int seed = 0; seed < 200; seed++)
            {
                AgentGenerator.Rng = new System.Random(seed);
                var m = AgentGenerator.CreateMission(Db, Client(78, 3), 78, false);
                if (m.type == MissionType.Challenge) continue;
                Assert.That(m.target, Is.InRange(75, 78), "firstStation(Mido) + nextInt(4)");
            }
        }

        [Test]
        public void PurchaseOffersBuyGoodsAtTheAgentsStation()
        {
            FreePlayRank(4);
            int level = Session.Rank;
            var forbidden = new[] { 115, 116, 117, 131, 164, 175, 217, 218 };
            for (int seed = 0; seed < 200; seed++)
            {
                AgentGenerator.Rng = new System.Random(seed);
                var m = AgentGenerator.CreateMission(Db, Client(57), 55, true);
                string at = "seed " + seed + " good " + m.good;
                Assert.AreEqual(MissionType.Purchase, m.type);
                Assert.AreEqual(57, m.target, "the agent's station");
                var it = Db.Item(m.good);
                Assert.That(m.good, Is.GreaterThanOrEqualTo(97), at);
                Assert.That(it.occurrence, Is.GreaterThan(0), at);
                Assert.That((it.minPrice + it.maxPrice) / 2, Is.GreaterThan(0), at);
                Assert.AreEqual(0, it.blueprint.Count, at);
                CollectionAssert.DoesNotContain(forbidden, m.good, at);
                Assert.That(m.amount, Is.InRange(5, 19), at);
                Assert.AreEqual(System.Math.Min(it.techLevel, 10), m.difficulty, at + ": the item's tech level");
                Assert.AreEqual(FreelanceReference.Round50(FreelanceReference.Reward(8, m.difficulty, 0f, m.amount, it.maxPrice, level)), m.reward, at);
                Assert.AreEqual(0, m.bonus);
            }
        }

        [Test]
        public void OreMiningIsNeverRolledInSinglePlayer()
        {
            FreePlayRank(3);
            for (int k = 0; k < 400; k++)
                Assert.AreNotEqual(MissionType.OreMining, AgentGenerator.CreateMission(Db, Client(), 55, false).type);
        }

        [Test]
        public void OreMiningIsRolledInSessions()
        {
            FreePlayRank(3);
            NetSession.ActiveProvider = () => true;
            var ores = new List<FreelanceMission>();
            for (int k = 0; k < 400; k++)
            {
                var m = AgentGenerator.CreateMission(Db, Client(), 55, false);
                if (m.type == MissionType.OreMining) ores.Add(m);
            }
            NetSession.ActiveProvider = null;
            Assert.That(ores.Count, Is.GreaterThan(5), "type 15 in the roll (nextInt(16))");
            foreach (var m in ores)
            {
                Assert.That(m.amount, Is.InRange(30, 119), "nextInt(90) + 30 t");
                Assert.IsNotNull(Db.Item(m.good), "one of the station's asteroid ores");
            }
        }

        [Test]
        public void SameSeedSameMission()
        {
            FreePlayRank(3);
            AgentGenerator.Rng = new System.Random(77);
            var used = (bool[])Session.UsedMissionTypes.Clone();
            var a = AgentGenerator.CreateMission(Db, Client(), 55, false);
            AgentGenerator.Rng = new System.Random(77);
            Session.UsedMissionTypes = used;
            var b = AgentGenerator.CreateMission(Db, Client(), 55, false);
            Assert.AreEqual(a.type, b.type);
            Assert.AreEqual(a.target, b.target);
            Assert.AreEqual(a.reward, b.reward);
            Assert.AreEqual(a.amount, b.amount);
        }

        [Test]
        public void TheRankTermDominatesLate()
        {
            FreePlayRank(20);
            AgentGenerator.Rng = new System.Random(5);
            var m = AgentGenerator.CreateMission(Db, Client(), 55, false);
            Assert.That(m.reward, Is.GreaterThanOrEqualTo(80000), "level 20 adds 80 000 (freelance_missions.md 2.5)");
        }
    }
}
