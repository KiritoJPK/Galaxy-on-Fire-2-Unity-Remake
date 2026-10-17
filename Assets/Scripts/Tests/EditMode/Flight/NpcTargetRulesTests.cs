// NpcTargetRulesTests.cs
// The remake's NPC fighter target pick (CLAUDE.md "NPCs and combat", AI; NpcTargetRules, NpcShip.UpdateSmartTargeting,
// option Settings.SmarterNpcTargeting): the nearest hostile target in the box, the player's distance x0.7, and at most
// 2 + difficulty (rounded up) attackers on one target while another one is free. Players' report 2026-10-17: a raider group
// arriving went for the player all together and ignored the patrols beside it.

using System.Collections.Generic;
using GoF2Remake.Flight;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class NpcTargetRulesTests
    {
        static NpcTargetRules.Candidate C(float distance, bool player = false, int attackers = 0) =>
            new NpcTargetRules.Candidate { sqrDistance = distance * distance, player = player, attackers = attackers };

        [Test]
        public void NoCandidatesNoPick()
        {
            Assert.AreEqual(-1, NpcTargetRules.Pick(new List<NpcTargetRules.Candidate>(), 3));
        }

        [Test]
        public void TheNearestTargetWins()
        {
            var list = new List<NpcTargetRules.Candidate> { C(20000f, player: true), C(9000f), C(5000f) };
            Assert.AreEqual(2, NpcTargetRules.Pick(list, 3), "a patrol next to the raider, not the player 20 km off");
        }

        [Test]
        public void ThePlayerCountsAtSevenTenthsOfTheDistance()
        {
            // The player 13 km off counts 9.1 km: nearer than a patrol at 10 km, farther than one at 9 km.
            Assert.AreEqual(0, NpcTargetRules.Pick(new List<NpcTargetRules.Candidate> { C(13000f, true), C(10000f) }, 3));
            Assert.AreEqual(1, NpcTargetRules.Pick(new List<NpcTargetRules.Candidate> { C(13000f, true), C(9000f) }, 3));
        }

        [Test]
        public void AFullTargetIsPassedOverWhileAnotherIsFree()
        {
            var list = new List<NpcTargetRules.Candidate> { C(1000f, true, attackers: 3), C(30000f) };
            Assert.AreEqual(1, NpcTargetRules.Pick(list, 3), "the next-nearest in reach");
            list[0] = C(1000f, true, attackers: 2);
            Assert.AreEqual(0, NpcTargetRules.Pick(list, 3), "below the cap the nearest");
        }

        [Test]
        public void EveryTargetFullTheNearestAnyway()
        {
            var list = new List<NpcTargetRules.Candidate> { C(8000f, attackers: 4), C(2000f, true, attackers: 3) };
            Assert.AreEqual(1, NpcTargetRules.Pick(list, 3));
        }

        [Test]
        public void TheCapGrowsWithTheDifficulty()
        {
            Assert.AreEqual(2, NpcTargetRules.AttackerCap(0f), "Easy");
            Assert.AreEqual(3, NpcTargetRules.AttackerCap(0.5f), "Normal");
            Assert.AreEqual(3, NpcTargetRules.AttackerCap(1f), "Hard");
            Assert.AreEqual(4, NpcTargetRules.AttackerCap(1.5f), "Extreme");
        }

        /// <summary>The report's case: 7 pirates arrive 2-8 km from the player with 3 patrols between them; one after another
        /// they pick (each pick counted for the next), so on Normal 3 go for the player and the rest for the patrols.</summary>
        [Test]
        public void ARaiderGroupSplitsOverThePlayerAndThePatrols()
        {
            float[] player = { 3000f, 3500f, 2000f, 4000f, 5000f, 6000f, 8000f };
            float[][] patrols =
            {
                new[] { 6000f, 9000f, 12000f }, new[] { 5000f, 9000f, 11000f }, new[] { 7000f, 8000f, 12000f },
                new[] { 6000f, 9500f, 10000f }, new[] { 5500f, 9000f, 11000f }, new[] { 6500f, 8000f, 9000f },
                new[] { 7000f, 9000f, 10000f },
            };
            int cap = NpcTargetRules.AttackerCap(0.5f);
            var attackers = new int[4];
            for (int r = 0; r < player.Length; r++)
            {
                var list = new List<NpcTargetRules.Candidate> { C(player[r], true, attackers[0]) };
                for (int p = 0; p < 3; p++) list.Add(C(patrols[r][p], false, attackers[p + 1]));
                attackers[NpcTargetRules.Pick(list, cap)]++;
            }
            Assert.AreEqual(3, attackers[0], "three on the player");
            Assert.AreEqual(4, attackers[1] + attackers[2] + attackers[3], "the rest on the patrols");
            foreach (int a in attackers) Assert.LessOrEqual(a, cap);
        }

        /// <summary>A local patrol isn't hostile to the player: only the raiders are its candidates (the caller leaves the
        /// player out), and the nearest of them wins.</summary>
        [Test]
        public void WithoutThePlayerTheNearestRaider()
        {
            var list = new List<NpcTargetRules.Candidate> { C(12000f), C(4000f, attackers: 1), C(7000f) };
            Assert.AreEqual(1, NpcTargetRules.Pick(list, 3));
        }
    }
}
