// EngineFx.cs
// Remake mods: Galaxy on Fire 3's engine effect at a mod ship's exhausts (ships.json "engineFx", Data.CustomEngineFx), in
// place of the glow disc and the exhaust particles. GoF3 puts two things at each engine helper (its data.pdb's
// EngineAttachment and VfxList, e.g. NivelianEngine = /assets/vfx/engines/nivelian/vfx_engine_nivelian_000.ae4scene and
// TrailNivelian = particles/ship_engine_nivelian.psb):
//   - the flare ("glow"): GoF3's glow_mesh, two camera-facing quads (a_pos2dOffset): a 9 x 9 unit square of the whole
//     texture (the hot star) and a 27 x 2.7 streak at half brightness with the texture turned a quarter (its vertical
//     middle stretched sideways), its AngleBlend dimming it seen from the front;
//   - the cone ("cone"): four crossed cards along the exhaust (GoF3's exhaust mesh: radius 1.62, rings at 0 / 1.73 / 6.91 /
//     12.09 units with vertex colours 0 / 0.77 / 0.2 / 0, u 0.39 -> 0.69 along, v 0 -> 1 across), here as long as the
//     throttle; an engine's own rings by "coneProfile" (the Terran / Vossk exhaust meshes), and "coneFade" its material's
//     AngleBlend: each card fades as it turns edge-on to the camera (FadeCards), so from straight behind only the flares show;
//   - the stream ("trail"): GoF3's Fork particle trail of stretched streaks (beam_001.bmp) that wander off sideways, so
//     each stream wavers like lightning and bends when the ship turns: here 'trailCount' camera-facing ribbons per exhaust,
//     points emitted at the nozzle that keep the ship's velocity (the stream stays with the ship) plus their own speed
//     backward (trailLength / trailTime at full throttle) and a random sideways drift that grows with their age; the ribbon
//     takes the streak texture's middle row across it (a continuous stream; whole copies per segment read as beads) and
//     each point a random brightness, as the overlapping streaks give GoF3's streams their flicker.
// Each exhaust blows along its mount's "rotation" (ModHardpoints.MountTurn: GoF3's engine helper turned, the Vossk hulls'
// engines 8.5-10 deg outward) and is sized by its "fxScale".
//   - the helix ("spiral"): GoF3's spiral, two ribbon strands winding round the exhaust (radius 0.5 -> 0.1 units over 10.5,
//     bright down the middle, fading toward the end) that its animation spins, a thin bolt running along each (GoF3 draws a
//     beam through a flow map; its files name the textures by GUID only: the mod gives vfx_generic_electricity_000, the
//     lightning crackling in each strand): here 'spiralCount' camera-facing strands per exhaust on a helix that spins, the
//     texture scrolling along them, as long as the throttle like the cone.
// Level: EngineDrive like EngineFlame (0 idle .. 1 full throttle .. 2 a full boost; off while the engine glow part is, a
// quarter-second fade; in the docked station capped at full throttle). Every part is under the engine glow part, so it goes
// with the engines; the trails' points are kept in world space and written into their meshes in the part's space.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class EngineFx : MonoBehaviour
    {
        const float M = 0.05f;              // metres per game unit
        const float FadePerSecond = 4f;
        const float Response = 6f;          // 1/s: how fast the effect follows the throttle
        const float EmitInterval = 1f / 60f;
        const int MaxPoints = 64;
        const float SwayEase = 0.12f;       // per emitted point: how fast a stream's sideways motion turns
        // The materials' _Glow (HDR for the bloom), per part: matched by eye against GoF3 (the Moornta in its ship viewer and
        // in flight), the trails the brightest, the cone a soft glow at the nozzle, the helix's bolts kept below white.
        const float GlowHdr = 0.75f, ConeHdr = 0.5f, TrailHdr = 1f, SpiralHdr = 0.45f;
        const int StrandPoints = 40;

        static readonly int ColorId = Shader.PropertyToID("_Color");

        // GoF3's exhaust cards (vfx_engine_nivelian_000_exhaust): rings along the length (share of it), their brightness and u;
        // the default when the engine gives no "coneProfile" of its own.
        static readonly float[] ConeRings = { 0f, 1.73f / 12.09f, 6.91f / 12.09f, 1f };
        static readonly float[] ConeBright = { 0f, 0.77f, 0.2f, 0f };
        static readonly float[] ConeU = { 0.39f, 0.44f, 0.56f, 0.69f };
        const int ConeCards = 4;

        [SerializeField] GameObject engineGlow;
        [SerializeField] List<Vector3> nozzles = new List<Vector3>();   // root space
        [SerializeField] List<float> scales = new List<float>();         // the mount's fxScale (an oval's mean)
        [SerializeField] List<Profile> profiles = new List<Profile>();   // each nozzle's outline (ModExhausts' normalized profile)

        [System.Serializable] sealed class Profile { public float[] r; }
        [SerializeField] List<Quaternion> turns = new List<Quaternion>();   // the mount's rotation (GoF3's engine helper's turn)
        [SerializeField] List<Color> colours = new List<Color>();
        // engineFx coneColor / trailColor / spiralColor: alpha 0 = not given (the engine's colour, 'colours')
        [SerializeField] Color flareTint, coneTint, trailTint, spiralTint;
        [SerializeField] Material glowMaterial, coneMaterial, trailMaterial, spiralMaterial;
        [SerializeField] Vector3 glowHalf;   // the square's half size, the streak's half width and half height
        [SerializeField] float glowIntensity, coneLength, coneHalfWidth, coneIntensity;
        [SerializeField] float[] coneRings, coneBright, coneU;   // the engine's own cone rings ("coneProfile"; null: the default)
        [SerializeField] Vector2 coneFade;                        // "coneFade" |N.V| from .. to (to <= 0: none)
        [SerializeField] int trailCount;
        [SerializeField] float trailLength, trailHalfWidth, trailWander, trailTime, trailIntensity;
        [SerializeField] int spiralCount;
        [SerializeField] float spiralLength, spiralRadius0, spiralRadius1, spiralHalfWidth, spiralTurns, spiralSpin, spiralScroll, spiralRepeat, spiralIntensity;

        class Point { public Vector3 pos, vel, drift; public float age, bright; }
        class Trail
        {
            public readonly List<Point> points = new List<Point>();
            public Mesh mesh;
            public MeshRenderer renderer;
            public int engine;
            public float emitTimer;
            public Vector3 sway;   // the sideways motion the next point takes: it wanders smoothly, so the stream undulates
            public Vector2 start;  // where on the nozzle it leaves (its plane, in its half sizes: an oval's streams spread along it)
        }

        readonly List<(Transform t, MeshRenderer r, int engine)> flares = new List<(Transform, MeshRenderer, int)>();
        readonly List<(Transform t, MeshRenderer r, int engine, Mesh mesh)> cones = new List<(Transform, MeshRenderer, int, Mesh)>();
        Color[] coneBase;                      // the cone mesh's own vertex colours (the edge-on fade scales each card's)
        readonly List<Color> coneColours = new List<Color>();
        readonly List<Trail> trails = new List<Trail>();
        readonly List<(Mesh mesh, MeshRenderer r, int engine, int strand)> strands = new List<(Mesh, MeshRenderer, int, int)>();
        readonly List<Vector3> rpos = new List<Vector3>();
        readonly List<float> rwidth = new List<float>(), rv = new List<float>();
        readonly List<Color> rcol = new List<Color>();
        float spinTime;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> vcolours = new List<Color>();
        readonly List<int> tris = new List<int>();
        MaterialPropertyBlock block;
        EngineDrive drive;
        bool ready, inStation;
        float thrust = -1f, boost, level = -1f;
        Vector3 lastPos, velocity;
        bool hasLast;

        /// <summary>Sets it up on a ship template: the parts under 'glowPart' (the engine glow part: they go with the engines),
        /// one per exhaust mount of 'c'; the materials made here (copies of ModAssets.fxAdditive with the mod's PNGs).</summary>
        public static void Add(GameObject root, GameObject glowPart, CustomShipData c, ModInfo mod)
        {
            if (c?.engineFx == null || c.mounts == null) return;
            Create(root, glowPart, c.engineFx, c.mounts, mod, c.assembly ?? c.name, m => m.fxScale > 0f ? m.fxScale : 1f,
                   m => m.glowColor != null && m.glowColor.Length >= 3 ? m.glowColor : c.engineGlowColor);
        }

        /// <summary>The effect 'fx' at every exhaust mount (slotType 3) of 'mounts' (ship-relative game units, as the host's
        /// own frame: 'host' is the ship's root, or a child of the engine glow part aligned with it, ModExhausts), its parts
        /// under 'glowPart'. 'scaleOf' sizes each exhaust, 'profileOf' gives its nozzle's outline (ModExhausts' normalized
        /// profile: the cone's cards and the helix follow it, the trails spread along its long side; null = round), 'centreOf'
        /// moves it in its mount's plane (Unity metres), 'colourOf' its colour (null: fx.color); 'main' / 'second' (a
        /// customized exhaust's colours, ModExhausts) replace the flare and cone colour / the trails' and helix's. 'materials'
        /// (by part name) are reused across ships when given (made once there).</summary>
        public static EngineFx Create(GameObject host, GameObject glowPart, CustomEngineFx fx, IEnumerable<WeaponMount> mounts, ModInfo mod, string label,
                                      System.Func<WeaponMount, float> scaleOf, System.Func<WeaponMount, float[]> colourOf = null,
                                      Color? main = null, Color? second = null, Dictionary<string, Material> materials = null,
                                      System.Func<WeaponMount, Vector2> centreOf = null, System.Func<WeaponMount, float[]> profileOf = null)
        {
            var additive = ModAssets.Get()?.fxAdditive;
            if (fx == null || host == null || glowPart == null || additive == null || mounts == null) return null;
            var e = host.AddComponent<EngineFx>();
            e.engineGlow = glowPart;
            Color fallback = fx.color != null && fx.color.Length >= 3 ? new Color(fx.color[0], fx.color[1], fx.color[2], 1f) : new Color(0.22f, 0.86f, 1f, 1f);
            foreach (var m in mounts)
            {
                if (m == null || m.slotType != 3) continue;
                var g = colourOf?.Invoke(m);
                var turn = ModHardpoints.MountTurn(m);
                var centre = centreOf?.Invoke(m) ?? Vector2.zero;
                e.nozzles.Add(WeaponSystem.MountToLocal(m) + turn * new Vector3(centre.x, centre.y, 0f));
                e.scales.Add(Mathf.Max(0.01f, scaleOf != null ? scaleOf(m) : 1f));
                e.profiles.Add(new Profile { r = profileOf?.Invoke(m) });
                e.turns.Add(turn);
                e.colours.Add(main ?? (g != null && g.Length >= 3 ? new Color(g[0], g[1], g[2], 1f) : fallback));
            }
            Color Tint(float[] rgb) => rgb != null && rgb.Length >= 3 ? new Color(rgb[0], rgb[1], rgb[2], 1f) : Color.clear;
            e.flareTint = main ?? Tint(fx.flareColor);
            e.coneTint = main ?? Tint(fx.coneColor);
            e.trailTint = second ?? Tint(fx.trailColor);
            e.spiralTint = second ?? Tint(fx.spiralColor);
            Material Made(string file, string part, bool repeatV, float hdr)
            {
                if (string.IsNullOrEmpty(file)) return null;
                if (materials != null && materials.TryGetValue(part, out var have) && have != null) return have;
                var tex = ModMaterials.Texture(mod, file, false);
                if (tex == null) { Debug.LogWarning($"Mods: {mod?.Id}: \"{label}\" engineFx: {file} isn't a PNG / JPG the game can read"); return null; }
                tex.wrapModeU = TextureWrapMode.Clamp;
                tex.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var mat = ModMaterials.CopyOf(additive, $"{label} engine {part}");
                mat.SetTexture("_MainTex", tex);
                mat.SetTextureScale("_MainTex", Vector2.one);
                mat.SetTextureOffset("_MainTex", Vector2.zero);
                mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_Glow")) mat.SetFloat("_Glow", hdr);
                if (materials != null) materials[part] = mat;
                return mat;
            }
            e.glowMaterial = Made(fx.glow, "glow", false, GlowHdr);
            e.coneMaterial = Made(fx.cone, "cone", false, ConeHdr);
            e.trailMaterial = Made(fx.trail, "trail", false, TrailHdr);
            var gs = fx.glowSize;
            e.glowHalf = new Vector3(gs != null && gs.Length >= 1 ? gs[0] : 240f, gs != null && gs.Length >= 2 ? gs[1] : 720f,
                                     gs != null && gs.Length >= 3 ? gs[2] : (gs != null && gs.Length >= 2 ? gs[1] * 0.1f : 72f)) * M;
            e.glowIntensity = fx.glowIntensity;
            e.coneLength = fx.coneLength * M;
            e.coneHalfWidth = fx.coneWidth * M;
            e.coneIntensity = fx.coneIntensity;
            var cp = fx.coneProfile;
            if (cp != null && cp.Length >= 6 && cp.Length % 3 == 0)
            {
                int n = cp.Length / 3;
                e.coneRings = new float[n]; e.coneBright = new float[n]; e.coneU = new float[n];
                for (int k = 0; k < n; k++) { e.coneRings[k] = cp[3 * k]; e.coneBright[k] = cp[3 * k + 1]; e.coneU[k] = cp[3 * k + 2]; }
            }
            if (fx.coneFade != null && fx.coneFade.Length >= 2 && fx.coneFade[1] > fx.coneFade[0])
                e.coneFade = new Vector2(Mathf.Max(0f, fx.coneFade[0]), fx.coneFade[1]);
            e.trailCount = Mathf.Clamp(fx.trailCount, 0, 4);
            e.trailLength = fx.trailLength * M;
            e.trailHalfWidth = fx.trailWidth * M;
            e.trailWander = fx.trailWander * M;
            e.trailTime = Mathf.Max(0.05f, fx.trailTime);
            e.trailIntensity = fx.trailIntensity;
            e.spiralMaterial = Made(fx.spiral, "spiral", true, SpiralHdr);
            e.spiralCount = Mathf.Clamp(fx.spiralCount, 0, 4);
            e.spiralLength = fx.spiralLength * M;
            var sr = fx.spiralRadius;
            e.spiralRadius0 = (sr != null && sr.Length >= 1 ? sr[0] : 27f) * M;
            e.spiralRadius1 = (sr != null && sr.Length >= 2 ? sr[1] : 5f) * M;
            e.spiralHalfWidth = fx.spiralWidth * M;
            e.spiralTurns = fx.spiralTurns;
            e.spiralSpin = fx.spiralSpin;
            e.spiralScroll = fx.spiralScroll;
            e.spiralRepeat = fx.spiralRepeat;
            e.spiralIntensity = fx.spiralIntensity;
            return e;
        }

        void OnEnable()
        {
            drive?.Reset();
            hasLast = false;
            foreach (var t in trails) t.points.Clear();
            inStation = FindAnyObjectByType<World.StationLevel>() != null;
        }

        readonly List<Mesh> owned = new List<Mesh>();

        void OnDestroy()
        {
            foreach (var m in owned) if (m != null) Destroy(m);
        }

        void Setup()
        {
            ready = true;
            block = new MaterialPropertyBlock();
            drive = new EngineDrive(transform);
            if (engineGlow == null) return;
            var parent = engineGlow.transform;
            Mesh quad = glowMaterial != null ? Flare() : null;
            if (quad != null) owned.Add(quad);
            bool fading = coneFade.y > 0f;
            Mesh cone = coneMaterial != null ? Cone() : null;
            if (cone != null) { owned.Add(cone); coneBase = cone.colors; }
            for (int i = 0; i < nozzles.Count; i++)
            {
                var at = transform.TransformPoint(nozzles[i]);
                if (glowMaterial != null)
                {
                    var t = Child(parent, $"engine_fx_glow_{i}", quad, glowMaterial, at, out var r);
                    t.localScale = Vector3.one * (scales[i] / Mathf.Max(1e-4f, parent.lossyScale.x));
                    flares.Add((t, r, i));
                }
                if (coneMaterial != null)
                {
                    var mesh = cone;
                    // its cards reach the nozzle's outline; each fading cone its own mesh (the cards' colours per view)
                    if (Shaped(i) || fading) { mesh = Cone(Shaped(i) ? i : -1); owned.Add(mesh); if (fading) mesh.MarkDynamic(); }
                    var t = Child(parent, $"engine_fx_cone_{i}", mesh, coneMaterial, at, out var r);
                    t.rotation = transform.rotation * turns[i] * Quaternion.LookRotation(Vector3.back, Vector3.up);   // +z of the cone = backward
                    cones.Add((t, r, i, mesh));
                }
                if (spiralMaterial != null)
                    for (int k = 0; k < spiralCount; k++)
                    {
                        var mesh = new Mesh { name = $"engine_fx_spiral_{i}_{k}" };
                        mesh.MarkDynamic();
                        owned.Add(mesh);
                        var t = Child(parent, mesh.name, mesh, spiralMaterial, parent.position, out var r);
                        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                        strands.Add((mesh, r, i, k));
                    }
                if (trailMaterial != null)
                    for (int k = 0; k < trailCount; k++)
                    {
                        var mesh = new Mesh { name = $"engine_fx_trail_{i}_{k}" };
                        mesh.MarkDynamic();
                        owned.Add(mesh);
                        var t = Child(parent, mesh.name, mesh, trailMaterial, parent.position, out var r);
                        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                        // A long nozzle's streams leave spread along its long side (a slot's along the slot).
                        float wide = Factor(i, 0f) + Factor(i, Mathf.PI), tall = Factor(i, 0.5f * Mathf.PI) + Factor(i, 1.5f * Mathf.PI);
                        float along = trailCount > 1 && Mathf.Max(wide, tall) > 1.3f * Mathf.Min(wide, tall) ? (2f * k / (trailCount - 1) - 1f) * 0.6f : 0f;
                        var start = wide >= tall ? new Vector2(along, 0f) : new Vector2(0f, along);
                        trails.Add(new Trail { mesh = mesh, renderer = r, engine = i, start = start, emitTimer = Random.value * EmitInterval });
                    }
            }
        }

        static Transform Child(Transform parent, string name, Mesh mesh, Material mat, Vector3 at, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>GoF3's glow_mesh in metres: the square (the whole texture, brightness 1) and the streak (brightness 0.5, the
        /// texture turned a quarter: u across the streak, v along it), both faces.</summary>
        Mesh Flare()
        {
            float s = glowHalf.x, w = glowHalf.y, h = glowHalf.z;
            var half = new Color(0.5f, 0.5f, 0.5f, 1f);
            var m = new Mesh { name = "engine_fx_glow" };
            m.SetVertices(new[] { new Vector3(-s, -s, 0), new Vector3(s, -s, 0), new Vector3(s, s, 0), new Vector3(-s, s, 0),
                                  new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0) });
            m.SetUVs(0, new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1),
                                new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1) });
            m.SetColors(new[] { Color.white, Color.white, Color.white, Color.white, half, half, half, half });
            var tri = new List<int>();
            for (int q = 0; q < 8; q += 4) tri.AddRange(new[] { q, q + 2, q + 1, q, q + 3, q + 2, q, q + 1, q + 2, q, q + 2, q + 3 });
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>GoF3's exhaust: four cards through the axis (0, 45, 90, 135 deg), along +z, 1 long and 1 wide (half), in
        /// its own space; the transform scales it to the cone's size.</summary>
        Mesh Cone(int nozzle = -1)
        {
            var m = new Mesh { name = "engine_fx_cone" };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tr = new List<int>();
            // The engine's own rings only when they were given: Instantiate copies a null serialized array as an empty one,
            // so every ship (a copy of its template) without a "coneProfile" had no rings and no cone.
            bool own = coneRings != null && coneRings.Length >= 2 && coneBright != null && coneBright.Length == coneRings.Length
                       && coneU != null && coneU.Length == coneRings.Length;
            float[] rings = own ? coneRings : ConeRings, bright = own ? coneBright : ConeBright, us = own ? coneU : ConeU;
            for (int card = 0; card < ConeCards; card++)
            {
                float a = card * Mathf.PI / 4f;
                var side = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                int start = v.Count;
                for (int ring = 0; ring < rings.Length; ring++)
                    for (int s = 0; s < 2; s++)
                    {
                        // (the cone's x is the mount's -x: LookRotation backward)
                        var d = side * (s == 0 ? -1f : 1f);
                        float f = nozzle >= 0 ? Factor(nozzle, Mathf.Atan2(d.y, -d.x)) : 1f;
                        v.Add(d * f + Vector3.forward * rings[ring]);
                        uv.Add(new Vector2(us[ring], s));
                        col.Add(new Color(bright[ring], bright[ring], bright[ring], 1f));
                    }
                for (int ring = 0; ring < rings.Length - 1; ring++)
                {
                    int a0 = start + ring * 2, b0 = a0 + 2;
                    tr.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1, a0, a0 + 1, b0, a0 + 1, b0 + 1, b0 });   // both faces
                }
            }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tr, 0);
            m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            if (!ready) Setup();
            if (engineGlow == null || nozzles.Count == 0) return;
            float dt = Time.deltaTime;
            float d = drive.Sample(dt, engineGlow);
            if (inStation && !drive.Previewing) d = Mathf.Min(d, 1f);
            float t = Mathf.Clamp01(d), b = Mathf.Clamp01(d - 1f);
            if (thrust < 0f) { thrust = t; boost = b; }
            else
            {
                float k = 1f - Mathf.Exp(-dt * Response);
                thrust = Mathf.Lerp(thrust, t, k);
                boost = Mathf.Lerp(boost, b, k);
            }
            bool on = engineGlow.activeInHierarchy && !drive.Off;
            level = level < 0f ? (on ? 1f : 0f) : Mathf.MoveTowards(level, on ? 1f : 0f, Time.unscaledDeltaTime * FadePerSecond);

            var p = transform.position;
            if (hasLast && dt > 0f)
            {
                var v = (p - lastPos) / dt;
                if (v.magnitude < 2000f) velocity = v;   // (a jump or respawn moves the ship in one frame)
            }
            lastPos = p; hasLast = true;

            var cam = Camera.main;
            float power = level * (0.35f + 0.65f * thrust) * (1f + 0.5f * boost);
            foreach (var (ft, r, i) in flares)
            {
                if (ft == null) continue;
                float face = 1f;
                if (cam != null)
                {
                    ft.rotation = Quaternion.LookRotation(ft.position - cam.transform.position, cam.transform.up);
                    float along = Vector3.Dot((cam.transform.position - ft.position).normalized, Back(i));   // 1 = seen from behind
                    face = 0.3f + 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.7f, along));
                }
                SetColour(r, Tinted(flareTint, i) * (glowIntensity * power * face));
            }
            float coneScale = Mathf.Lerp(0.3f, 1f, thrust) * (1f + 0.3f * boost);
            foreach (var (ct, r, i, mesh) in cones)
            {
                if (ct == null) continue;
                float lossy = Mathf.Max(1e-4f, ct.parent.lossyScale.x);
                ct.localScale = new Vector3(coneHalfWidth, coneHalfWidth, coneLength * coneScale) * (scales[i] / lossy);
                SetColour(r, Tinted(coneTint, i) * (coneIntensity * level * (0.5f + 0.5f * thrust)));
                if (coneFade.y > 0f && cam != null) FadeCards(ct, mesh, cam.transform.position);
            }
            if (trails.Count > 0) UpdateTrails(dt, cam, power);
            if (strands.Count > 0) UpdateStrands(dt, cam, power, coneScale);
        }

        /// <summary>GoF3's AngleBlend on the exhaust cards: each card's vertex colours x smoothstep(from, to, |N.V|), so a card
        /// seen edge-on (from straight behind: every card contains the axis) vanishes and the flare alone shows.</summary>
        void FadeCards(Transform cone, Mesh mesh, Vector3 eye)
        {
            if (mesh == null || coneBase == null) return;
            var view = (eye - cone.position).normalized;
            int perCard = coneBase.Length / ConeCards;
            coneColours.Clear();
            for (int card = 0; card < ConeCards; card++)
            {
                float a = card * Mathf.PI / ConeCards;
                // the card spans (cos a, sin a, 0) and +z: its normal; the cone's x and y are scaled alike, so it holds
                var n = cone.rotation * new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coneFade.x, coneFade.y, Mathf.Abs(Vector3.Dot(n, view))));
                for (int k = 0; k < perCard; k++)
                {
                    var c = coneBase[card * perCard + k];
                    coneColours.Add(new Color(c.r * f, c.g * f, c.b * f, 1f));
                }
            }
            mesh.SetColors(coneColours);
        }

        /// <summary>The helix: each strand's points along the exhaust (its own frame: +z backward, the mount's turn), spun about
        /// the axis, tapering, the texture scrolled along it; drawn facing the camera.</summary>
        void UpdateStrands(float dt, Camera cam, float power, float lengthScale)
        {
            spinTime += dt;
            foreach (var (mesh, r, i, k) in strands)
            {
                if (mesh == null) continue;
                if (cam == null || power <= 0.01f) { mesh.Clear(); continue; }
                float size = scales[i], length = spiralLength * lengthScale * size;
                var frame = transform.rotation * turns[i] * Quaternion.LookRotation(Vector3.back, Vector3.up);
                var origin = transform.TransformPoint(nozzles[i]);
                float phase = (float)k / Mathf.Max(1, spiralCount) + spiralSpin * spinTime;
                float scroll = spiralScroll * spinTime + k * 0.37f;
                var c = Tinted(spiralTint, i) * (spiralIntensity * power);
                rpos.Clear(); rwidth.Clear(); rcol.Clear(); rv.Clear();
                for (int j = 0; j < StrandPoints; j++)
                {
                    float s = j / (StrandPoints - 1f);
                    float a = 2f * Mathf.PI * (spiralTurns * s + phase);
                    float radius = Mathf.Lerp(spiralRadius0, spiralRadius1, s) * size;
                    // (the nozzle's outline: the helix runs along it; the frame's x is the mount's -x)
                    float f = Factor(i, Mathf.PI - a);
                    rpos.Add(origin + frame * new Vector3(Mathf.Cos(a) * radius * f, Mathf.Sin(a) * radius * f, s * length));
                    rwidth.Add(spiralHalfWidth * size * Mathf.Lerp(1f, 0.5f, s));
                    // GoF3's strands: about half bright at the nozzle, full a tenth along, then down to nothing at the end
                    // (squared: in GoF3's flight the crackle is gone well before the strands' full length).
                    float fade = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(s * 10f)) * (1f - s) * (1f - s);
                    rcol.Add(new Color(c.r * fade, c.g * fade, c.b * fade, 1f));
                    rv.Add(s * spiralRepeat - scroll);
                }
                Ribbon(mesh, r.transform, cam.transform.position);
            }
        }

        /// <summary>A camera-facing ribbon through rpos (rwidth half widths, rcol colours, rv the texture's v along it; u across)
        /// into 'mesh' in 'space', both faces.</summary>
        void Ribbon(Mesh mesh, Transform space, Vector3 eye)
        {
            verts.Clear(); uvs.Clear(); vcolours.Clear(); tris.Clear();
            int n = rpos.Count;
            for (int j = 0; j < n; j++)
            {
                var along = rpos[Mathf.Min(n - 1, j + 1)] - rpos[Mathf.Max(0, j - 1)];
                if (along.sqrMagnitude < 1e-8f) along = transform.forward;
                var side = Vector3.Cross(along, eye - rpos[j]).normalized * rwidth[j];
                verts.Add(space.InverseTransformPoint(rpos[j] - side));
                verts.Add(space.InverseTransformPoint(rpos[j] + side));
                uvs.Add(new Vector2(0f, rv[j])); uvs.Add(new Vector2(1f, rv[j]));
                vcolours.Add(rcol[j]); vcolours.Add(rcol[j]);
                int m = verts.Count;
                if (m >= 4) tris.AddRange(new[] { m - 4, m - 2, m - 3, m - 3, m - 2, m - 1, m - 4, m - 3, m - 2, m - 3, m - 1, m - 2 });   // both faces
            }
            mesh.Clear();
            if (n < 2) return;
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(vcolours); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void SetColour(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, new Color(c.r, c.g, c.b, 1f));
            r.SetPropertyBlock(block);
        }

        Color Tinted(Color tint, int i) => tint.a > 0f ? tint : colours[i];

        /// <summary>The way exhaust 'i' blows (world space): the ship's backward, turned by the mount.</summary>
        Vector3 Back(int i) => transform.rotation * (turns[i] * Vector3.back);

        /// <summary>Nozzle i's outline against its mean radius in a direction of its mount's plane (1: round).</summary>
        float Factor(int i, float angle) => i < profiles.Count ? ModExhausts.FactorOf(profiles[i]?.r, angle) : 1f;

        bool Shaped(int i) => i < profiles.Count && profiles[i]?.r != null;

        void UpdateTrails(float dt, Camera cam, float power)
        {
            float speed0 = trailLength / trailTime * Mathf.Lerp(0.3f, 1f, thrust) * (1f + 0.5f * boost);
            var right = transform.right; var up = transform.up;
            foreach (var tr in trails)
            {
                var pts = tr.points;
                float size = scales[tr.engine], speed = speed0 * size, wander = trailWander * size;
                var back = Back(tr.engine);
                // Age, move and drift: the sideways drift grows (a random walk), so each stream wanders more toward its end.
                for (int j = pts.Count - 1; j >= 0; j--)
                {
                    var q = pts[j];
                    q.age += dt;
                    if (q.age >= trailTime) { pts.RemoveAt(j); continue; }
                    var kick = Random.insideUnitSphere;
                    kick -= Vector3.Project(kick, q.vel);   // across the stream only
                    q.drift += kick * (wander * 0.25f * Mathf.Sqrt(dt) / (trailTime * Mathf.Sqrt(trailTime)));   // a little turbulence (a random walk: the same at any frame rate)
                    q.pos += (q.vel + q.drift) * dt;
                }
                if (power > 0.02f)
                {
                    tr.emitTimer -= dt;
                    float half = coneHalfWidth * scales[tr.engine];   // the effect's nozzle radius, sized like the cone
                    var off = tr.start * (half * Factor(tr.engine, Mathf.Atan2(tr.start.y, tr.start.x)));   // out to the outline
                    var nozzle = transform.TransformPoint(nozzles[tr.engine] + turns[tr.engine] * new Vector3(off.x, off.y, 0f));
                    while (tr.emitTimer <= 0f && pts.Count < MaxPoints)
                    {
                        tr.emitTimer += EmitInterval;
                        // The sway: an ease toward a new random sideways speed, so successive points move alike and the stream
                        // bends in smooth waves (each point on its own made a zigzag).
                        var target = (right * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * (wander / trailTime);
                        tr.sway = Vector3.Lerp(tr.sway, target, SwayEase);
                        pts.Add(new Point { pos = nozzle, vel = velocity + back * speed, drift = tr.sway, age = 0f, bright = Random.Range(0.7f, 1.15f) });
                    }
                    if (pts.Count > 0) pts[pts.Count - 1].pos = nozzle;   // the newest point stays on the nozzle
                }
                Build(tr, cam, power);
            }
        }

        void Build(Trail tr, Camera cam, float power)
        {
            var pts = tr.points;
            verts.Clear(); uvs.Clear(); vcolours.Clear(); tris.Clear();
            var mesh = tr.mesh;
            if (pts.Count < 2 || cam == null) { mesh.Clear(); return; }
            var space = tr.renderer.transform;
            var eye = cam.transform.position;
            var c = Tinted(trailTint, tr.engine) * (trailIntensity * power);
            float halfWidth = trailHalfWidth * scales[tr.engine];
            for (int j = pts.Count - 1; j >= 0; j--)   // newest (the nozzle) first
            {
                var q = pts[j];
                var next = pts[Mathf.Max(0, j - 1)].pos; var prev = pts[Mathf.Min(pts.Count - 1, j + 1)].pos;
                var along = next - prev;
                if (along.sqrMagnitude < 1e-8f) along = transform.forward;
                var side = Vector3.Cross(along, eye - q.pos).normalized;
                float life = q.age / trailTime;
                float w = halfWidth * Mathf.Lerp(1f, 0.35f, life);
                // In from the nozzle, out toward the end; each point its own brightness (GoF3's streaks overlap unevenly: the
                // stream flickers along its length).
                float fade = Mathf.Clamp01(life * 12f) * (1f - life) * (1f - life) * q.bright;
                verts.Add(space.InverseTransformPoint(q.pos - side * w));
                verts.Add(space.InverseTransformPoint(q.pos + side * w));
                uvs.Add(new Vector2(0f, 0.5f)); uvs.Add(new Vector2(1f, 0.5f));   // the streak's middle across: one continuous stream
                var col = new Color(c.r * fade, c.g * fade, c.b * fade, 1f);
                vcolours.Add(col); vcolours.Add(col);
                int n = verts.Count;
                if (n >= 4) tris.AddRange(new[] { n - 4, n - 2, n - 3, n - 3, n - 2, n - 1, n - 4, n - 3, n - 2, n - 3, n - 1, n - 2 });   // both faces
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(vcolours); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
