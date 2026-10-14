// HangarShipShadow.cs
// Remake (the original's hangars have no shadows): a soft shadow under a hangar ship, its hull's silhouette from above
// (ShipShadowSet, baked in the Editor: the silhouette, a soft halo around it and the hull's underside; the mods' ships
// baked the same way at run time by ShipShadowBaker, hidden until it is done; a soft oval of its bounds for ships that
// can't be, the debug capital hulls). The hangar's light comes from the
// camera's side, so a real shadow falls behind the ship, out of view; this is the contact shadow under it. Projected like
// a decal (GoF2/HangarShadow on a box around the footprint, reading the station camera's depth texture), so it lies on
// whatever is under the hull: the pads, the Terran cradles (the hull sits below their rims), the raised rims and
// pedestals, the crates. The box sits in the hangar, not on the ship: it only turns with the ship's heading. Shown while
// the ship stands on its pad (the turntable, the parked ships; at once for a ship spawned there), fading out as it lifts off (gone 15 m up) and staying on
// the pad as it flies away; an arriving ship's fades in the same way as it comes down onto its pad (ExpectLanding: the
// flight names the pad, so the shadow knows the floor before the ship gets there). The "Hangar ship shadows" option
// (Settings.HangarShadows: off / player ship only / all ships) hides them live; off also gives the station camera its
// own depth texture setting back (the shadows' depth prepass / copy is most of their cost on phones).

