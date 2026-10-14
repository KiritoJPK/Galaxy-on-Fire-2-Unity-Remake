// OpaqueTexture.cs
// The refraction effects (the cloak, the emergency system's bubble) read the screen behind them like the original's
// s_texture_base FBO copy. URP only makes that copy (_CameraOpaqueTexture) when the camera asks for it, so the effects
// switch it on for the main camera while they run (reference counted) instead of paying for it all the time.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class OpaqueTexture
    {
        static readonly HashSet<object> users = new HashSet<object>();
        static UniversalAdditionalCameraData camData;
        static CameraOverrideOption previous;

        /// <summary>'user' needs the opaque texture (on) or is done with it (off).</summary>
        public static void Request(object user, bool on)
        {
            if (on ? !users.Add(user) : !users.Remove(user)) return;
            if (on && users.Count == 1)
            {
                var cam = Camera.main;
                camData = cam != null ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
                if (camData == null) return;
                previous = camData.requiresColorOption;
                camData.requiresColorOption = CameraOverrideOption.On;
            }
            else if (!on && users.Count == 0 && camData != null)
            {
                camData.requiresColorOption = previous;
                camData = null;
            }
        }
    }
}
