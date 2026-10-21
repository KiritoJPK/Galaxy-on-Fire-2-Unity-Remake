// StationLevel.cs
// The docked-station scene (ModStation, module 5): builds the hangar (the main station view) and the bar
// (Space Lounge) of the current station and switches between them instantly, like the original.
// Research and all constants: Reference/research/station_interior.md, tables in StationTables.
//   Hangar (CutScene 0x17 / Level::createScene 0xc2910): room per hangar index at identity, the player's ship on a
//     turntable at (0, Y[ship], 0) (NPC engine meshes, exhaust off), 0..N parked ships on fixed slots; camera from the
//     phone table relative to the ship pivot with a slow random position drift (ModStation::OnUpdate 0xed2a8);
//     one fixed light from the camera side plus a per-race ambient (ModStation::resetLight 0xe9f1c); Vossk fog.
//   Bar (SpaceLounge 0x197890): room per race (no rotation = Unity yaw 180), one visitor per bar agent (Stock.agents,
//     AgentGenerator) on random slots as
//     camera-facing billboards with a glow behind and a floor shadow (updateScreenPositions 0x19ec30); camera eases
//     from A to B in 3 s on the first visit, then sways around the room origin (SpaceLounge::update 0x19ef20); lit by
//     the system's sun (StarSystem::initLight); Terran service bot loops, the Midorian prop replays now and then; remake:
//     the Vossk bar's two bartender arms the original never shows replay every 20 / 22 s (VosskBarBots).
// Both show the current system's sky behind the room (Level::createSpace builds the StarSystem for these levels).
// Music per station/race, ambience per screen (Station_Atmo_Mainview / _Lounge).
// Arrival also rolls or refreshes the station's shop stock (Shop.EnterStation); the shop itself is HangarWindow.
// Remake-only hangar flights (Settings.HangarFlights): docking from space flies the player's ship in through the
// forcefield onto the turntable (the station menu, its conversations and windows wait until it has landed),
// launching lifts it off and flies it out before the flight level loads (HangarFlight); the other ships come and go
// on the parked slots meanwhile (HangarTraffic). The camera keeps the original framing (the rooms are only modelled
// where it looks).
// Not yet: turret on the player ship (CutScene::checkForTurret),
// home-base stored ships.

