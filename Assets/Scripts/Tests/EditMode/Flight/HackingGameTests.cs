// HackingGameTests.cs
// The Supernova add-on's hacking minigame (CLAUDE.md "Supernova add-on", Hacking; HackingGame.cs header from
// HackingGame 0x179638). Expected behaviour from the decompiled description, independent of the code's helpers: a 2 x 3
// board (index = row * 3 + column); the left button turns tiles 0 1 / 3 4 clockwise, the right one 1 2 / 4 5
// (written out here as cycles: 3 -> 0 -> 1 -> 4 -> 3 and 4 -> 1 -> 2 -> 5 -> 4); the target of kind 1 is the pairs
// 0 0 1 1 2 2, kind 2 holds 0-3 and two more of 0-3, kind 3 0-4 and one more, kind 4 six different tiles; the scramble is
// never solvable in 'kind' moves (checked with this test's own breadth-first search); a turn animates 300 ms; solved, the
// tiles blink 1500 ms, then won. UnityEngine.Random is seeded and put back.

using System;
using System.Collections.Generic;
using GoF2Remake.Flight;
using NUnit.Framework;
using Random = UnityEngine.Random;

namespace GoF2Remake.Tests
{
    public class HackingGameTests
    {
        static HackingGame Make(int kind, int seed)
        {
            var saved = Random.state;
            Random.InitState(seed);
            try { return new HackingGame(kind, 3); }
            finally { Random.state = saved; }
        }

        // clockwise: what was at 'from' moves to 'to'
        static int[] Left(int[] s) => Cycle(s, 3, 0, 1, 4);
        static int[] Right(int[] s) => Cycle(s, 4, 1, 2, 5);

        static int[] Cycle(int[] s, int a, int b, int c, int d)
        {
            var r = (int[])s.Clone();
            r[b] = s[a]; r[c] = s[b]; r[d] = s[c]; r[a] = s[d];
            return r;
        }

        static string Key(int[] s) => string.Join(",", s);

        /// <summary>Fewest turns from 'from' to 'to' (-1 = unreachable), and the moves (false left, true right).</summary>
        static int Distance(int[] from, int[] to, out List<bool> path)
        {
            var prev = new Dictionary<string, (string, bool)>();
            var queue = new Queue<int[]>();
            queue.Enqueue(from);
            prev[Key(from)] = (null, false);
            path = new List<bool>();
            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                string k = Key(s);
                if (k == Key(to))
                {
                    for (string at = k; prev[at].Item1 != null; at = prev[at].Item1) path.Insert(0, prev[at].Item2);
                    return path.Count;
                }
                foreach (bool right in new[] { false, true })
                {
                    var n = right ? Right(s) : Left(s);
                    string nk = Key(n);
                    if (prev.ContainsKey(nk)) continue;
                    prev[nk] = (k, right);
                    queue.Enqueue(n);
                }
            }
            return -1;
        }

        static int[] Sorted(int[] a) { var s = (int[])a.Clone(); Array.Sort(s); return s; }

        static void Settle(HackingGame g) { g.Update(301f); }

