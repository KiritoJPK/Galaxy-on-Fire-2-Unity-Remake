// CoreRulesTests.cs
// Edit-mode tests of the plain rules in GoF2.Core / GoF2.Game (Window > General > Test Runner, or the CLI's run_tests).
// The java.util.Random values come from an independent implementation of Java's generator (checked against Java's own
// new Random(0).nextInt() = -1155484576), so they test the port, not themselves.

using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class CoreRulesTests
    {
        [Test]
        public void JavaRandomMatchesJava()
        {
            var r = new GoF2Remake.Data.JavaRandom(42);
            Assert.AreEqual(new[] { 30, 63, 48, 84, 70, 25 }, Draw(r, 100, 6));
            // a power-of-two bound takes Java's other branch
            r = new GoF2Remake.Data.JavaRandom(300 * 78);
            Assert.AreEqual(new[] { 800, 586, 421, 213 }, Draw(r, 1024, 4));
            r = new GoF2Remake.Data.JavaRandom(0);
            Assert.AreEqual(new[] { 5, 2, 4, 2, 4 }, Draw(r, 7, 5));
        }

        static int[] Draw(GoF2Remake.Data.JavaRandom r, int bound, int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = r.NextInt(bound);
            return a;
        }

        [Test]
        public void MiningOreRateByLayer()
        {
            // MiningGame::update: yield x ((layer + 1) / 7 x 2.35 + 0.15) t/s
            Assert.AreEqual(1f / 7f * 2.35f + 0.15f, GoF2Remake.Flight.MiningGame.OreRate(0, 1f), 1e-5f);
            Assert.AreEqual(2.5f, GoF2Remake.Flight.MiningGame.OreRate(6, 1f), 1e-5f);
            Assert.AreEqual(5f, GoF2Remake.Flight.MiningGame.OreRate(6, 2f), 1e-5f);
        }
    }
}
