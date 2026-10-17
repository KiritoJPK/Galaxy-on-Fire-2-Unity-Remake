// TargetLockRules.cs
// The remake's ship lock rules (CombatRadar; CLAUDE.md "NPCs and combat", Radar), plain C# so the pick can be tested:
//   priority   Settings.OriginalTargetLock off (the "Smart" lock): among the ships in the crosshair box the hostile ones
//              come first, then the one nearest the crosshair. The original (Radar::draw) takes the first of the list.
//   hold       Settings.KeepEnemyLock, only with the priority: while a hostile ship is locked and alive no neutral or
//              friendly ship becomes a candidate. Players asked for the two apart: the priority without the hold lets a
//              neutral ship be scanned in a battle.
//   release    the Unlock target control (GameControls.UnlockTarget): the lock is dropped and that ship is no candidate
//              again until it has left the crosshair box (in both lock modes), so the priority doesn't take it straight back
//              and any other ship in the box can be locked.

namespace GoF2Remake.Flight
{
    public static class TargetLockRules
    {
        /// <summary>Added to a non-hostile ship's score: any hostile ship in the box comes first.</summary>
        public const float NonHostilePenalty = 1e9f;

        /// <summary>Whether the lock is held for the enemy: no neutral or friendly candidate meanwhile.</summary>
        public static bool HoldsEnemy(bool priority, bool keepEnemyLock, bool lockedHostileAlive) =>
            priority && keepEnemyLock && lockedHostileAlive;

        /// <summary>Whether a ship in the box may become the candidate.</summary>
        public static bool Eligible(bool holdsEnemy, bool hostile, bool released) => !released && (!holdsEnemy || hostile);

        /// <summary>The priority's score (lower wins): the squared screen distance to the crosshair, hostile ships first.</summary>
        public static float Score(float dx, float dy, bool hostile) => dx * dx + dy * dy + (hostile ? 0f : NonHostilePenalty);

        /// <summary>The released ship stays released while it is alive and in the crosshair box.</summary>
        public static bool StillReleased(bool alive, bool inBox) => alive && inBox;
    }
}
