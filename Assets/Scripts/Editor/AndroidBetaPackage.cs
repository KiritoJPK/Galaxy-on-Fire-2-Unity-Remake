// AndroidBetaPackage.cs
// Fork change: an Android build that installs beside the published one. Android keeps apps apart by package: a game built
// here under the release's package (com.joppietoppie.gof2remake) but signed with another key can't update the installed
// one and has to replace it (uninstall first, its data with it). With GoF2 > Build > Android Beta Package ticked (EditorPrefs,
// this machine only) or GOF2_ANDROID_BETA=1 in the Editor's environment (command-line builds), an Android build gets the
// package com.joppietoppie.gof2remake.beta and the launcher label "GoF2 Beta" (AndroidAppLabel), so both stay installed.
// The beta has its own data (Android/data/<package>/files: saves, settings, mods, the Transfer folder). Like
// BuildVersionStamp, the package is set before the build and put back once it has finished or failed, so the project
// settings keep the release's.

using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GoF2Remake.EditorTools
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class AndroidBetaPackage : IPreprocessBuildWithReport
    {
        public const string Suffix = ".beta";
        public const string Label = "GoF2 Beta";
        public const string EnvironmentVariable = "GOF2_ANDROID_BETA";
        const string PrefKey = "GoF2.AndroidBetaPackage";
        const string MenuPath = "GoF2/Build/Android Beta Package";

        // After BuildVersionStamp.
        public int callbackOrder => int.MinValue + 2;

        /// <summary>The next Android build is a beta: the menu item or the environment variable.</summary>
        public static bool Enabled => EditorPrefs.GetBool(PrefKey, false) || Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

        /// <summary>The build under way is a beta (AndroidAppLabel reads it when the Gradle project is generated).</summary>
        public static bool ThisBuild { get; private set; }

        [MenuItem(MenuPath, priority = 231)]
        static void Toggle() => EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, false));

        [MenuItem(MenuPath, true)]
        static bool ToggleShown()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, false));
            return true;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            ThisBuild = report.summary.platform == BuildTarget.Android && Enabled;
            if (!ThisBuild) return;
            var target = NamedBuildTarget.Android;
            string saved = PlayerSettings.GetApplicationIdentifier(target);
            string beta = saved.EndsWith(Suffix, StringComparison.Ordinal) ? saved : saved + Suffix;
            PlayerSettings.SetApplicationIdentifier(target, beta);
            UnityEngine.Debug.Log($"GoF2: Android beta build: package {beta}, launcher label \"{Label}\" (installs beside {saved})");
            // The build saves the project settings with the beta package in them: put the release's back and save again.
            EditorApplication.delayCall += () =>
            {
                PlayerSettings.SetApplicationIdentifier(target, saved);
                ThisBuild = false;
                AssetDatabase.SaveAssets();
            };
        }
    }
}
