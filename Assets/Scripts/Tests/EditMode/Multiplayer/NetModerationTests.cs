// NetModerationTests.cs
// Feature: CLAUDE.md "Multiplayer" > Moderation and Player profiles: roles on the profile (0 player, 1 op, 2 admin,
// 3 master; the console outranks everyone); masters make admins, admins make ops; nobody acts on a pilot of their own
// role or higher, or gives a role as high as their own; ops kick (with minutes of cooldown, default 5, a temporary ban)
// and temp-ban for at most 24 h, admins also ban for good; a ban holds the profile and every device it signed in from;
// bans are kept in bans.json and the run-out ones pruned; the admin token is checked in constant time and kept out of the
// web log; profiles travel gzipped in 4000-byte chunks; tokens are stored as their SHA-256.
// Expected values: CLAUDE.md's rules and numbers (5 min, 24 h, 4000 bytes), SHA-256 test vectors (FIPS 180-2: "abc").
// A dedicated server's profile folder is set up in the sandbox (NetServerFixture); no session or player is online, so
// the commands act on the profiles by name.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using NUnit.Framework;
using P = GoF2Remake.Tests.NetServerFixture.Pilot;
using M = GoF2Remake.Multiplayer.NetModeration;

namespace GoF2Remake.Tests
{
    public class NetModerationTests : SandboxTest
    {
        string folder;

        [SetUp]
        public void StartServer() => folder = NetServerFixture.Start(Sandbox, Pilots());

        [TearDown]
        public void StopServer() => NetServerFixture.Stop();

        static IEnumerable<P> Pilots() => new[]
        {
            new P("mmm", "Mara", M.Master),
            new P("aaa", "Ann", M.Admin),
            new P("ooo", "Otto", M.Op),
            new P("ppp", "Pete", M.Player, "devP"),
            new P("qqq", "Quinn", M.Player),
        };

        static string Rank => Localization.Extra("mpAdminRank", "You can't do that to a pilot of your rank or higher.");
        static string OnlyAdmin => Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
        static string OnlyMaster => Localization.Extra("mpAdminOnlyMaster", "Only the server's master admin can do that.");

        static int Role(string id) => NetProfiles.RoleOf(id);

        // ---- roles ----

        [Test]
        public void RoleNames()
        {
            Assert.AreEqual("player", M.RoleName(M.Player));
            Assert.AreEqual("op", M.RoleName(M.Op));
            Assert.AreEqual("admin", M.RoleName(M.Admin));
            Assert.AreEqual("master", M.RoleName(M.Master));
            Assert.AreEqual("master", M.RoleName(M.ServerConsole));
            Assert.IsTrue(M.Player < M.Op && M.Op < M.Admin && M.Admin < M.Master && M.Master < M.ServerConsole);
        }

        [Test]
        public void TheFixturesRoles()
        {
            Assert.AreEqual(M.Master, Role("mmm"));
            Assert.AreEqual(M.Admin, Role("aaa"));
            Assert.AreEqual(M.Op, Role("ooo"));
            Assert.AreEqual(M.Player, Role("ppp"));
            Assert.AreEqual(0, NetProfiles.RoleOf(null), "a guest");
            Assert.AreEqual(0, NetProfiles.RoleOf("nosuchprofile"));
        }

        [Test]
        public void AnAdminMakesOps()
        {
            Assert.IsNotEmpty(M.Run("op", "Pete", M.Admin, "Ann"));
            Assert.AreEqual(M.Op, Role("ppp"));
            M.Run("deop", "Pete", M.Admin, "Ann");
            Assert.AreEqual(M.Player, Role("ppp"));
        }

        [Test]
        public void AnOpMakesNobodyAnything()
        {
            Assert.AreEqual(OnlyAdmin, M.Run("op", "Pete", M.Op, "Otto"));
            Assert.AreEqual(M.Player, Role("ppp"));
            Assert.AreEqual(OnlyAdmin, M.Run("deop", "Otto", M.Op, "Otto"));
            Assert.AreEqual(M.Op, Role("ooo"));
        }

        [Test]
        public void NobodyActsOnTheirOwnRankOrHigher()
        {
            Assert.AreEqual(Rank, M.Run("deop", "Ann", M.Admin, "Ann"), "an admin on another admin");
            Assert.AreEqual(M.Admin, Role("aaa"));
            Assert.AreEqual(Rank, M.Run("deop", "Mara", M.Admin, "Ann"), "an admin on the master");
            Assert.AreEqual(M.Master, Role("mmm"));
            Assert.AreEqual(Rank, M.Run("unadmin", "Mara", M.Master, "Mara"), "a master on a master");
            Assert.AreEqual(M.Master, Role("mmm"));
        }

