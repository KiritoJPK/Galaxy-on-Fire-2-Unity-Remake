// NetCommands.cs
// Chat commands: a line typed in the chat that starts with "/" is a command, never sent as chat (NetChat.Send). Every
// command that acts on the session runs on the server (ServerCommands): the chat sends its name and arguments
// (NetState.ServerCommandRpc), the server checks the sender's rights and runs it, and its answer comes back as a notice
// only the sender sees. The dedicated server's console runs the very same commands (RunOnServer with no issuer: every
// right, named "Server"). Only the commands that just change or read this game stay here (help, netstats, pos).
// Each command says who may use it (`available` here for /help and the suggestions, `allowed` on the server, which
// decides). Typing "/" lists the matching commands over the chat line and Tab completes / cycles them, and a player name
// after the commands that take one (ChatView, Completions).
// This game:
//   /help                      the commands this player can use
//   /netstats                  shows / hides the network stats over the HUD (NetStats)
//   /version                   this game's build (BuildVersion.Text)
//   /pos                       your orbit and game coordinates (what /tp takes)
// The server (the console without the "/"):
//   /players                   everyone in the session: where they are, their ship, squad, admin (admins and the console:
//                              their client ids too)
//   /g <text>, /l <text>       one line to Global / Local without switching the channel (the console: g = its say)
//   /w <player> <text>         a private message to that player only
//   /invite <player>           a squad invitation (docked at the same station, like the pilot list's Invite)
//   /leave                     leaves the arena queue or match (NetArena), else the squad
//   /kick <player> [reason]    admins: drops a player (never the host's own player; only the host removes an admin); on a
//                              server with profiles NetModeration's kick (ops too, a rejoin cooldown in minutes)
//   /tp [player] <player | station [x y z | dock]>   admins: teleports (NetTeleport) a player (the chat: yourself by
//                              default) to a player, an orbit (at the launch spot or game coordinates) or a station's hangar
//   /tphere <player>           admins: brings a player to you
//   /kill, /heal, /give, /credits, /spawn, /mute, /unmute, /ship, /ammo, /reveal, /peace, /cheat, /title, /timer
//                              admins: NetAdmin (destroy, repair, items, credits, NPC ships and objects, chat muting, the
//                              debug panel's tools for one player: fly any hull, refill, reveal the map, make peace, a
//                              cheat toggle for the session; a title and a countdown on screen, EventScreen)
//   /dialog [players] <speaker> : <text> [| page ...]   admins: the dialogue window with a story speaker's or a race's face
//   /reward [players] <credits | item [amount]> [+ ...] [| title]   admins: a payout in the mission reward box
//   /event <name | stop | list>  admins: runs an event graph (EventRunner: game modes such as waves of enemies)
//   /news <text | clear>       admins: a GalNet item on every station's news ticker (NetNews)
//   /admin, /unadmin <player>  the host: makes a player an admin for the session / takes it back (with profiles: the
//                              master admin, NetModeration, kept on the profile)
// A dedicated server with player profiles adds (their own handlers answer them): moderation by the profiles' roles
// (NetModeration: /tempban /ban /unban /bans /op /deop /staff /say /disband /deleteprofile /settings /set /claimadmin;
// /web a login code for the web admin, WebAdmin),
// the profiles (NetProfiles: /link /control /profile) and factions (NetFactions: /faction, /f); everywhere the arena matches
// (NetArena: /duel /accept /decline /ffa /arena /top) and, this game's own, the squad's distress calls (NetDistress:
// /sos, /assist <player>).
// Admins: the host's own player always, else the players the host or the server console made admins (IPilot.IsAdmin,
// for the session only: names aren't verified, so nothing is remembered by name), and on a server with profiles the
// profiles' admins and masters (NetPlayer.StaffRole, NetModeration).
// Players are named whole and case-insensitively, the longest name the arguments start with ("Player 2 hi"), by a client
// id as the first word, or by a selector like Minecraft's (FindTargets): @a everyone, @s yourself, @p the nearest other
// player, @r a random other player, and by state @alive (in space, not destroyed), @space, @docked, @dead (destroyed in
// space), @survivors (an event's players never destroyed since its fight began, EventRunner.Survived), @team (the event's
// players: a bar mission's team; while a mission's event runs a line, every selector only finds its team: Scope), any of them narrowed
// to one orbit by [orbit=<station>] (in its space or docked at its station: "@alive[orbit=78]", "@r[orbit=Var Hastra]");
// a command on several players runs for each ("/tp @a 78 dock", "/kick @r", "/reward @survivors 1000").

