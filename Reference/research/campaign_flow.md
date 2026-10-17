# Campaign (story) flow

How the original drives its story: the single campaign counter in `Status`, the mission object that belongs to each
counter value, who advances it (docking, space levels, cinematics, radio calls), the dialogue tables, the bar,
the restrictions a story mission imposes, and the ending. Covers the main game (indices 0-45), the **Valkyrie**
add-on (46-84) and the **Supernova** add-on (85-162).

All addresses are Ghidra addresses (image base 0x10000) in `libgof2hdaa.so`. Text IDs index `Assets/Localization/text_en.json`.
Station / system / item / ship numbers are the indices of `stations.json`, `systems.json`, `items.json`, `ships.json`.
Tables that live in the binary are decoded by **`Reference/tools/campaign/campaign_flow_tables.py`**:

```
python Reference/tools/campaign/campaign_flow_tables.py            # markdown table, one row per index (generated data)
python Reference/tools/campaign/campaign_flow_tables.py dialog 23  # every page of the briefing + success dialogue of index 23
python Reference/tools/campaign/campaign_flow_tables.py json out.json
```

Per-level details (what spawns in each campaign orbit, `Level::createCampaignMission` 0x0c3370, `LevelScript::process`
0x160d50) are out of scope here; this document only gives each step's goal in one line.

Confidence markers: **(verified)** = read directly from the decompiled code / binary tables; **(inferred)** = deduced
from dialogue text, objective text or surrounding code; **(uncertain)** = plausible but not traced.

---

## 1. Campaign state in `Status`

### 1.1 The counter

| Field | Meaning | Functions |
|---|---|---|
| `Status+0x1e8` (int) | **campaign index** ("current campaign mission"). One value per story *step*, not per chapter. 0..162. | `getCurrentCampaignMission` 0x0b6840, `setCurrentCampaignMission` 0x0b6c90 (only `GameRecord::load` 0x180dc4, `MenuTouchWindow::startSupernovaChallenge` 0x154238 and the broker special case in `ModStation::OnUpdate` 0x0ed2a8 call the setter) |
| `Status+0x194` | `Array<Mission*>` of length 2 (`getMaxMissions` 0x0b6c80 = 2): **[0] campaign mission, [1] freelance mission** (`Mission::empty` when none) | `getMissions` 0x0b6bf0, `getCampaignMission` 0x0b6846, `setCampaignMission` 0x0b6c0c (sets `Mission+0x50` = campaign flag, deletes the old one), `getFreelanceMission` 0x0b6bf6 / `setFreelanceMission` 0x0b6c00, `removeMission` 0x0b9878 |
| `Status+0x190` (400) | the **active mission of the current level** (the one the orbit is built around, or `Mission::empty`) | `getMission` 0x0b6850, `setMission` 0x0b6826; chosen by `Status::departStation` 0x0b63e0 (§3.2) |
| `Status+0x100/+0x104` | playing time (ms, 64-bit) when the current step started (written by every `nextCampaignMission` case) | used for the 12 s delay of story radio calls (`MGame::OnUpdate` 0x1ac778) |
| `Status+0x178` (bool) | "story radio call pending": set when the index becomes **93, 111 or 143** (`DAT_00252b00` = {0x5d, 0x6f, 0x8f}); `MGame::OnUpdate` plays radio message group 0x1b (0, 1 or 2) in space ≥ 12 000 ms after the step started, then clears it | `nextCampaignMission` loop head |
| `Status+0x1c4` | freelance missions completed (`incMissionCount` 0x0b6c84) | campaign type 0x96 |
| `Status+0x1c0` | kills (`incKills`) | – |
| `Status+0x1d0` | stations visited (`visitStation` 0x0b9918) | index 90 decrements it |
| `Status+0x1ac` | credits (`changeCredits` 0x0b8e84) | rewards |
| `Status+0x38` | `Array<bool>` system visibility (index = system) | several steps reveal systems |
| `Status+0x78` | the **Void space pseudo-station**; `inAlienOrbit` 0x0b6810 = current station == it | Void-space steps |
| `Status+0x7c / +0x80` | Void-invasion **system / station** (−1 none, −10 = disabled for good). Set by steps 24 (9/48), 28 (18/91); between steps 32 and 44 `departStation` moves it to a random visible system every ~10 departures (never systems 10/15, never the campaign target) | `departStation`, step 40 (type 0xa1) |
| `Status+0x84` | station to return to when leaving Void space (0x1a0a = 6666 after the DLC quick-starts) | `MGame::OnUpdate` wormhole code |
| `Status+0x8c` | the player's **own ship** parked while a loaner ship is flown (steps 48-54, 56-57) | cases 0x2f/0x37 save, 0x36/0x39 restore |
| `Status+0x90` | `Array<int>` target stations of a type-0xa3 step (entries become negative when done) | step 59 |
| `Status+0x174` | counter for types 0xa8 / 0xb8 (hacking, passengers) | steps 91, 94, 139 |
| `Status+0x114` | Kaamo (station 108) purchase state (0/1/2/3; 3 when `options[0x36]`); not campaign | `ModStation::OnInitialize` |

### 1.2 Milestone predicates (verified)

- `Status::gameWon` 0x0b6832: **index > 44** (i.e. ≥ 45).
- `Status::dlc1Won` 0x0b8eda: **index > 83** (≥ 84) = Valkyrie finished.
- There is no "dlc2Won"; index 162 is the end of Supernova (objective text 748, last `nextCampaignMission` case 0xa1).
- `GameRecord::load` sets record flags `+0x102 = index > 45` and `+0x119 = index > 85` (save-slot preview badges, inferred).

### 1.3 `Status::nextCampaignMission` 0x0b6c98 (verified)

```
old = Status+0x1e8
loop:
    new = old + 1
    if new in {93, 111, 143}: Status+0x178 = 1
    switch old:
        case 52, 128: old = new; goto loop        # indices 53 and 129 are never current
        case k (known): Status+0x1e8 = new; Status+0x100 = playingTime;
                        side effects; setCampaignMission(new Mission(type, reward, target)); [statusValue / goods / hidden]
        case 45, 106, >=162 (no case): Status+0x1e8 = new; playingTime; campaign mission unchanged
```

So **`case k` creates the objective of index k+1**. The objective of index N is completed → the success dialogue of
index N plays → `nextCampaignMission` → index N+1 with its new objective. The complete transition table is in §4 and
in `T` of the tool script. Mission objects created with the 3-int constructor `Mission::Mission(int type, int reward,
int targetStation)` 0x187cc0 (`Mission+0x50` = 1 → `isCampaignMission`, name "" (`getName` returns text 354+type only
for freelance missions)). Hidden steps call `Mission::setVisible(false)` (not shown on the map / radar / missions list).

### 1.4 New game and DLC starts (verified)

- **`Status::resetGame` 0x0ba78c** (new game, `MenuTouchWindow::startGOF2` 0x1540f0 then module 2 = space, music 143 IntroAtmo):
  index 0, campaign mission `Mission(4, 0, 78)` which is also the active level mission; credits 0; station 78 Var Hastra;
  ship **10 Phantom** with items 2, 2 (Nirai Charged Pulse ×2), 54, 59, 82, 73 and 36 ×6 (Edo). Standing new, Void
  invasion off (−1), all systems at their `initiallyVisible`; if the Valkyrie add-on is owned (`options[0x35]`), system 25 visible.
  Index 0 is the **prologue level** ("3598 A.D. – Dareius Asteroid Belt", texts 1668-1676, briefing = steering/lock tutorial 1677/1680).
- The Phantom is only the prologue ship: at **index 1** `ModStation::OnInitialize` 0x0e8080 replaces it with ship **0 Betty**
  (race 8) carrying **90 Gunant's Drill** and **81 Telta Quickscan**, both unsaleable. The remake's "new game = Phantom
  at Var Hastra" is correct for `resetGame` but the story itself continues in Betty.
