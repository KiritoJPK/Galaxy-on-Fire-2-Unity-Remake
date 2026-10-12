// ChaseCamera.cs
// Damped third-person chase camera in the spirit of GoF2's TargetFollowCamera.
//
// Recovered facts:
//  * Two damping coefficients (TargetFollowCamera::update 0x186e40): +0x12c eases the camera position, +0x128 the
//    look-at point. Fixed 0.006 / 0.005 (TargetFollowCamera::resetShipHandling) except with the mouse cursor (PlayerEgo
//    ::update, Globals::mouseCursorActivated), where they depend on handling h (setShipHandling):
//      position  0.01h * 0.011 + 0.001        look-at  (1 - 0.01h) * 0.015 + 0.003
//    The exact damping curve (aproximateCooefficients..., a polynomial in dt) is approximated with exponentials.
//  * The camera looks at a point, not along a rotation: +0x14, eased toward the ship's matrix x the target offset (+0x2c),
//    and MatrixGetLookAt(position, that point, the ship's up) from where the camera really is. translateNoUpdate moves
//    both. The dodge (PlayerEgo::updateManeuver) shifts the target offset sideways by -3 x the yaw rate: with the look
//    point only ~34 m ahead of the camera (offsets (0, 600, -1338) / (0, 600, -650) units) the camera turns ~40 deg and
//    keeps the sliding ship in view. (The remake used to put that offset on the camera's position and ease a rotation
//    aimed from where the camera should be: the ship slid out of the frame.)
//  * Boost (MGame::OnUpdate): the vertical FOV grows by 0.35 rad x the boost percentage, and
//    TargetFollowCamera::setBoostPercentage(pct, clamp(boost speed, 2, 8)) jitters the look-at point by
//    pct x n units per axis each frame. The radial blur on top (post effect 0x1400002) isn't reproduced.

using UnityEngine;

namespace GoF2Remake.Flight
{
    public class ChaseCamera : MonoBehaviour
    {
        public ShipController target;

        [Header("Placement (meters, ship local space)")]
        public Vector3 offset = new Vector3(0f, 2.5f, -9f);
        public Vector3 lookOffset = new Vector3(0f, 1f, 6f);

        [Header("Damping (per millisecond coefficients, like the original)")]
        public bool handlingDependent = false; // false = touch-mode constants
        public float positionCoefficient = 0.006f;
        public float rotationCoefficient = 0.005f;

        [Header("Turn offset: camera slides sideways while turning")]
        public float turnSlideMeters = 1.5f;
        [Tooltip("Share of the original's dodge target offset (-3 x the yaw rate, on the look-at point) the camera takes; 1 = as decoded.")]
        public float dodgeLookScale = 1f;
        [Tooltip("Share of the dodge's sideways move the camera takes back each frame (translateNoUpdate(-0.9 x the slide)).")]
        public float dodgeLag = 0.9f;

        [Header("Boost")]
        public float baseFov = 60f;
        [Tooltip("Degrees added at full boost: MGame::OnUpdate's 0.35 rad x the boost percentage.")]
        public float boostFovAdd = 20.05f;

        Camera cam;
        Vector3 slide;
        // TargetFollowCamera+0x14: the eased look-at point (world); invalid = start from where it should be.
        Vector3 lookPoint;
        bool lookValid;
        float shakeMs, shakeUnits, rumble;
        bool wasBoosting;

        /// <summary>TargetFollowCamera::hit / hitSmall: the camera position jitters rnd(2a) - a units per axis per frame
        /// ('ms' 1000 / a 6 when the player is hit, 50 / 2 per own shot).</summary>
        public void Shake(float ms, float amplitudeUnits)
        {
            if (ms >= shakeMs) { shakeMs = ms; shakeUnits = amplitudeUnits; }
        }

        /// <summary>TargetFollowCamera::setRumblePercentage (explosions, each frame): the look-at point jitters by
        /// p * (rnd(100) - 50) units per axis. The strongest caller this frame wins.</summary>
        public void Rumble(float p) => rumble = Mathf.Max(rumble, p);

