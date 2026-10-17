// HudMessageQueue.cs
// The flight HUD's message queue as plain rules (MiningView draws it). The original (Hud::hudEvent, Hud::drawEventQueue
// 0x18f098 / Hud::updateQueue 0x18f2a0): one message at a time for 4000 ms of game time, a queue of 20, in order.
// Remake: a text already showing or waiting isn't queued again (a repeated event held the queue up for a minute).
// Remake (a player's report, 2026-10-17): the transient readouts don't wait their turn. A cargo scan, a "Target: X"
// notice or a cargo gain (HudMessageKind.Scan / Target / Gain) takes the plate at once from another transient one and
// drops a waiting one of its own kind, so locking ship after ship shows each scan straight away instead of a 4 s queue
// of old ones. Normal messages (warnings, mission and story texts, refusals, "Cargo hold full") queue as before and are
// never cut short: a transient one waits behind them and doesn't jump ahead of one already waiting (so repeated scans
// can't hold them back). Gains coming in together (a crate of several stacks, multi-grab tractor beams, the mining
// payout's ore and core) merge into one line within 1 s of the first (or while still waiting): "5t Gold, 1t Gold
// Core", the same item summed, at most 3 parts (a fourth replaces the line).

using System.Collections.Generic;

namespace GoF2Remake.Flight
{
    public enum HudMessageKind { Normal, Scan, Target, Gain }

    public sealed class HudMessageQueue
    {
        public const float Seconds = 4f;        // Hud::updateQueue
        public const int Capacity = 20;
        public const float MergeSeconds = 1f;
        public const int MaxMergedParts = 3;

        public struct Message
        {
            public string text;
            public int colour;   // Hud::drawEventQueue: 0 white, 1 red, 2 green, 3 orange
            public HudMessageKind kind;
            public Message(string text, int colour, HudMessageKind kind) { this.text = text; this.colour = colour; this.kind = kind; }
        }

        readonly List<Message> waiting = new List<Message>();
        Message current;
        float start, end = -1f;

        public bool Showing => end >= 0f;
        public Message Current => current;
        public int WaitingCount => waiting.Count;
        /// <summary>Bumped whenever the shown message (or its text) changes.</summary>
        public int Version { get; private set; }

        public static bool IsTransient(HudMessageKind kind) => kind != HudMessageKind.Normal;

        public void Post(string text, int colour, HudMessageKind kind, float now)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!IsTransient(kind))
            {
                if (waiting.Count >= Capacity) return;
                if (Showing && current.text == text) return;
                foreach (var m in waiting) if (m.text == text) return;
                waiting.Add(new Message(text, colour, kind));
                Update(now);
                return;
            }

            // A gain still waiting takes the new one in (it hasn't been seen yet); another kind's waiting one is replaced.
            int same = waiting.FindIndex(m => m.kind == kind);
            if (same >= 0 && kind == HudMessageKind.Gain)
            {
                var merged = MergeGains(waiting[same].text, text);
                if (merged != null) { waiting[same] = new Message(merged, colour, kind); return; }
            }
            waiting.RemoveAll(m => m.kind == kind);
            Update(now);
            if (Showing && IsTransient(current.kind))
            {
                if (kind == HudMessageKind.Gain && current.kind == HudMessageKind.Gain && now - start < MergeSeconds)
                {
                    var merged = MergeGains(current.text, text);
                    if (merged != null)
                    {
                        current = new Message(merged, colour, kind);
                        end = now + Seconds;
                        Version++;
                        return;
                    }
                }
                if (!waiting.Exists(m => !IsTransient(m.kind)))
                {
                    Show(new Message(text, colour, kind), now);
                    return;
                }
            }
            if (waiting.Count >= Capacity) return;
            waiting.Add(new Message(text, colour, kind));
            Update(now);
        }

        /// <summary>Ends the shown message after its time and shows the next.</summary>
        public void Update(float now)
        {
            if (Showing && now >= end) { end = -1f; Version++; }
            if (!Showing && waiting.Count > 0)
            {
                var next = waiting[0];
                waiting.RemoveAt(0);
                Show(next, now);
            }
        }

        public void Clear()
        {
            waiting.Clear();
            if (Showing) { end = -1f; Version++; }
        }

        void Show(Message m, float now)
        {
            current = m;
            start = now;
            end = now + Seconds;
            Version++;
        }

        /// <summary>"5t Gold" + "3t Gold" = "8t Gold"; + "1t Gold Core" = "8t Gold, 1t Gold Core". Null when a part
        /// isn't "[+]Nt name" or there would be more than MaxMergedParts.</summary>
        public static string MergeGains(string shown, string added)
        {
            if (!ParseGain(added, out var plus, out int amount, out var name)) return null;
            var parts = shown.Split(new[] { ", " }, System.StringSplitOptions.None);
            var outParts = new List<string>(parts.Length + 1);
            bool summed = false;
            foreach (var p in parts)
            {
                if (!ParseGain(p, out var pPlus, out int pAmount, out var pName)) return null;
                if (!summed && pName == name && pPlus == plus) { outParts.Add($"{plus}{(long)pAmount + amount}t {name}"); summed = true; }
                else outParts.Add(p);
            }
            if (!summed)
            {
                if (outParts.Count >= MaxMergedParts) return null;
                outParts.Add(added);
            }
            return string.Join(", ", outParts);
        }

        static bool ParseGain(string text, out string plus, out int amount, out string name)
        {
            plus = ""; amount = 0; name = null;
            if (string.IsNullOrEmpty(text)) return false;
            int i = 0;
            if (text[0] == '+') { plus = "+"; i = 1; }
            int digits = i;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9') i++;
            if (i == digits || i + 2 >= text.Length || text[i] != 't' || text[i + 1] != ' ') return false;
            if (!int.TryParse(text.Substring(digits, i - digits), out amount)) return false;
            name = text.Substring(i + 2);
            return true;
        }
    }
}
