// SessionRules.cs
// The gameplay rules a multiplayer session decides for everyone (NetRules, GoF2.Net): their bits, here so the options
// (Settings) can ask for the session's value through GameHooks.SessionRule.

namespace GoF2Remake.Data
{
    public static class SessionRules
    {
        public const int CloakLosesPursuers = 1, PirateEvents = 2, CapitalShips = 4, InformerOriginalRule = 8,
                         KaamoStacking = 16, KaamoKeepsEquipment = 32, CloakBay = 64, EmpBreaksCloak = 128;
    }
}
