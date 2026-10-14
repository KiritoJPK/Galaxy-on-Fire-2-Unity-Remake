// NpcGunTests.cs
// Feature: CLAUDE.md "Weapons" (Gun: one weapon on one mount, bullet pool, attr 11 reload, attr 9 damage, attr 13 speed in
// u/ms, attr 12 "range" = lifetime in ms, rockets coast 2 s past their lifetime) and "NPCs and combat" > NPC stats (the NPC
// gun: 4 bullets, 16 u/ms, 3000 ms): the plain-C# parts of Flight.Gun (construction, TryFire, Update without targets,
// Inject).
// Expected values: Reference/research/weapons.md 2 (Level::createGun's pool per sort: 20 lasers / blasters / thermo, 25
// auto-cannon / scatter, 15 turret / plasma collector, 5 rockets / missiles, item - 211 cluster, 1 bomb / shock blast, 10
// mines, 3 sentries; beams 9 / 10 / 11 / 228 one; spread error 2 / 20; mines and sentries speed 2), 3.2 (Gun::shootAt:
// spawn at mount + (0, 0, 100), velocity = forward x speed, error uniform in [-err/200, err/200) per axis), CLAUDE.md
// (lifetime = attr 12, speed = attr 13, the 2000 ms coast). Item attributes are read from the game's tables (the economy
// may change them), so the tests check the mapping, not a number.

