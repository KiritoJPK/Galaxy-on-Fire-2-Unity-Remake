// ThrottleGlow.cs
// Remake-only: a custom ship's glow that follows its engines (custom_ships.json "throttleGlow", built by
// CustomShipBuilder.BuildThrottleGlow) instead of the exhaust flame, e.g. the USS Enterprise's (65) blue warp nacelle
// grilles. Sits on the additive glow mesh (GoF2/Additive) and sets its _Glow / _Color per frame:
//   throttle 0 -> 'idle', throttle 100 % -> 'full', boosting -> up to 'boost' (FlightModel.BoostVisualPercent);
//   the brake (FlightModel.Braking) counts as throttle 0.
// Travelling (the planet jump: Navigation.Jumping; a jumpgate or Khador jump: SystemJump.Traveling) counts as a full boost,
// and the Khador Drive's charge builds the glow up toward it. While boosting or travelling, an optional trail in the glow's
// colour streams from the glow's rear ends ('trailPoints', set by the builder; 'trailWidth' metres, 'trailTime' s; a band's
// trails, 'trailProfile', shrink and dim from its middle to its ends like an ellipse); never
// while docking or leaving a station (the hangar flights, the launch fly-in).
// The drive comes from EngineDrive (the player's ShipController, else the ship's measured speed). The level eases toward the target so throttle steps
// fade in and out. Never hidden with the engines: while the game has the player's engine parts off (mining, object
// docking, cutscenes; AssembledObject.playerVariantParts holds the empty 'engineState' for it) the glow sits at 'idle'.
// Remake mods: 'off' (ships.json "off") is the level while parked in a hangar with the engines off instead.

using UnityEngine;

