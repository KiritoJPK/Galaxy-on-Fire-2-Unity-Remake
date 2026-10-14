// ConsoleInput.cs
// The dedicated server console's own line editing (DedicatedServer), so Tab can complete the commands: the keys are read one
// at a time (Windows: the server's console window in raw mode, WinConsole.ReadKey; Linux: Console.ReadKey on a terminal)
// and the input line under the log is drawn here, after a "> " prompt. Enter runs the line (echoed above as "> line"),
// Backspace deletes, Esc clears, Up / Down walk the lines run before; Tab completes the command name typed so far and
// cycles through the matching ones on every press (Shift+Tab back; nothing typed: every command), like the game's chat
// (ChatView). Typing a character starts over from what is typed. Log lines written meanwhile go above the input line
// (WriteLine: the line is cleared, the text written, the line drawn again).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    static class ConsoleInput
    {
        const string Prompt = "> ";
        const int HistorySize = 50;

        /// <summary>One key: the character it types ('\0' = none) and the keys that edit the line.</summary>
        public struct Key
        {
            public char character;
            public bool enter, backspace, escape, tab, shift, up, down;
        }

        static readonly object sync = new object();
        static readonly List<string> history = new List<string>();
        static TextWriter output;
        static string[] commandNames = new string[0];
        static string line = "";
        static int historyIndex;
        // Tab completion: the name typed before the first Tab (null = not cycling) and the match shown.
        static string completionPrefix;
        static int completionIndex;
        static bool clearWithEscape;

        /// <summary>The line editor runs (else the console reads whole lines and WriteLine is a plain write).</summary>
        public static bool Active { get; private set; }

        /// <summary>Reads keys on a background thread until readKey returns null (the input closed). 'vtClear': the console
        /// takes the "erase line" escape sequence (else the line is blanked with spaces).</summary>
        public static void Start(Func<Key?> readKey, TextWriter writer, Action<string> submit, string[] names, bool vtClear)
        {
            output = writer;
            commandNames = names ?? new string[0];
            clearWithEscape = vtClear;
            Active = true;
            lock (sync) Draw();
            var thread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        var key = readKey();
                        if (key == null) break;
                        string run = Handle(key.Value);
                        if (run != null) submit(run);
                    }
                }
                catch (Exception) { }
                Active = false;
            }) { IsBackground = true, Name = "Server console keys" };
            thread.Start();
        }

        /// <summary>A line above the input line (log, answers).</summary>
        public static void WriteLine(string text)
        {
            lock (sync)
            {
                if (!Active) { output.WriteLine(text); return; }
                Clear();
                output.WriteLine(text);
                Draw();
            }
        }

        /// <summary>One key; returns the line to run on Enter.</summary>
        static string Handle(Key k)
        {
            lock (sync)
            {
                if (k.enter)
                {
                    string run = line;
                    Clear();
                    if (run.Trim().Length > 0)
                    {
                        output.WriteLine(Prompt + run);
                        if (history.Count == 0 || history[history.Count - 1] != run) history.Add(run);
                        if (history.Count > HistorySize) history.RemoveAt(0);
                    }
                    historyIndex = history.Count;
                    SetLine("", true);
                    return run.Trim().Length > 0 ? run : null;
                }
                if (k.tab) { Complete(k.shift ? -1 : 1); return null; }
                if (k.up || k.down)
                {
                    if (history.Count == 0) return null;
                    historyIndex = Math.Max(0, Math.Min(history.Count, historyIndex + (k.up ? -1 : 1)));
                    SetLine(historyIndex < history.Count ? history[historyIndex] : "", true);
                    return null;
                }
                if (k.escape) { SetLine("", true); return null; }
                if (k.backspace) { if (line.Length > 0) SetLine(line.Substring(0, line.Length - 1), true); return null; }
                if (k.character >= ' ') SetLine(line + k.character, true);
                return null;
            }
        }

        /// <summary>Tab: the first command matching the name typed (no space yet), then the next one each time, wrapping.</summary>
        static void Complete(int dir)
        {
            string prefix = completionPrefix;
            if (prefix == null)
            {
                if (line.IndexOf(' ') >= 0) return;   // past the command name
                prefix = line.Trim().ToLowerInvariant();
            }
            var matches = Array.FindAll(commandNames, n => n.StartsWith(prefix, StringComparison.Ordinal));
            if (matches.Length == 0) return;
            if (completionPrefix == null)
            {
                completionPrefix = prefix;
                completionIndex = dir > 0 ? 0 : matches.Length - 1;
            }
            else completionIndex = ((completionIndex + dir) % matches.Length + matches.Length) % matches.Length;
            SetLine(matches[completionIndex], false);
        }

        /// <summary>A new input line, drawn; 'typed' (not Tab's) ends a completion cycle.</summary>
        static void SetLine(string text, bool typed)
        {
            if (typed) completionPrefix = null;
            Clear();
            line = text;
            Draw();
        }

        static void Clear()
        {
            if (clearWithEscape) output.Write("\r\u001b[2K");
            else output.Write("\r" + new string(' ', Prompt.Length + line.Length) + "\r");
        }

        static void Draw() => output.Write(Prompt + line);
    }
}
