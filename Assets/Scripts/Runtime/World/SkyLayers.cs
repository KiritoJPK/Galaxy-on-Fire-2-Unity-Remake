// SkyLayers.cs
// The extra sky layers of an orbit (Level::createSpace 0xbbba0 / renderBG 0xd43f0; Reference/research/space_backdrop.md,
// "Skybox layers"). Camera-centred meshes, world-aligned (identity model matrix, not rotated by R_sky; the supernova
// flares do take R_sky, the nebula's sun-aligned rotation: renderBG draws them with its matrix), drawn by
// GoF2/SkyLayer on the far plane in the original's order: the ring sky before the sun and planets, the rest after them.
//   planet ring sky   Status::inPlanetRingOrbit: stations 120, 126, 130, 132                         alpha
//   supernova flares  the supernova system (27), mission != 89 and < 158; the nasty texture from 106,
//                     animation x1.5 above 106                                                        additive
//   storms            Status::inStormOrbit: mission >= 90 and (system 27 or nebula texture 16 / 18);
//                     a new random rotation (3 x nextInt(65536), unseeded) each time the animation wraps   additive
//   asteroid belt     systems 24, 25, 26 (the effects setting is always on here)                     alpha + LIGHT0
// The storm / flare parts animate with their `extra` (opacity) and `v5_0` (UV scroll) channels (PartAnimation).
// Not built: the alien-orbit and prologue sky exceptions (their own levels handle the sky).

