// NetArenaClient.cs
// Remake-only: a player's side of an arena match (NetArena). The server's start (ArenaStartRpc) takes the player from
// their hangar into the arena: the profile is uploaded first (nothing of the match is saved), the equipment and its ammo
// are noted, and the Space scene loads in arena mode (SpaceLevel: Current's template orbit, the Void's home, without its
// station, gate, docking, jumps or mining, and without NPCs unless the match has the Void fighters (voids); the
// asteroids around the centre; NetOrbitId = the match's own orbit id). The level
// reports in once (ArenaReadyRpc); the controls and guns stay locked until the server's countdown ends. Destroyed, the
// ship comes back RespawnSeconds later at the spawn point farthest from the other players (the arena reloads, the
// equipment and ammo restored, repaired). The end (ArenaEndRpc) shows the result for ResultSeconds, then the player is
// docked again where they started, everything as before the match; /leave or the server's ArenaLeaveRpc goes home at
// once. ArenaView draws it (score, timer, countdown, kill feed, result) and calls Tick.

using System.Collections.Generic;
using GoF2Remake.Data;
using GoF2Remake.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetArenaClient
    {
        public const float RespawnSeconds = 3.5f, ResultSeconds = 6f;
        /// <summary>The spawn ring's radius (Unity metres) and its points: the players start around the arena's centre,
        /// facing it.</summary>
        const float SpawnRadius = 2000f;
        const int SpawnPoints = 8;
        const string SpaceScene = "Space", StationScene = "Station";

        /// <summary>The match this player is in, null = none.</summary>
        public sealed class Match : World.ArenaMatchInfo
        {
            // orbitId, template, voids (the Void fighters are in the arena: its traffic, run by the first player there)
            public int id, slot, killLimit;
            public NetArena.Kind kind;
            public float seconds;
            public NetArena.Phase phase = NetArena.Phase.Loading;
            public float phaseLeft;                      // seconds: the countdown, or the fight's time left
            public ulong[] players = new ulong[0];
            public int[] kills = new int[0];
            public readonly List<(string text, float until)> feed = new List<(string, float)>();
            public string result;                       // the end's text, null while it runs
            public float resultUntil;
            public bool readySent;
        }

        public static Match Current { get; private set; }
        /// <summary>In a match of the running session: never in single player (a session that ended mid-match left Current
        /// set until the next session, and the next single-player flight got the arena layout, no jumps, no mining and no game
        /// over: a softlock on death).</summary>
        public static bool InMatch => Current != null && NetGame.Active;

        static int homeStation = -1;
        static List<ItemStack> equipment, cargo;
        static int selectedSecondary = -1;
        static float deadSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        /// <summary>A new session (NetGame.PrepareSession) or the main menu after one (NetGame.OnMainMenu): no match.</summary>
        public static void Reset()
        {
            Current = null;
            homeStation = -1;
            equipment = cargo = null;
            deadSeconds = 0f;
        }

        // ---- from the server --------------------------------------------------------------------------------

        internal static void OnStart(int id, byte kind, int orbitId, int template, bool voids, int slot, int killLimit, float seconds)
        {
            if (Current != null) return;
            NetProfileClient.Upload();   // the profile as it is before the match (nothing of the match is saved)
            homeStation = Session.StationIndex;
            equipment = Session.Equipment.ConvertAll(e => e.Clone());
            cargo = Session.Cargo.ConvertAll(e => e.Clone());   // loot from the Void fighters doesn't come home either
            selectedSecondary = Session.SelectedSecondary;
            Current = new Match { id = id, kind = (NetArena.Kind)kind, orbitId = orbitId, template = template, voids = voids, slot = slot, killLimit = killLimit, seconds = seconds };
            var dock = Object.FindAnyObjectByType<StationLevel>();
            if (dock != null) dock.Depart(LoadArena);   // the take-off first (the hangar flights option)
            else LoadArena();
        }

        internal static void OnState(int id, byte phase, float left, ulong[] players, int[] kills, string feed)
        {
            var m = Current;
            if (m == null || m.id != id) return;
            m.phase = (NetArena.Phase)phase;
            m.phaseLeft = left;
            m.players = players ?? new ulong[0];
            m.kills = kills ?? new int[0];
            if (!string.IsNullOrEmpty(feed)) { m.feed.Add((feed, Time.unscaledTime + 5f)); if (m.feed.Count > 4) m.feed.RemoveAt(0); }
        }

        internal static void OnEnd(int id, string text)
        {
            var m = Current;
            if (m == null || m.id != id) return;
            m.phase = NetArena.Phase.Over;
            m.result = text;
            m.resultUntil = Time.unscaledTime + ResultSeconds;
        }

        /// <summary>The server took this player out of the match (their /leave): home at once.</summary>
        internal static void OnLeave() => GoHome();

        // ---- the level --------------------------------------------------------------------------------------

        static void LoadArena()
        {
            if (Current == null) return;
            RestoreLoadout();
            Session.LaunchedFromStation = false;
            Session.ArrivedByTravel = false;
            Session.DockedFromSpace = false;
            SceneManager.LoadScene(SpaceScene);
        }

        /// <summary>The equipment and ammo as they were when the match began, the ship repaired.</summary>
        static void RestoreLoadout()
        {
            if (equipment != null) Session.Equipment = equipment.ConvertAll(e => e.Clone());
            Session.SelectedSecondary = selectedSecondary;
            Session.PlayerHull = Session.PlayerArmor = -1;
            Session.PlayerShield = -1f;
        }

        /// <summary>SpaceLevel, once built in arena mode: the server hears this player is in (the first load only).</summary>
        public static void OnLevelReady()
        {
            var m = Current;
            if (m == null || m.readySent || NetState.Instance == null) return;
            m.readySent = true;
            NetState.Instance.ArenaReadyRpc(m.id);
        }

        /// <summary>SpaceLevel.SpawnPlayer in arena mode: the first time the server's slot, after that the spawn point
        /// farthest from the other players in the arena; facing the centre.</summary>
        public static Pose SpawnPose()
        {
            var m = Current;
            int slot = m != null ? m.slot % SpawnPoints : 0;
            if (m != null && m.readySent)
            {
                float best = -1f;
                for (int i = 0; i < SpawnPoints; i++)
                {
                    var at = SpawnPoint(i);
                    float nearest = float.MaxValue;
                    foreach (var p in NetPlayer.All)
                        if (p != null && !p.IsOwner && p.IsSpawned && p.InSpace && p.Station == m.orbitId)
                            nearest = Mathf.Min(nearest, (p.transform.position - at).sqrMagnitude);
                    if (nearest > best) { best = nearest; slot = i; }
                }
            }
            var pos = SpawnPoint(slot);
            return new Pose(pos, Quaternion.LookRotation(-pos.normalized, Vector3.up));
        }

        /// <summary>Slot 'i' of the ring: a duel's two players face each other across it (slots 0 and 1 are opposite).</summary>
        static Vector3 SpawnPoint(int i)
        {
            int order = (i % 2 == 0 ? i / 2 : i / 2 + SpawnPoints / 2) % SpawnPoints;
            float a = order * Mathf.PI * 2f / SpawnPoints;
            return new Vector3(Mathf.Sin(a), (i % 3 - 1) * 0.08f, Mathf.Cos(a)) * SpawnRadius;
        }

        /// <summary>ArenaView, every frame in flight: the controls during the countdown, the respawn, the way home.</summary>
        public static void Tick(SpaceLevel level)
        {
            var m = Current;
            if (m == null) return;
            float dt = Time.unscaledDeltaTime;
            if (m.phase == NetArena.Phase.Countdown || m.phase == NetArena.Phase.Fighting) m.phaseLeft = Mathf.Max(0f, m.phaseLeft - dt);
            m.feed.RemoveAll(f => f.until < Time.unscaledTime);
            if (m.result != null && Time.unscaledTime >= m.resultUntil) { GoHome(); return; }
            if (level == null || level.Player == null) return;
            bool fight = m.phase == NetArena.Phase.Fighting;
            level.Player.inputLocked = !fight;
            if (level.Weapons != null) level.Weapons.Blocked = !fight;
            if (level.Health != null && level.Health.Dead && m.result == null)
            {
                if ((deadSeconds += dt) >= RespawnSeconds) { deadSeconds = 0f; RestoreLoadout(); SceneManager.LoadScene(SpaceScene); }
            }
            else deadSeconds = 0f;
        }

        /// <summary>Back to the station the match began from, the loadout as it was.</summary>
        static void GoHome()
        {
            if (Current == null) return;
            Current = null;
            deadSeconds = 0f;
            RestoreLoadout();
            if (cargo != null) Session.Cargo = cargo.ConvertAll(e => e.Clone());
            if (homeStation >= 0) Session.StationIndex = homeStation;
            Session.DockedFromSpace = false;
            Session.LaunchedFromStation = false;
            SceneManager.LoadScene(StationScene);
        }

        /// <summary>The player's name for the scoreboard.</summary>
        public static string NameOf(ulong client)
        {
            var p = NetSquad.Find(client);
            return p != null ? p.DisplayName : "?";
        }
    }
}
