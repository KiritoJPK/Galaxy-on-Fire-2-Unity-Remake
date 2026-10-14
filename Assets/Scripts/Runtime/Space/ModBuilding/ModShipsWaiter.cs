// ModShipsWaiter.cs
// Runs an action once the mods' ship models are built (ModShips.Ready), or after a time limit: entering a game scene
// waits for them, so no mod ship stands in as the Phantom (ModShips.WhenReady). A hidden DontDestroyOnLoad object per
// wait, gone when it has run.

using System;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public class ModShipsWaiter : MonoBehaviour
    {
        Action action;
        float deadline;

        public static void Run(Action a, float timeoutSeconds)
        {
            if (ModShips.Ready) { a(); return; }
            var go = new GameObject("Waiting for mod ships") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            var w = go.AddComponent<ModShipsWaiter>();
            w.action = a;
            w.deadline = Time.realtimeSinceStartup + timeoutSeconds;
        }

        void Update()
        {
            bool timedOut = Time.realtimeSinceStartup > deadline;
            if (!ModShips.Ready && !timedOut) return;
            if (timedOut) Debug.LogWarning("Mods: the ship models took too long; going on without them");
            var a = action;
            action = null;
            Destroy(gameObject);
            a?.Invoke();
        }
    }
}
