# EVERSPACE 2 flight: measuring it from gameplay recordings

The remake's free flight (`FlightModel.StepFree`, Options > Controls "Free flight (EVERSPACE 2 style)") was first built
from public descriptions of EVERSPACE 2's controls. This file is how its numbers are tuned against the real game:
**black-box measurements of recordings**, nothing read from the game's files (its archives are encrypted, and its code
isn't ours to copy). The tool is `Reference/tools/es2flight/es2measure.py`; the results go in "Results" below and then
into the constants at the top of `FlightModel`'s free-flight section.

## What each measurement sets

| Clip | Measured | Free-flight constant it sets |
|---|---|---|
| 1 thrust | top speed, standstill to top: time and curve shape | `FreeAccelMs` (and whether `MoveTowards` (constant rate) or an exponential approach fits) |
| 1 thrust (release) | stop from top speed, dampeners on | `FreeDampenShare` |
| 2 reverse | top reverse speed, its acceleration | `FreeReverseShare` |
| 3 strafe, 4 up / down | top sideways / vertical speed | `FreeSideShare` |
| 5 drift | dampeners off (if the game has it): what the speed does | `FreeCapEaseShare`, the drift rules |
| 6 boost | boost speed, energy drain and refill times, restart level, turn and acceleration while boosting | `BoostRestartEnergy`, `FreeBoostAccelShare`, `FreeBoostTurnShare`, the pool timing |
| 7 yaw, 8 pitch | top turn rate, how fast it builds and stops (lag), at rest and at top speed | `FreeTurnLagMs`, the top rate scale |
| 9 roll | top roll rate, its lag | `FreeRollRate` |
| 10 mouse | how the aim follows the mouse (optional) | `TetherRadius`, `TetherReturnMs` |

A second and third ship of another class (light / medium / heavy) shows how the numbers scale with the ship (the remake
scales by handling).

## Before recording

- **Recording**: OBS (installed), the game's window at 1920 x 1080 or more, **60 fps constant**, high quality (CQP 16-18
  or a high bitrate); one file per clip, named as in the table (`01_thrust_shipname.mkv` ...).
