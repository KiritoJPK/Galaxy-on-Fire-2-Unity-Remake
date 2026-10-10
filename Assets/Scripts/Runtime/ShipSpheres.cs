// ShipSpheres.cs
// Remake: the player ship's collision sphere (PlayerCollision): centred on the ship's model and reaching its farthest
// vertex, so every hull, small or big, bumps into stations, gates, freighters and asteroids with its own size (the original
// collided with the ship's centre point only). Built from the hull's meshes (not the engine glows and other additive /
// emissive layers): each mesh's own tight sphere (its vertices' box centre, out to the farthest vertex), then one sphere
// around them all from the centre of their box. The game's ship meshes aren't readable in a build, so their spheres come
// from Resources/GoF2Data/ship_spheres.json (GoF2 > Build > Ship Collision Spheres); readable meshes (mods' ships, the
// Editor) are measured directly, and a mesh in neither falls back to its bounds' corners.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class ShipSpheres
    {
        public const string TablePath = "GoF2Data/ship_spheres";

        [Serializable] public class Entry { public string mesh; public float[] c; public float r; }
        [Serializable] public class Table { public Entry[] items; }

        static Dictionary<string, (Vector3 centre, float radius)> table;
        static readonly Dictionary<Mesh, (Vector3 centre, float radius)> cache = new Dictionary<Mesh, (Vector3, float)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { table = null; cache.Clear(); }

        /// <summary>The parts of a ship that aren't hull: glows, flames, additive / emissive layers, lower details, effects.</summary>
        static readonly string[] NotHull = { "_add", "glow", "emissive", "engine", "flame", "trail", "_lod", "shadow", "muzzle", "beam", "shield", "fx" };

        public static bool IsHull(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (var s in NotHull) if (n.Contains(s)) return false;
            return true;
        }

        /// <summary>A mesh's tight sphere in its own space: the centre of its vertices' box, out to the farthest vertex.</summary>
        public static (Vector3 centre, float radius) Measure(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return (Vector3.zero, 0f);
            var b = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices) b.Encapsulate(v);
            float r2 = 0f;
            foreach (var v in vertices) r2 = Mathf.Max(r2, (v - b.center).sqrMagnitude);
            return (b.center, Mathf.Sqrt(r2));
        }

        static void LoadTable()
        {
            table = new Dictionary<string, (Vector3, float)>(StringComparer.Ordinal);
            var text = Resources.Load<TextAsset>(TablePath);
            if (text == null) return;
            var t = JsonUtility.FromJson<Table>(text.text);
            if (t?.items == null) return;
            foreach (var e in t.items)
                if (!string.IsNullOrEmpty(e.mesh) && e.c != null && e.c.Length == 3) table[e.mesh] = (new Vector3(e.c[0], e.c[1], e.c[2]), e.r);
        }

        /// <summary>A mesh's sphere in its own space: measured (readable / Editor), else the table, else its bounds' corners.</summary>
        public static (Vector3 centre, float radius) ForMesh(Mesh mesh)
        {
            if (mesh == null) return (Vector3.zero, 0f);
            if (cache.TryGetValue(mesh, out var s)) return s;
            bool done = false;
            if (mesh.isReadable || Application.isEditor)
            {
                try { s = Measure(mesh.vertices); done = s.radius > 0f; } catch (Exception) { }
            }
            if (!done)
            {
                if (table == null) LoadTable();
                string name = mesh.name.Replace(" Instance", "");
                if (table.TryGetValue(name, out var t)) { s = t; done = true; }
            }
            if (!done) s = (mesh.bounds.center, mesh.bounds.extents.magnitude);
            cache[mesh] = s;
            return s;
        }

        /// <summary>The ship's sphere in its own space (the root's): centred on its hull, reaching its farthest part. Radius 0
        /// without any hull mesh (then it is a point, as in the original).</summary>
        public static (Vector3 centre, float radius) ForShip(Transform ship)
        {
            var spheres = new List<(Vector3 c, float r)>();
            float rootScale = MaxAbs(ship.lossyScale);
            if (rootScale < 1e-6f) rootScale = 1f;
            foreach (var mf in ship.GetComponentsInChildren<MeshFilter>(false))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled || !IsHull(mf.name) || !IsHull(mf.sharedMesh.name)) continue;
                var (c, r) = ForMesh(mf.sharedMesh);
                if (r <= 0f) continue;
                var local = ship.InverseTransformPoint(mf.transform.TransformPoint(c));
                spheres.Add((local, r * MaxAbs(mf.transform.lossyScale) / rootScale));
            }
            if (spheres.Count == 0) return (Vector3.zero, 0f);
            var box = new Bounds(spheres[0].c, Vector3.one * spheres[0].r * 2f);
            foreach (var (c, r) in spheres) box.Encapsulate(new Bounds(c, Vector3.one * r * 2f));
            float radius = 0f;
            foreach (var (c, r) in spheres) radius = Mathf.Max(radius, (c - box.center).magnitude + r);
            return (box.center, radius * rootScale);
        }

        static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
    }
}
