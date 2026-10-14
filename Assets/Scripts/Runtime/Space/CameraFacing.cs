// CameraFacing.cs
// A pivot that takes the camera's rotation every frame (AEGeometry::setDirection(camera dir, camera up), like
// Explosion::render does for an explosion's alpha billboard); the part inside keeps animating in its own local space.

using UnityEngine;

namespace GoF2Remake.Flight
{
    public class CameraFacing : MonoBehaviour
    {
        /// <summary>Puts 'part' in a new camera-facing pivot at its place (same parent, same local transform inside).</summary>
        public static CameraFacing Wrap(Transform part)
        {
            if (part == null) return null;
            var pivot = new GameObject(part.name + " (faces camera)").transform;
            pivot.SetParent(part.parent, false);
            pivot.localPosition = part.localPosition;
            pivot.localScale = Vector3.one;
            part.SetParent(pivot, false);
            part.localPosition = Vector3.zero;
            var f = pivot.gameObject.AddComponent<CameraFacing>();
            f.LateUpdate();
            return f;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
