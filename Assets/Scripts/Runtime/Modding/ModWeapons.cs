// ModWeapons.cs
// Remake mods: a weapon's own look and sound (items.json "fx" on a new item or an override). The projectile, the muzzle
// flash and the impact are each the base item's (the item's Look), a mod sprite (a PNG on a camera-facing quad, or
// stretched along the flight), a mod model (glTF / GLB, with the fx shader when it names a texture, else its own
// materials), or false (none). "shot" / "explosionSound" are the mod's audio files (one at random per shot), "shotLoops"
// whether the shot sound loops while the trigger is held. What the weapon does stays the base item's (a beam stays a
// beam, a mine a mine): only what it looks and sounds like changes.
// Each item gets a WeaponFx of its own (a copy of the base item's, the parts and clips replaced), which WeaponFx.Load hands
// to every gun (the player's, NPCs', turrets, sentry guns, other players' mirrored shots). The parts are built under a
// hidden DontDestroyOnLoad holder (GunRig instantiates them); ModFxPart gives the sprites and models their lifetime,
// growth, fade and camera facing. Loaded in the background with the ships when the mods change (Preload; ModLoading).

using System.Collections.Generic;
using System.Threading.Tasks;
using GoF2Remake.Flight;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModWeapons
    {
        const float M = 0.05f;
        const string File = ModContent.ItemsFile;

        /// <summary>One part of an "fx": a sprite or a model (or removed).</summary>
        public class Part
        {
            public bool remove;
            public string sprite, model, texture;
            public float size = 200f, glow = -1f, lifetimeMs, grow = 1f, spin;
            public Color color = Color.white;
            public bool additive = true, fade = true, alongFlight;
            public float stretch = 1f;
            public Vector3 rotate;
        }

        public class Def
        {
            public ModInfo mod;
            public int item;
            public string label, where;
            public Part projectile, muzzle, impact;
            public List<string> shots, explosionSounds;
            public bool? shotLoops;
        }

        static readonly Dictionary<int, WeaponFx> made = new Dictionary<int, WeaponFx>();
        static readonly List<Object> owned = new List<Object>();
        static readonly List<GLTFast.GltfImport> imports = new List<GLTFast.GltfImport>();
        static GameObject holder;
        static GLTFast.TimeBudgetPerFrameDeferAgent agent;
        static Mesh quad;
        static int loadingRevision = -1, loadedRevision = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            made.Clear(); owned.Clear(); imports.Clear();
            holder = null; agent = null; quad = null; loadingRevision = loadedRevision = -1; Count = Done = 0;
        }

        public static int Count { get; private set; }
        public static int Done { get; private set; }

        /// <summary>Every mod weapon's fx is built (or there are none).</summary>
        public static bool Ready
        {
            get
            {
                if (loadedRevision == ModManager.Revision) return true;
                Preload();
                return false;
            }
        }

        /// <summary>A mod item's own fx (null: none, or not built yet: the base item's is used meanwhile).</summary>
        public static WeaponFx Fx(int item)
        {
            if (loadedRevision != ModManager.Revision) { Preload(); return null; }
            return made.TryGetValue(item, out var fx) ? fx : null;
        }

        public static void Preload()
        {
            int rev = ModManager.Revision;
            if (loadingRevision == rev) return;
            loadingRevision = rev;
            _ = LoadAll(rev);
        }

        // ---- the "fx" entries ---------------------------------------------------------------------------------------

        /// <summary>The "fx" of every item of the mods that are on (a later mod's fx for the same item wins).</summary>
        public static List<Def> Defs()
        {
            var byItem = new Dictionary<int, Def>();
            foreach (var mod in ModManager.Active)
                foreach (var d in ModContent.Parse(mod).items)
                {
                    if (!(ModJson.Get(d.json, "fx") is JToken fx)) continue;
                    int index = d.key != null ? ModContent.ItemIndexOf(d.key) : ModContent.TryResolveItem(d.overrideRef, out int t) ? t : -1;
                    if (index < 0) continue;
                    try { byItem[index] = Parse(mod, fx, index, d.localId ?? d.overrideRef); }
                    catch (ModJsonException e) { Warn(mod, e.Message); }
                }
            return new List<Def>(byItem.Values);
        }

        static readonly HashSet<string> FxFields = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "projectile", "muzzle", "impact", "shot", "shotLoops", "explosionSound" };
        static readonly HashSet<string> PartFields = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "sprite", "model", "texture", "size", "color", "glow", "additive", "lifetime", "grow", "fade", "spin", "alongFlight", "stretch", "rotate" };

        static Def Parse(ModInfo mod, JToken token, int item, string label)
        {
            string where = ModJson.Where(token, File);
            if (!(token is JObject o)) throw new ModJsonException($"{where}: \"fx\" must be an object {{ ... }}");
            foreach (var p in o.Properties())
                if (!FxFields.Contains(p.Name)) mod.Warnings.Add($"{ModJson.Where(p, File)}: unknown fx field \"{p.Name}\" (ignored)");
            var d = new Def
            {
                mod = mod, item = item, label = label, where = where,
                projectile = ParsePart(mod, ModJson.Get(o, "projectile"), "projectile"),
                muzzle = ParsePart(mod, ModJson.Get(o, "muzzle"), "muzzle"),
                impact = ParsePart(mod, ModJson.Get(o, "impact"), "impact"),
                shots = Files(o, "shot"),
                explosionSounds = Files(o, "explosionSound"),
            };
            if (ModJson.Has(o, "shotLoops")) d.shotLoops = ModJson.Bool(o, "shotLoops", false, File);
            foreach (var f in Concat(d.shots, d.explosionSounds))
                if (!mod.Source.Exists(f)) throw new ModJsonException($"{where}: the sound {f} isn't in the mod");
            return d;
        }

        static IEnumerable<string> Concat(List<string> a, List<string> b)
        {
            if (a != null) foreach (var s in a) yield return s;
            if (b != null) foreach (var s in b) yield return s;
        }

        static List<string> Files(JObject o, string key)
        {
            var t = ModJson.Get(o, key);
            if (t == null) return null;
            if (t.Type == JTokenType.String) return new List<string> { (string)t };
            var list = ModJson.Strings(o, key);
            if (list == null || list.Count == 0) throw new ModJsonException($"{ModJson.Where(t, File)}: \"{key}\" must be a file or a list of files");
            return list;
        }

        static Part ParsePart(ModInfo mod, JToken t, string name)
        {
            if (t == null) return null;
            string where = ModJson.Where(t, File);
            if (t.Type == JTokenType.Boolean)
            {
                if ((bool)t) throw new ModJsonException($"{where}: \"{name}\": true means nothing; give a sprite / model, or false for none");
                return new Part { remove = true };
            }
            if (!(t is JObject o)) throw new ModJsonException($"{where}: \"{name}\" must be an object {{ \"sprite\": ... }} or false");
            foreach (var p in o.Properties())
                if (!PartFields.Contains(p.Name)) mod.Warnings.Add($"{ModJson.Where(p, File)}: unknown field \"{p.Name}\" in \"{name}\" (ignored)");
            var part = new Part
            {
                sprite = ModJson.Str(o, "sprite"), model = ModJson.Str(o, "model"), texture = ModJson.Str(o, "texture"),
                size = ModJson.Float(o, "size", 200f, File),
                glow = ModJson.Float(o, "glow", -1f, File),
                lifetimeMs = ModJson.Float(o, "lifetime", name == "projectile" ? 0f : name == "muzzle" ? 100f : 300f, File),
                grow = ModJson.Float(o, "grow", 1f, File),
                spin = ModJson.Float(o, "spin", 0f, File),
                additive = ModJson.Bool(o, "additive", true, File),
                fade = ModJson.Bool(o, "fade", name != "projectile", File),
                alongFlight = ModJson.Bool(o, "alongFlight", false, File),
                stretch = ModJson.Float(o, "stretch", 1f, File),
            };
            if ((part.sprite == null) == (part.model == null))
                throw new ModJsonException($"{where}: \"{name}\" needs either \"sprite\" (a PNG) or \"model\" (a glTF / GLB)");
            if (part.size <= 0f) throw new ModJsonException($"{where}: \"{name}\": \"size\" must be above 0");
            if (ModJson.Get(o, "color") is JToken c) part.color = ParseColor(c);
            if (ModJson.Get(o, "rotate") is JArray r && r.Count == 3) part.rotate = new Vector3((float)r[0], (float)r[1], (float)r[2]);
            foreach (var f in new[] { part.sprite, part.model, part.texture })
                if (f != null && !mod.Source.Exists(f)) throw new ModJsonException($"{where}: \"{name}\": {f} isn't in the mod");
            return part;
        }

        /// <summary>[r, g, b] / [r, g, b, a] from 0 to 1, or "#rrggbb" / "#rrggbbaa".</summary>
        static Color ParseColor(JToken t)
        {
            if (t.Type == JTokenType.String && ColorUtility.TryParseHtmlString((string)t, out var hc)) return hc;
            if (t is JArray a && (a.Count == 3 || a.Count == 4))
                return new Color((float)a[0], (float)a[1], (float)a[2], a.Count == 4 ? (float)a[3] : 1f);
            throw new ModJsonException($"{ModJson.Where(t, File)}: a color is [r, g, b] (0 to 1) or \"#rrggbb\"");
        }

        // ---- loading -------------------------------------------------------------------------------------------------

        static async Task LoadAll(int rev)
        {
            foreach (var o in owned) if (o != null) Object.Destroy(o);
            owned.Clear();
            made.Clear();
            foreach (var i in imports) i.Dispose();
            imports.Clear();
            List<Def> defs;
            try { defs = Defs(); }
            catch (System.Exception e) { Debug.LogException(e); defs = new List<Def>(); }
            Count = defs.Count;
            Done = 0;
            if (defs.Count > 0)
            {
                if (holder == null)
                {
                    holder = new GameObject("Mod weapon fx");
                    holder.SetActive(false);
                    Object.DontDestroyOnLoad(holder);
                }
                if (agent == null)
                {
                    var go = new GameObject("Mod weapon loader") { hideFlags = HideFlags.HideInHierarchy };
                    Object.DontDestroyOnLoad(go);
                    agent = go.AddComponent<GLTFast.TimeBudgetPerFrameDeferAgent>();
                    agent.SetFrameBudget(0.3f);
                }
                var tasks = new List<Task>();
                foreach (var d in defs) tasks.Add(LoadOne(rev, d));
                await Task.WhenAll(tasks);
            }
            if (rev != ModManager.Revision) return;
            loadedRevision = rev;
        }

        static async Task LoadOne(int rev, Def d)
        {
            try
            {
                // Everything of the item at once: textures, models and sounds.
                var parts = new[] { d.projectile, d.muzzle, d.impact };
                var built = new Task<GameObject>[3];
                for (int i = 0; i < 3; i++) built[i] = parts[i] == null || parts[i].remove ? Task.FromResult<GameObject>(null) : BuildPart(rev, d, parts[i], i);
                var shots = LoadClips(d.mod, d.shots);
                var booms = LoadClips(d.mod, d.explosionSounds);
                await Task.WhenAll(built[0], built[1], built[2], shots, booms);
                if (rev != ModManager.Revision) return;
                int look = ModContent.ItemLook(d.item);
                var baseFx = Resources.Load<WeaponFx>($"{WeaponFx.ResourcesFolder}/item_{look:000}");
                var fx = baseFx != null ? Object.Instantiate(baseFx) : ScriptableObject.CreateInstance<WeaponFx>();
                fx.name = $"mod fx {d.mod.Id}:{d.label}";
                fx.item = d.item;
                if (d.projectile != null) fx.projectile = built[0].Result;
                if (d.muzzle != null) fx.muzzleFlash = built[1].Result;
                if (d.impact != null) fx.impact = built[2].Result;
                if (shots.Result != null && shots.Result.Length > 0) { fx.shots = shots.Result; fx.shot = shots.Result[0]; }
                if (booms.Result != null && booms.Result.Length > 0) { fx.explosionSounds = booms.Result; fx.explosionSound = booms.Result[0]; }
                if (d.shotLoops.HasValue) fx.shotLoops = d.shotLoops.Value;
                owned.Add(fx);
                made[d.item] = fx;
                Debug.Log($"Mods: {d.mod.Id}: weapon fx for \"{d.label}\" (item {d.item})");
            }
            catch (System.Exception e) { Warn(d.mod, $"{d.where}: {e.Message}"); Debug.LogException(e); }
            finally { if (rev == ModManager.Revision) Done++; }
        }

        static async Task<AudioClip[]> LoadClips(ModInfo mod, List<string> files)
        {
            if (files == null || files.Count == 0) return null;
            var tasks = new List<Task<AudioClip>>();
            foreach (var f in files)
            {
                var tcs = new TaskCompletionSource<AudioClip>();
                ModAudio.Load(mod, ModAudio.Find(mod.Source, f) ?? f, c => tcs.TrySetResult(c), false);
                tasks.Add(tcs.Task);
            }
            var clips = await Task.WhenAll(tasks);
            var list = new List<AudioClip>();
            foreach (var c in clips) if (c != null) { list.Add(c); owned.Add(c); }
            return list.ToArray();
        }

        static readonly string[] PartNames = { "projectile", "muzzle", "impact" };

        /// <summary>A part's template: a root (GunRig scales and turns it) holding the sprite / model with its ModFxPart.</summary>
        static async Task<GameObject> BuildPart(int rev, Def d, Part p, int kind)
        {
            string tex = p.sprite ?? p.texture;
            if (tex != null) await ModMaterials.PreloadTexture(d.mod, tex, false);
            GameObject visual = null;
            if (p.model != null)
            {
                var bytes = await Task.Run(() => d.mod.Source.ReadBytes(p.model));
                if (rev != ModManager.Revision || holder == null) return null;
                if (bytes == null) { Warn(d.mod, $"{d.where}: {p.model} not found"); return null; }
                var import = await ModGltf.Load(d.mod, bytes, agent);
                bytes = null;
                if (import == null) { Warn(d.mod, $"{p.model}: not a glTF model glTFast can read"); return null; }
                imports.Add(import);
                if (rev != ModManager.Revision || holder == null) return null;
                visual = new GameObject("model");
                visual.transform.SetParent(holder.transform, false);
                if (!await import.InstantiateMainSceneAsync(visual.transform)) { Object.Destroy(visual); Warn(d.mod, $"{p.model}: no scene to show"); return null; }
                if (rev != ModManager.Revision || holder == null) return null;
            }
            var root = new GameObject($"{d.mod.Id}:{d.label} {PartNames[kind]}");
            root.transform.SetParent(holder.transform, false);
            owned.Add(root);
            Material mat = null;
            if (tex != null)
            {
                var texture = ModMaterials.Texture(d.mod, tex, false);
                var assets = ModAssets.Get();
                var template = assets != null ? (p.additive ? assets.fxAdditive : assets.fxAlpha) : null;
                if (texture != null && template != null)
                {
                    if (p.sprite != null) texture.wrapMode = TextureWrapMode.Clamp;
                    mat = new Material(template) { name = root.name };
                    mat.SetTexture("_MainTex", texture);
                    mat.SetTextureScale("_MainTex", Vector2.one);
                    mat.SetTextureOffset("_MainTex", Vector2.zero);
                    mat.SetColor("_Color", p.color);
                    if (p.glow >= 0f && mat.HasProperty("_Glow")) mat.SetFloat("_Glow", p.glow);
                    owned.Add(mat);
                }
                else if (texture == null) Warn(d.mod, $"{d.where}: {tex} isn't a PNG / JPG the game can read");
            }
            if (p.sprite != null)
            {
                if (mat == null) { Object.Destroy(root); return null; }
                visual = new GameObject("sprite");
                visual.AddComponent<MeshFilter>().sharedMesh = Quad();
                visual.AddComponent<MeshRenderer>().sharedMaterial = mat;
                visual.transform.localScale = new Vector3(p.size, p.size * Mathf.Max(0.01f, p.stretch), p.size) * M;
                visual.transform.SetParent(root.transform, false);
            }
            else
            {
                visual.name = "model";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = Quaternion.Euler(p.rotate);
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (mat != null)
                    foreach (var r in renderers)
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                        r.sharedMaterials = mats;
                        // The GoF2 Shader Graphs multiply by the vertex colour (every game material has it on): white
                        // where the model has none.
                        var mesh = ModShipBuilder.MeshOf(r);
                        if (mesh != null && mesh.colors32.Length == 0 && mesh.isReadable) mesh.colors32 = White(mesh.vertexCount);
                    }
                // The largest extent to 'size' game units, centred on the part's origin.
                var b = ModShipBuilder.BoundsIn(root.transform, renderers);
                float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (largest > 0f) visual.transform.localScale *= p.size * M / largest;
                b = ModShipBuilder.BoundsIn(root.transform, renderers);
                visual.transform.localPosition -= b.center;
            }
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            var fxPart = visual.AddComponent<ModFxPart>();
            fxPart.lifetimeMs = p.lifetimeMs;
            fxPart.grow = p.grow;
            fxPart.fade = p.fade && mat != null;
            fxPart.additive = p.additive;
            fxPart.spin = p.spin;
            fxPart.faceCamera = p.sprite != null && !p.alongFlight;
            fxPart.alongFlight = p.sprite != null && p.alongFlight;
            return root;
        }

        /// <summary>A 1 x 1 quad facing -Z, both sides drawn (the GoF2 Shader Graphs cull back faces).</summary>
        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "mod fx quad" };
            var v = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            var uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.vertices = new[] { v[0], v[1], v[2], v[3], v[0], v[1], v[2], v[3] };
            quad.uv = new[] { uv[0], uv[1], uv[2], uv[3], uv[0], uv[1], uv[2], uv[3] };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 };
            quad.colors32 = White(8);   // the GoF2 Shader Graphs multiply by it
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            Object.DontDestroyOnLoad(quad);
            return quad;
        }

        static Color32[] White(int n)
        {
            var c = new Color32[n];
            for (int i = 0; i < n; i++) c[i] = new Color32(255, 255, 255, 255);
            return c;
        }

        static void Warn(ModInfo mod, string message)
        {
            if (!mod.Warnings.Contains(message)) mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }
    }
}
