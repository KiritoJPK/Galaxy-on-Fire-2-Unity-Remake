// SpaceCoordinateTests.cs
// Feature: CLAUDE.md "Coordinates and scale" and "Space scene" (Rotations: game setRotation(x, y, z) = Rx*Ry*Rz, mirrored
// and the import's 180 deg yaw undone, so game (0, pi, 0) = identity) and "Navigation" (the HUD distance, Radar::calcDistance).
// Expected values: hand derivation from the engine conventions in Reference/research/space_level_setup.md 0 and
// space_backdrop.md "Engine conventions" (local +Z of Rx(p)*Ry(y) = (sin y, -sin p cos y, cos p cos y); Unity = (x, y, -z)
// * 0.05; S R S with S = diag(1, 1, -1) is Rx(-x) Ry(-y) Rz(z)); the distance strings from
// Reference/tools/autopilot/autopilot_tables.py calc_distance (run with python -I).

using GoF2Remake.Flight;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class SpaceCoordinateTests
    {
        static Vector3 GameDir(float pitch, float yaw) =>
            new Vector3(Mathf.Sin(yaw), -Mathf.Sin(pitch) * Mathf.Cos(yaw), Mathf.Cos(pitch) * Mathf.Cos(yaw));

        // Rx(x) Ry(y) Rz(z) applied to v (column vectors, the engine's MatrixSetRotation)
        static Vector3 GameRotate(Vector3 e, Vector3 v)
        {
            v = new Vector3(v.x * Mathf.Cos(e.z) - v.y * Mathf.Sin(e.z), v.x * Mathf.Sin(e.z) + v.y * Mathf.Cos(e.z), v.z);
            v = new Vector3(v.x * Mathf.Cos(e.y) + v.z * Mathf.Sin(e.y), v.y, -v.x * Mathf.Sin(e.y) + v.z * Mathf.Cos(e.y));
            v = new Vector3(v.x, v.y * Mathf.Cos(e.x) - v.z * Mathf.Sin(e.x), v.y * Mathf.Sin(e.x) + v.z * Mathf.Cos(e.x));
            return v;
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(v.x, v.y, -v.z);

        [Test]
        public void GamePositionToUnity()
        {
            Assert.AreEqual(new Vector3(0.5f, 1f, -500f), OrbitLayout.ToUnity(new Vector3(10f, 20f, 10000f)));
            Assert.AreEqual(new Vector3(-1000f, 0f, 1000f), OrbitLayout.ToUnity(new Vector3(-20000f, 0f, -20000f)));
            Assert.AreEqual(new Vector3(3f, -4f, 5f), OrbitLayout.DirToUnity(new Vector3(3f, -4f, -5f)));
            Assert.AreEqual(0.05f, OrbitLayout.MetersPerUnit);
        }

        [Test]
        public void DirectionIsLocalZOfRxRy()
        {
            foreach (var (p, y) in new[] { (0f, 0f), (0.1f, 1.2f), (-0.19f, 4.4f), (0.05f, -2f), (0.196f, Mathf.PI / 2f) })
            {
                var expected = GameRotate(new Vector3(p, y, 0f), Vector3.forward);
                Assert.That(Vector3.Distance(expected, OrbitLayout.Direction(p, y)), Is.LessThan(1e-5f), $"({p}, {y})");
                Assert.That(Vector3.Distance(GameDir(p, y), OrbitLayout.Direction(p, y)), Is.LessThan(1e-5f));
            }
        }

        [Test]
        public void StationRotationIsIdentityInUnity()
        {
            // stations and gates are placed at game (0, pi, 0)
            var q = OrbitLayout.RotationToUnity(new Vector3(0f, Mathf.PI, 0f));
            Assert.That(Quaternion.Angle(Quaternion.identity, q), Is.LessThan(0.01f));
        }

        [Test]
        public void IdentityGameRotationFacesUnityMinusZ()
        {
            // a game object at rotation 0 faces game +Z = Unity -Z; the imported model faces Unity +Z
            var q = OrbitLayout.RotationToUnity(Vector3.zero);
            Assert.That(Vector3.Distance(new Vector3(0f, 0f, -1f), q * Vector3.forward), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(Vector3.up, q * Vector3.up), Is.LessThan(1e-5f));
        }

        [Test]
        public void RotationKeepsForwardAndUpThroughTheMirror()
        {
            // the model's nose and top must point where the game's local +Z / +Y point, mirrored into Unity
            var rnd = new System.Random(1234);
            for (int i = 0; i < 50; i++)
            {
                var e = new Vector3((float)(rnd.NextDouble() * 6.3 - 3.15), (float)(rnd.NextDouble() * 6.3 - 3.15), (float)(rnd.NextDouble() * 6.3 - 3.15));
                var q = OrbitLayout.RotationToUnity(e);
                Assert.That(Vector3.Distance(Mirror(GameRotate(e, Vector3.forward)), q * Vector3.forward), Is.LessThan(1e-4f), $"forward {e}");
                Assert.That(Vector3.Distance(Mirror(GameRotate(e, Vector3.up)), q * Vector3.up), Is.LessThan(1e-4f), $"up {e}");
            }
        }

        [Test]
        public void UndockYawTurnsTheShipAwayFromTheStation()
        {
            // the undocking player at yaw +-1600 / 65536 of a turn faces roughly game +Z (Unity -Z), away from the station
            float a = OrbitLayout.UndockYaw / 65536f * 2f * Mathf.PI;
            Assert.AreEqual(0.1534f, a, 1e-4f);
            var fwd = OrbitLayout.RotationToUnity(new Vector3(0f, a, 0f)) * Vector3.forward;
            Assert.That(Vector3.Angle(new Vector3(0f, 0f, -1f), fwd), Is.EqualTo(8.79f).Within(0.01f));
            var station = OrbitLayout.RotationToUnity(new Vector3(0f, Mathf.PI, 0f)) * Vector3.forward;
            Assert.That(Vector3.Dot(station, fwd), Is.LessThan(-0.98f), "they face opposite ways");
        }

        [TestCase(0f, "0m"), TestCase(127f, "0m"), TestCase(128f, "8m"), TestCase(500f, "24m"), TestCase(10000f, "624m"),
         TestCase(13000f, "808m"), TestCase(15999f, "992m"), TestCase(16000f, "1.0km"), TestCase(20000f, "1.2km"),
         TestCase(26000f, "1.6km"), TestCase(80000f, "5.0km"), TestCase(100000f, "6.2km"), TestCase(1000000f, "62.4km")]
        public void HudDistance(float units, string text)
        {
            // Radar::calcDistance 0x15827c
            Assert.AreEqual(text, Navigation.FormatDistance(units));
        }

        [Test]
        public void HudDistanceTenthsIgnoreSmallRemainders()
        {
            // m = 8 * floor(d / 128): 1048 m (d = 128 * 131) has a remainder under 100 -> "1.0km"
            Assert.AreEqual("1.0km", Navigation.FormatDistance(128f * 131f));
            Assert.AreEqual("1.1km", Navigation.FormatDistance(128f * 138f));   // 1104 m
        }
    }
}
