// ModMaterials.cs
// Materials for mods' ships, always copies of the URP Lit templates in ModAssets:
//   - FromGltf: the glTF model's own PBR materials (base colour, metallic-roughness converted to URP's metallic /
//     smoothness map, normal map, emission, alpha mode), through ModGltfMaterials (glTFast's material generator);
//   - FromSpec: a ships.json "materials" entry (CustomShipMaterial, the PR #34 format): the mod's PNG textures as diffuse,
//     normal, metallic (R) / smoothness (A) mask, emission, detail maps, alpha clip or glass; it replaces the model's
//     material on the renderers / submeshes it names.
// Textures load once per mod and file (Texture), compressed, with mipmaps. ModShips decodes them first in the background
// (PreloadTexture: UnityWebRequestTexture decodes the PNG / JPG off the main thread, ModTextureEncoder compresses it to
// DXT with its mipmaps on a worker thread where the GPU reads DXT; elsewhere Texture2D.Compress, one texture per frame), so
// the builder's Texture calls find them made. The result is kept on disk (ModTextureCache): a later start reads it back on
// a worker thread instead of decoding and compressing again.
// Normal maps (normal = true): Android builds read a normal map's X from its alpha channel and Y from green (Player settings,
// Android normal map encoding "DXT5nm-style": URP's UnpackNormal with UNITY_ASTC_NORMALMAP_ENCODING). The game's normal maps
// are imported as Normal Map textures, which Unity re-encodes that way; a mod's PNG arrives as plain RGB with alpha 1, so on
// Android every normal came out bent sideways (X = 1) and the mods' hulls nearly black. On Android the loader copies X (red)
// into alpha before compressing (NormalsInAlpha); desktop reads either layout (UnpackNormalmapRGorAG) and is left as it was.

