// NetAggressionTests.cs
// Feature: CLAUDE.md "Multiplayer" > Players against players: another player turns hostile when one of the two attacks
// the other: 3 hits or 50 damage within 10 s (a stray shot doesn't count); it lasts 120 s from the last hit either way and
// ends when either is destroyed (Forget) or the session ends (Clear). And remote motion smoothing (NetSmoothing): a jump
// of more than 250 m (a respawn, a teleport) snaps instead of easing.
// Expected values: CLAUDE.md's numbers (3 hits, 50 damage, 120 s, 250 m). Within one edit-mode test Time.unscaledTime
// doesn't move, so every hit falls in the same 10 s window and nothing runs out.

using GoF2Remake.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class NetAggressionTests
    {
        [SetUp]
        public void Start() => NetAggression.Clear();

        [TearDown]
        public void End() => NetAggression.Clear();

        [Test]
        public void HostileFor120Seconds()
        {
            Assert.AreEqual(120f, NetAggression.HostileSeconds);
        }

        [Test]
        public void NobodyIsHostileAtFirst()
        {
            Assert.IsFalse(NetAggression.IsHostile(7));
        }

        [Test]
        public void TwoSmallHitsArentAnAttack()
        {
            NetAggression.Hit(7, 10f);
            NetAggression.Hit(7, 10f);
            Assert.IsFalse(NetAggression.IsHostile(7));
        }

        [Test]
        public void TheThirdHitIsAnAttack()
        {
            NetAggression.Hit(7, 1f);
            NetAggression.Hit(7, 1f);
            NetAggression.Hit(7, 1f);
            Assert.IsTrue(NetAggression.IsHostile(7));
        }

        [Test]
        public void FiftyDamageIsAnAttack()
        {
            NetAggression.Hit(8, 50f);
            Assert.IsTrue(NetAggression.IsHostile(8), "a missile is enough");
            NetAggression.Hit(9, 49.9f);
            Assert.IsFalse(NetAggression.IsHostile(9));
            NetAggression.Hit(10, 25f);
            NetAggression.Hit(10, 25f);
            Assert.IsTrue(NetAggression.IsHostile(10), "damage adds up");
        }

        [Test]
        public void NegativeDamageCountsAsAHitOnly()
        {
            NetAggression.Hit(11, -100f);
            NetAggression.Hit(11, 49f);
            Assert.IsFalse(NetAggression.IsHostile(11), "no damage from a negative hit, 2 hits");
            NetAggression.Hit(11, 0f);
            Assert.IsTrue(NetAggression.IsHostile(11), "the third hit");
        }

        [Test]
        public void EachPlayerIsCountedApart()
        {
            NetAggression.Hit(12, 1f);
            NetAggression.Hit(13, 1f);
            NetAggression.Hit(14, 1f);
            Assert.IsFalse(NetAggression.IsHostile(12));
            Assert.IsFalse(NetAggression.IsHostile(13));
            Assert.IsFalse(NetAggression.IsHostile(14));
        }

        [Test]
        public void DestroyedEndsIt()
        {
            NetAggression.Hit(15, 100f);
            NetAggression.Forget(15);
            Assert.IsFalse(NetAggression.IsHostile(15));
            NetAggression.Hit(15, 1f);
            NetAggression.Hit(15, 1f);
            Assert.IsFalse(NetAggression.IsHostile(15), "the hits before were forgotten too");
        }

        [Test]
        public void ForgetOnlyThatPlayer()
        {
            NetAggression.Hit(16, 100f);
            NetAggression.Hit(17, 100f);
            NetAggression.Forget(16);
            Assert.IsTrue(NetAggression.IsHostile(17));
        }

        [Test]
        public void ClearEndsEveryone()
        {
            NetAggression.Hit(18, 100f);
            NetAggression.Hit(19, 1f);
            NetAggression.Clear();
            Assert.IsFalse(NetAggression.IsHostile(18));
            NetAggression.Hit(19, 1f);
            NetAggression.Hit(19, 1f);
            Assert.IsFalse(NetAggression.IsHostile(19), "the tallies went too");
        }

        [Test]
        public void HitsWhileHostileKeepItHostile()
        {
            NetAggression.Hit(20, 100f);
            NetAggression.Hit(20, 1f);
            Assert.IsTrue(NetAggression.IsHostile(20));
        }

        // ---- NetSmoothing ----

        static void WithTransform(System.Action<Transform> body)
        {
            var go = new GameObject("NetSmoothingTest") { hideFlags = HideFlags.HideAndDontSave };
            try { body(go.transform); }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NothingBeforeTheFirstPosition()
        {
            WithTransform(t =>
            {
                t.position = new Vector3(1, 2, 3);
                new NetSmoothing().Apply(t, Quaternion.identity);
                Assert.AreEqual(new Vector3(1, 2, 3), t.position);
            });
        }

        [Test]
        public void TheFirstPositionSnaps()
        {
            WithTransform(t =>
            {
                var s = new NetSmoothing();
                var rot = Quaternion.Euler(0, 90, 0);
                s.Push(new Vector3(100, 0, 0));
                s.Apply(t, rot);
                Assert.AreEqual(new Vector3(100, 0, 0), t.position);
                Assert.That(Quaternion.Angle(rot, t.rotation), Is.LessThan(0.01f));
            });
        }

        [Test]
        public void AJumpOfMoreThan250mSnaps()
        {
            WithTransform(t =>
            {
                var s = new NetSmoothing();
                s.Push(Vector3.zero);
                s.Apply(t, Quaternion.identity);
                s.Push(new Vector3(0, 0, 251f));
                s.Apply(t, Quaternion.identity);
                Assert.AreEqual(new Vector3(0, 0, 251f), t.position, "a jump, not motion: straight there");
            });
        }

        [Test]
        public void ASmallMoveEasesWithoutOvershooting()
        {
            WithTransform(t =>
            {
                var s = new NetSmoothing();
                s.Push(Vector3.zero);
                s.Apply(t, Quaternion.identity);
                s.Push(new Vector3(0, 0, 10f));
                s.Apply(t, Quaternion.identity);
                Assert.That(t.position.z, Is.InRange(0f, 10f + 1e-3f));
                Assert.AreEqual(0f, t.position.x, 1e-4f);
            });
        }

        [Test]
        public void SnapPutsItStraightThere()
        {
            WithTransform(t =>
            {
                var s = new NetSmoothing();
                s.Push(Vector3.zero);
                s.Apply(t, Quaternion.identity);
                s.Push(new Vector3(5, 0, 0));
                s.Snap();
                s.Apply(t, Quaternion.identity);
                Assert.AreEqual(new Vector3(5, 0, 0), t.position);
            });
        }
    }
}
