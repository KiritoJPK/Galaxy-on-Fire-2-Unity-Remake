// SpaceHooksInstaller.cs
// Fills in what the game's rules (GoF2.Game, GameHooks) need from the flight layer: repairing the live ship, resetting the
// key bindings, pruning the hangar shadows' cache. Runs before the first scene, and in the Editor on every load
// (EditorHookInstaller), so edit-mode tools see the same.

using GoF2Remake.Data;
using GoF2Remake.World;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public static class SpaceHooksInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            GameHooks.RepairLiveShip = PlayerHealth.RepairLive;
            GameHooks.ResetControls = GameControls.ResetToDefaults;
            GameHooks.ModsScanned = ShipShadowBaker.PruneCache;
        }
    }
}