- Skipping the prologue cinematic (`MGame::OnTouchEnd` 0x1a98d8, index 0) = `nextCampaignMission`, `setKills(3)`, module 2
  (the index-1 space cinematic); skipping at index 1 goes straight to the station (module 5).
- **`MenuTouchWindow::startValkyrie` 0x153ba4**: `resetGame`, 45× `nextCampaignMission` (→ 45), active mission empty,
  ship **5 Inflict** (Terran) with 2, 5 (items +8/+0x14), 36 ×10, 81, 51, 86 ×10, 85 Khador Drive; station **91 Dima**;
  systems 6 and 25 visible; kills 197; many hints pre-set; then station module.
- **`MenuTouchWindow::startSupernova` 0x153e14**: `resetGame`, 84× `nextCampaignMission` (→ 84), ship **30 Berger CrossXT**
  (Midorian) fully equipped (items 176, 20, 36 ×20, 44 ×20, 81, 51, 68, 86, 85, 56) + 8 Energy Cells; station **70 Dis**;
  kills 386; then station module.
- `MenuTouchWindow::startSupernovaChallenge` 0x154238 sets index **152** directly (ship 46 Blue Fyre) – an arcade mode, not the story.
- Autosave on docking (`ModStation::autosave` 0x0e9eb4 from `OnInitialize`), except index 77 at station 101.

### 1.5 Save-load repairs (`GameRecord::load` 0x180dc4, verified)

Saves are made docked, so the loader moves you back to a safe start when a save was taken inside an in-space chain:
index 25 → 24 (`Mission(4,0,48)`, invasion 9/48), 29 → 28 (`Mission(4,0,91)`, invasion 18/91), 41 → 39
(`Mission(0xb,0,30)`), index 35 with a target ≠ 29 → `Mission(0xb,0,29)`. Supernova-era fixes for old saves force
86..103 back to 86 (`Mission(0xb,0,100)`). After load: `dlc1Won` → the jump drive becomes saleable; `gameWon` → cargo
131 Alien Remains saleable. These tell the remake which steps are **checkpoints**.

---

## 2. Mission object and objective types

### 2.1 `Mission` fields (game/Mission.c, verified)

| Offset | Field | Accessor |
|---|---|---|
| +0x0 / +0x1 | failed / won flags | `setFailed`/`hasFailed` 0x187fd4, `setWon`/`hasWon` 0x187fdc |
| +0x4 | agent (bar character who gave it; campaign: 0) | `getAgent` 0x1881c0 |
| +0x8 | **type** (−1 = empty) | `getType` 0x187ffa, `isEmpty` 0x187fba |
| +0xc | client name | `getClientName` 0x188058 |
| +0x14 | target name (wanted/escort) | `getTargetName` 0x1881c8 |
| +0x1c | client image | `getClientImage` 0x188066 |
| +0x20 | client race (standing reward) | `getClientRace` 0x18806a |
| +0x24 | **reward** (credits) | `getReward` 0x18806e (`getCosts` also returns +0x24 – bug in the original) |
| +0x2c | bonus | `getBonus` 0x18807e |
| +0x30 / +0x34 | **target station** index / name | `getTargetStation` 0x1880a0, `setTargetStation` 0x1880c8 |
| +0x3c | target system name | `getTargetSystemName` 0x1880b2 |
| +0x44 | difficulty | `getDifficulty` 0x188138 |
| +0x48 | instant-action flag | `isInstantActionMission` 0x18808c |
| +0x4c | distance (jumps) | `calcDistance` 0x188140 |
| +0x50 | **campaign flag** | `isCampaignMission` 0x188092 |
| +0x54 / +0x58 | **production goods** index / amount (goods to deliver, passengers, target amount) | `setProductionGoods` 0x187fe4 |
| +0x5c | **statusValue** (counter / parameter) | `setStatusValue` 0x187ff2 |
| +0x60 | visible | `isVisible` 0x187fce |

`Mission::isOutsideMission` 0x188054 always returns 1 (unused distinction).

### 2.2 Objective types and their completion rule

Rules from **`Status::missionCompleted(bool docked, bool inLoungeIntroDone, long long levelTimeMs)` 0x0b924c** (verified).
It scans both slots ([0] campaign first, then freelance) and returns the first mission whose rule holds; if a mission is
already `hasWon`, it returns null (the dialogue for it is already up). Called with `docked = true` by
`ModStation::OnUpdate` 0x0ed2a8 every frame while no window is open (lounge flag = `SpaceLounge::introFinished`), and with
`docked = false` by `MGame::successCheck` 0x1b0620 in space (every frame after the first 5000 ms of the level).

| Type | Rule (done when …) | Used by indices |
|---|---|---|
| 0x04 | never here: **scripted campaign level** in the target orbit; the level's success objective / `LevelScript` decides | 0, 7, 14, 16, 21, 24, 26, 28, 29, 38, 41, 56, 63, 64, 67, 78, 81, 87, 89, 97, 100, 105, 106, 114, 120, 123, 125, 131, 137, 142, 145, 147, 154, 157, 158 |
| 0x01, 0x06, 0x0a, 0x0c | never here: freelance level types reused (Defense 355, Wanted 360, Intercept 364, Challenge 366 = kill race vs a rival) | 80; 69, 70; 73; 36 |
| 0x0b (and 0) | **docked at the target station** | 1, 3, 5, 8-12, 15, 17, 22, 23, 27, 31, 32, 34, 35, 39, 43, 44, 47, 48, 54, 55, 57, 60, 62, 65, 66, 71, 74-77, 82, 83, 86, 88, 90, 93, 96, 98, 101, 103, 108, 117, 122, 124, 127, 132, 134, 136, 138, 140, 141, 146, 153, 156, 159 |
| 0x08 | docked at target **with goods(index, amount) in cargo** (index 143: also any pending product 210 at the target) | 33, 68, 118, 121, 143 |
| 0x96 | freelance missions completed ≥ statusValue (step 13: current count + 1) | 13 |
| 0x9a | current cargo load ≥ statusValue t | 2 (10 t), 4 (25 t) |
| 0x9c | in space, in the target orbit, level time ≥ 10 000 ms | 18, 19, 25, 30, 49, 50, 51 |
| 0x9e | a primary weapon (item type 0) and an item of sort 10 mounted (hint 1746: weapon + shield) | 6 |
| 0xa0 | docked anywhere, or in space ≥ 10 000 ms outside the target orbit (target −1 → anywhere): a **delayed story call** | 37, 42, 52 |
| 0xa1 | never here; `departStation` makes it the level mission in the Void-invasion orbit (`Status+0x80`) | 40 |
| 0xa3 | every entry of `Status+0x90` negative | 59 |
| 0xa4 | in space > 10 000 ms (a radio call shortly after launch) | 61, 72, 85, 155 |
| 0xa5 | in space in the target orbit (target −1: level-driven only) | 79, 152 |
| 0xa6 | docked at target with goods in cargo, or the goods item mounted | 58 (179 Liberator ×10), 104 (206 Gamma Shield II) |
| 0xa8 | `Status+0x174` ≥ statusValue (counter reset) | 139 (10) |
| 0xaa | statusValue == 1 (set by the level / cinematic script) | 95, 99, 109, 119, 126, 133, 144, 160, 161 |
| 0xab | docked at target, **inside its Space Lounge**, lounge intro finished | 110, 111, 113, 115, 116, 148-151 |
| 0xac | as 0xab with goods in cargo | 112 (146 Magnetar Juice) |
| 0xae | statusValue ≥ goods amount (e.g. mined tons) | 135 (155 Titanium ×140) |
| 0xb8 | statusValue == 0 (people still to move, decremented by the level); index 92 additionally needs the transporter docking point | 91, 92, 94, 102 |
| 0xbd | docked, an equipment item of sort == statusValue mounted | 20 (sort 6 = EMP bomb) |
| −1 (empty) | never | 45, 84, 128, 130, 162 (free play / wanted hunts) |

