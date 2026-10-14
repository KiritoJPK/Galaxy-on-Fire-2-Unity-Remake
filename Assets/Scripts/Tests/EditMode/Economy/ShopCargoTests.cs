// ShopCargoTests.cs
// Feature: CLAUDE.md "Shop" > Cargo (every unit 1 t, mounted items weigh nothing, buying never checks space), Ship::getMaxLoad
// with compression and the Kaamo cargo mod, Ship::getBaseHP / getCombinedHP, Ship::refreshValue's fire power, the
// one-per-ship categories (Item::canBeInstalledMultipleTimes), unknown item numbers refused (the 2026-10-12 crash report).
// Expected numbers: Reference/research/shop.md 3.3 / 5 / 6 (rules, the multi-install table) and the decompiled
// Ship::refreshValue (sorts 0-3, 8 and 25); stats by hand from ships.json / item_attributes.json: Phantom (10) cargo 52, hull
// 200; Rhoda Blackhole (67) compression 100 %, Autopacker 2 (64) 25 %, Ultracompact (65) 40 %; Fluxed Matter Shield (54)
// 220, T'yol (59) 250; Nirai Charged Pulse (2) damage 6, reload 380 ms.

using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ShopCargoTests : EconomyTest
    {
        [Test]
        public void NewGameState()
        {
            // shop.md 8 (Status::resetGame): 0 credits, the Phantom at Var Hastra, 2 x Nirai Charged Pulse, 6 Edo, Fluxed
            // Matter Shield, T'yol, Telta Ecoscan, Synchrotron Boost; an empty hold
            Session.ResetNewGame();
            Assert.AreEqual(0, Session.Credits);
            Assert.AreEqual(10, Session.ShipIndex);
            Assert.AreEqual(78, Session.StationIndex);
            CollectionAssert.AreEqual(new[] { 2, 2, 36, 54, 59, 82, 73 }, Session.Equipment.ConvertAll(e => e.item));
            CollectionAssert.AreEqual(new[] { 1, 1, 6, 1, 1, 1, 1 }, Session.Equipment.ConvertAll(e => e.amount));
            Assert.IsEmpty(Session.Cargo);
            Assert.IsEmpty(Session.ShipMods);
            Assert.AreEqual(0, Session.KaamoState);
            Assert.IsEmpty(Session.UnlockedBlueprints);
        }

        [Test]
        public void AddToCargoMergesStacks()
        {
            Shop.AddToCargo(122, 3);
            Shop.AddToCargo(122, 4);
            Shop.AddToCargo(109, 1);
            Assert.AreEqual(2, Session.Cargo.Count);
            Assert.AreEqual(7, Shop.CargoOf(122));
            Assert.AreEqual(8, Shop.CargoLoad());
        }

        [Test]
        public void AddToCargoIgnoresNothingAndNegative()
        {
            Shop.AddToCargo(122, 0);
            Shop.AddToCargo(122, -5);
            Assert.IsEmpty(Session.Cargo);
        }

        [Test]
        public void UnknownItemNumbersNeverReachTheHold()
        {
            Shop.AddToCargo(99999, 3);
            Shop.AddToCargo(-4, 3);
            Assert.IsEmpty(Session.Cargo);
            Assert.AreEqual(0, Shop.CargoLoad());
        }

        [Test]
        public void RemoveFromCargoTakesAcrossStacksAndDropsEmptyOnes()
        {
            Session.Cargo.Add(new ItemStack(122, 2));
            Session.Cargo.Add(new ItemStack(109, 5));
            Session.Cargo.Add(new ItemStack(122, 3));
            Shop.RemoveFromCargo(122, 4);
            Assert.AreEqual(1, Shop.CargoOf(122));
            Assert.AreEqual(5, Shop.CargoOf(109));
            Shop.RemoveFromCargo(122, 10);
            Assert.AreEqual(0, Shop.CargoOf(122));
            Assert.AreEqual(1, Session.Cargo.Count);
        }

        [Test]
        public void MountedItemsWeighNothing()
        {
            // the new game's equipment (7 stacks, 6 Edo missiles) is mounted, the hold is empty
            Assert.AreEqual(0, Shop.CargoLoad());
            Assert.AreEqual(52, Shop.FreeCargo(Db));
        }

        [Test]
        public void MaxLoadIsTheShipsCargo()
        {
            Assert.AreEqual(52, Shop.MaxLoad(Db));   // Phantom
            Session.ShipIndex = 0;
            Assert.AreEqual(25, Shop.MaxLoad(Db));   // Betty
        }

        [Test]
        public void CompressionAddsAPercentageOfTheBaseCargo()
        {
            Session.Equipment.Add(new ItemStack(67, 1));   // +100 %
            Assert.AreEqual(104, Shop.MaxLoad(Db));
            Session.Equipment.RemoveAt(Session.Equipment.Count - 1);
            Session.Equipment.Add(new ItemStack(64, 1));   // +25 %
            Session.Equipment.Add(new ItemStack(65, 1));   // +40 %: stackable, (int)(52 x 0.65) = 33
            Assert.AreEqual(85, Shop.MaxLoad(Db));
        }

        [Test]
        public void CargoModAddsThirtyTonnesBeforeCompression()
        {
            // getMaxLoad: (base + 30 per cargo mod) + (int)((base + mods) x compression %)
            Session.ShipMods.Add(1);
            Assert.AreEqual(82, Shop.MaxLoad(Db));
            Session.Equipment.Add(new ItemStack(67, 1));
            Assert.AreEqual(164, Shop.MaxLoad(Db));
            Session.ShipMods.Add(1);   // a stacked level (remake)
            Assert.AreEqual(224, Shop.MaxLoad(Db));
        }

        [Test]
        public void FreeCargoCanGoNegative()
        {
            Shop.AddToCargo(122, 60);
            Assert.AreEqual(-8, Shop.FreeCargo(Db));
        }

        [Test]
        public void HitPointsOfHullShieldAndArmor()
        {
            Assert.AreEqual(200, Shop.BaseHp(Db));
            Assert.AreEqual(200 + 220 + 250, Shop.CombinedHp(Db));
            Session.ShipMods.Add(0);
            Assert.AreEqual(240, Shop.BaseHp(Db), "+40 per armor mod");
            Session.ShipMods.Add(0);
            Assert.AreEqual(280, Shop.BaseHp(Db));
            Session.Equipment.RemoveAll(e => e.item == 54);
            Assert.AreEqual(280 + 250, Shop.CombinedHp(Db));
        }

        [Test]
        public void FirePowerOfTheStartLoadout()
        {
            // two Nirai Charged Pulse: 2 x 6 / 380 x 1000; the Edo missiles (sort 5) don't count
            Assert.AreEqual(2f * 6f / 380f * 1000f, Shop.FirePower(Db), 1e-3f);
            Session.Equipment.Clear();
            Assert.AreEqual(0f, Shop.FirePower(Db));
        }

        [Test]
        public void FirstMountedFindsTheCategory()
        {
            Assert.AreEqual(54, Shop.FirstMounted(Db, 9)?.index);
            Assert.AreEqual(59, Shop.FirstMounted(Db, 10)?.index);
            Assert.IsNull(Shop.FirstMounted(Db, 19), "no drill");
        }

        [Test]
        public void OnePerShipCategoriesMatchTheOriginalTable()
        {
            // shop.md 6 (DAT_00254930): one per ship 8, 9, 10, 13-19, 21, 26-29, 33, 35, 37, 38, 41; the rest multi
            var once = new[] { 8, 9, 10, 13, 14, 15, 16, 17, 18, 19, 21, 26, 27, 28, 29, 33, 35, 37, 38, 41 };
            for (int sort = 0; sort <= 43; sort++)
                Assert.AreEqual(System.Array.IndexOf(once, sort) < 0, Shop.CanInstallMultiple(sort), "sort " + sort);
        }

        [Test]
        public void InsertStockKeepsItemOrder()
        {
            var s = Stock(70, 5, 1, 122, 3);
            Shop.InsertStock(s, new ItemStack(55, 2));
            Shop.InsertStock(s, new ItemStack(0, 1));
            Shop.InsertStock(s, new ItemStack(200, 1));
            CollectionAssert.AreEqual(new[] { 0, 5, 55, 122, 200 }, s.items.ConvertAll(r => r.item));
        }

        [Test]
        public void GeometryLookups()
        {
            Assert.AreEqual(15, Shop.SystemOf(Db, 78));
            Assert.AreEqual(3, Shop.RaceOfSystem(Db, 15), "Mido is Midorian");
            Assert.AreEqual(25, Shop.SystemOf(Db, 106), "Sao Perula in Loma");
            Assert.AreEqual(26, Shop.SystemOf(Db, KaamoClub.Station));
        }

        [Test]
        public void SupernovaSystemUntilStep158()
        {
            Session.CampaignMission = 157;
            Assert.IsTrue(Shop.InSupernovaSystem(27, 109));
            Session.CampaignMission = 158;
            Assert.IsFalse(Shop.InSupernovaSystem(27, 109));
            Session.CampaignMission = 100;
            Assert.IsFalse(Shop.InSupernovaSystem(26, 108));
        }
    }
}
