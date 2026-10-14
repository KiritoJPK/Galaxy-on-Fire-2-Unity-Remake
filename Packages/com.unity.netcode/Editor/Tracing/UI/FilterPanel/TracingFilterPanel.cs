using System;
using System.Collections.Generic;
using Unity.Netcode.Tracing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Netcode.Editor.Tracing.UI.FilterPanel
{
    // Which diff filter a TracingFilterPanel.OnFiltersChanged notification is about.
    // Target visibility is owned by TracingTargetStore and reaches the views through its Changed event.
    enum FilterChangeKind
    {
        DiffReasons,
        ChangedValuesOnly,
        FuzzyDiffThreshold,
    }

    /// <summary>
    /// The "Filter options" side panel of the tracing window. Pops out and collapses from the left, like the
    /// editor search window's sidebar, and hosts every view filter in one place: the fuzzy diff threshold,
    /// changed-values-only, the visibility of each diff tag, the visibility of each tracing target.
    /// </summary>
    class TracingFilterPanel : VisualElement
    {
        const string k_PanelUssPath = Constants.Stylesheets + "tracing-filter-panel.uss";
        const string k_FilterRowTemplatePath = Constants.Templates + "tracing-filter-row.uxml";
        const string k_Toggle = "toggle";
        const string k_Name = "name";
        const string k_TracingType = "tracing-type";
        const string k_Info = "info";
        const string k_FoldoutInputUssClass = "unity-toggle__input";
        const string k_GearIconName = "SettingsIcon";

        internal const string k_TextTitle = "Filter Options";
        internal const string k_TextFuzzyThreshold = "Fuzzy Diff Range";
        internal const string k_TextChangedValuesOnly = "Changed Values Only";
        internal const string k_TextDiffTags = "Diff Tags";
        internal const string k_TextHighlightedDiffs = "Highlighted Diffs";
        internal const string k_TextTickFiltering = "Tick-Level Diffs";
        internal const string k_TextTracingTarget = "Tracing Targets";
        internal const string k_TextNameColumn = "Name";
        internal const string k_TooltipHighlightedDiffs =
            "Diffs with a selected tag are highlighted and mark their tick and frame as differing.";
        internal const string k_TooltipTickFiltering =
            "Tags that apply to the whole tick. Deselecting one mutes all diffs in ticks where it occurs.";
        internal const string k_TooltipToggleVisibility = "Temporarily hide this from the filtering view.";
        internal const string k_TooltipManagedComponent = "Managed component values are not captured or diffed.";
        internal const string k_TooltipUntracedTarget =
            "This target has not been traced yet. Resume or restart the tracing to trace it.";
        internal const string k_TooltipRemovedTarget =
            "This target will not be traced anymore, but its recorded traces stay viewable. "
            + "Add it back to the tracing targets to trace it again.";
        internal const string k_TooltipChangeTargets =
            "Change the tracing targets. Changing tracing sources will reset the tracing data.";
        internal const string k_TooltipChangedValuesOnly =
            "Only highlight diffs whose values were actually changed by the traced system.";
        internal const string k_TooltipFuzzyThreshold =
            "Hide component data diffs whose largest per-field difference does not exceed this value.";

        DiffInfo.DiffReasons m_SelectedDiffReasons = DiffInfo.AllDiffReasons;
        // DiffReasons that are present in the current trace (excluding post tracing filters)
        DiffInfo.DiffReasons m_PresentDiffReasons;
        bool m_ChangedValuesOnly;
        float m_FuzzyDiffThreshold;

        readonly VisualTreeAsset m_FilterRowTemplate;
        readonly List<TracingTarget> m_TargetItems = new();
        readonly List<(DiffReasonCatalog.Entry entry, Button toggle, Label chip)> m_TagToggles = new();

        Slider m_ThresholdSlider;
        FloatField m_ThresholdField;
        Toggle m_ChangedValuesOnlyToggle;
        Foldout m_TagsSection;
        Foldout m_TargetsSection;
        Button m_HighlightedTagsEye;
        Button m_TickTagsEye;
        Button m_TargetsEye;
        Button m_TargetsGear;
        Label m_TagsCountLabel;
        Label m_TargetsCountLabel;
        ListView m_TargetListView;

        public event Action<FilterChangeKind> OnFiltersChanged;

        internal DiffInfo.DiffReasons SelectedDiffReasons => m_SelectedDiffReasons;
        internal bool ChangedValuesOnly => m_ChangedValuesOnly;
        internal float FuzzyDiffThreshold => m_FuzzyDiffThreshold;
        internal Foldout TagsSection => m_TagsSection;
        internal Foldout TargetsSection => m_TargetsSection;
        internal Button HighlightedTagsEye => m_HighlightedTagsEye;
        internal Button TickTagsEye => m_TickTagsEye;
        internal Button TargetsEye => m_TargetsEye;
        internal Button TargetsGear => m_TargetsGear;
        internal Label TagsCountLabel => m_TagsCountLabel;
        internal Label TargetsCountLabel => m_TargetsCountLabel;
        internal ListView TargetListView => m_TargetListView;
        internal Button GetDiffTagToggle(int index) => m_TagToggles[index].toggle;
        internal Label GetDiffTagChip(int index) => m_TagToggles[index].chip;

        public TracingFilterPanel()
        {
            LoadFromSettings();
            AddToClassList(TracingFilterPanelUssClasses.Base);
            var panelUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_PanelUssPath);
            if (panelUss != null)
                styleSheets.Add(panelUss);
            m_FilterRowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_FilterRowTemplatePath);

            Add(CreateHeader());

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList(TracingFilterPanelUssClasses.Body);
            body.Add(CreateFuzzyThresholdRow());
            body.Add(CreateChangedValuesOnlyRow());
            body.Add(CreateSeparator());
            body.Add(CreateTagsSection());
            body.Add(CreateSeparator());
            body.Add(CreateTargetsSection());
            Add(body);

            SyncDiffControlsFromState();

            // The target rows follow the live selection, the recording set, and whether traces are shown
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            TracingTargetStore.Changed += OnTargetsChanged;
            TracingDisplayState.Changed += ScheduleRefreshTargets;
            RefreshTargets();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            TracingTargetStore.Changed -= OnTargetsChanged;
            TracingDisplayState.Changed -= ScheduleRefreshTargets;
        }

        void OnTargetsChanged(TracingTargetChanges change) => ScheduleRefreshTargets();

        void ScheduleRefreshTargets() => schedule.Execute(RefreshTargets);

        VisualElement CreateFuzzyThresholdRow()
        {
            var group = new VisualElement { tooltip = k_TooltipFuzzyThreshold };
            group.AddToClassList(TracingFilterPanelUssClasses.ThresholdGroup);

            var label = new Label(k_TextFuzzyThreshold);
            label.AddToClassList(TracingFilterPanelUssClasses.ThresholdLabel);
            group.Add(label);

            var row = new VisualElement();
            row.AddToClassList(TracingFilterPanelUssClasses.ThresholdRow);
            group.Add(row);

            m_ThresholdSlider = new Slider(0f, 1f);
            m_ThresholdSlider.AddToClassList(TracingFilterPanelUssClasses.ThresholdSlider);
            m_ThresholdSlider.RegisterValueChangedCallback(evt =>
                SetThreshold(FuzzyThresholdSlider.PositionToThreshold(evt.newValue), fromSlider: true));
            row.Add(m_ThresholdSlider);

            m_ThresholdField = new FloatField();
            m_ThresholdField.AddToClassList(TracingFilterPanelUssClasses.ThresholdField);
            m_ThresholdField.RegisterValueChangedCallback(evt =>
            {
                var threshold = float.IsFinite(evt.newValue) ? Math.Max(0f, evt.newValue) : 0f;
                if (threshold != evt.newValue)
                    m_ThresholdField.SetValueWithoutNotify(threshold);
                SetThreshold(threshold, fromSlider: false);
            });
            row.Add(m_ThresholdField);
            return group;
        }

        void SetThreshold(float threshold, bool fromSlider)
        {
            if (fromSlider)
                m_ThresholdField.SetValueWithoutNotify(threshold);
            else
                m_ThresholdSlider.SetValueWithoutNotify(FuzzyThresholdSlider.ThresholdToPosition(threshold));

            if (m_FuzzyDiffThreshold == threshold)
                return;
            m_FuzzyDiffThreshold = threshold;
            RaiseFiltersChanged(FilterChangeKind.FuzzyDiffThreshold);
        }

        VisualElement CreateChangedValuesOnlyRow()
        {
            var row = new VisualElement();
            row.AddToClassList(TracingFilterPanelUssClasses.OptionsRow);

            m_ChangedValuesOnlyToggle = new Toggle(k_TextChangedValuesOnly)
            {
                value = m_ChangedValuesOnly,
                tooltip = k_TooltipChangedValuesOnly,
            };
            m_ChangedValuesOnlyToggle.AddToClassList(TracingFilterPanelUssClasses.ChangedValuesToggle);
            m_ChangedValuesOnlyToggle.RegisterValueChangedCallback(evt =>
            {
                m_ChangedValuesOnly = evt.newValue;
                RaiseFiltersChanged(FilterChangeKind.ChangedValuesOnly);
            });
            row.Add(m_ChangedValuesOnlyToggle);
            return row;
        }

        // Divider between the filter groups (threshold & changed values / Diff Tags / Tracing Targets).
        static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.AddToClassList(TracingFilterPanelUssClasses.Separator);
            return separator;
        }

        static VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.AddToClassList(TracingFilterPanelUssClasses.Header);

            var title = new Label(k_TextTitle);
            title.AddToClassList(TracingFilterPanelUssClasses.HeaderLabel);
            header.Add(title);
            return header;
        }

        // A section header line: "> Title ......... visible/total".
        Foldout CreateSection(string titleText, out Label countLabel)
        {
            var section = new Foldout { text = titleText, value = true };
            section.AddToClassList(TracingFilterPanelUssClasses.Section);

            var headerInput = section.Q<Toggle>()?.Q(className: k_FoldoutInputUssClass);

            var spacer = new VisualElement();
            spacer.AddToClassList(TracingFilterPanelUssClasses.SectionHeaderSpacer);
            headerInput?.Add(spacer);

            countLabel = new Label();
            countLabel.AddToClassList(TracingFilterPanelUssClasses.SectionCount);
            headerInput?.Add(countLabel);
            return section;
        }

        // The "Label | show/hide-all eye" line under a section header, above its rows.
        static VisualElement CreateListHeader(string labelText, string tooltip, out Button eye)
        {
            var header = new VisualElement { tooltip = tooltip };
            header.AddToClassList(TracingFilterPanelUssClasses.ListHeader);

            var nameLabel = new Label(labelText);
            nameLabel.AddToClassList(TracingFilterPanelUssClasses.ListHeaderLabel);
            header.Add(nameLabel);

            eye = new Button();
            eye.AddToClassList(TracingFilterPanelUssClasses.RowToggle);
            eye.AddToClassList(TracingFilterPanelUssClasses.SectionEye);
            header.Add(eye);
            return header;
        }

        Foldout CreateTagsSection()
        {
            m_TagsSection = CreateSection(k_TextDiffTags, out m_TagsCountLabel);

            // Two tag groups with independent select/deselect-all toggles.
            m_TagsSection.Add(CreateListHeader(k_TextHighlightedDiffs, k_TooltipHighlightedDiffs, out m_HighlightedTagsEye));
            TracingFilterRowUtility.SetToggleAction(m_HighlightedTagsEye,
                () => OnTagGroupEyeClicked(DiffReasonCatalog.HighlightedMask));
            m_TagsSection.Add(CreateTagGroupList(tickScope: false));

            var tickHeader = CreateListHeader(k_TextTickFiltering, k_TooltipTickFiltering, out m_TickTagsEye);
            tickHeader.AddToClassList(TracingFilterPanelUssClasses.ListHeaderSpaced);
            TracingFilterRowUtility.SetToggleAction(m_TickTagsEye,
                () => OnTagGroupEyeClicked(DiffReasonCatalog.TickScopeMask));
            m_TagsSection.Add(tickHeader);
            m_TagsSection.Add(CreateTagGroupList(tickScope: true));
            return m_TagsSection;
        }

        VisualElement CreateTagGroupList(bool tickScope)
        {
            var list = new VisualElement();
            list.AddToClassList(TracingFilterPanelUssClasses.DiffTagList);

            var rowIndex = 0;
            foreach (var entry in DiffReasonCatalog.All)
            {
                if (DiffReasonCatalog.IsTickScope(entry) != tickScope)
                    continue;
                var row = MakeItemRow();
                row.Q<VisualElement>(k_TracingType).AddToClassList(TracingFilterPanelUssClasses.RowIconHidden);
                row.Q<VisualElement>(k_Info).AddToClassList(TracingFilterPanelUssClasses.RowInfoHidden);
                var chip = row.Q<Label>(k_Name);
                chip.text = entry.Label;
                chip.AddToClassList(TracingFilterPanelUssClasses.DiffTagChip);
                row.tooltip = entry.Tooltip;
                row.EnableInClassList(TracingFilterPanelUssClasses.RowEven, rowIndex % 2 == 0);
                rowIndex++;

                var toggle = row.Q<Button>(k_Toggle);
                TracingFilterRowUtility.SetToggleAction(toggle, () =>
                {
                    m_SelectedDiffReasons ^= entry.Reason;
                    RefreshDiffTagToggle(toggle, chip, entry);
                    RefreshTagsEyes();
                    RaiseFiltersChanged(FilterChangeKind.DiffReasons);
                });
                m_TagToggles.Add((entry, toggle, chip));
                list.Add(row);
            }

            return list;
        }

        bool AllTagsSelected(DiffInfo.DiffReasons groupMask) => (m_SelectedDiffReasons & groupMask) == groupMask;

        void OnTagGroupEyeClicked(DiffInfo.DiffReasons groupMask)
        {
            m_SelectedDiffReasons = AllTagsSelected(groupMask)
                ? m_SelectedDiffReasons & ~groupMask
                : m_SelectedDiffReasons | groupMask;
            RefreshDiffTagToggles();
            RefreshTagsEyes();
            RaiseFiltersChanged(FilterChangeKind.DiffReasons);
        }

        void RefreshDiffTagToggles()
        {
            foreach (var (entry, toggle, chip) in m_TagToggles)
            {
                RefreshDiffTagToggle(toggle, chip, entry);
            }
        }

        void RefreshDiffTagToggle(Button toggle, Label chip, DiffReasonCatalog.Entry entry)
        {
            var selected = (m_SelectedDiffReasons & entry.Reason) != 0;
            TracingFilterRowUtility.SetEyeIcon(toggle, selected);
            // Red border if the reason occurs in the current traces, gray otherwise.
            chip.EnableInClassList(TracingFilterPanelUssClasses.DiffTagChipPresent,
                (m_PresentDiffReasons & entry.Reason) != 0);
            toggle.tooltip = DiffReasonCatalog.SelectionTooltip(entry, selected);
        }

        // Set which DiffReasons are present in the current traces.
        public void SetPresentDiffReasons(DiffInfo.DiffReasons reasons)
        {
            if (m_PresentDiffReasons == reasons)
                return;
            m_PresentDiffReasons = reasons;
            RefreshDiffTagToggles();
        }

        void RefreshTagsEyes()
        {
            RefreshTagGroupEye(m_HighlightedTagsEye, DiffReasonCatalog.HighlightedMask, "highlighted diff tags");
            RefreshTagGroupEye(m_TickTagsEye, DiffReasonCatalog.TickScopeMask, "tick-level diff tags");

            var selected = 0;
            foreach (var entry in DiffReasonCatalog.All)
            {
                if ((m_SelectedDiffReasons & entry.Reason) != 0)
                    selected++;
            }
            if (m_TagsCountLabel != null)
                m_TagsCountLabel.text = $"{selected}/{DiffReasonCatalog.All.Length}";
        }

        void RefreshTagGroupEye(Button eye, DiffInfo.DiffReasons groupMask, string groupName)
        {
            if (eye == null)
                return;
            var allOn = AllTagsSelected(groupMask);
            eye.tooltip = (allOn ? "Deselect all " : "Select all ") + groupName;
            TracingFilterRowUtility.SetEyeIcon(eye, allOn);
        }

        Foldout CreateTargetsSection()
        {
            m_TargetsSection = CreateSection(k_TextTracingTarget, out m_TargetsCountLabel);
            AddTargetsGear();
            m_TargetsSection.Add(CreateListHeader(k_TextNameColumn, null, out m_TargetsEye));
            TracingFilterRowUtility.SetToggleAction(m_TargetsEye, OnAllTargetsEyeClicked);

            m_TargetsSection.Add(CreateTargetListView());
            return m_TargetsSection;
        }

        void AddTargetsGear()
        {
            m_TargetsGear = new Button { tooltip = k_TooltipChangeTargets };
            m_TargetsGear.AddToClassList(TracingFilterPanelUssClasses.SectionGear);
            m_TargetsGear.style.backgroundImage =
                EditorGUIUtility.IconContent(k_GearIconName).image as Texture2D;
            m_TargetsGear.clicked += SelectTracingTargetWindow.ShowWindow;

            var headerInput = m_TargetsSection.Q<Toggle>()?.Q(className: k_FoldoutInputUssClass);
            var titleLabel = headerInput?.Q<Label>(className: Foldout.textUssClassName);
            if (titleLabel != null)
                headerInput.Insert(headerInput.IndexOf(titleLabel) + 1, m_TargetsGear);
        }

        ListView CreateTargetListView()
        {
            var list = new ListView
            {
                itemsSource = m_TargetItems,
                makeItem = MakeItemRow,
                bindItem = (row, i) =>
                {
                    var item = m_TargetItems[i];
                    var icon = row.Q<VisualElement>(k_TracingType);

                    icon.EnableInClassList(TracingFilterPanelUssClasses.IconSystem, item.IsSystem);
                    icon.EnableInClassList(TracingFilterPanelUssClasses.IconComponent, !item.IsSystem);

                    var nameLabel = row.Q<Label>(k_Name);
                    nameLabel.text = item.Id.DisplayName;
                    nameLabel.tooltip = item.Id.FullName;

                    row.EnableInClassList(TracingFilterPanelUssClasses.RowEven, i % 2 == 0);

                    var toggle = row.Q<Button>(k_Toggle);
                    var info = row.Q<VisualElement>(k_Info);

                    if (item.Id.IsManagedComponent)
                    {
                        info.EnableInClassList(TracingFilterPanelUssClasses.RowInfoHidden, true);
                        toggle.EnableInClassList(TracingFilterPanelUssClasses.RowToggleInvisible, false);
                        toggle.EnableInClassList(TracingFilterPanelUssClasses.IconVisible, false);
                        toggle.EnableInClassList(TracingFilterPanelUssClasses.IconHidden, false);
                        toggle.EnableInClassList(TracingFilterPanelUssClasses.IconInfo, true);
                        toggle.pickingMode = PickingMode.Ignore;
                        toggle.focusable = false;
                        toggle.tooltip = string.Empty;
                        row.tooltip = k_TooltipManagedComponent;
                        TracingFilterRowUtility.SetToggleAction(toggle, null);
                        return;
                    }

                    // Rows recycle: undo the managed-badge state before applying the eye states below.
                    toggle.EnableInClassList(TracingFilterPanelUssClasses.IconInfo, false);
                    toggle.pickingMode = PickingMode.Position;
                    toggle.focusable = true;

                    info.EnableInClassList(TracingFilterPanelUssClasses.RowInfoHidden, !item.IsUntraced && !item.IsRemoved);
                    info.tooltip = item.IsUntraced ? k_TooltipUntracedTarget
                        : item.IsRemoved ? k_TooltipRemovedTarget : string.Empty;

                    if (item.IsUntraced)
                    {
                        // Not traced yet, nothing to show or hide, no eye icon needed.
                        toggle.EnableInClassList(TracingFilterPanelUssClasses.RowToggleInvisible, true);
                        row.tooltip = k_TooltipUntracedTarget;
                        TracingFilterRowUtility.SetToggleAction(toggle, null);
                        return;
                    }

                    // Removed targets keep a working eye: their recorded data can still be shown or hidden.
                    toggle.EnableInClassList(TracingFilterPanelUssClasses.RowToggleInvisible, false);
                    row.tooltip = item.IsRemoved ? k_TooltipRemovedTarget : string.Empty;
                    toggle.tooltip = k_TooltipToggleVisibility;
                    TracingFilterRowUtility.SetEyeIcon(toggle, !item.Hidden);
                    TracingFilterRowUtility.SetToggleAction(toggle, () =>
                    {
                        TracingTargetStore.SetHidden(item.Id, !item.Hidden);
                        TracingFilterRowUtility.SetEyeIcon(toggle, !item.Hidden);
                        RefreshTargetsEye();
                    });
                },
                unbindItem = (row, i) => TracingFilterRowUtility.SetToggleAction(row.Q<Button>(k_Toggle), null),
            };

            list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            list.selectionType = SelectionType.None;
            var scrollView = list.Q<ScrollView>();
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(
                evt => list.style.height = evt.newRect.height);
            m_TargetListView = list;
            return list;
        }

        void OnAllTargetsEyeClicked()
        {
            if (HasHiddenTargets)
            {
                TracingTargetStore.SetHidden(m_TargetItems, false);
            }
            else
            {
                // Untraced targets have no data yet and managed components never will: both have nothing to show/hide.
                TracingTargetStore.SetHidden(
                    m_TargetItems.FindAll(item => !item.IsUntraced && !item.Id.IsManagedComponent), true);
            }

            m_TargetListView?.RefreshItems();
            RefreshTargetsEye();
        }

        bool HasHiddenTargets => m_TargetItems.Exists(item => item.Hidden);

        void RefreshTargetsEye()
        {
            if (m_TargetsEye == null)
                return;
            var allOn = !HasHiddenTargets;
            m_TargetsEye.tooltip = allOn ? "Hide all tracing targets" : "Show all tracing targets";
            TracingFilterRowUtility.SetEyeIcon(m_TargetsEye, allOn);

            var visible = 0;
            foreach (var item in m_TargetItems)
            {
                if (!item.Hidden)
                    visible++;
            }
            if (m_TargetsCountLabel != null)
                m_TargetsCountLabel.text = $"{visible}/{m_TargetItems.Count}";
        }

        // Rebuilds the target rows from the store.
        public void RefreshTargets()
        {
            TracingTargetStore.GetPanelRows(m_TargetItems);
            m_TargetListView?.RefreshItems();
            RefreshTargetsEye();
        }

        void RaiseFiltersChanged(FilterChangeKind change)
        {
            SaveToSettings(change);
            OnFiltersChanged?.Invoke(change);
        }

        void LoadFromSettings()
        {
            var settings = TracingToolUserSettings.instance;
            m_SelectedDiffReasons = settings.SelectedDiffReasons;
            m_ChangedValuesOnly = settings.ChangedValuesOnly;
            m_FuzzyDiffThreshold = settings.FuzzyDiffThreshold;
        }

        void SaveToSettings(FilterChangeKind change)
        {
            var settings = TracingToolUserSettings.instance;
            switch (change)
            {
                case FilterChangeKind.DiffReasons:
                    settings.SelectedDiffReasons = m_SelectedDiffReasons;
                    break;
                case FilterChangeKind.ChangedValuesOnly:
                    settings.ChangedValuesOnly = m_ChangedValuesOnly;
                    break;
                case FilterChangeKind.FuzzyDiffThreshold:
                    settings.FuzzyDiffThreshold = m_FuzzyDiffThreshold;
                    break;
            }
        }

        void SyncDiffControlsFromState()
        {
            m_ThresholdSlider?.SetValueWithoutNotify(FuzzyThresholdSlider.ThresholdToPosition(m_FuzzyDiffThreshold));
            m_ThresholdField?.SetValueWithoutNotify(m_FuzzyDiffThreshold);
            m_ChangedValuesOnlyToggle?.SetValueWithoutNotify(m_ChangedValuesOnly);
            RefreshDiffTagToggles();
            RefreshTagsEyes();
        }

        internal VisualElement MakeItemRow()
        {
            var row = m_FilterRowTemplate.Instantiate();
            row.AddToClassList(TracingFilterPanelUssClasses.Row);
            return row;
        }
    }
}
