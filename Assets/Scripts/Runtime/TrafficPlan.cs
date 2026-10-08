// TrafficPlan.cs
// Which NPC ships an orbit gets (Level::createMission 0xbda70, empty-mission branch; Reference/research/
// npc_traffic_ai.md 2.2-2.4), as plain C# spawn specs in game units. Random per visit (the original reseeds its
// java.util.Random with time(NULL)), so UnityEngine.Random.
//   raiders    chance 90 / 65 / 35 / 10 % by security (one lower on Extreme); count rnd(4) (Extreme rnd(6) + 2, x2)
//              + rank / 4; 75 % pirates else the system's enemy race; one ship model for the group; around
//              (rnd +-50000, 0, 50000..100000) +-20000
//   jumpers    rnd(2) system-race fighters, created dead; the level relaunches them from the station (Traffic)
//   freighters rnd(5) at (+-(20000..80000), +-20000, +-80000), flying game +Z; none at Var Hastra (78)
//   local      security + rnd(2) + freighters / 4 system-race fighters around (+-10000, +-10000, 20000..50000) +-20000;
//              at least 7 at a station whose race the player attacked; 4 when nothing else spawned
//   special    stations 100, 101, 108, 10: nothing; 102-104: rnd(5) + 3 pirates; Loma (black market): rnd(4) + 6
//              pirates; systems 32 / 33 (pirate loot orbits): rnd(4) + 10 pirates
// Not yet: Wanted targets and their wingmen, the Terran battleship / carrier and Vossk battleship specials with turrets,
// pirate outposts, late-campaign Specters, freelance missions, the Void / alien orbit.

