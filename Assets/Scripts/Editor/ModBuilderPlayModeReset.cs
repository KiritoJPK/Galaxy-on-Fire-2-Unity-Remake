// ModBuilderPlayModeReset.cs
// Leaving Play mode with a Mod Builder project open: the builder's session is dropped (Modding.ModBuilder.CloseAfterPlayMode),
// and its MCP server and the AI's virtual input devices go. Play mode runs without a domain reload, so the builder's statics
// kept the project as the only active mod and refused saving until the next Play: edit-mode tests run after such a session
// saw 65 ships and failed every save test.

using GoF2Remake.Modding;
using GoF2Remake.UI;
using UnityEditor;

namespace GoF2Remake.EditorTools
{
    [InitializeOnLoad]
    static class ModBuilderPlayModeReset
    {
        static ModBuilderPlayModeReset()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            ModBuilderMcp.Stop();
            McpInput.Uninstall();
            ModBuilder.CloseAfterPlayMode();
        }
    }
}
