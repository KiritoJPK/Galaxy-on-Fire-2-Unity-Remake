// InputHintsTests.cs
// CLAUDE.md "UI and platforms": the hint texts' #KEY_* tokens (Globals::replaceKeyBindingTokens, FlightHints.KeyTokens)
// show the current bindings, the keyboard's for keys and mouse, the controller's buttons for a controller; a control
// without a controller button names the autopilot menu's (its entry is in that menu). The PC version's #KEY_ACTION_MENU is
// its E "Actions" menu (CLAUDE.md: "E actions (the quick menu)", "the PC version's E 'Actions'"), which in the remake is
// the Actions menu row (E / the D-pad left): texts 591 / 594 / 638 tell the player to open it to reach the Khador Drive,
// the cloak and the wingmen, which are in the quick menu. InputGlyph: the controller hints use Xbox labels; a binding the
// HUD names is a keycap for keys and mouse buttons (wide past three letters) or a controller button; nothing for touch.

using System.Linq;
using GoF2Remake.Flight;
using GoF2Remake.UI;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class InputHintsTests
    {
        DataUiTestKit.PrefsSnapshot prefs;
        DataUiTestKit.TextLanguage english;
        InputKind mode;

        [SetUp]
        public void Defaults()
        {
            prefs = new DataUiTestKit.PrefsSnapshot();
            english = new DataUiTestKit.TextLanguage("en");
            GameControls.ResetToDefaults();
            mode = InputMode.Current;
        }

        [TearDown]
        public void Restore()
        {
            DataUiTestKit.SetInputMode(mode);
            english?.Dispose();
            prefs?.Dispose();
        }

        static string Key(string path) => GameControls.ShortName(path);

        [Test]
        public void TextWithoutTokensIsUntouched()
        {
            DataUiTestKit.SetInputMode(InputKind.KeyboardMouse);
            string s = "Fly to the station.";
            Assert.AreSame(s, FlightHints.KeyTokens(s));
            Assert.AreEqual("", FlightHints.KeyTokens(""));
            Assert.IsNull(FlightHints.KeyTokens(null));
        }

        [Test]
        public void KeyboardTokensNameTheKeys()
        {
            DataUiTestKit.SetInputMode(InputKind.KeyboardMouse);
            Assert.AreEqual(Key("<Keyboard>/f"), FlightHints.KeyTokens("#KEY_DOCK"));
            Assert.AreEqual(Key("<Keyboard>/q"), FlightHints.KeyTokens("#KEY_AUTOPILOT"));
            Assert.AreEqual(Key("<Keyboard>/k"), FlightHints.KeyTokens("#KEY_KHADOR_DRIVE"));
            Assert.AreEqual(Key("<Keyboard>/v"), FlightHints.KeyTokens("#KEY_WINGMEN"));
            Assert.AreEqual(Key("<Keyboard>/c"), FlightHints.KeyTokens("#KEY_CLOAK"));
            Assert.AreEqual("TAB", FlightHints.KeyTokens("#KEY_FAST_FORWARD"));
            Assert.AreEqual("SPACE", FlightHints.KeyTokens("#KEY_PRIMARY"));
            Assert.AreEqual(Key("<Keyboard>/r"), FlightHints.KeyTokens("#KEY_SECONDARY"));
            Assert.AreEqual(Key("<Keyboard>/w"), FlightHints.KeyTokens("#KEY_BOOST"));
        }

        [Test]
        public void TheLongerTokenIsReplacedFirst()
        {
            DataUiTestKit.SetInputMode(InputKind.KeyboardMouse);
            // #KEY_SECONDARY is a prefix of #KEY_SECONDARY_WEAPONS (text 1879)
            Assert.AreEqual(Key("<Keyboard>/g") + " / " + Key("<Keyboard>/r"), FlightHints.KeyTokens("#KEY_SECONDARY_WEAPONS / #KEY_SECONDARY"));
        }

        [Test]
        public void EveryHintTextIsFullyReplaced()
        {
            // the PC texts with tokens (CLAUDE.md "Keyboard": 17 / 18, 585, 591, 594, 597, 604, 607, 610, 638, 1731 / 1732,
            // 1752, 1754, 1879)
            foreach (var kind in new[] { InputKind.KeyboardMouse, InputKind.Gamepad })
            {
                DataUiTestKit.SetInputMode(kind);
                foreach (int id in new[] { 17, 18, 585, 586, 591, 592, 594, 595, 597, 598, 604, 607, 610, 638, 639, 1726, 1731, 1732, 1752, 1754, 1879, 1880 })
                {
                    string t = FlightHints.KeyTokens(GoF2Remake.Data.Localization.Get(id));
                    StringAssert.DoesNotContain("#KEY", t, $"text {id} ({kind})");
                }
            }
        }

        [Test]
        public void ControllerTokensNameTheButtons()
        {
            DataUiTestKit.SetInputMode(InputKind.Gamepad);
            Assert.AreEqual("X", FlightHints.KeyTokens("#KEY_DOCK"));
            Assert.AreEqual("RT", FlightHints.KeyTokens("#KEY_PRIMARY"));
            Assert.AreEqual("LT", FlightHints.KeyTokens("#KEY_SECONDARY"));
            Assert.AreEqual("A", FlightHints.KeyTokens("#KEY_BOOST"));
            Assert.AreEqual("Y", FlightHints.KeyTokens("#KEY_FAST_FORWARD"));
            Assert.AreEqual("D-PAD →", FlightHints.KeyTokens("#KEY_SECONDARY_WEAPONS"));
            Assert.AreEqual("VIEW", FlightHints.KeyTokens("#KEY_AUTOPILOT"));
            Assert.AreEqual("RS", FlightHints.KeyTokens("#KEY_CLOAK"));
        }

        [Test]
        public void AControlWithoutAButtonNamesTheAutopilotMenus()
        {
            DataUiTestKit.SetInputMode(InputKind.Gamepad);
            Assert.AreEqual("VIEW", FlightHints.KeyTokens("#KEY_WINGMEN"), "wingmen: no controller default");
            Assert.AreEqual("VIEW", FlightHints.KeyTokens("#KEY_KHADOR_DRIVE"));
        }

        [Test]
        public void AnUnboundKeyIsADash()
        {
            DataUiTestKit.SetInputMode(InputKind.KeyboardMouse);
            GameControls.Clear(DataUiTestKit.Row("wingmen"), BindSlot.Key1);
            Assert.AreEqual("–", FlightHints.KeyTokens("#KEY_WINGMEN"));
        }

        [Test]
        public void TheActionMenuTokenNamesTheActionsMenuKey()
        {
            // FlightHints.KeyTokens maps #KEY_ACTION_MENU to the autopilot menu (Q / View) unless the autopilot menu has a
            // second key; its comment says E is that second key, but E is the Actions menu's own row (GameControls).
            DataUiTestKit.SetInputMode(InputKind.KeyboardMouse);
            Assert.AreEqual(Key("<Keyboard>/e"), FlightHints.KeyTokens("#KEY_ACTION_MENU"), "keyboard: E, the Actions menu");
            DataUiTestKit.SetInputMode(InputKind.Gamepad);
            Assert.AreEqual("D-PAD ←", FlightHints.KeyTokens("#KEY_ACTION_MENU"), "controller: the D-pad left");
        }

        // ---- InputGlyph ------------------------------------------------------------------------------------------

        static string Text(VisualElement e) => ((Label)e).text;

        [TestCase(PadButton.A, "A", "pad-face--a")]
        [TestCase(PadButton.B, "B", "pad-face--b")]
        [TestCase(PadButton.X, "X", "pad-face--x")]
        [TestCase(PadButton.Y, "Y", "pad-face--y")]
        [TestCase(PadButton.LeftStick, "LS", "pad-stick")]
        [TestCase(PadButton.RightStick, "RS", "pad-stick")]
        [TestCase(PadButton.DPad, "D-PAD", "pad-menu")]
        [TestCase(PadButton.LeftTrigger, "LT", "pad-trigger")]
        [TestCase(PadButton.RightTrigger, "RT", "pad-trigger")]
        [TestCase(PadButton.LeftBumper, "LB", "pad-bumper")]
        [TestCase(PadButton.RightBumper, "RB", "pad-bumper")]
        [TestCase(PadButton.Menu, "MENU", "pad-menu")]
        [TestCase(PadButton.View, "VIEW", "pad-menu")]
        public void PadGlyphsUseXboxLabels(PadButton button, string text, string cls)
        {
            var e = InputGlyph.Pad(button);
            Assert.AreEqual(text, Text(e));
            Assert.IsTrue(e.ClassListContains(cls), cls);
            Assert.IsTrue(e.ClassListContains("gof-semibold"));
            Assert.AreEqual(PickingMode.Ignore, e.pickingMode, "glyphs never take a click");
            if (cls.StartsWith("pad-face--")) Assert.IsTrue(e.ClassListContains("pad-face"));
        }

        [Test]
        public void KeycapsAreWidePastThreeLetters()
        {
            var narrow = InputGlyph.Key("F");
            Assert.AreEqual("F", Text(narrow));
            Assert.IsTrue(narrow.ClassListContains("keycap"));
            Assert.IsFalse(narrow.ClassListContains("keycap--wide"));
            Assert.IsTrue(InputGlyph.Key("SPACE", true).ClassListContains("keycap--wide"));
        }

        static System.Collections.Generic.List<VisualElement> For(string action, InputKind kind, bool both = false) =>
            (System.Collections.Generic.List<VisualElement>)DataUiTestKit.CallStatic(typeof(InputGlyph), "For", DataUiTestKit.FlightAction(action), kind, both);

        [Test]
        public void HintsShowTheBindingsForTheInputKind()
        {
            var fire = For("FirePrimary", InputKind.KeyboardMouse);
            Assert.AreEqual(1, fire.Count, "the first keyboard slot");
            Assert.AreEqual("SPACE", Text(fire[0]));
            Assert.IsTrue(fire[0].ClassListContains("keycap--wide"));
            var both = For("FirePrimary", InputKind.KeyboardMouse, true);
            CollectionAssert.AreEqual(new[] { "SPACE", "LMB" }, both.Select(Text).ToArray());
            Assert.IsFalse(both[1].ClassListContains("keycap--wide"), "LMB: three letters");
            var pad = For("FirePrimary", InputKind.Gamepad);
            Assert.AreEqual(1, pad.Count);
            Assert.AreEqual("RT", Text(pad[0]));
            Assert.IsTrue(pad[0].ClassListContains("pad-trigger"));
            CollectionAssert.AreEqual(new[] { "↑", "↓", "←", "→" }, For("Steer", InputKind.KeyboardMouse).Select(Text).ToArray(), "a composite part by part");
            CollectionAssert.AreEqual(new[] { "RB", "LB" }, For("Throttle", InputKind.Gamepad).Select(Text).ToArray());
            var dodge = For("DodgeLeft", InputKind.Gamepad);
            Assert.AreEqual("RS ←", Text(dodge[0]));
            Assert.IsTrue(dodge[0].ClassListContains("pad-stick"));
            var camera = For("Camera", InputKind.Gamepad);
            Assert.AreEqual("D-PAD ↑", Text(camera[0]));
            Assert.IsTrue(camera[0].ClassListContains("pad-menu"));
            Assert.IsTrue(For("Boost", InputKind.Gamepad)[0].ClassListContains("pad-face--a"));
        }

        [Test]
        public void NoHintWithoutABinding()
        {
            CollectionAssert.IsEmpty(For("FirePrimary", InputKind.Touch), "touch shows its own controls");
            CollectionAssert.IsEmpty(For("Wingmen", InputKind.Gamepad), "no controller default");
            CollectionAssert.IsEmpty(For("MouseSteering", InputKind.Gamepad), "no controller slot");
            CollectionAssert.IsEmpty(For("DodgeLeft", InputKind.KeyboardMouse), "no keyboard default");
            GameControls.Clear(DataUiTestKit.Row("boost"), BindSlot.Key1);
            CollectionAssert.IsEmpty(For("Boost", InputKind.KeyboardMouse), "an unbound control's hint is left out");
        }

        [Test]
        public void TheSecondKeyStandsInForAnUnboundFirst()
        {
            GameControls.Clear(DataUiTestKit.Row("firePrimary"), BindSlot.Key1);
            CollectionAssert.AreEqual(new[] { "LMB" }, For("FirePrimary", InputKind.KeyboardMouse).Select(Text).ToArray());
        }
    }
}
