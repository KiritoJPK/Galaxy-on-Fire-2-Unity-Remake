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
        /// <summary>Remake: the player slides along this hull's MeshColliders instead of the volumes (HullCollision); NPC
        /// fighters still steer by the volumes. Null = none.</summary>
        [System.NonSerialized] public HullBody hull;
        /// <summary>The player collides with the hull, not the volumes.</summary>
        public bool UsesHull => hull != null && hull.Active;

        public bool Active => isActiveAndEnabled && volumes != null && volumes.Count > 0;

        /// <summary>Remake (a fleet battle's capital ships turn, CapitalShip): the volumes were made for 'baseRotation' and
        /// turn with the owner from there (the original's are never rotated).</summary>
        public bool rotates;
        public Quaternion baseRotation = Quaternion.identity;

        /// <summary>'world' in the volumes' frame (relative to the owner, turned back to the base rotation).</summary>
        Vector3 Local(Vector3 world)
        {
            var p = world - transform.position;
            return rotates ? baseRotation * (Quaternion.Inverse(transform.rotation) * p) : p;
        }

        Vector3 ToWorld(Vector3 local) => transform.position + (rotates ? transform.rotation * (Quaternion.Inverse(baseRotation) * local) : local);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>outerCollide: 'world' touches this object; 'index' = the volume touched (-1 = only the gate cube).</summary>
        public bool Touches(Vector3 world, out int index)
        {
            index = -1;
            var p = Local(world);
            if (cubeHalf > 0f && (Mathf.Abs(p.x) >= cubeHalf || Mathf.Abs(p.y) >= cubeHalf || Mathf.Abs(p.z) >= cubeHalf)) return false;
            for (int i = 0; i < volumes.Count; i++)
                if (volumes[i].Contains(p)) { index = i; return true; }
            return cubeIsContact;
        }

        /// <summary>projectCollisionOnSurface: 'world' moved out of every volume containing it.</summary>
        public Vector3 PushOut(Vector3 world) => ToWorld(CollisionVolume.ProjectOut(Local(world), volumes));

        /// <summary>getProjectionVector: the unit vector away from the touched volume (or the owner).</summary>
        public Vector3 ProjectionVector(Vector3 world, int index)
        {
            var from = projectFromVolume && index >= 0 && index < volumes.Count ? ToWorld(volumes[index].centre) : transform.position;
            var d = world - from;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.zero;
        }

        /// <summary>Editor aid: the volumes as wire boxes / spheres.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            if (volumes == null) return;
            // Remake: an object with its real hull (HullCollision) shows that (its MeshColliders) instead; the volumes still
            // steer the NPC fighters round it, but the player and the shots no longer touch them.
            if (GetComponentInChildren<HullBody>(true) != null) return;
            foreach (var v in volumes)
            {
                if (v.sphere) Gizmos.DrawWireSphere(transform.position + v.centre, v.radius);
                else Gizmos.DrawWireCube(transform.position + v.centre, v.half * 2f);
            }
        }
    }
}
