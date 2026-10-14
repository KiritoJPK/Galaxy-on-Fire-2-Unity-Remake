// DpadTapNavigation.cs
// UI Toolkit's runtime input (the Input System's InputSystemProvider) samples the UI/Navigate action's value once a frame:
// a D-pad press released within the same frame reads as no direction and is dropped. The Steam controller's D-pad (a
// trackpad click through Steam Input) sends exactly such taps, so the main and station menus needed several presses to
// move one entry. ButtonControl.wasPressedThisFrame does see those presses: a D-pad button pressed this frame and already
// up again is a tap the panel missed, and it is sent to the panel as the NavigationMoveEvent it would have dispatched.
// A press still held was (or will be) seen by the panel itself.

using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class DpadTapNavigation
    {
        /// <summary>Call once a frame while the menu takes input.</summary>
        public static void Pump(VisualElement root)
        {
            var pad = Gamepad.current;
            if (pad == null || root?.panel == null) return;
            var dpad = pad.dpad;
            if (Tap(dpad.up)) Send(root, NavigationMoveEvent.Direction.Up);
            if (Tap(dpad.down)) Send(root, NavigationMoveEvent.Direction.Down);
            if (Tap(dpad.left)) Send(root, NavigationMoveEvent.Direction.Left);
            if (Tap(dpad.right)) Send(root, NavigationMoveEvent.Direction.Right);
        }

        static bool Tap(UnityEngine.InputSystem.Controls.ButtonControl b) => b.wasPressedThisFrame && !b.isPressed;

        static void Send(VisualElement root, NavigationMoveEvent.Direction direction)
        {
            var target = root.focusController?.focusedElement as VisualElement ?? root;
            using (var e = NavigationMoveEvent.GetPooled(direction))
            {
                e.target = target;
                target.SendEvent(e);
            }
        }
    }
}
