// ShipSphereBuilder.cs
// Menu "GoF2/Build/Ship Collision Spheres": Resources/GoF2Data/ship_spheres.json, each ship mesh's tight sphere (its
// vertices' box centre and the distance to its farthest vertex, in the mesh's own space) for the player ship's collision
// sphere (Flight.ShipSpheres). The game's ship models aren't readable in a build, so the build reads this table; run it again
// after adding or changing a ship model.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GoF2Remake.Flight;
using UnityEditor;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    public static class ShipSphereBuilder
    {
        const string OutPath = "Assets/Resources/" + ShipSpheres.TablePath + ".json";

        [MenuItem("GoF2/Build/Ship Collision Spheres", priority = 209)]
        public static void Build()
        {
            var entries = new SortedDictionary<string, (Vector3 c, float r)>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/ships/")) continue;
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                {
                    if (mesh == null || !ShipSpheres.IsHull(mesh.name) || entries.ContainsKey(mesh.name)) continue;
                    var s = ShipSpheres.Measure(mesh.vertices);
                    if (s.radius > 0f) entries[mesh.name] = s;
                }
            }
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\"items\":[\n");
            int n = 0;
            foreach (var kv in entries)
            {
                var (c, r) = kv.Value;
                sb.Append(n++ == 0 ? "" : ",\n");
                sb.Append(string.Format(ci, "{{\"mesh\":\"{0}\",\"c\":[{1:0.###},{2:0.###},{3:0.###}],\"r\":{4:0.###}}}", kv.Key, c.x, c.y, c.z, r));
            }
            sb.Append("\n]}\n");
            File.WriteAllText(OutPath, sb.ToString());
            AssetDatabase.ImportAsset(OutPath);
            Debug.Log($"ShipSphereBuilder: {entries.Count} ship meshes written to {OutPath}");
        }
    }
}
