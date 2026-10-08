// FlightModel.cs
// Clean re-implementation of the Galaxy on Fire 2 (Android 2.0.16) player flight behaviour,
// written from notes on the decompiled PlayerEgo / Ship / TargetFollowCamera code.
// Pure C# (only uses UnityEngine math types), so it can be unit-tested outside a scene.
//
// Units: the original works in milliseconds and "GoF units". This class keeps those units
// internally so every constant matches the original; the MonoBehaviour converts to Unity.

using System;
using UnityEngine;

namespace GoF2Remake.Flight
{
    /// <summary>Per-ship inputs to the flight model. Values map 1:1 to data/ships.json + items.json.</summary>
    [Serializable]
    public class FlightStats
    {
        [Tooltip("ships.json 'handling' (e.g. Betty = 120). The game divides it by 100.")]
        public float handling = 120f;

        [Tooltip("Number of installed handling upgrades (bar-agent mods). Each adds +0.2 to base handling.")]
        public int handlingUpgrades = 0;

        [Tooltip("Agility bonus from an installed Steering Nozzle (items.json stats.agility, attribute 28). 0 = none.")]
        public float agility = 0f;

        [Tooltip("Booster items.json stats.boostSpeed (attribute 25). 0 = no booster.")]
        public int boostSpeed = 0;

        [Tooltip("Booster items.json stats.boostDurationMs (attribute 27).")]
        public int boostDurationMs = 0;

        [Tooltip("Booster items.json stats.boostRechargeMs (attribute 26). Booster is usable only if > 0.")]
        public int boostRechargeMs = 0;

        [Header("Hardcore mode: cargo weight reduces handling")]
        public bool cargoAffectsHandling = false;
        public float cargoLoad = 0f;
        public float cargoCapacity = 1f;
    }

    public class FlightModel
    {
        // ---- constants recovered from the binary -------------------------------------------
        public const float BaseSpeed = 2.0f;                  // units per ms, identical for every ship
        const float RateToRadiansPerMs = 2f * Mathf.PI / 65536f * 0.033f; // turn-rate units -> rad/ms
        const float TargetRateScale = 750f;                   // target rate = trunc(input*750*H)/63
        const int TargetRateDivisor = 63;
        const float RampBase = 3.3f;                          // ramp step = dt*H / ((3.3 - sens)*20)
        const float PitchUpSensitivityScale = 1.25f;
        const float PitchDownSensitivityScale = 1.45f;
        const float DecayDivisor = 126f;                      // no input: rate -> 0 by dt*H/126
        const float CollisionPushDecay = 0.7f;                // per frame
        const float CollisionPushCutoff = 0.01f;

        // Auto-level ("align to horizon", triggered by a double tap in the original HUD)
        const float LevelDoneThreshold = 0.015f;              // |up.x| below this and up.y > 0 -> done
        const float LevelRateUpsideDown = 0.00075f;           // rad per ms
        const float LevelRateFar = 0.00035f;                  // |up.x| > 0.3
        const float LevelRateNear = 0.00025f;
        const int LevelMaxDtMs = 60;
        const float LevelRateOvershoot = 0.00035f, LevelRateFine = 0.0002f;   // PlayerEgo::roll's fine phase (+0x324)
        int levelSide;        // PlayerEgo+0x2a9: the sign of up.x last frame (1 negative, 2 positive, 0 none)
        bool levelFine;       // PlayerEgo+0x324: the lean changed sides once, the last part goes slow
        bool rollLevelled;    // the roll part of a level-out is done
        /// <summary>Remake (#37, Settings.LevelPitch): a level-out also brings the nose to the horizon (world up, the
        /// orbit's and the station's up, like the autopilot's moveToPosition), not only the roll (the original).</summary>
        public bool LevelPitch;
        const float LevelPitchRate = 0.0006f;       // rad per ms (~34 deg/s), slower over the last 10 deg
        const float LevelPitchDone = 0.01f;         // rad

        // ---- configuration ------------------------------------------------------------------
        /// <summary>Options-menu steering sensitivity. Must stay well below 3.3/1.45 (~2.27). Default is a guess.</summary>
        public float Sensitivity = 1.0f;
        /// <summary>Tilt steering (options[0x11] = 0): PlayerEgo::down / up ramp the pitch with 1.45 / 1.25 x the tilt
        /// sensitivity; the touch stick, keys and pads use the sensitivity as it is.</summary>
        public bool TiltMode;

