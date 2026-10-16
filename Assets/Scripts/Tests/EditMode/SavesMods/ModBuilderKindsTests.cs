// ModBuilderKindsTests.cs
// Feature: CLAUDE.md "Mods" > Mod Builder: its other kinds of content. A project keeps its systems, stations, rooms,
// blueprints, new-game options, campaign and step quests in the mods' own files (ModProject: written, read back, in the undo
// steps; a removed quest's file goes); the step quests (.gof2quest, Events.QuestFile) become a script EventRunner reads, every
// step its line; picking another mod's content makes that mod a dependency (ModRefs, mod.json "dependencies"), and the
// project knows the mods its files name; the expressions docked(station) / inorbit(station) read; a room on one of the
// game's rooms gets that room's markers from the game's tables (StationLevel.GameRoomMarkers).
// Expected values: the formats in Modding/README.md; the Terran hangar's tables (StationTables: 2 parked pads, the flights'
// gate at (0, 55, -549)); the Terran bar's 7 visitor spots.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GoF2Remake.Events;
using GoF2Remake.Modding;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class ModBuilderKindsTests : SavesModsTest
    {
        [Test]
        public void TheNewKindsAreWrittenAndReadBack()
        {
            var p = ModProject.Create("Kinds test", "tests");
            var sys = p.AddSystem("Kepler", new Vector3(60, 40, 50), 2);
            sys.Gates = new List<string> { "4", "other:far" };
            var st = p.AddStation("Kepler Prime", p.Key(sys.LocalId), 6, 12);
            st.Model = "gof2:station_078_midorian";
            var room = p.AddInterior("Kepler hangar", false, "gof2:hangar_terran");
            room.SetMarker("pad", new Vector3(1, 2, 3));
            st.Hangar = p.Key(room.Id);
            var bp = p.AddBlueprint("Falcon", true, "28");
            bp.Ingredients = new List<ProjectStack> { new ProjectStack("155", 400) };
            bp.SetAutocomplete(true, 150000);
            p.AddGameOption("Hard mode").Default = true;
            p.Campaign = new ProjectCampaign(new JObject { ["name"] = "Frontier", ["startStation"] = p.Key(st.LocalId), ["startShip"] = 10 });
            var q = p.AddQuest("The signal", false);
            q.AddStep("dialog")["pages"] = new JArray(new JObject { ["speaker"] = "Keith", ["text"] = "Hello." });
            p.Save();

            var back = ModProject.Load(p.Folder);
            Assert.IsNull(back.LoadError);
            Assert.AreEqual("Kepler", back.Systems.Single().Name);
            CollectionAssert.AreEqual(new[] { "4", "other:far" }, back.Systems[0].Gates);
            Assert.AreEqual(new Vector3(60, 40, 50), back.Systems[0].Position);
            Assert.AreEqual("kinds_test:kepler", back.Stations.Single().System);
            Assert.AreEqual("kinds_test:kepler_hangar", back.Stations[0].Hangar);
            Assert.AreEqual(new Vector3(1, 2, 3), back.Interiors.Single().Marker("pad"));
            Assert.AreEqual(150000, back.Blueprints.Single().AutocompletePrice);
            Assert.AreEqual(400, back.Blueprints[0].Ingredients.Single().amount);
            Assert.IsTrue(back.GameOptions.Single().Default);
            Assert.AreEqual("kinds_test:kepler_prime", back.Campaign.StartStation);
            Assert.AreEqual("the_signal", back.Quests.Single().Name);
            Assert.IsTrue(File.Exists(Path.Combine(p.Folder, "events", "the_signal.gof2quest")));
            // The files are the mods' own: the game's parsers read them.
            Assert.IsTrue(File.Exists(Path.Combine(p.Folder, "systems.json")) && File.Exists(Path.Combine(p.Folder, "interiors.json")));

            // A removed quest's file goes with the next save.
            back.Quests.Clear();
            back.Save();
            Assert.IsFalse(File.Exists(Path.Combine(p.Folder, "events", "the_signal.gof2quest")));
        }

        [Test]
        public void UndoTakesTheNewKindsBack()
        {
            var p = ModProject.Create("Kinds undo", "tests");
            using var history = new ModProjectHistory(p);
            p.AddSystem("Kepler", new Vector3(50, 50, 50), 0);
            p.AddQuest("Q", true);
            p.Campaign = new ProjectCampaign(new JObject { ["name"] = "C" });
            history.Commit();
            history.Undo();
            Assert.IsEmpty(p.Systems);
            Assert.IsEmpty(p.Quests);
            Assert.IsNull(p.Campaign);
            history.Redo();
            Assert.AreEqual(1, p.Systems.Count);
            Assert.AreEqual("q", p.Quests.Single().Name);
            Assert.AreEqual("C", p.Campaign.Name);
        }

        static JObject Step(string type, params (string k, JToken v)[] fields)
        {
            var o = new JObject { ["type"] = type };
            foreach (var (k, v) in fields) o[k] = v;
            return o;
        }

        [Test]
        public void EveryStepBecomesItsLine()
        {
            var quest = new JObject
            {
                ["format"] = "gof2quest", ["title"] = "Signal", ["kind"] = "quest", ["startsWhen"] = "visited(78)", ["reward"] = 500,
                ["steps"] = new JArray(
                    Step("dialog", ("pages", new JArray(new JObject { ["speaker"] = "Keith", ["text"] = "One # two | three {x}", ["voice"] = "clip_1" }))),
                    Step("radio", ("speaker", "vossk K'ekki"), ("text", "Come.")),
                    Step("objective", ("text", "Go"), ("station", 78)),
                    Step("goto", ("station", 78), ("dock", true)),
                    Step("fight", ("station", 70), ("name", "Raider"), ("ships", new JArray(new JObject { ["ship"] = 2, ["count"] = 3, ["race"] = "pirate" }))),
                    Step("cargo", ("item", 22), ("amount", 5)),
                    Step("reward", ("credits", 100), ("items", new JArray(new JObject { ["item"] = 46, ["amount"] = 2 }))),
                    Step("give", ("item", 22), ("amount", 1)),
                    Step("wait", ("seconds", 2)),
                    Step("waituntil", ("condition", "credits > 5")),
                    Step("checkpoint"),
                    Step("music", ("track", "battle")),
                    Step("title", ("title", "Hi"), ("subtitle", "there")),
                    Step("waypoint", ("station", 78), ("at", "0 0 20000")),
                    Step("startquest", ("quest", "next_one")),
                    Step("command", ("line", "/heal @a")),
                    Step("complete", ("reward", 1000))),
            };
            var problems = new List<string>();
            string script = QuestFile.Compile(quest, problems);
            CollectionAssert.IsEmpty(problems);
            var lines = script.Split('\n').Select(l => l.Trim()).ToList();
            foreach (var expected in new[]
            {
                "mission title Signal", "mission kind quest", "mission starts visited(78)", "mission reward 500",
                "dialog @a Keith : One No. two / three (x) [voice clip_1]",
                "radio @a vossk K'ekki : Come.",
                "objective Go", "target 78",
                "wait until docked(78) > 0",
                "questorbit 70", "wait until inorbit(70) > 0", "spawn @alive[orbit=70] 2 pirate 3 enemy named Raider", "wait until alive(Raider) == 0", "questorbit 70 off",
                "wait until cargo(22) >= 5",
                "reward @a 100 + 46 2", "give @a 22 1", "wait 2", "wait until credits > 5", "checkpoint step11", "music @a battle",
                "title @a Hi | there", "waypoint @a 78 0 0 20000", "startquest next_one", "heal @a", "complete 1000",
            })
                CollectionAssert.Contains(lines, expected);
            // EventRunner reads the whole script (its own parser).
            var compile = typeof(EventRunner).GetMethod("Compile", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string).MakeByRefType() }, null);
            Assert.IsNotNull(compile, "EventRunner.Compile");
            var args = new object[] { script, null };
            Assert.IsNotNull(compile.Invoke(null, args), "the script compiles: " + args[1]);
            Assert.IsNull(args[1]);
        }

        [Test]
        public void AQuestWithoutAnEndCompletes()
        {
            var problems = new List<string>();
            string script = QuestFile.Compile(new JObject { ["format"] = "gof2quest", ["title"] = "T", ["kind"] = "mission", ["steps"] = new JArray(Step("wait", ("seconds", 1))) }, problems);
            Assert.IsTrue(script.TrimEnd().EndsWith("complete"));
            StringAssert.Contains("mission kind mission", script);
            StringAssert.Contains("mission players 1 4", script);
            Assert.IsTrue(QuestFile.Is("{ \"format\": \"gof2quest\" }"));
            Assert.IsFalse(QuestFile.Is("%YAML 1.1"));
        }

        [Test]
        public void ABadStepIsAProblem()
        {
            var problems = new List<string>();
            QuestFile.Compile(new JObject { ["title"] = "T", ["steps"] = new JArray(Step("fight"), Step("nonsense")) }, problems);
            Assert.AreEqual(2, problems.Count);
        }

        [Test]
        public void AnotherModsContentBecomesADependency()
        {
            WriteMod("ship_pack", "ship_pack", "1.2", "", ("ships.json", "[ { 'id': 'falcon', 'name': 'Falcon', 'model': 'models/f.glb', 'armor': 100 } ]"),
                     ("items.json", "[ { 'id': 'pulse', 'base': 0, 'name': 'Pulse cannon' } ]"),
                     ("characters.json", "[ { 'id': 'vega', 'name': 'Captain Vega', 'portrait': 'p.png' } ]"));
            WriteFile(Path.Combine(ModsFolder, "ship_pack"), "models/f.glb", Dummy);
            WriteFile(Path.Combine(ModsFolder, "ship_pack"), "p.png", Dummy);
            ModManager.Scan();
            var p = ModProject.Create("Uses others", "tests");
            p.AddItem(0, "Mine");
            p.Save();

            var items = ModRefs.List(RefKind.Item, p);
            Assert.IsTrue(items.Any(e => e.value == "0" && e.group == ModRefs.GameGroup), "the game's items");
            Assert.IsTrue(items.Any(e => e.value == "uses_others:mine" && e.own), "the project's own, by key");
            var pulse = items.Single(e => e.value == "ship_pack:pulse");
            Assert.AreEqual("ship_pack", pulse.modId);
            Assert.AreEqual(0, pulse.icon, "its base's icon");
            Assert.IsTrue(ModRefs.List(RefKind.Ship, p).Any(e => e.value == "ship_pack:falcon"));
            Assert.IsTrue(ModRefs.List(RefKind.Character, p).Any(e => e.value == "ship_pack:vega" && e.name == "Captain Vega"));

            Assert.IsTrue(ModRefs.Use(p, pulse), "a new dependency");
            Assert.IsFalse(ModRefs.Use(p, pulse), "only once");
            CollectionAssert.AreEqual(new[] { "ship_pack" }, p.Dependencies);
            p.Save();
            var m = ModManifest.Read(ModSource.Open(p.Folder));
            CollectionAssert.Contains(m.dependencies, "ship_pack");
            Assert.AreEqual("1.2", m.dependencyVersions["ship_pack"], "the version installed now is the lowest needed");

            // The project names the mods its files use.
            p.Campaign = new ProjectCampaign(new JObject { ["name"] = "C", ["startShip"] = "ship_pack:falcon", ["startStation"] = 78 });
            CollectionAssert.Contains(p.ModsNamed(), "ship_pack");
            p.RemoveDependency("ship_pack");
            CollectionAssert.IsEmpty(p.Dependencies);
            Assert.AreEqual("Pulse cannon (Test ship_pack)", ModRefs.Label(RefKind.Item, "ship_pack:pulse", p));
        }

        [Test]
        public void DockedAndInOrbitRead()
        {
            Assert.IsTrue(EventRunner.Condition("docked(78) >= 0 and inorbit(Var Hastra) >= 0", out string error), error);
            Assert.IsNull(error);
            Assert.IsFalse(EventRunner.Condition("docked(nowhere at all) >= 0", out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void AModsTrailOverTheGames()
        {
            var db = GoF2Remake.Data.Database.Shared;
            int missile = Enumerable.Range(0, 233).First(i => db.Item(i) is GoF2Remake.Data.ItemData d && d.TypeId == 1 && d.category == "Missile");
            var rocket = new GoF2Remake.Flight.Gun(db.Item(missile), Vector3.zero, true);
            var laser = new GoF2Remake.Flight.Gun(db.Item(0), Vector3.zero, false);
            Assert.AreEqual(100f, GoF2Remake.Flight.RocketTrail.For(rocket, null).size, "the game's own");
            Assert.IsNull(GoF2Remake.Flight.RocketTrail.For(rocket, new GoF2Remake.Flight.TrailLook { none = true }), "trail: false");
            var own = GoF2Remake.Flight.RocketTrail.For(rocket, new GoF2Remake.Flight.TrailLook { width = 220f, lifeMs = 5000f });
            Assert.AreEqual(220f, own.size);
            Assert.AreEqual(5000f, own.lifeMs);
            Assert.GreaterOrEqual(own.pool * own.sectionMs, 5000f, "enough sections for its life");
            Assert.IsNull(GoF2Remake.Flight.RocketTrail.For(laser), "a laser has none");
            Assert.IsNotNull(GoF2Remake.Flight.RocketTrail.For(laser, new GoF2Remake.Flight.TrailLook { width = 50f }), "but a mod gives it one");
        }

        [Test]
        public void AGameRoomHasItsMarkers()
        {
            var hangar = World.StationLevel.GameRoomMarkers("hangar_terran");
            Assert.IsNotNull(hangar);
            foreach (var k in new[] { "pad", "camera", "camera_target", "parked_1", "parked_2", "gate", "gate_out" }) Assert.IsTrue(hangar.ContainsKey(k), k);
            Assert.AreEqual(new Vector3(0f, 55f, -549f), hangar["gate"]);
            Assert.Greater(Vector3.Distance(hangar["camera"], hangar["pad"]), 20f, "the camera stands off the pad");
            var bar = World.StationLevel.GameRoomMarkers("bar_terran");
            Assert.AreEqual(7, bar.Keys.Count(k => k.StartsWith("visitor_")));
            Assert.IsTrue(bar.ContainsKey("camera") && bar.ContainsKey("camera_target"));
            Assert.IsNull(World.StationLevel.GameRoomMarkers("station_078_midorian"));
        }
    }
}
