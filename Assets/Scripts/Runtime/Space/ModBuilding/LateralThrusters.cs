// LateralThrusters.cs
// Remake mods: a mod ship's manoeuvring jets (mounts of slotType 4 with a 'direction'; EVERSPACE 2's SKT_LateralThruster
// sockets, which fire its P_Vehicle_LateralThruster): each a short jet blowing along its direction whenever the ship moves
// against it (strafing, rising, sinking, reversing), as EVERSPACE 2's do (checked in the game: strafe right fires the
// left-side jets, down the upward ones on the wings, reverse the forward-facing ones at the wing tips; turning fires none).
// Each jet is two crossed cards drawn by GoF2/ModEngineFlame as boost cards (UV1.y 0.25: the boost colour, stretched by
// _FlameBoost) plus a small glow disc facing along it (the spot seen end-on), in the boost colour of the ship's own flames
// (else EVERSPACE 2's TB_Default blue); a kit colour slot targeting the "lateral thruster" material ("property": "boost",
// EVERSPACE 2's lateral thruster colour) sets it instead, and the discs take the jets' colour. What the ship asks for: the player's free-flight keys (FlightModel.ThrustInput), in
// the original flight the strafe (StrafeVelocity) and the brake; any other copy (another player's ship, NPCs) its measured
// acceleration in its own frame. None in the docked station. The jets sit under the engine glow part, so they go with the
// engines (parked, mining, cutscenes) and the hangar heights and shadows leave them out.

