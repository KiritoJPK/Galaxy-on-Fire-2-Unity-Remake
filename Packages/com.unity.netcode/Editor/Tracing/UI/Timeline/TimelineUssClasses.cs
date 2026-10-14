namespace Unity.Netcode.Editor.Tracing.UI.Timeline
{
    static class TimelineUssClasses
    {
        public const string Base = "timeline";
        public const string Grow = Base + "--grow";
        public const string LoadingSpinnerContainer = Base + "__loading-spinner-container";
        public const string LoadingSpinner = Base + "__loading-spinner";
        public const string FrameSelector = Base + "__frame-selector";
        public const string FrameSelectorBackground = FrameSelector + "__background";
        public const string FrameSelectorFrameMarker = FrameSelector + "__frame-marker";
        public const string FrameSelectorFrameMarkerInactive = FrameSelectorFrameMarker + "--inactive";
        public const string FrameSelectorRuler = FrameSelector + "__ruler";
        public const string FrameSelectorRulerLine = FrameSelector + "__ruler-line";
        public const string FrameSelectorRulerLineTall = FrameSelectorRulerLine + "--tall";
        public const string FrameSelectorBandTag = FrameSelector + "__band-tag";
        public const string FrameSelectorRulerTag = FrameSelector + "__ruler-tag";
        public const string FrameSelectorSnapshotBand = FrameSelector + "__snapshot-band";
        public const string FrameSelectorSnapshotMarker = FrameSelector + "__snapshot-marker";
        public const string FrameSelectorSnapshotMarkerSelected = FrameSelectorSnapshotMarker + "--selected";
        public const string FrameSelectorSnapshotTag = FrameSelector + "__snapshot-tag";
        public const string FrameSelectorDraggerPin = FrameSelector + "__dragger-pin";
        public const string TickSelector = Base + "__tick-selector";
        public const string TickSelectorSelected = TickSelector + "--selected";
        public const string TickSelectorButtonGroupButton = TickSelector + "__button-group-button";
        public const string TickSelectorButtonGroupButtonDiff = TickSelectorButtonGroupButton + "--diff";
        public const string ReplayIndicator = FrameSelector + "__replay-indicator";
    }
}
