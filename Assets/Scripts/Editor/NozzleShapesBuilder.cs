// NozzleShapesBuilder.cs
// GoF2 > Build > Nozzle Shapes: Resources/GoF2Data/nozzle_shapes.json, the shape of every exhaust nozzle of the game's ships
// (0..63), for the mods' exhaust styles (Modding.ModExhausts: an effect takes the width, height and centre of the nozzle it
// sits on). Measured on each ship's player engine glow mesh (*_engine_glow_add, the glow the original draws in its nozzles):
// every vertex goes to its nearest exhaust mount (weapons_hd slot 3), and each mount's vertices give its half width / height
// (game units) and their centre's offset from the mount (Unity metres in the model's frame). The game's meshes aren't
// readable in a build, so this runs in the Editor. A mount whose vertices are too few or too far off (a glow shared between
// mounts that don't match it) gets none (the round size then, ModExhausts.NozzleRadius).

using System.Collections.Generic;
using System.IO;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.Modding;
using GoF2Remake.Visuals;
using UnityEditor;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    public static class NozzleShapesBuilder
    {
        const string OutPath = "Assets/Resources/GoF2Data/nozzle_shapes.json";
        const int Ships = 64;
        const float M = 0.05f;

        [MenuItem("GoF2/Build/Nozzle Shapes", priority = 207)]
        public static void Build()
        {
            var db = Database.Load();
            var file = new ModExhausts.ShapeFile();
            int measured = 0, skipped = 0;
            for (int ship = 0; ship < Ships; ship++)
            {
                var entry = db.ShipAssembly(ship);
                var prefab = entry != null ? AssembledObject.LoadPrefab(entry) : null;
                var mounts = db.MountsOf(ship, 3);
                if (prefab == null || mounts.Count == 0) continue;
                var go = Object.Instantiate(prefab);
                try
                {
                    var asm = go.GetComponent<AssembledObject>();
                    var glow = asm != null && asm.playerVariantParts != null && asm.playerVariantParts.Length > 0 ? asm.playerVariantParts[0] : null;
                    if (glow == null) continue;
                    var points = new List<Vector3>();
                    foreach (var mf in glow.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null)
                            foreach (var v in mf.sharedMesh.vertices) points.Add(go.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
                    var at = mounts.ConvertAll(WeaponSystem.MountToLocal);
                    var shapes = ModExhausts.Measure(points, at, mounts.ConvertAll(ModHardpoints.MountTurn), float.MaxValue);
                    var s = new ModExhausts.ShipShapes { ship = ship };
                    for (int i = 0; i < mounts.Count; i++)
                    {
                        var sh = shapes[i] ?? new ModExhausts.NozzleShape();
                        float R(float v, float k) => Mathf.Round(v * k) / k;   // (a smaller file)
                        sh.mount = new[] { R(at[i].x, 1000f), R(at[i].y, 1000f), R(at[i].z, 1000f) };
                        sh.halfWidth = R(sh.halfWidth, 10f); sh.halfHeight = R(sh.halfHeight, 10f);
                        sh.centreX = R(sh.centreX, 1000f); sh.centreY = R(sh.centreY, 1000f);
                        if (sh.profile != null) for (int k = 0; k < sh.profile.Length; k++) sh.profile[k] = R(sh.profile[k], 10f);
                        if (sh.halfWidth > 0f) measured++; else skipped++;
                        s.nozzles.Add(sh);
                    }
                    file.ships.Add(s);
                }
                finally { Object.DestroyImmediate(go); }
            }
            // (JsonUtility writes the floats as doubles: 23.100000381469727 back to 23.1)
            var json = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(file), @"-?\d+\.\d{5,}",
                x => float.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllText(OutPath, json);
            AssetDatabase.ImportAsset(OutPath);
            Debug.Log($"Nozzle Shapes: {measured} nozzles of {file.ships.Count} ships measured, {skipped} left round -> {OutPath}");
        }
    }
}
