// EngineMaterialGlow.cs
// Remake mods: the materials a mod ship marks "engine" (ships.json / shipkits.json "materials": the nozzles' own glowing
// surfaces, EVERSPACE 2's nozzle rings and grilles) follow the engines: dark at idle, full with the thrust (EngineDrive: the
// player's throttle, else the ship's measured speed), as EVERSPACE 2's do (the user's shots of a Raptor at rest: no glow;
// they kept 18 % at idle before). On a
// ship a station spawns (StationLevel.SpawnShip sets followEngine) they also follow the engine glow part
// (AssembledObject.playerVariantParts' engine_glow): dark on the turntable and the pads, lit through a hangar flight.
// Materials marked "boost" (boost vents) glow only while boosting, with the boost (EngineDrive above 1). Changes ease over
// a quarter second. The materials are this ship's own copies (renderer.materials), so other ships of the model keep theirs.
// A material's "engineIdle" (0..1) is its share at throttle 0: 1 = steady like the game's own engine glow (lit whenever the
// engines run, dark on a parked ship), as a modder asked; the default 0 follows the throttle.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public class EngineMaterialGlow : MonoBehaviour
    {
        public string[] materials = new string[0];   // material names (contains, case-insensitive)
        public float[] materialIdle = new float[0];       // per 'materials' entry: its share at throttle 0 (engineIdle)
        public string[] boostMaterials = new string[0];   // ... glowing only while boosting
        public bool followEngine;

        const float FadePerSecond = 4f;

        class Glowing
        {
            public Material mat;
            public Color emission;
            public float idle, level = -1f;
        }

        readonly List<Glowing> glowing = new List<Glowing>();
        readonly List<Glowing> boosting = new List<Glowing>();
        GameObject engineGlow;
        Visuals.EngineDrive drive;
        bool ready;

        /// <summary>Adds the component to a ship template for the named materials (none: nothing added); 'idle' gives a
        /// material's share at throttle 0 (CustomShipMaterial.engineIdle; missing = 0).</summary>
        public static void Add(GameObject ship, IEnumerable<string> names, IEnumerable<string> boostNames = null, IDictionary<string, float> idle = null)
        {
            var list = names?.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray() ?? new string[0];
            var blist = boostNames?.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray() ?? new string[0];
            if (ship == null || list.Length + blist.Length == 0) return;
            var c = ship.GetComponent<EngineMaterialGlow>();
            if (c == null) c = ship.AddComponent<EngineMaterialGlow>();
            var idles = new Dictionary<string, float>();
            for (int i = 0; i < c.materials.Length; i++) idles[c.materials[i]] = i < c.materialIdle.Length ? c.materialIdle[i] : 0f;
            if (idle != null) foreach (var kv in idle) if (!string.IsNullOrEmpty(kv.Key)) idles[kv.Key] = Mathf.Clamp01(kv.Value);
            c.materials = c.materials.Concat(list).Distinct().ToArray();
            c.materialIdle = c.materials.Select(n => idles.TryGetValue(n, out float f) ? f : 0f).ToArray();
            c.boostMaterials = c.boostMaterials.Concat(blist).Distinct().ToArray();
        }

        /// <summary>The glow a material shows (0..1 of its emission): dark while 'off', else from its idle share at throttle 0
        /// to full at full throttle ('drive', EngineDrive's 0..2).</summary>
        public static float Target(float drive, float idle, bool off) => off ? 0f : Mathf.Lerp(Mathf.Clamp01(idle), 1f, Mathf.Clamp01(drive));

        void Setup()
        {
            ready = true;
            var asm = GetComponent<Visuals.AssembledObject>();
            if (asm != null && asm.playerVariantParts != null)
            {
                engineGlow = asm.playerVariantParts.FirstOrDefault(p => p != null && p.name.Contains("engine_glow"));
                if (engineGlow == null) engineGlow = asm.playerVariantParts.FirstOrDefault(p => p != null);
            }
            Collect(materials, materialIdle, glowing);
            Collect(boostMaterials, null, boosting);
        }

        void Collect(string[] names, float[] idles, List<Glowing> into)
        {
            var wanted = names.Select(m => m.ToLowerInvariant()).ToArray();
            if (wanted.Length == 0) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (!shared.Any(m => m != null && Match(m.name, wanted) >= 0)) continue;
                var own = r.materials;   // this ship's copies
                for (int i = 0; i < own.Length; i++)
                {
                    int k = own[i] != null && i < shared.Length && shared[i] != null ? Match(shared[i].name, wanted) : -1;
                    if (k < 0 || !own[i].HasProperty("_EmissionColor")) continue;
                    into.Add(new Glowing { mat = own[i], emission = own[i].GetColor("_EmissionColor"), idle = idles != null && k < idles.Length ? idles[k] : 0f });
                }
            }
        }

        /// <summary>The first wanted name the material's name contains, -1 = none.</summary>
        static int Match(string name, string[] wanted)
        {
            string n = name.ToLowerInvariant();
            for (int i = 0; i < wanted.Length; i++) if (n.Contains(wanted[i])) return i;
            return -1;
        }

        void Update()
        {
            if (!ready) { Setup(); drive = new Visuals.EngineDrive(transform); }
            if (glowing.Count == 0 && boosting.Count == 0) return;
            float d = drive.Sample(Time.deltaTime, followEngine ? engineGlow : null);
            bool off = (followEngine && !On) || drive.Off;   // free flight with no thrust: dark like a parked ship
            foreach (var g in glowing) Ease(g, Target(d, g.idle, off));
            foreach (var g in boosting) Ease(g, off ? 0f : Mathf.Clamp01(d - 1f));
        }

        static void Ease(Glowing g, float target)
        {
            if (g.mat == null) return;
            if (g.level < 0f) g.level = target;
            else if (Mathf.Approximately(g.level, target)) return;
            else g.level = Mathf.MoveTowards(g.level, target, Time.unscaledDeltaTime * FadePerSecond);
            g.mat.SetColor("_EmissionColor", g.emission * g.level);
        }

        bool On => engineGlow == null || engineGlow.activeInHierarchy;

        void OnDestroy()
        {
            foreach (var g in glowing) if (g.mat != null) Destroy(g.mat);
            foreach (var g in boosting) if (g.mat != null) Destroy(g.mat);
        }
    }
}
