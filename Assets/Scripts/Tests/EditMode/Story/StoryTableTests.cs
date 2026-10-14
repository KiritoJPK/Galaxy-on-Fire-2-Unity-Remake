// StoryTableTests.cs
// Feature: CLAUDE.md "Story" (Resources/GoF2Data/story.json via StoryTable: per index the mission, objective text, the
// briefing / success pages with speakers, radio lines; speakers' portraits; the alien font; the tutorial Info pages left
// out while the tutorials are off) and the Debug mission list's StepSummaries.
// Expected values: Reference/research/campaign_flow.md §4 (the step table: type, target, objective text, hidden steps,
// briefing / success text ranges), §4's counts (60 briefing pages, 1028 success pages; no dialogue at 162), §5 (speaker name
// = text 1597 + speaker, 16 Info, 19 / 56 the alien font), §8 (rewards 23 / 31 / 54 / 57 / 72), §2.2 (statusValues and
// goods per type); CLAUDE.md "Story" for the tutorial pages (1677 / 1680 / 1718 / 1725 / 1730 / 1746 / 1749 / 1750 / 1753 /
// 1816 / 1862 / 1878) and the Info pages with story content (191 / 192 ...).

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class StoryTableTests
    {
        /// <summary>campaign_flow.md §4: index, objective text (DAT_00258f68).</summary>
        static readonly int[,] Objectives =
        {
            {0,749}, {9,749}, {10,652}, {11,653}, {12,654}, {13,655}, {14,656}, {16,657}, {18,658}, {20,659}, {21,659},
            {23,660}, {24,661}, {28,662}, {32,663}, {33,664}, {34,665}, {35,666}, {36,667}, {38,668}, {40,669}, {44,670},
            {45,670}, {46,671}, {48,672}, {52,672}, {54,672}, {55,673}, {56,674}, {58,675}, {59,676}, {61,677}, {62,678},
            {63,679}, {64,680}, {66,681}, {67,682}, {68,683}, {69,684}, {70,684}, {71,685}, {72,686}, {73,687}, {74,688},
            {77,689}, {78,690}, {79,690}, {80,691}, {81,691}, {84,692}, {85,692}, {86,693}, {87,694}, {90,694}, {91,695},
            {92,696}, {93,697}, {94,698}, {95,698}, {96,699}, {97,700}, {98,701}, {100,702}, {102,703}, {103,704},
            {104,705}, {105,706}, {106,707}, {107,708}, {108,709}, {109,710}, {111,711}, {112,712}, {113,713}, {114,714},
            {115,715}, {116,716}, {117,717}, {118,718}, {120,719}, {122,720}, {123,721}, {125,722}, {127,723}, {128,724},
            {130,725}, {131,726}, {134,727}, {135,728}, {136,729}, {137,730}, {138,731}, {139,732}, {140,733}, {141,734},
            {142,735}, {143,736}, {145,737}, {146,738}, {147,739}, {148,740}, {151,740}, {152,741}, {153,742}, {154,743},
            {156,744}, {157,745}, {158,746}, {159,747}, {160,748}, {162,748},
        };

        [Test]
        public void OneStepPerIndex()
        {
            Assert.AreEqual(163, StoryTable.Steps.Count, "0..162");
            for (int i = 0; i < StoryTable.Steps.Count; i++) Assert.AreEqual(i, StoryTable.Step(i).index);
            Assert.IsNull(StoryTable.Step(-1));
            Assert.IsNull(StoryTable.Step(163));
        }

        [Test]
        public void ObjectiveTextsMatchTheBinarysTable()
        {
            for (int r = 0; r < Objectives.GetLength(0); r++)
                Assert.AreEqual(Objectives[r, 1], StoryTable.Step(Objectives[r, 0]).objectiveText, "step " + Objectives[r, 0]);
        }

        [Test]
        public void HiddenSteps()
        {
            var hidden = StoryTable.Steps.Where(s => !s.visible && s.index != 53 && s.index != 129).Select(s => s.index).ToList();
            CollectionAssert.AreEquivalent(new[] { 13, 37, 45, 52, 84, 128, 130, 162 }, hidden);
        }

        [Test]
        public void RewardsOnlyOnTheResearchedSteps()
        {
            var rewards = StoryTable.Steps.Where(s => s.reward != 0).ToDictionary(s => s.index, s => s.reward);
            CollectionAssert.AreEquivalent(new Dictionary<int, int> { { 23, 20000 }, { 31, 30000 }, { 54, 200000 }, { 57, 150000 }, { 72, 150000 } }, rewards);
        }

        [TestCase(2, 10)]
        [TestCase(4, 25)]
        [TestCase(20, 6)]
        [TestCase(91, 10)]
        [TestCase(92, 10)]
        [TestCase(94, 83)]
        [TestCase(102, 1700)]
        [TestCase(139, 10)]
        public void StatusValues(int index, int value)
        {
            Assert.AreEqual(value, StoryTable.Step(index).value);
        }

        [TestCase(33, 164, 50)]
        [TestCase(58, 179, 10)]
        [TestCase(68, 175, 1)]
        [TestCase(104, 206, 1)]
        [TestCase(112, 146, 1)]
        [TestCase(118, 209, 1)]
        [TestCase(121, 209, 1)]
        [TestCase(135, 155, 140)]
        [TestCase(143, 210, 1)]
        public void GoodsToDeliver(int index, int item, int amount)
        {
            Assert.AreEqual(item, StoryTable.Step(index).goodsItem);
            Assert.AreEqual(amount, StoryTable.Step(index).goodsAmount);
        }

        [Test]
        public void DialoguePageTotals()
        {
            Assert.AreEqual(60, StoryTable.Steps.Sum(s => s.briefing.Count), "briefing pages");
            Assert.AreEqual(1028, StoryTable.Steps.Sum(s => s.success.Count), "success pages");
            Assert.AreEqual(0, StoryTable.Step(162).briefing.Count + StoryTable.Step(162).success.Count, "no dialogue at 162");
        }

        [Test]
        public void BriefingsOnTheResearchedSteps()
        {
            var withBriefing = StoryTable.Steps.Where(s => s.briefing.Count > 0).Select(s => s.index).ToList();
            CollectionAssert.AreEquivalent(new[] { 0, 2, 4, 7, 11, 14, 16, 21, 23, 24, 26, 29, 32, 36, 38, 41, 48, 65, 67, 135 }, withBriefing);
        }

        [TestCase(0, 1677, 1680)]
        [TestCase(2, 1721, 1725)]
        [TestCase(7, 1748, 1753)]
        [TestCase(24, 1913, 1916)]
        [TestCase(48, 2105, 2111)]
        [TestCase(65, 2240, 2250)]
        [TestCase(67, 2283, 2286)]
        [TestCase(135, 2879, 2882)]
        public void BriefingTextRange(int index, int first, int last)
        {
            var p = StoryTable.Step(index).briefing;
            Assert.AreEqual(first, p[0].text);
            Assert.AreEqual(last, p[p.Count - 1].text);
        }

        [TestCase(1, 1700, 1718)]
        [TestCase(23, 1898, 192)]
        [TestCase(43, 2069, 2070)]
        [TestCase(44, 2075, 2078)]
        [TestCase(83, 2444, 2452)]
        [TestCase(116, 2720, 2737)]
        [TestCase(159, 3110, 3120)]
        [TestCase(161, 3126, 3131)]
        public void SuccessTextRange(int index, int first, int last)
        {
            var p = StoryTable.Step(index).success;
            Assert.AreEqual(first, p[0].text);
            Assert.AreEqual(last, p[p.Count - 1].text);
        }

        [TestCase(0)]
        [TestCase(14)]
        [TestCase(28)]
        [TestCase(49)]
        [TestCase(78)]
        [TestCase(89)]
        public void LevelStepsWithoutASuccessConversation(int index)
        {
            Assert.AreEqual(0, StoryTable.Step(index).success.Count);
        }

        [Test]
        public void EveryPageHasAKnownSpeaker()
        {
            foreach (var s in StoryTable.Steps)
            {
                foreach (var p in s.briefing.Concat(s.success))
                {
                    Assert.That(p.speaker, Is.InRange(0, 99), "step " + s.index);
                    Assert.That(p.text, Is.GreaterThan(0), "step " + s.index);
                    Assert.IsNotNull(p.voice);
                }
                foreach (var r in s.radio)
                {
                    Assert.That(r.speaker, Is.GreaterThanOrEqualTo(0), "radio of step " + s.index);
                    Assert.That(r.text, Is.GreaterThan(0));
                    Assert.That(r.count, Is.GreaterThanOrEqualTo(1));
                }
            }
        }

        [Test]
        public void SpeakerNamesAreTheTextBlockFrom1597()
        {
            for (int s = 0; s < StoryTable.SpeakerCount; s++)
                Assert.AreEqual(Localization.Get(1597 + s), StoryTable.OriginalSpeakerName(s));
            Assert.AreEqual(Localization.Get(1597), StoryTable.OriginalSpeakerName(0), "0 Keith T. Maxwell");
        }

        [Test]
        public void PortraitDescriptors()
        {
            Assert.That(StoryTable.SpeakerCount, Is.GreaterThanOrEqualTo(60));
            for (int s = 0; s < StoryTable.SpeakerCount; s++) Assert.AreEqual(5, StoryTable.Portrait(s).Length, "speaker " + s);
            Assert.IsNull(StoryTable.Portrait(-1));
            Assert.IsNull(StoryTable.Portrait(StoryTable.SpeakerCount), "no descriptor past the table (random faces)");
            Assert.AreEqual(0, StoryTable.PortraitLayers(-1).Count);
            Assert.AreEqual(5, StoryTable.Portrait(0).Length);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, StoryTable.Portrait(0), "Keith's descriptor is zeroed (lounge_ui.md)");
        }

        [Test]
        public void AlienFontAndNarration()
        {
            for (int s = 0; s < 70; s++)
            {
                Assert.AreEqual(s == 19 || s == 56, StoryTable.UsesAlienFont(s), "speaker " + s);
                Assert.AreEqual(s == 16, StoryTable.IsNarration(s), "speaker " + s);
            }
        }

        // ---- the tutorial pages (Settings.TutorialHints) -------------------------------------------------------

        static readonly int[] TutorialPages = { 1677, 1680, 1718, 1725, 1730, 1746, 1749, 1750, 1753, 1816, 1862, 1878 };

        [Test]
        public void TutorialPagesLeftOutWhileTheTutorialsAreOff()
        {
            using (new SettingsScope<bool>(() => Settings.TutorialHints, v => Settings.TutorialHints = v))
            {
                Settings.TutorialHints = false;
                Assert.AreEqual(0, StoryTable.Shown(StoryTable.Step(0).briefing).Count, "index 0's whole briefing");
                foreach (var s in StoryTable.Steps)
                    foreach (var p in StoryTable.Shown(s.success))
                        Assert.IsFalse(p.speaker == 16 && TutorialPages.Contains(p.text), "step " + s.index + " page " + p.text);
                var shown23 = StoryTable.Shown(StoryTable.Step(23).success);
                Assert.IsTrue(shown23.Exists(p => p.text == 191) && shown23.Exists(p => p.text == 192), "story Info pages stay");
                var step7 = StoryTable.Step(7).briefing;
                Assert.Less(StoryTable.Shown(step7).Count, step7.Count);
                Assert.IsNull(StoryTable.Shown(null));
            }
        }

        [Test]
        public void EveryPageShownWithTheTutorialsOn()
        {
            using (new SettingsScope<bool>(() => Settings.TutorialHints, v => Settings.TutorialHints = v))
            {
                Settings.TutorialHints = true;
                var pages = StoryTable.Step(0).briefing;
                Assert.AreSame(pages, StoryTable.Shown(pages));
            }
        }

        [Test]
        public void TheTutorialPagesAreInfoPagesOfTheTable()
        {
            var info = new HashSet<int>();
            foreach (var s in StoryTable.Steps)
                foreach (var p in s.briefing.Concat(s.success))
                    if (p.speaker == 16) info.Add(p.text);
            foreach (int t in TutorialPages) Assert.IsTrue(info.Contains(t), "Info page " + t);
        }

        // ---- the Debug mission list's texts ------------------------------------------------------------------

        [Test]
        public void StepSummariesTitleEveryReachableStep()
        {
            for (int i = 0; i <= Story.LastIndex; i++)
            {
                var e = StepSummaries.Get(i);
                Assert.IsNotNull(e, "step " + i);
                Assert.AreEqual(i, e.index);
                if (i != 53 && i != 129) Assert.IsFalse(string.IsNullOrWhiteSpace(e.title), "title of step " + i);
            }
            Assert.IsNull(StepSummaries.Get(163));
            Assert.IsNull(StepSummaries.Get(-1));
        }
    }
}
