// EndingDrift.cs
// The ending's backdrop (MenuBackground at campaign index 0x2b): the beer and the bra (meshes 0x37d0 / 0x37d1). The
// original adds them as two PlayerStatics at the origin (Level::createScene: position (0, 0, 0), PlayerStatic::update
// moves nothing), i.e. inside the station and far too small to see from the backdrop camera (the beer is ~11 m tall, the
// bra ~26 m wide, the camera 2-5 km out). Remake pick: they drift through space across the camera's view instead,
// tumbling slowly, one after the other, again and again (the camera itself keeps turning).

using UnityEngine;

namespace GoF2Remake.Visuals
{
    public class EndingDrift : MonoBehaviour
    {
        const float PassSeconds = 22f;          // one crossing of the view
        const float MinDistance = 45f, MaxDistance = 70f;   // metres in front of the camera

        Transform cam;
        Vector3 local, localVelocity, spinAxis;   // the path in the camera's frame (the camera keeps turning)
        float spinDegPerSec, waitSeconds, leftSeconds;
        Renderer[] renderers;

        /// <param name="delaySeconds">When its first crossing starts (the second object follows the first).</param>
        public void Setup(Transform camera, float delaySeconds)
        {
            cam = camera;
            waitSeconds = delaySeconds;
            renderers = GetComponentsInChildren<Renderer>(true);
            Show(false);
        }

        void Show(bool on)
        {
            foreach (var r in renderers) if (r != null) r.enabled = on;
        }

        /// <summary>A new crossing: from just off one side of the view to the other, at a random height and distance.</summary>
        void StartPass()
        {
            var c = cam.GetComponent<Camera>();
            float distance = Random.Range(MinDistance, MaxDistance);
            float halfHeight = distance * Mathf.Tan((c != null ? c.fieldOfView : 50f) * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * (c != null ? c.aspect : 16f / 9f);
            float side = Random.value < 0.5f ? -1f : 1f;
            float margin = 20f;   // starts and ends out of view (the objects are up to ~26 m across)
            var start = new Vector3(side * (halfWidth + margin), Random.Range(-0.5f, 0.5f) * halfHeight, distance);
            var end = new Vector3(-side * (halfWidth + margin), Random.Range(-0.5f, 0.5f) * halfHeight, distance + Random.Range(-10f, 10f));
            local = start;
            localVelocity = (end - start) / PassSeconds;
            transform.position = cam.TransformPoint(local);
            transform.rotation = Random.rotation;
            spinAxis = Random.onUnitSphere;
            spinDegPerSec = Random.Range(15f, 35f);
            leftSeconds = PassSeconds;
            Show(true);
        }

        void Update()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;
            if (leftSeconds <= 0f)
            {
                if ((waitSeconds -= dt) > 0f) return;
                StartPass();
                waitSeconds = PassSeconds;   // the next crossing after the other object's
            }
            local += localVelocity * dt;
            transform.position = cam.TransformPoint(local);
            transform.Rotate(spinAxis, spinDegPerSec * dt, Space.World);
            if ((leftSeconds -= dt) <= 0f) Show(false);
        }
    }
}
