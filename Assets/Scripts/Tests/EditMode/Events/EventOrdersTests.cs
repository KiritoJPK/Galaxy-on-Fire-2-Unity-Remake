// EventOrdersTests.cs
// Feature: CLAUDE.md "Multiplayer" > Events, Ship orders (EventShipOrders, /npc) and Cutscenes (EventCutscene, /camera):
//   <ship name> goto <place> [speed u/ms] [radius units] [fight] [then hold | resume | vanish | jump]
//   <ship name> route <place> / <place> / ... [loop] ...;  follow / escort (= follow ... fight) <player | ship "name">
//   [offset right up forward]; attack <player | ship>; flee [seconds s]; dock; hold; resume; jump; speed <u/ms>
//   places: "x y z", "station [x y z]", "player [right up forward]", "ship <name> [x y z]" ("quotes" keep a name together);
//   a camera shot: <place> [to <place>] [over s] [look <place>] [follow] [fov deg 5..150] [shake 0..1].
// Expected values: the documented grammar (CLAUDE.md, the files' headers); orders and shots written by hand.

using GoF2Remake.Events;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;
using Kind = GoF2Remake.Events.EventCutscene.PlaceKind;

namespace GoF2Remake.Tests
{
    public class EventOrdersTests
    {
        static EventShipOrders.Parsed Order(string text)
        {
            Assert.IsTrue(EventShipOrders.TryParse(text, out var p, out string error), text + ": " + error);
            Assert.IsNull(error);
            return p;
        }

        static void NoOrder(string text)
        {
            Assert.IsFalse(EventShipOrders.TryParse(text, out _, out string error), text);
            Assert.IsNotNull(error, text + ": a reason");
        }

        // ---- words ----

        [Test]
        public void WordsSplitOnSpacesAndKeepQuotesTogether()
        {
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, EventCutscene.Words("  a  b\tc "));
            CollectionAssert.AreEqual(new[] { "ship", "Pirate Boss", "0" }, EventCutscene.Words("ship \"Pirate Boss\" 0"));
            CollectionAssert.AreEqual(new[] { "x", "open ended" }, EventCutscene.Words("x \"open ended"));
            Assert.IsEmpty(EventCutscene.Words(""));
            Assert.IsEmpty(EventCutscene.Words(null));
        }

        [Test]
        public void NumbersAreInvariantAndBounded()
        {
            var w = EventCutscene.Words("1.5 -20 1e9 NaN abc");
            Assert.IsTrue(EventCutscene.Number(w, 0, out float a)); Assert.AreEqual(1.5f, a);
            Assert.IsTrue(EventCutscene.Number(w, 1, out float b)); Assert.AreEqual(-20f, b);
            Assert.IsFalse(EventCutscene.Number(w, 2, out _), "beyond 1e8");
            Assert.IsFalse(EventCutscene.Number(w, 3, out _), "NaN");
            Assert.IsFalse(EventCutscene.Number(w, 4, out _));
            Assert.IsFalse(EventCutscene.Number(w, 5, out _), "past the end");
        }

        // ---- places ----

        static EventCutscene.Place Place(string text, out int used)
        {
            var w = EventCutscene.Words(text);
            int i = 0;
            Assert.IsTrue(EventCutscene.ParsePlace(w, ref i, out var p, out string error), text + ": " + error);
            used = i;
            return p;
        }

        [Test]
        public void APointIsThreeNumbers()
        {
            var p = Place("100 200 -300", out int used);
            Assert.AreEqual(Kind.Point, p.kind);
            Assert.AreEqual(new Vector3(100, 200, -300), p.offset);
            Assert.AreEqual(3, used);
        }

        [Test]
        public void PlayerAndStationTakeAnOptionalOffset()
        {
            var a = Place("player", out int u1);
            Assert.AreEqual(Kind.Player, a.kind); Assert.AreEqual(Vector3.zero, a.offset); Assert.AreEqual(1, u1);
            var b = Place("player 0 600 -2500", out int u2);
            Assert.AreEqual(Kind.Player, b.kind); Assert.AreEqual(new Vector3(0, 600, -2500), b.offset); Assert.AreEqual(4, u2);
            var c = Place("STATION 0 0 20000 over 3", out int u3);
            Assert.AreEqual(Kind.Station, c.kind); Assert.AreEqual(new Vector3(0, 0, 20000), c.offset); Assert.AreEqual(4, u3);
        }

