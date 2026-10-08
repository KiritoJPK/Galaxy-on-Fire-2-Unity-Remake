// FileDialog.cs
// Remake-only: the system's save / open file dialog for SaveTransfer (and, on Windows, the mod browser's Import mod). The Editor uses EditorUtility's panels; a Windows
// player calls comdlg32's GetSaveFileNameW / GetOpenFileNameW (modal: the game waits while it is open). Elsewhere there
// is no dialog (Available false) and SaveTransfer uses its Transfer folder.

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GoF2Remake.Data
{
    public static class FileDialog
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        public static bool Available => true;
#else
        public static bool Available => false;
#endif

        static string StartFolder => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        /// <summary>Asks where to write a file ('extension' without the dot). Null: cancelled / no dialog.</summary>
        public static string Save(string defaultName, string extension)
        {
#if UNITY_EDITOR
            string p = UnityEditor.EditorUtility.SaveFilePanel("Export saves", StartFolder, Path.GetFileNameWithoutExtension(defaultName), extension);
            return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
            return Win32.Show(true, defaultName, extension);
#else
            return null;
#endif
        }

        /// <summary>Asks for a file to read ('extension' without the dot). Null: cancelled / no dialog.</summary>
        public static string Open(string extension)
        {
#if UNITY_EDITOR
            string p = UnityEditor.EditorUtility.OpenFilePanel("Import saves", StartFolder, extension);
            return string.IsNullOrEmpty(p) ? null : p;
#elif UNITY_STANDALONE_WIN
            return Win32.Show(false, null, extension);
#else
            return null;
#endif
        }

        /// <summary>Asks for a file to read with its own title and filter ('extensions' without the dots; the Mods' Import
        /// mod). Windows player only (the Editor and the other platforms have their own pickers, Modding.ModImport). Null:
        /// cancelled / no dialog.</summary>
        public static string Open(string title, string filterName, params string[] extensions)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return Win32.Show(false, null, extensions.Length > 0 ? extensions[0] : "", title, filterName, extensions);
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static class Win32
        {
            [StructLayout(LayoutKind.Sequential)]
            struct OpenFileName
            {
                public int lStructSize;
                public IntPtr hwndOwner, hInstance, lpstrFilter, lpstrCustomFilter;
                public int nMaxCustFilter, nFilterIndex;
                public IntPtr lpstrFile;
                public int nMaxFile;
                public IntPtr lpstrFileTitle;
                public int nMaxFileTitle;
                public IntPtr lpstrInitialDir, lpstrTitle;
                public int Flags;
                public short nFileOffset, nFileExtension;
                public IntPtr lpstrDefExt, lCustData, lpfnHook, lpTemplateName, pvReserved;
                public int dwReserved, FlagsEx;
            }

            const int OFN_OVERWRITEPROMPT = 0x2, OFN_NOCHANGEDIR = 0x8, OFN_PATHMUSTEXIST = 0x800, OFN_FILEMUSTEXIST = 0x1000,
                OFN_EXPLORER = 0x80000;
            const int MaxPath = 4096;

            [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", SetLastError = true)]
            static extern bool GetSaveFileName(ref OpenFileName ofn);

            [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", SetLastError = true)]
            static extern bool GetOpenFileName(ref OpenFileName ofn);

            [DllImport("user32.dll")]
            static extern IntPtr GetActiveWindow();

            public static string Show(bool save, string defaultName, string extension, string titleText = null, string filterName = null,
                                      string[] extensions = null)
            {
                IntPtr filter = IntPtr.Zero, file = IntPtr.Zero, dir = IntPtr.Zero, title = IntPtr.Zero, defExt = IntPtr.Zero;
                try
                {
                    extensions ??= new[] { extension };
                    string patterns = string.Join(";", Array.ConvertAll(extensions, e => "*." + e));
                    filter = Marshal.StringToHGlobalUni($"{filterName ?? "Galaxy on Fire 2 saves"} ({patterns})\0{patterns}\0\0");
                    file = Marshal.AllocHGlobal(MaxPath * 2);
                    var name = new char[MaxPath];
                    if (!string.IsNullOrEmpty(defaultName)) defaultName.CopyTo(0, name, 0, Math.Min(defaultName.Length, MaxPath - 1));
                    Marshal.Copy(name, 0, file, MaxPath);
                    dir = Marshal.StringToHGlobalUni(StartFolder ?? "");
                    title = Marshal.StringToHGlobalUni(titleText ?? (save ? "Export saves" : "Import saves"));
                    defExt = Marshal.StringToHGlobalUni(extension);
                    var ofn = new OpenFileName
                    {
                        lStructSize = Marshal.SizeOf<OpenFileName>(),
                        hwndOwner = GetActiveWindow(),
                        lpstrFilter = filter,
                        nFilterIndex = 1,
                        lpstrFile = file,
                        nMaxFile = MaxPath,
                        lpstrInitialDir = dir,
                        lpstrTitle = title,
                        lpstrDefExt = defExt,
                        Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
                    };
                    bool ok = save ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn);
                    if (!ok) return null;   // cancelled (or CommDlgExtendedError; either way no file)
                    string path = Marshal.PtrToStringUni(file);
                    return string.IsNullOrEmpty(path) ? null : path;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("FileDialog: " + e.Message);
                    return null;
                }
                finally
                {
                    foreach (var p in new[] { filter, file, dir, title, defExt })
                        if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
                }
            }
        }
#endif
    }
}
