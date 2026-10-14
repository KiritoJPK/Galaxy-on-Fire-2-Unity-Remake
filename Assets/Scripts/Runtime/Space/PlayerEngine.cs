// PlayerEngine.cs
// The player ship's engine loop and boost sound (the FEV's LGCY data, fev_lgcy.py --event 42..45 / 1104-1107 / 38-41).
//   PlayerEgo::PlayerEgo 0xa5940    +0x1c engine event: ship 42 -> 1104 EngineDLC_08, 43 -> 1106 EngineDLC_11, 40 -> 1107
//                                   EngineDLC_01, else by handling (Ship::getHandling): >= 1.4 45, >= 1.15 44, >= 0.95 43,
//                                   else 42; +0xd4 boost sound: boosters 71-74 -> 38-41, 195 -> 1102 Polytron_Boost
//   MGame::OnInitialize             FModSound::play(+0x1c) at the level's start; PlayerEgo::PauseEngineSound while mining
//   PlayerEgo::handleShip 0xa7df0   parameter 0 "Vertical" = max(|stick x|, |stick y|) (+0x268 / +0x270, PlayerEgo::right /
//                                   down): the pitch envelope x0.891 -> x1 at 0.5 -> x1.122 (42 / 44 / 45; 43 x0.944 .. x1.059;
//                                   the DLC engines x0.891 .. x1.122 linear, only their second layer); parameter 1 = x * 0.2 +
//                                   0.5 (speaker spread, no 2D effect); "load" is never set: its volume envelope's 0.7
//   PlayerEgo::boost                FModSound::play(+0xd4) when a boost starts
// In the supernova orbits the gamma shield's loop takes the engine's slot (PlayerHealth plays it), so no engine then.
// Volumes: the event volume x Sfx.EventGain, x the events' linear rolloff from the camera (EngineVoices; the sound stays
// 2D, only its volume follows the distance).

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class PlayerEngine : MonoBehaviour
    {
        const float LoadGain = 0.7f;
        static readonly float[] EngineVolume = { 0.0977f, 0.0955f, 0.0876f, 0.0876f, 0.0746f, 0.068f, 0.0566f };
        static readonly float[] BoostVolume = { 0.2018f, 0.195f, 0.1862f, 0.2065f, 0.2526f };

        ShipController ship;
        PlayerHealth health;
        Mining mining;
        AudioSource main, extra, boostSource;
        int engine = -1, boost = -1;
        bool wasBoosting;
        /// <summary>MGame::OnInitialize starts the engine loop only above campaign index 1: the prologue's and the rescue's
        /// Phantom is silent (index 0 swaps it for the broken engine 156, IntroCutscenes).</summary>
        bool storySilent;

        public static PlayerEngine Attach(GameObject player, Database db, ShipController ship)
        {
            var e = player.AddComponent<PlayerEngine>();
            e.Setup(db, ship);
            e.storySilent = !Session.FreePlay && Session.CampaignMission <= 1;
            return e;
        }

        void Setup(Database db, ShipController shipController)
        {
            ship = shipController;
            health = GetComponent<PlayerHealth>();
            mining = GetComponent<Mining>();
            var audio = CombatAudio.Load();
            engine = EngineIndex(db, Session.ShipIndex);
            var booster = Shop.FirstMounted(db, 14);
            boost = booster == null ? -1 : booster.index >= 71 && booster.index <= 74 ? booster.index - 71 : booster.index == 195 ? 4 : -1;
            if (audio == null) return;
            main = Loop(Pick(audio.playerEngines, engine));
            extra = Loop(Pick(audio.playerEngineExtras, engine));
            boostSource = gameObject.AddComponent<AudioSource>();
            boostSource.playOnAwake = false;
            boostSource.spatialBlend = 0f;
            boostSource.clip = GoF2Remake.Modding.ModSounds.Get(Pick(audio.boosters, boost));
        }

        /// <summary>PlayerEgo::PlayerEgo 0xa5940: ships 42 / 43 / 40 their DLC engines, else by handling.</summary>
        static int EngineIndex(Database db, int ship)
        {
            float handling = (db.Ship(ship)?.handling ?? 100) / 100f;
            return ship == 42 ? 4 : ship == 43 ? 5 : ship == 40 ? 6
                 : handling >= 1.4f ? 3 : handling >= 1.15f ? 2 : handling >= 0.95f ? 1 : 0;
        }

        /// <summary>The ship's engine loop and its volume (event volume x load x EventGain, without the FX volume): the
        /// hangar flights (HangarFlight).</summary>
        public static AudioClip EngineClip(Database db, int ship, out float volume)
        {
            int e = EngineIndex(db, ship);
            volume = EngineVolume[e] * LoadGain * Sfx.EventGain;
            return Pick(CombatAudio.Load()?.playerEngines, e);
        }

        static AudioClip Pick(AudioClip[] list, int i) => list != null && i >= 0 && i < list.Length ? list[i] : null;

        AudioSource Loop(AudioClip clip)
        {
            if (clip == null) return null;
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.clip = GoF2Remake.Modding.ModSounds.Get(clip);
            return s;
        }

        void Update()
        {
            if (ship == null) return;
            if (health == null) health = GetComponent<PlayerHealth>();
            if (mining == null) mining = GetComponent<Mining>();
            bool visible = ship.visualModel == null || ship.visualModel.gameObject.activeInHierarchy;
            bool on = visible && !storySilent && (health == null || (!health.Dead && !health.GammaLoopActive))
                      && (mining == null || (mining.State != Mining.Phase.Docked && mining.State != Mining.Phase.Mining));
            // "Vertical": the stick's larger axis, 0..1.
            var stick = ship.SteerInput;
            float v = Mathf.Clamp01(Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y)));
            float pitch = engine >= 4 ? Mathf.Pow(2f, 8f * Mathf.Lerp(0.479167f, 0.520833f, v) - 4f)
                        : engine == 1 ? PitchEnvelope(v, 0.489583f, 0.510417f) : PitchEnvelope(v, 0.479167f, 0.520833f);
            float volume = (engine >= 0 ? EngineVolume[engine] : 0f) * LoadGain * Sfx.EventGain * Settings.SfxVolume;
            // A 3D event at the ship heard from the camera (EngineVoices): about 85 % from the chase camera, quiet from the
            // launch camera's fly-by.
            var ear = EngineVoices.Listener();
            // Remake debug: a capital ship's fitted camera sits km out, far past the 500 m rolloff: the distance is scaled by
            // the normal chase distance / the fitted one (ShipController.engineEarScale, 1 for an ordinary ship).
            if (ear != null) volume *= EngineVoices.Rolloff(Vector3.Distance(ear.position, transform.position) * ship.engineEarScale);
            // ... and deeper the heavier it is (ShipController.mass).
            float deep = Mathf.Lerp(1f, 0.6f, ship.mass);
            // 1104: one layer with the pitch; 1106 / 1107: EngineDLC_07 plain + the pitched second layer.
            Drive(main, on, volume, (engine >= 5 ? 1f : pitch) * deep);
            Drive(extra, on, volume, pitch * deep);

            bool boosting = ship.Model != null && ship.Model.IsBoosting;
            if (boosting && !wasBoosting && boostSource != null && boostSource.clip != null)
            {
                boostSource.pitch = Mathf.Lerp(1f, 0.7f, ship.mass);   // 1 for every ordinary ship, as before
                boostSource.PlayOneShot(GoF2Remake.Modding.ModSounds.Get(boostSource.clip), BoostVolume[boost] * Sfx.EventGain * Settings.SfxVolume);
            }
            wasBoosting = boosting;
        }

        static float PitchEnvelope(float v, float low, float high) =>
            Mathf.Pow(2f, 8f * (v < 0.5f ? Mathf.Lerp(low, 0.5f, v * 2f) : Mathf.Lerp(0.5f, high, v * 2f - 1f)) - 4f);

        static void Drive(AudioSource s, bool on, float volume, float pitch)
        {
            if (s == null) return;
            s.volume = volume;
            s.pitch = pitch * TimeExtender.SoundPitch;
            if (on && !s.isPlaying) s.Play();
            else if (!on && s.isPlaying) s.Stop();
        }
    }
}
