// InputFeelTests.cs
// AutofireLatch (CLAUDE.md "UI and platforms", Touch controls / Options "double-press fire for auto-fire";
// Reference/research/touch_hud.md 6.2: a second press within 249 ms latches autofire, the next single press releases it
// and doesn't fire) and HapticMixer (CLAUDE.md "UI and platforms", Haptics / Haptics fix #24; remake-only, so tested
// from its documented rules: a pulse at full strength for the first quarter, then a linear fade to 0; the strongest
// source per motor wins, not a sum; the frame's rumble is used once, the light motor at 0.6 of it; a stop is always
// sent, other changes from 0.01).

using GoF2Remake.Flight;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class AutofireLatchTests
    {
        [Test]
        public void WindowIs249Ms()
        {
            Assert.AreEqual(0.249f, AutofireLatch.WindowSeconds, 1e-6f);
        }

        [Test]
        public void SinglePressFiresWithoutLatching()
        {
            bool latched = false; float last = -10f;
            Assert.IsTrue(AutofireLatch.Press(ref latched, ref last, 5f));
            Assert.IsFalse(latched);
            Assert.AreEqual(5f, last);
        }

        [Test]
        public void DoublePressLatches()
        {
            bool latched = false; float last = -10f;
            AutofireLatch.Press(ref latched, ref last, 5f);
            Assert.IsTrue(AutofireLatch.Press(ref latched, ref last, 5.2f));
            Assert.IsTrue(latched);
        }

        [Test]
        public void SlowSecondPressDoesNotLatch()
        {
            bool latched = false; float last = -10f;
            AutofireLatch.Press(ref latched, ref last, 5f);
            Assert.IsTrue(AutofireLatch.Press(ref latched, ref last, 5.3f));
            Assert.IsFalse(latched);
        }

        [Test]
        public void NextSinglePressReleasesWithoutFiring()
        {
            bool latched = false; float last = -10f;
            AutofireLatch.Press(ref latched, ref last, 5f);
            AutofireLatch.Press(ref latched, ref last, 5.1f);
            Assert.IsFalse(AutofireLatch.Press(ref latched, ref last, 7f), "the releasing press doesn't fire");
            Assert.IsFalse(latched);
            Assert.IsTrue(AutofireLatch.Press(ref latched, ref last, 9f));
        }

        [Test]
        public void FastPressWhileLatchedStaysLatched()
        {
            bool latched = false; float last = -10f;
            AutofireLatch.Press(ref latched, ref last, 5f);
            AutofireLatch.Press(ref latched, ref last, 5.1f);
            Assert.IsTrue(AutofireLatch.Press(ref latched, ref last, 5.2f));
            Assert.IsTrue(latched);
        }
    }

    public class HapticMixerTests
    {
        [Test]
        public void EnvelopeHoldsAQuarterThenFades()
        {
            Assert.AreEqual(1f, HapticMixer.Envelope(0f, 100f));
            Assert.AreEqual(1f, HapticMixer.Envelope(25f, 100f));
            Assert.AreEqual(0.5f, HapticMixer.Envelope(62.5f, 100f), 1e-5f);
            Assert.AreEqual(0.2f, HapticMixer.Envelope(85f, 100f), 1e-5f);
            Assert.AreEqual(0f, HapticMixer.Envelope(100f, 100f));
            Assert.AreEqual(0f, HapticMixer.Envelope(500f, 100f));
        }

        [Test]
        public void EnvelopeIsMonotonic()
        {
            float last = 2f;
            for (float t = 0f; t <= 400f; t += 5f)
            {
                float e = HapticMixer.Envelope(t, 400f);
                Assert.LessOrEqual(e, last);
                Assert.That(e, Is.InRange(0f, 1f));
                last = e;
            }
        }

        [Test]
        public void PulseClampsItsValues()
        {
            var p = new HapticPulse(2f, -1f, 0f, -5f, 3f);
            Assert.AreEqual(1f, p.low);
            Assert.AreEqual(0f, p.high);
            Assert.AreEqual(1f, p.ms);
            Assert.AreEqual(0f, p.phoneMs);
            Assert.AreEqual(1f, p.phoneAmplitude);
        }

        [Test]
        public void ScaledKeepsTheLength()
        {
            var p = new HapticPulse(0.8f, 0.4f, 200f, 100f, 0.6f).Scaled(0.5f);
            Assert.AreEqual(0.4f, p.low, 1e-6f);
            Assert.AreEqual(0.2f, p.high, 1e-6f);
            Assert.AreEqual(200f, p.ms);
            Assert.AreEqual(100f, p.phoneMs);
            Assert.AreEqual(0.3f, p.phoneAmplitude, 1e-6f);
            Assert.AreEqual(0.8f, new HapticPulse(0.8f, 0f, 10f).Scaled(5f).low, 1e-6f, "k clamped to 1");
        }

        [Test]
        public void PulseFadesAndEnds()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(0.8f, 0.4f, 100f));
            var v = m.Evaluate(62.5f);
            Assert.AreEqual(0.8f, v.x, 1e-6f);
            Assert.AreEqual(0.4f, v.y, 1e-6f);
            v = m.Evaluate(37.5f);   // at 62.5 ms: half
            Assert.AreEqual(0.4f, v.x, 1e-5f);
            Assert.AreEqual(0.2f, v.y, 1e-5f);
            Assert.AreEqual(0, m.Count, "aged to its length: removed");
            Assert.AreEqual(Vector2.zero, m.Evaluate(10f));
        }

        [Test]
        public void StrongestPerMotorWins()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(0.5f, 0.1f, 100f));
            m.Add(new HapticPulse(0.3f, 0.9f, 100f));
            m.Add(new HapticPulse(0.3f, 0.9f, 100f));
            var v = m.Evaluate(10f);
            Assert.AreEqual(0.5f, v.x, 1e-6f);
            Assert.AreEqual(0.9f, v.y, 1e-6f);
        }

        [Test]
        public void RumbleIsUsedOnce()
        {
            var m = new HapticMixer();
            m.Rumble(0.5f);
            m.Rumble(0.3f);
            var v = m.Evaluate(16f);
            Assert.AreEqual(0.5f, v.x, 1e-6f);
            Assert.AreEqual(0.5f * HapticMixer.RumbleHighShare, v.y, 1e-6f);
            Assert.AreEqual(0.5f, m.LastRumble);
            Assert.AreEqual(Vector2.zero, m.Evaluate(16f));
            Assert.AreEqual(0f, m.LastRumble);
        }

        [Test]
        public void RumbleAndPulsesMixByMax()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(0.2f, 0.7f, 100f));
            m.Rumble(0.6f);
            var v = m.Evaluate(1f);
            Assert.AreEqual(0.6f, v.x, 1e-6f);
            Assert.AreEqual(0.7f, v.y, 1e-6f);
        }

        [Test]
        public void RumbleIsClamped()
        {
            var m = new HapticMixer();
            m.Rumble(5f);
            Assert.AreEqual(1f, m.Evaluate(1f).x);
        }

        [Test]
        public void PauseKeepsOnlyTheOptionsPreview()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(1f, 1f, 100f));
            m.Add(new HapticPulse(0.5f, 0.5f, 100f), ui: true);
            m.Rumble(1f);
            m.ClearGame();
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(0.5f, m.Evaluate(1f).x, 1e-6f);
            m.Clear();
            Assert.AreEqual(0, m.Count);
        }

        [Test]
        public void StopIsAlwaysSent()
        {
            Assert.IsTrue(HapticMixer.ShouldSend(0f, 0f, 0.005f, 0f), "#24: a fade's last few percent");
            Assert.IsTrue(HapticMixer.ShouldSend(0f, 0f, 0f, 0.001f));
            Assert.IsFalse(HapticMixer.ShouldSend(0f, 0f, 0f, 0f), "already stopped");
        }

        [Test]
        public void SmallChangesAreNotSent()
        {
            Assert.IsFalse(HapticMixer.ShouldSend(0.5f, 0.5f, 0.495f, 0.5f));
            Assert.IsTrue(HapticMixer.ShouldSend(0.5f, 0.5f, 0.48f, 0.5f));
            Assert.IsTrue(HapticMixer.ShouldSend(0.5f, 0.52f, 0.5f, 0.5f));
            Assert.IsTrue(HapticMixer.ShouldSend(0.3f, 0f, 0f, 0f), "a start");
        }
    }
}