namespace GoF2Remake.Visuals
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class ThrottleGlow : MonoBehaviour
    {
        [Tooltip("Tint (multiplies the mask's own colours).")]
        public Color color = new Color(0.55f, 0.8f, 1f, 1f);
        [Tooltip("Glow intensity at throttle 0.")]
        public float idle = 0.35f;
        [Tooltip("Glow intensity at full throttle (> 1 blooms).")]
        public float full = 4f;
        [Tooltip("Glow intensity at the height of a boost.")]
        public float boost = 7f;
        [Tooltip("Remake mods: glow intensity while the ship is parked in a hangar with its engines off; below 0 = 'idle'.")]
        public float off = -1f;
        [Tooltip("How fast the glow follows the throttle (1/s).")]
        public float response = 4f;
        [Tooltip("An empty player engine part: while the game has the engines off (it is inactive) the glow stays at idle.")]
        public GameObject engineState;
        [Tooltip("Where the trails start (this object's space); none = no trail.")]
        public Vector3[] trailPoints = new Vector3[0];
        [Tooltip("Per trail point (empty = all 1): its width, brightness and (0.4 + 0.6 x) length; the ellipse across a band.")]
        public float[] trailProfile = new float[0];
        [Tooltip("Trail width at its start, metres (0 = no trail).")]
        public float trailWidth;
        [Tooltip("Trail length in seconds.")]
        public float trailTime = 0.6f;
        [Tooltip("Trail brightness, x the glow's level (additive: dimmer also reads as more see-through).")]
        public float trailBrightness = 0.3f;

        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Renderer[] renderers;
        MaterialPropertyBlock block;
        EngineDrive drive;
        TrailRenderer[] trails;
        Material trailMaterial;
        float level = -1f;
        World.SpaceLevel spaceLevel;
        bool inStation;      // the docked station scene: hangar flights (arriving, leaving) never leave a trail

        void OnEnable()
        {
            if (trails == null) BuildTrails();
            renderers = GetComponent<Renderer>() != null ? new[] { GetComponent<Renderer>() } : new Renderer[0];
            block ??= new MaterialPropertyBlock();
            if (drive == null) drive = new EngineDrive(transform); else drive.Reset();
            inStation = FindAnyObjectByType<World.StationLevel>() != null;
            spaceLevel = inStation ? null : FindAnyObjectByType<World.SpaceLevel>();
        }

        /// <summary>No trail while docking or leaving a station: the station scene's hangar flights, and in space the launch
        /// fly-in after leaving one (the arrival after a planet jump / jumpgate / wormhole keeps it).</summary>
        bool TrailSuppressed => inStation
            || (spaceLevel != null && !spaceLevel.LaunchCameraOver && !spaceLevel.StreamOutArrival);

        void OnDestroy()
        {
            if (trailMaterial != null) Destroy(trailMaterial);
        }

        /// <summary>One TrailRenderer per trail point, on a copy of the glow's material that takes vertex colours (the
        /// colour fades to black along the trail: the material is additive).</summary>
        void BuildTrails()
        {
            trails = new TrailRenderer[0];
            var mr = GetComponent<Renderer>();
            if (trailWidth <= 0f || trailPoints == null || trailPoints.Length == 0 || mr == null || mr.sharedMaterial == null) return;
            trailMaterial = new Material(mr.sharedMaterial) { name = mr.sharedMaterial.name + " (trail)" };
            // Plain white along the trail: the glow's own texture is its mask (mostly black outside the lit parts), which
            // stretched over the trail left it nearly invisible.
            trailMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            trailMaterial.EnableKeyword("_USEVERTEXCOLOR_ON");
            trailMaterial.SetFloat("_UseVertexColor", 1f);
            trailMaterial.SetColor(ColorId, color);
            trailMaterial.SetFloat(GlowId, full * trailBrightness);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0f), new GradientColorKey(new Color(0.22f, 0.22f, 0.22f), 0.3f), new GradientColorKey(Color.black, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trails = new TrailRenderer[trailPoints.Length];
            for (int i = 0; i < trailPoints.Length; i++)
            {
                var go = new GameObject("trail_" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = trailPoints[i];
                float p = trailProfile != null && i < trailProfile.Length ? trailProfile[i] : 1f;
                var t = go.AddComponent<TrailRenderer>();
                t.sharedMaterial = trailMaterial;
                t.time = trailTime * (0.4f + 0.6f * p);
                t.minVertexDistance = trailWidth * 0.5f;
                t.widthMultiplier = trailWidth * p;
                t.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
                t.colorGradient = p >= 1f ? gradient : Scaled(gradient, p);
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.receiveShadows = false;
                t.numCapVertices = 0;   // a rounded cap reaches half the width past the glow: a band-wide trail covered the hull
                t.emitting = false;
                trails[i] = t;
            }
        }

        /// <summary>The gradient with its colours x 'k' (additive material: dimmer).</summary>
        static Gradient Scaled(Gradient g, float k)
        {
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color *= k;
            var scaled = new Gradient();
            scaled.SetKeys(keys, g.alphaKeys);
            return scaled;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float d = drive.Sample(dt, engineState);
            float target = drive.Off ? 0f : d <= 1f ? Mathf.Lerp(idle, full, d) : Mathf.Lerp(full, boost, d - 1f);   // free flight, no thrust: off
            // Remake mods ("off"): parked in a hangar (the engine part hidden) the glow sits at 'off', so with idle = full it
            // is steady like the game's own engine glow: dark on the pad, lit through the take-off and all flight.
            if (inStation && off >= 0f && engineState != null && !engineState.activeInHierarchy) target = off;
            level = level < 0f ? target : Mathf.Lerp(level, target, 1f - Mathf.Exp(-dt * response));
            block.SetFloat(GlowId, level);
            block.SetColor(ColorId, color);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
            // The trail while boosting or travelling (above full throttle), as bright as the glow.
            if (trails != null && trails.Length > 0)
            {
                bool emit = d > 1.05f && gameObject.activeInHierarchy && !TrailSuppressed;
                if (trailMaterial != null) trailMaterial.SetFloat(GlowId, Mathf.Max(full, level) * trailBrightness);
                foreach (var t in trails) if (t != null && t.emitting != emit) t.emitting = emit;
            }
        }
    }
}
