// HackingGame.cs
// The hacking minigame of the Supernova add-on (HackingGame 0x179638, decoded here; not in the research files): docking at
// an object with docking type 3 (PlayerEgo::update 0xa8ed0: the secure containers, the Vossk battleships, Valkyrie, the
// wrecks with hidden blueprints) starts HackingGame(0, 4, -1, -1, dockIndex), (0, 1, ...) at campaign 91.
//   board        six code tiles in two rows of three (index = row * 3 + column); the target pattern is shown under it
//   moves        two buttons turn a 2 x 2 block clockwise: the left one tiles 0, 1, 4, 3 (rotateLeftCW), the right one
//                1, 2, 5, 4 (rotateRightCW); a turn animates 300 ms and ignores input meanwhile (isRotating); sound 0x8e2
//   target       kind 1: pairs (0 0 1 1 2 2); kind 2: 0-3 and two random of 0-3; kind 3: 0-4 and one random of 0-4; else
//                six different tiles (kind 4, the normal game); shuffled by 40 random swaps (reInit 0x179720)
//   scramble     from the target: 2 * kind rounds, each 1-2 turns, left and right alternately; if the result can be solved in
//                'kind' moves (solvableInNSteps, at most 3 turns of one button in a row) it is scrambled further
//   won          the board equals the target: sound 0x8e1 once, the tiles blink for 1500 ms (update), then gameWon()
// Plain C#; HackingView draws it and forwards the buttons.

using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GoF2Remake.Flight
{
    public class HackingGame
    {
        public const float TurnMs = 300f, CelebrateMs = 1500f;

        public readonly int[] target = new int[6];
        /// <summary>The logical board (turned at once); Shown lags it by the 300 ms turn animation.</summary>
        public readonly int[] board = new int[6];
        public readonly int[] shown = new int[6];
        public int Kind { get; }
        /// <summary>The docking target's index (getDockingIndex), for the level scripts.</summary>
        public int DockIndex { get; }
        /// <summary>A turn is animating: -1 left block, +1 right block, 0 none; TurnProgress 0..1.</summary>
        public int Turning { get; private set; }
        public float TurnProgress => Mathf.Clamp01(turnMs / TurnMs);
        public bool Solved => Equal(shown, target);
        /// <summary>HackingGame::gameWon: solved and the 1500 ms blink over.</summary>
        public bool Won => solvedMs > CelebrateMs && Solved;
        public float SolvedMs => solvedMs;

        public event Action Turned, SolvedNow;

        float turnMs, solvedMs;

        public HackingGame(int kind, int dockIndex)
        {
            Kind = kind;
            DockIndex = dockIndex;
            Init();
        }

        /// <summary>HackingGame::reInit.</summary>
        public void Init()
        {
            Turning = 0;
            turnMs = solvedMs = 0f;
            switch (Kind)
            {
                case 1: for (int i = 0; i < 6; i++) target[i] = i / 2; break;
                case 2: for (int i = 0; i < 4; i++) target[i] = i; target[4] = Random.Range(0, 4); target[5] = Random.Range(0, 4); break;
                case 3: for (int i = 0; i < 5; i++) target[i] = i; target[5] = Random.Range(0, 5); break;
                default: for (int i = 0; i < 6; i++) target[i] = i; break;
            }
            for (int n = 0; n < 40; n++)
            {
                int a = Random.Range(0, 6), b = Random.Range(0, 6);
                (target[a], target[b]) = (target[b], target[a]);
            }
            Array.Copy(target, board, 6);
            if (Kind > 0)
            {
                // Guard: a kind whose target has only one tile type repeated can't be scrambled (never the case here).
                int guard = 0;
                for (int round = 0; round < Kind * 2; round++)
                {
                    int turns = Random.Range(0, 2) + 1;
                    for (int t = 0; t < turns; t++) { if ((round & 1) == 0) RotateRight(board); else RotateLeft(board); }
                    if (round == Kind * 2 - 1 && SolvableIn(Kind, 0, 0, 0, board) && ++guard < 100) round = 0;   // then round 1 (as the original)
                }
            }
            Array.Copy(board, shown, 6);
        }

        /// <summary>PlayerEgo::hackingRotateLCW: not while turning or won.</summary>
        public void TurnLeft() => Turn(-1);
        public void TurnRight() => Turn(1);

        void Turn(int side)
        {
            if (Turning != 0 || Solved) return;
            if (side < 0) RotateLeft(board); else RotateRight(board);
            Turning = side;
            turnMs = 0f;
            Turned?.Invoke();
        }

        /// <summary>HackingGame::update(dt): the turn animation, then the win blink. False once won (the original's 0).</summary>
        public bool Update(float dtMs)
        {
            if (Turning != 0)
            {
                turnMs += dtMs;
                if (turnMs > TurnMs) { Turning = 0; turnMs = 0f; Array.Copy(board, shown, 6); }
            }
            if (!Solved) return true;
            if (solvedMs == 0f) SolvedNow?.Invoke();
            solvedMs += Mathf.Max(dtMs, 0.001f);
            return solvedMs <= CelebrateMs;
        }

        // rotateLeftCW(int*) 0x179ab6: tiles 0 1 / 3 4 clockwise.
        static void RotateLeft(int[] s)
        {
            int a = s[3];
            s[3] = s[4]; s[4] = s[1];
            int b = s[0];
            s[0] = a; s[1] = b;
        }

        // rotateRightCW(int*) 0x179ad4: tiles 1 2 / 4 5 clockwise.
        static void RotateRight(int[] s)
        {
            int a = s[4];
            s[4] = s[5]; s[5] = s[2];
            int b = s[1];
            s[1] = a; s[2] = b;
        }

        /// <summary>solvableInNSteps 0x1799a4: reachable within n more moves, at most 3 turns of one button in a row.</summary>
        bool SolvableIn(int n, int depth, int lefts, int rights, int[] state)
        {
            if (Equal(state, target)) return true;
            if (n <= depth) return false;
            if (lefts < 3)
            {
                var s = (int[])state.Clone();
                RotateLeft(s);
                if (SolvableIn(n, depth + 1, lefts + 1, 0, s)) return true;
            }
            if (rights < 3)
            {
                var s = (int[])state.Clone();
                RotateRight(s);
                if (SolvableIn(n, depth + 1, 0, rights + 1, s)) return true;
            }
            return false;
        }

        static bool Equal(int[] a, int[] b)
        {
            for (int i = 0; i < 6; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
