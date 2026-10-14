// OreProbabilityTests.cs
// Feature: CLAUDE.md "Mining" (asteroid ores: Galaxy::getAsteroidProbabilities, ores near their cheapest system dominate;
// Naneroh's Novanium after step 0x59) and "Space scene" (the alien orbit's Void Crystals).
// Expected numbers: Reference/research/space_props.md "Ore choice" (the rule, and its table "Top ores per system (p after
// the -2k adjustment)"), cross-checked with a hand-run Python version of the rule over systems.json / items.json; item
// numbers from Reference/research/mining.md (Gold 154 .. Hypanium 163, Void Crystals 164, Novanium 217).

using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.World;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class OreProbabilityTests : SandboxTest
    {
        const int Gold = 154, Titanium = 155, Iron = 156, Orichalzine = 157, Pyresium = 158, Sodil = 159, Doxtrite = 160,
                  Cesogen = 161, Perrius = 162, Hypanium = 163, VoidCrystals = 164;

        static OrbitLayout At(int system, int station) => new OrbitLayout { systemIndex = system, stationIndex = station };

        [TestCase(0, Iron, 100, Cesogen, 75, Gold, 65)]           // Suteo
        [TestCase(1, Sodil, 79, Iron, 72, Cesogen, 55)]           // Pan
        [TestCase(3, Hypanium, 87, Cesogen, 81, Perrius, 77)]     // V'ikka
        [TestCase(9, Gold, 100, Cesogen, 81, Perrius, 74)]        // Weymire
        [TestCase(12, Hypanium, 88, Doxtrite, 85, Perrius, 80)]   // Oom'bak
        [TestCase(15, Pyresium, 86, Gold, 77, Orichalzine, 59)]   // Mido (table: "Pyresium 86, Gold 77")
        [TestCase(20, Titanium, 87, Hypanium, 81, Doxtrite, 64)]  // K'ontrr
        [TestCase(22, Doxtrite, 85, Perrius, 63, Orichalzine, 60)]// Beidan
        [TestCase(26, Gold, 87, Pyresium, 77, Orichalzine, 66)]   // Shima
        [TestCase(27, Pyresium, 93, Gold, 69, Cesogen, 50)]       // Ginoya
        [TestCase(33, Sodil, 90, Hypanium, 74, Titanium, 60)]     // Alda
        public void TopOresPerSystem(int system, int a, int pa, int b, int pb, int c, int pc)
        {
            Session.CampaignMission = 20;
            var list = OrbitLayout.OreProbabilities(Db, At(system, Db.Systems.First(s => s.index == system).stations[0]));
            Assert.AreEqual((a, pa), list[0]);
            Assert.AreEqual((b, pb), list[1]);
            Assert.AreEqual((c, pc), list[2]);
        }

        [TestCase(0), TestCase(15), TestCase(27), TestCase(31)]
        public void ElevenPairsSortedWithEveryOreOnce(int system)
        {
            Session.CampaignMission = 20;
            var list = OrbitLayout.OreProbabilities(Db, At(system, Db.Systems.First(s => s.index == system).stations[0]));
            Assert.AreEqual(11, list.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(Gold, 11), list.Select(e => e.item));
            for (int k = 1; k < list.Count; k++)
                Assert.That(list[k].p, Is.LessThanOrEqualTo(list[k - 1].p), "descending");
            Assert.IsTrue(list.All(e => e.p >= 0 && e.p <= 100));
            Assert.AreEqual(0, list.Single(e => e.item == VoidCrystals).p, "Void Crystals 0 outside the alien orbit");
        }

        [Test]
        public void AlienOrbitIsAllVoidCrystals()
        {
            var list = OrbitLayout.OreProbabilities(Db, At(-1, Session.VoidOrbit));
            Assert.AreEqual((VoidCrystals, 100), list[0]);
            Assert.IsTrue(list.Skip(1).All(e => e.p == 0));
        }

        [Test]
        public void NanerohTurnsToNovaniumAfterStep89()
        {
            // getAsteroidProbabilities with Status::inSupernovaOrbit (station 0x6d) and mission > 0x59: every ore is 0xd9,
            // the chances stay Ginoya's
            var naneroh = At(27, 109);
            Session.CampaignMission = 0x59;
            Assert.IsFalse(OrbitLayout.NovaniumOrbit(naneroh));
            Assert.AreEqual(Pyresium, OrbitLayout.OreProbabilities(Db, naneroh)[0].item);
            Session.CampaignMission = 0x5a;
            Assert.IsTrue(OrbitLayout.NovaniumOrbit(naneroh));
            var list = OrbitLayout.OreProbabilities(Db, naneroh);
            Assert.IsTrue(list.All(e => e.item == OrbitLayout.Novanium));
            Assert.AreEqual(217, OrbitLayout.Novanium);
            Assert.AreEqual(new[] { 93, 69, 50 }, list.Take(3).Select(e => e.p).ToArray());
        }

        [Test]
        public void OnlyNanerohHasNovanium()
        {
            Session.CampaignMission = 162;
            Assert.IsFalse(OrbitLayout.NovaniumOrbit(At(27, 110)));
            Assert.IsFalse(OrbitLayout.NovaniumOrbit(At(27, 112)));
            Assert.IsFalse(OrbitLayout.NovaniumOrbit(At(-1, 109)), "no system: not the supernova orbit");
            Assert.IsTrue(OrbitLayout.NovaniumOrbit(At(27, 109)));
        }

        [Test]
        public void FinishedWorldHasNovanium()
        {
            // CLAUDE.md "Mining": the remake reads Session.WorldIndex, so the finished game's world (multiplayer) has it too
            Session.UseCompletedWorld();
            Assert.IsTrue(OrbitLayout.NovaniumOrbit(At(27, 109)));
            Session.ResetNewGame();
            Session.FreePlay = true;   // plain free play reads index 20
            Session.CampaignMission = 162;
            Assert.IsFalse(OrbitLayout.NovaniumOrbit(At(27, 109)));
        }
    }
}
