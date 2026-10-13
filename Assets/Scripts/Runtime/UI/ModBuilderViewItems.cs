// ModBuilderViewItems.cs
// The Mod Builder's Items kind (part of ModBuilderView): the project's items.json (ProjectItem). Left: the items, new ones
// made from a game item (a copy of it: its type, category, look and behaviour, Modding/README.md "New items") or changes to
// a game item ("override"), Remove. Right: the selected one's name, description, shop values (tech level, occurrence, the
// price range), its own shop icon (a PNG, else the base's) and the stats its base item has, by their readable names
// (Modding.ItemStats); a value left at the base's
// isn't written, so the base's stays. "Fit to the test ship" mounts it on the Ships tab's ship for Test fly (once the game
// has read the project and numbered a new item). Edits are written at once; the game reads them at the next test flight or
// when the builder leaves this kind (the loadout's pickers then list them).

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
        VisualElement itemList, itemPage;
        ProjectItem currentItem;
        /// <summary>Item edits the game hasn't read yet.</summary>
        bool itemsChanged;

        static readonly string[] TypeLabels = { "Primary weapon", "Secondary weapon", "Turret", "Equipment", "Commodity" };

        void BuildItemsPanels(VisualElement left, VisualElement right)
        {
            Add(left, new Label(T("mbItems", "Items").ToUpperInvariant()), "mbv-heading", "gof-semibold");
            var scroll = Add(left, new ScrollView(ScrollViewMode.Vertical), "mbv-ship-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(scroll);
            itemList = scroll;
            Act(left, T("mbNewItem", "+ New item"), PickItemBase, "mbv-add");
            Act(left, T("mbChangeItem", "+ Change a game item"), PickItemOverride, "mbv-add");
            Act(left, T("mbRemoveItem", "Remove item"), AskRemoveItem, "mbv-danger");

            var page = Add(right, new ScrollView(ScrollViewMode.Vertical), "mbv-right-scroll");
            page.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            page.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            itemPage = Add(page, new VisualElement(), "mbv-page");
        }

        /// <summary>The item's number in the game: an override's item, a new item's once the game has read it (-1 before).</summary>
        int ItemIndex(ProjectItem it) =>
            it == null ? -1 : it.IsOverride ? it.OverrideIndex : ModContent.ItemIndexOf(Project.Id + ":" + it.LocalId);

        /// <summary>The icon the builder shows for an item: its own PNG (read from the project as it is now), else the game's
        /// (a new item's once numbered), else its base's.</summary>
        Texture2D ItemIconOf(ProjectItem it)
        {
            if (!string.IsNullOrEmpty(it.Icon) && ModManager.BuilderProject != null)
            {
                var own = ModMaterials.Texture(ModManager.BuilderProject, it.Icon, false);
                if (own != null) return own;
            }
            int index = ItemIndex(it);
            return index >= 0 ? ItemInfo.ItemIcon(index) : it.Original >= 0 ? ItemInfo.ItemIcon(it.Original) : null;
        }

        string ItemTitle(ProjectItem it) =>
            !string.IsNullOrEmpty(it.Name) ? it.Name : it.Original >= 0 ? GameNames.Item(it.Original) : it.LocalId ?? "?";

        void RebuildItems()
        {
            itemList.Clear();
            if (Project == null) return;
            if (currentItem != null && !Project.Items.Contains(currentItem)) currentItem = null;
            currentItem ??= Project.Items.FirstOrDefault();
            foreach (var it in Project.Items)
            {
                var item = it;
                var row = new Button(() => { sound(false); currentItem = item; RebuildItems(); BuildItemPage(); }) { focusable = false };
                row.AddToClassList("mbv-ship-row");
                row.EnableInClassList("mbv-ship-row--on", it == currentItem);
                var icon = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-ship-icon");
                var tex = ItemIconOf(it);
                if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
                var text = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-ship-text");
                Add(text, new Label(ItemTitle(it)) { pickingMode = PickingMode.Ignore }, "mbv-ship-name");
                string sub = it.IsOverride
                    ? string.Format(T("mbChanges", "Changes {0}"), it.Original >= 0 ? GameNames.Item(it.Original) : (string)it.ToJson()["override"])
                    : string.Format(T("mbBasedOn", "New · based on {0}"), it.Original >= 0 ? GameNames.Item(it.Original) : (string)it.ToJson()["base"]);
                Add(text, new Label(sub) { pickingMode = PickingMode.Ignore }, "mbv-ship-sub");
                itemList.Add(row);
            }
            if (Project.Items.Count == 0)
                Note(itemList, T("mbNoItems", "No items yet. Make a new one from a game item, or change one of the game's items."));
        }

        void BuildItemPage()
        {
            itemPage.Clear();
            var it = currentItem;
            if (it == null) { Note(itemPage, T("mbPickItem", "Add an item on the left to start.")); return; }
            var db = Database.Shared;
            var original = it.Original >= 0 ? db.Item(it.Original) : null;
            bool isNew = !it.IsOverride;

            var head = Add(itemPage, new VisualElement(), "mbv-item-head");
            var icon = Add(head, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-item-head-icon");
            var tex = ItemIconOf(it);
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
            var headText = Add(head, new VisualElement(), "mbv-ship-text");
            Add(headText, new Label((isNew ? T("mbNewItemSub", "New item") : string.Format(T("mbChanges", "Changes {0}"), GameNames.Item(it.Original))).ToUpperInvariant()), "mbv-heading", "gof-semibold");
            if (original != null)
                Add(headText, new Label($"{T("mbItemType" + original.TypeId, TypeLabels[Mathf.Clamp(original.TypeId, 0, 4)])} · {ItemInfo.Category(original)}"
                                        + (isNew ? "  ·  " + string.Format(T("mbBasedOnShort", "based on {0}"), GameNames.Item(it.Original)) : "")), "mbv-note");

            var name = Text(itemPage, T("mbName", "Name"), it.Name, 40, v => { it.Name = v.Trim(); ItemEdited(); RebuildItems(); });
            if (original != null) name.textEdition.placeholder = GameNames.Item(it.Original);
            var desc = Text(itemPage, T("mbDescription", "Description"), it.Description, 600, v => { it.Description = v; ItemEdited(); }, true);
            if (original != null && it.IsOverride) desc.textEdition.placeholder = GameNames.ItemDescription(it.Original);
            if (original == null)
            {
                Note(itemPage, T("mbItemOtherMod", "This item is built on another mod's item: its other values are set by hand in items.json."));
                return;
            }

            // A new item's values equal to its base's aren't written (the base's stay); an override's are once changed.
            int? Opt(int v, int baseValue) => isNew && v == baseValue ? (int?)null : v;
            Heading(itemPage, T("mbShop", "Shop"));
            var irow = Add(itemPage, new VisualElement(), "mbv-row");
            Add(irow, new Label($"{T("mbIcon", "Icon")}: {(it.Icon != null ? System.IO.Path.GetFileName(it.Icon) : T("mbBaseIcon", "the base item's"))}"), "mbv-inline-label");
            if (ModImport.Available)
                Act(irow, T("mbChoose", "Choose…"), () => PickImage(T("mbPickIcon", "Choose a shop icon (180 x 88)"), path =>
                    {
                        string old = it.Icon;
                        it.Icon = Project.AddFile(path, "icons", it.IsOverride ? "item_" + it.OverrideIndex : it.LocalId, ".png");
                        Project.DeleteFile(old, Project.Items.Any(o => o != it && o.Icon == old));
                        ItemEdited();
                        RebuildItems();
                        BuildItemPage();
                    }));
            if (it.Icon != null)
                Act(irow, "✕", () =>
                {
                    string old = it.Icon;
                    it.Icon = null;
                    Project.DeleteFile(old, Project.Items.Any(o => o != it && o.Icon == old));
                    ItemEdited();
                    RebuildItems();
                    BuildItemPage();
                }, "mbv-step");
            Note(itemPage, T("mbIconNote", "180 x 88 like the game's icons, frame and background included (copy one of the game's to start from)."));
            Int(itemPage, T("mbTechLevel", "Tech level (1-10)"), it.TechLevel ?? original.techLevel, 1, 10, v => { it.TechLevel = Opt(v, original.techLevel); ItemEdited(); });
            Int(itemPage, T("mbOccurrence", "How often shops stock it (0 never - 90)"), it.Occurrence ?? original.occurrence, 0, 100,
                v => { it.Occurrence = Opt(v, original.occurrence); ItemEdited(); });
            IntegerField minField = null, maxField = null;
            void Prices()
            {
                int min = minField.value, max = Mathf.Max(min, maxField.value);
                if (max != maxField.value) maxField.SetValueWithoutNotify(max);
                if (isNew && min == original.minPrice && max == original.maxPrice) it.SetPrices(null, null);
                else it.SetPrices(min, max);
                ItemEdited();
            }
            minField = Int(itemPage, T("mbMinPrice", "Lowest price"), it.MinPrice ?? original.minPrice, 0, 999999999, _ => Prices());
            maxField = Int(itemPage, T("mbMaxPrice", "Highest price"), it.MaxPrice ?? original.maxPrice, 0, 999999999, _ => Prices());

            Heading(itemPage, T("mbItemStats", "Stats"));
            var names = StatNames();
            int shown = 0;
            foreach (int id in original.attrKeys)
            {
                if (!names.TryGetValue(id, out var statName)) continue;
                int attr = id, baseValue = original.Attr(id);
                Int(itemPage, StatLabel(statName), it.Stat(id) ?? baseValue, -999999999, 999999999, v =>
                {
                    it.SetStat(attr, statName, isNew && v == baseValue ? (int?)null : v);
                    ItemEdited();
                });
                shown++;
            }
            if (shown == 0) Note(itemPage, T("mbNoStats", "This kind of item has no stats to change."));
            else Note(itemPage, T("mbStatsNote", "Times are in milliseconds, speeds in game units per ms (20 units = 1 m). A shot's range is its speed x its lifetime."));

            Heading(itemPage, T("mbTry", "Try it"));
            var tryRow = Add(itemPage, new VisualElement(), "mbv-row");
            if (original.TypeId <= 3) Act(tryRow, T("mbFitItem", "Fit to the test ship"), () => FitItem(it));
            if (it.IsOverride) Act(tryRow, T("mbUndoItem", "Undo all changes"), () => Confirm(T("mbUndoItem", "Undo all changes"),
                string.Format(T("mbUndoItemText", "Put {0} back as the game has it?"), GameNames.Item(it.Original)), () =>
                {
                    int at = Project.Items.IndexOf(it);
                    Project.Items[at] = currentItem = new ProjectItem(new Newtonsoft.Json.Linq.JObject { ["override"] = it.OverrideIndex });
                    ItemEdited();
                    RebuildItems();
                    BuildItemPage();
                }), "mbv-danger");
            Note(itemPage, original.TypeId <= 3
                ? T("mbFitNote", "Mounts it on the ship being built (the Ships tab's Loadout), then press Test fly to fly it against the pirates.")
                : T("mbCommodityNote", "Commodities are bought and sold in shops; they can't be mounted."));
        }

        void ItemEdited()
        {
            itemsChanged = true;
            MarkDirty();
        }

        /// <summary>The first readable stat name of every attribute (ItemStats).</summary>
        static Dictionary<int, string> StatNames()
        {
            var d = new Dictionary<int, string>();
            foreach (var kv in ItemStats.Readable()) if (!d.ContainsKey(kv.Value)) d[kv.Value] = kv.Key;
            return d;
        }

        /// <summary>"reloadMs" -> "Reload (ms)", "shieldCapacity" -> "Shield capacity".</summary>
        static string StatLabel(string name)
        {
            string key = "mbStat_" + name;
            var words = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsUpper(c) && words.Length > 0) words.Append(' ');
                words.Append(words.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            string label = words.ToString();
            if (label.EndsWith(" ms")) label = label.Substring(0, label.Length - 3) + " (ms)";
            return T(key, label);
        }

        /// <summary>The game items a new item can be based on / changed: every original one that can be bought.</summary>
        List<(string, Texture2D, Action)> GameItemChoices(Action<int> pick, Func<int, bool> skip = null)
        {
            var db = Database.Shared;
            var choices = new List<(string, Texture2D, Action)>();
            foreach (var it in db.Items.Where(i => !i.modded && i.maxPrice > 0 && !Session.Unsaleable.Contains(i.index) && !ModContent.IsMissingItem(i.index))
                                       .OrderBy(i => i.TypeId).ThenBy(i => i.categoryId).ThenBy(i => i.maxPrice))
            {
                int index = it.index;
                if (skip != null && skip(index)) continue;
                choices.Add((GameNames.Item(index), ItemInfo.ItemIcon(index), () => pick(index)));
            }
            return choices;
        }

        void PickItemBase() => ShowPicker(T("mbNewItemTitle", "Base the new item on"), GameItemChoices(index =>
        {
            string name = string.Format(T("mbCustomItem", "Custom {0}"), GameNames.Item(index));
            currentItem = Project.AddItem(index, name);
            AfterItemAdded();
            Status(string.Format(T("mbItemMade", "A copy of {0}: give it a name and its own stats."), GameNames.Item(index)), false);
        }));

        void PickItemOverride()
        {
            var taken = new HashSet<int>(Project.Items.Where(i => i.IsOverride).Select(i => i.OverrideIndex));
            ShowPicker(T("mbChangeItemTitle", "Change a game item"), GameItemChoices(index =>
            {
                currentItem = Project.AddItemOverride(index);
                AfterItemAdded();
            }, taken.Contains));
        }

        void AfterItemAdded()
        {
            SaveNow();
            itemsChanged = false;
            RequestReload(false);   // a new item gets its number (the loadout and Fit use it)
            RebuildItems();
            BuildItemPage();
        }

        void AskRemoveItem()
        {
            var it = currentItem;
            if (it == null) return;
            Confirm(T("mbRemoveItem", "Remove item"), string.Format(T("mbRemoveItemText", "Remove \"{0}\" from the project?"), ItemTitle(it)), () =>
            {
                Project.Items.Remove(it);
                currentItem = Project.Items.FirstOrDefault();
                SaveNow();
                itemsChanged = false;
                RequestReload(false);
                RebuildItems();
                BuildItemPage();
            });
        }

        /// <summary>Mounts the item on the test ship: a weapon in every slot of its kind, equipment in place of one of its
        /// category (else a free slot, else the last). A new item waits for the game to read the project first.</summary>
        void FitItem(ProjectItem it)
        {
            if (Current == null) { Status(T("mbNoShipToFit", "Add a ship in the Ships tab first: the item is fitted to it."), true); return; }
            int index = ItemIndex(it);
            if (index < 0 || itemsChanged || reloadPending || reloading)
            {
                itemsChanged = false;
                afterReload.Add(() => FitItem(it));
                if (!reloadPending && !reloading) RequestReload(false);
                Status(T("mbUpdating", "Updating the preview…"), false);
                return;
            }
            var db = Database.Shared;
            var data = db.Item(index);
            if (data == null || data.TypeId > 3) { Status(T("mbCantFit", "That item can't be mounted."), true); return; }
            int kind = data.TypeId;
            var s = Current;
            if (s.SlotCount(kind) <= 0)
            {
                Status(string.Format(T("mbNoSlotFor", "{0} has no slot for it: give it one in the Ships tab."), ShipName(s, db)), true);
                return;
            }
            var slots = SlotsOf(db, s);
            if (kind < 3)
                for (int i = 0; i < s.SlotCount(kind); i++) SetSlot(kind, i, index);
            else
            {
                int slot = slots[3].FindIndex(i => db.Item(i)?.category == data.category);
                if (slot < 0) slot = slots[3].Count < s.SlotCount(3) ? slots[3].Count : s.SlotCount(3) - 1;
                SetSlot(3, slot, index);
            }
            Status(string.Format(T("mbFitted", "{0} is on {1}. Press Test fly to try it."), ItemTitle(it), ShipName(s, db)), false);
        }
    }
}
