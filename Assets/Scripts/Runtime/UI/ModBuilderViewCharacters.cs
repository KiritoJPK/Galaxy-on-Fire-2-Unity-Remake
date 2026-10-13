// ModBuilderViewCharacters.cs
// The Mod Builder's Characters kind (part of ModBuilderView): the project's characters.json (ProjectCharacter,
// Modding/README.md "Characters"), edited in the station's Space Lounge (ModBuilderView.SelectKind). Left: the characters with their portraits, a new one from a PNG portrait, Remove.
// Right: the portrait as a conversation shows it (UI.Portrait.ShowCharacter: the game's dark backdrop under it and its
// scanline overlay, the "frame", over it), the name, the portrait file, race, gender, mirrored / frame / background, and the story character it stands in for
// ("replaces": that speaker's portrait everywhere, and its name in the conversation headers), picked from the story's
// speakers with their portraits; without one it is a character of the mod's own, for its quests, bar missions and events
// (named "mod_id:id" or by name). Edits are written at once; the game reads them when the project is read again.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        VisualElement characterList, characterPage;
        ProjectCharacter currentCharacter;

        static readonly string[] RaceWords = { "terran", "vossk", "nivelian", "midorian", "pirate", "void" };

        void BuildCharactersPanels(VisualElement left, VisualElement right)
        {
            Add(left, new Label(T("mbCharacters", "Characters").ToUpperInvariant()), "mbv-heading", "gof-semibold");
            var scroll = Add(left, new ScrollView(ScrollViewMode.Vertical), "mbv-ship-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(scroll);
            characterList = scroll;
            var add = Act(left, T("mbNewCharacter", "+ New character from a portrait…"), NewCharacter, "mbv-add");   // any image ImageImport reads
            add.SetEnabled(ModImport.Available);
            Act(left, T("mbRemoveCharacter", "Remove character"), AskRemoveCharacter, "mbv-danger");

            var page = Add(right, new ScrollView(ScrollViewMode.Vertical), "mbv-right-scroll");
            page.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            page.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            characterPage = Add(page, new VisualElement(), "mbv-page");
        }

        /// <summary>The character as the game draws it (ModCharacters.Def), from the project's files.</summary>
        ModCharacters.Def DefOf(ProjectCharacter c) => new ModCharacters.Def
        {
            mod = ModManager.BuilderProject, id = c.Id, key = Project.Id + ":" + c.Id, portrait = c.Portrait,
            name = new Dictionary<string, string> { ["en"] = c.Name ?? c.Id },
            male = c.Male, mirrored = c.Mirrored, frame = c.Frame, background = c.Background,
            race = c.Race != null ? Mathf.Max(0, Multiplayer.NetAdmin.RaceWord(c.Race)) : 0,
        };

        Texture2D PortraitOf(ProjectCharacter c) => ModManager.BuilderProject != null ? ModCharacters.Portrait(DefOf(c)) : null;

        void RebuildCharacters()
        {
            characterList.Clear();
            if (Project == null) return;
            if (currentCharacter != null && !Project.Characters.Contains(currentCharacter)) currentCharacter = null;
            currentCharacter ??= Project.Characters.FirstOrDefault();
            foreach (var ch in Project.Characters)
            {
                var c = ch;
                var row = new Button(() => { sound(false); currentCharacter = c; RebuildCharacters(); BuildCharacterPage(); }) { focusable = false };
                row.AddToClassList("mbv-ship-row");
                row.EnableInClassList("mbv-ship-row--on", c == currentCharacter);
                var thumb = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-char-thumb");
                var tex = PortraitOf(c);
                if (tex != null) thumb.style.backgroundImage = new StyleBackground(tex);
                var text = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-ship-text");
                Add(text, new Label(c.Name ?? c.Id) { pickingMode = PickingMode.Ignore }, "mbv-ship-name");
                string sub = c.Replaces != null ? string.Format(T("mbStandsIn", "Stands in for {0}"), SpeakerLabel(c.Replaces)) : T("mbOwnCharacter", "Own character");
                Add(text, new Label(sub) { pickingMode = PickingMode.Ignore }, "mbv-ship-sub");
                characterList.Add(row);
            }
            if (Project.Characters.Count == 0)
                Note(characterList, T("mbNoCharacters", "No characters yet. Add one from a portrait image (PNG, JPG, TGA, BMP, DDS or the game's .aei; 4:5 like the game's portraits)."));
        }

        void BuildCharacterPage()
        {
            characterPage.Clear();
            var c = currentCharacter;
            if (c == null) { Note(characterPage, T("mbPickCharacter", "Add a character on the left to start.")); return; }

            // The portrait as a conversation shows it, under its name.
            var preview = Add(characterPage, new VisualElement(), "mbv-char-preview");
            Add(preview, new Label((c.Name ?? c.Id).ToUpperInvariant()), "mbv-char-preview-name", "gof-semibold");
            var box = Add(preview, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-portrait");
            if (ModManager.BuilderProject != null) Portrait.ShowCharacter(box, DefOf(c), false);

            Text(characterPage, T("mbName", "Name"), c.Name, 40, v => { c.Name = v.Trim(); CharacterEdited(); RebuildCharacters(); BuildCharacterPage(); });
            Note(characterPage, string.Format(T("mbCharacterKey", "Quests, bar missions and events speak through it as {0}:{1} (or by its name): conversations, radio calls, a mission's client."), Project.Id, c.Id));

            Heading(characterPage, T("mbPortrait", "Portrait"));
            var prow = Add(characterPage, new VisualElement(), "mbv-row");
            Add(prow, new Label(System.IO.Path.GetFileName(c.Portrait ?? "")), "mbv-inline-label");
            if (ModImport.Available)
                Act(prow, T("mbChoose", "Choose…"), () => PickPng(path =>
                {
                    string old = c.Portrait;
                    c.Portrait = Project.AddFile(path, "portraits", c.Id, ".png");
                    Project.DeleteFile(old, Project.Characters.Any(o => o != c && o.Portrait == old));
                    CharacterEdited();
                    RebuildCharacters();
                    BuildCharacterPage();
                }));
            Note(characterPage, T("mbPortraitNote", "4:5 like the game's portraits (320 x 400 stays sharp); a wider picture is cropped at the sides, a taller one keeps its top."));
            Note(characterPage, T("mbPortraitLayersNote", "Scanlines: the faint lines over every portrait in the game. Dark backdrop: shows only through transparent parts of the picture (a cut-out character)."));
            var toggles = Add(characterPage, new VisualElement(), "mbv-row");
            FlagButton(toggles, T("mbMirrored", "Mirrored"), c.Mirrored, v => c.Mirrored = v);
            // characters.json "frame" / "background": the game's scanline overlay and the dark backdrop behind the picture.
            FlagButton(toggles, T("mbScanlines", "Scanlines"), c.Frame, v => c.Frame = v);
            FlagButton(toggles, T("mbBackdrop", "Dark backdrop"), c.Background, v => c.Background = v);

            Add(characterPage, new Label(T("mbRace", "Race").ToUpperInvariant()), "mbv-label");
            var races = Add(characterPage, new VisualElement(), "mbv-seg-row");
            for (int i = 0; i < RaceWords.Length; i++)
            {
                string word = RaceWords[i];
                var b = Act(races, T("race" + RaceNames[i], RaceNames[i]), () => { c.Race = word; CharacterEdited(); BuildCharacterPage(); }, "mbv-seg");
                b.EnableInClassList("mbv-seg--on", string.Equals(c.Race, word, StringComparison.OrdinalIgnoreCase));
            }
            Add(characterPage, new Label(T("mbGender", "Gender").ToUpperInvariant()), "mbv-label");
            var genders = Add(characterPage, new VisualElement(), "mbv-seg-row");
            Act(genders, T("mbMale", "Male"), () => { c.Male = true; CharacterEdited(); BuildCharacterPage(); }, "mbv-seg").EnableInClassList("mbv-seg--on", c.Male);
            Act(genders, T("mbFemale", "Female"), () => { c.Male = false; CharacterEdited(); BuildCharacterPage(); }, "mbv-seg").EnableInClassList("mbv-seg--on", !c.Male);
            Note(characterPage, T("mbRaceNote", "The race and gender pick the figure a bar mission's client stands as in the Space Lounge, and the colour of the name."));

            Heading(characterPage, T("mbStandsInForOptional", "Stands in for (optional)"));
            var srow = Add(characterPage, new VisualElement(), "mbv-row");
            Add(srow, new Label(c.Replaces != null ? SpeakerLabel(c.Replaces) : T("mbNobody", "Nobody")), "mbv-inline-label");
            Act(srow, T("mbChoose", "Choose…"), () => PickSpeaker(c));
            if (c.Replaces != null) Act(srow, "✕", () => { c.Replaces = null; CharacterEdited(); RebuildCharacters(); BuildCharacterPage(); }, "mbv-step");
            Note(characterPage, T("mbStandsInNote", "Leave it at Nobody for a character of your own. Pick a story character to replace them: their portrait everywhere, the story included, and their name in the conversation and radio headers. What other characters say and the voices stay as they are."));
        }

        void FlagButton(VisualElement parent, string label, bool on, Action<bool> set)
        {
            var b = Act(parent, $"{label}: {(on ? T("mbOn", "on") : T("mbOff", "off"))}", () => { set(!on); CharacterEdited(); RebuildCharacters(); BuildCharacterPage(); });
            b.EnableInClassList("mbv-toggle--on", on);
        }

        void CharacterEdited() => MarkDirty();

        void PickPng(Action<string> use) => PickImage(T("mbPickPortrait", "Choose a portrait"), use);   // stored as PNG

        void NewCharacter() => PickPng(path =>
        {
            string name = Pretty(System.IO.Path.GetFileNameWithoutExtension(pickedName ?? "character"));
            if (name == "New ship") name = T("mbNewCharacterName", "New character");
            string id = Project.CharacterIdFor(name);
            var c = Project.AddCharacter(name, Project.AddFile(path, "portraits", id, ".png"));
            currentCharacter = c;
            SaveNow();
            RebuildCharacters();
            BuildCharacterPage();
        });

        void AskRemoveCharacter()
        {
            var c = currentCharacter;
            if (c == null) return;
            Confirm(T("mbRemoveCharacter", "Remove character"), string.Format(T("mbRemoveCharacterText", "Remove \"{0}\" from the project?"), c.Name ?? c.Id), () =>
            {
                Project.Characters.Remove(c);
                Project.DeleteFile(c.Portrait, Project.Characters.Any(o => o.Portrait == c.Portrait));
                currentCharacter = Project.Characters.FirstOrDefault();
                SaveNow();
                RebuildCharacters();
                BuildCharacterPage();
            });
        }

        /// <summary>A "replaces" value as the story speaker's name (a number is shown by its name).</summary>
        static string SpeakerLabel(string replaces) =>
            int.TryParse(replaces, out int n) && n >= 0 && n < StoryTable.SpeakerCount ? StoryTable.OriginalSpeakerName(n) : replaces;

        /// <summary>The story's speakers with their portraits; the pick is written by name when no other speaker has it, else
        /// by number.</summary>
        void PickSpeaker(ProjectCharacter c)
        {
            modal.Clear();
            var boxEl = Add(modal, new VisualElement(), "mbv-modal-box", "mbv-modal-box--list", "mbv-panel");
            Heading(boxEl, T("mbStandsInTitle", "Stands in for"));
            var scroll = Add(boxEl, new ScrollView(ScrollViewMode.Vertical), "mbv-picker-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(scroll);
            var grid = Add(scroll, new VisualElement(), "mbv-picker-grid");
            var names = Enumerable.Range(0, StoryTable.SpeakerCount).Select(StoryTable.OriginalSpeakerName).ToList();
            var seen = new HashSet<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                if (string.IsNullOrWhiteSpace(n) || !seen.Add(n)) continue;   // one tile per name (the generic speakers repeat)
                int speaker = i;
                string value = names.Count(x => x == n) == 1 ? n : speaker.ToString();
                var tile = new Button(() =>
                {
                    sound(false);
                    CloseModal();
                    c.Replaces = value;
                    CharacterEdited();
                    RebuildCharacters();
                    BuildCharacterPage();
                }) { focusable = false };
                tile.AddToClassList("mbv-tile");
                tile.AddToClassList("mbv-speaker-tile");
                var clip = Add(tile, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-speaker-clip");
                var portrait = Add(clip, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-portrait", "mbv-portrait--small");
                Portrait.ShowSpeaker(portrait, speaker, false, original: true);
                Add(tile, new Label(n) { pickingMode = PickingMode.Ignore }, "mbv-tile-name");
                grid.Add(tile);
            }
            Act(boxEl, T("mbCancel", "Cancel"), CloseModal);
            modal.AddToClassList("mbv-modal--shown");
        }
    }
}
