// NetModeration.cs
// Remake-only: moderation on a dedicated server with profiles (NetProfiles). Roles are kept on the profile:
//   master (3) the server's owner: everything below, plus making / removing admins (/admin, /unadmin) and deleting
//              profiles (/deleteprofile). Claimed in the game with the server's admin token ("/claimadmin TOKEN"), so a
//              server needs no console: the token comes from -admintoken (or the GOF2_ADMIN_TOKEN environment variable),
//              else admin_token.txt beside the profiles, made at the first start; every start logs it. Delete the file
//              for a new one. The console can also make masters ("master <pilot|profile>");
//   admin (2)  everything below, plus permanent bans, making / removing ops (/op, /deop), announcements (/say), ending
//              factions (/disband TAG);
//   op (1)     kicks with a cooldown and temporary bans (at most MaxOpBanMinutes), unbans, the ban list;
//   player (0).
// Nobody acts on a pilot of their own role or higher, or gives a role as high as their own (the console does anything).
// Chat commands (answered privately; the result is announced to everyone):
//   /kick <pilot> [minutes] [reason]     drops the pilot; they can't come back for 'minutes' (default KickMinutes)
//   /tempban <pilot> <minutes> [reason]  the same as a ban that ends
//   /ban <pilot> [reason]                admin: for good
//   /unban <pilot|profile id>            /bans the list
//   /op <pilot>, /deop <pilot>           admin; /admin <pilot>, /unadmin <pilot> master
//   /say <text>, /disband <TAG>          admin; /deleteprofile <profile id> master
//   /settings, /set <key> <value>        admin: the server's settings while it runs (NetServerSettings)
//   /claimadmin <token>                  anyone with a profile: becomes a master
//   /staff                               who the masters, admins and ops are
// A pilot is a name online, "#<client id>" (the window's buttons), or a profile's last name / id for one who is offline.
// A ban holds the profile and every device it signed in from (the device labels), so a banned player can't come back
// with a new profile from the same device; it is checked when a game signs in (NetProfiles.OnLogin) and the game is
// dropped with the reason and the time left. Stored in bans.json beside the profiles (written like them); bans that ran
// out are dropped. Console: kick <id|name> [minutes] [reason], ban, tempban, unban, bans, op, deop, admin, unadmin, master,
// unmaster, staff. The station window's Admin tab (UI.MultiplayerWindow) has buttons for all of it (NetPanel: the staff, every
// profile and the server's status for admins).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetModeration
    {
        public const int Player = 0, Op = 1, Admin = 2, Master = 3, ServerConsole = 4;
        public const string TokenEnvironment = "GOF2_ADMIN_TOKEN";

        static string token;

        public static string RoleName(int role) => role >= Master ? "master" : role == Admin ? "admin" : role == Op ? "op" : "player";
        public const int KickMinutes = 5, MaxOpBanMinutes = 24 * 60;

        [Serializable]
        public class Ban
        {
            public string account = "", name = "", reason = "", by = "", at = "";
            public List<string> devices = new List<string>();
            public long until;   // Unix seconds, 0 = for good
        }

        [Serializable] class BanList { public List<Ban> bans = new List<Ban>(); }

        static BanList list;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { list = null; token = null; }

        static string PathOf => Path.Combine(NetProfiles.Folder, "bans.json");
        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>NetProfiles.Start: the bans loaded.</summary>
        public static void Load()
        {
            try { list = File.Exists(PathOf) ? JsonUtility.FromJson<BanList>(File.ReadAllText(PathOf)) : null; }
            catch (Exception e) { Debug.LogError($"NetModeration: bans.json unreadable ({e.Message}); starting a new list."); list = null; }
            if (list == null) list = new BanList();
            if (list.bans == null) list.bans = new List<Ban>();
            Prune();
            LoadToken();
        }

        /// <summary>WebAdmin without player profiles (Load never ran): the admin token all the same, for the web login.</summary>
        internal static void EnsureToken()
        {
            if (token != null) return;
            try { if (!string.IsNullOrEmpty(NetProfiles.Folder)) Directory.CreateDirectory(NetProfiles.Folder); } catch (Exception) { }
            LoadToken();
        }

        /// <summary>The admin token: -admintoken, GOF2_ADMIN_TOKEN, else admin_token.txt (made once, random).</summary>
        static void LoadToken()
        {
            token = NetGame.CommandLineValue("-admintoken");
            if (string.IsNullOrWhiteSpace(token)) token = Environment.GetEnvironmentVariable(TokenEnvironment);
            string file = Path.Combine(NetProfiles.Folder, "admin_token.txt");
            if (string.IsNullOrWhiteSpace(token))
            {
                try { if (File.Exists(file)) token = File.ReadAllText(file).Trim(); } catch (Exception) { }
                if (string.IsNullOrWhiteSpace(token))
                {
                    var b = new byte[9];
                    using (var r = System.Security.Cryptography.RandomNumberGenerator.Create()) r.GetBytes(b);
                    token = Convert.ToBase64String(b).Replace('+', 'X').Replace('/', 'Y');
                    NetProfiles.Write(file, token);
                }
            }
            token = token.Trim();
            Debug.Log($"Server: admin token {token}: type /claimadmin {token} in the game's chat to become this server's master admin"
                      + (WebAdmin.Running ? ", or log in to the web admin with it." : "."));
        }

        /// <summary>'text' with the admin token hidden (WebAdmin's log: readable by admins, who mustn't see it). Called on any
        /// thread (Application.logMessageReceivedThreaded).</summary>
        internal static string Redact(string text)
        {
            var t = token;
            return string.IsNullOrEmpty(text) || string.IsNullOrEmpty(t) || text.IndexOf(t, StringComparison.Ordinal) < 0
                ? text : text.Replace(t, "[admin token]");
        }

        internal static bool TokenMatches(string given)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(given) || given.Length != token.Length) return false;
            int diff = 0;
            for (int i = 0; i < token.Length; i++) diff |= token[i] ^ given[i];   // the same time for every wrong guess
            return diff == 0;
        }

        static void Save() { if (list != null) NetProfiles.Write(PathOf, JsonUtility.ToJson(list, true)); }

        static void Prune()
        {
            if (list != null && list.bans.RemoveAll(b => b.until > 0 && b.until <= Now) > 0) Save();
        }

        /// <summary>NetProfiles.OnLogin: why this game may not play here (null = it may): a ban on its profile or device.</summary>
        public static string BanReason(string account, string device)
        {
            if (list == null) return null;
            Prune();
            var ban = list.bans.Find(b => (account != null && b.account == account) || (!string.IsNullOrEmpty(device) && b.devices.Contains(device)));
            if (ban == null) return null;
            string why = ban.reason.Length > 0 ? ban.reason : Localization.Extra("mpBanNoReason", "no reason given");
            return ban.until == 0
                ? string.Format(Localization.Extra("mpBanned", "You are banned from this server ({0})."), why)
                : string.Format(Localization.Extra("mpBannedFor", "You are banned from this server for another {0} ({1})."), Duration(ban.until - Now), why);
        }

        static string Duration(long seconds)
        {
            seconds = Math.Max(0, seconds);
            if (seconds < 60) return $"{seconds} s";
            if (seconds < 3600) return $"{seconds / 60 + (seconds % 60 > 0 ? 1 : 0)} min";
            if (seconds < 86400) return $"{seconds / 3600.0:0.#} h";
            return $"{seconds / 86400.0:0.#} days";
        }

        /// <summary>A connected player's role (guests and players 0).</summary>
        public static int RoleOfClient(ulong client)
        {
            if (NetProfiles.Enabled) return NetProfiles.RoleOf(NetProfiles.AccountOf(client));
            // A session without profiles (hosted fresh from the menu): the host is its master, the players it made admins
            // (NetCommands' /admin, NetPlayer.IsAdmin) its admins: the multiplayer window's Admin tab for them.
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsHost && client == Unity.Netcode.NetworkManager.ServerClientId) return Master;
            var p = NetSquad.Find(client);
            return p != null && p.IsAdmin ? Admin : Player;
        }

        /// <summary>Server: a player's role onto their NetPlayer (NetCommands' rights and /help); after signing in and on a
        /// role change.</summary>
        internal static void SyncRole(ulong client) => NetSquad.Find(client)?.SetStaffRole(RoleOfClient(client));

        // ---- finding a pilot -------------------------------------------------------------------------------

        /// <summary>"#5" (a client id), a name online, else a profile's id or last name. 'client' = the online game, if any.</summary>
        static bool Find(string who, out string account, out ulong? client, out string name)
        {
            account = null; client = null; name = who;
            who = (who ?? "").Trim();
            if (who.Length == 0) return false;
            if (who[0] == '#' && ulong.TryParse(who.Substring(1), out ulong id))
            {
                var p = NetSquad.Find(id);
                if (p == null) return false;
                client = id; name = p.DisplayName; account = NetProfiles.AccountOf(id);
                return true;
            }
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsSpawned && string.Equals(p.DisplayName, who, StringComparison.OrdinalIgnoreCase))
                {
                    client = p.OwnerClientId; name = p.DisplayName; account = NetProfiles.AccountOf(p.OwnerClientId);
                    return true;
                }
            account = NetProfiles.FindAccount(who);
            if (account == null) return false;
            name = NetProfiles.AccountName(account) ?? account;
            foreach (ulong c in NetProfiles.ClientsOf(account)) { client = c; break; }
            return true;
        }

        /// <summary>'actor' (a role) may act on a pilot of role 'target': only on a lower one (the console on anyone).</summary>
        static bool Outranks(int actor, int target) => actor == ServerConsole || actor > target;

        // ---- commands ---------------------------------------------------------------------------------------

        /// <summary>NetState.SendChatRpc: a moderation command's answer to its sender; null = not one.</summary>
        public static string Command(ulong client, string text)
        {
            var words = text.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = words.Length > 0 ? words[0].ToLowerInvariant().TrimStart('/') : "";
            if (!IsCommand(cmd)) return null;
            int role = RoleOfClient(client);
            if (cmd == "staff") return Staff();
            if (cmd == "claimadmin") return Claim(client, words.Length > 1 ? words[1].Trim() : "");
            if (role < Op) return Localization.Extra("mpAdminNoRight", "Only the server's ops and admins can do that.");
            return Run(cmd, words.Length > 1 ? words[1] : "", role, NetSquad.Find(client)?.DisplayName ?? "?");
        }

        static bool IsCommand(string cmd) =>
            cmd == "kick" || cmd == "tempban" || cmd == "ban" || cmd == "unban" || cmd == "bans" || cmd == "op" || cmd == "deop" || cmd == "staff"
            || cmd == "admin" || cmd == "unadmin" || cmd == "say" || cmd == "disband" || cmd == "deleteprofile" || cmd == "claimadmin"
            || cmd == "set" || cmd == "settings";

        static float lastWrongClaim = -100f;

        /// <summary>/claimadmin TOKEN: the right token makes this player's profile a master (a wrong one is logged, and only
        /// one try per 5 s is checked).</summary>
        static string Claim(ulong client, string given)
        {
            string account = NetProfiles.AccountOf(client);
            if (account == null) return Localization.Extra("mpAdminClaimGuest", "Only a pilot with a profile can claim the server.");
            if (Time.realtimeSinceStartup - lastWrongClaim < 5f) return Localization.Extra("mpAdminClaimWait", "Wait a moment before trying again.");
            if (!TokenMatches(given))
            {
                lastWrongClaim = Time.realtimeSinceStartup;
                Debug.LogWarning($"Server: client {client} ({NetSquad.Find(client)?.DisplayName}) tried a wrong admin token.");
                return Localization.Extra("mpAdminClaimWrong", "That isn't this server's admin token.");
            }
            NetProfiles.SetRole(account, Master);
            SyncRole(client);
            Debug.Log($"Server: {NetSquad.Find(client)?.DisplayName} (profile {account}) claimed the server: master admin.");
            return Localization.Extra("mpAdminClaimed", "You are this server's master admin now: the station's Multiplayer window (top right) has an Admin tab.");
        }

        /// <summary>DedicatedServer: a console command (the console outranks everyone; admin / unadmin only there).</summary>
        public static string ConsoleCommand(string cmd, string args)
        {
            if (!NetProfiles.Enabled) return "Moderation needs player profiles (-noprofiles is set). \"kick <id>\" still drops a player.";
            if (cmd == "master" || cmd == "unmaster") return SetRoleOf(FirstWord(args, out _), cmd == "master" ? Master : Player, ServerConsole);
            if (cmd == "staff") return Staff();
            if (cmd == "token") return $"Admin token: {token} (/claimadmin {token} in the game's chat).";
            return Run(cmd, args, ServerConsole, "Server");
        }

        internal static string Run(string cmd, string args, int role, string by)
        {
            string who = FirstWord(args, out string rest);
            switch (cmd)
            {
                case "bans": return BansText();
                case "unban": return Unban(args.Trim(), by);
                case "op": case "deop":
                    if (role < Admin) return Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
                    return SetRoleOf(who, cmd == "op" ? Op : Player, role);
                case "admin": case "unadmin":
                    if (role < Master) return Localization.Extra("mpAdminOnlyMaster", "Only the server's master admin can do that.");
                    return SetRoleOf(who, cmd == "admin" ? Admin : Player, role);
                case "say":
                {
                    if (role < Admin) return Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
                    string text = NetChat.Clean(args);
                    if (text.Length == 0) return "/say <text>";
                    NetState.Instance?.ServerChat(role == ServerConsole ? "Server" : $"[{RoleName(role)}] {by}", text);
                    return "";
                }
                case "disband":
                    if (role < Admin) return Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
                    return NetFactions.ConsoleDisband(who);
                case "settings":
                    if (role < Admin) return Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
                    return NetServerSettings.ListText();
                case "set":
                    if (role < Admin) return Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
                    return NetServerSettings.Set(who, rest, by);
                case "deleteprofile":
                {
                    if (role < Master) return Localization.Extra("mpAdminOnlyMaster", "Only the server's master admin can do that.");
                    string account = NetProfiles.FindAccount(who);
                    if (account == null) return string.Format(Localization.Extra("mpAdminNoProfile", "No pilot with a profile called \"{0}\"."), who);
                    if (!Outranks(role, NetProfiles.RoleOf(account))) return Localization.Extra("mpAdminRank", "You can't do that to a pilot of your rank or higher.");
                    return NetProfiles.ConsoleDelete(account);
                }
                case "kick":
                {
                    int minutes = KickMinutes;
                    string reason = rest;
                    string first = FirstWord(rest, out string after);
                    if (int.TryParse(first, out int m)) { minutes = m; reason = after; }
                    if (role < Admin) minutes = Mathf.Clamp(minutes, 0, MaxOpBanMinutes);
                    return Punish(who, Mathf.Max(0, minutes), reason, role, by, true);
                }
                case "tempban":
                {
                    string first = FirstWord(rest, out string reason);
                    if (!int.TryParse(first, out int minutes) || minutes <= 0) return "/tempban <pilot> <minutes> [reason]";
                    if (role < Admin && minutes > MaxOpBanMinutes) return string.Format(Localization.Extra("mpAdminOpMax", "Ops ban for at most {0} hours."), MaxOpBanMinutes / 60);
                    return Punish(who, minutes, reason, role, by, false);
                }
                case "ban":
                    if (role < Admin) return Localization.Extra("mpAdminOnlyAdmin", "Only an admin can do that.");
                    return Punish(who, -1, rest, role, by, false);
                default: return null;
            }
        }

        static string FirstWord(string text, out string rest)
        {
            text = (text ?? "").Trim();
            int space = text.IndexOf(' ');
            rest = space < 0 ? "" : text.Substring(space + 1).Trim();
            return space < 0 ? text : text.Substring(0, space);
        }

        /// <summary>Kick (minutes of cooldown, 0 = none), temporary ban (minutes) or ban (-1 = for good).</summary>
        static string Punish(string who, int minutes, string reason, int role, string by, bool kick)
        {
            if (!Find(who, out string account, out ulong? client, out string name))
                return string.Format(Localization.Extra("mpAdminNobody", "No pilot or profile \"{0}\"."), who);
            if (!Outranks(role, NetProfiles.RoleOf(account))) return Localization.Extra("mpAdminRank", "You can't do that to a pilot of your rank or higher.");
            reason = NetChat.Clean(reason);
            bool ban = minutes != 0 && list != null && (account != null || client != null);
            if (ban)
            {
                var devices = account != null ? NetProfiles.DevicesOf(account) : new List<string>();
                if (account == null && client.HasValue) { string d = NetProfiles.DeviceOf(client.Value); if (!string.IsNullOrEmpty(d)) devices.Add(d); }
                list.bans.RemoveAll(b => account != null && b.account == account);
                list.bans.Add(new Ban { account = account ?? "", name = name, reason = reason, by = by, at = DateTime.UtcNow.ToString("o"),
                                        devices = devices, until = minutes < 0 ? 0 : Now + minutes * 60L });
                Save();
            }
            string length = minutes < 0 ? Localization.Extra("mpAdminForGood", "for good") : minutes > 0 ? string.Format(Localization.Extra("mpAdminFor", "for {0}"), Duration(minutes * 60L)) : "";
            string note = reason.Length > 0 ? $": {reason}" : "";
            if (client.HasValue)
            {
                string why = kick
                    ? string.Format(Localization.Extra("mpAdminKickedYou", "{0} removed you from the server{1}{2}."), by, minutes > 0 ? $" ({string.Format(Localization.Extra("mpAdminComeBack", "you can come back in {0}"), Duration(minutes * 60L))})" : "", note)
                    : string.Format(Localization.Extra("mpAdminBannedYou", "{0} banned you from the server {1}{2}."), by, length, note);
                // Every device of the profile goes (a ban holds them all), else the one game.
                var games = account != null ? new List<ulong>(NetProfiles.ClientsOf(account)) : new List<ulong>();
                if (!games.Contains(client.Value)) games.Add(client.Value);
                foreach (ulong g in games) NetGame.Kick(g, why);
            }
            NetState.Instance?.Announce(kick
                ? string.Format(Localization.Extra("mpAdminKickNews", "{0} was kicked by {1}{2}."), name, by, note)
                : string.Format(Localization.Extra("mpAdminBanNews", "{0} was banned {1} by {2}{3}."), name, length, by, note));
            Debug.Log($"Server: {by} {(kick ? "kicked" : "banned")} {name}{(account != null ? $" (profile {account})" : "")} {length}{note}");
            return "";
        }

        static string Unban(string who, string by)
        {
            if (list == null) return Localization.Extra("mpAdminNoBans", "Nobody is banned.");
            Prune();
            var ban = list.bans.Find(b => b.account == who || string.Equals(b.name, who, StringComparison.OrdinalIgnoreCase));
            if (ban == null) return string.Format(Localization.Extra("mpAdminNotBanned", "\"{0}\" isn't banned."), who);
            list.bans.Remove(ban);
            Save();
            Debug.Log($"Server: {by} lifted the ban on {ban.name}.");
            return string.Format(Localization.Extra("mpAdminUnbanned", "The ban on {0} is lifted."), ban.name);
        }

        /// <summary>A pilot's role to 'role' (0 = back to player), by an actor who outranks both the old and the new role.</summary>
        static string SetRoleOf(string who, int role, int actor)
        {
            if (!Find(who, out string account, out _, out string name) || account == null)
                return string.Format(Localization.Extra("mpAdminNoProfile", "No pilot with a profile called \"{0}\"."), who);
            int now = NetProfiles.RoleOf(account);
            if (!Outranks(actor, now) || !Outranks(actor, role)) return Localization.Extra("mpAdminRank", "You can't do that to a pilot of your rank or higher.");
            if (now == role) return string.Format(Localization.Extra("mpAdminAlready", "{0} is a {1} already."), name, RoleName(role));
            NetProfiles.SetRole(account, role);
            foreach (ulong c in NetProfiles.ClientsOf(account)) SyncRole(c);
            Tell(account, role == Player ? Localization.Extra("mpAdminYouPlayer", "You are a player again (no server role).")
                                         : string.Format(Localization.Extra("mpAdminYouRole", "You are a {0} of this server now: the station's Multiplayer window (top right) has an Admin tab."), RoleName(role)));
            Debug.Log($"Server: {name} (profile {account}) is a {RoleName(role)} now.");
            return string.Format(Localization.Extra("mpAdminRoleSet", "{0} is a {1} now."), name, RoleName(role));
        }

        static void Tell(string account, string text) { foreach (ulong c in NetProfiles.ClientsOf(account)) NetState.Instance?.Notify(c, text); }

        static string BansText()
        {
            Prune();
            if (list == null || list.bans.Count == 0) return Localization.Extra("mpAdminNoBans", "Nobody is banned.");
            var sb = new StringBuilder(Localization.Extra("mpAdminBanList", "Bans:"));
            foreach (var b in list.bans)
                sb.Append($"\n{b.name} ({(b.account.Length > 0 ? b.account : "guest")}): {(b.until == 0 ? "for good" : Duration(b.until - Now) + " left")}, by {b.by}{(b.reason.Length > 0 ? ": " + b.reason : "")}");
            return sb.ToString();
        }

        static string Staff()
        {
            var staff = NetProfiles.Staff();
            if (staff.Count == 0) return Localization.Extra("mpAdminNoStaff", "This server has no admins or ops yet (/claimadmin with the server's token).");
            var sb = new StringBuilder(Localization.Extra("mpAdminStaff", "Staff:"));
            foreach (var (name, role, online) in staff) sb.Append($"\n{name}: {RoleName(role)}{(online ? ", online" : "")}");
            return sb.ToString();
        }

        // ---- the web admin (WebAdmin) -----------------------------------------------------------------

        /// <summary>The moderation commands a web admin who logged in with a login code (a profile's role) may run.</summary>
        internal static bool IsWebCommand(string cmd) => IsCommand(cmd) && cmd != "claimadmin";

        /// <summary>WebAdmin: a moderation command line from a web admin with a profile's 'role' (the console's rights come
        /// with the admin token, DedicatedServer.Run). The answer; null = not a moderation command.</summary>
        internal static string WebCommand(string line, int role, string by)
        {
            var words = (line ?? "").Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = words.Length > 0 ? words[0].ToLowerInvariant().TrimStart('/') : "";
            if (!IsWebCommand(cmd)) return null;
            if (cmd == "staff") return Staff();
            if (role < Op) return Localization.Extra("mpAdminNoRight", "Only the server's ops and admins can do that.");
            return Run(cmd, words.Length > 1 ? words[1] : "", role, by);
        }

        // ---- the station window (NetPanel) -------------------------------------------------------------

        /// <summary>The player's role, every online pilot's role and the bans into the snapshot (the bans for ops only).</summary>
        internal static void FillPanel(ulong client, NetPanel.State s)
        {
            s.role = RoleOfClient(client);
            foreach (var p in s.pilots) p.role = RoleOfClient((ulong)p.client);
            FillFor(s);
        }

        /// <summary>The snapshot's moderation part for s.role: the bans (ops), the server, staff, profiles and settings
        /// (admins). Also the web admin's (WebAdmin, the console's role for the admin token).</summary>
        internal static void FillFor(NetPanel.State s)
        {
            if (s.role < Op) return;
            if (s.role >= Admin) { NetServerSettings.FillPanel(s); s.serverStatus = DedicatedServer.StatusText(); }   // with or without profiles
            if (list == null || !NetProfiles.Enabled) return;
            if (s.role >= Admin)
            {
                foreach (var (name, role, online) in NetProfiles.Staff()) s.staff.Add(new NetPanel.StaffRow { name = name, role = role, online = online });
                NetProfiles.FillProfiles(s);
                foreach (var row in s.profileRows) row.banned = list.bans.Exists(b => b.account == row.id);
            }
            Prune();
            foreach (var b in list.bans)
                s.bans.Add(new NetPanel.BanRow { name = b.name, account = b.account, reason = b.reason, by = b.by,
                                                 left = b.until == 0 ? Localization.Extra("mpAdminForGood", "for good") : Duration(b.until - Now) + " left" });
        }
    }
}
