// NetArena.cs
// Remake-only: player-versus-player arena matches, run by the session's server (a dedicated server or a player host).
// Outside a match the players can't hurt each other (free roam is player-versus-environment; -freepvp on a dedicated
// server keeps the old free-for-all everywhere, NetGame.FreePvp). Matches are fought in a private copy of an orbit: every
// match has its own orbit id (OrbitId, far above the station indices), so the "same orbit" checks of the multiplayer
// code (NetPlayer.SharesOrbit, the proxies, local chat, the asteroids) keep a match to its players. The level built
// for it is the Void's home orbit (the alien orbit, Session.VoidOrbit: its violet sky and fog, Void crystal asteroids
// around the centre for cover) without the Void station, docking, jumps or mining (NetArenaClient, SpaceLevel's arena
// mode). Normally empty; with the "voids" option the orbit's own Void fighters are there too, hostile to everyone, and
// the dead ones come back every 45 s (Traffic.UpdateAlienAttackers): run by the first player there like any orbit's
// NPCs (NetOrbit). A Void kill isn't a point; dying to one is a respawn.
//   Duel (1v1): "/duel <name> [voids]" challenges a player (both docked); "/accept" or "/decline" within 60 s. First
//     to DuelKills kills or DuelSeconds, the more kills wins (a tie is a draw).
//   Free-for-all: "/ffa [voids]" joins that queue (docked; with and without Voids are separate queues); the match
//     starts FfaWaitSeconds after the second player joins, or at once with FfaMaxPlayers. First to FfaKills kills or
//     FfaSeconds.
//   "/leave" quits a queue or a match (a duel is then lost); "/arena" says what runs; "/top" the profiles' leaderboard.
// A match: every player's game loads the arena (ArenaStartRpc) and reports in (ArenaReadyRpc); once all are in (or
// after ReadySeconds) a CountdownSeconds countdown runs, then the fight. A kill is the victim's game reporting it
// (NetState.DestroyedByRpc): the killer scores, the victim respawns at another spawn point (its game reloads the
// arena). Nothing is at stake: the equipment and ammo are restored at every respawn and afterwards, and nothing of the
// match is saved to the profile but the stats (NetProfiles.AddArenaStats: kills, deaths, wins).
// The end: everyone gets the result (ArenaEndRpc) and goes back docked where they came from. A player leaving or
// disconnecting is out; a duel is won by the one who stays, a free-for-all ends with fewer than 2 players.