        [Test]
        public void TargetsByKind()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 2, 2 }, Sorted(Make(1, seed).target));
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Sorted(Make(4, seed).target));
                var k2 = Make(2, seed).target;
                for (int t = 0; t < 4; t++) CollectionAssert.Contains(k2, t);
                foreach (int t in k2) Assert.That(t, Is.InRange(0, 3));
                var k3 = Make(3, seed).target;
                for (int t = 0; t < 5; t++) CollectionAssert.Contains(k3, t);
                foreach (int t in k3) Assert.That(t, Is.InRange(0, 4));
            }
        }

        [Test]
        public void ScrambleIsNeverSolvableInKindMoves()
        {
            for (int kind = 1; kind <= 4; kind++)
                for (int seed = 0; seed < 40; seed++)
                {
                    var g = Make(kind, seed);
                    CollectionAssert.AreEqual(Sorted(g.target), Sorted(g.board), "the board holds the target's tiles");
                    CollectionAssert.AreEqual(g.board, g.shown);
                    int d = Distance((int[])g.board.Clone(), g.target, out _);
                    Assert.Greater(d, kind, $"kind {kind}, seed {seed}: reachable, but not within {kind} moves");
                    Assert.IsFalse(g.Solved);
                }
        }

        [Test]
        public void SameSeedSameBoard()
        {
            var a = Make(4, 123);
            var b = Make(4, 123);
            CollectionAssert.AreEqual(a.target, b.target);
            CollectionAssert.AreEqual(a.board, b.board);
            Assert.AreEqual(3, a.DockIndex);
            Assert.AreEqual(4, a.Kind);
        }

        [Test]
        public void LeftButtonTurnsTheLeftBlockClockwise()
        {
            var g = Make(4, 1);
            var before = (int[])g.board.Clone();
            g.TurnLeft();
            CollectionAssert.AreEqual(Left(before), g.board);
            CollectionAssert.AreEqual(before, g.shown, "shown lags by the turn animation");
            Assert.AreEqual(-1, g.Turning);
            g.Update(150f);
            Assert.AreEqual(0.5f, g.TurnProgress, 1e-5f);
            g.Update(150f);
            Assert.AreEqual(-1, g.Turning, "300 ms: still turning (past it ends)");
            g.Update(1f);
            Assert.AreEqual(0, g.Turning);
            CollectionAssert.AreEqual(g.board, g.shown);
        }

        [Test]
        public void RightButtonTurnsTheRightBlockClockwise()
        {
            var g = Make(4, 2);
            var before = (int[])g.board.Clone();
            g.TurnRight();
            CollectionAssert.AreEqual(Right(before), g.board);
            Assert.AreEqual(1, g.Turning);
        }

        [Test]
        public void NoTurnWhileTurning()
        {
            var g = Make(4, 3);
            var before = (int[])g.board.Clone();
            int turned = 0;
            g.Turned += () => turned++;
            g.TurnLeft();
            g.TurnRight();
            g.TurnLeft();
            CollectionAssert.AreEqual(Left(before), g.board);
            Assert.AreEqual(1, turned);
        }

        [Test]
        public void FourTurnsAreAFullCircle()
        {
            var g = Make(4, 4);
            var before = (int[])g.board.Clone();
            for (int i = 0; i < 4; i++) { g.TurnRight(); Settle(g); }
            CollectionAssert.AreEqual(before, g.board);
        }

        [Test]
        public void SolvingWinsAfterTheBlink()
        {
            var g = Make(4, 5);
            int d = Distance((int[])g.board.Clone(), g.target, out var path);
            Assert.Greater(d, 0);
            int solvedNow = 0;
            g.SolvedNow += () => solvedNow++;
            foreach (bool right in path)
            {
                Assert.IsFalse(g.Solved);
                if (right) g.TurnRight(); else g.TurnLeft();
                Assert.IsTrue(g.Update(301f));
            }
            Assert.IsTrue(g.Solved);
            Assert.IsFalse(g.Won, "the tiles blink first");
            Assert.AreEqual(1, solvedNow);
            Assert.IsTrue(g.Update(1000f));
            Assert.IsFalse(g.Won);
            Assert.IsFalse(g.Update(600f), "past 1500 ms: over");
            Assert.IsTrue(g.Won);
            Assert.AreEqual(1, solvedNow);
        }

        [Test]
        public void NoTurnsOnceSolved()
        {
            var g = Make(1, 6);
            Distance((int[])g.board.Clone(), g.target, out var path);
            foreach (bool right in path) { if (right) g.TurnRight(); else g.TurnLeft(); Settle(g); }
            Assume.That(g.Solved);
            var solved = (int[])g.board.Clone();
            g.TurnLeft();
            CollectionAssert.AreEqual(solved, g.board);
            Assert.AreEqual(0, g.Turning);
        }

        [Test]
        public void InitScramblesAgain()
        {
            var saved = Random.state;
            Random.InitState(77);
            try
            {
                var g = new HackingGame(4, 0);
                var first = Key(g.board) + "|" + Key(g.target);
                bool changed = false;
                for (int i = 0; i < 5 && !changed; i++) { g.Init(); changed = Key(g.board) + "|" + Key(g.target) != first; }
                Assert.IsTrue(changed);
                Assert.Greater(Distance((int[])g.board.Clone(), g.target, out _), 4);
            }
            finally { Random.state = saved; }
        }
    }
}