Types 0x97 (kills), 0x98 (item mounted), 0x99 (stations visited), 0x9b (`Status+0x1d4`), 0x9d, 0xa2 (blueprint
ingredients), 0xa7, 0xa9, 0xad, 0xb7 exist in `missionCompleted` / other code but no story step uses them (verified
against the transition table). Freelance types 0..15 are named by texts 354..369.

---

## 3. Who advances the story

### 3.1 Docked (`ModStation`, verified)

1. **Docking** (`MGame::dockEvent` 0x1afebc → module 5) runs `ModStation::OnInitialize` 0x0e8080: step-specific stock
   fixes (below), autosave, then `ModStation::OnUpdate` 0x0ed2a8 calls `Status::missionCompleted(true, lounge…)`.
2. If it returns a mission → `DialogueWindow(mission, 0, mode 1)` (success). Campaign content = success pages of the
   current index (§5). A freelance success adds text 389 "Return to #S…" style lines instead.
3. Closing the dialogue (`ModStation::OnTouchEnd` 0x0ea4ec):
   - freelance: `incMissionCount`, reward message, credits, remove;
   - campaign **index 43**: starts the **ending** (§9), no increment yet;
   - campaign **index 148** (broker hunt): no increment (see special case below);
   - otherwise **`nextCampaignMission`**, then by the *new* index N:
     - N ∈ {48, 49, 55, 56, 58}: the hangar cutscene swaps the ship model (loaner / own ship);
     - N ∈ {9, 44, 75, 76, 83}: mission removed, **station module reloaded** (a fresh station scene; the next
       dialogue follows immediately because the new objective is "docked here");
     - N = 45: mission removed, back to free play (a module call through the vtable, uncertain what it shows);
     - N = 18: mission removed, nothing else;
     - N = 77 at station 100: ship 37 Cronus added to the dealer (price 0 via `OnInitialize`);
     - N = 78: `departStation` + space module (**auto-launch**, escape from Valkyrie);
     - N = 84 at station 100: ships 38 Typhon and 40 Nemesis to the dealer, +1 137 S'kolptorr Rum cargo;
     - N = 104: hangar window rebuilt (blueprints);
     - **auto-launch into a story orbit** (`initStreamOutPosition = 1`, `departStation(target)`, module 2): N = 89 → 109,
       99 → 10, 109 → 114, 119 → 10, 133 → 120; N = 144 → 112 and N = 160 → 10 (without stream-out);
     - N = 128: `Status::activateNewWanted` (the Pal Tyyrt hunt uses the wanted boards);
     - then the **reward** (`Mission::getReward + getBonus` of the completed mission) is credited, except for the
       reload / launch branches (their missions all have reward 0 anyway).
4. **Lounge-only objectives** (0xab/0xac) complete when the lounge intro has finished.
5. **Special lounge logic** in `ModStation::OnUpdate`:
   - index **116** in system 18 (Pescal Inartu): each lounge of stations 90-94 not yet searched sets bit (station−90) in
     the statusValue and plays a flavour line (sound 0x619+i, text 2716+i, station 94 uses slot 3) – Maissa (93) is the
     real target (type 0xab);
   - indices **148-150**: in the lounges of 55 Kappa, 66 Inari Onu, 9 Coppolite a one-off broker dialogue (index 148/149/150
     content, bits 1/2/4 of the statusValue, dummy `Mission(0xa0,0,-1)`); at **96 Kalun Amir** the objective completes,
     the code sets the index to **151** and plays dialogue 151; closing it advances to 152. Indices 149/150 are only
     dialogue slots, never current.
6. Step-specific station tweaks in `OnInitialize`: index 20 at Kappa: item 41 EMP GL I free + 10 added to stock; index 27
   at the target: 131 Alien Remains removed from cargo; index 77 at 100: Cronus price 0; indices 80-84 (or `dlc1Won`) at
   100: dealer offers 37/38/40; `gameWon` at Thynome without any Khador Drive: 50 Void Crystals stocked; stations 10/100/101:
   ≥ 10 Energy Cells in stock if the player has < 6; index 162: hint 613 once; `gameWon` / `dlc1Won`: add-on hints 110/111
   if not owned.

### 3.2 In space (`MGame`, `Level`, `LevelScript`)

- **Active level mission** (`Status::departStation` 0x0b63e0, verified): on every launch `Status+400` = the first slot
  whose target is the departing station and whose type is not a "docked/counter" type (excluded: 0x96, 0x97, 0x99,
  0x9b, 0xa1, 0xa2, 0xa3, 0xa4, 0xa6, 0xa7, 0xa9, 0xad, 8, 0xe; a freelance 0xb/0xd also excluded); plus: type 0xa1 in the
  Void-invasion orbit (index < 45), type 0xa0 when the orbit is **not** the target, type 0xad with its goods in cargo.
  `Level::init` 0x0bb49c builds **`Level::createCampaignMission`** when that mission is a campaign mission (unless the
  game is won and no add-on is owned), otherwise the normal `Level::createMission` traffic.
- **Briefing**: `MGame::dialogueEvent` 0x1b0498, after the level's start sequence, opens `DialogueWindow(mode 0)` when the
  index has briefing pages (`DialogueWindow::hasBriefingDialogue` 0x194c90) and the mission is visible.
- **Success**: `MGame::successCheck` 0x1b0620: `missionCompleted(false, …)` or the level objective
  (`Level::checkObjective`). Campaign with success pages → `DialogueWindow(mode 1)`; closing it (`MGame::OnTouchEnd`) →
  `nextCampaignMission` + credits. Campaign **index > 45 without success pages** → `nextCampaignMission` immediately
  (objectives removed, level mission emptied).
- **Failure**: `MGame::gameOverCheck` 0x1b0d04: player dead (after the emergency system) or `Level::checkGameOver` →
  `DialogueWindow(mode 2)` = text 392 "Mission failed!" (+ 527 "The freighter has been destroyed." at indices 38, 40, 41)
  + 319 "Game Over"; the game then reloads the last record (`RecordHandler::recordStoreRead`). Three or more failures in
  a row of the same index make enemy guns deal **×0.7** damage (`Globals::lastCampaignMissionFailCount`,
  `Level::assignGuns` 0x0cb638).
- **Wormholes** (`MGame::OnUpdate`, verified): entering a wormhole with a campaign level mission: index < 41 →
  `nextCampaignMission` (index 29 excluded; index 40 only after `LevelScript` event > 3, and the freighter HP is carried
  over in `Level::lastMissionFreighterHitpoints`); index **41 → the player dies** (you must win first); index 42 →
  objectives removed; then the Void-space exit (`Status+0x84` or the Void station).
- **Khador Drive**: at index 78 using the drive (`MGame::UseKhadorDrive` 0x1a9480) charges a jump to Void space and calls
  `nextCampaignMission`.
- **`LevelScript::process` 0x160d50** advances scripted steps itself (cinematics, fly-throughs). Call sites of
  `nextCampaignMission`: 0x1628be (then departs to station 100), 0x16440c (→ space module), 0x166764 (setStation current),
  0x166eae+0x166eb6 (**two** increments, then station 10), 0x16b72c, 0x16b7d0 (→ station 111), 0x16d41e (→ station 98),
  0x16e102 (→ 111), 0x16f2b0 (→ 113). The one at 0x166764 is inside `case 0x90` (index 144 → 145). Mapping the others to
  indices needs the per-level work (uncertain).
