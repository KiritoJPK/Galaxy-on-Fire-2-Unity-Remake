// WantedBoardTests.cs
// Feature: CLAUDE.md "Supernova add-on" (Most Wanted: WantedBoard, Session.Wanted; the boards by race, activation on docking,
// movement between orbits, the bounty, the storyline criminals' surrender, the Quineros ships).
// Expected values: Reference/research/wingmen_wanted.md §2.1-2.8 and Reference/tools/wingmen/wingmen_wanted.py (`wanted 10`:
// the 25 entries' boards, rewards, requiredBounties, requiredMission; jump_bounds: storyline 2..4, else
// (i-1)%6/3+2 .. (i-1)%6/2+4 path nodes; activate / move: S1 / S2 in visible systems with gate routes outside 6, 25-28, a
// BFS gate path within the bounds, the criminal on a station of the path; one gate per orbit change toward travelsTo, into
// its station inside its system, a new trip once arrived; it waits where the player is or is heading); §2.2 (the board
// tab: Terran from 128, the others from 161, not in the Void or at Kaamo); §2.8 (storyline 0 / 1 surrender: no bounty).
// Station picks: 55 Kappa (Union, Terran), 15 (V'ikka, Vossk), 47 / 45 (Weymire, gate station 45).

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class WantedBoardTests : StoryAreaTestBase
    {
        static readonly int[] Rewards =
        {
            50000, 60000, 75000, 85000, 100000, 120000, 140000, 60000, 75000, 85000, 100000, 120000, 140000,
            60000, 75000, 85000, 100000, 120000, 140000, 60000, 75000, 85000, 100000, 120000, 140000,
        };
        static readonly int[] RequiredBounties = { 0, 0, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5 };
        static readonly HashSet<int> Excluded = new HashSet<int> { 6, 25, 26, 27, 28 };

        static (int lo, int hi) JumpBounds(int i) => i < 2 ? (2, 4) : ((i - 1) % 6 / 3 + 2, (i - 1) % 6 / 2 + 4);
        static int SystemOf(int station) => Db.Stations.Find(s => s.index == station).system;

        [Test]
        public void TheTwentyFiveCriminals()
        {
            Assert.AreEqual(25, Db.Wanted.Count);
            for (int i = 0; i < 25; i++)
            {
                var w = Db.Wanted[i];
                Assert.AreEqual(i < 7 ? 0 : i < 13 ? 1 : i < 19 ? 2 : 3, w.board, "board of " + i);
                Assert.AreEqual(i == 0 ? 128 : i == 1 ? 130 : i < 7 ? 131 : 161, w.requiredMission, "requiredMission of " + i);
                Assert.AreEqual(Rewards[i], w.reward, "reward of " + i);
                Assert.AreEqual(RequiredBounties[i], w.requiredBounties, "requiredBounties of " + i);
            }
            Assert.IsTrue(WantedBoard.IsStoryline(0) && WantedBoard.IsStoryline(1));
            Assert.IsFalse(WantedBoard.IsStoryline(2));
        }

        [Test]
        public void BoardAccess()
        {
            Session.CampaignMission = 127;
            Assert.IsFalse(WantedBoard.Accessible(Db, 55));
            Session.CampaignMission = 128;
            Assert.IsTrue(WantedBoard.Accessible(Db, 55), "Terran stations from 128");
            Assert.IsFalse(WantedBoard.Accessible(Db, 15), "a Vossk station");
            Assert.IsFalse(WantedBoard.Accessible(Db, 108), "not at Kaamo");
            Assert.IsFalse(WantedBoard.Accessible(Db, Session.VoidOrbit), "not in the Void");
            Session.CampaignMission = 161;
            Assert.IsTrue(WantedBoard.Accessible(Db, 15), "the other races from 161");
            Session.FreePlay = true;
            Assert.IsFalse(WantedBoard.Accessible(Db, 55), "never in free play");
        }

        [Test]
        public void BoardRows()
        {
            Session.CampaignMission = 128;
            CollectionAssert.AreEqual(Enumerable.Range(0, 7), WantedBoard.ListFor(Db, 55).Select(w => w.index));
            Session.CampaignMission = 161;
            Assert.AreEqual(0, WantedBoard.ListFor(Db, 15).Count, "the other boards list from 162");
            Session.CampaignMission = 162;
            CollectionAssert.AreEqual(Enumerable.Range(7, 6), WantedBoard.ListFor(Db, 15).Select(w => w.index));
        }

        [Test]
        public void StorylineRow()
        {
            Session.CampaignMission = 128; Assert.AreEqual(0, WantedBoard.StorylineRow);
            Session.CampaignMission = 130; Assert.AreEqual(1, WantedBoard.StorylineRow);
            Session.CampaignMission = 131; Assert.AreEqual(-1, WantedBoard.StorylineRow);
        }

        void CheckRoute(int i)
        {
            var st = WantedBoard.State(Db, i);
            Assert.IsTrue(st.active, "active " + i);
            int a = SystemOf(st.lastSeen), b = SystemOf(st.travelsTo);
            var visible = GalaxyMap.Visibility(Db);
            foreach (int s in new[] { a, b })
            {
                var sys = Db.Systems.Find(x => x.index == s);
                Assert.IsTrue(visible[s] && sys.jumpRoutesTo.Count > 0 && !Excluded.Contains(s), "usable system " + s);
            }
            Assert.AreNotEqual(a, b, "S2 in another system");
            var path = GalaxyMap.SystemPath(Db, a, b);
            var (lo, hi) = JumpBounds(i);
            Assert.That(path.Count, Is.InRange(lo, hi), "path of " + i);
            CollectionAssert.Contains(path, SystemOf(st.current), "the criminal somewhere on the route");
        }

        [Test]
        public void TheFirstStorylineCriminalActivatesAt128()
        {
            Session.CampaignMission = 128;
            for (int seed = 0; seed < 20; seed++)
            {
                Session.Wanted = new List<WantedState>();
                UnityEngine.Random.InitState(seed);
                Assert.AreEqual(1, WantedBoard.ActivateNew(Db, 55));
                CheckRoute(0);
                Assert.IsFalse(WantedBoard.State(Db, 1).active, "Kehnor only at 130");
            }
            Assert.AreEqual(0, WantedBoard.ActivateNew(Db, 55), "already active");
            Assert.AreEqual(0, WantedBoard.ActivateNew(Db, 15), "only the station's board");
        }

        [Test]
        public void StorylineCriminalsOnlyAtTheirExactStep()
        {
            Session.CampaignMission = 131;
            Assert.AreEqual(1, WantedBoard.ActivateNew(Db, 55), "entry 2 (no bounty needed)");
            Assert.IsFalse(WantedBoard.State(Db, 0).active);
            Assert.IsFalse(WantedBoard.State(Db, 1).active);
            Assert.IsTrue(WantedBoard.State(Db, 2).active);
            CheckRoute(2);
        }

        [Test]
        public void TheBoardUnlocksOneKillAtATime()
        {
            Session.CampaignMission = 131;
            WantedBoard.ActivateNew(Db, 55);
            Assert.IsFalse(WantedBoard.State(Db, 3).active);
            Session.CollectedBounties[0] = 1;
            Assert.AreEqual(1, WantedBoard.ActivateNew(Db, 55));
            Assert.IsTrue(WantedBoard.State(Db, 3).active);
            CheckRoute(3);
            Session.CollectedBounties[0] = 4;
            Assert.AreEqual(3, WantedBoard.ActivateNew(Db, 55), "4, 5, 6");
            for (int i = 4; i <= 6; i++) CheckRoute(i);
        }

        [Test]
        public void NoActivationInFreePlayTheVoidOrKaamo()
        {
            Session.CampaignMission = 162;
            Session.FreePlay = true;
            Assert.AreEqual(0, WantedBoard.ActivateNew(Db, 55));
            Session.FreePlay = false;
            Assert.AreEqual(0, WantedBoard.ActivateNew(Db, Session.VoidOrbit));
            Assert.AreEqual(0, WantedBoard.ActivateNew(Db, 108));
        }

        WantedState Traveller(int i, int current, int to)
        {
            var st = WantedBoard.State(Db, i);
            st.active = true; st.current = current; st.travelsTo = to; st.lastSeen = current;
            return st;
        }

        [Test]
        public void MovesOneGateAtATimeThenIntoItsStation()
        {
            Session.CampaignMission = 131;
            var st = Traveller(3, 55, 47);
            WantedBoard.Move(Db, 78, -1);
            Assert.AreEqual(45, st.current, "the next system's jumpgate station (Weymire)");
            WantedBoard.Move(Db, 78, -1);
            Assert.AreEqual(47, st.current, "into its destination inside the system");
            WantedBoard.Move(Db, 78, -1);
            Assert.AreEqual(47, st.current, "arrived: it stays this step");
            Assert.AreEqual(47, st.lastSeen);
            Assert.AreNotEqual(SystemOf(47), SystemOf(st.travelsTo), "a new trip to another system");
            var path = GalaxyMap.SystemPath(Db, SystemOf(47), SystemOf(st.travelsTo));
            var (lo, hi) = JumpBounds(3);
            Assert.That(path.Count, Is.InRange(lo, hi));
        }

        [Test]
        public void ItWaitsWhereThePlayerIsOrIsHeading()
        {
            Session.CampaignMission = 131;
            var st = Traveller(3, 55, 47);
            WantedBoard.Move(Db, 55, -1);
            Assert.AreEqual(55, st.current, "you find it there");
            WantedBoard.Move(Db, 78, 55);
            Assert.AreEqual(55, st.current, "the programmed station");
        }

        [Test]
        public void InactiveOrDeadCriminalsDontMove()
        {
            Session.CampaignMission = 131;
            var st = Traveller(3, 55, 47);
            st.terminated = true;
            WantedBoard.Move(Db, 78, -1);
            Assert.AreEqual(55, st.current);
            st.terminated = false; st.active = false;
            WantedBoard.Move(Db, 78, -1);
            Assert.AreEqual(55, st.current);
            st.active = true; Session.FreePlay = true;
            WantedBoard.Move(Db, 78, -1);
            Assert.AreEqual(55, st.current);
        }

        [Test]
        public void InOrbitPicksTheLowestRequiredBounties()
        {
            Session.CampaignMission = 131;
            Traveller(5, 55, 47);
            Traveller(3, 55, 47);
            Assert.AreEqual(3, WantedBoard.InOrbit(Db, 55).index);
            WantedBoard.State(Db, 3).terminated = true;
            Assert.AreEqual(5, WantedBoard.InOrbit(Db, 55).index);
            Assert.IsNull(WantedBoard.InOrbit(Db, 57));
            Session.FreePlay = true;
            Assert.IsNull(WantedBoard.InOrbit(Db, 55));
        }

        [Test]
        public void AKillPaysTheBountyOnce()
        {
            Session.CampaignMission = 131;
            var st = Traveller(2, 55, 47);
            Session.Credits = 1000;
            Assert.AreEqual(75000, WantedBoard.OnKilled(Db, 2));
            Assert.AreEqual(76000, Session.Credits);
            Assert.IsTrue(st.terminated);
            Assert.IsFalse(st.active);
            Assert.AreEqual(1, Session.CollectedBounties[0], "+1 on its board");
            Assert.AreEqual(0, WantedBoard.OnKilled(Db, 2));
            Assert.AreEqual(76000, Session.Credits);
            Traveller(9, 15, 17);
            WantedBoard.OnKilled(Db, 9);
            Assert.AreEqual(1, Session.CollectedBounties[1], "the Vossk board");
        }

        [Test]
        public void TheStorylineCriminalsSurrender()
        {
            Session.CampaignMission = 128;
            var st = Traveller(0, 55, 47);
            WantedBoard.OnSurrender(Db, 0, 55);
            Assert.IsFalse(st.active);
            Assert.IsFalse(st.terminated, "not killed");
            Assert.AreEqual(0, Session.CollectedBounties[0], "no bounty");
            Assert.AreEqual(0, Session.Credits);
            Assert.IsTrue(Story.IsComplete(Db, new StoryContext { docked = false, station = 55, levelMs = 6000f }), "wins campaign 128");

            Session.CampaignMission = 131;
            Session.StoryMission = new StoryMission { type = StoryType.Level, station = 112 };
            WantedBoard.OnSurrender(Db, 1, 55);
            Assert.AreEqual(StoryType.Level, Story.Mission.type, "130's surrender only at 130");
        }

        [Test]
        public void QuinerosSellsTheDeadBossesShips()
        {
            Assert.AreEqual(0, WantedBoard.QuinerosShips(Db).Count);
            WantedBoard.State(Db, 6).terminated = true;
            WantedBoard.State(Db, 18).terminated = true;
            CollectionAssert.AreEqual(new[] { 45, 47 }, WantedBoard.QuinerosShips(Db));
            WantedBoard.State(Db, 12).terminated = true;
            WantedBoard.State(Db, 24).terminated = true;
            CollectionAssert.AreEqual(new[] { 45, 46, 47, 48 }, WantedBoard.QuinerosShips(Db));
        }

        [Test]
        public void TheBoardSurvivesASaveAndLoad()
        {
            Session.CampaignMission = 131;
            Session.StationIndex = 55;
            Traveller(4, 55, 47);
            Session.CollectedBounties[2] = 3;
            Assert.IsTrue(SaveGame.Save(4));
            Session.ResetNewGame();
            Assert.IsTrue(SaveGame.Load(4));
            var st = WantedBoard.State(Db, 4);
            Assert.IsTrue(st.active);
            Assert.AreEqual(47, st.travelsTo);
            Assert.AreEqual(3, Session.CollectedBounties[2]);
        }
    }
}
