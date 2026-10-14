// NewGamePlusTests.cs
// Feature: CLAUDE.md "UI and platforms", New Game+ (remake, GitHub #4): a finished game (main game past 44, Valkyrie past 83,
// Supernova at 162; the newest one) carries over the credits, the unlocked blueprints and the medals (the better grade of
// each), the Kaamo Club owned with the old storage plus the old ship (its mods), its equipment and cargo parked in it (not the
// story's unsaleable items).
// Expected values: CLAUDE.md (the remake defines the feature); medal grades per lounge_ui.md §6.3 (1 gold is the best,
// 3 bronze the least); Shop.ShipRace for the parked hull's race (DAT_00254990).

using System;
using System.Collections.Generic;
using System.IO;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class NewGamePlusTests : StoryAreaTestBase
    {
        static SaveData Save(Campaign c, int index, bool freePlay = false) =>
            new SaveData { campaign = (int)c, campaignMission = index, freePlay = freePlay, ship = 10 };

        [TestCase(Campaign.GalaxyOnFire2, 44, false)]
        [TestCase(Campaign.GalaxyOnFire2, 45, true)]
        [TestCase(Campaign.Valkyrie, 83, false)]
        [TestCase(Campaign.Valkyrie, 84, true)]
        [TestCase(Campaign.Supernova, 161, false)]
        [TestCase(Campaign.Supernova, 162, true)]
        public void FinishedCampaigns(Campaign c, int index, bool finished)
        {
            Assert.AreEqual(finished, NewGamePlus.Finished(Save(c, index)));
        }

        [Test]
        public void FreePlayAndNothingAreNeverFinished()
        {
            Assert.IsFalse(NewGamePlus.Finished(Save(Campaign.GalaxyOnFire2, 162, freePlay: true)));
            Assert.IsFalse(NewGamePlus.Finished(null));
        }

        static void Write(int slot, SaveData s, DateTime when)
        {
            Directory.CreateDirectory(SaveGame.Folder);
            File.WriteAllText(SaveGame.SlotPath(slot), UnityEngine.JsonUtility.ToJson(s));
            File.SetLastWriteTimeUtc(SaveGame.SlotPath(slot), when);
        }

        [Test]
        public void FindFinishedTakesTheNewestFinishedSave()
        {
            Assert.IsNull(NewGamePlus.FindFinished(out int none));
            Assert.AreEqual(-1, none);
            var t = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            Write(1, Save(Campaign.GalaxyOnFire2, 30), t.AddHours(5));            // newest, not finished
            Write(2, Save(Campaign.Valkyrie, 84), t.AddHours(1));
            Write(3, Save(Campaign.GalaxyOnFire2, 50), t.AddHours(3));
            Write(4, Save(Campaign.Supernova, 120), t.AddHours(4));               // not finished
            var found = NewGamePlus.FindFinished(out int slot);
            Assert.AreEqual(3, slot);
            Assert.AreEqual(50, found.campaignMission);
        }

        static SaveData Finished() => new SaveData
        {
            campaign = (int)Campaign.GalaxyOnFire2, campaignMission = 45, credits = 1234567, ship = 10,
            shipMods = new List<int> { 0, 1 },
            unlockedBlueprints = new List<int> { 85, 179 },
            medals = new int[45],
            unsaleable = new List<int> { 90 },
            equipment = new List<ItemStack> { new ItemStack(2, 1), new ItemStack(90, 1), new ItemStack(36, 6) },
            cargo = new List<ItemStack> { new ItemStack(122, 5), new ItemStack(36, 4), new ItemStack(164, 0) },
            kaamoItems = new List<ItemStack> { new ItemStack(122, 3) },
            kaamoShips = new List<StoredShip> { new StoredShip(5, 0, null), new StoredShip(5, 0, null), new StoredShip(9, 1, new List<int> { 2 }) },
        };

        [Test]
        public void CreditsAndBlueprintsCarryOver()
        {
            Story.StartCampaign(Db, Campaign.GalaxyOnFire2);
            Session.Credits = 100;
            Session.UnlockedBlueprints.Add(206);
            NewGamePlus.Apply(Finished());
            Assert.AreEqual(1234667, Session.Credits);
            CollectionAssert.IsSupersetOf(Session.UnlockedBlueprints, new[] { 85, 179, 206 });
        }

        [Test]
        public void NegativeCreditsAreNotCarried()
        {
            var old = Finished();
            old.credits = -500;
            Session.Credits = 100;
            NewGamePlus.Apply(old);
            Assert.AreEqual(100, Session.Credits);
        }

        [Test]
        public void TheKaamoClubIsOwnedWithTheOldStorageAndShip()
        {
            NewGamePlus.Apply(Finished());
            Assert.AreEqual(3, Session.KaamoState);
            int Amount(int item) { int n = 0; foreach (var s in Session.KaamoItems) if (s.item == item) n += s.amount; return n; }
            Assert.AreEqual(8, Amount(122), "storage 3 + hold 5, merged");
            Assert.AreEqual(10, Amount(36), "mounted 6 + hold 4");
            Assert.AreEqual(1, Amount(2));
            Assert.AreEqual(0, Amount(90), "the story's unsaleable items stay behind");
            Assert.AreEqual(0, Amount(164), "nothing of an empty stack");
            Assert.AreEqual(Session.KaamoItems.Count, new HashSet<int>(Session.KaamoItems.ConvertAll(s => s.item)).Count, "one row per item");

            var ships = Session.KaamoShips.ConvertAll(s => s.ship);
            CollectionAssert.AreEquivalent(new[] { 5, 9, 10 }, ships, "the stored hulls once each and the old ship");
            var own = Session.KaamoShips.Find(s => s.ship == 10);
            CollectionAssert.AreEqual(new[] { 0, 1 }, own.mods, "with its mods");
            Assert.AreEqual(Shop.ShipRace[10], own.race);
            CollectionAssert.AreEqual(new[] { 2 }, Session.KaamoShips.Find(s => s.ship == 9).mods);
        }

        [Test]
        public void TheOldShipIsntStoredTwice()
        {
            var old = Finished();
            old.kaamoShips.Add(new StoredShip(10, 0, null));
            NewGamePlus.Apply(old);
            Assert.AreEqual(1, Session.KaamoShips.FindAll(s => s.ship == 10).Count);
        }

        [Test]
        public void MedalsCarryOverWhereTheNewGameHasNone()
        {
            var old = Finished();
            old.medals[4] = 2;
            old.medals[16] = 1;
            NewGamePlus.Apply(old);
            Assert.AreEqual(2, Session.Medals[4]);
            Assert.AreEqual(1, Session.Medals[16]);
            Assert.AreEqual(0, Session.Medals[5]);
        }

        [Test]
        public void MedalsKeepTheBetterGrade()
        {
            // CLAUDE.md: "the medals (the better grade of each)". Grades are 1 gold .. 3 bronze, so the better grade is the
            // smaller non-zero one; NewGamePlus.Apply takes Math.Max, which keeps bronze over gold. A Valkyrie start presets
            // medal 23 bronze (AddonStartHints), so an old gold 23 comes out bronze.
            Story.StartCampaign(Db, Campaign.Valkyrie);
            Assert.AreEqual(3, Session.Medals[23]);
            var old = Finished();
            old.medals[23] = 1;
            old.medals[30] = 1;
            NewGamePlus.Apply(old);
            Assert.AreEqual(1, Session.Medals[23], "gold beats bronze");
            Assert.AreEqual(1, Session.Medals[30]);
        }

        [Test]
        public void ABrokenMedalArrayIsIgnored()
        {
            var old = Finished();
            old.medals = new int[10];
            old.medals[4] = 1;
            NewGamePlus.Apply(old);
            Assert.AreEqual(0, Session.Medals[4]);
        }

        [Test]
        public void TheNewGameStaysNew()
        {
            Story.StartCampaign(Db, Campaign.GalaxyOnFire2);
            NewGamePlus.Apply(Finished());
            Assert.AreEqual(0, Story.Index, "the story starts over");
            Assert.AreEqual(10, Session.ShipIndex, "the starting ship");
            Assert.AreEqual(78, Session.StationIndex);
            CollectionAssert.AreEqual(new[] { 30, 0 }, Session.Standing);
            Assert.AreEqual(0, Session.Cargo.Count);
            Assert.IsFalse(Session.Equipment.Exists(e => e.item == 90));
        }

        [Test]
        public void ApplyNullDoesNothing()
        {
            Session.Credits = 7;
            NewGamePlus.Apply(null);
            Assert.AreEqual(7, Session.Credits);
            Assert.AreEqual(0, Session.KaamoState);
        }
    }
}
