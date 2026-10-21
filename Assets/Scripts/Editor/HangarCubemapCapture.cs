// HangarCubemapCapture.cs  (Editor only)
// Modified: the reflection cubemaps from the game as the remake draws it. The original's (Resources/GoF2Reflections)
// are small and blurry, and their faces are in the engine's order (top, the four sides, bottom), which Unity's Auto
// layout reads as +X, -X, +Y, -Y, +Z, -Z: the faces landed on the wrong sides. In Play mode, menu "GoF2/Tools/Capture
// Reflection Cubemap" writes the cubemap of where the player is, as a 1 x 6 vertical strip in Unity's order (the
// importer takes it as a Cubemap with its glossy convolution):
//   - docked in a hangar: the room from the player ship's pivot (its lights, animated layers and fog as they are that
//     frame; the ships, the player's own included, their shadows, the room's bots and the sky outside left out) over
//     cubemap_hangar_<race>.png. The
//     Valkyrie hangars (7, 8) had none: cubemap_hangar_deep_science / _battlestation, which SkyReflection.SetHangar
//     loads when they exist.
//   - in space: the sky alone (the skybox: nebula and stars, no sun, planets or station) over the system's
//     cubemap_skybox_XXX.png (SkyReflection.SystemCubeName), in the sky's own frame (OrbitBuilder.SkyRotation undone,
//     so every station of the system can use it, as the original's).

