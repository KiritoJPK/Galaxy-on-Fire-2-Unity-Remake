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

    /// <summary>A file of a mod that couldn't be read (a damaged archive, a closed one): a data problem of that mod, reported
    /// as its warning, not a bug of the game.</summary>
    public class ModFileException : IOException
    {
        public ModFileException(string message, Exception inner = null) : base(message, inner) { }

        /// <summary>A failure reading a mod's files (this, IO, a damaged archive, an archive closed by a rescan) rather than a
        /// bug: the loaders warn on the mod and don't log the exception (the cloud diagnostics count those as crashes).</summary>
        public static bool IsFileError(Exception e) => e is IOException || e is InvalidDataException || e is ObjectDisposedException;
    }

    /// <summary>A zipped mod. The archive stays open while the mod is installed (its entries are read on demand); a mod
    /// whose files all sit in one top folder (zipped from the folder itself) is read from inside it. Thread-safe: the
    /// background loaders read it from worker threads, one entry at a time.</summary>
    public class ZipSource : ModSource
    {
        ZipArchive zip;
        bool disposed;
        readonly object gate = new object();
        readonly Dictionary<string, ZipArchiveEntry> entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> files = new List<string>();
        string prefix = "";

        // Shared for delete: an export or Delete in the Mod Browser can replace the zip while this source (or one a domain
        // reload left behind, open until it is collected) still reads it.
        static ZipArchive OpenArchive(string archive) =>
            new ZipArchive(new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete), ZipArchiveMode.Read);

        public ZipSource(string archive)
        {
            Location = archive;
            zip = OpenArchive(archive);
            var all = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
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

        /// <summary>The entry's bytes. A read that fails (a local header or deflate data that doesn't check out) is tried once
        /// more on the archive opened afresh, in case the open stream got out of step; failing again, the file is damaged in
        /// the archive (players' reports from Android: a mod zip of 79 models, one or two entries bad) and a ModFileException
        /// says so. A source closed meanwhile (the Mod Browser rescans: ModManager.Scan releases the old sources while the
        /// loaders still read) throws a ModFileException too, never a read on a closed stream.</summary>
        public override byte[] ReadBytes(string path)
        {
            var f = Resolve(path);
            if (f == null) return null;
            Exception first = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                byte[] bytes;
                uint crc;
                lock (gate)
                {
                    if (disposed) throw new ModFileException($"{f}: the mod's archive was closed (the mods were read again)");
                    try
                    {
                        if (attempt > 0) Reopen();
                        bytes = ReadEntry(f, out crc);
                    }
                    catch (Exception e) when (e is InvalidDataException || e is IOException) { first ??= e; continue; }
                }
                // The CRC outside the gate, so the workers check in parallel. The inflater doesn't check it: a damaged
                // entry can inflate without an error into the wrong bytes (tried on CoreCLR), which glTFast then reads.
                if (Crc32.Of(bytes) == crc) return bytes;
                first ??= new InvalidDataException("CRC mismatch");
            }
            throw new ModFileException($"{f} is damaged in {Path.GetFileName(Location)} ({first?.Message}): download or import the mod again", first);
        }

        byte[] ReadEntry(string f, out uint crc)
        {
            if (!entries.TryGetValue(f, out var entry)) throw new ModFileException($"{f}: no longer in {Path.GetFileName(Location)}");
            crc = entry.Crc32;
            using var s = entry.Open();
            using var m = entry.Length > 0 && entry.Length < int.MaxValue ? new MemoryStream((int)entry.Length) : new MemoryStream();
            s.CopyTo(m);
            if (m.Length != entry.Length) throw new InvalidDataException($"{m.Length} of {entry.Length} bytes");
            return m.Length == m.Capacity ? m.GetBuffer() : m.ToArray();
        }

        /// <summary>The archive opened again, the entries taken from the new one (under the gate).</summary>
        void Reopen()
        {
            var fresh = OpenArchive(Location);
            var map = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in fresh.Entries)
            {
                if (string.IsNullOrEmpty(e.Name)) continue;
                string n = Normalise(e.FullName);
                if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) map[n.Substring(prefix.Length)] = e;
            }
            try { zip.Dispose(); } catch { }
            zip = fresh;
            entries.Clear();
            foreach (var kv in map) entries[kv.Key] = kv.Value;
        }

        public override void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                zip.Dispose();
            }
        }
    }

    /// <summary>The zip format's CRC-32 (IEEE, reflected 0xEDB88320), sliced by 8: a mod's models are read whole on every start
    /// (hundreds of MB for the big ship packs), so the check has to keep up with the reads.</summary>
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class Crc32
    {
        static readonly uint[] table = Make();

        static uint[] Make()
        {
            var t = new uint[8 * 256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            for (int i = 0; i < 256; i++)
                for (int s = 1; s < 8; s++) t[s * 256 + i] = (t[(s - 1) * 256 + i] >> 8) ^ t[t[(s - 1) * 256 + i] & 0xFF];
            return t;
        }

        public static uint Of(byte[] data)
        {
            var t = table;
            uint c = 0xFFFFFFFFu;
            int i = 0, n = data.Length;
            for (; i + 8 <= n; i += 8)
            {
                uint a = c ^ (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24);
                uint b = (uint)(data[i + 4] | data[i + 5] << 8 | data[i + 6] << 16 | data[i + 7] << 24);
                c = t[7 * 256 + (a & 0xFF)] ^ t[6 * 256 + ((a >> 8) & 0xFF)] ^ t[5 * 256 + ((a >> 16) & 0xFF)] ^ t[4 * 256 + (a >> 24)]
                  ^ t[3 * 256 + (b & 0xFF)] ^ t[2 * 256 + ((b >> 8) & 0xFF)] ^ t[256 + ((b >> 16) & 0xFF)] ^ t[b >> 24];
            }
            for (; i < n; i++) c = t[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return ~c;
        }
    }
}
