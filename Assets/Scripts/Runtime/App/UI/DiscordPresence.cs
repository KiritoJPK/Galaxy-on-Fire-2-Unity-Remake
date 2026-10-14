// DiscordPresence.cs
// Remake: Discord Rich Presence on desktop ("Playing Galaxy on Fire 2 Unity Remake" + what the player is doing), through
// DiscordIpc and the Discord application 1555054317155647571 (its art assets: logo, gof2 / valkyrie / supernova, the race
// emblems race_terran / vossk / nivelian / midorian / pirate / void). Every 2 s it reads the current scene:
//   main menu        "In the main menu"                                    large: logo
//   docked           "Docked at <station>" (" · Space Lounge")              large: the campaign's art, small: the system's race
//   in flight        "Mining" / "In combat" / "Watching a cutscene" / "Flying near <station>" / "In the Void"
//   second line      the freelance mission ("Freelance: Junk removal"), else the story step's title, else free play;
//                    multiplayer: "Multiplayer · squad of N"
// The timer runs from the start of the game session. Off with Options > Gameplay "Discord status", and on mobile.

using GoF2Remake.Data;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class DiscordPresence : MonoBehaviour
    {
        const string ApplicationId = "1555054317155647571";
        static DiscordPresence instance;

        DiscordIpc ipc;
        long startUnix;
        float nextUpdate;
        string lastJson;

        public static void Install()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (instance != null) return;
            var go = new GameObject("DiscordPresence");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DiscordPresence>();
#endif
        }

        void Start()
        {
            startUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ipc = new DiscordIpc(ApplicationId);
            ipc.Start();
        }

        void OnDestroy()
        {
            ipc?.Dispose();
            if (instance == this) instance = null;
        }

        void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 2f;
            string json = Settings.DiscordPresence ? BuildActivity() : null;
            if (json == lastJson) return;
            lastJson = json;
            ipc.SetActivity(json);
        }

        string BuildActivity()
        {
            string details, state = null, large = "logo", largeText = "Galaxy on Fire 2", small = null, smallText = null;
            var scene = SceneManager.GetActiveScene().name;
            bool inGame = scene == "Space" || scene == "Station";
            if (!inGame)
            {
                details = Localization.Extra("discordMenu", "In the main menu");
            }
            else
            {
                (large, largeText) = Session.FreePlay ? ("gof2", "Free play")
                    : Session.Campaign == Campaign.Supernova ? ("supernova", "Supernova")
                    : Session.Campaign == Campaign.Valkyrie ? ("valkyrie", "Valkyrie") : ("gof2", "Galaxy on Fire 2");
                largeText += " · " + Session.DifficultyName(Session.Difficulty);
                var db = Database.Shared;   // every 2 s: Load() parsed every table again (a hitch and MBs of garbage)
                var station = db.Stations.Find(s => s.index == Session.StationIndex);
                string stationName = station != null ? station.name : "";
                if (station != null)
                {
                    var system = db.Systems.Find(s => s.index == station.system);
                    small = RaceIcon(system != null ? system.raceId : -1);
                    smallText = station.systemName + " system";
                }
                if (scene == "Station")
                {
                    var level = FindAnyObjectByType<StationLevel>();
                    details = "Docked at " + stationName;
                    if (level != null && level.View == StationView.Lounge) details += " · Space Lounge";
                }
                else
                {
                    var level = FindAnyObjectByType<SpaceLevel>();
                    bool voidOrbit = Session.StationIndex == Session.VoidOrbit;
                    if (voidOrbit) { small = "race_void"; smallText = "The Void"; }
                    if (level != null && level.Cutscene) details = "Watching a cutscene";
                    else if (level != null && level.Mining != null && level.Mining.State != Flight.Mining.Phase.Idle) details = "Mining an asteroid";
                    else if (level != null && level.Traffic != null && level.Traffic.HostileCount > 0) details = "In combat";
                    else details = voidOrbit ? "In the Void" : "Flying near " + stationName;
                }
                if (!Session.FreelanceMission.IsEmpty) state = "Freelance: " + Session.FreelanceMission.Name;
                else if (!Session.FreePlay && StepSummaries.Get(Story.Index) is StepSummaries.Entry step && !string.IsNullOrEmpty(step.title))
                    state = "Story: " + step.title;
                if (Multiplayer.NetGame.Active)
                {
                    int squad = Multiplayer.NetSquad.Members().Count;
                    state = squad > 1 ? $"Multiplayer · squad of {squad}" : "Multiplayer";
                }
            }

            var sb = new System.Text.StringBuilder("{\"type\":0");
            sb.Append(",\"details\":").Append(DiscordIpc.Quote(Fit(details)));
            if (!string.IsNullOrEmpty(state)) sb.Append(",\"state\":").Append(DiscordIpc.Quote(Fit(state)));
            sb.Append(",\"timestamps\":{\"start\":").Append(startUnix).Append('}');
            sb.Append(",\"assets\":{\"large_image\":").Append(DiscordIpc.Quote(large)).Append(",\"large_text\":").Append(DiscordIpc.Quote(Fit(largeText)));
            if (small != null) sb.Append(",\"small_image\":").Append(DiscordIpc.Quote(small)).Append(",\"small_text\":").Append(DiscordIpc.Quote(Fit(smallText ?? small)));
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>Discord takes 2..128 characters per text field.</summary>
        static string Fit(string s)
        {
            if (string.IsNullOrEmpty(s)) return "--";
            if (s.Length < 2) s += " ";
            return s.Length > 128 ? s.Substring(0, 127) + "…" : s;
        }

        static string RaceIcon(int race) => race switch
        {
            0 => "race_terran",
            1 => "race_vossk",
            2 => "race_nivelian",
            3 => "race_midorian",
            8 => "race_pirate",
            9 => "race_void",
            _ => null,
        };
    }
}
