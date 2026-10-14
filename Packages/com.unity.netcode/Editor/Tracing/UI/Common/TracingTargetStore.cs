using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Netcode.Tracing;
using UnityEditor;

namespace Unity.Netcode.Editor.Tracing.UI
{
    // Which parts of the target state a <see cref="TracingTargetStore.Changed"/> notification is about.
    [Flags]
    internal enum TracingTargetChanges
    {
        None = 0,
        // Targets were selected or deselected.
        Selection = 1 << 0,
        // A component switched between required and optional.
        Requirement = 1 << 1,
        // The set of targets the recorded was set, expanded or reset.
        Recording = 1 << 2,
        // A target was hidden from or shown in the views.
        Visibility = 1 << 3,
    }

    /// <summary>
    /// A tracing target and everything the tool need to tracks about it.
    /// Created and mutated by <see cref="TracingTargetStore"/> only; the views only read it.
    /// </summary>
    internal class TracingTarget
    {
        public TracingTargetId Id { get; }

        public bool Selected { get; internal set; }

        // Has been traced by the current recording
        public bool Recorded { get; internal set; }

        // Components only
        public bool Required { get; internal set; }

        public bool Hidden { get; internal set; }

        public TracingTarget(TracingTargetId id) => Id = id;

        public bool IsSystem => Id.IsSystem;

        // Selected after the currently displayed recording was paused, has not been traced yet. GhostInstance is always traced.
        public bool IsUntraced => Selected && !Recorded && !Id.IsGhostInstance && TracingDisplayState.IsShowingTraces;

        // Deselected after being traced and the currently displayed recording still contains it.
        public bool IsRemoved => Recorded && !Selected;
    }

    /// <summary>
    /// The single source of truth for tracing targets: the user's selection (persisted through
    /// <see cref="TracingToolUserSettings"/>), the set of currently recorded targets, and each target's
    /// visibility in the filter panel. Every consumer reads a filtered view of the one list kept here and listens to
    /// the one <see cref="Changed"/> event.
    /// The store rebuilds the runtime target sets <see cref="UnmanagedConfig"/>
    /// on every selection or requirement change, and again on play mode entry, which disposes them.
    /// </summary>
    internal static class TracingTargetStore
    {
        // List to keep the selection order: the filter panel rows, the picker's Selected tab and the persisted selection
        // all follow it. A deselected target keeps its slot while its recording is displayed.
        static readonly List<TracingTarget> s_Targets = new();
        static readonly Dictionary<TracingTargetId, TracingTarget> s_TargetsById = new();

        public static event Action<TracingTargetChanges> Changed;

