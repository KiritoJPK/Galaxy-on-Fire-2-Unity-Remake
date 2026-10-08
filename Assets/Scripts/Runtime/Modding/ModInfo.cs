// ModInfo.cs
// One installed mod: its manifest, files, what went wrong reading it, its content hash (multiplayer compares it) and the
// preview image for the mod browser.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public class ModInfo
    {
        public ModManifest Manifest { get; }
        public ModSource Source { get; }
        /// <summary>The mod can't be used (a broken mod.json, a duplicate id, a broken content file); shown in the browser.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Usable, but something was skipped (an unknown field, a missing translation).</summary>
        public readonly List<string> Warnings = new List<string>();

        public string Id => Manifest?.id ?? System.IO.Path.GetFileNameWithoutExtension(Source?.Location ?? "?");
        public string Name => Manifest?.Name ?? Id;
        public string Version => Manifest?.version ?? "?";
        public bool Broken => Manifest == null || Errors.Count > 0;

        public ModInfo(ModManifest manifest, ModSource source) { Manifest = manifest; Source = source; }

        string hash;

        /// <summary>SHA-256 of every file (path and contents, sorted), first 12 hex digits: two games with the same hash have
        /// the same mod, whatever its version says.</summary>
        public string Hash
        {
            get
            {
                if (hash != null || Source == null) return hash ?? "";
                using var sha = SHA256.Create();
                foreach (var f in Source.Files)
                {
                    var p = Encoding.UTF8.GetBytes(f.ToLowerInvariant() + "\n");
                    sha.TransformBlock(p, 0, p.Length, null, 0);
                    var b = Source.ReadBytes(f) ?? Array.Empty<byte>();
                    sha.TransformBlock(b, 0, b.Length, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var sb = new StringBuilder();
                for (int i = 0; i < 6; i++) sb.Append(sha.Hash[i].ToString("x2"));
                return hash = sb.ToString();
            }
        }

        /// <summary>"id@version#hash", what multiplayer and the save files record.</summary>
        public string Signature => $"{Id}@{Version}#{Hash}";

        Texture2D preview;
        bool previewTried;

        /// <summary>The manifest's preview image (null = none or unreadable); loaded on first use.</summary>
        public Texture2D Preview
        {
            get
            {
                if (previewTried) return preview;
                previewTried = true;
                var bytes = Manifest != null && Source != null ? Source.ReadBytes(Manifest.preview) : null;
                if (bytes == null) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Id + " preview", wrapMode = TextureWrapMode.Clamp };
                if (t.LoadImage(bytes, true)) preview = t;
                else { UnityEngine.Object.Destroy(t); Warnings.Add($"{Manifest.preview}: not a PNG or JPG image"); }
                return preview;
            }
        }

        /// <summary>A file of the mod on disk, for loaders that read from a path (UnityWebRequest: audio, background image
        /// decoding): the folder mod's own, a zip mod's copied into the cache once (temporaryCachePath/ModFiles/&lt;id&gt;_&lt;zip
        /// date&gt;/...). Thread-safe (the zip reads are locked); null when it can't be had.</summary>
        public string LocalFile(string file)
        {
            try
            {
                if (Source is FolderSource) return System.IO.Path.Combine(Source.Location, file);
                long stamp = System.IO.File.GetLastWriteTimeUtc(Source.Location).Ticks ^ new System.IO.FileInfo(Source.Location).Length;
                string path = System.IO.Path.Combine(cacheRoot, "ModFiles", Id + "_" + stamp.ToString("x"), file);
                if (!System.IO.File.Exists(path))
                {
                    var bytes = Source.ReadBytes(file);
                    if (bytes == null) return null;
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllBytes(path + ".tmp", bytes);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    System.IO.File.Move(path + ".tmp", path);
                }
                return path;
            }
            catch (Exception e) { Debug.LogWarning($"Mods: {Id}: {file}: {e.Message}"); return null; }
        }

        /// <summary>A GLB's embedded image written out for the texture decoder (ModMaterials.PreloadImage, which deletes it
        /// afterwards): temporaryCachePath/ModFiles/&lt;id&gt;_images/&lt;name&gt;.&lt;ext&gt;. Any thread; null when it can't be written.</summary>
        public string LocalImage(string name, string ext, byte[] bytes)
        {
            try
            {
                if (bytes == null) return null;
                string path = System.IO.Path.Combine(cacheRoot, "ModFiles", Id + "_images", name + "." + ext);
                if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length == bytes.Length) return path;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                System.IO.File.WriteAllBytes(tmp, bytes);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                System.IO.File.Move(tmp, path);
                return path;
            }
            catch (Exception e) { Debug.LogWarning($"Mods: {Id}: image {name}: {e.Message}"); return null; }
        }

        // Application.temporaryCachePath may only be read on the main thread: taken when the mod is made.
        readonly string cacheRoot = Application.temporaryCachePath;

        public void Release()
        {
            if (preview != null) UnityEngine.Object.Destroy(preview);
            preview = null;
            Source?.Dispose();
        }
    }
}