        [Test]
        public void OnlyTheMasterMakesAdmins()
        {
            Assert.AreEqual(OnlyMaster, M.Run("admin", "Pete", M.Admin, "Ann"));
            Assert.AreEqual(M.Player, Role("ppp"));
            M.Run("admin", "Pete", M.Master, "Mara");
            Assert.AreEqual(M.Admin, Role("ppp"));
            M.Run("unadmin", "Ann", M.Master, "Mara");
            Assert.AreEqual(M.Player, Role("aaa"));
        }

        [Test]
        public void TheConsoleOutranksEveryone()
        {
            M.Run("unadmin", "Mara", M.ServerConsole, "Server");
            Assert.AreEqual(M.Player, Role("mmm"));
        }

        [Test]
        public void ARoleAlreadyHeld()
        {
            string answer = M.Run("op", "Otto", M.Admin, "Ann");
            StringAssert.Contains("Otto", answer);
            Assert.AreEqual(M.Op, Role("ooo"));
        }

        [Test]
        public void RoleChangesAreSaved()
        {
            M.Run("op", "Quinn", M.Admin, "Ann");
            NetServerFixture.Reset(typeof(NetProfiles));
            NetProfiles.Configure(true, NetProfiles.DefaultEarnPerMinute, folder);
            NetProfiles.Start();
            Assert.AreEqual(M.Op, Role("qqq"));
        }

        [Test]
        public void ProfilesByIdOrName()
        {
            Assert.AreEqual("ppp", NetProfiles.FindAccount("ppp"));
            Assert.AreEqual("ppp", NetProfiles.FindAccount("pete"), "the last pilot name, any case");
            Assert.IsNull(NetProfiles.FindAccount("Nobody"));
            Assert.IsNull(NetProfiles.FindAccount(""));
            Assert.AreEqual("Pete", NetProfiles.AccountName("ppp"));
            CollectionAssert.AreEqual(new[] { "devP" }, NetProfiles.DevicesOf("ppp"));
            Assert.IsEmpty(NetProfiles.DevicesOf("qqq"));
        }

        // ---- kicks and bans ----

        [Test]
        public void APlayerCantModerate()
        {
            // Client 99 has no profile: a player.
            Assert.AreEqual(M.Player, M.RoleOfClient(99));
            Assert.AreEqual(Localization.Extra("mpAdminNoRight", "Only the server's ops and admins can do that."), M.Command(99, "/kick Pete"));
            Assert.IsNull(M.BanReason("ppp", null));
            Assert.IsNull(M.Command(99, "/hello there"), "not a moderation command");
            Assert.IsNull(M.Run("nosuchcommand", "", M.Admin, "Ann"));
        }

        [Test]
        public void AnOpKicksAPlayerForMinutes()
        {
            Assert.AreEqual("", M.Run("kick", "Pete 10 spamming the chat", M.Op, "Otto"));
            string why = M.BanReason("ppp", null);
            Assert.IsNotNull(why, "a kick with minutes is a temporary ban");
            StringAssert.Contains("spamming the chat", why);
            StringAssert.Contains("10 min", why);
            Assert.IsNotNull(M.BanReason(null, "devP"), "the profile's devices are held too");
            Assert.IsNull(M.BanReason("qqq", null));
        }

        [Test]
        public void AKickHasAFiveMinuteDefault()
        {
            Assert.AreEqual(5, M.KickMinutes);
            M.Run("kick", "Pete", M.Admin, "Ann");
            string why = M.BanReason("ppp", null);
            Assert.IsNotNull(why);
            StringAssert.Contains("5 min", why);
        }

        [Test]
        public void AKickOfZeroMinutesIsNoBan()
        {
            M.Run("kick", "Quinn 0", M.Admin, "Ann");
            Assert.IsNull(M.BanReason("qqq", null));
        }

        [Test]
        public void AnOpCantKickAnOp()
        {
            Assert.AreEqual(Rank, M.Run("kick", "Otto 10", M.Op, "Otto"));
            Assert.IsNull(M.BanReason("ooo", null));
            Assert.AreEqual(Rank, M.Run("kick", "Ann", M.Op, "Otto"));
        }