        /// <summary>TargetFollowCamera on another object (the Liberator; the turret view): the camera sits at
        /// follow.TransformPoint(followOffset) looking at follow.TransformPoint(followLookOffset), world up unless
        /// followUsesUp; 'followRigid' snaps instead of easing. Null = behind the ship.</summary>
        [System.NonSerialized] public Transform follow;
        [System.NonSerialized] public Vector3 followOffset, followLookOffset;
        [System.NonSerialized] public bool followRigid, followUsesUp;
        /// <summary>A constant rumble on top (PlayerEgo::update: 0.2 while the Liberator is steered).</summary>
        [System.NonSerialized] public float constantRumble;
        /// <summary>A level script's look-at camera (CutsceneCamera) holds the camera: the launch / arrival camera leaves it
        /// alone and doesn't hand it back (LevelScript: M0 / M1 never end the fly-in, their scripts own the camera).</summary>
        [System.NonSerialized] public bool scriptCamera;

        void Awake() => cam = GetComponent<Camera>();
        void OnEnable() => lookValid = false;

        void LateUpdate()
        {
            if (target == null) return;
            var ship = target.transform;
            var model = target.Model;
            float dtMs = Time.deltaTime * 1000f * TimeExtender.PlayerFactor;   // TargetFollowCamera gets the player's dt
            // TargetFollowCamera::update 0x186e40 does everything inside `if (0 < dt)`: paused, the camera neither follows,
            // shakes nor rumbles (the shake timer would never run out, the jitter piled up every frame).
            if (dtMs <= 0f) { rumble = 0f; return; }
            if (constantRumble > 0f) rumble = Mathf.Max(rumble, constantRumble);
            // Remake: the same rumble on the controller / phone (Haptics; its own option, not the camera shake's): explosions,
            // the Liberator, the boost below; the boost's start as a pulse.
            bool boosting = model.IsBoosting;
            if (boosting && !wasBoosting) Haptics.Play(Haptics.Boost);
            wasBoosting = boosting;
            float boostRumble = boosting ? model.BoostVisualPercent * Mathf.Clamp(model.CurrentSpeed, 2f, 8f) / 50f : 0f;
            // Remake debug: a heavy hull's engines (ShipController.mass): how hard they work = the gap between the speed and
            // the one the throttle asks for (FlightModel.MoveSpeed eases toward it), plus the boost.
            float mass = target.mass;
            float strain = mass > 0f ? Mathf.Clamp01(Mathf.Abs((model.Braking ? 0f : model.Throttle * model.CurrentSpeed) - model.MoveSpeed) / FlightModel.BaseSpeed) : 0f;
            float engineRumble = mass * (0.06f + 0.25f * strain + 0.3f * model.BoostVisualPercent);
            Haptics.Rumble(Mathf.Max(rumble, Mathf.Max(boostRumble, engineRumble)));
            rumble *= GoF2Remake.Data.Settings.CameraShake;   // the camera shake option
            if (follow != null)
            {
                var fp = follow.TransformPoint(followOffset);
                var fr = Quaternion.LookRotation(follow.TransformPoint(followLookOffset) - fp, followUsesUp ? follow.up : Vector3.up);
                float k = followRigid ? 1f : 1f - Mathf.Exp(-0.01f * dtMs);
                transform.position = Vector3.Lerp(transform.position, fp, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, fr, k);
                if (rumble > 0f)
                {
                    float j = rumble * 0.6f;
                    transform.rotation *= Quaternion.Euler(Random.Range(-j, j), Random.Range(-j, j), 0f);
                    rumble = 0f;
                }
                if (cam != null) cam.fieldOfView = GoF2Remake.Visuals.Aspect.VerticalFov(baseFov, cam.aspect);
                lookValid = false;
                return;
            }

            // VR: the camera is the pilot's seat (Vr.VrCockpit), rigidly with the ship: no lag, slide, shake or boost zoom.
            if (GoF2Remake.Vr.VrMode.Enabled)
            {
                transform.SetPositionAndRotation(ship.TransformPoint(GoF2Remake.Vr.VrCockpit.Seat), ship.rotation);
                rumble = 0f;
                shakeMs = 0f;
                if (cam != null) cam.fieldOfView = GoF2Remake.Visuals.Aspect.VerticalFov(baseFov, cam.aspect);
                lookValid = false;
                return;
            }

            float posK = positionCoefficient, rotK = rotationCoefficient;
            if (handlingDependent)
            {
                float h = model.Handling;
                posK = 0.01f * h * 0.011f + 0.001f;
                rotK = (1f - 0.01f * h) * 0.015f + 0.003f;
            }
            if (mass > 0f) { posK *= Mathf.Lerp(1f, 0.35f, mass); rotK *= Mathf.Lerp(1f, 0.35f, mass); }   // a heavy, unhurried follow

            // Slide opposite to the turn, proportional to turn rate (approximation of the target offset).
            float maxRate = Mathf.Max(1f, 750f * model.Handling / 63f);
            Vector3 targetSlide = new Vector3(-model.YawRate / maxRate * turnSlideMeters,
                                              model.PitchRate / maxRate * turnSlideMeters * 0.5f, 0f);
            // PlayerEgo::updateManeuver: during a dodge the target offset's x is -3 x the yaw rate (game units; the game's
            // +x is the ship's left, the remake's -x), y and z kept: the look-at point leads the slide. The camera's own
            // offset stays (no turn slide meanwhile); LevelScript::resetCamera puts the target offset back at the end.
            Vector3 lookShift = Vector3.zero;
            if (target.Maneuver.Active)
            {
                targetSlide = Vector3.zero;
                lookShift = new Vector3(3f * dodgeLookScale * model.YawRate * target.metersPerUnit, 0f, 0f);
            }
            slide = Vector3.Lerp(slide, targetSlide, 1f - Mathf.Exp(-posK * dtMs));

            Vector3 desiredPos = ship.TransformPoint(offset + slide);
            Vector3 desiredLook = ship.TransformPoint(lookOffset + lookShift);
            // A new start (the chase taking over from a fly-in or another camera) or a jump: the look point starts where the
            // camera looks now, at the ship's distance, and eases onto the ship from there (no turn in one frame).
            if (!lookValid || (desiredLook - lookPoint).sqrMagnitude > 1000f * 1000f)
            {
                lookPoint = transform.position + transform.forward * (desiredLook - transform.position).magnitude;
                lookValid = true;
            }

            transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-posK * dtMs));
            lookPoint = Vector3.Lerp(lookPoint, desiredLook, 1f - Mathf.Exp(-rotK * dtMs));
            // ... and translateNoUpdate(-0.9 x the slide) every frame, the position and the look-at point alike: the camera
            // takes back most of the sideways move, so the ship slides across the view and the camera turns after it.
            var noUpdate = -dodgeLag * target.ManeuverSlide + target.StrafeSlide;   // handleShip: the strafe too
            transform.position += noUpdate;
            lookPoint += noUpdate;
            // MatrixGetLookAt(position, look-at point, the ship's up), from where the camera is.
            var toLook = lookPoint - transform.position;
            if (toLook.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(toLook, ship.up);

            if (shakeMs > 0f)
            {
                shakeMs -= dtMs;
                float a = shakeUnits * target.metersPerUnit * GoF2Remake.Data.Settings.CameraShake;
                transform.position += new Vector3(Random.Range(-a, a), Random.Range(-a, a), Random.Range(-a, a));
            }
            if (boosting)
                rumble = Mathf.Max(rumble, boostRumble * GoF2Remake.Data.Settings.CameraShake);
            if (rumble > 0f)
            {
                // 50 units at the look-at point (about 690 units away) = about 4 degrees.
                var look = lookPoint;
                float j = rumble * 50f * target.metersPerUnit;
                look += new Vector3(Random.Range(-j, j), Random.Range(-j, j), Random.Range(-j, j));
                transform.rotation = Quaternion.LookRotation(look - transform.position, transform.up);
                rumble = 0f;
            }

            if (cam != null)   // a heavy hull's boost pushes the view out less (it gathers speed slowly anyway)
                cam.fieldOfView = GoF2Remake.Visuals.Aspect.VerticalFov(baseFov + boostFovAdd * model.BoostVisualPercent * Mathf.Lerp(1f, 0.5f, mass), cam.aspect);
        }

        /// <summary>Snap behind the ship (use after spawning / undocking).</summary>
        public void Snap()
        {
            if (target == null) return;
            var ship = target.transform;
            transform.position = ship.TransformPoint(offset);
            transform.rotation = Quaternion.LookRotation(ship.TransformPoint(lookOffset) - transform.position, ship.up);
            lookPoint = ship.TransformPoint(lookOffset);
            lookValid = true;
            if (GoF2Remake.Vr.VrMode.Enabled) transform.SetPositionAndRotation(ship.TransformPoint(GoF2Remake.Vr.VrCockpit.Seat), ship.rotation);
            slide = Vector3.zero;
        }
    }
}
