// OrbitRulesTests.cs
// Feature: CLAUDE.md "Space scene" (empty orbits, the station object in 27 / 110 / 111, the alien orbit, the supernova sun,
// lights / fog / dust tint, the sky exceptions, the jumpgate per race, asteroid types, the planet ring orbits, the remake's
// rebuilt Ginoya in the completed world).
// Expected numbers: Reference/research/space_level_setup.md (2 "Empty orbit", 3.2 gate geometry by race, 4 undock spot,
// 6 initLight tables and fog table), Reference/research/space_backdrop.md (sun textures / flare colours of Appendix A, the
// supernova sun sizes, the Void branch, the ring orbit's 26000 / 65536, LIGHT0 clamp examples tex 5 / tex 12),
// Reference/research/space_props.md (asteroid types / meshes / bounding radii, the alien orbit's field centre, the fog-sprite
// tint rule of section 4), and the binary's tables read independently from libgof2hdaa.so (DAT_00252ca0 fog tints,
// DAT_00258bf0 planet light, StarSystem::initLight's flat index 0x17 for the Void).

using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class OrbitRulesTests : SandboxTest
    {
        static OrbitLayout Build(int station, int world = 20)
        {
            Session.CampaignMission = world;
            return OrbitLayout.Build(Db, station);
        }

        static void AreColor(float r, float g, float b, Color c, string what, float tol = 1e-4f)
        {
            Assert.AreEqual(r, c.r, tol, what + " r");
            Assert.AreEqual(g, c.g, tol, what + " g");
            Assert.AreEqual(b, c.b, tol, what + " b");
        }

        // ---- Status::inEmptyOrbit 0xb8ee8 (space_level_setup.md 2) ----------------------------------------------------

        [TestCase(102), TestCase(103), TestCase(104), TestCase(109), TestCase(110), TestCase(132), TestCase(133), TestCase(134)]
        public void AlwaysEmptyOrbits(int station)
        {
            foreach (int mission in new[] { 0, 1, 20, 45, 84, 100, 162 })
                Assert.IsTrue(OrbitLayout.IsEmptyOrbit(station, mission), $"station {station} at {mission}");
        }

        [TestCase(0), TestCase(10), TestCase(70), TestCase(100), TestCase(108), TestCase(112), TestCase(113), TestCase(131)]
        public void OrdinaryStationsAreNeverEmpty(int station)
        {
            foreach (int mission in new[] { 0, 1, 20, 45, 84, 94, 100, 154, 162 })
                Assert.IsFalse(OrbitLayout.IsEmptyOrbit(station, mission), $"station {station} at {mission}");
        }

        [Test]
        public void VarHastraIsEmptyOnlyInThePrologueAndRescue()
        {
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(78, 0));
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(78, 1));
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(78, 2));
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(78, 20));
        }

        [Test]
        public void LuurIsEmptyAfterMission93()
        {
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(111, 93));
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(111, 94));
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(111, 162));
        }

        [Test]
        public void ValkyrieOrbitIsEmptyFrom84()
        {
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(101, 83));
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(101, 84));
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(101, 162));
        }

        [Test]
        public void CompletedWorldRebuildsGinoyasStations()
        {
            // CLAUDE.md "Multiplayer", the finished game's world: Naneroh, Valpatro and Luur intact and open for docking.
            Assert.IsTrue(OrbitLayout.IsRebuiltGinoya(109) && OrbitLayout.IsRebuiltGinoya(110) && OrbitLayout.IsRebuiltGinoya(111));
            Assert.IsFalse(OrbitLayout.IsRebuiltGinoya(108) || OrbitLayout.IsRebuiltGinoya(112));
            Session.UseCompletedWorld();
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(109, Session.WorldIndex));
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(110, Session.WorldIndex));
            Assert.IsFalse(OrbitLayout.IsEmptyOrbit(111, Session.WorldIndex));
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(102, Session.WorldIndex), "the other empty orbits stay empty");
            Assert.IsTrue(OrbitLayout.IsEmptyOrbit(132, Session.WorldIndex));
        }

        [Test]
        public void StationObjectInEmptyOrbits()
        {
            // Level::createSpace 0xbc0f6: 27, 110 and 111 keep their PlayerStation even in an empty orbit (no docking).
            var o = Build(110);
            Assert.IsFalse(o.hasStation);
            Assert.IsTrue(o.stationObject);
            o = Build(111, 94);
            Assert.IsFalse(o.hasStation);
            Assert.IsTrue(o.stationObject);
            o = Build(102);
            Assert.IsFalse(o.hasStation);
            Assert.IsFalse(o.stationObject);
            o = Build(78, 0);
            Assert.IsFalse(o.hasStation, "the prologue has no station");
            Assert.IsFalse(o.stationObject);
            o = Build(78, 20);
            Assert.IsTrue(o.hasStation);
            Assert.IsTrue(o.stationObject);
        }

        // ---- the alien orbit (Session.VoidOrbit) -------------------------------------------------------------------

        [TestCase(0, true), TestCase(42, true), TestCase(43, false), TestCase(83, false), TestCase(84, true), TestCase(153, true), TestCase(154, false), TestCase(162, false)]
        public void VoidStationExistsOutside43To83AndFrom154(int mission, bool station)
        {
            var o = Build(Session.VoidOrbit, mission);
            Assert.IsTrue(o.alienOrbit);
            Assert.AreEqual(station, o.hasStation);
            Assert.AreEqual(station, o.stationObject);
        }

        [Test]
        public void AlienOrbitLayout()
        {
            var o = Build(Session.VoidOrbit);
            Assert.AreEqual(-1, o.systemIndex);
            Assert.IsFalse(o.hasJumpgate);
            Assert.AreEqual("jumpgate_terran", o.JumpgateAssembly, "race 0 in the alien orbit");
            // landmark 2 at (0, rand(20000) - 10000, rand(50000) + 170000)
            Assert.AreEqual(0f, o.hiddenJumpgate.x);
            Assert.That(o.hiddenJumpgate.y, Is.InRange(-10000f, 9999f));
            Assert.That(o.hiddenJumpgate.z, Is.InRange(170000f, 219999f));
            // Void branch: sun_010 straight ahead (L = (0, 0, -1)), one planet planet_void_big at yaw 5.2347097, unseeded size
            Assert.AreEqual("sun_010", o.sunTexture);
            Assert.AreEqual(0.2288818359375f, o.sunScale, 1e-7f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 0f, -1f), o.lightDirection), 1e-6f);
            Assert.AreEqual(1, o.planets.Count);
            var p = o.planets[0];
            Assert.IsTrue(p.isOrbitPlanet);
            Assert.AreEqual("planet_void_big", p.texture);
            Assert.AreEqual(5.2347097f, p.yaw, 1e-6f);
            Assert.IsFalse(p.flip);
            Assert.That(p.scale, Is.InRange(20000f / 65536f, 39999f / 65536f));
            // the void uses sun colour row 10, no fog, its Void asteroids around (-30000, 0, 30000), dust tint 0x9274d4
            AreColor(.43f, .21f, .34f, o.sunColor, "sun colour");
            Assert.IsFalse(o.fog);
            Assert.AreEqual(1, o.asteroidType);
            Assert.AreEqual("asteroid_void", o.AsteroidAssembly);
            Assert.AreEqual(6064f, o.AsteroidMeshRadius);
            Assert.AreEqual(new Vector3(-30000f, 0f, 30000f), o.asteroidCentre);
            Color32 dust = o.dustFogTint;
            Assert.AreEqual(0x92, dust.r); Assert.AreEqual(0x74, dust.g); Assert.AreEqual(0xd4, dust.b);
        }

        [Test]
        public void AlienOrbitPlanetLightReadsTheFlatTableIndex23()
        {
            // StarSystem::initLight 0x15d080: without a station the planet-light colour is read at DAT_00258bf0 + 0x17 * 4,
            // a flat index (not row 23 x 3), so (0.11, 0.18, 0.25) x 1.5 (space_backdrop.md "Lighting"; the binary's table:
            // elements 23..25 = 0.11, 0.18, 0.25, row 23 = 0.64, 0.50, 0.67).
            var o = Build(Session.VoidOrbit);
            AreColor(0.11f * 1.5f, 0.18f * 1.5f, 0.25f * 1.5f, o.planetLightColor, "planet light");
        }

        // ---- the sun (space_backdrop.md Appendix A, the supernova overrides) ----------------------------------------

        [TestCase(0, "sun_004", 3), TestCase(2, "sun_009", 3), TestCase(3, "sun_005", 2), TestCase(4, "sun_008", 0), TestCase(6, "sun_000", 1),
         TestCase(7, "sun_007", 4), TestCase(8, "sun_001", 2), TestCase(22, "sun_008", 2), TestCase(23, "sun_001", 0), TestCase(25, "sun_000", 1),
         TestCase(28, "sun_002", 3), TestCase(29, "sun_007", 3), TestCase(31, "sun_006", 3), TestCase(33, "sun_007", 3)]
        public void SunTextureAndFlareColourPerSystem(int system, string texture, int flare)
        {
            var o = Build(Db.Systems.First(s => s.index == system).stations[0]);
            Assert.AreEqual(texture, o.sunTexture);
            Assert.AreEqual(flare, o.flareColor);
            Assert.AreEqual(0.2288818359375f, o.sunScale, 1e-7f, "15000 / 65536");
            Assert.IsFalse(o.supernovaSun);
        }

        [Test]
        public void GinoyaBeforeTheSupernovaReachesIt()
        {
            // inSupernovaSystem (27 before 0x9e): the sn_supernova sun at 0.99182, flare colour 5 (K = 80).
            var o = Build(112, 20);
            Assert.AreEqual("sn_supernova", o.sunTexture);
            Assert.AreEqual(0.9918212890625f, o.sunScale, 1e-7f);
            Assert.IsTrue(o.supernovaSun);
            Assert.AreEqual(5, o.flareColor);
            Assert.IsTrue(o.skySunAligned, "system 27's sky +Y points at the sun");
        }

        [Test]
        public void GinoyaAtMission89UsesTheSnSun011()
        {
            var o = Build(112, 0x59);
            Assert.AreEqual("sn_sun_011", o.sunTexture);
            Assert.AreEqual(3, o.flareColor);
            Assert.AreEqual(0.9918212890625f, o.sunScale, 1e-7f);
            Assert.IsTrue(o.supernovaSun);
        }

        [TestCase(0x69, 0.9918212890625f), TestCase(0x6a, 1.373291015625f), TestCase(0x9d, 1.373291015625f)]
        public void SupernovaSunGrowsFrom106(int mission, float scale)
        {
            var o = Build(112, mission);
            Assert.AreEqual(scale, o.sunScale, 1e-7f);
            Assert.IsTrue(o.supernovaSun);
            Assert.AreEqual("sn_supernova", o.sunTexture);
        }

        [TestCase(0x9e), TestCase(162)]
        public void GinoyaAfterTheSupernova(int mission)
        {
            // past 157 the system is no longer the supernova system: normal size, sn_sun_011, flare colour 3
            var o = Build(112, mission);
            Assert.IsFalse(o.supernovaSun);
            Assert.AreEqual(0.2288818359375f, o.sunScale, 1e-7f);
            Assert.AreEqual("sn_sun_011", o.sunTexture);
            Assert.AreEqual(3, o.flareColor);
        }

        // ---- sky rotation exceptions ------------------------------------------------------------------------------

        [TestCase(116), TestCase(114), TestCase(130), TestCase(128)]
        public void FogSkiesHaveNoRotation(int station)
        {
            // inFogSkyboxOrbit (textureIndex 17 / 18: Talidor, Wah'norr): rx = ry = rz = 0
            Assert.AreEqual(Vector3.zero, Build(station).skyEuler);
        }

        [TestCase(0), TestCase(78), TestCase(120)]
        public void OnlyGinoyasSkyIsSunAligned(int station)
        {
            Assert.IsFalse(Build(station).skySunAligned);
        }

        // ---- lights, fog, dust (StarSystem::initLight, Level::initParticleSystems) ---------------------------------

        [Test]
        public void SunAndPlanetLightColours()
        {
            var o = Build(0);   // Suteo: sysTex 4, station texture 0
            AreColor(.18f, .23f, .27f, o.sunColor, "sun colour tex 4");
            AreColor(.29f * 1.5f, .35f * 1.5f, .31f * 1.5f, o.planetLightColor, "planet light stTex 0 x 1.5");
            o = Build(120);     // Paréah: sysTex 16, station texture 26
            AreColor(.39f, .48f, .44f, o.sunColor, "sun colour tex 16");
            AreColor(.21f * 1.5f, .27f * 1.5f, .34f * 1.5f, o.planetLightColor, "planet light stTex 26");
            o = Build(112);     // Ginoya: sysTex 15, station texture 24
            AreColor(1f, .52f, .24f, o.sunColor, "sun colour tex 15");
            AreColor(.45f * 1.5f, .38f * 1.5f, .34f * 1.5f, o.planetLightColor, "planet light stTex 24");
        }

        [Test]
        public void SunLightIsClampedTo2()
        {
            // LIGHT0 = clamp(15 * sunColour, 0, 2): white x2 for most systems, tex 5 (2, 1.95, 1.65), tex 12 (1.2, 2, 2)
            AreColor(2f, 1.95f, 1.65f, Build(15).SunLightColor, "V'ikka (tex 5)", 1e-3f);
            AreColor(1.2f, 2f, 2f, Build(100).SunLightColor, "Beidan (tex 12)", 1e-3f);
            AreColor(2f, 2f, 2f, Build(78).SunLightColor, "Mido (tex 9)", 1e-3f);
        }

        [TestCase(101, 0xdb, 0x69, 0x23, 50000f)]   // Herjaza, tex 11
        [TestCase(100, 0x16, 0x3e, 0x7c, 50000f)]   // Beidan, tex 12
        [TestCase(112, 0x82, 0x44, 0x1f, 100000f)]  // Ginoya, tex 15
        [TestCase(119, 0x47, 0x66, 0x5e, 150000f)]  // Paréah, tex 16
        [TestCase(116, 0x73, 0x8d, 0x95, 150000f)]  // Talidor, tex 17
        [TestCase(130, 0xab, 0xa0, 0x75, 150000f)]  // Wah'norr, tex 18
        public void FogSystems(int station, int r, int g, int b, float end)
        {
            var o = Build(station);
            Assert.IsTrue(o.fog);
            Color32 c = o.fogColor;
            Assert.AreEqual(new[] { r, g, b }, new int[] { c.r, c.g, c.b });
            Assert.AreEqual(end, o.fogEnd);
        }

        [TestCase(0), TestCase(78), TestCase(108), TestCase(126), TestCase(132)]
        public void NoFogElsewhere(int station)
        {
            Assert.IsFalse(Build(station).fog);
        }

        [Test]
        public void GinoyaHasNoFogAtMission89()
        {
            // initLight case 0xf returns before the fog at campaign 0x59 (the supernova cutscene)
            Assert.IsFalse(Build(112, 89).fog);
            Assert.IsTrue(Build(112, 88).fog);
        }

        [TestCase(0, 73, 73, 73)]      // Suteo tex 4: 0x7a7a7a x 0.6
        [TestCase(78, 123, 133, 135)]  // Mido tex 9: 0xcedee1 x 0.6
        [TestCase(100, 153, 153, 153)] // Beidan tex 12: 0xffffff x 0.6
        [TestCase(105, 104, 109, 75)]  // Loma tex 14: 0xaeb77d x 0.6
        public void DustFogTint(int station, int r, int g, int b)
        {
            // Level::initParticleSystems: rgb = DAT_00252ca0[sysTex] * 0.6, alpha 0xbb
            Color32 c = Build(station).dustFogTint;
            Assert.AreEqual(new[] { r, g, b, 0xbb }, new int[] { c.r, c.g, c.b, c.a });
        }

        [Test]
        public void DustFogTintInTheSupernovaSystem()
        {
            // Level::initParticleSystems: in the supernova system the factor is 0.5 and the alpha 0xff
            // (space_props.md 4 "Fog tint"; the decompiled code: fVar17 = 0.5, uVar10 = 0xff when inSupernovaSystem).
            // Ginoya tex 15 = 0xdb6923 -> (109, 52, 17, 255).
            Color32 c = Build(112, 20).dustFogTint;
            Assert.AreEqual(new[] { 109, 52, 17, 255 }, new int[] { c.r, c.g, c.b, c.a });
        }

        // ---- gates, asteroids, ring orbits, undock ------------------------------------------------------------------

        [TestCase(5, "jumpgate_terran", 7500)]       // Pan, Terran
        [TestCase(15, "jumpgate_vossk_anim", 11250)] // V'ikka, Vossk
        [TestCase(30, "jumpgate_nivelian", 7500)]    // Behén, Nivelian
        [TestCase(20, "jumpgate_midorian", 7500)]    // Eanya, Midorian
        public void JumpgateByRace(int station, string assembly, int radius)
        {
            var o = Build(station);
            Assert.IsTrue(o.hasJumpgate);
            Assert.AreEqual(assembly, o.JumpgateAssembly);
            Assert.AreEqual(radius, o.JumpgateRadius);
        }

        [Test]
        public void GatelessSystemsHaveNoVisibleGate()
        {
            // systems with jumpgateStation -1 (Mido, Y'mirr, Beidan, Herjaza, Skavac, Skor Terpa, Alda)
            foreach (int station in new[] { 78, 50, 100, 101, 103, 132, 133 })
                Assert.IsFalse(Build(station).hasJumpgate, $"station {station}");
        }

        [Test]
        public void AsteroidTypes()
        {
            var o = Build(100);   // Beidan (system 22): ice
            Assert.AreEqual(2, o.asteroidType);
            Assert.AreEqual("v_asteroid_ice", o.AsteroidAssembly);
            Assert.AreEqual(2835f, o.AsteroidMeshRadius);
            o = Build(0);
            Assert.AreEqual(0, o.asteroidType);
            Assert.AreEqual("asteroid_01", o.AsteroidAssembly);
            Assert.AreEqual(3547f, o.AsteroidMeshRadius);
        }

        [TestCase(120), TestCase(126), TestCase(130), TestCase(132)]
        public void PlanetRingOrbits(int station)
        {
            // Status::inPlanetRingOrbit: the orbit planet is 26000 / 65536 and has no ring of its own (the sky layer shows it)
            var o = Build(station);
            Assert.IsTrue(o.ringOrbit);
            var orbit = o.planets.Single(p => p.isOrbitPlanet);
            Assert.AreEqual(26000f / 65536f, orbit.scale, 1e-7f);
            Assert.IsFalse(orbit.ring);
        }

        [Test]
        public void RingPlanetsSeenFromElsewhere()
        {
            // orbitHasPlanetRing (120, 126, 130, 132): their planets carry the ring in the other orbits of the system
            Assert.IsFalse(Build(119).ringOrbit);
            Assert.IsTrue(Build(119).planets.Single(p => p.station == 120).ring);
            Assert.IsTrue(Build(124).planets.Single(p => p.station == 126).ring);
            Assert.IsTrue(Build(128).planets.Single(p => p.station == 130).ring);
            Assert.IsFalse(Build(128).planets.Single(p => p.station == 131).ring);
        }

        [Test]
        public void OrbitPlanetSizeByStationTexture()
        {
            // StarSystem::StarSystem: own planet s = nextInt(20000) + 20000; t in {6, 11, 12, 17}: nextInt(15000) + 35000;
            // t in {9, 16}: nextInt(13000) + 32500 (scale = s / 65536)
            foreach (var st in Db.Stations.Where(s => s.index < 135 && s.index != 120 && s.index != 126 && s.index != 130 && s.index != 132))
            {
                float s = Build(st.index).planets.Single(p => p.isOrbitPlanet).scale * 65536f;
                int t = st.textureIndex;
                if (t == 6 || t == 11 || t == 12 || t == 17) Assert.That(s, Is.InRange(35000f - 0.5f, 49999f + 0.5f), $"station {st.index}");
                else if (t == 9 || t == 16) Assert.That(s, Is.InRange(32500f - 0.5f, 45499f + 0.5f), $"station {st.index}");
                else Assert.That(s, Is.InRange(20000f - 0.5f, 39999f + 0.5f), $"station {st.index}");
            }
        }

        [Test]
        public void ForcedSunSlots()
        {
            // DAT_00258e20: station texture 9, 13, 14, 18, 20 -> slot 6; 21 -> 4; 22 -> 16
            Assert.AreEqual(6, Build(10).sunSlot);    // tex 9
            Assert.AreEqual(6, Build(8).sunSlot);     // tex 13
            Assert.AreEqual(6, Build(62).sunSlot);    // tex 14
            Assert.AreEqual(6, Build(116).sunSlot);   // tex 18
            Assert.AreEqual(6, Build(105).sunSlot);   // tex 20
            Assert.AreEqual(4, Build(106).sunSlot);   // tex 21
            Assert.AreEqual(16, Build(107).sunSlot);  // tex 22
            Assert.AreEqual(16, Build(126).sunSlot);  // tex 22
        }

        [Test]
        public void OrbitPlanetFlipsWhenTheSunIsPastSlot11()
        {
            foreach (int station in new[] { 0, 5, 10, 78, 107, 112, 120 })
            {
                var o = Build(station);
                Assert.AreEqual(o.sunSlot >= 12, o.planets.Single(p => p.isOrbitPlanet).flip, $"station {station}");
            }
        }

        [Test]
        public void UndockSpot()
        {
            // Level::init 0xbb7a8: (10, 10, 10000), yaw +-1600 / 65536 of a turn
            Assert.AreEqual(new Vector3(10f, 10f, 10000f), OrbitLayout.UndockPosition);
            Assert.AreEqual(1600, OrbitLayout.UndockYaw);
            Assert.AreEqual(7500, OrbitLayout.GateRadius);
            Assert.AreEqual(11250, OrbitLayout.GateRadiusVossk);
        }
    }
}
