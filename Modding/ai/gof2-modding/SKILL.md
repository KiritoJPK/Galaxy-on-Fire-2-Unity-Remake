---
name: gof2-remake-modding
description: Make mods for the Galaxy on Fire 2 Unity Remake - JSON data mods with new or changed items, weapons, ships, blueprints, new-game option cards, star systems and stations, campaigns, characters, music, sounds and skins. Use when someone wants to create, fix, extend or balance a GoF2 Remake mod, or asks how its mod files work.
---

# Galaxy on Fire 2 Remake modding

You help a player make a mod for the Galaxy on Fire 2 Unity Remake. A mod is a folder (or a .zip of it) of JSON files plus
the PNG / GLB / OGG assets they name. Mods contain no code. This file is the format reference with the rules; next to it:

- `reference.md`: every number and name of the original game a mod can refer to: items (every stat and raw attribute),
  item categories, attribute numbers, blueprint recipes, ships (hull size, hangar height, model name), every original
  ship's hardpoints as `mounts` JSON, the Default economy's numbers, systems, skies (what each `sky` looks like), stations,
  planets (what each `planet` looks like), story steps, story speakers, every model with the texture names a skin
  replaces, backdrop textures, music tracks, sound effects, the event sound / music words, the built-in event graphs.
- `voices.md`: every voice clip with its speaker and English line (for `[voice <clip>]`).
- `events.md`: every event graph node with its options, ports, types, choices and defaults, the expression words, every
  command with its syntax and the player selectors (for quest / bar mission / cutscene recipes).
- `guide.md`: the full modders' guide, every field of every file with more detail and examples than this file. When a
  field or behaviour isn't covered here, look it up there.

If you can't open them, ask the user to paste the part you need (or to attach it).

## How to work

1. Find out what the mod should do. Ask for anything that changes the files: names, which ships/items it builds on, prices,
   when content should appear, which assets (models, images, sounds) the user has or will make.
2. Write every file in full, each under its path inside the mod folder (`my_mod/items.json` ...). JSON only: no trailing
   commas. `//` comments are allowed but keep files plain unless the user wants notes.
3. Use real numbers and names: an original item, ship, system, station, speaker, story step, texture, model, music track,
   sound or voice clip is referred to by its number or name from `reference.md` / `voices.md`; never guess one. Content from a mod (this one or another) is referred to by its key `mod_id:id`.
4. You can't make binary assets. For every PNG / GLB / OGG a file names, tell the user exactly what to provide (size, format,
   layout, where to put it). Paths in JSON are relative to the mod folder, with `/`.
5. Event graphs (quests, bar missions, cutscenes) are made in the Unity Editor, not written by hand. For those, give the user
   a node-by-node recipe (see "Quests"). A simple quest or bar mission can be written by hand instead as a step list,
   `events/<name>.gof2quest` (see "Quests made as step lists"; the in-game Mod Builder writes the same).
6. Check your output against the checklist at the end before giving it.

The player installs the mod by copying the folder (or zip) into the game's `Mods` folder, pressing **Refresh** in the main
menu's **Mods** screen and turning it on. Problems show there with file and line: errors stop the mod, warnings skip a field.

## Basics

- **Units:** 20 game units = 1 metre. Times in milliseconds. Speeds in game units per millisecond.
- **Races:** 0 Terran, 1 Vossk, 2 Nivelian, 3 Midorian, 8 pirate, 9 Void, 10 Specter. Systems/stations use 0-3. Some fields
  take race names instead (`"terran"`, `"vossk"`, `"nivelian"`, `"midorian"`, `"pirate"`, `"void"`, `"specter"`, `"any"`).
- **Ids:** lowercase `a-z`, `0-9`, `_`, `-` (max 40 for the mod id). Ids are permanent: saves refer to them, never rename after
  release. Inside one file an id must be unique.
- **Keys:** the game knows mod content as `<mod id>:<id>` (`plasma_arsenal:plasma_lance`). Use the key to refer to another
  mod's content (put that mod in `dependencies`) or to your own content in other files. Your own items listed earlier in
  items.json can be named by key too.
- **Texts:** `name` / `description` are a string or one per language: `{ "en": "Plasma Lance", "de": "Plasmalanze" }`.
  Languages: en, de, fr, es, it, nl, pl, ru, pt, zh, hi, ar, ja, ko. Or `text/<lang>.json` files with keys like
  `items.<id>.name`, `items.<id>.description`, `ships.<id>.name`, `systems.<id>.name`, `stations.<id>.name` (an override uses
  the number: `items.12.name`).
- **Economies:** players pick Android (default numbers) or Default (PC prices). Ships have `priceDefault`; items have a
  `defaultEconomy` block with the same fields.

## Mod folder

Images (icons, portraits, textures, skins, planets, suns, skies, card art, preview) may be PNG (safest), JPG, TGA, BMP, DDS
(DXT1/3/5, uncompressed) or the game's .aei.

```
my_mod/
  mod.json            required
  preview.png         Mods screen image, 16:9 (640 x 360)
  items.json          new / changed items and weapons
  ships.json          new / changed ships (models in models/, icons in icons/)
  blueprints.json     blueprints for ships and items
  gameoptions.json    cards in the new game's Game options panel
  campaign.json       a whole new game under Start new game
  exhausts.json       exhaust styles any ship can wear (the hangar's Customize screen)
  systems.json, stations.json, interiors.json   new star systems, stations, hangars and bars
  characters.json     speakers with portraits
  events/*.gof2event  quests and bar missions as Unity event graphs
  events/*.gof2quest  quests and bar missions as step lists (by hand or the Mod Builder)
  text/<lang>.json    translations
  music/, sounds/, voices/, textures/          replacement / new audio and skins
```

### mod.json

```json
{
    "id": "canon_ships",
    "name": "All Canon Ships",
    "version": "1.0",
    "author": "Torch",
    "description": "The ships of Galaxy on Fire 3D and 3.",
    "preview": "preview.png",
    "credits": "Models: ..., licence ...",
    "dependencies": ["other_mod", "ship_pack>=1.2"]
}
```

`id` is required; `name`/`description` can be per language; `preview` defaults to `preview.png`. Optional `"multiplayerIgnore": ["parts/", "maps/", "icons/", "preview.png"]`: files multiplayer doesn't compare (models,
textures, icons; "folder/" or `*` patterns), so a phone build with lighter models and textures under the same names plays with
the PC build. Root `.json` files, `events/` and mod.json's id / version / dependencies always count.

## items.json

A list. Each entry adds an item (`"id"` + `"base"`) or changes one (`"override"`).

