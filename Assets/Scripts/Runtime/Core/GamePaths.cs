// GamePaths.cs
// The folder everything the game keeps between runs lives in (save games, the mod registry, ship presets, mod projects,
// the mod caches, the events and server folders): Application.persistentDataPath, or a sandbox folder while the edit /
// play mode tests run (Scripts/Tests: TestSandbox), so they never touch the player's own saves and mods.

using UnityEngine;

namespace GoF2Remake
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class GamePaths
    {
        /// <summary>A folder in place of persistentDataPath (tests only); null = the real one.</summary>
        public static string Override;

        /// <summary>Application.persistentDataPath unless a test sandbox replaces it (main thread only, like it).</summary>
        public static string Data => string.IsNullOrEmpty(Override) ? Application.persistentDataPath : Override;
    }
}
