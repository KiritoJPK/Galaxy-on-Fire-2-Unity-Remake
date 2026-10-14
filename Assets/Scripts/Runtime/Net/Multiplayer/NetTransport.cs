// NetTransport.cs
// Unity Transport with a byte count for the network stats (NetStats, the chat's /netstats): what Netcode hands it to send
// (after Netcode's own batching) and the messages it delivers, Netcode's payloads without the transport's or Relay's
// headers. Everything else is UnityTransport's.

using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace GoF2Remake.Multiplayer
{
    public sealed class NetTransport : UnityTransport
    {
        public override void Initialize(NetworkManager networkManager = null)
        {
            OnTransportEvent -= CountIn;
            OnTransportEvent += CountIn;
            base.Initialize(networkManager);
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            NetStats.CountOut(payload.Count);
            base.Send(clientId, payload, networkDelivery);
        }

        static void CountIn(NetworkEvent type, ulong clientId, ArraySegment<byte> payload, float receiveTime)
        {
            if (type == NetworkEvent.Data) NetStats.CountIn(payload.Count);
        }
    }
}
