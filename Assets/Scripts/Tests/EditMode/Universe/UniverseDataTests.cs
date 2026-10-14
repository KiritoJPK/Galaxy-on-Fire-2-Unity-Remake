// UniverseDataTests.cs
// Feature: CLAUDE.md "Data" (systems / stations tables) and "Space scene" / "Star map and system travel" (one visible
// jumpgate per gated system, in its jumpgateStation orbit; gates both ways; the gateless systems).
// Expected values: invariants of the original's universe, checked by hand on systems.json / stations.json with python -I
// (135 stations 0..134 in 34 systems, each listed by exactly one system; symmetric gate routes; jumpgateStation -1 exactly
// for the systems without routes: Mido, Y'mirr, Beidan, Herjaza, Skavac, Skor Terpa, Alda, as space_level_setup.md 2 lists);
// station counts / the new game's start from space_level_setup.md 9 (station 78 Var Hastra in system 15 Mido).

using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class UniverseDataTests : SandboxTest
    {
        [Test]
        public void OriginalUniverseSize()
        {
            Assert.AreEqual(Enumerable.Range(0, 34), Db.Systems.Where(s => s.index < 34).Select(s => s.index).OrderBy(i => i));
            Assert.AreEqual(Enumerable.Range(0, 135), Db.Stations.Where(s => s.index < 135).Select(s => s.index).OrderBy(i => i));
        }

        [Test]
        public void EveryStationBelongsToOneExistingSystemThatListsIt()
        {
            foreach (var st in Db.Stations)
            {
                var sys = Db.Systems.Where(s => s.index == st.system).ToList();
                Assert.AreEqual(1, sys.Count, $"station {st.index}: system {st.system}");
                Assert.IsTrue(sys[0].stations.Contains(st.index), $"station {st.index} not listed by system {st.system}");
                Assert.AreEqual(1, Db.Systems.Count(s => s.stations.Contains(st.index)), $"station {st.index} listed once");
            }
            foreach (var sys in Db.Systems)
                foreach (int st in sys.stations)
                    Assert.AreEqual(sys.index, Db.Stations.Single(s => s.index == st).system, $"system {sys.index} lists station {st}");
        }

        [Test]
        public void SystemIndicesAndStationIndicesAreUnique()
        {
            Assert.AreEqual(Db.Systems.Count, Db.Systems.Select(s => s.index).Distinct().Count());
            Assert.AreEqual(Db.Stations.Count, Db.Stations.Select(s => s.index).Distinct().Count());
        }

        [Test]
        public void GateRoutesGoBothWays()
        {
            foreach (var sys in Db.Systems)
                foreach (int to in sys.jumpRoutesTo)
                {
                    var other = Db.Systems.SingleOrDefault(s => s.index == to);
                    Assert.IsNotNull(other, $"system {sys.index} routes to missing system {to}");
                    Assert.IsTrue(other.jumpRoutesTo.Contains(sys.index), $"{sys.index} -> {to} has no way back");
                    Assert.AreNotEqual(sys.index, to, "no route to itself");
                }
        }

        [Test]
        public void JumpgateStationIsInItsOwnSystem()
        {
            foreach (var sys in Db.Systems.Where(s => s.jumpgateStation >= 0))
            {
                Assert.IsTrue(sys.stations.Contains(sys.jumpgateStation), $"system {sys.index}");
                Assert.AreEqual(sys.index, Db.Stations.Single(s => s.index == sys.jumpgateStation).system);
            }
        }

        [Test]
        public void GatelessSystemsAreExactlyThoseWithoutRoutes()
        {
            foreach (var sys in Db.Systems)
                Assert.AreEqual(sys.jumpgateStation < 0, sys.jumpRoutesTo.Count == 0, $"system {sys.index}");
            CollectionAssert.AreEquivalent(new[] { 10, 15, 22, 23, 24, 32, 33 },
                Db.Systems.Where(s => s.index < 34 && s.jumpgateStation < 0).Select(s => s.index));
        }

        [Test]
        public void SystemsHaveStationsAndSaneFields()
        {
            foreach (var sys in Db.Systems)
            {
                Assert.That(sys.stations.Count, Is.InRange(1, 7), $"system {sys.index}");
                Assert.That(sys.raceId, Is.InRange(0, 3), $"system {sys.index}");
                Assert.That(sys.securityLevel, Is.InRange(0, 3), $"system {sys.index}");
                Assert.IsNotNull(sys.mapPosition, $"system {sys.index}");
                Assert.That(sys.textureIndex, Is.InRange(0, 18), $"system {sys.index}");
            }
            foreach (var st in Db.Stations.Where(s => s.index < 135))
                Assert.That(st.textureIndex, Is.InRange(0, 26), $"station {st.index}");
        }

        [Test]
        public void NewGameStartsAtVarHastraInMido()
        {
            Assert.AreEqual(78, Session.StationIndex);
            var st = Db.Stations.Single(s => s.index == 78);
            Assert.AreEqual(15, st.system);
            Assert.AreEqual(-1, Db.Systems.Single(s => s.index == 15).jumpgateStation, "Mido has no jumpgate");
        }

        [Test]
        public void RevealedSystemsExist()
        {
            foreach (int s in Story.RevealedSystems)
                Assert.IsTrue(Db.Systems.Exists(x => x.index == s), $"system {s}");
        }
    }
}
