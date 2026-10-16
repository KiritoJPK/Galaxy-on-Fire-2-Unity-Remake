// McpInput.cs
// The Mod Builder MCP server's hands in a play test or a test flight (its press / steer / ui_click tools): a virtual keyboard
// and gamepad of their own (InputSystem.AddDevice, fed in onBeforeUpdate like Vr.VrPad), holding keys, game actions by their
// current bindings (GameControls, so a player's rebinding is followed) and the left stick for a while, on the real clock (the
// game pauses in its menus); and the UI's buttons found by their text on every panel (PanelRenderer's root) and pressed as a
// controller's submit would. State is only sent while something is held, and once more to let go, so the player's own
// keyboard stays the current one otherwise. The game window is usually not focused meanwhile (the player is in their AI
// app): a built game drops a keyboard's input then and the Editor sends it to its own windows unless the Game view has the
// focus, so while something is held the input settings ignore the focus (InputSettings.backgroundBehavior IgnoreFocus,
// editorInputBehaviorInPlayMode AllDeviceInputAlwaysGoesToGameView), and get their own values back a moment after the
// release (Tick, from the MCP pump): the player typing in another window doesn't steer the game the rest of the time. The
// settings are a copy (made once: a settings asset is never changed), its two values switched: swapping whole objects lost
// the original, which the Input System destroys when it made it itself. Plain C#.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoF2Remake.Flight;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class McpInput
    {
        static Keyboard keyboard;
        static Gamepad gamepad;
        static readonly Dictionary<Key, float> keys = new Dictionary<Key, float>();
        static readonly Dictionary<GamepadButton, float> buttons = new Dictionary<GamepadButton, float>();
        static Vector2 stick;
        static float stickUntil, rightTriggerUntil, leftTriggerUntil;
        static bool sentIdle = true;
        static InputSettings settingsCopy;
        static bool focusless;
        static InputSettings.BackgroundBehavior savedBackground;
        static InputSettings.EditorInputBehaviorInPlayMode savedEditorBehaviour;
        static float lastActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { keyboard = null; gamepad = null; keys.Clear(); buttons.Clear(); stickUntil = rightTriggerUntil = leftTriggerUntil = 0f; sentIdle = true; settingsCopy = null; focusless = false; }

        /// <summary>Something is still held.</summary>
        public static bool Busy => keys.Count > 0 || buttons.Count > 0 || stickUntil > Time.realtimeSinceStartup
                                   || rightTriggerUntil > Time.realtimeSinceStartup || leftTriggerUntil > Time.realtimeSinceStartup;

        /// <summary>The input settings that ignore the window's focus while the AI holds something.</summary>
        static void Focusless()
        {
            lastActive = Time.realtimeSinceStartup;
            if (focusless) return;
            if (settingsCopy == null || InputSystem.settings != settingsCopy)
            {
                settingsCopy = UnityEngine.Object.Instantiate(InputSystem.settings);   // a copy: a settings asset stays as it is
                InputSystem.settings = settingsCopy;
            }
            savedBackground = settingsCopy.backgroundBehavior;
            savedEditorBehaviour = settingsCopy.editorInputBehaviorInPlayMode;
            settingsCopy.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settingsCopy.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            focusless = true;
        }

        static void RestoreSettings()
        {
            if (!focusless) return;
            focusless = false;
            if (settingsCopy == null) return;
            settingsCopy.backgroundBehavior = savedBackground;
            settingsCopy.editorInputBehaviorInPlayMode = savedEditorBehaviour;
        }

        /// <summary>The MCP pump, each frame: the player's input settings back once nothing has been held for a moment.</summary>
        public static void Tick()
        {
            if (focusless && !Busy && sentIdle && Time.realtimeSinceStartup - lastActive > 0.3f) RestoreSettings();
        }

        static void Install()
        {
            Focusless();
            if (keyboard != null) return;
            keyboard = InputSystem.AddDevice<Keyboard>("MCP Keyboard");
            gamepad = InputSystem.AddDevice<Gamepad>("MCP Gamepad");
            InputSystem.onBeforeUpdate += Feed;
            Application.quitting += Uninstall;
        }

        /// <summary>The devices gone (the server stopped, the game quits).</summary>
        public static void Uninstall()
        {
            InputSystem.onBeforeUpdate -= Feed;
            Application.quitting -= Uninstall;
            keys.Clear(); buttons.Clear(); stickUntil = rightTriggerUntil = leftTriggerUntil = 0f;
            RestoreSettings();
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (gamepad != null) { InputSystem.RemoveDevice(gamepad); gamepad = null; }
            sentIdle = true;
        }

        // ---- what to hold -------------------------------------------------------------------------------------------

        public static void HoldKey(Key k, float seconds) { Install(); keys[k] = Time.realtimeSinceStartup + seconds; }
        public static void HoldButton(GamepadButton b, float seconds) { Install(); buttons[b] = Time.realtimeSinceStartup + seconds; }
        public static void Stick(Vector2 v, float seconds) { Install(); stick = Vector2.ClampMagnitude(v, 1f); stickUntil = Time.realtimeSinceStartup + seconds; }
        public static void RightTrigger(float seconds) { Install(); rightTriggerUntil = Time.realtimeSinceStartup + seconds; }
        public static void LeftTrigger(float seconds) { Install(); leftTriggerUntil = Time.realtimeSinceStartup + seconds; }

        /// <summary>A key by its name ("enter", "escape", "f", "1", "space", "upArrow", "f12"...).</summary>
        public static bool TryKey(string name, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim();
            if (n.Length == 1 && char.IsDigit(n[0])) n = "Digit" + n;
            if (n.Equals("esc", StringComparison.OrdinalIgnoreCase)) n = "Escape";
            if (n.Equals("return", StringComparison.OrdinalIgnoreCase)) n = "Enter";
            return Enum.TryParse(n, true, out key) && key != Key.None;
        }

        /// <summary>A key name's Input System name ("enter" -> "Enter", "1" -> "Digit1"), null when there is no such key.</summary>
        public static string KeyName(string name) => TryKey(name, out var k) ? k.ToString() : null;

        /// <summary>Holds a game action (a GameControls row id: "action", "boost", "firePrimary"...) by its keyboard binding,
        /// else its controller one. Null, or why not.</summary>
        public static string HoldAction(string id, float seconds)
        {
            var row = GameControls.Rows.FirstOrDefault(r => string.Equals(r.id, id, StringComparison.OrdinalIgnoreCase));
            if (row == null) return $"No action \"{id}\": {string.Join(", ", GameControls.Rows.Select(r => r.id))}.";
            if (row.partLabels != null) return $"\"{row.id}\" is a stick / axis: use steer.";
            foreach (var slot in new[] { BindSlot.Key1, BindSlot.Key2 })
                foreach (var path in GameControls.Paths(row, slot))
                    if (path.StartsWith("<Keyboard>/") && TryKey(path.Substring("<Keyboard>/".Length), out var k)) { HoldKey(k, seconds); return null; }
            foreach (var path in GameControls.Paths(row, BindSlot.Pad))
            {
                string p = path.Replace("<Gamepad>/", "");
                switch (p)
                {
                    case "rightTrigger": RightTrigger(seconds); return null;
                    case "leftTrigger": LeftTrigger(seconds); return null;
                }
                if (PadButton(p, out var b)) { HoldButton(b, seconds); return null; }
            }
            return $"\"{row.id}\" has no key or button bound.";
        }

        static bool PadButton(string p, out GamepadButton b)
        {
            b = default;
            switch (p)
            {
                case "buttonSouth": b = GamepadButton.South; return true;
                case "buttonEast": b = GamepadButton.East; return true;
                case "buttonWest": b = GamepadButton.West; return true;
                case "buttonNorth": b = GamepadButton.North; return true;
                case "leftShoulder": b = GamepadButton.LeftShoulder; return true;
                case "rightShoulder": b = GamepadButton.RightShoulder; return true;
                case "start": b = GamepadButton.Start; return true;
                case "select": b = GamepadButton.Select; return true;
                case "dpad/up": b = GamepadButton.DpadUp; return true;
                case "dpad/down": b = GamepadButton.DpadDown; return true;
                case "dpad/left": b = GamepadButton.DpadLeft; return true;
                case "dpad/right": b = GamepadButton.DpadRight; return true;
                case "leftStickPress": b = GamepadButton.LeftStick; return true;
                case "rightStickPress": b = GamepadButton.RightStick; return true;
            }
            return false;
        }

        static void Feed()
        {
            if (keyboard == null) return;
            float now = Time.realtimeSinceStartup;
            foreach (var k in keys.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList()) keys.Remove(k);
            foreach (var b in buttons.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList()) buttons.Remove(b);
            bool stickOn = stickUntil > now, rt = rightTriggerUntil > now, lt = leftTriggerUntil > now;
            bool any = keys.Count > 0 || buttons.Count > 0 || stickOn || rt || lt;
            if (!any && sentIdle) return;   // nothing held: the player's own devices stay the current ones
            if (any) lastActive = now;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.Keys.ToArray()));
            var s = new GamepadState { leftStick = stickOn ? stick : Vector2.zero, rightTrigger = rt ? 1f : 0f, leftTrigger = lt ? 1f : 0f };
            foreach (var b in buttons.Keys) s = s.WithButton(b, true);
            InputSystem.QueueStateEvent(gamepad, s);
            sentIdle = !any;
        }

        // ---- the UI -------------------------------------------------------------------------------------------------

        static PropertyInfo rootProperty;

        /// <summary>The roots of every panel shown, the topmost (highest sorting order) first.</summary>
        public static List<VisualElement> Roots()
        {
            rootProperty ??= typeof(PanelRenderer).GetProperty("rootVisualElement", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var list = new List<(VisualElement root, float order)>();
            foreach (var pr in UnityEngine.Object.FindObjectsByType<PanelRenderer>())
            {
                if (!pr.enabled || !pr.gameObject.activeInHierarchy || rootProperty == null) continue;
                if (rootProperty.GetValue(pr) is VisualElement root && root.panel != null)
                    list.Add((root, pr.panelSettings != null ? pr.panelSettings.sortingOrder : 0f));
            }
            return list.OrderByDescending(x => x.order).Select(x => x.root).ToList();
        }

        /// <summary>The element is on screen: laid out, not hidden by itself or a parent.</summary>
        static bool Shown(VisualElement e)
        {
            var r = e.worldBound;
            if (float.IsNaN(r.width) || r.width < 2f || r.height < 2f) return false;
            for (var p = e; p != null; p = p.parent)
            {
                var st = p.resolvedStyle;
                if (st.display == DisplayStyle.None || st.visibility == Visibility.Hidden || st.opacity < 0.05f) return false;
            }
            return e.enabledInHierarchy;
        }

        static string TextOf(VisualElement e)
        {
            if (e is TextElement t && !string.IsNullOrWhiteSpace(t.text)) return t.text;
            var parts = e.Query<TextElement>().ToList().Where(x => x != e && !string.IsNullOrWhiteSpace(x.text)).Select(x => x.text.Trim());
            return string.Join(" ", parts);
        }

        static string Clean(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "").Replace("\n", " ").Trim();

        /// <summary>Every shown button with its text, topmost panel first.</summary>
        public static List<(Button button, string text)> Buttons()
        {
            var list = new List<(Button, string)>();
            foreach (var root in Roots())
                foreach (var b in root.Query<Button>().ToList())
                    if (Shown(b))
                    {
                        string t = Clean(TextOf(b));
                        if (t.Length > 0) list.Add((b, t));
                    }
            return list;
        }

        /// <summary>Presses the shown button whose text is 'text' (else the first containing it; 'index' among several),
        /// as a controller's submit does. Null, or why not; 'pressed' its text.</summary>
        public static string Click(string text, int index, out string pressed)
        {
            pressed = null;
            var all = Buttons();
            string want = Clean(text).ToLowerInvariant();
            var exact = all.Where(b => b.text.ToLowerInvariant() == want).ToList();
            var hits = exact.Count > 0 ? exact : all.Where(b => b.text.ToLowerInvariant().Contains(want)).ToList();
            if (hits.Count == 0) return $"No button \"{text}\" on screen. Shown: {string.Join(" | ", all.Select(b => b.text).Distinct().Take(40))}";
            if (index < 0 || index >= hits.Count) return $"{hits.Count} buttons match \"{text}\": index 0..{hits.Count - 1}.";
            var (button, label) = hits[index];
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
            pressed = label;
            return null;
        }
    }
}
