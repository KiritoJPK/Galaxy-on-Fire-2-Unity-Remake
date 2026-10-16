// SkyReflection.cs
// The ambient light of the current skybox (remake): DynamicGI.UpdateEnvironment after a level sets RenderSettings.skybox.
// The reflection the hulls show (URP Lit's environment reflection): the system's reflection cubemap, like the original
// (MGame::OnInitialize / ModMainMenu::OnInitialize: TextureCreate(0x2efe + the system's textureIndex), 0x2f08 in the alien
// orbit, then PaintCanvas::ChangeCubeTexture; BumpRimCubeShader adds it to the hulls weighted by their specular map). The
// cubemaps are Resources/GoF2Reflections (Textures/*/cubemaps' cubemap_skybox_000..018, glossy convolution), set as
// Unity's custom environment reflection. In a station the last one bound stays (the original never binds the hangar
// cubemaps 0x2ef4..0x2ef7): the station's system's. There used to be a realtime ReflectionProbe of the sky here, but both
// quality levels have Realtime Reflection Probes off, so it never rendered (rendered, it would reflect the black space sky).

using UnityEngine;
using UnityEngine.Rendering;

namespace GoF2Remake.World
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // black: a DontSave cubemap kept for the run, made again when gone
    public static class SkyReflection
    {
        /// <summary>The ambient light from RenderSettings.skybox (call after changing it).</summary>
        public static void Update()
        {
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>The system's reflection cubemap as the environment reflection: 0x2efe + the system's texture index, the
        /// alien orbit 0x2f08 (index 10).</summary>
        public static void SetSystem(OrbitLayout layout)
        {
            if (layout == null) return;
            int index = layout.alienOrbit ? 10 : layout.systemTexture;
            string name = index <= 10 ? $"cubemap_skybox_{index:000}" : index <= 14 ? $"v_cubemap_skybox_{index:000}" : $"sn_cubemap_skybox_{index:000}";
            var cube = Resources.Load<Cubemap>("GoF2Reflections/" + name);
            Shader.SetGlobalFloat(ReflectionId, 1f);
            Shader.SetGlobalColor(RimId, RimColor(layout));
            if (cube == null) return;
            // Remake: also as a global of its own, which GoF2/Hull and GoF2/EmissiveCube sample. unity_SpecCube0 (the custom
            // environment reflection) came out empty in the Android build (Forward on the Mobile renderer, per-object probe
            // data), so the hulls had no reflection there; the PC's Forward+ fills it.
            Shader.SetGlobalTexture(CubeId, cube);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
        }

        /// <summary>Remake: in a hangar its own race's cubemap (Resources/GoF2Reflections/cubemap_hangar_*, the original's
        /// unused 0x2ef4..0x2ef7) instead of the system's: Terran, Vossk, Nivelian, Midorian (StationTables.HangarIndex
        /// 0..3, also in the Supernova systems). The Valkyrie hangars (7, 8) have none yet: a black cubemap, no reflection
        /// (the rim light stays). Call after SetSystem, which the lounge and space go back to.</summary>
        public static void SetHangar(int hangarIndex)
        {
            Cubemap cube;
            if (hangarIndex >= 0 && hangarIndex < HangarCubes.Length)
            {
                cube = Resources.Load<Cubemap>("GoF2Reflections/" + HangarCubes[hangarIndex]);
                if (cube == null) return;   // not found: the system's stays
            }
            else if (hangarIndex == 7 || hangarIndex == 8) cube = Black;
            else return;
            Shader.SetGlobalTexture(CubeId, cube);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
        }

        static readonly string[] HangarCubes = { "cubemap_hangar_terran", "cubemap_hangar_vossk", "cubemap_hangar_nivelian", "cubemap_hangar_midorian" };

        static Cubemap black;
        static Cubemap Black
        {
            get
            {
                if (black != null) return black;
                black = new Cubemap(1, TextureFormat.RGBA32, false) { name = "GoF2BlackReflection", hideFlags = HideFlags.DontSave };
                for (int f = 0; f < 6; f++) black.SetPixel((CubemapFace)f, 0, 0, Color.black);
                black.Apply(false, true);
                return black;
            }
        }

        static readonly int ReflectionId = Shader.PropertyToID("_GoF2HullReflection"), RimId = Shader.PropertyToID("_GoF2HullRimColor"),
            CubeId = Shader.PropertyToID("_GoF2ReflectionCube");

        /// <summary>StarSystem::initLight: LightSetRimColor(3 x DAT_00258d34[sysTex]): the sun colour table but for systems 11
        /// and 15 (GoF2/Hull's rim light).</summary>
        static Color RimColor(OrbitLayout layout)
        {
            var c = layout.systemTexture == 11 ? new Color(0.20f, 0.11f, 0.04f)
                  : layout.systemTexture == 15 ? new Color(0.50f, 0.26f, 0.12f)
                  : layout.sunColor;
            return new Color(c.r * 3f, c.g * 3f, c.b * 3f, 1f);
        }
    }
}