using GoF2Remake.Data;
using GoF2Remake.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoF2Remake.World
{
    public class SkyLayers : MonoBehaviour
    {
        const int RingQueue = 2899, FlaresQueue = 2904, StormsQueue = 2906, BeltQueue = 2907;

        Camera cam;
        Transform stormRoot;
        PartAnimation stormAnim;
        int stormLoops;

        public static bool InPlanetRingOrbit(int station) => station == 120 || station == 126 || station == 130 || station == 132;

        public static SkyLayers Spawn(OrbitLayout layout, Camera camera, Transform parent = null)
        {
            var go = new GameObject("Sky layers");
            go.transform.SetParent(parent, false);
            var layers = go.AddComponent<SkyLayers>();
            layers.Build(layout, camera);
            return layers;
        }

        void Build(OrbitLayout layout, Camera camera)
        {
            cam = camera;
            var a = SkyLayerAssets.Load();
            if (a == null) { Debug.LogWarning($"SkyLayers: missing Resources/{SkyLayerAssets.ResourcePath}"); return; }
            int mission = Session.WorldIndex;
            bool supernova = layout.systemIndex == 27;

            if (InPlanetRingOrbit(layout.stationIndex))
            {
                // Remake: the ring is a thin slab (its top and bottom faces 4.3 units apart, 76 under the camera): both faces
                // drawn, the two copies of the bands sat a little apart and their alpha blend changed with the triangles'
                // order (top first in some of the 64 segments, bottom first in others), short bright dashes along the
                // rings at the segments' edges. Only the face toward the camera is drawn now.
                var ring = Add(a.ringSky, a.ringSkyMaterial, RingQueue, 1f);
                if (ring != null)
                    foreach (var r in ring.GetComponentsInChildren<Renderer>(true))
                        if (r.sharedMaterial != null) r.sharedMaterial.SetFloat("_Cull", (float)CullMode.Back);
                PolarRing(ring);
            }
            if (supernova && mission != 89 && mission < 158)
            {
                var mat = mission < 106 ? a.flaresMaterial : a.flaresNastyMaterial;
                float speed = mission > 106 ? 1.5f : 1f;
                // Their loop is 1000 .. 60000 ms: the UV scroll runs one texture width over it (a seamless wrap); the first
                // second is a one-off fade from 100 to the steady 50.
                // Level::renderBG draws them with the nebula's matrix, R_sky included (in system 27 its +Y points at the
                // supernova), so the fire streams out of the supernova; only the ring sky and the storms reset to the bare
                // camera rotation.
                var skyRotation = OrbitBuilder.SkyRotation(layout);
                var f1 = Add(a.flares1, mat, FlaresQueue, speed);   // from 1000 ms: their first positive key (the default)
                var f2 = Add(a.flares2, mat, FlaresQueue + 1, speed);
                if (f1 != null) f1.rotation = skyRotation;
                if (f2 != null) f2.rotation = skyRotation;
            }
            // Remake multiplayer: no storms in the completed world (Session.CompletedWorld).
            if (mission >= 90 && !Session.CompletedWorld && (supernova || layout.systemTexture == 16 || layout.systemTexture == 18))
            {
                stormRoot = Add(a.storms, a.stormsMaterial, StormsQueue, 1f);   // from 33 ms (the default): every part at 100 before
                stormAnim = stormRoot != null ? stormRoot.GetComponentInChildren<PartAnimation>() : null;
                if (stormRoot != null) stormRoot.rotation = RandomRotation();
            }
            if (layout.systemIndex >= 24 && layout.systemIndex <= 26)
            {
                var belt = Add(a.asteroidBelt, a.asteroidBeltMaterial, BeltQueue, 1f);
                if (belt != null)
                {
                    // Blend 8 + light 0: lit by LIGHT0 (toward the sun, clamp(15 * sun colour, 0, 2)) plus the sun-colour ambient.
                    var block = new MaterialPropertyBlock();
                    var c = layout.sunColor;
                    block.SetVector("_LightDir", OrbitLayout.DirToUnity(layout.lightDirection).normalized);
                    block.SetColor("_LightColor", new Color(Mathf.Clamp(15f * c.r, 0f, 2f), Mathf.Clamp(15f * c.g, 0f, 2f), Mathf.Clamp(15f * c.b, 0f, 2f)));
                    block.SetColor("_Ambient", c);
                    foreach (var r in belt.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>'loopStartMs' 0 = the animation's own start (PartAnimation's default, the original's range start).</summary>
        Transform Add(GameObject prefab, Material mat, int queue, float speed, float loopStartMs = 0f)
        {
            if (prefab == null || mat == null) return null;
            var go = Instantiate(prefab, transform, false);
            go.transform.rotation = OrbitLayout.RotationToUnity(Vector3.zero);   // world-aligned: game identity
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var m = new Material(mat) { renderQueue = queue };
                // The mesh's vertex colours multiply in (the supernova flares' fade to the edges); meshes without any don't.
                var mf = r.GetComponent<MeshFilter>();
                m.SetFloat("_UseVertexColor", mf != null && mf.sharedMesh != null && mf.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) ? 1f : 0f);
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            foreach (var lg in go.GetComponentsInChildren<LODGroup>(true)) lg.enabled = false;
            foreach (var anim in go.GetComponentsInChildren<PartAnimation>(true))
            {
                anim.speed = speed;
                anim.loop = true;
                if (loopStartMs > 0f) anim.loopStartMs = loopStartMs;
                anim.applyMaterialChannels = true;
            }
            return go.transform;
        }

        /// <summary>Remake: the planet ring sky (sn_skybox_planet_ring_alpha) maps its bands by the distance to the ring's centre
        /// (GoF2/SkyLayer's _Polar): the mesh's two radii, 142 and 461.34 in the file (the inner 4/13 of the outer), the
        /// texture's quarter rings around its (1, 0) corner from 0.1757 to 1.0024 (the vertices' UVs). The centre and the outer
        /// radius come from the mesh's bounds (the ring is flat: its widest extent is the outer radius). The ring's inner and
        /// outer edges are transparent in the texture, so the 64-sided outline doesn't show.</summary>
        static void PolarRing(Transform ring)
        {
            if (ring == null) return;
            foreach (var r in ring.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                var m = r.sharedMaterial;
                if (mf == null || mf.sharedMesh == null || m == null || !m.HasProperty("_Polar")) continue;
                var b = mf.sharedMesh.bounds;
                float outer = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
                m.SetFloat("_Polar", 1f);
                m.SetVector("_PolarCenter", b.center);
                m.SetVector("_PolarRadii", new Vector4(outer * 4f / 13f, outer, 0.1757f, 1.0024f));
                m.SetVector("_PolarUV", new Vector4(1f, 0f, 0f, 0f));
            }
        }

        /// <summary>The storm layer's re-roll: three random Euler angles (the decompiler lost the arguments).</summary>
        static Quaternion RandomRotation() => OrbitLayout.RotationToUnity(new Vector3(
            Random.Range(0, 65536) / 65536f * 2f * Mathf.PI, Random.Range(0, 65536) / 65536f * 2f * Mathf.PI,
            Random.Range(0, 65536) / 65536f * 2f * Mathf.PI));

        // At infinity: camera-centred. Set right before each camera renders: a camera moved later in the frame (the chase /
        // cutscene cameras' LateUpdate) would otherwise leave the layers a frame behind, jittering at speed.
        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == cam) transform.position = camera.transform.position;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            transform.position = cam.transform.position;
            if (stormAnim != null && stormAnim.Loops != stormLoops)
            {
                stormLoops = stormAnim.Loops;
                stormRoot.rotation = RandomRotation();
            }
        }
    }
}
