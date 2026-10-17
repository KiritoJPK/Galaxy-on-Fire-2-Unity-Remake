// PhotoTools.cs
// Remake-only: what turns the two orbit views around the player's ship into photo modes, shared by the flight's Action
// Freeze (PhotoMode) and the hangar's Inspect ship (StationMenu); players' suggestion "Everspace-style Action Freeze"
// (2026-10-17):
//   Free camera     the camera leaves the orbit and flies anywhere within reach of the ship (World.FreeCamera): F, the
//                   controller's X or the hint toggles it; W A S D move, E / Space up, Q / C down, Shift faster, Alt
//                   slower, a drag or the arrows look, the wheel / + - / triggers zoom the lens; controller: the left stick
//                   moves, the right one looks, RB / LB up / down, the left stick press faster; touch: a finger looks, a
//                   pinch moves forward / back, two fingers dragged slide the camera.
//   Camera settings a panel at the left (Tab, the controller's View or the hint): orbit / free, field of view, roll, the
//                   free camera's speed, depth of field (the game's, off, on the ship, a focus distance) with its blur,
//                   exposure, contrast, saturation, vignette, film grain, hide the ship, a thirds grid, a frame (16:9,
//                   21:9, 4:3, 1:1, 9:16: the screenshot is cut to it) and the screenshot's size (the screen's, x2 or
//                   x4), Reset; PhotoSettings keeps them for the run.
//   Effects         a global URP volume of its own on layer 26 at priority 1000 (over the game's and the hangar's depth of
//                   field), its profile made in code: URP keeps the effects' shaders in a build only because
//                   Assets/Settings/PhotoModeShaders.asset has them on.
//   Screenshot      Capture: the overlay hidden, the screen's picture (at twice its size: ScreenCapture's superSize), cut
//                   to the frame, stored like the screenshots before (PhotoMode.Store).
// The hosts keep their own orbit input and overlay (title, hints, Back, message) and ask this for the rest.

