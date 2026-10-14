// MultiplayerWindow.cs
// Remake-only: the station's multiplayer window (Chat / Faction / Arena / Profile / Admin) in a session, the buttons for
// everything the chat commands do (NetFactions, NetArena, NetProfiles). A "MULTIPLAYER" button in the top bar, left of the
// Menu button, opens it (a dot on it when a faction invitation or a duel challenge waits). Tabs:
//   Chat: the whole chat (NetChat: the last lines, kept while the window lives), the channel (Local / Global), the line
//     and a Send button; Enter sends, a line starting with "/" is a command (NetCommands); the flight / station chat
//     panel (ChatView) hides meanwhile. Built once (a snapshot doesn't rebuild it: the line keeps its focus and draft);
//   Squad (NetSquad, client-side, rebuilt when it changes): invitations (Accept / Decline), the members with where they
//     are and a distress call (Help), Leave, the pilots docked here to Invite; distress calls themselves are made in space
//     (the flight squad window, the E menu, /sos), which the tab says;
//   Trade (NetTradeClient, client-side like Squad): the trade requests (Accept / Decline); without a trade the pilots
//     docked here to Trade with; in one, this player's offer (credits, the goods with -1 / remove) beside the other's,
//     the tradeable cargo to add (+1 / +10 / All), who accepted, Accept trade / Cancel trade. It opens by itself when a
//     trade starts (NetTradeClient.Opened);
//   Faction: invitations (Join), without a faction a Create form and the factions; in one: the bank (Deposit / Withdraw), the
//     members (Promote / Demote / Make leader / Kick by rank), the pilots online to Invite, the territory (the claims,
//     and for the station docked at: Claim / Make home / Unclaim / Siege), the sieges, Leave / Disband (asked twice);
//   Arena: a challenge waiting (Accept / Decline), the Void fighters option, the pilots to Challenge, the free-for-all
//     queue (Join / Leave), the matches, the leaderboard;
//   Profile: the profile, Take control (another device controls it), Get a link code, Link with a code (+ force);
//   Admin (only for the server's ops, admins and masters, NetModeration): a reason and minutes field, every pilot online
//     with Kick (the minutes as the cooldown), Ban for the minutes, Ban for good and the roles (admins: op; masters:
//     admin), the bans with Unban; for admins also the server's status and an announcement, the staff, every profile
//     (a filter; Ban / Unban, roles, Delete for masters, asked twice), the factions (Disband, asked twice) and the server's
//     settings (NetServerSettings: a field or a switch with Save each; the ones the launcher's command line sets say so).
// The Profile tab also has "Claim this server" (the server's admin token, /claimadmin) for its owner.
// Every button sends the chat command (NetPanel.Command) and the window shows the server's answer (the next chat notice)
// at its foot. The content comes from the server's snapshot (NetPanel.Latest, asked for every 2 s while open) and is
// rebuilt only when it changed, so a text field keeps its focus. Esc / B closes it (StationMenu.Back); while it is open
// the station menu's own keys wait. Built in code; the buttons use Squad.uss.

