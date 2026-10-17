// NpcTargetRules.cs
// The remake's NPC fighter target pick (NpcShip.UpdateTargetingLocal; CLAUDE.md "NPCs and combat", AI), plain C# so it can
// be tested. Option Settings.SmarterNpcTargeting (a session rule), on by default; off = the original.
//   original   PlayerFighter::update: the enemy list has the player first, then the other races' ships; a fighter takes the
//              first one inside its +-50 000-unit box, so every hostile fighter in the orbit went for the player and none
//              for the local patrols beside it (players' report, 2026-10-17: a raider group arriving shredded the player).
//   remake     among the candidates in the box (the player and its sentry guns while the ship is hostile to the player, the
//              ships of a race hostile to its own) the nearest one, the player's distance counted x PlayerBias, so the
//              player stays a little likelier; a candidate that already has AttackerCap attackers is passed over while
//              another one is free, so a raider group splits up over the player and the patrols.

namespace GoF2Remake.Flight
{
    public static class NpcTargetRules
    {
        /// <summary>The player's distance counts this much: within 1 / 0.7 of the nearest ship's distance the player wins.</summary>
        public const float PlayerBias = 0.7f;

        public struct Candidate
        {
            /// <summary>Squared distance to the candidate (its hull's nearest point).</summary>
            public float sqrDistance;
            /// <summary>The player or one of its sentry guns (PlayerBias).</summary>
            public bool player;
            /// <summary>How many other ships attack it now.</summary>
            public int attackers;
        }

        /// <summary>How many fighters go for one target before the next ones look elsewhere: 2 + the difficulty rounded up
        /// (Session.Difficulty 0 / 0.5 / 1 / 1.5: Easy 2, Normal 3, Hard 3, Extreme 4).</summary>
        public static int AttackerCap(float difficulty) => 2 + (int)System.Math.Ceiling(difficulty - 1e-4f);

        /// <summary>The candidate's score, lower wins: its squared distance, the player's x PlayerBias².</summary>
        public static float Score(Candidate c) => c.player ? c.sqrDistance * (PlayerBias * PlayerBias) : c.sqrDistance;

        /// <summary>The pick: the best score among the candidates with fewer than 'cap' attackers, else (all taken) the best
        /// score of all; -1 with no candidates.</summary>
        public static int Pick(System.Collections.Generic.IReadOnlyList<Candidate> candidates, int cap)
        {
            int best = -1, bestFree = -1;
            float score = float.MaxValue, scoreFree = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float s = Score(candidates[i]);
                if (s < score) { score = s; best = i; }
                if (candidates[i].attackers < cap && s < scoreFree) { scoreFree = s; bestFree = i; }
            }
            return bestFree >= 0 ? bestFree : best;
        }
    }
}