        /// <summary>
        /// True: turn rates and the cosmetic model tilt follow partial stick input (needed for analog sticks and
        /// touch). False: the original's rules (rates only grow toward the target while a direction is held; the tilt
        /// uses the input's sign). Identical for full-deflection (keyboard) input.
        /// </summary>
        public bool TrackAnalogInput = true;

        /// <summary>Reproduce the original's integer truncation of boost speed (see Configure).</summary>
        public bool TruncateBoostSpeed = true;

        // ---- remake debug: the weight of the hulls the player can't normally fly (PlayerHull.MassOf) ----------------------
        /// <summary>0 = an ordinary ship, flown exactly like the original; up to 1 = the largest hulls (the carrier, the Vossk
        /// battleship, the battlestation, the Void ship), set from the hull's size. It lowers the top turn rate, makes the
        /// turn rates slow to build up and to die away, eases the speed toward the throttle instead of jumping to it, and
        /// slows the level-out and the strafe: a capital ship that answers like one.</summary>
        public float Mass;
        /// <summary>The top turn rate's share.</summary>
        public float TurnScale => Mathf.Lerp(1f, 0.15f, Mass);
        /// <summary>The turn rates' ramp and decay share (their inertia): below TurnScale, so a heavy hull also takes longer to
        /// reach its (lower) top rate and to stop turning (~2 s at the carrier's 0.91, against 0.55 s for any ordinary ship).</summary>
        public float Inertia => TurnScale * Mathf.Lerp(1f, 0.18f, Mass);
        /// <summary>The auto-level's and the manual roll's share.</summary>
        public float RollScale => Mathf.Lerp(1f, 0.25f, Mass);
        /// <summary>The strafe's and the dodge's share.</summary>
        public float SideScale => Mathf.Lerp(1f, 0.2f, Mass);
        /// <summary>The time the engines take between a standstill and the base speed (ms); 0 = at once (the original). A boost
        /// surges 2.5x as fast (it would take far longer than the boost lasts otherwise).</summary>
        public float AccelMs => Mass * 7000f;
        /// <summary>The speed the ship actually flies at (units/ms): Throttle x CurrentSpeed at once for an ordinary ship,
        /// eased toward it for a heavy one (AccelMs).</summary>
        public float MoveSpeed { get; private set; } = BaseSpeed;

        float StepMoveSpeed(float dtMs)
        {
            float target = Braking ? 0f : Throttle * CurrentSpeed;
            if (AccelMs <= 1f) return MoveSpeed = target;
            float rate = BaseSpeed / AccelMs;   // units/ms per ms
            if (target > BaseSpeed || MoveSpeed > BaseSpeed) rate *= 2.5f;   // the boost's surge and its run-down
            return MoveSpeed = Mathf.MoveTowards(MoveSpeed, target, rate * dtMs);
        }

        public float Handling { get; private set; }            // "H" in the notes (0x154)
        float boostSpeedValue = 5f;
        int boostDurationMs = 5000, boostRechargeMs = 20000;
        bool hasBooster;
        FlightStats stats;

        // ---- state ----------------------------------------------------------------------------
        public float Throttle { get; private set; } = 1f;      // 0xbc, 0..1
        public float CurrentSpeed { get; private set; } = BaseSpeed; // 0xb8, units per ms
        public float YawRate { get; private set; }             // 0x27c
        public float PitchRate { get; private set; }           // 0x278
        public bool IsBoosting { get; private set; }
        public bool IsLeveling { get; private set; }
        int boostTimerMs;                                       // 0x138: counts up; negative while recharging
        float collisionPush;                                    // 0x37c

        /// <summary>Visual banking amount: +/-H while steering, 0 otherwise (0x25c / 0x258).</summary>
        public float VisualYawBank { get; private set; }
        public float VisualPitchBank { get; private set; }

