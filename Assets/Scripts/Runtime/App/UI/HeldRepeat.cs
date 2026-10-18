// HeldRepeat.cs
// Remake: a held direction (a key, the D-pad, a stick) repeats in the menus that read the devices themselves (the flight
// pause menu): one step on the press, then after a short wait a step every few frames while it is held, like the panels'
// own navigation (a player's report: holding up / down in the pause menu's Options moved one row only). Real time: the
// game is paused under those menus.

using UnityEngine;

namespace GoF2Remake.UI
{
    public sealed class HeldRepeat
    {
        public const float Delay = 0.4f, Interval = 0.08f;

        int dir;
        float next;

        /// <summary>True for the step that came from a press (not from holding): what may wrap round at a list's end.</summary>
        public bool Fresh { get; private set; }

        /// <summary>The step this frame (-1, 0 or 1). 'pressed': the direction pressed this frame (a tap released within the
        /// frame counts too), 'held': the direction held now.</summary>
        public int Tick(int pressed, int held) => Tick(pressed, held, Time.unscaledTime);

        public int Tick(int pressed, int held, float now)
        {
            Fresh = false;
            if (pressed != 0)
            {
                dir = held == pressed ? pressed : 0;
                next = now + Delay;
                Fresh = true;
                return pressed;
            }
            if (held == 0 || held != dir) { dir = 0; return 0; }
            if (now < next) return 0;
            next = Mathf.Max(next + Interval, now);
            return dir;
        }

        public void Reset() => dir = 0;
    }
}
