// QuestGraph.cs
// Remake-only: a .gof2quest as a node graph, the way the Mod Builder's graph editor shows it (ModBuilderQuestGraph): every
// step a node, the flow wires its order. A step's "Next" output is the step after it in its list; a block's outputs (If's
// Then / Else, an answer's steps, a branch...) are its own lists; the Start node's output is the quest's "steps". Like Graph
// Toolkit's event graphs a node is reached by one wire only, so the tree is the graph. Chains wired to nothing are kept in
// "loose" (lists of steps; they never run) and every node's place in "pos": [x, y] (the Start node's "startPos").
// Value nodes ("values": [{ "id", "type", fields, "pos" }]: a number, the game's state, a variable, maths, a comparison, and /
// or / not, random, the players, an orbit...) wire into a step's field or another value node's input: the field holds
// { "$value": id } and QuestFile writes the expression the nodes make (Resolve). Plain C#.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GoF2Remake.Events
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // read-only tables
    public static class QuestGraph
    {
        public const string Next = "next", Start = "start";

        // ---- flow ports ---------------------------------------------------------------------------------------

        /// <summary>A step's flow outputs: Next, then its blocks ("then", "else", "steps", "none") and its answers / branches
        /// ("answer:0", "branch:1"), with a label.</summary>
        public static List<(string port, string label)> Outputs(JObject step)
        {
            var list = new List<(string, string)> { (Next, "Next") };
            var t = QuestSteps.Get((string)step["type"] ?? "");
            if (t == null) return list;
            if (t.answers && step["answers"] is JArray answers)
                for (int k = 0; k < answers.Count; k++)
                    list.Add(("answer:" + k, $"{k + 1}. {(string)answers[k]?["text"] ?? ""}".TrimEnd('.', ' ')));
            if (t.branches && step["branches"] is JArray branches)
                for (int k = 0; k < branches.Count; k++)
                    list.Add(("branch:" + k, $"Branch {k + 1} ({branches[k]?["weight"] ?? 1})"));
            foreach (var b in t.blocks) list.Add((b.key, b.english));
            return list;
        }

        /// <summary>The list behind a block output (null for Next).</summary>
        public static JArray ListOf(JObject step, string port, bool create = true)
        {
            if (port == Next) return null;
            JObject holder = step;
            string key = port;
            if (port.StartsWith("answer:") || port.StartsWith("branch:"))
            {
                var arr = step[port.StartsWith("answer:") ? "answers" : "branches"] as JArray;
                int k = int.Parse(port.Substring(7), CultureInfo.InvariantCulture);
                if (arr == null || k < 0 || k >= arr.Count || !(arr[k] is JObject h)) return null;
                holder = h;
                key = "steps";
            }
            if (holder[key] is JArray list) return list;
            if (!create) return null;
            list = new JArray();
            holder[key] = list;
            return list;
        }

        public static JArray Root(JObject quest) => quest["steps"] as JArray ?? (JArray)(quest["steps"] = new JArray());

        /// <summary>The chains wired to nothing: lists of steps.</summary>
        public static JArray Loose(JObject quest) => quest["loose"] as JArray ?? (JArray)(quest["loose"] = new JArray());

        /// <summary>Every chain that starts a column of nodes: the quest's own, then the loose ones.</summary>
        public static IEnumerable<JArray> Chains(JObject quest)
        {
            yield return Root(quest);
            if (quest["loose"] is JArray loose)
                foreach (var c in loose.OfType<JArray>()) yield return c;
        }

        /// <summary>Every step of the graph, nested and loose ones too.</summary>
        public static IEnumerable<JObject> AllSteps(JObject quest)
        {
            foreach (var chain in Chains(quest).ToList())
                foreach (var s in Walk(chain)) yield return s;
        }

        static IEnumerable<JObject> Walk(JArray list)
        {
            foreach (var st in list.OfType<JObject>().ToList())
            {
                yield return st;
                foreach (var (_, child) in QuestSteps.Children(st))
                    foreach (var s in Walk(child)) yield return s;
            }
        }

        /// <summary>Where a step's input wire comes from: the step before it ("next"), the block that holds its list (its
        /// port), the Start node (null, "start"), or nothing (a loose chain's first: null, null).</summary>
        public static (JObject from, string port) Incoming(JObject step)
        {
            if (!(step.Parent is JArray list)) return (null, null);
            int i = list.IndexOf(step);
            if (i > 0) return ((JObject)list[i - 1], Next);
            return OwnerOf(list);
        }

        /// <summary>What a list hangs from: a block step and its port, the Start node, or nothing (loose).</summary>
        public static (JObject from, string port) OwnerOf(JArray list)
        {
            if (list.Parent is JProperty prop && prop.Parent is JObject holder)
            {
                if (prop.Name == "steps" && holder["format"] != null) return (null, Start);   // the quest itself
                if (holder["type"] != null) return (holder, prop.Name);
                // An answer's / branch's steps: holder is the answer, inside the step's "answers" / "branches".
                if (holder.Parent is JArray arr && arr.Parent is JProperty ap && ap.Parent is JObject step)
                    return (step, (ap.Name == "answers" ? "answer:" : "branch:") + arr.IndexOf(holder));
            }
            return (null, null);
        }

        /// <summary>The step wired to an output (null: none).</summary>
        public static JObject Target(JObject quest, JObject from, string port)
        {
            if (from == null) return Root(quest).FirstOrDefault() as JObject;
            if (port == Next)
            {
                if (!(from.Parent is JArray list)) return null;
                int i = list.IndexOf(from);
                return i + 1 < list.Count ? list[i + 1] as JObject : null;
            }
            return ListOf(from, port, false)?.FirstOrDefault() as JObject;
        }

        /// <summary>A step and the steps after it in its list: the chain a wire into it carries.</summary>
        static List<JToken> ChainFrom(JObject step)
        {
            var list = (JArray)step.Parent;
            return list.Skip(list.IndexOf(step)).ToList();
        }

        /// <summary>Takes the chain from 'step' on out of its list (a loose list left empty goes).</summary>
        static List<JToken> TakeChain(JObject quest, JObject step)
        {
            var chain = ChainFrom(step);
            var list = (JArray)step.Parent;
            foreach (var t in chain) t.Remove();
            if (list.Count == 0 && list.Parent is JArray loose && loose == quest["loose"]) list.Remove();
            return chain;
        }

        static void AddLooseChain(JObject quest, List<JToken> chain)
        {
            if (chain.Count == 0) return;
            // Out of their list first: a JArray made of tokens that still have a parent holds copies of them.
            foreach (var t in chain) if (t.Parent != null) t.Remove();
            var list = new JArray();
            foreach (var t in chain) list.Add(t);
            Loose(quest).Add(list);
        }

        /// <summary>'inner' is 'outer' or inside one of its blocks.</summary>
        static bool Inside(JObject inner, JObject outer)
        {
            for (JToken t = inner; t != null; t = t.Parent)
                if (t == outer) return true;
            return false;
        }

        /// <summary>Wires an output to a step: the step and the ones after it in its list move there; what the output led to
        /// before goes loose. False when that would wire a block into itself.</summary>
        public static bool Connect(JObject quest, JObject from, string port, JObject to)
        {
            if (to == null || to == from || !(to.Parent is JArray)) return false;
            var chainSteps = ChainFrom(to).OfType<JObject>().ToList();
            if (from != null && chainSteps.Any(c => Inside(from, c))) return false;
            if (from != null && QuestSteps.Get((string)to["type"] ?? "")?.topOnly == true && port != Next) return false;
            var chain = TakeChain(quest, to);
            if (from == null)
            {
                var root = Root(quest);
                AddLooseChain(quest, root.ToList());
                root.RemoveAll();
                foreach (var t in chain) root.Add(t);
                return true;
            }
            if (port == Next)
            {
                var list = (JArray)from.Parent;
                int i = list.IndexOf(from);
                var after = list.Skip(i + 1).ToList();
                foreach (var t in after) t.Remove();
                AddLooseChain(quest, after);
                foreach (var t in chain) list.Add(t);
                return true;
            }
            var target = ListOf(from, port);
            if (target == null) return false;
            AddLooseChain(quest, target.ToList());
            target.RemoveAll();
            foreach (var t in chain) target.Add(t);
            return true;
        }

        /// <summary>Cuts the wire out of an output: what it led to goes loose.</summary>
        public static void Cut(JObject quest, JObject from, string port)
        {
            var to = Target(quest, from, port);
            if (to != null) AddLooseChain(quest, TakeChain(quest, to));
        }

        /// <summary>Cuts the wire into a step: it and the steps after it go loose.</summary>
        public static void Detach(JObject quest, JObject step)
        {
            if (Incoming(step).port == null && step.Parent is JArray l && l.IndexOf(step) == 0) return;   // already loose
            AddLooseChain(quest, TakeChain(quest, step));
        }

        /// <summary>A new step on its own (loose).</summary>
        public static void AddLoose(JObject quest, JObject step)
        {
            if (step.Parent != null) step.Remove();
            var list = new JArray();
            list.Add(step);
            Loose(quest).Add(list);
        }

        /// <summary>Removes a step: the steps after it close up behind the one before it; its blocks' steps go loose.</summary>
        public static void Delete(JObject quest, JObject step)
        {
            foreach (var (_, child) in QuestSteps.Children(step).ToList())
                if (child.Count > 0)
                {
                    var steps = child.ToList();
                    child.RemoveAll();
                    AddLooseChain(quest, steps);
                }
            var list = step.Parent as JArray;
            step.Remove();
            if (list != null && list.Count == 0 && list.Parent is JArray loose && loose == quest["loose"]) list.Remove();
        }

        /// <summary>No empty loose chains; no "loose" key without any.</summary>
        public static void Tidy(JObject quest)
        {
            if (!(quest["loose"] is JArray loose)) return;
            foreach (var c in loose.ToList()) if (!(c is JArray a) || a.Count == 0) c.Remove();
            if (loose.Count == 0) quest.Remove("loose");
            if (quest["values"] is JArray v && v.Count == 0) quest.Remove("values");
        }

        // ---- places -------------------------------------------------------------------------------------------

        public static bool HasPos(JObject o, string key = "pos") => o[key] is JArray p && p.Count >= 2;

        public static (float x, float y) Pos(JObject o, string key = "pos") =>
            o[key] is JArray p && p.Count >= 2 ? ((float)p[0], (float)p[1]) : (0f, 0f);

        public static void SetPos(JObject o, float x, float y, string key = "pos") => o[key] = new JArray(Math.Round(x), Math.Round(y));

        /// <summary>Places the nodes without a place (all of them with 'all'): each chain a column going down, a block's
        /// lists to its right; the loose chains right of the rest. 'height' is a node's height on screen.</summary>
        public static void Layout(JObject quest, bool all, Func<JObject, float> height, float width = 260f, float gap = 40f)
        {
            if (all || !HasPos(quest, "startPos")) SetPos(quest, 0, 0, "startPos");
            var (sx, sy) = Pos(quest, "startPos");
            float right = sx + width;
            float Column(JArray list, float x, float y)
            {
                foreach (var st in list.OfType<JObject>())
                {
                    if (all || !HasPos(st)) SetPos(st, x, y);
                    var (px, py) = Pos(st);
                    right = Math.Max(right, px + width);
                    float bottom = py + height(st);
                    float cy = py;
                    foreach (var (_, child) in QuestSteps.Children(st))
                    {
                        if (child.Count == 0) continue;
                        cy = Column(child, px + width + gap, cy) + gap;
                    }
                    y = Math.Max(bottom + gap, cy);
                }
                return y;
            }
            Column(Root(quest), sx, sy + 110f);
            if (quest["loose"] is JArray loose)
            {
                float x = right + gap * 2;
                foreach (var chain in loose.OfType<JArray>())
                {
                    var first = chain.FirstOrDefault() as JObject;
                    float y0 = first != null && HasPos(first) && !all ? Pos(first).y : sy;
                    float r0 = right;
                    Column(chain, x, y0);
                    x = Math.Max(x + width + gap, right + gap);
                    right = Math.Max(right, r0);
                }
            }
            if (quest["values"] is JArray values)
            {
                float vy = sy;
                foreach (var v in values.OfType<JObject>())
                    if (all || !HasPos(v)) { SetPos(v, sx - 240f, vy); vy += 90f; }
            }
        }

        // ---- value nodes --------------------------------------------------------------------------------------

        /// <summary>What a value node's output gives: a number (expressions, conditions), players (a selector) or an orbit.</summary>
        public enum ValueKind { Number, Players, Orbit }

        public sealed class ValueType
        {
            public string type, english;
            public ValueKind output;
            /// <summary>Its inputs (wirable, else typed): key, kind of value, label, default.</summary>
            public (string key, ValueKind kind, string label, string def)[] inputs = new (string, ValueKind, string, string)[0];
            /// <summary>Its options (typed only): key, label, the choices (empty: text).</summary>
            public (string key, string label, string[] choices, string def)[] options = new (string, string, string[], string)[0];
            internal Func<Func<string, string>, Func<string, string>, string> write;
        }

        static readonly string[] States = { "players", "inspace", "docked", "dead", "enemies", "ships", "time", "kills", "credits", "rank", "station", "system",
                                            "ship", "campaign", "systemsvisited", "missionstation", "toppoints" };

        static (string, ValueKind, string, string) In(string key, string label, string def = "0", ValueKind kind = ValueKind.Number) => (key, kind, label, def);

        public static readonly List<ValueType> ValueTypes = new List<ValueType>
        {
            new ValueType { type = "number", english = "Number", options = new[] { ("value", "Value", new string[0], "0") }, write = (i, o) => Num(o("value")) },
            new ValueType { type = "state", english = "Game state", options = new[] { ("what", "What", States, "players") }, write = (i, o) => o("what") },
            new ValueType { type = "variable", english = "Variable", options = new[] { ("name", "Name", new string[0], "x") }, write = (i, o) => o("name").ToLowerInvariant() },
            new ValueType { type = "math", english = "Math", inputs = new[] { In("a", "A"), In("b", "B") },
                options = new[] { ("op", "Operation", new[] { "+", "-", "*", "/", "%" }, "+") }, write = (i, o) => $"({i("a")} {o("op")} {i("b")})" },
            new ValueType { type = "compare", english = "Compare", inputs = new[] { In("a", "A"), In("b", "B") },
                options = new[] { ("op", "Is", new[] { "==", "!=", "<", "<=", ">", ">=" }, ">=") }, write = (i, o) => $"({i("a")} {o("op")} {i("b")})" },
            new ValueType { type = "logic", english = "And / Or", inputs = new[] { In("a", "A", "1"), In("b", "B", "1") },
                options = new[] { ("op", "Both / either", new[] { "and", "or" }, "and") }, write = (i, o) => $"({i("a")} {o("op")} {i("b")})" },
            new ValueType { type = "not", english = "Not", inputs = new[] { In("a", "Value", "0") }, write = (i, o) => $"(not {i("a")})" },
            new ValueType { type = "random", english = "Random", inputs = new[] { In("min", "From", "1"), In("max", "To", "10") }, write = (i, o) => $"random({i("min")}, {i("max")})" },
            new ValueType { type = "minmax", english = "Min / Max", inputs = new[] { In("a", "A"), In("b", "B") },
                options = new[] { ("op", "Which", new[] { "min", "max" }, "min") }, write = (i, o) => $"{o("op")}({i("a")}, {i("b")})" },
            new ValueType { type = "floor", english = "Round down", inputs = new[] { In("a", "Value") }, write = (i, o) => $"floor({i("a")})" },
            new ValueType { type = "count", english = "Count players", inputs = new[] { In("players", "Players", "@a", ValueKind.Players) }, write = (i, o) => $"count({i("players")})" },
            new ValueType { type = "alive", english = "Ships alive", options = new[] { ("ship", "Named", new string[0], "Raider") }, write = (i, o) => $"alive({o("ship")})" },
            new ValueType { type = "distance", english = "Ship's distance", options = new[] { ("ship", "Named", new string[0], "Raider"), ("at", "To x y z", new string[0], "0 0 0") },
                write = (i, o) => $"distance({o("ship")}, {string.Join(", ", (o("at") ?? "0 0 0").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))})" },
            new ValueType { type = "cargo", english = "Cargo of an item", options = new[] { ("item", "Item", new string[0], "22") }, write = (i, o) => $"cargo({o("item")})" },
            new ValueType { type = "docked", english = "Docked at", inputs = new[] { In("station", "Station", "78", ValueKind.Orbit) }, write = (i, o) => $"docked({i("station")})" },
            new ValueType { type = "inorbit", english = "In the orbit of", inputs = new[] { In("station", "Station", "78", ValueKind.Orbit) }, write = (i, o) => $"inorbit({i("station")})" },
            new ValueType { type = "visited", english = "Has visited", inputs = new[] { In("station", "Station", "78", ValueKind.Orbit) }, write = (i, o) => $"visited({i("station")})" },
            new ValueType { type = "quest", english = "A quest's state", options = new[] { ("name", "Quest", new string[0], "my_quest") }, write = (i, o) => $"quest({o("name")})" },
            new ValueType { type = "expression", english = "Expression", options = new[] { ("text", "Expression", new string[0], "0") }, write = (i, o) => "(" + (o("text") ?? "0") + ")" },
            new ValueType { type = "players", english = "Get players", output = ValueKind.Players, inputs = new[] { In("orbit", "In the orbit of (optional)", "", ValueKind.Orbit) },
                options = new[] { ("who", "Who", new[] { "a", "alive", "space", "docked", "dead", "survivors", "r", "p" }, "alive") },
                write = (i, o) => "@" + o("who") + (i("orbit") is var orb && orb.Length > 0 ? $"[orbit={orb}]" : "") },
            new ValueType { type = "orbit", english = "Get orbit", output = ValueKind.Orbit, options = new[] { ("station", "Station", new string[0], "78") }, write = (i, o) => o("station") },
        };

        public static ValueType GetValueType(string type) => ValueTypes.FirstOrDefault(v => v.type == type);

        static string Num(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d.ToString("0.###", CultureInfo.InvariantCulture) : "0";

        public static JArray Values(JObject quest) => quest["values"] as JArray ?? (JArray)(quest["values"] = new JArray());

        public static JObject Value(JObject quest, string id) => (quest["values"] as JArray)?.OfType<JObject>().FirstOrDefault(v => (string)v["id"] == id);

        public static JObject NewValue(JObject quest, string type)
        {
            int n = 1;
            while (Value(quest, "v" + n) != null) n++;
            var v = new JObject { ["id"] = "v" + n, ["type"] = type };
            var t = GetValueType(type);
            if (t != null) foreach (var opt in t.options) v[opt.key] = opt.def;
            Values(quest).Add(v);
            return v;
        }

        /// <summary>A field wired to a value node: its id (else null).</summary>
        public static string WiredValue(JToken field) => field is JObject o && o["$value"] is JValue v ? (string)v : null;

        public static JObject Wire(string valueId) => new JObject { ["$value"] = valueId };

        /// <summary>Removes a value node and every wire out of it (the fields go back to empty).</summary>
        public static void DeleteValue(JObject quest, JObject value)
        {
            string id = (string)value["id"];
            value.Remove();
            foreach (var o in quest.Descendants().OfType<JObject>().Where(o => WiredValue(o) == id).ToList())
            {
                if (o.Parent is JProperty p) p.Remove();
            }
        }

        /// <summary>The expression a value node makes (a number, a selector or a station), "" with a problem when it can't (a
        /// loop, a missing node).</summary>
        public static string Resolve(JObject quest, string id, Action<string> problem, HashSet<string> seen = null)
        {
            seen ??= new HashSet<string>();
            var v = Value(quest, id);
            if (v == null) { problem($"a wire leads to the missing value node {id}."); return "0"; }
            if (!seen.Add(id)) { problem("value nodes wired into themselves."); return "0"; }
            var t = GetValueType((string)v["type"] ?? "");
            if (t == null) { problem($"unknown value node \"{v["type"]}\"."); return "0"; }
            string Input(string key)
            {
                var def = t.inputs.FirstOrDefault(x => x.key == key);
                var tok = v[key];
                string wired = WiredValue(tok);
                if (wired != null) return Resolve(quest, wired, problem, new HashSet<string>(seen));
                string s = tok is JValue jv && jv.Type != JTokenType.Null ? jv.ToString(CultureInfo.InvariantCulture).Trim() : def.def ?? "";
                if (def.kind == ValueKind.Number && s.Length == 0) s = "0";
                return s.Replace("#", "").Replace("{", "").Replace("}", "");
            }
            string Option(string key) => v[key] is JValue jv && jv.Type != JTokenType.Null ? jv.ToString(CultureInfo.InvariantCulture).Trim().Replace("#", "").Replace("{", "").Replace("}", "")
                                       : t.options.FirstOrDefault(x => x.key == key).def;
            return t.write(Input, Option);
        }
    }
}
