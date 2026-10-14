using System;
using System.Collections.Generic;
using Unity.Netcode.Editor.Tracing.UI;
using Unity.Netcode.Tracing;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.Editor.Tracing
{
    internal enum FilterPanelVisibility
    {
        AutoOpen,
        UserOpened,
        UserClosed,
    }

    [Serializable]
    internal class TracingColumnFilterEntry
    {
        public string columnName;
        public bool enabled;
        public List<string> hiddenValues = new();
    }

    [Serializable]
    internal class TracingTargetSelectionEntry
    {
        public string assemblyQualifiedName;
        public bool hidden;
        // Components only
        public bool required;
    }

    /// <summary>
    /// Per-user, per-project settings for the Prediction Tracing Tool: the tracing target selection and the UI preferences of its windows
    /// </summary>
    [FilePath("UserSettings/Packages/com.unity.netcode/TracingTool.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TracingToolUserSettings : ScriptableSingleton<TracingToolUserSettings>
    {
        const int k_CurrentVersion = 1;

        static readonly Color k_DefaultPredictedColor = new Color(1f, 165f / 255f, 0f);
        static readonly Color k_DefaultAuthorityColor = new Color(178f / 255f, 178f / 255f, 178f / 255f);
        internal static Color DefaultDifferenceColor => EditorGUIUtility.isProSkin
            ? new Color32(0xFF, 0x53, 0x4A, 0xFF)
            : new Color32(0xB1, 0x0C, 0x0C, 0xFF);

        // Tracing can only capture prediction-loop systems and unmanaged component values, so the
        // Predicted and Unmanaged filters start enabled to hide the rest.
        static List<TracingColumnFilterEntry> DefaultColumnFilters() => new()
        {
            new TracingColumnFilterEntry
            {
                columnName = TracingColumnFilters.k_ColumnTracingPredicted,
                enabled = true,
                hiddenValues = { TracingColumnFilters.k_PredictedFilterValueNotPredicted },
            },
            new TracingColumnFilterEntry
            {
                columnName = TracingColumnFilters.k_ColumnTracingUnmanaged,
                enabled = true,
                hiddenValues = { TracingColumnFilters.k_UnmanagedFilterValueManaged },
            },
        };

#pragma warning disable 414 // written to the settings file for future migrations, never read yet
        [SerializeField] int m_Version = k_CurrentVersion;
#pragma warning restore 414
        [SerializeField] List<TracingTargetSelectionEntry> m_TargetSelection = new();
        static List<string> DefaultDisabledDiffTags() => new()
        {
            nameof(DiffInfo.DiffReasons.DeltaTime),
            nameof(DiffInfo.DiffReasons.BatchedTick),
            nameof(DiffInfo.DiffReasons.PartialTick),
        };

        // Disabled diff tags so tags added in future versions are enabled by default.
        [SerializeField] List<string> m_DisabledDiffTags = DefaultDisabledDiffTags();
        [SerializeField] float m_FuzzyDiffThreshold;
        [SerializeField] bool m_ChangedValuesOnly;
        [SerializeField] FilterPanelVisibility m_FilterPanelVisibility = FilterPanelVisibility.AutoOpen;
        [SerializeField] ReplayRenderingFrequency m_ReplayFrequency = ReplayRenderingFrequency.OncePerFrame;
        [SerializeField] float m_ReplaySpeed = UI.ReplaySpeed.Default;
        [SerializeField] List<TracingColumnFilterEntry> m_ColumnFilters = DefaultColumnFilters();
        [SerializeField] int m_TargetSelectionTab;
        [SerializeField] bool m_SceneVisEnabled = true;
        [SerializeField] bool m_SceneVisShowPredicted = true;
        [SerializeField] bool m_SceneVisShowAuthority = true;
        [SerializeField] bool m_SceneVisShowDifference = true;
        [SerializeField] Color m_SceneVisPredictedColor = k_DefaultPredictedColor;
        [SerializeField] Color m_SceneVisAuthorityColor = k_DefaultAuthorityColor;
        [SerializeField] Color m_SceneVisDifferenceColor;
        [SerializeField] bool m_SceneVisDifferenceColorCustomized;

        void OnEnable()
        {
            m_Version = k_CurrentVersion;
            AssemblyReloadEvents.beforeAssemblyReload += SaveSettings;
            EditorApplication.quitting += SaveSettings;
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SaveSettings;
            EditorApplication.quitting -= SaveSettings;
            SaveSettings();
        }

        void SaveSettings() => Save(true);

        // Test support: snapshots the serialized state so fixtures can restore the user's settings.
        internal string CaptureStateForTests() => JsonUtility.ToJson(this);

        internal void RestoreStateForTests(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
            TracingTargetStore.Load();
        }

        internal void ResetStateToDefaultForTests()
        {
            m_TargetSelection.Clear();
            m_DisabledDiffTags = DefaultDisabledDiffTags();
            m_FuzzyDiffThreshold = 0f;
            m_ChangedValuesOnly = false;
            m_FilterPanelVisibility = FilterPanelVisibility.AutoOpen;
            m_ReplayFrequency = ReplayRenderingFrequency.OncePerFrame;
            m_ReplaySpeed = UI.ReplaySpeed.Default;
            m_ColumnFilters = DefaultColumnFilters();
            m_TargetSelectionTab = 0;
            m_SceneVisEnabled = true;
            m_SceneVisShowPredicted = true;
            m_SceneVisShowAuthority = true;
            m_SceneVisShowDifference = true;
            m_SceneVisPredictedColor = k_DefaultPredictedColor;
            m_SceneVisAuthorityColor = k_DefaultAuthorityColor;
            m_SceneVisDifferenceColor = default;
            m_SceneVisDifferenceColorCustomized = false;
            TracingTargetStore.Load();
        }

        internal IReadOnlyList<TracingTargetSelectionEntry> TargetSelection => m_TargetSelection;

        internal void SetTargetSelection(List<TracingTargetSelectionEntry> entries)
        {
            m_TargetSelection.Clear();
            m_TargetSelection.AddRange(entries);
        }

        internal DiffInfo.DiffReasons SelectedDiffReasons
        {
            get
            {
                var selected = DiffInfo.AllDiffReasons;
                foreach (var name in m_DisabledDiffTags)
                {
                    if (Enum.TryParse<DiffInfo.DiffReasons>(name, out var reason)
                        && (DiffReasonCatalog.SelectableMask & reason) != 0)
                    {
                        selected &= ~reason;
                    }
                }
                return selected;
            }
            set
            {
                m_DisabledDiffTags.Clear();
                foreach (var entry in DiffReasonCatalog.All)
                {
                    if ((value & entry.Reason) == 0)
                        m_DisabledDiffTags.Add(entry.Reason.ToString());
                }
            }
        }

        internal float FuzzyDiffThreshold
        {
            get => float.IsFinite(m_FuzzyDiffThreshold) && m_FuzzyDiffThreshold > 0f ? m_FuzzyDiffThreshold : 0f;
            set => m_FuzzyDiffThreshold = value;
        }

        internal bool ChangedValuesOnly
        {
            get => m_ChangedValuesOnly;
            set => m_ChangedValuesOnly = value;
        }

        internal FilterPanelVisibility FilterPanelVisibility
        {
            get => m_FilterPanelVisibility switch
            {
                FilterPanelVisibility.UserOpened => FilterPanelVisibility.UserOpened,
                FilterPanelVisibility.UserClosed => FilterPanelVisibility.UserClosed,
                _ => FilterPanelVisibility.AutoOpen,
            };
            set => m_FilterPanelVisibility = value;
        }

        internal ReplayRenderingFrequency ReplayFrequency
        {
            get => m_ReplayFrequency switch
            {
                ReplayRenderingFrequency.OncePerTick => ReplayRenderingFrequency.OncePerTick,
                ReplayRenderingFrequency.OncePerSystem => ReplayRenderingFrequency.OncePerSystem,
                _ => ReplayRenderingFrequency.OncePerFrame,
            };
            set => m_ReplayFrequency = value;
        }

        internal float ReplaySpeed
        {
            get => float.IsFinite(m_ReplaySpeed)
                ? Mathf.Clamp(m_ReplaySpeed, UI.ReplaySpeed.Min, UI.ReplaySpeed.Max)
                : UI.ReplaySpeed.Default;
            set => m_ReplaySpeed = value;
        }

        internal int TargetSelectionTab
        {
            get => m_TargetSelectionTab;
            set => m_TargetSelectionTab = value;
        }

        internal IReadOnlyList<TracingColumnFilterEntry> ColumnFilters => m_ColumnFilters;

        internal void SetColumnFilters(List<TracingColumnFilterEntry> entries)
        {
            m_ColumnFilters = entries ?? new List<TracingColumnFilterEntry>();
        }

        internal bool SceneVisEnabled
        {
            get => m_SceneVisEnabled;
            set => m_SceneVisEnabled = value;
        }

        internal bool SceneVisShowPredicted
        {
            get => m_SceneVisShowPredicted;
            set => m_SceneVisShowPredicted = value;
        }

        internal bool SceneVisShowAuthority
        {
            get => m_SceneVisShowAuthority;
            set => m_SceneVisShowAuthority = value;
        }

        internal bool SceneVisShowDifference
        {
            get => m_SceneVisShowDifference;
            set => m_SceneVisShowDifference = value;
        }

        internal Color SceneVisPredictedColor
        {
            get => m_SceneVisPredictedColor;
            set => m_SceneVisPredictedColor = value;
        }

        internal Color SceneVisAuthorityColor
        {
            get => m_SceneVisAuthorityColor;
            set => m_SceneVisAuthorityColor = value;
        }

        internal Color SceneVisDifferenceColor
        {
            get => m_SceneVisDifferenceColorCustomized ? m_SceneVisDifferenceColor : DefaultDifferenceColor;
            set
            {
                m_SceneVisDifferenceColor = value;
                m_SceneVisDifferenceColorCustomized = true;
            }
        }
    }
}
