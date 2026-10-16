// ModBuilderViewQuests.cs
// The Mod Builder's Quests kind (part of ModBuilderView), edited in the Space Lounge where missions are offered: the
// project's quests, bar missions and events as step trees (ProjectQuest, events/<name>.gof2quest; the game turns the steps
// into an event graph's script, Events.QuestFile; every step type and its fields are Events.QuestSteps'; Modding/README.md
// "Quests made in the Mod Builder"). Left: the quests, a new one of each kind, Remove, the event graphs made in the Unity
// Editor (each converted to steps, or kept as it is) and Import an event graph (a .gof2event, converted or copied in). Right:
// the title, the kind, when a quest starts, a bar mission's offer, client and stations, the reward, the variables; then the
// steps: blocks (If, While, Repeat, At the same time, Every, When something happens, Ask, Vote, Random) show their own lists
// under them, the picked step opens for editing (moved up / down inside its list, copied, removed), "+ Add a step" after it
// or into a block. Speakers, stations, ships, items and quests come from ModRefs: the game's, the project's own (its
// characters!) and other mods' (which become dependencies). Play starts a play test with it under way
// (ModBuilderPlay.StartQuest); the script it makes can be looked at.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Events;
using GoF2Remake.Modding;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        VisualElement questList, questPage;
        ProjectQuest currentQuest;
        string currentGraph;
        JObject currentStep;
        /// <summary>The node graph of the quest (the Graph view), between the panels.</summary>
        QuestGraphView graph;
        VisualElement graphTools;
        Button graphWideButton;
        IVisualElementScheduledItem graphRedraw;
        static bool GraphMode { get => PlayerPrefs.GetInt("mb_quest_graph", 1) == 1; set => PlayerPrefs.SetInt("mb_quest_graph", value ? 1 : 0); }
        static bool GraphWide { get => PlayerPrefs.GetInt("mb_quest_graph_wide", 0) == 1; set => PlayerPrefs.SetInt("mb_quest_graph_wide", value ? 1 : 0); }

        static string StepName(string type) => QuestSteps.Get(type) is QuestStepType t ? T("mbStep_" + type, t.english) : type;

        void BuildQuestsPanels(VisualElement left, VisualElement right)
        {
            questList = ListPanel(left, T("mbQuests", "Quests"));
            Act(left, T("mbNewQuest", "+ New quest"), () => NewQuest("quest"), "mbv-add");
            Act(left, T("mbNewMission", "+ New bar mission"), () => NewQuest("mission"), "mbv-add");
            Act(left, T("mbNewEvent", "+ New event"), () => NewQuest("event"), "mbv-add");
            Act(left, T("mbImportGraph", "Import an event graph…"), ImportGraph, "mbv-add");
            Act(left, T("mbRemoveQuest", "Remove"), AskRemoveQuest, "mbv-danger", "mbv-remove");
            questPage = PagePanel(right);
            BuildQuestGraph();
        }

        // ---- the graph view -----------------------------------------------------------------------------------

        void BuildQuestGraph()
        {
            graph = new QuestGraphView
            {
                stepName = StepName,
                stepSummary = StepSummary,
                fieldText = english => T("mbField_" + english.Replace(" ", ""), english),
                selected = _ => { currentStep = graph.Selection as JObject; BuildQuestPage(); },
                edited = () => { MarkDirty(); RebuildQuests(); BuildQuestPage(); },
                pickStep = (done, cut) => PickStepFor(done, cut, false),
                pickValue = PickValueType,
            };
            graph.style.display = DisplayStyle.None;
            graphTools = new VisualElement();
            graphTools.AddToClassList("mbg-tools");
            Act(graphTools, T("mbGraphAddStep", "+ Step"), () => graph.NewStepAt(), "mbv-step");
            Act(graphTools, T("mbGraphAddValue", "+ Value"), graph.NewValue, "mbv-step");
            Act(graphTools, T("mbGraphTidy", "Tidy up"), graph.Tidy, "mbv-step");
            Act(graphTools, T("mbGraphFrame", "Find Start"), graph.Frame, "mbv-step");
            Act(graphTools, "+", () => graph.ZoomBy(1.15f), "mbv-step");
            Act(graphTools, "−", () => graph.ZoomBy(1f / 1.15f), "mbv-step");
            graphWideButton = Act(graphTools, "", () => { GraphWide = !GraphWide; UpdateQuestGraph(); }, "mbv-step");
            graph.Add(graphTools);
            var help = new Label(T("mbGraphHelp", "Drag the canvas to move around, the wheel to zoom. Drag from an output (right) onto a node to wire it, or onto the canvas for a new step; drag a wired input away to cut it. Double-click the canvas for a new step; Delete removes the selected node."));
            help.AddToClassList("mbg-help");
            help.pickingMode = PickingMode.Ignore;
            graph.Add(help);
            root.Add(graph);
        }

        /// <summary>The graph is shown with the Quests kind in the Graph view (a step-list quest open); wide hides the list.</summary>
        void UpdateQuestGraph()
        {
            if (graph == null) return;
            bool show = kind == ModBuilder.Kind.Quests && GraphMode && currentQuest != null && currentGraph == null;
            graph.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            root.EnableInClassList("mbv-root--graph", show);
            root.EnableInClassList("mbv-root--graph-wide", show && GraphWide);
            graphWideButton.text = GraphWide ? T("mbGraphNarrow", "Show the list") : T("mbGraphWide", "Wider");
            if (!show) return;
            if (graph.quest != currentQuest.Json)
            {
                graph.quest = currentQuest.Json;
                graph.Select(null, false);
                graph.Rebuild();
                graph.schedule.Execute(graph.Frame);
            }
        }

        /// <summary>The graph drawn again a moment after the last edit (typing in the inspector).</summary>
        void RedrawGraphSoon()
        {
            if (graph == null || graph.style.display == DisplayStyle.None) return;
            graphRedraw?.Pause();
            graphRedraw = graph.schedule.Execute(() => graph.Rebuild()).StartingIn(200);
        }

        void PickValueType(Action<JObject> done)
        {
            var choices = new List<(string, Texture2D, Action)>();
            foreach (var t in QuestGraph.ValueTypes)
            {
                string type = t.type;
                string what = t.output == QuestGraph.ValueKind.Players ? T("mbValueGivesPlayers", "gives players") : t.output == QuestGraph.ValueKind.Orbit ? T("mbValueGivesOrbit", "gives a station")
                            : T("mbValueGivesNumber", "gives a number");
                choices.Add(($"{T("mbValue_" + type, t.english)}\n{what}", null, () => done(QuestGraph.NewValue(currentQuest.Json, type))));
            }
            ShowPicker(T("mbGraphAddValue", "+ Value"), choices, true);
        }

        /// <summary>The inspector beside the graph: the selected step's fields, a value node's, or the quest's settings.</summary>
        void BuildGraphInspector(ProjectQuest q)
        {
            var top = Row(questPage);
            Act(top, T("mbPlayQuest", "Play it"), () => PlayQuest(q), "mbv-play");
            Act(top, T("mbShowScript", "Show the script"), () => ShowScript(q));
            Act(top, T("mbListView", "List view"), () => { GraphMode = false; UpdateQuestGraph(); BuildQuestPage(); });
            var sel = graph.Selection as JObject;
            if (sel != null && sel.Parent == q.Json["values"]) { BuildValueInspector(q, sel); return; }
            if (sel != null && sel.Parent is JArray && QuestSteps.Get((string)sel["type"] ?? "") is QuestStepType def)
            {
                Heading(questPage, StepName(def.type));
                var tools = Row(questPage);
                Act(tools, T("mbDuplicate", "Copy"), () =>
                {
                    var copy = (JObject)sel.DeepClone();
                    var (x, y) = QuestGraph.Pos(sel);
                    QuestGraph.SetPos(copy, x + 40, y + 40);
                    QuestGraph.AddLoose(q.Json, copy);
                    graph.Rebuild();
                    graph.Select(copy);
                    MarkDirty();
                }, "mbv-step");
                if (QuestGraph.Incoming(sel).port != null)
                    Act(tools, T("mbCutWireIn", "Cut its wire in"), () => { QuestGraph.Detach(q.Json, sel); graph.Rebuild(); MarkDirty(); BuildQuestPage(); }, "mbv-step");
                Act(tools, T("mbDelete", "Delete"), graph.DeleteSelected, "mbv-danger");
                Act(tools, T("mbQuestSettings", "Quest settings"), () => graph.Select(null), "mbv-step");
                JsonButton(questPage, sel, () => { graph.Rebuild(); BuildQuestPage(); }, T("mbJsonStepNote", "The step's fields (QuestSteps; Modding/README.md \"Quests made in the Mod Builder\")."));
                int index = QuestGraph.AllSteps(q.Json).ToList().IndexOf(sel) + 1;
                BuildStep(questPage, q, sel, def, index.ToString(CultureInfo.InvariantCulture), 0);
                Note(questPage, T("mbGraphWiredNote", "A field with a dot on the node takes a wire from a value node (+ Value): a number, the game's state, maths, a comparison, the players, an orbit."));
                return;
            }
            BuildQuestSettings(q);
            JsonButton(questPage, q.Json, () => { graph.Rebuild(); RebuildQuests(); BuildQuestPage(); }, T("mbJsonQuestNote", "The whole .gof2quest: its settings, steps, the loose chains and value nodes."));
            int loose = (q.Json["loose"] as JArray)?.Count ?? 0;
            if (loose > 0) Note(questPage, string.Format(T("mbLooseNote", "{0} chain(s) of steps aren't wired to anything: they don't run until wired to Start or another step."), loose));
        }

        void BuildValueInspector(ProjectQuest q, JObject v)
        {
            var t = QuestGraph.GetValueType((string)v["type"] ?? "");
            Heading(questPage, t != null ? T("mbValue_" + t.type, t.english) : (string)v["type"]);
            var tools = Row(questPage);
            Act(tools, T("mbDelete", "Delete"), graph.DeleteSelected, "mbv-danger");
            Act(tools, T("mbQuestSettings", "Quest settings"), () => graph.Select(null), "mbv-step");
            if (t == null) return;
            void Changed(bool rebuild) { MarkDirty(); RedrawGraphSoon(); if (rebuild) BuildQuestPage(); }
            foreach (var (key, label, choices, def) in t.options)
            {
                string cur = v[key]?.ToString() ?? def;
                if (choices.Length > 0)
                {
                    if (choices.Length <= 6) Segments(questPage, T("mbValueOpt_" + key, label), choices, Math.Max(0, Array.IndexOf(choices, cur)), n => { v[key] = choices[n]; Changed(true); });
                    else
                    {
                        Caption(questPage, T("mbValueOpt_" + key, label));
                        var row = Row(questPage);
                        Add(row, new Label(cur), "mbv-inline-label");
                        Act(row, T("mbChoose", "Choose…"), () => ShowPicker(label, choices.Select(c => (c, (Texture2D)null, (Action)(() => { v[key] = c; Changed(true); }))).ToList()), "mbv-step");
                    }
                }
                else if (key == "station")
                    RefField(questPage, T("mbStation", "Station"), RefKind.Station, cur, x => { v[key] = ProjectEntry.RefToken(x ?? "78"); Changed(true); }, clear: false);
                else if (key == "item")
                    RefField(questPage, T("mbItem", "Item"), RefKind.Item, cur, x => { v[key] = ProjectEntry.RefToken(x ?? "22"); Changed(true); }, clear: false);
                else if (key == "name" && t.type == "quest")
                    RefField(questPage, T("mbQuestField", "Quest"), RefKind.Quest, cur, x => { if (x != null) { v[key] = x; Changed(true); } }, clear: false);
                else Text(questPage, T("mbValueOpt_" + key, label), cur, 200, x => { v[key] = x.Trim(); Changed(false); });
            }
            foreach (var (key, kindOf, label, def) in t.inputs)
            {
                string wired = QuestGraph.WiredValue(v[key]);
                if (wired != null)
                {
                    var row = Row(questPage);
                    var src = QuestGraph.Value(q.Json, wired);
                    Add(row, new Label($"{T("mbValueIn_" + key, label)}: {T("mbWiredFrom", "wired from")} {(src != null ? T("mbValue_" + (string)src["type"], (string)src["type"]) : "?")}"), "mbv-inline-label");
                    Act(row, "✕", () => { v.Remove(key); Changed(true); }, "mbv-step");
                }
                else if (kindOf == QuestGraph.ValueKind.Orbit)
                    RefField(questPage, T("mbValueIn_" + key, label), RefKind.Station, v[key]?.ToString() ?? (def.Length > 0 ? def : null), x => { if (x == null) v.Remove(key); else v[key] = ProjectEntry.RefToken(x); Changed(true); }, T("mbNone", "none"));
                else Text(questPage, T("mbValueIn_" + key, label), v[key]?.ToString() ?? def, 200, x => { v[key] = Num(x); Changed(false); });
            }
            var problems = new List<string>();
            string expr = QuestGraph.Resolve(q.Json, (string)v["id"], problems.Add);
            Note(questPage, T("mbValueGives", "It gives:") + " " + expr + (problems.Count > 0 ? "  ✖ " + problems[0] : ""));
        }

        void RebuildQuests()
        {
            questList.Clear();
            if (Project == null) return;
            var graphs = Project.EventGraphs();
            if (currentQuest != null && !Project.Quests.Contains(currentQuest)) currentQuest = null;
            if (currentGraph != null && !graphs.Contains(currentGraph)) currentGraph = null;
            if (currentQuest == null && currentGraph == null) currentQuest = Project.Quests.FirstOrDefault();
            if (currentQuest == null && currentGraph == null) currentGraph = graphs.FirstOrDefault();
            foreach (var quest in Project.Quests)
            {
                var q = quest;
                ListRow(questList, q.Title ?? q.Name, KindName(q.Kind) + "  ·  " + q.Name + "  ·  " + string.Format(T("mbSteps", "{0} steps"), CountSteps(q.Steps)), q == currentQuest,
                        () => { currentQuest = q; currentGraph = null; currentStep = null; RebuildQuests(); BuildQuestPage(); }, null, null);
            }
            if (graphs.Count > 0)
            {
                Heading(questList, T("mbEventGraphs", "Event graphs"));
                foreach (var g in graphs)
                {
                    string name = g;
                    ListRow(questList, name, T("mbMadeInUnity", "Made in the Unity Editor"), name == currentGraph,
                            () => { currentGraph = name; currentQuest = null; currentStep = null; RebuildQuests(); BuildQuestPage(); }, null, null);
                }
            }
            if (Project.Quests.Count == 0 && graphs.Count == 0)
                Note(questList, T("mbNoQuests", "No quests yet. A quest starts by itself and runs like the game's story; a bar mission is offered by a visitor in the Space Lounges; an event is started by /event in multiplayer or by another's Next event step."));
        }

        static int CountSteps(JArray steps) => ProjectQuest.All(steps, Lists).Count();

        static IEnumerable<JArray> Lists(JObject st) => QuestSteps.Children(st).Select(c => c.list);

        static string KindName(string kind) => kind == "mission" ? T("mbBarMission", "Bar mission") : kind == "event" ? T("mbEvent", "Event") : T("mbQuest", "Quest");

        void NewQuest(string kind) => AskText(kind == "mission" ? T("mbNewMission", "+ New bar mission") : kind == "event" ? T("mbNewEvent", "+ New event") : T("mbNewQuest", "+ New quest"),
            T("mbTitleField", "Title"), "", title =>
        {
            var q = Project.AddQuest(title, kind == "mission");
            q.Kind = kind;
            if (kind == "mission") { q.Offer = T("mbOfferDefault", "I have a job for you."); q.Reward = 10000; }
            // A first conversation and the end, to build on.
            var page = new JObject { ["speaker"] = "player", ["text"] = T("mbFirstLine", "Let's get to work.") };
            q.AddStep("dialog")["pages"] = new JArray(page);
            if (kind != "event") q.AddStep("complete");
            currentQuest = q;
            currentGraph = null;
            currentStep = null;
            MarkDirty();
            RebuildQuests();
            BuildQuestPage();
        });

        void AskRemoveQuest()
        {
            if (currentGraph != null)
            {
                string g = currentGraph;
                Confirm(T("mbRemoveQuest", "Remove"), string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), g), () =>
                {
                    Project.RemoveEventGraph(g);
                    currentGraph = null;
                    QuestFilesChanged();
                });
                return;
            }
            var q = currentQuest;
            if (q == null) return;
            Confirm(T("mbRemoveQuest", "Remove"), string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), q.Title ?? q.Name), () =>
            {
                Project.Quests.Remove(q);
                currentQuest = null;
                currentStep = null;
                MarkDirty();
                RebuildQuests();
                BuildQuestPage();
            });
        }

        /// <summary>A graph file came or went: saved, read again, the lists redrawn.</summary>
        void QuestFilesChanged()
        {
            SaveNow();
            RequestReload(false);
            RebuildQuests();
            BuildQuestPage();
        }

        void QuestEdited(bool rebuildPage = false)
        {
            MarkDirty();
            RedrawGraphSoon();
            if (rebuildPage) BuildQuestPage();
        }

        // ---- event graphs made in the Unity Editor ---------------------------------------------------------------

        void ImportGraph() => PickRaw(T("mbImportGraph", "Import an event graph…"), T("mbGraphFilter", "Event graphs"), new[] { "gof2event", "gof2netevent" }, picked =>
        {
            string text = File.ReadAllText(picked.path);
            if (!EventGraphScript.IsGraph(text)) { Status(T("mbNotGraph", "That isn't an event graph (a .gof2event the Unity Editor saved)."), true); return; }
            string name = Path.GetFileNameWithoutExtension(picked.name ?? picked.path);
            string src = Path.Combine(Application.temporaryCachePath, "mb_graph_" + Guid.NewGuid().ToString("N") + ".gof2event");
            File.Copy(picked.path, src, true);
            var choices = new List<(string, Texture2D, Action)>
            {
                (T("mbConvertSteps", "Convert to steps") + "\n" + T("mbConvertStepsWhat", "Edit it here; the lines the builder has no step for stay Command steps"), null,
                    () => { try { ConvertGraph(name, text); } finally { try { File.Delete(src); } catch { } } }),
                (T("mbKeepGraph", "Add it as it is") + "\n" + T("mbKeepGraphWhat", "It runs as the Unity Editor made it; edit it there"), null, () =>
                {
                    try
                    {
                        string added = Project.AddEventGraph(src);
                        currentGraph = added;
                        currentQuest = null;
                        Status(string.Format(T("mbGraphAdded", "\"{0}\" added."), added), false);
                        QuestFilesChanged();
                    }
                    finally { try { File.Delete(src); } catch { } }
                }),
            };
            ShowPicker(T("mbImportGraph", "Import an event graph…"), choices);
        });

        /// <summary>A graph's script as steps (QuestFile.FromScript), a quest of the project; 'replace': the graph of that name
        /// in the project goes.</summary>
        void ConvertGraph(string name, string text, bool replace = false)
        {
            var problems = new List<EventGraphScript.Problem>();
            string script = EventGraphScript.FromFile(text, problems);
            var errors = problems.Where(p => !p.warning).ToList();
            if (errors.Count > 0 || script.Length == 0)
            {
                Status("✖ " + (errors.Count > 0 ? (errors[0].node != null ? errors[0].node + ": " : "") + errors[0].message : T("mbGraphEmpty", "The graph has no steps.")), true);
                return;
            }
            var notes = new List<string>();
            var json = QuestFile.FromScript(script, Pretty(name), notes);
            if (replace) Project.RemoveEventGraph(name);
            var q = Project.AddQuest(name, json);
            currentQuest = q;
            currentGraph = null;
            currentStep = null;
            MarkDirty();
            QuestFilesChanged();
            Status(string.Format(T("mbGraphConverted", "\"{0}\" is now steps."), q.Name) + (notes.Count > 0 ? " " + string.Join(" ", notes) : ""), false);
        }

        void BuildGraphPage(string name)
        {
            Heading(questPage, name);
            Note(questPage, T("mbGraphNote", "Made in the Unity Editor with Graph Toolkit: it runs as it is. Edit it there, or convert it to steps to edit it here (the lines the builder has no step for stay Command steps, so it runs the same)."));
            var top = Row(questPage);
            Act(top, T("mbPlayQuest", "Play it"), () =>
            {
                SaveNow();
                ModBuilder.Reload();
                afterReload.Clear();
                string why = ModBuilderPlay.StartQuest(name);
                if (why != null) Status(why, true);
            }, "mbv-play");
            Act(top, T("mbShowScript", "Show the script"), () =>
            {
                string path = Project.EventGraphPath(name);
                var problems = new List<EventGraphScript.Problem>();
                string script = path != null ? EventGraphScript.FromFile(File.ReadAllText(path), problems) : "";
                ShowScriptText(script, problems.Select(p => (p.warning ? "⚠ " : "✖ ") + (p.node != null ? p.node + ": " : "") + p.message).ToList());
            });
            Act(questPage, T("mbConvertSteps", "Convert to steps"), () =>
            {
                string path = Project.EventGraphPath(name);
                if (path == null) return;
                Confirm(T("mbConvertSteps", "Convert to steps"), string.Format(T("mbConvertConfirm", "\"{0}\" becomes a step list the builder edits; the graph file goes (Undo brings it back)."), name),
                        () => ConvertGraph(name, File.ReadAllText(path), true));
            }, "mbv-add");
        }

        // ---- the page ---------------------------------------------------------------------------------------------

        void BuildQuestPage()
        {
            questPage.Clear();
            if (currentGraph != null) { BuildGraphPage(currentGraph); return; }
            var q = currentQuest;
            UpdateQuestGraph();
            if (q == null) { Note(questPage, T("mbPickQuest", "Add a quest or a bar mission on the left to start.")); return; }
            if (GraphMode) { BuildGraphInspector(q); return; }
            var top = Row(questPage);
            Act(top, T("mbPlayQuest", "Play it"), () => PlayQuest(q), "mbv-play");
            Act(top, T("mbShowScript", "Show the script"), () => ShowScript(q));
            Act(top, T("mbGraphView", "Graph view"), () => { GraphMode = true; currentStep = null; BuildQuestPage(); });
            BuildQuestSettings(q);

            Heading(questPage, T("mbStepsHeading", "Steps"));
            if (currentStep != null && !ProjectQuest.All(q.Steps, Lists).Any(x => x.step == currentStep)) currentStep = null;
            BuildStepList(questPage, q, q.Steps, "", 0);
            if (q.Steps.Count == 0) Note(questPage, T("mbNoSteps", "No steps: add the first one."));
            var addList = currentStep?.Parent as JArray ?? q.Steps;
            Act(questPage, currentStep != null ? T("mbAddStepAfter", "+ Add a step after it") : T("mbAddStep", "+ Add a step"),
                () => PickStepType(q, addList, currentStep, addList != q.Steps), "mbv-add");
            int looseChains = (q.Json["loose"] as JArray)?.Count ?? 0;
            if (looseChains > 0) Note(questPage, string.Format(T("mbLooseListNote", "{0} chain(s) of steps wired to nothing are only in the graph view."), looseChains));
            Note(questPage, T("mbStepsNote", "The steps run one after another; a block (If, Repeat, Ask...) runs the steps under it. A quest ends at Complete or Fail (without one it completes after the last step). Put a Checkpoint after each step the player shouldn't repeat after loading a save."));
        }

        /// <summary>The quest's own settings: title, kind, start, offer, reward, variables.</summary>
        void BuildQuestSettings(ProjectQuest q)
        {
            Text(questPage, T("mbTitleField", "Title"), q.Title, 60, v => { q.Title = v.Trim(); QuestEdited(); RebuildQuests(); });
            Note(questPage, string.Format(T("mbQuestName", "Its name for other quests and the campaign: {0}"), q.Name));
            string[] kinds = { "quest", "mission", "event" };
            Segments(questPage, T("mbKind", "Kind"), new[] { T("mbQuest", "Quest"), T("mbBarMission", "Bar mission"), T("mbEvent", "Event") }, Array.IndexOf(kinds, q.Kind),
                     n => { q.Kind = kinds[n]; QuestEdited(true); RebuildQuests(); });
            if (q.Kind == "quest") BuildStartsWhen(q);
            else if (q.Kind == "mission") BuildMissionOffer(q);
            else Note(questPage, T("mbEventKindNote", "An event runs for everyone in a multiplayer session when an admin starts it (/event <name>), or when another quest or event reaches a Next event step. Its variables marked Setting can be changed at the start: /event name wave=3."));
            if (q.Kind != "event") Int(questPage, T("mbRewardCredits", "Reward (credits)"), q.Reward, 0, 999999999, v => { q.Reward = v; QuestEdited(); });
            BuildVariables(q);
            if (q.Kind != "event")
                FlagButton(Row(questPage), T("mbAutoComplete", "Completes after its last step"), !(q.Get("autoComplete") is JValue ac && ac.Type == JTokenType.Boolean && !(bool)ac),
                           v => { q.Set("autoComplete", v ? null : (JToken)false); QuestEdited(); });
        }

        void BuildVariables(ProjectQuest q)
        {
            Heading(questPage, T("mbVariables", "Variables"));
            foreach (var v in q.Variables.OfType<JObject>().ToList())
            {
                var o = v;
                var box = Add(questPage, new VisualElement(), "mbv-page-box");
                var row = Row(box);
                Add(row, new Label((string)o["name"] ?? "?"), "mbv-inline-label");
                Act(row, "✕", () => { o.Remove(); q.TidyVariables(); QuestEdited(true); }, "mbv-step");
                Text(box, T("mbVariableStart", "Starts at (an expression)"), o["value"]?.ToString() ?? "0", 120, s => { o["value"] = Num(s); QuestEdited(); });
                FlagButton(Row(box), T("mbVariableSetting", "Setting (/event name=value changes it)"), o["setting"] is JValue sv && sv.Type == JTokenType.Boolean && (bool)sv,
                           b => { if (b) o["setting"] = true; else o.Remove("setting"); QuestEdited(); });
            }
            Act(questPage, T("mbAddVariable", "+ Variable"), () => AskText(T("mbAddVariable", "+ Variable"), T("mbVariableName", "Name (letters, digits, _)"), "", name =>
            {
                name = name.Trim().ToLowerInvariant();
                if (!QuestFile.IsName(name)) { Status(T("mbBadVariable", "A variable's name: letters, digits and _, not starting with a digit."), true); return; }
                if (q.Variables.OfType<JObject>().Any(x => (string)x["name"] == name)) return;
                q.Variables.Add(new JObject { ["name"] = name, ["value"] = 0 });
                QuestEdited(true);
            }), "mbv-add");
            Note(questPage, T("mbVariablesNote", "Numbers the quest remembers: Set a variable changes one, conditions read it (kills > goal), a text shows it in braces: \"Wave {wave}\"."));
        }

        static JToken Num(string s) =>
            double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (Math.Abs(d - Math.Round(d)) < 1e-9 ? (JToken)(long)Math.Round(d) : d) : (JToken)(s ?? "").Trim();

        // ---- the step tree ------------------------------------------------------------------------------------

        void BuildStepList(VisualElement parent, ProjectQuest q, JArray list, string prefix, int depth)
        {
            var steps = list.OfType<JObject>().ToList();
            for (int i = 0; i < steps.Count; i++) StepCard(parent, q, steps[i], prefix + (i + 1), depth);
        }

        void StepCard(VisualElement parent, ProjectQuest q, JObject st, string number, int depth)
        {
            string type = (string)st["type"] ?? "";
            var def = QuestSteps.Get(type);
            bool open = st == currentStep;
            var card = Add(parent, new VisualElement(), "mbv-step-card");
            card.EnableInClassList("mbv-step-card--on", open);
            card.EnableInClassList("mbv-step-card--block", def != null && (def.blocks.Length > 0 || def.answers || def.branches));
            var head = new Button(() => { sound(false); currentStep = open ? null : st; BuildQuestPage(); }) { focusable = false };
            head.AddToClassList("mbv-step-head");
            Add(head, new Label($"{number}. {StepName(type)}") { pickingMode = PickingMode.Ignore }, "mbv-step-name", "gof-semibold");
            string summary = StepSummary(st);
            if (summary.Length > 0) Add(head, new Label(summary) { pickingMode = PickingMode.Ignore }, "mbv-step-summary");
            card.Add(head);
            if (open)
            {
                var tools = Row(card);
                Act(tools, "▲", () => { q.MoveStep(st, -1); QuestEdited(true); }, "mbv-step");
                Act(tools, "▼", () => { q.MoveStep(st, 1); QuestEdited(true); }, "mbv-step");
                Act(tools, T("mbDuplicate", "Copy"), () =>
                {
                    var copy = (JObject)st.DeepClone();
                    ProjectQuest.InsertAfter((JArray)st.Parent, st, copy);
                    currentStep = copy;
                    QuestEdited(true);
                    RebuildQuests();
                }, "mbv-step");
                Act(tools, T("mbDelete", "Delete"), () => { st.Remove(); currentStep = null; QuestEdited(true); RebuildQuests(); }, "mbv-danger");
                if (def == null) Note(card, string.Format(T("mbUnknownStep", "\"{0}\" isn't a step this game knows."), type));
                else BuildStep(card, q, st, def, number, depth);
            }
            // A block's lists, always shown, each with its own "+ Add a step".
            if (def == null) return;
            foreach (var (label, list) in QuestSteps.Children(st))
            {
                var holder = Add(card, new VisualElement(), "mbv-step-children");
                Add(holder, new Label(label.ToUpperInvariant()), "mbv-label");
                BuildStepList(holder, q, list, number + ".", depth + 1);
                var l = list;
                Act(holder, T("mbAddStepHere", "+ Add a step here"), () => PickStepType(q, l, null, true), "mbv-add", "mbv-add--small");
            }
        }

        /// <summary>The step types by group; one goes after 'after' in 'list' (at its end without one).</summary>
        void PickStepType(ProjectQuest q, JArray list, JObject after, bool insideBlock)
        {
            var choices = new List<(string, Texture2D, Action)>();
            foreach (var (gk, gEnglish) in QuestSteps.Groups)
            {
                bool head = false;
                foreach (var t in QuestSteps.Types.Where(x => x.group == gk))
                {
                    if (t.topOnly && insideBlock) continue;
                    if (!head) { choices.Add((T("mbStepGroup_" + gk, gEnglish), null, null)); head = true; }
                    string type = t.type;
                    choices.Add(($"{StepName(type)}\n{T("mbStepWhat_" + type, t.what)}", null, () =>
                    {
                        var st = QuestSteps.New(type);
                        NewStepDefaults(st, type);
                        ProjectQuest.InsertAfter(list, after, st);
                        currentStep = st;
                        QuestEdited(true);
                        RebuildQuests();
                    }));
                }
            }
            ShowPicker(T("mbAddStep", "+ Add a step"), choices, true);
        }

        /// <summary>The step types by group for the graph: 'done' gets the new step; 'cut' adds a first tile that cuts the
        /// wire instead.</summary>
        void PickStepFor(Action<JObject> done, Action cut, bool insideBlock)
        {
            var choices = new List<(string, Texture2D, Action)>();
            if (cut != null) choices.Add((T("mbCutWire", "Cut the wire") + "\n" + T("mbCutWireWhat", "What it led to is wired to nothing"), null, cut));
            foreach (var (gk, gEnglish) in QuestSteps.Groups)
            {
                bool head = false;
                foreach (var t in QuestSteps.Types.Where(x => x.group == gk))
                {
                    if (t.topOnly && insideBlock) continue;
                    if (!head) { choices.Add((T("mbStepGroup_" + gk, gEnglish), null, null)); head = true; }
                    string type = t.type;
                    choices.Add(($"{StepName(type)}\n{T("mbStepWhat_" + type, t.what)}", null, () =>
                    {
                        var st = QuestSteps.New(type);
                        NewStepDefaults(st, type);
                        done(st);
                    }));
                }
            }
            ShowPicker(T("mbAddStep", "+ Add a step"), choices, true);
        }

        void NewStepDefaults(JObject st, string type)
        {
            switch (type)
            {
                case "dialog": st["pages"] = new JArray(new JObject { ["speaker"] = "player", ["text"] = "" }); break;
                case "radio": st["speaker"] = "player"; st["text"] = ""; break;
                case "goto": st["station"] = ProjectEntry.RefToken(Session.StationIndex.ToString()); st["dock"] = true; break;
                case "fight": st["station"] = ProjectEntry.RefToken(Session.StationIndex.ToString()); st["ships"] = new JArray(new JObject { ["ship"] = 2, ["count"] = 3, ["race"] = "pirate" }); st["quiet"] = true; break;
                case "cargo": st["item"] = 22; st["amount"] = 10; break;
                case "reward": st["credits"] = 5000; break;
                case "waypoint": case "questorbit": case "teleport": st["station"] = ProjectEntry.RefToken(Session.StationIndex.ToString()); break;
                case "spawn": st["ship"] = 2; st["race"] = "pirate"; st["count"] = 2; st["name"] = "Raider"; break;
                case "shipgoto": case "shiproute": case "shipfollow": case "shipattack": case "shipaction": st["ship"] = "Raider"; break;
                case "on": st["trigger"] = "died"; break;
                case "ask": case "vote": st["question"] = ""; break;
            }
        }

        string StepSummary(JObject st)
        {
            string S(string k) => st[k] is JValue v && v.Type != JTokenType.Null ? v.ToString(CultureInfo.InvariantCulture) : "";
            string Short(string s) => s.Length > 70 ? s.Substring(0, 67) + "…" : s;
            string type = S("type");
            var def = QuestSteps.Get(type);
            string who = S("who").Length > 0 && S("who") != "@a" ? "  ·  " + WhoText(S("who")) : "";
            switch (type)
            {
                case "dialog":
                    var pages = (st["pages"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
                    return pages.Count == 0 ? "" : Short($"{WhoLabel(S2(pages[0], "speaker"))}: {S2(pages[0], "text")}") + (pages.Count > 1 ? $"  (+{pages.Count - 1})" : "") + who;
                case "radio": return Short($"{WhoLabel(S("speaker"))}: {S("text")}") + who;
                case "objective": return Short(S("text"));
                case "goto": return (st["dock"]?.Type == JTokenType.Boolean && !(bool)st["dock"] ? T("mbToOrbit", "the orbit of ") : T("mbDockAt", "dock at ")) + RefLabel(RefKind.Station, S("station"), "?");
                case "fight":
                    var ships = (st["ships"] as JArray)?.OfType<JObject>().Select(o => $"{(int?)o["count"] ?? 1} × {RefLabel(RefKind.Ship, S2(o, "ship"), "?")}") ?? Enumerable.Empty<string>();
                    return Short(string.Join(", ", ships) + "  ·  " + RefLabel(RefKind.Station, S("station"), T("mbWherever", "wherever the player is")));
                case "cargo": return $"{S("amount")} × {RefLabel(RefKind.Item, S("item"), "?")}";
                case "reward": return $"{S("credits")} $" + ((st["items"] as JArray)?.Count > 0 ? " + " + string.Format(T("mbItemsCount", "{0} items"), ((JArray)st["items"]).Count) : "") + who;
                case "give": return $"{(S("amount").Length > 0 ? S("amount") : "1")} × {RefLabel(RefKind.Item, S("item"), "?")}" + who;
                case "complete": return S("reward").Length > 0 ? S("reward") + " $" : "";
                case "fail": return Short(S("title"));
                case "if": case "while": case "waituntil": return Short(S("condition"));
                case "repeat": return S("count") + " ×" + (S("as").Length > 0 ? "  (" + S("as") + ")" : "");
                case "every": case "wait": return S("seconds") + " s";
                case "on":
                {
                    var f = def?.Field("trigger");
                    int i = f != null ? Array.IndexOf(f.values, S("trigger")) : -1;
                    string t = i >= 0 ? T("mbTrigger_" + S("trigger"), f.labels[i]) : S("trigger");
                    return t + (S("station").Length > 0 ? "  ·  " + RefLabel(RefKind.Station, S("station"), S("station")) : "");
                }
                case "ask": case "vote": return Short(S("question")) + "  (" + ((st["answers"] as JArray)?.Count ?? 0) + ")" + who;
                case "random": return string.Format(T("mbBranchesCount", "{0} branches"), (st["branches"] as JArray)?.Count ?? 0);
                case "set": return $"{S("variable")} = {S("value")}";
                case "note": return Short(S("text"));
                case "command": return Short(S("line"));
            }
            if (def == null) return "";
            // A plain step: its first few values.
            var parts = new List<string>();
            foreach (var f in def.fields)
            {
                if (f.key == "who" || !f.Shown(st) || !(st[f.key] is JValue v) || v.Type == JTokenType.Null) continue;
                string s = v.ToString(CultureInfo.InvariantCulture);
                if (s.Length == 0) continue;
                switch (f.kind)
                {
                    case QuestFieldKind.Station: s = RefLabel(RefKind.Station, s, s); break;
                    case QuestFieldKind.Ship: s = RefLabel(RefKind.Ship, s, s); break;
                    case QuestFieldKind.Item: s = RefLabel(RefKind.Item, s, s); break;
                    case QuestFieldKind.Bool: s = (bool)v ? T("mb_" + f.key, f.english) : ""; break;
                    case QuestFieldKind.Choice:
                    case QuestFieldKind.Race:
                        int i = Array.IndexOf(f.values, s);
                        if (i >= 0 && i < f.labels.Length) s = f.labels[i];
                        break;
                }
                if (s.Length > 0) parts.Add(s);
                if (parts.Count >= 3) break;
            }
            return Short(string.Join("  ·  ", parts)) + who;
        }

        static string S2(JObject o, string k) => o[k] is JValue v && v.Type != JTokenType.Null ? v.ToString() : "";

        /// <summary>A speaker for people: a character's name ("Captain Vega"), a story speaker's, "Keith (the player)".</summary>
        string WhoLabel(string speaker) => string.IsNullOrEmpty(speaker) ? "?" : RefLabel(RefKind.Character, speaker, speaker);

        static readonly (string value, string english)[] WhoChoices =
        {
            ("@a", "Everyone the quest is for"), ("@trigger", "The player a When block is about"), ("@alive", "Players alive in space"),
            ("@docked", "Players docked"), ("@survivors", "Players never destroyed since the fight began"), ("@r", "A random player"),
        };

        static string WhoText(string who) => WhoChoices.FirstOrDefault(w => w.value == who) is var c && c.value != null ? T("mbWho_" + c.value.TrimStart('@'), c.english) : who;

        // ---- one step's fields ----------------------------------------------------------------------------------

        void BuildStep(VisualElement card, ProjectQuest q, JObject st, QuestStepType def, string number, int depth)
        {
            if (!string.IsNullOrEmpty(def.what)) Note(card, T("mbStepWhat_" + def.type, def.what));
            foreach (var f in def.fields)
            {
                if (def.custom && f.key != "who") continue;
                if (!f.Shown(st)) continue;
                BuildField(card, q, st, f);
            }
            if (def.answers) BuildAnswers(card, st);
            if (def.branches) BuildBranches(card, st);
            if (def.custom) BuildCustomStep(card, q, st, number);
            else switch (def.type)
            {
                case "camera":
                    Note(card, T("mbPlaceNote", "A place: player [right up forward] (game units from the ship), station [x y z], x y z (in the orbit), ship <name> [x y z]. 20 units are a metre."));
                    break;
                case "shipgoto": case "shiproute": case "shipfollow": case "shipattack": case "shipaction":
                    Note(card, T("mbShipOrderNote", "The ship is named by a Spawn ships or Fight step (its name on the lock plate; several are \"Name 1\", \"Name 2\"...). Places: player [right up forward], station [x y z], x y z, ship <name>."));
                    break;
                case "on":
                    Note(card, T("mbOnNote", "Its steps run each time it happens, beside the rest of the quest; in them, For: \"The player a When block is about\" is the one it happened to."));
                    break;
                case "command":
                    Note(card, T("mbCommandNote", "Any event command without the \"/\": spawn @a 2 pirate 3 enemy named Raider, npc @a Raider attack player, cutscene @a start, ..."));
                    break;
            }
        }

        void BuildField(VisualElement card, ProjectQuest q, JObject st, QuestField f)
        {
            string label = T("mbField_" + f.key, f.english);
            string S() => st[f.key] is JValue v && v.Type != JTokenType.Null ? v.ToString(CultureInfo.InvariantCulture) : null;
            string Cur() => S() ?? (f.def is JValue d && d.Type != JTokenType.Null ? d.ToString(CultureInfo.InvariantCulture) : null);
            void Set(JToken v, bool rebuild)
            {
                if (v == null || v.Type == JTokenType.Null || v.Type == JTokenType.String && ((string)v).Length == 0) st.Remove(f.key); else st[f.key] = v;
                QuestEdited(rebuild);
            }
            // Wired to a value node in the graph: the wire, not a field (✕ cuts it).
            if (QuestGraph.WiredValue(st[f.key]) is string wiredId)
            {
                Caption(card, label);
                var wrow = Row(card);
                var src = currentQuest != null ? QuestGraph.Value(currentQuest.Json, wiredId) : null;
                var probs = new List<string>();
                string expr = currentQuest != null ? QuestGraph.Resolve(currentQuest.Json, wiredId, probs.Add) : "?";
                Add(wrow, new Label($"◆ {(src != null ? T("mbValue_" + (string)src["type"], QuestGraph.GetValueType((string)src["type"])?.english ?? (string)src["type"]) : "?")}: {expr}"), "mbv-inline-label");
                Act(wrow, "✕", () => Set(null, true), "mbv-step");
                return;
            }
            switch (f.kind)
            {
                case QuestFieldKind.Who:
                {
                    Caption(card, label);
                    var row = Row(card);
                    Add(row, new Label(WhoText(Cur() ?? "@a")), "mbv-inline-label");
                    Act(row, T("mbChoose", "Choose…"), () =>
                    {
                        var choices = WhoChoices.Select(w => (T("mbWho_" + w.value.TrimStart('@'), w.english) + "\n" + w.value, (Texture2D)null, (Action)(() => Set(w.value == "@a" ? null : w.value, true)))).ToList();
                        choices.Add((T("mbWhoCustom", "A selector…") + "\n@alive[orbit=78]", null, () =>
                            AskText(label, T("mbSelector", "A selector (@a, @alive[orbit=Var Hastra] ...)"), Cur() ?? "@a", v => Set(v.Trim() == "@a" ? null : v.Trim(), true), 80)));
                        ShowPicker(label, choices);
                    }, "mbv-step");
                    break;
                }
                case QuestFieldKind.Text:
                case QuestFieldKind.Variable:
                case QuestFieldKind.Xyz:
                case QuestFieldKind.Place:
                    Text(card, label, Cur(), 160, v => Set(v.Trim(), false));
                    break;
                case QuestFieldKind.LongText:
                    Text(card, label, Cur(), 600, v => Set(v, false), true);
                    break;
                case QuestFieldKind.Expression:
                    Text(card, label, Cur(), 200, v => Set(Num(v), false));
                    break;
                case QuestFieldKind.Condition:
                    ConditionField(card, label, Cur(), v => Set(v, false));
                    break;
                case QuestFieldKind.Int:
                case QuestFieldKind.Float:
                    if (f.optional || f.kind == QuestFieldKind.Float && (f.min < -1e8f || f.max > 1e8f) && f.min == float.MinValue)
                    {
                        var tf = Text(card, label, Cur(), 40, v => Set(string.IsNullOrWhiteSpace(v) ? null : Num(v), false));
                        tf.textEdition.placeholder = T("mbDefault", "default");
                    }
                    else if (f.kind == QuestFieldKind.Int)
                    {
                        int.TryParse(Cur() ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv);
                        Int(card, label, iv, (int)Math.Max(f.min, int.MinValue / 2), (int)Math.Min(f.max, int.MaxValue / 2), v => Set(v, false));
                    }
                    else
                    {
                        float.TryParse(Cur() ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out float fv);
                        Float(card, label, fv, Math.Max(f.min, -1e9f), Math.Min(f.max, 1e9f), v => Set(Math.Round(v, 3), false));
                    }
                    break;
                case QuestFieldKind.Bool:
                {
                    bool on = st[f.key] is JValue b && b.Type == JTokenType.Boolean ? (bool)b : f.def is JValue d && d.Type == JTokenType.Boolean && (bool)d;
                    FlagButton(Row(card), label, on, v => Set(v, false), () => BuildQuestPage());
                    break;
                }
                case QuestFieldKind.Choice:
                case QuestFieldKind.Race:
                {
                    int cur = Math.Max(0, Array.IndexOf(f.values, Cur() ?? ""));
                    var labels = f.labels.Select((l, i) => T("mbChoice_" + f.key + "_" + f.values[i], l)).ToArray();
                    if (f.values.Length <= 5 || f.kind == QuestFieldKind.Race)
                        Segments(card, label, labels, cur, n => Set(f.values[n].Length == 0 ? null : f.values[n], true));
                    else
                    {
                        Caption(card, label);
                        var row = Row(card);
                        Add(row, new Label(labels[cur]), "mbv-inline-label");
                        Act(row, T("mbChoose", "Choose…"), () => ShowPicker(label, f.values.Select((v, i) =>
                            (labels[i], (Texture2D)null, (Action)(() => Set(v, true)))).ToList()), "mbv-step");
                    }
                    break;
                }
                case QuestFieldKind.Station:
                    RefField(card, label, RefKind.Station, Cur(), v => Set(string.IsNullOrEmpty(v) ? null : ProjectEntry.RefToken(v), true), f.optional ? T("mbNone", "none") : null, f.optional);
                    break;
                case QuestFieldKind.Ship:
                    RefField(card, label, RefKind.Ship, Cur(), v => Set(string.IsNullOrEmpty(v) ? null : ProjectEntry.RefToken(v), true), null, f.optional);
                    break;
                case QuestFieldKind.Item:
                    RefField(card, label, RefKind.Item, Cur(), v => Set(string.IsNullOrEmpty(v) ? null : ProjectEntry.RefToken(v), true), null, f.optional);
                    break;
                case QuestFieldKind.Quest:
                    RefField(card, label, RefKind.Quest, Cur(), v => Set(v, true), clear: false, filter: e => e.value != q.Name);
                    break;
                case QuestFieldKind.Event:
                {
                    Caption(card, label);
                    var row = Row(card);
                    Add(row, new Label(Cur() ?? T("mbNone", "none")), "mbv-inline-label");
                    Act(row, T("mbChoose", "Choose…"), () =>
                    {
                        var choices = new List<(string, Texture2D, Action)> { (T("mbFromThisMod2", "This mod"), null, null) };
                        foreach (var other in Project.Quests.Where(x => x != q)) { string n = other.Name; choices.Add(((other.Title ?? n) + "\n" + n, null, () => Set(n, true))); }
                        foreach (var g in Project.EventGraphs()) { string n = g; choices.Add((n, null, () => Set(n, true))); }
                        choices.Add((T("mbFromGame", "The game"), null, null));
                        foreach (var b in EventRunner.List().Where(x => !Project.Quests.Any(o => o.Name == x) && !Project.EventGraphs().Contains(x))) { string n = b; choices.Add((n, null, () => Set(n, true))); }
                        ShowPicker(label, choices, true);
                    }, "mbv-step");
                    break;
                }
                case QuestFieldKind.Speaker:
                    SpeakerField(card, Cur(), v => Set(v, true), f.optional);
                    break;
                case QuestFieldKind.Music:
                {
                    Caption(card, label);
                    var row = Row(card);
                    Add(row, new Label(Cur() ?? "stop"), "mbv-inline-label");
                    Act(row, T("mbChoose", "Choose…"), () => PickMusicWord(v => Set(v, true)), "mbv-step");
                    break;
                }
                case QuestFieldKind.Sound:
                {
                    Caption(card, label);
                    var row = Row(card);
                    Add(row, new Label(Cur() ?? "?"), "mbv-inline-label");
                    Act(row, T("mbChoose", "Choose…"), () => ShowPicker(label, Enum.GetNames(typeof(EventSound)).Select(n =>
                        (n, (Texture2D)null, (Action)(() => Set(n.ToLowerInvariant(), true)))).ToList()), "mbv-step");
                    break;
                }
                case QuestFieldKind.ShipName:
                {
                    Text(card, label, Cur(), 32, v => Set(v.Trim(), false));
                    var names = ShipNames(q);
                    if (names.Count > 0)
                    {
                        var row = Row(card);
                        foreach (var n in names.Take(6)) { string nm = n; Act(row, nm, () => Set(nm, true), "mbv-step"); }
                    }
                    break;
                }
            }
            if (!string.IsNullOrEmpty(f.help)) Note(card, T("mbHelp_" + f.key, f.help));
        }

        /// <summary>The ship names the quest's Spawn and Fight steps give (for the ship orders).</summary>
        static List<string> ShipNames(ProjectQuest q)
        {
            var names = new List<string>();
            int enemies = 0;
            foreach (var (st, _) in ProjectQuest.All(q.Steps, Lists))
            {
                string type = (string)st["type"];
                if (type != "spawn" && type != "fight") continue;
                string n = (string)st["name"];
                if (string.IsNullOrWhiteSpace(n) && type == "fight") n = "Quest Enemy " + ++enemies;
                else if (type == "fight") enemies++;
                if (!string.IsNullOrWhiteSpace(n) && !names.Contains(n.Trim())) names.Add(n.Trim());
            }
            return names;
        }

        void BuildAnswers(VisualElement card, JObject st)
        {
            var answers = st["answers"] as JArray ?? (JArray)(st["answers"] = new JArray());
            Caption(card, T("mbAnswers", "Answers (2 to 4; each runs the steps under it)"));
            int k = 0;
            foreach (var a in answers.OfType<JObject>().ToList())
            {
                var o = a;
                int n = ++k;
                var row = Row(card);
                Text(row, n + ".", (string)o["text"], 60, v => { o["text"] = v; QuestEdited(); });
                if (answers.Count > 2) Act(row, "✕", () => { o.Remove(); QuestEdited(true); }, "mbv-step");
            }
            if (answers.Count < 4) Act(card, T("mbAddAnswer", "+ Answer"), () => { answers.Add(new JObject { ["text"] = "", ["steps"] = new JArray() }); QuestEdited(true); }, "mbv-add");
        }

        void BuildBranches(VisualElement card, JObject st)
        {
            var branches = st["branches"] as JArray ?? (JArray)(st["branches"] = new JArray());
            Caption(card, T("mbBranches", "Branches: one runs, the bigger its weight the likelier"));
            int k = 0;
            foreach (var b in branches.OfType<JObject>().ToList())
            {
                var o = b;
                int n = ++k;
                var row = Row(card);
                Float(row, string.Format(T("mbBranchWeight", "{0}: weight"), n), o["weight"] is JValue w && (w.Type == JTokenType.Integer || w.Type == JTokenType.Float) ? (float)w : 1f, 0f, 1000f,
                      v => { o["weight"] = Math.Round(v, 3); QuestEdited(); });
                if (branches.Count > 1) Act(row, "✕", () => { o.Remove(); QuestEdited(true); }, "mbv-step");
            }
            Act(card, T("mbAddBranch", "+ Branch"), () => { branches.Add(new JObject { ["weight"] = 1, ["steps"] = new JArray() }); QuestEdited(true); }, "mbv-add");
        }

        /// <summary>The steps whose fields need their own page (pages, ships, items...).</summary>
        void BuildCustomStep(VisualElement card, ProjectQuest q, JObject st, string number)
        {
            string type = (string)st["type"];
            string S(string k) => st[k] is JValue v && v.Type != JTokenType.Null ? v.ToString() : null;
            void SetS(string k, string v) { if (string.IsNullOrEmpty(v)) st.Remove(k); else st[k] = v; QuestEdited(); }
            void SetRef(string k, string v) { if (string.IsNullOrEmpty(v)) st.Remove(k); else st[k] = ProjectEntry.RefToken(v); QuestEdited(true); }
            int I(string k, int d) => st[k] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (int)Math.Round((double)v) : d;
            string clipBase = q.Name + "_" + number.Replace('.', '_');
            switch (type)
            {
                case "dialog":
                {
                    var pages = st["pages"] as JArray ?? (JArray)(st["pages"] = new JArray());
                    int pn = 0;
                    foreach (var page in pages.OfType<JObject>().ToList())
                    {
                        var p = page;
                        int pi = pn++;
                        var box = Add(card, new VisualElement(), "mbv-page-box");
                        var prow = Row(box);
                        Add(prow, new Label(string.Format(T("mbPageN", "Page {0}"), pi + 1)), "mbv-inline-label");
                        if (pages.Count > 1) Act(prow, "✕", () => { p.Remove(); QuestEdited(true); }, "mbv-step");
                        SpeakerField(box, (string)p["speaker"], v => { p["speaker"] = v; QuestEdited(true); });
                        Text(box, T("mbLine", "Text"), (string)p["text"], 600, v => { p["text"] = v; QuestEdited(); }, true);
                        VoiceField(box, clipBase + "_" + (pi + 1), (string)p["voice"], v => { if (v == null) p.Remove("voice"); else p["voice"] = v; QuestEdited(true); });
                    }
                    Act(card, T("mbAddPage", "+ Page"), () =>
                    {
                        string last = pages.OfType<JObject>().LastOrDefault()?["speaker"]?.ToString() ?? "player";
                        pages.Add(new JObject { ["speaker"] = last, ["text"] = "" });
                        QuestEdited(true);
                    }, "mbv-add");
                    Note(card, T("mbDialogNote", "%player% in a text is the player's name; {variable} shows a variable."));
                    break;
                }
                case "radio":
                    SpeakerField(card, S("speaker"), v => SetS("speaker", v));
                    Text(card, T("mbLine", "Text"), S("text"), 300, v => SetS("text", v), true);
                    VoiceField(card, clipBase, S("voice"), v => { SetS("voice", v); BuildQuestPage(); });
                    break;
                case "objective":
                    Text(card, T("mbObjectiveText", "Objective"), S("text"), 300, v => SetS("text", v), true);
                    RefField(card, T("mbTargetStation", "Target on the map"), RefKind.Station, S("station"), v => SetRef("station", v), T("mbNoTarget", "none"));
                    break;
                case "goto":
                    RefField(card, T("mbStation", "Station"), RefKind.Station, S("station"), v => SetRef("station", v), clear: false);
                    Segments(card, null, new[] { T("mbDocked", "Docked there"), T("mbInOrbit", "In its orbit") }, st["dock"]?.Type == JTokenType.Boolean && !(bool)st["dock"] ? 1 : 0,
                             n => { st["dock"] = n == 0; QuestEdited(true); });
                    Text(card, T("mbObjectiveOptional", "Objective meanwhile (optional)"), S("objective"), 300, v => SetS("objective", v));
                    break;
                case "fight":
                {
                    RefField(card, T("mbInOrbitOf", "In the orbit of"), RefKind.Station, S("station"), v => SetRef("station", v), T("mbWherever", "wherever the player is"));
                    var ships = st["ships"] as JArray ?? (JArray)(st["ships"] = new JArray());
                    foreach (var sh in ships.OfType<JObject>().ToList())
                    {
                        var o = sh;
                        var box = Add(card, new VisualElement(), "mbv-page-box");
                        var srow = Row(box);
                        Add(srow, new Label(RefLabel(RefKind.Ship, S2(o, "ship"), "?")), "mbv-inline-label");
                        Act(srow, T("mbChoose", "Choose…"), () => PickRef(RefKind.Ship, T("mbShip", "Ship"), e => { o["ship"] = ProjectEntry.RefToken(e.value); QuestEdited(true); }), "mbv-step");
                        Act(srow, "✕", () => { o.Remove(); QuestEdited(true); }, "mbv-step");
                        Int(box, T("mbCount", "How many"), o["count"] is JValue c ? (int)c : 1, 1, 10, v => { o["count"] = v; QuestEdited(); });
                        string race = (string)o["race"] ?? "";
                        string[] races = { "", "pirate", "terran", "vossk", "nivelian", "midorian", "void" };
                        Segments(box, T("mbFlownBy", "Flown by"), new[] { T("mbItsMaker", "its maker"), "Pirates", "Terran", "Vossk", "Nivelian", "Midorian", "Void" },
                                 Math.Max(0, Array.IndexOf(races, race)), n => { if (n == 0) o.Remove("race"); else o["race"] = races[n]; QuestEdited(true); });
                        string beh = (string)o["behaviour"] ?? "enemy";
                        string[] behs = { "enemy", "neutral", "friendly" };
                        Segments(box, null, new[] { T("mbEnemy", "Enemy"), T("mbNeutral", "Neutral"), T("mbFriendly", "Friendly") }, Math.Max(0, Array.IndexOf(behs, beh)),
                                 n => { if (n == 0) o.Remove("behaviour"); else o["behaviour"] = behs[n]; QuestEdited(true); });
                    }
                    Act(card, T("mbAddShips", "+ Ships"), () => PickRef(RefKind.Ship, T("mbShip", "Ship"), e => { ships.Add(new JObject { ["ship"] = ProjectEntry.RefToken(e.value), ["count"] = 1 }); QuestEdited(true); }), "mbv-add");
                    Text(card, T("mbShipsName", "Their name (on the lock plate; ship orders use it)"), S("name"), 32, v => SetS("name", v));
                    var frow = Row(card);
                    FlagButton(frow, T("mbQuietOrbit", "No other traffic there"), st["quiet"]?.Type != JTokenType.Boolean || (bool)st["quiet"], v => st["quiet"] = v, () => BuildQuestPage());
                    Text(card, T("mbObjectiveOptional", "Objective meanwhile (optional)"), S("objective"), 300, v => SetS("objective", v));
                    Note(card, T("mbFightNote", "They come in front of the player a few seconds after the player reaches the orbit; the step ends when none of them is left."));
                    break;
                }
                case "cargo":
                    RefField(card, T("mbItem", "Item"), RefKind.Item, S("item"), v => SetRef("item", v), clear: false);
                    Int(card, T("mbAmount", "Amount"), I("amount", 1), 1, 100000, v => { st["amount"] = v; QuestEdited(); });
                    RefField(card, T("mbDockedAtOptional", "Docked at (optional)"), RefKind.Station, S("station"), v => SetRef("station", v), T("mbAnywhere", "anywhere"));
                    Text(card, T("mbObjectiveOptional", "Objective meanwhile (optional)"), S("objective"), 300, v => SetS("objective", v));
                    break;
                case "reward":
                {
                    Int(card, T("mbCredits", "Credits"), I("credits", 0), 0, 999999999, v => { st["credits"] = v; QuestEdited(); });
                    var items = (st["items"] as JArray)?.OfType<JObject>().Select(o => new ProjectStack(ProjectEntry.RefText(o["item"]), (int?)o["amount"] ?? 1)).ToList() ?? new List<ProjectStack>();
                    StackList(card, T("mbItems", "Items"), items, l => { st["items"] = ProjectStack.Write(l); if (l.Count == 0) st.Remove("items"); QuestEdited(true); }, false);
                    Text(card, T("mbBoxTitle", "Title of the box (optional)"), S("title"), 60, v => SetS("title", v));
                    break;
                }
                case "checkpoint":
                    Text(card, T("mbCheckpointName", "Name (optional; a save remembers it)"), S("name"), 40, v => SetS("name", v?.Trim().Replace(' ', '_')));
                    Note(card, T("mbCheckpointNote", "Loading a save goes on from here: what came before isn't played again. Keep checkpoints on the quest's own line of steps."));
                    break;
                case "startquest":
                    RefField(card, T("mbQuestField", "Quest"), RefKind.Quest, S("quest"), v => SetS("quest", v), clear: false, filter: e => e.value != q.Name);
                    break;
                case "complete":
                    Int(card, T("mbRewardOwn", "Reward (-1 = the quest's)"), I("reward", -1), -1, 999999999, v => { if (v < 0) st.Remove("reward"); else st["reward"] = v; QuestEdited(); });
                    Text(card, T("mbBoxTitle", "Title of the box (optional)"), S("title"), 60, v => SetS("title", v));
                    break;
                case "fail":
                    Text(card, T("mbFailText", "What the player is told"), S("title"), 120, v => SetS("title", v));
                    break;
            }
        }

        /// <summary>A speaker: a character (the project's own, the game's story characters, other mods'), or "race name".</summary>
        void SpeakerField(VisualElement parent, string value, Action<string> set, bool clear = false)
        {
            Caption(parent, T("mbSpeaker", "Speaker"));
            var row = Row(parent);
            Add(row, new Label(string.IsNullOrEmpty(value) ? T("mbNone", "none") : WhoLabel(value)), "mbv-inline-label");
            Act(row, T("mbChoose", "Choose…"), () => PickRef(RefKind.Character, T("mbSpeaker", "Speaker"), e => set(e.value)), "mbv-step");
            Act(row, T("mbRaceAndName", "A race and a name…"), () => AskText(T("mbRaceAndName", "A race and a name…"), T("mbRaceNameField", "e.g. vossk K'ekki, terran female Jane"), value, set, 60), "mbv-step");
            if (clear && !string.IsNullOrEmpty(value)) Act(row, "✕", () => set(null), "mbv-step");
        }

        /// <summary>A page's voice: a sound file of the project (voices/), or the game's voice line by its name.</summary>
        void VoiceField(VisualElement parent, string clip, string value, Action<string> set)
        {
            var row = Row(parent);
            Add(row, new Label($"{T("mbVoice", "Voice")}: {value ?? T("mbNone", "none")}"), "mbv-inline-label");
            Act(row, T("mbVoiceFile", "Sound file…"), () => PickAudio(T("mbPickVoice", "Choose the voice line"), path =>
            {
                Project.SetAudio(ModProject.VoicesFolder, clip, path);
                set(clip);
            }), "mbv-step");
            Act(row, T("mbGameVoice", "Game's line…"), () => AskText(T("mbGameVoice", "Game's line…"), T("mbVoiceName", "The voice line's name (voices.md)"), value, set, 80), "mbv-step");
            if (value != null) Act(row, "✕", () => set(null), "mbv-step");
        }

        static readonly string[] StartModes = { "At once", "After a quest", "After a visit", "After a story step", "Never", "Condition" };

        void BuildStartsWhen(ProjectQuest q)
        {
            string w = q.StartsWhen.Trim();
            int mode = w.Length == 0 ? 0 : w == "never" ? 4 : Matches(w, @"^quest\((.+)\)\s*==\s*2$", out string qa) ? 1
                     : Matches(w, @"^visited\((.+)\)$", out string va) ? 2 : Matches(w, @"^campaign\s*>=\s*(\d+)$", out string ca) ? 3 : 5;
            Segments(questPage, T("mbStartsWhen", "Starts"), StartModes.Select(m => T("mbStart_" + m.Replace(" ", ""), m)).ToArray(), mode, n =>
            {
                q.StartsWhen = n switch { 0 => "", 4 => "never", 1 => $"quest({Project.Quests.FirstOrDefault(o => o != q)?.Name ?? "my_quest"}) == 2",
                                          2 => $"visited({Session.StationIndex})", 3 => "campaign >= 20", _ => w };
                QuestEdited(true);
            });
            switch (mode)
            {
                case 1:
                    Matches(w, @"^quest\((.+)\)\s*==\s*2$", out string after);
                    RefField(questPage, T("mbAfterQuest", "When this quest is done"), RefKind.Quest, after, v => { if (v != null) { q.StartsWhen = $"quest({v}) == 2"; QuestEdited(true); } }, clear: false,
                             filter: e => e.value != q.Name);
                    break;
                case 2:
                    Matches(w, @"^visited\((.+)\)$", out string st);
                    RefField(questPage, T("mbAfterVisit", "Once the player has docked at"), RefKind.Station, st, v => { if (v != null) { q.StartsWhen = $"visited({v})"; QuestEdited(true); } }, clear: false);
                    break;
                case 3:
                    Matches(w, @"^campaign\s*>=\s*(\d+)$", out string step);
                    Int(questPage, T("mbStoryStep", "From the story step"), int.TryParse(step, out int s) ? s : 20, 0, 162, v => { q.StartsWhen = $"campaign >= {v}"; QuestEdited(); });
                    Note(questPage, T("mbStoryStepNote", "The GoF2 story's step (Debug menu's list); in free play and mod campaigns it is -1, so such a quest waits for the story."));
                    break;
                case 5:
                    ConditionField(questPage, T("mbCondition", "Condition"), w, v => { q.StartsWhen = v ?? ""; QuestEdited(); });
                    break;
            }
        }

        static bool Matches(string s, string pattern, out string group)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s ?? "", pattern);
            group = m.Success ? m.Groups[1].Value.Trim() : null;
            return m.Success;
        }

        void BuildMissionOffer(ProjectQuest q)
        {
            Text(questPage, T("mbOffer", "What the visitor says"), q.Offer, 600, v => { q.Offer = v; QuestEdited(); }, true);
            RefField(questPage, T("mbClient", "The client"), RefKind.Character, q.Client, v => { q.Client = v; QuestEdited(true); }, T("mbClientAny", "someone of the station's race"));
            Caption(questPage, T("mbOfferedAt", "Offered at"));
            var stations = q.Stations;
            var row = Row(questPage);
            Add(row, new Label(stations.Count == 0 ? T("mbEveryStation", "every station") : string.Join(", ", stations.Select(s => RefLabel(RefKind.Station, s, s)))), "mbv-inline-label");
            Act(row, "+", () => PickRef(RefKind.Station, T("mbOfferedAt", "Offered at"), e => { var l = q.Stations; if (!l.Contains(e.value)) l.Add(e.value); q.Stations = l; QuestEdited(true); }), "mbv-step");
            if (stations.Count > 0) Act(row, "✕", () => { q.Stations = null; QuestEdited(true); }, "mbv-step");
            var players = q.Get("players") as JArray;
            int min = players != null && players.Count > 0 ? (int)players[0] : 1, max = players != null && players.Count > 1 ? (int)players[1] : 4;
            var prow = Row(questPage, "mbv-xyz");
            Int(prow, T("mbMinPilots", "Pilots: at least"), min, 1, 16, v => { q.Set("players", new JArray(v, Math.Max(v, max))); QuestEdited(); });
            Int(prow, T("mbMaxPilots", "at most"), max, 1, 16, v => { q.Set("players", new JArray(Math.Min(min, v), v)); QuestEdited(); });
        }

        /// <summary>A track for a Music step: the game's (the event words) or a mod's.</summary>
        void PickMusicWord(Action<string> pick)
        {
            var choices = new List<(string, Texture2D, Action)> { (T("mbStopMusic", "Stop the music"), null, () => pick("stop")), (T("mbFromGame", "The game"), null, null) };
            foreach (var m in Enum.GetNames(typeof(EventMusic))) { string w = m.ToLowerInvariant(); choices.Add((m, null, () => pick(w))); }
            string group = null;
            foreach (var e in ModRefs.List(RefKind.Music, Project).Where(e => e.group != ModRefs.GameGroup))
            {
                if (e.group != group) { group = e.group; choices.Add((e.own ? string.Format(T("mbFromThisMod", "This mod: {0}"), e.group) : string.Format(T("mbFromMod", "Mod: {0} (becomes a dependency)"), e.group), null, null)); }
                var en = e;
                choices.Add((e.name, null, () => { if (ModRefs.Use(Project, en)) RequestReload(false); pick(en.value); }));
            }
            ShowPicker(T("mbMusic", "Music"), choices);
        }

        void ShowScript(ProjectQuest q)
        {
            var problems = new List<string>();
            string script = QuestFile.Compile(q.ToJson(), problems);
            ShowScriptText(script, problems.Select(p => "✖ " + p).ToList());
        }

        void ShowScriptText(string script, List<string> problems)
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(box, T("mbScript", "The script the game runs"));
            foreach (var p in problems) Add(box, new Label(p), "mbv-note", "mbv-warn");
            var scroll = Add(box, new ScrollView(ScrollViewMode.Vertical), "mbv-picker-scroll");
            Add(scroll, new TextField { value = script, multiline = true, isReadOnly = true }, "mbv-field", "mbv-script");
            Act(box, T("mbClose", "Close"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        void PlayQuest(ProjectQuest q)
        {
            var problems = new List<string>();
            QuestFile.Compile(q.ToJson(), problems);
            if (problems.Count > 0) { Status("✖ " + problems[0], true); return; }
            SaveNow();
            ModBuilder.Reload();
            afterReload.Clear();
            string why = ModBuilderPlay.StartQuest(q.Name);
            if (why != null) Status(why, true);
        }
    }
}
