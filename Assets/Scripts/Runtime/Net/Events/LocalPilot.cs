// LocalPilot.cs
// The single player's pilot for the event system (EventHost.Pilots without a session): id 0, the pilot name
// (the story's hero, speaker 0: Keith T. Maxwell), read from the scene the game is in: in space (SpaceLevel, its ship
// and health) or docked (StationLevel, not while taking off), the current station.

using GoF2Remake.Data;
using UnityEngine;
using GoF2Remake.Multiplayer;

namespace GoF2Remake.Events
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class LocalPilot : IPilot
    {
        public static readonly LocalPilot Instance = new LocalPilot();

        World.SpaceLevel space;
        World.StationLevel station;
        int foundFrame = -1;

        void Find()
        {
            if (foundFrame == Time.frameCount) return;
            foundFrame = Time.frameCount;
            if (space == null) space = Object.FindAnyObjectByType<World.SpaceLevel>();
            if (station == null) station = Object.FindAnyObjectByType<World.StationLevel>();
        }

        public ulong OwnerClientId => 0;

        /// <summary>The story's hero (speaker 0, Keith T. Maxwell): single player's %player%.</summary>
        public string DisplayName
        {
            get
            {
                string n = StoryTable.SpeakerName(0);
                return string.IsNullOrEmpty(n) ? "Keith" : n;
            }
        }

        public bool InSpace { get { Find(); return space != null && space.Player != null; } }

        public bool InHangar { get { Find(); return station != null && !station.PlayerDeparting; } }

        /// <summary>The hull left, 0..1 like NetPlayer's (0 = destroyed).</summary>
        public float Hull
        {
            get
            {
                Find();
                if (space == null || space.Health == null) return 1f;
                if (space.Health.Dead) return 0f;
                var t = space.Health.Target;
                return t != null && t.maxHp > 0 ? Mathf.Clamp01((float)space.Health.Hp.hull / t.maxHp) : 1f;
            }
        }

        public int Station => Session.StationIndex;

        public Vector3 Position { get { Find(); return space != null && space.Player != null ? space.Player.transform.position : Vector3.zero; } }

        public NetPlayer.Place Where => InSpace ? NetPlayer.Place.Space : InHangar ? NetPlayer.Place.Hangar
                                      : station != null ? NetPlayer.Place.Departing : NetPlayer.Place.None;

        public Quaternion Rotation { get { Find(); return space != null && space.Player != null ? space.Player.transform.rotation : Quaternion.identity; } }

        public int ShipIndex => Session.ShipIndex;
        public bool IsOwner => true;
        public bool IsSpawned => true;
        public bool IsAdmin => true;   // the single player runs every command
        public int SquadId => 0;
    }
}
