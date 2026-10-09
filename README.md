![Galaxy on Fire 2 Remake main menu](.github/header.webp)

# Galaxy on Fire 2 Remake (Unity)

A remake of the 2010 space game *Galaxy on Fire 2* by FISHLABS in Unity, for Windows (also as a UWP app), Linux and
Android. It aims to be a faithful port of the original gameplay, including flight, combat, trading, mining, stations,
the bar and the whole story. It is built on the original assets and on game logic ported from the decompiled game code.
Downloads are on the [releases page](https://github.com/JoppieToppie/Galaxy-on-Fire-2-Unity-Remake/releases); each
release says how to install it.

**Status:** the main campaign, the Valkyrie add-on and the Supernova add-on can be played from start to end. The
Supernova opening and final battle have been rebuilt from the original scripts, but the add-on has not been fully played
through yet. Multiplayer and VR are experimental. A build's version is the date and time of its release (for example
`2026.10.01.1200`), the same on every platform, shown in the main menu.

## Features

- **Story:** the full main campaign with the prologue, the Void and the ending, plus the Valkyrie and Supernova add-ons.
  Dialogue is voiced (English or German), with portraits, radio chatter and cutscenes.
- **Flight:** the original flight model and chase camera. The station orbits are rebuilt exactly like the original,
  with their skies, planets, suns, lens flare and asteroid fields. Travel covers the autopilot, planet jumps,
  fast-forward, the star map, jumpgates, the Khador Drive and the Void's wormhole.
- **Combat:** every weapon, including missiles, beams, EMP, mines, turrets, sentry guns, the Liberator and the other
  special weapons. NPC traffic and AI, capital ships, pirate bases, Specters and the Most Wanted criminals. Combat
  equipment: cloak, emergency system, time extender, repair beams and gamma shields.
- **Economy and stations:** hangars, shops and ship dealers, and the space lounge with bar agents and every
  freelance mission type. Also blueprints, wingmen, medals, the Kaamo Club and save games.
- **Mining:** asteroid mining with the drilling minigame, plus gas clouds, hacking and docking at objects.
- **Multiplayer (experimental):** a shared universe in the finished game's world, online through a server browser or a
  join code, or over a local network. Players can host from the game or run a dedicated server, which keeps every
  player's progress. Players see each other in space and in the hangars; the host picks PvE (players fight only in
  arena matches and faction sieges) or PvP. Squads share bar missions and their rewards; factions claim stations, lay
  sieges and keep a bank. Everyone shares the NPC traffic, the crates, the asteroids and the shop stock. Local and
  global chat, chat commands, admin tools, a web admin page and event game modes (node graphs: waves of pirates,
  survival, races, quizzes...) with on-screen titles, timers, dialogs and rewards.
