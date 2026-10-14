// NetDelayedShutdown.cs
// Multiplayer: the host leaving with players connected (NetGame.Shutdown) first tells them why (NetState.SessionEndingRpc),
// then this closes the session a moment later (on the NetworkManager's GameObject, which lives in DontDestroyOnLoad), so
// the message is out before the connection goes; and quits the game afterwards when that was the reason (closing the
// window).

using System.Collections;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    public sealed class NetDelayedShutdown : MonoBehaviour
    {
        public void Run(float seconds, bool quit) => StartCoroutine(After(seconds, quit));

        IEnumerator After(float seconds, bool quit)
        {
            yield return new WaitForSecondsRealtime(seconds);
            NetGame.ShutdownNow();
            if (quit) Application.Quit();
        }
    }
}
