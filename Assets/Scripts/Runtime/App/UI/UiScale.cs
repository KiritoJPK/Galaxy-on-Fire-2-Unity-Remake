// UiScale.cs
// Remake: how big the UI comes out physically. The panels scale to the screen's height (1920 x 1080, phones 1600 x 900),
// so on a small high-density screen (a 5.5" handheld at 1080p, the Retroid Pocket G2) a 17-unit text line is about 1.3 mm
// high and a 36-unit button 3 mm: too small to read or tap. Large() says a panel should use its large variant (the
// multiplayer chat and window: `.chat--large`, `.mpw--large`): when the body text would be under BodyInches on the
// device, measured from the panel's own scaling and the screen's dpi; desktops and tablets stay as they are.

using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public static class UiScale
    {
        /// <summary>The body text's panel size and the smallest it should be on the screen (2.8 mm).</summary>
        const float BodyUnits = 17f, BodyInches = 0.11f;

        /// <summary>How many times larger the UI should be for the body text to reach BodyInches (1 = as it is, or no dpi).</summary>
        public static float Physical(VisualElement e)
        {
            float dpi = Screen.dpi;
            if (dpi <= 0f) return 1f;
            float ppp = e != null && e.panel != null ? e.scaledPixelsPerPoint : Screen.height / 1080f;
            if (ppp <= 0f) return 1f;
            float inches = BodyUnits * ppp / dpi;
            return Mathf.Max(1f, BodyInches / inches);
        }

        /// <summary>The large variant: the text would be well under BodyInches (phones, handhelds).</summary>
        public static bool Large(VisualElement e) => Physical(e) >= 1.3f;

        /// <summary>The same from the screen's height and the panel's reference height (a panel's layout pass, before its
        /// scaling has applied): the screens' root class "ui-large" (MainMenu, StationMenu, FlightHud).</summary>
        public static bool Large(float screenHeight, float referenceHeight)
        {
            float dpi = Screen.dpi;
            if (dpi <= 0f || screenHeight <= 0f || referenceHeight <= 0f) return false;
            return BodyInches / (BodyUnits * (screenHeight / referenceHeight) / dpi) >= 1.3f;
        }
    }
}
