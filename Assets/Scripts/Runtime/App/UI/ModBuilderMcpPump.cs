// ModBuilderMcpPump.cs
// Runs the Mod Builder's MCP server's work on the main thread (ModBuilderMcp.Pump: switching it on and off with the builder,
// the tools' calls, their waits) and its screenshots at the end of a frame. One, kept across scenes (play tests change them),
// made by the builder's screen.

using System;
using System.Collections;
using UnityEngine;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class ModBuilderMcpPump : MonoBehaviour
    {
        public static ModBuilderMcpPump Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("Mod Builder MCP");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ModBuilderMcpPump>();
        }

        void Update() => ModBuilderMcp.Pump();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ModBuilderMcp.Stop();
        }

        void OnApplicationQuit() => ModBuilderMcp.Stop();

        /// <summary>The screen as drawn this frame (the UI included), at most 'maxWidth' wide, as a PNG.</summary>
        public void Capture(int maxWidth, Action<byte[], int, int> done) => StartCoroutine(CaptureAtEnd(maxWidth, done));

        IEnumerator CaptureAtEnd(int maxWidth, Action<byte[], int, int> done)
        {
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            int w = shot.width, h = shot.height;
            if (w > maxWidth)
            {
                h = Mathf.RoundToInt(h * (maxWidth / (float)w));
                w = maxWidth;
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(shot, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Destroy(shot);
                shot = small;
            }
            byte[] png = shot.EncodeToPNG();
            Destroy(shot);
            done(png, w, h);
        }
    }
}
