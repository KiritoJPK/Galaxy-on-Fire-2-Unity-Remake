// VrControls.cs
// Remake VR: the cockpit's small side-stick on the right console and the speed handle on the left console (VrCockpit).
// Always there: not held they show the ship's own inputs (the stick tilts with the steering, the handle sits at the
// throttle, moved by the controllers or keys). With the option "VR flight: grab the stick and throttle"
// (Settings.VrGrabControls) they are grabbed with that hand's grip near the handle (12 cm): the stick follows the hand's
// travel from where it grabbed (6 cm = full deflection, forward = nose down, right = yaw right; FlightHud reads Steer
// instead of the touch stick, the drill too) and the hand's twist rolls (45 deg = full); the handle slides along its 28 cm
// slot, back = 0 %, forward = 100 % (ShipController.SetThrottle). Meanwhile the grips aren't the gamepad's shoulders
// (VrPad.GripsAsShoulders: the throttle steps).

using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Vr
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class VrControls : MonoBehaviour
    {
        const float GrabReach = 0.12f, FullDeflection = 0.06f, FullTwist = 45f, MaxTilt = 20f, StickLength = 0.14f;
        static readonly Vector3 StickBase = new Vector3(0.6f, -0.425f, 0.32f);
        static readonly Vector3 HandleFrom = new Vector3(-0.6f, -0.395f, 0.08f), HandleTo = new Vector3(-0.6f, -0.395f, 0.36f);

        /// <summary>The grabbed stick's steering (x yaw right, y nose up), null = not held.</summary>
        public static Vector2? Steer { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Steer = null;

        VrRig rig;
        Transform stickPivot, stickTop, handle;
        MeshRenderer stickGrip, handleKnob;
        Material handleMaterial, handleHeld;
        bool stickHeld, handleHeld2, rightWas, leftWas;
        Vector3 stickGrabHand;
        Quaternion stickGrabRotation;

        public void Init(VrRig owner, Material frame)
        {
            rig = owner;
            handleMaterial = new Material(frame) { name = "VR handle" };
            handleMaterial.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.22f));
            handleHeld = new Material(frame) { name = "VR handle held" };
            handleHeld.SetColor("_BaseColor", new Color(0.85f, 0.55f, 0.15f));
            var parts = new GameObject("VR stick and throttle").transform;
            parts.SetParent(transform, false);
            // The side-stick: a base on the right console, a short shaft turning about its foot, a grip on top.
            Part(PrimitiveType.Cylinder, "Stick base", StickBase, new Vector3(0.07f, 0.012f, 0.07f), frame, parts);
            stickPivot = new GameObject("Stick pivot").transform;
            stickPivot.SetParent(parts, false);
            stickPivot.localPosition = StickBase;
            Part(PrimitiveType.Cylinder, "Stick shaft", new Vector3(0f, StickLength * 0.5f, 0f), new Vector3(0.018f, StickLength * 0.5f, 0.018f), frame, stickPivot);
            stickTop = Part(PrimitiveType.Capsule, "Stick grip", new Vector3(0f, StickLength, 0f), new Vector3(0.035f, 0.045f, 0.035f), handleMaterial, stickPivot);
            stickGrip = stickTop.GetComponent<MeshRenderer>();
            // The speed handle: a slot along the left console and the handle riding in it.
            Part(PrimitiveType.Cube, "Throttle slot", (HandleFrom + HandleTo) * 0.5f - new Vector3(0f, 0.02f, 0f), new Vector3(0.03f, 0.012f, 0.32f), frame, parts);
            // The lever: a short shaft rising out of the slot with a crosswise grip on top; 'handle' rides along the slot.
            handle = new GameObject("Throttle lever").transform;
            handle.SetParent(parts, false);
            handle.localPosition = HandleFrom;
            Part(PrimitiveType.Cylinder, "Throttle shaft", new Vector3(0f, 0.045f, 0f), new Vector3(0.016f, 0.045f, 0.016f), frame, handle);
            var grip = Part(PrimitiveType.Capsule, "Throttle grip", new Vector3(0f, 0.095f, 0f), new Vector3(0.035f, 0.05f, 0.035f), handleMaterial, handle);
            grip.localRotation = Quaternion.Euler(0f, 0f, 90f);   // lying across, for the left hand
            handleKnob = grip.GetComponent<MeshRenderer>();
        }

        static Transform Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void LateUpdate()
        {
            bool grabMode = Settings.VrGrabControls;
            VrPad.GripsAsShoulders = !grabMode;
            var level = FindAnyObjectByType<World.SpaceLevel>();
            var ship = level != null ? level.Player : null;
            if (ship == null) { Release(null); return; }
            bool canGrab = grabMode && VrMode.Headset && rig.RightHand != null && rig.LeftHand != null;
            if (!canGrab) Release(ship);
            bool right = canGrab && VrPad.RightGrip > 0.6f, left = canGrab && VrPad.LeftGrip > 0.6f;

            // The stick.
            if (canGrab)
            {
                var rightLocal = transform.InverseTransformPoint(rig.RightHand.position);
                if (right && !rightWas && Vector3.Distance(rightLocal, StickBase + Vector3.up * StickLength) < GrabReach)
                {
                    stickHeld = true;
                    stickGrabHand = rightLocal;
                    stickGrabRotation = Quaternion.Inverse(transform.rotation) * rig.RightHand.rotation;
                }
                if (!right && stickHeld) { stickHeld = false; ship.SetRoll(0f); Steer = null; }
                if (stickHeld)
                {
                    var d = rightLocal - stickGrabHand;
                    var steer = new Vector2(Mathf.Clamp(d.x / FullDeflection, -1f, 1f), Mathf.Clamp(-d.z / FullDeflection, -1f, 1f));
                    Steer = steer;
                    var now = Quaternion.Inverse(transform.rotation) * rig.RightHand.rotation;
                    float roll = Mathf.DeltaAngle(0f, (Quaternion.Inverse(stickGrabRotation) * now).eulerAngles.z);
                    ship.SetRoll(Mathf.Clamp(-roll / FullTwist, -1f, 1f));
                }
            }
            // Not held: the ship's own steering (controllers, keys) tilts it.
            var tilt = stickHeld && Steer.HasValue ? Steer.Value : Vector2.ClampMagnitude(ship.SteerInput, 1f);
            stickPivot.localRotation = Quaternion.Slerp(stickPivot.localRotation, Quaternion.Euler(-tilt.y * MaxTilt, 0f, -tilt.x * MaxTilt),
                                                        stickHeld ? 1f : Time.unscaledDeltaTime * 12f);
            stickGrip.sharedMaterial = stickHeld ? handleHeld : handleMaterial;

            // The speed handle.
            float t;
            if (canGrab)
            {
                var leftLocal = transform.InverseTransformPoint(rig.LeftHand.position);
                if (left && !leftWas && Vector3.Distance(leftLocal, handle.localPosition + Vector3.up * 0.095f) < GrabReach) handleHeld2 = true;
                if (!left) handleHeld2 = false;
                if (handleHeld2)
                {
                    var axis = HandleTo - HandleFrom;
                    ship.SetThrottle(Mathf.Clamp01(Vector3.Dot(leftLocal - HandleFrom, axis) / axis.sqrMagnitude));
                }
            }
            t = ship.Model != null ? ship.Model.Throttle : 0f;
            handle.localPosition = Vector3.Lerp(HandleFrom, HandleTo, t);
            handleKnob.sharedMaterial = handleHeld2 ? handleHeld : handleMaterial;
            rightWas = right;
            leftWas = left;
        }

        void Release(Flight.ShipController ship)
        {
            if (stickHeld && ship != null) ship.SetRoll(0f);
            stickHeld = handleHeld2 = false;
            Steer = null;
        }

        // Switched off with the cockpit (a cutscene): let go of both.
        void OnDisable()
        {
            var level = stickHeld ? FindAnyObjectByType<World.SpaceLevel>() : null;
            if (level != null && level.Player != null) level.Player.SetRoll(0f);
            stickHeld = handleHeld2 = false;
            Steer = null;
            VrPad.GripsAsShoulders = true;
        }

        void OnDestroy()
        {
            if (handleMaterial != null) Destroy(handleMaterial);
            if (handleHeld != null) Destroy(handleHeld);
        }
    }
}