using System;
using System.Collections.Generic;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GoF2Remake.World
{
    public enum StationView { Hangar, Lounge }

    [DefaultExecutionOrder(-100)]
    public class StationLevel : MonoBehaviour
    {
        const float M = OrbitLayout.MetersPerUnit;
        const float TwoPi = Mathf.PI * 2f;

        [Header("Scene")]
        public Camera mainCamera;
        [Tooltip("LIGHT0: the fixed hangar light, the system's sun in the bar.")]
        public Light keyLight;
        public string spaceScene = "Space";

        [Header("Station (-1 = Session)")]
        public int stationOverride = -1;
        public int shipOverride = -1;

        [Header("Bar visitors (single-mesh prefabs, set by the scene builder)")]
        public GameObject visitorTerranMale;
        public GameObject visitorTerranFemale;
        public GameObject visitorVossk;
        public GameObject visitorNivelian;
        public GameObject visitorMultipod;
        public GameObject visitorBobolan;
        public GameObject visitorGrey;
        public GameObject visitorGlow;
        public GameObject visitorShadow;
        [Tooltip("Glow material per bar race (materials 34800..34803).")]
        public Material[] glowMaterials = new Material[4];

        [Header("Audio")]
        public AudioSource musicSource;
        [Tooltip("Indexed by StationTables.Music.")]
        public AudioClip[] music = new AudioClip[7];
        public AudioClip mainViewAmbience;
        public AudioClip[] mainViewAdds;
        public AudioClip loungeAmbience;
        public AudioClip[] loungeAdds;
        [Tooltip("95 Station_Atmo_Hangar, while the hangar window is open (ModStation::OnKeyPress).")]
        public AudioClip hangarAmbience;
        public AudioClip[] hangarAdds;

        [Header("Tuning (not recovered constants)")]
        [Tooltip("URP intensity of the hangar light (original: diffuse 1.0, half the space sun's 2.0).")]
        public float hangarLightIntensity = 1.1f;
        [Tooltip("Scales the hangar ambient ((race ambient + GL global 0.2) * material ambient 0.7).")]
        public float hangarAmbientScale = 1f;
        public float sunIntensityAt2 = 1.6f;

        public StationView View { get; private set; } = StationView.Hangar;
        public event Action ViewChanged;
        public StationData Station { get; private set; }
        public OrbitLayout Layout { get; private set; }
        public int HangarIndex { get; private set; }
        public int BarRace { get; private set; }
        public int VisitorCount { get; private set; }
        public Database Database => db;
        /// <summary>This station's shop stock and ships for sale (kept while it is among the last 3 visited).</summary>
        public StationStock Stock { get; private set; }
        public bool IntroPlaying => introT < 1.25f && View == StationView.Lounge;

        Database db;
        int dbRevision;                       // ModManager.Revision when db was loaded
        /// <summary>The station's game database (the Customize panel's stats).</summary>
        public Database Db => db;
        Transform hangarRoot, barRoot, playerShip;
        int shipIndex;
        Vector3 shipPivot;                    // Unity, the camera parent (0, Y[ship at entry], 0)

        // Turntable (ModStation +0xe0 / 120 px per rad), game yaw in radians.
        float shipYaw, flingVelocity;         // rad/s

        // Hangar camera drift: three EaseInOuts around the table position (game units).
        readonly EaseInOut[] drift = { new EaseInOut(), new EaseInOut(), new EaseInOut() };
        readonly bool[] driftSign = new bool[3];
        Vector3 hangarCamBase;

        // Bar camera: intro A -> B (EaseInOutMatrix, t 0.75 -> 1.25), then sway.
        Vector3 barPosA, barPosB;
        Quaternion barRotA, barRotB;
        float introT = 0.75f;
        bool loungeVisited;
        readonly EaseInOut swayYaw = new EaseInOut();
        float swaySpeed = 2f, bobPhase;

        readonly List<Visitor> visitors = new List<Visitor>();
        PartAnimation midorianProp;
        float midorianTimer;
        // Remake: the Vossk bar's bartender arms (VosskBarBots): bot_a / bot_c the end arm's two animations, bot_b the second arm.
        VosskBarBots vosskBots;
        PartAnimation vosskBotA, vosskBotB, vosskBotC;
        bool vosskBotsPosed;
        CycleSound mainViewAtmo, loungeAtmo, hangarAtmo;
        bool hangarWindowOpen;

        class Visitor { public Transform body, glow; public Vector3 feet; public Agent agent; public float height; }

        /// <summary>The bar's agents with their visitor billboards (in Stock.agents order).</summary>
        public int VisitorAgentCount => visitors.Count;
        public Agent VisitorAgent(int i) => visitors[i].agent;
        /// <summary>World position just above the visitor's head (name labels, taps).</summary>
        public Vector3 VisitorHead(int i) => visitors[i].feet + Vector3.up * visitors[i].height;
        public Vector3 VisitorFeet(int i) => visitors[i].feet;
        public Camera MainCamera => mainCamera;

        HangarFlight playerFlight;
        HangarTraffic traffic;
        Action afterDeparture;
        bool departed;
        /// <summary>The player's ship is flying in or out (the station menu hides and waits).</summary>
        public bool PlayerFlying => playerFlight != null || departed || pendingDeparture != null;
        /// <summary>The player's ship on the turntable (VR: grabbed to turn it).</summary>
        public Transform PlayerShip => playerShip;
        /// <summary>VR: a visitor picked with the laser (the lounge opens their chat, LoungePanel).</summary>
        public System.Action<int> VisitorPicked;
        /// <summary>The player's ship is taking off or gone (multiplayer: the others in this hangar see it leave).</summary>
        public bool PlayerDeparting => (playerFlight != null && !playerFlight.arriving) || departed || pendingDeparture != null;
        /// <summary>The hangar's ship traffic (multiplayer: the other players' ships too, NetHangar), null = none.</summary>
        public HangarTraffic Traffic => traffic;
        /// <summary>This docking flew in from the orbit (multiplayer: the others see it land; else the ship just appears).</summary>
        public bool ArrivedFlying { get; private set; }

        /// <summary>AEEngine EaseInOut (0x7aa34): a + (b - a) * (sin(phi) * 0.5 + 0.5), phi 3pi/2 -> 5pi/2, Increase(d) adds
        /// d / 65536 * 2pi, so a whole leg takes 32768 units of d.</summary>
        class EaseInOut
        {
            float from, to, phi = 2.5f * Mathf.PI;
            public float Target => to;
            public float Value => from + (to - from) * (Mathf.Sin(phi) * 0.5f + 0.5f);
            public void Start(float a, float b) { from = a; to = b; phi = 1.5f * Mathf.PI; }
            public void Increase(float d) => phi = Mathf.Min(phi + d / 65536f * TwoPi, 2.5f * Mathf.PI);
        }

        // ---- build -------------------------------------------------------------------------------------------

        void Awake()
        {
            Story.ReopenUnshownSuccess();   // #88
            Modding.ModShips.ModelsChanged += OnModShipModels;
            Modding.ModTraffic.Warm();   // remake mods: more kit parts for the NPCs' random builds, loaded while docked
            // Multiplayer: loaded after its session ended (a docking queued behind the menu): on to the menu (nothing is
            // saved, SaveGame.SessionGame).
            if (GoF2Remake.Data.NetSession.SessionLost) SceneManager.LoadScene("MainMenu");
            db = Database.Load();
            dbRevision = Modding.ModManager.Revision;
            int station = stationOverride >= 0 ? stationOverride : Session.StationIndex;
            Hangar.NewDocking();
            if (stationOverride < 0) Session.LastDockedStation = station;   // where a multiplayer pilot starts and respawns
            Stock = Shop.EnterStation(db, station);
            PirateReports.Refresh(db);   // remake: the Pirate activity board's old reports off, new ones in
            Story.OnDocked(db, station, Stock);   // ModStation::OnInitialize's story tweaks (index 1: Betty ...)
            // Remake debug (PlayerHull): a hull the player can't normally fly never sits in a hangar (an old save from the
            // battleship toggle): back in the player's own ship.
            if (!PlayerHull.PlayerShip) PlayerHull.ForceOwnShip();
            Hangar.SyncBuiltIn(db);  // remake mods: the flown ship's built-in items (ships.json "integrated": "items")
            Hangar.FitToSlots(db);   // remake: no more mounted than the ship has slots (a mod's lower slot count, a debug swap)
            shipIndex = shipOverride >= 0 ? shipOverride : Session.ShipIndex;
            Station = db.Stations.Find(s => s.index == station);
            Layout = OrbitLayout.Build(db, station);
            // Remake mods: a station may name the race of its hangar and bar ("interior", ModWorld).
            int interior = Modding.ModWorld.InteriorRace(station);
            HangarIndex = StationTables.HangarIndex(station, interior >= 0 ? interior : Layout.raceId);
            // Remake mods: a custom hangar / bar room (interiors.json, ModInteriors); the race's tables stay for the rest.
            customHangar = Modding.ModInteriors.HangarOf(station);
            customBar = Modding.ModInteriors.BarOf(station);
            // The Mod Builder previews one of its project's rooms in place of the station's (ModBuilder.PreviewInterior).
            if (Modding.ModBuilder.Active && Modding.ModInteriors.Built(Modding.ModBuilder.PreviewInterior) is Modding.ModInteriors.Room preview)
            {
                if (preview.def.hangar) customHangar = preview; else customBar = preview;
            }
            Session.VisitedStations.Add(station);   // Galaxy::setVisited: the star map's "Already visited"
            // Docking repairs the ship (the original launches with Status hull / shield / armor = -1, "full", StarMap::
            // depart; assumed for every launch) and autosaves (ModStation::autosave).
            // Survivor medal: the hull % this ship arrived with (before the repair).
            int maxHull = Modding.ModShipKits.Armor(db.Ship(Session.ShipIndex), 100) + 40 * Session.ModLevel(0);
            Session.LastArrivalHullPercent = Session.PlayerHull < 0 ? 100 : Mathf.RoundToInt(100f * Session.PlayerHull / Mathf.Max(1, maxHull));
            Session.ArrivedWithoutGear = Achievements.NoWeaponOrEquipment(db);   // Harum-Scarum: the loadout docked with
            Session.HighestCredits = Mathf.Max(Session.HighestCredits, Session.Credits);
            Session.PlayerHull = Session.PlayerArmor = -1;
            Session.PlayerShield = -1f;
            // The gamma pool too: launching sets Status +0x68 to -1 (ModStation::OnTouchEnd, StarMap::depart), so a docked
            // supernova station (Var Lupra 112 ...) sends you out with a full pool; kept, a nearly empty one killed you again.
            Session.PlayerGamma = -1f;
            if (Story.AutosaveAllowed(station)) Session.Autosave();
            BarRace = StationTables.BarRace(interior >= 0 ? interior : Layout.raceId);
            if (mainCamera == null) mainCamera = Camera.main;
            ApplyAntialiasing();
            SetupDepthOfField();
            Settings.Changed -= ApplyAntialiasing;
            Settings.Changed += ApplyAntialiasing;

            OrbitBuilder.SetupSky(Layout);   // the system's sky shows through the openings of both rooms
            OrbitBuilder.SpawnBackdrop(Layout, mainCamera);
            // Remake: the orbit's sky layers outside too, as in space: the planet ring sky (stations 120, 126, 130, 132), the
            // supernova flares (the stronger "nasty" ones from mission 106) and the storms in the supernova system, the asteroid
            // belt of systems 24-26 (Skavac, Loma, Shima) lit by the system's sun.
            SkyLayers.Spawn(Layout, mainCamera);
            BuildHangar();
            BuildBar();
            // Remake: ships now and then flying past outside the bar's windows (not at an evacuated station).
            if (!Evacuated) gameObject.AddComponent<BarFlybys>().Setup(this, barRoot, barPosB, barRotB, BarRace == 1, RandomParkedShip,
                                                       ship => SpawnShip(ship, Vector3.zero, Quaternion.identity, barRoot, "Flyby"));
            Debug.Log($"StationLevel: station {station} {Station?.name} ({Station?.systemName}), hangar {HangarIndex}, " +
                      $"bar {BarRace}, ship {shipIndex}, {VisitorCount} visitors");

            if (musicSource != null)
            {
                var clip = music != null && music.Length > 0 ? music[(int)StationTables.MusicFor(station, Layout.raceId)] : null;
                // Globals::playMusicAndFadeOutCurrent(0): station 10 at campaign 0x9f plays 144 OutroSong.
                if (station == 10 && !Session.FreePlay && Session.CampaignMission == 0x9f && StoryAssets.Load()?.outroSong != null) clip = StoryAssets.Load().outroSong;
                if (clip == null && music != null && music.Length > 0) clip = music[0];
                clip = Modding.ModWorld.StationMusic(station) ?? Modding.ModMusic.Replace(clip);   // remake mods: a station's / system's own, a replaced track
                musicSource.clip = clip;
                musicSource.loop = true;
                musicSource.volume = Settings.MusicVolume * GoF2Remake.World.SpaceHooks.SceneMusic;
                if (clip != null) musicSource.Play();
            }
            SetView(StationView.Hangar, true);

            // Remake: docked from space = fly in through the forcefield (not after loading a save, a new game, a reload).
            bool flyIn = Session.DockedFromSpace && Settings.HangarFlights;
            Session.DockedFromSpace = false;
            ArrivedFlying = flyIn;
            var lane = Lane;
            if (flyIn && lane != null && playerShip != null)
            {
                var engine = HangarFlight.AddEngine(playerShip.gameObject, true, db, shipIndex, out float volume);
                playerFlight = HangarFlight.Arrival(playerShip, lane, playerShip.position, playerShip.rotation, engine, volume);
                playerFlightStart = Time.unscaledTime;
            }
        }

        StationTables.HangarLane Lane => customHangar != null ? CustomLane
            : HangarIndex >= 0 && HangarIndex < StationTables.HangarLanes.Length ? StationTables.HangarLanes[HangarIndex] : null;

        // ---- remake mods: a custom hangar / bar (ModInteriors) ----

        Modding.ModInteriors.Room customHangar, customBar;
        StationTables.HangarLane customLane;

        /// <summary>The custom hangar's way in and out: through its "gate" marker toward "gate_out", passable at gateSpan (default
        /// the gate's height +-30 m), crossing the room at 'cruise' metres above the pad (default 40); none without the markers.</summary>
        StationTables.HangarLane CustomLane
        {
            get
            {
                if (customLane != null || customHangar == null || !customHangar.hasGate || !customHangar.def.flights) return customLane;
                var outward = customHangar.gateOut - customHangar.gate;
                outward.y = 0f;
                if (outward.sqrMagnitude < 1e-4f) return null;
                float gy = customHangar.gate.y;
                customLane = new StationTables.HangarLane
                {
                    gate = customHangar.gate, outward = outward.normalized,
                    gateSpan = customHangar.def.gateSpan ?? new Vector2(gy - 30f, gy + 30f),
                    cruise = customHangar.pad.y + (float.IsNaN(customHangar.def.cruise) ? 40f : customHangar.def.cruise),
                };
                return customLane;
            }
        }

        /// <summary>The player's (and a parked ship's) pivot on a pad: the original's table, or a custom hangar's marker plus the
        /// ship's own height (StationTables.ShipY).</summary>
        Vector3 PadPosition(int ship) => customHangar != null
            ? customHangar.pad + Vector3.up * (StationTables.ShipY(ship) * M)
            : OrbitLayout.ToUnity(new Vector3(0f, StationTables.PadPivotY(HangarIndex, -1, ship, Quaternion.identity) / M, 0f));

        int ParkedSlotCount => customHangar != null ? customHangar.parked.Count : StationTables.ParkedSlots[HangarIndex]?.Length ?? 0;
        int ParkedMaxCount => customHangar != null ? (customHangar.def.parkedMax >= 0 ? Mathf.Min(customHangar.def.parkedMax, customHangar.parked.Count) : customHangar.parked.Count)
                                                   : StationTables.ParkedMax[HangarIndex];

        int VisitorSlotCount => customBar != null ? customBar.visitors.Count : StationTables.VisitorSlots[BarRace].Length;
        Vector3 SlotFeet(int slot) => customBar != null ? customBar.visitors[slot] : OrbitLayout.ToUnity(StationTables.VisitorSlots[BarRace][slot]);

        /// <summary>Remake: the Valkyrie battlestation hangar's v_hangar_battlestation_add_part32 / 33 sat off their place (their
        /// X keys are the absolute place while the import baked it into their vertices too): held at a local x placed by eye
        /// in the remake (their other keys still play).</summary>
        static void PlaceValkyrieHangarParts(GameObject room)
        {
            if (room == null) return;
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
            {
                float x;
                if (t.name == "v_hangar_battlestation_add_part32") x = -57.2f;
                else if (t.name == "v_hangar_battlestation_add_part33") x = -57.1f;
                else continue;
                var anim = t.GetComponentInParent<PartAnimation>(true);
                if (anim == null || !anim.PinLocalX(t, x)) t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);
            }
        }

        void BuildHangar()
        {
            hangarRoot = new GameObject("Hangar").transform;
            string room = StationTables.HangarRoom[HangarIndex];
            // Level::createScene: PlayerStatic + setRotation(0, pi, 0) = Unity identity. Remake mods: a custom room as it is.
            GameObject roomGo;
            if (customHangar != null) { roomGo = Instantiate(customHangar.template, Vector3.zero, Quaternion.identity, hangarRoot); roomGo.name = "Room (" + customHangar.def.key + ")"; }
            else roomGo = Spawn(room, Vector3.zero, OrbitLayout.RotationToUnity(new Vector3(0f, Mathf.PI, 0f)), hangarRoot, "Room");
            HangarRoom = roomGo != null ? roomGo.transform : null;
            // CutScene::process updates every geometry of the scene each frame, so the room's animated layers all run,
            // not only the *_anim meshes (the Terran hangar_terran_add lights run along the side gutters; at frame 0 they
            // sat on the player's pad). The loops skip their one-off first key (every part at the origin for 33 / 50 ms,
            // a jump on every wrap: PartAnimation's default start), the rotations swing back and forth (the Vossk ring lights' 57 deg sweep, clear of the
            // portal, instead of snapping back), and the Vossk portal's light fades out as it moves (its `extra` channel).
            if (roomGo != null)
                foreach (var a in roomGo.GetComponentsInChildren<PartAnimation>(true))
                {
                    a.play = true;
                    // Modified: not the Terran hangar's bots (hangar_terran_bot_*): they drive a keyed loop, a heading swung
                    // back on every other loop turned them against their path.
                    a.pingPongRotation = !a.gameObject.name.Contains("_bot_");
                    // Remake: and the Nivelian hangar's flashing light (hangar_nivelian_add, PartAnimation.HasSignedExtra).
                    // Modified: and the Terran hangar's blinking light (hangar_terran_add_part0, its `extra` 0..100).
                    if (a.gameObject.name.Contains("_anim") || a.HasSignedExtra || a.gameObject.name.StartsWith("hangar_terran_add"))
                        a.applyMaterialChannels = true;
                    // Modified: the Terran hangar's beta bots are keyed heading first (PartAnimation.headingFirst).
                    if (a.gameObject.name.StartsWith("hangar_terran_bot_")) a.headingFirst = true;
                }
            if (customHangar == null) PlaceValkyrieHangarParts(roomGo);
            CollectRoomHighlights(roomGo);

            shipPivot = PadPosition(shipIndex);   // clear of the pad (remake)
            var ship = SpawnShip(shipIndex, shipPivot, Quaternion.identity, hangarRoot, "Player ship");
            playerShip = ship != null ? ship.transform : null;
            if (playerShip != null) PlayerHull.FitHangar(playerShip);   // remake debug: a freighter / capital ship shrunk
            CentreOnPad();
            RefreshTurret(true);
            shipYaw = StationTables.StartYaw(HangarIndex);
            if (customHangar != null)
            {
                // Unity degrees: "startYaw", else the ship's nose 35 deg off the camera's line (game yaw: Unity yaw = 180 - it).
                var toCam = customHangar.camera - customHangar.pad;
                float unityYaw = !float.IsNaN(customHangar.def.startYaw) ? customHangar.def.startYaw
                               : Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg + 35f;
                shipYaw = (180f - unityYaw) * Mathf.Deg2Rad;
            }
            ApplyShipYaw();
            // Multiplayer: another player docked here runs the hangar's NPC ships: theirs come with its snapshot (NetHangar).
            bool hangarFollower = GoF2Remake.Data.NetSession.Active && GoF2Remake.World.SpaceHooks.OtherDocked(Layout.stationIndex);
            if (!hangarFollower) SpawnParkedShips();
            // The others come and go (remake), except the club's stored hulls. Multiplayer: the other players docked here park
            // on the slots too (NetHangar), with or without the NPC traffic and the flights.
            bool slots = ParkedSlotCount > 0 && ParkedMaxCount > 0;
            bool npcTraffic = Settings.HangarFlights && Lane != null && slots && !KaamoClub.StorageAt(Layout.stationIndex) && !Evacuated;
            if (npcTraffic || (slots && GoF2Remake.Data.NetSession.Active))
                traffic = new HangarTraffic(Lane, ParkedSlotCount, ParkedMaxCount,
                                            parkedShips, db, NewParkedShip, ParkedPosition,
                                            (ship, pos, rot) => SpawnShip(ship, pos, rot, hangarRoot, "Visiting ship",
                                                                          // another player's: their build of a customizable ship
                                                                          traffic != null && traffic.SpawningGuest >= 0 ? SpaceHooks.GuestAssembly?.Invoke((ulong)traffic.SpawningGuest, ship) : null),
                                            npcTraffic, Settings.HangarFlights, FitParkingYaw);
            if (traffic != null) traffic.NpcSpawned = (go, ship) => AddNpcTurrets(go, ship, null);
            if (traffic != null && GoF2Remake.Data.NetSession.Active)
            {
                traffic.KeepOneFree = true;          // a pad for another player docking
                traffic.RunsNpcs = !hangarFollower;
            }
            SpaceHooks.StationBuilt?.Invoke(this);   // multiplayer: the other players in the hangar (NetHangar)

            // Camera: ModStation::OnInitialize state 0x14 (phone table), rotation order 2 with roll -0.03.
            hangarCamBase = customHangar != null ? Vector3.zero : StationTables.HangarCameraPos[HangarIndex];   // a custom room drifts about its marker
            for (int i = 0; i < 3; i++) { driftSign[i] = Random.Range(0, 20) < 10; NextDriftLeg(i, Base(i)); }
        }

        /// <summary>CutScene::replacePlayerShip 0xa53b4: after buying a ship, the new one on the turntable at its own height
        /// (the camera keeps the pivot of the ship the hangar was entered with).</summary>
        public void ReplacePlayerShip(int index)
        {
            // The Mod Builder changes the mods while docked: a ship added since the hangar opened isn't in the old tables.
            if (dbRevision != Modding.ModManager.Revision) { db = Database.Load(); dbRevision = Modding.ModManager.Revision; }
            if (playerShip != null) Destroy(playerShip.gameObject);
            shipIndex = index;
            if (index == Session.ShipIndex) Hangar.SyncBuiltIn(db);   // its built-in items (a purchase, a stored hull, a debug swap)
            var ship = SpawnShip(index, PadPosition(index), Quaternion.identity, hangarRoot, "Player ship");
            playerShip = ship != null ? ship.transform : null;
            if (playerShip != null) PlayerHull.FitHangar(playerShip);   // remake debug: a freighter / capital ship shrunk
            CentreOnPad();
            RefreshTurret(true);
            ApplyShipYaw();
            ApplyEnginePreview();
        }

        bool enginePreview;
        float enginePreviewDrive = 1f;

        /// <summary>How hard the previewed engines run (1 full throttle, 2 a full boost; EnginePreview).</summary>
        public void PreviewEngineDrive(float drive)
        {
            enginePreviewDrive = drive;
            var preview = playerShip != null ? playerShip.GetComponent<GoF2Remake.Visuals.EnginePreview>() : null;
            if (preview != null) preview.drive = drive;
        }

        /// <summary>Remake mods: the turntable ship's engines run (the Customize screen: its exhaust can be seen) or stop again.</summary>
        public void PreviewEngines(bool on)
        {
            enginePreview = on;
            ApplyEnginePreview();
        }

        void ApplyEnginePreview()
        {
            if (playerShip == null) return;
            var asm = playerShip.GetComponent<AssembledObject>();
            if (asm != null && asm.playerVariantParts != null)
                foreach (var p in asm.playerVariantParts) if (p != null) p.SetActive(enginePreview);
            var preview = playerShip.GetComponent<GoF2Remake.Visuals.EnginePreview>();
            if (enginePreview && preview == null) playerShip.gameObject.AddComponent<GoF2Remake.Visuals.EnginePreview>().drive = enginePreviewDrive;
            else if (!enginePreview && preview != null) Destroy(preview);
        }

        GameObject turret, hardpoints;
        string turretItems = "";

        /// <summary>CutScene::checkForTurret 0xa4594: the mounted turret on the turntable ship, rebuilt whenever the turret
        /// item changes (the original re-runs it after every equipment change). Remake: every turret, each on its mount, and a
        /// kit ship's weapon models (ModHardpoints), rebuilt when the weapons change too.</summary>
        void RefreshTurret(bool force)
        {
            string items = string.Join(",", GoF2Remake.Flight.PlayerTurret.TurretItems(db, Session.Equipment)) + "|"
                         + string.Join(",", Modding.ModHardpoints.WeaponItems(db, Session.Equipment, false).Select(i => i.index)) + "|"
                         + string.Join(",", Modding.ModHardpoints.WeaponItems(db, Session.Equipment, true).Select(i => i.index));
            if (!force && items == turretItems) return;
            turretItems = items;
            if (turret != null) Destroy(turret);
            if (hardpoints != null) Destroy(hardpoints);
            turret = playerShip != null ? GoF2Remake.Flight.PlayerTurret.BuildStatic(db, shipIndex, Session.Equipment, playerShip) : null;
            hardpoints = playerShip != null ? Modding.ModHardpoints.AttachWeapons(db, shipIndex, Session.Equipment, playerShip) : null;
        }

        readonly List<HangarTraffic.Parked> parkedShips = new List<HangarTraffic.Parked>();

        /// <summary>The club's parked hulls changed (Use / Sell in the storage): park them again.</summary>
        public void RefreshParkedShips()
        {
            if (!KaamoClub.StorageAt(Layout.stationIndex)) return;
            foreach (var p in parkedShips) if (p.go != null) Destroy(p.go);
            parkedShips.Clear();
            SpawnParkedShips();
        }

        /// <summary>Level::createScene 0x17: count = nextInt(max + 1), 70 % a fighter of the hangar's race, 30 % a random race
        /// (of those 30 % pirates); random free slot, yaw nextInt(300) / 100 rad. The owned Kaamo Club (kaamo_club.md 6.6)
        /// parks the first min(stored, max) hulls of its storage instead, in list order.</summary>
        void SpawnParkedShips()
        {
            int slotCount = ParkedSlotCount;
            int max = ParkedMaxCount;
            if (slotCount <= 0 || max <= 0 || Evacuated) return;   // remake: an evacuated station's hangar is empty
            bool club = KaamoClub.StorageAt(Layout.stationIndex);
            int count = Mathf.Min(club ? Mathf.Min(Session.KaamoShips.Count, max) : Random.Range(0, max + 1), slotCount);
            if (GoF2Remake.Data.NetSession.Active && !club) count = Mathf.Min(count, slotCount - 1);   // a pad for another player
            var taken = new bool[slotCount];
            for (int n = 0; n < count; n++)
            {
                int ship = club ? Session.KaamoShips[n].ship : NewParkedShip();
                int slot = Random.Range(0, slotCount), tries = 0;
                while (taken[slot] && ++tries < 100) slot = Random.Range(0, slotCount);
                if (taken[slot]) break;
                taken[slot] = true;
                float yaw = FitParkingYaw(slot, ship, Random.Range(0, 300) / 100f);
                var rot = OrbitLayout.RotationToUnity(new Vector3(0f, yaw, 0f));
                var parked = SpawnShip(ship, ParkedPosition(slot, ship, rot), rot, hangarRoot, $"Parked ship {n}");
                // Remake: its turrets (a stored hull its own mounted ones, Settings.KaamoKeepsEquipment; an NPC's its type's).
                AddNpcTurrets(parked, ship, club ? GoF2Remake.Flight.PlayerTurret.TurretItems(db, Session.KaamoShips[n].equipment) : null);
                if (parked != null) parkedShips.Add(new HangarTraffic.Parked { go = parked, slot = slot, ship = ship, yaw = yaw });
            }
        }

        /// <summary>Remake (generic turrets): a ship parked or landing here shows its turrets still, like the player's ship
        /// (Flight.TurretCore.Display): 'items' (a stored hull's mounted ones), else the turret items its type's NPCs carry in
        /// space (Database.NpcTurrets, a mod's "npcTurrets"), else the fighter turret of ships 45 / 51 (Traffic, the
        /// turret_002_static object Level::createFighterTurrets puts on them). Returns 'shipGo'.</summary>
        GameObject AddNpcTurrets(GameObject shipGo, int ship, IList<int> items)
        {
            if (shipGo == null) return shipGo;
            var shown = GoF2Remake.Flight.TurretCore.DisplayAll(db, ship, items ?? db.NpcTurretsOf(ship), shipGo.transform);
            // Turret items take the fighter turret's place (Traffic.Create), which sits on that mount.
            string fighterTurret = items == null && shown == null ? GoF2Remake.World.Traffic.FighterTurretAssembly(ship) : null;
            var prefab = fighterTurret != null ? AssembledObject.LoadPrefab(db.AssemblyByName(fighterTurret)) : null;
            if (prefab != null)
            {
                var t = Instantiate(prefab, shipGo.transform, false);
                t.name = "Fighter turret";
                t.transform.localPosition = GoF2Remake.World.Traffic.FighterTurretLocal(ship) + prefab.transform.localPosition;
                foreach (var a in t.GetComponentsInChildren<PartAnimation>(true)) a.speed = 0f;
            }
            return shipGo;
        }

        /// <summary>A heading that fits the pad (StationTables.FitParkingYaw); a custom hangar's as it is.</summary>
        float FitParkingYaw(int slot, int ship, float gameYaw) =>
            customHangar != null ? gameYaw : StationTables.FitParkingYaw(HangarIndex, slot, ship, gameYaw);

        /// <summary>The evacuated supernova system's stations (Shop.InSupernovaSystem, system 27 before campaign 158): an empty
        /// lounge like the original's (AgentGenerator.CreateAgents), and (remake) no parked ships, hangar traffic or bar flybys
        /// either.</summary>
        bool Evacuated => Shop.InSupernovaSystem(Shop.SystemOf(db, Layout.stationIndex), Layout.stationIndex);

        int NewParkedShip() => Layout.stationIndex == 100 ? StationTables.DeepScienceShips[Random.Range(0, 3)] : RandomParkedShip();

        /// <summary>The Unity pivot of 'ship' parked on 'slot' at 'rotation' (the slot plus the ship's height, lifted where the
        /// hull would cut into the pad: StationTables.PadPivotY).</summary>
        Vector3 ParkedPosition(int slot, int ship, Quaternion rotation)
        {
            // Another player's customizable ship floats at the height of their build (multiplayer, HangarTraffic.SpawningGuest).
            float guestY = traffic != null && traffic.SpawningGuest >= 0 && NetSession.Active && SpaceHooks.GuestShipY != null
                ? SpaceHooks.GuestShipY((ulong)traffic.SpawningGuest, ship) : float.NaN;
            return customHangar != null
                ? customHangar.parked[slot] + Vector3.up * ((float.IsNaN(guestY) ? StationTables.ShipY(ship) : guestY) * M)   // a custom hangar's marker
                : new Vector3(0f, StationTables.PadPivotY(HangarIndex, slot, ship, rotation, guestY), 0f)
                  + Vector3.Scale(OrbitLayout.ToUnity(StationTables.ParkedSlots[HangarIndex][slot]), new Vector3(1f, 0f, 1f));
        }

        int RandomParkedShip()
        {
            int race = HangarIndex;
            if (Random.value >= 0.7f) race = Random.value < 0.3f ? 8 : Random.Range(0, 4);
            var list = race == 8 ? StationTables.PirateFighters : StationTables.RaceFighters[Mathf.Clamp(race, 0, 3)];
            return list[Random.Range(0, list.Length)];
        }

        void BuildBar()
        {
            barRoot = new GameObject("Space Lounge").transform;
            // createScene branch 4: rooms with no rotation (game identity = Unity yaw 180).
            GameObject room;
            if (customBar != null) { room = Instantiate(customBar.template, Vector3.zero, Quaternion.identity, barRoot); room.name = "Room (" + customBar.def.key + ")"; }
            else room = Spawn(StationTables.BarRoom[BarRace], Vector3.zero, OrbitLayout.RotationToUnity(Vector3.zero), barRoot, "Room");
            BarRoom = room != null ? room.transform : null;
            // As in the hangar: the loops (and the Midorian prop's replays) skip their one-off first key, where every part
            // sits at the origin for 33 / 50 ms; played, it flashed for a frame on every wrap (the Nivelian bar's 6.5 s
            // bar_nivelian_anim_add, the Midorian prop each time it replayed).
            if (room != null && BarRace == 3 && customBar == null)
            {
                // CutScene::initialize (mode 4): bar_midorian_alpha_anim is a one-shot, restarted with 30 % every 2 s.
                foreach (var a in room.GetComponentsInChildren<PartAnimation>(true))
                    if (a.gameObject.name.Contains("alpha_anim")) { midorianProp = a; a.loop = false; a.play = false; }
            }
            if (room != null && BarRace == 1 && customBar == null)
            {
                // Remake: the bartender arms (bar_vossk_bot_*, in the assembly): one-shots, VosskBarBots says when.
                foreach (var a in room.GetComponentsInChildren<PartAnimation>(true))
                {
                    string n = a.gameObject.name;
                    if (!n.StartsWith("bar_vossk_bot_")) continue;
                    a.loop = false;
                    a.play = false;
                    if (n.StartsWith("bar_vossk_bot_a")) vosskBotA = a;
                    else if (n.StartsWith("bar_vossk_bot_b")) vosskBotB = a;
                    else if (n.StartsWith("bar_vossk_bot_c")) vosskBotC = a;
                }
                if (vosskBotA != null && vosskBotB != null && vosskBotC != null)
                {
                    vosskBots = new VosskBarBots(() => Random.value);
                    vosskBotC.gameObject.SetActive(false);   // the end arm's other animation: one arm shows at a time
                }
            }

            // One visitor per agent (Generator::createAgents, kept with the stock): slot nextInt(7) re-rolled until free,
            // the mesh by the agent's race (Level::createScene 0xc2b3e: a Midorian with a Nivelian face uses the Nivelian
            // mesh, female Terrans their own).
            var agents = Stock != null ? Stock.agents : new List<Agent>();
            slotTaken = new bool[VisitorSlotCount];
            VisitorCount = Mathf.Min(agents.Count, VisitorSlotCount);
            for (int i = 0; i < VisitorCount; i++) SpawnVisitor(agents[i]);
            // Remake multiplayer: the event graphs' bar missions offered here (EventMissions) join as they arrive.
            GoF2Remake.World.SpaceHooks.RequestEventOffers?.Invoke(Station != null ? Station.index : Session.StationIndex, AddVisitor);

            barPosA = OrbitLayout.ToUnity(StationTables.BarCameraStart[BarRace]);
            barPosB = OrbitLayout.ToUnity(StationTables.BarCameraRest[BarRace]);
            barRotA = CameraRotation(0f, StationTables.BarCameraStartYaw[BarRace], 0f);
            barRotB = CameraRotation(0f, StationTables.BarCameraRestYaw[BarRace], 0f);
            if (customBar != null)
            {
                // A custom bar's markers: the resting view, and the first visit's glide from "camera_start" (else no glide).
                barPosB = customBar.camera;
                barRotB = Quaternion.LookRotation(customBar.cameraTarget - customBar.camera, Vector3.up);
                barPosA = customBar.hasStart ? customBar.cameraStart : barPosB;
                barRotA = customBar.hasStart ? Quaternion.LookRotation(customBar.cameraStartTarget - customBar.cameraStart, Vector3.up) : barRotB;
            }
            swayYaw.Start(0f, 5f);
        }

        bool[] slotTaken;

        /// <summary>Remake: one more visitor on a free slot after the bar was built (false: every slot taken). The lounge's
        /// plates and list follow (LoungePanel rebuilds when the count changes).</summary>
        public bool AddVisitor(Agent agent)
        {
            if (agent == null || slotTaken == null || barRoot == null || System.Array.IndexOf(slotTaken, false) < 0) return false;
            if (visitors.Exists(v => v.agent == agent)) return true;
            return SpawnVisitor(agent);
        }

        /// <summary>A visitor billboard for 'agent' on a random free slot (Generator::createAgents' slot nextInt(7) re-rolled
        /// until free) with its glow and floor shadow.</summary>
        bool SpawnVisitor(Agent agent)
        {
            if (System.Array.IndexOf(slotTaken, false) < 0) return false;
            int slot;
            do slot = Random.Range(0, slotTaken.Length); while (slotTaken[slot]);
            slotTaken[slot] = true;
            int i = visitors.Count;
            {
                int race = agent.race == 3 && agent.portrait != null && agent.portrait[0] == 2 ? 2 : agent.race;
                var prefab = VisitorPrefab(StationTables.VisitorFor(race, !agent.male));
                if (prefab == null) return false;
                var feet = SlotFeet(slot);
                var v = new Visitor { feet = feet, agent = agent };
                v.body = Instantiate(prefab, feet, Quaternion.identity, barRoot).transform;
                v.body.name = $"Visitor {i} ({prefab.name})";
                var bounds = new Bounds(feet, Vector3.zero);
                foreach (var r in v.body.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                v.height = Mathf.Max((bounds.max.y - feet.y) * 0.55f, 60f * M);   // the billboard quad is about twice the figure
                if (visitorGlow != null)
                {
                    v.glow = Instantiate(visitorGlow, feet, Quaternion.identity, barRoot).transform;
                    v.glow.name = $"Visitor {i} glow";
                    var mat = glowMaterials != null && BarRace < glowMaterials.Length ? glowMaterials[BarRace] : null;
                    if (mat != null) foreach (var r in v.glow.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                }
                if (visitorShadow != null)
                    Instantiate(visitorShadow, feet + new Vector3(0f, 20f * M, 0f), OrbitLayout.RotationToUnity(Vector3.zero), barRoot).name = $"Visitor {i} shadow";
                visitors.Add(v);
            }
            return true;
        }

        GameObject VisitorPrefab(StationTables.Visitor v) => v switch
        {
            StationTables.Visitor.TerranFemale => visitorTerranFemale != null ? visitorTerranFemale : visitorTerranMale,
            StationTables.Visitor.Vossk => visitorVossk,
            StationTables.Visitor.Nivelian => visitorNivelian,
            StationTables.Visitor.Multipod => visitorMultipod,
            StationTables.Visitor.Bobolan => visitorBobolan,
            StationTables.Visitor.Grey => visitorGrey,
            _ => visitorTerranMale,
        };

        GameObject Spawn(string assembly, Vector3 unityPos, Quaternion rot, Transform parent, string label)
        {
            var prefab = AssembledObject.LoadPrefab(db.AssemblyByName(assembly));
            if (prefab == null) { Debug.LogWarning($"StationLevel: no assembled prefab '{assembly}'"); return null; }
            var go = Instantiate(prefab, unityPos, rot, parent);
            go.name = label;
            foreach (var lod in go.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);   // close-up room: full detail
            return go;
        }

        /// <summary>createShip(race, 0, idx, null, false): NPC mesh group, setExhaustVisible(false), asleep.</summary>
        GameObject SpawnShip(int index, Vector3 gamePos, float gameYaw, Transform parent, string label, AssemblyData assembly = null)
        {
            var entry = assembly ?? PlayerHull.Assembly(db, index);   // the player's own ship: remake debug, the Ships tab's pick
            if (entry == null) return null;
            var go = Spawn(entry.name, OrbitLayout.ToUnity(gamePos), OrbitLayout.RotationToUnity(new Vector3(0f, gameYaw, 0f)), parent, label);
            var asm = go != null ? go.GetComponent<AssembledObject>() : null;
            if (asm != null)
            {
                asm.SetPlayerVariant(false);
                if (asm.npcVariantParts != null) foreach (var p in asm.npcVariantParts) if (p != null) p.SetActive(false);   // exhaust off
                // Remake mods: the exhaust style of the player's build, or of a guest's (ModExhausts; before the particles,
                // which it replaces).
                var exhaustBuild = label == "Player ship" ? (PlayerHull.OwnEngines(db) ? Modding.ModShipKits.PlayerBuild(index) : null)
                                 : traffic != null && traffic.SpawningGuest >= 0 && NetSession.Active
                                   ? SpaceHooks.GuestBuild?.Invoke((ulong)traffic.SpawningGuest, index) : null;
                if (exhaustBuild != null) Modding.ModExhausts.Apply(db, index, go, exhaustBuild);
                // Remake: the hangar flights and bar flybys fly on the player's engine (its glow mesh and exhaust particles)
                // instead of the NPC flame meshes, like the NPCs in space (Settings.NpcPlayerEngines; the player's own ship
                // always): some hulls' _engine_add parts showed nothing, and the VoidX's was blue where the player's is purple.
                // The particles follow the glow, which HangarFlight / BarFlybys switch on (HangarFlight.UsesPlayerEngine).
                if (asm.playerVariantParts != null && asm.playerVariantParts.Length > 0 && asm.playerVariantParts[0] != null
                    && (Settings.NpcPlayerEngines || !asm.HasNpcExhaust || label == "Player ship"))
                {
                    var glow = asm.playerVariantParts[0];
                    // A customizable ship's exhausts where its build has them (a guest's build, the player's).
                    var build = Modding.ModShipKits.BuildOfAssembly(entry.name);
                    GoF2Remake.Flight.ShipExhaust.AttachRemote(go, db, go.transform, index, () => glow != null && glow.activeInHierarchy,
                                                               () => 0f, () => 0f, scaled: true,
                                                               mounts: build != null ? Modding.ModShipKits.MountsShown(db, index, 3, build) : null);
                }
            }
            // Remake mods: a ship's "engine" materials (its nozzles) glow only while its engine glow shows: dark parked, lit in a
            // hangar flight (EngineMaterialGlow).
            if (go != null && go.GetComponent<Modding.EngineMaterialGlow>() is Modding.EngineMaterialGlow nozzles) nozzles.followEngine = true;
            // Remake: a soft shadow on the floor under every hangar ship (not the bar's flybys); it shows once the ship rests
            // (the "Hangar ship shadows" option: off, the player's own ship only, every ship).
            if (go != null && hangarRoot != null && parent == hangarRoot) HangarShipShadow.Attach(go, entry.name, true, label == "Player ship");   // at once; a landing flight hides it (ExpectLanding)
            return go;
        }

        GameObject SpawnShip(int index, Vector3 unityPos, Quaternion unityRot, Transform parent, string label, AssemblyData assembly = null)
        {
            var go = SpawnShip(index, Vector3.zero, 0f, parent, label, assembly);
            if (go != null) go.transform.SetPositionAndRotation(unityPos, unityRot);
            return go;
        }

        /// <summary>Remake: the rooms' thin, glossy parts (the Nivelian bar stools, the Midorian window frames) shimmered as the
        /// camera swayed: SMAA works within one frame. The station camera takes URP's temporal AA instead (its slow camera
        /// and still rooms are where TAA has nothing to smear), unless a temporal upscaler (DLSS, FSR 2+, STP) already
        /// anti-aliases or MSAA is on (URP's TAA needs it off); then SMAA as before. Again when the options change.</summary>
        /// <summary>Remake mods: a customizable ship's parts arrived (loaded on demand, ModShips.EnsureBuild): the turntable shows
        /// the player's build. Also a mod ship whose model wasn't built yet when the hangar opened (a save loaded while the mods
        /// still loaded, a player's report: the Phantom stood in until the next docking): its own model now.</summary>
        void OnModShipModels()
        {
            if (this == null || playerShip == null || PlayerFlying) return;
            // The stand-in is another prefab (AssembledObject.ModScope = the prefab's name); the Mod Builder rebuilds its ship itself.
            var want = PlayerHull.Assembly(db, Session.ShipIndex);
            var shown = playerShip.GetComponent<AssembledObject>();
            bool standIn = !Modding.ModManager.BuilderOpen && want != null && want.pack == Modding.ModShips.Pack
                           && shown != null && shown.ModScope != want.name;
            if (!standIn && Modding.ModShipKits.CustomizationOf(Session.ShipIndex) == null) return;
            ReplacePlayerShip(Session.ShipIndex);
        }

        void OnDestroy()
        {
            Modding.ModShips.ModelsChanged -= OnModShipModels;
            Settings.Changed -= ApplyAntialiasing;
            if (dofProfile != null) Destroy(dofProfile);
            var copies = new HashSet<Material>();
            foreach (var (_, _, matte) in roomHighlights) foreach (var m in matte) copies.Add(m);
            foreach (var (_, lit, _) in roomHighlights) foreach (var m in lit) copies.Remove(m);
            foreach (var m in copies) if (m != null) Destroy(m);
        }

        // ---- depth of field (remake option "Hangar depth of field", Settings.HangarDepthOfField) ---------------------------

        /// <summary>The volumes' layer only the station camera listens to (the star map's and the item window's cameras,
        /// which share the scene, keep their default mask): the turntable's focus distance would blur them.</summary>
        const int DofLayer = 26;
        Volume dofVolume;
        VolumeProfile dofProfile;
        UnityEngine.Rendering.Universal.DepthOfField dof;

        /// <summary>URP's depth of field focused on the player's ship in the hangar view: Bokeh (a 300 mm lens at f/2: at the
        /// ~130 m the hangar cameras keep from the turntable the ~60 m ship stays sharp, the room behind it goes soft),
        /// Gaussian on phones (cheaper; the background only). The focus follows the ship (the camera drifts, the hangar
        /// flights move it).</summary>
        bool bokehDof;   // the hangar view's mode (Bokeh, phones Gaussian); the orbit always Gaussian (UpdateDepthOfField)

        void SetupDepthOfField()
        {
            if (mainCamera == null) return;
            var go = new GameObject("Hangar depth of field") { layer = DofLayer };
            go.transform.SetParent(transform, false);
            dofVolume = go.AddComponent<Volume>();
            dofVolume.isGlobal = true;
            dofVolume.priority = 50f;
            // A profile made in code: URP keeps the depth of field shaders in a build only because
            // Assets/Settings/HangarDepthOfFieldShaders.asset has it on (ShaderBuildPreprocessor scans the profile assets).
            dofProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            dof = dofProfile.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
            bokehDof = !Application.isMobilePlatform;
            dof.mode.Override(bokehDof ? UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh : UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian);
            dof.focalLength.Override(300f);
            dof.aperture.Override(2f);
            dof.gaussianMaxRadius.Override(1f);
            dof.highQualitySampling.Override(false);
            dofVolume.profile = dofProfile;   // an instance, not a shared profile: Bootstrap.ApplyPostProcessing leaves it alone
            dofVolume.enabled = false;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(mainCamera);
            if (data != null) data.volumeLayerMask |= 1 << DofLayer;
        }

        // The room's URP Lit materials with and without their specular highlights (CollectRoomHighlights).
        readonly List<(Renderer renderer, Material[] lit, Material[] matte)> roomHighlights = new List<(Renderer, Material[], Material[])>();
        bool roomMatte;

        /// <summary>Remake: under the hangar's depth of field the room's specular highlights are left out. The key light's
        /// highlights on the room's glossy parts (the Terran forcefield's top beam) are tiny HDR glints that pop in and out as
        /// the camera drifts a few centimetres; sharp they are a pixel's sparkle, but the blur spreads each into a bright
        /// blob, which flickered, worst where the beam meets the screen's top edge (measured with the camera stepped like its
        /// drift: 0.7-1 % of the top strip's pixels flickering, Bokeh or Gaussian, with or without DLSS; 0 with the
        /// highlights off, as with the depth of field off). Under the blur the room looks the same without them.</summary>
        void CollectRoomHighlights(GameObject room)
        {
            roomHighlights.Clear();
            roomMatte = false;
            if (room == null) return;
            var copies = new Dictionary<Material, Material>();
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                Material[] matte = null;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty("_SpecularHighlights") || m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")) continue;
                    if (!copies.TryGetValue(m, out var c))
                    {
                        c = new Material(m) { name = m.name + " (no highlights)" };
                        c.SetFloat("_SpecularHighlights", 0f);
                        c.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        copies[m] = c;
                    }
                    matte ??= (Material[])mats.Clone();
                    matte[i] = c;
                }
                if (matte != null) roomHighlights.Add((r, mats, matte));
            }
        }

        void SetRoomMatte(bool matte)
        {
            if (roomMatte == matte) return;
            roomMatte = matte;
            foreach (var (r, lit, m) in roomHighlights)
                if (r != null) r.sharedMaterials = matte ? m : lit;
        }

        void UpdateDepthOfField()
        {
            if (dofVolume == null) return;
            bool on = Settings.HangarDepthOfField && View == StationView.Hangar && playerShip != null && mainCamera != null
                      && !GoF2Remake.Data.VrFlags.Enabled && !GoF2Remake.World.SpaceHooks.StarMapOpen;
            if (dofVolume.enabled != on) dofVolume.enabled = on;
            SetRoomMatte(on);
            if (!on) return;
            float focus = Mathf.Max(1f, Vector3.Distance(mainCamera.transform.position, playerShip.position));
            // Inspect ship / the Mod Builder: focused on the ship's centre, through a wider lens (300 mm at f/2 kept only ~6 m
            // sharp up close).
            float inspect = Mathf.SmoothStep(0f, 1f, Inspect.Blend);
            if (inspect > 0f) focus = Mathf.Lerp(focus, Mathf.Max(1f, Vector3.Distance(mainCamera.transform.position, Inspect.Centre)), inspect);
            dof.focalLength.value = Mathf.Lerp(300f, 50f, inspect);
            dof.focusDistance.value = focus;
            dof.gaussianStart.value = focus + Mathf.Lerp(25f, Inspect.Radius * 1.2f + 10f, inspect);
            dof.gaussianEnd.value = focus + Mathf.Lerp(140f, Inspect.Radius * 3f + 60f, inspect);
            // Orbiting, a physical lens can't both keep the whole hull sharp and blur the room: at ~80 m a 50 mm lens blurred
            // nothing (the option seemed off in the Mod Builder), a longer one the hull's own far side. The orbit takes the
            // Gaussian blur instead, which starts past the hull (gaussianStart above) and blurs the room behind it.
            var mode = inspect > 0.5f || !bokehDof ? UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian
                                                   : UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh;
            if (dof.mode.value != mode) dof.mode.value = mode;
            dof.gaussianMaxRadius.value = Mathf.Lerp(1f, 1.5f, inspect);
        }

        void ApplyAntialiasing()
        {
            if (mainCamera == null) return;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(mainCamera);
            if (data == null) return;
            var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            bool msaa = urp != null && urp.msaaSampleCount > 1;
            data.antialiasing = (GoF2Remake.World.SpaceHooks.TemporalUpscaler != null && GoF2Remake.World.SpaceHooks.TemporalUpscaler()) || msaa
                ? UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing;
            data.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
        }

        /// <summary>Game camera rotation, order 2 (Ry * Rx * Rz, looking down local -Z) -> Unity (looking down +Z).</summary>
        static Quaternion CameraRotation(float x, float y, float z) =>
            Quaternion.Euler(-x * Mathf.Rad2Deg, -y * Mathf.Rad2Deg, z * Mathf.Rad2Deg);

        /// <summary>Remake (the Mod Builder's rooms on a game room, "gof2:hangar_terran"): the markers that room has in the
        /// game's tables, room metres as interiors.json's "markers": the pad, the camera and where it looks, the parked pads,
        /// the flights' gate; a bar's resting and first-visit cameras and its visitor spots. Null: not one of the game's rooms.</summary>
        public static Dictionary<string, Vector3> GameRoomMarkers(string assembly)
        {
            var m = new Dictionary<string, Vector3>();
            int h = System.Array.IndexOf(StationTables.HangarRoom, assembly);
            if (h >= 0)
            {
                const int probe = 10;   // the Phantom: the pads' floor is the pivot less its height
                float FloorY(int slot) => StationTables.PadPivotY(h, slot, probe, Quaternion.identity) - StationTables.ShipY(probe) * M;
                var pad = new Vector3(0f, FloorY(-1), 0f);
                m["pad"] = pad;
                var pivot = pad + Vector3.up * (StationTables.ShipY(probe) * M);
                var cam = pivot + OrbitLayout.ToUnity(StationTables.HangarCameraPos[h]);
                m["camera"] = cam;
                var r = StationTables.HangarCameraRot[h];
                m["camera_target"] = cam + CameraRotation(r.x, r.y, StationTables.HangarCameraRoll) * Vector3.forward * Vector3.Distance(cam, pivot);
                var slots = StationTables.ParkedSlots[h];
                if (slots != null)
                    for (int i = 0; i < slots.Length; i++)
                        m["parked_" + (i + 1)] = Vector3.Scale(OrbitLayout.ToUnity(slots[i]), new Vector3(1f, 0f, 1f)) + Vector3.up * FloorY(i);
                var lane = StationTables.HangarLanes[h];
                if (lane != null && !lane.bays)
                {
                    m["gate"] = lane.gate;
                    m["gate_out"] = lane.gate + lane.outward * 300f;
                }
                return m;
            }
            int b = System.Array.IndexOf(StationTables.BarRoom, assembly);
            if (b < 0) return null;
            var rest = OrbitLayout.ToUnity(StationTables.BarCameraRest[b]);
            m["camera"] = rest;
            m["camera_target"] = rest + CameraRotation(0f, StationTables.BarCameraRestYaw[b], 0f) * Vector3.forward * 60f;
            var start = OrbitLayout.ToUnity(StationTables.BarCameraStart[b]);
            m["camera_start"] = start;
            m["camera_start_target"] = start + CameraRotation(0f, StationTables.BarCameraStartYaw[b], 0f) * Vector3.forward * 60f;
            var spots = StationTables.VisitorSlots[b];
            for (int i = 0; i < spots.Length; i++) m["visitor_" + (i + 1)] = OrbitLayout.ToUnity(spots[i]);
            return m;
        }

        /// <summary>Remake: the Vossk bar's bartender arms (VosskBarBots). The first time the bar shows, they take their rest
        /// pose (the models sit at the room's origin until their animation is applied, and that is parsed only once the bar
        /// is active); then each replays when its slot comes round, the end arm as bot_a or bot_c.</summary>
        void UpdateVosskBots(float dtMs)
        {
            if (!vosskBotsPosed)
            {
                vosskBotsPosed = true;
                vosskBotA.Hold();
                vosskBotB.Hold();
            }
            var start = vosskBots.Tick(dtMs, () => Random.value);
            if ((start & VosskBarBots.Start.EndArm) != 0)
            {
                var play = vosskBots.EndArmPlaysC ? vosskBotC : vosskBotA;
                var rest = vosskBots.EndArmPlaysC ? vosskBotA : vosskBotC;
                rest.gameObject.SetActive(false);
                play.gameObject.SetActive(true);
                play.loop = false;
                play.Restart();
            }
            if ((start & VosskBarBots.Start.SecondArm) != 0) vosskBotB.Restart();
        }

        // ---- view switching ----------------------------------------------------------------------------------

        /// <summary>Instant switch, like ModStation (no fades). The lounge plays its camera intro on the first visit only.</summary>
        public void SetView(StationView view, bool force = false)
        {
            if (view == View && !force) return;
            View = view;
            if (view != StationView.Hangar) Inspect.Reset();
            hangarRoot.gameObject.SetActive(view == StationView.Hangar);
            barRoot.gameObject.SetActive(view == StationView.Lounge);
            if (view == StationView.Lounge)
            {
                introT = loungeVisited ? 1.25f : 0.75f;   // SpaceLounge::init starts at B
                introSkip = false;
                loungeVisited = true;
                ApplyLoungeLighting();
            }
            else
            {
                flingVelocity = 0f;
                ApplyHangarLighting();
                traffic?.ResumeAudio();
            }
            PlayAmbience();
            UpdateCamera(0f);
            ViewChanged?.Invoke();
        }

        void ApplyHangarLighting()
        {
            SkyReflection.SetSystem(Layout);   // the hulls' reflection: the station's system's cubemap
            if (customHangar == null) SkyReflection.SetHangar(HangarIndex);   // Remake: the hangar's own (none in the Valkyrie's)
            if (keyLight != null)
            {
                keyLight.transform.rotation = Quaternion.LookRotation(-OrbitLayout.DirToUnity(StationTables.HangarTowardLight).normalized);
                if (customHangar != null && customHangar.hasLight && (customHangar.pad - customHangar.light).sqrMagnitude > 1e-4f)
                    keyLight.transform.rotation = Quaternion.LookRotation((customHangar.pad - customHangar.light).normalized);   // its "light" marker
                keyLight.color = Color.white;
                // No URP shadows on the ships (the original has none; HangarShipShadow gives their contact shadows): within
                // the 50 m shadow distance Inspect ship's close-ups showed the hull shading itself (a player's report).
                keyLight.shadows = LightShadows.None;
                keyLight.intensity = customHangar != null && customHangar.def.lightIntensity >= 0f ? customHangar.def.lightIntensity : hangarLightIntensity;
            }
            var amb = (StationTables.HangarAmbient(Layout.raceId) + new Color(0.2f, 0.2f, 0.2f)) * 0.7f * hangarAmbientScale;
            if (customHangar?.def.ambient is Color customAmb) amb = customAmb;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(amb.r, amb.g, amb.b);
            if (customHangar != null) SetCustomFog(customHangar.def);
            else SetFog(HangarIndex == 1, StationTables.HangarFogEnd);
        }

        void ApplyLoungeLighting()
        {
            SkyReflection.SetSystem(Layout);
            OrbitBuilder.SetupLights(Layout, keyLight, null, sunIntensityAt2, sunShadows: LightShadows.Soft);   // the room's own shadows
            RenderSettings.ambientMode = AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();
            if (customBar != null)
            {
                // A custom bar: its "light" marker shines toward the resting view's target; its own ambient and fog.
                if (keyLight != null && customBar.hasLight && (customBar.cameraTarget - customBar.light).sqrMagnitude > 1e-4f)
                    keyLight.transform.rotation = Quaternion.LookRotation((customBar.cameraTarget - customBar.light).normalized);
                if (keyLight != null && customBar.def.lightIntensity >= 0f) keyLight.intensity = customBar.def.lightIntensity;
                if (customBar.def.ambient is Color a) { RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = a; }
                SetCustomFog(customBar.def);
                return;
            }
            SetFog(BarRace == 1, StationTables.BarFogEnd);
        }

        /// <summary>A custom room's fog ("fog": colour and end in metres), else none.</summary>
        static void SetCustomFog(Modding.ModInteriors.Def d)
        {
            bool on = d.fogColor.HasValue && d.fogEnd > 0f;
            GoF2Remake.World.SpaceHooks.SceneFog(on);
            if (!on) return;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.fogEndDistance = d.fogEnd;
            RenderSettings.fogColor = d.fogColor.Value;
        }

        static void SetFog(bool on, float end)
        {
            GoF2Remake.World.SpaceHooks.SceneFog(on);   // off below Quality High
            if (!on) return;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.fogEndDistance = end * M;
            RenderSettings.fogColor = StationTables.VosskFog;
        }

        // ---- turntable (ModStation::OnTouchMove 0xea340 / OnTouchEnd / OnUpdate) ----------------------------

        /// <summary>Turns the player's ship by 'gameRadians' (the original: +0xe0 += dx px, yaw = +0xe0 / 120).</summary>
        public void RotateShip(float gameRadians)
        {
            if (PlayerFlying) return;
            flingVelocity = 0f;
            shipYaw += gameRadians;
            ApplyShipYaw();
        }

        /// <summary>Lets the ship keep turning after a drag; slows by x0.9 per 20 ms frame (normalised).</summary>
        public void FlingShip(float gameRadiansPerSecond) => flingVelocity = PlayerFlying ? 0f : gameRadiansPerSecond;

        void ApplyShipYaw()
        {
            if (playerShip == null || PlayerFlying) return;
            playerShip.rotation = OrbitLayout.RotationToUnity(new Vector3(0f, shipYaw, 0f));
            // Modified: a hull off its pivot stands centred on the pad and turns about its own middle (CentreOnPad).
            if (padCentre != Vector3.zero) playerShip.position = padPivot - playerShip.TransformVector(padCentre);
        }

        /// <summary>Modified: the VoidX (ship 8): its hull's middle sits ~18 m off its pivot along its length (the model's centre
        /// at -355 units, the other hulls' within ~85), so on the pad it stood off the turntable and swung around it.</summary>
        public const int OffCentreShip = 8;
        /// <summary>The pad pivot the player's ship was spawned at, and its hull's middle in its own frame (horizontal; zero for
        /// the hulls that stand on their pivot).</summary>
        Vector3 padPivot, padCentre;

        /// <summary>Modified: the player's ship centred on its pad (ApplyShipYaw keeps it so as it turns). The arrival flight
        /// lands on the centred spot (it flies to where the ship stands).</summary>
        void CentreOnPad()
        {
            padCentre = Vector3.zero;
            if (playerShip == null) return;
            padPivot = playerShip.position;
            if (shipIndex != OffCentreShip) return;
            var local = ShipLocalBounds(playerShip, false);
            if (local.size != Vector3.zero) padCentre = new Vector3(local.center.x, 0f, local.center.z);
        }

        // ---- remake: Inspect ship (HangarInspect; UI.StationMenu reads the input) ----------------------------

        /// <summary>The hangar's orbit camera around the player's ship.</summary>
        public HangarInspect Inspect { get; } = new HangarInspect();

        /// <summary>Starts inspecting the player's ship: only in the hangar, with the ship on its pad (not flying), not in VR.</summary>
        public bool BeginInspect() => BeginInspect(1.15f);

        /// <summary>Inspect ship's orbit, 'minRadii' ship radii at the closest (the Mod Builder's camera goes nearer).</summary>
        public bool BeginInspect(float minRadii)
        {
            if (View != StationView.Hangar || playerShip == null || PlayerFlying || GoF2Remake.Data.VrFlags.Enabled || mainCamera == null) return false;
            // The hull's box in the ship's own frame (a world box around a ship turned 45 deg is ~1.3x too big), without the
            // additive glow layers, unless there is nothing else.
            var local = ShipLocalBounds(playerShip, false);
            if (local.size == Vector3.zero) local = ShipLocalBounds(playerShip, true);
            if (local.size == Vector3.zero) local = new Bounds(Vector3.zero, Vector3.one * 20f);
            var scale = playerShip.lossyScale;
            var world = new Bounds(playerShip.TransformPoint(local.center), Vector3.Scale(local.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            flingVelocity = 0f;
            Inspect.Begin(mainCamera.transform.position, world, minRadii);   // its extents' length is the hull's radius
            return true;
        }

        public void EndInspect() => Inspect.End();

        /// <summary>The meshes' bounds in 'ship''s own space (the shown ones; 'withAdditive' takes the glow layers too).</summary>
        static Bounds ShipLocalBounds(Transform ship, bool withAdditive)
        {
            bool any = false;
            var b = new Bounds();
            var toShip = ship.worldToLocalMatrix;
            foreach (var mf in ship.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled || (!withAdditive && mf.name.Contains("_add"))) continue;
                var lb = mf.sharedMesh.bounds;
                var m = toShip * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>SpaceLounge::OnTouchEnd case 0 jumps to B; remake: the rest of the ease runs at a 0.7 s pace instead, so the
        /// camera doesn't snap.</summary>
        public void SkipIntro()
        {
            if (IntroPlaying) introSkip = true;
        }

        bool introSkip;

        /// <summary>ModStation::leaveStation 0xec1ec after the "Depart the station?" confirmation: into space (after the
        /// remake's take-off).</summary>
        public void Launch()
        {
            // ModStation::OnTouchEnd, leaving at campaign 0x18 from Thynome (10): the AB-1 Retractor seller of index 23 has
            // moved on (Agent::setEvent(1) + setOfferAccepted: 858 "I don't have anything left, sorry."), as step 23's Info
            // page 192 warns.
            if (!Session.FreePlay && Story.Index == 24 && Layout.stationIndex == 10 && Stock?.agents != null)
                foreach (var a in Stock.agents)
                    if (a.offer == AgentOffer.SellItem && a.sellItem == 68) a.accepted = a.known = true;   // known: no first-chat offer
            Depart(LaunchNow);
        }

        /// <summary>Remake: the player's ship takes off and flies out through the forcefield, then 'then' runs (loads the
        /// next scene); straight away with the flights off. It lifts off at once and holds over its pad while another
        /// ship flies (HangarTraffic: one flight at a time).</summary>
        public void Depart(Action then)
        {
            if (PlayerFlying) return;
            var lane = Lane;
            if (!Settings.HangarFlights || lane == null || playerShip == null) { then?.Invoke(); return; }
            SetView(StationView.Hangar);
            // The camera eases back to the hangar's for the take-off first: the flight works out where the ship leaves the
            // view (HangarFlight's shrink) from the camera as it starts, and from the orbit's close-up (the Mod Builder's
            // camera) it shrank away before it reached the forcefield.
            if (Inspect.Shown)
            {
                Inspect.End();
                pendingDeparture = then;
                return;
            }
            flingVelocity = 0f;
            afterDeparture = then;
            var engine = HangarFlight.AddEngine(playerShip.gameObject, true, db, shipIndex, out float volume);
            playerFlight = HangarFlight.Departure(playerShip, lane, engine, volume);
            playerFlightStart = Time.unscaledTime;
            playerFlight.Hold = traffic != null && traffic.Busy;
        }

        /// <summary>A tap / key during the player's flight: landed at once, or gone. Not in its first 0.4 s (the key that
        /// confirmed the launch).</summary>
        public void SkipPlayerFlight()
        {
            if (playerFlight != null && Time.unscaledTime - playerFlightStart > 0.4f) playerFlight.Skip();
        }

        float playerFlightStart;
        /// <summary>A take-off waiting for the camera's blend back from the orbit (Depart).</summary>
        Action pendingDeparture;

        void LaunchNow()
        {
            Session.LastDepartureTime = Time.realtimeSinceStartup;   // Status+0x70, for computerTradeGoods
            Session.ArrivedByTravel = false;
            Session.LaunchedFromStation = true;
            int storyOrbit = Story.LaunchStation;
            if (storyOrbit >= 0)
            {
                // Index 48: departStation(58) + initStreamOutPosition, arriving in B'akrram's orbit (Taret Orskk flies).
                Session.PreviousStationIndex = Session.StationIndex;
                Session.StationIndex = storyOrbit;
                Session.ArrivedByTravel = true;
                Session.LaunchedFromStation = false;
            }
            if (Application.CanStreamedLevelBeLoaded(spaceScene)) SceneManager.LoadScene(spaceScene);
        }

        // ---- per frame ---------------------------------------------------------------------------------------

        void Update()
        {
            float dtMs = Time.deltaTime * 1000f;
            if (pendingDeparture != null && !Inspect.Shown)
            {
                var then = pendingDeparture;
                pendingDeparture = null;
                Depart(then);
            }
            if (playerFlight != null)
            {
                playerFlight.Hold = traffic != null && traffic.Busy;
                playerFlight.Update(Time.deltaTime);
                if (playerFlight.Done)
                {
                    bool arrived = playerFlight.arriving;
                    playerFlight = null;
                    if (arrived) ApplyShipYaw();
                    else
                    {
                        departed = true;   // keeps the menu hidden until the scene changes
                        var then = afterDeparture;
                        afterDeparture = null;
                        then?.Invoke();
                    }
                }
            }
            traffic?.Update(dtMs, playerFlight != null || departed);
            if (View == StationView.Hangar && flingVelocity != 0f)
            {
                shipYaw += flingVelocity * Time.deltaTime;
                // Modified: a fast flick keeps the ship turning and slows it gently (to 35 % a second; the original's x0.9 per
                // 20 ms frame stopped it within half a second), down to a crawl before it stops (it snapped still at 1 px a
                // frame, ~24 deg/s). How much of the speed is left after a second, and the speed it stops at (rad/s):
                const float FlingKeptPerSecond = 0.35f, FlingStopSpeed = 0.03f;
                flingVelocity *= Mathf.Pow(FlingKeptPerSecond, dtMs / 1000f);
                if (Mathf.Abs(flingVelocity) < FlingStopSpeed) flingVelocity = 0f;
                ApplyShipYaw();
            }

            if (View == StationView.Lounge && midorianProp != null && (midorianTimer += dtMs) > 2000f)
            {
                midorianTimer = 0f;
                if (Random.value < 0.3f) midorianProp.Restart();
            }
            if (View == StationView.Lounge && vosskBots != null) UpdateVosskBots(dtMs);

            if (View == StationView.Hangar) RefreshTurret(false);
            if (musicSource != null) musicSource.volume = Settings.MusicVolume * GoF2Remake.World.SpaceHooks.SceneMusic;
            float atmoMs = Time.unscaledDeltaTime * 1000f;
            mainViewAtmo?.Update(atmoMs); loungeAtmo?.Update(atmoMs); hangarAtmo?.Update(atmoMs);
        }

        void LateUpdate()
        {
            UpdateCamera(Time.deltaTime * 1000f);
            UpdateDepthOfField();
            if (View == StationView.Lounge) UpdateBillboards();
        }

        // ---- remake: the Mod Builder's free camera (its rooms' editor) ----

        /// <summary>The rooms as built (the hangar's and the Space Lounge's), for the Mod Builder.</summary>
        public Transform HangarRoom { get; private set; }
        public Transform BarRoom { get; private set; }
        /// <summary>The custom room shown (a mod's or the builder's preview), null = the race's.</summary>
        public Modding.ModInteriors.Room CustomHangar => customHangar;
        public Modding.ModInteriors.Room CustomBar => customBar;

        /// <summary>The Mod Builder holds the camera (its room editor's orbit): this pose and lens instead of the room's.</summary>
        public bool FreeCamera { get; set; }
        public Pose FreeCameraPose { get; set; }
        public float FreeCameraFov { get; set; } = 50f;

        void UpdateCamera(float dtMs)
        {
            if (mainCamera == null) return;
            var cam = mainCamera.transform;
            if (FreeCamera)
            {
                cam.SetPositionAndRotation(FreeCameraPose.position, FreeCameraPose.rotation);
                mainCamera.fieldOfView = Aspect.VerticalFov(FreeCameraFov, mainCamera.aspect);
                mainCamera.nearClipPlane = 0.5f;
                mainCamera.farClipPlane = 20000f;
                return;
            }
            if (GoF2Remake.Data.VrFlags.Enabled) { VrCamera(cam); return; }
            if (View == StationView.Hangar)
            {
                // ModStation::OnUpdate: each axis eases to a new target on the other side of the base when within 5 units.
                var p = Vector3.zero;
                for (int i = 0; i < 3; i++)
                {
                    drift[i].Increase(dtMs);
                    if (Mathf.Abs(drift[i].Value - drift[i].Target) < 5f) NextDriftLeg(i, drift[i].Value);
                    p[i] = drift[i].Value;
                }
                if (customHangar != null)
                {
                    // A custom hangar: its "camera" marker, drifting a few metres, looking at "camera_target" (else the pad).
                    var at = customHangar.camera + OrbitLayout.ToUnity(p);
                    cam.SetPositionAndRotation(at, Quaternion.LookRotation(customHangar.cameraTarget - at, Vector3.up));
                    SetCustomLens(customHangar.def, StationTables.HangarFov, StationTables.HangarNear, StationTables.HangarFar, false);
                }
                else
                {
                    var r = StationTables.HangarCameraRot[HangarIndex];
                    cam.SetPositionAndRotation(shipPivot + OrbitLayout.ToUnity(p), CameraRotation(r.x, r.y, StationTables.HangarCameraRoll));
                    SetLens(StationTables.HangarFov, StationTables.HangarNear, StationTables.HangarFar, false);
                }
                // Remake: Inspect ship, from the hangar pose just set to the orbit around the ship (and back).
                if (Inspect.Shown || Inspect.Active)
                {
                    Inspect.Tick(Time.unscaledDeltaTime * 1000f);
                    mainCamera.nearClipPlane = Inspect.Apply(cam, cam.position, cam.rotation, mainCamera.nearClipPlane);
                    // The photo tools' field of view (UI.PhotoTools), eased in with the orbit.
                    if (Inspect.FovDeg > 0f) mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, Inspect.FovDeg, Mathf.SmoothStep(0f, 1f, Inspect.Blend));
                    // A panel over part of the screen (the Customize screen): the view turns aside so the ship stands in the
                    // middle of the free part (InspectFrameX). The turn comes and goes with the orbit's own eased blend, so
                    // the Customize screen moves in and out like Inspect ship (a separate linear ease times the raw blend
                    // started and stopped abruptly, and the target reset on leaving swung the view back before the orbit
                    // did); a change of the panel's width once in eases over InspectFrameMs.
                    if (!Inspect.Active || Inspect.Blend < 1f) inspectFrame = InspectFrameX;
                    else inspectFrame = Mathf.MoveTowards(inspectFrame, InspectFrameX, Time.unscaledDeltaTime * 1000f / InspectFrameMs);
                    float off = (inspectFrame - 0.5f) * Mathf.SmoothStep(0f, 1f, Inspect.Blend);
                    if (Mathf.Abs(off) > 0.0005f)
                    {
                        float halfTanX = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * mainCamera.aspect;
                        cam.rotation *= Quaternion.AngleAxis(-Mathf.Atan(halfTanX * 2f * off) * Mathf.Rad2Deg, Vector3.up);
                    }
                }
                else inspectFrame = InspectFrameX = 0.5f;   // the blend back has ended: the frame back in the middle
            }
            else
            {
                Vector3 pos;
                Quaternion rot;
                if (introT < 1.25f)
                {
                    // EaseInOutMatrix(A, B, 3000): t 0.75 -> 1.25, blend sin(2 pi t) * 0.5 + 0.5; Increase(min(dt, 50)).
                    float introMs = introSkip ? 700f : StationTables.BarIntroMs;
                    introT = Mathf.Min(1.25f, introT + Mathf.Min(dtMs, 50f) * 0.5f / introMs);
                    if (introT >= 1.25f) introSkip = false;
                    float b = Mathf.Sin(TwoPi * introT) * 0.5f + 0.5f;
                    pos = Vector3.Lerp(barPosA, barPosB, b);
                    rot = Quaternion.Slerp(barRotA, barRotB, b);
                }
                else
                {
                    // Sway: [RotY(v / 35), translation (0, bob, 0)] * B, v easing between -4..5 at speed 1..4.
                    swayYaw.Increase(dtMs * swaySpeed);
                    if (Mathf.Abs(swayYaw.Value - swayYaw.Target) < 0.25f)
                    {
                        swayYaw.Start(swayYaw.Value, 5 - Random.Range(0, 10));
                        swaySpeed = 1 + Random.Range(0, 4);
                    }
                    // Phase step 0.05..0.12 per 20 ms frame in the original (normalised); bob at most 3.5 units.
                    bobPhase += Mathf.Clamp(dtMs * 0.0025f, 0f, 0.12f);
                    var yaw = Quaternion.Euler(0f, -swayYaw.Value / 35f * Mathf.Rad2Deg, 0f);
                    // The original turns the view about the room's origin; a custom bar about its own camera spot.
                    pos = (customBar != null ? barPosB : yaw * barPosB) + new Vector3(0f, Mathf.Sin(bobPhase) * 3.5f * M, 0f);
                    rot = yaw * barRotB;
                }
                // Remake: talking to a visitor, the view turns to them and zooms in a little (FocusVisitor), easing back
                // when the chat closes. Only sideways, keeping the room camera's pitch and roll: they stand beside the chat
                // window, in the middle of the room left of it (ChatFrameX, from LoungePanel; #82).
                chatZoom = Mathf.MoveTowards(chatZoom, chatVisitor >= 0 ? 1f : 0f, dtMs / ChatZoomMs);
                float z = chatZoom * chatZoom * (3f - 2f * chatZoom);   // smoothstep
                if (customBar != null) SetCustomLens(customBar.def, StationTables.BarFov, StationTables.BarNear, StationTables.BarFar);
                else SetLens(StationTables.BarFov, StationTables.BarNear, StationTables.BarFar);
                if (z > 0f) mainCamera.fieldOfView *= 1f - ChatZoomIn * z;
                if (z > 0f && chatTarget != Vector3.zero)
                {
                    var toVisitor = chatTarget - pos;
                    toVisitor.y = 0f;
                    if (toVisitor.sqrMagnitude > 0.01f)
                    {
                        // Turn past the visitor by the angle that puts them ChatFrameX of the width from the left.
                        float halfTanX = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * mainCamera.aspect;
                        float past = Mathf.Atan(halfTanX * (1f - 2f * ChatFrameX)) * Mathf.Rad2Deg;
                        var e = rot.eulerAngles;
                        float yaw = Mathf.Atan2(toVisitor.x, toVisitor.z) * Mathf.Rad2Deg + past;
                        rot = Quaternion.Euler(e.x, Mathf.LerpAngle(e.y, yaw, z), e.z);
                    }
                }
                cam.SetPositionAndRotation(pos, rot);
            }
        }

        /// <summary>Remake: where the Inspect ship orbit puts the ship across the screen, from the left (0) to the right (1);
        /// 0.5 = the middle. The Customize screen sets it to the middle of the part its panel leaves free.</summary>
        public float InspectFrameX = 0.5f;
        float inspectFrame = 0.5f;
        const float InspectFrameMs = 450f;

        /// <summary>Remake: the bar camera's zoom toward the visitor talked to (the share of the field of view taken off), eased
        /// over ChatZoomMs.</summary>
        const float ChatZoomIn = 0.25f, ChatZoomMs = 800f;
        /// <summary>Where the visitor talked to stands across the screen, from the left (0) to the right (1): the middle of
        /// the room beside the chat window, left of it or (the window on the left) right of it (LoungePanel sets it from the
        /// window's place).</summary>
        public float ChatFrameX = 0.24f;
        int chatVisitor = -1;
        Vector3 chatTarget;
        float chatZoom;

        /// <summary>The Space Lounge's chat opened with visitor 'index' (LoungePanel): the camera eases toward them; -1 = the
        /// chat closed, back to the room's view.</summary>
        public void FocusVisitor(int index)
        {
            chatVisitor = index >= 0 && index < visitors.Count ? index : -1;
            if (chatVisitor >= 0) chatTarget = visitors[chatVisitor].feet + Vector3.up * visitors[chatVisitor].height * 0.5f;   // the figure's middle
        }

        /// <summary>VR: standing in the room, still (no drift, sway or intro). The hangar: on the floor beside the pad, between
        /// the original camera's spot and the ship, facing it, the eyes 1.7 m above the hull's bottom (the pad); the bar: the
        /// original's view point (B), level.</summary>
        void VrCamera(Transform cam)
        {
            if (View == StationView.Hangar)
            {
                var ship = playerShip;
                var bounds = new Bounds(shipPivot, Vector3.one);
                if (ship != null && !PlayerFlying)
                {
                    bool any = false;
                    foreach (var r in ship.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                    }
                    vrShipBounds = bounds;
                }
                else if (vrShipBounds.size != Vector3.zero) bounds = vrShipBounds;
                var original = customHangar != null ? customHangar.camera : shipPivot + OrbitLayout.ToUnity(hangarCamBase);
                var away = original - bounds.center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                away.Normalize();
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                var pos = bounds.center + away * (radius + 5f);
                pos.y = bounds.min.y + 1.7f;
                cam.SetPositionAndRotation(pos, Quaternion.LookRotation(-away, Vector3.up));
                SetLens(StationTables.HangarFov, StationTables.HangarNear, StationTables.HangarFar, false);
            }
            else
            {
                introT = 1.25f;   // no intro ease
                cam.SetPositionAndRotation(barPosB, Quaternion.Euler(0f, barRotB.eulerAngles.y, 0f));
                SetLens(StationTables.BarFov, StationTables.BarNear, StationTables.BarFar);
            }
        }

        Bounds vrShipBounds;

        float Base(int axis) => hangarCamBase[axis];

        void NextDriftLeg(int axis, float from)
        {
            driftSign[axis] = !driftSign[axis];
            int min = axis == 0 ? 18 : axis == 1 ? 30 : 50, range = axis == 0 ? 131 : axis == 1 ? 120 : 100;
            float offset = (min + Random.Range(0, range)) * (driftSign[axis] ? 1f : -1f);
            drift[axis].Start(from, Base(axis) + offset);
        }

        /// <summary>A custom room's lens: "fov" (vertical degrees), "near" / "far" (metres); the original's otherwise.</summary>
        void SetCustomLens(Modding.ModInteriors.Def d, float fovRad, float near, float far, bool keepWidth = true)
        {
            SetLens(d.fov > 0f ? d.fov * Mathf.Deg2Rad : fovRad, d.near > 0f ? d.near / M : near, d.far > 0f ? d.far / M : far, keepWidth);
        }

        /// <summary>'keepWidth': wider screens keep the 16:9 width (the bars end outside it); the hangars use the original
        /// Hor+ view like the flight cameras (a new hangar model is being made, so their cut-off edges are left for it).</summary>
        void SetLens(float fovRad, float near, float far, bool keepWidth = true)
        {
            mainCamera.fieldOfView = keepWidth ? Aspect.VerticalFovKeepWidth(fovRad * Mathf.Rad2Deg, mainCamera.aspect)
                                               : Aspect.VerticalFov(fovRad * Mathf.Rad2Deg, mainCamera.aspect);
            mainCamera.nearClipPlane = near * M;
            mainCamera.farClipPlane = far * M;
        }

        /// <summary>SpaceLounge::updateScreenPositions: visitors and glows face the camera (MatrixGetLookAt, local +Z toward
        /// the camera, with the camera's up); the glow sits 100 units behind; Terran bars turn visitors a further 180 deg.</summary>
        void UpdateBillboards()
        {
            // VR: they face the head (the headset's camera), upright.
            var eye = SpaceHooks.VrEye?.Invoke();
            var cam = eye != null ? eye.transform : mainCamera.transform;
            foreach (var v in visitors)
            {
                var toCam = cam.position - v.feet;
                if (toCam.sqrMagnitude < 1e-6f) continue;
                var look = Quaternion.LookRotation(toCam.normalized, eye != null ? Vector3.up : cam.up);
                if (v.body != null) v.body.rotation = BarRace == 0 ? look * Quaternion.Euler(0f, 180f, 0f) : look;
                if (v.glow != null)
                {
                    var z = look * Vector3.forward;
                    v.glow.SetPositionAndRotation(v.feet - new Vector3(z.x, 0f, z.z) * (100f * M), look);
                }
            }
        }

        // ---- audio -------------------------------------------------------------------------------------------

        /// <summary>The screen's ambience event (the FEV's LGCY data, CycleSound): 122 Station_Atmo_Mainview on the main view,
        /// 95 Station_Atmo_Hangar while the hangar window is open (ModStation::OnKeyPress stops 122 and plays 95), 108
        /// Station_Atmo_Lounge in the lounge.</summary>
        void PlayAmbience()
        {
            if (mainViewAtmo == null)
            {
                mainViewAtmo = CycleSound.MainView(gameObject, mainViewAmbience, mainViewAdds);
                hangarAtmo = CycleSound.Hangar(gameObject, hangarAmbience, hangarAdds);
                // The two lounge add definitions: every Add_* wave but Add_1 (Add_1) / but Add_11 (Add_2).
                var adds = loungeAdds ?? new AudioClip[0];
                loungeAtmo = CycleSound.Lounge(gameObject, loungeAmbience,
                                               System.Array.FindAll(adds, c => c != null && c.name != "Station_Atmo_Lounge_Add_1"),
                                               System.Array.FindAll(adds, c => c != null && c.name != "Station_Atmo_Lounge_Add_11"));
            }
            var want = View == StationView.Lounge ? loungeAtmo : hangarWindowOpen ? hangarAtmo : mainViewAtmo;
            foreach (var a in new[] { mainViewAtmo, loungeAtmo, hangarAtmo }) if (a != want && a.IsPlaying) a.Stop();
            if (!want.IsPlaying) want.Play();
        }

        /// <summary>The hangar window opened / closed: its own ambience (95) instead of the main view's (122).</summary>
        public void SetHangarWindowOpen(bool open)
        {
            if (hangarWindowOpen == open) return;
            hangarWindowOpen = open;
            PlayAmbience();
        }
    }
}
