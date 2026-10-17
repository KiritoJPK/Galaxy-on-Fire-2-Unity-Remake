// ModProject.cs
// Remake-only: a Mod Builder project (ModBuilder). A project is a mod's folder under Application.persistentDataPath/
// ModProjects/<id>, written in the mods' own format (mod.json, ships.json, models/*.glb; Modding/README.md), so the game
// reads it like any installed mod while it is being built (ModManager.SetBuilderProject) and Export only zips it into
// the Mods folder. Beside the mod's files it keeps builder.json (never exported): each ship's test-flight loadout, the
// test cheats and the ship last worked on.
// The ships are ProjectShip: their ships.json entry as a JObject (fields the builder doesn't know, from a hand edit, are
// kept), with typed accessors for what the builder edits and the hardpoints as WeaponMounts. Items (items.json) and
// characters (characters.json) are ProjectItem / ProjectCharacter (ProjectContent.cs) the same way; their files are written
// only once the project has some (or had the file). So are the systems / stations / rooms (ProjectWorld.cs), blueprints,
// new-game options and the campaign (ProjectGame.cs) and the quests made in the builder (events/<name>.gof2quest,
// ProjectQuest). Its audio is plain files: music/, sounds/ and voices/ (AudioFiles). What other mods it uses is its
// manifest's "dependencies" (AddDependency: ModRefs adds one when another mod's content is picked).
// Plain C#.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using GoF2Remake.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class ModProject
    {
        public const string BuilderFile = "builder.json";
        public const string ModelsFolder = "models";

        /// <summary>Where the projects live (created on first use).</summary>
        public static string Root => Path.Combine(GoF2Remake.GamePaths.Data, "ModProjects");

        public string Folder { get; private set; }
        public string Id { get; private set; }
        public string Name { get => Str(manifest, "name") ?? Id; set => manifest["name"] = value; }
        public string Description { get => Str(manifest, "description") ?? ""; set => manifest["description"] = value; }
        public string Author { get => Str(manifest, "author") ?? ""; set => manifest["author"] = value; }
        public string Version { get => Str(manifest, "version") ?? "1.0"; set => manifest["version"] = value; }
        public string Website { get => Str(manifest, "website") ?? ""; set { if (string.IsNullOrWhiteSpace(value)) manifest.Remove("website"); else manifest["website"] = value.Trim(); } }
        /// <summary>mod.json "credits": who made what (the Mods screen shows it).</summary>
        public string Credits { get => Str(manifest, "credits") ?? ""; set { if (string.IsNullOrWhiteSpace(value)) manifest.Remove("credits"); else manifest["credits"] = value; } }
        /// <summary>The Mods screen's picture (mod.json "preview", default preview.png): its path in the project, or null.</summary>
        public string PreviewImage
        {
            get
            {
                string p = Str(manifest, "preview") ?? "preview.png";
                return File.Exists(Path.Combine(Folder, p)) ? p : null;
            }
        }

        public void SetPreviewImage(string sourcePng)
        {
            File.Copy(sourcePng, Path.Combine(Folder, "preview.png"), true);
            manifest.Remove("preview");
        }

        // ---- translations (text/<lang>.json) ------------------------------------------------------------------

        public const string TextFolder = "text";
        public static readonly string[] TextLanguages = { "de", "fr", "es", "it", "nl", "pl", "ru", "pt", "zh", "hi", "ja", "ko", "ar" };

        /// <summary>Every text of the project a translation can carry: key ("items.plasma_lance.name") and its English.</summary>
        public List<(string key, string english)> TranslatableTexts()
        {
            var list = new List<(string, string)>();
            foreach (var it in Items)
            {
                string id = it.IsOverride ? it.OverrideIndex.ToString() : it.LocalId;
                if (id == null) continue;
                list.Add(($"items.{id}.name", it.Name ?? ""));
                list.Add(($"items.{id}.description", it.Description ?? ""));
            }
            foreach (var s in Ships)
            {
                string id = s.IsOverride ? s.OverrideIndex.ToString() : s.LocalId;
                if (id == null) continue;
                list.Add(($"ships.{id}.name", s.Name ?? ""));
                list.Add(($"ships.{id}.description", s.Description ?? ""));
            }
            foreach (var s in Systems) { string id = s.IsOverride ? s.OverrideRef : s.LocalId; if (id != null) list.Add(($"systems.{id}.name", s.Name ?? "")); }
            foreach (var s in Stations) { string id = s.IsOverride ? s.OverrideRef : s.LocalId; if (id != null) list.Add(($"stations.{id}.name", s.Name ?? "")); }
            return list;
        }

        /// <summary>text/&lt;lang&gt;.json as key -> text (empty when there is none).</summary>
        public Dictionary<string, string> Texts(string lang)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string f = Path.Combine(Folder, TextFolder, lang + ".json");
            try
            {
                if (File.Exists(f) && JToken.Parse(File.ReadAllText(f)) is JObject o)
                    foreach (var p in o.Properties()) if (p.Value.Type == JTokenType.String) d[p.Name] = (string)p.Value;
            }
            catch (Exception e) { Debug.LogWarning($"Mod Builder: {f}: {e.Message}"); }
            return d;
        }

        /// <summary>One translation set (empty: removed); the file goes when none is left.</summary>
        public void SetText(string lang, string key, string text)
        {
            var d = Texts(lang);
            if (string.IsNullOrWhiteSpace(text)) d.Remove(key); else d[key] = text;
            string dir = Path.Combine(Folder, TextFolder), f = Path.Combine(dir, lang + ".json");
            if (d.Count == 0) { try { if (File.Exists(f)) File.Delete(f); } catch { } return; }
            Directory.CreateDirectory(dir);
            var o = new JObject();
            foreach (var kv in d.OrderBy(k => k.Key, StringComparer.Ordinal)) o[kv.Key] = kv.Value;
            File.WriteAllText(f, o.ToString(Formatting.Indented));
        }
        public DateTime Modified { get; private set; }

        public readonly List<ProjectShip> Ships = new List<ProjectShip>();
        public readonly List<ProjectItem> Items = new List<ProjectItem>();
        public readonly List<ProjectCharacter> Characters = new List<ProjectCharacter>();
        public readonly List<ProjectSystem> Systems = new List<ProjectSystem>();
        public readonly List<ProjectStation> Stations = new List<ProjectStation>();
        public readonly List<ProjectInterior> Interiors = new List<ProjectInterior>();
        public readonly List<ProjectBlueprint> Blueprints = new List<ProjectBlueprint>();
        /// <summary>shipkits.json: the customizable ships' kits of parts (ModShipKits).</summary>
        public readonly List<ProjectKit> Kits = new List<ProjectKit>();
        public readonly List<ProjectGameOption> GameOptions = new List<ProjectGameOption>();
        public readonly List<ProjectQuest> Quests = new List<ProjectQuest>();
        /// <summary>campaign.json, null = the mod has no campaign.</summary>
        public ProjectCampaign Campaign;
        public const string CampaignFile = "campaign.json", InteriorsFile = "interiors.json";
        JObject manifest = new JObject();
        JObject builder = new JObject();
        /// <summary>A content file couldn't be read (a hand edit gone wrong): shown, and the project isn't saved over it.</summary>
        public string LoadError { get; private set; }

        static string Str(JObject o, string key) => o[key] is JValue v && v.Type == JTokenType.String ? (string)v : null;

        // ---- the list ----------------------------------------------------------------------------------------------

        /// <summary>Every project, the most recently changed first.</summary>
        public static List<ModProject> List()
        {
            var l = new List<ModProject>();
            try
            {
                Directory.CreateDirectory(Root);
                foreach (var dir in Directory.GetDirectories(Root))
                {
                    if (!File.Exists(Path.Combine(dir, ModManifest.FileName))) continue;
                    var p = Load(dir);
                    if (p != null) l.Add(p);
                }
            }
            catch (Exception e) { Debug.LogWarning($"Mod Builder: can't read {Root}: {e.Message}"); }
            l.Sort((a, b) => b.Modified.CompareTo(a.Modified));
            return l;
        }

        /// <summary>A name as an id (like a repository's name on GitHub / GitLab): lower case, a-z 0-9, anything else one '_'.</summary>
        public static string Slug(string name)
        {
            var chars = (name ?? "").Trim().ToLowerInvariant().Select(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' ? c : '_').ToArray();
            string id = new string(chars).Trim('_');
            while (id.Contains("__")) id = id.Replace("__", "_");
            if (id.Length == 0) id = "my_mod";
            if (id.Length > ModManifest.MaxIdLength - 4) id = id.Substring(0, ModManifest.MaxIdLength - 4).Trim('_');
            return id;
        }

        /// <summary>The id this project's name gives: its slug, numbered ("_2") when another project or mod has that.</summary>
        public string IdFromName(string name)
        {
            string id = Slug(name), unique = id;
            for (int i = 2; IdRefusal(unique) != null && i < 1000; i++) unique = $"{id}_{i}";
            return unique;
        }

        /// <summary>A project's id from its name: lower case a-z 0-9 _, made unique among the projects and installed mods.</summary>
        public static string IdFor(string name)
        {
            string id = Slug(name);
            string unique = id;
            for (int i = 2; Directory.Exists(Path.Combine(Root, unique)) || ModManager.Find(unique) != null; i++) unique = $"{id}_{i}";
            return unique;
        }

        /// <summary>A new, empty project named 'name'.</summary>
        public static ModProject Create(string name, string author)
        {
            name = string.IsNullOrWhiteSpace(name) ? "My mod" : name.Trim();
            string id = IdFor(name);
            var p = new ModProject { Folder = Path.Combine(Root, id), Id = id };
            p.manifest = new JObject
            {
                ["id"] = id, ["name"] = name, ["version"] = "1.0",
                ["author"] = string.IsNullOrWhiteSpace(author) ? "" : author.Trim(),
                ["description"] = "",
            };
            Directory.CreateDirectory(p.Folder);
            p.Save();
            Debug.Log($"Mod Builder: new project \"{name}\" ({p.Folder})");
            return p;
        }

        /// <summary>Reads a project folder (null when its mod.json can't be read).</summary>
        public static ModProject Load(string folder)
        {
            try
            {
                var p = new ModProject { Folder = folder };
                p.manifest = JObject.Parse(File.ReadAllText(Path.Combine(folder, ModManifest.FileName)));
                p.Id = Str(p.manifest, "id") ?? Path.GetFileName(folder);
                p.Modified = Directory.GetLastWriteTimeUtc(folder);
                foreach (var f in new[] { ModManifest.FileName, ModContent.ShipsFile, ModContent.ItemsFile, ModCharacters.File, BuilderFile,
                                          ModWorld.SystemsFile, ModWorld.StationsFile, InteriorsFile, ModBlueprints.File, ModGameOptions.File, CampaignFile, ProjectKit.File })
                {
                    string path = Path.Combine(folder, f);
                    if (File.Exists(path)) { var t = File.GetLastWriteTimeUtc(path); if (t > p.Modified) p.Modified = t; }
                }
                string ships = Path.Combine(folder, ModContent.ShipsFile);
                if (File.Exists(ships))
                {
                    try
                    {
                        if (JToken.Parse(File.ReadAllText(ships)) is JArray list)
                            foreach (var t in list.OfType<JObject>()) p.Ships.Add(new ProjectShip(t));
                    }
                    catch (Exception e) { p.LoadError = $"{ModContent.ShipsFile}: {e.Message}"; }
                }
                p.ReadList(ModContent.ItemsFile, o => p.Items.Add(new ProjectItem(o)));
                p.ReadList(ModCharacters.File, o => p.Characters.Add(new ProjectCharacter(o)));
                p.ReadList(ModWorld.SystemsFile, o => p.Systems.Add(new ProjectSystem(o)));
                p.ReadList(ModWorld.StationsFile, o => p.Stations.Add(new ProjectStation(o)));
                p.ReadList(InteriorsFile, o => p.Interiors.Add(new ProjectInterior(o)));
                p.ReadList(ModBlueprints.File, o => p.Blueprints.Add(new ProjectBlueprint(o)));
                p.ReadList(ProjectKit.File, o => p.Kits.Add(new ProjectKit(o)));
                p.ReadList(ModGameOptions.File, o => p.GameOptions.Add(new ProjectGameOption(o)));
                string campaign = Path.Combine(folder, CampaignFile);
                if (File.Exists(campaign))
                {
                    try { p.Campaign = new ProjectCampaign(JObject.Parse(File.ReadAllText(campaign))); }
                    catch (Exception e) { p.LoadError ??= $"{CampaignFile}: {e.Message}"; }
                }
                string events = Path.Combine(folder, ProjectQuest.Folder);
                if (Directory.Exists(events))
                    foreach (var f in Directory.GetFiles(events, "*." + ProjectQuest.Extension).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        try { p.Quests.Add(new ProjectQuest(Path.GetFileNameWithoutExtension(f), JObject.Parse(File.ReadAllText(f)))); }
                        catch (Exception e) { p.LoadError ??= $"{ProjectQuest.Folder}/{Path.GetFileName(f)}: {e.Message}"; }
                    }
                string b = Path.Combine(folder, BuilderFile);
                if (File.Exists(b)) { try { p.builder = JObject.Parse(File.ReadAllText(b)); } catch { p.builder = new JObject(); } }
                return p;
            }
            catch (Exception e) { Debug.LogWarning($"Mod Builder: can't read the project {folder}: {e.Message}"); return null; }
        }

        /// <summary>A content file's entries (a broken file is the LoadError, and the project isn't saved over it).</summary>
        void ReadList(string file, Action<JObject> add)
        {
            string path = Path.Combine(Folder, file);
            if (!File.Exists(path)) return;
            try
            {
                if (JToken.Parse(File.ReadAllText(path)) is JArray list)
                    foreach (var t in list.OfType<JObject>()) add(t);
            }
            catch (Exception e) { LoadError ??= $"{file}: {e.Message}"; }
        }

        /// <summary>Writes mod.json, ships.json, items.json, characters.json and builder.json (each through a .tmp file).</summary>
        public void Save()
        {
            if (LoadError != null) { Debug.LogWarning($"Mod Builder: {Id} not saved: {LoadError}"); return; }
            Directory.CreateDirectory(Folder);
            Write(ModManifest.FileName, manifest.ToString(Formatting.Indented));
            var list = new JArray();
            foreach (var s in Ships) list.Add(s.ToJson());
            Write(ModContent.ShipsFile, list.ToString(Formatting.Indented));
            WriteList(ModContent.ItemsFile, Items.Select(i => i.ToJson()));
            WriteList(ModCharacters.File, Characters.Select(c => c.ToJson()));
            WriteList(ModWorld.SystemsFile, Systems.Select(c => c.ToJson()));
            WriteList(ModWorld.StationsFile, Stations.Select(c => c.ToJson()));
            WriteList(InteriorsFile, Interiors.Select(c => c.ToJson()));
            WriteList(ModBlueprints.File, Blueprints.Select(c => c.ToJson()));
            WriteList(ProjectKit.File, Kits.Select(c => c.ToJson()));
            WriteList(ModGameOptions.File, GameOptions.Select(c => c.ToJson()));
            string campaign = Path.Combine(Folder, CampaignFile);
            if (Campaign != null) Write(CampaignFile, Campaign.ToJson().ToString(Formatting.Indented));
            else if (File.Exists(campaign)) File.Delete(campaign);
            WriteQuests();
            Write(BuilderFile, builder.ToString(Formatting.Indented));
            Modified = DateTime.UtcNow;
        }

        // ---- undo / redo (ModProjectHistory) ----------------------------------------------------------------------

        /// <summary>builder.json keys an undo never takes back: where the builder sits (its hangar: switching reloads the
        /// scene), the ship last worked on, the ids this project had (a rename is done on the projects page).</summary>
        static readonly string[] KeptBuilderKeys = { "station", "lastShip", "previousIds" };

        /// <summary>Everything the project's JSON files hold, as it is in memory now (the mod, ships with their hardpoints,
        /// items, characters, systems, stations, rooms, blueprints, new-game options, the campaign, the builder's quests,
        /// builder.json).</summary>
        public JObject CaptureState()
        {
            var b = (JObject)builder.DeepClone();
            foreach (var k in KeptBuilderKeys) b.Remove(k);
            var quests = new JObject();
            foreach (var q in Quests) quests[q.Name] = q.ToJson();
            return new JObject
            {
                ["manifest"] = manifest.DeepClone(),
                ["ships"] = new JArray(Ships.Select(s => s.ToJson())),
                ["items"] = new JArray(Items.Select(i => i.ToJson())),
                ["characters"] = new JArray(Characters.Select(c => c.ToJson())),
                ["systems"] = new JArray(Systems.Select(c => c.ToJson())),
                ["stations"] = new JArray(Stations.Select(c => c.ToJson())),
                ["interiors"] = new JArray(Interiors.Select(c => c.ToJson())),
                ["blueprints"] = new JArray(Blueprints.Select(c => c.ToJson())),
                ["kits"] = new JArray(Kits.Select(c => c.ToJson())),
                ["gameOptions"] = new JArray(GameOptions.Select(c => c.ToJson())),
                ["campaign"] = Campaign != null ? (JToken)Campaign.ToJson() : JValue.CreateNull(),
                ["quests"] = quests,
                ["builder"] = b,
            };
        }

        static IEnumerable<JObject> Objects(JObject state, string key) =>
            (state[key] as JArray)?.OfType<JObject>().Select(o => (JObject)o.DeepClone()) ?? Enumerable.Empty<JObject>();

        /// <summary>Puts a CaptureState back (new entry objects: look the current ones up again by their Ref); the project's
        /// id and the builder.json keys in KeptBuilderKeys stay as they are now.</summary>
        public void RestoreState(JObject state)
        {
            string id = (string)manifest["id"];
            manifest = (JObject)state["manifest"].DeepClone();
            if (id != null) manifest["id"] = id;
            Ships.Clear();
            foreach (var o in ((JArray)state["ships"]).OfType<JObject>()) Ships.Add(new ProjectShip((JObject)o.DeepClone()));
            Items.Clear();
            foreach (var o in ((JArray)state["items"]).OfType<JObject>()) Items.Add(new ProjectItem((JObject)o.DeepClone()));
            Characters.Clear();
            foreach (var o in ((JArray)state["characters"]).OfType<JObject>()) Characters.Add(new ProjectCharacter((JObject)o.DeepClone()));
            Systems.Clear(); Systems.AddRange(Objects(state, "systems").Select(o => new ProjectSystem(o)));
            Stations.Clear(); Stations.AddRange(Objects(state, "stations").Select(o => new ProjectStation(o)));
            Interiors.Clear(); Interiors.AddRange(Objects(state, "interiors").Select(o => new ProjectInterior(o)));
            Blueprints.Clear(); Blueprints.AddRange(Objects(state, "blueprints").Select(o => new ProjectBlueprint(o)));
            Kits.Clear(); Kits.AddRange(Objects(state, "kits").Select(o => new ProjectKit(o)));
            GameOptions.Clear(); GameOptions.AddRange(Objects(state, "gameOptions").Select(o => new ProjectGameOption(o)));
            Campaign = state["campaign"] is JObject c ? new ProjectCampaign((JObject)c.DeepClone()) : null;
            Quests.Clear();
            if (state["quests"] is JObject qs)
                foreach (var prop in qs.Properties()) if (prop.Value is JObject qo) Quests.Add(new ProjectQuest(prop.Name, (JObject)qo.DeepClone()));
            var b = (JObject)state["builder"].DeepClone();
            foreach (var k in KeptBuilderKeys) if (builder[k] != null) b[k] = builder[k].DeepClone();
            builder = b;
        }

        /// <summary>A content file, written once there is something in it or it was there already.</summary>
        void WriteList(string file, IEnumerable<JObject> entries)
        {
            var list = new JArray(entries);
            if (list.Count == 0 && !File.Exists(Path.Combine(Folder, file))) return;
            Write(file, list.ToString(Formatting.Indented));
        }

        void Write(string file, string text)
        {
            string path = Path.Combine(Folder, file), tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Why 'id' can't be this project's new mod id (null = it can): the mod.json rules, and no other project
        /// or installed mod (but this project's own export) may have it.</summary>
        public string IdRefusal(string id)
        {
            if (!ModManifest.ValidId(id)) return $"an id may only use a-z, 0-9, _ and - (at most {ModManifest.MaxIdLength})";
            if (string.Equals(id, Id, StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var dir in Directory.Exists(Root) ? Directory.GetDirectories(Root) : new string[0])
            {
                if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(Path.GetFileName(dir), id, StringComparison.OrdinalIgnoreCase)) return $"another project uses \"{id}\"";
                var other = File.Exists(Path.Combine(dir, ModManifest.FileName)) ? Load(dir) : null;
                if (other != null && string.Equals(other.Id, id, StringComparison.OrdinalIgnoreCase)) return $"the project \"{other.Name}\" uses \"{id}\"";
            }
            var mod = ModManager.Find(id);
            if (mod != null && !PreviousIds.Contains(mod.Id, StringComparer.OrdinalIgnoreCase))
                return $"the installed mod \"{mod.Name}\" uses \"{id}\"";
            return null;
        }

        /// <summary>The ids this project had before (builder.json "previousIds"): an export replaces a mod installed under one.</summary>
        public List<string> PreviousIds =>
            builder["previousIds"] is JArray a ? a.Select(t => t.ToString()).ToList() : new List<string>();

        /// <summary>Gives the mod a new id: mod.json, every "old:" key the project's own files name (ships, items,
        /// characters, event graphs refer to their mod's content as "id:name"), the folder (renamed when it was named after
        /// the id), the old id kept in builder.json so the next export replaces the mod installed under it. Returns the
        /// project read again from its new place (this object is done with), or null with the error.</summary>
        public ModProject ChangeId(string id, out string error)
        {
            error = IdRefusal(id);
            if (error != null) return null;
            if (string.Equals(id, Id, StringComparison.Ordinal)) return this;
            string old = Id;
            try
            {
                var prev = PreviousIds;
                if (!prev.Contains(old, StringComparer.OrdinalIgnoreCase)) prev.Add(old);
                prev.RemoveAll(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase));
                builder["previousIds"] = new JArray(prev);
                manifest["id"] = id;
                Save();
                // "old:..." references in the project's own files (not a longer id that only ends in it).
                var re = new System.Text.RegularExpressions.Regex("(?<![A-Za-z0-9_-])" + System.Text.RegularExpressions.Regex.Escape(old) + ":");
                foreach (var f in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".json" && ext != ".gof2event" && ext != ".gof2netevent" && ext != ".gof2quest") continue;
                    if (Path.GetFileName(f) == ModManifest.FileName || Path.GetFileName(f) == BuilderFile) continue;
                    string text = File.ReadAllText(f), changed = re.Replace(text, id + ":");
                    if (changed != text) File.WriteAllText(f, changed);
                }
                string folder = Folder;
                if (string.Equals(Path.GetFileName(Folder), old, StringComparison.OrdinalIgnoreCase))
                {
                    string target = Path.Combine(Root, id);
                    if (!Directory.Exists(target)) { Directory.Move(Folder, target); folder = target; }
                }
                Debug.Log($"Mod Builder: the project \"{Name}\" is now the mod \"{id}\" (was \"{old}\")");
                return Load(folder);
            }
            catch (Exception e) { error = e.Message; Debug.LogWarning($"Mod Builder: changing {old}'s id failed: {e.Message}"); return null; }
        }

        /// <summary>Deletes the project's folder.</summary>
        public bool Delete(out string error)
        {
            error = null;
            try { Directory.Delete(Folder, true); Debug.Log($"Mod Builder: deleted the project {Folder}"); return true; }
            catch (Exception e) { error = e.Message; return false; }
        }

        // ---- export ---------------------------------------------------------------------------------------------

        /// <summary>An installed mod (not this project) has the project's id, or an id it had before (ChangeId): the export
        /// replaces it.</summary>
        public ModInfo InstalledMod =>
            ModManager.Installed.FirstOrDefault(m => string.Equals(m.Id, Id, StringComparison.OrdinalIgnoreCase))
            ?? ModManager.Installed.FirstOrDefault(m => m.Id != null && PreviousIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Zips the mod's files (not builder.json) into the Mods folder as "<name>.zip" (the project's name as it is
        /// now), replacing an installed mod with its id in its own folder (it keeps its on / off and load order), or one under
        /// an id it had before (ChangeId; the new id is turned on when that was). Returns the zip's path, or null with the
        /// error.</summary>
        public string Export(out string error)
        {
            error = null;
            Save();
            try
            {
                var old = InstalledMod;
                string folder = old != null ? ModManager.FolderOf(old) : ModManager.MainFolder;
                // The mod installed under an earlier id: the new one takes its place, on if it was on.
                bool renamed = old != null && !string.Equals(old.Id, Id, StringComparison.OrdinalIgnoreCase);
                bool wasOn = renamed && ModManager.IsEnabled(old.Id);
                if (old != null && !ModImport.Delete(old, out error, keepEnabled: !renamed)) return null;
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, ZipName(folder)), tmp = target + ".tmp";
                using (var zip = new ZipArchive(File.Create(tmp), ZipArchiveMode.Create))
                    foreach (var f in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                    {
                        string rel = f.Substring(Folder.Length).TrimStart('/', '\\').Replace('\\', '/');
                        if (rel == BuilderFile || rel.EndsWith(".tmp") || rel.StartsWith(TemplatesFolder + "/")) continue;   // not the skin templates
                        var e = zip.CreateEntry(rel, System.IO.Compression.CompressionLevel.Optimal);
                        using var dst = e.Open();
                        using var src = File.OpenRead(f);
                        src.CopyTo(dst);
                    }
                if (File.Exists(target)) File.Delete(target);
                File.Move(tmp, target);
                Debug.Log($"Mod Builder: exported \"{Id}\" to {target}");
                ModManager.Scan();
                if (wasOn) ModManager.SetEnabled(Id, true);
                return target;
            }
            catch (Exception e) { error = e.Message; Debug.LogWarning($"Mod Builder: export of {Id} failed: {e.Message}"); return null; }
        }

        /// <summary>The zip's file name from the project's name (characters a file name can't hold become '_'); another
        /// file of that name in the folder (a different mod: the old one was removed first) gets the id appended.</summary>
        string ZipName(string folder)
        {
            var bad = Path.GetInvalidFileNameChars();
            string name = new string((Name ?? "").Select(c => Array.IndexOf(bad, c) >= 0 ? '_' : c).ToArray()).Trim().TrimEnd('.');
            if (name.Length > 60) name = name.Substring(0, 60).TrimEnd();
            if (name.Length == 0) name = Id;
            string file = name + ".zip";
            if (File.Exists(Path.Combine(folder, file)) || Directory.Exists(Path.Combine(folder, name))) file = $"{name} ({Id}).zip";
            return file;
        }

        // ---- ships ------------------------------------------------------------------------------------------------

        /// <summary>A new ship id (a-z 0-9 _) from a name, unique in the project.</summary>
        public string ShipIdFor(string name) => UniqueId(name, "ship", id => Ships.Any(s => s.LocalId == id));
        public string ItemIdFor(string name) => UniqueId(name, "item", id => Items.Any(s => s.LocalId == id));
        public string CharacterIdFor(string name) => UniqueId(name, "character", id => Characters.Any(s => s.Id == id));

        internal static string UniqueId(string name, string fallback, Func<string, bool> taken)
        {
            var chars = (name ?? "").Trim().ToLowerInvariant().Select(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' ? c : '_').ToArray();
            string id = new string(chars).Trim('_');
            while (id.Contains("__")) id = id.Replace("__", "_");
            if (id.Length == 0) id = fallback;
            if (id.Length > 32) id = id.Substring(0, 32).Trim('_');
            string unique = id;
            for (int i = 2; taken(unique); i++) unique = $"{id}_{i}";
            return unique;
        }

        /// <summary>Paths AddFile handed out this run: never again, even once deleted (ModMaterials keeps a texture by its
        /// path until the project is read again, so a reused name would show the old picture).</summary>
        static readonly HashSet<string> issued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Copies a file into one of the project's folders under a name not used yet (a new name for every copy, so
        /// nothing that loaded the old file shows it again); its path inside the project ("portraits/x.png").</summary>
        public string AddFile(string sourceFile, string folder, string baseName, string extension)
        {
            string dir = Path.Combine(Folder, folder);
            Directory.CreateDirectory(dir);
            string name = baseName + extension;
            for (int i = 2; File.Exists(Path.Combine(dir, name)) || issued.Contains(Path.Combine(dir, name)); i++) name = $"{baseName}_{i}{extension}";
            issued.Add(Path.Combine(dir, name));
            File.Copy(sourceFile, Path.Combine(dir, name), true);
            return folder + "/" + name;
        }

        /// <summary>Deletes a file of the project unless 'stillUsed' (a portrait something else still names).</summary>
        public void DeleteFile(string path, bool stillUsed)
        {
            if (string.IsNullOrEmpty(path) || stillUsed) return;
            try { File.Delete(Path.Combine(Folder, path)); } catch { }
        }

        // ---- skins ---------------------------------------------------------------------------------------------

        /// <summary>Where the builder saves the game's textures to paint over (never exported).</summary>
        public const string TemplatesFolder = "templates";
        static readonly string[] ImageExtensions = ImageImport.FileExtensions;   // every format the game reads

        /// <summary>The game ships the project re-skins: the ones added in the builder (builder.json "skins") and any whose
        /// texture a file in textures/ replaces ("ship_028_terran_diffuse" is ship 28's; v_ / sn_ add-on ships too). Also the
        /// project's new ships on a game model with textures of their own (textures/ships/&lt;id&gt;/, or their id in "skins"),
        /// by the number the game gave them (none while the project can't be read).</summary>
        public List<int> SkinShips()
        {
            var set = new SortedSet<int>();
            if (builder["skins"] is JArray a) foreach (var t in a) if (t.Type == JTokenType.Integer) set.Add((int)t);
            foreach (var s in Ships)
            {
                if (!OwnsSkinOf(s)) continue;
                bool listed = builder["skins"] is JArray l && l.Any(t => t.Type == JTokenType.String && (string)t == s.LocalId);
                if (!listed && SkinTextureFiles(ShipSkinScope(s)).Count == 0) continue;
                int i = ModContent.ShipIndexOf(Key(s.LocalId));
                if (i >= 0) set.Add(i);
            }
            string dir = Path.Combine(Folder, ModContent.TexturesFolder);
            var shipName = new System.Text.RegularExpressions.Regex(@"^(?:v_|sn_)?ship_(\d{3})_");
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir))
                {
                    var m = shipName.Match(Path.GetFileNameWithoutExtension(f));
                    if (m.Success && ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) set.Add(int.Parse(m.Groups[1].Value));
                }
                // a ship's own textures: textures/<its assembly>/
                foreach (var d in Directory.GetDirectories(dir))
                {
                    var m = shipName.Match(Path.GetFileName(d));
                    if (m.Success && Directory.GetFiles(d).Any(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
                        set.Add(int.Parse(m.Groups[1].Value));
                }
            }
            return set.ToList();
        }

        /// <summary>The project's replacements of game textures that aren't a ship's (stations, planets, suns, rings...):
        /// the texture names in textures/.</summary>
        public List<string> OtherTextures()
        {
            var list = new List<string>();
            string dir = Path.Combine(Folder, ModContent.TexturesFolder);
            if (!Directory.Exists(dir)) return list;
            var shipName = new System.Text.RegularExpressions.Regex(@"^(?:v_|sn_)?ship_(\d{3})_");
            foreach (var f in Directory.GetFiles(dir))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                if (!shipName.IsMatch(n) && ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) list.Add(n);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>backdrop.json (the retextured planets drawn as stars), null = none.</summary>
        public JObject Backdrop
        {
            get
            {
                string f = Path.Combine(Folder, "backdrop.json");
                try { return File.Exists(f) ? JToken.Parse(File.ReadAllText(f)) as JObject : null; }
                catch { return null; }
            }
            set
            {
                string f = Path.Combine(Folder, "backdrop.json");
                if (value == null || !value.HasValues) { try { if (File.Exists(f)) File.Delete(f); } catch { } return; }
                File.WriteAllText(f, value.ToString(Formatting.Indented));
            }
        }

        public void AddSkin(int ship)
        {
            if (!(builder["skins"] is JArray a)) builder["skins"] = a = new JArray();
            // The project's own ship by its id (its number is only this run's).
            if (OwnSkinShip(ship) is ProjectShip own) { if (!a.Any(t => t.Type == JTokenType.String && (string)t == own.LocalId)) a.Add(own.LocalId); return; }
            if (!a.Any(t => t.Type == JTokenType.Integer && (int)t == ship)) a.Add(ship);
        }

        public void RemoveSkin(int ship)
        {
            if (!(builder["skins"] is JArray a)) return;
            string id = OwnSkinShip(ship)?.LocalId;
            foreach (var t in a.Where(t => t.Type == JTokenType.Integer && (int)t == ship || id != null && t.Type == JTokenType.String && (string)t == id).ToList())
                t.Remove();
        }

        /// <summary>A new ship of the project that flies one of the game's models: it can have textures of its own.</summary>
        public static bool OwnsSkinOf(ProjectShip s) => s != null && !s.IsOverride && s.HasGameModel && !string.IsNullOrEmpty(s.LocalId);

        /// <summary>The project's new ship on a game model with game number 'ship' (null: none, or a game ship).</summary>
        public ProjectShip OwnSkinShip(int ship) =>
            ship < 0 ? null : Ships.FirstOrDefault(s => OwnsSkinOf(s) && ModContent.ShipIndexOf(Key(s.LocalId)) == ship);

        /// <summary>Where a new ship's own textures go under textures/ ("ships/&lt;id&gt;", ModContent.ShipTexturesFolder; the
        /// SkinTexture / SetSkinTexture scope), null when it can't have any.</summary>
        public static string ShipSkinScope(ProjectShip s) =>
            OwnsSkinOf(s) ? ModContent.ShipTexturesFolder.Substring(ModContent.TexturesFolder.Length + 1) + "/" + s.LocalId : null;

        /// <summary>The pictures under textures/&lt;scope&gt;/ (texture names), empty for none.</summary>
        public List<string> SkinTextureFiles(string scope)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(scope)) return list;
            string dir = Path.Combine(Folder, ModContent.TexturesFolder, scope);
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir))
                if (ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) list.Add(Path.GetFileNameWithoutExtension(f));
            return list;
        }

        /// <summary>The project's replacement of a game texture (textures/&lt;name&gt;.png or .jpg; with a scope, the one
        /// for that assembly's objects only, textures/&lt;scope&gt;/&lt;name&gt;), null = none.</summary>
        public string SkinTexture(string textureName, string scope = null)
        {
            string folder = scope == null ? ModContent.TexturesFolder : ModContent.TexturesFolder + "/" + scope;
            foreach (var ext in ImageExtensions)
            {
                string rel = folder + "/" + textureName + ext;
                if (File.Exists(Path.Combine(Folder, rel))) return rel;
            }
            return null;
        }

        /// <summary>A PNG as the replacement of a game texture, for every object or (scope) one assembly's only (any other
        /// one of that name there goes).</summary>
        public void SetSkinTexture(string textureName, string sourceFile, string scope = null)
        {
            RemoveSkinTexture(textureName, scope);
            string dir = scope == null ? Path.Combine(Folder, ModContent.TexturesFolder) : Path.Combine(Folder, ModContent.TexturesFolder, scope);
            Directory.CreateDirectory(dir);
            File.Copy(sourceFile, Path.Combine(dir, textureName + ".png"), true);
        }

        public void RemoveSkinTexture(string textureName, string scope = null)
        {
            string dir = scope == null ? Path.Combine(Folder, ModContent.TexturesFolder) : Path.Combine(Folder, ModContent.TexturesFolder, scope);
            foreach (var ext in ImageExtensions)
            {
                string path = Path.Combine(dir, textureName + ext);
                if (File.Exists(path)) File.Delete(path);
            }
            if (scope != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }

        // ---- items and characters ------------------------------------------------------------------------------

        /// <summary>A new item: a copy of the game item 'baseItem' under its own name.</summary>
        public ProjectItem AddItem(int baseItem, string name)
        {
            var it = new ProjectItem(new JObject { ["id"] = ItemIdFor(name), ["base"] = baseItem, ["name"] = name, ["description"] = "" });
            Items.Add(it);
            return it;
        }

        /// <summary>A change to the game item 'item' (nothing changed until a field is given).</summary>
        public ProjectItem AddItemOverride(int item)
        {
            var it = new ProjectItem(new JObject { ["override"] = item });
            Items.Add(it);
            return it;
        }

        /// <summary>A new character with its portrait (a path in the project).</summary>
        public ProjectCharacter AddCharacter(string name, string portrait)
        {
            var c = new ProjectCharacter(new JObject
            {
                ["id"] = CharacterIdFor(name), ["name"] = name, ["portrait"] = portrait, ["race"] = "terran", ["gender"] = "male",
            });
            Characters.Add(c);
            return c;
        }

        // ---- the other kinds ------------------------------------------------------------------------------------

        public string SystemIdFor(string name) => UniqueId(name, "system", id => Systems.Any(s => s.LocalId == id));
        public string StationIdFor(string name) => UniqueId(name, "station", id => Stations.Any(s => s.LocalId == id));
        public string InteriorIdFor(string name) => UniqueId(name, "room", id => Interiors.Any(s => s.Id == id));
        public string BlueprintIdFor(string name) => UniqueId(name, "blueprint", id => Blueprints.Any(s => s.Id == id));
        public string KitIdFor(string name) => UniqueId(name, "kit", id => Kits.Any(s => s.Id == id));
        public string GameOptionIdFor(string name) => UniqueId(name, "option", id => GameOptions.Any(s => s.Id == id));
        /// <summary>A quest's file name: one word (a-z 0-9 _), unique among the project's quests and event graphs.</summary>
        public string QuestNameFor(string name) => UniqueId(name, "quest", id => Quests.Any(q => string.Equals(q.Name, id, StringComparison.OrdinalIgnoreCase))
                                                                          || File.Exists(Path.Combine(Folder, ProjectQuest.Folder, id + ".gof2event")));

        /// <summary>The key other content names one of the project's own entries by ("my_mod:kepler").</summary>
        public string Key(string localId) => Id + ":" + localId;

        public ProjectSystem AddSystem(string name, Vector3 position, int race)
        {
            var s = new ProjectSystem(new JObject { ["id"] = SystemIdFor(name), ["name"] = name, ["race"] = race, ["security"] = 2 });
            s.Position = position;
            Systems.Add(s);
            return s;
        }

        /// <summary>A change to one of the game's (or another mod's) systems.</summary>
        public ProjectSystem AddSystemOverride(string system)
        {
            var s = new ProjectSystem(new JObject { ["override"] = ProjectEntry.RefToken(system) });
            Systems.Add(s);
            return s;
        }

        public ProjectStation AddStation(string name, string system, int techLevel, int planet)
        {
            var s = new ProjectStation(new JObject { ["id"] = StationIdFor(name), ["name"] = name, ["system"] = ProjectEntry.RefToken(system),
                                                     ["techLevel"] = techLevel, ["planet"] = planet });
            Stations.Add(s);
            return s;
        }

        public ProjectStation AddStationOverride(string station)
        {
            var s = new ProjectStation(new JObject { ["override"] = ProjectEntry.RefToken(station) });
            Stations.Add(s);
            return s;
        }

        public ProjectInterior AddInterior(string name, bool bar, string model)
        {
            var r = new ProjectInterior(new JObject { ["id"] = InteriorIdFor(name), ["type"] = bar ? "bar" : "hangar", ["model"] = model });
            Interiors.Add(r);
            return r;
        }

        public ProjectBlueprint AddBlueprint(string name, bool ship, string product)
        {
            var b = new ProjectBlueprint(new JObject { ["id"] = BlueprintIdFor(name) });
            b.SetProduct(ship, product);
            b.Ingredients = new List<ProjectStack>();
            Blueprints.Add(b);
            return b;
        }

        /// <summary>A new kit with no parts yet (the game skips it until it has one).</summary>
        public ProjectKit AddKit(string name)
        {
            var k = new ProjectKit(new JObject { ["id"] = KitIdFor(name), ["name"] = name, ["scale"] = 1, ["slots"] = new JArray("body", "wings", "rear"), ["parts"] = new JArray() });
            Kits.Add(k);
            return k;
        }

        public ProjectKit FindKit(string id) => Kits.Find(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>A new customizable ship flying 'kit' (this project's): sensible stats, its type 'kitType', the default
        /// build each slot's first part (the game's Normalize does that for a slot left out).</summary>
        public ProjectShip AddKitShip(string id, string name, ProjectKit kit, string kitType)
        {
            var s = new ProjectShip(new JObject
            {
                ["id"] = id, ["name"] = name, ["description"] = "",
                ["race"] = 0, ["armor"] = 300, ["cargo"] = 60, ["handling"] = 110,
                ["price"] = 400000,
                ["slots"] = new JObject { ["primary"] = 2, ["secondary"] = 1, ["turret"] = 0, ["equipment"] = 8 },
                ["kit"] = kit.Id, ["kitType"] = kitType, ["build"] = new JObject { ["tier"] = 0 },
                ["mounts"] = new JArray(),
            });
            Ships.Add(s);
            return s;
        }

        public ProjectGameOption AddGameOption(string name)
        {
            var o = new ProjectGameOption(new JObject { ["id"] = GameOptionIdFor(name), ["name"] = name });
            GameOptions.Add(o);
            return o;
        }

        public ProjectQuest AddQuest(string title, bool mission)
        {
            var q = new ProjectQuest(QuestNameFor(title), new JObject { ["title"] = title, ["kind"] = mission ? "mission" : "quest" });
            Quests.Add(q);
            return q;
        }

        /// <summary>A quest from a whole .gof2quest object (an event graph converted to steps, QuestFile.FromScript).</summary>
        public ProjectQuest AddQuest(string name, JObject json)
        {
            var q = new ProjectQuest(QuestNameFor(string.IsNullOrWhiteSpace(name) ? (string)json["title"] ?? "quest" : name), json);
            Quests.Add(q);
            return q;
        }

        /// <summary>An event graph made in the Unity Editor copied into events/ as it is (it runs like the quests).</summary>
        public string AddEventGraph(string sourceFile)
        {
            string name = UniqueId(Path.GetFileNameWithoutExtension(sourceFile), "event",
                id => Quests.Any(q => string.Equals(q.Name, id, StringComparison.OrdinalIgnoreCase)) || EventGraphs().Any(g => string.Equals(g, id, StringComparison.OrdinalIgnoreCase)));
            string dir = Path.Combine(Folder, ProjectQuest.Folder);
            Directory.CreateDirectory(dir);
            File.Copy(sourceFile, Path.Combine(dir, name + ".gof2event"), true);
            return name;
        }

        /// <summary>An event graph's file in the project (.gof2event, or the old .gof2netevent), or null.</summary>
        public string EventGraphPath(string name)
        {
            foreach (string ext in new[] { ".gof2event", ".gof2netevent" })
            {
                string f = Path.Combine(Folder, ProjectQuest.Folder, name + ext);
                if (File.Exists(f)) return f;
            }
            return null;
        }

        public void RemoveEventGraph(string name)
        {
            string f = EventGraphPath(name);
            if (f != null) try { File.Delete(f); } catch { }
        }

        /// <summary>events/&lt;name&gt;.gof2quest for every quest; a builder quest file no longer in the list goes.</summary>
        void WriteQuests()
        {
            string dir = Path.Combine(Folder, ProjectQuest.Folder);
            if (Quests.Count > 0) Directory.CreateDirectory(dir);
            foreach (var q in Quests) Write(ProjectQuest.Folder + "/" + q.Name + "." + ProjectQuest.Extension, q.ToJson().ToString(Formatting.Indented));
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*." + ProjectQuest.Extension))
                if (!Quests.Any(q => string.Equals(q.Name, Path.GetFileNameWithoutExtension(f), StringComparison.OrdinalIgnoreCase)))
                    try { File.Delete(f); } catch { }
        }

        /// <summary>The event graphs made in the Unity Editor (events/*.gof2event): listed, not edited by the builder.</summary>
        public List<string> EventGraphs()
        {
            string dir = Path.Combine(Folder, ProjectQuest.Folder);
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, "*.gof2event").Concat(Directory.GetFiles(dir, "*.gof2netevent"))
                            .Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ---- audio: plain files ---------------------------------------------------------------------------------

        public const string MusicFolder = "music", SoundsFolder = "sounds", VoicesFolder = "voices";
        public static readonly string[] AudioExtensions = { ".ogg", ".wav", ".mp3" };

        /// <summary>The audio files of a folder ("music", "sounds", "voices", "voices/de"): name without extension -> path in the
        /// project.</summary>
        public List<(string name, string path)> AudioFiles(string folder)
        {
            string dir = Path.Combine(Folder, folder);
            if (!Directory.Exists(dir)) return new List<(string, string)>();
            return Directory.GetFiles(dir).Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .Select(f => (Path.GetFileNameWithoutExtension(f), folder + "/" + Path.GetFileName(f))).ToList();
        }

        /// <summary>An audio file into a folder under 'name' (any other file of that name there goes: one per name).</summary>
        public string SetAudio(string folder, string name, string sourceFile)
        {
            RemoveAudio(folder, name);
            string dir = Path.Combine(Folder, folder);
            Directory.CreateDirectory(dir);
            string ext = Path.GetExtension(sourceFile).ToLowerInvariant();
            if (!AudioExtensions.Contains(ext)) ext = ".ogg";
            File.Copy(sourceFile, Path.Combine(dir, name + ext), true);
            return folder + "/" + name + ext;
        }

        public void RemoveAudio(string folder, string name)
        {
            string dir = Path.Combine(Folder, folder);
            foreach (var ext in AudioExtensions)
            {
                string f = Path.Combine(dir, name + ext);
                if (File.Exists(f)) File.Delete(f);
            }
        }

        // ---- dependencies (mod.json) ------------------------------------------------------------------------------

        /// <summary>The mods it depends on (ids; "id>=version" entries by their id).</summary>
        public List<string> Dependencies =>
            manifest["dependencies"] is JArray a ? a.Select(t => t.ToString()).Select(d => { int ge = d.IndexOf(">=", StringComparison.Ordinal); return (ge > 0 ? d.Substring(0, ge) : d).Trim(); })
                                                    .Where(d => d.Length > 0).ToList() : new List<string>();

        /// <summary>Makes 'mod' a dependency (its version as the lowest needed); false when it was one already.</summary>
        public bool AddDependency(string mod, string version = null)
        {
            if (string.IsNullOrEmpty(mod) || string.Equals(mod, Id, StringComparison.OrdinalIgnoreCase)) return false;
            if (Dependencies.Contains(mod, StringComparer.OrdinalIgnoreCase)) return false;
            if (!(manifest["dependencies"] is JArray a)) manifest["dependencies"] = a = new JArray();
            a.Add(string.IsNullOrEmpty(version) ? mod : mod + ">=" + version);
            return true;
        }

        public void RemoveDependency(string mod)
        {
            if (!(manifest["dependencies"] is JArray a)) return;
            foreach (var t in a.ToList())
            {
                string d = t.ToString();
                int ge = d.IndexOf(">=", StringComparison.Ordinal);
                if (string.Equals((ge > 0 ? d.Substring(0, ge) : d).Trim(), mod, StringComparison.OrdinalIgnoreCase)) t.Remove();
            }
            if (a.Count == 0) manifest.Remove("dependencies");
        }

        /// <summary>The mods a key in the project's files names ("other_mod:..."), not its own and not "gof2:" / "quest:": what
        /// ModRefs should have made a dependency.</summary>
        public HashSet<string> ModsNamed()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var re = new System.Text.RegularExpressions.Regex("\"([a-z0-9_-]{1,40}):[a-z0-9_-]+\"");
            foreach (var t in CaptureState().Properties().Where(p => p.Name != "manifest" && p.Name != "builder"))
                foreach (System.Text.RegularExpressions.Match m in re.Matches(t.Value.ToString(Formatting.None)))
                {
                    string mod = m.Groups[1].Value;
                    if (mod != Id && mod != GameModels.Prefix.TrimEnd(':') && mod != "id" && mod != "override") set.Add(mod);
                }
            return set;
        }

        /// <summary>Copies a GLB into the project's models folder; its path inside the project ("models/<name>.glb").</summary>
        public string AddModel(string sourceFile, string baseName)
        {
            string dir = Path.Combine(Folder, ModelsFolder);
            Directory.CreateDirectory(dir);
            string name = baseName + ".glb";
            for (int i = 2; File.Exists(Path.Combine(dir, name)); i++) name = $"{baseName}_{i}.glb";
            File.Copy(sourceFile, Path.Combine(dir, name), true);
            return ModelsFolder + "/" + name;
        }

        /// <summary>Copies a PNG into the project's textures folder; its path inside the project ("textures/<name>.png").</summary>
        public string AddTexture(string sourceFile, string baseName)
        {
            string dir = Path.Combine(Folder, "textures");
            Directory.CreateDirectory(dir);
            string name = baseName + ".png";
            File.Copy(sourceFile, Path.Combine(dir, name), true);
            return "textures/" + name;
        }

        /// <summary>A new ship from a model file in the project: sensible stats, no hardpoints yet (the builder places
        /// defaults once the model is built).</summary>
        public ProjectShip AddNewShip(string id, string name, string modelPath)
        {
            var s = new ProjectShip(new JObject
            {
                ["id"] = id, ["name"] = name, ["description"] = "",
                ["race"] = 0, ["armor"] = 300, ["cargo"] = 60, ["handling"] = 110,
                ["price"] = 400000,
                ["slots"] = new JObject { ["primary"] = 2, ["secondary"] = 1, ["turret"] = 0, ["equipment"] = 8 },
                ["model"] = modelPath, ["modelLength"] = 1000, ["modelYaw"] = 0, ["hangarHeight"] = 250,
                ["mounts"] = new JArray(),
            }) { NeedsLayout = true };
            Ships.Add(s);
            return s;
        }

        /// <summary>An override of an original ship: its stats and hardpoints copied, so they can be changed.</summary>
        public ProjectShip AddOverride(Database db, int ship)
        {
            var s = new ProjectShip(new JObject { ["override"] = ship });
            s.FillFromGame(db.Ship(ship));
            s.Mounts.AddRange((db.WeaponMounts.Find(w => w.ship == ship)?.mounts ?? new List<WeaponMount>()).Select(ProjectShip.Copy));
            Ships.Add(s);
            return s;
        }

        // ---- builder.json --------------------------------------------------------------------------------------

        JObject Section(string key)
        {
            if (!(builder[key] is JObject o)) builder[key] = o = new JObject();
            return o;
        }

        /// <summary>A ship's test loadout (item, amount), null = none chosen yet.</summary>
        public List<ItemStack> Loadout(ProjectShip s)
        {
            if (!(Section("loadouts")[s.Ref] is JArray a)) return null;
            var l = new List<ItemStack>();
            foreach (var e in a.OfType<JArray>()) if (e.Count >= 2) l.Add(new ItemStack((int)e[0], (int)e[1]));
            return l;
        }

        public void SetLoadout(ProjectShip s, List<ItemStack> items)
        {
            var a = new JArray();
            foreach (var i in items) a.Add(new JArray(i.item, i.amount));
            Section("loadouts")[s.Ref] = a;
        }

        /// <summary>The icon the builder made for a ship (ShipIconMaker): its file and the look it was made from, null = none.
        /// A ships.json "icon" that isn't this file is the modder's own and is never replaced.</summary>
        public (string file, string key)? GeneratedIcon(ProjectShip s) =>
            Section("icons")[s.Ref] is JObject o ? ((string)o["file"], (string)o["key"]) : ((string, string)?)null;

        /// <summary>Writes a generated icon to icons/&lt;id&gt;.png and makes it the ship's icon.</summary>
        public void SetGeneratedIcon(ProjectShip s, byte[] png, string key)
        {
            string dir = Path.Combine(Folder, "icons");
            Directory.CreateDirectory(dir);
            string file = "icons/" + (s.LocalId ?? "ship") + ".png";
            File.WriteAllBytes(Path.Combine(Folder, file), png);
            s.Icon = file;
            Section("icons")[s.Ref] = new JObject { ["file"] = file, ["key"] = key };
        }

        public bool Flag(string key, bool fallback) => builder[key] is JValue v && v.Type == JTokenType.Boolean ? (bool)v : fallback;
        public void SetFlag(string key, bool on) => builder[key] = on;

        /// <summary>The station the builder sits at and test-flies from (ModBuilder.Station), -1 = the default.</summary>
        public int BuilderStation
        {
            get => builder["station"] is JValue v && v.Type == JTokenType.Integer ? (int)v : -1;
            set => builder["station"] = value;
        }

        /// <summary>The ship last worked on (its Ref), null = none.</summary>
        public string LastShip { get => Str(builder, "lastShip"); set => builder["lastShip"] = value; }

        /// <summary>A loadout key moves with a renamed ship.</summary>
        public void RenameRef(string from, string to)
        {
            var l = Section("loadouts");
            if (l[from] != null) { l[to] = l[from]; l.Remove(from); }
            var ic = Section("icons");
            if (ic[from] != null) { ic[to] = ic[from]; ic.Remove(from); }
            if (LastShip == from) LastShip = to;
        }
    }

    /// <summary>One ships.json entry of a project: a new ship ("id") or an override of an original ship ("override").</summary>
    public class ProjectShip
    {
        readonly JObject json;
        /// <summary>The hardpoints (ships.json "mounts"): slotType 0 primary, 1 secondary, 2 turret, 3 engine exhaust.</summary>
        public readonly List<WeaponMount> Mounts = new List<WeaponMount>();
        /// <summary>A new ship whose default hardpoints haven't been placed yet (done once its model is built).</summary>
        public bool NeedsLayout;

        public ProjectShip(JObject o)
        {
            json = o;
            ReadMounts();
        }

        /// <summary>The hardpoints read again from the JSON (after Edit as JSON).</summary>
        public void ReadMounts()
        {
            Mounts.Clear();
            if (json["mounts"] is JArray a)
                foreach (var m in a.OfType<JObject>())
                {
                    try { Mounts.Add(JsonUtility.FromJson<WeaponMount>(m.ToString())); } catch { }
                }
        }

        /// <summary>The entry's JSON itself (Edit as JSON); the less common fields read and written raw.</summary>
        public JObject Json => json;
        public JToken Get(string k) => json[k];
        public void Set(string k, JToken v) { if (v == null || v.Type == JTokenType.Null) json.Remove(k); else json[k] = v; }

        public static WeaponMount Copy(WeaponMount m) => new WeaponMount
        {
            slotType = m.slotType, upsideDown = m.upsideDown,
            position_engine = m.position_engine != null ? (int[])m.position_engine.Clone() : new int[3],
            turretAngles = m.turretAngles != null ? (float[])m.turretAngles.Clone() : null,
            glowColor = m.glowColor != null ? (float[])m.glowColor.Clone() : null,
            glowSize = m.glowSize != null ? (float[])m.glowSize.Clone() : null,
            turretUp = m.turretUp != null ? (float[])m.turretUp.Clone() : null,
            outline = m.outline != null ? (float[])m.outline.Clone() : null, builtIn = m.builtIn,
            direction = m.direction != null ? (float[])m.direction.Clone() : null,
            rotation = m.rotation != null ? (float[])m.rotation.Clone() : null,
            nodes = m.nodes != null ? (string[])m.nodes.Clone() : null, fxScale = m.fxScale,
        };

        public bool IsOverride => json["override"] != null;
        public int OverrideIndex => IsOverride && int.TryParse(json["override"].ToString(), out int i) ? i : -1;
        public string LocalId => json["id"] is JValue v ? (string)v : null;
        /// <summary>The key builder.json files it under: "id:<id>" / "override:<n>".</summary>
        public string Ref => IsOverride ? "override:" + OverrideIndex : "id:" + LocalId;

        string S(string k) => json[k] is JValue v && v.Type == JTokenType.String ? (string)v : null;
        int I(string k, int d) => json[k] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? Mathf.RoundToInt((float)v) : d;
        float F(string k, float d) => json[k] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (float)v : d;

        public string Name { get => S("name"); set { if (string.IsNullOrEmpty(value)) json.Remove("name"); else json["name"] = value; } }
        public string Description { get => S("description") ?? ""; set => json["description"] = value ?? ""; }
        public string Model { get => S("model"); set { if (string.IsNullOrEmpty(value)) json.Remove("model"); else json["model"] = value; } }
        /// <summary>The shop icon (a 180 x 88 PNG in the project), null = none.</summary>
        public string Icon { get => S("icon"); set { if (string.IsNullOrEmpty(value)) json.Remove("icon"); else json["icon"] = value; } }
        public int Race { get => I("race", 0); set => json["race"] = value; }
        public int Armor { get => I("armor", 100); set => json["armor"] = value; }
        public int Cargo { get => I("cargo", 0); set => json["cargo"] = value; }
        public int Handling { get => I("handling", 100); set => json["handling"] = value; }
        public int Price { get => I("price", 0); set => json["price"] = value; }
        public int HangarHeight { get => I("hangarHeight", 250); set => json["hangarHeight"] = value; }
        public float ModelLength { get => F("modelLength", 1000f); set => json["modelLength"] = Mathf.Round(value); }
        public float ModelYaw { get => F("modelYaw", 0f); set => json["modelYaw"] = Mathf.Round(value); }
        public float ModelPitch { get => F("modelPitch", 0f); set => SetOrRemove("modelPitch", Mathf.Round(value)); }
        public float ModelRoll { get => F("modelRoll", 0f); set => SetOrRemove("modelRoll", Mathf.Round(value)); }

        void SetOrRemove(string key, float v) { if (v == 0f) json.Remove(key); else json[key] = v; }

        /// <summary>modelOffset (game units, x y z in the ship's frame); zeros = none.</summary>
        public Vector3Int ModelOffset
        {
            get => json["modelOffset"] is JArray a && a.Count >= 3
                ? new Vector3Int(Mathf.RoundToInt((float)a[0]), Mathf.RoundToInt((float)a[1]), Mathf.RoundToInt((float)a[2])) : Vector3Int.zero;
            set { if (value == Vector3Int.zero) json.Remove("modelOffset"); else json["modelOffset"] = new JArray(value.x, value.y, value.z); }
        }

        JObject Slots
        {
            get
            {
                if (!(json["slots"] is JObject o)) json["slots"] = o = new JObject();
                return o;
            }
        }

        /// <summary>Slot count by kind: 0 primary, 1 secondary, 2 turret, 3 equipment.</summary>
        public int SlotCount(int kind)
        {
            string k = SlotKey(kind);   // read without making a "slots" object (FillFromGame looks for a missing one)
            return json["slots"] is JObject o && o[k] is JValue v && v.Type == JTokenType.Integer ? (int)v : 0;
        }

        public void SetSlotCount(int kind, int count) => Slots[SlotKey(kind)] = Mathf.Clamp(count, 0, 99);

        /// <summary>The builder's hull textures: one "materials" entry for every mesh (ships.json; ModMaterials.FromSpec), its
        /// 'role' "diffuse", "normal" or "metallicSmoothness" (null = none). False when the entry list is more than the builder
        /// manages (several entries, or one limited to some meshes: a hand edit), which it then leaves alone.</summary>
        public bool HullTexturesEditable => !(json["materials"] is JArray a) || a.Count == 0
                                            || a.Count == 1 && a[0] is JObject o && string.IsNullOrEmpty((string)o["mesh"]) && o["submesh"] == null;

        public string HullTexture(string role) =>
            json["materials"] is JArray a && a.Count == 1 && a[0] is JObject o && o[role] is JValue v ? (string)v : null;

        public void SetHullTexture(string role, string path)
        {
            if (!HullTexturesEditable) return;
            var o = json["materials"] is JArray a && a.Count == 1 && a[0] is JObject e ? e : new JObject { ["smoothness"] = 1 };
            if (string.IsNullOrEmpty(path)) o.Remove(role); else o[role] = path;
            bool any = o["diffuse"] != null || o["normal"] != null || o["metallicSmoothness"] != null;
            if (any) json["materials"] = new JArray(o); else json.Remove("materials");
        }

        static string SlotKey(int kind) => kind == 0 ? "primary" : kind == 1 ? "secondary" : kind == 2 ? "turret" : "equipment";

        public int MountCount(int slotType) => Mounts.Count(m => m.slotType == slotType);

        // ---- a customizable ship (ModShipKits) ----

        /// <summary>The kit it flies ("kit": this mod's kit id, or "mod_id:kit_id"), null = a ship with a model.</summary>
        public string Kit { get => S("kit"); set { if (string.IsNullOrEmpty(value)) json.Remove("kit"); else json["kit"] = value; } }
        public bool IsKitShip => !string.IsNullOrEmpty(Kit);
        /// <summary>Which of the kit's parts it may fit (the parts' "types").</summary>
        public string KitType { get => S("kitType") ?? ""; set => json["kitType"] = value ?? ""; }

        /// <summary>The default build's part in a slot (null = the slot's first part the type may fit).</summary>
        public string BuildPart(string slot) => json["build"] is JObject b && b[slot] is JValue v && v.Type == JTokenType.String ? (string)v : null;

        public void SetBuildPart(string slot, string part)
        {
            if (!(json["build"] is JObject b)) json["build"] = b = new JObject();
            if (string.IsNullOrEmpty(part)) b.Remove(slot); else b[slot] = part;
        }

        public int BuildTier
        {
            get => json["build"] is JObject b && b["tier"] is JValue v && v.Type == JTokenType.Integer ? (int)v : 0;
            set { if (!(json["build"] is JObject b)) json["build"] = b = new JObject(); b["tier"] = Mathf.Max(0, value); }
        }

        /// <summary>The default build's parts as build text ("slot=part;...;tier=N"), for a preview of another part.</summary>
        public string BuildText()
        {
            if (!(json["build"] is JObject b)) return "tier=0";
            var parts = b.Properties().Where(p => p.Value.Type == JTokenType.String && p.Name != "skin" && p.Name != "weaponColor"
                                                  && !p.Name.StartsWith("exhaust")).Select(p => p.Name + "=" + (string)p.Value);
            return string.Join(";", parts.Append("tier=" + BuildTier));
        }

        /// <summary>The ship flies a kit: its model's own fields go.</summary>
        public void UseKit(string kit, string kitType)
        {
            foreach (var k in new[] { "model", "modelLength", "modelYaw", "modelPitch", "modelRoll", "modelOffset", "materials", "hangarHeight", "engineGlowRadius" })
                json.Remove(k);
            Kit = kit;
            KitType = kitType;
            if (!(json["build"] is JObject)) json["build"] = new JObject { ["tier"] = 0 };
            NeedsLayout = false;
        }

        /// <summary>The ship flies one of the game's own models ("gof2:<name>", GameModels), not a file.</summary>
        public bool HasGameModel => GameModels.Is(Model);

        /// <summary>Remake (GameModels): the ship flies one of the game's models ('assembly', no file): the file model's sizing,
        /// turns, offset, hull textures and hangar height go (the game model sits as made, on the pads like its ship), and
        /// the hardpoints become 'mounts' (that ship's, copied; empty for a model that is no ship's). 'from': that ship's
        /// stats, slots and race for a new ship (null = keep them).</summary>
        public void UseGameModel(string assembly, ShipData from, IEnumerable<WeaponMount> mounts, int race = -1)
        {
            Model = GameModels.Ref(assembly);
            foreach (var k in new[] { "modelLength", "modelYaw", "modelPitch", "modelRoll", "modelOffset", "materials", "hangarHeight", "engineGlowRadius" })
                json.Remove(k);
            Mounts.Clear();
            if (mounts != null) Mounts.AddRange(mounts.Select(Copy));
            NeedsLayout = false;   // the model's own hardpoints, not the builder's first guess from its box
            if (from == null) return;
            Armor = from.armor;
            Cargo = from.cargo;
            Handling = Mathf.RoundToInt(from.handling);
            Price = from.price;
            int[] counts = { from.slots?.primary ?? 0, from.slots?.secondary ?? 0, from.slots?.turret ?? 0, from.slots?.equipment ?? 0 };
            for (int k = 0; k < 4; k++) SetSlotCount(k, counts[k]);
            if (race >= 0) Race = race;
        }

        /// <summary>An override's stats and slots the entry doesn't give, from the game's ship (they are the game's anyway):
        /// the builder shows and counts what the ship really has (a hand-written override without "slots" had none).</summary>
        public void FillFromGame(ShipData d)
        {
            if (d == null) return;
            if (json["armor"] == null) json["armor"] = d.armor;
            if (json["cargo"] == null) json["cargo"] = d.cargo;
            if (json["handling"] == null) json["handling"] = Mathf.RoundToInt(d.handling);
            if (json["price"] == null) json["price"] = d.price;
            var slots = Slots;
            int[] counts = { d.slots?.primary ?? 0, d.slots?.secondary ?? 0, d.slots?.turret ?? 0, d.slots?.equipment ?? 0 };
            for (int k = 0; k < 4; k++) if (slots[SlotKey(k)] == null) slots[SlotKey(k)] = counts[k];
        }

        /// <summary>The entry as written to ships.json (the hardpoints from Mounts).</summary>
        public JObject ToJson()
        {
            var o = (JObject)json.DeepClone();
            o["mounts"] = new JArray(Mounts.Select(MountJson));
            return o;
        }

        /// <summary>A hardpoint as ships.json / shipkits.json write it (the remake's mod fields only when set: a mount the
        /// builder doesn't edit keeps them).</summary>
        public static JObject MountJson(WeaponMount m)
        {
            JArray Floats(float[] a, int digits) => new JArray(a.Select(f => (float)Math.Round(f, digits)));
            var e = new JObject { ["slotType"] = m.slotType, ["position_engine"] = new JArray(m.position_engine ?? new int[3]) };
            if (m.turretAngles != null && m.turretAngles.Length > 0) e["turretAngles"] = Floats(m.turretAngles, 3);
            // A turret's facing: turretUp, but a plain top / under-hull one as before (none / upsideDown).
            var up = m.TurretUpLocal;
            if (m.slotType == 2 && m.turretUp != null && m.turretUp.Length >= 3 && up.y < 0.9999f && up.y > -0.9999f)
                e["turretUp"] = Floats(m.turretUp, 3);
            else if (m.slotType == 2 && up.y <= -0.9999f) e["upsideDown"] = true;
            else if (m.slotType != 2 && m.upsideDown) e["upsideDown"] = true;
            if (m.glowColor != null && m.glowColor.Length >= 3) e["glowColor"] = Floats(m.glowColor, 3);
            if (m.glowSize != null && m.glowSize.Length >= 3) e["glowSize"] = Floats(m.glowSize, 1);
            if (m.outline != null && m.outline.Length >= 6) e["outline"] = Floats(m.outline, 1);
            if (m.builtIn) e["builtIn"] = true;
            if (m.direction != null && m.direction.Length >= 3) e["direction"] = Floats(m.direction, 3);
            if (m.rotation != null && m.rotation.Length >= 3) e["rotation"] = Floats(m.rotation, 3);
            if (m.nodes != null && m.nodes.Length > 0) e["nodes"] = new JArray(m.nodes);
            if (m.fxScale > 0f) e["fxScale"] = (float)Math.Round(m.fxScale, 3);
            return e;
        }
    }
}
