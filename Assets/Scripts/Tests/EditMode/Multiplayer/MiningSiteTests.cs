// MiningSiteTests.cs
// Feature: CLAUDE.md "Multiplayer" > Bar missions, Ore Mining (remake): the mission's plant, guards, haulers and an
// asteroid field of its own sit at a remote site 9-11 km from the station (MiningSite), drawn from the mission's seed so
// every game of the squad builds the same site and field; the player's "Mining site" waypoint is short of the plant on the
// station's side, the field leaves the plant and the way in clear, and its asteroid indices (NetOrbit, NetState's
// destroyed lists) start past the orbit's own field.
// Expected values: the numbers MiningSite states (180 000 + 40 000 units, 12 000 short, 15 000 past, 5-7 big + 18-25
// small, indices 1000 + 100 per slot below 10 000, its field 140 000 from the orbit's, the plant 50 000 from a gate),
// two builds of one seed compared with each other.

using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class MiningSiteTests : SandboxTest
    {
        static FreelanceMission Mission(long netId) => new FreelanceMission
        {
            type = MissionType.OreMining, netId = netId, target = 70, clientStation = 78, good = 158, amount = 60, reward = 12000, difficulty = 4,
        };

        [Test]
        public void TheSquadSharesTheSite()
        {
            var db = Database.Load();
            var dis = OrbitLayout.Build(db, 70);
            var a = MiningSite.For(Mission(123456789012L), dis);
            var b = MiningSite.For(Mission(123456789012L), dis);
            Assert.AreEqual(a.seed, b.seed);
            Assert.AreEqual(a.plant, b.plant);
            Assert.AreEqual(a.bigCount, b.bigCount);
            Assert.AreEqual(a.smallCount, b.smallCount);
            Assert.AreNotEqual(a.plant, MiningSite.For(Mission(987654321L), dis).plant, "another squad's mission: another site");
            Assert.AreNotEqual(a.plant, MiningSite.For(Mission(123456789012L), OrbitLayout.Build(db, 71)).plant, "another orbit: another site");
            // Single player (no net id): the mission's own values.
            Assert.AreEqual(MiningSite.For(Mission(0), dis).plant, MiningSite.For(Mission(0), dis).plant);
        }

        [Test]
        public void TheSiteIsRemote()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var s = new MiningSite(seed * 7919 + 13);
                float km = s.plant.magnitude * OrbitLayout.MetersPerUnit / 1000f;
                Assert.That(km, Is.InRange(9f, 11.1f), "about 10 km from the station");
                Assert.That(Mathf.Abs(s.plant.y), Is.LessThanOrEqualTo(20000f));
                Assert.That(s.plant.magnitude * OrbitLayout.MetersPerUnit, Is.LessThan(1e6f), "inside NetGuard's coordinate range");
                Assert.AreEqual(MiningSite.ApproachUnits, (s.plant - s.waypoint).magnitude, 1f);
                Assert.Less(s.waypoint.magnitude, s.plant.magnitude, "the waypoint on the station's side");
                Assert.Greater(s.fieldCentre.magnitude, s.plant.magnitude, "the field beyond the plant");
                Assert.That(s.bigCount, Is.InRange(5, 7));
                Assert.That(s.smallCount, Is.InRange(18, 25));
                Assert.That(s.IndexBase, Is.InRange(MiningSite.SiteBase, 10000 - MiningSite.SlotSize));
                Assert.GreaterOrEqual(s.IndexBase, 80, "past the orbit's own field (at most 79)");
            }
        }

        [Test]
        public void ClearOfTheOrbitsFieldAndGates()
        {
            var db = Database.Load();
            int clear = 0, orbits = 0;
            for (int station = 0; station < 100; station++)
            {
                var layout = OrbitLayout.Build(db, station);
                var s = MiningSite.For(Mission(station * 7919L + 1), layout);
                orbits++;
                bool fieldClear = (s.fieldCentre - layout.asteroidCentre).magnitude >= MiningSite.FieldClearUnits;
                bool gateClear = (s.plant - layout.hiddenJumpgate).magnitude >= MiningSite.GateClearUnits
                                 && (!layout.hasJumpgate || (s.plant - layout.jumpgate).magnitude >= MiningSite.GateClearUnits);
                if (fieldClear && gateClear) clear++;
            }
            Assert.AreEqual(orbits, clear, "every main-game orbit finds a clear direction");
        }

        [Test]
        public void TheWayInIsClear()
        {
            var s = new MiningSite(4242);
            Assert.IsTrue(s.Blocks(s.plant + new Vector3(5000, 0, 0)), "next to the plant");
            Assert.IsTrue(s.Blocks(s.waypoint), "on the waypoint");
            Assert.IsTrue(s.Blocks(s.waypoint - s.plant.normalized * 30000f), "on the way in");
            Assert.IsFalse(s.Blocks(s.fieldCentre + Vector3.Cross(s.plant.normalized, Vector3.up).normalized * 20000f), "out in the field");
        }

        [Test]
        public void TwoGamesBuildTheSameField()
        {
            var db = Database.Load();
            var layout = OrbitLayout.Build(db, 70);
            var site = MiningSite.For(Mission(55555L), layout);
            Transform Build()
            {
                var saved = Random.state;
                Random.InitState(site.seed);
                var t = OrbitBuilder.SpawnField(db, layout, site.fieldCentre, site.bigCount + site.smallCount, site.bigCount,
                                                MiningSite.BigSide, MiningSite.SmallSide, 158, null,
                                                u => site.Blocks(new Vector3(u.x, u.y, -u.z) / OrbitLayout.MetersPerUnit), "Mining site asteroids");
                Random.state = saved;
                return t;
            }
            var a = Build();
            var b = Build();
            try
            {
                Assert.Greater(a.childCount, site.bigCount, "the field has its rocks");
                Assert.AreEqual(a.childCount, b.childCount);
                for (int i = 0; i < a.childCount; i++)
                {
                    var ta = a.GetChild(i).GetComponent<Target>();
                    var tb = b.GetChild(i).GetComponent<Target>();
                    Assert.AreEqual(ta.transform.position, tb.transform.position, $"asteroid {i}");
                    Assert.AreEqual(ta.quality, tb.quality);
                    Assert.AreEqual(158, ta.oreItem, "all of the mission's ore");
                    var game = new Vector3(ta.transform.position.x, ta.transform.position.y, -ta.transform.position.z) / OrbitLayout.MetersPerUnit;
                    Assert.IsFalse(site.Blocks(game), $"asteroid {i} clear of the plant and the way in");
                }
            }
            finally
            {
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }
    }
}
