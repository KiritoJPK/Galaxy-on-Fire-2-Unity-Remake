// OrbitViewUi.cs
// Remake: the overlay of the two orbit cameras around the player's ship, the hangar's Inspect ship (StationMenu) and the
// flight's Action Freeze (PhotoMode): a title at the top left, the controls as hints at the top right (Hide UI, Screenshot
// and Back clickable, a tap on touch), the Back button in the footer, "Screenshot saved." above it. Styles: .orbit-view-* in
// GoF2Common.uss, so both panels have them.

using System;
using GoF2Remake.Data;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class OrbitViewUi
    {
        public static Label Title()
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("orbit-view-title");
            l.AddToClassList("gof-semibold");
            return l;
        }

        /// <summary>The footer's Back button; 'push' plays the press sound (the click plays its own).</summary>
        public static Button Back(Action back, Action push)
        {
            var b = new Button(back) { text = Localization.Extra("hudBack", "BACK"), focusable = false };
            b.AddToClassList("orbit-view-back");
            b.AddToClassList("gof-semibold");
            if (push != null) b.RegisterCallback<PointerDownEvent>(_ => push(), TrickleDown.TrickleDown);
            return b;
        }

        public static Label Message()
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("orbit-view-message");
            l.AddToClassList("gof-semibold");
            return l;
        }

        public static VisualElement Hints()
        {
            var h = new VisualElement { pickingMode = PickingMode.Ignore };
            h.AddToClassList("orbit-view-hints");
            return h;
        }

        /// <summary>The controls for this input kind: rotate (the drag and 'rotateKeys'), zoom (the wheel, + / -), Hide UI (H / Y),
        /// Screenshot (Enter / A), Back (Esc / B). The actions play their own release sound; 'push' the press.</summary>
        public static void BuildHints(VisualElement hints, InputKind kind, string[] rotateKeys,
                                      Action hideUi, Action screenshot, Action back, Action push)
        {
            if (hints == null) return;
            hints.Clear();
            string T(string key, string english) => Localization.Extra(key, english);
            void Add(string label, Action click, params VisualElement[] glyphs)
            {
                var h = new VisualElement { pickingMode = click != null ? PickingMode.Position : PickingMode.Ignore };
                h.AddToClassList("hint");
                foreach (var g in glyphs) { g.pickingMode = PickingMode.Ignore; h.Add(g); }
                var l = new Label(label) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("hint-label");
                l.AddToClassList("gof-semibold");
                h.Add(l);
                if (click != null)
                {
                    h.AddToClassList("orbit-view-hint--action");
                    if (push != null) h.RegisterCallback<PointerDownEvent>(_ => push());
                    h.RegisterCallback<ClickEvent>(_ => click());
                }
                hints.Add(h);
            }
            string rotate = T("inspectRotate", "ROTATE"), zoom = T("inspectZoom", "ZOOM"), backText = T("hudBack", "BACK");
            string hide = T("inspectHideUi", "HIDE UI"), shot = T("inspectScreenshot", "SCREENSHOT");
            if (kind == InputKind.KeyboardMouse)
            {
                var keys = new VisualElement[rotateKeys.Length + 1];
                keys[0] = InputGlyph.Key(T("inspectDrag", "DRAG"), true);
                for (int i = 0; i < rotateKeys.Length; i++) keys[i + 1] = InputGlyph.Key(rotateKeys[i]);
                Add(rotate, null, keys);
                Add(zoom, null, InputGlyph.Key(T("inspectWheel", "WHEEL"), true), InputGlyph.Key("+"), InputGlyph.Key("-"));
                Add(hide, hideUi, InputGlyph.Key("H"));
                Add(shot, screenshot, InputGlyph.Key("ENTER", true));
                Add(backText, back, InputGlyph.Key("ESC"));
            }
            else if (kind == InputKind.Gamepad)
            {
                Add(rotate, null, InputGlyph.Pad(PadButton.LeftStick), InputGlyph.Pad(PadButton.RightStick));
                Add(zoom, null, InputGlyph.Pad(PadButton.LeftTrigger), InputGlyph.Pad(PadButton.RightTrigger));
                Add(hide, hideUi, InputGlyph.Pad(PadButton.Y));
                Add(shot, screenshot, InputGlyph.Pad(PadButton.A));
                Add(backText, back, InputGlyph.Pad(PadButton.B));
            }
            else
            {
                Add(T("inspectTouch", "DRAG TO ROTATE · PINCH TO ZOOM"), null);
                Add(hide, hideUi);
                Add(shot, screenshot);
            }
        }

        /// <summary>A screen point (pixels, y up) on the Back button or a clickable hint: a press there isn't a drag.</summary>
        public static bool OverControl(VisualElement root, Button back, VisualElement hints, UnityEngine.Vector2 screen)
        {
            if (root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new UnityEngine.Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            if (back != null && back.resolvedStyle.display != DisplayStyle.None && back.worldBound.Contains(p)) return true;
            if (hints != null)
                foreach (var h in hints.Children())
                    if (h.pickingMode == PickingMode.Position && h.worldBound.Contains(p)) return true;
            return false;
        }
    }
}
