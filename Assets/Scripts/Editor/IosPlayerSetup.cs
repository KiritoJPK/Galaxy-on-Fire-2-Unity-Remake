// IosPlayerSetup.cs  (Editor only)
// The iOS player's settings and Info.plist keys, set from code like IosUpscalerDefine so they also land while an Editor has
// the project open (on every Editor load and before an iOS build; each setting only while it still has Unity's default, so a
// choice made in Player settings stays):
//   bundle id            com.joppietoppie.gof2remake (Android's; was the URP template's com.Unity-Technologies...)
//   home indicator       auto-hidden (prefersHomeIndicatorAutoHidden): it fades out while nothing touches the screen's
//                        bottom edge, the touch controls sit just above it
//   system gestures      deferred on the top and bottom edges (preferredScreenEdgesDeferringSystemGestures): a swipe from
//                        an edge during flight (the stick, the fire cluster, a throttle drag) shows the grabber first
//                        instead of opening the Notification / Control Center or leaving the game
// The safe area itself (the notch / Dynamic Island on the left or right in landscape, the home indicator's strip at the
// bottom) is kept clear at runtime by UI.SafeArea.
// Info.plist (after the Xcode project is written):
//   CFBundleDisplayName  "GoF2 Remake" on the home screen, the Android launcher's label (AndroidAppLabel). Not through
//                        PlayerSettings.iOS.applicationDisplayName: that is the product name itself, which names the desktop
//                        save folder and PlayerPrefs
//   UIFileSharingEnabled + LSSupportsOpeningDocumentsInPlace   the game's Documents folder (persistentDataPath: Saves, Mods,
//                        the Transfer folder, Screenshots, ship_presets.json) in the Files app ("On My iPhone"), so mods
//                        and save transfers work without a computer
//   CADisableMinimumFrameDurationOnPhone   ProMotion iPhones run above 60 fps (without it iOS caps a phone app at 60:
//                        the frame rate option's V-Sync took 60 as the display's rate)
//   NSLocalNetworkUsageDescription   iOS 14+ asks before an app talks to the local network: without the text a LAN game
//                        (hosting, joining by address) fails silently
//   NSPhotoLibraryAddUsageDescription   Action Freeze / Inspect ship screenshots into the photo library (PhotoLibrary.mm)
//   LSApplicationCategoryType action games + GCSupportsGameMode   iOS 18's Game Mode (fewer background tasks, lower Bluetooth
//                        latency for controllers and AirPods)
//   GCSupportsControllerUserInteraction + GCSupportedGameControllers (ExtendedGamepad)   the game takes a controller's
//                        input as its own (the menus are navigated with one)

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    [InitializeOnLoad]
    public class IosPlayerSetup : IPreprocessBuildWithReport
    {
        public const string BundleId = "com.joppietoppie.gof2remake";
        /// <summary>The home screen's name, AndroidAppLabel.Label (that file needs the Android module).</summary>
        public const string DisplayName = "GoF2 Remake";
        const string TemplateBundlePrefix = "com.Unity-Technologies";

        static IosPlayerSetup() => EditorApplication.delayCall += () => Ensure();

        public int callbackOrder => -1900;   // after IosUpscalerDefine

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.iOS) Ensure();
        }

        /// <summary>Sets what is still at Unity's default; true if anything changed.</summary>
        static bool Ensure()
        {
            bool changed = false;
            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (string.IsNullOrEmpty(id) || id.StartsWith(TemplateBundlePrefix))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                changed = true;
            }
            if (!PlayerSettings.iOS.hideHomeButton)
            {
                PlayerSettings.iOS.hideHomeButton = true;
                changed = true;
            }
            if (PlayerSettings.iOS.deferSystemGesturesMode == UnityEngine.iOS.SystemGestureDeferMode.None)
            {
                PlayerSettings.iOS.deferSystemGesturesMode =
                    UnityEngine.iOS.SystemGestureDeferMode.TopEdge | UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
                changed = true;
            }
            if (changed) Debug.Log("IosPlayerSetup: iOS player settings set (bundle id, home indicator, edge gestures).");
            return changed;
        }

#if UNITY_IOS
        [PostProcessBuild(100)]
        static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = System.IO.Path.Combine(path, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetString("CFBundleDisplayName", DisplayName);
            root.SetBoolean("UIFileSharingEnabled", true);
            root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
            root.SetString("NSLocalNetworkUsageDescription",
                "Multiplayer on your local network: hosting a game and joining one by its address.");
            root.SetString("NSPhotoLibraryAddUsageDescription", "Saves your Action Freeze screenshots to your photo library.");
            root.SetString("LSApplicationCategoryType", "public.app-category.action-games");
            root.SetBoolean("GCSupportsGameMode", true);
            root.SetBoolean("GCSupportsControllerUserInteraction", true);
            var controllers = root.CreateArray("GCSupportedGameControllers");
            controllers.AddDict().SetString("ProfileName", "ExtendedGamepad");
            plist.WriteToFile(plistPath);
            Debug.Log("IosPlayerSetup: Info.plist keys written (home screen name, Files app access, ProMotion, local network, photos, Game Mode, controllers).");
        }
#endif
    }
}
