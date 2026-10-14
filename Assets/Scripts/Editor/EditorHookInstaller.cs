// EditorHookInstaller.cs
// The layers' hooks (GameHooks, NetSession: GoF2.Game's view of the layers above it) filled in for the Editor too: the
// runtime installers run only when Play mode starts, and the Editor's tools (the builders, the Mod Builder's data) call
// the game's rules in edit mode.

using UnityEditor;

namespace GoF2Remake.EditorTools
{
    static class EditorHookInstaller
    {
        [InitializeOnLoadMethod]
        static void Install()
        {
            GoF2Remake.Flight.SpaceHooksInstaller.Install();
            GoF2Remake.Multiplayer.NetHooksInstaller.Install();
            GoF2Remake.AppHooksInstaller.Install();
        }
    }
}
