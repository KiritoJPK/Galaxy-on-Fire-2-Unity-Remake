// NetFactionsTests.cs
// Feature: CLAUDE.md "Multiplayer" > Factions: lasting player groups kept by profile in factions.json (a tag of 2-4
// letters / digits, a name of at most 24, at most 50 members); a deleted profile leaves its faction, a leaderless faction
// passes to its first officer, else its first member, an empty one ends (and its claims with it); a faction's home is a
// station it claims; claims cost 500 000 from the bank, at most 3 per faction, lapse after 14 days, not the Kaamo Club
// (108) or Loma (system 25); sieges cost 250 000, the toll is 10 000; at a held station members pay 10 % less and other
// factions 5 % more, the tax going to the holder's bank; the console disbands by tag.
// Expected values: CLAUDE.md's numbers; the tax worked out by hand (a price of 1050 with 5 % on top holds 50 of tax).
// A dedicated server's profile folder with a factions.json is set up in the sandbox (NetServerFixture).

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using NUnit.Framework;
using P = GoF2Remake.Tests.NetServerFixture.Pilot;

namespace GoF2Remake.Tests
{
    public class NetFactionsTests : SandboxTest
    {
        const int AlphaStation = 10, SoloStation = 11;

        static string FactionsJson() =>
            "{\"factions\":[" +
            "{\"id\":\"f1\",\"name\":\"Alpha Wing\",\"tag\":\"ABC\",\"leader\":\"a\",\"created\":\"\",\"officers\":[\"b\"],\"members\":[\"a\",\"b\",\"c\"],\"bank\":0,\"home\":" + AlphaStation + "}," +
            "{\"id\":\"f2\",\"name\":\"Lone Star\",\"tag\":\"SOLO\",\"leader\":\"s\",\"created\":\"\",\"officers\":[],\"members\":[\"s\"],\"bank\":1000,\"home\":" + SoloStation + "}," +
            "{\"id\":\"f3\",\"name\":\"Xylo\",\"tag\":\"XY\",\"leader\":\"x\",\"created\":\"\",\"officers\":[],\"members\":[\"x\",\"y\",\"z\"],\"bank\":0,\"home\":-1}" +
            "],\"claims\":[" +
            "{\"station\":" + AlphaStation + ",\"faction\":\"f1\",\"claimed\":\"\",\"lastDock\":\"\",\"protectedUntil\":0}," +
            "{\"station\":" + SoloStation + ",\"faction\":\"f2\",\"claimed\":\"\",\"lastDock\":\"\",\"protectedUntil\":0}" +
            "],\"sieges\":[]}";

        [SetUp]
        public void StartServer()
        {
            var pilots = new[] { "a", "b", "c", "s", "x", "y", "z" }.Select(id => new P(id, "Pilot " + id.ToUpperInvariant(), 0));
            NetServerFixture.Start(Sandbox, pilots, FactionsJson());
        }

        [TearDown]
        public void StopServer() => NetServerFixture.Stop();

        [Test]
        public void TheDocumentedNumbers()
        {
            Assert.AreEqual(24, NetFactions.MaxNameLength);
            Assert.AreEqual(50, NetFactions.MaxMembers);
            Assert.AreEqual(500_000, NetFactions.DefaultClaimCost);
            Assert.AreEqual(3, NetFactions.DefaultMaxClaims);
            Assert.AreEqual(14, NetFactions.DefaultLapseDays);
            Assert.AreEqual(250_000, NetFactions.DefaultSiegeCost);
            Assert.AreEqual(10_000, NetFactions.DefaultToll);
            Assert.AreEqual(10, NetFactions.MemberDiscountPercent);
            Assert.AreEqual(5, NetFactions.TaxPercent);
        }

