// NoEntitiesBootstrap.cs
// Netcode for GameObjects 3.0 brings Netcode for Entities (and Entities) as a hard dependency, whose automatic bootstrap
// (ClientServerBootstrap) would create an Entities host world at startup and tick its systems every frame, single player
// included. The remake doesn't use Entities, and NGO only needs them in its unified mode (UNIFIED_NETCODE, not set):
// this bootstrap (the project's own, so Entities picks it over the package's) creates no client / server world, only the
// default world Entities insists on (World.DefaultGameObjectInjectionWorld), empty: no systems, not in the player loop.

namespace GoF2Remake.Multiplayer
{
    [UnityEngine.Scripting.Preserve]
    public sealed class NoEntitiesBootstrap : Unity.Netcode.ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            Unity.Entities.World.DefaultGameObjectInjectionWorld = new Unity.Entities.World(defaultWorldName, Unity.Entities.WorldFlags.Game);
            return true;
        }
    }
}
