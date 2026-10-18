// EventGraphUpgrade.cs
// The event graphs began as multiplayer events: their files were .gof2netevent and their option enums lived in
// GoF2Remake.Multiplayer (Graph Toolkit stores an enum option by its type's name, m_EnumType.m_Identification). Any such
// file imported into the project (an old graph, one copied in from a server's Events folder or a mod) is renamed to
// .gof2event (AssetDatabase.MoveAsset: the same GUID, so references stay) with the enum types' new namespace,
// GoF2Remake.Events, so Graph Toolkit opens it. The game itself reads both extensions (EventRunner.LegacyExtension) and
// never looks at those type names.
// The type names also carry their assembly: the graphs saved before the layered assemblies name Assembly-CSharp, where the
// enums no longer are (GoF2.Net), so Graph Toolkit couldn't resolve them and the import threw "Type provided must be an
// Enum" (hidden while the old import results were cached; a platform switch's reimport showed it, 2026-10-17). Every
// imported graph naming an enum type's assembly wrongly gets the right one written in (FixAssemblies) and is imported again.

using System.IO;
using System.Text.RegularExpressions;
using GoF2Remake.Events;
using UnityEditor;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    public class EventGraphUpgrade : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                string p = path;
                if (path.EndsWith("." + EventRunner.LegacyExtension, System.StringComparison.OrdinalIgnoreCase))
                    EditorApplication.delayCall += () => Upgrade(p);
                else if (path.EndsWith("." + EventGraph.Extension, System.StringComparison.OrdinalIgnoreCase))
                    EditorApplication.delayCall += () => Reassemble(p);
            }
        }

        // "GoF2Remake.Events.EventMathOp, Assembly-CSharp," (or the old namespace) as Graph Toolkit stores an enum's type.
        static readonly Regex EnumType = new(@"GoF2Remake\.(?:Events|Multiplayer)\.(Event\w+), ([\w.\-]+),");

        /// <summary>'text' with every option enum type naming its namespace and assembly as they are now; types that don't
        /// exist (any more) are left as they are.</summary>
        public static string FixAssemblies(string text)
        {
            string assembly = typeof(EventMathOp).Assembly.GetName().Name;
            return EnumType.Replace(text, m =>
                typeof(EventMathOp).Assembly.GetType("GoF2Remake.Events." + m.Groups[1].Value) is { IsEnum: true }
                    ? $"GoF2Remake.Events.{m.Groups[1].Value}, {assembly}," : m.Value);
        }

        static void Reassemble(string path)
        {
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path), fixedText = FixAssemblies(text);
            if (fixedText == text) return;   // nothing to fix: no import loop
            File.WriteAllText(path, fixedText);
            AssetDatabase.ImportAsset(path);
            Debug.Log($"GoF2: event graph {path}: its option types now name the {typeof(EventMathOp).Assembly.GetName().Name} assembly");
        }

        static void Upgrade(string path)
        {
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            string target = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, "." + EventGraph.Extension).Replace('\\', '/'));
            // The new type names first: MoveAsset imports the file at its new path at once.
            File.WriteAllText(path, FixAssemblies(text.Replace("GoF2Remake.Multiplayer.Event", "GoF2Remake.Events.Event")));
            string error = AssetDatabase.MoveAsset(path, target);
            if (!string.IsNullOrEmpty(error)) { Debug.LogWarning($"GoF2: couldn't rename {path} to {target}: {error}"); return; }
            AssetDatabase.ImportAsset(target);
            Debug.Log($"GoF2: event graph {path} renamed to {target} (the .gof2event format)");
        }
    }
}