        [Test]
        public void ConfigureClampsTheServersChoices()
        {
            NetFactions.Configure(-1, 500, 0, -5, -10);
            Assert.AreEqual(0, NetFactions.ClaimCost);
            Assert.AreEqual(100, NetFactions.MaxClaims);
            Assert.AreEqual(1, NetFactions.LapseDays);
            Assert.AreEqual(0, NetFactions.SiegeCost);
            Assert.AreEqual(0, NetFactions.Toll);
            NetFactions.Configure(750_000, 5, 30, 100_000, 2500);
            Assert.AreEqual(750_000, NetFactions.ClaimCost);
            Assert.AreEqual(5, NetFactions.MaxClaims);
            Assert.AreEqual(30, NetFactions.LapseDays);
            Assert.AreEqual(100_000, NetFactions.SiegeCost);
            Assert.AreEqual(2500, NetFactions.Toll);
        }

        [Test]
        public void MembersKnowTheirFaction()
        {
            Assert.AreEqual("ABC", NetFactions.Of("a").tag);
            Assert.AreEqual("ABC", NetFactions.Of("c").tag);
            Assert.AreEqual("SOLO", NetFactions.Of("s").tag);
            Assert.IsNull(NetFactions.Of("nobody"));
            Assert.IsNull(NetFactions.Of(null));
            Assert.IsNull(NetFactions.OfClient(99), "a guest");
        }

        [Test]
        public void TheHomeIsAClaimedStation()
        {
            Assert.AreEqual(AlphaStation, NetFactions.HomeOf("c"));
            Assert.AreEqual(-1, NetFactions.HomeOf("x"), "no home set");
            Assert.AreEqual(-1, NetFactions.HomeOf("nobody"));
        }

        [Test]
        public void ALeaderlessFactionPassesToItsFirstOfficer()
        {
            NetFactions.OnAccountDeleted("a");
            var f = NetFactions.Of("b");
            Assert.AreEqual("b", f.leader);
            Assert.IsFalse(f.officers.Contains("b"), "the new leader is no longer an officer");
            Assert.IsFalse(f.members.Contains("a"));
            CollectionAssert.AreEqual(new[] { "b", "c" }, f.members);
        }

        [Test]
        public void WithoutOfficersItPassesToTheFirstMember()
        {
            NetFactions.OnAccountDeleted("x");
            Assert.AreEqual("y", NetFactions.Of("y").leader);
        }

        [Test]
        public void AMemberLeavingKeepsTheLeader()
        {
            NetFactions.OnAccountDeleted("c");
            var f = NetFactions.Of("a");
            Assert.AreEqual("a", f.leader);
            CollectionAssert.AreEqual(new[] { "b" }, f.officers);
            Assert.IsNull(NetFactions.Of("c"));
        }

        [Test]
        public void AnEmptyFactionEndsWithItsClaims()
        {
            NetFactions.OnAccountDeleted("s");
            Assert.IsNull(NetFactions.Of("s"));
            StringAssert.DoesNotContain("SOLO", NetFactions.ConsoleList());
            var s = new NetPanel.State { dockedStation = SoloStation };
            NetFactions.FillPanel(99, s);
            Assert.AreEqual("", s.stationHolder, "its claim is free again");
            Assert.IsFalse(s.factions.Any(r => r.tag == "SOLO"));
        }

        [Test]
        public void ChangesAreSaved()
        {
            NetFactions.OnAccountDeleted("a");
            NetFactions.Load();
            Assert.AreEqual("b", NetFactions.Of("b").leader, "factions.json was written");
        }

        [Test]
        public void TheConsoleDisbandsByTag()
        {
            StringAssert.Contains("ABC", NetFactions.ConsoleDisband("abc"));
            Assert.IsNull(NetFactions.Of("a"));
            Assert.IsNull(NetFactions.Of("b"));
            Assert.AreEqual(-1, NetFactions.HomeOf("c"));
            StringAssert.Contains("NOPE", NetFactions.ConsoleDisband("NOPE"));
        }

        [Test]
        public void TheConsoleLists()
        {
            string list = NetFactions.ConsoleList();
            StringAssert.StartsWith("3 faction(s):", list);
            StringAssert.Contains("[ABC] Alpha Wing: 3 member(s)", list);
            StringAssert.Contains("[SOLO] Lone Star: 1 member(s)", list);
        }

