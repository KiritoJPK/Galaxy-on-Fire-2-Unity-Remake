// StoryLevelTests.cs
// Every story step can be started the way the Debug panel's mission select does it (Story.StartAtMission: the campaign's
// start, every step before it replayed) and its scene builds and runs a few seconds without an error: the story orbits'
// levels (MainCampaignLevels, ValkyrieLevels, SupernovaLevels), the cutscenes and the docked steps (CLAUDE.md "Story",
// "Supernova add-on"). "Smoke" is a spread over the three campaigns; "Slow" is every step (about 15 minutes).

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoF2Remake.Tests
{
    public class StoryLevelTests : PlayModeTest
    {
        static readonly int[] Spread =
        {
            0, 1, 2, 5, 7, 14, 16, 21, 24, 25, 29, 36, 38, 40, 41, 44,           // main game
            45, 48, 49, 56, 64, 69, 73, 78, 79, 80, 81, 84,                    // Valkyrie
            89, 91, 94, 102, 105, 114, 125, 135, 139, 144, 145, 154, 157, 158, 162,   // Supernova
        };

        static IEnumerable<int> SmokeSteps() => Spread;

        /// <summary>Every step the mission select offers (the ones only reached inside another map to it).</summary>
        static IEnumerable<int> AllSteps() => Enumerable.Range(0, Story.LastIndex + 1).Where(i => Story.MissionSelectStart(i) == i);

        [UnityTest, Category("Smoke")]
        public IEnumerator KeyStoryStepsRun([ValueSource(nameof(SmokeSteps))] int step) => RunStep(step);

        [UnityTest, Category("Slow")]
        public IEnumerator EveryStoryStepRuns([ValueSource(nameof(AllSteps))] int step) => RunStep(step);

        IEnumerator RunStep(int step)
        {
            Session.ResetNewGame();
            string scene = Story.StartAtMission(Database.Load(), step);
            Assert.IsFalse(string.IsNullOrEmpty(scene), "a scene to load");
            yield return Load(scene);
            if (scene == "Space")
                yield return WaitFor(() => Find<SpaceLevel>() != null && Find<SpaceLevel>().Player != null, 60f, "the story orbit");
            else
                yield return WaitFor(() => Find<StationLevel>() != null, 60f, "the station");
            yield return Run(4f);
            Assert.GreaterOrEqual(Story.Index, Story.MissionSelectStart(step), "the story didn't fall back");
        }
    }
}
