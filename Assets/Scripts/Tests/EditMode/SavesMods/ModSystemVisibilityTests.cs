// ModSystemVisibilityTests.cs
// Feature: CLAUDE.md "Mods" > Systems that come and go: systems.json "available" (ModUnlocks.SystemAvailable) shows a
// system only while its condition holds; an event's / quest's "system <system> show | hide | reset" (Session.SystemShown,
// GalaxyMap.SetShown) wins over it until reset and is saved; GalaxyMap.Visibility applies both to a copy, so the revealed
// state (Session.SystemVisible) survives; a player in a hidden system goes to GalaxyMap.FallbackStation (the nearest system
// on the map along the gate routes). The quest step "system" compiles to the command line.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Events;
using GoF2Remake.Modding;
using GoF2Remake.Multiplayer;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ModSystemVisibilityTests : SavesModsTest
    {
        const string ModId = "t_sys";
        GameHooks.ConditionCheck savedCondition;
        List<GraphQuestState> savedQuests;
        HashSet<string> savedDone;

        [SetUp]
        public void Prepare()
        {
            savedCondition = GameHooks.Condition;
            GameHooks.Condition = EventRunner.Condition;
            savedQuests = Session.GraphQuests;
            savedDone = Session.GraphQuestsDone;
            Session.GraphQuests = new List<GraphQuestState>();
            Session.GraphQuestsDone = new HashSet<string>();
            Session.SystemShown = new Dictionary<int, bool>();
            Session.SystemVisible = null;
        }

        [TearDown]
        public void Restore()
        {
            GameHooks.Condition = savedCondition;
            Session.GraphQuests = savedQuests;
            Session.GraphQuestsDone = savedDone;
            Session.SystemShown = new Dictionary<int, bool>();
            Session.SystemVisible = null;
        }

        Database ModOn()
        {
            WriteMod("sys", ModId, "1.0", "",
                ("systems.json", @"[ { 'id': 'den', 'name': 'Smugglers Den', 'race': 0, 'security': 0, 'visible': false, 'position': [50, 50, 50],
                                       'gates': [15], 'available': 'quest(t_run) == 1' } ]"),
                ("stations.json", @"[ { 'id': 'hideout', 'system': 't_sys:den', 'name': 'Hideout', 'techLevel': 3, 'planet': 1 } ]"));
            ScanAndEnable(ModId);
            Assert.IsFalse(Installed(ModId).Broken, AllMessages(Installed(ModId)));
            return Database.Load();
        }

        int Den => ModWorld.SystemIndexOf(ModId + ":den");

        [Test]
        public void TheConditionShowsTheSystemOnlyWhileItHolds()
        {
            var db = ModOn();
            Assert.GreaterOrEqual(Den, 34);
            Assert.IsFalse(GalaxyMap.IsVisible(db, Den), "quest not started");
            Assert.IsTrue(GalaxyMap.ForcedHidden(Den));

            Session.GraphQuests.Add(new GraphQuestState { name = "t_run" });
            Assert.IsTrue(GalaxyMap.IsVisible(db, Den), "while the quest runs");
            Assert.IsFalse(GalaxyMap.ForcedHidden(Den));

            Session.GraphQuests.Clear();
            Session.GraphQuestsDone.Add("t_run");
            Assert.IsFalse(GalaxyMap.IsVisible(db, Den), "the quest is over");
            Assert.IsFalse(Session.SystemVisible[Den], "the revealed state is the map's own");
        }

        [Test]
        public void AShowOrHideWinsUntilResetAndKeepsTheRevealedState()
        {
            var db = ModOn();
            GalaxyMap.SetShown(Den, true);
            Assert.IsTrue(GalaxyMap.IsVisible(db, Den), "shown over the condition");
            GalaxyMap.SetShown(Den, null);
            Assert.IsFalse(GalaxyMap.IsVisible(db, Den), "reset: the condition again");

            Assert.IsTrue(GalaxyMap.IsVisible(db, 15), "Mido, visible from the start");
            GalaxyMap.SetShown(15, false);
            Assert.IsFalse(GalaxyMap.IsVisible(db, 15));
            Assert.IsTrue(GalaxyMap.ForcedHidden(15));
            Assert.IsTrue(Session.SystemVisible[15], "still revealed underneath");
            GalaxyMap.SetShown(15, null);
            Assert.IsTrue(GalaxyMap.IsVisible(db, 15));
        }

        [Test]
        public void TheShowAndHideAreSaved()
        {
            ModOn();
            GalaxyMap.SetShown(Den, true);
            GalaxyMap.SetShown(15, false);
            Assert.IsTrue(SaveGame.Save(1));
            Session.SystemShown = new Dictionary<int, bool>();
            Assert.IsTrue(SaveGame.Load(1));
            Assert.IsTrue(Session.SystemShown.TryGetValue(Den, out bool den) && den, "the mod's system by its key");
            Assert.IsTrue(Session.SystemShown.TryGetValue(15, out bool mido) && !mido);
        }

        [Test]
        public void APlayerInAHiddenSystemGoesToTheNearestOneOnTheMap()
        {
            var db = ModOn();
            int to = GalaxyMap.FallbackStation(db, Den);
            var station = db.Stations.Find(s => s.index == to);
            Assert.IsNotNull(station);
            Assert.AreEqual(15, station.system, "through its gate to Mido");

            GalaxyMap.SetShown(15, false);   // Mido hidden too: further along Mido's routes, or the last docked station
            to = GalaxyMap.FallbackStation(db, Den);
            int system = db.Stations.Find(s => s.index == to).system;
            Assert.AreNotEqual(Den, system);
            Assert.IsTrue(GalaxyMap.IsVisible(db, system));
        }

        [Test]
        public void TheQuestStepAndTheCommandsSystems()
        {
            var problems = new List<string>();
            string script = QuestFile.Compile(new JObject
            {
                ["format"] = "gof2quest", ["title"] = "T",
                ["steps"] = new JArray(new JObject { ["type"] = "system", ["system"] = "t_sys:den", ["action"] = "hide" },
                                       new JObject { ["type"] = "system", ["system"] = 15 }),
            }, problems);
            Assert.IsEmpty(problems, string.Join("; ", problems));
            StringAssert.Contains("system @a t_sys:den hide", script);
            StringAssert.Contains("system @a 15 show", script, "show by default");

            Assert.IsTrue(NetAdmin.ParseSystem("15", out int n) && n == 15);
            string mido = Database.Load().Systems.Find(s => s.index == 15).name;
            Assert.IsTrue(NetAdmin.ParseSystem(mido.ToUpperInvariant(), out n) && n == 15, "by name, any case");
            Assert.IsFalse(NetAdmin.ParseSystem("no such system", out _));
            Assert.IsFalse(NetAdmin.ParseSystem("9999", out _));
        }
    }
}
