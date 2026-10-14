// SquadView.cs
// Multiplayer squad UI (NetSquad) in the flight HUD and the station menu, while a session runs:
//   the squad window (right side, only while in a squad; the header collapses it): every member with where they are
//     (an orbit or docked, by station name), a shield bar and a hull bar with the armor over it (like the HUD's; a pool the
//     ship lacks: not shown), and Leave; a member calling for help shows a red "⚠ HELP" and a Help button (NetDistress:
//     the Khador Drive or the autopilot to them); the local player's own row has Distress call / End the call in space;
//   in the station, the pilot list (collapsible too, only with other players docked here): each with Invite (or "In your
//     squad" / "Invited") and Trade (NetTradeClient; or "Asked" / "Trading", which opens the Multiplayer window's Trade tab);
//   an invitation popup (only while docked: squads form in a hangar): "<name> invites you to their squad", and when this
//     player has a mission that accepting abandons it (NetMissions.AbandonWarning), with Accept / Decline (45 s);
//   a trade request popup (docked, while no squad invitation shows): "<name> wants to trade with you", Accept (the
//     Multiplayer window's Trade tab opens) / Decline;
//   in the station, an online session's join code with Copy, on its own plate under the system information.
// The rows are rebuilt only when their content changes (a rebuilt button would lose a press); the bars update live.
// Styles: Resources/GoF2Net/Squad.uss.

