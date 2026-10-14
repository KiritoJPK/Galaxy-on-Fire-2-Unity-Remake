using System;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Netcode.Tracing;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Netcode.Editor.Tracing.UI
{
    internal class TracingWindowUtility
    {
        // Opens the source declaring the type (system or component); the fallback regex matches class and struct.
        internal static void OpenScriptInIde(Type systemType)
        {
            if (systemType == null)
            {
                UnityEngine.Debug.LogWarning("Could not open script: Type is null.");
                return;
            }

            // Generic types' Type.Name includes a backtick arity suffix (e.g. "MySystem`1") that
            // never appears in source. Strip it so AssetDatabase / regex search find the file.
            var searchName = StripGenericArity(systemType.Name);

            // 1. Try the fast approach first (works if the file name matches the type name).
            // MonoScript.GetClass returns the open generic definition for generic types, so a closed
            // generic systemType won't equal it directly — compare against the generic type definition too.
            var guids = AssetDatabase.FindAssets($"t:MonoScript {searchName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                    continue;
                var scriptClass = script.GetClass();
                if (scriptClass == null)
                    continue;
                if (scriptClass == systemType
                    || (systemType.IsGenericType && systemType.GetGenericTypeDefinition() == scriptClass))
                {
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }

            // 2. Fallback: the system is likely a secondary type inside another file.
            var assemblyName = systemType.Assembly.GetName().Name;
            var regex = new Regex($@"\b(?:class|struct)\s+{Regex.Escape(searchName)}\b");

            foreach (var unityAsm in CompilationPipeline.GetAssemblies())
            {
                if (unityAsm.name != assemblyName)
                    continue;

                foreach (var sourceFilePath in unityAsm.sourceFiles)
                {
                    string text;
                    try
                    {
                        text = File.ReadAllText(sourceFilePath);
                    }
                    catch (IOException)
                    {
                        // File temporarily locked or otherwise unreadable — skip it rather than
                        // aborting the whole scan.
                        continue;
                    }
                    // Cheap pre-filter: skip files that don't mention the system name at all
                    // before paying the regex cost.
                    if (!text.Contains(searchName))
                        continue;
                    var match = regex.Match(text);
                    if (!match.Success)
                        continue;

                    var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(sourceFilePath);
                    if (scriptAsset == null)
                        continue;

                    AssetDatabase.OpenAsset(scriptAsset, CountLines(text, match.Index));
                    return;
                }
            }

            UnityEngine.Debug.LogWarning($"Could not find the script file containing type: {systemType.FullName}");
        }

        static string StripGenericArity(string name)
        {
            var backtick = name.IndexOf('`');
            return backtick >= 0 ? name.Substring(0, backtick) : name;
        }

        static int CountLines(string text, int upTo)
        {
            var line = 1;
            for (var i = 0; i < upTo; i++)
            {
                if (text[i] == '\n')
                    line++;
            }
            return line;
        }

        internal static bool IsTracingRunning =>
            EditorApplication.isPlaying && !EditorApplication.isPaused && TracingDataAccess.Config.Data.EnableTracing;

        public static void SetHidden(VisualElement element, bool shouldHide)
        {
            element?.EnableInClassList(TracingWindowUssClasses.Hidden, shouldHide);
        }
    }
}
