// AndroidAppLabel.cs
// The Android launcher shows "GoF2 Remake", not the product name "Galaxy on Fire 2" (the original's app name: with both
// installed they looked the same; the package id com.joppietoppie.gof2remake already differs). Only the generated Gradle
// project's app_name string changes: the product name stays, so the desktop builds keep their save folder, PlayerPrefs and
// window title. A beta build (AndroidBetaPackage, its own package beside the release) is "GoF2 Beta".

using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;

namespace GoF2Remake.EditorTools
{
    public class AndroidAppLabel : IPostGenerateGradleAndroidProject
    {
        public const string Label = "GoF2 Remake";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // 'path' is the unityLibrary module; the launcher module (the app's label) sits beside it. Every strings.xml with
            // app_name gets the label.
            var root = Directory.GetParent(path)?.FullName ?? path;
            foreach (var file in Directory.GetFiles(root, "strings.xml", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                string label = AndroidBetaPackage.ThisBuild ? AndroidBetaPackage.Label : Label;
                string changed = Regex.Replace(text, "(<string name=\"app_name\">)[^<]*(</string>)", "${1}" + label + "${2}");
                if (changed != text) File.WriteAllText(file, changed);
            }
        }
    }
}
