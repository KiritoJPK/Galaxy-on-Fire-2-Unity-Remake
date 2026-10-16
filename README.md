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
  - Export and import of all save games as one file (the buttons under Load game in the main menu), to move your games to
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

Multiplayer is experimental. Everyone in a session shares one universe: you see each other in space and in the
hangars, form squads, fly bar missions together, share the shops, found factions that own stations, and fight in arena
matches. Sessions take place in the finished game's world: a new pilot starts docked at Dis in a Betty with 10 000
credits. A dedicated server (or a host's persistent world) keeps each player's progress; your single-player saves are
never touched. Only the **exact same game version** can play together.

- **[Playing together](docs/MULTIPLAYER.md)**: joining and hosting, chat, squads, missions, shops, factions, owning
  stations, arena fights, admins and every chat command.
- **[Running a dedicated server](SERVER.md)**: setting one up, its options and console, the message of the day,
  becoming its admin, its files.
- **[How multiplayer works](docs/MULTIPLAYER-DEV.md)**: the code, for programmers.

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
