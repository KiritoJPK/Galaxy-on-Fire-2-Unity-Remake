// ModTextures.cs
// Remake mods: texture replacements (skins). A mod that is on ships files in its textures folder (textures/<name>.png | .jpg)
// named like one of the game's textures (the file names in Assets/Textures/<pack>/..., e.g. ship_028_terran_diffuse for the
// Veteran's hull, ship_028_terran_normal_specular for its normal map): each replaces that texture on every assembled object
// that uses it (ships in flight, the NPCs, the hangar, the item window; AssembledObject.Awake -> Apply) and on the backdrop's
// planets, suns and rings (planet_000_small...; Backdrop.Load -> Replace), the object getting a
// copy of the material with the mod's texture (the game's material assets are never changed). The image must have the
// original's layout (the same UV mapping); its size may differ. A later mod in the load order wins. Names with "_normal" or
// "_metallic" load as linear data, the rest as colour. Loaded in the background when the mods change (Preload; the main
// menu's loading screen waits, ModLoading).

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModTextures
    {
        public const string Folder = "textures";
        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
        // The texture slots of the game's materials (URP Lit copies and the GoF2 Shader Graphs).
        static readonly string[] Slots = { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" };

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // A game material -> its copy with the mods' textures (null: none of its textures is replaced).
        static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        static readonly HashSet<Material> made = new HashSet<Material>();
        static int revision = -1, loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { textures.Clear(); names.Clear(); copies.Clear(); made.Clear(); revision = -1; loading = 0; }

        public static int Count => names.Count;
        public static int Loading => loading;

        /// <summary>Every texture of the mods that are on is loaded (or failed).</summary>
        public static bool Ready { get { Preload(); return loading == 0; } }

        /// <summary>Loads the mods' textures again when the mods changed (no-op otherwise).</summary>
        public static void Preload()
        {
            if (revision == ModManager.Revision) return;
            revision = ModManager.Revision;
            textures.Clear();
            names.Clear();
            foreach (var c in copies.Values) if (c != null) UnityEngine.Object.Destroy(c);
            copies.Clear();
            made.Clear();
            var files = new Dictionary<string, (ModInfo mod, string file)>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in ModManager.Active)
                foreach (string f in mod.Source.FilesIn(Folder, Extensions))
                    files[Path.GetFileNameWithoutExtension(f)] = (mod, f);
            int started = revision;
            foreach (var kv in files)
            {
                string name = kv.Key;
                names.Add(name);
                Load(kv.Value.mod, kv.Value.file, name, started);
            }
            if (files.Count > 0) Debug.Log($"Mods: {files.Count} texture replacement(s) from the mods");
        }

        static async void Load(ModInfo mod, string file, string name, int started)
        {
            loading++;
            bool normal = name.IndexOf("_normal", StringComparison.OrdinalIgnoreCase) >= 0;   // the game's normal maps (Android layout)
            bool linear = normal || name.IndexOf("_metallic", StringComparison.OrdinalIgnoreCase) >= 0;
            try
            {
                await ModMaterials.PreloadTexture(mod, file, linear, normal: normal);
                if (started != revision) return;
                var tex = ModMaterials.Texture(mod, file, linear, normal: normal);
                if (tex != null) { tex.name = name; textures[name] = tex; }
                else Debug.LogWarning($"Mods: {mod.Id}: could not load {file}");
            }
            catch (Exception e) { Debug.LogWarning($"Mods: {mod.Id}: {file}: {e.Message}"); }
            finally { if (started == revision) loading--; }
        }

        /// <summary>Puts the mods' textures on an assembled object's renderers (its own material copies). Cheap without any.</summary>
        public static void Apply(GameObject root)
        {
            if (root == null || !Application.isPlaying) return;
            if (revision != ModManager.Revision) Preload();
            if (textures.Count == 0) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var copy = CopyFor(mats[i]);
                    if (copy != null) { mats[i] = copy; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>Remake mods: a material that isn't on an assembled object (the backdrop's planets, suns and rings,
        /// Backdrop.Load): its copy with the mods' textures, or the material itself when none of them is replaced.</summary>
        public static Material Replace(Material m)
        {
            if (m == null || !Application.isPlaying) return m;
            if (revision != ModManager.Revision) Preload();
            if (textures.Count == 0) return m;
            return CopyFor(m) ?? m;
        }

        static Material CopyFor(Material m)
        {
            if (m == null || made.Contains(m)) return null;   // already a copy (Apply again on the same object)
            if (copies.TryGetValue(m, out var copy)) return copy;
            foreach (var slot in Slots)
            {
                if (!m.HasProperty(slot)) continue;
                var t = m.GetTexture(slot);
                if (t == null || !textures.TryGetValue(t.name, out var mod)) continue;
                if (copy == null) { copy = new Material(m) { name = m.name + " (mod)" }; }
                copy.SetTexture(slot, mod);
            }
            if (copy != null) made.Add(copy);
            if (copy != null || loading == 0) copies[m] = copy;   // "none" only once everything has loaded
            return copy;
        }
    }
}
