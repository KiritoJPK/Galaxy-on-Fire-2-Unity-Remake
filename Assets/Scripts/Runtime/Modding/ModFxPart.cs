// ModFxPart.cs
// Remake mods: one part of a mod weapon's effect (items.json "fx", ModWeapons): a projectile, muzzle flash or impact made
// from the mod's sprite or model. Turns to face the camera (sprites), and over its lifetime (muzzle flashes, impacts)
// grows from 1 to 'grow' times its size and fades out (the GoF2 Shader Graphs' _Color: rgb on additive, alpha otherwise,
// like PartAnimation's `extra` channel). PartAnimation.PlayOnce restarts it and counts its lifetime like an animation, so
// GunRig shows and hides it like the game's own fx (GunRig.MaxLength).

using UnityEngine;

namespace GoF2Remake.Modding
{
    public class ModFxPart : MonoBehaviour
    {
        [Tooltip("Milliseconds it lasts (0: as long as it is shown, no growing or fading).")]
        public float lifetimeMs;
        [Tooltip("Its size at the end of the lifetime, times the start.")]
        public float grow = 1f;
        [Tooltip("Fades out over the lifetime.")]
        public bool fade = true;
        [Tooltip("Turns to face the camera (a sprite).")]
        public bool faceCamera;
        [Tooltip("A sprite stretched along its flight: turns about the flight direction to face the camera (laser bolts).")]
        public bool alongFlight;
        [Tooltip("Spins about its facing axis, degrees per second.")]
        public float spin;
        [Tooltip("Fades by _Color's rgb (additive) instead of its alpha.")]
        public bool additive = true;

        float ageMs;
        Vector3 baseScale;
        Quaternion baseRotation;
        Vector3 lastPosition, flight;
        bool fresh;   // just shown: GunRig moves it to the new bullet after enabling it
        bool initialised;
        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;

        void Init()
        {
            if (initialised) return;
            initialised = true;
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            }
            block = new MaterialPropertyBlock();
        }

        /// <summary>From the start again (PartAnimation.PlayOnce).</summary>
        public void Restart()
        {
            Init();
            ageMs = 0f;
            Apply();
            Orient();   // facing the camera from its first frame (it was placed after this frame's LateUpdate)
        }

        void OnEnable() { Init(); fresh = true; flight = Vector3.zero; }

        void Update()
        {
            if (lifetimeMs <= 0f && spin == 0f) return;
            ageMs += Time.deltaTime * 1000f;
            Apply();
        }

        void Apply()
        {
            if (lifetimeMs <= 0f) return;
            float t = Mathf.Clamp01(ageMs / lifetimeMs);
            transform.localScale = baseScale * Mathf.Lerp(1f, grow, t);
            if (!fade) return;
            float f = 1f - t;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_Color")) continue;
                var c = baseColors[i];
                r.GetPropertyBlock(block);
                block.SetColor("_Color", additive ? new Color(c.r * f, c.g * f, c.b * f, c.a) : new Color(c.r, c.g, c.b, c.a * f));
                r.SetPropertyBlock(block);
            }
        }

        void LateUpdate() => Orient();

        void Orient()
        {
            var turn = Quaternion.AngleAxis(spin * ageMs * 0.001f, Vector3.forward);
            if (alongFlight)
            {
                var c = Camera.main;
                var moved = fresh ? Vector3.zero : transform.position - lastPosition;
                lastPosition = transform.position;
                fresh = false;
                if (moved.sqrMagnitude > 1e-8f) flight = moved.normalized;
                var dir = flight != Vector3.zero ? flight : transform.parent != null ? transform.parent.forward : transform.forward;
                if (c == null) return;
                // Its length (the quad's y) along the flight, its face turned to the camera about it.
                var toCam = c.transform.position - transform.position;
                var n = toCam - dir * Vector3.Dot(toCam, dir);
                if (n.sqrMagnitude < 1e-8f) n = -c.transform.forward;
                transform.rotation = Quaternion.LookRotation(-n, dir);
                return;
            }
            if (!faceCamera)
            {
                if (spin != 0f) transform.localRotation = baseRotation * turn;
                return;
            }
            var cam = Camera.main;
            if (cam == null) return;
            // The quad faces local -Z; turned like the camera it faces it.
            transform.rotation = cam.transform.rotation * turn;
        }

        /// <summary>The longest lifetime of the parts under 'root' (GunRig.MaxLength, PartAnimation.PlayOnce).</summary>
        public static float MaxLength(GameObject root)
        {
            float l = 0f;
            foreach (var p in root.GetComponentsInChildren<ModFxPart>(true)) l = Mathf.Max(l, p.lifetimeMs);
            return l;
        }

        /// <summary>Restarts every part under 'root'; returns the longest lifetime.</summary>
        public static float RestartAll(GameObject root) => RestartAll(root.GetComponentsInChildren<ModFxPart>(true));

        /// <summary>RestartAll on parts collected beforehand (GunRig: per shot and per hit, without a hierarchy walk).</summary>
        public static float RestartAll(ModFxPart[] parts)
        {
            float l = 0f;
            foreach (var p in parts) { p.Restart(); l = Mathf.Max(l, p.lifetimeMs); }
            return l;
        }
    }
}
