// NetProfiles.cs
// Remake-only: player profiles kept by a dedicated server (DedicatedServer; -noprofiles switches them off), so a player's
// credits, ship, equipment, cargo, Kaamo Club and squad survive between sessions on that server.
//   Identity: a profile (account) is proven by a random secret token the server gives each device on its first visit
//     (only its SHA-256 hash is stored); the device's own id is a label (NetProfileClient sends a hash of it). A device
//     without a known token gets a new profile. No limit on their number: a profile nobody signed in to for PruneDays (30)
//     is pruned (Prune: at the start and every hour; never one that is online or staff), moved to Pruned/ (the newest
//     KeepPruned kept) so a mistake can be undone by hand.
//   Linking: "/link" in the chat gives a 6-letter code for 5 minutes; another device types "/link CODE" and from then on
//     signs in to the same profile with its own token. A device whose own profile has progress needs "/link CODE force".
//   Several devices of one profile online: the first is the controller, the others observers (docked only: no launch,
//     hangar, lounge or map, NetProfileClient.Refusal; their uploads are ignored). "/control" on an observer takes over
//     once the controller is docked: the controller uploads first (RequestUpload), the observer then gets that profile.
//     A controller that leaves hands control to the profile's next device online. One connection per device: signing
//     in again from the same device drops the older connection.
//   Storage: <persistentDataPath>/ServerProfiles (or -profiledir): accounts.json (the accounts, the server's id) and one
//     <account>.json per profile, a SaveData without the shop memory (SaveGame.ProfileJson). Written atomically, the
//     previous file kept as .bak.
//   Trust: the client's game runs the economy, so an upload is what the client says. Every upload is parsed and checked
//     like an imported save (SaveGame.TryParse: indices, no negative amounts). With the Debug menu allowed (-allowdebug)
//     that is all (client-authoritative); without it an upload is also turned away when the profile's worth (credits +
//     ships + items at their lowest price, Worth) grew faster than EarnBurst + EarnPerMinute per minute online
//     (-maxearn), or when it flies a hull nobody can own (13 / 14 / 15). Not a real server authority: that needs the
//     trades and rewards decided by the server (the plan's phase 2).
//   Squads: an account remembers its squad (a key); joining / leaving one updates it, a disconnect doesn't, and a
//     controller signing in joins a squadmate who is online (NetState.RestoreSquad).
// Profiles go over the network gzipped in 4000-byte chunks (Pack / Unpack): Unity Transport's default payload limit is
// 6144 bytes per message.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetProfiles
    {
        /// <summary>A profile unused this long is pruned (Prune).</summary>
        public const int PruneDays = 30;
        const int KeepPruned = 50;                  // the newest pruned profile files kept in Pruned/
        const float PruneEverySeconds = 3600f;
        public const int DefaultEarnPerMinute = 1_000_000;
        const long EarnBurst = 2_000_000;
        public const int ChunkBytes = 4000;
        const int MaxChunks = 64;                    // 256 KB gzipped: far more than a profile needs
        const int MaxJsonBytes = 4 * 1024 * 1024;
        const float LinkCodeSeconds = 300f, HandoverSeconds = 5f;
        const string LinkAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0 / O, 1 / I
        const int MaxDeviceLength = 64;

        [Serializable] class Device { public string device, tokenHash, added; }

        [Serializable]
        class Account
        {
            public string id, name, squad, created, lastSeen;
            public List<Device> devices = new List<Device>();
            public long worth;     // at the last accepted upload (Worth)
            public float playSeconds;
            public int arenaKills, arenaDeaths, arenaWins;   // NetArena's matches (the leaderboard, /top)
            public int role;   // NetModeration: 0 player, 1 op, 2 admin
        }

        [Serializable] class AccountIndex { public string serverId; public List<Account> accounts = new List<Account>(); }

        /// <summary>One connected player: their profile (null = a guest) and whether this device controls it.</summary>
        class Login
        {
            public ulong client;
            public Account account;
            public string device;
            public bool controller;
            public float acceptedAt;   // real time of the login or the last accepted upload (the earning allowance)
        }

        class Incoming { public int seq, count, got; public byte[][] parts; }

        static bool configured;
        static string folder;
        static AccountIndex index;
        static readonly Dictionary<ulong, Login> logins = new Dictionary<ulong, Login>();
        static readonly Dictionary<ulong, Incoming> incoming = new Dictionary<ulong, Incoming>();
        static readonly Dictionary<string, (string account, float until)> links = new Dictionary<string, (string, float)>();
        // A /control in progress, per account: the old controller uploads, then the new one gets the profile.
        static readonly Dictionary<string, (ulong from, ulong to, float until)> handovers = new Dictionary<string, (ulong, ulong, float)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            configured = false;
            folder = null;
            index = null;
            nextPrune = 0f;
            EarnPerMinute = DefaultEarnPerMinute;
            logins.Clear();
            incoming.Clear();
            links.Clear();
            handovers.Clear();
        }

        static float nextPrune;

        /// <summary>Without the Debug menu: how much worth a profile may gain per minute online (-maxearn), on top of EarnBurst.</summary>
        public static int EarnPerMinute { get; internal set; } = DefaultEarnPerMinute;

        /// <summary>This process keeps profiles: a dedicated server started without -noprofiles, or a persistent world hosted
        /// from the menu (NetGame.PersistentHost: the host signs in to its own profile like any player).</summary>
        public static bool Enabled => configured && (NetGame.Dedicated || NetGame.PersistentHost);

        /// <summary>The hosting player's own game (a persistent hosted world): always a profile, always the master admin,
        /// never banned, its uploads never doubted (it runs the world).</summary>
        static bool IsHostClient(ulong client) => NetGame.PersistentHost && client == Unity.Netcode.NetworkManager.ServerClientId;

        /// <summary>This server's id (accounts.json), the key under which a client keeps its token; "" = none.</summary>
        public static string ServerId => index != null ? index.serverId : "";

        /// <summary>DedicatedServer.Boot: the command line's choices.</summary>
        public static void Configure(bool on, int earnPerMinute, string profileDir)
        {
            configured = on;
            EarnPerMinute = Mathf.Max(0, earnPerMinute);
            folder = string.IsNullOrEmpty(profileDir) ? Path.Combine(Application.persistentDataPath, "ServerProfiles") : profileDir;
        }

        /// <summary>NetGame.StartServer, before NetState spawns: the accounts loaded (and the server's id made once).</summary>
        public static void Start()
        {
            logins.Clear(); incoming.Clear(); links.Clear(); handovers.Clear();
            if (!configured) return;
            try
            {
                Directory.CreateDirectory(folder);
                string path = IndexPath;
                index = File.Exists(path) ? JsonUtility.FromJson<AccountIndex>(File.ReadAllText(path)) : null;
            }
            catch (Exception e) { Debug.LogError($"NetProfiles: accounts.json unreadable ({e.Message}); starting a new list (the old file is kept as .bak)."); BackUp(IndexPath); index = null; }
            if (index == null) index = new AccountIndex();
            if (index.accounts == null) index.accounts = new List<Account>();
            if (string.IsNullOrEmpty(index.serverId)) index.serverId = RandomHex(16);
            SaveIndex();
            NetFactions.Load();
            NetModeration.Load();
            Debug.Log($"Server: player profiles on: {index.accounts.Count} in {folder}.");
            Prune();
        }

        static string IndexPath => Path.Combine(folder, "accounts.json");

        /// <summary>Where the profiles are kept (NetFactions keeps factions.json there too).</summary>
        internal static string Folder => folder;

        /// <summary>A connected player's profile id, null = a guest or not signed in.</summary>
        internal static string AccountOf(ulong client) => logins.TryGetValue(client, out var l) && l.account != null ? l.account.id : null;

        /// <summary>A profile's last pilot name, null = unknown.</summary>
        internal static string AccountName(string id)
        {
            var a = index?.accounts.Find(x => x.id == id);
            return a == null || string.IsNullOrEmpty(a.name) ? null : a.name;
        }

        /// <summary>The connected devices of a profile.</summary>
        internal static IEnumerable<ulong> ClientsOf(string id)
        {
            var result = new List<ulong>();
            foreach (var l in logins.Values) if (l.account != null && l.account.id == id) result.Add(l.client);
            return result;
        }

        /// <summary>The player's device controls its profile (a signed-in controller, not a guest or an observer).</summary>
        internal static bool Controls(ulong client) => logins.TryGetValue(client, out var l) && l.account != null && l.controller;

        /// <summary>Credits the server moved in or out of a profile (the faction bank): its recorded worth moves too, so the
        /// next upload is measured against what the game should have now.</summary>
        internal static void AdjustWorth(string id, long delta)
        {
            var a = index?.accounts.Find(x => x.id == id);
            if (a == null) return;
            a.worth += delta;
            SaveIndex();
        }

        /// <summary>A profile's role (NetModeration), 0 for none / a guest.</summary>
        internal static int RoleOf(string id) => id == null ? 0 : index?.accounts.Find(x => x.id == id)?.role ?? 0;

        internal static void SetRole(string id, int role)
        {
            var a = index?.accounts.Find(x => x.id == id);
            if (a == null || a.role == role) return;
            a.role = role;
            SaveIndex();
        }

        /// <summary>A profile by its id or its last pilot name (null = none).</summary>
        internal static string FindAccount(string idOrName)
        {
            if (index == null || string.IsNullOrEmpty(idOrName)) return null;
            var a = index.accounts.Find(x => x.id == idOrName) ?? index.accounts.Find(x => string.Equals(x.name, idOrName, StringComparison.OrdinalIgnoreCase));
            return a?.id;
        }

        /// <summary>The device labels a profile signed in from (a ban holds them all).</summary>
        internal static List<string> DevicesOf(string id)
        {
            var a = index?.accounts.Find(x => x.id == id);
            var list = new List<string>();
            if (a != null) foreach (var d in a.devices) if (!string.IsNullOrEmpty(d.device) && !list.Contains(d.device)) list.Add(d.device);
            return list;
        }

        /// <summary>The admins' window: every profile (the most recent first, at most 200).</summary>
        internal static void FillProfiles(NetPanel.State s)
        {
            if (index == null) return;
            var all = new List<Account>(index.accounts);
            all.Sort((a, b) => string.CompareOrdinal(b.lastSeen ?? "", a.lastSeen ?? ""));
            for (int i = 0; i < all.Count && i < 200; i++)
            {
                var a = all[i];
                s.profileRows.Add(new NetPanel.ProfileRow { id = a.id, name = a.name ?? "", role = a.role, devices = a.devices.Count, online = IsOnline(a.id),
                                                         lastSeen = (a.lastSeen ?? "").Length >= 10 ? a.lastSeen.Substring(0, 10) : "" });
            }
        }

        /// <summary>A connected game's device label (a guest's too).</summary>
        internal static string DeviceOf(ulong client) => logins.TryGetValue(client, out var l) ? l.device : null;

        /// <summary>The admins and ops: name, role, online.</summary>
        internal static List<(string name, int role, bool online)> Staff()
        {
            var list = new List<(string, int, bool)>();
            if (index != null)
                foreach (var a in index.accounts)
                    if (a.role > 0) list.Add((string.IsNullOrEmpty(a.name) ? a.id : a.name, a.role, IsOnline(a.id)));
            return list;
        }

        internal static bool IsOnline(string id)
        {
            foreach (var l in logins.Values) if (l.account != null && l.account.id == id) return true;
            return false;
        }
        static string ProfilePath(string id) => Path.Combine(folder, id + ".json");

        // ---- signing in -------------------------------------------------------------------------------------

        /// <summary>NetState.LoginRpc: a connected game says who it is. Answers with its profile (or a fresh start), its new
        /// token if it got one, and its role.</summary>
        public static void OnLogin(ulong client, string token, string device, string name)
        {
            if (!Enabled || NetState.Instance == null || logins.ContainsKey(client)) return;
            device = CleanDevice(device);
            name = NetGame.Clean(name);
            Account account = null;
            string newToken = null;
            if (!string.IsNullOrEmpty(token))
            {
                string hash = Hash(token);
                account = index.accounts.Find(a => a.devices.Exists(d => d.tokenHash == hash));
                var entry = account?.devices.Find(d => d.tokenHash == hash);
                if (entry != null) entry.device = device;   // a label only (the token proves it)
            }
            // A ban on this profile or this device (NetModeration): dropped with the reason (never the host itself).
            if (!IsHostClient(client) && NetModeration.BanReason(account?.id, device) is string banned)
            {
                Debug.Log($"Server: client {client} ({(account != null ? "profile " + account.id : "no profile")}) is banned; dropped.");
                NetGame.Kick(client, banned);
                return;
            }
            if (account == null)
            {
                account = new Account { id = NewAccountId(), created = Now() };
                newToken = AddDevice(account, device);
                index.accounts.Add(account);
                Debug.Log($"Server: new profile {account.id} for {(name.Length > 0 ? name : "client " + client)} ({index.accounts.Count} in all).");
                if (name.Length > 0)
                    NetNews.Post(NetNews.Kind.Pilot, $"New pilot in the sector: {NetNews.Safe(NetGame.Clean(name))} registers at Dis", -1, "newpilot", 120f);
            }
            // One connection per device: the older one goes (a reconnect while the dropped one still times out).
            if (account != null)
                foreach (var other in new List<Login>(logins.Values))
                    if (other.account == account && other.device == device)
                    {
                        logins.Remove(other.client);
                        if (other.controller) Debug.Log($"Server: profile {account.id} signed in again from the same device; the older connection goes.");
                        NetGame.Kick(other.client, Localization.Extra("mpSignedInElsewhere", "You signed in again from this device."));
                    }
            var login = new Login { client = client, account = account, device = device, acceptedAt = Time.realtimeSinceStartup };
            login.controller = account == null || ControllerOf(account) == null;
            logins[client] = login;
            if (account != null)
            {
                if (name.Length > 0) account.name = name;
                account.lastSeen = Now();
                if (IsHostClient(client) && account.role < NetModeration.Master)
                {
                    account.role = NetModeration.Master;   // the world's owner
                    Debug.Log($"Server: the host ({account.name}) is this world's master admin.");
                }
                SaveIndex();
            }
            SetObserverFlag(client, !login.controller);
            SendProfile(login, newToken);
            if (account != null && !login.controller)
                NetState.Instance.Notify(client, Localization.Extra("mpObserverJoined",
                    "Your profile is in use on another device: this one watches from the station. Type /control once the other one is docked."));
            if (login.controller && account != null) RestoreSquad(login);
            NetFactions.OnLogin(client);   // the faction's tag on the name
        }

        /// <summary>NetGame: a player left. A controller's profile goes to its next device online, if any.</summary>
        public static void OnDisconnect(ulong client)
        {
            incoming.Remove(client);
            if (!logins.TryGetValue(client, out var login)) return;
            logins.Remove(client);
            if (login.account == null) return;
            login.account.lastSeen = Now();
            SaveIndex();
            foreach (var pair in new List<KeyValuePair<string, (ulong from, ulong to, float until)>>(handovers))
                if (pair.Value.from == client || pair.Value.to == client)
                {
                    handovers.Remove(pair.Key);
                    if (pair.Value.from == client && logins.TryGetValue(pair.Value.to, out var waiting)) Promote(waiting);
                }
            if (login.controller && ControllerOf(login.account) == null)
                foreach (var other in logins.Values)
                    if (other.account == login.account) { Promote(other); break; }
        }

        /// <summary>NetState.Update (server): link codes and handovers that ran out.</summary>
        public static void Tick()
        {
            if (!Enabled) return;
            float now = Time.realtimeSinceStartup;
            if (now >= nextPrune) Prune();   // hourly: profiles unused for PruneDays
            if (links.Count > 0)
                foreach (var code in new List<string>(links.Keys))
                    if (links[code].until < now) links.Remove(code);
            if (handovers.Count > 0)
                foreach (var pair in new List<KeyValuePair<string, (ulong from, ulong to, float until)>>(handovers))
                {
                    if (pair.Value.until >= now) continue;
                    handovers.Remove(pair.Key);   // no upload in time: the last saved profile
                    if (logins.TryGetValue(pair.Value.to, out var to)) Promote(to);
                }
        }

        static Login ControllerOf(Account account)
        {
            foreach (var l in logins.Values) if (l.account == account && l.controller) return l;
            return null;
        }

        /// <summary>This device controls its profile now: it gets the latest saved profile (its game reloads the station).</summary>
        static void Promote(Login login)
        {
            if (login.account != null && ControllerOf(login.account) != null) return;
            login.controller = true;
            login.acceptedAt = Time.realtimeSinceStartup;
            SetObserverFlag(login.client, false);
            SendProfile(login, null);
            NetState.Instance?.Notify(login.client, Localization.Extra("mpNowController", "This device controls your profile now."));
            Debug.Log($"Server: client {login.client} controls profile {login.account?.id} now.");
        }

        static void Demote(Login login)
        {
            login.controller = false;
            SetObserverFlag(login.client, true);
            NetState.Instance?.SendRole(login.client, false);
        }

        static void SetObserverFlag(ulong client, bool on)
        {
            var p = NetSquad.Find(client);
            if (p != null) p.SetObserver(on);
        }

        static void SendProfile(Login login, string newToken)
        {
            string json = "";
            if (login.account != null)
                try { if (File.Exists(ProfilePath(login.account.id))) json = File.ReadAllText(ProfilePath(login.account.id)); }
                catch (Exception e) { Debug.LogError($"NetProfiles: profile {login.account.id} unreadable: {e.Message}"); }
            int home = login.account != null ? NetFactions.HomeOf(login.account.id) : -1;   // a faction member starts at its home
            NetState.Instance?.SendProfile(login.client, newToken, login.controller, login.account == null, json, home);
            NetModeration.SyncRole(login.client);   // the profile's role onto its NetPlayer (NetCommands' rights)
        }

        // ---- uploads ----------------------------------------------------------------------------------------

        /// <summary>NetState.UploadChunkRpc: a piece of a client's profile; the last one checks and saves it.</summary>
        public static void OnUploadChunk(ulong client, int seq, int part, int count, byte[] data)
        {
            if (!Enabled || data == null || data.Length > ChunkBytes || count < 1 || count > MaxChunks || part < 0 || part >= count) return;
            if (!incoming.TryGetValue(client, out var inc) || inc.seq != seq)
                incoming[client] = inc = new Incoming { seq = seq, count = count, parts = new byte[count][] };
            if (inc.count != count || inc.parts[part] != null) return;
            inc.parts[part] = data;
            if (++inc.got < count) return;
            incoming.Remove(client);
            string json = Unpack(inc.parts);
            if (json != null) Receive(client, json);
        }

        static void Receive(ulong client, string json)
        {
            if (!logins.TryGetValue(client, out var login) || login.account == null) return;
            var account = login.account;
            bool handing = handovers.TryGetValue(account.id, out var handover) && handover.from == client;
            if (!login.controller && !handing) return;   // observers don't write
            float now = Time.realtimeSinceStartup;
            if (!SaveGame.TryParse(json, NetGame.Db, out var save, out string problem))
            {
                Reject(login, problem);
                return;
            }
            long worth = Worth(save);
            if (!NetGame.HostAllowsDebug && !IsHostClient(client) && Implausible(login, save, worth) is string why)
            {
                Reject(login, why);
                return;
            }
            if (!Write(ProfilePath(account.id), json)) return;
            account.worth = worth;
            account.playSeconds = save.playSeconds;
            account.lastSeen = Now();
            login.acceptedAt = now;
            SaveIndex();
            if (handing)
            {
                handovers.Remove(account.id);
                if (logins.TryGetValue(handover.to, out var to)) Promote(to);
            }
        }

        /// <summary>A persistent hosted world: the host's own game saved straight into its profile (NetGame.Shutdown, so the
        /// last minutes aren't lost when the host closes the session).</summary>
        internal static void SaveHost(string json)
        {
            if (Enabled && !string.IsNullOrEmpty(json)) Receive(Unity.Netcode.NetworkManager.ServerClientId, json);
        }

        static void Reject(Login login, string why)
        {
            Debug.LogWarning($"Server: profile {login.account.id} ({login.account.name}): upload not saved: {why}.");
            NetState.Instance?.Notify(login.client, string.Format(Localization.Extra("mpNotSaved", "The server didn't save your progress: {0}."), why));
        }

        /// <summary>Without the Debug menu: what can't have happened in the game (null = plausible).</summary>
        static string Implausible(Login login, SaveData save, long worth)
        {
            if (save.ship == 13 || save.ship == 14 || save.ship == 15) return "a ship nobody can own";
            if (save.kaamoShips != null && save.kaamoShips.Exists(k => k.ship == 13 || k.ship == 14 || k.ship == 15)) return "a stored ship nobody can own";
            float minutes = Mathf.Max(0f, Time.realtimeSinceStartup - login.acceptedAt) / 60f;
            long allowed = EarnBurst + (long)(minutes * EarnPerMinute);
            if (worth - login.account.worth > allowed)
                return $"its worth grew by {worth - login.account.worth:N0} in {minutes:0.#} minutes (at most {allowed:N0})";
            return null;
        }

        /// <summary>What a profile owns: credits, the ships (their price) and every item at its lowest price.</summary>
        static long Worth(SaveData s)
        {
            var db = NetGame.Db;
            long sum = s.credits;
            long Items(List<ItemStack> list)
            {
                long v = 0;
                if (list != null) foreach (var st in list) if (st != null) v += (long)(db.Item(st.item)?.minPrice ?? 0) * Math.Max(0, st.amount);
                return v;
            }
            long ShipPrice(int ship) => db.Ship(ship)?.price ?? 0;
            sum += ShipPrice(s.ship) + Items(s.equipment) + Items(s.cargo) + Items(s.kaamoItems);
            if (s.kaamoShips != null) foreach (var k in s.kaamoShips) if (k != null) sum += ShipPrice(k.ship) + Items(k.equipment);
            if (s.hasParkedShip && s.parkedShip != null) sum += ShipPrice(s.parkedShip.ship) + Items(s.parkedShip.equipment) + Items(s.parkedShip.cargo);
            return sum;
        }

        // ---- chat commands ----------------------------------------------------------------------------------

        /// <summary>NetState.SendChatRpc: a line starting with '/' while profiles are on. The answer goes only to its sender.</summary>
        public static string Command(ulong client, string text)
        {
            if (!logins.TryGetValue(client, out var login)) return Localization.Extra("mpNotSignedIn", "Not signed in yet.");
            var words = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = words.Length > 0 ? words[0].ToLowerInvariant() : "";
            switch (cmd)
            {
                case "/link":
                    return words.Length == 1 ? NewLinkCode(login) : UseLinkCode(login, words[1], words.Length > 2 && words[2].ToLowerInvariant() == "force");
                case "/control":
                    return TakeControl(login);
                case "/profile": case "/whoami":
                    if (login.account == null) return Localization.Extra("mpGuestShort", "You play as a guest: nothing is saved.");
                    return string.Format(Localization.Extra("mpProfileInfo", "Profile {0}, {1} device(s); this one {2}."), login.account.id,
                        login.account.devices.Count, login.controller ? Localization.Extra("mpControls", "controls it") : Localization.Extra("mpWatches", "watches"));
                default:
                    return Localization.Extra("mpCommands", "Commands: /duel <name>, /accept, /decline, /ffa, /leave, /arena, /top; /faction (help: /faction help), /f <text>; /link (a code for another device), /link CODE, /control, /profile; /staff (ops: /kick, /tempban, /unban, /bans; admins: /ban, /op, /deop).");
            }
        }

        static string NewLinkCode(Login login)
        {
            if (login.account == null) return Localization.Extra("mpGuestNoLink", "A guest has no profile to link.");
            if (!login.controller) return Localization.Extra("mpLinkFromController", "Ask for a link code on the device that controls the profile.");
            string code;
            do
            {
                var sb = new StringBuilder();
                var bytes = new byte[6];
                using (var r = RandomNumberGenerator.Create()) r.GetBytes(bytes);
                foreach (byte b in bytes) sb.Append(LinkAlphabet[b % LinkAlphabet.Length]);
                code = sb.ToString();
            } while (links.ContainsKey(code));
            links[code] = (login.account.id, Time.realtimeSinceStartup + LinkCodeSeconds);
            return string.Format(Localization.Extra("mpLinkCode",
                "Link code {0} (5 minutes): on your other device join this server and type /link {0} in the chat."), code);
        }

        static string UseLinkCode(Login login, string code, bool force)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!links.TryGetValue(code, out var link) || link.until < Time.realtimeSinceStartup)
                return Localization.Extra("mpLinkUnknown", "That link code is unknown or ran out.");
            var target = index.accounts.Find(a => a.id == link.account);
            if (target == null) { links.Remove(code); return Localization.Extra("mpLinkUnknown", "That link code is unknown or ran out."); }
            if (target == login.account) return Localization.Extra("mpLinkSame", "This device already uses that profile.");
            var own = login.account;
            if (own != null)
            {
                bool only = own.devices.Count <= 1;
                if (only && own.playSeconds > 300f && !force)
                    return string.Format(Localization.Extra("mpLinkForce",
                        "This device has a profile of its own with progress: linking deletes it. Type /link {0} force to go ahead."), code);
                if (login.controller) Demote(login);
                own.devices.RemoveAll(d => d.device == login.device);
                if (only) DeleteAccount(own);
            }
            links.Remove(code);
            string token = AddDevice(target, login.device);
            login.account = target;
            login.controller = ControllerOf(target) == null;
            login.acceptedAt = Time.realtimeSinceStartup;
            target.lastSeen = Now();
            SaveIndex();
            SetObserverFlag(login.client, !login.controller);
            SendProfile(login, token);
            Debug.Log($"Server: client {login.client} linked to profile {target.id} ({target.devices.Count} devices).");
            if (login.controller) RestoreSquad(login);
            NetFactions.OnLogin(login.client);
            return login.controller ? Localization.Extra("mpLinked", "Linked: this device uses your profile now.")
                                    : Localization.Extra("mpLinkedObserver", "Linked. Your other device controls the profile: this one watches until you type /control.");
        }

        static string TakeControl(Login login)
        {
            if (login.account == null) return Localization.Extra("mpGuestShort", "You play as a guest: nothing is saved.");
            if (login.controller) return Localization.Extra("mpAlreadyController", "This device already controls your profile.");
            if (handovers.ContainsKey(login.account.id)) return Localization.Extra("mpHandoverBusy", "Taking control already...");
            var current = ControllerOf(login.account);
            if (current == null) { Promote(login); return ""; }
            var player = NetSquad.Find(current.client);
            if (player != null && !player.InHangar)
                return Localization.Extra("mpControllerFlying", "Your other device is in space: take control once it is docked.");
            Demote(current);
            handovers[login.account.id] = (current.client, login.client, Time.realtimeSinceStartup + HandoverSeconds);
            NetState.Instance?.RequestUpload(current.client);
            NetState.Instance?.Notify(current.client, Localization.Extra("mpControlTaken", "Another device of yours took control of the profile: this one only watches now."));
            return Localization.Extra("mpTakingControl", "Taking control...");
        }

        // ---- squads -----------------------------------------------------------------------------------------

        /// <summary>NetState: 'joiner' joined 'leader''s squad (both remember it).</summary>
        public static void SquadJoined(ulong leader, ulong joiner)
        {
            if (!Enabled || !logins.TryGetValue(leader, out var l) || !logins.TryGetValue(joiner, out var j) || l.account == null || j.account == null) return;
            if (string.IsNullOrEmpty(l.account.squad)) l.account.squad = RandomHex(8);
            j.account.squad = l.account.squad;
            SaveIndex();
        }

        /// <summary>NetState: the player left their squad on purpose (a disconnect keeps it).</summary>
        public static void SquadLeft(ulong client)
        {
            if (!Enabled || !logins.TryGetValue(client, out var l) || l.account == null || string.IsNullOrEmpty(l.account.squad)) return;
            l.account.squad = null;
            SaveIndex();
        }

        /// <summary>A controller signed in: back into the squad of a remembered squadmate who is online.</summary>
        static void RestoreSquad(Login login)
        {
            string key = login.account?.squad;
            if (string.IsNullOrEmpty(key) || NetState.Instance == null) return;
            var me = NetSquad.Find(login.client);
            foreach (var other in logins.Values)
            {
                if (other == login || !other.controller || other.account == null || other.account == login.account || other.account.squad != key) continue;
                var mate = NetSquad.Find(other.client);
                if (mate == null) continue;
                NetState.Instance.RestoreSquad(mate, me);
                return;
            }
        }

        // ---- the station window (NetPanel) -----------------------------------------------------------------

        /// <summary>The player's profile into the window's snapshot, and the leaderboard.</summary>
        internal static void FillPanel(ulong client, NetPanel.State s)
        {
            s.profiles = Enabled;
            if (!Enabled) return;
            if (logins.TryGetValue(client, out var l))
            {
                s.guest = l.account == null;
                s.controller = l.controller;
                if (l.account != null) { s.profileId = l.account.id; s.devices = l.account.devices.Count; }
            }
            s.top = Leaderboard();
        }

        // ---- arena stats (NetArena) -------------------------------------------------------------------------

        /// <summary>A finished match's kills, deaths and win into the player's profile (guests: nothing).</summary>
        public static void AddArenaStats(ulong client, int kills, int deaths, bool won)
        {
            if (!Enabled || !logins.TryGetValue(client, out var l) || l.account == null) return;
            l.account.arenaKills += kills;
            l.account.arenaDeaths += deaths;
            if (won) l.account.arenaWins++;
            SaveIndex();
        }

        /// <summary>/top: the ten profiles with the most arena wins (then kills).</summary>
        public static string Leaderboard()
        {
            if (!Enabled) return Localization.Extra("mpTopNone", "This server keeps no profiles, so no leaderboard.");
            var list = index.accounts.FindAll(a => a.arenaKills + a.arenaDeaths > 0);
            if (list.Count == 0) return Localization.Extra("mpTopEmpty", "No arena matches fought yet.");
            list.Sort((a, b) => a.arenaWins != b.arenaWins ? b.arenaWins.CompareTo(a.arenaWins) : b.arenaKills.CompareTo(a.arenaKills));
            var sb = new StringBuilder(Localization.Extra("mpTopTitle", "Arena leaderboard (wins, kills / deaths):"));
            for (int i = 0; i < list.Count && i < 10; i++)
                sb.Append($"\n{i + 1}. {(string.IsNullOrEmpty(list[i].name) ? list[i].id : list[i].name)}: {list[i].arenaWins}, {list[i].arenaKills} / {list[i].arenaDeaths}");
            return sb.ToString();
        }

        // ---- the server console -----------------------------------------------------------------------------

        /// <summary>DedicatedServer's "profiles" command.</summary>
        public static string ConsoleList()
        {
            if (!Enabled) return "Player profiles are off (-noprofiles).";
            var sb = new StringBuilder($"{index.accounts.Count} profile(s) in {folder} (unused for {PruneDays} days: pruned):");
            foreach (var a in index.accounts)
            {
                string online = "";
                foreach (var l in logins.Values) if (l.account == a) online += $" client {l.client}{(l.controller ? "" : " (watching)")}";
                sb.Append($"\n  {a.id}  {(string.IsNullOrEmpty(a.name) ? "?" : a.name),-20}  {a.devices.Count} device(s), worth {a.worth:N0}, last seen {a.lastSeen}{(online.Length > 0 ? ", online:" + online : "")}");
            }
            return sb.ToString();
        }

        /// <summary>DedicatedServer's "profile delete &lt;id&gt;": not while one of its devices is online.</summary>
        public static string ConsoleDelete(string id)
        {
            if (!Enabled) return "Player profiles are off (-noprofiles).";
            var a = index.accounts.Find(x => x.id == id);
            if (a == null) return $"No profile \"{id}\" (see \"profiles\").";
            foreach (var l in logins.Values) if (l.account == a) return $"Profile {id} is online (client {l.client}): kick it first.";
            DeleteAccount(a);
            SaveIndex();
            return $"Deleted profile {id} (its file is kept as {id}.json.bak).";
        }

        /// <summary>Profiles nobody signed in to for PruneDays (by lastSeen, else created) go: not one that is online, nor
        /// staff (NetModeration roles: an op, admin or the master, e.g. the world's owner). Each leaves its faction like a
        /// deleted profile; its file moves to Pruned/ (the newest KeepPruned kept). Returns how many went.</summary>
        public static int Prune()
        {
            nextPrune = Time.realtimeSinceStartup + PruneEverySeconds;
            if (!Enabled || index == null) return 0;
            var cutoff = DateTime.UtcNow.AddDays(-PruneDays);
            var gone = new List<Account>();
            foreach (var a in index.accounts)
            {
                if (a.role > 0 || IsOnline(a.id)) continue;
                string when = string.IsNullOrEmpty(a.lastSeen) ? a.created : a.lastSeen;
                if (!DateTime.TryParse(when, null, System.Globalization.DateTimeStyles.RoundtripKind, out var seen)) continue;   // unknown: kept
                if (seen.ToUniversalTime() < cutoff) gone.Add(a);
            }
            if (gone.Count == 0) return 0;
            string pruned = Path.Combine(folder, "Pruned");
            try { Directory.CreateDirectory(pruned); } catch (Exception e) { Debug.LogError($"NetProfiles: can't make {pruned}: {e.Message}"); }
            foreach (var a in gone)
            {
                index.accounts.Remove(a);
                NetFactions.OnAccountDeleted(a.id);
                try
                {
                    string file = ProfilePath(a.id);
                    string target = Path.Combine(pruned, a.id + ".json");
                    if (File.Exists(file))
                    {
                        File.Copy(file, target, true);
                        File.SetLastWriteTimeUtc(target, DateTime.UtcNow);   // the prune's time: the newest prunes are kept
                    }
                    // Its accounts.json entry (devices, role, statistics) beside it: "profile restore <id>" puts both back.
                    File.WriteAllText(Path.Combine(pruned, a.id + ".account.json"), JsonUtility.ToJson(a, true));
                    if (File.Exists(file)) File.Delete(file);
                    if (File.Exists(file + ".bak")) File.Delete(file + ".bak");
                }
                catch (Exception e) { Debug.LogError($"NetProfiles: pruning {a.id}'s file failed: {e.Message}"); }
                Debug.Log($"Server: profile {a.id} ({(string.IsNullOrEmpty(a.name) ? "?" : a.name)}) pruned: last seen {a.lastSeen ?? a.created}.");
            }
            SaveIndex();
            // Only the newest pruned files stay.
            try
            {
                var files = new List<FileInfo>(new DirectoryInfo(pruned).GetFiles("*.json"));
                files.RemoveAll(f => f.Name.EndsWith(".account.json", StringComparison.OrdinalIgnoreCase));
                files.Sort((x, y) => y.LastWriteTimeUtc.CompareTo(x.LastWriteTimeUtc));
                for (int i = KeepPruned; i < files.Count; i++)
                {
                    string entry = Path.Combine(pruned, Path.GetFileNameWithoutExtension(files[i].Name) + ".account.json");
                    files[i].Delete();
                    if (File.Exists(entry)) File.Delete(entry);
                }
            }
            catch (Exception e) { Debug.LogError($"NetProfiles: tidying {pruned} failed: {e.Message}"); }
            Debug.Log($"Server: {gone.Count} profile(s) unused for {PruneDays} days pruned; {index.accounts.Count} left.");
            return gone.Count;
        }

        /// <summary>DedicatedServer's "profile restore &lt;id&gt;": a pruned profile back (its file and its accounts.json entry, so
        /// its devices sign in to it again), its last-seen date reset so the next prune doesn't take it at once.</summary>
        public static string ConsoleRestore(string id)
        {
            if (!Enabled) return "Player profiles are off (-noprofiles).";
            if (index.accounts.Exists(x => x.id == id)) return $"Profile {id} exists already.";
            string pruned = Path.Combine(folder, "Pruned");
            string file = Path.Combine(pruned, id + ".json"), entry = Path.Combine(pruned, id + ".account.json");
            if (!File.Exists(entry)) return $"No pruned profile \"{id}\" in {pruned}.";
            Account a;
            try { a = JsonUtility.FromJson<Account>(File.ReadAllText(entry)); }
            catch (Exception e) { return $"{entry} can't be read: {e.Message}"; }
            if (a == null || a.id != id) return $"{entry} isn't profile {id}.";
            try
            {
                if (File.Exists(file)) { File.Copy(file, ProfilePath(id), true); File.Delete(file); }
                File.Delete(entry);
            }
            catch (Exception e) { return $"Restoring {id} failed: {e.Message}"; }
            a.lastSeen = Now();
            index.accounts.Add(a);
            SaveIndex();
            Debug.Log($"Server: pruned profile {id} restored.");
            return $"Restored profile {id} ({(string.IsNullOrEmpty(a.name) ? "?" : a.name)}). It left its faction when it was pruned.";
        }

        static void DeleteAccount(Account a)
        {
            index.accounts.Remove(a);
            NetFactions.OnAccountDeleted(a.id);
            BackUp(ProfilePath(a.id));
            Debug.Log($"Server: profile {a.id} deleted.");
        }

        // ---- files and helpers ------------------------------------------------------------------------------

        static void SaveIndex()
        {
            if (index != null) Write(IndexPath, JsonUtility.ToJson(index, true));
        }

        /// <summary>Through a temporary file, the previous version kept as .bak (a crash mid-write loses nothing).</summary>
        internal static bool Write(string path, string text)
        {
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, text);
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e) { Debug.LogError($"NetProfiles: writing {path} failed: {e.Message}"); return false; }
        }

        static void BackUp(string path)
        {
            try { if (File.Exists(path)) { File.Copy(path, path + ".bak", true); File.Delete(path); } }
            catch (Exception e) { Debug.LogError($"NetProfiles: moving {path} aside failed: {e.Message}"); }
        }

        static string AddDevice(Account account, string device)
        {
            string token = RandomToken();
            account.devices.Add(new Device { device = device, tokenHash = Hash(token), added = Now() });
            return token;
        }

        static string NewAccountId()
        {
            string id;
            do id = RandomHex(8); while (index.accounts.Exists(a => a.id == id));
            return id;
        }

        static string CleanDevice(string device)
        {
            var sb = new StringBuilder();
            foreach (char c in device ?? "") if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '#') sb.Append(c);
            string d = sb.ToString();
            return d.Length > MaxDeviceLength ? d.Substring(0, MaxDeviceLength) : d;
        }

        static string Now() => DateTime.UtcNow.ToString("o");

        static string RandomToken()
        {
            var bytes = new byte[32];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static string RandomHex(int bytes)
        {
            var b = new byte[bytes];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(b);
            return Hex(b);
        }

        /// <summary>SHA-256 as lower-case hex (tokens are stored only this way; NetProfileClient hashes the device id too).</summary>
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
        }

        static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>A profile's JSON gzipped and cut into network-sized chunks (none for an empty text).</summary>
        public static List<byte[]> Pack(string json)
        {
            var parts = new List<byte[]>();
            if (string.IsNullOrEmpty(json)) return parts;
            byte[] packed;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    byte[] raw = Encoding.UTF8.GetBytes(json);
                    gz.Write(raw, 0, raw.Length);
                }
                packed = ms.ToArray();
            }
            for (int i = 0; i < packed.Length; i += ChunkBytes)
            {
                var chunk = new byte[Math.Min(ChunkBytes, packed.Length - i)];
                Buffer.BlockCopy(packed, i, chunk, 0, chunk.Length);
                parts.Add(chunk);
            }
            if (parts.Count > MaxChunks) Debug.LogError($"NetProfiles: a profile of {packed.Length} bytes gzipped is too big to send.");
            return parts;
        }

        /// <summary>The chunks back into the JSON (null = broken or too big).</summary>
        public static string Unpack(byte[][] parts)
        {
            try
            {
                using (var packed = new MemoryStream())
                {
                    foreach (var p in parts) { if (p == null) return null; packed.Write(p, 0, p.Length); }
                    packed.Position = 0;
                    using (var gz = new GZipStream(packed, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        var buffer = new byte[16384];
                        int n;
                        while ((n = gz.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, n);
                            if (output.Length > MaxJsonBytes) return null;
                        }
                        return Encoding.UTF8.GetString(output.ToArray());
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("NetProfiles: a profile didn't unpack: " + e.Message); return null; }
        }
    }
}
