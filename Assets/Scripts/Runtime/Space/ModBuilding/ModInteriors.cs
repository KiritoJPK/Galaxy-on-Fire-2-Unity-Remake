// ModInteriors.cs
// Remake mods: custom hangars and Space Lounge bars (interiors.json), each a glTF / GLB room with named marker nodes (empty
// nodes, matched by name without case; numbered ones by their prefix) that say where things are, so a modder places them in
// Blender instead of typing coordinates:
//   hangar: "pad" (the player's ship stands on it, required), "camera" (required) + "camera_target" (where it looks; default
//           the pad), "parked_1", "parked_2"... (parked ships' pads), "gate" + "gate_out" (the forcefield's centre and a point
//           outside it: the hangar flights fly through there; none = no flights), "light" (the key light shines from there
//           toward the pad)
//   bar:    "camera" + "camera_target" (the resting view, required), "camera_start" + "camera_start_target" (the first visit's
//           glide from there; default the resting view), "visitor_1", "visitor_2"... (where visitors stand, at least one),
//           "light" (default: the system's sun, like the originals)
// interiors.json: [ { "id", "type": "hangar" | "bar", "model", "scale" (default 1: the glTF's metres), "materials" (like
// ships.json's), "fov" (vertical degrees; default the originals': hangar 46, bar 69), "near" / "far" (metres), hangar:
// "startYaw" (degrees the player's ship is turned to), "parkedMax" (default the parked pads' count), "flights" (default true),
// "cruise" (metres above the floor the flights cross at), "gateSpan" [low, high] (heights the forcefield can be passed at),
// both: "ambient" [r, g, b], "lightIntensity", "fog" [r, g, b, end metres] } ]
// A station picks them (stations.json "hangar" / "bar": "mod_id:interior_id", or a race name like "interior"). Built by
// ModStations with the station models (all in the background at the main menu's start); the room's own glTF cameras and
// lights are removed (the station camera and key light do that work). While a room isn't built, or when it failed, the
// station falls back to its race's room.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ModInteriors
    {
        public const string File = "interiors.json";

        static readonly HashSet<string> Fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "type", "model", "scale", "materials", "fov", "near", "far", "startYaw", "parkedMax", "flights", "cruise", "gateSpan",
            "ambient", "lightIntensity", "fog",
        };

        /// <summary>One interiors.json entry.</summary>
        public class Def
        {
            public ModInfo mod;
            public string key, id, model, label;
            public bool hangar;
            public float scale = 1f, fov = -1f, near = -1f, far = -1f, startYaw = float.NaN, lightIntensity = -1f, cruise = float.NaN;
            public int parkedMax = -1;
            public bool flights = true;
            public Vector2? gateSpan;
            public Color? ambient, fogColor;
            public float fogEnd;
            public List<CustomShipMaterial> materials = new List<CustomShipMaterial>();
        }

        /// <summary>A built room: its template and its markers (room-root space, Unity metres).</summary>
        public class Room
        {
            public Def def;
            public GameObject template;
            public Vector3 pad, camera, cameraTarget, cameraStart, cameraStartTarget, light, gate, gateOut;
            public bool hasLight, hasGate, hasStart;
            public readonly List<Vector3> parked = new List<Vector3>(), visitors = new List<Vector3>();
        }

        static readonly Dictionary<string, Def> defs = new Dictionary<string, Def>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>(StringComparer.OrdinalIgnoreCase);
        static int parsedRevision = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { defs.Clear(); rooms.Clear(); parsedRevision = -1; }

        /// <summary>Every interior of the mods that are on (read again when the mods change).</summary>
        public static IEnumerable<Def> All()
        {
            if (parsedRevision != ModManager.Revision)
            {
                parsedRevision = ModManager.Revision;
                defs.Clear();
                foreach (var mod in ModManager.Active)
                {
                    try { Read(mod); }
                    catch (ModJsonException e) { Warn(mod, e.Message); }
                }
            }
            return defs.Values.ToList();
        }

        static void Read(ModInfo mod)
        {
            var token = ModJson.Read(mod.Source, File);
            if (token == null) return;
            if (!(token is JArray list)) throw new ModJsonException($"{File}: must be a list [ {{ ... }}, ... ]");
            foreach (var t in list)
            {
                string where = ModJson.Where(t, File);
                if (!(t is JObject o)) throw new ModJsonException($"{where}: each entry must be an object {{ ... }}");
                foreach (var prop in o.Properties())
                    if (!Fields.Contains(prop.Name)) Warn(mod, $"{ModJson.Where(prop, File)}: unknown field \"{prop.Name}\" (ignored)");
                string id = ModJson.Str(o, "id"), type = ModJson.Str(o, "type", "").ToLowerInvariant(), model = ModJson.Str(o, "model");
                if (!ModManifest.ValidId(id ?? "")) throw new ModJsonException($"{where}: \"id\" is missing or uses more than a-z, 0-9, _ and -");
                if (type != "hangar" && type != "bar") throw new ModJsonException($"{where}: \"type\" must be hangar or bar");
                if (string.IsNullOrEmpty(model)) throw new ModJsonException($"{where}: \"model\" (a glTF / GLB file) is missing");
                var d = new Def
                {
                    mod = mod, id = id, key = mod.Id + ":" + id, model = model, label = id, hangar = type == "hangar",
                    scale = Mathf.Max(0.001f, ModJson.Float(o, "scale", 1f, File)),
                    fov = ModJson.Float(o, "fov", -1f, File), near = ModJson.Float(o, "near", -1f, File), far = ModJson.Float(o, "far", -1f, File),
                    startYaw = ModJson.Has(o, "startYaw") ? ModJson.Float(o, "startYaw", 0f, File) : float.NaN,
                    parkedMax = ModJson.Int(o, "parkedMax", -1, File),
                    flights = ModJson.Bool(o, "flights", true, File),
                    cruise = ModJson.Has(o, "cruise") ? ModJson.Float(o, "cruise", 0f, File) : float.NaN,
                    lightIntensity = ModJson.Float(o, "lightIntensity", -1f, File),
                };
                if (ModJson.Get(o, "gateSpan") is JArray gs && gs.Count == 2) d.gateSpan = new Vector2((float)gs[0], (float)gs[1]);
                if (ModJson.Get(o, "ambient") is JArray am && am.Count == 3) d.ambient = new Color((float)am[0], (float)am[1], (float)am[2]);
                if (ModJson.Get(o, "fog") is JArray fg && fg.Count == 4) { d.fogColor = new Color((float)fg[0], (float)fg[1], (float)fg[2]); d.fogEnd = (float)fg[3]; }
                if (ModJson.Get(o, "materials") is JArray mats)
                    foreach (var m in mats) d.materials.Add(JsonUtility.FromJson<CustomShipMaterial>(m.ToString()));
                if (defs.ContainsKey(d.key)) throw new ModJsonException($"{where}: the id \"{id}\" is used twice");
                defs[d.key] = d;
            }
        }

        /// <summary>An interior key ("mod_id:interior_id") of the given type, or null.</summary>
        public static Def Find(string key, bool hangar)
        {
            if (string.IsNullOrEmpty(key)) return null;
            All();
            return defs.TryGetValue(key, out var d) && d.hangar == hangar ? d : null;
        }

        /// <summary>The built room of a station's custom hangar / bar (null: none, or not built: the race's room).</summary>
        public static Room HangarOf(int station) => RoomFor(ModWorld.HangarKey(station), true);
        public static Room BarOf(int station) => RoomFor(ModWorld.BarKey(station), false);

        static Room RoomFor(string key, bool hangar)
        {
            var d = Find(key, hangar);
            return d != null && rooms.TryGetValue(d.key, out var r) && r.template != null ? r : null;
        }

        internal static void Clear()
        {
            foreach (var r in rooms.Values) if (r.template != null) UnityEngine.Object.Destroy(r.template);
            rooms.Clear();
        }

        /// <summary>ModStations: the template around the instantiated glTF scene, its markers read, its cameras / lights gone.
        /// Null (warned) when a required marker is missing.</summary>
        internal static GameObject Build(Def d, GameObject model)
        {
            var root = new GameObject("interior " + d.key);
            model.name = "room";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * d.scale;
            foreach (var c in model.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var l in model.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (d.materials.Count > 0)
            {
                var mats = new Material[d.materials.Count];
                for (int i = 0; i < mats.Length; i++) mats[i] = ModMaterials.FromSpec(d.mod, d.materials[i], $"{d.key} {d.materials[i].mesh}{d.materials[i].submesh}");
                foreach (var r in renderers)
                {
                    var mesh = ModShipBuilder.MeshOf(r);
                    var assigned = r.sharedMaterials;
                    int count = Mathf.Max(1, mesh != null ? mesh.subMeshCount : assigned.Length);
                    if (assigned.Length != count) Array.Resize(ref assigned, count);
                    for (int s = 0; s < count; s++) { var m = ModShipBuilder.MaterialFor(d.materials, mats, r.name, s); if (m != null) assigned[s] = m; }
                    r.sharedMaterials = assigned;
                }
            }
            foreach (var r in renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }

            // The markers, by name (room-root space).
            var marks = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var numbered = new List<(string name, int n, Vector3 pos)>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.Trim();
                var p = root.transform.InverseTransformPoint(t.position);
                if (!marks.ContainsKey(n)) marks[n] = p;
                foreach (string prefix in new[] { "parked", "visitor" })
                    if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string rest = n.Substring(prefix.Length).TrimStart('_', ' ', '.', '-');
                        int.TryParse(new string(rest.TakeWhile(char.IsDigit).ToArray()), out int k);
                        numbered.Add((prefix, k, p));
                    }
            }
            bool Has(string name, out Vector3 v) => marks.TryGetValue(name, out v);
            var room = new Room { def = d, template = root };
            if (d.hangar)
            {
                if (!Has("pad", out room.pad)) return Fail(d, root, "a hangar needs a \"pad\" marker (where the player's ship stands)");
                if (!Has("camera", out room.camera)) return Fail(d, root, "a hangar needs a \"camera\" marker");
                room.cameraTarget = Has("camera_target", out var ct) ? ct : room.pad;
                room.hasGate = Has("gate", out room.gate) && Has("gate_out", out room.gateOut);
                foreach (var p in numbered.Where(x => x.name == "parked").OrderBy(x => x.n)) room.parked.Add(p.pos);
            }
            else
            {
                if (!Has("camera", out room.camera)) return Fail(d, root, "a bar needs a \"camera\" marker");
                if (!Has("camera_target", out room.cameraTarget)) return Fail(d, root, "a bar needs a \"camera_target\" marker (where the camera looks)");
                room.hasStart = Has("camera_start", out room.cameraStart);
                room.cameraStartTarget = Has("camera_start_target", out var cst) ? cst : room.cameraTarget;
                foreach (var p in numbered.Where(x => x.name == "visitor").OrderBy(x => x.n)) room.visitors.Add(p.pos);
                if (room.visitors.Count == 0) return Fail(d, root, "a bar needs \"visitor_1\", \"visitor_2\"... markers (where visitors stand)");
            }
            room.hasLight = Has("light", out room.light);
            rooms[d.key] = room;
            return root;
        }

        static GameObject Fail(Def d, GameObject root, string why)
        {
            Warn(d.mod, $"{File}: \"{d.id}\": {why}");
            UnityEngine.Object.Destroy(root);
            return null;
        }

        static void Warn(ModInfo mod, string message)
        {
            if (!mod.Warnings.Contains(message)) mod.Warnings.Add(message);
            Debug.LogWarning($"Mods: {mod.Id}: {message}");
        }
    }
}
