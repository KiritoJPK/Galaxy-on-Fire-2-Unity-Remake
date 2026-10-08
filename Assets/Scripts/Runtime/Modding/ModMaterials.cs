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
// a worker thread instead of decoding and compressing again. A GLB's embedded images come the same way (PreloadImage, by
// their content, see ModGltf), their role's conversion done on the CPU before compression. A texture is made once however
// many ask for it at the same time (Once), and at most DecodeSlots are decoded at once (each is uncompressed until then).
// Normal maps (normal = true): Android builds read a normal map's X from its alpha channel and Y from green (Player settings,
// Android normal map encoding "DXT5nm-style": URP's UnpackNormal with UNITY_ASTC_NORMALMAP_ENCODING). The game's normal maps
// are imported as Normal Map textures, which Unity re-encodes that way; a mod's PNG arrives as plain RGB with alpha 1, so on
// Android every normal came out bent sideways (X = 1) and the mods' hulls nearly black. On Android the loader copies X (red)
// into alpha before compressing (NormalsInAlpha); desktop reads either layout (UnpackNormalmapRGorAG) and is left as it was.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            pending.Clear();   // what is still loading is dropped when it ends (Store: the revision changed)
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

        /// <summary>How a decoded texture is converted before it is compressed.</summary>
        enum Remap { None, NormalToAlpha, MetallicRoughness }

        /// <summary>A normal map in the Android layout: X (red) copied into alpha, the mipmaps made again (RGBA32 if it had no
        /// alpha). Returns the texture to use (a new one when the format changed; the old one is destroyed).</summary>
        static Texture2D NormalToAlpha(Texture2D t) => Convert(t, Remap.NormalToAlpha);

        /// <summary>A decoded (readable) texture converted for its use: NormalToAlpha above, or a glTF metallic-roughness map
        /// (B metallic, G roughness) as URP Lit's metallic / smoothness map (R metallic, A smoothness = 1 - roughness), like
        /// Hidden/GoF2/MetallicRoughnessToGloss but before compression (the shader's render texture stayed uncompressed).</summary>
        static Texture2D Convert(Texture2D t, Remap remap)
        {
            var px = t.GetPixels32();
            if (remap == Remap.NormalToAlpha) for (int i = 0; i < px.Length; i++) px[i].a = px[i].r;
            else for (int i = 0; i < px.Length; i++) px[i] = new Color32(px[i].b, 0, 0, (byte)(255 - px[i].g));
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
        static readonly Dictionary<string, Task> pending = new Dictionary<string, Task>();
        static SemaphoreSlim decodeSlots;

        /// <summary>Decodes at once at most: each decoded texture is uncompressed (a 1024 px one 4-5.6 MB) until it is
        /// compressed, and a mod of many models asks for hundreds at the same time.</summary>
        static SemaphoreSlim DecodeSlots => decodeSlots ??= new SemaphoreSlim(Application.isMobilePlatform ? 2 : 6);

        /// <summary>Decodes a texture in the background into the cache Texture reads (nothing when it is there already, or
        /// when the background way fails: Texture then loads it the plain way).</summary>
        public static Task PreloadTexture(ModInfo mod, string path, bool linear, bool readable = false, bool normal = false)
        {
            Check();
            if (mod == null || string.IsNullOrEmpty(path)) return Task.CompletedTask;
            string key = Key(mod, path, linear, readable, normal);
            var remap = normal && NormalsInAlpha ? Remap.NormalToAlpha : Remap.None;
            return Once(key, () => Decode(key, mod.Id + ":" + path, mod.Id, path, null, () => mod.LocalFile(path), linear, readable, remap));
        }

        static string ImageKey(ModInfo mod, string hash, ModGlbImages.Role role) =>
            mod.Id + "|#image:" + hash + "|" + role + (role == ModGlbImages.Role.Normal && NormalsInAlpha ? "|n" : "");

        /// <summary>A GLB's embedded image (ModGltf, ModGlbImages) as a texture for one role, made in the background once per
        /// mod and image content: base colour / emission sRGB, normal and metallic-roughness maps linear and converted.</summary>
        public static Task PreloadImage(ModInfo mod, string hash, string ext, byte[] bytes, ModGlbImages.Role role)
        {
            Check();
            string key = ImageKey(mod, hash, role);
            bool linear = role != ModGlbImages.Role.Color;
            var remap = role == ModGlbImages.Role.MetallicRoughness ? Remap.MetallicRoughness
                      : role == ModGlbImages.Role.Normal && NormalsInAlpha ? Remap.NormalToAlpha : Remap.None;
            return Once(key, () => Decode(key, $"{mod.Id}:{hash.Substring(0, 8)} {role}", mod.Id, null, hash + "|" + role,
                () => mod.LocalImage(hash + "_" + role, ext, bytes), linear, false, remap));
        }

        /// <summary>A texture PreloadImage made (null: none, or it couldn't be read).</summary>
        public static Texture2D Image(ModInfo mod, string hash, ModGlbImages.Role role)
        {
            Check();
            return textures.TryGetValue(ImageKey(mod, hash, role), out var t) ? t : null;
        }

        /// <summary>One load per key: a key already made or being made isn't made again (many models share images).</summary>
        static Task Once(string key, System.Func<Task> make)
        {
            if (textures.ContainsKey(key)) return Task.CompletedTask;
            if (pending.TryGetValue(key, out var running)) return running;
            var task = Run();
            if (!task.IsCompleted) pending[key] = task;
            return task;

            async Task Run()
            {
                try { await make(); }
                finally { pending.Remove(key); }
            }
        }

        /// <summary>Stores a made texture, unless the mods changed meanwhile (then it goes).</summary>
        static void Store(string key, Texture2D t, int rev)
        {
            if (rev != revision || textures.ContainsKey(key)) { Object.Destroy(t); return; }
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 8;
            t.filterMode = FilterMode.Trilinear;
            made.Add(t);
            textures[key] = t;
        }

        /// <summary>Reads a texture back from the disk cache or decodes, converts and compresses it. The cache file is named
        /// by the mod file's path, size and date ('path'), or by the content ('content', a GLB's image).</summary>
        static async Task Decode(string key, string name, string modId, string path, string content, System.Func<string> localFile,
            bool linear, bool readable, Remap remap)
        {
            int rev = revision;
            // Desktop GPUs read DXT: the worker thread compresses it with its own mipmaps (ModTextureEncoder).
            bool encode = !readable && SystemInfo.SupportsTextureFormat(TextureFormat.DXT1) && SystemInfo.SupportsTextureFormat(TextureFormat.DXT5);
            string kind = (encode ? "dxt" : "gpu") + (remap == Remap.NormalToAlpha ? "-nrm" : remap == Remap.MetallicRoughness ? "-mr" : "");
            string cacheRoot = ModTextureCache.Root;   // persistentDataPath: main thread only
            var slots = DecodeSlots;
            await slots.WaitAsync();
            string file = null;
            try
            {
                string cacheFile = null;
                ModTextureCache.Entry cached = null;
                file = await Task.Run(() =>
                {
                    string f = null;
                    if (!readable)
                    {
                        // Made before (ModTextureCache): read back here, off the main thread.
                        if (content != null) cacheFile = ModTextureCache.FileForContent(cacheRoot, modId, content, linear, kind);
                        else if ((f = localFile()) != null) cacheFile = ModTextureCache.FileFor(cacheRoot, modId, path, f, linear, kind);
                        if (cacheFile != null) cached = ModTextureCache.Read(cacheFile);
                    }
                    return cached != null ? null : f ?? localFile();
                });
                if (rev != revision || textures.ContainsKey(key)) return;
                if (cached != null && ModTextureCache.Make(cached, name) is Texture2D fromCache) { Store(key, fromCache, rev); return; }
                if (file == null) return;
                var p = UnityEngine.Networking.DownloadedTextureParams.Default;
                p.mipmapChain = !encode;
                p.linearColorSpace = linear;
                p.readable = true;   // the pixels for the encoder / Compress; Apply(.., true) drops them afterwards
                using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(new System.Uri(file).AbsoluteUri, p);
                var op = req.SendWebRequest();
                while (!op.isDone) await Awaitable.NextFrameAsync();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success || rev != revision || textures.ContainsKey(key)) return;
                var t = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                if (t == null) return;
                t.name = name;
                if (remap != Remap.None) t = Convert(t, remap);
                var work = encode ? ModTextureEncoder.Start(t, linear) : null;
                if (encode && work == null && loggedFormats.Add(t.format + (t.width % 4 == 0 && t.height % 4 == 0 ? "" : " (size)")))
                    Debug.Log($"Mods: {name}: {t.format} {t.width}x{t.height} is compressed on the main thread (the background encoder takes RGBA32 / ARGB32 / RGB24, sizes in multiples of 4)");
                if (work != null)
                {
                    // A Burst job compresses it with its mipmaps (ModTextureEncoder); the decoded copy goes now.
                    Object.Destroy(t);
                    while (!work.Done) await Awaitable.NextFrameAsync();
                    if (rev != revision || textures.ContainsKey(key)) { work.Dispose(); return; }
                    byte[] raw = cacheFile != null ? work.Raw() : null;
                    var c = work.Take(name);
                    if (raw != null) WriteCache(cacheFile, c, linear, raw);
                    Store(key, c, rev);
                    return;
                }
                if (!readable)
                {
                    // One compression per frame: several finishing together would make one long frame. Phones take the
                    // fast mode (ETC2 in high quality took several times as long, the first start's loading screen).
                    while (compressFrame == Time.frameCount) await Awaitable.NextFrameAsync();
                    compressFrame = Time.frameCount;
                    if (rev != revision || textures.ContainsKey(key)) { Object.Destroy(t); return; }
                    if (t.width % 4 == 0 && t.height % 4 == 0) t.Compress(!Application.isMobilePlatform);
                    if (cacheFile != null) WriteCache(cacheFile, t, linear, t.GetRawTextureData());
                    t.Apply(false, true);
                }
                Store(key, t, rev);
            }
            finally
            {
                slots.Release();
                // A GLB image's file was only the way into the decoder (ModInfo.LocalImage).
                if (content != null && file != null) _ = Task.Run(() => { try { System.IO.File.Delete(file); } catch (System.Exception) { } });
            }
        }

        static void WriteCache(string cacheFile, Texture2D t, bool linear, byte[] raw)
        {
            int w = t.width, h = t.height, mips = t.mipmapCount;
            var format = t.format;
            _ = Task.Run(() => ModTextureCache.Write(cacheFile, w, h, format, mips, linear, raw));
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

        /// <summary>A glTF material as a URP Lit copy (ModGltfMaterials). 'images' gives a texture index's texture for a role
        /// when the mods' loader made it (ModGltf: compressed, metallic-roughness and normal maps already converted); else
        /// glTFast's own texture, converted here on the GPU.</summary>
        public static Material FromGltf(GLTFast.Schema.MaterialBase g, GLTFast.IGltfReadable gltf,
            System.Func<int, ModGlbImages.Role, Texture2D> images = null)
        {
            var a = ModAssets.Get();
            if (a == null) return null;
            var mode = g.GetAlphaMode();
            bool clip = mode == GLTFast.Schema.MaterialBase.AlphaMode.Mask, glass = mode == GLTFast.Schema.MaterialBase.AlphaMode.Blend;
            var m = Copy(a.Lit(clip, glass, false), string.IsNullOrEmpty(g.name) ? "glTF material" : g.name);
            var pbr = g.PbrMetallicRoughness;
            bool ours = false;   // the last Tex came from 'images'
            Texture2D Tex(GLTFast.Schema.TextureInfoBase info, string property, ModGlbImages.Role role = ModGlbImages.Role.Color)
            {
                ours = false;
                if (info == null || info.index < 0) return null;
                var t = images?.Invoke(info.index, role);
                ours = t != null;
                if (t == null) t = gltf.GetTexture(info.index);
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
            var mr = pbr != null ? Tex(pbr.MetallicRoughnessTexture, "_MetallicGlossMap", ModGlbImages.Role.MetallicRoughness) : null;
            if (mr != null && ours)
            {
                m.SetTexture("_MetallicGlossMap", mr);
                m.SetFloat("_Smoothness", 1f);
            }
            else if (mr != null && a.metallicRoughness != null)
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
            Texture nrm = Tex(g.NormalTexture, "_BumpMap", ModGlbImages.Role.Normal);
            if (nrm != null && NormalsInAlpha && !ours) nrm = NormalToAlpha(nrm, m.name);
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