using System;
using System.Collections;
using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public sealed class PhotoTools
    {
        const int EffectsLayer = 26;            // StationLevel's depth of field layer: only the scene cameras see it
        const float MoveSpeed = 15f;            // m/s at speed x1
        const float LookPerPixel = 0.0035f;     // rad per 1080p pixel of a drag
        const float LookKeys = 1.2f;            // rad/s, the arrows
        const float LookStick = 1.8f;           // rad/s, a stick pushed fully

        readonly VisualElement panel, frame, frameBox, gridLines;
        readonly VisualElement[] bars = new VisualElement[4];
        readonly List<OptionControl> rows = new List<OptionControl>();
        readonly Action<bool> sound;
        int selected;

        Camera cam;
        Transform ship;
        Func<Vector3> shipCentre;
        float shipRadius, freeRange, floorY, savedFov, savedNear;
        bool hangar, active, flightCamera;
        readonly World.FreeCamera free = new World.FreeCamera();
        Func<(Vector3 pos, Quaternion rot)> orbitPose;

        GameObject volumeGo;
        VolumeProfile profile;
        DepthOfField dof;
        ColorAdjustments color;
        Vignette vignette;
        FilmGrain grain;
        UniversalAdditionalCameraData camData;
        int savedMask;

        bool shipHidden;

        bool dragging;
        bool pressOverUi, wasPressed;   // a press that began on the panel or a button never turns the camera
        readonly VisualElement notice;
        readonly Label noticePath;
        readonly Button noticeButton;
        string savedPath;
        float noticeMs;
        const float NoticeMs = 8000f;
        float dragTravel;
        /// <summary>A short click / tap on the view with the free camera (no drag).</summary>
        public event Action Tapped;
        Vector2 lastPointer;
        float pinchSpan = -1f;
        Vector2 pinchMid;

        /// <summary>The panel is open: the arrows / sticks / Enter are its own.</summary>
        public bool PanelOpen { get; private set; }
        public bool Free => active && PhotoSettings.Free;
        /// <summary>The orbit / free switch or the panel changed: the host rebuilds its hints.</summary>
        public event Action Changed;

        /// <summary>'overlay' is the host's photo overlay (Hide UI hides its children: the panel and the frame go with it);
        /// 'sound' the button sounds (true: press, false: release).</summary>
        public PhotoTools(VisualElement overlay, Action<bool> sound)
        {
            this.sound = sound;
            frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("photo-frame");
            for (int i = 0; i < 4; i++) { bars[i] = new VisualElement { pickingMode = PickingMode.Ignore }; bars[i].AddToClassList("photo-frame-bar"); frame.Add(bars[i]); }
            frameBox = new VisualElement { pickingMode = PickingMode.Ignore };
            frameBox.AddToClassList("photo-frame-box");
            gridLines = new VisualElement { pickingMode = PickingMode.Ignore };
            gridLines.AddToClassList("photo-grid");
            for (int i = 0; i < 4; i++)
            {
                var l = new VisualElement { pickingMode = PickingMode.Ignore };
                l.AddToClassList(i < 2 ? "photo-grid-v" : "photo-grid-h");
                if (i < 2) l.style.left = Length.Percent(i == 0 ? 33.333f : 66.667f); else l.style.top = Length.Percent(i == 2 ? 33.333f : 66.667f);
                gridLines.Add(l);
            }
            frameBox.Add(gridLines);
            frame.Add(frameBox);
            frame.RegisterCallback<GeometryChangedEvent>(_ => LayoutFrame());
            overlay.Insert(0, frame);   // under the title, hints and footer

            panel = new VisualElement();
            panel.AddToClassList("photo-panel");
            var title = new Label(T("photoSettings", "CAMERA SETTINGS")) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("photo-panel-title");
            title.AddToClassList("gof-semibold");
            panel.Add(title);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("photo-panel-scroll");
            panel.Add(scroll);
            foreach (var d in Defs())
            {
                var c = new OptionControl(d);
                c.Field.AddToClassList("option-row");
                c.Field.focusable = false;   // the panel's own highlight (Select), as the pause menu's options
                c.Changed += () => { Refresh(); if (d.id == "mode") Changed?.Invoke(); };
                rows.Add(c);
                scroll.Add(c.Root);
            }
            overlay.Add(panel);

            // After a screenshot: where it went, and (desktop) a button / O to show it in the file browser.
            notice = new VisualElement { pickingMode = PickingMode.Ignore };
            notice.AddToClassList("photo-saved");
            noticePath = new Label { pickingMode = PickingMode.Ignore };
            noticePath.AddToClassList("photo-saved-path");
            notice.Add(noticePath);
            noticeButton = new Button(ShowSaved) { text = T("photoShowInFolder", "SHOW IN FOLDER"), focusable = false };
            noticeButton.AddToClassList("photo-saved-button");
            noticeButton.AddToClassList("gof-semibold");
            noticeButton.RegisterCallback<PointerDownEvent>(_ => sound?.Invoke(true), TrickleDown.TrickleDown);
            notice.Add(noticeButton);
            overlay.Add(notice);
            notice.style.display = DisplayStyle.None;
            ShowPanel(false);
        }

        static string T(string key, string english) => Localization.Extra(key, english);
        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100f)} %";
        static string Signed(float v, string unit = "") => (v > 0f ? "+" : "") + v.ToString(Mathf.Abs(v) < 10f && unit == " EV" ? "0.0" : "0") + unit;

        IEnumerable<OptionDef> Defs()
        {
            yield return new OptionDef { id = "mode", kind = OptionKind.Choice, segmented = true, label = () => T("photoCamera", "Camera"),
                choices = () => new[] { T("photoOrbit", "Orbit"), T("photoFree", "Free") },
                getIndex = () => PhotoSettings.Free ? 1 : 0, setIndex = i => SetFree(i == 1) };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoFov", "Field of view"), min = PhotoSettings.MinFov, max = PhotoSettings.MaxFov,
                get = () => PhotoSettings.Fov, set = v => PhotoSettings.Fov = v, format = v => $"{Mathf.RoundToInt(v)}°" };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoRoll", "Roll"), min = -45f, max = 45f,
                get = () => PhotoSettings.Roll, set = v => PhotoSettings.Roll = Mathf.Abs(v) < 0.75f ? 0f : v, format = v => $"{Mathf.RoundToInt(v)}°" };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoSpeed", "Free camera speed"), min = 0.1f, max = 5f,
                get = () => PhotoSettings.Speed, set = v => PhotoSettings.Speed = v, format = v => $"x{v:0.0}", visible = () => PhotoSettings.Free };
            yield return new OptionDef { kind = OptionKind.Choice, label = () => T("photoDof", "Depth of field"),
                choices = () => new[] { T("photoDofGame", "Default"), T("photoOff", "Off"), T("photoDofShip", "On the ship"), T("photoDofManual", "Manual") },
                getIndex = () => (int)PhotoSettings.Dof, setIndex = i => PhotoSettings.Dof = (PhotoSettings.DofMode)i };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoFocus", "Focus distance"), min = 0f, max = 1f,
                get = () => PhotoSettings.Focus, set = v => PhotoSettings.Focus = v, format = _ => Metres(PhotoSettings.FocusMetres),
                visible = () => PhotoSettings.Dof == PhotoSettings.DofMode.Manual };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoBlur", "Blur"), min = 0f, max = 1f,
                get = () => PhotoSettings.Blur, set = v => PhotoSettings.Blur = v, format = Pct,
                visible = () => PhotoSettings.Dof >= PhotoSettings.DofMode.Ship };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoExposure", "Exposure"), min = -3f, max = 3f,
                get = () => PhotoSettings.Exposure, set = v => PhotoSettings.Exposure = Mathf.Abs(v) < 0.05f ? 0f : v, format = v => Signed(Mathf.Round(v * 10f) / 10f, " EV") };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoContrast", "Contrast"), min = -50f, max = 50f,
                get = () => PhotoSettings.Contrast, set = v => PhotoSettings.Contrast = Mathf.Abs(v) < 1f ? 0f : v, format = v => Signed(Mathf.Round(v)) };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoSaturation", "Saturation"), min = -100f, max = 100f,
                get = () => PhotoSettings.Saturation, set = v => PhotoSettings.Saturation = Mathf.Abs(v) < 1f ? 0f : v, format = v => Signed(Mathf.Round(v)) };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoVignette", "Vignette"), min = 0f, max = 0.6f,
                get = () => PhotoSettings.Vignette, set = v => PhotoSettings.Vignette = v, format = v => Pct(v / 0.6f) };
            yield return new OptionDef { kind = OptionKind.Slider, label = () => T("photoGrain", "Film grain"), min = 0f, max = 1f,
                get = () => PhotoSettings.Grain, set = v => PhotoSettings.Grain = v, format = Pct };
            yield return new OptionDef { kind = OptionKind.Toggle, label = () => T("photoHideShip", "Hide the ship"),
                getBool = () => PhotoSettings.HideShip, setBool = v => PhotoSettings.HideShip = v };
            yield return new OptionDef { kind = OptionKind.Toggle, label = () => T("photoGrid", "Grid (thirds)"),
                getBool = () => PhotoSettings.Grid, setBool = v => PhotoSettings.Grid = v };
            yield return new OptionDef { kind = OptionKind.Choice, label = () => T("photoFrame", "Frame"),
                choices = () => { var c = (string[])PhotoSettings.FrameNames.Clone(); c[0] = T("photoFrameFull", "Full screen"); return c; },
                getIndex = () => PhotoSettings.Frame, setIndex = i => PhotoSettings.Frame = i };
            yield return new OptionDef { kind = OptionKind.Choice, segmented = true, label = () => T("photoSize", "Screenshot size"),
                choices = () => new[] { T("photoSizeScreen", "Screen"), "x2", "x4" },
                getIndex = () => PhotoSettings.Size, setIndex = i => PhotoSettings.Size = i };
            yield return new OptionDef { kind = OptionKind.Button, label = () => T("photoReset", "RESET"),
                action = () => { PhotoSettings.Reset(); Refresh(); Changed?.Invoke(); } };
        }

        static string Metres(float m) => m >= 1000f ? $"{m / 1000f:0.0} km" : $"{Mathf.RoundToInt(m)} m";

        // ---- the session -----------------------------------------------------------------------------------------

        /// <summary>A photo mode opens on 'camera' around 'shipRoot' ('centre' its middle, 'radius' its size, m); the free
        /// camera stays within 'range' metres of the ship and above 'floor'; 'pose' is the orbit camera's pose (the free camera
        /// starts there). 'hangarMode': the hangar sets the lens itself (HangarInspect), flight here.</summary>
        public void Begin(Camera camera, Transform shipRoot, Func<Vector3> centre, float radius, float range, float floor,
                          Func<(Vector3, Quaternion)> pose, bool hangarMode)
        {
            End();
            cam = camera;
            ship = shipRoot;
            shipCentre = centre;
            shipRadius = Mathf.Max(1f, radius);
            freeRange = range;
            floorY = floor;
            orbitPose = pose;
            hangar = hangarMode;
            active = cam != null;
            if (!active) return;
            savedFov = cam.fieldOfView;
            savedNear = cam.nearClipPlane;
            flightCamera = !hangar;
            PhotoSettings.DefaultFov = PhotoSettings.Fov = Mathf.Clamp(savedFov, PhotoSettings.MinFov, PhotoSettings.MaxFov);
            if (PhotoSettings.Free) StartFree();
            SetupEffects();
            ShowPanel(false);
            Refresh();
        }

        public void End()
        {
            if (!active) return;
            active = false;
            ShowPanel(false);
            ShowShip(true);
            DestroyEffects();
            if (cam != null && flightCamera) { cam.fieldOfView = savedFov; cam.nearClipPlane = savedNear; }
            cam = null;
            ship = null;
        }

        void SetFree(bool on)
        {
            PhotoSettings.Free = on;
            if (on && active) StartFree();
        }

        void StartFree()
        {
            var (pos, rot) = orbitPose != null ? orbitPose() : (cam.transform.position, cam.transform.rotation);
            free.Begin(pos, rot, shipCentre != null ? shipCentre() : pos, freeRange, floorY);
        }

        public void ToggleFree()
        {
            sound?.Invoke(false);
            SetFree(!PhotoSettings.Free);
            Refresh();
            Changed?.Invoke();
        }

        public void ToggleSettings()
        {
            sound?.Invoke(false);
            ShowPanel(!PanelOpen);
            Changed?.Invoke();
        }

        void ShowPanel(bool open)
        {
            PanelOpen = open;
            panel.EnableInClassList("photo-panel--open", open);
            if (open) { selected = Mathf.Clamp(selected, 0, rows.Count - 1); Highlight(); }
        }

        void Refresh()
        {
            foreach (var r in rows) r.Refresh();
            if (selected < rows.Count && !rows[selected].Shown) Move(1);
            Highlight();
            LayoutFrame();
        }

        void Highlight()
        {
            bool keys = InputMode.Current != InputKind.Touch;
            for (int i = 0; i < rows.Count; i++) rows[i].Field.EnableInClassList("options-selected", keys && PanelOpen && i == selected);
            if (keys && PanelOpen && selected < rows.Count && panel.Q<ScrollView>() is ScrollView sv && sv.contentContainer.Contains(rows[selected].Root))
                sv.ScrollTo(rows[selected].Root);
        }

        void Move(int dir)
        {
            for (int n = 0; n < rows.Count; n++)
            {
                selected = (selected + dir + rows.Count) % rows.Count;
                if (rows[selected].Shown) break;
            }
            Highlight();
        }

        /// <summary>A screen point (pixels, y up) over the open panel: a press there isn't a drag of the camera.</summary>
        public bool OverPanel(Vector2 screen)
        {
            if (panel.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel.panel, new Vector2(screen.x, Screen.height - screen.y));
            if (PanelOpen && panel.worldBound.Contains(p)) return true;
            return notice.resolvedStyle.display != DisplayStyle.None && noticeButton.resolvedStyle.display != DisplayStyle.None
                   && noticeButton.worldBound.Contains(p);
        }

        /// <summary>The press latch: true while the pointer held now went down on the panel or one of the host's buttons
        /// ('overUi', checked only as the press begins), so dragging a slider off the panel doesn't turn the camera.</summary>
        public bool PressOverUi(Vector2? pressed, Func<Vector2, bool> overUi)
        {
            bool down = pressed.HasValue;
            if (down && !wasPressed) pressOverUi = OverPanel(pressed.Value) || (overUi != null && overUi(pressed.Value));
            if (!down) pressOverUi = false;
            wasPressed = down;
            return pressOverUi;
        }

        // ---- input ------------------------------------------------------------------------------------------------

        /// <summary>The keys this adds, every frame: F / X the free camera, Tab / View the panel; with the panel open its
        /// navigation (and Esc / B close it: 'back' is then taken). Returns true when the host's own keys and controller
        /// input should rest (the panel has them).</summary>
        public bool TickKeys(Keyboard kb, Gamepad pad, out bool backTaken)
        {
            backTaken = false;
            if (!active) return false;
            if (kb != null && kb.oKey.wasPressedThisFrame && notice.resolvedStyle.display != DisplayStyle.None) ShowSaved();
            if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame)) ToggleSettings();
            if (!PanelOpen)
            {
                if ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) ToggleFree();
                return false;
            }
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            { backTaken = true; ToggleSettings(); return true; }
            int move = 0, side = 0;
            if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) move = -1;
            if (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) move = 1;
            if (pad != null && (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame)) move = -1;
            if (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame)) move = 1;
            if (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) side = -1;
            if (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) side = 1;
            if (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame)) side = -1;
            if (pad != null && (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame)) side = 1;
            if (move != 0) Move(move);
            if (selected < rows.Count)
            {
                if (side != 0) rows[selected].Step(side);
                if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    || (pad != null && pad.buttonSouth.wasPressedThisFrame)) rows[selected].Activate();
            }
            return true;
        }

        /// <summary>The free camera's input (real time). 'overControl': a screen point on one of the host's buttons. The
        /// right stick still turns it while the panel is open.</summary>
        public void TickFree(Keyboard kb, Gamepad pad, float dt, Func<Vector2, bool> overControl, out bool turning)
        {
            turning = false;
            if (!Free) return;
            float scale = 1080f / Mathf.Max(1, Screen.height);
            Vector3 move = Vector3.zero;
            Vector2 look = Vector2.zero;
            float speed = MoveSpeed * PhotoSettings.Speed, zoom = 0f;
            bool keys = !PanelOpen;
            if (kb != null && keys)
            {
                move.x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                move.z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                move.y = (kb.eKey.isPressed || kb.spaceKey.isPressed ? 1 : 0) - (kb.qKey.isPressed || kb.cKey.isPressed || kb.leftCtrlKey.isPressed ? 1 : 0);
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) speed *= 4f;
                if (kb.leftAltKey.isPressed) speed *= 0.25f;
                look.x += ((kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0)) * LookKeys * dt;
                look.y += ((kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0)) * LookKeys * dt;
                zoom += ((kb.minusKey.isPressed || kb.numpadMinusKey.isPressed || kb.pageDownKey.isPressed ? 1 : 0)
                       - (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed || kb.pageUpKey.isPressed ? 1 : 0)) * dt * 1.5f;
            }
            if (pad != null)
            {
                var r = pad.rightStick.ReadValue();
                look += new Vector2(r.x, r.y) * LookStick * dt;
                if (keys)
                {
                    var l = pad.leftStick.ReadValue();
                    move.x += l.x;
                    move.z += l.y;
                    move.y += (pad.rightShoulder.isPressed ? 1 : 0) - (pad.leftShoulder.isPressed ? 1 : 0);
                    if (pad.leftStickButton.isPressed) speed *= 4f;
                    zoom += (pad.leftTrigger.ReadValue() - pad.rightTrigger.ReadValue()) * dt * 1.5f;
                }
            }
            // Pointer: the mouse (left or right button) looks toward the drag; a finger grabs the view (the other way);
            // two fingers: a pinch moves forward / back, the pair dragged slides the camera.
            var ts = Touchscreen.current;
            int down = 0;
            if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) down++;
            if (down >= 2)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero; int n = 0;
                foreach (var t in ts.touches) if (t.press.isPressed) { if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue(); n++; }
                float span = (a - b).magnitude * scale;
                var mid = (a + b) * 0.5f * scale;
                if (pinchSpan >= 0f)
                {
                    free.Move(new Vector3(0f, 0f, 1f), (span - pinchSpan) * 0.15f * PhotoSettings.Speed);
                    var d = mid - pinchMid;
                    free.Move(new Vector3(-d.x, -d.y, 0f).normalized, d.magnitude * 0.12f * PhotoSettings.Speed);
                }
                pinchSpan = span;
                pinchMid = mid;
                dragging = false;
                turning = true;
            }
            else
            {
                pinchSpan = -1f;
                Vector2? pos = null;
                bool touch = down == 1;
                if (touch) pos = ts.primaryTouch.position.ReadValue();
                else if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed)) pos = Mouse.current.position.ReadValue();
                bool overUi = PressOverUi(pos, overControl);
                if (pos.HasValue && !overUi)
                {
                    if (!dragging) { dragging = true; lastPointer = pos.Value; dragTravel = 0f; }
                    var px = (pos.Value - lastPointer) * scale;
                    dragTravel += px.magnitude;
                    lastPointer = pos.Value;
                    look += (touch ? -px : px) * LookPerPixel;
                    turning = true;
                }
                else if (dragging)
                {
                    dragging = false;
                    if (dragTravel < 8f) Tapped?.Invoke();   // a short click / tap: the host brings its hidden UI back
                }
            }
            if (Mouse.current != null && (Mouse.current.position.ReadValue() is var mp) && !OverPanel(mp))
            {
                float steps = Mathf.Clamp(Mouse.current.scroll.ReadValue().y, -1f, 1f);
                if (Mathf.Abs(steps) > 0f) zoom -= steps * 0.08f;
            }
            // The look is slower through a long lens, so a zoomed-in view can still be aimed.
            float lens = Mathf.Clamp(PhotoSettings.Fov / 60f, 0.2f, 1.5f);
            free.Look(look.x * lens, look.y * lens);
            free.Move(move, speed * dt);
            if (zoom != 0f) { PhotoSettings.Fov = Mathf.Clamp(PhotoSettings.Fov * Mathf.Exp(zoom), PhotoSettings.MinFov, PhotoSettings.MaxFov); Refresh(); }
        }

        /// <summary>The camera's pose for the free camera with the roll: the flight sets it here, the hangar hands it to
        /// HangarInspect.</summary>
        public (Vector3 pos, Quaternion rot) FreePose => (free.Position, free.Rotation);

        /// <summary>The lens's roll on top of a pose.</summary>
        public static Quaternion Rolled(Quaternion rot) => rot * Quaternion.AngleAxis(PhotoSettings.Roll, Vector3.forward);

        /// <summary>Every frame after the host placed its camera: the flight's lens (field of view, roll, near plane), the
        /// effects, the ship shown or hidden, the frame guides.</summary>
        public void LateTick()
        {
            if (!active || cam == null) return;
            if (flightCamera)
            {
                cam.fieldOfView = PhotoSettings.Fov;
                cam.nearClipPlane = Free ? 0.2f : savedNear;
            }
            UpdateEffects();
            ShowShip(!PhotoSettings.HideShip);
            frame.EnableInClassList("photo-frame--shown", PhotoSettings.FrameAspect > 0f || PhotoSettings.Grid);
            gridLines.style.display = PhotoSettings.Grid ? DisplayStyle.Flex : DisplayStyle.None;
            if (InputMode.Current == InputKind.Touch) Highlight();
            if (noticeMs > 0f && (noticeMs -= Time.unscaledDeltaTime * 1000f) <= 0f) notice.style.display = DisplayStyle.None;
        }

        /// <summary>The saved screenshot's notice: its path, and the button where the system's file browser can show it.</summary>
        void ShowNotice(string path)
        {
            savedPath = path;
            noticePath.text = path.Replace('\\', '/');
            bool canShow = PhotoMode.CanShowInFolder && System.IO.Path.IsPathRooted(path);
            noticeButton.style.display = canShow ? DisplayStyle.Flex : DisplayStyle.None;
            noticeButton.text = T("photoShowInFolder", "SHOW IN FOLDER") + (canShow && InputMode.Current == InputKind.KeyboardMouse ? "  (O)" : "");
            notice.style.display = DisplayStyle.Flex;
            noticeMs = NoticeMs;
        }

        void ShowSaved()
        {
            if (string.IsNullOrEmpty(savedPath) || !PhotoMode.CanShowInFolder) return;
            sound?.Invoke(false);
            PhotoMode.ShowInFolder(savedPath);
        }

        // ---- the ship hidden ----------------------------------------------------------------------------------

        // Moved to the layer VR's cockpit hides the hull on (VrRig.HiddenLayer 28), which the camera then leaves out: turning
        // the renderers off didn't hold, the engine parts are switched on and off every frame (ShipController).
        const int HiddenLayer = 28;
        readonly List<(GameObject go, int layer)> movedLayers = new List<(GameObject, int)>();
        int savedCulling;

        void ShowShip(bool show)
        {
            if (show == !shipHidden || cam == null && !show) return;
            if (!show)
            {
                movedLayers.Clear();
                if (ship != null)
                    foreach (var r in ship.GetComponentsInChildren<Renderer>(true))
                        if (r.gameObject.layer != HiddenLayer) { movedLayers.Add((r.gameObject, r.gameObject.layer)); r.gameObject.layer = HiddenLayer; }
                savedCulling = cam.cullingMask;
                cam.cullingMask &= ~(1 << HiddenLayer);
                shipHidden = true;
            }
            else
            {
                foreach (var (go, layer) in movedLayers) if (go != null) go.layer = layer;
                movedLayers.Clear();
                if (cam != null) cam.cullingMask = savedCulling;
                shipHidden = false;
            }
        }

        // ---- effects ------------------------------------------------------------------------------------------

        void SetupEffects()
        {
            volumeGo = new GameObject("Photo mode effects") { layer = EffectsLayer };
            var v = volumeGo.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 1000f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            dof = profile.Add<DepthOfField>(false);
            color = profile.Add<ColorAdjustments>(false);
            vignette = profile.Add<Vignette>(false);
            grain = profile.Add<FilmGrain>(false);
            vignette.color.Override(Color.black);
            vignette.smoothness.Override(0.45f);
            grain.type.Override(FilmGrainLookup.Medium3);
            v.profile = profile;
            camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null) { savedMask = camData.volumeLayerMask; camData.volumeLayerMask |= 1 << EffectsLayer; }
            UpdateEffects();
        }

        void DestroyEffects()
        {
            if (camData != null) camData.volumeLayerMask = savedMask;
            camData = null;
            if (volumeGo != null) UnityEngine.Object.Destroy(volumeGo);
            if (profile != null) UnityEngine.Object.Destroy(profile);
            volumeGo = null;
            profile = null;
        }

        void UpdateEffects()
        {
            if (profile == null || cam == null) return;
            var mode = PhotoSettings.Dof;
            dof.active = mode != PhotoSettings.DofMode.Game;
            if (dof.active)
            {
                float toShip = shipCentre != null ? Mathf.Max(0.5f, Vector3.Distance(cam.transform.position, shipCentre())) : 50f;
                float blur = PhotoSettings.Blur;
                bool bokeh = mode == PhotoSettings.DofMode.Manual && !Application.isMobilePlatform;
                dof.mode.Override(mode == PhotoSettings.DofMode.Off ? DepthOfFieldMode.Off : bokeh ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Gaussian);
                if (mode == PhotoSettings.DofMode.Ship)
                {
                    // Sharp over the whole hull, the rest behind it blurred (what a long lens at an open aperture would do,
                    // which a physical lens can't up close: HangarInspect's note in StationLevel.UpdateDepthOfField).
                    float start = toShip + shipRadius * 1.1f;
                    dof.gaussianStart.Override(start);
                    dof.gaussianEnd.Override(start + Mathf.Lerp(shipRadius * 30f + 400f, shipRadius * 1.5f + 5f, blur));
                    dof.gaussianMaxRadius.Override(Mathf.Lerp(0.6f, 1.5f, blur));
                    dof.highQualitySampling.Override(true);
                }
                else if (mode == PhotoSettings.DofMode.Manual)
                {
                    float focus = PhotoSettings.FocusMetres;
                    dof.focusDistance.Override(focus);
                    dof.focalLength.Override(Mathf.Lerp(35f, 300f, blur));
                    dof.aperture.Override(Mathf.Lerp(5.6f, 1.4f, blur));
                    dof.gaussianStart.Override(focus);
                    dof.gaussianEnd.Override(focus * Mathf.Lerp(6f, 1.3f, blur) + 2f);
                    dof.gaussianMaxRadius.Override(Mathf.Lerp(0.6f, 1.5f, blur));
                    dof.highQualitySampling.Override(true);
                }
            }
            // Over the game's own brightness (Bootstrap.ApplyPostProcessing's postExposure on the scene volumes).
            color.active = PhotoSettings.Exposure != 0f || PhotoSettings.Contrast != 0f || PhotoSettings.Saturation != 0f;
            color.postExposure.overrideState = PhotoSettings.Exposure != 0f;
            color.postExposure.value = Settings.BrightnessExposure + PhotoSettings.Exposure;
            color.contrast.overrideState = PhotoSettings.Contrast != 0f;
            color.contrast.value = PhotoSettings.Contrast;
            color.saturation.overrideState = PhotoSettings.Saturation != 0f;
            color.saturation.value = PhotoSettings.Saturation;
            vignette.active = PhotoSettings.Vignette > 0f;
            vignette.intensity.Override(PhotoSettings.Vignette);
            grain.active = PhotoSettings.Grain > 0f;
            grain.intensity.Override(PhotoSettings.Grain);
            grain.response.Override(0.8f);
        }

        // ---- the frame guides ---------------------------------------------------------------------------------

        void LayoutFrame()
        {
            var r = frame.contentRect;
            if (r.width <= 1f || r.height <= 1f) return;
            float aspect = PhotoSettings.FrameAspect;
            float w = r.width, h = r.height, fw = w, fh = h;
            if (aspect > 0f) { if (w / h > aspect) fw = h * aspect; else fh = w / aspect; }
            float x = (w - fw) * 0.5f, y = (h - fh) * 0.5f;
            frameBox.style.left = x; frameBox.style.top = y; frameBox.style.width = fw; frameBox.style.height = fh;
            void Bar(VisualElement b, float l, float t, float bw, float bh)
            {
                b.style.left = l; b.style.top = t; b.style.width = Mathf.Max(0f, bw); b.style.height = Mathf.Max(0f, bh);
            }
            Bar(bars[0], 0f, 0f, w, y);                  // above
            Bar(bars[1], 0f, y + fh, w, h - y - fh);     // below
            Bar(bars[2], 0f, y, x, fh);                  // left
            Bar(bars[3], x + fw, y, w - x - fw, fh);     // right
        }

        // ---- the screenshot -----------------------------------------------------------------------------------

        /// <summary>The picture without 'overlay' (hidden for it), cut to the frame, stored; 'done' gets whether it was.</summary>
        public IEnumerator Capture(VisualElement overlay, Action<bool> done)
        {
            overlay.style.visibility = Visibility.Hidden;
            bool fps = FpsCounter.Suppressed;
            FpsCounter.Suppressed = true;
            yield return null;   // a frame drawn without the overlay
            yield return new WaitForEndOfFrame();
            bool ok = false;
            Texture2D shot = null;
            try
            {
                // Twice the size: Unity renders the frame again at 2x (the post-processing, the bloom and the lens flare
                // with it; a URP single camera request lost them).
                shot = ScreenCapture.CaptureScreenshotAsTexture(PhotoSettings.SuperSize(Screen.width, Screen.height));
                var rect = PhotoSettings.FrameRect(shot.width, shot.height, PhotoSettings.FrameAspect);
                if (rect.width != shot.width || rect.height != shot.height)
                {
                    var cut = new Texture2D(rect.width, rect.height, TextureFormat.RGB24, false);
                    cut.SetPixels(shot.GetPixels(rect.x, rect.y, rect.width, rect.height));
                    cut.Apply();
                    UnityEngine.Object.Destroy(shot);
                    shot = cut;
                }
                string saved = PhotoMode.StoreAt(shot.EncodeToPNG(), $"GoF2_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                ok = saved != null;
                if (ok) ShowNotice(saved);
            }
            catch (Exception e) { Debug.LogWarning("Photo mode screenshot: " + e.Message); }
            if (shot != null) UnityEngine.Object.Destroy(shot);
            overlay.style.visibility = StyleKeyword.Null;
            FpsCounter.Suppressed = fps;
            done?.Invoke(ok);
        }
    }
}
