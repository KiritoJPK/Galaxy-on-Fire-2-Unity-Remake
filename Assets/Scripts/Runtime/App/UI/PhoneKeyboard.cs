// PhoneKeyboard.cs
// Remake: a TextField on a phone typed through an on-screen keyboard the game opens itself (TouchScreenKeyboard), read
// every frame and never written to while it is up, instead of UI Toolkit's own soft keyboard for the field
// (hideSoftKeyboard). A player's report (2026-10-18): holding Backspace in the multiplayer chat deleted one letter and
// stopped. On GameActivity (the Android entry point here) every write to the keyboard (its text, its character limit, a
// new Open) ends in Unity's UGASoftKeyboard::UpdateTextInputState, which pushes the text to Android and restarts the IME's
// input connection (GameActivity_restartInput, see the Editor's AndroidPlayer/Source/GameActivity/UGASoftKeyboard.cpp),
// and a restart ends the keyboard's key repeat; UI Toolkit's soft keyboard keeps its keyboard in step with the field.
// The HUD chat (ChatView) does the same with its own send / close rules.
//   Attach(field, done): on a device with an on-screen keyboard (else nothing). Focusing the field opens it with the
//   field's text, type and password flag; while it is up its text goes into the field (cut to the field's maxLength there,
//   never written back) with the caret where the keyboard's is; Done / the checkmark calls 'done' (true = open it again
//   for the next line, the chat's send), Back or a tap outside it ends the typing (the field loses the focus, the text
//   stays); the field losing the focus closes it, except to a 'keep' button of its row (the chat's Local / Global and
//   Send: a player's report, tapping the channel closed the keyboard), which hands the focus back after its tap.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class PhoneKeyboard
    {
        /// <summary>A device with an on-screen keyboard (phones, tablets).</summary>
        public static bool Supported => Application.isMobilePlatform && TouchScreenKeyboard.isSupported;

        static TextField owner;
        static TouchScreenKeyboard keyboard;
        static Func<bool> ownerDone;
        static int openFrame, keepFrame = -10;
        static IVisualElementScheduledItem poll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            owner = null;
            keyboard = null;
            ownerDone = null;
            poll = null;
        }

        /// <summary>The field types through the game's own keyboard on a phone. 'done': the keyboard's Done (null = just end
        /// the typing); returns true to open the keyboard again (a line sent, the next one).</summary>
        public static void Attach(TextField field, Func<bool> done = null, params VisualElement[] keep)
        {
            if (field == null || !Supported) return;
            field.hideSoftKeyboard = true;
            field.selectAllOnFocus = false;
            field.selectAllOnMouseUp = false;
            field.RegisterCallback<FocusInEvent>(_ => Open(field, done));
            field.RegisterCallback<FocusOutEvent>(_ => { if (owner == field && Time.frameCount - keepFrame > 1) Close(); });
            foreach (var k in keep)
            {
                if (k == null) continue;
                // The press: the focus leaving the line now doesn't close the keyboard; the release: the line takes it back.
                k.RegisterCallback<PointerDownEvent>(_ => { if (owner == field) keepFrame = Time.frameCount; }, TrickleDown.TrickleDown);
                k.RegisterCallback<PointerUpEvent>(_ =>
                {
                    if (owner != field) return;
                    keepFrame = Time.frameCount;
                    field.schedule.Execute(() => { if (owner == field && field.panel != null) field.Focus(); }).ExecuteLater(1);
                });
            }
            field.RegisterCallback<DetachFromPanelEvent>(_ => { if (owner == field) Close(); });
        }

        static void Open(TextField field, Func<bool> done)
        {
            if (owner == field && keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible) return;
            if (owner != null && owner != field) Close();
            owner = field;
            ownerDone = done;
            TouchScreenKeyboard.hideInput = false;
            string text = field.value ?? "";
            keyboard = TouchScreenKeyboard.Open(text, field.keyboardType, true, false, field.isPasswordField);
            openFrame = Time.frameCount;
            if (keyboard != null) keyboard.selection = new RangeInt(text.Length, 0);   // with the Open: no extra restart
            poll?.Pause();
            poll = field.schedule.Execute(Poll).Every(0);
        }

        static void Close()
        {
            poll?.Pause();
            poll = null;
            if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible) keyboard.active = false;
            keyboard = null;
            owner = null;
            ownerDone = null;
        }

        /// <summary>Every frame while the keyboard is up: its text into the field. Gone: Done runs 'done', anything else
        /// ends the typing.</summary>
        static void Poll()
        {
            var field = owner;
            if (field == null || keyboard == null) { Close(); return; }
            string text = keyboard.text ?? "";
            if (field.maxLength > 0 && text.Length > field.maxLength) text = text.Substring(0, field.maxLength);
            var status = keyboard.status;
            if (status == TouchScreenKeyboard.Status.Visible)
            {
                if (field.value != text)
                {
                    field.value = text;
                    var sel = keyboard.selection;
                    int at = sel.start >= 0 && sel.start <= text.Length ? sel.start + Mathf.Max(0, sel.length) : text.Length;
                    at = Mathf.Min(at, text.Length);
                    field.SelectRange(at, at);
                }
                return;
            }
            if (Time.frameCount - openFrame <= 5) return;   // still coming up
            var done = ownerDone;
            bool doneKey = status == TouchScreenKeyboard.Status.Done;
            if (doneKey) field.value = text;   // some keyboards hand the last word over only when they close
            Close();
            if (doneKey && done != null && done())
            {
                if (field.panel != null && field.focusController?.focusedElement == field) Open(field, done);
                else field.Focus();   // FocusIn opens it
                return;
            }
            field.Blur();
        }
    }
}
