// ModelViewer.cs
// The Mod Builder MCP server's eyes on a model (ModBuilderMcp's view_model): any model (a game assembly, a project ship or
// station, a .glb) rendered off-screen from named angles into one contact sheet, without touching what the game shows. Like
// Modding.ShipIconMaker: a stage far below the scene on the item window's layer (30), orthographic cameras rendering into a
// texture, the scene's lights, ambient and fog kept off that layer meanwhile, two lights of its own that follow each view so
// every side is lit; a transparent background from a render on black and one on white. On top: each hardpoint as a coloured
// dot with its tag ("P1", "E2": primary red, secondary amber, turret cyan, engine violet, ModBuilderGizmo's colours), hollow
// where the hull hides it (a ray against MeshColliders on the model's readable meshes), and each tile's name, in a small
// bitmap font of its own. Plain C#; renders on the main thread.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Visuals;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // read-only tables
    public static class ModelViewer
    {
        const int Layer = 30, Super = 2;
        static readonly Vector3 Stage = new Vector3(0f, -90000f, -60000f);   // away from the icon maker's and the item window's

        /// <summary>The named views: where the camera stands, in the model's own frame (Unity: +z the nose, -x the pilot's
        /// left: the models are mirrored), and its up.</summary>
        public static readonly (string name, Vector3 from, Vector3 up)[] Angles =
        {
            ("front", Vector3.forward, Vector3.up),
            ("back", Vector3.back, Vector3.up),
            ("left", Vector3.left, Vector3.up),
            ("right", Vector3.right, Vector3.up),
            ("top", Vector3.up, Vector3.forward),
            ("bottom", Vector3.down, Vector3.forward),
            ("threequarter", new Vector3(-0.75f, 0.5f, 0.6f), Vector3.up),
            ("rear", new Vector3(0.75f, 0.45f, -0.6f), Vector3.up),
        };

        public static readonly string[] DefaultAngles = { "left", "top", "front", "threequarter" };

        public struct Marker
        {
            /// <summary>In the model root's own space (Unity units).</summary>
            public Vector3 local;
            public int type;
            public string label;
        }

        public sealed class Options
        {
            public string[] angles = DefaultAngles;
            public int tile = 512;
            /// <summary>"grey" (default), "dark", "light", "transparent".</summary>
            public string background = "grey";
            /// <summary>The engine glows and flames drawn too.</summary>
            public bool glow;
            public List<Marker> markers;
        }

        /// <summary>The contact sheet of 'source' (a prefab, or an object of the scene when 'isInstance': it is moved onto the
        /// stage for the render and back); 'info' its size and what was drawn. Null (and 'error') when nothing could be drawn.</summary>
        public static Texture2D Render(GameObject source, Options o, bool isInstance, out string info, out string error)
        {
            info = error = null;
            if (source == null) { error = "No model."; return null; }
            var angles = (o.angles == null || o.angles.Length == 0 ? DefaultAngles : o.angles).Select(a => a.Trim().ToLowerInvariant()).ToList();
            foreach (var a in angles) if (!Angles.Any(x => x.name == a)) { error = $"Unknown angle '{a}': {string.Join(", ", Angles.Select(x => x.name))}."; return null; }
            int tile = Mathf.Clamp(o.tile, 128, 1024);
            bool transparent = o.background == "transparent";
            Color bg = o.background == "dark" ? new Color(0.06f, 0.07f, 0.09f) : o.background == "light" ? new Color(0.85f, 0.86f, 0.88f) : new Color(0.32f, 0.33f, 0.36f);

            var stage = new GameObject("Model viewer stage");
            stage.transform.position = Stage;
            var rt = new RenderTexture(tile * Super, tile * Super, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var lights = new List<(Light light, int mask)>();
            var ambientMode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            bool fog = RenderSettings.fog;
            Transform oldParent = null;
            Vector3 oldPos = default;
            Quaternion oldRot = default;
            var oldLayers = new List<(GameObject go, int layer)>();
            GameObject go = null;
            try
            {
                if (isInstance)
                {
                    go = source;
                    oldParent = go.transform.parent; oldPos = go.transform.position; oldRot = go.transform.rotation;
                    go.transform.SetParent(stage.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    go = Object.Instantiate(source, stage.transform, false);
                    go.transform.localPosition = Vector3.zero;
                }
                go.SetActive(true);
                var asm = go.GetComponent<AssembledObject>();
                if (asm != null && !isInstance)
                {
                    asm.SetPlayerVariant(true);
                    if (!o.glow)
                        foreach (var parts in new[] { asm.playerVariantParts, asm.npcVariantParts })
                            if (parts != null) foreach (var p in parts) if (p != null) p.SetActive(false);
                }
                if (!isInstance) foreach (var lg in go.GetComponentsInChildren<LODGroup>(true)) lg.ForceLOD(0);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) r.enabled = false;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) { oldLayers.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = Layer; }
                var bounds = Bounds(go, o.glow);
                if (bounds.size == Vector3.zero) { error = "The model draws nothing (no meshes, or still loading)."; return null; }
                AddColliders(go);
                Physics.SyncTransforms();

                var camGo = new GameObject("Model viewer camera");
                camGo.transform.SetParent(stage.transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.orthographic = true;
                cam.aspect = 1f;
                cam.allowHDR = false;
                cam.allowMSAA = true;
                cam.allowDynamicResolution = false;
                cam.targetTexture = rt;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = false;
                var key = AddLight(stage.transform, new Color(1f, 0.96f, 0.9f), 1.25f);
                var fill = AddLight(stage.transform, new Color(0.6f, 0.68f, 0.8f), 0.5f);
                foreach (var l in Object.FindObjectsByType<Light>())
                    if (l != key && l != fill) { lights.Add((l, l.cullingMask)); l.cullingMask &= ~(1 << Layer); }
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);
                RenderSettings.fog = false;

                int cols = angles.Count == 1 ? 1 : angles.Count <= 4 ? 2 : angles.Count <= 6 ? 3 : 4;
                int rows = (angles.Count + cols - 1) / cols;
                var sheet = new Color32[cols * tile * rows * tile];
                if (!transparent) { var b32 = (Color32)bg; for (int i = 0; i < sheet.Length; i++) sheet[i] = b32; }
                var root = go.transform;
                float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
                // Each view's camera, kept for the markers and names drawn once every tile is in place (a tag reaching past its
                // tile was overwritten by the next one).
                var poses = new List<(Vector3 pos, Quaternion rot, float size, int ox, int oy, string name)>();
                for (int n = 0; n < angles.Count; n++)
                {
                    var view = Angles.First(x => x.name == angles[n]);
                    var dir = root.TransformDirection(view.from.normalized);
                    var up = root.TransformDirection(view.up);
                    var ct = cam.transform;
                    ct.position = bounds.center + dir * radius * 4f;
                    ct.rotation = Quaternion.LookRotation(-dir, up);
                    cam.nearClipPlane = radius * 1.5f;
                    cam.farClipPlane = radius * 6.5f;
                    float half = 0f;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var p = ct.InverseTransformPoint(c);
                        half = Mathf.Max(half, Mathf.Abs(p.x), Mathf.Abs(p.y));
                    }
                    cam.orthographicSize = half * 1.14f;
                    // The key light from the camera's side, a little above and to the left; the fill from behind.
                    key.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(-25f, ct.up) * Quaternion.AngleAxis(25f, ct.right) * ct.forward, ct.up);
                    fill.transform.rotation = Quaternion.LookRotation(-ct.forward + ct.up * 0.4f, ct.up);

                    Color32[] px;
                    if (transparent)
                    {
                        var black = Grab(cam, rt, Color.black);
                        var white = Grab(cam, rt, Color.white);
                        px = Down(black, white, tile, true, bg);
                    }
                    else px = Down(Grab(cam, rt, bg), null, tile, false, bg);
                    int ox = (n % cols) * tile, oy = (rows - 1 - n / cols) * tile;   // the sheet's rows from the top
                    Blit(px, tile, sheet, cols * tile, ox, oy);
                    poses.Add((ct.position, ct.rotation, cam.orthographicSize, ox, oy, view.name));
                }
                foreach (var (pos, rot, orthoSize, ox, oy, name) in poses)
                {
                    cam.transform.SetPositionAndRotation(pos, rot);
                    cam.orthographicSize = orthoSize;
                    if (o.markers != null) DrawMarkers(cam, root, o.markers, sheet, cols * tile, ox, oy, tile);
                    Text(sheet, cols * tile, ox + 6, oy + tile - 8, name.ToUpperInvariant(), 3, new Color32(255, 255, 255, 255));
                }
                var size = bounds.size;   // the stage is unscaled: metres
                info = $"{angles.Count} view(s), {cols * tile} x {rows * tile}; size {size.x:0.#} x {size.y:0.#} x {size.z:0.#} m (width x height x length), "
                     + $"{size.x / 0.05f:0} x {size.y / 0.05f:0} x {size.z / 0.05f:0} game units";
                var tex = new Texture2D(cols * tile, rows * tile, TextureFormat.RGBA32, false) { name = "model view" };
                tex.SetPixels32(sheet);
                tex.Apply();
                return tex;
            }
            catch (Exception e)
            {
                error = "The model couldn't be drawn: " + e.Message;
                Debug.LogException(e);
                return null;
            }
            finally
            {
                foreach (var (l, mask) in lights) if (l != null) l.cullingMask = mask;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                RenderSettings.fog = fog;
                if (isInstance && go != null)
                {
                    foreach (var c in go.GetComponentsInChildren<MeshCollider>(true)) if (c.gameObject.name == ColliderName) Object.DestroyImmediate(c.gameObject);
                    foreach (var (g, layer) in oldLayers) if (g != null) g.layer = layer;
                    go.transform.SetParent(oldParent, false);
                    go.transform.SetPositionAndRotation(oldPos, oldRot);
                }
                Object.DestroyImmediate(stage);
                rt.Release();
                Gone(rt);
            }
        }

        const string ColliderName = "Model viewer pick";

        /// <summary>Destroyed (at once outside Play mode: the edit-mode tests draw too).</summary>
        static void Gone(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static Light AddLight(Transform parent, Color colour, float intensity)
        {
            var go = new GameObject("Model viewer light");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = colour;
            l.intensity = intensity;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>The drawn meshes' bounds (the glow layers only with 'glow').</summary>
        static Bounds Bounds(GameObject go, bool glow)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!glow && r.name.Contains("_add")) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds();
        }

        /// <summary>MeshColliders on the readable hull meshes, for the markers' "hidden by the hull" test.</summary>
        static void AddColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled || !mf.sharedMesh.isReadable || mf.name.Contains("_add")) continue;
                var c = new GameObject(ColliderName) { layer = Layer };
                c.transform.SetParent(mf.transform, false);
                try { c.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; } catch (Exception) { Object.DestroyImmediate(c); }
            }
        }

        static Color32[] Grab(Camera cam, RenderTexture rt, Color background)
        {
            cam.backgroundColor = background;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Gone(tex);
            return px;
        }

        /// <summary>Down to the tile size (a box filter); with 'white' the alpha from the two backgrounds.</summary>
        static Color32[] Down(Color32[] a, Color32[] white, int tile, bool transparent, Color bg)
        {
            int w = tile * Super;
            var o = new Color32[tile * tile];
            for (int y = 0; y < tile; y++)
                for (int x = 0; x < tile; x++)
                {
                    float r = 0f, g = 0f, b = 0f, al = 0f;
                    for (int sy = 0; sy < Super; sy++)
                        for (int sx = 0; sx < Super; sx++)
                        {
                            int i = (y * Super + sy) * w + x * Super + sx;
                            var p = a[i];
                            if (transparent)
                            {
                                var q = white[i];
                                float alpha = Mathf.Clamp01(1f - ((q.r - p.r) + (q.g - p.g) + (q.b - p.b)) / (3f * 255f));
                                al += alpha;
                                r += p.r / 255f; g += p.g / 255f; b += p.b / 255f;   // on black: colour x alpha
                            }
                            else { r += p.r / 255f; g += p.g / 255f; b += p.b / 255f; al += 1f; }
                        }
                    const float n = Super * Super;
                    if (transparent)
                    {
                        if (al <= 0.01f) { o[y * tile + x] = new Color32(0, 0, 0, 0); continue; }
                        o[y * tile + x] = new Color(Mathf.Clamp01(r / al), Mathf.Clamp01(g / al), Mathf.Clamp01(b / al), al / n);
                    }
                    else o[y * tile + x] = new Color(r / n, g / n, b / n, 1f);
                }
            return o;
        }

        static void Blit(Color32[] src, int size, Color32[] dst, int dstW, int ox, int oy)
        {
            for (int y = 0; y < size; y++) Array.Copy(src, y * size, dst, (oy + y) * dstW + ox, size);
        }

        // ---- markers ------------------------------------------------------------------------------------------------

        static void DrawMarkers(Camera cam, Transform root, List<Marker> markers, Color32[] sheet, int w, int ox, int oy, int tile)
        {
            int radius = Mathf.Max(4, tile / 70);
            foreach (var m in markers)
            {
                var world = root.TransformPoint(m.local);
                var vp = cam.WorldToViewportPoint(world);
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
                int px = ox + Mathf.RoundToInt(vp.x * tile), py = oy + Mathf.RoundToInt(vp.y * tile);
                // Hidden: the hull between the camera and the point (orthographic: along the view's direction).
                var from = world - cam.transform.forward * (cam.farClipPlane);
                float toPoint = Vector3.Distance(from, world);
                bool hidden = Physics.Raycast(from, cam.transform.forward, out var hit, toPoint, 1 << Layer, QueryTriggerInteraction.Ignore)
                              && hit.distance < toPoint - cam.orthographicSize * 0.01f;
                Color32 c = ModBuilderGizmo.TypeColours[Mathf.Clamp(m.type, 0, 3)];
                Disc(sheet, w, px, py, radius + 2, new Color32(0, 0, 0, 255), false);
                Disc(sheet, w, px, py, radius, c, hidden);
                if (!string.IsNullOrEmpty(m.label)) Text(sheet, w, px + radius + 4, py + radius + 2, m.label, 2, c);
            }
        }

        static void Disc(Color32[] px, int w, int cx, int cy, int r, Color32 c, bool ring)
        {
            int h = px.Length / w;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d2 = x * x + y * y;
                    if (d2 > r * r || ring && d2 < (r - 2) * (r - 2)) continue;
                    int X = cx + x, Y = cy + y;
                    if (X >= 0 && X < w && Y >= 0 && Y < h) px[Y * w + X] = c;
                }
        }

        // ---- a 3 x 5 bitmap font ----------------------------------------------------------------------------------

        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = ".#.#.#####.##.#", ['B'] = "##.#.###.#.###.", ['C'] = ".###..#..#...##", ['D'] = "##.#.##.##.###.",
            ['E'] = "####..##.#..###", ['F'] = "####..##.#..#..", ['G'] = ".###..#.##.#.##", ['H'] = "#.##.#####.##.#",
            ['I'] = "###.#..#..#.###", ['J'] = "..#..#..##.#.#.", ['K'] = "#.##.###.#.##.#", ['L'] = "#..#..#..#..###",
            ['M'] = "#.########.##.#", ['N'] = "##.#.##.##.##.#", ['O'] = ".#.#.##.##.#.#.", ['P'] = "##.#.###.#..#..",
            ['Q'] = ".#.#.##.###..##", ['R'] = "##.#.###.#.##.#", ['S'] = ".###...#...###.", ['T'] = "###.#..#..#..#.",
            ['U'] = "#.##.##.##.####", ['V'] = "#.##.##.##.#.#.", ['W'] = "#.##.########.#", ['X'] = "#.##.#.#.#.##.#",
            ['Y'] = "#.##.#.#..#..#.", ['Z'] = "###..#.#.#..###", ['0'] = "####.##.##.####", ['1'] = ".#.##..#..#.###",
            ['2'] = "##...#.#.#..###", ['3'] = "##...#.#...###.", ['4'] = "#.##.####..#..#", ['5'] = "####..##...###.",
            ['6'] = ".###..####.####", ['7'] = "###..#.#..#..#.", ['8'] = "####.#####.####", ['9'] = "####.####..###.",
            ['-'] = "......###......", ['.'] = ".............#.", [':'] = "....#.....#....", ['/'] = "..#..#.#.#..#..",
            [' '] = "...............",
        };

        /// <summary>'text' at (x, y) = its top-left corner (y up), 'scale' pixels a dot, with a dark outline.</summary>
        public static void Text(Color32[] px, int w, int x, int y, string text, int scale, Color32 colour)
        {
            var dark = new Color32(0, 0, 0, 255);
            for (int pass = 0; pass < 2; pass++)
            {
                int cx = x;
                foreach (char ch in text.ToUpperInvariant())
                {
                    if (!Glyphs.TryGetValue(ch, out var g)) g = Glyphs[' '];
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                        {
                            if (g[row * 3 + col] != '#') continue;
                            int gx = cx + col * scale, gy = y - row * scale;
                            if (pass == 0) Fill(px, w, gx - 1, gy - scale, scale + 2, scale + 2, dark);
                            else Fill(px, w, gx, gy - scale + 1, scale, scale, colour);
                        }
                    cx += 4 * scale;
                }
            }
        }

        static void Fill(Color32[] px, int w, int x, int y, int fw, int fh, Color32 c)
        {
            int h = px.Length / w;
            for (int j = 0; j < fh; j++)
                for (int i = 0; i < fw; i++)
                {
                    int X = x + i, Y = y + j;
                    if (X >= 0 && X < w && Y >= 0 && Y < h) px[Y * w + X] = c;
                }
        }
    }
}
