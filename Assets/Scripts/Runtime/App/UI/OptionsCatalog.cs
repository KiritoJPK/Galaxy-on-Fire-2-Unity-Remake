// OptionsCatalog.cs
// Every player option as data (label, kind, range, get / set on Settings), in the order the menus show them. The main
// menu's Options panel (one tab per page) and the in-flight pause menu build their rows from it (OptionControl).
// Original options (Reference/research/mainmenu_notes.md 2.3 / 2.6): Music 34, FX 35, Voice 36, Brightness 503
// (513-515), Quality 504 (507-509, descriptions 510-512), Sensitivity 499, Invert controls 500, Default settings 497.
// The rest are remake options (Localization.Extra texts). Not here: touch / accelerometer steering and its
// calibration (490-494; tilt isn't built), the text language (the Language tab's buttons, with the voice language row under them).

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.UI
{
    public enum OptionPage { Sound, Graphics, Controls, Gameplay, Language, Bindings }
    public enum OptionKind { Slider, Toggle, Choice, Button, Binding }

    public sealed class OptionDef
    {
        public string id;
        public OptionPage page;
        public OptionKind kind;
        public Func<string> label;
        public Func<string> description;   // optional line under the row
        public Func<UnityEngine.UIElements.VisualElement> extra;   // optional element under the row (not focusable)
        public bool inGameOnly;            // only in a running game's menus (pause / station), not the main menu
        public bool menuOnly;              // only in the main menu (no game loaded), not the pause / station menus
        public Func<bool> visible;         // optional: the row shows only while true (OptionControl.Refresh)
        public Func<bool> enabled;         // optional: the row is greyed out and skipped by the keys / controller while false

        // Slider
        public float min, max;
        public Func<float> get;
        public Action<float> set;
        public Func<float, string> format;

        // Toggle
        public Func<bool> getBool;
        public Action<bool> setBool;

        // Choice
        public Func<string[]> choices;
        public Func<int> getIndex;
        public Action<int> setIndex;
        public bool segmented;

        // Button
        public Action action;

        // Binding (a key bindings row, BindingRow)
        public Flight.ControlRow control;
    }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class OptionsCatalog
    {
        static string X(string key, string english) => Localization.Extra(key, english);

        /// <summary>The save folder row's press: the main menu asks and runs the folder picker (MainMenu.ChooseSaveFolder).</summary>
        public static Action SaveFolderPressed;
        static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)} %";

        /// <summary>Remake (Modding.ModFlight): who sets the flight style now instead of the Free flight toggle.</summary>
        static string FlightStyleForcedNote()
        {
            string who = Modding.ModFlight.ForcedBy switch
            {
                "event" => X("flightForcedEvent", "an event"),
                "session" => X("flightForcedSession", "the multiplayer session"),
                "ship" => X("flightForcedShip", "the ship you fly"),
                "campaign" => X("flightForcedCampaign", "this mod campaign"),
                _ => X("flightForcedOption", "a game option of a mod"),
            };
            string style = FlightStyles.Current == FlightStyles.Free ? X("flightStyleFree", "Free flight (EVERSPACE 2 style)") : X("flightStyleOriginal", "Original");
            return string.Format(X("flightForcedNote", "Now set by {0}: {1}."), who, style);
        }

        public static string PageTitle(OptionPage page) => page switch
        {
            OptionPage.Sound => X("tabSound", "Sound"),
            OptionPage.Graphics => Localization.Get(502),
            OptionPage.Controls => Localization.Get(498),
            OptionPage.Bindings => X("tabBindings", "Key bindings"),
            OptionPage.Language => Localization.Get(0),
            _ => X("tabGameplay", "Gameplay"),
        };

        /// <summary>Remake (players' request): "Default settings" for one tab only, the choice the menus offer next to every
        /// tab (Settings' key groups; the Key bindings tab resets the bindings).</summary>
        public static void ResetPage(OptionPage page)
        {
            switch (page)
            {
                case OptionPage.Sound: Settings.Reset(Settings.SoundKeys); break;
                case OptionPage.Graphics: Settings.Reset(Settings.GraphicsKeys); break;
                case OptionPage.Controls: Settings.Reset(Settings.ControlsKeys); break;
                case OptionPage.Gameplay: Settings.Reset(Settings.GameplayKeys); break;
                case OptionPage.Language: Settings.Reset(Settings.LanguageKeys); break;
                case OptionPage.Bindings: Flight.GameControls.ResetToDefaults(); break;
            }
        }

        /// <summary>The defaults prompt's question and its two reset buttons' texts.</summary>
        public static string ResetQuestion => X("resetQuestion", "Reset to the default settings:");
        public static string ResetTabLabel(OptionPage page) => string.Format(X("resetTab", "Only {0}"), PageTitle(page));
        public static string ResetAllLabel => X("resetAll", "All tabs");

        static readonly string[] VoiceCodes = { "auto", "en", "de" };
        static readonly float[] RenderScales = { 0.5f, 0.67f, 0.75f, 0.8f, 0.9f, 1f, 1.25f, 1.5f, 2f };
        static readonly int[] MsaaSamples = { 1, 2, 4, 8 };

        /// <summary>The options of this platform, page by page.</summary>
        public static List<OptionDef> All()
        {
            var list = new List<OptionDef>
            {
                // ---- sound
                Slider("master", OptionPage.Sound, () => X("masterVolume", "Master"), 0f, 1f, () => Settings.MasterVolume, v => Settings.MasterVolume = v, Percent),
                Slider("music", OptionPage.Sound, () => Localization.Get(34), 0f, 1f, () => Settings.MusicVolume, v => Settings.MusicVolume = v, Percent),
                Slider("sfx", OptionPage.Sound, () => Localization.Get(35), 0f, 1f, () => Settings.SfxVolume, v => Settings.SfxVolume = v, Percent),
                Slider("voice", OptionPage.Sound, () => Localization.Get(36), 0f, 1f, () => Settings.VoiceVolume, v => Settings.VoiceVolume = v, Percent),
                // Remake: the original sounds only the first primary gun (Player::calcWeaponSounds).
                Toggle("eachWeaponSound", OptionPage.Sound, () => X("eachWeaponSound", "Every weapon's own shot sound"),
                    () => Settings.EachWeaponSound, v => Settings.EachWeaponSound = v),
                // Remake: the voices apart from the text language (only English and German were recorded); on the Language tab.
                Choice("voiceLanguage", OptionPage.Language, () => X("voiceLanguage", "Voice language"), true,
                    () => new[] { X("voiceAuto", "As text"), "English", "Deutsch" },
                    () => Array.IndexOf(VoiceCodes, Settings.VoiceLanguage) is var i && i >= 0 ? i : 0,
                    i => Settings.VoiceLanguage = VoiceCodes[i]),
            };

            // Remake (desktop; phones pause in the background): silent while the window isn't focused.
            if (!Application.isMobilePlatform)
                list.Add(Toggle("muteInBackground", OptionPage.Sound, () => X("muteInBackground", "Mute in background"),
                    () => Settings.MuteInBackground, v => Settings.MuteInBackground = v));

            // ---- graphics
            if (Bootstrap.HasDisplayOptions)
            {
                list.Add(Choice("displayMode", OptionPage.Graphics, () => X("displayMode", "Display mode"), true,
                    () => new[] { X("borderless", "Borderless"), X("fullscreen", "Fullscreen"), X("windowed", "Windowed") },
                    () => (int)Settings.DisplayMode, i => Settings.DisplayMode = (DisplayMode)i));
                var sizes = Bootstrap.Resolutions();
                list.Add(Choice("resolution", OptionPage.Graphics, () => X("resolution", "Resolution"), false,
                    () => sizes.Select(r => $"{r.x} × {r.y}").Prepend(X("resolutionNative", "Display's own")).ToArray(),
                    () => { int i = sizes.IndexOf(Settings.Resolution); return i < 0 ? 0 : i + 1; },
                    i => Settings.Resolution = i <= 0 ? Vector2Int.zero : sizes[i - 1]));
            }
            list.Add(Choice("frameRate", OptionPage.Graphics, () => X("frameRate", "Frame rate"), true,
                () => new[] { "30", "60", "120", X("fpsUncapped", "UNCAPPED"), X("fpsVSync", "V-SYNC") },
                () => (int)Settings.FrameRate, i => Settings.FrameRate = (FrameRate)i));
            list.Add(Choice("quality", OptionPage.Graphics, () => Localization.Get(504), true,
                () => new[] { Localization.Get(507), Localization.Get(508), Localization.Get(509) },
                () => Settings.Quality, i => Settings.Quality = i,
                () => QualityDescription(Settings.Quality)));
            // DLSS / FSR 2+ pick their own render resolution (the Upscaler quality below): no render scale while one is on.
            // MetalFX keeps the render scale (its input resolution; URP clamps it into MetalFX Temporal's supported range).
            bool DlssOrFsrOn() => Bootstrap.ActiveUpscaler == Settings.UpscalerDlss || Bootstrap.ActiveUpscaler == Settings.UpscalerFsrTemporal;
            var renderScale = Choice("renderScale", OptionPage.Graphics, () => X("renderScale", "Render scale"), false,
                () => RenderScales.Select(Percent).ToArray(),
                () => Nearest(RenderScales, Settings.RenderScale > 0f ? Settings.RenderScale : Bootstrap.DefaultRenderScale),
                i => Settings.RenderScale = RenderScales[i]);
            renderScale.visible = () => !DlssOrFsrOn();
            list.Add(renderScale);
            // Only the upscalers this device runs (Android: FSR 1 needs GLES 3.1 / Vulkan, STP Vulkan; DLSS / FSR 2+: Windows
            // builds with the upscaler framework, the GPU and graphics API they need; MetalFX: macOS / iOS on Metal, UpscalerFramework). Asked again on every
            // refresh: DLSS / FSR are only known once URP has made its pipeline (the first frame), and the main menu builds
            // its rows before that.
            List<int> Upscalers()
            {
                var l = new List<int> { Settings.UpscalerOff };
                if (Bootstrap.FsrSupported) l.Add(Settings.UpscalerFsr);
                if (Bootstrap.StpSupported) l.Add(Settings.UpscalerStp);
                if (Bootstrap.DlssSupported) l.Add(Settings.UpscalerDlss);
                if (Bootstrap.FsrTemporalSupported) l.Add(Settings.UpscalerFsrTemporal);
                if (Bootstrap.MetalFxSpatialSupported) l.Add(Settings.UpscalerMetalFxSpatial);
                if (Bootstrap.MetalFxTemporalSupported) l.Add(Settings.UpscalerMetalFxTemporal);
                return l;
            }
            var upscalers = Upscalers();
            string UpscalerName(int u) => u switch
            {
                Settings.UpscalerFsr => "FSR 1",
                Settings.UpscalerStp => "STP",
                Settings.UpscalerDlss => "DLSS",
                Settings.UpscalerFsrTemporal => UpscalerFramework.BestFsrLabel ?? "FSR",
                Settings.UpscalerMetalFxSpatial => "MetalFX",
                Settings.UpscalerMetalFxTemporal => "MetalFX Temporal",
                _ => X("off", "Off"),
            };
            if (upscalers.Count > 1 || UpscalerFramework.Compiled)
                list.Add(Choice("upscaler", OptionPage.Graphics, () => X("upscaler", "Upscaler"), true,
                    () => Upscalers().Select(UpscalerName).ToArray(),
                    () => Math.Max(0, Upscalers().IndexOf(Bootstrap.ActiveUpscaler)),
                    i => { var l = Upscalers(); Settings.Upscaler = l[Math.Clamp(i, 0, l.Count - 1)]; },
                    () => Bootstrap.ActiveUpscaler switch
                    {
                        Settings.UpscalerFsr => X("upscalerFsr", "AMD FidelityFX Super Resolution 1: sharp upscaling from the render scale"),
                        Settings.UpscalerStp => X("upscalerStp", "Unity Spatial-Temporal Post-processing: temporal anti-aliasing and upscaling, replaces MSAA"),
                        Settings.UpscalerDlss => X("upscalerDlss", "NVIDIA DLSS: AI upscaling and anti-aliasing (DLAA at Native), replaces MSAA; the quality sets its resolution"),
                        Settings.UpscalerFsrTemporal => string.Format(X("upscalerFsrTemporal", "AMD {0}: temporal upscaling and anti-aliasing, replaces MSAA; the quality sets its resolution"),
                            UpscalerFramework.BestFsrLabel ?? "FSR"),
                        Settings.UpscalerMetalFxSpatial => X("upscalerMetalFxSpatial", "Apple MetalFX Spatial: sharp upscaling from the render scale"),
                        Settings.UpscalerMetalFxTemporal => X("upscalerMetalFxTemporal", "Apple MetalFX Temporal: temporal anti-aliasing and upscaling from the render scale, replaces MSAA"),
                        _ => X("upscalerOff", "Plain scaling from the render scale"),
                    }));
            // DLSS / FSR 2+: the render resolution by quality mode, in place of the render scale; only while one of them is on (in
            // builds with the framework).
            if (UpscalerFramework.Compiled)
            {
                var quality = Choice("upscalerQuality", OptionPage.Graphics, () => X("upscalerQuality", "Upscaler quality"), false,
                    () => new[] { X("upscalerNative", "Native (DLAA / native AA)"), X("upscalerQ", "Quality"), X("upscalerB", "Balanced"),
                                  X("upscalerP", "Performance"), X("upscalerUP", "Ultra performance") },
                    () => Settings.UpscalerQuality, i => Settings.UpscalerQuality = i,
                    () => X("upscalerQualityHelp", "The resolution DLSS / FSR render at, from native down to a third (replaces the render scale)"));
                quality.visible = DlssOrFsrOn;
                list.Add(quality);
            }
            // MSAA with a temporal upscaler on (STP, DLSS, FSR 2+, MetalFX Temporal): its anti-aliasing takes the place, so the row
            // is greyed out (a player's request: with DLAA it read as if it did something) and says why.
            var msaa = Choice("msaa", OptionPage.Graphics, () => X("antiAliasing", "Anti-aliasing"), true,
                () => new[] { X("off", "Off"), "MSAA 2×", "MSAA 4×", "MSAA 8×" },
                () => Bootstrap.IsTemporal(Bootstrap.ActiveUpscaler) ? 0
                    : Math.Max(0, Array.IndexOf(MsaaSamples, Settings.Msaa > 0 ? Settings.Msaa : Bootstrap.DefaultMsaa)),
                i =>
                {
                    if (i > 0 && Bootstrap.IsTemporal(Bootstrap.ActiveUpscaler)) Settings.Upscaler = Settings.UpscalerOff;
                    Settings.Msaa = MsaaSamples[i];
                },
                () => Bootstrap.IsTemporal(Bootstrap.ActiveUpscaler)
                    ? X("msaaByUpscaler", "The upscaler does the anti-aliasing: turn it off to use MSAA") : "");
            msaa.enabled = () => !Bootstrap.IsTemporal(Bootstrap.ActiveUpscaler);
            list.Add(msaa);
            list.Add(Choice("brightness", OptionPage.Graphics, () => Localization.Get(503), true,
                () => new[] { Localization.Get(513), Localization.Get(514), Localization.Get(515) },
                () => Settings.Brightness, i => Settings.Brightness = i));
            // Remake: the remake's bloom (the HDR glow of lights and effects), the original's (every bright pixel, ClassicBloomPass)
            // or both (#45).
            list.Add(Choice("bloom", OptionPage.Graphics, () => X("bloom", "Bloom"), true,
                () => new[] { X("off", "Off"), X("bloomRemake", "Remake"), X("bloomOriginal", "Original"), X("bloomBoth", "Both") },
                () => Mathf.Clamp(Settings.BloomStyle, 0, 3), i => Settings.BloomStyle = i));
            // Remake: the hangar ships' contact shadows (HangarShipShadow); fewer for slower devices.
            list.Add(Choice("hangarShadows", OptionPage.Graphics, () => X("hangarShadows", "Hangar ship shadows"), true,
                () => new[] { X("off", "Off"), X("hangarShadowsPlayer", "Player ship only"), X("hangarShadowsAll", "All ships") },
                () => Settings.HangarShadows, i => Settings.HangarShadows = i));
            var hangarDof = Toggle("hangarDof", OptionPage.Graphics, () => X("hangarDof", "Hangar depth of field"),
                () => Settings.HangarDepthOfField, v => Settings.HangarDepthOfField = v);
            hangarDof.description = () => X("hangarDofHelp", "In the hangar the camera focuses on your ship and the room behind it goes soft. Not in the original.");
            list.Add(hangarDof);
            list.Add(Toggle("lensFlare", OptionPage.Graphics, () => X("lensFlare", "Lens flare"), () => Settings.LensFlare, v => Settings.LensFlare = v));
            list.Add(Toggle("npcPlayerEngines", OptionPage.Graphics, () => X("npcPlayerEngines", "Other ships' engines like yours"),
                () => Settings.NpcPlayerEngines, v => Settings.NpcPlayerEngines = v));
            list.Add(Slider("fov", OptionPage.Graphics, () => X("fov", "Field of view"), 55f, 95f,
                () => Settings.FieldOfView, v => Settings.FieldOfView = Mathf.Round(v), v => $"{Mathf.RoundToInt(v)}°"));
            list.Add(Slider("cameraShake", OptionPage.Graphics, () => X("cameraShake", "Camera shake"), 0f, 1f,
                () => Settings.CameraShake, v => Settings.CameraShake = v, Percent));
            list.Add(Choice("chaseCamera", OptionPage.Graphics, () => X("chaseCamera", "Chase camera"), true,
                () => new[] { X("chaseCameraRemake", "Remake"), X("chaseCameraOriginal", "Original") },
                () => Settings.OriginalChaseCamera ? 1 : 0, i => Settings.OriginalChaseCamera = i == 1,
                () => X("chaseCameraHelp", "Remake: the camera stays close behind the ship, which stays near the middle of the screen. Original: the camera follows more slowly, as in the original game: in a turn the ship swings out to the side and the crosshair moves off centre.")));
            list.Add(Toggle("showFps", OptionPage.Graphics, () => X("showFps", "Show FPS"), () => Settings.ShowFps, v => Settings.ShowFps = v));
            // Remake (#137): the counter in any corner or the middle of any side, so it can stay clear of what a player watches.
            var fpsPosition = Choice("fpsPosition", OptionPage.Graphics, () => X("fpsPosition", "FPS counter position"), false,
                () => new[]
                {
                    X("fpsPosTopLeft", "Top left"), X("fpsPosTop", "Top"), X("fpsPosTopRight", "Top right"),
                    X("fpsPosLeft", "Left"), X("fpsPosRight", "Right"),
                    X("fpsPosBottomLeft", "Bottom left"), X("fpsPosBottom", "Bottom"), X("fpsPosBottomRight", "Bottom right"),
                },
                () => Settings.FpsPosition, i => Settings.FpsPosition = i,
                () => X("fpsPositionHelp", "Where on the screen the FPS counter sits: a corner, or the middle of a side."));
            fpsPosition.visible = () => Settings.ShowFps;
            list.Add(fpsPosition);

            // ---- controls
            // Remake: free flight after EVERSPACE 2 (Settings.FlightStyle, FlightModel.StepFree); it switches the key layout too.
            // A toggle: off = the original's flight and key layout, untouched.
            var freeFlight = Toggle("flightStyle", OptionPage.Controls, () => X("flightStyleFree", "Free flight (EVERSPACE 2 style)"),
                () => Settings.FlightStyle == FlightStyles.Free, v => Settings.FlightStyle = v ? FlightStyles.Free : FlightStyles.Original);
            freeFlight.description = () => (FlightStyles.Forced ? FlightStyleForcedNote() + " " : "") + X("flightStyleHelp", "Original: a throttle, the ship always flies forward. Free flight, after EVERSPACE 2: hold W / S to thrust, A / D to strafe, Space / Ctrl for up and down, Q / E to roll and Shift to boost; the mouse aims, Z toggles the inertial dampeners (off: the ship keeps drifting), Alt held looks around. It has its own key layout (Key bindings). Keyboard, mouse and controller; touch keeps the original.");
            list.Add(freeFlight);
            // MenuTouchWindow state 8: 490 Touch / 491 Accelerometer pictures (options[0x11]), 492 Steering Calibration
            // (493, then OK stores the device's position), the sensitivity slider per mode (+0x14 / +0x18).
            if (Flight.TiltSteering.Available)
            {
                list.Add(Choice("steering", OptionPage.Controls, () => X("steering", "Steering"), true,
                    () => new[] { Localization.Get(490), Localization.Get(491) },
                    () => Settings.TiltSteering ? 1 : 0, i =>
                    {
                        bool tilt = i == 1;
                        if (tilt && !Settings.TiltCalibrated) Flight.TiltSteering.Calibrate();   // remake: calibrate on first use
                        Settings.TiltSteering = tilt;
                    }));
                list.Add(new OptionDef
                {
                    id = "calibrate", page = OptionPage.Controls, kind = OptionKind.Button, label = () => Localization.Get(492),
                    description = () => Localization.Get(493), action = Flight.TiltSteering.Calibrate,
                });
                list.Add(Slider("tiltSensitivity", OptionPage.Controls, () => Localization.Get(499) + " (" + Localization.Get(491) + ")", 0f, 1f,
                    () => Settings.TiltSensitivity, v => Settings.TiltSensitivity = v, v => Mathf.RoundToInt(v * 100f).ToString()));
            }
            list.Add(Slider("sensitivity", OptionPage.Controls, () => Localization.Get(499), 0.2f, 2.2f,
                () => Settings.Sensitivity, v => Settings.Sensitivity = v, v => v.ToString("0.0")));
            // Remake: the touch fire button's double-press autofire for the keys, the mouse and the controller.
            list.Add(Toggle("keyAutofire", OptionPage.Controls, () => X("keyAutofire", "Double-press fire for auto-fire"),
                () => Settings.KeyAutofire, v => Settings.KeyAutofire = v));
            // Remake (#37): Level out levels the nose too (the original: the roll only).
            list.Add(Toggle("levelPitch", OptionPage.Controls, () => X("levelPitch", "Level out also levels the nose"),
                () => Settings.LevelPitch, v => Settings.LevelPitch = v));
            // options[0x10] "Invert controls" (500), split per axis; the mining drill has its own pair.
            list.Add(Toggle("invert", OptionPage.Controls, () => X("invertY", "Invert up / down"), () => Settings.InvertPitch, v => Settings.InvertPitch = v));
            list.Add(Toggle("invertYaw", OptionPage.Controls, () => X("invertX", "Invert left / right"), () => Settings.InvertYaw, v => Settings.InvertYaw = v));
            list.Add(Toggle("invertDrillY", OptionPage.Controls, () => X("invertDrillY", "Mining drill: invert up / down"), () => Settings.InvertDrillY, v => Settings.InvertDrillY = v));
            list.Add(Toggle("invertDrillX", OptionPage.Controls, () => X("invertDrillX", "Mining drill: invert left / right"), () => Settings.InvertDrillX, v => Settings.InvertDrillX = v));
            // Remake (#121): the cameras turned round the ship (free look, Action Freeze, Inspect ship) have their own pair.
            var orbitY = Toggle("invertOrbitY", OptionPage.Controls, () => X("invertOrbitY", "Camera views: invert up / down"),
                () => Settings.InvertOrbitY, v => Settings.InvertOrbitY = v);
            orbitY.description = () => X("invertOrbitHelp", "The cameras you turn around the ship: free look, Action Freeze, Inspect ship and the Mod Builder, by drag, stick or keys. Separate from the flight controls' inversion.");
            list.Add(orbitY);
            var orbitX = Toggle("invertOrbitX", OptionPage.Controls, () => X("invertOrbitX", "Camera views: invert left / right"),
                () => Settings.InvertOrbitX, v => Settings.InvertOrbitX = v);
            orbitX.description = orbitY.description;
            list.Add(orbitX);
            if (Flight.ControllerGyro.Supported)
            {
                list.Add(Toggle("gyroSteering", OptionPage.Controls, () => X("gyroSteering", "Controller gyro (DualSense, DualShock 4, Switch Pro)"),
                    () => Settings.GyroSteering, v => { Settings.GyroSteering = v; Flight.ControllerGyro.Recenter(); }));
                list.Add(Slider("gyroSensitivity", OptionPage.Controls, () => X("gyroSensitivity", "Gyro sensitivity"), 0.25f, 3f,
                    () => Settings.GyroSensitivity, v => Settings.GyroSensitivity = v, v => v.ToString("0.00")));
            }
            if (!Application.isMobilePlatform)
            {
                list.Add(Toggle("mouseSteering", OptionPage.Controls, () => X("mouseSteering", "Mouse steering"), () => Settings.MouseSteering, v => Settings.MouseSteering = v));
                // Remake: a dead zone around the centre, so a mouse near the middle leaves the ship flying straight.
                var mouseDeadzone = Slider("mouseDeadzone", OptionPage.Controls, () => X("mouseDeadzone", "Mouse steering dead zone"), 0f, 0.3f,
                    () => Settings.MouseDeadzone, v => Settings.MouseDeadzone = v, Percent);
                mouseDeadzone.description = () => X("mouseDeadzoneHelp", "How far the mouse can move from the centre before the ship turns.");
                list.Add(mouseDeadzone);
            }
            // Remake (#61): the original's Configure screen (Options > Controls, 494 / 495) moved the touch controls' left
            // group and right cluster up and down; here as two sliders, shown when the touch controls are in use.
            foreach (bool right in new[] { false, true })
            {
                bool r = right;
                var touchHeight = Slider(r ? "touchRightHeight" : "touchLeftHeight", OptionPage.Controls,
                    () => r ? X("touchRightHeight", "Touch controls height: fire buttons") : X("touchLeftHeight", "Touch controls height: stick"), 0f, 1f,
                    () => { float v = r ? Settings.TouchRightHeight : Settings.TouchLeftHeight; return v < 0f ? TouchControls.DefaultHeight01(r) : v; },
                    v => { if (r) Settings.TouchRightHeight = v; else Settings.TouchLeftHeight = v; }, Percent);
                touchHeight.description = () => X("touchHeightHelp", "How high the touch controls sit on the screen: 0 % is the highest, 100 % the lowest.");
                touchHeight.visible = () => Application.isMobilePlatform || InputMode.Current == InputKind.Touch;
                list.Add(touchHeight);
            }
            // Remake VR: the cockpit's grabbable stick (right grip) and throttle lever (left grip), else the controllers as a gamepad.
            var vrGrab = Toggle("vrGrabControls", OptionPage.Controls, () => X("vrGrabControls", "VR flight: grab the stick and throttle"),
                () => Settings.VrGrabControls, v => Settings.VrGrabControls = v);
            vrGrab.visible = () => Vr.VrMode.Enabled;
            list.Add(vrGrab);
            // Remake: haptic feedback (Haptics), the controller's rumble and the phone's vibration; moving it plays a sample.
            var haptics = Slider("haptics", OptionPage.Controls, () => X("haptics", "Vibration"), 0f, 1f,
                () => Settings.HapticsIntensity, v => { Settings.HapticsIntensity = v; Flight.Haptics.Preview(); },
                v => v <= 0f ? X("off", "Off") : Percent(v));
            haptics.description = () => Application.isMobilePlatform
                ? X("hapticsHelpMobile", "Phone vibration (touch controls) and controller rumble: hits, collisions, explosions, missiles, boost, jumps and mining.")
                : X("hapticsHelp", "Controller rumble: hits, collisions, explosions, shots, boost, jumps and mining.");
            list.Add(haptics);
            list.Add(Slider("deadzone", OptionPage.Controls, () => X("deadzone", "Stick dead zone"), 0.05f, 0.4f,
                () => Settings.StickDeadzone, v => Settings.StickDeadzone = v, Percent));
            // Remake: the controller hints' button icons (InputMode.Style, PadGlyph): the controller in use, or always one style.
            list.Add(Choice("padGlyphs", OptionPage.Controls, () => X("padGlyphs", "Controller button icons"), true,
                () => new[] { X("padGlyphsAuto", "Auto"), "Xbox", "PlayStation" },
                () => Settings.PadGlyphs, i => Settings.PadGlyphs = i,
                () => X("padGlyphsHelp", "Auto: PlayStation buttons (cross, circle, square, triangle) while a DualSense or DualShock is in use, Xbox buttons for any other controller.")));
            // Remake: every flight control rebindable (GameControls): two keyboard / mouse keys and a controller button each.
            list.Add(new OptionDef
            {
                id = "resetBindings", page = OptionPage.Bindings, kind = OptionKind.Button,
                label = () => X("resetBindings", "Reset key bindings"),
                // Which layout the list shows: each flight style has its own (GameControls.ApplyStyle).
                description = () =>
                {
                    Flight.GameControls.ApplyStyle();   // the layout of the style in use (a mod's or an event's too)
                    return string.Format(X("bindingsLayout", "Key layout: {0}. The Free flight toggle under {1} switches it."),
                                         FlightStyles.Current == FlightStyles.Free ? X("flightStyleFree", "Free flight (EVERSPACE 2 style)")
                                                                                    : X("flightStyleOriginal", "Original"),
                                         PageTitle(OptionPage.Controls))
                           + (FlightStyles.Forced ? " " + FlightStyleForcedNote() : "")
                           + " " + X("bindingsHelp", "Keys, second keys and controller buttons: pick one to change it. Esc (or the controller's B, or a tap) cancels; nothing pressed for 10 seconds cancels too. To clear one: its ×, a right click, Delete (or the controller's X) on the selected row, or Backspace while it waits for a key. The menu keys stay fixed.");
                },
                action = Flight.GameControls.ResetToDefaults,
                extra = BindingRow.Header,
            });
            foreach (var row in Flight.GameControls.Rows)
                list.Add(new OptionDef { id = "bind_" + row.id, page = OptionPage.Bindings, kind = OptionKind.Binding, label = row.label, control = row,
                                         visible = row.visible });   // rows only one flight style reads

            // ---- gameplay
            list.Add(Toggle("launchCamera", OptionPage.Gameplay, () => X("launchCamera", "Launch and arrival camera"),
                () => Settings.LaunchCamera, v => Settings.LaunchCamera = v));
            list.Add(Toggle("hangarFlights", OptionPage.Gameplay, () => X("hangarFlights", "Hangar arrival and take-off"),
                () => Settings.HangarFlights, v => Settings.HangarFlights = v));
            list.Add(Toggle("tutorialHints", OptionPage.Gameplay, () => X("tutorialHints", "Tutorials"),
                () => Settings.TutorialHints, v => Settings.TutorialHints = v));
            var storyStep = Toggle("showStoryStep", OptionPage.Gameplay, () => X("showStoryStep", "Show story step"),
                () => Settings.ShowStoryStep, v => Settings.ShowStoryStep = v);
            storyStep.description = () => X("showStoryStepHelp", "The current story step at the bottom right of the screen. Handy for bug reports.");
            list.Add(storyStep);
            var dyingCargo = Toggle("dyingCargoMarkers", OptionPage.Gameplay, () => X("dyingCargoMarkers", "Salvage markers on dying ships"),
                () => Settings.DyingCargoMarkers, v => Settings.DyingCargoMarkers = v);
            dyingCargo.description = () => X("dyingCargoMarkersHelp", "A ship carrying cargo shows the container markers while it tumbles before exploding, as in the original. Off: no markers; aim at it and the tractor beam still pulls its cargo.");
            list.Add(dyingCargo);
            var cloakLoses = Toggle("cloakLosesPursuers", OptionPage.Gameplay, () => X("cloakLosesPursuers", "Cloak shakes off pursuers"),
                () => Settings.CloakLosesPursuers, v => Settings.CloakLosesPursuers = v);
            cloakLoses.description = () => X("cloakLosesPursuersHelp", "Enemy ships lose track of you while you're cloaked: they fly to where they last saw you, then go back to their patrol. Off: as in the original, they keep chasing and aiming at you but don't fire.");
            list.Add(SessionRule(cloakLoses));
            var empCloak = Toggle("empBreaksCloak", OptionPage.Gameplay, () => X("empBreaksCloak", "EMP breaks cloaks"),
                () => Settings.EmpBreaksCloak, v => Settings.EmpBreaksCloak = v);
            empCloak.description = () => X("empBreaksCloakHelp", "An EMP hit on a cloaked ship drops its cloak at once, and a cloak still charging fails: yours, the Specters' and other players'. Off: as in the original, nothing ends a cloak early.");
            list.Add(SessionRule(empCloak));
            var smartTargeting = Toggle("smarterNpcTargeting", OptionPage.Gameplay, () => X("smarterNpcTargeting", "Smarter NPC targeting"),
                () => Settings.SmarterNpcTargeting, v => Settings.SmarterNpcTargeting = v);
            smartTargeting.description = () => X("smarterNpcTargetingHelp", "Enemy fighters attack the nearest hostile ship, you a little likelier, and spread out over their targets instead of all going for you. Off: as in the original, every enemy fighter in range goes for you first.");
            list.Add(SessionRule(smartTargeting));
            // Remake (players' report: fighters hard to hit since the mesh collision): every hull's real shape, the flyable ships
            // on the original's hit cube, or the original's volumes everywhere (Settings.ShipCollision, Flight.HullCollision.Mode).
            list.Add(SessionRule(Choice("shipCollision", OptionPage.Gameplay, () => X("shipCollision", "Ship collision"), true,
                () => new[] { X("shipCollisionExact", "Exact shape"), X("shipCollisionMixed", "Mixed"), X("shipCollisionOriginal", "Original") },
                () => Settings.ShipCollision, i => Settings.ShipCollision = i,
                () => X("shipCollisionHelp", "Exact shape: shots hit every ship and object on its real shape, so a near miss misses, and you bump into other players' hulls. Mixed: fighters, your ship and other players' ships take the hits on a box around them, as in the original game, which makes them easier to hit, and other players' ships are spheres; freighters, battleships, carriers, stations and other objects keep their real shape. Original: as in the original game, every ship, station and object uses the original's boxes and spheres."))));
            var pirateEvents = Toggle("pirateEvents", OptionPage.Gameplay, () => X("pirateEvents", "Pirate outposts and bosses"),
                () => Settings.PirateEvents, v => Settings.PirateEvents = v);
            pirateEvents.description = () => X("pirateEventsHelp", "Now and then an orbit holds a pirate outpost with its guards or a pirate boss with escorts; destroying them pays a bounty. Not in the original.");
            list.Add(SessionRule(pirateEvents));
            var capitalShips = Toggle("capitalShips", OptionPage.Gameplay, () => X("capitalShips", "Capital ship enhancements"),
                () => Settings.CapitalShips, v => Settings.CapitalShips = v);
            capitalShips.description = () => X("capitalShipsHelp", "Battleships and carriers get escorts and stronger turrets; the carrier and the Vossk battleship can be destroyed for loot, the carrier launches Inflicts when attacked and lets trusted pilots dock to resupply. Not in the original.");
            list.Add(SessionRule(capitalShips));
            var kaamoStacking = Toggle("kaamoStacking", OptionPage.Gameplay, () => X("kaamoStacking", "Stackable Kaamo Club upgrades"),
                () => Settings.KaamoStacking, v => Settings.KaamoStacking = v);
            kaamoStacking.description = () => string.Format(X("kaamoStackingHelp", "The Kaamo Club's mechanics fit their upgrade again and again, each level costing twice the last, up to level {0}. Off: one of each, as in the original. Not in the original."),
                Settings.KaamoMaxLevel);
            list.Add(SessionRule(kaamoStacking));
            // Remake: the stacking's top level (Session.MaxModLevel), a session rule too (-kaamomaxlevel, /set kaamomaxlevel).
            var kaamoLevel = Choice("kaamoMaxLevel", OptionPage.Gameplay, () => X("kaamoMaxLevel", "Kaamo Club upgrade limit"), false,
                () => Enumerable.Range(SessionRules.MinKaamoLevel, SessionRules.MaxKaamoLevel - SessionRules.MinKaamoLevel + 1).Select(n => n.ToString()).ToArray(),
                () => Settings.KaamoMaxLevel - SessionRules.MinKaamoLevel, i => Settings.KaamoMaxLevel = SessionRules.MinKaamoLevel + i,
                () => X("kaamoMaxLevelHelp", "How many times each upgrade can be fitted to one ship."));
            kaamoLevel.visible = () => Settings.KaamoStacking;
            list.Add(SessionRule(kaamoLevel));
            var kaamoGear = Toggle("kaamoKeepsEquipment", OptionPage.Gameplay, () => X("kaamoKeepsEquipment", "Stored ships keep their equipment"),
                () => Settings.KaamoKeepsEquipment, v => Settings.KaamoKeepsEquipment = v);
            kaamoGear.description = () => X("kaamoKeepsEquipmentHelp", "A ship you park in the Kaamo Club keeps its weapons, turrets and equipment, and they are back on it when you fly it again. Off: they move to the ship you take, as in the original. Ships traded in elsewhere always hand theirs over.");
            list.Add(SessionRule(kaamoGear));
            var cloakBay = Toggle("cloakBay", OptionPage.Gameplay, () => X("cloakBay", "Cloak bay on cloaking ships"),
                () => Settings.CloakBay, v => Settings.CloakBay = v);
            cloakBay.description = () => X("cloakBayHelp", "The Specter and the Scimitar have a cloak built in. A cloak you mount on them replaces it without taking an equipment slot. Off: it takes a slot, as in the original.");
            list.Add(SessionRule(cloakBay));
            // #28: the Informer mission's rule for other ships dying after the spy.
            list.Add(SessionRule(Choice("informerRule", OptionPage.Gameplay, () => X("informerRule", "Informer missions"), true,
                () => new[] { X("informerRemake", "Remake"), X("informerOriginal", "Original") },
                () => Settings.InformerOriginalRule ? 1 : 0, i => Settings.InformerOriginalRule = i == 1,
                () => X("informerRuleHelp", "Original: any other ship destroyed in the spy's orbit before you dock fails the mission, even after the spy is dead. Remake: once the spy is dead, other kills no longer count."))));
            list.Add(Choice("targetLock", OptionPage.Gameplay, () => X("targetLock", "Target lock"), true,
                () => new[] { X("targetLockSmart", "Smart"), X("targetLockOriginal", "Original") },
                () => Settings.OriginalTargetLock ? 1 : 0, i => Settings.OriginalTargetLock = i == 1,
                () => X("targetLockPriorityHelp", "Smart: hostile ships are locked first, then the one nearest the crosshair. Original: the first ship in the box is locked, whoever it is, and any new lock replaces the old one.") + " "
                      + X("targetLockBeams", "With Smart, beam lasers also aim only at hostile ships, or at the ship you have locked.") + " "
                      + X("targetLockUnlock", "The Unlock target key (Key bindings, U by default) drops the lock; on touch, tap the lock plate.")));
            // Remake (players' suggestion): the Smart lock's hold on an enemy, its own option (CombatRadar, TargetLockRules).
            var keepEnemy = Toggle("keepEnemyLock", OptionPage.Gameplay, () => X("keepEnemyLock", "Keep enemy lock"),
                () => Settings.KeepEnemyLock, v => Settings.KeepEnemyLock = v);
            keepEnemy.description = () => X("keepEnemyLockHelp", "While a hostile ship is locked, a neutral or friendly ship passing through the crosshair can't take the lock (your missiles stay on your target). Off: you can lock and scan any ship during a battle. Only with the Smart target lock.");
            keepEnemy.visible = () => !Settings.OriginalTargetLock;
            list.Add(keepEnemy);
            // Remake mods: NPC fighters in the mods' ships of their race (Modding.ModTraffic).
            list.Add(Choice("modShipTraffic", OptionPage.Gameplay, () => X("modShipTraffic", "Mod ships in traffic"), true,
                () => new[] { X("modShipTrafficOff", "Off"), "10 %", "25 %", "50 %" },
                () => Settings.ModShipTraffic, i => Settings.ModShipTraffic = i,
                () => X("modShipTrafficHelp", "How often an NPC fighter flies one of your mods' ships of its race instead of the game's. Their hulls and guns stay those of the game's fighters.")));
            list.Add(Toggle("autoAdvance", OptionPage.Gameplay, () => X("autoPlayDialogue", "Auto-play dialogue"),
                () => Settings.AutoAdvanceDialogue, v => Settings.AutoAdvanceDialogue = v));
            list.Add(Toggle("animatedDialogue", OptionPage.Gameplay, () => X("animatedDialogue", "Animated dialogue"),
                () => Settings.AnimatedDialogue, v => Settings.AnimatedDialogue = v));
            list.Add(Toggle("inputHints", OptionPage.Gameplay, () => X("inputHintsFlight", "Button hints in flight"),
                () => Settings.InputHints, v => Settings.InputHints = v));
            // Remake (immersion): the flight HUD's markings Full / Minimal / Off (FlightHud); also the "Toggle HUD" key (F1).
            list.Add(Choice("hudMode", OptionPage.Gameplay, () => X("hudMode", "Flight HUD"), true,
                () => new[] { X("hudFull", "Full"), X("hudMinimal", "Minimal"), X("hudOff", "Off") },
                () => Settings.HudMode, i => Settings.HudMode = i,
                () => X("hudModeHelp", "Minimal: only the crosshair, the action prompt, warnings and messages. Off: only messages, for maximum immersion. Menus, conversations and the radio always show. The Toggle HUD key (F1 by default) switches it in flight.")));
            // Remake (players' request): a far ship's marker that leaves the ship in view, and its distance (CombatView,
            // FarShipMarkers).
            list.Add(Choice("farShipMarkers", OptionPage.Gameplay, () => X("farShipMarkers", "Far ship markers"), true,
                () => new[] { X("farMarkerOriginal", "Original"), X("farMarkerRing", "Ring"), X("farMarkerSmallDot", "Small dot") },
                () => Settings.FarShipMarkers, i => Settings.FarShipMarkers = i,
                () => X("farShipMarkersHelp", "How a distant ship is marked on screen. Original: a filled dot over the ship. Ring: a hollow ring around it. Small dot: a small dot just above it. Both leave the ship in view; a locked ship gets a bracket around it.")));
            var markerDistance = Toggle("markerDistance", OptionPage.Gameplay, () => X("markerDistance", "Distance on ship markers"),
                () => Settings.MarkerDistance, v => Settings.MarkerDistance = v);
            markerDistance.description = () => X("markerDistanceHelp", "Shows the distance under every distant ship's marker, not only under the locked ship's.");
            list.Add(markerDistance);
            // Remake: Discord Rich Presence (DiscordPresence), desktop only.
            if (!Application.isMobilePlatform)
                list.Add(Toggle("discordPresence", OptionPage.Gameplay, () => X("discordPresence", "Show what I'm doing in Discord"),
                    () => Settings.DiscordPresence, v => Settings.DiscordPresence = v));
            // Remake: the difficulty of the game in progress (the new game's panel sets it first; saved with the game). NPC
            // hulls and guns, raiders and the Extreme rules follow from the next orbit or docking. Not in multiplayer: every
            // session plays on Normal.
            if (!Multiplayer.NetGame.Active)
            {
                float[] levels = { Session.DifficultyEasy, Session.DifficultyNormal, Session.DifficultyHard, Session.DifficultyExtreme };
                var difficulty = Choice("difficulty", OptionPage.Gameplay, () => X("difficulty", "Difficulty"), true,
                    () => levels.Select(Session.DifficultyName).ToArray(),
                    () => Nearest(levels, Session.Difficulty), i => Session.Difficulty = levels[Mathf.Clamp(i, 0, levels.Length - 1)],
                    () => X("difficultyHelp", "Changes the game in progress; enemies and the Extreme rules follow from the next orbit or docking."));
                difficulty.inGameOnly = true;
                list.Add(difficulty);
            }
            // Remake: the testing tools (Cheats.Unlocked), also opened by F10, LB + RB or three fingers on the main menu.
            var debug = Toggle("debugTools", OptionPage.Gameplay, () => X("debugTools", "Debug tools"), () => Cheats.Unlocked, v => Cheats.Unlocked = v);
            debug.description = () => X("debugToolsHelp", "A Debug button in the main menu (the mission select) and a Debug page in the pause and station menus (cheats, items, spawns).");
            debug.visible = () => Cheats.Allowed;   // not in a multiplayer session that doesn't allow the Debug menu
            list.Add(debug);
            // Remake (a player's request): the saves also kept in a folder the player picks, for file managers and sync tools
            // (Data.SaveFolder); main menu only: no game is loaded there. The main menu runs the choice (SaveFolderPressed).
            if (SaveFolder.Supported)
                list.Add(new OptionDef
                {
                    id = "saveFolder", page = OptionPage.Gameplay, kind = OptionKind.Button, menuOnly = true,
                    label = () => string.Format(X("saveFolder", "Save folder: {0}"),
                        SaveFolder.Chosen ? SaveFolder.DisplayName : X("saveFolderDefault", "this device only")),
                    description = () => SaveFolder.LastProblem != null
                        ? string.Format(X("saveFolderUnreachable", "The folder can't be reached ({0}). Your saves stay on this device and go to the folder once it is back."), SaveFolder.LastProblem)
                        : X("saveFolderHelp", "Keeps your saves and ship presets in a folder you choose as well, where file managers and sync tools (Syncthing) reach them, so the game moves between devices. The game always keeps its own copies too."),
                    action = () => SaveFolderPressed?.Invoke(),
                });
            return list;
        }

        /// <summary>510-512 "Low quality:\n- Smoke off\n- Fog off\n- Detail low" as one line: "Smoke off · Fog off · Detail low".</summary>
        static string QualityDescription(int quality)
        {
            var lines = Localization.Get(510 + quality).Replace("\r", "").Split('\n').Skip(1)
                .Select(l => l.TrimStart('-', ' ').Trim()).Where(l => l.Length > 0);
            return string.Join("  ·  ", lines);
        }

        static int Nearest(float[] values, float v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
            return best;
        }

        static OptionDef Slider(string id, OptionPage page, Func<string> label, float min, float max, Func<float> get, Action<float> set,
                                Func<float, string> format) =>
            new OptionDef { id = id, page = page, kind = OptionKind.Slider, label = label, min = min, max = max, get = get, set = set, format = format };

        /// <summary>A Gameplay option that is the session's rule in multiplayer (NetRules): its row hides for a player in
        /// someone else's session (the host or the server decides; a player host changes it for everyone).</summary>
        static OptionDef SessionRule(OptionDef d)
        {
            var shown = d.visible;
            d.visible = () => !Multiplayer.NetRules.ClientLocked && (shown == null || shown());
            return d;
        }

        static OptionDef Toggle(string id, OptionPage page, Func<string> label, Func<bool> get, Action<bool> set) =>
            new OptionDef { id = id, page = page, kind = OptionKind.Toggle, label = label, getBool = get, setBool = set };

        static OptionDef Choice(string id, OptionPage page, Func<string> label, bool segmented, Func<string[]> choices, Func<int> get,
                                Action<int> set, Func<string> description = null) =>
            new OptionDef
            {
                id = id, page = page, kind = OptionKind.Choice, label = label, segmented = segmented, choices = choices, getIndex = get,
                setIndex = set, description = description,
            };
    }
}
