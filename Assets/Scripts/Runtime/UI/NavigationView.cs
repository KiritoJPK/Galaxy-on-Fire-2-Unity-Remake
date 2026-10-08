// NavigationView.cs
// The flight HUD's navigation visuals (Reference/research/autopilot_travel.md 1 and 4), driven by FlightHud:
//   landmarks near the screen centre (+-w/6): bracket 0x4f2 with the name, the station's "Tech level: N" and the distance
//     (Radar::calcDistance, from the camera); elsewhere only the jumpgate gets its icon 0x453 (off screen clamped to the
//     radar ellipse 657 x 491 around the centre); the station has no marker then
//   planets: the gate icon next to the jumpgate station's planet, the name only while the planet is in the lock box
//   lock ring and top plate for station / jumpgate / planet locks (race icon of the system for landmarks), shared with
//     MiningView's elements
//   autopilot (0x4b0 / lit 0x4b1 while the autopilot or an asteroid approach runs; opens the autopilot menu otherwise)
//     and fast-forward (0x541 / held 0x540, only while allowed) on their pill (0x53f), touch only, hidden in the minigame
// Positions are HD pixels = panel units.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class NavigationView
    {
        const float M = 0.05f;
        const float EllipseX = 657f, EllipseY = 491f;   // image 0x4c7, Radar::elipsoidIntersect

        class Marker
        {
            public Navigation.Target target;
            public VisualElement bracket, icon, story, freelance;
            public Label name, tech, distance;
        }

        readonly VisualElement layer, lockRing, lockPlate, lockClass, navButtons, fastForward, autopilotButton, pill;
        readonly Label lockOre;
        readonly Texture2D[] lockFrames = new Texture2D[24];
        readonly Texture2D autopilotOff, autopilotOn, fastForwardOff, fastForwardOn, clockOff, clockOn;
        bool clockMode, extenderTap;

        /// <summary>The clock was tapped this frame (the time extender's button, HUD key 0x100 outside the autopilot).</summary>
        public bool ConsumeExtenderTap() { bool t = extenderTap; extenderTap = false; return t; }
        readonly List<Marker> markers = new List<Marker>();
        readonly Dictionary<int, Texture2D> raceIcons = new Dictionary<int, Texture2D>();
        Navigation built;
        int builtCount;
        string techLine;
        int systemRace, gateStation = -1;
        bool fastForwardPressed;

        public bool FastForwardPressed => fastForwardPressed;
        public event System.Action AutopilotButton;

        // Looked up once per name: the markers ask for theirs every frame (a Resources.Load each, with its path string).
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        static Texture2D Tex(string name)
        {
            if (!textures.TryGetValue(name, out var t) || t == null) textures[name] = t = Resources.Load<Texture2D>("GoF2Hud/" + name);
            return t;
        }

        public NavigationView(VisualElement root)
        {
            for (int i = 0; i < 24; i++) lockFrames[i] = Tex($"lock_{i:00}");
            layer = root.Q("navMarkers");
            lockRing = root.Q("lockRing");
            lockPlate = root.Q("lockPlate");
            lockClass = root.Q("lockClass");
            lockOre = root.Q<Label>("lockOre");
            navButtons = root.Q("navButtons");
            fastForward = root.Q("fastForwardButton");
            autopilotButton = root.Q("autopilotButton");
            autopilotOff = Tex("autopilot");
            autopilotOn = Tex("autopilot_on");
            fastForwardOff = Tex("fastforward");
            fastForwardOn = Tex("fastforward_on");
            clockOff = Tex("time_extender");
            clockOn = Tex("time_extender_on");
            pill = root.Q("navButtonPill");
            Image(pill, Tex("button_pill"));
            Image(fastForward, fastForwardOff);
            Image(autopilotButton, autopilotOn);

            // Fast-forward is hold-to-use (MGame::OnTouchEnd ends it on any release); the autopilot button is a tap.
            fastForward.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                if (clockMode) { extenderTap = true; return; }
                fastForwardPressed = true;
                fastForward.CapturePointer(e.pointerId);
            });
            fastForward.RegisterCallback<PointerUpEvent>(e => { fastForwardPressed = false; fastForward.ReleasePointer(e.pointerId); });
            fastForward.RegisterCallback<PointerCancelEvent>(e => fastForwardPressed = false);
            autopilotButton.RegisterCallback<PointerUpEvent>(e => { AutopilotButton?.Invoke(); e.StopPropagation(); });
            autopilotButton.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
        }

        static void Image(VisualElement e, Texture2D tex)
        {
            if (e == null || tex == null) return;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.width = tex.width;
            e.style.height = tex.height;
        }

        static Label Text(VisualElement parent, string cls)
        {
            var l = new Label { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
            l.AddToClassList("nav-label");
            if (cls != null) l.AddToClassList(cls);
            l.AddToClassList("gof-semibold");
            parent.Add(l);
            return l;
        }

        void Build(Navigation nav, int race, int jumpgateStation, int techLevel)
        {
            built = nav;
            builtCount = nav.Targets.Count;
            systemRace = race;
            gateStation = jumpgateStation;
            techLine = $"{Localization.Get(133)}: {techLevel}";
            layer.Clear();
            markers.Clear();
            foreach (var t in nav.Targets)
            {
                var m = new Marker { target = t };
                bool wormhole = t.kind == Navigation.Kind.Wormhole;
                if (t.kind != Navigation.Kind.Planet && !wormhole)
                {
                    m.bracket = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
                    m.bracket.AddToClassList("nav-abs");
                    Image(m.bracket, Tex("bracket"));
                    layer.Add(m.bracket);
                }
                if (t.kind == Navigation.Kind.Jumpgate || t.kind == Navigation.Kind.Waypoint || wormhole || (t.kind == Navigation.Kind.Planet && t.station == gateStation))
                {
                    m.icon = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
                    m.icon.AddToClassList("nav-abs");
                    Image(m.icon, Tex(t.kind == Navigation.Kind.Waypoint ? (t.freelance ? "map_freelance" : "map_story") : wormhole ? "wormhole_icon" : "gate_icon"));
                    layer.Add(m.icon);
                }
                // Radar::draw: the gold story icon 0x454 next to the campaign target's planet (visible missions only); remake:
                // step 59's convoy stations too, like the map (Story.MapMarks; the original only marks the mission's own station),
                // and an event graph quest's target (EventRunner.IsQuestTarget; it may change in the orbit: shown per frame).
                if (t.kind == Navigation.Kind.Planet)
                {
                    m.story = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
                    m.story.AddToClassList("nav-abs");
                    Image(m.story, Tex("map_story"));
                    layer.Add(m.story);
                }
                // Radar::draw: the white freelance icon 0x455 on the freelance mission's target planet (type 0xe: the
                // client's station), drawn after the story icon at the same spot.
                if (t.kind == Navigation.Kind.Planet)
                {
                    m.freelance = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
                    m.freelance.AddToClassList("nav-abs");
                    Image(m.freelance, Tex("map_freelance"));
                    layer.Add(m.freelance);
                }
                m.name = Text(layer, null);
                m.name.text = t.name;
                // Radar::draw: the Void station and the gate get the distance only, the wormhole its name only.
                if (t.kind == Navigation.Kind.Station && t.station != Session.VoidOrbit) { m.tech = Text(layer, "nav-label--dim"); m.tech.text = techLine; }
                if (t.kind != Navigation.Kind.Planet && !wormhole) m.distance = Text(layer, "nav-label--dim");
                markers.Add(m);
            }
        }

        /// <summary>Status::getFreelanceMission's target (Mission::getTargetStation; type 0xe Stolen goods: the agent's station).</summary>
        static bool IsFreelanceTarget(int station)
        {
            // Remake: a single-player event graph bar mission's target ("target" line).
            if (Events.EventMissions.Active != null && Events.EventRunner.LocalMissionTarget == station) return true;
            if (!Freelance.Active) return false;
            var f = Freelance.Mission;
            return station == (f.type == MissionType.StolenGoods ? f.clientStation : f.target);
        }

        /// <summary>The gold story icon: the campaign target (visible missions only, Story.MapMarks) or an event graph quest's.</summary>
        static bool IsStoryTarget(int station) =>
            (!Session.FreePlay && Story.Mission.visible && Story.MapMarks(station)) || Events.EventRunner.IsQuestTarget(station);

        /// <param name="race">The system's race (plate icon for landmarks).</param>
        public void Update(Navigation nav, Camera cam, bool touch, Mining.Phase miningPhase, int race, int jumpgateStation, int techLevel)
        {
            bool show = nav != null && cam != null && layer.panel != null && !nav.Jumping;
            layer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateButtons(nav, touch, miningPhase);
            if (!show) return;
            if (built != nav || builtCount != nav.Targets.Count) Build(nav, race, jumpgateStation, techLevel);

            var origin = layer.worldBound.position;
            float w = Screen.width, h = Screen.height;
            var centre = layer.layout.size / 2f;
            foreach (var m in markers)
            {
                var t = m.target;
                if (t.hidden)
                {
                    if (m.bracket != null) m.bracket.style.display = DisplayStyle.None;
                    if (m.icon != null) m.icon.style.display = DisplayStyle.None;
                    if (m.story != null) m.story.style.display = DisplayStyle.None;
                    if (m.freelance != null) m.freelance.style.display = DisplayStyle.None;
                    m.name.style.display = DisplayStyle.None;
                    if (m.distance != null) m.distance.style.display = DisplayStyle.None;
                    continue;
                }
                var sp = cam.WorldToScreenPoint(t.Position);
                bool onScreen = sp.z > 0f && sp.x >= 0f && sp.y >= 0f && sp.x <= w && sp.y <= h;
                bool nearCentre = onScreen && Mathf.Abs(sp.x - w / 2f) < w / 6f && Mathf.Abs(sp.y - h / 2f) < w / 6f;
                Vector2 p = onScreen ? RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, t.Position, cam) - origin : Vector2.zero;
                bool planet = t.kind == Navigation.Kind.Planet;

                if (m.bracket != null)
                {
                    m.bracket.style.display = nearCentre ? DisplayStyle.Flex : DisplayStyle.None;
                    if (nearCentre) Place(m.bracket, p.x - 40.5f, p.y - 40.5f);
                }
                if (planet)
                {
                    bool inBox = nav.Candidate == t;   // the name shows only while the planet is in the lock box
                    if (m.icon != null) { m.icon.style.display = onScreen ? DisplayStyle.Flex : DisplayStyle.None; Place(m.icon, p.x + 10f, p.y - 10f); }
                    // Radar::draw: the mission icons at x + 10 (+ 24 past the gate icon), y - 10; the name moves 14 px on (phone
                    // pixels: the HD icons are 26 px wide, so 28 here, or the name covers the icon).
                    float ix = p.x + (m.icon != null ? 24f : 10f);
                    bool freelanceHere = m.freelance != null && IsFreelanceTarget(t.station);
                    bool storyHere = m.story != null && IsStoryTarget(t.station);
                    if (m.story != null) { m.story.style.display = onScreen && storyHere ? DisplayStyle.Flex : DisplayStyle.None; Place(m.story, ix, p.y - 10f); }
                    if (m.freelance != null) { m.freelance.style.display = onScreen && freelanceHere ? DisplayStyle.Flex : DisplayStyle.None; Place(m.freelance, ix, p.y - 10f); }
                    m.name.style.display = onScreen && inBox ? DisplayStyle.Flex : DisplayStyle.None;
                    Place(m.name, ix + (storyHere || freelanceHere ? 28f : 0f), p.y - 10f);
                    continue;
                }

                // Landmarks: labels near the centre; elsewhere the jumpgate icon on the radar ellipse, the station nothing.
                bool station = t.kind == Navigation.Kind.Station && t.station != Session.VoidOrbit;
                float lx = station ? 50f : 10f;
                if (t.kind == Navigation.Kind.DockingTarget && m.name.text != t.name) m.name.text = t.name;   // renamed by the level
                m.name.style.display = nearCentre ? DisplayStyle.Flex : DisplayStyle.None;
                if (m.distance != null) m.distance.style.display = nearCentre ? DisplayStyle.Flex : DisplayStyle.None;
                if (m.tech != null) m.tech.style.display = nearCentre ? DisplayStyle.Flex : DisplayStyle.None;
                if (nearCentre)
                {
                    Place(m.name, p.x + lx, p.y);
                    if (m.tech != null) Place(m.tech, p.x + lx, p.y + 30f);
                    if (m.distance != null)
                    {
                        Place(m.distance, p.x + lx, p.y + (station ? 60f : 30f));
                        m.distance.text = Navigation.FormatDistance((t.Position - cam.transform.position).magnitude / M);
                    }
                }
                if (m.icon != null)
                {
                    m.icon.style.display = nearCentre || (!onScreen && Vr.VrMode.Enabled) ? DisplayStyle.None : DisplayStyle.Flex;   // VR: none on the ellipse
                    if (!nearCentre)
                    {
                        Vector2 q = onScreen ? p : OffScreen(cam, t.Position, centre);
                        float half = t.kind == Navigation.Kind.Wormhole ? 29f : 13f;
                        Place(m.icon, q.x - half, q.y - half);
                    }
                }
            }

            // Lock ring on the crosshair and the top plate (Radar::drawCurrentLock) for landmark / planet locks.
            var locking = nav.Candidate;
            if (locking != null && nav.LockFrame >= 0)
            {
                lockRing.EnableInClassList("lock-ring--shown", true);
                Image(lockRing, lockFrames[Mathf.Clamp(nav.LockFrame, 0, 23)]);
            }
            var locked = nav.Locked;
            if (locked != null)
            {
                lockPlate.EnableInClassList("lock-plate--shown", true);
                lockOre.text = locked.name;
                // The remake's lockable waypoint shows its own marker, not the system's race icon (#28).
                var icon = locked.kind == Navigation.Kind.Planet ? null
                         : locked.kind == Navigation.Kind.Waypoint ? Tex(locked.freelance ? "map_freelance" : "map_story")
                         : RaceIcon(systemRace);
                lockClass.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
                Image(lockClass, icon);
            }
            else lockClass.style.display = DisplayStyle.Flex;
        }

        /// <summary>Radar::update for an object not on screen: its direction clamped onto the radar ellipse.</summary>
        static Vector2 OffScreen(Camera cam, Vector3 world, Vector2 centre)
        {
            var local = cam.transform.InverseTransformPoint(world);
            var d = new Vector2(local.x, -local.y);
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            float k = Mathf.Sqrt(d.x * d.x / (EllipseX * EllipseX) + d.y * d.y / (EllipseY * EllipseY));
            return centre + d / k;
        }

        Texture2D RaceIcon(int race)
        {
            int frame = race >= 0 && race <= 3 ? race : race == 8 ? 8 : 9;
            if (!raceIcons.TryGetValue(frame, out var t)) raceIcons[frame] = t = Tex($"race_{frame}");
            return t;
        }

        void UpdateButtons(Navigation nav, bool touch, Mining.Phase miningPhase)
        {
            bool approach = miningPhase == Mining.Phase.Approaching;
            bool active = nav != null && (nav.Autopilot || approach) && !nav.Jumping;
            // Hud::draw 10: not at campaign 0 / 1 (the prologue and the rescue).
            bool visible = touch && nav != null && !nav.Jumping && (miningPhase == Mining.Phase.Idle || approach)
                           && (Session.FreePlay || Session.CampaignMission > 1);
            navButtons.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            bool canFf = active && nav.CanFastForward;
            var ext = nav != null ? nav.Extender : null;
            clockMode = !canFf && ext != null && !nav.Autopilot && !approach && !nav.Jumping;
            fastForward.style.visibility = canFf || clockMode ? Visibility.Visible : Visibility.Hidden;
            pill.style.visibility = fastForward.style.visibility;   // Hud::draw 11: the pill only behind the FF / clock icon
            if (!canFf) fastForwardPressed = false;
            if (clockMode)
            {
                Image(fastForward, ext.Running || ext.Flashing ? clockOn : clockOff);
                fastForward.EnableInClassList("nav-button--dim", !ext.Ready && !ext.Running);   // tinted while not ready
            }
            else
            {
                fastForward.EnableInClassList("nav-button--dim", false);
                Image(fastForward, fastForwardPressed && nav.FastForward ? fastForwardOn : fastForwardOff);
            }
            Image(autopilotButton, active || nav != null && nav.MenuOpen ? autopilotOn : autopilotOff);
        }

        // By the translate, not left / top: the markers move every frame, and left / top ran the panel's layout each time.
        static void Place(VisualElement e, float x, float y) => e.style.translate = new Translate(x, y);
    }
}
