// ModHullLightingTests.cs
// Feature: CLAUDE.md "Space scene" > Hull reflection, mod materials: the mods' material templates (Resources/GoF2Mods/
// ModAssets, ModAssetsBuilder) carry _GOF2_PBR, so GoF2/Hull leaves its GoF2 reflection and rim out for them (a mod's
// metallic / smoothness alpha is PBR smoothness, not the original's specular mask: the mods' hulls came out milky white in
// the hangars); in a GoF2 hangar (SkyReflection.SetHangar) their indirect light is dimmed and their main light comes from
// above, out of a system (SetSystem: space, the lounge) neither applies.

using GoF2Remake.Modding;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class ModHullLightingTests
    {
        [Test]
        public void TheModTemplatesArePbrHulls()
        {
            var a = Resources.Load<ModAssets>(ModAssets.ResourcePath);
            Assert.IsNotNull(a, "Resources/" + ModAssets.ResourcePath);
            foreach (var m in new[] { a.litOpaque, a.litOpaqueDetail, a.litCutout, a.litCutoutDetail, a.litTransparent, a.litTransparentDetail })
            {
                Assert.IsNotNull(m);
                Assert.AreEqual("GoF2/Hull", m.shader.name, m.name);
                Assert.IsTrue(m.IsKeywordEnabled("_GOF2_PBR"), m.name + ": no GoF2 terms on a mod's PBR maps");
            }
        }

        [Test]
        public void AGoF2HangarLightsTheModHullsFromAbove()
        {
            float dim = Shader.GetGlobalFloat("_GoF2PbrIndirectDim");
            var light = Shader.GetGlobalVector("_GoF2PbrLight");
            try
            {
                SkyReflection.SetHangar(0);
                Assert.AreEqual(SkyReflection.HangarPbrIndirectDim, Shader.GetGlobalFloat("_GoF2PbrIndirectDim"), 1e-5f);
                var l = Shader.GetGlobalVector("_GoF2PbrLight");
                Assert.AreEqual(1f, l.w, 1e-5f, "on in a hangar");
                Assert.Greater(l.y, 0f, "from above");
                Assert.AreEqual(SkyReflection.HangarPbrLightStrength, Shader.GetGlobalColor("_GoF2PbrLightColor").r, 1e-5f);
            }
            finally
            {
                Shader.SetGlobalFloat("_GoF2PbrIndirectDim", dim);
                Shader.SetGlobalVector("_GoF2PbrLight", light);
            }
        }
    }
}
