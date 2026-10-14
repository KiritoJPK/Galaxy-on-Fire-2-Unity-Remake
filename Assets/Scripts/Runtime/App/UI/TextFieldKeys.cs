// TextFieldKeys.cs
// Remake: keys typed into a UI Toolkit TextField stay in it. The project-wide UI map turns W A S D and the arrows into
// navigation moves and Space / Enter into submits, so typing a code, an amount or a name moved the focus to the menu's
// buttons (or pressed one). A field Guard()ed here keeps those events while a key is held (a controller's D-pad and
// stick still move on; the up / down arrows leave the field like the menus expect) and Esc only drops its focus.
// The multiplayer window also turns the game's own keys off while one of its fields has the focus (NetChat.SetTyping).

using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class TextFieldKeys
    {
        /// <summary>'target' is a TextField or inside one.</summary>
        public static bool InTextField(IEventHandler target)
        {
            for (var v = target as VisualElement; v != null; v = v.parent)
                if (v is TextField) return true;
            return false;
        }

        /// <summary>A navigation event that came from typing into the focused text field (a key held, not the up / down
        /// arrows): it belongs to the field, not to the menu.</summary>
        public static bool IsTyping(EventBase e, VisualElement focused)
        {
            if (!InTextField(focused)) return false;
            var kb = Keyboard.current;
            if (kb == null || !kb.anyKey.isPressed) return false;   // a controller's D-pad / stick, or a click
            if (e is NavigationMoveEvent && (kb.upArrowKey.isPressed || kb.downArrowKey.isPressed)) return false;
            return true;
        }

        /// <summary>The field keeps its typed keys: no menu navigation, no submit, and Esc only drops its focus.</summary>
        public static void Guard(TextField field)
        {
            if (field == null) return;
            void Swallow(EventBase e)
            {
                e.StopPropagation();
                field.focusController?.IgnoreEvent(e);
            }
            field.RegisterCallback<NavigationMoveEvent>(e => { if (IsTyping(e, field)) Swallow(e); }, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(e => { if (IsTyping(e, field)) Swallow(e); }, TrickleDown.TrickleDown);
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != UnityEngine.KeyCode.Escape) return;
                Swallow(e);
                field.Blur();
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationCancelEvent>(e => { Swallow(e); field.Blur(); }, TrickleDown.TrickleDown);
        }
    }
}
