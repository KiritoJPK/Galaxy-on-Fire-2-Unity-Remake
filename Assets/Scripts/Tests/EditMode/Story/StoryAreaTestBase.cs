// StoryAreaTestBase.cs
// Shared base of the Story-area tests (story, freelance, bar agents, wingmen, Most Wanted, pirate board, medals, New Game+):
// SandboxTest's temporary data folder and fresh new game, plus the static state Session.ResetNewGame leaves alone (the
// campaign, difficulty and economy, the fail counter), the random generators the rules draw from (AgentGenerator.Rng,
// UnityEngine.Random) and the multiplayer providers (no session while these tests run), all put back afterwards.

using System;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public abstract class StoryAreaTestBase : SandboxTest
    {
        Campaign savedCampaign;
        float savedDifficulty;
        Economy savedEconomy;
        int savedLastFailed, savedFailCount;
        Random savedRng;
        UnityEngine.Random.State savedUnityRandom;
        Func<bool> savedActive, savedSessionGame;
        Func<double> savedServerTime;
        bool savedRestorePending;

        [SetUp]
        public void SaveStoryStatics()
        {
            savedCampaign = Session.Campaign;
            savedDifficulty = Session.Difficulty;
            savedEconomy = Session.Economy;
            savedLastFailed = Session.LastFailedMission;
            savedFailCount = Session.FailCount;
            savedRng = AgentGenerator.Rng;
            savedUnityRandom = UnityEngine.Random.state;
            savedActive = NetSession.ActiveProvider;
            savedSessionGame = NetSession.SessionGameProvider;
            savedServerTime = NetSession.ServerTimeProvider;
            savedRestorePending = GraphQuestState.RestorePending;
            // Single player, Normal, the story campaign, reproducible draws.
            NetSession.ActiveProvider = null;
            NetSession.SessionGameProvider = null;
            NetSession.ServerTimeProvider = null;
            Session.Campaign = Campaign.GalaxyOnFire2;
            Session.Difficulty = Session.DifficultyNormal;
            AgentGenerator.Rng = new Random(12345);
            UnityEngine.Random.InitState(12345);
        }

        [TearDown]
        public void RestoreStoryStatics()
        {
            Session.Campaign = savedCampaign;
            Session.Difficulty = savedDifficulty;
            Session.Economy = savedEconomy;
            Session.LastFailedMission = savedLastFailed;
            Session.FailCount = savedFailCount;
            AgentGenerator.Rng = savedRng;
            UnityEngine.Random.state = savedUnityRandom;
            NetSession.ActiveProvider = savedActive;
            NetSession.SessionGameProvider = savedSessionGame;
            NetSession.ServerTimeProvider = savedServerTime;
            GraphQuestState.RestorePending = savedRestorePending;
        }

        /// <summary>A step as the game has it after nextCampaignMission created it (Status+0x1e8 = index, slot 0 = its mission).</summary>
        protected static void AtStep(int index) => Story.StartNewGame(index);

        /// <summary>Enter step 'n' from step n - 1 (Status::nextCampaignMission's case n - 1); returns the new index.</summary>
        protected static int EnterStep(int n)
        {
            Story.StartNewGame(n - 1);
            return Story.Advance(Db);
        }

        /// <summary>The player's rank set through kills alone (free play: the campaign index doesn't count; one station
        /// visited): Status::checkForLevelUp's XP thresholds 0, 7, 21, 42, 70, 105, 147, 196, 252, 315, 385, 462, 546, 637,
        /// 735, 840, 952, 1071, 1197, 1330, 1650 (npc_traffic_ai.md 2.2).</summary>
        protected static void SetRankFreePlay(int rank)
        {
            int[] xp = { 0, 7, 21, 42, 70, 105, 147, 196, 252, 315, 385, 462, 546, 637, 735, 840, 952, 1071, 1197, 1330, 1650 };
            Session.FreePlay = true;
            Session.VisitedStations = new System.Collections.Generic.HashSet<int> { 78 };
            Session.Kills = Math.Max(0, xp[rank] - 1);
            Assert.AreEqual(rank, Session.Rank, "rank set up through kills");
        }

        protected static bool Visible(int system)
        {
            var v = GalaxyMap.Visibility(Db);
            return system >= 0 && system < v.Length && v[system];
        }
    }
}
