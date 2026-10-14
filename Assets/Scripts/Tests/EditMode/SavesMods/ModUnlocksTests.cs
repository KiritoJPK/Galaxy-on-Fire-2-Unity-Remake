// ModUnlocksTests.cs
// Feature: CLAUDE.md "Mods (remake-only)" > Conditions (ModUnlocks.cs): items.json / ships.json "available" (new entries
// and overrides) is a condition in the event graphs' expression language (EventRunner.Condition: credits, visited(station),
// option(mod:option), ...); no condition = always; one that can't be read counts as false and warns once on its mod.
// ships.json "dealer" { chance %, systemRace, minTechLevel } puts a mod ship in the ordinary dealers' lists
// (AddDealerShips). The test wires GameHooks.Condition to EventRunner.Condition as the game's NetHooksInstaller does.

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Events;
using GoF2Remake.Modding;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ModUnlocksTests : SavesModsTest
    {
        GameHooks.ConditionCheck savedCondition;

        [SetUp]
        public void WireConditions()
        {
            savedCondition = GameHooks.Condition;
            GameHooks.Condition = EventRunner.Condition;
        }

        [TearDown]
        public void UnwireConditions() => GameHooks.Condition = savedCondition;

        void ModOn()
        {
            string dir = WriteMod("u", "t_unlock", "1.0", "", ("items.json", @"[
                    { 'id': 'gated', 'base': 2, 'name': 'Gated', 'available': 'credits >= 1000' },
                    { 'id': 'free', 'base': 2, 'name': 'Free' },
                    { 'id': 'broken', 'base': 2, 'name': 'Broken', 'available': 'credits >=' },
                    { 'id': 'optional', 'base': 2, 'name': 'Optional', 'available': 'option(t_unlock:extra)' },
                    { 'override': 12, 'available': 'visited(70)' }
                ]"),
                ("ships.json", @"[
                    { 'id': 'yacht', 'model': 'y.glb', 'name': 'Yacht', 'available': 'visited(70)', 'dealer': { 'chance': 100, 'systemRace': -1, 'minTechLevel': 0 } },
                    { 'id': 'never', 'model': 'y.glb', 'name': 'Never', 'dealer': { 'chance': 0 } },
                    { 'id': 'hightech', 'model': 'y.glb', 'name': 'High tech', 'dealer': { 'chance': 100, 'minTechLevel': 11 } },
                    { 'id': 'vossk', 'model': 'y.glb', 'name': 'Vossk only', 'dealer': { 'chance': 100, 'systemRace': 1 } }
                ]"),
                ("gameoptions.json", "[ { 'id': 'extra', 'name': 'Extra' } ]"));
            WriteFile(dir, "y.glb", Dummy);
            ScanAndEnable("t_unlock");
            Assert.IsFalse(Installed("t_unlock").Broken, AllMessages(Installed("t_unlock")));
            Database.Load();
        }

        int Item(string id) => ModContent.ItemIndexOf("t_unlock:" + id);
        int Ship(string id) => ModContent.ShipIndexOf("t_unlock:" + id);

        [Test]
        public void TheGameHooksConditionIsTheEventGraphsExpressions()
        {
            Assert.IsTrue(EventRunner.Condition("", out _));
            Session.Credits = 10;
            Assert.IsTrue(EventRunner.Condition("credits >= 10", out string e1), e1);
            Assert.IsFalse(EventRunner.Condition("credits > 10", out string e2), e2);
            Assert.IsNull(e2);
            Assert.IsFalse(EventRunner.Condition("credits >=", out string e3));
            Assert.IsNotNull(e3);
        }

        [Test]
        public void AnItemWithAConditionIsAvailableOnlyWhileItHolds()
        {
            ModOn();
            Session.Credits = 0;
            Assert.IsFalse(ModUnlocks.ItemAvailable(Item("gated")));
            Session.Credits = 1000;
            Assert.IsTrue(ModUnlocks.ItemAvailable(Item("gated")));
        }

        [Test]
        public void WithoutAConditionAnItemIsAlwaysAvailable()
        {
            ModOn();
            Assert.IsTrue(ModUnlocks.ItemAvailable(Item("free")));
            Assert.IsTrue(ModUnlocks.ItemAvailable(2), "an original item");
        }

        [Test]
        public void AnOverridesConditionAppliesToTheOriginalItem()
        {
            ModOn();
            Session.VisitedStations = new HashSet<int> { 78 };
            Assert.IsFalse(ModUnlocks.ItemAvailable(12));
            Session.VisitedStations.Add(70);
            Assert.IsTrue(ModUnlocks.ItemAvailable(12));
        }

        [Test]
        public void ABrokenConditionIsFalseAndWarnsOnce()
        {
            ModOn();
            Session.Credits = 1000000;
            Assert.IsFalse(ModUnlocks.ItemAvailable(Item("broken")));
            Assert.IsFalse(ModUnlocks.ItemAvailable(Item("broken")));
            var warnings = Installed("t_unlock").Warnings.Where(w => w.Contains("\"available\"")).ToList();
            Assert.AreEqual(1, warnings.Count, AllMessages(Installed("t_unlock")));
            StringAssert.Contains("credits >=", warnings[0]);
        }

        [Test]
        public void AGameOptionCanUnlockAnItem()
        {
            ModOn();
            Assert.IsFalse(ModUnlocks.ItemAvailable(Item("optional")));
            Session.ModGameOptions.Add("t_unlock:extra");
            Assert.IsTrue(ModUnlocks.ItemAvailable(Item("optional")));
        }

        [Test]
        public void AShipWithAConditionIsSoldOnlyWhileItHolds()
        {
            ModOn();
            Session.VisitedStations = new HashSet<int> { 78 };
            Assert.IsFalse(ModUnlocks.ShipAvailable(Ship("yacht")));
            Assert.IsTrue(ModUnlocks.ShipAvailable(Ship("never")), "no condition");
            Session.VisitedStations.Add(70);
            Assert.IsTrue(ModUnlocks.ShipAvailable(Ship("yacht")));
        }

        [Test]
        public void HoldsWithoutAConditionIsTrue()
        {
            Assert.IsTrue(ModUnlocks.Holds(null, null, "x"));
            Assert.IsTrue(ModUnlocks.Holds(null, "  ", "x"));
            Assert.IsFalse(ModUnlocks.Holds(null, "1 > 2", "x"));
            Assert.IsTrue(ModUnlocks.Holds(null, "2 > 1", "x"));
        }

        [Test]
        public void WithoutAConditionCheckEverythingIsAvailable()
        {
            ModOn();
            GameHooks.Condition = null;
            Session.Credits = 0;
            Assert.IsTrue(ModUnlocks.ItemAvailable(Item("gated")));
        }

        [Test]
        public void DealersStockModShipsByTheirDealerEntry()
        {
            ModOn();
            var db = Database.Load();
            Session.VisitedStations = new HashSet<int> { 78, 70 };
            // station 70 (Dis): every ship whose chance is 100, race any, tech level reachable
            var ships = new List<int> { 5 };
            ModUnlocks.AddDealerShips(db, 70, 0, ships);
            CollectionAssert.Contains(ships, Ship("yacht"));
            CollectionAssert.DoesNotContain(ships, Ship("never"), "chance 0");
            CollectionAssert.DoesNotContain(ships, Ship("hightech"), "tech level 11 is above every station's");
            CollectionAssert.DoesNotContain(ships, Ship("vossk"), "a Vossk system only");
            Assert.AreEqual(1, ships.Count(s => s == Ship("yacht")));
            ModUnlocks.AddDealerShips(db, 70, 0, ships);
            Assert.AreEqual(1, ships.Count(s => s == Ship("yacht")), "never twice");
            var vossk = new List<int>();
            ModUnlocks.AddDealerShips(db, 70, 1, vossk);
            CollectionAssert.Contains(vossk, Ship("vossk"));
        }

        [Test]
        public void ADealerShipMustBeAvailable()
        {
            ModOn();
            var db = Database.Load();
            Session.VisitedStations = new HashSet<int> { 78 };
            var ships = new List<int>();
            ModUnlocks.AddDealerShips(db, 70, 0, ships);
            CollectionAssert.DoesNotContain(ships, Ship("yacht"));
        }

        [Test]
        public void TheDealerEntryIsRead()
        {
            ModOn();
            var c = CustomShips.Get(Ship("yacht"));
            Assert.IsNotNull(c.dealer);
            Assert.AreEqual(100f, c.dealer.chance);
            Assert.AreEqual(-1, c.dealer.systemRace);
            Assert.AreEqual(0, c.dealer.minTechLevel);
            Assert.AreEqual(11, CustomShips.Get(Ship("hightech")).dealer.minTechLevel);
            Assert.AreEqual(1, CustomShips.Get(Ship("vossk")).dealer.systemRace);
        }
    }
}
