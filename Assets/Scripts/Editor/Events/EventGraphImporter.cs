// EventGraphImporter.cs
// Imports a .gof2event graph as a TextAsset of the file's own text: Resources.Load finds a graph under
// Resources/GoF2Events and EventRunner reads it like a graph in a server's Events folder (EventGraphFile, EventGraphScript),
// so a built-in event is that one file, in the Editor and in builds.
// Each import also reads the file the way the server does and compares the script with Graph Toolkit's view of the graph
// (EventGraphCompiler): a difference means Graph Toolkit's file format changed (a Unity update) and EventGraphFile needs
// updating. The graph's problems and a script EventRunner can't run are import warnings.

using System.Collections.Generic;
using System.IO;
using GoF2Remake.Multiplayer;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;
using GoF2Remake.Events;

namespace GoF2Remake.EditorTools
{
    [ScriptedImporter(2, new[] { EventGraph.Extension, EventRunner.LegacyExtension })]
    public class EventGraphImporter : ScriptedImporter
    {
        // The files Database.Load reads.
        static readonly string[] DataFiles =
            { "ships", "items", "systems", "stations", "assemblies", "weapons_hd", "wanted", "item_attributes", "economy_default" };

        public override void OnImportAsset(AssetImportContext ctx)
        {
            // The checks look names up in the game data (EventNames -> Database.Load): import those files first, or a
            // fresh Library (a Unity upgrade) checked every graph against empty tables.
            foreach (string data in DataFiles) ctx.DependsOnArtifact("Assets/Resources/GoF2Data/" + data + ".json");
            string file = File.ReadAllText(ctx.assetPath);
            string name = Path.GetFileName(ctx.assetPath);
            var graph = GraphDatabase.LoadGraphForImporter<EventGraph>(ctx.assetPath);
            if (name.Contains(" ")) ctx.LogImportWarning($"{name}: /event takes one word: rename it without spaces (e.g. my_event).");
            if (graph != null && graph.NodeCount > 0)   // a new graph is saved once before its Start node is added
            {
                var problems = new List<EventGraphScript.Problem>();
                string script = EventGraphScript.FromFile(file, problems);
                foreach (var p in problems) ctx.LogImportWarning($"{name}: {(p.node != null ? p.node + ": " : "")}{p.message}");
                string error = EventRunner.Check(script);
                if (error != null) ctx.LogImportWarning($"{name}: the script doesn't compile: {error}");
                // The Editor's own reading is only a check: a graph it can't read (an option type Graph Toolkit can't resolve)
                // still imports, or the built-in events were missing from the game.
                string editor;
                try { editor = EventGraphCompiler.Compile(graph); }
                catch (System.Exception e)
                {
                    ctx.LogImportWarning($"{name}: Graph Toolkit can't read this graph ({(e.InnerException ?? e).Message}); the game still can.");
                    editor = script;
                }
                if (editor != script)
                    ctx.LogImportWarning($"{name}: the game reads this graph differently from the Editor (Graph Toolkit's file format changed?): " +
                                         $"update EventGraphFile.\nEditor:\n{editor}\nGame:\n{script}");
            }
            var asset = new TextAsset(file) { name = Path.GetFileNameWithoutExtension(ctx.assetPath) };
            ctx.AddObjectToAsset("graph", asset);
            ctx.SetMainObject(asset);
        }
    }
}
