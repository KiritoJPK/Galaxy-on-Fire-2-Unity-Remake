// ChatView.cs
// Multiplayer chat panel (NetChat) in the flight HUD and the station menu, while a session runs: on the left, the recent
// lines (fading 12 s after they came in, all shown while typing) over an input row. The chat key (B, GameControls.Chat;
// or the small "Chat" button above the lines, sized for a finger) opens the input with the cursor in it, the send key (Enter / keypad Enter,
// GameControls.ChatSend) sends, the channel key (Tab, GameControls.ChatChannel) switches Local / Global, Esc closes; all
// rebindable in Options > Controls (read here from the devices: the game's keys are off while it is open,
// NetChat.SetTyping). The field keeps the focus while typing: the UI's navigation (the arrows, W A S D, Tab, Space /
// Enter as submit) would otherwise move it to a menu button and end the typing. Local lines reach the players in this
// orbit or docked here, global ones everyone. Another player's line plays the original's incoming-message sound (FMOD
// event 125 Message_Inc, volume 0.241, one at a time; a copy of the clip in Resources/GoF2Net/ChatMessage).
// A line starting with "/" is a command (NetCommands): the matching commands show over it, or after a command that takes a
// player the matching players, and Tab completes the first, then cycles through them (Shift+Tab back; "/" alone cycles
// every command; NetCommands.Completions); Tab then doesn't switch the channel. Private messages (/w) show as
// "[From X]" / "[To X]" in violet. The network stats (NetStats) show top left while
// /netstats has them on.
// Enter (and keypad Enter) always sends, read from the key event itself: the rebindable send key is read from the device
// (GameControls.PressedNow), which can miss the frame the UI's key event arrives in, and the TextField then took the
// Enter as its own submit, lost the focus and the line was only hidden (Suspend), never sent. The row also has a Send
// button (touch, mouse). While the station's multiplayer window (MultiplayerWindow) is open, its Chat tab is the chat: this
// panel hides and the chat key doesn't open it.
// Phones (TouchScreenKeyboard): the chat opens the on-screen keyboard itself instead of the field's own (hideSoftKeyboard),
// so it can tell the keyboard's Done / checkmark (sends the line) from Back or a tap outside it (closes, the draft kept);
// the field's own keyboard only closed and blurred on Done, so the line was never sent.
// A conversation stays open (remake rework): sending keeps the line (PC, controller: the field keeps the focus; phones: the
// keyboard comes straight back up), so a player can answer without opening the chat again; Enter / Done on an empty line,
// Esc, the row's close button or the Chat tab close it. Phones: while typing the chat sits at the top of the screen
// (.chat--phone.chat--open), where the on-screen keyboard can't cover the line (at 36 % its input row ended up under it).
// Chat rework (players' report from a Retroid Pocket G2): placed and sized relative to the HUD (Chat.uss percentages), and
// on a small high-density screen (UiScale.Large) the large variant (.chat--large: about 1.5x, finger-sized buttons) with
// fewer lines (LargeLines; LargeOpenLines while a phone types, so the line stays above the keyboard). The on-screen
// keyboard shows its own input box again (TouchScreenKeyboard.hideInput false): with it hidden, holding Backspace stopped
// after one letter and the line stayed hidden under the keyboard; the box is the system's own editing (hold to delete,
// the cursor, selection). The line never selects all on focus: after a send the next line's letters showed highlighted.
// The Chat button sits next to the Multiplayer button (MultiplayerWindow moves it: left of it in the station's top bar, at
// its height in the flight HUD's column), the size of the Menu button.
// Styles: Resources/GoF2Net/Chat.uss.