using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModMaterials
    {
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Color32, Texture2D> solids = new Dictionary<Color32, Texture2D>();
        static readonly List<Object> made = new List<Object>();

        static int revision = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { textures.Clear(); solids.Clear(); made.Clear(); revision = -1; }

        /// <summary>The mods changed: what was made for the old ones goes (ships, stations and backdrops share this cache, so it
        /// isn't one loader's to clear).</summary>
        static void Check()
        {
            if (revision == ModManager.Revision) return;
            Release();
            revision = ModManager.Revision;
        }

        /// <summary>Everything made for the mods' ships goes (the mods changed).</summary>
        public static void Release()
        {
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            textures.Clear();
            solids.Clear();
        }

        /// <summary>The build reads normal maps' X from alpha (Android's normal map encoding, see the header).</summary>
        public static bool NormalsInAlpha =>
#if UNITY_ANDROID
            true;
#else
            false;
#endif

        static string Key(ModInfo mod, string path, bool linear, bool readable, bool normal) =>
            mod.Id + "|" + ModSource.Normalise(path).ToLowerInvariant() + (linear ? "|l" : "") + (readable ? "|r" : "")
            + (normal && NormalsInAlpha ? "|n" : "");

        /// <summary>A normal map in the Android layout: X (red) copied into alpha, the mipmaps made again (RGBA32 if it had no
        /// alpha). Returns the texture to use (a new one when the format changed; the old one is destroyed).</summary>
        static Texture2D NormalToAlpha(Texture2D t)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i].a = px[i].r;
            bool mips = t.mipmapCount > 1;
            var o = t;
            if (t.format != TextureFormat.RGBA32 && t.format != TextureFormat.ARGB32)
            {
                o = new Texture2D(t.width, t.height, TextureFormat.RGBA32, mips, true)
                    { name = t.name, wrapMode = t.wrapMode, anisoLevel = t.anisoLevel, filterMode = t.filterMode };
                Object.Destroy(t);
            }
            o.SetPixels32(px);
            o.Apply(mips, false);
            return o;
        }

        static Material normalToAlpha;

        /// <summary>A glTF normal map (glTFast's texture, not readable) in the Android layout, on the GPU.</summary>
        static Texture NormalToAlpha(Texture src, string name)
        {
            if (normalToAlpha == null)
            {
                var shader = Resources.Load<Shader>("GoF2Mods/NormalToAlpha");
                if (shader == null) return src;
                normalToAlpha = new Material(shader);
            }
            var rt = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = name + " normal", useMipMap = true, autoGenerateMips = true, wrapMode = src.wrapMode, anisoLevel = 8 };
            Graphics.Blit(src, rt, normalToAlpha);
            made.Add(rt);
            return rt;
        }

        /// <summary>A PNG / JPG of the mod (null = none or unreadable); linear for data (normal, metallic, masks); normal for a
        /// normal map (its Android layout).</summary>
        public static Texture2D Texture(ModInfo mod, string path, bool linear, bool readable = false, bool normal = false)
        {
            Check();
            if (mod == null || string.IsNullOrEmpty(path)) return null;
            string key = Key(mod, path, linear, readable, normal);
            if (textures.TryGetValue(key, out var t)) return t;
            var bytes = mod.Source.ReadBytes(path);
            if (bytes == null) { Warn(mod, $"{path}: file not found"); textures[key] = null; return null; }
            t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = mod.Id + ":" + path, wrapMode = TextureWrapMode.Repeat };
            if (!t.LoadImage(bytes, false)) { Object.Destroy(t); Warn(mod, $"{path}: not a PNG or JPG image"); textures[key] = null; return null; }
            if (normal && NormalsInAlpha) t = NormalToAlpha(t);
            t.anisoLevel = 8;
            t.filterMode = FilterMode.Trilinear;
            if (!readable)
            {
                if (t.width % 4 == 0 && t.height % 4 == 0) t.Compress(true);
                t.Apply(false, true);
            }
            made.Add(t);
            textures[key] = t;
            return t;
        }

        static int compressFrame = -1;
        static readonly HashSet<string> loggedFormats = new HashSet<string>();

        /// <summary>Decodes a texture in the background into the cache Texture reads (nothing when it is there already, or
        /// when the background way fails: Texture then loads it the plain way).</summary>
        public static async System.Threading.Tasks.Task PreloadTexture(ModInfo mod, string path, bool linear, bool readable = false, bool normal = false)
        {
            Check();
            if (mod == null || string.IsNullOrEmpty(path)) return;
            bool toAlpha = normal && NormalsInAlpha;
            string key = Key(mod, path, linear, readable, normal);
            if (textures.ContainsKey(key)) return;
            // Desktop GPUs read DXT: the worker thread compresses it with its own mipmaps (ModTextureEncoder).
            bool encode = !readable && SystemInfo.SupportsTextureFormat(TextureFormat.DXT1) && SystemInfo.SupportsTextureFormat(TextureFormat.DXT5);
            string cacheRoot = ModTextureCache.Root;   // persistentDataPath: main thread only
            string cacheFile = null;
            ModTextureCache.Entry cached = null;
            string file = await System.Threading.Tasks.Task.Run(() =>
            {
                string f = mod.LocalFile(path);
                if (f != null && !readable)
                {
                    // Made before (ModTextureCache): read back here, off the main thread.
                    cacheFile = ModTextureCache.FileFor(cacheRoot, mod.Id, path, f, linear, (encode ? "dxt" : "gpu") + (toAlpha ? "-nrm" : ""));
                    cached = ModTextureCache.Read(cacheFile);
                }
                return f;
            });
            if (file == null || textures.ContainsKey(key)) return;
            if (cached != null && ModTextureCache.Make(cached, mod.Id + ":" + path) is Texture2D fromCache)
            {
                fromCache.wrapMode = TextureWrapMode.Repeat;
                fromCache.anisoLevel = 8;
                fromCache.filterMode = FilterMode.Trilinear;
                made.Add(fromCache);
                textures[key] = fromCache;
                return;
            }
            var p = UnityEngine.Networking.DownloadedTextureParams.Default;
            p.mipmapChain = !encode;
            p.linearColorSpace = linear;
            p.readable = true;   // the pixels for the encoder / Compress; Apply(.., true) drops them afterwards
            using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(new System.Uri(file).AbsoluteUri, p);
            var op = req.SendWebRequest();
            while (!op.isDone) await Awaitable.NextFrameAsync();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success || textures.ContainsKey(key)) return;
            var t = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            if (t == null) return;
            t.name = mod.Id + ":" + path;
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 8;
            t.filterMode = FilterMode.Trilinear;
            if (toAlpha) t = NormalToAlpha(t);
            var work = encode ? ModTextureEncoder.Start(t, linear) : null;
            if (encode && work == null && loggedFormats.Add(t.format + (t.width % 4 == 0 && t.height % 4 == 0 ? "" : " (size)")))
                Debug.Log($"Mods: {path}: {t.format} {t.width}x{t.height} is compressed on the main thread (the background encoder takes RGBA32 / ARGB32 / RGB24, sizes in multiples of 4)");
            if (work != null)
            {
                // A Burst job compresses it with its mipmaps (ModTextureEncoder); the decoded copy goes now.
                string name = t.name;
                Object.Destroy(t);
                while (!work.Done) await Awaitable.NextFrameAsync();
                if (textures.ContainsKey(key)) { work.Dispose(); return; }
                byte[] raw = cacheFile != null ? work.Raw() : null;
                var c = work.Take(name);
                if (raw != null)
                {
                    int w = c.width, h = c.height, mips = c.mipmapCount;
                    var format = c.format;
                    string target = cacheFile;
                    _ = System.Threading.Tasks.Task.Run(() => ModTextureCache.Write(target, w, h, format, mips, linear, raw));
                }
                c.wrapMode = TextureWrapMode.Repeat;
                c.anisoLevel = 8;
                c.filterMode = FilterMode.Trilinear;
                made.Add(c);
                textures[key] = c;
                return;
            }
            if (!readable)
            {
                // One compression per frame: several finishing together would make one long frame.
                while (compressFrame == Time.frameCount) await Awaitable.NextFrameAsync();
                compressFrame = Time.frameCount;
                if (textures.ContainsKey(key)) { Object.Destroy(t); return; }
                if (t.width % 4 == 0 && t.height % 4 == 0) t.Compress(true);
                if (cacheFile != null)
                {
                    byte[] raw = t.GetRawTextureData();
                    int w = t.width, h = t.height, mips = t.mipmapCount;
                    var format = t.format;
                    string target = cacheFile;
                    _ = System.Threading.Tasks.Task.Run(() => ModTextureCache.Write(target, w, h, format, mips, linear, raw));
                }
                t.Apply(false, true);
            }
            made.Add(t);
            textures[key] = t;
        }

        static void Warn(ModInfo mod, string message)
        {
            if (mod.Warnings.Contains(message)) return;
            mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }

        /// <summary>A 1 x 1 texture of one colour (a metallic / smoothness value without a map).</summary>
        static Texture2D Solid(Color c, bool linear = true)
        {
            Color32 k = c;
            if (solids.TryGetValue(k, out var t) && t != null) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false, linear) { name = "mod solid " + k };
            t.SetPixel(0, 0, c);
            t.Apply(false, true);
            made.Add(t);
            return solids[k] = t;
        }

        static Material Copy(Material template, string name)
        {
            Check();
            var m = new Material(template) { name = name };
            made.Add(m);
            return m;
        }

        static Color Rgb(float[] v, Color fallback) => v != null && v.Length >= 3 ? new Color(v[0], v[1], v[2], 1f) : fallback;

        /// <summary>A ships.json material entry (CustomShipMaterial, see CustomShipBuilder in PR #34 for the original).</summary>
        public static Material FromSpec(ModInfo mod, CustomShipMaterial e, string name)
        {
            var a = ModAssets.Get();
            if (a == null) return null;
            bool clip = e.alphaClip > 0f, glass = e.opacity > 0f && e.opacity < 1f;
            var dAlb = Texture(mod, e.detailAlbedo, true);
            var dNrm = Texture(mod, e.detailNormal, true, normal: true);
            var m = Copy(a.Lit(clip && !glass, glass, dAlb != null || dNrm != null), name);
            m.SetTexture("_DetailAlbedoMap", null);
            m.SetTexture("_DetailNormalMap", null);
            var diffuse = Texture(mod, e.diffuse, false);
            m.SetTexture("_BaseMap", diffuse);
            m.SetTexture("_MainTex", diffuse);
            var baseColor = Rgb(e.color, Color.white);
            if (glass) baseColor.a = e.opacity;
            m.SetColor("_BaseColor", baseColor);
            var nrm = Texture(mod, e.normal, true, normal: true);
            m.SetTexture("_BumpMap", nrm);
            m.SetFloat("_BumpScale", e.normalScale > 0f ? e.normalScale : 1f);
            var ms = Texture(mod, e.metallicSmoothness, true);
            m.SetTexture("_MetallicGlossMap", ms != null ? ms : Solid(new Color(e.metallic >= 0f ? e.metallic : 0f, 0f, 0f, 1f)));
            m.SetFloat("_Smoothness", e.smoothness);
            var em = Texture(mod, e.emission, false);
            bool emColor = e.emissionColor != null && e.emissionColor.Length >= 3;
            if (em != null || emColor)
            {
                m.SetTexture("_EmissionMap", em);
                var ec = Rgb(e.emissionColor, Color.white) * e.emissionIntensity;
                ec.a = 1f;
                m.SetColor("_EmissionColor", ec);
            }
            else m.SetColor("_EmissionColor", Color.black);
            if (clip && !glass) m.SetFloat("_Cutoff", e.alphaClip);
            m.SetFloat("_Cull", e.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            if (dAlb != null || dNrm != null)
            {
                m.SetTexture("_DetailAlbedoMap", dAlb);
                m.SetTexture("_DetailNormalMap", dNrm);
                m.SetFloat("_DetailAlbedoMapScale", 1f);
                m.SetFloat("_DetailNormalMapScale", e.detailNormalScale > 0f ? e.detailNormalScale : 1f);
                var tiling = Vector2.one * (e.detailTiling > 0f ? e.detailTiling : 1f);
                m.SetTextureScale("_DetailAlbedoMap", tiling);
                m.SetTextureScale("_DetailNormalMap", tiling);
            }
            return m;
        }

        /// <summary>A glTF material as a URP Lit copy (ModGltfMaterials).</summary>
        public static Material FromGltf(GLTFast.Schema.MaterialBase g, GLTFast.IGltfReadable gltf)
        {
            var a = ModAssets.Get();
            if (a == null) return null;
            var mode = g.GetAlphaMode();
            bool clip = mode == GLTFast.Schema.MaterialBase.AlphaMode.Mask, glass = mode == GLTFast.Schema.MaterialBase.AlphaMode.Blend;
            var m = Copy(a.Lit(clip, glass, false), string.IsNullOrEmpty(g.name) ? "glTF material" : g.name);
            var pbr = g.PbrMetallicRoughness;
            Texture2D Tex(GLTFast.Schema.TextureInfoBase info, string property)
            {
                if (info == null || info.index < 0) return null;
                var t = gltf.GetTexture(info.index);
                if (t != null && gltf.IsTextureYFlipped(info.index))
                {
                    m.SetTextureScale(property, new Vector2(1f, -1f));
                    m.SetTextureOffset(property, new Vector2(0f, 1f));
                }
                return t;
            }
            var baseColor = pbr != null ? pbr.BaseColor : Color.white;
            m.SetColor("_BaseColor", baseColor);
            var baseMap = pbr != null ? Tex(pbr.BaseColorTexture, "_BaseMap") : null;
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_MainTex", baseMap);
            float metal = pbr != null ? pbr.metallicFactor : 0f, rough = pbr != null ? pbr.roughnessFactor : 1f;
            var mr = pbr != null ? Tex(pbr.MetallicRoughnessTexture, "_MetallicGlossMap") : null;
            if (mr != null && a.metallicRoughness != null)
            {
                // B metal, G roughness -> R metal, A smoothness (the map's values; URP multiplies the smoothness by _Smoothness,
                // glTF's factors are taken as 1, the usual export).
                var rt = new RenderTexture(mr.width, mr.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = m.name + " metallic", useMipMap = true, autoGenerateMips = true, wrapMode = mr.wrapMode };
                Graphics.Blit(mr, rt, a.metallicRoughness);
                made.Add(rt);
                m.SetTexture("_MetallicGlossMap", rt);
                m.SetFloat("_Smoothness", 1f);
            }
            else
            {
                m.SetTexture("_MetallicGlossMap", Solid(new Color(metal, 0f, 0f, 1f)));
                m.SetFloat("_Smoothness", 1f - rough);
            }
            Texture nrm = Tex(g.NormalTexture, "_BumpMap");
            if (nrm != null && NormalsInAlpha) nrm = NormalToAlpha(nrm, m.name);
            m.SetTexture("_BumpMap", nrm);
            if (g.NormalTexture != null) m.SetFloat("_BumpScale", g.NormalTexture.scale);
            var emissive = g.Emissive;
            var emMap = Tex(g.EmissiveTexture, "_EmissionMap");
            if (emMap != null || emissive.maxColorComponent > 0f)
            {
                m.SetTexture("_EmissionMap", emMap);
                m.SetColor("_EmissionColor", emMap != null && emissive.maxColorComponent <= 0f ? Color.white : emissive);
            }
            else m.SetColor("_EmissionColor", Color.black);
            if (clip) m.SetFloat("_Cutoff", g.alphaCutoff);
            if (g.doubleSided) m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }

        /// <summary>Material for renderers without one.</summary>
        public static Material Default()
        {
            var a = ModAssets.Get();
            if (a == null) return null;
            var m = Copy(a.litOpaque, "mod default");
            m.SetColor("_EmissionColor", Color.black);
            m.SetTexture("_BumpMap", null);
            m.SetTexture("_MetallicGlossMap", Solid(new Color(0f, 0f, 0f, 0.5f)));
            return m;
        }
    }
}