```json
[
    { "id": "plasma_lance", "base": 12, "name": "Plasma Lance", "description": "...", "techLevel": 3, "occurrence": 40,
      "price": [24000, 26500], "stats": { "damage": 14, "reloadMs": 700, "lifetimeMs": 1800, "speed": 16 } },
    { "override": 50, "stats": { "shieldCapacity": 65 }, "maxPrice": 4600 }
]
```

A new item is a copy of `base` (required: an original number or a key): it keeps the base's type and category and looks,
sounds and works like it (icon, projectile, special behaviour); every field you give replaces the base's value. Pick the
base for the behaviour you want (a blaster for a blaster, a homing missile for a homing missile). Blueprint recipes aren't
copied.

| Field | |
|---|---|
| `name`, `description` | String or per language. |
| `techLevel` | 1-10: shops sell items up to their station's tech level. 11-20: gear above the game's (the originals stop at 10): stocked only by the tech level 10 stations, at its `occurrence` (keep it low); a tier 5 pirate outpost's equipment is such an item one time in three; ordinary NPCs never drop gear from tech level 8. Combine with `available` (`won(main)`), a high price, a blueprint with `autocomplete: false`. |
| `occurrence` | How often shops stock it and NPCs carry it as loot, 0-90 (0 = never: only `alwaysSoldAt`, blueprints, rewards). |
| `price` / `minPrice`, `maxPrice` | `1500` or `[1200, 1500]`; a station's price lies in the range. |
| `lowestPriceSystem`, `highestPriceSystem` | System numbers where it's cheapest / dearest. |
| `vosskOnly` | `true`: only Vossk systems sell it. |
| `alwaysSoldAt` | A station number that always stocks it (78 = Var Hastra, the start; -1 none). |
| `stats` | Named stats (below); copy the base item's stats from `reference.md` and change them. |
| `attributes` | Raw attribute numbers `{ "9": 14 }` for anything without a name (not 0-8). |
| `defaultEconomy` | The same fields, laid over the main ones in the Default economy only. The main fields apply in both economies, so give only what differs (usually the prices). |
| `icon` | Own shop icon PNG, 180 x 88 (frame included). Default: the base's. |
| `fx` | The weapon's own look and sound (below). |
| `available` | A condition (see Conditions) before shops stock it or loot drops it. |

Stats (name = attribute): damage 9, empDamage 10, reloadMs 11, lifetimeMs 12 (shot flight time; with speed = range), speed 13
(units/ms), blastRadius 14, guided 15, automatic 16 (auto turret), turretTurnSpeed 17, shieldCapacity 18, shieldRegenMs 19,
armor 20, cargoBonus 22, tractorAuto 23 (1 nearest, 2 any crate), tractorLockMs 24, boostSpeed 25, boostRechargeMs 26,
boostDurationMs 27, agility 28 (steering nozzle), lockTimeMs 29 (scanner), showClassAAsteroids 30, radarShowsCargo 31,
drillSpeed 32, miningYield 33, cabins 34, cloakDurationMs 35, cloakChargeMs 36, energyCells 38 (per cloak), fireRateFactor 39,
damageFactor 40 (weapon mods %), emergencyMs 41, timeExtenderMs 42, timeExtenderCooldownMs 43, collectorSpeed 49,
collectorMagnitude 50, collectorRange 51, gammaShielding 52, beamRange 53, beamStrength 54, beamTargets 55, miningBeam 100 (a
drill based on 86-90 that cuts while flying), miningBeamRange 101, miningBeamLayerMs 102, miningBeamLook 103 (9/10/11/228), phaseCloak 104 (a
cloak based on 94-96 whose ship passes through asteroids, stations, gates, ships, shots and explosions while cloaked),
missiles 105 (1 = a gun's or turret's shots are homing missiles: a turret's home on its target, else on the scanner lock;
smoke trail, rocket burst; not for beams, bombs, mines or cluster missiles), salvoShots 106 (shots side by side per trigger),
salvoCount 107 (salvos per magazine, then reloadMs), salvoGapMs 108 (ms between them), turretRange 109 (auto turret search
range, units, default 60000), flightScope
110 (the item's booster / nozzle stats count in 0 both flight styles, 1 the original only, 2 free flight only), topSpeed 111,
turnRate 112, strafeSpeed 113 (% bonuses, both styles), originalTopSpeed 114, originalTurnRate 115, originalStrafeSpeed 116,
freeTopSpeed 117, freeTurnRate 118, freeStrafeSpeed 119, freeAcceleration 120, freeBoostFactor 121 (free flight's boost, 350 =
x3.5; default the booster's original boost speed x 3.2 / 3). See Flight styles. tractorTargets 122 (a tractor beam based on
68-70 / 194 pulling 1..8 containers at once: the extra beams take the nearest ones by themselves, no lock, the scanner stays
free), tractorRange 123 (their reach, units, default 30000), shieldDamage 124 (a weapon's damage to shields in %, default 100,
the Disruptor 183 and items based on it 200; only ships with a shield: players, not NPCs). A new item also sounds like its base (a booster's boost sound, a
tractor beam's beam).
items.json's own names (`loadingTimeMs`, `range`, `projectileSpeed`...) work too. An unknown stat name is an error.

### Weapon fx

```json
"fx": {
    "projectile": { "sprite": "fx/bolt.png", "size": 450, "stretch": 4, "alongFlight": true, "color": [0.75, 0.45, 1.0], "glow": 3 },
    "muzzle": { "sprite": "fx/flash.png", "size": 500, "lifetime": 90, "grow": 1.4 },
    "impact": { "sprite": "fx/impact.png", "size": 2400, "lifetime": 280, "grow": 2.2 },
    "shot": ["fx/shot_1.ogg", "fx/shot_2.ogg"], "shotLoops": false, "explosionSound": "fx/boom.ogg"
}
```

Each of projectile / muzzle / impact is `false` (none), `{ "color": ..., "glow": ... }` alone (the base item's own effect
recoloured: shape, animation and brightness kept, hue replaced; the easy way to recolour an original weapon), or an object
with the mod's own `sprite` (PNG in the mod, white on black or transparent; `color` tints it) or `model` (GLB; with
`texture` drawn glowing), `size` (game units; a blaster bolt is ~1600, an impact glow several thousand), `color`
(`[r,g,b]` 0-1, or 0-255 when a value is above 1, or `"#rrggbb"`), `glow` (>1 blooms; game effects ~2.5), `additive` (default true),
`lifetime` ms (muzzle 100, impact 300), `grow`, `fade`, `spin` (deg/s), `alongFlight` + `stretch` (laser bolts), `rotate`
(models). A beam laser's projectile (items based on 9, 10, 11, 228) is the beam: its sprite spans the whole beam, `size` wide,
turned about it to the camera, the PNG's height along the beam (vertical strip; alongFlight / stretch ignored, grow widens
only); a model is fitted `size` across and the beam's length along +Z. Without `sprite` / `model` a part may also take `item` (another item's number: its part instead of the base's,
e.g. `{ "item": 46, "scale": 0.35 }` the AMR Extinctor's missile at 35 %) and `scale` (size factor). `shot` /
`explosionSound` may be an item's number (its sounds; `shotLoops` then that item's unless given). `"trail"`: `false` (none)
or `{ "color": [r, g, b], "width": units (half; a rocket's 100), "length": ms (3000) }`, the shots' smoke trail (any
weapon). Turrets: `"turret":
"models/t.glb"` or `{ "model": ..., "muzzle": units, "shotStart": units, "scale": f, "offset": [x, y, z] }` is the
turret's own model (flight, hangar, other players): origin on the mount, 1 unit = 1 m, a node `pivot` (the base, turns)
holding a node named `*_gun` (tilts; barrel along +Z), else it turns as one piece; muzzle / shotStart along the gun node in
game units; `scale` resizes, `offset` moves it off the mount (game units, x pilot's left, y the turret's up, z forward);
without `model`, `scale` / `offset` reshape the base item's turret; `"item": 48` instead of `model` = that game turret's
model with its muzzle / shot start. Any `model` (turret, part) may be `"gof2:<model name>"`, one of the game's models from
reference.md's "Models and their textures" (no file needed; fx parts also take `"gof2:bra"` / `"gof2:beer"`, the ending's props). Sounds: `.ogg` / `.wav` / `.mp3`. Behaviour never changes (a beam
stays a beam) except through the stats above.

