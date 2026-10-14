namespace Unity.Netcode.Editor.Tracing.UI.TracingToolbar
{
    internal static class TracingToolbarUssClasses
    {
        public const string Base = "tracing-toolbar";
        public const string Icon =  Base + "__icon";
        public const string DropdownContainerLabel = Base + "__dropdown-container__label";
        // Replay speed row inside the frequency dropdown
        public const string DropdownSpeedRow = Base + "__dropdown-container__speed-row";
        public const string DropdownSpeedRowLabel = DropdownSpeedRow + "__label";
        public const string DropdownSpeedRowSlider = DropdownSpeedRow + "__slider";
        public const string DropdownSpeedRowField = DropdownSpeedRow + "__field";
        // icons for different states of the toolbar buttons, using BEM naming convention for clarity
        public const string IconPlay = Icon + "__play";
        public const string IconPlayPerTick = IconPlay + "--per-tick";
        public const string IconPlayPerSystem = IconPlay + "--per-system";
        public const string IconPause = Icon + "__pause";
        public const string IconRecord = Icon + "__record";
        public const string IconRecordOff = Icon + "__record--off";
        // Shows/hides the "Filter Options" panel; --active while it is open
        public const string PanelToggle = Base + "__panel-toggle";
        public const string PanelToggleActive = PanelToggle + "--active";
        // Playback controls
        public const string PlaybackControls = Base + "__playback-controls";
        public const string PlaybackControlsIconContainer = PlaybackControls + "__icon-container";
    }
}
