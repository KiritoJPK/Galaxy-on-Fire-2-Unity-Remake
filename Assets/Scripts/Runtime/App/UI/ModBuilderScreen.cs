// ModBuilderScreen.cs
// Remake-only: what the Mod Builder's two screens share (Modding.ModBuilder): ModBuilderView over the Station scene (its
// hangar, Space Lounge and star map) and ModBuilderSpaceView over the Space scene (a station's orbit). Built in code with
// Resources/GoF2ModBuilder/ModBuilder.uss:
//   top bar      the project, the kinds of content (each edited where it lives in the game: ModBuilder.PlaceOf; a kind of
//                another scene loads it, ModBuilder.GoTo), Undo / Redo, the screen's own buttons, Menu (Options, Save, Exit)
//   panels       a left and a right panel per kind (the screen fills the kinds it edits)
//   modal        pickers (tiles with icons, in sections), confirmations, a name prompt, the reference picker (ModRefs: the
//                game's content, the project's own and other mods', whose pick makes that mod a dependency)
//   status       one line at the foot: what happened, the project's problems as the game read it
// Edits mark the project dirty and are saved 0.4 s after the last one (a save point is an undo step, ModBuilder.History);
// RequestReload lets the game read the project again (ModBuilder.Reload) and waits for the mods' models (ReloadDone).
// Plain C#.

