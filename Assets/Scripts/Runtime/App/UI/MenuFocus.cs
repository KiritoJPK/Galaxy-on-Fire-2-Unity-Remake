// MenuFocus.cs
// Remake: shared by the main menu's and the station's focus safety nets (#74, and a Discord report of 2026-10-18): a
// controller's or the arrow keys' navigation press, and the first item of a list.

using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class MenuFocus
    {
        /// <summary>A navigation press this frame: the controller's D-pad, left stick or A, or an arrow key (some pads send
        /// their D-pad as arrow keys on Android, and B as Escape, which switches the input mode away from the controller).</summary>
        public static bool NavPressed(Gamepad pad, Keyboard kb) =>
            (pad != null && (pad.dpad.ReadValue().sqrMagnitude > 0.25f || pad.leftStick.ReadValue().sqrMagnitude > 0.25f
                             || pad.buttonSouth.wasPressedThisFrame))
            || (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
                               || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame));

        /// <summary>The first item of a list, null for none.</summary>
        public static VisualElement First(VisualElement[] items) => items != null && items.Length > 0 ? items[0] : null;
    }
}
