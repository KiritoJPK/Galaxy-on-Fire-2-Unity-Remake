// Obstacle.cs
// Something with bounding volumes (CollisionVolume) that the player slides along and NPC fighters steer out of:
// the station, the visible jumpgate and freighters (their wreck after the death animation). Fighters and the player
// have none: the player flies through fighters, and fighters through each other (ship_combat.md 2.9,
// npc_traffic_ai.md 5.7). Asteroids are handled separately (touching one destroys it, PlayerCollision).
//   PlayerStation::outerCollide 0x147f40       contact = inside the cube +-(bounding radius + 5000 units) around the
//                                              station and inside one of its volumes
//   PlayerStaticFar::collide 0x141498          jumpgate: contact = inside the cube +-radius around the gate; the push
//                                              only moves the player out of the sphere (in the cube's corners the
//                                              camera shakes without a push, like the original)
//   PlayerFixedObject::outerCollide 0x18017a   freighters: inside one of the boxes (the wreck boxes once dead)
//   *::getProjectionVector                     NPC avoidance direction: away from the touched volume's centre
//                                              (stations, gates) or from the ship's position (freighters)
// Remake: an obstacle can collide with its own model instead (UseMeshes: MeshColliders on its hull, a ship being a sphere of
// MeshProbeRadius against them, pushed out along the penetration; SweepBlocked stops a fast ship at the surface it would
// have passed through). Kothar's damaged station uses it (OrbitBuilder), its boxes having been far off its hull.

using System.Collections.Generic;
using UnityEngine;