using System.Collections.Generic;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Flight
{
    public enum NpcGroup { Local, Jumper, Freighter, Raider, Wingman, Escort, Special, Turret, Guard, Outpost }

    public class SpawnSpec
    {
        public NpcGroup group;
        public int race, ship;
        public bool freighter;
        public Vector3 position;   // game units
        public Route route;    // null = the default patrol
        public bool startsDead;    // jumpers
        // Campaign levels (Level::createCampaignMission, campaign_levels_a.md 1.2):
        public bool asleep;        // setToSleep: waits until the player comes within its detect range (NpcShip.UpdateSleep)
        public bool inactive;      // setInitActive(false): waits until the level script wakes it
        public bool alwaysEnemy, alwaysFriend;
        public bool alwaysNeutral; // remake (/spawn, the debug spawner): neither hostile nor friendly, whatever the standing
        public int eventTag;       // remake multiplayer: the event batch that spawned it (EventRunner counts them), 0 = none
        public int hitpoints = -1; // Player::setHitpoints / setMaxHitpoints override (-1 = the createShip formula)
        public bool noLoot;        // KIPlayer+0x4c / +0x48 = 0: no cargo, no crate
        public int nameText = -1;  // KIPlayer+0x18: the name the lock plate shows (text id)
        public string name;        // KIPlayer+0x18 as a literal (freelance: the agent's / the Wanted target's name)
        // Freelance levels (Level::createMission 0xbda70, freelance_missions.md 4.1):
        public bool stationary;    // KIPlayer+0x48 = 0: parked (Protection's mining ships), still a target
        public float speed = -1f;  // setSpeed (u/ms, -1 = the normal 2.0 with boosts): 3.0 for the Challenge rival / Wanted
        public int missionCrate = -1; // PlayerFighter::setMissionCrate: carries only this item; EMP-disabling it drops the crate
        // Static objects (Level::createStaticObject 0xcda54, PlayerFixedObject): the Kaamo siege's Pirate Outposts.
        public string fixedObject;    // assembly name: a target that never moves, has no gun, no engine and no loot
        public int collisionId = -1;  // Level::getBoundingVolume id (collision.json below 2000, else static_collisions.json)
        public float hitRadius = -1f; // Player+0x40, the bullet hit cube's half size (units)
        public GameObject wreckPrefab; // setWreckedMeshId: the wreck animation played on death, then the explosion
        // PlayerFixedObject::setDeadButSelectable 0x180248 (the Supernova wrecks with a hidden blueprint): a fixedObject
        // freighter ('ship' 13 / 15 of 'race') shown as its wreck held at the animation's end, invulnerable, still lockable.
        public bool deadButSelectable;
        public float explosionScale = 1f;
        // Capital-ship turrets (PlayerTurret, npc_combat_specials.md 1): a static turret object.
        public string turretAssembly;  // turret_002_static (Terran) / turret_003_static (Vossk)
        public Vector3 rotation;       // game Euler (radians, Rx*Ry*Rz) of the root
        public float scale = 1f;       // setScaling (turrets 6)
        public bool guard;             // PlayerFighter+0x12b: waking calls Level::pirateStationAction(true)
        public int lootItem = -1, lootAmount;   // a fixed crate (the pirate outposts, DAT_002543e0)
        // Campaign step 59's arms convoy (Level::createMission 0xbe742): its freighter, escorts and turrets (Traffic scripts it).
        public int convoyRole;         // SpawnSpec.ConvoyFreighter / ConvoyEscort / ConvoyTurret, 0 = none
        public const int ConvoyFreighter = 1, ConvoyEscort = 2, ConvoyTurret = 3;
        // Supernova story objects (PlayerFixedObject::setDockingType, KIPlayer::setSpacePoints, KIPlayer+0x70):
        public int dockingType;        // 0 none, 1 drop-off, 2 pick-up, 3 hackable (ObjectDocking)
        public int spacePoints = -1;   // the SpacePoints set (docks_hd.json)
        public bool radarHidden;       // KIPlayer+0x70: no marker, no lock
        // The Most Wanted criminal (KIPlayer+0x3e / +0x44) and its escort (wingmen_wanted.md 2.6).
        public int wantedIndex = -1;
        public int wantedEscortOf = -1;
        public int gunItem = -1;       // Level::assignGuns: the wanted's own weapon ...
        public float gunFactor = 1f;   // ... at x4
        public int secondaryItem = -1;     // a second gun slot (the wanted flying ships 45-48: G'liissk rockets) ...
        public float secondaryFactor = 1f; // ... at x4
        public int hiddenBlueprint = -1;   // a Supernova wreck's hidden blueprint (TrafficPlan.HiddenBlueprints slot)
        public int modBlueprint = -1;      // remake mods: a derelict's blueprint (the product item; blueprints.json "derelict")
        public int pirateEvent;            // remake: EventOutpost / EventBoss / EventTurret (TrafficPlan.AddPirateEvent), 0 = none
        public bool eventDangerous;        // ... in a Dangerous system: the bigger bounty (Traffic.PirateEventDone)
        public const int EventOutpost = 1, EventBoss = 2, EventTurret = 3;
        // Remake (Settings.CapitalShips, World.CapitalShips): the capital ship enhancements.
        public int capital;                // the host: CapitalBattleship / CapitalCarrier / CapitalVossk, 0 = none
        public bool capitalPart;           // one of its turrets or escorts (its turrets go with it: Traffic.DestroyCapitalTurrets)
        public SpawnSpec capitalHost;      // ... and whose
        public bool capitalEnhanced;       // built with the option on (hit boxes, CapitalShip on the host)
        public bool fleetBattle;           // a fleet battle's capital ship (TrafficPlan.AddFleetBattle) ...
        public SpawnSpec battleFoe;        // ... and the enemy capital ship it closes in on
        public float deathMs = -1f;        // a fixed object's dying time before the big explosion (-1 = its wreck animation or 20 s)
        public int empPoints = -1;         // the EMP pool (-1 = NpcTables.Emp)
        public const int CapitalBattleship = 1, CapitalCarrier = 2, CapitalVossk = 3;
    }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class TrafficPlan
    {
        /// <summary>Station::stationHasHiddenBlueprint 0xb3ec8 (blueprints_mods.md 1.4 3): per slot the station
        /// (DAT_0025273c), the blueprint (DAT_002521f0), the wreck's race (DAT_00253754), position (DAT_00253768) and docking
        /// point set (DAT_002537a4: loadSpacePoints 11-14, one per race's freighter wreck).</summary>
        public static readonly (int station, int blueprint, int race, Vector3 position, int points)[] HiddenBlueprints =
        {
            (132, 226, 1, new Vector3(-20000, 30000, 80000), 14), (133, 221, 3, new Vector3(40000, -30000, 100000), 11),
            (134, 223, 2, new Vector3(-80000, 80000, -90000), 12), (129, 225, 0, new Vector3(40000, 20000, 140000), 13),
            (123, 227, 2, new Vector3(40000, 20000, 140000), 12),
        };

        static Vector3 Jitter() => new Vector3(Random.Range(0, 40000) - 20000, Random.Range(0, 40000) - 20000, Random.Range(0, 40000) - 20000);

        /// <summary>'playerGame': the player's start (game units), where freelance escorts gather.</summary>
        /// <param name="wormholeGame">The orbit's visible wormhole (game units), where a Void raid comes from; null = none.</param>
        /// <summary>Level::createMission's Informer layout: an Informer mission at its target station while it isn't spoiled
        /// (status[0xf1] == 0; a spoiled one gets the normal traffic).</summary>
        public static bool InformerOrbit(int station)
        {
            var fm = Session.FreelanceMission;
            return fm != null && fm.type == MissionType.Informer && fm.target == station && !Session.InformerFailed;
        }

        public static List<SpawnSpec> Build(Database db, int station, SystemData system, Vector3 playerGame = default, Vector3? wormholeGame = null)
        {
            var list = new List<SpawnSpec>();
            // The alien orbit (npc_traffic_ai.md 2.1): 2 Void fighters, more with the player's level (2 at index 0x21 / 0x44),
            // anywhere in the orbit, always enemy; nothing else.
            if (station == Session.VoidOrbit)
            {
                int n = 2, more = Session.Rank / 2 - 1 + Random.Range(0, 2);
                if (more >= 2 && Session.CampaignMission != 0x21 && Session.CampaignMission != 0x44) n = more;
                for (int i = 0; i < n; i++)
                    list.Add(new SpawnSpec { group = NpcGroup.Raider, race = Standing.Void, ship = NpcTables.RandomFighter(Standing.Void), alwaysEnemy = true,
                                             position = new Vector3(Random.Range(0, 120000) - 60000, Random.Range(0, 80000) - 40000, Random.Range(0, 120000) - 60000) });
                return list;
            }
            if (system == null) return list;
            int sysRace = Mathf.Clamp(system.raceId, 0, 3);
            bool hardcore = Session.IsExtreme;
            int rank = Session.Rank;
            int cm = Session.CampaignMission;
            bool story = !Session.FreePlay;

            // Mido (system 15) before campaign 0x10: no raiders (except on Extreme), and rnd(2) instead of the security level
            // in the local fighter count (the tutorial's home system stays quiet).
            bool mido = story && system.index == 15 && cm < 0x10;
            int r100 = Random.Range(0, 100);
            int sec = system.securityLevel;
            int secEff = sec >= 1 && hardcore ? sec - 1 : sec;
            bool raidersOn = !(mido && !hardcore) && r100 < NpcTables.RaiderChance(secEff);
            var raiderSpawn = RaiderSpawn();
            int raiderRace = Random.Range(0, 100) < 75 ? Standing.Pirate : Standing.EnemyRaceOf(sysRace);
            int raiders = raidersOn ? Random.Range(0, 4) : 0;
            if (raiders > 0)
            {
                if (hardcore) raiders = Random.Range(0, 6) + 2;
                raiders = (int)(raiders * Session.DifficultyFactor);
                raiders += rank / 4;
            }
            if (secEff == 3 && raidersOn && Session.Difficulty < 1f) raiders = Random.Range(0, 2) + 1;   // Easy / Normal

            int jumpers = 0, freighters = 0, x = 0;
            if (station != 78) { jumpers = Random.Range(0, 2); freighters = Random.Range(0, 5); x = Random.Range(0, 2); }
            int local = (mido ? Random.Range(0, 2) : secEff) + x + freighters / 4;
            // The "big battle" (npc_traffic_ai.md 2.2): raiders on, campaign > 0x1f, 8 %: 9 raiders against 9 locals.
            if (raidersOn && cm > 0x1f && Random.Range(0, 100) < 8) raiders = local = 9;
            // Freelance cargo attracts pirates: int(d / 10 * 5) escorts for Courier, Passenger and Ore Mining missions (types 0,
            // 0xb, 0xf; Ore Mining is only rolled in multiplayer). The station 100 / 101 / 108 / 10 rule below leaves them too.
            var fm = Session.FreelanceMission;
            int escorts = fm != null && (fm.type == MissionType.Courier || fm.type == MissionType.Passenger || fm.type == MissionType.OreMining)
                ? (int)(fm.difficulty / 10f * 5f) : 0;
            // A pirate-base system (npc_combat_specials.md 3.2): no raider group; instead 2 (Extreme 4-6) pirates near the player.
            bool baseSystem = PirateBases.SystemHasBase(db, system.index);
            int baseEscorts = 0;
            if (baseSystem) { raidersOn = false; raiders = 0; escorts = 0; baseEscorts = hardcore ? Random.Range(0, 3) + 4 : 2; }
            // Coming out of the Void, or at the station the Void attack (not at 42): 2-5 Void raiders from the wormhole and at
            // least 2 freighters for them to hunt (Level::createMission, npc_traffic_ai.md 2.2).
            bool voidRaid = story && cm != 42 && cm < 45 && (Session.ComingFromVoid || station == Session.VoidInvasionStation);
            if (voidRaid)
            {
                raidersOn = true;
                raiders = Random.Range(0, 4) + 2;
                raiderRace = Standing.Void;
                freighters = Mathf.Max(freighters, 2);
                if (wormholeGame.HasValue) raiderSpawn = wormholeGame.Value;
            }
            // Status::getWantedInCurrentOrbit: a Most Wanted criminal here caps the police at 2 (it and its escort come first).
            var wanted = WantedBoard.InOrbit(db, station);
            if (wanted != null) local = Mathf.Min(local, 2);
            // An Informer mission at its target station: only 7 local fighters (6 once the informer is dead), the first
            // named "Informer" (1663); no jumpers, freighters or raiders. Otherwise an attacked station has at least 7, and an
            // orbit with nothing at all gets 4 (before the special orbits below empty theirs again).
            bool informer = InformerOrbit(station);
            if (informer) { local = Session.InformerKilled ? 6 : 7; jumpers = freighters = raiders = escorts = baseEscorts = 0; raidersOn = false; }
            else
            {
                if (Session.AttackedStations.Contains(station)) local = Mathf.Max(local, 7);
                if (jumpers + local + freighters + raiders + escorts + baseEscorts == 0) local = 4;
            }
            // The special orbits: 102-104 only 3-7 pirates (escorts stay); 100 / 101 / 108 and Thynome (10) empty.
            if (station >= 102 && station <= 104) { local = jumpers = freighters = 0; raiders = Random.Range(0, 5) + 3; raidersOn = true; raiderRace = Standing.Pirate; }
            else if (station == 100 || station == 101 || station == 108 || station == 10) { local = jumpers = freighters = raiders = 0; raidersOn = false; }
            // Campaign 0x24 / 0x25 in S'kolptorr: no local fighters, no raiders; 0x2a / 0x2b: no raiders, no pirate escorts.
            if (story && (cm == 0x24 || cm == 0x25) && system.index == 5) local = raiders = 0;
            if (story && cm > 0x29 && cm < 0x2c) raiders = escorts = baseEscorts = 0;
            // Loma's black market (system 25): 6-9 pirates (Extreme rnd(3) + 2n), nothing else. The pirate loot orbits
            // (systems 32 / 33): 10-13 pirates (the same on Extreme) with double hull, speed 3.5, around the player.
            bool lootOrbit = system.index == 32 || system.index == 33;
            if (system.index == 25 || lootOrbit)
            {
                escorts = baseEscorts = local = jumpers = freighters = 0;
                raiders = Random.Range(0, 4) + (lootOrbit ? 10 : 6);
                if (hardcore) raiders = Random.Range(0, 3) + raiders * 2;
                raidersOn = true;
                raiderRace = Standing.Pirate;
            }
            // Late-campaign Specter raids (100 < campaign < 0x91): (campaign / 144 * 15 + 5) % (x2 Extreme), 2-4 (x2).
            int specters = 0;
            if (story && cm > 100 && cm < 0x91)
            {
                int p = (int)(cm / 144f * 15f + 5f) << (hardcore ? 1 : 0);
                if (Random.Range(0, 100) < p) specters = (Random.Range(0, 3) + 2) << (hardcore ? 1 : 0);
            }
            // The capital-ship specials (npc_combat_specials.md 2.1): the first freighter becomes the battleship / carrier
            // (Terran) or the Vossk battleship.
            bool terran = sysRace == 0 && freighters > 0 && Random.Range(0, 100) < 30;
            bool vossk = sysRace == 1 && freighters > 0 && Random.Range(0, 100) < 30 && cm > 0x8c;
            bool carrier = terran && Random.Range(0, 100) < 30 && cm > 0x67;
            // The supernova system (27) before campaign 0x9e (Status::inSupernovaSystem): nothing but the story's.
            if (story && system.index == 27 && cm < 0x9e) { local = jumpers = freighters = raiders = 0; raidersOn = terran = vossk = false; }
            // Remake (CapitalShips): now and then a fleet battle (a Terran and a Vossk capital ship with their wings) instead of
            // the single capital ship special.
            bool fleetBattle = FleetBattleHere(station, system, sysRace, baseSystem, story);
            if (fleetBattle) terran = vossk = false;
            if (terran || vossk) freighters--;
            // A pirate raid on a traveller (initStreamOutPosition): rnd(100) < rank + 20 (40 Extreme) moves the pirates'
            // point toward the arriving player (Route::setNewCoords(player position / f); f is lost in the decompile,
            // the remake takes half the way).
            if (raidersOn && raiderRace == Standing.Pirate && Session.ArrivedByTravel && !lootOrbit && Random.Range(0, 100) < rank + (hardcore ? 40 : 20))
                raiderSpawn = playerGame / 2f;

            // Campaign step 59 (type 0xa3) at one of its target stations still to do (Status+0x90): the rival arms convoy
            // and the local fighters instead of the rest of the traffic.
            if (!Session.FreePlay && Session.StoryMission != null && Session.StoryMission.type == StoryType.TargetList && Session.StoryTargets.Contains(station))
            {
                AddConvoy(list, sysRace, local);
                return list;
            }

            // 1 local fighters around one point in front of the station
            var wpLocal = new Vector3(Random.Range(0, 20000) - 10000, Random.Range(0, 20000) - 10000, Random.Range(0, 30000) + 20000);
            if (wanted != null) AddWanted(list, wanted, wpLocal);
            for (int i = 0; i < local; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Local, race = sysRace, ship = NpcTables.RandomFighter(sysRace), position = wpLocal + Jitter(),
                                             nameText = informer && i == 0 && !Session.InformerKilled ? 1663 : -1 });
            // 5 pirate escorts around the player
            for (int i = 0; i < escorts; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Raider, race = Standing.Pirate, ship = NpcTables.RandomFighter(Standing.Pirate),
                                             position = playerGame + new Vector3(Random.Range(0, 160000) - 80000, Random.Range(0, 100000) - 50000, Random.Range(0, 160000) - 80000) });
            // 2 jumpers (dead until relaunched from the station), each with a far one-point route
            for (int i = 0; i < jumpers; i++)
            {
                var route = new Route(false);
                route.points.Add(new Vector3(Random.Range(0, 400000) - 200000, Random.Range(0, 200000) - 100000, Random.Range(0, 100000) + 50000));
                list.Add(new SpawnSpec { group = NpcGroup.Jumper, race = sysRace, ship = NpcTables.RandomFighter(sysRace), route = route, startsDead = true });
            }
            // 3 freighters
            for (int i = 0; i < freighters; i++)
            {
                var (ship, race) = NpcTables.RandomFreighter(sysRace);
                float s = Random.value < 0.5f ? -1f : 1f;
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Freighter, race = race, ship = ship, freighter = true,
                    position = new Vector3(s * (Random.Range(0, 60000) - 80000), Random.Range(0, 40000) - 20000, Random.Range(0, 160000) - 80000),
                });
            }
            if (terran || vossk) AddCapitalShip(db, list, terran, carrier, station);
            if (fleetBattle) AddFleetBattle(db, list, station);
            // 4 raiders (the loot orbits': Player::setHitpoints(2 x max), speed 3.5, each at the player + (20000 +- rnd 50000,
            // 10000 +- rnd 50000, 20000 +- rnd 50000))
            int raidersFrom = list.Count;
            AddRaiders(list, raiders, raiderRace, raiderSpawn);
            if (lootOrbit)
                for (int i = raidersFrom; i < list.Count; i++)
                {
                    float S() => Random.Range(0, 2) == 0 ? 1f : -1f;
                    list[i].hitpoints = 2 * NpcTables.Hull(0, list[i].ship);
                    list[i].speed = 3.5f;
                    list[i].position = playerGame + new Vector3(S() * Random.Range(0, 50000) + 20000, S() * Random.Range(0, 50000) + 10000, S() * Random.Range(0, 50000) + 20000);
                }
            // Pirate-base system: the escorts near the player; the base station's orbit: the outpost and its guards.
            for (int i = 0; i < baseEscorts; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Escort, race = Standing.Pirate, ship = NpcTables.RandomFighter(Standing.Pirate),
                                             position = playerGame + new Vector3(Random.Range(0, 160000) - 80000, Random.Range(0, 100000) - 50000, Random.Range(0, 160000) - 80000) });
            if (PirateBases.StationHasBase(station)) AddPirateBase(list, station, hardcore);
            // 8 a Supernova wreck with a hidden blueprint: dockable and hackable (docking type 3) until it is found.
            // Level::createMission (hidden-blueprint block): createShip(race DAT_00253754[k], 1, k == 0 ? 0xd : 0xf), the
            // race's freighter (the Vossk one for slot 0), setDockingType(3), PlayerFixedObject::setDeadButSelectable (its
            // wreck mesh at the animation's end, HP 1, invulnerable), placed at DAT_00253768[k], loadSpacePoints(
            // DAT_002537a4[k]). A fixed object here (never moves, no engine or gun) showing the wreck (deadButSelectable).
            for (int k = 0; k < HiddenBlueprints.Length; k++)
            {
                if (HiddenBlueprints[k].station != station) continue;
                bool found = (Session.HiddenBlueprintsFound & (1 << k)) != 0;
                int race = HiddenBlueprints[k].race;
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Special, race = race, ship = k == 0 ? 13 : 15, position = HiddenBlueprints[k].position,
                    fixedObject = NpcTables.FreighterAssembly(race), deadButSelectable = true, stationary = true, alwaysFriend = true,
                    hitpoints = 9999999, noLoot = true, nameText = 3211, dockingType = found ? 0 : 3, spacePoints = HiddenBlueprints[k].points,
                    hiddenBlueprint = k, hitRadius = 4000f,
                });
            }
            // Remake mods: a mod blueprint's derelict (blueprints.json "derelict"), at most one per visit: a hackable freighter
            // wreck like the hidden blueprints' (its race's: the Vossk freighter 13, else 15, docking point sets 11-14),
            // somewhere out in front of the station; the hack leaves the blueprint in a data crate (Traffic.OnHackWon).
            var derelict = Modding.ModBlueprints.RollDerelict();
            if (derelict != null)
            {
                int race = derelict.derelictRace >= 0 ? derelict.derelictRace : Random.Range(0, 4);
                int points = race == 1 ? 14 : race == 3 ? 11 : race == 2 ? 12 : 13;
                var at = new Vector3(Random.Range(-80000f, 80000f), Random.Range(-40000f, 40000f), Random.Range(60000f, 140000f));
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Special, race = race, ship = race == 1 ? 13 : 15, position = at,
                    fixedObject = NpcTables.FreighterAssembly(race), deadButSelectable = true, stationary = true, alwaysFriend = true,
                    hitpoints = 9999999, noLoot = true, nameText = 3211, dockingType = 3, spacePoints = points,
                    modBlueprint = derelict.product, hitRadius = 4000f,
                });
            }
            // 7 Specters, always enemy: one point near the player, each at it + createShip's +-20000 jitter.
            if (specters > 0)
            {
                float S() => Random.value < 0.5f ? -1f : 1f;
                var sp = playerGame + new Vector3(S() * (Random.Range(0, 50000) + 20000), S() * (Random.Range(0, 50000) + 10000), S() * (Random.Range(0, 50000) + 20000));
                for (int i = 0; i < specters; i++)
                    list.Add(new SpawnSpec { group = NpcGroup.Special, race = Standing.Specter, ship = 44, alwaysEnemy = true, position = sp + Jitter() });
            }
            AddPirateEvent(list, db, station, system, secEff, story, cm, baseSystem);
            return list;
        }

        // ---- remake: pirate events (GitHub #6) ----------------------------------------------------------------------

        /// <summary>The chance of an event per orbit entry by the system's security level (0 Dangerous, 1 Risky; Average and
        /// Secure systems never have one).</summary>
        static readonly int[] EventChance = { 15, 7 };
        static readonly int[] BossShips = { 60, 29, 32, 25 };       // Darkzov, Mantis, Wasp, Tyrion
        static readonly int[] BossLoot = { 113, 108, 107, 102, 101 };   // Implants, Vossk Organs, Organs, Rare Plants / Animals
        static readonly int[] DangerousBossLoot = { 113, 108 };          // Implants, Vossk Organs
        /// <summary>A Dangerous outpost's crate: one piece of equipment by the player's rank (below 10: Berger Converge IV,
        /// Tyrfing Blaster, MaxHeat o20, Mass Driver MD 10, Beamshield II; from 10: M6 A3 "Wolverine", D'iol, T'yol, L'ksaar,
        /// Fluxed Matter Shield, Rhoda Blackhole).</summary>
        static readonly int[] DangerousLootLow = { 8, 21, 30, 27, 53 };
        static readonly int[] DangerousLootHigh = { 11, 58, 59, 49, 54, 67 };
        /// <summary>A Dangerous outpost's turrets (game units from its centre, the outpost unrotated): the four corners of the
        /// raised hub on top and two under the deck, turned over (measured on the station_pirates mesh).</summary>
        static readonly (Vector3 pos, Vector3 rot)[] OutpostTurrets =
        {
            (new Vector3(4000, 917, 4000), Vector3.zero), (new Vector3(-4000, 917, 4000), Vector3.zero),
            (new Vector3(4000, 917, -4000), Vector3.zero), (new Vector3(-4000, 917, -4000), Vector3.zero),
            (new Vector3(4000, -8548, 4000), new Vector3(0, 0, Mathf.PI)), (new Vector3(-4000, -8548, -4000), new Vector3(0, 0, Mathf.PI)),
        };

        static HashSet<int> storyStations;

        /// <summary>Every station a step of the three campaigns (story.json) sends the player to, the station the Void attack
        /// and step 59's convoy targets: the pirate events stay out of the storylines' orbits.</summary>
        static bool InStoryline(int station)
        {
            if (storyStations == null)
            {
                storyStations = new HashSet<int>();
                foreach (var step in StoryTable.Steps) if (step != null && step.station >= 0) storyStations.Add(step.station);
            }
            return storyStations.Contains(station) || station == Session.VoidInvasionStation || Session.StoryTargets.Contains(station);
        }

        /// <summary>Remake (option "Pirate outposts and bosses", on by default; players' suggestions): now and then (15 % in
        /// Dangerous systems, 7 % in Risky ones, never in Average or Secure ones) a free-flight orbit holds a sleeping pirate
        /// outpost with its guards far out (the pirate bases' outpost: radio 435-437 when a guard wakes, 438-440 when it falls)
        /// or a pirate boss (a Wanted-like hull and gun by the player's rank, speed 3.5, a crate of rare goods) with 2-4 escorts
        /// in front of the station, further out than the raiders; either pays a bounty (Traffic.PirateEventDone). In a
        /// Dangerous system the outpost has six turrets, three more guards and a piece of equipment in its crate, the boss two
        /// more escorts and more of the costlier goods, and the bounty is half as much again. Not below rank 2, at a station
        /// any campaign step sends the player to, in Mido before Keith leaves it (campaign 0x10), the special orbits (Loma, the
        /// empty ones, the supernova system, the pirates' own systems), a pirate base's system or the Kaamo siege.</summary>
        static void AddPirateEvent(List<SpawnSpec> list, Database db, int station, SystemData system, int secEff, bool story, int cm, bool baseSystem)
        {
            if (!Settings.PirateEvents || (story && cm < 0x10) || baseSystem || KaamoClub.SiegeAt(station) || Session.Rank < 2) return;
            if (system.index == 15 && !story) return;   // Mido: only once Keith has left it (free play keeps it quiet)
            if (station == 100 || station == 101 || station == 108 || station == 10 || (station >= 102 && station <= 104)) return;
            if (system.index == 25 || system.index == 27 || system.index == 32 || system.index == 33) return;
            int sec = system.securityLevel;   // 0 Dangerous, 1 Risky, 2 Average, 3 Secure (texts 402-405)
            if (sec < 0 || sec >= EventChance.Length || InStoryline(station)) return;
            if (Random.Range(0, 100) >= EventChance[sec]) return;
            AddPirateEventShips(list, Random.value < 0.5f, sec == 0);
        }

        /// <summary>The event's ships: the outpost with its guards (and turrets when Dangerous), or the boss with its escorts.
        /// Public for testing (spawned with Traffic.SpawnShip).</summary>
        public static void AddPirateEventShips(List<SpawnSpec> list, bool outpostEvent, bool dangerous)
        {
            int rank = Mathf.Min(Session.Rank, 20);
            if (outpostEvent)
            {
                // The outpost, 110-160 km out in a random direction (flattened; the biggest stations reach 70 km), guarded like
                // a pirate base.
                var dir = Random.onUnitSphere;
                dir.y *= 0.3f;
                var at = dir.normalized * Random.Range(110000f, 160000f);
                var assets = CombatAssets.Load();
                var loot = PirateBases.Loot[Random.Range(0, PirateBases.Loot.Length)];
                if (dangerous)
                {
                    var table = rank < 10 ? DangerousLootLow : DangerousLootHigh;
                    loot = (table[Random.Range(0, table.Length)], 1);
                }
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Outpost, race = Standing.Pirate, ship = -1, position = at,
                    fixedObject = "station_pirates", collisionId = 1002, hitRadius = 7500f, hitpoints = KaamoClub.OutpostHull(),
                    wreckPrefab = assets != null ? assets.outpostWreck : null, explosionScale = 8f, stationary = true, asleep = true,
                    nameText = 441, lootItem = loot.item, lootAmount = loot.amount, alwaysEnemy = true, pirateEvent = SpawnSpec.EventOutpost,
                    eventDangerous = dangerous,
                });
                int guards = (int)((Session.Difficulty - 0.5f) * 5f + 5f) + (dangerous ? 3 : 0);
                for (int g = 0; g < guards; g++)
                {
                    float S() => Random.value < 0.5f ? -1f : 1f;
                    list.Add(new SpawnSpec
                    {
                        group = NpcGroup.Guard, race = Standing.Pirate, ship = NpcTables.RandomFighter(Standing.Pirate), asleep = true, guard = true,
                        position = at + new Vector3(S() * Random.Range(0, 20000) + 10000, S() * Random.Range(0, 20000) + 10000, S() * Random.Range(0, 20000) + 10000),
                    });
                }
                if (dangerous)
                    foreach (var t in OutpostTurrets)
                    {
                        var turret = Turret(0, at + t.pos, t.rot);
                        turret.race = Standing.Pirate;
                        turret.alwaysEnemy = true;
                        turret.pirateEvent = SpawnSpec.EventTurret;
                        list.Add(turret);
                    }
                return;
            }
            // The boss and its escorts, in front of the station like a raider group but 30 km further out (no raider waves:
            // group Special).
            var spawn = RaiderSpawn() + new Vector3(0f, 0f, 30000f);
            int hull = (int)((15 * rank + 1500 + 4 * 45) * Session.DifficultyFactor);
            int gun = rank < 5 ? 23 : rank < 10 ? 15 : rank < 15 ? 26 : 21;   // Micro Gun MKII, H'nookk, Scram Cannon, Tyrfing
            var bossLoot = dangerous ? DangerousBossLoot : BossLoot;
            list.Add(new SpawnSpec
            {
                group = NpcGroup.Special, race = Standing.Pirate, ship = BossShips[Random.Range(0, BossShips.Length)], position = spawn,
                hitpoints = hull, nameText = 1606, speed = 3.5f, gunItem = gun, gunFactor = 2f, alwaysEnemy = true,
                lootItem = bossLoot[Random.Range(0, bossLoot.Length)], lootAmount = dangerous ? Random.Range(6, 13) : Random.Range(3, 9),
                pirateEvent = SpawnSpec.EventBoss, eventDangerous = dangerous,
            });
            int escorts = Random.Range(2, 5) + (dangerous ? 2 : 0);
            for (int i = 0; i < escorts; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Special, race = Standing.Pirate, ship = NpcTables.RandomFighter(Standing.Pirate),
                                         position = spawn + Jitter(), alwaysEnemy = true });
        }

        // ---- the Most Wanted criminal (Level::createMission 0xbda70, wingmen_wanted.md 2.6) ---------------------------

        /// <summary>[0] the criminal at the police's point (its race, pirates for races 4+), hull 15 * min(rank, 20) +
        /// its hitpoints + 4 * 45 (x2 Extreme), speed 4.5, its weapon x4, its loot and name; then its escort of the same race
        /// at half that hull.</summary>
        static void AddWanted(List<SpawnSpec> list, WantedData w, Vector3 wpLocal)
        {
            int race = w.race < 4 ? w.race : Standing.Pirate;
            int h = 15 * Mathf.Min(Session.Rank, 20) + w.hitpoints + 4 * 45;
            int hull = (int)(h * Session.DifficultyFactor);
            list.Add(new SpawnSpec
            {
                group = NpcGroup.Local, race = race, ship = w.ship, position = wpLocal + Jitter(), hitpoints = hull, name = w.name,
                lootItem = w.loot, lootAmount = w.lootAmount, wantedIndex = w.index, speed = 4.5f, gunItem = w.weapon, gunFactor = 4f,
                // Level::assignGuns: the wanted's flying ships 45-48 add a rocket gun (item 31 G'liissk, 4 x damage).
                secondaryItem = w.ship >= 45 && w.ship <= 48 ? 31 : -1, secondaryFactor = 4f,
            });
            for (int i = 0; i < w.numWingmen; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Escort, race = race, ship = NpcTables.RandomFighter(race), position = wpLocal + Jitter(),
                                         hitpoints = hull / 2, wantedEscortOf = w.index });
        }

        // ---- step 59's arms convoy (Level::createMission 0xbe742, npc_combat_specials.md 4) --------------------------

        /// <summary>Table 0x253464 (per system race): the two battlestation turrets' offsets from the freighter and rotations.</summary>
        static readonly (Vector3 pos, Vector3 rot)[][] ConvoyTurrets =
        {
            new[] { (new Vector3(0, -1097.14f, -4178.23f), new Vector3(0, 0, Mathf.PI)), (new Vector3(0, 1158.09f, 1180.59f), Vector3.zero) },
            new[] { (new Vector3(0, -1096.98f, -2691.52f), new Vector3(0, 0, Mathf.PI)), (new Vector3(0, 1893.48f, 1068.07f), Vector3.zero) },
            new[] { (new Vector3(0, -484.719f, 1741.22f), new Vector3(0, 0, Mathf.PI)), (new Vector3(0, 1458.84f, -2304.28f), Vector3.zero) },
            new[] { (new Vector3(0, -516.08f, -3744.45f), new Vector3(0, 0, Mathf.PI)), (new Vector3(0, 515.766f, -3744.45f), Vector3.zero) },
        };

        /// <summary>[0] the freighter "Arms delivery" (1664) parked at C = (+-(80000..109999), -6000..-3001, 120000..169999),
        /// its crate one of items 0 / 1 / 2 / 36 / 22 / 23; [1-5] escorts looping on C; [6-7] battlestation turrets (1000 HP,
        /// scale 0.3); then the local fighters at the usual point in front of the station. The player's route is C
        /// (Traffic.ConvoyRoute).</summary>
        static void AddConvoy(List<SpawnSpec> list, int race, int local)
        {
            var wpLocal = new Vector3(Random.Range(0, 20000) - 10000, Random.Range(0, 20000) - 10000, Random.Range(0, 30000) + 20000);
            float sign = Random.Range(0, 2) == 0 ? 1f : -1f;
            var c = new Vector3(sign * (Random.Range(0, 30000) + 80000), Random.Range(0, 3000) - 6000, Random.Range(0, 50000) + 120000);
            int[] loot = { 0, 1, 2, 0x24, 0x16, 0x17 };
            list.Add(new SpawnSpec
            {
                group = NpcGroup.Special, race = race, ship = race == 1 ? 13 : 15, freighter = true, stationary = true, position = c,
                nameText = 1664, lootItem = loot[Random.Range(0, loot.Length)], lootAmount = 1, convoyRole = SpawnSpec.ConvoyFreighter,
            });
            var loop = new Route(true);
            loop.points.Add(c);
            for (int i = 0; i < 5; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Escort, race = race, ship = NpcTables.RandomFighter(race), position = c + Jitter(),
                                         route = loop, convoyRole = SpawnSpec.ConvoyEscort });
            foreach (var t in ConvoyTurrets[race])
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Turret, race = race, ship = -1, position = c + t.pos, rotation = t.rot, scale = 0.3f, hitpoints = 1000,
                    turretAssembly = "v_station_battlestation_turret", noLoot = true, nameText = 1666, stationary = true,
                    convoyRole = SpawnSpec.ConvoyTurret,
                });
            for (int i = 0; i < local; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Local, race = race, ship = NpcTables.RandomFighter(race), position = wpLocal + Jitter() });
        }

        // ---- capital ships (npc_combat_specials.md 2.2 - 2.4) ---------------------------------------------------

        /// <summary>Table 0x253604 / 0x253544 / 0x2536ac: the turrets' offsets from their host (game units) and rotations.</summary>
        public static readonly (Vector3 pos, Vector3 rot)[] BattleshipTurrets =
        {
            (new Vector3(5322, 1893, -15571), new Vector3(0, 0, -Mathf.PI / 2)), (new Vector3(2356, 1115, 7324), new Vector3(0, 0, -Mathf.PI / 2)),
            (new Vector3(0, -4261, 4876), new Vector3(0, 0, Mathf.PI)), (new Vector3(-5322, 1893, -15571), new Vector3(0, 0, Mathf.PI / 2)),
            (new Vector3(0, 5669, -13872), Vector3.zero), (new Vector3(-2356, 1115, 7324), new Vector3(0, 0, Mathf.PI / 2)),
            (new Vector3(0, 2855, 4161), Vector3.zero),
        };
        public static readonly (Vector3 pos, Vector3 rot)[] CarrierTurrets =
        {
            (new Vector3(-6726, 2458, -66), new Vector3(0, Mathf.PI / 2, 0)), (new Vector3(5824, 2458, 10243), new Vector3(0, -Mathf.PI / 2, 0)),
            (new Vector3(-6726, 2458, 2100), new Vector3(0, Mathf.PI / 2, 0)), (new Vector3(5824, 2458, 8437), new Vector3(0, -Mathf.PI / 2, 0)),
            (new Vector3(-5549, -2743, 11954), new Vector3(-Mathf.PI, Mathf.PI / 2, 0)), (new Vector3(-5549, -2743, -3711), new Vector3(-Mathf.PI, Mathf.PI / 2, 0)),
            (new Vector3(4981, -2743, 14659), new Vector3(Mathf.PI, -2.1817f, 0)), (new Vector3(0, 2458, -32811), Vector3.zero),
        };
        public static readonly Vector3[] VosskTurrets =
        {
            new Vector3(10283, -1123, 26039), new Vector3(-10171, -1083, 25907), new Vector3(-15624, -787, -7569),
            new Vector3(15624, -787, -7569), new Vector3(0, 4901, 3084),
        };

        static void AddCapitalShip(Database db, List<SpawnSpec> list, bool terran, bool carrier, int station)
        {
            int kind = !terran ? SpawnSpec.CapitalVossk : carrier ? SpawnSpec.CapitalCarrier : SpawnSpec.CapitalBattleship;
            // The original's boxes in createMission: the battleship's, the carrier's and the Vossk battleship's.
            System.Func<Vector3> roll = kind == SpawnSpec.CapitalBattleship
                ? () => new Vector3(Random.Range(0, 80000) - 40000, Random.Range(0, 10000) - 5000, Random.Range(0, 80000) + 40000)
                : kind == SpawnSpec.CapitalCarrier
                ? () => new Vector3(Random.Range(0, 80000) - 40000, Random.Range(0, 40000) - 20000, Random.Range(0, 80000) + 100000)
                : () => new Vector3(Random.Range(0, 80000) - 40000, Random.Range(0, 40000) - 20000, Random.Range(0, 80000) + 60000);
            // Remake option: escorts, stronger turrets, the killable carrier / Vossk battleship (World.CapitalShips).
            AddCapitalShipAt(db, list, kind, ClearOfStation(station, CapitalVolumes(kind), roll), Settings.CapitalShips);
        }

        static List<CollisionVolume> CapitalVolumes(int kind) => kind == SpawnSpec.CapitalBattleship ? CollisionVolume.ForFreighter(14, 0)
            : CollisionVolume.ForStaticObject(kind == SpawnSpec.CapitalCarrier ? 2005 : 2006);

        /// <summary>A capital ship of 'kind' (SpawnSpec.Capital*) at 'host' (game units) with its turrets, enhanced (escorts,
        /// killable: World.CapitalShips) or as the original's; returns the host's index in the list.</summary>
        public static int AddCapitalShipAt(Database db, List<SpawnSpec> list, int kind, Vector3 host, bool enhanced)
        {
            int first = list.Count;
            if (kind == SpawnSpec.CapitalBattleship)
            {
                list.Add(new SpawnSpec { group = NpcGroup.Special, race = 0, ship = 14, freighter = true, stationary = true, position = host,
                                         capital = SpawnSpec.CapitalBattleship });
                foreach (var t in BattleshipTurrets) list.Add(Turret(0, host + t.pos, t.rot));
            }
            else if (kind == SpawnSpec.CapitalCarrier)
            {
                list.Add(new SpawnSpec { group = NpcGroup.Special, race = 0, ship = -1, position = host, fixedObject = "sn_carrier_terran_1",
                                         collisionId = 2005, hitRadius = 0f, hitpoints = 9999999, noLoot = true, stationary = true,
                                         capital = SpawnSpec.CapitalCarrier });
                foreach (var t in CarrierTurrets) list.Add(Turret(0, host + t.pos, t.rot));
            }
            else
            {
                list.Add(new SpawnSpec { group = NpcGroup.Special, race = 1, ship = -1, position = host, fixedObject = "sn_battleship_vossk",
                                         collisionId = 2006, hitRadius = 0f, hitpoints = 9999999, noLoot = true, stationary = true, nameText = 1667,
                                         capital = SpawnSpec.CapitalVossk });
                foreach (var t in VosskTurrets) list.Add(Turret(1, host + t, Vector3.zero));
            }
            for (int i = first + 1; i < list.Count; i++) { list[i].capitalPart = true; list[i].capitalHost = list[first]; }
            if (enhanced) GoF2Remake.World.CapitalShips.Enhance(db, list, first);
            return first;
        }

        // ---- remake: fleet battles (World.CapitalShips) -------------------------------------------------------------

        /// <summary>A fleet battle in this orbit: the option on, a Terran or Vossk system (the two at war), past step 103 (the
        /// carrier's era; the finished game's world too), 3 % per visit; not in the special orbits, a pirate base system, the
        /// Kaamo siege or a storyline's orbit while a story runs.</summary>
        static bool FleetBattleHere(int station, SystemData system, int sysRace, bool baseSystem, bool story)
        {
            if (!Settings.CapitalShips || (sysRace != 0 && sysRace != 1) || Session.CampaignMission <= 0x67) return false;
            if (Session.FreePlay && !Session.CompletedWorld) return false;
            if (baseSystem || KaamoClub.SiegeAt(station) || (story && InStoryline(station))) return false;
            if (station == 100 || station == 101 || station == 108 || station == 10 || (station >= 102 && station <= 104)) return false;
            if (system.index == 25 || system.index == 27 || system.index == 32 || system.index == 33) return false;
            return Random.Range(0, 100) < GoF2Remake.World.CapitalShips.FleetBattleChance;
        }

        /// <summary>A fleet battle (World.CapitalShips): a Terran carrier or battleship and the Vossk battleship side by side,
        /// both facing game +Z, BattleSeparationUnits apart across 'centre' (rolled 110 000-150 000 units in front of the
        /// station, clear of it), each with its turrets and escorts and a wing that loops round the enemy capital ship.</summary>
        public static void AddFleetBattle(Database db, List<SpawnSpec> list, int station, Vector3? centre = null)
        {
            int terranKind = Random.value < 0.5f ? SpawnSpec.CapitalCarrier : SpawnSpec.CapitalBattleship;
            float side = Random.value < 0.5f ? -1f : 1f;
            var half = new Vector3(GoF2Remake.World.CapitalShips.BattleSeparationUnits / 2f, 0f, 0f) * side;
            Vector3 c = centre ?? Vector3.zero;
            if (centre == null)
            {
                var hull = CollisionVolume.ForStation(GoF2Remake.World.OrbitBuilder.StationLook(station), false);
                for (int tries = 0; tries < 40; tries++)
                {
                    c = new Vector3(Random.Range(0, 60000) - 30000, Random.Range(0, 16000) - 8000, Random.Range(0, 40000) + 110000);
                    if (!Touches(hull, CapitalVolumes(terranKind), c - half) && !Touches(hull, CapitalVolumes(SpawnSpec.CapitalVossk), c + half)) break;
                }
            }
            var terran = list[AddCapitalShipAt(db, list, terranKind, c - half, true)];
            var vossk = list[AddCapitalShipAt(db, list, SpawnSpec.CapitalVossk, c + half, true)];
            terran.fleetBattle = vossk.fleetBattle = true;
            terran.battleFoe = vossk;
            vossk.battleFoe = terran;
            AddBattleWing(list, terran, vossk);
            AddBattleWing(list, vossk, terran);
        }

        /// <summary>4 + rank / 5 fighters of the capital ship's race beside it, looping round the enemy capital ship (they meet
        /// the enemy fleet there).</summary>
        static void AddBattleWing(List<SpawnSpec> list, SpawnSpec own, SpawnSpec foe)
        {
            var loop = new Route(true);
            loop.points.Add(foe.position + new Vector3(0, 8000, -45000));
            loop.points.Add(foe.position + new Vector3(30000, 4000, 0));
            loop.points.Add(foe.position + new Vector3(0, 8000, 45000));
            loop.points.Add(foe.position + new Vector3(-30000, 4000, 0));
            int n = 4 + Mathf.Min(Session.Rank, 20) / 5;
            for (int i = 0; i < n; i++)
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Escort, race = own.race, ship = NpcTables.RandomFighter(own.race), route = loop,
                    position = own.position + new Vector3(Random.Range(-15000, 15000), Random.Range(-6000, 6000), Random.Range(-20000, 20000)),
                    capitalPart = true, capitalHost = own, capitalEnhanced = true,
                });
        }

        /// <summary>Remake (#45): the capital ship's box starts 2 km in front of the station, inside the bigger stations (Valadon's
        /// 3 km hull had the battleship through it; the original rolls it anywhere in the box): the point is rolled again
        /// while the ship's volumes (game units relative to it) would touch the station's, at most 40 times.</summary>
        static Vector3 ClearOfStation(int station, List<CollisionVolume> ship, System.Func<Vector3> roll)
        {
            var hull = CollisionVolume.ForStation(GoF2Remake.World.OrbitBuilder.StationLook(station), false);
            Vector3 p = roll();
            for (int i = 0; i < 40 && Touches(hull, ship, p); i++) p = roll();
            return p;
        }

        static bool Touches(List<CollisionVolume> station, List<CollisionVolume> ship, Vector3 gamePos)
        {
            var offset = new Vector3(gamePos.x, gamePos.y, -gamePos.z) * 0.05f;   // Unity metres, the station at the origin
            const float Margin = 150f;
            foreach (var a in station)
            {
                var ah = a.sphere ? Vector3.one * a.radius : a.half;
                foreach (var b in ship)
                {
                    var bh = (b.sphere ? Vector3.one * b.radius : b.half) + Vector3.one * Margin;
                    var d = b.centre + offset - a.centre;
                    if (Mathf.Abs(d.x) < ah.x + bh.x && Mathf.Abs(d.y) < ah.y + bh.y && Mathf.Abs(d.z) < ah.z + bh.z) return true;
                }
            }
            return false;
        }

        /// <summary>Level::createStaticObject 0x1a74 / 0x1a76: 1000 HP, scale 6, name 1666 "Turret", no loot.</summary>
        static SpawnSpec Turret(int race, Vector3 pos, Vector3 rot) => new SpawnSpec
        {
            group = NpcGroup.Turret, race = race, ship = -1, position = pos, rotation = rot, scale = 6f, hitpoints = 1000,
            turretAssembly = race == 1 ? "turret_003_static" : "turret_002_static", noLoot = true, nameText = 1666, stationary = true,
        };

        // ---- pirate bases (npc_combat_specials.md 3) --------------------------------------------------------

        static void AddPirateBase(List<SpawnSpec> list, int station, bool hardcore)
        {
            int i = PirateBases.IndexOf(station);
            var table = PirateBases.OutpostPositions[i];
            var assets = CombatAssets.Load();
            var loot = PirateBases.Loot[i];
            list.Add(new SpawnSpec
            {
                group = NpcGroup.Outpost, race = Standing.Pirate, ship = -1, position = table + Jitter() / 2f,   // +-10000
                fixedObject = "station_pirates", collisionId = 1002, hitRadius = 7500f, hitpoints = KaamoClub.OutpostHull(),
                wreckPrefab = assets != null ? assets.outpostWreck : null, explosionScale = 8f, stationary = true, asleep = true,
                nameText = 441, lootItem = loot.item, lootAmount = loot.amount, alwaysEnemy = true,
            });
            int guards = (int)((Session.Difficulty - 0.5f) * 5f + 5f);   // 2 / 5 / 7 / 10
            for (int g = 0; g < guards; g++)
            {
                float S() => Random.value < 0.5f ? -1f : 1f;
                list.Add(new SpawnSpec
                {
                    group = NpcGroup.Guard, race = Standing.Pirate, ship = NpcTables.RandomFighter(Standing.Pirate), asleep = true, guard = true,
                    position = table + new Vector3(S() * Random.Range(0, 20000) + 10000, S() * Random.Range(0, 20000) + 10000, S() * Random.Range(0, 20000) + 10000),
                });
            }
        }

        static Vector3 RaiderSpawn() => new Vector3(Random.Range(0, 100000) - 50000, 0f, Random.Range(0, 50000) + 50000);

        static void AddRaiders(List<SpawnSpec> list, int n, int race, Vector3 spawn)
        {
            int ship = NpcTables.RandomFighter(race);   // one model for the whole group
            for (int i = 0; i < n; i++)
                list.Add(new SpawnSpec { group = NpcGroup.Raider, race = race, ship = ship, position = spawn + Jitter() });
        }
    }
}
