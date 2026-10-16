// EnginePreview.cs
// Remake: a parked ship's engines running for a look at them (the hangar's Customize screen, StationLevel.PreviewEngines):
// EngineDrive reads 'drive' (0 idle .. 1 full throttle .. 2 a full boost) for every engine effect under it (EngineFx,
// EngineFlame, ThrottleGlow) instead of the ship's measured speed, which is 0 on the turntable.

using UnityEngine;

namespace GoF2Remake.Visuals
{
    public class EnginePreview : MonoBehaviour
    {
        public float drive = 1f;
    }
}
