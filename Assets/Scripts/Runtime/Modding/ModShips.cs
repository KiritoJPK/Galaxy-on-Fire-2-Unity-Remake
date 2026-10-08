// ModShips.cs
// The models of the active mods' ships: each ships.json entry's glTF / GLB file loaded with glTFast (materials through
// ModGltfMaterials) and built into an AssembledObject template (ModShipBuilder), kept under a hidden DontDestroyOnLoad
// holder for the whole run; AssembledObject.LoadPrefab hands the template out for the ship's assembly ("ship_NNN_mod",
// pack "mod"), so every place that shows a ship (hangar, flight, NPCs, the item window, other players) instantiates it
// like a prefab. glTF loading is asynchronous: Preload starts it (the main menu and Bootstrap, again whenever the
// active mods change) and the menu waits for Ready before a game scene loads. A ship whose model isn't there (still
// loading, or broken) shows the Phantom's. Also each ship's shop icon (the entry's "icon" PNG, 180 x 88 like the
// originals; none = the Phantom's).
// The ships load side by side (at most ShipSlots at once: each holds its whole GLB file until glTFast has read it, and the
// GoF3 Ships mod's 79 came to 430 MB): its files are read on worker threads, its textures decoded in the background
// (ModMaterials.PreloadTexture; a GLB's embedded images the same way, ModGltf), its glTF parsed by glTFast's jobs, and the
// main-thread work of all of them shares one time budget per frame (glTFast's TimeBudgetPerFrameDeferAgent), so the game
// keeps running (the main menu's loading screen shows Progress / Current, ModLoading).