namespace GoF2Remake.Flight
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class Obstacle : MonoBehaviour
    {
        public static readonly List<Obstacle> All = new List<Obstacle>();

        /// <summary>Level::getLandmarks (station, jumpgate): checked before ships.</summary>
        public bool landmark;
        /// <summary>PlayerStation: touching it with the autopilot to the station docks (MGame::dockEvent).</summary>
        public bool isStation;
        /// <summary>The jumpgate (PlayerStaticFar): contact is the cube +-cubeHalf itself.</summary>
        public bool cubeIsContact;
        /// <summary>Metres: the station's prefilter cube / the jumpgate's contact cube; 0 = none.</summary>
        public float cubeHalf;
        /// <summary>The avoidance direction starts at the touched volume's centre (else at the owner's position).</summary>
        public bool projectFromVolume = true;
        public List<CollisionVolume> volumes = new List<CollisionVolume>();
        /// <summary>Remake: the model's own surfaces (UseMeshes); when set they are the collision instead of the volumes.</summary>
        public List<MeshCollider> meshes = new List<MeshCollider>();
        /// <summary>Remake: metres, the size of a ship against the meshes.</summary>
        public const float MeshProbeRadius = 8f;
        const int MeshIndex = -2;

        bool UsesMeshes => meshes != null && meshes.Count > 0;
        public bool Active => isActiveAndEnabled && (UsesMeshes || (volumes != null && volumes.Count > 0));

        /// <summary>Remake: collide with the model's surfaces: a MeshCollider (no rigidbody, ignored by raycasts) on each of its
        /// solid meshes under 'root' (not the additive / emissive layers, the flames or the lower details). The meshes must be
        /// readable (Read/Write); returns false (and keeps the volumes) when none could be used.</summary>
        public bool UseMeshes(GameObject root)
        {
            meshes.Clear();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                string n = mf.name;
                if (mesh == null || n.Contains("_add") || n.Contains("emissive") || n.Contains("emitters") || n.Contains("_lod")
                    || n.StartsWith("Flame")) continue;
                if (!mesh.isReadable) { Debug.LogWarning($"Obstacle: {mesh.name} isn't readable (Read/Write): no mesh collision for it"); continue; }
                var mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
                mc.isTrigger = false;
                mf.gameObject.layer = 2;   // Ignore Raycast
                meshes.Add(mc);
            }
            return meshes.Count > 0;
        }

        static readonly MaterialPropertyBlock shownBlock = new MaterialPropertyBlock();

        /// <summary>The surface is there now: its renderer drawn (active, enabled, not scaled to nothing, its `extra` fade
        /// not at 0). A station's parts that only show later (the Supernova era's extra arms) don't collide while hidden.</summary>
        static bool Shown(MeshCollider mc)
        {
            if (mc == null || !mc.enabled || !mc.gameObject.activeInHierarchy) return false;
            var r = mc.GetComponent<Renderer>();
            if (r == null || !r.enabled) return false;
            var s = mc.transform.lossyScale;
            if (Mathf.Abs(s.x) < 1e-4f || Mathf.Abs(s.y) < 1e-4f || Mathf.Abs(s.z) < 1e-4f) return false;
            if (!r.HasPropertyBlock()) return true;
            r.GetPropertyBlock(shownBlock);
            if (shownBlock.HasFloat("_Fade") && shownBlock.GetFloat("_Fade") < 0.01f) return false;
            if (shownBlock.HasColor("_Color")) { var c = shownBlock.GetColor("_Color"); if (c.a < 0.01f || c.maxColorComponent < 0.01f) return false; }
            return true;
        }

        static SphereCollider probe;
        static SphereCollider Probe
        {
            get
            {
                if (probe != null) return probe;
                var go = new GameObject("Obstacle probe") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
                probe = go.AddComponent<SphereCollider>();
                probe.isTrigger = true;
                probe.radius = MeshProbeRadius;
                go.transform.position = new Vector3(0f, -1e7f, 0f);
                return probe;
            }
        }

        /// <summary>The push out of the meshes for a ship (a sphere of 'radius' at 'world'; zero = clear of them).</summary>
        Vector3 MeshPush(Vector3 world, float radius)
        {
            var push = Vector3.zero;
            var probeCollider = Probe;
            probeCollider.radius = radius;
            var box = new Bounds(world, Vector3.one * (radius * 2f));
            foreach (var mc in meshes)
            {
                if (!Shown(mc) || !mc.bounds.Intersects(box)) continue;
                if (Physics.ComputePenetration(probeCollider, world + push, Quaternion.identity, mc, mc.transform.position, mc.transform.rotation,
                                               out var dir, out float dist))
                    push += dir * dist;
            }
            return push;
        }

        /// <summary>Remake: the size a ship is against this obstacle: its own sphere (PlayerCollision), else the old probe for
        /// the meshes and a point for the volumes, as the original.</summary>
        static float MeshRadius(float radius) => radius > 0f ? radius : MeshProbeRadius;

        /// <summary>Remake: a volume grown by a ship's radius (a box by the radius on every side, a sphere by it).</summary>
        static CollisionVolume Grown(CollisionVolume v, float radius) =>
            radius <= 0f ? v : v.sphere ? CollisionVolume.Sphere(v.centre, v.radius + radius) : CollisionVolume.Box(v.centre, v.half + Vector3.one * radius);

        /// <summary>Remake: a ship going from 'from' to 'to' crosses the meshes: 'stop' is short of the surface it hit.</summary>
        public bool SweepBlocked(Vector3 from, Vector3 to, out Vector3 stop) => SweepBlocked(from, to, 0f, out stop);

        /// <summary>The same for a ship of 'radius' (its centre's path).</summary>
        public bool SweepBlocked(Vector3 from, Vector3 to, float radius, out Vector3 stop)
        {
            stop = to;
            if (!UsesMeshes || !isActiveAndEnabled) return false;
            float r = MeshRadius(radius);
            var d = to - from;
            float len = d.magnitude;
            if (len < r) return false;   // short steps: the penetration push handles them
            var ray = new Ray(from, d / len);
            bool hit = false; float best = len + r;
            foreach (var mc in meshes)
            {
                if (!Shown(mc)) continue;
                if (mc.Raycast(ray, out var h, best)) { best = h.distance; hit = true; }
            }
            if (hit && best < len + r) stop = from + ray.direction * Mathf.Max(0f, Mathf.Min(len, best - r));
            return hit && best < len + r;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>outerCollide: 'world' touches this object; 'index' = the volume touched (-1 = only the gate cube).</summary>
        public bool Touches(Vector3 world, out int index) => Touches(world, 0f, out index);

        /// <summary>Remake: the same for a ship that is a sphere of 'radius' around 'world' (0 = a point, the original).</summary>
        public bool Touches(Vector3 world, float radius, out int index)
        {
            index = -1;
            var p = world - transform.position;
            float cube = cubeHalf + Mathf.Max(0f, radius);
            if (cubeHalf > 0f && (Mathf.Abs(p.x) >= cube || Mathf.Abs(p.y) >= cube || Mathf.Abs(p.z) >= cube)) return false;
            if (UsesMeshes)
            {
                if (MeshPush(world, MeshRadius(radius)) == Vector3.zero) return false;
                index = MeshIndex;
                return true;
            }
            for (int i = 0; i < volumes.Count; i++)
                if (Grown(volumes[i], radius).Contains(p)) { index = i; return true; }
            return cubeIsContact;
        }

        /// <summary>projectCollisionOnSurface: 'world' moved out of every volume containing it.</summary>
        public Vector3 PushOut(Vector3 world) => PushOut(world, 0f);

        /// <summary>Remake: the same for a ship that is a sphere of 'radius' around 'world'.</summary>
        public Vector3 PushOut(Vector3 world, float radius)
        {
            if (UsesMeshes)
            {
                float r = MeshRadius(radius);
                for (int pass = 0; pass < 3; pass++)   // a corner between two surfaces: a few pushes
                {
                    var push = MeshPush(world, r);
                    if (push == Vector3.zero) break;
                    world += push;
                }
                return world;
            }
            if (radius <= 0f) return transform.position + CollisionVolume.ProjectOut(world - transform.position, volumes);
            var grown = new List<CollisionVolume>(volumes.Count);
            foreach (var v in volumes) grown.Add(Grown(v, radius));
            return transform.position + CollisionVolume.ProjectOut(world - transform.position, grown);
        }

        /// <summary>getProjectionVector: the unit vector away from the touched volume (or the owner).</summary>
        public Vector3 ProjectionVector(Vector3 world, int index)
        {
            if (index == MeshIndex) { var push = MeshPush(world, MeshProbeRadius); return push.sqrMagnitude > 1e-8f ? push.normalized : Vector3.zero; }
            var from = transform.position;
            if (projectFromVolume && index >= 0 && index < volumes.Count) from += volumes[index].centre;
            var d = world - from;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.zero;
        }

        /// <summary>Editor aid: the volumes as wire boxes / spheres.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            if (UsesMeshes)   // Remake: the surfaces it collides with
            {
                foreach (var mc in meshes)
                    if (Shown(mc) && mc.sharedMesh != null) Gizmos.DrawWireMesh(mc.sharedMesh, mc.transform.position, mc.transform.rotation, mc.transform.lossyScale);
                return;
            }
            if (volumes == null) return;
            foreach (var v in volumes)
            {
                if (v.sphere) Gizmos.DrawWireSphere(transform.position + v.centre, v.radius);
                else Gizmos.DrawWireCube(transform.position + v.centre, v.half * 2f);
            }
        }
    }
}
