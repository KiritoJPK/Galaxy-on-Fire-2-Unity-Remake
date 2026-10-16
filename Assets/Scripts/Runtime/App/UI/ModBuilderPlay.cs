// ModBuilderPlay.cs
// Remake-only: the Mod Builder's play tests (Modding.ModBuilder.PlayTesting): the game as a player has it with the project on,
// from the builder's station: plainly (to fly to the project's systems and stations, meet its ships, take its bar missions),
// with one of its quests started (EventRunner.StartQuest), or from its campaign's start (ModCampaigns.Start). The station
// menu, the flight HUD and the pause menu work as in a game; their Back to Mod Builder (and a death, a failed quest) ends it
// (End: the builder's game as the project opened, its hangar). Plain C#.

using GoF2Remake.Data;
using GoF2Remake.Modding;
using UnityEngine.SceneManagement;

namespace GoF2Remake.UI
{
    public static class ModBuilderPlay
    {
        /// <summary>The play test of the project as it is (the station's menu at the builder's station).</summary>
        public static void Start()
        {
            ModBuilder.BeginPlayTest();
            SceneManager.LoadScene("Station");
        }

        /// <summary>A play test with the quest 'name' started (its "Starts when" skipped). The reason when it can't start.</summary>
        public static string StartQuest(string name)
        {
            ModBuilder.BeginPlayTest();
            string why = Events.EventRunner.StartQuest(name);
            if (why != null) { ModBuilder.EndPlayTest(); return why; }
            SceneManager.LoadScene("Station");
            return null;
        }

        /// <summary>A play test from the project's campaign's start (where it docks, its ship, money and quest). The reason when
        /// it can't.</summary>
        public static string StartCampaign()
        {
            var c = ModCampaigns.All().Find(x => x.mod == ModManager.BuilderProject);
            if (c == null) return Localization.Extra("mbNoCampaignToPlay", "The game hasn't read the campaign yet (or it has an error): check the messages.");
            ModBuilder.BeginPlayTest();
            string scene = ModCampaigns.Start(Database.Shared, c, out string error);
            if (scene == null) { ModBuilder.EndPlayTest(); return error; }
            SceneManager.LoadScene(scene);
            return null;
        }

        /// <summary>Back to the builder.</summary>
        public static void End() => ModBuilder.EndPlayTest();
    }
}
