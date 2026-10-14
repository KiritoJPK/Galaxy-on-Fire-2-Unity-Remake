// AutofireLatch.cs
// The fire button's autofire latch (MGame::OnTouchBegin 0x1a898a, touch_hud.md 6.2): a press within 249 ms of the previous
// one latches autofire (MGame+0x5c), the next single press releases it and doesn't fire. Shared by the touch fire button
// (TouchControls) and, as a remake option, the keyboard / mouse / controller fire binding (WeaponSystem, Settings.KeyAutofire).

namespace GoF2Remake.Flight
{
    public static class AutofireLatch
    {
        /// <summary>MGame::OnTouchBegin: diff &lt; 0xf9 ms.</summary>
        public const float WindowSeconds = 0.249f;

        /// <summary>A fire press at 'now' (seconds). Returns false when it released the latch: that press doesn't fire.</summary>
        public static bool Press(ref bool latched, ref float lastPress, float now)
        {
            bool fires = true;
            if (now - lastPress <= WindowSeconds) latched = true;
            else if (latched) { latched = false; fires = false; }
            lastPress = now;
            return fires;
        }
    }
}