## ships.json

```json
[
    {
        "id": "valkyrie", "name": "Valkyrie", "description": "...", "race": 0,
        "armor": 480, "cargo": 60, "handling": 150, "price": 3800000, "priceDefault": 7600000,
        "slots": { "primary": 4, "secondary": 2, "turret": 0, "equipment": 12 },
        "model": "models/valkyrie.glb", "icon": "icons/valkyrie.png", "modelLength": 1000, "modelYaw": 0, "hangarHeight": 290,
        "mounts": [
            { "slotType": 0, "position_engine": [-73, -23, 442] },
            { "slotType": 0, "position_engine": [73, -23, 442] },
            { "slotType": 1, "position_engine": [0, -48, 115] },
            { "slotType": 3, "position_engine": [0, 0, -478], "turretAngles": [0.55, 0.55, 0.55] }
        ],
        "lounge": { "systemRace": 0, "minCampaign": 32, "minRank": 12, "chance": 12 },
        "dealer": { "chance": 2, "systemRace": -1, "minTechLevel": 5 },
        "available": "option(canon_ships:gof3) and won(supernova)"
    },
    { "override": 10, "armor": 260, "cargo": 60 }
]
```

| Field | |
|---|---|
| `race` | Builder: 0-3, 8 pirate, 9 Void (1 % cheaper in its race's systems). |
| `armor`, `cargo`, `handling` | Hull points, hold (t), handling (originals 30-162; 100 average). |
| `price`, `priceDefault` | Price; Default economy price (0 = same). |
| `slots` | primary, secondary, turret, equipment. One turret slot per turret mount. |
| `model` | **Required** for a new ship: `.glb`/`.gltf` in the mod (Blender: glTF Binary, +Y up; nose toward +Z), or `"gof2:ship_028_terran"` (a game ship's model, no file: its hardpoints unless `mounts` is given, its hangar height unless `hangarHeight`, its icon unless `icon`; the model fields below don't apply). Also on an override (another ship's model). Stations take `"gof2:<model>"` too (as made unless `modelSize`). |
| `modelLength`, `modelYaw` | Nose-to-tail length in game units (Phantom ~1000); yaw degrees if the nose isn't +Z. |
| `icon` | Shop icon PNG 180 x 88, or a game icon `"gof2:ship_028"` (items: `"gof2:item_046"`). Default: the Phantom's (a game model: its ship's). |
| `hangarHeight` | Pivot height above the hangar pad (originals 140-400). |
| `mounts` | Game units from the ship centre (x right, y up, z forward). `slotType` 0 primary, 1 secondary, 2 turret (`upsideDown`; `builtIn`: no game turret model, the turret item fires from the point), 3 engine exhaust (`turretAngles[0]` particle size, `glowColor` [r,g,b], `glowSize` [halfW, halfH, length]), 4 manoeuvring jet (`direction` [x,y,z], the way it blows: fires when the ship strafes, rises, sinks or reverses against it). Give each gun slot its own mount where possible (the originals do). |
| `engineGlowRadius`, `engineGlowColor` | Flame size (default 24) / colour for all exhausts. |
| `materials` | Replace materials: entries `{ "mesh", "submesh", "diffuse", "color", "normal", "normalScale", "metallicSmoothness", "metallic", "smoothness", "emission", "emissionColor", "emissionIntensity", "alphaClip", "opacity", "detailAlbedo", "detailNormal", "detailTiling", "detailNormalScale", "doubleSided", "engine" (true: glows only while the engines run, dark parked in a hangar), "engineIdle" (with engine: share at throttle 0, 0..1; 1 = steady like the game's engine glow, lit through take-off and all flight), "boost" (true: glows only while boosting), "damageEmission" (mask PNG: embers glowing with hull damage), "damageDetail" / "damageNormal" (charring detail colour with mask alpha, and normals, blended in with hull damage), "shimmer" (true: additive layer over the nozzle glow, emission pattern x scrolling flameNoise), "hologram" (true: additive rings whose emission pattern turns and which bob; UV2 x = spin -1..1, y = 1 - bob share), "flame" (true: engine flame cards, UV u 0 at the nozzle to 1 at the tip, stretched backward with the throttle; colour = emissionColor x emissionIntensity), "flameLength" (model units at full throttle, 5), "flameIdle" (share at idle, 0.15), "boostColor", "flameNoise" (tiling grey PNG); a second UV channel x 0..1 across each card softens its edges, its y the kind: 0.5 a main card (the engine colour, edge-on cards fade), 0.25 a boost card (only while boosting, in boostColor); a ship with flames gets no glow discs or exhaust particles; "engine" materials dim at idle, full at full throttle }`. Without it the GLB's own materials are used. |
| `throttleGlow`, `extraGlows` | Hull parts glowing with the throttle: `{ "mask", "mesh", "submesh", "color", "idle", "full", "boost", "off", "offset", "trailWidth", "trailTime", "trailBrightness", "trailCount" }`; "off" = brightness parked in a hangar (default idle; idle = full and off 0 = a steady glow, dark on the pad). |
| `integrated` | Also in an override: built-in equipment `{ "khadorDrive": true/false, "cloak": true (U'tool stats) / a cloak item (number or "mod:id") / false }`, like the Kothar ships' drive (37/38/40) and the Specter's / Scimitar's cloak (44/49); false removes an original's. `"items": [n | "mod:id" | { "item", "amount" }]`: any equipment built in (no slot, can't be removed; a secondary's amount = starting ammo, default 10); a fitted one-per-ship item replaces a built-in one of its kind without a slot until demounted; weapons / turrets add to the slots. |
| `engineFx` | A Galaxy on Fire 3 style engine effect at every exhaust instead of the flame and particles: `{ "glow", "glowSize": [square, streakW, streakH], "glowIntensity", "cone", "coneLength", "coneWidth", "coneIntensity", "nozzle" (the nozzle radius it's drawn for, default coneWidth: exhaust styles are sized against it), "coneProfile": [share, brightness, u, ...] per ring, "coneFade": [from, to] (cards fade edge-on: from behind only the flare), "trail", "trailCount", "trailLength", "trailWidth", "trailWander", "trailTime", "trailIntensity", "spiral", "spiralCount", "spiralLength", "spiralRadius": [nozzle, end], "spiralWidth", "spiralTurns", "spiralSpin", "spiralScroll", "spiralRepeat", "spiralIntensity", "color", "flareColor", "coneColor", "trailColor", "spiralColor" }` (additive PNGs, tinted by the part's colour, else each exhaust's `glowColor`; game units; the spiral is a helix of strands with the picture along them); an exhaust mount may add `"fxScale"` and `"rotation"`. |
| `lounge` | A Space Lounge visitor sells it: systemRace (-1 any), minCampaign (story step), minRank (free play), chance %. |
| `npcTurrets` | Also in an override: turret items (numbers or "mod:id") the NPC ships of this type carry on its turret mounts (n-th item on n-th slotType-2 mount); they aim and fire by themselves, and show in hangars. 45 / 51 swap their built-in turret for them. |
| `dealer` | Ordinary ship dealers stock it: chance % (decimals ok) per dealer list, systemRace, minTechLevel. Not the special yards. |
| `traffic` | `{ "weight": 1, "races": [8] }`: NPC fighters of those races (default its `race`) fly it at the "Mod ships in traffic" option's chance (default 25 %), weighted; `weight` 0 = never. Ships over 150 m never. Hull / guns stay the game's fighter's. A kit ship's NPCs fly random builds (parts, tier, colours, skin); `"randomBuild": false` = its default build. |
| `available` | Condition before dealers / lounges sell it (and NPCs fly it). |
| `flightStyle` | `"free"` / `"original"`: the ship always flies so (default the player's option). |
| `kit`, `kitType`, `build` | A customizable ship instead of `model` (see shipkits.json). |
| `sounds` | The ship's own sounds (new ships and overrides): `{ "engine", "boostStart", "boostLoop", "boostStop", "charger" }`, each a file, a list of files (random) or `{ "file" / "files", "volume": v or [from, to], "pitch": v or [from, to] }`. Loops go from the first value to the second along: engine standstill -> top speed, boostLoop boost start -> full, charger empty -> full; one-shots (boostStart / Stop) take a random pitch in the range. Files anywhere in the mod but not sounds/. The game's volumes: engine ~0.27, booster start 0.8, shots 0.7. Also played by NPCs flying the ship and other players' copies. |
| `flight` | % bonuses `{ "topSpeed", "turnRate", "strafeSpeed", "acceleration" (free only), "boostFactor" (free, 350 = x3.5), "original": {...}, "free": {...} }`. |

`override` changes an original (number) or another mod's ship (key) in both economies (only the price has a Default twin,
`priceDefault`): armor, cargo, price, priceDefault, slots, handling, name,
description, `mounts`, `flightStyle`, `flight`, `sounds` (mod ships also race, hangarHeight), and an original can get a new `model` (+ its fields). `slots` merges
(give only the changed counts); `mounts` replaces the ship's whole list (exhausts too). More weapon slots than gun mounts: the
extra weapons share the mounts; fewer slots: what doesn't fit goes to the hold on the next docking.

## blueprints.json

Built in the hangar's Blueprints tab: invest ingredients (or autocomplete); the product is made at the station of the first
investment. A ship product waits there and replaces the player's ship like a purchase (old ship traded in at its price, or
parked in the Kaamo Club if owned).

