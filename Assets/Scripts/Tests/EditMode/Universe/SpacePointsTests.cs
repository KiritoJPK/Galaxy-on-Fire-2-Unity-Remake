// SpacePointsTests.cs
// Feature: CLAUDE.md "Supernova add-on", Object docking (SpacePoints = docks_hd.json sets, type 1 approach, 2 docking
// point; one ship per port: SpacePoint::take / giveFree shared by the player and the story's shuttles) and "Coordinates
// and scale" (ship-relative offsets map to (-x, y, z) * 0.05).
// Expected numbers: Resources/GoF2Data/docks_hd.json read with python -I (each set's point types and position_engine),
// directions from FileRead::loadSpacePoints as quoted in SpacePoints.cs' header: MatrixSetRotation(-r0, -r2, r1),
// column 2 = (-sin r2, sin r0 cos r2, cos r0 cos r2), evaluated by hand for the file's angles.

using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class SpacePointsTests
    {
        [TestCase(1, new[] { 1, 2, 1, 2 })]                        // mining plant
        [TestCase(2, new[] { 2, 1, 1, 2 })]                        // secure containers
        [TestCase(3, new[] { 2, 1 })]                              // damaged Midorian freighter
        [TestCase(4, new[] { 2, 1, 1, 1, 2, 2 })]                  // cargo_001_midorian
        [TestCase(6, new[] { 1, 1, 2, 2, 1, 2, 1, 2 })]            // the burning station (listed twice: the first wins)
        [TestCase(7, new[] { 2, 1 })]                              // battlestation
        [TestCase(8, new[] { 2, 1, 2, 1, 2, 1, 1, 2, 3, 3, 3, 3, 3 })] // Vossk battleship
        [TestCase(10, new[] { 1, 2, 1, 2, 1, 2, 1, 2 })]           // 102's damaged Tadram station
        [TestCase(11, new[] { 2, 1 })]
        [TestCase(12, new[] { 1, 2 })]
        [TestCase(13, new[] { 1, 2 })]
        [TestCase(14, new[] { 1, 2 })]
        public void PointTypesPerSet(int set, int[] types)
        {
            Assert.AreEqual(types, SpacePoints.Set(set).Select(p => p.type).ToArray());
        }

        [Test]
        public void TheCarrierHas34Points()
        {
            var set = SpacePoints.Set(5);
            Assert.AreEqual(34, set.Count);
            Assert.AreEqual(8, set.Count(p => p.type == 4), "type 4: unused by the player's docking");
            Assert.AreEqual(13, set.Count(p => p.type == SpacePoints.Approach));
            Assert.AreEqual(13, set.Count(p => p.type == SpacePoints.Dock));
        }

        [TestCase(0), TestCase(9), TestCase(15), TestCase(-1)]
        public void UnknownSetsAreEmpty(int set)
        {
            Assert.IsNotNull(SpacePoints.Set(set));
            Assert.AreEqual(0, SpacePoints.Set(set).Count);
        }

        [Test]
        public void PositionsAndDirections()
        {
            var p = SpacePoints.Set(3)[0];      // rotation (0, 0, 90): nose along engine -x
            Assert.AreEqual(2, p.type);
            Assert.That(Vector3.Distance(new Vector3(-2.9846f, 585.3182f, 2126.2983f), p.engine), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(new Vector3(-1f, 0f, 0f), p.dir), Is.LessThan(1e-5f));

            p = SpacePoints.Set(1)[1];          // rotation (0, 0, -90): along +x
            Assert.That(Vector3.Distance(new Vector3(12543.7012f, -5984.834f, 898.0588f), p.engine), Is.LessThan(1e-2f));
            Assert.That(Vector3.Distance(new Vector3(1f, 0f, 0f), p.dir), Is.LessThan(1e-5f));

            p = SpacePoints.Set(10)[0];         // rotation (0, 0, -135)
            Assert.AreEqual(SpacePoints.Approach, p.type);
            Assert.That(Vector3.Distance(new Vector3(17914.8555f, 3298.0071f, 24040.2988f), p.engine), Is.LessThan(1e-2f));
            float h = Mathf.Sqrt(0.5f);
            Assert.That(Vector3.Distance(new Vector3(h, 0f, -h), p.dir), Is.LessThan(1e-5f));

            p = SpacePoints.Set(10)[2];         // rotation (0, 0, 45)
            Assert.That(Vector3.Distance(new Vector3(-h, 0f, h), p.dir), Is.LessThan(1e-5f));
        }

        [Test]
        public void EveryDirectionIsAUnitVector()
        {
            for (int set = 1; set <= 14; set++)
                foreach (var p in SpacePoints.Set(set))
                    Assert.AreEqual(1f, p.dir.magnitude, 1e-4f, $"set {set}");
        }

        [Test]
        public void EngineOffsetsToObjectSpace()
        {
            Assert.AreEqual(new Vector3(-0.5f, 1f, 2f), SpacePoints.ToLocal(new Vector3(10f, 20f, 40f)));
            Assert.AreEqual(new Vector3(-3f, 4f, 5f), SpacePoints.DirToLocal(new Vector3(3f, 4f, 5f)));
        }

        [Test]
        public void PortsAreTakenAndFreed()
        {
            var a = new GameObject("SpacePointsTest A") { hideFlags = HideFlags.HideAndDontSave };
            var b = new GameObject("SpacePointsTest B") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Assert.IsFalse(SpacePoints.IsTaken(a.transform, 1));
                SpacePoints.Take(a.transform, 1);
                Assert.IsTrue(SpacePoints.IsTaken(a.transform, 1));
                Assert.IsFalse(SpacePoints.IsTaken(a.transform, 3), "per point");
                Assert.IsFalse(SpacePoints.IsTaken(b.transform, 1), "per object");
                SpacePoints.Take(a.transform, 1);   // taking twice is harmless
                SpacePoints.Free(a.transform, 1);
                Assert.IsFalse(SpacePoints.IsTaken(a.transform, 1), "one Free undoes it");
                SpacePoints.Free(a.transform, 1);   // freeing a free port is harmless
                // null objects are ignored
                SpacePoints.Take(null, 1);
                Assert.IsFalse(SpacePoints.IsTaken(null, 1));
                SpacePoints.Free(null, 1);
            }
            finally
            {
                SpacePoints.Free(a.transform, 1);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