        public static IReadOnlyList<TracingTarget> Targets => s_Targets;

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            Load();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && !TracingDataAccess.Config.Data.Initialized)
                    PushToRuntimeConfig();
            };
        }

        // Replaces the selection with the persisted one. Types that no longer exist, or are no longer registered
        // components, are dropped so the runtime push never sees them.
        public static void Load()
        {
            s_Targets.Clear();
            s_TargetsById.Clear();
            var registeredComponents = new HashSet<Type>();
            TracingTargetTypes.CollectRegisteredComponentTypes(registeredComponents);
            foreach (var entry in TracingToolUserSettings.instance.TargetSelection)
            {
                if (!TracingTargetId.TryParse(entry.assemblyQualifiedName, out var id) || s_TargetsById.ContainsKey(id))
                    continue;
                // A system registers itself with the TypeManager on first use; a component must already be registered.
                if (!id.IsSystem && !registeredComponents.Contains(id.Type))
                    continue;
                var target = Add(id);
                target.Selected = true;
                target.Required = !id.IsSystem && entry.required;
                target.Hidden = entry.hidden;
            }

            Commit(TracingTargetChanges.Selection | TracingTargetChanges.Requirement
                | TracingTargetChanges.Recording | TracingTargetChanges.Visibility);
        }

        public static bool TryGet(TracingTargetId id, out TracingTarget target)
        {
            if (id.IsValid)
                return s_TargetsById.TryGetValue(id, out target);
            target = null;
            return false;
        }

        public static bool IsSelected(Type type) => TryGet(TracingTargetId.Of(type), out var target) && target.Selected;

        public static bool IsRequired(Type componentType)
            => TryGet(TracingTargetId.Of(componentType), out var target) && target.Selected && target.Required;

        public static bool IsHidden(TracingTargetId id) => TryGet(id, out var target) && target.Hidden;

        // True if the views skip a target's diffs: hidden by its eye, or GhostInstance when it is not selected
        public static bool IsHiddenInViews(TracingTargetId id) =>
            TryGet(id, out var target) ? target.Hidden : id.IsGhostInstance;

        public static int GetSelectedCount()
        {
            CountSelected(out var systems, out var components);
            return systems + components;
        }

        public static void CountSelected(out int systems, out int components)
        {
            systems = 0;
            components = 0;
            foreach (var target in s_Targets)
            {
                if (!target.Selected)
                    continue;
                if (target.IsSystem)
                    systems++;
                else
                    components++;
            }
        }

        // A valid set of targets needs at least one system and one component.
        // GhostInstance is only in this store when explicitly selected.
        public static bool HasValidSelection()
        {
            CountSelected(out var systems, out var components);
            return systems > 0 && components > 0;
        }

        public static void SetSelected(Type type, bool selected)
        {
            Commit(ApplySelected(TracingTargetId.Of(type), selected));
        }

        // Batch set (select/deselect all)
        public static void SetSelected(IEnumerable<Type> types, bool selected)
        {
            var change = TracingTargetChanges.None;
            foreach (var type in types)
            {
                change |= ApplySelected(TracingTargetId.Of(type), selected);
            }

            Commit(change);
        }

        static TracingTargetChanges ApplySelected(TracingTargetId id, bool selected)
        {
            if (!id.IsValid)
                return TracingTargetChanges.None;

            s_TargetsById.TryGetValue(id, out var target);
            if (selected)
            {
                if (target is { Selected: true })
                    return TracingTargetChanges.None;
                target ??= Add(id);
                target.Selected = true;
                var change = TracingTargetChanges.Selection;
                if (!target.Recorded && TracingWindowUtility.IsTracingRunning)
                {
                    target.Recorded = true;
                    change |= TracingTargetChanges.Recording;
                }
                return change;
            }

            if (target is not { Selected: true })
                return TracingTargetChanges.None;
            target.Selected = false;
            target.Required = false;
            // GhostInstance is always traced, so it never lingers as a removed target.
            if (!target.Recorded || id.IsGhostInstance)
                Remove(target);
            return TracingTargetChanges.Selection;
        }

        // Components only. Marking a component required or optional selects it if it wasn't already.
        public static void SetRequired(Type componentType, bool required)
        {
            var id = TracingTargetId.Of(componentType);
            if (!id.IsValid || id.IsSystem)
                return;
            var change = ApplySelected(id, true);
            var target = s_TargetsById[id];
            if (target.Required != required)
            {
                target.Required = required;
                change |= TracingTargetChanges.Requirement;
            }
            Commit(change);
        }

        public static void SetHidden(TracingTargetId id, bool hidden)
        {
            if (TryGet(id, out var target))
                Commit(ApplyHidden(target, hidden));
        }

        // Batch set (select/deselect all)
        public static void SetHidden(IEnumerable<TracingTarget> targets, bool hidden)
        {
            var change = TracingTargetChanges.None;
            foreach (var target in targets)
            {
                change |= ApplyHidden(target, hidden);
            }

            Commit(change);
        }

        static TracingTargetChanges ApplyHidden(TracingTarget target, bool hidden)
        {
            if (target.Hidden == hidden)
                return TracingTargetChanges.None;
            target.Hidden = hidden;
            return TracingTargetChanges.Visibility;
        }

        // The recording that starts now traces exactly the current selection.
        public static void CaptureRecording()
        {
            for (var i = s_Targets.Count - 1; i >= 0; i--)
            {
                var target = s_Targets[i];
                if (target.Selected)
                    target.Recorded = true;
                else // Any target that was removed but still displayed is now completely gone
                    Remove(target);
            }

            Commit(TracingTargetChanges.Recording);
        }

        // A resumed recording keeps its recorded targets and traces the current selection too.
        public static void MergeRecording()
        {
            foreach (var target in s_Targets)
            {
                target.Recorded |= target.Selected;
            }

            Commit(TracingTargetChanges.Recording);
        }

        /// <summary>
        /// The filter panel's rows: the selected targets with data,
        /// then the removed ones while their recording is displayed,
        /// then not yet traced ones.
        /// </summary>
        public static void GetPanelRows(List<TracingTarget> rows)
        {
            rows.Clear();
            foreach (var target in s_Targets)
            {
                if (target.Selected && !target.IsUntraced)
                    rows.Add(target);
            }

            if (TracingDisplayState.IsShowingTraces || TracingWindowUtility.IsTracingRunning)
            {
                foreach (var target in s_Targets)
                {
                    if (target.IsRemoved)
                        rows.Add(target);
                }
            }

            foreach (var target in s_Targets)
            {
                if (target.IsUntraced)
                    rows.Add(target);
            }
        }

        // Copies the selection into the runtime config. GhostInstance is skipped: the backend always traces it.
        public static void PushToRuntimeConfig()
        {
            TypeManager.Initialize();
            ref var config = ref TracingDataAccess.Config.Data;
            if (config.Initialized)
                config.ResetTracingTargets();

            foreach (var target in s_Targets)
            {
                if (!target.Selected || target.Id.IsGhostInstance)
                    continue;
                if (target.IsSystem)
                    config.AddSystemTypeToTrace(TypeManager.GetSystemTypeIndex(target.Id.Type));
                else if (target.Required)
                    config.AddRequiredTypeToTrace(ComponentType.ReadWrite(target.Id.Type));
                else
                    config.AddOptionalTypeToTrace(ComponentType.ReadWrite(target.Id.Type));
            }
        }

        static TracingTarget Add(TracingTargetId id)
        {
            var target = new TracingTarget(id);
            s_Targets.Add(target);
            s_TargetsById.Add(id, target);
            return target;
        }

        static void Remove(TracingTarget target)
        {
            s_Targets.Remove(target);
            s_TargetsById.Remove(target.Id);
        }

        static void Commit(TracingTargetChanges change)
        {
            if (change == TracingTargetChanges.None)
                return;

            // Does the change need to be persisted
            const TracingTargetChanges persisted = TracingTargetChanges.Selection | TracingTargetChanges.Requirement | TracingTargetChanges.Visibility;
            if ((change & persisted) != 0)
                Save();

            if ((change & (TracingTargetChanges.Selection | TracingTargetChanges.Requirement)) != 0)
                PushToRuntimeConfig();

            Changed?.Invoke(change);
        }

        static void Save()
        {
            var entries = new List<TracingTargetSelectionEntry>();
            foreach (var target in s_Targets)
            {
                if (!target.Selected)
                    continue;
                entries.Add(new TracingTargetSelectionEntry
                {
                    assemblyQualifiedName = target.Id.Key,
                    required = target.Required,
                    hidden = target.Hidden,
                });
            }

            TracingToolUserSettings.instance.SetTargetSelection(entries);
        }
    }
}
