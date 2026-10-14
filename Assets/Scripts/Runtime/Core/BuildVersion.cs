// BuildVersion.cs
// The version shown in the main menu's credit line: the date and time of the git commit the player was built from
// (yyyy.MM.dd.HHmm, UTC; the build's own local time without git), so every platform of one commit shows the same, stamped into PlayerSettings.bundleVersion by the Editor's BuildVersionStamp for each build, so Application.version
// (and Android's versionName) carries it. In the Editor: "editor".
// Fingerprint: what multiplayer compares instead (two builds of the same code play together, whenever they were built):
// the hash of the code and data the Editor's BuildFingerprint wrote into Resources/GoF2Build for the build; a build
// without it (older) falls back to its version.
// Text is what the main menu's credit line, /version in the chat, the multiplayer window and the dedicated server's log
// show (no git branch / commit: joining a game compares the fingerprint, not the commit).

using UnityEngine;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class BuildVersion
    {
        public const string Format = "yyyy.MM.dd.HHmm";
        public const string FingerprintResource = "GoF2Build/BuildFingerprint";

        static string fingerprint;

        public static string Text => Application.isEditor ? "editor" : Application.version;

        /// <summary>The code's fingerprint ("editor" in the Editor): the same for every build of the same code.</summary>
        public static string Fingerprint
        {
            get
            {
                if (Application.isEditor) return "editor";
                if (fingerprint == null)
                {
                    var asset = Resources.Load<TextAsset>(FingerprintResource);
                    string text = asset != null ? asset.text.Trim() : "";
                    fingerprint = text.Length > 0 ? text : Application.version;
                }
                return fingerprint;
            }
        }
    }
}