        [Test]
        public void TheOtherFactionsPayATaxToTheHolder()
        {
            // A price with 5 % on top: 1050 = 1000 + 50 of tax.
            var alpha = NetFactions.Of("a");
            long before = alpha.bank;
            NetFactions.OnPurchase(99, AlphaStation, 1050);
            Assert.AreEqual(before + 50, alpha.bank);
            NetFactions.OnPurchase(99, AlphaStation, 105);
            Assert.AreEqual(before + 55, alpha.bank);
        }

        [Test]
        public void NoTaxWhereNobodyHoldsTheStation()
        {
            var alpha = NetFactions.Of("a");
            var solo = NetFactions.Of("s");
            long a = alpha.bank, s = solo.bank;
            NetFactions.OnPurchase(99, 78, 1050);
            NetFactions.OnPurchase(99, AlphaStation, 0);
            NetFactions.OnPurchase(99, AlphaStation, -10);
            Assert.AreEqual(a, alpha.bank);
            Assert.AreEqual(s, solo.bank);
        }

        [Test]
        public void ThePanelListsTheFactionsAndTheStationsHolder()
        {
            var s = new NetPanel.State { dockedStation = AlphaStation };
            NetFactions.FillPanel(99, s);
            Assert.AreEqual(3, s.factions.Count);
            var abc = s.factions.Single(r => r.tag == "ABC");
            Assert.AreEqual(3, abc.members);
            Assert.AreEqual(1, abc.claims);
            Assert.AreEqual("ABC", s.stationHolder);
            Assert.IsFalse(s.inFaction, "a guest");
            Assert.AreEqual(NetFactions.ClaimCost, s.claimCost);
            Assert.AreEqual(NetFactions.MaxClaims, s.maxClaims);
        }

        [Test]
        public void TheKaamoClubAndLomaCantBeClaimed()
        {
            var kaamo = new NetPanel.State { dockedStation = 108 };
            NetFactions.FillPanel(99, kaamo);
            Assert.IsFalse(kaamo.stationClaimable, "the Kaamo Club (108)");
            var loma = Db.Stations.First(st => st.system == 25);
            var atLoma = new NetPanel.State { dockedStation = loma.index };
            NetFactions.FillPanel(99, atLoma);
            Assert.IsFalse(atLoma.stationClaimable, "Loma (system 25)");
            var varHastra = new NetPanel.State { dockedStation = 78 };
            NetFactions.FillPanel(99, varHastra);
            Assert.IsTrue(varHastra.stationClaimable);
            Assert.AreEqual("", varHastra.stationHolder);
        }

        [Test]
        public void ACommandWithoutAProfile()
        {
            Assert.IsNull(NetFactions.Command(99, "/hello"), "not a faction command");
            Assert.IsNotNull(NetFactions.Command(99, "/faction create NEW New Faction"), "a guest has no faction");
            Assert.IsNull(NetFactions.Of("nobody"));
            Assert.AreEqual(3, NetFactions.ConsoleList().Split('\n').Length - 1, "nothing was created");
        }

        [Test]
        public void NoSiegesYet()
        {
            Assert.AreEqual(Localization.Extra("mpSiegeNone", "No sieges."), NetFactions.ConsoleSieges());
        }

        [Test]
        public void OutsideASessionPricesAndRelationsAreTheGamesOwn()
        {
            // NetFactionsClient reads the claims NetState carries: none without a session.
            Assert.IsFalse(NetFactionsClient.Owner(AlphaStation, out _, out _));
            Assert.AreEqual("", NetFactionsClient.OwnerText(AlphaStation));
            Assert.AreEqual(1000, NetFactionsClient.BuyPrice(AlphaStation, 1000));
            Assert.AreEqual(1000, NetFactionsClient.SellPrice(AlphaStation, 1000));
            Assert.AreEqual(0, NetFactionsClient.Relation(AlphaStation, "ABC", -1));
            Assert.IsFalse(NetFactionsClient.SiegePvp(AlphaStation, "ABC", "XY"));
            Assert.IsNull(NetFactionsClient.SiegeAt(AlphaStation));
        }

        [Test]
        public void WithoutProfilesThereAreNoFactions()
        {
            NetServerFixture.Stop();
            StringAssert.Contains("-noprofiles", NetFactions.ConsoleList());
            Assert.IsNull(NetFactions.Of("a"));
        }
    }
}
