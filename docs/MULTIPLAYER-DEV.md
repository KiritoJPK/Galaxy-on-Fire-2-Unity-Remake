# How multiplayer works (for programmers)

This page explains the multiplayer code: what runs where, who decides what, and where to look when you change it.
It is for people who work on the code. The player guide is **[MULTIPLAYER.md](MULTIPLAYER.md)**; the server operator
guide is **[SERVER.md](../SERVER.md)**. The full, detailed notes for every feature are in `CLAUDE.md` ("Multiplayer").

All multiplayer code is in `Assets/Scripts/Runtime/Multiplayer` (namespace `GoF2Remake.Multiplayer`). The event graphs
are in `Assets/Scripts/Runtime/Events`. Single player never runs any of it: every multiplayer path checks
`NetGame.Active` first.

## The big picture

- **Stack:** Netcode for GameObjects 3.0 over Unity Transport. Online play goes through Unity Relay (join codes) and
  Unity Lobby (the server browser). Local play connects by IP on port 7777.
- **No shared scenes.** Netcode's scene management is off. Every player runs their own game (their own Space and
  Station scenes, map and docking), like single player. The network objects live in `DontDestroyOnLoad`, and each
  player shows only what is where they are.
- **No server-side world.** The server does not simulate space. Each player's game simulates its own ship. NPCs in an
  orbit are simulated by one player's game (the *orbit authority*, below).
