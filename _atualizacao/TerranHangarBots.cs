// TerranHangarBots.cs
// Modified: the Terran hangar's four service bots (hangar_terran_bot_a..d_anim, each with its _anim_add glow layer) fly one
// at a time instead of all looping together: the next is picked at random among the four, never the one that has just
// flown, after a short pause once the last one's animation has ended. Between flights a bot is hidden (their tracks fly
// from one side of the hangar to the other, so a bot parked at either end would hang there in mid-air).
// Plain C# (StationLevel drives it in the hangar view): Tick says which bot starts.

using System;

namespace GoF2Remake.World
{
    public sealed class TerranHangarBots
    {
        /// <summary>The first flight: within this long of the hangar showing.</summary>
        public const float FirstPlayWithinMs = 1500f;
        /// <summary>The pause between one bot's end and the next one's start.</summary>
        public const float GapMinMs = 500f, GapMaxMs = 1500f;

        readonly int count;
        float waitMs;

        /// <summary>The bot flying now (-1 between flights).</summary>
        public int Current { get; private set; } = -1;
        /// <summary>The bot that flew last (-1 before the first flight): never picked next.</summary>
        public int Last { get; private set; } = -1;

        /// <summary>'random' gives 0..1 (UnityEngine.Random.value in the game).</summary>
        public TerranHangarBots(int count, Func<float> random)
        {
            this.count = count;
            waitMs = random() * FirstPlayWithinMs;
        }

        /// <summary>'playing': the current bot's animation still runs. Returns the bot to start now, else -1.</summary>
        public int Tick(float dtMs, bool playing, Func<float> random)
        {
            if (count <= 0) return -1;
            if (Current >= 0)
            {
                if (playing) return -1;
                Last = Current;
                Current = -1;
                waitMs = GapMinMs + random() * (GapMaxMs - GapMinMs);
            }
            if ((waitMs -= dtMs) > 0f) return -1;
            Current = Pick(random);
            return Current;
        }

        /// <summary>A random bot other than Last.</summary>
        int Pick(Func<float> random)
        {
            if (count == 1) return 0;
            int choices = Last >= 0 ? count - 1 : count;
            int pick = Math.Min((int)(random() * choices), choices - 1);
            if (Last >= 0 && pick >= Last) pick++;
            return pick;
        }
    }
}
