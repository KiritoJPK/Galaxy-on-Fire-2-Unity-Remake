// NetMotd.cs
// Remake-only: the server's message of the day. Its text is the file motd.txt beside the server's settings (NetProfiles.Folder;
// without profiles, Application.persistentDataPath), so it can hold anything a text editor can, ASCII art included: every
// line, space and tab is kept (tabs as 4 spaces), no rich text. Read when the session starts and by "/motd reload";
// "/motd set <text>" writes it (\n for a new line), "/motd clear" empties it (admins and the console). Placeholders filled
// for each player: %player% (their pilot name), %players% (players online), %server% (the server's name).
// A joining player's game asks for it once it enters the world (RequestOnEnter -> NetState.MotdRequestRpc); the server
// answers that player (MotdRpc) with the text and its hash. The game shows it in EventScreen's MOTD window (monospace,
// sized to the longest line) unless this server's same text was already seen (PlayerPrefs mp_motd_<server>, set when the
// window closes): a changed MOTD shows again. "/motd" asks for it again and always shows it.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetMotd
    {
        /// <summary>The most the text may be (UTF-8 bytes): one RPC under Unity Transport's 6144-byte payload.</summary>
        public const int MaxBytes = 4000;
        public const int MaxLines = 60;

        static string text = "", hash = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { text = hash = ""; }

        static string PathOf => Path.Combine(string.IsNullOrEmpty(NetProfiles.Folder) ? Application.persistentDataPath : NetProfiles.Folder, "motd.txt");

        /// <summary>Server, at the session's start: the file read (none = no MOTD).</summary>
        public static void Load()
        {
            string raw = "";
            try { if (File.Exists(PathOf)) raw = File.ReadAllText(PathOf, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning($"Server: motd.txt unreadable ({e.Message})."); }
            SetText(raw);
            if (text.Length > 0) Debug.Log($"Server: MOTD from {PathOf} ({text.Split('\n').Length} lines).");
        }

        /// <summary>Line endings, tabs, no control characters, cut to MaxLines / MaxBytes (whole lines kept).</summary>
        static void SetText(string raw)
        {
            raw = (raw ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            var sb = new StringBuilder();
            foreach (char c in raw) if (c == '\n' || c >= ' ') sb.Append(c);
            var lines = sb.ToString().TrimEnd().Split('\n');
            var kept = new StringBuilder();
            for (int i = 0; i < lines.Length && i < MaxLines; i++)
            {
                string line = lines[i].TrimEnd();
                if (Encoding.UTF8.GetByteCount(kept.ToString()) + Encoding.UTF8.GetByteCount(line) + 1 > MaxBytes) break;
                if (i > 0) kept.Append('\n');
                kept.Append(line);
            }
            text = kept.ToString().Trim('\n');
            if (text.Trim().Length == 0) text = "";
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                hash = BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>"/motd [reload | set <text> | clear]": without arguments the issuer sees it again (anyone); the rest are
        /// for admins and the console.</summary>
        public static string Command(string args, Events.IPilot by)
        {
            bool admin = by == null || NetCommands.IsAdmin(by);   // null = the console
            string byName = NetCommands.IssuerName(by);
            args = (args ?? "").Trim();
            string word = args.Split(' ')[0].ToLowerInvariant();
            if (word.Length == 0)
            {
                if (by == null) return text.Length > 0 ? text : Localization.Extra("mpMotdNone", "This server has no message of the day.");
                if (text.Length == 0) return Localization.Extra("mpMotdNone", "This server has no message of the day.");
                Send(by.OwnerClientId, true);
                return "";
            }
            if (!admin) return Localization.Extra("mpMotdAdmins", "Only admins can change the message of the day: /motd shows it.");
            switch (word)
            {
                case "reload":
                    Load();
                    return text.Length > 0 ? string.Format(Localization.Extra("mpMotdLoaded", "MOTD reloaded: {0} lines."), text.Split('\n').Length)
                                           : Localization.Extra("mpMotdEmpty", "MOTD reloaded: motd.txt is empty or missing.");
                case "clear":
                    Write("");
                    Debug.Log($"Server: {byName} cleared the MOTD.");
                    return Localization.Extra("mpMotdCleared", "The message of the day is cleared.");
                case "set":
                    string body = args.Length > 3 ? args.Substring(3).Trim().Replace("\\n", "\n") : "";
                    if (body.Length == 0) return "/motd set <text>  (\\n = a new line; for ASCII art edit motd.txt and /motd reload)";
                    Write(body);
                    Debug.Log($"Server: {byName} set the MOTD.");
                    return Localization.Extra("mpMotdSet", "The message of the day is set: players see it when they join (/motd shows it).");
                default:
                    return Localization.Extra("mpMotdUsage", "/motd shows the message of the day; admins: /motd reload | set <text> | clear");
            }
        }

        static void Write(string raw)
        {
            SetText(raw);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathOf));
                File.WriteAllText(PathOf, text, new UTF8Encoding(false));
            }
            catch (Exception e) { Debug.LogWarning($"Server: motd.txt not written ({e.Message})."); }
        }

        /// <summary>Server: the MOTD to one player ('force': shown even when already seen), placeholders filled.</summary>
        public static void Send(ulong client, bool force)
        {
            var state = NetState.Instance;
            if (state == null || !state.IsServer || text.Length == 0) return;
            var p = NetSquad.Find(client);
            int players = 0;
            foreach (var x in NetPlayer.All) if (x != null && x.IsSpawned) players++;
            string filled = text.Replace("%player%", p != null ? p.DisplayName : "")
                                .Replace("%players%", players.ToString())
                                .Replace("%server%", ServerTitle());
            state.SendMotd(client, ServerTitle(), filled, hash, force);
        }

        static string ServerTitle()
        {
            string listed = PlayerPrefs.GetString("mp_session_name", "").Trim();   // the Host card's game name
            string name = NetGame.Dedicated ? DedicatedServer.ListName : listed.Length > 0 ? listed : NetGame.DefaultSessionName();
            return string.IsNullOrWhiteSpace(name) ? Localization.Extra("mpMotdTitle", "Message of the day") : name;
        }

        // ---- the player's side ------------------------------------------------------------------------------

        /// <summary>NetGame.EnterWorld: ask the server for its MOTD (shown unless this text was seen on this server).</summary>
        public static void RequestOnEnter()
        {
            var state = NetState.Instance;
            if (state != null && state.IsSpawned) state.MotdRequestRpc();
        }

        /// <summary>NetState.MotdRpc: shown in EventScreen's window; the hash remembered when it is closed.</summary>
        internal static void Received(string title, string body, string textHash, bool force)
        {
            if (string.IsNullOrEmpty(body)) return;
            string key = "mp_motd_" + title;   // per server (its name): another server's same text still shows
            if (!force && PlayerPrefs.GetString(key, "") == textHash) return;
            Events.EventScreen.ShowMotd(title, body, () => { PlayerPrefs.SetString(key, textHash); PlayerPrefs.Save(); });
        }
    }
}
