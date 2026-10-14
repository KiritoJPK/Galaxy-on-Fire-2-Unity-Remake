// EventsTestUtil.cs
// Helpers shared by the event-graph tests: building an EventGraphData in memory, the script's lines without their "#@"
// node markers, and the English text table for tests that compare names.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Events;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    internal static class EventsTestUtil
    {
        /// <summary>A node with its stored values (port / option name -> invariant text).</summary>
        public static EventGraphData.Node Node(EventGraphData g, string id, string kind, params (string key, string value)[] values)
        {
            var n = new EventGraphData.Node { id = id, kind = kind };
            foreach (var (k, v) in values) n.values[k] = v;
            g.nodes.Add(n);
            return n;
        }

        public static void Wire(EventGraphData g, string fromNode, string fromPort, string toNode, string toPort) =>
            g.wires.Add(new EventGraphData.Wire { fromNode = fromNode, fromPort = fromPort, toNode = toNode, toPort = toPort });

        /// <summary>A graph with a Start node "start" (no description).</summary>
        public static EventGraphData WithStart(params (string key, string value)[] startValues)
        {
            var g = new EventGraphData();
            var values = new List<(string, string)> { ("Description", "") };
            values.AddRange(startValues);
            Node(g, "start", "EventStartNode", values.ToArray());
            return g;
        }

        /// <summary>The script's lines: comments and "#@" markers off, trimmed, blanks dropped.</summary>
        public static List<string> Lines(string script)
        {
            var list = new List<string>();
            foreach (string raw in (script ?? "").Replace("\r", "").Split('\n'))
            {
                int hash = raw.IndexOf('#');
                string line = (hash >= 0 ? raw.Substring(0, hash) : raw).Trim();
                if (line.Length > 0) list.Add(line);
            }
            return list;
        }

        /// <summary>The first word of every line ("wait", "spawn", "mission"...).</summary>
        public static HashSet<string> Words(string script) =>
            new HashSet<string>(Lines(script).Select(l => l.Split(' ')[0].ToLowerInvariant()));

        public static List<EventGraphScript.Problem> Errors(List<EventGraphScript.Problem> problems) => problems.Where(p => !p.warning).ToList();

        public static string Describe(IEnumerable<EventGraphScript.Problem> problems) =>
            string.Join("; ", problems.Select(p => (p.node ?? "graph") + ": " + p.message));

        /// <summary>The tests comparing names take the English table (the Editor may run in another language).</summary>
        public static void RequireEnglish()
        {
            Localization.Get(0);
            if (!string.Equals(Localization.Language, "en", StringComparison.OrdinalIgnoreCase))
                Assert.Ignore("needs the English text table (Localization.Language is " + Localization.Language + ")");
        }
    }
}
