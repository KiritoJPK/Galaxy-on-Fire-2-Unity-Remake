// ModSource.cs
// Where a mod's files come from: a folder in a Mods folder, or a .zip archive (the same layout zipped, with
// mod.json at its root or inside one top folder). Paths inside a mod use forward slashes and are case-insensitive, so
// a mod made on Windows also loads on Android / Linux.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace GoF2Remake.Modding
{
    public abstract class ModSource : IDisposable
    {
        /// <summary>The folder or archive on disk.</summary>
        public string Location { get; protected set; }

        /// <summary>Every file of the mod, relative paths with '/'.</summary>
        public abstract IReadOnlyList<string> Files { get; }

        public abstract byte[] ReadBytes(string path);

        public string ReadText(string path)
        {
            var b = ReadBytes(path);
            if (b == null) return null;
            // Strip a UTF-8 byte order mark (Notepad writes one).
            int start = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(b, start, b.Length - start);
        }

        public bool Exists(string path) => Resolve(path) != null;

        /// <summary>The file's own spelling of a path, matched without case; null = no such file.</summary>
        protected string Resolve(string path)
        {
            path = Normalise(path);
            foreach (var f in Files) if (string.Equals(f, path, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        /// <summary>The files in a folder of the mod (no subfolders) with one of the extensions.</summary>
        public IEnumerable<string> FilesIn(string folder, params string[] extensions)
        {
            folder = Normalise(folder).TrimEnd('/') + "/";
            foreach (var f in Files)
            {
                if (!f.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.IndexOf('/', folder.Length) >= 0) continue;
                if (extensions.Length == 0 || extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))) yield return f;
            }
        }

        public static string Normalise(string path) => (path ?? "").Replace('\\', '/').TrimStart('/');

        public virtual void Dispose() { }

        /// <summary>A folder or archive that holds a mod.json, else null.</summary>
        public static ModSource Open(string location)
        {
            try
            {
                if (Directory.Exists(location))
                    return File.Exists(Path.Combine(location, ModManifest.FileName)) ? new FolderSource(location) : null;
                string ext = Path.GetExtension(location).ToLowerInvariant();
                if (File.Exists(location) && ext == ".zip")
                {
                    var z = new ZipSource(location);
                    if (z.Exists(ModManifest.FileName)) return z;
                    z.Dispose();
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"Mods: can't open {location}: {e.Message}"); }
            return null;
        }
    }

    public class FolderSource : ModSource
    {
        readonly List<string> files = new List<string>();

        public FolderSource(string folder)
        {
            Location = folder;
            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                files.Add(Normalise(f.Substring(folder.Length)));
            files.Sort(StringComparer.OrdinalIgnoreCase);
        }

        public override IReadOnlyList<string> Files => files;

        public override byte[] ReadBytes(string path)
        {
            var f = Resolve(path);
            return f == null ? null : File.ReadAllBytes(Path.Combine(Location, f));
        }
    }

    /// <summary>A zipped mod. The archive stays open while the mod is installed (its entries are read on demand); a mod
    /// whose files all sit in one top folder (zipped from the folder itself) is read from inside it.</summary>
    public class ZipSource : ModSource
    {
        readonly ZipArchive zip;
        readonly Dictionary<string, ZipArchiveEntry> entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> files = new List<string>();

        public ZipSource(string archive)
        {
            Location = archive;
            zip = new ZipArchive(File.OpenRead(archive), ZipArchiveMode.Read);
            var all = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
            string prefix = "";
            if (!all.Any(e => string.Equals(Normalise(e.FullName), ModManifest.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                var inner = all.FirstOrDefault(e => Normalise(e.FullName).EndsWith("/" + ModManifest.FileName, StringComparison.OrdinalIgnoreCase)
                    && Normalise(e.FullName).Count(c => c == '/') == 1);
                if (inner != null) prefix = Normalise(inner.FullName).Substring(0, Normalise(inner.FullName).IndexOf('/') + 1);
            }
            foreach (var e in all)
            {
                string n = Normalise(e.FullName);
                if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                n = n.Substring(prefix.Length);
                entries[n] = e;
                files.Add(n);
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
        }

        public override IReadOnlyList<string> Files => files;

        public override byte[] ReadBytes(string path)
        {
            var f = Resolve(path);
            if (f == null) return null;
            lock (zip)
            {
                using var s = entries[f].Open();
                using var m = new MemoryStream();
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        public override void Dispose() => zip.Dispose();
    }
}
