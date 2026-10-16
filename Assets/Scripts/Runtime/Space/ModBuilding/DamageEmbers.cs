// DamageEmbers.cs
// Remake mods: battle damage embers on a mod ship (ships.json / shipkits.json "materials" with "damageEmission": a mask set
// as the material's emission). EVERSPACE 2's hull materials glow in burn patches as the ship is damaged (their DamageAmount,
// EmberIntensity 50, EmberSpeed 0.1; the graph isn't in the export): here the masked materials glow ember-orange from a third
// of the hull lost to full at 90 %, flickering slowly. The damage is the ship's Target (the player's, an NPC's, another
// player's copy: whatever its HullFraction says); a ship without one (the hangar's parked ships) stays dark. Materials with a
// charring detail layer ("damageDetail": EVERSPACE 2's damage colour and normals) blend it in with the same damage, a little
// ahead of the embers and without the flicker: the burn patches darken and pit, then glow. The materials
// are this ship's own copies (renderer.materials), so other ships of the model keep theirs.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public class DamageEmbers : MonoBehaviour
    {
        public string[] materials = new string[0];   // material names (contains, case-insensitive)

        static readonly Color Ember = new Color(1f, 0.32f, 0.06f);
        const float Brightness = 3f;
        const float From = 0.33f, Full = 0.9f;   // hull lost
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly List<Material> glowing = new List<Material>();
        readonly List<Material> charring = new List<Material>();
        float charShown = -1f;
        static readonly int DetailAlbedoScale = Shader.PropertyToID("_DetailAlbedoMapScale");
        static readonly int DetailNormalScale = Shader.PropertyToID("_DetailNormalMapScale");
        const float CharFrom = 0.2f, CharFull = 0.75f;   // hull lost
        Flight.Target target;
        bool ready;
        float shown = -1f, seed;

        /// <summary>Adds the component to a ship template for the named materials (none: nothing added).</summary>
        public static void Add(GameObject ship, IEnumerable<string> names)
        {
            var list = names?.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            if (ship == null || list == null || list.Length == 0) return;
            var c = ship.GetComponent<DamageEmbers>();
            if (c == null) c = ship.AddComponent<DamageEmbers>();
            c.materials = c.materials.Concat(list).Distinct().ToArray();
        }

        void Setup()
        {
            ready = true;
            seed = Random.value * 100f;
            var wanted = materials.Select(m => m.ToLowerInvariant()).ToArray();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (!shared.Any(m => m != null && Matches(m.name, wanted))) continue;
                var own = r.materials;   // this ship's copies
                for (int i = 0; i < own.Length; i++)
                {
                    if (own[i] == null || i >= shared.Length || shared[i] == null || !Matches(shared[i].name, wanted)) continue;
                    if (own[i].GetTexture("_EmissionMap") != null && own[i].HasProperty(EmissionId)) glowing.Add(own[i]);
                    if (own[i].HasProperty("_DetailMask") && own[i].GetTexture("_DetailMask") != null) charring.Add(own[i]);
                }
            }
        }

        static bool Matches(string name, string[] wanted)
        {
            string n = name.ToLowerInvariant();
            return wanted.Any(w => n.Contains(w));
        }

        void Update()
        {
            if (!ready) Setup();
            if (glowing.Count == 0 && charring.Count == 0) return;
            if (target == null) target = GetComponentInParent<Flight.Target>();
            float lost = target != null && target.Alive ? 1f - Mathf.Clamp01(target.HullFraction) : 0f;
            float charLevel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CharFrom, CharFull, lost));
            if (!Mathf.Approximately(charLevel, charShown))
            {
                charShown = charLevel;
                foreach (var m in charring) if (m != null) { m.SetFloat(DetailAlbedoScale, charLevel); m.SetFloat(DetailNormalScale, charLevel); }
            }
            if (glowing.Count == 0) return;
            float level = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(From, Full, lost));
            if (level <= 0f && shown == 0f) return;
            float flicker = level > 0f ? 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 0.8f, seed) : 0f;
            shown = level * flicker;
            var c = Ember * (Brightness * shown);
            c.a = 1f;
            foreach (var m in glowing) if (m != null) m.SetColor(EmissionId, c);
        }

        void OnDestroy()
        {
            foreach (var m in glowing.Concat(charring).Distinct()) if (m != null) Destroy(m);
        }
    }
}
