// GraphQuestStateTests.cs
// Feature: CLAUDE.md "Multiplayer", Event graph quests (single player): GraphQuestState is what a save keeps of a lasting
// graph run (a quest or a bar mission: its checkpoint, variables, objective, target, quiet orbits, the bar mission's station
// and offer); Session.GraphQuests / GraphQuestsDone saved from v13; loading a save with runs flags RestorePending.
// Expected values: CLAUDE.md (the remake defines the feature): the defaults (kind Quest, no target, no station), the save
// round trip, RestorePending only with runs under way, a new game clearing them.

using System.Collections.Generic;
using GoF2Remake.Data;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class GraphQuestStateTests : StoryAreaTestBase
    {
        static GraphQuestState Sample() => new GraphQuestState
        {
            name = "kepler_rescue", kind = GraphQuestState.KindMission, title = "Rescue at Kepler", checkpoint = "docked",
            varNames = new List<string> { "wave", "score" }, varValues = new List<double> { 3, 1250.5 },
            objective = "Find the convoy", target = 57, quietOrbits = new List<int> { 57, 58 }, station = 55, offer = "{\"x\":1}",
        };

        [Test]
        public void Defaults()
        {
            var q = new GraphQuestState();
            Assert.AreEqual(GraphQuestState.KindQuest, q.kind);
            Assert.AreEqual(-1, q.target);
            Assert.AreEqual(-1, q.station);
            Assert.AreEqual("", q.checkpoint, "from the start");
            Assert.AreEqual(0, q.varNames.Count);
            Assert.AreEqual(0, q.quietOrbits.Count);
            Assert.AreNotEqual(GraphQuestState.KindMission, GraphQuestState.KindQuest);
        }

        [Test]
        public void JsonRoundTrip()
        {
            var back = JsonUtility.FromJson<GraphQuestState>(JsonUtility.ToJson(Sample()));
            Assert.AreEqual("kepler_rescue", back.name);
            Assert.AreEqual(GraphQuestState.KindMission, back.kind);
            Assert.AreEqual("Rescue at Kepler", back.title);
            Assert.AreEqual("docked", back.checkpoint);
            CollectionAssert.AreEqual(new[] { "wave", "score" }, back.varNames);
            CollectionAssert.AreEqual(new[] { 3.0, 1250.5 }, back.varValues);
            Assert.AreEqual("Find the convoy", back.objective);
            Assert.AreEqual(57, back.target);
            CollectionAssert.AreEqual(new[] { 57, 58 }, back.quietOrbits);
            Assert.AreEqual(55, back.station);
            Assert.AreEqual("{\"x\":1}", back.offer);
        }

        [Test]
        public void SavedAndRestoredWithTheGame()
        {
            Session.GraphQuests.Add(Sample());
            Session.GraphQuestsDone.Add("prologue_side");
            Session.StationIndex = 55;
            Assert.IsTrue(SaveGame.Save(8));
            Session.ResetNewGame();
            GraphQuestState.RestorePending = false;
            Assert.AreEqual(0, Session.GraphQuests.Count, "a new game has none");
            Assert.IsTrue(SaveGame.Load(8));
            Assert.AreEqual(1, Session.GraphQuests.Count);
            Assert.AreEqual("docked", Session.GraphQuests[0].checkpoint);
            CollectionAssert.AreEqual(new[] { 3.0, 1250.5 }, Session.GraphQuests[0].varValues);
            Assert.IsTrue(Session.GraphQuestsDone.Contains("prologue_side"));
            Assert.IsTrue(GraphQuestState.RestorePending, "they run again from their checkpoints");
        }

        [Test]
        public void NoRestoreWithoutRuns()
        {
            Session.StationIndex = 55;
            Assert.IsTrue(SaveGame.Save(9));
            GraphQuestState.RestorePending = true;
            Assert.IsTrue(SaveGame.Load(9));
            Assert.IsFalse(GraphQuestState.RestorePending);
        }
    }
}