        public void Configure(FlightStats s)
        {
            stats = s;
            // Ship::getHandling() = handling/100 + 0.2 per handling upgrade
            float baseH = s.handling / 100f + 0.2f * s.handlingUpgrades;
            // PlayerEgo ctor: H = (h + h*agility/100) * 20
            Handling = (baseH + baseH * (s.agility / 100f)) * 20f;

            // PlayerEgo ctor: boost speed = int(2*boostSpeed/100) + 2. The original stores this as an int,
            // so e.g. Linear Boost (60) gives 3.0 (1.5x) although the shop UI implies 1.6x, and
            // Cyclotron (80) ends up equal to Linear. Set TruncateBoostSpeed = false for the "intended" values.
            float boost = 2f * s.boostSpeed / 100f + 2f;
            boostSpeedValue = TruncateBoostSpeed ? (int)(2f * s.boostSpeed / 100f) + 2 : boost;
            boostDurationMs = s.boostDurationMs;
            boostRechargeMs = s.boostRechargeMs;
            hasBooster = boostRechargeMs > 0;

            CurrentSpeed = BaseSpeed;
            MoveSpeed = BaseSpeed;
            Throttle = 1f;
            boostTimerMs = 0;
            IsBoosting = false;
        }

        /// <summary>Handling after the hardcore-mode cargo penalty: H*(0.6 + 0.4*(1 - load/max)).</summary>
        public float EffectiveHandling
        {
            get
            {
                if (stats == null || !stats.cargoAffectsHandling || stats.cargoCapacity <= 0f) return Handling;
                float free = 1f - Mathf.Clamp01(stats.cargoLoad / stats.cargoCapacity);
                return Handling * 0.6f + Handling * free * 0.4f;
            }
        }

        /// <summary>Maximum turn rate in degrees per second, handy for tuning/UI.</summary>
        public float MaxTurnRateDegPerSec =>
            (int)(TargetRateScale * EffectiveHandling) / TargetRateDivisor * RateToRadiansPerMs * 1000f * Mathf.Rad2Deg;

        public void ChangeThrottle(float delta) => Throttle = Mathf.Clamp01(Throttle + delta);
        /// <summary>The PC version's "Brake" (binding 3361, apart from "Throttle down" 3360): the engines stop while it is
        /// held; the throttle is kept, so releasing it flies on at once. The phone original has no brake.</summary>
        public bool Braking;

        // ---- strafe (PlayerEgo::strafe 0xad838, the PC port's handler: no caller in the phone build; handleShip moves
        // the ship by it, PlayerEgo+0x37c / +0x380) -------------------------------------------------------------------
        const float StrafeFrameMs = 1000f / 30f;
        float strafeRamp = 0.1f;
        /// <summary>Sideways speed in units/ms, + = right.</summary>
        public float StrafeVelocity { get; private set; }

        /// <summary>Held strafe key, every frame: v = ramp * dir * min(H' * 30 * 0.002, 2) (H' = the cargo-reduced handling
        /// on Extreme), the ramp from 0.1 x1.5 a (30 fps) frame up to 1: full sideways speed after ~6 frames.</summary>
        public void Strafe(int dir, float dtMs)
        {
            StrafeVelocity = strafeRamp * dir * Mathf.Min(EffectiveHandling * 30f * 0.002f, 2f) * SideScale;
            strafeRamp = Mathf.Min(strafeRamp * Mathf.Pow(1.5f, dtMs / StrafeFrameMs * Inertia), 1f);
        }

        /// <summary>handleShip: while |v| &gt; 0.01 the ship moves v * dt sideways and v drops x0.7 a (30 fps) frame;
        /// below it the ramp resets to 0.1. Returns this frame's sideways move in units.</summary>
        public float StepStrafe(float dtMs)
        {
            if (Mathf.Abs(StrafeVelocity) <= 0.01f) { StrafeVelocity = 0f; strafeRamp = 0.1f; return 0f; }
            float d = StrafeVelocity * dtMs;
            StrafeVelocity *= Mathf.Pow(0.7f, dtMs / StrafeFrameMs);
            return d;
        }
        public void SetThrottle(float value) => Throttle = Mathf.Clamp01(value);

        public bool HasBooster => hasBooster;
        public bool BoostReady => !IsBoosting && hasBooster && boostTimerMs >= 0;

        /// <summary>0 at recharge start, 1 when ready.</summary>
        public float BoostRechargePercent =>
            boostTimerMs >= 0 || boostRechargeMs <= 0 ? 1f : 1f + (float)boostTimerMs / boostRechargeMs;

        public void Boost()
        {
            if (!BoostReady) return;
            Throttle = 1f;   // MGame::OnTouchEnd HUD element 2: full throttle first, then the boost
            boostTimerMs = 0;
            CurrentSpeed = boostSpeedValue;
            IsBoosting = true;
        }

