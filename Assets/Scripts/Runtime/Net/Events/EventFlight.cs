// EventFlight.cs
// Remake events: the flight style an event sets on this player's game (/flightstyle [players] <free | original | player>,
// the event graphs' Set Flight Model node; NetAdmin.Order.Flight): FlightStyles.Current follows it while the event system
// runs here (a session or single player's quests, EventHost.ScreensActive), before the ship's, the mod campaign's and the
// player's own choice (Modding.ModFlight). Cleared by "player", the event's end (EventRunner) and the session's end.

namespace GoF2Remake.Events
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class EventFlight
    {
        static int style = -1;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => style = -1;

        /// <summary>The style an event sets (Data.FlightStyles.Original / Free), -1 = none.</summary>
        public static int Forced => style >= 0 && EventHost.ScreensActive ? style : -1;

        internal static void Apply(int value) => style = value is 0 or 1 ? value : -1;
    }
}