using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class NpcGunTests : NpcTestBase
    {
        const float M = 0.05f;   // metres per game unit

        static ItemData ItemOf(int categoryId) => Db.Items.First(it => it.categoryId == categoryId && !it.modded && !new[] { 9, 10, 11, 228 }.Contains(it.index));
        static Gun GunOf(int item, bool secondary = false) => new Gun(Db.Item(item), Vector3.zero, secondary);

        [TestCase(0, Gun.Kind.Laser, 20)]
        [TestCase(1, Gun.Kind.Blaster, 20)]
        [TestCase(2, Gun.Kind.AutoCannon, 25)]
        [TestCase(3, Gun.Kind.Thermo, 20)]
        [TestCase(4, Gun.Kind.Rocket, 5)]
        [TestCase(5, Gun.Kind.Missile, 5)]
        [TestCase(6, Gun.Kind.EmpBomb, 1)]
        [TestCase(7, Gun.Kind.Nuke, 1)]
        [TestCase(8, Gun.Kind.Turret, 15)]
        [TestCase(11, Gun.Kind.Mine, 10)]
        [TestCase(25, Gun.Kind.ScatterGun, 25)]
        [TestCase(34, Gun.Kind.Ionizing, 1)]
        [TestCase(39, Gun.Kind.Sentry, 3)]
        [TestCase(42, Gun.Kind.ShockBlast, 1)]
        public void KindAndPoolPerSort(int sort, Gun.Kind kind, int pool)
        {
            var it = ItemOf(sort);
            var g = new Gun(it, Vector3.zero, it.TypeId == 1);
            Assert.AreEqual(kind, g.kind);
            Assert.AreEqual(sort, g.categoryId);
            Assert.AreEqual(pool, g.bullets.Length, it.name);
            Assert.IsFalse(g.isBeam);
        }

        [Test]
        public void PlasmaCollectorPoolIs15()
        {
            // weapons.md 2: "8 Turret, 35 Plasma collector | N 15"; Gun's pool switch has no case for sort 35 (20).
            var g = new Gun(ItemOf(35), Vector3.zero, false);
            Assert.AreEqual(Gun.Kind.PlasmaCollector, g.kind);
            Assert.AreEqual(15, g.bullets.Length);
        }

        [TestCase(214, 3)]
        [TestCase(215, 4)]
        [TestCase(216, 5)]
        public void ClusterPoolIsItemMinus211(int item, int pool)
        {
            var g = GunOf(item, true);
            Assert.AreEqual(Gun.Kind.ClusterMissile, g.kind);
            Assert.AreEqual(pool, g.bullets.Length);
            Assert.IsTrue(g.Homing);
        }

        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(228)]
        public void BeamLasersAreOneShotHitscan(int item)
        {
            var g = GunOf(item);
            Assert.IsTrue(g.isBeam);
            Assert.AreEqual(1, g.bullets.Length);
            Assert.AreEqual(0, g.TryFire(Vector3.zero, Quaternion.identity));
            Assert.AreEqual(Gun.BeamRangeUnits, g.BeamLengthUnits, "no target: parked 30000 ahead");
            Assert.AreEqual(30000f, Gun.BeamRangeUnits);
            Assert.AreEqual(Vector3.forward, g.BeamDir);
            Assert.IsNull(g.BeamTarget);
        }

        [Test]
        public void StatsComeFromTheAttributes()
        {
            foreach (int i in new[] { 0, 2, 7, 13, 19, 22, 28, 31, 36, 37, 48, 180, 211 })
            {
                var it = Db.Item(i);
                var g = GunOf(i, it.TypeId == 1);
                Assert.AreEqual(it.Attr(9), g.damage, it.name + " damage = attr 9");
                Assert.AreEqual(it.Attr(10), g.emp, it.name + " EMP = attr 10");
                Assert.AreEqual(Mathf.Max(1, it.Attr(11, 500)), g.reloadMs, it.name + " reload = attr 11");
                Assert.AreEqual(it.Attr(12, 2000), g.lifetimeMs, it.name + " lifetime = attr 12");
                Assert.AreEqual(it.Attr(13, 20), g.speedUnitsPerMs, it.name + " speed = attr 13");
                Assert.AreEqual(it.Attr(14), g.magnitude, it.name + " blast = attr 14");
                Assert.AreEqual(i, g.itemIndex);
                Assert.AreEqual(i, g.lookIndex);
            }
        }

        [Test]
        public void ShockBlastAndMineSpecials()
        {
            var shock = GunOf(226, true);
            Assert.AreEqual(1f, shock.lifetimeMs);
            Assert.AreEqual(0f, shock.speedUnitsPerMs);
            Assert.AreEqual(80000f, shock.magnitude);
            Assert.AreEqual(2f, new Gun(ItemOf(11), Vector3.zero, true).speedUnitsPerMs, "mines drop at 2 u/ms");
        }

        [Test]
        public void SpreadPerSort()
        {
            Assert.AreEqual(2f, new Gun(ItemOf(2), Vector3.zero, false).spreadError);
            Assert.AreEqual(2f, new Gun(ItemOf(25), Vector3.zero, false).spreadError);
            Assert.AreEqual(20f, new Gun(ItemOf(3), Vector3.zero, false).spreadError);
            Assert.AreEqual(0f, GunOf(0).spreadError);
            Assert.AreEqual(0f, GunOf(13).spreadError);
        }

        [Test]
        public void HomingAndBombFlags()
        {
            Assert.IsTrue(new Gun(ItemOf(5), Vector3.zero, true).Homing);
            Assert.IsTrue(new Gun(ItemOf(3), Vector3.zero, false).Homing, "thermo projectiles home");
            Assert.IsFalse(new Gun(ItemOf(4), Vector3.zero, true).Homing);
            Assert.IsFalse(GunOf(0).Homing);
            Assert.IsTrue(new Gun(ItemOf(6), Vector3.zero, true).IsBomb);
            Assert.IsTrue(new Gun(ItemOf(7), Vector3.zero, true).IsBomb);
            Assert.IsTrue(new Gun(ItemOf(34), Vector3.zero, true).IsBomb);
            Assert.IsFalse(new Gun(ItemOf(5), Vector3.zero, true).IsBomb);
        }

        // ---- firing --------------------------------------------------------------------------------------------

        [Test]
        public void ShotSpawnsAheadOfTheMountAndFliesAlongTheNose()
        {
            var mount = new Vector3(1f, 2f, 3f);
            var g = new Gun(Db.Item(0), mount, false);
            var origin = new Vector3(10f, 20f, 30f);
            var rot = Quaternion.Euler(0f, 90f, 0f);
            Assert.IsTrue(g.Ready, "ready at the start");
            int i = g.TryFire(origin, rot);
            Assert.AreEqual(0, i);
            var b = g.bullets[i];
            var expect = origin + rot * (mount + new Vector3(0f, 0f, 100f * M));
            Assert.Less((b.position - expect).magnitude, 1e-4f);
            Assert.Less((b.velocity - rot * Vector3.forward * g.speedUnitsPerMs * M).magnitude, 1e-6f);
            Assert.AreEqual(g.lifetimeMs, b.timer);
            Assert.IsTrue(g.IsActive(i));
            Assert.IsFalse(g.Ready, "reloading");
            Assert.AreEqual(-1, g.TryFire(origin, rot));
        }

        [Test]
        public void ReloadTime()
        {
            var g = GunOf(0);
            g.TryFire(Vector3.zero, Quaternion.identity);
            g.Update(g.reloadMs - 1f, null, null);
            Assert.IsFalse(g.Ready);
            g.Update(2f, null, null);
            Assert.IsTrue(g.Ready, "reloadAcc > attr 11");
            Assert.AreEqual(g.IsActive(0) ? 1 : 0, g.TryFire(Vector3.zero, Quaternion.identity), "the first free bullet");
        }

        [Test]
        public void BulletsMoveAndExpireAtTheirLifetime()
        {
            var g = GunOf(0);
            int i = g.TryFire(Vector3.zero, Quaternion.identity);
            var start = g.bullets[i].position;
            g.Update(100f, null, null);
            Assert.AreEqual(g.speedUnitsPerMs * M * 100f, (g.bullets[i].position - start).z, 1e-3f);
            g.Update(g.lifetimeMs - 101f, null, null);
            Assert.IsTrue(g.IsActive(i));
            g.Update(2f, null, null);
            Assert.IsFalse(g.IsActive(i), "attr 12 is a time in ms");
        }

        [Test]
        public void RocketsCoastTwoSecondsPastTheirLifetime()
        {
            var g = GunOf(31, true);
            Assert.AreEqual(Gun.Kind.Rocket, g.kind);
            int expired = -1;
            g.Expired += k => expired = k;
            int i = g.TryFire(Vector3.zero, Quaternion.identity);
            g.Update(g.lifetimeMs + 1000f, null, null);
            Assert.IsTrue(g.IsActive(i), "still flying (and hitting)");
            Assert.AreEqual(-1, expired);
            g.Update(1001f, null, null);
            Assert.IsFalse(g.IsActive(i));
            Assert.AreEqual(i, expired);
        }

        [Test]
        public void PoolLimitsShotsInFlight()
        {
            var g = GunOf(0);
            for (int k = 0; k < 20; k++)
            {
                g.reloadAcc = g.reloadMs + 1f;
                Assert.AreEqual(k, g.TryFire(Vector3.zero, Quaternion.identity));
            }
            g.reloadAcc = g.reloadMs + 1f;
            Assert.AreEqual(-1, g.TryFire(Vector3.zero, Quaternion.identity), "all 20 in flight");
            g.Update(g.lifetimeMs + 1f, null, null);
            g.reloadAcc = g.reloadMs + 1f;
            Assert.AreEqual(0, g.TryFire(Vector3.zero, Quaternion.identity), "free again");
        }

        [Test]
        public void ClusterMissilesFireTheWholeSalvo()
        {
            var g = GunOf(216, true);
            int fired = 0;
            g.Fired += (gun, k) => fired++;
            Assert.AreEqual(0, g.TryFire(Vector3.zero, Quaternion.identity));
            Assert.AreEqual(1, fired, "one salvo, one event");
            for (int k = 0; k < g.bullets.Length; k++) Assert.IsTrue(g.IsActive(k));
        }

        [Test]
        public void AutoCannonSpread()
        {
            // err 2: each axis += rnd(2) * 0.01 - 0.01, i.e. -0.01 or 0
            var g = new Gun(ItemOf(2), Vector3.zero, false);
            Random.InitState(3);
            var xs = new System.Collections.Generic.HashSet<float>();
            for (int k = 0; k < 100; k++)
            {
                g.reloadAcc = g.reloadMs + 1f;
                int i = g.TryFire(Vector3.zero, Quaternion.identity);
                if (i < 0) { g.Update(g.lifetimeMs + 1f, null, null); continue; }
                var v = g.bullets[i].velocity.normalized;
                float dx = v.x / v.z, dy = v.y / v.z;
                Assert.That(dx >= -0.0101f && dx < 0.0101f && dy >= -0.0101f && dy < 0.0101f, v.ToString());
                xs.Add(Mathf.Round(dx * 1000f));
            }
            Assert.Greater(xs.Count, 1, "it does spread");
        }

        [Test]
        public void NpcGun()
        {
            // Level::assignGuns: 4 bullets, 16 u/ms, 3000 ms, the race's reload and damage, the item only the look.
            InFreePlay(10);
            var g = new Gun(Db.Item(NpcTables.GunItem(0)), NpcTables.GunDamage(0), NpcTables.GunReloadMs, NpcTables.GunPool, NpcTables.GunLifetimeMs, NpcTables.GunSpeed);
            Assert.AreEqual(4, g.bullets.Length);
            Assert.AreEqual(9f, g.damage);
            Assert.AreEqual(560f, g.reloadMs);
            Assert.AreEqual(3000f, g.lifetimeMs);
            Assert.AreEqual(16f, g.speedUnitsPerMs);
            Assert.AreEqual(Vector3.zero, g.mountLocal);
            Assert.IsTrue(g.Ready, "the first shot is ready");
            Assert.AreEqual(1f, new Gun(Db.Item(0), 5f, 0f, 4, 3000f, 16f).reloadMs, "reload at least 1 ms");
        }

        [Test]
        public void InjectUsesAFreeBullet()
        {
            var g = new Gun(Db.Item(0), 1f, 500f, 2, 3000f, 16f);
            Assert.IsTrue(g.Inject(Vector3.one, Vector3.forward, Vector3.up, 1500f));
            Assert.IsTrue(g.Inject(Vector3.one, Vector3.forward, Vector3.up, 1500f));
            Assert.IsFalse(g.Inject(Vector3.one, Vector3.forward, Vector3.up, 1500f), "the pool is full");
            Assert.AreEqual(1500f, g.bullets[0].timer);
            g.Update(1501f, null, null);
            Assert.IsFalse(g.IsActive(0));
            Assert.IsTrue(g.Inject(Vector3.zero, Vector3.forward, Vector3.up, 100f));
        }
    }
}
