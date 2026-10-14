// ShotVoices.cs
// The gun shot sounds' voices, shared by the player's guns, NPC guns and other players' mirrored shots. The FEV's weapon
// events allow max_playbacks 2 (behaviour 1, steal newest; the missile launches 1): at most two of the same shot play at
// once, a third restarts the newest. Before, every shot was its own PlayOneShot with unlimited overlap: a few ships firing
// summed far past full scale and clipped (the "crunchy" guns, worst in multiplayer, where every mirrored gun played too).
// 2D sources, the distance falloff of Sfx.PlayAt for shots away from the camera.

using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ShotVoices
    {
        const int MaxPerClip = 2;

        sealed class Voices
        {
            public readonly AudioSource[] sources = new AudioSource[MaxPerClip];
            public readonly float[] started = new float[MaxPerClip];
        }

        static GameObject host;
        static readonly Dictionary<AudioClip, Voices> byClip = new Dictionary<AudioClip, Voices>();

        /// <summary>A shot at the listener (the player's own guns); 'volume' already includes the FX volume; 'pitch' the
        /// player's primaries' (a fire-rate weapon mod, WeaponSystem).</summary>
        public static void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null || volume <= 0.001f) return;
            if (host == null)
            {
                host = new GameObject("ShotVoices");
                Object.DontDestroyOnLoad(host);
                byClip.Clear();
            }
            if (!byClip.TryGetValue(clip, out var v)) byClip[clip] = v = new Voices();
            float now = Time.unscaledTime;
            int pick = -1, newest = 0;
            for (int i = 0; i < MaxPerClip; i++)
            {
                var s = v.sources[i];
                if (s == null)
                {
                    s = v.sources[i] = host.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 0f;
                    s.clip = GoF2Remake.Modding.ModSounds.Get(clip);
                }
                if (pick < 0 && !s.isPlaying) pick = i;
                if (v.started[i] > v.started[newest]) newest = i;
            }
            if (pick < 0) pick = newest;   // behaviour 1: the newest instance gives way
            var src = v.sources[pick];
            src.volume = volume;
            src.pitch = pitch;
            src.Stop();
            src.Play();
            v.started[pick] = now;
        }

        /// <summary>A shot somewhere in space: falls off linearly to Sfx.AudibleMeters from the camera, times the FX volume.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;
            var cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, position) : 0f;
            Play(clip, volume * Mathf.Clamp01(1f - d / Sfx.AudibleMeters) * Settings.SfxVolume);
        }
    }
}
