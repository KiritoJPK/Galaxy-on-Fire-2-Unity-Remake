// BindingRow.cs
// One line of the key bindings list (remake, Options > Controls; OptionKind.Binding, styled by GoF2Common.uss .binding-*):
// the control's name, then its slots as cells: two keyboard / mouse keys and the controller button (GameControls). A click or
// tap on a cell, or Enter / A on the row (left / right pick the cell), waits for the new key: Esc cancels (and the other
// devices: GameControls.Rebind, or 10 s without input), Backspace / Delete
// unbinds, a right click unbinds a cell. Remake (players couldn't find how to clear one, and a controller couldn't): a bound
// cell shows a × (on hover, on the selected cell of the selected row, always on touch) that clears it, and on the selected
// row Delete or the controller's X clears the selected cell without a capture (ClearSelected; Backspace is Back in the
// pause menu). The row itself is the focusable element, like ChoiceRow. While a key is being captured the panel's
// navigation events are swallowed (the menus' own keys would act on it too).

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class BindingRow : VisualElement
    {
        readonly ControlRow row;
        readonly Label label;
        readonly Button[] cells = new Button[3];
        readonly Button[] clears = new Button[3];
        int selected;
        int listening = -1;
        string part;

        static readonly HashSet<IPanel> blockedPanels = new HashSet<IPanel>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => blockedPanels.Clear();

        public BindingRow(ControlRow row)
        {
            this.row = row;
            focusable = true;
            AddToClassList("binding-row");
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("binding-label");
            Add(label);
            for (int s = 0; s < 3; s++)
            {
                int slot = s;
                var b = new Button { focusable = false };
                b.AddToClassList("binding-cell");
                b.AddToClassList("gof-semibold");
                if (s == (int)BindSlot.Pad) b.AddToClassList("binding-cell--pad");
                if (!row.HasSlot((BindSlot)s)) { b.AddToClassList("binding-cell--none"); b.SetEnabled(false); }
                b.clicked += () => { selected = slot; Listen(slot); };
                b.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (e.button != 1 || !row.HasSlot((BindSlot)slot)) return;   // right click: unbind
                    GameControls.CancelRebind();
                    GameControls.Clear(row, (BindSlot)slot);
                });
                // The × that clears the cell (its press stays its own: the cell under it doesn't start a capture).
                var x = new Button(() => { selected = slot; Clear(slot); }) { text = "×", focusable = false };
                x.AddToClassList("binding-cell-clear");
                x.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                x.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
                x.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                b.Add(x);
                clears[s] = x;
                cells[s] = b;
                Add(b);
            }
            // Delete / the controller's X on the selected row clear its selected cell (every frame while shown).
            schedule.Execute(PollClear).Every(0);
            if (!row.HasSlot(BindSlot.Key1)) selected = 2;
            RegisterCallback<NavigationSubmitEvent>(e =>
            {
                if (GameControls.BlocksMenus) return;
                Activate();
                e.StopPropagation();
                focusController?.IgnoreEvent(e);
            });
            RegisterCallback<AttachToPanelEvent>(e => { GameControls.Changed += Refresh; InputMode.Changed += Refresh; BlockWhileRebinding(e.destinationPanel); Refresh(); });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                GameControls.Changed -= Refresh;
                InputMode.Changed -= Refresh;
                if (listening >= 0) GameControls.CancelRebind();
            });
            Refresh();
        }

        /// <summary>The column titles over the rows (under Reset key bindings): Key, Second key, Controller.</summary>
        public static VisualElement Header()
        {
            var h = new VisualElement { pickingMode = PickingMode.Ignore };
            h.AddToClassList("binding-row");
            h.AddToClassList("binding-header");
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("binding-label");
            h.Add(spacer);
            foreach (var text in new[] { Localization.Extra("bindKey", "Key"), Localization.Extra("bindKey2", "Second key"), Localization.Extra("bindPad", "Controller") })
            {
                var l = new Label(text) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("binding-cell");
                l.AddToClassList("binding-header-cell");
                h.Add(l);
            }
            return h;
        }

        /// <summary>Left / right on the row: the next cell that has a slot.</summary>
        public void Step(int dir)
        {
            if (listening >= 0) return;
            for (int i = selected + dir; i >= 0 && i < 3; i += dir)
                if (row.HasSlot((BindSlot)i)) { selected = i; break; }
            Refresh();
        }

        /// <summary>Enter / A on the row: capture the selected cell's key.</summary>
        public void Activate() => Listen(selected);

        /// <summary>Clears the selected cell's binding (Delete or the controller's X on the selected row).</summary>
        public void ClearSelected() => Clear(selected);

        void Clear(int slot)
        {
            if (!row.HasSlot((BindSlot)slot)) return;
            GameControls.CancelRebind();
            if (GameControls.IsBound(row, (BindSlot)slot)) GameControls.Clear(row, (BindSlot)slot);
        }

        /// <summary>The menus select a row by focus (main menu, station) or by a class on its option (the pause menu).</summary>
        bool IsSelectedRow()
        {
            if (focusController != null && focusController.focusedElement == this) return true;
            for (VisualElement v = parent; v != null; v = v.parent)
                if (v.ClassListContains("autopilot-menu-item--selected")) return true;
            return false;
        }

        void PollClear()
        {
            if (listening >= 0 || panel == null || GameControls.BlocksMenus || !IsSelectedRow()) return;
            var kb = Multiplayer.NetChat.Keys;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.deleteKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) ClearSelected();
        }

        /// <summary>A keyboard cell takes a capture only once a keyboard was used (InputMode.KeyboardSeen): on a phone without
        /// one it would wait for a key that can't come.</summary>
        static bool Usable(int slot) => slot == (int)BindSlot.Pad || InputMode.KeyboardSeen;

        void Listen(int slot)
        {
            if (!Usable(slot) && row.HasSlot(BindSlot.Pad)) slot = selected = (int)BindSlot.Pad;   // Enter / A on the row
            if (!row.HasSlot((BindSlot)slot) || !Usable(slot)) return;
            listening = slot;
            part = null;
            Refresh();
            GameControls.Rebind(row, (BindSlot)slot, p => { part = p; Refresh(); }, () => { listening = -1; part = null; Refresh(); });
            // A menu page closed meanwhile (Back with the mouse): stop waiting.
            schedule.Execute(() => { if (listening >= 0 && !IsShown()) GameControls.CancelRebind(); }).Every(100).Until(() => listening < 0);
        }

        bool IsShown()
        {
            if (panel == null) return false;
            for (VisualElement v = this; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        public void Refresh()
        {
            label.text = row.label();
            for (int s = 0; s < 3; s++)
            {
                var b = cells[s];
                bool has = row.HasSlot((BindSlot)s);
                bool wait = s == listening;
                string text = !has ? "" : wait ? Prompt() : GameControls.SlotText(row, (BindSlot)s);
                b.text = has && text.Length == 0 ? "—" : text;
                bool bound = has && GameControls.IsBound(row, (BindSlot)s);
                b.EnableInClassList("binding-cell--unbound", has && !wait && !bound);
                b.EnableInClassList("binding-cell--bound", bound && !wait);
                b.EnableInClassList("binding-cell--listening", wait);
                b.EnableInClassList("binding-cell--selected", s == selected);
                b.SetEnabled(has && Usable(s));
            }
        }

        string Prompt()
        {
            string press = listening == (int)BindSlot.Pad ? Localization.Extra("bindPressButton", "Press a button") : Localization.Extra("bindPressKey", "Press a key");
            return part != null ? $"{press}: {part}" : press + "…";
        }

        /// <summary>While a key is being captured (and the frame after), the panel's navigation events don't reach the menus:
        /// Esc would also go back, Enter confirm, the arrows move.</summary>
        static void BlockWhileRebinding(IPanel p)
        {
            if (p == null || !blockedPanels.Add(p)) return;
            void Swallow(EventBase e)
            {
                if (!GameControls.BlocksMenus) return;
                e.StopImmediatePropagation();
                (e.target as VisualElement)?.focusController?.IgnoreEvent(e);
            }
            var tree = p.visualTree;
            tree.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
            tree.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            tree.RegisterCallback<NavigationCancelEvent>(Swallow, TrickleDown.TrickleDown);
            tree.RegisterCallback<KeyDownEvent>(Swallow, TrickleDown.TrickleDown);
        }
    }
}
