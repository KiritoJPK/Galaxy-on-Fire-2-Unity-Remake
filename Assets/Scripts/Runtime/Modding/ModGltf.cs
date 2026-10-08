// ModGltf.cs
// Loads a mod's glTF / GLB model with glTFast (ships, stations, rooms, weapon fx), its embedded images made by the mods' own
// texture loader instead of glTFast's (ModGlbImages: glTFast keeps each model's images uncompressed, one copy per model):
// the GLB's images are taken out on a worker thread, each different one loaded once per mod, compressed and cached
// (ModMaterials.PreloadImage, its role's conversion done on the CPU: metallic-roughness to URP's metallic / smoothness,
// a normal map's Android layout), then glTFast reads the rest and ModGltfMaterials puts them on the materials.

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public static class ModGltf
    {
        /// <summary>The model read by glTFast (null: glTFast couldn't read it). The import owns the meshes and materials:
        /// keep it until they go, then Dispose it.</summary>
        public static async Task<GLTFast.GltfImport> Load(ModInfo mod, byte[] bytes, GLTFast.IDeferAgent agent)
        {
            ModGlbImages.Result stripped = null;
            try { stripped = await Task.Run(() => ModGlbImages.Strip(bytes)); }
            catch (System.Exception e) { Debug.LogWarning($"Mods: {mod.Id}: a model's images are left to glTFast ({e.Message})"); }
            System.Func<int, ModGlbImages.Role, Texture2D> images = null;
            if (stripped != null)
            {
                bytes = stripped.glb;
                var tasks = new List<Task>();
                foreach (var img in stripped.images.Values)
                    foreach (var role in new[] { ModGlbImages.Role.Color, ModGlbImages.Role.Normal, ModGlbImages.Role.MetallicRoughness })
                        if ((img.roles & role) != 0) tasks.Add(ModMaterials.PreloadImage(mod, img.hash, img.ext, img.bytes, role));
                await Task.WhenAll(tasks);
                foreach (var img in stripped.images.Values) img.bytes = null;
                images = (texture, role) =>
                    stripped.textureImage.TryGetValue(texture, out int i) && stripped.images.TryGetValue(i, out var img)
                        ? ModMaterials.Image(mod, img.hash, role) : null;
            }
            var import = new GLTFast.GltfImport(null, agent, new ModGltfMaterials(images));
            var settings = new GLTFast.ImportSettings { GenerateMipMaps = true, AnisotropicFilterLevel = 8 };
            if (await import.Load(bytes, null, settings)) return import;
            import.Dispose();
            return null;
        }
    }
}
