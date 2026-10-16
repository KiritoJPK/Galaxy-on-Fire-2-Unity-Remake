// NetFactions.cs
// Remake-only: factions, the lasting player groups of a dedicated server with profiles (NetProfiles). A squad (NetSquad)
// stays the session's quick group for flying together (shared missions, no friendly fire); a faction is kept between
// sessions by profile and is what will hold territory (home station, claims, bank: later phases).
//   A faction: a name (MaxNameLength), a tag of 2-4 letters / digits shown before its members' names ("[TAG] Name": the
//     lock plate, chat), a leader, officers and members (profile ids), a bank (credits, for the later phases) and a home
//     station (-1 = none yet).
//   Chat commands (answered to the sender only): /faction create TAG Name, /faction invite <pilot>, /faction join TAG,
//     /faction leave, /faction kick <pilot>, /faction promote / demote <pilot>, /faction leader <pilot>, /faction disband,
//     /faction info [TAG], /faction list; "/f <text>" talks to the faction members online.
//   Ranks: the leader does everything; officers invite and kick members; members talk. The leader leaves only by
//     handing over (/faction leader) or as the last member (the faction ends).
//   Storage: factions.json beside the profiles (NetProfiles.Folder), written like them (through .tmp, the old one as .bak).
//   A profile deleted (the console, a /link that replaces it) leaves its faction; a leaderless faction passes to its first
//   officer, else its first member.
// Phase 2, territory:
//   Bank: "/faction deposit N" takes N credits from the player (ChargeRpc: their game pays if it can and answers) into the
//     bank; "/faction withdraw N" (officers) pays N out to the player (GrantRpc). Both move the profile's worth with them
//     (NetProfiles.AdjustWorth), so the upload check neither trips on a withdrawal nor lets a deposit that wasn't paid
//     go unnoticed for long (the client still runs its credits: a modified game can lie about paying).
//   Claims: "/faction claim" (officers, docked at the station) claims it for ClaimCost from the bank, at most MaxClaims per
//     faction; not the Kaamo Club (108) or Loma (system 25). The first claim is the home; "/faction home" (officers, docked at
//     another claim) moves it; "/faction unclaim" (officers, docked there) gives it up. A claim no member has docked at for
//     LapseDays lapses. The claims reach every player (NetState.Claims: "station|TAG|Name|VAT" lines; NetFactionsClient), who
//     see the owner on the star map, the station's header and the orbit information.
//   Home: a member signing in starts docked at the faction's home, and a destroyed member respawns there.
// Phase 3, contest and benefits:
//   Siege: "/faction siege" (officers of another faction, in the orbit or docked there; SiegeCost from the bank as the
//     stake; one at a time; the faction needs room for another claim; not within ProtectionHours of the station's last
//     siege; not the defenders' home while they hold another station) is announced and armed SiegeDelaySeconds later.
//     Armed, it waits until pilots of both factions are alive in the orbit's space at once (real players only: NPCs never
//     start it). Meanwhile:
//       the station defenders escalate (NetOrbit.UpdateSiegeDefenders): DefenderCount / DefenderLevel by the
//         DefenderStepSeconds steps since the arming, frozen when the fight starts;
//       the claim drains: a defending pilot docking there (not a respawn after dying in the fight) halves its remaining
//         lapse days (rounded up) while more than LapseFloorDays are left, never resets it (HalveLapse); a claim lapsing
//         now goes to the attacker (the stake back);
//       the standoff clock: a side present while the other side has a pilot online elsewhere adds to the absent side's
//         standoff; StandoffSeconds of it = that side forfeits;
//       no fight by expiresAt (SiegeMeetSeconds from the arming) = one more day, at most MaxExtensions times, then it ends
//         with no fight: the defenders keep it and win the stake.
//     The fight (SiegeSeconds): the two factions' pilots may fire at each other there (NetFactionsClient.SiegePvp,
//     NetPlayer.PvpWith); every pilot of a side destroyed in the orbit scores for the other side (attKills / defKills); a
//     destroyed defender respawns docked at that station (NetFactionsClient.DefendsSiegeHere in FlightHud). A side whose
//     last pilot there left alive (docked, jumped, disconnected) and stays gone SiegeForfeitSeconds waves the white flag
//     and loses. At the end more kills wins; a tie = held. The stake goes to the winner. Called off, the stake back to the
//     attacker: the claim given up or the defenders disbanding. A fought or finished siege leaves the station protected for
//     ProtectionHours. A claim that lapses without a siege can't be claimed again for LapsedLockSeconds.
//     NetState.Sieges carries them to the players (the HUD banner, TerritoryView). "/faction sieges" lists them.
//   Defence: in a held station's orbit its own race's NPC fighters treat the holder's members as friends and the
//     members of other factions as enemies, unless that pilot paid the toll (Toll credits, asked on arrival by
//     TerritoryView; NetPlayer.TollStation) for this visit. Pilots without a faction are treated as always.
//   Trade cut: at a held station members buy items MemberDiscountPercent cheaper and pay no VAT.
//   VAT: each claim has the holder's rate (Claim.vat, 0..MaxVat %, "/faction vat <rate> [station]", officers; DefaultVat for
//     a new claim). Everyone else (other factions and pilots without one) buys items and dealer ships at the list price plus
//     a tenth of the rate (VatMarkup: 20 % -> +2 %); the rate's share of what they paid goes to the holder's bank
//     (OnPurchase from the shared stock's trades, OnShipBought from the dealer's). Selling there pays no VAT. Units players
//     sold into a station's stock carry no VAT when bought back (NetStock's resale count, cleared when the stock is rolled
//     again), so a buy-and-sell loop doesn't mint money. A higher rate thins the station's goods: each stock roll keeps
//     (100 - rate) % of every row and of the dealer ships (NetStock.Make).
//   Garrison (players' request: a station whose holders are offline fell to one attacker in 5 minutes): officers set a
//     claim's garrison, "/faction garrison <ships> <level> [station]" or the Faction tab: 0..MaxGarrison fighters of the
//     system's race at level 1..MaxGarrisonLevel (hull x(1 + 0.5 (level - 1)), gun x(1 + 0.25 (level - 1)), the dearer
//     fighters from level 3). Upkeep GarrisonUpkeep x ships x level a day from the bank, the first day when it is set
//     (not while the station's siege runs); a bank that can't pay disbands it. In an armed siege the server counts the
//     garrison (Siege.garrisonAlive); the orbit's authority flies that many (NetOrbit.UpdateGarrison: hostile to the attackers, friends of the holders, swarming in
//     front of the station) and reports each death (NetState.GarrisonKillRpc, OnGarrisonKill); a dead fighter comes back
//     GarrisonRespawnSeconds later. Siege.garrisonAlive / level go out with the sieges (PublishSieges).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetFactions
    {
        public const int MaxNameLength = 24, MaxMembers = 50;
        const float InviteSeconds = 300f, ClaimTickSeconds = 60f, ChargeSeconds = 300f;   // a deposit's answer may lag (a phone in the background)
        public const int DefaultClaimCost = 500_000, DefaultMaxClaims = 3, DefaultLapseDays = 14;
        public const int DefaultSiegeCost = 250_000, DefaultToll = 10_000;
        public const int MemberDiscountPercent = 10, DefaultVat = 5, MaxVat = 20;

        /// <summary>The price a non-member pays at a station with VAT 'rate': the list price + a tenth of the rate.</summary>
        public static int VatMarkup(int price, int rate) => rate <= 0 || price <= 0 ? price : (int)Math.Min(int.MaxValue, (long)Math.Round(price * (1.0 + rate / 1000.0)));

        /// <summary>Server: the VAT rate of the station's claim (0 = no holder or none).</summary>
        public static int VatAt(int station) => list == null ? 0 : Mathf.Clamp(ClaimAt(station)?.vat ?? 0, 0, MaxVat);
        const long SiegeDelaySeconds = 600, SiegeSeconds = 900, ProtectionHours = 24;
        const long SiegeMeetSeconds = 2 * 86400, SiegeForfeitSeconds = 60, StandoffSeconds = 3600, LapsedLockSeconds = 48 * 3600;
        const int MaxExtensions = 4, LapseFloorDays = 3;
        const float SiegeTickSeconds = 5f;
        /// <summary>The free station defenders of an armed siege's orbit, on top of the garrison (NetOrbit): DefenderStart at
        /// DefenderStartLevel, + DefenderStepCount and + 1 level every DefenderStepSeconds until the fight, at most DefenderMax
        /// at MaxGarrisonLevel; from DefenderGunLevel their guns hit DefenderGunBonus harder.</summary>
        public const int DefenderStart = 16, DefenderStepCount = 4, DefenderMax = 48, DefenderStartLevel = 3, DefenderGunLevel = 4;
        public const long DefenderStepSeconds = 6 * 3600;
        public const float SiegeDefenderRespawnSeconds = 30f, DefenderGunBonus = 1.5f;
        public static int DefenderCount(int step) => Mathf.Min(DefenderStart + DefenderStepCount * Mathf.Max(0, step), DefenderMax);
        public static int DefenderLevel(int step) => Mathf.Min(DefenderStartLevel + Mathf.Max(0, step), MaxGarrisonLevel);
        public const int MaxGarrison = 12, MaxGarrisonLevel = 5, GarrisonUpkeep = 300;
        const long GarrisonRespawnSeconds = 90, DaySeconds = 86400;
        const float GarrisonKillWindow = 10f;   // at most GarrisonKillsPerWindow reports count in it (a modified game reporting a whole garrison)
        const int GarrisonKillsPerWindow = 6;

        /// <summary>A garrison's upkeep a day.</summary>
        public static int GarrisonCost(int ships, int level) => Mathf.Max(0, ships) * Mathf.Clamp(level, 1, MaxGarrisonLevel) * GarrisonUpkeep;

        /// <summary>A siege's price from the bank (-siegecost), the toll a pilot of another faction pays at a held station
        /// (-toll: credits, or "N%" = N percent of the pilot's credits, stored as -N; 0 = none; TollText / ParseToll).</summary>
        public static int SiegeCost { get; internal set; } = DefaultSiegeCost;
        public static int Toll { get; internal set; } = DefaultToll;

        /// <summary>"10000" = that many credits, "5%" = 5 % of the pilot's credits (1..100, stored as -5); null = not valid.</summary>
        public static int? ParseToll(string text)
        {
            text = (text ?? "").Trim().Replace(",", "").Replace(" ", "");
            if (text.EndsWith("%"))
                return int.TryParse(text.TrimEnd('%'), out int pct) && pct >= 0 && pct <= 100 ? -pct : (int?)null;
            return long.TryParse(text, out long n) && n >= 0 ? (int)Math.Min(n, int.MaxValue) : (int?)null;
        }

        /// <summary>The toll setting as typed: "10000" or "5%".</summary>
        public static string TollText(int toll) => toll < 0 ? $"{-toll}%" : toll.ToString();

        /// <summary>What a pilot with 'credits' pays for the toll setting 'toll' (0 = none).</summary>
        public static int TollFor(int toll, long credits) =>
            toll >= 0 ? toll : (int)Math.Min(int.MaxValue, Math.Max(1L, Math.Max(0L, credits) * -toll / 100));
        const int KaamoStation = 108, LomaSystem = 25;

        /// <summary>A claim's price from the bank (-claimcost), the claims per faction (-maxclaims), the days without a member
        /// docking before a claim lapses (-claimdays).</summary>
        public static int ClaimCost { get; internal set; } = DefaultClaimCost;
        public static int MaxClaims { get; internal set; } = DefaultMaxClaims;
        public static int LapseDays { get; internal set; } = DefaultLapseDays;

        /// <summary>DedicatedServer.Boot: the command line's choices.</summary>
        public static void Configure(int claimCost, int maxClaims, int lapseDays, int siegeCost, int toll)
        {
            SiegeCost = Mathf.Max(0, siegeCost);
            Toll = Mathf.Max(-100, toll);   // -N = N % of the pilot's credits
            ClaimCost = Mathf.Max(0, claimCost);
            MaxClaims = Mathf.Clamp(maxClaims, 0, 100);
            LapseDays = Mathf.Max(1, lapseDays);
        }

        [Serializable]
        public class Faction
        {
            public string id, name, tag, leader, created;
            public List<string> officers = new List<string>(), members = new List<string>();   // members include everyone
            public long bank;
            public int home = -1;
        }

        [Serializable] public class Claim
        {
            public int station; public string faction, claimed, lastDock; public long protectedUntil;
            public int garrisonSize, garrisonLevel = 1; public long garrisonPaidUntil;   // the garrison (0 = none) and its upkeep
            public int vat = DefaultVat;                                                  // the holder's VAT rate (%)
        }

        /// <summary>A siege: times in Unix seconds. armed = waiting for both sides (from startsAt until expiresAt), started =
        /// the fight (until endsAt); stake = the attacker's SiegeCost, to the winner; noAttackersSince / noDefendersSince =
        /// when that side was last seen gone from the orbit during the fight (0 = there). See the header for the rest.</summary>
        [Serializable] public class Siege
        {
            public int station; public string attacker, defender; public long startsAt, endsAt, expiresAt, stake; public bool armed, started;
            public long noAttackersSince, noDefendersSince;
            public int extensions, attKills, defKills, fightStep = -1;   // the days added, the fight's score, the escalation step frozen at its start
            public float attStandoff, defStandoff;                       // seconds each side was absent while the other waited (standoff clock)
            public bool attLeftAlive, defLeftAlive;                      // that side's last pilot in the orbit left it alive (the white flag)
            [NonSerialized] public List<ulong> respawning = new List<ulong>();   // defenders destroyed in the fight: their next dock there is a respawn
            public int garrisonAlive, garrisonLevel = 1;                  // the defenders' fighters alive now (server-side count)
            public List<long> garrisonBack = new List<long>();            // when each dead one comes back (Unix seconds)
            [NonSerialized] public float killWindowStart = -100f;
            [NonSerialized] public int killsInWindow;
            // Each pilot of the two factions at the last tick (the fight's deaths and departures, the defenders' docks).
            [NonSerialized] public Dictionary<ulong, (bool attacker, bool inOrbit, bool alive, bool docked)> last = new Dictionary<ulong, (bool, bool, bool, bool)>();
        }

        [Serializable]
        class FactionList
        {
            public List<Faction> factions = new List<Faction>();
            public List<Claim> claims = new List<Claim>();
            public List<Siege> sieges = new List<Siege>();
            public List<Lapsed> lapsed = new List<Lapsed>();
        }

        /// <summary>A claim that lapsed without a siege: nobody claims it before 'until' (Unix seconds).</summary>
        [Serializable] public class Lapsed { public int station; public long until; }

        // Deposits waiting for the player's game to pay: by a token the server made (the amount is the server's own).
        static readonly Dictionary<int, (string account, string faction, int amount, float until)> charges = new Dictionary<int, (string, string, int, float)>();
        static int nextCharge = 1;
        static float claimTimer, siegeTimer;

        static FactionList list;
        static readonly Dictionary<string, (string faction, float until)> invites = new Dictionary<string, (string, float)>();   // by profile id

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            list = null; invites.Clear(); charges.Clear(); claimTimer = siegeTimer = 0f; taxDirty = false;
            ClaimCost = DefaultClaimCost; MaxClaims = DefaultMaxClaims; LapseDays = DefaultLapseDays;
            SiegeCost = DefaultSiegeCost; Toll = DefaultToll;
        }

        static string PathOf => Path.Combine(NetProfiles.Folder, "factions.json");
        /// <summary>The file before the groups were called factions (read once, when factions.json doesn't exist yet).</summary>
        static string LegacyPath => Path.Combine(NetProfiles.Folder, "crews.json");

        /// <summary>NetProfiles.Start: the factions loaded.</summary>
        public static void Load()
        {
            invites.Clear();
            try
            {
                if (File.Exists(PathOf)) list = JsonUtility.FromJson<FactionList>(File.ReadAllText(PathOf));
                else if (File.Exists(LegacyPath))
                {
                    // A server from before the rename ("crews"): its groups, claims and sieges carry over (written as
                    // factions.json on the next save; crews.json stays as it was).
                    string old = File.ReadAllText(LegacyPath).Replace("\"crews\":", "\"factions\":").Replace("\"crew\":", "\"faction\":");
                    list = JsonUtility.FromJson<FactionList>(old);
                    Debug.Log($"Server: crews.json read as the factions ({list?.factions?.Count ?? 0}).");
                }
                else list = null;
            }
            catch (Exception e) { Debug.LogError($"NetFactions: factions.json unreadable ({e.Message}); starting a new list (the old file stays as .bak on the next write)."); list = null; }
            if (list == null) list = new FactionList();
            if (list.factions == null) list.factions = new List<Faction>();
            if (list.claims == null) list.claims = new List<Claim>();
            if (list.sieges == null) list.sieges = new List<Siege>();
            if (list.lapsed == null) list.lapsed = new List<Lapsed>();
            foreach (var c in list.factions) { c.officers ??= new List<string>(); c.members ??= new List<string>(); }
            foreach (var c in list.claims) { c.garrisonLevel = Mathf.Clamp(c.garrisonLevel, 1, MaxGarrisonLevel); c.vat = Mathf.Clamp(c.vat, 0, MaxVat); }
            foreach (var x in list.sieges)
            {
                x.garrisonBack ??= new List<long>();
                x.last ??= new Dictionary<ulong, (bool, bool, bool, bool)>();
                x.respawning ??= new List<ulong>();
                x.garrisonLevel = Mathf.Clamp(x.garrisonLevel, 1, MaxGarrisonLevel);
                if (x.expiresAt == 0) x.expiresAt = x.startsAt + SiegeMeetSeconds;   // a siege from before the meeting rule
                if (x.started) x.armed = true;
            }
            charges.Clear();
            PublishClaims();
        }

        static void Save() { if (list != null) NetProfiles.Write(PathOf, JsonUtility.ToJson(list, true)); }

        /// <summary>The faction a profile belongs to, null = none.</summary>
        public static Faction Of(string account) => account == null || list == null ? null : list.factions.Find(c => c.members.Contains(account));

        /// <summary>The faction of a connected player, null = none (or a guest).</summary>
        public static Faction OfClient(ulong client) => Of(NetProfiles.AccountOf(client));

        static Faction ByTag(string tag) => list?.factions.Find(c => string.Equals(c.tag, tag, StringComparison.OrdinalIgnoreCase));

        static bool IsOfficer(Faction c, string account) => c.leader == account || c.officers.Contains(account);

        // ---- names --------------------------------------------------------------------------------------------

        /// <summary>NetProfiles: a player signed in (or linked): their faction's tag on their name, its home.</summary>
        public static void OnLogin(ulong client) => RefreshTag(client);

        static void RefreshTag(ulong client)
        {
            var p = NetSquad.Find(client);
            if (p == null) return;
            p.SetFactionTag(OfClient(client)?.tag ?? "");
            p.SetFactionHome(HomeOf(NetProfiles.AccountOf(client)));
        }

        static void RefreshTags(string account) { foreach (ulong c in NetProfiles.ClientsOf(account)) RefreshTag(c); }

        static void Tell(string account, string text) { foreach (ulong c in NetProfiles.ClientsOf(account)) NetState.Instance?.Notify(c, text); }

        static void TellFaction(Faction faction, string text) { foreach (var m in faction.members) Tell(m, text); }

        static string NameOf(string account) => NetProfiles.AccountName(account) ?? account;

        /// <summary>A faction member by the pilot name (online or not; the profile's last name).</summary>
        static string MemberByName(Faction faction, string name)
        {
            foreach (var m in faction.members) if (string.Equals(NameOf(m), name, StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }

        // ---- chat commands ----------------------------------------------------------------------------------

        /// <summary>NetState.SendChatRpc: a /faction or /f command's answer to its sender; null = not one of them.</summary>
        public static string Command(ulong client, string text)
        {
            var words = text.Trim().Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string cmd = words.Length > 0 ? words[0].ToLowerInvariant() : "";
            if (cmd != "/faction" && cmd != "/f") return null;
            string me = NetProfiles.AccountOf(client);
            if (me == null) return Localization.Extra("mpFactionGuest", "Factions need a profile on this server (you play as a guest).");
            if (cmd == "/f") return Talk(me, text.Trim().Length > 2 ? text.Trim().Substring(2).Trim() : "");
            string sub = words.Length > 1 ? words[1].ToLowerInvariant() : "info";
            string arg = words.Length > 2 ? words[2].Trim() : "";
            switch (sub)
            {
                case "create": return Create(me, arg);
                case "invite": return Invite(me, arg);
                case "join": return Join(me, arg);
                case "leave": return Leave(me);
                case "kick": return Kick(me, arg);
                case "promote": return SetOfficer(me, arg, true);
                case "demote": return SetOfficer(me, arg, false);
                case "leader": return HandOver(me, arg);
                case "disband": return Disband(me);
                case "info": return Info(arg.Length > 0 ? ByTag(arg) : Of(me), arg);
                case "list": return ListText();
                case "deposit": return Deposit(client, me, arg);
                case "withdraw": return Withdraw(client, me, arg);
                case "claim": return ClaimHere(client, me);
                case "unclaim": return Unclaim(client, me);
                case "home": return SetHome(client, me);
                case "claims": return ClaimsText(arg.Length > 0 ? ByTag(arg) : Of(me));
                case "siege": return DeclareSiege(client, me);
                case "sieges": return SiegesText();
                case "garrison": return SetGarrison(client, me, arg);
                case "vat": return SetVat(client, me, arg);
                default:
                    return Localization.Extra("mpFactionHelp", "Faction commands: /faction create TAG Name, invite <pilot>, join TAG, leave, kick <pilot>, " +
                                                            "promote / demote <pilot>, leader <pilot>, disband, info [TAG], list; deposit N, withdraw N; " +
                                                            "claim, unclaim, home, claims [TAG] (docked at the station); garrison <ships> <level> [station]; vat <0-20> [station]; siege (in another faction's orbit), sieges; " +
                                                            "/f <text> talks to your faction.");
            }
        }

        static string Create(string me, string arg)
        {
            if (Of(me) != null) return Localization.Extra("mpFactionAlready", "You are in a faction already: /faction leave first.");
            int space = arg.IndexOf(' ');
            string tag = (space < 0 ? arg : arg.Substring(0, space)).ToUpperInvariant();
            string name = space < 0 ? "" : NetGame.Clean(arg.Substring(space + 1));
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            if (tag.Length < 2 || tag.Length > 4 || !IsTag(tag) || name.Length == 0)
                return Localization.Extra("mpFactionCreateUsage", "/faction create TAG Name: a tag of 2-4 letters or digits, then the faction's name.");
            if (ByTag(tag) != null) return string.Format(Localization.Extra("mpFactionTagTaken", "The tag {0} is taken."), tag);
            if (list.factions.Exists(c => string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)))
                return string.Format(Localization.Extra("mpFactionNameTaken", "A faction called {0} exists already."), name);
            var faction = new Faction { id = Guid.NewGuid().ToString("N").Substring(0, 8), name = name, tag = tag, leader = me, created = DateTime.UtcNow.ToString("o") };
            faction.members.Add(me);
            list.factions.Add(faction);
            Save();
            RefreshTags(me);
            Debug.Log($"Server: faction [{tag}] {name} created by {NameOf(me)}.");
            NetNews.Post(NetNews.Kind.Faction, $"New faction in the sector: {NetNews.FactionName(tag, name)}, founded by {NetNews.Safe(NameOf(me))}");
            return string.Format(Localization.Extra("mpFactionCreated", "Faction [{0}] {1} created. /faction invite <pilot> to bring others in."), tag, name);
        }

        static bool IsTag(string tag)
        {
            foreach (char c in tag) if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }

        static string Invite(string me, string who)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            if (faction.members.Count >= MaxMembers) return string.Format(Localization.Extra("mpFactionFull", "The faction is full ({0})."), MaxMembers);
            NetPlayer target = null;
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsSpawned && string.Equals(p.DisplayName, who, StringComparison.OrdinalIgnoreCase)) { target = p; break; }
            string account = target != null ? NetProfiles.AccountOf(target.OwnerClientId) : null;
            if (account == null) return string.Format(Localization.Extra("mpFactionNoPilot", "No pilot with a profile called \"{0}\" online."), who);
            if (Of(account) != null) return string.Format(Localization.Extra("mpFactionTheyHaveOne", "{0} is in a faction already."), target.DisplayName);
            invites[account] = (faction.id, Time.realtimeSinceStartup + InviteSeconds);
            Tell(account, string.Format(Localization.Extra("mpFactionInvited", "{0} invites you to the faction [{1}] {2}: type /faction join {1}."), NameOf(me), faction.tag, faction.name));
            return string.Format(Localization.Extra("mpFactionInviteSent", "Invitation sent to {0} (5 minutes)."), target.DisplayName);
        }

        static string Join(string me, string tag)
        {
            if (Of(me) != null) return Localization.Extra("mpFactionAlready", "You are in a faction already: /faction leave first.");
            var faction = ByTag(tag);
            if (faction == null || !invites.TryGetValue(me, out var inv) || inv.faction != faction.id || inv.until < Time.realtimeSinceStartup)
                return Localization.Extra("mpFactionNoInvite", "You have no invitation from that faction.");
            if (faction.members.Count >= MaxMembers) return string.Format(Localization.Extra("mpFactionFull", "The faction is full ({0})."), MaxMembers);
            invites.Remove(me);
            faction.members.Add(me);
            Save();
            RefreshTags(me);
            TellFaction(faction, string.Format(Localization.Extra("mpFactionJoined", "{0} joined the faction."), NameOf(me)));
            return "";
        }

        static string Leave(string me)
        {
            var faction = Of(me);
            if (faction == null) return Localization.Extra("mpFactionNone", "You aren't in a faction.");
            if (faction.leader == me && faction.members.Count > 1)
                return Localization.Extra("mpFactionLeaderLeave", "Hand the faction over first (/faction leader <pilot>), or /faction disband.");
            Remove(faction, me);
            return Localization.Extra("mpFactionYouLeft", "You left the faction.");
        }

        static string Kick(string me, string who)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            string target = MemberByName(faction, who);
            if (target == null) return string.Format(Localization.Extra("mpFactionNoMember", "No faction member called \"{0}\"."), who);
            if (target == me) return Localization.Extra("mpFactionKickSelf", "Use /faction leave.");
            if (target == faction.leader || (faction.officers.Contains(target) && faction.leader != me))
                return Localization.Extra("mpFactionKickRank", "Only the leader can remove an officer, and nobody the leader.");
            Tell(target, string.Format(Localization.Extra("mpFactionKicked", "You were removed from the faction [{0}]."), faction.tag));
            Remove(faction, target);
            return "";
        }

        static string SetOfficer(string me, string who, bool on)
        {
            var faction = Of(me);
            if (faction == null || faction.leader != me) return Localization.Extra("mpFactionNotLeader", "Only the faction's leader can do that.");
            string target = MemberByName(faction, who);
            if (target == null || target == me) return string.Format(Localization.Extra("mpFactionNoMember", "No faction member called \"{0}\"."), who);
            if (on && !faction.officers.Contains(target)) faction.officers.Add(target);
            if (!on) faction.officers.Remove(target);
            Save();
            TellFaction(faction, string.Format(on ? Localization.Extra("mpFactionPromoted", "{0} is an officer now.") : Localization.Extra("mpFactionDemoted", "{0} is no officer any more."), NameOf(target)));
            return "";
        }

        static string HandOver(string me, string who)
        {
            var faction = Of(me);
            if (faction == null || faction.leader != me) return Localization.Extra("mpFactionNotLeader", "Only the faction's leader can do that.");
            string target = MemberByName(faction, who);
            if (target == null || target == me) return string.Format(Localization.Extra("mpFactionNoMember", "No faction member called \"{0}\"."), who);
            faction.leader = target;
            faction.officers.Remove(target);
            if (!faction.officers.Contains(me)) faction.officers.Add(me);   // the old leader stays an officer
            Save();
            TellFaction(faction, string.Format(Localization.Extra("mpFactionNewLeader", "{0} leads the faction now."), NameOf(target)));
            return "";
        }

        static string Disband(string me)
        {
            var faction = Of(me);
            if (faction == null || faction.leader != me) return Localization.Extra("mpFactionNotLeader", "Only the faction's leader can do that.");
            TellFaction(faction, string.Format(Localization.Extra("mpFactionDisbanded", "The faction [{0}] {1} was disbanded."), faction.tag, faction.name));
            var members = new List<string>(faction.members);
            int claimsBefore = list.claims.Count;
            list.factions.Remove(faction);
            list.claims.RemoveAll(c => c.faction == faction.id);   // its territory is free again (the bank goes with it)
            foreach (var s in new List<Siege>(list.sieges))
                if (s.defender == faction.id) CallOff(s, Localization.Extra("mpSiegeOffDisband", "[{0}] {1} is gone: the siege of {2} is off, the {3:N0} credits stake goes back to [{4}]."), faction.tag, faction.name);
            list.sieges.RemoveAll(s => s.attacker == faction.id || s.defender == faction.id);   // an attacker's stake goes with its bank
            PublishClaims();
            PublishSieges();
            Save();
            foreach (var m in members) RefreshTags(m);
            Debug.Log($"Server: faction [{faction.tag}] {faction.name} disbanded.");
            int freed = claimsBefore - list.claims.Count;
            NetNews.Post(NetNews.Kind.Faction, $"{NetNews.FactionName(faction.tag, faction.name)} is no more"
                + (freed > 0 ? $": {freed} station{(freed == 1 ? "" : "s")} up for grabs" : ""));
            return "";
        }

        static string Talk(string me, string text)
        {
            var faction = Of(me);
            if (faction == null) return Localization.Extra("mpFactionNone", "You aren't in a faction.");
            text = NetChat.Clean(text);
            if (text.Length == 0) return "/f <text>";
            TellFaction(faction, $"[{faction.tag}] {NameOf(me)}: {text}");
            Debug.Log($"[Faction {faction.tag}] {NameOf(me)}: {text}");
            return "";
        }

        static string Info(Faction faction, string asked)
        {
            if (faction == null)
                return asked.Length > 0 ? string.Format(Localization.Extra("mpFactionNoTag", "No faction with the tag {0}."), asked.ToUpperInvariant())
                                        : Localization.Extra("mpFactionNoneHelp", "You aren't in a faction. /faction create TAG Name, or ask a faction for an invitation; /faction list shows them.");
            var sb = new StringBuilder($"[{faction.tag}] {faction.name}: {faction.members.Count} member(s), leader {NameOf(faction.leader)}");
            if (faction.officers.Count > 0) sb.Append(", officers ").Append(string.Join(", ", faction.officers.ConvertAll(NameOf)));
            var online = faction.members.FindAll(m => NetProfiles.IsOnline(m));
            sb.Append(online.Count > 0 ? ". Online: " + string.Join(", ", online.ConvertAll(NameOf)) : ". Nobody online");
            sb.Append($". Bank {faction.bank:N0}");
            var claims = list.claims.FindAll(c => c.faction == faction.id);
            if (claims.Count > 0) sb.Append(". Territory: ").Append(string.Join(", ", claims.ConvertAll(c => StationName(c.station) + (c.station == faction.home ? " (home)" : ""))));
            sb.Append('.');
            return sb.ToString();
        }

        static string ListText()
        {
            if (list == null || list.factions.Count == 0) return Localization.Extra("mpFactionNoFactions", "No factions yet. /faction create TAG Name starts one.");
            var sb = new StringBuilder(Localization.Extra("mpFactionListTitle", "Factions:"));
            foreach (var c in list.factions) sb.Append($"\n[{c.tag}] {c.name}: {c.members.Count}");
            return sb.ToString();
        }

        /// <summary>A member out of the faction (left, kicked, profile deleted); an emptied faction ends, a leaderless one passes on.</summary>
        static void Remove(Faction faction, string account)
        {
            faction.members.Remove(account);
            faction.officers.Remove(account);
            invites.Remove(account);
            if (faction.members.Count == 0)
            {
                list.factions.Remove(faction);
                list.claims.RemoveAll(c => c.faction == faction.id);
                PublishClaims();
                Debug.Log($"Server: faction [{faction.tag}] {faction.name} ended (no members).");
            }
            else
            {
                if (faction.leader == account)
                {
                    faction.leader = faction.officers.Count > 0 ? faction.officers[0] : faction.members[0];
                    faction.officers.Remove(faction.leader);
                    TellFaction(faction, string.Format(Localization.Extra("mpFactionNewLeader", "{0} leads the faction now."), NameOf(faction.leader)));
                }
                TellFaction(faction, string.Format(Localization.Extra("mpFactionMemberLeft", "{0} left the faction."), NameOf(account)));
            }
            Save();
            RefreshTags(account);
        }

        /// <summary>NetProfiles: a profile was deleted.</summary>
        public static void OnAccountDeleted(string account)
        {
            var faction = Of(account);
            if (faction != null) Remove(faction, account);
        }

        // ---- the bank ---------------------------------------------------------------------------------------

        static bool TryAmount(string arg, out int amount) => int.TryParse(arg.Replace(",", "").Replace(".", "").Replace(" ", ""), out amount) && amount > 0;

        static string Deposit(ulong client, string me, string arg)
        {
            var faction = Of(me);
            if (faction == null) return Localization.Extra("mpFactionNone", "You aren't in a faction.");
            if (!TryAmount(arg, out int amount)) return "/faction deposit N";
            if (!NetProfiles.Controls(client)) return Localization.Extra("mpFactionController", "Do that on the device that controls your profile.");
            int token = nextCharge++;
            charges[token] = (me, faction.id, amount, Time.realtimeSinceStartup + ChargeSeconds);
            NetState.Instance?.Charge(client, token, amount);
            return "";
        }

        /// <summary>NetState.ChargedRpc: the player's game paid a deposit (or couldn't: not enough credits).</summary>
        public static void OnCharged(ulong client, int token, bool paid)
        {
            if (!charges.TryGetValue(token, out var c) || c.account != NetProfiles.AccountOf(client))
            {
                if (paid) Debug.LogWarning($"Server: client {client} paid an unknown or expired faction deposit ({token}): nothing banked.");
                return;
            }
            charges.Remove(token);
            var faction = list?.factions.Find(x => x.id == c.faction);
            if (!paid) { NetState.Instance?.Notify(client, Localization.Extra("mpFactionNoCredits", "You don't have that many credits.")); return; }
            NetProfiles.AdjustWorth(c.account, -c.amount);
            if (faction == null) { Grant(client, c.account, c.amount); return; }   // the faction went meanwhile: back to the player
            faction.bank += c.amount;
            Save();
            TellFaction(faction, string.Format(Localization.Extra("mpFactionDeposited", "{0} put {1:N0} credits into the bank ({2:N0})."), NameOf(c.account), c.amount, faction.bank));
        }

        static string Withdraw(ulong client, string me, string arg)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            if (!TryAmount(arg, out int amount)) return "/faction withdraw N";
            if (!NetProfiles.Controls(client)) return Localization.Extra("mpFactionController", "Do that on the device that controls your profile.");
            if (faction.bank < amount) return string.Format(Localization.Extra("mpFactionBankShort", "The bank has {0:N0} credits."), faction.bank);
            faction.bank -= amount;
            Save();
            Grant(client, me, amount);
            TellFaction(faction, string.Format(Localization.Extra("mpFactionWithdrew", "{0} took {1:N0} credits from the bank ({2:N0})."), NameOf(me), amount, faction.bank));
            return "";
        }

        /// <summary>Credits to the player's game; their profile's worth moves with it (the upload check).</summary>
        static void Grant(ulong client, string account, int amount)
        {
            NetProfiles.AdjustWorth(account, amount);
            NetState.Instance?.Grant(client, amount);
        }

        // ---- territory --------------------------------------------------------------------------------------

        static string StationName(int station)
        {
            var st = NetGame.Db.Stations.Find(s => s.index == station);
            return st != null ? st.name : $"station {station}";
        }

        static Claim ClaimAt(int station) => list?.claims.Find(c => c.station == station);

        /// <summary>The player's station while docked there (-1 = not docked).</summary>
        static int DockedAt(ulong client)
        {
            var p = NetSquad.Find(client);
            return p != null && p.InHangar ? p.Station : -1;
        }

        static string ClaimHere(ulong client, string me)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            int station = DockedAt(client);
            var st = NetGame.Db.Stations.Find(s => s.index == station);
            if (st == null) return Localization.Extra("mpFactionDockFirst", "Dock at the station first.");
            if (station == KaamoStation || st.system == LomaSystem) return Localization.Extra("mpFactionNotClaimable", "This station can't be claimed.");
            var held = ClaimAt(station);
            if (held != null)
            {
                var owner = list.factions.Find(c => c.id == held.faction);
                return held.faction == faction.id ? Localization.Extra("mpFactionOwnClaim", "Your faction holds this station already.")
                                            : string.Format(Localization.Extra("mpFactionClaimedBy", "[{0}] {1} holds this station."), owner?.tag, owner?.name);
            }
            int count = list.claims.FindAll(c => c.faction == faction.id).Count;
            if (count >= MaxClaims) return string.Format(Localization.Extra("mpFactionMaxClaims", "A faction holds at most {0} stations."), MaxClaims);
            list.lapsed.RemoveAll(l => l.until <= UnixNow);
            var locked = list.lapsed.Find(l => l.station == station);
            if (locked != null)
                return string.Format(Localization.Extra("mpFactionLapsedLock", "This station's claim lapsed: it can't be claimed for another {0:0.#} hours."), (locked.until - UnixNow) / 3600f);
            if (faction.bank < ClaimCost)
                return string.Format(Localization.Extra("mpFactionClaimCost", "A claim costs {0:N0} credits from the bank (it has {1:N0}): /faction deposit N."), ClaimCost, faction.bank);
            faction.bank -= ClaimCost;
            string now = DateTime.UtcNow.ToString("o");
            list.claims.Add(new Claim { station = station, faction = faction.id, claimed = now, lastDock = now });
            if (faction.home < 0 || ClaimAt(faction.home)?.faction != faction.id) faction.home = station;
            Save();
            PublishClaims();
            RefreshHomes(faction);
            Debug.Log($"Server: [{faction.tag}] claimed {st.name}.");
            NetState.Instance?.Announce(string.Format(Localization.Extra("mpFactionClaimedNews", "[{0}] {1} claimed {2}."), faction.tag, faction.name, st.name));
            NetNews.Post(NetNews.Kind.Territory, $"{NetNews.FactionName(faction.tag, faction.name)} plants its flag on {NetNews.Place(station)}", station);
            return "";
        }

        static string Unclaim(ulong client, string me)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            var held = ClaimAt(DockedAt(client));
            if (held == null || held.faction != faction.id) return Localization.Extra("mpFactionNotYours", "Dock at one of your faction's stations first.");
            list.claims.Remove(held);
            if (faction.home == held.station) faction.home = list.claims.Find(c => c.faction == faction.id)?.station ?? -1;
            Save();
            PublishClaims();
            RefreshHomes(faction);
            TellFaction(faction, string.Format(Localization.Extra("mpFactionUnclaimed", "Your faction gave up {0}."), StationName(held.station)));
            NetNews.Post(NetNews.Kind.Territory, $"{NetNews.FactionName(faction.tag, faction.name)} pulls out of {NetNews.Place(held.station)}: the station is free", held.station);
            return "";
        }

        static string SetHome(ulong client, string me)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            var held = ClaimAt(DockedAt(client));
            if (held == null || held.faction != faction.id) return Localization.Extra("mpFactionNotYours", "Dock at one of your faction's stations first.");
            faction.home = held.station;
            Save();
            RefreshHomes(faction);
            TellFaction(faction, string.Format(Localization.Extra("mpFactionHome", "{0} is your faction's home now."), StationName(held.station)));
            return "";
        }

        static double DaysSince(string utc) =>
            DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? (DateTime.UtcNow - t.ToUniversalTime()).TotalDays : 0;

        static string ClaimsText(Faction faction)
        {
            if (faction == null) return Localization.Extra("mpFactionNone", "You aren't in a faction.");
            var claims = list.claims.FindAll(c => c.faction == faction.id);
            if (claims.Count == 0) return string.Format(Localization.Extra("mpFactionNoClaims", "[{0}] holds no stations."), faction.tag);
            var sb = new StringBuilder($"[{faction.tag}] {faction.name}:");
            foreach (var c in claims)
                sb.Append($"\n{StationName(c.station)}{(c.station == faction.home ? " (home)" : "")}: lapses in {Math.Max(0, LapseDays - DaysSince(c.lastDock)):0.#} days without a member docking");
            return sb.ToString();
        }

        /// <summary>A profile's faction home station, -1 = none (NetProfiles: where a member's game starts).</summary>
        public static int HomeOf(string account)
        {
            var faction = Of(account);
            return faction != null && ClaimAt(faction.home)?.faction == faction.id ? faction.home : -1;
        }

        static void RefreshHomes(Faction faction)
        {
            foreach (var m in faction.members)
                foreach (ulong c in NetProfiles.ClientsOf(m)) NetSquad.Find(c)?.SetFactionHome(HomeOf(m));
        }

        /// <summary>Every claim to the players (NetState.Claims): "station|TAG|Name|VAT %" per line.</summary>
        static void PublishClaims()
        {
            if (list == null || NetState.Instance == null) return;
            var sb = new StringBuilder();
            foreach (var c in list.claims)
            {
                var faction = list.factions.Find(x => x.id == c.faction);
                if (faction != null) sb.Append(c.station).Append('|').Append(faction.tag).Append('|').Append(faction.name.Replace("|", " "))
                                       .Append('|').Append(Mathf.Clamp(c.vat, 0, MaxVat)).Append('\n');
            }
            NetState.Instance.SetClaims(sb.ToString());
        }

        /// <summary>NetState: spawned on the server (the claims go out once it exists).</summary>
        public static void OnStateSpawned() { PublishClaims(); PublishSieges(); NetState.Instance?.SetToll(Toll); }

        /// <summary>NetState.Update (server): members docked at a claim keep it; claims nobody kept lapse; old deposits drop.</summary>
        public static void Tick()
        {
            if (list == null) return;
            if ((siegeTimer -= Time.unscaledDeltaTime) <= 0f) { siegeTimer = SiegeTickSeconds; TickSieges(SiegeTickSeconds); }
            if (charges.Count > 0)
                foreach (var key in new List<int>(charges.Keys)) if (charges[key].until < Time.realtimeSinceStartup) charges.Remove(key);
            if ((claimTimer -= Time.unscaledDeltaTime) > 0f) return;
            claimTimer = ClaimTickSeconds;
            if (list.claims.Count == 0) return;
            bool changed = taxDirty;   // the trade tax since the last write (saved once a minute)
            taxDirty = false;
            string now = DateTime.UtcNow.ToString("o");
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned || !p.InHangar) continue;
                var held = ClaimAt(p.Station);
                var faction = held != null ? OfClient(p.OwnerClientId) : null;
                // Under siege docking doesn't keep the claim (TickSieges halves its days instead).
                if (faction != null && faction.id == held.faction && SiegeAt(held.station) == null) { held.lastDock = now; changed = true; }
            }
            if (TickUpkeep()) changed = true;
            foreach (var c in new List<Claim>(list.claims))
            {
                if (DaysSince(c.lastDock) < LapseDays) continue;
                var siege = SiegeAt(c.station);
                var holderTag = list.factions.Find(x => x.id == c.faction)?.tag;
                if (siege != null && siege.defender == c.faction && list.factions.Exists(x => x.id == siege.attacker))
                {
                    // Lapsing under siege: the station goes to the attackers (the stake back to them).
                    EndSiege(siege, true, string.Format(Localization.Extra("mpSiegeLapsed", "Nobody of [{0}] kept {1} under siege: the claim lapsed."), holderTag, StationName(c.station)));
                    changed = true;
                    continue;
                }
                list.claims.Remove(c);
                list.lapsed.RemoveAll(l => l.station == c.station);
                list.lapsed.Add(new Lapsed { station = c.station, until = UnixNow + LapsedLockSeconds });
                var faction = list.factions.Find(x => x.id == c.faction);
                if (faction != null)
                {
                    if (faction.home == c.station) faction.home = list.claims.Find(x => x.faction == faction.id)?.station ?? -1;
                    TellFaction(faction, string.Format(Localization.Extra("mpFactionLapsed", "Nobody of your faction docked at {0} for {1} days: the claim lapsed."), StationName(c.station), LapseDays));
                    RefreshHomes(faction);
                }
                Debug.Log($"Server: the claim on {StationName(c.station)} lapsed.");
                NetNews.Post(NetNews.Kind.Territory, faction != null
                    ? $"{NetNews.FactionName(faction.tag, faction.name)} abandoned {NetNews.Place(c.station)}: nobody docked there for {LapseDays} days"
                    : $"{NetNews.Place(c.station)} is unclaimed again", c.station);
                changed = true;
            }
            if (changed) { Save(); PublishClaims(); }
        }


        // ---- sieges (phase 3) ---------------------------------------------------------------------------

        static long UnixNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        static Siege SiegeAt(int station) => list?.sieges.Find(s => s.station == station);

        static string DeclareSiege(ulong client, string me)
        {
            var faction = Of(me);
            if (faction == null || !IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            var p = NetSquad.Find(client);
            int station = p != null ? p.Station : -1;
            var held = ClaimAt(station);
            if (held == null) return Localization.Extra("mpSiegeWhere", "Fly to (or dock at) a station another faction holds first.");
            if (held.faction == faction.id) return Localization.Extra("mpFactionOwnClaim", "Your faction holds this station already.");
            if (SiegeAt(station) != null) return Localization.Extra("mpSiegeRunning", "This station is under siege already.");
            if (list.sieges.Exists(s => s.attacker == faction.id)) return Localization.Extra("mpSiegeOne", "Your faction is besieging another station already.");
            var holder = list.factions.Find(c => c.id == held.faction);
            if (holder != null && holder.home == station && list.claims.Exists(c => c.faction == holder.id && c.station != station))
                return string.Format(Localization.Extra("mpSiegeHome", "{0} is the home of [{1}]: besiege their other stations first."), StationName(station), holder.tag);
            if (held.protectedUntil > UnixNow)
                return string.Format(Localization.Extra("mpSiegeProtected", "This station can't be besieged for another {0:0.#} hours."), (held.protectedUntil - UnixNow) / 3600f);
            if (list.claims.FindAll(c => c.faction == faction.id).Count >= MaxClaims)
                return string.Format(Localization.Extra("mpFactionMaxClaims", "A faction holds at most {0} stations."), MaxClaims);
            if (faction.bank < SiegeCost)
                return string.Format(Localization.Extra("mpSiegeCost", "A siege costs {0:N0} credits from the bank (it has {1:N0})."), SiegeCost, faction.bank);
            var defender = list.factions.Find(c => c.id == held.faction);
            faction.bank -= SiegeCost;
            long now = UnixNow;
            list.sieges.Add(new Siege { station = station, attacker = faction.id, defender = held.faction, startsAt = now + SiegeDelaySeconds,
                                        endsAt = now + SiegeDelaySeconds + SiegeSeconds, expiresAt = now + SiegeDelaySeconds + SiegeMeetSeconds, stake = SiegeCost });
            Save();
            PublishSieges();
            NetState.Instance?.Announce(string.Format(Localization.Extra("mpSiegeDeclared3",
                "[{0}] {1} besieges {2}, held by [{3}] {4}: in {5} minutes the station's defences rise until pilots of both factions meet in the orbit (within {6} days); then a 15-minute fight, the most kills wins. {7:N0} credits go to the winner."),
                faction.tag, faction.name, StationName(station), defender?.tag, defender?.name, SiegeDelaySeconds / 60, SiegeMeetSeconds / 86400, SiegeCost));
            Debug.Log($"Server: [{faction.tag}] besieges {StationName(station)} ([{defender?.tag}]).");
            NetNews.Post(NetNews.Kind.War, $"{NetNews.FactionName(faction.tag, faction.name)} declares war on {NetNews.FactionName(defender?.tag, defender?.name)}: "
                + $"siege of {NetNews.Place(station)} in {SiegeDelaySeconds / 60} minutes", station);
            return "";
        }

        static string SiegesText()
        {
            if (list == null || list.sieges.Count == 0) return Localization.Extra("mpSiegeNone", "No sieges.");
            var sb = new StringBuilder(Localization.Extra("mpSiegeList", "Sieges:"));
            long now = UnixNow;
            foreach (var s in list.sieges)
            {
                var a = list.factions.Find(c => c.id == s.attacker);
                var d = list.factions.Find(c => c.id == s.defender);
                sb.Append($"\n{StationName(s.station)}: [{a?.tag}] against [{d?.tag}], {s.stake:N0} credits at stake, ")
                  .Append(!s.armed ? $"armed in {(s.startsAt - now) / 60 + 1} min"
                          : !s.started ? $"waiting for both sides ({(s.expiresAt - now) / 3600f:0.#} h left, {s.extensions}/{MaxExtensions} days added, "
                                         + $"{DefenderCount(Step(s, now))} defenders at level {DefenderLevel(Step(s, now))}, standoff [{a?.tag}] {s.attStandoff / 60f:0} / [{d?.tag}] {s.defStandoff / 60f:0} of {StandoffSeconds / 60} min)"
                          : $"fighting {s.attKills} : {s.defKills}, {(s.endsAt - now) / 60 + 1} min left");
            }
            return sb.ToString();
        }

        /// <summary>The escalation step of an armed siege (frozen at the fight's start).</summary>
        static int Step(Siege s, long now) => !s.armed ? 0 : s.fightStep >= 0 ? s.fightStep : (int)(Math.Max(0, now - s.startsAt) / DefenderStepSeconds);

        /// <summary>Every 5 s: see the header (arming, escalation, the claim draining, the standoff clock, the extensions, the
        /// fight's score and white flags).</summary>
        static void TickSieges(float dt)
        {
            if (list.sieges.Count == 0) return;
            long now = UnixNow;
            bool changed = false;
            foreach (var s in new List<Siege>(list.sieges))
            {
                var held = ClaimAt(s.station);
                var attacker = list.factions.Find(c => c.id == s.attacker);
                var defender = list.factions.Find(c => c.id == s.defender);
                if (attacker == null) { list.sieges.Remove(s); changed = true; continue; }   // the attackers' faction ended: the stake with it
                if (held == null || held.faction != s.defender || defender == null)
                {
                    // The claim was given up: nothing to fight for, the stake back.
                    CallOff(s, Localization.Extra("mpSiegeOffClaim", "{2} isn't held by [{0}] {1} any more: the siege is off, the {3:N0} credits stake goes back to [{4}]."),
                            defender?.tag, defender?.name);
                    changed = true;
                    continue;
                }
                if (now < s.startsAt) continue;
                string place = StationName(s.station);
                if (!s.armed)
                {
                    s.armed = true;
                    s.garrisonAlive = held.garrisonSize;
                    s.garrisonLevel = Mathf.Clamp(held.garrisonLevel, 1, MaxGarrisonLevel);
                    s.garrisonBack.Clear();
                    changed = true;
                    NetState.Instance?.Announce(string.Format(Localization.Extra("mpSiegeArmed2",
                        "The siege of {0} is armed: its defences rise until pilots of [{1}] and [{2}] meet in its orbit."), place, attacker.tag, defender.tag));
                }
                // The garrison's dead come back after their cooldown.
                for (int i = s.garrisonBack.Count - 1; i >= 0; i--)
                    if (now >= s.garrisonBack[i]) { s.garrisonBack.RemoveAt(i); s.garrisonAlive++; changed = true; }

                int att = 0, def = 0;
                bool attOnline = false, defOnline = false;
                var seen = new HashSet<ulong>();
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || !p.IsSpawned) continue;
                    var faction = OfClient(p.OwnerClientId);
                    bool isAtt = faction != null && faction.id == s.attacker, isDef = faction != null && faction.id == s.defender;
                    if (!isAtt && !isDef) continue;
                    ulong id = p.OwnerClientId;
                    seen.Add(id);
                    if (isAtt) attOnline = true; else defOnline = true;
                    bool inOrbit = p.InSpace && p.Station == s.station, alive = p.Hull > 0f, docked = p.InHangar && p.Station == s.station;
                    s.last.TryGetValue(id, out var prev);
                    if (s.started && prev.inOrbit && prev.alive)
                    {
                        if (inOrbit && !alive)
                        {
                            // Destroyed in the fight: a point for the other side; a defender respawns docked there.
                            if (isDef) { s.attKills++; if (!s.respawning.Contains(id)) s.respawning.Add(id); } else s.defKills++;
                            changed = true;
                        }
                        else if (!inOrbit) { if (isAtt) s.attLeftAlive = true; else s.defLeftAlive = true; }
                    }
                    if (inOrbit && alive) { if (isAtt) att++; else def++; }
                    if (isDef && docked && !prev.docked)
                    {
                        if (s.respawning.Remove(id)) changed = true;      // back after dying in the fight: not a dock
                        else if (HalveLapse(held, defender)) changed = true;
                    }
                    s.last[id] = (isAtt, inOrbit, alive, docked);
                }
                // Gone from the session while alive in the orbit during the fight: left it alive.
                foreach (var id in new List<ulong>(s.last.Keys))
                {
                    if (seen.Contains(id)) continue;
                    var prev = s.last[id];
                    s.last.Remove(id);
                    if (s.started && prev.inOrbit && prev.alive) { if (prev.attacker) s.attLeftAlive = true; else s.defLeftAlive = true; }
                }

                if (!s.started)
                {
                    if (att > 0 && def > 0)
                    {
                        s.fightStep = Step(s, now);
                        s.started = true;
                        s.endsAt = now + SiegeSeconds;
                        s.noAttackersSince = s.noDefendersSince = 0;
                        s.attKills = s.defKills = 0;
                        s.attLeftAlive = s.defLeftAlive = false;
                        s.respawning.Clear();
                        changed = true;
                        NetState.Instance?.Announce(string.Format(Localization.Extra("mpSiegeStarts3",
                            "The siege of {0} has begun: [{1}] and [{2}] may fire at each other there for 15 minutes. The most kills wins; leaving the orbit alive is surrender."),
                            place, attacker.tag, defender.tag));
                        NetNews.Post(NetNews.Kind.War, $"Fighting erupts at {NetNews.Place(s.station)}: [{NetNews.Safe(attacker.tag)}] against [{NetNews.Safe(defender.tag)}]", s.station);
                        continue;
                    }
                    // The standoff clock: one side waits there while the other has a pilot online elsewhere.
                    if (att > 0 && def == 0 && defOnline) { s.defStandoff += dt; changed = true; }
                    if (def > 0 && att == 0 && attOnline) { s.attStandoff += dt; changed = true; }
                    if (s.defStandoff >= StandoffSeconds)
                        EndSiege(s, true, string.Format(Localization.Extra("mpSiegeStandoffDef", "[{0}] kept away from {1} while the attackers waited there."), defender.tag, place));
                    else if (s.attStandoff >= StandoffSeconds)
                        EndSiege(s, false, string.Format(Localization.Extra("mpSiegeStandoffAtt", "[{0}] kept away from {1} while the defenders waited there."), attacker.tag, place));
                    else if (now >= s.expiresAt)
                    {
                        if (s.extensions < MaxExtensions)
                        {
                            s.extensions++;
                            s.expiresAt += DaySeconds;
                            changed = true;
                            NetState.Instance?.Announce(string.Format(Localization.Extra("mpSiegeExtended",
                                "No fight at {0} yet: the siege goes on for another day ({1} of {2})."), place, s.extensions, MaxExtensions));
                        }
                        else EndSiege(s, false, string.Format(Localization.Extra("mpSiegeNoFight", "Nobody fought at {0}."), place));
                    }
                    continue;
                }
                // The fight: the white flag (a side's last pilot left the orbit alive and stays gone), then the clock.
                if (att > 0) { s.noAttackersSince = 0; s.attLeftAlive = false; } else if (s.noAttackersSince == 0) { s.noAttackersSince = now; changed = true; }
                if (def > 0) { s.noDefendersSince = 0; s.defLeftAlive = false; } else if (s.noDefendersSince == 0) { s.noDefendersSince = now; changed = true; }
                if (s.defLeftAlive && s.noDefendersSince > 0 && now - s.noDefendersSince >= SiegeForfeitSeconds)
                    EndSiege(s, true, string.Format(Localization.Extra("mpSiegeWhiteFlag", "[{0}] left the fight at {1}: white flag."), defender.tag, place));
                else if (s.attLeftAlive && s.noAttackersSince > 0 && now - s.noAttackersSince >= SiegeForfeitSeconds)
                    EndSiege(s, false, string.Format(Localization.Extra("mpSiegeWhiteFlag", "[{0}] left the fight at {1}: white flag."), attacker.tag, place));
                else if (now >= s.endsAt)
                    EndSiege(s, s.attKills > s.defKills, string.Format(Localization.Extra("mpSiegeScore", "The fight at {0} is over: [{1}] {2} : {3} [{4}]."),
                                                                       place, attacker.tag, s.attKills, s.defKills, defender.tag));
            }
            if (changed) { Save(); PublishSieges(); }
        }

        /// <summary>NetState.DestroyedByRpc (checked there: a hit from the killer within 30 s): in a siege fight a pilot of one
        /// side destroying one of the other side in that orbit gets their ship repaired (hull, shield, armor), as the
        /// destroyed one respawns fresh.</summary>
        public static void OnPilotKilled(NetPlayer victim, NetPlayer killer)
        {
            if (list == null || victim == null || killer == null || NetState.Instance == null) return;
            var s = SiegeAt(victim.Station);
            if (s == null || !s.started || !killer.InSpace || killer.Station != s.station || killer.Hull <= 0f) return;
            var a = OfClient(victim.OwnerClientId);
            var b = OfClient(killer.OwnerClientId);
            if (a == null || b == null) return;
            bool opposite = (a.id == s.attacker && b.id == s.defender) || (a.id == s.defender && b.id == s.attacker);
            if (!opposite) return;
            NetState.Instance.SendAdmin(killer.OwnerClientId, NetAdmin.Order.Heal, 1, 0, 0, null, "");
        }

        /// <summary>A defending pilot docked at their besieged claim: half its remaining lapse days (rounded up) while more than
        /// LapseFloorDays are left (15 -> 8 -> 4 -> 2; 3 or less stays). True = changed.</summary>
        static bool HalveLapse(Claim held, Faction defender)
        {
            double remaining = LapseDays - DaysSince(held.lastDock);
            if (remaining <= LapseFloorDays) return false;
            double left = Math.Ceiling(remaining / 2.0);
            held.lastDock = DateTime.UtcNow.AddDays(-(LapseDays - left)).ToString("o");
            TellFaction(defender, string.Format(Localization.Extra("mpSiegeLapseHalved",
                "{0} is under siege: docking there doesn't keep the claim, it lapses in {1:0} days now. Fight for it."), StationName(held.station), left));
            return true;
        }

        /// <summary>A siege that wasn't fought out: gone, its stake back in the attacker's bank, announced ('format': {0} / {1}
        /// the defenders' tag / name, {2} the station, {3} the stake, {4} the attackers' tag).</summary>
        static void CallOff(Siege s, string format, string defenderTag, string defenderName)
        {
            list.sieges.Remove(s);
            var attacker = list.factions.Find(c => c.id == s.attacker);
            if (attacker != null) attacker.bank += Math.Max(0, s.stake);
            NetState.Instance?.Announce(string.Format(format, defenderTag, defenderName, StationName(s.station), Math.Max(0, s.stake), attacker?.tag));
            Debug.Log($"Server: the siege of {StationName(s.station)} is called off; {s.stake:N0} back to [{attacker?.tag}].");
            Save();
            PublishSieges();
        }

        /// <summary>A siege decided: 'taken' = the attackers take the station; 'reason' (optional) is announced first.</summary>
        static void EndSiege(Siege s, bool taken, string reason = null)
        {
            if (!list.sieges.Remove(s)) return;
            if (!string.IsNullOrEmpty(reason)) NetState.Instance?.Announce(reason);
            var held = ClaimAt(s.station);
            var attacker = list.factions.Find(c => c.id == s.attacker);
            var defender = list.factions.Find(c => c.id == s.defender);
            if (held != null) held.protectedUntil = UnixNow + ProtectionHours * 3600L;
            var winner = taken ? attacker : defender;
            if (winner != null) winner.bank += Math.Max(0, s.stake);   // the stake to the winning side
            if (taken && held != null && attacker != null)
            {
                held.faction = attacker.id;
                held.lastDock = DateTime.UtcNow.ToString("o");
                held.garrisonSize = 0;   // the beaten garrison is gone; the new holder sets its own
                held.garrisonLevel = 1;
                held.garrisonPaidUntil = 0;
                if (defender != null && defender.home == s.station) defender.home = list.claims.Find(c => c.faction == defender.id)?.station ?? -1;
                if (attacker.home < 0 || ClaimAt(attacker.home)?.faction != attacker.id) attacker.home = s.station;
                PublishClaims();
                if (defender != null) RefreshHomes(defender);
                RefreshHomes(attacker);
            }
            NetState.Instance?.Announce(taken
                ? string.Format(Localization.Extra("mpSiegeTaken2", "[{0}] {1} took {2} from [{3}] and wins the {4:N0} credits stake."), attacker?.tag, attacker?.name, StationName(s.station), defender?.tag, Math.Max(0, s.stake))
                : string.Format(Localization.Extra("mpSiegeHeld2", "[{0}] held {1} against [{2}] and wins the {3:N0} credits stake."), defender?.tag, StationName(s.station), attacker?.tag, Math.Max(0, s.stake)));
            Debug.Log($"Server: the siege of {StationName(s.station)} is over: {(taken ? "taken" : "held")}.");
            NetNews.Post(taken ? NetNews.Kind.Breaking : NetNews.Kind.War, taken
                ? $"{NetNews.Place(s.station)} has fallen! {NetNews.FactionName(attacker?.tag, attacker?.name)} seizes it from [{NetNews.Safe(defender?.tag)}]"
                : $"{NetNews.FactionName(defender?.tag, defender?.name)} holds {NetNews.Place(s.station)}: the [{NetNews.Safe(attacker?.tag)}] siege is broken", s.station);
            Save();
            PublishSieges();
        }

        /// <summary>The sieges to the players (NetState.Sieges): "station|attacker TAG|defender TAG|phase (0 announced, 1 armed
        /// and waiting for both sides, 2 fighting)|seconds left (to the arming, the call-off or the end)|stake|garrison
        /// alive|garrison level|station defenders|their level|attackers' kills|defenders' kills|days added" per line.</summary>
        static void PublishSieges()
        {
            if (list == null || NetState.Instance == null) return;
            var sb = new StringBuilder();
            long now = UnixNow;
            foreach (var s in list.sieges)
            {
                var a = list.factions.Find(c => c.id == s.attacker);
                var d = list.factions.Find(c => c.id == s.defender);
                if (a == null || d == null) continue;
                long left = s.started ? s.endsAt - now : s.armed ? s.expiresAt - now : s.startsAt - now;
                sb.Append(s.station).Append('|').Append(a.tag).Append('|').Append(d.tag).Append('|').Append(s.started ? 2 : s.armed ? 1 : 0)
                  .Append('|').Append(Math.Max(0, left)).Append('|').Append(Math.Max(0, s.stake))
                  .Append('|').Append(s.armed ? s.garrisonAlive : 0).Append('|').Append(s.garrisonLevel)
                  .Append('|').Append(s.armed ? DefenderCount(Step(s, now)) : 0).Append('|').Append(DefenderLevel(Step(s, now)))
                  .Append('|').Append(s.attKills).Append('|').Append(s.defKills).Append('|').Append(s.extensions).Append('\n');
            }
            NetState.Instance.SetSieges(sb.ToString());
        }

        // ---- the garrison ----------------------------------------------------------------------------------

        /// <summary>"/faction garrison [ships level] [station]": the claim's garrison (officers), or what it is now. The station is
        /// the one docked at unless given (any of the faction's claims, by number). Setting it pays the new upkeep's first day.</summary>
        static string SetGarrison(ulong client, string me, string arg)
        {
            var faction = Of(me);
            if (faction == null) return Localization.Extra("mpFactionNone", "You aren't in a faction.");
            var words = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int station = DockedAt(client);
            if (words.Length >= 3 && int.TryParse(words[2], out int given)) station = given;
            var held = ClaimAt(station);
            if (held == null || held.faction != faction.id) return Localization.Extra("mpGarrisonWhere", "Dock at one of your faction's stations, or name one: /faction garrison <ships> <level> <station>.");
            if (words.Length == 0)
                return string.Format(Localization.Extra("mpGarrisonInfo", "{0}: {1} fighters at level {2}, {3:N0} credits a day."),
                                     StationName(station), held.garrisonSize, held.garrisonLevel, GarrisonCost(held.garrisonSize, held.garrisonLevel));
            if (!IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            if (words.Length < 2 || !int.TryParse(words[0], out int ships) || !int.TryParse(words[1], out int level)
                || ships < 0 || ships > MaxGarrison || level < 1 || level > MaxGarrisonLevel)
                return string.Format(Localization.Extra("mpGarrisonUsage", "/faction garrison <ships 0-{0}> <level 1-{1}> [station]"), MaxGarrison, MaxGarrisonLevel);
            var siege = SiegeAt(station);
            if (siege != null && siege.armed) return Localization.Extra("mpGarrisonSiege", "The garrison can't change while the siege runs.");
            int cost = GarrisonCost(ships, level);
            long now = UnixNow;
            if (ships > 0 && (ships > held.garrisonSize || level > held.garrisonLevel || now >= held.garrisonPaidUntil))
            {
                if (faction.bank < cost)
                    return string.Format(Localization.Extra("mpGarrisonCost", "That garrison costs {0:N0} credits a day, paid now from the bank (it has {1:N0})."), cost, faction.bank);
                faction.bank -= cost;
                held.garrisonPaidUntil = now + DaySeconds;
            }
            held.garrisonSize = ships;
            held.garrisonLevel = level;
            Save();
            TellFaction(faction, ships == 0
                ? string.Format(Localization.Extra("mpGarrisonNone", "{0} has no garrison now."), StationName(station))
                : string.Format(Localization.Extra("mpGarrisonSet", "{0}'s garrison: {1} fighters at level {2}, {3:N0} credits a day."), StationName(station), ships, level, cost));
            return "";
        }

        /// <summary>Claim tick: each garrison's day of upkeep from the bank when its paid day is over; disbanded without it.</summary>
        static bool TickUpkeep()
        {
            bool changed = false;
            long now = UnixNow;
            foreach (var c in list.claims)
            {
                if (c.garrisonSize <= 0 || now < c.garrisonPaidUntil) continue;
                var faction = list.factions.Find(x => x.id == c.faction);
                if (faction == null) continue;
                int cost = GarrisonCost(c.garrisonSize, c.garrisonLevel);
                changed = true;
                if (faction.bank >= cost)
                {
                    faction.bank -= cost;
                    c.garrisonPaidUntil = Math.Max(c.garrisonPaidUntil, now - DaySeconds) + DaySeconds;
                    continue;
                }
                c.garrisonSize = 0;
                TellFaction(faction, string.Format(Localization.Extra("mpGarrisonUnpaid", "The bank couldn't pay {0}'s garrison ({1:N0} credits a day): it left."), StationName(c.station), cost));
            }
            return changed;
        }

        /// <summary>NetState.GarrisonKillRpc: the orbit's authority saw one of 'station''s garrison die. Checked: the siege runs,
        /// a fighter is alive, the sender runs that orbit and is there; at most GarrisonKillsPerWindow in GarrisonKillWindow.</summary>
        public static void OnGarrisonKill(ulong client, int station)
        {
            var s = SiegeAt(station);
            if (s == null || !s.armed || s.garrisonAlive <= 0) return;
            var from = NetSquad.Find(client);
            if (from == null || !from.OrbitAuthority || !NetGuard.InOrbit(client, station)) return;
            float t = Time.realtimeSinceStartup;
            if (t - s.killWindowStart > GarrisonKillWindow) { s.killWindowStart = t; s.killsInWindow = 0; }
            if (++s.killsInWindow > GarrisonKillsPerWindow) return;
            s.garrisonAlive--;
            s.garrisonBack.Add(UnixNow + GarrisonRespawnSeconds);
            Save();
            PublishSieges();
        }

        // ---- the station's toll and the trade tax (phase 3) ---------------------------------------------

        /// <summary>NetState.TollPaidRpc: a player of another faction paid the toll at 'station' (their game took the credits):
        /// into the holder's bank.</summary>
        public static void OnTollPaid(ulong client, int station, int amount)
        {
            var held = ClaimAt(station);
            string account = NetProfiles.AccountOf(client);
            var payer = Of(account);
            if (held == null || account == null || payer == null || payer.id == held.faction || Toll == 0) return;
            var owner = list.factions.Find(c => c.id == held.faction);
            if (owner == null) return;
            // A fixed toll is the server's own amount; a percentage is what the pilot's game says it paid, at most that share
            // of the profile's recorded worth (its credits are part of it) and at least 1.
            int paid = Toll > 0 ? Toll : Mathf.Clamp(amount, 1, TollFor(Toll, NetProfiles.WorthOf(account)));
            owner.bank += paid;
            NetProfiles.AdjustWorth(account, -paid);
            Save();
            TellFaction(owner, string.Format(Localization.Extra("mpTollReceived", "[{0}] {1} paid the {2:N0} credits toll at {3}."), payer.tag, NameOf(account), paid, StationName(station)));
        }

        /// <summary>NetState.StockItemRpc: 'units' of 'item' bought at 'station' for 'price' each that carry VAT (not resold
        /// stock): the rate's share of what a non-member paid into the holder's bank. The price is the buyer's game's, capped
        /// at the item's highest list price with the markup.</summary>
        public static void OnPurchase(ulong client, int station, int item, int price, int units)
        {
            var held = ClaimAt(station);
            int rate = VatAt(station);
            if (held == null || price <= 0 || units <= 0 || rate <= 0) return;
            var buyer = OfClient(client);
            if (buyer != null && buyer.id == held.faction) return;   // members: their discount, no VAT
            var owner = list.factions.Find(c => c.id == held.faction);
            var it = NetGame.Db?.Item(item);
            if (owner == null || it == null) return;
            long paid = Math.Min(price, VatMarkup(it.maxPrice, rate)) * (long)units;
            long vat = (long)Math.Round(paid / (1.0 + rate / 1000.0) * rate / 100.0);
            if (vat <= 0) return;
            owner.bank += vat;
            taxDirty = true;
        }

        /// <summary>NetState.StockShipRpc: a dealer ship bought at 'station' (the reserved row taken): the VAT of a non-member's
        /// purchase from the ship's list price (the server's own number).</summary>
        public static void OnShipBought(ulong client, int station, int ship)
        {
            var held = ClaimAt(station);
            int rate = VatAt(station);
            var s = NetGame.Db?.Ship(ship);
            if (held == null || rate <= 0 || s == null || s.price <= 0) return;
            var buyer = OfClient(client);
            if (buyer != null && buyer.id == held.faction) return;
            var owner = list.factions.Find(c => c.id == held.faction);
            if (owner == null) return;
            owner.bank += (long)Math.Round(s.price * rate / 100.0);
            taxDirty = true;
        }

        /// <summary>"/faction vat [rate] [station]": the claim's VAT rate (officers), or what it is now. The station is the one
        /// docked at unless given (any of the faction's claims, by number). A new rate thins the goods from the next stock roll.</summary>
        static string SetVat(ulong client, string me, string arg)
        {
            var faction = Of(me);
            if (faction == null) return Localization.Extra("mpFactionNone", "You aren't in a faction.");
            var words = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int station = DockedAt(client);
            if (words.Length >= 2 && int.TryParse(words[1], out int given)) station = given;
            var held = ClaimAt(station);
            if (held == null || held.faction != faction.id) return Localization.Extra("mpVatWhere", "Dock at one of your faction's stations, or name one: /faction vat <rate> <station>.");
            if (words.Length == 0)
                return string.Format(Localization.Extra("mpVatInfo", "{0}: VAT {1}%, {2}% fewer goods."), StationName(station), held.vat, held.vat);
            if (!IsOfficer(faction, me)) return Localization.Extra("mpFactionNotOfficer", "Only a faction's leader and officers can do that.");
            if (!int.TryParse(words[0].TrimEnd('%'), out int rate) || rate < 0 || rate > MaxVat)
                return string.Format(Localization.Extra("mpVatUsage", "/faction vat <0-{0}> [station]"), MaxVat);
            held.vat = rate;
            Save();
            PublishClaims();
            TellFaction(faction, string.Format(Localization.Extra("mpVatSet", "{0}'s VAT is {1}% now: its goods thin to {2}% from the next restock."),
                                               StationName(station), rate, 100 - rate));
            return "";
        }

        static bool taxDirty;

        // ---- the station window (NetPanel) -----------------------------------------------------------------

        /// <summary>The player's faction, invitations, the station they are docked at and every faction into the snapshot.</summary>
        internal static void FillPanel(ulong client, NetPanel.State s)
        {
            if (list == null) return;
            string me = NetProfiles.AccountOf(client);
            s.claimCost = ClaimCost; s.siegeCost = SiegeCost; s.maxClaims = MaxClaims; s.toll = Toll;
            s.sieges = SiegesText();
            foreach (var c in list.factions)
                s.factions.Add(new NetPanel.FactionRow { tag = c.tag, name = c.name, members = c.members.Count, claims = list.claims.FindAll(x => x.faction == c.id).Count });
            if (s.dockedStation >= 0)
            {
                var held = ClaimAt(s.dockedStation);
                s.stationHolder = held != null ? list.factions.Find(c => c.id == held.faction)?.tag ?? "" : "";
                var st = NetGame.Db.Stations.Find(x => x.index == s.dockedStation);
                s.stationClaimable = st != null && s.dockedStation != KaamoStation && st.system != LomaSystem;
            }
            if (me != null && invites.TryGetValue(me, out var inv) && inv.until > Time.realtimeSinceStartup)
            {
                var from = list.factions.Find(c => c.id == inv.faction);
                if (from != null) s.factionInvites.Add(from.tag + "|" + from.name);
            }
            var faction = Of(me);
            if (faction == null) return;
            s.inFaction = true;
            s.factionTag = faction.tag;
            s.factionName = faction.name;
            s.bank = faction.bank;
            s.home = HomeOf(me);
            s.rank = faction.leader == me ? 2 : faction.officers.Contains(me) ? 1 : 0;
            foreach (var m in faction.members)
                s.members.Add(new NetPanel.Member { name = NameOf(m), rank = faction.leader == m ? 2 : faction.officers.Contains(m) ? 1 : 0, online = NetProfiles.IsOnline(m) });
            s.members.Sort((a, b) => a.rank != b.rank ? b.rank.CompareTo(a.rank) : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            foreach (var c in list.claims)
                if (c.faction == faction.id)
                    s.claims.Add(new NetPanel.ClaimRow { station = c.station, name = StationName(c.station), home = c.station == s.home,
                                                         daysLeft = (float)Math.Max(0, LapseDays - DaysSince(c.lastDock)), sieged = SiegeAt(c.station) != null,
                                                         garrisonSize = c.garrisonSize, garrisonLevel = Mathf.Clamp(c.garrisonLevel, 1, MaxGarrisonLevel),
                                                         vat = Mathf.Clamp(c.vat, 0, MaxVat) });
        }

        // ---- the server console -----------------------------------------------------------------------------

        /// <summary>DedicatedServer's "factions" command.</summary>
        public static string ConsoleList()
        {
            if (list == null) return "Factions need player profiles (-noprofiles is set).";
            if (list.factions.Count == 0) return "No factions.";
            var sb = new StringBuilder($"{list.factions.Count} faction(s):");
            foreach (var c in list.factions)
                sb.Append($"\n  [{c.tag}] {c.name}: {c.members.Count} member(s), leader {NameOf(c.leader)}, bank {c.bank:N0}");
            return sb.ToString();
        }

        /// <summary>DedicatedServer's "sieges" command.</summary>
        public static string ConsoleSieges() => list == null ? "Factions need player profiles (-noprofiles is set)." : SiegesText();

        /// <summary>DedicatedServer's "faction disband TAG".</summary>
        public static string ConsoleDisband(string tag)
        {
            var faction = ByTag(tag);
            if (faction == null) return $"No faction with the tag {tag}.";
            return Disband(faction.leader) == "" ? $"Disbanded [{faction.tag}] {faction.name}." : "Could not disband it.";
        }
    }
}
