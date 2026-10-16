// NetOrbit.cs
// Multiplayer: the local player's Space level in the shared world (SpaceLevel adds it while a session runs).
//   Asteroids: the field is built from the orbit's seed (SpaceLevel.SpawnNetworkAsteroids), the same for every player
//     there; on arrival the ones already destroyed this session are removed (NetState's list), and a destruction here
//     (shot, mined, rammed) or elsewhere in this orbit reaches everyone (by index in the field).
//   The orbit authority (the first player in an empty orbit, SpaceLevel.NetAuthority: only then is new traffic built) runs
//   the orbit's NPC traffic and shows it to the
//     others: one NetProxy per living ship (by its index in Traffic.Ships) and one NetCrate per crate, spawned for it by
//     the host and owned by this player; despawned when the ship is gone or the crate taken / expired. Leaving the orbit
//     takes them away: the player still there with the lowest client id takes the orbit over, rebuilding each flying ship
//     where it was (Traffic.Adopt, same model, race and hull); NetState keeps the old proxies a few seconds for that.
//   A freelance mission's orbit (FreelanceOrbit) is this player's own NPCs whether or not they run the orbit: its ships and
//     junk are shown to everyone here the same way (never taken over), and they spawn out of the others' view
//     (OutOfSight).

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.Flight;
using GoF2Remake.World;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class NetOrbit : MonoBehaviour, World.ISharedOrbit
    {
        const float ScanSeconds = 0.25f;
        /// <summary>Proxy ids from here on are a freelance mission's space junk (FreelanceOrbit.Junk), below it ships.</summary>
        public const int JunkBase = 100000;
        const float SightCos = 0.42f, SightRange = 60000f * OrbitLayout.MetersPerUnit, NearRange = 1500f * OrbitLayout.MetersPerUnit;

        /// <summary>The local player's orbit, null outside a multiplayer Space level.</summary>
        public static NetOrbit Current { get; private set; }

        SpaceLevel level;
        readonly List<Target> asteroids = new List<Target>();
        readonly HashSet<int> requestedShips = new HashSet<int>();
        readonly Dictionary<NpcShip, NetProxy> proxies = new Dictionary<NpcShip, NetProxy>();
        readonly Dictionary<int, Crate> crates = new Dictionary<int, Crate>();
        readonly Dictionary<Crate, NetCrate> netCrates = new Dictionary<Crate, NetCrate>();
        readonly Dictionary<Crate, int> crateIds = new Dictionary<Crate, int>();
        readonly HashSet<int> requestedJunk = new HashSet<int>();
        readonly Dictionary<Target, NetProxy> junkProxies = new Dictionary<Target, NetProxy>();
        readonly Dictionary<Target, float> junkDeadSince = new Dictionary<Target, float>();
        int nextCrateId;
        bool applyingRemote, requestedList;
        float scanTimer;

        public int Station => level != null && level.Layout != null ? level.NetOrbitId : -1;   // an arena: the match's own id
        /// <summary>The orbit's system race (a remote kill's standing, Standing.ApplyKill).</summary>
        public int SystemRace => level != null && level.Traffic != null ? level.Traffic.SystemRace : -1;
        bool Authority => level != null && level.NetAuthority;

        /// <summary>NetProxy: a mission ship's role in this player's freelance mission (FreelanceOrbit.RoleFlags).</summary>
        public int MissionRole(NpcShip ship) => level != null && level.FreelanceOrbit != null ? level.FreelanceOrbit.RoleFlags(ship) : 0;

        /// <summary>Another player's game still runs 'client's ships here (they are in this orbit).</summary>
        public static bool InOrbit(ulong client, int station)
        {
            var p = NetSquad.Find(client);
            return p != null && p.InSpace && p.Station == station;
        }

        public void Setup(SpaceLevel spaceLevel)
        {
            level = spaceLevel;
            Current = this;
            if (level.Asteroids == null) return;
            foreach (Transform t in level.Asteroids)
            {
                var target = t.GetComponent<Target>();
                int index = asteroids.Count;
                asteroids.Add(target);
                if (target != null) target.Died += _ => OnLocalAsteroidDied(index);
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            ClearNpcHooks();
            if (watchedTraffic != null) watchedTraffic.ShipDied -= OnShipDied;
        }

        // ---- the raid news (NetNews) -------------------------------------------------------------------------

        Traffic watchedTraffic;
        readonly HashSet<ulong> raidKillers = new HashSet<ulong>();
        int raidKills, raidRace = -1;

        /// <summary>The orbit's traffic (made after Setup, or anew on a takeover): its deaths watched.</summary>
        void WatchTraffic()
        {
            var t = level != null ? level.Traffic : null;
            if (t == watchedTraffic) return;
            if (watchedTraffic != null) watchedTraffic.ShipDied -= OnShipDied;
            watchedTraffic = t;
            if (t != null) t.ShipDied += OnShipDied;
        }

        /// <summary>A raider went down: who downed it (this player, or another one: Target.remoteKiller). Once none of the
        /// orbit's raiders is left, the authority reports the raid's defenders to the server for the news.</summary>
        void OnShipDied(NpcShip ship, bool byPlayer)
        {
            if (Authority && ship != null && ship.Spec.eventTag == GarrisonTag)
            {
                // A garrison fighter: the server's count goes down (it comes back after its cooldown, NetFactions).
                garrisonHoldUntil = Time.unscaledTime + GarrisonHoldSeconds;
                if (NetState.Instance != null && NetState.Instance.IsSpawned) NetState.Instance.GarrisonKillRpc(Station);
                return;
            }
            if (Authority && ship != null && ship.Spec.eventTag == SiegeDefenderTag)
            {
                // A siege's free defender: back after its cooldown (this game's own count, nothing to report).
                defenderBack.Add(Time.unscaledTime + NetFactions.SiegeDefenderRespawnSeconds);
                return;
            }
            if (!Authority || ship == null || ship.Spec.group != NpcGroup.Raider || watchedTraffic == null) return;
            if (byPlayer && NetPlayer.Local != null) raidKillers.Add(NetPlayer.Local.OwnerClientId);
            else if (ship.Target.killedByRemote && ship.Target.remoteKiller != ulong.MaxValue) raidKillers.Add(ship.Target.remoteKiller);
            else return;   // an NPC's kill (the station's fighters): theirs, not the players'
            raidKills++;
            raidRace = ship.Race;
            if (watchedTraffic.Ships.Exists(s => s != ship && s.Spec.group == NpcGroup.Raider && !s.Gone && s.Target != null && s.Target.Alive)) return;
            if (raidKills >= NetNews.MinDefenseKills && NetState.Instance != null && NetState.Instance.IsSpawned)
                NetState.Instance.DefenseReportRpc(Station, raidRace, raidKills, new List<ulong>(raidKillers).ToArray());
            raidKills = 0;   // a new wave is a new raid
            raidKillers.Clear();
        }

        // ---- the NPCs and the other players (the authority's NpcShip hooks) --------------------------------------

        readonly List<Target> remotePlayers = new List<Target>();

        void UpdateNpcHooks()
        {
            remotePlayers.Clear();
            foreach (var p in NetPlayer.All)
                if (p != null && p.SharesOrbit && p.LocalTarget != null && p.LocalTarget.Alive) remotePlayers.Add(p.LocalTarget);
            // The other players' wingmen here: in the line of fire and attacked like their players.
            foreach (var w in FindObjectsByType<NetProxy>())
                if (w.IsSpawned && w.IsWingman && w.Station == Station && w.LocalTarget != null && w.LocalTarget.Alive && w.LocalTarget.enabled && !w.IsOwner)
                    remotePlayers.Add(w.LocalTarget);
            NpcShip.RemotePlayers = remotePlayers;
            NpcShip.HostileToRemote = HostileToRemote;
            NpcShip.HostileToLocalBySquad = HostileToLocalBySquad;
            NpcShip.TerritoryToLocal = TerritoryToLocal;
            NpcShip.RemoteDockedAtObject = t => t != null && t.GetComponent<NetPlayer>() is NetPlayer p && p.DockedAtObject;
        }

        static void ClearNpcHooks()
        {
            NpcShip.RemotePlayers = null;
            NpcShip.HostileToRemote = null;
            NpcShip.HostileToLocalBySquad = null;
            NpcShip.TerritoryToLocal = null;
            NpcShip.RemoteDockedAtObject = null;
        }

        /// <summary>An always-hostile race (pirates, the Void, Specters), a race their own standing makes an enemy, a ship their
        /// squad turned on, or one hostile to the local player (this orbit's authority) while they are in its squad.</summary>
        static bool HostileToRemote(NpcShip ship, Target t)
        {
            var p = t != null ? t.GetComponent<NetPlayer>() : null;
            if (p == null)
            {
                // Another player's wingman: hostile when the ship is hostile to that player.
                var w = t != null ? t.GetComponent<NetProxy>() : null;
                var owner = w != null && w.IsWingman ? NetSquad.Find(w.Creator) : null;
                return owner != null && owner.LocalTarget != null && HostileToRemote(ship, owner.LocalTarget);
            }
            int r = ship.Race;
            if (ship.alwaysNeutral && !ship.turnedEnemy)
            {
                foreach (var id in ship.aggressors) if (NetSquad.SameClient(id, p)) return true;
                return false;
            }
            if (r == Standing.Pirate || r == Standing.Void || r == Standing.Specter) return true;
            // A faction's held station: its own race's fighters spare its members and attack other factions' pilots without the toll.
            int territory = Territory(ship, p.Station, p.FactionTag, p.TollStation);
            if (territory != 0) return territory < 0;
            if (Standing.IsEnemyWith(r, p.Standing0, p.Standing1, p.Signature)) return true;   // their own standing toward the race
            foreach (var id in ship.aggressors) if (NetSquad.SameClient(id, p)) return true;
            return ship.Target != null && ship.Target.hostileToPlayer && NetSquad.Same(p, NetPlayer.Local);
        }

        /// <summary>NetFactions' station defence: a fighter of the held station's race (the system's), toward a pilot. A siege's
        /// garrison fighter: the attackers' enemy, the holders' friend, the others' as the system's fighters always are.</summary>
        static int Territory(NpcShip ship, int station, string tag, int tollAt)
        {
            var orbit = Current;
            if (orbit == null || orbit.level == null || orbit.level.Layout == null || ship.Race != orbit.level.Layout.raceId) return 0;
            if (ship.Spec.eventTag == GarrisonTag || ship.Spec.eventTag == SiegeDefenderTag) return NetFactionsClient.GarrisonRelation(station, tag);
            return NetFactionsClient.Relation(station, tag, tollAt);
        }

        // ---- a siege's garrison (NetFactions) ---------------------------------------------------------------

        /// <summary>SpawnSpec.eventTag of the garrison fighters (no event's batch tag reaches it; kept by a takeover's AdoptSpec,
        /// and like an event's ship never relaunched by the traffic).</summary>
        public const int GarrisonTag = 0x3FFFFFF0;
        /// <summary>SpawnSpec.eventTag of an armed siege's free station defenders (UpdateSiegeDefenders).</summary>
        public const int SiegeDefenderTag = 0x3FFFFFF1;
        readonly List<float> defenderBack = new List<float>();   // when each dead siege defender may be flown in again
        const float GarrisonHoldSeconds = 3f;   // after a death: the server's count catches up before more are flown in
        const int GarrisonSpawnPerTick = 4;
        float garrisonHoldUntil, garrisonTimer;

        /// <summary>The authority flies the besieged station's garrison: as many as the server counts alive (NetState.Sieges),
        /// swarming in front of the station; the siege over, they jump out.</summary>
        void UpdateGarrison()
        {
            if ((garrisonTimer -= Time.unscaledDeltaTime) > 0f) return;
            garrisonTimer = 1f;
            var traffic = level != null ? level.Traffic : null;
            if (!Authority || traffic == null || level.Layout == null) return;
            var siege = NetFactionsClient.SiegeAt(Station);
            UpdateSiegeDefenders(traffic, siege);
            int want = siege != null && siege.armed ? siege.garrisonAlive : 0;
            var alive = traffic.Ships.FindAll(x => x != null && x.Spec.eventTag == GarrisonTag && !x.Gone && x.Target != null && x.Target.Alive);
            if (want == 0)
            {
                foreach (var x in alive) x.JumpOut();
                return;
            }
            if (alive.Count >= want || Time.unscaledTime < garrisonHoldUntil) return;
            int lvl = Mathf.Clamp(siege.garrisonLevel, 1, NetFactions.MaxGarrisonLevel);
            int race = level.Layout.raceId;
            int count = Mathf.Min(want - alive.Count, GarrisonSpawnPerTick);
            for (int i = 0; i < count; i++)
            {
                int ship = lvl >= 3 ? NpcTables.StrongFighter(level.Database, race) : NpcTables.RandomFighter(race);
                traffic.SpawnShip(new SpawnSpec
                {
                    group = NpcGroup.Local, race = race, ship = ship, eventTag = GarrisonTag, noLoot = true,
                    position = new Vector3(Random.Range(-20000f, 20000f), Random.Range(-8000f, 8000f), Random.Range(15000f, 45000f)),
                    hitpoints = Mathf.RoundToInt(NpcTables.Hull(0, ship) * (1f + 0.5f * (lvl - 1))),
                    gunItem = NpcTables.GunItem(race), gunFactor = 1f + 0.25f * (lvl - 1),
                    name = string.Format(Localization.Extra("mpGarrisonName", "[{0}] Garrison"), siege.defender),
                });
            }
            traffic.ConnectPlayers();
        }

        /// <summary>An armed siege's orbit (waiting for both sides or fighting) gets the server's count of station defenders
        /// (NetFactions.DefenderCount, growing until the fight) of the system's race on top of the paid garrison, free for the
        /// holders, at its level (NetFactions.DefenderLevel: the dearer fighters, hull and gun as a garrison of that level, the
        /// gun NetFactions.DefenderGunBonus harder from DefenderGunLevel), the garrison's relation (the attackers' enemy, the
        /// holders' friend), each back NetFactions.SiegeDefenderRespawnSeconds after its death; they jump out when the siege
        /// ends. Any orbit, also those with no traffic of their own (Thynome, the deep science stations).</summary>
        void UpdateSiegeDefenders(Traffic traffic, NetFactionsClient.SiegeInfo siege)
        {
            var alive = traffic.Ships.FindAll(x => x != null && x.Spec.eventTag == SiegeDefenderTag && !x.Gone && x.Target != null && x.Target.Alive);
            if (siege == null || !siege.armed)
            {
                foreach (var x in alive) x.JumpOut();
                defenderBack.Clear();
                return;
            }
            float now = Time.unscaledTime;
            defenderBack.RemoveAll(t => now >= t);
            int want = siege.defenders - defenderBack.Count;
            if (alive.Count >= want) return;
            int lvl = Mathf.Clamp(siege.defenderLevel, 1, NetFactions.MaxGarrisonLevel);
            float gunBonus = lvl >= NetFactions.DefenderGunLevel ? NetFactions.DefenderGunBonus : 1f;
            int race = level.Layout.raceId;
            int count = Mathf.Min(want - alive.Count, GarrisonSpawnPerTick);
            for (int i = 0; i < count; i++)
            {
                int ship = NpcTables.StrongFighter(level.Database, race);
                traffic.SpawnShip(new SpawnSpec
                {
                    group = NpcGroup.Local, race = race, ship = ship, eventTag = SiegeDefenderTag, noLoot = true,
                    position = new Vector3(Random.Range(-30000f, 30000f), Random.Range(-12000f, 12000f), Random.Range(12000f, 60000f)),
                    hitpoints = Mathf.RoundToInt(NpcTables.Hull(0, ship) * (1f + 0.5f * (lvl - 1))),
                    gunItem = NpcTables.GunItem(race), gunFactor = (1f + 0.25f * (lvl - 1)) * gunBonus,
                    name = string.Format(Localization.Extra("mpSiegeDefenderName", "[{0}] Defender"), siege.defender),
                });
            }
            traffic.ConnectPlayers();
        }

        static int TerritoryToLocal(NpcShip ship)
        {
            var me = NetPlayer.Local;
            return me == null ? 0 : Territory(ship, me.Station, me.FactionTag, NetFactionsClient.TollStation);
        }

        /// <summary>Another member of the local player's squad shot it, or an event turned it on the local player (/provoke).</summary>
        static bool HostileToLocalBySquad(NpcShip ship)
        {
            var me = NetPlayer.Local;
            if (me == null) return false;
            if (ship.aggressors.Contains(me.OwnerClientId)) return true;
            if (me.SquadId == 0) return false;
            foreach (var id in ship.aggressors) if (id != me.OwnerClientId && NetSquad.SameClient(id, me)) return true;
            return false;
        }

        // ---- asteroids --------------------------------------------------------------------------------------

        void OnLocalAsteroidDied(int index)
        {
            if (applyingRemote || NetState.Instance == null || !NetState.Instance.IsSpawned) return;
            NetState.Instance.AsteroidDestroyedRpc(Station, index, Mining.MiningOut);
        }

        /// <summary>An asteroid of 'station' was destroyed by someone ('by': their pilot name; 'mined': they drilled it out,
        /// else shot / rammed / a blast): gone here too (with its explosion).</summary>
        public void OnAsteroidGone(int station, int index, string by, bool mined)
        {
            if (station != Station || index < 0 || index >= asteroids.Count) return;
            var t = asteroids[index];
            if (t == null || !t.Alive || !t.isActiveAndEnabled) return;
            goneBy[t] = (by ?? "", mined);
            applyingRemote = true;
            t.Explode();
            applyingRemote = false;
        }

        readonly Dictionary<Target, (string by, bool mined)> goneBy = new Dictionary<Target, (string, bool)>();

        /// <summary>Mining: another player's game destroyed this asteroid: their pilot name and whether they mined it out
        /// (else shot or rammed it); false = not another player.</summary>
        public bool DestroyedBy(Target asteroid, out string by, out bool mined)
        {
            by = null;
            mined = false;
            if (asteroid == null || !goneBy.TryGetValue(asteroid, out var g)) return false;
            by = g.by;
            mined = g.mined;
            return true;
        }

        /// <summary>This orbit's index of an asteroid (the same for every player: the seeded field), -1 = none.</summary>
        public int IndexOf(Target asteroid) => asteroid != null ? asteroids.IndexOf(asteroid) : -1;

        readonly HashSet<int> held = new HashSet<int>(), heldNow = new HashSet<int>();

        /// <summary>The asteroids another player here lands on or drills stop spinning here too (the miner's own game stops
        /// its spin, Mining.BeginLanding / Docked); they spin on once nobody is on them (not while this player mines one).</summary>
        void UpdateHeldAsteroids()
        {
            heldNow.Clear();
            foreach (var p in NetPlayer.All)
                if (p != null && !p.IsOwner && p.IsSpawned && p.InSpace && p.Station == Station && p.LandedAsteroid >= 0) heldNow.Add(p.LandedAsteroid);
            var mining = level != null ? level.Mining : null;
            var mine = mining != null && mining.State != Mining.Phase.Idle ? mining.Target : null;
            foreach (int i in heldNow)
                if (!held.Contains(i)) SetSpin(i, false);
            foreach (int i in held)
                if (!heldNow.Contains(i) && (mine == null || IndexOf(mine) != i)) SetSpin(i, true);
            held.Clear();
            held.UnionWith(heldNow);
        }

        void SetSpin(int index, bool on)
        {
            if (index < 0 || index >= asteroids.Count || asteroids[index] == null) return;
            var spin = asteroids[index].GetComponent<GoF2Remake.Visuals.Spin>();
            if (spin != null) spin.enabled = on;
        }

        /// <summary>Mining: another player here is landing on, sits on or drills this asteroid.</summary>
        public bool OthersOn(Target asteroid)
        {
            int index = IndexOf(asteroid);
            return index >= 0 && held.Contains(index);
        }

        /// <summary>Mining: how many other players here are drilling this asteroid right now (NetPlayer.MiningAsteroid).</summary>
        public int OtherMiners(Target asteroid)
        {
            int index = IndexOf(asteroid), n = 0;
            if (index < 0) return 0;
            foreach (var p in NetPlayer.All)
                if (p != null && !p.IsOwner && p.IsSpawned && p.InSpace && p.Station == Station && p.MiningAsteroid == index) n++;
            return n;
        }

        /// <summary>Arriving: the asteroids destroyed here before, removed without a trace.</summary>
        public void OnDestroyedList(int station, int[] indices)
        {
            if (station != Station || indices == null) return;
            foreach (int i in indices)
                if (i >= 0 && i < asteroids.Count && asteroids[i] != null) asteroids[i].gameObject.SetActive(false);
        }

        // ---- the authority's ships and crates ---------------------------------------------------------------

        /// <summary>Ship 'localId' (its index in the traffic), null = none.</summary>
        public NpcShip Ship(int localId)
        {
            var ships = level != null && level.Traffic != null ? level.Traffic.Ships : null;
            return ships != null && localId >= 0 && localId < ships.Count ? ships[localId] : null;
        }

        public Crate Crate(int localId) => crates.TryGetValue(localId, out var c) ? c : null;

        /// <summary>Junk 'localId' (JunkBase + its index in the freelance mission's list), null = none.</summary>
        public Target Junk(int localId)
        {
            var junk = level != null && level.FreelanceOrbit != null ? level.FreelanceOrbit.Junk : null;
            int i = localId - JunkBase;
            return junk != null && i >= 0 && i < junk.Count ? junk[i] : null;
        }

        public int JunkKind(int localId) => level != null && level.FreelanceOrbit != null ? level.FreelanceOrbit.JunkKind(localId - JunkBase) : 0;

        public void Register(NetProxy proxy, Target junk) => junkProxies[junk] = proxy;

        // ---- out of sight -----------------------------------------------------------------------------------

        /// <summary>A spawn point (game units) out of the other players' view here: one inside a player's view (a 65 deg cone
        /// along their ship within 60 km, or closer than 1.5 km) is mirrored to behind them, a few passes over everyone.
        /// Outside a session (or alone) the point itself.</summary>
        public static Vector3 OutOfSight(Vector3 gamePos)
        {
            if (!NetGame.Active) return gamePos;
            var u = OrbitLayout.ToUnity(gamePos);
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || !p.SharesOrbit) continue;
                    var eye = p.transform.position;
                    var f = p.transform.forward;
                    var v = u - eye;
                    float dist = v.magnitude;
                    bool seen = dist < NearRange || (dist < SightRange && Vector3.Dot(v / Mathf.Max(dist, 0.001f), f) > SightCos);
                    if (!seen) continue;
                    // Behind them (the mirror image across the plane at their ship), and not right on top of them.
                    var back = v - 2f * Vector3.Dot(v, f) * f;
                    if (Vector3.Dot(back, f) > -NearRange) back -= f * (NearRange * 2f);
                    u = eye + back;
                    moved = true;
                }
                if (!moved) break;
            }
            return new Vector3(u.x, u.y, -u.z) / OrbitLayout.MetersPerUnit;
        }

        public void Register(NetProxy proxy, NpcShip ship) => proxies[ship] = proxy;
        public void Register(NetCrate net, Crate crate) => netCrates[crate] = net;

        /// <summary>The proxy showing 'ship' to the others (for shot targets), null = none.</summary>
        public NetProxy ProxyOf(NpcShip ship) => ship != null && proxies.TryGetValue(ship, out var p) ? p : null;

        void Update()
        {
            var state = NetState.Instance;
            if (state == null || !state.IsSpawned) return;
            if (!requestedList) { requestedList = true; state.RequestDestroyedRpc(Station); }
            WatchTraffic();
            UpdateHeldAsteroids();
            UpdateGarrison();
            // Two players arriving at once both found the orbit empty and built its traffic: the higher client id stands
            // down (its ships go, the other's stay), early in the visit only.
            if (Authority && Time.timeSinceLevelLoad < 15f && OtherAuthorityFirst()) level.DropNetAuthority();
            if (!Authority) TryTakeOver();
            // This player's own NPCs: the orbit's traffic (the authority) or a freelance mission's (anyone).
            bool ownNpcs = Authority || (level.Traffic != null && level.Traffic.Ships.Count > 0) || level.FreelanceOrbit != null;
            if (ownNpcs) UpdateNpcHooks(); else ClearNpcHooks();
            if (!ownNpcs) return;
            if ((scanTimer -= Time.unscaledDeltaTime) > 0f) return;
            scanTimer = ScanSeconds;
            DropOwnStale(state);
            ScanShips(state);
            ScanJunk(state);
            ScanCrates(state);
        }

        bool OtherAuthorityFirst()
        {
            var me = NetPlayer.Local;
            if (me == null) return false;
            foreach (var p in NetPlayer.All)
                if (p != null && p != me && p.IsSpawned && p.InSpace && p.Station == Station && p.OrbitAuthority && p.OwnerClientId < me.OwnerClientId)
                    return true;
            return false;
        }

        /// <summary>Nobody runs this orbit any more (its authority left): the player here with the lowest id takes over
        /// the old authority's flying ships.</summary>
        void TryTakeOver()
        {
            var me = NetPlayer.Local;
            if (me == null || !me.InSpace || me.Station != Station || !NetState.IsOrbitAuthority(Station)) return;
            foreach (var p in NetPlayer.All)
                if (p != null && p != me && p.IsSpawned && p.InSpace && p.Station == Station && p.OwnerClientId < me.OwnerClientId) return;
            var proxies = FindObjectsByType<NetProxy>();
            bool any = false;
            foreach (var proxy in proxies)
                if (proxy.IsSpawned && (!proxy.IsOwner || proxy.Orphan) && proxy.Station == Station) { any = true; break; }
            if (!any && level.Traffic != null && level.Traffic.Ships.Count == 0 && Time.timeSinceLevelLoad < 3f) return;   // wait for them to arrive
            level.TakeOverNetAuthority();
            if (level.Traffic == null) return;
            bool adopted = false;
            foreach (var proxy in proxies)
            {
                // Only ships whose game is gone from here (not a player still flying them: a siege, a mission of theirs).
                if (!proxy.IsSpawned || proxy.Station != Station || !proxy.Adoptable || InOrbit(proxy.Creator, Station)) continue;
                var u = proxy.WorldPosition;
                var ship = level.Traffic.Adopt(proxy.AdoptSpec(new Vector3(u.x, u.y, -u.z) / OrbitLayout.MetersPerUnit), proxy.WorldRotation, proxy.HullFraction);
                foreach (var id in proxy.Aggressors) ship.aggressors.Add(id);   // still hostile to whom it was
                proxy.MarkAdopted();
                NetState.Instance.AdoptedRpc(proxy.NetworkObjectId);   // gone for the others at once (they get this player's)
                adopted = true;
            }
            if (adopted) level.Traffic.ConnectPlayers();   // Level::connectPlayers: the adopted ships' enemy lists
        }

        /// <summary>This player's own proxies left from an earlier visit (or spawned after their ship went): they go.</summary>
        void DropOwnStale(NetState state)
        {
            ulong me = NetGame.LocalId;
            foreach (var proxy in FindObjectsByType<NetProxy>())
                if (proxy.IsSpawned && proxy.IsOwner && proxy.Creator == me && proxy.Orphan && !proxy.Adopted) state.DespawnRpc(proxy.NetworkObjectId);
        }

        void ScanShips(NetState state)
        {
            var ships = level.Traffic != null ? level.Traffic.Ships : null;
            if (ships == null) return;
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];
                bool alive = ship != null && !ship.Gone && !ship.LocalOnly && !string.IsNullOrEmpty(ship.ModelPath);
                if (alive && requestedShips.Add(i)) state.SpawnProxyRpc(Station, i);
                if (alive || !requestedShips.Contains(i)) continue;
                // Gone (dead after its explosion, jumped out): its proxy goes; a relaunch asks for a new one.
                if (ship != null && proxies.TryGetValue(ship, out var proxy))
                {
                    if (proxy != null && proxy.IsSpawned) state.DespawnRpc(proxy.NetworkObjectId);
                    proxies.Remove(ship);
                    requestedShips.Remove(i);
                }
            }
        }

        void ScanJunk(NetState state)
        {
            var junk = level.FreelanceOrbit != null ? level.FreelanceOrbit.Junk : null;
            if (junk == null) return;
            for (int i = 0; i < junk.Count; i++)
            {
                var t = junk[i];
                if (t != null && t.Alive && requestedJunk.Add(i)) state.SpawnProxyRpc(Station, JunkBase + i);
                if (t != null && t.Alive) continue;
                // Destroyed: its proxy goes a moment later (the others see the explosion first).
                if (t == null || !junkProxies.TryGetValue(t, out var proxy)) continue;
                if (!junkDeadSince.TryGetValue(t, out float since)) { junkDeadSince[t] = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < 1.5f) continue;
                if (proxy != null && proxy.IsSpawned) state.DespawnRpc(proxy.NetworkObjectId);
                junkProxies.Remove(t);
            }
        }

        void ScanCrates(NetState state)
        {
            foreach (var crate in FindObjectsByType<Crate>(FindObjectsInactive.Exclude))
            {
                // A crate made by a cargo steal is the stealer's already (pulled at once, the ship keeps the rest): not shared.
                if (crate.remote || crate.stolenFrom != null || crateIds.ContainsKey(crate)) continue;
                int id = nextCrateId++;
                crateIds[crate] = id;
                crates[id] = crate;
                state.SpawnCrateRpc(Station, id);
            }
            List<Crate> gone = null;
            foreach (var pair in crateIds) if (pair.Key == null) (gone ??= new List<Crate>()).Add(pair.Key);
            if (gone == null) return;
            foreach (var crate in gone)
            {
                crates.Remove(crateIds[crate]);
                crateIds.Remove(crate);
                if (netCrates.TryGetValue(crate, out var net))
                {
                    if (net != null && net.IsSpawned) state.DespawnRpc(net.NetworkObjectId);
                    netCrates.Remove(crate);
                }
            }
        }
    }
}
