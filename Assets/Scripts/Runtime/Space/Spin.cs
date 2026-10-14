// Spin.cs
// Constant slow rotation (asteroids, background props).

using UnityEngine;

namespace GoF2Remake.Visuals
{
    public class Spin : MonoBehaviour
    {
        [Tooltip("Degrees per second around each local axis.")]
        public Vector3 degreesPerSecond = new Vector3(0f, 3f, 0f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