- **Game settings**, written down for the results:
  - **Field of view**: the setting's value, and whether the game calls it horizontal or vertical. The turn
    measurements convert screen motion to degrees with it; clip 7's full turn checks it.
  - **Off**: motion blur, camera shake, depth of field, chromatic aberration, any speed / boost field-of-view effect if it
    can be turned off (if not, say so: it changes the background's scale while boosting).
  - The **first-person camera** for the turn clips (7-9) if the game has one: a third-person camera lags behind the ship
    and turns its own way. If only third person: record anyway and note it.
  - Default HUD size, so the speed readout stays in one place.
- **Place**: open space with a textured background (a nebula, a planet, distant asteroids), nothing close by to hit, no
  enemies. Same spot for every clip.
- **Input**: keys for movement (full input at once). For the turns a controller stick pushed fully and held is best (the
  mouse's aim moves); note which you used.

## The clips

Each clip: about 2 s still, then the input held until the value has settled **plus about 3 s**, then released, then
about 3 s more after it has settled again. Say the times you pressed and released, roughly, if you can.

1. **Thrust**: from a standstill, hold forward until top speed + 3 s, release (dampeners on) until stopped + 3 s.
2. **Reverse**: the same with backward thrust.
3. **Strafe**: the same, sideways.
4. **Up / down**: the same, vertically.
5. **Drift** (only if the game has a dampeners-off / decoupled mode): at top speed switch it off, release the thrust,
   wait 5 s; then thrust forward again for 3 s; then dampeners back on.
6. **Boost**: from top speed, hold boost until the energy is empty and keep holding 3 s; release and wait until the bar
   is full + 3 s; then tap boost briefly once. Keep the energy bar on screen.
7. **Yaw**: from a standstill, push the stick fully right and hold for at least **two full turns**, release. Note a
   landmark straight ahead at the start (a planet, a station): when it passes the crosshair again is a full turn.
   Then the same **at top speed**.
8. **Pitch**: the same, stick fully up (nose down) or down; at rest is enough.
9. **Roll**: hold roll for two full turns, release.
10. **Mouse** (optional): move the mouse a short, steady distance to the right and stop; hold still 3 s; repeat further.

## Processing

```bash
T=Reference/tools/es2flight/es2measure.py
# Frames to find the speed readout and the energy bar (pixel rectangles X,Y,W,H in the source video):
python3 $T probe 01_thrust.mkv --at 1 5 --out frames
python3 $T speed 01_thrust.mkv --crop X,Y,W,H --csv 01_speed.csv
python3 $T fit 01_speed.csv --end <release time>          # the acceleration
python3 $T fit 01_speed.csv --start <release time>        # the stop
python3 $T bar 06_boost.mkv --crop X,Y,W,H --csv 06_bar.csv
python3 $T fit 06_bar.csv --column fill --end <empty time>
python3 $T rate 07_yaw.mkv --hfov <FOV> --mask <HUD rects> --csv 07_yaw.csv
python3 $T total 07_yaw.csv --column yaw --start <turn start> --end <landmark back>   # should be 360
python3 $T fit 07_yaw.csv --column yaw --abs
```

`fit` compares three curve shapes (a constant rate, an exponential approach, an eased smoothstep) and gives the top /
settled value, the 10-90 % time, the rate and the time constant. If `total` doesn't give 360 for a full turn, scale the
field of view until it does (the setting may be vertical, or the view wider than the slider says).

Tested on synthetic clips with known motion (2026-10-09): a speed readout with stars in its rectangle (every frame read,
tau 0.79 s for 0.80), a pan (39.9 deg/s for 40, tau 0.31 s for 0.30), a roll (90.4 deg/s for 90), a whole turn's angle
(187.6 deg for 187.7), a draining and refilling bar (-0.498 / +0.671 per s for -0.5 / +0.667), linear ramps told from
exponential ones.

## Results

### Recordings (2026-10-09, Nemesis-E9-1 "Sentinel", difficulty Nightmare, third-person camera, FOV setting 75)

Clips driven by keystrokes (ydotool) and recorded at 60 fps; the turn rates from the background (`rate --hud`, which
ignores the HUD's no-motion peak), yaw also from when the view repeats during a steady turn (a full 360 degrees, so
independent of the field of view; it put the effective horizontal FOV at 73.4 for this measurement).

| Axis (full input: arrow keys / E) | Top rate | View 10-90 % rise | Release to 10 % |
|---|---|---|---|
| Yaw | **125.6 deg/s** (view repeat every 2.867 s) | 0.33 s | ~0.5 s |
| Pitch | 122 deg/s (same as yaw within the noise) | 0.40 s | ~0.5 s |
| Roll | 125.6 deg/s | 0.20 s | 0.50 s |
| Yaw at forward speed | 126 deg/s: speed doesn't change the turn rate | 0.48 s | 0.45 s |

- The turn starts ~0.17 s after the key press (input and frame latency included).
- The view's rise includes the third-person camera's rotation lag. A simulation (camera following by an exponential
  lag) shows the asset's default stiffness 5 alone would already take 0.44 s, longer than measured, so the turning
  camera runs at its "high" stiffness (10, `CameraTPRotationLagStiffnessHigh`); the ship itself then reaches its top
  rate in roughly 0.2-0.3 s.
- The mouse is a virtual joystick: the reticle stays where the mouse leaves it and the ship keeps turning at a rate by
  its offset (a 300-unit move: 105 deg/s; part of the 125 maximum); `bMoveMouseBackToCenter=False` in the player's
  GameUserSettings.ini says the same. Mouse rate vs offset is not measured yet.
- Not measurable this way: linear speed. The HUD shows no speed readout (the number at the top is not the speed), so
  the thrust / reverse / strafe / up-down / boost clips (01-06) show the inputs working but give no numbers; see
  "Forward acceleration" below for how it was measured instead.

### Forward acceleration (station distance readout, 2026-10-09)

With the crosshair on a station, EVERSPACE 2 labels it with its distance in whole metres under 1 km ("THE GOLDEN ACE
813m"). Locked ships read in 100 m steps above 1 km and move; station markers can't be locked, but the crosshair label
needs no lock. `label_distance.py` finds the label in each frame and OCRs it; the readings that fit a smooth path (a
local line within +-4 m over 0.4 s; 340 of 386 kept) give the speed from a sliding line fit over 0.3 s. Clips 24 (W 3 s
toward the station from 813 m) and 25 (S 4 s back out), first-person camera without the ship:

| s after W | 0.2 | 0.5 | 0.7 | 0.9 | 1.1 | 1.3 | 1.5-3.0 |
|---|---|---|---|---|---|---|---|
| m/s | 43 | ~55 (OCR gap) | 76 | 86 | 91 | 94 | 93-96 |

- An exponential approach, tau ~0.43 s (the gain halves every ~0.3 s; the initial ~220 m/s^2 = top / tau), 10-90 % in
  ~1 s; top speed 93-96 m/s.
- Braking with nothing held, from 94 m/s: 88 / 81 / 77 / 64 / 55 / 41 m/s over 1.6 s, roughly linear, ~35 m/s^2; the
  reverse stop from 48 m/s took ~1.4 s in the earlier image-growth run, also ~35 m/s^2. The asset's 60 is probably
  scaled by the player's `InertiaDampenersStrength` (0.5).
- Reverse top speed 48 m/s, half the forward one (the assets' 75 times `StrafeHoverBackwardSpeedFactor`, which these
  numbers put at ~0.64). Strafe and up / down weren't measured: if the same factor applies they would be ~64 / ~48 m/s.
- An earlier try measured the growth of the view (`es2measure.py zoom`, flying straight at a surface: growth = speed /
  distance) against the station many km away: the right timing, too small a signal to trust.

### Strafe and up / down (2026-10-09, third-person camera, the station 645 m ahead)

The label disappears once the station drifts ~55 px off the crosshair, so the sideways speed came from the station's
angular speed across the view (`rate --hud`, the ship masked: the third-person camera lags, so the ship moves inside the
frame) times the label's distance. Clips 26 (D 1.2 s, then A 1.2 s) and 27 (Space 1 s, then Left Ctrl 1 s):

- Strafe: 59 / 57 m/s on the plateau (right / left), built up over ~0.9 s (close to linear in the view), stopped in
  ~1.3 s. The assets' 100 m/s times the ~0.64 the backward speed suggested gives 64: consistent.
- Up / down: ~70 / ~78 m/s, ~1 s to build up. Less certain: the two directions differ and vertical moves tilt the
  third-person camera.
- Station parts nearer than the label's point read faster, so all of these are upper bounds of a sort; the camera's
  location lag softens the build-up (the ship itself is probably a little quicker).

### Boost (2026-10-09, third-person camera, toward the station from 4.2 km)

Clip 28: W 2.5 s, then Shift held 4.5 s (W still held), Shift and W released, 6 s of glide. Above 1 km the label reads in
0.1 km steps; `label_distance.py` read them (a decimal point is sometimes missed: "38.0" = 3.8), each step's first
frame gives the time per 100 m.

- The label's distance is the **camera's**, not the ship's: during the boost the third-person camera falls behind (the
  ship shrinks on screen) and then catches up, so the step times show speeds up to 460-660 m/s while the camera closes
  the gap, after the boost had ended. Only spans that start and end with the camera at its normal distance are the
  ship's: 3.8 km (5.87 s, before the boost, at ~94 m/s) to 2.0 km (11.77 s, the ship back near the camera) = 1800 m in
  5.9 s.
- The boost flames burn until Shift was released (4.5 s), so the pool lasted at least 4.5 s: the blueprint's
  `MaxBoostDuration` 3.5 s is overridden by the equipment.
- With that, the 1800 m give a boost top speed of ~345 m/s (a ~1 s ramp, the boost to Shift's release, then 35 m/s^2 of
  braking): ~x3.6 the top speed, not the assets' x2.5. +-15 % (the camera's offset at the two ends).
- The forward run (clips 24 / 25) used the first-person camera, which follows the ship rigidly enough for its numbers.

### Camera lag, turning while boosting, the mouse (2026-10-09, run 4)

Clips 30 (a yaw step in each camera view), 33 (turning at speed, then turning while boosting), 34 (mouse offsets).

- The same arrow-key yaw step in the three views (cockpit / first person without the cockpit / third person): 10 %
  reached after 0.10 / 0.09 / 0.17 s, 10 -> 85 % in 0.31 / 0.38 / 0.40 s, stopping 85 -> 10 % in 0.35 / ~0.45 / ~0.45 s.
  The cockpit view follows the ship rigidly, so the ship's own ramp is linear over ~0.4 s (0.31 / 0.75); the
  third-person camera adds only ~0.07 s of delay and a little softening (the earlier ~0.2-0.3 s estimate leaned too much
  on camera lag). In this darker scene the full-rate part of the turns reads ~0 (motion blur smears the station; the
  ramps stay sharp), so these were timed against the known 125 deg/s.
- Turning at speed: 122 deg/s; turning while boosting: 100 deg/s (x0.82). The boost widens the view (the zoom measurement
  saw x0.951 by the time the turn started, FOV ~76 instead of 73.4), which makes a turn read low: corrected ~105 deg/s,
  x0.86; with the zoom measurement's known low bias, x0.85-0.9.
- The mouse: the reticle moved 0.88 px (1080p) per mouse unit, linear (no pointer acceleration showed). Offsets
  33 / 71 / 107 / 141 / 211 px turned 0 / 0 / 23 / 32 / 47 deg/s: a hard dead zone up to ~90 px (~8 % of the screen
  height), beyond it the rate proportional to the whole offset (0.22 deg/s per px), which reaches 125 deg/s at ~560 px:
  full deflection at ~0.52 of the screen height (the first mouse clip's accelerated 300-unit jump turned 105 deg/s).
  Run 5 went further: 274 / 364 / 478 px turned 63 / 83 / 110 deg/s, still 0.229 deg/s per px through the centre, no
  saturation yet: 125 deg/s at ~546 px (0.51 of the screen height). Applied: radius 0.5, a hard dead zone (not rescaled).

### The cockpit speed gauge (2026-10-09, run 6): the reference for the linear motion

The cockpit view's dashboard has a speed gauge (km/h: forward top 343 km/h = 95.3 m/s, the distance readout's 93-96).
Recorded at the game's 4K (a 640 x 400 crop of the dashboard) and read with `gauge_speed.py` (the ring found by
correlating edge maps each frame, the cockpit sways; 99 % of the frames read). Clips 38-41: W 3 s, S 3 s, D 2 s, Space
2 s, each with its stop. **This supersedes the strafe / up-down estimates from the station's drift above** (those
depended on the depth of whatever part of the station dominated the view: 57-82 m/s strafe, 45-78 up / down).

