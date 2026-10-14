// DiscordIpc.cs
// A minimal Discord Rich Presence client over the desktop app's local IPC (no SDK, no network): the named pipe
// \\.\pipe\discord-ipc-0..9; frames of (int32 opcode, int32 length, UTF-8 JSON). Handshake (op 0) with the application
// id, then SET_ACTIVITY commands (op 1). Runs on its own thread: connects when Discord is running, retries every 15 s,
// sends the latest activity at most every 4 s (Discord allows 5 updates per 20 s), and drops it silently on any error.
// Closing the pipe (quitting) clears the presence. Windows only for now (macOS / Linux use a Unix socket instead).

using System;
using System.IO;
using System.Text;
using System.Threading;

namespace GoF2Remake.UI
{
    public sealed class DiscordIpc : IDisposable
    {
        readonly string clientId;
        readonly object gate = new object();
        string pendingActivity;    // JSON object, or "null" to clear; null = nothing new
        Thread thread;
        volatile bool running;

        public DiscordIpc(string clientId) => this.clientId = clientId;

        public void Start()
        {
            if (running) return;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "DiscordIpc" };
            thread.Start();
        }

        /// <summary>The activity to show (a JSON object), or null to clear it.</summary>
        public void SetActivity(string activityJson)
        {
            lock (gate) pendingActivity = activityJson ?? "null";
        }

        public void Dispose() => running = false;

        void Run()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            string lastSent = null;
            while (running)
            {
                Stream pipe = Connect();
                if (pipe == null) { Sleep(15000); continue; }
                try
                {
                    Write(pipe, 0, "{\"v\":1,\"client_id\":\"" + clientId + "\"}");
                    Read(pipe);   // READY
                    UnityEngine.Debug.Log("DiscordIpc: connected to Discord");
                    lastSent = null;
                    while (running)
                    {
                        string activity;
                        lock (gate) { activity = pendingActivity; pendingActivity = null; }
                        if (activity != null && activity != lastSent)
                        {
                            string nonce = Guid.NewGuid().ToString("N");
                            Write(pipe, 1, "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + pid + ",\"activity\":" + activity + "},\"nonce\":\"" + nonce + "\"}");
                            string reply = Read(pipe);
                            if (reply.Contains("\"evt\":\"ERROR\"")) UnityEngine.Debug.LogWarning("DiscordIpc: " + reply);
                            lastSent = activity;
                            Sleep(4000);
                        }
                        else Sleep(250);
                    }
                }
                catch (Exception) { /* Discord closed or the pipe broke: reconnect later */ }
                finally { try { pipe.Dispose(); } catch (Exception) { } }
                if (running) Sleep(15000);
            }
#endif
        }

        void Sleep(int ms)
        {
            for (int t = 0; t < ms && running; t += 250) Thread.Sleep(250);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static Stream Connect()
        {
            for (int i = 0; i < 10; i++)
            {
                var pipe = new System.IO.Pipes.NamedPipeClientStream(".", "discord-ipc-" + i, System.IO.Pipes.PipeDirection.InOut);
                try { pipe.Connect(200); return pipe; }
                catch (Exception) { pipe.Dispose(); }
            }
            return null;
        }
#endif

        static void Write(Stream s, int op, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            s.Write(frame, 0, frame.Length);
            s.Flush();
        }

        static string Read(Stream s)
        {
            byte[] header = ReadExactly(s, 8);
            int op = BitConverter.ToInt32(header, 0), length = BitConverter.ToInt32(header, 4);
            if (length < 0 || length > 1 << 20) throw new IOException("bad frame");
            string json = Encoding.UTF8.GetString(ReadExactly(s, length));
            if (op == 2) throw new IOException("closed: " + json);   // CLOSE
            return json;
        }

        static byte[] ReadExactly(Stream s, int n)
        {
            var buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = s.Read(buf, got, n - got);
                if (r <= 0) throw new IOException("pipe closed");
                got += r;
            }
            return buf;
        }

        /// <summary>A JSON string literal.</summary>
        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
