// QuestGraphView.cs
// The Mod Builder's quest graph (ModBuilderViewQuests' Graph view): a .gof2quest drawn as nodes on a canvas the way Graph
// Toolkit draws the Unity Editor's event graphs (Events.QuestGraph: the flow wires are the step tree, the value nodes wire
// into fields). Each step a node with its flow input on the left of its header and its outputs on the right (Next, a block's
// Then / Else / Do, an Ask's answers, a Random's branches); its wirable fields (conditions, numbers, stations, whom it is
// for) have a value input each; value nodes (numbers, the game's state, maths, comparisons, players, orbits) have an output.
//   pointer   drag the background (or with the right / middle button anywhere) to pan; the wheel or + / - zooms about the
//             pointer; drag a header to move a node; drag from an output onto a node to wire it (onto empty canvas: a new
//             step there, or cut the wire); drag a wired input away to cut it or onto another node; drag a value node's
//             output onto a field's input; click selects (the inspector on the right edits it); double-click the canvas
//             for a new step; Delete removes the selected node.
// The owner (ModBuilderView) supplies the names, the pickers and the inspector. Plain UI Toolkit, no MonoBehaviour.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GoF2Remake.Events;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // read-only tables
    public sealed class QuestGraphView : VisualElement
    {
        public const float NodeWidth = 250f, ValueWidth = 210f;
        const float Far = 50000f;

        /// <summary>The quest's JSON (ProjectQuest.json).</summary>
        public JObject quest;
        public Func<string, string> stepName;
        public Func<JObject, string> stepSummary;
        public Func<string, string> fieldText;
        /// <summary>The selection changed (a step, a value node, "start" for the Start node, or null).</summary>
        public Action<object> selected;
        /// <summary>The graph changed: save it and refresh the inspector.</summary>
        public Action edited;
        /// <summary>A new step at a place: the owner's picker calls back with it ('cut': an extra choice that cuts the wire).</summary>
        public Action<Action<JObject>, Action> pickStep;
        public Action<Action<JObject>> pickValue;

        public object Selection { get; private set; }

        readonly VisualElement content, wireLayer, nodeLayer;
        readonly Label zoomLabel;
        Vector2 pan = new Vector2(60f, 60f);
        float zoom = 0.85f;
        readonly Dictionary<object, VisualElement> nodes = new Dictionary<object, VisualElement>();
        readonly Dictionary<(object node, string port), VisualElement> outPorts = new Dictionary<(object, string), VisualElement>();
        readonly Dictionary<object, VisualElement> inPorts = new Dictionary<object, VisualElement>();
        readonly Dictionary<(object node, string field), VisualElement> dataIns = new Dictionary<(object, string), VisualElement>();
        readonly Dictionary<object, VisualElement> dataOuts = new Dictionary<object, VisualElement>();

        enum Drag { None, Pan, Move, Flow, Data }
        Drag drag;
        int pointerId = -1;
        Vector2 dragStart, dragLast, panStart, moveStart;
        object dragNode;
        string dragPort;
        Vector2 dragPointer;   // in wire layer coordinates
        float lastClick;
        bool moved;

        sealed class Tag
        {
            public string what;   // "head", "out", "in", "din", "dout"
            public object node;
            public string port;
        }

        public QuestGraphView()
        {
            AddToClassList("mbg-canvas");
            pickingMode = PickingMode.Position;
            focusable = true;
            generateVisualContent += DrawGrid;
            content = Add(this, new VisualElement { pickingMode = PickingMode.Ignore }, "mbg-content");
            wireLayer = Add(content, new VisualElement { pickingMode = PickingMode.Ignore }, "mbg-wires");
            wireLayer.style.left = -Far;
            wireLayer.style.top = -Far;
            wireLayer.style.width = Far * 2f;
            wireLayer.style.height = Far * 2f;
            wireLayer.generateVisualContent += DrawWires;
            nodeLayer = Add(content, new VisualElement { pickingMode = PickingMode.Ignore }, "mbg-nodes");
            zoomLabel = Add(this, new Label { pickingMode = PickingMode.Ignore }, "mbg-zoom");
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKey);
            ApplyTransform();
        }

        static T Add<T>(VisualElement parent, T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            parent.Add(e);
            return e;
        }

        // ---- building --------------------------------------------------------------------------------------

        /// <summary>Draws the quest again (after any change); new nodes get a place first.</summary>
        public void Rebuild()
        {
            nodeLayer.Clear();
            nodes.Clear();
            outPorts.Clear();
            inPorts.Clear();
            dataIns.Clear();
            dataOuts.Clear();
            if (quest == null) { wireLayer.MarkDirtyRepaint(); return; }
            QuestGraph.Layout(quest, false, EstimateHeight);
            if (Selection is JObject sel && !QuestGraph.AllSteps(quest).Contains(sel) && !IsValue(sel)) Selection = null;
            BuildStart();
            foreach (var st in QuestGraph.AllSteps(quest)) BuildStep(st);
            foreach (var v in QuestGraph.Values(quest).OfType<JObject>()) BuildValue(v);
            QuestGraph.Tidy(quest);
            wireLayer.MarkDirtyRepaint();
        }

        /// <summary>Places every node again (Tidy up).</summary>
        public void Tidy()
        {
            if (quest == null) return;
            QuestGraph.Layout(quest, true, EstimateHeight);
            Rebuild();
            edited?.Invoke();
        }

        /// <summary>Brings the Start node (or the selection) into view.</summary>
        public void Frame()
        {
            if (quest == null) return;
            var (x, y) = Selection is JObject o && QuestGraph.HasPos(o) ? QuestGraph.Pos(o) : QuestGraph.Pos(quest, "startPos");
            pan = new Vector2(300f - x * zoom, 70f - y * zoom);
            ApplyTransform();
        }

        static float EstimateHeight(JObject st)
        {
            var t = QuestSteps.Get((string)st["type"] ?? "");
            int rows = QuestGraph.Outputs(st).Count + (t?.fields.Count(f => Wirable(f) && f.Shown(st)) ?? 0);
            return 64f + rows * 22f;
        }

        static bool Wirable(QuestField f) => f.kind == QuestFieldKind.Condition || f.kind == QuestFieldKind.Expression || f.kind == QuestFieldKind.Int
                                          || f.kind == QuestFieldKind.Float || f.kind == QuestFieldKind.Station || f.kind == QuestFieldKind.Who;

        static QuestGraph.ValueKind KindOf(QuestField f) => f.kind == QuestFieldKind.Who ? QuestGraph.ValueKind.Players
                                                          : f.kind == QuestFieldKind.Station ? QuestGraph.ValueKind.Orbit : QuestGraph.ValueKind.Number;

        VisualElement NodeBox(object key, float x, float y, float width, string title, Color colour, params string[] classes)
        {
            var box = Add(nodeLayer, new VisualElement(), "mbg-node");
            foreach (var c in classes) box.AddToClassList(c);
            box.EnableInClassList("mbg-node--selected", Selection == key || (key is string s && s == "start" && Selection as string == "start"));
            box.style.left = x;
            box.style.top = y;
            box.style.width = width;
            var head = Add(box, new VisualElement(), "mbg-head");
            head.style.backgroundColor = colour;
            head.userData = box.userData = new Tag { what = "head", node = key };   // anywhere on it: select / move
            Add(head, new Label(title) { pickingMode = PickingMode.Ignore }, "mbg-title", "gof-semibold");
            box.RegisterCallback<GeometryChangedEvent>(_ => wireLayer.MarkDirtyRepaint());
            nodes[key] = box;
            return box;
        }

        VisualElement Port(VisualElement parent, string what, object node, string port, bool data, string extra = null)
        {
            var dot = Add(parent, new VisualElement(), "mbg-port", data ? "mbg-port--data" : "mbg-port--flow");
            if (extra != null) dot.AddToClassList(extra);
            dot.userData = new Tag { what = what, node = node, port = port };
            return dot;
        }

        void OutputRow(VisualElement box, object node, string port, string label, bool wired)
        {
            var row = Add(box, new VisualElement(), "mbg-row", "mbg-row--out");
            Add(row, new Label(label) { pickingMode = PickingMode.Ignore }, "mbg-row-label");
            var dot = Port(row, "out", node, port, false, "mbg-port--right");
            dot.EnableInClassList("mbg-port--wired", wired);
            outPorts[(node, port)] = dot;
        }

        void BuildStart()
        {
            var (x, y) = QuestGraph.Pos(quest, "startPos");
            string kind = (string)quest["kind"] ?? "quest";
            var box = NodeBox("start", x, y, NodeWidth, "START", new Color(0.18f, 0.45f, 0.22f), "mbg-node--start");
            Add(box, new Label(((string)quest["title"] ?? "") + "  ·  " + kind) { pickingMode = PickingMode.Ignore }, "mbg-summary");
            OutputRow(box, "start", QuestGraph.Start, "Next", QuestGraph.Root(quest).Count > 0);
        }

        static readonly Dictionary<string, Color> GroupColours = new Dictionary<string, Color>
        {
            { "story", new Color(0.20f, 0.33f, 0.52f) }, { "flow", new Color(0.42f, 0.30f, 0.12f) }, { "places", new Color(0.14f, 0.40f, 0.40f) },
            { "ships", new Color(0.48f, 0.18f, 0.18f) }, { "player", new Color(0.30f, 0.38f, 0.16f) }, { "cutscene", new Color(0.34f, 0.22f, 0.44f) },
            { "screen", new Color(0.24f, 0.28f, 0.36f) }, { "score", new Color(0.44f, 0.36f, 0.12f) }, { "other", new Color(0.28f, 0.28f, 0.28f) },
        };

        void BuildStep(JObject st)
        {
            var def = QuestSteps.Get((string)st["type"] ?? "");
            var (x, y) = QuestGraph.Pos(st);
            var colour = def != null && GroupColours.TryGetValue(def.group, out var c) ? c : new Color(0.3f, 0.3f, 0.3f);
            var box = NodeBox(st, x, y, NodeWidth, (stepName?.Invoke((string)st["type"]) ?? (string)st["type"]).ToUpperInvariant(), colour);
            var (from, port) = QuestGraph.Incoming(st);
            var input = Port(box, "in", st, null, false, "mbg-port--in");
            input.EnableInClassList("mbg-port--wired", port != null);
            inPorts[st] = input;
            box.EnableInClassList("mbg-node--loose", port == null);
            string summary = stepSummary?.Invoke(st) ?? "";
            if (summary.Length > 0) Add(box, new Label(summary) { pickingMode = PickingMode.Ignore }, "mbg-summary");
            if (def != null)
                foreach (var f in def.fields)
                {
                    if (!Wirable(f) || !f.Shown(st) || def.custom && f.key != "who") continue;
                    DataInRow(box, st, f.key, fieldText?.Invoke(f.english) ?? f.english, st[f.key]);
                }
            foreach (var (p, label) in QuestGraph.Outputs(st))
                OutputRow(box, st, p, p == QuestGraph.Next ? "Next" : label, QuestGraph.Target(quest, st, p) != null);
        }

        void DataInRow(VisualElement box, object node, string key, string label, JToken value)
        {
            var row = Add(box, new VisualElement(), "mbg-row", "mbg-row--in");
            var dot = Port(row, "din", node, key, true, "mbg-port--left");
            string wired = QuestGraph.WiredValue(value);
            dot.EnableInClassList("mbg-port--wired", wired != null);
            string shown = wired != null ? "◆" : value is JValue v && v.Type != JTokenType.Null ? v.ToString(CultureInfo.InvariantCulture) : "";
            if (shown.Length > 26) shown = shown.Substring(0, 24) + "…";
            Add(row, new Label(shown.Length > 0 && wired == null ? $"{label}: {shown}" : label) { pickingMode = PickingMode.Ignore }, "mbg-row-label");
            dataIns[(node, key)] = dot;
        }

        void BuildValue(JObject v)
        {
            var t = QuestGraph.GetValueType((string)v["type"] ?? "");
            if (!QuestGraph.HasPos(v)) QuestGraph.SetPos(v, 0, 0);
            var (x, y) = QuestGraph.Pos(v);
            var colour = t?.output == QuestGraph.ValueKind.Players ? new Color(0.16f, 0.36f, 0.30f) : t?.output == QuestGraph.ValueKind.Orbit ? new Color(0.14f, 0.32f, 0.44f) : new Color(0.26f, 0.26f, 0.34f);
            var box = NodeBox(v, x, y, ValueWidth, (fieldText?.Invoke(t?.english ?? (string)v["type"]) ?? (string)v["type"]).ToUpperInvariant(), colour, "mbg-node--value");
            var head = box.Q(className: "mbg-head");
            var outDot = Port(head, "dout", v, null, true, "mbg-port--right");
            outDot.EnableInClassList("mbg-port--wired", quest.Descendants().OfType<JObject>().Any(o => QuestGraph.WiredValue(o) == (string)v["id"]));
            dataOuts[v] = outDot;
            if (t == null) return;
            var opts = string.Join("  ", t.options.Select(o => v[o.key]?.ToString() ?? o.def).Where(s => !string.IsNullOrEmpty(s)));
            if (opts.Length > 0) Add(box, new Label(opts) { pickingMode = PickingMode.Ignore }, "mbg-summary");
            foreach (var inp in t.inputs) DataInRow(box, v, inp.key, fieldText?.Invoke(inp.label) ?? inp.label, v[inp.key]);
        }

        // ---- drawing -------------------------------------------------------------------------------------------

        void DrawGrid(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            float step = 40f * zoom;
            if (step < 8f) return;
            p.strokeColor = new Color(1f, 1f, 1f, 0.04f);
            p.lineWidth = 1f;
            p.BeginPath();
            for (float gx = Mathf.Repeat(pan.x, step); gx < r.width; gx += step) { p.MoveTo(new Vector2(gx, 0)); p.LineTo(new Vector2(gx, r.height)); }
            for (float gy = Mathf.Repeat(pan.y, step); gy < r.height; gy += step) { p.MoveTo(new Vector2(0, gy)); p.LineTo(new Vector2(r.width, gy)); }
            p.Stroke();
        }

        Vector2 Center(VisualElement e) => wireLayer.WorldToLocal(e.worldBound.center);

        static void Curve(Painter2D p, Vector2 a, Vector2 b, Color colour, float width)
        {
            float dx = Mathf.Max(50f, Mathf.Abs(b.x - a.x) * 0.5f);
            p.strokeColor = colour;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(a);
            p.BezierCurveTo(a + new Vector2(dx, 0f), b - new Vector2(dx, 0f), b);
            p.Stroke();
        }

        static readonly Color FlowColour = new Color(0.86f, 0.90f, 0.95f, 0.85f), DataColour = new Color(0.45f, 0.85f, 1f, 0.85f);

        void DrawWires(MeshGenerationContext ctx)
        {
            if (quest == null) return;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            // Flow: each output to the node it leads to.
            foreach (var kv in outPorts)
            {
                var (node, port) = kv.Key;
                var to = node is string ? QuestGraph.Target(quest, null, QuestGraph.Start) : QuestGraph.Target(quest, (JObject)node, port);
                if (to == null || !inPorts.TryGetValue(to, out var dst)) continue;
                if (dragGrab != null && to == dragGrab) continue;   // the wire being moved
                Curve(p, Center(kv.Value), Center(dst), FlowColour, 3f);
            }
            // Data: each wired input from its value node's output.
            foreach (var kv in dataIns)
            {
                var (node, key) = kv.Key;
                string id = QuestGraph.WiredValue(node is JObject o ? o[key] : null);
                if (id == null) continue;
                var v = QuestGraph.Value(quest, id);
                if (v == null || !dataOuts.TryGetValue(v, out var src)) continue;
                Curve(p, Center(src), Center(kv.Value), DataColour, 2f);
            }
            // The wire being dragged.
            if (drag == Drag.Flow || drag == Drag.Data)
            {
                VisualElement from = drag == Drag.Flow ? (dragNode != null && outPorts.TryGetValue((dragNode, dragPort), out var fp) ? fp : null)
                                                       : (dragNode != null && dataOuts.TryGetValue(dragNode, out var dp) ? dp : null);
                if (from != null) Curve(p, Center(from), dragPointer, drag == Drag.Flow ? new Color(1f, 0.8f, 0.3f, 0.95f) : new Color(0.5f, 1f, 1f, 0.95f), 3f);
            }
        }

        void ApplyTransform()
        {
            content.style.translate = new Translate(pan.x, pan.y);
            content.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + " %";
            MarkDirtyRepaint();
            wireLayer.MarkDirtyRepaint();
        }

        // ---- pointer -------------------------------------------------------------------------------------------

        Tag TagAt(VisualElement e)
        {
            for (; e != null && e != this; e = e.parent)
                if (e.userData is Tag t) return t;
            return null;
        }

        object NodeAt(Vector2 panelPos)
        {
            var picked = panel?.Pick(panelPos);
            for (var e = picked; e != null && e != this; e = e.parent)
                foreach (var kv in nodes)
                    if (kv.Value == e) return kv.Key;
            return null;
        }

        Tag PortAt(Vector2 panelPos)
        {
            var picked = panel?.Pick(panelPos);
            return TagAt(picked);
        }

        Vector2 ToCanvas(Vector2 panelPos) => (this.WorldToLocal(panelPos) - pan) / zoom;

        JObject dragGrab;   // a step whose incoming wire is being moved

        void OnDown(PointerDownEvent e)
        {
            if (drag != Drag.None) return;
            Focus();
            var tag = TagAt(e.target as VisualElement);
            pointerId = e.pointerId;
            dragStart = dragLast = e.position;
            moved = false;
            if (e.button == 1 || e.button == 2 || tag == null)
            {
                // A double click on the empty canvas: a new step there.
                if (tag == null && e.button == 0 && Time.unscaledTime - lastClick < 0.35f) { NewStepAt(ToCanvas(e.position)); lastClick = 0f; e.StopPropagation(); return; }
                if (tag == null && e.button == 0) lastClick = Time.unscaledTime;
                drag = Drag.Pan;
                panStart = pan;
            }
            else
            {
                switch (tag.what)
                {
                    case "head":
                        drag = Drag.Move;
                        dragNode = tag.node;
                        moveStart = NodePos(tag.node);
                        Select(tag.node);
                        break;
                    case "out":
                        drag = Drag.Flow;
                        dragNode = tag.node;
                        dragPort = tag.port;
                        dragGrab = null;
                        break;
                    case "in":
                    {
                        var st = (JObject)tag.node;
                        var (from, port) = QuestGraph.Incoming(st);
                        if (port == null) { drag = Drag.Move; dragNode = st; moveStart = NodePos(st); Select(st); break; }
                        drag = Drag.Flow;
                        dragNode = from != null ? (object)from : "start";
                        dragPort = port;
                        dragGrab = st;
                        break;
                    }
                    case "dout":
                        drag = Drag.Data;
                        dragNode = tag.node;
                        break;
                    case "din":
                    {
                        // A wired input picked up: its wire comes off and goes with the pointer.
                        var owner = (JObject)tag.node;
                        string id = QuestGraph.WiredValue(owner[tag.port]);
                        if (id == null) { Select(tag.node); return; }
                        owner.Remove(tag.port);
                        drag = Drag.Data;
                        dragNode = QuestGraph.Value(quest, id);
                        Changed(false);
                        break;
                    }
                }
            }
            dragPointer = wireLayer.WorldToLocal(e.position);
            this.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        Vector2 NodePos(object key)
        {
            if (key is string) { var (x, y) = QuestGraph.Pos(quest, "startPos"); return new Vector2(x, y); }
            var (px, py) = QuestGraph.Pos((JObject)key);
            return new Vector2(px, py);
        }

        void OnMove(PointerMoveEvent e)
        {
            if (drag == Drag.None || e.pointerId != pointerId) return;
            Vector2 pos = e.position;
            if ((pos - dragStart).sqrMagnitude > 16f) moved = true;
            switch (drag)
            {
                case Drag.Pan:
                    pan = panStart + (Vector2)(pos - dragStart);
                    ApplyTransform();
                    break;
                case Drag.Move:
                {
                    var p = moveStart + (Vector2)(pos - dragStart) / zoom;
                    if (dragNode is string) QuestGraph.SetPos(quest, p.x, p.y, "startPos"); else QuestGraph.SetPos((JObject)dragNode, p.x, p.y);
                    if (nodes.TryGetValue(dragNode, out var box)) { box.style.left = Mathf.Round(p.x); box.style.top = Mathf.Round(p.y); }
                    break;
                }
                case Drag.Flow:
                case Drag.Data:
                    dragPointer = wireLayer.WorldToLocal(pos);
                    wireLayer.MarkDirtyRepaint();
                    break;
            }
            dragLast = pos;
        }

        void OnUp(PointerUpEvent e)
        {
            if (drag == Drag.None || e.pointerId != pointerId) return;
            var d = drag;
            var node = dragNode;
            string port = dragPort;
            var grab = dragGrab;
            Vector2 pos = e.position;
            EndDrag();
            switch (d)
            {
                case Drag.Pan:
                    if (!moved && e.button == 0) Select(null);
                    break;
                case Drag.Move:
                    if (moved) edited?.Invoke();
                    break;
                case Drag.Flow:
                {
                    var from = node as JObject;
                    string p = node is string ? QuestGraph.Start : port;
                    var target = NodeAt(pos) as JObject;
                    if (target != null && !IsValue(target) && target.Parent is JArray && QuestSteps.Get((string)target["type"] ?? "") != null)
                    {
                        if (target == grab && moved) { Rebuild(); break; }
                        if (target == grab) break;
                        if (!QuestGraph.Connect(quest, from, p, target)) { Rebuild(); break; }
                        Changed(true);
                    }
                    else if (grab != null)
                    {
                        // A moved wire dropped on nothing: cut.
                        if (moved) { QuestGraph.Detach(quest, grab); Changed(true); }
                        else Select(grab);
                    }
                    else if (moved || target == null)
                    {
                        var at = ToCanvas(pos);
                        bool wired = QuestGraph.Target(quest, from, p) != null;
                        pickStep?.Invoke(st =>
                        {
                            QuestGraph.SetPos(st, at.x, at.y - 18f);
                            QuestGraph.AddLoose(quest, st);
                            QuestGraph.Connect(quest, from, p, st);
                            Selection = st;
                            Changed(true);
                            selected?.Invoke(st);
                        }, wired ? () => { QuestGraph.Cut(quest, from, p); Changed(true); } : (Action)null);
                    }
                    wireLayer.MarkDirtyRepaint();
                    break;
                }
                case Drag.Data:
                {
                    var src = node as JObject;
                    var tag = PortAt(pos);
                    if (src != null && tag != null && tag.what == "din" && tag.node is JObject owner && owner != src && Accepts(owner, tag.port, src))
                    {
                        owner[tag.port] = QuestGraph.Wire((string)src["id"]);
                        Changed(true);
                    }
                    else Rebuild();
                    break;
                }
            }
        }

        /// <summary>A value node's output fits an input: players into whom, orbits / numbers into stations, numbers anywhere
        /// else; never into itself through its own inputs.</summary>
        bool Accepts(JObject owner, string key, JObject value)
        {
            var vt = QuestGraph.GetValueType((string)value["type"] ?? "");
            if (vt == null) return false;
            QuestGraph.ValueKind want;
            if (!IsValue(owner) && QuestSteps.Get((string)owner["type"] ?? "") is QuestStepType st && st.Field(key) is QuestField f) want = KindOf(f);
            else if (QuestGraph.GetValueType((string)owner["type"] ?? "") is QuestGraph.ValueType ot && ot.inputs.Any(i => i.key == key)) want = ot.inputs.First(i => i.key == key).kind;
            else return false;
            if (want == QuestGraph.ValueKind.Players) return vt.output == QuestGraph.ValueKind.Players;
            if (vt.output == QuestGraph.ValueKind.Players) return false;
            if (want == QuestGraph.ValueKind.Number && vt.output == QuestGraph.ValueKind.Orbit) return true;   // an orbit is a station's number
            // A value node fed (through its inputs) by the owner would loop.
            if (IsValue(owner) && Feeds(owner, value, 0)) return false;
            return true;
        }

        /// <summary>'a' is wired (directly or further back) into 'b'.</summary>
        bool Feeds(JObject a, JObject b, int depth)
        {
            if (depth > 40) return true;
            foreach (var p in b.Properties())
            {
                string id = QuestGraph.WiredValue(p.Value);
                if (id == null) continue;
                var v = QuestGraph.Value(quest, id);
                if (v == a || v != null && Feeds(a, v, depth + 1)) return true;
            }
            return false;
        }

        void EndDrag()
        {
            if (pointerId >= 0 && this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
            pointerId = -1;
            drag = Drag.None;
            dragNode = null;
            dragPort = null;
            dragGrab = null;
            wireLayer.MarkDirtyRepaint();
        }

        void OnWheel(WheelEvent e)
        {
            float old = zoom;
            zoom = Mathf.Clamp(zoom * Mathf.Pow(0.9f, Mathf.Sign(e.delta.y)), 0.25f, 1.6f);
            Vector2 local = this.WorldToLocal(e.mousePosition);
            pan = local - (local - pan) * (zoom / old);
            ApplyTransform();
            e.StopPropagation();
        }

        void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Delete && Selection != null) { DeleteSelected(); e.StopPropagation(); }
            else if (e.keyCode == KeyCode.Equals || e.keyCode == KeyCode.KeypadPlus) ZoomBy(1.15f);
            else if (e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.KeypadMinus) ZoomBy(1f / 1.15f);
        }

        public void ZoomBy(float f)
        {
            float old = zoom;
            zoom = Mathf.Clamp(zoom * f, 0.25f, 1.6f);
            Vector2 c = contentRect.center;
            pan = c - (c - pan) * (zoom / old);
            ApplyTransform();
        }

        // ---- editing ---------------------------------------------------------------------------------------------

        /// <summary>A value node (in the quest's "values"), not a step.</summary>
        bool IsValue(JObject o) => o != null && o.Parent != null && o.Parent == quest?["values"];

        public void Select(object node, bool tell = true)
        {
            Selection = node;
            foreach (var kv in nodes) kv.Value.EnableInClassList("mbg-node--selected", Equals(kv.Key, node));
            if (tell) selected?.Invoke(node);
        }

        void Changed(bool rebuild)
        {
            if (rebuild) Rebuild();
            edited?.Invoke();
        }

        /// <summary>A new step on its own where the canvas was double-clicked (or at the view's centre).</summary>
        public void NewStepAt(Vector2? canvasPos = null)
        {
            var at = canvasPos ?? ((contentRect.center - pan) / zoom);
            pickStep?.Invoke(st =>
            {
                QuestGraph.SetPos(st, at.x, at.y);
                QuestGraph.AddLoose(quest, st);
                Selection = st;
                Changed(true);
                selected?.Invoke(st);
            }, null);
        }

        public void NewValue()
        {
            var at = (contentRect.center - pan) / zoom;
            pickValue?.Invoke(v =>
            {
                QuestGraph.SetPos(v, at.x, at.y);
                Selection = v;
                Changed(true);
                selected?.Invoke(v);
            });
        }

        public void DeleteSelected()
        {
            if (!(Selection is JObject o)) return;
            if (IsValue(o)) QuestGraph.DeleteValue(quest, o);
            else QuestGraph.Delete(quest, o);
            Selection = null;
            Changed(true);
            selected?.Invoke(null);
        }
    }
}
