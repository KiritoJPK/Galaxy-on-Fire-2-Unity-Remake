// NpcTurretTests.cs
// Feature: CLAUDE.md "NPCs and combat" > generic turrets: any turret item on any ship (Flight.TurretCore.ForItem, the item's
// *_ship_mounted model from Resources/GoF2Weapons) and the hangar's still copy (TurretCore.Display, CutScene::checkForTurret:
// hangar_turret_item_N turned (0, pi, 0), the plasma collectors 198-200 not); World.NpcMountedTurrets' multiplayer packing
// ("ship:item,item", NetProxy). Ship 28 Veteran has one turret mount (weapons_hd.json), 22 Groza none; 48 Hammerhead D2A2 and
// 181 are turret items, 198 a plasma collector.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class NpcTurretTests
    {
        readonly List<Object> made = new List<Object>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        Transform Ship()
        {
            var go = new GameObject("ship");
            made.Add(go);
            return go.transform;
        }

        [Test]
        public void AnyTurretItemOnAnyShipsMount()
        {
            var db = Database.Load();
            var mounts = db.MountsOf(28, 2);
            Assert.AreEqual(1, mounts.Count, "the Veteran's turret mount");
            var ship = Ship();
            foreach (int index in new[] { 47, 48, 181, 224 })
            {
                var item = db.Item(index);
                var core = TurretCore.ForItem(item, WeaponFx.Load(index), ship, mounts[0], ship, "Turret " + index, ship.forward);
                Assert.IsNotNull(core, $"{index}: its turret model");
                made.Add(core.Root.gameObject);
                Assert.IsTrue(core.Valid, $"{index}: pivot and gun found");
                Assert.AreSame(ship, core.Root.parent);
                Assert.AreEqual(WeaponSystem.MountToLocal(mounts[0]), core.Root.localPosition, $"{index}: on the mount");
                Assert.AreEqual(index, core.Item);
                Assert.IsNotNull(core.Gun, $"{index}: the item's own gun");
                Assert.AreEqual(index, core.Gun.itemIndex);
                Assert.AreEqual(item.Attr(9), core.Gun.damage, $"{index}: the item's damage");
                Assert.AreEqual(index == 48, core.alternateSides, "only the Hammerhead alternates");
                Assert.AreEqual(TurretCore.ShotOffsetUnits(item, null), core.shotOffsetUnits);
                Assert.AreEqual(item.Attr(17, 100) * 1.5f / 200f * TurretAim.RadPerMs, core.Aim.yawRatePerMs, 1e-9f);
                Assert.Greater(Vector3.Dot(core.BarrelForward, ship.forward), 0.99f, $"{index}: starts aimed at the nose");
            }
        }

        [Test]
        public void TheHangarsStillCopy()
        {
            var db = Database.Load();
            var ship = Ship();
            var root = TurretCore.DisplayAll(db, 28, new[] { 48 }, ship);
            Assert.IsNotNull(root);
            Assert.AreEqual("Turrets", root.name);
            Assert.AreEqual(1, root.transform.childCount);
            var t = root.transform.GetChild(0);
            Assert.AreEqual("Turret", t.name);
            Assert.AreEqual(WeaponSystem.MountToLocal(db.MountsOf(28, 2)[0]), t.localPosition);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 180f, 0f), t.localRotation), 0.01f, "turned (0, pi, 0) against the ship");
            foreach (var a in t.GetComponentsInChildren<Visuals.PartAnimation>(true)) Assert.AreEqual(0f, a.speed, "still");
            var collector = TurretCore.DisplayAll(db, 28, new[] { 198 }, Ship());
            Assert.IsNotNull(collector);
            Assert.Less(Quaternion.Angle(Quaternion.identity, collector.transform.GetChild(0).localRotation), 0.01f, "a plasma collector isn't turned");
            Assert.IsNull(TurretCore.DisplayAll(db, 22, new[] { 48 }, Ship()), "no turret mount, no turret");
            Assert.IsNull(TurretCore.DisplayAll(db, 28, new int[0], Ship()));
        }

        [Test]
        public void MultiplayerPacking()
        {
            Assert.AreEqual("28:48", NpcMountedTurrets.Pack(28, new[] { 48 }));
            Assert.AreEqual("", NpcMountedTurrets.Pack(28, new int[0]));
            Assert.IsTrue(NpcMountedTurrets.Unpack("51:48,181", out int ship, out var items));
            Assert.AreEqual(51, ship);
            CollectionAssert.AreEqual(new[] { 48, 181 }, items);
            Assert.IsFalse(NpcMountedTurrets.Unpack("", out _, out _));
            Assert.IsFalse(NpcMountedTurrets.Unpack("x:48", out _, out _));
            Assert.IsFalse(NpcMountedTurrets.Unpack("28:", out _, out _));
            Assert.IsFalse(NpcMountedTurrets.Unpack("28:1,2,3,4,5,6,7,8,9", out _, out _), "at most 8");
        }

        [Test]
        public void TheFighterTurretOfShips45And51()
        {
            Assert.AreEqual("turret_002_static", Traffic.FighterTurretAssembly(45));
            Assert.AreEqual("turret_002_static", Traffic.FighterTurretAssembly(51));
            Assert.IsNull(Traffic.FighterTurretAssembly(28));
            Assert.Less(Vector3.Distance(new Vector3(0f, 172.25f, -460.7f) * 0.05f, Traffic.FighterTurretLocal(45)), 1e-4f, "Level::createFighterTurrets");
            Assert.Less(Vector3.Distance(new Vector3(0f, 470f, -83f) * 0.05f, Traffic.FighterTurretLocal(51)), 1e-4f);
        }
    }
}