```json
[
    {
        "id": "valkyrie",
        "ship": "canon_ships:valkyrie",
        "ingredients": [ { "item": 155, "amount": 400 }, { "item": 127, "amount": 60 }, { "item": 122, "amount": 100 } ],
        "autocomplete": 1500000,
        "available": "option(canon_ships:gof3) and won(supernova)",
        "lounge": { "chance": 3, "price": 400000, "systemRace": -1 },
        "derelict": { "chance": 2, "race": "terran" },
        "drops": [ { "race": "pirate", "chance": 1 }, { "race": "vossk", "chance": 0.5 } ]
    },
    {
        "id": "valkyrie_gold",
        "ship": "canon_ships:valkyrie_gold",
        "requiresShip": "canon_ships:valkyrie",
        "ingredients": [ { "item": 154, "amount": 1000 }, { "item": 165, "amount": 100 }, { "item": 122, "amount": 300 } ],
        "autocomplete": false,
        "unlocked": true,
        "available": "option(canon_ships:gof3) and won(supernova)"
    }
]
```

| Field | |
|---|---|
| `id` | **Required.** |
| `ship` **or** `item` | **Required, exactly one.** The product (key or number). An item that already has a blueprint gets yours. Secondary weapons come 10 per run. |
| `ingredients` | **Required, at least one.** `{ "item", "amount" }` (amount >= 1). Commodities, ores, cores and items (demounted). |
| `requiresShip` | Only built while flying that ship; the result replaces it (a skin). Kaamo upgrades stay. |
| `autocomplete` | `true` (default, 1.25 x product price), a number (that price) or `false` (never). |
| `available` | Condition before any source offers it. |
| `unlocked` | `true`: known as soon as available (next docking). |
| `lounge` | Lounge seller: chance % per bar refresh, price (default half the product price), systemRace. |
| `derelict` | chance % of orbit visits holding a hackable derelict freighter (race terran/vossk/nivelian/midorian/any) with it; "stations": [ numbers or "mod:id" ] only there (chance then defaults to 100), "position": [ x, y, z ] game units in the orbit (/pos). Hacking fires the `hacked` trigger too. |
| `drops` | `{ "race", "chance" }`: chance % (decimals ok) that a ship of that race the player destroys drops it. |

