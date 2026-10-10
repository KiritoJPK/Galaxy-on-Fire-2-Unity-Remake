// StationMeshCollisionTools.cs
// Menu "GoF2/Collision/Make Station Models Readable": Read/Write on every station and jumpgate model
// (Assets/Models/<pack>/stations/*.fbx, .../jumpgates/*.fbx).
// The option "Precise station collision" (Settings.StationMeshCollision, Obstacle.UseMeshes) builds MeshColliders from the
// station's (and jumpgate's) meshes at run time, which needs their data on the CPU; a model that isn't readable keeps the original's boxes.
// Run it once (and again for a new station model), then commit the changed .fbx.meta files.

using UnityEditor;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    public static class StationMeshCollisionTools
    {
        [MenuItem("GoF2/Collision/Make Station Models Readable", priority = 230)]
        public static void MakeReadable()
        {
            int changed = 0, total = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if ((!path.Contains("/stations/") && !path.Contains("/jumpgates/")) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                    total++;
                    if (importer.isReadable) continue;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"StationMeshCollisionTools: Read/Write turned on for {changed} of {total} station and jumpgate models");
        }

        [MenuItem("GoF2/Collision/Toggle Precise Station Collision", priority = 231)]
        public static void Toggle()
        {
            Data.Settings.StationMeshCollision = !Data.Settings.StationMeshCollision;
            Debug.Log($"Precise station collision: {(Data.Settings.StationMeshCollision ? "on" : "off")} (from the next orbit)");
        }
    }
}