- Top speeds: forward 343 km/h; backward, strafe and up / down 172 km/h each: exactly half (the assets'
  `StrafeHoverBackwardSpeedFactor`, here 0.5, applied to all three).
- Speeding up: a cubic ease-out to the top speed in a fixed time, v = V (1 - (1 - t/T)^3) (the assets'
  `EaseAccelerationExponent` 3). T fitted at four points each: forward 1.69 / 1.69 / 1.74 / 1.85 s -> ~1.75 s; backward,
  strafe, up 1.25-1.30 s -> ~1.27 s. The initial rate 3V/T (forward ~163 m/s^2) matches the ~170 m/s^2 the readings
  start with; the earlier exponential (tau 0.43 s) was an approximation of this curve.
- Braking with nothing held: linear, 343 -> 0 in ~2.9 s, 172 -> 0 in ~1.5 s: ~32-33 m/s^2 on every axis.
- The motion shows ~0.3 s after the key press (input and display latency; not modelled).

### The boost on the gauge (2026-10-09, run 7, clips 42 / 43)

W 2.5 s, Shift held 5.5 s, then W alone 3 s, then nothing (open space; clip 43 in a settled cockpit view: the run starts
on unpausing and checks the cockpit three times over a second; clip 42 had left the cockpit view for most of the boost).
The gauge digits needed a tight crop (+-25 x +-11 px at 4K): with four digits a wider one took in the gauge's inner ring
and read it as extra "1"s; the most common reading of nine small offsets per frame (352 of 381 frames read).

