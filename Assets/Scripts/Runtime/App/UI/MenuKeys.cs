// MenuKeys.cs
// Modified: the "i" with a menu's keys and the "?" with what a page is for, as one-line helpers (OptionInfo's icons and
// window). The keys window holds the HUDs' hint boxes (GoF2InputGlyphs.uss .hint, laid out as a list by .hints-popup),
// built each time it opens for the input in use (the keyboard's keys on touch). Used by the Debug pages (the station's and
// the pause menu's), the in-game Options page, the Missions and Status windows and the main menu.

using System;
using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class MenuKeys
    {
        /// <summary>The rows a menu's keys window lists.</summary>
        [Flags]
        public enum Rows
        {
            Select = 1,        // up / down (the D-pad)
            Change = 2,        // left / right change a value
            MoveChange = 4,    // left / right move across a grid or change a value (the Debug pages)
            Tabs = 8,          // Q / E, LB / RB
            Confirm = 16,      // Enter, A
            Back = 32,         // Esc, B
        }

        static string X(string key, string english) => Localization.Extra(key, english);

        /// <summary>One hint box: the keys over the label.</summary>
        public static VisualElement Hint(string label, params VisualElement[] glyphs)
        {
            var h = new VisualElement { pickingMode = PickingMode.Ignore };
            h.AddToClassList("hint");
            var keys = new VisualElement { pickingMode = PickingMode.Ignore };
            keys.AddToClassList("hint-keys");
            foreach (var g in glyphs) { g.pickingMode = PickingMode.Ignore; keys.Add(g); }
            h.Add(keys);
            var l = new Label(label) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("hint-label");
            l.AddToClassList("gof-semibold");
            h.Add(l);
            return h;
        }

        /// <summary>The keys window's content: an empty list styled as one (.hints-popup).</summary>
        public static VisualElement Popup()
        {
            var c = new VisualElement { pickingMode = PickingMode.Ignore };
            c.AddToClassList("hints-popup");
            return c;
        }

        /// <summary>The usual menu keys, in this order: select, change / move, switch tab, confirm, back.</summary>
        public static VisualElement Content(Rows rows)
        {
            var c = Popup();
            bool pad = InputMode.Current == InputKind.Gamepad;
            string select = X("hudSelect", "SELECT"), tabs = X("hudSwitchTab", "SWITCH TAB");
            string side = (rows & Rows.MoveChange) != 0 ? X("hudMoveChange", "MOVE / CHANGE")
                        : (rows & Rows.Change) != 0 ? X("hudChangeValue", "CHANGE VALUE") : null;
            if (pad)
            {
                // The D-pad does both: one box.
                string dpad = (rows & Rows.Select) != 0 ? (side != null ? $"{select} / {side}" : select) : side;
                if (dpad != null) c.Add(Hint(dpad, InputGlyph.Pad(PadButton.DPad)));
                if ((rows & Rows.Tabs) != 0) c.Add(Hint(tabs, InputGlyph.Pad(PadButton.LeftBumper), InputGlyph.Pad(PadButton.RightBumper)));
                if ((rows & Rows.Confirm) != 0) c.Add(Hint(X("hudConfirm", "CONFIRM"), InputGlyph.Pad(PadButton.A)));
                if ((rows & Rows.Back) != 0) c.Add(Hint(X("hudBack", "BACK"), InputGlyph.Pad(PadButton.B)));
                return c;
            }
            if ((rows & Rows.Select) != 0) c.Add(Hint(select, InputGlyph.Key("↑"), InputGlyph.Key("↓")));
            if (side != null) c.Add(Hint(side, InputGlyph.Key("←"), InputGlyph.Key("→")));
            if ((rows & Rows.Tabs) != 0) c.Add(Hint(tabs, InputGlyph.Key("Q"), InputGlyph.Key("E")));
            if ((rows & Rows.Confirm) != 0) c.Add(Hint(X("hudConfirm", "CONFIRM"), InputGlyph.Key("ENTER", true)));
            if ((rows & Rows.Back) != 0) c.Add(Hint(X("hudBack", "BACK"), InputGlyph.Key("ESC")));
            return c;
        }

        /// <summary>An "i" (the keys icon) in the layout (not over it), 'size' square; 'content' builds its window.</summary>
        public static VisualElement KeysIcon(Func<VisualElement> content, float size = 32f)
        {
            var icon = OptionInfo.Icon(content, Resources.Load<Texture2D>("GoF2Hud/option_keys"));
            return InLayout(icon, "menu-keys", size);
        }

        /// <summary>A "?" in the layout (not over it), 'size' square; 'text' is read when it opens.</summary>
        public static VisualElement HelpIcon(Func<string> text, float size = 32f) => InLayout(OptionInfo.Icon(text), "menu-help", size);

        static VisualElement InLayout(VisualElement icon, string cls, float size)
        {
            icon.AddToClassList(cls);
            icon.style.position = Position.Relative;
            icon.style.width = icon.style.height = size;
            icon.style.flexShrink = 0;
            icon.style.alignSelf = Align.Center;
            return icon;
        }

        /// <summary>The Debug pages' "i" and "?" at the right end of their tab row (in place of the "Q ◂ ▸ E" line).</summary>
        public static void AddDebugIcons(VisualElement tabRow)
        {
            var keys = KeysIcon(() => Content(Rows.Select | Rows.MoveChange | Rows.Tabs | Rows.Confirm | Rows.Back));
            keys.style.marginLeft = new StyleLength(StyleKeyword.Auto);
            keys.style.marginRight = 12f;
            keys.style.marginBottom = 6f;
            tabRow.Add(keys);
            var help = HelpIcon(DebugHelp);
            help.style.marginRight = 6f;
            help.style.marginBottom = 6f;
            tabRow.Add(help);
        }

        /// <summary>What the Debug page is for, tab by tab (the tabs' own names).</summary>
        static string DebugHelp()
        {
            string help = X("debugHelp",
                "Testing tools of the remake.\n\n{0}: switches that change the game's rules (god mode, infinite ammo, free shopping...).\n"
                + "{1}: instant changes (credits, repair, energy cells, the whole star map, peace with every race).\n"
                + "{2}: any item into the cargo hold, or mounted at once.\n{3}: fly any ship of the game.\n"
                + "{4}: up to 14 ships saved with their equipment.\n{5} (in flight): ships, capital ships and objects next to you.\n\n"
                + "The result of the last action shows under the tabs.");
            try
            {
                return X("debugTitle", "Debug") + "\n\n" + string.Format(help, X("debugCheats", "Cheats"), X("debugActions", "Actions"),
                    X("debugItems", "Give items"), X("debugShips", "Ships"), X("debugPresets", "Presets"), X("debugSpawn", "Spawn"));
            }
            catch (FormatException) { return help; }
        }
    }
}
