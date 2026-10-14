// VrCockpit.cs
// Remake VR, in flight: the player sits in a cockpit (the original has none: the same code-built cockpit for every ship
// until a modelled one replaces it). ChaseCamera puts the scene camera rigidly on the seat (VrMode: no lag, shake or boost
// zoom), so the rig, the head and this cockpit (a child of the rig) ride with the ship; the player's own hull is hidden (its
// renderers on VrRig.HiddenLayer, which the eye doesn't draw; put back while the cockpit is off: VrRig's cutscene view).
// The flight HUD is split: its middle (the crosshair, the markers, the lock plate, the messages, the menus) stays on the
// canopy HUD 2 m ahead, which spans the scene camera's view (VrRig) so the markers sit on what they mark, cropped to that
// middle; its corners go onto the cockpit's displays, the same texture cut out (VrPanels.Crop): the shield / hull / armor
// bars and the recharge icons (left), the cargo readout (right), the selected secondary (centre). The control hints aren't
// shown in the cockpit; the left console has the speed handle (VrControls).

using GoF2Remake.Flight;
using GoF2Remake.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.Vr
{
    public sealed class VrCockpit : MonoBehaviour
    {
        /// <summary>The seat in the ship's frame (metres): the scene camera's place in VR flight (ChaseCamera).</summary>
        public static readonly Vector3 Seat = Data.VrFlags.CockpitSeat;

        // The HUD's regions (panel pixels at 1920 x 1080, top-left origin): x, y, width, height.
        static readonly Rect StatusRegion = new Rect(0f, 0f, 430f, 215f);
        static readonly Rect ReadoutRegion = new Rect(1490f, 0f, 430f, 145f);
        static readonly Rect SecondaryRegion = new Rect(730f, 1035f, 460f, 45f);
        static readonly Rect CanopyRegion = new Rect(326f, 0f, 1268f, 1026f);

        VrRig rig;
        VrPanels panels;
        PanelSettings hudPanel;
        Material frame, glass;
        readonly System.Collections.Generic.Dictionary<GameObject, int> hiddenLayers = new System.Collections.Generic.Dictionary<GameObject, int>();
        readonly System.Collections.Generic.List<Renderer> renderers = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<(Material material, Rect region)> displays = new System.Collections.Generic.List<(Material, Rect)>();
        RenderTexture hudTexture;

        public void Init(VrRig owner, VrPanels screenPanels)
        {
            rig = owner;
            panels = screenPanels;
            frame = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            frame.SetColor("_BaseColor", new Color(0.11f, 0.12f, 0.14f));
            frame.SetFloat("_Metallic", 0.65f);
            frame.SetFloat("_Smoothness", 0.45f);
            glass = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            glass.SetColor("_BaseColor", new Color(0.015f, 0.03f, 0.045f));
            Build();
            gameObject.AddComponent<VrControls>().Init(rig, frame);   // the grabbable stick and throttle (an option)
        }

        // ---- the model -----------------------------------------------------------------------------------------

        GameObject Box(string name, Vector3 centre, Vector3 size, Vector3 euler, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A strut of the canopy frame from 'a' to 'b'.</summary>
        void Strut(Vector3 a, Vector3 b, float thickness = 0.035f)
        {
            var go = Box("Strut", (a + b) * 0.5f, new Vector3(thickness, thickness, Vector3.Distance(a, b)), Vector3.zero, frame);
            go.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
        }

        /// <summary>A display on 'parent' (a panel's face), showing 'region' of the HUD: a dark glass with the cut-out on it.</summary>
        void Display(Transform parent, Vector2 centre, float width, Rect region)
        {
            float height = width * region.height / region.width;
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(back.GetComponent<Collider>());
            back.name = "Display";
            back.transform.SetParent(parent, false);
            // The face of a unit cube scaled to the panel: undo its scale, sit just in front of its -z face.
            var s = parent.localScale;
            back.transform.localScale = new Vector3((width + 0.02f) / s.x, (height + 0.02f) / s.y, 1f);
            back.transform.localPosition = new Vector3(centre.x / s.x, centre.y / s.y, -0.5f - 0.002f / s.z);
            back.GetComponent<MeshRenderer>().sharedMaterial = glass;
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(screen.GetComponent<Collider>());
            screen.name = "Display screen";
            screen.transform.SetParent(parent, false);
            screen.transform.localScale = new Vector3(width / s.x, height / s.y, 1f);
            screen.transform.localPosition = new Vector3(centre.x / s.x, centre.y / s.y, -0.5f - 0.004f / s.z);
            var m = VrPanels.ScreenMaterial(3000);
            screen.GetComponent<MeshRenderer>().sharedMaterial = m;
            displays.Add((m, region));
        }

        void Build()
        {
            // Dashboard: a sloped top and a front panel facing the pilot under the canopy's view.
            Box("Dashboard top", new Vector3(0f, -0.42f, 0.68f), new Vector3(1.36f, 0.06f, 0.42f), new Vector3(-18f, 0f, 0f), frame);
            // Its -z face (the displays) tilted back toward the pilot's eyes.
            var front = Box("Dashboard panel", new Vector3(0f, -0.33f, 0.66f), new Vector3(1.2f, 0.3f, 0.05f), new Vector3(27f, 0f, 0f), frame).transform;
            Box("Dashboard hood", new Vector3(0f, -0.16f, 0.74f), new Vector3(1.26f, 0.03f, 0.2f), new Vector3(-8f, 0f, 0f), frame);
            // The tub: a floor, the wall under the dashboard, the side walls and the seat's back.
            Box("Floor", new Vector3(0f, -0.88f, 0.15f), new Vector3(1.6f, 0.04f, 2f), Vector3.zero, frame);
            Box("Front wall", new Vector3(0f, -0.66f, 0.84f), new Vector3(1.36f, 0.46f, 0.05f), new Vector3(-10f, 0f, 0f), frame);
            Box("Left wall", new Vector3(-0.78f, -0.55f, 0.15f), new Vector3(0.05f, 0.7f, 1.7f), Vector3.zero, frame);
            Box("Right wall", new Vector3(0.78f, -0.55f, 0.15f), new Vector3(0.05f, 0.7f, 1.7f), Vector3.zero, frame);
            Box("Seat back", new Vector3(0f, -0.25f, -0.42f), new Vector3(0.62f, 1f, 0.12f), new Vector3(-8f, 0f, 0f), frame);
            Box("Headrest", new Vector3(0f, 0.32f, -0.48f), new Vector3(0.34f, 0.22f, 0.1f), new Vector3(-8f, 0f, 0f), frame);
            // Side consoles.
            var left = Box("Left console", new Vector3(-0.6f, -0.5f, 0.2f), new Vector3(0.26f, 0.14f, 0.8f), new Vector3(0f, 0f, 12f), frame).transform;
            Box("Right console", new Vector3(0.6f, -0.5f, 0.2f), new Vector3(0.26f, 0.14f, 0.8f), new Vector3(0f, 0f, -12f), frame);
            // The canopy frame.
            var sillL = new Vector3(-0.7f, -0.16f, 0.86f);
            var sillR = new Vector3(0.7f, -0.16f, 0.86f);
            var topL = new Vector3(-0.42f, 0.46f, 0.42f);
            var topR = new Vector3(0.42f, 0.46f, 0.42f);
            Strut(sillL, sillR, 0.045f);
            Strut(sillL, topL);
            Strut(sillR, topR);
            Strut(topL, topR);
            Strut(topL, new Vector3(-0.48f, 0.42f, -0.35f));
            Strut(topR, new Vector3(0.48f, 0.42f, -0.35f));
            Strut(new Vector3(-0.72f, -0.2f, -0.3f), sillL, 0.04f);
            Strut(new Vector3(0.72f, -0.2f, -0.3f), sillR, 0.04f);

            // The displays on the front panel and the left console (their -z faces point at the pilot).
            Display(front, new Vector2(-0.38f, 0.02f), 0.32f, StatusRegion);
            Display(front, new Vector2(0.38f, 0.04f), 0.3f, ReadoutRegion);
            Display(front, new Vector2(0f, -0.08f), 0.34f, SecondaryRegion);
            // The radar scope in the middle of the front panel, between the status and the cargo displays.
            var radar = new GameObject("VR Radar").transform;
            radar.SetParent(transform, false);
            radar.position = front.TransformPoint(new Vector3(0f, 0.045f / front.localScale.y, -0.5f - 0.003f / front.localScale.z));
            radar.rotation = front.rotation;
            radar.gameObject.AddComponent<VrRadar>().Init();
            _ = left;
        }

        // ---- per frame -------------------------------------------------------------------------------------

        void LateUpdate()
        {
            HidePlayerHull();
            // The flight HUD's texture: the displays show their regions, the canopy only the middle.
            if (hudPanel == null)
            {
                var hud = FindAnyObjectByType<FlightHud>();
                var pr = hud != null ? hud.GetComponent<PanelRenderer>() : null;
                hudPanel = pr != null ? pr.panelSettings : null;
                if (hudPanel != null) panels.Crop(hudPanel, ToUv(CanopyRegion));
            }
            var tex = hudPanel != null ? panels.TextureOf(hudPanel) : null;
            if (tex != hudTexture)
            {
                hudTexture = tex;
                foreach (var (m, region) in displays)
                {
                    m.SetTexture("_BaseMap", tex);
                    var uv = ToUv(region);
                    m.SetTextureScale("_BaseMap", uv.size);
                    m.SetTextureOffset("_BaseMap", uv.position);
                }
            }
        }

        /// <summary>A HUD region (panel pixels, top-left origin, 1920 x 1080) as a texture rect (bottom-left origin, 0..1).</summary>
        static Rect ToUv(Rect r) => new Rect(r.x / 1920f, 1f - (r.y + r.height) / 1080f, r.width / 1920f, r.height / 1080f);

        /// <summary>The player's hull (and whatever is attached to it later: turrets, exhaust) onto the hidden layer.</summary>
        void HidePlayerHull()
        {
            var level = FindAnyObjectByType<World.SpaceLevel>();
            var ship = level != null ? level.Player : null;
            var model = ship != null && ship.visualModel != null ? ship.visualModel.gameObject : null;
            if (model == null) return;
            model.GetComponentsInChildren(true, renderers);
            foreach (var r in renderers)
            {
                var go = r.gameObject;
                if (go.layer == VrRig.HiddenLayer) continue;
                hiddenLayers[go] = go.layer;
                go.layer = VrRig.HiddenLayer;
            }
        }

        /// <summary>The hull's own layers back (the cockpit switched off for a cutscene, or gone).</summary>
        void ShowPlayerHull()
        {
            foreach (var kv in hiddenLayers) if (kv.Key != null && kv.Key.layer == VrRig.HiddenLayer) kv.Key.layer = kv.Value;
            hiddenLayers.Clear();
        }

        void OnDisable() => ShowPlayerHull();

        void OnDestroy()
        {
            if (frame != null) Destroy(frame);
            if (glass != null) Destroy(glass);
            foreach (var (m, _) in displays) if (m != null) Destroy(m);
        }
    }
}
