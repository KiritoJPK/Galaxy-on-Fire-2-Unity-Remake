// NetServerFixture.cs
// A dedicated server's profile folder in the test sandbox: accounts.json (and optionally factions.json / bans.json)
// written as the server keeps them, NetProfiles configured on it and started (which loads the factions and the bans too),
// and everything those static classes keep put back afterwards (their ResetStatics, by reflection).

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GoF2Remake.Multiplayer;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    internal static class NetServerFixture
    {
        public struct Pilot
        {
            public string id, name, device;
            public int role;
            public Pilot(string id, string name, int role, string device = null) { this.id = id; this.name = name; this.role = role; this.device = device; }
        }

        public static string Folder(string sandbox) => Path.Combine(sandbox, "ServerProfiles");

        /// <summary>Writes the accounts (and the factions / bans JSON when given) and starts NetProfiles on them.</summary>
        public static string Start(string sandbox, IEnumerable<Pilot> pilots, string factionsJson = null, string bansJson = null)
        {
            string folder = Folder(sandbox);
            Directory.CreateDirectory(folder);
            var sb = new StringBuilder("{\"serverId\":\"testserver\",\"accounts\":[");
            bool first = true;
            string now = DateTime.UtcNow.ToString("o");
            foreach (var p in pilots)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":\"").Append(p.id).Append("\",\"name\":\"").Append(p.name).Append("\",\"squad\":\"\",\"created\":\"").Append(now)
                  .Append("\",\"lastSeen\":\"").Append(now).Append("\",\"devices\":[");
                if (p.device != null) sb.Append("{\"device\":\"").Append(p.device).Append("\",\"tokenHash\":\"x\",\"added\":\"").Append(now).Append("\"}");
                sb.Append("],\"worth\":0,\"playSeconds\":0,\"arenaKills\":0,\"arenaDeaths\":0,\"arenaWins\":0,\"role\":").Append(p.role).Append('}');
            }
            sb.Append("]}");
            File.WriteAllText(Path.Combine(folder, "accounts.json"), sb.ToString());
            if (factionsJson != null) File.WriteAllText(Path.Combine(folder, "factions.json"), factionsJson);
            if (bansJson != null) File.WriteAllText(Path.Combine(folder, "bans.json"), bansJson);
            NetProfiles.Configure(true, NetProfiles.DefaultEarnPerMinute, folder);
            NetProfiles.Start();
            return folder;
        }

        public static void Stop()
        {
            Reset(typeof(NetModeration));
            Reset(typeof(NetFactions));
            Reset(typeof(NetProfiles));
        }

        public static void Reset(Type t)
        {
            var m = t.GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, t.Name + ".ResetStatics");
            m.Invoke(null, null);
        }
    }
}
