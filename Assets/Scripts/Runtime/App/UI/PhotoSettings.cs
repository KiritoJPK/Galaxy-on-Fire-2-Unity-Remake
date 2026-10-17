// PhotoSettings.cs
// Remake-only: the photo modes' camera settings (Action Freeze in flight, Inspect ship in the hangar; UI.PhotoTools shows
// them in its Camera settings panel). Kept for the run, shared by both modes, so a look set up in flight is there in the
// hangar too; Reset puts them back. The field of view starts at the camera's own each time a photo mode opens. Plain C#.

using UnityEngine;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class PhotoSettings
    {
        /// <summary>Depth of field: the game's own (none in flight, the hangar's option there), none, sharp on the ship with the
        /// rest blurred, or a focus distance of the player's.</summary>
        public enum DofMode { Game, Off, Ship, Manual }

        public const float MinFov = 10f, MaxFov = 110f;   // vertical, degrees
        public const float MaxFocusMetres = 3000f;
        public static readonly float[] FrameAspects = { 0f, 16f / 9f, 21f / 9f, 4f / 3f, 1f, 9f / 16f };
        public static readonly string[] FrameNames = { "", "16:9", "21:9", "4:3", "1:1", "9:16" };

        public static bool Free;
        public static float Fov, DefaultFov = 60f;
        public static float Roll;            // degrees, + counter-clockwise
        public static float Speed = 1f;      // the free camera's, x
        public static DofMode Dof;
        public static float Focus = 0.5f;    // 0..1: 1 .. MaxFocusMetres, exponential (FocusMetres)
        public static float Blur = 0.5f;     // 0..1
        public static float Exposure;        // EV
        public static float Contrast;        // -50..50
        public static float Saturation;      // -100..100
        public static float Vignette;        // 0..0.6
        public static float Grain;           // 0..1
        public static bool HideShip, Grid;
        public static int Frame;             // FrameAspects
        public static int Size;              // the screenshot: 0 = the screen's size, 1 = twice it, 2 = four times

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Reset(); Free = false; DefaultFov = 60f; Fov = DefaultFov; }

        /// <summary>Everything back to the defaults (the mode, orbit or free, stays).</summary>
        public static void Reset()
        {
            Fov = DefaultFov;
            Roll = 0f;
            Speed = 1f;
            Dof = DofMode.Game;
            Focus = 0.5f;
            Blur = 0.5f;
            Exposure = Contrast = Saturation = Vignette = Grain = 0f;
            HideShip = Grid = false;
            Frame = 0;
            Size = 0;
        }

        /// <summary>The manual focus distance in metres.</summary>
        public static float FocusMetres => Mathf.Max(1f, Mathf.Pow(MaxFocusMetres, Mathf.Clamp01(Focus)));

        public const int MaxShotPixels = 16384;   // a texture's largest side

        /// <summary>ScreenCapture's superSize for Size on a w x h screen: 1, 2 or 4, less when the picture would pass
        /// MaxShotPixels (a 4K screen at x4).</summary>
        public static int SuperSize(int w, int h)
        {
            int want = Size >= 2 ? 4 : Size == 1 ? 2 : 1;
            int fit = Mathf.Max(1, MaxShotPixels / Mathf.Max(1, Mathf.Max(w, h)));
            return Mathf.Clamp(Mathf.Min(want, fit), 1, 4);
        }

        public static float FrameAspect => Frame > 0 && Frame < FrameAspects.Length ? FrameAspects[Frame] : 0f;

        /// <summary>The part of a w x h picture the frame keeps, centred (pixels; aspect 0 = all of it).</summary>
        public static RectInt FrameRect(int w, int h, float aspect)
        {
            if (aspect <= 0f || w <= 0 || h <= 0) return new RectInt(0, 0, Mathf.Max(0, w), Mathf.Max(0, h));
            if ((float)w / h > aspect)
            {
                int fw = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 1, w);
                return new RectInt((w - fw) / 2, 0, fw, h);
            }
            int fh = Mathf.Clamp(Mathf.RoundToInt(w / aspect), 1, h);
            return new RectInt(0, (h - fh) / 2, w, fh);
        }
    }
}
