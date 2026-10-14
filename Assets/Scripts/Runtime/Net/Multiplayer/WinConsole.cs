// WinConsole.cs
// The dedicated server's console on Windows (DedicatedServer): a Windows player is a GUI program, started without a console
// and with no standard handles unless they were redirected. Open makes a console window of its own; Input / Output are
// streams on the standard handles (the new console's, or the redirected ones); OnClose runs on Ctrl+C, Ctrl+Break and the
// window's close button (Windows gives the handler a few seconds before it ends the process on a close). RawInput / ReadKey:
// the keys one at a time for the console's own line editing (ConsoleInput: Tab completion), EnableVt the erase-line
// escape sequence.

#if UNITY_STANDALONE_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    static class WinConsole
    {
        const int StdInput = -10, StdOutput = -11;
        const uint Utf8 = 65001, FileTypeDisk = 1, FileTypePipe = 3;

        delegate bool CtrlHandler(uint ctrlType);
        static CtrlHandler handler;   // kept alive: the native side holds only a pointer

        /// <summary>The standard output is redirected to a file or a pipe. A console handle inherited from the terminal that
        /// started this GUI program doesn't count: it isn't attached to that console, so nothing written there shows.</summary>
        public static bool HasOutput()
        {
            IntPtr h = GetStdHandle(StdOutput);
            if (h == IntPtr.Zero || h == new IntPtr(-1)) return false;
            uint type = GetFileType(h);
            return type == FileTypeDisk || type == FileTypePipe;
        }

        public static void Open(string title)
        {
            if (GetConsoleWindow() == IntPtr.Zero && !AllocConsole()) throw new IOException("AllocConsole failed");
            // The new console's own handles (an inherited terminal handle would still be in the slots).
            SetStdHandle(StdInput, CreateFile("CONIN$", GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero));
            SetStdHandle(StdOutput, CreateFile("CONOUT$", GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero));
            SetConsoleOutputCP(Utf8);
            SetConsoleCP(Utf8);
            SetConsoleTitle(title);
        }

        public static Stream Input() => Open(StdInput, FileAccess.Read);
        public static Stream Output() => Open(StdOutput, FileAccess.Write);

        static Stream Open(int std, FileAccess access)
        {
            IntPtr h = GetStdHandle(std);
            if (h == IntPtr.Zero || h == new IntPtr(-1)) return null;
            return new FileStream(new SafeFileHandle(h, false), access);
        }

        /// <summary>The input is the console (not a pipe): its line editing and echo off, so ReadKey gets every key (Ctrl+C
        /// still reaches OnClose's handler). False = leave it as it is.</summary>
        public static bool RawInput()
        {
            IntPtr h = GetStdHandle(StdInput);
            if (h == IntPtr.Zero || h == new IntPtr(-1) || !GetConsoleMode(h, out uint mode)) return false;
            return SetConsoleMode(h, (mode & ~(EnableLineInput | EnableEchoInput)) | EnableProcessedInput);
        }

        /// <summary>The output takes VT escape sequences (Windows 10+): true when switched on.</summary>
        public static bool EnableVt()
        {
            IntPtr h = GetStdHandle(StdOutput);
            return h != IntPtr.Zero && h != new IntPtr(-1) && GetConsoleMode(h, out uint mode) && SetConsoleMode(h, mode | EnableVirtualTerminalProcessing);
        }

        /// <summary>The next key press (blocking; key releases and other events skipped), null when the input is gone.</summary>
        public static ConsoleInput.Key? ReadKey()
        {
            IntPtr h = GetStdHandle(StdInput);
            while (true)
            {
                if (!ReadConsoleInput(h, out InputRecord r, 1, out uint read) || read == 0) return null;
                if (r.EventType != KeyEvent || r.KeyDown == 0) continue;
                return new ConsoleInput.Key
                {
                    character = r.UnicodeChar,
                    enter = r.VirtualKeyCode == 0x0D, backspace = r.VirtualKeyCode == 0x08, escape = r.VirtualKeyCode == 0x1B,
                    tab = r.VirtualKeyCode == 0x09, up = r.VirtualKeyCode == 0x26, down = r.VirtualKeyCode == 0x28,
                    shift = (r.ControlKeyState & ShiftPressed) != 0,
                };
            }
        }

        public static void OnClose(Action action)
        {
            closeAction = action;
            handler = OnCtrl;
            SetConsoleCtrlHandler(handler, true);
        }

        static Action closeAction;

        // IL2CPP passes only static methods marked like this to native code (a lambda's closure can't be marshalled).
        [AOT.MonoPInvokeCallback(typeof(CtrlHandler))]
        static bool OnCtrl(uint ctrlType)
        {
            closeAction?.Invoke();
            return true;
        }

        const uint EnableProcessedInput = 1, EnableLineInput = 2, EnableEchoInput = 4, EnableVirtualTerminalProcessing = 4, ShiftPressed = 0x10;
        const ushort KeyEvent = 1;

        /// <summary>INPUT_RECORD with its KEY_EVENT_RECORD (20 bytes).</summary>
        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        struct InputRecord
        {
            [FieldOffset(0)] public ushort EventType;
            [FieldOffset(4)] public int KeyDown;
            [FieldOffset(8)] public ushort RepeatCount;
            [FieldOffset(10)] public ushort VirtualKeyCode;
            [FieldOffset(12)] public ushort VirtualScanCode;
            [FieldOffset(14)] public char UnicodeChar;
            [FieldOffset(16)] public uint ControlKeyState;
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetConsoleMode(IntPtr handle, out uint mode);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetConsoleMode(IntPtr handle, uint mode);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ReadConsoleInputW")]
        static extern bool ReadConsoleInput(IntPtr handle, out InputRecord record, uint length, out uint read);
        const uint GenericRead = 0x80000000, GenericWrite = 0x40000000, FileShareRead = 1, FileShareWrite = 2, OpenExisting = 3;
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetStdHandle(int std, IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetStdHandle(int std);
        [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr handle);
        [DllImport("kernel32.dll")] static extern bool SetConsoleOutputCP(uint codePage);
        [DllImport("kernel32.dll")] static extern bool SetConsoleCP(uint codePage);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetConsoleTitle(string title);
        [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(CtrlHandler handler, bool add);
    }
}
#endif
