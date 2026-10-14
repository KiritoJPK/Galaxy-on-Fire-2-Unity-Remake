// HitchLogger.cs
// Development builds only (and the Editor with PlayerPrefs "debug_hitchlog" = 1): measures frame hitches to see whether
// shader / pipeline warm-up is worth doing. Every frame over 33 ms (a 30 fps frame; the game runs at 60 / 120) goes to Application.persistentDataPath/hitches.log
// with the scene, the seconds since it loaded, the campaign step and station, and tags: "load" (within a few frames of a
// scene load, expected), "GC" (a garbage collection ran), "focus" (the app was paused or lost focus, not a hitch).
// It also switches GraphicsSettings.logWhenShaderIsCompiled on, so the player log (and this file, where Unity routes the
// lines through the log callback) shows which shader variants were compiled or uploaded to the driver right before a hitch.
// On quit / pause a summary per scene is appended. Pull it from a phone with
//   adb exec-out run-as com.joppietoppie.gof2remake cat files/hitches.log

using System.Collections.Generic;
using System.IO;
using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GoF2Remake
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class HitchLogger : MonoBehaviour
    {
        const float ThresholdMs = 33f;
        const int LoadFrames = 3;

        static HitchLogger instance;

        readonly object fileLock = new object();
        readonly Dictionary<string, (int count, int loads, float worst, float total)> perScene = new();
        StreamWriter file;
        float sceneLoadTime;
        int sceneLoadFrame = -100, gcCount, skipUntilFrame;
        double lastFrameTime;
        string sceneName = "";

        public static void Install()
        {
            bool on = Application.isEditor ? PlayerPrefs.GetInt("debug_hitchlog", 0) == 1 : Debug.isDebugBuild;
            if (!on || instance != null) return;
            var go = new GameObject("HitchLogger");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitchLogger>();
        }

        void Awake()
        {
            GraphicsSettings.logWhenShaderIsCompiled = true;
            try
            {
                file = new StreamWriter(Path.Combine(GoF2Remake.GamePaths.Data, "hitches.log"), false) { AutoFlush = true };
                file.WriteLine($"# {Application.productName} {Application.version} (Unity {Application.unityVersion}), {System.DateTime.Now:yyyy-MM-dd HH:mm}");
                file.WriteLine($"# {SystemInfo.deviceModel}, {SystemInfo.operatingSystem}");
                file.WriteLine($"# {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio.value:0} Hz");
                file.WriteLine($"# frames over {ThresholdMs} ms: time, frame, ms, scene +seconds, mission / station, tags");
            }
            catch (System.Exception e) { Debug.LogWarning("HitchLogger: no log file: " + e.Message); }
            gcCount = System.GC.CollectionCount(0);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceivedThreaded += OnLog;
            var active = SceneManager.GetActiveScene();
            if (active.isLoaded) OnSceneLoaded(active, LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceivedThreaded -= OnLog;
            WriteSummary();
            lock (fileLock) { file?.Dispose(); file = null; }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            sceneName = scene.name;
            sceneLoadTime = Time.realtimeSinceStartup;
            sceneLoadFrame = Time.frameCount;
            Write($"-- {sceneName} loaded (frame {Time.frameCount})");
        }

        // Unity's "Compiled shader: ..." / "Uploaded shader variant to the GPU driver: ..." lines, when they reach the callback.
        void OnLog(string message, string stack, LogType type)
        {
            if (message.StartsWith("Compiled shader") || message.StartsWith("Uploaded shader") || message.StartsWith("Created pipeline"))
                Write("   " + message.Replace('\n', ' '));
        }

        void OnApplicationFocus(bool focus) => skipUntilFrame = Time.frameCount + 2;

        void OnApplicationPause(bool paused)
        {
            skipUntilFrame = Time.frameCount + 2;
            if (paused) WriteSummary();
        }

        void Update()
        {
            // The real clock: Unity resets deltaTime after a scene load, which hid the load frames.
            double now = Time.realtimeSinceStartupAsDouble;
            float ms = lastFrameTime > 0 ? (float)((now - lastFrameTime) * 1000.0) : 0f;
            lastFrameTime = now;
            int gc = System.GC.CollectionCount(0);
            bool gcRan = gc != gcCount;
            gcCount = gc;
            if (ms < ThresholdMs || Time.frameCount < 3) return;

            // This frame measured the last one: a scene load's own frames still count as "load".
            bool load = Time.frameCount - sceneLoadFrame <= LoadFrames;
            bool focus = Time.frameCount <= skipUntilFrame;
            string tags = (load ? " load" : "") + (gcRan ? " GC" : "") + (focus ? " focus" : "");
            float since = Time.realtimeSinceStartup - sceneLoadTime;
            Write($"{Time.realtimeSinceStartup,8:0.0}s  f{Time.frameCount,-7} {ms,6:0} ms  {sceneName} +{since:0.0}s  " +
                  $"m{Session.CampaignMission} s{Session.StationIndex}{tags}");
            if (focus) return;

            perScene.TryGetValue(sceneName, out var s);
            if (load) s.loads++;
            else { s.count++; s.worst = Mathf.Max(s.worst, ms); s.total += ms; }
            perScene[sceneName] = s;
        }

        void WriteSummary()
        {
            if (perScene.Count == 0) return;
            Write($"== summary at {Time.realtimeSinceStartup:0}s (loads not counted in the hitches)");
            foreach (var pair in perScene)
                Write($"   {pair.Key}: {pair.Value.count} hitches, worst {pair.Value.worst:0} ms, " +
                      $"{pair.Value.total:0} ms in all; {pair.Value.loads} load frames");
        }

        void Write(string line)
        {
            lock (fileLock) file?.WriteLine(line);
            if (!line.StartsWith("   ")) Debug.Log("[Hitch] " + line);
        }
    }
}