using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public sealed class ChatView : MonoBehaviour
    {
        const float ShowSeconds = 12f, FadeSeconds = 2f;
        const int Lines = 8, LargeLines = 5, LargeOpenLines = 4;
        /// <summary>Message_Inc's event volume (FMOD event 125).</summary>
        const float MessageVolume = 0.241f;
        /// <summary>Frames Open keeps focusing the field (the row only shows once its style has applied).</summary>
        const int FocusFrames = 15;

        VisualElement box, log, suggest;
        Label stats;
        // Tab completion: the line typed before the first Tab (null = not cycling) and the completion shown.
        string completionLine;
        int completionIndex = -1;
        string completedText;   // the line Tab last wrote (its change event arrives later: not newly typed)
        float nextStats;
        TextField field;
        Button channel, sendButton, closeButton;
        Button tab;
        TouchScreenKeyboard keyboard;   // phones: the on-screen keyboard the chat opened (null when none)
        int keyboardFrame = -10;        // the frame it was opened (it may not report Visible straight away)
        AudioSource sound;
        AudioClip messageClip;
        bool open, hooked, large;
        float nextScaleCheck;
        int focusTries, swallowFrame = -1, openFrame = -10, suspendFrame = -10;

        /// <summary>The panel on 'parent' (again after a UI reload), on the HUD's own GameObject.</summary>
        public static void Attach(GameObject host, VisualElement parent)
        {
            if (parent == null) return;
            var view = host.GetComponent<ChatView>();
            if (view == null) view = host.AddComponent<ChatView>();
            view.host = host;
            view.Build(parent);
        }

        GameObject host;

        void Build(VisualElement parent)
        {
            box?.RemoveFromHierarchy();
            tab?.RemoveFromHierarchy();   // it may have been moved next to the Multiplayer button
            stats?.RemoveFromHierarchy();
            box = new VisualElement { name = "chat", pickingMode = PickingMode.Ignore };
            box.AddToClassList("chat");
            var sheet = Resources.Load<StyleSheet>("GoF2Net/Chat");
            if (sheet != null) box.styleSheets.Add(sheet);

            tab = new Button { name = "chatTab", focusable = false };   // never a stop for the menus' navigation
            tab.AddToClassList("chat-tab");
            // MultiplayerWindow moves the tab next to its Multiplayer button (the station's top bar, the flight HUD's
            // column): its own sheet goes with it, and its state classes are its own (TabClasses).
            if (sheet != null) tab.styleSheets.Add(sheet);
            // Opened on the press (not the release), and the touch stays off the HUD under it (steering, the fire area).
            // Open, it closes the chat (the press may already have taken the field's focus this frame: Suspend first).
            tab.RegisterCallback<PointerDownEvent>(e =>
            {
                if (open) Close();
                else if (suspendFrame >= Time.frameCount - 1) field.value = "";   // closed by this very press
                else Open();
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            box.Add(tab);
            log = new VisualElement { pickingMode = PickingMode.Ignore };
            log.AddToClassList("chat-log");
            box.Add(log);
            suggest = new VisualElement { pickingMode = PickingMode.Ignore };
            suggest.AddToClassList("chat-suggest");
            suggest.style.display = DisplayStyle.None;
            box.Add(suggest);

            var row = new VisualElement();
            row.AddToClassList("chat-input-row");
            channel = new Button(ToggleChannel);
            channel.AddToClassList("chat-channel");
            row.Add(channel);
            field = new TextField { maxLength = NetChat.MaxCommandLength };   // a chat line is cut to MaxLength when sent
            field.AddToClassList("chat-field");
            field.hideSoftKeyboard = SoftKeyboard;   // phones: the chat's own keyboard (OpenKeyboard), whose Done it can see
            field.selectAllOnFocus = false;          // refocused after a send, the next line's letters showed selected
            field.selectAllOnMouseUp = false;
            field.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            field.RegisterValueChangedCallback(e =>
            {
                if (e.newValue != completedText) { completionLine = null; completionIndex = -1; completedText = null; }   // typed: start over
                RefreshSuggestions();
            });
            // The UI's navigation keeps out of the line: no focus move (the arrows move the cursor, W A S D / Space type).
            field.RegisterCallback<NavigationCancelEvent>(e => { Close(); Swallow(e); }, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            // Focus going anywhere but the row's own buttons (a click on the game, Android closing the keyboard): the
            // typing ends, the draft stays; otherwise the game's keys would stay off.
            field.RegisterCallback<FocusOutEvent>(e =>
            {
                if (e.relatedTarget is VisualElement to && (to == channel || to == sendButton || to == closeButton)) return;
                if (focusTries > 0) return;   // still opening
                Suspend();
            });
            row.Add(field);
            sendButton = new Button(SendLine) { text = Localization.Extra("mpChatSend", "Send").ToUpperInvariant() };
            sendButton.AddToClassList("chat-channel");
            sendButton.AddToClassList("chat-send");
            row.Add(sendButton);
            closeButton = new Button(Close) { text = "×" };   // touch / mouse; Esc, B and Enter on an empty line too
            closeButton.AddToClassList("chat-channel");
            closeButton.AddToClassList("chat-close");
            row.Add(closeButton);
            box.Add(row);
            parent.Add(box);
            stats = new Label { name = "netstats", pickingMode = PickingMode.Ignore };
            stats.AddToClassList("netstats");
            // The station menu: top right under its Menu button (top left is the system block and the join-code plate).
            stats.EnableInClassList("netstats--station", host.GetComponent<StationMenu>() != null);
            if (sheet != null) stats.styleSheets.Add(sheet);
            stats.style.display = DisplayStyle.None;
            parent.Add(stats);

            if (!hooked) { NetChat.Added += OnAdded; GoF2Remake.Flight.GameControls.Changed += RefreshKeys; hooked = true; }
            RefreshKeys();
            RefreshChannel();
            Rebuild();
            SetOpenClass(open);
            box.EnableInClassList("chat--phone", SoftKeyboard);
            nextScaleCheck = 0f;
        }

        /// <summary>The large variant on a small high-density screen (UiScale), checked once a second (a resolution change).</summary>
        void UpdateScale()
        {
            if (Time.unscaledTime < nextScaleCheck || box.panel == null) return;
            nextScaleCheck = Time.unscaledTime + 1f;
            bool l = UiScale.Large(box);
            if (l == large && box.ClassListContains("chat--large") == l) return;
            large = l;
            box.EnableInClassList("chat--large", l);
            tab.EnableInClassList("chat-tab--large", l);
            Rebuild();
        }

        /// <summary>The box's open state, and the tab's (it may sit elsewhere: MultiplayerWindow).</summary>
        void SetOpenClass(bool on)
        {
            box.EnableInClassList("chat--open", on);
            tab.EnableInClassList("chat-tab--open", on);
            tab.EnableInClassList("chat-tab--phone-open", on && SoftKeyboard);
        }

        int VisibleLines => !large ? Lines : open && SoftKeyboard ? LargeOpenLines : LargeLines;

        void OnDestroy()
        {
            if (hooked) { NetChat.Added -= OnAdded; GoF2Remake.Flight.GameControls.Changed -= RefreshKeys; }
            if (open) NetChat.DropTyping();   // the scene's actions go with it (not enabled again)
            CloseKeyboard();
        }

        /// <summary>A device with an on-screen keyboard (phones, tablets).</summary>
        static bool SoftKeyboard => Application.isMobilePlatform && TouchScreenKeyboard.isSupported;

        /// <summary>Phones: the on-screen keyboard for the line (the draft in it, the cursor at its end). Its own input box is
        /// hidden: the text shows in the chat's field (PollKeyboard copies it over).</summary>
        void OpenKeyboard()
        {
            if (!SoftKeyboard) return;
            if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible) return;
            TouchScreenKeyboard.hideInput = false;   // the system's input box: its own editing (hold Backspace), above the keyboard
            string text = field.value ?? "";
            keyboard = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default, true, false, false);
            keyboardFrame = Time.frameCount;
            if (keyboard != null) keyboard.selection = new RangeInt(text.Length, 0);
        }

        void CloseKeyboard()
        {
            if (keyboard == null) return;
            if (keyboard.status == TouchScreenKeyboard.Status.Visible) keyboard.active = false;
            keyboard = null;
        }

        /// <summary>Phones, every frame while the chat's keyboard is up: its text into the line; once it is gone, Done (the
        /// checkmark / Enter) sends, Back or a tap outside it ends the typing with the draft kept (as a click elsewhere).</summary>
        void PollKeyboard()
        {
            if (keyboard == null) return;
            string text = keyboard.text ?? "";
            if (text.Length > field.maxLength) text = text.Substring(0, field.maxLength);
            var status = keyboard.status;
            if (status == TouchScreenKeyboard.Status.Visible)
            {
                if (field.value != text)
                {
                    field.value = text;
                    // The caret where the keyboard's is (else it stayed put while the text grew past it, off the field's end).
                    var sel = keyboard.selection;
                    int at = sel.start >= 0 && sel.start <= text.Length ? sel.start + Mathf.Max(0, sel.length) : text.Length;
                    field.SelectRange(Mathf.Min(at, text.Length), Mathf.Min(at, text.Length));
                }
                return;
            }
            if (Time.frameCount - keyboardFrame <= 5) return;   // still coming up
            keyboard = null;
            if (!open) return;
            if (status == TouchScreenKeyboard.Status.Done)
            {
                field.value = text;   // some keyboards hand the last word over only when they close
                SendLine();           // an empty line closes; otherwise the keyboard comes back for the next one
                return;
            }
            field.Blur();
            Suspend();
        }

        /// <summary>An event the line keeps to itself: no other handler, no focus move.</summary>
        void Swallow(EventBase e)
        {
            e.StopPropagation();
            field.focusController?.IgnoreEvent(e);
        }

        void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape) { Close(); Swallow(e); return; }
            // The chat key that opened the line types its letter a moment later (the text arrives after the focus): the
            // characters of the first frames after opening stay out (nobody types that fast).
            if (Time.frameCount - openFrame <= 2 && e.character != '\0') { Swallow(e); return; }
            // A command being typed: Tab completes / cycles it (before the channel key, Tab by default).
            if (e.keyCode == KeyCode.Tab && (completionLine != null || (field.value.Length > 0 && field.value[0] == '/')))
            {
                swallowFrame = Time.frameCount;
                Swallow(e);
                Complete(e.shiftKey ? -1 : 1);
                return;
            }
            // The send / channel keys (rebindable): their key events, and the character the key would type, stay out of the line.
            bool send = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n'
                        || GoF2Remake.Flight.GameControls.PressedNow(GoF2Remake.Flight.GameControls.ChatSend);
            bool channelKey = !send && GoF2Remake.Flight.GameControls.PressedNow(GoF2Remake.Flight.GameControls.ChatChannel);
            if ((e.keyCode != KeyCode.None || e.character == '\n') && (send || channelKey) && swallowFrame != Time.frameCount)
            {
                swallowFrame = Time.frameCount;
                Swallow(e);
                if (send) SendLine();
                else ToggleChannel();
                return;
            }
            if (swallowFrame == Time.frameCount) { Swallow(e); return; }
            // Never a tab in the line, and Tab never moves the focus.
            if (e.keyCode == KeyCode.Tab || e.character == '\t') Swallow(e);
        }

        /// <summary>Tab: the first completion of what was typed, then the next one each time (dir -1: back), wrapping.</summary>
        void Complete(int dir)
        {
            string typed = completionLine ?? field.value;
            var matches = NetCommands.Completions(typed);
            if (matches.Count == 0) return;
            if (completionLine == null)
            {
                completionLine = typed;
                completionIndex = dir > 0 ? 0 : matches.Count - 1;
            }
            else completionIndex = ((completionIndex + dir) % matches.Count + matches.Count) % matches.Count;
            string text = matches[completionIndex].line;
            completedText = text;
            field.value = text;
            field.schedule.Execute(() => field.SelectRange(text.Length, text.Length));   // the cursor at the end
            RefreshSuggestions();
        }

        /// <summary>The completions of the line (as typed before cycling), the one Tab picked highlighted.</summary>
        void RefreshSuggestions()
        {
            if (suggest == null) return;
            suggest.Clear();
            string typed = open ? completionLine ?? field.value : null;
            var matches = typed != null ? NetCommands.Completions(typed) : null;
            bool show = matches != null && matches.Count > 0;
            suggest.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            int picked = completionLine != null ? completionIndex : -1;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var line = new Label($"<b>{m.label}</b>   <color=#9fb3c0>{m.description}</color>") { pickingMode = PickingMode.Ignore };
                line.AddToClassList("chat-suggest-row");
                line.EnableInClassList("chat-suggest-row--picked", i == picked);
                suggest.Add(line);
            }
            var hint = new Label(Localization.Extra("mpCmdTabHint", "Tab complete  ·  Enter run")) { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("chat-suggest-hint");
            suggest.Add(hint);
        }

        void Open()
        {
            open = true;
            SetOpenClass(true);
            NetChat.SetTyping(true);
            focusTries = FocusFrames;   // Update focuses the field once the row shows (a hidden element can't take it)
            openFrame = Time.frameCount;
            field.Focus();
            OpenKeyboard();
            Rebuild();
            RefreshSuggestions();
        }

        bool FieldFocused()
        {
            var f = field?.focusController?.focusedElement as VisualElement;
            return f != null && (f == field || field.Contains(f));
        }

        void Close()
        {
            if (!open) return;
            open = false;
            SetOpenClass(false);
            field.value = "";
            CloseKeyboard();
            field.Blur();
            NetChat.SetTyping(false);
            Rebuild();
        }

        /// <summary>The send key, Done or Send: the line goes and the chat stays open for the next one (the field keeps the
        /// focus, a phone's keyboard comes back up); an empty line closes it.</summary>
        void SendLine()
        {
            string line = field.value;
            if (string.IsNullOrWhiteSpace(line)) { Close(); return; }
            NetChat.Send(line);
            if (!open) return;   // the command ended the session or left the scene
            completionLine = null; completionIndex = -1; completedText = null;
            field.value = "";
            field.SelectRange(0, 0);
            focusTries = FocusFrames;
            field.Focus();
            OpenKeyboard();
            RefreshSuggestions();
        }

        /// <summary>A chat line as rich text (this panel and the multiplayer window's Chat tab).</summary>
        internal static string Format(NetChat.Message m)
        {
            if (m.channel == NetChat.Channel.Notice) return m.text;
            if (m.channel == NetChat.Channel.Whisper)
            {
                // A private message: from the other player, or the copy of one's own to them.
                string w = string.Format(m.own ? Localization.Extra("mpWhisperTo", "To {0}") : Localization.Extra("mpWhisperFrom", "From {0}"), m.from);
                return $"<color=#d6a2ff>[{w}]</color> {m.text}";
            }
            string tag = m.channel == NetChat.Channel.Global ? Localization.Extra("mpChatGlobal", "Global") : Localization.Extra("mpChatLocal", "Local");
            string color = m.channel == NetChat.Channel.Global ? "#f0b35a" : "#8fd8ff";
            return $"<color={color}>[{tag}]</color> <b>{m.from}</b>: {m.text}";
        }

        /// <summary>The field lost the focus: no more typing (the game's keys back), the draft kept for the next Open.</summary>
        void Suspend()
        {
            if (!open) return;
            open = false;
            suspendFrame = Time.frameCount;
            CloseKeyboard();
            SetOpenClass(false);
            NetChat.SetTyping(false);
            Rebuild();
            RefreshSuggestions();
        }

        void ToggleChannel()
        {
            NetChat.Sending = NetChat.Sending == NetChat.Channel.Global ? NetChat.Channel.Local : NetChat.Channel.Global;
            RefreshChannel();
        }

        /// <summary>The tab and the empty line name the current keys (they are rebindable).</summary>
        void RefreshKeys()
        {
            if (tab == null) return;
            string key = GoF2Remake.Flight.GameControls.KeyText(GoF2Remake.Flight.GameControls.Chat, false);
            // The key as a hint after the name, not on phones (no keyboard to press it on).
            bool showKey = key.Length > 0 && !Application.isMobilePlatform;
            tab.text = Localization.Extra("mpChat", "Chat").ToUpperInvariant() + (showKey ? $"  <color=#8fd8ff>{key}</color>" : "");
            string sendKey = GoF2Remake.Flight.GameControls.KeyText(GoF2Remake.Flight.GameControls.ChatSend, false);
            string channelKey = GoF2Remake.Flight.GameControls.KeyText(GoF2Remake.Flight.GameControls.ChatChannel, false);
            var hints = new System.Collections.Generic.List<string>();
            if (sendKey.Length > 0) hints.Add(string.Format(Localization.Extra("mpChatSendHint", "{0} send"), sendKey));
            if (channelKey.Length > 0) hints.Add(string.Format(Localization.Extra("mpChatChannelHint", "{0} channel"), channelKey));
            hints.Add(Localization.Extra("mpChatCloseHint", "Esc close"));
            // Phones: no keys to name.
            field.textEdition.placeholder = SoftKeyboard ? Localization.Extra("mpChatPhoneHint", "Type a message")
                : string.Join("  ·  ", hints);
        }

        void RefreshChannel()
        {
            bool global = NetChat.Sending == NetChat.Channel.Global;
            channel.text = (global ? Localization.Extra("mpChatGlobal", "Global") : Localization.Extra("mpChatLocal", "Local")).ToUpperInvariant();
            channel.EnableInClassList("chat-channel--global", global);
        }

        void OnAdded(NetChat.Message m)
        {
            Rebuild();
            // A line from another player (not one's own, not a join / leave notice): the incoming-message sound.
            if (m.channel != NetChat.Channel.Notice && !m.own) PlayMessageSound();
        }

        void PlayMessageSound()
        {
            if (messageClip == null) messageClip = Resources.Load<AudioClip>("GoF2Net/ChatMessage");
            if (messageClip == null) return;
            if (sound == null)
            {
                sound = gameObject.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.spatialBlend = 0f;
            }
            if (sound.isPlaying) return;   // max_playbacks 1
            sound.clip = GoF2Remake.Modding.ModSounds.Get(messageClip);
            sound.volume = Mathf.Clamp01(MessageVolume * GoF2Remake.Flight.Sfx.EventGain * Settings.SfxVolume);
            sound.Play();
        }

        void Rebuild()
        {
            if (log == null) return;
            log.Clear();
            var all = NetChat.Messages;
            for (int i = Mathf.Max(0, all.Count - VisibleLines); i < all.Count; i++)
            {
                var m = all[i];
                var line = new Label { pickingMode = PickingMode.Ignore, userData = m, text = Format(m) };
                line.AddToClassList("chat-line");
                if (m.channel == NetChat.Channel.Notice) line.AddToClassList("chat-line--notice");
                else if (m.own) line.AddToClassList("chat-line--own");
                log.Add(line);
            }
        }

        /// <summary>The /netstats overlay, refreshed 4 times a second while it shows.</summary>
        void UpdateStats(bool session)
        {
            if (stats == null) return;
            string text = session && NetStats.Shown && Time.unscaledTime >= nextStats ? NetStats.Text() : null;
            bool show = session && NetStats.Shown;
            stats.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (text == null) return;
            nextStats = Time.unscaledTime + 0.25f;
            stats.text = text;
        }

        void Update()
        {
            NetChat.KeepGameKeysOff();
            if (box == null) return;
            UpdateScale();
            bool session = NetGame.Active;
            bool window = MultiplayerWindow.IsOpenAny;   // the station's multiplayer window: its Chat tab is the chat meanwhile
            box.style.display = session && !window ? DisplayStyle.Flex : DisplayStyle.None;
            // Moved next to the Multiplayer button: shown inline (its new classes may hide it, the station's with a controller);
            // a phone typing hides it (the line stays above the keyboard).
            if (tab.parent != box) tab.style.display = session && !window && !(open && SoftKeyboard) ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateStats(session);
            if (!session || window) { if (open) Suspend(); return; }
            PollKeyboard();
            if (!open && !NetChat.Typing && GoF2Remake.Flight.GameControls.Chat.WasPressedThisFrame()) Open();   // rebindable (B)
            // Opening: the cursor goes into the line as soon as the row can take the focus.
            if (open && focusTries > 0)
            {
                if (FieldFocused()) focusTries = 0;
                else { focusTries--; field.Focus(); }
            }
            // Lines fade out a while after they came in (all shown while typing).
            float now = Time.unscaledTime;
            foreach (var child in log.Children())
            {
                if (!(child.userData is NetChat.Message m)) continue;
                float age = now - m.time;
                float a = open ? 1f : Mathf.Clamp01(1f - (age - ShowSeconds) / FadeSeconds);
                child.style.opacity = a;
                child.style.display = a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
