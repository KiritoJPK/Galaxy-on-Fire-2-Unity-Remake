// ModBuilderView.cs
// Remake-only: the Mod Builder's screen over the hangar (Modding.ModBuilder), in place of the station menu (StationMenu
// hides its own and hands Update over). Built in code with Resources/GoF2ModBuilder/ModBuilder.uss:
//   top bar      the project, the kinds of content (Ships, Items, Characters; the rest greyed out, "Soon"), Save and Exit;
//                a kind swaps the two side panels (Items / Characters: ModBuilderViewItems / ModBuilderViewCharacters,
//                partial parts of this class); Items keeps the hangar, its turntable ship and Test fly; Characters is
//                edited in the station's Space Lounge (no ship, Test fly or Engines there); Skins (ModBuilderViewSkins)
//                puts the skinned game ship on the turntable and flies it;
//   left         the project's ships: new ones from a .glb model (the system's file picker, ModImport.Pick) or changes
//                to one of the game's ships (a picker of the originals), Remove;
//   right        the selected ship's tabs: Ship (name, description, race, armor, cargo, handling, price, equipment
//                slots, hangar height, model, length, turn), Hardpoints (add primary / secondary / turret / engine
//                points by clicking the hull, their list, the selected one's coordinates, mirror, delete, slot counts,
//                the turret's upside-down and the engine's flame size), Loadout (the test flight's weapons and
//                equipment per slot, Auto-fill, god mode and infinite ammo);
//   over the view the Engines slider (off at the left, the throttle, a boost at the right end: the glow and exhaust as in
//   flight);
//   bottom right TEST FLY: saves, lets the game read the project again and takes off into Dis' orbit (ModBuilder).
// The 3D view in between is the hangar with World.ModBuilderGizmo: a drag on a hardpoint or its arrows moves it, a drag
// elsewhere (or the right mouse button, one finger) orbits the camera (the hangar's Inspect ship orbit, HangarInspect),
// the wheel / a pinch / + - zoom, W A S D and the arrows turn, Delete removes the selected point, M mirrors it, Ctrl snaps
// to 10 units, Esc cancels. Changes are written at once; what changes the model (a new model, its size, the engine
// points, the hangar height) makes the game read the project again (ModBuilder.Reload) and the turntable ship is
// rebuilt when its model is ready. Plain C#.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    public partial class ModBuilderView : ModBuilderScreen
    {
        const float M = 0.05f;
        static readonly string[] TypeNames = { "Primary", "Secondary", "Turret", "Engine" };
        static readonly string[] TypeShort = { "P", "S", "T", "E" };
        static readonly int[] Races = { 0, 1, 2, 3, 8, 9 };
        static readonly string[] RaceNames = { "Terran", "Vossk", "Nivelian", "Midorian", "Pirate", "Void" };

        readonly StationLevel level;
        VisualElement viewport, labels;
        readonly VisualElement ship, hardpoints, loadout, shipList;
        readonly Label placeHint;
        readonly Button testFly;
        readonly Button[] tabs = new Button[3];
        readonly ModBuilderGizmo gizmo;
        int tab;
        ProjectShip Current => ModBuilder.Ship;

        // reloading the project (the model changes)
        bool launchPending;
        int boundShip = -2;
        Transform boundTransform;

        // placing a new hardpoint (Add): the next click on the hull
        int placing = -1;
        // the pointer over the 3D view
        enum Drag { None, Gizmo, Camera, Pan }
        Drag drag;
        Vector2 last, downAt;
        float travel, pinch = -1f;
        Vector2 fling;
        // The drag's turn not yet applied (1080p pixels): pointer moves come at the mouse's rate, not the frame's (0, 1 or 2
        // in a frame), so turning by each one as it came stepped unevenly and felt like dropped frames; the camera now
        // takes it frame by frame, eased over ~25 ms (OrbitEaseMs).
        Vector2 pendingTurn;
        const float OrbitEaseMs = 25f;

        protected override void BuildViewport(VisualElement root)
        {
            viewport = Add(root, new VisualElement { name = "mbvViewport" }, "mbv-viewport");
            viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            viewport.RegisterCallback<WheelEvent>(e => { OnWheel(e.delta.y); e.StopPropagation(); });
            labels = Add(root, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-labels");
        }

        protected override void AddTopButtons(VisualElement top)
        {
            TopButton(top, $"{T("mbHangar", "Hangar")}: {HangarTypeName(Session.StationIndex)}", PickHangar, "mbv-hangar-button");
            TopButton(top, T("mbPlay", "Play"), ShowPlayMenu, "mbv-play-button");
        }

        /// <summary>The play tests (ModBuilderPlay): the game as a player has it, with the project on: from the builder's
        /// station, from the campaign's start, or with one of its quests under way.</summary>
        void ShowPlayMenu()
        {
            void Run(Func<string> start)
            {
                SaveNow();
                ModBuilder.Reload();
                afterReload.Clear();
                string why = start();
                if (why != null) Status(why, true);
            }
            var choices = new List<(string, Texture2D, Action)>
            {
                (T("mbPlayHere", "Play from this station") + "\n" + T("mbPlayHereSub", "Dock, fly, trade: your systems, stations, ships and bar missions are in the game"), null,
                 () => Run(() => { ModBuilderPlay.Start(); return null; })),
            };
            if (Project.Campaign != null)
                choices.Add((T("mbPlayCampaign", "Play the campaign") + "\n" + T("mbPlayCampaignSub", "From its start: its station, ship and story quest"), null, () => Run(ModBuilderPlay.StartCampaign)));
            foreach (var q in Project.Quests)
            {
                var quest = q;
                choices.Add((string.Format(T("mbPlayQuestN", "Play \"{0}\""), q.Title ?? q.Name) + "\n" + T("mbPlayQuestSub", "With the quest under way from its first step"), null,
                             () => Run(() => ModBuilderPlay.StartQuest(quest.Name))));
            }
            ShowPicker(T("mbPlay", "Play"), choices);
        }

        public ModBuilderView(StationLevel level, VisualElement stationRoot, Action<bool> sound) : base(stationRoot, sound)
        {
            this.level = level;

            // ---- left: the ships (or the kind's list) ----
            var left = leftGroups[(int)ModBuilder.Kind.Ships];
            Add(left, new Label(T("mbShips", "Ships").ToUpperInvariant()), "mbv-heading", "gof-semibold");
            var shipScroll = Add(left, new ScrollView(ScrollViewMode.Vertical), "mbv-ship-scroll");
            shipScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            shipScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragScroll(shipScroll);
            shipList = shipScroll;
            Action importAction = ModImport.Available ? ImportModel : null;
            var newShip = Act(left, T("mbNewShip", "+ New ship from .glb"), () => importAction?.Invoke(), "mbv-add");
            newShip.SetEnabled(ModImport.Available);
            Act(left, T("mbNewShipGame", "+ New ship on a game model"), () => PickGameShip(T("mbNewShipGameTitle", "New ship on a game model"), NewShipOnGameModel), "mbv-add");
            Act(left, T("mbNewShipKit", "+ New ship from a kit"), NewShipFromKit, "mbv-add");
            Act(left, T("mbChangeShip", "+ Change a game ship"), PickOriginal, "mbv-add");
            Act(left, T("mbRemoveShip", "Remove ship"), AskRemove, "mbv-danger", "mbv-remove");

            // ---- right: the selected ship ----
            var right = rightGroups[(int)ModBuilder.Kind.Ships];
            var tabRow = Add(right, new VisualElement(), "mbv-tabs");
            string[] tabNames = { T("mbTabShip", "Ship"), T("mbTabHardpoints", "Hardpoints"), T("mbTabLoadout", "Loadout") };
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                tabs[i] = Act(tabRow, tabNames[i], () => SelectTab(n), "mbv-tab");
            }
            var scroll = Add(right, new ScrollView(ScrollViewMode.Vertical), "mbv-right-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            ship = Add(scroll, new VisualElement(), "mbv-page");
            hardpoints = Add(scroll, new VisualElement(), "mbv-page");
            loadout = Add(scroll, new VisualElement(), "mbv-page");
            BuildItemsPanels(leftGroups[(int)ModBuilder.Kind.Items], rightGroups[(int)ModBuilder.Kind.Items]);
            BuildCharactersPanels(leftGroups[(int)ModBuilder.Kind.Characters], rightGroups[(int)ModBuilder.Kind.Characters]);
            BuildSkinsPanels(leftGroups[(int)ModBuilder.Kind.Skins], rightGroups[(int)ModBuilder.Kind.Skins]);
            BuildBlueprintsPanels(leftGroups[(int)ModBuilder.Kind.Blueprints], rightGroups[(int)ModBuilder.Kind.Blueprints]);
            BuildKitsPanels(leftGroups[(int)ModBuilder.Kind.Kits], rightGroups[(int)ModBuilder.Kind.Kits]);
            BuildCampaignPanels(leftGroups[(int)ModBuilder.Kind.Campaigns], rightGroups[(int)ModBuilder.Kind.Campaigns]);
            BuildInteriorsPanels(leftGroups[(int)ModBuilder.Kind.Interiors], rightGroups[(int)ModBuilder.Kind.Interiors]);
            BuildQuestsPanels(leftGroups[(int)ModBuilder.Kind.Quests], rightGroups[(int)ModBuilder.Kind.Quests]);
            BuildAudioPanels(leftGroups[(int)ModBuilder.Kind.Audio], rightGroups[(int)ModBuilder.Kind.Audio]);
            BuildSystemsPanels(leftGroups[(int)ModBuilder.Kind.Systems], rightGroups[(int)ModBuilder.Kind.Systems]);

            // ---- bottom ----
            placeHint = Add(root, new Label { pickingMode = PickingMode.Ignore }, "mbv-place-hint");
            // The engines: the turntable ship's engine glow and exhaust as in flight, a slider from off through the throttle to
            // a boost at its right end.
            var engines = uiEngines = Add(root, new VisualElement(), "mbv-engines", "mbv-panel");
            var engineRow = Add(engines, new VisualElement(), "mbv-engines-row");
            Add(engineRow, new Label(T("mbEngines", "Engines").ToUpperInvariant()), "mbv-engines-title", "gof-semibold");
            engineValue = Add(engineRow, new Label(), "mbv-engines-value");
            engineSlider = Add(engines, new Slider(0f, 1f) { value = 0f, focusable = false }, "mbv-engines-slider");
            engineSlider.RegisterValueChangedCallback(e => { engineLevel = e.newValue; ApplyEngines(); });
            ApplyEngines();
            testFly = Add(root, new Button(StartTestFlight) { text = T("mbTestFly", "Test fly").ToUpperInvariant() }, "mbv-testfly", "gof-semibold");
            testFly.RegisterCallback<PointerDownEvent>(_ => sound(true), TrickleDown.TrickleDown);
            FinishLayout();
            // Over everything while the panels fly in or out: nothing takes a click meanwhile.
            uiBlocker = Add(root, new VisualElement { pickingMode = PickingMode.Position }, "mbv-blocker");
            uiBlocker.style.display = DisplayStyle.None;

            var go = new GameObject("Mod Builder gizmo");
            gizmo = go.AddComponent<ModBuilderGizmo>();
            gizmo.cam = level.MainCamera;
            gizmo.Moved += _ => OnMountMoved();

            // Back from a test flight: the flight's wear undone.
            if (ModBuilder.TestFlying) ModBuilder.OnDocked();
            SelectTab(0);
            RebuildShips();
            RebuildPages();
            SelectKind(ModBuilder.OpenKind);
            // The models of a project just opened may still be loading: the turntable shows the right one once they are.
            reloading = !ModShips.Ready;
            ShowProblems();
        }

        void OnWheel(float delta)
        {
            if (ModalOpen) return;
            if (kind == ModBuilder.Kind.Interiors) InteriorWheel(delta);
            else if (InHangar && ShipKind(kind)) level.Inspect.Zoom(Mathf.Pow(0.85f, -Mathf.Sign(delta)));
        }

        // ---- kinds of content ---------------------------------------------------------------------------------

        /// <summary>The station shows its hangar (the turntable ship, its orbit camera); the lounge kinds show the bar.</summary>
        bool InHangar => level.View == StationView.Hangar;

        /// <summary>The kind shows the hangar's turntable ship (and flies it).</summary>
        static bool ShipKind(ModBuilder.Kind k) => k == ModBuilder.Kind.Ships || k == ModBuilder.Kind.Items || k == ModBuilder.Kind.Skins
                                                 || k == ModBuilder.Kind.Blueprints || k == ModBuilder.Kind.Campaigns || k == ModBuilder.Kind.Kits;

        /// <summary>The kind edits hardpoints on the turntable ship: a ship's own, or a kit part's (ModBuilderViewKits).</summary>
        bool EditsMounts => kind == ModBuilder.Kind.Ships || kind == ModBuilder.Kind.Kits;

        /// <summary>The hardpoints the gizmo edits: the selected kit part's in the Kits kind, else the current ship's.</summary>
        List<WeaponMount> EditedMounts => kind == ModBuilder.Kind.Kits ? currentPart?.Mounts : Current?.Mounts;

        /// <summary>A ship's slot counts follow its hardpoints (a kit part's are its own "slots", set on its page).</summary>
        bool SlotsFollow => kind == ModBuilder.Kind.Ships && Current != null;

        /// <summary>The page listing the hardpoints shown again.</summary>
        void RefreshPoints()
        {
            if (kind == ModBuilder.Kind.Kits) BuildKitPage();
            else BuildHardpointsPage();
        }

        protected override bool EditsHere(ModBuilder.Kind k) => ModBuilder.PlaceOf(k) != ModBuilder.Place.Space;

        /// <summary>Shows a kind's panels; the hardpoint gizmo only with the ships; the lounge kinds in the Space Lounge, the
        /// systems on the star map, a room in itself.</summary>
        protected override void ShowKind(ModBuilder.Kind k, ModBuilder.Kind was)
        {
            bool ships = k == ModBuilder.Kind.Ships;
            placing = -1;
            if (gizmo.Selected >= 0) { gizmo.Selected = -1; gizmo.Refresh(); }
            gizmo.gameObject.SetActive(ships || k == ModBuilder.Kind.Kits);
            var place = ModBuilder.PlaceOf(k);
            bool bar = place == ModBuilder.Place.Lounge;
            if (place != ModBuilder.Place.Map && k != ModBuilder.Kind.AiTools) level.SetView(bar ? StationView.Lounge : StationView.Hangar);
            viewport.pickingMode = place == ModBuilder.Place.Map ? PickingMode.Ignore : PickingMode.Position;   // the map takes the pointer
            root.EnableInClassList("mbv-root--map", place == ModBuilder.Place.Map);
            drag = Drag.None;
            fling = Vector2.zero;
            // Test fly only where flying tests what is edited: a ship, an item fitted to it, a skin. Blueprints and the
            // campaign keep their ship on the turntable (and its engines) as a preview only.
            bool flies = Flies(k), turntable = ShipKind(k);
            testFly.style.display = flies ? StyleKeyword.Null : DisplayStyle.None;
            root.EnableInClassList("mbv-root--bar", !flies);   // no Test fly: the right panel reaches the bottom
            uiEngines.style.display = turntable ? StyleKeyword.Null : DisplayStyle.None;
            // Item changes reach the rest of the builder (the loadout's pickers) once the game has read them.
            if (was == ModBuilder.Kind.Items && itemsChanged) { itemsChanged = false; RequestReload(true); }
            if (was == ModBuilder.Kind.Systems && k != ModBuilder.Kind.Systems) CloseMap();
            if (was == ModBuilder.Kind.Interiors && k != ModBuilder.Kind.Interiors) EndInteriorView();
            if (k == ModBuilder.Kind.Items) { RebuildItems(); BuildItemPage(); }
            if (k == ModBuilder.Kind.Characters) { RebuildCharacters(); BuildCharacterPage(); }
            if (k == ModBuilder.Kind.Skins) { RebuildSkins(); BuildSkinPage(); }
            if (k == ModBuilder.Kind.Blueprints) { RebuildBlueprints(); BuildBlueprintPage(); }
            if (k == ModBuilder.Kind.Kits) EnterKits();
            else if (was == ModBuilder.Kind.Kits) LeaveKits();
            if (k == ModBuilder.Kind.Campaigns) { RebuildCampaign(); BuildCampaignPage(); }
            if (k == ModBuilder.Kind.Interiors) { RebuildInteriors(); BuildInteriorPage(); ShowInteriorInScene(); }
            if (k == ModBuilder.Kind.Quests) { RebuildQuests(); BuildQuestPage(); }
            else UpdateQuestGraph();
            if (k == ModBuilder.Kind.Audio) { RebuildAudio(); BuildAudioPage(); }
            if (k == ModBuilder.Kind.Systems) { RebuildSystems(); BuildSystemPage(); OpenMap(); }
            // The turntable swaps between the project's ship, the skinned game ship and the campaign's start ship.
            if (SwapsTurntable(was) != SwapsTurntable(k) && (int)was >= 0) { ModBuilder.ApplyShip(); boundShip = -2; }
            if (ships) { RebuildShips(); BuildHardpointsPage(); BuildLoadoutPage(); }
        }

        /// <summary>The kinds Test fly is offered in.</summary>
        static bool Flies(ModBuilder.Kind k) => k == ModBuilder.Kind.Ships || k == ModBuilder.Kind.Items || k == ModBuilder.Kind.Skins || k == ModBuilder.Kind.Kits;

        static bool SwapsTurntable(ModBuilder.Kind k) => k == ModBuilder.Kind.Skins || k == ModBuilder.Kind.Campaigns || k == ModBuilder.Kind.Blueprints;

        protected override bool CanReload => drag != Drag.Gizmo;
        protected override bool Dragging => drag != Drag.None;

        protected override void OnReloadStarted() => ModBuilder.ApplyShip();

        protected override void OnReloaded()
        {
            ModBuilder.ApplyShip();
            boundShip = -2;
            RebuildShips();
            if (kind == ModBuilder.Kind.Items) { RebuildItems(); BuildItemPage(); }
            if (kind == ModBuilder.Kind.Skins) { RebuildSkins(); BuildSkinPage(); }
            if (kind == ModBuilder.Kind.Blueprints) { RebuildBlueprints(); BuildBlueprintPage(); }
            if (kind == ModBuilder.Kind.Kits) { RebuildKits(); BuildKitPage(); }
            if (kind == ModBuilder.Kind.Campaigns) { RebuildCampaign(); BuildCampaignPage(); }
            if (kind == ModBuilder.Kind.Quests) { RebuildQuests(); BuildQuestPage(); }
            if (kind == ModBuilder.Kind.Audio) { RebuildAudio(); BuildAudioPage(); }
            if (kind == ModBuilder.Kind.Characters) { RebuildCharacters(); BuildCharacterPage(); }
            if (kind == ModBuilder.Kind.Systems) { RebuildSystems(); BuildSystemPage(); RefreshMap(); }
            if (kind == ModBuilder.Kind.Interiors) { RebuildInteriors(); BuildInteriorPage(); }
        }

        protected override Action BeforeUndo()
        {
            string shipRef = Current?.Ref, itemRef = currentItem?.Ref, characterRef = currentCharacter?.Ref, kitId = currentKit?.Id, partId = currentPart?.Id;
            var picks = RememberPicks();
            return () =>
            {
                ModBuilder.Ship = Project.Ships.FirstOrDefault(s => s.Ref == shipRef) ?? Project.Ships.FirstOrDefault();
                currentItem = Project.Items.FirstOrDefault(i => i.Ref == itemRef) ?? Project.Items.FirstOrDefault();
                currentCharacter = Project.Characters.FirstOrDefault(c => c.Ref == characterRef) ?? Project.Characters.FirstOrDefault();
                currentKit = Project.FindKit(kitId) ?? Project.Kits.FirstOrDefault();
                currentPart = currentKit?.Find(partId) ?? currentKit?.Parts.FirstOrDefault();
                picks?.Invoke();
                var skins = Project.SkinShips();
                if (!skins.Contains(ModBuilder.SkinShip)) ModBuilder.SkinShip = skins.Count > 0 ? skins[0] : -1;
                gizmo.Selected = -1;
                placing = -1;
                ModBuilder.ApplyShip();
                boundShip = -2;
                RebuildShips();
                RebuildPages();
                OnReloaded();
                gizmo.Refresh();
            };
        }

        // ---- pages ----------------------------------------------------------------------------------------------

        void SelectTab(int n)
        {
            tab = n;
            for (int i = 0; i < 3; i++) tabs[i].EnableInClassList("mbv-tab--on", i == n);
            ship.style.display = n == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            hardpoints.style.display = n == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            loadout.style.display = n == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            if (n != 1) placing = -1;
        }

        void RebuildShips()
        {
            shipList.Clear();
            if (Project == null) return;
            var db = Database.Shared;
            foreach (var s in Project.Ships)
            {
                var row = new Button(() => { sound(false); SelectShip(s); }) { focusable = false };
                row.AddToClassList("mbv-ship-row");
                row.EnableInClassList("mbv-ship-row--on", s == Current);
                int index = ModBuilder.IndexOf(s);
                var icon = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-ship-icon");
                var tex = index >= 0 ? ItemInfo.ShipIcon(index) : null;
                if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
                var text = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-ship-text");
                Add(text, new Label(ShipName(s, db)) { pickingMode = PickingMode.Ignore }, "mbv-ship-name");
                Add(text, new Label(s.IsOverride ? string.Format(T("mbChanges", "Changes {0}"), GameNames.Ship(s.OverrideIndex))
                                    : s.IsKitShip ? T("mbKitShipSub", "Customizable ship") : T("mbNewShipSub", "New ship"))
                    { pickingMode = PickingMode.Ignore }, "mbv-ship-sub");
                shipList.Add(row);
            }
            if (Project.Ships.Count == 0)
                Note(shipList, T("mbNoShips", "No ships yet. Add a new ship from a .glb model, or change one of the game's ships."));
        }

        static string ShipName(ProjectShip s, Database db) =>
            !string.IsNullOrEmpty(s.Name) ? s.Name : s.IsOverride ? GameNames.Ship(s.OverrideIndex) : s.LocalId;

        void SelectShip(ProjectShip s)
        {
            if (s == Current) return;
            SaveNow();
            ModBuilder.Ship = s;
            Project.LastShip = s?.Ref;
            gizmo.Selected = -1;
            placing = -1;
            ModBuilder.ApplyShip();
            boundShip = -2;   // the turntable swaps to it (Update)
            RebuildShips();
            RebuildPages();
            MarkDirty();
        }

        void RebuildPages()
        {
            BuildShipPage();
            BuildHardpointsPage();
            BuildLoadoutPage();
        }

        // Ship ------------------------------------------------------------------------------------------------------

        void BuildShipPage()
        {
            ship.Clear();
            var s = Current;
            if (s == null) { Note(ship, T("mbPickShip", "Add a ship on the left to start.")); return; }
            JsonButton(ship, s.Json, () => { s.ReadMounts(); SaveNow(); RequestReload(false); RebuildShips(); BuildShipPage(); BuildHardpointsPage(); },
                       T("mbJsonShipNote", "ships.json's fields: also materials (per mesh), throttleGlow / extraGlows, dealer, lounge, npcTurrets, priceDefault, engineGlowColor / Radius, available."));
            var db = Database.Shared;
            var original = s.IsOverride ? db.Ship(s.OverrideIndex) : null;
            Heading(ship, s.IsOverride ? string.Format(T("mbChanges", "Changes {0}"), GameNames.Ship(s.OverrideIndex)) : T("mbNewShipSub", "New ship"));
            var name = Text(ship, T("mbName", "Name"), s.Name, 40, v => { s.Name = v.Trim(); RebuildShips(); MarkDirty(); });
            if (s.IsOverride) name.textEdition.placeholder = GameNames.Ship(s.OverrideIndex);
            Text(ship, T("mbDescription", "Description"), s.Description, 600, v => { s.Description = v; MarkDirty(); }, true);
            if (!s.IsOverride)
            {
                Add(ship, new Label(T("mbRace", "Built by").ToUpperInvariant()), "mbv-label");
                var row = Add(ship, new VisualElement(), "mbv-seg-row");
                for (int i = 0; i < Races.Length; i++)
                {
                    int race = Races[i];
                    var b = Act(row, T("race" + RaceNames[i], RaceNames[i]), () => { s.Race = race; MarkDirty(); BuildShipPage(); }, "mbv-seg");
                    b.EnableInClassList("mbv-seg--on", s.Race == race);
                }
            }
            Heading(ship, T("mbStats", "Stats"));
            Int(ship, T("mbArmor", "Hull points"), s.Armor, 1, 100000, v => { s.Armor = v; MarkDirty(); });
            Int(ship, T("mbCargo", "Cargo (t)"), s.Cargo, 0, 100000, v => { s.Cargo = v; MarkDirty(); });
            Int(ship, T("mbHandling", "Handling"), s.Handling, 1, 400, v => { s.Handling = v; MarkDirty(); });
            Int(ship, T("mbPrice", "Price"), s.Price, 0, 999999999, v => { s.Price = v; MarkDirty(); });
            Int(ship, T("mbEquipmentSlots", "Equipment slots"), s.SlotCount(3), 0, 40, v => { s.SetSlotCount(3, v); MarkDirty(); BuildLoadoutPage(); });
            if (original != null)
                Note(ship, string.Format(T("mbOriginalStats", "Original: {0} hull, {1} t, handling {2}, {3}."), original.armor, original.cargo,
                                         Mathf.RoundToInt(original.handling), ItemInfo.Credits(original.price)));
            BuildShipMore(s);
            if (s.IsKitShip) { BuildKitShipSection(s); return; }   // its look is the kit's parts, its height over the pad measured

            Heading(ship, T("mbModel", "Model"));
            bool gameModel = s.HasGameModel;
            bool hasModel = !string.IsNullOrEmpty(s.Model) && !gameModel;
            int modelShip = gameModel && GameModels.Is(s.Model, out string modelName) ? GameModels.ShipOf(modelName) : -1;
            Note(ship, gameModel ? string.Format(T("mbGameModelNote", "The game's {0} model, as it is: it sits on the pads like that ship."),
                                                 modelShip >= 0 ? GameNames.Ship(modelShip) : s.Model)
                       : hasModel ? s.Model : T("mbOriginalModel", "The game's own model."));
            var mrow = Add(ship, new VisualElement(), "mbv-row");
            if (ModImport.Available)
                Act(mrow, hasModel ? T("mbReplaceModel", "Replace model…") : T("mbUseModel", "Use a .glb model…"), () => PickModel(s));
            Act(mrow, T("mbGameModel", "Game model…"), () => PickGameShip(T("mbGameModelTitle", "Use a game ship's model"), i => UseGameShipModel(s, i)));
            if (s.IsOverride && (hasModel || gameModel)) Act(mrow, T("mbOriginalModelButton", "Original model"), () =>
            {
                s.Model = null;
                if (gameModel)
                {
                    // Back to the ship's own model: its own hardpoints too.
                    s.Mounts.Clear();
                    s.Mounts.AddRange(Database.GameMounts(s.OverrideIndex).Select(ProjectShip.Copy));
                }
                RequestReload(true);
                BuildShipPage();
            });
            if (hasModel)
            {
                Int(ship, T("mbLength", "Length nose to tail (units, 20 = 1 m)"), Mathf.RoundToInt(s.ModelLength), 50, 200000,
                    v => ResizeModel(s, v));
                var srow = Add(ship, new VisualElement(), "mbv-row");
                Add(srow, new Label(T("mbScale", "Scale")), "mbv-inline-label");
                Act(srow, "−10%", () => ResizeModel(s, Mathf.Max(50f, Mathf.Round(s.ModelLength / 1.1f))));
                Act(srow, "+10%", () => ResizeModel(s, Mathf.Min(200000f, Mathf.Round(s.ModelLength * 1.1f))));
                // The model's orientation in its file: a turn about each axis in 90 deg steps (Unity Euler x / y / z).
                void TurnRow(string key, string english, Func<float> get, Action<float> set)
                {
                    var row = Add(ship, new VisualElement(), "mbv-row");
                    Add(row, new Label($"{T(key, english)}: {Mathf.RoundToInt(get())}°"), "mbv-inline-label");
                    Act(row, "−90°", () => { set(Mathf.Repeat(get() - 90f, 360f)); OnModelTurned(); });
                    Act(row, "+90°", () => { set(Mathf.Repeat(get() + 90f, 360f)); OnModelTurned(); });
                }
                TurnRow("mbYawAxis", "Turn (yaw)", () => s.ModelYaw, v => s.ModelYaw = v);
                TurnRow("mbPitchAxis", "Tip (pitch)", () => s.ModelPitch, v => s.ModelPitch = v);
                TurnRow("mbRollAxis", "Roll", () => s.ModelRoll, v => s.ModelRoll = v);
                Note(ship, T("mbYawNote", "Turn the model until its nose points along the arrow on the floor and its top faces up."));
                // The model's place on the ship's pivot (game units; the hardpoints stay where they are).
                Add(ship, new Label(T("mbOffset", "Position (units from the pivot)").ToUpperInvariant()), "mbv-label");
                var off = s.ModelOffset;
                var orow = Add(ship, new VisualElement(), "mbv-row", "mbv-xyz");
                Int(orow, "X", off.x, -200000, 200000, v => NudgeModel(s, 0, v - s.ModelOffset.x));
                Int(orow, "Y", off.y, -200000, 200000, v => NudgeModel(s, 1, v - s.ModelOffset.y));
                Int(orow, "Z", off.z, -200000, 200000, v => NudgeModel(s, 2, v - s.ModelOffset.z));
                var nrow = Add(ship, new VisualElement(), "mbv-row");
                int step = Mathf.Max(5, Mathf.RoundToInt(s.ModelLength * 0.02f));   // 2 % of the length per press
                for (int axis = 0; axis < 3; axis++)
                {
                    int a = axis;
                    Act(nrow, "XYZ"[a] + "−", () => NudgeModel(s, a, -step), "mbv-step");
                    Act(nrow, "XYZ"[a] + "+", () => NudgeModel(s, a, step), "mbv-step");
                }
                Act(nrow, T("mbCentreModel", "Centre on pivot"), () => CentreModel(s));
                BuildTextures(s);
            }
            if (!s.IsOverride && !gameModel)
            {
                var hrow = Add(ship, new VisualElement(), "mbv-row");
                Int(hrow, T("mbHangarHeight", "Height over the pad"), s.HangarHeight, 0, 20000, v => { s.HangarHeight = v; RequestReload(true); });
                Act(hrow, T("mbAuto", "Auto"), () => { AutoHangarHeight(); BuildShipPage(); });
            }
        }

        static readonly (string role, string english)[] TextureRoles =
        {
            ("diffuse", "Base colour"), ("normal", "Normal map"), ("metallicSmoothness", "Metallic / smoothness"),
        };

        /// <summary>Hull textures for a model whose look comes as separate PNGs (one material for the whole hull); a model
        /// with its own textures needs none.</summary>
        void BuildTextures(ProjectShip s)
        {
            Heading(ship, T("mbTextures", "Textures"));
            if (!s.HullTexturesEditable) { Note(ship, T("mbTexturesManual", "This ship's materials are set by hand in ships.json.")); return; }
            Note(ship, T("mbTexturesNote", "Only for a model without textures of its own (it shows white): image maps for the whole hull (PNG, JPG, TGA, BMP, DDS; stored as PNG)."));
            foreach (var (role, english) in TextureRoles)
            {
                string r = role;
                string current = s.HullTexture(role);
                var row = Add(ship, new VisualElement(), "mbv-row");
                Add(row, new Label($"{T("mbTex_" + role, english)}: {(current != null ? System.IO.Path.GetFileName(current) : T("mbNone", "none"))}"), "mbv-inline-label");
                Act(row, T("mbChoose", "Choose…"), () => PickImage(T("mbPickTexture", "Choose a texture"), path =>
                    {
                        string id = s.IsOverride ? "ship_" + s.OverrideIndex : s.LocalId;
                        s.SetHullTexture(r, Project.AddTexture(path, $"{id}_{r}"));
                        RequestReload(false);
                        BuildShipPage();
                    }));
                if (current != null) Act(row, "✕", () => { s.SetHullTexture(r, null); RequestReload(false); BuildShipPage(); }, "mbv-step");
            }
        }

        /// <summary>The model was turned: rebuilt, and its height over the pad measured again once it is (its bottom moved).</summary>
        void OnModelTurned() => OnModelMoved();

        bool autoHeightAfterReload;

        /// <summary>The model was moved or resized: rebuilt, its height over the pad measured again.</summary>
        void OnModelMoved()
        {
            autoHeightAfterReload = Current != null && !Current.IsOverride;
            RequestReload(true);
            BuildShipPage();
        }

        /// <summary>The model moved 'units' along an axis; its hardpoints go with it.</summary>
        void NudgeModel(ProjectShip s, int axis, int units)
        {
            if (units == 0) return;
            var d = Vector3Int.zero;
            d[axis] = units;
            MoveModel(s, d);
        }

        void MoveModel(ProjectShip s, Vector3Int delta)
        {
            s.ModelOffset += delta;
            foreach (var m in s.Mounts)
            {
                if (m.position_engine == null || m.position_engine.Length < 3) continue;
                for (int i = 0; i < 3; i++) m.position_engine[i] += delta[i];
            }
            gizmo.Refresh();
            OnModelMoved();
            BuildHardpointsPage();
        }

        /// <summary>The model sized to 'length' (nose to tail); its hardpoints scale with it about the model's origin.</summary>
        void ResizeModel(ProjectShip s, float length)
        {
            float k = length / Mathf.Max(1f, s.ModelLength);
            var o = s.ModelOffset;
            s.ModelLength = length;
            foreach (var m in s.Mounts)
            {
                if (m.position_engine == null || m.position_engine.Length < 3) continue;
                for (int i = 0; i < 3; i++) m.position_engine[i] = o[i] + Mathf.RoundToInt((m.position_engine[i] - o[i]) * k);
                if (m.glowSize != null) for (int i = 0; i < m.glowSize.Length; i++) m.glowSize[i] = Mathf.Round(m.glowSize[i] * k);
            }
            gizmo.Refresh();
            OnModelMoved();
            BuildHardpointsPage();
        }

        /// <summary>The hull's box centred on the ship's pivot (from the model as it is built now).</summary>
        void CentreModel(ProjectShip s)
        {
            if (boundTransform == null) return;
            var c = gizmo.LocalBounds.center;   // Unity ship frame, with the current offset in it
            MoveModel(s, new Vector3Int(Mathf.RoundToInt(c.x / M), -Mathf.RoundToInt(c.y / M), -Mathf.RoundToInt(c.z / M)));
        }

        // Hardpoints ------------------------------------------------------------------------------------------------

        void BuildHardpointsPage()
        {
            hardpoints.Clear();
            var s = Current;
            if (s == null) return;
            Heading(hardpoints, T("mbAddPoint", "Add a hardpoint"));
            var addRow = Add(hardpoints, new VisualElement(), "mbv-row", "mbv-add-row");
            for (int t = 0; t < 4; t++)
            {
                int type = t;
                var b = Act(addRow, "+ " + T("mbType" + TypeNames[t], TypeNames[t]), () => { placing = placing == type ? -1 : type; BuildHardpointsPage(); }, "mbv-type-button");
                b.style.borderLeftColor = ModBuilderGizmo.TypeColours[t];
                b.EnableInClassList("mbv-type-button--on", placing == type);
            }
            Note(hardpoints, T("mbPointsHelp", "Drag a point or its arrows to move it (Ctrl snaps). Drag elsewhere to turn the view, wheel to zoom. Delete removes, M mirrors."));

            for (int t = 0; t < 3; t++)
            {
                int type = t;
                var row = Add(hardpoints, new VisualElement(), "mbv-row", "mbv-slot-row");
                Add(row, new Label(string.Format(T("mbSlotsLine", "{0} slots: {1}  ·  {2} points"), T("mbType" + TypeNames[t], TypeNames[t]), s.SlotCount(t), s.MountCount(t))), "mbv-inline-label");
                Act(row, "−", () => { s.SetSlotCount(type, s.SlotCount(type) - 1); MarkDirty(); BuildHardpointsPage(); BuildLoadoutPage(); }, "mbv-step");
                Act(row, "+", () => { s.SetSlotCount(type, s.SlotCount(type) + 1); MarkDirty(); BuildHardpointsPage(); BuildLoadoutPage(); }, "mbv-step");
            }

            Heading(hardpoints, T("mbPoints", "Hardpoints"));
            var counts = new int[4];
            for (int i = 0; i < s.Mounts.Count; i++)
            {
                int index = i;
                var m = s.Mounts[i];
                int type = Mathf.Clamp(m.slotType, 0, 3);
                counts[type]++;
                var p = m.position_engine ?? new int[3];
                var row = new Button(() => { sound(false); gizmo.Selected = index; gizmo.Refresh(); BuildHardpointsPage(); }) { focusable = false };
                row.AddToClassList("mbv-point-row");
                row.EnableInClassList("mbv-point-row--on", index == gizmo.Selected);
                var dot = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-dot");
                dot.style.backgroundColor = ModBuilderGizmo.TypeColours[type];
                Add(row, new Label($"{T("mbType" + TypeNames[type], TypeNames[type])} {counts[type]}") { pickingMode = PickingMode.Ignore }, "mbv-point-name");
                Add(row, new Label($"{p[0]}, {p[1]}, {p[2]}") { pickingMode = PickingMode.Ignore }, "mbv-point-pos");
                hardpoints.Add(row);
            }
            if (s.Mounts.Count == 0) Note(hardpoints, T("mbNoPoints", "No hardpoints: pick a kind above, then click on the hull."));
            if (s.IsKitShip) Note(hardpoints, T("mbKitShipPoints", "A customizable ship's hardpoints come from its kit's parts (Ship kits). These are only for a kind no part of a build has."));
            BuildSelectedPoint();
        }

        IntegerField px, py, pz;

        void BuildSelectedPoint(VisualElement into = null)
        {
            var s = EditedMounts;
            int i = gizmo.Selected;
            if (s == null || i < 0 || i >= s.Count) { px = null; return; }
            var m = s[i];
            m.position_engine ??= new int[3];
            int type = Mathf.Clamp(m.slotType, 0, 3);
            var selectedBox = Add(into ?? hardpoints, new VisualElement(), "mbv-selected");
            Heading(selectedBox, string.Format(T("mbSelectedPoint", "Selected: {0}"), T("mbType" + TypeNames[type], TypeNames[type])));
            var xyz = Add(selectedBox, new VisualElement(), "mbv-row", "mbv-xyz");
            px = Int(xyz, "X", m.position_engine[0], -200000, 200000, v => { m.position_engine[0] = v; OnMountEdited(type); });
            py = Int(xyz, "Y", m.position_engine[1], -200000, 200000, v => { m.position_engine[1] = v; OnMountEdited(type); });
            pz = Int(xyz, "Z", m.position_engine[2], -200000, 200000, v => { m.position_engine[2] = v; OnMountEdited(type); });
            Note(selectedBox, T("mbXyzNote", "Game units from the ship's centre: X toward the pilot's left, Y up, Z forward (20 units = 1 m)."));
            var acts = Add(selectedBox, new VisualElement(), "mbv-row");
            Act(acts, T("mbMirror", "Mirror"), Mirror);
            Act(acts, T("mbCentre", "Centre X"), () => { m.position_engine[0] = 0; OnMountEdited(type); RefreshPoints(); });
            Act(acts, T("mbDelete", "Delete"), DeleteSelected, "mbv-danger");
            if (type == 2)
            {
                // The way the turret's base faces (WeaponMount.turretUp): a preset, or the hull's surface at the point.
                Add(selectedBox, new Label(T("mbTurretFaces", "Turret faces")), "mbv-inline-label");
                var faces = Add(selectedBox, new VisualElement(), "mbv-row", "mbv-add-row");
                var up = m.TurretUpLocal;
                foreach (var (key, label, dir) in TurretFacings)
                {
                    var d = dir;
                    var b = Act(faces, T(key, label), () => { SetTurretUp(m, d); OnMountEdited(2); RefreshPoints(); }, "mbv-type-button");
                    b.EnableInClassList("mbv-type-button--on", Vector3.Dot(up, new Vector3(-d.x, d.y, d.z)) > 0.999f);
                }
                Act(selectedBox, T("mbTurretMatchHull", "Match the hull here"), () =>
                {
                    if (gizmo.HullNormalAt(i, out var n)) { m.SetTurretUp(gizmo.GameDirOf(n), true); OnMountEdited(2); RefreshPoints(); }
                    else Status(T("mbTurretNoHull", "No hull found at that point."), true);
                });
                var g = new Vector3(-up.x, up.y, up.z);
                Note(selectedBox, string.Format(T("mbTurretFacesNote", "Facing {0:0.##}, {1:0.##}, {2:0.##} (X left, Y up, Z forward). Placing or dragging it over the hull turns it to the surface."), g.x, g.y, g.z));
            }
            if (type == 3)
            {
                float r = m.glowSize != null && m.glowSize.Length >= 1 ? m.glowSize[0] : 24f;
                float len = m.glowSize != null && m.glowSize.Length >= 3 ? m.glowSize[2] : r * 0.87f;
                float particles = m.turretAngles != null && m.turretAngles.Length > 0 ? m.turretAngles[0] : 0.55f;
                Int(selectedBox, T("mbFlameSize", "Flame radius (units)"), Mathf.RoundToInt(r), 1, 5000, v =>
                {
                    float l = m.glowSize != null && m.glowSize.Length >= 3 ? m.glowSize[2] : v * 0.87f;
                    m.glowSize = new[] { (float)v, v, l };
                    OnMountEdited(3);
                });
                Int(selectedBox, T("mbFlameLength", "Flame length (units)"), Mathf.RoundToInt(len), 0, 10000, v =>
                {
                    float rr = m.glowSize != null && m.glowSize.Length >= 1 ? m.glowSize[0] : 24f;
                    m.glowSize = new[] { rr, rr, (float)v };
                    OnMountEdited(3);
                });
                Int(selectedBox, T("mbParticles", "Exhaust particles (%)"), Mathf.RoundToInt(particles * 100f), 0, 1000, v =>
                {
                    float f = v / 100f;
                    m.turretAngles = new[] { f, f, f };
                    MarkDirty();
                });
                if (kind == ModBuilder.Kind.Ships && Current.IsOverride && string.IsNullOrEmpty(Current.Model))
                    Note(selectedBox, T("mbEngineOriginal", "With the game's own model the flame is part of the model: only the exhaust particles move."));
            }
        }

        void RefreshSelectedFields()
        {
            var s = EditedMounts;
            int i = gizmo.Selected;
            if (s == null || i < 0 || i >= s.Count || px == null) return;
            var p = s[i].position_engine;
            px.SetValueWithoutNotify(p[0]);
            py.SetValueWithoutNotify(p[1]);
            pz.SetValueWithoutNotify(p[2]);
        }

        // Loadout ---------------------------------------------------------------------------------------------------

        void BuildLoadoutPage()
        {
            loadout.Clear();
            var s = Current;
            if (s == null) return;
            var db = Database.Shared;
            Heading(loadout, T("mbTestFlight", "Test flight"));
            var flags = Add(loadout, new VisualElement(), "mbv-row");
            var god = Act(flags, ModBuilder.TestGodMode ? T("mbGodOn", "God mode: on") : T("mbGodOff", "God mode: off"),
                          () => { ModBuilder.TestGodMode = !ModBuilder.TestGodMode; MarkDirty(); BuildLoadoutPage(); });
            god.EnableInClassList("mbv-toggle--on", ModBuilder.TestGodMode);
            var ammo = Act(flags, ModBuilder.TestInfiniteAmmo ? T("mbAmmoOn", "Infinite ammo: on") : T("mbAmmoOff", "Infinite ammo: off"),
                           () => { ModBuilder.TestInfiniteAmmo = !ModBuilder.TestInfiniteAmmo; MarkDirty(); BuildLoadoutPage(); });
            ammo.EnableInClassList("mbv-toggle--on", ModBuilder.TestInfiniteAmmo);
            Act(loadout, T("mbAutoFill", "Auto-fill every slot"), () =>
            {
                Project.SetLoadout(s, DefaultLoadout(db, s));
                MarkDirty();
                ModBuilder.ApplyShip();
                BuildLoadoutPage();
            });
            var slots = SlotsOf(db, s);
            for (int kind = 0; kind < 4; kind++)
            {
                if (s.SlotCount(kind) <= 0) continue;
                Heading(loadout, T("mbKind" + kind, kind == 0 ? "Primary weapons" : kind == 1 ? "Secondary weapons" : kind == 2 ? "Turrets" : "Equipment"));
                for (int i = 0; i < s.SlotCount(kind); i++)
                {
                    int k = kind, slot = i;
                    int item = slot < slots[k].Count ? slots[k][slot] : -1;
                    var row = new Button(() => { sound(false); PickItem(k, slot); }) { focusable = false };
                    row.AddToClassList("mbv-item-row");
                    var icon = Add(row, new VisualElement { pickingMode = PickingMode.Ignore }, "mbv-item-icon");
                    if (item >= 0) { var tex = ItemInfo.ItemIcon(item); if (tex != null) icon.style.backgroundImage = new StyleBackground(tex); }
                    Add(row, new Label(item >= 0 ? GameNames.Item(item) : T("mbEmptySlot", "— empty —")) { pickingMode = PickingMode.Ignore }, "mbv-item-name");
                    loadout.Add(row);
                }
            }
        }

        /// <summary>The project's loadout of 's' sorted into its slots by kind (item numbers).</summary>
        List<int>[] SlotsOf(Database db, ProjectShip s)
        {
            var slots = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            foreach (var st in Project.Loadout(s) ?? new List<ItemStack>())
            {
                var it = db.Item(st.item);
                if (it == null || it.TypeId < 0 || it.TypeId > 3) continue;
                slots[it.TypeId].Add(st.item);
            }
            return slots;
        }

        void SetSlot(int kind, int slot, int item)
        {
            var db = Database.Shared;
            var s = Current;
            var slots = SlotsOf(db, s);
            while (slots[kind].Count <= slot) slots[kind].Add(-1);
            slots[kind][slot] = item;
            var list = new List<ItemStack>();
            for (int k = 0; k < 4; k++)
                for (int i = 0; i < Mathf.Min(slots[k].Count, s.SlotCount(k)); i++)
                    if (slots[k][i] >= 0) list.Add(new ItemStack(slots[k][i], k == 1 ? ModBuilder.Ammo : 1));
            Project.SetLoadout(s, list);
            MarkDirty();
            ModBuilder.ApplyShip();
            BuildLoadoutPage();
        }

        /// <summary>ModBuilder.DefaultLoadout for the ship as it is being edited (its slot counts, which the game reads only
        /// after the project is saved and read again).</summary>
        static List<ItemStack> DefaultLoadout(Database db, ProjectShip s)
        {
            var l = new List<ItemStack>();
            ItemData Best(int kind, params string[] categories)
            {
                var items = ModBuilder.ItemsFor(db, kind);
                foreach (var c in categories)
                {
                    var b = items.Where(i => i.category == c).OrderByDescending(i => i.maxPrice).FirstOrDefault();
                    if (b != null) return b;
                }
                return items.OrderByDescending(i => i.maxPrice).FirstOrDefault();
            }
            var p = Best(0, "Laser", "Blaster");
            for (int i = 0; i < s.SlotCount(0) && p != null; i++) l.Add(new ItemStack(p.index, 1));
            var sec = Best(1, "Missile");
            for (int i = 0; i < s.SlotCount(1) && sec != null; i++) l.Add(new ItemStack(sec.index, ModBuilder.Ammo));
            var tur = Best(2, "Turret");
            for (int i = 0; i < s.SlotCount(2) && tur != null; i++) l.Add(new ItemStack(tur.index, 1));
            int eq = 0;
            foreach (var cat in new[] { "Shield", "Armor", "Booster", "Scanner", "Tractor beam", "Steering nozzle" })
            {
                if (eq >= s.SlotCount(3)) break;
                var e = ModBuilder.ItemsFor(db, 3).Where(i => i.category == cat).OrderByDescending(i => i.maxPrice).FirstOrDefault();
                if (e != null) { l.Add(new ItemStack(e.index, 1)); eq++; }
            }
            return l;
        }

        void OnMountEdited(int type)
        {
            gizmo.Refresh();
            MarkDirty();
            if (type == 3 && (kind == ModBuilder.Kind.Kits || !string.IsNullOrEmpty(Current?.Model))) RequestReload(true);   // the flame is built into the model
            if (type == 2) RequestReload(true);   // the turntable's turrets sit on the mounts as the game reads them
        }

        /// <summary>The turret facings offered (game frame: x toward the pilot's left, y up, z forward).</summary>
        static readonly (string key, string label, Vector3 dir)[] TurretFacings =
        {
            ("mbFaceUp", "Up", Vector3.up), ("mbFaceDown", "Down", Vector3.down), ("mbFaceLeft", "Left", Vector3.right),
            ("mbFaceRight", "Right", Vector3.left), ("mbFaceFront", "Front", Vector3.forward), ("mbFaceBack", "Back", Vector3.back),
        };

        static void SetTurretUp(WeaponMount m, Vector3 game) => m.SetTurretUp(game);

        void OnMountMoved() => RefreshSelectedFields();

        /// <summary>A new point where the hull was clicked; a turret faces out of the hull there ('normal', world).</summary>
        void AddMount(int type, Vector3 world, Vector3 normal)
        {
            var s = EditedMounts;
            var g = gizmo.GameOf(world);
            var m = new WeaponMount { slotType = type, position_engine = new[] { g.x, g.y, g.z } };
            if (type == 2) m.SetTurretUp(gizmo.GameDirOf(normal), true);
            if (type == 3)
            {
                float r = Mathf.Clamp(Mathf.Round(Mathf.Min(gizmo.LocalBounds.size.x, gizmo.LocalBounds.size.y) / M * 0.05f), 8f, 200f);
                m.glowSize = new[] { r, r, r * 0.87f };
                m.turretAngles = new[] { 0.55f, 0.55f, 0.55f };
            }
            s.Add(m);
            if (type < 3 && SlotsFollow && Current.SlotCount(type) < Current.MountCount(type)) Current.SetSlotCount(type, Current.MountCount(type));
            gizmo.Selected = s.Count - 1;
            gizmo.Refresh();
            OnMountEdited(type);
            RefreshPoints();
            BuildLoadoutPage();
        }

        void DeleteSelected()
        {
            var s = EditedMounts;
            int i = gizmo.Selected;
            if (s == null || i < 0 || i >= s.Count) return;
            int type = s[i].slotType;
            int before = s.Count(x => x.slotType == type);
            s.RemoveAt(i);
            if (type < 3 && SlotsFollow && Current.SlotCount(type) == before) Current.SetSlotCount(type, before - 1);   // the slots followed the points
            gizmo.Selected = -1;
            gizmo.Refresh();
            OnMountEdited(type);
            RefreshPoints();
            BuildLoadoutPage();
        }

        /// <summary>A copy of the selected point on the other side (x mirrored); a point on the centre line has none.</summary>
        void Mirror()
        {
            var s = EditedMounts;
            int i = gizmo.Selected;
            if (s == null || i < 0 || i >= s.Count) return;
            var m = s[i];
            if (m.position_engine[0] == 0) { Status(T("mbMirrorCentre", "The point is on the centre line: move it sideways first."), true); return; }
            var c = ProjectShip.Copy(m);
            c.position_engine[0] = -c.position_engine[0];
            if (c.turretUp != null && c.turretUp.Length >= 3) c.turretUp[0] = -c.turretUp[0];   // a side turret faces the other side
            s.Insert(i + 1, c);
            if (c.slotType < 3 && SlotsFollow && Current.SlotCount(c.slotType) < Current.MountCount(c.slotType)) Current.SetSlotCount(c.slotType, Current.MountCount(c.slotType));
            gizmo.Selected = i + 1;
            gizmo.Refresh();
            OnMountEdited(c.slotType);
            RefreshPoints();
            BuildLoadoutPage();
        }

        /// <summary>The pivot's height over the pad from the hull's lowest point.</summary>
        void AutoHangarHeight()
        {
            var s = Current;
            if (s == null || s.IsOverride || s.HasGameModel || boundTransform == null) return;   // a game model sits like its ship
            s.HangarHeight = Mathf.Max(0, Mathf.CeilToInt(-gizmo.LocalBounds.min.y / M) + 4);
            RequestReload(true);
        }

        /// <summary>A new ship's first hardpoints from its model's box: two guns at the front, a secondary under it, an engine
        /// at the back; and its height over the pad.</summary>
        void AutoLayout(ProjectShip s)
        {
            s.NeedsLayout = false;
            var b = gizmo.LocalBounds;
            int G(float unity) => Mathf.RoundToInt(unity / M);
            int w = G(b.extents.x * 0.45f), cy = G(b.center.y), front = G(b.max.z - b.size.z * 0.2f), back = G(b.min.z), under = G(b.min.y + b.size.y * 0.3f);
            s.Mounts.Clear();
            s.Mounts.Add(new WeaponMount { slotType = 0, position_engine = new[] { -w, cy, front } });
            s.Mounts.Add(new WeaponMount { slotType = 0, position_engine = new[] { w, cy, front } });
            s.Mounts.Add(new WeaponMount { slotType = 1, position_engine = new[] { 0, under, G(b.center.z) } });
            float r = Mathf.Clamp(Mathf.Round(Mathf.Min(b.size.x, b.size.y) / M * 0.05f), 8f, 200f);
            s.Mounts.Add(new WeaponMount { slotType = 3, position_engine = new[] { 0, cy, back }, glowSize = new[] { r, r, r * 0.87f }, turretAngles = new[] { 0.55f, 0.55f, 0.55f } });
            s.SetSlotCount(0, 2);
            s.SetSlotCount(1, 1);
            s.HangarHeight = Mathf.Max(0, Mathf.CeilToInt(-b.min.y / M) + 4);
            Project.SetLoadout(s, DefaultLoadout(Database.Shared, s));
            RequestReload(false);
            RebuildPages();
            Status(T("mbLayoutDone", "Placed a first set of hardpoints. Move them onto the hull in Hardpoints."), false);
        }

        // ---- ships ---------------------------------------------------------------------------------------------------

        void ImportModel() => PickGlb(T("mbPickModel", "Choose a ship model"), path =>
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(pickedName ?? "ship");
            string id = Project.ShipIdFor(name);
            string model = Project.AddModel(path, id);
            var s = Project.AddNewShip(id, Pretty(name) == "New" ? T("mbNewShipName", "New ship") : Pretty(name), model);
            ModBuilder.Ship = s;
            Project.LastShip = s.Ref;
            gizmo.Selected = -1;
            RequestReload(false);
            RebuildShips();
            RebuildPages();
            Status(T("mbLoadingModel", "Loading the model…"), false);
        });

        void PickModel(ProjectShip s) => PickGlb(T("mbPickModel", "Choose a ship model"), path =>
        {
            string baseName = s.IsOverride ? "ship_" + s.OverrideIndex : s.LocalId;
            s.Model = Project.AddModel(path, baseName);
            if (s.IsOverride && s.ModelLength <= 0f) s.ModelLength = 1000f;
            RequestReload(false);
            BuildShipPage();
            Status(T("mbLoadingModel", "Loading the model…"), false);
        });

        /// <summary>A picker of the game's flyable ships (with their icons) for a game model.</summary>
        void PickGameShip(string title, Action<int> pick)
        {
            var db = Database.Shared;
            var choices = new List<(string name, Texture2D icon, Action pick)>();
            for (int i = 0; i < GameNames.OriginalShips; i++)
            {
                if (i == 13 || i == 14 || i == 15 || db.Ship(i) == null || db.ShipAssembly(i, original: true) == null) continue;
                int index = i;
                choices.Add((GameNames.Ship(i), ItemInfo.ShipIcon(i), () => pick(index)));
            }
            ShowPicker(title, choices);
        }

        /// <summary>A new ship on game ship 'index''s model (GameModels): its stats, slots, race and hardpoints to start from,
        /// its icon unless one is set; nothing to import.</summary>
        void NewShipOnGameModel(int index)
        {
            var db = Database.Shared;
            string name = string.Format(T("mbCustomShip", "Custom {0}"), GameNames.Ship(index));
            string id = Project.ShipIdFor(name);
            var asm = db.ShipAssembly(index, original: true);
            var s = Project.AddNewShip(id, name, GameModels.Ref(asm.name));
            s.UseGameModel(asm.name, db.Ship(index), Database.GameMounts(index), Shop.RaceOfShip(index));
            Project.SetLoadout(s, ModBuilder.DefaultLoadout(db, index));
            ModBuilder.Ship = s;
            Project.LastShip = s.Ref;
            gizmo.Selected = -1;
            RequestReload(false);
            RebuildShips();
            RebuildPages();
            Status(string.Format(T("mbShipOnGameModel", "A new ship on the {0}'s model: give it a name and its own stats."), GameNames.Ship(index)), false);
        }

        /// <summary>The ship flies game ship 'index''s model instead (its hardpoints come along; the stats stay).</summary>
        void UseGameShipModel(ProjectShip s, int index)
        {
            var db = Database.Shared;
            var asm = db.ShipAssembly(index, original: true);
            if (asm == null) return;
            string old = s.HasGameModel ? null : s.Model;
            s.UseGameModel(asm.name, null, Database.GameMounts(index));
            if (!string.IsNullOrEmpty(old) && !Project.Ships.Any(o => o != s && o.Model == old))
                try { System.IO.File.Delete(System.IO.Path.Combine(Project.Folder, old)); } catch { }
            gizmo.Selected = -1;
            RequestReload(false);
            RebuildPages();
            Status(string.Format(T("mbShipModelSet", "{0}'s model, with its hardpoints."), GameNames.Ship(index)), false);
        }

        /// <summary>A picker of the game's ships for "Change a game ship" (not those already in the project).</summary>
        void PickOriginal()
        {
            var db = Database.Shared;
            var taken = new HashSet<int>(Project.Ships.Where(s => s.IsOverride).Select(s => s.OverrideIndex));
            var choices = new List<(string name, Texture2D icon, Action pick)>();
            for (int i = 0; i < GameNames.OriginalShips; i++)
            {
                if (i == 13 || i == 14 || i == 15 || taken.Contains(i) || db.Ship(i) == null || db.ShipAssembly(i) == null) continue;
                int index = i;
                choices.Add((GameNames.Ship(i), ItemInfo.ShipIcon(i), () =>
                {
                    var s = Project.AddOverride(db, index);
                    ModBuilder.Ship = s;
                    Project.LastShip = s.Ref;
                    gizmo.Selected = -1;
                    Project.SetLoadout(s, ModBuilder.DefaultLoadout(db, index));
                    RequestReload(false);
                    RebuildShips();
                    RebuildPages();
                }));
            }
            ShowPicker(T("mbChangeShipTitle", "Change a game ship"), choices);
        }

        /// <summary>The kinds of hangar by StationTables.HangarIndex (null: none).</summary>
        static readonly string[] RoomNames = { "Terran", "Vossk", "Nivelian", "Midorian", null, null, null, "Deep science", "Battlestation" };

        /// <summary>The kind of hangar a station has ("Vossk"), for the top bar's Hangar button.</summary>
        static string HangarTypeName(int station)
        {
            var db = Database.Shared;
            var st = db.Stations.ElementAtOrDefault(station);
            if (st == null) return "?";
            int room = StationTables.HangarIndex(station, db.Systems.ElementAtOrDefault(st.system)?.raceId ?? 0);
            return room >= 0 && room < RoomNames.Length && RoomNames[room] != null ? T("mbRoom" + room, RoomNames[room]) : "?";
        }

        /// <summary>One station per kind of hangar (StationTables.HangarIndex): picking one moves the builder there, and its
        /// orbit is the test flights'. Dis (Terran) and Var Hastra (Midorian) where they can, else the first that can host.</summary>
        /// <summary>The hangar kinds the builder can sit in: (room, its English name, the station that has it); 'current' the
        /// room the builder is in now (-1 none).</summary>
        static List<(int room, string name, int station)> HangarChoices(out int current)
        {
            var db = Database.Shared;
            var rooms = RoomNames;
            int[] preferred = { 70, -1, -1, 78 };
            int RoomOf(int i) => StationTables.HangarIndex(i, db.Systems.ElementAtOrDefault(db.Stations[i].system)?.raceId ?? 0);
            var hosts = Enumerable.Range(0, db.Stations.Count).Where(i => ModBuilder.CanHost(i, db)).ToList();
            current = hosts.Contains(Session.StationIndex) ? RoomOf(Session.StationIndex) : -1;
            var list = new List<(int, string, int)>();
            for (int room = 0; room < rooms.Length; room++)
            {
                if (rooms[room] == null) continue;
                int r = room;
                int station = room < preferred.Length && preferred[room] >= 0 && hosts.Contains(preferred[room]) && RoomOf(preferred[room]) == room
                    ? preferred[room] : hosts.Where(i => RoomOf(i) == r).DefaultIfEmpty(-1).First();
                if (station >= 0) list.Add((room, rooms[room], station));
            }
            return list;
        }

        void PickHangar()
        {
            var db = Database.Shared;
            var choices = new List<(string, Texture2D, Action)>();
            foreach (var (room, name, station) in HangarChoices(out int current))
            {
                var st = db.Stations[station];
                int race = db.Systems.ElementAtOrDefault(st.system)?.raceId ?? 0;
                string label = $"{T("mbRoom" + room, name)}\n{st.name}" + (room == current ? " ✓" : "");
                var emblem = Resources.Load<Texture2D>("GoF2Hud/race_" + (race >= 0 && race < 4 ? race : 0));
                int r = room, s = station;
                choices.Add((label, emblem, () =>
                {
                    if (r == current) return;
                    SaveNow();
                    ModBuilder.SwitchStation(s);
                }));
            }
            ShowPicker(T("mbHangarTitle", "Build and test-fly at"), choices);
        }

        void AskRemove()
        {
            var s = Current;
            if (s == null) return;
            Confirm(T("mbRemoveShip", "Remove ship"), string.Format(T("mbRemoveShipText", "Remove \"{0}\" from the project?"), ShipName(s, Database.Shared)), () =>
            {
                Project.Ships.Remove(s);
                // Its model file goes too when no other ship uses it.
                if (!string.IsNullOrEmpty(s.Model) && !s.HasGameModel && !Project.Ships.Any(o => o.Model == s.Model))
                    try { System.IO.File.Delete(System.IO.Path.Combine(Project.Folder, s.Model)); } catch { }
                ModBuilder.Ship = Project.Ships.FirstOrDefault();
                Project.LastShip = ModBuilder.Ship?.Ref;
                gizmo.Selected = -1;
                RequestReload(false);
                RebuildShips();
                RebuildPages();
            });
        }

        void PickItem(int kind, int slot)
        {
            var db = Database.Shared;
            var choices = new List<(string, Texture2D, Action)> { (T("mbEmptySlot", "— empty —"), null, () => SetSlot(kind, slot, -1)) };
            foreach (var it in ModBuilder.ItemsFor(db, kind))
            {
                int index = it.index;
                choices.Add((GameNames.Item(index), ItemInfo.ItemIcon(index), () => SetSlot(kind, slot, index)));
            }
            ShowPicker(T("mbChooseItem", "Choose"), choices);
        }

        // ---- test flight --------------------------------------------------------------------------------------

        void StartTestFlight()
        {
            if (!Flies(kind)) return;
            bool skin = kind == ModBuilder.Kind.Skins && ModBuilder.SkinShip >= 0;
            if (Current == null && !skin) { Status(T("mbNoShipToFly", "Add a ship first."), true); return; }
            if (!InHangar) return;
            if (launchPending || level.PlayerFlying) return;
            sound(false);
            launchPending = true;
            RequestReload(false);   // the game flies what the files say now
            Status(T("mbPreparing", "Preparing the test flight…"), false);
        }

        void Launch()
        {
            launchPending = false;
            if (ModManager.BuilderProject == null || ModManager.BuilderProject.Broken) { ShowProblems(); return; }
            ModBuilder.StartTestFlight();
            if (level.PlayerShip == null || boundShip != Session.ShipIndex) { level.ReplacePlayerShip(Session.ShipIndex); boundShip = -2; }
            engineLevel = 0f;   // the take-off lights them itself (and moves, so no stationary plume)
            engineSlider.SetValueWithoutNotify(0f);
            ApplyEngines();
            gizmo.Selected = -1;
            gizmo.Bind(null, null);
            boundTransform = null;
            placing = -1;
            level.Launch();
        }

        // ---- per frame ---------------------------------------------------------------------------------------

        public void Update()
        {
            // Exit pressed: the project is closed and the main menu is loading; nothing here is rebuilt any more.
            if (!ModBuilder.Active) return;
            float dt = Time.unscaledDeltaTime * 1000f;
            bool flying = level.PlayerFlying;
            UpdateUiAnimation(flying);
            if (flying || uiShown < 1f)
            {
                nowMs += dt;
                UpdateLayoutClasses();
                if (!flying) return;   // landed, the panels still flying in: no input until they are in place
                if (SpaceLevel.PlayerTriedToFly()) level.SkipPlayerFlight();
                labels.Clear();
                status.style.display = placeHint.style.display = DisplayStyle.None;   // inline styles beat the hiding rule
                return;
            }
            if (!UpdateCommon(dt)) return;
            if (!reloading && !reloadPending && boundShip != Session.ShipIndex) BindShip();
            else if (boundShip >= 0 && level.PlayerShip != null && level.PlayerShip != boundTransform) RebindShip();
            if (launchPending && !reloading && !reloadPending) Launch();
            placeHint.text = placing >= 0 ? string.Format(T("mbPlaceHint", "Click on the hull to place the new {0} point.  Esc cancels."),
                                                          T("mbType" + TypeNames[placing], TypeNames[placing]).ToLowerInvariant())
                           : PlaceHintOfKind();
            placeHint.style.display = placeHint.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ReadKeys(dt);
            UpdateCamera(dt);
            UpdateLabels();
            UpdateKind(dt);
        }

        // The panels fly out as the ship takes off and back in once it has landed, like the station menu
        // (StationMenu.UpdateUiAnimation: 450 ms, ease-out cubic, run backwards going away): the top bar up, the ships panel
        // left, the ship panel, Test fly and the engines to their edges, each fading, one after another (the last one first
        // going away). The state at the first frame is taken as it is: back from a test flight the ship flies in with them away.
        const float UiAnimMs = 450f, UiPartMs = 300f;
        float uiShown = 1f;   // 0 away .. 1 in place
        bool uiStateTaken;
        VisualElement uiEngines, uiBlocker;

        void UpdateUiAnimation(bool flying)
        {
            float target = flying ? 0f : 1f;
            if (!uiStateTaken) { uiStateTaken = true; uiShown = target; ApplyUiAnimation(); return; }
            if (uiShown == target) return;
            uiShown = Mathf.MoveTowards(uiShown, target, Time.unscaledDeltaTime * 1000f / UiAnimMs);
            ApplyUiAnimation();
        }

        void ApplyUiAnimation()
        {
            bool away = uiShown <= 0f, still = uiShown >= 1f;
            root.EnableInClassList("mbv-root--flying", away);
            uiBlocker.style.display = still ? DisplayStyle.None : DisplayStyle.Flex;
            // Each part, its delay and where it goes (panel px).
            var parts = new (VisualElement el, float delay, Vector2 off)[]
            {
                (uiTop, 0f, new Vector2(0f, -90f)),
                (uiLeft, 50f, new Vector2(-160f, 0f)),
                (uiRight, 100f, new Vector2(200f, 0f)),
                (uiEngines, 125f, new Vector2(0f, -60f)),
                (testFly, 150f, new Vector2(220f, 0f)),
            };
            float ms = Mathf.Clamp01(uiShown) * UiAnimMs;
            foreach (var (el, delay, off) in parts)
            {
                if (el == null) continue;
                if (still) { el.style.translate = StyleKeyword.Null; el.style.opacity = StyleKeyword.Null; continue; }
                float p = Mathf.Clamp01((ms - delay) / UiPartMs);
                float r = (1f - p) * (1f - p) * (1f - p);   // 1 - easeOutCubic: the share of the way still to go
                el.style.translate = new Translate(off.x * r, off.y * r);
                el.style.opacity = 1f - r;
            }
        }

        float engineLevel;
        readonly Slider engineSlider;
        readonly Label engineValue;
        /// <summary>The slider's right end past this is the boost (the throttle is full there).</summary>
        const float BoostFrom = 0.9f;

        void ApplyEngines()
        {
            bool on = engineLevel > 0.001f;
            float throttle = Mathf.Clamp01(engineLevel / BoostFrom);
            float boost = Mathf.Clamp01((engineLevel - BoostFrom) / (1f - BoostFrom));
            engineValue.text = !on ? T("mbEnginesOff", "Off").ToUpperInvariant()
                             : boost > 0f ? T("mbEnginesBoost", "Boost").ToUpperInvariant()
                             : $"{Mathf.RoundToInt(throttle * 100f)} %";
            engineValue.EnableInClassList("mbv-engines-value--boost", boost > 0f);
            var asm = boundTransform != null ? boundTransform.GetComponent<Visuals.AssembledObject>() : null;
            if (asm != null) asm.SetExhaust(on, HangarFlight.UsesPlayerEngine(asm));
            // The exhaust particles follow the glow but come per distance flown: standing on the pad they need the
            // stationary rate.
            if (boundTransform != null)
                foreach (var e in boundTransform.GetComponentsInChildren<Flight.ShipExhaust>(true))
                {
                    e.stationary = on ? throttle : 0f;
                    e.stationaryBoost = boost;
                }
        }

        /// <summary>The turntable shows the current ship: rebuilt, the gizmo on it, the camera orbiting it.</summary>
        void BindShip()
        {
            int index = Session.ShipIndex;
            level.ReplacePlayerShip(index);
            boundShip = index;
            boundTransform = level.PlayerShip;
            if (boundTransform == null)
            {
                gizmo.Bind(null, null);
                Status(T("mbNoPreview", "The ship couldn't be built for the preview. Check its model, or reopen the project."), true);
                ShowProblems();
                return;
            }
            var s = Current;
            gizmo.Bind(boundTransform, EditedMounts ?? new List<WeaponMount>());
            if (InHangar && ShipKind(kind)) level.BeginInspect(0.55f);
            ApplyEngines();   // the rebuilt ship comes with its engines off
            // A new ship's first hardpoints (just imported, or never given any).
            // Not on a game model: it came with that ship's own hardpoints (or, a model that is no ship's, none on purpose).
            if (s != null && !s.IsOverride && !s.HasGameModel && !s.IsKitShip && (s.NeedsLayout || s.Mounts.Count == 0) && ModBuilder.IndexOf(s) == index) AutoLayout(s);
            else if (autoHeightAfterReload && s != null && ModBuilder.IndexOf(s) == index)
            {
                autoHeightAfterReload = false;
                int before = s.HangarHeight;
                AutoHangarHeight();
                if (s.HangarHeight == before) reloadPending = false;   // nothing moved: no second rebuild
                BuildShipPage();
            }
            RefreshPoints();
            MaybeMakeIcon(s, index);
        }

        /// <summary>The station rebuilt the turntable ship by itself (a customizable ship's parts arrived, ModShips.ModelsChanged):
        /// the gizmo and the engines on the new one.</summary>
        void RebindShip()
        {
            boundTransform = level.PlayerShip;
            gizmo.Bind(boundTransform, EditedMounts ?? new List<WeaponMount>());
            ApplyEngines();
            MaybeMakeIcon(Current, Session.ShipIndex);
        }

        /// <summary>A new ship's shop icon, rendered from its model once (ShipIconMaker) into icons/&lt;id&gt;.png: when it has
        /// none, or the builder's own no longer matches its look (another model file, a turn, other hull textures). An icon
        /// the modder set by hand is left alone.</summary>
        void MaybeMakeIcon(ProjectShip s, int index)
        {
            if (s == null || s.IsOverride || string.IsNullOrEmpty(s.Model) && !s.IsKitShip || s.HasGameModel || ModBuilder.IndexOf(s) != index) return;   // a game model has the game's icon
            if (s.IsKitShip && kind == ModBuilder.Kind.Kits) return;   // a preview build, not the default the icon shows
            var made = Project.GeneratedIcon(s);
            if (!string.IsNullOrEmpty(s.Icon) && (made == null || made.Value.file != s.Icon)) return;   // the modder's own
            string key = IconKey(s);
            if (made != null && made.Value.key == key && System.IO.File.Exists(System.IO.Path.Combine(Project.Folder, made.Value.file))) return;
            var db = Database.Shared;
            var entry = db.ShipAssembly(index);
            if (entry == null || !ModShips.Built.Any(b => b.ship == index)) return;   // not built: the Phantom would stand in
            var icon = ShipIconMaker.Make(ModShips.Template(entry.name));
            if (icon == null) return;
            ModBuilder.History?.Commit();   // the edits so far are their own step: the icon joins it, not a step of its own
            Project.SetGeneratedIcon(s, icon.EncodeToPNG(), key);
            Project.Save();
            ModBuilder.History?.Absorb();
            ModShips.SetIcon(index, icon);
            RebuildShips();
        }

        /// <summary>What the icon is drawn from: the model file, its turn and the hull textures.</summary>
        string IconKey(ProjectShip s)
        {
            if (s.IsKitShip)
            {
                // A kit ship: its default build, the kit's parts and their files.
                var kit = Project.FindKit(s.Kit);
                var sb = new System.Text.StringBuilder("kit|" + s.Kit + "|" + s.KitType + "|" + s.BuildText());
                if (kit != null)
                {
                    kit.Flush();
                    sb.Append('|').Append(kit.Scale).Append('|').Append(kit.Yaw);
                    foreach (var p in kit.Parts)
                    {
                        var fi = new System.IO.FileInfo(System.IO.Path.Combine(Project.Folder, p.Model ?? ""));
                        sb.Append('|').Append(p.Id).Append('@').Append(fi.Exists ? fi.Length + ":" + fi.LastWriteTimeUtc.Ticks : "missing");
                    }
                    sb.Append('|').Append(kit.Json["colorSlots"]?.ToString(Newtonsoft.Json.Formatting.None));
                }
                return sb.ToString();
            }
            var file = new System.IO.FileInfo(System.IO.Path.Combine(Project.Folder, s.Model));
            string stamp = file.Exists ? file.Length + "@" + file.LastWriteTimeUtc.Ticks : "missing";
            var materials = s.ToJson()["materials"];
            return $"1|{s.Model}|{stamp}|{s.ModelYaw}|{s.ModelPitch}|{s.ModelRoll}|{materials?.ToString(Newtonsoft.Json.Formatting.None)}";
        }

        void ReadKeys(float dt)
        {
            if (ReadCommonKeys(EscapeInKind)) return;
            var kb = Keyboard.current;
            if (EditsMounts && (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) DeleteSelected();
            if (EditsMounts && kb.mKey.wasPressedThisFrame) Mirror();
            if (!InHangar || !ShipKind(kind)) return;   // the Space Lounge, the map, a room: no ship to fly or turn around
            if (kb.lKey.wasPressedThisFrame && Flies(kind)) StartTestFlight();
            float frames = dt / (1000f / 30f);
            float left = kb.leftArrowKey.isPressed || kb.aKey.isPressed ? 1f : 0f, right = kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 1f : 0f;
            float up = kb.upArrowKey.isPressed || kb.wKey.isPressed ? 1f : 0f, dn = kb.downArrowKey.isPressed || kb.sKey.isPressed ? 1f : 0f;
            var d = new Vector2(left - right, up - dn) * 4f * frames;
            if (d != Vector2.zero) level.Inspect.Turn(d.x * 0.005f, d.y * 0.005f);
            float zoom = (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed ? 1f : 0f) - (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed ? 1f : 0f);
            if (zoom != 0f) level.Inspect.Zoom(Mathf.Pow(1.03f, zoom * frames));
        }

        /// <summary>Esc in the screen: a hardpoint being placed or selected, then the kind's own (a marker being placed).</summary>
        bool EscapeInKind()
        {
            if (placing >= 0) { placing = -1; BuildHardpointsPage(); return true; }
            if (gizmo.Selected >= 0) { gizmo.Selected = -1; gizmo.Refresh(); BuildHardpointsPage(); return true; }
            return EscapeKind();
        }

        void UpdateCamera(float dt)
        {
            if (!InHangar || !ShipKind(kind)) return;
            if (!level.Inspect.Active && boundTransform != null && !level.PlayerFlying) level.BeginInspect(0.55f);
            var ts = Touchscreen.current;
            int down = 0;
            if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) down++;
            if (down >= 2)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero; int n = 0;
                foreach (var t in ts.touches) if (t.press.isPressed) { if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue(); n++; }
                float span = (a - b).magnitude;
                if (pinch > 1f && span > 1f) level.Inspect.Zoom(pinch / span);
                pinch = span;
                if (drag == Drag.Camera) drag = Drag.None;
                return;
            }
            pinch = -1f;
            if (pendingTurn != Vector2.zero)
            {
                var step = pendingTurn * (1f - Mathf.Exp(-dt / OrbitEaseMs));
                if (pendingTurn.sqrMagnitude < 0.01f) step = pendingTurn;
                pendingTurn -= step;
                level.Inspect.Turn(step.x * 0.005f, step.y * 0.005f);
            }
            if (drag == Drag.None && fling != Vector2.zero)
            {
                float frames = dt / (1000f / 30f);
                level.Inspect.Turn(fling.x * frames * 0.005f, fling.y * frames * 0.005f);
                fling *= Mathf.Pow(0.9f, frames);
                if (fling.magnitude <= 0.5f) fling = Vector2.zero;
            }
            if (drag == Drag.None && Pointer.current != null && !ModalOpen && EditsMounts) gizmo.Hover(Pointer.current.position.ReadValue());
        }

        /// <summary>A short tag by every hardpoint ("P1", "E2"...), where it shows on screen.</summary>
        void UpdateLabels()
        {
            var s = EditedMounts;
            var cam = level.MainCamera;
            int n = s != null && boundTransform != null && cam != null && EditsMounts ? s.Count : 0;
            while (labels.childCount < n) Add(labels, new Label { pickingMode = PickingMode.Ignore }, "mbv-point-tag");
            while (labels.childCount > n) labels.RemoveAt(labels.childCount - 1);
            if (n == 0) return;
            float w = root.layout.width, h = root.layout.height;
            if (float.IsNaN(w) || w <= 0f) return;
            var counts = new int[4];
            for (int i = 0; i < n; i++)
            {
                var m = s[i];
                int type = Mathf.Clamp(m.slotType, 0, 3);
                counts[type]++;
                var tag = (Label)labels[i];
                var sp = cam.WorldToScreenPoint(gizmo.WorldOf(m));
                if (sp.z <= 0f) { tag.style.display = DisplayStyle.None; continue; }
                tag.style.display = DisplayStyle.Flex;
                tag.text = TypeShort[type] + counts[type];
                // translate, not left / top: moving a tag every frame of an orbit then needs no layout pass
                tag.style.translate = new Translate(sp.x / Screen.width * w + 10f, (Screen.height - sp.y) / Screen.height * h - 24f);
                tag.style.color = ModBuilderGizmo.TypeColours[type];
                tag.EnableInClassList("mbv-point-tag--on", i == gizmo.Selected);
            }
        }

        // ---- the pointer over the 3D view -----------------------------------------------------------------------

        static Vector2 ScreenPos() => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        void OnPointerDown(PointerDownEvent e)
        {
            if (ModalOpen) return;
            if (KindPointerDown(e)) return;   // the kinds with a view of their own (a room)
            if (!InHangar || !ShipKind(kind)) return;
            // While the preview rebuilds (reloading) the camera still turns and pans; only the hardpoints wait for the ship.
            // (It refused every drag then, and a rebuild that never finished left the camera stuck.)
            bool shipReady = boundTransform != null && !reloading;
            if (!shipReady && !level.Inspect.Active) return;
            var p = ScreenPos();
            downAt = last = p;
            travel = 0f;
            fling = Vector2.zero;
            viewport.CapturePointer(e.pointerId);
            if (e.button == 2) { drag = Drag.Pan; return; }   // the middle button pans the view (a click centres it again)
            if (!EditsMounts || EditedMounts == null || !shipReady) { drag = Drag.Camera; return; }   // the hardpoints: a ship's, a kit part's
            if (e.button == 0 && placing >= 0)
            {
                if (gizmo.RaycastHull(p, out var hit, out var normal)) { int type = placing; placing = -1; AddMount(type, hit, normal); }
                else Status(T("mbMissedHull", "Click on the ship's hull."), false);
                drag = Drag.None;
                return;
            }
            if (e.button == 0 && gizmo.BeginDrag(p))
            {
                drag = Drag.Gizmo;
                gizmo.Refresh();
                RefreshPoints();
                if (kind == ModBuilder.Kind.Ships && tab != 1) SelectTab(1);
                return;
            }
            drag = Drag.Camera;
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (KindPointerMove(e)) return;
            if (drag == Drag.None || !viewport.HasPointerCapture(e.pointerId)) return;
            if (e.pressedButtons == 0) { viewport.ReleasePointer(e.pointerId); EndDrag(); return; }   // released where we didn't hear it
            var p = ScreenPos();
            float scale = 1080f / Mathf.Max(1, Screen.height);
            var delta = (p - last) * scale;
            delta.y = -delta.y;   // a drag up takes the camera down (the ship tips up with the finger), as in Inspect ship
            travel += delta.magnitude;
            last = p;
            if (drag == Drag.Pan)
            {
                level.Inspect.Pan(new Vector2(delta.x, -delta.y));   // the screen's own up (delta's y is the orbit's, flipped)
                return;
            }
            if (drag == Drag.Gizmo)
            {
                var kb = Keyboard.current;
                gizmo.Drag(p, kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed));
                MarkDirty();
            }
            else
            {
                pendingTurn += delta;   // applied frame by frame (UpdateCamera)
                fling = delta;
            }
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (KindPointerUp(e)) return;
            if (!viewport.HasPointerCapture(e.pointerId)) return;
            viewport.ReleasePointer(e.pointerId);
            EndDrag();
        }

        void EndDrag()
        {
            if (drag == Drag.Gizmo)
            {
                gizmo.EndDrag();
                var s = EditedMounts;
                int i = gizmo.Selected;
                if (s != null && i >= 0 && i < s.Count) OnMountEdited(s[i].slotType);
                RefreshPoints();
            }
            else if (drag == Drag.Pan)
            {
                if (travel < 6f) level.Inspect.ResetPan();   // a middle click: back on the ship's centre
            }
            else if (drag == Drag.Camera)
            {
                if (fling.magnitude <= 3f) fling = Vector2.zero;
                // A click (no drag) on empty space: nothing selected.
                if (travel < 6f && gizmo.Selected >= 0) { fling = Vector2.zero; gizmo.Selected = -1; gizmo.Refresh(); BuildHardpointsPage(); }
            }
            drag = Drag.None;
        }

        /// <summary>The scene goes (StationMenu's OnDestroy): the gizmo with it.</summary>
        public void Dispose()
        {
            if (gizmo != null) UnityEngine.Object.Destroy(gizmo.gameObject);
            DisposeKinds();
            DisposeAi();
            SaveNow();
        }
    }
}
