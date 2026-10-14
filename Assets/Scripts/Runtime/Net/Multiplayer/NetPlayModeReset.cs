// NetPlayModeReset.cs
// Editor only: this project enters Play mode without a domain reload, and Netcode for GameObjects' code generation adds
// its 25 message types to a static list (ILPPMessageProvider.__network_message_types) in a RuntimeInitializeOnLoad
// method on every Play start. Without the reload the list doubles on the second Play, and hosting or joining then fails
// ("Allowed types is not equal to the number of message type indices! Allowed Count: 50 | Index Count: 25"). This clears
// it first (SubsystemRegistration runs before the generated AfterSceneLoad method). Builds always start fresh: not
// compiled there.

#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    public static class NetPlayModeReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearNetcodeMessageTypes()
        {
            var type = typeof(Unity.Netcode.NetworkManager).Assembly.GetType("Unity.Netcode.ILPPMessageProvider");
            var field = type?.GetField("__network_message_types", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field?.GetValue(null) is IList list) list.Clear();
        }
    }
}
#endif
