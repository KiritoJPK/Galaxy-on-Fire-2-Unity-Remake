// SkyReflection.cs
// The ambient light of the current skybox (remake): DynamicGI.UpdateEnvironment after a level sets RenderSettings.skybox.
// The environment reflection (what URP's Lit materials reflect) stays the default one a player falls back to. There used to
// be a realtime ReflectionProbe of the sky here, but both quality levels have Realtime Reflection Probes off, so it never
// rendered and only left an empty probe around every level (rendered, it would reflect the black space sky anyway).

using UnityEngine;

namespace GoF2Remake.World
{
    public static class SkyReflection
    {
        /// <summary>The ambient light from RenderSettings.skybox (call after changing it).</summary>
        public static void Update()
        {
            DynamicGI.UpdateEnvironment();
        }
    }
}
