// InputGlyph.cs
// Small UI Toolkit elements that show an input: a keyboard keycap or an Xbox-style controller button (the game
// always uses Xbox labels, whatever pad is connected). Styled by UI/GoF2InputGlyphs.uss (.keycap, .pad-*).

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public enum PadButton { A, B, X, Y, LeftStick, RightStick, DPad, LeftTrigger, RightTrigger, LeftBumper, RightBumper, Menu, View }

    public static class InputGlyph
    {
        /// <summary>The button hints option (Settings.InputHints): hides a HUD's hint row container while it is off.</summary>
        public static void TrackHintsOption(VisualElement hints)
        {
            if (hints == null) return;
            void Apply() => hints.EnableInClassList("hints--off", !Settings.InputHints);
            Apply();
            Settings.Changed += Apply;
            hints.RegisterCallback<DetachFromPanelEvent>(_ => Settings.Changed -= Apply);
        }

        public static VisualElement Key(string text, bool wide = false)
        {
            var l = Text(text, "keycap");
            if (wide) l.AddToClassList("keycap--wide");
            return l;
        }

        public static VisualElement Pad(PadButton button) => button switch
        {
            PadButton.A => Face("A", "pad-face--a"),
            PadButton.B => Face("B", "pad-face--b"),
            PadButton.X => Face("X", "pad-face--x"),
            PadButton.Y => Face("Y", "pad-face--y"),
            PadButton.LeftStick => Text("LS", "pad-stick"),
            PadButton.RightStick => Text("RS", "pad-stick"),
            PadButton.DPad => Text("D-PAD", "pad-menu"),
            PadButton.LeftTrigger => Text("LT", "pad-trigger"),
            PadButton.RightTrigger => Text("RT", "pad-trigger"),
            PadButton.LeftBumper => Text("LB", "pad-bumper"),
            PadButton.RightBumper => Text("RB", "pad-bumper"),
            PadButton.View => Text("VIEW", "pad-menu"),
            _ => Text("MENU", "pad-menu"),
        };

        /// <summary>What an action is bound to right now (GameControls, rebindable) for this input kind: the keyboard's first
        /// slot (the second too with 'both', or when the first is unbound), a composite part by part; the controller's slot.
        /// Empty when nothing is bound (the hint is left out).</summary>
        public static List<VisualElement> For(InputAction action, InputKind kind, bool both = false)
        {
            var list = new List<VisualElement>();
            var row = GameControls.RowOf(action);
            if (row == null || kind == InputKind.Touch) return list;
            if (kind == InputKind.Gamepad)
            {
                if (row.HasSlot(BindSlot.Pad)) foreach (var p in GameControls.Paths(row, BindSlot.Pad)) AddPath(list, p, true);
                return list;
            }
            bool first = GameControls.IsBound(row, BindSlot.Key1);
            if (first) foreach (var p in GameControls.Paths(row, BindSlot.Key1)) AddPath(list, p, false);
            if (!first || both) foreach (var p in GameControls.Paths(row, BindSlot.Key2)) AddPath(list, p, false);
            return list;
        }

        static void AddPath(List<VisualElement> list, string path, bool pad)
        {
            if (string.IsNullOrEmpty(path)) return;
            string name = GameControls.ShortName(path);
            if (!pad) { list.Add(Key(name, name.Length > 3)); return; }
            list.Add(name switch
            {
                "A" => Pad(PadButton.A), "B" => Pad(PadButton.B), "X" => Pad(PadButton.X), "Y" => Pad(PadButton.Y),
                "LT" => Pad(PadButton.LeftTrigger), "RT" => Pad(PadButton.RightTrigger),
                "LB" => Pad(PadButton.LeftBumper), "RB" => Pad(PadButton.RightBumper),
                "LS" => Pad(PadButton.LeftStick), "RS" => Pad(PadButton.RightStick),
                "MENU" => Pad(PadButton.Menu), "VIEW" => Pad(PadButton.View),
                _ => Text(name, name.StartsWith("D-PAD") ? "pad-menu" : "pad-stick"),
            });
        }

        static VisualElement Face(string letter, string colour)
        {
            var l = Text(letter, "pad-face");
            l.AddToClassList(colour);
            return l;
        }

        /// <summary>Glyphs before a button's text: the text moves into a child Label (a Button measures its own text only
        /// while it has no children: with a glyph child it shrank to its padding) laid out in a row with them
        /// (.glyph-button in GoF2InputGlyphs.uss). Returns the label: set the text there from now on.</summary>
        public static Label WithGlyphs(Button button, params VisualElement[] glyphs)
        {
            button.AddToClassList("glyph-button");
            foreach (var g in glyphs)
            {
                g.pickingMode = PickingMode.Ignore;
                g.AddToClassList("glyph-button-glyph");
                button.Add(g);
            }
            var label = new Label(button.text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("glyph-button-label");
            button.text = "";
            button.Add(label);
            return label;
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            l.AddToClassList("gof-semibold");
            return l;
        }
    }
}