- **Cinematic interludes** (`LevelScript::LevelScript` 0x15e650): indices **95, 99, 109, 119, 126, 133, 160, 161** hide and
  protect the player ship, switch the HUD off and use a look-at camera at (−15000, 800, 75000); their type-0xaa objective
  is set by the script so the success dialogue plays over the scene. These are the "scenes without Keith".
- **Story calls in space**: type 0xa4 (10 s after launch), 0xa0 (10 s outside the target orbit), and the add-on entry
  calls (§3.3).

### 3.3 Add-on entry (verified, `MGame::OnUpdate` 0x1ac778)

- **Valkyrie**: owned (`options[0x35]`), `hints[0x26]` unset, in space, not mining/autopilot, no level mission, `gameWon` →
  success dialogue **46** ("Incoming call detected" … meet Corny at Kanado) with a dummy `Mission(0xa0,0,-1)`, then
  **two** `nextCampaignMission` (45 → 46 → 47 = `Mission(0xb,0,74)`).
- **Supernova**: owned (`options[0x37]`), `hints[0x31]` unset, same conditions, `dlc1Won` → dialogue **85**, then 84 → 85 → 86
  (`Mission(0xb,0,100)`).
- Without the add-on, after `gameWon` / `dlc1Won` the station shows hint 110 / 111 (buy the add-on) once.

---

## 4. Complete step table (indices 0-162)

Columns: **type** (§2.2), **target** station, **obj** = objective text shown in the Missions window
(`DAT_00258f68[index]`, '#' = target station name; 749 = "I'm on my way to #."), **brief / success** = dialogue text ID
ranges (pages; `dialog N` in the tool prints them with speakers), **reward** credited when the success dialogue closes,
**effects** = side effects of the `nextCampaignMission` case that created the step. "orbit" = the step's level is built
in that station's orbit; "dock" = completes on docking. Goal lines are inferred from type + dialogue + objective text.

### Main campaign (0-45)

