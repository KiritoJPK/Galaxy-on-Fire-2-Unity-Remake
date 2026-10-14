// StoryTableTests.cs
// CLAUDE.md "Story": speaker names are text 1597 + speaker (DialogueWindow::init), the names CLAUDE.md uses: Keith T.
// Maxwell (0), Brent Snocom (1), the Pirate Boss (9), Info (16, the tutorial pages: "not a character talking"), Story
// (17, the narrator, types in), the Void (19), Moonsprocket (38), Corny (56); a mod character that "replaces" a speaker
// lends its name (none in a test sandbox). With the tutorial popups off the Info pages that only teach the controls
// (1677 / 1680, 1718, 1725, 1730, 1746, 1749, 1750, 1753, 1816, 1862, 1878) are left out of a conversation, the story Info
// pages (1782, 1896, 191 / 192, 2171, 2184) always show (StoryTable.Shown).

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class StoryTableSpeakerTests : SandboxTest
    {
        DataUiTestKit.TextLanguage english;
        DataUiTestKit.PrefsSnapshot prefs;

        [SetUp]
        public void English()
        {
            prefs = new DataUiTestKit.PrefsSnapshot();
            english = new DataUiTestKit.TextLanguage("en");
        }

        [TearDown]
        public void Back()
        {
            english?.Dispose();
            prefs?.Dispose();
        }

        [TestCase(0, "Keith T. Maxwell")]
        [TestCase(1, "Brent Snocom")]
        [TestCase(9, "Pirate Boss")]
        [TestCase(16, "Info")]
        [TestCase(17, "Story")]
        [TestCase(19, "Void")]
        [TestCase(38, "Moonsprocket")]
        [TestCase(56, "Corny")]
        public void SpeakerNames(int speaker, string name)
        {
            Assert.AreEqual(name, StoryTable.SpeakerName(speaker));
            Assert.AreEqual(name, StoryTable.OriginalSpeakerName(speaker), "no mod replaces it");
            Assert.AreEqual(Localization.Get(1597 + speaker), StoryTable.SpeakerName(speaker));
        }

        [Test]
        public void SpeakerNamesFollowTheLanguage()
        {
            using (new DataUiTestKit.TextLanguage("de"))
                Assert.AreEqual(Localization.Get(1597 + 9), StoryTable.SpeakerName(9));
        }

        [Test]
        public void OnlyInfoIsNarration()
        {
            for (int s = 0; s < 64; s++) Assert.AreEqual(s == 16, StoryTable.IsNarration(s), "speaker " + s);
        }

        static List<DialoguePage> Pages(params (int speaker, int text)[] p) =>
            p.Select(x => new DialoguePage { speaker = x.speaker, text = x.text }).ToList();

        [Test]
        public void TutorialPagesHideWithTheTutorialsOff()
        {
            var pages = Pages((16, 1677), (0, 1678), (16, 1782), (16, 1878), (6, 1879));
            Settings.TutorialHints = false;
            var shown = StoryTable.Shown(pages);
            CollectionAssert.AreEqual(new[] { 1678, 1782, 1879 }, shown.Select(p => p.text).ToArray(), "the controls pages go, the story Info page stays");
            Settings.TutorialHints = true;
            Assert.AreSame(pages, StoryTable.Shown(pages), "tutorials on: every page");
        }

        [Test]
        public void OnlyInfoPagesAreTutorials()
        {
            // the same text from another speaker isn't a tutorial page
            Settings.TutorialHints = false;
            var pages = Pages((0, 1677), (17, 1725));
            Assert.AreSame(pages, StoryTable.Shown(pages));
            Assert.IsNull(StoryTable.Shown(null));
        }

        [Test]
        public void EveryListedTutorialPageIsAnInfoPageOfTheStory()
        {
            // The listed ids must be pages the story really shows as "Info" (else the list hides nothing).
            int[] tutorial = { 1677, 1680, 1718, 1725, 1730, 1746, 1749, 1750, 1753, 1816, 1862, 1878 };
            var info = new HashSet<int>(StoryTable.Steps.SelectMany(s => s.briefing.Concat(s.success)).Where(p => p.speaker == 16).Select(p => p.text));
            foreach (int t in tutorial) Assert.IsTrue(info.Contains(t), "text " + t);
            Settings.TutorialHints = false;
            foreach (int t in new[] { 1782, 1896, 191, 192, 2171, 2184 })
                Assert.AreEqual(1, StoryTable.Shown(Pages((16, t))).Count, "story Info page " + t + " always shows");
        }
    }
}
