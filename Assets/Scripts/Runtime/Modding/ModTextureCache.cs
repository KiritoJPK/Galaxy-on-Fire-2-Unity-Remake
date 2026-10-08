// ModTextureCache.cs
// Remake mods: the mods' textures as the game uploads them (DXT with its mipmaps where the GPU reads DXT, else what
// Texture2D.Compress made), kept on disk for later plays: persistentDataPath/ModCache/Textures/<mod id>/<hash>.tex. Without
// it every start decoded each PNG / JPG again and compressed it (most of a mod ship's loading time); a cached one is read on
// a worker thread and handed to the GPU as it is (ModMaterials.PreloadTexture). The hash is of the file's path in the mod,
// its size and its change time, linear or not, and the kind (DXT or the platform's own compression), so an edited texture
// is made again. ModManager.Scan deletes an uninstalled mod's folder; the mod browser's "Rebuild cache" (ClearAll) deletes
// every mod cache (these, the hangar shadows, the zip mods' unpacked files) and loads the mods again.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModTextureCache
    {
        const int Magic = 0x31545847;   // "GXT1"
        const int Version = 1;          // a change to the format or to the encoder: every texture is made again

        /// <summary>A cached texture, read on a worker thread.</summary>
        public sealed class Entry
        {
            public int width, height, mips;
            public TextureFormat format;
            public bool linear;
            public byte[] data;
        }

        static string root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => root = null;

        /// <summary>persistentDataPath/ModCache/Textures (read it on the main thread first: persistentDataPath).</summary>
        public static string Root => root ??= Path.Combine(Application.persistentDataPath, "ModCache", "Textures");

        /// <summary>The cache file of a mod's texture (any thread): 'localFile' is the file on disk (ModInfo.LocalFile),
        /// 'kind' how it is compressed ("dxt" / "gpu").</summary>
        public static string FileFor(string rootDir, string modId, string path, string localFile, bool linear, string kind)
        {
            var fi = new FileInfo(localFile);
            string id = $"{ModSource.Normalise(path).ToLowerInvariant()}|{(linear ? 1 : 0)}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}|{kind}|{Version}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id));
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++) sb.Append(hash[i].ToString("x2"));
            return Path.Combine(rootDir, modId, sb + ".tex");
        }

        /// <summary>The cache file of a texture named by its content (a GLB's embedded image, ModGltf: 'content' = its hash
        /// and role), any thread.</summary>
        public static string FileForContent(string rootDir, string modId, string content, bool linear, string kind)
        {
            string id = $"#content|{content}|{(linear ? 1 : 0)}|{kind}|{Version}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id));
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++) sb.Append(hash[i].ToString("x2"));
            return Path.Combine(rootDir, modId, sb + ".tex");
        }

        /// <summary>The cached texture, or null (none, or unreadable: it is made again). Any thread.</summary>
        public static Entry Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                using var r = new BinaryReader(File.OpenRead(file));
                if (r.ReadInt32() != Magic || r.ReadInt32() != Version) return null;
                var e = new Entry { width = r.ReadInt32(), height = r.ReadInt32(), format = (TextureFormat)r.ReadInt32(), mips = r.ReadInt32(), linear = r.ReadInt32() != 0 };
                int length = r.ReadInt32();
                if (e.width <= 0 || e.height <= 0 || e.mips <= 0 || length <= 0 || length > 256 << 20) return null;
                e.data = r.ReadBytes(length);
                return e.data.Length == length ? e : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Stores a texture's GPU data (any thread; written to a temporary file first, so a crash leaves no half file).</summary>
        public static void Write(string file, int width, int height, TextureFormat format, int mips, bool linear, byte[] data)
        {
            if (string.IsNullOrEmpty(file) || data == null || data.Length == 0) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                string tmp = file + ".tmp";
                using (var w = new BinaryWriter(File.Create(tmp)))
                {
                    w.Write(Magic); w.Write(Version);
                    w.Write(width); w.Write(height); w.Write((int)format); w.Write(mips); w.Write(linear ? 1 : 0);
                    w.Write(data.Length);
                    w.Write(data);
                }
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch (Exception e) { Debug.LogWarning($"Mods: texture cache: {e.Message}"); }
        }

        /// <summary>The texture from a cached entry (main thread), or null when this GPU can't take it.</summary>
        public static Texture2D Make(Entry e, string name)
        {
            if (e == null || !SystemInfo.SupportsTextureFormat(e.format)) return null;
            Texture2D t = null;
            try
            {
                t = new Texture2D(e.width, e.height, e.format, e.mips, e.linear) { name = name };
                t.LoadRawTextureData(e.data);
                t.Apply(false, true);
                return t;
            }
            catch (Exception ex)
            {
                if (t != null) UnityEngine.Object.Destroy(t);
                Debug.LogWarning($"Mods: texture cache: {name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Deletes the cached textures of the mods no longer installed (ModManager.Scan: every mod id found, on or off).</summary>
        public static void PruneCache(IEnumerable<string> installedIds)
        {
            try
            {
                if (!Directory.Exists(Root)) return;
                var keep = new HashSet<string>(installedIds, StringComparer.OrdinalIgnoreCase);
                foreach (var dir in Directory.GetDirectories(Root))
                    if (!keep.Contains(Path.GetFileName(dir))) Directory.Delete(dir, true);
            }
            catch (Exception e) { Debug.LogWarning($"Mods: texture cache: {e.Message}"); }
        }

        /// <summary>Every mod cache goes (the mod browser's "Rebuild cache"): the textures and hangar shadows
        /// (persistentDataPath/ModCache) and the zip mods' unpacked files (temporaryCachePath/ModFiles). The caller then
        /// rescans the mods, which loads them all again.</summary>
        public static void ClearAll()
        {
            foreach (var dir in new[] { Path.Combine(Application.persistentDataPath, "ModCache"), Path.Combine(Application.temporaryCachePath, "ModFiles") })
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
                catch (Exception e) { Debug.LogWarning($"Mods: couldn't delete {dir}: {e.Message}"); }
            }
            Debug.Log("Mods: every mod cache deleted (textures, hangar shadows, unpacked zips); loading the mods again");
        }
    }
}
