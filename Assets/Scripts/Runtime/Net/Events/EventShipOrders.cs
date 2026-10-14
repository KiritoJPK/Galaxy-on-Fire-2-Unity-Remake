// EventShipOrders.cs
// Remake events: orders for the ships an event spawned (the graphs' ship nodes, /npc; NetAdmin.Order.Npc), carried out by
// the game that owns them (its traffic: Spawn made them there; others see them through their NetProxies). The text:
//   <ship name> goto <place> [speed u/ms] [radius units] [fight] [then hold | resume | vanish | jump]
//   <ship name> route <place> / <place> / ... [loop] [speed] [radius] [fight] [then ...]
//   <ship name> follow <player | ship "name"> [offset right up forward] [speed] [fight]   (escort = follow ... fight)
//   <ship name> attack <player | ship "name">
//   <ship name> flee [seconds s]          away from the player, then it jumps out
//   <ship name> dock                       into the station, gone
//   <ship name> hold | resume | jump | speed <u/ms>
// Places as the camera shots' (EventCutscene): "x y z" / "station x y z" (game coordinates of the orbit), "player [right up
// forward]", "ship <name> [x y z]". Every living ship of that name gets the order ("Name 1", "Name 2"... of a numbered
// spawn too); following ships of a group spread out to the sides. The order's places are read when it arrives (a route of
// "player ..." points stays where the player was then); follow and attack keep after their ship.

using System;
using System.Collections.Generic;
using System.Globalization;
using GoF2Remake.Flight;
using GoF2Remake.World;
using UnityEngine;

