# Multiplayer guide

Multiplayer is experimental. Everyone in a session shares one universe: you see each other in space and in the hangars,
form squads, fly bar missions together, trade from the same shops, found factions that hold stations, and fight in
arena matches. This guide is for players and for people hosting a game from the menu. Running a dedicated server
(command-line options, the console, its files, running it as a service) is in **[SERVER.md](../SERVER.md)**; how the
multiplayer code works is in **[MULTIPLAYER-DEV.md](MULTIPLAYER-DEV.md)**.

## Contents

- [Quick start](#quick-start)
- [The basics](#the-basics)
- [Joining a game](#joining-a-game)
- [Hosting from the game](#hosting-from-the-game)
- [Your progress](#your-progress)
- [The Multiplayer window](#the-multiplayer-window)
- [Chat](#chat)
- [Other players](#other-players)
- [Squads](#squads)
- [Bar missions together](#bar-missions-together)
- [Mining, crates and NPCs](#mining-crates-and-npcs)
- [The economy](#the-economy)
- [Factions](#factions)
- [Territory: claims, sieges and garrisons](#territory-claims-sieges-and-garrisons)
- [Arena matches](#arena-matches)
- [Events](#events)
- [Moderation and admin commands](#moderation-and-admin-commands)
- [What works differently from single player](#what-works-differently-from-single-player)
- [Troubleshooting](#troubleshooting)
- [Command reference](#command-reference)

## Quick start

**Join a public game:** main menu > **Multiplayer** > type a pilot name if asked > click a game in the server browser.

**Play with a friend:**

1. One of you opens **Multiplayer**, picks **Invite only** on the **Host a game** card and presses **Host**.
2. The join code (six letters or digits, like `QKJH9N`) is copied to the clipboard and shown in the station under the
   system information. Send it to your friend.
3. The friend opens **Multiplayer**, types the code in the field under the server browser and presses **Join**.

## The basics

- **One shared universe.** Every player has their own game (scenes, map, docking) in the same world. Players in the
  same orbit see each other's ships; players docked at the same station see each other's ships in the hangar.
- **The finished game's world.** Sessions take place after all three campaigns: every system is on the map, every
  shop sells its full range (Kothar's jump-drive ships, the Vossk add-on ships, the VoidX at Thynome...), there is no
  story, no wormhole and no Void invasion. Ginoya is rebuilt after the supernova.
- **The start.** A new pilot starts docked at **Dis** in a **Betty** with **10 000 credits** and a basic loadout:
  Nirai Impulse EX 1, Targe Shield, Telta Quickscan, IMT Extract 1.3. Dis always sells the Khador Drive. On a server
  with profiles, a returning pilot starts where they last docked (or at their faction's home).
- **Same version only.** Only games built from exactly the same code can play together. The server browser shows a
  game on another version as "needs <version>" and won't join it.
- **Your single-player saves are never touched.** A session is never saved to your save slots.
- **Fixed rules:** medals are off, every session plays on **Normal** difficulty with the Android economy, and the
  Time Extender doesn't work (the world can't slow down for one player).

## Joining a game

1. In the main menu, open **Multiplayer**. If you haven't set a **pilot name** yet, the game asks for one (other
   players see it, and the server uses it to find you in commands).
2. The **server browser** lists the public games, this version's first and the fullest first. Each row shows:
   - the game's name and the host,
   - players and the limit (for example `4 / 16`, or "full"),
   - **PvE** or **PvP** (whether players can fight each other outside arenas and sieges),
   - the version (another version: "needs <version>", not joinable),
   - tags: **SERVER** (a dedicated server: your progress is kept), **PASSWORD**, **MODDED** (with the mods it runs).

   The list refreshes every 5 seconds. Click or tap a game to join it.
3. A **PASSWORD** game: type the password in the **Password** field under the list first. Joining without it asks for
   it.
4. A **MODDED** game: you need the same mods (same files) installed in your Mods folder, on or off doesn't matter. The
   browser says "needs mods: ..." when some are missing.
5. A game that isn't listed: type its **join code** in the field under the list and press **Join**. On a local
   network type the host's address instead (`192.168.1.20`, or `192.168.1.20:7778` for another port).

The menu stays up while connecting ("Connecting to ...") and fades once you are in. If the server has a message of the
day, it opens in a window the first time you join, and again whenever it changes; `/motd` shows it any time.

## Hosting from the game

On the **Host a game** card, pick a mode:

| Mode | What it is |
|---|---|
| **Public** | Online, listed in the server browser under the **Game name** you choose (default "<your name>'s universe"). |
| **Invite only** | Online, not listed. Friends join with your join code. |
| **Local network** | Players on the same network or a VPN (Hamachi, ZeroTier, Radmin, Tailscale...). The card lists the addresses others can join on, named by adapter (tap one to copy it), and the port, 7777 by default. |

Then the settings:

| Setting | Meaning |
|---|---|
| **Password** | Optional, in every mode. |
| **Max players** | 2 to 100, you included (default 16). A player past the limit is turned away with "The game is full". |
| **Debug menu** | **Off** (default) or **Allowed**: whether players may use the Debug menu (cheats, items, spawns). Off also switches off any cheats left on from single player for the session. |
| **Combat** | **PvE** (default): players fight each other only in arena matches and faction sieges. **PvP**: anywhere. |
| **World** | **Fresh** (default): a one-off session, nothing is kept. **Persistent**: every player's progress, the factions, bans, staff, news and settings are kept on your device (the `HostedWorld` folder in the game's data folder), like a dedicated server, and you are the world's master admin. |
| **Mods** | **Off** (default) or **Allowed**: your enabled mods become the session's; joining players need them installed. |

Press **Host**. If the port is in use, the card tells you and puts the next free port in the field.

- Online, the join code is copied to your clipboard and shown under the station's system information with a **Copy**
  button.
- Online play goes through Unity Relay: no port forwarding, but it needs an internet connection.
- With a player host all traffic goes through the host's connection, so for big sessions a dedicated server is better.
- When the host leaves, everyone gets "The host ended the session." and goes back to the menu.

## Your progress

| Where you play | What is kept |
|---|---|
| A dedicated server, or a Persistent hosted world | Your **profile**: credits, ship and its Kaamo upgrades, equipment, cargo, the Kaamo Club and its storage, your squad. |
| A Fresh hosted game | Nothing. |

A profile is saved when you dock, every 60 seconds, and when you leave. The server checks each save like an imported
save game; without the Debug menu it also refuses saves where your worth grew impossibly fast, or hulls nobody can own
(you see "The server didn't save your progress: ...").

A profile is tied to a secret key your game keeps for that server. To use it on another device:

| Command | Meaning |
|---|---|
| `/link` | A 6-letter code, valid 5 minutes (on the device that plays the profile). |
| `/link CODE` | On the other device, joined to the same server: use the profile the code belongs to. `/link CODE force` if this device already has more than 5 minutes of progress there (that progress is deleted). |
| `/control` | Two devices of one profile online: the first one plays, the other watches from the station (no hangar, lounge, map or launch). This takes over once the playing one is docked. |
| `/profile` | The profile's id and devices. |

A profile nobody has used for 30 days is moved aside; a server admin can bring it back.

**Destroyed in a session** you don't load a save: "Tap to respawn at the station." docks you at that orbit's station,
repaired (a faction member at the faction's home). An event can set respawn points in space instead.

## The Multiplayer window

Everything in this guide can be done with buttons in the **Multiplayer** window instead of chat commands:

- **Docked:** the **MULTIPLAYER** button in the top bar, left of Menu.
- **In flight:** the Multiplayer button on the right, under the cargo readout, or **N** (rebindable). The flight
  controls wait while it is open.

A dot on the button means something waits: an invitation, a challenge or an unread chat line. The window shows the
server's answer to each button at its foot. Tabs:

| Tab | What is in it |
|---|---|
| **Chat** | The whole chat, the Local / Global switch, the line and **Send**. |
| **Squad** | Invitations (Accept / Decline), the members and where they are, distress calls (**Help**), **Leave**, the pilots docked here to **Invite**. |
| **Faction** | Create or join; the bank (Deposit / Withdraw), the members and their ranks, inviting pilots, the territory with each claim's garrison, Claim / Make home / Give up / Siege for the station you are docked at, the sieges, Leave / Disband. |
| **Arena** | A challenge waiting, the Void fighters option, the pilots to challenge, the free-for-all queue, the matches running, the leaderboard. |
| **Profile** | Your profile, Take control, link codes, "Claim this server" (its owner). |
| **Admin** | Ops and up only (see [Moderation](#moderation-and-admin-commands)). |

Dangerous buttons (Leave, Disband, Ban for good) ask twice.

- **Controller:** **LB / RB** switch tabs, the D-pad moves through the buttons (the focused one has an amber frame),
  **A** presses, **B** closes.
- **Keyboard:** **Q / E** switch tabs, **Esc** closes.
- **Phones and handhelds:** on a small high-density screen (a phone, the Retroid Pocket G2) the window fills the screen
  with bigger text and finger-sized buttons, and moves to the top half while the on-screen keyboard is up.

## Chat

| Key | Action |
|---|---|
| **B** | Open the chat (or tap the small **Chat** button above the lines). |
| **Enter** | Send. The chat stays open for the next line; Enter on an empty line closes it. |
| **Tab** | Switch the channel. While typing a command, Tab completes it instead (Shift+Tab goes back). |
| **Esc** | Close (the draft is kept for next time). |

All keys are rebindable in Options > Key bindings.

- **Channels:** **Local** reaches the players in your orbit or docked at your station; **Global** reaches everyone.
- **Private messages:** `/w <player> <text>`, shown in violet as "[From X]" / "[To X]".
- **Commands:** a line starting with `/` is a command, never sent as chat. Typing `/` lists the commands you can use
  with a short description; Tab completes command and player names.
- Lines fade 12 seconds after they arrive (all show while typing). Another player's line plays a short sound.
- On phones the chat moves to the top of the screen while typing, and the on-screen keyboard shows its own input box
  (its Done button sends).

## Other players

- **Markers:** another player is a **yellow** marker (neutral), a squadmate **green**, a player hostile to you **red**.
  They can be locked like any ship. Their faction tag shows before their name: `[TAG] Name`.
- **Fighting on a PvE server:** players can't hurt each other outside arena matches and faction sieges: shots pass
  through.
- **Fighting on a PvP server:** attacking a player (3 hits, or 50 damage within 10 seconds, so a stray shot doesn't
  count) makes the two of you enemies for 120 seconds after the last hit either way: red markers, and your turrets and
  sentry guns fire at each other. Destroying a player announces "X was destroyed by Y." to everyone.
- **What you see of them:** their ship and turret, engine glow and exhaust, boost, cloak (off your radar and markers
  while cloaked), jumps (the Khador charge and its effect, the jumpgate opening), and their shots. Their missiles locked
  on you trigger the missile warning; a boost or the cloak shakes them off. EMP on another player drains their shield.
- **In the hangar:** other players docked at your station park on its pads and fly in and out (with Hangar arrival and
  take-off on).

## Squads

A squad is the group you fly with this session.

**Forming a squad:**

1. Dock at the same station as the other player.
2. Open the Multiplayer window, **Squad** tab, and press **Invite** next to them (or type `/invite <player>`).
3. They get an Accept / Decline popup for 45 seconds. Joining a squad drops their own bar mission (the popup warns).
4. Accepting adds them to your squad (a new one if you had none).

**In a squad:**

- Squadmates are green markers, and your weapons don't hurt each other (shots pass through).
- The squad window on the right (collapsible) shows each member, where they are, and their shield and hull bars.
- The star map shows your squad: a green dot with the count by a system, the names under a station.
- On a server with profiles the squad is remembered: signing in again puts you back with a squadmate who is online.

**Distress calls:**

1. In space, press **Distress call** (under the Multiplayer button in flight) or type `/sos`.
2. Your squad gets a notice with where you are, sees you in red on the map, a banner at the top, and a **Help** button
   by your name (also `/assist <name>`, or "Help <name>" in the actions menu, E).
3. **Help** programs your station and takes them there the fastest way: an instant Khador jump with the drive and
   enough energy cells, else the autopilot (docked: the launch first). They arrive 1.5 km from you, facing you.
4. The call ends when you dock, after 10 minutes, when you leave the squad, or with **End the call**.

**Leaving:** **Leave** in the Squad tab, or `/leave`. A squad of one dissolves.

## Bar missions together

A squad has **one bar mission**, shared by every member.

1. Dock with the **whole squad** at the agent's station.
2. One member takes the mission in the Space Lounge.
3. Every member gets it (replacing their own mission); the others see a mission card with the mission, who took it,
   the client and the reward.

While it runs:

- **Progress is shared:** anyone's kills count, the Challenge score too, and goals like ore unloaded at the mining
  plant add up.
- **The mission's orbit** is built by the first member who arrives; the others join in. If that member leaves, the
  next one takes over the mission's ships where they are.
- **Containers and passengers** stay with whoever carries them, so only that member delivers them. If they
  disconnect, what they carried goes to a squadmate.
- **Loot** from the mission's ships is for the squad only. Players outside the squad see the mission's ships as
  ordinary ships.

At the end:

- **Success** pays every member an equal share of the reward, with +5 standing and the mission count for each.
- **Failure**, or any member's **Discard** (the Missions window warns), ends it for the whole squad.
- Leaving the squad removes the squad's mission for you.

In sessions the bars also offer **Ore Mining** missions (mine an amount of ore and unload it at a mining plant), and a
server can add **event missions** (made with event graphs): extra visitors in the Space Lounge with their own offers,
taken the same way.

## Mining, crates and NPCs

- **Asteroids** are the same for everyone in an orbit. Several players can drill the same asteroid at once; each plays
  their own mining game and the ore is split between them. The first to finish (or anyone shooting it) destroys it for
  all: the others get "Mined out by X." and their share. A rock someone sits on stops spinning for everyone.
- **Crates:** the first tractor beam to start pulling a crate claims it; nobody else's beam can take it meanwhile. The
  claim goes back if that beam lets go or the player leaves the orbit.
- **NPC ships:** the first player in an empty orbit runs its traffic; everyone there sees the same ships. When that
  player leaves, another one takes the ships over where they are.
- **Who NPCs attack:** each player's own standing decides how the races treat them. An NPC that you or a squadmate
  shoot turns on your squad. Pirates, the Void and Specters attack everyone.
- **Kills:** whoever destroys an NPC gets the kill and its standing change in their own game. Raiders downed together
  can make the station news ("defence" items).

## The economy

- **Shared shops.** Every station's items and dealer ships are one list for all players. A unit someone buys is gone
  for everyone; a unit sold joins the stock. If two players buy the last unit at once, the second gets "Sold out" and
  their credits back. The open hangar list updates live.
- **Restocking:** a station's list is made again every **15 minutes**. **Rare goods** (commodities worth 5 000 or
  more: Buskat, Vossk Organs, Implants and the like) don't come back with it: what is left stays, and a sold-out rare
  good returns a third at a time every 10 minutes, full again after 30.
- **Prices** are fixed per station and item, within ±2 % of the item's price for that place on the map. Buying and
  selling the same item at one station never makes a profit; profit comes from carrying goods between systems.
- **Loma** (Var Destro, Sao Perula, Quineros) buys everything at its maximum price. Its pirates ask a toll on arrival
  (2–20 % of your cargo's value); refuse, or shoot one, and they attack.
- **Story-only blueprints:** the Khador Drive and Disruptor Laser blueprints (Var Destro) and Gamma Shield II and
  Chromo Plasma (Quineros) are sold by visitors in the Loma lounges at 1.5× the product's price.
- **Kaamo Club upgrades** stack up to the server's limit of each per ship (5 by default; the host's "Kaamo Club upgrade
  limit" option, a dedicated server's `kaamomaxlevel`), the price doubling every level.
- **Buying a ship:** the old ship's equipment goes into your cargo hold (story items stay mounted). Mount what you want
  on the new ship in the **Ship** tab; launching is refused while the hold is overloaded.

## Factions

A faction is a lasting group of players on a server that keeps profiles (a dedicated server or a Persistent hosted
world). Members show `[TAG]` before their name. Not to be confused with your **squad** (this session's group) or your
**wingmen** (NPC pilots hired in a bar).

| Rank | Can |
|---|---|
| **Leader** | Everything, including ranks, handing over leadership and disbanding. |
| **Officer** | Invite and kick members, withdraw from the bank, claim and give up stations, move the home, set garrisons, declare sieges. |
| **Member** | Talk to the faction, deposit into the bank. |

- **Create:** the **Faction** tab's form, or `/faction create TAG Name` (a tag of 2–4 letters or digits, unique on the
  server; a name up to 24 characters). Up to 50 members.
- **Join:** an officer invites you (`/faction invite <pilot>`); you press **Join** or type `/faction join TAG` within 5
  minutes.
- **Leave:** `/faction leave`. The leader leaves only by handing over (`/faction leader <pilot>`) or as the last
  member (the faction ends). A leaderless faction passes to its first officer.
- **The bank:** `/faction deposit N` moves credits from you into the bank (your game pays), `/faction withdraw N`
  (officers) pays out to you. Claims, sieges and garrisons are paid from it. Disbanding loses it.
- **Faction chat:** `/f <text>` to the members online.

## Territory: claims, sieges and garrisons

### Claims

**Claiming a station:**

1. Dock at the station.
2. In the **Faction** tab, press **Claim** (or type `/faction claim`). Officers only.
3. The bank pays the claim cost. Everyone hears about it.

The defaults (the server can change them):

| Setting | Default |
|---|---|
| Claim cost | 500 000 |
| Stations per faction | 3 |
| A claim is lost when no member docks there for | 14 days |
| Siege cost | 250 000 |
| Toll | 10 000 |

- The Kaamo Club and Loma can't be claimed.
- The first claim is the faction's **home**: members start and respawn there. `/faction home` (docked at another of
  your stations) moves it; `/faction unclaim` gives a station up.
- **Where it shows:** the holder's tag on the star map (station and system), in the station's header and in the orbit
  information. In flight, a station your faction holds has a **green orb** on its planet (red while it is under
  siege).

**At a held station:**

- Members buy items **10 %** cheaper (and sell at that price too).
- Pilots of other factions pay **5 %** more, which goes into the holder's bank.
- The station's own fighters protect the members and attack other factions' pilots, unless those pay the **toll**
  asked on arrival (it goes to the holder's bank and lasts the visit). Pilots without a faction are treated as usual.

### Sieges

**Declaring a siege:**

1. An officer flies into another faction's orbit (or docks at its station).
2. In the **Faction** tab, press **Siege** (or `/faction siege`). The bank pays the siege cost.

Rules:

- One siege at a time per faction, the faction needs room for another claim, and a station can't be besieged within
  24 hours of its last siege.
- It is announced to everyone, **starts 10 minutes later and lasts 15**.
- During the siege, the two factions' pilots can fight each other in that orbit, even on a PvE server.
- Every 5 seconds the side with more pilots in the orbit moves the control: each extra pilot is worth a third of a
  percent per second (one attacker alone takes an undefended station in 5 minutes). Garrison fighters count for the
  defenders (below).
- **100 %** = the attackers take the station (and it becomes their home if they had none). When the time runs out
  first, the defenders keep it.

A banner at the top of the screen in that orbit shows the factions, the control, the time left and the garrison.
`/faction sieges` lists the sieges.

### Garrisons

A garrison defends a claim when the holders are offline.

**Setting a garrison:**

1. Open the **Faction** tab. Under each of your stations is its garrison.
2. Pick the **fighters** (0–12) and the **level** (1–5) with the − / + buttons.
3. Press **Set**. The button shows the daily cost.

Or type `/faction garrison <fighters> <level> [station]` (the station docked at, or a station number).

- **Upkeep:** 300 × fighters × level credits a day from the bank (at most 18 000). The first day is paid when you set
  it. If the bank can't pay, the garrison leaves. It can't be changed while the station's siege runs.
- **In a siege:** each living garrison fighter counts as **a third of a defending pilot**, so 12 fighters hold like 4
  players. They swarm in front of the station, attack the attackers and spare the holders. A destroyed fighter comes
  back **90 seconds** later. When the siege ends they jump out.
- **Level:** hull × (1 + 0.5 per level above 1), guns × (1 + 0.25 per level above 1), and the stronger fighter models
  from level 3. They drop no loot.
- A station that changes hands loses its garrison; the new holder sets its own.

## Arena matches

Arena matches are fights between players with nothing at stake. A match takes its players from their station into a
private copy of the Void's home system, and back when it ends. Ships, equipment, ammo and cargo come back as they were;
only the statistics (wins, kills, deaths) are kept.

| Command | Meaning |
|---|---|
| `/duel <name> [voids]` | Challenge a pilot (both docked): first to 3 kills, or the most after 5 minutes. `voids` adds the Void's own fighters, which attack everyone. |
| `/accept`, `/decline` | Answer a challenge within 60 seconds. |
| `/ffa [voids]` | Join the free-for-all queue (docked): starts 30 seconds after a second pilot joins, or at once with 8. First to 15 kills, or the most after 10 minutes. |
| `/leave` | Leave the queue or the match (leaving a duel loses it). |
| `/arena` | The matches running and their scores. |
| `/top` | The leaderboard (needs profiles). |

How a match runs:

1. Everyone takes off and the arena loads (players who don't arrive within 20 seconds are sent home).
2. A 5-second countdown with controls and guns locked.
3. The fight. A destroyed pilot respawns in the arena after 3.5 seconds, repaired. No mining, docking or jumps.
4. The result for 6 seconds (winner or draw, everyone's kills and deaths), then back to the station.

## Events

An event is a node graph the server runs: game modes like waves of pirates, a race or a quiz. Admins start one with
`/event <name>` (`/event list` shows them and their settings, `/event stop` ends one). Two come with the game: `waves`
(more pirates every wave, the survivors paid per wave) and `survival`.

Events can spawn ships and give them orders (fly to, follow, attack, flee, dock), run cutscenes with camera shots and
fades, show titles, timers and conversations, ask questions and hold votes, keep a scoreboard, set respawn points and
pay rewards. Graphs are made in the Unity Editor (Assets > Create > GoF2 > Event Graph, or Event Graph From Template)
and saved as `<name>.gof2event` in the `Events` folder next to the game or server, or shipped in a mod.

## Moderation and admin commands

### Roles

On a server with profiles:

| Role | How | Can |
|---|---|---|
| **Master** | `/claimadmin <token>` with the server's admin token (see [SERVER.md](../SERVER.md)), or the Profile tab's "Claim this server" | Everything: makes admins, deletes profiles. |
| **Admin** | Made by a master (`/admin <pilot>`) | Bans for good, announcements, server settings, ending factions, the admin tools. |
| **Op** | Made by an admin (`/op <pilot>`) | Kicks, temporary bans (24 hours at most). |

On a Fresh hosted game the host is the admin and can make others admin for the session (`/admin`, `/unadmin`).
Nobody can act on someone of their own rank or higher. A ban covers the profile and every device it was used on.
Ops and up get the **Admin** tab: kicks, bans and roles per pilot, the bans with Unban; admins also the server status,
announcements, every profile, the factions and the server settings.

| Command | Who | Meaning |
|---|---|---|
| `/kick <pilot> [minutes] [reason]` | ops | Drop a pilot; they can't rejoin for the minutes (default 5). |
| `/tempban <pilot> <minutes> [reason]` | ops | Ban for a while. |
| `/ban <pilot> [reason]` | admins | Ban for good. |
| `/unban <pilot or id>`, `/bans` | ops | Lift a ban; list them. |
| `/mute <players> [minutes]`, `/unmute` | admins | Silence a player's chat. |
| `/say <text>` | admins | An announcement to everyone. |
| `/news <text \| clear>` | admins | A GalNet item on every station's news ticker. |
| `/motd [reload \| set <text> \| clear]` | everyone / admins | Show the message of the day; admins change it (see [SERVER.md](../SERVER.md)). |
| `/settings`, `/set <key> <value>` | admins | The server's settings while it runs. |
| `/disband <TAG>` | admins | End a faction. |
| `/staff` | everyone | Who the masters, admins and ops are. |

### Admin tools

They work on players by name, by client id, or with a selector: `@a` everyone, `@s` yourself, `@p` the nearest other
player, `@r` a random one, `@alive`, `@space`, `@docked`, `@dead`, `@survivors`, each narrowed to an orbit with
`[orbit=<station>]` (for example `@alive[orbit=Var Hastra]`). In the chat, leaving out the players means yourself.

| Command | What it does |
|---|---|
| `/tp [players] <player \| station [x y z \| dock]>`, `/tphere <player>` | Teleport to a player, an orbit (by number or name) or into a station's hangar. |
| `/kill`, `/heal`, `/ammo`, `/reveal`, `/peace [players]` | Destroy, repair, refill secondaries, reveal the map, reset the standings. |
| `/give [players] <item> [amount] [mount]` | Items into the hold (docked, `mount` mounts them). |
| `/credits [players] <amount>` | Give credits, or take them with a minus. |
| `/spawn [players] <ship \| object> [race] [count] [enemy \| friendly \| neutral] [named <name>] [at x y z]` | Ships or scenery near the players; `named` puts a name on them. |
| `/ship [players] <ship \| own>` | Swap the players' ship, or back to their own. |
| `/cheat [players] <god \| ammo \| cooldown \| primary \| boost \| onehit \| locks \| shopping \| jumps> [on \| off]` | A cheat for those players, this session only. |
| `/title`, `/timer`, `/dialog`, `/reward`, `/radio`, `/sound`, `/music` | Titles, countdowns, conversations, rewards and sound (mostly for events). |
| `/event <name \| stop \| list>` | Run an event graph. |
| `/flightmodel [gof2 \| es2 \| player]` | The flight model everyone flies: `gof2` (the game's, the default), `es2` (EVERSPACE 2 style) or `player` (each player's own Options choice). One model for everyone keeps PvP fair. No argument shows it. |

## What works differently from single player

| Single player | Multiplayer |
|---|---|
| Menus, conversations and the map pause the game. | Nothing pauses: the flight controls stop instead while a menu is open. |
| Save and load. | No saves; a server keeps your profile. |
| Each station's stock is your own, rerolled after 3 other stations. | Shared stock, rerolled every 15 minutes; rare goods restock slowly. |
| The story runs. | The finished game's world, no story. |
| Medals, any difficulty, either economy. | No medals, Normal, the Android economy. |
| The Time Extender works. | It doesn't. |
| Destroyed: load the last save. | Destroyed: respawn at the orbit's station, or your faction's home. |

## Troubleshooting

| Problem | What to do |
|---|---|
| "needs <version>" / "This game runs version X" | You and the server must run builds of the same code: update to the same release. |
| "Wrong password." / "needs a password" | Type the password in the Password field before joining. |
| "The game is full (N players)." | The server is at its player limit; try later. |
| "needs mods: ..." | Install the listed mods (same versions) in your Mods folder. |
| "No game with that code" | The code is wrong, or the game has ended. |
| A port is in use (hosting) | The card puts the next free port in the field; press Host again. |
| "The connection to the host was lost." | The host or your network dropped. Join again; a server keeps your progress. |
| "The server didn't save your progress: ..." | The server refused a save (see [Your progress](#your-progress)). Ask an admin. |
| The chat or the window is hard to read on a phone | The large layout switches on by itself on small high-density screens. |

## Command reference

Every command is also listed in the game by `/help` (only the ones you can use).

| Command | Who | Section |
|---|---|---|
| `/help`, `/players`, `/pos`, `/netstats` | everyone | [Chat](#chat) |
| `/g`, `/l`, `/w` | everyone | [Chat](#chat) |
| `/invite`, `/leave`, `/sos`, `/assist` | everyone | [Squads](#squads) |
| `/link`, `/control`, `/profile` | everyone (servers with profiles) | [Your progress](#your-progress) |
| `/faction ...`, `/f` | faction members | [Factions](#factions), [Territory](#territory-claims-sieges-and-garrisons) |
| `/duel`, `/accept`, `/decline`, `/ffa`, `/arena`, `/top` | everyone | [Arena matches](#arena-matches) |
| `/motd`, `/staff`, `/claimadmin` | everyone | [Moderation](#moderation-and-admin-commands) |
| `/kick`, `/tempban`, `/unban`, `/bans`, `/op`, `/deop` | ops and up | [Moderation](#moderation-and-admin-commands) |
| `/ban`, `/mute`, `/say`, `/news`, `/settings`, `/set`, `/disband`, admin tools, `/event` | admins | [Moderation](#moderation-and-admin-commands) |
| `/admin`, `/unadmin`, `/deleteprofile` | master (or the host) | [Moderation](#moderation-and-admin-commands) |

All faction subcommands: `create TAG Name`, `invite <pilot>`, `join TAG`, `leave`, `kick <pilot>`,
`promote <pilot>`, `demote <pilot>`, `leader <pilot>`, `disband`, `info [TAG]`, `list`, `deposit N`, `withdraw N`,
`claim`, `unclaim`, `home`, `claims [TAG]`, `garrison <fighters> <level> [station]`, `siege`, `sieges`.
