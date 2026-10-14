// NetStats.cs
// The network stats the chat's /netstats shows over the flight HUD and the station menu (ChatView): this game's role and
// connection (host / client, Relay or direct), the ping, its jitter and the packet loss (the client's connection to the
// host, Unity Transport's ConnectionStatistics; the host: each player's ping), the data in / out per second and in all
// (NetTransport's byte count: Netcode's payloads, without the transport's or Relay's headers) and the players. Whether
// it shows is remembered (PlayerPrefs "mp_netstats").

using System.Text;
using GoF2Remake.Data;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetStats
    {
        const float RateWindowSeconds = 1f;
        const int MaxPingRows = 3;   // the host's per-player pings (the panel stays clear of the station's pilot list)

        static long bytesIn, bytesOut, windowIn, windowOut;
        static float windowStart = -1f, inPerSecond, outPerSecond;

        // Play mode without a domain reload keeps statics.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        /// <summary>Shown over the HUD and the station menu while a session runs (/netstats).</summary>
        public static bool Shown
        {
            get => PlayerPrefs.GetInt("mp_netstats", 0) == 1;
            set => PlayerPrefs.SetInt("mp_netstats", value ? 1 : 0);
        }

        /// <summary>A new session: the counts start over.</summary>
        public static void Reset()
        {
            bytesIn = bytesOut = windowIn = windowOut = 0;
            windowStart = -1f;
            inPerSecond = outPerSecond = 0f;
        }

        public static void CountIn(int bytes) => bytesIn += bytes;
        public static void CountOut(int bytes) => bytesOut += bytes;

        /// <summary>The per-second rates, over windows of at least RateWindowSeconds (real time).</summary>
        static void Sample()
        {
            float now = Time.unscaledTime;
            if (windowStart < 0f) { windowStart = now; windowIn = bytesIn; windowOut = bytesOut; return; }
            float dt = now - windowStart;
            if (dt < RateWindowSeconds) return;
            inPerSecond = (bytesIn - windowIn) / dt;
            outPerSecond = (bytesOut - windowOut) / dt;
            windowStart = now;
            windowIn = bytesIn;
            windowOut = bytesOut;
        }

        static string X(string key, string english) => Localization.Extra(key, english);

        static string Rate(float bytesPerSecond) => bytesPerSecond < 1024f ? $"{bytesPerSecond:0} B/s" : $"{bytesPerSecond / 1024f:0.0} KB/s";
        static string Total(long bytes) => bytes < 1024 * 1024 ? $"{bytes / 1024f:0.0} KB" : $"{bytes / (1024f * 1024f):0.00} MB";

        /// <summary>The overlay's text (rich text), null outside a session.</summary>
        public static string Text()
        {
            var manager = NetworkManager.Singleton;
            if (!NetGame.Active || manager == null || !(manager.NetworkConfig.NetworkTransport is UnityTransport transport)) return null;
            Sample();
            var sb = new StringBuilder();
            sb.Append("<b>").Append(X("mpStatsTitle", "NETWORK")).Append("</b>\n");
            bool relay = transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport;
            string link = relay ? X("mpStatsRelay", "Relay") : X("mpStatsDirect", "direct");
            string role = manager.IsHost ? X("mpStatsHost", "Host")
                : NetState.Instance != null && NetState.Instance.Dedicated ? X("mpStatsClientDedicated", "Client, dedicated server")
                : X("mpStatsClient", "Client");
            sb.Append(role).Append(" · ").Append(link).Append('\n');

            if (manager.IsHost)
            {
                // The host: each player's round trip (Unity Transport's RTT per connection), the first MaxPingRows.
                int others = 0;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p.IsOwner) continue;
                    if (others++ < MaxPingRows)
                        sb.Append(string.Format(X("mpStatsPlayerPing", "{0}: {1} ms"), p.DisplayName, transport.GetCurrentRtt(p.OwnerClientId))).Append('\n');
                }
                if (others > MaxPingRows) sb.Append(string.Format(X("mpStatsMorePlayers", "+{0} more"), others - MaxPingRows)).Append('\n');
                if (others == 0) sb.Append(X("mpStatsNoPlayers", "No other players")).Append('\n');
            }
            else
            {
                // The client's one connection, to the host: ping (smoothed RTT), its spread and the packet loss.
                ref var driver = ref transport.GetNetworkDriver();
                ulong id = transport.ServerClientId;
                if (driver.IsCreated)
                {
                    var connection = UnsafeUtility.As<ulong, NetworkConnection>(ref id);
                    var stats = driver.GetConnectionStatistics(connection);
                    float ping = stats.Latency.SmoothedCurrent > 0f ? stats.Latency.SmoothedCurrent : transport.GetCurrentRtt(manager.NetworkConfig.NetworkTransport.ServerClientId);
                    sb.Append(string.Format(X("mpStatsPing", "Ping {0:0} ms (jitter {1:0} ms)"), ping, stats.Latency.StandardDeviation)).Append('\n');
                    sb.Append(string.Format(X("mpStatsLoss", "Packet loss {0:0.0} %"), stats.PacketLossPercent)).Append('\n');
                }
            }

            sb.Append(string.Format(X("mpStatsIn", "In {0} ({1})"), Rate(inPerSecond), Total(bytesIn))).Append('\n');
            sb.Append(string.Format(X("mpStatsOut", "Out {0} ({1})"), Rate(outPerSecond), Total(bytesOut))).Append('\n');
            sb.Append(string.Format(X("mpStatsPlayers", "Players {0}"), NetPlayer.All.Count));
            return sb.ToString();
        }
    }
}
