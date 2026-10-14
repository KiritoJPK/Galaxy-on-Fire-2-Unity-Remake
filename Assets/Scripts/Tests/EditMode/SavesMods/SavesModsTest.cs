// SavesModsTest.cs
// The base of the save game and mod tests (area "SavesMods"): SandboxTest's empty data folder, plus the mod system's
// cached state put aside for the test and back afterwards, so a test sees only the mods it writes into the sandbox's Mods
// folder and the original tables:
//   - PlayerPrefs "mods_enabled" (ModManager's enabled list) cleared for the test and restored;
//   - ModManager's scanned list dropped (its archives closed; previews are never loaded here) and the Revision bumped,
//     ModRegistry's and ModTextureCache's cached paths / tables dropped (they cache what they read from the data folder:
//     without this, ModManager.Scan's cache pruning could reach the player's real cache folder);
//   - Session.Campaign / Difficulty / Economy (ResetNewGame keeps them) and GraphQuestState.RestorePending restored.
// Economy is Android during the tests (the remake's data files: items.json / ships.json as they are on disk), so the
// expected values can be read straight from Resources/GoF2Data.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public abstract class SavesModsTest : SandboxTest
    {
        const string EnabledKey = "mods_enabled";

        /// <summary>The original tables' sizes (Resources/GoF2Data: items.json 233, ships.json 64, systems.json 34, stations.json 135).</summary>
        protected const int OriginalItems = 233, OriginalShips = 64, OriginalSystems = 34, OriginalStations = 135;

        bool hadEnabled;
        string savedEnabled;
        Campaign campaign;
        float difficulty;
        Economy economy;
        bool restorePending;
        readonly List<ModSource> opened = new List<ModSource>();

        [SetUp]
        public void EnterModSandbox()
        {
            Assume.That(ModManager.BuilderProject, Is.Null, "the Mod Builder has a project open");
            hadEnabled = PlayerPrefs.HasKey(EnabledKey);
            savedEnabled = PlayerPrefs.GetString(EnabledKey, "");
            PlayerPrefs.SetString(EnabledKey, "");
            campaign = Session.Campaign;
            difficulty = Session.Difficulty;
            economy = Session.Economy;
            restorePending = GraphQuestState.RestorePending;
            ResetModState();
            Session.Economy = Economy.Android;
            Session.Campaign = Campaign.GalaxyOnFire2;
            Session.Difficulty = Session.DifficultyNormal;
        }

        [TearDown]
        public void LeaveModSandbox()
        {
            foreach (var s in opened) s.Dispose();
            opened.Clear();
            ResetModState();
            if (hadEnabled) PlayerPrefs.SetString(EnabledKey, savedEnabled);
            else PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.Save();
            Session.Campaign = campaign;
            Session.Difficulty = difficulty;
            Session.Economy = economy;
            GraphQuestState.RestorePending = restorePending;
        }

        static void ResetModState()
        {
            // ModManager: close the scanned mods' sources (ModInfo.Release would also Object.Destroy a loaded preview,
            // which edit mode refuses), forget them and bump the Revision so every cache keyed on it rebuilds.
            var t = typeof(ModManager);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Static;
            var installed = t.GetField("installed", F);
            var active = t.GetField("active", F);
            Assert.IsNotNull(installed, "ModManager.installed not found (the test sandbox resets it)");
            Assert.IsNotNull(active, "ModManager.active not found (the test sandbox resets it)");
            if (installed.GetValue(null) is List<ModInfo> list) foreach (var m in list) m.Source?.Dispose();
            installed.SetValue(null, null);
            active.SetValue(null, null);
            ModManager.EndSession();
            var rev = t.GetProperty("Revision", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true);
            Assert.IsNotNull(rev, "ModManager.Revision's setter not found");
            rev.Invoke(null, new object[] { ModManager.Revision + 1 });
            Invoke(typeof(ModRegistry), "ResetStatics");
            Invoke(typeof(ModTextureCache), "ResetStatics");
            ModContent.ClearParsed();
        }

        /// <summary>A private static method of the game's code (the mods' cached state).</summary>
        protected static void Invoke(Type type, string method)
        {
            var m = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, $"{type.Name}.{method} not found");
            m.Invoke(null, null);
        }

        /// <summary>ModRegistry's table forgotten (read again from the data folder on next use).</summary>
        protected static void ReloadRegistry() => Invoke(typeof(ModRegistry), "ResetStatics");

        // ---- writing mods -------------------------------------------------------------------------------------------

        protected string ModsFolder => ModManager.UserFolder;

        /// <summary>JSON written with ' for " (none of the test texts has an apostrophe).</summary>
        protected static string Q(string s) => s.Replace('\'', '"');

        /// <summary>A mod.json.</summary>
        protected static string Manifest(string id, string version = "1.0", string extra = "") =>
            Q("{ 'id': '" + id + "', 'name': 'Test " + id + "', 'version': '" + version + "', 'author': 'Tests'" + extra + " }");

        /// <summary>A folder mod in the sandbox's Mods folder: mod.json and the files (text, ' for ").</summary>
        protected string WriteMod(string folder, string id, string version = "1.0", string manifestExtra = "", params (string path, string text)[] files)
        {
            string dir = Path.Combine(ModsFolder, folder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ModManifest.FileName), Manifest(id, version, manifestExtra));
            foreach (var (p, text) in files) WriteFile(dir, p, Encoding.UTF8.GetBytes(Q(text)));
            return dir;
        }

        protected static void WriteFile(string dir, string path, byte[] bytes)
        {
            string full = Path.Combine(dir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
        }

        /// <summary>A placeholder model / image file (the content parsers only check that the file exists).</summary>
        protected static readonly byte[] Dummy = Encoding.ASCII.GetBytes("glTF dummy, never loaded by these tests");

        /// <summary>A zip archive of the entries (name, bytes), stored uncompressed when 'stored'.</summary>
        protected static string WriteZip(string path, bool stored, params (string name, byte[] data)[] entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                foreach (var (name, data) in entries)
                {
                    var e = zip.CreateEntry(name, stored ? System.IO.Compression.CompressionLevel.NoCompression : System.IO.Compression.CompressionLevel.Optimal);
                    using var s = e.Open();
                    s.Write(data, 0, data.Length);
                }
            return path;
        }

        protected static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(Q(text));

        /// <summary>A mod opened straight from its folder / zip, without ModManager (closed after the test).</summary>
        protected ModInfo Open(string location)
        {
            var src = ModSource.Open(location);
            Assert.IsNotNull(src, location + " isn't a mod");
            opened.Add(src);
            ModManifest manifest = null;
            ModInfo info;
            try { manifest = ModManifest.Read(src); info = new ModInfo(manifest, src); }
            catch (ModJsonException e) { info = new ModInfo(null, src); info.Errors.Add(e.Message); }
            return info;
        }

        /// <summary>A mod opened and its content files parsed (ModContent.Parse: errors go to info.Errors).</summary>
        protected ModInfo OpenParsed(string location)
        {
            var info = Open(location);
            if (!info.Broken) ModContent.Parse(info);
            return info;
        }

        /// <summary>Reads the sandbox's Mods folder and turns the mods on in this order.</summary>
        protected static void ScanAndEnable(params string[] ids)
        {
            ModManager.Scan();
            foreach (var id in ids) ModManager.SetEnabled(id, true);
        }

        protected static ModInfo Installed(string id)
        {
            var m = ModManager.Find(id);
            Assert.IsNotNull(m, "mod " + id + " not installed");
            return m;
        }

        protected static string AllMessages(ModInfo m) => string.Join(" | ", m.Errors) + " || " + string.Join(" | ", m.Warnings);

        /// <summary>A folder of the repository (Modding/Examples/...), null when it isn't there.</summary>
        protected static string RepoPath(params string[] parts)
        {
            var all = new List<string> { Application.dataPath, ".." };
            all.AddRange(parts);
            string p = Path.GetFullPath(Path.Combine(all.ToArray()));
            return Directory.Exists(p) || File.Exists(p) ? p : null;
        }

        protected static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(from.Length).TrimStart('\\', '/');
                string dst = Path.Combine(to, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(f, dst, true);
            }
        }

        /// <summary>An example mod of Modding/Examples copied into the sandbox's Mods folder (the test is ignored without it).</summary>
        protected string CopyExample(string name)
        {
            string src = RepoPath("Modding", "Examples", name);
            if (src == null) Assert.Ignore("Modding/Examples/" + name + " isn't in this checkout");
            string dst = Path.Combine(ModsFolder, name);
            CopyFolder(src, dst);
            return dst;
        }

        protected static List<ItemStack> Stacks(params (int item, int amount)[] s)
        {
            var l = new List<ItemStack>();
            foreach (var (i, a) in s) l.Add(new ItemStack(i, a));
            return l;
        }

        protected static string Describe(IEnumerable<ItemStack> l)
        {
            var sb = new StringBuilder();
            if (l != null) foreach (var s in l) sb.Append(s == null ? "null " : $"{s.item}x{s.amount} ");
            return sb.ToString().Trim();
        }
    }
}
