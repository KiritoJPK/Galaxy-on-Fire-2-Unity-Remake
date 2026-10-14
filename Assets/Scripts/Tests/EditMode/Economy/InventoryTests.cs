// InventoryTests.cs
// Feature: CLAUDE.md "Shop" > Ship mods kept with the hull ("a traded-in hull keeps them in the dealer row that takes it",
// StationStock.shipMods: ModsOf / TakeMods / PutMods / PruneShipMods) and the plain inventory records (ItemStack,
// StoredShip, ParkedShip).
// Expected: Reference/research/blueprints_mods.md 2.3 (mods belong to the hull; HangarWindow::OnTouchEnd 0x176d94 gives the
// old ship's mods to its new dealer row and the bought row's mods to the new ship) and the remake's rules in CLAUDE.md
// (rows replaced outside a trade drop their mods). Pure data: no economy numbers.

using System.Collections.Generic;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class InventoryTests
    {
        [Test]
        public void ItemStackClone()
        {
            var a = new ItemStack(36, 6);
            var b = a.Clone();
            b.amount = 1;
            Assert.AreEqual(6, a.amount);
            Assert.AreEqual(36, b.item);
        }

        [Test]
        public void StoredShipCopiesItsLists()
        {
            var mods = new List<int> { 0, 1 };
            var gear = new List<ItemStack> { new ItemStack(54, 1) };
            var s = new StoredShip(9, 1, mods, gear);
            mods.Add(2);
            gear.Clear();
            CollectionAssert.AreEqual(new[] { 0, 1 }, s.mods);
            Assert.AreEqual(1, s.equipment.Count);
            var bare = new StoredShip(9, 1, null);
            Assert.IsNotNull(bare.mods);
            Assert.IsEmpty(bare.equipment);
        }

        [Test]
        public void NewStationStockIsEmpty()
        {
            var s = new StationStock();
            Assert.IsEmpty(s.items);
            Assert.IsEmpty(s.ships);
            Assert.IsEmpty(s.shipMods);
            Assert.IsEmpty(s.ModsOf(10));
        }

        [Test]
        public void DealerRowsCarryTheirMods()
        {
            var s = new StationStock { ships = new List<int> { 9, 10 } };
            s.PutMods(10, new List<int> { 1, 2 });
            s.PutMods(9, new List<int>());   // no mods: no entry
            s.PutMods(9, null);
            Assert.AreEqual(1, s.shipMods.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, s.ModsOf(10));
            Assert.IsEmpty(s.ModsOf(9));
        }

        [Test]
        public void ModsOfARowNotListedAreNone()
        {
            var s = new StationStock { ships = new List<int> { 9 } };
            s.PutMods(10, new List<int> { 3 });
            Assert.IsEmpty(s.ModsOf(10), "the row has left the list");
        }

        [Test]
        public void TakingARowsModsRemovesThem()
        {
            var s = new StationStock { ships = new List<int> { 10 } };
            s.PutMods(10, new List<int> { 0, 0 });
            var taken = s.TakeMods(10);
            CollectionAssert.AreEqual(new[] { 0, 0 }, taken);
            Assert.IsEmpty(s.shipMods);
            Assert.IsEmpty(s.TakeMods(10));
            taken.Add(5);
            Assert.IsEmpty(s.ModsOf(10), "a copy");
        }

        [Test]
        public void PruneDropsTheModsOfRowsGone()
        {
            var s = new StationStock { ships = new List<int> { 9, 10 } };
            s.PutMods(9, new List<int> { 1 });
            s.PutMods(10, new List<int> { 2 });
            s.ships.Remove(9);
            s.PruneShipMods();
            Assert.AreEqual(1, s.shipMods.Count);
            Assert.AreEqual(10, s.shipMods[0].ship);
            s.shipMods = null;
            s.PruneShipMods();
            Assert.IsNotNull(s.shipMods, "an old save's null list is made");
        }

        [Test]
        public void ParkedShipStartsUnknown()
        {
            var p = new ParkedShip();
            Assert.AreEqual(-1, p.hull);
            Assert.AreEqual(-1, p.armor);
            Assert.AreEqual(-1f, p.shield);
            Assert.IsEmpty(p.equipment);
            Assert.IsEmpty(p.cargo);
        }
    }
}
