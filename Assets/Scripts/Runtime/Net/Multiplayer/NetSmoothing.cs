// NetSmoothing.cs
// Remote objects' motion between network updates (NetPlayer, NetProxy): each received position is kept with its
// arrival time and a velocity estimate; the object is drawn a little ahead along that velocity (at most 250 ms) and eases
// toward it, and snaps when it is far off (a respawn, a teleport).

using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    public sealed class NetSmoothing
    {
        const float Ease = 12f, MaxAhead = 0.25f, SnapDistance = 250f;

        Vector3 position, velocity;
        float stamp;
        bool has, snap = true;

        public void Push(Vector3 p)
        {
            float now = Time.unscaledTime;
            if (has && (p - position).sqrMagnitude > SnapDistance * SnapDistance) { velocity = Vector3.zero; snap = true; }   // a jump, not motion
            else if (has && now - stamp > 0.001f) velocity = Vector3.Lerp(velocity, (p - position) / (now - stamp), 0.5f);
            position = p;
            stamp = now;
            has = true;
        }

        /// <summary>The next Apply puts it straight at the target (shown again, a long way off).</summary>
        public void Snap() => snap = true;

        public void Apply(Transform t, Quaternion rotation)
        {
            if (!has) return;
            var target = position + velocity * Mathf.Min(Time.unscaledTime - stamp, MaxAhead);
            if (snap || (t.position - target).sqrMagnitude > SnapDistance * SnapDistance)
            {
                t.SetPositionAndRotation(target, rotation);
                snap = false;
                return;
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * Ease);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, target, k), Quaternion.Slerp(t.rotation, rotation, k));
        }
    }
}
