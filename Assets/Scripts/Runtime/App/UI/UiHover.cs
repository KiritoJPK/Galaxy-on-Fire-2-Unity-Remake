// UiHover.cs
// Modified: the menus' hover sound (the button push, softer) for the mouse: only on the tabs (the hangar's, the Missions and
// Status windows', the Options', the Debug pages') and the side menus' buttons (the station's main view; the flight HUD's
// autopilot / actions menu and the main menu's column pass their own); any other button, row or field is silent under the
// mouse (the keys / a controller still sound on every move). The mouse coming onto another such button inside a container
// plays it once (moving within it doesn't; a list scrolling under a still mouse doesn't either: InputMode.MouseMovedRecently).
// IsHoverable / Target: what the keys' move sound is for.

using System;
using GoF2Remake.Data;
using UnityEngine.UIElements;
using PointerType = UnityEngine.UIElements.PointerType;

namespace GoF2Remake.UI
{
    public static class UiHover
    {
        /// <summary>What the hover sound is for: a button, a toggle, a slider, a choice, an option's row.</summary>
        public static bool IsHoverable(VisualElement v) =>
            v is Button || v is Toggle || v is Slider || v is ChoiceRow || v.ClassListContains("option-row");

        /// <summary>The outermost hoverable element at 't', up to 'stop' (a choice's drop-down box is part of its row).</summary>
        public static VisualElement Target(VisualElement t, VisualElement stop = null)
        {
            VisualElement found = null;
            for (var v = t; v != null; v = v.parent)
            {
                if (IsHoverable(v)) found = v;
                if (v == stop) break;
            }
            return found;
        }

        /// <summary>Modified: the main menu's click sound (the button release) for a click on any button or toggle (not in a
        /// shop window, .hangar-window: its rows and arrows have their own sounds); the hosts skip the press's push and play
        /// one release per frame, so a button that already plays it on its click isn't doubled.</summary>
        public static bool ClickSounds(IEventHandler target)
        {
            bool found = false;
            for (var v = target as VisualElement; v != null; v = v.parent)
            {
                if (v.ClassListContains("hangar-window")) return false;
                if (v is Button || v is Toggle) found = true;
            }
            return found;
        }

        /// <summary>A press outside a shop window (.hangar-window): its push sound is left out (the click sounds instead).</summary>
        public static bool SilentPress(IEventHandler target)
        {
            for (var v = target as VisualElement; v != null; v = v.parent)
                if (v.ClassListContains("hangar-window")) return false;
            return true;
        }

        static readonly string[] MenuClasses = { "station-button", "hangar-tab", "tab-button", "debug-tab", "missions-tab" };

        /// <summary>The tab or side menu button at 't' (up to 'stop'), null elsewhere: what the mouse's hover sound is for.</summary>
        public static VisualElement MenuTarget(VisualElement t, VisualElement stop = null)
        {
            for (var v = t; v != null; v = v.parent)
            {
                if (v is Button && Array.Exists(MenuClasses, v.ClassListContains)) return v;
                if (v == stop) break;
            }
            return null;
        }

        /// <summary>'sound' when the mouse comes onto another tab or side menu button inside 'container' ('target' picks the
        /// element for a hovered one, MenuTarget by default; null = no sound).</summary>
        public static void Hook(VisualElement container, Action sound, Func<VisualElement, VisualElement> target = null)
        {
            if (container == null) return;
            VisualElement last = null;
            container.RegisterCallback<PointerOverEvent>(e =>
            {
                if (e.pointerType != PointerType.mouse || !InputMode.MouseMovedRecently || !(e.target is VisualElement t)) return;
                var h = target != null ? target(t) : MenuTarget(t, container);
                if (h == last) return;
                last = h;
                if (h != null && h.enabledInHierarchy) sound?.Invoke();
            });
            container.RegisterCallback<PointerLeaveEvent>(e => { if (e.target == container) last = null; });
        }
    }
}
