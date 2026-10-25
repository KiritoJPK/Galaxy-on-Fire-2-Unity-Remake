// ChoiceRow.cs
// One option with a fixed set of values (remake UI, styled by GoF2Common.uss .choice-*): a label, then either
// segments (every value as a clickable button, for a few short values) or a stepper (‹ value ›, for long lists).
// Modified: the long lists are a drop-down instead of the stepper: a box with the value and a down chevron; a click / tap
// opens the list of values under it (ChoicePopup, the current one ticked), a pick or a press outside closes it. The keys
// and the controller still step through the values (left / right). The box sits at the row's right end, a set share of
// its width; a value longer than the box slides left and back (a pause at each end) so it can be read.
// The row itself is the focusable element: left / right step through it (MainMenu, PauseMenu call Step); Enter / A leave
// it as it is (they cycled it, a player's report: A should only take the value shown).

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public class ChoiceRow : VisualElement
    {
        readonly Label label;
        readonly VisualElement segments;
        readonly Label value;
        readonly Button dropdown;
        readonly VisualElement textClip;
        float marqueeMs;
        bool sliding;
        const float MarqueeHoldMs = 1200f, MarqueeSpeed = 40f;   // px per second

        /// <summary>A value wider than its box slides to its end and back, holding still a moment at each end. The label is
        /// placed by hand (absolute, its text's own width): laid out by the row, it was squeezed to the box and only
        /// trembled in the middle.</summary>
        void Marquee()
        {
            if (value == null || textClip == null) return;
            // Hidden (another tab, the menu closed): back to the start, so it begins again from the text's first letter (a
            // value that fits keeps its centred place: set to 0 it showed at the left and jumped back, as if animated).
            if (!Shown(textClip)) { marqueeMs = 0f; if (sliding) value.style.left = 0f; return; }
            float clipW = textClip.layout.width;
            if (float.IsNaN(clipW) || clipW <= 0f) return;
            float textW = value.MeasureTextSize(value.text ?? "", 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
            if (float.IsNaN(textW)) return;
            value.style.width = textW + 2f;
            float over = textW + 2f - clipW;
            sliding = over > 1f;
            if (over <= 1f)
            {
                value.style.left = (clipW - textW - 2f) * 0.5f;   // fits: centred, still
                marqueeMs = 0f;
                return;
            }
            float travelMs = over / MarqueeSpeed * 1000f;
            float cycle = 2f * (MarqueeHoldMs + travelMs);
            marqueeMs = (marqueeMs + 33f) % cycle;
            float t = marqueeMs, x;
            if (t < MarqueeHoldMs) x = 0f;
            else if ((t -= MarqueeHoldMs) < travelMs) x = t / travelMs;
            else if ((t -= travelMs) < MarqueeHoldMs) x = 1f;
            else x = 1f - (t - MarqueeHoldMs) / travelMs;
            value.style.left = -over * Mathf.SmoothStep(0f, 1f, x);
        }
        string[] choices = Array.Empty<string>();
        int index;

        /// <summary>The player picked another value (not raised by SetWithoutNotify).</summary>
        public event Action<int> Changed;

        public bool Segmented { get; }
        public int Index => index;
        public int Count => choices.Length;

        public ChoiceRow(bool segmented)
        {
            Segmented = segmented;
            focusable = true;
            AddToClassList("choice-row");
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("choice-label");
            Add(label);
            if (segmented)
            {
                segments = new VisualElement();
                segments.AddToClassList("choice-segments");
                Add(segments);
            }
            else
            {
                dropdown = new Button { focusable = false };
                dropdown.AddToClassList("choice-dropdown");
                value = new Label { pickingMode = PickingMode.Ignore };
                value.AddToClassList("choice-value");
                value.AddToClassList("gof-semibold");
                textClip = new VisualElement { pickingMode = PickingMode.Ignore };
                textClip.AddToClassList("choice-dropdown-text");
                textClip.Add(value);
                // Placed by Marquee every frame: no transition (a stylesheet's slid it in from the left on every page shown).
                value.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new TimeValue(0f) });
                dropdown.Add(textClip);
                value.schedule.Execute(Marquee).Every(33);
                textClip.RegisterCallback<GeometryChangedEvent>(_ => Marquee());   // placed at once, not a tick later
                var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
                chevron.AddToClassList("choice-dropdown-chevron");
                dropdown.Add(chevron);
                dropdown.clicked += () =>
                {
                    if (ChoicePopup.IsOpenFor(dropdown)) ChoicePopup.Close();
                    else if (enabledInHierarchy) ChoicePopup.Open(dropdown, choices, index, Pick);
                };
                Add(dropdown);
            }
        }


        public string LabelText { get => label.text; set => label.text = value; }

        /// <summary>New value texts and the current index, without raising Changed.</summary>
        public void SetWithoutNotify(string[] texts, int current)
        {
            texts ??= Array.Empty<string>();
            bool rebuild = segments != null && segments.childCount != texts.Length;
            choices = texts;
            index = texts.Length == 0 ? 0 : Math.Clamp(current, 0, texts.Length - 1);
            if (rebuild)
            {
                segments.Clear();
                for (int i = 0; i < texts.Length; i++)
                {
                    int pick = i;
                    var b = new Button { focusable = false };
                    b.AddToClassList("choice-segment");
                    if (i == texts.Length - 1) b.AddToClassList("choice-segment--last");   // USS has no :last-child in Unity 7
                    b.AddToClassList("gof-semibold");
                    b.clicked += () => Pick(pick);
                    segments.Add(b);
                }
            }
            UpdateView();
        }

        /// <summary>One step left (-1) or right (+1), clamped to the ends.</summary>
        public void Step(int dir) => Pick(index + dir);

        void Pick(int i)
        {
            if (choices.Length == 0) return;
            i = Math.Clamp(i, 0, choices.Length - 1);
            if (i == index) return;
            index = i;
            UpdateView();
            Changed?.Invoke(i);
        }

        /// <summary>Shown on screen: neither it nor an ancestor is display: none.</summary>
        static bool Shown(VisualElement e)
        {
            if (e.panel == null) return false;
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        void UpdateView()
        {
            marqueeMs = 0f;   // a new value starts sliding from its beginning
            if (segments != null)
            {
                for (int i = 0; i < segments.childCount && i < choices.Length; i++)
                {
                    var b = (Button)segments[i];
                    b.text = choices[i];
                    b.EnableInClassList("choice-segment--active", i == index);
                }
            }
            else if (value != null) value.text = choices.Length > 0 ? choices[index] : "";
        }
    }
}
