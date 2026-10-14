// VersionAndUpdateTests.cs
// CLAUDE.md "UI and platforms", Version / Update check: a build's version is the date and time of its commit,
// yyyy.MM.dd.HHmm (UTC), "editor" in the Editor (also its fingerprint); release tags are yyyy.MM.dd and only a release
// from a later day than the build counts as an update (the build's .HHmm is ignored); "editor", "0.1.0" and the like are no
// date; a tag may start with "v" or carry a suffix. Expected values by hand.

using System;
using System.Globalization;
using GoF2Remake.UI;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class VersionAndUpdateTests
    {
        [Test]
        public void TheEditorIsEditor()
        {
            Assert.AreEqual("editor", BuildVersion.Text);
            Assert.AreEqual("editor", BuildVersion.Fingerprint);
        }

        [Test]
        public void TheVersionFormatIsTheCommitTime()
        {
            Assert.AreEqual("yyyy.MM.dd.HHmm", BuildVersion.Format);
            string v = new DateTime(2026, 10, 4, 9, 5, 0, DateTimeKind.Utc).ToString(BuildVersion.Format, CultureInfo.InvariantCulture);
            Assert.AreEqual("2026.10.04.0905", v);
            Assert.IsTrue(UpdateCheck.TryParseDate(v, out int d));
            Assert.AreEqual(20261004, d, "a build's version reads as its day");
        }

        [TestCase("2026.10.14", 20261014)]
        [TestCase("2026.10.14.2200", 20261014)]
        [TestCase("v2026.10.14", 20261014)]
        [TestCase(" 2026.1.5", 20260105)]
        [TestCase("2026.10.14-hotfix", 20261014)]
        [TestCase("2026.12.31", 20261231)]
        public void DatesParse(string version, int expected)
        {
            Assert.IsTrue(UpdateCheck.TryParseDate(version, out int d), version);
            Assert.AreEqual(expected, d);
        }

        [TestCase("editor")]
        [TestCase("0.1.0")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("2026.13.01")]
        [TestCase("2026.00.10")]
        [TestCase("2026.10.32")]
        [TestCase("2026.10.0")]
        [TestCase("26.10.14")]
        [TestCase("2026.10.145")]
        public void NonDatesDontParse(string version)
        {
            Assert.IsFalse(UpdateCheck.TryParseDate(version, out int d), version ?? "null");
            Assert.AreEqual(0, d);
        }

        [TestCase("2026.10.15", "2026.10.14.2359", true)]
        [TestCase("2026.10.14", "2026.10.14.0001", false)]
        [TestCase("2026.10.14", "2026.10.14.2359", false)]
        [TestCase("2026.10.13", "2026.10.14.0000", false)]
        [TestCase("2027.01.01", "2026.12.31.2359", true)]
        [TestCase("v2026.11.01", "2026.10.31.1200", true)]
        [TestCase("2026.10.15", "editor", false)]
        [TestCase("latest", "2026.10.14.0000", false)]
        public void OnlyALaterDayIsAnUpdate(string tag, string build, bool newer)
        {
            Assert.AreEqual(newer, UpdateCheck.IsNewer(tag, build));
        }

        [Test]
        public void TheReleasePageIsTheRepositorys()
        {
            Assert.AreEqual("JoppieToppie/Galaxy-on-Fire-2-Unity-Remake", UpdateCheck.Repository);
            Assert.AreEqual("https://github.com/JoppieToppie/Galaxy-on-Fire-2-Unity-Remake/releases/latest", UpdateCheck.ReleasesPage);
        }
    }
}
