// TargetLockRulesTests.cs
// The remake's ship lock (CLAUDE.md "NPCs and combat", Radar; TargetLockRules, CombatRadar): the Smart lock's priority
// (hostile first, then the nearest to the crosshair), its hold on an enemy (Settings.KeepEnemyLock, its own option after a
// player's suggestion) and the ship let go by Unlock target, which stays out until it leaves the crosshair box.

using System.Linq;
using GoF2Remake.Flight;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class TargetLockRulesTests
    {
        struct Ship { public string name; public float dx, dy; public bool hostile; }

        /// <summary>CombatRadar's pick with the Smart lock: the eligible ship with the lowest score, or null.</summary>
        static string Pick(Ship[] ships, bool keepEnemyLock, bool lockedHostileAlive, string released = null)
        {
            bool holds = TargetLockRules.HoldsEnemy(true, keepEnemyLock, lockedHostileAlive);
            return ships.Where(s => TargetLockRules.Eligible(holds, s.hostile, s.name == released))
                        .OrderBy(s => TargetLockRules.Score(s.dx, s.dy, s.hostile))
                        .Select(s => s.name).FirstOrDefault();
        }

        static readonly Ship Enemy = new Ship { name = "enemy", dx = 60, dy = 40, hostile = true };
        static readonly Ship Trader = new Ship { name = "trader", dx = 2, dy = 1, hostile = false };
        static readonly Ship Raider = new Ship { name = "raider", dx = 90, dy = 0, hostile = true };

        [Test]
        public void HostileShipsComeFirstThenTheNearest()
        {
            Assert.AreEqual("enemy", Pick(new[] { Trader, Raider, Enemy }, true, false), "hostile first, the nearer of the two");
            Assert.AreEqual("trader", Pick(new[] { Trader }, true, false), "no hostile ship in the box: the neutral one");
            Assert.Less(TargetLockRules.Score(1000, 1000, true), TargetLockRules.Score(0, 0, false), "any hostile beats any neutral");
        }

        [Test]
        public void TheHoldKeepsNeutralShipsOffWhileAnEnemyIsLocked()
        {
            Assert.IsNull(Pick(new[] { Trader }, true, true), "an enemy locked: the trader can't take it");
            Assert.AreEqual("raider", Pick(new[] { Trader, Raider }, true, true), "another hostile ship still can");
            Assert.AreEqual("trader", Pick(new[] { Trader }, true, false), "the locked enemy dead or neutral: no hold");
        }

        [Test]
        public void WithoutTheHoldANeutralShipCanBeLockedDuringABattle()
        {
            Assert.AreEqual("trader", Pick(new[] { Trader }, false, true), "an enemy locked, the hold off: the trader locks");
            Assert.AreEqual("enemy", Pick(new[] { Trader, Enemy }, false, true), "the priority stays: a hostile ship in the box first");
        }

        [Test]
        public void TheHoldIsOnlyPartOfTheSmartLock()
        {
            Assert.IsFalse(TargetLockRules.HoldsEnemy(false, true, true), "the Original lock never holds");
            Assert.IsTrue(TargetLockRules.HoldsEnemy(true, true, true));
            Assert.IsFalse(TargetLockRules.HoldsEnemy(true, false, true));
        }

        [Test]
        public void AnUnlockedShipStaysOutUntilItLeavesTheBox()
        {
            // Unlocked: the enemy still in the box isn't picked straight back, the trader beside it is (no lock, no hold).
            Assert.AreEqual("trader", Pick(new[] { Trader, Enemy }, true, false, released: "enemy"));
            Assert.AreEqual("raider", Pick(new[] { Trader, Enemy, Raider }, true, false, released: "enemy"), "another enemy still first");
            Assert.IsFalse(TargetLockRules.Eligible(false, true, true), "released: never a candidate");
            Assert.IsTrue(TargetLockRules.StillReleased(true, true), "alive and in the box: still released");
            Assert.IsFalse(TargetLockRules.StillReleased(true, false), "out of the box: lockable again");
            Assert.IsFalse(TargetLockRules.StillReleased(false, true), "dead: forgotten");
        }
    }
}
