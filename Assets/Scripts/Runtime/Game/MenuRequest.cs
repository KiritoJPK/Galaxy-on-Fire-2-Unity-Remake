// MenuRequest.cs
// What the main menu (GoF2.App) should show when it loads next, set by the layers below it before they go back to it: the
// Mod Builder's projects page, a session's Multiplayer panel, the station menu's new game.

namespace GoF2Remake.Data
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class MenuRequest
    {
        /// <summary>A panel to open as soon as the menu loads (MainMenu.OpenPanelOnStart), null = none.</summary>
        public static string OpenPanelOnStart;
    }
}
