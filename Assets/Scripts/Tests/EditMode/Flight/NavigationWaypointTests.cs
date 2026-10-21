// NavigationWaypointTests.cs
// Navigation's two waypoint slots (CLAUDE.md "Navigation"; a Discord report, "(MP) Pirate Hideout mission and side mission
// waypoints same system"): an event's waypoint (EventWaypoint -> SetEventWaypoint) sits beside the player route (a campaign
// level's or a freelance mission's, SetRoute), and setting or clearing either one leaves the other.

using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class NavigationWaypointTests
    {
        GameObject go;
        Navigation nav;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("NavigationWaypointTests");
            nav = go.AddComponent<Navigation>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        static Route OnePoint(Vector3 p)
        {
            var r = new Route(false);
            r.points.Add(p);
            return r;
        }

        Navigation.Target[] Waypoints => nav.Targets.Where(t => t.kind == Navigation.Kind.Waypoint).ToArray();

        [Test]
        public void AnEventWaypointShowsBesideTheFreelanceRoute()
        {
            nav.SetRoute(OnePoint(new Vector3(1000, 0, 2000)), true);
            nav.SetEventWaypoint(OnePoint(new Vector3(-5000, 0, 9000)), "Pirate Hideout", true);
            Assert.AreEqual(2, Waypoints.Length);
            Assert.AreEqual(1, Waypoints.Count(t => t.eventWaypoint && t.name == "Pirate Hideout"));
            Assert.AreEqual(1, Waypoints.Count(t => !t.eventWaypoint && t.freelance));
        }

        [Test]
        public void ClearingTheEventWaypointKeepsTheFreelanceRoute()
        {
            nav.SetRoute(OnePoint(new Vector3(1000, 0, 2000)), true);
            nav.SetEventWaypoint(OnePoint(new Vector3(-5000, 0, 9000)), null, true);
            nav.SetEventWaypoint(null, null, false);
            Assert.AreEqual(1, Waypoints.Length);
            Assert.IsFalse(Waypoints[0].eventWaypoint);
            Assert.IsNotNull(nav.PlayerRoute);
        }

        [Test]
        public void ANewPlayerRouteKeepsTheEventWaypoint()
        {
            nav.SetEventWaypoint(OnePoint(new Vector3(-5000, 0, 9000)), null, true);
            nav.SetRoute(OnePoint(new Vector3(1000, 0, 2000)), true);
            nav.SetRoute(null);
            Assert.AreEqual(1, Waypoints.Length);
            Assert.IsTrue(Waypoints[0].eventWaypoint);
        }
    }
}