using System.Collections.Generic;
using System.Text;
using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class SquadView : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;

        static bool squadCollapsed, pilotsCollapsed;
        VisualElement codePlate;
        Label codeLabel;
        Button copyButton;

        VisualElement box, squadPanel, squadBody, pilotsPanel, pilotsBody, invitePopup, tradePopup;
        Button squadHeader, pilotsHeader;
        Label inviteText, tradeText;
        bool hangar;
        int hereStation = -1;
        string squadKey = "", pilotsKey = "";
        NetSquad.Invite shownInvite;
        NetTradeClient.Request shownTrade;
        readonly Dictionary<ulong, (VisualElement shield, VisualElement armor, VisualElement hull)> bars = new Dictionary<ulong, (VisualElement, VisualElement, VisualElement)>();
        float refresh;
        Database db;

        /// <summary>The squad UI on 'parent' (again after a UI reload); 'hangarStation' &gt;= 0 in the station: its pilot list.</summary>
        public static void Attach(GameObject host, VisualElement parent, int hangarStation = -1)
        {
            if (parent == null) return;
            var view = host.GetComponent<SquadView>();
            if (view == null) view = host.AddComponent<SquadView>();
            view.Build(parent, hangarStation);
        }

        void Build(VisualElement parent, int hangarStation)
        {
            db = Database.Load();
            hangar = hangarStation >= 0;
            hereStation = hangarStation;
            box?.RemoveFromHierarchy();
            invitePopup?.RemoveFromHierarchy();
            tradePopup?.RemoveFromHierarchy();
            squadKey = pilotsKey = "";
            shownInvite = null;
            shownTrade = null;
            var sheet = Resources.Load<StyleSheet>("GoF2Net/Squad");

            box = new VisualElement { name = "squad", pickingMode = PickingMode.Ignore };
            box.AddToClassList("squad");
            if (sheet != null) box.styleSheets.Add(sheet);
            squadPanel = Panel(out squadHeader, out squadBody, () => { squadCollapsed = !squadCollapsed; squadKey = ""; });
            box.Add(squadPanel);
            pilotsPanel = Panel(out pilotsHeader, out pilotsBody, () => { pilotsCollapsed = !pilotsCollapsed; pilotsKey = ""; });
            box.Add(pilotsPanel);
            // Under everything else on that layer (the hangar, missions and status windows, the HUD): the first child.
            parent.Insert(0, box);

            invitePopup = new VisualElement();
            invitePopup.AddToClassList("squad-invite");
            if (sheet != null) invitePopup.styleSheets.Add(sheet);
            inviteText = new Label();
            inviteText.AddToClassList("squad-invite-text");
            invitePopup.Add(inviteText);
            var row = new VisualElement();
            row.AddToClassList("squad-invite-buttons");
            row.Add(MakeButton(Localization.Extra("mpAccept", "Accept"), () => { if (shownInvite != null) NetSquad.Accept(shownInvite); shownInvite = null; }, "squad-button--accept"));
            row.Add(MakeButton(Localization.Extra("mpDecline", "Decline"), () => { if (shownInvite != null) NetSquad.Decline(shownInvite); shownInvite = null; }, null));
            invitePopup.Add(row);
            parent.Add(invitePopup);

            // A trade request (NetTradeClient): the same popup's look.
            tradePopup = new VisualElement();
            tradePopup.AddToClassList("squad-invite");
            if (sheet != null) tradePopup.styleSheets.Add(sheet);
            tradeText = new Label();
            tradeText.AddToClassList("squad-invite-text");
            tradePopup.Add(tradeText);
            var tradeRow = new VisualElement();
            tradeRow.AddToClassList("squad-invite-buttons");
            tradeRow.Add(MakeButton(Localization.Extra("mpAccept", "Accept"), () =>
            {
                if (shownTrade != null) { NetTradeClient.Accept(shownTrade); MultiplayerWindow.OpenTradeAny(); }
                shownTrade = null;
            }, "squad-button--accept"));
            tradeRow.Add(MakeButton(Localization.Extra("mpDecline", "Decline"), () => { if (shownTrade != null) NetTradeClient.Decline(shownTrade); shownTrade = null; }, null));
            tradePopup.Add(tradeRow);
            parent.Add(tradePopup);
            BuildCodePlate(parent, sheet);
            Refresh();
        }

        /// <summary>The station: an online session's join code (for asking friends in; a tap on Copy copies it) on its own
        /// plate under the system information.</summary>
        void BuildCodePlate(VisualElement parent, StyleSheet sheet)
        {
            codePlate?.RemoveFromHierarchy();
            codePlate = null;
            var info = hangar ? parent.Q(className: "station-info") : null;
            if (info == null || info.parent == null) return;
            codePlate = new VisualElement { name = "joinCodePlate" };
            codePlate.AddToClassList("station-info");
            codePlate.AddToClassList("squad-code");
            if (sheet != null) codePlate.styleSheets.Add(sheet);
            var title = new Label(Localization.Extra("mpJoinCode", "Join code").ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("info-line");
            title.AddToClassList("squad-code-title");
            codePlate.Add(title);
            var row = new VisualElement();
            row.AddToClassList("squad-code-row");
            codeLabel = new Label { pickingMode = PickingMode.Ignore };
            codeLabel.AddToClassList("info-system");
            codeLabel.AddToClassList("gof-semibold");
            codeLabel.AddToClassList("squad-code-value");
            row.Add(codeLabel);
            copyButton = MakeButton(Localization.Extra("mpCopy", "copy"), () =>
            {
                if (NetGame.JoinCode == null) return;
                GUIUtility.systemCopyBuffer = NetGame.JoinCode;
                copyButton.text = Localization.Extra("mpCopied", "copied").ToUpperInvariant();
            }, null);
            row.Add(copyButton);
            codePlate.Add(row);
            info.parent.Insert(info.parent.IndexOf(info) + 1, codePlate);
        }

        void RefreshCodePlate()
        {
            if (codePlate == null) return;
            string code = NetGame.Active ? NetGame.JoinCode : null;
            codePlate.style.display = code != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (code != null && codeLabel.text != code)
            {
                codeLabel.text = code;
                copyButton.text = Localization.Extra("mpCopy", "copy").ToUpperInvariant();
            }
        }

        static VisualElement Panel(out Button header, out VisualElement body, System.Action toggle)
        {
            var panel = new VisualElement();
            panel.AddToClassList("squad-panel");
            header = new Button(toggle) { focusable = false };   // clicks / taps only: Space or a controller's A in flight never press it
            header.AddToClassList("squad-header");
            panel.Add(header);
            body = new VisualElement();
            body.AddToClassList("squad-body");
            panel.Add(body);
            return panel;
        }

        static Button MakeButton(string text, System.Action onClick, string cls)
        {
            var b = new Button(onClick) { text = text.ToUpperInvariant(), focusable = false };   // the distress call too: never by a stray key
            b.AddToClassList("squad-button");
            if (cls != null) b.AddToClassList(cls);
            return b;
        }

        void Update()
        {
            if (box == null) return;
            bool session = NetGame.Active;
            box.style.display = session ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshCodePlate();
            if (!session) { invitePopup.style.display = tradePopup.style.display = DisplayStyle.None; return; }
            if ((refresh -= Time.unscaledDeltaTime) > 0f) return;
            refresh = RefreshSeconds;
            Refresh();
        }

        void Refresh()
        {
            RefreshSquad();
            RefreshPilots();
            RefreshInvite();
            RefreshTrade();
        }

        string Where(NetPlayer p)
        {
            string station = db?.Stations.Find(s => s.index == p.Station)?.name ?? "?";
            return p.InHangar ? string.Format(Localization.Extra("mpDocked", "Docked at {0}"), station)
                 : p.InSpace ? string.Format(Localization.Extra("mpInOrbit", "{0} orbit"), station)
                 : Localization.Extra("mpTravelling", "Travelling");
        }

        void RefreshSquad()
        {
            var members = NetSquad.Members();
            squadPanel.style.display = members.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (members.Count <= 1) { squadKey = ""; return; }
            var sb = new StringBuilder(squadCollapsed ? "c" : "o");
            foreach (var m in members) sb.Append('|').Append(m.OwnerClientId).Append(m.DisplayName).Append(Where(m)).Append(m.Distress);
            sb.Append('|').Append(NetDistress.Active).Append(NetPlayer.Local != null && NetPlayer.Local.InSpace);
            string key = sb.ToString();
            if (key != squadKey)
            {
                squadKey = key;
                squadHeader.text = $"{Localization.Extra("mpSquad", "Squad").ToUpperInvariant()} ({members.Count})  {(squadCollapsed ? "+" : "-")}";
                squadBody.Clear();
                bars.Clear();
                squadBody.style.display = squadCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
                foreach (var m in members)
                {
                    var row = new VisualElement();
                    row.AddToClassList("squad-member");
                    var name = new Label((m.Distress ? "⚠ " : "") + m.DisplayName + (m.IsOwner ? $"  ({Localization.Extra("mpYou", "you")})" : ""));
                    name.AddToClassList("squad-name");
                    if (m.Distress) name.style.color = new Color(1f, 0.4f, 0.35f);
                    row.Add(name);
                    // A call for help: the others' Help (the fastest way there), the local player's own call / end.
                    var caller = m;
                    if (!m.IsOwner && m.Distress)
                        row.Add(MakeButton(Localization.Extra("mpHelpButton", "Help"), () => { string msg = NetDistress.Help(caller); if (!string.IsNullOrEmpty(msg)) NetChat.Notice(msg); squadKey = ""; }, "squad-button--accept"));
                    if (m.IsOwner && (m.InSpace || NetDistress.Active))
                        row.Add(MakeButton(NetDistress.Active ? Localization.Extra("mpDistressEnd", "End the call") : Localization.Extra("mpDistressCall", "Distress call"),
                                           () => { NetChat.Notice(NetDistress.Toggle()); squadKey = ""; }, NetDistress.Active ? null : "squad-button--leave"));
                    var where = new Label(Where(m));
                    where.AddToClassList("squad-where");
                    row.Add(where);
                    var shieldFill = Bar(row, "squad-bar--shield");
                    var hullFill = Bar(row, "squad-bar--hull");
                    var armorFill = new VisualElement();   // CombatView: the armor fill over the hull in one bar
                    armorFill.AddToClassList("squad-bar-fill");
                    armorFill.AddToClassList("squad-bar-armor");
                    hullFill.parent.Add(armorFill);
                    bars[m.OwnerClientId] = (shieldFill, armorFill, hullFill);
                    squadBody.Add(row);
                }
                squadBody.Add(MakeButton(Localization.Extra("mpLeaveSquad", "Leave squad"), NetSquad.Leave, "squad-button--leave"));
            }
            foreach (var m in members)
                if (bars.TryGetValue(m.OwnerClientId, out var b))
                {
                    SetBar(b.shield, m.Shield);
                    SetBar(b.hull, m.Hull);
                    b.armor.style.display = m.Armor < 0f ? DisplayStyle.None : DisplayStyle.Flex;
                    b.armor.style.width = Length.Percent(Mathf.Clamp01(m.Armor) * 100f);
                }
        }

        /// <summary>A thin bar (shield / armor / hull) under a member's name; returns its fill.</summary>
        static VisualElement Bar(VisualElement row, string cls)
        {
            var bar = new VisualElement();
            bar.AddToClassList("squad-bar");
            bar.AddToClassList(cls);
            var fill = new VisualElement();
            fill.AddToClassList("squad-bar-fill");
            bar.Add(fill);
            row.Add(bar);
            return fill;
        }

        /// <summary>The fraction's width; a pool the ship doesn't have (-1): no bar.</summary>
        static void SetBar(VisualElement fill, float fraction)
        {
            fill.parent.style.display = fraction < 0f ? DisplayStyle.None : DisplayStyle.Flex;
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }

        void RefreshPilots()
        {
            var me = NetPlayer.Local;
            var pilots = new List<NetPlayer>();
            if (hangar && me != null)
            {
                pilots.Add(me);   // the local player first, marked "(you)"
                foreach (var p in NetPlayer.All)
                    if (p != null && p.IsSpawned && !p.IsOwner && p.InHangar && p.Station == hereStation) pilots.Add(p);
            }
            pilotsPanel.style.display = pilots.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (pilots.Count == 0) { pilotsKey = ""; return; }
            var sb = new StringBuilder(pilotsCollapsed ? "c" : "o");
            foreach (var p in pilots) sb.Append('|').Append(p.OwnerClientId).Append(p.DisplayName).Append(NetSquad.Same(p, me)).Append(NetSquad.WasInvited(p))
                                        .Append(NetTradeClient.WasAsked(p)).Append(TradingWith(p));
            string key = sb.ToString();
            if (key == pilotsKey) return;
            pilotsKey = key;
            pilotsHeader.text = $"{Localization.Extra("mpPilotsHere", "Pilots in this hangar").ToUpperInvariant()} ({pilots.Count})  {(pilotsCollapsed ? "+" : "-")}";
            pilotsBody.Clear();
            pilotsBody.style.display = pilotsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var p in pilots)
            {
                var row = new VisualElement();
                row.AddToClassList("squad-pilot");
                var name = new Label(p.DisplayName + (p == me ? $"  ({Localization.Extra("mpYou", "you")})" : ""));
                name.AddToClassList("squad-name");
                row.Add(name);
                if (p == me) { }   // no invitation to oneself
                else if (NetSquad.Same(p, me))
                {
                    var tag = new Label(Localization.Extra("mpInYourSquad", "In your squad"));
                    tag.AddToClassList("squad-tag");
                    row.Add(tag);
                }
                else if (NetSquad.WasInvited(p))
                {
                    var tag = new Label(Localization.Extra("mpInvitedTag", "Invited"));
                    tag.AddToClassList("squad-tag");
                    row.Add(tag);
                }
                else
                {
                    var target = p;
                    row.Add(MakeButton(Localization.Extra("mpInvite", "Invite"), () => { NetSquad.InviteTo(target); pilotsKey = ""; }, null));
                }
                if (p != me) AddTrade(row, p);
                pilotsBody.Add(row);
            }
        }

        static bool TradingWith(NetPlayer p) => NetTradeClient.Current != null && p != null && NetTradeClient.Current.partner == p.OwnerClientId;

        /// <summary>A pilot's trade button: Trade, "Asked" while the request waits, "Trading" (opens the trade) during one.</summary>
        void AddTrade(VisualElement row, NetPlayer p)
        {
            if (TradingWith(p))
                row.Add(MakeButton(Localization.Extra("mpTradeTrading", "Trading"), MultiplayerWindow.OpenTradeAny, "squad-button--accept"));
            else if (NetTradeClient.WasAsked(p))
            {
                var tag = new Label(Localization.Extra("mpTradeAsked", "Asked"));
                tag.AddToClassList("squad-tag");
                row.Add(tag);
            }
            else
            {
                var target = p;
                row.Add(MakeButton(Localization.Extra("mpTradeButton", "Trade"), () => { NetTradeClient.Ask(target); pilotsKey = ""; }, null));
            }
        }

        void RefreshTrade()
        {
            var list = NetTradeClient.Requests;
            var latest = list.Count > 0 ? list[list.Count - 1] : null;
            // Docked only, one popup at a time (a squad invitation first), none while a trade is open.
            if (NetPlayer.Local == null || !NetPlayer.Local.InHangar || shownInvite != null || NetTradeClient.Current != null) latest = null;
            shownTrade = latest;
            if (latest != null) tradeText.text = string.Format(Localization.Extra("mpTradeRequested", "{0} wants to trade with you."), latest.name);
            tradePopup.style.display = shownTrade != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void RefreshInvite()
        {
            var invites = NetSquad.Invites;
            var latest = invites.Count > 0 ? invites[invites.Count - 1] : null;
            if (NetPlayer.Local == null || !NetPlayer.Local.InHangar) latest = null;   // squads form only in a hangar
            shownInvite = latest;
            if (latest != null)
            {
                string warning = NetMissions.AbandonWarning();
                inviteText.text = string.Format(Localization.Extra("mpSquadInvited", "{0} invites you to their squad."), latest.name)
                                  + (warning.Length > 0 ? "\n" + warning : "");
            }
            invitePopup.style.display = shownInvite != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
