// HeldRepeatTests.cs
// HeldRepeat: the pause menu's held up / down / left / right (a player's report: holding a direction moved one row only).
// One step on the press, nothing during the Delay, then a step every Interval while held; a release ends it; a tap
// released within its frame still steps once; only the press's step is Fresh (the one that may wrap round).

using GoF2Remake.UI;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class HeldRepeatTests
    {
        [Test]
        public void APressStepsOnceThenRepeatsWhileHeld()
        {
            var r = new HeldRepeat();
            Assert.AreEqual(1, r.Tick(1, 1, 0f));
            Assert.IsTrue(r.Fresh);
            Assert.AreEqual(0, r.Tick(0, 1, HeldRepeat.Delay - 0.01f), "nothing before the delay");
            Assert.AreEqual(1, r.Tick(0, 1, HeldRepeat.Delay + 0.001f), "then a step");
            Assert.IsFalse(r.Fresh, "a held step isn't a press");
            Assert.AreEqual(0, r.Tick(0, 1, HeldRepeat.Delay + HeldRepeat.Interval * 0.5f));
            Assert.AreEqual(1, r.Tick(0, 1, HeldRepeat.Delay + HeldRepeat.Interval + 0.002f), "and on every interval");
            Assert.AreEqual(0, r.Tick(0, 0, 5f), "released");
            Assert.AreEqual(0, r.Tick(0, 1, 6f), "held again without a press: no step");
        }

        [Test]
        public void ATapReleasedInItsFrameStepsOnceAndTheOtherWayStartsAfresh()
        {
            var r = new HeldRepeat();
            Assert.AreEqual(-1, r.Tick(-1, 0, 0f), "a tap");
            Assert.AreEqual(0, r.Tick(0, 0, 1f));
            Assert.AreEqual(1, r.Tick(1, 1, 1.1f));
            Assert.AreEqual(-1, r.Tick(-1, -1, 1.2f), "the other way");
            Assert.AreEqual(0, r.Tick(0, -1, 1.2f + HeldRepeat.Delay - 0.01f), "with its own delay");
            Assert.AreEqual(-1, r.Tick(0, -1, 1.2f + HeldRepeat.Delay + 0.01f));
        }
    }
}
