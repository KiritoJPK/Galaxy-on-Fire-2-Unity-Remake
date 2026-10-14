// MiningGameTests.cs
// The mining minigame (CLAUDE.md "Mining", Minigame; Reference/research/mining.md 1.1, 4.3-4.5). Expected numbers from the
// research: layers = class (A 7, B 6, C 5, D 4), 6 s each, ring radii A 250/210/170/140/110/80/50, B 250..80,
// C 250/200/150/100/70, D 250/170/120/80 px; ore rate yield * ((layer + 1) / 7 * 2.35 + 0.15) t/s; a 2.5 s energy budget
// outside the ring for the whole session, empty = lost with no ore; first drift after 2.5 s; movement dt * (input +
// drift) / 10 px; IMT Extract 1.3 = attr 32 30 (steadiness 0.75), attr 33 60 (yield 0.6); perfect runs (mining.md 4.5,
// mining_tables.py's perfect_run at 16 ms frames, re-run in Python without the binary: D 14.28, C 20.88, B 28.70,
// A 37.72) -> D 14, C 20, B 28, A 37 + a core. UnityEngine.Random (the drift) is seeded and put back.

using GoF2Remake.Flight;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class MiningGameTests
    {
        const int Gold = 154;

        static MiningGame ImtExtract13(int quality, bool tutorial = false) => new MiningGame(quality, Gold, 30, 60, tutorial);

        /// <summary>Plays a whole game holding the drill on the centre (a proportional controller, stronger than any drift).</summary>
        static MiningGame PerfectRun(int quality, int seed, out int tons)
        {
            var saved = Random.state;
            Random.InitState(seed);
            try
            {
                var g = ImtExtract13(quality);
                int t = 0;
                g.NewTon += () => t++;
                for (int guard = 0; guard < 100000 && g.Update(16f); guard++)
                {
                    var d = g.Drill;
                    g.SetInput(new Vector2(Mathf.Clamp(-d.x / 10f, -1f, 1f), Mathf.Clamp(-d.y / 10f, -1f, 1f)));
                }
                tons = t;
                return g;
            }
            finally { Random.state = saved; }
        }

        [TestCase(4, 14)]
        [TestCase(5, 20)]
        [TestCase(6, 28)]
        [TestCase(7, 37)]
        public void PerfectRunWithImtExtract13(int quality, int expectedTons)
        {
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var g = PerfectRun(quality, seed, out int tons);
                Assert.IsTrue(g.Won);
                Assert.IsFalse(g.Lost);
                Assert.AreEqual(0f, g.OutsideMs, "the controller kept the drill inside");
                Assert.AreEqual(expectedTons, g.OreAmount);
                Assert.AreEqual(expectedTons, tons, "one NewTon per whole ton");
                Assert.AreEqual(quality == 7, g.GotCore);
                Assert.AreEqual(quality - 1, g.Layer);
            }
        }

        [Test]
        public void QualityIsClampedToTheFourClasses()
        {
            Assert.AreEqual(4, ImtExtract13(1).Quality);
            Assert.AreEqual(7, ImtExtract13(9).Quality);
            Assert.AreEqual(5, ImtExtract13(5).LayerCount);
            Assert.IsTrue(ImtExtract13(7).HasCore);
            Assert.IsFalse(ImtExtract13(6).HasCore);
        }

        [Test]
        public void DrillFactors()
        {
            // steadiness = attr32 / 100 * 1.5 + 0.3, yield = attr33 / 100
            var g = ImtExtract13(5);
            Assert.AreEqual(0.75f, g.Steadiness, 1e-5f);
            Assert.AreEqual(0.6f, g.Yield, 1e-5f);
            var gunant = new MiningGame(5, Gold, 100, 100);
            Assert.AreEqual(1.8f, gunant.Steadiness, 1e-5f);
            Assert.AreEqual(1f, gunant.Yield, 1e-5f);
        }

        [TestCase(7, new[] { 250, 210, 170, 140, 110, 80, 50 })]
        [TestCase(6, new[] { 250, 210, 170, 140, 110, 80 })]
        [TestCase(5, new[] { 250, 200, 150, 100, 70 })]
        [TestCase(4, new[] { 250, 170, 120, 80 })]
        public void RingRadiiPerClass(int quality, int[] radii)
        {
            var g = ImtExtract13(quality);
            for (int i = 0; i < radii.Length; i++) Assert.AreEqual(radii[i], g.RadiusOf(i));
        }

        [Test]
        public void LayerSpeedsTable()
        {
            CollectionAssert.AreEqual(new float[] { 5, 8, 12, 17, 23, 30, 38 }, MiningGame.LayerSpeeds);
        }

        [Test]
        public void OreRatePerLayer()
        {
            // yield x 0.486, 0.821, 1.157, 1.493, 1.829, 2.164, 2.500 t/s
            float[] perYield = { 0.486f, 0.821f, 1.157f, 1.493f, 1.829f, 2.164f, 2.5f };
            for (int l = 0; l < 7; l++)
            {
                Assert.AreEqual(perYield[l], MiningGame.OreRate(l, 1f), 1e-3f);
                Assert.AreEqual(perYield[l] * 0.6f, MiningGame.OreRate(l, 0.6f), 1e-3f);
            }
        }

        [Test]
        public void OreAccruesInTheFirstLayerBeforeAnyDrift()
        {
            // no drift for the first 2.5 s, the drill stays at the centre: 2 s x 0.6 x 0.4857 t/s
            var g = ImtExtract13(4);
            for (int i = 0; i < 200; i++) Assert.IsTrue(g.Update(10f));
            Assert.AreEqual(Vector2.zero, g.Drill);
            Assert.AreEqual(2f * 0.6f * (1f / 7f * 2.35f + 0.15f), g.Ore, 1e-4f);
            Assert.AreEqual(0, g.Layer);
            Assert.AreEqual(2000f, g.LayerTime, 1e-3f);
        }

        [Test]
        public void LayerAdvancesPastSixSeconds()
        {
            var g = ImtExtract13(4);
            for (int i = 0; i < 240; i++) g.Update(10f);   // before 2500 ms: still no drift
            var saved = Random.state;
            Random.InitState(5);
            try
            {
                for (int i = 0; i < 400 && g.Layer == 0; i++)
                {
                    var d = g.Drill;
                    g.SetInput(new Vector2(Mathf.Clamp(-d.x / 10f, -1f, 1f), Mathf.Clamp(-d.y / 10f, -1f, 1f)));
                    g.Update(10f);
                }
            }
            finally { Random.state = saved; }
            Assert.AreEqual(1, g.Layer);
            Assert.AreEqual(0f, g.LayerTime, "6010 ms: the layer timer starts again");
        }

        [Test]
        public void InputMovesThreeTimesTheValue()
        {
            // input = 3 v; movement dt * input / 10 px: 100 ms at v 0.5 = 15 px
            var g = ImtExtract13(4);
            g.SetInput(new Vector2(0.5f, -0.25f));
            for (int i = 0; i < 10; i++) g.Update(10f);
            Assert.AreEqual(15f, g.Drill.x, 1e-3f);
            Assert.AreEqual(-7.5f, g.Drill.y, 1e-3f);
        }

        [Test]
        public void EnergyBudgetRunsOutOutside()
        {
            var saved = Random.state;
            Random.InitState(3);
            try { EnergyRun(); }
            finally { Random.state = saved; }
        }

        static void EnergyRun()
        {
            var g = ImtExtract13(4);
            int leftRing = 0;
            g.InsideChanged += inside => { if (!inside) leftRing++; };
            g.SetInput(new Vector2(1f, 0f));                 // 0.3 px / ms: out of the 250 px ring after ~834 ms
            float ms = 0f;
            bool running = true;
            while (running && ms < 10000f) { running = g.Update(10f); ms += 10f; }
            Assert.IsTrue(g.Lost);
            Assert.IsFalse(g.Won);
            Assert.AreEqual(0f, g.Ore, "a lost game gives no ore");
            Assert.AreEqual(0, g.OreAmount);
            Assert.AreEqual(0f, g.Energy01, 1e-6f);
            Assert.AreEqual(1, leftRing);
            Assert.AreEqual(840f + 2500f, ms, 20f);          // left at ~840 ms, then 2.5 s outside
            Assert.IsFalse(g.Update(10f), "over");
        }

        [Test]
        public void EnergyIsSharedForTheWholeSession()
        {
            // 1 s outside, back in, out again: the budget doesn't refill
            var saved = Random.state;
            Random.InitState(11);
            try
            {
                var g = ImtExtract13(4);
                g.SetInput(new Vector2(1f, 0f));
                for (int i = 0; i < 1000 && g.Inside; i++) g.Update(10f);
                for (int i = 0; i < 100; i++) g.Update(10f);    // ~1000 ms outside
                float used = g.OutsideMs;
                Assert.AreEqual(1f - used / 2500f, g.Energy01, 1e-5f);
                g.SetInput(new Vector2(-1f, 0f));
                for (int i = 0; i < 1000 && !g.Inside && !g.Lost; i++) g.Update(10f);
                Assert.IsTrue(g.Inside);
                float afterReturn = g.OutsideMs;
                for (int i = 0; i < 20; i++) g.Update(10f);
                Assert.AreEqual(afterReturn, g.OutsideMs, "inside: nothing drains, nothing comes back");
                Assert.Greater(afterReturn, used);
            }
            finally { Random.state = saved; }
        }

        [Test]
        public void TutorialPullsTheDrillBack()
        {
            var g = ImtExtract13(4, tutorial: true);
            g.SetInput(new Vector2(1f, 0f));
            for (int i = 0; i < 1000 && g.Inside; i++) g.Update(10f);
            Assume.That(g.Inside, Is.False);
            g.SetInput(Vector2.zero);
            for (int i = 0; i < 100 && !g.Inside; i++) g.Update(10f);
            Assert.IsTrue(g.Inside);
            Assert.IsFalse(g.Lost);
        }

        [Test]
        public void ReleasedStickStopsTheDrill()
        {
            // remake deviation: the original keeps the last input
            var g = ImtExtract13(4);
            g.SetInput(new Vector2(1f, 0f));
            g.Update(100f);
            g.SetInput(Vector2.zero);
            float x = g.Drill.x;
            g.Update(100f);
            Assert.AreEqual(x, g.Drill.x);
        }

        [Test]
        public void FinishedGameStopsUpdating()
        {
            var g = PerfectRun(4, 9, out _);
            float ore = g.Ore;
            Assert.IsFalse(g.Update(16f));
            Assert.AreEqual(ore, g.Ore);
        }

        [Test]
        public void SameSeedSameRun()
        {
            var a = PerfectRun(6, 42, out _);
            var b = PerfectRun(6, 42, out _);
            Assert.AreEqual(a.Ore, b.Ore);
            Assert.AreEqual(a.Drill, b.Drill);
        }
    }

    // MiningBeamExtraction (CLAUDE.md "Mining", the remake's mining beam for mods): the minigame's ore without the minigame.
    // Expected: a whole asteroid = the perfect run x yield (mining.md 4.5: yield x 23.74 D, 34.71 C, 47.70 B, 62.70 A; at
    // yield 1.0 Gunant's row 23 / 34 / 47 / 62), the time per layer (attr 102) scales the rate, Extreme pays half of what
    // is cut until the rock is depleted (PlayerEgo::stopMining halving an unfinished run) and the rest then.
    public class MiningBeamExtractionTests
    {
        static int CutAll(MiningBeamExtraction b, float stepMs)
        {
            int total = 0;
            for (int i = 0; i < 1000000 && !b.Depleted; i++) total += b.Update(stepMs);
            return total;
        }

        [TestCase(4, 23)]
        [TestCase(5, 34)]
        [TestCase(6, 47)]
        [TestCase(7, 62)]
        public void WholeAsteroidIsThePerfectRun(int quality, int tons)
        {
            Assert.AreEqual(tons, CutAll(new MiningBeamExtraction(quality, 100, 6000f), 16f));
        }

        [TestCase(4, 14)]
        [TestCase(7, 37)]
        public void YieldScales(int quality, int tons)
        {
            Assert.AreEqual(tons, CutAll(new MiningBeamExtraction(quality, 60, 6000f), 16f));
        }

        [Test]
        public void OneLongStepPaysLikeManyShort()
        {
            var big = new MiningBeamExtraction(7, 100, 6000f);
            int all = big.Update(1e9f);
            Assert.IsTrue(big.Depleted);
            Assert.AreEqual(CutAll(new MiningBeamExtraction(7, 100, 6000f), 7f), all);
        }

        [Test]
        public void FasterBeamSameOreSooner()
        {
            var fast = new MiningBeamExtraction(4, 100, 3000f);
            int tons = fast.Update(11999f);
            Assert.IsFalse(fast.Depleted);
            tons += fast.Update(1f);
            Assert.IsTrue(fast.Depleted, "4 layers x 3000 ms");
            Assert.AreEqual(23, tons);
        }

        [Test]
        public void ProgressByLayers()
        {
            var b = new MiningBeamExtraction(4, 100, 6000f);
            Assert.AreEqual(0f, b.Progress01);
            b.Update(6000f);
            Assert.AreEqual(0.25f, b.Progress01, 1e-5f);
            b.Update(3000f);
            Assert.AreEqual(0.375f, b.Progress01, 1e-5f);
            b.Update(1e6f);
            Assert.AreEqual(1f, b.Progress01);
        }

        [Test]
        public void CoreOnlyFromClassA()
        {
            var a = new MiningBeamExtraction(7, 100, 6000f);
            a.Update(41999f);
            Assert.IsFalse(a.GotCore);
            a.Update(1f);
            Assert.IsTrue(a.GotCore);
            var b = new MiningBeamExtraction(6, 100, 6000f);
            b.Update(1e6f);
            Assert.IsTrue(b.Depleted);
            Assert.IsFalse(b.GotCore);
        }

        [Test]
        public void ExtremePaysHalfUntilDepleted()
        {
            // three layers of D: 6 * (0.486 + 0.821 + 1.157) = 14.79 t cut -> 7 paid; the last layer brings the rest of 23
            var b = new MiningBeamExtraction(4, 100, 6000f, halveUnfinished: true);
            Assert.AreEqual(7, b.Update(18000f));
            Assert.AreEqual(16, b.Update(6000f));
            Assert.IsTrue(b.Depleted);
            var normal = new MiningBeamExtraction(4, 100, 6000f);
            Assert.AreEqual(14, normal.Update(18000f));
        }

        [Test]
        public void NothingAfterDepletedOrWithoutTime()
        {
            var b = new MiningBeamExtraction(4, 100, 6000f);
            Assert.AreEqual(0, b.Update(0f));
            Assert.AreEqual(0, b.Update(-50f));
            b.Update(1e6f);
            Assert.AreEqual(0, b.Update(1000f));
        }

        [Test]
        public void ArgumentsAreClamped()
        {
            var b = new MiningBeamExtraction(2, -50, 10f);
            Assert.AreEqual(4, b.Quality);
            Assert.AreEqual(0f, b.Yield);
            Assert.AreEqual(100f, b.LayerMs);
            Assert.AreEqual(0, b.Update(1e6f));
            Assert.AreEqual(7, new MiningBeamExtraction(12, 100, 6000f).Quality);
        }
    }
}
