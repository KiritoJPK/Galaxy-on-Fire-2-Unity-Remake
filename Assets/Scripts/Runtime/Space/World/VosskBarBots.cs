// VosskBarBots.cs
// Remake: the Vossk bar's two bartender arms (bar_vossk_bot_a/b/c_anim_alpha, in the bar_vossk assembly). The original ships
// the three models without a resource id and never shows them: CutScene::process's Vossk branch (mode 4) restarts two
// geometries, CutScene+0x30 every 20000 ms and +0x34 every 22000 ms, but CutScene::initialize only fills those slots for the
// Terran bar (Reference/research/station_interior.md 2.4). Their tracks show two arms: bot_b (15.5 s) is the arm along the
// bar, bot_a (15 s, it turns in place) and bot_c (20.5 s, it travels down the bar and back) are two animations of the arm
// at the bar's end (both start there). So, the dead code's two slots: the second arm replays bot_b every 20 s, the end arm
// bot_a or bot_c (at random) every 22 s, the other one hidden meanwhile; between plays each rests in its first pose. The
// first plays come within a few seconds of entering the bar (the original's timers would wait 20 / 22 s).
// Plain C# (StationLevel drives it, in the lounge view only): Tick says what starts.

using System;

namespace GoF2Remake.World
{
    public sealed class VosskBarBots
    {
        /// <summary>CutScene+0x30 / +0x34's periods.</summary>
        public const float SecondArmEveryMs = 20000f, EndArmEveryMs = 22000f;
        /// <summary>The first plays: within this long of entering the bar.</summary>
        public const float FirstPlayWithinMs = 4000f;

        [Flags] public enum Start { None = 0, EndArm = 1, SecondArm = 2 }

        float endArmMs, secondArmMs;   // until the next play

        /// <summary>The end arm's animation now: false bot_a, true bot_c.</summary>
        public bool EndArmPlaysC { get; private set; }

        /// <summary>'random' gives 0..1 (UnityEngine.Random.value in the game).</summary>
        public VosskBarBots(Func<float> random)
        {
            endArmMs = random() * FirstPlayWithinMs;
            secondArmMs = random() * FirstPlayWithinMs;
        }

        /// <summary>'dtMs' of the lounge in view: which arms start now (EndArm: EndArmPlaysC says which animation).</summary>
        public Start Tick(float dtMs, Func<float> random)
        {
            var s = Start.None;
            if ((endArmMs -= dtMs) <= 0f)
            {
                endArmMs += EndArmEveryMs;
                if (endArmMs <= 0f) endArmMs = EndArmEveryMs;
                EndArmPlaysC = random() < 0.5f;
                s |= Start.EndArm;
            }
            if ((secondArmMs -= dtMs) <= 0f)
            {
                secondArmMs += SecondArmEveryMs;
                if (secondArmMs <= 0f) secondArmMs = SecondArmEveryMs;
                s |= Start.SecondArm;
            }
            return s;
        }
    }
}