using System.Collections.Generic;
using System.Threading.Tasks;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModShips
    {
        public const string Pack = "mod";
        const int FallbackShip = 10;   // the Phantom

        static readonly Dictionary<int, GameObject> templates = new Dictionary<int, GameObject>();
        static readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, string> names = new Dictionary<int, string>();
        static readonly List<GLTFast.GltfImport> imports = new List<GLTFast.GltfImport>();
        static GameObject holder;
        static GLTFast.TimeBudgetPerFrameDeferAgent agent;
        static int loadingRevision = -1, loadedRevision = -1;
        static Task loading;
        static int steps, stepsDone;
        static readonly List<string> current = new List<string>();
        static System.Threading.SemaphoreSlim shipSlots;

        /// <summary>Ships loading at once.</summary>
        static System.Threading.SemaphoreSlim ShipSlots => shipSlots ??= new System.Threading.SemaphoreSlim(Application.isMobilePlatform ? 3 : 8);

        /// <summary>How far the models are (0..1; 1 with nothing to load).</summary>
        public static float Progress => steps == 0 ? 1f : Mathf.Clamp01((float)stepsDone / steps);

        /// <summary>A ship being loaded now (its name; null: none).</summary>
        public static string Current => current.Count > 0 ? current[current.Count - 1] : null;

        /// <summary>The ships to load and those done.</summary>
        public static int Count { get; private set; }
        public static int Done { get; private set; }

        /// <summary>The mods' ship models were (re)built: anything that showed a mod ship before (another player's ship built
        /// when they joined, before the session's mods were on) builds it again (NetPlayer).</summary>
        public static event System.Action ModelsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            templates.Clear(); icons.Clear(); imports.Clear(); names.Clear();
            holder = null; agent = null; loading = null; ModelsChanged = null;
            loadingRevision = loadedRevision = -1;
            steps = stepsDone = 0; current.Clear(); Count = Done = 0;
        }

        /// <summary>The active mods' ship models are built (or there are none).</summary>
        public static bool Ready
        {
            get
            {
                if (loadedRevision == ModManager.Revision) return true;
                if (ModContent.ActiveShips().Count == 0 && ModContent.ModelOverrides().Count == 0 && templates.Count == 0) return true;
                Preload();
                return false;
            }
        }

        /// <summary>Starts building the active mods' ship models when the mods changed since the last time.</summary>
        public static void Preload()
        {
            int rev = ModManager.Revision;
            if (loadingRevision == rev) return;
            loadingRevision = rev;
            loading = LoadAll(rev);
        }

        const int StepsPerShip = 4;   // textures, file, glTF, built

        static async Task LoadAll(int rev)
        {
            Release();
            if (holder == null)
            {
                holder = new GameObject("Mod ship models");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
            }
            if (agent == null)
            {
                // Active (the holder isn't): the agent measures the frame in its Update.
                var go = new GameObject("Mod ship loader") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                agent = go.AddComponent<GLTFast.TimeBudgetPerFrameDeferAgent>();
            }
            agent.SetFrameBudget(0.6f);   // of a frame: the loading screen only animates a bar meanwhile
            var ships = ModContent.ActiveShips();
            ships.AddRange(ModContent.ModelOverrides());   // the originals whose model a mod replaces
            Count = ships.Count;
            Done = 0;
            steps = ships.Count * StepsPerShip;
            stepsDone = 0;
            current.Clear();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var tasks = new List<Task>();
            foreach (var (mod, c) in ships) tasks.Add(LoadShip(rev, mod, c));
            await Task.WhenAll(tasks);
            if (rev != ModManager.Revision) return;   // the mods changed meanwhile: a newer load runs
            loadedRevision = rev;
            if (ships.Count > 0) Debug.Log($"Mods: {ships.Count} ship model(s) loaded in {watch.ElapsedMilliseconds} ms");
            ModelsChanged?.Invoke();
        }

        /// <summary>The PNGs a ship's entry uses, with how they are read (ModMaterials.FromSpec, the throttle glow masks).</summary>
        static IEnumerable<(string path, bool linear, bool readable, bool normal)> TexturesOf(CustomShipData c)
        {
            if (!string.IsNullOrEmpty(c.icon)) yield return (c.icon, false, false, false);
            if (c.materials != null)
                foreach (var m in c.materials)
                {
                    if (m == null) continue;
                    if (!string.IsNullOrEmpty(m.diffuse)) yield return (m.diffuse, false, false, false);
                    if (!string.IsNullOrEmpty(m.normal)) yield return (m.normal, true, false, true);
                    if (!string.IsNullOrEmpty(m.metallicSmoothness)) yield return (m.metallicSmoothness, true, false, false);
                    if (!string.IsNullOrEmpty(m.emission)) yield return (m.emission, false, false, false);
                    if (!string.IsNullOrEmpty(m.detailAlbedo)) yield return (m.detailAlbedo, true, false, false);
                    if (!string.IsNullOrEmpty(m.detailNormal)) yield return (m.detailNormal, true, false, true);
                }
            if (c.throttleGlow != null && !string.IsNullOrEmpty(c.throttleGlow.mask)) yield return (c.throttleGlow.mask, false, true, false);
            if (c.extraGlows != null)
                foreach (var g in c.extraGlows) if (g != null && !string.IsNullOrEmpty(g.mask)) yield return (g.mask, false, true, false);
        }

        /// <summary>One ship: its textures and model file at once, then glTFast, then the template.</summary>
        static async Task LoadShip(int rev, ModInfo mod, CustomShipData c)
        {
            var slots = ShipSlots;
            await slots.WaitAsync();
            string label = c.name;
            current.Add(label);
            int counted = 0;
            void Step() { counted++; stepsDone++; }
            try
            {
                if (rev != ModManager.Revision) return;
                var textures = new List<Task>();
                foreach (var (path, linear, readable, normal) in TexturesOf(c)) textures.Add(ModMaterials.PreloadTexture(mod, path, linear, readable, normal));
                var file = string.IsNullOrEmpty(c.model) ? Task.FromResult<byte[]>(null) : Task.Run(() => mod.Source.ReadBytes(c.model));
                await Task.WhenAll(textures);
                Step();
                var bytes = await file;
                Step();
                if (rev != ModManager.Revision) return;
                var icon = ModMaterials.Texture(mod, c.icon, false);
                if (icon != null) icons[c.index] = icon;
                if (bytes == null) { Warn(mod, $"ships.json: \"{c.name}\": model {c.model} not found"); return; }
                var import = await ModGltf.Load(mod, bytes, agent);
                bytes = null;
                if (import == null) { Warn(mod, $"{c.model}: not a glTF model glTFast can read"); return; }
                imports.Add(import);
                Step();
                if (rev != ModManager.Revision || holder == null) return;
                var scene = new GameObject("scene");
                scene.transform.SetParent(holder.transform, false);
                if (!await import.InstantiateMainSceneAsync(scene.transform)) { Object.Destroy(scene); Warn(mod, $"{c.model}: no scene to show"); return; }
                if (rev != ModManager.Revision || holder == null) return;
                var template = ModShipBuilder.Build(c, mod, scene);
                template.transform.SetParent(holder.transform, false);
                templates[c.index] = template;
                names[c.index] = c.name;
                Debug.Log($"Mods: {mod.Id}: ship {c.index} \"{c.name}\" built from {c.model}");
            }
            catch (System.Exception e) { Warn(mod, $"{c.model}: {e.Message}"); Debug.LogException(e); }
            finally
            {
                if (rev == ModManager.Revision)
                {
                    stepsDone += StepsPerShip - counted;   // the steps a failed ship skipped
                    Done++;
                }
                current.Remove(label);
                slots.Release();
            }
        }

        static void Warn(ModInfo mod, string message)
        {
            if (!mod.Warnings.Contains(message)) mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }

        /// <summary>The old templates and their resources go (the mods changed).</summary>
        static void Release()
        {
            foreach (var t in templates.Values) if (t != null) Object.Destroy(t);
            templates.Clear();
            icons.Clear();
            names.Clear();
            foreach (var i in imports) i.Dispose();
            imports.Clear();
            if (holder != null) foreach (Transform child in holder.transform) Object.Destroy(child.gameObject);
            loadedRevision = -1;
        }

        /// <summary>The ship number in a "ship_NNN_mod" assembly name, -1 = not one.</summary>
        static int ShipOf(string assembly) =>
            assembly != null && assembly.StartsWith("ship_") && assembly.Length > 8 && int.TryParse(assembly.Substring(5, 3), out int n) ? n : -1;

        /// <summary>The template for a mod ship's assembly; the Phantom's prefab while the model isn't built.</summary>
        public static GameObject Template(string assembly)
        {
            int ship = ShipOf(assembly);
            if (ship >= 0 && templates.TryGetValue(ship, out var t) && t != null) return t;
            if (!Ready) Debug.LogWarning($"Mods: ship {ship}'s model is still loading: the Phantom stands in");
            var db = Database.Load();
            return Visuals.AssembledObject.LoadPrefab(db.ShipAssembly(FallbackShip));
        }

        /// <summary>Runs the action once the models are built (at most 'timeoutSeconds' later).</summary>
        public static void WhenReady(System.Action action, float timeoutSeconds = 30f) => ModShipsWaiter.Run(action, timeoutSeconds);

        /// <summary>The built templates (ship number, name, template), for the loading screen's hangar shadows
        /// (World.ShipShadowBaker); only complete once Ready.</summary>
        public static IEnumerable<(int ship, string name, GameObject template)> Built
        {
            get
            {
                foreach (var kv in templates)
                    if (kv.Value != null) yield return (kv.Key, names.TryGetValue(kv.Key, out var n) ? n : kv.Value.name, kv.Value);
            }
        }

        /// <summary>A mod ship's shop icon, null = none.</summary>
        public static Texture2D Icon(int ship) => icons.TryGetValue(ship, out var t) ? t : null;
    }
}