        [Test]
        public void AnOpsKickLastsAtMost24Hours()
        {
            M.Run("kick", "Pete 5000", M.Op, "Otto");
            string why = M.BanReason("ppp", null);
            Assert.IsNotNull(why);
            Assert.IsTrue(why.Contains("24 h") || why.Contains("1 days"), why + ": 1440 minutes, not 5000");
        }

        [Test]
        public void AnOpsTempbanLastsAtMost24Hours()
        {
            Assert.AreEqual(24 * 60, M.MaxOpBanMinutes);
            Assert.IsNotEmpty(M.Run("tempban", "Pete 1441", M.Op, "Otto"));
            Assert.IsNull(M.BanReason("ppp", null), "refused");
            Assert.AreEqual("", M.Run("tempban", "Pete 1440", M.Op, "Otto"));
            Assert.IsNotNull(M.BanReason("ppp", null));
        }

        [Test]
        public void AnAdminsTempbanMayBeLonger()
        {
            Assert.AreEqual("", M.Run("tempban", "Pete 4320 cheating", M.Admin, "Ann"));
            StringAssert.Contains("3 days", M.BanReason("ppp", null));
        }

        [Test]
        public void ATempbanNeedsMinutes()
        {
            Assert.IsNotEmpty(M.Run("tempban", "Pete", M.Admin, "Ann"));
            Assert.IsNotEmpty(M.Run("tempban", "Pete 0", M.Admin, "Ann"));
            Assert.IsNotEmpty(M.Run("tempban", "Pete soon", M.Admin, "Ann"));
            Assert.IsNull(M.BanReason("ppp", null));
        }

        [Test]
        public void OnlyAdminsBanForGood()
        {
            Assert.AreEqual(OnlyAdmin, M.Run("ban", "Pete", M.Op, "Otto"));
            Assert.IsNull(M.BanReason("ppp", null));
            Assert.AreEqual("", M.Run("ban", "Pete griefing", M.Admin, "Ann"));
            string why = M.BanReason("ppp", null);
            Assert.IsNotNull(why);
            StringAssert.Contains("griefing", why);
            Assert.AreEqual(Rank, M.Run("ban", "Mara", M.Admin, "Ann"));
            Assert.IsNull(M.BanReason("mmm", null));
        }

        [Test]
        public void UnbanLiftsIt()
        {
            M.Run("ban", "Pete", M.Admin, "Ann");
            Assert.IsNotEmpty(M.Run("unban", "Pete", M.Op, "Otto"), "an op may lift a ban");
            Assert.IsNull(M.BanReason("ppp", null));
            Assert.IsNull(M.BanReason(null, "devP"));
            Assert.IsNotEmpty(M.Run("unban", "Pete", M.Op, "Otto"), "\"isn't banned\"");
        }

        [Test]
        public void AnUnknownPilotIsNoTarget()
        {
            Assert.AreEqual(string.Format(Localization.Extra("mpAdminNobody", "No pilot or profile \"{0}\"."), "Nobody"),
                            M.Run("kick", "Nobody", M.Admin, "Ann"));
            Assert.IsNotEmpty(M.Run("op", "Nobody", M.Admin, "Ann"));
        }

        [Test]
        public void BansAreKeptInTheirFile()
        {
            M.Run("ban", "Pete", M.Admin, "Ann");
            Assert.IsTrue(File.Exists(Path.Combine(folder, "bans.json")));
            NetServerFixture.Reset(typeof(NetModeration));
            Assert.IsNull(M.BanReason("ppp", null), "nothing loaded");
            M.Load();
            Assert.IsNotNull(M.BanReason("ppp", null), "the ban after a restart");
        }

        [Test]
        public void RunOutBansArePruned()
        {
            NetServerFixture.Stop();
            long past = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60;
            long future = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
            string bans = "{\"bans\":[" +
                          "{\"account\":\"ppp\",\"name\":\"Pete\",\"reason\":\"old\",\"by\":\"Ann\",\"at\":\"\",\"devices\":[],\"until\":" + past + "}," +
                          "{\"account\":\"qqq\",\"name\":\"Quinn\",\"reason\":\"new\",\"by\":\"Ann\",\"at\":\"\",\"devices\":[],\"until\":" + future + "}," +
                          "{\"account\":\"ooo\",\"name\":\"Otto\",\"reason\":\"forever\",\"by\":\"Ann\",\"at\":\"\",\"devices\":[],\"until\":0}]}";
            folder = NetServerFixture.Start(Sandbox, Pilots(), bansJson: bans);
            Assert.IsNull(M.BanReason("ppp", null), "ran out");
            Assert.IsNotNull(M.BanReason("qqq", null));
            Assert.IsNotNull(M.BanReason("ooo", null), "0 = for good");
        }

