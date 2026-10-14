// FpsCounter.cs
// Remake-only: the frame rate and frame time at the top centre (Options > Graphics "Show FPS", Settings.ShowFps), over
// every scene and menu. Averaged over half a second of real time, so a paused game still counts. Created by Bootstrap and
// kept for the whole run; its own panel (the star map's panel settings, sorted above everything else).

using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class FpsCounter : MonoBehaviour
    {
        const float WindowSeconds = 0.5f;

        static FpsCounter instance;
        /// <summary>Hidden for now (Action Freeze: the screenshot shows the scene only).</summary>
        public static bool Suppressed;
        PanelRenderer panelRenderer;
        PanelSettings runtimePanel;
        VisualTreeAsset emptyTree;
        Label label;
        int frames;
        float elapsed;

        public static void Install()
        {
            if (instance != null) return;
            var assets = StarMapAssets.Load();
            if (assets == null || assets.panelSettings == null) return;
            var go = new GameObject("FpsCounter");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FpsCounter>();
            instance.runtimePanel = Instantiate(assets.panelSettings);
            instance.runtimePanel.sortingOrder = assets.panelSettings.sortingOrder + 100;   // over the HUDs, menus and the map
            instance.runtimePanel.referenceResolution = new Vector2Int(1920, 1080);
            instance.runtimePanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            instance.runtimePanel.match = 1f;
            instance.emptyTree = ScriptableObject.CreateInstance<VisualTreeAsset>();
            var pr = go.AddComponent<PanelRenderer>();
            pr.panelSettings = instance.runtimePanel;
            pr.visualTreeAsset = instance.emptyTree;
            go.SetActive(true);
        }

        void OnEnable()
        {
            panelRenderer = GetComponent<PanelRenderer>();
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
            Settings.Changed += Apply;
        }

        void OnDisable()
        {
            panelRenderer?.UnregisterUIReloadCallback(OnUIReload);
            Settings.Changed -= Apply;
        }

        void OnDestroy()
        {
            if (runtimePanel != null) Destroy(runtimePanel);
            if (emptyTree != null) Destroy(emptyTree);
            if (instance == this) instance = null;
        }

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
            // Top centre: the corners hold the status bars, the cargo readout, the pause button and the touch controls.
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.left = bar.style.right = 0;
            bar.style.top = 2;
            bar.style.alignItems = Align.Center;
            root.Add(bar);
            label = new Label { pickingMode = PickingMode.Ignore };
            var s = label.style;
            s.fontSize = 15;
            s.color = new Color(0.85f, 1f, 0.85f);
            s.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            s.paddingLeft = s.paddingRight = 6;
            s.paddingTop = s.paddingBottom = 2;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 3;
            bar.Add(label);
            Apply();
        }

        void Apply()
        {
            if (label != null) label.style.display = Settings.ShowFps && !Suppressed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Update()
        {
            if (label == null) return;
            var want = Settings.ShowFps && !Suppressed ? DisplayStyle.Flex : DisplayStyle.None;
            if (label.style.display != want) label.style.display = want;
            if (want == DisplayStyle.None) return;
            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < WindowSeconds) return;
            float fps = frames / elapsed;
            label.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0} FPS  {1:0.0} ms", fps, 1000f / Mathf.Max(fps, 0.001f));
            frames = 0;
            elapsed = 0f;
        }
    }
}
