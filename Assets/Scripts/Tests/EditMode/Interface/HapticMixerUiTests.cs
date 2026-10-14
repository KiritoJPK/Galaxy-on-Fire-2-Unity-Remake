// HapticMixerTests.cs
// CLAUDE.md "UI and platforms", Haptics: HapticMixer mixes the pulses (held for the first quarter, then a linear fade;
// the strongest per motor wins, not a sum) and the frame's continuous rumble (the strongest caller of the frame, used once;
// the light motor takes 0.6 of it); the game's pulses stop when it pauses, the options' preview keeps playing; a stop is
// always sent (#24: the 0.01 step left a fade's last few percent running), other changes from 0.01 on. Expected values
// by hand from those rules.

using GoF2Remake.Flight;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class HapticMixerUiTests
    {
        [TestCase(0f, 100f, 1f)]
        [TestCase(25f, 100f, 1f)]
        [TestCase(62.5f, 100f, 0.5f)]
        [TestCase(81.25f, 100f, 0.25f)]
        [TestCase(100f, 100f, 0f)]
        [TestCase(150f, 100f, 0f)]
        [TestCase(10f, 40f, 1f)]
        [TestCase(25f, 40f, 0.5f)]
        public void PulsesHoldAQuarterThenFade(float age, float length, float strength)
        {
            Assert.AreEqual(strength, HapticMixer.Envelope(age, length), 1e-5f);
        }

        [Test]
        public void TheEnvelopeNeverRises()
        {
            float last = 1f;
            for (float t = 0f; t <= 120f; t += 1f)
            {
                float e = HapticMixer.Envelope(t, 100f);
                Assert.LessOrEqual(e, last + 1e-6f);
                Assert.That(e, Is.InRange(0f, 1f));
                last = e;
            }
        }

        [Test]
        public void PulsesAreClampedAndScaled()
        {
            var p = new HapticPulse(2f, -1f, 0f, -5f, 3f);
            Assert.AreEqual(1f, p.low);
            Assert.AreEqual(0f, p.high);
            Assert.AreEqual(1f, p.ms, "at least 1 ms");
            Assert.AreEqual(0f, p.phoneMs);
            Assert.AreEqual(1f, p.phoneAmplitude);
            var s = new HapticPulse(0.8f, 0.4f, 200f, 50f, 0.6f).Scaled(0.5f);
            Assert.AreEqual(0.4f, s.low, 1e-6f);
            Assert.AreEqual(0.2f, s.high, 1e-6f);
            Assert.AreEqual(200f, s.ms, "the length stays");
            Assert.AreEqual(50f, s.phoneMs);
            Assert.AreEqual(0.3f, s.phoneAmplitude, 1e-6f);
            Assert.AreEqual(0.8f, new HapticPulse(0.8f, 0f, 10f).Scaled(5f).low, 1e-6f, "never stronger");
        }

        [Test]
        public void TheStrongestPulsePerMotorWins()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(0.3f, 0.9f, 100f));
            m.Add(new HapticPulse(0.7f, 0.2f, 100f));
            m.Add(new HapticPulse(0.5f, 0.5f, 100f));
            var v = m.Evaluate(10f);
            Assert.AreEqual(0.7f, v.x, 1e-6f, "low: the max, not the sum");
            Assert.AreEqual(0.9f, v.y, 1e-6f);
            Assert.AreEqual(3, m.Count);
        }

        [Test]
        public void PulsesFadeAndEnd()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(1f, 1f, 100f));
            Assert.AreEqual(1f, m.Evaluate(62.5f).x, 1e-6f, "at 0 ms");
            Assert.AreEqual(0.5f, m.Evaluate(10f).x, 1e-5f, "at 62.5 ms: half way down the fade");
            m.Evaluate(30f);   // 72.5 -> 102.5 ms: over
            Assert.AreEqual(0, m.Count, "gone once its length is over");
            Assert.AreEqual(Vector2.zero, m.Evaluate(10f));
        }

        [Test]
        public void RumbleIsTheFramesStrongestUsedOnce()
        {
            var m = new HapticMixer();
            m.Rumble(0.3f);
            m.Rumble(0.8f);
            m.Rumble(0.5f);
            var v = m.Evaluate(16f);
            Assert.AreEqual(0.8f, v.x, 1e-6f);
            Assert.AreEqual(0.8f * HapticMixer.RumbleHighShare, v.y, 1e-6f);
            Assert.AreEqual(0.6f, HapticMixer.RumbleHighShare);
            Assert.AreEqual(0.8f, m.LastRumble, 1e-6f);
            Assert.AreEqual(Vector2.zero, m.Evaluate(16f), "used up");
            Assert.AreEqual(0f, m.LastRumble);
            m.Rumble(5f);
            Assert.AreEqual(1f, m.Evaluate(16f).x, "clamped to 1");
        }

        [Test]
        public void PulsesAndRumbleMixByMax()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(0.2f, 0.9f, 100f));
            m.Rumble(0.5f);
            var v = m.Evaluate(1f);
            Assert.AreEqual(0.5f, v.x, 1e-6f);
            Assert.AreEqual(0.9f, v.y, 1e-6f);
        }

        [Test]
        public void PausingStopsTheGamesPulsesButNotThePreview()
        {
            var m = new HapticMixer();
            m.Add(new HapticPulse(1f, 1f, 500f));
            m.Add(new HapticPulse(0.4f, 0.4f, 500f), ui: true);
            m.Rumble(1f);
            m.ClearGame();
            Assert.AreEqual(1, m.Count);
            var v = m.Evaluate(1f);
            Assert.AreEqual(0.4f, v.x, 1e-6f, "the preview plays on, no rumble");
            m.Clear();
            Assert.AreEqual(0, m.Count);
            Assert.AreEqual(Vector2.zero, m.Evaluate(1f));
        }

        [TestCase(0f, 0f, 0.003f, 0f, true)]    // a stop after a faint fade is still sent
        [TestCase(0f, 0f, 0f, 0f, false)]       // nothing to stop
        [TestCase(0.5f, 0.5f, 0.5f, 0.5f, false)]
        [TestCase(0.505f, 0.5f, 0.5f, 0.5f, false)]
        [TestCase(0.52f, 0.5f, 0.5f, 0.5f, true)]
        [TestCase(0.5f, 0.48f, 0.5f, 0.5f, true)]
        [TestCase(0.004f, 0f, 0f, 0f, false)]
        public void WhenToSendTheMotors(float low, float high, float sentLow, float sentHigh, bool send)
        {
            Assert.AreEqual(send, HapticMixer.ShouldSend(low, high, sentLow, sentHigh));
        }
    }
}