        /// <summary>
        /// Boost strength for visuals (engine flame, FOV, camera): ramps 0->1 over the first sixth of the
        /// boost, holds at 1, ramps back to 0 over the last sixth. Original flame scale = 1 + 0.5*this.
        /// </summary>
        public float BoostVisualPercent
        {
            get
            {
                if (!IsBoosting || boostDurationMs <= 0) return 0f;
                float p = boostTimerMs / (boostDurationMs / 6f);
                if (p < 1f) return p;
                if (p > 5f) return Mathf.Max(0f, 6f - p);
                return 1f;
            }
        }

        public void AlignToHorizon() { IsLeveling = true; levelSide = 0; levelFine = false; rollLevelled = false; }
        /// <summary>A manual roll (remake) cancels the auto-level.</summary>
        public void StopLeveling() => IsLeveling = false;

        public void AddCollisionPush(float amount) => collisionPush += amount;

        /// <summary>
        /// Advances one frame.
        /// </summary>
        /// <param name="steer">x = yaw (-1 right .. +1 left, as in the original), y = pitch (+1 down .. -1 up).
        /// Use the controller's invert options to change feel.</param>
        /// <param name="dtMs">Frame time in milliseconds.</param>
        /// <param name="shipUp">Ship's current up vector expressed in world space (for auto-level).</param>
        /// <param name="shipRight">Ship's current right vector in world space (for auto-level).</param>
        /// <returns>Rotation to apply this frame in local space (pitch around X, yaw around Y, roll around Z),
        /// in degrees, plus forward distance in GoF units and sideways collision push.</returns>
        public FrameResult Step(Vector2 steer, float dtMs, Vector3 shipUp, Vector3 shipRight)
        {
            float he = EffectiveHandling;

            // ---- steering input: ramp turn rates toward the stick target ----------------------
            bool yawInput = Mathf.Abs(steer.x) > 1e-4f;
            bool pitchInput = Mathf.Abs(steer.y) > 1e-4f;
            // Cosmetic model tilt. The original uses the input's sign (digital keys); for analog input scale it
            // with the deflection, or a slight push past the centre flips the model to the full tilt.
            VisualYawBank = !yawInput ? 0f : (TrackAnalogInput ? Mathf.Clamp(steer.x, -1f, 1f) : Mathf.Sign(steer.x)) * he;
            VisualPitchBank = !pitchInput ? 0f : (TrackAnalogInput ? Mathf.Clamp(steer.y, -1f, 1f) : Mathf.Sign(steer.y)) * he;

            if (yawInput) YawRate = RampToward(YawRate, steer.x, he, dtMs, 1f);
            if (pitchInput)
                PitchRate = RampToward(PitchRate, steer.y, he, dtMs,
                    !TiltMode ? 1f : steer.y > 0 ? PitchDownSensitivityScale : PitchUpSensitivityScale);

            // ---- rotation for this frame --------------------------------------------------------
            float pitchRad = dtMs * PitchRate * RateToRadiansPerMs;
            float yawRad = dtMs * YawRate * RateToRadiansPerMs;
            // The roll is checked every frame until the whole level-out is done (pitching the nose moves the lean); with
            // LevelPitch a steep nose (more than ~45 deg) comes down first, where the lean means little.
            bool steep = LevelPitch && Mathf.Abs(Vector3.Cross(shipRight, shipUp).normalized.y) > 0.7f;
            rollLevelled = false;
            float rollRad = IsLeveling && !steep ? AutoLevelRoll(dtMs, shipUp, shipRight) * RollScale : 0f;
            if (IsLeveling)
            {
                bool pitchLevelled = true;
                if (LevelPitch && !pitchInput)
                {
                    // The nose's angle above the horizon; a positive local-X turn lowers the nose.
                    var fwd = Vector3.Cross(shipRight, shipUp).normalized;
                    float above = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f));
                    if (Mathf.Abs(above) > LevelPitchDone)
                    {
                        pitchLevelled = false;
                        float rate = LevelPitchRate * Mathf.Clamp01(Mathf.Abs(above) / 0.17f + 0.25f) * RollScale;
                        // Upside down (the roll not done yet) the ship's own pitch axis turns the other way.
                        float sign = shipUp.y >= 0f ? 1f : -1f;
                        pitchRad += sign * Mathf.Sign(above) * Mathf.Min(Mathf.Abs(above), rate * Mathf.Min(dtMs, LevelMaxDtMs));
                    }
                }
                if (rollLevelled && pitchLevelled) { IsLeveling = false; rollLevelled = false; }
            }

