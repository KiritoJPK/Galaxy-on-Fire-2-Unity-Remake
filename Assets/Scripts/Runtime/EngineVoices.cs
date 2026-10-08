// EngineVoices.cs
// The engine loops in space as the original's FMOD events play them (the FEV's LGCY data, fev_lgcy.py --event 42..48):
// every engine event is 3D with linear rolloff from min_distance 1 to max_distance 10000 game units (0.05 .. 500 m), the
// listener at the camera (MGame::OnUpdate with 3D sound on, Globals::options[0xf], the default) and the sound at the ship
// (Player::PlayEngineSound 0xb09c4 / Player::update: the ship's position and velocity every frame). The NPC engines (46 enemy,
// 47 freighter, 48 wingmen) have max_playbacks 3 with behaviour 5 ("just fail if quietest"): at most three of each event
// sound at once, the loudest; they fade in over 800 ms and out over 200 ms. The remake used to give every ship its own loop
// audible to 1.5 km, which made busy orbits loud.

using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class EngineVoices
    {
        public const float MinMeters = 0.05f, MaxMeters = 500f;   // 1 / 10000 game units
        const int MaxPerEvent = 3;
        const float FadeInMs = 800f, FadeOutMs = 200f;

        sealed class Voice
        {
            public AudioSource source;
            public int eventId;
            public float baseVolume, gain, audible;
            public bool loudest;   // among the event's MaxPerEvent loudest this frame
        }

        static readonly int[] Events = { 46, 47, 48 };   // not a new array every frame

        static readonly List<Voice> voices = new List<Voice>();
        static readonly List<Voice> candidates = new List<Voice>();
        static int tickedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { voices.Clear(); tickedFrame = -1; listener = null; }

        /// <summary>The engine events' 3D settings on a Unity source: linear from 0.05 to 500 m.</summary>
        public static void Setup3D(AudioSource s)
        {
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = MinMeters;
            s.maxDistance = MaxMeters;
            s.dopplerLevel = 0f;
        }

        /// <summary>The volume factor of a sound this far from the listener (the events' linear rolloff).</summary>
        public static float Rolloff(float meters) => Mathf.Clamp01(1f - (meters - MinMeters) / (MaxMeters - MinMeters));

        /// <summary>An NPC engine loop of event 'eventId' (46 / 47 / 48) at 'baseVolume' (the event volume x Sfx.EventGain,
        /// without the FX volume); again with another event or volume (a ship becoming a wingman) replaces it.</summary>
        public static void Register(AudioSource source, int eventId, float baseVolume)
        {
            if (source == null) return;
            var v = voices.Find(x => x.source == source);
            if (v == null) voices.Add(v = new Voice { source = source });
            v.eventId = eventId;
            v.baseVolume = baseVolume;
            source.volume = 0f;
        }

        public static void Unregister(AudioSource source) => voices.RemoveAll(x => x.source == source);

        /// <summary>Once a frame (NpcShip.Update calls it): per event, the three loudest playing loops fade in, the rest out.</summary>
        public static void Tick()
        {
            if (tickedFrame == Time.frameCount) return;
            tickedFrame = Time.frameCount;
            voices.RemoveAll(x => x.source == null);
            var at = Listener();
            float dtMs = Time.unscaledDeltaTime * 1000f;
            foreach (int id in Events)
            {
                candidates.Clear();
                foreach (var v in voices)
                {
                    if (v.eventId != id) continue;
                    bool playing = v.source.isActiveAndEnabled && v.source.isPlaying;
                    float d = at != null ? Vector3.Distance(at.position, v.source.transform.position) : 0f;
                    v.audible = playing ? v.baseVolume * Rolloff(d) : 0f;
                    if (v.audible > 0f) candidates.Add(v);
                }
                candidates.Sort((a, b) => b.audible.CompareTo(a.audible));
                foreach (var v in voices) v.loudest = false;
                for (int i = 0; i < candidates.Count && i < MaxPerEvent; i++) candidates[i].loudest = true;   // was IndexOf per voice
                foreach (var v in voices)
                {
                    if (v.eventId != id) continue;
                    bool on = v.loudest;
                    v.gain = Mathf.MoveTowards(v.gain, on ? 1f : 0f, dtMs / (on ? FadeInMs : FadeOutMs));
                    v.source.volume = v.baseVolume * v.gain * Settings.SfxVolume;
                }
            }
        }

        static AudioListener listener;

        /// <summary>Where the sound is heard: the scene's AudioListener (the level camera), found again after a scene change.</summary>
        public static Transform Listener()
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener != null && listener.isActiveAndEnabled) return listener.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }
    }
}
