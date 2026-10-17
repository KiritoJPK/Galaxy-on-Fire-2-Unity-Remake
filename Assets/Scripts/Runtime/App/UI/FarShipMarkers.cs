// FarShipMarkers.cs
// Remake (players' request, 2026-10-17, "the far ship icon dot blocks the view of the ship"): how CombatView draws a far ship's
// on-screen marker (Options > Gameplay "Far ship markers", Settings.FarShipMarkers) and the distance under it ("Distance on
// ship markers", Settings.MarkerDistance). Plain C#: the choices only, CombatView places the elements.
//   Original   the filled dot over the ship, the ring when locked (Radar::draw), the locked one's distance at (-61, +61)
//   Ring       the lock ring's four arcs with the centre cleared (ship_ring_open_*, Reference/tools/hud/make_open_rings.py)
//   Small dot  the dot at 0.6 x, its centre 26 px above the ship
// Locked, the remake's styles take the faction bracket 81 x 81 (the near ship's lock bracket), open in the middle too.
// Off screen (the radar ellipse) every style keeps the original's dot / ring.

using GoF2Remake.Data;

namespace GoF2Remake.UI
{
    public static class FarShipMarkers
    {
        public enum Shape { Dot, Ring, OpenRing, Bracket }

        public const float SmallDotScale = 0.6f, SmallDotRise = 26f;

        /// <summary>The far on-screen marker: its image, scale and how far (panel px) its centre sits above the ship.</summary>
        public static (Shape shape, float scale, float rise) Look(int style, bool locked)
        {
            if (style == Settings.FarMarkerOriginal) return (locked ? Shape.Ring : Shape.Dot, 1f, 0f);
            if (locked) return (Shape.Bracket, 1f, 0f);
            return style == Settings.FarMarkerSmallDot ? (Shape.Dot, SmallDotScale, SmallDotRise) : (Shape.OpenRing, 1f, 0f);
        }

        /// <summary>Whether a far on-screen marker shows its distance: the locked one always (the original), the others with
        /// the option.</summary>
        public static bool ShowsDistance(bool locked, bool option) => locked || option;

        /// <summary>The top of an unlocked marker's distance label below the ship (panel px): under the image's visible part
        /// (the dot's glow ends ~14 px out, the open ring's arcs 29), under the ship itself for the small dot above it. The
        /// locked label stays at the original's +61.</summary>
        public static float LabelTop(int style, bool locked)
        {
            if (locked) return 61f;
            return style switch
            {
                Settings.FarMarkerRing => 30f,
                Settings.FarMarkerSmallDot => 12f,
                _ => 16f,
            };
        }

        /// <summary>A key that changes exactly when Navigation.FormatDistance's text does (Radar::calcDistance: metres in steps
        /// of 8 below 1 km, then km with one decimal), so the labels are written only when their text changes.</summary>
        public static int DistanceKey(float units)
        {
            int m = 8 * (int)(units / 128f);
            if (m < 1000) return m;
            return 1000 + (m / 1000) * 10 + (m % 1000) / 100;
        }
    }
}
