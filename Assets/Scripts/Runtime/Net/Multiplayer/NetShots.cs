// NetShots.cs
// Multiplayer shot targets (homing missiles, beams) travel as NetworkObject ids:
//   an orbit's NPC ship = its NetProxy (its owner resolves it back to the NpcShip's Target, the others to the proxy's)
//   a player's ship     = that player's NetPlayer (resolved to the local player's own Target where that player is local)

using GoF2Remake.Flight;
using GoF2Remake.World;
using Unity.Netcode;

namespace GoF2Remake.Multiplayer
{
    public static class NetShots
    {
        public const ulong NoTarget = ulong.MaxValue;

        /// <summary>The id another player can resolve 'target' from (NoTarget = none / not shared).</summary>
        public static ulong TargetId(Target target)
        {
            if (target == null || !NetGame.Active) return NoTarget;
            var proxy = target.GetComponent<NetProxy>();
            if (proxy != null && proxy.IsSpawned) return proxy.NetworkObjectId;
            var player = target.GetComponent<NetPlayer>();
            if (player != null && player.IsSpawned) return player.NetworkObjectId;
            if (target.isPlayer)
            {
                var own = NetPlayer.Local;
                return own != null && own.IsSpawned ? own.NetworkObjectId : NoTarget;
            }
            var ship = target.GetComponent<NpcShip>();
            var shipProxy = ship != null && NetOrbit.Current != null ? NetOrbit.Current.ProxyOf(ship) : null;
            return shipProxy != null && shipProxy.IsSpawned ? shipProxy.NetworkObjectId : NoTarget;
        }

        /// <summary>The local Target for an id from TargetId, null = none.</summary>
        public static Target Resolve(ulong id)
        {
            if (id == NoTarget || NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return null;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj) || obj == null) return null;
            var proxy = obj.GetComponent<NetProxy>();
            if (proxy != null) return proxy.LocalTarget;
            var player = obj.GetComponent<NetPlayer>();
            return player != null ? player.LocalTarget : null;
        }
    }
}
