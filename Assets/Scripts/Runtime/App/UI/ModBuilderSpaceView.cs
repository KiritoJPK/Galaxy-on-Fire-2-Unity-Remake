// ModBuilderSpaceView.cs
// Remake-only: the Mod Builder's screen over the Space scene (Modding.ModBuilder.InSpace), where its space kinds are edited:
// Stations (in their own orbit: the station at the centre, its planet and sun behind it) and Weapon fx (a firing range in the
// builder's orbit: ModBuilderSpaceViewFx). The flight HUD hides and hands its Update over (FlightHud), the flight level holds
// the player's ship still (SpaceLevel.HoldForBuilder) and the chase camera is off: this screen's camera orbits the station or
// the range (drag turns, the middle button pans, the wheel zooms). The other kinds load the Station scene (ModBuilder.GoTo).
// Stations: the project's stations.json (ProjectStation, Modding/README.md "Stations"): left the new stations and the changes
// to others, right the name, its system (the game's, the project's or another mod's: a dependency), tech level, planet, the
// look (a game station's, a .glb of its own or one of the game's models, its size and turn), what ships bump into, its
// hangar and bar (the race's or a room of the project), its planet image and music. A change to its looks builds the orbit
// again (the game reads the project, the scene loads); Play from here starts a play test docked at it.

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
    public partial class ModBuilderSpaceView : ModBuilderScreen
    {
        const float M = 0.05f;
        readonly SpaceLevel level;
        VisualElement viewport;
        // the orbit camera: the point it turns about, yaw / pitch (rad), distance (m)
        Vector3 pivot;
        float yaw = 0.7f, pitch = 0.25f, distance = 3000f, radius = 1000f;
        bool dragging, panning;
        Vector2 last;
        Vector2 pendingTurn;

        VisualElement stationList, stationPage;
        ProjectStation currentStation;

        static readonly string[] InteriorRaces = { "terran", "vossk", "nivelian", "midorian" };

        protected override void BuildViewport(VisualElement root)
        {
            viewport = Add(root, new VisualElement { name = "mbsViewport" }, "mbv-viewport");
            viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.RegisterCallback<WheelEvent>(e => { if (!ModalOpen) distance = Mathf.Clamp(distance * Mathf.Pow(0.85f, -Mathf.Sign(e.delta.y)), radius * 0.3f, radius * 12f); e.StopPropagation(); });
        }

        protected override void AddTopButtons(VisualElement top) => TopButton(top, T("mbPlay", "Play"), ShowPlayMenu, "mbv-play-button");

        public ModBuilderSpaceView(SpaceLevel level, VisualElement hudRoot, Action<bool> sound) : base(hudRoot, sound)
        {
            this.level = level;
            var left = leftGroups[(int)ModBuilder.Kind.Stations];
            stationList = ListPanel(left, T("mbStations", "Stations"));
            Act(left, T("mbNewStation", "+ New station"), NewStation, "mbv-add");
            Act(left, T("mbChangeStation", "+ Change a station"), () => PickRef(RefKind.Station, T("mbChangeStationTitle", "Change a station"), e => ChangeStation(e.value),
                                                                                e => !e.own && !Project.Stations.Any(s => s.IsOverride && s.OverrideRef == e.value)), "mbv-add");
            Act(left, T("mbRemoveStation", "Remove"), AskRemoveStation, "mbv-danger", "mbv-remove");
            stationPage = PagePanel(rightGroups[(int)ModBuilder.Kind.Stations]);
            BuildFxPanels(leftGroups[(int)ModBuilder.Kind.WeaponFx], rightGroups[(int)ModBuilder.Kind.WeaponFx]);
            FinishLayout();
            currentStation = Project?.Stations.FirstOrDefault(s => s.Ref == ModBuilder.SpaceStationRef) ?? Project?.Stations.FirstOrDefault(s => StationNumberOf(s) == Session.StationIndex)
                             ?? Project?.Stations.FirstOrDefault();
            SelectKind(ModBuilder.OpenKind);
            ShowProblems();
        }

        protected override bool EditsHere(ModBuilder.Kind k) => ModBuilder.PlaceOf(k) == ModBuilder.Place.Space;

        protected override void ShowKind(ModBuilder.Kind k, ModBuilder.Kind was)
        {
            if (k == ModBuilder.Kind.AiTools) { StopRange(); return; }   // the AI tools' tab: the orbit stays as it is
            bool fx = k == ModBuilder.Kind.WeaponFx;
            if (level.Player != null) foreach (var r in level.Player.GetComponentsInChildren<Renderer>(true)) r.enabled = fx;   // the shooter only in the range
            if (fx) { RebuildFx(); BuildFxPage(); StartRange(); }
            else { StopRange(); RebuildStations(); BuildStationPage(); FrameStation(); }
        }

        protected override void OnReloaded()
        {
            if (kind == ModBuilder.Kind.WeaponFx) { RebuildFx(); BuildFxPage(); RestartRange(); }
            else { RebuildStations(); BuildStationPage(); }
        }

        protected override Action BeforeUndo()
        {
            string st = currentStation?.Ref, it = currentFx?.Ref;
            return () =>
            {
                currentStation = Project.Stations.FirstOrDefault(s => s.Ref == st) ?? Project.Stations.FirstOrDefault();
                currentFx = Project.Items.FirstOrDefault(i => i.Ref == it);
                OnReloaded();
            };
        }

        protected override bool Dragging => dragging || panning;

        // ---- the camera -------------------------------------------------------------------------------------------

        void FrameStation()
        {
            var st = level.Station;
            var b = new Bounds(Vector3.zero, Vector3.one * 200f);
            bool any = false;
            if (st != null)
                foreach (var r in st.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            pivot = b.center;
            radius = Mathf.Clamp(b.extents.magnitude, 100f, 20000f);
            distance = radius * 2.4f;
            ApplyAiView();
        }

        void ApplyCamera(float dt)
        {
            var cam = level.mainCamera;
            if (cam == null) return;
            if (pendingTurn != Vector2.zero)
            {
                var step = pendingTurn * (1f - Mathf.Exp(-dt / 25f));
                if (pendingTurn.sqrMagnitude < 0.01f) step = pendingTurn;
                pendingTurn -= step;
                var turn = Settings.OrbitTurn(step);   // #121: the camera views' own inversion
                yaw += turn.x * 0.005f;
                pitch = Mathf.Clamp(pitch - turn.y * 0.005f, -1.45f, 1.45f);
            }
            var dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
            var pos = pivot + dir * distance;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos, Vector3.up));
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, (distance + radius * 2f) * 1.5f);
            cam.nearClipPlane = Mathf.Clamp(distance * 0.002f, 0.3f, 20f);
        }

        static Vector2 ScreenPos() => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        void OnPointerDown(PointerDownEvent e)
        {
            if (ModalOpen) return;
            last = ScreenPos();
            dragging = e.button == 0 || e.button == 1;
            panning = e.button == 2;
            viewport.CapturePointer(e.pointerId);
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (!viewport.HasPointerCapture(e.pointerId)) return;
            if (e.pressedButtons == 0) { viewport.ReleasePointer(e.pointerId); dragging = panning = false; return; }
            var p = ScreenPos();
            var d = (p - last) * (1080f / Mathf.Max(1, Screen.height));
            last = p;
            if (panning)
            {
                var cam = level.mainCamera.transform;
                float k = distance * 0.0012f;
                pivot -= cam.right * d.x * k + cam.up * d.y * k;
            }
            else if (dragging) pendingTurn += new Vector2(d.x, d.y);
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (!viewport.HasPointerCapture(e.pointerId)) return;
            viewport.ReleasePointer(e.pointerId);
            dragging = panning = false;
        }

        // ---- per frame ----------------------------------------------------------------------------------------

        public void Update()
        {
            float dt = Time.unscaledDeltaTime * 1000f;
            if (!UpdateCommon(dt)) return;
            if (!ReadCommonKeys())
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    float f = dt / (1000f / 30f);
                    float l = kb.leftArrowKey.isPressed || kb.aKey.isPressed ? 1f : 0f, r = kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 1f : 0f;
                    float u = kb.upArrowKey.isPressed || kb.wKey.isPressed ? 1f : 0f, dn = kb.downArrowKey.isPressed || kb.sKey.isPressed ? 1f : 0f;
                    yaw += (r - l) * 0.03f * f;
                    pitch = Mathf.Clamp(pitch + (u - dn) * 0.03f * f, -1.45f, 1.45f);
                    float z = (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed ? 1f : 0f) - (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed ? 1f : 0f);
                    if (z != 0f) distance = Mathf.Clamp(distance * Mathf.Pow(1.03f, z * f), radius * 0.3f, radius * 12f);
                }
            }
            if (kind == ModBuilder.Kind.WeaponFx) UpdateRange(dt);
            ApplyCamera(dt);
        }

        public void Dispose()
        {
            DisposeAi();
            StopRange();
            SaveNow();
        }

        // ---- the play menu ------------------------------------------------------------------------------------

        void ShowPlayMenu()
        {
            var choices = new List<(string, Texture2D, Action)>
            {
                (T("mbPlayHere", "Play from this orbit's station"), null, () => { SaveNow(); ModBuilder.Reload(); ModBuilderPlay.Start(); }),
            };
            ShowPicker(T("mbPlay", "Play"), choices);
        }

        // ---- stations -----------------------------------------------------------------------------------------

        int StationNumberOf(ProjectStation st) => st.IsOverride ? ModBuilder.StationNumber(st.OverrideRef) : ModWorld.StationIndexOf(Project.Key(st.LocalId));

        string StationTitle(ProjectStation s) => !string.IsNullOrEmpty(s.Name) ? s.Name : s.IsOverride ? RefLabel(RefKind.Station, s.OverrideRef, s.OverrideRef) : s.LocalId;

        void RebuildStations()
        {
            stationList.Clear();
            if (Project == null) return;
            if (currentStation != null && !Project.Stations.Contains(currentStation)) currentStation = null;
            currentStation ??= Project.Stations.FirstOrDefault();
            foreach (var station in Project.Stations)
            {
                var s = station;
                int n = StationNumberOf(s);
                string sub = (s.IsOverride ? string.Format(T("mbChanges", "Changes {0}"), RefLabel(RefKind.Station, s.OverrideRef, s.OverrideRef)) : RefLabel(RefKind.System, s.System, "?"))
                           + (n == Session.StationIndex ? "  ·  " + T("mbShownHere", "this orbit") : "");
                ListRow(stationList, StationTitle(s), sub, s == currentStation, () => PickStation(s), null, null);
            }
            if (Project.Stations.Count == 0)
                Note(stationList, T("mbNoStations", "No stations yet. A new station goes into a system (yours or the game's); its orbit shows here."));
        }

        /// <summary>A station picked: its orbit shows (the scene loads when it is another).</summary>
        void PickStation(ProjectStation s)
        {
            currentStation = s;
            ModBuilder.SpaceStationRef = s.Ref;
            RebuildStations();
            BuildStationPage();
            int n = StationNumberOf(s);
            if (n >= 0 && n != Session.StationIndex) { SaveNow(); ModBuilder.ReloadSpace(n); }
            else if (n < 0) Status(T("mbStationNotRead", "The game hasn't read this station yet (check its system): fix the messages above."), true);
        }

        void NewStation() => PickRef(RefKind.System, T("mbNewStationSystem", "In which system?"), e => AskText(T("mbNewStation", "+ New station"), T("mbName", "Name"), "", name =>
        {
            var st = Project.AddStation(name, e.value, 5, 6);
            st.LooksLike = 70;
            currentStation = st;
            ModBuilder.SpaceStationRef = st.Ref;
            MarkDirty();
            RebuildStations();
            BuildStationPage();
            OrbitAfterReload(st);
        }));

        void ChangeStation(string reference)
        {
            var st = Project.AddStationOverride(reference);
            currentStation = st;
            ModBuilder.SpaceStationRef = st.Ref;
            MarkDirty();
            RebuildStations();
            BuildStationPage();
            OrbitAfterReload(st);
        }

        /// <summary>The game reads the project, then the station's orbit loads (its looks built again).</summary>
        void OrbitAfterReload(ProjectStation st)
        {
            RequestReload(true);
            afterReload.Add(() =>
            {
                if (kind != ModBuilder.Kind.Stations || st != currentStation) return;
                int n = StationNumberOf(st);
                if (n < 0) { Status(T("mbStationNotRead", "The game hasn't read this station yet (check its system): fix the messages above."), true); return; }
                ModBuilder.ReloadSpace(n);
            });
            Status(T("mbOrbitRebuilding", "Building the orbit again…"), false);
        }

        void AskRemoveStation()
        {
            var s = currentStation;
            if (s == null) return;
            Confirm(T("mbRemoveStation", "Remove"), string.Format(T("mbRemoveText", "Remove \"{0}\" from the project?"), StationTitle(s)), () =>
            {
                Project.Stations.Remove(s);
                if (!string.IsNullOrEmpty(s.Model) && !GameModels.Is(s.Model) && !Project.Stations.Any(o => o.Model == s.Model))
                    Project.DeleteFile(s.Model, false);
                currentStation = null;
                MarkDirty();
                RequestReload(false);
                RebuildStations();
                BuildStationPage();
            });
        }

        void Edited(bool looks)
        {
            MarkDirty();
            if (looks && currentStation != null) OrbitAfterReload(currentStation);
            else RequestReload(true);
            BuildStationPage();
        }

        void BuildStationPage()
        {
            stationPage.Clear();
            var s = currentStation;
            if (s == null) { Note(stationPage, T("mbPickStation", "Add a station on the left to start.")); return; }
            JsonButton(stationPage, s.Json, () => { SaveNow(); RequestReload(false); RebuildStations(); BuildStationPage(); },
                       T("mbJsonStationNote", "stations.json's fields: also volumes (collision boxes / spheres), materials, hangar / bar, music, planetTexture."));
            bool isNew = !s.IsOverride;
            Heading(stationPage, isNew ? T("mbNewStationSub", "New station") : string.Format(T("mbChanges", "Changes {0}"), RefLabel(RefKind.Station, s.OverrideRef, s.OverrideRef)));
            if (isNew) Note(stationPage, string.Format(T("mbStationKey", "Other content names it {0}."), Project.Key(s.LocalId)));
            int shown = StationNumberOf(s);
            if (shown >= 0 && shown != Session.StationIndex) Act(stationPage, T("mbShowOrbit", "Show its orbit"), () => { SaveNow(); ModBuilder.ReloadSpace(shown); });
            Text(stationPage, T("mbName", "Name"), s.Name, 40, v => { s.Name = v.Trim(); Edited(false); RebuildStations(); });
            if (isNew)
            {
                RefField(stationPage, T("mbSystem", "System"), RefKind.System, s.System, v => { if (v != null) { s.System = v; Edited(true); } }, clear: false);
                Int(stationPage, T("mbTechLevel", "Tech level (0-10: what its shop sells)"), s.TechLevel, 0, 10, v => { s.TechLevel = v; Edited(false); });
                var prow = Row(stationPage);
                Add(prow, new Label(string.Format(T("mbPlanetN", "Its planet: the game's planet {0}"), s.Planet)), "mbv-inline-label");
                Act(prow, "−", () => { int p = s.Planet - 1; if (p == 23) p = 22; if (p < 0) p = 26; s.Planet = p; Edited(true); }, "mbv-step");
                Act(prow, "+", () => { int p = s.Planet + 1; if (p == 23) p = 24; if (p > 26) p = 0; s.Planet = p; Edited(true); }, "mbv-step");
            }
            else Int(stationPage, T("mbTechLevel", "Tech level (0-10: what its shop sells)"), s.TechLevel, 0, 10, v => { s.TechLevel = v; Edited(false); });

            // ---- the look ----
            Heading(stationPage, T("mbLook", "Look"));
            bool game = GameModels.Is(s.Model, out string gm);
            bool file = !string.IsNullOrEmpty(s.Model) && !game;
            Note(stationPage, game ? string.Format(T("mbStationGameModel", "The game's {0}."), gm) : file ? s.Model
                            : s.LooksLike != null ? string.Format(T("mbLooksLike", "Like {0} (its model and shape)."), RefLabel(RefKind.Station, s.LooksLike.ToString(), s.LooksLike.ToString()))
                            : T("mbOwnLook", "Its system's race's first station."));
            var mrow = Row(stationPage);
            Act(mrow, T("mbLikeStation", "Like a game station…"), () => PickRef(RefKind.Station, T("mbLikeStation", "Like a game station…"), e =>
            {
                if (!int.TryParse(e.value, out int n)) { Status(T("mbLikeGameOnly", "Only one of the game's stations."), true); return; }
                s.LooksLike = n;
                s.Model = null;
                Edited(true);
            }, e => e.group == ModRefs.GameGroup));
            if (ModImport.Available)
                Act(mrow, T("mbOwnGlb", "Your .glb…"), () => PickGlb(T("mbPickStationModel", "Choose the station's model"), path =>
                {
                    string old = file ? s.Model : null;
                    s.Model = Project.AddModel(path, s.LocalId ?? "station_" + s.OverrideRef);
                    if (old != null && old != s.Model) Project.DeleteFile(old, Project.Stations.Any(o => o != s && o.Model == old));
                    s.ModelSize ??= 40000f;
                    Edited(true);
                }));
            Act(mrow, T("mbGameModel", "Game model…"), () => PickGameStationModel(s));
            if (game || file)
            {
                Act(mrow, T("mbNoOwnModel", "No own model"), () => { s.Model = null; s.ModelSize = null; Edited(true); }, "mbv-step");
                Float(stationPage, T("mbStationSize", "Size (largest extent, units: 20 = 1 m)"), s.ModelSize ?? (game ? 0f : 40000f), 0f, 400000f, v => { s.ModelSize = v <= 0f ? (float?)null : v; Edited(true); });
                if (game) Note(stationPage, T("mbGameModelSize", "0 = the model's own size."));
                var yrow = Row(stationPage);
                Add(yrow, new Label(string.Format(T("mbTurnN", "Turn: {0}°"), Mathf.RoundToInt(s.ModelYaw))), "mbv-inline-label");
                Act(yrow, "−15°", () => { s.ModelYaw -= 15f; Edited(true); }, "mbv-step");
                Act(yrow, "+15°", () => { s.ModelYaw += 15f; Edited(true); }, "mbv-step");
                var crow = Row(stationPage);
                FlagButton(crow, T("mbModelCentre", "Centred on its middle"), s.ModelCentre, v => s.ModelCentre = v, () => Edited(true));
                Note(stationPage, T("mbStationFront", "A launch leaves along the model's front (+Z after the turn), toward the orbit's planet."));
            }
            Segments(stationPage, T("mbCollision", "What ships bump into"), new[] { T("mbBox", "Its box"), T("mbSphere", "A sphere"), T("mbNoCollision", "Nothing") },
                     s.Collision == "sphere" ? 1 : s.Collision == "none" ? 2 : 0, n => { s.Collision = n == 1 ? "sphere" : n == 2 ? "none" : "box"; Edited(true); });

            // ---- docked ----
            Heading(stationPage, T("mbDocked", "Docked there"));
            int ir = Array.IndexOf(InteriorRaces, (s.Interior ?? "").ToLowerInvariant());
            if (int.TryParse(s.Interior, out int irn)) ir = irn;
            Segments(stationPage, T("mbInteriorRace", "Hangar and bar of"), new[] { T("mbSystemRace", "its system's race") }.Concat(ModRefs.RaceNames.Select(r => T("race" + r, r))).ToArray(),
                     ir + 1, n => { s.Interior = n == 0 ? null : InteriorRaces[n - 1]; Edited(false); });
            RefField(stationPage, T("mbOwnHangar", "Its own hangar"), RefKind.Interior, s.Hangar, v => { s.Hangar = v; Edited(false); }, T("mbRaceRoom", "the race's"),
                     filter: e => e.sub == "hangar");
            RefField(stationPage, T("mbOwnBar", "Its own Space Lounge"), RefKind.Interior, s.Bar, v => { s.Bar = v; Edited(false); }, T("mbRaceRoom", "the race's"),
                     filter: e => e.sub == "bar");
            Note(stationPage, T("mbRoomsNote", "Rooms are made in the Hangars & bars kind."));
            RefField(stationPage, T("mbStationMusic", "Docked music"), RefKind.Music, s.Music, v => { s.Music = v; Edited(false); }, T("mbSystemsMusic", "its system's"),
                     filter: e => e.group != ModRefs.GameGroup);

            Heading(stationPage, T("mbPlanetImage", "Its planet"));
            var irow = Row(stationPage);
            Add(irow, new Label($"{T("mbImage", "Image")}: {(s.PlanetTexture != null ? System.IO.Path.GetFileName(s.PlanetTexture) : T("mbGamePlanet", "the game's planet"))}"), "mbv-inline-label");
            Act(irow, T("mbChoose", "Choose…"), () => PickImage(T("mbPickPlanet", "Choose the planet (a round planet on transparency)"), path =>
            {
                string old = s.PlanetTexture;
                s.PlanetTexture = Project.AddFile(path, "textures", (s.LocalId ?? "station") + "_planet", ".png");
                Project.DeleteFile(old, false);
                Edited(true);
            }), "mbv-step");
            if (s.PlanetTexture != null) Act(irow, "✕", () => { Project.DeleteFile(s.PlanetTexture, false); s.PlanetTexture = null; Edited(true); }, "mbv-step");

            Act(stationPage, T("mbPlayStation", "Play: dock here"), () =>
            {
                int n = StationNumberOf(s);
                if (n < 0) { Status(T("mbStationNotRead", "The game hasn't read this station yet (check its system): fix the messages above."), true); return; }
                SaveNow();
                ModBuilder.Reload();
                Session.StationIndex = n;
                ModBuilderPlay.Start();
            }, "mbv-play");
        }

        void PickGameStationModel(ProjectStation s)
        {
            var db = Database.Shared;
            var names = db.Assemblies.Where(a => a.pack != ModContent.Pack && a.name != null && a.name.StartsWith("station_")).Select(a => a.name).Distinct().OrderBy(n => n).ToList();
            var choices = names.Select(n => (n, (Texture2D)null, (Action)(() => { s.Model = GameModels.Ref(n); s.ModelSize = null; Edited(true); }))).ToList();
            ShowPicker(T("mbGameStationModel", "One of the game's station models"), choices, true);
        }
    }
}
