// EmissiveReflection.cs
// Remake: the *_emissive layers (GoF2/Unlit: a station's lit panels, a gate's inner ring, a ship's markings) take the
// hull's cubemap reflection and rim too (GoF2/EmissiveCube). They are drawn over parts of the hull with its own atlas,
// unlit and opaque, so where they lie the reflection GoF2/Hull gives the rest of the hull was gone: those panels looked
// flat next to the shining hull around them. The hull material in the same object that uses the same atlas lends its
// normal map and specular (the emissive material has the diffuse only). Station interiors keep URP Lit (no GoF2/Hull),
// so their emissive layers stay as they are; so do layers whose atlas no hull of the object uses.
// Remake: GoF2/AlphaTest layers too (sn_station_*_wrecked_alpha_emissive: a wrecked station's torn panels had no
// reflection): the copy clips like them (_ALPHATEST_ON) and draws both faces, in their render queue.
// Remake: and the *_alpha layers on GoF2/AlphaTest (sn_plasma_gun_valkyrie_alpha: the Valkyrie's plasma gun's cut-out
// plates had no reflection of the space cubemap next to the shining hull).

using System.Collections.Generic;
using UnityEngine;

namespace GoF2Remake.Visuals
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class EmissiveReflection
    {
        const string ShaderPath = "GoF2Emissive/EmissiveCube";
        static Shader shader;
        static bool loaded;
        // (emissive material, hull material) -> the reflecting copy, shared by every object that uses the pair.
        static readonly Dictionary<(Material, Material), Material> cache = new Dictionary<(Material, Material), Material>();

        static readonly int MainTexId = Shader.PropertyToID("_MainTex"), BaseMapId = Shader.PropertyToID("_BaseMap"),
            BumpMapId = Shader.PropertyToID("_BumpMap"), GlossMapId = Shader.PropertyToID("_MetallicGlossMap"),
            ColorId = Shader.PropertyToID("_Color"), CullId = Shader.PropertyToID("_Cull"), GlowId = Shader.PropertyToID("_Glow"), VertexColorId = Shader.PropertyToID("_UseVertexColor");

        /// <summary>Gives the emissive layers under 'root' the hull's reflection (AssembledObject.Awake).</summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;
            if (!loaded) { loaded = true; shader = Resources.Load<Shader>(ShaderPath); }
            if (shader == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Dictionary<Texture, Material> hulls = null;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                bool emissiveLayer = r.gameObject.name.Contains("_emissive");
                if (!emissiveLayer && !r.gameObject.name.Contains("_alpha")) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null || !m.HasProperty(MainTexId)) continue;
                    bool alphaTest = m.shader.name == "GoF2/AlphaTest";
                    if (!alphaTest && (m.shader.name != "GoF2/Unlit" || !emissiveLayer)) continue;   // an _alpha layer: AlphaTest only
                    var atlas = m.GetTexture(MainTexId);
                    if (atlas == null) continue;
                    hulls ??= Hulls(renderers);
                    if (!hulls.TryGetValue(atlas, out var hull)) continue;
                    if (!cache.TryGetValue((m, hull), out var shine) || shine == null)
                    {
                        shine = new Material(shader) { name = m.name + " (reflection)" };
                        shine.SetTexture(MainTexId, atlas);
                        shine.SetTextureScale(MainTexId, m.GetTextureScale(MainTexId));
                        shine.SetTextureOffset(MainTexId, m.GetTextureOffset(MainTexId));
                        if (m.HasProperty(ColorId)) shine.SetColor(ColorId, m.GetColor(ColorId));
                        if (m.HasProperty(GlowId)) shine.SetFloat(GlowId, m.GetFloat(GlowId));
                        if (m.HasProperty(VertexColorId)) shine.SetFloat(VertexColorId, m.GetFloat(VertexColorId));
                        shine.SetTexture(BumpMapId, hull.GetTexture(BumpMapId));
                        shine.SetTexture(GlossMapId, hull.GetTexture(GlossMapId));
                        if (alphaTest)
                        {
                            shine.EnableKeyword("_ALPHATEST_ON");
                            shine.SetFloat(CullId, (float)UnityEngine.Rendering.CullMode.Off);   // the graph's Render Face: Both
                            shine.renderQueue = m.renderQueue;
                        }
                        cache[(m, hull)] = shine;
                    }
                    mats[i] = shine;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>The object's GoF2/Hull materials with a specular map, by their atlas.</summary>
        static Dictionary<Texture, Material> Hulls(Renderer[] renderers)
        {
            var hulls = new Dictionary<Texture, Material>();
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name != "GoF2/Hull") continue;
                    var atlas = m.HasProperty(BaseMapId) ? m.GetTexture(BaseMapId) : null;
                    if (atlas == null || hulls.ContainsKey(atlas) || m.GetTexture(GlossMapId) == null) continue;
                    hulls[atlas] = m;
                }
            return hulls;
        }
    }
}
