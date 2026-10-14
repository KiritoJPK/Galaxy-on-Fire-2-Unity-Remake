// StandingAndRouteTests.cs
// Standing (CLAUDE.md "NPCs and combat", Relations / Signatures; Reference/research/ship_combat.md 2.6-2.7,
// combat_equipment.md 3.3): two axes in -100..100, [0] Terran (+) / Vossk (-), [1] Nivelian (+) / Midorian (-), a new game
// at 30 / 0; applyDelict moves the race's axis away from it by n (x2 hardcore); enemy at <= -71 toward the race, friend at
// >= 71; pirates and the Void always enemies; a kill 5, a pirate kill +1 toward the system race (enemy-race table 1, 0, 3,
// 2) unless a signature is worn; a signature: its race 100, the rival -100, the others 70; invalidated = removed, delict
// 100. NPC vs NPC: pirates / Void / Specters against everyone, Terran-Vossk, Nivelian-Midorian (npc_traffic_ai.md).
// Route (Reference/research/npc_traffic_ai.md 5.8, Route.cs header): a waypoint is reached within 2000 units on every
// axis; the default patrol takes 2-4 of four corner boxes in front of the station without repeats, looping; the Void flies
// the square (+-40000, 0, +-40000).

using GoF2Remake.Data;
using GoF2Remake.Flight;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class StandingTests : SandboxTest
    {
        float savedDifficulty;

        [SetUp] public void KeepDifficulty() { savedDifficulty = Session.Difficulty; Session.Difficulty = Session.DifficultyNormal; }
        [TearDown] public void RestoreDifficulty() => Session.Difficulty = savedDifficulty;

        static void Wear(int signatureItem) => Session.Equipment.Add(new ItemStack(signatureItem, 1));

        [Test]
        public void NewGameStanding()
        {
            CollectionAssert.AreEqual(new[] { 30, 0 }, Session.Standing);
            Assert.AreEqual(30, Standing.Toward(0));
            Assert.AreEqual(-30, Standing.Toward(1));
            Assert.AreEqual(0, Standing.Toward(2));
            Assert.AreEqual(0, Standing.Toward(3));
            Assert.AreEqual(-1, Standing.SignatureRace);
        }

        [Test]
        public void DelictMovesAwayFromTheRace()
        {
            Standing.ApplyDelict(0, 5);   // Terran: s0 - 5
            Assert.AreEqual(25, Session.Standing[0]);
            Standing.ApplyDelict(1, 5);   // Vossk: s0 + 5
            Assert.AreEqual(30, Session.Standing[0]);
            Standing.ApplyDelict(2, 7);
            Assert.AreEqual(-7, Session.Standing[1]);
            Standing.ApplyDelict(3, 2);
            Assert.AreEqual(-5, Session.Standing[1]);
        }

        [Test]
        public void DelictDoublesOnExtreme()
        {
            Session.Difficulty = Session.DifficultyExtreme;
            Standing.ApplyDelict(0, 5);
            Assert.AreEqual(20, Session.Standing[0]);
            Session.Difficulty = Session.DifficultyHard;
            Standing.ApplyDelict(0, 5);
            Assert.AreEqual(15, Session.Standing[0], "Hard isn't hardcore");
        }

        [Test]
        public void AxesAreClamped()
        {
            Standing.ApplyDelict(1, 500);
            Assert.AreEqual(100, Session.Standing[0]);
            Standing.ApplyDelict(2, 500);
            Assert.AreEqual(-100, Session.Standing[1]);
        }

        [Test]
        public void DelictIgnoresOtherRaces()
        {
            Standing.ApplyDelict(Standing.Pirate, 50);
            Standing.ApplyDelict(-1, 50);
            CollectionAssert.AreEqual(new[] { 30, 0 }, Session.Standing);
        }

        [Test]
        public void EnemyBeyondSeventy()
        {
            Session.Standing[0] = -70;
            Assert.IsFalse(Standing.IsEnemy(0));
            Session.Standing[0] = -71;
            Assert.IsTrue(Standing.IsEnemy(0));
            Assert.IsTrue(Standing.IsFriend(1), "-71 toward Terran is 71 toward Vossk");
            Session.Standing[1] = 71;
            Assert.IsTrue(Standing.IsEnemy(3));
            Assert.IsTrue(Standing.IsFriend(2));
            Session.Standing[1] = 70;
            Assert.IsFalse(Standing.IsEnemy(3));
            Assert.IsFalse(Standing.IsFriend(2));
        }

        [Test]
        public void PiratesAndVoidAlwaysEnemies()
        {
            Session.Standing[0] = 100; Session.Standing[1] = 100;
            Assert.IsTrue(Standing.IsEnemy(Standing.Pirate));
            Assert.IsTrue(Standing.IsEnemy(Standing.Void));
            Assert.IsFalse(Standing.IsFriend(Standing.Pirate));
            Assert.IsFalse(Standing.IsEnemy(Standing.Specter), "Specters have no standing");
        }

        [Test]
        public void KillCostsFive()
        {
            Standing.ApplyKill(2, 0);
            Assert.AreEqual(-5, Session.Standing[1]);
            Standing.ApplyKill(0, 0);
            Assert.AreEqual(25, Session.Standing[0]);
        }

        [Test]
        public void PirateKillHelpsTheSystemRace()
        {
            Standing.ApplyKill(Standing.Pirate, 0);   // Terran system: delict 1 toward the Vossk -> +1 Terran
            Assert.AreEqual(31, Session.Standing[0]);
            // Midorian system: a delict toward the Nivelians (applyDelict: Nivelian -n) -> toward the Midorians, the negative side
            // of axis [1] (ship_combat.md 2.7's labels are swapped; its isEnemy rule makes Terran / Nivelian positive)
            Standing.ApplyKill(Standing.Pirate, 3);
            Assert.AreEqual(-1, Session.Standing[1]);
            Standing.ApplyKill(Standing.Pirate, Standing.Pirate);   // no race: nothing
            CollectionAssert.AreEqual(new[] { 31, -1 }, Session.Standing);
        }

        [Test]
        public void EnemyRaceTable()
        {
            Assert.AreEqual(1, Standing.EnemyRaceOf(0));
            Assert.AreEqual(0, Standing.EnemyRaceOf(1));
            Assert.AreEqual(3, Standing.EnemyRaceOf(2));
            Assert.AreEqual(2, Standing.EnemyRaceOf(3));
            Assert.AreEqual(Standing.Pirate, Standing.EnemyRaceOf(Standing.Void));
        }

        [Test]
        public void SignatureOverridesTheAxes()
        {
            Session.Standing[0] = 100;   // a Terran friend wearing the Vossk signature (190)
            Wear(190);
            Assert.AreEqual(1, Standing.SignatureRace);
            Assert.AreEqual(100, Standing.Toward(1));
            Assert.AreEqual(-100, Standing.Toward(0));
            Assert.AreEqual(70, Standing.Toward(2));
            Assert.AreEqual(70, Standing.Toward(3));
            Assert.IsTrue(Standing.IsFriend(1));
            Assert.IsTrue(Standing.IsEnemy(0));
            Assert.IsFalse(Standing.IsFriend(2), "70 is neutral");
            Assert.IsFalse(Standing.IsEnemy(2));
            Assert.AreEqual(100, Standing.RawToward(0), "the raw axes ignore it");
            Assert.AreEqual(-100, Standing.RawToward(1));
        }

        [Test]
        public void PirateKillWithSignatureChangesNothing()
        {
            Wear(191);
            Standing.ApplyKill(Standing.Pirate, 0);
            CollectionAssert.AreEqual(new[] { 30, 0 }, Session.Standing);
        }

        [Test]
        public void InvalidatingTheSignature()
        {
            Wear(192);   // Midorian
            Assert.IsTrue(Standing.InvalidateSignature());
            Assert.AreEqual(-1, Standing.SignatureRace);
            Assert.IsFalse(Session.Equipment.Exists(e => e.item == 192));
            Assert.AreEqual(100, Session.Standing[1], "delict 100 toward the Midorians");
            Assert.IsFalse(Standing.InvalidateSignature(), "nothing left to invalidate");
        }

        [Test]
        public void TowardWithOtherPlayersValues()
        {
            Assert.AreEqual(-50, Standing.TowardWith(0, -50, 10, -1));
            Assert.AreEqual(-10, Standing.TowardWith(3, -50, 10, -1));
            Assert.IsTrue(Standing.IsEnemyWith(0, -71, 0, -1));
            Assert.IsTrue(Standing.IsFriendWith(2, 0, 0, 2));
            Assert.IsTrue(Standing.IsEnemyWith(3, 0, 0, 2));
            Assert.IsTrue(Standing.IsEnemyWith(Standing.Void, 100, 100, -1));
        }

        [Test]
        public void NpcRaceHostility()
        {
            Assert.IsTrue(Standing.RacesHostile(0, 1));
            Assert.IsTrue(Standing.RacesHostile(3, 2));
            Assert.IsFalse(Standing.RacesHostile(0, 2));
            Assert.IsFalse(Standing.RacesHostile(1, 3));
            Assert.IsFalse(Standing.RacesHostile(1, 1));
            for (int r = 0; r <= 3; r++)
            {
                Assert.IsTrue(Standing.RacesHostile(Standing.Pirate, r));
                Assert.IsTrue(Standing.RacesHostile(r, Standing.Void));
                Assert.IsTrue(Standing.RacesHostile(Standing.Specter, r));
            }
            Assert.IsFalse(Standing.RacesHostile(Standing.Pirate, Standing.Pirate));
            Assert.IsTrue(Standing.RacesHostile(Standing.Pirate, Standing.Void));
        }

        [Test]
        public void RaceWords()
        {
            Assert.AreEqual(0, Standing.RaceWord("Terrans"));
            Assert.AreEqual(1, Standing.RaceWord("VOSSK"));
            Assert.AreEqual(2, Standing.RaceWord("nivelian"));
            Assert.AreEqual(3, Standing.RaceWord("midorians"));
            Assert.AreEqual(Standing.Pirate, Standing.RaceWord("pirates"));
            Assert.AreEqual(Standing.Void, Standing.RaceWord("void"));
            Assert.AreEqual(Standing.Specter, Standing.RaceWord("specter"));
        }
    }

    public class RouteTests
    {
        static Route Square(bool loop)
        {
            var r = new Route(loop);
            r.points.Add(new Vector3(0f, 0f, 10000f));
            r.points.Add(new Vector3(10000f, 0f, 10000f));
            return r;
        }

        [Test]
        public void ReachedWithin2000OnEveryAxis()
        {
            var r = Square(false);
            r.Update(new Vector3(1999f, -1999f, 8001f));
            Assert.AreEqual(1, r.index);
            r.Update(new Vector3(10000f, 2000f, 10000f));   // y exactly 2000 away: not yet
            Assert.AreEqual(1, r.index);
            r.Update(new Vector3(8500f, 1500f, 11999f));
            Assert.AreEqual(2, r.index);
        }

        [Test]
        public void FarOnOneAxisIsNotReached()
        {
            var r = Square(true);
            r.Update(new Vector3(0f, 0f, 30000f));
            Assert.AreEqual(0, r.index);
        }

        [Test]
        public void NonLoopingRouteEnds()
        {
            var r = Square(false);
            r.Update(r.points[0]);
            r.Update(r.points[1]);
            Assert.IsNull(r.Waypoint);
            r.Update(r.points[0]);
            Assert.IsNull(r.Waypoint, "stays finished");
        }

        [Test]
        public void LoopingRouteStartsAgain()
        {
            var r = Square(true);
            r.Update(r.points[0]);
            Assert.AreEqual(r.points[1], r.Waypoint);
            r.Update(r.points[1]);
            Assert.AreEqual(0, r.index);
            Assert.AreEqual(r.points[0], r.Waypoint);
        }

        [Test]
        public void CloneStartsFromTheFirstPoint()
        {
            var r = Square(true);
            r.Update(r.points[0]);
            var c = r.Clone();
            Assert.AreEqual(0, c.index);
            Assert.IsTrue(c.loop);
            CollectionAssert.AreEqual(r.points, c.points);
            c.points.Add(Vector3.zero);
            Assert.AreEqual(2, r.points.Count, "its own list");
        }

        [Test]
        public void VoidPatrolsTheSquare()
        {
            var r = Route.DefaultPatrol(Standing.Void);
            Assert.IsTrue(r.loop);
            CollectionAssert.AreEqual(new[]
            {
                new Vector3(-40000f, 0f, -40000f), new Vector3(40000f, 0f, -40000f),
                new Vector3(40000f, 0f, 40000f), new Vector3(-40000f, 0f, 40000f),
            }, r.points);
        }

        static int Corner(Vector3 p)
        {
            Assert.That(p.y, Is.InRange(-10000f, 0f));
            bool left = p.x >= -30000f && p.x <= -5000f, right = p.x >= 5000f && p.x <= 30000f;
            bool near = p.z >= 20000f && p.z <= 45000f, far = p.z >= 55000f && p.z <= 80000f;
            Assert.IsTrue((left || right) && (near || far), $"{p} is in none of the corner boxes");
            return left ? (near ? 0 : 3) : (near ? 1 : 2);
        }

        [Test]
        public void FighterPatrolTakesTwoToFourCorners()
        {
            var saved = Random.state;
            try
            {
                var counts = new System.Collections.Generic.HashSet<int>();
                for (int seed = 0; seed < 200; seed++)
                {
                    Random.InitState(seed);
                    var r = Route.DefaultPatrol(0);
                    Assert.IsTrue(r.loop);
                    Assert.That(r.points.Count, Is.InRange(2, 4));
                    counts.Add(r.points.Count);
                    var seen = new System.Collections.Generic.HashSet<int>();
                    foreach (var p in r.points) Assert.IsTrue(seen.Add(Corner(p)), "no corner twice");
                }
                CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, counts);
            }
            finally { Random.state = saved; }
        }
    }
}