Found blueprints come in a data container the tractor beam pulls in. Sources stop once the player knows the blueprint.
Useful ingredients (see reference.md): 122 Energy Cells, 127 Microchips, 154 Gold, 155 Titanium, 157 Orichalzine, 165 Golden
Core, 114 Explosives, 118 Electronics.

## shipkits.json (customizable ships)

The in-game Mod Builder has a **Ship kits** kind for these (slots, parts, their hardpoints and stats, paint slots); its MCP
tools read and write them as entry kind `kits`. A kit with an empty `parts` list is skipped with a warning.

Ships built from parts the player swaps in the hangar's Customize ship screen (EVERSPACE 2-style modules).
`[ { "id": "medium", "slots": ["body","rear","cockpit","wings"], "scale": 1.0 (Unity m per model unit; one for the whole kit),
"yaw": 0, "materials": [...], "parts": [ { "id": "reaver", "slot": "wings", "name": "Reaver", "model": "parts/w.glb",
"types": ["sentinel"] (ship types allowed; none = all), "tiers": ["parts/w_t1.glb", ...] (extension meshes per tier),
"mounts": [ ...like a ship's mounts... ], "tierMounts": [ [ ... ], ... ] } ] } ]` (a build uses its parts' mounts per slot type,
the ship's own only for a type no part has; turret mounts with `"builtIn": true` take the turret item's gun and stats but no turret model, for parts whose mesh has the turret). A kit's `"hardpoints": { "toggle": true (optional: the Customize screen's Weapon models row hides the weapon / turret models and the mounts' `nodes`, build `weapons=off`, a default `"build"` may say `"weapons": false`; the weapons still fire from the mounts), "turret": "x.glb" (nodes `pivot`, a `...gun` under it, optional `muzzle`; shown on `builtIn` mounts), "weapons": [ { "slot": "primary" | "secondary" | "turret", "model": "y.glb" ("" = none), "items": [numbers or "mod:id"], "categories": [numbers], "sound": { "shot": S, "loop": S, "release": ms } } ] }` shows the mounted weapons and turret as models (first matching rule per slot, one without filters = the default; a rule's optional `sound` replaces the weapon's sound on these ships: `shot` per shot (random file / pitch range), `loop` while held (a beam; `shot` then once at the start, faded out over `release` ms); S as in ships.json `sounds`; a `muzzle` node is where the shots leave; a turret weapon's muzzle lands on the turret's; a turret without `muzzle` carries the weapon on its `gun` node and fires from the weapon's muzzle). A primary / secondary mount may carry `"rotation"` (Euler degrees `[x, y, z]` or quaternion `[x, y, z, w]`, ship frame x right, y up, z forward) turning its weapon model; shots still fly along the nose; `"nodes": ["<model node name>"]` on such a mount shows those parts of the ship's model (its pylon) only while the mount carries a weapon. A kit ship's hangar height is measured from its build (lowest point incl. weapon models + 1.5 m); `hangarHeight` is ignored for it. Parts may also carry `"stats": { "armor", "cargo", "handling" (% of the ship's),
"equipment" (slots), "topSpeed", "turnRate", "acceleration", "strafeSpeed" (%) }` and `"slots": { "primary", "secondary",
"turret" }`: a build sums them (slots per type: its parts' sum, else the ship's); the tier is cosmetic.
All parts share one origin (export in place). A ship in ships.json uses `"kit": "medium"` instead of `"model"`, with
`"kitType": "sentinel"` (required) and `"build": { "wings": "reaver", "body": "body_a", "tier": 1 }` (the default: dealers'
and NPCs'; missing slots take the first allowed part). The player's build is
saved per ship type and shown in the hangar and in flight; changing it is free. Colours (optional, in the kit):
`"palettes": { "main": [ { "id", "name", "color": "#RRGGBB", "metallic", "smoothness" } ] }`, `"colorSlots": [ { "id", "name",
"palette", "default", "targets": [ { "material": "<name contains>", "property": "base"|"emission"|"boost"|"engine", "intensity" } ] } ]` (boost = a flame's boost colour; the manoeuvring jets' material is "lateral thruster"); a strength slot `{ "id", "name", "steps": [0..1 numbers], "default": "0.5", "scales": "<colour slot id>" }` (no palette) scales that slot's emission,
`"presets": [ { "id", "name", "colors": { "<slot>": "<palette id or #RRGGBB/metallic/smoothness>" } } ]`; a ship's build may
carry `"colors": { ... }`. Give separately paintable areas their own materials in the model.
Skins (optional, in the kit): texture paint jobs, a Skin row in Customize ("Standard" = the model's own):
`"skins": [ { "id" (not "none"), "name", "types": [ship types; none = all], "engineColor": [r, g, b] (optional, every
exhaust's glow), "materials": [ { "material": "<model material name contains>", "diffuse", "emission", "metallicSmoothness"
(URP layout: R metallic, A smoothness), "normal" (PNG / JPG paths), "color", "emissionColor" ([r, g, b]), "emissionIntensity" } ] } ]`;
fields left out keep the model's own, no stats change; a build may carry `"skin": "<id>"`.
Any other ship (an original's "override", or a new ship without "kit") may carry `"skins"` (as above, no "types"; materials by the ship's own material names, e.g. "ship_010_terran_diffuse" for the Phantom's hull) and / or `"weaponColors"` (palette list or true): it then gets the Customize screen with Skin and Weapon colour rows; saved per ship; engineColor works only on kit ships.
Every kit ship also gets a Weapon colour row (energy weapons and energy-bolt turrets' shots, flashes, impacts; "Original" = their
own): the game's colour list, or the kit's palette `"weapons"`; a build may carry `"weaponColor": "<palette id or #RRGGBB>"`.

## exhausts.json (exhaust styles for every ship)

`[ { "id", "name", "engineFx": { ...as ships.json engineFx } | "model": "exhausts/x.glb" + "materials": [ { "material": "...",
"flame": true, ... } ] + "yaw", "nozzle" (radius the effect is made for: game units for engineFx, default its engineFx nozzle, else coneWidth; model
units for a model, default 1), "palette": [ { "id", "name", "color": "#RRGGBB" } ], "mainName", "secondName", "group" +
"groupName" + "scale" (flame models with one group are one choice; each nozzle wears the one whose nozzle x scale, Unity
metres per model unit, is nearest its size) } ]`. A flame model:
origin at the nozzle exit, flame along -Z. Any ship (the game's too) then has a Customize screen with Exhaust ("Ship's own" or a
style) and its two colours (engineFx: flare+cone / trails+helix; model: flame / boost cards); each exhaust scaled by its nozzle
against "nozzle" by its nozzle's mean radius, its cross-section bent to the nozzle's outline (a mount's `"outline": [x0, y0, ...]`
game units around it, used only when at least half the mount's glowSize and centred within 1.5 x it; else meshes with
"nozzle" in a material name; else glowSize as an oval; game ships measured; a ship with its own engineFx giving a nozzle: its nozzles
taken as large as that nozzle x fxScale) and the player's Exhaust size (0.5-2); the model's own exhaust hidden; a flame
model's boost cards only on its own mod's ships (elsewhere the boost recolours the main flame); a flame model style isn't
offered on its own mod's ships (cut from their own exhaust parts). Builds: `"exhaust": "mod_id:id"`, `"exhaustColor"`, `"exhaustColor2"`,
`"exhaustSize"`.

## Flight styles

Two ways to fly: the **original** (throttle, always forward) and **free flight** (the EVERSPACE 2-style option: held thrust /
strafe / up-down, held boost, dampeners). Mods pick per content: item stats 105-116 (above; bonuses add up over the ship and
every mounted item, never below -90 %; an item's freeBoostFactor beats the ship's), ships' `flightStyle` / `flight`, and
`flightStyle` in campaign.json and gameoptions.json. Who decides, first match: an event (Set Flight Model, `/flightstyle`),
the ship flown, the mod campaign, a new-game option that is on, the player's option. Touch, tilt and VR always fly the
original. The original has no acceleration (the throttle is at once); its top speed bonus scales the boost too.

## gameoptions.json

Cards in Start new game's **Game options** panel, each a switch saved with the game; content asks for it with `option(...)`.

```json
[
    { "id": "gof3", "name": "GoF3 Ships", "description": "Galaxy on Fire 3's ships as blueprints after the Supernova.",
      "image": "cards/gof3.png", "imageHover": "cards/gof3_hover.png", "showTitle": false, "default": true }
]
```

`id` required. `image` is card art like the game's campaign cards: **290 x 448** or a multiple (580 x 896), portrait; cropped
to the card. `showTitle` false when the art carries its own title (else a name plate is drawn at the foot). `default` is the
first-time position and what multiplayer uses. A game started before the mod was installed has the option off.
`"flightStyle": "free" | "original"`: while the option is on, the game flies so.

## Conditions

Used by `available` (items, ships, blueprints) and quests' "Starts when". Expression language:

| Word | Value |
|---|---|
| `option(mod_id:option_id)` | 1 if that game option is on in this game (`option(option_id)` works if unique). |
| `won(main)`, `won(valkyrie)`, `won(supernova)` | 1 if that campaign's story is finished. |
| `systemsvisited` | Star systems docked in; `>= 2` = has left Mido (the start system). |
| `campaign` | Story step (0-162; -1 in free play / mod campaigns). |
| `rank`, `credits`, `kills`, `station`, `system`, `ship` | Player state. |
| `visited(station)`, `has(item)`, `cargo(item)`, `quest(name)` | Docked there (1/0); count owned incl. mounted / in hold; quest 0 none, 1 running, 2 done. |

Operators: `and`, `or`, `not`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `+ - * /`, parentheses, `min(a,b)`, `max(a,b)`,
`floor(x)`, `random(a,b)`. Example: `"option(canon_ships:gof3d) and systemsvisited >= 2"`. An unreadable condition counts as
false and warns.

## campaign.json

A whole new game under Start new game, shown as a card next to the three campaigns.

```json
{
    "name": "Kepler Frontier", "description": "...", "image": "card.png", "imageHover": "card_hover.png", "showTitle": true,
    "startStation": "frontier_systems:kepler_prime", "startShip": 10, "credits": 25000,
    "equipment": [0, { "item": 37, "amount": 12 }], "cargo": [{ "item": 122, "amount": 5 }], "standing": [40, 10],
    "quest": "kepler_story", "galaxy": "mod", "items": "all", "ships": "all",
    "trafficShips": { "terran": ["starwars_ships:jedi_starfighter"], "pirate": [2, 23] }
}
```

`startStation` and `startShip` required. `image` 290 x 448 card art. `standing` = [Terran/Vossk, Nivelian/Midorian] -100..100
(default [30, 0]). `galaxy`/`items`/`ships` `"mod"` limit the game to mods' content. `quest` = an event graph started with
the game. `"flightStyle": "free" | "original"`: the campaign's games always fly so.

## systems.json, stations.json

```json
[ { "id": "kepler", "name": "Kepler", "race": 0, "security": 2, "visible": true, "position": [8, 27, 83], "sky": 4,
    "gates": [4, 17], "gateStation": "frontier_systems:rho_freeport", "sunTexture": "textures/sun.png", "sunColor": [1, 0.8, 0.55],
    "skybox": { "nebula": "sky/nebula.png", "stars": 1, "nebulaBrightness": 0.8 }, "spaceMusic": "kepler_space" } ]
```
```json
[ { "id": "kepler_prime", "system": "frontier_systems:kepler", "name": "Kepler Prime", "techLevel": 7, "planet": 12,
    "looksLike": 40, "model": "models/station.glb", "modelSize": 50000, "collision": "box", "interior": "nivelian",
    "planetTexture": "textures/planet.png", "music": "kepler_bar" } ]
```

`looksLike` borrows only an original station's model and collision (reference.md's Model column): its special rules (the
Kaamo Club's storage and siege, a pirate base) stay with the original number. Stations have no hidden flag: every station of
a visible system shows on the map. A system's `"available"` condition (an expression, e.g. `"quest(smuggler_run) == 1"`)
shows it only while it holds and hides it otherwise (a player inside is taken to the nearest visible system); events and
quests can also `system <system> show | hide | reset` (step `system`, node Show / Hide System), saved until reset, over
`available`.

Systems: race 0-3, security 0 (lawless) - 3 (secure), map position (originals x 15-94, y 2-96, z 10-90), sky 0-18, gates
both ways, up to 7 stations each. Skybox images: 2:1 panorama or 6:1 cube strip (right, left, up, down, front, back), light
on black, 8192 x 4096 sharp. Stations: techLevel 0-10, planet 0-22 or 24-26, looksLike = an original station whose model
it borrows, modelSize = largest extent in game units (default 40000), collision box/sphere/none or `volumes` (what NPC fighters steer around; the player and shots use the model's real meshes, `none` turns all off), interior =
race of hangar and bar, or own rooms `hangar` / `bar` from interiors.json (GLB rooms with marker empties: hangar `pad`,
`camera` required, `camera_target`, `parked_N`, `gate`, `gate_out`, `light`; bar `camera`, `camera_target`, `visitor_N`
required). `override` changes originals (name, techLevel, looks, music...).

## characters.json, voices, music, sounds, textures

- `characters.json`: `[{ "id", "name", "portrait": "portraits/x.png" (4:5, 320 x 400), "race", "gender", "mirrored",
  "background", "frame", "replaces" }]`; `frame` = the game's faint scanline overlay, `background` = a dark backdrop
  that only shows through transparent parts of the PNG (both on by default). Named in dialogs as `id`, `mod_id:id` or the name; `id as Other Name`.
  `replaces` (a story speaker's name, e.g. `"Keith"`, or number) takes that speaker's portrait everywhere, and its name
  in the headers when the character has a `name`; other lines' mentions of the old name and the voices stay.
- Voice-over: `[voice <clip>]` in a Dialog page / Radio line; files `voices/<clip>.ogg` (or `voices/en/`, `voices/de/`).
- `music/<name>.ogg`: a game track's name (`Space_Terraner`, `Station_Vossk`, `Space_Battle_Full`, `Space_NoCombat_Void`
  (main menu)...) replaces it; any other name is a new track for systems/stations (`spaceMusic`, `stationMusic`, `music`) and
  quests.
- `sounds/<name>.ogg`: replaces the game sound with that file name (from the game's Assets/Audio folders).
- `textures/<name>.png`: replaces that game texture everywhere (`ship_028_terran_diffuse`; keep the original UV layout,
  2048 x 2048). `textures/<model>/<name>.png` (e.g. `textures/ship_028_terran/glow_terran.png`) replaces a shared texture
  (`glow_<race>` lights, `ship_engine_glow`) on that one model only, before the every-ship one. A new ship on a game model
  (`"model": "gof2:<model>"`) takes `textures/ships/<ship id>/<name>.png` on that ship alone (the game's ship of that model
  keeps its look). Also the sky's planets, suns and rings (`planet_000_small` ... `planet_019_big`, `planet_void_*`,
  `sn_planet_024_*` ... `sn_planet_026_*`, `sun_000` ..., `sn_sun_011`, `sn_planet_ring`): one whole disc image each, keep the
  transparent edge. `backdrop.json` `{ "planets": "star", "planetGlow": 2, "planetFlare": 0.35 }` draws the retextured planets
  as stars (facing the camera, no ring, a core glow, a weaker copy of the sun's flare).
- `textures/<name>.png` also replaces the combat HUD's images by their `Assets/Resources/GoF2Hud` names: `ship_dot_<faction>`,
  `ship_ring_<faction>`, `ship_ring_open_<faction>`, `ship_bracket_<faction>`, `ship_bar_` / `ship_fill_<faction>` (faction
  `enemy` / `friend` / `neutral`), `crate_dot`, `bracket`, `crate_off`, `lock_00` ... `lock_23`; drawn at their own size.

## Quests made as step lists (.gof2quest)

Prefer these over event graphs: plain JSON, no Unity Editor, and the Mod Builder opens them as a node graph.
`events/<name>.gof2quest` (the file name is its name: letters, digits, `_`): `{ "format": "gof2quest", "title", "kind":
"quest" | "mission" | "event", "startsWhen", "reward", "offer", "client", "stations", "players", "variables": [{ name, value,
setting }], "steps": [...] }`. Every step type, field and default is in `quests.md` (the same as `quest.schema.json`), made
from the game's own table: read it before writing a quest. In short: story steps (`dialog` pages, `radio`, `objective`,
`title`), flow and blocks (`wait`, `waituntil`, `if` then / else, `while`, `repeat`, `parallel`, `every`, `on` <trigger> (also
`hacked`: the player hacked a wreck, its name as %victim%), `ask`
/ `vote` answers, `random` branches, `set`, `checkpoint`, `startquest`, `startevent`, `complete`, `fail`), places (`goto`,
`waypoint`, `target`, `questorbit`, `restrict`, `respawn`, `teleport`), ships (`fight`, `spawn`, `wreck` (a hackable freighter
wreck: race, name, at), `shipgoto`, `shiproute`,
`shipfollow`, `shipattack`, `shipaction`, `provoke`), the player (`reward`, `give`, `cargo`, `credits`, `ship`, `heal`...),
cutscenes (`cutscene`, `camera`, `fade`, `letterbox`...), `music`, `sound`, `timer`, score steps and `command` (any event
command line). Steps acting on players take `who` (default `@a`; `@trigger` inside an `on` block). Speakers as in Dialog
(`Keith`, `my_mod:vega`, `vossk K'ekki`, `player`); stations / ships / items by number or key; `{variable}` in a text shows
it. Without `complete` / `fail` a quest completes after its last step. Put a `checkpoint` after each step a save shouldn't
repeat (not inside a block).

## The Mod Builder's MCP server

When the user has the game's Mod Builder open with its **AI tools** MCP server on (Windows), you may have its tools:
`builder_status`, `search_content` (find the right numbers / keys: use its `value`), `list_entries`, `get_entries`,
`put_entry` (the mod files' own JSON; quests with `name`), `patch_entry` (for changes to an existing entry: ops `set` /
`insert` / `remove` / `move` / `test` by paths like "steps/2/pages/0/text" or "steps/3/then/0", all or none; it keeps the rest
and a quest's graph layout, so prefer it to rewriting the whole entry; `test` a step's type first so a stale index can't
edit the wrong step; `get_entries` with `path` reads one part), `delete_entry`, `list_files`, `read_file`, `write_file`,
`compile_quest`, `validate`, `get_quest_schema`, `play_test`, `end_play_test`, `get_log`, `screenshot`, `undo`, `redo`,
`show_kind`; and to see and drive the builder: `view_model` (a model from named angles in one image: "gof2:<assembly>",
"ship:<id | override:N | number>" with its hardpoints as tagged dots P / S / T / E, hollow where the hull hides them,
"station:...", or "models/x.glb"), `view_texture` / `list_textures`, `select_entry` (an entry on screen; ships' tab, a quest's
graph), `view` (the builder's camera from front / back / left / right / top / bottom / threequarter / rear, zoom, with a
screenshot, `clean` hides the panels), `hardpoint` (op list / add / snap / select / mirror / delete; add puts a point where a
line from a side meets the hull: use it instead of guessing coordinates, then check with view_model), `engines`, `test_fly` /
`end_test_fly`, `weapon_range`, `switch_hangar`, `list_projects`, `create_project`, `open_project`, `export_mod`, `loadout`
(the test loadout and cheats), `room_marker` (a room's markers placed by a ray from inside it: the pad `toward` "down"),
`view` with `map` (the star map on a system); and during a play test or test flight `press` (actions such as action,
autopilotMenu, fastForward, boost, firePrimary, or keys such as enter, escape, 1-8), `steer`, `ui_buttons` / `ui_click` (a
button by its text): play a quest through to check it (ui_click a conversation's Next / Close and Launch; the autopilot menu
plus a number key and a held fastForward dock you), watching with the screenshots they answer with. Then work
in the live project instead of giving the user files: look things up, put entries, `validate` (fix every error), look at
what you made (`view_model`, `view_texture`, `select_entry` + `view`), `play_test` a quest and check it with `screenshot` /
`builder_status` / `get_log`, `end_play_test`. Every
change is an undo step. What waits for the player depends on the permission mode they chose (builder_status's
`permission`): ReadOnly (changes are refused: suggest instead), AskEveryChange (each change waits for their Yes, up to 5
minutes: make few, complete calls), AskRemovals (only delete_entry asks), AllowEverything (nothing asks: remove only
what they asked you to). "The player said no" means stop and ask them what they want.

## Quests and bar missions (event graphs)

Made as `.gof2event` node graphs in the Unity Editor (the remake's project: Assets > Create > GoF2 > Event Graph), saved
to the mod's `events/` folder; the file name is the graph's name. The Start node's Kind: **Quest** (starts by itself when
"Starts when" holds), **Bar Mission** (offered by a lounge visitor; Offered at, Client, Reward) or **Event** (multiplayer).
Nodes include Dialog, Radio, Spawn (named ships), Wait / Wait Until, On (docked, entered orbit, destroyed, cleared, near a
point), Set Waypoint, Set Objective, Checkpoint, Quest Orbit, Give Item, Reward, Mission Complete / Failed, Start Quest, ship
orders (Fly To, Fly Route, Follow, Attack, Ship Action), cutscene nodes (Start Cutscene, Camera Shot, Fade, End Cutscene),
Play Music, Set Flight Model (free flight / original / the player's choice, undone at the graph's end; expression
`freeflight` = 1 in free flight). A Checkpoint (and a quest's start) is kept by the game's next save: the auto-save at the next docking or a manual
save; a quest started while docked isn't in that docking's auto-save. Quest Orbit takes effect the next time the orbit is built
(launching from its station counts). You can't write the file; describe the graph as an ordered list of nodes with their settings and wires for the
user to build, using the exact node, option and port names (and option choices) from `events.md`.

## Balancing (the originals, Android economy)

| Price band (credits) | Armor | Cargo t | Handling | Primary slots | Equipment slots | Examples |
|---|---|---|---|---|---|---|
| up to ~80 000 (starter) | 95-190 | 25-110 | 102-160 | 1-3 | 3-7 | Betty, Wasp, Cronus, Taipan |
| ~85 000-470 000 (low) | 115-1200 (freighter types) | 28-480 | 30-162 | 0-4 | 5-9 | Hera, Teneta, Rhino |
| ~630 000-3 100 000 (mid/high) | 145-335 | 40-320 | 90-150 | 2-4 | 9-13 | Phantom (200/52/150, 4 slots 2/1/0/9), Ward |
| ~3 300 000-12 000 000 (top) | 235-1000 | 30-180 | 50-155 | 1-5 | 5-16 | Scimitar, Ghost, Nemesis, Specter (800 armor, 12 M) |

Guidance: stay inside a band's ranges for "as good as" that band; beat its best values only for ships meant to be special
(and price them above 12 M or gate them behind blueprints / late conditions). Weapons: compare damage x (1000 / reloadMs) to
the originals of the same category and tech level in reference.md. Prices roughly follow power; tech level gates where
it's sold. Tech level 11+ (a "Tech Level 11" add-on): beat the best original of the category (reference.md) and price it
above it; such gear sells only at the 16 tech level 10 stations, so give it a low occurrence (5-15).

## Example: a small canon-ships mod

```
canon_ships/
  mod.json
  gameoptions.json   two cards: gof3d (default on), gof3 (default on)
  ships.json         viper_3d (weak, dealer chance 1 after leaving Mido), valkyrie + valkyrie_gold (blueprint only)
  blueprints.json    valkyrie (lounge / derelict / drops, autocomplete 1.5 M), valkyrie_gold (skin, no autocomplete)
  models/*.glb, icons/*.png (180 x 88), cards/gof3d.png + cards/gof3.png (580 x 896)
```

ships.json entry for the weak GoF3D ship (no better than the worst originals):

```json
{ "id": "viper_3d", "name": "Viper", "race": 0, "armor": 110, "cargo": 30, "handling": 110, "price": 45000,
  "slots": { "primary": 1, "secondary": 1, "turret": 0, "equipment": 4 },
  "model": "models/viper_3d.glb", "icon": "icons/viper_3d.png", "modelLength": 900, "hangarHeight": 250,
  "mounts": [ { "slotType": 0, "position_engine": [0, -20, 400] }, { "slotType": 1, "position_engine": [0, -40, 100] },
              { "slotType": 3, "position_engine": [0, 0, -420], "turretAngles": [0.5, 0.5, 0.5] } ],
  "dealer": { "chance": 1, "systemRace": -1, "minTechLevel": 0 },
  "available": "option(canon_ships:gof3d) and systemsvisited >= 2" }
```

## Checklist

- mod.json has `id`; every other file is the right shape (items/ships/blueprints/gameoptions/characters/systems/stations/
  interiors are lists `[ ... ]`; campaign.json and mod.json are objects `{ ... }`).
- Every entry has `id` (new) or `override` (change), never both; new items have `base`; new ships have `model`; blueprints
  have exactly one of `ship` / `item` and at least one ingredient.
- Ids unique per file, lowercase `a-z0-9_-`. Cross-file references use `mod_id:id`; other mods are in `dependencies`.
- Original numbers and names come from reference.md / voices.md (items 0-232, ships 0-63, systems 0-33, stations 0-134,
  speakers, story steps, texture / model / music / sound / voice names).
- Stat names from the list above; numbers are integers (except chances, colours, sizes).
- Conditions use only the words above; option keys match gameoptions.json ids (`mod_id:option_id`).
- Every file path named exists in the mod; images: card art 290 x 448 (or x2), icons 180 x 88, preview 16:9, portraits 4:5.
- Prices and stats sit in the intended balance band.
