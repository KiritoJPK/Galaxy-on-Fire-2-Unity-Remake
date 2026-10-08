// AndroidModImportActivity.cs
// The mod browser's "Import mod" on Android opens ModImportActivity (Assets/Plugins/Android/ModImportActivity.java,
// Modding.ModImport): an activity has to be in the manifest, so the generated Gradle project's unityLibrary manifest
// gets it here, see-through and outside the game's task history.

using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    public class AndroidModImportActivity : IPostGenerateGradleAndroidProject
    {
        const string Activity = "com.joppietoppie.gof2remake.ModImportActivity";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // 'path' is the unityLibrary module.
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest))
            {
                Debug.LogWarning("AndroidModImportActivity: no manifest at " + manifest + ", Import mod won't work");
                return;
            }
            string text = File.ReadAllText(manifest);
            if (text.Contains("\"" + Activity + "\"")) return;
            int end = text.IndexOf("</application>", System.StringComparison.Ordinal);
            if (end < 0)
            {
                Debug.LogWarning("AndroidModImportActivity: no <application> in the manifest, Import mod won't work");
                return;
            }
            text = text.Insert(end, "    <activity android:name=\"" + Activity + "\" android:exported=\"false\"\n"
                                  + "              android:theme=\"@android:style/Theme.Translucent.NoTitleBar\"\n"
                                  + "              android:excludeFromRecents=\"true\"\n"
                                  + "              android:configChanges=\"orientation|screenSize|screenLayout|keyboardHidden|uiMode\" />\n  ");
            File.WriteAllText(manifest, text);
        }
    }
}
