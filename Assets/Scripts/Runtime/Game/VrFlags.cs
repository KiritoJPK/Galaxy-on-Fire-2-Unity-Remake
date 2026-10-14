// VrFlags.cs
// Whether the game runs in VR (VrMode, GoF2.App, decides it before the first scene: -vr / -vrsim): here so the options and
// the flight (Settings, ChaseCamera, MenuCamera, StationLevel) can read it.

namespace GoF2Remake.Data
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class VrFlags
    {
        /// <summary>VR layout on (a headset, or the desktop simulation).</summary>
        public static bool Enabled;
        /// <summary>The desktop simulation (-vrsim): the VR layout without a headset.</summary>
        public static bool Simulated;
        /// <summary>A real headset.</summary>
        public static bool Headset => Enabled && !Simulated;
        /// <summary>The pilot's eye point in the ship's frame (VrCockpit: the chase camera sits there in VR).</summary>
        public static readonly UnityEngine.Vector3 CockpitSeat = new UnityEngine.Vector3(0f, 1f, 0f);
    }
}
