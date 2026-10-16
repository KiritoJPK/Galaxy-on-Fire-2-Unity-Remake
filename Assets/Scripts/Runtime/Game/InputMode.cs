// InputMode.cs
// Which kind of input the player is using right now: touch, keyboard/mouse or a controller. Switches on the
// first real input of another kind (a touch, a key press, a mouse move or click, a button or a stick pushed past a
// dead zone), on phones and PCs alike, so UI can show touch controls, keyboard hints or controller hints.
// Starts as Touch on mobile, Gamepad if one is connected on other platforms, else KeyboardMouse.
// Remake: the controller hints' button style (PadStyle): the controller last used picks it (a Sony pad = PlayStation, any
// other = Xbox), unless Options > Controls "Controller button icons" (Settings.PadGlyphs) forces one.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using GoF2Remake.Data;

namespace GoF2Remake.UI
{
    public enum InputKind { Touch, KeyboardMouse, Gamepad }

    /// <summary>How the controller hints label the buttons: Xbox (A / B / X / Y, LB / RB, LT / RT, View / Menu) or
    /// PlayStation (cross / circle / square / triangle, L1 / R1, L2 / R2, Create or Share / Options).</summary>
    public enum PadStyle { Xbox, PlayStation }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class InputMode
    {
        const float StickThreshold = 0.35f;   // sticks drift; only count a deliberate push
        const float MouseThreshold = 4f;      // pixels per event

        public static InputKind Current { get; private set; }

        /// <summary>A real keyboard was used this run (always on desktops): the key bindings' keyboard cells only take a
        /// capture then. A phone without one waited for a key that never came.</summary>
        public static bool KeyboardSeen { get; private set; }

        /// <summary>Raised on the main thread when Current changes.</summary>
        public static event Action Changed;

        /// <summary>The style of the controller last used (Xbox until a Sony one is used).</summary>
        public static PadStyle DetectedStyle { get; private set; }

        /// <summary>The controller last used is a DualShock 4: its Create button is called Share.</summary>
        public static bool DualShock4 { get; private set; }

        /// <summary>The controller hints' style: the option's (Settings.PadGlyphs), on Auto the controller last used's.</summary>
        public static PadStyle Style => Settings.PadGlyphs switch
        {
            Settings.PadGlyphsXbox => PadStyle.Xbox,
            Settings.PadGlyphsPlayStation => PadStyle.PlayStation,
            _ => DetectedStyle,
        };

        /// <summary>Raised on the main thread when Style (or the Share / Create name) changes: another controller was used
        /// or the option changed. The pad glyphs redraw themselves; GameControls passes it on as its Changed.</summary>
        public static event Action StyleChanged;

        static InputDevice lastPad;
        static PadStyle shownStyle;
        static bool shownDs4;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Current = Application.isMobilePlatform ? InputKind.Touch
                    : Gamepad.current != null ? InputKind.Gamepad : InputKind.KeyboardMouse;
            KeyboardSeen = !Application.isMobilePlatform;
            lastPad = Gamepad.current;
            DetectedStyle = StyleOf(lastPad);
            DualShock4 = IsDualShock4(lastPad);
            shownStyle = Style;
            shownDs4 = DualShock4;
            InputSystem.onEvent -= OnEvent;   // domain reload may be off: never subscribe twice
            InputSystem.onEvent += OnEvent;
            Settings.Changed -= CheckStyle;
            Settings.Changed += CheckStyle;   // the option (and Default settings)
        }

        /// <summary>The button style of a controller: PlayStation for Sony's (the Input System's DualShock / DualSense
        /// layouts on every platform, or a pad that reports Sony as its maker, e.g. an Android one without its own layout),
        /// else Xbox. Null = Xbox.</summary>
        public static PadStyle StyleOf(InputDevice device) =>
            device == null ? PadStyle.Xbox : StyleOf(device.layout, device.description.manufacturer, device.description.product);

