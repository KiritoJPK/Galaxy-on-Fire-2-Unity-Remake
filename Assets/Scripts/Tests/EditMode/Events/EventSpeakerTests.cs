// EventSpeakerTests.cs
// Feature: CLAUDE.md "Multiplayer" > Chat commands (/dialog, /radio, the questions' speakers) and "Mods" > Characters:
// a speaker is "player" (the reader: -2), a story speaker by name or the one name it starts ("Keith", "Keith as Bob"
// renamed), a mod's character (-3, by key "mod_id:id"), or "<race> [female | male] [name]" with a face made once per
// dialog (-1); the spec travels as "id SEP name SEP extra" (SpeakerSpec). SplitSpeaker finds the speaker's colon by trying
// each colon in turn. EventNames checks the names the event commands take (stations, items, ships).
// Expected values: the speaker table's names (text 1597 + speaker in Localization/text_en.json: 0 Keith T. Maxwell,
// 2 Gunant Breh and 60 Gunant IV, 10 Pirate, 50 Vossk), the race words of Standing.RaceWord, the documented spec form.

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Events;
using GoF2Remake.Multiplayer;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class EventSpeakerTests : SandboxTest
    {
        const char US = '\u001f';

        // ---- SpeakerSpec ----

        [Test]
        public void ASpecOfAStorySpeaker()
        {
            Assert.IsTrue(SpeakerSpec.TryParse("0" + US + "Bob" + US, US, out var s));
            Assert.AreEqual(0, s.id);
            Assert.AreEqual("Bob", s.name);
            Assert.IsNull(s.face);
            Assert.IsNull(s.characterKey);
            Assert.AreEqual("Bob", s.DisplayName("Reader"), "a renamed speaker shows the new name");
        }

        [Test]
        public void ASpecOfAGeneratedFace()
        {
            Assert.IsTrue(SpeakerSpec.TryParse("-1" + US + "Jane" + US + "2,3,4,5,6", US, out var s));
            Assert.AreEqual(SpeakerSpec.Generated, s.id);
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6 }, s.face);
        }

        [Test]
        public void ASpecOfTheReader()
        {
            Assert.IsTrue(SpeakerSpec.TryParse("-2" + US + "%player%" + US, US, out var s));
            Assert.AreEqual(SpeakerSpec.Reader, s.id);
            Assert.AreEqual("Ann", s.DisplayName("Ann"));
        }

        [Test]
        public void ASpecOfAModCharacter()
        {
            Assert.IsTrue(SpeakerSpec.TryParse("-3" + US + "Captain" + US + "frontier_systems:vega", US, out var s));
            Assert.AreEqual(SpeakerSpec.Character, s.id);
            Assert.AreEqual("frontier_systems:vega", s.characterKey);
            Assert.AreEqual("Captain", s.DisplayName("x"));
            Assert.IsNull(s.CharacterDef, "its mod isn't installed here");
        }

        [Test]
        public void TheReadersNameFillsPlayer()
        {
            Assert.IsTrue(SpeakerSpec.TryParse("3" + US + "Agent of %player%" + US, US, out var s));
            Assert.AreEqual("Agent of Ann", s.DisplayName("Ann"));
        }

        [Test]
        public void AStorySpeakerWithoutRenameShowsItsName()
        {
            Assert.IsTrue(SpeakerSpec.TryParse("0" + US + US, US, out var s));
            Assert.AreEqual(StoryTable.SpeakerName(0), s.DisplayName("Ann"));
        }

        [Test]
        public void BrokenSpecs()
        {
            Assert.IsFalse(SpeakerSpec.TryParse("", US, out _));
            Assert.IsFalse(SpeakerSpec.TryParse(null, US, out _));
            Assert.IsFalse(SpeakerSpec.TryParse("7", US, out _), "no separator");
            Assert.IsFalse(SpeakerSpec.TryParse("x" + US + "y", US, out _), "no number");
        }

        [Test]
        public void TheIdsAreDistinct()
        {
            Assert.AreEqual(-1, SpeakerSpec.Generated);
            Assert.AreEqual(-2, SpeakerSpec.Reader);
            Assert.AreEqual(-3, SpeakerSpec.Character);
        }

        // ---- ResolveSpeaker / SplitSpeaker ----

        [Test]
        public void PlayerIsTheReader()
        {
            var faces = new Dictionary<string, string>();
            Assert.AreEqual("-2" + US + "%player%" + US, NetAdmin.ResolveSpeaker("player", faces));
            Assert.AreEqual("-2" + US + "%player%" + US, NetAdmin.ResolveSpeaker("YOU", faces));
        }

        [Test]
        public void AStorySpeakerByItsFirstName()
        {
            EventsTestUtil.RequireEnglish();
            var faces = new Dictionary<string, string>();
            Assert.AreEqual("0" + US + US, NetAdmin.ResolveSpeaker("Keith", faces));
            Assert.AreEqual("0" + US + US, NetAdmin.ResolveSpeaker("keith t. maxwell", faces), "the whole name, any case");
            Assert.AreEqual("0" + US + "Bob" + US, NetAdmin.ResolveSpeaker("Keith as Bob", faces));
        }

        [Test]
        public void AnExactNameWinsOverLongerOnes()
        {
            EventsTestUtil.RequireEnglish();
            var faces = new Dictionary<string, string>();
            Assert.AreEqual("10" + US + US, NetAdmin.ResolveSpeaker("Pirate", faces), "speaker 10 is \"Pirate\", 9 \"Pirate Boss\"");
            Assert.AreEqual("9" + US + US, NetAdmin.ResolveSpeaker("Pirate Boss", faces));
            Assert.AreEqual("50" + US + US, NetAdmin.ResolveSpeaker("vossk", faces), "speaker 50 is \"Vossk\" (before the race word)");
        }

        [Test]
        public void AnAmbiguousFirstNameIsNoSpeaker()
        {
            EventsTestUtil.RequireEnglish();
            // "Gunant" starts both Gunant Breh (2) and Gunant IV (60): no speaker, and it is no race either.
            Assert.IsNull(NetAdmin.ResolveSpeaker("Gunant", new Dictionary<string, string>()));
            Assert.AreEqual("2" + US + US, NetAdmin.ResolveSpeaker("Gunant Breh", new Dictionary<string, string>()));
        }

        [Test]
        public void ARaceAndANameMakeAFaceOnce()
        {
            EventsTestUtil.RequireEnglish();
            var faces = new Dictionary<string, string>();
            string a = NetAdmin.ResolveSpeaker("terran female Jane", faces);
            Assert.IsNotNull(a);
            Assert.IsTrue(SpeakerSpec.TryParse(a, US, out var s));
            Assert.AreEqual(SpeakerSpec.Generated, s.id);
            Assert.AreEqual("Jane", s.name);
            Assert.IsNotNull(s.face);
            Assert.Greater(s.face.Length, 0);
            Assert.AreEqual(a, NetAdmin.ResolveSpeaker("Terran Female Jane", faces), "the same face for every page of the dialog");
            string other = NetAdmin.ResolveSpeaker("nivelian Kel", faces);
            Assert.IsTrue(SpeakerSpec.TryParse(other, US, out var k));
            Assert.AreEqual("Kel", k.name);
        }

        [Test]
        public void ARaceAloneIsNamedAfterTheRace()
        {
            EventsTestUtil.RequireEnglish();
            string spec = NetAdmin.ResolveSpeaker("midorian male", new Dictionary<string, string>());
            Assert.IsTrue(SpeakerSpec.TryParse(spec, US, out var s));
            Assert.AreEqual(SpeakerSpec.Generated, s.id);
            Assert.AreEqual(Localization.Get(406 + 3), s.name, "race 3's name (text 406 + race)");
        }

        [Test]
        public void NobodyIsNoSpeaker()
        {
            Assert.IsNull(NetAdmin.ResolveSpeaker("", new Dictionary<string, string>()));
            Assert.IsNull(NetAdmin.ResolveSpeaker("Hello there", new Dictionary<string, string>()));
            Assert.IsNull(NetAdmin.ResolveSpeaker("nomod:nobody", new Dictionary<string, string>()));
        }

        [Test]
        public void SplitSpeakerFindsTheSpeakersColon()
        {
            var faces = new Dictionary<string, string>();
            Assert.AreEqual(7, NetAdmin.SplitSpeaker("player : Hello: how are you", faces, out string spec));
            Assert.AreEqual("-2" + US + "%player%" + US, spec);
            Assert.AreEqual(-1, NetAdmin.SplitSpeaker("Hello there: friend", faces, out _), "no speaker before any colon");
            Assert.AreEqual(-1, NetAdmin.SplitSpeaker("no colon at all", faces, out _));
            Assert.AreEqual(-1, NetAdmin.SplitSpeaker(": empty speaker", faces, out _));
        }

        [Test]
        public void SplitSpeakerTriesEachColon()
        {
            EventsTestUtil.RequireEnglish();
            // A race speaker's page with a colon in its text splits at the colon that ends the speaker.
            var faces = new Dictionary<string, string>();
            int at = NetAdmin.SplitSpeaker("vossk K'ekki : Time: now", faces, out string spec);
            Assert.AreEqual("vossk K'ekki ".Length, at);
            Assert.IsTrue(SpeakerSpec.TryParse(spec, US, out var s));
            Assert.AreEqual("K'ekki", s.name);
        }

        [Test]
        public void RaceWords()
        {
            Assert.AreEqual(0, NetAdmin.RaceWord("Terran"));
            Assert.AreEqual(0, NetAdmin.RaceWord("terrans"));
            Assert.AreEqual(1, NetAdmin.RaceWord("VOSSK"));
            Assert.AreEqual(2, NetAdmin.RaceWord("nivelians"));
            Assert.AreEqual(3, NetAdmin.RaceWord("midorian"));
            Assert.AreEqual(GoF2Remake.Flight.Standing.Pirate, NetAdmin.RaceWord("pirates"));
            Assert.AreEqual(GoF2Remake.Flight.Standing.Void, NetAdmin.RaceWord("void"));
            Assert.AreEqual(GoF2Remake.Flight.Standing.Specter, NetAdmin.RaceWord("specters"));
            Assert.AreEqual(-1, NetAdmin.RaceWord("nobody"));
        }

        // ---- EventNames ----

        [Test]
        public void EveryStationNameIsKnown()
        {
            var stations = EventNames.Stations();
            Assert.IsTrue(stations.Any(s => s.name == "void"));
            foreach (var (label, name) in stations)
                Assert.IsNull(EventNames.CheckStation(name), label);
            Assert.IsNull(EventNames.CheckStation("78"));
            Assert.IsNotNull(EventNames.CheckStation("99999"));
            Assert.IsNotNull(EventNames.CheckStation("Nowhere At All"));
            Assert.IsNotNull(EventNames.CheckStation("78 extra"), "a station and more isn't one name");
        }

        [Test]
        public void EveryItemNameFindsItsItem()
        {
            var db = NetGame.Db;
            int checkedNames = 0;
            foreach (var it in db.Items)
            {
                string n = GoF2Remake.UI.ItemInfo.ItemName(it.index);
                if (string.IsNullOrEmpty(n) || n.StartsWith("#")) continue;
                int found = NetAdmin.FindItem(n, out string error);
                Assert.GreaterOrEqual(found, 0, n + ": " + error);
                Assert.AreEqual(n, GoF2Remake.UI.ItemInfo.ItemName(found), "the first item of that exact name");
                checkedNames++;
            }
            Assert.Greater(checkedNames, 200);
        }

        [Test]
        public void ItemsByNumber()
        {
            Assert.AreEqual(85, NetAdmin.FindItem("85", out _));
            Assert.AreEqual(-1, NetAdmin.FindItem("100000", out string error));
            Assert.IsNotNull(error);
            Assert.IsNull(EventNames.CheckItem("85"));
            Assert.IsNotNull(EventNames.CheckItem("no such item at all"));
        }

        [Test]
        public void ShipsByNumberAndName()
        {
            var db = NetGame.Db;
            Assert.AreEqual(10, NetAdmin.FindShip("10", out _));
            string phantom = GoF2Remake.World.DebugSpawner.ShipName(db, 10);
            Assert.AreEqual(10, NetAdmin.FindShip(phantom, out _));
            Assert.IsNull(EventNames.CheckShip("10", false));
            Assert.IsNotNull(EventNames.CheckShip("no such ship at all", false));
        }
    }
}
