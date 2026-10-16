// ModBuilderViewKits.cs
// The Mod Builder's Ship kits kind (part of ModBuilderView), edited in the hangar: customizable ships (ModShipKits,
// Modding/README.md "Customizable ships"), the project's shipkits.json (ProjectKit). Left: the kits, a new kit, Remove.
// Right: the kit (name, one scale and turn for every part: they share one origin), its slots in assembly order (add,
// rename, move, remove), the ship that flies it (a project ship with "kit": made here when there is none), its parts by slot
// (a .glb each, copied into models/) and the selected part: name, slot, the ship types that may fit it, its model and tier
// extension models, its stats (the hull, cargo and handling in % of the ship's, equipment slots, the flight bonuses), the
// weapon slots it gives, and its hardpoints, placed on the hull with the hangar's gizmo like a ship's (ModBuilderView's
// EditedMounts). Then its paint slots (a colour per material name filter, the kit's "paint" palette); presets, skins, the
// weapon and turret models and materials through Edit as JSON. The turntable shows the kit's ship with the selected part in
// its slot (ModBuilder.PreviewBuild), and Test fly flies that build. The Ships kind edits a kit ship's type and default build
// (BuildKitShipSection).

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using GoF2Remake.World;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView
    {
        VisualElement kitList, kitPage;
        ProjectKit currentKit;
        ProjectKitPart currentPart;
        /// <summary>The tier the preview shows the selected part at (its extension models).</summary>
        int kitPreviewTier;

        static readonly string[] PartStats = { "armor", "cargo", "handling", "equipment", "topSpeed", "turnRate", "acceleration", "strafeSpeed" };
        static readonly string[] PartStatNames = { "Hull (%)", "Cargo (%)", "Handling (%)", "Equipment slots", "Top speed (%)", "Turn rate (%)", "Acceleration (%)", "Strafe speed (%)" };
        static readonly string[] PartSlotKeys = { "primary", "secondary", "turret" };

        void BuildKitsPanels(VisualElement left, VisualElement right)
        {
            kitList = ListPanel(left, T("mbKits", "Ship kits"));
            Act(left, T("mbNewKit", "+ New kit"), NewKit, "mbv-add");
            Act(left, T("mbRemoveKit", "Remove kit"), AskRemoveKit, "mbv-danger", "mbv-remove");
            kitPage = PagePanel(right);
        }

        void EnterKits()
        {
            if (currentKit != null && !Project.Kits.Contains(currentKit)) currentKit = null;
            currentKit ??= Project.Kits.FirstOrDefault();
            if (currentPart != null && currentKit?.Parts.Contains(currentPart) != true) currentPart = null;
            currentPart ??= currentKit?.Parts.FirstOrDefault();
            RebuildKits();
            BuildKitPage();
            ShowKitPreview();
        }

        void LeaveKits()
        {
            ModBuilder.PreviewBuild = null;
            ModBuilder.ApplyShip();
            boundShip = -2;
        }

        static string KitName(ProjectKit k) => string.IsNullOrEmpty(k.Name) ? k.Id : k.Name;

        void RebuildKits()
        {
            kitList.Clear();
            if (Project == null) return;
            foreach (var kit in Project.Kits)
            {
                var k = kit;
                ListRow(kitList, KitName(k), string.Format(T("mbKitSub", "{0} parts · {1} slots"), k.Parts.Count, k.Slots.Count), k == currentKit, () =>
                {
                    if (k == currentKit) return;
                    SaveNow();
                    currentKit = k;
                    currentPart = k.Parts.FirstOrDefault();
                    gizmo.Selected = -1;
                    placing = -1;
                    RebuildKits();
                    BuildKitPage();
                    ShowKitPreview();
                }, null, null);
            }
            if (Project.Kits.Count == 0)
                Note(kitList, T("mbNoKits", "No kits yet. A kit is a set of ship parts (.glb models: bodies, wings, engines...) that players combine into their own ship in the hangar's Customize screen."));
        }

        /// <summary>The project ship that flies a kit (its first), null = none yet.</summary>
        ProjectShip KitShipOf(ProjectKit k) =>
            k == null ? null : Project.Ships.FirstOrDefault(s => string.Equals(s.Kit, k.Id, StringComparison.OrdinalIgnoreCase)
                                                                 || string.Equals(s.Kit, Project.Key(k.Id), StringComparison.OrdinalIgnoreCase));

        /// <summary>The turntable: the kit's ship in its default build with the selected part (at the preview tier) in its
        /// slot.</summary>
        void ShowKitPreview()
        {
            var ship = KitShipOf(currentKit);
            if (ship == null) { ModBuilder.PreviewBuild = null; return; }
            var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in ship.BuildText().Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) != "tier") parts[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            int tier = ship.BuildTier;
            if (currentPart != null && !string.IsNullOrEmpty(currentPart.Slot))
            {
                parts[currentPart.Slot] = currentPart.Id;
                tier = Mathf.Min(kitPreviewTier, currentPart.Tiers.Count);
            }
            string text = string.Join(";", parts.Select(kv => kv.Key + "=" + kv.Value).Append("tier=" + tier));
            if (ModBuilder.Ship == ship && ModBuilder.PreviewBuild == text) return;
            ModBuilder.Ship = ship;
            Project.LastShip = ship.Ref;
            ModBuilder.PreviewBuild = text;
            ModBuilder.ApplyShip();
            boundShip = -2;
        }

        void NewKit()
        {
            AskText(T("mbNewKit", "+ New kit"), T("mbName", "Name"), T("mbNewKitName", "My ship kit"), name =>
            {
                var k = Project.AddKit(name);
                currentKit = k;
                currentPart = null;
                MarkDirty();
                RebuildKits();
                BuildKitPage();
                Status(T("mbKitAdded", "Now add its parts: each slot (body, wings, rear...) takes one part per build."), false);
            });
        }

        void AskRemoveKit()
        {
            var k = currentKit;
            if (k == null) return;
            Confirm(T("mbRemoveKit", "Remove kit"), string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), KitName(k)), () =>
            {
                Project.Kits.Remove(k);
                // Its part models go too when nothing else uses them.
                foreach (var p in k.Parts)
                    foreach (var f in p.Tiers.Prepend(p.Model)) DeleteUnusedModel(f);
                currentKit = Project.Kits.FirstOrDefault();
                currentPart = currentKit?.Parts.FirstOrDefault();
                RequestReload(false);
                RebuildKits();
                BuildKitPage();
            });
        }

        /// <summary>A model file of the project deleted when no ship or kit part uses it any more.</summary>
        void DeleteUnusedModel(string file)
        {
            if (string.IsNullOrEmpty(file) || GameModels.Is(file)) return;
            if (Project.Ships.Any(s => s.Model == file) || Project.Kits.Any(k => k.Parts.Any(p => p.Model == file || p.Tiers.Contains(file)))) return;
            try { System.IO.File.Delete(System.IO.Path.Combine(Project.Folder, file)); } catch { }
        }

        void KitEdited(bool reload, bool list = false)
        {
            MarkDirty();
            if (reload) RequestReload(true);
            if (list) RebuildKits();
            BuildKitPage();
            ShowKitPreview();
        }

        // ---- the page ----------------------------------------------------------------------------------------------

        void BuildKitPage()
        {
            if (kitPage == null) return;
            kitPage.Clear();
            var k = currentKit;
            if (k == null)
            {
                Note(kitPage, T("mbPickKit", "Add a kit on the left to start."));
                Note(kitPage, T("mbKitAbout", "Customizable ships are built from parts that share one origin: each slot (a body, wings, a rear...) takes one part, and the hangar's Customize screen lets players swap them, pick colours and skins. Every part brings its own hardpoints and stats."));
                return;
            }
            k.Flush();
            JsonButton(kitPage, k.Json, () =>
            {
                // The JSON was replaced in place: the parts read again.
                int i = Project.Kits.IndexOf(k);
                var fresh = new ProjectKit(k.Json);
                if (i >= 0) Project.Kits[i] = fresh;
                currentKit = fresh;
                currentPart = fresh.Find(currentPart?.Id) ?? fresh.Parts.FirstOrDefault();
                gizmo.Selected = -1;
                KitEdited(true, true);
            }, T("mbJsonKitNote", "shipkits.json's fields (Modding/README.md \"Customizable ships\"): also \"palettes\", \"colorSlots\", \"presets\" (colour schemes), \"skins\" (texture sets), \"hardpoints\" (weapon and turret models), \"materials\"."));

            Heading(kitPage, T("mbKit", "Kit"));
            Text(kitPage, T("mbName", "Name"), k.Name, 40, v => { k.Name = v.Trim(); KitEdited(false, true); });
            Note(kitPage, string.Format(T("mbKitIdNote", "Id: {0}. Its ships name it as \"kit\": \"{0}\"."), k.Id));
            var srow = Row(kitPage);
            Float(srow, T("mbKitScale", "Scale (m per model unit)"), k.Scale, 0.0001f, 10000f, v => { k.Scale = v; KitEdited(true); });
            Act(srow, "−10%", () => { k.Scale = Mathf.Max(0.0001f, k.Scale / 1.1f); KitEdited(true); }, "mbv-step");
            Act(srow, "+10%", () => { k.Scale = Mathf.Min(10000f, k.Scale * 1.1f); KitEdited(true); }, "mbv-step");
            var yrow = Row(kitPage);
            Add(yrow, new Label($"{T("mbYawAxis", "Turn (yaw)")}: {Mathf.RoundToInt(k.Yaw)}°"), "mbv-inline-label");
            Act(yrow, "−90°", () => { k.Yaw = Mathf.Repeat(k.Yaw - 90f, 360f); KitEdited(true); });
            Act(yrow, "+90°", () => { k.Yaw = Mathf.Repeat(k.Yaw + 90f, 360f); KitEdited(true); });
            Note(kitPage, T("mbKitScaleNote", "One scale and turn for every part: the parts are modelled around one origin, the ship's pivot. Turn them until the nose points along the arrow on the floor."));

            BuildKitShip(k);
            BuildKitSlots(k);
            BuildKitParts(k);
            if (currentPart != null) BuildPartPage(k, currentPart);
            BuildKitColours(k);
        }

        /// <summary>The ship that flies the kit (made here when there is none): the turntable shows it.</summary>
        void BuildKitShip(ProjectKit k)
        {
            Heading(kitPage, T("mbKitShip", "Ship"));
            var ship = KitShipOf(k);
            if (ship == null)
            {
                Note(kitPage, T("mbKitNoShip", "No ship flies this kit yet: make one to see the parts on the turntable. Its stats, price and default build are set in the Ships kind."));
                Act(kitPage, T("mbKitMakeShip", "+ Make a ship from this kit"), () => NewKitShip(k));
                return;
            }
            Note(kitPage, string.Format(T("mbKitShipNote", "The turntable shows {0} with the selected part. Its stats, price and default build: Ships kind."),
                                        string.IsNullOrEmpty(ship.Name) ? ship.LocalId : ship.Name));
            if (k.Parts.Count == 0) Note(kitPage, T("mbKitNoPartsYet", "The game skips a kit without parts: until the first one, the ship shows a stand-in."));
        }

        void NewKitShip(ProjectKit k)
        {
            AskText(T("mbKitMakeShip", "+ Make a ship from this kit"), T("mbName", "Name"), KitName(k), name =>
            {
                var s = Project.AddKitShip(Project.ShipIdFor(name), name, k, k.Id);
                Project.SetLoadout(s, DefaultLoadout(Database.Shared, s));
                ModBuilder.Ship = s;
                Project.LastShip = s.Ref;
                ModBuilder.PreviewBuild = null;
                RequestReload(false);
                RebuildShips();
                RebuildPages();
                BuildKitPage();
                ShowKitPreview();
            });
        }

        void BuildKitSlots(ProjectKit k)
        {
            Heading(kitPage, T("mbKitSlots", "Slots"));
            Note(kitPage, T("mbKitSlotsNote", "In assembly order: a build takes one part per slot (a slot whose parts don't fit a ship's type stays empty)."));
            var slots = k.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                int n = i;
                string slot = slots[i];
                var row = Row(kitPage);
                Add(row, new Label($"{slot}  ·  {string.Format(T("mbKitPartsN", "{0} parts"), k.PartsIn(slot).Count)}"), "mbv-inline-label");
                if (n > 0) Act(row, "↑", () => { var l = k.Slots; (l[n - 1], l[n]) = (l[n], l[n - 1]); k.Slots = l; KitEdited(true); }, "mbv-step");
                if (n < slots.Count - 1) Act(row, "↓", () => { var l = k.Slots; (l[n + 1], l[n]) = (l[n], l[n + 1]); k.Slots = l; KitEdited(true); }, "mbv-step");
                Act(row, T("mbRename", "Rename"), () => AskSlotName(T("mbRename", "Rename"), slot, v =>
                {
                    k.RenameSlot(slot, v);
                    foreach (var s in Project.Ships.Where(s => s.IsKitShip && s.BuildPart(slot) != null)) { s.SetBuildPart(v, s.BuildPart(slot)); s.SetBuildPart(slot, null); }
                    KitEdited(true);
                }), "mbv-step");
                Act(row, "✕", () => Confirm(T("mbKitRemoveSlot", "Remove slot"),
                    string.Format(T("mbKitRemoveSlotText", "Remove the slot \"{0}\" and its {1} parts?"), slot, k.PartsIn(slot).Count), () =>
                    {
                        var gone = k.PartsIn(slot);
                        k.RemoveSlot(slot);
                        foreach (var p in gone) foreach (var f in p.Tiers.Prepend(p.Model)) DeleteUnusedModel(f);
                        if (gone.Contains(currentPart)) currentPart = k.Parts.FirstOrDefault();
                        KitEdited(true, true);
                    }), "mbv-step");
            }
            Act(kitPage, T("mbKitAddSlot", "+ Add a slot"), () => AskSlotName(T("mbKitAddSlot", "+ Add a slot"), "", v =>
            {
                var l = k.Slots;
                if (!l.Contains(v, StringComparer.OrdinalIgnoreCase)) { l.Add(v); k.Slots = l; }
                KitEdited(false);
            }));
        }

        /// <summary>Asks for a slot's name: a-z 0-9 _ -, not one a build text uses itself.</summary>
        void AskSlotName(string title, string value, Action<string> ok) => AskText(title, T("mbKitSlotName", "Slot (a-z, 0-9, _)"), value, v =>
        {
            string id = ModProject.UniqueId(v, "slot", _ => false);
            if (ModShipKits.Build.Reserved(id)) { Status(string.Format(T("mbKitSlotReserved", "\"{0}\" can't be a slot's name."), id), true); return; }
            ok(id);
        }, 24);

        void BuildKitParts(ProjectKit k)
        {
            Heading(kitPage, T("mbKitParts", "Parts"));
            foreach (string slot in k.Slots)
            {
                string sl = slot;
                Caption(kitPage, slot);
                foreach (var part in k.PartsIn(slot))
                {
                    var p = part;
                    string sub = (p.Types.Count > 0 ? string.Join(", ", p.Types) : T("mbKitEveryType", "every type"))
                                 + (p.Tiers.Count > 0 ? "  ·  " + string.Format(T("mbKitTiersN", "{0} tiers"), p.Tiers.Count) : "")
                                 + (p.Mounts.Count > 0 ? "  ·  " + string.Format(T("mbKitPointsN", "{0} points"), p.Mounts.Count) : "");
                    ListRow(kitPage, string.IsNullOrEmpty(p.Name) ? p.Id : p.Name, sub, p == currentPart, () =>
                    {
                        currentPart = p;
                        kitPreviewTier = 0;
                        gizmo.Selected = -1;
                        placing = -1;
                        BuildKitPage();
                        ShowKitPreview();
                        gizmo.Bind(boundTransform, EditedMounts ?? new List<WeaponMount>());
                    }, null, null);
                }
                if (ModImport.Available)
                    Act(kitPage, string.Format(T("mbKitAddPart", "+ {0} part from .glb"), slot), () => AddPart(k, sl), "mbv-add");
            }
            if (k.Slots.Count == 0) Note(kitPage, T("mbKitNoSlots", "Add a slot first."));
        }

        void AddPart(ProjectKit k, string slot) => PickGlb(T("mbKitPickPart", "Choose a part model"), path =>
        {
            string name = Pretty(System.IO.Path.GetFileNameWithoutExtension(pickedName ?? slot));
            string id = k.PartIdFor(name);
            string model = Project.AddModel(path, $"{k.Id}_{id}");
            var p = k.AddPart(id, slot, name, model);
            currentPart = p;
            kitPreviewTier = 0;
            gizmo.Selected = -1;
            RequestReload(false);
            KitEdited(false, true);
            Status(T("mbLoadingModel", "Loading the model…"), false);
        });

        void BuildPartPage(ProjectKit k, ProjectKitPart p)
        {
            var box = Add(kitPage, new VisualElement(), "mbv-selected");
            Heading(box, string.Format(T("mbKitPart", "Part: {0}"), string.IsNullOrEmpty(p.Name) ? p.Id : p.Name));
            k.Flush();
            JsonButton(box, p.Json, () => { p.ReadMounts(); gizmo.Selected = -1; KitEdited(true, true); },
                       T("mbJsonPartNote", "A part's fields: also \"tierMounts\" (the tier extensions' hardpoints), \"anchors\" (points it offers other parts) and \"anchor\" (the point it sits on)."));
            Text(box, T("mbName", "Name"), p.Name, 40, v => { p.Name = v.Trim(); KitEdited(false); });
            Note(box, string.Format(T("mbKitPartIdNote", "Id: {0}. A ship's build names it in its slot."), p.Id));
            var slots = k.Slots;
            if (slots.Count > 1)
                Segments(box, T("mbKitPartSlot", "Slot"), slots.ToArray(), slots.FindIndex(s => string.Equals(s, p.Slot, StringComparison.OrdinalIgnoreCase)),
                         n => { p.Slot = slots[n]; KitEdited(true); });
            Text(box, T("mbKitPartTypes", "Fits ship types"), string.Join(", ", p.Types), 200, v =>
            {
                p.Types = v.Split(',').Select(t => ModProject.UniqueId(t, "", _ => false)).Where(t => t.Length > 0).Distinct().ToList();
                KitEdited(true);
            });
            Note(box, T("mbKitPartTypesNote", "Comma-separated ship types (a kit ship's \"Kit type\"); empty: every ship of the kit may fit it."));

            Caption(box, T("mbModel", "Model"));
            var mrow = Row(box);
            Add(mrow, new Label(p.Model ?? "?"), "mbv-inline-label");
            if (ModImport.Available)
                Act(mrow, T("mbReplaceModel", "Replace model…"), () => PickGlb(T("mbKitPickPart", "Choose a part model"), path =>
                {
                    string old = p.Model;
                    p.Model = Project.AddModel(path, $"{k.Id}_{p.Id}");
                    DeleteUnusedModel(old);
                    RequestReload(false);
                    KitEdited(false);
                }));

            Caption(box, T("mbKitTiers", "Tier extensions"));
            Note(box, T("mbKitTiersNote", "Extra models shown from build tier 1, 2, ... (armour plates, bigger engines): the Customize screen's tier."));
            var tiers = p.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                int n = i;
                var row = Row(box);
                Add(row, new Label($"{T("mbKitTier", "Tier")} {n + 1}: {System.IO.Path.GetFileName(tiers[n])}"), "mbv-inline-label");
                Act(row, "✕", () => { var l = p.Tiers; string f = l[n]; l.RemoveAt(n); p.Tiers = l; DeleteUnusedModel(f); KitEdited(true); }, "mbv-step");
            }
            var trow = Row(box);
            if (ModImport.Available)
                Act(trow, T("mbKitAddTier", "+ Tier model (.glb)"), () => PickGlb(T("mbKitPickTier", "Choose a tier model"), path =>
                {
                    var l = p.Tiers;
                    l.Add(Project.AddModel(path, $"{k.Id}_{p.Id}_t{l.Count + 1}"));
                    p.Tiers = l;
                    kitPreviewTier = l.Count;
                    RequestReload(false);
                    KitEdited(false);
                }));
            if (tiers.Count > 0)
            {
                Add(trow, new Label($"{T("mbKitPreviewTier", "Preview tier")}: {Mathf.Min(kitPreviewTier, tiers.Count)}"), "mbv-inline-label");
                Act(trow, "−", () => { kitPreviewTier = Mathf.Max(0, Mathf.Min(kitPreviewTier, tiers.Count) - 1); BuildKitPage(); ShowKitPreview(); }, "mbv-step");
                Act(trow, "+", () => { kitPreviewTier = Mathf.Min(tiers.Count, kitPreviewTier + 1); BuildKitPage(); ShowKitPreview(); }, "mbv-step");
            }

            Caption(box, T("mbKitPartStats", "Stats"));
            Note(box, T("mbKitPartStatsNote", "Added up over a build's parts: the hull, cargo and handling in % of the ship's own, equipment slots, and the flight bonuses (both flight styles)."));
            for (int i = 0; i < PartStats.Length; i++)
            {
                string key = PartStats[i];
                Int(box, T("mbKitStat_" + key, PartStatNames[i]), p.Stat(key), -1000, 1000, v => { p.SetStat(key, v); MarkDirty(); });
            }
            Caption(box, T("mbKitPartSlots", "Weapon slots it gives"));
            Note(box, T("mbKitPartSlotsNote", "-1 = none: the ship's own count for a type no part of the build gives."));
            string[] slotNames = { T("mbTypePrimary", "Primary"), T("mbTypeSecondary", "Secondary"), T("mbTypeTurret", "Turret") };
            var sr = Row(box, "mbv-xyz");
            for (int i = 0; i < PartSlotKeys.Length; i++)
            {
                string key = PartSlotKeys[i];
                Int(sr, slotNames[i], p.SlotCount(key), -1, 99, v => { p.SetSlotCount(key, v); MarkDirty(); });
            }

            BuildPartPoints(box, p);

            var del = Row(box);
            Act(del, T("mbKitRemovePart", "Remove part"), () => Confirm(T("mbKitRemovePart", "Remove part"),
                string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), string.IsNullOrEmpty(p.Name) ? p.Id : p.Name), () =>
                {
                    k.RemovePart(p);
                    foreach (var f in p.Tiers.Prepend(p.Model)) DeleteUnusedModel(f);
                    foreach (var s in Project.Ships.Where(s => s.IsKitShip && s.BuildPart(p.Slot) == p.Id)) s.SetBuildPart(p.Slot, null);
                    currentPart = k.Parts.FirstOrDefault();
                    gizmo.Selected = -1;
                    RequestReload(false);
                    KitEdited(false, true);
                }), "mbv-danger");
        }

        /// <summary>The part's hardpoints: placed on the hull like a ship's (the gizmo edits EditedMounts).</summary>
        void BuildPartPoints(VisualElement box, ProjectKitPart p)
        {
            Caption(box, T("mbPoints", "Hardpoints"));
            Note(box, T("mbKitPointsNote", "Ship-relative, on the hull as the turntable shows it. A build's points of each kind are its parts'; the ship's own only for a kind none of its parts has."));
            var addRow = Add(box, new VisualElement(), "mbv-row", "mbv-add-row");
            for (int t = 0; t < 4; t++)
            {
                int type = t;
                var b = Act(addRow, "+ " + T("mbType" + TypeNames[t], TypeNames[t]), () => { placing = placing == type ? -1 : type; BuildKitPage(); }, "mbv-type-button");
                b.style.borderLeftColor = ModBuilderGizmo.TypeColours[t];
                b.EnableInClassList("mbv-type-button--on", placing == type);
            }
            var counts = new int[5];
            for (int i = 0; i < p.Mounts.Count; i++)
            {
                int index = i;
                var m = p.Mounts[i];
                int type = Mathf.Clamp(m.slotType, 0, 4);
                counts[type]++;
                var pos = m.position_engine ?? new int[3];
                var row = new Button(() => { sound(false); gizmo.Selected = index; gizmo.Refresh(); BuildKitPage(); }) { focusable = false };
                row.AddToClassList("mbv-point-row");
                row.EnableInClassList("mbv-point-row--on", index == gizmo.Selected);
                var dot = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-dot");
                dot.style.backgroundColor = ModBuilderGizmo.TypeColours[Mathf.Min(type, 3)];
                string typeName = type < 4 ? T("mbType" + TypeNames[type], TypeNames[type]) : T("mbTypeJet", "Jet");
                Add(row, new Label($"{typeName} {counts[type]}") { pickingMode = PickingMode.Ignore }, "mbv-point-name");
                Add(row, new Label($"{pos[0]}, {pos[1]}, {pos[2]}") { pickingMode = PickingMode.Ignore }, "mbv-point-pos");
                box.Add(row);
            }
            if (p.Mounts.Count == 0) Note(box, T("mbNoPoints", "No hardpoints: pick a kind above, then click on the hull."));
            if (KitShipOf(currentKit) == null) Note(box, T("mbKitPointsNeedShip", "Make a ship from this kit to place them on the turntable."));
            BuildSelectedPoint(box);
        }

        /// <summary>The kit's paint slots: a colour per material name filter, from the kit's "paint" palette (players pick it in
        /// the Customize screen).</summary>
        void BuildKitColours(ProjectKit k)
        {
            Heading(kitPage, T("mbKitColours", "Colours"));
            Note(kitPage, T("mbKitColoursNote", "A paint slot recolours the materials whose name contains its text (the part models' own material names). Presets, other palettes and skins: Edit as JSON."));
            foreach (var (id, name, material) in k.ColorSlots)
            {
                string cid = id;
                var row = Row(kitPage);
                Add(row, new Label($"{name ?? id}: {(material ?? "?")}"), "mbv-inline-label");
                Act(row, "✕", () => { k.RemoveColorSlot(cid); KitEdited(true); }, "mbv-step");
            }
            var crow = Row(kitPage);
            Act(crow, T("mbKitAddPaint", "+ Paint slot"), () => AskColorSlot(k, false));
            Act(crow, T("mbKitAddLights", "+ Lights slot"), () => AskColorSlot(k, true));
        }

        void AskColorSlot(ProjectKit k, bool lights) =>
            AskText(lights ? T("mbKitAddLights", "+ Lights slot") : T("mbKitAddPaint", "+ Paint slot"),
                    T("mbKitMaterialFilter", "Material name contains"), lights ? "light" : "paint", material =>
            {
                string name = lights ? T("mbKitLightsName", "Lights") : string.Format(T("mbKitPaintName", "Paint ({0})"), material);
                string id = ModProject.UniqueId(material, "colour", c => k.ColorSlots.Any(s => s.id == c));
                k.AddColorSlot(id, name, material, lights);
                KitEdited(true);
            });

        // ---- a kit ship in the Ships kind -------------------------------------------------------------------------

        /// <summary>The Ship tab's kit section of a kit ship: the kit, its type, its default build (what the dealers sell and
        /// NPCs fly) and tier.</summary>
        void BuildKitShipSection(ProjectShip s)
        {
            Heading(ship, T("mbKitShipSection", "Customizable ship"));
            var own = Project.FindKit(s.Kit.Contains(':') && s.Kit.StartsWith(Project.Id + ":", StringComparison.OrdinalIgnoreCase) ? s.Kit.Substring(Project.Id.Length + 1) : s.Kit);
            var other = own == null ? ModShipKits.Find(s.Kit.Contains(':') ? s.Kit : null) : null;
            var row = Row(ship);
            Add(row, new Label($"{T("mbKit", "Kit")}: {(own != null ? KitName(own) : other != null ? $"{other.Name} ({other.mod.Id})" : s.Kit + " ?")}"), "mbv-inline-label");
            Act(row, T("mbChoose", "Choose…"), () => PickKitFor(s));
            if (own != null) Act(row, T("mbKitEdit", "Edit kit"), () => { currentKit = own; currentPart = own.Parts.FirstOrDefault(); SelectKind(ModBuilder.Kind.Kits); });
            Text(ship, T("mbKitType", "Kit type"), s.KitType, 32, v => { s.KitType = ModProject.UniqueId(v, own?.Id ?? "standard", _ => false); RequestReload(true); BuildShipPage(); });
            Note(ship, T("mbKitTypeNote", "Parts list the types that may fit them; the ship's type picks which."));

            Caption(ship, T("mbKitDefaultBuild", "Default build"));
            Note(ship, T("mbKitDefaultBuildNote", "What the dealers sell and other pilots fly; players change theirs in the hangar's Customize screen."));
            IEnumerable<(string slot, List<(string id, string name)> parts)> slots =
                own != null ? own.Slots.Select(sl => (sl, own.PartsIn(sl).Where(p => p.Types.Count == 0 || p.Types.Contains(s.KitType, StringComparer.OrdinalIgnoreCase))
                                                                .Select(p => (p.Id, string.IsNullOrEmpty(p.Name) ? p.Id : p.Name)).ToList()))
                : other != null ? other.slots.Select(sl => (sl, other.PartsFor(sl, s.KitType).Select(p => (p.id, p.Name)).ToList()))
                : Enumerable.Empty<(string, List<(string, string)>)>();
            foreach (var (slot, parts) in slots)
            {
                string sl = slot;
                var r = Row(ship);
                string picked = s.BuildPart(slot);
                string shown = parts.FirstOrDefault(p => string.Equals(p.id, picked, StringComparison.OrdinalIgnoreCase)).name
                               ?? (parts.Count > 0 ? parts[0].name + " " + T("mbKitFirst", "(first)") : T("mbKitNoneFits", "none fits"));
                Add(r, new Label($"{slot}: {shown}"), "mbv-inline-label");
                if (parts.Count > 1)
                    Act(r, T("mbChoose", "Choose…"), () => ShowPicker(slot, parts.Select(p => (p.name, (Texture2D)null, (Action)(() =>
                    {
                        s.SetBuildPart(sl, p.id);
                        RequestReload(true);
                        BuildShipPage();
                    }))).ToList()));
            }
            int maxTier = own?.MaxTier ?? other?.MaxTier ?? 0;
            if (maxTier > 0) Int(ship, T("mbKitBuildTier", "Tier"), s.BuildTier, 0, maxTier, v => { s.BuildTier = v; RequestReload(true); });
        }

        /// <summary>A picker of the kits a ship can fly: the project's and the other mods' that are on (its dependencies).</summary>
        void PickKitFor(ProjectShip s)
        {
            var choices = new List<(string, Texture2D, Action)>();
            foreach (var k in Project.Kits)
            {
                var kit = k;
                choices.Add((KitName(kit), null, () => { s.UseKit(kit.Id, string.IsNullOrEmpty(s.KitType) ? kit.Id : s.KitType); RequestReload(true); BuildShipPage(); }));
            }
            foreach (var k in ModShipKits.All().Where(k => k.mod != null && k.mod.Id != Project.Id))
            {
                string key = k.Key;
                choices.Add(($"{k.Name}\n{k.mod.Name}", null, () => { s.UseKit(key, string.IsNullOrEmpty(s.KitType) ? k.localId : s.KitType); RequestReload(true); BuildShipPage(); }));
            }
            if (choices.Count == 0) { Status(T("mbKitNoKits", "No kits yet: make one in Ship kits."), true); return; }
            ShowPicker(T("mbKitPick", "Fly a kit"), choices);
        }

        /// <summary>"+ New ship from a kit" in the Ships kind's list.</summary>
        void NewShipFromKit()
        {
            if (Project.Kits.Count == 0) { Status(T("mbKitNoKits", "No kits yet: make one in Ship kits."), true); return; }
            var choices = Project.Kits.Select(k => (KitName(k), (Texture2D)null, (Action)(() =>
            {
                var kit = k;
                AskText(T("mbNewShipKit", "+ New ship from a kit"), T("mbName", "Name"), KitName(kit), name =>
                {
                    var s = Project.AddKitShip(Project.ShipIdFor(name), name, kit, kit.Id);
                    Project.SetLoadout(s, DefaultLoadout(Database.Shared, s));
                    ModBuilder.Ship = s;
                    Project.LastShip = s.Ref;
                    gizmo.Selected = -1;
                    RequestReload(false);
                    RebuildShips();
                    RebuildPages();
                });
            }))).ToList();
            ShowPicker(T("mbNewShipKit", "+ New ship from a kit"), choices);
        }
    }
}
