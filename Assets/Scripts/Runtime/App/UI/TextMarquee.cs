// TextMarquee.cs
// Modified: a one-line label inside a clipping box: centred and still when it fits; else it slides to its end and back,
// holding a moment at each end (the options' drop-downs do the same: ChoiceRow.Marquee), starting again from its first
// letter whenever it was hidden. The label is placed by hand (absolute, its text's own width): laid out by its box, it was
// squeezed to it. Used by the Debug page's item grids (CheatsCatalog).

using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class TextMarquee
    {
        const float HoldMs = 1200f, Speed = 40f;   // px per second
        const long TickMs = 33;

        /// <summary>'label' (a child of 'clip') slides inside 'clip' when wider than it.</summary>
        public static void Attach(VisualElement clip, Label label)
        {
            clip.style.overflow = Overflow.Hidden;
            label.style.position = Position.Absolute;
            label.style.top = 0f;
            label.style.bottom = 0f;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new TimeValue(0f) });
            float ms = 0f;
            bool sliding = false;
            void Tick()
            {
                if (!Shown(clip)) { ms = 0f; if (sliding) label.style.left = 0f; return; }
                float clipW = clip.layout.width;
                if (float.IsNaN(clipW) || clipW <= 0f) return;
                float textW = label.MeasureTextSize(label.text ?? "", 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                if (float.IsNaN(textW)) return;
                label.style.width = textW + 2f;
                float over = textW + 2f - clipW;
                sliding = over > 1f;
                if (!sliding) { label.style.left = (clipW - textW - 2f) * 0.5f; ms = 0f; return; }
                float travel = over / Speed * 1000f;
                ms = (ms + TickMs) % (2f * (HoldMs + travel));
                float t = ms, x;
                if (t < HoldMs) x = 0f;
                else if ((t -= HoldMs) < travel) x = t / travel;
                else if ((t -= travel) < HoldMs) x = 1f;
                else x = 1f - (t - HoldMs) / travel;
                label.style.left = -over * Mathf.SmoothStep(0f, 1f, x);
            }
            label.schedule.Execute(Tick).Every(TickMs);
            clip.RegisterCallback<GeometryChangedEvent>(_ => Tick());
        }

        static bool Shown(VisualElement e)
        {
            if (e.panel == null) return false;
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }
    }
}