using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GoF2Remake.World
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class HangarShipShadow : MonoBehaviour
    {
        const float FadeHeight = 15f, RestSeconds = 0.3f;
        /// <summary>The projector box, metres around the resting hull's lowest point: down to the floor under a raised hull or a sunk pad centre (the Vossk turntable's disc is ~2 m below it),
        /// up past the pads' rims and recesses (full over its lower part, gone at its top).</summary>
        const float Below = 6f, Above = 8f;

        static Mesh boxMesh;
        /// <summary>The camera the shadows switched the depth texture on for, and its own setting before (restored when the
        /// option is off).</summary>
        static Camera depthCamera;
        static CameraOverrideOption depthBefore;

        GameObject box;
        MeshRenderer boxRenderer;
        MaterialPropertyBlock block;
        Color baseColour;
        Vector2 center;
        float size, bottom, heightRange = 1f;
        float floorY = float.NaN, restTime, alpha;
        float landingPadY = float.NaN;   // an arriving ship's pad (its pivot's height there), until it has landed
        Vector3 lastPos;
        bool player;
        bool settle;     // spawned resting: the floor and full strength on the first update (SpawnShip places the ship after Attach)
        string baking;   // the assembly ShipShadowBaker is baking for this ship (hidden meanwhile)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { boxMesh = null; depthCamera = null; }

        /// <summary>The option lets this ship's shadow show (off: none; player ship only: the turntable's).</summary>
        bool Allowed => Settings.HangarShadows == Settings.HangarShadowsAll || (player && Settings.HangarShadows == Settings.HangarShadowsPlayer);

        /// <summary>The station camera's depth texture: on while the option shows any shadow, else back to its own setting.</summary>
        static void ApplyDepth()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (depthCamera != cam) { depthCamera = cam; depthBefore = data.requiresDepthOption; }
            var want = Settings.HangarShadows == Settings.HangarShadowsOff ? depthBefore : CameraOverrideOption.On;
            if (data.requiresDepthOption != want) data.requiresDepthOption = want;
        }

        /// <summary>A shadow for a hangar ship (StationLevel.SpawnShip); 'resting': it stands on its pad now, shown at once (a
        /// landing flight hides it again, ExpectLanding; else it shows once the ship has come to rest); 'player': the player's own ship (the option's "Player ship only"). Attached
        /// whatever the option, hidden while it doesn't allow it, so changing it while docked takes effect at once.</summary>
        public static HangarShipShadow Attach(GameObject ship, string assembly, bool resting, bool player = false)
        {
            var set = ShipShadowSet.Load();
            if (ship == null || set == null || set.material == null) return null;
            var s = ship.AddComponent<HangarShipShadow>();
            // A mod's ship never takes the set's map: its assembly name is a number this game's mod registry gave it, which
            // another game (or this one before) gave another ship.
            var entry = ShipShadowBaker.IsModShip(ship) ? null : set.Find(assembly);
            Texture2D tex = null;
            if (entry != null && entry.texture != null) tex = s.Use(entry);
            else if (ShipShadowBaker.Request(assembly, ship, out entry))
            {
                // Its hull baked here (a mod's ship): the map now, or once the worker thread is done.
                if (entry != null) tex = s.Use(entry);
                else
                {
                    s.baking = assembly;
                    s.size = 1f;
                    s.bottom = LocalBounds(ship).min.y;
                    tex = set.oval;
                }
            }
            else
            {
                // A soft oval of the hull's bounds (in the ship's own space, at scale 1).
                tex = set.oval;
                var b = LocalBounds(ship);
                s.center = new Vector2(b.center.x, b.center.z);
                s.size = Mathf.Max(b.size.x, b.size.z) * 1.25f;
                s.bottom = b.min.y;
            }
            if (tex == null || s.size <= 0f) { Destroy(s); return null; }
            s.player = player;
            // The projection reads the scene's depth: the station camera renders its depth texture (unless the option is off).
            ApplyDepth();
            s.box = new GameObject("Shadow (" + ship.name + ")");
            s.box.transform.SetParent(ship.transform.parent, false);
            s.box.AddComponent<MeshFilter>().sharedMesh = Box();
            s.boxRenderer = s.box.AddComponent<MeshRenderer>();
            s.boxRenderer.sharedMaterial = set.material;
            s.boxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.boxRenderer.receiveShadows = false;
            s.baseColour = set.material.HasProperty("_Color") ? set.material.GetColor("_Color") : new Color(0f, 0f, 0f, 0.7f);
            s.block = new MaterialPropertyBlock();
            s.block.SetTexture("_MainTex", tex);
            s.lastPos = ship.transform.position;
            s.settle = resting;
            s.Place();
            return s;
        }

        /// <summary>The ship is flying in to land with its pivot at height 'padY' (HangarFlight.Arrival): its shadow lies on
        /// that floor from now on and fades in as the ship comes down, the reverse of a take-off. A hover on the way
        /// (the turn over the pad) isn't taken for the landing.</summary>
        public void ExpectLanding(float padY) { landingPadY = padY; restTime = 0f; alpha = 0f; settle = false; }

        /// <summary>Takes a baked map; its texture.</summary>
        Texture2D Use(ShipShadowSet.Entry entry)
        {
            center = entry.center;
            size = entry.size;
            bottom = entry.bottom;
            heightRange = entry.heightRange;
            return entry.texture;
        }

        /// <summary>The ship's mesh bounds in its own space at scale 1 (not the additive glow layers or the trails).</summary>
        static Bounds LocalBounds(GameObject ship)
        {
            var root = ship.transform;
            var b = new Bounds();
            bool any = false;
            foreach (var mf in ship.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.name.Contains("_add") || mf.name.StartsWith("engine_glow") || mf.name.StartsWith("throttle_glow")
                    || !mf.gameObject.activeInHierarchy) continue;
                var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>The unit cube (-0.5..0.5), the projector's volume.</summary>
        static Mesh Box()
        {
            if (boxMesh != null) return boxMesh;
            boxMesh = new Mesh { name = "hangar shadow box" };
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            boxMesh.vertices = v;
            boxMesh.triangles = new[]
            {
                0, 2, 3, 0, 3, 1,   4, 5, 7, 4, 7, 6,   0, 4, 6, 0, 6, 2,
                1, 3, 7, 1, 7, 5,   0, 1, 5, 0, 5, 4,   2, 6, 7, 2, 7, 3,
            };
            boxMesh.RecalculateBounds();
            return boxMesh;
        }

        float Scale => transform.lossyScale.x;
        float HullBottom() => transform.position.y + bottom * Scale;

        void LateUpdate()
        {
            if (box == null) return;
            if (baking != null) PollBake();
            var pos = transform.position;
            if (settle)
            {
                settle = false;
                lastPos = pos;
                restTime = RestSeconds;
                floorY = HullBottom();
                alpha = 1f;
            }
            float dt = Time.deltaTime;
            restTime = (pos - lastPos).sqrMagnitude < 1e-6f ? restTime + dt : 0f;
            lastPos = pos;
            if (!float.IsNaN(landingPadY)) floorY = landingPadY + bottom * Scale;   // coming in: the pad's floor
            // Standing on a pad: the hull's lowest point is at the floor (coming in: only once down on it).
            if (restTime >= RestSeconds && (float.IsNaN(landingPadY) || HullBottom() <= floorY + 1f))
            {
                floorY = HullBottom();
                landingPadY = float.NaN;
            }
            float target = 0f;
            if (!float.IsNaN(floorY)) target = Mathf.Clamp01(1f - (HullBottom() - floorY) / FadeHeight);
            alpha = Mathf.MoveTowards(alpha, target, dt * 3f);
            Place();
        }

        /// <summary>The run-time bake done: its map (fading in), or the oval when it failed.</summary>
        void PollBake()
        {
            bool bakeable = ShipShadowBaker.Request(baking, gameObject, out var entry);
            if (bakeable && entry == null) return;   // still on the worker thread
            baking = null;
            if (entry != null) block.SetTexture("_MainTex", Use(entry));
            else
            {
                var b = LocalBounds(gameObject);
                center = new Vector2(b.center.x, b.center.z);
                size = Mathf.Max(b.size.x, b.size.z) * 1.25f;
                bottom = b.min.y;
            }
            if (restTime >= RestSeconds && float.IsNaN(landingPadY)) floorY = HullBottom();
        }

        void Place()
        {
            bool show = alpha > 0.005f && !float.IsNaN(floorY) && Allowed && baking == null;
            if (boxRenderer.enabled != show) boxRenderer.enabled = show;
            if (!show) return;
            var fwd = transform.forward;
            fwd.y = 0f;
            var yaw = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
            float scale = Scale;
            float height = Mathf.Max(0f, HullBottom() - floorY);
            float side = size * scale * (1f + height / 40f);   // a lifting ship's shadow spreads
            var c = yaw * new Vector3(center.x, 0f, center.y) * scale;
            box.transform.SetPositionAndRotation(new Vector3(transform.position.x + c.x, floorY + (Above - Below) * 0.5f, transform.position.z + c.z), yaw);
            box.transform.localScale = new Vector3(side, Below + Above, side);
            block.SetColor("_Color", new Color(baseColour.r, baseColour.g, baseColour.b, baseColour.a * alpha));
            block.SetVector("_Hull", new Vector4(HullBottom(), heightRange * scale, 0f, 0f));
            boxRenderer.SetPropertyBlock(block);
        }

        void OnEnable()
        {
            if (box != null) box.SetActive(true);
            Settings.Changed += ApplyDepth;
        }

        void OnDisable()
        {
            if (box != null) box.SetActive(false);
            Settings.Changed -= ApplyDepth;
        }
        void OnDestroy() { if (box != null) Destroy(box); }
    }
}
