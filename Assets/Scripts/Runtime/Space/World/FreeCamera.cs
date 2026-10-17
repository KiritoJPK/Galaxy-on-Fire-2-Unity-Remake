// FreeCamera.cs
// Remake-only: the photo modes' free camera (Action Freeze in flight, Inspect ship in the hangar; UI.PhotoTools drives it),
// players' suggestion "Everspace-style Action Freeze" (2026-10-17): the camera leaves the orbit around the ship and flies
// anywhere within reach of it, like EVERSPACE 2's photo mode. Yaw about the world's up, pitch short of straight up / down,
// moved in its own frame (forward / right) and along the world's up; kept within 'range' metres of the point it was given
// and above a floor (the hangar's). The lens's roll is UI.PhotoSettings', applied on top by the hosts. Plain C#.

using UnityEngine;

namespace GoF2Remake.World
{
    public sealed class FreeCamera
    {
        public const float MaxPitch = 89f * Mathf.Deg2Rad;

        Vector3 centre;
        float range, floorY;

        public Vector3 Position { get; private set; }
        /// <summary>Radians: + yaw turns right (toward +x from +z), + pitch looks up.</summary>
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public Quaternion Rotation => Quaternion.Euler(-Pitch * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg, 0f);
        public Vector3 Forward => Rotation * Vector3.forward;

        /// <summary>From the camera's pose now (its roll dropped), within 'range' metres of 'centre' and above 'floor'.</summary>
        public void Begin(Vector3 position, Quaternion rotation, Vector3 centre, float range, float floor = float.NegativeInfinity)
        {
            this.centre = centre;
            this.range = Mathf.Max(1f, range);
            floorY = floor;
            var f = rotation * Vector3.forward;
            Yaw = Mathf.Atan2(f.x, f.z);
            Pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)), -MaxPitch, MaxPitch);
            Position = position;
            Clamp();
        }

        /// <summary>Turns: + yaw right, + pitch up (radians).</summary>
        public void Look(float yaw, float pitch)
        {
            Yaw = Mathf.Repeat(Yaw + yaw + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
            Pitch = Mathf.Clamp(Pitch + pitch, -MaxPitch, MaxPitch);
        }

        /// <summary>Moves 'metres' along 'input' (x right, y the world's up, z forward, each -1..1).</summary>
        public void Move(Vector3 input, float metres)
        {
            if (input.sqrMagnitude < 1e-6f || metres <= 0f) return;
            var rot = Rotation;
            var flat = rot * Vector3.right;
            var dir = flat * input.x + Vector3.up * input.y + rot * Vector3.forward * input.z;
            Position += Vector3.ClampMagnitude(dir, 1f) * metres;
            Clamp();
        }

        void Clamp()
        {
            var o = Vector3.ClampMagnitude(Position - centre, range);
            if (centre.y + o.y < floorY)
            {
                // Lifted onto the floor, then drawn in sideways so it stays within the range.
                o.y = floorY - centre.y;
                float side = range * range - o.y * o.y;
                var flat = side > 0f ? Vector2.ClampMagnitude(new Vector2(o.x, o.z), Mathf.Sqrt(side)) : Vector2.zero;
                o = new Vector3(flat.x, o.y, flat.y);
            }
            Position = centre + o;
        }
    }
}
