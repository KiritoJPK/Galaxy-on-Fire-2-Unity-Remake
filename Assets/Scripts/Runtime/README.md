# Runtime code layers

The runtime scripts are split into assemblies (asmdefs) that only reference the layers below them. A layer never calls a
layer above it directly: what it needs from above goes through a hook class it owns, which the upper layer fills in at
startup. That keeps single player free of multiplayer, and keeps Netcode's IL post-processors (and a recompile of the
lower layers) off the code that doesn't need them.

```
GoF2.Core      Core/            pure rules and utilities, no Unity package references (JavaRandom, MiningGame, Cloak,
                                Hitpoints, CollisionVolume, HackingGame, BuildVersion...)
GoF2.Game      Game/, Modding/  the game's state and rules: Session, Database, Story, Shop, Freelance, Standing, saves,
                                settings, and the mod loading (Modding/ joins through GoF2.Game.asmref)
GoF2.Space     Space/           the flight and station levels: flight, combat, NPCs, the world (Space/World), the mod
                                models (Space/ModBuilding), and the few UI helpers the layers above share (Space/UI:
                                ItemInfo, Portrait, StarMapTypes, UiHooks)
GoF2.Net       Net/             multiplayer (Net/Multiplayer: Netcode for GameObjects, Transport, Relay / Lobby) and the
                                event graphs (Net/Events)
GoF2.App       App/             the screens (App/UI), VR (App/Vr), Bootstrap and the hook wiring (AppHooksInstaller)
```

Beside them: `GoF2.TextureEncoder` (the Burst texture encoder, below Game), `GoF2.EntitiesBootstrap` (the empty Entities
world, the only code that references Unity.Entities), and in `Scripts/Tests` `GoF2.Tests` (edit-mode tests).

## Hooks

| Hook class | Owned by | Filled by |
|---|---|---|
| `Data.GameHooks` | Game | `Flight.SpaceHooksInstaller` (Space), `Multiplayer.NetHooksInstaller` (Net) |
| `Data.NetSession` | Game | `NetHooksInstaller` |
| `World.SpaceHooks` | Space | `NetHooksInstaller`, `AppHooksInstaller` |
| `UI.UiHooks` | Space | `AppHooksInstaller` |

Small state the lower layers read directly lives in Game: `Data.VrFlags` (VR on, the cockpit seat), `Data.MenuRequest`
(the main menu's panel to open).

The installers run at `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, and in the Editor on every load
(`Editor/EditorHookInstaller`), so edit-mode tools see the same. Every hook answers like single player without events
while unset. `GoF2.Tests`' `LayerHookTests` fails when a hook nobody fills is added: add it to the installer of the layer
that provides it (or, for one a level sets while it runs, to the test's `SetAtRuntime`).

Network objects the levels need to see (`NetPlayer`, `NetProxy`, `NetCrate`, `NetOrbit`, the arena's match) implement
Space's interfaces (`IOrbitPeer`, `IOrbitProxy`, `IOrbitCrate`, `ISharedOrbit`, `ArenaMatchInfo`).

## Adding code

- A new file goes in the lowest layer whose code it needs. If it needs something from above, add a member to that
  layer's hook class (with a single-player fallback) instead of a reference.
- Namespaces don't follow the assemblies (`GoF2Remake.UI` exists in Space and App, `GoF2Remake.Flight` in Game and
  Space): the folder decides the assembly.
- Only GoF2.Net and GoF2.App reference `Unity.Netcode.Runtime` (App for the multiplayer screens' NetPlayer members).
  GoF2.Space and below must not: that is what keeps single player compiling without Netcode.