        [Test]
        public void AShipPlaceNeedsItsName()
        {
            var p = Place("ship \"Pirate Boss\" 0 500 0", out int used);
            Assert.AreEqual(Kind.Ship, p.kind);
            Assert.AreEqual("Pirate Boss", p.name);
            Assert.AreEqual(new Vector3(0, 500, 0), p.offset);
            Assert.AreEqual(5, used);
            foreach (var bad in new[] { "ship", "ship to player", "", "nowhere", "1 2", "player 1 2" })
            {
                var w = EventCutscene.Words(bad);
                int i = 0;
                Assert.IsFalse(EventCutscene.ParsePlace(w, ref i, out _, out string error), bad);
                Assert.IsNotNull(error, bad);
            }
        }

        // ---- camera shots ----

        [Test]
        public void AShotFromAPlaceAlone()
        {
            Assert.IsTrue(EventCutscene.TryParseShot("player 0 600 -2500", out var s, out string error), error);
            Assert.AreEqual(Kind.Player, s.from.kind);
            Assert.AreEqual(new Vector3(0, 600, -2500), s.from.offset);
            Assert.IsFalse(s.to.HasValue);
            Assert.AreEqual(Kind.Player, s.look.kind, "it looks at the player by default");
            Assert.AreEqual(0f, s.seconds);
            Assert.AreEqual(0f, s.fov);
            Assert.AreEqual(0f, s.shake);
            Assert.IsFalse(s.follow);
        }

        [Test]
        public void AShotWithEveryOption()
        {
            Assert.IsTrue(EventCutscene.TryParseShot("station 0 0 20000 to player 0 0 -3000 over 4 look ship \"Pirate Boss\" follow fov 60 shake 0.5",
                                                    out var s, out string error), error);
            Assert.AreEqual(Kind.Station, s.from.kind);
            Assert.IsTrue(s.to.HasValue);
            Assert.AreEqual(Kind.Player, s.to.Value.kind);
            Assert.AreEqual(new Vector3(0, 0, -3000), s.to.Value.offset);
            Assert.AreEqual(4f, s.seconds);
            Assert.AreEqual(Kind.Ship, s.look.kind);
            Assert.AreEqual("Pirate Boss", s.look.name);
            Assert.IsTrue(s.follow);
            Assert.AreEqual(60f, s.fov);
            Assert.AreEqual(0.5f, s.shake);
        }

        [Test]
        public void AShotsLimits()
        {
            Assert.IsTrue(EventCutscene.TryParseShot("player fov 5", out _, out _));
            Assert.IsTrue(EventCutscene.TryParseShot("player fov 150", out _, out _));
            Assert.IsTrue(EventCutscene.TryParseShot("player shake 0", out _, out _));
            Assert.IsTrue(EventCutscene.TryParseShot("player shake 1", out _, out _));
            Assert.IsTrue(EventCutscene.TryParseShot("player over 0", out _, out _));
            foreach (var bad in new[] { "player fov 4", "player fov 151", "player shake 1.5", "player shake -0.1", "player over -1",
                                        "player over", "player banana", "", "player to", "player look nowhere" })
            {
                Assert.IsFalse(EventCutscene.TryParseShot(bad, out _, out string error), bad);
                Assert.IsNotNull(error, bad);
            }
        }

        // ---- ship orders ----

        [Test]
        public void GotoWithItsOptions()
        {
            var p = Order("Pirate Boss goto 100 200 300 speed 3 radius 800 fight then hold");
            Assert.AreEqual("Pirate Boss", p.ship);
            Assert.AreEqual("goto", p.verb);
            Assert.AreEqual(1, p.points.Count);
            Assert.AreEqual(Kind.Point, p.points[0].kind);
            Assert.AreEqual(new Vector3(100, 200, 300), p.points[0].offset);
            Assert.AreEqual(3f, p.speed);
            Assert.AreEqual(800f, p.radius);
            Assert.IsTrue(p.fight);
            Assert.AreEqual(NpcOrder.Then.Hold, p.then);
        }

        [Test]
        public void TheDefaults()
        {
            var p = Order("Drone goto station");
            Assert.AreEqual(-1f, p.speed, "the ship's own speed");
            Assert.AreEqual(-1f, p.radius);
            Assert.IsFalse(p.fight);
            Assert.IsFalse(p.loop);
            Assert.AreEqual(NpcOrder.Then.Resume, p.then);
            Assert.AreEqual(6f, p.seconds);
        }