| # | type | target | obj | brief | success | goal (where / how) · reward · effects |
|---|---|---|---|---|---|---|
| 0 | 0x04 | 78 Var Hastra | 749 | 1677-1680 | – | Prologue 3598 A.D.: Phantom vs pirates, tutorial (orbit 78). resetGame |
| 1 | 0x0b | 78 | 749 | – | 1700-1718 | Drifting-ship cinematic, towed into Var Hastra; wake up, Gunant gives Betty. Ship → Betty (in `OnInitialize`) |
| 2 | 0x9a | 78 | 749 | 1721-1725 | 1728-1730 | Mine 10 t of ore (load ≥ 10), orbit 78; success in space |
| 3 | 0x0b | 78 | 749 | – | 1733-1737 | Dock at Var Hastra with the ore |
| 4 | 0x9a | 78 | 749 | 1738 | 1739-1740 | Mine a full 25 t; a pirate shows up (success in space: flee). Cargo cleared first |
| 5 | 0x0b | 78 | 749 | – | 1741-1746 | Dock at Var Hastra; the hangar opens (menu button 0 enabled from index 5) |
| 6 | 0x9e | 78 | 749 | – | 1747 | Buy and mount a weapon and a shield. Cargo removed |
| 7 | 0x04 | 78 | 749 | 1748-1753 | 1758-1762 | Raid the pirate hideout with Gunant (orbit 78) |
| 8 | 0x0b | 78 | 749 | – | 1763-1771 | Dock at Var Hastra; the year is 3624. All items made saleable |
| 9 | 0x0b | 78 | 749 | – | 1772-1782 | Station reload, story beat "35 years passed", Gunant sends you to Norris. Map + Missions buttons enabled |
| 10 | 0x0b | 79 Kernstal | 652 | – | 1783-1794 | Dock at Kernstal, meet Norris Bernard. Drill → 86 IMT Extract 1.3 |
| 11 | 0x0b | 76 Yrdal Gedal | 653 | 1795 | 1796-1808 | Check the silent station (briefing in orbit), dock: Mkkt Bkkt |
| 12 | 0x0b | 79 | 654 | – | 1809-1816 | Report to Norris; Space Lounge opens (button 1 from index 12) |
| 13 | 0x96 hidden | – | 655 | – | 1817-1818 | Complete one freelance mission; then Norris' call (convoy arrived) |
| 14 | 0x04 | 79 | 656 | 1819 | – | Save the Terran convoy from pirates (orbit 79); captured → Alioth |
| 15 | 0x0b | 98 Alioth | 749 | – | 1825-1835 | Interrogated at Alioth; Void alarm |
| 16 | 0x04 | 98 | 657 | 1836-1837 | 1843-1845 | Defend the freighters against Voids (orbit 98) |
| 17 | 0x0b | 98 | 749 | – | 1846-1862 | Brent's briefing; jumpgate hint |
| 18 | 0x9c | 56 Suttnar | 658 | – | 1863-1870 | Reach Suttnar orbit (10 s): Boyle kidnapped. Ship race → Terran |
| 19 | 0x9c | 55 Kappa | 749 | – | 1871-1875 | Reach Kappa orbit: EMP-bomb plan |
| 20 | 0xbd (sort 6) | 55 | 659 | – | 1876 | Dock at Kappa and mount EMP bombs (10 free in stock) |
| 21 | 0x04 | 55 | 659 | 1877-1878 | 1886 | Disable the kidnapper with EMP between two waypoints (orbit 55) |
| 22 | 0x0b | 55 | 749 | – | 1887-1896 | Dock: Boyle rescued, Wolf-Reiser coordinates |
| 23 | 0x0b | 10 Thynome | 660 | 1897 | 1898-192 | Take Boyle to Deep Science (Thynome), meet Carla · **20 000** · system 6 visible; tractor-beam vendor in the lounge |
| 24 | 0x04 | 48 Sahi | 661 | 1913-1916 | – | Capture a Void ship with scanner + tractor beam at Sahi (map gate 532); wormhole → 25. Invasion 9/48 |
| 25 | 0x9c | −1 | 749 | – | 1922-1924 | Void home system (first wormhole ride). 131 Alien Remains unsaleable |
| 26 | 0x04 | 48 | 749 | 1928-1930 | 1931-1932 | Back at Sahi, Voids following: survive (orbit 48). Invasion off |
| 27 | 0x0b | 10 | 749 | – | 1933-1943 | Deliver the Void remains to Thynome (removed on docking); date deal |
| 28 | 0x04 | 91 Dima | 662 | – | – | Enter the Void wormhole at Dima with the probe; wormhole → 29. Invasion 18/91 |
| 29 | 0x04 | −1 | 749 | 1947 | 1954 | Void space: lock the mother ship to launch the probe, fly back |
| 30 | 0x9c | 91 | 749 | – | 1955-1959 | Back in Dima orbit: call Carla |
| 31 | 0x0b | 98 | 749 | – | 1960-1969 | Dock at Alioth, Brent's news · **30 000** |
| 32 | 0x0b | 10 | 663 | 1970 | 1971-1981 | Thynome: meet Khador (drive idea) |
| 33 | 0x08 | 10 | 664 | – | 1982-1990 | Bring **50 Void Crystals** (164) from the Void to Thynome |
| 34 | 0x0b | 30 Nähma | 665 | – | 1991-2005 | Brent's plan at Nähma. Crystals removed; **Khador Drive blueprint** unlocked |
| 35 | 0x0b | 29 Ga'kkrr | 666 | – | 2006-2015 | Meet Errkt Uggut in S'kolptorr |
| 36 | 0x0c | 27 B'akka | 667 | 2016-2017 | 2018-2019 | Kill contest against Errkt at a pirate hideout (orbit 27) |
| 37 | 0xa0 hidden | 27 | 749 | – | 2020-2027 | Delayed call: Brent furious; convoy hijacked |
| 38 | 0x04 | 22 Dekato | 668 | 2028 | 2030-2031 | Destroy the Mido escort without hitting the convoy (fail text 527) |
| 39 | 0x0b | 30 | 749 | – | 2032-2041 | Final briefing at Nähma (bomb, scanner + tractor beam for Errkt's pod) |
| 40 | 0xa1 | −1 (invasion orbit) | 669 | – | – | Escort Errkt's freighter into the Void wormhole (fail 527) |
| 41 | 0x04 | −1 | 749 | 2049-2051 | 2058-2062 | Void space: mother ship battle, destroy the freighter yourself (fail 527; entering the wormhole early kills you) |
| 42 | 0xa0 | −1 | 749 | – | 2065-2068 | Out of the wormhole: call Carla. Invasion disabled (−10) |
| 43 | 0x0b | 10 | 749 | – | 2069-2070 | The date at Thynome → **ending + credits** (§9) |
| 44 | 0x0b | 10 | 670 | – | 2075-2078 | Morning after, Carla's note |
| 45 | empty hidden | – | 670 | – | – | **Free play** (`gameWon`) · **+40 000** credited by `nextCampaignMission` 44→45 |

### Valkyrie add-on (46-84)

| # | type | target | obj | brief | success | goal · reward · effects |
|---|---|---|---|---|---|---|
| 46 | (none) | – | 671 | – | 2079-2088 | Radio call in space (add-on entry, §3.3); transient |
| 47 | 0x0b | 74 Kanado | 749 | – | 2089-2104 | Meet Cornelius (Corny) at Kanado |
| 48 | 0x0b | 58 B'akrram | 672 | 2105-2111 | 2112-2119 | Chauffeured to B'akrram in loaner **9 H'Soc** (own ship parked); autopilot disabled at 48 |
| 49 | 0x9c | 58 | 672 | – | – | Leave in the stolen **41 K'Suukk** calmly (3× FlaK 9-9). Docking refused 49-54 except at Kanado |
| 50 | 0x9c | 62 Makke S'ik | 672 | – | – | Fly the K'Suukk through Makke S'ik orbit |
| 51 | 0x9c | 25 S'inokk | 672 | – | – | … through S'inokk orbit |
| 52 | 0xa0 hidden | 25 | 672 | – | – | Delayed trigger; next is 54 (53 skipped) |
| 54 | 0x0b | 74 | 672 | – | 2130-2137 | Deliver the ship to Kanado · **200 000**; Herjaza coordinates |
| 55 | 0x0b | 101 Valkyrie | 673 | – | 2140-2153 | Meet Alice on Valkyrie. System 23 visible, own ship back |
| 56 | 0x04 | 102 Scion | 674 | – | 2160-2161 | Test the turret on loaner **39 S'Kanarr** (orbit 102) |
| 57 | 0x0b | 101 | 749 | – | 2162-2171 | Report · **150 000**; Liberator talk |
| 58 | 0xa6 | 101 | 675 | – | 2173-2184 | Build **10 Liberators** (179) from the blueprint, bring them. Own ship back |
| 59 | 0xa3 | 101 | 676 | – | 2190 | Destroy the rival freighters at 56 Suttnar, 45 Öhna, 22 Dekato (bonus per Liberator kill) |
| 60 | 0x0b | 101 | 749 | – | 2193-2200 | Report · **50 000 + 50 000·v** (v = statusValue of step 59) |
| 61 | 0xa4 | 101 | 677 | – | 2201-2208 | 10 s in space: Carla's call |
| 62 | 0x0b | 100 Kothar | 678 | – | 2210-2222 | Visit Carla at Kothar. System 22 visible |
| 63 | 0x04 | 103 Coromesk | 679 | – | 2226-2227 | Find kidnapped Khador: beat the pirates at Coromesk. System 24 visible |
| 64 | 0x04 | 104 Nosdron | 680 | – | 2235-2239 | Free Khador at Nosdron |
| 65 | 0x0b | 100 | 749 | 2240-2250 | 2254-2270 | Bring Khador back to Kothar |
| 66 | 0x0b | 101 | 681 | – | 2271-2282 | Back to Alice; Cornelius, mines |
| 67 | 0x04 | 104 | 682 | 2283-2286 | 2299-2306 | Cover Cornelius vs the Skavac pirates (mines 60/61/62 in stock) |
| 68 | 0x08 | 66 Inari Onu | 683 | – | 2308-2317 | Bring a **Void Essence** (175, drilled in Void space) to Netor |
| 69 | 0x06 | 66 | 684 | – | – | Hunt Trot Lykkt (Disruptor thief) at Inari Onu orbit |
| 70 | 0x06 | 65 Lopat | 684 | – | 2324 | … at Lopat |
| 71 | 0x0b | 66 | 685 | – | 2325-2331 | Report to Netor |
| 72 | 0xa4 | 101 | 686 | – | 2332-2346 | Alice's call in space · **150 000** (mission reward). **Disruptor Laser blueprint**, +1 Void Essence |
| 73 | 0x0a | 81 Teres | 687 | – | 2355-2363 | Intercept the "weapons" convoy |
| 74 | 0x0b | 100 | 688 | – | 2364-2365 | Escort it to Kothar |
| 75 | 0x0b | 100 | 749 | – | 2366-2378 | Carla: Alice is her sister (station reload) |
| 76 | 0x0b | 100 | 749 | – | 2379-2388 | Khador's warning (station reload) |
| 77 | 0x0b | 101 | 689 | – | 2389-2408 | Confront Alice: imprisoned (jump drive unsaleable, no autosave) |
| 78 | 0x04 | 101 | 690 | – | – | Escape Valkyrie (auto-launch; jump drive removed) |
| 79 | 0xa5 | −1 | 690 | – | – | Escape continues (level-driven) |
| 80 | 0x01 | 100 | 691 | – | 2426-2431 | Defend Kothar against Alice |
| 81 | 0x04 | −1 | 691 | – | – | Chase Alice (level-driven, uncertain where) |
| 82 | 0x0b | 100 | 749 | – | 2436-2443 | Aftermath with Carla |
| 83 | 0x0b | 100 | 749 | – | 2444-2452 | Khador gives the **Cronus**; station reload → 84: dealer adds Typhon + Nemesis, +1 S'kolptorr Rum |
| 84 | empty hidden | – | 692 | – | – | Free play (`dlc1Won`); jump drive saleable, +1 Khador Drive |

### Supernova add-on (85-162)

| # | type | target | obj | brief | success | goal · effects |
|---|---|---|---|---|---|---|
| 85 | 0xa4 | – | 692 | – | 2474-2475 | Carla's call (add-on entry); transient |
| 86 | 0x0b | 100 | 693 | – | 2476-2479 | Pick Carla up at Kothar |
| 87 | 0x04 | 10 | 694 | – | – | Fly her to Thynome (orbit 10) |
| 88 | 0x0b | 10 | 694 | – | 2486-2493 | "A few months later…" |
| 89 | 0x04 | 109 Naneroh | 694 | – | – | Auto-launch: the Ginoya supernova (orbit 109). 10 Luxury removed, systems 27/28 visible |
| 90 | 0x0b | 10 | 694 | – | 2495-2503 | Gunant's distress call: bring passenger cabins. Naneroh un-visited |
| 91 | 0xb8 (10) | 110 Valpatro | 695 | – | – | Evacuate 10 miners (cabins gate 3214) |
| 92 | 0xb8 (10) | 113 Tadram | 696 | – | 2523-2529 | Drop them at the transporter |
| 93 | 0x0b | 114 Midantha | 697 | – | 2530-2540 | Meet Bargand Surr (story radio flag) |
| 94 | 0xb8 (83) | 111 Luur | 698 | – | 2548-2554 | Evacuate Luur (+Gamma Shield I; cabins gate) |
| 95 | 0xaa | 10 | 698 | – | 2556-2571 | Cinematic: Carla, Moonsprocket, Lampeter, Khador |
| 96 | 0x0b | 98 | 699 | – | 2572-2581 | Brent at Alioth |
| 97 | 0x04 | 85 Genoh | 700 | – | 2585-2587 | Pirates at Genoh |
| 98 | 0x0b | 120 Katashän | 701 | – | 2588-2599 | Meet Trunt Harval. System 29 visible |
| 99 | 0xaa | 10 | 701 | – | 2601-2612 | Cinematic (auto-launch): Moonsprocket and Carla |
| 100 | 0x04 | 98 | 702 | – | – | Alioth orbit event |
| 101 | 0x0b | 98 | 702 | – | 2615-2624 | Report to Brent (+repair beam next) |
| 102 | 0xb8 (1700) | 113 | 703 | – | 2634-2639 | Protect the dropships at Tadram (+207 repair beam) |
| 103 | 0x0b | 10 | 704 | – | 2640-2649 | Thynome: reversal plan |
| 104 | 0xa6 | 10 | 705 | – | 2650-2656 | Build **Gamma Shield II** (206) from the blueprint and bring it |
| 105 | 0x04 | 109 | 706 | – | – | Fire Khador's missile into the supernova (Gamma Shield II gate 3217) |
| 106 | 0x04 | 111 | 707 | – | – | Check Luur |
| 107 | (none) | – | 708 | – | – | Follow the stealth fighter (no case 106: level-driven, uncertain) |
| 108 | 0x0b | 10 | 709 | – | 2672-2686 | Back to Thynome |
| 109 | 0xaa | 114 | 710 | – | 2688-2691 | Cinematic (auto-launch): Bargand and Brent |
| 110 | 0xab | 10 | 710 | – | 2692-2694 | Thynome lounge: barkeeper |
| 111 | 0xab | 38 Nepis | 711 | – | 2695-2699 | Nepis lounge: drunk lady (story radio flag) |
| 112 | 0xac | 38 | 712 | – | 2700-2702 | Bring her a Magnetar Juice (146) |
| 113 | 0xab | 82 Plural Z | 713 | – | 2703-2707 | Plural Z lounge |
| 114 | 0x04 | 83 Marktesh | 714 | – | 2711 | Loan sharks at Marktesh |
| 115 | 0xab | 82 | 715 | – | 2712-2715 | Back to Plural Z |
| 116 | 0xab | 93 Maissa | 716 | – | 2720-2737 | Search the Pescal Inartu lounges (90-94; flavour 2716-2719) |
| 117 | 0x0b | 126 Bak S'ondorr | 717 | – | 2738-2743 | Go to Bak S'ondorr. System 30 visible |
| 118 | 0x08 | 126 | 718 | – | 2744 | Buy K'mirkk Toad Mutagen (209) there |
| 119 | 0xaa | 10 | 718 | – | 2746-2756 | Cinematic (auto-launch): Carla and Khador |
| 120 | 0x04 | 40 Valadon | 719 | – | – | Careful transport leg (level, uncertain) |
| 121 | 0x08 | 93 | 719 | – | 2758-2766 | Deliver the mutagen to Moonsprocket |
| 122 | 0x0b | 10 | 720 | – | 2767-2784 | Bring Moonsprocket to Thynome |
| 123 | 0x04 | 121 Növan | 721 | – | – | Nivelian vaults at Növan |
| 124 | 0x0b | 121 | 721 | – | 2789-2803 | Dock at Növan |
| 125 | 0x04 | 55 | 722 | – | 2822-2827 | Search the prism freighter's route (black box) |
| 126 | 0xaa | 120 | 722 | – | 2829-2834 | Cinematic: Trunt Harval |
| 127 | 0x0b | 98 | 723 | – | 2835-2845 | First prism to Brent |
| 128 | empty hidden | – | 724 | – | 2846-2849 | Hunt Pal Tyyrt on the wanted boards (`activateNewWanted`) |
| 130 | empty hidden | – | 725 | – | 2850-2858 | Hunt Kehnor (129 skipped) |
| 131 | 0x04 | 112 Var Lupra | 726 | – | – | Deliver the second prism (orbit 112) |
| 132 | 0x0b | 112 | 726 | – | 2860-2866 | Dock at Var Lupra |
| 133 | 0xaa | 120 | 726 | – | 2868-2870 | Cinematic (auto-launch): Trunt |
| 134 | 0x0b | 22 | 727 | – | 2871-2878 | Mining operation at Dekato |
| 135 | 0xae | 103 | 728 | 2879-2882 | 2887-2888 | Mine **140 t Titanium** (155) at Coromesk (drill gate 3213) |
| 136 | 0x0b | 112 | 729 | – | 2889-2894 | Third prism to Var Lupra |
| 137 | 0x04 | 58 | 730 | – | – | B'akrram orbit |
| 138 | 0x0b | 58 | 731 | – | 2900-2923 | Mekant at B'akrram |
| 139 | 0xa8 (10) | 131 Bra'Murr | 732 | – | 2929 | Hack the cargo bay (Vossk ship + signature gate 3215). System 31 visible |
| 140 | 0x0b | 112 | 733 | – | 2930-2941 | Last prism to Var Lupra |
| 141 | 0x0b | 78 | 734 | – | 2942-2951 | Gunant at Var Hastra. **Chromo Plasma blueprint** |
| 142 | 0x04 | 79 | 735 | – | 2957-2961 | Plasma tutorial at Kernstal (+15 ionizing missiles, filter, collector; gate 3216/3218) |
| 143 | 0x08 | 112 | 736 | – | 2962-2967 | Bring Chromo Plasma (210) (story radio flag) |
| 144 | 0xaa | 112 | 736 | – | – | Array firing cinematic (`LevelScript` case 0x90 → 145) |
| 145 | 0x04 | 112 | 737 | – | 2975 | Trunt attacks the array |
| 146 | 0x0b | 112 | 738 | – | 2976-2987 | Array destroyed: regroup |
| 147 | 0x04 | −1 | 739 | – | 2990-2997 | Void space: Alice and Valkyrie |
| 148 | 0xab | 96 Kalun Amir | 740 | – | 2998-3003 | Find a real-estate broker (flavour at 55/66/9 = dialogues 148/149/150) |
| 151 | 0xab | 96 | 740 | – | 3012-3019 | Broker found at Kalun Amir |
| 152 | 0xa5 | −1 | 741 | – | 3020-3027 | Back to Alice |
| 153 | 0x0b | 98 | 742 | – | 3028-3038 | Energy cells from Brent |
| 154 | 0x04 | −1 | 743 | – | 3051-3066 | The deal in Void space |
| 155 | 0xa4 | – | 743 | – | 3067-3070 | Call after launch |
| 156 | 0x0b | 99 Damarque I | 744 | – | 3071-3075 | Hand Alice over |
| 157 | 0x04 | 112 | 745 | – | – | Defend Var Lupra |
| 158 | 0x04 | 111 | 746 | – | 3105-3109 | Take Trunt Harval down at Luur |
| 159 | 0x0b | 10 | 747 | – | 3110-3120 | Celebration at Thynome |
| 160 | 0xaa | 10 | 748 | – | 3122-3124 | Cinematic: Carla finds Keith's letter |
| 161 | 0xaa | 93 | 748 | – | 3126-3131 | Cinematic: the bar at Maissa |
| 162 | empty hidden | – | 748 | – | – | End (hint 613 "wanted boards unlocked" once) |

Counts: **163 index values** (0-162); 161 reachable (53 and 129 are skipped; 46, 85, 149, 150 are transient/dialogue-only).
Dialogue tables cover 0-161 (162 entries). Briefing pages: 60 in total over 19 indices; success pages: 1028.

---

## 5. Dialogue system (`DialogueWindow`, game/DialogueWindow.c, verified)

- Modes (`DialogueWindow+0x3c`): **0 briefing, 1 success, 2 failure**. `set(mission, mode, index)` 0x1949dc (index −1 = current
  campaign index); mode 1 marks the mission won, mode 2 failed.
- Campaign pages: `DialogueWindow::init` 0x194734 precomputes offsets from `DAT_00259ed0` (briefing lengths) and
  `DAT_0025a158` (success lengths); `loadContent` 0x194cd0 reads the pair `(speaker, textId)` at
  `DAT_0025a3e0` / `DAT_0025a5c0` + offset + 2·page. `length()` 0x195ca4 = entries / 2.
- Speaker `s`: portrait `PTR_DAT_002647ec[s]` (ImageFactory character parts), name **text 1597 + s** (0 Keith T. Maxwell,
  1 Brent Snocom, 2 Gunant Breh, 3 Norris Bernard, 4 Mkkt Bkkt, 5 Tommy, 6 Carla, 7 Errkt Uggut, … 15 Computer, 16 Info,
  17 Story, … 26 Alice, 31 Cornelius Tenner, 38 Moonsprocket, 40 Brillo Lampeter, … 70 Battleship). Speaker 0 (Keith) sets
  `+0x68` (player portrait side). Speakers 19 (Void) and 56 use the alien font.
- Per page: voice/sfx `Globals::getDialogueSoundId(textId, agent)`; with voice-over on (`options[0xe]`) pages advance when
  the sound ends. Special pages: 1718/1730 (`0x6b6`/`0x6c2`) get key-binding tokens replaced; 1833 (`0x729`) plays sound
  0x88 + 0xa2; 2785/2786 (`0x891`/`0x892`) are swapped for 2783/2784 when the statusValue is 1; 2782 (`0xade`) in German
  shows the Info portrait.
- Buttons: Back (text 179), Next (180) → on the last page Close (181); Skip (395).
- Failure (campaign): 392 + (527 at indices 38, 40, 41) + 319.
- Freelance success: random text from `DAT_0025c6c4` (5 variants) + "\n\n" + 389 "Return to #S to collect your reward." for a
  story agent, else 216 "Mission accomplished!"; type 12 (challenge) uses 370 with #Q1/#Q2 lap times; station 108 (Kaamo) has its own 6/18-page
  dialogue from `DAT_0025c5f0` / `DAT_0025c680`.

---

## 6. Station bar, agents and the Missions window

- **Campaign steps are never offered by bar agents.** They are automatic: the objective of the current index is
  always in slot [0], and its dialogues open by themselves on docking, in the lounge (0xab/0xac), in space or in cinematics.
- "Story agents" in the code are the **named bar characters** of `agents.json` (`Status::loadAgents`, list at `Status+0x20`, `Status::getAgents` 0x0b9af4):
  `Agent::isStoryAgent` 0x1a6726 = `Agent+0x50 == 0` (agent index ≥ 0), `isGenericAgent` 0x1a6732 = `+0x50 == 1` (index −1,
  random). `Generator::createAgents` 0x0a1fd0 puts every named agent of the station into the lounge when **index > 16**
  (station 106 Sao Perula only after `dlc1Won`), fills up to 3-5 agents with generic ones (`createAgent`), refreshes
  offer 9 (sells an item from `DAT_00251d10`, cheaper set before index 142) and offer 10 (sells a ship from `DAT_00251f40`).
  With a hostile race a spy agent (offer 7) may appear (35 %). At **index 23 at Thynome** an extra generic agent sells item
  **68 AB-1 "Retractor"** (tractor beam) for 100 000 (Info pages 191/192 at the end of success dialogue 23); `ModStation::OnTouchEnd` at index 24
  marks him as gone when you launch.
- A story agent's freelance mission success adds "Return to #S…" (389) with the agent's station.
- Lounge visuals and chat UI: see `station_interior.md`.
- **Menu buttons** (`ModStation::OnInitialize`, verified): 0 Hangar (167), 1 Space Lounge (398), 2 Map (177),
  3 Missions (129), 4 Status (169). Half-transparent (disabled): Hangar while index < 5; Map and Missions while index < 9
  or == 15; Lounge while index < 12, == 15, or at stations 100/101.
- **MissionsWindow** (`MissionsWindow::init` 0x17a604): tabs 129 Missions / 3219 Most Wanted; left pane = campaign: objective text
  `DAT_00258f68[index]` (index < 164) with '#' → target station name (types 0xa7/0xae: '#Q' → amount progress). When
  `gameWon` and no add-on is owned: text 670, or 650 (all gold medals, VoidX at Thynome). Right pane = the freelance
  mission (freelance missions can be cancelled there). The map button in it opens `StarMap(missionMode, mission)`
  (`starmap_travel.md`).
- Stranded check on docking: no jumpgate route, no jump drive, no Khador Drive in cargo, index > 16 → offer a 25 000 shuttle
  to Magnetar (335).

---

## 7. Restrictions during a story step (details in `autopilot_travel.md` / `starmap_travel.md`)

"On a mission" = the **level mission** (`Status+400`) is non-empty and its type ∉ {0, 0xb, 0xd, 0xab, 0xac} (0xbd also
allowed for docking / the drive). Then:

- **Docking** refused with HUD event 0x15 → text **525 "Not possible on a mission."** (`MGame::dockEvent` 0x1afebc);
  also for indices 49-54 unless the station is 74 Kanado.
- **Planet-jump locks** refused with the same event (`Radar::draw` 0x1554fc, `missionBlocks`).
- **Khador Drive** refused with the same event (`MGame::UseKhadorDrive` 0x1a9480), except at index 78 (the drive *is* the
  step: it charges a jump and advances the index); additionally refused at index 65 while in the target (Kothar) orbit.
- **Autopilot button** off at index ≤ 1 and 48; planet locks only after index 1, fire-jump only from index 10; galaxy
  view on the map only after index 15.
- **Star map requirement boxes** when you pick the campaign target (`StarMap::OnTouchEnd` 0x0db69c): 24 scanner +
  tractor beam (532), 91/94 passenger cabins (3214), 105 Gamma Shield II (3217), 135 drill (3213), 139 Vossk ship + signature
  (3215), 142 spectral filter + plasma collector + 15 ionizing missiles (3216) / 1 t free cargo (3218).
- There is **no general jump block** in this binary apart from the above; the "mission blocks (text 525)" in the star-map
  notes is this same event.

**Markers**: the campaign target station gets the gold "story" icon 0x454 next to its planet on the radar
(`Radar::draw`, visible missions only, not during 148-151, special cases 116 (unsearched lounges 90-94), 120 (Maissa),
125 (freighter-route stations)), on the star map (icon slot 1), and the freelance target the white icon 0x455. The
Missions-window map draws a yellow route to the target. Void-invasion station `Status+0x80` is marked after index 32.

---

## 8. Rewards summary (verified)

Credits come only from `Mission::getReward + getBonus` when a campaign success dialogue closes (station or space), plus
the +40 000 of 44→45: 23 → 20 000, 31 → 30 000, 54 → 200 000, 57 → 150 000, 60 → 50 000 + 50 000·v, 72 → 150 000,
45 → 40 000. Items / ships / blueprints are handed out by the `nextCampaignMission` side effects listed in §4 (loaner ships
48/49/56, Khador Drive blueprint 34, Liberator 58, Disruptor 72, Cronus 77/83, Typhon + Nemesis 84, Gamma Shield I 94,
repair beam 102, Gamma Shield II blueprint 104, Chromo Plasma blueprint 141, plasma kit 142). Standing: a campaign success
dialogue also calls `Standing::applyMissionCompleted(clientRace)` only for freelance missions (campaign missions have no
client race).

---

## 9. Ending, credits and post-game

- **Index 43**, docked at Thynome, success dialogue closes (`ModStation::OnTouchEnd`): mission removed, music **144
  OutroSong**, a `Radio` with messages **2071-2074** ("Keith! You did it! …", 4000 ms delay), image 0x1b5a, a centred
  `ScrollTouchBox` with **text 48 (the staff credits)** scrolling up, `CutScene(2)` started after 6 s (`ModStation::OnUpdate`,
  state `+0x5c`). After **136 000 ms** (`+0xb0` > 0x21340, starting at −12 000) — or a tap once the last radio message is
  shown (`ModStation::OnTouchBegin` 0x0ea154) — `nextCampaignMission` (→ 44) and the station module restarts.
- **Index 44** at Thynome: dialogue 2075-2078 (Carla's note); closing → 45, **+40 000**, `gameWon`.
- Post-game: `Level::init` builds normal traffic missions; MissionsWindow shows 670; station hints 110 (buy Valkyrie);
  with all gold medals Thynome offers ship 8 **VoidX** (text 650); Thynome keeps Void Crystals stocked for the Khador Drive.
- **Valkyrie end**: success 83 at Kothar → station reload → 84 (`dlc1Won`), dealer bonuses; hint 111 (buy Supernova).
  No credits roll.
- **Supernova end**: 159 celebration → 160 (auto-launch cinematic at Thynome, the letter) → 161 (Maissa bar cinematic) →
  162 (end; hint 613). No credits roll. The main-menu credits (`MenuTouchWindow` 0x14816c, text 48) are separate.

---

## 10. Uncertainties

- Exact mapping of the `LevelScript::process` `nextCampaignMission` call sites (except 0x166764 = index 144) to indices;
  which steps (14, 24, 28, 40, 49-52, 69, 78, 79, 81, 87, 89, 91, 100, 105-107, 120, 123, 131, 137, 157) end by script vs
  by `successCheck`'s "no success dialogue → advance" rule.
- Targets −1 (25, 28-29, 40-42, 79, 81, 147, 152, 154) are Void space or the invasion orbit; which one per step needs the
  level code. 81 (chasing Alice) is a guess.
- 0xb8 statusValues 83 (Luur) and 1700 (Tadram) are read as "people to move"; the level may interpret them differently.
- The N = 45 branch in `ModStation::OnTouchEnd` calls a vtable slot (+0x10) with 0x10000 – probably a module/menu reset.
- Sort numbers (6 = EMP bomb, 10 = shield, 18 = jump drive, 19 = mining laser) are inferred from context, not from a table.
- Index 107 has no creating case (current mission stays the 106 one); it is probably advanced by the level script.
- Standing effects of story kills are handled by the levels (not traced).

---

## 11. Remake implementation notes

Plain C# (namespace `GoF2Remake.Data` / `GoF2Remake.World`), unit-testable, data generated from the tool's `json` output.

```csharp
public enum StoryObjective {           // Mission+0x8 values used by the campaign
    None = -1, Level = 0x04, Defense = 0x01, Wanted = 0x06, Purchase = 0x08, Intercept = 0x0a, Dock = 0x0b,
    Challenge = 0x0c, FreelanceCount = 0x96, CargoLoad = 0x9a, ReachOrbit = 0x9c, EquipWeaponAndShield = 0x9e,
    DelayedCall = 0xa0, VoidInvasion = 0xa1, DestroyTargets = 0xa3, CallAfterLaunch = 0xa4, InOrbit = 0xa5,
    DeliverBuilt = 0xa6, Counter = 0xa8, ScriptFlag = 0xaa, Lounge = 0xab, LoungeWithGoods = 0xac,
    AmountReached = 0xae, Passengers = 0xb8, EquipSort = 0xbd }

public sealed class StoryStep {        // one row of §4 (163 rows, index = campaign index)
    public int Index;
    public StoryObjective Type; public int TargetStation = -1; public int Reward; public bool Hidden;
    public int StatusValue; public int GoodsItem = -1, GoodsAmount;
    public int ObjectiveTextId;                       // DAT_00258f68
    public DialoguePage[] Briefing, Success;          // (speaker, textId)
    public StoryEffect[] OnEnter;                     // side effects of nextCampaignMission for this index
    public bool Cinematic, SkipIndex;                 // 95/99/...; 53/129
    public int AutoLaunchStation = -1;                // 89/99/109/119/133/144/160 (+78)
    public bool ReloadStationAfter;                   // 9/44/75/76/83
}
public readonly struct DialoguePage { public readonly int Speaker, TextId; }   // name = text 1597 + Speaker

public abstract class StoryEffect { }                 // RevealSystem(n), GiveItem(i, n), RemoveItem(i, n), SetUnsaleable(i, b),
                                                      // LoanShip(ship, race, items[]), RestoreOwnShip, UnlockBlueprint(bp, ingredients),
                                                      // SetVoidInvasion(system, station), AddCredits(n), StationStock(...)

public sealed class StoryState {       // lives in Session / the save game
    public int Index;                                 // Status+0x1e8
    public StoryMission Current;                      // slot 0 (type, target, statusValue, goods, won, visible)
    public long StepStartMs;                          // Status+0x100
    public bool PendingStoryRadio;                    // Status+0x178
    public int VoidInvasionSystem = -1, VoidInvasionStation = -1, DeparturesSinceInvasionMove;
    public ShipState ParkedOwnShip;                   // Status+0x8c
    public int[] TargetList;                          // Status+0x90
    public int Counter;                               // Status+0x174
    public bool GameWon => Index > 44;  public bool Dlc1Won => Index > 83;
}

public static class StoryRules {       // Status::missionCompleted / nextCampaignMission / departStation as pure functions
    public static bool IsComplete(StoryMission m, StoryContext ctx);   // ctx: docked, inLounge, levelMs, station, cargo, equipment, counters
    public static void Advance(StoryState s, StoryTable t, IStoryWorld w);  // index+1 (skip 53/129), apply OnEnter, new mission
    public static bool IsLevelMission(StoryMission m, int departingStation, StoryState s);  // departStation rule (§3.2)
    public static bool BlocksDockingAndJumps(StoryMission levelMission);  // type ∉ {0, 0xb, 0xd, 0xab, 0xac}
}
```

- Keep the index semantics exactly (objective of N created on entering N; success dialogue N; then N+1), including the
  skipped 53/129, the transient 46/85 and the 148→151 broker shortcut; the dialogue and objective tables are then a
  direct copy of the binary tables.
- A `StoryDirector` MonoBehaviour per scene: in `Station` it runs `IsComplete(docked: true, …)` each frame when no window is
  open, shows the dialogue, then applies the post-close branch (§3.1 step 3); in `Space` it runs the in-space check after
  5 s, the briefing after the start sequence, failure → "Mission failed!" + reload of the autosave (the remake already
  keeps one in memory), and the Valkyrie/Supernova entry calls (§3.3).
- Per-step level content (type 0x04/0x01/0x06/0x0a/0x0c/0xa1/0xa5/0xaa) comes from the separate `createCampaignMission` /
  `LevelScript` research; until then a step can be marked "auto-succeed after N seconds" for testing the flow.
- Wire the restrictions through one query (`BlocksDockingAndJumps`) used by the Dock prompt, planet-jump lock, Khador
  Drive and the star map; the requirement boxes of §7 are a small per-index table.
- Replace the remake's free-play assumptions (`Session.CampaignMission = 20`, Var Hastra drill/energy cells) once the
  story runs: start at index 0 with the prologue, or at index 1 in Betty if the prologue level is not built yet.
- Save games: persist `Index`, the current mission (type/target/statusValue/goods/visible), invasion station, parked ship,
  target list, counter; on load apply the §1.5 checkpoint repairs.
