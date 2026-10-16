// ChoiceRow.cs
// One option with a fixed set of values (remake UI, styled by GoF2Common.uss .choice-*): a label, then either
// segments (every value as a clickable button, for a few short values) or a stepper (‹ value ›, for long lists).
// The row itself is the focusable element: left / right step through it (MainMenu, PauseMenu call Step).

using System;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public class ChoiceRow : VisualElement
    {
        readonly Label label;
        readonly VisualElement segments;
        readonly Label value;
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
                var stepper = new VisualElement();
                stepper.AddToClassList("choice-stepper");
                stepper.Add(Arrow("‹", -1));
                value = new Label { pickingMode = PickingMode.Ignore };
                value.AddToClassList("choice-value");
                value.AddToClassList("gof-semibold");
                stepper.Add(value);
                stepper.Add(Arrow("›", 1));
                Add(stepper);
            }
        }

        Button Arrow(string text, int dir)
        {
            var b = new Button { text = text, focusable = false };
            b.AddToClassList("choice-arrow");
            b.AddToClassList("gof-semibold");
            b.clicked += () => Step(dir);
            return b;
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

        /// <summary>Confirm on the row (Enter / A): the next value, wrapping round.</summary>
        public void Cycle() { if (choices.Length > 0) Pick((index + 1) % choices.Length); }

        void Pick(int i)
        {
            if (choices.Length == 0) return;
            i = Math.Clamp(i, 0, choices.Length - 1);
            if (i == index) return;
            index = i;
            UpdateView();
            Changed?.Invoke(i);
        }

        void UpdateView()
        {
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