- **Mods:** new and changed items (with their own weapon effects and sounds), ships from 3D models, star systems and
  stations with their own models, planets, suns, skies, hangars and bars, characters with portraits, voice-over, music
  and sound effects, quests and bar missions, and whole campaigns that show up under New Game. See [Mods](#mods).
- **VR (experimental):** PC VR through OpenXR with the `-vr` launch option: a cockpit in flight with the instruments on
  its displays, standing in the hangars and the bars, the menus on a floating screen with a laser pointer.
- **Remake extras:**
  - Arrival and take-off flights in the hangar, soft shadows under the ships there, depth of field on your ship,
    **Inspect ship** (an orbit camera around it), animated dialogue text, and the original-style bloom as an option.
  - **Capital ship enhancements** (an option): escorts, stronger turrets and missile salvos; the carrier and the Vossk
    battleship can be destroyed, rare fleet battles between them, and the carrier sells supplies to pilots it trusts.
  - **Pirate events** (an option): in Risky and Dangerous systems an orbit may hold a pirate outpost or a pirate boss
    with a bounty.
  - A **missile warning**, and boosting or cloaking shakes off homing missiles.
  - **New Game+:** with a finished game in your saves, start again with its credits, blueprints, medals and ships.
  - A new game's **Game options**: the Kaamo Club from the start, **Hardcore** (permadeath: dying deletes the run's
    saves) and the tutorial popups (off by default).
  - Kaamo Club: stored ships keep their equipment and the mechanics' upgrades stack (both options, on by default).
  - Sell all / Buy all in the shop, a smart target lock (hostile ships first; the original rule is an option), hold a
    save slot to delete it, and the Missions window's map can travel to the mission's target.
  - A full-map overview on the star map, and new medals as a short toast instead of a window.
  - Four difficulties (Easy, Normal, Hard, Extreme) that can be changed during a game, and a choice between the PC
    and Android economies for a new game.
  - Other ships' engines like the player's (an option), photo mode, screenshots, an FPS counter, and every weapon's
    own shot sound (an option; the original only plays the first gun's).
  - Upscaling: FSR 1 and STP everywhere they are supported, NVIDIA DLSS and AMD FSR 2 / 3 / 4 on Windows, MetalFX on
    macOS and iOS.
  - Discord Rich Presence on Windows: your Discord status shows what you are doing in the game.
  - Dutch, Simplified Chinese and Hindi translations (the remake's own texts in all 11 languages), and a choice between
    German and English voices.
  - Debug tools (Options > Gameplay): jump to any story step, cheats, give items, spawn ships and objects, fly any hull
    (capital ships included), save and load ship loadouts.
  - The current story step and station can show small at the bottom right (Options > Gameplay), so a screenshot of a
    bug shows where it happened.
  - Export and import of all save games as one file (Options > Gameplay in the main menu), to move your games to
    another PC or phone. Importing checks the file first and replaces every existing save.
- **Controls and screens:** touch, tilt, keyboard and mouse (with the PC version's clickable on-screen buttons when
  mouse steering is off), and controllers (shown with Xbox buttons), all rebindable.
  Gyro steering with a DualSense, DualShock 4 or Switch Pro Controller on Windows. Haptic feedback on controllers and
  Android phones (hits, collisions, explosions, weapons, boost, jumps and mining) with an intensity setting. Landscape
  screens from 4:3 up to 32:9, phones included.

## Getting started

1. Install **Unity 7000.0.0a7** with Unity Hub. Add Android Build Support if you want phone builds.
2. Clone the repository with Git LFS installed. The assets are about 2.1 GB in LFS.
3. Open the project in Unity and open `Assets/Scenes/MainMenu.unity`, then press Play.

The scenes are `MainMenu` (build index 0), `Space` (the flight level) and `Station` (docked). The **GoF2** menu in the
Editor holds the asset build tools. The generated assets are already in the repository.

### Building

Always **switch the active build profile** (File > Build Profiles > Switch Profile) before building another platform.
URP chooses which shader variants to keep from the active platform. An Android build made while Windows was active
renders black, so the editor script `BuildTargetGuard` refuses such builds. `BuildVersionStamp` gives every build its
date and time as its version; for a release, set the Editor process's `GOF2_BUILD_VERSION` environment variable so all
platforms get the same one.

Windows builds use IL2CPP, which needs Visual Studio 2022 (or newer) with the **Desktop development with C++** workload
and a Windows 10 / 11 SDK. Android and Linux builds don't need it.

#### Scene lighting (environment reflections)

The scenes have no baked lighting data, on purpose: the game builds its levels at runtime and each system has its own
sky. `SkyReflection` updates the ambient light from the current sky wherever the sky is set. Don't generate lighting
for the scenes: a baked probe is one fixed sky for every system.

### Multiplayer

Multiplayer is experimental. Everyone in a session shares one universe: you see each other in space and in the hangars,
form squads, and fly bar missions together. Sessions take place in the finished game's world: a new pilot starts
docked at Dis in a Betty with 10 000 credits and a basic loadout. A dedicated server (or a host's persistent world)
keeps each player's progress; elsewhere nothing is kept. Sessions never touch your single-player saves. Only the
**exact same game version** can play together, so everyone needs the same release.

#### Joining a game

1. In the main menu, open **Multiplayer** and type your **pilot name** at the top.
2. The **server browser** lists the public games with their name, host, players (for example `4 / 16`) and version.
   It refreshes every 5 seconds. Click a game to join it.
   - A game tagged **PASSWORD** needs its password: type it in the **Password** field under the list first.
   - A game on another version shows "needs <version>" and can't be joined.
3. To join a game that isn't listed, type its **join code** (six letters or digits, like `QKJH9N`) in the field under
   the list and press **Join**. On a local network you can type the host's address instead (`192.168.1.20`, or
   `192.168.1.20:7778` for another port).

#### Hosting from the game

On the **Host a game** card, pick a mode:

- **Public:** online, and listed in the server browser under the **Game name** you choose.
- **Invite only:** online, but not listed. Friends join with your join code.
- **Local network:** for players on the same network (or a VPN like Hamachi or ZeroTier). The card lists your
  addresses (tap one to copy it) and the port, 7777 by default.

Every mode can have a **password** and a **Max players** limit (2 to 100, you included). **Debug menu** (Off by
default) decides whether the players may use the Debug menu (cheats, items, spawns) in your session. **Combat**:
**PvE** (default: players fight each other only in arena matches and faction sieges) or **PvP** (anywhere); the server
browser shows which. **World**:
**Fresh** (default) is a one-off session where nothing is kept; **Persistent** keeps every player's progress, the
factions, bans, staff, news and server settings on your device (the `HostedWorld` folder in the game's data folder),
like a dedicated server, and makes you the world's master admin (the Multiplayer window's Admin tab). Your own
progress there is that world's, separate from your single-player saves. Press **Host**. In an online
game the join code is copied to your clipboard and shown under the station's system information, with a Copy button.
Online play goes through Unity Relay: no port forwarding, but it needs an internet connection. With a player host,
all traffic goes through the host's connection, so for big sessions a dedicated server is better.

#### Running a dedicated server

A dedicated server hosts a session without anyone playing on that machine. It uses the normal Windows or Linux
download: edit and run `Start Dedicated Server.bat` (Windows) or `start-server.sh` (Linux) in the game folder. Its
options, the console, the settings admins can change while it runs, becoming its admin, its files and running it as a
Linux service are in **[SERVER.md](SERVER.md)**.

**Player profiles**

A dedicated server keeps each player's progress: credits, ship and its mods, equipment, cargo, the Kaamo Club and its
storage, and their squad. A player's game gets a secret key from the server on its first visit and signs in with it
every time after; the progress is saved when docking, every minute and when leaving. There is no limit on the number of
profiles: a profile nobody has used for 30 days is moved aside (an admin can bring it back with the console's `profile
restore <id>`). Players type these in the chat:

| Command | Meaning |
|---|---|
| `/link` | A 6-letter code for 5 minutes, to use the profile on another device. |
| `/link CODE` | On the other device: use the profile the code belongs to. `/link CODE force` if this device has progress of its own (it is deleted). |
| `/control` | Two devices of one profile online: the first one plays, the other watches from the station. This takes over once the playing one is docked. |
| `/profile` | The profile's id and devices. |

**Squads: the map and distress calls**

The star map shows where your squad is (a green dot with the count by a system, the names under a station). In space,
your own row in the squad window has **Distress call** (or type `/sos`): your squad gets a notice, sees you in red on
the map and gets a **Help** button by your name (or `/assist <name>`). Help takes them to you the fastest way: the Khador
Drive if they have one and the energy cells, else the autopilot. They arrive right next to you. In flight both are
also in the actions menu (E, the controller's D-pad left, or the touch quick menu button), and a banner at the top
shows who needs help.

**Factions**

Everything below is also in the station's **Multiplayer** window (the button left of Menu, top right), with buttons
instead of commands.

A faction is a lasting group of players on a server with profiles: a tag before its members' names, a bank, claimed
stations and sieges. Not to be confused with your **squad** (the friends you invited to fly with you this session) or
your **wingmen** (the NPC pilots you hire in a bar).

| Command | Meaning |
|---|---|
| `/faction create TAG Name` | Start a faction: a tag of 2-4 letters or digits, then its name. |
| `/faction invite <pilot>` | Invite a pilot (leader and officers). They type `/faction join TAG` within 5 minutes. |
| `/faction leave`, `/faction kick <pilot>` | Leave, or remove a member (officers remove members, the leader anyone). |
| `/faction promote <pilot>`, `/faction demote <pilot>`, `/faction leader <pilot>` | Ranks (leader only). |
| `/faction info [TAG]`, `/faction list`, `/faction disband` | About a faction, all factions, end yours (leader). |
| `/faction deposit N`, `/faction withdraw N` | Put credits into the faction bank; take them out (leader and officers). |
| `/faction claim`, `/faction unclaim`, `/faction home` | Docked at a station: claim it for the faction (paid from the bank), give it up, or make it the faction's home (leader and officers). Members start and respawn at the home. |
| `/faction claims [TAG]` | A faction's stations. A station no member docks at for 14 days is lost. |
| `/f <text>` | Talk to your faction. |
| `/faction siege`, `/faction sieges` | In another faction's orbit (leader and officers): besiege it, paid from the bank. It starts 10 minutes later and lasts 15; meanwhile the two factions may fight there, and the side with more pilots in the orbit takes control. At 100 % the station changes hands; otherwise the defenders keep it. |

At a faction's station its members buy items 10 % cheaper and its fighters protect them. Pilots of other factions pay 5 % more
(into the faction's bank) and are attacked by the station's fighters unless they pay the toll asked on arrival.

**Moderation**

**Become the master admin**: the server writes an admin token to its log at every start (see [SERVER.md](SERVER.md)).
In the game, type `/claimadmin <token>` in the chat, or use the station's Multiplayer window, Profile tab, "Claim this
server". The master admin makes admins
(`/admin <pilot>`, `/unadmin`), admins make ops (`/op <pilot>`, `/deop`). Ops and up get an **Admin** tab in the
Multiplayer window: kicks and bans, roles, and for admins the server status, announcements, every profile and the
factions.

| Command | Who | Meaning |
|---|---|---|
| `/kick <pilot> [minutes] [reason]` | ops | Drop a pilot; they can't come back for the minutes (default 5, 0 = at once). |
| `/tempban <pilot> <minutes> [reason]` | ops | Ban for a while (ops at most 24 hours). |
| `/ban <pilot> [reason]` | admins | Ban for good. |
| `/unban <pilot or profile id>`, `/bans` | ops | Lift a ban; list them. |
| `/say <text>`, `/disband <TAG>` | admins | An announcement to everyone; end a faction. |
| `/admin <pilot>`, `/unadmin <pilot>`, `/deleteprofile <id>` | master | Admin roles; delete a profile. |
| `/claimadmin <token>` | anyone | Become the master admin with the server's admin token. |
| `/staff` | everyone | Who the masters, admins and ops are. |

A ban covers the pilot's profile and every device it was used on. Nobody can act on someone of their own rank or
higher. Admins also change the server's settings while it runs (`/settings`, `/set <key> <value>`, or the Admin tab;
see [SERVER.md](SERVER.md)).

**Arena matches**

On a PvE server (the default) players can only fight each other in arena matches and faction sieges (a PvP server, or
`-freepvp`, allows it anywhere). A match takes its players
from their station into a private copy of the Void's home system (empty, or with its Void fighters attacking everyone),
and back when it ends. Nothing is at stake: ships, equipment and ammo come back
as they were, and only the match's statistics are kept.

| Command | Meaning |
|---|---|
| `/duel <name> [voids]` | Challenge a pilot to a duel: first to 3 kills, or the most kills after 5 minutes. Both must be docked. Add `voids` to fight among the Void's own fighters. |
| `/accept`, `/decline` | Answer a challenge (within 60 seconds). |
| `/ffa [voids]` | Join the free-for-all queue (docked): it starts 30 seconds after a second pilot joins, or at once with 8. First to 15 kills, or the most after 10 minutes. `voids` joins the queue for matches with the Void fighters. |
| `/leave` | Leave the queue or the match (leaving a duel loses it). |
| `/arena` | The matches running and their scores. |
| `/top` | The arena leaderboard (needs player profiles). |

Without `-allowdebug` the server turns away progress that can't be right (worth growing faster than `-maxearn`, ships
nobody can own). The players' games still run the economy, so this isn't proof against a modified game.

The console also runs every chat command below without the "/" (`list` and `say` are `players` and `g`). Tab completes
command names, Up / Down go through the history.

#### Chat commands

Open the chat with **B** and type a line starting with `/`. Typing `/` lists the commands; **Tab** completes commands
and player names. Everyone can use:

| Command | What it does |
|---|---|
| `/help` | The commands you can use. |
| `/players` | Who is playing, where, in which ship and squad. |
| `/g <text>`, `/l <text>` | A line to the Global or Local channel. |
| `/w <player> <text>` | A private message. |
| `/invite <player>`, `/leave` | Invite a pilot docked at your station to your squad, or leave it. |
| `/netstats` | Network statistics (ping, packet loss, data rates). |
| `/pos` | Your orbit and position, in the coordinates `/tp` takes. |

**Admin commands** are for the host's own player, the dedicated server's console, and the players the host makes
admin with `/admin <player>` (`/unadmin` takes it back). They work on players by name, by client id, or with a
selector: `@a` everyone, `@s` yourself, `@p` the nearest other player, `@r` a random one, and `@alive`, `@space`,
`@docked`, `@dead`, `@survivors` (in an event: never destroyed since it began). In the chat, leaving out the players
means yourself.

| Command | What it does |
|---|---|
| `/kick <player> [reason]` | Removes a player; they see the reason. |
| `/mute <players> [minutes]`, `/unmute` | Silences a player's chat. |
| `/tp [players] <player \| station [x y z \| dock]>`, `/tphere <player>` | Teleports to a player, an orbit (by number or name) or into a station's hangar. |
| `/kill`, `/heal`, `/ammo`, `/reveal`, `/peace [players]` | Destroys, repairs, refills secondaries, reveals the map, resets the standings. |
| `/give [players] <item> [amount] [mount]` | Items into the hold (docked, `mount` also mounts them). |
| `/credits [players] <amount>` | Gives (or with a minus, takes) credits. |
| `/spawn [players] <ship \| object> [race] [count] [enemy \| friendly \| neutral] [named <name>] [at x y z]` | Ships (by number or name) or scenery near the players; `named` puts a name on them in the HUD. |
| `/ship [players] <ship \| own>` | Swaps the players' ship (any hull of the debug tools), or back to their own. |
| `/cheat [players] <god \| ammo \| cooldown \| primary \| boost \| onehit \| locks \| shopping \| jumps> [on \| off]` | A cheat for those players, this session only. |
| `/title [players] <text> [\| subtitle] [for <seconds>]` | A big title on their screen (`clear` removes it). |
| `/timer [players] <seconds \| m:ss> [label]` | A countdown at the top of their screen (`stop` removes it). |
| `/dialog [players] <speaker> : <text> [\| [speaker :] next page ...]` | A conversation window. The speaker is a story character ("Keith", "Keith as Bob"), a race and a name ("vossk K'ekki") or `player`; `%player%` is the reader's name. A page `reward: <rewards>` pays when it closes. |
| `/reward [players] <credits \| item [amount]> [+ more] [\| title]` | Credits and items, shown in the mission reward box. |
| `/event <name \| stop \| list>` | Starts or stops an event script. |

#### Events

An event is a node graph the server runs, for game modes like waves of pirates, a race or a quiz. Two come with the
game: `/event waves` (more pirates every wave, the survivors paid per wave) and `/event survival`. Graphs are made in
the Unity Editor (Assets > Create > GoF2 > Event Graph, or Event Graph From Template: King of the Hill, Boss Fight, Free
For All, Pirate Base, Quiz, Race, Vote, Waves, Survival and more) and saved as `<name>.gof2event`. Put one in the
`Events` folder next to the game (or the dedicated server), or ship it in a mod, and start it with `/event <name>`.
A graph can spawn ships and give them orders (fly to, follow, attack, flee, dock), run cutscenes with camera shots and
fades, ask questions and hold votes, keep a scoreboard and pay rewards. A graph with a mission title is offered as a bar
mission in the Space Lounge instead, for the squad that takes it.

## Mods

Mods add or change the game's content as data, no code needed: items (with their own weapon effects and sounds), ships
from GLB models (new ones, or new models for the original ships), texture replacements (skins), star systems and
stations (with their own models, planets, suns, skies, hangars and bars), characters with portraits, voice-over, music
and sound effects, blueprints, quests and bar missions (event graphs), options for a new game, and whole campaigns that
appear under New Game.

The easiest way to install one: in the main menu's **Mods** screen, press **Import mod** and pick the mod's `.zip` (on
Android too). **Delete** removes the selected mod. You can also put a mod (a folder or its `.zip`) in the Mods folder
yourself, then turn it on in the Mods screen:

| Platform | Mods folder |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\JoppieToppie\Galaxy on Fire 2\Mods`, or a `Mods` folder next to `GoF2Remake.exe` |
| Linux | `~/.config/unity3d/JoppieToppie/Galaxy on Fire 2/Mods`, or a `Mods` folder next to the game |
| Android | `Android/data/com.joppietoppie.gof2remake/files/Mods` (reachable over USB) |

The Mods screen's **Open mods folder** button opens it. A save made with mods remembers them, and loading it without
them removes their items (refunded). In multiplayer the host decides whether mods are allowed; joining players need the
same mods installed.

To make a mod, see the modders' guide [Modding/README.md](Modding/README.md), with two example mods in
[Modding/Examples](Modding/Examples). [Modding/ai](Modding/ai) has a guide to making mods with Claude or ChatGPT.

## VR (experimental)

Start the Windows build with `-vr` (for example a shortcut to `GoF2Remake.exe -vr`) with a PC VR headset connected and an
OpenXR runtime active (SteamVR, Meta Quest Link, Windows Mixed Reality). Without a headset the game starts normally.

- **Flight:** you sit in a cockpit. The shield, hull and cargo readouts are on its displays, the radar in the middle of
  the dashboard, the rest of the HUD on the canopy. Cutscenes keep the horizon level and fade between shots.
- **Stations:** you stand in the hangar beside your ship (grab it with the grip and pull to turn it) and in the bar
  (point at a visitor and pull the trigger to talk).
- **Menus** appear on a floating screen; point with the right controller and pull the trigger.
- **Controls:** the controllers work as a gamepad (sticks, triggers, A / B / X / Y, the grips as LB / RB). With Options >
  Controls > "VR flight: grab the stick and throttle" you fly with the cockpit's side-stick (right hand) and throttle
  lever (left hand) instead.

The cockpit is the same for every ship for now. VR has only been tested without a headset so far.

## Controls (keyboard)

| Key | Action |
|---|---|
| Arrow keys | Steer |
| W | Boost |
| S | Brake (the engines stop while held) |
| `]` / `/` or the mouse wheel | Throttle |
| Space / left mouse | Primary weapons |
| R / right mouse | Secondary weapon |
| G | Switch secondary weapon |
| A / D | Strafe left / right |
| 1 / 3 | Roll left / right |
| 2 | Level out |
| F / Enter | Action (dock, autopilot, mine, jump) |
| Q | Autopilot menu |
| E | Actions menu (secondary weapons, wingmen, cloak, Khador Drive) |
| Tab | Fast-forward |
| T | Camera / turret view |
| V / K / C / X | Wingmen / Khador Drive / cloak / time extender |
| M or middle mouse | Toggle mouse steering |
| B | Chat (multiplayer) |
| F12 | Screenshot |
| Esc | Pause |

Every flight control can be rebound in Options > Key bindings (two keyboard / mouse keys and a controller button
each). Controllers and touch are fully supported. The in-game hints show the buttons for whichever input you last used.

**Controllers on Linux:** Xbox, PlayStation and Switch Pro controllers work directly. On a Steam Deck in desktop mode,
or with a controller the game doesn't recognise, add the game to Steam (Add a Non-Steam Game) and start it from there:
Steam Input then presents the controls as an Xbox controller.

## Project layout

```
Assets/Scripts/Runtime/   game code (flight, world, NPCs, UI, multiplayer)
Assets/Scripts/Editor/    import settings, asset and prefab builders, menu items
Assets/Resources/         game data (JSON), assembled prefabs, sky / HUD / combat assets
Assets/UI/                UI Toolkit screens (UXML / USS)
Reference/                decompiled original code, research notes and conversion tools
```

`CLAUDE.md` is the full technical documentation. It covers every system, the original functions it is based on and
the choices the remake made.

## Credits

**Galaxy on Fire 2 Remake** by JoppieToppie.

The remake is built on the **FULL HD version and modifications made by KiritoJPK**, thanks to the Galaxy on Fire 2™ and
4PDA community: <https://github.com/KiritoJPK/Galaxy-on-Fire-2-FULL-HD-Android>

© 2011 Designed and developed by FISHLABS Entertainment GmbH, powered by ABYSS® Game Engine. Galaxy on Fire 2™ and
ABYSS® are registered trademarks of FISHLABS Entertainment GmbH. All rights reserved. This is an unofficial fan project,
not affiliated with or endorsed by FISHLABS or Deep Silver.

Fonts: the original game's interface typeface, and Inter (SIL Open Font License). Controller gyro: [JoyShockLibrary](https://github.com/JibbSmart/JoyShockLibrary)
by Julian Smart (MIT License). Built with Unity, the Universal Render Pipeline, Netcode for GameObjects and OpenXR.