        /// <summary>StyleOf for a layout name and the device's reported maker / product (testable without a device).</summary>
        public static PadStyle StyleOf(string layout, string manufacturer, string product)
        {
            if (!string.IsNullOrEmpty(layout) && BasedOn(layout, "DualShockGamepad")) return PadStyle.PlayStation;
            if (Has(manufacturer, "Sony")) return PadStyle.PlayStation;
            if (Has(product, "DualSense") || Has(product, "DualShock") || Has(product, "PlayStation")) return PadStyle.PlayStation;
            return PadStyle.Xbox;
        }

        /// <summary>A DualShock 4 (Share), not a DualSense (Create).</summary>
        public static bool IsDualShock4(InputDevice device) =>
            device != null && IsDualShock4(device.layout, device.description.product);

        public static bool IsDualShock4(string layout, string product) =>
            (!string.IsNullOrEmpty(layout) && layout.IndexOf("DualShock4", StringComparison.OrdinalIgnoreCase) >= 0)
            || Has(product, "DualShock 4") || Has(product, "DualShock4");

        static bool Has(string s, string part) => !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        static bool BasedOn(string layout, string baseLayout)
        {
            try { return string.Equals(layout, baseLayout, StringComparison.OrdinalIgnoreCase) || InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout); }
            catch (ArgumentException) { return false; }   // a layout the Input System doesn't know
        }

        static void UsedPad(InputDevice device)
        {
            if (device == lastPad) return;
            lastPad = device;
            DetectedStyle = StyleOf(device);
            DualShock4 = IsDualShock4(device);
            CheckStyle();
        }

        static void CheckStyle()
        {
            var style = Style;
            if (style == shownStyle && DualShock4 == shownDs4) return;
            shownStyle = style;
            shownDs4 = DualShock4;
            StyleChanged?.Invoke();
        }

        static void OnEvent(InputEventPtr e, InputDevice device)
        {
            if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) return;
            InputKind kind;
            switch (device)
            {
                case Touchscreen _:
                    kind = InputKind.Touch;
                    break;
                case Gamepad _:
                    if (!HasControlAbove(e, device, StickThreshold)) return;
                    kind = InputKind.Gamepad;
                    UsedPad(device);
                    break;
                case Keyboard keyboard:
                    if (!HasControlAbove(e, device, 0.5f)) return;
                    kind = InputKind.KeyboardMouse;
                    // Android's back button arrives as Escape: only another key shows a real keyboard.
                    if (!KeyboardSeen)
                        foreach (var c in e.EnumerateChangedControls(device, 0.5f))
                            if (c != keyboard.escapeKey) { KeyboardSeen = true; break; }
                    break;
                case Mouse mouse:
                    // Touches on some platforms also arrive as a simulated mouse: ignore those.
                    if (Current == InputKind.Touch && Touchscreen.current != null && Touchscreen.current.primaryTouch.isInProgress) return;
                    if (!MouseUsed(e, mouse)) return;
                    kind = InputKind.KeyboardMouse;
                    break;
                default:
                    return;
            }
            if (kind == Current) return;
            Current = kind;
            Changed?.Invoke();
        }

        /// <summary>True if the event sets any control of the device to at least this magnitude (a pressed button = 1).</summary>
        static bool HasControlAbove(InputEventPtr e, InputDevice device, float threshold)
        {
            foreach (var control in e.EnumerateChangedControls(device, threshold)) return true;
            return false;
        }

        static bool MouseUsed(InputEventPtr e, Mouse mouse)
        {
            if (mouse.delta.ReadValueFromEvent(e, out Vector2 d) && d.sqrMagnitude >= MouseThreshold * MouseThreshold) return true;
            if (mouse.leftButton.ReadValueFromEvent(e, out float l) && l > 0.5f) return true;
            return mouse.rightButton.ReadValueFromEvent(e, out float r) && r > 0.5f;
        }
    }
}
