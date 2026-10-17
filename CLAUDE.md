# Galaxy on Fire 2 remake (Unity)

A remake of the 2010 mobile game *Galaxy on Fire 2* (Fishlabs / Deep Silver) in Unity. The goal is a faithful port of the original gameplay (flight, combat, trading, stations, missions), built on the original assets and on logic ported from the decompiled game code.

## Environment

- Unity **7000.0** (alpha 7000.0.0a10, from 6000.7.0b3 on 2026-10-06, a8 on 2026-10-10, a9 on 2026-10-14, a10 on 2026-10-16; .NET CoreCLR instead of Mono), **URP 17.7**, Windows.
- **Embedded `Packages/com.unity.netcode`** (Netcode for Entities 7.1.0, which Netcode for GameObjects 3.1 pulls in; copied
  without its docs and tests): a9's built-in Entities 6.7 dropped the DOTS Hierarchy window, so the registry package's
  `Editor/Authoring/HierarchyDrawers.cs` didn't compile; its `DotsHierarchyItemDecorator` is removed there. Delete the folder
  once a newer Netcode for Entities compiles on the Editor in use.
- **Scripting backend:** IL2CPP for Windows / Linux (Standalone; the Editor modules "Windows / Linux Build Support (IL2CPP)";
  Linux builds from Windows use `com.unity.toolchain.win-x86_64-linux` + `com.unity.sdk.linux-x86_64`, which the first Linux
  IL2CPP build added) and Android; native callbacks must be static `[AOT.MonoPInvokeCallback]` methods (`WinConsole`). A
  build folder made with Mono needs clearing before an IL2CPP build. Never ship the `*_BackUpThisFolder_ButDontShipItWithYourGame`
  folder (IL2CPP's debug data) in a release.
- **Input System package only.** Active Input Handling is set to the new system. Never use `UnityEngine.Input` or the legacy Input Manager.
- Packages include `com.unity.pipeline` (Unity CLI bridge; 0.8.0-exp.1 or later: 0.7 listened on `http://+:<port>/`, which
  CoreCLR's HttpListener on Windows refuses without admin rights, "No available ports in range 7800-7849"). The package
  doesn't stop its server before a code reload, and the old listener's http.sys registration outlives the reload, so each
  recompile used to keep one port of that range until the Editor quit; `PipelineReloadGuard` (Editor) stops it first. `com.unity.ai.assistant` was removed (its AI Generators logged
  "NoSubscription" errors), so there is no Unity MCP server.
- Driving the Editor: if the `unity` CLI is available, use it to read the Console, run menu items, enter Play mode and inspect the scene. Check with `unity status` first. When an Editor is connected, don't hand-edit `.unity` / `.prefab` / `.asset` YAML.

## Layout

```
Assets/
  Scripts/Runtime/   the runtime code in layered assemblies, lowest first (Scripts/Runtime/README.md: the layers, the hooks):
    Core/            GoF2.Core: pure rules / utilities (JavaRandom, MiningGame, Cloak, Hitpoints, CollisionVolume...)
    Game/, Modding/  GoF2.Game: state and rules (Session, Database, Story, Shop, Freelance, saves, Settings), mod loading
    Space/           GoF2.Space: ship controller, chase camera, combat, keyframe player (FlightModel is in Game); Space/UI: the
                     helpers the screens share (ItemInfo, Portrait, StarMapTypes, UiHooks); Space/ModBuilding: mod models
    Space/World/     station orbits: OrbitLayout (seeded layout, in Game), OrbitBuilder (layout -> scene, shared by
                     the flight level and the menu background), SpaceLevel, Backdrop, SpaceDust,
                     SystemJump (jumpgate / Khador travel), Traffic + NpcShip (NPC ships), Wormhole (the Void's wormhole);
                     story: StorySpace, CampaignLevel, IntroCutscenes (0 / 1), MainCampaignLevels (14-42),
                     ValkyrieLevels (48-81), SupernovaLevels (87-158), GasCloudField, NpcCloak;
                     docked station: StationLevel + StationTables; SpaceHooks (what the levels need from Net / App)
    Net/Multiplayer/ GoF2.Net: multiplayer (see "Multiplayer")
    Net/Events/      the event graphs (single player quests / bar missions and multiplayer events alike):
                     EventRunner (runs them), EventGraphFile + EventGraphScript (read / compile a .gof2event),
                     EventHost / IPilot / LocalPilot / LocalEvents (session or single player), EventMissions, EventScreen
                     (titles, dialogues, rewards, questions), EventCutscene, EventWaypoint, EventRespawn, EventRules,
                     EventAudio, EventNames; the commands they run stay in Multiplayer (NetCommands, NetAdmin)
    App/             GoF2.App: Bootstrap, AppHooksInstaller; App/UI the screens, App/Vr VR
  Scripts/Editor/    import settings, prefab builder, asset pack installer, menu items
  Models/            1,125 converted .fbx meshes, each with a .gof2mesh.json sidecar (pivots, keyframes)
  Textures/          1,281 .png (diffuse, *_normal_specular = normal map, *_metallic_smoothness generated)
  Audio/             4,416 .ogg, one folder per original FMOD bank (English + German voices)
  Resources/GoF2Data/ all game data as JSON (see "Data" below)
  Localization/      14 languages: text_<lang>.json + GoF2_strings.csv (text_nl.json = the remake's Dutch translation of the
                     whole table, text_hi.json its Hindi one, text_zh.json Simplified Chinese from PR #50 (FantasyToLife)
                     with the entries it left in English translated; the remake's own `Localization.Extra` texts per
                     language in `Resources/GoF2Localization/extra_<lang>.json`, see "UI and platforms")
  Shaders/           Shader Graphs (URP Unlit): GoF2/Unlit, Additive, AlphaBlend, AlphaTest; _Glow > 1 = HDR for bloom.
                     Hand-written HLSL where Shader Graph can't: GoF2/SpaceSky (skybox), Backdrop (far-plane quads), SpaceDust,
                     Cloak and ShieldBubble (screen refraction: they switch the camera's opaque texture on, `OpaqueTexture`),
                     HangarShadow (the hangar ships' contact shadows, projected from the camera depth texture), Hull (URP Lit + the
                     original's hull reflection and rim, see "Space scene")
  UI/                UI Toolkit: GoF2Theme.tss + GoF2Common.uss (tokens, control styles), GoF2InputGlyphs.uss (hint rows,
                     keycaps, Xbox buttons), GoF2PanelSettings, fonts: Serpentine ICG Light (the original's interface typeface, font 1111's bitmap glyphs; the default and, with the asset's faux bold, `.gof-semibold`) falling back to Inter (OFL) for Cyrillic and Latin Extended-A, MainMenu/ (UXML, USS, cut images,
                     menu post-processing profile), Flight/ (HUD), Station/ (station menu), StarMap/ (star map overlay)
  Skyboxes/          skybox_0XX.png (6-face strip -> Cubemap) + .mat, old combined bakes (only the Flight Test scene uses one)
  Materials/, Prefabs/ generated by the "GoF2 > Build > Materials And Prefabs" menu item (one prefab per game mesh)
  Resources/Assembled/ one prefab per game object (ship, station, jumpgate, hangar...), see "Assembled prefabs".
                     In Resources so levels load only what they use (AssembledObject.LoadPrefab)
  Resources/GoF2Sky/ flight-level sky: stars_00X + nebula_0XX cubemaps, SpaceSky.mat template
  Resources/GoF2Backdrop/ sun / planet / ring / dust materials, one per texture (loaded by name)
  Resources/GoF2Icons/ shop icons item_XXX / ship_XXX (frame + icon, 180x88), made by "Build Item Icons"
  Resources/GoF2Hud/ flight HUD images (lock ring, plate, minigame, brackets, autopilot / fast-forward, star map rings and
                     icons, race logos), made by "Build HUD Images"
  Resources/GoF2StarMap/ StarMapAssets: what the star map can't load by name (overlay UXML, sun materials, Khador fx, sounds)
  Resources/GoF2Combat/ CombatAssets: crates, wrecks, explosion, tractor beams, hit / death sounds, space and battle music
  Resources/GoF2LanguageTables.asset references every text table so any scene loads text on first use (Localization)
  Scenes/            MainMenu, Space (flight level), Station (docked: hangar + bar)
  Settings/          URP assets, GoF2_VolumeProfile (bloom)
Reference/           decompiled original code, binaries and conversion tools (see Reference/README.md)
Modding/             the modders' guide (README.md) and example mods (Examples/plasma_arsenal); Scripts/Runtime/Modding the loader
Mods/                the mods the Editor loads (git-ignored)
```

Menu items (from `Scripts/Editor`), grouped in submenus; **GoF2 > Tools Overview** (`GoF2ToolsWindow`) lists them all with what each makes and a Run button (keep its table in step when adding or renaming a tool):

- **GoF2 > Scenes > Main Menu Scene**: `Assets/Scenes/MainMenu.unity` (build index 0; Space 1, Station 2).
- **GoF2 > Scenes > Space Scene**: `Assets/Scenes/Space.unity`, the flight level (see "Space scene"). Also (re)makes `Resources/GoF2Backdrop` and bakes the space skies if missing.
- **GoF2 > Scenes > Station Scene**: `Assets/Scenes/Station.unity`, the docked station (see "Station scene"). Wires the bar visitor prefabs, glow materials, music, ambience and language tables.
- **GoF2 > Scenes > Add Post Processing To Open Scene**: global Volume with `Assets/Settings/GoF2_VolumeProfile.asset` (Bloom, threshold 1) + camera post-processing on.
- **GoF2 > Build > Modding AI Reference** (`ModdingReferenceBuilder`): `Modding/ai/gof2-modding/reference.md`, every number and name of the original game a mod or event graph refers to (Android economy): items with every stat / raw attribute, item categories, attribute numbers, blueprint recipes, ships (model, hangar height), systems (sky), stations (race, planet, model), story steps, story speakers, every assembled model with the texture names a skin replaces, backdrop textures, music, sound effects, the event sound / music words, the built-in event graphs; ship hull sizes and every original ship's hardpoints as `mounts` JSON, the Default economy's differences, what each sky / sun / planet looks like (hand-written tables `SkyLooks` / `SunLooks` / `PlanetLooks`: update them when a texture changes); `voices.md` (every voice clip with its speaker and English line, for `[voice <clip>]`); `quests.md` +
  `quest.schema.json` (`Events.QuestSchema`, made from `QuestSteps` and `QuestGraph.ValueTypes`: every `.gof2quest` step,
  field, kind and default, a JSON Schema 2020-12 with shared `$defs` who / text / value / wire; a new step row there is in
  them at the next run); `events.md` (`ModdingEventsReference`: every event graph node's options and ports, read from `EventGraphNodes.cs` by a small tokenizer, so keep the node classes in that file's style, plus every command from `NetCommands.Reference`); and `guide.md`, a copy of `Modding/README.md`. For the AI modding guide (modders kept asking for ids). Run it again after changing the game data; headless: `Unity.exe -batchmode -nographics -projectPath . -executeMethod GoF2Remake.EditorTools.ModdingReferenceBuilder.Batch -quit`.
- **GoF2 > Build > Combat Assets**: `Resources/GoF2Combat/CombatAssets` (`CombatAssets`). Also run by Create Space Scene.
- **GoF2 > Build > Star Map Assets**: `Resources/GoF2StarMap/StarMapAssets` (`StarMapAssets`). Also run by Create Space Scene, and by Create Station Scene when missing.
- **GoF2 > Build > Network Prefabs**: `Resources/GoF2Net`, the multiplayer network prefabs (see "Multiplayer").
- **GoF2 > Build > Linux Dedicated Server** (`LinuxServerBuild`): `Build/LinuxServer/GoF2Server.x86_64`, Unity's Dedicated Server build (Linux, subtarget Server, IL2CPP, the dedicated server optimizations on: no texture / shader data; no audio either: `ServerAudioStrip` clears the AudioClip references of the Resources assets for the build, 4346 clips / 171 MB, backed up in `Library/GoF2ServerAudioBackup` and put back afterwards (also on the next Editor load after a crash), the scene's clips in the build's copy (`AssetPostprocessor.OnProcessScene`); Minimal managed stripping (the server target had it off) and IL2CPP's OptimizeSize code generation, restored after the build like the target: GameAssembly.so 206 -> 82 MB, the zip 323 -> 126 MB), only the first scene; it starts as a server by itself (`UNITY_SERVER` in `DedicatedServer.Enabled`), `start-server.sh` beside it. Needs the Hub module "Linux Dedicated Server Build Support"; switches the active target for the build and back: the menu items switch, wait for the scripts to recompile and build after the domain reload (SessionState carries the job), since the toolchain packages only join the Linux build while the scripts are compiled for Linux (`UnityEditor.Sysroot` behind `UNITY_STANDALONE_LINUX_API`; built in the switch's own call: "No Linux sysroot found for x64"); `LinuxServerBuild.Busy` / `LastResult` for scripts. The game's code is 10 % of the server's generated C++ (packages, UI Toolkit, .NET and IL2CPP's generics the rest), so splitting client code into assemblies the server leaves out was judged not worth its refactor (2026-10-14). `LinuxServerBuild.Batch` is its command line (exits 1 on a failed build). Docker (`docker/`, SERVER.md "Docker"): `build-server.sh` builds it in GameCI's base image with the Editor of `ProjectVersion.txt` (the account only for the licence calls, never in the Editor's environment); **GoF2 > Build > Linux Dedicated Server (Release Zip)** (`LinuxServerBuild.ReleaseZip`) is the same build as `Build/Release/GoF2Remake-LinuxServer-<tag>.zip` (top folder GoF2Server, the tag = GOF2_BUILD_VERSION's date; the entries marked as made on Unix with their modes, `MarkMadeOnUnix`: .NET writes the host it runs on and unzip ignored the modes), built with a release's other platforms and attached to it. Every published release runs `.github/workflows/server-image.yml`: with that zip in the release it unpacks it (only `docker/` checked out, no licence) into the image, so the image is the release's own build; without it it builds the release's commit (textures, Reference/, Modding/ left as LFS pointers; secrets UNITY_EMAIL / UNITY_PASSWORD; a manual run takes a tag and optionally the shown version, GOF2_BUILD_VERSION) and attaches that zip to the release; either way it checks the server starts and pushes `ghcr.io/joppietoppie/gof2-server:<tag>` and `:latest` (not for a pre-release). The release tag must be the commit the release was built from, or the fingerprints differ.
- **GoF2 > Build > Event Audio**: `Resources/GoF2Events/EventAudio`, the sounds and music the event graphs play (see "Multiplayer", Events).
- **GoF2 > Build > Hangar Heights**: `Resources/GoF2Data/hangar_heights.json`, how far each ship is lifted off each hangar pad (see "Station scene"). Run it again after changing a hangar room or a ship model.
- **GoF2 > Build > Nozzle Shapes** (`NozzleShapesBuilder`): `Resources/GoF2Data/nozzle_shapes.json`, each game ship's (0..63) exhaust nozzles as their outline (radius at 32 angles), extents and centre, measured on its player engine glow mesh (vertices to the nearest slot-3 mount; the game's meshes are only readable in the Editor), for the mods' exhaust styles (see "Mods", Exhaust styles). Run it again after changing a ship model or its exhaust mounts.
- **GoF2 > Build > Void Station Collision** (`VoidStationCollisionBuilder`): `Resources/GoF2Data/void_station_extra.json`, extra collision boxes for the Void station's outer blades and lower spire (see "Space scene", Alien orbit). Run it again after changing the station's model or animation.
- **GoF2 > Build > Hangar Shadows**: `Resources/GoF2Station/ShipShadows` (+ `ShipShadows.asset`, `HangarShipShadow.mat`), each ship's contact shadow in the hangars (see "Station scene"). Run it again after changing a ship model.
- **GoF2 > Build > HUD Images**: `Resources/GoF2Hud`, the HUD / star map images and the alien font glyphs cut from the original interface atlases (rects in `Reference/research/mining.md`, `autopilot_travel.md`, `starmap_travel.md`). Also run by Create Space Scene.
- **GoF2 > Build > Item Icons**: `Resources/GoF2Icons`, one icon per item and ship cut from the original atlases per `Reference/research/item_icons.json` (see "Shop").
- **GoF2 > Build > Hull Icons** (`HullIconBuilder`): `Resources/GoF2Icons/hull_<key>.png`, shop-style icons for the hulls without a shop image (the Debug Ships tab's "Not normally flyable": the freighters, the Terran battleship, the carrier, the Vossk battleship, the Valkyrie battlestation, the Void mother ship; 13-15's own images are a "?"): each model rendered in a preview scene from its left, above and a little ahead, the nose to the bottom left like the original's ship icons, a wide or tall hull turned to fill the icon, fitted to the pixels drawn (alpha from a render on black and one on white), over the ship frame 1276; read by `ItemInfo.HullIcon`. Run it again after changing those models.
- **GoF2 > Build > Text Icons**: `Resources/Sprite Assets/gof2_text_icons` (a TextCore sprite asset + its 64 px atlas, `TextIconsBuilder`): the dialogue's inline icons: a remake-drawn coin (and a heart, last in the atlas, for the main menu's credit line), the race emblems (GoF2Hud race_0/1/2/3/8/9, trimmed to their opaque part), the jumpgate / wormhole / blueprint (map_products) / ore core / container (crate_off) / autopilot icons, every item and ship shop icon (the centre square of the plate). The runtime panel text settings find it by name under `Sprite Assets/`, so no PanelTextSettings asset exists.
- **GoF2 > Build > Sky Layers**: `Resources/GoF2Backdrop/SkyLayerAssets` + the `sky_*` materials (see "Space scene"). Also run by Create Space Scene.
- **GoF2 > Build > Space Skies**: stars (3) and nebula (19, incl. Valkyrie/Supernova) layers as separate cubemaps for the flight levels (2048 px faces, Android 1365: at 1024 the 2048 px sky textures were magnified on a 1080p screen and the stars blurred).
- **GoF2 > Build > Materials And Prefabs**: one material per game material and one prefab per game mesh, from `resources.json`.
- **GoF2 > Build > Assembled Prefabs**: one prefab per game object, from `assemblies.json` (see "Assembled prefabs").
- **GoF2 > Import > Apply Emissive Glow To Materials**: `_Glow` = 4 on emissive/lights materials, 2.5 on additive ones, 1 elsewhere (skyboxes never bloom; lit alpha-test layers neither, whatever the mesh is called: the wrecked station's `*_alpha_emissive` girders bloomed the red of their cut-away texels). Constants in `PostProcessing.cs`.
- **GoF2 > Import > Reimport Models Only**
- **GoF2 > Import > Reapply Import Settings**
- **GoF2 > Setup > Install Asset Pack**: one-time setup, already done.
- **GoF2 > Legacy > Flight Test Scene**: saves `Assets/Scenes/FlightTest.unity` (assembled Betty + chase camera), an old test scene: not kept in the repo or the build.
- **GoF2 > Legacy > Bake Combined Skyboxes**: the old combined sky bakes (`Skyboxes/`, stars layer not matched to the system); only the Flight Test scene uses them.
- **Assets > Create > GoF2 > Event Graph** / **Event Graph From Template**: a multiplayer event graph (see "Multiplayer", Events).

## Conventions

- **Layered assemblies** (`Scripts/Runtime/README.md`): GoF2.Core <- GoF2.Game <- GoF2.Space <- GoF2.Net <- GoF2.App, each
  referencing only the ones below. The folder decides the assembly, not the namespace. A lower layer never names a type of
  a higher one: it adds a member to its hook class (`GameHooks`, `NetSession`, `SpaceHooks`, `UiHooks`) with a single-player
  fallback, and the higher layer's installer (`SpaceHooksInstaller`, `NetHooksInstaller`, `AppHooksInstaller`) fills it
  (`LayerHookTests` checks none is left unset). Only GoF2.Net and GoF2.App reference Netcode. New files in `Space/` keep the
  `GoF2Remake.Flight` / `World` / `Visuals` / `UI` namespaces: never `GoF2Remake.Space` (see below), whatever an IDE derives
  from the folder.
- Namespaces: `GoF2Remake.Flight`, `GoF2Remake.Data`, `GoF2Remake.Visuals`, `GoF2Remake.World` (flight level), `GoF2Remake.UI`, `GoF2Remake.Events` (the event graphs), `GoF2Remake.Multiplayer`, `GoF2Remake.Modding`, `GoF2Remake.EditorTools`. Never a namespace segment that shadows a Unity type (`GoF2Remake.Space` broke `Space.Self`).
- **One MonoBehaviour/ScriptableObject per file, and the file name must equal the class name.** Otherwise prefabs save it as a missing script.
- Gameplay logic is a clean C# reimplementation of the behaviour in the decompiled code, not a line-by-line transliteration. Comment the original function names and constants you based it on.
- Keep game-logic classes plain C# where possible (like `FlightModel`) so they can be unit-tested. MonoBehaviours adapt them to Unity.
- A type with static fields, auto-properties or events carries `[Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]`
  (Unity 6.7's UAL0010 / UAL0013 analyzer): Play mode runs without a domain reload and the code resets what it needs itself
  (`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`). Use `FindObjectsByType<T>()` / `(FindObjectsInactive)`, never the
  obsolete `FindObjectsSortMode` overloads.
- Game time is in **milliseconds**. Convert with `Time.deltaTime * 1000f`.
- **Tests** (`Scripts/Tests/README.md`): ~3000 edit-mode tests of the rules and data (`EditMode/<area>/`, ~30 s) and play-mode
  smoke tests of the scenes, story levels and a hosted session (`PlayMode/`). Run the edit-mode suite after a change; add a
  test with a new feature. Tests touching state derive from `SandboxTest` / `PlayModeTest` (`GamePaths.Override`: a
  temporary data folder, never the player's saves or mods); code reaching persistentDataPath goes through `GamePaths.Data`.

## Coordinates and scale

- **Game to Unity: `(x, y, z) -> (x, y, -z) * 0.05`** (0.05 m per game unit). Models face **+Z**.
  - The FBX files themselves face -Z. `ModelOrientationPostprocessor` rotates them 180° about Y at import.
- `ImportSettings.ModelScale` (0.05) must match `ShipController.metersPerUnit`.
- Data files with positions (`weapons_hd.json`, `docks_hd.json`) contain `position_file` (as stored) and `position_engine` (game space). Apply the rule above to world positions from `position_engine`; **ship-relative offsets** (weapon mounts) map to Unity ship space as `(-x, y, z) * 0.05` (the models are mirrored and turned 180 deg on import).
- The original stores animation position keys Z-up. The engine uses `(c0, c2, -c1)`. Keyframe rotations are in radians, Rx * Ry * Rz in the file's Z-up frame; `PartAnimation` turns them about Unity x / z / y with the file-Z angle negated (the part frame is the file's through (-x, z, -y), a mirror; see `rotationMap`).

## Data (`Assets/Resources/GoF2Data`, loaded with `Database.Load()`, JsonUtility)

- `Database.Load()` parses every table again (~6 ms and several MB of garbage): use it once per scene (the levels keep
  their own, `level.Database`) or where the result is changed; read-only lookups that run more often (UI rebuilds, spawns,
  multiplayer mirrors, the Mod Builder) take `Database.Shared`, made again only when the economy or the mods change, and
  never write to it.

- `ships`, `items`, `systems`, `stations`: the economy and universe.
  - `items[].statList` has named stats (damage, range, boostSpeed, agility…).
  - `items[].rawAttributes` has the original attribute IDs.
- `item_attributes.json`: every item's original attribute pairs as lists (`ItemData.Attr(id)`), generated by `Reference/tools/shop/build_item_attributes.py` because JsonUtility can't read the `rawAttributes` dictionary.
- `agents` (bar characters), `wanted` (bounty targets), `names`, `ticker`.
- `weapons_hd` (gun mounts per ship: slotType 0 primary, 1 secondary, 2 turret, 3 = engine exhaust points, not turrets), `docks_hd` (docking points), `shipparts` / `stationparts`, collision tables, DLC variants `sn_*` / `v_*`.
- `economy_default.json`: the Default Economy overlay on `items` / `item_attributes` / `ships` (see "Shop").
- `resources.json`: the original resource table. `meshes[]` (id, model path, materialId), `materials[]` (shading type, textures), `textures[]`. IDs match the decompiled code; most main-game ship meshes are 17000 + ship index (a few special ships differ, so look them up by model path).
- Text IDs (`Localization/text_en.json`):
  - ship name = 913 + index, ship description = 977 + index
  - item name = 1274 + index, item description = 1041 + index
  - item category = 221 + categoryId, race = 406 + raceId
- Item attribute keys:
  - 9 damage, 10 EMP damage, 11 loading time (ms), 12 range, 13 projectile speed
  - 18/19 shield capacity/regen, 20 armor, 22 cargo bonus
  - 25/26/27 boost speed/recharge/duration, 28 agility, 31 radar
  - 39/40 fire-rate/damage factor

## Assembled prefabs

The game stores every object as several meshes (hull, `_lights_add`, `_emissive`, `_engine_add`, `_lod_N`, ...) and assembles them in code. `Prefabs/<pack>/...` are the single meshes (1:1 with resource ids, use them when code refers to a mesh id); `Resources/Assembled/<pack>/<category>/` are the assembled objects (root `AssembledObject` + nested per-mesh prefabs + `LODGroup`).

- Rules: `Reference/tools/asset_conversion/assemblies/rules/assemblies_{ships,stations,level,scenes}.json` + `*_notes.md` (functions, addresses, uncertainties). `build_assemblies.py` merges them into `Resources/GoF2Data/assemblies.json`; then run the menu item. Groups with no assembling code get a name-based assembly (`origin` says so).
- `patch_resources.py` added 19 meshes the resource-table emulation had recorded as garbage (burning/wrecked supernova stations etc.), and (remake) the engine glows 17939 / 17941 of ships 39 / 41.
- Ships (`Globals::getShipGroup(idx, race, isPlayer)`): the player ship uses `*_engine_glow_add` (17900+idx), NPCs `*_engine_add` (18000+idx); both are in the prefab, switch with `SetPlayerVariant()`. Battleship (idx 14) is scaled 2x. Ship 15 is the freighter, picked by race. Look ships up with `Database.ShipAssembly(idx, race)`: the add-on ships (39-41, 44+) are `v_ship_NNN_*` / `sn_ship_NNN_*`.
- A ship hull's own keys play from the start (`ImportPostprocessor.IsAnimatedShipHull` in Materials And Prefabs: under
  ships/, not a wreck or explosion): the Cronus' (`v_ship_037_deep_science`) engine pods sit inside the hull at rest and
  hang bobbing under its stern once played; the import played only "_anim" models, so they were hidden and still. Its
  prefab was set by hand (`play: 1`) rather than by a rebuild.
- LOD: each level shows only its LOD mesh, its LOD child meshes and the last-added child (usually the engine). Generic ships switch at 5000/13000 units and cull at 80000. **In this binary `AEGeometry::updateLod` always picks LOD 0** (the modded APK), the prefabs use the intended distances.
- Stations are placed rotated (0, pi, 0); stored as `spawnRotationEngine`, not baked in. State-dependent parts (mission, race, jumpgate activation) are in `conditionalParts` and start inactive.
- Suns and planets are the `plane` mesh textured per system at runtime, and skyboxes pick layers per system: not pre-assembled.

## Space scene

`Space.unity` holds only a camera, two directional lights, post-processing and `SpaceLevel`, which builds the current orbit at startup from `Session.StationIndex` (new game: station 78 Var Hastra, ship 10 Phantom, `Status::resetGame`). Research: `Reference/research/space_level_setup.md`, `space_backdrop.md` (+ `space_backdrop_sim.py`), `space_props.md`.

- **One level = one station orbit.** Station at the origin, the other stations of the system are planets. Visible jumpgate only in the system's `jumpgateStation` orbit.
- **Empty orbits** (`OrbitLayout.IsEmptyOrbit`, `Status::inEmptyOrbit` 0xb8ee8; no docking, no station target): 102-104, 109, 110, 132-134 always, 111 (Luur) above index 0x5d, 101 (Herjaza, the Valkyrie's orbit) from 0x54 (the battlestation is in the Void then), 78 at index 0 / 1 (the prologue, the rescue, a menu backdrop rolled there); the station's model stays in 27 / 110 / 111 (`Level::createSpace` 0xbc0f6, `OrbitLayout.stationObject`). Free play and sessions read index 20.
- **Stable layout:** the original's RNG is `java.util.Random` (`JavaRandom`), seeded per station: jumpgates and sky rotation `2 * station` (separate sequences), sun/planets `300 * station`, asteroid count/centre `station`. `OrbitLayout` reproduces the research tables exactly. Everything the original randomises per visit uses `UnityEngine.Random`.
- **Rotations:** game `setRotation(x, y, z)` = Rx*Ry*Rz; `OrbitLayout.RotationToUnity` mirrors it and undoes the import's 180 deg yaw, so game (0, pi, 0) (stations, gates) = identity and an undocking player faces Unity -Z.
- **Sky:** stars `systemIndex % 3` + nebula `system textureIndex`, rotated per station (`_SkyRotation`); it is `RenderSettings.skybox`, so it also gives ambient light (`SkyReflection.Update` = DynamicGI.UpdateEnvironment wherever the sky is set). The
  environment reflection is the system's reflection cubemap, as the original binds it (`MGame::OnInitialize` /
  `ModMainMenu::OnInitialize`: `TextureCreate(0x2efe + textureIndex)`, the alien orbit 0x2f08, `ChangeCubeTexture`):
  `Resources/GoF2Reflections/cubemap_skybox_000..018` (glossy convolution) as Unity's custom environment reflection
  (`SkyReflection.SetSystem`, from `OrbitBuilder.SetupSky` and `StationLevel`'s lighting; a mod system its template's). No
  realtime probe (both quality levels have them off). `GoF2/Hull` and `GoF2/EmissiveCube` sample it as the global
  `_GoF2ReflectionCube` (set with it, PR #106): `unity_SpecCube0` came out empty in the Android build (Forward on the
  Mobile renderer). **Hangar cubemaps** (remake, PR #110, a departure: the original never binds 0x2ef4..0x2ef7): while
  the hangar is shown its race's own (`SkyReflection.SetHangar(HangarIndex)`, `Resources/GoF2Reflections/cubemap_hangar_terran
  / vossk / nivelian / midorian`, glossy convolution; the Supernova systems too), the Valkyrie hangars (7 / 8) a black one
  (no reflection, the rim stays), a mod's own hangar and the lounge the system's.
- **Fog in builds**: Graphics > Fog Modes is Custom (Linear / Exp / Exp2 kept, `m_FogStripping: 1`, PR #106): on Automatic
  the fog variants were stripped, since no scene has fog on (the game turns it on from code).
- **Hull reflection** (`GoF2/Hull` = `Assets/Shaders/Hull.shader`, a copy of URP 17.7's Lit.shader with one change:
  ForwardLit's fragment is `HullForwardPass.hlsl`'s, URP's own plus the original's `BumpRimCubeShader` terms from the
  fragment source in libgof2hdaa.so: + cubemap(reflect) x clamp(smoothstep(0.6, 1, 1 - N.V), spec^2, 2 spec + rim) +
  rim x spec^2 x rimColor, spec = the specular map, `_MetallicGlossMap`'s alpha; sampled with the GPU's mip choice; x (1 -
  metallic)): the ships, stations, turrets and objects with a specular map (165 materials, `AssetImport`), not the hangar /
  bar rooms or the sky props (plain URP Lit, the original's have no cube reflection); globals `_GoF2HullReflection` (1) and
  `_GoF2HullRimColor` (3 x `DAT_00258d34[sysTex]`, the sun colour table but systems 11 / 15, `StarSystem::initLight`).
  URP Lit alone reflected ~4 % (a dielectric): the hulls had none of the original's shine. The mods' material templates
  (`ModAssetsBuilder`) use it too; a mod's hangar / bar goes back to Lit (`ModMaterials.PlainLit`). Measured: no frame-time
  difference (a 4K frame filled with hull, on, off and plain Lit within ±0.1 ms). When URP is updated, copy its Lit.shader
  again and redo the two marked lines. Anything that picks hull renderers by shader name accepts "GoF2/Hull" (the cloaks,
  the star map).
- **Sun/planets:** quads 1000 m from the camera, drawn at the far plane (`GoF2/Backdrop`; with the sky layers drawn by
  `BackdropPass` right after the skybox, LightMode `GoF2Backdrop`, so they are in the opaque texture the cloak and the shield
  bubble refract: in URP's transparent pass the cloak showed black over a planet); the sun at the texture's own brightness (additive, like the original), only its near-white core lifted into HDR (×1.6, `_CoreGlow`) under the remake's bloom; sun swells + streak near the screen centre (the supernova system, system 27 before 0x9e: the sun keeps game +x as its up and its size (also at Tadram 113 / Naneroh 109, whose sun slot 6 lies 0.02 deg off game x: a float "parallel" test gave them the camera's up, so it rolled with the view; `Backdrop.SupernovaSunRotation`), and a sn_sun_011 glow of 0.3 × the sun + the swell rolls with the camera and makes the streak; `StarSystem::render` with +0xc); planets mirrored so their lit rim faces the sun. Mission 0 (the prologue and the main menu's backdrop after `Status::resetGame`) halves the orbit planet unless a texture size rule replaces it; no camera zoom on the orbit planet in the planet ring orbits and the alien orbit; the alien orbit has its sun straight ahead and `planet_void_big` at yaw 5.2347 (60 deg to the right), unseeded size; K'ontrr (system 20) swaps 58's and 62's planet textures like the original (textures by the stations' file order, roles by the system's list).
- **No URP shadows** in space or the hangar (`OrbitBuilder.SetupLights`' sunShadows, `StationLevel.ApplyHangarLighting`; the Space Lounge keeps its room's): within URP's 50 m shadow distance the player's hull shaded itself as a photo camera came close (a player's report, 2026-10-17); the original draws none, the hangar has `HangarShipShadow`.
- **Lights:** LIGHT0 toward the sun (`clamp(15 * sunColour, 0, 2)`), LIGHT1 from the orbit planet (Unity +Z), linear fog in 6 systems. Camera: vertical FOV 1.22 rad, near 1 m, far 15 km, chase offsets (0, 600, -1338) / (0, 600, -650) game units.
- **Main menu background** (`MenuBackground`, like the original: `ModMainMenu::OnInitialize` 0x1a46dc, CutScene(2), Level type 2): `Status::resetGame` (`Session.ResetNewGame`; not while the ending is pending) and a random main-game station 0..99 (`Galaxy::getStation(nextInt(100))`; the ending keeps its station) on every menu entry, its orbit built with `OrbitBuilder` like a flight level, with the ordinary free-flight traffic (`Traffic` / `TrafficPlan` / `NpcShip` on the empty mission: patrols in the box in front of the station, raiders fighting the locals, freighters 1-4 km beside it, the static Terran battleship, jumpers, respawns) and the station / gate volumes (`OrbitBuilder.AddObstacles`) the fighters turn away from; the player exists but inactive at the origin (an untargetable dead `Target`). Camera (`MenuCamera`, CutScene::process mode 2): fixed at (rnd(20000) − 20000, 0, rnd(60000) + 40000), yaw from −π/4 by dt·5e-5 (a turn every ~126 s), fov 0.92, near 200, far 200000; the station is only in view for part of each turn. Remake: full-detail station; the stations are 1-5 km across (Tornard 57 reaches 3.4 km out in front), so a camera spot inside or against the hull backs straight out (along game +z) until it is clear by a third of the station's size, at least 800 m (for the nearest spots about half the stations; at most ~5 km out); no asteroid within 150 m of the camera, measured from its surface (the biggest reach 390 m from their centre, Void crystals 670 m; a centre test put the camera inside one); no capital ship (battleship, carrier, Vossk battleship, a fleet battle's) with the camera inside or within 60 m of its boxes: its point is rolled again (`TrafficPlan.MenuCamera`, `AtMenuCamera`; a placement put the menu camera inside the battleship's hull); no traffic music under the menu theme (`Traffic.MenuBackdrop`).
- **Docking / launch:** within 16000 units of the station (`PlayerEgo::collidesWithStation`), the HUD offers **Dock** (tap, F / Enter, or controller X). It only appears after the player has left that range once, because the undock spawn is inside it; the original requires the autopilot instead. Inside the range Dock beats a lock on the station or on a planet behind it (`FlightHud.DockBeforeLock`, #24: the action after the Dock prompt showed was a planet jump when that lock completed meanwhile, which looked like a launch). Docking loads `Station` straight away with no animation, like `MGame::dockEvent`. Launching from the station sets `Session.LaunchedFromStation`: a fixed camera 9000 units ahead watches the ship fly past for 7 s (`LevelScript`), then the chase camera eases in. The player has no control meanwhile (`ShipController.inputLocked`, weapons blocked); any key, button, stick, click or tap except Esc / Menu and the flight menus' keys (Q / E / V / K, View, D-pad left) skips it (`LevelScript::skipSequence`: the fly-in ends like its natural end, the chase camera eases in; with the launch camera option off it snaps); the menu keys do nothing until the fly-in ends (players' report: the menu opened under the hidden HUD and its pause looked like a frozen game). Same for the arrival fly-in. Meanwhile the HUD, the radar and its ship markers are hidden, only the orbit information shows (`MGame::OnRender2D` skips `Hud::draw` / `Radar::draw` while the fly-in holds LevelScript's cutscene flag; `.hud-launch`).
- The HUD has one action prompt (tap, F / Enter, controller X): Autopilot / Jump / Autopilot off (see "Navigation"), Mine / Abort / Stop mining (see "Mining"), else Dock.
- Music: the system race's space track, battle tracks by the number of hostile ships (see "NPCs and combat").
- **Lens flare** (`LensFlareView` in the flight HUD, under the HUD; `StarSystem::render2D` / `LensFlare::render2D`): 7 images of `gof2_interface.png` (1288-1290 → `GoF2Hud/flare_0..2`) on the line centre → sun at t 0.5 / 0.25 / 0.75 / 0.125 / 1/11 / -0.75 / -0.2, alpha 70 + I (40 + I), plus a full-screen glare of alpha I, tinted by the system's flare colour; I = 64 (80 Ginoya) · (1 − d / (H/2)) (`Backdrop.FlareIntensity`, also the sun swelling); shown while the sun projects in front within −W..2W / −H..2H, no occlusion. Sizes assume a 768-high canvas (64 px images). The alphas are raised to 2.2 (`GammaAlpha`): the original blends in gamma space, the linear-space UI turned the glare's 25 % white into a 54 % grey.
- **Extra sky layers** (`SkyLayers`, `GoF2/SkyLayer` far-plane shader, assets in `Resources/GoF2Backdrop/SkyLayerAssets` from **GoF2 > Build > Sky Layers**): camera-centred, world-aligned meshes in the original's order: planet ring sky (stations 120 / 126 / 130 / 132, before the sun and planets), supernova flares (system 27, mission ≠ 89 and < 158, turned by the nebula's sun-aligned R_sky so the fire streams out of the supernova, the nasty texture from 106, ×1.5 speed above 106), storms (mission ≥ 90 in system 27 or nebula 16 / 18, a new random rotation each loop), asteroid belt (systems 24-26, lit by LIGHT0, clamped to LDR). The storm / flare parts use `PartAnimation.applyMaterialChannels`: `extra` = opacity (`_Fade`), `v5_0` = UV scroll (`_UVOffset`, 100 = one texture width, assumed).
- **Wormhole** (`Wormhole`, landmark 3, `PlayerWormHole::update` / `PlayerEgo::calcCollision`, decoded this time, not in the research files): exists until the game is won; visible when coming out of the Void, at the station the Void attack (`Session.VoidInvasionStation`) or in the alien orbit, index < 43; random spot (rand(80000) − 40000, rand(40000) − 20000, rand(40000) + 40000), radius 40000, turned to the camera (+0.5 on the direction's x, a slight tilt); its two layers spin clockwise about the facing axis (a turn per 20 s / 10 s; the old rotation map turned it backwards). Timer: grows 3 s, open 60 s, shrinks 3 s, then gone or (alien orbit / attacked station) reopens elsewhere (alien orbit x ±(30000..90000), y 20000..60000, z −60000..−100000; else ±(20000..60000) per axis; index 29 / 41: that × 2.7 from the player); the mission lock keeps it open at index 40 (not alien) and 42 (alien). Pull (`PlayerCollision`): visible, not shrinking, within 40000: loop sound 34, the ship moves toward it by (40000 − d) / 256 units per 30 fps frame, camera hit; within 1000 = inside. The ride (`SpaceLevel`, `MGame::OnUpdate`): an active campaign mission advances first (index < 41, not 29 / 40; 40 only after Errkt's freighter went through, its hull carried into 41), entering early at 29 / 40 / 41 kills the player (remake: during a level's cutscene then, the wormhole neither pulls nor takes the player, `SpaceLevel.WormholeHeld`; at 40 until Errkt's freighter is through it doesn't pull at all, `WormholeNoPull`: Errkt's call comes 40 s in wherever the player is, the cutscene following him drew a player near the wormhole in, and its pull near the centre outruns any ship; flying in still kills), 42 in the alien orbit is the level script's; then into the alien orbit (this station remembered as `Session.VoidReturnStation`, Status+0x84) or back out to it, arrival as a stream-out with the wormhole 10000 behind the player closing after a second (`LevelScript` ctor 0x16056c). HUD: 545 "Wormhole" near the centre, icon 0x450 (`GoF2Hud/wormhole_icon`) elsewhere, never locked.
- **Alien orbit** (`Session.VoidOrbit` = station −1, the Void's home, `OrbitLayout.alienOrbit`): the Void station (`station_void`, collision 1001, plus the remake's 400 m boxes for the outer blades and lower spire the 1001 volumes miss at its held 13.6 km pose, `CollisionVolume.VoidStationExtras`, #37; battlestation after the Valkyrie add-on, collision 1003 as the original's `PlayerStation::PlayerStation` (the remake gave it 1001); none at index 43-83 / ≥ 154), sky `nebula_010` + `stars_002`, the orbit planet `planet_void_big`, Void asteroids around (−30000, 0, 30000) (Void Crystals), fog tint 0x9274d4, arrival at (0, rand(20000) − 10000, rand(50000) + 170000) facing the station; the station is lockable as 415 "Void" (distance only, no autopilot, not in the menu), no docking, the Khador Drive straight back to `Session.VoidReturnStation` unless a mission blocks jumps (`MGame::UseKhadorDrive` 0x1a9480 has no Void rule; the remake refused it through the main story until #26); traffic 2+ Void fighters (`TrafficPlan`); music 145 / 136 (`StoryAssets.voidMusic` / `voidBattle`).
- **Void invasion** (Status+0x7c / +0x80): at the attacked station (and coming out of the Void, not at index 42) 2-5 Void raiders from the wormhole and ≥ 2 freighters; every 45 s (10 s at index 41) dead Void ships come back, at the wormhole at the attacked station, around the player in the alien orbit and coming out of the Void (`Traffic.UpdateAlienAttackers`); from index 32 to 44 every 10th departure elsewhere re-rolls the attacked station (`Story.OnDepart`, a random visible system, not 10 / 15); −10 from 42 / 45. Void ships drop 1-3 t Alien Remains.
- The index-43 menu backdrop (the ending's) adds the beer / bra statics 0x37d0 / 0x37d1 (`MenuBackground.SpawnStatics`, `StoryAssets.menuStatics`). The original puts them at the origin (`Level::createScene`: the PlayerStatic position args are 0 in the machine code; `PlayerStatic::update` moves nothing), inside the station and too small to see (the beer ~11 m, the bra ~26 m, the camera km away); remake pick (`EndingDrift`): they tumble across the camera's view on a camera-relative path, 45-70 m out, 22 s per crossing, the beer from 6 s, the bra 11 s later, again and again. The only prologue sky exception is index 0 in a story level (`Level::createSpace`), already built.

## Navigation (locks, autopilot, planet jump, fast-forward)

Research: `Reference/research/autopilot_travel.md` (+ `Reference/tools/autopilot/autopilot_tables.py`). `Navigation` (on the player ship) and `NavigationView` (flight HUD).

- **Locks** (`Radar::draw`): the station and the visible jumpgate (on screen, within +-w/6 of the centre and +-w/16 of the crosshair) and the other stations' planets (+-w/32 of the crosshair; the current station's own planet never). Scanner lock time (attr 29, 8000 ms without), no -200 ms and no ring delay (unlike asteroids). Landmarks beat planets, planets beat asteroids (`Mining` asks `BlocksAsteroidLock`). HUD: bracket + name + "Tech level" + distance (`Radar::calcDistance`, the original's own "m/km") near the centre, jumpgate icon elsewhere (off screen on the radar ellipse), planet names only while in the lock box, top plate with the system's race icon.
- **Crosshair on the autopilot** (`PlayerEgo::draw` 0xae4d0, #85): not drawn while the autopilot flies (+0x158), outside the turret
  view; the remake makes it transparent (`.crosshair--autopilot`) so the throttle gauge and lock rings keep its place.
- **Menu presses** (remake, #85): `Navigation.PressesBlocked`, true while a menu, conversation or map halts the controls and for
  the frame after it closes: the boost, level out, brake override, dodge, gyro recenter and the secondary switch ignore their
  presses then (A on the pause menu's Resume, an autopilot or quick menu entry boosted the ship).
- **Autopilot** (station / jumpgate locked + action): "Target: X" + sound 28; `ShipController.autopilotTarget` steers by `moveToPosition` (world up; the model banks into the turn by its return value: the signed turn per frame averaged over 5 samples × H·750/63·1.2 × 15.14, clamped, eased by dt·H/81 into the yaw rate, `FlightModel.AutopilotBank`, taken per 33.3 ms of real time: the frame rate doesn't change it, but fast-forward does, as in the original (its update runs with dt x 5, so each sample carries five times the turn and the ship banks hard, to the limit 1.2 x a full stick's; a player's report with the original's video: the remake divided it away by the scaled dt); #45: it flew the turns flat; the rate is also the yaw rate +0x27c, which the model banks from, so handed back the ship eases out of the turn), throttle reset to 100 % once, stick ignored, throttle / boost / guns still work. Within 16000 units of the station it docks by itself. Action again (or the touch autopilot button) = "Autopilot Off" + sound 29. Reaching the gate opens the jumpgate flow (see "Star map and system travel").
- **Programmed destination** (`Session.ProgrammedStation`, from the star map; `LevelScript::setAutoPilotToProgrammedStation`): at the end of the launch / arrival camera the autopilot flies to its planet in this system (the planet lock then jumps by itself), or in another system to the gate (gate orbit) or the gate station's planet. Arriving at the destination's orbit clears it.
- **Planet jump** (planet locked + action, no confirmation): sound 5, the camera freezes and looks at the ship, straight on at 8 u/ms for 3 s, HUD hidden, then `Space` reloads in that station's orbit (`Session.StationIndex`, `PreviousStationIndex`, `ArrivedByTravel`): arrival at 4x the previous station's planet billboard (~4 km), or the hidden gate in the gate orbit, facing the station, with the travel fly-in camera.
- **Autopilot menu** (`Hud::initHudMenu(3)`): the touch autopilot button, Q or the controller's View button while nothing else flies the ship (1-8 pick an entry; with keys and mouse the entries read "1." to "8.", like the PC version's numbered menus; remake: Q / E / V / K do nothing during a level's cutscene, #46); the game pauses (`Time.timeScale` 0). Entries: "Destination: X" (programmed station), Asteroid field (the field centre; like the original the autopilot keeps flying there until switched off), "<name> Station", Jumpgate (gate orbit only). Pick = "Target: X" + sound 28 + autopilot.
- **Quick menu** (`Hud::initHudMenu(0)`, "Menu" 172): the touch quick menu button, E (the PC version's "Actions") or the controller's D-pad left; refused only while mining, so it opens on the autopilot too (`MGame::OnTouchEnd` key 4). Entries in its order: 266 **Secondary weapons** with any secondary mounted (its list `initHudMenu(1)`: "<name> (<amount>)", a pick selects it), 306 Wingmen, the cloak (the item's name), 1359 Khador Drive, and (remake) the time extender (the item's name; half-transparent while recharging), so one key reaches every device. V / K open Wingmen / Khador Drive directly.
- **Fast-forward** (hold the touch button, Tab, or controller Y) while the autopilot, an asteroid approach or an object docking's approach runs, the target is >= 20000 units (1 km) away and no hostile ship is around: `Time.timeScale` = 5 for the whole game; stops on release, arrival or when the autopilot ends.
- **Menu order** (`Hud::initHudMenu(3)`): outside the alien orbit Asteroid field, Station, Jumpgate, 573 **Waypoint** (a player route not finished: the autopilot follows the route, "Target: Waypoint"), "Destination: X"; then every named docking target (also in the alien orbit). The original's only restriction is no menu in mission type 0xb7 (the unreachable challenge).

## Star map and system travel

Research: `Reference/research/starmap_travel.md` (+ `Reference/tools/starmap/starmap_tables.py`: sun positions, gate paths, energy cells, exact system layouts). Rules in `GalaxyMap` (plain C#), the map in `StarMap` (UI, created at runtime by `Open()`), the flight side in `SystemJump` (on the player ship).

- **Star map** (`StarMap`): one fullscreen map for the station's Map button, the jumpgate and the Khador Drive. Its own 3D scene on layer 31 with its own camera (the level's cameras and lights are switched off while open, flat ambient, no fog) plus the `UI/StarMap` overlay on a higher panel sort order; the owner pauses its game.
  - Galaxy view: additive sun sprites of the visible systems (`Session.SystemVisible`, from `initiallyVisible`; hidden systems aren't drawn), faint route lines and the pulse toward the gate neighbours (not with a drive); tap selects (sound 103), the camera glides it to the centre, a second tap zooms in (2184.5 ms sine ease, 106): the current system or a gate neighbour, any visible system with a Khador Drive, else 420. Drag pans with inertia and spring limits (not while a system is being centred: `StarMap::update`'s centring overwrites the spring, so Skor Terpa past the limit can be reached; remake: the limits take in every visible sun, `StarMap.PanMin` / `PanMax`: Loma, Talidor, Ginoya, Skor Terpa, Skavac and Me'enkk lie past the original's box, and once centred the spring dragged them back off the centre, up to ~280 px, so a second tap missed them). Remake: the full-map overview, one zoom level further out fitting every visible sun (the Full map / Close up button, Q, the controller's X, the mouse wheel, a pinch; Esc leaves it first): no panning there, a tap or Enter on a system zooms back in centred on it; the zoom in / out of it takes the galaxy <-> system zoom's 2184.5 ms with its sine ease (`StarMap.OverviewMs`; it was 650 ms, over long before its Map_Zoom_In / Out sound, 2.68 / 2.32 s).
  - System view: the planets on their orbits (`java.util.Random(system * 1000)`, same layout every visit), lit from the sun's position like `StarMap::initStarSystem`'s light (`LightSetLightPosition`, a fixed-function point light without falloff: `Resources/GoF2StarMap/MapPlanet.shader`, ambient 0.2 + diffuse toward the global `_StarMapSunPos`; the remake had a directional light that lit every planet from one side), drag turns it (pitch +-45 deg), tap selects a planet (104), release turns it to the front (105), a second tap confirms: 419 on the current station, 579 / 582 / 581 on energy cells with a drive, else "Destination: X / Travel to this station?" (574 + 421).
  - Overlay: rings, names (selected orange), race and security lines, "Tech level", visited tick (`Session.VisitedStations`, set on docking), gate icon, "you are here" pulse, system block with the race logo, "Energy cells needed: N / M" (578), Key legend (400).
  - Input: touch / mouse drag and tap; keyboard arrows select, WASD move / turn, Enter zoom in / confirm, K key, Esc back; controller D-pad, left stick, A, Y key, B back.
- **Station Map button** (177; keyboard M, controller Y; refused with an overloaded hold, 204): station mode (jump mode with a drive). Picking a station leaves at once (`StarMap::depart`): the launch sequence, then the autopilot to the programmed station, or for another system with a drive the Khador charge.
- **Jumpgate** (`MGame::dockEvent`): the autopilot to the gate entering its sphere (7500, Vossk 11250) hides the HUD and pauses; with a programmed station "Destination: X / Travel to this station?" (Yes preselected), No or no destination opens the map in gate mode (only the current system and its gate neighbours, any station there). Backing out puts the ship past the gate with the controls back. Remake (#37): a destroyed ship takes no gate and its Khador charge stops (shot down entering the gate it still jumped, repaired).
- **Gate jump scene** (`MGame::startJumpScene` / `updateJumpScene`, ~6.3 s): ship at gate - (0, 0, 10000) facing game +Z at 2 u/ms, fixed look-at camera at gate + (-2000, 300, -6000) drifting (5, 2, -3) u/ms; when it passes gate z - 10000 the gate's idle `_anim_add` is swapped for the `_jump_anim_add` child (5000 ms, once) and sound 31 (Jumpgate_3b / 4c / 1b / 2b at random) plays; 1000 ms in the ship vanishes; at the end `Space` reloads in the target station's orbit. Every gate layer opens with a one-off key (t 0, the loop from 50 ms; the Midorian ring lights at the centre there): looped from 0 the ring parts jumped 17.8 m on every 2.5 s wrap; now skipped like every animation's (see "Known open items", `PartAnimation`).
- **Khador Drive** (item 85, or ships 37 / 38 / 40): the autopilot menu entry opens the map in jump mode; a jump to another system (instant jump) charges 5000 ms (sound 33, cells removed at the start, "-N t Energy Cells", the charge bar in the HUD: `Hud::draw` 0x1933f6's frame 0x53a with the striped fill 0x539 revealed from the centre outward, half-width min(1, rate · 1.05) · 194, and 318 "Drive charging" (the cloak's 317) in its lower panel; `GoF2Hud/charge_frame` / `charge_fill`, #48) once the level is 5 s old, then the `khador_jump` fx (15026, scale 2) 3000 units ahead, camera at fx + R(-2000, 300, -2000) drifting (5, 2, -5), ship hidden at 1700 ms, reload at the fx end (~4 s). Cells = gate jumps on the shortest route through visible systems, 4 without one, x2 on Extreme.
- **Arrival** from another system: the hidden gate in the gate orbit, else (0, 0, 100000), facing the station, with the 7 s fly-in camera; the orbit information (race logo, station, "<System> System", security level in its colour) shows during it. The programmed station is cleared: no autopilot leg follows a system jump.
- **Loma** (system 25, the Valkyrie add-on's black market; gates to Aquila, Union and Shima): on the map from a new game's
  start and after every load (`Status::resetGame` / `GameRecord::load` show it whenever the Valkyrie add-on is owned, and
  the remake owns both; `GalaxyMap.Visibility`, `SaveGame`), not only in the add-on campaigns. In the orbit: 6-9 pirates
  (+ the raider waves, security 0), disarmed until the toll 448 is answered (see "Radio chatter"), maximum prices at all
  three stations (`calcCargoPrices`: Status+0x78 is a placeholder Station, so every real one), no seen-price records
  (`HangarWindow::initialize`), signatures (sort 29) only rolled into Loma stock, kills count but change no standing
  (`Player::damage`), no ticker, no docking fine; arriving by travel 40000 further out (`Level::init`).
- **Remake-only (free play, no campaign):** the campaign takes the player out of gateless Mido; without it, a ship in a system without a jumpgate counts as having a Khador Drive (it still needs energy cells), and Var Hastra always stocks energy cells.
- **Mission maps** (`StarMapMode.Mission`, Missions window / lounge offers / Most Wanted): view only in the original (remake, players' suggestion: the Missions window's map travels, `StarMap.Open`'s allowTravel, `StationMenu.CanTravelFromMap` / `TravelFromMap`: confirming a station is the Map button's departure with the station programmed, the Khador rules with a drive; without one a target past the gate neighbours is reached along the gate route: each gate's prompt names the destination and jumps to the next system's gate orbit (`SystemJump.NextHop`; `Arrive` keeps the programmed station on a hop, so the autopilot flies on to the next gate; the original's gate jumped straight to a far programmed station, which the Distress Help used); the lounge's offer maps stay view only), centred on the target, the yellow route along the gate path (finished segments opaque, the current one grows with alpha 255·t on the 0.99 s pulse, the next when t wraps); the Wanted map starts it at the criminal's last-seen system (`setStart`). A gateless start system (Mido) has no route, like the original.
- **Reveal** (the lounge's bought coordinates, `revealSystem`): the camera on the new system, hidden (no label, lines or input) for 4000 ms while its sun grows (remake: to the normal size), then selected; view only.
- **Volatile goods** (`GalaxyMap.HasVolatileGoods`: 209 / 204 in the hold): the Khador menu entry and a map target outside the gate routes give 612; a gate neighbour picked on the Khador map goes by the jumpgate. Mission blocks: 525 at the gate and on the Khador entry.
- **Sounds** (the FEV's LGCY data): 31 Jumpgate = one of Jumpgate_3b / 4c / 1b / 2b at random, 32 KhadorDrive = Jumpgate_5c. 102 Map_Whoosh is a drag odometer (`StarMap.AddWhoosh`, StarMap+0x1c0: + min(|dx + dy|, 10) per galaxy drag move, + min(|yaw + pitch velocity| · 0.01, 10) per frame in the system view, 0 past 100): entering the parameter regions 0 / 25 / 49.8 / 75 plays Map_Click_01 (event volume 0.439); a touch-down restarts it, the back button stops it.

## NPCs and combat

Research: `Reference/research/npc_traffic_ai.md` (+ `Reference/tools/npc/`: `npc_tables.py orbit <station> <level>` simulates an orbit's traffic) and `ship_combat.md` (+ `Reference/tools/combat/combat_tables.py`). Plain C#: `Hitpoints` (pools), `Standing`, `NpcTables`, `Route`, `TrafficPlan`; MonoBehaviours: `Traffic` (level), `NpcShip` (one ship), `PlayerHealth` + `CombatRadar` (player), `Crate`, `Explosion`; HUD `CombatView`. `Target` is the shared hittable object (asteroids, NPCs, the player), `GunRig` the shared gun visuals. Collisions: `CollisionVolume` (plain C#), `Obstacle`, `PlayerCollision` (see "Collisions" below).

- **Traffic** (`Level::createMission`, random per visit; remake #45: a capital ship's point is rolled again while its volumes would touch the station's, `TrafficPlan.ClearOfStation`: Valadon's hull had the battleship through it in 17 % of rolls): local fighters of the system race (security + rnd(2) + freighters/4), 0-1 jumpers (relaunch from the station every 10 s, fly off after 20 s), 0-4 freighters (fly game +Z at 1 u/ms, unarmed), raiders (90/65/35/10 % by security; 75 % pirates, else the system's enemy race; one model per group). Var Hastra: no freighters or jumpers. Special orbits: 102-104, Loma, systems 32/33 pirates only; 100, 101, 108, 10 empty. Every 45 s dead local fighters relaunch from the station and a destroyed raider group comes back (max 2 waves, security 0 / 1). `TrafficPlan.Build` keeps the original's order: the Mido rule (system 15 before campaign 16), 4 locals checked before the special orbits (102-104 keep escorts), 100 / 101 / 108 / 10 zeroed without an early return, the S'kolptorr and 0x2a / 0x2b rules, Loma and the loot orbits (pirates, Extreme rnd(3) + 2n; loot orbits double hull, speed 3.5, around the player), the supernova system zeroed after the local check, the pirate ambush on arrival (the raiders' point moved toward the arriving player; how far is lost in the decompile, the remake takes a quarter of the way, `TrafficPlan.AmbushShare`: half the way put up to 19 Extreme pirates on a player within a minute of arriving, a player's report 2026-10-16), the carrier box (±40000, ±20000, 100000 + rnd 80000), Specters around one shared point. In free flight Specters target only the player (`ConnectPlayers`).
- **NPC stats**: hull only, `4·campaign + 14·rank + 20` (freighters ×5, Extreme ×2), hit cube ±1000 units (±650 Extreme); one gun per fighter: 4 bullets, 16 u/ms, 3000 ms, reload `600 − 2·campaign`, damage 3..22 by rank, the race's projectile and shot sound. Engine loop per ship: sound 46 = the FEV sound definition `Engine_Enemy_01`, one of `Engine_09` / `Engine_newnew_05` / `02` / `06_mixdown` / `03` at random; freighters 47 = `Engine_Freighter_03` / `02`. `Session.Rank` comes from `Session.Xp` (`Status::getXP`: ore / 50 + kills + wingmen hired / 3 + cores + 2 · freelance missions + campaign index + visited stations).
- **AI** (`PlayerFighter::update`): patrol the box in front of the station; attack anything inside ±50 000 units (hostile ships the player first, neutral / friendly ships the race-hostile ships: pirates / Void vs everyone, Terran vs Vossk, Nivelian vs Midorian); turn toward the target at `dt·48/65536` (like a handling-100 ship, no inertia); fire inside a ±0.0076 cone within ±35 000; circle away inside ±8000; boost 5 % per 5 s or after losing 40 %; re-roll the target every 5 s. Same-race ships never target or hit each other. Remake option (Options > Gameplay "Smarter NPC targeting", `Settings.SmarterNpcTargeting`, on by default, a session rule; players' report 2026-10-17: a raider group arriving ignored the patrols beside it and all seven shredded the player, since the player is first in every enemy list): a fighter takes the nearest target in its box (`NpcShip.UpdateSmartTargeting`, rules in `NpcTargetRules`, plain C#): the player and its sentry guns while it is hostile to the player, counted at 0.7 x their distance, else the ships of a race hostile to its own (the hull's nearest point); a target that already has 2 + the difficulty rounded up attackers (Easy 2, Normal / Hard 3, Extreme 4) is passed over while another candidate in the box is free; the target is kept between the 5 s re-rolls while it can still be taken; with nothing in the box the original's rules (its route, a non-hostile ship's far race-hostile pick). Left to the original: story and freelance mission orbits, a script's only enemy (`SetOnlyEnemy`), mission ships, a turned ship (it goes for the player who shot it), the Specters, the fleet battles' fighters (their own nearest pick), wingmen, turrets, the multiplayer remote players' pick. Off = the original exactly.
- **Relations**: standing axes Terran/Vossk and Nivelian/Midorian (new game 30 / 0), hostile beyond ±70, pirates / Void always. Kills: standing −5 with the race (a pirate: +1 toward the system race), kills stat for hostile ships. Friendly fire on system-race / attack-race ships: 33 % of their hull → "Hold your fire!", 50 % → that ship turns, 66 % → the whole race turns and the station remembers it (next visit ≥ 7 hostile local fighters, "He's back!"). Radio texts show as HUD messages.
- **Damage** (`Player::damage`): shield → armor → hull, no reduction (armor is a second pool). Player: hull = `ships.json armor`, shield attr 18 (regen: full in attr 19 ms, ≥ 101 ms ticks, no delay), armor attr 20, repair bots; invulnerable during the launch / arrival camera and jump scenes; a non-hostile NPC's stray hit does 20 %; touching an asteroid destroys it and costs 20. Hit feedback: camera shake 1000 ms, sounds 25 / 23 / 24 by layer, red shield icon 500 ms, blue / red hit arcs 300 ms. Hull / shield / armor persist between levels; docking repairs (assumed) and autosaves.
- **Damage smoke** (`ShipSmoke`, `Reference/research/prologue_particles.md`): below 33 % of the hull an NPC fighter trails smoke (`sprite_smoke`, alpha) and fire (`sprite_fire`, additive), the prologue wreck's records 15 / 42; off when repaired, burning through the death tumble, off at the explosion. The player has none. **Death burn** (`ShipBurn`, `PlayerEgo::explode` / `PlayerFighter`): record 9 SET_EXPLOSION (sprite_explosion, 8/s, 700 ms, 100..1100 +500/s) from the first dying frame through the tumble, then record 11's one particle (2000..3000, 1500 ms; `emitManual` writes one) at the explosion; the player's tumble is the original's fixed +0.03 rad per 30-fps frame on each Euler axis. **Exhaust particles** (`ShipExhaust`, `Level::initParticleSystems`; the player only): one system per slot-3 anchor (scale = its `turretAngles`), particles.png cell by ship (table 0x252ba0), one per 8 units, 80 ms, ×(1 + 0.5 ramp) while boosting, grey by the cloak, off with the engine glow; each particle's backward speed (4000 k u/s) is set at emission along the nozzle and kept in the world (`IParticleSystem::emit` takes the emitter's matrix only then; #114: a local-space velocity swung the plume round with the ship, so it never bent behind it in a turn); matched by eye to a screenshot of the
  original (the Inflict on the chase camera): each particle's quad is 1.3 × its size and the exhaust has its own copy of the
  particles material at `_Glow` 7 (the shared one 2.5: the original's gamma-space additive blending saturates the plume
  to white, the linear pipeline doesn't), so the plume is the original's bright three-lobed flame and covers the engine
  glow's dashed ring (before: thin, dim plumes with the glow discs showing). Remake (#85, `ShipExhaust.PlumeSetback`): the plume starts a quarter of a particle's size
  behind the anchor: from the side or a rear corner the newest camera-facing quad lay over the engine housing (the Dark
  Angel's); half the size cleared it but uncovered the glow rings in the chase view. Each particle's cell is copied out of particles.png into its own 128 px render texture (clamped, its own mipmaps, `ShipExhaust.CellMaterial`): the glows are the atlas's top row with the trail strips right under them, and with the fx atlases' mipmaps a UV inset only held at full size, so lines showed under the plume. NPC engine trails don't exist in this build (the `Trail` class is never constructed). Remake option (Graphics, "Other ships' engines like yours", `Settings.NpcPlayerEngines`, on by default): NPC ships use the player's engine system instead, the `*_engine_glow_add` mesh and these exhaust particles (`ShipExhaust.AttachRemote`); so do the hangar flights and the bar flybys (`StationLevel.SpawnShip` attaches the particles, `scaled` so the plume shrinks with the flight's ship, following the glow; `HangarFlight.UsesPlayerEngine`; the player's own ship always: some hulls' `_engine_add` showed no flame there, the VoidX's was blue where the player's is purple); ships without a glow mesh (the battleship, static objects) keep their `_engine_add` (39 / 41 have one in the remake: the original skips their player engine child, its resource table lacks 17939 / 17941, but the OBB's meshes map onto v_ship_engine_glow.png's green Vossk glows; `patch_resources.py` registers them with material 34815, so the player flying the S'Kanarr / K'Suukk sees them too). The engine glow materials (34813 / 34815) bloom at 3.5, the other additive layers 2.5. Materials in `CombatAssets`.
- **Death**: NPC fighters tumble 1.5-3 s, explode (`Explosion`: camera-facing blast + debris, sound 18/19, camera rumble within 30 000 units) and drop a race container with their cargo (2/3 carry some; 60 s); while a fighter with cargo tumbles (`PlayerFighter::update`: KIPlayer+0x48 = cargoAvailable()) it has the crate markers (`Radar::draw`: the bracket 0x4f2 near, the diamond 0x4f1 far, the box off screen; a dying ship without cargo has none; `NpcShip.DyingWithCargo`; remake option, Options > Gameplay "Salvage markers on dying ships", `Settings.DyingCargoMarkers`, on by default: off hides the markers, the salvage lock stays) and is a salvage target: the tractor makes its crate at the ship (`TractorBeam::update`, `CreateStealCrate`) and the capture takes the whole first cargo entry with no crate left at the explosion (`KIPlayer::captureCrate`, `NpcShip.LootTaken`); a crate still being pulled when it explodes becomes its crate (the remake had no marker and no salvage for dying ships until 2026-10); freighters play their wreck animation then a ×6 explosion. The player: camera freezes, explosion at 3 s, "Game Over" at 8 s, "Tap to load last savegame." after 7 s more (remake: it stays on, the original blinks it) → the last docked state (the auto-save slot, `SaveGame`), or the main menu without one.
- **Radar** (`CombatRadar`, only with a scanner): ship lock in the crosshair box after the scanner's attr 29 (sound 26), sticky until the ship dies or another lock completes; homing missiles use it. The original takes the first ship of the list in the box, any faction, and any completed lock replaces the old one (`Radar::draw`), so a neutral crossing the box stole the lock and the missiles; remake (players' feedback; Options > Gameplay "Target lock": Smart (default) / Original, `Settings.OriginalTargetLock`): hostile ships first, then the one nearest the crosshair, and (the hold, its own option since a player asked for the two apart: Options > Gameplay "Keep enemy lock", `Settings.KeepEnemyLock`, on by default, shown only with Smart; off, a neutral ship can be locked and scanned while an enemy is locked) no neutral or friendly candidate while a hostile ship is locked and alive (rules in `TargetLockRules`, plain C#); a crate in the box beats the ship already locked and the non-hostile ones (#76: the locked enemy kept the tractor off the container it dropped), a new hostile ship still comes first. **Unlock target** (remake, the same suggestion; the original has none: only the ship's death or another lock ended one): the "Unlock target" binding (U, the controller's B: in flight B is only the menus' back, the flight's presses wait meanwhile, `Navigation.PressesBlocked`), or on touch a tap on the lock plate (pickable only while a ship is locked) drops the ship lock (`CombatRadar.Unlock`, "Target unlocked"; the missiles homing on it fly straight on, `WeaponSystem.HomingLock`), and that ship is no candidate again until it has left the crosshair box (`CombatRadar.Released`, both lock modes), so the priority doesn't take it straight back and any other ship there can be locked; the HUD's key hints show UNLOCK while a ship is locked. A ship (or salvage) in the crosshair box beats the asteroid lock: it suspends and drops it (`Navigation.ShipLockActive`, `Radar::draw` 0x1574c0), and a locked asteroid never stops a ship lock (only the approach / mining does, `isDockingToAsteroid`; the remake had it the other way round until #27). Crates: salvage lock (ring after 500 ms, tractor attr 24), the beam pulls at 10 u/ms and captures within 400 units (the first cargo entry, capped to free cargo); without a tractor beam "No tractor beam." (540; remake, #45, like the drill: once, the ring drops and that crate is ignored until it leaves the crosshair box).
- **HUD** (`CombatView`): shield and hull/armor bars top-left, the selected secondary "<name> (<amount>)" on the plate 0x4c2 at the bottom centre in every input mode (`Hud::draw` redraws it opaque while the PC version's mouse steers; hidden at 0 ammo and in the turret view; remake: no speed / boost readout; a tap / click cycles); remake (#81 / #82), the **weapon strip** (`TouchControls`, `.sec-strip` in FlightHud.uss): with two or more secondaries mounted, a row of their shop icons with the ammo (the selected one framed amber, an empty one dimmed): holding the touch secondary button 350 ms fans it out left of the button, a slide onto an icon and lifting picks it (no shot; lifting elsewhere picks nothing; the hovered name above it, a light haptic per icon), a short tap still fires; any other change of the selection (G / D-pad right, a tap on the plate, the quick menu's list) shows it for 2 s (touch: at the button; a plate tap and keys / controller: above the plate), its icons taking a tap / click meanwhile; not in VR; the touch throttle gauge 0x548 under the crosshair (2 s fade) also shows in keyboard / controller flight when the throttle moves (`PlayerEgo::draw` calls `drawThrottle` in every mode); the faint radar ellipse (image 0x4c7, the top-left quarter drawn four times mirrored around the centre, 657 x 491; remake: x900 / 1080 in the phone layout (`FlightHud.RadarScale`, the markers and hit arcs on it too), whose 900-high panel it overflowed, so the off-screen dots at the top / bottom weren't seen (2026-10-16, players' report); not while drilling: `MGame::OnRender2D` skips `Radar::draw`); HUD messages (`MiningView.ShowMessage`, `Hud::drawEventQueue` / `updateQueue`, ship_combat.md 7.7) one at a time for 4 s of game time (a queue of 20; remake: a text already showing or waiting isn't queued again; remake, a player's report 2026-10-17, `HudMessageQueue` (plain C#): the cargo scan readout (`CombatRadar.ReadCargo`), the "Target: X" notices (`Navigation`, `Mining`) and cargo gains (captures, the mining payout, gas clouds) are transient kinds (`HudMessageKind`): a new one takes the plate at once from any transient one showing and drops a waiting one of its own kind, so ship after ship scans without a queue; normal messages (warnings, mission / story texts, "Hold your fire!", hold full) still queue and are never cut short, and a transient one doesn't jump ahead of one waiting; gains within 1 s of the first (or still waiting) merge into one line, the same item summed, at most 3 parts: "8t Gold, 1t Gold Core") on the plate 0x4c3 at the top centre, y −8, or 56 while the lock plate shows (the original's 42 + the remake plate's 14 px lower position), white / red / green / orange; remake: with keys or a controller the booster's, the cloak's and the time extender's shop icons under the status bars show their recharge (`CooldownView`: a left-to-right sweep, the part not yet recharged see-through, opaque when ready, a flash then; while it runs the lit part drains with what is left, a cyan edge; touch shows them on its buttons); ship markers red / green / yellow: off screen a dot on the radar ellipse, far a dot (ring + distance when locked), near a hull bar (+ bracket when locked); remake (players' request, 2026-10-17: the far dot hid the ship), Options > Gameplay "Far ship markers" (`Settings.FarShipMarkers`, per player): Original (default) / Ring (the lock ring's arcs with the centre dot cleared, `GoF2Hud/ship_ring_open_*` from `Reference/tools/hud/make_open_rings.py`) / Small dot (the dot x0.6, 26 px above the ship), a locked far ship in either the faction bracket around it (`FarShipMarkers`, plain C#; off screen the original's dot / ring in every style), and "Distance on ship markers" (`Settings.MarkerDistance`, off by default): the distance (`Navigation.FormatDistance`) under every far on-screen marker, 16 px in the faction's colour (`.combat-label--far`), the locked one's as the original's; each label is written only when its text changes (`FarShipMarkers.DistanceKey`); a mod's `textures/<name>` replaces any of `CombatView`'s GoF2Hud images by name (`ModTextures.Hud`, read when the image is first drawn: the markers, lock ring frames, status bars, hit arcs; not the crosshair, navigation markers or touch controls); crate markers; lock plate "<race> NN%" with the race icon; hit arcs; the orange crosshair 0x4ce for 200 ms on a player bullet's hit (Level+0x30), the player turrets' hits included, aimed in the turret view or on auto fire (`Level::createGun` makes the turret guns player guns too, case 0x23; #48, then a player's report 2026-10-16).
- **Music** (`Radar::draw` 0x157c6c hostile counter, scanner only; active ships only, so a level's script-held ship doesn't count until it wakes; `Traffic.UpdateMusic`): 0 hostile ships → the calm track (`Traffic.CalmClip`: the alien orbit / a Void-attacked station 145, campaign 1 143, Kaamo 146, 101 147, the supernova system the mission target's 2241 (< 0x6a) / 2242 else 148, the deep science orbits 10 / 100 152, else the system race's), 1-2 / 3-4 / 5+ → Space_Battle_Low / Medium / Full (136 in the alien orbit, at an attacked station and at campaign 0x10; 151 for an uncovered criminal, 149 / 150 for Specters). A battle track that plays is kept until the orbit is calm (only 151 takes over). Hostiles also block fast-forward. Docked: station 10 at 0x9f plays 144.
- **Music at 102** (the dropships): the level plays 0x8c0 over its opening cutscene, then hands the music to `Traffic`
  (`Radar::draw` 0x157c6c: the calm track at the target is the 2241 loop, and the attacking Specters' 149 / 150 replace it,
  since the loop isn't in the battle set); the level kept its loop through the attacks.
- **Music in a level's cutscene** (`Traffic.UpdateMusic`): the track isn't switched while the cutscene flag holds, but its
  fade-in goes on: a level in its cutscene from the start (the Supernova's "Meanwhile..." orbits: 152 Deep Science at
  Thynome, `MGame::OnInitialize`'s `playMusicAndFadeOutCurrent(1)`) started its track at volume 0 and kept it there.
- **Music under conversations** (`Navigation.MusicPaused` / `SyncMusic`; `MGame::pauseSounds` 0x1a8304 =
  `FModSound::pauseAllPlayingSoundFXEvents`, effects category only): a conversation, hint, map or the autopilot menu pauses the
  sound effects and engines but not the music (the remake paused the whole listener); only the pause menu stops the music too
  (`MGame::OnTouchEnd` `pauseAllPlaying`). The flight music sources (`Traffic`, `CampaignLevel`) ignore the listener pause.
- **Mesh collision** (remake, 2026-10, always on; `HullCollision` + `HullBody`, Unity physics): every ship (NPC, player, other players, proxies, mods' glTF ships), station, jumpgate, turret and story / static object gets MeshColliders on its own LOD 0 meshes (not the additive / light / glow layers) on layer 24, under a kinematic Rigidbody on the model's root (no simulation is needed: the queries sync the transforms once a frame); the hull models are imported with Read/Write for it (`AssetImport.IsHullModel`: ships, stations, jumpgates, turrets, misc; a mod's glTF is readable), each mesh cooked once (`Physics.BakeMesh`). Shots (`Gun.TestHits`): a 3 m sphere cast along each shot's path this frame hits the nearest hull whose target the gun may hit (stations' hulls have no target: shots pass, as the original), so a turret in front of its hull takes the shot and gaps let shots through; scatter guns keep their proximity cube for fighters but burst on a big object's real hull (`Gun.BigTarget`: hit boxes, or a hit radius over 2000 units like the story's static objects; 102's carrier, +-6000, had a fuse cube up to 900 m across), asteroids and crates their cube; the original's +-1000-unit (50 m) cube around a fighter is gone, so near misses miss. The player's ship: a probe sphere (a third of the model's largest extent, 3..60 m) pushed out of every landmark's / freighter's / other player's hull it overlaps (`PlayerCollision.CheckHulls`, `Physics.ComputePenetration`; it only resolves while the sphere's centre is outside, which flight never crosses), sliding along it, flying through its gaps; fighters stay pass-through. An obstacle without volumes (a mod station's `"collision": "none"`) has no hull contact either. From a contributor's precise-collision version (2026-10-11, ported): the probe is also swept from last frame's place each frame (`PlayerCollision.SweepHulls`, `HullCollision.Sweep`: half the probe as a sphere, which a ship resting on a hull doesn't start inside, and the centre as a ray; the nearer stop wins, not over 300 m, a teleport), so a fast ship (boost, fast-forward) can't pass a thin part between frames (checked: 792 single-frame paths through a jumpgate's structure all stopped at the entry face, the paths through its opening untouched); a part that can't be seen (renderer off, scaled to nothing, faded out by `extra`: `HullCollision.Shown`) stops neither shots nor the ship; the main menu's backdrop builds no hulls (`HullCollision.Suppressed`); the asteroids' cube is grown by the probe; Kothar's station exploding (0x50) or damaged collides by collision 1005 (`PlayerStation::PlayerStation`; the remake used 100, `OrbitBuilder.KotharDamaged`). NPC avoidance keeps the volumes below. Measured (Editor, a fleet battle of 39 ships, A/B in one session): no frame-time difference (4.5 ms both), physics 0.06 ms fixed step + 0.08 ms sync per frame; the fleet battle spawns in 156 ms with its hulls.
- **Collisions** (`PlayerEgo::calcCollision`, ship_combat.md 2.9; the remake's mesh collision above replaces this for the player and shots): the station, the visible jumpgate and freighters are `Obstacle`s with the original's axis-aligned bounding volumes (`CollisionVolume`, never rotated). Inside one, the player is put on the nearest face / the sphere surface each frame (two passes) and keeps flying, so it slides along; camera shake, no damage. The player flies through fighters and fighters through each other; touching an asteroid destroys it and costs 20. Off during the launch / arrival camera, the jump scenes and mining; the gate is skipped while the autopilot flies into it. The autopilot to the station also docks on touching it (`MGame::dockEvent`). Volumes: stations from `collision.json` by index (no entry: Vossk 1000; 109 / 110 static 2002), centre (-a, c, b) with the station's (0, pi, 0) turn baked in, box half (|d|, |f|, |e|), sphere r |d| / 2 (alien orbit x0.9 / x0.4); the gate a sphere of its radius (7500 / 11250) with the cube +-radius as contact; freighter boxes from `Level::createShip` constants (also the bullet hit boxes); after the death animation the wreck stays put (state 4) with `wreck_collisions.json` boxes (battleship 0 x2, Midorian 1, Nivelian 2, Terran 3, Vossk 4; x1.1 / 0.6). The wreck mesh takes the hull's transform as it is (`AEGeometry::setMatrix`: same heading, the battleship's x2; checked against all five intact models). NPC fighters (§5.7): inside the first landmark's, then the first ship's volumes they turn away from the volume's centre (`dir += (away - fwd) * speed * 0.03`) with an extra step. Verified in Play mode: station 78 / 80 volumes match the models, sliding, docking by contact, a freighter carrying the player on its bow, the wreck boxes, the gate sphere, a fighter leaving the station.
- **Capital ships and pirate bases** (`Reference/research/npc_combat_specials.md`): in Terran space 30 % of orbits with freighters get the battleship (7 turrets) or, past campaign 0x67, the carrier (8); Vossk space past 0x8c the Vossk battleship (5); turrets (`NpcShip` turret mode, `TurretAim`) are 1000-HP static objects that die with their host. Remake (players' reports): a shot inside a big hull's hit boxes and a smaller target's cube hits the smaller one (`Gun.SmallerInside`): `Gun::calcCharacterCollision` takes the first target of the list and the host comes before its turrets, so the Terran battleship's turrets, all 7 inside its 11 boxes, took no hits (the hull did), and the enhanced carrier's / Vossk battleship's new boxes shielded theirs; a shot elsewhere on the hull still hits the hull. The big battle (8 %, campaign > 0x1f): 9 raiders against 9 locals. Pirate bases at stations 1 / 33 / 47 / 86 (`PirateBases`): the system has pirate escorts instead of raiders, the base orbit a sleeping outpost with 5 (10 Extreme) guards in red fog; waking a guard = radio 435-437, the outpost's death = radio 438-440, its crate, and 20000 (442) at the next docking; its station is unmanned (434, relaunch; 434 and 442 voiced by the Nivelian set, MSG_PIRATE_STATION_NO_ENTRANCE / _REWARD). Nivelian hints 443 / 444.
- **Capital ship enhancements** (remake, players' suggestion; option "Capital ship enhancements", `Settings.CapitalShips`, off by default; Options > Gameplay and the new game's Game options panel (`MainMenu.RefreshCapitalToggle`); rules in `World.CapitalShips` (plain C#), the orbit side in `World.CapitalShip` on the host, added by `Traffic.Create` for a spec built with the option on, `SpawnSpec.capitalEnhanced`): the capital ships of `TrafficPlan.AddCapitalShip` (`SpawnSpec.capital`; their turrets and escorts `capitalPart` / `capitalHost`) get escorts (3 + rank / 7, +1 on Hard / Extreme, +2 for the carrier, of the dearer half of the race's fighters, `NpcTables.StrongFighter`, hull x1.5, gun x1.3, looping round the host, `SpawnSpec.routeAround`) and turrets of (500 + 50 rank) x difficulty hull with the gun x(1 + 0.1 rank), the Vossk battleship's 5 x1.6 both (`NpcShip.SetupTurret` applies `gunFactor`). Balanced after players' feedback (2026-10-11: a maxed ship killed the carrier barely losing its shield; the turrets had 4000 hull at rank 20, a third of the battleship's, and nothing in a battle aimed at them; the Vossk battleship lost every battle; the resupply was unlimited). The carrier and the Vossk battleship (the original's indestructible decor) become killable: the battleship's hull (`NpcTables.Hull(1, 14)`, 12 000 at rank 20 on Normal) x8 (96 000), the enhanced Terran battleship x5 (60 000), EMP x3 a freighter's, their collision boxes 2005 / 2006 as hit boxes (`Target.boxes`; before, only the +-1000 cube at the centre was hit), 8 s of explosions along the hull (`SpawnSpec.deathMs`) then an x14 blast, nothing left to collide with, their own turrets with them (`Traffic.DestroyCapitalTurrets`); a crate (`CapitalShips.RollLoot`, every enhanced capital ship, the Terran battleship too): two tech level 5+ items and 8-15 t of rare goods at most once an hour of play (`Session.CapitalLootReadyAt`, saved), else 4-8 t of rare goods, then energy cells and a hold of 2-3 freighter cargo rolls (a player's report 2026-10-16: within the hour the crate was empty, and the battleship had a freighter's roll); the player's kill is a delict of 20 (on top of the kill's 5), the Vossk one counts for medal 39. The player's first hit on a friendly or neutral one radios "Hold your fire!", 3 hits or 0.5 % of the hull turn the race (`Traffic.AlarmAllFriends`; a hostile one at once). The carrier and the Vossk battleship, attacked in the last 20 s (by NPCs, or by a player who provoked it), launch 5 fighters every 20 s from their pads' approach points, 15 in all: the carrier Inflicts (ship 5) from its deck (docking set 5), the Vossk battleship H'Socs (ship 9) from its four wing pads (set 8, the original's own points on that hull) (`CapitalShips.HasDeck` / `LaunchShip` / `PadSet`), hostile to the player once provoked (multiplayer: to the players who shot it, `NpcShip.aggressors`). Resupply: the carrier ("Carrier", `CapitalShips.CarrierName`; not text 1512, a medal that reads "Spediteur" / "Курьер" in the other languages) and the Vossk battleship ("Battleship", 1667) are docking targets (`ObjectDocking.Resupply`, their pads; hidden while hostile, in a battle or still coasting after one, and then ships to mark, lock and scan: `NpcShip.OffersDocking` / `CapitalShips.DeckOpen`, used by the radar, the HUD markers and Navigation; the docking type kept them off the radar for good until 2026-10-16) for a pilot the race trusts (standing 71+) in a ship of that race, or with its signature (`CapitalShips.DockRefusal`); docked, a shop window like the hangar's Shop tab (`UI.CarrierShopWindow`, built in code with `HangarWindow.uss`, linked from `FlightHud.uxml`; `FlightHud.OpenCarrierShop`, `Navigation.OpenMenu(force)`: the game paused; the root's `hud-shop` hides the HUD's markings and lays the key hints out in one row) lists Repairs (hull and armor, 10 $ a point, unlimited), 266 Secondary weapons (each mounted one of its race, up to 50 mounted), its race's gear under the hangar's type headers 265 / 266 / 267 / 269 (players' suggestion: the Vossk ship sells Vossk items, the carrier Terran ones; `CapitalShips.Store.Gear`: the Vossk battleship 5 of the 17 Vossk-made items, attr 60, which the original sells only in Vossk systems; the carrier 6 of the other items of tech level 4+, not the Khador Drive or the signatures; 1-2 of each, 10-20 rounds of a secondary, into the hold; the other race's mounted secondaries NOT STOCKED, `resupplyWrongRace`) and energy cells (the hold's room) with their shop icons and unit prices (the orbit station's x1.25, `CapitalShips.ResupplyOffers` / `Buy`), from the ship's store for the visit (`CapitalShips.Store` on `CapitalShip.store`: 10-15 energy cells, 5-10 rounds per secondary, none of a blueprint product, rows SOLD OUT / NOT STOCKED; players' feedback: unlimited Extinctors made the blueprint pointless); the details show the trade box (what the ship has left, a held buy arrow, the unit price, what the ship has), Buy all, the stats and description, or the Repair button; up / down, right / Enter / A buy, Shift + right / X buy all, Esc / B undock (leaving it undocks). Sounds: the approach's autopilot sound as with any object, 0x65 Button_to_ship on a purchase, the station hangar's ambience loop (Station_Atmo_Hangar3) under the window (`CombatAudio.shopBuy` / `hangarAtmo`, filled by `WeaponBuilder`); the button release on Undock; the deck takes the ship without a sound for now (the objects' Docking_Landing sounded like mining there, the hangar's mount clunk didn't fit either). Multiplayer proxies now take the owner's hit boxes (`NetProxy.hitVolume`: the capital ships, freighters and the battleship; before, the other players hit only their centre cube). Fixed on the way: a fixed object dropped its crate twice (at its death and the end of its dying), so a pirate outpost's loot came twice (`NpcShip.DropCrate`, one per death). Verified in Play mode (spawned through `AddCapitalShip` at Emisto): the boxes against the model, the warning and provoking, both launch cases, the carrier's and the Vossk battleship's deaths (turrets, one crate, standing, cooldown), the battleship's escorts, docking refused and allowed, the menu's purchases, the undock. Not tested in multiplayer.
- **Fleet battles** (remake, with the capital ship enhancements; `TrafficPlan.FleetBattleHere` / `AddFleetBattle`, `CapitalShip.UpdateBattle`): 3 % of the free-flight orbits in Terran and Vossk systems past step 103, not in a Secure one (`TrafficPlan.SecureSystem`, a player's report 2026-10-16) (the finished game's world too; not the special orbits, a pirate base system, the Kaamo siege or a storyline's orbit while a story runs) hold a battle instead of the single capital ship: a Terran carrier or battleship and the Vossk battleship side by side (both facing game +Z) 80 000 units apart, 110 000-150 000 in front of the station, each with its turrets and escorts and a wing of 4 + rank / 5 fighters looping round the other (`SpawnSpec.fleetBattle` / `battleFoe`). The two sail a shared course (`CapitalShip.BattleCourse`, one object for both: a point at 0.35 u/ms whose heading curves gently left and right, an S every 2 min at up to 1 deg/s, turned back past 60 000 units from where the battle began and away from the station within 100 000) in lanes 21 000 units either side of it (+-6000 weaving, +-3000 up and down), each steering toward a point 20 000 units ahead in its lane: an eased turn rate of at most 2 deg/s, banked into the turn up to 15 deg, pitching at most ~5 deg, a little faster or slower to stay abreast (players' feedback: closing in straight looked artificial); their turrets parented to them, their collision volumes turning with them (`Obstacle.rotates` / `baseRotation`: the original's volumes are never rotated), the escorts' and wings' loops moved along every 2 s (`SpawnSpec.routeAround`, `NpcShip.ShiftRoute`); once the enemy is gone the winner levels out and coasts to a stop over 12 s (`CapitalShip.Underway`: no docking until it has) (`TrafficPlan.AddCapitalShipAt` builds any capital ship at a point). The enhanced turrets measure and aim at the nearest point of a big ship's hull (`Target.NearestPoint` over its boxes, `NpcShip.TurretAimPoint`; the original aims at the centre, out of reach of a 3 km hull) and their shots fly 3600 ms; every other turret is a turret hunter (`SpawnSpec.turretHunter`: an enemy turret within its reach counts as 0.3 x its distance); the carrier and the Vossk battleship launch their fighters as defenders. The battle's fighters (escorts, wings, launched) take the nearest race-hostile ship within 60 000 units of the course (`CapitalShips.BattleLeashUnits`, `CapitalShip.BattleArea`), else fly their loops: they took the first of the enemy list, and in a Terran system the station's patrols came first, so the Vossk side's fighters flew off to the station and never hit the enemy capital ship (the Terran side won every battle there). The system race calls the battle 6 s in and the winners call its end (`Traffic.RaceRadio`, a generic face; only while they still stand, `CapitalShip.WinnerStands`: with both destroyed both called victory); battle music while both stand (`Traffic.FleetBattleRaging`); no docking at the carrier meanwhile (`CapitalShips.InBattle`). The player picks a side through the standing, or by shooting one (provoked); helping destroy one (the kill or 5 % of its hull) while the winners aren't hostile pays 30 000 + 3000 x rank (`Traffic.PayBounty`). An enhanced Terran battleship's death takes only its own turrets (the original's `DestroyTurrets` took every turret of the level). Debug page (Spawn): "Capital ship" + "Spawn capital ship" (any of the three, enhanced) and "Spawn fleet battle" (`DebugSpawner.SpawnCapital` / `SpawnFleetBattle`). Verified in Play mode: the battle built and closing in, the turrets switching to the enemy hull in reach and wearing it down, the carrier's defenders, the refused dock, the bounty; after the rebalance (rank 20, neutral standing): the course, lanes and banking, the fighters at the battle, damage both ways (the battleship's side ~120 / s, the Vossk's ~150 / s: about 8 minutes to the battleship's death). Not played through as a player; not tested in multiplayer.
- **Capital ship missiles** (remake, with the capital ship enhancements; `CapitalShip.UpdateMissiles`): every 12-15 s each capital ship launches a salvo of 6 homing missiles, 250 ms apart (the first 3-15 s in, then every 12-17 s, so two ships fire out of step), from beside its turret mounts, climbing out a little (up x0.35 + the target's direction), with the player's smoke trails (`GunRig.EnableTrails`; the original's NPC rockets have none), each race its own: the Terrans' Intelli Jet (37, fast, 18 u/ms) and the Vossk-made S'koonn (38, attr 60, 10 u/ms), at the item's speed with its damage against 100, 10 s of flight. Targets: in a fleet battle the enemy capital ship, a missile in three a hostile fighter within 40 000 units of the hull instead (`Gun.SetTarget`: its own target), else the nearest hostile ship within 40 000 units (2 km) of its hull (the player only while hostile to it). On a big ship each missile homes on its own spot (`CapitalShips.SpreadAim`, `Gun.SetAim`: 2 times in 5 one of its live turrets, else a random point in its hit boxes, the bigger likelier), so a salvo spreads over the hull and its turrets; the player's capital hull salvos (`PlayerCapitalMissiles`) spread the same. Damage: the NPC gun's (rank, difficulty) x10 on a big ship's hull, x5 on a fighter or turret, x0.2 a stray hit on a player it isn't after. A boost shakes them off like any homing missile (see "Missile warning and evasion"), and the rest of the salvo at that ship is called off. They are `NpcShip.ExtraGuns`, so multiplayer's shot mirrors show them to the other players (whose own ships they don't hit). Verified in Play mode: the salvos in a battle hitting both hulls, the trails, a hostile Vossk battleship firing at the player, a boost with three in flight (no hits).
- **Pirate events** (remake, GitHub #6; Options > Gameplay "Pirate outposts and bosses", `Settings.PirateEvents`, on by default; `TrafficPlan.AddPirateEvent`, players' suggestions): per free-flight orbit entry 15 % in Dangerous systems and 7 % in Risky ones (the system's own security level, texts 402-405; never Average or Secure), not below rank 2, before campaign 0x10, in Mido in free play, at any station a step of the three campaigns sends the player to (story.json's stations, the Void-attacked station, step 59's targets: `InStoryline`), in a pirate base system, the Kaamo siege, Loma, the empty and special orbits, systems 27 / 32 / 33: half the time a sleeping pirate outpost 110-160 km out with its guards (as a pirate base: radio 435-437 / 438-440, red fog, its crate), else a Pirate Boss (1606; hull (15 rank + 1680) x difficulty, speed 3.5, a gun by rank x2, 3-8 t of rare goods) with 2-4 escorts in front of the station, 30 km further out than the raiders, who calls 8 s in (speaker 9). Dangerous: the outpost has 6 pirate turrets (`turret_002_static`, on the hub's deck and under it, measured on the mesh; `SpawnSpec.EventTurret`, they go with it), 3 more guards and one piece of equipment in its crate by rank (`DangerousLootLow` / `High`), the boss 2 more escorts and 6-12 t Implants / Vossk Organs, and the bounty x1.5. Destroying either pays a bounty at once (`Traffic.PirateEventDone`: outpost 10 000, boss 4000, + 1500 per rank). `AddPirateEventShips` builds the ships alone (tests: spawned with `Traffic.SpawnShip`).
- **Threat tiers** (remake, players' suggestion "Better Pirates", 2026-10): every pirate event has a tier 1 Minor / 2 Low / 3 Moderate / 4 High / 5 Extremely dangerous (`PirateReports.RollTier`: Dangerous systems lean high, Risky ones low; below rank 4 no tier 5 and tier 4 a third as often; from rank 12 tier 1 half as often and tier 5 more), replacing the Dangerous / not split above (`SpawnSpec.eventTier`, `TrafficPlan.AddPirateEventShips`): outposts x0.4 .. x2 hull, guards base -3 / -1 / 0 / +3 / +6 (the dearer fighters from tier 4, `NpcTables.StrongFighter`), turrets 0 / 0 / 2 / 4 / 6, the crate 1 half the pirate base's goods, 2 those, 3 + 2-4 t rare goods, 4 a piece of equipment (by rank) + 3-6 t of the costliest goods, 5 two pieces of the best equipment + 8-12 t, and at every tier the pirates' stash, freighter cargo rolls 1 / 2 / 2 / 3 / 4 (`TrafficPlan.OutpostStashRolls`; a player's report 2026-10-16: an outpost gave 1 t of something); bosses x0.35 .. x2.4 hull, gun x1 .. x3, 0 / 1 / 2-3 / 4-5 / 6-7 escorts, 1-2 .. 10-16 t of goods (the costliest from tier 4), speed 4 at tier 5; the bounty x0.5 / 0.8 / 1 / 1.5 / 2.5 (`PirateReports.Bounty`). Loot of several kinds drops a crate per kind, thrown apart at 3x a crate's push in directions spread over a sphere (`NpcShip.DropCrate`, `Crate.Launch`; a crate holds all of one thing), and a crate of several stacks gives every one (`CombatRadar.Capture`; the capital ships' crates gave only the first).
- **Pirate activity board** (remake, same suggestion; part of the option "Pirate outposts and bosses", so a session rule; `PirateReports` plain C#, `MissionsWindow`'s third tab "Pirate activity"): the authorities report 3-5 sightings (3 + rank / 8) of an outpost or a named boss (names combined from preset epithets, first names and surnames in seven patterns, untranslated), each at a hidden station of a visible system where `TrafficPlan.PirateEventAllowed` lets an event happen (one per system), with its tier, a bounty set when reported (the event's x1.25, a contract) and 40-90 min of playing time on the board; refreshed on docking (`StationLevel`), saved (v17 `pirateSightings`). The board names the system, never the station (Show on map routes to the system; the mission map marks no planet); entering the sighting's orbit spawns it (`TrafficPlan.AddPirateEvent`, whatever the pilot's rank), destroying it pays its bounty and marks it done (`Traffic.PirateEventDone`, `PirateReports.Done`); with the board the unannounced roll drops to 5 % / 2 %, and never on top of a raider group (`AddPirateEvent`'s hasRaiders; a reported sighting still comes; same report: on Extreme the two together overwhelmed a pilot on arrival). Warnings: the station ticker reports the sightings in this system and its gate neighbours (`PirateReports.Ticker`, `StationMenu.SetupTicker`), and 5 s into an orbit of a sighting's system the authorities radio once (`Traffic.UpdatePirateEvent`, `RaceRadio`; a boss's own call waits behind it); the boss's lock plate and call carry its name. Multiplayer: one board for the session, kept by the server (`PirateReports.ServerTick` every 5 s from `NetState.Update`: 4 reports, bounties at rank 10, times on the server clock `PirateReports.Now`) and published as JSON in `NetState.pirateBoard` (mirrored into every game's `Session.PirateSightings`, `ApplyShared`, a profile load too); the orbit's authority spawns the reported event, its destruction goes to the server (`NetState.PirateSightingDoneRpc`, checked against the board and the sender's orbit), which pays the killer, or the killer's squad split evenly (every member online, `PirateBountyRpc`, a chat notice for a share). Verified in Play mode: the board, the tab, the warning and the call, a kill paying the report (single player and a hosted session: 59 400 paid by the server, the board refilled), save and load, the tiers' numbers and a tier-5 outpost's three crates flying apart. Not tested with a second player (the squad split).
- **Radio chatter** (`Traffic.Radio`, `Level::createRadioMessage`): friendly fire, alarm, "He's back!", pirate base and hint lines show in the flight HUD's radio box (the campaign radio wins) with a random face of the race (`AgentGenerator.CreatePortrait`) or the Pirate Boss (speaker 9), name 1597 + image, 2 s delay, `lines·2000 + 1500` ms; blocks fast-forward. A new line replaces the pending ones (the queue is cleared) and the face is always a male one; kinds 0 / 1 are skipped at the story target. **Alice's Void chatter** (`SetupVoidChatter`, `Level::createRadioMessage(8)`): after the Valkyrie add-on, in the alien orbit before campaign 0x93, one of eight voiced Alice / Keith talks (tables 0x2541c8 / 0x2543a0). **Loma's toll** (`SetupLoma` / `UpdateLomaToll`, radio kinds 9-0xd, 448-456): the pirates ask cargo value × 2 / 5 / 10 / 20 % (448, #P / #C) in a ChoiceWindow (`PauseMenu.Ask`, the game paused); paid = neutral pirates for the visit (`Session.LomaTollPaid`), refused or a pirate hit = hostile.
- **Docking fine** (`StationMenu.CheckDockingFine`, `ModStation::OnInitialize`): an enemy race's station asks |standing| / 100 · 2800 ± 100 (205), a station whose forces the player attacked rank · 150 + 1000 (206), ×10 Extreme; No = straight back into space, not enough credits = 203. The attack is also forgotten once the station falls off the original's 3-station stack (`Session.RecentOrbits` / `ArriveInOrbit` from `SpaceLevel`, `Status::addStationToStack`: the flag lives on the stacked Station object, a station pushed off is reloaded clean): 3 new distinct orbits later; revisits don't reorder; not saved (after a load the stack refills before anything is forgotten). The race's standing itself stays (an enemy race's ships stay hostile after a 205 fine).
- **Station dialogs and Esc / B** (`StationMenu.Back`): the original's ChoiceWindow can't be dismissed (`ModStation::OnKeyPress`
  ignores every key while one is open), so back = its No button or a message's OK and the dialog's action always runs (Esc on
  the docking fine used to close it unpaid). Remake: with hangar flights the station's conversations, fines and medals wait
  1 s after the landing (`ArrivalSettleMs`); meanwhile, until the docking checks have had their first go, the Hangar, Lounge, Map, Launch and the system menu wait too (`StationMenu.ArrivalPending`, #37 / #46: the checks don't run while the hangar window or system menu is open, so opening one first traded and saved without the fine, or sold a loaner before its conversation).
- **Signatures** (items 189-192, `Standing.SignatureRace`): the race is a friend (+100), its rival an enemy, the others neutral whatever the axes say; friendly fire on that race (33 % / 10 % Extreme of a hull) or any race (50 % / 25 %) removes it (324, delict 100). Mission bonuses use `Standing.RawToward`.
- EMP-disabled NPCs stop moving and show the lightning of records 17 / 18 (`EmpSparks`, khador_jump bolts); remake: their engines go dark meanwhile (`NpcShip.UpdateEmpEngines`: the glow / `_engine_add` parts off, so a mod ship's flames too, the scripts' `SetExhaust` kept; the other players' copies from `NetProxy.empDisabled`), the exhaust particles already stopped; near ships have an EMP bar under the hull bar while EMP < max. The player's EMP (`NpcShip.OnPlayerEmp` / `OnPlayerDisabled`): a Wanted ship attacks back (`attackWanted`); a system-race ship past a third of its EMP turns hostile; disabling one alarms its race and is a delict. NPC-vs-NPC EMP from the gun item's attr 10.
- **NPC guns** (`NpcTables.GunDamage`, `Level::assignGuns`): campaign 4 → damage 1; Wanted (freelance type 6, not friends) and the Challenge rival rank + base at speed 28; Void ×2 at 0x10, else ×0.8; 0x31-0x34 / 0x38 → 5; turrets at 0x50 ×1.7; at 0x46 the non-wingmen fire the Disruptor (183) ×2.5; Specters ×0.7; campaign 7 pirates ×0.5; hull 270 at 49-52 (`IsHull270Mission`). NPC guns start loaded; a stray hit on the player does ×0.2, ×0.75 while docked at an object (`ObjectDocking.PlayerDocked`: fighters circle at ±12000 and hold fire while the player is above them).
- **Fighter turrets** (`Traffic.AttachFighterTurret`, `Level::createFighterTurrets`): ships 45 / 51 carry `turret_002_static` (invulnerable, untargetable, gun 22 ×0.5) at (0, 172.25, −460.7) / (0, 470, −83), synced with the host (`NpcShip.SyncTurret`). Untargetable: no HUD marker and no ship lock (the remake's ship loops missed the flag: the Rhinos at 102 had a second green dot and the lock jumped between the two), no hull (`HullCollision.Detach`: radius 0 in the original, the mesh had stopped the shots at the Rhino), and it dies with its host (`DestroyAsTurret` clears `invulnerable` first: it stayed in space). Turrets, the player's auto turrets and sentry guns don't aim at a cloaked ship, a cloaked Specter included (remake: `handleTurret` reads Player+0x5e, which only the player's cloak sets, so the original's turrets kept shooting cloaked Specters; see "Specters"). NPC **sentry guns** 211-213 (`sn_sentry_gun_00N`); at 0x9e damage ×1.5 · 0.3, speed ×1.2, hull ×5.
- **Wreck explosions** (`WreckBurn`, `PlayerFixedObject::update` 0x17f6b4): a freighter or fixed object dying gets an explosion with fire streaks at once (sound 20) and record 22 SET_EXPLOSION_CARGO (23 SET_EXPLOSION_BATTLESHIP for the battleship and the Pirate Outpost) on the wreck until its animation ends; record 24 is level 80's deep science station. 140 ms into the death the wreck mesh takes its damaged material (`MaterialCreate(0x8248 + kind)` + `MeshChangeMaterial`: the charred `*_dmg` textures, 33352-33357 for the Midorian / Nivelian / Terran / Vossk freighters, the battleship and the pirate outpost; `CombatAssets.wreckIntact` / `wreckDamaged`, `ApplyWreckDamage`; a multiplayer copy's hull takes it too); the remake kept the intact textures on every wreck until 2026-10-11 (a player's report at Kaamo). The outpost wreck uses volume id 5 (`CollisionVolume.ForWreckId`); crates wait for the wreck (`Crate.DelayExpiry`).
- **Space junk** (the freelance Junk removal): `Target.RadarObjects`, radar dots and lockable after the ships; 10 % leave a container of 1-10 t Space Waste (99).
- Wanted criminals, NPC cloaking (Specters): see "Supernova add-on".
- **NPC rockets** (`NpcShip.MakeGun` / `SetSecondaryGun`, `Level::assignGuns`): rocket / missile items (sorts 4 / 5 / 40) become RocketGuns (speed 8, 10 000 ms, reload 3000; sorts 5 / 40 home on the ship's current target after 1000 ms, `RocketGun::seekEnemy`); a second gun slot toggles every 20 000 ms (`PlayerFighter+0x2e0`): the Wanted flying ships 45-48 (G'liissk x4), Harval at 157 / 158 (item 7 x3 + Shesha x4).
- **Missile warning and evasion** (remake; the original has neither): any homing missile (Missile, ClusterMissile, Thermo kinds) locked on a boosting or cloaked player loses its lock for good and flies straight on (`Gun.Update`: `Bullet.lockLost`, `Target.ShakesMissiles` = `boosting` || `cloaked`, set by `ShipController` / `PlayerCloak` for the local ship and by `NetPlayer` from the owner's boost / cloak for the other players; a lock that went away while its ship was cloaked (the radar dropping it, an NPC's `HomingTarget` going null) shakes them off too, so they never home again on the next lock or when the cloak ends), whoever fired it: NPC rockets, the capital ships' salvos, other players' missiles (their real missile in the shooter's game and its mirror in the target's); NPC ships never evade. `Gun.LockShaken` -> "Missiles evaded!" (green, at most every 1.5 s). The missiles homing on the local player (`IncomingMissiles`, reported by `Gun.Update`, so mirrors count) drive `MissileWarningView` in the flight HUD: a pulsing red banner "INCOMING MISSILE" (with the count) over the boost key and "BOOST TO EVADE" (only with a booster, `FlightModel.HasBooster`), a red diamond on each missile (off screen on the radar ellipse; not in VR), and a two-tone beep made in code, every quarter of the nearest missile's time to impact (110..650 ms); hidden in cinematics, the launch camera and death. Multiplayer: `NetPlayer` writes its boost back to exactly 0 when it ends (it stuck just above 0, so the others kept seeing a boost); `Gun.LastLock` goes with a shot (`NetShotSender`), so a capital ship's salvo is mirrored at its own target; a capital ship picks and hits the other players it is hostile to (`NpcShip.RemotePlayers` / `HostileToRemote`, `NpcShip.HitTargets`, a stray hit x0.2); an NPC's mirrored shots pass through its own side (`NetProxy.SameSide`: the battleship's missile copies "hit" its own turrets' copies at the launchers, the warning flashed for a frame). Rocket / missile / cluster missile hits emit record 11 SET_EXPLOSION_MANUALLY_BIG at the bullet (`GunRig.ShowImpact` -> `ShipBurn.ManualBurst`, the original's rocket impact; before, no rocket hit showed anything). Verified in Play mode with a hostile Vossk battleship's salvos: the banner, the markers, a boost with three in flight (all lost, "Missiles evaded!"), the bursts. Multiplayer (Editor host, Windows build client): the host's salvo locked on the client and its boost value synced; the NetProxy.SameSide fix is not yet verified in a build.
- **Cargo stealing** (`CombatRadar`, `TractorBeam::update` / `KIPlayer::captureCrate`): an EMP-disabled ship with cargo (`NpcShip.HasCargo`) in the crosshair box is a salvage candidate, not a ship lock (same ring, the beam's attr 24); locked, a container of its cargo appears at the ship (`CreateStealCrate`, createCrate(0)) and is pulled in; the capture takes rnd(amount) of the first entry (at least 1, capped to the free cargo) off the ship (`StealFrom`), `Standing::applyStealCargo` (delict 2), a friend's cargo sets `Traffic.FriendCargoStolen` (Level::stealFriendCargo, Objective 0x13); the beam lets go when the ship dies. **Tractor auto modes** (attr 23, `Radar::Radar` +0x1aa / +0x1ab): 1 (AB-3 Kingfisher) the nearest crate on screen at once, 2 (AB-4 Octopus) any crate at once, even off screen and on the autopilot; crates only, never ships. Remake, for mods: `tractorTargets` (attr 122) pulls several crates at once without the lock (see "Mods", A modder's requests).
- **Disruptor vs shields** (remake, a players' request 2026-10-16; its description says it destabilizes shields, which the
  original never made true): a player weapon's hit on a shield counts x `Gun.ShieldFactor` (items.json stat `shieldDamage`,
  attr 124, a percentage; the Disruptor Laser 183 and items based on it 200), the part the shield doesn't absorb going on
  to the armor and hull at its plain value (`Hitpoints.Damage(dmg, shieldFactor)`, `Target.Damage`, `RemoteDamage` and
  `NetPlayer.HitUpRpc` carry it, clamped 0.01..10, 1 for an NPC's shot); the NPCs' generic guns (step 0x46's Disruptors
  too) stay at 1, and the NPCs have no shield, so it shows in multiplayer between players; the item window's row "Damage to
  shields" (`ItemInfo.ItemStats`, Extra `statShieldDamage`).
- **Static-object volumes** (`Level::getBoundingVolume` in `createStaticObject`, `SpawnSpec.collisionId`, `CollisionVolume.ForStaticObject`: centre (a, c, -b), boxes x1.2, spheres x0.6, never rotated): pirate outposts 1002, Valkyrie 1003, the mining plant 2000, the plasma array stages 2001, 0x5279 2002, the secure containers 2003, the carrier 2005, the Vossk battleship 2006, the burning Luur platform collision.json 111; none for the wrecks, the junk field or cargo_001. Remake: the object being docked at doesn't block its own approach (`PlayerCollision`), so a ship starting beside it isn't pinned on a hull face.
- **Scanner cargo readout** (`CombatRadar.ReadCargo`, `Radar+0x1a5`): with a scanner of attr 31 = 1 (Ecoscan / Proscan / Ultrascan) a new ship lock within 24000 units per axis shows the ship's first cargo entry ("<n>t <item>", white; a capture is green) or "Nothing to salvage." (542) when it carries no cargo list.
- **Voices of generic speakers** (`GenericVoice`, `Globals::getDialogueSoundId`): the text -> voice table 0x255210 (`Resources/GoF2Data/voice_table.json` from `Reference/tools/dialogue/build_voice_table.py`), else the agent's GENERIC set by race / gender / face (Terran male / female, Vossk, Nivelian (Midorians of body 2), Multipod, Bobolan, Grey, pirates) and the text's line: the radio chatter (426-431, 445-447, the pirate base 435-440, the Nivelian hints 443 / 444), the freelance briefing / success / failure / return (370-389) and the wingmen's goodbye (313). The GENERIC bank is in the story assets (**GoF2 > Build > Story Assets**). A set without the recording stays silent (Vossk 313).

## Story (campaign, dialogue, radio)

Research: `Reference/research/campaign_flow.md` (the step table 0-162, completion rules, who advances), `campaign_levels_a/b/c.md` (what each story orbit spawns and scripts), `levelscript_cutscenes.md` (LevelScript cutscenes), `dialogue_cutscenes.md` (dialogue window, portraits, radio, voice), `freelance_missions.md` (bar agents, side missions; not built yet). Data: `Resources/GoF2Data/story.json` from `Reference/tools/campaign/build_story_json.py` (per index: mission type / target / reward / value / goods / visible, objective text, briefing + success pages with speaker and voice, radio lines; speakers' portrait descriptors and part offsets); `Resources/GoF2Story/StoryAssets` ("GoF2 > Build > Story Assets": voice clips English + German, portrait parts with their image heights from `Textures/_texture_manifest.json`).

- **State and rules** (`Story`, plain C#; state in `Session`, saved by `SaveGame` v2): `CampaignMission` = the index, `StoryMission` = slot 0. `IsComplete` = `Status::missionCompleted` per type (dock at target, cargo load, reach orbit 10 s, weapon + armor mounted (sort 10 = armor), lounge, counters...; level types are decided by the campaign level). `Advance` = `nextCampaignMission` (52 → 54, 128 → 130; the new step's mission + side effects, main campaign only so far). `IsLevelMission` = `departStation` (which orbit is a story orbit), `BlocksDocking` / `BlocksJumps` (525 "Not possible on a mission."), planet jumps from index 10, menu unlocks (Hangar 5, Map 9, Lounge 13: OnKeyPress refuses it before 13, a player's report), checkpoint repairs on load (25 → 24, 29 → 28, 41 → 39; remake: 79 → 78, a save the old Khador advance stranded, #49). Story items are unsaleable and can't be demounted (323).
- **Start**: a new game starts the story (`StartCampaign`): main game = the prologue in flight (index 0, Phantom at Var Hastra's orbit), then the rescue (1), then docked at Var Hastra (the Phantom becomes Betty with Gunant's Drill + Telta Quickscan when docking at index 1); Valkyrie = 45 advances, Inflict at Dima; Supernova = 84 advances, Berger CrossXT at Dis. `Session.FreePlay` (remake-only, old saves) keeps the index-20 free play with the Mido help rules.
- **Docked** (`StationMenu`): while no window is open a completed mission opens its success conversation; closing it advances, then reloads the station (9, 44, 75, 76, 83), launches into a story orbit (78, 89, 99, 109, 119, 133, 144, 160) or credits the reward, and autosaves.
- **In flight** (`StorySpace` on the level, `CampaignLevel` in a story orbit): story orbits get no normal traffic; the briefing after the launch camera (restarts the mission clock), success checks from 5 s (in-space rules or the level's win objective), failure → "Mission failed!" / "Game Over" → last save, the add-on entry calls at 45 / 84. The game pauses during conversations (`Navigation.Paused`). The story icon (`Story.MapMarks`, map and radar) has the original's special cases: 116 the bars not searched yet, 120 Maissa only (not the level's orbit, Valadon), 125 the freighter stations not scanned yet, none at 52 / 128 / 130 / 148-151. The player route shows its current waypoint as a lockable "Waypoint" target (548, story icon `map_story`; 543 / 544 when reached); the campaign target's planet gets the story icon.
- **Success pause** (remake, `StorySpace.SuccessPending`): an in-flight success conversation opens 1.5 s after the mission
  completes (the original: the same frame, `MGame::successCheck`, while e.g. the mined asteroid is still bursting); meanwhile
  the mission counts as won and the player is invulnerable.
- **A step won without its conversation** (#88, `Story.ReopenUnshownSuccess` from `SpaceLevel` / `StationLevel.Awake`): a
  mission still marked won when a scene starts never had its success conversation (it runs within one scene and advances
  the step), so it is opened again and completes anew; step 72's call won, the scene changed in the 1.5 s before its
  conversation, and the save kept the step won for good (`IsComplete` skips a won mission).
- **Dialogue** (`DialogueView`, `UI/Dialogue`, a template instance in the station and flight UIs): speaker header, portrait (`Portrait`: background, parts 2-1-0-3 at native canvas size, frame; Keith mirrored), scrolling text, Back / Skip (confirm 396) / Next-Close; voice per page, auto-advance after the line when voice is on; one-page notes with OK. Menus pause it: the flight pause menu
pauses the HUD's shared voice source (it ignores the listener pause for the conversations that pause the game, so the
radio's line kept talking), the station's system menu over a conversation sets `DialogueView.Paused` (voice, typing, input
and auto-advance wait; the menu takes the keys). The Void (19) and Corny (56) talk in the **alien font** there and in the radio (`AlienText`, `StoryTable.UsesAlienFont`): the 26 magenta glyphs A..Z of `gof2_interface_iphone4.png` (y 263, cut to `GoF2Hud/alien_A..Z` by Build HUD Images; order, 2 px gaps and 12 px spaces assumed: the .aei glyph table wasn't converted) as image rows next to the hidden Label, wrapped at spaces; the radio counts 30 alien characters per line. Keyboard Enter/Space/→ next, Backspace/← back, Esc skip; controller A / B / Y, X the AUTO toggle; with a controller the buttons carry their glyphs (`InputGlyph.WithGlyphs`: the text moves into a child Label, a Button measures its own text only while it has no children; remake, nothing showed that Y skips). **AUTO** (remake, Genshin-like, the header's pill, the same setting as Options > Gameplay "Auto-play dialogue", `Settings.AutoAdvanceDialogue`, on by default): on, a voiced page turns when its line has ended + 500 ms (the original's `DialogueWindow::update`), a page without voice 1.5 s + 45 ms per character (2.5..12 s) after it has typed in; never past the last page. **Animated dialogue** (remake option, `Settings.AnimatedDialogue`, `TextReveal`; off = the plain page at once): the page types in (each letter fades in and settles from a bright tint; the rest is laid out invisibly so nothing reflows), 55 letters/s or paced to the voice line (ends at 85 % of it), pauses after , . ? ! and a hesitation after "...", "!" sentences faster; all-caps shouts (4+ letters or 2+ before "!", not EMP / HUD / AMR) 130 % bold with a short shake (`<voffset>`); `*Sigh*` actions italic and dim without the asterisks, typed slowly; names tinted: people pale gold with their titles ("Lieutenant Commander Brent Snocom"), places / factions light aqua with "Station" / "System", ships / items / story goods pink (a one-word item only when the texts never write it in lower case: not Drugs (Gold is never written in lower case, so it is tinted); names of 2+ words in any case: "Khador drive"), each race in its emblem's colour (Terran orange, Vossk green, Nivelian blue, Midorian grey, pirates bone white, Void violet; in either case except "void"), also the factions named after it ("Vossk Empire"); plurals too; inline icons (`<sprite ... tint=1>`, white with the letter's alpha so they fade in; tint=1 is needed, plain sprites ignore the text alpha): a coin before every amount ("N$", "N credits" and each language's credit word), before the first mention on the page the race emblem, the jumpgate / wormhole / blueprint / autopilot icon (text ids 547 / 545 / 271 / 571), an item's / ship's shop icon, an equipment category's icon before its word ("tractor beam", "scanner", "missiles"; "mine" only as "mines"), the ore core before "core" and the container before "container" (English; "plasma array", "core generator", "core of" get none); sources: the speakers, stations, systems, races, ships, items, in multiplayer the session's pilot names (a server `/dialog`'s %player%), and `TextReveal.LoreNames` (the English lore names only the texts use, from an audit of every dialogue text; most factions are translated in the other languages, so those only tint in English); a conversation ignores its keys, buttons and taps for its first 0.8 s (`DialogueView.InGrace`: keys still pressed from drilling or firing turned step 4's pirate warning away unread); Next or a tap first shows the whole page, pages read before show at once, a new speaker's portrait fades in, long pages scroll along, Next breathes when done. Used by every place characters talk: the dialogue window (story, freelance / agent notes), the flight radio and chatter, the lounge chat (the agent's generated name tinted too) and the ending's radio; not the hints, ticker or item texts, nor the pages that aren't a character talking (`StoryTable.IsNarration`: 16 Info, the tutorial pages; 17 Story, the narrator, types in). Checked against all 11 text tables (Hindi and Chinese came later): Russian marks actions as `<шепотом>` (treated like `*…*`), CJK pauses on 。！？、 without spaces, Arabic / Hebrew and Indic (Hindi's Devanagari) pages fade in whole (per-letter tags would split the letter joining and the conjuncts / reordered vowel signs, `TextReveal.IsShapedScript`), the Chinese credit word without a space ("20,000个信用分"), all-caps words of item / ship / station / system names ("Micro Gun MKII") and EMP / PEM / IEM / ЭМИ never shout; alien-font glyphs fade in order.
- **Radio** (`Radio` + the flight HUD's radio box): the level's lines in array order, one at a time, 2 s delay, `lines·2000 + 1500` ms, voice on appearance; blocks fast-forward. Trigger types: time, chain, ship deaths / activity / hull, route, event, station lock, armor.
- **Cutscenes** (`IntroCutscenes`, `CutsceneCamera`; `levelscript_cutscenes.md`): LevelScript look-at camera with world dolly and rumble, fades, the level's own music / loops, HUD and controls off (`SpaceLevel.Cutscene`); no locks or lock sounds then, nor in the launch / arrival fly-in (`Navigation.RadarHidden`: `MGame::OnRender2D` skips `Radar::draw`; a player's report). Index 0, the prologue: no station (`inEmptyOrbit`), asteroid belt at the origin, the sky's nebula 3 (`skybox_003`) under the orbit's stars and sky rotation, the orbit planet `planet_001_big` at half size (`StarSystem::StarSystem`: Level type 3 + mission 0); the Phantom (unkillable) meets three pirates (2 Hiro, 23 Azov, 2 Hiro, HP 150; exhausts and engine sound off until they wake; remake: no asteroid reaches within 150 m of their three spots, `IntroCutscenes.ClearPirateBubble`, so none sits inside them), the fight (the steering briefing opens when control is given), the hyperdrive failure (sounds 157 / 158 / 161), the time jump (`hyper_drive` fx, 160, music 141 = TimeShift_Start), arrival under the system's own sky (nebula 9, `Level::switchSkyboxForIntro`), asteroids gone, the orbit planet `planet_000_big` (`switchPlanetForIntro`'s x2 never shows: `StarSystem::render` resets the orbit planet's scale every frame), the tumbling wreck (156) trailing smoke and fire (`ShipSmoke`; the drift along game -z, the model alone turning by `PlayerEgo::rotate`'s stored Euler angles from (pi/4, pi/4, pi/4), `IntroCutscenes.Tumble`, `ShipController.modelHeld`), fade → index 1. Index 1, the rescue: the frozen Phantom, Gunant's salvager (ship 30) closing in, his three radio lines, fade → docked. **Skip** (the pause menu's 395 entry, `LevelScript::canSkipCutsceneNow`; no on-screen button): the original's unreachable `MGame::OnTouchEnd` branches, index 0 = 3 kills + advance to the rescue, index 1 = straight to the station. (Remake #16's "Hold to skip" plate was removed again: it brought bugs.) Portraits come from `story.json` speaker layers (resolved through the image ids; the computer speakers share `11_3_3`).
- **Campaign levels built** (the whole main story): 0 / 1 (above), 4 / 5 (sleeping pirate brought in by index 5's script), 7 (pirate trap: route, 3 sleepers at waypoint 2, Gunant always-friend), and in `MainCampaignLevels` (`campaign_levels_a.md` 3.5-3.16): 14 the EMP arrest at Kernstal (first kill → the EMP hit's sound; the line over → the cutscene with the EMP lightning on the ship (#85: the original puts the systems on the player's reference matrix, which only the cutscene's `updateReferenceMatrix` refreshes, so its chase view shows none; flashScreen(3) is no visible flash), turned, cruiser → docked at Alioth, 15), 16 the first Void contact at Alioth (freighters, Brent's Inflicts, the Void ignore the player, flee into the wormhole; a fight plays 136 Space_Combat_Void, `Radar::draw` 0x10), 21 the Hijacker at Kappa (scouts turn hostile, EMP him, killing him fails; launching needs an EMP bomb, 531), 24 Void samples at Sahi (3 t from crates → the wormhole swallows the player; the Sahi jump needs a scanner and a tractor beam, Carla's 532), 25 / 26 the Void orbit and the pursuers back at Sahi, 28 the Dima wormhole, 29 the probe (lock the Void station) + 180 s survival (HUD countdown), 36 the kill contest with Errkt (NPC vs player kills, `CampaignLevel.NpcKills / PlayerKills`), 38 the Nivelian freighters (unkillable on success; 527 on failure), 40 Errkt's freighter through the wormhole (reserve wave, hull carried; spawned as race 0 like `createShip(0, 1, 0xd)` (ship 13 is the Vossk freighter's model whatever the race, `Traffic.Create`; it showed the Terran one) and turned Vossk when it appears, the player held still during the cutscene following it, so `connectPlayers`' enemy lists leave it out of the Terran escorts', #26), 41 the escort in the Void (Errkt_CutSeq; the three Void ships put on the freighter and moved by their offsets, so they come in from its side and hit it (#46: as absolute positions they were ~70 000 units off and never fired); three LevelScript camera shots: among the Void ships at freighter + (-38000, 0, -30200) at "attacking from the sides", ahead and below at + (-3000, -2000, 12000) at "The engine is damaged!", and the crash at + (3000, 1000, 2000) dollying (1, 1, -2) u/ms; from the engine hit records 41 / 40 burn and smoke on it (`WreckBurn.VosskCargo` / `VosskFreighterSmoke`) and 0x9b Errkt_CutSeq_01, the broken ship's sound effects, plays over the music (remake: from the freighter, 3D to 40000 units; the original has no position); its engines off (`AssembledObject.SetExhaust` turns the freighters' single `_engine_add` part off); the freighter drifts, then 100 hull of its maximum, ~5 %; no friendly-fire rules on it from then on, `NpcShip.noFriendlyFire`), 42 finishing Errkt, the mother ship's explosion (remake, #84: the Khador Drive answers 525 in the alien orbit at 42, so the
way out is the wormhole and its explosion; 42's 0xa0 mission isn't a level mission there, so the original's drive skipped it; explosion meshes 14285-14287, sounds 153 / 154; remake: the wormhole closes behind the player, Errkt's freighter or wreck goes with the station) and the ride out. The level radio waits during a gate / Khador / planet jump's scene (#46: Cornelius' call over the jumpgate). Lost floats the remake picked are listed in the file header. Story orbits without a case stay empty (their radio still plays). A level script flies the player at its cutscene's speed (`playerSpeed`) only while its cutscene holds the ship (`c.Cutscene`): the mining approach and object docking set `externalControl` too, and the push (at the last cutscene's speed, a boost's if the player boosted) flew the ship through the asteroid it was landing on, e.g. after 29's probe (fixed 2026-10-11). NPC options for levels: asleep / inactive (`Wake`), always-enemy / friend, hit points, no loot, parked / moving (`SetMoving`), routes, only-enemy targets, race; hostile sleepers are hidden after the tutorial. Failures (and deaths in a story orbit) count toward `Session.FailCount`: 3 in a row of the same index → NPC guns ×0.7.
- **Ending** (`EndingCredits`, `UI/Ending`; `dialogue_cutscenes.md` 3.4): closing 43's success conversation loads the main menu scene with `Session.EndingPending` (the original's CutScene(2) is that backdrop): the backdrop fades in, 144 OutroSong, Brent's radio 2071-2074 (RADIO_43_0..3) from 4 s, then the logo (0x1b5a, `GoF2Hud/ending_logo`) rises at 30 px/s, holds 4 s and rises on with the staff credits (text 48); fade out from 130 s; at 136 s, or a tap once the last line has shown: `nextCampaignMission` (44) and the station (Keith's epilogue, then 45: +40 000, game won).
- **Map**: from index 32 the galaxy view shows the early-warning wormhole at the attacked system (moving to the attacked planet in its system view); `Story.TargetStation` points the Missions window, the map and the HUD's story icon there at index 40.
- **Valkyrie add-on (45-84)** (`campaign_levels_a.md` 3.17-3.19, `campaign_levels_b.md` parts 1-2, `campaign_flow.md` 4): the step side effects (`Story.ApplyStepEffects`, cases 0x2f-0x53): the loaner ships (48 Taret's H'Soc, 49 the stolen K'Suukk, 56 the S'Kanarr) with the own ship parked in `Session.ParkedShip` (Status+0x8c; back at 55 / 58), systems 23 / 22 / 24 revealed, the Liberator blueprint (58; the original's `BluePrint::lock` at 59 writes the unlocked flag too, so it stays unlocked), step 59's target stations (`Session.StoryTargets`, Status+0x90), Cornelius' mines in stock (67), the Void Essence (68 / 69, the core of Void Crystals; case 0x47 gives one more with the Disruptor blueprint), the Disruptor blueprint (72), the jump drive unsaleable (77), taken (78) and back with a spare (84); save v6. Station side: launching at 48 goes straight into B'akrram's orbit (`Story.LaunchStation`), menu locks (Hangar 48 / 49 / 56, remake: and whenever a loaner is flown, `Session.ParkedShip`, #46: the K'Suukk docks at 54 and the S'Kanarr at 57, and a quick tap on Hangar sold their guns or the loaner itself; Lounge 49, Map 48 / 49, at 77 the Map only in the Cronus, 326), no autosave imprisoned (77 at 101), Khador's ships at Kothar (77 the Cronus, 80-84 and after: Cronus / Typhon / Nemesis, 84 + S'kolptorr Rum), the hangar turntable swaps to the loaner. Levels in `ValkyrieLevels` (48 chauffeured on the autopilot, 49-52 the K'Suukk escape and the Vossk turning hostile, 56 the turret test, 63 / 64 / 65 / 67 the Skavac pirates, Khador's rescue and Corny's break-in, 69 / 70 Trot Lykkt with the Disruptor, 73 the EMP convoy, 78 the escape from the battlestation, 79 the misjump, 80 Alice's attack on Kothar (turrets and shield generators, the laser, the explosion variant of the station; the deep science station's thruster flames keep scrolling and its damaged flames are split per engine glow, #70: `MakeFlamePieces80` reads the mesh, so `AssetImport` keeps `v_station_deep_science_damaged_emitters_anim_add.fbx` Read/Write, the only game model that is), 81 Alice stranded in the Void); the in-flight transitions 64 -> Kothar's orbit, 73 -> docked at Kothar, 80 -> the Void (`SpaceLevel.TravelTo`). Step 59's arms convoy is normal traffic at the target stations (`TrafficPlan.AddConvoy`, scripted in `Traffic.UpdateConvoy`: within 50 000 all hostile, the freighter's death kills its turrets and ticks the station off, radio 2185-2189; a Liberator kill (`Target.lastPlayerWeapon` 179) is the 50 000 bonus of step 60). Khador Drive story cases (`Story.ForcedKhadorTarget`): 78 misjumps into the Void and advances (remake, #49: as the jump scene starts, not on picking the drive: the charge waits for a 5 s old level and no pause, and at 79 the autopilot docked at the Valkyrie and stranded the player; a station a level script hides, 78's jumping away, leaves the autopilot menu, the locks and the docking, `Navigation.StationObject`), 80 in the Void goes to Kothar; the drive asks 422 "Jump to the Void's system?" whenever its map opens outside the Void (remake, the user's request 2026-10-16: the map opens at once and its footer's Jump to the Void button, V / RB, asks 422 with No preselected, `StarMap.AskVoid`; asked first with Yes preselected, Enter / A out of habit jumped into the Void) (`MGame::UseKhadorDrive` 0x1a9480, no campaign gate; free play and multiplayer too, with a real drive: an item 85 or ships 37 / 38 / 40), checking 2 cells in, 1 out (x2 Extreme) but taking only 1 (2) going in (`startChargingJumpDrive` 0x1a9710; 580 "two cells, only one left" when that is the case, else 579), and in the Void always returns to `Session.VoidReturnStation` (remake: the story's own jumps take what cells there are; in the Void the only refusal is the mission gate, `Story.BlocksJumps`). New NPC controls: `shootingEnabled`, `frozen`, `detectRange`, `SetGun`; a level's scripted move (`scriptedSpeed`, `AEGeometry::moveForward`) never touches the ship's own speed, so a released ship flies at its own again (kept, it left Harval at ~9 u/ms in 158 and 154's fighters at 0).
- **Supernova add-on (84-162)**: see "Supernova add-on".
- **Nags** (`StorySpace.CheckReminder`, `MGame::OnUpdate`): at 0x17 / 0x18 in free flight (no level, not mining) an hour of playing time after the step began brings the passenger's nag (Tommy 533 / 534, Carla 535 / 536, voiced; the step clock restarts); at 0x18 with a tractor beam, once, Carla's 1912 (hint 0x25).
- **Gunant's drill** (#58): step 10 swaps a mounted drill for the IMT Extract 1.3 (case 10); the remake also swaps Gunant's
  Drill (90) in the hold, demounted before his conversation (the hangar opening before the docking checks is closed since
  2026-10-07, `ArrivalPending`).
- **Station-side story checks** (`StationMenu`): the launch refusals (529 / 530 at 6 / 7, 531 at 20 / 21, 326 at 77, `RefuseLaunchForStory`); the rescue (335: docked in a system without gate routes after campaign 16, no jump drive and no Khador Drive: the shuttle to Dis (70) for 25 000, `CheckRescue`); the add-on starts give their hints and medals (23 bronze, 30 gold, `Story.AddonStartHints`).

## Supernova add-on (84-162)

Research: `Reference/research/campaign_levels_b.md` (parts 2-3), `campaign_levels_c.md`, `campaign_flow.md` 5, `wingmen_wanted.md` 2.
Sounds / music / the 89 sky in `Resources/GoF2Story/SupernovaAssets` (**GoF2 > Build > Supernova Assets**; the DLC2 ids checked
against the FEV's LGCY data, see "Sound").

- **Story rules** (`Story`): step side effects 89-144 (items given / taken, systems 27-31 revealed, counters reset); empty
  missions at 45 / 84 / 128 / 130 / 162; a pending blueprint product completes 143's purchase; `RequirementRefusal` (the planet
  jump / launch checks: cabins 3214, 105 gamma shield II 3217, 135 3213, 139 a Vossk ship + signature 3215, 142 3216 / 3218).
  `StorySpace`: the after-success teleports (95, 96 / 127, 100, 110, 120, 126, 134, 144, 155, 161, 162), Carla's chapter calls,
  125's decoy scan. `StationMenu`: the 116 bars (90 / 91 / 92 / 94) and the 148 brokers (55 / 66 / 9).
- **Levels** (`SupernovaLevels`, run by `CampaignLevel` after the main / Valkyrie levels; the header lists every level and the
  remake's picks): 87, 89 (the Luur supernova cutscene), 91 / 92 / 94 / 102 (evacuations by docking, shuttles and Rhinos, Specter
  waves), the "Meanwhile..." cutscenes (95, 99, 109, 119, 126, 133, 160, 161), 97, 100, 105 (the bomb), 106, 114, 120, 123, 125
  (hack the secure containers), 131, 135 (titanium into the mining plant), 137, 139 (the Vossk battleships), 142 (the plasma
  tutorial), 144 / 145 (Harval, the array destroyed), 147, 154 (Alice's betrayal, 91 s hack), 157 (the final battle), 158 (the
  Harval duel). `AsteroidCentre` moves the field for 89 / 114 / 145 / 154. Remake (a player's report): at 102 the player can
  ferry evacuees too: the original's damaged Tadram object (inactive there, docking type 0: only the Rhinos used its pads) is a
  hidden pick-up target on set 10's pads named like the station, whose own landmark it replaces (`Navigation.StationReplaced`;
  `PlayerCollision` lets the approach through the station's volumes); the cutscenes and the carrier's jump end a docking first
  (`ObjectDocking.Abort`); a shuttle finding every pad taken waits 5000 units out from the nearest one (it used to "dock" at the
  object's centre, inside the hull). 144 / 145 (a player's report): the reload into the 145 attack is a stream-out arrival (the hidden gate) only after 143 was won in space at Var Lupra (`Session.StreamOutAfterCutscene`, `Level::initStreamOutPositionAfterCutscene`), else the undock point; Harval's shot stays the camera's target, hidden, through the array's explosion (destroyed, the camera turned to the station). 154: Hans' freighter flies his route onto Valkyrie's pad (docking set 7, 10 s, then back out; the shuttles' docking with a route that runs once), not into the hull's centre. Remake pick: in 105's opening shot the player and the
  escorts fly 2.5 u/ms, just ahead of the camera's dolly (at the launch speed they slid back at it tail first).
- **89, Luur's supernova** (`StarSystem::switchSunForSupernovaIntro` and LevelScript states 1-4): the look-at helper starts 100 000 units sunward of Luur and 200 000 to its own right, drifting back so the view reaches the sun as it explodes; the sun is sn_sun_011 before the blast (system 27's sun is 0.99182, 1.37329 from 0x6a, its streak sn_sun_011); the container shoots from behind the camera toward the sun with a 500 000-long trail at half speed; no fog at 89; rumble p · (rnd(2A) − A) (`CutsceneCamera.RumbleAmplitude`: 100 · falloff / 30, then 1 / 100); the explosion = the ring mesh 0x2df1 (texture 0x2df3, from 0.68665, +4e-5 per ms) and the core mesh 0x2df2 (0x2df4) at the sun (`Backdrop.StartSupernovaExplosion`; the converted meshes are ~4 m, scaled to the plane quad's 3250 m): the ring in the sun's place (no roll), the core as the glow and the streak at 0.3 × the ring. 105's `switchSunForSupernovaExpansion` only sets the sun back to 1.37329.
- **Level details from the decompiled scripts**: 157's finale (states 5-12: the Liberator killed, Alice to the Valkyrie, the
  Valkyrie backing away with 0x8cb accelerating to 10 u/ms until z 100 000, Harval chasing at 4 then 2 u/ms firing
  (`NpcShip.scriptedFire`), the explosions 0x8c4 at +(-2000, 1000, -8000) then every 8 s, burning stage 1 after 300 ms, stage 2,
  the big blast 0x8c3, the beam 0x8c7, the supernova 0x8c8); 158's opening (Harval rides a helper from 50 000 toward the sun to
  (9000, 0, -13000) at 2.5 u/ms, shown from 25 s, exhaust off 20-34 s, state 2 at 34 s coasting to 300 units per frame, the
  player rolls dt / 2000 (axis lost, remake pick), then faces Harval); 145 (Harval's Shesha x2, the Specters fire from state 2,
  record 23 at the array); 144 (camera drift 0.28 u/ms, instant cloaks); 135 (every 75 s only the dead pirates come back, no
  cargo; 2 -> 3 on that tick); 102 (a Specter whose target died takes the first living dropship, else the player; instant cloaks);
  91 (the wreck's 0x8e9 LOOP, and with 8 aboard after 180 s 0x8ea and its `_wrecked_anim` played once, unverified); 154 (the
  fighters creep 0.5 u/ms in state 1; turret view / free look / Liberator off, the auto turret off until control returns); 89 /
  81 clear the new station's attacked-friends flag. The Coromesk (103) rule: from campaign 0x55 (or 0x87) every start there is at
  (70 000, 0, 100 000) facing the station (`LevelScript::LevelScript` 0x160380, `SpaceLevel.SpawnPlayer`).
- **Object docking** (`ObjectDocking` on the player, `SpacePoints` = `docks_hd.json` sets: type 1 approach, 2 docking point):
  NPC static objects with a `DockingType` (1 drop-off, 2 pickup, 3 hackable) are lockable "DOCK" targets (`Navigation`); the
  autopilot to the approach point, a 2000 ms ease in, a look-at camera, then transfers 1 unit / 1500 ms (ore 1000; "Loading"
  3204 / "Unloading" 3205, 3200) of passengers (cabins) or goods, or the hacking game; UNDOCK eases back out. No collision meanwhile.
  One ship per port: the player takes the nearest free approach point (`SpacePoints.Take` / `Free`, SpacePoint::take /
  giveFree; a taken one only when all are), shared with the story's shuttles (`SupernovaLevels.ShuttleDocking`; a shuttle
  used to dock onto the player's port). Docked (not hacking) the turrets work as in the original (PlayerEgo::setTurretMode
  refuses only mining, the Liberator and auto turrets): the auto turrets fire and the camera key cycles standard / each
  manual turret (`FreeLookCamera.TurretAllowed`, from the ease-in on; the approach is an ordinary autopilot leg with the
  camera key free (`MGame::switchCamera` refuses only an asteroid docking; the remake refused it from the approach on until
  2026-10); not free look; `PlayerTurret.GunsBlocked` ignores the docked ship's
  blocked guns; the chase camera is on for the view; the stick still reads under `externalControl`; the mouse aims it with mouse steering (`FlightHud.UpdateMouseSteering` leaves it on in a docked turret view) and the fire button fires it (`WeaponSystem`: the docked ship's blocked guns don't halt the turret view; the press used to be latched, a player's report at step 135)). Fast-forward works on the approach while it is 20 000+ units out (`MGame::OnTouchBegin` key 0x100 takes `PlayerEgo::isDockingToDockingPoint` like the autopilot; `ObjectDocking.ApproachDistanceUnits`), and the quick menu opens approaching and docked (key 4 refuses only mining; not easing in / out or hacking, and without the Khador Drive entry while docked).
- **Hacking** (`HackingGame` plain C#, `HackingView`): the 2x3 tile board, the left / right 2x2 blocks turn clockwise, scrambled
  2 x kind rounds and never solvable in `kind` moves (kind 1 at 91, else 4); won 1500 ms after solving. Keys A / D (Q / E, arrows),
  pad LB / RB, taps on the halves. Hidden-blueprint wrecks (`TrafficPlan` table: stations 123 / 129 / 132-134) unlock their
  blueprint (radio 3156+k, `Session.HiddenBlueprintsFound`). Each is its race's freighter (`Level::createMission`:
  createShip(DAT_00253754[k], ship 13 / 15), `PlayerFixedObject::setDeadButSelectable`): the race's freighter wreck held at its
  end pose (`SpawnSpec.deadButSelectable`, `NpcShip.ShowWreckAtEnd`, invulnerable, `ModelPath` "wreck:N" for the proxies),
  docking points sets 14 / 11 / 12 / 13 / 12 (DAT_002537a4: Vossk 132, Midorian 133, Nivelian 134 / 123, Terran 129).
- **Gas clouds** (`GasCloudField`; needs a spectral filter, sort 33, not in the Void; #77, `Radar::draw` 0x1570ec: with the filter's attr 57 (ST-X, Omega) every live cloud has the white diamond 0x4f1 and is a lock target (`CombatRadar.LockedCloud`: in the crosshair box with no ship or crate there, the ring from 500 ms, locked at the scanner's lock time - 200 ms with sound 0x1a, held only while in the box; the plate 3236 "Gas cloud", the distance under its marker; a display aid only, `Radar::getLockedGasCloud` has no caller), with attr 58 (Omega) also 0x1f62 (`GoF2Hud/cloud_off`) on the radar ellipse off screen; the SA-1 has neither): clouds by `Galaxy::getPlasmaProbabilities`
  (items 201-204); an ionizing blast (`Gun.Detonated`) bursts clouds in reach into sparks; the plasma collector turret (sort 35,
  `PlayerTurret` collector mode, meshes 198-200) pulls sparks in the turret view (the turret view shows the gun's `sn_plasma_stream_anim_add` cone, its texture scrolling inward, `PlayerTurret.stream`: until 2026-10-11 the lookup hid the stream and both its mesh parts and switched only the last part back on, under its hidden parent, so no vacuum swirl showed; attr 49 speed, 51 range; a spark in its sight takes "toward the turret" as its own direction and keeps it after, #79; the crosshair blue 0x1f5e, red 0x1f5d only while a live spark is in range, #78) and collects within 800
  (one sound at a time, messages summed over 600 ms).
- **Most Wanted** (`WantedBoard` plain C#, `Session.Wanted`, the Missions window's Most Wanted tab; save v7): criminals activate
  and move between stations (`WantedBoard.Move` on arrivals); in their orbit the criminal and escorts (`TrafficPlan.AddWanted`:
  hull 15 x min(rank, 20) + hp + 180, speed 4.5, its own gun x4) hide until locked; the storyline criminals surrender below 1/3
  hull at 128 / 130; a kill pays the bounty (3206). Music `wantedMusic`.
- **Specters** (race 10): `NpcCloak` (the player cloak's look, 2000 ms fades, off radar from 25 % and, remake, cloaked for everyone's targeting from there (`Target.cloaked`, the player's rule: turrets, auto turrets and sentry guns don't aim at it, fighters and wingmen chase it but hold fire, missiles lose their lock; the original only took it off the radar), still firing (`PlayerFighter::update` checks only the target's cloak; the remake held their fire until 2026-10); no loot (ships 44 / 49 lose their cargo list in `Level::createShip`, race 10 gets none, `NpcShip.RollLoot`); random cloaking 50 %
  when panicking, else 30 % every 8 s, 9-14 s), raids at 100 < campaign < 0x91, music 149 / 150; the calm supernova system has no
  traffic before 0x9e and plays `gammaRayMusic`. Static story objects: the mining plant (103), the plasma array (112, stage by
  campaign), Luur's burning / wrecked station (111).
- **Animations the original never plays**: `PlayerStation::update` skips the station's animation at 101 and in the alien orbit,
  and `PlayerFixedObject::update` never advances an idle one, so the battlestation holds a pose there: its folded load pose
  until step 78 unfolds it, then its last frame (`PartAnimation.HoldAllAtEnd`: the ctor's Transform::Update(the length),
  station 0x65 from 0x50 0x1473c2, the alien orbit after the Valkyrie add-on 0x146e90, `Level::createStaticObject(0x4220)`
  0xcdcee; levels 80 / 81 / 154 / 157, `OrbitBuilder.SpawnStation`, the debug hull: the turrets, shield generators, 157's fire
  and the 1003 volumes sit on the unfolded arms; looped, it kept "opening itself"). Sky layers follow the camera in `beginCameraRendering` (no frame of lag).
- **Ambient story radio** (`StorySpace`, `MGame::OnUpdate`): the chapter calls (Status+0x178 at 93 / 111 / 143: the level 5 s
  old and 12 s of playing time since the step began, `Session.StoryStepStart`; Carla 0xc60 + 2k, Keith 0xc61 + 2k) and Mrs
  Moonsprocket's complaint at 122-124 (no freelance mission, not mining, an hour since the step began, which it resets; 0xc5c /
  0xc5d at 122, else 0xc5e / 0xc5f, speaker 38, `MOONSPROCKET_PASSENGER_TALK_*`).
- Verified in Play mode: every level builds; 89, 91 -> 92 (load and unload the miners), 94 (the pickup, the shuttles, the
  drop-off), 102 (the Rhinos, the carrier jump), 105, 125 (hack a container), 135 (titanium into the plant), 139 (both hacks,
  the prism), 142, 145, 154 (the hack against the 91 s limit), 157 -> 158, Harval's missiles, a criminal in orbit with its
  rockets, the Most Wanted tab, the 116 and 148 bar scenes, the chapter call and the complaint. Not played through as a player.
- Object docking approach: reached within PlayerEgo+0x1d8 = 0x578 (1400) units; remake: a ship circling the point (its turning
  circle ~2500 units across) starts the ease-in after 4 s within 4000 units.
- Sky layers: looping animations skip their one-off first keys (`PartAnimation.loopStartMs`: flares 1000 ms, storms 33 ms);
  looped from 0 they flashed the whole sky white.

## Bar and freelance (agents, chat, missions, blueprints, wingmen, Status)

Research: `Reference/research/freelance_missions.md` (agents, offers, mission generation, chat texts, missions in space, delivery), `lounge_ui.md` (the HD lounge UI, portraits, lounge voices, ticker, Missions and Status windows, medals), `blueprints_mods.md` (+ `Reference/tools/blueprints/blueprint_table.py`), `wingmen_wanted.md` (+ `Reference/tools/wingmen/wingmen_wanted.py`); `Reference/tools/missions/mission_tables.py` (Python port of the generator).

- **Agents** (`Agent`, `AgentGenerator`): `Generator::createAgents` per station when it isn't among the last 3 visited, kept on `StationStock.agents` and saved: 3-5 visitors (story agents from `agents.json` once campaign > 16, generic ones: 20 % any of 8 races, offers mission 46 % / small talk / item / purchase / wingmen, one wingman offer per bar, 35 % diplomats for hostile races, 1 % a 10x mission), names from `names.json`, portraits `ImageFactory::createChar` (the generic sets draw through `Portrait.Show`). Nobody in the supernova system's lounges (`Status::inSupernovaSystem`, system 27 before campaign 0x9e: the evacuated Ginoya stations; a save's kept visitors there are cleared on docking, `Shop.EnterStation`); remake, the same stations (`StationLevel.Evacuated`) have no parked ships, hangar traffic or bar flybys either. At index 24, launching from Thynome (10) leaves index 23's AB-1 Retractor seller with nothing to sell (`ModStation::OnTouchEnd`: setEvent(1) + setOfferAccepted, 858; `StationLevel.Launch`). Missions: `createMission` (target rules, every type once before repeats, difficulty, reward `(int(d/10*5500)+1500)*(dist/1200+1)` per type + `10*level^3`, standing bonus recomputed at every chat; the taken mission pays the bonus its chat promised, whatever the standing is by then, like the original's stored Mission bonus: `FreelanceMission.promisedBonus`, set by `Freelance.Accept`, a player's report; older saves recompute).
- **Lounge** (`LoungePanel`, `UI/Station/Lounge.uss`): one visitor billboard per agent; plates over them (race until talked to, then name + role; remake: always shown) and a visitor list (remake, for keys / controller); tapping opens the chat (`LoungeChat`): the original's texts (greeting, intro, offer, reward line, question), HD answers (green Okay / red No thanks + Let me see it or Show it on the map + What's the risk?; No thanks closes; a single Okay for closing lines), deals: the original asks again in a ChoiceWindow (865 / 866-873 / 885); remake: Okay is the deal, the station dialog only asks when it warns (864 the current mission discarded, the Extreme up-front costs, the multiplayer squad; `LoungeChat.ConfirmWarning`; the dialog is opaque: the 0.93 panel let the chat text read through it), checks 337 / 338 / 203 / 785 as messages, one lounge voice greeting per chat (`LOUNGE_eng/deu` in the story assets). Diplomats rehabilitate (±35), coordinate sellers reveal the system (the map opens on it), blueprint sellers unlock (remake, free play only: the blueprints only story steps unlock, Khador Drive / Disruptor at Var Destro 105 and Gamma Shield II / Chromo Plasma at Quineros 107, are sold by a generic visitor at 1.5x the item's max price until known, `AgentGenerator.AddStoryBlueprintSellers`; remake: a seller whose blueprint is already owned, e.g. Sao Perula's Liberator after step 58, has 858 "nothing left" instead of selling it again for nothing like the original, `AgentGenerator.StoryAgent`), mod sellers mod the hull.
- **Freelance** (`Freelance`, one mission at a time, `Session.FreelanceMission`): Courier loads Secure Containers (116, unsaleable), Passenger needs cabins; docking delivers Courier / Passenger / Purchase / Stolen goods / Informer (the client's message, payout with the 1 000 001 guard, standing +5, missions completed +1, sound 36). The target orbit of a space mission is built by `FreelanceOrbit` instead of the traffic: Defense, Protection, Recovery / Salvage (EMP the Hijacker: its container drops for the tractor beam, then deliver it; the Hijacker's and the Informer's lock plate reads their name alone in red, like a Most Wanted criminal's, `Target.plateRed`, `Radar::drawCurrentLock`'s 0x64b / 0x67f), Pirate hunting, Wanted, Junk removal (121 s, HUD countdown; gone once the mission is over, #46), Escort, Intercept, Challenge (score vs the rival; an odd number of pirates, i + 3 or i + 4, so the kills never tie: a tie is the rival's); briefing, success / failure messages; remake waypoint to the enemies (lockable, its own marker on the lock plate; it blocks no other lock). A mission orbit (the level mission Status+400) refuses docking, the gate, planet jumps (the autopilot's too) and the Khador Drive with 525 until the mission is won or failed, except Courier (`Freelance.BlocksTravel`, `MGame::dockEvent` / `Radar::draw` / `MGame::UseKhadorDrive`); the story's level missions refuse the gate too. Informer orbits skip the "He's back!" alarm and the remake's pirate events (a player's report: a pirate's death there spoiled the mission; their ships, `SpawnSpec.pirateEvent` incl. `EventGuard`, never count in `Traffic.OnShipDied`) (`TrafficPlan.InformerOrbit`; a spoiled Informer gets the normal traffic). Killing the spy alarms its race like any friendly fire (the original). Options > Gameplay "Informer missions" (`Settings.InformerOriginalRule`): Remake (default) = other deaths after the spy's no longer count; Original = any other death before docking fails it (`PlayerFighter::update` 0xf1c8c checks only +0xf1). Stolen goods' markers (`Freelance.MarkedStation` / `MapStation`: the star map, Show on map, the HUD): only the system until the documents are aboard, then the client's station (the original marks the documents' station from the start). Escort attackers (case 9) are always-enemy at a random waypoint of the route, the freighters of the client's race (Terran past race 3); without it they showed as friends while killing the convoy. **Sleepers** (`NpcShip.UpdateSleep`, `PlayerFighter::update` 0xf19b8 / 0xf2750): a hostile one wakes only on the player (or the steered Liberator) within +-25 000, or within +-detectRange (default 50 000) unless the player is cloaked; other ships never wake it (the rival, the convoy and wingmen woke them early); a non-hostile one on its target within +-detectRange; fixed objects and freighters on any enemy within +-50 000 (`PlayerFixedObject::update`). Courier / Passenger cargo draws pirate escorts; the Informer orbit has its spy (1663). Story step 13 is completed by a freelance mission. EMP weapons now drain NPC EMP.
- **Stolen goods** (#113): the original marks the agent's station from the start (`Mission::getTargetStation`), and players
  went back there looking for the documents; remake: no marker until the documents are aboard, then the client's station
  (`NavigationView.IsFreelanceTarget`, `Freelance.HasDocuments`), and the target's shop gets no new set while they are
  aboard. A mission orbit's waypoint goes as soon as its mission ends however it ends (discarded, another taken, the
  squad), not only through its own dialogs (#48, `FreelanceOrbit.routeCleared`).
- **Missions window** (`MissionsWindow`): Story and Freelance side by side, Show on map (`StarMapMode.Mission`: view only, centred on the target, story / freelance icons; like the original it doesn't program the autopilot: the station's Map does), Discard (418). The story text is every step's objective (`MissionsWindow::init` 0x17a604: DAT_00258f68[index] below 0xa4), hidden and empty missions too (step 13's "find work in the Space Lounge" before the convoy); the map button only for a visible mission with a target. The pause menu's Missions page the same. Remake: a "Pirate activity" tab beside Most Wanted (see "NPCs and combat", Pirate activity board).
- **Blueprints** (`Blueprints`, the hangar's Blueprints tab 272): 25 products keyed by item (items.json ingredients), per-ingredient progress, production station (first investment; 212), 200 $ per unit shipping from another station (288; volatile 204 / 209 refused, 289), 210 / 223 need gate routes (528), Autocomplete `int(qty*maxPrice*1.25)` (210: 2 000 000 + the rest's value), a finished run to the hold (211) or waiting at the production station (210, collected on docking, 213; remake, a player's report: the row picked while an ingredient's units commit is looked up again in the rebuilt list, `HangarWindow.Select`: a finished run left the old Autocomplete row selected on the Shop tab, and A autocompleted 'blueprint -1' for 0 $ into an '#item-1' in the hold). Unlocks: lounge sellers, campaign steps 34 / 58 / 72 / 104 / 141 with pre-invested ingredients. Remake (a player's suggestion): a run finished or autocompleted at its production station whose product has a free slot (`Hangar.OffersMountAfterBuild`: no swap, no commodity, not a secondary that joined its mounted stack) asks 211 + "Mount it now?" (`HangarWindow.Finish`); Yes mounts it (208) and shows the Ship tab, No leaves it in the hold.
- **Wingmen** (`Wingmen`, `Traffic.SpawnWingmen`, `NpcShip` wingman mode): spawned next to the player in every orbit (model seeded by the name length, 600 hull, unarmed in a Challenge), formation slots, attack the first hostile ship; the flight menu's Wingmen entry (306) gives the commands 307-311 (fire at will, attack my target, secure next waypoint, laser / EMP blaster); a dead wingman leaves the contract; the 10-minute contract runs while flying, goodbye 313 at the next docking.
- **Station extras**: the news ticker on the main view (`NewsTicker`: the campaign window's story news + 2 random items, tokens, 50 px/s; a press holds it and it follows the pointer 1:1, no inertia, `NewsTicker::OnTouchBegin` / `OnTouchMove`; remake: the full width of the UI along its bottom edge, repeated so it is never empty, and it keeps scrolling under dialogs; not at 101 / 108 / Loma); the Status window (`StatusWindow`: pilot, ship, reputation bars, statistics, 45 medals by grade with hints; `Achievements` checks on docking, "New medal!" 353; with all base medals the wingman fans pay you).
- The Most Wanted board (Missions window tab): see "Supernova add-on".
- **Medals** (`StatusWindow`, `TouchButton::draw` style 4, images from **Build HUD Images**: `GoF2Hud/medal_plate_*`, `medal_00..44`, `medal_pressed`): the plate by grade (elite 36-44 their own), the 54 px symbol at (114, 41) tinted by grade (DAT_00252060 / 50), the pressed overlay on the selected medal, the name in white under it; the elite medals react at grade 0 too. Counters: 8 Personal Need = the booze tonnes gained per hangar visit (`Session.BoozeBought`, `ModStation::OnKeyPress` / `OnTouchEnd`), 9 Barkeeper = the booze types (132-153) bought / sold / from the lounge / from non-Void crates (`BoozeTypes`; its silver hint lists the missing drinks), 21 Alien Hunter = tonnes from Void crates (`AlienRemainsCollected`); save v8. `checkForNewMedal`'s strict medals (5-8, 10, 16, 18, 20, 21, 26, 27, 29, 31-34, 36) need more than the threshold; 36 counts `Ship::getMaxLoad`. 22 = no weapon or equipment from campaign 8, the loadout docked with (`Session.ArrivedWithoutGear`, taken as the station loads: stripping the ship before the check earned it, #46); the elite medals 38 / 40 / 41 / 42 / 44 (`Session.EliteFlags`, `Achievements.Elite`): the Ore Athlete streak (`Session.OreStreak`, reset on docking and a lost mining game), 40 kills without a scanner (`BlindKills`), 41 asteroids by rockets in one flight, 44 asteroids by the Liberator; save v9 (also the Loma flags).
- Type 15 Ore Mining (unreachable in the original's generator) is only rolled in multiplayer sessions (see "Multiplayer").

## Kaamo Club (station 108, Shima)

Research: `Reference/research/kaamo_club.md` (states, the siege, docking conversations, storage rules, save; corrects
`blueprints_mods.md` 3 on 457 / 3162 / 3163), `blueprints_mods.md` 2-3, `shop.md` 5. Rules in `KaamoClub` (plain C#:
`Session.KaamoState` 0-3, `KaamoItems`, `KaamoShips` = `StoredShip` index / race / mods; save v4), the orbit in
`KaamoSiege`, the storage in `Hangar` / `HangarWindow`.

- **Siege** (state 0, `Level::createMission`): 4 Pirate Outposts (`station_pirates`, name 441) at fixed points as `NpcShip`
  static objects (`SpawnSpec.fixedObject`: never move, no gun / engine / loot, a +-7500 hit cube, the `collision.json`
  1002 volumes with the static-object rule `CollisionVolume.ForStaticObject`; death: wreck animation 14246 then an x8
  explosion, the wreck stays; hull `5 (4 campaign + 15 rank + 20)`), 6 (8) pirates within +-20000, respawned every
  22.5 s while an outpost stands. At 5 s Mkkt Bkkt calls (457, voiced) and the state becomes 1; all dead = 458,
  missions completed +1. Docking, the gate and the Khador Drive give 525 while it runs. Remake: the siege keeps the
  player's freelance mission (the original overwrites it). Music 146 `HomeBase_NoCombat` in the orbit (all states).
- **Docking** (`StationMenu.CheckKaamo`): state 1 = the 18-page first visit (459-475 + 476, voiced) -> 2; state 2 = 476
  (not enough: >= 30 000 001 $ and 50 t Buskat in the hold) or 477 -> Yes: pay, (remake) the unused 6-page purchase
  talk 479-484, 485 -> owned (3), the storage cleared.
- **Storage** (owned, at 108): the Shop tab becomes 186 "Store": the storage is 108's stock (one list), free transfers,
  no prices, unsaleable goods refused (323), mounted items not listed; stored hulls with their sell value, row buttons
  332 "Use" (336 / 329 / 333: cargo and equipment move over, the old hull takes the row) and 330 "Sell" (334; X / pad X).
  One hull per type. The Midorian hangar parks the first 5 stored hulls (the original 3; its remodelled room has two more pads).
- **Stored hulls keep their equipment** (remake, players' suggestion; Options > Gameplay "Stored ships keep their equipment",
  `Settings.KaamoKeepsEquipment`, on by default; `StoredShip.equipment`, null / empty = a bare hull, older saves): a hull going
  into the club (327 Keep, a lounge seller's Keep, a mod blueprint's delivery) keeps what is mounted on it
  (`Hangar.EquipmentToStore`; the new hull starts bare) except the story's unsaleable items, which stay with the player; "Use"
  mounts the stored hull's own items (`SwitchTo`'s 'mount': the story items first, the rest by slot, overflow to the hold) and
  the old hull keeps its; off: the original (the items move over) and a stored hull's items go to the storage
  (`KaamoClub.AddToStorage`), as do a sold stored hull's. Trade-ins elsewhere hand the items over as before. The Store tab shows
  "N mounted" on the row and the items under the details (`ItemInfo.AddEquipmentLines`); checked by `SaveGame.TryParse`, fixed by
  `ModSaves`, counted in a profile's worth.
- **Buying elsewhere** while owning it: 304, then 327 with 330 Sell (trade-in) / 331 Keep (328 when the old type is
  stored, else the full price and the old hull goes to the club, the dealer row is gone). Remake (#83): with "Stored ships
  keep their equipment" on and movable equipment mounted, Keep then asks whether it moves to the new ship (Yes: mounted
  there, the rest to the hold, the original) or stays on the old hull in the club (No) (`StationMenu.AskMoveEquipment`,
  `Hangar.KeepAndBuyShip(ship, moveEquipment)`; the lounge sellers' Keep too).
- **Lounge**: agents 21-26 once campaign > 16 (mechanics = ship mods, 25 special items, 26 a ship of [55..60] the player
  neither flies nor stores, bare hull into the storage, greeting only until owned). Medal 37 counts the stored hulls;
  the map shows Shima as "Secure" after the siege and the orange house when owned.
- **Expansion at the start** (remake, GitHub #8): the original's Kaamo Club in-app purchase (texts 78 / 88 / 93; `Status::resetGame` sets state 3 while it is bought) is the new game's toggle in the Game options panel after the economy (with Hardcore and Start game) (`MainMenu.KaamoFromStart`, PlayerPrefs `newgame_kaamo`): the club owned from the start, no siege, no purchase.
- Not wired in the original and left out: advert 189, the 478 hint (no shop to jump to), radio 3162 / 3163 (voiced, never sent).

## Mining

Research: `Reference/research/mining.md` (+ `Reference/tools/mining/mining_tables.py`). `Mining` (on the player ship, set up by `SpaceLevel`) runs lock -> autopilot approach -> landing -> minigame -> payout; `MiningGame` is the minigame logic (plain C#); `MiningView` draws the lock ring, ore plate, HUD messages and the minigame in the flight HUD.

- **Asteroids** get an ore (`Galaxy::getAsteroidProbabilities`: ores near their cheapest system dominate, e.g. Mido = Pyresium, Gold) and a class from the scale (D/C/B, big ones 50 % A) in `OrbitBuilder.SpawnAsteroids`; stored on `Target` (`oreItem`, `quality`, `scale`). Naneroh (109, `Status::inSupernovaOrbit`) after step 0x59 (`Session.WorldIndex`, so the finished game's world too): every ore is Novanium (217, its core 218) on the type-3 magma asteroid (`sn_asteroid_magma` + its explosion; `OrbitBuilder.NovaniumOrbit`); the remake had left it out, so Novanium couldn't be mined. Remake: the explosion billboard (0x4213, `asteroid_explosion.png`) is shared by Explosion types 2 and 3 and its fragment cell is Void-crystal purple, so the ordinary asteroids (type 0) use a rock-coloured copy (`Resources/GoF2Combat/asteroid_explosion_rock.png` from `Reference/tools/combat/make_rock_explosion.py`, `Target.explosionTexture`); the Void asteroids keep the purple.
- **Lock:** needs a drill (category 19) mounted; the crosshair box (+-w/16) must hold the asteroid for the scanner's lock time (attr 29, 8000 ms without) - 200 ms. The action prompt then says Mine. Without a drill (`Radar::draw` 0x157b00: hudEvent 0x14, the original keeps the ring full and repeats it every frame) the remake says 541 "No drill installed." once and drops the lock; that asteroid isn't tried again until it has left the crosshair box (`Mining.refusedNoDrill`), so looking away and back starts a new lock.
- **Approach:** player steering off, full throttle, autopilot turn; last 2000 units: exhaust off, landing sound, chase camera frozen, the model pitches ~80 deg nose up; stops at `scale * 2500` units, asteroid spin off. The booster works on the way in and the landing (MGame::OnTouchBegin refuses it only while drilling: PlayerEgo::isMining is the MiningGame), and the approach flies at moveToPosition's dt x throttle x the current speed, so a boost speeds it up (`ShipController.externalControl` keeps the boost timer running, `FlightModel.TickBoost`).
- **Minigame:** layers = class (A 7 .. D 4), 6 s each inside the ring; ore rate `yield * ((layer+1)/7*2.35+0.15)` t/s; a 2.5 s off-target energy budget per session (empty = no ore). Drill (`Mining.ReadDrillInput`, the stronger wins): the touch stick or the Drill row (`GameControls.Drill`: arrows, W A S D, the left stick), squared per axis like `Hud::getAnalog`, or with mouse steering on the mouse (the PC version: `PlayerEgo::update` feeds the cursor's offset, clamped to +-0.7 of half the screen, linear; centred when the approach docks); `MiningGame.SetInput` = 3 x the shaped value (it squared again: the stick ran at raw^4). Perfect runs with IMT Extract 1.3: D 14 t, C 20, B 28, A 37 + 1 core (verified in Play mode).
- **Payout:** ore capped to free cargo (core first), "12t Pyresium" messages, the asteroid explodes (no crate); ores and cores are normal commodities in the shop.
- **Shot asteroids** (`PlayerAsteroid::update` 0xf7060, `Target.DropAsteroidCrate`): an asteroid destroyed by a hit (guns, rockets, blasts, NPC shots, ramming; not mining, whose `Target.Explode` clears the loot, nor another game's destruction in multiplayer) leaves a crate for the tractor beam: class A 4 % with 1 core (Void Crystals' Void Essence, Novanium's 218), the others 20 % with 1-3 t of the ore, so ore comes without a drill too. The model is `createCrate(1)`'s rock container 0x421e `asteroid_01_junk`, Void Crystals (2) 0x421f `asteroid_void_junk` (`Crate.look`, `CombatAssets.CrateModel`, synced by `NetCrate`; the junk's 0x4218 is look 3); race -1 (no standing or Alien Hunter rules). The remake had left the drop out until 2026-10.
- **The fire button** (`MGame::OnTouchBegin` 0x1a838c: the original's fire button is the mining button too): during the approach, the landing and the settle it turns the autopilot off (571 + 39, sound 29, no ore) and that press fires nothing (`Hud::releaseAllKeys`, `WeaponSystem.SwallowPrimaryPress`); in the minigame it stops mining with the ore so far and, still held, the guns fire at once. The primary guns are silent from the approach on (`WeaponSystem.PrimaryBlocked`), missiles still fire until the minigame (`MGame::OnTouchEnd`), which blocks everything. The touch fire button stays on screen meanwhile. Not done: the original also starts the approach with the fire button while an asteroid is locked (the remake: the action prompt).
- **Mining beam** (remake, for mods: a drill item with attribute 100 = 1; stats `miningBeam` / `miningBeamRange` 101 /
  `miningBeamLayerMs` 102 / `miningBeamLook` 103, `ItemStats`; the example is the `mining_beam` mod in the mods repo, the
  IMT Extract Beam 5.0): `Mining`'s beam mode keeps the ship in free flight (State stays Idle: no prompt, `Interact` and the
  action arrow do nothing). The lock is the usual one; holding fire on the locked asteroid claims the fire button
  (`WeaponSystem.FireClaimed`, `Mining.BeamClaimsFire`: the primaries stay silent; only while the beam cuts or could start:
  the nose on the rock, in reach, room in the hold, else the guns keep the press; a press still held when the beam stops
  is swallowed until released, so it doesn't turn into gunfire at the next rock) and cuts it within attr 101 units of its surface (default 24000,
  the beam lasers' object reach), the lock following it in a ±w/5 box while the beam holds. `MiningBeamExtraction` (plain
  C#) is the minigame's ore logic without the minigame: the layers one by one at `MiningGame.OreRate` × attr 33, attr 102 ms
  each with the rate scaled to it (a whole asteroid = a perfect minigame run × the yield); the progress stays per asteroid
  (`Mining.BeamProgress`, shown on the lock plate as a percentage); depleted: the core (class A), `Explode` with
  `MiningOut`. The beam is fixed to the ship: straight along its heading (the unbanked root, like the guns), cutting
  where that line meets the rock's visible surface (a sphere of 0.85 × the mesh's bounding radius = hit radius / 0.7 × 0.85;
  the hit radius alone hid the impact and the chunks inside the rock), the beams ending a fifth deeper; off the rock or
  beyond the reach (counted along the beam) it runs straight ahead at full length and cuts nothing.
  `MiningBeamFx`: beams from the leftmost and rightmost primary mounts (the attr-103 beam laser's projectile:
  a steady looping core without its fade + two pulses replayed with it), its impact every 300 ms, particles.png star
  sparks, every ton a chunk of the asteroid's own mesh with a glow and a sparkling trail spiralling into the ship (0.7 s +
  1 s per 900 m); the ton enters the hold as it is cut (`Mining.Launch`; the chunk is the look only, its arrival a haptic:
  delivering on arrival put chunks still flying at a scene change into the next scene's state, after a failed mission the
  reloaded save's), a class-A rock keeping one ton of room free for its core (`BeamRoom`). Extreme: half the ore while
  the rock stands, the withheld half when it is depleted (`MiningBeamExtraction` halveUnfinished, like
  `PlayerEgo::stopMining` halving an unfinished run). The mod's icon is its own: items.json `icon` (any mod
  item, or an override's for an original item, a new item without one its nearest base's; `ModContent.ItemIcon`, read by `ItemInfo.ItemIcon`, which every shop / HUD
  icon goes through; uncompressed, clamped), here Gunant's Drill's icon with the beam turned green. Sound: the beam laser's shot on ignition, the repair
  beam's hum and the drill's `DrillSound` by layer; haptics as the drill on target. Not counted: Ore Athlete (38: it
  rewards the minigame's perfect runs, which the beam has none of), the multiplayer ore split (each player keeps what their
  own beam cuts).
- **Remake-only:** Var Hastra (78) always stocks a drill (IMT Extract 1.3) while there is no campaign; releasing the stick stops the drill's player movement (the original keeps the last input). The mining plant at 103 is a docking target (see "Supernova add-on"). The drill sound (`DrillSound`, event 1's layers by drill_speed = (LAYER_SPEEDS[layer] − 5) / 33 · 3): Slow_1 always, Add_1 from 1 (pitch ×0.896 → ×1 at 2, then ×1.196), Add_2 from 2, a Switch click on entering 0 / 1 / 2, event volume 0.244; off target (`MiningGame::update`: stop(1) + play(3), back on play(1) + stop(3)) event 3 Mining_Drill_Broken is one loop (10 s wave) at 0.245 until the drill is back on target or mining ends; the landing (2) at 0.183. **Ultrascan** (84, attr 30, `Mining.ClassAMarkers`, `Radar::draw` 0x1577de): inside the field's 100 000-unit sphere (the level's field, `OrbitLayout.asteroidCentre`: also the Void's crystal field, which has no autopilot "Asteroid field" entry; the remake took the entry's position and showed no letters there), not docking / mining, every class-A asteroid on screen gets the "A" letter (0x44e frame 0) with its top-left on the asteroid.

## Station scene

`Station.unity` holds a camera, one light, post-processing, `StationLevel` and the station menu (`UI/Station`, `StationMenu`). The level builds the current station's hangar and bar at startup. Switching between them is instant, with no fades, like `ModStation`. Research: `Reference/research/station_interior.md`. All tables (camera positions, slots, ship heights, fighters per race, music) are in `StationTables`.

- **Hangar index:** station 101 uses the battlestation hangar, 100 the deep science one, every other station its system's race.
  - Rooms: the assembled `hangar_*` prefabs sit at identity; the `bar_*` prefabs at Unity yaw 180 (game identity). Verified in Unity: ships sit on the pads and visitors stand on the floor.
  - Camera conversion: a game order-2 rotation (x, y, z) becomes `Quaternion.Euler(-x, -y, z)`. This frames the ship right of centre, like the original.
- **Hangar** (the main station view):
  - The player's ship sits on a turntable at (0, Y[ship], 0), with the NPC engine meshes and the exhaust off.
  - Turning the ship: drag (1 rad per quarter of the screen height, i.e. the original's 120 px of 480, with fling), keyboard A/D, or the right stick.
  - 0..N parked ships: 70 % of the hangar's race, 30 % a random race or pirates.
  - Camera: the phone table, relative to the ship; FOV 0.8 rad vertical; slow random position drift.
  - Lighting: one fixed light from the camera side, a flat per-race ambient, and fog in Vossk hangars.
  - Depth of field (remake option, Graphics "Hangar depth of field", `Settings.HangarDepthOfField`, off by default (it was on except on phones until 2026-10); `StationLevel.SetupDepthOfField` / `UpdateDepthOfField`): URP's DoF on a volume of its own (layer 26, only in the station camera's volume mask, so the star map and the item window stay sharp; an instance profile, which `Bootstrap.ApplyPostProcessing` leaves alone), focused each frame on the player's ship: Bokeh, a 300 mm lens at f/2 (the hangar cameras keep ~130 m from the turntable: the ~60 m ship sharp, the room behind soft), Gaussian on phones (focus + 25 .. + 140 m); off in the bar, under the star map and in VR. Meanwhile the room's URP Lit materials drop their specular highlights (`CollectRoomHighlights` / `SetRoomMatte`: copies with `_SPECULARHIGHLIGHTS_OFF`, swapped in while the blur is on): the key light's glints on the glossy parts (the Terran forcefield's top beam) popped in and out as the camera drifted, and the blur spread each into a flickering blob, worst at the screen's top edge (Bokeh or Gaussian, with or without DLSS; measured with the camera stepped like its drift: 0.7-1 % of the top strip flickering, 0 without them). URP strips the depth of field shaders from a build unless a volume profile asset has it on (the code-made profile isn't seen): `Assets/Settings/HangarDepthOfFieldShaders.asset` (Bokeh on, used by nothing) keeps them; without it the builds logged "BokehDepthOfField ... has been stripped" and showed no blur.
- **Space Lounge:**
  - 3-4 generic visitors (the race and gender rules of `Generator::createAgent`) on random slots, as camera-facing billboards with a glow and a floor shadow.
  - Camera: a 3 s intro on the first visit (a tap skips it), then a slow sway. Remake: while the player talks to a visitor (`LoungePanel.OpenChat` -> `StationLevel.FocusVisitor`) the view turns sideways only (the room camera's pitch and roll kept: tilted down to them it looked odd) so they stand beside the chat window, in the middle of the room left of it (`StationLevel.ChatFrameX`, set by `LoungePanel` from the window's place), and zooms in a quarter, eased over 0.8 s (smoothstep) on top of the sway, and back when the chat closes; not in VR. The chat window (#82) is 940 wide at the bottom right (phones 900, 80 / 140 from the edges, clear of rounded corners and the hint row); it spanned the screen from the side panel's edge and covered the visitors.
  - Lighting: the system's sun, with skybox ambient.
  - Anti-aliasing (remake, `StationLevel.ApplyAntialiasing`, both rooms): URP's TAA on the station camera, SMAA when a temporal
    upscaler (DLSS / FSR 2+ / STP) is active or MSAA is on; the thin glossy parts (the Nivelian bar stools, the Midorian
    window frames) shimmered under SMAA as the camera swayed. Flight keeps SMAA.
  - The Terran bot loops; the Midorian prop replays at random.
  - Remake: the Vossk bar's bartender arms. The original ships `bar_vossk_bot_a/b/c_anim_alpha` without resource ids and
    never shows them (CutScene::process's Vossk branch restarts two slots, +0x30 every 20000 ms and +0x34 every 22000 ms,
    that only the Terran bar fills). Their tracks are two arms behind the counter: bot_b (15.5 s) the arm along the bar,
    bot_a (15 s, turning in place) and bot_c (20.5 s, down the bar and back) two animations of the arm at its end. They are
    in the bar_vossk assembly under remake mesh ids 99001-99003 (`build_assemblies.py` MODEL_OVERRIDE; per-mesh prefabs
    `Prefabs/main/bars/bar_vossk_bot_*` made by hand with the room's material 20059, as the Terran bot has its room's, the
    game prefabs' rotation signs -1 / -1 / -1; the bar_vossk entry was spliced into `assemblies.json`, whose freighter LOD
    lists were fixed by hand after the script, and only `bar_vossk.prefab` rebuilt). `VosskBarBots` (plain C#) plays the
    dead slots: bot_b every 20 s, the end arm bot_a or bot_c at random every 22 s (the other hidden), the first plays
    within 4 s; `StationLevel.UpdateVosskBots` holds them in their first pose the first time the bar shows (until then the
    models sit at the room's origin). Tests `VosskBarBotsTests`; checked in Play mode at V'ikka (all three playing, the
    swap, the rest poses).
  - Remake (`BarFlybys`): every 6-20 s a ship (30 % a small formation) flies past outside the windows. Once per visit, from the
    camera's rest pose, two tiny renders against different background colours find the see-through pixels and one with
    `Hidden/GoF2/BarDistance` the room's nearest surfaces; a path is flown only if no point of it is ever in front of the
    room from anywhere in the camera's sway (the nearest-surface limit widened over ±14 px). Vossk: its fog would hide them,
    so its flybys (layer 27, left out of the bar camera) are drawn by a render pass of its own (`BarFlybys.FlybyPass`, after
    the transparents, against the bar's depth, `unity_FogParams` set to no fog while it draws; only while a flyby is in the
    air). It was an overlay camera stacked on the bar camera (PR #109): a camera stack turns URP's temporal upscalers off
    (DLSS, FSR 3, STP), so the station dropped to the plain render scale from the first lounge visit on.
- **Menu:**
  - Layout: header; system, tech level and race; Hangar (opens the shop window, see "Shop"), Space Lounge and Map (the star map, see "Star map and system travel") buttons; **Launch**, refused while the cargo hold is overloaded (204); the original's "Depart the station?" (397) is left out (remake: it launches at once).
  - Credits and cargo (remake, players' request; `StationMenu.UpdateFunds`, `.station-funds` in StationMenu.uss): the hangar window footer's two plates ("CARGO 65 / 195 t" with text 184, red when overloaded; the credits in amber) at the top right under the bar, on the main view and in the Space Lounge (its visitor list moved down under them), read every frame and written when a value changes (lounge deals, payouts, medal rewards, fines, multiplayer trades, tolls); they fly up and away with the bar in the menu's UI animation; hidden under the hangar window (its own footer), the Missions and Status windows and the star map.
  - Input modes work like the flight HUD: touch; keyboard 1 / 2 / M / L / Esc (Launch shows its key: L / the controller's X glyph before its text); controller LB / RB / Y / X / B / Menu. The number keys are the PC version's "Menu button 1 - 9" (3356, its key table is still in the Android binary: a tap on `Globals::sub_menu_buttons`): on the main view 1 Hangar, 2 Lounge, 3 Map, 4 Missions, 5 Status; in the Space Lounge 1-9 (and the keypad's) pick its visitors in the list's order instead, numbered "1." on the rows with keys and mouse (remake, #75, `LoungePanel.NumberKey`); in the hangar window 1 / 2 / 3 = Ship / Shop (Store) / Blueprints (`HangarWindow::initialize`), 4 / 5 nothing; Q / E (LB / RB) cycle the tabs. No hint row on the main view (the buttons say it); toasts sit at the bottom, above the ticker.
  - Esc or B steps back: dialog, then lounge, then the system menu (Save game, Back to Main Menu).
  - The mouse takes the selection to the `station-button` it is over (`PointerOverEvent` on the root, like MainMenu's
    `HookFocusSound`): a menu opened with the mouse focused its first entry, which stayed lit beside the hovered one.
  - Music per race and station; ambience per screen as the FMOD cycle events (`CycleSound`): 122 Mainview, 95 Hangar while the hangar window is open (`StationLevel.SetHangarWindowOpen`, `ModStation::OnKeyPress`), 108 Lounge.
- Missions (129, unlocks with the Map) and Status (169): see "Bar and freelance".
- **System menu** (`StationMenu`, `SysPage` Main / Save / Load / Options): 30 Save game, 29 Load (the slot list), 28 New game (the main menu's campaign panel, `MainMenu.OpenPanelOnStart`), 31 Options (the `OptionControl` rows), 43 About (text 45), 522 Back to Main Menu.
- **New medals** (`ShowNextMedal`, `checkForNewMedal`): the reward 5000 / 2500 / 1000 by grade (none on Extreme); remake: instead of the original's ChoiceWindow per medal that waits for OK, a toast at the top centre (`.medal-toast` in StationMenu.uss: the plate `StatusWindow.MedalPlate`, 353, the name, the reward; border, top band and title in the grade's colour `StatusWindow.MedalTint`; drops in and grows with a short flash; the remake's own sound `Resources/GoF2Sfx/MedalToast`, synthesized by `Reference/tools/audio/medal_sound`) for 4 / 5 / 6 / 7 s (bronze / silver / gold / elite; 3 s while more medals wait, the last one its full time; the pointer resting on it pauses the countdown), then it fades and the next medal follows; it doesn't hold up the station's other checks; a tap / click opens the Status window on that medal (`OpenMedal`, `StatusWindow.SelectMedal`); medal hints 649 / 650 / 651 and the blueprint hints 232 / 3233 (`CheckMedalHints`); first-visit hints (622 hangar, 627 lounge, 635 missions, 640 phones, 628 / 631 on the map via `StarMap.ShowHint`); the hangar's selection hints 587-589.
- **Inspect ship** (remake, the side menu's "Inspect ship" under Status (hangar view only, not in VR), keys 6 / I, the controller's right stick press; `World.HangarInspect` (plain C#) + `StationLevel.BeginInspect` / `EndInspect`, input in `StationMenu.UpdateInspect`): the camera leaves its drifting hangar spot and orbits the player's ship like the flight's Action Freeze (`PhotoMode`): yaw / pitch around the hull's centre (its meshes' box in the ship's own frame, no glow layers), starting where the hangar camera is and zooming in to 2.1x the hull's radius (1.15x..4x), a 0.9 s eased blend in and back out, the pitch kept above the pad (the hull's lowest point + 1.5 m), the near plane pulled in to stay in front of the hull; drag / one finger turns (0.005 rad per 1080p pixel, the fling x0.9 a 30 fps frame), the wheel, a pinch, + / - (Page Up / Down) and the triggers zoom, the arrows / W A S D and both sticks turn. The menu flies out meanwhile (the UI animation, `UiBlocked`), a footer keeps Back and the hints row at the top right the controls; Hide UI (H, the controller's Y; a short click / tap or H / Y brings it back, a drag still turns) and Screenshot (Enter / P, the controller's A: `PhotoMode.Store`, no UI in it, 55 / 56) are clickable hints (taps on touch). Back: Esc, Backspace, B, 6 / I, the right stick press. The depth of field focuses on the hull's centre meanwhile and turns Gaussian (also in the Mod Builder's orbit): sharp to a little past the hull, the room behind blurred (300 mm at f/2 kept ~6 m sharp up close, a 50 mm lens at the orbit's ~80 m blurred nothing). Leaving the hangar view drops it (`HangarInspect.Reset`); a take-off ends it. The rooms are only modelled where the original camera looks, so some angles see past them. Action Freeze's photo tools work here too (the free camera and the Camera settings panel, see "UI and platforms"); the free camera's reach shows more of the rooms' unmodelled sides.
- The turret (`CutScene::checkForTurret`): the item's `hangar_turret_item_N` assembly on the turntable ship's slot-2 mount, turned (0, pi, 0) except the plasma collectors, still; rebuilt when the turret item changes (`StationLevel.RefreshTurret`). Remake (`TurretCore.Display`, `StationLevel.AddNpcTurrets`): the NPC ships parked and landing there show their turrets the same way (their type's `Database.NpcTurrets`, else 45 / 51 their fighter turret `turret_002_static` at its spot), the Kaamo Club's stored hulls their own mounted turrets, other players' ships theirs (`NetHangar`); `HangarTraffic.NpcSpawned` tells NPC ships from guests.
- **Hangar flights** (remake-only, option "Hangar arrival and take-off", `Settings.HangarFlights`; the original cuts straight between space and the parked ship): `HangarFlight` (plain C#) flies a ship along its hangar's lane (`StationTables.HangarLanes`, Unity metres measured on the room meshes: the forcefield's centre and outward direction, the height span it can be passed at, a cruise height above the parked ships; Vossk: the emblem portal on −x, bays facing the ring's centre, so the ships swing through the centre (every bend is a wide fillet) and fly into the bay 5 m up). Arrival: from 450 m outside (growing from nothing, so nothing pops in where the opening shows space) through the forcefield, level (the Nivelian hangar's six roof pillars, `StationTables.HangarLane.pillars` measured on its mesh, are flown round: `HangarFlight.PillarRoute`, an A* over a 4 m grid keeping each pillar's radius + the ship's half width + 10 m clear and favouring the middle of the gaps, string-pulled, cached per pad and width; a player's report 2026-10-16, ships flew through the pillar in front of the right pad; checked with every hangar ship's outline: 10.9 m at the closest), at up to 200 m/s, braking hard, then gently over the last 25 m through a rounded corner with a slight flare to a stop over the pad, a turn on the spot (65 deg/s, a little bank) to the parking yaw, straight down. Departure: the reverse (straight up, the turn at the top, the corner, out, shrinking away; the ship stays hidden at the end, not back at full size for the frame before Space loads). Where the path leaves (enters) the hangar camera's view the ship shrinks to nothing over the last (grows over the first) 160 m in view (at most 60 % of the lane in view), so it never crosses the screen edge at full size (a pad just outside the view, the Midorian hangar's right one, isn't such an edge: the ship takes off there at full size and flies into the view; it vanished over the pad and crossed the hangar unseen); the engine loop takes the space engines' rolloff (`EngineVoices.Setup3D`, to 500 m) and fades with the ship's size. Landings and take-offs are always vertical. The camera keeps the original framing (the rooms are only modelled where the 16:9 hangar camera looks). Only the player's own docking flies in (`SpaceLevel.Dock(true)`: the autopilot, touching the station, the Dock prompt, `Session.DockedFromSpace`); the story's moves into a station (the rescue, arrests, "docked at ...") and loads don't. `Launch` / `Depart` take off first (the story's direct launches into a story orbit don't). The station menu is away meanwhile: it flies / fades out as the take-off starts and back in once the ship has landed (`StationMenu.UpdateUiAnimation`, 450 ms, ease-out cubic: the top bar slides up, Launch right, the ticker down, the safe area fades, and the side panel's entries (the station information, then each shown button) come in one by one like the main menu's buttons: 40 px from the left, 180 ms each, 50 ms apart, the last one first going away; while it is away or moving nothing takes input: a blocker over the panel, the navigation events stopped, `Update` returns early (`UiBlocked`); the station's first frame takes the state as it is, so a fly-in starts with the menu away and a ship simply on its pad shows it at once); its conversations, fines, medals and hints wait until the ship has landed; any key / tap / button skips the flight. `HangarTraffic`: the parked ships (the original's 0..max) come and go every 12-35 s (landings likelier the emptier the hangar, at a random parking yaw like the parked ones; not in the owned Kaamo Club, none at the battlestation, max 0); one flight in the air at a time, the player's included, so nothing collides: the traffic waits for the player, and a departing player holds over its pad while an NPC flight finishes.
- **Parking heights** (remake, `StationTables.PadPivotY`, `Resources/GoF2Data/hangar_heights.json` from **GoF2 > Build > Hangar Heights**): the original's floor y + ShipY, lifted only where the hull would cut into the pad, per hangar, slot (the
  turntable too: its largest lift), ship and heading (24 bins); never lowered (the Terran cradles are open funnels). The
  builder drops each ship's hull bottom (the lowest vertex per 0.1 m column; 0.6 m left up to 1 m of hull in the steep
  pad rims) onto the room's meshes (MeshColliders in a preview scene, the animated parts at their loop start, not the
  additive layers) at every heading, from 30 m above the table height (12 m under the Vossk bays' roofs: 12 m started
  inside the Midorian / deep science raised pedestals for the tall hulls, or took them for a wall, so 18 ships had no lift
  there and sat ~12 m deep in the pedestal, a player's report at Kaamo). A parked or landing ship that needs a lift takes
  the nearest measured heading (15 deg steps; in between a long hull swept into what both cleared), and one that fits
  within 1 m of the pad's least lift (`StationTables.FitParkingYaw`; at some headings the Rhino reached over the 22 m
  structures beside a Midorian pedestal and hung 10 m in the air). Checked: every ship on every Kaamo pad at random
  headings, no hull more than 0.1 m into the room. Before: the wide
  hulls in the Midorian pads' raised rims (the H'Soc's wings 1.9 m), everything inside the raised pedestals of Midorian /
  deep science slot 2. `HangarFlight` also scales its bank and pitch down near the floor (the Vossk bays are flown into 5 m
  up: a banked H'Soc's wing dipped ~3 m into it), keeping the hull 0.4 m above its parked bottom.
- **Ship shadows** (remake; the original's hangars have none, and the hangar light comes from the camera's side, so a real
  shadow falls behind the ship and out of URP's 50 m shadow distance): `HangarShipShadow` on every ship `SpawnShip` puts in
  the hangar (the turntable, the parked ships, the flights, other players' ships) projects a soft contact shadow like a decal:
  `GoF2/HangarShadow` on a box around the footprint (only the ship's yaw, 12 m below (6 until 2026-10-16: the Terran turntable's inner ring, sloping ~7 m under a low kit ship's bottom into its pit, stayed lit) to 8 m above the resting hull's lowest
  point, a little wider as it lifts) rebuilds each pixel's scene point from the station camera's depth texture (switched on
  by it) and darkens it by the ship's map from **GoF2 > Build > Hangar Shadows** (`ShipShadowSet`, `HangarShadowsBuilder`:
  the hull's triangles rasterised from above into 128 x 128 over a square 2.2 x the hull, linear, uncompressed; R the
  underside's height over each texel (spread outward), G the silhouette, A a wide soft halo): under the silhouette and in
  the halo around it (the part a camera looking down past the hull sees), only below the hull's underside there (so the
  hull never shades itself; 3 m above it past the hull, the rims around a sunk hull), only on surfaces facing up (normals
  from the depth's derivatives), full up to 2.4 m above the floor, fading out to 8 m (`_Hull.zw`). Shown at once for a ship spawned on its pad (the turntable, the parked ships, a guest), fading out
  over 15 m as it lifts off and staying on the pad; an arriving ship's fades in the same way as it comes down
  (`HangarFlight.Arrival` -> `ExpectLanding`: the pad's floor is known from the start, the hover over the pad isn't
  taken for the landing; verified: none during the approach at 32 m, 16 % at 12.6 m, full at touch-down). The bake is `ShipShadowBaker` (shared by the builder): the mods' ships
  are baked by it at run time (glTFast keeps their meshes readable; once per assembly and set of mods, the rasterising on a
  worker thread, the shadow hidden until it is done; their engine / throttle glows left out; kept for later plays in
  `persistentDataPath/ModCache/ShipShadows/<mod id>/<hull hash>.shadow` (the hull's triangles are its identity, no mod
  hash: hashing every file of a mod was most of the cached load), read and written on the worker thread: the loading
  screen's pass keeps only the files its ships used in their mods' folders (`PruneUnused`: a new version's old maps go),
  `ModManager.Scan` deletes an uninstalled mod's folder,
  `PruneCache`; `CacheVersion` re-bakes them all after a change to the bake; verified: 5 PR ships cached, read back
  identical in 0-30 ms, both clean-ups; made on the main menu's loading screen, `ModShipsBaked` polled by
  `ModLoading`: once the models are built every template's map is read or baked, one hull read per frame, "Hangar shadows
  N / M: <ship>"; the templates' inactive holder is why `Collect` ignores the root's own active state; measured: 0.25 s
  baking all five PR ships, 0.03 s from the cache) and never take the set's
  map (their "ship_NNN_mod" numbers differ between games: stale maps of other ships under those names gave the Falcon a
  wrong shadow; the builder skips pack "mod"); the debug capital hulls get a soft oval of their bounds (the game's
  meshes are imported non-readable, so a build can't rasterise them). Verified in Play mode in the Terran,
  Nivelian, Vossk, Midorian and deep science hangars. The Graphics option **Hangar ship shadows** (`Settings.HangarShadows`:
  Off / Player ship only / All ships, default All; performance complaints on phones): every hangar ship still gets its
  `HangarShipShadow` (`Attach(..., player)`: `SpawnShip`'s "Player ship", the turntable's), the box is just hidden while the
  option doesn't allow it, so changing it docked applies at once; Off also gives the station camera's `requiresDepthOption`
  back its own value from before the first shadow (the depth texture is the bigger part of the cost), any other choice sets
  it On (again on every `Settings.Changed`).
- The room's animations all run (`CutScene::process` updates every geometry, not only the `_anim` meshes), skipping their one-off first key like every animation (`PartAnimation.loopStartMs`, see "Known open items"; played, the key flashed the Nivelian bar and the Midorian bar for a frame on every wrap, parked the Terran gutter lights on the player's pad and made the loops jump; the Vossk bar's streaks start at 500 ms); rotations swing back and forth (`pingPongRotation`: the Vossk ring lights' 57 deg sweep stays clear of the portal) and the `_anim` layers fade by their `extra` channel (the Vossk portal light).
- The Kaamo Club parks its stored hulls: see "Kaamo Club".
- **Midorian hangar, contributed remodel** (remake, 2026-10; sources `Reference/contrib/immersive_hangars`, Blender OBJs):
  the room made whole (walls, ceiling, tanks and light strips where the original's hangar was open to space, a third row of
  pads, the props copied around the bigger floor) and station_078_midorian's hangar mouth (a new frame and glowing panel).
  They replace the converted originals in `Models/main/hangars/hangar_midorian*.fbx` and
  `Models/main/stations/station_078_midorian(_emissive).fbx` in place (same .meta GUIDs and mesh names, so the per-mesh
  prefabs and assemblies keep working; the old geometry is only in git history), written by
  `Reference/tools/asset_conversion/contrib_immersive_hangars.py` (run with Blender; it documents the OBJ -> FBX mapping,
  the per-file offsets and the fixes). The assembly's sixth prop slot (ids 14401/14402, which the original fills with x5 a
  second time) shows x6 (`build_assemblies.py` MODEL_OVERRIDE; `Prefabs/main/hangars/hangar_midorian_x6*.prefab` made by
  hand, x6 has no resource id). Its third row of pads, behind the turntable, are parked slots 3 / 4 (`StationTables.ParkedSlots`,
  game (0, 0, -4096) a raised pedestal like slot 2, (-4096, 0, -4096) a sunk pad like the turntable's; `ParkedMax` 5). The hangar
  heights were rebuilt for it.

## Shop

Research: `Reference/research/shop.md` (+ `item_icons.json`, reference price code `Reference/tools/shop/prices.py`). Rules are plain C# in `Shop` (prices, stock, dealer ships) and `Hangar` (one opening of the window: trading, mounting, ship trade); the UI is `HangarWindow` (tabs Ship / Shop in `UI/Station`, styles `HangarWindow.uss`) driven by `StationMenu`. Player state is in `Session` (credits, `Equipment` = mounted stacks with secondary ammo as the amount, `Cargo`, the last 3 stations' stock).

- **Economy** (`Session.Economy`, the new game's second step after the difficulty, saved from v10; older saves = Android):
  **Android** = the remake's data files (the Android OBB's items.bin / ships.bin) or **Default** = the macOS / Windows /
  iPhone tables (KiritoJPK's `GOF2_2.0.16_Default_Economy.apk` differs from the Android one only by
  `assets/data/bin/items.bin` / `ships.bin`, kept in `Reference/binaries/default_economy/`;
  `Reference/tools/shop/build_default_economy.py` writes `Resources/GoF2Data/economy_default.json`: the complete values of
  the 188 items and 55 ships that differ). `Database.Load` applies the overlay when the session's economy is Default
  (`Database.Economy` says which): item prices, tech levels, occurrences, every attribute (and the stats), blueprint recipes;
  ship prices, stats and slots. Android vs Default: commodities ×2-25 (Energy Cells 1400-1540 / 560-700), tractor beams ~×18,
  signatures 500 000 / 7500 (Android: only at 107, attr 61), shields / armor / repair bots ~×2.5, blueprint recipes several
  times the ingredients (Khador Drive 200 / 10 Pyresium), starter weapons / scanner / drill cheaper, ships ×0.5-0.8; a few
  stats (mining lasers' attrs 32 / 33, PE Proton 17 / 49-51), tech levels and Night Owl's 3 / 2 secondary slots.
  Multiplayer sessions always use Android (the shared stock). The save slots show difficulty and economy.
- **Prices** are deterministic per station: min + distance factor * (max - min) +- 2 %. The original draws the +- 2 % from `java.util.Random(station)` reseeded per list (cargo, mounted, stock), so an item's price depended on its list place and a unit bought from the stock could sell for more from the hold (a multiplayer report: Garuda at Thynome bought 724, sold 746, repeatedly); remake, in a multiplayer session only: `java.util.Random(station * 10007 + item)` per item (`Shop.PriceList`, `NetSession.Active`); single player keeps the original's, verified identical to `prices.py` (its docking's prices pinned, `Hangar.PinPrices`). Selling pays the buying price (a faction member at its own station sells at the 10 % cut too, `Hangar.Sell`); anything sold joins the station's stock. Loma (system 25) always charges the maximum. Remake: an item's price is fixed for the docking (`Hangar.PinPrices`, cleared by `Hangar.NewDocking` in `StationLevel`): an item's price depends on its place in the lists, so a trade moving it between the stock and the hold could shift it by up to ±2 %, and in multiplayer, where the window is priced again after every trade, buying and selling at once made money with every click.
- **Stock** (`Generator::getItemBuyList`) and **dealer ships** (`getShipBuyList`) are rolled with `UnityEngine.Random` when the station isn't among the last 3 (`Status::departStation`: arriving in its orbit, `Shop.ArriveInOrbit` from `SpaceLevel`, or docking; a revisit doesn't reorder the stack; the remake pushed only on docking until 2026-10-16, so Katashán's empty dealer, `getShipBuyList`'s 1 in 6, stayed until 3 other dockings; not in multiplayer sessions); re-docking after > 30 s nibbles 0-2 units off each row.
- **Cargo:** every unit is 1 t, mounted items weigh nothing; buying never checks space, launching is refused while overloaded.
- **Unknown item numbers** (a Windows crash report, 2026-10-12: `ItemInfo.Category` on a null item from `HangarWindow.AddRow`
  after every trade; where the number came from is not known yet): `Shop.AddToCargo` refuses an item `Database.Shared` lacks
  (a warning with the caller's stack), `Hangar.ShopItems` leaves one already in the hold or stock out of the list (a warning
  once per number), `ItemInfo.Category(null)` is "", and `SaveGame.Load` drops such items from the save (after
  `ModSaves.Fix`, `DropUnknownItems`: hold, equipment, shop memory and its bar offers, the Kaamo Club, the parked ship,
  blueprints, the production queue; a warning each). A second report (Android, 2026-10-10) had it at docking.
- **Blueprint states** (`Blueprints.Fit`, from `State` and the readers): the per-ingredient list is made as long as the
  product's current recipe; one saved under another recipe (Default economy, a mod, an empty one for a then-unknown
  product) was shorter and the Blueprints tab read past its end (an Android crash report, 2026-10-11).
- **Cloud Diagnostics**: `Bootstrap.Init` switches exception capture off in the Editor
  (`CrashReportHandler.enableCaptureExceptions`): contributors' clones reported their Editors' errors.
- **ScrollTo** from a scheduled callback checks `contentContainer.Contains` first (`CheatsCatalog`'s icon grid,
  `HangarWindow.ScrollToSelected`): the list can be rebuilt before it runs, and ScrollView.ScrollTo throws then.
- **Sell all / Buy all** (remake, players' suggestion; Store all / Take all in the Kaamo Club's storage; `HangarWindow.TradeAll`, buttons under the trade arrows, or Shift + left / right, or a controller's LT / RT, shown on the buttons and in the hint row): every unit of the selected item: what the hold has of it, or the station's stock as far as the hold has room and the credits reach (no "not enough credits" after some units). In a session one stock message carries the count (`Hangar.BeginBatch` / `EndBatch`; `NetState.StockItemRpc` takes up to 10 000 units, grants what the host's row has and refuses the rest in one `ItemRefusedRpc` with the count, the faction tax on the granted units).
- **List after a trade** (remake, `HangarWindow.Rebuild`; after a mount, demount or swap too, #45: they reset the selection to the top): the same row stays selected; when it is gone (missiles of a mounted
  type bought out: they go onto the mounted stack, not the hold) the row that took its place (or the one before it at the
  end, `NearestSelectable`), so the list keeps its place; it used to jump back to the top.
- **Lists wrap round** (remake, players' request; `HangarWindow.MoveSelection`, `CarrierShopWindow.Move`): up on the
  first row selects the last and down on the last the first (every tab, the blueprint ingredients, the carrier's shop),
  scrolled to it, so the commodities are one press up from the shop's top; only a fresh press wraps, a held key's repeat
  stops at the end. The mouse wheel / touch scrolling don't wrap; the station's focus-navigated lists (lounge visitors,
  Missions, save slots) don't either.
- **Mounting:** first free slot of the item's type; on docking the equipment is fitted to the ship's slots (remake, `Hangar.FitToSlots`, from `StationLevel`: a mod lowering a ship's slots or a debug hull swap could leave more mounted than it has; the rest goes to the hold in slot order like a bought ship's, story items keep their place); secondaries move with their ammo; one-per-ship categories (shield, armor, booster, scanner...) swap (287); bought missiles of a mounted type join the mounted stack. Remaining missiles are written back to `Equipment` when docking. Remake: the Ship tab tints mounted rows with a cyan edge and badges the hold's candidates IN CARGO under an amber "Available in cargo" header; a a passenger cabin can't be demounted while the other cabins couldn't seat the passengers aboard (323, `HangarWindow::selectItem` 0x178e20); a double click / double tap mounts or demounts (the Mount / Demount button's action; a controller's confirm button does the same).
- **Hangar opened with A / Enter** (#104, `StationMenu.hangarConfirmHeld`): the window ignores Enter / A until the press that
  opened it is released; the Hangar button took the press, and in the same frame the window bought its first row.
- **Ships:** price -1 % in systems of the ship's race; trade-in at full price; remake (players' report), in a multiplayer session only: a bought hull (dealer, lounge seller, Kaamo Keep, a blueprint's ship) starts bare, the saleable mounted items go to the hold (`Hangar.SwitchTo` dismount; the story's unsaleable items stay mounted; single player, like the original, moves them onto the new hull); equipment that doesn't fit goes to cargo; the hangar's turntable swaps the model. Remake (#60 / #62): without the Kaamo Club the 304 question adds what happens to the old ship ("Your X is traded in for N, so you pay / get back M", `shopTradeInNote` / `shopTradeInRefund`); the details panel's ship stats of a dealer or stored hull carry the original's comparison arrows against the ship flown (`ItemInfo.ShipStatsCompared`, the Kaamo upgrades counted on both sides; the original shows them only in its item window); a Shop tab row of an item the hold has gets the Ship tab's cargo look (amber edge, IN CARGO).
- **New game:** 0 credits, like the original (sell starting gear to get money). `Session.CampaignMission` is a free-play 20 (past the tutorial locks) until there is a campaign. Var Hastra always stocks a drill and energy cells then (remake-only, see "Mining" and "Star map and system travel").
- Blueprints tab: see "Bar and freelance". Ship mods (Kaamo mechanics): +40 hull, +30 t cargo, +1 equipment slot, handling +0.2, kept with the hull (`Session.ShipMods`). Remake (players' suggestion): a hull's mods show as lines under its description, "Kaamo Club armor upgrade applied (+40)" etc. (`ItemInfo.FillModLines`: the hangar's own / dealer / stored ship, the carrier's repair page, the item window), and the hangar's stat rows include them with "(+)" (`ItemInfo.ShipStats(..., mods)`). **Stacking** (remake, Options > Gameplay "Stackable Kaamo Club upgrades", `Settings.KaamoStacking`, on by default): the mechanics fit their mod again up to the upgrade limit (`Session.MaxModLevel` = Options > Gameplay "Kaamo Club upgrade limit", `Settings.KaamoMaxLevel`, 2..20, default 5; 10 until 2026-10-16, after players' feedback that uncapped they reached 45 equipment slots on a Cronus; its row only while stacking is on; a session rule: the host's option, a dedicated server's `-kaamomaxlevel N` / `/set kaamomaxlevel`, carried beside the rule bits as `NetRules.HostKaamoLevel` / `NetState.KaamoLevel`; a server with profiles cuts profiles back to it, `NetProfiles.ClampMods`; levels fitted before a lower limit stay in single player; at the top the mechanic says so, `kaamoModMaxed`), a level = how many times it is in `Session.ShipMods` (`Session.ModLevel`; every effect is per level: hull, cargo, slots, `BuildFlightStats`' handling upgrades), the price x2 per level fitted (`LoungeChat.ModPrice`, capped at int.MaxValue; a ship without a price, the Cronus (37, Khador's gift at step 77), is valued at 2 500 000 between its sister ships the Typhon and the Nemesis, `UnpricedShipValue`; in multiplayer the Cronus itself costs 1 150 000, `Database.CronusSessionPrice`: its upgrades cost 0 at every level; the offer adds "(Upgrade level N.)"), the lines show the total and "level N"; off = the original's one of each (`LoungeChat.ModForSale`), levels already fitted stay. The list format is unchanged, so saves, traded-in / stored hulls and presets carry the levels. A traded-in hull keeps them in the dealer row that takes it (`StationStock.shipMods`, by ship index; `HangarWindow::OnTouchEnd` 0x176d94 adds the old ship's mods to that row and gives the bought row's mods to the new ship), so selling a modded ship and buying it back returns its mods; the row shows (+) and lists them. Rows replaced outside a trade (story steps, the multiplayer host's shared list) drop their mods (`PruneShipMods`).
- Kaamo Club storage (Store tab, stored hulls, 327 Sell / Keep): see "Kaamo Club".
- The Supernova wrecks' hidden blueprints (hacking): see "Supernova add-on".
- **Dealer extras** (`Shop.GenerateShips`, `getShipBuyList` 0xa0eb8): none in the supernova system (no stock either); Kothar after Valkyrie exactly 37 / 38 / 40; Quineros (107) every pirate ship plus 45-48 for the terminated Most Wanted 6 / 12 / 18 / 24; Thynome (10) the VoidX alone with every gold medal (re-rolled on docking); a roll of 0 ships gives none; after Valkyrie the Vossk dealers 1/2 each 39 / 41 and every Vossk fighter roll 60 / 25 / 15 % 9 / 41 / 39 (`NpcTables.RandomFighter`, traffic too); Katashán (120) after 158: 44 (Extreme or all Supernova medals) and 49. Docking fix-ups (`Story.OnDocked`): 50 Void Crystals at Thynome after the game is won for a player without a Khador Drive, 10 energy cells at 10 / 100 / 101 for a nearly empty hold; stock extras 190 (139, system 25), 209 at 126.
- **Item details** (`ItemInfoWindow`, `ListItemWindow`): the selected row's "i" (or I / controller Y; remake: with a controller the "i" shows the yellow Y instead, `.row-info-pad`, and an INFO hint in the hangar's hint row) and the lounge's "Let me see it" (776, no price) open a full-screen window: 390 Info, 643, the name box, the stat rows (ships with 0x512 / 0x513 / 0x514 comparison arrows against the current ship, mods "(+)"), the 3D player-variant ship on layer 30 into a RenderTexture over the floor glow 0x50b (remake: framed by its bounds along the original's camera direction), drag / A D / stick turning (120 px = 1 rad, from 260 px, ×0.9 fling), 280 Description with the text; sound 97 on opening; Back 170 / Esc / B. Remake (#82): the hangar's details panel shows the same turning ship under the name for ship rows and mod ship blueprints (`ShipPreview`, shared with this window, each on a stage of its own; the shop icon hidden meanwhile, `.detail-preview`), spinning slowly (0.3 rad/s) unless dragged; a click / tap on it opens this window. Phones (PR #107): the preview's
  texture is the box's own size on screen times the render scale, MSAA 2x, no mipmaps (the desktop's screen-tall 2x
  supersampled one cost frame rate), the ship-only view keeps the desktop's 1300 x 800 box, and the hangar list's
  scrollbar has a 56 px touch area around the slim track (`.list-bar-track`), its handle at least 60 px.
- **Weapon mod factors** (attrs 39 / 40) read "-10 %" for a negative one (#105: "+-10 %", `ItemInfo` Signed).

## Weapons

Research: `Reference/research/weapons.md` (functions, per-item table, fx, sounds, lock-on, HUD rects). `weapon_fx.json` is generated from it by `Reference/tools/weapons/build_weapon_fx.py`; **GoF2 > Build > Weapon Fx** turns it into `Resources/GoF2Weapons/item_XXX` (`WeaponFx`: projectile / muzzle / impact prefabs + shot sound) and cuts the crosshair.

- **Shop with a controller / keyboard** (remake): on a shop or ingredient row A / Enter buys (takes, adds) one unit and X
  sells (stores) one, like the right / left arrows (the original has only the arrows; its 101 / 100 sounds play).
- **Controller focus** (remake, `GoF2Common.uss` `.input-gamepad`): the focused control gets an amber fill and frame with a
  controller (the main menu root carries the class too); a focused stepper / segment row (`.choice-row`, the Debug pages' Give items / Spawn / Ships) its fill and amber arrows (#24). The main menu's Debug panel: left / right jump between the mission list and the cheats, back to where the focus was in that column (`MainMenu.OnNavigate`). The Status window's medal grid scrolls to the focused medal (#45).
- `Gun` (plain C#): one per equipped weapon on its mount (remake: weapons past the model's mount points, a mod's or a Kaamo upgrade's extra slots, reuse the mounts in turn, the centre without any; they were skipped, so an extra secondary could be neither selected nor fired); bullet pool; attr 11 reload, attr 9 damage, **attr 13 speed in units/ms, attr 12 "range" = lifetime in ms**; straight along the nose (no convergence/aim assist); axis-aligned cube hit test (or local boxes for freighters; remake: the hull's meshes, see "Mesh collision"), never hitting its `owner`; rockets/missiles coast 2 s past their lifetime; missiles home on `LockTarget` (the radar's ship lock). NPC guns use the same class (see "NPCs and combat"); `GunRig` draws projectiles, muzzle flashes and impacts for both; the muzzle flash points along the ship's nose with the camera's up (`ObjectGun::update`; turrets: the turret's matrix).
- `WeaponSystem` (player): primaries fire independently while held (the looping shot sounds end like their FMOD events on release: "loop and play to end" cannons / thermo / Hammerhead turrets finish the shot in progress, the "cutoff" DLC turrets fade 50-100 ms; `LoopRelease`), secondary one per release (ammo = item amount, `Session.EquipmentAmounts`). Keys: Space / LMB fire, R / RMB missile; controller RT fire, LT missile (throttle moved to LB/RB).
- `Target`: hittable objects (asteroids: radius meshRadius*scale*0.7, HP scale*100+30, rockets kill asteroids instantly, explosion prefab + sound 21; ships and the player with `Hitpoints`). Stations are never hit.
- **Special weapons** (`Reference/research/weapons_special.md`, `Gun.Kind`): beams (9-11, 228: a stretched beam mesh, instant hit, auto-aim within a cone; `BeamGun::update`: the beam keeps the direction and length it was fired with while its start rides on the mount, so through a turn it stays where it was shot; re-aimed every frame from 2026-10-01 until players asked for the original back on 2026-10-11), scatter guns (burst cube), EMP bombs and nukes (a second press detonates, area damage with falloff, explosion types 7 / 11), mines (drift, proximity), cluster missiles (salvo), the Shock Blast (226: damage + EMP + push, `NpcShip.InitPush`), ionizing missiles, plasma collectors, weapon mods (sort 28 factors on primaries). Several secondaries: G / D-pad right (or tapping the HUD label) cycles `Session.SelectedSecondary`.
- **Beam auto-aim** (#100, `WeaponSystem.BeamTarget`): with the Smart target lock (the default) a friendly or neutral ship is
  only aimed at when it is the radar's lock; the research says the original takes the nearest *enemy*, the remake took any
  ship and a friendly crossing the box took the shots. Original lock: as before. Never an asteroid (a player's report): `Radar::draw` sets
  the auto-aim flag (KIPlayer+0x6f) only in its loop over `Level::getEnemies` (the ±24000 cube branch is for near ships),
  its asteroid loop never does; the remake's beams took the nearest rock in the box while enemies were about.
- **Liberator** (guided rocket): the ship holds still and the stick steers the rocket, the camera follows it; ends on impact or timeout. Remake: steered as seen through the camera (`WeaponSystem.UpdateLiberator`, `Gun.SteerBullet`'s axes: the camera's up and right; the camera follows it upright, world up, like the original's `useTargetsUpVector(false)`), so stick right turns it right on screen whatever the ship's roll at launch (turned about its own launch-time axes, a Liberator fired upside down steered mirrored); the invert options apply; mouse steering steers it too (`FlightHud.UpdateMouseSteering` switched it off while a missile was steered), the virtual stick centred as it starts and ends.
- **Turrets** (`PlayerTurret`, `TurretAim`): the turret-slot item on the ship's turret mount (in space the `*_ship_mounted` assemblies, in the hangar `hangar_turret_item_N`; remake fix: the Archimedes' (182) and the 181's gun offsets are written turned with their base in the ship-mounted rules, (-34, 88, -1.6) / (0, 88, 11), since the base turns inside the pivot there; unturned the Archimedes' gun sat on the far side of its base in space, while the hangar, which turns the whole object, was right; every turret's gun-to-base offset now matches between the two); auto turrets (180-182) aim and fire at hostiles, the others by the turret view (T / D-pad up, touch button), Y / D-pad down toggles auto fire; animations only while firing. **Sentry guns** (`SentryGun`, 211-213): up to 3 deployed as secondaries, 100 HP, 3 s invulnerable, hostile NPCs attack them.
- **Turret view inversion** (#103): the flight's invert options apply to the manual turret view too (`PlayerTurret`), as they
  already did to the steered Liberator (`WeaponSystem`).
- **One turret for everyone** (remake, `TurretCore` + `TurretTargeting`, plain C#; edit-mode `TurretCoreTests`, `NpcTurretTests`): the player's turret items (`PlayerTurret`), the NPC turret objects (`NpcShip` turret mode: the capital ships', the outposts' and pirate events', the fighter turrets of 45 / 51, the NPC sentry guns), the player's sentries (`SentryGun`) and every hangar's still copy share one core: the model's "pivot" / "*_gun" nodes (`FindNodes`), the mount placement (`PlaceOnMount`, `WeaponMount.TurretTurn`), the aim (`ItemAim`: attr 17's yaw, pitch -70 .. +500; `StaticAim`: -100 .. +600; a sentry's unlimited), the muzzle and shot start (`ShotFrom.GunFrame`: the item's (0, 0, 300), 48's alternating x +-80, 181's z -300, a mod model's own; `AheadOfBarrel`: an NPC turret object's 600 units; `Muzzle`: a sentry's), firing (`Fire`) and the animations that run only while firing; the pick is `TurretTargeting` with the original's two rule sets kept apart: `Retarget` (`PlayerEgo::handleAutoTurret`, the sentries: every 3000 ms or at once when the target is lost, the unreachable one skipped) and `Timed` (`PlayerTurret::pickEnemy` / `handleTurret`, the NPC turrets: only after more than 3000 ms, the unreachable one as a fallback; its `Measure` sees the best distance so far, which the turret hunters' x0.3 needs). What stays per owner: the turret view, auto switch, collectors and loop sound (`PlayerTurret`); the Target, hull, death, host sync and capital enhancements (`NpcShip`); deploying and death (`SentryGun`). `TurretCore.Display` / `DisplayAll` is the hangar's (`CutScene::checkForTurret`), `PlayerTurret.BuildStatic` calls it. On top (remake): **any turret item on an NPC** — `SpawnSpec.turretItems` (turret items on the ship's own turret mounts, `World.NpcMountedTurrets`: the item's model, rates, gun, missiles and salvos, aimed and fired by the NPC turrets' rules, range attr 109 else 50000, while flying, awake, unparked, not EMP-disabled; manual items aim by themselves, collectors only sit there; their guns in `NpcShip.ExtraGuns` for the shot mirrors; on 45 / 51 they take the fighter turret's place), else the ship type's (`Database.NpcTurrets`, a mod's ships.json "npcTurrets"); `SpawnSpec.turretItem` builds a turret object from an item (`TurretCore.ForItemModel`: its model at the object's scale, its gun x `gunFactor`, its aim and shot start; ModelPath "turret:N", `NpcShip.TurretModelPrefix`, which `NetProxy` loads); `/spawn ... turrets 48,182`, the debug Spawn page's "Turrets" row (the spawned ship's mounts, a capital ship's turret objects). The original's NPC ships carry no turret items, so nothing changes for them. Multiplayer: another player's copy of such an NPC shows its turret items still (`NetProxy.turrets`, "ship:item,item", `NpcMountedTurrets.Pack`); not tested with a second player. Verified in Play mode (a test mod's GLB turret, item 236 based on 182 with missiles, as the Veteran's `npcTurrets`): the player's auto turret (the mod model) killing two hostile Veterans, the NPC Veterans' mounted mod turrets firing in a fleet battle, the capital turrets aiming and firing (9 of 13 at once), a battleship built from item 48's turrets firing at the Vossk side, a sentry picking a raider, the manual turret view (47) with the crosshair on the barrel, the hangar's turntable with the mod turret, a parked NPC Veteran and Rhino with theirs.
- **Homing missiles near their goal** (remake, `Gun.Update`; user report): `RocketGun::seekEnemy` steers by (goal dir - dir) / k a frame at a constant speed, which can't turn tighter than ~150 m at 18 u/ms, so a missile passing its target (no ±1000-unit hit cube with the hull meshes) or reaching a salvo's spot in empty space inside a hull's hit boxes (`CapitalShips.SpreadAim`) looped round it. Within 250 ms of flight of its goal it now turns straight at it (`TerminalHomingMs`), and reaching the goal (this frame's path within 3 m of it) is a hit on its homing target when the gun has a hit list (not the multiplayer mirrors).
- **Smoke trails** (`RocketTrail`, `GunRig.EnableTrails`; `RocketGun::setRadar` / `update`, `ParticleSystemMesh::emitTrail`):
  the player's rockets and missiles (and other players' mirrored ones) trail record 39, a ribbon of particles.png's white
  smoke strip, a new section every 125 units, the last 29 kept (~180 m), 200 units wide, white -> transparent over 3000
  ms, drawn 2000 ms more after the rocket dies; the cluster missiles and thermo guns 28 / 29 / 30 records 25 / 26 / 27
  (the gold / red / purple strips, 50 / 100 / 150 units, every 50, 25 sections, 1000 ms). NPC rockets have none (setRadar
  is the player's); the SunFire o50 (193) record 28, its own flame strip on v_projectiles.png (material 24096, Level+0x98; #45). Every BombGun sort (EMP bombs, AMR nukes, ionizing missiles, the Shock Blast) trails record 12, the fire sprites (the Liberator's emission stays off), the Fireworks record 47, the same on the sparks sheet (material 27321, Level+0x9c) (`RocketTrail.HasMissileTrail` / `MissileTrail`, `MaterialFor`; `CombatAssets.sunfireTrailMaterial` / `fireworksSparkMaterial`). A nuke's blast is the plain type-0 ship explosion at scale 1 without fire streaks, like the original (`BombGun::BombGun`: `Explosion(0)`, `setScaling` only for the Shock Blast). Remake: one camera-facing ribbon for the
  original's two crossed ones. The rocket meshes' `_add` flame children were already there.
- Explosions (`Explosion.Spawn(type, ...)`): 0 ship, 7 EMP, 8-10 scatter (one mesh, each gun its own colour: materials 20151 / 20152 / 20153 for 176 / 177 / 178, `CombatAssets.scatterMaterials`; the one per-mesh prefab carries only the last id's, so every scatter burst was the Icarus' until they were swapped in), 11 shock blast, 13 fireworks. The fx atlases are imported clamped (repeat wrapping drew lines on explosions; except khador_jump and hyper_drive, whose meshes map past 0..1: `AssetImport.FxRepeats`, the original never clamps) with mipmaps, trilinear and 8x anisotropic like the original (`AssetImport.IsFxAtlas`; every fx .aei is type 3 / 0x42, image flag 2 = glGenerateMipmap): without mips the exhaust and engine glow shimmered.
- **Pooling** (remake, 2026-10-11): explosions are pooled (`Explosion`: a played-out one gives its parts back, keyed by prefab, material and pivot, and its root waits for the next Spawn; the pool is an inactive holder in the active scene and goes with it; a caller that keeps an explosion checks `Done(Serial)`, as `NpcShip` does, since the root is reused), one-shot sounds are 24 voices (`Sfx`: a free one, else the quietest when the new sound is louder), rocket-hit bursts one emitter per kind (`ShipBurn.ManualBurst`), and `PartAnimation` parses each model's sidecar once (`Parse`, shared read-only by every instance: every Instantiate of an animated model parsed it again, ~300 KB of garbage per ship explosion). Measured (Editor, 50 explosions/s for 20 s): garbage 193 KB/frame -> none after warm-up, the worst spawning frame 36 ms -> 4 ms, p95 frame 21.3 -> 17.8 ms. Projectiles, impacts, rocket trails (`GunRig`) and shot sounds (`ShotVoices`) were pooled already.

## Combat equipment

Research: `Reference/research/combat_equipment.md`. All equipment is looked up by item category (= the original's sort, `Shop.FirstMounted`).

- **Cloak** (`Cloak` plain C# + `PlayerCloak`; items 94-96, integrated on ships 44 / 49 = item 95's stats; a mounted cloak replaces the integrated one; remake option, Options > Gameplay "Cloak bay on cloaking ships", `Settings.CloakBay`, on by default: on those two ships it goes into the integrated cloak's bay instead of an equipment slot (`Hangar.HasCloakBay` / `BaySlot` / `BayTakes`: one more equipment place while a cloak is mounted; off = the original, a slot; turned off, the next docking's `FitToSlots` moves what no longer fits to the hold)): the quick menu's entry (the item's name; half-transparent while not ready) or C / right stick press pays attr 38 energy cells (122, "-N t Energy Cells"; not enough = 583), charges attr 36 ms (the Khador charge bar with "Cloak charging" 317), cloaks for attr 35 ms (2 s fades), then a 7000 ms (Extreme 12000) cooldown and "Cloak ready" (316). Cloaked (`Target.cloaked`): NPCs keep chasing but don't fire, turrets don't aim, sleepers don't wake; nothing ends it early. Remake option (Options > Gameplay "EMP breaks cloaks", `Settings.EmpBreaksCloak`, on by default, a session rule; players' suggestion): an EMP hit (a shot or a blast) on a cloaked or charging cloak ends it at once, then the cooldown (`Cloak.Break`, `PlayerCloak.Break`: "Cloak disrupted!"; from NPC EMP shots, `NpcShip.EmpBreaksCloak`, and in multiplayer another player's, `NetPlayer.EmpRpc`; the EMP still drains nothing else of the player); a Specter's cloak drops the same and can't come back on its own for 6 s (`NpcCloak.Break`, `NpcShip.BreakCloak` from `WeaponSystem.ApplyEmp`, NPC EMP and `NpcShip.OnRemoteEmp`); a phase cloak is out of the EMP's reach. Multiplayer: an NPC's cloak reaches the other players' copies (`NetProxy.cloakPct`, drawn with `NpcCloak.Show` like another player's, off the radar and the lock from 25 %); the copy stays hittable while cloaked (it used to vanish whole, hull included), so anyone's EMP breaks a Specter's cloak and a blind shot lands. Remake option (Options > Gameplay "Cloak shakes off pursuers", `Settings.CloakLosesPursuers`, off by default): NPC ships lose a cloaked target (`NpcShip.Pursuable` in the targeting), fly to where they last saw it (12 s at most, until within 150 m) and go back to their routes; a finished route flies straight on instead of circling the cloaked player. In a session it is the session's rule (see "Multiplayer", Gameplay rules). Remake: the homing missiles locked on the ship lose their lock like on a boost (see "Missile warning and evasion"); in multiplayer the other players lose the ship's marker and lock (a held lock too, `CombatRadar`) from the moment it engages. Look: the hull switches to `GoF2/Cloak` (BumpShaderCloak's fragment shader as it is: dissolve by `cloak_map.png`'s veins into the refracted screen with a light-blue band along the front, over the lit hull up to 0.5 and into pure refraction by 0.75, so the pattern shows through most of each 2 s fade; #47: it ran at twice the speed; drawn by `CloakPass` after URP's transparents from a copy of the frame, like the original's refraction FBO (`Engine::CopyFBO` after every alpha / additive material), so the hull refracts the ship's own lights and glare, smeared by the wobble: the crawling coloured patches of a cloaked ship), the lights keep glowing and animating (the original's white at alpha max(50, ...) leaves its additive layers at full brightness; #47: the remake hid them), the NPC Specters' engine glow hides from 25 % (`CloakGlow`, `NpcCloak`; the code hides the ship group's last child), the players' ships keep theirs (`CloakGlow.KeepEngine`, #85: the reporter's screenshots of the original show it on cloaked player ships); sound 30. Medal 19 counts `Session.CloakMs`. **Phase cloak** (remake, for mods: a cloak item with attribute 104 = 1, `ItemStats` "phaseCloak"; the example is the `pegaphase` mod in the mods repo, the PegaPhase): while cloaked, fades included (`PlayerCloak.Phasing`), `PlayerCollision` skips the landmarks, freighters, other players' ships and asteroids (the wormhole still pulls); ending inside one collides as usual. `Target.phased` (set by `PlayerCloak`, and by `NetPlayer` from the owner's `phase` variable on the other players' copies, whose sphere `Obstacle` turns off): `Gun` hit tests and blasts skip it, `Target.Damage` and EMP do nothing.
- **Emergency system** (185): the hull running out sets it to 1 instead (`Target.SaveFromDeath`), 10 s invulnerable (attr 41) in the `v_shield` bubble (`GoF2/ShieldBubble`, `SimpleRefractionShader`: an invisible sphere bending the screen behind it, the rim from the noise texture read as a tangent-space normal, no scroll; the scene it shows clamped to LDR (#45: the HDR sun core smeared over it bloomed the screen white); grows / shrinks over 5 %), sound 1115 looped until it ends; the item is used up. Kills meanwhile count for medal 43 (`Session.GraveRiserKills`).
- **Time extender** (184): the fast-forward slot shows its clock outside the autopilot (touch), or X / left stick press: 15 s (attr 42) of world ×0.3 (`Time.timeScale`, `Navigation.ApplyTimeScale`) with the player's own updates (flight, chase camera, guns, cloak, beams) at ×0.7 (`TimeExtender.PlayerFactor`), sounds pitched down, sound 1120 TimeShift_Start, 1119 TimeShift_01b at the end; tap again ends it; 30 s cooldown (attr 43), then the icon flashes. Cinematics, jumps and death cancel it.
- **Repair / transfusion beams** (`RepairBeam`, sorts 37 / 41): automatic; every 2.5 s the attr 55 slots take the most damaged friendly ships (repair: hull +dt·0.03·attr54/100) or hostile ships (transfusion: −dt·0.01·attr54/100 as player damage, the same into the player's shield; both only while the shield isn't full, and only then are hostile ships picked: a full shield leaves the beams idle, which players took for switching on and off at random) within attr 53; beam meshes 19092 / 19093, loop sounds.
- **Shield injector** (227): an empty shield takes 30 t Blue Plasma (202, "-30t Blue Plasma") and refills at 0.15 per ms; sounds 2258 / 2257 / 2259.
- **Gamma** (supernova stations 109-113, `PlayerHealth`): a 0..100 pool drains at the station's rate by campaign progress (gamma shields 205 / 206 cut it by attr 52 %), "Warning: Gamma shield low" (3201) below 15, death at 0, carried between those orbits (`Session.PlayerGamma`); the HUD's third status row (icon 0x1f59, frame 0x1f5a, fill 0x1f5b, `GoF2Hud/status_gamma*`) shows the pool.
  The blaze (`PlayerHealth.SetupBlaze`, `PlayerEgo::PlayerEgo` 0xa5d8c / `PlayerEgo::update` 0xa9b7c): in the supernova system or
  Luur's orbit with a gamma shield mounted, meshes 18803 + 18802 (`sn_ship_blaze_flames / glow_anim_add`, `CombatAssets.gammaBlaze*`)
  around the ship, scaled by its bounding radius x 1.75 (`PlayerEgo::setShip` +0x3c), turned to the sun's light direction (up
  0, 1, 0), ship 8 300 units ahead, animated; gone on death and the planet jump.
- Spectral filters, gas clouds and plasma collectors: see "Supernova add-on".
- Not yet: the cloak's exhaust colour and the "not enough cells" window (a HUD message stands in), the menu button's ready flash.

## Sound

The FMOD data comes from the FEV's LGCY chunk (`Reference/tools/audio/fev_lgcy.py`, `Reference/research/fmod_event_ids.txt`: every system id -> event, group, .ogg files). Builders take clips by event id where it matters (`WeaponBuilder.EventClips`: every weapon's shot event from `weapon_fx.json`'s `soundId`, all its waves, `WeaponFx.Shot` / `ExplosionSound` pick one at random; the sentry deploy is 2263 for all three).

- **Volumes**: an event's own volume × `Sfx.EventGain` (4, the level the remake's older sounds already play at) for the sounds rebuilt from the FEV: the engines, boosts, NPC engines (46 0.076, freighters 47 0.195), the gamma loop, the collector loop, the ambiences, the alarm. The older SFX still play at hand-set levels.
- **Engine loops in space** (`EngineVoices`; every engine event 42-48 / 1104-1107 is 3D, linear rolloff from 1 to 10000 game
  units, the listener at the camera: `MGame::OnUpdate`, `Player::PlayEngineSound` / `Player::update`): NPC and other
  players' engines fade linearly from 0.05 to 500 m; the player's own engine (a 2D source) takes the same factor from the
  camera's distance (about 85 % behind the ship); the NPC events 46 / 47 / 48 play at most 3 each at once, the loudest
  (max_playbacks 3, behaviour 5), fading in 800 ms / out 200 ms. Before: every ship's loop audible to 1.5 km.
- **Parameters** (envelope values: pitch ×2^(8v − 4), volume linear): the drill (`DrillSound`, see "Mining"), the map whoosh (see "Star map"), the player engine (`PlayerEngine`: `PlayerEgo::PlayerEgo` picks 42-45 by handling, ships 42 / 43 / 40 1104 / 1106 / 1107; pitch by the stick's larger axis ×0.891 → ×1.122, "load" never set = gain 0.7; the gamma shield's loop takes its place; paused while mining) and the boost sound (38-41 by booster, 1102 Polytron, on a boost's start).
- **Cycle events** (`CycleSound`: a self-running looping parameter with looped layers and oneshot adds at fixed points, random waves): 153 the mothership (level 42, "time" 0.5 from `LevelScript::process` state 6), 122 / 95 / 108 the station ambiences.
- **Volatile goods** (`VolatileCargo`, `PlayerEgo::update`): with 204 / 209 aboard the volatile force grows by stick changes (dt·0.0005·max|Δ|, counted per 30 fps frame), boosting (dt·0.001·0.13, Polytron 0.17) and asteroid hits (+0.2), the dodge (+0.17), a player bomb's ignition, every shot (+0.008) and every hit taken (+0.065); it decays 0.025/s; at 1 the ship explodes; 35 Selfdestruct_Warning beeps by it (the spawn intensity envelope 0 below 0.2, then 0.18 → 0.66, which libfmodevent turns into (e^(5.7865 v) − 1) · 0.3078: a beep every 1.7 s at 0.2, almost continuous at 1; +3 st); 3202 warns 5 s in (`FlightHints`).
- **Music rules** (`Traffic.UpdateMusic`, `Radar::draw` 0x157c6c): nothing switches while 143 IntroAtmo plays; at 0x91 a calm orbit keeps the battle track.
- **Flight UI**: buttons in the pause / autopilot menus, the Menu button and the dialogue click 124 / 123; the pause menu's confirmation 126. Step 15's success page 1833 (or skipping it) stops the music for 136 and loops 162 Alert (`StationMenu.StartVoidAlarm`).
- Fixed from the audit: 141 after the time jump = Space_Battle_Medium looped (was TimeShift_Start), the gamma shields' loops (205 = 2261, 206 = 2260), the gas cloud collect = 2256 Mud1-4, 92's freighter jump 0x8c9, 105's bomb 14, the Void / Specter shots 62 / 2276, 144 OutroSong loops, 144/145's periodic explosion Med, space junk 22, the collector turret view 2255.
- 156 (the prologue wreck's broken engine) is a `CycleSound` (3.3 s cycle, random adds: no repeat, volume ×0.708..1, pitch ±0.025 octave, sound definition 1253); 158 Rumble_CutScene_01 is a 17.9 s oneshot (0.624), 161 Engine_09_Broken a loop at 0.115 (`CampaignLevel.PlayLoop` takes the event volume); 1116 (the Liberator) pitches its loop by the rocket's banking (`BombGun::update`: banking += dt·stick x·0.01, Vertical = ×0.2) and adds EngineDLC_06 from 0.336.
- **Weapon mod pitch** (`WeaponSystem.primaryPitch`, #76; `MGame::OnInitialize` -> `PlayerEgo::pitchAllPrimaryGuns(1 - Ship::getFireRateFactor())`, Gun+0xb0, `Player::playShootSound`'s raw FMOD pitch, raw x 4 = octaves): with a sort-28 weapon mod that speeds the fire rate the player's primary shots (and their loops) play 2^(4 x (attr 39 / 100)) higher: the Nirai Overdrive (+20 %) 0.8 octaves; a slower one (the Overcharge, -10 %) is skipped like the original's negative value.
- **Shot voices** (`ShotVoices`): every gun shot (the player's, NPCs', other players' mirrored ones) plays through shared 2D voices, at most 2 per shot sound, a third restarting the newest (the FEV weapon events' max_playbacks 2, behaviour 1); shots away from the camera fall off like `Sfx.PlayAt`. Before, each shot was its own PlayOneShot and a few ships firing clipped ("crunchy"). The mirror sounds only one primary per shooter (`Player::calcWeaponSounds`: only the first primary gun makes the sound, also the player's own: a mixed loadout sounds slot 1, as the original; the Sound tab's "Every weapon's own shot sound" sounds the first gun of each primary item, mirrors too). Real voices 64 (AudioManager).
- The rescue (index 1) plays the system race's space track (index 0's state 16 sets `Globals::switch_to_target_setting`: `playMusicAndFadeOutCurrent(1)`, Mido 137); 141 Space_Battle_Medium is the time jump's arrival only.
- Not yet: the generic event volumes of the older SFX; the loop waves' seams (many SFX loops end some samples past their clean loop point, a click per loop: Engine_Freighter_02, Engine_09).

## UI and platforms

- **UI Toolkit only** (runtime): shared theme `UI/GoF2Theme.tss`; per-screen UXML/USS; controllers in `Scripts/Runtime/App/UI` (`GoF2Remake.UI`). The theme doesn't include Unity's default runtime theme, so built-in controls are styled in `GoF2Common.uss` and the document root is stretched in code.
- **Landscape only** (portrait disallowed in Player Settings). Supported from 4:3 to 32:9 and phones: the panel scales to screen height (reference 1920x1080, 1600x900 on phones), UI stays inside a centred max-21:9 safe area, `layout-narrow` / `layout-phone` classes adapt the layout, device safe-area insets are applied. The phone test reads `UnityEngine.Device.Application.isMobilePlatform` (PR #108),
  so the Device Simulator shows the phone layout (`UnityEngine.Application` stays the Editor there); the same in a build. Cameras use `Aspect.VerticalFov` (Hor+ on wide screens, never narrower than 16:9); the bars `Aspect.VerticalFovKeepWidth` (wider screens keep the 16:9 width and lose a little top and bottom: the bars are only modelled as far as the original's camera sees); the hangars are back on Hor+ (`StationLevel.SetLens(..., keepWidth: false)`): a new hangar model is being made, so their cut-off edges on 21:9 / 32:9 are left for it.
- **Keyboard** (flight): the PC / Mac version's defaults (Galaxy on Fire 2 Full HD: the PCGameControls wiki list, and the PC help texts in the text table: 17 / 18, 587, 638, 1731 / 1732, 1752, 1754, 1879 with their `#KEY_*` tokens, `FlightHints.KeyTokens`): arrows steer, W booster, S brake (the PC version's "Brake" 3361: the engines stop while held, the throttle is kept; a boost overrides it), `]` / `/` (and the wheel) thrust, Space / LMB primary, R / RMB secondary, A / D strafe (held; the PC binding screen's 3350 / 3351 "Strafe left / right", `PlayerEgo::strafe` 0xad838, never called on the phone: sideways speed ramp x min(H' x 0.06, 2) u/ms, the ramp 0.1 x1.5 a frame, x0.7 a frame after release, the camera moves with it; the dodge keeps the touch swipe and the right-stick flick and can be bound), 1 / 3 roll ("Turn left / right", remake roll: the phone original only auto-levels; touch: the Level button slid sideways), F the action prompt (Dock: dock, the autopilot to a locked station, the planet jump; Enter too), Q autopilot (the target list, again = off) and E actions (the quick menu), 1-8 pick a menu entry, Tab fast-forward ("Speed up"), T view, V wingmen, K Khador Drive, C cloak, M / middle mouse toggle mouse steering, Esc pause. Strafe works on the autopilot too (remake). Remake keys: 2 level out, G switch secondary, U unlock target (see "NPCs and combat", Radar), X time extender, Y turret auto fire, F1 toggle HUD (Full / Minimal / Off, see **Options**), F12 a screenshot anywhere (`ScreenshotKey`, saved like the photo mode's: PC Pictures/Galaxy on Fire 2, Android MediaStore). The phone binary has no flight keys (`MGame::OnKeyPress` is empty). These are the defaults: every flight
  control is rebindable (see **Key bindings**).
- **Key bindings** (remake, `GameControls` + `BindingRow`; their own Options tab "Key bindings" (`OptionPage.Bindings`) in the main, pause and station menus, the panels wide enough for its six tabs): the flight
  controls are one code-made InputActionMap ("Flight", always enabled; chat typing and a capture suspend it,
  `GameControls.Suspend`) of 38 rows (steer, throttle, brake, boost, roll, level out, strafe left / right, dodge left / right (no keyboard default), fire, fire secondary,
  switch secondary, camera / turret view, auto turret, unlock target, action, autopilot menu, actions menu, fast-forward, wingmen, Khador Drive, cloak,
  time extender, mouse steering, mining drill (read only while drilling, so its keys may overlap the flight's), chat, chat send, chat channel, screenshot, toggle HUD, multiplayer window (N), distress call (unbound)), each with two keyboard / mouse slots and a controller slot (binding
  groups Keyboard / Gamepad; steer's controller slot is a whole stick or the D-pad so the stick keeps its radial dead zone,
  the other controller slots also take a stick pushed one way past half way, "RS ↑";
  steer, throttle and roll are composites captured part by part; a later part can't take what an earlier part of that slot took, #24: the stick still pushed left for Roll's "left" was taken again for "right"). Defaults: the PC keys above and the controller buttons
  (LS steer, RB / LB throttle, A boost, Y level out and fast-forward (hold), RT / LT fire, D-pad right / up / down switch
  secondary / camera / auto turret, B unlock target, X action, View autopilot menu, D-pad left actions menu, RS / LS press cloak / time extender, RS ← / → dodge: a binding like any other (it was a fixed flick that the cloak's
  right-stick-press binding switched off; a stick binding is ignored in free look, where the stick turns the camera). Overrides in PlayerPrefs `controls_bindings`
  (by action name and binding index: the Input System's own JSON finds bindings by id, new every launch for code-made ones, so
  nothing saved applied after a restart; an empty override unbinds); "Reset key bindings" and Default settings (497) clear them. A
  capture (`GameControls.Rebind`, `PerformInteractiveRebinding`): Esc cancels, Backspace / Delete (or a right click on the
  cell) unbinds (a controller cell's capture takes the keyboard's Backspace / Delete too); remake (players couldn't find it, and a controller couldn't clear one): a bound cell's × (on hover, on the selected row's selected cell, always on touch) and Delete / the controller's X on the selected row clear the selected cell without a capture (`BindingRow.ClearSelected`, polled by the row itself in all three menus), the controller's Menu can't be taken; input of another kind cancels too (a keyboard cell: the controller's
  Menu / B or a tap; a controller cell: a mouse click or a tap; `CancelFromOtherDevice`), and so do 10 s without a match
  (a keyboard cell picked with a controller or on a phone waited for good with the menus blocked: a softlock); on a phone the
  keyboard cells only take a capture once a real keyboard was used this run (`InputMode.KeyboardSeen`; Android's back button
  arrives as Escape and doesn't count), Enter / A on a row then picks the controller cell; Reset key bindings needs no bound key; a key or button can be bound to several rows at once (no swapping:
  the controller has too few buttons for one each); meanwhile and on the frame after
  (`GameControls.BlocksMenus`) the menus ignore their keys and the panel's navigation events. Fixed on purpose: the menus'
  keys (arrows, Enter, Esc, controller A / B / Menu) and pause. The HUD hints (`InputGlyph.For`, rebuilt on
  `GameControls.Changed`; an unbound control's hint is left out), the action prompt's glyph and the hint texts' `#KEY_*`
  tokens (`FlightHints.KeyTokens`) show the current bindings, the controller's in the current button style (Xbox /
  PlayStation, see **Input modes**; the bindings stay `<Gamepad>/...` paths, so a binding is the same button on both;
  `GameControls.PadControl` also reads a capture of a pad-only control, e.g. `<DualSenseGamepadHID>/touchpadButton`).
- **Free flight** (remake, Options > Controls toggle "Free flight (EVERSPACE 2 style)", `Settings.FlightStyle`,
  `FlightStyles`; off by default, and off is the original flight and key layout unchanged; single player and sessions, it only changes how the player's own ship flies): built
  after EVERSPACE 2 (not from its code): its numbers come from recordings of the game measured by
  `Reference/tools/es2flight/es2measure.py` and from its asset values (`Reference/research/es2_flight_measurements.md`).
  `FlightModel.StepFree` (plain C#): thrust / strafe / up-down held instead of a throttle, per axis in the ship's frame
  (`FreeAxis`): top speeds forward the original's 2 u/ms (100 m/s; EVERSPACE 2's cockpit speed gauge read 343 km/h = 95.3),
  backward, strafe and up / down x0.5 (the gauge: 172 km/h each); every axis eases out cubically to its top speed in a
  fixed time, v = V (1 - (1 - t/T)^3) (the game's EaseAccelerationExponent 3), written as a rate of the speed still missing
  so it continues from any speed: forward T 1750 ms, the others 1270 ms, at handling 100 (sqrt-scaled by the handling,
  + `AccelMs`). Turning: every axis (roll too) at
  EVERSPACE 2's 125 deg/s at handling 100 (scaled by (H / 20)^0.75, `FreeTurnExponent`: a bare Rhino 51; softly capped
  there, only a tenth of the rest counting, `FreeTurnAboveShare`: an Inflict 127 instead of 148, a Dace with the Pulsed
  Plasma Thrust 146 instead of 335, players' feedback 2026-10-16: the nimble ships whipped round; the steering
  sensitivity option x0.6..1.6, 1 at its default, read from `Settings.Sensitivity`: `FlightModel.Sensitivity` is the
  original's ramp setting, a fixed 2.7 under mouse steering, which turned free flight x1.6 too fast; x0.85 boosting, measured), reached by a linear 400 ms ramp (`FreeTurnRampMs`, measured in
  EVERSPACE 2's cockpit view, which follows the ship rigidly); speed doesn't change it. No auto-level (Level out still works). Inertial dampeners (`ShipController.Dampeners`, toggled by
  `GameControls.Dampeners`, on at start): each axis flies the speed asked for and what isn't asked for brakes away at
  33 m/s^2 (the gauge, linear on every axis; `FreeDecelMs` 3000: a stop from top speed; the asset says 60, probably
  scaled by the player's dampener strength setting), which also eases a boost's speed off afterwards; off: the thrust
  adds to the momentum, above the soft cap (top speed, the boost's while boosting) the speed eases back at that rate. A
  held boost on an energy pool: to the booster's original boost speed x 3.2 / 3 (`FreeBoostFactor`: the Me'al's x3 is
  EVERSPACE 2's measured x3.2, the gauge's 1110 of 343 km/h; Linear / Cyclotron x1.6, Synchrotron x2.67, Polytron x4.27;
  a booster without a boost speed x3.2), eased out cubically over 3.1 s from the
  top speed (`FreeBoostEaseMs`, over the gap), a full pool lasts the booster's duration (attribute 27; 3.5 s without one: EVERSPACE 2's boost lasted 4.5 s held, so its equipment sets it)
  and refills in its recharge time, restarts from 20 % and only on a fresh press after running empty (`BoostHeld`,
  `BoostRechargePercent` = the pool). The forward
  thrust held is `Throttle` (0 with no key, whatever the momentum) and the forward speed `MoveSpeed`; with no forward
  thrust and no boost the main engines are off (`FlightModel.EnginesOff` -> `EngineDrive.Off`: mod flames shrink into the
  nozzles, the nozzle / throttle glows go dark, `ShipExhaust` stops, `ShipController.UpdateEngineParts` stops drawing the
  model's player engine parts (Renderer.enabled, not the jets), `NetPlayer`'s engine flag), so a ship at rest or coasting
  burns nothing; after the launch / arrival fly-in the ship comes to rest (`ShipController.freeSettle`: braked like the
  dampeners even with them off, until the player thrusts). With the dampeners off the thrust never pushes past the soft
  cap (before, the cap's easing was weaker than the thrust and a held key climbed on for good). The sideways part of the
  move goes to `StrafeSlide`, so the chase camera stays square behind a sliding ship. `ShipController.FreeFlightActive`: the
  option with keyboard / mouse or a controller (touch, tilt and VR keep the original); the autopilot, the launch / arrival
  camera, the turret view, the dodge and computer control (mining, object docking) run the original path and hand back
  the velocity (`SyncFree`). Mods can tune each style or set the style (see "Mods", Flight styles). The mouse: EVERSPACE 2's virtual joystick (`ReadVirtualStick`: the mouse moves a reticle
  inside a circle of 0.5 x the screen height, `StickRadius`, EVERSPACE 2's measured ~0.52: the rate grows linearly with the
  reticle's offset; it stays where the mouse leaves it; beyond the mouse steering dead zone (a hard one, not rescaled, like
  the game's ~8 % of the screen height) the deflection is the whole offset over the radius (measured to 478 px: 0.229 deg/s
  per px, linear); still during free look, centred while the autopilot steers; the HUD
  draws the circle, `.mouse-stick`, faded in as the aim dot nears it (from 60 % of the radius, full at 90 %, 150 ms in, 400 ms out; always shown it read as a second radar ellipse, shown for any steering it came up for every small correction; `FlightHud.UpdateStickRing`). The chase camera follows free flight faster by `FlightModel.FreeTurnSpeedup` (the free top turn rate over the original's full-stick rate, ~2.4 for an Inflict) and keeps the turn slide at the original's size (`ChaseCamera`): tuned for the original's rates it fell behind and the ship swung to the edge of the picture. Free look while held
  (`GameControls.FreeLookHold`, `FreeLookCamera`; both styles; in free flight the aim is ignored meanwhile). Its own key
  layout (`GameControls.FreeFlightDefaults`, overrides under `controls_bindings_free`, `ApplyStyle` on the option's
  change): W / S thrust, A / D strafe, Space / L-Ctrl up / down, Q / E roll, L-Shift boost, Z dampeners, L-Alt free
  look, LMB / RMB fire, R autopilot menu, Tab actions menu, H fast-forward; controller: left stick thrust / strafe, right
  stick aims, LB / RB roll (up / down unbound), the rest as before; the original-only rows (throttle, brake, strafe
  left / right) and the free-only rows hide in the other style's bindings list (`ControlRow.visible`); the Key bindings
  page's first line names the layout shown ("Key layout: ...", `bindingsLayout`), and its Reset resets only that layout.
  EVERSPACE 2's own defaults (its Input.ini) match the layout but for Z (its photo mode; its dampener toggle has no
  default key). Verified in Play mode before the measured numbers (keyboard events and a virtual controller): thrust, the
  dampened stop, the drift with the dampeners off, strafe and up / down, the roll, the right stick's turn, the boost's
  pool, the hints and the circle; the measured numbers checked on `FlightModel` alone (125 deg/s in 0.23 s, forward / strafe
  against the gauge within 0.04 of the top speed at every sampled time, the 3.0 s stop, reverse half the forward speed). Played in Play mode with the measured numbers (2026-10-10, the Phantom, handling 150; keys held by
  re-queueing the keyboard state before every Input System update, the Input System set to ignore focus for the test): 100
  m/s forward reached as the cubic ease predicts (90 % 0.77 s, 99 % 1.12 s), stopped in 2.96 s; strafe and up 50 m/s,
  stopped in 1.48 s; the boost 209 / 306 / 320 m/s at +1 / +2 / +3 s, braking 33 m/s^2 after it; the heading turning
  153.1 deg/s (125 x sqrt(1.5), the square root of then; now 169); the mouse's yaw the offset's horizontal share of the radius x the top rate (94.1 / 55.6
  deg/s for 232 / 137 px of 377), nothing back in the dead zone.
- **D-pad taps** (`DpadTapNavigation`): UI Toolkit's runtime input samples UI/Navigate once a frame and drops a D-pad press released within the frame (the Steam controller's trackpad D-pad); the main and station menus send those taps (`wasPressedThisFrame` and already up) to the panel as NavigationMoveEvents.
- **Input modes:** `InputMode` tracks the last input kind (Touch / KeyboardMouse / Gamepad, switching on the first real input of another kind, on any platform). The flight HUD (`UI/Flight`, `FlightHud`) shows the touch controls (see **Touch controls** below) only for touch, keycap hints for keyboard and controller button hints for a controller, in the **Xbox** or the **PlayStation** style (`InputMode.Style`, remake; Options > Controls "Controller button icons", `Settings.PadGlyphs` Auto / Xbox / PlayStation, Auto by default: the controller last used picks it, `InputMode.StyleOf`: the Input System's DualShock / DualSense layouts (`DualShockGamepad` and every layout based on it, any platform) or a pad reporting Sony as its maker (or DualSense / DualShock / PlayStation in its product name) = PlayStation, anything else (XInput, Switch Pro, VR's `VrPad`) = Xbox; switched live, `InputMode.StyleChanged`, which `GameControls.Changed` passes on). Every controller glyph is a `PadGlyph` (`InputGlyph.Pad` / `For`), which redraws itself when the style changes, so the glyphs built once (dialogue buttons, Launch, the hangar's LT / RT, the mod browser's X) follow too; Xbox unchanged (coloured A / B / X / Y, LB / RB, LT / RT, LS / RS, VIEW / MENU), PlayStation dark face buttons with their symbols drawn by Painter2D (cross blue, circle red, square pink, triangle green: the UI font has no ✕), L1 / R1, L2 / R2, L / R sticks, L3 / R3 presses, OPTIONS, CREATE (SHARE on a DualShock 4, `InputMode.DualShock4`), TOUCHPAD. The text names (`GameControls.ShortName(path, style)`: binding cells, the `#KEY_*` tokens) use × ○ □ △ (Inter has them). Touch steering goes through `ShipController.SetSteer` (the stronger of touch and the built-in actions wins); touch mode also switches the chase camera to the original's fixed touch damping. Esc / Android back / controller Menu / the touch pause button open the **pause menu** (`PauseMenu`, MenuTouchWindow mode 1: 40 Pause; Resume 41, Missions 129 (remake: also in the alien orbit, where the original hides it, and with the Most Wanted criminals on the move), Cargo hold 166, Options 31 (every option but the language), Skip 395 during the prologue / rescue, Back to Main Menu 522 → 523): game time and sounds pause (`Navigation.PauseMenuOpen`, `AudioListener.pause`); after the player's death Esc goes straight to the main menu. **Action Freeze** (59, `PhotoMode`, the pause menu's state 0xd / `MGame::setCinematicMode`): the world stays frozen, the HUD hides, an orbit camera around the ship (drag −0.005 rad/px, the pitch free all the way round and clamped to ±200 px only by a fling (`MGame::OnUpdate` 0x1aec2c; the in-flight free look clamps always), ×0.9 fling, the camera turning with the orbit (no flip over the top), pinch / wheel / triggers zoom 1500..20000 units, arrows / sticks), the logo 0x534 (the Full HD logo) and a footer with Back (170); the original's share buttons are dead code (no upload). Remake: the overlay is Inspect ship's (`OrbitViewUi`, `.orbit-view-*` in GoF2Common.uss, shared by both): "ACTION FREEZE" at the top left, the controls as hints at the top right (rotate, zoom, Hide UI H / Y, Screenshot Enter / P / A, Back; the last three clickable), Back and the title screen's remake logo (`logo_gof2_remake.png`, `.photo-logo`) in the footer; Screenshot is the original's 60 "Save to library" (PNG to Pictures/Galaxy on Fire 2, Android MediaStore; 55 / 56); Hide UI clears the screen until a short click / tap or H / Y again; + / - (Page Up / Down) zoom too; the overlay fades while the camera is dragged. **Photo tools** (remake, players' suggestion "Everspace-style Action Freeze", 2026-10-17; `PhotoTools`, shared with Inspect ship): the **free camera** (F / the controller's X / its hint; `World.FreeCamera`: yaw about the world's up, pitch short of +-90 deg, moved in its own frame and up along the world's, within 5 km of the ship in flight, six ship radii and above the pad in the hangar; W A S D, E / Space up, Q / C down, Shift x4, Alt x0.25, a drag or the arrows look (the mouse toward the drag, a finger grabs), the wheel / + - / triggers zoom the lens; the left stick moves, the right looks, RB / LB up / down; touch: a pinch moves, two fingers slide) and the **Camera settings** panel at the left (Tab / View / its hint; `PhotoSettings`, kept for the run, shared by both modes; rows are `OptionControl`s): orbit / free, field of view (from the camera's own at each entry), roll, the free camera's speed, depth of field (Default = the game's / Off / On the ship = Gaussian sharp over the hull / Manual = Bokeh at a focus distance, 1 m-3 km, Gaussian on phones) with its blur, exposure (over `Settings.BrightnessExposure`), contrast, saturation, vignette, film grain, hide the ship (its renderers moved to layer 28, which the camera then leaves out: the engine parts are switched every frame), a thirds grid, a frame (16:9 / 21:9 / 4:3 / 1:1 / 9:16: dimmed bars, the screenshot cut to it) and the screenshot's size (the screen's, x2 or x4: `ScreenCapture.CaptureScreenshotAsTexture` superSize, capped so the picture stays within 16384 px, `PhotoSettings.SuperSize`; a URP single camera request lost the post-processing), Reset. After a screenshot a notice shows where it went (`PhotoMode.StoreAt` returns the path) with Show in folder (O; desktop: Explorer with the file selected, `PhotoMode.ShowInFolder`). A press that begins on the panel, the notice or a button never turns the camera, even dragged off it (`PhotoTools.PressOverUi`, also the hosts' orbit drags). The panel flies in from the left and fades (0.45 s, ease-out cubic, USS transitions on `.photo-panel` / `--open`; closed it is hidden by `visibility`). Inspect ship fades the station menu's `side-shade` / `bottom-shade` with the menu (`.station-inspecting`): they darkened the left of the view and its screenshots. The effects are a global volume of its own (layer 26, priority 1000, a profile made in code; `Assets/Settings/PhotoModeShaders.asset` keeps their shaders in builds, URP strips unused post-processing variants). The hangar hands the free pose, roll and lens to `HangarInspect` (`SetFreePose`, `RollDeg`, `FovDeg`), which keeps them through the blend back. Not in the Customize screen. **Tilt steering** (`TiltSteering`, `MGame::handleAccelerometer`; Controls: 490 Touch / 491 Accelerometer when the device has one, 492 Steering Calibration (493, calibrates on first use), the tilt sensitivity 0..1): yaw clamp(Y·2.5)², pitch 3·max(x' − cal1, Z − cal2) squared, no dead zone; in tilt mode the flight model's pitch ramps use 1.45 / 1.25 × the tilt sensitivity (the stick, keys and pads now use the plain sensitivity, like the original); the touch stick is squared per axis (`Hud::getAnalog`²); dimmed stick (alpha 50).
- **Touch controls** (`TouchControls`, `Reference/research/touch_hud.md`): the original's flight controls as UI Toolkit elements with the original images (`GoF2Hud/touch_*`, **GoF2 > Build > HUD Images**) and their own pointer events, placed like `Globals::setCoordsSteer` / `setCoordsFire` with the Android defaults S = 415 / F = 365 in the safe area (panel units = HD pixels): the left group (fast-forward / time extender and autopilot on their pill from `NavigationView`, the pill only behind a shown icon, the autopilot hidden at campaign 0 / 1; the fixed stick 0x4c1 / 0x4b6 / 0x4b7 at centre (165, S + 277), travel 94, touch area ±112 × ±216, no dead zone, a double tap within 499 ms levels out; boost 0x4b2 / 0x4b3, alpha by the charge, the ready blink (the original's 2 s of one lit frame per 80 ms strobed at 120 Hz; remake: 5 flashes in 1 s, lit the last 100 of every 200 ms, in time at any frame rate)), the right cluster (background 0x6aa / 0x4c6 at (w − 321, F); fire 0x4b4 / 0x4b5, also the action button: autopilot / planet jump / docking target / asteroid approach / undock with the arrow 0x536 when fire acts, the rest is Mining's own fire handling; a second press within 249 ms latches autofire; quick menu 0x4ba (opens the quick menu when it has entries), camera (the next mode's icon), secondary 0x4bc (hidden at 0 ammo; held 350 ms: the weapon strip, see "NPCs and combat", HUD), the auto-turret toggle), pause 0x4b8 at (w − 121, 24) in the HUD root (also during cutscenes, the launch / arrival camera and jump scenes, there at 12 % opacity, opaque while held (remake, `Frame.cinematic`, for immersion), at its last place in flight: a cutscene hides the safe area (display: none), which zeroes the touch layer's size, and the width's 1920 fallback pushed it off a narrower HUD; sounds 124 / 123; the top-right readout moves left of it), the secondary plate 0x4c2 at the bottom centre ("<name> (<n>)", remake: a tap switches). Buttons act on release over them (sliding off cancels), fast-forward while held. The empty screen behind them (`.touch-gesture-zone`): a vertical drag sets the throttle after 160 px (400 px = 100 %, up = faster) with the gauge 0x548 under the crosshair (2 s fade), a sideways flick dodges, in free look the drag orbits. No Level button, throttle slider or action prompt on touch (the original has none). The original's Configure screen (Options > Controls, 494 / 495: S and F dragged up and down) is two sliders in Options > Controls (remake, #61; shown with touch input): "Touch controls height: stick / fire buttons", `Settings.TouchLeftHeight` / `TouchRightHeight` 0..1 between the highest (300) and lowest place the original allows (h − 425 / h − 311), -1 = the defaults 415 / 365 (`TouchControls.Height01`). Hidden under dialogues, the pause menu, the star map and death; only the pause button in cutscenes, jumps and the launch camera; only the stick under the autopilot menu.
- **Flight hints** (`FlightHints`, the original's hint chain: 596 / 590 / 593 / 584 / 637 / 648 / 619 / 1725 / 620 / 621 / 617 / 636, the Nivelian 443 / 444 radio, 599-609, the volatile 3202 and red plasma 3161; keyboard variants +1, `KeyTokens`): each once (`Session.Hints`), in a ChoiceWindow that pauses the game. Remake: the tutorial popups (these, the stations' first-visit help 622 / 627 / 635 / 640, the map's 628 / 631, the hangar's 587-589, and the story conversations' Info pages that
  only teach the controls or menus, `StoryTable.Shown`: 1677 / 1680 (index 0's whole briefing), 1718, 1725, 1730, 1746, 1749,
  1750, 1753, 1816, 1862, 1878; the Info pages with story content (1782, 1896, 191 / 192, 2171, 2184) always show; not the
  medal hints) are an option, off by default (`Settings.TutorialHints`, Options > Gameplay "Tutorials"); a new game's Game options panel has it as a toggle (`MainMenu.RefreshTutorialToggle`; it was a question after the economy); while off nothing is marked shown.
- **HUD readout** (`HudReadout`, top right, `GoF2Hud/hud_*`): the level timer, cargo "load / max t", passengers (0xb8, the counter against the cabins), goods (0xae), the kill score (Challenge / 36) and the volatile bar.
- **Camera modes** (`FreeLookCamera`, `MGame::switchCamera`): T / D-pad up / the turret button cycle standard, cockpit, turret (manual turrets) and free look (217 / 219 / 218 / 220): an orbit around the ship (−0.005 rad/px, pitch ±200 px, zoom 1500..20000, middle mouse / right stick / touch drag); no locks or tractor in the turret view.
- **Cockpit view** (remake, `FreeLookCamera.Mode.FirstPerson` = the original's mode 2, `TargetFollowCamera::enableFirstPersonCam`, which `switchCamera` / `nextCamId` always skip; its text 219 "Cockpit" and the camera button's eye icon 0x4be / 0x4bf, `GoF2Hud/touch_cam_2(_on)`, were already in the data): `ChaseCamera.firstPerson` puts the camera rigidly on the bow with the root's rotation (the guns' line, so the crosshair stays at the centre): `ChaseCamera.FirstPersonSeat` from the hull's meshes (`PlayerHull.HullBounds`, no glow layers; game ships, mods' glTF and kit builds, the debug capital hulls), on the guns' line (the pivot, which the crosshair, locks and beams aim from) a fifth of the half height above the middle but at most 1.5 m off it (a capital hull's 100 m dropped the crosshair 5 deg), 0.8 of the way to the bow (a hull over 300 m long 0.9) but behind the frontmost primary gun (not behind half way), so the muzzle flashes and shots start in view; the crosshair, the lock boxes and the beams' auto-aim take their aim line from the cockpit camera meanwhile (`ChaseCamera.AimOrigin`: a capital hull's bow camera sat past the pivot's 1.1 km aim point, which hid the crosshair); the model's renderers on layer 28 (VR's / Action Freeze's hidden layer, rescanned every 0.5 s and on a hull swap: turrets, kit weapons), the near plane 0.3 m; the hit shake and the explosions' / boost's rumble as small turns of the view (the original keeps x0.001 / x0.0015 of them), the boost's FOV at half; the FOV option as everywhere. Whatever takes the camera over turns the chase camera off (launch / arrival, cutscenes, jumps, mining's landing, object docking, death, Action Freeze): the hull shows and the camera starts from behind the ship (`ChaseCamera.OnDisable`, not inside a tumbling bow), and it comes back in the cockpit when the chase camera does; the turret view, free look (also the held key) and the Liberator leave and return to it. The last of standard / cockpit the camera button reached is kept for the next flights (PlayerPrefs `flight_camera_cockpit`, `FreeLookCamera.Base`); a turret view or free look ending by itself goes back to it. The dodge works in it (`FreeLookCamera.ShipView`). Not offered in VR (it flies from the seat). Local only: multiplayer is unaffected.
- **Free look with the mouse** (remake): with mouse steering the cursor stays captured in free look and plain mouse movement
  orbits the camera (`FreeLookCamera.mouseLook`, the mouse delta), the ship's mouse steering pauses; the middle button held
  works without mouse steering. Hangar tabs: Q / LB left, E / RB right (both used to go right).
- **Controller gyro** (remake option, Windows, off by default; `ControllerGyro`, Options > Controls "Controller gyro" + "Gyro sensitivity"): DualSense / DualShock 4 / Switch Pro / Joy-Cons through JoyShockLibrary 3.0 (MIT, `Assets/Plugins/JoyShockLibrary/x86_64`, Editor + Windows x64 only; its notice on the About page): tilt steering from the accelerometer (gravity, like the phone's `TiltSteering`): tilting forward / back pitches, rolling like a steering wheel steers sideways, by the angle from the rest pose taken on connecting and on Level out (pitch in the Y-Z plane, roll as gravity's lean toward X, so any grip angle works; both reading conventions cancel against the rest pose), a full offset at 20 deg / sensitivity, a 2 deg dead zone, added to the other steering. The first version integrated the gyro's turn rate in player space: the uncalibrated bias walked the pitch while the controller lay still and the max-of-both rule held the stick off (#35). Steam Input / DS4Windows on for the game hides the motion data (a reading without gravity is logged once). The device scan (JslConnectDevices, ~360 ms) runs on a worker thread every 3 s while no motion controller is connected (#37: on the main thread it stuttered manual flight; the autopilot skips the gyro). Not tested with a real controller yet (the axis signs come from JSL's documented frame; the invert options cover a flipped axis).
- **Haptics** (remake option, Options > Controls "Vibration", `Settings.HapticsIntensity` 0..1, 0 = off, default 100 %; the
  original has none: `Engine::Vibrate` is an empty stub and `Globals::init` calls `VibrateEnable(false)`): `Haptics` (created
  by `Bootstrap`, kept for the run) sends to the controller last used while `InputMode` is Gamepad (the Input System's
  `SetMotorSpeeds`: XInput, DualShock 4 / DualSense; a no-op where the platform has no rumble) or to the phone while it is
  Touch (on Android also while it is Gamepad, #73: the Input System's Android backend sends no controller rumble, so the
  device's motor stands in, the one built-in controls of handhelds like the Konkr Pocket FIT have) (`PhoneVibrator`, Android `Vibrator` over JNI: `VibrationEffect.createOneShot` with the amplitude when the motor
  has amplitude control, else shorter buzzes for weaker pulses; `AndroidVibratePermission` (Editor) adds the VIBRATE
  permission to the Gradle manifest). `HapticMixer` (plain C#) mixes the pulses (held for the first quarter, then a linear
  fade; the strongest per motor wins) and the frame's continuous rumble. Pulses (`Haptics.Play` with the presets in
  `Haptics`): primary shots (controller only), secondary launches, a ship lock, player hits by layer (shield / armor /
  hull), ramming an asteroid or a landmark, the boost's start, the hull giving out and the ship's explosion, gate /
  Khador / planet jumps, mining (landing, each ton, each layer, the drill off target every 180 ms, won / lost). Rumble
  (`Haptics.Rumble`, each frame): ChaseCamera's rumble before the camera shake option (explosions, the Liberator, the
  boost), CutsceneCamera's (Rumble × amplitude / 50), scraping a landmark (0.35), the wormhole's pull, the Khador charge,
  the drill on target; on the phone only from 0.2 (overlapping 110 ms one-shots). Everything stops while
  `Navigation.InputHalted`, `AudioListener.pause` or without focus (`StopAll` on focus loss, pause and destroy, so no
  motor keeps running after Play mode); the slider's preview (`Haptics.Preview`) plays in the pause menu too. Motors run on
  the real clock. Not tested on a device yet.
- **Haptics fix (#24)**: a stop is always sent (`HapticMixer.ShouldSend`; the 0.01 step left a fade's last few percent running
  on the Xbox until the guide was opened) as an explicit zero, and the motor speeds are sent again every 0.5 s while running
  and for 2 s after a stop.
- **Mouse steering** (remake option, desktop only, `ShipController.mouseSteering`; M or the middle mouse button toggles it in flight, the PC version's text 18): the cursor locked and hidden, the virtual offset (±0.7 half screen, sensitivity 2.7) steers like the stick; remake: a round dead zone around the centre (Options > Controls "Mouse steering dead zone", `Settings.MouseDeadzone`, 0-30 % of the vertical range, default 8 %; the original has none) where the ship flies straight, the steering rescaled from 0 at its edge to full at the limit (`ShipController.ReadMouseSteer`, `MouseInDeadzone`); a small dot marks the offset once it leaves the dead zone (one crosshair only). Turned off, the PC version's cursor mode (`Globals::mouseCursorActivated` 0, the manual's "Mouse control menu / ship"; `FlightHud.cursorMode`, `TouchControls.Frame.cursor`): `Hud::draw`'s buttons show and take clicks (the `Globals::iPad == 0` path and the Full HD manual's screenshot): no stick or gestures, the cluster from the bottom-right corner (0x6aa at (w − 397, h − 406)), the autopilot / fast-forward pill and the boost at the bottom left, the pause button top right (the cargo readout left of it); the fire button's arrow replaces the action prompt and the hint column hides; a click on a button isn't also the mouse's fire / missile binding (`WeaponSystem.mouseOverControls`). Handling-dependent camera damping only for keyboard / mouse. Losing focus on mobile (or without `runInBackground`) opens the pause menu.
- **Difficulty** (`Session.Difficulty`, options+0x2c; the new game's difficulty panel): Easy 0 / Normal 0.5 / Hard 1 /
  Extreme 1.5, the PC / Mac Full HD version's four (texts 518-520, 25); the phone menu offers only Normal and Extreme, but its
  code still handles all four. `Session.DifficultyFactor` = the original's `x + x·(difficulty − 0.5)` (×0.5 / 1 / 1.5 / 2):
  NPC hulls and gun damage (`NpcTables`), raider counts, pirate-base guards `int((d − 0.5)·5 + 5)` (2 / 5 / 7 / 10), the
  Wanted, the Kaamo / pirate outposts, the hull-270 missions, the freelance enemy counts; "difficulty < 1" (a secure system's
  1-2 raiders: Easy / Normal) and "> 0.7" (Escort / Intercept freighters ×1.4: Hard / Extreme); the cloak cooldown 5000 /
  7000 / 9000 / 12000 ms; Loma's toll 2 / 5 / 10 / 20 %. `Session.IsExtreme` (`Status::hardCoreMode`, == 1.5) keeps the
  Extreme-only rules (economy, stock, mining, standing, energy cells, hit cube 650, medals, fines, self-damage). The save
  slot rows show the difficulty. Remake: the pause and station menus' Options > Gameplay has a Difficulty row (`OptionDef.inGameOnly`, not in the main menu or multiplayer) that changes the game in progress; spawns and the Extreme rules follow from the next orbit or docking.
- **Save games** (`SaveGame`, the original's RecordHandler slots; v12 records the mods, see "Mods"): one JSON file per slot in `Application.persistentDataPath/Saves` (remake format, `SaveData`, versioned); slot 0 = auto-save on docking (`ModStation::autosave`), 1..11 manual. Only saved while docked, so loading opens the Station scene. The main menu's Resume loads the newest slot, Load lists the slots with the original's preview fields; game over reloads slot 0. Manual saves: the station's system menu (Menu 172 via the Menu button, Esc on the main view or the controller's Menu: Save game 30 with the slot list, slot 0 refused with 487, overwrite asks 49, then 50; Back to Main Menu 522 / 523).
- **Original game saves** (remake; the format `OriginalSave` + `OriginalRecord`, the mapping `OriginalSaveConvert`, the files `OriginalSaveTransfer`, all GoF2.Game; `OriginalSaveTests`, `OriginalSaveTransferTests`): the Android original's `gof2_save_game_N` (+ the unsigned `gof2_save_game_preview_N`) in `Android/data/net.fishlabs.gof2hdallandroid2012/files`, `RecordHandler::recordStoreWrite` 0xdf760 / `recordStoreRead`: raw little-endian fields (int 4, bool 1, long 8, float 4, strings an int length + UTF-16LE), the later versions' fields after the int 0x6e6a78, blueprints' invested amounts without a count (the product's recipe length, Android and Default economy differ: `OriginalSave.IngredientCounts`), and a SHA-256 signature over the data, `addHash`'s 25-byte salt and the key `g_android_origami_super_club` (the community build: "9506fe987e36474d"; Fishlabs' store build: the device's android_id, so those saves are tied to a phone; `recordStoreRead` refuses a wrong one, `OriginalSave.Read` doesn't check, `Write` signs). Checked against the decompile and the community tool gof2edit's layout (AGPL, not copied). A PC Full HD save (VGTimes' "all medals" save, 2016; same file names in `%APPDATA%\Galaxy On Fire 2 Full HD`) is the same stream unsigned with its own tables (109 stations, 196 items, 36 medals, 17 blueprints, Default economy, no 0x6e6a78 fields): both its slots and previews read and wrote back byte for byte (not in the repository: a third party's file). 965 real Android saves (XylonDrag's GitHub mission / fast-start saves for the community builds, RE-FHD and OG, steps up to 162; not in the repository, no licence) all read to their last byte (964 Android economy, 1 Default); the 773 signed with the community key wrote back byte for byte, the other 192 (`gof2_save_game_-1` / `_1-1`, no previews, presumably the store build's device-ID signature) differ only in the signature; all 773 previews byte for byte. **Import / export** (the Load panel's second row, "Import original save" / "Export for original game", hidden without a file picker, `ModImport.Available`; `MainMenu.PickOriginalImport` / `AskOriginalExport`): import picks one `gof2_save_game_N` (`ModImport.Pick`), `OriginalSaveConvert.Read` tries signed / unsigned x Android / Default recipes and takes the reading that ends on the last byte and looks sane (`Plausible`), `ToSave` maps it (the campaign from the index: 84+ Supernova, 45+ Valkyrie; the station the stack's current one; the story mission rebuilt from the step with the save's counters; the freelance mission, blueprints, Wanted board, Kaamo storage, medals' counters, Status+0xc8.. stats, known prices; the bar visitors re-rolled; the save's economy), checked by `SaveGame.TryParse` like an imported save, then a question with the station / step / credits and which game wrote it, written into the first empty manual slot (1-11; none = "delete a save first"). Export (`OriginalSaveTransfer.Export`) writes every slot as `gof2_save_game_N` + `gof2_save_game_preview_N` (same slot numbers) into `Transfer/Original saves <date>` signed with the community key (`ToRecord`: Android item / station tables, equipment past the stock slot counts (the cloak bay, stacked upgrades) to the hold, one of each Kaamo mod, the 27 story agents, mod items left out, a note for each): they load in the community builds, not in Fishlabs' store build (device-ID signature). Checked: all 967 real saves (773 community, 192 store-build, 2 PC) convert and pass TryParse; three imported (a Valkyrie loaner at 56, Supernova 128, the PC save) loaded docked in Play mode without errors; an export of four slots read back signed with every field matching. Not yet: an exported save loaded in the original game on a phone.
- **Deleting saves** (remake, players' suggestion; `SaveSlotRow`, the main menu's Load list and the station's Save / Load lists): holding a used slot for 3 s (`SaveSlotRow.HoldSeconds`; a tap / click held without dragging, or Delete / the controller's X held while the row is selected) fills it red from the left ("Hold to delete...") and deletes it; a line under the list says how (`AttachHint`, by input mode); a key must be let go before the next row can start (the selection stays on the emptied row); the click that ends a hold doesn't load / save (`HoldUsed`). `SaveGame.Delete` moves the file to `Saves/Deleted` (time-stamped, the newest 20 kept) so a slip can be undone by hand.
- **Save export / import** (remake, `SaveTransfer`, main menu only: the Export saves / Import saves buttons under the Load game list, `MainMenu`; the list is rebuilt after an import; without a file dialog a note under them names the Transfer folder): Export writes every slot's file unchanged into one `.gof2saves` JSON (format tag, format version, game version, the slots). Import replaces every slot: the file is read and checked whole first (`SaveTransfer.Check`: format, at most 12 unique slots 0-11, each save through `SaveGame.TryParse`: version 1..current, campaign, economy, finite times, difficulty 0-2, story index 0-162, and every station / ship / item / system index it names, cargo, equipment, Kaamo storage, the parked ship, shop memory and bar visitors, blueprints, production, the Wanted board, against the game's tables), then a warning that it overwrites ALL saves (the auto-save too); only Yes writes, after checking again: the new slots to `.import` files, the old ones moved to `Saves/BeforeImport` (put back if writing fails), then the new ones into place. Windows and the Editor pick the file with the system dialog (`FileDialog`: comdlg32 / EditorUtility); Linux and Android use a Transfer folder (`~/Documents/GoF2 Remake`; on Android the app's `files/Transfer`, reachable over USB) and import the newest `.gof2saves` there.
- **A finished game imports** (#117): `SaveGame.TryParse` accepts -10 as the invaded system / station (`Story.OnDepart`
  writes it once the game is won: nothing attacked any more); every finished game's auto-save was refused.
- **Save folder** (remake, a player's request: Android's Transfer folder in Android/data is out of other apps' reach;
  `SaveFolder` + `SaveFolderPicker` (GoF2.Game), the "save folder" part of `MainMenu`; Options > Gameplay "Save
  folder: ...", a Button row with `OptionDef.menuOnly` (not in the pause / station menus), default none = unchanged): the
  slots and `ship_presets.json` are mirrored into a folder the player picks, for file managers and sync tools (Syncthing).
  A mirror, not a move: the game always reads and writes its own Saves (`GamePaths.Data`, so the tests' sandbox and the
  caches are untouched) and `SaveFolder.Sync` keeps the folder in step after every save / delete / hardcore death / import
  (`AfterLocalChange` in `SaveGame`, `SaveTransfer`, `OriginalSaveTransfer`, `ShipPresets`, which reads its file again on
  `SaveFolder.Revision`), on main-menu entry and before the main menu's and the station's save lists. Rules per file against
  the hash both sides had at the last sync (`save_folder_sync.json` beside Saves, with the files' sizes / times so unchanged
  ones aren't read): one side changed it = that side wins, one side deleted it = deleted on the other (a local copy to
  Saves/Deleted as `_removed`), both changed or no common past = the newer `savedAtUtc` (other files the file time) wins and
  the loser goes to Saves/Deleted as `_conflict` (newest 20 kept); a pulled slot's file time is its `savedAtUtc` (Resume's
  newest slot). Choosing a folder (`Inspect` / `Choose`): an empty one asks Copy them / Start empty (this device's set
  aside in Saves/BeforeFolder/<time>), one with saves Keep the newer / Only the folder's; "This device only" (`UseDefault`)
  keeps everything. An unreachable folder (unplugged, deleted, grant revoked: `LastProblem`) never blocks a save: a notice
  once per run, the row's description, and the next sync catches up; a missing folder is never created (a drive's mount
  point). Android: Storage Access Framework (`Plugins/Android/SaveFolderActivity.java`, ACTION_OPEN_DOCUMENT_TREE +
  `takePersistableUriPermission`, its static list / read / write / delete over the ContentResolver as UTF-8 text; registered
  by the Editor's `AndroidModImportActivity`), no manifest permission; MANAGE_EXTERNAL_STORAGE (All files access) was
  rejected: Play-restricted, a settings trip, and plain paths can't read other apps' files without it. iOS: the document
  picker in folder mode (`Plugins/iOS/SaveFolderPicker.mm`), the folder kept as a security-scoped bookmark (target
  `ios-bookmark:<base64>`), opened again for each sync and then plain file calls. UWP: FolderPicker, the folder in the
  FutureAccessList under a token of its own per pick (`uwp-folder:<token>`), its files through Windows.Storage (waited on
  from Unity's app thread). Desktop: a plain folder (Editor OpenFolderPanel, Windows SHBrowseForFolderW on an STA worker
  thread, Linux zenity / kdialog, macOS osascript). Not mirrored: Mods, the Transfer folder, mod_registry.json (`ModSaves.Fix` renumbers by key). Tests
  `SaveFolderTests` (a sandbox folder as the other device; `SaveFolder.TestTarget` / `StoreOverride`, never the player's
  choice while `GamePaths.Override` is set). Verified in the Editor (Windows); not yet run on Android, iOS, UWP, Linux or
  macOS.
- **Hardcore** (remake, permadeath; the new game's toggle in the Game options panel after the economy (`MainMenu.PickEconomy` -> `gameOptionsPanel`: the Kaamo Club, Hardcore, Tutorials, then Start game, amber like the station's Launch (`.choice-button--start`), with the difficulty and economy picked), `MainMenu.hardcoreNext`, off whenever a difficulty is picked; not the original's "hardcore mode", which is Extreme): `Session.Hardcore` with `Session.RunId` (a new id at every new game; save v15 `hardcore` / `runId`); it saves only to the auto-save (`SaveGame.Save` refuses other slots, the station hides Save game); the player's death (`FlightHud.OnGameOver`, not in sessions or with an event's respawn point) deletes every slot of the run at once (`SaveGame.DeleteRun`: closing the game on the death screen keeps nothing) and Tap returns to the main menu; mission and quest failures still reload the auto-save; the slot rows say HARDCORE.
- **New Game+** (remake, GitHub #4, `NewGamePlus`): with a finished game in the slots (main game past 44, Valkyrie past 83, Supernova at 162; the newest one) the campaign panel shows a "New Game+" toggle (off each time it opens). Turned on, the new game (after the campaign's first step) gets the old credits added, the unlocked blueprints and the medals (the better grade of each), the Kaamo Club owned with the old storage plus the old ship (its mods), its equipment and cargo (not the story's unsaleable items) parked in it; story, map, standings and the starting ship are new.
- Text comes from the original table via `Localization.Get(textId)`; remake-only strings via `Extra(key, english)`, translated per language in `Resources/GoF2Localization/extra_<lang>.json` (a { key: text } object, loaded with the table by `Localization.LoadExtra`; 11 languages, KiritoJPK, PR #54, then Chinese and Hindi; a key it lacks stays English: add new keys there when adding Extra texts). **Languages in the menu** (`MainMenuBuilder.Languages`, written into the main menu scene's `MainMenu.languageCodes / Names / Tables`; the station scene and `Resources/GoF2LanguageTables` take every `text_*.json`): English, German, French, Spanish, Italian, Dutch, Polish, Russian, Portuguese (Brazil), Simplified Chinese, Hindi. The UI fonts (Serpentine, Inter) have no CJK or Devanagari glyphs: those come from the system's fonts through TextCore's OS font fallback (`TextSettings.fallbackOSFontAssets`), with correct Devanagari shaping (verified on Windows; Android has Noto CJK / Devanagari; Linux and the Xbox depend on the fonts installed). The Japanese, Korean and Arabic tables exist but aren't offered (not checked in game). Settings in `Settings` (PlayerPrefs), menu choices in `Session`.
- **Story step label** (remake, `StoryStepLabel`, installed by `Bootstrap` like `FpsCounter`, its own panel; only with Options > Gameplay "Show story step", `Settings.ShowStoryStep`, on by default since 2026-10-11 (was off, #40); a player who turned it off keeps it off): bottom right in
  every game scene (not the main menu, not in photo mode: `FpsCounter.Suppressed`), small and half-transparent, never
  picking: "Step 23: <StepSummaries title> · <station> #<index>" (the add-ons prefixed with the campaign, "Free play"
  without a story), refreshed twice a second, so a bug report's screenshot shows where the player was.
- **Options** (`Settings`, PlayerPrefs, cached): `OptionsCatalog` lists every option (label, kind, range, get / set) and both the main menu's Options panel (tabs Sound / Graphics / Controls / Gameplay / Language, rows built at runtime) and the in-game ones build their rows from it (`OptionControl`: Slider, Toggle or `ChoiceRow` segments / stepper). The pause menu's and the station system menu's Options page is `OptionsView`, the same panel in code (title + amber accent, the tabs, Back / Default settings) with the main menu's classes, shared in `GoF2Common.uss`; Q / E or LB / RB switch tabs, left / right on the tab row too. Original options: Music / FX / Voice, Brightness (513-515), **Quality** 504 (Low / Medium = smoke off (`ShipSmoke`), fog off (`Bootstrap.SetSceneFog`), LOD bias ×0.35 / ×0.6), Sensitivity, Invert (500, remake: split into up / down and left / right, and the mining drill's own pair), Default settings 497 (remake, players' request: it asks first whether to reset only the tab on show or every tab, or Cancel: the main menu's dialog with a third button (`ShowResetChoice`), the in-game `OptionsView` in its footer's place, Esc / B cancels; the keys per tab are `Settings.SoundKeys` / `GraphicsKeys` / `ControlsKeys` / `GameplayKeys` / `LanguageKeys`, `OptionsCatalog.ResetPage`; a new option's key goes in its tab's list: the old single list had missed 19 later options). Remake: master volume, window mode + resolution (desktop players only), frame rate, render scale, upscaler (Off / FSR 1 / STP / DLSS / FSR 2-4 / MetalFX / MetalFX Temporal, only those the device supports: FSR needs shader model 4.5, STP compute and no GLES, so Vulkan on Android; DLSS / FSR 2-4 see below; the temporal ones force MSAA off) and its quality (DLSS / FSR 2+: Native = DLAA / native AA, Quality, Balanced, Performance, Ultra performance; they pick their resolution from it, not the render scale) and MSAA (the URP asset; 0 = the platform's own), bloom (Off / Remake: URP Bloom on the HDR glow, threshold 1 / Original: `ClassicBloomPass`, see below / Both: the two together, #45), hangar depth of field (see "Station scene"), lens flare, hangar ship shadows (Off / Player ship only / All ships, see "Station scene"), field of view (chase camera, default the original's 1.22 rad), camera shake, vibration (see **Haptics**), stick dead zone, launch / arrival camera, hangar arrival and take-off (`HangarFlight`), auto-play dialogue (the dialogue's AUTO pill), animated dialogue (`TextReveal`), button hints in flight (the flight HUD's hint row, `InputGlyph.TrackHintsOption` / `.hints--off`, whose rule must outrank the HUDs' `.input-keyboard .hints`), the flight HUD (Gameplay tab, `Settings.HudMode`, players' request "for maximum immersion": Full / Minimal / Off, also the Toggle HUD binding, F1, which steps through them with a HUD message; `FlightHud` puts `.hud-minimal` / `.hud-off` on the root, FlightHud.uss hides by `visibility`, since the markers' layers, the status panel and the readout set their display in code: Minimal hides the status bars, the radar ellipse, every marker (ships, landmarks, missiles), the lock plate, the readouts, the hints, the cooldown icons, the hit arcs and, with keys or a controller, the throttle gauge / secondary plate / weapon strip; Off also the crosshair, lock ring, action prompt, missile warning, charge bar, mouse reticle / stick circle and the arrival's orbit information; always kept: the HUD messages, the radio, conversations, the menus, the mining and hacking games, the touch controls (and on touch their secondary plate) and game over; the lens flare is its own option), Show FPS (`FpsCounter`, its own panel over every screen, top centre), every weapon's own shot sound (`Settings.EachWeaponSound`, Sound tab), mute in background (desktop, `Settings.MuteInBackground`: `Bootstrap` zeroes the listener volume while the window is unfocused), double-press fire for auto-fire (`Settings.KeyAutofire`, off by default: the touch fire button's 249 ms latch, `AutofireLatch`, on the keyboard / mouse / controller fire binding; the PC version had none), the voice language (the Language tab under the language buttons: As text / English / Deutsch, `Settings.VoiceLanguage` / `GermanVoices`; every voice goes through `StoryAssets.Voice`). `Bootstrap` applies the process-wide ones on every change (and bloom / exposure to every scene's global volumes on load) and restores the Editor's values when Play mode ends. The PC and Mobile URP assets are saved with STP selected on purpose: URP strips STP's compute shaders from a build unless a pipeline asset uses it (`STPResourceStripper`); at runtime the option replaces it (off = URP's automatic filter; on Android FSR 1 only below 100 %, #45: at 100 % or more part of the screen froze). `UrpStpGuard` (Editor) puts STP back on the project's URP assets on Editor load, whenever Play mode ends (a recompile during Play mode skips `Bootstrap`'s restore) and before every build; at runtime `Bootstrap.StpSupported` also needs `STP.RuntimeResources` in the build (checked by reflection), so a build without them hides STP instead of freezing. **DLSS / FSR 2-4** (`UpscalerFramework`): URP's upscaler framework behind the undocumented `ENABLE_UPSCALER_FRAMEWORK` define (Player settings, Standalone only; Android keeps the asset's upscaling filter) on the built-in modules `com.unity.modules.nvidia` / `com.unity.modules.amd`. With it compiled in URP ignores the old upscaling filter: every upscaler choice goes through `UniversalRenderPipeline.SetUpscaler` (Off = `unity.auto`, `amd.fsr1`, `unity.stp`, `nvidia.dlss4`, the newest of `amd.fsr4` / `fsr3` / `fsr2`), set again on `RenderPipelineManager.activeRenderPipelineCreated`; the quality mode on the framework's options; device support from the framework's own `isSupportedOnDevice` (read through the internal `UniversalRenderPipeline.upscaling`; DLSS: RTX, D3D11 / 12 / Vulkan; FSR 2 D3D11 / 12 / Vulkan; FSR 3 / 4 D3D12, FSR 4 AMD's newest GPUs; Windows builds only; only known once URP has made its pipeline at the first frame, after the main menu built its rows: the Upscaler row asks again on every refresh, the Upscaler quality row shows only while DLSS / FSR 2+ is the active upscaler and the Render scale row only while it isn't (`OptionDef.visible`; they pick their own resolution), and the main menu refreshes its rows when Options opens). `UrpStpGuard` keeps every URP asset's upscaler priority list (STP, DLSS, FSR 4 / 3 / 2, FSR 1, Auto) so `isStpUsed` holds and nothing is stripped; the scaling mode stays None (the Editor's views render plain). The PC asset's opaque texture is full size (opaque downsampling None: FSR's reactive mask warned every frame with 2x). Verified in the Editor on an RTX 5070 laptop (D3D12): DLSS (all modes) and FSR 3 in the menu, station and flight; no frame generation. **Context leak** (`UpscalerFramework.FlushContexts`, hooked on `endCameraRendering` by `Bootstrap`): the framework keeps one DLSS / FSR context per camera (~190 MB at 3440 x 1440) and drops it after 400 frames unused or on a resolution / quality change, but `UniversalRenderPipeline.Render` records those DestroyFeature calls after the frame's last Submit and never submits them, so every new camera (the star map, the item window, each scene's) and every camera off for 400 frames (the level's behind the map) leaked its context: a player build reached 7 GB in 15 map visits (reported on 50-series cards, measured on a 4080 at 143 Hz). The hook runs the cleanup after each camera and submits it; verified in a build: flat over 12 map visits, memory back on leaving the station. **MetalFX** (macOS and iOS): the core package's own `apple.metalfx-spatial` / `apple.metalfx-temporal` upscalers, compiled in with the built-in module `com.unity.modules.metalfx` (manifest) on macOS / iOS targets with the framework define (iOS: `IosUpscalerDefine` (Editor) keeps `ENABLE_UPSCALER_FRAMEWORK` in the iOS define symbols on Editor load and stops an iOS build that lacked it; on iOS the framework offers Off / FSR 1 / STP / MetalFX, the Mobile URP asset keeps its upscaler list through `UrpStpGuard`; iOS minimum 16.0); options 5 / 6 (`Settings.UpscalerMetalFxSpatial` / `Temporal`), offered only when `isSupportedOnDevice` (Metal, macOS 13+ / iOS 16+). No quality modes: the Render scale row stays and sets their input resolution (URP clamps it into MetalFX Temporal's supported range); Temporal counts as temporal (MSAA off). Not yet verified on a Mac or an iPhone.
- **Original bloom** (`ClassicBloomPass` + `Resources/GoF2PostFx/ClassicBloom.shader`; the bloom option's "Original"): the original's only post effect (`AbyssEngine::BloomShader`, on from `MGame::OnInitialize` via `Engine::SetPostEffect(0x1400000, true)`; the radial blur 0x1400002 and glow 0x1400001 exist but are never switched on) as a URP render-graph pass after the post-processing, queued from `beginCameraRendering` on every base screen camera with post-processing (no renderer-asset change): the scene sampled into 256 × 256 through the original's gamma-space bright pass (Luminance 0.08, middle gray 0.005, white cutoff 0.014: about everything above half brightness), 6 × the "horizontal" blur (its offsets are vec2(o), a diagonal blur) and the vertical one (9 taps, texSize 256), then clamp(scene + glow) (`Engine::switchBloom` = 3 in the binary); URP's Bloom is off meanwhile. The original also blooms its HUD (drawn into the same FBO); the remake's UI Toolkit HUD doesn't.
- Frame rate option (`Settings.FrameRate`: 30 / 60 / 120 / Uncapped / V-Sync, default V-Sync) is applied by `Bootstrap.ApplyFrameRate`. Mobile is always display-synced: V-Sync there means `targetFrameRate` = display refresh rate.
- Main menu research (flow, text IDs, image rects, camera, sounds): `Reference/research/mainmenu_notes.md`.
- **Discord Rich Presence** (remake, desktop; Windows so far): `DiscordPresence` + `DiscordIpc` (the desktop app's local pipe, no SDK) for the Discord application "Galaxy on Fire 2 Unity Remake" (1555054317155647571); details / state from the scene (docked, flying, mining, combat, cutscene; the freelance mission or the story step's title, multiplayer squad), the campaign art large and the system's race emblem small (assets from `Reference/tools/discord/make_assets.py`), the timer from the game's start; Options > Gameplay "Show what I'm doing in Discord".
- **App icon** (`MainMenuBuilder.BuildAppIcons`, run by Main Menu Scene; the art `UI/AppIcon/icon_source.png`, 2000 x 2000
  with rounded corners): `icon.png` the default icon (Windows / Linux / iOS, Android legacy and round), Android's adaptive
  icon = `icon_foreground.png` (the art at 62 % inside the 66.7 % visible area, so a circle mask keeps the title) over
  `icon_background.png` (the art's middle blurred and enlarged) + `icon_monochrome.png` (its bright parts in white, the
  themed icon); UWP's tiles, store logo and splash at 100 / 200 % in `UI/AppIcon/UWP` (`PlayerSettings.WSA` visual assets:
  squares = the art, wide / splash = the art on its blur; an image over 200 KB is left out at 200 %, the UWP packager refuses
  bigger logos (APPX3207), so the 310 x 310 and wide tiles and the splash come at 100 % only). Without the art the old icon
  (the GoF2 logo on skybox_003).
- **Android name**: package `com.joppietoppie.gof2remake`, launcher label "GoF2 Remake" (`AndroidAppLabel` rewrites the Gradle project's app_name); the product name stays "Galaxy on Fire 2" so the desktop save folder and PlayerPrefs don't move.
  **Beta builds** (fork, `AndroidBetaPackage`): with GoF2 > Build > Android Beta Package ticked (per machine) or
  `GOF2_ANDROID_BETA=1`, an Android build gets the package `com.joppietoppie.gof2remake.beta` and the label "GoF2 Beta", so it
  installs beside the published game (the same package signed with another key has to replace it); put back after the build
  like `BuildVersionStamp`. The beta has its own data folder (saves, mods, Transfer). The Java class
  `com.joppietoppie.gof2remake.ModImportActivity` keeps its name: a class's Java package is not the app's package.
- **Android Vulkan pre-transform off** (Player settings > Android > "Apply display rotation during rendering",
  `PlayerSettings.vulkanEnablePreTransform`, off since 2026-10-14; a player's report): with it on, at render scale 100 %
  without anti-aliasing part of the screen froze (the same symptom as #45's FSR at 100 %+: URP's final pass through an
  intermediate texture doesn't get the pre-rotation, so it covers only part of the rotated back buffer); off, it didn't
  happen. Off = Android rotates the landscape frame itself (free on most recent phones' display hardware, else a little GPU
  composition); not measured here. `Bootstrap.ApplyUpscaling` still keeps FSR 1 below 100 % until that is checked on a phone.
- **UWP** (Universal Windows Platform, no build profile: `EditorUserBuildSettings.SwitchActiveBuildTarget(WSA, WSAPlayer)`
  first, then `BuildPipeline.BuildPlayer` to a folder; IL2CPP, x64, D3D): package `JoppieToppie.GoF2Remake`, Start menu
  name "GoF2 Remake", capabilities InternetClient / InternetClientServer / PrivateNetworkClientServer (multiplayer and
  hosting), signed with a test certificate `Assets/WSATestCertificate.pfx` (publisher CN=JoppieToppie, made by the internal
  `EditorUtility.WSACreateTestCertificate(path, "JoppieToppie", "", overwrite)` through reflection: a publisher given as
  "CN=..." became CN="CN=..."; git-ignored, a new clone makes its own). Unity writes a Visual Studio solution; the
  package comes from MSBuild (`MSBuild "Galaxy on Fire 2.sln" -p:Configuration=Master -p:Platform=x64 -p:AppxBundle=Never
  -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxPackageDir=...`; needs Visual Studio's "C++ Universal Windows Platform
  tools", `Microsoft.VisualStudio.ComponentGroup.UWP.VC`, which Burst needs too): `<name>_x64_Master_Test` with the
  .msix, the .cer and Install.ps1 (it trusts the certificate, admin prompt, then installs); a release zips that folder
  without the .appxsym symbols and the TelemetryDependencies folder (the script then sends no sideload telemetry). Not
  in UWP: Discord, the controller gyro, the save file dialog (Transfer folder instead), the dedicated server
  (everything desktop-only is behind `UNITY_STANDALONE_WIN`), the window mode / resolution options
  (`Bootstrap.HasDisplayOptions` false under `UNITY_WSA`: `Screen.mainWindowDisplayInfo` throws NotSupportedException
  there, which stopped `Bootstrap.Init` and the main menu's options setup, a black screen after the splash). The back
  buffer took the window's view-pixel size (1920 x 1080 on a 4K TV, scaled up): `UwpDisplay` sets it to the window's
  raw pixels (`DisplayInformation.RawPixelsPerViewPixel`), on an Xbox to the HDMI mode (`HdmiDisplayInformation`; only
  the One X / Series X above 1080p, `AnalyticsInfo.DeviceForm`), again on resize / DPI change, and logs "UwpDisplay: ..."
  (not yet verified on a 4K display or an Xbox). Testing
  a UWP build here: with Developer Mode on, unpack the .msix (its entry names are URL-encoded) without
  AppxSignature.p7x / AppxBlockMap.xml / [Content_Types].xml / AppxMetadata, `Add-AppxPackage -Register` its
  AppxManifest.xml, start it from `shell:AppsFolder\<family>!App`; the log is
  `%LOCALAPPDATA%\Packages\JoppieToppie.GoF2Remake_*\TempState\UnityPlayer.log`. `BuildVersionStamp` also stamps the package version
  (yyyy.M.d.HHmm). Switching to UWP adds default WindowsStoreApps entries to every texture .meta: revert them.
- **UWP sky** (#20): the space-sky strips (`Resources/GoF2Sky`) have a WindowsStoreApps override to uncompressed RGBA32 with 1365 px faces
  (`SkyboxBaker.ApplyUwpOverride`; the asteroid belt sky layer's `skybox_asteroid_belt_diffuse` too, RGBA32 at 2048, set by `AssetImport` since 2026-10-11: compressed it showed as a checkerboard of squares on the Xbox; the Xbox runs UWP apps at D3D feature level 10, which has no BC7: the default CompressedHQ
  drew bands of shifted tiles, BC1 bands and a criss-cross; if RGBA32 still breaks it is the cubemap layout, and the next step
  is the shader's 2D strip path; unverified on the device). Menus: a text field is one
  focus stop (`MainMenu.InsideTextField`; its inner input was listed too and handed the focus back, trapping the D-pad in the
  multiplayer panel's name field), panels open on their first non-text item, and on UWP the text typed while the system keyboard
  was open is kept when it closes with B (`KeepKeyboardText`; closing counts as Cancel and put the old text back).
- **Version** (`BuildVersion`): the menu's credit line and the About page show "Galaxy on Fire 2 Unity Remake created with
  <heart sprite> by JoppieToppie · <version>" (`BuildVersion.Text`; also `/version` in the chat, the multiplayer window's
  foot and the dedicated server's first log line; no git branch / commit any more: joining compares the fingerprint); the version is the date and time of the git commit the build comes from (`yyyy.MM.dd.HHmm`, UTC; fork change, upstream
  uses the build's own time, still the fallback without git), so Windows, Linux and Android builds of one commit match; stamped into
  `PlayerSettings.bundleVersion` for each build by `BuildVersionStamp` (Editor) and put back afterwards, so
  `Application.version` and Android's versionName carry it; "editor" in the Editor.
- **Update check** (remake, `UpdateCheck`): entering the main menu (after the title screen) asks GitHub once per run for the
  newest release (`api.github.com/repos/JoppieToppie/Galaxy-on-Fire-2-Unity-Remake/releases/latest`, no token; drafts and
  pre-releases ignored). Release tags are `yyyy.MM.dd`; the build's `.HHmm` is ignored, so only a release from a later day
  than the build counts. Then "Update available · <tag>" shows at the bottom centre (`.update-row`, only while no panel is
  open; Down from the last menu row reaches it) and opens the release page (`Application.OpenURL`). A failed request is tried
  again on the next menu entry. The Editor ("editor") checks only with `MainMenu.editorTestVersion` set (e.g. 2026.09.29.2200).
- **Debug sizing** (remake): the main menu's Debug panel is 88 % of the screen high with the mission list and the cheats (a ScrollView following the focus) filling it; the pause menu's Debug page scrolls its rows (`.debug-scroll`, as tall as them, shrunk to the panel) with the selection kept in view; the station's Debug page fills the menu's 92 % (`.system-menu--wide`). On a small high-density screen the screens' root gets `ui-large` (`UiScale.Large(height, reference)`, set with `layout-phone` in MainMenu / StationMenu / FlightHud) and the debug, pause, system-menu and choice rows grow about 1.35x.
- **Debug panel** (remake-only testing tools, `MainMenu.BuildDebugPanel`; F10, LB + RB or three fingers held for 1 s, or five taps on the version text, each within 1 s of the last; its tap zone reaches 40-60 px past the text; or Options > Gameplay "Debug tools", `Cheats.Unlocked`, which also shows a Debug button in the main menu; with Resume shown too the column starts higher, `.main-column--full`, so Exit stays clear of the version text and its tap zone): on the left the mission list: a search field (every word must match; a number matches the step index) and every step 0-162 by campaign (53 / 129 skipped), "index · title · station" over an optional subtitle (`StepSummaries`, `Resources/GoF2Data/step_summaries.json` from the hand-written table in `Reference/tools/campaign/build_step_summaries.py`: a one-line title per step, the subtitle for extra info; both cut to one line); a row → the difficulty panel → `Story.StartAtMission`: the step's campaign start (main / Valkyrie from 45 / Supernova from 84), every step up to it through `Advance` (side effects) with its reward credited, Betty past the rescue, the tutorial's free weapon and armor past step 6 and its hints shown past 8; a replay of what each step's task leaves the player with (`Story.PlayStep`: Betty, the tutorial gear and hints, 20's EMP GL I, 50's Vossk standing, 58's Liberators, 77's Cronus, 91's miners and cabin, 118's mutagen) and where it leaves them (`PlaceAfter` / `ExitTo`, the Void return station); then the step's story orbit (launched when docked there, else an arrival from the previous place) or that place (docked, never at an empty orbit or a refusing station). Steps only reached inside another (42, 46, 53, 107, 129, 149 / 150) aren't offered (`Story.MissionSelectStart`). On the right the cheat toggles (`Cheats`, PlayerPrefs `cheat_*`, rows from `CheatsCatalog` through `OptionControl`): god mode (`Target.Damage` / `PlayerHealth.Kill`, no gamma drain, volatile goods safe), infinite ammo (`WeaponSystem`), no boost cooldown (`FlightModel.UpdateBoost`: ready again as a boost ends), no primary weapon cooldown (`WeaponSystem` reloads the held primaries as fast as their bullet pool sustains, `Gun.SustainedReloadMs` = lifetime / pool: reloaded every frame they emptied the pool in a burst and waited out a lifetime), no secondary weapon cooldown (`WeaponSystem.FireSecondary`: the reload skipped; the pool still limits rockets / bombs in flight), one-hit kills (the player's hits; script-invulnerable ships still survive), instant locks (`Cheats.LockMs` in `CombatRadar` / `Mining` / `Navigation`), free shopping (items and ships, `Hangar`), free jumps (`GalaxyMap.EnergyCells`, `SystemJump`, the cloak). Once opened (`Cheats.Unlocked`) the pause menu and the station's system menu get a Debug page with the toggles and the actions: +100 000 / +1 000 000 credits, repair (hull, shield, armor, gamma), secondaries to 50, +20 energy cells, reveal all systems, make peace (both standing axes 0, `AttackedStations` cleared), fill the Kaamo Club (`Cheats.FillKaamoClub`: owned, one of every item the storage takes (50 of each secondary; not the unsaleable story items) and a hull of every ownable ship type, not 13 / 14 / 15 or the one flown; topped up, never doubled); **Give items** (item type, item (a grid of the type's shop icons with their names: a tap / click picks, the picked one framed and scrolled into view, two rows visible, the cells sharing each row's width; its stepper is an invisible focus stop for keys / controllers: selected, the grid is framed amber, left / right step through the icons and up / down a row (`CheatsCatalog.MoveVertical`, the columns counted from the cells' layout; the grid has no scroll bar: one appearing narrowed the rows by a cell, so down went one to the right; from the top / bottom row on to Item type / Amount, a short last row takes its last icon); `CheatsCatalog.ItemPicker` on the shared `GridPicker`, which the Ships tab's Ship row uses too (the shop icons; the hulls not normally flyable with Build Hull Icons' renders), #82; bigger text in the debug cards on phones; the Debug pages are 1180 wide, the station's 570 high, so the buttons fit); keys / D-pad on the Debug pages move on screen (`SpatialNav`: up / down the nearest row, left / right the item beside, no wrapping), left / right only step steppers, segments and sliders (`OptionControl.StepsSideways`; a toggle flipped on left / right, and down from Add to cargo hold went to Add and mount), amount 1-1000: into the
  hold; docked also "Add and mount", `Cheats.GiveAndMount` through `Hangar.Mount` / `Swap`); in flight **Spawn** (`DebugSpawner`):
  any ship by race / model / behaviour (hostile, by standing, friendly) 400 m ahead as ordinary traffic (a "Turrets" row: their own, or any turret item on every turret mount and on a spawned capital ship's turret objects), and any assembly of
  `assemblies.json` by category as scenery ahead of the player, far enough out for its size; **Presets** tab (`ShipPresets`, `CheatsCatalog.Presets`; players' suggestion): 8 slots (picked in the same icon grid, `GridPicker`: the ship's icon and "N. ship", an empty slot by name; under the grid a hint panel, `GridPicker`'s hint / `CheatsCatalog.PresetHint`, for the slot under the pointer, else the picked one: the ship, its mods and save time over its equipment as shop icons with names and amounts, the same item once with the amounts added) in `persistentDataPath/ship_presets.json` (outside the saves), each the hull flown (its PlayerHull key, debug hulls too), the mounted equipment with its amounts and the ship mods; Save the ship flown now, Load (equipment and mods set, the hull flown through `PlayerHull.Fly` or rebuilt in place when it is the same; docked a hull the hangar can't take is refused first; in flight `PlayerHealth.RefreshLoadout` sets the maxima from the new loadout, full), Delete; items no longer in the game are left out; **Ships** tab (`PlayerHull`, `CheatsCatalog.Hulls`, `SpaceLevel.SwapPlayerShip`, which also sets the hull, shield and armor maxima from the new hull and equipment, each pool keeping its share: `PlayerHealth.RefreshLoadout(db, true)`): fly any of the
  game's 68 hulls where the player is, picked like Give items by Ship type (the races from the model names, Other, Modded (the mods' ships.json
  ships; the list is made again when the mods change, `PlayerHull.CheckMods`) and Not normally flyable) and Ship: ships 0-63 with a model (50 / 53 have none), the Vossk freighter 13, the three race
  freighters 15 (`Database.ShipAssembly` now resolves 13 / 15 like `Globals::getShipGroup`), the Terran battleship 14, and
  the capital ships outside ships.json flying with ship 14's stats: the Terran carrier, the Vossk battleship, the Valkyrie
  battlestation and the Void mother ship (held at its full-size pose like the alien orbit, `PlayerHull.PrepareModel`). The
  capital ships' turrets (`TrafficPlan.BattleshipTurrets` / `CarrierTurrets` / `VosskTurrets`, `ValkyrieLevels.StationTurrets`
  with the shield generators as scenery) ride on the model as the player's auto turrets (not the NPCs' fighter turret of 45 / 51: the player's Bloodstar / Rhino have a turret slot, #45)
  (`NpcShip.MakePlayerTurret`: aim at what is hostile to the player, shots pass the player, off the radar; they fire the item
  their model is, `Hull.turretGun`: the battleship's and carrier's `turret_002` the Hammerhead D2A2 48, the Vossk
  battleship's `turret_003` the L'ksaar 49, not the NPCs' race blaster). Remake (players' suggestion): the capital hulls
  (battleship, carrier, Vossk battleship, Valkyrie) have no weapon slots, so the fire button launches the enhanced capital
  ships' salvo (`PlayerCapitalMissiles`, `Hull.missileItem`: 4 homing missiles of the race, the Intelli Jet 37 / S'koonn
  38, 250 ms apart from the turret mounts, one salvo per 4 s while held) at the locked ship when hostile, else the nearest
  hostile ship within 2 km of the hull; never a neutral or friendly one; the item's damage, x3 on hit boxes. Hulls over 150 m
  (sized without the additive glow layers) get a chase camera behind the stern (its height x `Hull.cameraHeight`: the carrier 0.4, just above its deck), the model centred on the pivot and a longer
  far plane, and are shrunk on the hangar turntable. The hulls the player can't normally own (13 / 14 / 15, the capital ships; never an ownable or mod ship) fly with weight (`PlayerHull.ApplyMass`: `ShipController.mass` = log(radius / 150 m) / log(2500 / 150), the flown model's size: race freighters ~0.2, the Terran battleship 0.72, the carrier 0.91): `FlightModel.Mass` lowers the top turn rate (`TurnScale`, the carrier 4.9 deg/s against 21.6) and gives the turn rates inertia (`Inertia`: 2.2 s to full turn, they coast on after release), eases the speed toward the throttle (`MoveSpeed`, `AccelMs` 7 s x mass, the boost 2.5x faster) and slows the level-out / roll (`RollScale`) and the strafe / dodge (`SideScale`); less and slower banking, a heavier chase camera (its coefficients x0.35 at 1; a slow engine sway of the view read as shaking and was taken out again), a controller rumble while the engines strain and boost, a smaller boost FOV, a deeper engine (pitch x0.6, boost x0.7) heard as from a normal chase distance (`engineEarScale`: the fitted camera km out was past the 500 m rolloff). Mass 0 = the original's numbers everywhere. The pick is PlayerPrefs `cheat_hull` while `Session.ShipIndex` is its
  ship; "Back to your own ship" returns to the one flown before (`cheat_previousShip`). Hulls the player can't normally own
  (13, 14, 15 and the capital ships) never land in a hangar: no Dock prompt, the autopilot and `SpaceLevel.Dock(true)` refuse,
  the story's moves into a station (`Dock(false)`) and a station loaded with one (`StationLevel`) put the player back in their
  own ship (`PlayerHull.ForceOwnShip`); docked, the tab only swaps to ships the player can own. Debug spawns call
  `Traffic.ConnectPlayers` so they get their targets. Medals still count what cheats bring.
- **Hitch logger** (`HitchLogger`, development builds only; in the Editor with PlayerPrefs `debug_hitchlog` = 1): frames over 33 ms on the real clock to `persistentDataPath/hitches.log` (scene, seconds since its load, campaign step / station, tags load / GC / focus, a summary per scene) and `GraphicsSettings.logWhenShaderIsCompiled` on, so logcat shows every "Uploaded shader variant to the GPU driver" with its time. Measured on the S24 Ultra (Vulkan, Adreno 750, Vulkan pipeline cache deleted, 2026-09-28): 28 variants / ~360 ms at startup before the menu shows, then only at scene loads (GoF2/Unlit 49 ms at the first station, SpaceDust 26 ms), nothing during play (the menu backdrop's live orbit already uses the flight shaders), no frame over 50 ms: no shader warm-up (`GraphicsStateCollection`) needed so far.
- **FMOD event IDs -> .ogg**: `Reference/research/fmod_event_ids.txt` (every system id, from the FEV's LGCY data; see "Known open items"). Menu theme = 145 `Space_NoCombat_Void`, new game = 143 `IntroAtmo`, buttons 124/123, dialog 126.

## Multiplayer (remake-only)

Netcode for GameObjects 3.0 (`com.unity.netcode.gameobjects`, which now also brings Netcode for Entities, Entities, Burst and
Collections) over Unity Transport: online through Unity Relay with a join code and the Unity Lobby server browser
(`com.unity.services.multiplayer`, project linked to Unity Cloud, Relay and Lobby on in the dashboard, anonymous Unity
Authentication), or on the local network by direct IP, port 7777. Code in `Scripts/Runtime/Net/Multiplayer` (`GoF2Remake.Multiplayer`);
the network prefabs in `Resources/GoF2Net` (**GoF2 > Build > Network Prefabs**: NetPlayer / NetProxy / NetState / NetCrate, each a
NetworkObject; it re-saves them so each gets its own GlobalObjectIdHash, and switches Android's internet permission on).
Single player is untouched: every multiplayer path runs only while `NetGame.Active`.

- **One shared world, no scene synchronisation** (`EnableSceneManagement = false`): every player starts a fresh free-play
  game in the finished game's world (below) docked at Dis (70) and plays it like single player, their own scenes, economy, map,
  jumps and docking. A new pilot starts in Betty with 10 000 credits and a simple loadout (Nirai Impulse EX 1, Targe Shield,
  Telta Quickscan, IMT Extract 1.3; `NetGame.StartShip` / `StartCredits` / `StartEquipment` in `PrepareSession`), not
  resetGame's Phantom, so they work up to better ships; a server profile replaces it once it loads. The network objects live in DontDestroyOnLoad and each player shows only what is where they are.
  The host spawns NetState and its own NetPlayer and loads `Station`; a client connects (10 x 1 s) and loads `Station` when
  NetState reaches it (`NetGame.EnterWorld`); each connecting player gets a NetPlayer. Nothing is saved in a session
  (`SaveGame.Save` refuses), so it never touches the single-player saves; a dedicated server keeps player profiles
  instead (see **Player profiles**).
- **The finished game's world** (`Session.UseCompletedWorld`, `Session.CompletedWorld`; set by `NetGame.PrepareSession` and
  `SaveGame.ApplyProfile`): free play (no story steps) at campaign index 162, so everything that reads the index sees the
  won game: the full shop stock and dealer ships (Kothar's 37 / 38 / 40, the Vossk 39 / 41, the carrier and Vossk battleship
  in traffic, Mido's dealers), and the medal / Most Wanted gates count as met (Thynome's VoidX, Katashán's Specter,
  Quineros' 45-48); the add-ons' revealed systems on the map (`Story.RevealedSystems`, `GalaxyMap.Visibility`); Ginoya after
  the supernova (normal sun, no flares, its traffic and shops); no wormhole, Void invasion or Kappa's free EMP GL I. Remake
  picks: no storms anywhere (`SkyLayers`); Naneroh, Valpatro and Luur intact and open for docking (`OrbitLayout.IsRebuiltGinoya`;
  Luur its pre-supernova model, Naneroh / Valpatro borrow Midantha's / Tergalon's model and volumes, `OrbitBuilder.StationLook`);
  sessions start docked at Dis (70, `NetGame.Station`), whose shop always sells the Khador Drive (85). The Cronus (37), 0 in the Android
  economy (step 77 sells it for nothing), costs 1 150 000 there (`Database.CronusSessionPrice`, applied by `Database.Load`
  while `Session.CompletedWorld`; `PrepareSession` resets `NetGame.Db`): a hull of its stats (~500 000) plus its Khador Drive. The world's look and menus read `Session.WorldIndex` (free play's 20, else the index);
  NPC hulls / reloads and the Kaamo outposts use the won game's capped 45 / 180. Naneroh keeps the original's
  gamma rays after the story, 1/s (and the gamma blaze with a gamma shield), like single player (#80; first the remake had none).
- **Menu**: the main menu's Multiplayer button (between New game and Load) opens the Multiplayer panel (a fixed 84 %
  high panel: the title with the "Experimental" badge and the pilot name `mp_name`, a one-line intro, then the server
  browser and the host column). **Pilot name required**: without one (`NetGame.PlayerName` empty, no `-mpname`) opening
  the panel asks for it in the menu's dialog (`MainMenu.NeedsName`: its `dialogField`, OK refuses an empty name, Back
  closes), and so do Host, Join and a server browser row before they start (`WithName`). **Server browser** (`NetLobby.Query` every 5 s while the panel is open; rows rebuilt
  only when the list changed, so a controller's focus stays): every listed game of every version, this version's first
  and the fullest first; two lines per game: its name with SERVER (dedicated) / PASSWORD / MODDED tags and PVP / PVE (`NetLobby` key `pvp`, kept current like the player count; none for older listings), then the host, players /
  limit ("full") and the version (another version: amber "needs X", not joinable, a tap says why); a tap joins by its
  join code (a password game with the password field empty asks for it first); "N games" by the title. Under it one
  row: the code-or-address field (`mp_address`), the password field (this run only; `-mppassword` for testing), Join.
  **Host a game**: Public / Invite only / Local network (`mp_mode`); online = a Relay session (Public also listed under
  the Game name field, `mp_session_name`, default "<pilot>'s universe"); the password field (`mp_host_password`,
  optional, every mode) and Max players beside it (`mp_max_players`, `NetGame.MaxPlayers`: 2..100, the host included,
  default 16; Relay's connections are that minus a player host, the browser's limit is it, and the connection approval
  turns away a player past it in every mode, "The game is full (N players)."); **Combat** PvE / PvP (`mp_pvp`, PvE by default: `NetGame.FreePvp`, which NetState carries; PvE = players fight only in arena matches and faction sieges, PvP = anywhere; a dedicated server's `-freepvp`, the launchers' `PVP=1`, the server settings' "freepvp"); Local network shows every address of this device others could join on, named by adapter (`NetGame.LocalAddresses`: Ethernet / Wi-Fi first, then VPNs like Hamachi,
  ZeroTier, Radmin, Tailscale; not down, loopback, link-local or virtual-machine adapters; Android's Linux names
  mapped: wlan = Wi-Fi, swlan / ap = Hotspot, rndis / usb = USB, tun = VPN, mobile data (rmnet, ccmni) left out; Windows'
  mobile hotspot = Hotspot; none = a "connect to Wi-Fi" line, never 127.0.0.1), a tap copies it (with
  ":port" when not 7777), and the port field (`mp_port`, `NetGame.HostPort`, default 7777); the host listens on all
  adapters (0.0.0.0). A port in use is caught before the fade (`NetGame.CanHost`: UDP bind test), the panel says so and
  puts the next free port in the field. The address field takes a join code (6 letters / digits, `NetGame.IsJoinCode`)
  or "host" / "host:port" (a name is looked up, `NetGame.ParseAddress`); the status line under the cards gives the last
  session's end reason. The host card's settings (text, addresses / port, name, password / player limit, the Debug / Combat /
  World / Mods switches two to a row) sit in `mpHostScroll` between the mode tabs and Host: at 16:9 they fit; when they
  don't (three or more adapters, short screens) they scroll by wheel / touch / focus with an amber "More options" hint
  under them (`MainMenu.SetupHostMore`; the scroll bars are hidden everywhere). Before, the four switch rows squeezed the
  local network's address list and port field flat. Touch presses don't take the focus in the menu, except in a text field (the on-screen keyboard
  needs it).
- **Online (Relay / Lobby)** (`NetGame.PrepareOnlineHost` / `StartClientOnline`, `NetLobby`): hosting online signs in
  (anonymous; a profile per `-mpname`, so two games on one machine are two players), reserves a Relay allocation (`NetGame.MaxPlayers`
  connections, one less for a player host; a dedicated server's `-maxplayers`) and its join code before the fade, then StartHost
  / StartServer connect through it (`RelayServerData`, "dtls"); the code goes to the clipboard and shows on its own plate
  under the station's system information (Copy; `SquadView`); a client joins the allocation by its code and connects. A listed game
  is a public lobby (`NetLobby.Publish`: name, host, players (N1), dedicated, password, version (S1), the join code;
  nobody joins the lobby, it is only the listing): a heartbeat every 15 s, the player count updated as it changes,
  deleted with the session (a game that died drops out after Lobby's 30 s). Errors in plain words (`OnlineError`: no
  game with that code, no internet, else the service's message).
- **Version check and passwords** (Netcode's connection approval, `NetGame.Approve`): every connecting game sends
  `NetGame.Protocol` (the code's fingerprint, `BuildVersion.Fingerprint`: the Editor's `BuildFingerprint` hashes the runtime
  scripts, `Resources/GoF2Net` prefabs, `Resources/GoF2Data` JSON and the resolved versions of the multiplayer packages
  (Netcode, Unity Transport, Multiplayer Services, from `packages-lock.json`; not the rest of the package list: a build-only
  package such as the Linux toolchain mustn't split players), paths sorted, line endings normalised, SHA-256's first 12 hex digits; `BuildVersionStamp` writes it into `Resources/GoF2Build/BuildFingerprint.txt`
  for the build, git-ignored and deleted afterwards), its password and its shown version (`ConnectionData`); builds of the
  same code play together whenever and for whichever platform they were built (verified: two Windows builds 2 minutes
  apart), the server browser and its sort use the fingerprint too (lobby key `protocol`; "another build" for the same shown
  version with other code); only the same fingerprint gets in ("This game runs version X, yours is Y..."; the Editor as server takes any version, and every build
  lets the Editor in, for testing: it skips only the version check, not the password), then the session's password if it has one (`HostPassword`: "needs a password" / "Wrong
  password."); the reason is the player's popup. The server checks it: the lobby's join code is public. Builds from
  before the check don't use approval, which is part of Netcode's config hash, so they fail its handshake. Verified with
  a Windows build and the Editor: the version refusal, no / wrong / right password over Relay, the listing and its count.
- **NetPlayer** (one per player): the owner writes where they are (station; `Place` Space / Hangar / Departing, from the
  scene it is in and `StationLevel.PlayerDeparting`), ship, name, hull, and in space the pose. The others in the same
  orbit (`SharesOrbit`) see the ship model with the pilot name, lockable, a sphere `Obstacle` (1.6 x the model's bounding
  radius) the local ship slides along; elsewhere it is hidden. Remote motion is smoothed (`NetSmoothing`: velocity
  estimate, at most 250 ms ahead, eased, snapped beyond 250 m). It also writes its shield / armor fractions (-1 = none),
  which its copy hands its Target (`netShield` / `netArmor`) for the HUD marker (`CombatView.DrawPools`, players' request):
  near, a second frame of the faction's bar 11 px above the hull bar with the shield in the status bar's cyan, and the hull
  bar's fill in the status bar's red with the yellow armor over it (shield, armor, then hull drain, `Player::damage`); a
  pool of -1 is left out, NPC markers (both -1) are unchanged.
- **Players against players**: another player is a yellow (neutral) marker until one of the two attacks the other (weapons or EMP, 3 hits or 50 damage within 10 s, so a stray shot doesn't count: the victim's game counts in `NetPlayer.HitRpc` / `EmpRpc`, the attacker's game in `RemoteDamage` / `RemoteEmp`): then both are enemies to each other for 120 s from the last hit either way, like an NPC that turned (`NetAggression`): red markers, and the auto turrets, a debug hull's turrets and the sentry guns fire (they read `hostileToPlayer`); forgotten when either is destroyed or the session ends. Hits on their ship (guns, missiles, blasts,
  NPC shots from the authority's side) go to their game (`NetPlayer.HitRpc`, `Target.RemoteDamage` with the NPC flag),
  which applies them to its own ship. Destroyed by a player: "X was destroyed by Y." for everyone
  (`NetState.DestroyedByRpc`). A destroyed player's game over says "Tap to respawn at the station." and docks them at the
  station they last docked at (`Session.LastDockedStation`, set by `StationLevel`, saved as `lastDockedStation`), repaired
  (no save is loaded). Players can only hurt each other in an arena match (below) or on a
  server started with `-freepvp` (`NetState.FreePvp`, `NetGame.FreePvp`): elsewhere another player's ship is
  `playerProof` (shots pass) and the owner's `HitRpc` / `EmpRpc` drop a player's hit unless the sender is in the same
  orbit and may fight (`NetPlayer.PvpWith`).
- **Server settings while running** (`NetServerSettings`; the operator guide is `SERVER.md`, keep it in step with the
  server's options and commands): name, password, maxplayers, allowdebug, freepvp, maxearn, claimcost,
  maxclaims, claimdays, siegecost, toll. Changed by admins (`/set <key> <value>`, `/settings`, the Admin tab's Server
  settings: a field or switch with Save each) or the console (`set`, `settings`); applied at once where possible
  (allowdebug / freepvp through `NetState.SetDebugAllowed` / `SetFreePvp`, the toll through `SetToll`; the name at the
  next listing, `DedicatedServer.ListName`; a higher player limit online after a restart) and saved to
  `server_settings.json` beside the profiles. A start applies the defaults, then the file, then the command line (an
  option given there wins: `FromCommandLine`, the window says "set by the launcher"). The password is never sent.
- **Moderation** (`NetModeration`, a dedicated server with profiles; its commands are `NetCommands` rows, so they show in /help and Tab completion; a profile's role is on `NetPlayer.StaffRole`, and admins / masters also get `NetCommands`' admin commands; on such a server `/kick` and `/admin` / `/unadmin` are this system's, elsewhere the session-only ones): roles on the profile (`Account.role`: 0 player, 1 op,
  2 admin, 3 master). The **master admin** (the owner) is claimed in the game: `/claimadmin <token>` (or the station
  window's Profile tab, "Claim this server"), the token from `-admintoken` / `GOF2_ADMIN_TOKEN`, else
  `admin_token.txt` beside the profiles (made at the first start; logged at every start; console `token`; a wrong try is
  logged, one check per 5 s), so a server needs no console. Masters make admins (`/admin`, `/unadmin`) and delete
  profiles (`/deleteprofile <id>`, not while online); admins make ops (`/op`, `/deop`), announce (`/say`, as
  "[admin] Name") and end factions (`/disband <TAG>`); the console can do everything and makes masters (`master` /
  `unmaster`). Nobody acts on a pilot of their own role or higher, or gives a role as high as their own. Ops: `/kick <pilot> [minutes] [reason]` (dropped; can't rejoin for the minutes, default 5, a
  temporary ban), `/tempban <pilot> <minutes> [reason]` (ops at most 24 h), `/unban <name|profile>`, `/bans`; admins
  also `/ban <pilot> [reason]` (for good). `/staff` for everyone. Nobody acts on their own rank or higher (the console
  on anyone). A pilot: a name online, `#<client id>` (the window's buttons) or a profile's id / last name offline. A ban
  holds the profile and every device label it signed in from (no new profile from the same device), checked in
  `NetProfiles.OnLogin` (dropped with the reason and the time left); every device of a banned profile online is
  dropped; kicks and bans are announced. `bans.json` beside the profiles; run-out bans are pruned. Console: `kick
  <id|name> [minutes] [reason]`, `ban`, `tempban`, `unban`, `bans`, `op`, `deop`, `admin`, `unadmin`, `staff`. The
  station window's Admin tab (ops and above: `NetPanel.State.role`, `bans`, each pilot's `role`): reason and minutes
  fields, Kick / Ban (minutes) / Ban for good / Make op / Remove op / Make admin (masters) per pilot, Unban per ban; for
  admins also the server's status line (`DedicatedServer.StatusText`) and an announcement field, the staff (remove op /
  admin), every profile (`NetProfiles.FillProfiles`: at most 200, the most recent first; a filter; Ban / Unban, roles,
  Delete for masters) and the factions (Disband). Dangerous buttons (Ban for good, Delete, Disband) ask twice. Not tested
  in a build yet.
- **Factions** (`NetFactions`, a dedicated server with profiles; phase 1 of home systems / territory): the lasting player
  groups, kept by profile in `factions.json` beside the profiles (squads stay the session's quick fly-together groups).
  A faction: name (24), tag (2-4 letters / digits, unique; `NetPlayer.FactionTag`, server-written, `TaggedName` "[TAG] Name"
  on the lock plate and in chat), leader, officers, members (profile ids, at most 50), bank and home station (for the
  next phases). Chat commands (private answers): `/faction create TAG Name`, `invite <pilot>` (officers; 5 min),
  `join TAG`, `leave` (the leader only by handing over or as the last member), `kick <pilot>` (officers: members;
  the leader: officers too), `promote` / `demote <pilot>`, `leader <pilot>`, `disband` (leader), `info [TAG]`, `list`;
  `/f <text>` to the faction's online members. A deleted profile leaves its faction; a leaderless faction passes to its first
  officer, else first member; an empty one ends. Console: `factions`, `faction disband <TAG>`.
  Phase 2, territory (`NetFactionsClient` on the player's side): the **bank** (`/faction deposit N`: the server asks the
  player's game to pay, `ChargeRpc` / `ChargedRpc` with a server token; `/faction withdraw N`, officers: `GrantRpc`; both
  move the profile's recorded worth, `NetProfiles.AdjustWorth`, so the upload check fits them; and the credits in its
  saved game, `NetProfiles.MoveCredits`: a deposit, a toll or a trade's credits must be in the profile's last accepted upload,
  `SpendableCredits`, else refused and given back to the game (a game claiming credits it never had fed a faction bank 36
  billion while its own uploads were refused; closing the game before the next upload no longer gets a deposit back)); **claims** (`/faction claim`, officers, docked there: `-claimcost` (500 000) from the
  bank, `-maxclaims` (3) per faction, not 108 or system 25; `/faction unclaim`, `/faction home`, `/faction claims [TAG]`; the first
  claim is the home; a claim no member docked at for `-claimdays` (14) lapses, `NetFactions.Tick` once a minute; a claim
  is announced to everyone); `NetState.Claims` ("station|TAG|Name|VAT" lines, a 4 KB FixedString) shows the holder on the
  star map (planet "[TAG] Name", system "Name [TAG]"), the station header's system line and the orbit information.
  **Home**: no longer a start or respawn point: signing out and in, or dying, was a free trip home (energy cells and time
  saved). Every pilot signs in docked at the station they last docked at (`SaveGame.ApplyProfile`: `lastDockedStation`
  beats the profile's `station`, which an upload in flight sets to the orbit) and respawns there (`FlightHud.LoadLastSave`;
  a siege defender still respawns at the besieged station). `ProfileHeaderRpc` still carries the home, unused. Disbanding frees the faction's claims (the bank is
  lost).
  Phase 3, contest and benefits: **sieges** (`/faction siege`, officers of another faction in the orbit or docked there:
  `-siegecost` (250 000) from the bank as the stake, one per faction, the faction needs room for a claim, not within 24 h of
  the station's last siege, not the defenders' home while they hold another station; announced, armed 10 min later). Armed,
  it waits until pilots of both factions are alive in that orbit's space at once (real players: NPCs never start it).
  Meanwhile the station defenders escalate (`NetOrbit.UpdateSiegeDefenders`, tag `SiegeDefenderTag`, in every orbit, also
  Thynome's and the others without traffic: 16 at level 3, +4 and +1 level every 6 h, at most 48 at level 5, guns x1.5 from
  level 4, the garrison's relation, back 30 s after each death, `NetFactions.DefenderCount` / `DefenderLevel`; frozen at the
  fight's start); the claim drains: a defender docking there halves its remaining lapse days, rounded up (15 -> 8 -> 4), 3 or
  fewer stay, never a reset (`HalveLapse`; the claim tick's refresh skips sieged claims; a respawn after dying in the fight
  isn't a dock), and a claim lapsing then goes to the attacker with the stake; the standoff clock: a side present while the
  other has a pilot online elsewhere adds to the absent side's standoff, 60 min = that side forfeits; no fight within 2 days
  of the arming = +1 day, at most 4 times, then the defenders keep it and the stake. The fight runs 15 min, the two factions
  may fire at each other there (`NetFactionsClient.SiegePvp` in `NetPlayer.PvpWith`): every pilot of a side destroyed in the
  orbit scores for the other side, a player's kill of the other side repairs the killer at once (`NetFactions.OnPilotKilled` from
  `NetState.DestroyedByRpc`: hull, shield, armor through the Heal order, a = 1), a destroyed defender respawns docked at that station (`NetFactionsClient.DefendsSiegeHere`
  in `FlightHud.LoadLastSave`), a side whose last pilot there left alive (docked, jumped, disconnected) and stays gone 60 s
  waves the white flag and loses; at the end more kills wins, a tie = held. The stake to the winner; called off with the
  stake back to the attacker: the claim given up, the defenders disbanding (`CallOff`). 24 h protection after a decided
  siege. A claim lapsing without a siege can't be claimed for 48 h (`FactionList.lapsed`). `NetState.Sieges` lines (phase 0
  announced / 1 armed / 2 fighting, seconds left, stake, garrison, station defenders and level, kills, days added),
  `/faction sieges`, console `sieges`; `TerritoryView`'s banner);
  **defence** (in a held orbit the fighters of the system's race treat the holder's members as friends and other factions'
  pilots as enemies unless they paid the toll this visit: `NpcShip.TerritoryToLocal`, `NetOrbit.HostileToRemote`,
  `NetFactionsClient.Relation`; pilots without a faction as always); **toll** (`-toll`, 10 000 credits, or `N%` = N % of the pilot's credits (`NetFactions.ParseToll` / `TollFor`, stored as -N; the pilot's game sends the amount, the server caps it at that share of the profile's worth); `TerritoryView` asks on
  arrival through `Traffic.Ask`, `NetFactionsClient.PayToll` -> `TollPaidRpc` (only from that orbit), the holder's bank,
  `NetPlayer.TollStation` for the others' NPCs; a toll the profile's saved game doesn't hold is refused: `TollRefusedRpc`
  gives the game its credits back and marks it unpaid again, `NetFactionsClient.OnTollRefused`: the game marks it paid
  as it sends it, so the holder's fighters spared a refused pilot until then); **trade cut** (`Hangar.Buy` through `NetFactionsClient.BuyPrice`: members -10 %, no VAT; the list shows the plain
  price); **VAT** (per claim, `Claim.vat`, 0-20 %, default 5, `/faction vat <rate> [station]` or the Faction tab's claim rows,
  officers): everyone else (other factions and factionless pilots) buys items and dealer ships at the list price + a tenth of
  the rate (`NetFactions.VatMarkup`: 20 % -> +2 %; ships through `Hangar.BuyShipPrice` / `NetFactionsClient.ShipBuyPrice`),
  the rate's share of the paid price goes to the holder's bank (`NetFactions.OnPurchase` from `StockItemRpc`, the price capped
  at the item's max with the markup; `OnShipBought` from `StockShipRpc`, the server's own ship price); selling pays no VAT;
  units players sold into the stock carry none when bought back (`NetStock` resale count, reset with each roll), so a
  buy-and-sell loop mints nothing; each stock roll keeps (100 - rate) % of every row and of the dealer ships
  (`NetStock.ThinForVat`); the claims line carries the rate (`NetState.Claims`: "station|TAG|Name|VAT"). **Garrison** (players' request: offline holders lost a station to one attacker in 5 minutes): `/faction garrison <ships 0-12> <level 1-5> [station]` or the Faction tab's claim rows (officers, not during the station's siege); upkeep 300 x ships x level a day from the bank (`NetFactions.GarrisonCost`, the first day when set, unpaid = disbanded; reset when the station is taken). In an armed siege the server counts it (`Siege.garrisonAlive`, out with `NetState.Sieges`' 7th / 8th fields), a dead one back after 90 s; the orbit authority flies that many (`NetOrbit.UpdateGarrison`, eventTag `NetOrbit.GarrisonTag`, system race, level 3+ strong fighters, hull x(1 + 0.5 (L - 1)), gun x(1 + 0.25 (L - 1)), no loot, swarming in front of the station; `NetFactionsClient.GarrisonRelation`: the attackers' enemy, the holders' friend) and reports each death (`NetState.GarrisonKillRpc`: the sender runs the orbit, at most 6 per 10 s); they jump out when the siege ends; the banner shows the count. Not yet: shared storage at the home. Not tested in a build yet.
  UI: the **multiplayer window** (`MultiplayerWindow`, code-built, Squad.uss buttons; in the station a "MULTIPLAYER" button in
  the top bar left of Menu; in flight its own button on the right under the HUD readout and the "Multiplayer window"
  binding, N (`FlightHud`: Esc / B / the binding close it, the flight controls and the mouse-steering cursor wait
  meanwhile, `Navigation.InputHalted`); under it in flight a Distress call / End the call button (in a squad in space,
  `MultiplayerWindow.UpdateSos`; or the unbound "Distress call" binding); these buttons and the squad window's never take
  the focus, so Space / Enter / a controller's A can't press them; the Squad tab has Distress call / End the call in
  space; its text fields (`TextFieldKeys.Guard`) keep their typed keys (no menu navigation or submit, Esc drops the
  focus) and turn the game's keys off while focused (`NetChat.SetTyping`); a snapshot waits while a field has the focus
  or a pointer is down on the window (a rebuild under the finger dropped the pressed button and Android's keyboard); both lists scroll by mouse drag too (`DragScroll`) and the chat follows new
  lines only while at its end; the Admin tab also on a session without profiles for its host (master) and session admins
  (`NetModeration.RoleOfClient`): kick, mute, session admins, settings, announcements (`NetCommands.ModerateWithoutProfiles`); a dot when an invitation, a challenge or an unread chat line waits; Esc / B closes it, the station
  menu's keys wait while it is open). Tabs Chat (the whole chat: lines, Local / Global, the line, Send; Enter sends,
  "/" commands; built once so the line keeps focus and draft; `ChatView` hides meanwhile), Squad (invitations, members
  with where / distress and Help, Leave, the pilots docked here to Invite; distress itself is called in space), Faction,
  Arena, Profile, Admin. Every chat command is listed in /help everywhere (on a session without profiles the faction /
  profile / moderation ones answer that they need a dedicated server with profiles); `/squad [invite <p> | accept |
  decline | leave]` and `/sos` say why not when they can't. The tabs give
  every chat command as buttons and fields (faction create / join / bank / members by
  rank / invite / territory with Claim, Make home, Give up, Siege at the docked station / leave and disband asked
  twice; arena challenge, accept / decline, the Voids option, the free-for-all queue, matches, leaderboard; profile,
  take control, link codes). `NetPanel`: the window asks for a snapshot (`PanelRequestRpc`, every 2 s open, 6 s closed
  for the dot), the server fills a `NetPanel.State` (`NetProfiles` / `NetFactions` / `NetArena .FillPanel`) and sends it
  gzipped in chunks (`PanelChunkRpc`); buttons send the chat commands (`NetPanel.Command`) and the window shows the
  answering notice. The chat commands still work.
- **Arena matches** (`NetArena` server, `NetArenaClient` player, `ArenaView` HUD panel; chat commands, answered
  privately): `/duel <name> [voids]` (both docked; `/accept` / `/decline` within 60 s; first to 3 kills or 5 min) and
  `/ffa [voids]` (a queue per option, docked; starts 30 s after the 2nd pilot or at once with 8; first to 15 kills or
  10 min), `/leave`, `/arena`,
  `/top` (the profiles' leaderboard: wins, kills / deaths, `NetProfiles.AddArenaStats`). A match is a private copy of
  the Void's home orbit (`NetArena.Template` = `Session.VoidOrbit`: its sky, fog, music, Void crystal asteroids): its own orbit id `NetArena.OrbitBase` (100000) + the match, which
  `SpaceLevel.NetOrbitId` gives NetPlayer / NetOrbit, so the same-orbit checks keep it to its players (asteroids seeded
  by that id). SpaceLevel's arena mode: no station or gate (no docking, no jumps: `JumpsBlocked`), no traffic (passive)
  unless the match has the Void fighters (`voids`: the orbit's own traffic, run by the first player in the match's
  orbit like any orbit's NPCs, the dead ones back every 45 s by `Traffic.UpdateAlienAttackers`; a Void kill scores
  nothing, dying to one is a respawn), no wingmen, no mission / siege / spy, the asteroids at the centre, the player on a ring of 8 spawn points 2 km out
  facing the centre (a duel's two opposite; a respawn the point farthest from the others), no mining (`Mining.UpdateLock`).
  Flow: `ArenaStartRpc` (the profile uploaded, the equipment noted, the take-off, the arena loads) -> `ArenaReadyRpc`
  (all in, or 20 s: the missing ones sent home) -> 5 s countdown (controls and guns locked by `NetArenaClient.Tick`) ->
  the fight (`ArenaStateRpc`: phase, time left, kills, a kill-feed line; kills from `DestroyedByRpc`) -> the end
  (`ArenaEndRpc`: winner or draw and everyone's kills / deaths, shown 6 s) -> docked where the match began. Destroyed:
  no game over, the arena reloads 3.5 s later (repaired, the equipment and ammo as at the start). Nothing is at stake:
  no uploads during a match (`NetProfileClient.Upload`), the loadout and the cargo (Void loot) restored at the end. Leaving / disconnecting: out;
  a duel goes to the one who stays, a free-for-all ends below 2 players. Server console `arenas`; `list` shows
  "in arena match N". Not tested in a build yet.
- **Squads** (`NetSquad`, the host's `NetPlayer.SquadId`, NetState's RPCs): the station's pilot list (players docked
  there, the local player first as "(you)") has Invite; the invited player gets an Accept / Decline popup (45 s); accepting joins the inviter's squad (a new
  one if needed, leaving the old one); squads form only in a hangar (the popup shows only while docked, and the host
  refuses an acceptance unless both are docked at the same station); joining abandons the joiner's own bar mission (the
  popup warns, `NetMissions.AbandonWarning`) and the squad's active one (the inviter's, else a member's,
  `NetPlayer.MissionHeld`) is sent to them; Leave in the squad window; a squad of one dissolves (also after a disconnect).
  Squadmates are green markers; the players' weapons don't affect a squadmate (`Target.playerProof`: no damage, and their
  bullets pass through, `Gun.Ignores`; a squadmate's replayed shots pass through the squad too). UI: `SquadView`
  (`Resources/GoF2Net/Squad.uss`) in the flight HUD and the station menu: the collapsible squad window (right side, only in
  a squad: each member, where they are, a shield bar and a hull bar with the armor over it like the HUD's), the station's
  collapsible pilot list, the invitation popup.
- **Trades between players** (remake, `NetTrade` on the server, `NetTradeClient` on each game, RPCs in `NetState`, rate
  kind `PlayerTrade`): only while both are docked at the same station. A request (the station pilot list's Trade, the
  Multiplayer window's Trade tab, `/trade <pilot>`) waits 60 s for Accept / Decline (a popup like the squad invitation's,
  `/trade accept | decline`); the trade then opens in the Multiplayer window's Trade tab for both: each side's offer is
  credits and cargo items (no ships, no mounted equipment, no story items `Session.Unsaleable`, no Courier containers or
  cabins 116 / 117; at most 16 kinds), sent whole 0.3 s after the last change and kept within the hold and the wallet;
  every change makes a new version and takes back both accepts, Accept carries the version it saw and waits 2 s after the
  other side's last change. Both accepted: the server asks each game to pay its own offer (`TradePayRpc`: checked, out of
  the wallet and the hold), and only once both paid sends each the other's (`TradeDeliverRpc`); a side that can't pay,
  doesn't answer in 15 s or leaves fails it and whoever paid gets their offer back (a late payment too). Either side
  cancelling, taking off or leaving the station ends it. Profiles: the recorded worth moves with what each gave and got
  (`NetProfiles.AdjustWorth`), so the upload check stays right; the credits move in the saved games (`MoveCredits`): each
  side's out as its payment arrives (`NetTrade.Debit`; at the end, a periodic upload sent while the other side still paid
  already had them out and they were taken twice), back if it fails (`Refund`, also for a side that left), the other
  side's in when it completes. Goods past the hold's capacity are allowed (a warning; the
  launch refuses as for the shop).
- **NPCs and the players** (the authority's `NpcShip` hooks, set by NetOrbit): every other player in the orbit is in an
  NPC's hit list (their ships block its shots; a non-hostile NPC's stray hit does 20 % like the local player's). An NPC
  is hostile to another player when its race always is (pirates, the Void, Specters), when a player of their squad shot it
  (`NpcShip.aggressors`, by client id; a remote player's hit is taken as an NPC's hit, so the authority's standing and
  kill count don't change), or when it is hostile to the authority's player and they are in its squad; a ship a squadmate
  shot is hostile to the authority's player too (`HostileToLocalBySquad`). It then attacks them (`UpdateTargeting`: after
  the local targets, kept while valid, hostile and in the box). The proxies share the aggressors and the always-hostile
  flag, so each player's markers show the hostility toward them; a takeover keeps the aggressors.
- **Orbits** (`NetOrbit` on each player's Space level): asteroids from the world seed per station (`NetGame.OrbitSeed`,
  `SpaceLevel.SpawnNetworkAsteroids`: `OrbitBuilder.SpawnAsteroids` only draws from `UnityEngine.Random`), the same field for
  everyone; destroyed ones are kept per station (NetState) and removed for a player arriving later, until nobody has flown
  in that orbit for 2 minutes (`NetState.AsteroidRespawnSeconds`, `RespawnEmptyAsteroidFields`, PR #118): then the whole
  field comes back (not at once, so docking to sell ore and launching again doesn't refill it). **Only
  the first player in an empty orbit builds its traffic** (`NetState.OrbitEmpty`, `SpaceLevel.NetAuthority`), so nobody
  sees ships appear out of nowhere; that player (the orbit authority) shows its ships and crates to the others as NetProxy /
  NetCrate objects it owns (spawned by the host on request, by index in `Traffic.Ships`). When the authority leaves the
  orbit (docks, jumps, disconnects: proxies survive their owner, `DontDestroyWithOwner`), the player still there with the
  lowest client id takes over its flying ships where they are (`Traffic.Adopt`: model, race, group, hull; not fixed
  objects, turrets or story ships; only ships whose creator has left the orbit, `NetProxy.Creator`), connects them
  (`Traffic.ConnectPlayers`) and runs the orbit from then on (`SpaceLevel.TakeOverNetAuthority`: relaunches and raider
  waves, `Traffic.SetPassive`); the old copies go for everyone at once (`NetState.AdoptedRpc`), else NetState sweeps them
  after 5 s. Two players arriving at the same moment can both find the orbit empty: in the first 15 s the higher client
  id stands down (`SpaceLevel.DropNetAuthority`: its traffic vanishes, the other's is shown). A player's own proxies left
  from an earlier visit, or spawned after their ship went, are dropped by their owner. The Kaamo siege: one per orbit
  (`KaamoSiege.Running`, `NetPlayer.SiegeRun`): another player with the siege to fight gets the follower view (the call,
  docking refused, the win with the runner's, `NetState.SiegeWonRpc`), builds it anew when alone there; two runners at
  once: the higher client id stands down. The other players' hostile ships count for the battle music and block
  fast-forward (`Traffic.HostileCount`).
- **Mining** (the asteroids are the shared seeded field, destruction synced): several players may drill the same asteroid
  at once; each drills their own minigame, and the ore is split between them (`NetPlayer.MiningAsteroid`, the field index;
  `Mining` divides the payout by the most players drilling it at once during the session, at least 1 t for some ore).
  The first to finish (or anyone shooting it) destroys it for all (`NetState.AsteroidGoneRpc` with the pilot's name): the
  others' drilling ends with "Mined out by X." (drilled out: `Mining.MiningOut` during its explosion) or "X destroyed the
  asteroid." (shot, rammed, a blast) and pays their share of what they drilled (`NetOrbit.DestroyedBy`); one still
  approaching or landing gets the same message and undocks. An asteroid a player lands on, sits on or drills stops
  spinning for everyone there (`NetPlayer.LandedAsteroid`, `NetOrbit.UpdateHeldAsteroids`), and spins on once nobody is
  on it (a miner's own undock leaves it still while another player is on it, `NetOrbit.OthersOn`).
- **NetProxy**: the ship's model by its Resources path (`NpcShip.ModelPath`), pose, race, standing, hull, hit cube, hidden,
  life (dying: no marker / lock; dead: the explosion at the ship's scale, the model hidden unless it leaves a wreck;
  flying again after a relaunch), marked and locked like traffic ships (`Target.NetShips`, `CombatRadar`, `CombatView`);
  another player's hit is applied to the owner's ship (`Target.RemoteDamage`, an RPC to the owner) as its player's hit.
  Dying / dead copies are not alive (shots pass them, like on the owner's side); junk is known from its id
  (`NetOrbit.JunkBase`); a jump (the spawn's default pose, a respawn) is no velocity (`NetSmoothing`). The host holding a
  disconnected player's proxies shows them like any other (viewer mode, `OnGainedOwnership`). Other players' wingmen are
  proxies too: the authority's NPCs attack them like their player (`NetOrbit.HostileToRemote`), and a player's wingmen
  attack the other games' hostile ships (their hits applied by that game).
- **Shots** (every gun, the turret, rockets / missiles / bombs / mines / beams): `Gun.Fired` / `Gun.Ignited` ->
  `NetShotSender` (a NetProxy's owner, the owner's NetPlayer) -> an RPC to the others (shots unreliable, blasts reliable)
  -> `NetShotMirror` (only where the shooter is shown): a Gun per weapon item with its GunRig and shot sound, the bullet
  injected at the sender's pose and life left (`Gun.Inject`), beams through the gun's beam path at the same target,
  homing toward the sender's lock (ids: `NetShots`), blasts as the fx's explosion. The mirrors stop on what they hit here
  and show the impact, but deal no damage (no handler on their Hit): the shooter's own game applies the hits. Their pools
  live in the scene: after a scene change the mirrors start over (`NetShotMirror.Refresh`). Deployed sentry guns' shots
  are mirrored too; a beam's auto-aim never picks a squadmate.
- **Crates** (`NetCrate`): the authority's crates are real `Crate`s for the others in that orbit (`remote`: no drift or
  expiry of their own; the race's model, the same loot), so their radar, markers and tractor work. One player gets it:
  the host keeps the claim (the first beam to start pulling, `Crate.PullStarted`), the others' copies are
  `claimedByOther` (the radar leaves them, a beam on one lets go), the claimant's capture waits for the confirmation
  (`captureBlocked`; the owner's own capture only waits for another player's claim); another player's capture
  (`Crate.CapturedHere`) has the owner remove its crate; the claim goes back when the claimant's beam lets go or they
  leave the orbit (`ReleaseRpc`, the host's check); an owner's beam that grabbed it before the spawn claims it then.
  Mission loot (`Crate.missionLoot`: dropped by mission ships and junk, the Hijacker's container) is out of reach for
  players outside the mission's team (the host refuses their claims). Cargo-steal crates aren't shared. The Hijacker's
  container never expires (also single player: Recovery / Salvage were stuck after 60 s).
- **Hangars** (`NetHangar`, HangarTraffic's guests by client id): the other players docked at the local player's station
  park on the NPC slots. Docking here from this orbit flies in (only when that player flew in themselves,
  `NetPlayer.ArrivedFlying`; a session start or a respawn at the station just appears on a pad),
  their take-off flies out (queued with the other flights: one in the air at a time, the local player's included); one
  already docked on arrival, or with the flights off, is simply parked; gone another way = gone at once; a new ship is
  swapped in place; the mounted turret shows on it (`NetPlayer.TurretItem`, `PlayerTurret.BuildStatic`, also on the ship
  in space). The hangar's NPC ships are the same for everyone docked there: the first player there runs them
  (`HangarTraffic.RunsNpcs`, `NetPlayer.HangarRun`) and sends each landing / take-off (`NetState.HangarNpcRpc`, NPC ids
  `Parked.key`: the slot, or (client id + 1) * 1000 + n for new ones); a player docking later starts with none and takes
  the runner's (a snapshot, `HangarTraffic.ApplyNpcSnapshot`); when the runner leaves, the lowest client id there runs them
  on (the others take a new snapshot); two runners at once: the higher id stops. The NPC ships keep one pad free
  (`KeepOneFree`: they never land on the last one, and when a docking player takes it one takes off; a guest's landing
  waits for a pad instead of an NPC ship vanishing). With only players on the pads a new guest waits for a free one
  (still in the pilot list). In multiplayer the hangar keeps a HangarTraffic for the guests even without the NPC traffic.
- **Join / leave notices** (`NetPlayer`): a join shows in the others' chat only after the player has stayed 15 s, and not
  when they left under 2 minutes before (a relog); a leave only when their join was told, once per 2 minutes per name.
- **Event ships that vanish** (`EventRunner.CountShips`): a batch counts its ships until each is seen dying or dead
  (`Batch.down`), not just the ones present: a player leaving the orbit right after the spawn unloads their traffic, and
  Pirate Hideout paid out in 13 s (a log of 2026-10-12). The batch keeps the orbits it was seen in, so the players leaving
  them still ends the event (`CheckEmpty`; a mission fails). A completion logs its time and the players' kills.
- **Chat** (`NetChat`, `ChatView` in the flight HUD and the station menu; styles `Resources/GoF2Net/Chat.uss`; placed in percentages; on a small high-density screen (`UiScale.Large`: body text under 2.8 mm from the panel scaling and dpi, e.g. the Retroid Pocket G2) the chat, the squad window / invitation and the multiplayer window use their large variants (`.chat--large`, `.squad--large`, `.mpw--large`: about 1.5x, finger-sized buttons, the window 96 x 94 %); the on-screen keyboard shows its own input box (`hideInput` false: hidden, holding Backspace stopped after a letter); the lines never select all on focus (after a send the next line showed highlighted); the multiplayer window: LB / RB or Q / E switch tabs, a controller starts on the tab row, the list scrolls to the focus and a rebuild restores it, a phone typing raises the window to the top half, the Chat tab's line takes the focus only with keys and mouse): global
  (everyone) and local (the same orbit, or docked at the same station); the host stamps each line with the sender's name
  and location. The chat key (B) or the small Chat button above the lines (`.chat-tab`, a finger-sized button that opens on the press;
  the key shown after its name, not on phones) opens the input with the cursor in it (`ChatView` keeps focusing the
  field for a few frames until its row shows; the opening key's letter, which arrives after the focus, is dropped), the
  send key (Enter / keypad Enter, `GameControls.ChatSend`) sends, the channel key (Tab, `GameControls.ChatChannel`)
  switches Local / Global, Esc closes. Sending keeps the chat open for the next line (`ChatView.SendLine`: the field keeps
  the focus, a phone's keyboard comes straight back up), so a conversation needs no reopening; Enter / Done on an empty
  line, Esc / B, the row's × or the Chat tab close it. Phones: the field's own keyboard is off (`hideSoftKeyboard`) and `ChatView` opens
  the on-screen keyboard itself (`TouchScreenKeyboard`, its input box hidden, the text copied into the field each frame):
  Done / the checkmark sends, Back or a tap outside ends the typing with the draft kept (UI Toolkit's own keyboard only
  closed and blurred the field on Done, so nothing was sent); while typing the chat sits at the top of the screen
  (`.chat--phone.chat--open`, the tab hidden, the "/" suggestions capped) where the keyboard can't cover the line (at 36 %
  down the input row was under it); the send and channel keys are rebindable rows read straight from the devices
  (`GameControls.PressedNow`: the flight map is off while typing), and their key events and characters stay out of the
  line. The field keeps the focus: the project-wide UI map's Navigate (arrows, W A S D), Tab and Submit (Space / Enter)
  are swallowed there (`StopPropagation` + `focusController.IgnoreEvent`: stopping alone still moved the focus to a menu
  button, which ended the typing). Enter / keypad Enter always send, read from the key event (the rebindable send key is
  read from the device and could miss the UI event's frame: the TextField then took Enter as its submit, lost the focus,
  and the line was only hidden by `Suspend`); a Send button after the line (touch, mouse). Another player's line plays the original's
  incoming-message sound (FMOD 125 Message_Inc, volume 0.241 × `Sfx.EventGain` × the FX volume, one at a time; a copy
  of the clip in `Resources/GoF2Net/ChatMessage.ogg`). Lines fade 12 s after arriving (full width on a solid background: drawn over the flight HUD's key hints, it covers them); join / leave notices; every line
  is also in the player log (`[Chat ...]`). **Chat commands** (`NetCommands`): a line starting with "/" is a command, never
  sent as chat. Every command that acts on the session runs on the server: the chat sends its name and arguments
  (`NetState.ServerCommandRpc`), the server checks the sender's rights (`allowed`) and runs it, the answer is a notice for
  the sender; the dedicated server's console runs the very same table (`NetCommands.RunOnServer` with no issuer: every
  right, named "Server"; `list` = players, `say` = g). Only what changes or reads this game stays local: `/help` (the
  commands this player can run, `available`), `/netstats`, `/pos` (the orbit and the game coordinates /tp takes), `/sos` and `/assist` (`NetDistress`). The arena (`/duel` ..., `/leave` leaves a queue or match before the squad), profile (`/link` ...), faction (`/faction`, `/c`) and moderation commands are rows of the same table calling their own handlers; the station window's buttons send them as chat lines, which `SendChatRpc` runs through `NetCommands.RunOnServer`. Server
  commands: `/players` (where, ship, squad, admin; admins and the console see the client ids), `/g` / `/l <text>` (one line
  to Global / Local), `/w <player> <text>` (a private message to that player only, "[From X]" / "[To X]" in violet, not
  logged), `/invite <player>` (docked at the same station) and `/leave`; admins `/kick <player> [reason]` (never the host's
  player or the issuer, only the host / the console kicks another admin; everyone gets "X was removed from the session by
  Y"), `/tp [players] <player | station [x y z | dock]>` (`NetTeleport`: players (the chat: yourself by default) to another
  player (beside their ship, or into the hangar they are docked in), an orbit by station index, name or "void" (the launch
  spot, or game coordinates facing the station) or a station's hangar ("dock"); the server sends the order to that
  player's game, which moves its ship in place within the same orbit (`SpaceLevel.MoveForTeleport`; not while mining /
  object docking / jumping) or loads the orbit / station like a jump (`SpaceLevel.TeleportOut`, the pose through
  `NetTeleport.TakePose` in `SpawnPlayer`, no launch camera); refused while destroyed or leaving) and `/tphere <player>`;
  the host (and the console) `/admin` / `/unadmin <player>`. Admin tools (`NetAdmin`: the server checks the rights and arguments, logs the
  order and sends it to each target's own game, `NetState.AdminRpc`, server-only; in the chat the players may be left out
  = yourself): `/kill [players]` (in space, through god mode, "X was destroyed by Y" for everyone), `/heal [players]`
  (`Cheats.Repair`), `/give [players] <item> [amount] [mount]` (an item's number or name, 1..1000; "mount" docked:
  `Cheats.GiveAndMount`), `/credits [players] <amount>` (negative takes, never below 0), `/spawn [players] <ship | object>
  [race] [count] [enemy | friendly | neutral | standing] [turrets <item,item>] [at x y z]` (`DebugSpawner`: a ship's number or name as that game's
  traffic, the race by default its maker's else pirates, 1..10 side by side, enemy by default; "turrets": turret items on the
  ships' turret mounts, `NpcMountedTurrets`, carried in the order's text after '~'; neutral = the remake's
  `SpawnSpec.alwaysNeutral` / `NpcShip.alwaysNeutral`, neither side whatever the standings until turned or shot, also toward
  the other players (`NetOrbit.HostileToRemote`); a name that is no ship is an assemblies.json object as scenery; "at" = game
  coordinates in that player's orbit, else ahead of them; "named <name>" (to the end or up to "at x y z", at most 32
  characters, cleaned like a chat line; in the order's text after a `|`): a ship's lock-plate name (`SpawnSpec.name`,
  numbered "Name 1", "Name 2"... for several, the others see it through `NetProxy`'s label), an object a HUD marker
  (`Navigation.Kind.Marker` on the model's centre: bracket, name and distance near the crosshair like a landmark, never a
  lock candidate; local to that player's game); the event graph's Spawn node has a Name port for it, empty = none, old
  graphs without the port compile as before), `/ship [players] <ship | own>` (`PlayerHull.Fly` / `Restore`: any
  hull of the debug Ships tab, docked only ownable ones), `/ammo`, `/reveal`, `/peace [players]`, `/cheat [players] <god | ammo |
  cooldown | primary | boost | onehit | locks | shopping | jumps> [on | off]` (`Cheats.Grant`: that player's flag for the session, not
  saved, whatever the session allows), `/mute <players> [minutes]` / `/unmute` (the server drops their chat and whispers;
  never the host's player, only the host / console mutes an admin), `/title [players] <text> [| subtitle] [for <seconds>]`
  ("clear") and `/timer [players] <seconds | m:ss> [label]` ("stop"): `EventScreen`, its own panel over every scene, a big
  title with a subtitle in the upper middle (4 s by default, fades) and a countdown at the top centre (the last 10 s amber),
  cleared when the session ends. `/dialog [players] <speaker> : <text> [| [speaker :] page ...]`: the scene's dialogue window
  (`DialogueView.Latest`, queued behind one already open, `EventScreen.QueueDialog`), each page with its speaker (a page
  without one keeps the last): a story speaker by name (`StoryTable.SpeakerName`; "Keith as Bob" renames it), a race and a
  name ("vossk K'ekki", "terran female Jane": one `AgentGenerator.CreatePortrait` face made on the server, the same for
  everyone and every page), or "player" (the reader: speaker 0's face with their pilot name); %player% in a text or name is
  the reader's name; a page `reward [title]: <rewards>` pays when the dialogue closes (like a single-player mission's success page). `/reward [players] <credits | item [amount]> [+ ...] [| title]`: credits and / or up to 8 items into the hold, shown in the reward box (`EventScreen.ShowReward`, `Layout::showMissionRewardMessage` / `drawMissionRewardMessage` 0xe7684: the title, default 216 "Mission accomplished!", over "+ credits" and each item with its shop icon, centred, fades in 2 s, holds until 5 s, fades out until 7 s, sound 36). Typed commands may be 500 characters (`NetChat.MaxCommandLength`; chat lines stay 160).
- **Where the event graphs live** (they began as multiplayer events and now run single player's quests and bar missions too):
  `Scripts/Runtime/Net/Events`, namespace `GoF2Remake.Events` (was `Multiplayer`: `NetEvents` is `EventRunner`, the `NetEvent*`
  helpers `Event*`, `NetScreen` `EventScreen`); graph files `.gof2event` (the old `.gof2netevent` is still read by the game:
  `EventRunner.LegacyExtension`, the Events folders and mods' `events/`; imported into the project, `EventGraphUpgrade`
  renames one with `AssetDatabase.MoveAsset` (its GUID kept) and rewrites its enum option types `GoF2Remake.Multiplayer.Event*`
  -> `GoF2Remake.Events.Event*`, which Graph Toolkit stores by name; the game's reader ignores them); the built-ins and the
  event audio in `Resources/GoF2Events`. The chat command layer they run (`NetCommands`, `NetAdmin`, `NetTeleport`) stays in
  Multiplayer. Verified: every graph re-imported without a reader / Graph Toolkit mismatch, a template opened and compiled by
  Graph Toolkit, an old-format graph converted on import, a mod's old .gof2netevent and the built-ins loaded at runtime.
- **Events** (`EventRunner`, `/event <name [setting=value ...] | stop | list>`, admins and the console only): node graphs run
  on the server: `<name>.gof2event` (see Event graphs below) in an Events folder (`persistentDataPath/Events`,
  or next to the game / dedicated server's executable), else a mod's (`events/`, the mods that are on), else a built-in one
  (`Resources/GoF2Events`: waves, survival, the bar mission pirate_hideout); one global event at a time (/event: everyone) and any number of bar missions beside it
  (each run its own `EventRunner.EventRun`: threads, handlers, questions, batches, points, scoreboard, what it turned on);
  `/event list` shows each one's settings. There are no script files: the graph compiles to an internal line form, which is
  what runs. Its lines: any server command without the "/" (run as the server, "{expression}" parts filled in), `wait <s>`,
  `wait until <condition> [timeout <s>]`, `if` / `else` / `end`, `while` / `end`, `repeat <n> [as <var>]` / `end`,
  `set <var> = <expression>`, `param <var> = <default>` (a setting), `parallel` / `end` and `every <s>` / `end` (threads, 16 at
  most; the event ends with its main flow), `on <trigger> [station]` / `end` (a handler: died, docked, launched, joined, left,
  entered, respawned, kill (an event ship destroyed, its killer), pvpkill (a player destroyed another: the killer, `%victim%`
  the other; from `NetState.DestroyedByRpc`), cleared (every spawned enemy gone), hacked (the player hacked a wreck, its name
  as `%victim%`: `SpaceLevel`'s `Docking.HackWon` -> `SpaceHooks.WreckHacked` -> `EventRunner.OnWreckHacked`, in a session
  through `NetState.WreckHackedRpc`, checked against the sender's orbit); each firing runs the block as a thread with
  that player as `@trigger` and `%trigger%`; polled every 0.25 s, `DetectTriggers`), `ask <players> <s> | [speaker :] question
  | answers` with `answer <k>` / `end` blocks (each answer runs its block as a thread for that player; the flow waits for all
  answers or the time), `vote ...` with `choice <k>` / `end` blocks (the most picked answer's block, a tie at random, `choice 0`
  when nobody voted), `startevent <name> [setting=value ...]` (ends this event, starts that one with the same starter; 5 a
  second at most), `points <players> <n>`,
  `scoreboard on [title] / off` (every player's screen, `NetAdmin.Order.Scoreboard` -> `EventScreen`, the top 10 by the score
  mode, sent when it changes and every 5 s), `stop`, `score <kills | time | points>`, `winner [kills | time | points]` ("The
  winner is X with N kills" on screen and in the chat). Each line may end in `#@<node id>` (the live view). Expressions:
  numbers, variables, arithmetic, comparisons, and / or / not, random(a, b), min, max, floor, count(<players>) (a selector's
  players), and the state: `enemies` / `ships` (the event's living spawned ships: each spawn order gets a batch tag,
  `SpawnSpec.eventTag`, written on its NetProxy with the enemy bit, `NetProxy.EventTag`; a just-sent batch counts until its
  ships show, 6 s at most), `players`, `inspace`, `docked`, `dead`, `time`, `toppoints`, `missionstation` (where a bar mission was taken, -1 in an /event). Results: an event ship's killer
  (`NetProxy.Killer`: its own game's player, or the shooter of a remote hit) counts once per ship (by owner and traffic index:
  proxies are remade and Netcode reuses ids); time alive in space from the first spawn until a player's first death; points.
  Once it has spawned ships, an event ends by itself when no player has been alive in space in its ships' orbits for 3 s
  ("Event over" with the winner; `set autostop = 0` turns it off); its music, free for all, respawn points and travel
  restrictions end with it. Ticked by `NetState.Update` on the server; reset per session.
  **Questions** (ask / vote; `NetAdmin.Order.Ask` -> `EventScreen`): the Space Lounge chat's look (header with the speaker's name
  and the seconds left, the portrait beside the text, the answers as stacked station buttons; inline styles, the speaker
  resolved on the server as /dialog's, `NetAdmin.ResolveSpeaker`); answered by a click / tap, 1-4, a controller's A B X Y, Esc /
  View skips (`NetState.AnswerRpc` -> `EventRunner.OnAnswer`); while it shows `EventScreen.QuestionOpen` rests the flight controls
  (`Navigation.InputHalted`), the keys (`NetChat.Keys`) and the menus (`GameControls.BlocksMenus`).
  **Free for all** (`/pvp on | off`, admins; the Free For All node): `NetState.FreeForAll` (a NetworkVariable) makes every
  other player an enemy (`NetAggression.IsHostile`; squadmates excepted). **Respawn points** (`/respawn [players] <station
  [x y z] [spread <units>] [delay <s>] | off>`; Set / Clear Respawn Point): a destroyed ship comes back in space, repaired,
  after the delay (`EventRespawn`, `PlayerHealth` after the explosion, `NetTeleport.Respawn`; default 3 km in front of the
  station, `NetTeleport.RespawnSpot`: the launch spot is inside the bigger stations) instead of "Tap to respawn at the
  station". **Travel restrictions** (`/restrict [players] <jumps | docking | all | off>`; Restrict Travel): `EventRules`,
  in `Navigation.JumpsBlocked` (planet jumps, Khador Drive), `SystemJump.GateBlocked` and `SpaceLevel.DockingBlocked` (525).
  **Make Hostile** (`/provoke [players] [race] [within <m>]`): the NPC ships around each player (NetProxy positions on the
  server) turn on that player and their squad: the ship's own game adds them to `NpcShip.aggressors` (neutral spawns too;
  `NetOrbit.HostileToLocalBySquad` counts the local player's own id). **Radio** (`/radio [players] <speaker> : <text>`):
  a line in the flight HUD's radio box (`Traffic.Say`, queued after the waiting ones), speakers as /dialog's; docked: a chat
  line. **Waypoints** (`/waypoint [players] <station x y z | off>`; Set / Clear Waypoint): `EventWaypoint`, a one-point
  `Route` through `Navigation.SetRoute` (lockable "Waypoint", the autopilot), applied whenever the player is in that orbit,
  cleared on reaching it and at the event's end; the On node's Player Arrives (`on near <station> <x y z> <metres>`) fires
  when a player alive in space comes that close (server-side, NetPlayer positions). Spawned objects get collision
  (`DebugSpawner.SpawnObject`: `station_pirates` the original's volumes 1002 unrotated, others a box around the model) and
  exist only in the games they were sent to: spawn scenery for `@a[orbit=...]` at fixed coordinates.
  **Ship orders** (`EventShipOrders`, `NpcOrder`; NetAdmin order Npc 29, `/npc [players] <ship> <order>`; the server checks
  the text with `EventShipOrders.TryParse`, each target's game applies it to its own traffic ships of that name (Target.
  displayName, "Name N" of a numbered spawn too; proxies just mirror)): goto / route (places "/"-separated, loop), follow /
  escort (leader player | ship, offset right up forward in the leader's frame), attack (player | ship, regardless of
  standing), flee (from the player, then jump out), dock (into the station, vanish), hold (parked), resume, jump, speed;
  options speed / radius / fight / then hold | resume | vanish | jump / seconds; places through `EventCutscene.ParsePlace` /
  `ResolvePlace` (read when the order arrives; follow / attack keep their Transform / Target). `NpcShip.SetOrder` /
  `UpdateOrder` (before the freighter and targeting branches, after the story's scriptedSpeed): steering through the AI's own
  `Steer` / `Avoid` (Dock skips the landmark avoidance), `drift` cleared; a group spreads (`Spread`: 900 to the side, 600 back
  per rank); Follow aims 3000 units ahead along the leader's heading and closes the gap by speed (boost beyond 6000);
  remake: slower while facing away from the goal (x0.35..1 by alignment, not under 0.6 u/ms) so the original's fixed turn
  rate (a circle of speed / 0.00073 units) stays tight; escorts run `UpdateTargeting` and fight first. Exits by order
  (`JumpOut`, `LeaveQuietly`) set `Target.killedByNpc`, so no event credits a kill; `Traffic.UpdateOrbit` /
  `UpdateAlienAttackers` / the jumpers never revive a ship with an `eventTag`. Expressions `alive(name)` and
  `distance(name, x, y, z)` (`EventRunner.ShipState`) read `EventHost.Ship.name` / `position` (game units; NetProxy's label
  and pose in a session). Graph nodes (category Ships): Fly To, Fly Route, Follow (Escort), Attack, Ship Action
  (`EventShipThen`, `EventShipAction`; base `EventShipOrderNode`: "EventShipNode" is Change Ship's saved name). Verified in
  Play mode (single player): a pair flown ahead of the player and held, following in formation, docking (gone, no kill, not
  relaunched), a drone fleeing and jumping out, the expressions.
  **Cutscenes** (`EventCutscene`, its own DontDestroyOnLoad singleton; NetAdmin orders Cutscene 25 / Camera 26 / Fade 27 /
  Letterbox 28; commands `/cutscene [players] <start [nobars] [freeze] [invulnerable] | end>`, `/camera [players] <place [to
  place] [over s] [look place] [follow] [fov deg] [shake 0-1] | chase>`, `/fade [players] <out | in> [seconds] [rrggbb] | clear`,
  `/letterbox [players] <on | off>`; the server checks the text with `EventCutscene.TryParseShot`): places `player [right up
  forward]` (game units in the ship's own frame, unscaled), `station [x y z]` / `x y z` (orbit game coordinates), `ship <name>
  [x y z]` (a `Target.displayName` from Spawn's "named", "Name 1" numbering accepted; "quoted" names with spaces). Cinematic
  mode: `SpaceLevel.Cutscene` is also `EventCutscene.Cinematic` (the HUD's `hud-cinematic`, touch PauseOnly, no free look /
  photo mode), `Health.invulnerable` with "invulnerable", the player's `inputLocked` and `Weapons.Blocked` (released only when
  it set them and the launch camera is over), "freeze" = `externalControl` at speed 0, the autopilot off once; it survives a
  scene change (re-applied to the new level), a shot doesn't (bound to the scene it was sent in). The shot runs in LateUpdate
  (`DefaultExecutionOrder(5000)`: after the chase camera, before `VrRig`) with the ChaseCamera off (`scriptCamera`): position
  lerped from / to with smoothstep over the seconds, LookRotation to the look place every frame (world up), FOV through
  `Aspect.VerticalFov`, shake = look-point jitter × `Settings.CameraShake` + `Haptics.Rumble`; `camera chase` / `cutscene
  end` snap the chase camera back. Fade and bars (11 % of the height each, 0.6 s, smoothstep) on a panel of their own at the
  shared panel settings' sorting − 1, under the HUD, so the radio and conversations show over a black screen; docked too.
  All of it is cleared when `EventHost.ScreensActive` goes false; a run that sent one (`NoteCutsceneSet` / `NoteFadeSet`)
  sends `cutscene end` / `fade clear` at its End. Graph nodes (category Cutscene): Start Cutscene (Letterbox, Freeze ship,
  Invulnerable), Camera Shot (From / To / Look At text, Seconds, Fov, Shake, Follow; the compiler quotes a ship name with
  spaces, `EventGraphScript.Place`, and reports a bad shot on the node), Chase Camera, End Cutscene, Fade (`EventFade` Out /
  In / Clear, Seconds, Colour: the "#" is dropped, it starts a comment in the line form), Letterbox. Verified in Play mode
  (single player, line-form runs): the bars, the hidden HUD, the fades, a moving followed shot around the player, a station
  shot, a shot on a spawned "Pirate Boss", freeze and invulnerable, everything restored at the end; the nodes through
  `EventGraphScript.Compile` on an in-memory graph.
  **Sound and music** (`/sound [players] <sound>`, `/music [players] <track | stop>`, admins; the graphs' Play Sound / Play
  Music / Stop Music): `EventAudio` (`Resources/GoF2Events/EventAudio`, **GoF2 > Build > Event Audio**, the original FMOD
  events: alarm 162, warning 35, success 36, explosions, jumpgate, Khador...; tracks battle, boss 151, void, specters, intro,
  outro, the race themes...); a track loops on `EventScreen`'s own source and the scene's music fades out meanwhile
  (`EventScreen.SceneMusic` in `Traffic` and `StationLevel`).
  **Event graphs** (`.gof2event`, Unity's Graph Toolkit module, built in since 6000.4; Editor side in
  `Scripts/Editor/Events`): the only form of an event. Assets > Create > GoF2 > Event Graph, or Event Graph From Template
  (King of the Hill, Boss Fight, Free For All, Pirate Base, Pirate Hideout (a bar mission), Quiz, Race, Vote For The Next
  Event, Waves, Survival in
  `Scripts/Editor/Events/Templates`: copied with every graph / node /
  variable id renewed, `EventGraphMenus.NewIds`: a plain copy keeps Graph Toolkit's ids). Nodes: Flow (Start, Wait, Wait Until,
  Wait Forever, If, While, Repeat, Run In Parallel, Every, Random Branch with weights, Ask, Vote, Set Variable, Stop, Comment),
  Triggers (On: no flow in, its Do runs on each firing; Orbit limits it; Player / Name / Victim outputs into Players / texts),
  Event (Score, Winner, Add Points, Scoreboard, Mission Complete, Mission Failed, Start Event (with a picker of the project's
  events), Free For All, Set / Clear
  Respawn Point, Restrict Travel, Make Hostile, Set / Clear Waypoint), Commands (one per event command, Radio, Play Sound /
  Music, Stop Music, Command for any other line), Ships (Fly To, Fly Route, Follow, Attack, Ship Action), Cutscene (Start / End Cutscene, Camera Shot, Chase Camera, Fade, Letterbox),
  Players (Get Orbit: a station by number, name, void or a wired number -> Get Players (who, optionally in that orbit) -> any
  command's Players, written as `@alive[orbit=Var Hastra]`; Count Players), values (Game State, Math, Random, Floor, Compare,
  Logic, Not, Expression), the blackboard's Number variables (set to their defaults at the start; Input ones are the event's
  settings). Local sub-graphs (Graph Toolkit's "Create Local Subgraph"; stored in the same file) are inlined where their node
  is: give them Flow input / output variables (the flow enters by an input, goes on after the node from the output it
  reaches) and Number inputs (what is wired into or typed on the sub-graph's node); asset sub-graphs can't run on a server
  ("Unpack To Local Subgraph"). Name pickers under Spawn / Give Item / Reward / Change Ship / Get Orbit / Teleport
  (`EventGraphPickers`, NodeViews with searchable lists of the server's names, `EventNames`); typed names the server wouldn't
  know are warnings on the node. The live view (`EventGraphLiveView`, Play mode): the running event's graph (by name) shows
  each flow's current step (Graph Toolkit's GraphVisualization: an animated accent, a wait's progress as the fill) and the
  variables' values on their nodes (`EventRunner.ActiveSteps` / `Variables`). The server reads the file itself:
  `EventGraphFile` (a small reader of Graph Toolkit's serialized model, Unity YAML: nodes with their port / option values,
  wires, variables (m_Modifiers 0 local / 1 input / 2 output), local sub-graphs; node ids as Graph Toolkit's Hash128) and
  `EventGraphScript` (the one compiler, graph -> the line form above; the Editor's checks and Run In Play Mode feed it the
  live graph through `EventGraphCompiler`). The importer depends on the GoF2Data JSON files `Database.Load` reads (`ctx.DependsOnArtifact`: in a fresh Library the
  graphs were checked against empty tables) and keeps the file's text as its TextAsset (so `Resources/GoF2Events`
  graphs are the built-ins) and reads every graph both ways, warning if the game's reader disagrees with Graph Toolkit (a
  Unity update changing the format), and about a name with spaces (/event takes one word). Node kinds, port and option names
  are the Editor classes' (`EventGraphNodes`), the option enums the game's. The right-click "Event" menu (and Assets > GoF2 >
  Event Graph): export the graph (a file or the game's Events folder), run it in a hosted Play-mode session
  (`EventRunner.StartText`, server only). Graph Toolkit's toolbar API isn't public, hence the right-click menu.
  Admins: the host's own player, or players the host or the
  dedicated server's console made admins (`NetPlayer.IsAdmin`, server-written, for the session only: names aren't
  verified). Players are named whole, any case (the longest name the arguments start with), by a client id as the first
  word, or by a Minecraft-style selector (`NetCommands.FindTargets`): @a everyone, @s yourself, @p the nearest other player
  (same orbit by distance, else the same station), @r a random other player, and by state @alive (in space, not destroyed), @space, @docked, @dead, @survivors (an event's players never destroyed since its fight began, `EventRunner.Survived`: "reward @survivors {wave * 1000}"), each narrowed to one orbit by `[orbit=<station>]` (in its space or docked at its station: `@alive[orbit=78]`, `@r[orbit=Var Hastra]`); a command on several players runs for each
  ("/tp @a Player1", "/kick @r"); a destination is one player. Tab offers the selectors too. Typing "/" lists the matching commands over the line, after a command that
  takes a player the matching players; Tab completes the first and cycles through them (Shift+Tab back, "/" alone cycles
  all; before the channel key, Tab by default; `ChatView.Complete`, `NetCommands.Completions`);
  `/netstats` shows / hides the network stats (`NetStats`, PlayerPrefs `mp_netstats`, top left in the flight HUD, the
  station menu's top right under its Menu button, `ChatView`): host / client (dedicated server), Relay or direct, the client's ping, jitter and packet loss
  (Unity Transport's `GetConnectionStatistics` on the host connection), the host's ping per player (the first 3, then "+N more"), the data in / out per
  second and in all (`NetTransport`, UnityTransport counting Netcode's payloads, without the transport's / Relay's
  headers) and the players. While typing the game's keys
  are off: the code-made InputActions are disabled and the direct keyboard reads go through `NetChat.Keys` (null then;
  FlightHud, ShipController, Mining, SpaceLevel, StationMenu, PauseMenu), the station menu's navigation too.
- **Leaving**: closing the game leaves the session (`Application.quitting`); a player whose game died is dropped after 5 s
  (`DisconnectTimeoutMS`); a leaving player's ship goes at once. The host leaving with players connected first tells
  them why (`NetState.SessionEndingRpc`, "The host ended the session.") and closes 0.35 s later (`NetDelayedShutdown`;
  closing the window waits for it through `Application.wantsToQuit`, then quits). A session ending while playing takes a
  client back to the main menu's Multiplayer panel with the reason in a popup (`NetGame.PopupPending`, taken when the
  menu opens, `MainMenu.ShowNotice`); Netcode's own "[Disconnect Event] ..." texts become "The connection to the host was
  lost." / "No host found at that address." ("The host ended the session." when the host answered first); `-mpjoin`
  clients then keep trying to rejoin. The flight HUD's and the station menu's Back to Main Menu end the session. Netcode
  stopping a session by itself (`OnClientStopped`, a transport failure) is handled like a lost host. While the host
  closes, `NetGame.Active` is false and new connections are turned away with the reason. `NetGame.SessionGame`: a
  session's game (from PrepareSession until the menu opens after it) is never saved, even once the connection went, and
  a Space / Station scene loaded after the session ended goes to the menu (`NetGame.SessionLost`). The main menu always
  unmutes the listener (a pause menu open when a session ended). Every session plays on Normal difficulty.
- **Other players' ships look and sound like theirs**: the owner writes its engine state (the glow part showing: off
  while mining, docking at an object, dead), boost (`BoostVisualPercent`) and cloak percentage; the others' copy drives
  the glow parts (`AssembledObject.SetExhaust`), the exhaust particles (`ShipExhaust.AttachRemote`), a 3D engine loop
  (`HangarFlight.AddEngine`, pitched up while boosting) and the cloak (`NpcCloak.Show`: the hull dissolves, lights and
  glow hide from 25 %; `Target.cloaked` and `untargetable` from the moment it engages: off the radar, the markers and
  the lock, a held lock dropped (`CombatRadar`), the missiles on it shaken off). Their jumps too (`SystemJump.ChargeStarted` /
  `JumpFxStarted` -> `NetPlayer.ChargeRpc` / `JumpFxRpc`): the Khador Drive's charge (sound 33) and its khador_jump fx with
  sound 32, a jumpgate's `_jump_anim_add` (back to idle afterwards) with sound 31; the ship goes when theirs vanishes
  (`NetPlayer.visible`, the owner's model shown), with an explosion when it was destroyed (the death's model hiding).
- **EMP**: the player's EMP on another game's ship is relayed (`Target.RemoteEmp`): on an NPC proxy its owner's ship
  takes it (`NpcShip.OnRemoteEmp`: past a third of its EMP points it turns on that player, like `OnPlayerEmp`), an
  EMP-disabled proxy shows its lightning to everyone (`EmpSparks`); on another player (remake pick: players have no EMP
  pool) it drains that much shield and shows the lightning on their ship for 1.5 s. Squadmates are exempt.
- **Standings per player**: each player shares their standing axes and signature (`Standing.*With`); an orbit
  authority's NPCs are hostile to another player whose own standing makes the race an enemy, and each player's markers
  use their own standing. Another player's hits turn a ship on their squad by the single-player friendly-fire rule (half
  its hull, a quarter on Extreme; `NpcShip.OnRemoteHit`), and whoever destroys another game's NPC gets the kill in their
  own session (`KillCreditRpc`: `Standing.ApplyKill`, the kill counted when it was after them, pirate kills).
- **Bar missions** (the freelance missions, `NetMissions`): a squad has **one mission**, the same for every member
  (one squad id, `FreelanceMission.netId`), with shared goals. It can only be accepted with the whole squad docked at the
  agent's station (`NetMissions.AcceptRefusal`, in `Freelance.AcceptRefusal`); every member gets it (their own mission
  replaced); the return trip of Recovery / Salvage updates it for all. Progress is shared: anyone's kills count
  (`Target.killedByRemote`, the Challenge score too) and the mission's `status` is synced (`NetMissions.AddStatus`: ore a
  member unloads at the plant, the Hijacker's container captured by any member). Containers and passengers stay with the
  member who carries them, so only they deliver Courier / Passenger / a Recovery return trip (`NetMissions.CanDeliver`);
  Purchase / Stolen goods anyone with the goods. The mission orbit is built once: by the first squad member there
  (`NetMissions.ShouldRun`, `NetPlayer.MissionRun`), whose game runs it, and shows its ships and junk to everyone there
  (NetOrbit proxies every player's own NPCs; junk as `NetProxy` junk kind in `Target.RadarObjects`); mission ships are
  never taken over (`NpcShip.MissionShip`); the orbit stops when its runner loses the mission. A squadmate arriving
  there gets `FreelanceOrbit`'s follower mode (`SetupFollower`, `Running` false): nothing built, but the briefing, the
  runner's waypoint route, Junk removal timer and Challenge score (`NetPlayer.MissionRoute` / `MissionClock` /
  `MissionScore`), the return trip's message and the result as the mission's dialog (`NetMissions.ResultView`); when
  the runner leaves (docks, jumps, respawns, leaves the squad), the member there with the lowest client id takes over
  (`Promote`): the runner's mission ships go on as its own with their role and hull (`NetProxy.RoleFlags`, `specHull`),
  so do the junk, the mining plant and a loose container; with none left they count as done (inherited), with no orbit
  ever seen it is built anew; the score and the clock go on; the runner's own copies go (`NetProxy.TakenOverRpc`). Two
  runners at once: the higher client id stands down (`Demote`). A follower's Ore Mining unloads at a hidden local
  stand-in of the runner's plant (`NpcShip.LocalOnly`), the ore counted for the squad. Loot from mission ships and junk (`Crate.missionLoot`, the Hijacker's container too) is
  out of reach for players outside the mission's team (`NetCrate`: claimed by another for them). They spawn out of the
  other players' view (`NetOrbit.OutOfSight`: a point in a player's 65 deg cone within 60 km, or within 1.5 km, mirrored
  to behind them). Only the mission's team gets anything from the mission: players outside it never get the mission
  (briefing, waypoint, timer, reward, notices), see its ships as ordinary ships (no mission name on the lock plate,
  `NetProxy.missionShip`) and only get the normal kill credit. A success anywhere (in space or docked) pays every squad
  member an equal share (`Freelance.Succeed` -> `SplitReward`: standing +5 and missions completed for each; solo = all
  of it) and ends the mission for all; so does a failure, and a member's Discard (the Missions window warns: it ends it
  for the whole squad, `NetMissions.Abandon`). Joining a squad abandons the joiner's own mission and sends them the
  squad's (NetState.AcceptInviteRpc: the inviter's, else a member's, `NetPlayer.MissionHeld`); leaving a squad removes its
  mission for that player (`NetState.LeftSquadRpc` -> `NetMissions.OnLeftSquad`), and a member leaving with the
  containers / passengers aboard ends it for the squad. A member who disconnects doesn't end it: what they carried
  (`NetPlayer.MissionCargo`, a captured Recovery container too) goes to a squadmate holding the mission
  (`NetState.HandOverMission` from `NetPlayer.OnNetworkDespawn`: Netcode despawns the player object before the disconnect
  callback, `NetMissions.TakeCargo`). The host checks a new squad mission too (the squad all docked at the sender's
  station, no other member's in the last 3 s: else `MissionRefusedRpc`), ends each mission once (`endedMissions`: two
  members delivering at once), and a success pays a member who never got the mission their share as well; a lost
  Challenge's wager is shared like the reward. Mission dialogs act only on the mission they were about (a squadmate may
  have ended it meanwhile). A runner dying keeps running it (a result without its dialog). Informer: the spy is spawned
  by the team member when another player built the orbit's traffic (`SpaceLevel.SpawnInformerSpy`, not with a squadmate
  here, `NetMissions.TeamHere`), and its death (or a spoiling kill) reaches the squad (status 1 / 1000). An old
  invitation from a squadmate is dropped (accepting it would drop the squad's mission). The squadmates of the one who took
  a mission see which one was activated: the mission card (`EventScreen.ShowMissionCard`, `NetMissions.ShowCard`; a box in the
  upper middle: "New squad mission", its name, "Accepted by X", the client's face and name, the target, the reward; 9 s, a
  tap closes it, the message sound), besides the chat notice.
- **Event graph quests** (single player; `EventHost`, `LocalPilot`, `LocalEvents`, `Session.GraphQuests`; later the main and
  add-on stories are to become graphs too, so quests behave like the story): the event system runs without a session through
  `EventHost` (`IPilot`: NetPlayer in a session, the one `LocalPilot` (id 0, Keith T. Maxwell, `%player%`) without;
  orders applied at once with `NetAdmin.Apply`, the local traffic's ships as `EventHost.EventShips`, notices as a HUD
  message / station toast, a clock that stops while the game is paused) and `LocalEvents` (DontDestroyOnLoad, made after
  the first scene) ticks it. The Start node's **Kind** (`EventKind`: Event, Bar Mission, Quest; "mission kind" line; old
  graphs = Event, a bar mission when titled) and **Starts when** ("mission starts <expr>"; empty = at once, "never" = only
  `startquest`). Quests start by themselves in a game scene (Space / Station, not a session's game, not the ending; while
  not paused) once their condition holds (`EventRunner.CheckQuestStarts`, every second; not the built-in graphs, which are
  multiplayer examples) and are lasting runs: a `GraphQuestState` record (save v13 `graphQuests` / `graphQuestsDone`)
  written at the start and at each `checkpoint <name>` (top level only, `Step.depth`; the variables, objective, target, quiet
  orbits), restored after a load from its last checkpoint (`EventRunner.RestorePending` → `RestoreLocal` →
  `EventRun.Resume`: the top-level handlers and parallel / every blocks before it again, the music / waypoint / respawn /
  restrict commands before it again). New lines: `objective [text]`, `target <station | off>` (the gold story icon on the
  map and the HUD: `EventRunner.IsQuestTarget`, the Missions window's Show on map), `questorbit <station> [off]` (no normal
  traffic there: `SpaceLevel` passes it as a story orbit), `startquest <name>`; nodes Checkpoint, Set Objective, Quest
  Orbit, Start Quest. `complete` in a quest pays and marks it done (`Session.GraphQuestsDone`); `fail` is the story's
  failure (`EventHost.QuestFailed` → `StorySpace.FailQuest` / the station dialog → the auto-save). The Missions window's Story
  panel lists the quests (title + objective) after the campaign's objective (`EventRunner.Quests`). Expressions gained this
  game's state (after the variables): campaign, credits, rank, station, system, ship, kills; cargo(item), has(item),
  visited(station), quest(name). `Session.ResetNewGame` ends single player's runs (`EventRunner.ResetLocal`).
  Single-player bar missions: `EventMissions.RequestOffers` builds the offers locally (mods' / Events folder graphs for
  one pilot, not the built-ins), Okay starts a lasting run (`AcceptLocal`, record kind Mission with the offer); it is the
  player's one mission (`Freelance.AcceptRefusal` refuses a freelance mission meanwhile and vice versa), shows in the
  Missions window's Freelance panel (client, offer, objective, Show on map for its target, Discard =
  `EventRunner.AbandonLocalMission`), a success counts like a freelance one (`OnEnded` success flag: missions +1, standing
  +5). Verified in Play mode: a mod quest starting after a load (Keith's dialogue with a mod voice clip), the Missions window,
  a save at its checkpoint restored without repeating the dialogue, docking at the target completing it (+5000, done); a mod
  bar mission offered, accepted, saved, restored and discarded; a mod's replacement station theme and a new track by
  /music; hosting and /event waves after the refactor.
- **Event graph bar missions** (`EventMissions`): an event graph whose Start node has a Mission title (its Mission
  settings: offer text, client (a story character or "race name" like /dialog's speakers; empty: someone of the station's
  race), reward, offered at (stations; empty: every one), min / max pilots; compiled to `mission <key> <value>` lines) is
  offered in the multiplayer Space Lounges: the docking game asks the server (`NetState.RequestEventOffers`, from
  `StationLevel.BuildBar`) and each offer becomes a visitor on a free slot (`StationLevel.AddVisitor`; `LoungePanel` rebuilds its
  plates; `AgentOffer.EventMission` = 11 in `LoungeChat`: the offer, the reward, the pilots; Okay checks the whole squad docked
  here and the pilot count, confirms, `NetState.AcceptEventMission`). The server checks again and runs the event for that team
  (`EventRunner.StartMission`, the run's `team`: while it ticks `NetCommands.Scope` limits every selector to the team, @team names
  them; its counts, triggers, scoreboard and notices cover only them); every member holds it (`EventMissions.Active`: the
  Missions window's left panel shows it in a session), the others get the mission card. Mission Complete (`complete [reward] [|
  title]`: the reward (else the mission's) split evenly across the team in the reward box) and Mission Failed (`fail [title]`)
  end it; so do the team leaving its ships' orbits for 3 s (failed) or the session. One mission per player at a time.
  Verified in Play mode (hosted, solo): the offer at Var Hastra, the chat, accepting, the hideout's pirates, the reward.
  Stolen goods: the documents are only in the team's shop (not the shared stock); one member buying them takes them for
  the squad (the status). The host relays the results to the squad and anyone holding
  that mission (`NetState.MissionTeam`). In sessions the generator also rolls **15 Ore Mining** (never rolled by the
  original's generator, freelance_missions.md 2.3 / 4.1; offer text 801): one of the offering station's top 3 asteroid
  ores, 30-119 t; the orbit has int((int(0.2d)+1)hc) enemies of the client's enemy race on a route, the mining plant
  (`Traffic.MiningPlant`, docking type 1) and 2 client-race haulers looping 30 km that never fire; docking at the plant
  unloads the ore (`ObjectDocking`, 1 t per second, counted in the mission's `status`); won at the amount. Remake
  (players' feedback, 2026-10-17: 5-6 km in front of the station it felt out of place): all of it at a remote site 9-11 km
  out (`MiningSite`, plain C#: from the mission's seed, `MiningSite.Seed` = the orbit's shared seed with the squad's
  `netId`, else the mission's own values, so every game of the squad builds the same; first 40-60 km, too long a flight
  with a session's fast-forward at x1; the direction is drawn again until its field keeps 140 000 units from the orbit's
  field and the plant 50 000 from a jumpgate), with an asteroid field of its own
  beyond the plant (`OrbitBuilder.SpawnField`, `FreelanceOrbit.SiteAsteroids`: 5-7 big + 18-25 small rocks, all of the
  mission's ore, none within 15 000 units of the plant or 9000 of the way in) built in the runner's and the followers'
  games (`BuildSite`); the enemies asleep beside it (awake from afar they blocked the fast-forward all the way there); the
  player's route is the lockable "Mining site" waypoint 12 000 units short of the plant (`Route.name`, which the HUD, the
  autopilot menu and its "Target:" notice show instead of 548 / 573), and the briefing names it (Extra
  `miningSiteBriefing`). The site field's asteroids are indexed from `MiningSite.IndexBase` (1000 + 100 per slot, past the
  orbit's own field: `NetOrbit.AddField`), so NetState's destroyed lists and the drilling / landed indices keep them apart;
  players outside the team don't have the field and ignore those indices. A follower's stand-in plant and a takeover
  (`Promote`) use the site's own plant position.
- **Shared shop stock** (`NetStock`): every station's items and dealer ships are one list for all players, kept by the
  host: made with the single-player rules (`Shop.GenerateItems` / `GenerateShips`) the first time anyone docks there and
  made again every 15 minutes (`NetStock.ResetSeconds`; no 3-station re-roll or re-docking nibble in sessions); rare goods (commodities with a max price of 5000+: Buskat, Vossk Organs, Implants...; a report: Buskat back at Sao Perula after one system) don't come back with it: each roll sets their target amount (0 if the roll has none), what is left is kept, and every 10 minutes a short row gets a third of its target back, empty to full in 30 minutes (`NetStock.Make` / `UpdateRare`); a docking
  player's own list is replaced by it (items and ships in place, the bar's agents stay theirs); a trade (a unit bought /
  sold, several at once from Buy all / Sell all, a dealer row swapped, `Hangar`) goes to the host, which sends the list to everyone docked there (the open hangar
  window rebuilds, `HangarWindow.StockChanged`; during the blueprint view it waits, and an item new to the window gets its
  price on demand, `Hangar.PriceOf`). The host decides: a bought unit it no longer has is taken back and paid back
  (`NetStock.ItemRefused`), a dealer ship is reserved first (`NetStock.ReserveShip`); the docked list is empty until the
  host's arrives; lists are applied between frames (`NetStock.Flush`). The host's lists carry the stations' docking extras
  that aren't one player's (`NetStock.HostExtras`: Kappa's EMP GL I at free play, energy cells at 10 / 100 / 101). Not
  shared: the owned Kaamo Club's storage.
- **Debug menu in multiplayer** (`NetGame.HostAllowsDebug` → `NetState.DebugAllowed`, a NetworkVariable set when the
  world spawns, fixed for the session): off by default. The Host card's **Debug menu** Off / Allowed segments
  (`mp_allow_debug`), a dedicated server's `-allowdebug` (the launchers' `ALLOWDEBUG=1`; `status` and the startup log
  say which). `Cheats.Allowed` = no session, or the session allows it: then every player's pause / station menu has the
  Debug page, whether that device ever opened the main menu's Debug panel or not (`Cheats.PageShown`; the station's
  entry follows a change while docked); otherwise the pages are gone, the Options "Debug tools" row is hidden, and every cheat flag reads off (`Cheats.On`), so
  toggles left on in single player don't carry into a session (their saved values are kept). Client-side only: a
  modified client can still cheat.
- **Server checks** (`NetGuard`, `NetRateLimit`): every client request goes through the server, which checks it and limits
  its rate before it acts or passes it on. The server's messages to the players are `InvokePermission = RpcInvokePermission.Server`
  (Netcode's default lets any client send any RPC, and relays it): hits, EMP, shots, blasts, jump effects and kill credits
  go up to the server (`HitUpRpc`, `ShotUpRpc`...) and down from it. Checks: finite numbers in range (positions within 1e6 m,
  damage up to 1e6, 1e8 with the debug menu allowed), stations / items / ships that exist, where the sender is (in that
  orbit, the same orbit, docked there), never a squadmate's weapon, payload caps (mission JSON and hangar snapshots 4096,
  commands 32 / 512). Invitations are recorded on the server (an acceptance needs one, at most 60 s old); a mission is
  recorded with its team (only team members report results, a share capped by the reward); dealer ships need a
  reservation; a kill notice needs a hit from that killer within 30 s; proxies / crates spawn only for a player in that
  orbit (at most 300 / 150 each); a client despawns only its own proxies and crates. A token bucket per client and
  request kind drops a flood (logged once a minute); 1500 weighted drops in a minute (shots 0.1, trades 0.2) kick the
  client. The host's own player is exempt. Still trusted (no server-side world): each player's own ship, credits, cargo,
  standing and position, damage within the cap, and the orbit authority's NPCs.
- **Dedicated server** (`DedicatedServer`, `NetGame.StartServer`): the normal Windows / Linux player started with `-server`
  (with `-batchmode -nographics`; `-relay` online with a join code, listed as `-name "..."` unless `-unlisted`;
  `-password`; `-maxplayers` (2..100, default 16); `-allowdebug` (the Debug menu, see "Debug menu in multiplayer");
  `-port`, default 7777; `-fps`, default 60; the `GOF2_SERVER` environment
  variable does the same in the Editor's Play mode ("relay" = -relay), commands through `DedicatedServer.Run`). Every
  Windows / Linux build gets a launcher next to the game (`DedicatedServerLaunchers`: `Start Dedicated Server.bat` /
  `start-server.sh`, the name, password, player limit and `ALLOWDEBUG` at the top, online and listed; `-password` only when one is
set: Unity drops an empty "" argument, so `-password ""` read the next option as the password; `NetGame.CommandLineValue`
now takes an option right after another (a dash and a letter) as no value). `Bootstrap` hands over before
  the first scene wakes and swaps in an empty scene; the main menu scene never runs: in the Editor it is loaded already
  and switched off at once, in a player it is still loading then, so `MainMenu.OnEnable` / `MenuBackground.Awake` call
  `DedicatedServer.ShutOff` (the whole scene off before the rest wakes) and it is unloaded once loaded (before this, a
  player build ran the menu, its live orbit and its music under the server, and the menu reset the password); the
  process is muted (AudioListener volume 0, paused); none of the Bootstrap extras (options, Discord, haptics, bloom,
  the screenshot key); vsync off at the frame cap.
  `NetGame.StartServer` = StartHost's world without a player of its own (no NetPlayer, no EnterWorld); clients ids start
  at 1 (`NetState.Dedicated`: "Player N" counts from 1); the session-ending checks count the others, not the host
  (`OthersConnected`); `OnServerStopped` (a transport failure: the network or the Relay connection lost) no longer
  quits: `DedicatedServer.ConnectionLost` starts the session again (`Reconnect`: 5 s, then 10, 20, 40 and every 60 s;
  a new Relay allocation, join code and listing, the world seed kept, `StartServer(port, keepSeed)`; the players join
  again), also when an online server starts without a network; an expired anonymous sign-in is signed out (session
  token kept) and in again (`SignInForOnline`); the others' NetPlayers build no model there. The console (Windows: its
  own window through `WinConsole` unless stdout is redirected to a file or pipe (a terminal's inherited console handle
  doesn't count: a GUI program isn't attached to it) or `-noconsole`; with `-logFile` the window still opens (Unity's
  stdout is then that file); the log is mirrored into the window, Unity prints it only to a stdout it starts with;
  Linux: the terminal): joins / leaves with the client ids,
  each player's moves, chat; commands help, status, admin (alone: the admins), every chat server command
  without the "/" (players / list, g / say, w, kick, tp, admin / unadmin; see "Chat commands"; a kick's reason is the
  player's popup), stop (`NetGame.StopServer`: the goodbye, then quit; Ctrl+C
  and closing the window too). In its own console window (Windows) or on a terminal (Linux) the input line is edited by
  `ConsoleInput` (keys one at a time: `WinConsole.RawInput` / `ReadKey`, `Console.ReadKey`; a "> " prompt, log lines above
  it): Tab completes and cycles the command names (Shift+Tab back, nothing typed: all), Up / Down the history, Esc clears;
  piped input stays line by line. Verified on Windows; the Linux terminal path is untested. Linux quits hard (`DedicatedServer.ExitNow` on `Application.quitting`: the
  web admin and Netcode down, the log flushed, the terminal's settings from the start put back with `tcsetattr`, then libc
  `_exit`): the key reader blocked in `Console.ReadKey` kept Unity's teardown from ever ending after "CodeReloadManager
  destroyed"; the exit code is `DedicatedServer.Quit`'s (1 = failed). Windows the same (`ExitProcess` after the flush, 2026-10-12): the key reader in `ReadConsoleInputW` held IL2CPP's
  `Runtime::Shutdown` (AbortAllThreads -> Thread::Join), and a player's "Stopping the server..." was reported as a native crash. Verified: the Windows build headless with the Editor as the client (join, chat, say,
  kick, stop; over Relay, listed, with a password; the console window, no menu).
- **Persistent hosted world** (the Host card's World Fresh / Persistent segments, `mp_persistent`,
  `NetGame.HostWantsPersistent` -> `PersistentHost`): `StartHost` configures `NetProfiles` on
  `persistentDataPath/HostedWorld` (`SetUpHostedWorld`: the saved `server_settings.json` too, the card's password / player
  limit / Debug menu / name winning like a command line; `NetProfiles.Start` loads profiles, factions, bans, news), so
  `NetProfiles.Enabled` (`Dedicated || PersistentHost`) turns on everything a dedicated server with profiles has. The host
  signs in to its own profile (`NetProfileClient.Begin` instead of `EnterWorld`; its RPCs to itself run locally): always a
  profile, made the master admin on every login, never banned, uploads never doubted (`NetProfiles.IsHostClient`); on
  leaving its game is saved straight into the profile (`NetProfileClient.SaveHostNow` from `NetGame.Shutdown`). Fresh =
  unchanged (nothing kept, no profiles, so no factions or Admin tab). Online errors now list the inner exceptions
  (`NetGame.Describe`: Unity Services' "Some services couldn't be initialized" said nothing on its own). Not tested yet.
- **Message of the day** (`NetMotd`, `SERVER.md`): `motd.txt` beside the server's settings (`NetProfiles.Folder`, else `persistentDataPath`), read at the session's start and by `/motd reload`; `/motd set <text>` (`\n` new lines) / `clear` (admins, the console); every space kept, tabs 4 spaces, at most 60 lines / 4000 bytes (one RPC); `%player%`, `%players%`, `%server%` filled per player. A game entering the world asks (`NetGame.EnterWorld` -> `NetState.MotdRequestRpc`), the server answers that player (`MotdRpc`); shown in `EventScreen`'s MOTD window (the server's name, the text in JetBrains Mono (OFL, `Resources/GoF2Fonts`, a dynamic font asset made at runtime) so ASCII art lines up, no rich text, 14..22 by the longest line, scrolling both ways; OK / tap / Enter / Space / Esc / A / B / Menu close it; the other input rests, `EventScreen.QuestionOpen`) unless this server's same text was seen (PlayerPrefs `mp_motd_<server name>`, set on close); `/motd` always shows it.
- **Sector news** (`NetNews`): the station ticker's multiplayer items, before the game's own (`StationMenu.SetupTicker`;
  rebuilt when the strip wraps, `tickerNewsDirty`, and every minute for the ages). The server posts them and sends each to
  everyone (`NetState.NewsRpc`), a joining player gets the last 15 (`SendAll` from `NetPlayer.OnNetworkSpawn`); the last 40
  (3 days) kept in `news.json` with profiles. Kinds with a coloured "+++ KIND +++" kicker (BREAKING under 15 minutes old),
  the item at the docked station in bold, its age: territory (claims, give-ups, lapses), war (siege declared / begun,
  held), BREAKING a station taken, factions founded / disbanded, arena (a duel's score, a free-for-all's winner; no draws or
  forfeits), defence (`NetOrbit` counts the raiders the players downed, `Target.remoteKiller`; when none is left the orbit's
  authority sends `DefenseReportRpc`, 3+ kills, checked and once per orbit per 10 minutes; the Kaamo siege broken), new
  pilots, and an admin's `/news <text | clear>` (GalNet). Player text is made tag-free (`NetNews.Safe`). Not tested in a
  build yet.
- **Web admin** (`WebAdmin`, a dedicated server started with `-web [port]` / `-webport N`, `-webbind ADDRESS` default
  127.0.0.1; the launchers' `WEBPORT` / `WEBBIND`; the operator guide in `SERVER.md`): a minimal HTTP/1.1 server on a
  `TcpListener` (works in the IL2CPP player) serving one page (`Resources/GoF2Server/WebAdmin.html`: Tailwind CSS 4's
  browser build from jsDelivr, the script inline with a per-response CSP nonce, every server text set as textContent) and
  a JSON API (`/api/login`, `/logout`, `/state`, `/command`, `/log`) run on the main thread (`WebAdmin.Pump` from
  `DedicatedServer.Update`). Login like in the game: the admin token (`NetModeration.TokenMatches`, also without profiles:
  `EnsureToken`) = the console (`DedicatedServer.Run`), or a one-time code from `/web` (ops and up, 5 minutes, bound to the
  profile) = that profile's role, re-checked every request (`NetModeration.WebCommand`: moderation commands only). Session
  cookie HttpOnly / SameSite=Strict (12 h, 2 h idle, memory only), POSTs need the `X-GoF2-Admin` header, 5 wrong logins per
  address then 5 minutes' wait, every command logged. The state reuses `NetPanel.State` (`NetModeration.FillFor`,
  `NetFactions.FillPanel`); the log tab is a 1000-line ring of `logMessageReceivedThreaded`. Not tested in a build yet.
- **Player profiles** (`NetProfiles` server, `NetProfileClient` player; a dedicated server only, on unless `-noprofiles`;
  `-maxearn N` default 1 000 000, `-profiledir`): `<persistentDataPath>/ServerProfiles`
  holds `accounts.json` (the server's id, each account's devices with their token hashes, name, squad key, worth) and
  one `<account>.json` per profile, a `SaveData` without the shop memory (`SaveGame.ProfileJson`; loaded with
  `SaveGame.ApplyProfile`: the session's rules, no squad mission or Courier containers, docked where it was, an orbit
  without a station = Var Hastra). Files are written through `.tmp` with the previous one as `.bak`. Signing in: NetState
  carries `ProfilesOn` / the server id; a joining game doesn't enter the world at once but sends `LoginRpc` (its token
  for that server from PlayerPrefs `mp_token_<server>` (+ a -mpname hash), and a SHA-256 label of
  `SystemInfo.deviceUniqueIdentifier`), the server answers with a header (new token, role) and the gzipped profile in
  4000-byte chunks (Unity Transport's 6144-byte payload limit), then the game enters the world. No known token = a new
  profile; no limit on their number (the old `-maxprofiles`, default 50, turned new players into guests): a profile
  nobody signed in to for 30 days (`NetProfiles.PruneDays`, by lastSeen) is pruned at the start and hourly
  (`NetProfiles.Prune` from `Tick`; never online or staff), its file and its accounts.json entry moved to `Pruned/` (the
  newest 50 kept), leaving its faction; the console's `profile restore <id>` brings it back (`ConsoleRestore`). Console (and the web admin's `/api/command`, for control panels): `profile info | delete [force] | block [reason] |
  unblock <id|name>`, `profile blocked` (`ConsoleInfo`, `ConsoleDelete`: force signs the devices out and drops them first;
  block / unblock = `ban` / `unban`). Uploads (gzipped chunks, `UploadChunkRpc`): on every
  docking (`SaveGame.AutoSave`), every 60 s, when leaving (`NetGame.Shutdown`); each is checked like an imported save
  (`SaveGame.TryParse`), and without `-allowdebug` also turned away when the profile's worth (credits + ship prices +
  items at `minPrice`, `NetProfiles.Worth`) grew more than 2 000 000 + `-maxearn` per minute online since the last
  accepted one, or for hulls 13 / 14 / 15 (the player gets "The server didn't save your progress: ..."). Not a server
  authority: the client still runs the economy (the plan's phase 2: server-priced trades, claimed rewards). Kaamo upgrades
  past `Session.MaxModLevel` per mod and hull (builds of 2026-10-08 stacked them uncapped; a player had ten of each) are cut
  back at sign-in (`SendProfile` rewrites the file) and on every upload (`NetProfiles.ClampMods`). A refused upload is logged
  once per profile per 10 minutes with the count since, and the online staff (op and up) get a notice as often. Reconnect
  loops (a profile, or a device without one, signing in more than 8 times in 10 minutes) are turned away for 10 minutes
  (`ReconnectRefusal`, before the world: no join in the log or the chat). The join log line carries the profile and a
  short device label (`NetProfiles.Describe`). Devices: one
  connection per device (signing in again drops the older one); several devices of one profile = the first controls it,
  the others are observers (`NetPlayer.Observer`, server-written; `NetProfileClient.Refusal`: the station menu's
  Hangar / Lounge / Map / Launch refused; their uploads ignored); a controller leaving promotes the next device online
  (it gets the latest profile and reloads the station). Chat commands (answered privately, never relayed): `/link` (a
  6-letter code for 5 minutes, controller only), `/link CODE [force]` (this device joins that profile with its own token;
  its own profile is deleted when it was the only device, `force` needed past 5 minutes of play), `/control` (an observer
  takes over once the controller is docked: the controller is demoted and uploads once more (`RequestUploadRpc`), then
  the observer gets the profile; 5 s timeout = the last saved one), `/profile`. Squads: the account keeps a squad key
  (set on `AcceptInviteRpc`, cleared on `LeaveSquadRpc`, kept through a disconnect); a controller signing in joins a
  squadmate online (`NetState.RestoreSquad`, anywhere, not only in a hangar). Server console: `profiles`,
  `profile delete <id>` (not while online), `list` marks observers. Not tested in a build yet.
- **Faction stations on the planets** (`NavigationView`): in flight a neighbouring station's planet held by the player's faction (`NetFactionsClient.Owner`) gets a green orb before its name, red while its siege is declared or running; drawn as an element (no font glyph).
- **Distress calls** (`NetDistress`): a squad member in space calls for help (the squad window's own row: Distress call /
  End the call, or `/sos`; `NetPlayer.Distress`, owner-written; ends on docking, after 10 min or leaving the squad);
  the squadmates get a notice with where (`NetState.DistressRpc`), a red "⚠" name and a **Help** button in the squad
  window (or `/assist <name>`, both handled in the player's own game, `NetChat.Send`). Help programs the caller's
  station (`Session.ProgrammedStation`) and goes the fastest way: another system with a Khador Drive and the cells
  (`GalaxyMap.EnergyCells`): an instant jump (`Session.InstantJump`, SystemJump charges by itself); else the autopilot
  (`Navigation.ContinueToProgrammedStation`: the planet jump, or the gate route); docked: the launch first. Arriving in
  that orbit by travel, the helper comes out 1.5 km from the caller facing them (`NetDistress.ArrivalNear`,
  `SpaceLevel.SpawnPlayer`). The star map marks the squad when it opens (`StarMap.SquadMark`): a green "●N" by a
  system, "● names" under a planet, red with "⚠" for a call. In flight the Distress call button under the Multiplayer
  button (or the unbound "Distress call" binding; not in the quick menu, where a menu key picked it by accident), and
  the quick (actions) menu (E / D-pad left / the touch quick menu button; `Navigation.ActionEntries`, kind `Assist`):
  "Help <name>" for each squadmate calling, the answer as a HUD message; and a pulsing banner at the top
  (`TerritoryView`, under a siege's) with who calls for help and where. Not tested in a build yet.
- **Joining**: the menu stays up while connecting ("Connecting to ..."), it fades only once connected; `-mpjoin`
  clients open the Multiplayer panel and keep retrying quietly.
- **Gameplay rules** (`NetRules`, NetState's `rules` bit mask): the Gameplay options that change how the game plays are
  one rule for the whole session, since every orbit's NPCs, loot and missions run in one player's game: cloak shakes off
  pursuers, pirate outposts and bosses, capital ship enhancements, the informer missions' rule, stackable Kaamo Club
  upgrades, stored ships keep their equipment, the cloak bay, EMP breaks cloaks, smarter NPC targeting. `Settings`' getters return the session's value while a session runs (the
  host's `NetRules.Host`, a client NetState's); a player host takes its own options at hosting and changing one while
  hosting applies to everyone at once (`NetRules.HostChanged`); a dedicated server starts at the defaults, each flag
  (`-cloakhides`, `-capitalships`, `-informeroriginal`, `-nopirateevents`, `-nokaamostacking`, `-nokaamoequipment`, `-nocloakbay`, `-noempcloak`, `-originaltargeting`) turns
  one the other way, and the server settings (`cloakhides` ...) change them while running. The Kaamo Club upgrade limit
  is a number beside the bits (`NetRules.HostKaamoLevel`, `NetState.KaamoLevel`, `GameHooks.SessionKaamoLevel`;
  `-kaamomaxlevel N`, `/set kaamomaxlevel`, 2..20, default 5). So is the flight model (2026-10-16; players flying free
  flight beside the original in PvP had other turn rates, strafing and drift): `NetRules.HostFlight` (`SessionRules`:
  FlightStyles.Original "gof2" by default, Free "es2", or `PlayerFlight` -1 "player" = each player's own option; no Host
  card switch, the user found "free" confusing there): a player host's session starts at gof2, a dedicated server's
  `-flightmodel gof2 | es2 | player` (the launchers' `FLIGHT`), and admins' `/flightmodel [gof2 | es2 | player]` (no
  argument shows it; `NetCommands.FlightModel` -> `NetServerSettings.Set`, saved where the server keeps its settings) or
  `/set flightmodel`; `NetState.FlightModel` (sent as style + 2, 0 = not
  arrived), `GameHooks.SessionFlightStyle` -> `ModFlight.Decide` right after an event's style (an admin's /flightstyle still
  wins), before a mod ship's or campaign's; the Options row then says "Now set by the multiplayer session". The other players' rows are
  hidden (`OptionsCatalog.SessionRule`, `NetRules.ClientLocked`). Options that only change what one player sees or how
  they steer (target lock, salvage markers, cameras, dialogue, hints) stay each player's own.
- **Medals** are off in sessions (`Achievements.Check` / `Elite` award nothing, the Status window hides the medal column).
- **Off while a session runs**: the game pausing (the pause menu, dialogs and fast-forward keep `Time.timeScale` at 1;
  the flight controls stop instead while a menu, conversation or map is open, `Navigation.InputHalted`; the pause menu
  doesn't mute the sound), the Time Extender (the world can't slow for one player), saving and loading (the station's
  Save / Load entries are hidden, `SaveGame`).
- **Chat typing** ends when the field loses the focus (a click elsewhere, the keyboard closed; the draft stays); actions a
  component enables meanwhile go off too (`NetChat.KeepGameKeysOff`), a scene change drops the list (`DropTyping`); the
  windows' own key reads (dialogue, hacking, star map, item info, photo mode) go through `NetChat.Keys`; no mouse
  steering while typing.
- Testing: the Editor as host (set `Application.runInBackground = true` during Play, or Play mode stalls unfocused; an
  edited script recompiles and ends the session) and a Windows development build (the Windows build profile, into
  `Build/Windows`) as client: `GoF2Remake.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -mpjoin 127.0.0.1
  -mpname Pilot2` (`-mpjoin`: the menu skips its intro and joins, again every 2 s until a host answers; development
  builds also take `-mpdock` (docks once, a few seconds into the first flight after launching) and `-mpaccept` (accepts squad invitations
  while docked) and `-mptrade` (accepts trade requests while docked, offers 500 credits, accepts once the other side has) and `-mphost` (hosts from the menu), so the real hangar / squad flows run without a hand on the client
  (a phone: `adb shell "am start -n com.joppietoppie.gof2remake/com.unity3d.player.UnityPlayerGameActivity -e unity
  '-mphost -mpname Phone'"`). **Switch the active build profile before building another platform**: URP picks the shader
  variants to keep from the *active* build target's quality levels (`ShaderBuildPreprocessor`), so an Android APK built
  while Windows was active stripped URP's post-processing shaders (UberPost) and rendered lit geometry black;
  `BuildTargetGuard` (Editor) now fails such a build with that message. (The URP assets' prefiltering fields change
  with every build: harmless, URP rewrites them.) `-mpname`: this
  process's pilot name, not saved; explicit `-screen-*` options win over the window-mode option). The player has its own
  PlayerPrefs. Or the phone's development build joining the PC's LAN address. Multiplayer Play Mode (Editor clones)
  doesn't work in 6000.7.0b2: the clones fail to load URP's package shaders ("Host type is not matching any asset type"),
  so the package isn't installed.
- Play mode without a domain reload doubled Netcode's static message-type list on the second Play ("Allowed types is not
  equal to the number of message type indices"): `NetPlayModeReset` (Editor only) clears it at SubsystemRegistration.
- Netcode for Entities' automatic bootstrap is replaced (`NoEntitiesBootstrap`): only an empty default world, no client /
  server worlds, nothing in the player loop, so single player runs as without the package.

## Mods (remake-only)

Player-made content as data (no code: the game is IL2CPP), in `Scripts/Runtime/Modding` (`GoF2Remake.Modding`); the modders'
guide is `Modding/README.md` (keep it in step, and the AI version `Modding/ai/gof2-modding/SKILL.md` with it: an Agent Skill / paste-in guide for Claude and ChatGPT, with `reference.md` from **GoF2 > Build > Modding AI Reference** and the how-to `Modding/ai/README.md`), the examples `Modding/Examples/plasma_arsenal` (items) and `frontier_systems`
(systems / stations). Items, ships, systems and stations, quests and bar missions (event graphs), voice-over, music.

- **Unreadable mod files** (Android crash reports, 2026-10: "A local file header is corrupt" / "Corrupted data ReadInternal"
  on single GLBs of the GoF3 Ships zip): `ZipSource` reads one entry at a time under its gate, `Dispose` takes the gate too
  (a Mod Browser rescan, `ModManager.Scan`, released the archives while the background loaders still read them), checks
  the length and the CRC-32 (`Crc32`, slicing-by-8, ~2.4 GB/s on the desktop: the inflater doesn't, and a damaged entry
  inflated without an error into wrong bytes) and tries once more on the archive opened afresh; then a `ModFileException`
  ("X is damaged in Y.zip: download or import the mod again"). The loaders (`ModShips`, `ModStations`, `ModWeapons`,
  `ModCharacters`, `ModMaterials.Texture`, `ModJson.Read` as a ModJsonException, `ModInfo`, `EventRunner`) make it the mod's
  warning and never `Debug.LogException` a file error (`ModFileException.IsFileError`): the cloud diagnostics count every
  logged exception as an unhandled one; a load the mods changed under says nothing (a newer one runs).
- **A mod** = a folder or a `.zip` (files at its root or in one top folder, `ZipSource`) with `mod.json`
  (`ModManifest`: id (a-z 0-9 _ -, the lasting name), name / description (a string or per language), version, author,
  website, preview image (default preview.png), dependencies). Mod JSON is read with Newtonsoft's JToken (`ModJson`: comments
  allowed, errors with file and line), not JsonUtility. Folders (`ModManager.Folders`): `persistentDataPath/Mods` everywhere,
  `<exe dir>/Mods` on desktop players, `<project>/Mods` in the Editor (git-ignored).
- **On / off** (`ModManager`): PlayerPrefs `mods_enabled` (the ids in load order); new mods start off; `Active` = enabled,
  unbroken, dependencies on, dependencies first; `Revision` bumps on every change. A broken mod.json or content file makes the
  mod unusable (`ModInfo.Errors`), unknown fields are warnings. `ModInfo.Hash` = SHA-256 of every path + file (first 12 hex).
- **Items** (`ModContent`, items.json): `{ "id", "base": n | "mod:id", fields }` adds an item (a copy of the base: type,
  category and *look* kept, `ItemData.modded` / `modKey` / `Look`), `{ "override": n | "mod:id", fields }` changes one;
  fields: name, description, techLevel, occurrence, prices (`price` n or [min, max]), price systems, `vosskOnly` (attr 60),
  `alwaysSoldAt` (attr 61; a mod item's always stocks it there, also at the stations whose original filter allows one kind only, Var Destro 105 weapons, Quineros 107 equipment, Valkyrie 101, Sao Perula 106: `Shop.GenerateItemsOriginal` modAlways), `stats` by name (`ItemStats`: readable names + items.json's statList keys, kept in step with the
  attribute), raw `attributes`, `defaultEconomy` (the same, for the Default Economy). Texts inline or `text/<lang>.json`
  (`items.<id>.name`). `Database.Load` merges after the economy overlay (`ModContent.Apply`, only for the GoF2Data folder).
  A mod item's 0 occurrence means never (the add-on items' random roll in `Shop.GenerateItems` skips them).
- **Tech levels above 10** (a modder's report 2026-10-17, the community's "Tech Level 11 Add-On"): the original's items and
  stations both end at 10 (`Shop.TopTech`), and getItemBuyList stocks an item only where techI <= techS, so a tech level 11
  item was never sold. A mod item may now have 0..`Shop.MaxItemTech` (20; past it clamped with a warning, `ModContent`);
  `Shop.StationStocks` = techI <= techS or a station of tech level 10, which stocks every item above it by its occurrence
  alone (the 16 TL10 stations; Var Destro / Sao Perula / Quineros keep their kind filters); the original items never reach
  the new branch. A ships.json `dealer.minTechLevel` above 10 likewise = the TL10 stations (`ModUnlocks.AddDealerShips`).
  Loot: a tier-5 pirate outpost's two pieces of equipment are each 1 in `TrafficPlan.TopTechOdds` (3) one of
  `Shop.AboveTopTech` (mod weapons / equipment above 10, priced, occurrence > 0, no blueprint product, `available`, a mod
  campaign's filter; picked by occurrence, `Shop.PickByOccurrence`) while it isn't empty (`TrafficPlan.OutpostGear`; no extra
  random draws without such items); the enhanced capital ships' TL5+ gear pool takes them as before; `NpcTables.RollLoot`
  keeps the original's no gear from tech level 8. On the way, `available` is now honoured by the capital ships' gear and
  shop (`CapitalShips.RollLoot` / `ArmoryItem`), the lounge item sellers and the Purchase missions (`AgentGenerator`); a mod repair bot / gamma shield takes its base's repair rates / loop sound by its
  look (`PlayerHealth`, was by index: a repair bot based on 75 repaired at Mk II's rate). The
  Mod Builder's Items page takes 1-20 (`mbItemTechLevel`). Freelance difficulty stays capped at 10. Tests
  `ModTechLevelTests`. Verified in Play mode with a local test mod (`Mods/tech11_test`: a TL11 armour and a TL11 tractor).
- **Ships** (ships.json; the format of PR #34's custom_ships.json, `CustomShipData` in Database.cs): `{ "id", "model": glb,
  stats, slots, race, hangarHeight, mounts (slotType 0-4 (4: manoeuvring jets with a `direction`, `LateralThrusters`), `upsideDown` turrets, `builtIn` turrets: `PlayerTurret` puts an empty pivot / gun stand-in at the point instead of the item's `turretMounted` model (the item's gun, stats, aim, auto fire, turret view; shots from the point + 120 units, the view 60 units above it), `BuildStatic` (hangar, other players) shows nothing there; a plasma collector keeps only its stream, `sn_plasma_stream_anim_add` moved from a copy of the item's model onto the stand-in gun, so it pours from the point and turns with the aim; the ES2 kit's turret sockets), icon, modelLength / modelYaw,
  engineGlowRadius, materials, throttleGlow / extraGlows, lounge }` adds a ship (numbered after the original 64,
  `ModRegistry.Kind.Ship`, assembly "ship_NNN_mod", pack "mod"); `{ "override", armor, cargo, price, priceDefault, slots,
  handling, mounts }` changes one (`mounts` replaces its hardpoints, with or without a new model). Both take `npcTurrets`
  (remake, generic turrets: turret items, numbers or "mod:id", the NPC ships of that type carry on its turret mounts in space
  and parked in the hangar; `ModContent.ApplyNpcTurrets` -> `Database.NpcTurrets`; not a turret / more than the mounts: a
  warning). `ModContent.ApplyShips` merges them into Ships / Assemblies / WeaponMounts; `CustomShips` (the PR's
  API, now over the mods) serves race, hangar height and the lounge sellers; `GameNames.Ship` the texts (`ships.<id>.name`).
  A ship whose mod is off is a placeholder (never sold); `ModSaves.FixShips` puts a save flying one into a Phantom (refunded,
  its equipment in the hold) and drops stored / dealer / seller rows. An `override` of an original ship with `"model"`
  replaces its model (`ModContent.ModelOverrides`: built by `ModShips` like a new ship, assembly "ship_NNN_mod" added by
  `ApplyShips`, which `Database.ShipAssembly` returns before the original's; the original's mounts unless the entry has
  its own; not a CustomShips ship: no sellers, the stats stay). Verified in Play mode: the Groza (22) with its Galaxy on
  Fire 3: Manticore model (`Mods/groza`, converted from the .aem / .aei pack; git-ignored) in the hangar and in flight.
- **A modder's requests, 2026-10-16** (tests `ModEquipmentLookTests`, `ModBlueprintsTests`, `EventGraphCompilerTests`):
  a mod's booster sounds like its base (`PlayerEngine.BoostSound` by `ItemData.Look`: it was silent, the boost sound picked by
  item number) and a mod's tractor beam draws its base's beam (`CombatAssets.Tractor(look)`). **Multi-grab tractor beams**
  (`CombatRadar`, items.json stats `tractorTargets` 122 (1..8) / `tractorRange` 123 (units, default 30000)): N - 1 extra beams
  (or all N with no lock running) take the nearest crates within reach by themselves (`UpdateExtraBeams`, a `Pull` each with
  its own beam object; not steals; a mission crate only while the hold has room), no salvage lock, so the ship lock stays free
  (`Publish` counts only the lock's beam); they let go in the turret view, on death and in jumps; `Crate.pulled` keeps the
  manual lock off them and drives multiplayer's claims (`NetCrate.TrackPull`). Hackable wrecks for events and quests:
  `/spawn [players] wreck [race] [named <name>] [at x y z]` (`NetAdmin.Order.Wreck` 31, `DebugSpawner.SpawnWreck`,
  `TrafficPlan.HackableWreck`, `SpawnSpec.eventWreck`: hacked once, `Traffic.OnHackWon` sets its docking type 0), the quest
  step `wreck`, the graph's Spawn node with Ship "wreck" (the picker's "Hackable wreck"); hacking any wreck fires `on hacked`
  (above, Events). In a session the wreck exists only in the games it was sent to. Verified in Play mode (a test mod with a
  4-beam tractor based on 69 and a booster based on 73, at Dis): four blue beams at once on four crates, six crates in, the
  one past the range left; the booster's sound index 2 (73's); a quest's `wreck` spawned "Hulk", its `on hacked` paid and
  showed "Hacked Hulk" after the hack (`HackWon` invoked; the hacking game itself not played). Not tested in multiplayer.
- **Built-in equipment and a steady engine glow** (remake, a modder's requests, 2026-10-16): ships.json `integrated` (new
  ships and overrides; `ModContent.ApplyIntegrated`): `khadorDrive` true / false (`Database.IntegratedDrives`, defaults
  37 / 38 / 40, `Ship::hasJumpDriveIntegrated`; read by `GalaxyMap.HasIntegratedDrive` through `Database.Shared`) and
  `cloak` true (95's stats) / a cloak item (category 21) / false (`Database.IntegratedCloaks`, defaults 44 / 49 -> 95,
  `Ship::hasCloakIntegrated`; `PlayerCloak.HasCloak` / `Attach`, `Hangar.HasCloakBay`); an override can take an original's
  away; NPCs' cloaking is unchanged (Specters and ship 49). The glow: a material's `engineIdle` (with `engine`, 0..1, its
  share at throttle 0, `EngineMaterialGlow.Target`; 1 = lit whenever the engines run, dark parked) and a throttle glow's
  `off` (`ThrottleGlow.off`: its level parked in a hangar with the engine part hidden, default -1 = idle; idle = full with
  off 0 = steady): before, a mod ship's glow either followed the throttle or stayed lit on the pad. Tests in
  `ModContentTests`. Not yet looked at in Play mode.
- **Ship models** (`ModShips`, `ModShipBuilder`, `ModMaterials`, `ModGltfMaterials`; glTFast 6.20, `com.unity.cloud.gltfast`):
  each new ship's GLB loads with glTFast (asynchronous: `Preload` from the main menu, the menu's Leave and `NetGame.EnterWorld`
  wait for `Ready` / `WhenReady`, at most 30 s) into an AssembledObject template under a hidden DontDestroyOnLoad holder
  (the run-time port of PR #34's CustomShipBuilder: hull turned by modelYaw and scaled to modelLength (a skinned mesh baked into a plain one in its drawn pose first,
  `ModShipBuilder.FlattenSkinned`: the sizing, collision, shadows and the Mod Builder read the bind-pose mesh, which the
  bones drew elsewhere and at another size, so bone animations don't play; the GLB's node animations, parts moving on
  their own, loop: `ModGltf.LoopAnimations`, glTFast's legacy Animation at the default wrap mode played its clip once),
  engine glow discs at the
  slot-3 mounts on mat_34813 (a mount's `glowSize` [half width, half height, flame length] makes an ellipse; a `glowColor`
  or the ship's `engineGlowColor` switches the glow to `ModAssets.engineGlowTint`, the sprite in white
  (`Resources/GoF2Mods/ModEngineGlowWhite.png`, Build Mod Assets; the glow sprite there made round and soft, its ring of
  dashes averaged away) tinted by vertex colour, white-hot at the centre, and
  `ShipExhaust` tints the exhaust particles with it, on the particles.png cell whose product comes closest and brightest), throttle glows from their masks (`ThrottleGlow` with its trails), one LOD culled at 80000);
  `AssembledObject.LoadPrefab` hands it out for pack "mod" (while loading the game's own model of that ship stands in, else the Phantom's, always the original prefab: `Database.ShipAssembly(..., original: true)`; a project replacing the Phantom's model looped Template / LoadPrefab into a stack overflow on the builder's Exit), `NetProxy` for an
  "Assembled/mod/..." path. Materials are always copies of the URP Lit templates in `Resources/GoF2Mods/ModAssets`
  (**GoF2 > Build > Mod Assets**, `ModAssetsBuilder`: opaque / cutout / transparent, with and without detail maps; the
  templates hold placeholder textures and a faint emission so URP keeps their keywords and a build keeps the variants;
  a platform switch can drop their `_EMISSION` (seen switching to Android), so `ModAssetsGuard` (Editor) puts it back before
  every build: without it Linux / Android shipped without the emission variants, mod ships' glowing parts dark): the
  GLB's own glTF materials (`ModGltfMaterials`; metallic-roughness converted by `Hidden/GoF2/MetallicRoughnessToGloss`, the material's `metallicFactor` / `roughnessFactor` baked in, also on the CPU path, per factor pair (`ModGlbImages.Image.mrFactors`, `ModMaterials.RoleKey`): they were taken as 1, and the GoF3 Ships mod's maps are all metal in B with metallicFactor 0, so its hulls were mirrors, black against space) or
  the entry's `materials` (`ModMaterials.FromSpec`: the PR's fields plus `doubleSided`). Textures load from the mod's PNGs,
  compressed with mipmaps. Normal maps (ships / stations / rooms `normal` / `detailNormal`, glTF normal textures, `*_normal`
  texture replacements) are loaded with `normal: true`: Android's normal map encoding is "DXT5nm-style" (Player settings),
  so URP there reads X from alpha and Y from green (`UNITY_ASTC_NORMALMAP_ENCODING`); the game's Normal Map imports are
  re-encoded by Unity, a mod's RGB PNG wasn't, and every mod hull came out nearly black on Android (and in the Editor with
  the Android target). On Android `ModMaterials.NormalToAlpha` copies red into alpha before compressing (the glTF ones by
  `Hidden/GoF2/NormalToAlpha`, `Resources/GoF2Mods`); desktop reads either layout and is unchanged. **A GLB's embedded
  images** (`ModGltf.Load`, used by ships, stations, rooms and weapon fx) don't go through glTFast: `ModGlbImages.Strip` takes
  them out on a worker thread (each pointed at one shared 1 x 1 PNG, the indices kept, the other bufferViews packed into a
  new BIN; not for several / external buffers or meshopt) and `ModMaterials.PreloadImage` makes each different image once per
  mod by its MD5 (`ModInfo.LocalImage` writes it out for the decoder and it is deleted afterwards; `ModTextureCache.FileForContent`),
  compressed like the PNGs, the role's conversion on the CPU before compression (metallic-roughness to metallic / smoothness,
  Android's normal layout), and `ModGltfMaterials` / `FromGltf` take those (glTFast's own texture and the GPU conversions only
  for an image not taken out). glTFast kept every model's images uncompressed, one copy per model, plus an uncompressed
  render texture per metallic map (and per normal map on Android): the GoF3 Ships mod (79 GLBs, 1452 images, 251 different)
  crashed phones (2026-10; measured in the Editor: driver memory 4.9 GB -> 1.3 GB, its textures ~195 MB of DXT, 84 ships
  10 s from the cache). Textures load once per key however many ask at once (`ModMaterials.Once`), at most 6 decodes at once
  (2 on phones, `DecodeSlots`), phones compress in Texture2D.Compress's fast mode; at most 8 ships load at once (3 on phones,
  `ModShips.ShipSlots`), each holding its GLB until glTFast has read it. Shop icon: the entry's PNG (`ItemInfo.ShipIcon`), else the Phantom's; the dialogue tints mod
  ship names (no sprite). Code that sizes a ship by its renderers must skip trails (empty at the origin): the item window and
  `NetPlayer` use mesh renderers only. Another player's ship built before the session's mods were on (they join before
  NetState applies them) is built again on `ModShips.ModelsChanged`.
- **Engine effect** (`EngineFx`, ships.json `engineFx`, `CustomEngineFx`; the GoF3 Ships mod): Galaxy on Fire 3's engine at every
  exhaust mount in place of the glow discs (`BuildEngineGlow` without discs: the empty part marks the engines on / off) and the
  exhaust particles (`ShipExhaust` skips the ship): GoF3's NivelianEngine / TerranEngine / VosskEngine scenes
  (`assets/vfx/engines/<race>`: the flare `glow_mesh`, two camera-facing quads, a 9 x 9 unit square and a 27 x 2.7 streak at half
  brightness with the texture turned a quarter; the exhaust, four crossed cards 12.1 x 1.62 units with the texture's middle band
  u 0.39 -> 0.69 along and vertex brightness 0 / 0.77 / 0.2 / 0 at 0 / 1.73 / 6.91 / 12.09) and its VfxList trail (TrailNivelian =
  `particles/ship_engine_nivelian.psb`, Fork Particle Studio, unreadable: `beam_001.bmp` streaks drifting apart), the trail rebuilt
  as camera-facing ribbons of points that keep the ship's velocity plus their own backward speed (they bend when the ship turns,
  as in GoF3's ship viewer) and sway in smooth waves (each trail eases toward random sideways speeds, `SwayEase`; independent
  points made a zigzag), the streak's middle row across them (whole copies per segment read as beads) and a random brightness
  per point. EngineDrive levels (thrust 0.3..1 length, boost longer; a third of the glow at idle, unlike the ES2 flames); in the station capped; materials copies of
  `ModAssets.fxAdditive` (`ModMaterials.CopyOf`) at `_Glow` 0.75 / 0.5 / 1 / 0.45 (flare, cone, trail, helix). A mount's `fxScale` sizes it (GoF3's helper scale:
  the Retro 0.5 / 1.62) and its `rotation` turns it (`ModHardpoints.MountTurn`; five Vossk hulls' engines 8.5-10 deg inward).
  The helix (`spiral*`, only the Nivelian engine has one: `_spiral.ae4meshgroup`, two ribbon strands, radius 0.5 -> 0.1 over
  10.5 units, spun by its animation): camera-facing strands rebuilt each frame round the exhaust's axis, the texture (GoF3's
  `vfx_generic_electricity_000`, a guess: the spiral's material names its flow textures by GUID only) along them, scrolled, faded
  by (1 - s)^2 (in flight GoF3's crackle is gone well before the strands' end). Part colours (`flareColor` / `coneColor` / `trailColor` /
  `spiralColor`, over the exhaust's; the flare's until then was always the exhaust's): the builder reads them from GoF3's materials for the helix engine (Colorize_Color1 = the
  EngineAttachment colour, unless Colorize_Custom; times DiffuseColor when DiffuseColorMultiply: cone 0.08 / 0.51 / 1, helix and
  trails 0.22 / 0.56 / 1) with the flare 0.75, cone 0.4 and trails 0.35 dimmer under it; the Terran / Vossk engines keep the hull's
  nozzle colour (their hulls light the nozzles orange / yellow-green while their effect materials colour blue / green; nothing
  shows which GoF3 draws; corrected for the Terran engine below). The Terran engine (checked against a clip of the Argus rotating in
  GoF3's ship viewer): its materials' Colorize_Options 1 draw the textures in their own colour, so every part takes the decoded
  textures' brightness-weighted colour (amber: flare 1 / 0.65 / 0.28, cone and trail 1 / 0.54 / 0.27; `texture_colour`), not the
  EngineAttachment's blue or the hull's pale nozzle colour; no wavering streams but one thin straight streak (`RACE_TRAIL`: 1 per
  exhaust, 0.45 units wide, wander 0.6, x2 the cards' length, intensity 0.35) behind a cone 2.5 x brighter, the spike GoF3
  shows. A Terran engine on a hull whose nozzles glow yellower than that amber takes its parts halfway to the
  nozzles' colour (the builder's `nozzle_tinted`: GoF3 draws the flare additively over the lit nozzle; the Retro / Groza
  Gr'Gath's gold nozzles, checked against a GoF3 video: (1, 0.75, 0.35) measured, (1, 0.80, 0.35) the blend; also the
  Acheron and Cronus). Matched by eye against two clips of the Moornta (GoF3's ship viewer, and in flight from the side and
  behind; Play mode: behind, below, above, the side, a hard turn; the Terran, Vossk and Retro hulls; the hangar hides it).
- **Exhaust styles** (`ModExhausts`, a mod's `exhausts.json`; the guide's "Exhaust styles"): engine effects any ship can wear,
  picked in the hangar's Customize screen (`ShipCustomizePanel`'s Exhaust row and the style's two colour rows; with styles in
  the mods that are on every ship has the screen: `ModShipKits.CustomizationOf` gives a ship without other customizations a
  kit of its own with nothing else, `Kit.weaponColorRow` false). A style is a GoF3 `engineFx` (`EngineFx.Create`; its colours:
  flare + cone, trails + helix) or a flame `model` with flame `materials` (an ES2 thruster: loaded with the mod ships as the one
  part of the style's own kit, `ModShips.LoadAll` / `EnsureExhaust`; per exhaust an instance named `exhaust_nozzle_N` at the
  mount, `MountTurn` x the style's `yaw`, scaled by `NozzleRadius` against the style's `nozzle`, driven by an `EngineFlame` on
  the holder that blows each along its nozzle's -Z; colours: copies of the flame materials with `_EmissionColor` / `_BoostColor`
  at their own intensity; a flame style's boost cards are its own mod's ships' boost jets (`OwnShip`: the ship's key in the
  style's mod): on any other ship the holder's `EngineFlame.boostOverride` sets GoF2/ModEngineFlame's `_BoostOverride`, the boost
  cards stay hidden and the boost turns the main cards to the boost colour). The player's Exhaust size row (`exhaustsize=`,
  `ModExhausts.ParseSize`: 0.5..2 in 0.1 steps) multiplies each exhaust's fitted scale. Each exhaust takes its nozzle's outline
  (`ModExhausts.ShapesOf`, `NozzleShape`: the convex hull of the nozzle's geometry in the mount's plane, its area centroid as the
  centre, kept as `profile`, its radius at 32 angles; `FromPoints`): the mount's `outline` (game units, position_engine's axes:
  the GoF3 builder writes it from the nozzle's glowing surface, `engine_glows.engines`), else a mod ship's readable meshes with
  "nozzle" materials measured on the instance (`Measure`: vertices to the nearest mount; an ES2 hexagonal rear), else the mount's
  `glowSize` as an oval, else the game ship's measured glow (`nozzle_shapes.json`, x1.1: the glow sits inside the nozzle, median
  0.91 of the round size), else round (`NozzleRadius`). Sized by its mean radius; the shape itself: a flame model's meshes are
  copied per nozzle and bent to the outline (`Warp`: each vertex's distance from the axis x the profile's factor at its angle;
  freed with the holder's `EngineFlame.ownedMeshes`), an engineFx's cone cards reach the outline (a cone mesh per shaped
  nozzle), its helix runs along it and its trails spread along the long side (`EngineFx.profiles`, `Trail.start`), centred on
  the nozzle. Checked in Play mode: ship 7's wide nozzle blocks (both styles fill them), an ES2 Sentinel's hexagon (measured
  from its nozzle meshes) with a GoF3 drive, the Moornta's GoF3 outlines with an ES2 thruster. `NozzleRadius`: a mount's `glowSize`, a mod ship's `engineGlowRadius`, else 55 game units x the game
  ship's exhaust anchor scale (`turretAngles`). The build carries it (`exhaust=mod_id:id;exhaustcolor=;exhaustcolor2=;exhaustsize=`,
  `ModShipKits.Build`, kept out of `VariantAssembly`'s name like the weapon colour) and `ModExhausts.Apply` puts it on each model
  instance (the player's in `SpaceLevel` spawn / swap, the turntable and hangar guests in `StationLevel.SpawnShip`, other players'
  ships in `NetPlayer.BuildModel`): a holder `exhaust_style` under the engine glow part aligned with the ship's root (it goes with
  the engines), the model's own exhaust hidden (the glow part's renderers `forceRenderingOff`: `ShipController.UpdateEngineParts`
  only toggles `enabled`; a built-in `EngineFx` disabled; the model's `EngineFlame` `suppressed`, its own flames dark; no
  particles: `ShipExhaust` finds the holder's effect), the manoeuvring jets kept. The Customize screen runs the turntable's
  engines (`StationLevel.PreviewEngines` / `PreviewEngineDrive`, `Visuals.EnginePreview` read by `EngineDrive`, past the
  station's full-throttle cap: boosting while a boost colour row is selected, `ShipCustomizePanel.BoostRowSelected`). The GoF3
  Ships mod's builder writes GoF3's Nivelian (helix), Terran and Vossk drives (`exhaust_styles`, GoF3's engine colours as the
  palette); the ES2 importer three thrusters cut from one-nozzle rear parts (`exhaust_module`: the flame cards with their UV1,
  the main cards' nozzle ring at the origin, ES2's +X nose turned to +Z; ES2's exhaust palette). Verified in Play mode: the
  Nivelian helix and an ES2 thruster on the Phantom, a GoF3 Terran drive on an ES2 Sentinel (its flames dark), an ES2 ring
  thruster in custom colours on the GoF3 Moornta (its engine effect off, the boost cards in the second colour), the Customize
  screen's rows and the turntable preview. Not tested in multiplayer.
- **From PR #34, now general** (for any ship with the data): several turret mounts (`PlayerTurret.AttachAll`, one item per
  mount in equipment order, `upsideDown` / `turretUp` mounts; `FreeLookCamera` visits Turret 1, 2...; the HUD's auto-fire and turret-view
  hints for each kind; `Hangar` takes a second turret into a second slot), `ThrottleGlow`, lounge ship sellers
  (`AgentGenerator.AddCustomShipSellers`, `AgentOffer.SellShip`, `LoungeChat` / `LoungePanel` ConfirmShip: "Okay." buys, with
  the Kaamo Club owned 327 Sell / Keep; `Hangar.BuyShipFor` / `KeepAndBuyShipFor`), `Shop.RaceOfShip`, `StationTables.ShipY`.
  The turret view's crosshair follows the gun (`FlightHud.UpdateCrosshair`; collectors 0x1f5d `GoF2Hud/plasma_crosshair`,
  `.crosshair--plasma`); before, it stayed on the nose for every manual turret.
- **PR #34's ships as mods** (not in the repository: third-party models): `Mods/starwars_ships` (Jedi Starfighter,
  Millennium Falcon), `startrek_ships` (Enterprise-E, Enterprise-D), `halo_ships` (Space Banshee); GLBs exported with
  `ModModelExporter` (Project window, **GoF2 > Export Model As GLB (Mods)**; its own Editor assembly `GoF2.ModExport.Editor`,
  glTFast.Export isn't auto-referenced; the plain-materials mode for ships whose ships.json materials replace them all),
  the PR's textures (above 2048 px shrunk to 2048, the PR's import size), icons, previews, CREDITS.txt / mod.json credits
  (the Falcon's and the Banshee's sources aren't stated in the PR).
- **The look** (`ModContent.ItemLook`, `ItemData.Look`, `Gun.lookIndex`): the original item a mod item is based on supplies
  the icon (`ItemInfo.ItemIcon`), the weapon fx (`WeaponFx.Load`), the text sprite, the turret models, trails, loop-release
  rules and the index-keyed behaviour (beams 9-11 / 228, cluster pools, sentry looks). Code keyed on an item's number for a
  *behaviour* should use `Look`; story / rule checks keep the real index.
- **Names** (`GameNames`): every item / ship name and description goes through it (the original's text blocks 1274 / 1041 /
  913 / 977 + index sit back to back: item 233's name would be the first medal's); never `Localization.Get(1274 + i)`.
- **Numbers** (`ModRegistry`, `persistentDataPath/mod_registry.json`, outside the Mods folder): single player gives each "mod:id" key the next free
  number after the original table (233+) for good; a key whose mod is off gets a placeholder (`ModContent.IsMissingItem`:
  commodity 22, no price, never stocked) so the table stays 0..N-1 and saves keep their numbers. Sessions number afresh in the
  session's order (the same on every game) and never touch the registry.
- **Saves** (v12, `ModSaves`): `mods` (id, name, version) and `modKeys` (kind, key, number, price). `MissingMods` → the main
  menu's and the station's load dialogs warn; `Fix` (before `SaveGame.Apply`) moves keys to their current numbers (a save
  from another device; `ModRegistry.Adopt` takes the save's number when free) and removes a missing mod's items (equipment,
  cargo, Kaamo storage, the parked ship refunded at the saved price; shop rows, bar sellers, records, blueprints, a Purchase
  mission dropped). Imports accept a modded number the save names (`ModSaves.Names`).
- **Systems and stations** (`ModWorld`, systems.json / stations.json): new systems after the original 34 (a race template,
  map position, sky, gates both ways, `gateStation`), new stations after the 135 (at most 7 per system, `looksLike` = the
  original station whose model / collision it uses, `StationLook` in `OrbitBuilder`), overrides; registry kinds System /
  Station; placeholders for a mod that is off (`ModSaves.FixWorld` docks a save there at Var Hastra). The Void's invasions,
  the ticker and random lounge stations skip mod / placeholder systems.
- **Station models, planets, suns** (`ModStations`, `ModBackdrop`; stations.json `model` / `modelSize` (largest extent, game
  units, default 40000) / `modelYaw` / `modelCentre` / `materials`, `collision` box | sphere | none or `volumes`, `interior`,
  `planetTexture`; systems.json `sunTexture` / `sunColor`; new or override, `ModWorld.StationModel`): the GLB loads with the
  ships in the background (`ModStations.LoadAll`, its own `TimeBudgetPerFrameDeferAgent`, textures through
  `ModMaterials.PreloadTexture`, the planet / sun PNGs too; `ModLoading` waits for it) into an always-visible AssembledObject
  template "modstation_NNN" (pack "mod", registered in `db.Assemblies`; `AssembledObject.LoadPrefab` hands it out); scaled to
  modelSize, turned, its bounds' centre at the origin. `OrbitBuilder.StationAssembly` / `AddObstacles` use it once built
  (`ModStations.Built`; else the looksLike original and its collision.json volumes): volumes from the placed model's bounds
  (`ModStations.Volumes`, axis-aligned) or the listed ones. `SpaceLevel.StationDockRange` = max(16000, radius + 6000) and
  `UndockPoint` z = max(10000, radius + 3000) game units for a mod model (radius = the obstacle's cubeHalf − 5000; NetTeleport
  uses it too). `StationLevel`: `ModWorld.InteriorRace` picks the hangar and bar. Backdrop: texture names "mod:<id>|<path>"
  (`ModWorld.PlanetTexture` / `SunTexture` in `OrbitLayout.BuildStarSystem`) make copies of planet_000_big / sun_000 with the
  PNG (`ModBackdrop.Material`); `sunColor` 1 = LIGHT0 1.5. `ModMaterials` clears its cache itself when the mods change
  (`Check`: ships, stations and backdrops share it). Example: `Modding/Examples/frontier_systems` (Kepler Prime's GLB from
  primitives, its banded planet, a Nivelian interior, the sun). Verified in Play mode: the model in its orbit at 2.5 km, the
  planet behind it, the sun, a ship placed inside pushed out to the box face, Dock offered there, the Nivelian hangar docked.
  A sun PNG with a bright glow out to its edge blows up when the sun swells near the screen centre: the originals fade to black
  well inside the edge (the guide says so).
- **Weapon fx** (`ModWeapons`, items.json `"fx"` on a new item or an override; `ModFxPart`): projectile / muzzle / impact
  each the base item's, `false` (none), a `sprite` (PNG on a two-sided 1 x 1 quad, white vertex colours: the GoF2 Shader
  Graphs keep `_USEVERTEXCOLOR_ON` in every game material, so it stays on) or a `model` (glTF; with `texture` every
  renderer gets the fx material and white vertex colours, else its own glTF materials), or the base item's own part recoloured
  (only `color` / `glow`, `ModWeapons.Recolor`: the effect materials (GoF2/Additive / AlphaBlend; a missile's hull stays)
  copied with their texture made grey by `Resources/GoF2Mods/FxRecolor.shader` (each texel's brightest channel, alpha kept;
  one RenderTexture per atlas) and `_Color` = the colour, so the hue is replaced and the shape / brightness kept; a modder's
  colour-only projectile used to be refused whole, the original look left); colours `[r, g, b]` 0-1, 0-255 when a value is
  above 1, or "#rrggbb"; fields size (game units: the
  sprite's width / the model's largest extent; the N'saan's own bolt is ~1600 units, its impact ~6300), color, glow,
  additive (copies of `ModAssets.fxAdditive` = mat_27250 sprite_fire / `fxAlpha` = mat_20101 sprite_smoke, **GoF2 > Build >
  Mod Assets**), lifetime (muzzle 100 / impact 300 ms), grow, fade, spin, alongFlight + stretch (axial billboard about the
  flight direction, taken from its own movement: GunRig turns blaster roots to the camera), rotate; `shot` / `explosionSound`
  (files or lists, loaded decoded), `shotLoops`. Each item gets a WeaponFx of its own (`Instantiate` of the base item's with
  the parts / clips replaced); `WeaponFx.Load` returns `ModWeapons.Fx(item)` first (null while loading: the base look
  meanwhile; an override's item number resolves through `ModContent.TryResolveItem`, which now loads the table first: on
  the startup loading screen nothing had, and every override's fx was dropped), so every gun (player, NPCs, turrets, sentries, `NetShotMirror`) uses it. `ModFxPart` (on the sprite / model
  under the part's root, since GunRig sets the root's scale and rotation): camera facing, lifetime growth and fade through
  `_Color` (rgb additive, alpha otherwise); `PartAnimation.PlayOnce` restarts it (`ModFxPart.RestartAll`, also turning it
  to the camera at once) and `GunRig.MaxLength` counts its lifetime. A beam laser's projectile (`Gun.IsBeamLook`: 9-11 /
  228; a modder's report 2026-10-16: GunRig scales the beam's root (1, 1, length in game units), and a camera-facing sprite
  under that scale smeared across the screen, an `alongFlight` one took the ship's movement for its direction): a sprite
  is one game unit long from the root along +Z (`ModWeapons.FitBeamSprite`), `size` wide, turned about the root's +Z to
  face the camera (`ModFxPart.beam`; `grow` widens it only); a model sits on a "beam fit" node, `size` across and one
  unit long. Test `ModWeaponsTests.ABeamsSpriteRunsAlongTheBeamFacingTheCamera`; checked in an off-screen render. Loaded in the background with the rest
  (`ModLoading`, the menu's `Preload`). Verified in Play mode: the example's Plasma Lance (fired: violet bolts stretched
  along their flight, the flash at the gun, the ring impact, its own shot sound).
- **Missile turrets and salvos** (remake, for mods; a modder's missile turret, 2026-10-14, whose own source patch was
  folded in this way): stats `missiles` 105 (a plain gun's / turret's shots are homing missiles, `Gun.missiles`: `Homing`,
  `IsMissile` = the rocket burst on a hit (`GunRig.ShowImpact`) and the rocket smoke trail (`RocketTrail.For`), no shrink;
  a turret's home on its auto target, else the radar lock, `PlayerTurret.MissileLock`), `salvoShots` 106 (side by side, 120
  units apart, each shot its own `Fired` for the multiplayer mirrors), `salvoCount` 107 / `salvoGapMs` 108 (a magazine:
  `Gun.WaitMs` is the gap inside it, else the reload; the cheats and mirrors reset to `WaitMs`), `turretRange` 109 (the auto
  turret's search range); not for beams, bombs, mines, cluster missiles, sentries, collectors. fx `"turret"`: the turret's
  own GLB (`ModWeapons.BuildTurret`: a `pivot` node holding a `*_gun` node, 1 unit = 1 m; `WeaponFx.modTurret`,
  `turretMuzzleUnits` / `turretShotUnits` for its `muzzle` / `shotStart`), used in flight and by `PlayerTurret.BuildStatic`
  (the hangar, other players' ships); a part without sprite / model takes `item` (another item's part, `ModWeapons.Copy`)
  and `scale` (on a node under a new root: GunRig sets the root's scale); `shot` / `explosionSound` an item's number (its
  sounds and shot looping). Same day, turret modding made whole: a turret GLB without `pivot` / `*_gun` turns as one piece
  (`BuildTurret` wraps it in a `pivot` and a `model_gun`); `"turret"` takes `scale` (its root) and `offset` [x, y, z] (game
  units in the mount's frame, on the pivot, undone by the root's scale and turn, `ModWeapons.Reshape`), and without `model`
  they resize / move the base item's turret (a copy of its `*_ship_mounted` prefab, so the hangar shows it too); `muzzle` /
  `shotStart` apply to the base model as well. **Turret mounts face any way** (`WeaponMount.turretUp` [x, y, z], game frame
  like `position_engine`; `TurretUpLocal` / `TurretTurn` = `FromToRotation(up, it)`, straight down the old `upsideDown`
  half turn about the length, which `upsideDown` still means; `TurretHangs` below the ship's horizon): the sides, the nose,
  any slant, in flight and the hangar (`PlayerTurret.PlaceOnMount`); the turret view's camera takes the turret's yaw axis as
  its up and sits on its outer side, upside down (hanging) the other way up with the stick mirrored, as the under-hull view
  was. Mod Builder: the Items page lists the remake-only stats the item's kind takes even when its base has none of them
  (`ModBuilderViewItems.ExtraStats`: missiles / salvos by `Gun.FiresPlainShots`, a turret's automatic / turn speed / range,
  the mining beam's, the phase cloak) and a turret's **Turret model** section (pick a .glb into `models/`, Size %, Offset X Y
  Z, and with its own model Muzzle / Shots start; `ProjectItem.TurretModel` / `TurretScale` / `TurretOffset` /
  `TurretMuzzle` / `TurretShotStart`, an empty `"turret"` dropped; each change reloads, so the turntable shows it); a turret
  hardpoint's **Turret faces** presets (Up / Down / Left / Right / Front / Back), **Match the hull here**
  (`ModBuilderGizmo.HullNormalAt`: short rays from just outside the point), and placing or dragging it over the hull faces it
  out of the surface (`WeaponMount.SetTurretUp(dir, snap)`: within 15 deg of a ship axis it takes the axis); Mirror flips its
  x; `ProjectShip.ToJson` writes `turretUp` only for a slant (`upsideDown` straight down, nothing straight up); turret mount
  edits reload the project. Verified in Play mode (the builder on a Veteran override): an exported turret_002 GLB and a
  plain ship GLB at 15 % on the turntable after the reload, the new sections, a mount faced Left sitting on the hull's left
  side (its up the pilot's left). Not tested: the turret view on a side mount in flight, a hand-picked file (the picker).
- **The game's own models** (remake, 2026-10-14, players' request; `Modding.GameModels`): wherever a mod takes a 3D model it
  may name a game assembly instead of a file, `"gof2:<assembly name>"` (reference.md's "Models and their textures"); also a
  game shop icon, `"icon": "gof2:item_046"` / `"gof2:ship_028"` (`GameModels.Icon`, Resources/GoF2Icons). Ships (new or
  override): no mod assembly or `ModShips` build (it only loads the icon), `Database.ShipModels[ship]` = the game assembly,
  which `ShipAssembly` hands out first (not with `original: true`, the stand-in); the model ship's hardpoints from
  `Database.GameMounts` (weapons_hd.json as shipped, cached; `ModContent.CopyModelMounts`) unless the entry has `mounts`;
  a new one's `CustomShipData.gameModelShip` (`GameModels.ShipOf`: "ship_NNN_*", v_ / sn_ too) gives its hangar height
  (`hangarHeight` -1 = that ship's, `StationTables.ModelShip` for `ShipY` and the pad lifts) and icon (`ItemInfo.ShipIcon`);
  `modelLength` / turns / offset / materials don't apply. Stations (`ModStations.LoadOne`): a copy of the prefab into
  `Build`, at its own size unless `modelSize` (`StationModel.size` 0). Turrets (`ModWeapons`): `"turret": "gof2:<name>"`
  (any model; `TurretRig` makes the missing `pivot` / `*_gun`, as for a plain GLB) or `"turret": { "item": n }` (that
  item's `*_ship_mounted` model and its `TurretCore.MuzzleUnits` / `ShotOffsetUnits`); `scale` / `offset` on both. Fx parts:
  `"model": "gof2:<name>"` (`GameModel`; with `texture` the meshes get copies for the white vertex colours, the game's
  own mesh assets untouched); also `"gof2:bra"` / `"gof2:beer"`, the ending's single-mesh props without an assembly, from
  `StoryAssets.menuStatics`, `GameModels.Prop`: the community's exploding bra, `Modding/Examples/exploding_bra`, a
  Fireworks-based bomb with the bra as its projectile). Mod Builder: **+ New ship on a game model** and the Ship tab's **Game model…**
  (`PickGameShip`, `NewShipOnGameModel` / `UseGameShipModel`, `ProjectShip.UseGameModel`: the model ship's stats, slots,
  race and hardpoints copied, the file-model fields dropped, no box-guessed first hardpoints (`NeedsLayout` false; the
  builder had put two guns, a secondary and an engine over the copied set), no auto hangar height or rendered icon; the
  model controls hidden; an override's Original model takes its own hardpoints back); the Items page's **Game item…** icon,
  **Game turret…** model (`ProjectItem.TurretItem`) and **Look and sound** (`BuildLook`: projectile / muzzle flash / impact
  from any game weapon with a size %, shot / blast sound; `ProjectItem.FxPartItem` / `FxPartScale` / `SoundItem`; parts a
  hand edit gave a sprite / model shown as "your own"). Tests `GameModelsTests`. Verified in Play mode: a new ship on the
  Veteran's model (its 12 hardpoints on the hull, slots 3 / 4 / 1 / 12, the hangar), an item with item 48's turret model,
  item 46's projectile and launch sound. Not tested: a `gof2:` station or fx part in flight.
- **Sound effects** (`ModSounds`, a mod's `sounds/<clip name>.ogg | .wav | .mp3`): replaces the game's clip of that name
  everywhere; `ModSounds.Get(clip)` wraps every `PlayOneShot` and `.clip =` site (57, not the music sources: ModMusic's),
  memoised per clip (AudioClip.name allocates), a no-op without mod sounds; later mods win; loaded decoded
  (`ModAudio.Load(..., compressed: false)`). Verified: `Target_Lock_v08` replaced.
- **Ship and weapon sounds** (`ModShipSounds`): ships.json `"sounds"` (a new ship or an override; `ShipDef.sounds`,
  `ModContent.ShipSounds`) = engine / boostStart / boostLoop / boostStop / charger, each a file, a list (random) or { file(s),
  volume, pitch } with a value or [from, to]; a kit hardpoint rule's `"sound"` { shot, loop, release } (`Rule.sound`,
  `ModHardpoints.SoundOf` by the item's type as the slot). A file missing under its name is taken in another audio format of
  the same name (`Resolve`: the ES2 importer always names .ogg and keeps a .wav where it couldn't encode, and lists `audio/` in
  `multiplayerIgnore`, so builds on different computers still match). Files anywhere in the mod (not `sounds/`, ModSounds' replacements by
  name), loaded decoded with the rest (`Preload`, counted in `ModLoading`'s sounds). `PlayerEngine.UpdateOwn`: the engine by
  speed (standstill -> top speed: `MoveSpeed`, free flight `FreeVelocity`), boostStart / Stop on the boost's edges (random pitch
  in the range), boostLoop by `BoostVisualPercent`, the charger while recharging by `BoostRechargePercent`; a missing one keeps
  the game's. The engine also for `PlayerEngine.EngineClip` (hangar flights, other players) at 0.6 and NPCs flying the ship
  (`NpcShip`, event 46's voice slot) at 0.8. Weapons: `WeaponSystem` (`Rig.modSound`: `PlayShot` per shot via ShotVoices, a
  loop while held with the shot once at its start, released over `release` ms), `PlayerTurret`, `NetShotMirror` (the shooter's
  ship, `NetPlayer`; a looping rule keeps the weapon's own there). The ES2 importer writes them from EVERSPACE 2's cues
  (`tools/audio.py`, vgmstream through Wine). Verified in Play mode (the Gunship): every sound loads and resolves per
  weapon type, the engine plays, a boost runs the loop to pitch 1.5 and the stop, the charger climbs 0.7 -> 1.9 as it refills
  and stops when full. Not yet: the shots fired by hand, multiplayer.
- **Textures / skins** (`ModTextures`, a mod's `textures/<game texture name>.png | .jpg`, e.g. `ship_028_terran_diffuse` for
  the Veteran's hull; `textures/<assembly>/<name>` for one model only, e.g. `textures/ship_028_terran/glow_terran.png`: the
  shared lights / engine glow on the Veteran alone, before the every-object file; the scope is `AssembledObject.ModScope`,
  its prefab's name taken in Awake, and such objects get their own material copies, keyed (scope, material); the builder's
  Skins tab offers "This ship..." / "All ships..." on a shared texture): replaces that texture on every assembled object using it (`AssembledObject.Awake` -> `Apply`: the
  object's renderers get cached copies of their materials with the mod's texture in `_BaseMap` / `_MainTex` / `_BumpMap` /
  `_MetallicGlossMap` / `_EmissionMap`; the game's material assets are never touched; when the mods change in a scene, the
  Mod Builder's skins, the objects there get the game's materials back before the copies go and the new textures once
  loaded, `ModTextures.Revert` / `ReapplyAll`: a cleared skin drew the hangar's other ships of that model magenta); `_normal` / `_metallic` names load
  linear; loaded with the rest (`ModMaterials.PreloadTexture`, `ModLoading`). Verified in Play mode: a BountyBot
  (/make_skin_texture) Veteran skin on the hangar turntable (`Mods/veteran_skin`, git-ignored). The backdrop's planets, suns
  and rings too (#66, `Backdrop.Load` -> `ModTextures.Replace(material)`: their Resources/GoF2Backdrop materials aren't on
  assembled objects; the copy's texture clamped). A mod's `backdrop.json` (`ModBackdrop.StarPlanets`, the last mod in the load
  order with one wins) `{ "planets": "star", "planetGlow": 2, "planetFlare": 0.35 }` draws the retextured planets as stars:
  facing the camera with its roll, not mirrored, no ring, their core at `_CoreGlow` planetGlow under the remake's bloom, and
  looked at, the sun's flare at planetFlare of its strength (a slight swell, an additive horizontal streak of their own
  texture, `Body.starStreak`).
- **Mod campaigns** (`ModCampaigns`, a mod's `campaign.json`: name, description, image, imageHover, showTitle, startStation,
  startShip, credits, equipment, cargo, standing, quest, galaxy mod | all, items mod | all, ships mod | all, trafficShips per
  race): a card after the three campaign cards, picked the same way (`MainMenu.RefreshModCampaigns` / `ModCard` /
  `PickModCampaign`: the art cropped to the card, the hover art fading in while selected, a name plate unless showTitle is
  false; four cards shrink to fit the row (`.card-row--4`), five or more go small and the row scrolls sideways
  (`.card-row--many`, `card-scroll`, `DragScroll` horizontal too)) → difficulty →
  economy → `BeginGame` → `ModCampaigns.Start`: Session.ResetNewGame's state, then `Session.ModCampaign` (save v14
  `modCampaign`), `FreePlay` (no GoF2 story, index 20), the start station / ship / credits / equipment / cargo / standing, the
  quest as a pending `GraphQuestState` (`EventRunner.RestorePending`: started once the station is up; the record takes the
  graph's title). `ModCampaigns.Current` while its mod is on. "galaxy": "mod" = the campaign mod and its dependencies'
  systems only (`ModWorld.SystemOwner`): `GalaxyMap.Visibility` forces the rest hidden on every call (the map draws no sun
  for them, so no route or jump reaches them), `AgentGenerator.GenerateStationIndex` picks `ModCampaigns.RandomStation`
  instead of R(135), the news ticker skips them; "items" / "ships": "mod" filter `Shop.GenerateItems` / `GenerateShips` (the
  originals renamed *Original) and the bar's item sellers; `NpcTables.RandomFighter` → `ModCampaigns.TrafficShip`. Verified
  in Play mode with a local test campaign (Mods/test_campaign: Kepler, the Jedi Starfighter, the plasma weapons): the entry,
  the start (station, ship, credits, equipment), only Kepler visible, mod-only dealers, Jedi / Banshee traffic, missions
  inside Kepler, the quest running, save / load keeping it.
- **New-game options** (`ModGameOptions`, a mod's `gameoptions.json`: id, name, description, image, imageHover, showTitle,
  default): campaign-style cards in the Game options panel (`MainMenu.RefreshModOptions`: the panel goes wide, the toggles in
  a left column, the cards in a right one that scrolls sideways with "N options · scroll for more" past two, Start game under
  both; `NavigateGameOptions`: left / right through the cards, right from the column to the last card visited, left out of
  the first card back to the toggle it came from, up / down in the column; the containers are made again after a UI reload),
  ON / OFF badge, the art dimmed while off; the choice is remembered (PlayerPrefs `newgame_modopt_<mod:id>`) and becomes the
  game's at `BeginGame` (`ApplyChoices`, `Session.ModGameOptions`, save v16 `modGameOptions`); a multiplayer session plays
  with each option's default. Verified in Play mode with 8 options from two mods (with and without images, a missing image
  warned), the arrow-key navigation and the scrolling, and 3 mod campaigns (6 cards).
- **Customizable ships** (`ModShipKits`, the guide's "Customizable ships"; EVERSPACE 2's ship modules): a mod's
  `shipkits.json` lists kits (id, slots in assembly order, `scale` = Unity metres per model unit for every build, `yaw`,
  `materials`, parts { id, slot, name, model, `types` (the ship types allowed, EVERSPACE 2's wing whitelist), `tiers`
  (extension meshes per build tier) }, all at one origin); a ships.json ship with `kit` (made "mod_id:kit_id" by
  `ModContent.ShipData`), `kitType` and `build` (`CustomShipData.kit` / `kitType` / `defaultBuild`, the build text
  "slot=part;...;tier=N") instead of `model`. `ModShips` loads each kit's part files once (`partTemplates`, the kit's materials
  applied, `ModShipBuilder.ApplyMaterials`), and only those a build uses: the kit ships' default builds and the player's saved
  builds at the start, any other when a build first needs it (`EnsureBuild` / `Template` start it; `KitPartsLoaded` and
  `ModelsChanged` follow, `StationLevel` / `SpaceLevel.OnModShipModels` rebuild the player's ship, the Customize screen keeps
  the last build with "Loading parts..." meanwhile; the ES2 kits: 39 of 205 files at the start), assembles the default build as the ship's "ship_NNN_mod" template and a player's
  build as "ship_NNN_mod#<build>" the first time `Template` asks (`variants`, the newest 48 kept; `Database.AssemblyByName`
  answers a variant name); `ModShipBuilder.Build`'s kitScale / kitYaw keep the kit's size. `Session.ShipBuilds` (by the ship's
  key, save v17 `shipBuilds` "key|build"), `ModShipKits.PlayerBuild` (normalized: every slot a part the type may fit, the tier
  clamped) -> `PlayerHull.Assembly` (the hangar turntable, flight). The hangar's **Customize ship** button (under Inspect ship,
  only for a kit ship, `StationMenu.BeginCustomize`) opens `UI.ShipCustomizePanel` over Inspect ship's orbit view (the orbit turned aside so the ship stands in the middle of the screen right of the panel, `StationLevel.InspectFrameX`; the footer's Back hidden, it lay under Apply; the rows scroll in a list sized to the screen, `FitToScreen`, so the stats, buttons and help stay on it): a row per
  slot and the tier, up / down / left / right (W A S D, D-pad, left stick) and the arrows' clicks, every change shown on the
  turntable (`ReplacePlayerShip`), Enter / A applies (autosave), Esc / B / Back cancels (the old build back). Verified in Play
  mode with a box-part test kit (Mods/kit_test): parsing, the default builds, a variant, the type whitelist and tier clamp, the
  panel changing the turntable, cancel, the build in flight. Colours: a kit's `palettes` / `colorSlots` (targets: a material
  name filter and base / emission / engine) / `presets`, the build's `colors` ("color.slot=value": a palette id or
  "#RRGGBB/metallic/smoothness", `ModShipKits.Resolve`); `ModShips.Assemble` recolours copies of the matching materials
  (`ApplyColors`) and passes an engine slot's colour as the build's engineGlowColor; kit `materials` entries are named by their
  `material` filter (`ModShipBuilder.ApplyMaterials`, `CustomShipMaterial.material`: matching the model's own material names)
  so colour targets find them; the panel's Colour scheme row (the matching preset, else Custom) and a row per slot with a
  swatch. Verified with the private EVERSPACE 2 kits built from the user's own export (git-ignored `Mods/es2_ships`,
  `tools/build.py` reads the exported glTF parts, textures and DT_ShipModules / DT_ShipColors / DT_ShipColorSets /
  DT_DebugShipsKS JSON: never committed or shared): Light / Medium / Heavy kits (37 / 39 / 29 parts), nine ships with ES2's named
  default builds, ES2's 4 palettes, 7 slots and 22 schemes, the hull split into Paint1-3 by its vertex-colour masks; its
  `tools/phone.py` makes a phone build (the same mod id; Blender decimation to a quarter, 2.9 M -> 0.73 M triangles, half-size
  maps; `Build/es2_ships_phone.zip`, 43 MB). Mounts per part: a part's `mounts` / `tierMounts` (`ModShipKits.MountsFor`: per slot type the
  build's parts', else the ship's own), `Database.MountsOf` -> `ModShipKits.BuildMounts` (the player's build for the player's
  ship, else the default build: guns, secondaries, turrets, exhaust particles, the mining beam), `ModShips.Assemble` passes them
  as the build's mounts (the engine glow). Weapon and turret models (`ModHardpoints`, a kit's `hardpoints`: `turret` + `weapons`
  rules by slot, items (also by the look) or categories; loaded with the start's parts as part templates, `ModShips.PartTemplate`):
  the turret is rebuilt in the ship's frame (`TurretModel`: "Turret" -> "pivot" -> "kit_gun" -> "muzzle", the meshes in holders at
  the kit's turn and scale, the item's turret weapon on the gun with its muzzle on the turret's) and replaces `PlayerTurret`'s
  stand-in on `builtIn` mounts (its muzzle for the shots, 40 units ahead, and a collector's stream; `BuildStatic` shows it in the
  hangar and on other players' ships); the primary / secondary models go on the mounts in `WeaponSystem`'s order
  (`AttachWeapons`, from `SpaceLevel` and `StationLevel.RefreshTurret`, rebuilt when the weapons change) and `WeaponSystem`
  moves each gun to its model's `muzzle` node (`MuzzleOffset`); a mount's `rotation` (`WeaponMount.rotation`, Euler degrees or
  a quaternion in the ship model's frame, `ModHardpoints.MountTurn`) turns its weapon model and muzzle (the GoF3 hulls'
  slanted hardpoints; the shots still fly along the nose), and a turret model without a `muzzle` node carries its weapon on
  the gun node and fires from the weapon's muzzle; the weapon and turret models take the build's colours
  (`ModHardpoints.Colour` -> `ModShips.ApplyColors` with a cache of copies per build's colours: ES2's missile pod lights its
  bay frame in Emissive 2 and is painted in the hull colours; they kept the template's white before); a primary / secondary mount's `nodes` (`WeaponMount.nodes`, ship model node
  names: the GoF3 hulls' pylons) show only while that mount carries a weapon (`ModHardpoints.ShowMountNodes`, from every
  `AttachWeapons`: flight, the hangar turntable, other players' ships and hangar guests). **Skins** (a kit's `skins`, `ModShipKits.Skin`; `Build.skin`, text
  "skin=id", "none" = the model's own look over a default build's skin, `Build.Reserved` keeps "skin" / "tier" / "color." out
  of the slot names): texture sets per material name (diffuse, emission, metallicSmoothness in URP's layout, normal, colours)
  put on copies of the build's materials (`ModShips.ApplySkin`, before the colours) and an optional `engineColor` for every
  exhaust; their textures load with the build's parts (`SkinTextures` / `SkinReady` in `PartsReady`, `LoadOnDemand` preloads
  them, a failed one falls back to `ModMaterials.Texture`; `ModMaterials.Has`); the Customize panel's Skin row
  ("Standard" + the type's skins). **Weapon colour** (`Build.weaponColor`, "weaponcolor=id|#RRGGBB", "none" = the weapons'
  own; the kit's palette "weapons" else `ModShipKits.WeaponPalette`; left out of the variant assembly's name, the model doesn't
  change): the Customize panel's Weapon colour row on every kit ship (so the Customize button shows for every kit ship);
  `Flight.WeaponTint` colours the energy weapons (categories 0 / 1 / 3 by the Look, and the blaster-bolt turrets 47 / 48 / 49 /
  224) through `GunRig.Tint` (projectiles, muzzle flash, impacts, and thermo trails when called before `EnableTrails`): cached
  copies of their materials with the main texture blitted by `Hidden/GoF2/WeaponTint` (`Resources/GoF2Weapons`: brightness x
  the colour, the hottest part kept white; a red laser can't just be multiplied blue) and a grey `_Color` at its own intensity,
  `PartAnimation.RefreshMaterials` re-reading the fades; the player's guns (`WeaponSystem.BuildRig`), turret (`PlayerTurret`)
  and other players' mirrored shots (`NetShotMirror`'s weaponColor, from `NetPlayer.ShownBuild`). Verified in Play mode: the
  classification (a Micro Gun kept its own), green and blue bolts, beams and impacts, the row, Apply into flight.
  **Customizations of ships without a kit** (the game's own, one-model mod ships): a ships.json entry's `skins` / `weaponColors`
  (`ModShipKits.CustomizationOf`: a kit with no parts per ship, merged from every mod that is on, `ReadShipCustomizations`;
  `Skin.mod` the mod whose files it uses); builds saved under `BuildKey` ("gof2:<n>" for the game's ships), `TypeOf` "",
  `DefaultBuildOf` none; a skin shows on a skinned copy of the model (`VariantAssembly` -> "<model>#ship=N;skin=id",
  `ModShips.SkinnedTemplate` from `AssembledObject.LoadPrefab` / `ModShips.Template`, `ParseSkinned`; `Database.AssemblyByName`
  keeps the model's own pack for "#" names); the Customize screen, weapon colour, the rebuild hooks (`SpaceLevel` /
  `StationLevel.OnModShipModels`) and other players' views (`NetPlayer` build text) take `CustomizationOf`. Verified in Play
  mode with throwaway skin mods: the Phantom's Customize screen (Skin + Weapon colour rows), its crimson skin on the
  turntable and in flight with a mod weapon colour, the game's Groza (22) skinned in the hangar, the mod turned off (plain). Used by the GoF3 Ships mod (version 2: one kit "gof3", a hull part per ship, its GoF3 paint schemes as
  skins, the GoF3 guns as hardpoint models by weapon, a GoF3-parts turret on every hull, GoF3's projectile colours as its
  "weapons" palette (Colorize_Color1 of the GoF3 weapon effects' materials); built by
  `~/Downloads/gof3assets/tools/build_gof3_mod.py`). The ES2 converter adds ES2's player turret
  (`SK_Turret_PlayerShip_001`, skinned, split by bone into rigid base / pivot / gun meshes) and its weapon models
  (`SM_PrimaryWeapon_*`, the missile pod) with their muzzle sockets, mapped onto GoF2's weapons by item / category. Verified in
  Play mode on the Gunship: the hangar turntable's guns, pod and turret, the guns firing from the models' muzzles, the turret
  aiming, tilting and firing from its barrel, a plasma collector's stream from the kit turret. Other players' ships too (`NetPlayer.weapons`,
  an owner-written "p,p|s,s" of the mounted primaries / secondaries, `PackWeapons` / `WeaponStacks`; `NetPlayer.BuildTurret` and
  `NetHangar.ShowTurret` build them with the turret). **Other players' builds** (`NetPlayer.build`, FixedString512, the owner's
  `PlayerBuild` text, "" for another ship or past 500 bytes; `ShownBuild` = `ModShipKits.RemoteBuild`, normalized against this
  game's kit): their ship is the build's variant (`VariantAssembly`; parts not loaded yet load through `ModShips.Template` /
  `EnsureBuild` and `ModelsChanged` builds it again), its turret, weapons and exhaust particles on that build's mounts
  (`ModShipKits.MountsShown(db, ship, slot, build)`; `Database.MountsOf` answers with the local player's build); parked in the
  hangar the same (`HangarTraffic.SpawningGuest` -> `NetHangar.GuestAssembly` in `StationLevel.SpawnShip`, the exhausts from
  `ModShipKits.BuildOfAssembly`; a guest is parked again when its build changes or its parts finish loading). Verified in a session (the Editor hosting
  with mods allowed, a Linux development build joining on 127.0.0.1): the host's custom Gunship (Gunship B wings, body 7, a
  yellow hull) parked in the client's hangar and flying in front of it in space in that build with its turret. Not yet: the
  other eight ES2 ships looked at; the hangar's NPC ships of a kit type show the local player's build (`PlayerHull.Assembly`).
  **Hangar height of kit ships** (`ModHardpoints.HangarHeight`, from `StationTables.ShipY` through `KitShipY`; a guest's own build
  through `NetHangar.GuestShipY` in `StationLevel.ParkedPosition`, `HangarTraffic.SpawningGuest` now also around the pad
  placement): the shown model's lowest point (its mesh boxes without the glow parts) and the weapon models under it, an
  upside-down turret too, `HoverGap` 1.5 m above the pad; kept per model and weapons once the build's parts are loaded; ships.json
  `hangarHeight` is ignored for kit ships (the converter's value ignored the wing tiers: four default builds sat 2-7.5 m in the
  floor, other wings up to 15 m). **Engine materials** (`CustomShipMaterial.engine`, `EngineMaterialGlow` on the template from
  `ModShips.Assemble` / `ModShipBuilder.Build`): their emission follows the engine glow part on ships a station spawns
  (`StationLevel.SpawnShip` sets `followEngine`: dark parked, lit in a hangar flight, 0.25 s fade, this ship's material copies);
  always lit in space. Verified in Play mode: the nine ships' heights, the Sentinel's default build 2.1 m clear of the Midorian
  turntable (7.5 m into the floor before). **Engine flames** (`CustomShipMaterial.flame` / `flameLength` / `flameIdle` /
  `boostColor` / `flameNoise`; `GoF2/ModEngineFlame` from `ModAssets.engineFlame` (the Mod Assets builder resets the
  template to the shader's defaults), `ModMaterials.Flame`; `EngineFlame` on the template, `EngineDrive` the throttle reader
  it shares with `ThrottleGlow` and `EngineMaterialGlow`): EVERSPACE 2 draws its flames from small `M_Vehicle_EngineExhaust`
  sections of the rear / wing parts, flat cards with UV u from the nozzle to the tip that its material stretches with the
  thrust (its graph can't be exported: rebuilt from its parameters and matched against EVERSPACE 2 running, the player's
  Sentinel (Body_014, Rear_009, Nemesis tier 4) behind, beside and chased at full thrust, boosting, strafing in all four
  directions and reversing, through its photo mode). The cards' vertex colours say what they are: red the main flame (the
  thrust, the engine colour, also through a boost), green the boost jets (only while boosting, in the boost colour); the
  converter (`flame_across`) writes each card's coordinate across it into UV1.x and its kind into UV1.y, read as
  |y - 0.5| (glTFast flips V): 0 main, 0.25 boost, 0.5 none. The vertex shader moves each vertex u x the length backward
  (`_FlameDir`, the ship root's -Z in the renderer's object space, per renderer and submesh through a MaterialPropertyBlock,
  the renderer's bounds grown to match): main cards by thrust 0..1 (none at idle: below a drive of 0.02 the flames shrink into the nozzles like free flight's engines off; the user's ES2 shots of a ship at rest), +30 % at a full boost, boost cards by the
  boost alone, both x the engines' level (the engine glow part active, 0.25 s fade: shrunk into the nozzles and dark on a
  parked ship; in the station capped at full throttle). The fragment: additive, the colour gold near the nozzle (boost
  cards a tight near-white head, `_BoostCore`, then saturated in the boost colour at `_BoostBrightness` 1.6: matched to the
  user's ES2 customization-screen shots of the Wraith boosting; they were half as bright with a faint head), ES2's T_Noise_018_M stretched into fine streaks, faded hard toward the tip (`_Fade` 4), each card
  softened toward its long edges (UV1.x) and, for main cards, faded as it turns edge-on (|N.V|^2, ES2's OpacityExponent:
  from behind only thin rays remain, as in ES2; the remake had widened the cards toward the camera, `_Widen`, now 0, which
  turned Rear_009's boost cards into fans above and below the ring). A ship with flames keeps an empty `engine_glow` part
  (no discs) and has no exhaust particles (`ShipExhaust.Setup`). The nozzle glow (`M_Vehicles_Floaters_Emissive`, split by
  vertex colour by `glow_split`: green NozzleCore, black NozzleOff, the rest NozzleGlow) takes the engine colour slot through
  the brighter of its mask's red and green channels (T_Vehicles_Floaters_Emissive_M: the rings and grilles, and the dotted
  band Rear_009's ring samples), `engine`: `EngineMaterialGlow` lights it with the thrust, dark at idle (it kept 18 % at idle
  before; ES2's nozzles at rest are dark), dark parked. Intensities: flames 4, nozzles 1.6 (brighter clips to pale yellow under
  the remake's tone mapping; ES2 adds a bloom halo the remake's bloom can't without washing it out). **Manoeuvring jets**
  (`LateralThrusters`, mounts of slotType 4 with `WeaponMount.direction`; the converter reads every part's
  `SKT_LateralThruster` sockets, `thruster_mounts`: the Unreal rotator's forward axis): ES2 fires `P_Vehicle_LateralThruster`
  at them, each jet blowing along its socket whenever the ship moves against it (strafe right fires the left body and
  under-wing jets, down the upward ones on the wing tops, reverse the forward-facing wing tips; turning fires none). Each jet
  is two crossed boost cards drawn by the flame shader (5 m at full strength) plus a glow disc facing along it, in the
  ship's flames' boost colour; under the engine glow part (hidden with the engines, left out of the hangar heights and
  shadows). Input: the player's free-flight keys (`FlightModel.ThrustInput`, set by `StepFree`), in the original flight the
  strafe and the brake, other ships their measured acceleration; none in the station, except a small glow on every jet
  (0.35) while the Customize screen runs the engines (`Visuals.EnginePreview`), as ES2's customization screen shows them. `LateralThrusters.TestInput` overrides
  it for Editor tests. Colours like EVERSPACE 2's (TrailColorEngine / Boost / Lateral): a kit colour slot's target "property": "boost" sets a
  flame material's `_BoostColor` (`ModShips.ApplyColors`); the ES2 kits' slots Main thrusters (the flames and nozzles), Booster
  (the boost cards) and Side thrusters (the "lateral thruster" material: the jets and, through `LateralThrusters.Setup`, their
  discs; without a slot on it they take the ship's flames' boost colour). Strength slots (`ColorSlot.steps` / `scales`, no palette; the Customize
  panel's percentage rows; `ModShipKits.StrengthOf`) scale another slot's emission: the ES2 kits' Cybernetic glow (Emissive 3)
  and its intensity. Materials marked `"boost"` glow only while boosting (`EngineMaterialGlow.boostMaterials`, EngineDrive
  above 1); `"hologram"` materials are copies of `GoF2/ModHologram` (`ModAssets.hologram`, `ModMaterials.Hologram`): additive
  rings whose pattern turns and which bob, per ring by UV1 (x spin, y 1 - bob share), rebuilt from EVERSPACE 2's
  M_ShipModules_Holograms parameters (the converter's `holo_rings` reads the rings' vertex colours: red spin, blue bob);
  `"shimmer"` materials (`GoF2/ModNozzleShimmer`, `ModAssets.nozzleShimmer`, `ModMaterials.Shimmer`) draw additively over the
  nozzle glow's faces (the converter splits them twice, "NozzleShimmer"): the glow pattern times T_Noise_001_M scrolling at
  M_Vehicles_Floaters_Emissive's ScrollSpeed 0.3, driven by `EngineMaterialGlow` like the glow (which keeps 60 % then). `"damageEmission"` (a mask PNG, the emission of a
  material that has none): battle damage embers, `DamageEmbers` lighting them ember-orange with the ship's `Target.HullFraction`
  (a third lost to full at 90 %, a slow flicker; EVERSPACE 2's DamageAmount / Ember* parameters); the ES2 converter builds
  the mask from T_Vehicles_Damage_C_R_M_H (the blurred height's burn blobs, cracks from its colour) for the hull and details.
  `"damageDetail"` / `"damageNormal"`: charring through URP Lit's detail layer (detail colour with the burn mask in its alpha as
  `_DetailMask`, the damage normals), `DamageEmbers` raising `_DetailAlbedoMapScale` / `_DetailNormalMapScale` from a fifth
  of the hull lost to full at 75 %; the mod detail templates use `_DETAIL_SCALED` (MULX2 ignores the scale). `Hitpoints.hull`
  is an int (an eval setting it needs a cast). The
  ES2 export's skeletal animations (AnimSequence JSON) carry no keyframes and none targets the ship modules. Kit parts' `anchors` / `anchor` (`ModShipKits.Part`): a part sits on a named point another part of the build offers
  (`ModShips.Assemble` offsets its models; the ES2 bodies offer their SKT_Cockpit socket to the canopy and interior). The ES2
  lights (converter, matched against EVERSPACE 2's customization screen with Emissive 1 / 2 / 3 apart): TechDetails lights
  by `T_ShipModules_TechDetails_00N_E`'s sector IDs into Lamp<N>_S4..S7 (S4 Emissive 1, S5-S7 Emissive 2);
  M_Vehicles_Floaters_Emissive by vertex colour and glow atlas region (`glow_split`: Emissive 1 / 2 rings, the exhaust
  grilles and nozzles in the engine colour, the boost vents "BoostVent", the hot discs); the cybernetic glow lights the floater
  strips (T_ShipModules_Floaters_001's strip graphic, "CyberStrip", `floater_split`); the rest of the floaters are alpha-cut
  detail cards that never glow. The Wraith (Okkar hull, `okkar_split`): its glow faces are all white, so `glow_split`'s rules took its small lights for engine
  glow; only the faces within 1.5 m of its engine socket keep the engine colour, the rest (blade and wing-tip lights, the
  nose's bars) are Emissive 1, the spine's ribs Emissive 2; its plates don't glow (MI_Vehicles_Okkar_TechDetail_PlayerShip's
  EmissiveColor is #100801; they were in Emissive 1). Checked against the user's ES2 photo-mode shots with Emissive 1 blue
  and 2 red. The glow atlas (T_Vehicles_Floaters_Emissive_M) row 0.40..0.50 by the user's shots of a Nemesis-B9-1 I and a Raptor
  D1-1 I+ (Emissive 1 / 2 in different colours): its V-shaped triangles (u 0.34..0.50) Emissive 1 (engine glow before: body
  B's forward keel lamps, the small lamps beside body D's nose frame), the rest from u 0.5 Emissive 2 as before (the nose
  frame, the round dots of body B's rear keel lamps); cyan glow faces Emissive 2 (the missile pod's bay frame); the fin
  edges' dotted bars (u < 0.5) keep the engine colour. Verified in Play mode: thrust (chase, behind, side), a forced boost (the cyan slots in ES2's places),
  the jets in all four strafe directions and in reverse, parked in the hangar (flames, nozzles and jets off). The ES2 converter reads the meshes' sockets (SKT_PWSlot / SWSlot / Turret / Engine,
  Unreal cm -> the export's glTF metres (X, Z, Y); at most 4 exhausts per part). Stats and slots per part (`Part.armor` / `cargo` /
  `handling` %, `equipment`, flight %, `primarySlots` / `secondarySlots` / `turretSlots`; `ModShipKits.EffectOf` /
  `BuildEffect`, the player's build for the player's ship): `ModShipKits.Armor` / `Cargo` / `Handling` / `Slots` /
  `Effective` feed `Shop.BaseHp` / `MaxLoad`, `PlayerHealth`, `StationLevel` (the arrival's max hull), `Hangar.SlotCount` /
  `FitToSlots`, `Database.BuildFlightStats` (handling, the flight bonuses in both styles) and the stat rows (`ItemInfo`,
  `ItemInfoWindow`); the Customize panel's stats line (`StationMenu.CustomStatsText`) and Apply's `FitToSlots`; the tier is
  cosmetic. The ES2 kits' numbers are the converter's design (ES2's modules carry none): bodies hull / cargo / equipment by
  size, rears speed / acceleration by nozzles, wings turning / armour by span and gun / missile slots +-1 around the type's
  base by their sockets, turret slots by their turret sockets. Not yet: other players' builds in multiplayer (they see the
  default build), decals.
- **Flight styles** (`ModFlight`; the guide's "Flight styles"): mods choose per content whether they touch the original
  flight, free flight or both. Items: attributes 110-121 (`ItemStats`): `flightScope` (110: 0 both, 1 original, 2 free) keeps
  the item's booster / Steering Nozzle attributes 25-28 to one style; % bonuses `topSpeed` / `turnRate` / `strafeSpeed` (both,
  111-113), `original*` (114-116), `free*` + `freeAcceleration` (117-120) and `freeBoostFactor` (121, x100, instead of the
  booster's).
  Ships (new or override): `flightStyle` and `flight` { topSpeed, turnRate, strafeSpeed, acceleration, boostFactor, original {},
  free {} } (`ShipData.flightStyle` / `flightBoth` / `flightOriginal` / `flightFree`, non-serialized, set by
  `ModFlight.ApplyShip`). `Database.BuildFlightStats` fills `FlightStats.original` / `free` (`FlightTuning`: the style's
  booster, agility and summed `FlightBonus`; the ship's first, so an item's freeBoostFactor wins); `FlightModel.Configure`
  makes a `StyleState` per style and `FreeFlight` picks one (`Handling`, the booster, `TopSpeed`, the turn / strafe / ease
  factors; totals never below -90 %); the original scales its speed and boost by the top speed bonus, its target rate, ramp and
  decay by the turn bonus, its strafe by the strafe bonus (it has no acceleration). The style: `FlightStyles.Current` = the
  first of `EventFlight` (`/flightstyle [players] <free | original | player>`, NetAdmin.Order.Flight 30, the graphs' Set Flight
  Model node, undone at the event's end, re-applied when a quest resumes), the ship's, the mod campaign's, an on new-game
  option's (`"flightStyle"` in campaign.json / gameoptions.json), else `Settings.FlightStyle`; `ShipController.FreeFlightActive`,
  the HUD hints and the key layout (`GameControls.ApplyStyle`, polled by the flight and the bindings page) follow it, and the
  Options page names who sets it (`OptionsCatalog.FlightStyleForcedNote`). Expression `freeflight`. Verified on `FlightModel`
  in the Editor (the bonuses per style, a free-only booster, freeBoostFactor, a ship's JSON, the validation, a ship-forced
  style); not yet played with a real mod.
- **Conditions** (`Events.EventRunner.Condition(expression, out error)`: the event graphs' expression language outside an
  event, a throwaway run like the quests' "Starts when"; new words `option(mod:id)`, `won(main | valkyrie | supernova)`
  (or the multiplayer finished world), `systemsvisited` (distinct systems of `Session.VisitedStations`)): items.json /
  ships.json "available" (new entries and overrides; `ModUnlocks.ItemAvailable` in `Shop.GenerateItems` and
  `NpcTables.RollLoot`, `ShipAvailable` in `Shop.GenerateShips` and the lounge ship sellers), blueprints' "available"; an
  unreadable one is false with a warning on its mod. ships.json "dealer" { chance %, systemRace, minTechLevel }
  (`CustomShipData.dealer`, `ModUnlocks.AddDealerShips` at the end of `Shop.GenerateShipsOriginal`'s ordinary path: not the
  special yards).
- **Mod ships in traffic** (`ModTraffic`, Options > Gameplay "Mod ships in traffic", `Settings.ModShipTraffic`: Off / 10 /
  25 % (default) / 50 %, each player's own, not a session rule): `NpcTables.RandomFighter` / `StrongFighter` (every NPC
  fighter roll: locals, raiders, pirates, jumpers, escorts, guards, battle wings) take one of the mods' ships of the race
  (`ModTraffic.Pick`) at that chance; candidates: the mods that are on, `race` (or ships.json `traffic.races`; minor races
  count as pirates, no Specters), `ModUnlocks.ShipAvailable`, the model built (`ModShips.Built`) and at most 150 m long (its
  meshes along the nose, cached per `ModManager.Revision`), weighted by `traffic.weight` (`CustomTraffic`, 0 = never); a
  strong fighter one of the dearer half; a mod campaign's `trafficShips` for the race skip it (`ModCampaigns.HasTrafficShips`).
  The hull and gun come from `NpcTables` as before (only the look changes); freighters / capital ships / story ships keep
  theirs. Verified in Play mode (gof3_ships, 50 %): 37 of its 38 hulls qualify (the 159 m Bl'Arek doesn't), Dis's Terran
  fighters 4 of 5 GoF3 hulls with their GoF3 exhausts, the freighters untouched. Candidates are cached per race for 5 s
  (a scan cost ~2 ms a roll). A kit ship's NPCs fly random builds (`Traffic.Create` -> `ModTraffic.Assembly` ->
  `RandomBuild` -> `ModShipKits.VariantAssembly`, the variant in `NpcShip.ModelPath` for the proxies; `traffic.randomBuild`,
  default true): per slot a part the type may fit, a tier, a kit preset's colours (75 %) else a palette colour per slot, a
  skin 30 %; only from what is loaded (`ModShips.PartLoaded` / `BuildLoaded`: the tier steps down, then the skin goes, else
  the default build), so it assembles at once; `ModTraffic.Warm` (each `StationLevel.Awake`) starts 3 (phones 1) random
  full builds' parts loading through `ModShips.Warm` (`LoadOnDemand` quiet: no KitPartsLoaded / ModelsChanged of its own,
  unless a player build waited on the same files, `notifyPending`) until a kit has 150 (phones 40) part files loaded. A
  proxy whose build isn't loaded in that game shows the default build. The ES2 importer gives every ES2 ship
  `"traffic": { "races": [0, 1, 2, 3, 8, 9] }` (every race but the Specters). Verified in Play mode (es2_ships + gof3_ships,
  50 %): all six races roll ES2 ships (Void / pirates only those), 12 random Sentinel builds all different, four warm-ups
  taking its slot parts from ~7 to 15, a random-build Sentinel (Tormentor wings, body C) patrolling Dis, 2400 rolls in 57 ms.
- **Mod blueprints** (`ModBlueprints`, a mod's `blueprints.json`, parsed with the rest in `ModContent.Parse`): a "ship" or
  "item" product, ingredients, requiresShip (a skin: built only while flying that ship, which it rebuilds, the Kaamo upgrades
  kept), autocomplete (true / false / a price: `Blueprints.AutoCompletePrice` / `CanAutocomplete`, the hangar hides the row),
  available, unlocked (known once available, `UnlockAvailable` at `Shop.EnterStation`), and three sources while available and
  not known (`Offerable`): "lounge" (`AgentGenerator.AddModBlueprintSellers`, a local visitor with offer SellBlueprint, the
  openers 874 / 875 in `LoungeChat.StoryLine`), "derelict" (`TrafficPlan.Build`: one hackable freighter wreck per visit at
  most, `TrafficPlan.HackableWreck` + `SpawnSpec.modBlueprint`; `Traffic.OnHackWon` leaves a data crate; "stations" limits it
  to those orbits, `RollDerelict(station)` / `DerelictAllowedAt`, the chance then defaulting to 100, a list of unknown ones =
  nowhere; "position" [x, y, z] game units in the orbit, else out in front) and "drops" per race (`NpcShip.OnDied`, a kill by
  the player: `RollDrop`, a data crate). A data crate is a `Crate` (look 3, the junk container) holding the product item;
  `CombatRadar.Capture` learns a mod blueprint's product instead of loading it ("Blueprint found: X"). An item product gets the
  recipe on its `ItemData.blueprint`; a ship product is a hidden "blueprint item" ("<mod>:blueprint:<id>", numbered like a mod
  item in `ModContent.EnsureMapping`, made in `Apply` (`Deed`), its price the ship's after `ApplyShips`; `ModContent.ItemText`
  / `ItemInfo.ItemIcon` / `ItemInfo.Category` show it as the ship, "Ship blueprint"), so the Blueprints tab, the saves and the
  crates need nothing new. A finished ship never enters the hold: `Blueprints.Produce` leaves it pending at the production
  station, `Hangar.DeliverBuiltShips` (the hangar's Finish there, `StationMenu.CheckPendingProducts` on docking) swaps it in
  like a purchase: the old hull traded in at its price into the dealer list, or parked in the Kaamo Club when owned and free of
  that type; `level.ReplacePlayerShip`. The details show the ship's stats (`HangarWindow.ShowBlueprintDetails`), "Let me see
  it" the ship (`LoungePanel.ShowGoods`). Verified in Play mode with a test mod: the tab and details, autocomplete and the
  delivery (trade-in credited, gear moved), the skin's refusal and rebuild, the lounge seller's offer, a derelict hacked into a
  data crate, a pirate kill's drop, the capture unlocking it.
- **Custom hangars and bars** (`ModInteriors`, a mod's `interiors.json`: id, type hangar | bar, model, scale, materials, fov
  (degrees), near / far (metres), ambient, lightIntensity, fog [r, g, b, end m]; hangar startYaw (Unity degrees), parkedMax,
  flights, cruise, gateSpan; stations.json `hangar` / `bar` = an interior key, `interior` still the race): the GLB loads with the
  station models (`ModStations.LoadInterior`), its glTF cameras / lights removed; marker nodes by name, positions only (room-root
  space, Unity metres): hangar `pad` (required), `camera` (required), `camera_target` (default the pad), `parked_N`, `gate` +
  `gate_out` (the flights' lane: `StationLevel.CustomLane`, gateSpan default gate y ± 30, cruise pad + 40), `light`; bar
  `camera` + `camera_target` (required), `camera_start` + `camera_start_target`, `visitor_N` (at least one), `light`. A missing
  required marker = a warning and the race's room. `StationLevel`: `customHangar` / `customBar` replace the room, the pads
  (`PadPosition` / `ParkedPosition`: marker + `StationTables.ShipY`, no lift table), the parked slots / max (`HangarTraffic`),
  the lane, the camera (drift around the marker, LookRotation to the target, `SetCustomLens`), the key light / ambient / fog
  (`SetCustomFog`), the visitor slots (`SlotFeet`) and the bar camera (A / B from the markers; the sway turns about the camera
  spot, not the room's origin); HangarIndex / BarRace stay the race's for the parked fighters, glows, billboards and music.
  Example: frontier_systems' kepler_hangar / kepler_bar (primitives at the originals' scale: hangar 600 x 200 x 600 m, bar
  300 x 70 x 300 m). Verified in Play mode: both rooms built, docked in the custom hangar (the pad, a parked ship, the camera),
  the fly-in through its gate, the custom bar (visitors on the markers with their plates, the windows showing space); the
  visitors read as backlit figures as in the original bars.
- **Skies** (systems.json `"skybox"`: nebula / stars images, stars 0-2 or "none", nebulaBrightness / starsBrightness,
  rotation [x, y, z] Unity degrees; `ModWorld.Skybox` / `SkyOf`): `OrbitBuilder.SetupSky` (not for a nebula override such as the
  prologue's) sets GoF2/SpaceSky's `_NebulaMap` / `_StarsMap` with the keywords `_NEBULA_PANORAMA` / `_NEBULA_STRIP` /
  `_STARS_*` (`multi_compile_local`, so a build keeps all nine variants) by the image's aspect (2:1 panorama, 6:1 cube-face
  strip; else a warning and the game's layer), `_NebulaGain` / `_StarsGain`, and a fixed `_SkyRotation` when given (else
  the per-station R_sky). The shader samples the images directly at mip 0 (no baking; no seam at the panorama's wrap, the
  strip's faces kept half a texel in): panorama u = atan2(x, z) / 2π + 0.5 (+Z in the middle, +X at 0.75), v =
  asin(y) / π + 0.5; strip order +X, -X, +Y, -Y, +Z, -Z, each as seen from inside (up face: front at its bottom, down face:
  at its top). Images through `ModBackdrop.Texture` / `ModMaterials` (preloaded with the backdrops, `ModWorld.BackdropTextures`).
  Example: frontier_systems' 2048 x 1024 Kepler nebula (generated, seamless) over the game's star layer 1. Verified in Play
  mode: a labelled debug strip with a zero rotation (each face on its axis, upright, the up / down faces as documented), the
  panorama with the per-station turn, no seam.
- **Characters** (`ModCharacters`, a mod's `characters.json`: id, name (per language), portrait PNG, race, gender, mirrored,
  background, frame, replaces): `NetAdmin.ResolveSpeaker` takes a character by key "mod_id:id" (before the story speakers),
  else after them by id or name (any language), "x as Name" renaming; the spec is "-3 US rename US key" (`Events.SpeakerSpec`,
  which also parses the others: -1 face, -2 reader, ≥ 0 story). `NetAdmin.SplitSpeaker` finds the speaker's colon by trying
  each colon in turn (a key holds one). Drawn by `Portrait.ShowCharacter` (the PNG covering the 160 x 200 layers, background
  cover, top-centred; the game's background under it and frame over it unless off (the "frame" `portrait_frame.png` is a
  faint full-box scanline overlay, alpha <= 27, not a border; the background a dark half-transparent fill, seen only
  through a transparent PNG; the builder calls them Scanlines / Dark backdrop); `mirrored` flips): DialogueView
  (`Page.character`), the radio box (`Traffic.Chatter.character`), EventScreen's question and mission card, the lounge
  client and the Missions window (`EventMissions.Offer.character`; the character's race / gender pick the bar figure).
  `replaces` (a story speaker by name / first name / number): `Portrait.ShowSpeaker` draws the character instead, story
  included, and `StoryTable.SpeakerName` gives its name when it has one (every header, the radio, `%player%`;
  `OriginalSpeakerName` for lookups: the replacement's own resolution, /dialog names, which find either). `TextReveal.Names` tints character names as people (rebuilt when `ModManager.Revision` changes). Portraits
  through `ModMaterials` (Preload from the main menu). Example: frontier_systems' Captain Vega. Verified in Play mode: a
  dialogue page by id, one by key with a rename, a replaced story speaker (mirrored), a radio call in flight.
- **Dependencies** (`ModManifest.dependencies`, "id" or "id>=version" → `dependencyVersions`, `VersionAtLeast`): `Active`
  drops a mod whose dependency is missing, broken, off or too old (`InactiveReason` says which); `SetEnabled` on turns the
  dependencies on (recursively), off turns the mods that need it off (`NeededBy`); the browser lists each dependency's state
  and "Needed by". Another mod's content by key "mod:id": items.json / ships.json `base` / `override`, systems / stations
  refs, and the event graphs' names (`NetAdmin.FindShip` / `FindItem` / `FindHull`, `NetTeleport.ParseStation`).
- **Quests and bar missions** (event graphs in a mod's `events/*.gof2event`, found by `EventRunner.Load` / `List` after the
  Events folders, the later mod in the load order winning; then the built-ins): see "Event graph quests" in Multiplayer.
- **Voice-over** (`ModVoices`, `ModAudio`): "[voice <clip>]" in a Dialog page or a Radio line (`ModVoices.Take` in
  `NetAdmin.Apply`'s Dialog / Radio orders); the clip is an original voice line by its event name or a mod's
  `voices/<de|en>/<clip>.ogg|wav|mp3`, else `voices/<clip>.*`; loaded in the background (`Preload` when the order arrives;
  `ModAudio.Load`: UnityWebRequestMultimedia from the folder, a zip's file copied to temporaryCachePath/ModAudio first);
  `StoryAssets.Voice` falls back to `ModVoices.Get`, so the dialogue window and the radio box play it; `EventScreen` holds a
  queued dialogue up to 3 s while its clips load.
- **Music** (`ModMusic`): a mod's `music/<name>.*` named like an original clip (the asset names in Audio/MUSIC, DLC_MUSIC,
  DLC2_MUSIC) replaces it at every music start (`ModMusic.Replace` in Traffic, CampaignLevel.PlayMusic, StationLevel,
  MainMenu (swapped in when it loads, `ModMusic.Changed`), EndingCredits, StationMenu's Void alarm, EventScreen.PlayMusic);
  other names are new tracks: `/music <name>` (Order.Music a = -2, `EventScreen.PlayModMusic`), the Play Music node's Mod
  track, systems.json `spaceMusic` / `stationMusic` and stations.json `music` (`ModWorld.SpaceMusic` via
  `Traffic.RaceSpaceMusic`, `ModWorld.StationMusic`). All tracks load (kept compressed) when the mods change; the menu's
  Leave waits for `ModMusic.Ready` too.
- **Loading** (startup lag, `ModLoading`, `UI.ModLoadingView`): everything the mods bring loads at once in the background
  from the main menu's start (and again when the mods change): every ship together (`ModShips.LoadShip`: the files read on
  worker threads, the textures decoded by UnityWebRequestTexture off the main thread (`ModMaterials.PreloadTexture`, into
  the cache `ModMaterials.Texture` reads; zip files copied out once by `ModInfo.LocalFile`, cache folder by the zip's date and
  size), glTFast's main-thread work for all of them in one `TimeBudgetPerFrameDeferAgent` (0.6 of a frame; the old
  `UninterruptedDeferAgent` built each model in one go), the stations and rooms (`ModStations`), the weapon fx
  (`ModWeapons`), the music tracks and sound effects (`ModAudio`, `ModMusic`, `ModSounds`), the texture replacements (`ModTextures`), then the ships' hangar shadows
  (`World.ShipShadowBaker.ModShipsBaked`, see "Station scene"). Textures are compressed to DXT1 /
  DXT5 with their mipmaps by a Burst job (`ModTextureEncoder`, van Waveren's real-time encoder, native buffers: Texture2D.Compress
  took 110-145 ms per 2048 px texture on the main thread, a managed encoder's garbage a 2 s collection) where the GPU reads
  DXT; elsewhere (phones) Texture2D.Compress, one texture per frame. The result is kept on disk (`ModTextureCache`,
  `persistentDataPath/ModCache/Textures/<mod id>/<hash>.tex`, the hash of the file's path, size, change time, linear and the
  kind of compression): a later start reads it back on a worker thread instead of decoding and compressing (five PR ships
  2.3 s -> 1.3 s; the first start writes it, 126 MB for their 65 textures); an uninstalled mod's folder goes on `Scan`. The startup splash shows "Loading mods" with a progress
  bar and the ship / track in progress until it is done (`MainMenu.WaitForMods`, then the title; not shown when
  the mods that are on bring nothing to load, `ModLoading.HasWork`: no mods or only quests), so does the fade before a
  game scene when the mods changed in the menu (Leave). Measured in the Editor with the five PR ships: 5.3 s with frames of
  100-180 ms before, 0.9-1.9 s after with smooth frames (two of 83 / 164 ms right after the menu scene's load).
  Faster loading (2026-10-11, measured with the GoF3 Ships mod: 79 GLBs, 329 textures, 24-thread CPU, Editor, from the
  ships' start): cold 5.25 -> 2.92 s, warm 1.2 -> 1.0-1.1 s with the hangar shadows done 0.1 s after the ships (were up to a
  second, one hull per frame; now hull reads until 3 ms of the frame, `ShipShadowBaker.PrebakeReadMs`), GCs 25 -> 12 cold.
  The encoder spreads a texture over the job workers (`IJobParallelFor`: the role's conversion per pixel row, each mip level
  per row, every block row of every level at once; one serial job per texture before) and does the metallic-roughness /
  normal conversion itself (`ModTextureEncoder.Remap`; `ModMaterials.Convert` on the main thread is now only the fallback);
  output byte-identical to the old encoder (all 329 cached files compared). Cached textures don't wait for a decode slot
  (`CacheSlots` 16 / phones 4; `DecodeSlots` now processorCount / 2, 4..12, phones 2), their data and the encoder's output
  stay native (`ModTextureCache.Entry.data` is a NativeArray read straight from the file; the cache file written from
  `Work.Data` on a worker thread: the 1-5 MB managed copies drove the collections), and uploads share a 4 ms budget per
  frame (`ModMaterials.UploadTurn`). `ModShips.Ready` asks `ModContent.AnyShipModels` (ActiveShips rebuilt every entry: ~200
  KB and 2 ms per call for 79 ships, three calls a frame from the loading screen). Left: the GLBs are read whole into
  managed arrays for glTFast (~430 MB for this mod, most of the warm load's GCs).
  Phones (no DXT): the same jobs encode ETC2 (`ModTextureEncoder.Target.Etc2`, ETC2_RGB / ETC2_RGBA8, cache kind "etc2";
  every OpenGL ES 3 / Vulkan device reads it) instead of Texture2D.Compress on the main thread, one texture a frame: the colour
  in ETC1's individual / differential modes, never T / H / planar, the table and indices chosen by each pixel's brightness
  offset, the alpha as EAC. Checked in the Editor with Unity's own ETC2 decoder (2048 px ship maps): RGB 33.1 dB (Unity's
  fast ETC2 32.9, normal 36.6), a normal map with X in alpha 30.3 / 53.9 dB (Unity fast 30.1 / 54.1); with 4 job workers
  and Burst safety checks off 34-41 ms per opaque texture with its mipmaps, 104-135 ms with alpha, off the main thread (Unity
  fast: 31-51 / 363-418 ms on it, the top level only); the whole loader forced through it built all 79 GoF3 ships. On a
  phone (S24 Ultra): the 79 GoF3 ships cold in 9.5 s, nothing compressed on the main thread, the hulls
  checked up close. Texture2D.Compress stays for a GPU that reads neither; a phone's old "gpu" cache files are no longer read
  (nothing deletes them).
- **Mod Builder** (remake, experimental; every kind of content, see "Its other kinds" below): the main menu's Mods button opens a hub
  (`MainMenu.BuildModsHub`, `modsHubPanel`: Mod Browser / Mod Builder; Back from either returns to it). The projects page
  (`ModBuilderProjects`, `modBuilderPanel`, styles `.mb-*` in MainMenu.uss): `persistentDataPath/ModProjects/<id>`
  (`Modding.ModProject`: a mod folder in the normal format, mod.json / ships.json / models / textures, plus builder.json
  (never exported): each ship's test loadout, the test cheats, the last ship), the field above the list both searches it (every word in the name, id, author or description) and names a new project (Create / Enter), each row with what it holds ("1 ship · 1 skin",
  else Empty) and a picture (the first ship's icon, its own or the original's, else a skinned ship's, else an item's), New / name / author / version / description,
  the content kinds (Ships, Items, Characters, Skins open the builder on that kind, `ModBuilder.OpenKind`; the others greyed
  "Coming soon"), Open in hangar, Export as mod (zips it into the Mods folder as "<project name>.zip"; the mod id follows the name like a GitHub repository's: shown in grey under the Name field ("Mod id: x (was y)"), taken when the name is saved or left (`ModProject.Slug` / `IdFromName` / `ChangeId`: mod.json, the project's own "id:" keys, its folder, the old id kept in builder.json "previousIds" so the next export replaces the mod installed under it and keeps it on); zip mods are opened shared for delete, `ZipSource`, so the Editor no longer locks the old zip,
  replacing an installed mod of that id after asking, `ModImport.Delete`), Open folder, Delete. Opening one
  (`Modding.ModBuilder.Begin`): a throwaway free-play game (completed world) docked at the project's station (Dis, 70, until
  the top bar's Hangar button picks one station per hangar type: Terran Dis, Vossk, Nivelian, Midorian Var Hastra, deep
  science; builder.json "station", `ModBuilder.SwitchStation` reloads the scene, `CanHost` leaves out empty orbits, the Kaamo
  Club, the pirate bases and Loma; the test flights fly from that orbit), the project the only active
  mod (`ModManager.SetBuilderProject`: Active = the project and its dependencies, the enabled mods aside), nothing saved
  (`SaveGame.Save` / `AutoSave` refuse while `ModBuilder.Active`). The Station scene then shows `ModBuilderView`
  (`Resources/GoF2ModBuilder/ModBuilder.uss`) instead of the station menu (StationMenu hides its children and hands Update
  over): ships list (+ new ship from .glb via `ModImport.Pick(title, filter, exts)`, + change a game ship, remove), tabs
  Ship (name, description, race, stats, equipment slots, model; its length / scale +-10 %, turns about all three axes
  (ships.json `modelYaw` + the new `modelPitch` / `modelRoll`, `ModShipBuilder`: Euler(pitch, yaw, roll)), position (the new
  `modelOffset`, game units in the ship's frame, nudges of 2 % and Centre on pivot; moves and resizes carry the hardpoints
  along, every change re-measures the hangar height), hangar height + Auto, hull textures: one
  `materials` entry for all meshes, for GLBs without their own), Hardpoints (add by clicking the hull, list, X / Y / Z, mirror,
  centre, delete, slot counts following the points, a turret's facing (see "Missile turrets and salvos"), engine flame
  radius / length / particles), Loadout
  (per-slot item pickers, Auto-fill, god mode / infinite ammo). The 3D editor is `World.ModBuilderGizmo` (balls drawn over the
  hull by `Hidden/GoF2/BuilderGizmo`, Unity-style move arrows along the ship's axes (X = game +x = the pilot's left), a drag on
  a ball slides it over the hull (MeshColliders on layer 22), a flat arrow on the floor toward the nose); the camera is
  Inspect ship's orbit (`StationLevel.BeginInspect(minRadii)`; a middle-mouse drag pans its point, `HangarInspect.Pan`, at
  most 2 x the ship's radius off its centre, kept through rebuilds; a middle click centres it again). An Engines slider (off at the left, throttle up to 90 %,
  then the boost) lights the glow and a standing exhaust plume (`ShipExhaust.stationary` / `stationaryBoost`: a rate over
  time, the plume normally comes per distance flown). Below 1.58:1 / 1.4:1 the root takes `mbv-narrow` / `mbv-compact`
  (narrower panels, smaller text); the kinds strip scrolls sideways so Save / Exit stay on screen. The panels fly out as the ship takes off and back in once it
  has landed, like the station menu (`ModBuilderView.UpdateUiAnimation`: 450 ms, ease-out cubic, each panel to its own
  edge and fading, one after another; no input meanwhile). Edits save at once; model-shaping ones (model, size, turn,
  engine points, hangar height, textures) re-read the project (`ModBuilder.Reload`) and rebuild the turntable ship when its
  model is ready. A new ship without hardpoints gets a first set from its bounds (two guns, a secondary, an engine) and its
  hangar height. A new ship gets its shop icon rendered once its model is built (`Modding.ShipIconMaker`, the Hull Icons
  builder's look at runtime: left side from above, orthographic, alpha from a black and a white render, over
  `Resources/GoF2Icons/frame_ship.png`, which **GoF2 > Build > Ship Icon Frame** / Hull Icons write) into `icons/<id>.png`
  and ships.json `icon`; builder.json keeps the file and the look it was made from (model file, turn, hull textures), so it
  is made again only when that changes, and an icon the modder set by hand is never replaced. Test fly: the project read again, the loadout mounted, the cheats granted (`Cheats.Grant`, honoured while
  `ModBuilder.Active`), the normal take-off (`StationLevel.Depart` now waits for the orbit camera's blend back first: the
  flight's view-edge shrink is computed from the camera as it starts) into the builder station's orbit: `SpaceLevel` passive (no traffic,
  wingmen), jumps / gate refused, no tutorial popups, `World.ModBuilderRange` keeps 3 hostile pirates coming (6 s after a
  death); docking, the pause menu's "Back to Mod Builder" or a death returns to the builder (`ModBuilder.EndTestFlight`).
  **Undo / redo** (`Modding.ModProjectHistory`, `ModBuilder.History`, made in `Begin`, gone in `End`): a step is the
  project's JSON (`ModProject.CaptureState` / `RestoreState`: the manifest but its id, ships, items, characters,
  builder.json but "station" / "lastShip" / "previousIds") and its models / textures / icons / portraits by content hash,
  each content kept once in `persistentDataPath/ModBuilderHistory/<project>_<ticks>` (a crash's leftovers pruned at the next
  Begin), so an overwritten skin texture or a removed model comes back; recorded at the save points (`SaveNow`'s debounced
  save, `ModBuilder.Reload`; nothing while unchanged, 100 steps), a ship's generated icon joins the last step
  (`Absorb`). The top bar's Undo / Redo (enabled by `CanUndo` / `CanRedo`; an unsaved edit counts) and Ctrl+Z / Ctrl+Y /
  Ctrl+Shift+Z (not while typing: a text field keeps its own) put the step back, save it, re-pick the ship / item /
  character by `Ref` and read the project again. Verified in Play mode (a Veteran override's name and a hardpoint, undone
  and redone, on disk too); the keys not through simulated input.
  The top bar's Menu (`Localization.Get(172)`, also Esc with nothing else open; `ModBuilderView.ShowMenu`): Resume, Options
  (the game's `OptionsView` in the builder's modal, Q / E switch tabs, Esc back to the Menu), Save, Exit the Mod Builder
  (`ModBuilder.End`: the main menu's projects page). The orbit camera turns and pans while the preview rebuilds (only the
  hardpoints wait for the ship), a re-begun orbit keeps its zoom (`HangarInspect.Begin`), and a drag whose release was missed
  ends on the next move; `ModTextures.Preload` resets its loading count (an earlier revision's loads left it above 0 for good,
  so the preview waited forever and the camera refused new drags). The top bar's kinds swap the side panels (`ModBuilderView.SelectKind`;
  the gizmo only with Ships): **Items** (`ModBuilderViewItems`, items.json as `ProjectItem`): new items from a game item
  (a copy: type, category, look) or changes to one, name, description, its own icon PNG, tech level, occurrence, prices
  and the base's stats by readable name (`ItemStats`; a new item's value equal to the base's isn't written), the
  remake-only stats of its kind and a turret's own model (see "Missile turrets and salvos"), "Fit to the
  test ship" (every slot of a weapon's kind, equipment in place of its category; a new item waits for the project to be
  read and numbered: `afterReload`); item edits reach the game when the kind is left or at Test fly. **Characters**
  (`ModBuilderViewCharacters`, characters.json as `ProjectCharacter`), edited in the station's Space Lounge (no ship,
  Test fly or Engines there): a new one from a PNG portrait, the conversation-style preview (`Portrait.ShowCharacter`),
  name, portrait, race, gender, mirrored / frame / background, and the optional story speaker it stands in for (a picker
  of the speakers' own portraits, `Portrait.ShowSpeaker(..., original: true)`); without one it is the mod's own character
  for its quests and events. **Skins** (`ModBuilderViewSkins`): the game ships the project re-skins
  (`ModProject.SkinShips`: builder.json "skins" + any `textures/(v_|sn_)ship_NNN_*` file), the ship on the turntable and
  in Test fly (`ModBuilder.SkinShip`, its own default loadout), its textures off the model's materials (the lights and engine glow layers too, marked Shared when other ships use them; a freighter's / the battleship's burnt wreck set, `CombatAssets.wreckDamaged`, which `ApplyWreckDamage` now gives the mod's textures through `ModTextures.OriginalOf` / `Replace`),
  each replaced by a PNG under the texture's exact name (`textures/<name>.png`, ModTextures; the project is
  read again) or saved as a template (`templates/`, never exported: blitted off the GPU, a normal map decoded from its
  BC5 / DXT5nm-style X Y to an RGB normal map). Overrides missing stats or slots get the game ship's when a project opens
  (`ProjectShip.FillFromGame`: a hand-written one had no slots). `ModProject.AddFile` never hands out a name twice in a
  run (ModMaterials keeps textures by path until the project is read again). Verified in Play mode: projects, open, a .glb ship (auto hardpoints,
  textures), a Phantom override, placing / dragging points, the loadout, test flight, dock back, exit, export. Not yet: a
  real file-picker run, touch / controller use of the gizmo.
  **Its other kinds** (2026-10-15): the kinds are `ModBuilder.Kind` (12) and each is edited where it lives in the game
  (`ModBuilder.PlaceOf`: Ships / Items / Skins / Blueprints / Campaign in the hangar, Characters / Quests / Audio in the Space
  Lounge, Systems on the star map, Stations / Weapon fx in space, Hangars & bars in the room itself); a kind of the other
  scene loads it (`ModBuilder.GoTo`, `ReloadSpace`, `ReloadStation`). The screens share `ModBuilderScreen` (top bar with all
  kinds, Undo / Redo, Menu with Dependencies, the modal pickers, the save / reload cycle, the status line):
  `ModBuilderView` over the Station scene (partials per kind: ...Blueprints, ...Campaign, ...Interiors, ...Quests, ...Audio,
  ...Systems, ...Kinds the dispatch) and `ModBuilderSpaceView` over the Space scene (FlightHud hands it Update while
  `ModBuilder.InSpace`; `SpaceLevel.HoldForBuilder`: the player's ship held, no traffic, the chase camera off; its own orbit
  camera; ...SpaceViewFx). Project files: `ProjectSystem` / `ProjectStation` / `ProjectInterior` (ProjectWorld.cs),
  `ProjectBlueprint` / `ProjectCampaign` / `ProjectGameOption` (ProjectGame.cs), `ProjectQuest` (events/*.gof2quest), audio as
  files (music/, sounds/, voices/); all in the undo steps. **References** (`ModRefs`): every picker of other content
  (`PickRef` / `RefField`) lists the game's, the project's own (by key) and every other installed mod's (read from its files,
  on or off); another mod's pick makes it a dependency (`ModProject.AddDependency`, with the installed version as the lowest),
  `ModsNamed` the mods the files name. **Test fly** only in Ships, Items and Skins (`ModBuilderView.Flies`); Blueprints /
  Campaign show their ship on the turntable (`ModBuilder.PreviewShip` / `PreviewLoadout`). **Play** (`ModBuilderPlay`,
  `ModBuilder.PlayTesting`): a play test, the game with the project on (the station menu, traffic, quests:
  `LocalEvents` runs no quests in the builder otherwise), from the builder's station, the campaign's start or a quest under
  way; the pause / station menu's Back to Mod Builder, a death or a failed quest end it (`EndPlayTest`: the builder's game
  as it opened). Blueprints: product, ingredients, skin ship, autocomplete, condition, lounge / derelict / drops. Campaign:
  the card, start, equipment, cargo, standing, story quest, mod-only galaxy / items / ships, traffic ships; new-game options.
  Quests: `.gof2quest` step lists compiled by `Events.QuestFile` (EventRunner reads the extension; steps dialog, radio,
  objective, goto, fight, cargo, reward, give, wait, waituntil, checkpoint, music, title, waypoint, startquest, complete,
  fail, command; the expressions `docked(station)` / `inorbit(station)` added for them). Audio: game tracks / sounds
  replaced (`Resources/GoF2ModBuilder/SoundNames.txt`, Assets/Audio's SFX folders), new tracks, voice lines, ▶ previews.
  Systems: `StarMap.OpenBuilder` (every system, the builder's own amber and dragged: `MoveSun`, picks reported, no travel,
  drawn under the builder's panels); name, owner, security, position, gates, gate station, sky, nebula / stars / sun images,
  sunlight, music, its stations. Stations: system, tech level, planet, the look (like a game station, a .glb or a game model,
  size, turn, centre), collision, interior race / own hangar / bar, planet image, music; Play docks there. Hangars & bars:
  `ModBuilder.PreviewInterior` makes `StationLevel` build the project's room, `StationLevel.FreeCamera` the orbit camera,
  markers as balls clicked into the room (MeshColliders on layer 21) into interiors.json "markers" (`ModInteriors`: they win
  over the model's nodes); a room on a game room (`gof2:hangar_terran`, `ModStations.LoadInterior`) gets its markers from
  `StationLevel.GameRoomMarkers`. Weapon fx: per part the base's, a game part (another item's, scale, recoloured), a sprite,
  a model or none; shot / blast sounds; the trail (items.json fx "trail": none, or colour / width / length, also on a weapon
  without one: `WeaponFx.trail` (`TrailLook`, not serialized), `RocketTrail.For(gun, look)`, `GunRig.EnableTrails`); a
  range where the parked ship fires at a target ship 300-450 m off, seen from the side (Gun + GunRig like NetShotMirror; the
  target's hit cube 35 m: at 900 m every shot hit as it left the gun). Tests: `ModBuilderKindsTests`.
  **Ship kits** (2026-10-16, #87's customizable ships as a builder kind; `ModBuilder.Kind.Kits`, before AiTools, KindCount
  14; `ModBuilderViewKits`, `ProjectKit` / `ProjectKitPart` in ProjectKits.cs, `ModProject.Kits`, file shipkits.json, in the
  undo steps as "kits", MCP entry kind `kits`): the kit (name, scale, yaw), its slots (add, rename, reorder, remove; a
  build text's own keys refused, `ModShipKits.Build.Reserved`), parts per slot from a .glb (copied to models/), a part's
  name, slot, ship types, model, tier models, stats, weapon slots and hardpoints (the hangar gizmo edits
  `ModBuilderView.EditedMounts`: the part's in this kind, else the ship's; slot counts follow the points only for ships,
  `SlotsFollow`), paint / lights slots (`ProjectKit.AddColorSlot`, a "paint" palette made once); colours, presets, skins,
  hardpoint models by Edit as JSON. The turntable shows the kit's first ship with the selected part (and preview tier) in
  its slot: `ModBuilder.PreviewBuild` written into `Session.ShipBuilds` by `ApplyShip` only in this kind (cleared for the
  builder's ships otherwise, so Ships shows the default build); the station rebuilding the ship once the parts load is
  picked up by `ModBuilderView.RebindShip`. Ships kind: "+ New ship from a kit", a kit ship's section (kit from the project
  or a dependency, kit type, default build per slot, tier; `ProjectShip.Kit` / `KitType` / `BuildPart` / `BuildTier`), no
  model section, no auto layout; its icon from the default build. A kit with `"parts": []` is a warning and left out
  (it was an error that broke the mod). `ProjectShip.MountJson` / `Copy` keep #87's mount fields (outline, builtIn,
  direction, rotation, nodes, fxScale): the builder used to drop them. Tests: `ModBuilderKitsTests`. Verified in Play mode
  (a two-part test kit): the kind's page, the part swap on the turntable, a turret point added to a part, the Ships page's
  kit section and icon, a test flight in the kit ship.
  **Quest graph** (2026-10-15, user's request "a full graph in the game just like in Unity"): every step type is a row of
  `Events.QuestSteps` (type, picker group, fields with their kind, the line it makes; the steps needing more, blocks
  included, are written by `QuestFile`), so a new step there is editable at once. Blocks (if / while / repeat / parallel /
  every / on / ask / vote / random) hold nested step lists; `who` on every step; quest `variables` (param / set), kinds
  quest / mission / event, `autoComplete`; `{variable}` kept in texts (`QuestFile.Clean` with the known names). The graph
  (`Events.QuestGraph`, plain): flow wires ARE the step tree (Next = the next in its list, a block output its list, Start
  the quest's `steps`; one wire into a node, like Graph Toolkit), unwired chains in `loose`, places in `pos` / `startPos`,
  value nodes in `values` wired into fields as `{ "$value": id }` (`QuestReader.E` / `Arg` / `S` / `Ref` resolve them,
  `QuestGraph.Resolve`). `UI.QuestGraphView` draws it (UI Toolkit: pan, zoom, bezier wires by Painter2D on a 100 km layer,
  drag to wire / cut / move, double-click for a step, Delete); `ModBuilderViewQuests`' Graph view (default; PlayerPrefs
  `mb_quest_graph`, Wider hides the list) with the inspector on the right, or List view. Newtonsoft trap: `new JArray(tokens)`
  COPIES tokens that still have a parent (`AddLooseChain` removes them first). **Unity graphs**: `QuestFile.FromScript` turns
  an event graph's script into steps (the lines it has no step for stay `command` steps; every editor template comes back
  line for line), `FromGraphFile` places them where the Unity nodes stood (`EventGraphFile` now reads `m_Position`, the
  script's `#@id` markers); the Quests kind imports a .gof2event converted or as it is, and converts a project's graph.
  Also: **Edit as JSON…** on every entry page (`ModBuilderScreen.EditJson`, in place: `ProjectEntry.Json`,
  `ProjectShip.Json` / `ReadMounts`), the items' and ships' less common fields (`ModBuilderViewMore`: price systems, Vossk
  only, always sold at, available, attributes by number, Default Economy prices; available, dealer, lounge, npcTurrets,
  priceDefault, engine flame), a system's stars brightness / fixed sky turn, Menu > **Mod details** (website, credits,
  preview.png) and **Translations** (`ModProject.TranslatableTexts` / `Texts` / `SetText`, text/<lang>.json), Skins >
  **Other textures…** (any game texture, the GoF2Backdrop list, `backdrop.json`). Tests: `QuestGraphTests`. Verified in Play mode: every kind's page, a quest compiled and played (its
  conversation in the station, back to the builder), a system on the map with its route, a game hangar as the project's
  room with its markers, a project station in its own orbit, a laser recoloured and a project missile hitting the target
  ship. Not yet: a system dragged by hand, a marker clicked by hand, the campaign play test.
- **Mod Builder MCP server** (remake, 2026-10-15; user's request: the player's own AI assistant builds mods with the game's
  checks, no AI chat in the game; `UI.ModBuilderMcp` (+ `ModBuilderMcpTools`, `ModBuilderMcpPump`), the builder's **AI
  tools** button in the top bar between Redo and the Hangar button (not among the kinds: `ModBuilder.Kind.AiTools`, not in
  `KindTabs`; `ModBuilderScreenAi`, shown in the hangar's and space's builder screens): MCP's Streamable HTTP on a
  `TcpListener` (POST /mcp JSON-RPC 2.0 answered as application/json: initialize (protocol 2025-06-18 / 2025-03-26 /
  2024-11-05, `Mcp-Session-Id`), ping, tools/list, tools/call (text / image content, isError); notifications 202, GET 405,
  DELETE 200). Off by default (PlayerPrefs `mb_mcp_on`, port `mb_mcp_port` 47630, `mb_mcp_token`), Windows only (player and
  Editor, `Supported`), running only while `ModBuilder.Active` and not in a play test's own screens; 127.0.0.1 only, a
  Bearer token (constant-time), Origin / Host must be localhost (DNS rebinding). The HTTP thread never touches the Unity
  API (Localization, PlayerPrefs, Application, Time: `liveToken` / `liveVersion` are cached at Start): each call is queued
  and run on the main thread by the pump (`Call.Wait(condition, then)` for answers after a reload; screenshots at
  WaitForEndOfFrame). Edits go through `ModProject.CaptureState` / `RestoreState` like an undo (`ExternalEdit`: saved, an
  undo step, the screen refreshed, the project read again); files only inside the project's folders with their extensions
  (models, textures, icons, portraits, music, sounds, voices, events, text). **Permissions** (the tab's row, PlayerPrefs
  `mb_mcp_permission`, `ModBuilderMcp.Permissions`; user's request: an AI building a mod without the player watching the
  game): Read only (every change refused, the AI told to suggest instead), Ask always (every change waits for the player's
  Yes), Ask to remove (the default: removals ask, the rest goes through), Allow all (nothing asks); `ModBuilderMcp.Permit`
  gates every tool that isn't `readOnly` / `noAsk` (show_kind, end_play_test) in `Pump` (delete_entry `permitsItself`,
  after its own checks), with the tool's `ask` text; the question (`ExternalAsk`: Yes / No / Yes to everything, the last
  setting Allow all) waits up to 5 minutes (`AskSeconds`, the HTTP thread's wait grows by it; then a No, the dialog closed,
  `ExternalCancelAsk`); without a builder screen on show the change is refused; builder_status and the instructions say
  the mode (`livePermission` for the HTTP thread). Tools: builder_status, search_content (`ModRefs`: the game's, the project's, other mods'),
  list_entries / get_entries / put_entry / delete_entry (the mod files' own JSON; put answers after the reload with the
  mod's errors / warnings and a quest's problems, `EventRunner.CheckScript`), list_files / read_file / write_file,
  compile_quest, validate, get_quest_schema, play_test / end_play_test, get_log (a 600-line ring, the token redacted),
  screenshot, undo / redo, show_kind; and (user's request: the AI should see textures and models and do anything the builder's
  screen does; `ModBuilderMcpVision`) view_model (`UI.ModelViewer`: any model rendered off-screen from named angles,
  `ModelViewer.Angles` front / back / left / right / top / bottom / threequarter / rear, into one contact sheet, like
  `ShipIconMaker` on its own stage and layer 30 with the scene's lights off it and two lights following each view; "gof2:<name>",
  "ship:..." (its hardpoints as dots in ModBuilderGizmo's colours with "P1" tags, hollow where a ray against MeshColliders on
  the readable meshes meets the hull first), "station:..." (`OrbitBuilder.StationAssembly`), a project .glb through
  `ModGltf.Load`; the tags drawn after every tile in a 3 x 5 bitmap font of its own), view_texture (a project image through
  `ImageImport.Decode`, or a game texture by name: of a model, loaded, a GoF2Backdrop material, or the models whose names start
  it; blitted, a normal map decoded like the Skins tab's `DecodeNormals`, the alpha on a checkerboard / kept / a mask),
  list_textures, select_entry (`ModBuilderScreen.ExternalSelectEntry`: every kind, as a click on its row; the kind's scene
  first), view (`ExternalCamera`: `HangarInspect.SetView` on the turntable ship, the Interiors kind's free camera, the space
  screen's orbit, kept through the range's / station's own re-framing; answered with a screenshot, `clean` = the panels faded
  by opacity, `ExternalCleanView`), hardpoint (`ModBuilderViewAi`: list with the hull's box, add where a ray from a side through
  'at' / 'offset' meets the gizmo's pick colliders, the view's own `AddMount`; snap, select, mirror, delete), engines, test_fly
  / end_test_fly, weapon_range, switch_hangar (`ModBuilderView.HangarChoices`, shared with the Hangar button), list_projects,
  create_project / open_project (`ModBuilder.Begin` on another project without ending the builder, so the server stays up;
  the Station scene once `ModLoading` is idle), export_mod (asks before replacing an installed mod); loadout (the Loadout tab's
  `SetSlot` / Auto-fill / the test cheats; an item by number, key or the project's id), room_marker (`ExternalRoomMarker`:
  list with the room's box; place at a position or where a ray from a point inside the room 'toward' a side meets its pick
  colliders (layer 21), the camera-like markers a little off the surface like `PlaceMarkerAt`; remove; the room rebuilt;
  before its required markers exist the room isn't built, so the first go by position), view's `map` (`StarMap.FocusSystem`;
  not the overview: framing every system of the builder's map, placeholders far out too, broke it), and play-test input
  (`UI.McpInput`, only during a play test or test flight: press holds game actions by their current bindings
  (`GameControls.Paths`, keyboard first, else the controller's) and keys, steer holds the left stick (+ boost / fire /
  throttle), on a virtual keyboard and gamepad of its own fed in `InputSystem.onBeforeUpdate` like `VrPad`, state sent only
  while something is held; ui_buttons / ui_click find shown Buttons by text on every `PanelRenderer`'s root (by reflection) and
  send a NavigationSubmitEvent). The game window is unfocused while the player is in their AI app: a build drops keyboard input
  then and the Editor gives it to its own windows unless the Game view has the focus, so while something is held the input
  settings ignore the focus (`backgroundBehavior` IgnoreFocus, `editorInputBehaviorInPlayMode` AllDeviceInputAlwaysGoesToGameView)
  on a copy made once (an asset never changed), their own values back 0.3 s after the release (`McpInput.Tick` from the pump);
  swapping whole settings objects lost the original (the Input System destroys the default one it made). A tool's `ask`
  returning null passes the gate (hardpoint's / loadout's / room_marker's list). patch_entry (user's request: small edits
  without rewriting a quest; `Modding.EntryPatch`, plain C#: set / insert / remove / move / test by slash paths into the
  entry, "-" past a list's end, applied to a copy all or nothing, the rest byte for byte; get_entries' `path` reads a part):
  one undo step through `ExternalEdit`, an entry's id / override can't change, mod.json only its editable fields; a quest
  already laid out in the graph (`startPos`) gets its new steps placed by `QuestGraph.Layout(all: false)` and the steps
  after one in its list moved down when it lands on them (`MakeRoomAfter`); answered like put_entry after the reload
  (`AnswerAfterReload`: the mod's errors, the quest's own problems). Verified in Play mode: a laid-out quest's line changed,
  a radio step and a title inserted into its `if` block (the hand-moved node kept, the new ones placed, the one below moved
  down), a misnamed field caught by the answer and renamed by `move`, undo / redo, a ship's armor, an id change refused. The tab: on / off, the permissions, the state, the port, the token (hidden; Show, Copy, New token), the
  Claude Code / Claude Desktop (`npx mcp-remote`) / any-client setup with Copy (the token masked on screen), what the AI did
  (the button's green border while it runs). Docs: `Modding/ai/README.md`, SKILL.md. Verified with curl in Play mode: auth,
  the Host check, initialize, tools/list, put / compile / validate, a removal refused and confirmed, undo, a screenshot, a
  quest play test with its dialogue, the log, end play test, write_file and a character, a path escape and ships.json as a
  file refused; each permission mode (Read only refusing put_entry, Ask always's question answered by Yes to everything
  switching to Allow all, a removal under Allow all without a question, a No under Ask to remove); the vision tools: a game
  model and ship 28's contact sheet with its 12 tagged hardpoints, a project ship on a game model with hardpoints added onto
  its hull from the front / top / back and mirrored, a project .glb, station 70, the Veteran's diffuse / normal (decoded) /
  glow textures, select_entry of a ship (Hardpoints tab) and of a quest in its graph view, view from three sides in the hangar
  and from the top in the weapon range, a clean screenshot, engines, a test flight and back, the range with a recoloured
  weapon, the Vossk and Midorian hangars, a second project made, opened and left, export; a loadout set, auto-filled and its
  cheats switched; a project hangar on the game's Terran room given its pad and camera, then a parked pad found on the floor
  by a ray; the map centred on a system; a test flight steered (boost, fire), its autopilot menu opened and Dis picked by number
  keys, docked on held fast-forward; a quest played through by input alone (the conversation clicked away, Launch clicked, the
  orbit reached, the radio, the autopilot docking, its reward paid). Not yet: a real Claude client connected.
- **Leaving Play mode inside the builder** (Editor `ModBuilderPlayModeReset`, `ModBuilder.CloseAfterPlayMode`): Play mode runs
  without a domain reload and the builder's statics reset only when Play starts again, so after stopping Play mode with a
  project open it stayed the active mod and saving stayed refused: edit-mode tests run then saw its ships (65 instead of 64)
  and failed every save test (47). On entering Edit mode the session is dropped (the project saved, no scene change), the MCP
  server stopped and `McpInput`'s devices removed.
- **Builder star map under the panels** (`StarMap.OnEnable` / `baseSortingOrder`): the builder's map set its panel to "its own
  order - 1", but by then the panel settings were the map's runtime copy, 20 above the shared asset's, so the map (its names,
  frames) drew over the builder's panels; now the shared asset's order - 1.
- Orbit drags (remake, players' request): a vertical drag takes the camera down / up the other way round (a drag up tips the
  ship up) in Inspect ship, Action Freeze (`PhotoMode`, the original's y flip dropped) and the Mod Builder; keys and sticks
  unchanged.
- **Image formats** (`ImageImport`): every mod image (ModMaterials' `Texture` and background `PreloadTexture`, so skins,
  portraits, icons, hull textures, planets / suns / skies; ModInfo's preview; ModTextures' file list) and the Mod Builder's
  pickers (which store a picked image as PNG) read PNG / JPG by Unity's decoders and TGA (plain / RLE), BMP (8 / 24 / 32-bit),
  DDS (DXT1 / 3 / 5, uncompressed; not DX10) and the game's `.aei` (raw RGBA, the phone's ETC1, DXT1 / DXT5 by the data's size:
  the PC version's type 0x24 is DXT5) by its own decoders (`DecodePixels`, worker-thread safe, by magic; TGA by extension).
  Verified: each format against a reference image (size, orientation, colours, alpha), a PC `.aei` skin identical to an
  independent decode and shown on the turntable through the background loader.
- **Mod browser** (`ModBrowser`, the main menu's Mods button and `modsPanel`; styles `.mods-*` in MainMenu.uss): the list in
  load order (thumbnail, name, version · author, On / Off / Error / Can't load), a click or keyboard / controller focus selecting a row (the mouse's hover focus doesn't: passing over the rows on the way to Turn on changed the mod it acted on), the selected mod's preview, name, meta,
  description and credits (mod.json `credits`), where it is installed, errors and warnings, Turn on / off (also the
  controller's X from anywhere in the panel, `ToggleSelected`, its glyph on the button with a controller, `.mods-pad-x`), Earlier / Later, Open mods folder (the selected
  mod's folder, else `ModManager.MainFolder`: the user folder, the project's Mods in the Editor; desktop only, phones show
  the path), Refresh, Rebuild cache (`MainMenu.RebuildModCache`: `ModTextureCache.ClearAll` deletes `ModCache` (textures,
  hangar shadows) and the zip mods' unpacked files, then a rescan loads every mod again behind the fade with the loading screen).
  Mods change only in the menu: the tables are rebuilt when a game starts or loads. **Import mod** / **Delete** (`ModImport`;
  players couldn't reach the Android Mods folder): the system's file picker copies the picked file into
  temporaryCachePath/ModImport (Android: `ModImportActivity`, `Assets/Plugins/Android/ModImportActivity.java`, a see-through
  activity around ACTION_OPEN_DOCUMENT that copies on a worker thread and is polled through its static fields, registered
  in the unityLibrary manifest by the Editor's `AndroidModImportActivity`; Windows: `FileDialog.Open(title, filter, exts)`;
  Linux: zenity, else kdialog, on a worker thread; macOS: osascript's choose file; UWP: `FileOpenPicker` on the UI thread;
  the Editor: its open panel; none on iOS, `ModImport.Available`), `Inspect` reads it as a zip mod (mod.json, a valid id),
  `Install` moves it into `ModManager.MainFolder` as `<id>.zip` (a mod with that id: replaced in its own folder
  after the menu's Yes / No, keeping on / off and its load order); Delete asks (naming the mods that need it), turns it off,
  releases its zip and deletes the file or folder. The browser takes the menu's `ShowDialog` / `ShowNotice`. Verified in the
  Editor (a non-zip refused, a zip with a top folder installed, replaced by a newer zip and kept on, deleted through the
  dialog) and compiled for Android / Windows / Linux / UWP; not yet run on a phone, Linux, macOS or UWP.
- **Multiplayer** (`NetMods`): the Host card's "Mods (N on)" Off / Allowed (`mp_allow_mods`, off by default; a dedicated
  server's `-allowmods` / `ALLOWMODS=1` = every usable mod in its Mods folders). Every session starts without mods
  (`PrepareSession` → `BeginClient`); the host's `BeginHost` sets `ModManager.BeginSession` and `NetMods.SessionList`
  ("id@version#hash;..."), NetState carries it (`sessionMods`, FixedString4096) and a joining game turns exactly those on
  (`ApplyFromServer`) before `EnterWorld`. The connection data's 4th line lists the joiner's installed mods ("id#hash;...");
  `NetMods.Refusal` turns away a game lacking one (same files, on or off doesn't matter) with their names. "Same files" is
  `ModInfo.NetHash` (`NetSignature` in the session list): every file but those mod.json's `multiplayerIgnore` lists
  (`ModManifest.MultiplayerIgnores`: "folder/" or `*` patterns; never root `.json` files or `events/`), and mod.json only as its
  id / version / dependencies / that list, so a phone build with decimated models and halved textures plays with the PC build
  (the es2_ships importer writes `parts/`, `maps/`, `icons/`, `preview.png`); `ModInfo.Hash` (every file) stays the saves' signature. Lobby keys `mods` /
  `modnames`: the browser tags such games "Modded", shows the mods and "needs mods: ..." when this game lacks some (not
  joinable). `NetGame.OnMainMenu` → `NetMods.End` (single player's mods back); `NetGame.ResetDb` on every change.
- Verified in Play mode (ships): the three PR mods load without warnings; every ship on the turntable and the Falcon in flight
  (engine band, two turrets with the auto one under the hull, the turret crosshair); four lounge sellers at Var Hastra, the
  Falcon bought ("Okay.", trade-in); a Windows development build joining an Editor host with mods allowed: the session's
  four mods turned on, the five models built in the IL2CPP build, the host's Falcon parked in the client's hangar and seen in
  flight; the item window's 3D view. Not yet: a client refused for missing mods in a build (the check itself is verified).
- Verified in Play mode: the browser (folder, zip, broken mod with its line, toggling), the example's items in Var Hastra's shop
  with their texts / stats / icon, bought, mounted and fired with the N'saan's fx, a save with the mod turned off (the warning,
  removal, refund, placeholder), hosting with mods allowed (the session list, NetState, the join check both ways) and off (the
  original tables), the Host card row. Not yet: the server browser's Modded tag against a real listed game, a client build.

## VR (remake-only, PC VR through OpenXR; in progress)

Packages `com.unity.xr.openxr` (+ XR Plug-in Management, XR Core Utils). **GoF2 > Setup > Configure VR (OpenXR)** (`VrSetup`,
idempotent): the OpenXR loader for Standalone with Initialize on Startup off, the controller profiles (Oculus / Meta Touch,
Index, Vive, WMR, Reverb G2, Khronos simple). Code in `Scripts/Runtime/App/Vr` (`GoF2Remake.Vr`).

- **Only when asked for** (`VrMode`): `-vr` starts OpenXR before the first scene (`InitializeLoaderSync`, seated: tracking
  origin Device, recentred); without a headset / runtime it logs why and runs flat. `-vrsim` is the same VR layout on the
  desktop without a headset (right mouse drag looks around) for testing; in the Editor `GOF2_VR` = on / sim or PlayerPrefs
  `debug_vr` 1 / 2. `VrMode.Enabled` / `Headset` / `Simulated`.
- **Rig** (`VrRig`, one per scene, attached on sceneLoaded and after the first scene): the scene's camera stays the logical
  camera (`Camera.main`: projections, HUD markers, cutscenes, the chase camera unchanged) but renders nothing (culling 0, no
  post, desktop only, under the rest); the VR eye (`Camera.CopyFrom` of it, the eye's pose reset) on a rig that follows it
  after every camera script (`DefaultExecutionOrder(10000)`): its whole pose in flight, its position and yaw elsewhere;
  head (Input System `TrackedPoseDriver`, `<XRHMD>/centerEye*`), hands (`<XRController>{Left/RightHand}/pointer*`, small
  handles), the AudioListener moves to the head.
- **UI** (`VrPanels`): every PanelRenderer's PanelSettings renders into its own RenderTexture of the window's size (so the
  UI's screen <-> panel math stays exact) on a quad of the floating screen (URP Unlit transparent, layer 29, stacked by
  sorting order); assets get their target back when the scene ends. The screen: 1.9 m wide, 2 m ahead with a headset; in
  flight and in the simulation it fills the logical camera's view at 2 m (the HUD markers sit on what they mark; the
  desktop mouse lines up in the simulation). The star map's camera draws into a texture on the screen (the eye sees only the
  VR layer meanwhile; StarMap switches every camera off).
- **Controllers** (`VrPad`, a headset): the two controllers as one virtual Gamepad fed in `InputSystem.onBeforeUpdate`, so
  every control, its rebinding and the menus' controller navigation work unchanged: sticks, triggers, grips = LB / RB,
  right A / B, left X / Y, left menu = Menu, left stick click = View (autopilot menu), right stick click = D-pad left
  (actions menu); the right controller's laser is a virtual Mouse placed where it hits the screen (trigger clicks).
- In VR: the menu backdrop's camera doesn't turn and starts facing the station (`MenuCamera`), no launch / arrival camera
  (`Settings.LaunchCamera`), no mouse steering.
- **Cockpit** (`VrCockpit`, flight): `ChaseCamera` puts the scene camera rigidly on the seat (`VrCockpit.Seat`, 1 m above
  the ship's pivot, the ship's rotation; no lag, slide, shake or boost zoom), so the rig, the head and the cockpit (a child
  of the rig) ride with the ship; the player's hull is hidden (its renderers on layer 28, `VrRig.HiddenLayer`, which the
  eye doesn't draw; back while the cockpit is off). The same code-built cockpit for every ship
  (primitives, URP Lit dark metal: dashboard, front panel, hood, floor, walls, seat back, side consoles, canopy struts) until
  a modelled one replaces it. The flight HUD is split by region of its texture (`VrPanels.Crop` / `TextureOf`, panel pixels
  at 1920 x 1080): the canopy HUD shows only the middle (x 326..1594, y 0..1026: crosshair, markers, lock plate, messages,
  menus; `.hud-vr` centres the autopilot menu), the displays the corners (status bars + recharge icons left, cargo readout
  right, the secondary plate centre; the control hints aren't shown in the cockpit). The laser works in flight only while
  `Navigation.InputHalted` (a menu, conversation or map; else the trigger fires). Meanwhile the screen would sit behind
  the dashboard: with a headset it rises above it (1.6 m wide, 2 m out, its bottom edge 2 deg below straight ahead, tilted
  toward the eye), the simulation brings it 0.3 m out, in front of the cockpit (still filling the view). `VrRig.DebugLook`
  fixes the simulation's look for tests.
- **Cutscenes** (`VrRig.Cinematic`): while a level script's camera, the launch / arrival fly-in, a gate / Khador / planet
  jump scene or the death camera holds the scene camera and it has left the seat: stabilised, the rig at the cinematic
  camera's position with a level horizon (only its yaw: within 20 deg of the camera's heading it doesn't turn, beyond that
  at most 40 deg/s; never its pitch or roll, so a shot looking down has its subject below), the cockpit switched off (the
  hull shows), the screen at the 1.9 m size; every cut (60 m or 30 deg in one frame, entering or leaving) re-faces the
  camera's heading behind a 0.3 s fade from black (a quad at the eye).
- **Station** (`StationLevel.VrCamera`, `VrStation`): standing, still (no drift, sway or intro). The hangar: on the floor
  between the original camera's spot and the ship, facing it, the eyes 1.7 m above the hull's bottom, 5 m past its radius
  (the ships are ~60 m across: they tower over you); the bar: the original's view point B, level; the visitors turn to the
  head (`UpdateBillboards`, upright). The pointer past the UI (`VrRig.WorldPointer`: `VrPanels.UiAt` = a UI element under
  it, not a full-screen layer) picks a visitor (a 0.45 m capsule feet-head; the trigger / a click opens their chat,
  `StationLevel.VisitorPicked` -> `LoungePanel.OpenChat`), and with a headset the grip on the ship grabs it: pulling the
  hand right / left turns it (4 rad per metre along the head's right), letting go flings it (`FlingShip`).
- **Stick, throttle, radar** (`VrControls`, `VrRadar`, in the cockpit): a small side-stick on the right console and a speed
  lever (a shaft with a crosswise grip) in a slot on the left console, always there: not held they show the ship's inputs (the stick tilts with
  `ShipController.SteerInput`, the handle sits at the throttle). Options > Controls "VR flight: grab the stick and throttle"
  (`Settings.VrGrabControls`, VR only, off by default: the controllers alone as a gamepad): grabbed with that hand's grip
  within 12 cm, the stick follows the hand's travel (6 cm = full; forward = nose down, right = yaw right; `VrControls.Steer`
  replaces the touch stick in FlightHud) and its twist rolls (45 deg = full, `SetRoll`); the handle slides along its 28 cm
  slot (`SetThrottle`); the grips aren't LB / RB meanwhile (`VrPad.GripsAsShoulders`). The radar scope in the middle of
  the front panel: top-down, forward up, ships within 3 km (square-root scaled, the rest on the rim) red / green / yellow by
  `hostileToPlayer` / `friendToPlayer`, only with a scanner (`CombatRadar.HasScanner`), the station cyan, the player white,
  a dot above bigger / below smaller. In VR the HUD's radar ellipse and its off-screen dots / boxes / gate icons are hidden
  (they reached onto the displays; the scope has them).
- **Shaders and materials in VR builds** (#115): the VR materials come from templates in `Resources/GoF2Vr` (VrScreen,
  VrGlass, VrFrame; `VrPanels.FromTemplate`): `Shader.Find("Universal Render Pipeline/Unlit")` was null in the Windows
  build (no material uses Unlit, so it was stripped), `VrRig.Init` threw and the VR UI never appeared (`LateUpdate` now
  stops instead of throwing every frame). The hand-written shaders the scene cameras draw (SpaceSky, Backdrop, SkyLayer,
  SpaceDust, HangarShadow, Cloak, ShieldBubble, ModEngineFlame, ModHologram, ModNozzleShimmer) carry the single-pass
  instanced stereo macros (UNITY_VERTEX_INPUT_INSTANCE_ID, UNITY_VERTEX_OUTPUT_STEREO, the setup in vert / frag,
  `multi_compile_instancing`): without them they drew into the left eye only, so the right eye had no sky. A new
  hand-written shader drawn by a scene camera needs them too. Not verified with a headset.

## Recovered facts already implemented

- Flight (`FlightModel.cs`, from `PlayerEgo::handleShip / left / right / up / down / roll / update`):
  - base speed is 2 units/ms for every ship
  - handling `H = (handling/100 + 0.2*upgrades) * (1 + agility/100) * 20`
  - turn-rate target = `trunc(input*750*H)/63`
  - ramp = `dt*H/((3.3 - sens)*20)`; decay = `dt*H/126`
  - angle = `dt*rate*2π/65536*0.033`
  - boost speed = `int(2*boost/100)+2` (the integer truncation is intentional)
  - auto-level roll at 0.00025 to 0.00075 rad/ms, then a fine phase (overshoot 0.00035, then 0.0002); the lean is -right.y (#57: it was up along the horizontal part of 'right', nothing on a knife edge, so a ship on its side or upside down counted as level; checked from 800 random attitudes); remake (#37, Options > Controls "Level out also levels the nose", `Settings.LevelPitch`, on by default): the nose comes to the horizon too (world up = the station's, like the autopilot), a steep nose first, the roll re-checked until both are level, upside down the roll keeps its first direction; remake: steering ends a level-out (the original keeps rolling under the stick), once the stick was neutral after the press (the touch double tap's second tap is on the stick): with the nose levelling it pulled the nose back to the horizon whenever pitch was let go, and a steep nose never let it finish (`FlightModel.levelSteerArmed`)
  - a boost sets the throttle to 100 %; every shot kicks the camera (shake 50, 2)
- Chase camera: touch mode uses fixed damping (0.005 / 0.006). Mouse/controller mode depends on handling (position 0.01h·0.011 + 0.001, look (1 − 0.01h)·0.015 + 0.003). Each frame the camera and its look point move `ChaseCamera.Ease(x, dt)` = 1 − e^(−x·dt/2) of the way (the original's fitted damping polynomial, `aproximateCooefficientsForAproximationOfDampingFunktion`, within 2-3 % up to 50 ms frames; `ChaseCameraTests`). #114: the remake used 1 − e^(−x·dt) and caught up twice as fast, so the crosshair and the ship stayed near the middle in a turn. Boosting widens the FOV by 0.35 rad × the boost percentage and rumbles (pct × clamp(speed, 2, 8) / 50).
- The model's bank (`PlayerEgo::update` 0xaa5f4, `ShipController.UpdateVisualBank`): roll = −yawRate·2π/4096, tilt = pitchRate·0.25·2π/4096, straight from the turn rates, so it builds and settles with them (~0.5 s in, ~1.5 s out). #114: the remake eased toward an angle per stick input and snapped. A heavy debug hull eases it; free flight keeps its own bank.
- The dodge (`Maneuver`, `PlayerEgo::initManeuver` / `updateManeuver`, `MGame::maneuverTouchEnd`): a touch swipe off the controls (≤ 600 ms, > w/480·70 px sideways, < h/320·90 px up or down; A / D, the PC version's Move left / right; remake: a sideways flick of the right stick) slides the ship 1200 ms toward it (dt·4·(1 − t/1200)·H·0.05 units), turns the nose away (k·0.00628 rad per 30-fps frame) and banks by a yaw rate sin(πt/1200)·15H; no steering meanwhile, the autopilot included; each request adds 0.17 to the volatile goods. A player bomb's ignition adds 3 f to them (`addNukeVolatileForce`, f by the distance; the damage stays Extreme-only). The chase camera (`TargetFollowCamera`): the target offset, the look-at point, shifts x = −3 × the yaw rate, and the camera and that point both take back 0.9 × the slide per frame (`dodgeLag`); the camera looks at the eased point from where it is (`MatrixGetLookAt`), so it turns about 40 deg after the sliding ship and keeps it in view (the point is only ~34 m ahead of the camera). Until 2026-10-11 the remake put the shift on the camera position (scaled to 0.25) and eased a rotation aimed from where the camera should be, so the ship slid out of the frame (players compared videos).

Useful field offsets in the decompiled code:

- **PlayerEgo:** `0xb8` speed, `0xbc` throttle, `0x138` boost timer, `0x154` H, `0x278` pitch rate, `0x27c` yaw rate
- **Ship:** `0x4` armor, `0xc` cargo, `0x14` price, `0x18` handling/100, `0x1c` shield, `0x34` boostSpeed, `0x38` boostDelay, `0x3c` boostTime, `0x40` agility, `0x6c` equipment
- **Item:** `0x0` index, `0x4` type, `0x8` category, `0xc` tech level, `0x30` attributes

## Working with Reference/

- `python Reference/tools/show.py "^PlayerFighter::update$"` prints a decompiled function.
- Grep `Reference/decompiled/native/_ALL_FUNCTIONS.c` for callers, strings and IDs.
- `python Reference/tools/rf.py 000a8178` reads a `DAT_` constant (Ghidra image base 0x10000).
- Class overview: `Reference/decompiled/native/INDEX.md` and `CLASS_HIERARCHY.txt`.

## Known open items

- The FMOD event file (`Audio/_FMOD_GOF2.fev.bytes`) is parsed by `Reference/tools/audio/fev_lgcy.py` (the whole LGCY chunk from the decompiled loader `Reference/decompiled/fmodevent/`, libfmodevent.so via Ghidra: events, layers, sound instances, parameters, envelopes (their points are in EPRP), sound definitions with waves and banks; `--event <id|name>`, `--sounddef`, `--ids`, `--json`). `build_event_table.py` writes `Reference/research/fmod_event_ids.txt` (all 2293 system ids -> name, group, .ogg files); `fev_events.name_of` uses it. Envelope values: pitch ×2^(8v − 4), volume linear.
- Data conversion lost some non-ASCII characters (U+FFFD in `stations.json` names like "Neh?bru" and a few texts).
- `PartAnimation`: Scale keys are in the mesh's own (engine) axis order, not the Z-up swap of the position keys (the explosion debris streaks stretch sclZ 45x lengthwise; swapped they were km-long lines); every other animated mesh with uneven scale follows this too, only the explosion was checked in Play mode. The UV scrolls `v5_0` (u) / `v5_1` (v), 100 = one texture, run on every mesh (`_UVOffset` on GoF2/SkyLayer, else `_MainTex_ST`, which the Shader Graphs add: the remake subtracts the original's offsets, its textures are flipped; the burning stations' fire and smoke, plasma beams / streams, projectiles, gas clouds, the clamped fx atlases too: their scrolling cells stay inside the texture, the beam strips of sn_projectiles repeat every scroll period) and count toward the animation's length. The `extra` channel (0 to 100, opacity; `_Fade`, else the Shader Graphs' `_Color`) is only applied where `applyMaterialChannels` is on (the stations in their orbits, `OrbitBuilder.SpawnStation`: the blinking lights of the Nivelian / Midorian / pirate / Loma / Supernova-era stations and the Kaamo Club's flickering signs, frozen until 2026-10 (a player's report); an animation of only `extra` keys turns it on by itself and runs for their length (the Kaamo Club's lit panels, the plasma array stages' lights: they had no length and stayed off); on an opaque material it scales the colour, `_BaseColor` on URP Lit; the sky layers, explosions, gun projectiles / muzzle flashes / impacts via `GunRig.EnableFades`: an impact's 3600-unit glow is meant at 20 % for 267 ms; the hyper_drive, khador_jump, the gates' `_jump_anim_add` and the Void station explosion, which all fade to 0 by it); `v5_2`..`v5_6` aren't applied.
- `PartAnimation` starts every animation, and wraps every loop, at the original's range start: the file's smallest positive
  key (`LoadPoseMs`; `MeshCreateFromFile` 0x75c60 timeBetweenFrames, `Transform::InitAnimationRangeInTime` 0x7e31c,
  `SetAnimationState(3)`, `Transform::Update` 0x7e388), set in `Awake` as `loopStartMs` (an absolute time: code may set
  another after Awake, which never stacks on it; `SkyLayers.Add` only sets one it is given). 226 of the 243 animated files
  open with a one-off t 0 key (the animation from 33 / 50 / 100 ms), 125 with a different pose there; played from 0 they
  jumped on every wrap. Before this only the station rooms, the Void station and the sky layers skipped it; the other 94
  (weapon fx, explosions, the gates, the Kaamo Club / mining plant / plasma array effects, the deep science ships, the
  wrecked freighter, the Khador jump, the wormhole) now do too. Odd starts kept as the original has them: the Vossk bar's
  streaks 500 ms, the EMP rocket's flame 400 ms, the supernova flares 1000 ms; a range that starts at its end (one key:
  the magma asteroid) holds that pose. Not built: the original's explosion type 6 starting at 2300 ms (`Explosion::reset`).
- The default steering sensitivity is a guess (1.0); the original's touch / tilt sliders run 0..1 with 1.0 the maximum, the remake's touch slider 0.2-2.2.
- 13 resources referenced by the code aren't in the OBB (dev leftovers).
- Unity 7000.0.0a7 hands `ModelOrientationPostprocessor` part11 of `sn_cargo_001_midorian_wrecked` unreadable, so that
  60-vertex piece isn't turned 180 deg (a warning names it on import).

## Roadmap (suggested order)

0. Story: data, rules, dialogue, radio, docked / in-flight flow and the first levels done (see "Story"); freelance missions, the Missions window done (see "Bar and freelance"). The main story (0-45) with its levels, cutscenes, the Void and the ending done; the Valkyrie add-on (45-84) done; the Supernova add-on (84-162) built (see "Supernova add-on"), still to be played through.
1. Combat: guns, missiles, special weapons, turrets, sentries, ship damage, lock-on, NPC ships, capital ships, pirate bases and the combat equipment done (see "Weapons", "NPCs and combat", "Combat equipment"). Cargo stealing and the tractor auto modes done.
2. NPCs: free-flight traffic, fighter AI, wingmen, freelance mission orbits, capital-ship turrets, Wanted criminals, Specter cloaking done.
3. A star system scene: done (see "Space scene"), with the lens flare and the extra sky layers, autopilot, planet jumps, the star map and jumpgate / Khador travel. The Void's wormhole and home orbit done.
4. Stations and economy: station interior, shop, bar agents, blueprints, Status window done (see "Station scene", "Shop", "Bar and freelance"). Kaamo Club and the Most Wanted board done.
5. HUD and radar (`Hud`, `Radar`), then missions (`Mission`, `Objective`, `LevelScript`).
