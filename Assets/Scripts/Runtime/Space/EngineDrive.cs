// EngineDrive.cs
// Remake-only: how hard a ship's engines run, for the effects that follow them (ThrottleGlow, Modding.EngineFlame):
// 0 = idle, 1 = full throttle, up to 2 = the height of a boost (FlightModel.BoostVisualPercent); the brake counts as 0.
// Travelling (the planet jump: Navigation.Jumping; a jumpgate or Khador jump: SystemJump.Traveling) counts as a full boost,
// and the Khador Drive's charge builds up toward it. The player's ship reads its ShipController; any other copy (NPC,
// another player's ship in multiplayer, the hangar) has no throttle at hand, so it uses how fast the ship actually moves
// against the base speed every ship shares (FlightModel.BaseSpeed, game units per ms; above it = boosting).
// An inactive 'engineState' (a player engine part the game turns off while mining, object docking, cutscenes) reads 0.
// Free flight with no forward thrust held (FlightModel.EnginesOff) reads 0 with Off set: the effects treat it as engines off.
// A ship under an EnginePreview (the hangar's Customize screen) reads its 'drive'.

using GoF2Remake.Flight;
using UnityEngine;

namespace GoF2Remake.Visuals
{
    public class EngineDrive
    {
        const float MetersPerUnit = 0.05f;

        readonly Transform transform;
        ShipController ship;
        Navigation nav;
        World.SystemJump systemJump;
        EnginePreview preview;
        float lookupTimer;   // the level adds Navigation / SystemJump to the player after spawning the model: look again
        Vector3 lastPosition;
        bool hasLast;
        float measured;      // the moved-speed estimate (0..2: 1 = base speed)

        public EngineDrive(Transform transform)
        {
            this.transform = transform;
            Lookup();
        }

        void Lookup()
        {
            if (ship == null) ship = transform.GetComponentInParent<ShipController>();
            if (nav == null) nav = transform.GetComponentInParent<Navigation>();
            if (systemJump == null) systemJump = transform.GetComponentInParent<World.SystemJump>();
            preview = transform.GetComponentInParent<EnginePreview>();
        }

        /// <summary>Forget the last position (after a scene change or a re-enable: no speed from the jump).</summary>
        public void Reset() { hasLast = false; Lookup(); }

        /// <summary>The player's main engines are off (free flight with no forward thrust held: FlightModel.EnginesOff), as
        /// of the last Sample: the effects go dark as with the engine part off.</summary>
        public bool Off { get; private set; }

        /// <summary>The engines run for a look at them (EnginePreview): the station's cap at full throttle doesn't apply.</summary>
        public bool Previewing => preview != null && preview.isActiveAndEnabled;

        /// <summary>0 idle .. 1 full throttle .. 2 full boost, for this frame ('dt' seconds).</summary>
        public float Sample(float dt, GameObject engineState = null)
        {
            Off = false;
            if ((nav == null || systemJump == null || ship == null) && (lookupTimer -= Time.unscaledDeltaTime) <= 0f)
            {
                lookupTimer = 0.5f;
                Lookup();
            }
            if (engineState != null && !engineState.activeInHierarchy) { hasLast = false; measured = 0f; return 0f; }
            if (preview != null && preview.isActiveAndEnabled) return preview.drive;
            if ((nav != null && nav.Jumping) || (systemJump != null && systemJump.Traveling)) return 2f;
            if (ship != null && ship.Model != null)
            {
                var m = ship.Model;
                Off = m.EnginesOff && !(systemJump != null && systemJump.Charging);
                float d = (m.Braking ? 0f : m.Throttle) + m.BoostVisualPercent;
                if (systemJump != null && systemJump.Charging) d = Mathf.Max(d, 1f + systemJump.ChargeRate);
                return d;
            }
            var p = transform.position;
            if (hasLast && dt > 0f)
            {
                float unitsPerMs = (p - lastPosition).magnitude / dt / MetersPerUnit / 1000f;
                // A jump or respawn moves the ship in one frame: ignore it.
                if (unitsPerMs < FlightModel.BaseSpeed * 6f)
                    measured = Mathf.Lerp(measured, Mathf.Clamp(unitsPerMs / FlightModel.BaseSpeed, 0f, 2f), 1f - Mathf.Exp(-dt * 5f));
            }
            lastPosition = p;
            hasLast = true;
            return measured;
        }
    }
}
