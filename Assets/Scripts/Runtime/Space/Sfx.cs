// Sfx.cs
// One-shot sound effects in space. Distances are kilometres, so instead of Unity's 3D rolloff the volume falls off
// linearly with the distance to the camera (the original plays FMOD events at the ship position when 3D sound is on).
// Remake: a fixed set of voices in the active scene (gone with it, as the old one-off objects were): a free one, else the
// quietest playing one when the new sound is louder (like ShotVoices' cap). Each sound was a new GameObject with an
// AudioSource, destroyed after the clip, and a fleet battle's hits and deaths took as many voices as they liked.

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class Sfx
    {
        public const float AudibleMeters = 3000f;   // silent beyond (60000 game units)
        /// <summary>An FMOD event's own volume (the FEV's LGCY data: lasers 0.13-0.29, engines 0.06-0.1, boosters 0.2) times
        /// this is the remake's source volume, matching the levels the remake's other sounds already play at.</summary>
        public const float EventGain = 4f;

        const int Voices = 24;
        static AudioSource[] voices;
        static GameObject holder;   // null again once its scene is gone

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { voices = null; holder = null; }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            var cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, position) : 0f;
            float v = volume * Mathf.Clamp01(1f - d / AudibleMeters) * Settings.SfxVolume;
            if (v <= 0.01f) return;
            var src = Voice(v);
            if (src == null) return;   // every voice busy with a louder sound
            src.transform.position = position;
            src.clip = GoF2Remake.Modding.ModSounds.Get(clip);
            src.volume = v;
            src.Play();
        }

        /// <summary>A free voice, else the quietest one if it is quieter than 'volume'; null = none.</summary>
        static AudioSource Voice(float volume)
        {
            if (holder == null)
            {
                holder = new GameObject("Sfx voices");
                voices = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    var go = new GameObject("Sfx");
                    go.transform.SetParent(holder.transform, false);
                    var s = go.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 0f;
                    voices[i] = s;
                }
            }
            AudioSource quietest = null;
            foreach (var s in voices)
            {
                // Its pitch is left alone: TimeExtender pitches the sources it finds and puts their pitch back itself.
                if (!s.isPlaying && s.time <= 0f) return s;
                if (quietest == null || s.volume < quietest.volume) quietest = s;
            }
            if (quietest == null || quietest.volume >= volume) return null;
            quietest.Stop();
            return quietest;
        }
    }
}