using System;
using System.Collections.Generic;
using System.Text;
using GoF2Remake.Data;
using Unity.Netcode;
using UnityEngine;
using GoF2Remake.Events;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetCommands
    {
        enum Arg { None, Text, Player, PlayerText }

        /// <summary>One Tab completion: the whole line it writes, and the suggestion row's name and description.</summary>
        public struct Completion
        {
            public string line, label, description;
        }

        sealed class Command
        {
            public string name, usage = "";
            public Arg arg;
            public bool self;                              // the player argument may be the issuer themselves (completion)
            public Func<string> description;
            public Func<bool> available;                   // this game: listed by /help and the suggestions
            public Action<string> local;                   // runs on this game; null = a server command
            public Func<IPilot, bool> allowed;          // the server: the issuer may run it (null issuer = the console)
            public Func<string, IPilot, string> run;    // the server: args, issuer -> the answer
            public bool needsPlayer;                       // only a player can run it (not the console)
            public bool optional;                          // the argument may be left out (the chat: yourself)
        }

        static bool Everyone() => true;

        static readonly (string, Func<string>)[] Selectors =
        {
            ("@a", () => X("mpSelAll", "everyone")),
            ("@s", () => X("mpSelSelf", "yourself")),
            ("@p", () => X("mpSelNearest", "the nearest player")),
            ("@r", () => X("mpSelRandom", "a random player")),
            ("@alive", () => X("mpSelAlive", "everyone alive in space")),
            ("@survivors", () => X("mpSelSurvivors", "the event's players never destroyed")),
            ("@team", () => X("mpSelTeam", "the event's players (a bar mission's team)")),
            ("@space", () => X("mpSelSpace", "everyone in space")),
            ("@docked", () => X("mpSelDocked", "everyone docked")),
            ("@dead", () => X("mpSelDead", "everyone destroyed in space")),
        };
        static bool Anyone(IPilot p) => true;

        static readonly Command[] Commands =
        {
            new Command { name = "help", available = Everyone, local = _ => Help(),
                description = () => X("mpCmdHelp", "lists the commands you can use") },
            new Command { name = "netstats", available = Everyone, local = _ => ToggleStats(),
                description = () => X("mpCmdNetstats", "shows or hides the network stats (ping, packet loss, data in / out)") },
            new Command { name = "version", available = Everyone,
                local = _ => NetChat.Notice(string.Format(X("mpVersion", "This game: build {0}"), UI.BuildVersion.Text)),
                description = () => X("mpCmdVersion", "this game's build (version and the code it was built from)") },
            new Command { name = "pos", available = Everyone, local = _ => NetChat.Notice(NetTeleport.Position()),
                description = () => X("mpCmdPos", "your orbit and coordinates (what /tp takes)") },

            new Command { name = "players", available = Everyone, allowed = Anyone, run = (_, by) => Players(by),
                description = () => X("mpCmdPlayers", "everyone in the session: where they are, ship, squad") },
            new Command { name = "g", usage = "<text>", arg = Arg.Text, available = Everyone, allowed = Anyone, run = (a, by) => Say(a, by, true),
                description = () => X("mpCmdGlobal", "sends one line to Global") },
            new Command { name = "l", usage = "<text>", arg = Arg.Text, available = Everyone, allowed = Anyone, run = (a, by) => Say(a, by, false),
                needsPlayer = true, description = () => X("mpCmdLocal", "sends one line to Local") },
            new Command { name = "w", usage = "<player> <text>", arg = Arg.PlayerText, available = Everyone, allowed = Anyone, run = Whisper,
                description = () => X("mpCmdWhisper", "a private message to one player") },
            new Command { name = "invite", usage = "<player>", arg = Arg.Player, available = Everyone, allowed = Anyone, run = Invite,
                needsPlayer = true, description = () => X("mpCmdInvite", "invites a player docked here to your squad") },
            new Command { name = "leave", available = Everyone, allowed = Anyone,
                run = (_, by) => NetArena.InMatch(by.OwnerClientId) ? NetArena.Command(by.OwnerClientId, "/leave") ?? "" : Leave(by),
                needsPlayer = true, description = () => X("mpCmdLeave", "leaves your arena queue or match, else your squad") },
            new Command { name = "kick", usage = "<player> [minutes] [reason]", arg = Arg.PlayerText, available = () => LocalIsOp, allowed = IsOp,
                run = (a, by) => NetProfiles.Enabled ? Moderate("kick", a, by) : Kick(a, by),
                description = () => X("mpCmdKick", "admins: removes a player from the session (with profiles: ops too, and they can't rejoin for the minutes, default 5)") },
            new Command { name = "tp", usage = NetTeleport.Usage, arg = Arg.PlayerText, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetTeleport.Command(a, by, false),
                description = () => X("mpCmdTp", "admins: teleports you or a player to a player, an orbit (x y z in game units) or a hangar (dock)") },
            new Command { name = "tphere", usage = "<player>", arg = Arg.Player, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetTeleport.Command(a, by, true), needsPlayer = true,
                description = () => X("mpCmdTpHere", "admins: brings a player to you") },
            new Command { name = "kill", usage = "[players]", arg = Arg.Player, optional = true, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = NetAdmin.Kill, description = () => X("mpCmdKill", "admins: destroys ships in space (yourself by default)") },
            new Command { name = "heal", usage = "[players]", arg = Arg.Player, optional = true, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = NetAdmin.Heal, description = () => X("mpCmdHeal", "admins: repairs hull, shield and armor (yourself by default)") },
            new Command { name = "give", usage = "[players] <item> [amount] [mount]", arg = Arg.PlayerText, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = NetAdmin.Give, description = () => X("mpCmdGive", "admins: items into the hold (an item's number or name)") },
            new Command { name = "credits", usage = "[players] <amount>", arg = Arg.PlayerText, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = NetAdmin.Credits, description = () => X("mpCmdCredits", "admins: gives credits (negative: takes them)") },
            new Command { name = "spawn", usage = "[players] <ship | object> [race] [count] [enemy | friendly | neutral | standing] [named <name>] [at x y z]", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Spawn,
                description = () => X("mpCmdSpawn", "admins: NPC ships (a number or name; enemy by default) or an object (assemblies.json name), ahead or at x y z; named: their name on the HUD") },
            new Command { name = "ship", usage = "[players] <ship | own>", arg = Arg.PlayerText, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = NetAdmin.Ship, description = () => X("mpCmdShip", "admins: flies any ship of the debug Ships tab (its number or name; own = back)") },
            new Command { name = "ammo", usage = "[players]", arg = Arg.Player, optional = true, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetAdmin.Simple(a, by, NetAdmin.Order.Ammo), description = () => X("mpCmdAmmo", "admins: every mounted secondary to 50") },
            new Command { name = "reveal", usage = "[players]", arg = Arg.Player, optional = true, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetAdmin.Simple(a, by, NetAdmin.Order.Reveal), description = () => X("mpCmdReveal", "admins: every system on the star map") },
            new Command { name = "peace", usage = "[players]", arg = Arg.Player, optional = true, self = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetAdmin.Simple(a, by, NetAdmin.Order.Peace), description = () => X("mpCmdPeace", "admins: neutral standings, no station grudges") },
            new Command { name = "cheat", usage = "[players] <god | ammo | cooldown | primary | boost | onehit | locks | shopping | jumps> [on | off]", arg = Arg.PlayerText,
                self = true, available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Cheat,
                description = () => X("mpCmdCheat", "admins: a debug toggle for a player, this session only (toggled without on / off)") },
            new Command { name = "title", usage = "[players] <text> [| subtitle] [for <seconds>]", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Title,
                description = () => X("mpCmdTitle", "admins: a big title on screen (\"clear\" removes it)") },
            new Command { name = "timer", usage = "[players] <seconds | m:ss> [label]", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Timer,
                description = () => X("mpCmdTimer", "admins: a countdown on screen (\"stop\" removes it)") },
            new Command { name = "dialog", usage = "[players] <speaker> : <text> [| [speaker :] next page ...]", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Dialog,
                description = () => X("mpCmdDialog", "admins: a conversation; speakers: a story character (Keith as Bob), a mod's character (mod_id:vega), a race and a name (vossk K'ekki), or player; %player% = the reader") },
            new Command { name = "reward", usage = "[players] <credits | item [amount]> [+ more ...] [| title]", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Reward,
                description = () => X("mpCmdReward", "admins: a mission payout (credits and / or items) in the reward box") },
            new Command { name = "pvp", usage = "<on | off>", arg = Arg.Text, available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Pvp,
                description = () => X("mpCmdPvp", "admins: free for all, every pilot an enemy of every other (squadmates excepted)") },
            new Command { name = "respawn", usage = "[players] <station [x y z] [spread <units>] [delay <seconds>] | off>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Respawn,
                description = () => X("mpCmdRespawn", "admins: where destroyed ships come back in space (not docked), until off") },
            new Command { name = "restrict", usage = "[players] <jumps | docking | all | off>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Restrict,
                description = () => X("mpCmdRestrict", "admins: no jumps (planet, gate, Khador) and / or no docking, until off") },
            new Command { name = "provoke", usage = "[players] [race] [within <metres>]", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Provoke,
                description = () => X("mpCmdProvoke", "admins: the NPC ships around them turn on them and their squad (a race, a distance)") },
            new Command { name = "radio", usage = "[players] <speaker> : <text>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Radio,
                description = () => X("mpCmdRadio", "admins: a radio call in flight (a face, a name, gone by itself); speakers as /dialog") },
            new Command { name = "waypoint", usage = "[players] <station x y z | off>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Waypoint,
                description = () => X("mpCmdWaypoint", "admins: a waypoint to fly to (game coordinates in a station's orbit; /pos shows yours)") },
            new Command { name = "cutscene", usage = "[players] <start [nobars] [freeze] [invulnerable] | end>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Cutscene,
                description = () => X("mpCmdCutscene", "admins: cinematic mode in flight (no HUD or controls, letterbox bars; freeze holds the ship) until end") },
            new Command { name = "camera", usage = "[players] <place [to place] [over s] [look place] [follow] [fov deg] [shake 0-1] | chase>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Camera,
                description = () => X("mpCmdCamera", "admins: a camera shot; places: player [right up forward], station [x y z], x y z, ship <name> [x y z]; chase ends it") },
            new Command { name = "npc", usage = "[players] <ship> <goto place | route place / place ... [loop] | follow | escort | attack <player | ship \"name\"> | flee | dock | hold | resume | jump | speed n> [speed n] [radius r] [fight] [then hold | resume | vanish | jump]",
                arg = Arg.PlayerText, self = true, available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Npc,
                description = () => X("mpCmdNpc", "admins: orders for spawned ships by name: fly somewhere, follow / escort, attack, flee, dock, hold, resume") },
            new Command { name = "fade", usage = "[players] <out | in> [seconds] [rrggbb colour] | clear", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Fade,
                description = () => X("mpCmdFade", "admins: fades the screen to a colour (out, held) or back (in)") },
            new Command { name = "letterbox", usage = "[players] <on | off>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Letterbox,
                description = () => X("mpCmdLetterbox", "admins: the cinema bars at the top and bottom of the screen") },
            new Command { name = "sound", usage = "[players] <sound>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Sound,
                description = () => X("mpCmdSound", "admins: plays a sound (alarm, warning, success, explosion...)") },
            new Command { name = "music", usage = "[players] <track | stop>", arg = Arg.PlayerText, self = true,
                available = () => LocalIsAdmin, allowed = IsAdmin, run = NetAdmin.Music,
                description = () => X("mpCmdMusic", "admins: plays a music track instead of the game's (battle, boss, void...), stop ends it") },
            new Command { name = "event", usage = "<name [setting=value ...] | stop | list>", arg = Arg.Text, optional = true, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = EventRunner.Command, description = () => X("mpCmdEvent", "admins: runs an event (game modes like waves), stops it or lists them") },
            new Command { name = "news", usage = "<text | clear>", arg = Arg.Text, available = () => LocalIsAdmin, allowed = IsAdmin, run = News,
                description = () => X("mpCmdNews", "admins: a GalNet item on every station's news ticker (\"clear\" empties the news)") },
            new Command { name = "mute", usage = "<players> [minutes]", arg = Arg.PlayerText, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetAdmin.Mute(a, by, true), description = () => X("mpCmdMute", "admins: blocks a player's chat (for the session or some minutes)") },
            new Command { name = "unmute", usage = "<players>", arg = Arg.Player, available = () => LocalIsAdmin, allowed = IsAdmin,
                run = (a, by) => NetAdmin.Mute(a, by, false), description = () => X("mpCmdUnmute", "admins: lets a muted player chat again") },
            new Command { name = "admin", usage = "<player>", arg = Arg.Player, available = () => LocalIsHost || LocalRole >= NetModeration.Master,
                allowed = p => NetProfiles.Enabled || IsHostPlayer(p),
                run = (a, by) => NetProfiles.Enabled ? Moderate("admin", a, by) : SetAdmin(a, by, true),
                description = () => X("mpCmdAdmin", "host: makes a player an admin for this session (with profiles: the master admin, for good)") },
            new Command { name = "unadmin", usage = "<player>", arg = Arg.Player, available = () => LocalIsHost || LocalRole >= NetModeration.Master,
                allowed = p => NetProfiles.Enabled || IsHostPlayer(p),
                run = (a, by) => NetProfiles.Enabled ? Moderate("unadmin", a, by) : SetAdmin(a, by, false),
                description = () => X("mpCmdUnadmin", "host: takes a player's admin rights away") },

            // A dedicated server with player profiles: moderation by the profiles' roles (NetModeration; it checks the role).
            new Command { name = "tempban", usage = "<player> <minutes> [reason]", arg = Arg.PlayerText, available = () => LocalIsOp, allowed = Anyone,
                run = (a, by) => Moderate("tempban", a, by), description = () => X("mpCmdTempban", "ops: bans a pilot and their devices for some minutes") },
            new Command { name = "ban", usage = "<player> [reason]", arg = Arg.PlayerText, available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("ban", a, by), description = () => X("mpCmdBan", "admins: bans a pilot and their devices for good") },
            new Command { name = "unban", usage = "<name | profile>", arg = Arg.Text, available = () => LocalIsOp, allowed = Anyone,
                run = (a, by) => Moderate("unban", a, by), description = () => X("mpCmdUnban", "ops: lifts a ban") },
            new Command { name = "bans", available = () => LocalIsOp, allowed = Anyone,
                run = (a, by) => Moderate("bans", a, by), description = () => X("mpCmdBans", "ops: the bans") },
            new Command { name = "op", usage = "<player>", arg = Arg.Player, available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("op", a, by), description = () => X("mpCmdOp", "admins: makes a pilot an op (kicks, temporary bans)") },
            new Command { name = "deop", usage = "<player>", arg = Arg.Player, available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("deop", a, by), description = () => X("mpCmdDeop", "admins: takes a pilot's op role away") },
            new Command { name = "staff", available = Everyone, allowed = Anyone,
                run = (a, by) => Moderate("staff", a, by), description = () => X("mpCmdStaff", "the server's master admins, admins and ops") },
            new Command { name = "say", usage = "<text>", arg = Arg.Text, available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("say", a, by), needsPlayer = true, description = () => X("mpCmdSay", "admins: an announcement under your role") },
            new Command { name = "disband", usage = "<TAG>", arg = Arg.Text, available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("disband", a, by), description = () => X("mpCmdDisband", "admins: ends a faction") },
            new Command { name = "deleteprofile", usage = "<profile>", arg = Arg.Text, available = () => LocalRole >= NetModeration.Master, allowed = Anyone,
                run = (a, by) => Moderate("deleteprofile", a, by), description = () => X("mpCmdDeleteProfile", "master: deletes an offline pilot's profile") },
            new Command { name = "settings", available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("settings", a, by), description = () => X("mpCmdSettings", "admins: the server's settings") },
            new Command { name = "set", usage = "<key> <value>", arg = Arg.Text, available = () => LocalRole >= NetModeration.Admin, allowed = Anyone,
                run = (a, by) => Moderate("set", a, by), description = () => X("mpCmdSet", "admins: changes a server setting (saved)") },
            new Command { name = "web", available = () => ProfilesOn && LocalIsOp, allowed = Anyone, needsPlayer = true,
                run = (a, by) => WebAdmin.CodeFor(by as NetPlayer),
                description = () => X("mpCmdWeb", "ops: a one-time login code for the server's web admin") },
            new Command { name = "claimadmin", usage = "<token>", arg = Arg.Text, available = Everyone, allowed = Anyone,
                run = (a, by) => Moderate("claimadmin", a, by), needsPlayer = true,
                description = () => X("mpCmdClaimAdmin", "makes you the server's master admin with its admin token") },

            // Player profiles (NetProfiles), factions (NetFactions), arena matches (NetArena): answered by their own handlers.
            new Command { name = "link", usage = "[code] [force]", arg = Arg.Text, optional = true, available = Everyone, allowed = Anyone,
                run = (a, by) => Handler(NetProfiles.Command, true, "link", a, by), needsPlayer = true,
                description = () => X("mpCmdLink", "a code to join this profile from another device, or joins one with its code") },
            new Command { name = "control", available = Everyone, allowed = Anyone,
                run = (a, by) => Handler(NetProfiles.Command, true, "control", a, by), needsPlayer = true,
                description = () => X("mpCmdControl", "this device takes control of your profile") },
            new Command { name = "profile", available = Everyone, allowed = Anyone,
                run = (a, by) => Handler(NetProfiles.Command, true, "profile", a, by), needsPlayer = true,
                description = () => X("mpCmdProfile", "your profile on this server") },
            new Command { name = "faction", usage = "<create | invite | join | leave | kick | promote | demote | leader | disband | info | list | deposit | withdraw | claim | unclaim | home | claims | siege | sieges> ...",
                arg = Arg.Text, optional = true, available = Everyone, allowed = Anyone, run = (a, by) => Handler(NetFactions.Command, true, "faction", a, by), needsPlayer = true,
                description = () => X("mpCmdFaction", "your faction: members, bank, claimed stations, sieges") },
            new Command { name = "f", usage = "<text>", arg = Arg.Text, available = Everyone, allowed = Anyone, needsPlayer = true,
                run = (a, by) => NetAdmin.IsMuted(by.OwnerClientId, out string muted) ? muted : Handler(NetFactions.Command, true, "f", a, by),
                description = () => X("mpCmdFactionChat", "one line to your faction's pilots online") },
            new Command { name = "duel", usage = "<player> [voids]", arg = Arg.PlayerText, available = Everyone, allowed = Anyone,
                run = (a, by) => Handler(NetArena.Command, false, "duel", a, by), needsPlayer = true,
                description = () => X("mpCmdDuel", "challenges a docked pilot to an arena duel (voids: with the Void fighters)") },
            new Command { name = "accept", available = Everyone, allowed = Anyone, run = (a, by) => Handler(NetArena.Command, false, "accept", a, by), needsPlayer = true,
                description = () => X("mpCmdAccept", "accepts an arena challenge") },
            new Command { name = "decline", available = Everyone, allowed = Anyone, run = (a, by) => Handler(NetArena.Command, false, "decline", a, by), needsPlayer = true,
                description = () => X("mpCmdDecline", "declines an arena challenge") },
            new Command { name = "ffa", usage = "[voids]", arg = Arg.Text, optional = true, available = Everyone, allowed = Anyone,
                run = (a, by) => Handler(NetArena.Command, false, "ffa", a, by), needsPlayer = true,
                description = () => X("mpCmdFfa", "queues for an arena free-for-all") },
            new Command { name = "arena", available = Everyone, allowed = Anyone, run = (a, by) => Handler(NetArena.Command, false, "arena", a, by), needsPlayer = true,
                description = () => X("mpCmdArena", "the arena matches and queues") },
            new Command { name = "top", available = Everyone, allowed = Anyone, run = (a, by) => Handler(NetArena.Command, false, "top", a, by), needsPlayer = true,
                description = () => X("mpCmdTop", "the arena leaderboard") },

            // The squad's distress calls (NetDistress): this game's own.
            new Command { name = "sos", available = Everyone, local = _ => Sos(),
                description = () => X("mpCmdSos", "in space: calls your squad for help, or ends the call") },
            new Command { name = "assist", usage = "<player>", arg = Arg.Player, available = Everyone, local = Assist,
                description = () => X("mpCmdAssist", "flies to a squadmate calling for help") },
            new Command { name = "squad", usage = "[invite <player> | accept | decline | leave]", arg = Arg.Text, optional = true, available = Everyone,
                local = Squad, description = () => X("mpCmdSquad", "your squad; invite a pilot docked at your station, answer an invitation, leave") },
            // Trades between players (NetTradeClient / NetTrade): asked and answered by this game, the server checks them.
            new Command { name = "trade", usage = "[<player> | accept | decline | cancel]", arg = Arg.Text, optional = true, available = Everyone,
                local = Trade, description = () => X("mpCmdTrade", "trades credits and cargo with a pilot docked at your station") },
        };

        static bool ProfilesOn => NetState.Instance != null && NetState.Instance.ProfilesOn;

        /// <summary>This player's server role (NetModeration; 0 without profiles).</summary>
        static int LocalRole => NetPlayer.Local != null ? NetPlayer.Local.StaffRole : 0;

        /// <summary>This player may use the moderation commands: an admin, or an op of a server with profiles.</summary>
        static bool LocalIsOp => LocalIsAdmin || LocalRole >= NetModeration.Op;

        /// <summary>Server: 'p' may kick: an admin, or an op of a server with profiles.</summary>
        static bool IsOp(IPilot p) => IsAdmin(p) || (p is NetPlayer np && np.StaffRole >= NetModeration.Op);

        /// <summary>Server: a moderation command (NetModeration checks the role; the console outranks everyone).</summary>
        static string Moderate(string name, string args, IPilot by)
        {
            if (!NetProfiles.Enabled) return ModerateWithoutProfiles(name, args, by);
            return (by == null ? NetModeration.ConsoleCommand(name, args) : NetModeration.Command(by.OwnerClientId, $"/{name} {args}")) ?? "";
        }

        /// <summary>A session without profiles (hosted fresh from the menu): the settings and announcements still work for
        /// its admins (the host); bans, ops and the rest need profiles.</summary>
        static string ModerateWithoutProfiles(string name, string args, IPilot by)
        {
            bool admin = by == null || IsAdmin(by);
            switch (name)
            {
                case "settings":
                    return admin ? NetServerSettings.ListText() : X("mpCmdNoRights", "You don't have the rights for that command.");
                case "set":
                {
                    if (!admin) return X("mpCmdNoRights", "You don't have the rights for that command.");
                    args = (args ?? "").Trim();
                    int sp = args.IndexOf(' ');
                    return sp < 0 ? "/set <key> <value> (/settings lists them)" : NetServerSettings.Set(args.Substring(0, sp), args.Substring(sp + 1), IssuerName(by));
                }
                case "say":
                {
                    if (!admin) return X("mpCmdNoRights", "You don't have the rights for that command.");
                    string text = NetChat.Clean(args);
                    if (text.Length == 0) return "/say <text>";
                    NetState.Instance?.ServerChat(by == null ? IssuerName(null) : $"[admin] {by.DisplayName}", text);
                    return "";
                }
                default:
                    return X("mpCmdNeedsProfiles", "That needs player profiles: a dedicated server, or a game hosted with World: Persistent.");
            }
        }

        /// <summary>Server: a command answered by a (client, "/name args") handler (NetArena, NetFactions, NetProfiles);
        /// 'profiles' = it needs a server with player profiles.</summary>
        static string Handler(Func<ulong, string, string> handler, bool profiles, string name, string args, IPilot by)
        {
            if (by == null) return "";
            if (profiles && !NetProfiles.Enabled) return X("mpCmdNeedsProfiles", "That needs player profiles: a dedicated server, or a game hosted with World: Persistent.");
            return handler(by.OwnerClientId, $"/{name} {args}".Trim()) ?? "";
        }

        /// <summary>/news: an admin's item on the stations' tickers (NetNews), or "clear".</summary>
        static string News(string args, IPilot by)
        {
            args = (args ?? "").Trim();
            if (args.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                NetNews.Clear();
                Debug.Log($"Server: {IssuerName(by)} cleared the news.");
                return X("mpNewsCleared", "The news is cleared.");
            }
            if (args.Length > NetNews.MaxTextLength) return string.Format(X("mpNewsLong", "At most {0} characters."), NetNews.MaxTextLength);
            Debug.Log($"Server: {IssuerName(by)} posted news.");
            return NetNews.Post(NetNews.Kind.Galnet, NetNews.Safe(args)) ? X("mpNewsPosted", "On every station's ticker now.") : X("mpNewsFailed", "Not posted.");
        }

        /// <summary>/sos: the squad's distress call (NetDistress; only in space and in a squad, else why not).</summary>
        static void Sos()
        {
            if (!NetSquad.InSquad && !NetDistress.Active)
                NetChat.Notice(X("mpSosNoSquad", "Distress calls go to your squad: form one first (docked: /squad invite <pilot>)."));
            else NetChat.Notice(NetDistress.Toggle());
        }

        /// <summary>/squad: who is in it and where; invite, accept, decline, leave (the same as the squad window's buttons).</summary>
        static void Squad(string args)
        {
            args = (args ?? "").Trim();
            int space = args.IndexOf(' ');
            string sub = (space < 0 ? args : args.Substring(0, space)).ToLowerInvariant();
            string rest = space < 0 ? "" : args.Substring(space + 1).Trim();
            switch (sub)
            {
                case "":
                case "info":
                {
                    var members = NetSquad.Members();
                    var waiting = NetSquad.Invites;
                    if (members.Count == 0)
                    {
                        NetChat.Notice(waiting.Count > 0
                            ? string.Format(X("mpSquadWaiting", "{0} invites you to their squad: /squad accept or /squad decline."), waiting[waiting.Count - 1].name)
                            : X("mpSquadNone", "You aren't in a squad. Docked, /squad invite <pilot> invites a pilot docked at the same station."));
                        return;
                    }
                    var sb = new StringBuilder(X("mpSquadList", "Your squad:"));
                    foreach (var m in members)
                        sb.Append('\n').Append(m.Distress ? "⚠ " : "").Append(m.DisplayName).Append(m.IsOwner ? " " + X("mpCmdYou", "(you)") : "")
                          .Append(": ").Append(WhereText(m));
                    NetChat.Notice(sb.ToString());
                    return;
                }
                case "invite":
                {
                    var p = MatchPlayer(rest, out _) as NetPlayer;   // squads are session players
                    if (p == null) { NetChat.Notice(string.Format(X("mpSquadNoPilot", "No pilot called \"{0}\" online."), rest)); return; }
                    if (p.IsOwner) { NetChat.Notice(X("mpSquadSelf", "That's you.")); return; }
                    if (NetSquad.Same(p, NetPlayer.Local)) { NetChat.Notice(string.Format(X("mpSquadAlready", "{0} is in your squad already."), p.DisplayName)); return; }
                    var me = NetPlayer.Local;
                    if (me == null || !me.InHangar || !p.InHangar || me.Station != p.Station)
                    {
                        NetChat.Notice(X("mpSquadHangarOnly", "Squads can only be formed while docked in the same hangar."));
                        return;
                    }
                    NetSquad.InviteTo(p);
                    NetChat.Notice(string.Format(X("mpSquadSent", "Invitation sent to {0} (45 seconds)."), p.DisplayName));
                    return;
                }
                case "accept":
                {
                    var waiting = NetSquad.Invites;
                    if (waiting.Count == 0) { NetChat.Notice(X("mpSquadNoInvite", "No squad invitation is waiting.")); return; }
                    NetSquad.Accept(waiting[waiting.Count - 1]);
                    return;
                }
                case "decline":
                {
                    var waiting = NetSquad.Invites;
                    if (waiting.Count == 0) { NetChat.Notice(X("mpSquadNoInvite", "No squad invitation is waiting.")); return; }
                    NetSquad.Decline(waiting[waiting.Count - 1]);
                    NetChat.Notice(X("mpSquadDeclined", "Invitation declined."));
                    return;
                }
                case "leave":
                    if (!NetSquad.InSquad) { NetChat.Notice(X("mpCmdNoSquad", "You're not in a squad.")); return; }
                    NetSquad.Leave();
                    return;
                default:
                    NetChat.Notice("/squad [invite <player> | accept | decline | leave]");
                    return;
            }
        }

        /// <summary>/trade: ask a pilot docked here, answer a request, cancel the open trade (the Multiplayer window's Trade tab).</summary>
        static void Trade(string args)
        {
            args = (args ?? "").Trim();
            string sub = args.ToLowerInvariant();
            var waiting = NetTradeClient.Requests;
            switch (sub)
            {
                case "":
                    if (NetTradeClient.Current != null) { UI.MultiplayerWindow.OpenTradeAny(); return; }
                    NetChat.Notice(waiting.Count > 0
                        ? string.Format(X("mpTradeWaitingCmd", "{0} wants to trade with you: /trade accept or /trade decline."), waiting[waiting.Count - 1].name)
                        : "/trade [<player> | accept | decline | cancel]");
                    return;
                case "accept":
                    if (waiting.Count == 0) { NetChat.Notice(X("mpTradeNoRequest", "No trade request is waiting.")); return; }
                    NetTradeClient.Accept(waiting[waiting.Count - 1]);
                    return;
                case "decline":
                    if (waiting.Count == 0) { NetChat.Notice(X("mpTradeNoRequest", "No trade request is waiting.")); return; }
                    NetTradeClient.Decline(waiting[waiting.Count - 1]);
                    return;
                case "cancel":
                    if (NetTradeClient.Current == null) { NetChat.Notice(X("mpTradeNone", "You aren't trading.")); return; }
                    NetTradeClient.Cancel();
                    return;
            }
            var p = MatchPlayer(args, out _) as NetPlayer;
            if (p == null) { NetChat.Notice(string.Format(X("mpSquadNoPilot", "No pilot called \"{0}\" online."), args)); return; }
            if (p.IsOwner) { NetChat.Notice(X("mpTradeSelf", "You can't trade with yourself.")); return; }
            var me = NetPlayer.Local;
            if (me == null || !me.InHangar || !p.InHangar || me.Station != p.Station)
            {
                NetChat.Notice(X("mpTradeHangarOnly", "Trades are only possible while docked in the same hangar."));
                return;
            }
            NetTradeClient.Ask(p);   // the server answers (sent, or why not)
        }

        /// <summary>/assist: help a squadmate calling (NetDistress).</summary>
        static void Assist(string who)
        {
            var caller = MatchPlayer(who, out _) as NetPlayer;
            NetChat.Notice(caller != null && NetSquad.Same(caller, NetPlayer.Local) ? NetDistress.Help(caller) ?? ""
                : string.Format(X("mpHelpNoMate", "No squadmate called \"{0}\"."), who));
        }

        static string X(string key, string english) => Localization.Extra(key, english);

        static bool LocalIsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        /// <summary>This player may use the admin commands: the host, made an admin by the host / the server console, or a
        /// profile's admin / master (NetModeration).</summary>
        public static bool LocalIsAdmin => LocalIsHost || (NetPlayer.Local != null && (NetPlayer.Local.IsAdmin || NetPlayer.Local.StaffRole >= NetModeration.Admin));

        /// <summary>Server: 'p' has admin rights (the host's own player, made an admin, or a profile's admin / master).</summary>
        public static bool IsAdmin(IPilot p) => p != null && (p.IsAdmin || IsHostPlayer(p) || (p is NetPlayer np && np.StaffRole >= NetModeration.Admin));

        internal static bool IsHostPlayer(IPilot p) =>
            p != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && p.OwnerClientId == NetworkManager.ServerClientId;

        static bool Ready => NetState.Instance != null && NetState.Instance.IsSpawned;

        static Command Get(string name) => Array.Find(Commands, c => c.name == name);

        static string Usage(Command c) => $"/{c.name}{(c.usage.Length > 0 ? " " + c.usage : "")}";

        /// <summary>"/name usage" of a command (NetAdmin's answers).</summary>
        internal static string UsageOf(string name) { var c = Get(name); return c != null ? Usage(c) : "/" + name; }

        // ---- this game ----------------------------------------------------------------------------------------

        /// <summary>A chat line starting with "/": runs it here or sends it to the server (true = it was a command, nothing
        /// is sent as chat).</summary>
        public static bool TryRun(string line)
        {
            if (string.IsNullOrEmpty(line) || line[0] != '/') return false;
            string body = line.Substring(1).Trim();
            int space = body.IndexOf(' ');
            string name = (space < 0 ? body : body.Substring(0, space)).ToLowerInvariant();
            string args = space < 0 ? "" : body.Substring(space + 1).Trim();
            var command = Array.Find(Commands, c => c.name == name && c.available());
            if (command == null)
                NetChat.Notice(string.Format(X("mpCmdUnknown", "Unknown command /{0}. Type /help for the commands you can use."), name));
            else if (command.arg != Arg.None && args.Length == 0 && !command.optional)
                NetChat.Notice(Usage(command));
            else if (command.local != null)
                command.local(args);
            else if (Ready)
                NetState.Instance.ServerCommandRpc(command.name, args);
            return true;
        }

        static void Help()
        {
            NetChat.Notice(X("mpCmdList", "Commands:"));
            foreach (var c in Commands)
                if (c.available()) NetChat.Notice($"{Usage(c)}: {c.description()}");
        }

        static void ToggleStats()
        {
            NetStats.Shown = !NetStats.Shown;
            NetChat.Notice(NetStats.Shown ? X("mpStatsOn", "Network stats on (/netstats hides them).") : X("mpStatsOff", "Network stats off."));
        }

        // ---- the server: one implementation for the chat and the dedicated server's console -------------------

        /// <summary>The server commands' names (the console's commands and Tab completion).</summary>
        public static IEnumerable<string> ServerCommandNames
        {
            get { foreach (var c in Commands) if (c.run != null) yield return c.name; }
        }

        /// <summary>The console's help for the server commands: "name usage: description" lines.</summary>
        public static string ServerCommandHelp()
        {
            var sb = new StringBuilder();
            foreach (var c in Commands)
                if (c.run != null && !c.needsPlayer) sb.Append($"\n  {c.name}{(c.usage.Length > 0 ? " " + c.usage : "")}: {c.description()}");
            return sb.ToString();
        }

        /// <summary>Server: runs command 'name' for 'issuer' (null = the dedicated server's console) after checking its
        /// rights; the answer for the issuer, null when 'name' is no server command.</summary>
        public static string RunOnServer(string name, string args, IPilot issuer)
        {
            var c = Get((name ?? "").ToLowerInvariant());
            if (c == null || c.run == null) return null;
            if (!EventHost.ServerReady) return X("mpCmdNoServer", "The server isn't running.");
            if (issuer == null && c.needsPlayer) return string.Format(X("mpCmdPlayersOnly", "/{0} is for players in the chat."), c.name);
            if (issuer != null && !c.allowed(issuer)) return X("mpCmdNoRights", "You don't have the rights for that command.");
            args = (args ?? "").Trim();
            if (c.arg != Arg.None && args.Length == 0 && !c.optional) return Usage(c);
            return c.run(args, issuer) ?? "";
        }

        /// <summary>The issuer's name in notices and the log ("Server" for the console).</summary>
        public static string IssuerName(IPilot issuer) => issuer != null ? issuer.DisplayName : X("mpServerName", "Server");

        /// <summary>The player whose name 'args' starts with (whole name, any case, the longest one), the rest after it.</summary>
        internal static IPilot MatchPlayer(string args, out string rest)
        {
            IPilot best = null;
            rest = "";
            foreach (var p in EventHost.Pilots)
            {
                if (p == null || !p.IsSpawned) continue;
                string n = p.DisplayName;
                if (n.Length == 0 || !args.StartsWith(n, StringComparison.OrdinalIgnoreCase)) continue;
                if (args.Length > n.Length && args[n.Length] != ' ') continue;
                if (best == null || n.Length > best.DisplayName.Length) best = p;
            }
            if (best != null) rest = args.Substring(best.DisplayName.Length).Trim();
            return best;
        }

        // ---- player arguments: names, client ids and selectors ---------------------------------------------

        /// <summary>The players the arguments start with (Minecraft-style): a selector, @a everyone, @s the issuer, @p the
        /// nearest other player (the same orbit by distance, else the same station, else anyone), @r a random other
        /// player; else a whole name (MatchPlayer) or a client id. A selector may carry a filter in brackets:
        /// [orbit=&lt;station&gt;] (a number, a name or "void") keeps the players in that orbit or docked at its station
        /// ("@alive[orbit=78]", "@r[orbit=Var Hastra]"). 'error' says why none.</summary>
        public static List<IPilot> FindTargets(string args, IPilot issuer, out string rest, out string error)
        {
            var list = new List<IPilot>();
            error = null;
            args = (args ?? "").Trim();
            int space = args.IndexOf(' ');
            string first = space < 0 ? args : args.Substring(0, space);
            rest = space < 0 ? "" : args.Substring(space + 1).Trim();
            Func<IPilot, bool> where = p => true;
            int open = args.StartsWith("@") ? args.IndexOf('[') : -1;
            if (open > 0 && (space < 0 || open < space))
            {
                int close = args.IndexOf(']', open);
                if (close < 0) { error = X("mpSelBracket", "A selector's [ needs its ]."); return list; }
                if (!SelectorFilter(args.Substring(open + 1, close - open - 1), out where, out error)) return list;
                first = args.Substring(0, open);
                rest = args.Substring(close + 1).Trim();
            }
            if (Scope != null && first.StartsWith("@"))
            {
                // A bar mission's event (EventRunner): its selectors only find its own players.
                var filter = where;
                var scope = Scope;
                where = p => filter(p) && scope(p);
            }
            if (first.Length > 2 && first[0] == '@')
            {
                // By state: everyone it holds for.
                Func<IPilot, bool> holds;
                switch (first.ToLowerInvariant())
                {
                    case "@alive": holds = p => p.InSpace && p.Hull > 0f; break;
                    case "@space": holds = p => p.InSpace; break;
                    case "@docked": holds = p => p.InHangar; break;
                    case "@dead": holds = p => p.InSpace && p.Hull <= 0f; break;
                    case "@survivors": holds = EventRunner.Survived; break;
                    case "@team": holds = p => true; break;   // the event's players (a mission's team: Scope)
                    default:
                        error = string.Format(X("mpSelUnknownMore", "Unknown selector {0}: @a, @s, @p, @r, @alive, @survivors, @team, @space, @docked, @dead."), first);
                        return list;
                }
                foreach (var p in EventHost.Pilots) if (p != null && p.IsSpawned && holds(p) && where(p)) list.Add(p);
                if (list.Count == 0) error = NobodyMatches;
                return list;
            }
            if (first.Length == 2 && first[0] == '@')
            {
                char k = char.ToLowerInvariant(first[1]);
                var others = new List<IPilot>();
                foreach (var p in EventHost.Pilots) if (p != null && p.IsSpawned && p != issuer && where(p)) others.Add(p);
                switch (k)
                {
                    case 'a':
                        foreach (var p in EventHost.Pilots) if (p != null && p.IsSpawned && where(p)) list.Add(p);
                        break;
                    case 's':
                        if (issuer == null) { error = X("mpSelNoSelf", "@s is the player typing the command: not for the console."); return list; }
                        if (where(issuer)) list.Add(issuer);
                        break;
                    case 'p':
                        if (issuer == null) { error = X("mpSelNoNearest", "@p is nearest to the player typing the command: not for the console."); return list; }
                        var near = Nearest(issuer, others);
                        if (near != null) list.Add(near);
                        break;
                    case 'r':
                        if (others.Count > 0) list.Add(others[UnityEngine.Random.Range(0, others.Count)]);
                        break;
                    default:
                        error = string.Format(X("mpSelUnknown", "Unknown selector {0}: @a everyone, @s yourself, @p the nearest player, @r a random player."), first);
                        return list;
                }
                if (list.Count == 0) error = NobodyMatches;
                return list;
            }
            var named = MatchPlayer(args, out rest);
            if (named == null && ulong.TryParse(first.TrimStart('#'), out ulong id) && (named = EventHost.Find(id)) != null)   // "5" or "#5"
                rest = space < 0 ? "" : args.Substring(space + 1).Trim();
            if (named != null) list.Add(named);
            else { rest = ""; error = NoPlayer(first.Length > 0 ? first : args); }
            return list;
        }

        /// <summary>While a bar mission's event runs a line (EventRunner.Tick): the players its selectors may find (null: all).</summary>
        internal static Func<IPilot, bool> Scope;

        /// <summary>FindTargets' error when the selector is fine but nobody matches (count() and points take it as none).</summary>
        public static string NobodyMatches => X("mpSelNobody", "No player matches that.");

        /// <summary>A selector's bracket filter: "orbit=&lt;station&gt;" (comma-separated, all must hold).</summary>
        static bool SelectorFilter(string text, out Func<IPilot, bool> where, out string error)
        {
            where = p => true;
            error = null;
            foreach (string part in text.Split(','))
            {
                int eq = part.IndexOf('=');
                string key = (eq < 0 ? part : part.Substring(0, eq)).Trim().ToLowerInvariant(), value = eq < 0 ? "" : part.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;
                if (key == "orbit")
                {
                    if (!NetTeleport.ParseStation(value, out int station, out string left) || left.Length > 0)
                    {
                        error = string.Format(X("mpSelNoStation", "No station \"{0}\" (a number, a name or void)."), value);
                        return false;
                    }
                    var before = where;
                    where = p => before(p) && p.Station == station;
                    continue;
                }
                error = string.Format(X("mpSelFilter", "Unknown selector filter \"{0}\": [orbit=<station>]."), key);
                return false;
            }
            return true;
        }

        /// <summary>The one player the arguments start with (a selector that picks one, a name or a client id).</summary>
        public static IPilot FindTarget(string args, IPilot issuer, out string rest, out string error)
        {
            var list = FindTargets(args, issuer, out rest, out error);
            if (list.Count > 1) { error = X("mpSelOne", "Only one player here (not @a)."); return null; }
            return list.Count == 1 ? list[0] : null;
        }

        /// <summary>The other player nearest to 'from': in the same orbit by distance, else docked at the same station, else
        /// the first one.</summary>
        static IPilot Nearest(IPilot from, List<IPilot> others)
        {
            IPilot best = null;
            float bestD = float.MaxValue;
            foreach (var p in others)
            {
                float d = from.InSpace && p.InSpace && p.Station == from.Station ? (p.Position - from.Position).sqrMagnitude
                    : p.Station == from.Station ? 1e20f : 1e30f;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        static string NoPlayer(string args) => string.Format(X("mpCmdNoPlayer", "No player \"{0}\". /players lists them."), args);

        /// <summary>Runs 'each' for every player the arguments name (the rest of the line passed on) and joins the answers.
        /// The issuer is left out of a selector's list; named alone, "Not on yourself." unless 'self'.</summary>
        static string ForEachTarget(string args, IPilot by, bool self, Func<IPilot, string, string> each)
        {
            var targets = FindTargets(args, by, out string rest, out string error);
            if (targets.Count == 0) return error;
            bool selector = args.TrimStart().StartsWith("@");
            var answers = new List<string>();
            foreach (var t in targets)
            {
                if (t == by && !self)
                {
                    if (!selector || targets.Count == 1) answers.Add(X("mpCmdNotYourself", "Not on yourself."));
                    continue;
                }
                string a = each(t, rest);
                if (!string.IsNullOrEmpty(a)) answers.Add(a);
            }
            return string.Join("\n", answers);
        }

        static string Players(IPilot by)
        {
            bool ids = by == null || IsAdmin(by);
            var sb = new StringBuilder();
            int n = 0;
            foreach (var p in EventHost.Pilots)
            {
                if (p == null || !p.IsSpawned) continue;
                n++;
                sb.Append('\n');
                if (ids) sb.Append('#').Append(p.OwnerClientId).Append(' ');
                sb.Append(p.DisplayName);
                if (p == by) sb.Append(' ').Append(X("mpCmdYou", "(you)"));
                sb.Append(": ").Append(WhereText(p)).Append(", ").Append(UI.ItemInfo.ShipName(p.ShipIndex));
                if (p.SquadId != 0) sb.Append(", ").Append(p != by && NetSquad.Same(p as NetPlayer, by as NetPlayer) ? X("mpCmdYourSquad", "your squad") : X("mpCmdInSquad", "in a squad"));
                if (IsAdmin(p)) sb.Append(", ").Append(X("mpCmdAdminTag", "admin"));
            }
            return n == 0 ? X("mpCmdNoPlayers", "No players online.") : string.Format(X("mpCmdPlayerCount", "{0} player(s):"), n) + sb;
        }

        /// <summary>Where a player is, for /players (and the suggestions).</summary>
        public static string WhereText(IPilot p)
        {
            string station = DedicatedServer.StationName(p.Station);
            switch (p.Where)
            {
                case NetPlayer.Place.Space: return string.Format(X("mpWhereSpace", "in space at {0}"), station);
                case NetPlayer.Place.Hangar: return string.Format(X("mpWhereDocked", "docked at {0}"), station);
                case NetPlayer.Place.Departing: return string.Format(X("mpWhereDeparting", "taking off from {0}"), station);
                default: return X("mpWhereLoading", "loading");
            }
        }

        static string Say(string text, IPilot by, bool global)
        {
            text = NetChat.Clean(text);
            if (text.Length == 0) return Usage(Get(global ? "g" : "l"));
            NetState.Instance.Chat(by as NetPlayer, text, global);
            return "";
        }

        static string Whisper(string args, IPilot by) => ForEachTarget(args, by, false, (to, text) =>
        {
            text = NetChat.Clean(text);
            if (text.Length == 0) return Usage(Get("w"));
            NetState.Instance.Whisper(by as NetPlayer, to as NetPlayer, text);
            return "";
        });

        static string Invite(string args, IPilot by) => ForEachTarget(args, by, false, (to, _) =>
        {
            if (NetSquad.Same(to as NetPlayer, by as NetPlayer)) return string.Format(X("mpCmdAlreadySquad", "{0} is already in your squad."), to.DisplayName);
            if (!by.InHangar || !to.InHangar || to.Station != by.Station)
                return string.Format(X("mpCmdInviteHangar", "{0}: squads can only be formed while docked in the same hangar."), to.DisplayName);
            NetState.Instance.Invite(by as NetPlayer, to as NetPlayer);
            return string.Format(X("mpCmdInvited", "Invitation sent to {0}."), to.DisplayName);
        });

        static string Leave(IPilot by)
        {
            if (by.SquadId == 0) return X("mpCmdNoSquad", "You're not in a squad.");
            NetState.Instance.LeaveSquad(by as NetPlayer);
            return "";
        }

        static string Kick(string args, IPilot by) => ForEachTarget(args, by, false, (to, reason) =>
        {
            string name = to.DisplayName;
            if (IsHostPlayer(to) || (to.IsAdmin && by != null && !IsHostPlayer(by)))
                return string.Format(X("mpKickRefusedName", "{0} can't be kicked."), name);
            reason = NetChat.Clean(reason);
            string message = by == null && reason.Length == 0 ? X("mpKicked", "The server removed you from the session.")
                : reason.Length > 0 ? string.Format(X("mpKickedBy", "{0} removed you from the session: {1}"), IssuerName(by), reason)
                : string.Format(X("mpKickedByNoReason", "{0} removed you from the session."), IssuerName(by));
            if (!NetGame.Kick(to.OwnerClientId, message)) return string.Format(X("mpKickRefusedName", "{0} can't be kicked."), name);
            Debug.Log($"Server: {IssuerName(by)} kicked {name} ({to.OwnerClientId}){(reason.Length > 0 ? ": " + reason : "")}");
            NetState.Instance.NoticeAll(string.Format(X("mpKickedNotice", "{0} was removed from the session by {1}."), name, IssuerName(by)));
            return "";
        });

        static string SetAdmin(string args, IPilot by, bool on) => ForEachTarget(args, by, false, (to, _) =>
        {
            NetState.Instance.SetAdmin(to as NetPlayer, on);   // logged there
            return string.Format(on ? X("mpAdminGranted", "{0} is now an admin.") : X("mpAdminRevoked", "{0} is no longer an admin."), to.DisplayName);
        });

        // ---- completion (ChatView) --------------------------------------------------------------------------

        /// <summary>What Tab can write for the line typed so far: the commands its name starts with ("/" alone: all), or after a
        /// command that takes a player, the players whose name starts with what follows it. Empty = nothing to complete.</summary>
        public static List<Completion> Completions(string line)
        {
            var list = new List<Completion>();
            if (string.IsNullOrEmpty(line) || line[0] != '/') return list;
            int space = line.IndexOf(' ');
            if (space < 0)
            {
                string prefix = line.Substring(1).ToLowerInvariant();
                foreach (var c in Commands)
                    if (c.available() && c.name.StartsWith(prefix, StringComparison.Ordinal))
                        list.Add(new Completion { line = "/" + c.name + (c.arg != Arg.None ? " " : ""), label = Usage(c), description = c.description() });
                return list;
            }
            var command = Get(line.Substring(1, space - 1).ToLowerInvariant());
            if (command == null || !command.available() || (command.arg != Arg.Player && command.arg != Arg.PlayerText)) return list;
            string typed = line.Substring(space + 1);
            foreach (var (sel, what) in Selectors)
                if (sel.StartsWith(typed, StringComparison.OrdinalIgnoreCase) && typed.IndexOf(' ') < 0)
                    list.Add(new Completion { line = $"/{command.name} {sel}{(command.arg == Arg.PlayerText ? " " : "")}", label = sel, description = what() });
            foreach (var p in EventHost.Pilots)
            {
                if (p == null || !p.IsSpawned || (p.IsOwner && !command.self)) continue;
                string n = p.DisplayName;
                if (!n.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new Completion
                {
                    line = $"/{command.name} {n}{(command.arg == Arg.PlayerText ? " " : "")}",
                    label = n, description = WhereText(p),
                });
            }
            return list;
        }
    }
}
