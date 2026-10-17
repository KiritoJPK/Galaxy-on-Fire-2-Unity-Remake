// HudMessageQueueTests.cs
// The flight HUD's message queue (CLAUDE.md "NPCs and combat", HUD; HudMessageQueue): the original's one message at a
// time for 4 s in order (Hud::drawEventQueue / updateQueue), and the remake's transient kinds (cargo scans, target
// notices, cargo gains) that replace each other at once without cutting a normal message short.

using GoF2Remake.Flight;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class HudMessageQueueTests
    {
        const HudMessageKind N = HudMessageKind.Normal, Scan = HudMessageKind.Scan, Tgt = HudMessageKind.Target, Gain = HudMessageKind.Gain;

        [Test]
        public void NormalMessagesQueueForFourSecondsEach()
        {
            var q = new HudMessageQueue();
            q.Post("A", 0, N, 0f);
            q.Post("B", 1, N, 0.5f);
            Assert.AreEqual("A", q.Current.text);
            Assert.AreEqual(1, q.WaitingCount);
            q.Update(3.9f);
            Assert.AreEqual("A", q.Current.text);
            q.Update(4f);
            Assert.AreEqual("B", q.Current.text);
            Assert.AreEqual(1, q.Current.colour);
            q.Update(8f);
            Assert.IsFalse(q.Showing);
        }

        [Test]
        public void ANormalTextShowingOrWaitingIsntQueuedAgain()
        {
            var q = new HudMessageQueue();
            q.Post("A", 0, N, 0f);
            q.Post("A", 0, N, 1f);
            q.Post("B", 0, N, 1f);
            q.Post("B", 0, N, 1f);
            Assert.AreEqual(1, q.WaitingCount);
        }

        [Test]
        public void TheNormalQueueHoldsTwenty()
        {
            var q = new HudMessageQueue();
            for (int i = 0; i < 30; i++) q.Post("m" + i, 0, N, 0f);
            Assert.AreEqual(HudMessageQueue.Capacity, q.WaitingCount);
        }

        [Test]
        public void ANewScanReplacesTheShowingScanAtOnce()
        {
            var q = new HudMessageQueue();
            q.Post("5t Gold", 0, Scan, 0f);
            q.Post("Nothing to salvage.", 0, Scan, 0.5f);
            Assert.AreEqual("Nothing to salvage.", q.Current.text);
            Assert.AreEqual(0, q.WaitingCount);
            q.Update(4.2f);   // a full 4 s from the new scan
            Assert.IsTrue(q.Showing);
            q.Update(4.5f);
            Assert.IsFalse(q.Showing);
        }

        [Test]
        public void TheSameScanAgainRestartsItsTime()
        {
            var q = new HudMessageQueue();
            q.Post("Nothing to salvage.", 0, Scan, 0f);
            q.Post("Nothing to salvage.", 0, Scan, 3f);
            q.Update(5f);
            Assert.IsTrue(q.Showing);
        }

        [Test]
        public void TransientKindsReplaceEachOther()
        {
            var q = new HudMessageQueue();
            q.Post("Target: Asteroid", 0, Tgt, 0f);
            q.Post("5t Gold", 0, Scan, 0.2f);
            Assert.AreEqual("5t Gold", q.Current.text);
            q.Post("3t Ore", 2, Gain, 0.4f);
            Assert.AreEqual("3t Ore", q.Current.text);
            Assert.AreEqual(2, q.Current.colour);
            Assert.AreEqual(0, q.WaitingCount);
        }

        [Test]
        public void AScanNeverCutsANormalMessageShort()
        {
            var q = new HudMessageQueue();
            q.Post("Hold your fire!", 1, N, 0f);
            q.Post("5t Gold", 0, Scan, 0.5f);
            q.Post("2t Ore", 0, Scan, 1f);   // drops the waiting scan
            Assert.AreEqual("Hold your fire!", q.Current.text);
            Assert.AreEqual(1, q.WaitingCount);
            q.Update(4f);
            Assert.AreEqual("2t Ore", q.Current.text);
        }

        [Test]
        public void AScanDoesntJumpAheadOfAWaitingNormalMessage()
        {
            var q = new HudMessageQueue();
            q.Post("5t Gold", 0, Scan, 0f);
            q.Post("Mission failed!", 1, N, 0.5f);   // waits behind the scan, as in the original
            q.Post("2t Ore", 0, Scan, 1f);           // can't push it back: queued after it
            Assert.AreEqual("5t Gold", q.Current.text);
            q.Update(4f);
            Assert.AreEqual("Mission failed!", q.Current.text);
            q.Update(8f);
            Assert.AreEqual("2t Ore", q.Current.text);
        }

        [Test]
        public void AWaitingTransientOfAnotherKindStays()
        {
            var q = new HudMessageQueue();
            q.Post("Hold your fire!", 1, N, 0f);
            q.Post("5t Gold", 0, Scan, 0.5f);
            q.Post("Target: Station", 0, Tgt, 0.6f);
            Assert.AreEqual(2, q.WaitingCount);
        }

        [Test]
        public void GainsComingInTogetherMerge()
        {
            var q = new HudMessageQueue();
            q.Post("5t Gold", 2, Gain, 0f);
            q.Post("3t Gold", 2, Gain, 0.1f);
            Assert.AreEqual("8t Gold", q.Current.text);
            q.Post("1t Gold Core", 2, Gain, 0.2f);
            Assert.AreEqual("8t Gold, 1t Gold Core", q.Current.text);
            q.Post("2t Ore", 2, Gain, 0.3f);
            Assert.AreEqual("8t Gold, 1t Gold Core, 2t Ore", q.Current.text);
            q.Post("4t Water", 2, Gain, 0.4f);   // a fourth part: the latest alone
            Assert.AreEqual("4t Water", q.Current.text);
        }

        [Test]
        public void GainsWaitingBehindANormalMessageMerge()
        {
            var q = new HudMessageQueue();
            q.Post("Hold your fire!", 1, N, 0f);
            q.Post("5t Gold", 2, Gain, 1f);
            q.Post("1t Gold Core", 2, Gain, 3f);
            Assert.AreEqual(1, q.WaitingCount);
            q.Update(4f);
            Assert.AreEqual("5t Gold, 1t Gold Core", q.Current.text);
        }

        [Test]
        public void AGainAfterTheMergeWindowReplaces()
        {
            var q = new HudMessageQueue();
            q.Post("5t Gold", 2, Gain, 0f);
            q.Post("3t Gold", 2, Gain, 1.5f);
            Assert.AreEqual("3t Gold", q.Current.text);
        }

        [Test]
        public void GasCloudGainsKeepTheirPlus()
        {
            Assert.AreEqual("+5t Green Plasma", HudMessageQueue.MergeGains("+2t Green Plasma", "+3t Green Plasma"));
            Assert.IsNull(HudMessageQueue.MergeGains("Blueprint found: X", "3t Gold"));
            Assert.IsNull(HudMessageQueue.MergeGains("5t Gold", "Cargo hold is full."));
        }

        [Test]
        public void AGainDoesntMergeIntoAScan()
        {
            var q = new HudMessageQueue();
            q.Post("5t Gold", 0, Scan, 0f);
            q.Post("5t Gold", 2, Gain, 0.2f);
            Assert.AreEqual("5t Gold", q.Current.text);
            Assert.AreEqual(Gain, q.Current.kind);
        }

        [Test]
        public void TheVersionChangesWithThePlate()
        {
            var q = new HudMessageQueue();
            int v = q.Version;
            q.Post("5t Gold", 0, Scan, 0f);
            Assert.AreNotEqual(v, q.Version);
            v = q.Version;
            q.Update(1f);
            Assert.AreEqual(v, q.Version);
            q.Post("3t Gold", 0, Scan, 1f);
            Assert.AreNotEqual(v, q.Version);
        }
    }
}