using System.Collections.Generic;
using System.IO;
using GoF2Remake.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GoF2Remake.EditorTools
{
    public static class HangarCubemapCapture
    {
        /// <summary>Face size: the original's hangar cubemaps are 512 px (a 512 x 3072 strip), the skies' 256.</summary>
        const int FaceSize = 512;
        const string OutDir = ImportSettings.Root + "/Resources/GoF2Reflections";

        // The cubemap faces as seen from the centre (SkyboxBaker's).
        static readonly Vector3[] FaceForward = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] FaceUp = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

        /// <summary>A part of a hangar bot (a transform up to the room named *_bot_*).</summary>
        static bool IsBot(Transform t, Transform room)
        {
            for (; t != null && t != room; t = t.parent)
                if (t.name.Contains("_bot_")) return true;
            return false;
        }

        /// <summary>A ship of the hangar or its shadow (a child of the hangar root named so by StationLevel / HangarShipShadow).</summary>
        static bool IsShip(Transform t, Transform hangarRoot)
        {
            for (; t != null; t = t.parent)
                if (t.parent == hangarRoot)
                    return t.name.StartsWith("Player ship") || t.name.StartsWith("Parked ship") || t.name.StartsWith("Visiting ship")
                        || t.name.StartsWith("Shadow (");
            return false;
        }

        /// <summary>The sky outside the room: the planets and sun (OrbitBuilder's "Backdrop") and the orbit's "Sky layers".</summary>
        static bool IsSky(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == "Backdrop" || t.name == "Sky layers") return true;
            return false;
        }

        [MenuItem("GoF2/Tools/Capture Reflection Cubemap", priority = 400)]
        static void Capture()
        {
            var station = Object.FindFirstObjectByType<StationLevel>();
            if (station != null && station.View == StationView.Hangar && station.HangarRoom != null) { CaptureHangar(station); return; }
            var space = Object.FindFirstObjectByType<SpaceLevel>();
            if (space != null && space.Layout != null) { CaptureSky(space.Layout); return; }
            EditorUtility.DisplayDialog("GoF2", "Enter Play mode and dock in a hangar (its room) or fly in space (the system's sky) first.", "OK");
        }

        static void CaptureHangar(StationLevel level)
        {
            string name = SkyReflection.HangarCubeName(level.HangarIndex);
            if (name == null)
            {
                EditorUtility.DisplayDialog("GoF2", $"Hangar {level.HangarIndex} has no reflection cubemap slot.", "OK");
                return;
            }
            // From the player ship's pivot on the pad (where the reflecting hull is), the room's origin without one.
            Vector3 eye = level.PlayerShip != null ? level.PlayerShip.position : level.HangarRoom.position;

            // The ships (StationLevel's "Player ship", "Parked ship n", "Visiting ship"), their shadows and the room's bots
            // (hangar_terran_bot_*: they fly about, a still copy in the reflection would stay put) hidden for the capture.
            // The sky outside (the planets and sun: Backdrop, the orbit's "Sky layers"; the skybox: a black background) too.
            var hidden = new List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && (IsShip(r.transform, level.HangarRoom.parent) || IsBot(r.transform, level.HangarRoom) || IsSky(r.transform)))
                { r.enabled = false; hidden.Add(r); }
            try
            {
                var main = Camera.main;
                Write(name, Render(eye, Quaternion.identity, CameraClearFlags.SolidColor, main != null ? main.cullingMask : ~0));
            }
            finally
            {
                foreach (var r in hidden) if (r != null) r.enabled = true;
            }
            SkyReflection.SetHangar(level.HangarIndex);   // shown at once (the hangar bound the old one on entering)
            Debug.Log($"GoF2: {name} captured from the hangar ({FaceSize} px faces, from {eye}).");
        }

        static void CaptureSky(OrbitLayout layout)
        {
            string name = SkyReflection.SystemCubeName(layout);
            // The skybox alone (no layer drawn), turned back into the sky's own frame: the sky shader samples its cube at
            // inverse(rotation) * direction, so a face along cube direction d is seen along rotation * d.
            Write(name, Render(Vector3.zero, OrbitBuilder.SkyRotation(layout), CameraClearFlags.Skybox, 0));
            SkyReflection.SetSystem(layout);
            Debug.Log($"GoF2: {name} captured from the sky of system {layout.systemIndex} ({FaceSize} px faces).");
        }

        /// <summary>The six faces from 'eye' (turned by 'frame') into a 1 x 6 vertical strip, +X at the top.</summary>
        static Texture2D Render(Vector3 eye, Quaternion frame, CameraClearFlags clear, int cullingMask)
        {
            var strip = new Texture2D(FaceSize, FaceSize * 6, TextureFormat.RGB24, false);
            var rt = new RenderTexture(FaceSize, FaceSize, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("ReflectionCubemapCamera");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                var main = Camera.main;
                cam.clearFlags = clear;
                cam.backgroundColor = Color.black;
                cam.fieldOfView = 90f;
                cam.aspect = 1f;
                // Close: the game camera's near plane (metres off) cut away the pad right under the ship's pivot, a black
                // face at the bottom of the hangar cubemaps.
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = main != null ? main.farClipPlane : 5000f;
                cam.cullingMask = cullingMask;
                cam.targetTexture = rt;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                // The depth and opaque textures as the game camera has them (and more): a surface that reads them (the
                // hangars' decals and see-through floors) drew black without.
                data.requiresDepthTexture = true;
                data.requiresColorTexture = true;
                var mainData = main != null ? main.GetUniversalAdditionalCameraData() : null;
                if (mainData != null) data.SetRenderer(RendererIndex(mainData));
                camGo.transform.position = eye;

                var prev = RenderTexture.active;
                for (int f = 0; f < 6; f++)
                {
                    camGo.transform.rotation = frame * Quaternion.LookRotation(FaceForward[f], FaceUp[f]);
                    cam.Render();
                    RenderTexture.active = rt;
                    // Vertical strip: +X at the top (ReadPixels counts rows from the bottom).
                    strip.ReadPixels(new Rect(0, 0, FaceSize, FaceSize), 0, (5 - f) * FaceSize);
                }
                RenderTexture.active = prev;
                strip.Apply();
                cam.targetTexture = null;
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            return strip;
        }

        /// <summary>The renderer the game camera uses (its index in the pipeline asset's list; -1 = the default).</summary>
        static int RendererIndex(UniversalAdditionalCameraData data)
        {
            var field = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (int)field.GetValue(data) : -1;
        }

        static void Write(string name, Texture2D strip)
        {
            Directory.CreateDirectory(OutDir);
            string path = $"{OutDir}/{name}.png";
            bool isNew = !File.Exists(path);
            File.WriteAllBytes(path, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!isNew) return;
            // As the original's reflection cubemaps are imported.
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureShape = TextureImporterShape.TextureCube;
            ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;   // glossy
            ti.SetTextureSettings(settings);
            ti.mipmapEnabled = true;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
        }
    }
}
