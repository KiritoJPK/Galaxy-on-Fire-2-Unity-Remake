// AspectAndRandomTests.cs
// Aspect (CLAUDE.md "UI and platforms": cameras use Aspect.VerticalFov, Hor+ on wide screens and never narrower than 16:9;
// the bars Aspect.VerticalFovKeepWidth): expected values computed by hand / Python from the definition (the 16:9
// horizontal FOV kept below 16:9: 60 deg -> 75.178 at 4:3, 65.360 at 16:10, horizontal 91.493 deg; KeepWidth at 32:9
// -> 32.204, at 21:9 -> 47.488).
// JavaRandom (CLAUDE.md "Space scene", Stable layout: the original's RNG is java.util.Random): expected sequences from an
// independent Python implementation of java.util.Random (48-bit LCG 0x5DEECE66D + 0xB, nextInt(n) with Java's
// power-of-two branch and rejection loop), anchored on Java's own new Random(0).nextInt(100) = 60.

using GoF2Remake.Data;
using GoF2Remake.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class AspectTests
    {
        static float Horizontal(float verticalDeg, float aspect) =>
            2f * Mathf.Atan(Mathf.Tan(verticalDeg * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;

        [Test]
        public void SixteenByNineIsUnchanged()
        {
            Assert.AreEqual(60f, Aspect.VerticalFov(60f, 16f / 9f), 1e-4f);
        }

        [TestCase(21f / 9f)]
        [TestCase(32f / 9f)]
        public void WiderScreensKeepTheVerticalFov(float aspect)
        {
            Assert.AreEqual(60f, Aspect.VerticalFov(60f, aspect), 1e-4f);
        }

        [TestCase(4f / 3f, 75.17818f)]
        [TestCase(16f / 10f, 65.36037f)]
        public void NarrowerScreensWidenTheVerticalFov(float aspect, float expected)
        {
            Assert.AreEqual(expected, Aspect.VerticalFov(60f, aspect), 1e-3f);
        }

        [TestCase(4f / 3f)]
        [TestCase(16f / 10f)]
        [TestCase(1.5f)]
        public void HorizontalViewNeverNarrowerThan16By9(float aspect)
        {
            Assert.AreEqual(91.49284f, Horizontal(60f, 16f / 9f), 1e-3f);
            Assert.AreEqual(Horizontal(60f, 16f / 9f), Horizontal(Aspect.VerticalFov(60f, aspect), aspect), 1e-3f);
        }

        [Test]
        public void VerticalIsCapped()
        {
            Assert.AreEqual(110f, Aspect.VerticalFov(100f, 1f), 1e-4f);
            Assert.AreEqual(80f, Aspect.VerticalFov(100f, 1f, 80f), 1e-4f);
        }

        [Test]
        public void BadAspectReturnsTheInput()
        {
            Assert.AreEqual(60f, Aspect.VerticalFov(60f, 0f));
            Assert.AreEqual(60f, Aspect.VerticalFov(60f, -2f));
        }

        [TestCase(32f / 9f, 32.20423f)]
        [TestCase(21f / 9f, 47.48804f)]
        public void KeepWidthNarrowsOnWideScreens(float aspect, float expected)
        {
            float v = Aspect.VerticalFovKeepWidth(60f, aspect);
            Assert.AreEqual(expected, v, 1e-3f);
            Assert.AreEqual(Horizontal(60f, 16f / 9f), Horizontal(v, aspect), 1e-3f);
        }

        [Test]
        public void KeepWidthBelow16By9IsHorPlus()
        {
            Assert.AreEqual(Aspect.VerticalFov(60f, 4f / 3f), Aspect.VerticalFovKeepWidth(60f, 4f / 3f), 1e-5f);
            Assert.AreEqual(60f, Aspect.VerticalFovKeepWidth(60f, 16f / 9f), 1e-4f);
        }

        [Test]
        public void ChaseCameraFovAt4By3()
        {
            // the chase camera's 1.22 rad (69.9 deg) at 4:3 -> 85.96 deg
            Assert.AreEqual(85.96088f, Aspect.VerticalFov(1.22f * Mathf.Rad2Deg, 4f / 3f), 0.05f);
        }
    }

    public class JavaRandomTests
    {
        static int[] Draw(JavaRandom r, int bound, int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = r.NextInt(bound);
            return a;
        }

        [Test]
        public void JavasOwnFirstValue()
        {
            Assert.AreEqual(60, new JavaRandom(0).NextInt(100));
        }

        [TestCase(0L, 100, new[] { 60, 48, 29, 47, 15, 53 })]
        [TestCase(1L, 10, new[] { 5, 8, 7, 3, 4, 4, 4, 6 })]
        [TestCase(-1L, 1000, new[] { 913, 225, 579, 439, 604, 438 })]
        [TestCase(1099511627783L, 3, new[] { 1, 0, 0, 2, 0, 2, 2, 2, 2, 2 })]       // a seed past 32 bits
        [TestCase(30300L, 13, new[] { 10, 6, 11, 12, 12, 4 })]                        // 300 x station 101 (sun / planets)
        [TestCase(-987654321L, 1000000, new[] { 98362, 379534, 164416, 545630, 792710 })]
        public void SequencesMatchJava(long seed, int bound, int[] expected)
        {
            CollectionAssert.AreEqual(expected, Draw(new JavaRandom(seed), bound, expected.Length));
        }

        [TestCase(156L, 2, new[] { 1, 0, 1, 1, 0, 0, 0, 1 })]                         // 2 x station 78 (gates, sky)
        [TestCase(9999L, 65536, new[] { 57767, 4191, 1969, 25313, 50322 })]
        public void PowerOfTwoBoundsTakeTheHighBits(long seed, int bound, int[] expected)
        {
            CollectionAssert.AreEqual(expected, Draw(new JavaRandom(seed), bound, expected.Length));
        }

        [Test]
        public void LargestBoundUsesTheRejectionLoop()
        {
            CollectionAssert.AreEqual(new[] { 1426010965, 1639209600, 981296483, 547328344 },
                Draw(new JavaRandom(123456789L), int.MaxValue, 4));
        }

        [Test]
        public void BoundOneIsAlwaysZero()
        {
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, Draw(new JavaRandom(42L), 1, 4));
        }

        [Test]
        public void NonPositiveBoundGivesZero()
        {
            var r = new JavaRandom(5L);
            Assert.AreEqual(0, r.NextInt(0));
            Assert.AreEqual(0, r.NextInt(-7));
        }

        [Test]
        public void SetSeedRestartsTheSequence()
        {
            var r = new JavaRandom(77L);
            var first = Draw(r, 500, 6);
            r.SetSeed(77L);
            CollectionAssert.AreEqual(first, Draw(r, 500, 6));
            CollectionAssert.AreEqual(first, Draw(new JavaRandom(77L), 500, 6));
        }

        [Test]
        public void ValuesStayInRange()
        {
            var r = new JavaRandom(2024L);
            foreach (int bound in new[] { 2, 3, 7, 100, 1000, 25000, 1 << 20, 1000000007 })
                for (int i = 0; i < 200; i++) Assert.That(r.NextInt(bound), Is.InRange(0, bound - 1));
        }
    }
}