        [Test]
        public void OnlyAdminsSay()
        {
            Assert.AreEqual(OnlyAdmin, M.Run("say", "hello", M.Op, "Otto"));
            Assert.AreEqual("/say <text>", M.Run("say", "  ", M.Admin, "Ann"));
        }

        [Test]
        public void OnlyTheMasterDeletesProfiles()
        {
            Assert.AreEqual(OnlyMaster, M.Run("deleteprofile", "Pete", M.Admin, "Ann"));
            Assert.AreEqual(Rank, M.Run("deleteprofile", "Mara", M.Master, "Mara"));
            Assert.AreEqual("ppp", NetProfiles.FindAccount("ppp"));
        }

        // ---- the admin token ----

        [Test]
        public void TheAdminToken()
        {
            string token = NetGame.CommandLineValue("-admintoken");
            if (string.IsNullOrWhiteSpace(token)) token = Environment.GetEnvironmentVariable(M.TokenEnvironment);
            if (string.IsNullOrWhiteSpace(token)) token = File.ReadAllText(Path.Combine(folder, "admin_token.txt"));
            token = token.Trim();
            Assert.IsNotEmpty(token, "made at the first start");
            Assert.IsTrue(M.TokenMatches(token));
            Assert.IsFalse(M.TokenMatches(token.Substring(1) + (token[0] == 'A' ? 'B' : 'A')), "a wrong token of its length");
            Assert.IsFalse(M.TokenMatches(token + "x"));
            Assert.IsFalse(M.TokenMatches(""));
            Assert.IsFalse(M.TokenMatches(null));
            Assert.AreEqual("log: [admin token] here", M.Redact("log: " + token + " here"));
            Assert.AreEqual("nothing secret", M.Redact("nothing secret"));
        }

        // ---- profile transport ----

        [Test]
        public void TokensAreStoredAsSha256()
        {
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", NetProfiles.Hash("abc"));
            Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", NetProfiles.Hash(""));
            Assert.AreEqual(NetProfiles.Hash(""), NetProfiles.Hash(null));
        }

        [Test]
        public void AProfileTravelsInChunks()
        {
            Assert.AreEqual(4000, NetProfiles.ChunkBytes);
            var rng = new System.Random(5);
            var sb = new StringBuilder("{\"data\":\"");
            for (int i = 0; i < 60000; i++) sb.Append((char)('a' + rng.Next(26)));   // barely compressible
            sb.Append("\"}");
            string json = sb.ToString();
            var parts = NetProfiles.Pack(json);
            Assert.Greater(parts.Count, 1);
            foreach (var p in parts) Assert.LessOrEqual(p.Length, NetProfiles.ChunkBytes);
            Assert.AreEqual(json, NetProfiles.Unpack(parts.ToArray()));
        }

        [Test]
        public void ASmallProfileIsOneChunk()
        {
            var parts = NetProfiles.Pack("{\"credits\":5}");
            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual("{\"credits\":5}", NetProfiles.Unpack(parts.ToArray()));
            Assert.IsEmpty(NetProfiles.Pack(""));
            Assert.IsEmpty(NetProfiles.Pack(null));
        }

        [Test]
        public void BrokenChunksAreNoProfile()
        {
            Assert.IsNull(NetProfiles.Unpack(new[] { new byte[] { 1, 2, 3, 4 } }));
            var parts = NetProfiles.Pack("{\"x\":1}");
            Assert.IsNull(NetProfiles.Unpack(new[] { parts[0], null }));
        }

        [Test]
        public void ProfilesNeedADedicatedServer()
        {
            Assert.IsFalse(NetProfiles.Enabled, "configured, but no dedicated server or persistent host runs");
            Assert.AreEqual("testserver", NetProfiles.ServerId);
            Assert.AreEqual(30, NetProfiles.PruneDays);
            Assert.AreEqual(1_000_000, NetProfiles.DefaultEarnPerMinute);
        }
    }
}