using System;
using System.Collections.Generic;
using System.Text;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetArena
    {
        public enum Kind : byte { Duel = 0, FreeForAll = 1 }
        public enum Phase : byte { Waiting = 0, Loading = 1, Countdown = 2, Fighting = 3, Over = 4 }

        public const int DuelKills = 3, FfaKills = 15, FfaMaxPlayers = 8;
        public const float DuelSeconds = 300f, FfaSeconds = 600f;
        const float ChallengeSeconds = 60f, FfaWaitSeconds = 30f, ReadySeconds = 20f, CountdownSeconds = 5f;

        /// <summary>Arena orbit ids start here (the stations are 0..~130, the Void -1).</summary>
        public const int OrbitBase = 100000;

        /// <summary>The orbit every arena is a copy of: the Void's home (the alien orbit).</summary>
        public const int Template = Session.VoidOrbit;

        /// <summary>The chat option that brings the Void fighters into a match.</summary>
        const string VoidsWord = "voids";

        public static bool IsArenaOrbit(int orbit) => orbit >= OrbitBase;

        class Match
        {
            public int id;
            public Kind kind;
            public bool voids;   // the alien orbit's Void fighters fight everyone
            public Phase phase;
            public readonly List<ulong> players = new List<ulong>();
            public readonly Dictionary<ulong, int> kills = new Dictionary<ulong, int>(), deaths = new Dictionary<ulong, int>();
            public readonly HashSet<ulong> ready = new HashSet<ulong>();
            public float phaseUntil;   // real time: the queue's start, the loading timeout, the countdown's end, the time limit
            public int OrbitId => OrbitBase + id;
            public int KillLimit => kind == Kind.Duel ? DuelKills : FfaKills;
            public float Seconds => kind == Kind.Duel ? DuelSeconds : FfaSeconds;
        }

        static readonly List<Match> matches = new List<Match>();
        static readonly Dictionary<ulong, (ulong from, bool voids, float until)> challenges = new Dictionary<ulong, (ulong, bool, float)>();   // by the challenged
        static int nextId = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        /// <summary>A new session: no matches.</summary>
        public static void Reset()
        {
            matches.Clear();
            challenges.Clear();
            nextId = 1;
        }

        static float Now => Time.realtimeSinceStartup;

        static Match MatchOf(ulong client) => matches.Find(m => m.players.Contains(client));

        /// <summary>The player is in an arena queue or match (/leave leaves that before the squad, NetCommands).</summary>
        internal static bool InMatch(ulong client) => MatchOf(client) != null;

        static string Name(ulong client) { var p = NetSquad.Find(client); return p != null ? p.DisplayName : $"client {client}"; }

        // ---- chat commands ----------------------------------------------------------------------------------

        /// <summary>NetState.SendChatRpc: an arena command's answer to its sender; null = not an arena command.</summary>
        public static string Command(ulong client, string text)
        {
            var words = text.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = words.Length > 0 ? words[0].ToLowerInvariant() : "";
            string rest = words.Length > 1 ? words[1].Trim() : "";
            switch (cmd)
            {
                case "/duel": { bool voids = TakeVoids(ref rest); return Challenge(client, rest, voids); }
                case "/accept": return Accept(client);
                case "/decline": return Decline(client);
                case "/ffa": return JoinFfa(client, TakeVoids(ref rest));
                case "/leave": return Leave(client, true);
                case "/arena": case "/arenas": return Status(client);
                case "/top": return NetProfiles.Leaderboard();
                default: return null;
            }
        }

        /// <summary>A trailing "voids" option (taken off the text): the match has the Void fighters.</summary>
        static bool TakeVoids(ref string rest)
        {
            if (string.Equals(rest, VoidsWord, StringComparison.OrdinalIgnoreCase)) { rest = ""; return true; }
            if (!rest.EndsWith(" " + VoidsWord, StringComparison.OrdinalIgnoreCase)) return false;
            rest = rest.Substring(0, rest.Length - VoidsWord.Length).Trim();
            return true;
        }

        static string WithVoids(bool voids) => voids ? Localization.Extra("mpArenaWithVoids", " with the Void fighters") : "";

        /// <summary>Why this player can't start or join a match now (null = they can).</summary>
        static string CantJoin(ulong client)
        {
            var p = NetSquad.Find(client);
            if (p == null) return Localization.Extra("mpArenaNotHere", "Not in the session yet.");
            if (MatchOf(client) != null) return Localization.Extra("mpArenaAlready", "You are in a match already: /leave first.");
            if (p.Observer) return Localization.Extra("mpArenaObserver", "This device only watches your profile.");
            if (!p.InHangar) return Localization.Extra("mpArenaDocked", "Matches start from a station: dock first.");
            return null;
        }

        static string Challenge(ulong client, string who, bool voids)
        {
            if (who.Length == 0) return Localization.Extra("mpDuelUsage", "/duel <pilot name> [voids]");
            if (CantJoin(client) is string why) return why;
            NetPlayer target = null;
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsSpawned && p.OwnerClientId != client && string.Equals(p.DisplayName, who, StringComparison.OrdinalIgnoreCase)) { target = p; break; }
            if (target == null) return string.Format(Localization.Extra("mpDuelNobody", "No pilot called \"{0}\" here."), who);
            if (CantJoin(target.OwnerClientId) != null)
                return string.Format(Localization.Extra("mpDuelBusy", "{0} can't fight now (in a match, or not docked)."), target.DisplayName);
            challenges[target.OwnerClientId] = (client, voids, Now + ChallengeSeconds);
            NetState.Instance?.Notify(target.OwnerClientId, string.Format(Localization.Extra("mpDuelChallenged",
                "{0} challenges you to a duel (first to {1} kills{2}): type /accept or /decline."), Name(client), DuelKills, WithVoids(voids)));
            return string.Format(Localization.Extra("mpDuelSent", "Challenge sent to {0}."), target.DisplayName);
        }

        static string Accept(ulong client)
        {
            if (!challenges.TryGetValue(client, out var c) || c.until < Now) { challenges.Remove(client); return Localization.Extra("mpDuelNone", "Nobody has challenged you."); }
            challenges.Remove(client);
            if (CantJoin(client) is string why) return why;
            if (CantJoin(c.from) != null) return string.Format(Localization.Extra("mpDuelGone", "{0} can't fight any more."), Name(c.from));
            var m = new Match { id = nextId++, kind = Kind.Duel, voids = c.voids };
            m.players.Add(c.from);
            m.players.Add(client);
            matches.Add(m);
            Start(m);
            return "";
        }

        static string Decline(ulong client)
        {
            if (!challenges.TryGetValue(client, out var c)) return Localization.Extra("mpDuelNone", "Nobody has challenged you.");
            challenges.Remove(client);
            NetState.Instance?.Notify(c.from, string.Format(Localization.Extra("mpDuelDeclined", "{0} declined the duel."), Name(client)));
            return Localization.Extra("mpDuelDeclinedYou", "Duel declined.");
        }

        static string JoinFfa(ulong client, bool voids)
        {
            if (CantJoin(client) is string why) return why;
            var m = matches.Find(x => x.kind == Kind.FreeForAll && x.voids == voids && x.phase == Phase.Waiting && x.players.Count < FfaMaxPlayers);
            if (m == null) { m = new Match { id = nextId++, kind = Kind.FreeForAll, voids = voids, phase = Phase.Waiting }; matches.Add(m); }
            m.players.Add(client);
            if (m.players.Count == 2) m.phaseUntil = Now + FfaWaitSeconds;
            foreach (var p in m.players)
                if (p != client) NetState.Instance?.Notify(p, string.Format(Localization.Extra("mpFfaJoined", "{0} joined the free-for-all ({1} / {2})."), Name(client), m.players.Count, FfaMaxPlayers));
            if (m.players.Count >= FfaMaxPlayers) { Start(m); return ""; }
            return m.players.Count < 2
                ? string.Format(Localization.Extra("mpFfaWaiting", "In the free-for-all queue{0}: it starts 30 s after a second pilot joins (/leave to quit)."), WithVoids(voids))
                : string.Format(Localization.Extra("mpFfaSoon", "In the free-for-all queue{3} ({0} / {1}): it starts in {2:0} s."), m.players.Count, FfaMaxPlayers, Mathf.Max(0f, m.phaseUntil - Now), WithVoids(voids));
        }

        static string Status(ulong client)
        {
            var sb = new StringBuilder();
            foreach (var m in matches)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(m.kind == Kind.Duel ? "Duel" : "Free-for-all").Append(m.voids ? " with Voids" : "").Append($" #{m.id} ({m.phase}): ");
                for (int i = 0; i < m.players.Count; i++)
                    sb.Append(i > 0 ? ", " : "").Append(Name(m.players[i])).Append(' ').Append(m.kills.TryGetValue(m.players[i], out int k) ? k : 0);
            }
            return sb.Length > 0 ? sb.ToString() : Localization.Extra("mpArenaNoMatches", "No matches. /duel <name> or /ffa to start one.");
        }

        /// <summary>The player leaves their queue or match ('own': by their /leave; else a disconnect).</summary>
        static string Leave(ulong client, bool own)
        {
            challenges.Remove(client);
            var m = MatchOf(client);
            if (m == null) return own ? Localization.Extra("mpArenaNotIn", "You aren't in a queue or a match.") : null;
            m.players.Remove(client);
            bool fighting = m.phase != Phase.Waiting;
            if (fighting && own) NetState.Instance?.ArenaLeave(client);   // their game goes home
            foreach (var p in m.players)
                NetState.Instance?.Notify(p, string.Format(Localization.Extra("mpArenaLeft", "{0} left the match."), Name(client)));
            if (m.phase == Phase.Waiting)
            {
                if (m.players.Count == 0) matches.Remove(m);
                else if (m.players.Count < 2) m.phaseUntil = 0f;
            }
            else if (m.players.Count < 2) End(m, m.kind == Kind.Duel && m.players.Count == 1 ? m.players[0] : (ulong?)null, true);
            else Broadcast(m);
            return own ? Localization.Extra("mpArenaYouLeft", "You left.") : null;
        }

        // ---- the match's course -----------------------------------------------------------------------------

        static void Start(Match m)
        {
            m.phase = Phase.Loading;
            m.phaseUntil = Now + ReadySeconds;
            m.ready.Clear();
            foreach (var p in m.players) { m.kills[p] = 0; m.deaths[p] = 0; }
            for (int i = 0; i < m.players.Count; i++)
                NetState.Instance?.ArenaStart(m.players[i], m.id, (byte)m.kind, m.OrbitId, Template, m.voids, i, m.KillLimit, m.Seconds);
            Debug.Log($"Server: {(m.kind == Kind.Duel ? "duel" : "free-for-all")} #{m.id} starts: {string.Join(", ", m.players.ConvertAll(Name))}.");
        }

        /// <summary>NetState.ArenaReadyRpc: the player's game has the arena loaded.</summary>
        public static void OnReady(ulong client, int matchId)
        {
            var m = MatchOf(client);
            if (m == null || m.id != matchId || m.phase != Phase.Loading) return;
            m.ready.Add(client);
            if (m.ready.Count >= m.players.Count) BeginCountdown(m);
        }

        static void BeginCountdown(Match m)
        {
            m.phase = Phase.Countdown;
            m.phaseUntil = Now + CountdownSeconds;
            Broadcast(m);
        }

        /// <summary>NetState.DestroyedByRpc: a player's ship was destroyed by another player.</summary>
        public static void OnKill(ulong victim, ulong killer)
        {
            var m = MatchOf(victim);
            if (m == null || m.phase != Phase.Fighting || !m.players.Contains(killer) || killer == victim) return;
            m.kills[killer] = m.kills.TryGetValue(killer, out int k) ? k + 1 : 1;
            m.deaths[victim] = m.deaths.TryGetValue(victim, out int d) ? d + 1 : 1;
            Broadcast(m, string.Format(Localization.Extra("mpArenaKill", "{0} destroyed {1}."), Name(killer), Name(victim)));
            if (m.kills[killer] >= m.KillLimit) End(m, killer, false);
        }

        /// <summary>NetState.Update (server): queues, loading, countdowns and time limits.</summary>
        public static void Tick()
        {
            float now = Now;
            if (challenges.Count > 0)
                foreach (var key in new List<ulong>(challenges.Keys)) if (challenges[key].until < now) challenges.Remove(key);
            foreach (var m in new List<Match>(matches))
                switch (m.phase)
                {
                    case Phase.Waiting:
                        if (m.players.Count >= 2 && now >= m.phaseUntil) Start(m);
                        break;
                    case Phase.Loading:
                        if (now >= m.phaseUntil)
                        {
                            // The ones who never loaded are out (sent home, should their arena load later); the rest fight if
                            // there are two.
                            foreach (var p in new List<ulong>(m.players)) if (!m.ready.Contains(p)) Leave(p, true);
                            if (matches.Contains(m) && m.phase == Phase.Loading) BeginCountdown(m);
                        }
                        break;
                    case Phase.Countdown:
                        if (now >= m.phaseUntil) { m.phase = Phase.Fighting; m.phaseUntil = now + m.Seconds; Broadcast(m); }
                        break;
                    case Phase.Fighting:
                        if (now >= m.phaseUntil) End(m, Leader(m), false);
                        break;
                }
        }

        /// <summary>The player with the most kills, null on a tie.</summary>
        static ulong? Leader(Match m)
        {
            ulong? best = null;
            int top = -1;
            bool tie = false;
            foreach (var p in m.players)
            {
                int k = m.kills.TryGetValue(p, out int v) ? v : 0;
                if (k > top) { top = k; best = p; tie = false; }
                else if (k == top) tie = true;
            }
            return tie ? null : best;
        }

        static void End(Match m, ulong? winner, bool forfeit)
        {
            m.phase = Phase.Over;
            matches.Remove(m);
            string result = winner.HasValue
                ? string.Format(forfeit ? Localization.Extra("mpArenaWonForfeit", "{0} wins: the others left.") : Localization.Extra("mpArenaWon", "{0} wins!"), Name(winner.Value))
                : Localization.Extra("mpArenaDraw", "A draw.");
            var sb = new StringBuilder(result);
            var order = new List<ulong>(m.players);
            order.Sort((a, b) => (m.kills.TryGetValue(b, out int kb) ? kb : 0).CompareTo(m.kills.TryGetValue(a, out int ka) ? ka : 0));
            foreach (var p in order)
                sb.Append($"\n{Name(p)}: {(m.kills.TryGetValue(p, out int k) ? k : 0)} / {(m.deaths.TryGetValue(p, out int d) ? d : 0)}");
            foreach (var p in m.players)
            {
                NetProfiles.AddArenaStats(p, m.kills.TryGetValue(p, out int k) ? k : 0, m.deaths.TryGetValue(p, out int d) ? d : 0, winner == p);
                NetState.Instance?.ArenaEnd(p, m.id, sb.ToString());
            }
            Debug.Log($"Server: {(m.kind == Kind.Duel ? "duel" : "free-for-all")} #{m.id} over: {result.Replace('\n', ' ')}");
            if (winner.HasValue && !forfeit) PostResult(m, winner.Value);
        }

        /// <summary>The news (NetNews): a duel's winner and score, a free-for-all's winner.</summary>
        static void PostResult(Match m, ulong winner)
        {
            int Kills(ulong p) => m.kills.TryGetValue(p, out int k) ? k : 0;
            string where = m.voids ? "the Void arena, Void fighters and all" : "the Void arena";
            if (m.kind == Kind.Duel)
            {
                ulong loser = m.players.Find(p => p != winner);
                if (loser == winner || !m.players.Contains(loser)) return;
                NetNews.Post(NetNews.Kind.Arena, $"{NetNews.Safe(Name(winner))} defeats {NetNews.Safe(Name(loser))} {Kills(winner)}–{Kills(loser)} in a duel in {where}");
            }
            else
                NetNews.Post(NetNews.Kind.Arena, $"{NetNews.Safe(Name(winner))} wins a {m.players.Count}-pilot free-for-all in {where} with {Kills(winner)} kills");
        }

        /// <summary>The match's state to its players (the HUD): phase, seconds left, the players with their kills.</summary>
        static void Broadcast(Match m, string feed = "")
        {
            var ids = m.players.ToArray();
            var kills = Array.ConvertAll(ids, p => m.kills.TryGetValue(p, out int k) ? k : 0);
            float left = Mathf.Max(0f, m.phaseUntil - Now);
            foreach (var p in ids) NetState.Instance?.ArenaState(p, m.id, (byte)m.phase, left, ids, kills, feed ?? "");
        }

        /// <summary>The player's duel challenge, queue and the matches into the station window's snapshot.</summary>
        internal static void FillPanel(ulong client, NetPanel.State s)
        {
            if (challenges.TryGetValue(client, out var c) && c.until > Now) { s.duelFrom = Name(c.from); s.duelVoids = c.voids; }
            var m = MatchOf(client);
            if (m != null && m.phase == Phase.Waiting)
            {
                s.queue = m.voids ? 2 : 1;
                s.queueCount = m.players.Count;
                s.queueStartsIn = m.players.Count >= 2 ? Mathf.Max(0f, m.phaseUntil - Now) : -1f;
            }
            s.inMatch = m != null && m.phase != Phase.Waiting;
            foreach (var x in matches)
            {
                var sb = new StringBuilder(x.kind == Kind.Duel ? "Duel" : "Free-for-all");
                if (x.voids) sb.Append(" with Voids");
                sb.Append(x.phase == Phase.Waiting ? $" (queue, {x.players.Count} / {FfaMaxPlayers})" : $" ({x.phase})").Append(": ");
                for (int i = 0; i < x.players.Count; i++)
                    sb.Append(i > 0 ? ", " : "").Append(Name(x.players[i])).Append(x.phase == Phase.Waiting ? "" : $" {(x.kills.TryGetValue(x.players[i], out int k) ? k : 0)}");
                s.matches.Add(sb.ToString());
            }
        }

        /// <summary>NetGame: a player disconnected.</summary>
        public static void OnDisconnect(ulong client) => Leave(client, false);

        /// <summary>DedicatedServer's "arenas" command.</summary>
        public static string ConsoleList() => Status(0);
    }
}
