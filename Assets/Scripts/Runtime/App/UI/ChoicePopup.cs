// ChoicePopup.cs
// Modified: the list a ChoiceRow drop-down opens (Options: the settings with many values). Laid over the whole UI document
// (the options' scroll view would clip it), under its box, its right edge at the box's, at least 340 px wide (above it when there's no room below), kept
// on screen; one row per value, the current one ticked; past 8 values it scrolls (the wheel, a drag) with the current
// value in view. A pick sets the value and closes it; a press outside it, the box being hidden (another tab, the menu
// closed) or Close close it. Styled by GoF2Common.uss (.choice-popup*), the options' own look.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // nothing outlives a panel: rebuilt on every Open
    public static class ChoicePopup
    {
        const float ItemHeight = 40f, Gap = 4f, Margin = 12f, MinWidth = 340f;
        const int MaxVisible = 8;

        static VisualElement popup, owner, host;
        static IVisualElementScheduledItem follow;

        /// <summary>A list is open (Esc / B close it first).</summary>
        public static bool IsOpen => popup != null;

        /// <summary>The list is open under this box.</summary>
        public static bool IsOpenFor(VisualElement box) => popup != null && owner == box;

        /// <summary>Opens the list of 'values' under 'box', 'current' ticked; 'pick' gets the chosen index.</summary>
        public static void Open(VisualElement box, string[] values, int current, Action<int> pick)
        {
            Close();
            if (box?.panel == null || values == null || values.Length == 0) return;
            var root = box;
            while (root.parent != null && root.parent != box.panel.visualTree) root = root.parent;

            popup = new VisualElement { pickingMode = PickingMode.Position };
            popup.AddToClassList("choice-popup");
            popup.style.position = Position.Absolute;
            int shown = Mathf.Min(values.Length, MaxVisible);
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
            };
            scroll.AddToClassList("choice-popup-scroll");
            // The theme's scroll content is absolutely placed (its own height): the view gets the rows' height itself.
            scroll.style.height = shown * ItemHeight;
            popup.Add(scroll);
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                var item = new Button { focusable = false };
                item.AddToClassList("choice-popup-item");
                item.style.height = ItemHeight;
                var text = new Label(values[i]) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("choice-popup-text");
                text.AddToClassList("gof-semibold");
                item.Add(text);
                if (i == current)
                {
                    item.AddToClassList("choice-popup-item--current");
                    var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                    tick.AddToClassList("choice-popup-tick");
                    item.Add(tick);
                }
                item.clicked += () => { Close(); pick?.Invoke(index); };
                scroll.Add(item);
            }
            root.Add(popup);
            popup.BringToFront();
            owner = box;
            host = root;
            host.RegisterCallback<PointerDownEvent>(OutsidePress, TrickleDown.TrickleDown);
            int first = Mathf.Clamp(current - MaxVisible / 2, 0, Mathf.Max(0, values.Length - shown));
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0f, first * ItemHeight));
            popup.RegisterCallback<GeometryChangedEvent>(_ => Place());
            Place();
            follow = popup.schedule.Execute(Follow).Every(100);
        }

        /// <summary>Closes the list (if open).</summary>
        public static void Close()
        {
            follow?.Pause();
            follow = null;
            host?.UnregisterCallback<PointerDownEvent>(OutsidePress, TrickleDown.TrickleDown);
            host = null;
            owner = null;
            popup?.RemoveFromHierarchy();
            popup = null;
        }

        static void Place()
        {
            if (popup == null || owner == null || host == null) return;
            var r = host.WorldToLocal(owner.worldBound);
            // The box is narrow (a quarter of the row): the list is at least MinWidth wide, its right edge at the box's.
            float w = Mathf.Max(r.width, MinWidth);
            popup.style.width = w;
            float h = popup.resolvedStyle.height, hostW = host.resolvedStyle.width, hostH = host.resolvedStyle.height;
            if (float.IsNaN(h) || float.IsNaN(hostH)) { popup.style.left = r.xMax - w; popup.style.top = r.yMax + Gap; return; }
            float x = Mathf.Clamp(r.xMax - w, Margin, Mathf.Max(Margin, hostW - w - Margin));
            float y = r.yMax + Gap;
            if (y + h > hostH - Margin) y = Mathf.Max(Margin, r.yMin - Gap - h);
            popup.style.left = x;
            popup.style.top = y;
        }

        static void Follow()
        {
            if (owner == null) return;
            if (owner.panel == null || !Visible(owner)) { Close(); return; }
            Place();
        }

        /// <summary>A press outside the list closes it (the box toggles it itself); the press goes on.</summary>
        static void OutsidePress(PointerDownEvent e)
        {
            if (!(e.target is VisualElement t)) return;
            if (popup != null && (t == popup || popup.Contains(t))) return;
            if (owner != null && (t == owner || owner.Contains(t))) return;
            Close();
        }

        static bool Visible(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None || v.resolvedStyle.visibility == Visibility.Hidden) return false;
            var b = e.worldBound;
            return !float.IsNaN(b.width) && b.width > 0f;
        }
    }
}