using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoF2Remake.Modding
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class LateralThrusters : MonoBehaviour
    {
        const float M = 0.05f;
        const float StubLength = 0.8f;    // metres: the cards before stretching
        const float HalfWidth = 0.75f;
        const float JetLength = 5f;       // metres at full strength (EVERSPACE 2's ~1.4 m x the kit's 3.6)
        const float RiseRate = 14f, FallRate = 7f;   // 1/s
        const float AccelRef = 20f;       // m/s^2 of measured acceleration that counts as a full push (other ships)
        const float PreviewLevel = 0.35f; // each jet's strength while the Customize screen runs the engines

        static readonly int BoostId = Shader.PropertyToID("_FlameBoost");
        static readonly int DirId = Shader.PropertyToID("_FlameDir");
        static readonly int BoostColorId = Shader.PropertyToID("_BoostColor");
        static readonly Color DefaultColour = new Color(0.4f, 4.2f, 8f, 1f);   // EVERSPACE 2's TB_Default (linear 0.05 / 0.525 / 1) x 8

        [SerializeField] Renderer[] jets = new Renderer[0];
        [SerializeField] Transform[] discs = new Transform[0];
        [SerializeField] Vector3[] directions = new Vector3[0];   // ship space, where each jet blows

        /// <summary>Testing (the Editor's eval): overrides what every ship asks for while set.</summary>
        public static Vector3? TestInput;

        float[] levels;
        MaterialPropertyBlock block;
        ShipController ship;
        bool ready, inStation, hasColour;
        Color boostColour;
        Vector3 lastPosition, velocity, accel;
        bool hasLast;

        /// <summary>Adds the jets for the slotType-4 mounts to a ship template (none: nothing). 'parent' is the engine glow
        /// part when there is one.</summary>
        public static void Add(GameObject root, Transform parent, IEnumerable<WeaponMount> mounts)
        {
            var list = mounts?.Where(m => m != null && m.slotType == 4 && m.direction != null && m.direction.Length >= 3).ToList();
            var assets = ModAssets.Get();
            if (list == null || list.Count == 0 || assets?.engineFlame == null) return;
            var mat = new Material(assets.engineFlame) { name = "lateral thruster" };
            mat.SetColor(BoostColorId, DefaultColour);
            mat.SetFloat("_Length", JetLength);
            mat.SetFloat("_NoiseStrength", 0f);
            mat.SetFloat("_Fade", 1.6f);
            mat.SetFloat("_Taper", 0.55f);
            mat.SetFloat("_EdgeOnFade", 0.6f);
            mat.SetFloat("_Core", 1.5f);
            var cards = CardMesh();
            var disc = DiscMesh();
            var c = root.AddComponent<LateralThrusters>();
            var jets = new List<Renderer>(); var discs = new List<Transform>(); var dirs = new List<Vector3>();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                var d = new Vector3(-m.direction[0], m.direction[1], m.direction[2]).normalized;
                if (d == Vector3.zero) continue;
                var go = new GameObject("engine_glow_thruster_" + i);
                go.transform.SetParent(parent != null ? parent : root.transform, false);
                go.transform.localPosition = WeaponSystem.MountToLocal(m);
                go.transform.localRotation = Quaternion.LookRotation(d, Mathf.Abs(d.y) > 0.9f ? Vector3.forward : Vector3.up);
                go.AddComponent<MeshFilter>().sharedMesh = cards;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.localBounds = new Bounds(new Vector3(0f, 0f, JetLength * 0.5f), new Vector3(HalfWidth * 2f, HalfWidth * 2f, JetLength + StubLength));
                jets.Add(r);
                var dgo = new GameObject("engine_glow_thruster_disc_" + i);
                dgo.transform.SetParent(go.transform, false);
                dgo.AddComponent<MeshFilter>().sharedMesh = disc;
                var dr = dgo.AddComponent<MeshRenderer>();
                dr.sharedMaterial = assets.engineGlowTint != null ? assets.engineGlowTint : assets.engineGlow;
                dr.shadowCastingMode = ShadowCastingMode.Off;
                dr.receiveShadows = false;
                dgo.transform.localScale = Vector3.zero;
                discs.Add(dgo.transform);
                dirs.Add(d);
            }
            c.jets = jets.ToArray(); c.discs = discs.ToArray(); c.directions = dirs.ToArray();
        }

        /// <summary>Two crossed cards along +Z: UV0 u 0 at the nozzle .. 1 at the end, UV1 across with the boost kind.</summary>
        static Mesh CardMesh()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv1 = new List<Vector2>(); var t = new List<int>();
            foreach (var axis in new[] { Vector3.right, Vector3.up })
            {
                int b = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float u = k / 2, s = (k & 1) == 0 ? -1f : 1f;
                    v.Add(axis * HalfWidth * s + Vector3.forward * StubLength * u);
                    uv.Add(new Vector2(u, s < 0 ? 0f : 1f));
                    uv1.Add(new Vector2(s < 0 ? 0f : 1f, 0.25f));
                }
                t.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }
            var m = new Mesh { name = "lateral thruster cards" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetUVs(1, uv1); m.SetTriangles(t, 0);
            m.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList());
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The glow sprite's disc (ModShipBuilder's engine glow shape) facing along +Z, in the boost blue (vertex colours).</summary>
        static Mesh DiscMesh()
        {
            const int Segments = 10;
            var centre = new Vector2(0.46f, 0.947f);
            const float UvDisc = 0.0254f;
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { centre };
            var core = new Color(0.6f, 0.9f, 1f); var rim = new Color(0.05f, 0.525f, 1f);
            var col = new List<Color> { core }; var t = new List<int>();
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                v.Add(new Vector3(d.x, d.y, 0f)); uv.Add(centre + d * UvDisc); col.Add(rim);
                t.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % Segments, 0, 1 + (i + 1) % Segments, 1 + i });
            }
            var m = new Mesh { name = "lateral thruster glow" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        void OnEnable()
        {
            hasLast = false;
            inStation = FindAnyObjectByType<World.StationLevel>() != null;
        }

        void Setup()
        {
            ready = true;
            block = new MaterialPropertyBlock();
            levels = new float[jets.Length];
            ship = GetComponentInParent<ShipController>();
            // The jets' own colour when a colour slot set it, else the ship's own flames' boost colour, else the default.
            var own = jets.Length > 0 && jets[0] != null ? jets[0].sharedMaterial : null;
            if (own != null && own.GetColor(BoostColorId) == DefaultColour)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    var f = r.sharedMaterials.FirstOrDefault(EngineFlame.IsFlame);
                    if (f == null || jets.Contains(r)) continue;
                    boostColour = f.GetColor(BoostColorId);
                    hasColour = true;
                    break;
                }
            // The glow discs in that colour (their mesh's vertex colours: a pale core, the colour at the rim).
            var colour = hasColour ? boostColour : own != null ? own.GetColor(BoostColorId) : DefaultColour;
            float peak = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            if (peak > 0f && discs.Length > 0)
            {
                var rim = new Color(colour.r / peak, colour.g / peak, colour.b / peak, 1f);
                Mesh tinted = null;
                foreach (var d in discs)
                {
                    var mf = d != null ? d.GetComponent<MeshFilter>() : null;
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (tinted == null)
                    {
                        tinted = Instantiate(mf.sharedMesh);
                        var cols = new Color[tinted.vertexCount];
                        for (int i = 0; i < cols.Length; i++) cols[i] = i == 0 ? Color.Lerp(rim, Color.white, 0.6f) : rim;
                        tinted.colors = cols;
                    }
                    mf.sharedMesh = tinted;
                }
            }
        }

        /// <summary>What the ship asks for in its own frame (x right, y up, z forward; -1..1).</summary>
        Vector3 Wanted(float dt)
        {
            if (ship == null) ship = GetComponentInParent<ShipController>();
            if (ship != null && ship.Model != null)
            {
                hasLast = false;
                if (ship.FreeFlightActive && !ship.externalControl && !ship.inputLocked) return ship.Model.ThrustInput;
                float side = Mathf.Abs(ship.Model.StrafeVelocity) > 0.05f ? Mathf.Sign(ship.Model.StrafeVelocity) : 0f;
                return new Vector3(side, 0f, ship.Model.Braking ? -1f : 0f);
            }
            var p = transform.position;
            if (hasLast && dt > 0f)
            {
                var v = (p - lastPosition) / dt;
                if (v.magnitude < 2000f)
                {
                    float k = 1f - Mathf.Exp(-dt * 10f);
                    var a = (v - velocity) / dt;
                    velocity = Vector3.Lerp(velocity, v, k);
                    accel = Vector3.Lerp(accel, a, 1f - Mathf.Exp(-dt * 6f));
                }
            }
            lastPosition = p;
            hasLast = true;
            var local = transform.InverseTransformDirection(accel) / AccelRef;
            return new Vector3(Mathf.Clamp(local.x, -1f, 1f), Mathf.Clamp(local.y, -1f, 1f), Mathf.Clamp(local.z, -1f, 0f));
        }

        void LateUpdate()
        {
            if (!ready) Setup();
            if (jets.Length == 0) return;
            float dt = Time.deltaTime;
            var want = TestInput ?? (inStation ? Vector3.zero : Wanted(dt));
            // The hangar's Customize screen running the engines (Visuals.EnginePreview): every jet lit a little, as EVERSPACE
            // 2's customization screen shows its side thrusters.
            var preview = inStation ? GetComponentInParent<Visuals.EnginePreview>() : null;
            float floor = preview != null && preview.isActiveAndEnabled ? PreviewLevel : 0f;
            for (int i = 0; i < jets.Length; i++)
            {
                var r = jets[i];
                if (r == null) continue;
                // A jet pushes the ship against the way it blows.
                float target = Mathf.Max(floor, Mathf.Clamp01(Vector3.Dot(want, -directions[i]) * 1.3f - 0.3f));
                levels[i] = Mathf.MoveTowards(levels[i], target, dt * (target > levels[i] ? RiseRate : FallRate));
                r.GetPropertyBlock(block);
                block.SetFloat(BoostId, levels[i]);
                block.SetVector(DirId, new Vector4(0f, 0f, 1f, 0f));
                if (hasColour) block.SetColor(BoostColorId, boostColour);
                r.SetPropertyBlock(block);
                bool on = levels[i] > 0.01f;
                if (r.enabled != on) r.enabled = on;
                if (i < discs.Length && discs[i] != null) discs[i].localScale = Vector3.one * (HalfWidth * 1.6f * Mathf.Sqrt(levels[i]));
            }
        }
    }
}