using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]   // Open: cleared at SubsystemRegistration (ModBuilderScreenAi)
    public abstract partial class ModBuilderScreen
    {
        protected static string T(string key, string english) => Localization.Extra(key, english);

        protected readonly Action<bool> sound;
        protected readonly VisualElement root, modal;
        protected readonly Label status, projectLabel;
        protected VisualElement uiTop, uiLeft, uiRight;
        protected readonly VisualElement[] leftGroups = new VisualElement[ModBuilder.KindCount], rightGroups = new VisualElement[ModBuilder.KindCount];
        readonly Button[] kindTabs = new Button[ModBuilder.KindCount];
        protected Button undoButton, redoButton;
        protected ModBuilder.Kind kind = (ModBuilder.Kind)(-1);
        protected ModProject Project => ModBuilder.Project;

        // saving and reloading
        protected bool reloadPending, reloading, saveDirty;
        protected float reloadAtMs, saveAtMs, nowMs;
        /// <summary>Things to do once the game has read the project again (an item's number to use, Update).</summary>
        protected readonly List<Action> afterReload = new List<Action>();

        string statusText;
        float statusMs;
        bool statusError;

        /// <summary>The kinds in the top bar's order: the hangar's, the lounge's, the map's and space's, the rooms, the campaign.</summary>
        static readonly (string key, string english, ModBuilder.Kind kind)[] KindTabs =
        {
            ("mbShips", "Ships", ModBuilder.Kind.Ships), ("mbItems", "Items", ModBuilder.Kind.Items), ("mbFx", "Weapon fx", ModBuilder.Kind.WeaponFx),
            ("mbSkins", "Skins", ModBuilder.Kind.Skins), ("mbKits", "Ship kits", ModBuilder.Kind.Kits), ("mbBlueprints", "Blueprints", ModBuilder.Kind.Blueprints),
            ("mbCharacters", "Characters", ModBuilder.Kind.Characters), ("mbQuests", "Quests", ModBuilder.Kind.Quests), ("mbAudio", "Audio", ModBuilder.Kind.Audio),
            ("mbSystems", "Systems", ModBuilder.Kind.Systems), ("mbStations", "Stations", ModBuilder.Kind.Stations),
            ("mbInteriors", "Hangars & bars", ModBuilder.Kind.Interiors), ("mbCampaigns", "Campaign", ModBuilder.Kind.Campaigns),
        };

        /// <summary>Where each kind is edited, for the tabs' tooltips.</summary>
        static string PlaceName(ModBuilder.Kind k) => ModBuilder.PlaceOf(k) switch
        {
            ModBuilder.Place.Lounge => T("mbPlaceLounge", "Edited in the Space Lounge"),
            ModBuilder.Place.Map => T("mbPlaceMap", "Edited on the star map"),
            ModBuilder.Place.Space => T("mbPlaceSpace", "Edited in space"),
            _ => k == ModBuilder.Kind.Interiors ? T("mbPlaceRoom", "Edited in the room itself") : T("mbPlaceHangar", "Edited in the hangar"),
        };

        protected ModBuilderScreen(VisualElement parent, Action<bool> sound)
        {
            this.sound = sound;
            var sheet = Resources.Load<StyleSheet>("GoF2ModBuilder/ModBuilder");
            root = new VisualElement { name = "modBuilder", pickingMode = PickingMode.Ignore };
            root.AddToClassList("mbv-root");
            if (sheet != null) root.styleSheets.Add(sheet);
            parent.Add(root);
            BuildViewport(root);

            // ---- top bar ----
            var top = uiTop = Add(root, new VisualElement(), "mbv-top");
            var titleBox = Add(top, new VisualElement(), "mbv-title-box");
            Add(titleBox, new Label(T("mbTitle", "Mod Builder").ToUpperInvariant()), "mbv-title", "gof-semibold");
            projectLabel = Add(titleBox, new Label(), "mbv-project");
            // The kinds of content: a strip that scrolls sideways when the screen is narrow (the buttons never give way).
            var kindsScroll = Add(top, new ScrollView(ScrollViewMode.Horizontal), "mbv-kinds-scroll");
            kindsScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            kindsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(kindsScroll);
            var kinds = Add(kindsScroll, new VisualElement(), "mbv-kinds");
            foreach (var (k, e, kd) in KindTabs) KindTab(kinds, T(k, e), kd);
            undoButton = TopButton(top, T("mbUndo", "Undo"), () => UndoRedo(false), "mbv-undo-button");
            redoButton = TopButton(top, T("mbRedo", "Redo"), () => UndoRedo(true), "mbv-redo-button");
            // The AI tools (the MCP server's tab) aren't content: their own button, again = back to the kind before.
            aiButton = TopButton(top, T("mbAiTools", "AI tools"), () =>
            {
                sound(false);
                SelectKind(kind == ModBuilder.Kind.AiTools ? kindBeforeAi : ModBuilder.Kind.AiTools);
            }, "mbv-ai-button");
            AddTopButtons(top);
            TopButton(top, Localization.Get(172), ShowMenu, "mbv-menu-button");   // "Menu", like the station's

            // ---- the panels, one group per kind ----
            uiLeft = Add(root, new VisualElement(), "mbv-left", "mbv-panel");
            uiRight = Add(root, new VisualElement(), "mbv-right", "mbv-panel");
            for (int k = 0; k < ModBuilder.KindCount; k++)
            {
                leftGroups[k] = Add(uiLeft, new VisualElement(), "mbv-group");
                rightGroups[k] = Add(uiRight, new VisualElement(), "mbv-group");
                leftGroups[k].style.display = rightGroups[k].style.display = DisplayStyle.None;
            }
            BuildAiPanels(leftGroups[(int)ModBuilder.Kind.AiTools], rightGroups[(int)ModBuilder.Kind.AiTools]);

            status = new Label { pickingMode = PickingMode.Ignore };
            status.AddToClassList("mbv-status");
            modal = new VisualElement();
            modal.AddToClassList("mbv-modal");
            modal.RegisterCallback<PointerDownEvent>(e => { if (e.target == modal) CloseModal(); });
            projectLabel.text = Project != null ? Project.Name : "";
        }

        /// <summary>The status line and the modal go over everything the screen added (call at the end of its constructor).</summary>
        protected void FinishLayout()
        {
            root.Add(status);
            root.Add(modal);
        }

        /// <summary>The screen's 3D view under the panels (the pointer over the scene), added first.</summary>
        protected virtual void BuildViewport(VisualElement root) { }
        /// <summary>The screen's own top bar buttons (between Redo and Menu).</summary>
        protected virtual void AddTopButtons(VisualElement top) { }

        // ---- building blocks ---------------------------------------------------------------------------------

        protected static TE Add<TE>(VisualElement parent, TE e, params string[] classes) where TE : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            parent.Add(e);
            return e;
        }

        protected Button Act(VisualElement parent, string text, Action onClick, params string[] classes)
        {
            var b = new Button(() => { sound(false); onClick(); }) { text = text.ToUpperInvariant() };
            b.AddToClassList("mbv-button");
            foreach (var c in classes) b.AddToClassList(c);
            b.focusable = false;   // keys are the builder's shortcuts, not button presses
            parent.Add(b);
            return b;
        }

        protected Button TopButton(VisualElement parent, string text, Action onClick, string extra = null)
        {
            var b = Act(parent, text, onClick, "mbv-top-button");
            if (extra != null) b.AddToClassList(extra);
            return b;
        }

        void KindTab(VisualElement parent, string text, ModBuilder.Kind k)
        {
            // The name and the badge as two labels: a Button with text of its own and children lays them over each other.
            var b = new Button { focusable = false };
            b.AddToClassList("mbv-kind");
            Add(b, new Label(text.ToUpperInvariant()) { pickingMode = PickingMode.Ignore }, "mbv-kind-name");
            b.clicked += () => { if (k != kind) { sound(false); SelectKind(k); } };
            b.tooltip = PlaceName(k);
            kindTabs[(int)k] = b;
            parent.Add(b);
        }

        protected Label Heading(VisualElement parent, string text) => Add(parent, new Label(text.ToUpperInvariant()), "mbv-heading", "gof-semibold");

        protected Label Note(VisualElement parent, string text) => Add(parent, new Label(text), "mbv-note");

        protected Label Caption(VisualElement parent, string text) => Add(parent, new Label(text.ToUpperInvariant()), "mbv-label");

        protected VisualElement Row(VisualElement parent, params string[] extra)
        {
            var r = Add(parent, new VisualElement(), "mbv-row");
            foreach (var c in extra) r.AddToClassList(c);
            return r;
        }

        protected TextField Text(VisualElement parent, string label, string value, int max, Action<string> set, bool multi = false)
        {
            var f = Add(parent, new TextField(label) { value = value ?? "", maxLength = max, multiline = multi, isDelayed = true }, "mbv-field");
            if (multi) f.AddToClassList("mbv-field--multi");
            f.RegisterValueChangedCallback(e => set(e.newValue));
            return f;
        }

        protected IntegerField Int(VisualElement parent, string label, int value, int min, int max, Action<int> set)
        {
            var f = Add(parent, new IntegerField(label) { value = value, isDelayed = true }, "mbv-field");
            f.RegisterValueChangedCallback(e =>
            {
                int v = Mathf.Clamp(e.newValue, min, max);
                if (v != e.newValue) f.SetValueWithoutNotify(v);
                set(v);
            });
            return f;
        }

        protected FloatField Float(VisualElement parent, string label, float value, float min, float max, Action<float> set)
        {
            var f = Add(parent, new FloatField(label) { value = value, isDelayed = true }, "mbv-field");
            f.RegisterValueChangedCallback(e =>
            {
                float v = Mathf.Clamp(e.newValue, min, max);
                if (!Mathf.Approximately(v, e.newValue)) f.SetValueWithoutNotify(v);
                set(v);
            });
            return f;
        }

        /// <summary>An on / off button ("Mirrored: on").</summary>
        protected void FlagButton(VisualElement parent, string label, bool on, Action<bool> set, Action after = null)
        {
            var b = Act(parent, $"{label}: {(on ? T("mbOn", "on") : T("mbOff", "off"))}", () => { set(!on); MarkDirty(); after?.Invoke(); });
            b.EnableInClassList("mbv-toggle--on", on);
        }

        /// <summary>A row of segments, the current one lit; 'pick' gets the picked index.</summary>
        protected void Segments(VisualElement parent, string caption, string[] labels, int current, Action<int> pick)
        {
            if (caption != null) Caption(parent, caption);
            var row = Add(parent, new VisualElement(), "mbv-seg-row");
            for (int i = 0; i < labels.Length; i++)
            {
                int n = i;
                var b = Act(row, labels[i], () => pick(n), "mbv-seg");
                b.EnableInClassList("mbv-seg--on", i == current);
            }
        }

        /// <summary>A list row with an icon, a name and a line under it (the left panels' lists).</summary>
        protected Button ListRow(VisualElement parent, string name, string sub, bool on, Action pick, Texture2D icon = null, string iconClass = "mbv-ship-icon")
        {
            var row = new Button(() => { sound(false); pick(); }) { focusable = false };
            row.AddToClassList("mbv-ship-row");
            row.EnableInClassList("mbv-ship-row--on", on);
            if (iconClass != null)
            {
                var img = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, iconClass);
                if (icon != null) img.style.backgroundImage = new StyleBackground(icon);
            }
            var text = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-ship-text");
            Add(text, new Label(name) { pickingMode = PickingMode.Ignore }, "mbv-ship-name");
            if (!string.IsNullOrEmpty(sub)) Add(text, new Label(sub) { pickingMode = PickingMode.Ignore }, "mbv-ship-sub");
            parent.Add(row);
            return row;
        }

        /// <summary>A scrolling list under a heading in a left panel (the kinds' lists).</summary>
        protected ScrollView ListPanel(VisualElement left, string heading)
        {
            Add(left, new Label(heading.ToUpperInvariant()), "mbv-heading", "gof-semibold");
            var scroll = Add(left, new ScrollView(ScrollViewMode.Vertical), "mbv-ship-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(scroll);
            return scroll;
        }

        /// <summary>A scrolling page in a right panel.</summary>
        protected VisualElement PagePanel(VisualElement right)
        {
            var page = Add(right, new ScrollView(ScrollViewMode.Vertical), "mbv-right-scroll");
            page.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            page.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            return Add(page, new VisualElement(), "mbv-page");
        }

        protected static string Pretty(string fileName)
        {
            var s = (fileName ?? "").Replace('_', ' ').Replace('-', ' ').Trim();
            return s.Length == 0 ? "New" : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // ---- kinds --------------------------------------------------------------------------------------------

        /// <summary>A kind is picked in the top bar: in another scene the builder goes there; else the screen shows it.</summary>
        protected void SelectKind(ModBuilder.Kind k)
        {
            if (k != ModBuilder.Kind.AiTools && !EditsHere(k))   // the AI tools' tab is shown wherever the builder is
            {
                SaveNow();
                if (ModBuilder.GoTo(k)) return;
            }
            var was = kind;
            if (k == ModBuilder.Kind.AiTools && was != ModBuilder.Kind.AiTools && (int)was >= 0) kindBeforeAi = was;
            kind = k;
            aiButton?.EnableInClassList("mbv-top-button--on", k == ModBuilder.Kind.AiTools);
            ModBuilder.OpenKind = k;
            for (int i = 0; i < ModBuilder.KindCount; i++)
            {
                leftGroups[i].style.display = i == (int)k ? DisplayStyle.Flex : DisplayStyle.None;
                rightGroups[i].style.display = i == (int)k ? DisplayStyle.Flex : DisplayStyle.None;
                kindTabs[i]?.EnableInClassList("mbv-kind--on", i == (int)k);
            }
            ShowKind(k, was);
            if (k == ModBuilder.Kind.AiTools) ShowAiTools();
        }

        /// <summary>The kinds this screen edits (the others are another scene's).</summary>
        protected abstract bool EditsHere(ModBuilder.Kind k);
        /// <summary>The screen switches to a kind it edits ('was' the one before, -1 at the start).</summary>
        protected abstract void ShowKind(ModBuilder.Kind k, ModBuilder.Kind was);

        // ---- editing ------------------------------------------------------------------------------------------------

        protected void MarkDirty()
        {
            saveDirty = true;
            saveAtMs = nowMs + 400f;
        }

        protected void SaveNow()
        {
            saveDirty = false;
            Project?.Save();
            ModBuilder.History?.Commit();   // a save point is an undo step (when anything changed)
        }

        /// <summary>The game reads the project again soon ('soon': a structural change, half a second after the last one).</summary>
        protected void RequestReload(bool soon)
        {
            MarkDirty();
            reloadPending = true;
            reloadAtMs = nowMs + (soon ? 600f : 0f);
        }

        /// <summary>A reload may start now (not while a drag edits something).</summary>
        protected virtual bool CanReload => true;
        /// <summary>The game has read the project again and built what it needs (the mods' models).</summary>
        protected virtual bool ReloadDone => ModShips.Ready && !ModLoading.Busy;
        protected virtual void OnReloadStarted() { }
        protected virtual void OnReloaded() { }

        // ---- undo / redo (ModBuilder.History) --------------------------------------------------------------------

        /// <summary>The top bar's Undo / Redo follow what the history holds (an edit not saved yet can be undone too).</summary>
        void UpdateUndoButtons()
        {
            var h = ModBuilder.History;
            undoButton?.SetEnabled(h != null && (h.CanUndo || saveDirty));
            redoButton?.SetEnabled(h != null && h.CanRedo && !saveDirty);
        }

        /// <summary>The screen remembers what is picked before a step is undone; the returned action picks it again.</summary>
        protected virtual Action BeforeUndo() => null;
        /// <summary>A drag is under way: no undo meanwhile.</summary>
        protected virtual bool Dragging => false;

        /// <summary>Back (or forward) one step: the project's files as they were, read again by the game; what was worked on
        /// stays picked when it still exists.</summary>
        protected void UndoRedo(bool redo)
        {
            var h = ModBuilder.History;
            if (h == null || Project == null || Dragging) return;
            SaveNow();
            var restore = BeforeUndo();
            if (!(redo ? h.Redo() : h.Undo()))
            {
                Status(redo ? T("mbNothingToRedo", "Nothing to redo.") : T("mbNothingToUndo", "Nothing to undo."), false);
                return;
            }
            restore?.Invoke();
            RequestReload(false);
            Status(redo ? T("mbRedone", "Redone.") : T("mbUndone", "Undone."), false);
        }

        // ---- modal ----------------------------------------------------------------------------------------------

        protected bool ModalOpen => modal.ClassListContains("mbv-modal--shown");

        /// <summary>A picker of tiles; a choice without an action is a section's heading.</summary>
        protected void ShowPicker(string title, List<(string name, Texture2D icon, Action pick)> choices, bool search = false)
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(box, title);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mbv-picker-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(scroll);
            var grid = Add(scroll, new VisualElement(), "mbv-picker-grid");
            void Fill(string filter)
            {
                grid.Clear();
                var words = (filter ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string heading = null;
                foreach (var (name, icon, pick) in choices)
                {
                    if (pick == null) { heading = name; continue; }   // a section's heading: shown before its first match
                    if (words.Length > 0 && !words.All(w => name.ToLowerInvariant().Contains(w))) continue;
                    if (heading != null) { Add(grid, new Label(heading.ToUpperInvariant()), "mbv-picker-heading", "gof-semibold"); heading = null; }
                    var tile = new Button(() => { sound(false); CloseModal(); pick(); }) { focusable = false };
                    tile.AddToClassList("mbv-tile");
                    var img = Add(tile, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-tile-icon");
                    if (icon != null) img.style.backgroundImage = new StyleBackground(icon);
                    Add(tile, new Label(name) { pickingMode = PickingMode.Ignore }, "mbv-tile-name");
                    grid.Add(tile);
                }
            }
            if (search || choices.Count > 24)
            {
                var field = Add(box, new TextField { maxLength = 60 }, "mbv-field", "mbv-search");
                field.textEdition.placeholder = T("mbSearch", "Search");
                field.RegisterValueChangedCallback(e => Fill(e.newValue));
                field.schedule.Execute(() => field.Focus());
            }
            box.Add(scroll);
            Fill(null);
            Act(box, T("mbCancel", "Cancel"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        protected void Confirm(string title, string text, Action yes)
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--small", "mbv-panel");
            Heading(box, title);
            Add(box, new Label(text), "mbv-confirm-text");
            var row = Add(box, new VisualElement(), "mbv-row");
            Act(row, Localization.Get(134), () => { CloseModal(); yes(); }, "mbv-danger");
            Act(row, Localization.Get(135), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        /// <summary>Asks for a line of text (a name); OK hands it over when it isn't empty.</summary>
        protected void AskText(string title, string label, string value, Action<string> ok, int max = 40)
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--small", "mbv-panel");
            Heading(box, title);
            var field = Add(box, new TextField(label) { value = value ?? "", maxLength = max }, "mbv-field");
            var row = Add(box, new VisualElement(), "mbv-row");
            void Done()
            {
                string v = field.value?.Trim();
                if (string.IsNullOrEmpty(v)) { field.Focus(); return; }
                CloseModal();
                ok(v);
            }
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.StopPropagation(); Done(); } }, TrickleDown.TrickleDown);
            Act(row, T("mbOk", "OK"), Done);
            Act(row, T("mbCancel", "Cancel"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
            field.schedule.Execute(() => field.Focus());
        }

        /// <summary>A button that opens Edit as JSON for an entry (every field the modding guide lists, the builder's pages
        /// or not).</summary>
        protected void JsonButton(VisualElement page, JObject target, Action after, string note = null)
        {
            var row = Row(page);
            Act(row, T("mbEditJson", "Edit as JSON…"), () => EditJson(target, after, note), "mbv-step");
        }

        /// <summary>The entry's JSON in a text box; Apply puts it back in place (the same object, so the page's references
        /// stay), after checking it parses. Every field of the modding guide can be set this way.</summary>
        protected void EditJson(JObject target, Action after, string note = null)
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(box, T("mbEditJson", "Edit as JSON…"));
            Note(box, note ?? T("mbEditJsonNote", "Every field the modding guide (Modding/README.md) lists for this kind of entry can be written here, also the ones the builder has no field for."));
            var error = Add(box, new Label(), "mbv-note", "mbv-warn");
            error.style.display = DisplayStyle.None;
            var scroll = Add(box, new ScrollView(ScrollViewMode.Vertical), "mbv-picker-scroll");
            var field = Add(scroll, new TextField { value = target.ToString(Newtonsoft.Json.Formatting.Indented), multiline = true, maxLength = 200000 }, "mbv-field", "mbv-script");
            var row = Add(box, new VisualElement(), "mbv-row");
            Act(row, T("mbApply", "Apply"), () =>
            {
                JObject parsed;
                try { parsed = JObject.Parse(field.value); }
                catch (Exception e)
                {
                    error.text = "✖ " + e.Message;
                    error.style.display = DisplayStyle.Flex;
                    return;
                }
                target.RemoveAll();
                foreach (var p in parsed.Properties().ToList()) target.Add(p.Name, p.Value.DeepClone());
                CloseModal();
                MarkDirty();
                after?.Invoke();
            });
            Act(row, T("mbCancel", "Cancel"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        protected void CloseModal()
        {
            DropPendingAsk();
            modal.RemoveFromClassList("mbv-modal--shown");
            modal.RemoveFromClassList("mbv-modal--options");
            modal.Clear();
            optionsView = null;
        }

        // ---- references (ModRefs) ---------------------------------------------------------------------------------

        /// <summary>A picker of the game's, the project's own and the other mods' content of a kind, in sections; another mod's
        /// pick makes that mod a dependency (the game reads the project again so it is on). 'none' adds a first tile that
        /// clears the field.</summary>
        protected void PickRef(RefKind refKind, string title, Action<RefEntry> pick, Func<RefEntry, bool> filter = null, string none = null)
        {
            var choices = new List<(string, Texture2D, Action)>();
            if (none != null) choices.Add((none, null, () => pick(null)));
            string group = null;
            foreach (var e in ModRefs.List(refKind, Project))
            {
                if (filter != null && !filter(e)) continue;
                if (e.group != group)
                {
                    group = e.group;
                    string head = e.group == ModRefs.GameGroup ? T("mbFromGame", "The game") : e.own ? string.Format(T("mbFromThisMod", "This mod: {0}"), e.group)
                                : string.Format(T("mbFromMod", "Mod: {0} (becomes a dependency)"), e.group);
                    choices.Add((head, null, null));
                }
                var entry = e;
                choices.Add((string.IsNullOrEmpty(e.sub) ? e.name : $"{e.name}\n{e.sub}", RefIcon(refKind, e), () =>
                {
                    if (ModRefs.Use(Project, entry))
                    {
                        Status(string.Format(T("mbDependencyAdded", "\"{0}\" is now a dependency of this mod: it is on whenever this mod is."), entry.group), false);
                        RequestReload(false);
                    }
                    pick(entry);
                }));
            }
            ShowPicker(title, choices, true);
        }

        /// <summary>A reference entry's icon (an item's or a ship's shop icon; a race emblem for systems).</summary>
        protected static Texture2D RefIcon(RefKind k, RefEntry e)
        {
            switch (k)
            {
                case RefKind.Item: return e.icon >= 0 ? ItemInfo.ItemIcon(e.icon) : null;
                case RefKind.Ship:
                    if (e.icon >= 0) return ItemInfo.ShipIcon(e.icon);
                    int i = ModContent.ShipIndexOf(e.value);
                    return i >= 0 ? ItemInfo.ShipIcon(i) : null;
                default: return null;
            }
        }

        /// <summary>A stored reference as a label ("Falcon (Ship Pack)"), or the 'none' text.</summary>
        protected string RefLabel(RefKind k, string value, string none) => string.IsNullOrEmpty(value) ? none : ModRefs.Label(k, value, Project);

        /// <summary>A field that names other content: its current pick and a Choose… button (and ✕ to clear it when 'clear').</summary>
        protected void RefField(VisualElement parent, string caption, RefKind k, string value, Action<string> set, string none = null, bool clear = true,
                                Func<RefEntry, bool> filter = null)
        {
            Caption(parent, caption);
            var row = Row(parent);
            Add(row, new Label(RefLabel(k, value, none ?? T("mbNone", "none"))), "mbv-inline-label");
            Act(row, T("mbChoose", "Choose…"), () => PickRef(k, caption, e => set(e?.value), filter));
            if (clear && !string.IsNullOrEmpty(value)) Act(row, "✕", () => set(null), "mbv-step");
        }

        // ---- files --------------------------------------------------------------------------------------------

        bool picking;
        protected string pickedName;

        /// <summary>The system's file picker (ModImport.Pick, polled); 'handle' gets the copied file, which goes afterwards.</summary>
        protected void PickRaw(string title, string filterName, string[] extensions, Action<ModImport.Picked> handle)
        {
            if (picking || Project == null) return;
            if (!ModImport.Available) { Status(T("mbNoPicker", "This device has no file picker: put the file into the project's folder instead."), true); return; }
            picking = true;
            var poll = ModImport.Pick(title, filterName, extensions);
            ModImport.Picked picked = null;
            root.schedule.Execute(() =>
            {
                picked = poll();
                if (picked == null) return;
                picking = false;
                if (picked.cancelled) return;
                if (picked.error != null) { Status(string.Format(T("mbPickFailed", "The file couldn't be read: {0}"), picked.error), true); return; }
                try { pickedName = picked.name; handle(picked); }
                catch (Exception e) { Status(string.Format(T("mbPickFailed", "The file couldn't be read: {0}"), e.Message), true); }
                finally { ModImport.Discard(picked.path); }
            }).Every(200).Until(() => picked != null);
        }

        /// <summary>The picker for one kind of file: its first bytes must be 'magic', else 'wrong' is shown.</summary>
        protected void PickFile(string title, string filterName, string extension, byte[] magic, string wrong, Action<string> use) =>
            PickRaw(title, filterName, new[] { extension }, picked =>
            {
                var head = new byte[magic.Length];
                using (var f = System.IO.File.OpenRead(picked.path)) f.Read(head, 0, head.Length);
                if (!head.SequenceEqual(magic)) { Status(wrong, true); return; }
                use(picked.path);
            });

        /// <summary>A .glb model (glTF binary).</summary>
        protected void PickGlb(string title, Action<string> use) => PickFile(title, "glTF binary model", "glb", new byte[] { (byte)'g', (byte)'l', (byte)'T', (byte)'F' },
            T("mbNotGlb", "That isn't a .glb model (glTF binary). In Blender: File > Export > glTF 2.0, format glTF Binary."), use);

        /// <summary>The picker for an image (ImageImport: PNG, JPG, TGA, BMP, DDS, the game's .aei): 'use' gets it as a PNG
        /// (a temporary file, copied into the project by 'use').</summary>
        protected void PickImage(string title, Action<string> use) =>
            PickRaw(title, T("mbImageFilter", "Images"), ImageImport.Extensions, picked =>
            {
                string png = ImageImport.ToPng(picked.path, picked.name, out string error);
                if (png == null) { Status(error, true); return; }
                try { use(png); }
                finally { try { System.IO.File.Delete(png); } catch { } }
            });

        /// <summary>A sound or music file (.ogg, .wav, .mp3).</summary>
        protected void PickAudio(string title, Action<string> use) =>
            PickRaw(title, T("mbAudioFilter", "Sound files"), new[] { "ogg", "wav", "mp3" }, picked => use(picked.path));

        // ---- the Menu (like the station's: Options, leaving) ----------------------------------------------------

        protected OptionsView optionsView;

        /// <summary>The top bar's Menu (or Esc with nothing else open): Resume, Options (the game's, OptionsView), Save and
        /// Exit the Mod Builder (back to the projects page).</summary>
        protected void ShowMenu()
        {
            CloseModal();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--menu", "mbv-panel");
            Heading(box, Localization.Get(172));
            Act(box, T("mbResume", "Resume"), CloseModal, "mbv-menu-item");
            Act(box, Localization.Get(31), ShowOptions, "mbv-menu-item");   // Options
            Act(box, T("mbDependencies", "Dependencies"), ShowDependencies, "mbv-menu-item");
            Act(box, T("mbModDetails", "Mod details"), ShowModDetails, "mbv-menu-item");
            Act(box, T("mbTranslations", "Translations"), () => ShowTranslations(ModProject.TextLanguages[0]), "mbv-menu-item");
            Act(box, T("mbSave", "Save"), () => { SaveNow(); CloseModal(); Status(T("mbSaved", "Saved."), false); }, "mbv-menu-item");
            Act(box, T("mbExitBuilder", "Exit the Mod Builder"), () => { CloseModal(); SaveNow(); ModBuilder.End(); }, "mbv-menu-item", "mbv-danger");
            modal.AddToClassList("mbv-modal--shown");
        }

        void ShowOptions()
        {
            modal.Clear();
            optionsView = new OptionsView(ShowMenu, true);
            optionsView.Root.EnableInClassList("can-hover", true);
            optionsView.Changed += c => { if (c.def.kind == OptionKind.Choice || c.def.kind == OptionKind.Toggle) sound(false); };
            modal.Add(optionsView.Root);
            modal.AddToClassList("mbv-modal--options");
            modal.AddToClassList("mbv-modal--shown");
            optionsView.Select(optionsView.ActiveTab);
        }

        /// <summary>mod.json's other fields: the website, the credits and the Mods screen's picture.</summary>
        void ShowModDetails()
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(box, T("mbModDetails", "Mod details"));
            var scroll = Add(box, new ScrollView(ScrollViewMode.Vertical), "mbv-picker-scroll");
            Text(scroll, T("mbName", "Name"), Project.Name, 60, v => { if (v.Trim().Length > 0) { Project.Name = v.Trim(); MarkDirty(); } });
            Text(scroll, T("mbAuthor", "Author"), Project.Author, 60, v => { Project.Author = v.Trim(); MarkDirty(); });
            Text(scroll, T("mbVersion", "Version"), Project.Version, 20, v => { if (v.Trim().Length > 0) { Project.Version = v.Trim(); MarkDirty(); } });
            Text(scroll, T("mbDescription", "Description"), Project.Description, 600, v => { Project.Description = v; MarkDirty(); }, true);
            Text(scroll, T("mbWebsite", "Website"), Project.Website, 200, v => { Project.Website = v; MarkDirty(); });
            Text(scroll, T("mbCredits", "Credits (who made what)"), Project.Credits, 2000, v => { Project.Credits = v; MarkDirty(); }, true);
            var row = Row(scroll);
            Add(row, new Label($"{T("mbPreview", "Picture")}: {Project.PreviewImage ?? T("mbNone", "none")}"), "mbv-inline-label");
            if (ModImport.Available)
                Act(row, T("mbChoose", "Choose…"), () => PickImage(T("mbPreview", "Picture"), png => { Project.SetPreviewImage(png); MarkDirty(); ShowModDetails(); }), "mbv-step");
            Note(scroll, T("mbPreviewNote", "The picture beside the mod in the Mods screen (preview.png; about 16:9)."));
            Act(box, T("mbClose", "Close"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        /// <summary>The project's texts in another language (text/&lt;lang&gt;.json): every name and description beside its
        /// English, saved as typed.</summary>
        void ShowTranslations(string lang)
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(box, T("mbTranslations", "Translations"));
            int at = Array.IndexOf(ModProject.TextLanguages, lang);
            Segments(box, null, ModProject.TextLanguages.Select(l => l.ToUpperInvariant()).ToArray(), Math.Max(0, at), n => ShowTranslations(ModProject.TextLanguages[n]));
            var scroll = Add(box, new ScrollView(ScrollViewMode.Vertical), "mbv-picker-scroll");
            var texts = Project.Texts(lang);
            var all = Project.TranslatableTexts();
            if (all.Count == 0) Note(scroll, T("mbNothingToTranslate", "Nothing to translate yet: the names and descriptions of the project's items, ships, systems and stations show here."));
            foreach (var (key, english) in all)
            {
                Caption(scroll, key);
                if (!string.IsNullOrEmpty(english)) Note(scroll, english);
                texts.TryGetValue(key, out string cur);
                string k = key;
                Text(scroll, lang.ToUpperInvariant(), cur, 600, v => { Project.SetText(lang, k, v); MarkDirty(); }, key.EndsWith(".description"));
            }
            Note(scroll, T("mbTranslationsNote", "Empty = the English. Quests and characters carry their languages in their own texts; the game's language list: Options > Language."));
            Act(box, T("mbClose", "Close"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        /// <summary>The mods this one depends on (mod.json "dependencies"): each with its state, Remove; and the other mods the
        /// project's files name that aren't one yet (Add).</summary>
        protected void ShowDependencies()
        {
            modal.Clear();
            var box = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(box, T("mbDependencies", "Dependencies"));
            Note(box, T("mbDependenciesNote", "Mods whose content this one uses. They are turned on with it (and here in the builder). Picking another mod's ship, item, station or character anywhere adds it here."));
            var deps = Project.Dependencies;
            var named = Project.ModsNamed();
            foreach (var d in deps)
            {
                var m = ModManager.Find(d);
                var row = Row(box);
                string state = m == null ? T("mbDepMissing", "not installed") : m.Broken ? T("mbDepBroken", "has errors") : "v" + m.Version;
                Add(row, new Label($"{m?.Name ?? d}  ·  {state}" + (named.Contains(d) ? "" : "  ·  " + T("mbDepUnused", "not used by this mod's files"))), "mbv-inline-label");
                string id = d;
                Act(row, T("mbRemove", "Remove"), () =>
                {
                    Project.RemoveDependency(id);
                    RequestReload(false);
                    ShowDependencies();
                }, "mbv-danger");
            }
            if (deps.Count == 0) Note(box, T("mbNoDependencies", "None: this mod uses only the game's content and its own."));
            foreach (var d in named.Where(n => !deps.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                var m = ModManager.Find(d);
                var row = Row(box);
                Add(row, new Label(string.Format(T("mbDepNamed", "The files name \"{0}\", which isn't a dependency"), m?.Name ?? d)), "mbv-inline-label");
                string id = d;
                Act(row, T("mbAdd", "Add"), () =>
                {
                    Project.AddDependency(id, ModManager.Find(id)?.Version);
                    RequestReload(false);
                    ShowDependencies();
                });
            }
            Act(box, T("mbClose", "Close"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }

        // ---- status -------------------------------------------------------------------------------------------

        protected void Status(string text, bool error)
        {
            statusText = text;
            statusError = error;
            statusMs = error ? 8000f : 4000f;
        }

        /// <summary>The project's errors and warnings as the game read it (a broken entry keeps it out of the game).</summary>
        protected void ShowProblems()
        {
            var mod = ModManager.BuilderProject;
            if (mod == null) return;
            if (mod.Errors.Count > 0) Status("✖ " + string.Join("\n✖ ", mod.Errors.Take(3)), true);
            else if (mod.Warnings.Count > 0) Status("! " + string.Join("\n! ", mod.Warnings.Take(3)), false);
        }

        // ---- per frame ---------------------------------------------------------------------------------------

        /// <summary>Narrower screens get narrower panels: below 16:10 'mbv-narrow', below about 4:3 'mbv-compact' too.</summary>
        protected void UpdateLayoutClasses()
        {
            float w = root.layout.width, h = root.layout.height;
            if (float.IsNaN(w) || h <= 0f) return;
            float aspect = w / h;
            root.EnableInClassList("mbv-narrow", aspect < 1.58f);
            root.EnableInClassList("mbv-compact", aspect < 1.4f);
        }

        /// <summary>The shared part of a frame: saving, the reload, the undo buttons, the status line. False: nothing more this
        /// frame (the builder is closing).</summary>
        protected bool UpdateCommon(float dt)
        {
            if (!ModBuilder.Active) return false;
            UpdateLayoutClasses();
            nowMs += dt;
            if (saveDirty && nowMs >= saveAtMs) SaveNow();
            UpdateUndoButtons();
            if (reloadPending && nowMs >= reloadAtMs && CanReload)
            {
                reloadPending = false;
                reloading = true;
                ModBuilder.Reload();
                OnReloadStarted();
            }
            if (reloading)
            {
                if (!ReloadDone) { statusMs = Mathf.Max(statusMs, 300f); if (!statusError) statusText = T("mbUpdating", "Updating the preview…"); }
                else
                {
                    reloading = false;
                    ShowProblems();
                    OnReloaded();
                    var queued = afterReload.ToArray();
                    afterReload.Clear();
                    foreach (var a in queued) a();
                }
            }
            if (statusMs > 0f) statusMs -= dt;
            status.text = statusMs > 0f ? statusText : "";
            status.style.display = status.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            status.EnableInClassList("mbv-status--error", statusError);
            return true;
        }

        /// <summary>Esc (the open modal, a text field, then the screen's own, then the Menu), Q / E in the Options, Ctrl+Z / Y.
        /// True: the keys are taken this frame (a modal or text field has them).</summary>
        protected bool ReadCommonKeys(Func<bool> escape = null)
        {
            var kb = Keyboard.current;
            if (kb == null) return true;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            bool typing = focused != null && TextFieldKeys.InTextField(focused);
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (optionsView != null) { if (!optionsView.CloseResetPrompt()) ShowMenu(); }   // Options: back to the Menu
                else if (ModalOpen) CloseModal();
                else if (typing) focused.Blur();
                else if (escape == null || !escape()) ShowMenu();   // nothing else to close: the Menu, like the station's Esc
                return true;
            }
            if (optionsView != null)
            {
                // Q / E switch the Options tabs, as in the game's menus.
                if (kb.qKey.wasPressedThisFrame) optionsView.StepTab(-1);
                if (kb.eKey.wasPressedThisFrame) optionsView.StepTab(1);
                return true;
            }
            if (typing || ModalOpen) return true;
            // Ctrl+Z undo, Ctrl+Y / Ctrl+Shift+Z redo (a text field keeps its own).
            if (kb.ctrlKey.isPressed && kb.zKey.wasPressedThisFrame) { UndoRedo(kb.shiftKey.isPressed); return true; }
            if (kb.ctrlKey.isPressed && kb.yKey.wasPressedThisFrame) { UndoRedo(true); return true; }
            return false;
        }
    }
}
