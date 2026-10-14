// DrillSound.cs
// FMOD event 1 Mining_Drill (the FEV's LGCY data, Reference/tools/audio/fev_lgcy.py --event 1): a complex event on the
// parameter drill_speed (0..3; MiningGame::update sets (LAYER_SPEEDS[layer] - 5) / 33 * 3), event volume 0.244.
//   layer 0  Mining_Drill_Slow_1 loop over the whole range, no pitch change
//   layer 1  Mining_Drill_Add_1 loop over 1..3.083, pitch envelope x0.896 at 0 -> x1.0 at 2, then a step to x1.196
//   layer 2  Mining_Drill_Add_2 loop over 2..5 (i.e. from 2)
//   layer 3  Mining_Drill_Switch oneshots on entering 0..0.158 / 1.001..1.115 / 2..2.131, volume envelope 0.52 (0..0.427)
//            -> 0.6 (0.651) -> 0.61 (1.826) -> 0.74 (1.906)
// The loops are "loop and cutoff": they start on entering their region and stop at once on leaving it. Pitch envelope
// values v map to x2^(8v - 4) (FUN_00042cec), volume envelopes are linear gain. Plain C#: the owner hands in four
// AudioSources and calls Start / Set / Stop.

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class DrillSound
    {
        const float EventVolume = 0.244f;
        static readonly Vector2[] SwitchRegions = { new Vector2(0f, 0.1584f), new Vector2(1.001f, 1.115f), new Vector2(2f, 2.131f) };
        static readonly Vector2[] SwitchVolume = { new Vector2(0f, 0.52f), new Vector2(0.4273f, 0.52f), new Vector2(0.6514f, 0.6f),
                                                   new Vector2(1.826f, 0.61f), new Vector2(1.906f, 0.74f) };

        readonly AudioSource slow, add1, add2, switchSource;
        readonly AudioClip switchClip;
        float value = -1f;
        bool playing;

        public DrillSound(AudioSource slow, AudioSource add1, AudioSource add2, AudioSource switchSource,
                          AudioClip slowClip, AudioClip add1Clip, AudioClip add2Clip, AudioClip switchClip)
        {
            this.slow = Setup(slow, slowClip);
            this.add1 = Setup(add1, add1Clip);
            this.add2 = Setup(add2, add2Clip);
            this.switchSource = switchSource;
            this.switchClip = switchClip;
            if (switchSource != null) { switchSource.playOnAwake = false; switchSource.loop = false; }
        }

        static AudioSource Setup(AudioSource s, AudioClip clip)
        {
            if (s == null) return null;
            s.playOnAwake = false;
            s.loop = true;
            s.clip = GoF2Remake.Modding.ModSounds.Get(clip);
            return s;
        }

        public bool IsPlaying => playing;

        /// <summary>FModSound::play(1): the event starts at the current parameter (the regions it lies in play).</summary>
        public void Start(float drillSpeed)
        {
            playing = true;
            value = -1f;
            Set(drillSpeed);
        }

        public void Stop()
        {
            playing = false;
            foreach (var s in new[] { slow, add1, add2, switchSource }) if (s != null) s.Stop();
        }

        /// <summary>FModSound::setParamValue(1, 0, v), every frame of the minigame.</summary>
        public void Set(float drillSpeed)
        {
            if (!playing) return;
            float v = Mathf.Clamp(drillSpeed, 0f, 3f), before = value;
            value = v;
            float vol = EventVolume * Settings.SfxVolume;
            Loop(slow, true, vol, 1f);
            Loop(add1, v >= 1f && v <= 3.083f, vol, v < 2f ? Pitch(Mathf.Lerp(0.48012f, 0.5f, v / 2f)) : Pitch(0.532203f));
            Loop(add2, v >= 2f, vol, 1f);
            int now = Region(v), was = before < 0f ? -1 : Region(before);
            if (now >= 0 && now != was && switchSource != null && switchClip != null)
                switchSource.PlayOneShot(GoF2Remake.Modding.ModSounds.Get(switchClip), vol * Envelope(SwitchVolume, v));
        }

        static void Loop(AudioSource s, bool on, float volume, float pitch)
        {
            if (s == null || s.clip == null) return;
            s.volume = volume;
            if (!Mathf.Approximately(s.pitch, pitch)) s.pitch = pitch;
            if (on && !s.isPlaying) s.Play();
            else if (!on && s.isPlaying) s.Stop();
        }

        static int Region(float v)
        {
            for (int i = 0; i < SwitchRegions.Length; i++) if (v >= SwitchRegions[i].x && v <= SwitchRegions[i].y) return i;
            return -1;
        }

        /// <summary>FUN_00042cec: a pitch envelope value v scales the frequency by 2^(8v - 4).</summary>
        static float Pitch(float v) => Mathf.Pow(2f, 8f * v - 4f);

        static float Envelope(Vector2[] points, float x)
        {
            if (x <= points[0].x) return points[0].y;
            for (int i = 1; i < points.Length; i++)
                if (x <= points[i].x) return Mathf.Lerp(points[i - 1].y, points[i].y, (x - points[i - 1].x) / Mathf.Max(1e-6f, points[i].x - points[i - 1].x));
            return points[points.Length - 1].y;
        }
    }
}
