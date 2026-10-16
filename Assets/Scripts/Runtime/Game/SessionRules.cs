// SessionRules.cs
// The gameplay rules a multiplayer session decides for everyone (NetRules, GoF2.Net): their bits, here so the options
// (Settings) can ask for the session's value through GameHooks.SessionRule.

namespace GoF2Remake.Data
{
    public static class SessionRules
    {
        public const int CloakLosesPursuers = 1, PirateEvents = 2, CapitalShips = 4, InformerOriginalRule = 8,
                         KaamoStacking = 16, KaamoKeepsEquipment = 32, CloakBay = 64, EmpBreaksCloak = 128;

        /// <summary>Remake: the highest level of one Kaamo Club upgrade on a ship while stacking is on (Settings.KaamoMaxLevel,
        /// Session.MaxModLevel); a number, so the session carries it beside the bits (NetRules.HostKaamoLevel). 5 by default; 10 was the
        /// players' feedback of 2026-10 (45 equipment slots on a Cronus uncapped); 1 would be stacking off.</summary>
        public const int DefaultKaamoLevel = 5, MinKaamoLevel = 2, MaxKaamoLevel = 20;

        public static int ClampKaamoLevel(int level) => level < MinKaamoLevel ? MinKaamoLevel : level > MaxKaamoLevel ? MaxKaamoLevel : level;

        /// <summary>Remake: the flight model a session flies (NetRules.HostFlight): FlightStyles.Original ("gof2", the game's,
        /// the default: players flying the other one in PvP had other turn rates, strafing and drift), FlightStyles.Free
        /// ("es2", EVERSPACE 2 style) for everyone, or PlayerFlight ("player") = each player's own option.</summary>
        public const int PlayerFlight = -1, DefaultFlight = FlightStyles.Original;

        /// <summary>A flight model's name ("gof2" / "original", "es2" / "free", "player" / "any" / "choice"), -2 = none of them.</summary>
        public static int ParseFlight(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "gof2": case "original": case "0": return FlightStyles.Original;
                case "free": case "es2": case "everspace": case "1": return FlightStyles.Free;
                case "player": case "any": case "choice": case "-1": return PlayerFlight;
                default: return -2;
            }
        }

        public static string FlightText(int style) => style == FlightStyles.Free ? "es2" : style == PlayerFlight ? "player" : "gof2";
    }
}