using System;
using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class MultiplayerWindow : MonoBehaviour
    {
        enum Tab { Chat, Squad, Trade, Faction, Arena, Profile, Admin }

        static MultiplayerWindow current;

        /// <summary>The window is open (StationMenu: Esc closes it, its keys wait).</summary>
        public static bool IsOpenAny => current != null && current.isOpen;

        public static void CloseAny() { if (current != null) current.Close(); }

        VisualElement plate, window, tabs, body, chatPane;
        Button sosButton;
        ScrollView scroll, chatScroll;
        TextField chatField;
        Button chatChannel, menuButton;
        Button plateButton;
        Label status;
        Tab tab;
        bool isOpen, voids, force, confirmLeave;
        string tagText = "", nameText = "", amountText = "", linkText = "", reasonText = "", minutesText = "5";
        string sayText = "", filterText = "", tokenText = "", confirmKey = "";
        readonly Dictionary<string, string> settingEdits = new Dictionary<string, string>();
        int shownRole = -1;
        float refresh, answerUntil;
        bool typing;              // one of the window's text fields has the focus: the game's keys are off (NetChat.SetTyping)
        bool pendingRebuild;      // a snapshot came while a field had the focus or a finger / button was down
        float pressedSince = -1f; // a pointer went down on the window (unscaled time), -1 = none
        StyleSheet sheet;

        static readonly Color Panel = new Color(0.02f, 0.04f, 0.07f, 0.93f), Accent = new Color(0.56f, 0.85f, 1f),
                              Dim = new Color(0.85f, 0.92f, 0.97f, 0.65f), Good = new Color(0.47f, 0.9f, 0.55f), Bad = new Color(1f, 0.55f, 0.47f);

        /// <summary>The window's button and window on 'parent' (the station menu's or the flight HUD's safe area).</summary>
        public static void Attach(GameObject host, VisualElement parent, bool flight = false)
        {
            if (parent == null) return;
            var view = host.GetComponent<MultiplayerWindow>();
            if (view == null) view = host.AddComponent<MultiplayerWindow>();
            view.flight = flight;
            view.Build(parent);
        }

        /// <summary>The flight HUD's: its own button on the right (under the readout, over the squad window), N toggles it.</summary>
        bool flight;

        /// <summary>FlightHud: N (or the button) opens / closes it.</summary>
        public static void ToggleAny() { if (current != null) current.Toggle(); }

        /// <summary>A text field of the window has the focus (its keys aren't the game's: FlightHud leaves N alone).</summary>
        public static bool TypingAny => current != null && current.isOpen && TextFieldKeys.InTextField(current.window?.focusController?.focusedElement);

        void OnEnable()
        {
            current = this;
            NetPanel.Changed += OnChanged;
            NetChat.Added += OnChat;
            NetTradeClient.Opened += OnTradeOpened;
        }

        void OnDisable()
        {
            if (current == this) current = null;
            NetPanel.Changed -= OnChanged;
            NetChat.Added -= OnChat;
            NetTradeClient.Opened -= OnTradeOpened;
        }

        /// <summary>The Trade tab opened (the pilot list's Trade, a trade request's Accept).</summary>
        public static void OpenTradeAny() { if (current != null && !current.flight) current.OpenTab(Tab.Trade); }

        /// <summary>A trade started: its tab (docked: the station's window).</summary>
        void OnTradeOpened() { if (!flight) OpenTab(Tab.Trade); }

        void OpenTab(Tab t)
        {
            tab = t;
            confirmLeave = false;
            BuildTabs();
            if (isOpen) { ShowPane(); Rebuild(); }
            else Open();
        }

        void OnDestroy()
        {
            if (typing) { typing = false; NetChat.DropTyping(); }   // the scene's actions go with it (ChatView does the same)
            sosButton?.RemoveFromHierarchy();
            plate?.RemoveFromHierarchy();
            window?.RemoveFromHierarchy();
        }

        // ---- building -------------------------------------------------------------------------------------

        void Build(VisualElement parent)
        {
            sosButton?.RemoveFromHierarchy();
            sosButton = null;
            plate?.RemoveFromHierarchy();
            window?.RemoveFromHierarchy();
            sheet = Resources.Load<StyleSheet>("GoF2Net/Squad");

            // The button: in the top bar, left of the Menu button (its look); else under the station's information.
            plate = new VisualElement { name = "factionPlate" };
            var menu = parent.Q<Button>("menuButton");
            menuButton = menu;
            if (flight)
            {
                // In flight: on the right, under the HUD readout (top right) and over the squad window (26 %).
                plateButton = Btn(Localization.Extra("mpMultiplayer", "Multiplayer"), Toggle, null);
                if (sheet != null) plateButton.styleSheets.Add(sheet);
                plateButton.style.position = Position.Absolute;
                plateButton.style.right = 24;
                plateButton.style.top = new Length(17, LengthUnit.Percent);
                plateButton.style.marginLeft = 0;
                plate = plateButton;
                parent.Add(plateButton);
                // The squad's distress call (NetDistress) under it: shown in a squad in space. A click / tap or the
                // "Distress call" binding (unbound by default) only: like the button above it never takes the focus, so
                // Space / Enter / a controller's A can't press it by accident.
                sosButton = Btn(Localization.Extra("mpDistressCall", "Distress call"), ToggleDistress, "squad-button--leave");
                if (sheet != null) sosButton.styleSheets.Add(sheet);
                sosButton.focusable = false;
                sosButton.style.position = Position.Absolute;
                sosButton.style.right = 24;
                sosButton.style.top = new Length(22, LengthUnit.Percent);
                sosButton.style.marginLeft = 0;
                sosButton.style.display = DisplayStyle.None;
                parent.Add(sosButton);
            }
            else if (menu != null && menu.parent != null)
            {
                plateButton = new Button(Toggle) { name = "netButton" };
                plateButton.AddToClassList("station-menu-button");
                plateButton.AddToClassList("gof-semibold");
                plateButton.style.marginRight = 12;
                plate = plateButton;
                menu.parent.Insert(menu.parent.IndexOf(menu), plateButton);
            }
            else
            {
                if (sheet != null) plate.styleSheets.Add(sheet);
                plateButton = Btn(Localization.Extra("mpFactionArena", "Faction · Arena"), Open, null);
                plateButton.style.marginTop = 8;
                plate.Add(plateButton);
                var info = parent.Q(className: "station-info");
                if (info != null && info.parent != null)
                {
                    int at = info.parent.IndexOf(info) + 1;
                    var code = info.parent.Q("joinCodePlate");
                    if (code != null && code.parent == info.parent) at = info.parent.IndexOf(code) + 1;
                    info.parent.Insert(at, plate);
                }
                else
                {
                    plate.style.position = Position.Absolute;
                    plate.style.left = 24; plate.style.top = 140;
                    parent.Add(plate);
                }
            }

            // A click / tap (or the "Multiplayer window" binding, N) opens it; never the focus (Space or a controller's A on a
            // focused button pressed it again in flight).
            if (plateButton != null) plateButton.focusable = false;

            window = new VisualElement { name = "factionWindow" };
            if (sheet != null) window.styleSheets.Add(sheet);
            var w = window.style;
            w.position = Position.Absolute;
            w.left = new Length(50, LengthUnit.Percent); w.top = new Length(50, LengthUnit.Percent);
            w.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
            w.width = new Length(64, LengthUnit.Percent); w.height = new Length(78, LengthUnit.Percent);
            w.minWidth = 560;
            w.backgroundColor = Panel;
            w.borderTopWidth = w.borderBottomWidth = w.borderLeftWidth = w.borderRightWidth = 1;
            w.borderTopColor = w.borderBottomColor = w.borderLeftColor = w.borderRightColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            w.paddingTop = w.paddingBottom = 12; w.paddingLeft = w.paddingRight = 18;
            w.display = DisplayStyle.None;
            // A press on the window holds a new snapshot back until it is released (a rebuild under the finger dropped the
            // button being pressed: "Claim" did nothing).
            window.RegisterCallback<PointerDownEvent>(_ => pressedSince = Time.unscaledTime, TrickleDown.TrickleDown);
            window.RegisterCallback<PointerUpEvent>(_ => pressedSince = -1f, TrickleDown.TrickleDown);
            window.RegisterCallback<PointerCancelEvent>(_ => pressedSince = -1f, TrickleDown.TrickleDown);

            var head = Row();
            head.style.justifyContent = Justify.SpaceBetween;
            tabs = Row();
            head.Add(tabs);
            head.Add(Btn("×", Close, "squad-button--leave"));
            window.Add(head);
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddManipulator(new DragScroll(scroll));   // a mouse drag moves the list like a finger (the menus' lists)
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 10;
            body = scroll.contentContainer;
            window.Add(scroll);
            BuildChatPane();
            window.Add(chatPane);
            status = Text("", 15, Accent);
            status.style.marginTop = 8;
            status.style.whiteSpace = WhiteSpace.Normal;
            window.Add(status);
            // Which build this is (testers compare it): the version and the code's branch / commit.
            var build = Text(string.Format(Localization.Extra("mpBuildLine", "build {0}"), BuildVersion.Text), 12, Dim);
            build.style.alignSelf = Align.FlexEnd;
            build.style.marginTop = 4;
            window.Add(build);
            parent.Add(window);
            BuildTabs();
        }

        void BuildTabs()
        {
            tabs.Clear();
            int role = NetPanel.Latest != null ? NetPanel.Latest.role : 0;
            shownRole = role;
            if (tab == Tab.Admin && role < NetModeration.Op) tab = Tab.Faction;
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                if (t == Tab.Admin && role < NetModeration.Op) continue;   // the moderation tab: ops and admins only
                var name = t == Tab.Faction ? Localization.Extra("mpTabFaction", "Faction") : t == Tab.Arena ? Localization.Extra("mpTabArena", "Arena")
                         : t == Tab.Profile ? Localization.Extra("mpTabProfile", "Profile") : Localization.Extra("mpTabAdmin", "Admin");
                if (t == Tab.Chat) name = Localization.Extra("mpChat", "Chat");
                if (t == Tab.Squad) name = Localization.Extra("mpTabSquad", "Squad");
                if (t == Tab.Trade) name = Localization.Extra("mpTabTrade", "Trade");
                var b = Btn(name, () => { tab = t; confirmLeave = false; BuildTabs(); ShowPane(); Rebuild(); }, t == tab ? "squad-button--accept" : null);
                b.style.marginRight = 8;
                tabs.Add(b);
            }
        }

        // ---- open / close / refresh -----------------------------------------------------------------------

        void Toggle() { if (isOpen) Close(); else Open(); }

        void Open()
        {
            if (!NetGame.Active) return;
            isOpen = true;
            confirmLeave = false;
            window.style.display = DisplayStyle.Flex;
            window.BringToFront();
            status.text = "";
            refresh = 0f;
            ShowPane();
            Rebuild();
        }

        void Close()
        {
            isOpen = false;
            pressedSince = -1f;
            if (window != null) window.style.display = DisplayStyle.None;
            if (window?.focusController?.focusedElement is VisualElement f) f.Blur();
        }

        void Update()
        {
            if (plate == null) return;
            // The window's text fields: the game's own keys (the station's 1 / 2 / M / L, the flight controls, the hangar's
            // A / D) are off while one has the focus.
            bool fieldFocused = TypingAny;
            if (fieldFocused != typing) { typing = fieldFocused; NetChat.SetTyping(fieldFocused); }
            if (pendingRebuild && !Busy) { pendingRebuild = false; Rebuild(); }
            UpdateSos();
            bool session = NetGame.Active;
            plate.style.display = session ? DisplayStyle.Flex : DisplayStyle.None;
            // In the top bar: the new button takes the bar's free space on its left while it shows.
            if (plate == plateButton && menuButton != null)
                menuButton.style.marginLeft = session ? new StyleLength(0f) : new StyleLength(StyleKeyword.Null);
            if (!session) { if (isOpen) Close(); return; }
            var s = NetPanel.Latest;
            bool waiting = (s != null && (s.factionInvites.Count > 0 || s.duelFrom.Length > 0))
                           || NetTradeClient.Requests.Count > 0 || (NetTradeClient.Current != null && !(isOpen && tab == Tab.Trade));
            bool unread = unreadChat && !(isOpen && tab == Tab.Chat);
            string label = plate == plateButton ? Localization.Extra("mpMultiplayer", "Multiplayer") : Localization.Extra("mpFactionArena", "Faction · Arena");
            plateButton.text = (label + (waiting || unread ? "  •" : "")).ToUpperInvariant();
            if (isOpen && tab == Tab.Squad && SquadKey() != squadKey) Rebuild();
            if (isOpen && tab == Tab.Trade && TradeKey() != tradeKey && !Busy) Rebuild();
            // The plate's dot needs a snapshot now and then even while the window is closed.
            if ((refresh -= Time.unscaledDeltaTime) <= 0f) { refresh = isOpen ? NetPanel.RefreshSeconds : NetPanel.RefreshSeconds * 3f; NetPanel.Request(); }
        }

        void OnChanged()
        {
            if (NetPanel.Latest != null && NetPanel.Latest.role != shownRole && tabs != null) BuildTabs();   // made an op / admin, or no longer
            if (!isOpen || tab == Tab.Squad || tab == Tab.Trade) return;   // they follow the players, not the snapshot
            // Not under a finger or a text field being typed into (a rebuild made new fields: the focus and Android's keyboard
            // went, and a pressed button was gone before its release): once they are done (Update).
            if (Busy) pendingRebuild = true;
            else Rebuild();
        }

        /// <summary>A pointer is down on the window (at most 5 s: a release lost elsewhere) or one of its text fields has the
        /// focus: the content stays as it is meanwhile.</summary>
        bool Busy => (pressedSince >= 0f && Time.unscaledTime - pressedSince < 5f)
                     || TextFieldKeys.InTextField(window?.focusController?.focusedElement) && body != null
                        && body.Contains(window.focusController.focusedElement as VisualElement);

        /// <summary>The flight button: in a squad in space (or while a call runs), its text the call's state.</summary>
        void UpdateSos()
        {
            if (sosButton == null) return;
            var me = NetPlayer.Local;
            bool show = NetGame.Active && (NetDistress.Active || (me != null && me.InSpace && NetSquad.LocalSquad != 0 && !NetArena.IsArenaOrbit(me.Station)));
            sosButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            string text = (NetDistress.Active ? Localization.Extra("mpDistressEnd", "End the call") : Localization.Extra("mpDistressCall", "Distress call")).ToUpperInvariant();
            if (sosButton.text != text) sosButton.text = text;
            sosButton.EnableInClassList("squad-button--leave", !NetDistress.Active);
        }

        /// <summary>FlightHud: the "Distress call" binding (unbound by default) or the button.</summary>
        public static void ToggleDistressAny() { if (current != null && current.flight) current.ToggleDistress(); }

        void ToggleDistress()
        {
            string msg = NetDistress.Toggle();
            if (!string.IsNullOrEmpty(msg)) NetChat.Notice(msg);
        }

        /// <summary>The server's answer to a button (the next notice), and invitations / challenges as they come.</summary>
        void OnChat(NetChat.Message m)
        {
            if (m == null) return;
            AddChatLine(m);
            if (m.channel != NetChat.Channel.Notice && !m.own && !(isOpen && tab == Tab.Chat)) unreadChat = true;
            if (!isOpen || m.channel != NetChat.Channel.Notice || tab == Tab.Chat) return;
            if (Time.unscaledTime < answerUntil) status.text = m.text;
            NetPanel.Request();
        }

        void Send(string command)
        {
            answerUntil = Time.unscaledTime + 4f;
            status.text = "…";
            confirmLeave = false;
            NetPanel.Command(command);
        }

        // ---- the tabs ---------------------------------------------------------------------------------------

        void Rebuild()
        {
            pendingRebuild = false;
            if (body == null) return;
            float y = scroll.scrollOffset.y;
            body.Clear();
            if (tab == Tab.Chat) return;   // the chat pane is built once (its line keeps the focus and the draft)
            if (tab == Tab.Squad) { BuildSquad(); scroll.scrollOffset = new Vector2(0f, y); return; }   // no snapshot needed
            if (tab == Tab.Trade) { BuildTrade(); scroll.scrollOffset = new Vector2(0f, y); return; }
            var s = NetPanel.Latest;
            if (s == null) { body.Add(Text(Localization.Extra("mpPanelLoading", "Asking the server..."), 16, Dim)); return; }
            switch (tab)
            {
                case Tab.Faction: BuildFaction(s); break;
                case Tab.Arena: BuildArena(s); break;
                case Tab.Admin: BuildAdmin(s); break;
                default: BuildProfile(s); break;
            }
            scroll.scrollOffset = new Vector2(0f, y);
        }

        void BuildFaction(NetPanel.State s)
        {
            if (!s.profiles) { body.Add(Text(Localization.Extra("mpPanelNoProfiles", "Factions need a server that keeps player profiles (a dedicated server)."), 16, Dim)); return; }
            if (s.guest) { body.Add(Text(Localization.Extra("mpPanelGuest", "You play as a guest here: factions need a profile (see the Profile tab)."), 16, Dim)); return; }
            foreach (var inv in s.factionInvites)
            {
                var parts = inv.Split('|');
                string tag = parts[0], name = parts.Length > 1 ? parts[1] : "";
                var row = Line($"[{tag}] {name} " + Localization.Extra("mpPanelInvites", "invites you to their faction."), Good);
                if (!s.inFaction) row.Add(Btn(Localization.Extra("mpPanelJoin", "Join"), () => Send($"/faction join {tag}"), "squad-button--accept"));
                body.Add(row);
            }
            if (!s.inFaction)
            {
                Section(Localization.Extra("mpPanelCreate", "Start a faction"));
                var row = Row();
                row.Add(Field(Localization.Extra("mpPanelTag", "Tag"), tagText, 4, 90, v => tagText = v));
                row.Add(Field(Localization.Extra("mpPanelName", "Name"), nameText, NetFactions.MaxNameLength, 260, v => nameText = v));
                row.Add(Btn(Localization.Extra("mpPanelCreateButton", "Create"), () => Send($"/faction create {tagText.Trim()} {nameText.Trim()}"), "squad-button--accept"));
                body.Add(row);
                body.Add(Text(Localization.Extra("mpPanelCreateHint", "A tag of 2-4 letters or digits shows before your members' names. Or ask a faction to invite you."), 14, Dim));
                FactionList(s);
                return;
            }
            bool officer = s.rank >= 1, leader = s.rank >= 2;
            var title = Text($"[{s.factionTag}] {s.factionName}", 24, Accent);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(title);
            body.Add(Text(s.rank == 2 ? Localization.Extra("mpRankLeader", "You lead this faction.") : s.rank == 1 ? Localization.Extra("mpRankOfficer", "You are an officer.")
                                                                                                                   : Localization.Extra("mpRankMember", "You are a member."), 14, Dim));

            Section(Localization.Extra("mpPanelBank", "Bank"));
            var bank = Row();
            bank.Add(Text($"{s.bank:N0} " + Localization.Extra("mpCredits", "credits"), 18, Color.white));
            bank.Add(Field(Localization.Extra("mpPanelAmount", "Amount"), amountText, 12, 150, v => amountText = v, TouchScreenKeyboardType.NumberPad));
            bank.Add(Btn(Localization.Extra("mpPanelDeposit", "Deposit"), () => Send($"/faction deposit {amountText.Trim()}"), "squad-button--accept"));
            if (officer) bank.Add(Btn(Localization.Extra("mpPanelWithdraw", "Withdraw"), () => Send($"/faction withdraw {amountText.Trim()}"), null));
            body.Add(bank);

            Section(string.Format(Localization.Extra("mpPanelMembers", "Members ({0})"), s.members.Count));
            foreach (var m in s.members)
            {
                string rank = m.rank == 2 ? Localization.Extra("mpRankLeaderShort", "leader") : m.rank == 1 ? Localization.Extra("mpRankOfficerShort", "officer") : "";
                var row = Line($"{(m.online ? "●" : "○")} {m.name}{(rank.Length > 0 ? $"  ({rank})" : "")}", m.online ? Good : Dim);
                bool self = m.name == SelfName(s);
                if (!self && leader)
                {
                    if (m.rank == 0) row.Add(Btn(Localization.Extra("mpPanelPromote", "Promote"), () => Send($"/faction promote {m.name}"), null));
                    if (m.rank == 1) row.Add(Btn(Localization.Extra("mpPanelDemote", "Demote"), () => Send($"/faction demote {m.name}"), null));
                    row.Add(Btn(Localization.Extra("mpPanelMakeLeader", "Make leader"), () => Send($"/faction leader {m.name}"), null));
                }
                if (!self && officer && (leader || m.rank == 0)) row.Add(Btn(Localization.Extra("mpPanelKick", "Kick"), () => Send($"/faction kick {m.name}"), "squad-button--leave"));
                body.Add(row);
            }
            if (officer)
            {
                var free = s.pilots.FindAll(p => !p.self && string.IsNullOrEmpty(p.tag));
                if (free.Count > 0)
                {
                    Section(Localization.Extra("mpPanelInvite", "Invite a pilot"));
                    foreach (var p in free)
                    {
                        var row = Line(p.name, Color.white);
                        row.Add(Btn(Localization.Extra("mpPanelInviteButton", "Invite"), () => Send($"/faction invite {p.name}"), "squad-button--accept"));
                        body.Add(row);
                    }
                }
            }

            Section(string.Format(Localization.Extra("mpPanelTerritory", "Territory ({0} / {1})"), s.claims.Count, s.maxClaims));
            foreach (var c in s.claims)
                body.Add(Line($"{c.name}{(c.home ? "  ⌂ " + Localization.Extra("mpPanelHome", "home") : "")}{(c.sieged ? "  ⚔ " + Localization.Extra("mpPanelSieged", "under siege") : "")}  ·  " +
                              string.Format(Localization.Extra("mpPanelLapses", "lapses in {0:0.#} days without a member docking"), c.daysLeft), c.sieged ? Bad : Color.white));
            if (s.dockedStation >= 0)
            {
                string here = StationName(s.dockedStation);
                bool mine = s.stationHolder == s.factionTag, held = s.stationHolder.Length > 0;
                var row = Line(string.Format(Localization.Extra("mpPanelDockedAt", "Docked at {0}: {1}"), here,
                    !held ? Localization.Extra("mpPanelFree", "free") : mine ? Localization.Extra("mpPanelOurs", "yours") : $"[{s.stationHolder}]"), Accent);
                if (officer)
                {
                    if (!held && s.stationClaimable && s.claims.Count < s.maxClaims)
                        row.Add(Btn(string.Format(Localization.Extra("mpPanelClaim", "Claim ({0:N0})"), s.claimCost), () => Send("/faction claim"), "squad-button--accept"));
                    if (mine && s.home != s.dockedStation) row.Add(Btn(Localization.Extra("mpPanelMakeHome", "Make home"), () => Send("/faction home"), null));
                    if (mine) row.Add(Btn(Localization.Extra("mpPanelUnclaim", "Give up"), () => Send("/faction unclaim"), "squad-button--leave"));
                    if (held && !mine) row.Add(Btn(string.Format(Localization.Extra("mpPanelSiege", "Siege ({0:N0})"), s.siegeCost), () => Send("/faction siege"), "squad-button--leave"));
                }
                body.Add(row);
            }
            if (s.sieges.Length > 0) body.Add(Text(s.sieges, 14, Dim));

            Section(Localization.Extra("mpPanelFactions", "Factions"));
            FactionList(s);

            var end = Row();
            end.style.marginTop = 16;
            if (leader && s.members.Count <= 1 || !leader)
                end.Add(Btn(confirmLeave ? Localization.Extra("mpPanelConfirmLeave", "Really leave?") : Localization.Extra("mpPanelLeave", "Leave the faction"),
                            () => { if (confirmLeave) Send("/faction leave"); else { confirmLeave = true; Rebuild(); } }, "squad-button--leave"));
            if (leader)
                end.Add(Btn(confirmLeave ? Localization.Extra("mpPanelConfirmDisband", "Really disband? The bank is lost") : Localization.Extra("mpPanelDisband", "Disband"),
                            () => { if (confirmLeave) Send("/faction disband"); else { confirmLeave = true; Rebuild(); } }, "squad-button--leave"));
            body.Add(end);
        }

        void FactionList(NetPanel.State s)
        {
            if (s.factions.Count == 0) { body.Add(Text(Localization.Extra("mpPanelNoFactions", "No factions yet."), 14, Dim)); return; }
            foreach (var c in s.factions)
                body.Add(Text($"[{c.tag}] {c.name}  ·  {string.Format(Localization.Extra("mpPanelFactionRow", "{0} members, {1} stations"), c.members, c.claims)}", 15, Color.white));
        }

        void BuildArena(NetPanel.State s)
        {
            if (s.inMatch) { body.Add(Text(Localization.Extra("mpPanelInMatch", "You are in a match."), 16, Accent)); return; }
            if (s.duelFrom.Length > 0)
            {
                var row = Line(string.Format(Localization.Extra("mpPanelChallenged", "{0} challenges you to a duel{1}."), s.duelFrom,
                                             s.duelVoids ? Localization.Extra("mpArenaWithVoids", " with the Void fighters") : ""), Good);
                row.Add(Btn(Localization.Extra("mpAccept", "Accept"), () => Send("/accept"), "squad-button--accept"));
                row.Add(Btn(Localization.Extra("mpDecline", "Decline"), () => Send("/decline"), null));
                body.Add(row);
            }
            var opt = new Toggle(Localization.Extra("mpPanelVoids", "With the Void fighters (they attack everyone)")) { value = voids };
            opt.RegisterValueChangedCallback(e => voids = e.newValue);
            opt.style.marginTop = 6;
            body.Add(opt);
            string suffix = voids ? " voids" : "";

            Section(Localization.Extra("mpPanelDuel", "Duel (1v1)"));
            body.Add(Text(string.Format(Localization.Extra("mpPanelDuelHint", "First to {0} kills or the most after {1} minutes. Both pilots must be docked."),
                                        NetArena.DuelKills, Mathf.RoundToInt(NetArena.DuelSeconds / 60f)), 14, Dim));
            foreach (var p in s.pilots)
            {
                if (p.self) continue;
                string state = p.inMatch ? Localization.Extra("mpPanelBusy", "in a match") : !p.docked ? Localization.Extra("mpPanelInSpace", "in space") : "";
                var row = Line((p.tag.Length > 0 ? $"[{p.tag}] " : "") + p.name + (state.Length > 0 ? $"  ({state})" : ""), state.Length > 0 ? Dim : Color.white);
                if (state.Length == 0 && s.dockedStation >= 0) row.Add(Btn(Localization.Extra("mpPanelChallenge", "Challenge"), () => Send($"/duel {p.name}{suffix}"), "squad-button--accept"));
                body.Add(row);
            }
            if (s.pilots.Count <= 1) body.Add(Text(Localization.Extra("mpPanelNobody", "Nobody else is online."), 14, Dim));

            Section(Localization.Extra("mpPanelFfa", "Free-for-all"));
            body.Add(Text(string.Format(Localization.Extra("mpPanelFfaHint", "First to {0} kills or the most after {1} minutes; up to {2} pilots. It starts 30 s after a second pilot joins."),
                                        NetArena.FfaKills, Mathf.RoundToInt(NetArena.FfaSeconds / 60f), NetArena.FfaMaxPlayers), 14, Dim));
            var ffa = Row();
            if (s.queue > 0)
            {
                ffa.Add(Text(string.Format(Localization.Extra("mpPanelQueued", "In the queue{0}: {1} / {2}"), s.queue == 2 ? Localization.Extra("mpArenaWithVoids", " with the Void fighters") : "",
                                           s.queueCount, NetArena.FfaMaxPlayers) + (s.queueStartsIn >= 0f ? "  ·  " + string.Format(Localization.Extra("mpPanelStartsIn", "starts in {0:0} s"), s.queueStartsIn) : ""), 16, Good));
                ffa.Add(Btn(Localization.Extra("mpPanelLeaveQueue", "Leave the queue"), () => Send("/leave"), "squad-button--leave"));
            }
            else if (s.dockedStation >= 0) ffa.Add(Btn(Localization.Extra("mpPanelJoinFfa", "Join"), () => Send("/ffa" + suffix), "squad-button--accept"));
            body.Add(ffa);

            Section(Localization.Extra("mpPanelMatches", "Matches"));
            if (s.matches.Count == 0) body.Add(Text(Localization.Extra("mpArenaNoMatchesShort", "None running."), 14, Dim));
            foreach (var m in s.matches) body.Add(Text(m, 15, Color.white));
            if (s.profiles && s.top.Length > 0)
            {
                Section(Localization.Extra("mpPanelTop", "Leaderboard"));
                body.Add(Text(s.top, 15, Color.white));
            }
        }

        void BuildProfile(NetPanel.State s)
        {
            if (!s.profiles) { body.Add(Text(Localization.Extra("mpPanelNoProfilesHere", "This server doesn't keep player profiles: nothing is saved."), 16, Dim)); return; }
            if (s.guest) body.Add(Text(Localization.Extra("mpPanelGuestLong", "You play as a guest: the server has no room for another profile, so nothing is saved. With a link code from another device you can use its profile."), 16, Bad));
            else
            {
                body.Add(Text(string.Format(Localization.Extra("mpPanelProfile", "Profile {0}  ·  {1} device(s)"), s.profileId, s.devices), 18, Color.white));
                body.Add(Text(s.controller ? Localization.Extra("mpPanelControls", "This device plays the profile. Progress is saved on docking, every minute and when leaving.")
                                           : Localization.Extra("mpPanelWatches", "Another device of yours plays the profile: this one only watches from the station."), 15, s.controller ? Dim : Bad));
                if (!s.controller) body.Add(Btn(Localization.Extra("mpPanelTakeControl", "Take control (the other device must be docked)"), () => Send("/control"), "squad-button--accept"));
                Section(Localization.Extra("mpPanelOtherDevice", "Use this profile on another device"));
                body.Add(Text(Localization.Extra("mpPanelLinkHint", "Get a code here, then join this server on the other device and enter it there (5 minutes)."), 14, Dim));
                if (s.controller) body.Add(Btn(Localization.Extra("mpPanelGetCode", "Get a link code"), () => Send("/link"), "squad-button--accept"));
            }
            Section(Localization.Extra("mpPanelLinkHere", "Use another device's profile here"));
            var row = Row();
            row.Add(Field(Localization.Extra("mpPanelCode", "Code"), linkText, 6, 140, v => linkText = v.ToUpperInvariant()));
            var f = new Toggle(Localization.Extra("mpPanelForce", "Replace this device's own progress")) { value = force };
            f.RegisterValueChangedCallback(e => force = e.newValue);
            row.Add(f);
            row.Add(Btn(Localization.Extra("mpPanelLink", "Link"), () => Send($"/link {linkText.Trim()}{(force ? " force" : "")}"), "squad-button--accept"));
            body.Add(row);
            if (!s.guest && s.role < NetModeration.Master)
            {
                Section(Localization.Extra("mpPanelClaimServer", "Claim this server"));
                body.Add(Text(Localization.Extra("mpPanelClaimHint", "For the server's owner: its admin token is in the server's log (and admin_token.txt beside its profiles)."), 14, Dim));
                var claim = Row();
                claim.Add(Field(Localization.Extra("mpPanelToken", "Admin token"), tokenText, 64, 260, v => tokenText = v));
                claim.Add(Btn(Localization.Extra("mpPanelClaimButton", "Claim"), () => { Send($"/claimadmin {tokenText.Trim()}"); tokenText = ""; }, "squad-button--accept"));
                body.Add(claim);
            }
        }

        /// <summary>Admins: the server's settings, each with its field (or switch) and Save; saved on the server and kept
        /// after a restart (the command line's own options win at a start: said beside them).</summary>
        void BuildSettings(NetPanel.State s)
        {
            if (s.settings.Count == 0) return;
            Section(Localization.Extra("mpPanelSettings", "Server settings"));
            foreach (var row in s.settings)
            {
                string key = row.key;
                var line = Row();
                line.style.marginBottom = 4;
                var label = Text(row.label + (row.note.Length > 0 ? $"  ({row.note})" : "") + (row.cli ? "  · " + Localization.Extra("mpPanelFromCli", "set by the launcher") : ""), 15, row.cli ? Dim : Color.white);
                label.style.width = 380;
                line.Add(label);
                if (row.kind == (int)NetServerSettings.Kind.Toggle)
                {
                    bool on = row.value == "on";
                    line.Add(Btn(on ? Localization.Extra("mpPanelOn", "On") : Localization.Extra("mpPanelOff", "Off"), () => Send($"/set {key} {(on ? "off" : "on")}"),
                                 on ? "squad-button--accept" : null));
                }
                else
                {
                    bool password = row.kind == (int)NetServerSettings.Kind.Password;
                    string shown = settingEdits.TryGetValue(key, out var edit) ? edit : password ? "" : row.value;
                    var field = Field(password ? (row.passwordSet ? Localization.Extra("mpPanelPasswordSet", "set (type a new one, - for none)") : Localization.Extra("mpPanelPasswordNone", "none"))
                                               : row.label, shown, password ? NetGame.MaxPasswordLength : 64, 220, v => settingEdits[key] = v);
                    if (password) field.isPasswordField = true;
                    line.Add(field);
                    line.Add(Btn(Localization.Extra("mpPanelSave", "Save"), () =>
                    {
                        if (!settingEdits.TryGetValue(key, out var v) || v.Trim().Length == 0) return;
                        settingEdits.Remove(key);
                        Send($"/set {key} {v.Trim()}");
                    }, "squad-button--accept"));
                }
                body.Add(line);
            }
        }

        /// <summary>A dangerous button: the first press arms it ("Really?"), the second acts.</summary>
        Button Confirm(string label, string key, string command)
        {
            bool armed = confirmKey == key;
            return Btn(armed ? Localization.Extra("mpPanelReally", "Really?") + " " + label : label, () =>
            {
                if (confirmKey == key) { confirmKey = ""; Send(command); }
                else { confirmKey = key; Rebuild(); }
            }, "squad-button--leave");
        }

        static string RoleLabel(int role) => role >= NetModeration.Master ? "  (master)" : role == NetModeration.Admin ? "  (admin)" : role == NetModeration.Op ? "  (op)" : "";

        void BuildAdmin(NetPanel.State s)
        {
            if (s.role < NetModeration.Op) return;
            bool admin = s.role >= NetModeration.Admin, master = s.role >= NetModeration.Master;
            body.Add(Text(master ? Localization.Extra("mpPanelYouMaster", "You are this server's master admin.") : admin ? Localization.Extra("mpPanelYouAdmin", "You are an admin of this server.")
                                                                                                              : Localization.Extra("mpPanelYouOp", "You are an op of this server."), 15, Accent));
            if (admin)
            {
                Section(Localization.Extra("mpPanelServer", "Server"));
                if (s.serverStatus.Length > 0) body.Add(Text(s.serverStatus, 14, Color.white));
                var say = Row();
                say.Add(Field(Localization.Extra("mpPanelAnnounce", "Announcement to everyone"), sayText, 200, 480, v => sayText = v));
                say.Add(Btn(Localization.Extra("mpPanelSend", "Send"), () => { Send($"/say {sayText.Trim()}"); sayText = ""; }, "squad-button--accept"));
                body.Add(say);
                BuildSettings(s);
            }
            if (!s.profiles)
            {
                BuildAdminFresh(s, master);
                return;
            }
            var opts = Row();
            opts.Add(Field(Localization.Extra("mpPanelReason", "Reason"), reasonText, 80, 320, v => reasonText = v));
            opts.Add(Field(Localization.Extra("mpPanelMinutes", "Minutes"), minutesText, 6, 110, v => minutesText = v));
            body.Add(opts);
            body.Add(Text(string.Format(Localization.Extra("mpPanelAdminHint", "Kick: the pilot can't come back for the minutes (0 = at once). Ban: for the minutes{0}."),
                                        admin ? Localization.Extra("mpPanelAdminHintAdmin", ", or for good") : string.Format(Localization.Extra("mpPanelAdminHintOp", " (ops: at most {0} hours)"), NetModeration.MaxOpBanMinutes / 60)), 14, Dim));

            Section(Localization.Extra("mpPanelPilotsOnline", "Pilots online"));
            string minutes = int.TryParse(minutesText.Trim(), out int m) && m >= 0 ? m.ToString() : NetModeration.KickMinutes.ToString();
            string reason = reasonText.Trim();
            foreach (var p in s.pilots)
            {
                string rank = RoleLabel(p.role);
                var row = Line((p.tag.Length > 0 ? $"[{p.tag}] " : "") + p.name + rank + (p.self ? "  (" + Localization.Extra("mpPanelYou", "you") + ")" : ""), p.self ? Dim : Color.white);
                if (!p.self && s.role > p.role)
                {
                    string target = "#" + p.client;
                    row.Add(Btn(Localization.Extra("mpPanelKick", "Kick"), () => Send($"/kick {target} {minutes} {reason}"), "squad-button--leave"));
                    row.Add(Btn(Localization.Extra("mpPanelTempBan", "Ban (minutes)"), () => Send($"/tempban {target} {Mathf.Max(1, int.Parse(minutes))} {reason}"), "squad-button--leave"));
                    if (admin)
                    {
                        row.Add(Confirm(Localization.Extra("mpPanelBanForGood", "Ban for good"), "ban" + target, $"/ban {target} {reason}"));
                        if (p.role == NetModeration.Player) row.Add(Btn(Localization.Extra("mpPanelOp", "Make op"), () => Send($"/op {target}"), "squad-button--accept"));
                        if (p.role == NetModeration.Op) row.Add(Btn(Localization.Extra("mpPanelDeop", "Remove op"), () => Send($"/deop {target}"), null));
                        if (master && p.role < NetModeration.Admin) row.Add(Btn(Localization.Extra("mpPanelMakeAdmin", "Make admin"), () => Send($"/admin {target}"), "squad-button--accept"));
                    }
                }
                body.Add(row);
            }

            Section(string.Format(Localization.Extra("mpPanelBans", "Bans ({0})"), s.bans.Count));
            if (s.bans.Count == 0) body.Add(Text(Localization.Extra("mpAdminNoBans", "Nobody is banned."), 14, Dim));
            foreach (var b in s.bans)
            {
                var row = Line($"{b.name}  ·  {b.left}  ·  {b.by}{(b.reason.Length > 0 ? ": " + b.reason : "")}", Color.white);
                string key = b.account.Length > 0 ? b.account : b.name;
                row.Add(Btn(Localization.Extra("mpPanelUnban", "Unban"), () => Send($"/unban {key}"), "squad-button--accept"));
                body.Add(row);
            }
            if (!admin) return;

            Section(Localization.Extra("mpPanelStaff", "Staff"));
            if (s.staff.Count == 0) body.Add(Text(Localization.Extra("mpPanelNoStaff", "Nobody yet."), 14, Dim));
            foreach (var st in s.staff)
            {
                var row = Line($"{(st.online ? "●" : "○")} {st.name}{RoleLabel(st.role)}", st.online ? Good : Dim);
                if (s.role > st.role)
                {
                    if (st.role == NetModeration.Op) row.Add(Btn(Localization.Extra("mpPanelDeop", "Remove op"), () => Send($"/deop {st.name}"), null));
                    if (st.role == NetModeration.Admin && master) row.Add(Btn(Localization.Extra("mpPanelUnadmin", "Remove admin"), () => Send($"/unadmin {st.name}"), null));
                }
                body.Add(row);
            }

            Section(string.Format(Localization.Extra("mpPanelProfiles", "Profiles ({0})"), s.profileRows.Count));
            var find = Row();
            find.Add(Field(Localization.Extra("mpPanelFilter", "Filter by name or id"), filterText, 24, 300, v => filterText = v));
            find.Add(Btn(Localization.Extra("mpPanelFilterButton", "Filter"), Rebuild, null));   // a rebuild per letter would lose the field's focus
            body.Add(find);
            string filter = filterText.Trim();
            int shown = 0;
            foreach (var pr in s.profileRows)
            {
                if (filter.Length > 0 && pr.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && pr.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++shown > 60) { body.Add(Text(Localization.Extra("mpPanelMore", "More: narrow the filter."), 14, Dim)); break; }
                var row = Line($"{(pr.online ? "●" : "○")} {(pr.name.Length > 0 ? pr.name : "?")}  ·  {pr.id}{RoleLabel(pr.role)}  ·  {pr.devices} dev.  ·  {pr.lastSeen}{(pr.banned ? "  ·  BANNED" : "")}",
                               pr.banned ? Bad : pr.online ? Good : Color.white);
                if (s.role > pr.role)
                {
                    string id = pr.id;
                    if (pr.banned) row.Add(Btn(Localization.Extra("mpPanelUnban", "Unban"), () => Send($"/unban {id}"), "squad-button--accept"));
                    else row.Add(Confirm(Localization.Extra("mpPanelBanForGood", "Ban for good"), "banp" + id, $"/ban {id} {reasonText.Trim()}"));
                    if (pr.role == NetModeration.Player) row.Add(Btn(Localization.Extra("mpPanelOp", "Make op"), () => Send($"/op {id}"), null));
                    if (pr.role == NetModeration.Op) row.Add(Btn(Localization.Extra("mpPanelDeop", "Remove op"), () => Send($"/deop {id}"), null));
                    if (master && pr.role < NetModeration.Admin) row.Add(Btn(Localization.Extra("mpPanelMakeAdmin", "Make admin"), () => Send($"/admin {id}"), null));
                    if (master && pr.role == NetModeration.Admin) row.Add(Btn(Localization.Extra("mpPanelUnadmin", "Remove admin"), () => Send($"/unadmin {id}"), null));
                    if (master && !pr.online) row.Add(Confirm(Localization.Extra("mpPanelDelete", "Delete"), "del" + id, $"/deleteprofile {id}"));
                }
                body.Add(row);
            }

            Section(Localization.Extra("mpPanelFactions", "Factions"));
            if (s.factions.Count == 0) body.Add(Text(Localization.Extra("mpPanelNoFactions", "No factions yet."), 14, Dim));
            foreach (var c in s.factions)
            {
                var row = Line($"[{c.tag}] {c.name}  ·  {string.Format(Localization.Extra("mpPanelFactionRow", "{0} members, {1} stations"), c.members, c.claims)}", Color.white);
                row.Add(Confirm(Localization.Extra("mpPanelDisband", "Disband"), "faction" + c.tag, $"/disband {c.tag}"));
                body.Add(row);
            }
        }

        // ---- small builders ---------------------------------------------------------------------------------

        // ---- the Squad tab --------------------------------------------------------------------------------------

        string squadKey = "";

        /// <summary>What the Squad tab shows, as a key: rebuilt only when it changes (a rebuilt button loses a press).</summary>
        static string SquadKey()
        {
            var sb = new System.Text.StringBuilder();
            var me = NetPlayer.Local;
            sb.Append(me != null ? $"{me.SquadId}|{me.Station}|{me.InHangar}|{me.InSpace}|{NetDistress.Active}" : "-");
            foreach (var m in NetSquad.Members()) sb.Append('|').Append(m.OwnerClientId).Append(m.DisplayName).Append(NetCommands.WhereText(m)).Append(m.Distress);
            foreach (var i in NetSquad.Invites) sb.Append("|i").Append(i.from);
            foreach (var p in NetPlayer.All)
                if (p != null && p.IsSpawned && !p.IsOwner && p.InHangar && me != null && p.Station == me.Station)
                    sb.Append("|p").Append(p.OwnerClientId).Append(p.DisplayName).Append(p.SquadId).Append(NetSquad.WasInvited(p));
            return sb.ToString();
        }

        void BuildSquad()
        {
            squadKey = SquadKey();
            var me = NetPlayer.Local;
            foreach (var inv in new List<NetSquad.Invite>(NetSquad.Invites))
            {
                var row = Line(string.Format(Localization.Extra("mpSquadInvited", "{0} invites you to their squad."), inv.name), Good);
                var invite = inv;
                row.Add(Btn(Localization.Extra("mpAccept", "Accept"), () => { NetSquad.Accept(invite); squadKey = ""; }, "squad-button--accept"));
                row.Add(Btn(Localization.Extra("mpDecline", "Decline"), () => { NetSquad.Decline(invite); squadKey = ""; }, null));
                body.Add(row);
            }
            if (NetSquad.Invites.Count > 0 && NetMissions.AbandonWarning() is string warn && warn.Length > 0) body.Add(Text(warn, 14, Bad));

            var members = NetSquad.Members();
            Section(Localization.Extra("mpSquadTitle", "Your squad"));
            if (members.Count == 0)
                body.Add(Text(Localization.Extra("mpSquadEmptyHint", "You aren't in a squad. Invite a pilot docked here (below), or accept an invitation. A squad shares its bar mission and rewards, its members can't hurt each other, and they can call each other for help."), 15, Dim));
            else
            {
                foreach (var m in members)
                {
                    var row = Line((m.Distress ? "⚠ " : "") + m.DisplayName + (m.IsOwner ? $"  ({Localization.Extra("mpYou", "you")})" : "") + "  ·  " + NetCommands.WhereText(m),
                                   m.Distress ? Bad : Color.white);
                    var caller = m;
                    if (!m.IsOwner && m.Distress)
                        row.Add(Btn(Localization.Extra("mpHelpButton", "Help"), () => { string msg = NetDistress.Help(caller); if (!string.IsNullOrEmpty(msg)) status.text = msg; squadKey = ""; }, "squad-button--accept"));
                    body.Add(row);
                }
                var leave = Row();
                // The distress call (NetDistress): in space only; docked the line says so.
                bool inSpace = me != null && me.InSpace;
                if (inSpace || NetDistress.Active)
                    leave.Add(Btn(NetDistress.Active ? Localization.Extra("mpDistressEnd", "End the call") : Localization.Extra("mpDistressCall", "Distress call"),
                                  () => { status.text = NetDistress.Toggle() ?? ""; squadKey = ""; }, NetDistress.Active ? null : "squad-button--leave"));
                leave.Add(Btn(Localization.Extra("mpLeaveSquad", "Leave squad"), () => { NetSquad.Leave(); squadKey = ""; }, "squad-button--leave"));
                body.Add(leave);
                if (!inSpace)
                    body.Add(Text(Localization.Extra("mpSquadDistressHint", "Distress calls are made in space: here in flight, the squad window on the right, the actions menu (E), or /sos in the chat. A squadmate's call shows here and in flight with a Help button."), 14, Dim));
            }

            Section(Localization.Extra("mpSquadPilotsHere", "Pilots docked here"));
            int shown = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned || p.IsOwner || me == null || !p.InHangar || !me.InHangar || p.Station != me.Station) continue;
                shown++;
                var row = Line(p.DisplayName, Color.white);
                var target = p;
                if (NetSquad.Same(p, me)) row.Add(Text(Localization.Extra("mpInYourSquad", "In your squad"), 14, Good));
                else if (NetSquad.WasInvited(p)) row.Add(Text(Localization.Extra("mpInvited", "Invited"), 14, Dim));
                else row.Add(Btn(Localization.Extra("mpInvite", "Invite"), () => { NetSquad.InviteTo(target); squadKey = ""; }, "squad-button--accept"));
                body.Add(row);
            }
            if (shown == 0)
                body.Add(Text(me != null && me.InHangar ? Localization.Extra("mpSquadNobodyHere", "No other pilot is docked here. Squads form in a hangar: meet at a station.")
                                                       : Localization.Extra("mpSquadDockFirst", "Squads form while docked: dock at the same station as the other pilot."), 15, Dim));
        }

        // ---- the Trade tab --------------------------------------------------------------------------------------

        string tradeKey = "";

        /// <summary>What the Trade tab shows: the trade, the requests, the hold and the wallet, the pilots docked here, and
        /// whether Accept waits out the other side's last change.</summary>
        static string TradeKey()
        {
            var sb = new System.Text.StringBuilder();
            var me = NetPlayer.Local;
            sb.Append(NetTradeClient.Revision).Append('|').Append(Session.Credits).Append('|').Append(Shop.CargoLoad());
            sb.Append(me != null ? $"|{me.Station}|{me.InHangar}" : "|-");
            foreach (var c in Session.Cargo) sb.Append('|').Append(c.item).Append(':').Append(c.amount);
            foreach (var r in NetTradeClient.Requests) sb.Append("|r").Append(r.from);
            var v = NetTradeClient.Current;
            if (v != null) sb.Append("|h").Append(Time.unscaledTime - v.theirChangedAt < NetTradeClient.ChangedHoldSeconds);
            else if (me != null)
                foreach (var p in NetPlayer.All)
                    if (p != null && p.IsSpawned && !p.IsOwner && p.InHangar && p.Station == me.Station)
                        sb.Append("|p").Append(p.OwnerClientId).Append(p.DisplayName).Append(NetTradeClient.WasAsked(p));
            return sb.ToString();
        }

        void BuildTrade()
        {
            tradeKey = TradeKey();
            var me = NetPlayer.Local;
            bool docked = me != null && me.InHangar;
            foreach (var r in new List<NetTradeClient.Request>(NetTradeClient.Requests))
            {
                var row = Line(string.Format(Localization.Extra("mpTradeRequested", "{0} wants to trade with you."), r.name), Good);
                var request = r;
                if (docked) row.Add(Btn(Localization.Extra("mpAccept", "Accept"), () => { NetTradeClient.Accept(request); tradeKey = ""; }, "squad-button--accept"));
                row.Add(Btn(Localization.Extra("mpDecline", "Decline"), () => { NetTradeClient.Decline(request); tradeKey = ""; }, null));
                body.Add(row);
            }
            var v = NetTradeClient.Current;
            if (v == null) { BuildTradePilots(me, docked); return; }

            var title = Text(string.Format(Localization.Extra("mpTradeWith", "Trading with {0}"), v.partnerName), 22, Accent);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(title);
            body.Add(Text(Localization.Extra("mpTradeHint", "Put credits and goods from your hold into your offer. Any change takes back both accepts; the trade happens once you both accept the same offers."), 14, Dim));

            bool open = v.phase == NetTrade.Phase.Open;
            var cols = Row();
            cols.style.alignItems = Align.FlexStart;
            cols.style.flexWrap = Wrap.NoWrap;
            var left = Column(cols);
            var right = Column(cols);
            body.Add(cols);

            // This player's offer: the credits (a field) and the goods (-1 / remove).
            var draft = NetTradeClient.Draft;
            left.Add(Heading(Localization.Extra("mpTradeYourOffer", "Your offer") + (v.myAccepted ? "  ✓" : "")));
            var credits = Row();
            credits.Add(Text(Localization.Extra("mpTradeCredits", "Credits"), 16, Color.white));
            var field = Field(Localization.Extra("mpPanelAmount", "Amount"), draft.credits > 0 ? draft.credits.ToString() : "", 10, 150, value =>
            {
                long n = 0;
                foreach (char c in value) if (c >= '0' && c <= '9' && n < int.MaxValue) n = n * 10 + (c - '0');
                NetTradeClient.SetCredits((int)Math.Min(int.MaxValue, n));
            }, TouchScreenKeyboardType.NumberPad);
            field.SetEnabled(open);
            credits.Add(field);
            credits.Add(Text(string.Format(Localization.Extra("mpTradeOfCredits", "of {0:N0}"), Session.Credits), 14, Dim));
            left.Add(credits);
            if (draft.items.Count == 0) left.Add(Text(Localization.Extra("mpTradeNoGoods", "No goods."), 14, Dim));
            foreach (var s in draft.items)
            {
                var row = Line($"{s.amount} t {GameNames.Item(s.item)}", Color.white);
                int item = s.item, amount = s.amount;
                if (open)
                {
                    row.Add(Btn("−1", () => NetTradeClient.AddItem(item, -1), null));
                    row.Add(Btn("×", () => NetTradeClient.AddItem(item, -amount), "squad-button--leave"));
                }
                left.Add(row);
            }

            // The other side's offer, as the server last sent it.
            right.Add(Heading(string.Format(Localization.Extra("mpTradeTheirOffer", "{0}'s offer"), v.partnerName) + (v.theirAccepted ? "  ✓" : "")));
            right.Add(Text(v.theirs.credits > 0 ? $"{v.theirs.credits:N0} " + Localization.Extra("mpCredits", "credits")
                                                : Localization.Extra("mpTradeNoCredits", "No credits."), 16, v.theirs.credits > 0 ? Color.white : Dim));
            if (v.theirs.items.Count == 0) right.Add(Text(Localization.Extra("mpTradeNoGoods", "No goods."), 14, Dim));
            foreach (var s in v.theirs.items) right.Add(Text($"{s.amount} t {GameNames.Item(s.item)}", 16, Color.white));

            // Where it stands, and the buttons.
            bool held = Time.unscaledTime - v.theirChangedAt < NetTradeClient.ChangedHoldSeconds;
            string state; Color colour = Dim;
            if (!open) { state = Localization.Extra("mpTradeExchanging", "Exchanging..."); colour = Accent; }
            else if (held) { state = string.Format(Localization.Extra("mpTradeTheyChanged", "{0} changed their offer."), v.partnerName); colour = Bad; }
            else if (NetTradeClient.Sending) state = Localization.Extra("mpTradeSending", "Sending your offer...");
            else if (v.myAccepted) state = string.Format(Localization.Extra("mpTradeWaiting", "You accepted. Waiting for {0}."), v.partnerName);
            else if (v.theirAccepted) { state = string.Format(Localization.Extra("mpTradeTheyAccepted", "{0} accepted this trade."), v.partnerName); colour = Good; }
            else state = Localization.Extra("mpTradeNobodyAccepted", "Nobody has accepted yet.");
            var stateLine = Text(state, 16, colour);
            stateLine.style.marginTop = 12;
            body.Add(stateLine);
            // What comes in may not fit the hold (the shop doesn't check either: launching does).
            int incoming = 0, outgoing = 0;
            foreach (var s in v.theirs.items) incoming += s.amount;
            foreach (var s in draft.items) outgoing += s.amount;
            int over = Shop.CargoLoad() - outgoing + incoming - Shop.MaxLoad(NetGame.Db);
            if (incoming > 0 && over > 0)
                body.Add(Text(string.Format(Localization.Extra("mpTradeOverload", "This puts {0} t more in your hold than it carries: sell or store some before you launch."), over), 14, Bad));
            var buttons = Row();
            if (open)
            {
                bool empty = draft.Empty && v.theirs.Empty;
                var accept = Btn(Localization.Extra("mpTradeAccept", "Accept trade"), () => { NetTradeClient.Confirm(); tradeKey = ""; }, "squad-button--accept");
                accept.SetEnabled(!v.myAccepted && !held && !NetTradeClient.Sending && !empty);
                buttons.Add(accept);
                buttons.Add(Btn(Localization.Extra("mpTradeCancel", "Cancel trade"), () => { NetTradeClient.Cancel(); tradeKey = ""; }, "squad-button--leave"));
            }
            buttons.style.marginTop = 6;
            body.Add(buttons);

            // The hold: what can go into the offer.
            if (!open) return;
            Section(Localization.Extra("mpTradeYourCargo", "Your cargo"));
            int shown = 0;
            var seen = new HashSet<int>();
            foreach (var c in Session.Cargo)
            {
                if (c.amount <= 0 || !seen.Add(c.item) || !NetTradeClient.Tradeable(c.item)) continue;
                int have = Shop.CargoOf(c.item), offered = NetTradeClient.OfferedOf(c.item), item = c.item;
                shown++;
                var row = Line($"{GameNames.Item(item)}  ·  {have} t" + (offered > 0 ? "  " + string.Format(Localization.Extra("mpTradeOffered", "({0} offered)"), offered) : ""), Color.white);
                if (offered < have)
                {
                    row.Add(Btn("+1", () => NetTradeClient.AddItem(item, 1), null));
                    if (have - offered > 1) row.Add(Btn("+10", () => NetTradeClient.AddItem(item, 10), null));
                    row.Add(Btn(Localization.Extra("mpTradeAll", "All"), () => NetTradeClient.AddItem(item, have), "squad-button--accept"));
                }
                body.Add(row);
            }
            if (shown == 0) body.Add(Text(Localization.Extra("mpTradeEmptyHold", "Nothing in your hold can be traded. Demount equipment in the hangar to trade it."), 15, Dim));
        }

        /// <summary>No trade open: the pilots docked here to trade with.</summary>
        void BuildTradePilots(NetPlayer me, bool docked)
        {
            Section(Localization.Extra("mpTradePilots", "Trade with a pilot docked here"));
            if (!docked)
            {
                body.Add(Text(Localization.Extra("mpTradeDockFirst", "Trades happen in a hangar: dock at the same station as the other pilot."), 15, Dim));
                return;
            }
            int shown = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned || p.IsOwner || !p.InHangar || p.Station != me.Station) continue;
                shown++;
                var row = Line(p.DisplayName, Color.white);
                var target = p;
                if (NetTradeClient.WasAsked(p)) row.Add(Text(Localization.Extra("mpTradeAsked", "Asked"), 14, Dim));
                else row.Add(Btn(Localization.Extra("mpTradeButton", "Trade"), () => { NetTradeClient.Ask(target); tradeKey = ""; }, "squad-button--accept"));
                body.Add(row);
            }
            if (shown == 0) body.Add(Text(Localization.Extra("mpTradeNobodyHere", "No other pilot is docked here. Trades happen in a hangar: meet at a station."), 15, Dim));
            body.Add(Text(Localization.Extra("mpTradeWhat", "You trade credits and the goods in your hold (equipment too, once demounted). Ships and story items can't be traded."), 14, Dim));
        }

        /// <summary>A column of the Trade tab's two (the offers side by side).</summary>
        static VisualElement Column(VisualElement row)
        {
            var c = new VisualElement();
            c.style.flexGrow = 1;
            c.style.flexBasis = 0;
            c.style.marginRight = 16;
            c.style.minWidth = 0;
            row.Add(c);
            return c;
        }

        // ---- the Chat tab ---------------------------------------------------------------------------------------

        bool unreadChat;
        const int ChatKeep = 200;

        /// <summary>The chat pane: the lines (the ones NetChat still has, then each new one), the channel, the line, Send.</summary>
        void BuildChatPane()
        {
            chatPane = new VisualElement();
            chatPane.style.flexGrow = 1;
            chatPane.style.marginTop = 10;
            chatPane.style.display = DisplayStyle.None;
            chatScroll = new ScrollView(ScrollViewMode.Vertical);
            chatScroll.AddManipulator(new DragScroll(chatScroll));
            chatScroll.style.flexGrow = 1;
            chatScroll.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            chatScroll.style.paddingLeft = chatScroll.style.paddingRight = 10;
            chatScroll.style.paddingTop = chatScroll.style.paddingBottom = 6;
            chatPane.Add(chatScroll);
            foreach (var m in NetChat.Messages) AddChatLine(m);

            var hint = Text(Localization.Extra("mpChatWindowHint", "Enter sends · a line starting with / is a command (/help lists them) · /w <pilot> <text> whispers"), 13, Dim);
            hint.style.marginTop = 6;
            chatPane.Add(hint);

            var row = Row();
            row.style.flexWrap = Wrap.NoWrap;
            row.style.marginTop = 6;
            chatChannel = Btn("", ToggleChannel, null);
            chatChannel.style.marginLeft = 0;
            chatChannel.style.minWidth = 100;
            row.Add(chatChannel);
            chatField = new TextField { maxLength = NetChat.MaxCommandLength };
            chatField.style.flexGrow = 1;
            chatField.style.flexShrink = 1;
            chatField.style.marginLeft = 6;
            chatField.textEdition.placeholder = Localization.Extra("mpChatWindowPlaceholder", "Type a message");
            // Enter sends (the TextField would take it as its own submit and drop the focus); Tab switches the channel
            // unless a command is being typed.
            chatField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n')
                {
                    if (e.keyCode != KeyCode.None) SendChat();
                    e.StopPropagation();
                    chatField.focusController?.IgnoreEvent(e);
                }
                else if (e.keyCode == KeyCode.Tab || e.character == '\t')
                {
                    if (e.keyCode == KeyCode.Tab && !chatField.value.StartsWith("/")) ToggleChannel();
                    e.StopPropagation();
                    chatField.focusController?.IgnoreEvent(e);
                }
            }, TrickleDown.TrickleDown);
            chatField.RegisterCallback<NavigationSubmitEvent>(e => { e.StopPropagation(); chatField.focusController?.IgnoreEvent(e); }, TrickleDown.TrickleDown);
            chatField.RegisterCallback<NavigationMoveEvent>(e => { e.StopPropagation(); chatField.focusController?.IgnoreEvent(e); }, TrickleDown.TrickleDown);
            TextFieldKeys.Guard(chatField);   // Esc drops the focus (the next Esc closes the window)
            row.Add(chatField);
            row.Add(Btn(Localization.Extra("mpChatSend", "Send"), SendChat, "squad-button--accept"));
            chatPane.Add(row);
            RefreshChannel();
        }

        void ShowPane()
        {
            bool chat = tab == Tab.Chat;
            if (scroll != null) scroll.style.display = chat ? DisplayStyle.None : DisplayStyle.Flex;
            if (status != null) status.style.display = chat ? DisplayStyle.None : DisplayStyle.Flex;
            if (chatPane == null) return;
            chatPane.style.display = chat ? DisplayStyle.Flex : DisplayStyle.None;
            if (!chat) { if (chatField?.focusController?.focusedElement == chatField) chatField.Blur(); return; }
            unreadChat = false;
            ScrollChatDown();
            chatField.schedule.Execute(() => { if (isOpen && tab == Tab.Chat) chatField.Focus(); }).ExecuteLater(50);
        }

        void AddChatLine(NetChat.Message m)
        {
            if (chatScroll == null || m == null) return;
            var l = new Label(ChatView.Format(m)) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 16;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 2;
            l.style.color = m.channel == NetChat.Channel.Notice ? Dim : m.own ? new Color(1f, 1f, 1f, 0.8f) : Color.white;
            // Follow the new line only while reading the end (scrolled up to read older lines, the view stays put).
            bool atEnd = chatScroll.scrollOffset.y >= ChatMaxOffset - 24f;
            chatScroll.contentContainer.Add(l);
            while (chatScroll.contentContainer.childCount > ChatKeep) chatScroll.contentContainer.RemoveAt(0);
            if (atEnd) ScrollChatDown();
        }

        float ChatMaxOffset => chatScroll == null ? 0f
            : Mathf.Max(0f, chatScroll.contentContainer.layout.height - chatScroll.contentViewport.layout.height);

        void ScrollChatDown() =>
            chatScroll?.schedule.Execute(() => chatScroll.scrollOffset = new Vector2(0f, ChatMaxOffset)).ExecuteLater(1);

        void SendChat()
        {
            string line = chatField.value;
            chatField.value = "";
            if (!string.IsNullOrWhiteSpace(line)) NetChat.Send(line);
            chatField.schedule.Execute(() => chatField.Focus()).ExecuteLater(1);   // keep typing
        }

        void ToggleChannel()
        {
            NetChat.Sending = NetChat.Sending == NetChat.Channel.Global ? NetChat.Channel.Local : NetChat.Channel.Global;
            RefreshChannel();
        }

        void RefreshChannel()
        {
            if (chatChannel == null) return;
            bool global = NetChat.Sending == NetChat.Channel.Global;
            chatChannel.text = (global ? Localization.Extra("mpChatGlobal", "Global") : Localization.Extra("mpChatLocal", "Local")).ToUpperInvariant();
            chatChannel.style.color = global ? new Color(0.94f, 0.7f, 0.35f) : Accent;
        }

        /// <summary>The Admin tab of a session without profiles (hosted fresh from the menu): kick, mute and session admins
        /// (NetCommands); bans, ops and the profile list need profiles, which the tab says.</summary>
        void BuildAdminFresh(NetPanel.State s, bool master)
        {
            body.Add(Text(Localization.Extra("mpPanelFreshAdmin", "A fresh world keeps nothing: bans, ops, factions and the profile list need World: Persistent on the Host card (or a dedicated server). Kicks, mutes and session admins work here."), 14, Dim));
            var opts = Row();
            opts.Add(Field(Localization.Extra("mpPanelReason", "Reason"), reasonText, 80, 320, v => reasonText = v));
            opts.Add(Field(Localization.Extra("mpPanelMinutes", "Minutes"), minutesText, 6, 110, v => minutesText = v));
            body.Add(opts);
            body.Add(Text(Localization.Extra("mpPanelFreshHint", "Mute: for the minutes (0 = the whole session)."), 14, Dim));
            Section(Localization.Extra("mpPanelPilotsOnline", "Pilots online"));
            foreach (var p in s.pilots)
            {
                var row = Line((p.tag.Length > 0 ? $"[{p.tag}] " : "") + p.name + RoleLabel(p.role) + (p.self ? "  (" + Localization.Extra("mpPanelYou", "you") + ")" : ""), p.self ? Dim : Color.white);
                if (!p.self && s.role > p.role)
                {
                    string target = "#" + p.client;
                    row.Add(Btn(Localization.Extra("mpPanelKick", "Kick"), () => Send($"/kick {target} {reasonText.Trim()}"), "squad-button--leave"));
                    row.Add(Btn(Localization.Extra("mpPanelMute", "Mute"), () =>
                    {
                        int m = int.TryParse(minutesText.Trim(), out int v) && v > 0 ? v : 0;
                        Send(m > 0 ? $"/mute {target} {m}" : $"/mute {target}");
                    }, null));
                    row.Add(Btn(Localization.Extra("mpPanelUnmute", "Unmute"), () => Send($"/unmute {target}"), null));
                    if (master && p.role < NetModeration.Admin) row.Add(Btn(Localization.Extra("mpPanelMakeAdmin", "Make admin"), () => Send($"/admin {target}"), "squad-button--accept"));
                    if (master && p.role == NetModeration.Admin) row.Add(Btn(Localization.Extra("mpPanelUnadmin", "Remove admin"), () => Send($"/unadmin {target}"), null));
                }
                body.Add(row);
            }
        }

        static string SelfName(NetPanel.State s) => s.pilots.Find(p => p.self)?.name ?? "";

        static string StationName(int station) => NetGame.Db.Stations.Find(x => x.index == station)?.name ?? station.ToString();

        void Section(string title) => body.Add(Heading(title));

        /// <summary>A section's title (Section adds it to the tab; the Trade tab's columns take their own).</summary>
        static Label Heading(string title)
        {
            var l = Text(title.ToUpperInvariant(), 15, Accent);
            l.style.marginTop = 16;
            l.style.marginBottom = 4;
            l.style.letterSpacing = 1;
            l.style.borderBottomWidth = 1;
            l.style.borderBottomColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
            return l;
        }

        static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.flexWrap = Wrap.Wrap;
            return r;
        }

        /// <summary>A line of text with room for buttons after it.</summary>
        static VisualElement Line(string text, Color colour)
        {
            var r = Row();
            r.style.marginBottom = 4;
            var l = Text(text, 16, colour);
            l.style.flexGrow = 1;
            l.style.flexShrink = 1;
            r.Add(l);
            return r;
        }

        static Label Text(string text, int size, Color colour)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        static Button Btn(string text, Action onClick, string cls)
        {
            var b = new Button(onClick) { text = text.ToUpperInvariant() };
            b.AddToClassList("squad-button");
            if (cls != null) b.AddToClassList(cls);
            b.style.marginLeft = 6;
            b.style.marginTop = 2; b.style.marginBottom = 2;
            return b;
        }

        static TextField Field(string label, string value, int max, int width, Action<string> changed,
                                TouchScreenKeyboardType keyboard = TouchScreenKeyboardType.Default)
        {
            var f = new TextField { value = value, maxLength = max, keyboardType = keyboard };
            f.textEdition.placeholder = label;
            TextFieldKeys.Guard(f);   // typed keys stay in the field (no menu navigation, no game keys)
            f.style.width = width;
            f.style.marginLeft = 6;
            f.RegisterValueChangedCallback(e => changed(e.newValue));
            return f;
        }
    }
}
