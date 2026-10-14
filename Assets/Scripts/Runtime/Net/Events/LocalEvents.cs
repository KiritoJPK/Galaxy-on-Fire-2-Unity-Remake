// LocalEvents.cs
// Single player's event runner (EventHost.Local): a hidden DontDestroyOnLoad object that ticks EventRunner every frame
// without a session (in a session NetState.Update ticks it on the server). Made with the first scene and kept for the
// run (scenes change under it). In a game scene (Space, Station; not a session's game) it also starts a loaded save's
// quests and bar mission again from their checkpoints (EventRunner.RestoreLocal) and, while the game isn't paused, the quest
// graphs whose start condition holds (EventRunner.CheckQuestStarts).

using UnityEngine;
using UnityEngine.SceneManagement;
using GoF2Remake.Multiplayer;

namespace GoF2Remake.Events
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class LocalEvents : MonoBehaviour
    {
        static LocalEvents instance;

        /// <summary>Single player runs an event now.</summary>
        public static bool Running => instance != null && EventHost.Local && EventRunner.AnyRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => Ensure();

        public static void Ensure()
        {
            if (instance != null || !EventHost.Local) return;
            var go = new GameObject("Local events") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LocalEvents>();
        }

        static bool InGameScene
        {
            get
            {
                if (NetGame.SessionGame || Data.Session.EndingPending) return false;
                string scene = SceneManager.GetActiveScene().name;
                return scene == "Space" || scene == "Station";
            }
        }

        void Update()
        {
            if (!EventHost.Local) return;   // a session started: its server ticks
            if (InGameScene)
            {
                if (EventRunner.RestorePending) EventRunner.RestoreLocal();
                if (Time.timeScale > 0f) EventRunner.CheckQuestStarts();
            }
            EventRunner.Tick();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
