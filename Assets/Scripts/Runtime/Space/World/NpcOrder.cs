// NpcOrder.cs
// Remake events: a scripted order for an NPC ship (the event graphs' ship nodes and /npc, Events.EventShipOrders), which its
// own AI defers to while it lasts (NpcShip.SetOrder / UpdateOrder): fly to a point or along a route (looped or not), follow
// or escort a leader in formation, attack one target, flee and jump out, fly into the station and vanish. It steers like
// the ship's own flight (NpcShip.Steer: the original's turn rate, banking, obstacle avoidance), so a point within the ship's
// turning circle (~3700 game units across at the base speed) is reached by the arrival radius, not exactly. When it ends
// the ship holds still, resumes its own AI, vanishes or jumps out ("then"). Positions are Unity world space.

using System.Collections.Generic;
using GoF2Remake.Flight;
using UnityEngine;

namespace GoF2Remake.World
{
    public sealed class NpcOrder
    {
        public enum Kind { Move, Follow, Attack, Flee, Dock }
        public enum Then { Resume, Hold, Vanish, Jump }

        public Kind kind;
        public Then then = Then.Resume;
        /// <summary>Move: the points to fly through (Unity), the current one.</summary>
        public readonly List<Vector3> points = new List<Vector3>();
        public int index;
        public bool loop;
        /// <summary>Game units: a point counts as reached within it (Move), the station within it (Dock).</summary>
        public float radius = 2500f;
        /// <summary>u/ms; below 0: the ship's own (a freighter's 1 u/ms).</summary>
        public float speed = -1f;
        /// <summary>Fights enemies it meets on the way (an escort), then goes on.</summary>
        public bool fight;
        /// <summary>Follow: the leader and the formation offset in its frame (game units: right, up, forward).</summary>
        public Transform leader;
        public Vector3 offset = new Vector3(0f, 0f, -1500f);
        /// <summary>Attack: the one target; the order ends when it is gone.</summary>
        public Target target;
        /// <summary>Flee: away from this point (Unity), jumping out after the seconds.</summary>
        public Vector3 from;
        public float seconds = 6f;
    }
}