            // ---- no input: rates drain linearly back to zero (uses base H, not the cargo-reduced one)
            if (!yawInput) YawRate = Mathf.MoveTowards(YawRate, 0f, dtMs * Handling / DecayDivisor * Inertia);
            if (!pitchInput) PitchRate = Mathf.MoveTowards(PitchRate, 0f, dtMs * Handling / DecayDivisor * Inertia);

            // ---- movement -------------------------------------------------------------------------
            float forward = dtMs * StepMoveSpeed(dtMs);

            float push = 0f;
            if (Mathf.Abs(collisionPush) > CollisionPushCutoff)
            {
                push = collisionPush * dtMs;
                collisionPush *= CollisionPushDecay;
            }
            else collisionPush = 0f;

            UpdateBoost(dtMs);

            return new FrameResult
            {
                pitchDeg = pitchRad * Mathf.Rad2Deg,
                yawDeg = yawRad * Mathf.Rad2Deg,
                rollDeg = rollRad * Mathf.Rad2Deg,
                forwardUnits = forward,
                sidePushUnits = push
            };
        }

        // PlayerEgo::moveToPosition 0xa8720 (the autopilot) returns a bank value: the signed turn of each frame (+0x290, five
        // samples, +0x2a4 / +0x2a8), x the limit +0x284 = H * 750 / 63 * 1.2 x +0x288 = 15.14, clamped to the limit;
        // PlayerEgo::update then moves +0x280 toward it by dt * H / 81 and makes it the yaw rate +0x27c, which banks the model.
        const float AutopilotBankGain = 15.139845f;   // 0x41723ace
        readonly float[] autopilotTurns = new float[5];
        int autopilotTurnCount, autopilotTurnIndex;
        float autopilotRate;

        /// <summary>The autopilot's model bank (PlayerEgo::moveToPosition / update): 'turnLeft' = this frame's turn in radians,
        /// positive to the left; 'frameMs' = the frame's real length (unscaled); after Step, which left the bank at 0.</summary>
        public void AutopilotBank(float turnLeft, float dtMs, float frameMs)
        {
            float he = EffectiveHandling;
            // The original's samples are per frame (a 30 fps game): taken per 33.3 ms of real time here, so the bank doesn't
            // depend on the frame rate. Fast-forward runs the original's whole update with dt x 5 (autopilot_travel.md 4), so
            // each sample carries five times the turn and the ship banks hard into the turn (to the limit): per real time, not
            // per game time, keeps that (a player's report with the original's video; it was divided away).
            if (frameMs > 0f) turnLeft *= 33.333f / frameMs;
            autopilotTurns[autopilotTurnIndex] = turnLeft;
            autopilotTurnIndex = (autopilotTurnIndex + 1) % autopilotTurns.Length;
            autopilotTurnCount = Mathf.Min(autopilotTurnCount + 1, autopilotTurns.Length);
            float sum = 0f;
            for (int i = 0; i < autopilotTurnCount; i++) sum += autopilotTurns[i];
            float limit = TargetRateScale * he / TargetRateDivisor * 1.2f;
            float target = Mathf.Clamp(sum / autopilotTurnCount * limit * AutopilotBankGain, -limit, limit);
            autopilotRate = Mathf.MoveTowards(autopilotRate, target, dtMs * he / 81f);
            VisualYawBank = autopilotRate / (TargetRateScale / TargetRateDivisor);
        }

        /// <summary>The autopilot let go (PlayerEgo::update: +0x2a4 / +0x2a8 cleared).</summary>
        public void ResetAutopilotBank() { autopilotTurnCount = 0; autopilotTurnIndex = 0; autopilotRate = 0f; }

        /// <summary>PlayerEgo::updateManeuver: handleShip is skipped (no steering ramp, no turn from the rates); the yaw rate
        /// is the maneuver's (it banks the model and decays normally afterwards); the ship flies on at its speed.</summary>
        public FrameResult StepManeuver(float dtMs, float yawRate)
        {
            YawRate = yawRate;
            // The bank follows the rate: a full-stick rate (750 H / 63) banks like a full stick.
            VisualYawBank = yawRate / (TargetRateScale / TargetRateDivisor);
            VisualPitchBank = 0f;
            float forward = dtMs * StepMoveSpeed(dtMs);
            UpdateBoost(dtMs);
            return new FrameResult { forwardUnits = forward };
        }

        float RampToward(float rate, float input, float he, float dtMs, float sensitivityScale)
        {
            // target = trunc(input * 750 * H) / 63 (integer division, like the original)
            int raw = (int)(input * TargetRateScale * he);
            float target = raw / TargetRateDivisor * TurnScale;   // remake debug: a heavy hull's lower top rate (Mass)
            float denom = (RampBase - Sensitivity * sensitivityScale) * 20f;
            if (denom < 1f) denom = 1f; // guard against extreme sensitivity values
            float step = dtMs * he / denom * Inertia;

            // The original only accelerates toward the target in the input's direction and never slows an
            // over-target rate while the direction is held. That was fine for its digital input (the target is
            // always full deflection), but with an analog stick the rates stay saturated while the thumb moves
            // around, so steering snaps between 4 directions. Analog: track the target both ways at the same ramp.
            if (!TrackAnalogInput)
            {
                if (target > 0f && rate < target) return Mathf.Min(rate + step, target);
                if (target < 0f && rate > target) return Mathf.Max(rate - step, target);
                return rate;
            }
            return Mathf.MoveTowards(rate, target, step);
        }

        float AutoLevelRoll(float dtMs, Vector3 up, Vector3 right)
        {
            // "up.x" in the original = how much the ship's up vector leans sideways relative to the horizon; here: how far
            // the right wing points down (-right.y; for a small lean the same as up along the horizontal right). #57: it was
            // up along the horizontal part of 'right', which is nothing on a knife edge (the right wing straight up or down):
            // a ship on its side or upside down counted as level and stayed so.
            float lean = -right.y;
            float upY = up.y;

            if (Mathf.Abs(lean) < LevelDoneThreshold && upY > 0f)
            {
                rollLevelled = true;
                levelSide = 0;
                levelFine = false;
                return 0f;
            }

            // PlayerEgo::roll 0xa7c04: once the lean crosses zero (an overshoot) one step of 0.00035, then 0.0002 rad/ms until
            // level; before that 0.00075 upside down, else 0.00035 beyond 0.3, 0.00025 closer.
            int side = lean < 0f ? 1 : lean > 0f ? 2 : levelSide;
            // Remake: upside down the lean's sign is kept from the first frame (with the nose levelled at the same time,
            // LevelPitch, the ship could settle exactly inverted, where the lean flips sign every frame and the roll with it).
            if (upY < 0f && levelSide != 0) { side = levelSide; lean = side == 2 ? Mathf.Max(lean, 1e-4f) : Mathf.Min(lean, -1e-4f); }
            float rate;
            if (levelFine) rate = LevelRateFine;
            else if ((side == 2 && levelSide == 1) || (side == 1 && levelSide == 2)) { rate = LevelRateOvershoot; levelFine = true; }
            else if (upY < 0f) rate = LevelRateUpsideDown;
            else rate = Mathf.Abs(lean) > 0.3f ? LevelRateFar : LevelRateNear;
            levelSide = side;

            float dir = lean > 0f ? 1f : -1f; // roll so that the lean shrinks
            return dir * rate * Mathf.Min(dtMs, LevelMaxDtMs);
        }

        /// <summary>The boost timer alone, for frames another component moves the ship (ShipController.externalControl).</summary>
        public void TickBoost(float dtMs) => UpdateBoost(dtMs);

        void UpdateBoost(float dtMs)
        {
            // Timer counts up every frame; negative values mean "recharging" (PlayerEgo::update).
            int dt = Mathf.RoundToInt(dtMs);
            if (boostTimerMs < 0 && boostTimerMs + dt * 3 > 0) boostTimerMs = 0;
            boostTimerMs += dt;

            if (IsBoosting && boostTimerMs > boostDurationMs)
            {
                IsBoosting = false;
                CurrentSpeed = BaseSpeed;
                boostTimerMs = Data.Cheats.NoBoostCooldown ? 0 : -boostRechargeMs;   // remake debug: no recharge
            }
        }

        public struct FrameResult
        {
            public float pitchDeg, yawDeg, rollDeg;
            public float forwardUnits;
            public float sidePushUnits;
        }
    }
}
