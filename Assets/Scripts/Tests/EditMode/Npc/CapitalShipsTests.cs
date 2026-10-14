// CapitalShipsTests.cs
// Feature: CLAUDE.md "NPCs and combat" > Capital ships and pirate bases (the original's specials), Capital ship
// enhancements, Fleet battles (remake): TrafficPlan.AddCapitalShipAt / AddFleetBattle and World.CapitalShips (Enhance,
// LoopAround, LaunchShip / PadSet / HasDeck / Killable, BattleBounty, RollLoot, Store, ResupplyOffers, Buy, UnitPrice).
// Expected values: Reference/research/npc_combat_specials.md 2.2-2.4 (the turret tables 0x253604 / 0x253544 / 0x2536ac,
// 1000 HP, scale 6, turret_002 / turret_003, the battleship's hull 25 x (4 cm + 14 rank + 20)), CLAUDE.md "Capital ship
// enhancements" (escorts 3 + rank / 7, +1 Hard / Extreme, +2 carrier, hull x1.5, gun x1.3; turrets (500 + 50 rank) x
// difficulty, gun x(1 + 0.1 rank), the Vossk battleship's x1.6; the battleship's hull x5, the carrier's and the Vossk
// battleship's x8, "12 000 at rank 20 on Normal"; EMP x3 a freighter's; 8 s, x14; docking sets 5 / 8, Inflicts (5) and
// H'Socs (9); the store: cells 10-15, rounds 5-10, gear 5 Vossk-made / 6 Terran tech 4+, 1-2 each, secondaries 10-20,
// prices x1.25, up to 50 mounted; the loot: two tech 5+ items, 8-15 t rare goods, 5-10 cells, once an hour) and
// "Fleet battles" (80 000 apart, wings of 4 + rank / 5, 30 000 + 3000 x rank). Hand calculation from those.

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class CapitalShipsTests : NpcTestBase
    {
        static List<SpawnSpec> Capital(int kind, bool enhanced, Vector3 at = default)
        {
            var list = new List<SpawnSpec>();
            Assert.AreEqual(0, TrafficPlan.AddCapitalShipAt(Db, list, kind, at, enhanced));
            return list;
        }

        static List<SpawnSpec> Turrets(List<SpawnSpec> list) => list.Where(x => x.group == NpcGroup.Turret).ToList();
        static List<SpawnSpec> Escorts(List<SpawnSpec> list) => list.Where(x => x.group == NpcGroup.Escort).ToList();

        // ---- the original's specials ----------------------------------------------------------------------------

        [Test]
        public void BattleshipAndItsSevenTurrets()
        {
            InFreePlay(0);
            var host = new Vector3(1000, 2000, 60000);
            var list = Capital(SpawnSpec.CapitalBattleship, false, host);
            Assert.AreEqual(8, list.Count);
            Assert.AreEqual(14, list[0].ship);
            Assert.AreEqual(SpawnSpec.CapitalBattleship, list[0].capital);
            Assert.IsTrue(list[0].freighter && list[0].stationary);
            Assert.AreEqual(-1, list[0].hitpoints, "the createShip formula (kind 1 x25)");
            Assert.AreEqual(25 * 100, NpcTables.Hull(1, 14));
            // table 0x253604
            var offsets = new[] { new Vector3(5322, 1893, -15571), new Vector3(2356, 1115, 7324), new Vector3(0, -4261, 4876),
                                  new Vector3(-5322, 1893, -15571), new Vector3(0, 5669, -13872), new Vector3(-2356, 1115, 7324), new Vector3(0, 2855, 4161) };
            var rot = new[] { -Mathf.PI / 2, -Mathf.PI / 2, Mathf.PI, Mathf.PI / 2, 0f, Mathf.PI / 2, 0f };
            for (int i = 0; i < 7; i++)
            {
                var t = list[i + 1];
                Assert.AreEqual(NpcGroup.Turret, t.group);
                Assert.AreEqual(host + offsets[i], t.position);
                Assert.AreEqual(new Vector3(0, 0, rot[i]), t.rotation);
                AssertPlainTurret(t, 0, list[0]);
            }
        }

        static void AssertPlainTurret(SpawnSpec t, int race, SpawnSpec host)
        {
            // Level::createStaticObject 0x1a74 / 0x1a76: 1000 HP, scale 6, name 1666, no loot, own race.
            Assert.AreEqual(1000, t.hitpoints);
            Assert.AreEqual(6f, t.scale);
            Assert.AreEqual(1666, t.nameText);
            Assert.IsTrue(t.noLoot && t.stationary);
            Assert.AreEqual(race, t.race);
            Assert.AreEqual(race == 1 ? "turret_003_static" : "turret_002_static", t.turretAssembly);
            Assert.IsTrue(t.capitalPart);
            Assert.AreSame(host, t.capitalHost);
            Assert.IsFalse(t.capitalEnhanced);
        }

        [Test]
        public void CarrierAndItsEightTurrets()
        {
            var list = Capital(SpawnSpec.CapitalCarrier, false);
            Assert.AreEqual(9, list.Count);
            var h = list[0];
            Assert.AreEqual("sn_carrier_terran_1", h.fixedObject);
            Assert.AreEqual(2005, h.collisionId);
            Assert.AreEqual(9999999, h.hitpoints);
            Assert.AreEqual(0f, h.hitRadius);
            Assert.AreEqual(0, h.race);
            Assert.IsTrue(h.noLoot);
            // table 0x253544
            var offsets = new[] { new Vector3(-6726, 2458, -66), new Vector3(5824, 2458, 10243), new Vector3(-6726, 2458, 2100), new Vector3(5824, 2458, 8437),
                                  new Vector3(-5549, -2743, 11954), new Vector3(-5549, -2743, -3711), new Vector3(4981, -2743, 14659), new Vector3(0, 2458, -32811) };
            for (int i = 0; i < 8; i++) { Assert.AreEqual(offsets[i], list[i + 1].position); AssertPlainTurret(list[i + 1], 0, h); }
            Assert.AreEqual(new Vector3(Mathf.PI, -2.1817f, 0f), list[7].rotation);
        }

        [Test]
        public void VosskBattleshipAndItsFiveTurrets()
        {
            var list = Capital(SpawnSpec.CapitalVossk, false);
            Assert.AreEqual(6, list.Count);
            var h = list[0];
            Assert.AreEqual("sn_battleship_vossk", h.fixedObject);
            Assert.AreEqual(2006, h.collisionId);
            Assert.AreEqual(1667, h.nameText);
            Assert.AreEqual(9999999, h.hitpoints);
            Assert.AreEqual(1, h.race);
            // table 0x2536ac, rotation 0 for all
            var offsets = new[] { new Vector3(10283, -1123, 26039), new Vector3(-10171, -1083, 25907), new Vector3(-15624, -787, -7569),
                                  new Vector3(15624, -787, -7569), new Vector3(0, 4901, 3084) };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(offsets[i], list[i + 1].position);
                Assert.AreEqual(Vector3.zero, list[i + 1].rotation);
                AssertPlainTurret(list[i + 1], 1, h);
            }
        }

        // ---- the enhancements -----------------------------------------------------------------------------------

        [TestCase(SpawnSpec.CapitalBattleship, 5, 60000, 7)]
        [TestCase(SpawnSpec.CapitalCarrier, 7, 96000, 8)]
        [TestCase(SpawnSpec.CapitalVossk, 5, 96000, 5)]
        public void EnhancedHostHullAndEscortsAtRank20(int kind, int escorts, int hull, int turrets)
        {
            // rank 20 in the won game: the battleship's 25 x (180 + 280 + 20) = 12 000, x5 / x8; escorts 3 + 20 / 7 (+2 carrier)
            InCompletedWorld(20);
            Assert.AreEqual(12000, NpcTables.Hull(1, 14));
            Random.InitState(1);
            var list = Capital(kind, true);
            var h = list[0];
            Assert.IsTrue(h.capitalEnhanced);
            Assert.AreEqual(hull, h.hitpoints);
            Assert.AreEqual(turrets, Turrets(list).Count);
            var esc = Escorts(list);
            Assert.AreEqual(escorts, esc.Count);
            int race = kind == SpawnSpec.CapitalVossk ? 1 : 0;
            foreach (var e in esc)
            {
                Assert.AreEqual(race, e.race);
                Assert.AreEqual((int)(NpcTables.Hull(0, e.ship) * 1.5f), e.hitpoints);
                Assert.AreEqual(480 * 3 / 2, e.hitpoints, "a fighter's 180 + 280 + 20, x1.5");
                Assert.AreEqual(1.3f, e.gunFactor);
                Assert.AreEqual(NpcTables.GunItem(race), e.gunItem);
                Assert.AreSame(h, e.capitalHost);
                Assert.AreSame(h, e.routeAround);
                Assert.IsTrue(e.capitalPart && e.capitalEnhanced);
                Assert.IsTrue(e.route.loop);
                Assert.AreEqual(4, e.route.points.Count);
            }
        }

        [Test]
        public void EnhancedDeckShipsResupplyAndExplode()
        {
            InCompletedWorld(20);
            foreach (int kind in new[] { SpawnSpec.CapitalCarrier, SpawnSpec.CapitalVossk })
            {
                var h = Capital(kind, true)[0];
                Assert.AreEqual(NpcTables.Emp(1) * 3, h.empPoints);
                Assert.AreEqual((5 * 20 + 40) * 9, h.empPoints, "EMP x3 a freighter's");
                Assert.AreEqual(8000f, h.deathMs);
                Assert.AreEqual(14f, h.explosionScale);
                Assert.IsTrue(h.noLoot);
                Assert.AreEqual(ObjectDocking.Resupply, h.dockingType);
                Assert.AreEqual(kind == SpawnSpec.CapitalVossk ? 8 : 5, h.spacePoints);
                Assert.IsTrue(CapitalShips.Killable(h));
                Assert.IsTrue(CapitalShips.HasDeck(h));
            }
            var bs = Capital(SpawnSpec.CapitalBattleship, true)[0];
            Assert.IsFalse(CapitalShips.Killable(bs), "the battleship was always killable: not one of the new ones");
            Assert.IsFalse(CapitalShips.HasDeck(bs));
            Assert.AreEqual(-1, bs.empPoints);
            Assert.IsFalse(CapitalShips.Killable(Capital(SpawnSpec.CapitalCarrier, false)[0]), "only enhanced");
        }

        [TestCase(SpawnSpec.CapitalBattleship, 1000, 2f)]
        [TestCase(SpawnSpec.CapitalCarrier, 1000, 2f)]
        [TestCase(SpawnSpec.CapitalVossk, 1600, 3.2f)]
        public void EnhancedTurretsAtRank10(int kind, int hp, float gun)
        {
            // (500 + 50 x 10) x 1, gun 1 + 0.1 x 10; the Vossk battleship x1.6 both; every other turret a turret hunter.
            InFreePlay(10);
            var list = Capital(kind, true);
            var turrets = Turrets(list);
            for (int i = 0; i < turrets.Count; i++)
            {
                Assert.AreEqual(hp, turrets[i].hitpoints);
                Assert.AreEqual(gun, turrets[i].gunFactor, 1e-4f);
                Assert.IsTrue(turrets[i].capitalEnhanced);
                Assert.AreEqual(i % 2 == 1, turrets[i].turretHunter, "turret " + i);
            }
        }

        [Test]
        public void EnhancedTurretsScaleWithDifficulty()
        {
            InFreePlay(10);
            Session.Difficulty = Session.DifficultyExtreme;
            Assert.That(Turrets(Capital(SpawnSpec.CapitalBattleship, true)).All(t => t.hitpoints == 2000));
            Session.Difficulty = Session.DifficultyEasy;
            Assert.That(Turrets(Capital(SpawnSpec.CapitalBattleship, true)).All(t => t.hitpoints == 500));
        }

        [TestCase(Session.DifficultyEasy, 4)]
        [TestCase(Session.DifficultyNormal, 4)]
        [TestCase(Session.DifficultyHard, 5)]
        [TestCase(Session.DifficultyExtreme, 5)]
        public void EscortsPlusOneOnHardAndExtreme(float difficulty, int escorts)
        {
            InFreePlay(10);   // 3 + 10 / 7
            Session.Difficulty = difficulty;
            Random.InitState(2);
            Assert.AreEqual(escorts, Escorts(Capital(SpawnSpec.CapitalBattleship, true)).Count);
            Assert.AreEqual(escorts + 2, Escorts(Capital(SpawnSpec.CapitalCarrier, true)).Count);
        }

        [Test]
        public void EscortsAreTheDearerFighters()
        {
            InFreePlay(10);
            var dearer = Pool(0).OrderByDescending(s => Db.Ship(s).price).Take(6).ToArray();
            Random.InitState(3);
            for (int i = 0; i < 40; i++)
                foreach (var e in Escorts(Capital(SpawnSpec.CapitalCarrier, true))) CollectionAssert.Contains(dearer, e.ship);
            foreach (var e in Escorts(Capital(SpawnSpec.CapitalVossk, true))) Assert.AreEqual(9, e.ship, "the H'Soc before Valkyrie");
        }

        [Test]
        public void EscortLoopsAroundTheHost()
        {
            // four corners (30000, 6000, 0), (0, 9000, 50000), (-30000, 6000, 0), (0, 9000, -50000), x0.6 for the battleship,
            // each escort starting from its own corner.
            var host = new SpawnSpec { capital = SpawnSpec.CapitalCarrier };
            var c = new Vector3(100, 200, 300);
            var loop = CapitalShips.LoopAround(host, c, 0);
            Assert.IsTrue(loop.loop);
            CollectionAssert.AreEqual(new[] { c + new Vector3(30000, 6000, 0), c + new Vector3(0, 9000, 50000), c + new Vector3(-30000, 6000, 0), c + new Vector3(0, 9000, -50000) }, loop.points);
            var shifted = CapitalShips.LoopAround(host, c, 1);
            Assert.AreEqual(loop.points[1], shifted.points[0]);
            Assert.AreEqual(loop.points[0], shifted.points[3]);
            var bs = CapitalShips.LoopAround(new SpawnSpec { capital = SpawnSpec.CapitalBattleship }, Vector3.zero, 0);
            Assert.Less((bs.points[0] - new Vector3(18000, 3600, 0)).magnitude, 0.01f);
            Assert.Less((bs.points[1] - new Vector3(0, 5400, 30000)).magnitude, 0.01f);
        }

        [Test]
        public void DecksLaunchTheirRacesFighters()
        {
            var carrier = new SpawnSpec { capital = SpawnSpec.CapitalCarrier };
            var vossk = new SpawnSpec { capital = SpawnSpec.CapitalVossk };
            Assert.AreEqual(5, CapitalShips.LaunchShip(carrier), "Inflict");
            Assert.AreEqual(9, CapitalShips.LaunchShip(vossk), "H'Soc");
            Assert.AreEqual(5, CapitalShips.PadSet(carrier));
            Assert.AreEqual(8, CapitalShips.PadSet(vossk));
            Assert.AreEqual(15, CapitalShips.CarrierInflicts);
            Assert.AreEqual(5, CapitalShips.InflictsPerWave);
            Assert.AreEqual(20000f, CapitalShips.WaveMs);
            Assert.IsFalse(CapitalShips.HasDeck(null));
            Assert.IsFalse(CapitalShips.Killable(null));
        }

        [TestCase(0, 30000)]
        [TestCase(10, 60000)]
        [TestCase(20, 90000)]
        public void FleetBattleBounty(int rank, int bounty)
        {
            InFreePlay(rank);
            Assert.AreEqual(bounty, CapitalShips.BattleBounty);
        }

        [Test]
        public void FleetBattleLayout()
        {
            // a Terran carrier or battleship and the Vossk battleship 80 000 units apart across the centre, both enhanced,
            // each with a wing of 4 + rank / 5 looping round the other.
            InCompletedWorld(10);
            Random.InitState(4);
            var kinds = new HashSet<int>();
            for (int k = 0; k < 20; k++)
            {
                var list = new List<SpawnSpec>();
                var centre = new Vector3(0f, 0f, 130000f);
                TrafficPlan.AddFleetBattle(Db, list, 70, centre);
                var hosts = list.Where(x => x.capital != 0 && x.capitalHost == null).ToList();
                Assert.AreEqual(2, hosts.Count);
                var terran = hosts[0];
                var vossk = hosts[1];
                kinds.Add(terran.capital);
                Assert.AreEqual(SpawnSpec.CapitalVossk, vossk.capital);
                Assert.That(terran.capital == SpawnSpec.CapitalCarrier || terran.capital == SpawnSpec.CapitalBattleship);
                Assert.AreEqual(80000f, (terran.position - vossk.position).magnitude, 1f);
                Assert.AreEqual(centre, (terran.position + vossk.position) / 2f);
                Assert.AreEqual(0f, terran.position.y - vossk.position.y);
                Assert.IsTrue(terran.fleetBattle && vossk.fleetBattle && terran.capitalEnhanced && vossk.capitalEnhanced);
                Assert.AreSame(vossk, terran.battleFoe);
                Assert.AreSame(terran, vossk.battleFoe);
                foreach (var own in hosts)
                {
                    var wing = list.Where(x => x.routeAround == own.battleFoe && x.capitalHost == own).ToList();
                    Assert.AreEqual(4 + 10 / 5, wing.Count);
                    foreach (var w in wing)
                    {
                        Assert.AreEqual(own.race, w.race);
                        Assert.AreEqual(NpcGroup.Escort, w.group);
                        Assert.IsTrue(w.route.loop);
                        var d = w.position - own.position;
                        Assert.That(Mathf.Abs(d.x) <= 15000 && Mathf.Abs(d.y) <= 6000 && Mathf.Abs(d.z) <= 20000, d.ToString());
                    }
                }
            }
            CollectionAssert.AreEquivalent(new[] { SpawnSpec.CapitalCarrier, SpawnSpec.CapitalBattleship }, kinds.ToArray());
        }

        [Test]
        public void BattleConstants()
        {
            Assert.AreEqual(3, CapitalShips.FleetBattleChance);
            Assert.AreEqual(80000f, CapitalShips.BattleSeparationUnits);
            Assert.AreEqual(60000f, CapitalShips.BattleLeashUnits);
            Assert.AreEqual(21000f, CapitalShips.BattleLaneUnits);
            Assert.AreEqual(0.35f, CapitalShips.BattleSpeed);
            Assert.AreEqual(6, CapitalShips.MissilesPerSalvo);
            Assert.AreEqual(37, CapitalShips.TerranMissile);
            Assert.AreEqual(38, CapitalShips.VosskMissile);
            Assert.AreEqual(40000f, CapitalShips.MissileRangeUnits);
        }

        // ---- the loot -------------------------------------------------------------------------------------------

        [Test]
        public void CapitalLootOnceAnHour()
        {
            Random.InitState(5);
            Session.CapitalLootReadyAt = Session.PlaySeconds + 3600f;
            Assert.IsFalse(CapitalShips.LootReady);
            Assert.AreEqual(0, CapitalShips.RollLoot(Db).Count);
            Session.CapitalLootReadyAt = 0f;
            Assert.IsTrue(CapitalShips.LootReady);
            for (int i = 0; i < 100; i++)
            {
                var loot = CapitalShips.RollLoot(Db);
                Assert.AreEqual(4, loot.Count);
                for (int k = 0; k < 2; k++)
                {
                    var it = Db.Item(loot[k].item);
                    Assert.GreaterOrEqual(it.techLevel, 5);
                    Assert.LessOrEqual(it.TypeId, 3);
                    Assert.AreEqual(0, it.blueprint.Count);
                    if (it.TypeId == 1) Assert.That(loot[k].amount >= 10 && loot[k].amount <= 20);
                    else Assert.AreEqual(1, loot[k].amount);
                }
                Assert.AreNotEqual(loot[0].item, loot[1].item);
                CollectionAssert.Contains(new[] { 113, 108, 107, 102, 101 }, loot[2].item);
                Assert.That(loot[2].amount >= 8 && loot[2].amount <= 15);
                Assert.AreEqual(GalaxyMap.EnergyCellItem, loot[3].item);
                Assert.That(loot[3].amount >= 5 && loot[3].amount <= 10);
            }
            Assert.AreEqual(3600f, CapitalShips.LootCooldownSeconds);
        }

        [Test]
        public void CapitalLootSkipsUnsaleableItems()
        {
            Random.InitState(6);
            var pool = Db.Items.Where(it => it.maxPrice > 0 && it.blueprint.Count == 0 && it.TypeId <= 3 && it.techLevel >= 5 && it.occurrence > 0).Select(it => it.index).ToList();
            foreach (int i in pool.Skip(2)) Session.Unsaleable.Add(i);
            for (int k = 0; k < 20; k++)
            {
                var loot = CapitalShips.RollLoot(Db);
                CollectionAssert.AreEquivalent(pool.Take(2).ToArray(), loot.Take(2).Select(x => x.item).ToArray());
            }
        }

        // ---- the resupply store ----------------------------------------------------------------------------------

        [Test]
        public void RaceItemsByAttribute60()
        {
            foreach (var it in Db.Items)
            {
                bool vossk = it.Attr(60) == 1;
                Assert.AreEqual(vossk, CapitalShips.RaceItem(it, 1), it.name);
                Assert.AreEqual(!vossk, CapitalShips.RaceItem(it, 0), it.name);
            }
            Assert.IsFalse(CapitalShips.RaceItem(null, 0));
            Assert.IsFalse(CapitalShips.IsBlueprintProduct(null));
            Assert.IsTrue(Db.Items.Any(CapitalShips.IsBlueprintProduct), "the blueprint products (items.json ingredients)");
        }

        [Test]
        public void StoreStockPerItem()
        {
            // energy cells 10-15, a secondary of its race 5-10 rounds, a blueprint product or another race's weapon none;
            // the roll is kept for the visit.
            Random.InitState(7);
            var cellsSeen = new HashSet<int>();
            var ammoSeen = new HashSet<int>();
            var terranMissile = Db.Items.First(it => it.TypeId == 1 && CapitalShips.RaceItem(it, 0) && !CapitalShips.IsBlueprintProduct(it));
            var vosskWeapon = Db.Items.First(it => it.TypeId <= 3 && it.Attr(60) == 1);
            var product = Db.Items.First(CapitalShips.IsBlueprintProduct);
            for (int i = 0; i < 300; i++)
            {
                var store = new CapitalShips.Store(0);
                int cells = store.Left(Db, GalaxyMap.EnergyCellItem);
                Assert.That(cells >= 10 && cells <= 15);
                Assert.AreEqual(cells, store.Left(Db, GalaxyMap.EnergyCellItem), "kept");
                cellsSeen.Add(cells);
                int ammo = store.Left(Db, terranMissile.index);
                Assert.That(ammo >= 5 && ammo <= 10);
                ammoSeen.Add(ammo);
                Assert.AreEqual(0, store.Left(Db, vosskWeapon.index), "another race's weapon");
                Assert.AreEqual(0, store.Left(Db, product.index), "a blueprint product");
                store.Take(Db, GalaxyMap.EnergyCellItem, 3);
                Assert.AreEqual(cells - 3, store.Left(Db, GalaxyMap.EnergyCellItem));
                store.Take(Db, GalaxyMap.EnergyCellItem, 100);
                Assert.AreEqual(0, store.Left(Db, GalaxyMap.EnergyCellItem));
            }
            Assert.AreEqual(6, cellsSeen.Count);
            Assert.AreEqual(6, ammoSeen.Count);
        }

        [TestCase(1, 5)]
        [TestCase(0, 6)]
        public void StoreGear(int race, int count)
        {
            Random.InitState(8 + race);
            var store = new CapitalShips.Store(race);
            var gear = store.Gear(Db);
            Assert.AreEqual(count, gear.Count);
            Assert.AreSame(gear, store.Gear(Db), "rolled once");
            Assert.AreEqual(gear.Count, gear.Distinct().Count());
            foreach (int i in gear)
            {
                var it = Db.Item(i);
                Assert.LessOrEqual(it.TypeId, 3);
                Assert.Greater(it.maxPrice, 0);
                Assert.Greater(it.occurrence, 0);
                Assert.IsFalse(CapitalShips.IsBlueprintProduct(it));
                Assert.AreEqual(race == 1, it.Attr(60) == 1);
                Assert.AreNotEqual(85, i);
                Assert.That(i < 189 || i > 192, "no signature");
                if (race == 0) Assert.GreaterOrEqual(it.techLevel, 4);
                int left = store.Left(Db, i);
                if (it.TypeId == 1) Assert.That(left >= 10 && left <= 20);
                else Assert.That(left >= 1 && left <= 2);
            }
        }

        [Test]
        public void ResupplyOffersTheMountedSecondariesAndCells()
        {
            // the new game's equipment: 6 Edo (36) mounted; Terran store: rounds at the station's price x1.25, up to 50.
            Random.InitState(9);
            var store = new CapitalShips.Store(0);
            var offers = CapitalShips.ResupplyOffers(Db, null, store);
            Assert.IsFalse(offers.Any(o => o.kind == CapitalShips.OfferKind.Repair), "no health: no repair row");
            var edo = offers.Single(o => o.kind == CapitalShips.OfferKind.Ammo);
            Assert.AreEqual(36, edo.item);
            Assert.AreEqual(6, edo.have);
            Assert.IsFalse(edo.wrongRace || edo.notStocked);
            Assert.That(edo.stock >= 5 && edo.stock <= 10);
            Assert.AreEqual(Mathf.Min(50 - 6, edo.stock), edo.room);
            int price = Shop.PriceList(Db, Session.StationIndex, new[] { 36 })[0];
            Assert.AreEqual(Mathf.RoundToInt(price * 1.25f), edo.unitPrice);
            Assert.AreEqual(edo.unitPrice, CapitalShips.UnitPrice(Db, 36));
            var cells = offers.Last();
            Assert.AreEqual(CapitalShips.OfferKind.Cargo, cells.kind);
            Assert.AreEqual(GalaxyMap.EnergyCellItem, cells.item);
            Assert.AreEqual(Mathf.Min(Shop.FreeCargo(Db), cells.stock), cells.room);
            Assert.AreEqual(6, store.Gear(Db).Count, "the carrier's 6 pieces of gear");
            Assert.AreEqual(store.Gear(Db).Count(i => i != 36), offers.Count(o => o.kind == CapitalShips.OfferKind.Gear), "a mounted secondary keeps its rounds row");
            Assert.AreEqual(50, CapitalShips.AmmoCap);
        }

        [Test]
        public void VosskStoreDoesNotStockTerranRounds()
        {
            Random.InitState(10);
            var edo = CapitalShips.ResupplyOffers(Db, null, new CapitalShips.Store(1)).Single(o => o.kind == CapitalShips.OfferKind.Ammo);
            Assert.IsTrue(edo.wrongRace);
            Assert.IsTrue(edo.notStocked);
            Assert.AreEqual(0, edo.stock);
            Assert.AreEqual(0, edo.room);
            Session.Credits = 1000000;
            string msg = CapitalShips.Buy(Db, null, null, edo, 5, out int bought);
            Assert.AreEqual(0, bought);
            Assert.IsNotEmpty(msg);
            Assert.AreEqual(1000000, Session.Credits);
        }

        [Test]
        public void UnlimitedWithoutAStore()
        {
            var offers = CapitalShips.ResupplyOffers(Db, null, null);
            var edo = offers.Single(o => o.kind == CapitalShips.OfferKind.Ammo);
            Assert.AreEqual(-1, edo.stock);
            Assert.AreEqual(44, edo.room);
            Assert.IsFalse(offers.Any(o => o.kind == CapitalShips.OfferKind.Gear));
        }

        [Test]
        public void BuyingRoundsAsManyAsTheCreditsAllow()
        {
            Random.InitState(11);
            var store = new CapitalShips.Store(0);
            var edo = CapitalShips.ResupplyOffers(Db, null, store).Single(o => o.kind == CapitalShips.OfferKind.Ammo);
            int stock = edo.stock;
            Session.Credits = edo.unitPrice * 3 + edo.unitPrice / 2;
            string msg = CapitalShips.Buy(Db, null, store, edo, 10, out int bought);
            Assert.AreEqual(3, bought, msg);
            Assert.AreEqual(edo.unitPrice / 2, Session.Credits);
            Assert.AreEqual(9, Session.Equipment.Find(e => e.item == 36).amount);
            Assert.AreEqual(stock - 3, store.Left(Db, 36));
            Assert.AreEqual(9, edo.have);
            Assert.AreEqual(stock - 3, edo.stock);
            msg = CapitalShips.Buy(Db, null, store, edo, 1, out bought);
            Assert.AreEqual(0, bought);
            Assert.AreEqual(CapitalShips.NotEnough, msg);
        }

        [Test]
        public void BuyingCellsGoesIntoTheHoldUpToTheStore()
        {
            Random.InitState(12);
            var store = new CapitalShips.Store(0);
            var cells = CapitalShips.ResupplyOffers(Db, null, store).Last();
            int stock = cells.stock, free = Shop.FreeCargo(Db);
            Assume.That(free >= 15, "the Phantom's 52 t hold");
            Session.Credits = int.MaxValue / 2;
            CapitalShips.Buy(Db, null, store, cells, 100, out int bought);
            Assert.AreEqual(stock, bought, "the store's 10-15 cells limit it");
            Assert.AreEqual(bought, Shop.CargoOf(GalaxyMap.EnergyCellItem));
            Assert.AreEqual(0, store.Left(Db, GalaxyMap.EnergyCellItem));
            CapitalShips.Buy(Db, null, store, cells, 1, out bought);
            Assert.AreEqual(0, bought, "sold out");
        }
    }
}