- **The server checks and relays.** Every client request goes to the server. The server checks it, limits its rate,
  and acts or passes it on (see [Trust and checks](#trust-and-checks)).

## The network objects

The prefabs are in `Resources/GoF2Net`, made by **GoF2 > Build > Network Prefabs**.

| Object | One per | What it does |
|---|---|---|
| `NetState` | session | The session's shared state (seed, claims, sieges, toll, flags) as NetworkVariables, and most server RPCs. |
| `NetPlayer` | player | Where the player is (station, in space / hangar), ship, name, pose, hull / shield, faction tag, squad. Written by its owner. |
| `NetProxy` | NPC shown to others | A copy of an NPC run by another player's game: model, pose, race, hull, life. |
| `NetCrate` | crate shown to others | A copy of a crate run by another player's game, with its claim. |

`NetPlayer` shows the other players' ships in the same orbit (`SharesOrbit`) with smoothed motion (`NetSmoothing`).

## Starting a session

1. `NetGame.PrepareSession` sets up a fresh free-play game in the finished world (`Session.UseCompletedWorld`): the
   starting ship, credits and loadout.
2. The host (`NetGame.StartHost`) or the dedicated server (`NetGame.StartServer`) spawns `NetState`.
3. A client connects. Connection approval (`NetGame.Approve`) checks the build fingerprint, the password, the player
   limit and the mods.
4. When `NetState` reaches the client, it enters the world (`NetGame.EnterWorld`): the Station scene loads.
   A server with profiles first signs the player in and sends their profile (`NetProfileClient.Begin`).

The **build fingerprint** (`BuildVersion.Fingerprint`) is a hash of the runtime scripts, the network prefabs, the game
data and the multiplayer package versions. Only games with the same fingerprint can play together.

## Who decides what

| Thing | Decided by |
|---|---|
| The player's own ship, credits, cargo, standing, position | The player's own game (trusted, within checks). |
| NPCs and crates in an orbit | The **orbit authority**: the first player in an empty orbit (`NetState.OrbitEmpty`). |
| Shop stock | The server (`NetStock`): one list per station for everyone. |
| Factions, claims, sieges, garrisons, bans, profiles | The server (`NetFactions`, `NetModeration`, `NetProfiles`). |
| A squad's bar mission orbit | The first squad member there (`NetMissions`). |
| An event graph | The server (`EventRunner`). |

### The orbit authority

- The first player who arrives in an empty orbit builds its traffic (`SpaceLevel.NetAuthority`). Their game runs the
  NPCs and shows them to the others as `NetProxy` objects.
- When that player leaves, the remaining player with the lowest client id takes the ships over where they are
  (`Traffic.Adopt`, `SpaceLevel.TakeOverNetAuthority`).
- Two players arriving at the same moment: in the first 15 seconds the higher client id stands down.
- Hits on another game's NPC are sent to its owner (`Target.RemoteDamage`), which applies them.

### Shots

A shot is fired in the shooter's game (`Gun.Fired`), sent to the others (`NetShotSender`, unreliable), and replayed by
`NetShotMirror` as a visual copy. Mirrors show impacts but deal no damage: the shooter's game applies the hits.

## Server features at a glance

| Feature | Main files | Notes |
|---|---|---|
| Chat and commands | `NetChat`, `NetCommands`, `NetAdmin` | Every command that changes the session runs on the server. The console runs the same table. |
| Squads | `NetSquad`, `SquadView` | Invitations recorded on the server. |
| Shared shop | `NetStock` | 15-minute reroll; rare goods (max price ≥ 5000) restock a third every 10 minutes. |
| Profiles | `NetProfiles`, `NetProfileClient` | Uploads checked like an imported save (`SaveGame.TryParse`) and against a worth limit. |
| Factions and territory | `NetFactions`, `NetFactionsClient`, `TerritoryView` | Claims, sieges, garrisons (server-side count, the authority flies them: `NetOrbit.UpdateGarrison`). |
| Arena | `NetArena`, `NetArenaClient`, `ArenaView` | A private copy of the Void orbit per match (orbit id 100000 + match). |
| Moderation | `NetModeration`, `WebAdmin` | Roles on the profile; bans by profile and device. |
| News and MOTD | `NetNews`, `NetMotd` | MOTD from `motd.txt`, shown in `EventScreen`. |
| Server settings | `NetServerSettings` | `/set`, the Admin tab, `server_settings.json`. |
| Dedicated server | `DedicatedServer`, `ConsoleInput`, `WinConsole` | The normal player started with `-server`. |

## Trust and checks

The server can not see the world, so it checks what it can (`NetGuard`, `NetRateLimit`):

- Messages from the server to players use `InvokePermission = RpcInvokePermission.Server`, so a client can not send
  them. Netcode's default lets any client send any RPC.
- Numbers must be finite and in range; stations, items and ships must exist; the sender must be where the request
  says (in that orbit, docked there).
- Every client request kind has a token bucket. A flood is dropped; 1500 weighted drops in a minute kick the client.
- Profile uploads are checked like an imported save and against `-maxearn` (worth gained per minute).

Still trusted: each player's own ship, credits, cargo, standing and position, and the orbit authority's NPCs. A
modified game can cheat in those. Keep that in mind when you add a feature: anything worth protecting must be decided
on the server.

## Adding a multiplayer feature: checklist

1. Guard every new code path with `NetGame.Active` so single player stays the same.
2. Decide who owns the state: the player's game, the orbit authority or the server.
3. Client to server: `[Rpc(SendTo.Server)]` on `NetState`, rate-limited with `NetRateLimit.Allow`, checked with
   `NetGuard`.
4. Server to clients: `InvokePermission = RpcInvokePermission.Server`; to one player with
   `RpcTarget.Single(client, RpcTargetUse.Temp)`.
5. Keep one RPC under Unity Transport's 6144-byte payload; split bigger data into chunks (see `NetPanel`,
   `NetProfiles`).
6. NetworkVariable strings are `FixedString` (4 KB at most); use `NetState.Fit` to cut at a whole line.
7. Static fields need `[Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]` and a reset in
   `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` (Play mode runs without a domain reload).
8. A chat command: add a row to `NetCommands` (it then shows in `/help`, Tab completion and the console).
9. Buttons: add them to `MultiplayerWindow`; they send the chat command, so there is one code path.
10. Update `docs/MULTIPLAYER.md`, `SERVER.md` (server options) and `CLAUDE.md`.

## Testing

- **Editor as host + a Windows development build as client:**
  `GoF2Remake.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -mpjoin 127.0.0.1 -mpname Pilot2`.
  Set `Application.runInBackground = true` during Play, or Play mode stalls when the Editor loses focus.
- Development builds also take `-mphost` (host from the menu), `-mpdock` (dock a few seconds into the first flight)
  and `-mpaccept` (accept squad invitations), so flows run without a hand on the client.
- A dedicated server in the Editor: set the `GOF2_SERVER` environment variable (`relay` for online).
- An edited script recompiles and ends the session.
- **Switch the active build profile before building another platform** (URP keeps shader variants for the active
  platform only).