        [Test]
        public void EveryThen()
        {
            Assert.AreEqual(NpcOrder.Then.Hold, Order("A goto 0 0 0 then hold").then);
            Assert.AreEqual(NpcOrder.Then.Resume, Order("A goto 0 0 0 then resume").then);
            Assert.AreEqual(NpcOrder.Then.Vanish, Order("A goto 0 0 0 then vanish").then);
            Assert.AreEqual(NpcOrder.Then.Jump, Order("A goto 0 0 0 then jump").then);
            NoOrder("A goto 0 0 0 then explode");
            NoOrder("A goto 0 0 0 then");
        }

        [Test]
        public void AQuotedShipName()
        {
            var p = Order("\"Pirate Boss\" goto station 0 0 20000");
            Assert.AreEqual("Pirate Boss", p.ship);
            Assert.AreEqual(Kind.Station, p.points[0].kind);
            Assert.AreEqual(new Vector3(0, 0, 20000), p.points[0].offset);
        }

        [Test]
        public void ARouteOfPlaces()
        {
            var p = Order("Patrol route 0 0 0 / player 0 0 5000 / station loop radius 500");
            Assert.AreEqual("route", p.verb);
            Assert.AreEqual(3, p.points.Count);
            Assert.AreEqual(Kind.Point, p.points[0].kind);
            Assert.AreEqual(Kind.Player, p.points[1].kind);
            Assert.AreEqual(new Vector3(0, 0, 5000), p.points[1].offset);
            Assert.AreEqual(Kind.Station, p.points[2].kind);
            Assert.IsTrue(p.loop);
            Assert.AreEqual(500f, p.radius);
            NoOrder("Patrol route 0 0 0 /");
        }

        [Test]
        public void FollowAndEscort()
        {
            var f = Order("Wing follow player offset 100 0 -200 speed 2");
            Assert.AreEqual("follow", f.verb);
            Assert.AreEqual(Kind.Player, f.other.kind);
            Assert.IsTrue(f.hasOffset);
            Assert.AreEqual(new Vector3(100, 0, -200), f.offset);
            Assert.AreEqual(2f, f.speed);
            Assert.IsFalse(f.fight);

            var inline = Order("Wing follow player 100 0 -200");
            Assert.IsTrue(inline.hasOffset, "the place's own offset is the formation offset");
            Assert.AreEqual(new Vector3(100, 0, -200), inline.offset);

            var e = Order("Wing escort ship \"Cargo Hauler\"");
            Assert.AreEqual("escort", e.verb);
            Assert.IsTrue(e.fight, "escort = follow ... fight");
            Assert.AreEqual(Kind.Ship, e.other.kind);
            Assert.AreEqual("Cargo Hauler", e.other.name);
            Assert.IsFalse(e.hasOffset);
        }

        [Test]
        public void AttackNeedsAPlayerOrAShip()
        {
            Assert.AreEqual(Kind.Player, Order("Raider attack player").other.kind);
            Assert.AreEqual("Boss", Order("Raider attack ship Boss").other.name);
            NoOrder("Raider attack 1 2 3");
            NoOrder("Raider attack station");
            NoOrder("Raider follow 0 0 0");
        }

        [Test]
        public void TheSimpleVerbs()
        {
            foreach (var verb in new[] { "dock", "hold", "resume", "jump" })
            {
                var p = Order("Drone " + verb);
                Assert.AreEqual(verb, p.verb);
                Assert.IsEmpty(p.points);
            }
            Assert.AreEqual(10f, Order("Drone flee seconds 10").seconds);
            Assert.AreEqual("goto", Order("Drone GOTO 0 0 0").verb, "verbs in any case");
        }

        [Test]
        public void SpeedIsAPositiveUnitsPerMs()
        {
            Assert.AreEqual(3f, Order("Drone speed 3").speed);
            Assert.AreEqual(50f, Order("Drone speed 50").speed);
            NoOrder("Drone speed 0");
            NoOrder("Drone speed -2");
            NoOrder("Drone speed 51");
            NoOrder("Drone speed");
            NoOrder("Drone goto 0 0 0 speed fast");
        }

        [Test]
        public void ShipNamesOfSeveralWords()
        {
            Assert.AreEqual("Big Bad Pirate", Order("Big Bad Pirate hold").ship);
        }

        [Test]
        public void BadOrders()
        {
            NoOrder("");
            NoOrder("goto 0 0 0");                 // no ship
            NoOrder("Drone");                      // no verb
            NoOrder("Drone goto");                 // no place
            NoOrder("Drone goto 1 2");             // two numbers
            NoOrder("Drone hold banana");
            NoOrder("Drone goto 0 0 0 radius -5");
            NoOrder("Drone flee seconds -1");
            NoOrder("Drone follow player offset 1 2");
        }
    }
}
