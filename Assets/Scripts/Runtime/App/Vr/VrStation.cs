// VrStation.cs
// Remake VR, docked: what the pointer does past the UI (VrRig.WorldPointer: not over a UI element of the floating screen).
//   Bar      a visitor under the laser (a capsule from the feet to the head, 0.45 m) shows the laser on them; the trigger
//            (the simulation: a click) opens their chat (StationLevel.VisitorPicked, the lounge's own OpenChat).
//   Hangar   (a headset) the laser on the player's ship and the grip held grabs it: pulling the hand right or left turns it
//            (4 rad per metre along the head's right, like the touch drag: right = the original's +dx), letting go keeps it
//            turning (StationLevel.FlingShip, the turntable's own slow-down).
// StationLevel itself places the logical camera for VR: standing on the hangar floor beside the pad facing the ship, standing
// in the bar at the original's view point, no drift or sway, the visitors turned to the head.

using GoF2Remake.World;
using UnityEngine;

namespace GoF2Remake.Vr
{
    [DefaultExecutionOrder(10001)]
    public sealed class VrStation : MonoBehaviour
    {
        const float VisitorRadius = 0.45f, TurnPerMetre = 4f;

        VrRig rig;
        StationLevel level;
        bool grabbing, gripWasDown;
        Vector3 lastHand;
        float turnVelocity;

        public void Init(VrRig owner)
        {
            rig = owner;
            level = FindAnyObjectByType<StationLevel>();
        }

        void LateUpdate()
        {
            if (level == null || rig == null) return;
            if (level.View == StationView.Lounge) Lounge(rig.WorldPointer);
            else Hangar(rig.WorldPointer);
        }

        void Lounge(Ray? pointer)
        {
            grabbing = false;
            if (!pointer.HasValue || level.IntroPlaying) return;
            var ray = pointer.Value;
            int best = -1;
            float bestAlong = float.MaxValue;
            Vector3 bestPoint = default;
            for (int i = 0; i < level.VisitorAgentCount; i++)
            {
                var feet = level.VisitorFeet(i);
                var head = level.VisitorHead(i);
                if (!RayCapsule(ray, feet, head, VisitorRadius, out float along)) continue;
                if (along < bestAlong) { bestAlong = along; best = i; bestPoint = ray.GetPoint(along); }
            }
            if (best < 0) return;
            rig.ShowLaserTo(bestPoint);
            if (rig.SelectPressed) level.VisitorPicked?.Invoke(best);
        }

        void Hangar(Ray? pointer)
        {
            var ship = level.PlayerShip;
            bool grip = VrMode.Headset && VrPad.RightGrip > 0.6f;
            bool gripPressed = grip && !gripWasDown;
            gripWasDown = grip;
            if (ship == null || level.PlayerFlying || !VrMode.Headset) { grabbing = false; return; }
            var hand = rig.RightHandPosition;
            if (grabbing)
            {
                if (!grip)
                {
                    grabbing = false;
                    level.FlingShip(turnVelocity);
                    return;
                }
                var right = Vector3.ProjectOnPlane(rig.Eye.transform.right, Vector3.up).normalized;
                float turn = Vector3.Dot(hand - lastHand, right) * TurnPerMetre;
                lastHand = hand;
                if (turn != 0f) level.RotateShip(turn);
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                turnVelocity = Mathf.Lerp(turnVelocity, turn / dt, 0.5f);
                rig.ShowLaserTo(ShipCentre(ship, out _));
                return;
            }
            if (!pointer.HasValue) return;
            var centre = ShipCentre(ship, out float radius);
            if (!RaySphere(pointer.Value, centre, radius, out float distance)) return;
            rig.ShowLaserTo(pointer.Value.GetPoint(distance));
            if (gripPressed)
            {
                grabbing = true;
                lastHand = hand;
                turnVelocity = 0f;
            }
        }

        /// <summary>The ship's bounds (its renderers, not particles) as a sphere.</summary>
        static Vector3 ShipCentre(Transform ship, out float radius)
        {
            var bounds = new Bounds(ship.position, Vector3.zero);
            bool any = false;
            foreach (var r in ship.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            radius = Mathf.Max(1f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f);
            return bounds.center;
        }

        static bool RaySphere(Ray ray, Vector3 centre, float radius, out float distance)
        {
            distance = 0f;
            var oc = ray.origin - centre;
            float b = Vector3.Dot(oc, ray.direction), c = oc.sqrMagnitude - radius * radius;
            float h = b * b - c;
            if (h < 0f) return false;
            distance = -b - Mathf.Sqrt(h);
            if (distance < 0f) distance = -b + Mathf.Sqrt(h);
            return distance > 0f;
        }

        /// <summary>The ray passes within 'radius' of the segment a..b; 'along' = the distance along the ray there.</summary>
        static bool RayCapsule(Ray ray, Vector3 a, Vector3 b, float radius, out float along)
        {
            along = 0f;
            var u = ray.direction;
            var v = b - a;
            var w = ray.origin - a;
            float A = Vector3.Dot(u, u), B = Vector3.Dot(u, v), C = Vector3.Dot(v, v), D = Vector3.Dot(u, w), E = Vector3.Dot(v, w);
            float denom = A * C - B * B;
            float sc, tc;
            if (denom < 1e-6f) { sc = 0f; tc = C > 0f ? E / C : 0f; }
            else { sc = (B * E - C * D) / denom; tc = (A * E - B * D) / denom; }
            tc = Mathf.Clamp01(tc);
            sc = Mathf.Max(0f, Vector3.Dot(a + v * tc - ray.origin, u) / A);
            var closest = ray.origin + u * sc;
            var onSegment = a + v * tc;
            along = sc;
            return (closest - onSegment).sqrMagnitude <= radius * radius && sc > 0f;
        }
    }
}
