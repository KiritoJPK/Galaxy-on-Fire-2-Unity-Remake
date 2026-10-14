// GameNamesTests.cs
// Feature: CLAUDE.md "Mods", Names (GameNames: every item / ship name and description goes through it; the original's text
// blocks 1274 / 1041 / 913 / 977 + index sit back to back, so item 233's "name" would be the first medal's; never
// Localization.Get(1274 + i) elsewhere) and "UI and platforms" (Localization: text by original id, "#id" when missing;
// Extra's English fallback; Layout::formatCredits).
// Expected values: CLAUDE.md "Data" (ship name = 913 + index, description 977 + index, item name 1274 + index, description
// 1041 + index; 233 original items, 64 ships), lounge_ui.md §7 (medal names from 1507 = 1274 + 233); the credit format
// "thousands separated, $ after, ',' in English, '.' otherwise".

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class GameNamesTests : SandboxTest
    {
        [Test]
        public void TextBlocks()
        {
            Assert.AreEqual(1274, GameNames.ItemNameBase);
            Assert.AreEqual(1041, GameNames.ItemDescriptionBase);
            Assert.AreEqual(913, GameNames.ShipNameBase);
            Assert.AreEqual(977, GameNames.ShipDescriptionBase);
            Assert.AreEqual(233, GameNames.OriginalItems);
            Assert.AreEqual(64, GameNames.OriginalShips);
            Assert.AreEqual(1507, GameNames.ItemNameBase + GameNames.OriginalItems, "the first medal's name follows the items");
            Assert.AreEqual(GameNames.ShipDescriptionBase, GameNames.ShipNameBase + GameNames.OriginalShips, "ship blocks back to back");
        }

        [Test]
        public void OriginalItemsReadTheirBlocks()
        {
            for (int i = 0; i < GameNames.OriginalItems; i++)
            {
                if (ModContent.ItemText(i, false, out _)) continue;   // renamed by an active mod
                Assert.AreEqual(Localization.Get(1274 + i), GameNames.Item(i), "item " + i);
                Assert.AreEqual(Localization.Get(1041 + i), GameNames.ItemDescription(i), "item " + i);
            }
        }

        [Test]
        public void OriginalShipsReadTheirBlocks()
        {
            for (int s = 0; s < GameNames.OriginalShips; s++)
            {
                if (ModContent.ShipText(s, false, out _)) continue;
                Assert.AreEqual(Localization.Get(913 + s), GameNames.Ship(s), "ship " + s);
                Assert.AreEqual(Localization.Get(977 + s), GameNames.ShipDescription(s), "ship " + s);
            }
        }

        [Test]
        public void NumbersPastTheTablesNeverReadTheNextBlock()
        {
            if (!ModContent.ItemText(233, false, out _))
            {
                Assert.AreEqual("#item233", GameNames.Item(233));
                Assert.AreNotEqual(Localization.Get(1507), GameNames.Item(233), "not the first medal's name");
                Assert.AreEqual("", GameNames.ItemDescription(233));
            }
            Assert.AreEqual("#item-1", GameNames.Item(-1));
            Assert.AreEqual("", GameNames.ItemDescription(-1));
            Assert.AreEqual("#ship-1", GameNames.Ship(-1));
            Assert.AreEqual("", GameNames.ShipDescription(-1));
            if (!ModContent.ShipText(9999, false, out _)) Assert.AreEqual("#ship9999", GameNames.Ship(9999));
        }

        [Test]
        public void SomeKnownNamesAreNotEmpty()
        {
            foreach (int i in new[] { 0, 85, 122, 179, 232 }) Assert.IsFalse(string.IsNullOrEmpty(GameNames.Item(i)), "item " + i);
            foreach (int s in new[] { 0, 10, 14, 63 }) Assert.IsFalse(string.IsNullOrEmpty(GameNames.Ship(s)), "ship " + s);
        }

        [TestCase(0, "0$")]
        [TestCase(999, "999$")]
        [TestCase(1000, "1#000$")]
        [TestCase(1234567, "1#234#567$")]
        [TestCase(-40000, "-40#000$")]
        public void CreditsFormat(int value, string pattern)
        {
            string sep = Localization.Language == "en" ? "," : ".";
            Assert.AreEqual(pattern.Replace("#", sep), GameNames.Credits(value));
        }

        [Test]
        public void LocalizationFallbacks()
        {
            Assert.IsTrue(Localization.IsLoaded || Localization.Count > 0, "a table loads on first use");
            Assert.That(Localization.Count, Is.GreaterThan(3300));
            Assert.AreEqual("#-1", Localization.Get(-1));
            Assert.AreEqual("#999999", Localization.Get(999999));
            Assert.AreEqual("English fallback", Localization.Extra("noSuchKeyForTests_8d1f", "English fallback"));
        }

        /// <summary>The convention: no code outside GameNames reads the item / ship name blocks itself.</summary>
        [Test]
        public void NoRawNameBlockReadsOutsideGameNames()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "Scripts");
            var raw = new Regex(@"Localization\.Get\(\s*(?:(?:1274|1041|913|977)\s*\+|[A-Za-z_][\w.\[\]]*\s*\+\s*(?:1274|1041|913|977)\b)");
            var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/Tests/") && Path.GetFileName(f) != "GameNames.cs")
                .Where(f => raw.IsMatch(File.ReadAllText(f)))
                .Select(Path.GetFileName).ToList();
            CollectionAssert.IsEmpty(offenders, "use GameNames.Item / ItemDescription / Ship / ShipDescription");
        }
    }
}
