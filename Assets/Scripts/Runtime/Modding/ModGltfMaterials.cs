// ModGltfMaterials.cs
// glTFast's material generator for mods' models: every glTF material becomes a copy of the game's URP Lit templates
// (ModMaterials.FromGltf) instead of glTFast's own shader graphs, which builds wouldn't carry. 'images' gives the textures
// the mods' loader made for the GLB's images (ModGltf: the texture index, its role, and for a metallic-roughness map the
// material's metallic / roughness factors; null = glTFast's own).

using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using GLTFast.Schema;

namespace GoF2Remake.Modding
{
    public class ModGltfMaterials : IMaterialGenerator
    {
        readonly System.Func<int, ModGlbImages.Role, float, float, UnityEngine.Texture2D> images;

        public ModGltfMaterials(System.Func<int, ModGlbImages.Role, float, float, UnityEngine.Texture2D> images = null) { this.images = images; }

        public UnityEngine.Material GetDefaultMaterial(bool pointsSupport = false) => ModMaterials.Default();

        public UnityEngine.Material GenerateMaterial(MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false) =>
            ModMaterials.FromGltf(gltfMaterial, gltf, images);

        public void SetLogger(ICodeLogger logger) { }
    }
}
