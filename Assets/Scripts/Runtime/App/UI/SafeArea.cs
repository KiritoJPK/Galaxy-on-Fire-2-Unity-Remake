// SafeArea.cs
// Remake-only: the device's safe area (Screen.safeArea) as insets of a UI Toolkit panel, shared by every screen that keeps
// its controls clear of the display cutouts: the iPhone's notch / Dynamic Island (landscape: the same inset on the left and
// the right, whichever side the island is on, iOS reports both), its home indicator (the bottom inset), the rounded
// corners, Android's punch holes. Screen.safeArea is in pixels with y up; the insets come out in panel units.
// Screen.safeArea can reach past Screen.width / height on some Android phones (display cutouts / insets): it is clamped to
// the screen, and a safe area under half the screen either way is taken for nonsense and ignored.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class SafeArea
    {
        /// <summary>The insets in pixels of a w x h screen: x left, y right, z top, w bottom. None for a panel drawn into a
        /// texture (VR, the item window): that texture has no cutouts.</summary>
        public static Vector4 Pixels(float w, float h, bool offscreen)
        {
            if (offscreen || w <= 0f || h <= 0f) return Vector4.zero;
            var raw = Screen.safeArea;
            var sa = Rect.MinMaxRect(Mathf.Clamp(raw.xMin, 0f, w), Mathf.Clamp(raw.yMin, 0f, h),
                                     Mathf.Clamp(raw.xMax, 0f, w), Mathf.Clamp(raw.yMax, 0f, h));
            if (sa.width < w * 0.5f || sa.height < h * 0.5f) return Vector4.zero;   // nonsense: ignore
            return new Vector4(sa.xMin, w - sa.xMax, h - sa.yMax, sa.yMin);
        }

        /// <summary>The insets in 'root''s panel units (root laid out over the whole w-pixel-wide screen); zero while it has
        /// no size yet.</summary>
        public static Vector4 Panel(VisualElement root, float w, float h, bool offscreen)
        {
            if (root == null || !(root.layout.width > 0f) || w <= 0f) return Vector4.zero;   // also catches NaN
            return Pixels(w, h, offscreen) * (root.layout.width / w);
        }

        /// <summary>How Apply puts the insets on its target.</summary>
        public enum Mode
        {
            /// <summary>The left / right / top / bottom of an absolutely placed element (the HUD's and the station's).</summary>
            Position,
            /// <summary>The margins of a flex child stretched across its parent (the main menu's, which keeps a 100 % width
            /// and a max width for its 21:9 centring while there are no insets). Not padding: absolutely placed children
            /// are laid out against their parent's padding box, so padding never moved them.</summary>
            Margins,
        }

        /// <summary>
        /// Puts the insets on 'target' once 'root' is laid out (its size is NaN on the first frame: until then it retries
        /// every frame; a NaN inset collapses the UI into the top-left corner). 'applied' gets the pixel insets used.
        /// </summary>
        public static void Apply(VisualElement root, VisualElement target, float w, float h, bool offscreen, Mode mode,
                                 Action<Vector4> applied = null)
        {
            if (root == null || target == null) return;
            root.schedule.Execute(() =>
            {
                if (!(root.layout.width > 0f)) { Apply(root, target, w, h, offscreen, mode, applied); return; }
                var px = Pixels(w, h, offscreen);
                var p = px * (root.layout.width / w);
                var s = target.style;
                if (mode == Mode.Margins)
                {
                    s.marginLeft = p.x;
                    s.marginRight = p.y;
                    s.marginTop = p.z;
                    s.marginBottom = p.w;
                    // With insets the box stretches between them (its USS width of 100 % would overflow by the margins);
                    // without, the USS's own width and centring apply again.
                    bool inset = p != Vector4.zero;
                    s.width = inset ? new StyleLength(StyleKeyword.Auto) : new StyleLength(StyleKeyword.Null);
                    s.alignSelf = inset ? new StyleEnum<Align>(Align.Stretch) : new StyleEnum<Align>(StyleKeyword.Null);
                }
                else
                {
                    s.left = p.x;
                    s.right = p.y;
                    s.top = p.z;
                    s.bottom = p.w;
                }
                applied?.Invoke(px);
            }).ExecuteLater(1);
        }

        /// <summary>The class an edge-anchored element of a full-screen overlay carries to be kept clear of the cutouts
        /// (KeepClear): the overlay itself stays full screen (photo mode's frame bars must match the screenshot).</summary>
        public const string EdgeClass = "safe-edge";

        /// <summary>
        /// Keeps the EdgeClass descendants of a full-screen overlay (photo mode, Inspect ship: their titles, hints, footers
        /// and side panels) clear of the cutouts through their margins, which absolutely placed elements are offset by;
        /// checked when the overlay is laid out and twice a second (the safe area turns with the device, elements come and go). None in VR (the
        /// panels are drawn into textures there).
        /// </summary>
        public static void KeepClear(VisualElement overlay)
        {
            if (overlay == null) return;
            void Refresh()
            {
                if (overlay.panel == null || !(overlay.layout.width > 0f)) return;
                var p = Panel(overlay, Screen.width, Screen.height, Vr.VrMode.Enabled);
                // Every time (elements added since get theirs too); an unchanged inline value isn't laid out again.
                overlay.Query(className: EdgeClass).ForEach(e =>
                {
                    var s = e.style;
                    s.marginLeft = p.x;
                    s.marginRight = p.y;
                    s.marginTop = p.z;
                    s.marginBottom = p.w;
                });
            }
            overlay.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            overlay.schedule.Execute(Refresh).Every(500);
        }
    }
}
