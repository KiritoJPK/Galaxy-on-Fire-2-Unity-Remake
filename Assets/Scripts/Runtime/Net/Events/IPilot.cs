// IPilot.cs
// A player as the event system, the chat commands' selectors and the admin orders see one: in a multiplayer session a
// NetPlayer, in single player the one LocalPilot (EventHost.Pilots). Only what EventRunner / NetCommands / NetAdmin read.

using UnityEngine;
using GoF2Remake.Multiplayer;

namespace GoF2Remake.Events
{
    public interface IPilot
    {
        /// <summary>The player's id (a client id; 0 for the single player's pilot).</summary>
        ulong OwnerClientId { get; }
        string DisplayName { get; }
        bool InSpace { get; }
        bool InHangar { get; }
        float Hull { get; }
        int Station { get; }
        Vector3 Position { get; }
        bool IsSpawned { get; }
        bool IsAdmin { get; }
        int SquadId { get; }
        NetPlayer.Place Where { get; }
        Quaternion Rotation { get; }
        int ShipIndex { get; }
        /// <summary>This game's own player.</summary>
        bool IsOwner { get; }
    }
}
