// EventScriptTests.cs
// Feature: CLAUDE.md "Multiplayer" > Events: the line form the graphs compile to (EventRunner's own parser, Check): any
// server command without the "/", wait / wait until [timeout], if / else / end, while / end, repeat <n> [as <var>] / end,
// set, param, parallel / every / end, on <trigger> / end (died, docked, launched, joined, left, entered, kill, cleared,
// pvpkill, respawned, near), ask / vote with answer / choice blocks (a question and 2 to 4 answers; choice 0 = no votes),
// startevent, points, scoreboard on / off, score / winner (kills, time, points), stop, mission <key> (title, offer, client,
// reward, players, stations, kind, starts), complete / fail, checkpoint (top level only, unique), objective, target,
// questorbit, startquest; "#" comments and "#@" node markers; an event can't start another with "event".
// Expected values: the documented line form (CLAUDE.md, EventRunner's comments); valid and invalid scripts written by hand.

using System.Linq;
using System.Text;
using GoF2Remake.Events;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class EventScriptTests : SandboxTest
    {
        static void Valid(string script) => Assert.IsNull(EventRunner.Check(script), script);
        static void Invalid(string script) => Assert.IsNotNull(EventRunner.Check(script), script);

        [Test]
        public void AnEmptyScriptIsValid()
        {
            Valid("");
            Valid("# only a comment\n\n   \n");
        }

        [Test]
        public void WaitsNeedTheirArguments()
        {
            Valid("wait 5");
            Valid("wait {2 * 3}");
            Valid("wait until enemies == 0");
            Valid("wait until enemies == 0 timeout 60");
            Invalid("wait");
        }

        [Test]
        public void IfElseEndBlocks()
        {
            Valid("if players > 0\nstop\nend");
            Valid("if 1\nwait 1\nelse\nwait 2\nend");
            Valid("if 1\nif 0\nstop\nend\nend");
            Invalid("if\nend");
            Invalid("if 1\nstop");
            Invalid("else");
            Invalid("end");
            Invalid("while 1\nelse\nend");
        }

        [Test]
        public void TheErrorNamesTheLine()
        {
            string error = EventRunner.Check("wait 1\nwait 2\nelse");
            Assert.IsNotNull(error);
            StringAssert.Contains("3", error);
        }

        [Test]
        public void LoopsAndThreads()
        {
            Valid("while time < 10\nwait 1\nend");
            Valid("repeat 3\nwait 1\nend");
            Valid("repeat 3 as wave\nwait {wave}\nend");
            Valid("parallel\nwait 1\nend");
            Valid("every 2\nheal @a\nend");
            Invalid("repeat\nend");
            Invalid("repeat 3 as 1wave\nend");
            Invalid("every\nend");
            Invalid("while\nend");
        }

        [Test]
        public void SetAndParam()
        {
            Valid("set x = 1");
            Valid("set x 1");
            Valid("set _count = x + 1");
            Valid("param rounds = 3");
            Invalid("set 1x = 2");
            Invalid("set x =");
            Invalid("set x");
            Invalid("param rounds");
            Invalid("param = 3");
        }

        [Test]
        public void EveryTriggerWordIsAccepted()
        {
            foreach (var t in new[] { "died", "docked", "launched", "joined", "left", "entered", "kill", "cleared", "pvpkill", "respawned", "hacked" })
                Valid("on " + t + "\nend");
            Valid("on docked 78\nend");
            Valid("on near 78 0 0 20000 500\nend");
            Invalid("on exploded\nend");
            Invalid("on\nend");
            Invalid("on died");
        }

        [Test]
        public void AskAndVoteBlocks()
        {
            Valid("ask @a 20 | Ready? | Yes | No\nanswer 1\nstop\nend\nanswer 2\nwait 1\nend\nend");
            Valid("ask @a[orbit=Var Hastra] 20 | Ready? | Yes | No\nend");
            Valid("ask @a 20 | Q | A | B | C | D\nanswer 4\nend\nend");
            Valid("vote @a 30 | Next? | Waves | Survival\nchoice 1\nwait 1\nend\nchoice 0\nstop\nend\nend");
            Invalid("ask @a 20 | Ready? | Yes\nend");
            Invalid("ask @a 20 | Q | A | B | C | D | E\nend");
            Invalid("ask @a | Q | A | B\nend");
            Invalid("ask @a 20 | Q | A | B\nanswer 3\nend\nend");
            Invalid("ask @a 20 | Q | A | B\nanswer 0\nend\nend");
            Invalid("ask @a 20 | Q | A | B\nchoice 1\nend\nend");
            Invalid("vote @a 20 | Q | A | B\nanswer 1\nend\nend");
            Invalid("answer 1\nend");
        }

        [Test]
        public void ScoresAndWinners()
        {
            Valid("score kills\nscore time\nscore points");
            Valid("winner\nwinner kills\nwinner time\nwinner points");
            Invalid("score");
            Invalid("score gold");
            Invalid("winner gold");
        }

        [Test]
        public void PointsAndScoreboards()
        {
            Valid("points @a 5");
            Valid("points @trigger {kills * 10}");
            Valid("points @alive[orbit=Var Hastra] 5");
            Valid("scoreboard on Top pilots");
            Valid("scoreboard off");
            Invalid("points @a");
            Invalid("points");
            Invalid("scoreboard maybe");
        }

        [Test]
        public void MissionLines()
        {
            foreach (var key in new[] { "title X", "offer Bring it\\nback", "client vossk K'ekki", "reward 5000", "players 1 4", "stations 78, Dis", "kind quest", "starts visited(78)" })
                Valid("mission " + key);
            Invalid("mission colour red");
            Valid("complete");
            Valid("complete 5000 | Well done");
            Valid("fail");
            Valid("fail The convoy was lost");
        }

        [Test]
        public void CheckpointsAreTopLevelAndUnique()
        {
            Valid("checkpoint start\nwait 1\ncheckpoint middle");
            Valid("checkpoint");
            Invalid("checkpoint a\ncheckpoint a");
            Invalid("if 1\ncheckpoint a\nend");
            Invalid("parallel\ncheckpoint a\nend");
            Invalid("on died\ncheckpoint a\nend");
        }

        [Test]
        public void QuestLines()
        {
            Valid("objective Find the trader\\nat Var Hastra");
            Valid("target 78");
            Valid("target off");
            Valid("questorbit 78");
            Valid("questorbit 78 off");
            Valid("startquest second_part");
            Invalid("target");
            Invalid("questorbit");
            Invalid("startquest");
            Invalid("startquest two words");
        }

        [Test]
        public void StartEventButNoNestedEvent()
        {
            Valid("startevent waves");
            Valid("startevent waves rounds=3");
            Invalid("startevent");
            Invalid("event waves");
            Invalid("/event waves");
        }

        [Test]
        public void CommandsAreLinesOfTheirOwn()
        {
            // Any other line is a server command (run as the server, "/" optional).
            Valid("heal @a\n/ammo @a\nspawn @a pirate 3 enemy\ntitle @a Wave 1 | Get ready for 4");
        }

        [Test]
        public void NodeMarkersAndCommentsAreNotPartOfTheLine()
        {
            Valid("wait 5  #@node-1\n  if 1  #@node-2\n    stop # a note\n  end  #@node-2");
        }

        [Test]
        public void ATooLongScriptIsRefused()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 4001; i++) sb.Append("wait 1\n");
            Invalid(sb.ToString());
        }

        [Test]
        public void SettingsAreTheParamLines()
        {
            var s = EventRunner.Settings("param Rounds = 3\nset x = 1\nparam boss_hp = 100 * 2\nwait 1");
            CollectionAssert.AreEqual(new[] { "rounds", "boss_hp" }, s.Select(x => x.name).ToArray(), "names in lower case, in order");
            CollectionAssert.AreEqual(new[] { "3", "100 * 2" }, s.Select(x => x.value).ToArray());
            Assert.IsEmpty(EventRunner.Settings("wait 1"));
            Assert.IsEmpty(EventRunner.Settings("if 1"), "a script that doesn't compile has none");
        }

        // ---- /event (single player runs the events itself: EventHost.Local) ----

        [Test]
        public void EventListShowsTheBuiltIns()
        {
            string answer = EventRunner.Command("list", null);
            foreach (var n in new[] { "waves", "survival", "pirate_hideout" }) StringAssert.Contains(n, answer);
            Assert.AreEqual(answer, EventRunner.Command("", null), "no argument lists them too");
        }

        [Test]
        public void EventStopWithNothingRunning()
        {
            Assert.IsFalse(EventRunner.Running);
            Assert.AreEqual(GoF2Remake.Data.Localization.Extra("mpEventNotRunning", "No event is running."), EventRunner.Command("stop", null));
        }

        [Test]
        public void AnUnknownEventIsntStarted()
        {
            Assert.IsNotEmpty(EventRunner.Command("nosuchevent", null));
            Assert.IsNotEmpty(EventRunner.Command("../waves", null), "no paths");
            Assert.IsFalse(EventRunner.Running);
        }

        [Test]
        public void SettingsAreNameEqualsNumber()
        {
            Assert.IsNotEmpty(EventRunner.Command("waves rounds", null));
            Assert.IsNotEmpty(EventRunner.Command("waves rounds=many", null));
            Assert.IsNotEmpty(EventRunner.Command("waves nosuchsetting=1", null), "a setting the event doesn't have");
            Assert.IsFalse(EventRunner.Running, "none of them started it");
        }

        [Test]
        public void TheExtensionsAndBuiltInsFolder()
        {
            Assert.AreEqual("gof2event", EventRunner.GraphExtension);
            Assert.AreEqual("gof2netevent", EventRunner.LegacyExtension);
        }
    }
}
