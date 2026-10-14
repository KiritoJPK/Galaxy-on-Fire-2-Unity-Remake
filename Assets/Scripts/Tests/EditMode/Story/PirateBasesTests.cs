// PirateBasesTests.cs
// Feature: CLAUDE.md "NPCs and combat", Capital ships and pirate bases (PirateBases: stations 1 / 33 / 47 / 86, active
// until their outpost is destroyed, the reward 20 000 at the next docking).
// Expected values: Reference/research/npc_combat_specials.md §3 (DAT_00251f90 the base stations, all in Nivelian systems:
// 1 Mahasa in 0 Suteo, 33 in 2 Behén, 47 in 9 Weymire, 86 in 17 Nesla; DAT_00253724 the outposts' table positions;
// DAT_002543e0 the crates: 5 AMR Oppressor (45), Thermic o5 (28), Sh'koom (4), Sol EMP Mk II (17)).

using GoF2Remake.Data;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class PirateBasesTests : StoryAreaTestBase
    {
        [Test]
        public void TheFourBases()
        {
            CollectionAssert.AreEqual(new[] { 1, 33, 47, 86 }, PirateBases.Stations);
            Assert.AreEqual(20000, PirateBases.Reward);
            foreach (int st in PirateBases.Stations)
            {
                int sys = Db.Stations.Find(s => s.index == st).system;
                Assert.AreEqual(2, Db.Systems.Find(s => s.index == sys).raceId, "station " + st + " in a Nivelian system");
            }
            Assert.AreEqual(0, Db.Stations.Find(s => s.index == 1).system);
            Assert.AreEqual(17, Db.Stations.Find(s => s.index == 86).system);
        }

        [Test]
        public void OutpostPositionsAndLoot()
        {
            Assert.AreEqual(new Vector3(-200000, -100000, 50000), PirateBases.OutpostPositions[0]);
            Assert.AreEqual(new Vector3(50000, 200000, -100000), PirateBases.OutpostPositions[1]);
            CollectionAssert.AreEqual(new[] { (45, 5), (28, 1), (4, 1), (17, 1) }, PirateBases.Loot);
        }

        [Test]
        public void ActiveFromANewGame()
        {
            foreach (int st in PirateBases.Stations) Assert.IsTrue(PirateBases.StationHasBase(st));
            Assert.IsFalse(PirateBases.StationHasBase(2));
            Assert.AreEqual(-1, PirateBases.IndexOf(2));
            Assert.AreEqual(2, PirateBases.IndexOf(47));
            Assert.IsTrue(PirateBases.SystemHasBase(Db, 2));
            Assert.IsFalse(PirateBases.SystemHasBase(Db, 11));
        }

        [Test]
        public void DestroyingTheOutpost()
        {
            PirateBases.Destroyed(33);
            Assert.IsFalse(PirateBases.StationHasBase(33));
            Assert.IsTrue(Session.PirateBaseDestroyed[1]);
            Assert.IsTrue(Session.PirateBaseRewardPending, "paid at the next docking");
            Assert.IsFalse(PirateBases.SystemHasBase(Db, 2));
            Assert.IsTrue(PirateBases.StationHasBase(1), "the others stay");

            Session.PirateBaseRewardPending = false;
            PirateBases.Destroyed(33);
            Assert.IsFalse(Session.PirateBaseRewardPending, "once");
            PirateBases.Destroyed(55);
            Assert.IsFalse(Session.PirateBaseRewardPending, "not a base");
        }

        [Test]
        public void TheBasesStaySavedDestroyed()
        {
            PirateBases.Destroyed(86);
            Session.StationIndex = 78;
            Assert.IsTrue(SaveGame.Save(6));
            Session.ResetNewGame();
            Assert.IsTrue(PirateBases.StationHasBase(86));
            Assert.IsTrue(SaveGame.Load(6));
            Assert.IsFalse(PirateBases.StationHasBase(86));
        }
    }
}