namespace GoF2Remake.Events
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class EventShipOrders
    {
        public sealed class Parsed
        {
            public string ship, verb;
            public readonly List<EventCutscene.Place> points = new List<EventCutscene.Place>();
            public EventCutscene.Place other;   // follow / attack: the leader / target
            public bool loop, fight, hasOffset;
            public Vector3 offset;
            public float speed = -1f, radius = -1f, seconds = 6f;
            public NpcOrder.Then then = NpcOrder.Then.Resume;
        }

        static readonly HashSet<string> Verbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "goto", "route", "follow", "escort", "attack", "flee", "dock", "hold", "resume", "jump", "speed" };

        public static bool TryParse(string text, out Parsed p, out string error)
        {
            p = new Parsed();
            error = null;
            var w = EventCutscene.Words(text);
            int i = 0;
            var name = new List<string>();
            while (i < w.Count && !Verbs.Contains(w[i])) name.Add(w[i++]);
            p.ship = string.Join(" ", name).Trim();
            if (p.ship.Length == 0) { error = "which ship (the name Spawn gave it)?"; return false; }
            if (i >= w.Count) { error = "what should it do (goto, route, follow, escort, attack, flee, dock, hold, resume, jump, speed)?"; return false; }
            p.verb = w[i++].ToLowerInvariant();
            switch (p.verb)
            {
                case "goto":
                {
                    if (!EventCutscene.ParsePlace(w, ref i, out var at, out error)) return false;
                    p.points.Add(at);
                    break;
                }
                case "route":
                    while (true)
                    {
                        if (!EventCutscene.ParsePlace(w, ref i, out var at, out error)) return false;
                        p.points.Add(at);
                        if (i < w.Count && w[i] == "/") { i++; continue; }
                        break;
                    }
                    break;
                case "follow":
                case "escort":
                case "attack":
                    if (!EventCutscene.ParsePlace(w, ref i, out p.other, out error)) return false;
                    if (p.other.kind != EventCutscene.PlaceKind.Player && p.other.kind != EventCutscene.PlaceKind.Ship)
                    { error = p.verb + " needs player or ship \"name\""; return false; }
                    if (p.other.offset != Vector3.zero) { p.offset = p.other.offset; p.hasOffset = true; }
                    if (p.verb == "escort") p.fight = true;
                    break;
                case "speed":
                    if (!EventCutscene.Number(w, i++, out p.speed) || p.speed <= 0f || p.speed > 50f) { error = "speed needs units per ms (2 is a fighter's)"; return false; }
                    break;
            }
            while (i < w.Count)
            {
                string k = w[i++].ToLowerInvariant();
                switch (k)
                {
                    case "speed":
                        if (!EventCutscene.Number(w, i++, out p.speed) || p.speed <= 0f || p.speed > 50f) { error = "speed needs units per ms (2 is a fighter's)"; return false; }
                        break;
                    case "radius":
                        if (!EventCutscene.Number(w, i++, out p.radius) || p.radius <= 0f) { error = "radius needs game units"; return false; }
                        break;
                    case "seconds":
                        if (!EventCutscene.Number(w, i++, out p.seconds) || p.seconds < 0f) { error = "seconds needs a number"; return false; }
                        break;
                    case "offset":
                    {
                        if (!EventCutscene.Number(w, i, out float x) || !EventCutscene.Number(w, i + 1, out float y) || !EventCutscene.Number(w, i + 2, out float z))
                        { error = "offset needs right up forward"; return false; }
                        p.offset = new Vector3(x, y, z);
                        p.hasOffset = true;
                        i += 3;
                        break;
                    }
                    case "fight": p.fight = true; break;
                    case "loop": p.loop = true; break;
                    case "then":
                    {
                        string t = i < w.Count ? w[i++].ToLowerInvariant() : "";
                        if (t == "hold") p.then = NpcOrder.Then.Hold;
                        else if (t == "resume") p.then = NpcOrder.Then.Resume;
                        else if (t == "vanish") p.then = NpcOrder.Then.Vanish;
                        else if (t == "jump") p.then = NpcOrder.Then.Jump;
                        else { error = "then needs hold, resume, vanish or jump"; return false; }
                        break;
                    }
                    default:
                        error = $"\"{w[i - 1]}\" isn't understood here";
                        return false;
                }
            }
            return true;
        }

        /// <summary>The order on this game's own ships of that name (NetAdmin.Apply); how many took it.</summary>
        public static int Apply(string text)
        {
            if (!TryParse(text, out var p, out string error)) { Debug.LogWarning($"EventShipOrders: {error} ({text})"); return 0; }
            var level = UnityEngine.Object.FindAnyObjectByType<SpaceLevel>();
            if (level == null || level.Traffic == null) return 0;
            var ships = new List<NpcShip>();
            foreach (var s in level.Traffic.Ships)
            {
                if (s == null || s.Gone || s.Target == null || !s.Target.Alive || string.IsNullOrEmpty(s.Target.displayName)) continue;
                string n = s.Target.displayName;
                if (string.Equals(n, p.ship, StringComparison.OrdinalIgnoreCase) || n.StartsWith(p.ship + " ", StringComparison.OrdinalIgnoreCase)) ships.Add(s);
            }
            for (int k = 0; k < ships.Count; k++) Give(level, ships[k], p, k);
            return ships.Count;
        }

        static void Give(SpaceLevel level, NpcShip ship, Parsed p, int k)
        {
            switch (p.verb)
            {
                case "hold": ship.SetHold(); return;
                case "resume": ship.Resume(); return;
                case "jump": ship.JumpOut(); return;
                case "speed": ship.SetSpeed(p.speed); return;
            }
            var o = new NpcOrder { then = p.then, loop = p.loop, fight = p.fight, speed = p.speed, seconds = p.seconds };
            if (p.radius > 0f) o.radius = p.radius;
            switch (p.verb)
            {
                case "goto":
                case "route":
                    o.kind = NpcOrder.Kind.Move;
                    foreach (var place in p.points)
                        if (EventCutscene.ResolvePlace(level, place, out var at)) o.points.Add(at);
                    if (o.points.Count == 0) return;
                    // A group spreads out a little around the points.
                    if (k > 0) for (int j = 0; j < o.points.Count; j++) o.points[j] += Spread(k) * 0.05f;
                    break;
                case "follow":
                case "escort":
                {
                    o.kind = NpcOrder.Kind.Follow;
                    var leader = Other(level, p.other, ship.Target);
                    if (leader == null) return;
                    o.leader = leader.transform;
                    if (p.hasOffset) o.offset = p.offset;
                    o.offset += Spread(k);   // a group's ships beside each other
                    break;
                }
                case "attack":
                {
                    o.kind = NpcOrder.Kind.Attack;
                    o.target = Other(level, p.other, ship.Target);
                    if (o.target == null) return;
                    break;
                }
                case "flee":
                    o.kind = NpcOrder.Kind.Flee;
                    o.from = level.Player != null ? level.Player.transform.position : Vector3.zero;
                    break;
                case "dock":
                    o.kind = NpcOrder.Kind.Dock;
                    if (p.radius <= 0f) o.radius = 4000f;
                    break;
                default:
                    return;
            }
            ship.SetOrder(o);
        }

        /// <summary>Formation offsets of a group's k-th ship (game units): 0, right, left, further right...</summary>
        static Vector3 Spread(int k)
        {
            if (k == 0) return Vector3.zero;
            int side = k % 2 == 1 ? 1 : -1;
            int rank = (k + 1) / 2;
            return new Vector3(side * 900f * rank, 0f, -600f * rank);
        }

        static Target Other(SpaceLevel level, EventCutscene.Place place, Target self)
        {
            if (place.kind == EventCutscene.PlaceKind.Player) return level.Traffic != null ? level.Traffic.Player : null;
            return EventCutscene.FindShip(place.name, self);
        }
    }
}
