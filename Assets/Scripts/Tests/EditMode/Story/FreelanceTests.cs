// FreelanceTests.cs
// Feature: CLAUDE.md "Bar and freelance" (Freelance: one mission at a time, accepting / discarding, the mission orbit and its
// travel block, Recovery / Salvage's return trip, delivery on docking, payment with the 1 000 001 guard, standing +5, missions
// completed, the lost Challenge's wager).
// Expected values: Reference/research/freelance_missions.md §3.2 (337 / 338 refusals), §3.3 (Courier: item 116 x amount,
// unsaleable; Passenger: Status+0x34; Extreme pays (reward+bonus)/10 up front; an active mission discarded), §4.1 (the
// mission orbit: not for types 8, 14, 11, 13), §4.3 (Recovery / Salvage -> type 11, target the agent's station, status -1,
// text 803; the carrier's crate 117 for type 3, 116 for type 5), §4.4 (failure: the Challenge wager; containers removed),
// §5 (delivery per type, 6 666 for reward + bonus >= 1 000 001, standing +5 / Extreme +10 toward the client), §2.5 (the bonus
// recomputed with the current standing; none for types 8 and 12); CLAUDE.md (BlocksTravel: Courier and a won / failed
// mission never block).

using System.Collections.Generic;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class FreelanceTests : StoryAreaTestBase
    {
        static FreelanceMission M(int type, int target = 57, int amount = 0, int reward = 10000, int race = 4, int client = 55, int good = 0) =>
            new FreelanceMission { type = type, target = target, amount = amount, reward = reward, clientRace = race, clientStation = client, good = good };

        static Agent AgentWith(FreelanceMission m) => new Agent { name = "Client", station = m.clientStation, race = m.clientRace, mission = m };

        static void Take(FreelanceMission m) => Session.FreelanceMission = m;

        [Test]
        public void NoMissionAtANewGame()
        {
            Assert.IsFalse(Freelance.Active);
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(57));
            Assert.IsFalse(Freelance.IsMissionOrbit(57));
        }

        [Test]
        public void MaxPassengersSumsTheCabins()
        {
            Assert.AreEqual(0, Freelance.MaxPassengers(Db));
            Session.Equipment.Add(new ItemStack(93, 1));   // Large Cabin, 10
            Session.Equipment.Add(new ItemStack(92, 1));   // Medium Cabin, 5
            Assert.AreEqual(15, Freelance.MaxPassengers(Db));
        }

        [Test]
        public void CourierNeedsFreeCargo()
        {
            int free = Shop.FreeCargo(Db);
            Assert.IsNull(Freelance.AcceptRefusal(Db, M(MissionType.Courier, amount: free)));
            string refusal = Freelance.AcceptRefusal(Db, M(MissionType.Courier, amount: free + 1));
            Assert.IsNotNull(refusal, "337");
            StringAssert.Contains((free + 1).ToString(), refusal, "#Q = the amount");
        }

        [Test]
        public void CourierCountsTheContainersOfTheMissionItReplaces()
        {
            int free = Shop.FreeCargo(Db);
            var old = M(MissionType.Courier, amount: free);
            Freelance.Accept(Db, AgentWith(old));
            Assert.AreEqual(0, Shop.FreeCargo(Db));
            Assert.IsNull(Freelance.AcceptRefusal(Db, M(MissionType.Courier, amount: free)), "the old containers leave the hold");
        }

        [Test]
        public void PassengerNeedsCabins()
        {
            Assert.IsNotNull(Freelance.AcceptRefusal(Db, M(MissionType.Passenger, amount: 3)), "338");
            Session.Equipment.Add(new ItemStack(92, 1));
            Assert.IsNull(Freelance.AcceptRefusal(Db, M(MissionType.Passenger, amount: 5)));
            Assert.IsNotNull(Freelance.AcceptRefusal(Db, M(MissionType.Passenger, amount: 6)));
        }

        [Test]
        public void AcceptingACourierLoadsUnsaleableContainers()
        {
            var agent = AgentWith(M(MissionType.Courier, amount: 12));
            Freelance.Accept(Db, agent);
            Assert.IsTrue(Freelance.Active);
            Assert.AreEqual(12, Shop.CargoOf(Freelance.SecureContainer));
            Assert.IsTrue(Session.Unsaleable.Contains(Freelance.SecureContainer));
            Assert.IsTrue(agent.accepted);
            Assert.AreNotSame(agent.mission, Session.FreelanceMission, "the player's copy");
            Assert.AreEqual(MissionType.Courier, Session.FreelanceMission.type);
        }

        [Test]
        public void AcceptingAPassengerMissionBoardsThePassengers()
        {
            Freelance.Accept(Db, AgentWith(M(MissionType.Passenger, amount: 7)));
            Assert.AreEqual(7, Session.Passengers);
        }

        [Test]
        public void OneMissionAtATime()
        {
            Freelance.Accept(Db, AgentWith(M(MissionType.Courier, amount: 5)));
            Freelance.Accept(Db, AgentWith(M(MissionType.Defense, target: 45)));
            Assert.AreEqual(MissionType.Defense, Session.FreelanceMission.type);
            Assert.AreEqual(0, Shop.CargoOf(Freelance.SecureContainer), "the old mission's containers went");
            Assert.IsFalse(Session.Unsaleable.Contains(Freelance.SecureContainer));
        }

        [Test]
        public void ExtremePaysATenthUpFront()
        {
            var m = M(MissionType.Defense, reward: 20000);
            Assert.AreEqual(0, Freelance.UpFrontCost(m));
            Session.Difficulty = Session.DifficultyExtreme;
            Assert.AreEqual(2000, Freelance.UpFrontCost(m), "(reward + bonus) / 10");
            Session.Credits = 5000;
            Freelance.Accept(Db, AgentWith(m));
            Assert.AreEqual(3000, Session.Credits);
        }

        [Test]
        public void DiscardEmptiesTheMission()
        {
            Freelance.Accept(Db, AgentWith(M(MissionType.Passenger, amount: 4)));
            Shop.AddToCargo(Freelance.SecureCabin, 1);
            Session.Unsaleable.Add(Freelance.SecureCabin);
            Freelance.Discard();
            Assert.IsFalse(Freelance.Active);
            Assert.AreEqual(0, Session.Passengers);
            Assert.AreEqual(0, Shop.CargoOf(Freelance.SecureCabin));
            Assert.IsFalse(Session.Unsaleable.Contains(Freelance.SecureCabin));
        }

        // ---- the mission orbit (Status::departStation) --------------------------------------------------------

        [TestCase(MissionType.Courier, true)]
        [TestCase(MissionType.Defense, true)]
        [TestCase(MissionType.Recovery, true)]
        [TestCase(MissionType.JunkRemoval, true)]
        [TestCase(MissionType.Challenge, true)]
        [TestCase(MissionType.Purchase, false)]
        [TestCase(MissionType.StolenGoods, false)]
        [TestCase(MissionType.Passenger, false)]
        [TestCase(MissionType.Informer, false)]
        public void MissionOrbitByType(int type, bool orbit)
        {
            Take(M(type, target: 45));
            Assert.AreEqual(orbit, Freelance.IsMissionOrbit(45));
            Assert.IsFalse(Freelance.IsMissionOrbit(46), "only the target's orbit");
        }

        [Test]
        public void TheMissionOrbitBlocksTravelUntilWonOrFailed()
        {
            Take(M(MissionType.PirateHunting, target: 45));
            Assert.IsTrue(Freelance.BlocksTravel(45));
            Assert.IsFalse(Freelance.BlocksTravel(46));
            Session.FreelanceMission.won = true;
            Assert.IsFalse(Freelance.BlocksTravel(45));
            Session.FreelanceMission.won = false; Session.FreelanceMission.failed = true;
            Assert.IsFalse(Freelance.BlocksTravel(45));
            Take(M(MissionType.Courier, target: 45));
            Assert.IsFalse(Freelance.BlocksTravel(45), "Courier is delivered by docking");
        }

        // ---- Recovery / Salvage ------------------------------------------------------------------------------

        [TestCase(MissionType.Recovery, Freelance.SecureCabin)]
        [TestCase(MissionType.Salvage, Freelance.SecureContainer)]
        public void RecoveryAndSalvageTurnIntoAReturnTrip(int type, int crate)
        {
            Take(M(type, target: 45, client: 57));
            Session.FreelanceMission.won = true;
            Freelance.ToReturnTrip();
            var m = Session.FreelanceMission;
            Assert.AreEqual(MissionType.Passenger, m.type, "type 11");
            Assert.AreEqual(57, m.target, "the agent's station");
            Assert.AreEqual(-1, m.status);
            Assert.IsFalse(m.won);
            CollectionAssert.AreEqual(new[] { 803 }, m.textIds);
            Assert.IsTrue(Session.Unsaleable.Contains(crate), "the swapped crate item (verified 0xf0872)");
            Assert.AreEqual(Freelance.DockResult.Success, Freelance.CheckDocked(57));
        }

        [Test]
        public void TheReturnTripCountsNoPassengers()
        {
            Take(M(MissionType.Recovery, target: 45, client: 57));
            Freelance.ToReturnTrip();
            Session.Passengers = 0;
            Freelance.Succeed(true);
            Assert.AreEqual(0, Session.PassengersDelivered);
        }

        // ---- docking (Status::missionCompleted / missionFailed, docked) ---------------------------------------

        [TestCase(MissionType.Courier)]
        [TestCase(MissionType.Passenger)]
        public void CourierAndPassengerDeliverAtTheTarget(int type)
        {
            Take(M(type, target: 45));
            Assert.AreEqual(Freelance.DockResult.Success, Freelance.CheckDocked(45));
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(46));
        }

        [Test]
        public void PurchaseNeedsTheGoodsAtTheAgentsStation()
        {
            Take(M(MissionType.Purchase, target: 57, amount: 6, good: 122));
            Shop.AddToCargo(122, 5);
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(57));
            Shop.AddToCargo(122, 1);
            Assert.AreEqual(Freelance.DockResult.Success, Freelance.CheckDocked(57));
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(45));
        }

        [Test]
        public void InformerSucceedsAnywhereOnceTheSpyIsDead()
        {
            Take(M(MissionType.Informer, target: 45));
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(10));
            Session.InformerKilled = true;
            Assert.AreEqual(Freelance.DockResult.Success, Freelance.CheckDocked(10));
            Session.InformerFailed = true;
            Assert.AreEqual(Freelance.DockResult.Failure, Freelance.CheckDocked(10), "another ship died in its orbit");
        }

        [Test]
        public void StolenGoodsGoBackToTheClient()
        {
            Take(M(MissionType.StolenGoods, target: 45, client: 57));
            var stock = new StationStock { station = 45 };
            Freelance.OnEnterStation(stock);
            Assert.AreEqual(1, stock.items.Find(s => s.item == Freelance.Documents).amount, "the documents in the target's shop");
            Freelance.OnEnterStation(stock);
            Assert.AreEqual(1, stock.items.FindAll(s => s.item == Freelance.Documents).Count, "once");
            var other = new StationStock { station = 46 };
            Freelance.OnEnterStation(other);
            Assert.AreEqual(0, other.items.Count);

            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(57));
            Shop.AddToCargo(Freelance.Documents, 1);
            Assert.AreEqual(Freelance.DockResult.Success, Freelance.CheckDocked(57));
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(45));
        }

        [TestCase(MissionType.Defense)]
        [TestCase(MissionType.Challenge)]
        [TestCase(MissionType.Escort)]
        public void SpaceMissionsAreNotResolvedByDocking(int type)
        {
            Take(M(type, target: 45));
            Assert.AreEqual(Freelance.DockResult.None, Freelance.CheckDocked(45));
        }

        // ---- payment ------------------------------------------------------------------------------------------

        [Test]
        public void SuccessPaysRewardPlusTheCurrentStandingBonus()
        {
            Session.Standing = new[] { 40, 0 };
            Take(M(MissionType.Defense, reward: 10000, race: 0));
            Assert.AreEqual(4000, Session.FreelanceMission.CurrentBonus, "40 % of the reward");
            Session.Credits = 100;
            int paid = Freelance.Succeed(false);
            Assert.AreEqual(14000, paid);
            Assert.AreEqual(14100, Session.Credits);
            Assert.AreEqual(1, Session.FreelanceCompleted);
            Assert.AreEqual(45, Session.Standing[0], "+5 toward the Terrans");
            Assert.IsFalse(Freelance.Active);
        }

        [Test]
        public void SuccessPaysTheBonusPromisedOnAccepting()
        {
            // A standing change during the mission doesn't change the pay (the original keeps the chat's bonus).
            Session.Standing = new[] { 40, 0 };
            Freelance.Accept(Db, AgentWith(M(MissionType.Defense, reward: 10000, race: 0)));
            Session.Standing = new[] { 0, 0 };
            Assert.AreEqual(14000, Freelance.Succeed(false));
        }

        [Test]
        public void StandingPlusFiveTowardTheClientsRace()
        {
            Session.Standing = new[] { 30, 0 };
            Take(M(MissionType.Defense, race: 1)); Freelance.Succeed(true);
            Assert.AreEqual(25, Session.Standing[0], "toward the Vossk = the axis down");
            Take(M(MissionType.Defense, race: 2)); Freelance.Succeed(true);
            Assert.AreEqual(5, Session.Standing[1]);
            Take(M(MissionType.Defense, race: 3)); Freelance.Succeed(true);
            Assert.AreEqual(0, Session.Standing[1]);
            Take(M(MissionType.Defense, race: 6)); Freelance.Succeed(true);
            CollectionAssert.AreEqual(new[] { 25, 0 }, Session.Standing, "other races: no standing");
        }

        [Test]
        public void StandingPlusTenOnExtreme()
        {
            Session.Difficulty = Session.DifficultyExtreme;
            Session.Standing = new[] { 30, 0 };
            Take(M(MissionType.Defense, race: 0));
            Freelance.Succeed(true);
            Assert.AreEqual(40, Session.Standing[0]);
        }

        [TestCase(1000000, true, 1000000)]
        [TestCase(1000001, true, 6666)]
        [TestCase(2500000, true, 6666)]
        [TestCase(2500000, false, 2500000)]
        public void TheMillionGuardWhenDocked(int reward, bool docked, int paid)
        {
            Take(M(MissionType.Courier, reward: reward, race: 5));
            Assert.AreEqual(paid, Freelance.Succeed(docked));
            Assert.AreEqual(paid, Session.Credits);
        }

        [Test]
        public void NoBonusForPurchaseAndChallenge()
        {
            Session.Standing = new[] { 100, 0 };
            Assert.AreEqual(0, M(MissionType.Purchase, race: 0).CurrentBonus);
            Assert.AreEqual(0, M(MissionType.Challenge, race: 0).CurrentBonus);
            Assert.AreEqual(10000, M(MissionType.Courier, race: 0).CurrentBonus);
            Session.Standing = new[] { -50, -60 };
            Assert.AreEqual(5000, M(MissionType.Courier, race: 1).CurrentBonus, "Vossk liked at -50");
            Assert.AreEqual(0, M(MissionType.Courier, race: 0).CurrentBonus, "never negative");
            Assert.AreEqual(6000, M(MissionType.Courier, race: 3).CurrentBonus);
        }

        [Test]
        public void CourierDeliveryCountsTheContainers()
        {
            Freelance.Accept(Db, AgentWith(M(MissionType.Courier, amount: 9)));
            Freelance.Succeed(true);
            Assert.AreEqual(9, Session.ContainersDelivered);
            Assert.AreEqual(0, Shop.CargoOf(Freelance.SecureContainer));
            Assert.IsFalse(Session.Unsaleable.Contains(Freelance.SecureContainer));
        }

        [Test]
        public void PassengerDeliveryCountsThePassengers()
        {
            Freelance.Accept(Db, AgentWith(M(MissionType.Passenger, amount: 6)));
            Freelance.Succeed(true);
            Assert.AreEqual(6, Session.PassengersDelivered);
            Assert.AreEqual(0, Session.Passengers);
        }

        [Test]
        public void PurchaseAndStolenGoodsTakeTheGoods()
        {
            Take(M(MissionType.Purchase, amount: 6, good: 122));
            Shop.AddToCargo(122, 8);
            Freelance.Succeed(true);
            Assert.AreEqual(2, Shop.CargoOf(122));

            Take(M(MissionType.StolenGoods, target: 45, client: 57));
            Shop.AddToCargo(Freelance.Documents, 1);
            Freelance.Succeed(true);
            Assert.AreEqual(0, Shop.CargoOf(Freelance.Documents));
        }

        [Test]
        public void ALostChallengeCostsTheWager()
        {
            Session.Credits = 20000;
            Take(M(MissionType.Challenge, reward: 7500));
            Freelance.Fail();
            Assert.AreEqual(12500, Session.Credits);
            Assert.IsFalse(Freelance.Active);
            Assert.AreEqual(0, Session.FreelanceCompleted);
        }

        [Test]
        public void OtherFailuresCostNothing()
        {
            Session.Credits = 20000;
            Session.Standing = new[] { 30, 0 };
            Take(M(MissionType.Defense, reward: 7500, race: 0));
            Freelance.Fail();
            Assert.AreEqual(20000, Session.Credits);
            Assert.AreEqual(30, Session.Standing[0], "no standing change");
        }

        // ---- texts --------------------------------------------------------------------------------------------

        [Test]
        public void ResultTexts()
        {
            Take(M(MissionType.Defense, target: 45));
            for (int k = 0; k < 20; k++)
            {
                Freelance.SuccessText(out int s);
                Assert.That(s, Is.InRange(373, 377));
                Freelance.FailureText(out int f);
                Assert.That(f, Is.InRange(384, 388));
            }
            Take(M(MissionType.Challenge, target: 45));
            Freelance.SuccessText(out int c, 3, 2);
            Assert.AreEqual(370, c);
            Take(M(MissionType.Defense, target: 108));
            Freelance.SuccessText(out int k108);
            Assert.AreEqual(458, k108, "Shima");
        }

        [Test]
        public void MissionTypeNamesAreText354On()
        {
            for (int t = 0; t <= 15; t++) Assert.AreEqual(Localization.Get(354 + t), MissionType.Name(t));
            Assert.AreEqual("", MissionType.Name(-1));
            Assert.AreEqual("", MissionType.Name(16));
        }

        [Test]
        public void TheMissionSurvivesASaveAndLoad()
        {
            Freelance.Accept(Db, AgentWith(M(MissionType.Courier, target: 45, amount: 3, reward: 4321)));
            Session.StationIndex = 55;
            Assert.IsTrue(SaveGame.Save(2));
            Session.ResetNewGame();
            Assert.IsTrue(SaveGame.Load(2));
            Assert.AreEqual(MissionType.Courier, Session.FreelanceMission.type);
            Assert.AreEqual(45, Session.FreelanceMission.target);
            Assert.AreEqual(4321, Session.FreelanceMission.reward);
            Assert.AreEqual(3, Shop.CargoOf(Freelance.SecureContainer));
        }
    }
}
