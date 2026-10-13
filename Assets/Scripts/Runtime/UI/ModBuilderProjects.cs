// ModBuilderProjects.cs
// Remake-only: the main menu's Mod Builder page (Mods > Mod Builder): the player's mod projects (Modding.ModProject,
// Application.persistentDataPath/ModProjects). On the left the projects (the newest first) under a "new project" row
// (a name and Create); on the right the selected project: its name, author, version and description (saved as they are
// typed), what it holds by kind of content (Ships, Items, Characters and Skins open the hangar editor on that kind; the other
// kinds show "Coming soon" for now),
// and Open in hangar, Export as mod (zips it into the Mods folder, asks before replacing an installed mod with its id),
// Open folder (desktop) and Delete (asks).
// Plain C#: MainMenu creates it in its panel host and hands it its sound, focus, dialog hooks and the opening of a project
// (Modding.ModBuilder.Begin, then the Station scene behind the menu's fade).

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class ModBuilderProjects
    {
        public VisualElement Panel { get; }

        readonly Action<VisualElement> hookFocus;
        readonly Action click;
        readonly Action<ModProject> open;
        readonly Action<string, string, Action> ask;
        readonly Action<string, string> notice;
        readonly ScrollView list;
        readonly Label count, empty, note, title, meta;
        readonly VisualElement detail, content;
        readonly TextField newName, nameField, authorField, versionField, descField;
        readonly Label idHint;
        readonly Button openButton, exportButton, deleteButton;
        List<ModProject> projects = new List<ModProject>();
        ModProject selected;
        bool filling;

        static string T(string key, string english) => Localization.Extra(key, english);
        static string Count(int n, string oneKey, string one, string manyKey, string many) => n == 1 ? T(oneKey, one) : string.Format(T(manyKey, many), n);

        /// <summary>The kinds of content a mod can hold, in the order the page shows them; ships, items, characters and skins
        /// can be built so far (their ModBuilder.Kind in Buildable).</summary>
        static readonly (string key, string english, string desc)[] Kinds =
        {
            ("mbShips", "Ships", "New ships from a .glb model, or changes to the game's ships: stats, hardpoints, test flights."),
            ("mbItems", "Items", "New weapons and equipment, or changes to the game's items."),
            ("mbFx", "Weapon fx", "Projectiles, muzzle flashes, impacts and shot sounds."),
            ("mbSystems", "Systems", "New star systems and stations on the map."),
            ("mbInteriors", "Hangars", "Custom hangar and Space Lounge rooms."),
            ("mbQuests", "Quests", "Event graphs: quests and missions with dialogue."),
            ("mbCharacters", "Characters", "Portraits and names for conversations: your own characters, or new looks for the story's."),
            ("mbCampaigns", "Campaigns", "A new game of your own: start, galaxy, ships."),
            ("mbAudio", "Audio", "Replace or add music, sound effects and voices."),
            ("mbSkins", "Skins", "New textures for the game's ships: hull paint, lights and engine glow, for one ship or all that share them."),
            ("mbBlueprints", "Blueprints", "Ships and items built from ingredients."),
        };

        /// <summary>The kinds the builder edits: their place in Kinds and the builder's tab.</summary>
        static ModBuilder.Kind? Buildable(string key) => key switch
        {
            "mbShips" => ModBuilder.Kind.Ships, "mbItems" => ModBuilder.Kind.Items, "mbCharacters" => ModBuilder.Kind.Characters,
            "mbSkins" => ModBuilder.Kind.Skins, _ => null,
        };

        public ModBuilderProjects(VisualElement host, Action back, Action<VisualElement> hookFocus, Action click, Action<ModProject> open,
                                  Action<string, string, Action> ask, Action<string, string> notice)
        {
            this.hookFocus = hookFocus;
            this.click = click;
            this.open = open;
            this.ask = ask;
            this.notice = notice;
            Panel = new VisualElement { name = ModBuilder.ProjectsPanel };
            Panel.AddToClassList("panel");
            Panel.AddToClassList("mods-panel");
            Panel.usageHints = UsageHints.DynamicTransform;

            var titleRow = Add(Panel, new VisualElement(), "mods-title-row");
            Add(titleRow, new Label(T("mbTitle", "Mod Builder").ToUpperInvariant()), "panel-title", "gof-semibold");
            Add(titleRow, new Label(T("mpExperimental", "Experimental").ToUpperInvariant()), "mp-badge");
            Add(titleRow, new VisualElement(), "mp-title-spacer");
            count = Add(titleRow, new Label(), "mods-count");
            Add(Panel, new VisualElement(), "panel-accent");

            var body = Add(Panel, new VisualElement(), "mods-body");
            var left = Add(body, new VisualElement(), "mods-left");
            var newRow = Add(left, new VisualElement(), "mb-new-row");
            newName = Add(newRow, new TextField { maxLength = 40 }, "mb-field", "mb-new-name");
            // One field for both: what is typed filters the list (every word must match the name, id, author or
            // description), and Create / Enter makes a new project of that name.
            newName.textEdition.placeholder = T("mbNewPlaceholder", "Search, or name a new project");
            newName.RegisterValueChangedCallback(_ => RebuildRows());
            newName.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                e.StopPropagation();
                Create();
            }, TrickleDown.TrickleDown);
            ActionButton(newRow, T("mbCreate", "Create"), Create, "mb-create");
            list = Add(left, new ScrollView(ScrollViewMode.Vertical), "mods-list");
            list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(list);
            empty = Add(left, new Label(), "mods-empty", "mb-empty");

            detail = Add(body, new VisualElement(), "mods-detail");
            var detailScroll = Add(detail, new ScrollView(ScrollViewMode.Vertical), "mb-detail-scroll");
            detailScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            detailScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            title = Add(detailScroll, new Label(), "mods-name", "gof-semibold");
            meta = Add(detailScroll, new Label(), "mods-meta");
            var fields = Add(detailScroll, new VisualElement(), "mb-fields");
            nameField = Field(fields, T("mbName", "Name"), 60, v => { selected.Name = v.Trim().Length > 0 ? v.Trim() : selected.Id; title.text = selected.Name; });
            idHint = Add(nameField.parent, new Label(), "mb-id-hint");
            nameField.RegisterValueChangedCallback(_ => UpdateIdHint());
            // Leaving the name (or Enter) also moves an id that doesn't match it yet (a project renamed before the id
            // followed the name) without the name being edited.
            nameField.RegisterCallback<FocusOutEvent>(_ => { SaveIfDirty(); FollowName(); });
            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                SaveIfDirty();
                FollowName();
            });
            var pair = Add(fields, new VisualElement(), "mb-field-pair");
            authorField = Field(pair, T("mbAuthor", "Author"), 40, v => selected.Author = v.Trim());
            versionField = Field(pair, T("mbVersion", "Version"), 16, v => selected.Version = v.Trim().Length > 0 ? v.Trim() : "1.0");
            descField = Field(fields, T("mbDescription", "Description"), 600, v => selected.Description = v, true);
            Add(detailScroll, new Label(T("mbContent", "Content").ToUpperInvariant()), "mb-section", "gof-semibold");
            content = Add(detailScroll, new VisualElement(), "mb-kinds");

            var actions = Add(detail, new VisualElement(), "mods-actions", "mb-actions");
            openButton = ActionButton(actions, T("mbOpen", "Open in hangar"), () => { if (selected != null) { selected.Save(); open(selected); } }, "mods-toggle");
            exportButton = ActionButton(actions, T("mbExport", "Export as mod"), Export);
            if (!Application.isMobilePlatform)
                ActionButton(actions, T("mbFolder", "Open folder"), () => { if (selected != null) Application.OpenURL("file:///" + selected.Folder.Replace('\\', '/')); });
            deleteButton = ActionButton(actions, T("modsDelete", "Delete"), AskDelete, "mods-delete");

            var footer = Add(Panel, new VisualElement(), "mods-footer");
            note = Add(footer, new Label(), "mods-note");
            var backButton = Add(Panel, new Button(back) { text = "‹  " + Localization.Get(170).ToUpperInvariant() }, "menu-button", "back-button", "gof-semibold");
            hookFocus(backButton);
            host.Add(Panel);
        }

        static TE Add<TE>(VisualElement parent, TE e, params string[] classes) where TE : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            parent.Add(e);
            return e;
        }

        Button ActionButton(VisualElement parent, string text, Action onClick, string extra = null)
        {
            var b = new Button(() => { click(); onClick(); }) { text = text.ToUpperInvariant() };
            b.AddToClassList("mods-action");
            if (extra != null) b.AddToClassList(extra);
            hookFocus(b);
            parent.Add(b);
            return b;
        }

        /// <summary>A labelled text field whose edits go straight into the selected project (saved when the page is left).</summary>
        TextField Field(VisualElement parent, string label, int max, Action<string> set, bool multiline = false)
        {
            var box = Add(parent, new VisualElement(), "mb-field-box");
            Add(box, new Label(label.ToUpperInvariant()), "mb-field-label");
            var f = Add(box, new TextField { maxLength = max, multiline = multiline }, "mb-field");
            if (multiline) f.AddToClassList("mb-field--multi");
            f.RegisterValueChangedCallback(e => { if (!filling && selected != null) { set(e.newValue); dirty = true; } });
            f.RegisterCallback<FocusOutEvent>(_ => SaveIfDirty());
            return f;
        }

        /// <summary>The line under the name (like GitHub's "will be created as"): the mod id the name gives, in grey, and
        /// the id it had while that differs; the project takes it when the name is saved (FollowName).</summary>
        void UpdateIdHint()
        {
            if (selected == null) { idHint.text = ""; return; }
            string id = selected.IdFromName(nameField.value);
            idHint.text = id == selected.Id
                ? string.Format(T("mbIdHint", "Mod id: {0}"), id)
                : string.Format(T("mbIdHintChange", "Mod id: {0}  (was {1})"), id, selected.Id);
        }

        /// <summary>The name was saved: the mod id follows it (ModProject.ChangeId: mod.json, the project's own "id:" keys,
        /// its folder; the next export replaces the mod installed under the old id).</summary>
        void FollowName()
        {
            if (selected == null || selected.LoadError != null) return;
            string id = selected.IdFromName(selected.Name);
            if (id == selected.Id) return;
            var p = selected;
            var renamed = p.ChangeId(id, out string error);
            if (renamed == null) { notice(T("mbTitle", "Mod Builder"), string.Format(T("mbIdFailed", "The mod id couldn't follow the name: {0}"), error)); return; }
            int at = projects.IndexOf(p);
            if (at >= 0) projects[at] = renamed;
            selected = renamed;
            RebuildRows();
            Refresh();
        }

        bool dirty;

        void SaveIfDirty()
        {
            if (!dirty || selected == null) return;
            dirty = false;
            selected.Save();
            FollowName();
            RebuildRows();
        }

        /// <summary>Opening the page: the projects read again.</summary>
        public void Open()
        {
            string keep = selected?.Folder;
            projects = ModProject.List();
            selected = projects.FirstOrDefault(p => p.Folder == keep) ?? projects.FirstOrDefault();
            RebuildRows();
            Refresh();
        }

        /// <summary>Leaving the page: what was typed is kept.</summary>
        public void Close() => SaveIfDirty();

        void RebuildRows()
        {
            list.Clear();
            var shown = projects.Where(Matches).ToList();
            foreach (var p in shown)
            {
                var row = new Button(() => { click(); Select(p); }) { userData = p };
                row.AddToClassList("mods-row");
                var thumb = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mods-thumb", "mb-thumb");
                var icon = ProjectIcon(p);
                if (icon != null) thumb.style.backgroundImage = new StyleBackground(icon);
                var text = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mods-row-text");
                Add(text, new Label(p.Name) { pickingMode = PickingMode.Ignore }, "mods-row-name");
                Add(text, new Label(Sub(p)) { pickingMode = PickingMode.Ignore }, "mods-row-sub");
                row.EnableInClassList("mods-row--selected", p == selected);
                int hoverFrame = -10;
                row.RegisterCallback<PointerEnterEvent>(_ => hoverFrame = Time.frameCount);
                hookFocus(row);
                row.RegisterCallback<FocusInEvent>(_ => { if (Time.frameCount - hoverFrame > 1) Select(p); });
                list.Add(row);
            }
            empty.text = projects.Count == 0
                ? T("mbEmpty", "No projects yet. Type a name above and press Create to start your first mod.")
                : T("mbNoMatch", "No project matches. Press Create to start one with this name.");
            empty.style.display = shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            count.text = projects.Count == 1 ? T("mbCountOne", "1 project") : string.Format(T("mbCount", "{0} projects"), projects.Count);
            note.text = string.Format(T("mbNote", "Projects are kept in {0}. Export as mod puts a finished one into your Mods folder, where it is turned on in the Mod Browser."),
                                      ModProject.Root);
        }

        /// <summary>The search field's filter: every word typed is in the project's name, id, author or description.</summary>
        bool Matches(ModProject p)
        {
            string q = newName?.value?.Trim();
            if (string.IsNullOrEmpty(q)) return true;
            string hay = $"{p.Name} {p.Id} {p.Author} {p.Description}";
            foreach (var w in q.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (hay.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        // Icons read from the projects' folders, by path and change time (the list is rebuilt on every visit).
        static readonly Dictionary<string, Texture2D> fileIcons = new Dictionary<string, Texture2D>();

        /// <summary>The row's picture: the first ship's shop icon (its own made by the builder, an original's for an
        /// override), else the first re-skinned ship's, else the first item's (its own icon or its original's).</summary>
        static Texture2D ProjectIcon(ModProject p)
        {
            foreach (var s in p.Ships)
            {
                var own = FileIcon(p, s.Icon);
                if (own != null) return own;
                if (s.IsOverride && s.OverrideIndex >= 0) return ItemInfo.ShipIcon(s.OverrideIndex);
            }
            var skins = p.SkinShips();
            if (skins.Count > 0) return ItemInfo.ShipIcon(skins[0]);
            foreach (var i in p.Items)
            {
                var own = FileIcon(p, i.Icon);
                if (own != null) return own;
                if (i.Original >= 0) return ItemInfo.ItemIcon(i.Original);
            }
            return null;
        }

        static Texture2D FileIcon(ModProject p, string rel)
        {
            if (string.IsNullOrEmpty(rel)) return null;
            string path = System.IO.Path.Combine(p.Folder, rel);
            if (!System.IO.File.Exists(path)) return null;
            string key = path + "|" + System.IO.File.GetLastWriteTimeUtc(path).Ticks;
            if (fileIcons.TryGetValue(key, out var t) && t != null) return t;
            try { t = ImageImport.Decode(System.IO.File.ReadAllBytes(path), rel, false, false, out _); }
            catch { t = null; }
            if (t != null) t.wrapMode = TextureWrapMode.Clamp;
            fileIcons[key] = t;
            return t;
        }

        /// <summary>What the project holds, kind by kind ("2 ships · 1 skin"), or that it is still empty.</summary>
        static string Contents(ModProject p)
        {
            var parts = new List<string>();
            void Part(int n, string oneKey, string one, string manyKey, string many) { if (n > 0) parts.Add(Count(n, oneKey, one, manyKey, many)); }
            Part(p.Ships.Count, "mbOneShip", "1 ship", "mbShipsN", "{0} ships");
            Part(p.Items.Count, "mbOneItem", "1 item", "mbItemsN", "{0} items");
            Part(p.Characters.Count, "mbOneCharacter", "1 character", "mbCharactersN", "{0} characters");
            Part(p.SkinShips().Count, "mbOneSkin", "1 skin", "mbSkinsN", "{0} skins");
            return parts.Count > 0 ? string.Join("  ·  ", parts) : T("mbProjectEmpty", "Empty");
        }

        static bool HasContent(ModProject p) => p.Ships.Count + p.Items.Count + p.Characters.Count + p.SkinShips().Count > 0;

        static string Sub(ModProject p) => $"{Contents(p)}  ·  v{p.Version}  ·  {p.Modified.ToLocalTime():yyyy-MM-dd HH:mm}";

        void Select(ModProject p)
        {
            if (selected == p) return;
            SaveIfDirty();
            selected = p;
            foreach (var row in list.Children())
                row.EnableInClassList("mods-row--selected", row.userData == p);
            Refresh();
        }

        void Refresh()
        {
            detail.style.visibility = selected != null ? Visibility.Visible : Visibility.Hidden;
            if (selected == null) return;
            filling = true;
            title.text = selected.Name;
            meta.text = selected.LoadError != null ? "✖ " + selected.LoadError : "";
            meta.style.display = selected.LoadError != null ? DisplayStyle.Flex : DisplayStyle.None;
            nameField.value = selected.Name;
            UpdateIdHint();
            authorField.value = selected.Author;
            versionField.value = selected.Version;
            descField.value = selected.Description;
            filling = false;
            content.Clear();
            for (int i = 0; i < Kinds.Length; i++)
            {
                var (key, english, desc) = Kinds[i];
                var kind = Buildable(key);
                bool on = kind != null;
                var tile = new Button(() => { if (on && selected != null) { click(); selected.Save(); ModBuilder.OpenKind = kind.Value; open(selected); } });
                tile.AddToClassList("mb-kind");
                tile.SetEnabled(on);
                if (!on) tile.AddToClassList("mb-kind--soon");
                Add(tile, new Label(T(key, english).ToUpperInvariant()) { pickingMode = PickingMode.Ignore }, "mb-kind-title", "gof-semibold");
                string state = kind switch
                {
                    ModBuilder.Kind.Ships => Count(selected.Ships.Count, "mbOneShip", "1 ship", "mbShipsN", "{0} ships"),
                    ModBuilder.Kind.Items => Count(selected.Items.Count, "mbOneItem", "1 item", "mbItemsN", "{0} items"),
                    ModBuilder.Kind.Characters => Count(selected.Characters.Count, "mbOneCharacter", "1 character", "mbCharactersN", "{0} characters"),
                    ModBuilder.Kind.Skins => Count(selected.SkinShips().Count, "mbOneSkin", "1 skin", "mbSkinsN", "{0} skins"),
                    _ => T("mbSoon", "Coming soon"),
                };
                Add(tile, new Label(state.ToUpperInvariant()) { pickingMode = PickingMode.Ignore }, "mb-kind-state");
                tile.tooltip = T(key + "Desc", desc);
                if (on) hookFocus(tile);
                content.Add(tile);
            }
            openButton.SetEnabled(selected.LoadError == null);
            deleteButton.SetEnabled(true);
            exportButton.SetEnabled(selected.LoadError == null);
        }

        void Create()
        {
            string name = newName.value?.Trim();
            if (string.IsNullOrEmpty(name)) { newName.Focus(); return; }
            ModProject p;
            try { p = ModProject.Create(name, GoF2Remake.Multiplayer.NetGame.PlayerName); }
            catch (Exception e) { notice(T("mbTitle", "Mod Builder"), string.Format(T("mbCreateFailed", "The project couldn't be created: {0}"), e.Message)); return; }
            newName.value = "";
            projects.Insert(0, p);
            selected = p;
            RebuildRows();
            Refresh();
        }

        void Export()
        {
            if (selected == null) return;
            SaveIfDirty();
            string heading = T("mbExport", "Export as mod");
            void Run()
            {
                string path = selected.Export(out string error);
                if (path == null) notice(heading, string.Format(T("mbExportFailed", "The mod couldn't be exported: {0}"), error));
                else notice(heading, string.Format(T("mbExported", "\"{0}\" is in your Mods folder:\n{1}\nTurn it on in the Mod Browser to play with it."), selected.Name, path));
            }
            if (!HasContent(selected)) { notice(heading, T("mbExportEmpty", "This project has nothing in it yet. Open it in the hangar and add a ship, item, character or skin first.")); return; }
            var old = selected.InstalledMod;
            if (old != null)
                ask(heading, string.Format(T("mbExportReplace", "A mod with the id \"{0}\" is already installed (\"{1}\" v{2}). Replace it with this project?"),
                                           selected.Id, old.Name, old.Version), Run);
            else Run();
        }

        void AskDelete()
        {
            if (selected == null) return;
            var p = selected;
            string heading = T("mbDeleteTitle", "Delete project");
            ask(heading, string.Format(T("mbDeleteText", "Delete the project \"{0}\" and all its files? This can't be undone. (A mod you exported from it stays installed.)"), p.Name), () =>
            {
                if (!p.Delete(out string error)) { notice(heading, string.Format(T("mbDeleteFailed", "The project couldn't be deleted: {0}"), error)); return; }
                projects.Remove(p);
                selected = projects.FirstOrDefault();
                RebuildRows();
                Refresh();
            });
        }
    }
}
