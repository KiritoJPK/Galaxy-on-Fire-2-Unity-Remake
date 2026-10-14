// Route.cs
// Route / Waypoint (Reference/research/npc_traffic_ai.md 5.8): waypoints in game units, the current one is reached
// when the ship is within 2000 units on all three axes (Route::update 0x140f80), then the next; after the last it loops
// or the route ends (Waypoint() returns null).
//   PlayerFighter::PlayerFighter 0xefec0  the default patrol every fighter gets: 2, 3 or 4 of the corners
//     P0 (-30000..-5000, -10000..0, 20000..45000), P1 (5000..30000, -10000..0, 20000..45000),
//     P2 (5000..30000, -10000..0, 55000..80000), P3 (-30000..-5000, -10000..0, 55000..80000)
//   in random order without repeats, looping: a box in front of the station (the undock spawn is at z 10000).
//   The Void uses the shared square (+-40000, 0, +-40000).

using System.Collections.Generic;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public class Route
    {
        public const float ReachUnits = 2000f;

        public readonly List<Vector3> points = new List<Vector3>();   // game units
        public bool loop;
        public int index;

        public Route(bool loop) => this.loop = loop;

        public Route Clone()
        {
            var r = new Route(loop) { index = 0 };
            r.points.AddRange(points);
            return r;
        }

        /// <summary>Route::getWaypoint: the current waypoint (game units), null when a non-looping route is finished.</summary>
        public Vector3? Waypoint => index < points.Count ? points[index] : (Vector3?)null;

        /// <summary>Route::update: advance when 'posGame' reached the current waypoint.</summary>
        public void Update(Vector3 posGame)
        {
            if (index >= points.Count) return;
            var d = points[index] - posGame;
            if (Mathf.Abs(d.x) >= ReachUnits || Mathf.Abs(d.y) >= ReachUnits || Mathf.Abs(d.z) >= ReachUnits) return;
            index++;
            if (index >= points.Count && loop) index = 0;
        }

        /// <summary>The fighters' default patrol (race 9: the square).</summary>
        public static Route DefaultPatrol(int race)
        {
            var r = new Route(true);
            if (race == Standing.Void)
            {
                r.points.Add(new Vector3(-40000f, 0f, -40000f)); r.points.Add(new Vector3(40000f, 0f, -40000f));
                r.points.Add(new Vector3(40000f, 0f, 40000f)); r.points.Add(new Vector3(-40000f, 0f, 40000f));
                return r;
            }
            var corners = new List<Vector3>
            {
                new Vector3(Random.Range(0, 25000) - 30000, Random.Range(0, 10000) - 10000, Random.Range(0, 25000) + 20000),
                new Vector3(Random.Range(0, 25000) + 5000, Random.Range(0, 10000) - 10000, Random.Range(0, 25000) + 20000),
                new Vector3(Random.Range(0, 25000) + 5000, Random.Range(0, 10000) - 10000, Random.Range(0, 25000) + 55000),
                new Vector3(Random.Range(0, 25000) - 30000, Random.Range(0, 10000) - 10000, Random.Range(0, 25000) + 55000),
            };
            int n = Random.Range(0, 3) + 2;
            for (int i = 0; i < n; i++)
            {
                int k = Random.Range(0, corners.Count);
                r.points.Add(corners[k]);
                corners.RemoveAt(k);
            }
            return r;
        }
    }
}
