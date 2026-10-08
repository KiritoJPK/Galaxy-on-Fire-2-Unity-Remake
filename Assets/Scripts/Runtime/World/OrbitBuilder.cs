// OrbitBuilder.cs
// Turns a OrbitLayout into scene objects. Shared by the flight level (SpaceLevel) and the main menu
// background (MenuBackground), like the original, whose menu backdrop is a normal orbit level (Level type 2 in
// Level::createScene 0xc2910: createPlayer + an empty mission) built by the same Level::init code.
//   Level::createSpace      sky (GoF2/SpaceSky), station at the origin, jumpgate, sun/planets (Backdrop)
//   StarSystem::initLight   LIGHT0 toward the sun, LIGHT1 from the orbit planet (Unity +Z), skybox ambient, fog
//   Level::createAsteroids  asteroids around the seeded centre (per-visit placement with UnityEngine.Random), each
//                           with its ore (Galaxy::getAsteroidProbabilities) and quality A..D for mining
//   initParticleSystems     space dust + fog sprites around the camera

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GoF2Remake.World
{
    public static class OrbitBuilder
    {
        const float M = OrbitLayout.MetersPerUnit;

        // ---- sky, light, fog ---------------------------------------------------------------------------------

        public static void SetupSky(OrbitLayout layout, float ambientIntensity = 1f, int nebulaOverride = -1)
        {
            var template = Resources.Load<Material>("GoF2Sky/SpaceSky");
            if (template == null) { Debug.LogWarning("OrbitBuilder: run GoF2 > Build > Space Skies"); return; }
            var sky = new Material(template) { name = "SpaceSky (runtime)" };
            int stars = layout.systemIndex >= 0 ? layout.systemIndex % 3 : 2;   // alien/void: stars_002
            sky.SetTexture("_Stars", Resources.Load<Cubemap>($"GoF2Sky/stars_{stars:000}"));
            int nebula = nebulaOverride >= 0 ? nebulaOverride : layout.systemTexture;   // the prologue's belt: nebula 3
            sky.SetTexture("_Nebula", Resources.Load<Cubemap>($"GoF2Sky/nebula_{nebula:000}"));
            var rotation = SkyRotation(layout);
            // Remake mods: a system's own sky (systems.json "skybox": panorama / cube-strip images, the star layer, a turn).
            var mod = layout.systemIndex >= 0 && nebulaOverride < 0 ? Modding.ModWorld.SkyOf(layout.systemIndex) : null;
            if (mod != null)
            {
                SetModLayer(sky, mod.nebula, "_NebulaMap", "_NEBULA_PANORAMA", "_NEBULA_STRIP");
                if (mod.stars != null) SetModLayer(sky, mod.stars, "_StarsMap", "_STARS_PANORAMA", "_STARS_STRIP");
                else if (mod.starsLayer >= 0) sky.SetTexture("_Stars", Resources.Load<Cubemap>($"GoF2Sky/stars_{mod.starsLayer:000}"));
                sky.SetFloat("_NebulaGain", mod.nebulaBrightness);
                sky.SetFloat("_StarsGain", mod.starsLayer == -2 ? 0f : mod.starsBrightness);
                if (mod.rotation.HasValue) rotation = Quaternion.Euler(mod.rotation.Value);
            }
            // The shader maps world directions into the baked cube: the inverse of the sky's Unity rotation.
            sky.SetMatrix("_SkyRotation", Matrix4x4.Rotate(Quaternion.Inverse(rotation)));
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            SkyReflection.Update();   // the ambient light and, in builds too, the reflection

            Bootstrap.SetSceneFog(layout.fog);   // off below Quality High
            if (layout.fog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 0f;
                RenderSettings.fogEndDistance = layout.fogEnd * M;
                RenderSettings.fogColor = layout.fogColor;
            }
        }

        /// <summary>A mod's sky image on a GoF2/SpaceSky layer: 2:1 = a panorama, 6:1 = a strip of cube faces (else a warning
        /// and the game's layer stays).</summary>
        static void SetModLayer(Material sky, string name, string property, string panorama, string strip)
        {
            if (name == null) return;
            var tex = Modding.ModBackdrop.Texture(name);
            if (tex == null) return;
            float aspect = tex.width / (float)Mathf.Max(1, tex.height);
            bool isStrip = Mathf.Abs(aspect - 6f) < 0.05f, isPanorama = Mathf.Abs(aspect - 2f) < 0.05f;
            if (!isStrip && !isPanorama)
            {
                Debug.LogWarning($"OrbitBuilder: the skybox image {name} is {tex.width} x {tex.height}: a panorama is 2:1, a strip of cube faces 6:1");
                return;
            }
            tex.wrapMode = isPanorama ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.wrapModeV = TextureWrapMode.Clamp;
            sky.SetTexture(property, tex);
            sky.EnableKeyword(isPanorama ? panorama : strip);
        }

        /// <summary>R_sky in Unity (the baked cubemaps are the sky meshes at identity, after the import's 180 deg yaw).</summary>
        public static Quaternion SkyRotation(OrbitLayout layout)
        {
            if (!layout.skySunAligned) return OrbitLayout.RotationToUnity(layout.skyEuler);
            // System 27: X = a x b, Y = a (toward the sun), Z = b with b = normalize((1,0,0) x a). Mirrored to Unity
            // (S * R * S) the Z column flips sign: Y' = S a, Z' = -S b.
            var a = OrbitLayout.DirToUnity(layout.lightDirection).normalized;
            var b = OrbitLayout.DirToUnity(Vector3.Cross(Vector3.right, layout.lightDirection)).normalized;
            return Quaternion.LookRotation(-b, a) * Quaternion.Euler(0f, 180f, 0f);
        }

        /// <param name="sunIntensityAt2">URP intensity for the original's LIGHT0 diffuse of 2.0 (tuned, not recovered).</param>
        public static void SetupLights(OrbitLayout layout, Light sun, Light planet, float sunIntensityAt2 = 1.6f, float planetIntensity = 1f)
        {
            if (sun != null)
            {
                var toSun = OrbitLayout.DirToUnity(layout.lightDirection).normalized;
                sun.transform.rotation = Quaternion.LookRotation(-toSun);
                var c = layout.SunLightColor;   // 0..2 per channel, 2 for most systems
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b), 1e-3f);
                sun.color = c / max;
                sun.intensity = sunIntensityAt2 * max / 2f;
            }
            if (planet != null)
            {
                // LIGHT1 direction (0, 0, -1) game = toward the orbit planet (Unity +Z); light travels toward -Z.
                planet.transform.rotation = Quaternion.LookRotation(Vector3.back);
                planet.color = layout.planetLightColor;
                planet.intensity = planetIntensity;
            }
        }

        // ---- objects -----------------------------------------------------------------------------------------

        public static GameObject Spawn(Database db, string assembly, Vector3 gamePos, Quaternion rot, string label, Transform parent)
        {
            var prefab = AssembledObject.LoadPrefab(db.AssemblyByName(assembly));
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, OrbitLayout.ToUnity(gamePos), rot, parent);
            go.name = label;
            return go;
        }

        /// <summary>The station whose model and volumes a station uses: a mod's "looksLike" (Modding.ModWorld.StationLook);
        /// remake multiplayer's intact Naneroh / Valpatro (no model of their own) borrow Midantha's / Tergalon's, the
        /// Supernova add-on's Midorian stations next door (OrbitLayout.IsRebuiltGinoya).</summary>
        public static int StationLook(int station)
        {
            if (Session.CompletedWorld && station == 109) return 114;
            if (Session.CompletedWorld && station == 110) return 115;
            return Modding.ModWorld.StationLook(station);
        }

        /// <summary>Assembly name of a station (PlayerStation::PlayerStation, assemblies_stations_notes.md), null if none.</summary>
        public static string StationAssembly(Database db, OrbitLayout layout)
        {
            // Special cases, as they are before the campaign changes them.
            // PlayerStation: in the alien orbit the Void station (16443, collision 1001), after the Valkyrie add-on the battlestation.
            if (layout.alienOrbit) return Story.Dlc1Won ? "v_station_battlestation_anim" : "station_void";
            switch (layout.stationIndex)
            {
                // Kothar: exploding while Alice attacks (index 80), damaged after the add-on.
                case 100: return !Session.FreePlay && Story.Index == 80 ? "v_station_deep_science_explosion_anim"
                               : Story.Dlc1Won ? "v_station_deep_science_damaged" : "v_station_deep_science";
                case 101: return "v_station_battlestation_anim";
                case 108: return "station_kaamo_club";
                case 109: case 110:
                    if (Session.CompletedWorld) break;   // remake multiplayer: intact (StationLook)
                    return "sn_station_midorian_wrecked";
                // Luur (PlayerStation::PlayerStation): burning up to campaign 0x5d, the bare hull at 0x5e (its level adds the
                // burning platform), the wreck after. Remake multiplayer: intact (its model before the supernova, step 89).
                case 111: return Session.CompletedWorld ? "station_111_luur_intact_mission_89"
                               : Session.CampaignMission <= 0x5d ? "sn_burning_station_luur"
                               : Session.CampaignMission == 0x5e ? "station_111_luur_mission_94" : "sn_station_midorian_wrecked";
            }
            // A mod's own station model (ModStations), once built; else (and for the rest) the original it names ("looksLike",
            // Modding.ModWorld.StationLook).
            var own = Modding.ModWorld.ModelOf(layout.stationIndex);
            if (own != null && Modding.ModStations.Built(own.station)) return own.Assembly;
            string prefix = $"station_{StationLook(layout.stationIndex):000}_";
            var entry = db.Assemblies.Find(a => a.category == "stations" && (a.name.StartsWith(prefix)
                                                  || a.name.StartsWith("v_" + prefix) || a.name.StartsWith("sn_" + prefix)));
            return entry != null ? entry.name : layout.raceId == 1 ? "station_vossk" : null;   // Vossk: no collision entry
        }

        /// <summary>Level::createSpace / PlayerStation: at the origin, rotation (0, pi, 0) (= identity in Unity).</summary>
        public static GameObject SpawnStation(Database db, OrbitLayout layout, Transform parent = null)
        {
            if (!layout.stationObject) return null;   // Level::createSpace: the empty orbits 27 / 110 / 111 keep their PlayerStation
            string name = StationAssembly(db, layout);
            if (name == null) { Debug.LogWarning($"OrbitBuilder: no station assembly for {layout.stationIndex}"); return null; }
            var go = Spawn(db, name, Vector3.zero, OrbitLayout.RotationToUnity(new Vector3(0f, Mathf.PI, 0f)), "Station", parent);
            // Remake: the station's opacity keys play like the engine's (the blinking lights of the Nivelian, Midorian, pirate,
            // Loma and Supernova-era stations, the Kaamo Club's lit panels and glows); they were left out, the lights stayed on.
            if (go != null) foreach (var pa in go.GetComponentsInChildren<PartAnimation>(true)) pa.applyMaterialChannels = true;
            // PlayerStation::update advances the station's animation every frame except at 101 and in the alien orbit: the
            // battlestation's arms hold their first frame there, or their last once step 78 unfolded them (the ctor's
            // Transform::Update(the length): station 0x65 from campaign 0x50 0x1473c2, the alien orbit after the Valkyrie
            // add-on 0x146e90). The Void station holds the pose after its one-off first
            // key: at t 0 every part is at scale 1, from 50 ms the hull is x10.065 (about 10 km across), the size its
            // collision volumes (1001: spheres out to +-100 000 units) and the arrival 170 000-220 000 units out are made for.
            if (name == "station_void") PartAnimation.HoldAllAfterOneOff(go);
            else if ((layout.stationIndex == 101 && Session.CampaignMission >= 0x50) || (layout.alienOrbit && Story.Dlc1Won)) PartAnimation.HoldAllAtEnd(go);
            else if (layout.stationIndex == 101 || layout.alienOrbit) PartAnimation.HoldAll(go);
            return go;
        }

        /// <summary>The station's volumes (collision.json) and the visible jumpgate's sphere (Obstacle): the player slides
        /// along them (PlayerEgo::calcCollision) and NPC fighters turn away from them (PlayerFighter::update, the first
        /// landmark's volumes). The flight level and the menu backdrop.</summary>
        public static void AddObstacles(OrbitLayout layout, GameObject station, GameObject jumpgate)
        {
            const float M = OrbitLayout.MetersPerUnit;
            if (station != null)
            {
                var o = station.AddComponent<GoF2Remake.Flight.Obstacle>();
                o.landmark = o.isStation = true;
                var own = Modding.ModWorld.ModelOf(layout.stationIndex);
                o.volumes = own != null && Modding.ModStations.Built(own.station) ? Modding.ModStations.Volumes(own, station)   // a mod's model
                          : GoF2Remake.Flight.CollisionVolume.ForStation(StationLook(layout.stationIndex), layout.systemIndex < 0,
                                                                         layout.alienOrbit && Story.Dlc1Won);
                // PlayerStation+0x150: the transform's bounding radius (Transform+0xe0, about the station's own origin)
                // + 5000 units. A radius from the origin, not the bounds' half size: a lopsided station (Tornard, 57,
                // towers 3.4 km out on one side) had its far tower outside the cube, so nothing collided there.
                var pos = station.transform.position;
                var b = new Bounds(pos, Vector3.zero);
                foreach (var r in station.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float radius = 0f;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) != 0 ? b.max.x : b.min.x, (c & 2) != 0 ? b.max.y : b.min.y, (c & 4) != 0 ? b.max.z : b.min.z);
                    radius = Mathf.Max(radius, (corner - pos).magnitude);
                }
                o.cubeHalf = radius + 5000f * M;
            }
            if (jumpgate != null)
            {
                var o = jumpgate.AddComponent<GoF2Remake.Flight.Obstacle>();
                o.landmark = o.cubeIsContact = true;
                o.cubeHalf = layout.JumpgateRadius * M;
                o.volumes.Add(GoF2Remake.Flight.CollisionVolume.Sphere(Vector3.zero, layout.JumpgateRadius * M));
            }
        }

        public static GameObject SpawnJumpgate(Database db, OrbitLayout layout, Transform parent = null)
        {
            if (!layout.hasJumpgate) return null;
            return Spawn(db, layout.JumpgateAssembly, layout.jumpgate, OrbitLayout.RotationToUnity(new Vector3(0f, Mathf.PI, 0f)), "Jumpgate", parent);
        }

        /// <summary>
        /// Level::createAsteroids / PlayerAsteroid. The first 2..9 are big (cube +-30000, scale 1.2..2.19, no spin), the
        /// rest small (cube +-50000, scale 0.3..0.99, 0.1 rad/s on random axes). 'reject' (Unity position) re-rolls a
        /// spot, e.g. to keep the menu camera's orbit clear.
        /// </summary>
        public static Transform SpawnAsteroids(Database db, OrbitLayout layout, Transform parent = null, Func<Vector3, bool> reject = null)
        {
            var prefab = AssembledObject.LoadPrefab(db.AssemblyByName(layout.AsteroidAssembly));
            var explosion = AssembledObject.LoadPrefab(db.AssemblyByName(layout.AsteroidAssembly + "_explosion_anim"));
            var destroyedSound = GoF2Remake.Flight.CombatAudio.Load()?.asteroidDestroyed;
            // Remake: Explosion types 2 and 3 share the billboard 0x4213 and its purple fragments (Void Crystals); the ordinary
            // asteroids get a rock-coloured copy (Reference/tools/combat/make_rock_explosion.py).
            var explosionTexture = layout.asteroidType == 0 ? Resources.Load<Texture2D>("GoF2Combat/asteroid_explosion_rock") : null;
            var root = new GameObject("Asteroids").transform;
            root.SetParent(parent, false);
            if (prefab == null) return root;
            int big = Random.Range(2, 10);
            var bigPositions = new Vector3[big];
            var ores = OreProbabilities(db, layout);
            int oreCursor = 0;
            // Level::createAsteroids: a Novanium asteroid is type 3, mesh 18836 sn_asteroid_magma with its own explosion
            // (space_props.md, table UNK_00251fb0), whatever the orbit's own type; the same mesh radius as asteroid_01.
            GameObject magma = null, magmaExplosion = null;
            if (ores.Exists(o => o.item == Novanium))
            {
                magma = AssembledObject.LoadPrefab(db.AssemblyByName("sn_asteroid_magma"));
                magmaExplosion = AssembledObject.LoadPrefab(db.AssemblyByName("sn_asteroid_magma_explosion_anim"));
            }
            for (int i = 0; i < layout.asteroidCount; i++)
            {
                bool isBig = i < big;
                float side = isBig ? 60000f : 100000f;
                Vector3 pos;
                int tries = 0;
                bool bad;
                do
                {
                    pos = layout.asteroidCentre + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)) * side;
                    bad = (isBig && TooClose(pos, bigPositions, i))   // intended rule: big ones 8000 apart
                          || (reject != null && reject(OrbitLayout.ToUnity(pos)));
                } while (bad && ++tries < 40);
                if (bad && reject != null) continue;   // no valid spot: leave it out rather than block the view
                if (isBig) bigPositions[i] = pos;

                float scale = isBig ? Random.Range(120, 220) * 0.01f : Random.Range(30, 100) * 0.01f;
                int ore = PickOre(ores, ref oreCursor);
                // Quality from the scale; the big ones (and the largest small ones) are 50 % A, else D..B.
                int quality = scale < 0.4f ? 4 : scale < 0.7f ? 5 : scale < 0.92f ? 6 : Random.Range(0, 2) == 0 ? 7 : 4 + Random.Range(0, 3);
                var euler = new Vector3(Random.Range(0, 100), Random.Range(0, 100), Random.Range(0, 100)) * 0.01f * 2f * Mathf.PI;
                bool isMagma = ore == Novanium && magma != null;
                var model = isMagma ? magma : prefab;
                var go = Object.Instantiate(model, OrbitLayout.ToUnity(pos), OrbitLayout.RotationToUnity(euler), root);
                go.name = $"Asteroid {i}";
                go.transform.localScale = model.transform.localScale * scale;
                // PlayerAsteroid: hit radius = meshRadius * scale * 0.7, HP = scale * 100 + 30.
                var target = go.AddComponent<GoF2Remake.Flight.Target>();
                target.isAsteroid = true;
                target.radius = (isMagma ? MagmaMeshRadius : layout.AsteroidMeshRadius) * scale * 0.7f * M;
                target.maxHp = target.hp = scale * 100f + 30f;
                target.explosionPrefab = isMagma ? magmaExplosion : explosion;
                target.explosionScale = scale;
                target.explosionTexture = isMagma ? null : explosionTexture;
                target.destroyedSound = destroyedSound;
                target.oreItem = ore;
                target.quality = quality;
                target.scale = scale;
                float spin = 1f - Mathf.Clamp(scale, 0.9f, 1f);   // 0.1 rad/s for small, none for big
                if (spin > 0f)
                {
                    var axes = new Vector3(Random.Range(-1, 2), Random.Range(-1, 2), Random.Range(-1, 2));
                    go.AddComponent<Spin>().degreesPerSecond = new Vector3(-axes.x, -axes.y, axes.z) * spin * Mathf.Rad2Deg;
                }
            }
            return root;
        }

        /// <summary>Galaxy::getAsteroidProbabilities 0x1a4fb0: per ore 154..163, p = 100 - distance(system, the ore's cheapest
        /// system), 0 below 50; Void Crystals (164) appended with 0 (100 in an alien orbit); sorted descending (stable),
        /// then p[k] -= 2k for the positive ones.</summary>
        public static List<(int item, int p)> OreProbabilities(Database db, OrbitLayout layout)
        {
            var list = new List<(int item, int p)>();
            bool alien = layout.systemIndex < 0;
            for (int item = 154; item <= 163; item++)
            {
                var it = db.Item(item);
                int p = alien || it == null ? 0 : 100 - Shop.Distance(db, layout.systemIndex, it.lowestPriceSystem);
                list.Add((item, p < 50 ? 0 : p));
            }
            list.Add((164, alien ? 100 : 0));
            var sorted = list.Select((e, i) => (e, i)).OrderByDescending(x => x.e.p).ThenBy(x => x.i).Select(x => x.e).ToList();
            for (int k = 0; k < sorted.Count; k++) if (sorted[k].p > 0) sorted[k] = (sorted[k].item, sorted[k].p - 2 * k);
            // The supernova orbit after step 0x59: every pair's ore becomes Novanium (0xd9), the chances stay.
            if (NovaniumOrbit(layout)) for (int k = 0; k < sorted.Count; k++) sorted[k] = (Novanium, sorted[k].p);
            return sorted;
        }

        public const int Novanium = 217;   // 0xd9; its core is 218 (Target.CoreItem)
        const float MagmaMeshRadius = 3547f;   // sn_asteroid_magma's bounding sphere, as asteroid_01's

        /// <summary>Galaxy::getAsteroidProbabilities 0x1a4fb0 with Status::inSupernovaOrbit 0xb9088 (station 0x6d, Naneroh)
        /// and getCurrentCampaignMission > 0x59: the orbit's asteroids are all Novanium (Ginoya's other stations keep their
        /// normal ores). The remake reads Session.WorldIndex, so the finished game's world (multiplayer) has it too.</summary>
        public static bool NovaniumOrbit(OrbitLayout layout) =>
            layout.systemIndex >= 0 && layout.stationIndex == 0x6d && Session.WorldIndex > 0x59;

        /// <summary>Level::createAsteroids 0xbd34a: a cursor walks the pairs; a roll under p takes that ore and moves on
        /// (wrapping after pair 5), a miss starts over at the top ore.</summary>
        public static int PickOre(List<(int item, int p)> ores, ref int k)
        {
            for (int guard = 0; guard < 10000; guard++)
            {
                if (Random.Range(0, 100) < ores[k].p)
                {
                    int item = ores[k].item;
                    k = k + 1 > 5 ? 0 : k + 1;
                    if (item < 164 || item == 217 || ores[0].item == 164) return item;
                }
                else k = 0;
            }
            return ores[0].item;
        }

        static bool TooClose(Vector3 p, Vector3[] others, int count)
        {
            for (int j = 0; j < count; j++) if ((others[j] - p).sqrMagnitude < 8000f * 8000f) return true;
            return false;
        }

        /// <summary>SET_STARS + SET_FOG around the camera (Level::initParticleSystems 0xcc990).</summary>
        public static void SpawnDust(OrbitLayout layout, Transform parent = null)
        {
            var stars = new GameObject("SpaceDust").AddComponent<SpaceDust>();
            stars.transform.SetParent(parent, false);
            stars.Build(Resources.Load<Material>($"{Backdrop.MaterialFolder}/space_particle"), 500, 20f, 60f, Color.white, 10000f, 5000f, 2000f);
            var fog = new GameObject("SpaceFog").AddComponent<SpaceDust>();
            fog.transform.SetParent(parent, false);
            string fogTex = layout.systemTexture == 12 ? "v_fog_ice" : "fog";
            fog.Build(Resources.Load<Material>($"{Backdrop.MaterialFolder}/{fogTex}"), 15, 10000f, 10000f, layout.dustFogTint, 10000f, 5000f, 1000f);
        }

        public static Backdrop SpawnBackdrop(OrbitLayout layout, Camera camera, Transform parent = null)
        {
            var backdrop = new GameObject("Backdrop").AddComponent<Backdrop>();
            backdrop.transform.SetParent(parent, false);
            backdrop.Build(layout, camera);
            return backdrop;
        }
    }
}
