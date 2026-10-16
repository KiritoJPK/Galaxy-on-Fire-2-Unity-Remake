// QuestFile.cs
// Remake-only: a quest, bar mission or event made in the Mod Builder (events/<name>.gof2quest, Modding.ProjectQuest) turned
// into an event graph's script (EventRunner's line form, the same EventGraphScript writes from a node graph), so it runs like
// any other: a JSON tree of steps instead of nodes, for modders without the Unity Editor. Every step type, its fields and the
// plain steps' lines are QuestSteps'; this writes the header, the variables, the steps that need more than one line or a
// block (a conversation, a fight, If / While / Ask / Random ...) and walks the nested lists.
//   header    title, kind (quest | mission | event), startsWhen (quests), reward, offer / client / stations / players
//             (missions), variables [{ name, value, setting }] (a setting: /event name=value can change it)
//   steps     [{ "type": ..., fields }], blocks holding their own lists ("then", "else", "steps", answers[].steps,
//             branches[].steps)
// FromScript goes the other way: an event graph's script (a Unity-made .gof2event, EventGraphScript.FromFile) as steps, the
// lines it doesn't know as Command steps, so a graph opens in the builder and runs the same.
// Text never carries the script's own marks: '#' starts a comment there, '|' separates pages / answers; '{ }' stays only
// around a variable or the game's state ({kills}); they are replaced. Plain C#.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace GoF2Remake.Events
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // read-only tables
    public static class QuestFile
    {
        public const string Extension = "gof2quest";

        /// <summary>A .gof2quest file's text?</summary>
        public static bool Is(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.TrimStart();
            return t.StartsWith("{") && t.Contains("\"gof2quest\"");
        }

        /// <summary>The players a step speaks to by default: everyone the run covers (single player: the player; a bar
        /// mission: its team, NetCommands.Scope).</summary>
        const string Players = "@a";

        /// <summary>The words an expression may use besides the quest's variables (EventRunner.Eval).</summary>
        static readonly HashSet<string> StateWords = new HashSet<string>
        {
            "enemies", "ships", "time", "missionstation", "toppoints", "players", "inspace", "docked", "dead", "campaign", "credits",
            "rank", "station", "system", "ship", "kills", "systemsvisited", "random", "min", "max", "floor", "count", "alive", "distance",
            "cargo", "has", "visited", "quest", "option", "won", "inorbit", "and", "or", "not", "main", "valkyrie", "supernova",
        };

        public static bool IsName(string s) => !string.IsNullOrEmpty(s) && Regex.IsMatch(s, "^[A-Za-z_][A-Za-z0-9_]*$");

        /// <summary>The script, or "" with the problems (step numbers from 1, "3.2" inside a block).</summary>
        public static string Compile(string json, List<string> problems)
        {
            JObject o;
            try { o = JObject.Parse(json); }
            catch (Exception e) { problems.Add("can't read the quest file: " + e.Message); return ""; }
            return Compile(o, problems);
        }

        public static string Compile(JObject o, List<string> problems) => new Writer(o, problems).Run();

        sealed class Writer
        {
            readonly JObject o;
            readonly List<string> problems;
            readonly StringBuilder sb = new StringBuilder();
            readonly HashSet<string> names = new HashSet<string>(StateWords);
            int enemies, randoms, indent;
            bool ended;

            public Writer(JObject o, List<string> problems)
            {
                this.o = o;
                this.problems = problems;
            }

            void Line(string l) => sb.Append(' ', indent * 2).Append(l.TrimEnd()).Append('\n');

            string Clean(string s) => QuestFile.Clean(s, names);
            string Ref(JToken t) => t is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.String) ? Clean(v.ToString(CultureInfo.InvariantCulture)) : "";
            List<string> Refs(JToken t) => t is JArray a ? a.Select(Ref).Where(x => x.Length > 0).ToList() : new List<string>();
            /// <summary>A text the runner shows on several lines (an offer, an objective): its line breaks as \n.</summary>
            string Lines(string s) => Clean((s ?? "").Replace("\r", "").Replace("\n", "\\n"));

            public string Run()
            {
                string title = Clean(Str(o, "title"));
                string kind = Str(o, "kind").ToLowerInvariant();
                if (kind != "mission" && kind != "event") kind = "quest";
                int reward = Int(o, "reward", 0);
                foreach (var v in (o["variables"] as JArray ?? new JArray()).OfType<JObject>())
                    if (IsName(Str(v, "name"))) names.Add(Str(v, "name").ToLowerInvariant());
                CollectNames(o["steps"] as JArray);
                if (title.Length == 0 && kind != "event") problems.Add("The quest needs a title.");
                if (kind == "quest")
                {
                    if (title.Length > 0) Line("mission title " + title);
                    Line("mission kind quest");
                    string starts = Str(o, "startsWhen").Trim();
                    if (starts.Length > 0) Line("mission starts " + Expr(starts));
                    if (reward > 0) Line("mission reward " + reward);
                }
                else if (kind == "mission")
                {
                    Line("mission kind mission");
                    Line("mission title " + title);
                    string offer = Lines(Text(o, "offer"));
                    if (offer.Length > 0) Line("mission offer " + offer);
                    string client = Clean(Str(o, "client"));
                    if (client.Length > 0) Line("mission client " + client);
                    if (reward > 0) Line("mission reward " + reward);
                    int min = 1, max = 4;
                    if (o["players"] is JArray pl && pl.Count >= 2) { min = Math.Max(1, (int)pl[0]); max = Math.Max(min, (int)pl[1]); }
                    Line($"mission players {min} {max}");
                    var stations = Refs(o["stations"]);
                    if (stations.Count > 0) Line("mission stations " + string.Join(", ", stations));
                }
                foreach (var v in (o["variables"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    string name = Str(v, "name").ToLowerInvariant(), value = Expr(Str(v, "value"));
                    if (!IsName(name)) { problems.Add($"The variable \"{name}\": a name of letters, digits and _."); continue; }
                    if (value.Length == 0) value = "0";
                    Line((v["setting"] is JValue s && s.Type == JTokenType.Boolean && (bool)s ? "param " : "set ") + name + " = " + value);
                }
                Line("");
                Steps(o["steps"] as JArray ?? new JArray(), "", 0);
                if (!ended && kind != "event" && Bool(o, "autoComplete", true)) Line("complete");
                return sb.ToString();
            }

            /// <summary>The variables Set / Repeat steps name (so their {name} in a text stays an expression).</summary>
            void CollectNames(JArray steps)
            {
                if (steps == null) return;
                foreach (var st in steps.OfType<JObject>())
                {
                    string type = Str(st, "type");
                    if (type == "set" && IsName(Str(st, "variable"))) names.Add(Str(st, "variable").ToLowerInvariant());
                    if (type == "repeat" && IsName(Str(st, "as"))) names.Add(Str(st, "as").ToLowerInvariant());
                    foreach (var (_, list) in QuestSteps.Children(st)) CollectNames(list);
                }
            }

            void Steps(JArray steps, string prefix, int depth)
            {
                int n = 0;
                foreach (var t in steps)
                {
                    n++;
                    string at = prefix + n;
                    if (!(t is JObject st)) { problems.Add($"Step {at}: not a step."); continue; }
                    Step(st, at, depth);
                }
            }

            void Block(JArray list, string at, int depth)
            {
                indent++;
                Steps(list ?? new JArray(), at + ".", depth + 1);
                indent--;
            }

            void Step(JObject st, string at, int depth)
            {
                string type = Str(st, "type").ToLowerInvariant();
                var def = QuestSteps.Get(type);
                void Problem(string m) => problems.Add($"Step {at} ({def?.english ?? type}): {m}");
                if (def == null) { Problem($"unknown step \"{type}\"."); return; }
                if (def.topOnly && depth > 0) { Problem("only on the quest's own line of steps, not inside a block."); return; }
                var r = new QuestReader(st, def, Problem, Clean, id => QuestGraph.Resolve(o, id, Problem));
                string who = r.Who;
                if (def.line != null)
                {
                    string l = def.line(r);
                    if (!string.IsNullOrEmpty(l)) Line(l);
                    return;
                }
                switch (type)
                {
                    case "dialog":
                    {
                        var pages = new List<string>();
                        foreach (var p in (st["pages"] as JArray ?? new JArray()).OfType<JObject>())
                        {
                            string speaker = Clean(Str(p, "speaker")), text = Clean(Text(p, "text"));
                            if (text.Length == 0) continue;
                            if (speaker.Length == 0) speaker = "player";
                            pages.Add(speaker + " : " + text + Voice(p));
                        }
                        if (pages.Count == 0) { Problem("a conversation needs a page with text."); break; }
                        Line($"dialog {who} " + string.Join(" | ", pages));
                        break;
                    }
                    case "radio":
                    {
                        string speaker = Clean(Str(st, "speaker")), text = Clean(Text(st, "text"));
                        if (text.Length == 0) { Problem("a radio call needs a text."); break; }
                        Line($"radio {who} {(speaker.Length > 0 ? speaker : "player")} : {text}{Voice(st)}");
                        break;
                    }
                    case "objective":
                        Objective(Lines(Text(st, "text")), r.Ref("station"));
                        break;
                    case "goto":
                    {
                        string station = r.Ref("station");
                        if (station.Length == 0) { Problem("pick the station."); break; }
                        string obj = Clean(Text(st, "objective"));
                        if (obj.Length > 0) Objective(obj, station);
                        Line($"wait until {(Bool(st, "dock", true) ? "docked" : "inorbit")}({station}) > 0");
                        break;
                    }
                    case "fight":
                    {
                        string station = r.Ref("station");
                        var ships = (st["ships"] as JArray ?? new JArray()).OfType<JObject>().ToList();
                        if (ships.Count == 0) { Problem("add the ships to fight."); break; }
                        string name = Clean(Str(st, "name"));
                        if (name.Length == 0) name = "Quest Enemy " + (++enemies);
                        string obj = Clean(Text(st, "objective"));
                        if (obj.Length > 0) Objective(obj, station);
                        bool quiet = station.Length > 0 && Bool(st, "quiet", true);
                        if (quiet) Line("questorbit " + station);
                        Line(station.Length > 0 ? $"wait until inorbit({station}) > 0" : "wait until inspace > 0");
                        Line("wait 3");
                        string where = station.Length > 0 ? $"@alive[orbit={station}]" : "@alive";
                        foreach (var sh in ships)
                        {
                            string ship = Ref(sh["ship"]);
                            if (ship.Length == 0) { Problem("a ship isn't picked."); continue; }
                            int count = Math.Max(1, Math.Min(10, Int(sh, "count", 1)));
                            string race = Clean(Str(sh, "race")).ToLowerInvariant();
                            string behaviour = Clean(Str(sh, "behaviour")).ToLowerInvariant();
                            if (behaviour.Length == 0) behaviour = "enemy";
                            Line(QuestReader.Join("spawn", where, ship, race, count.ToString(CultureInfo.InvariantCulture), behaviour, "named", name));
                        }
                        Line("wait 2");
                        Line($"wait until alive({name}) == 0");
                        if (quiet) Line($"questorbit {station} off");
                        break;
                    }
                    case "cargo":
                    {
                        string item = r.Ref("item"), station = r.Ref("station");
                        if (item.Length == 0) { Problem("pick the item."); break; }
                        int amount = Math.Max(1, Int(st, "amount", 1));
                        string obj = Clean(Text(st, "objective"));
                        if (obj.Length > 0) Objective(obj, station);
                        Line($"wait until cargo({item}) >= {amount}" + (station.Length > 0 ? $" and docked({station}) > 0" : ""));
                        break;
                    }
                    case "reward":
                    {
                        var parts = new List<string>();
                        string credits = r.Arg("credits");
                        if (credits.Length > 0 && credits != "0") parts.Add(credits);
                        foreach (var it in (st["items"] as JArray ?? new JArray()).OfType<JObject>())
                        {
                            string item = Ref(it["item"]);
                            if (item.Length > 0) parts.Add(item + " " + Math.Max(1, Int(it, "amount", 1)));
                        }
                        if (parts.Count == 0) { Problem("a reward needs credits or items."); break; }
                        string rt = Clean(Text(st, "title"));
                        Line($"reward {who} " + string.Join(" + ", parts) + (rt.Length > 0 ? " | " + rt : ""));
                        break;
                    }
                    case "checkpoint":
                    {
                        string name = Clean(Str(st, "name")).Replace(' ', '_');
                        Line("checkpoint " + (name.Length > 0 ? name : "step" + at.Replace('.', '_')));
                        break;
                    }
                    case "startquest":
                    {
                        string q = Clean(Str(st, "quest"));
                        if (q.Length == 0 || q.Contains(" ")) { Problem("pick the quest."); break; }
                        Line("startquest " + q);
                        break;
                    }
                    case "complete":
                    {
                        string rw = Num(st, "reward");
                        string ct = Clean(Text(st, "title"));
                        Line(("complete " + rw).TrimEnd() + (ct.Length > 0 ? " | " + ct : ""));
                        if (depth == 0) ended = true;
                        break;
                    }
                    case "fail":
                        Line(("fail " + Clean(Text(st, "title"))).TrimEnd());
                        if (depth == 0) ended = true;
                        break;

                    // ---- blocks ----
                    case "if":
                    {
                        string c = Cond(r, Problem);
                        if (c == null) break;
                        Line("if " + c);
                        Block(st["then"] as JArray, at, depth);
                        if (st["else"] is JArray els && els.Count > 0) { Line("else"); Block(els, at + "e", depth); }
                        Line("end");
                        break;
                    }
                    case "while":
                    {
                        string c = Cond(r, Problem);
                        if (c == null) break;
                        Line("while " + c);
                        Block(st["steps"] as JArray, at, depth);
                        Line("end");
                        break;
                    }
                    case "repeat":
                    {
                        string count = r.N("count"), var = Str(st, "as").Trim().ToLowerInvariant();
                        if (count.Length == 0) count = "1";
                        if (var.Length > 0 && !IsName(var)) { Problem("the variable: letters, digits and _."); break; }
                        Line("repeat " + count + (var.Length > 0 ? " as " + var : ""));
                        Block(st["steps"] as JArray, at, depth);
                        Line("end");
                        break;
                    }
                    case "parallel":
                        Line("parallel");
                        Block(st["steps"] as JArray, at, depth);
                        Line("end");
                        break;
                    case "every":
                    {
                        string s = r.N("seconds");
                        Line("every " + (s.Length > 0 ? s : "10"));
                        Block(st["steps"] as JArray, at, depth);
                        Line("end");
                        break;
                    }
                    case "on":
                    {
                        string trigger = r.S("trigger"), station = r.Ref("station");
                        if (trigger == "near")
                        {
                            string xyz = r.Xyz("at");
                            if (station.Length == 0 || xyz.Length == 0) { Problem("the orbit and the point."); break; }
                            Line($"on near {station} {xyz} {(r.Arg("radius") is var rad && rad.Length > 0 ? rad : "500")}");
                        }
                        else Line(QuestReader.Join("on", trigger, station));
                        Block(st["steps"] as JArray, at, depth);
                        Line("end");
                        break;
                    }
                    case "ask":
                    case "vote":
                    {
                        string question = Clean(Text(st, "question")), speaker = Clean(Str(st, "speaker"));
                        var answers = (st["answers"] as JArray ?? new JArray()).OfType<JObject>().ToList();
                        if (question.Length == 0) { Problem("write the question."); break; }
                        if (answers.Count < 2 || answers.Count > 4) { Problem("2 to 4 answers."); break; }
                        var texts = answers.Select(a => Clean(Text(a, "text"))).ToList();
                        if (texts.Any(x => x.Length == 0)) { Problem("an answer has no text."); break; }
                        string sec = r.Arg("seconds");
                        Line(QuestReader.Join(type, who, sec.Length > 0 ? sec : "30") + " | " + (speaker.Length > 0 ? speaker + " : " : "") + question + " | " + string.Join(" | ", texts));
                        indent++;
                        for (int k = 0; k < answers.Count; k++)
                        {
                            if (!(answers[k]["steps"] is JArray list) || list.Count == 0) continue;
                            Line((type == "vote" ? "choice " : "answer ") + (k + 1));
                            Block(list, at + "." + (k + 1), depth);
                            Line("end");
                        }
                        if (type == "vote" && st["none"] is JArray none && none.Count > 0)
                        {
                            Line("choice 0");
                            Block(none, at + ".0", depth);
                            Line("end");
                        }
                        indent--;
                        Line("end");
                        break;
                    }
                    case "random":
                    {
                        var branches = (st["branches"] as JArray ?? new JArray()).OfType<JObject>().ToList();
                        if (branches.Count == 0) { Problem("add a branch."); break; }
                        var weights = branches.Select(b => Math.Max(0.0, b["weight"] is JValue w && (w.Type == JTokenType.Integer || w.Type == JTokenType.Float) ? (double)w : 1.0)).ToList();
                        if (branches.Count == 1) { Steps(branches[0]["steps"] as JArray ?? new JArray(), at + ".1.", depth + 1); break; }
                        string v = "_qr" + ++randoms;
                        string W(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
                        Line($"set {v} = random(1, {W(weights.Sum())})");
                        double sum = 0;
                        int opened = 0;
                        for (int k = 0; k < branches.Count; k++)
                        {
                            sum += weights[k];
                            var list = branches[k]["steps"] as JArray ?? new JArray();
                            if (k < branches.Count - 1)
                            {
                                Line($"if {v} <= {W(sum)}");
                                Block(list, at + "." + (k + 1), depth);
                                Line("else");
                                indent++;
                                opened++;
                            }
                            else Steps(list, at + "." + (k + 1) + ".", depth + 1);
                        }
                        for (int k = 0; k < opened; k++) { indent--; Line("end"); }
                        break;
                    }
                    default:
                        Problem($"unknown step \"{type}\".");
                        break;
                }
            }

            static string Cond(QuestReader r, Action<string> problem)
            {
                string c = r.E("condition");
                if (c.Length == 0) { problem("write the condition."); return null; }
                return c;
            }

            void Objective(string text, string station)
            {
                if (text.Length > 0) Line("objective " + text);
                Line("target " + (station.Length > 0 ? station : "off"));
            }

            string Voice(JObject p)
            {
                string v = Clean(Str(p, "voice"));
                return v.Length > 0 && !v.Contains(" ") ? " [voice " + v + "]" : "";
            }
        }

        static string Str(JObject o, string k) => o[k] is JValue v && v.Type != JTokenType.Null ? v.ToString(CultureInfo.InvariantCulture) : "";

        /// <summary>A text field: a string, or one per language ({"en": ...}): the player's language, else English.</summary>
        static string Text(JObject o, string k)
        {
            if (o[k] is JObject t)
            {
                string lang = Data.Localization.Language;
                return (string)(t[lang] ?? t["en"] ?? t.Properties().FirstOrDefault()?.Value) ?? "";
            }
            return Str(o, k);
        }

        static int Int(JObject o, string k, int d) => o[k] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (int)Math.Round((double)v) : d;
        static string Num(JObject o, string k) =>
            o[k] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) && (double)v >= 0 ? ((double)v).ToString("0.###", CultureInfo.InvariantCulture) : "";
        static bool Bool(JObject o, string k, bool d) => o[k] is JValue v && v.Type == JTokenType.Boolean ? (bool)v : d;

        /// <summary>A reference (a number or a key) as the script names it.</summary>
        static string Ref(JToken t) => t is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.String) ? Clean(v.ToString(CultureInfo.InvariantCulture)) : "";

        static List<string> Refs(JToken t) => t is JArray a ? a.Select(Ref).Where(x => x.Length > 0).ToList() : new List<string>();

        /// <summary>Text without the script's own marks (one line).</summary>
        public static string Clean(string s) => Clean(s, null);

        /// <summary>Text without the script's own marks; "{name}" / "{kills * 100}" stays when every word in it is one of
        /// these (the quest's variables, the game's state).</summary>
        public static string Clean(string s, HashSet<string> keep)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '#': b.Append("No."); break;
                    case '|': b.Append('/'); break;
                    case '{':
                    {
                        int close = s.IndexOf('}', i + 1);
                        if (keep != null && close > i + 1 && KeepsExpression(s.Substring(i + 1, close - i - 1), keep))
                        {
                            b.Append(s, i, close - i + 1);
                            i = close;
                        }
                        else b.Append('(');
                        break;
                    }
                    case '}': b.Append(')'); break;
                    case '\r': break;
                    case '\n': b.Append(' '); break;
                    default: b.Append(c); break;
                }
            }
            return b.ToString().Trim();
        }

        static bool KeepsExpression(string e, HashSet<string> keep)
        {
            if (e.IndexOfAny(new[] { '{', '|', '#', '"' }) >= 0) return false;
            if (!Regex.IsMatch(e, @"^[A-Za-z0-9_ +\-*/%().,<>=!]+$")) return false;
            foreach (Match m in Regex.Matches(e, "[A-Za-z_][A-Za-z0-9_]*"))
                if (!keep.Contains(m.Value.ToLowerInvariant())) return false;
            return Regex.IsMatch(e, "[A-Za-z_]");
        }

        /// <summary>An expression as written (one line; '#' would cut it).</summary>
        static string Expr(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Replace("#", "").Trim();

        // ---- an event graph's script as steps ------------------------------------------------------------------------

        /// <summary>A script (EventRunner's line form: an event graph's, EventGraphScript) as a .gof2quest's JSON: the header,
        /// the settings as variables, the blocks as nested lists, the lines that make a step of their own as that step and the
        /// rest as Command steps (they run as they did). Notes: what changed on the way.</summary>
        public static JObject FromScript(string script, string fallbackTitle, List<string> notes) => FromScript(script, fallbackTitle, notes, null);

        /// <summary>The same, each step placed where its node was ('places': node id -> x, y; the script's "#@id" markers say
        /// which node a line came from).</summary>
        public static JObject FromScript(string script, string fallbackTitle, List<string> notes, Dictionary<string, (float x, float y)> places)
        {
            var placed = new HashSet<string>();
            var o = new JObject { ["format"] = "gof2quest" };
            var variables = new JArray();
            var root = new JArray();
            // The open blocks: the list steps go into, what opened it ("if", "else", "ask" ...), its step.
            var open = new Stack<(JArray list, string kind, JObject step)>();
            var stray = new JArray();
            JArray Cur()
            {
                if (open.Count == 0) return root;
                if (open.Peek().list != null) return open.Peek().list;
                notes.Add("A line between a question and its answers was left out.");
                return stray;
            }
            string kind = "event";
            bool titled = false, sawStep = false;
            int commands = 0;
            var lines = (script ?? "").Replace("\r", "").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string raw = lines[n];
                int hash = raw.IndexOf('#');
                string comment = hash >= 0 && (hash + 1 >= raw.Length || raw[hash + 1] != '@') ? raw.Substring(hash + 1).Trim() : null;
                string line = (hash >= 0 ? raw.Substring(0, hash) : raw).Trim();
                if (line.Length == 0)
                {
                    if (!string.IsNullOrEmpty(comment)) Cur().Add(new JObject { ["type"] = "note", ["text"] = comment });
                    continue;
                }
                if (line.StartsWith("/")) line = line.Substring(1);
                int sp = line.IndexOf(' ');
                string word = (sp < 0 ? line : line.Substring(0, sp)).ToLowerInvariant();
                string rest = sp < 0 ? "" : line.Substring(sp + 1).Trim();
                string marker = hash >= 0 && hash + 1 < raw.Length && raw[hash + 1] == '@' ? raw.Substring(hash + 2).Trim() : null;
                JObject Add(JObject st)
                {
                    Cur().Add(st);
                    sawStep = true;
                    // The node's place, once per node (a node may write several lines: its first step takes it).
                    if (marker != null && places != null && places.TryGetValue(marker, out var p) && placed.Add(marker)) QuestGraph.SetPos(st, p.x, p.y);
                    return st;
                }
                JObject Command() { commands++; return Add(new JObject { ["type"] = "command", ["line"] = line }); }
                JObject Push(JObject st, string k, string listKey)
                {
                    Add(st);
                    var list = new JArray();
                    st[listKey] = list;
                    open.Push((list, k, st));
                    return st;
                }
                switch (word)
                {
                    case "mission":
                    {
                        int s2 = rest.IndexOf(' ');
                        string key = (s2 < 0 ? rest : rest.Substring(0, s2)).ToLowerInvariant(), v = s2 < 0 ? "" : rest.Substring(s2 + 1).Trim();
                        switch (key)
                        {
                            case "title": o["title"] = v; titled = true; break;
                            case "kind": kind = v.ToLowerInvariant() == "quest" ? "quest" : v.ToLowerInvariant() == "mission" ? "mission" : "event"; break;
                            case "starts": o["startsWhen"] = v; break;
                            case "reward": if (int.TryParse(v, out int rw)) o["reward"] = rw; else notes.Add("The reward \"" + v + "\" isn't a number: left out."); break;
                            case "offer": o["offer"] = v.Replace("\\n", "\n"); break;
                            case "client": o["client"] = v; break;
                            case "players":
                            {
                                var f = v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (f.Length >= 1 && int.TryParse(f[0], out int lo)) o["players"] = new JArray(lo, f.Length > 1 && int.TryParse(f[1], out int hi) ? hi : lo);
                                break;
                            }
                            case "stations":
                                o["stations"] = new JArray(v.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => int.TryParse(x, out int i) ? (JToken)i : x));
                                break;
                        }
                        break;
                    }
                    case "param":
                    {
                        int eq = rest.IndexOf('=');
                        if (eq > 0) variables.Add(new JObject { ["name"] = rest.Substring(0, eq).Trim(), ["value"] = rest.Substring(eq + 1).Trim(), ["setting"] = true });
                        break;
                    }
                    case "set":
                    {
                        int eq = rest.IndexOf('=');
                        string name = eq > 0 ? rest.Substring(0, eq).Trim() : "", value = eq > 0 ? rest.Substring(eq + 1).Trim() : "";
                        if (!sawStep && open.Count == 0 && IsName(name) && !name.StartsWith("_"))
                            variables.Add(new JObject { ["name"] = name, ["value"] = value });
                        else Add(new JObject { ["type"] = "set", ["variable"] = name, ["value"] = value });
                        break;
                    }
                    case "wait":
                        if (rest.StartsWith("until ", StringComparison.OrdinalIgnoreCase))
                        {
                            string cond = rest.Substring(6).Trim();
                            var st = new JObject { ["type"] = "waituntil" };
                            int t = cond.LastIndexOf(" timeout ", StringComparison.OrdinalIgnoreCase);
                            if (t >= 0) { st["timeout"] = NumOrText(cond.Substring(t + 9).Trim()); cond = cond.Substring(0, t).Trim(); }
                            st["condition"] = cond;
                            Add(st);
                        }
                        else Add(new JObject { ["type"] = "wait", ["seconds"] = NumOrText(rest) });
                        break;
                    case "if": Push(new JObject { ["type"] = "if", ["condition"] = rest, ["else"] = new JArray() }, "if", "then"); break;
                    case "else":
                        if (open.Count > 0 && open.Peek().kind == "if")
                        {
                            var (_, _, st) = open.Pop();
                            var list = new JArray();
                            st["else"] = list;
                            open.Push((list, "else", st));
                        }
                        else notes.Add($"Line {n + 1}: an else without its if, left out.");
                        break;
                    case "while": Push(new JObject { ["type"] = "while", ["condition"] = rest }, "while", "steps"); break;
                    case "repeat":
                    {
                        var st = new JObject { ["type"] = "repeat" };
                        int asAt = rest.LastIndexOf(" as ", StringComparison.OrdinalIgnoreCase);
                        st["count"] = NumOrText(asAt >= 0 ? rest.Substring(0, asAt).Trim() : rest);
                        if (asAt >= 0) st["as"] = rest.Substring(asAt + 4).Trim();
                        Push(st, "repeat", "steps");
                        break;
                    }
                    case "parallel": Push(new JObject { ["type"] = "parallel" }, "parallel", "steps"); break;
                    case "every": Push(new JObject { ["type"] = "every", ["seconds"] = NumOrText(rest) }, "every", "steps"); break;
                    case "on":
                    {
                        var w = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var st = new JObject { ["type"] = "on", ["trigger"] = w.Length > 0 ? w[0].ToLowerInvariant() : "died" };
                        if (w.Length > 0 && w[0].ToLowerInvariant() == "near" && w.Length >= 6)
                        {
                            st["station"] = RefToken(string.Join(" ", w.Skip(1).Take(w.Length - 5)));
                            st["at"] = string.Join(" ", w.Skip(w.Length - 4).Take(3));
                            st["radius"] = NumOrText(w[w.Length - 1]);
                        }
                        else if (w.Length > 1) st["station"] = RefToken(string.Join(" ", w.Skip(1)));
                        Push(st, "on", "steps");
                        break;
                    }
                    case "ask":
                    case "vote":
                    {
                        int bar = rest.IndexOf('|');
                        if (bar < 0) { Command(); break; }
                        var head = rest.Substring(0, bar).Trim();
                        var parts = rest.Substring(bar + 1).Split('|').Select(x => x.Trim()).ToList();
                        int last = head.LastIndexOf(' ');
                        string who = last > 0 ? head.Substring(0, last).Trim() : "@a", secs = last > 0 ? head.Substring(last + 1) : head;
                        var st = new JObject { ["type"] = word, ["seconds"] = NumOrText(secs) };
                        if (who != "@a") st["who"] = who;
                        string q = parts.Count > 0 ? parts[0] : "";
                        int colon = q.IndexOf(" : ", StringComparison.Ordinal);
                        if (colon > 0) { st["speaker"] = q.Substring(0, colon).Trim(); q = q.Substring(colon + 3).Trim(); }
                        st["question"] = q;
                        st["answers"] = new JArray(parts.Skip(1).Select(a => new JObject { ["text"] = a, ["steps"] = new JArray() }));
                        if (word == "vote") st["none"] = new JArray();
                        Add(st);
                        open.Push((null, word, st));
                        break;
                    }
                    case "answer":
                    case "choice":
                    {
                        if (open.Count == 0 || (open.Peek().kind != "ask" && open.Peek().kind != "vote") || !int.TryParse(rest, out int k))
                        { notes.Add($"Line {n + 1}: \"{line}\" outside its question, left out."); break; }
                        var q = open.Peek().step;
                        JArray list;
                        if (k == 0) { list = new JArray(); q["none"] = list; }
                        else if (q["answers"] is JArray answers && k <= answers.Count) { list = new JArray(); answers[k - 1]["steps"] = list; }
                        else { notes.Add($"Line {n + 1}: no answer {k}, left out."); break; }
                        open.Push((list, word, q));
                        break;
                    }
                    case "end":
                        if (open.Count > 0) open.Pop();
                        else notes.Add($"Line {n + 1}: an end without its block, left out.");
                        break;
                    case "objective":
                        Add(new JObject { ["type"] = "objective", ["text"] = rest.Replace("\\n", "\n") });
                        break;
                    case "target":
                    {
                        // "objective X" + "target S" (what a Set Objective node writes) is one step.
                        var list = Cur();
                        if (list.Count > 0 && list[list.Count - 1] is JObject prev && (string)prev["type"] == "objective" && prev["station"] == null)
                        { if (rest != "off") prev["station"] = RefToken(rest); }
                        else Add(new JObject { ["type"] = "target", ["station"] = rest == "off" ? null : RefToken(rest) });
                        break;
                    }
                    case "questorbit":
                    {
                        var w = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        bool off = w.Count > 1 && w[w.Count - 1].ToLowerInvariant() == "off";
                        if (off) w.RemoveAt(w.Count - 1);
                        Add(new JObject { ["type"] = "questorbit", ["station"] = RefToken(string.Join(" ", w)), ["on"] = !off });
                        break;
                    }
                    case "checkpoint": Add(new JObject { ["type"] = "checkpoint", ["name"] = rest }); break;
                    case "startquest": Add(new JObject { ["type"] = "startquest", ["quest"] = rest }); break;
                    case "startevent":
                    {
                        int s2 = rest.IndexOf(' ');
                        var st = new JObject { ["type"] = "startevent", ["event"] = s2 < 0 ? rest : rest.Substring(0, s2) };
                        if (s2 > 0) st["settings"] = rest.Substring(s2 + 1).Trim();
                        Add(st);
                        break;
                    }
                    case "complete":
                    {
                        int bar = rest.IndexOf('|');
                        string rw = (bar < 0 ? rest : rest.Substring(0, bar)).Trim(), title = bar < 0 ? "" : rest.Substring(bar + 1).Trim();
                        if (rw.Length > 0 && !int.TryParse(rw, out _)) { Command(); break; }
                        var st = new JObject { ["type"] = "complete" };
                        if (rw.Length > 0) st["reward"] = int.Parse(rw, CultureInfo.InvariantCulture);
                        if (title.Length > 0) st["title"] = title;
                        Add(st);
                        break;
                    }
                    case "fail": Add(rest.Length > 0 ? new JObject { ["type"] = "fail", ["title"] = rest } : new JObject { ["type"] = "fail" }); break;
                    case "stop": Add(new JObject { ["type"] = "stop" }); break;
                    case "score": Add(new JObject { ["type"] = "score", ["mode"] = rest }); break;
                    case "winner": Add(new JObject { ["type"] = "winner", ["mode"] = rest }); break;
                    case "scoreboard":
                        Add(rest.StartsWith("on") ? new JObject { ["type"] = "scoreboard", ["show"] = true, ["title"] = rest.Substring(2).Trim() } : new JObject { ["type"] = "scoreboard", ["show"] = false });
                        break;
                    case "pvp": Add(new JObject { ["type"] = "pvp", ["on"] = rest.Trim().ToLowerInvariant() == "on" }); break;
                    case "dialog":
                    {
                        if (!SplitWho(rest, out string who, out string body) || body.Split('|').Any(x => x.Trim().StartsWith("reward", StringComparison.OrdinalIgnoreCase))) { Command(); break; }
                        var pages = new JArray();
                        string last = "player";
                        bool ok = true;
                        foreach (var part in body.Split('|'))
                        {
                            string p = part.Trim();
                            int colon = p.IndexOf(" : ", StringComparison.Ordinal);
                            var page = new JObject();
                            if (colon > 0) { last = p.Substring(0, colon).Trim(); p = p.Substring(colon + 3).Trim(); }
                            else if (p.StartsWith(": ")) p = p.Substring(2);
                            var vm = Regex.Match(p, @"\s*\[voice ([^\]\s]+)\]");
                            if (vm.Success) { page["voice"] = vm.Groups[1].Value; p = p.Remove(vm.Index, vm.Length).Trim(); }
                            if (p.Length == 0) { ok = false; break; }
                            page["speaker"] = last;
                            page["text"] = p;
                            pages.Add(page);
                        }
                        if (!ok || pages.Count == 0) { Command(); break; }
                        var st = new JObject { ["type"] = "dialog", ["pages"] = pages };
                        if (who != "@a") st["who"] = who;
                        Add(st);
                        break;
                    }
                    case "radio":
                    {
                        if (!SplitWho(rest, out string who, out string body)) { Command(); break; }
                        int colon = body.IndexOf(" : ", StringComparison.Ordinal);
                        if (colon <= 0) { Command(); break; }
                        var st = new JObject { ["type"] = "radio", ["speaker"] = body.Substring(0, colon).Trim() };
                        string text = body.Substring(colon + 3).Trim();
                        var vm = Regex.Match(text, @"\s*\[voice ([^\]\s]+)\]");
                        if (vm.Success) { st["voice"] = vm.Groups[1].Value; text = text.Remove(vm.Index, vm.Length).Trim(); }
                        st["text"] = text;
                        if (who != "@a") st["who"] = who;
                        Add(st);
                        break;
                    }
                    case "music":
                    case "sound":
                    case "letterbox":
                    {
                        if (!SplitWho(rest, out string who, out string body) || body.Contains(" ")) { Command(); break; }
                        var st = word == "letterbox" ? new JObject { ["type"] = "letterbox", ["on"] = body.ToLowerInvariant() == "on" }
                               : new JObject { ["type"] = word, [word == "music" ? "track" : "sound"] = body };
                        if (who != "@a") st["who"] = who;
                        Add(st);
                        break;
                    }
                    case "cutscene":
                    {
                        if (!SplitWho(rest, out string who, out string body)) { Command(); break; }
                        var w = body.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        JObject st;
                        if (w.Length == 1 && w[0] == "end") st = new JObject { ["type"] = "cutsceneend" };
                        else if (w.Length > 0 && w[0] == "start")
                            st = new JObject { ["type"] = "cutscene", ["letterbox"] = !w.Contains("nobars"), ["freeze"] = w.Contains("freeze"), ["invulnerable"] = w.Contains("invulnerable") };
                        else { Command(); break; }
                        if (who != "@a") st["who"] = who;
                        Add(st);
                        break;
                    }
                    default:
                    {
                        SplitWho(rest, out string who, out string body);
                        var typed = body.StartsWith("@") ? null : Typed(word, who, body);
                        if (typed == null) { Command(); break; }
                        if (who != "@a") typed["who"] = who;
                        Add(typed);
                        break;
                    }
                }
            }
            if (open.Count > 0) notes.Add("A block had no end: it was closed at the end.");
            if (!titled && kind != "event") o["title"] = fallbackTitle;
            if (kind == "event" && titled) kind = "mission";   // an old graph with a Mission title is a bar mission (EventRunner.Info)
            o["kind"] = kind;
            if (variables.Count > 0) o["variables"] = variables;
            // A quest's script ends with "complete" by itself: not a step of its own. One that ends without it doesn't complete
            // after its last step.
            if (kind != "event")
            {
                var lastTop = root.Count > 0 ? root[root.Count - 1] as JObject : null;
                string lastType = (string)lastTop?["type"];
                if (lastType == "complete" && lastTop.Count == 1) lastTop.Remove();
                else if (lastType != "complete" && lastType != "fail") o["autoComplete"] = false;
            }
            o["steps"] = root;
            if (commands > 0) notes.Add(commands == 1 ? "1 line stays a Command step." : $"{commands} lines stay Command steps.");
            return o;
        }

        static readonly string[] RaceWords = { "pirate", "terran", "vossk", "nivelian", "midorian", "void" };
        static readonly string[] BehaviourWords = { "enemy", "neutral", "friendly", "standing" };

        /// <summary>A line's words: spaces inside { } and [ ] don't split ("{random(2, 4)}", "@alive[orbit=Var Hastra]").</summary>
        static List<string> Words(string body)
        {
            var words = new List<string>();
            var cur = new StringBuilder();
            int depth = 0;
            foreach (char c in body)
            {
                if (c == '{' || c == '[') depth++;
                else if ((c == '}' || c == ']') && depth > 0) depth--;
                if (c == ' ' && depth == 0) { if (cur.Length > 0) { words.Add(cur.ToString()); cur.Clear(); } continue; }
                cur.Append(c);
            }
            if (cur.Length > 0) words.Add(cur.ToString());
            return words;
        }

        static bool IsNumberOrExpr(string w) => double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || w.StartsWith("{") && w.EndsWith("}");

        /// <summary>A command line the builder has a step for, as that step (null: it stays a Command step). Only the forms the
        /// step writes back the same way.</summary>
        static JObject Typed(string word, string who, string body)
        {
            var w = Words(body);
            JObject St(string type) => new JObject { ["type"] = type };
            switch (word)
            {
                case "heal": case "ammo": case "kill": case "reveal": case "peace":
                    return w.Count == 0 ? St(word) : null;
                case "title":
                {
                    var st = St("title");
                    var m = Regex.Match(body, @"\s+for\s+(\S+)$");
                    string t = body;
                    if (m.Success && IsNumberOrExpr(m.Groups[1].Value)) { st["seconds"] = NumOrText(m.Groups[1].Value); t = body.Substring(0, m.Index); }
                    int bar = t.IndexOf('|');
                    st["title"] = (bar < 0 ? t : t.Substring(0, bar)).Trim();
                    if (bar >= 0) st["subtitle"] = t.Substring(bar + 1).Trim();
                    return st;
                }
                case "timer":
                {
                    if (w.Count == 1 && w[0] == "stop") { var st = St("timer"); st["stop"] = true; return st; }
                    if (w.Count == 0 || !IsNumberOrExpr(w[0])) return null;
                    var t2 = St("timer");
                    t2["seconds"] = NumOrText(w[0]);
                    if (w.Count > 1) t2["label"] = string.Join(" ", w.Skip(1));
                    return t2;
                }
                case "points":
                    return w.Count == 1 ? new JObject { ["type"] = "points", ["amount"] = NumOrText(w[0]) } : null;
                case "credits":
                    return w.Count == 1 && IsNumberOrExpr(w[0]) ? new JObject { ["type"] = "credits", ["amount"] = NumOrText(w[0]) } : null;
                case "restrict":
                    return w.Count == 1 && new[] { "jumps", "docking", "all", "off" }.Contains(w[0]) ? new JObject { ["type"] = "restrict", ["what"] = w[0] } : null;
                case "waypoint":
                    if (w.Count == 1 && w[0] == "off") return St("waypoint");
                    return w.Count == 4 && w.Skip(1).All(IsNumberOrExpr) ? new JObject { ["type"] = "waypoint", ["station"] = RefToken(w[0]), ["at"] = string.Join(" ", w.Skip(1)) } : null;
                case "respawn":
                {
                    if (w.Count == 1 && w[0] == "off") return St("respawn");
                    if (w.Count == 0) return null;
                    var st = new JObject { ["type"] = "respawn", ["station"] = RefToken(w[0]) };
                    int i = 1;
                    if (w.Count >= 4 && w.Skip(1).Take(3).All(IsNumberOrExpr)) { st["at"] = string.Join(" ", w.Skip(1).Take(3)); i = 4; }
                    for (; i + 1 < w.Count; i += 2)
                    {
                        if (w[i] != "spread" && w[i] != "delay") return null;
                        st[w[i]] = NumOrText(w[i + 1]);
                    }
                    return i == w.Count ? st : null;
                }
                case "provoke":
                {
                    var st = St("provoke");
                    int i = 0;
                    if (i < w.Count && RaceWords.Contains(w[i])) st["race"] = w[i++];
                    if (i + 1 < w.Count && w[i] == "within") { st["within"] = NumOrText(w[i + 1]); i += 2; }
                    return i == w.Count ? st : null;
                }
                case "tp":
                {
                    if (w.Count == 1) return new JObject { ["type"] = "teleport", ["station"] = RefToken(w[0]) };
                    if (w.Count == 2 && w[1] == "dock") return new JObject { ["type"] = "teleport", ["station"] = RefToken(w[0]), ["dock"] = true };
                    if (w.Count == 4 && w.Skip(1).All(IsNumberOrExpr)) return new JObject { ["type"] = "teleport", ["station"] = RefToken(w[0]), ["at"] = string.Join(" ", w.Skip(1)) };
                    return null;
                }
                case "reward":
                {
                    int bar = body.IndexOf('|');
                    var st = St("reward");
                    if (bar >= 0) st["title"] = body.Substring(bar + 1).Trim();
                    var items = new JArray();
                    foreach (var part in (bar < 0 ? body : body.Substring(0, bar)).Split('+').Select(x => x.Trim()).Where(x => x.Length > 0))
                    {
                        if (IsNumberOrExpr(part)) { if (st["credits"] != null) return null; st["credits"] = NumOrText(part); continue; }
                        var m = Regex.Match(part, @"^(.+?)\s+(\d+)$");
                        if (m.Success) items.Add(new JObject { ["item"] = RefToken(m.Groups[1].Value.Trim()), ["amount"] = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) });
                        else items.Add(new JObject { ["item"] = RefToken(part), ["amount"] = 1 });
                    }
                    if (items.Count > 0) st["items"] = items;
                    return st["credits"] != null || items.Count > 0 ? st : null;
                }
                case "spawn":
                {
                    if (w.Count == 0) return null;
                    var st = St("spawn");
                    int end = w.Count;
                    int at = w.LastIndexOf("at");
                    if (at >= 1 && at == w.Count - 4 && w.Skip(at + 1).All(IsNumberOrExpr)) { st["at"] = string.Join(" ", w.Skip(at + 1)); end = at; }
                    int named = w.IndexOf("named");
                    if (named >= 1 && named < end) { st["name"] = string.Join(" ", w.Skip(named + 1).Take(end - named - 1)); end = named; }
                    int tur = w.IndexOf("turrets");
                    if (tur >= 1 && tur == end - 2) { st["turrets"] = w[tur + 1]; end = tur; }
                    if (end >= 2 && BehaviourWords.Contains(w[end - 1])) { st["behaviour"] = w[end - 1]; end--; }
                    if (end >= 2 && IsNumberOrExpr(w[end - 1])) { st["count"] = NumOrText(w[end - 1]); end--; }
                    if (end >= 2 && RaceWords.Contains(w[end - 1])) { st["race"] = w[end - 1]; end--; }
                    if (end < 1) return null;
                    st["ship"] = RefToken(string.Join(" ", w.Take(end)));
                    return st;
                }
            }
            return null;
        }

        /// <summary>A Unity-made event graph (.gof2event) as a .gof2quest, its nodes where they were on the Unity canvas.
        /// Null with the problems when its graph has errors.</summary>
        public static JObject FromGraphFile(string yaml, string fallbackTitle, List<string> notes, List<EventGraphScript.Problem> problems)
        {
            EventGraphData data;
            try { data = EventGraphFile.Read(yaml); }
            catch (Exception e) { problems.Add(new EventGraphScript.Problem { message = "can't read the graph file: " + e.Message }); return null; }
            string script = EventGraphScript.Compile(data, problems);
            if (problems.Any(p => !p.warning) || script.Length == 0) return null;
            var places = new Dictionary<string, (float, float)>();
            foreach (var n in data.nodes) if (n.id != null) places[n.id] = (n.x, n.y);
            var json = FromScript(script, fallbackTitle, notes, places);
            var start = data.nodes.FirstOrDefault(n => n.kind == "EventStartNode" && (data.mainGraph == null || n.graph == data.mainGraph));
            if (start != null) QuestGraph.SetPos(json, start.x, start.y, "startPos");
            return json;
        }

        /// <summary>"@a rest" / "@alive[orbit=Var Hastra] rest" -> the selector and the rest (a command without one: "@a").</summary>
        static bool SplitWho(string rest, out string who, out string body)
        {
            who = "@a";
            body = rest.Trim();
            if (!body.StartsWith("@")) return body.Length > 0;
            int open = body.IndexOf('['), space = body.IndexOf(' ');
            int end = open >= 0 && (space < 0 || open < space) ? body.IndexOf(']') + 1 : space < 0 ? body.Length : space;
            if (end <= 0) return false;
            who = body.Substring(0, end).Trim();
            body = body.Substring(end).Trim();
            return body.Length > 0;
        }

        static JToken NumOrText(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (Math.Abs(d - Math.Round(d)) < 1e-9 ? (JToken)(long)Math.Round(d) : d) : s.Trim('{', '}');

        static JToken RefToken(string s) => int.TryParse(s, out int i) ? (JToken)i : s;
    }
}