- The boost: 343 -> 915 (1.15 s after Shift) -> 1047 (1.75) -> 1073 (2.05) -> 1100 (2.65) -> 1104 km/h (2.8): a cubic
  ease-out over the gap from the top speed to ~1110 km/h (308 m/s, x3.2), T 3.1-3.2 s (fitted at the four points). The
  distance-readout estimate of ~345 m/s before was an average over a longer boost.
- A long boost kept gaining speed on this ship: ~1600-1900 km/h around Shift's release (5.5 s), both runs ~1480 km/h 2 s
  later. Probably its equipment (legendary thrusters such as "Terminal Velocity" exist); not modelled.
- After the boost with W still held: ~1480 -> 1340 km/h over ~1 s (~40 m/s^2). After W too: a fast drop to ~700 km/h
  (~2x the top speed, ~170 m/s^2 for ~1 s; the assets' `MomentumOnBoostStop`?), then the usual ~35 m/s^2. Not modelled.

### Player settings (GameUserSettings.ini / Input.ini of this installation)

Keyboard: W / S thrust, A / D strafe, Space / Left Ctrl up / down, Q / E roll, arrow keys pitch / yaw, Left Shift boost
(held), Left Alt free look (toggle), V camera, middle mouse lock target, F interact, Z photo mode, P pause; the inertia
dampener toggle has no keyboard default. Auto-roll is a per-device option (on for controllers, off for keyboard / mouse
and joysticks). Mouse sensitivity 0.5, centre dead zone 0.5, `InertiaDampenersStrength` 0.5. FOV 75 (third and first
person).

### Values from the game's assets (decoded with the player's mappings file; numbers only)

The player ship's movement component, layered BP_PawnBase -> BP_ShipBase -> BP_Ship_Player (Unreal units converted):
top speed forward 100 m/s, strafe 100 m/s, backward 75 m/s, up / down 75 m/s; strafe acceleration 500 m/s^2; deceleration
(nothing held, dampeners on) 60 m/s^2; acceleration ease exponent 3; boost: top speed x2.5, forward acceleration x1.5,
longest boost 3.5 s; TurningBoost 0 (thrust doesn't redirect the velocity); banking at zero speed x0.5. Camera: FOV 75,
+15 while boosting, cruise up to +20; third-person rotation lag stiffness 3 / 5 / 10 (low / default / high). Inertia
dampeners on by default. Forward acceleration and the rotation rates are per-ship item attributes (made at run time),
not in the assets.

### Applied to the remake (2026-10-09)

`FlightModel` free flight: turn 125 deg/s at handling 100 (sqrt-scaled by the handling) with a 250 ms linear ramp, roll
the same; strafe x1 (in 200 ms), backward and up / down x0.75; dampeners brake at 60 m/s^2 per axis; boost x2.5 top speed,
x1.5 acceleration, 3.5 s per full pool, a fresh press after running empty. `ShipController`: the mouse is a virtual
joystick (the reticle stays put). Then from the distance readout: forward / back / up-down approach their top speed
exponentially (tau 430 ms at handling 100), braking 35 m/s^2, backward x0.5; then strafe x0.6 with the same build-up (up /
down kept at x0.75); then the boost x3.5 for the booster's duration (3.5 s without one). Then run 4: the turn ramp 400 ms, x0.85 boosting, the
mouse joystick's radius 0.5 of the screen height; run 5: the mouse's hard dead zone; run 6 (the gauge): backward, strafe
and up / down x0.5, the cubic ease-out (forward T 1.75 s, the others 1.27 s), braking 33 m/s^2; run 7 (the gauge): the
boost to x3.2, eased out over 3.1 s. Still open: the third-person camera's own small lag (~0.07 s, the
remake's chase camera keeps its own), the fast drop after a boost when the thrust is released (~2x the top speed),
the boost's growth on a long boost (equipment).

### Do the ship classes change these values? (the dumped assets, 2026-10-09)

Classes: `EShipClass` Light / Medium / Heavy (+ Okkar, the mining ship). From `DT_DebugShipsKS`: Light = Scout, Stinger,
Vanguard; Medium = Sentinel, Striker, Interceptor; Heavy = Gunship, Bomber, Vindicator. The measured Nemesis is a Sentinel,
so **every number above is a Medium ship's**.

Nothing in the assets ties a flight value to the class:
- One player pawn for every ship (`BP_Ship_Player`, its `ShipMovementComponent`); no per-class or per-type pawn.
- `DT_ShipSettings` (per class) holds only the third-person camera, identical for Light / Medium / Heavy: rotation lag
  stiffness 12 (the small camera lag measured), pivot x 3000, close arm 2000 at (0, 0, 500), far 2800 at (0, 0, 600),
  free look 2000. Only the mining ship's row differs (stiffness 10, arms 3000 / 3800).
- `DT_ShipModules_<class>` (`FShipModule`): meshes per slot (body, wings, rear, extension, base, cockpit), a ship-type
  whitelist and extension meshes: looks only.
- The class is otherwise read for the camera shake scale (`GetShipClassCameraShakeScale`, `ScaleWithShipClass`) and
  the cockpit widgets (`CockpitWidgetsClassLight / Medium / Heavy / Okkar`).
- The per-ship speed, acceleration, handling and boost come from the ship's item attributes (`ID_HANDLING` etc.) in
  `ItemTemplates`, which is built at run time (transient), not stored in the cooked assets.
- Ship-type passives are conditional perks, not base values: the Interceptor's speed buff on a lock
  (`ID_INTERCEPTOR_LOCK_ON_SPEED_INCREASE`), the Striker's on a locked target (`ID_STRIKER_LOCKED_TARGET_SPEED`), the
  Gunship's damage reduction, the Vanguard's overcharge.

So any differences between Light / Medium / Heavy ships (and between ships and tiers within a class) live in the run-time
item stats and can only be measured by flying them. The remake maps its own per-ship differences through the handling
(`FreeTopTurnRate`'s sqrt(H / 20), the ease time's sqrt scaling).

### The player ships (the dumped ship module tables, 2026-10-10)

A player ship is assembled from parts (`FShipModule` rows in `DT_ShipModules_Light / Medium / Heavy`, plus `_EA` / `_DEV`
variants; `DT_ShipModules_OkkarMedium` and `_MiningShip` hold one-off hulls): a base, a body, a rear, an exterior cockpit, a
cockpit interior and wings, each a mesh with optional extension meshes. Each row has a `ShipsWhiteList` (a set of `EShip`):
only the wings fill it, each wing with exactly one ship type; bodies, rears and cockpits are open to every type of their class.
So the wings are the model, and the Medium table names them (`ModuleName`, string table `ship_modules`). Decoding note: an enum
inside a set is serialized as its FName ("EShip::Sentinel"), not as the byte a top-level enum property is.

The internal type names, matched to the in-game ones through `DT_DebugShipsKS` (each named debug build's wings -> the wing's
whitelist):

| EShip | In game | Class | Wing models (`ModuleName` where the data has one) |
|---|---|---|---|
| PatrolShip | Vanguard | Light | 4: Wings_001-004 (the debug build's Mercury = Wings_002) |
| Scout | Scout | Light | 4: Wings_005-008 (Spirit = Wings_005) |
| SpecOps | Stinger | Light | 4: Wings_009-012 (Wasp = Wings_011) |
| Interceptor | Interceptor | Medium | 4: Orion, Gryphon, Predator, Stalker |
| Sentinel | Sentinel | Medium | 4: Nemesis, Thunderbolt, Reaver, Tormentor |
| AssaultFighter | Striker | Medium | 4: Harpy, Raptor, Osprey, Hawk |
| Bomber | Bomber | Heavy | 3: Wings_001, 003, 004 (Behemoth) |
| Defender | Vindicator | Heavy | 3: Wings_005 (Typhoon), 006, 008 |
| Gunship | Gunship | Heavy | 3: Wings_009 (Liberator), 010, 012 |

33 ship models in 9 types, plus the Okkar ship (`OkkarShip1`, DLC) and the mining ship. The Light and Heavy wings' names are
empty in the data (apart from Behemoth and Titan): those ships are named elsewhere, presumably by the run-time ship items. The
debug row "Bomber_Liberator" uses a Gunship wing (Wings_009), so the Liberator is a Gunship. The measured Nemesis is the
Sentinel's Nemesis wings.

Parts per class (type `Invalid` rows left out: Medium Cockpit_Exterior_010 / 012; Heavy Body_007 / 008 / 010, Rear_002 / 003
and the wings Titan (Bomber), Wings_007 (Vindicator), Wings_011 (Gunship): cut or unreleased):

| Class | Bodies | Rears | Cockpits (exterior) | Interiors | Wings per type | Builds per type | Builds per class |
|---|---|---|---|---|---|---|---|
| Light | 9 | 9 | 3 | 3 | 4 | 972 | 2916 |
| Medium (bodies A-I, rears 1-9) | 9 | 9 | 7 | 1 | 4 | 2268 | 6804 |
| Heavy | 7 | 7 | 4 | 1 | 3 | 588 | 1764 |

About 11 500 builds in all (wings x bodies x rears x cockpits), before the wings' tier extensions (`Extensions`, meshes
`..._Ext_T1_001` to `_T4_001`: 2-4 per wing, none on some), colours (`DT_ShipColors`, `DT_ShipColorSets`), decals and skins
(`DT_ShipSkins`: Alienware, Kickstarter). `DT_Ships_SpecialOffers` holds 15 fixed backer builds with their own names: Zephyrus,
Stellarwind, X-25 Swift Wing, Bordeaux, Insurgent, Capkaru SD42 (Striker), Lacunae, Goldstar, Hyena (Bomber), Fatebringer,
Fulmin (Vindicator), Quantum (Stinger), Hawkini, Darkstar (Sentinel), K-17 (Vanguard). None of this sets a flight value: the
per-ship stats are the run-time items' (see the section above).

### Equipment in the remake's free flight (2026-10-10)

The ship-class check above left the per-ship differences to the remake. Two changes so the original's equipment matters:
- **Boost by the booster** (`FlightModel.FreeBoostFactor`): the original boost speed (int(2 b / 100) + 2 u/ms) over the base
  speed, x 3.2 / 3, so the Me'al (x3 in the original) boosts like the measured Sentinel (x3.2): Linear / Cyclotron x1.6,
  Synchrotron x2.67, Polytron x4.27. Before, every booster gave x3.2.
- **Turn spread** (`FreeTurnExponent` 0.75): the top turn rate is 125 deg/s x (H / 20)^0.75 instead of the square root: a bare
  Rhino (H 6) 51 deg/s, handling 100 125, a Dace with the Pulsed Plasma Thrust (H 74.5) 335; the original's linear rate spans
  13-161 deg/s (12x), the square root 68-241 (3.5x), now 6.6x. The acceleration ease stays square-root scaled.
Checked on `FlightModel` alone (2.7 s of boost: 160 / 160 / 266 / 320 / 426 m/s; the turn rates above).
