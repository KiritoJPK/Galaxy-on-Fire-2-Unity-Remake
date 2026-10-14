// LinuxServerBuild.cs
// GoF2 > Build > Linux Dedicated Server: a headless Linux server build without the game's graphics and sound. Unity's
// Dedicated Server build (Linux Standalone, subtarget Server; the Unity Hub module "Linux Dedicated Server Build Support")
// with the dedicated server optimizations on: textures, audio and shader data are left out (the server draws and plays
// nothing), meshes keep only what isn't drawing data. Only the first scene goes in (the main menu, which the server
// switches off as it wakes: DedicatedServer.ShutOff); Resources/GoF2Data, the text tables, the web admin page, the events
// and the network prefabs stay, so the server runs exactly as the full build's -server mode. It starts as a server with no
// -server flag (UNITY_SERVER: DedicatedServer.Enabled), and the post-build step writes start-server.sh next to it
// (DedicatedServerLaunchers). Same code = same multiplayer fingerprint, so it serves the normal builds.
// Output: Build/LinuxServer/GoF2Server.x86_64. The active build target is switched to it for the build (BuildTargetGuard
// and URP want the build's own target active) and put back afterwards, the standalone subtarget too.
// GoF2 > Build > Linux Dedicated Server (Release Zip): the same build zipped as Build/Release/GoF2Remake-LinuxServer-<tag>.zip
// (top folder GoF2Server, the executables marked executable), built with the release's other platforms under the same
// GOF2_BUILD_VERSION, whose date is the tag. Attached to the GitHub release, it is what the server image workflow
// (.github/workflows/server-image.yml) puts in the Docker image, so the image and the release are one build.
// The IL2CPP toolchain packages (com.unity.toolchain.win-x86_64-linux, com.unity.sdk.linux-x86_64) only join the Linux
// build while the Editor's scripts are compiled for a Linux target (their UnityEditor.Sysroot base class sits behind
// UNITY_STANDALONE_LINUX_API), so the menu items switch the target first, let the Editor recompile and build after the
// domain reload (SessionState carries the job across it); built in the same call as the switch, IL2CPP found no toolchain
// ("No Linux sysroot found for x64"). The command line (docker/build-server.sh) starts the Editor on the Linux target and
// builds at once. The server build leaves the audio out (ServerAudioStrip: 171 MB) and uses Minimal managed stripping with
// IL2CPP's size-optimised code generation (the server target had stripping off: whole assemblies went in).

using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GoF2Remake.EditorTools
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class LinuxServerBuild
    {
        const string Output = "Build/LinuxServer/GoF2Server.x86_64";
        const string FirstScene = "Assets/Scenes/MainMenu.unity";

        // The job across the domain reload: "build" / "zip" while it waits for the Linux scripts, the settings to put back,
        // and the result (the build's or zip's path, empty when it failed).
        const string JobKey = "GoF2.LinuxServerBuild.Job";
        const string ResultKey = "GoF2.LinuxServerBuild.Result";
        const string PrevTargetKey = "GoF2.LinuxServerBuild.PrevTarget";
        const string PrevSubtargetKey = "GoF2.LinuxServerBuild.PrevSubtarget";
        const string PrevBackendKey = "GoF2.LinuxServerBuild.PrevBackend";
        const string PrevOptimizationsKey = "GoF2.LinuxServerBuild.PrevOptimizations";
        const string PrevStrippingKey = "GoF2.LinuxServerBuild.PrevStripping";
        const string PrevCodeGenKey = "GoF2.LinuxServerBuild.PrevCodeGen";
        const double CompileTimeoutSeconds = 600;

#if UNITY_STANDALONE_LINUX_API
        static readonly bool CompiledForLinux = true;
#else
        static readonly bool CompiledForLinux = false;
#endif

        static bool running;
        static double waitStarted;

        [MenuItem("GoF2/Build/Linux Dedicated Server", priority = 230)]
        static void Build() => Start(false);

        [MenuItem("GoF2/Build/Linux Dedicated Server (Release Zip)", priority = 231)]
        static void BuildReleaseZip() => Start(true);

        /// <summary>A menu item's job is switching, compiling or building.</summary>
        public static bool Busy => running || SessionState.GetString(JobKey, "") != "";

        /// <summary>The last menu job's result: the zip's (or the build's) full path, empty when it failed or none ran.</summary>
        public static string LastResult => SessionState.GetString(ResultKey, "");

        /// <summary>The command line (docker/build-server.sh, which starts the Editor with -buildTarget Linux64
        /// -standaloneBuildSubtarget Server): -executeMethod GoF2Remake.EditorTools.LinuxServerBuild.Batch; exits 1 when the
        /// build fails.</summary>
        public static void Batch()
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            EditorApplication.Exit(BuildNow(false) != null ? 0 : 1);
        }

        /// <summary>The menu items: notes the settings to put back, switches to the Linux server target and builds once the
        /// scripts are compiled for it (at once when they already are).</summary>
        public static void Start(bool zip)
        {
            if (Busy) { Debug.LogWarning("GoF2: a Linux dedicated server build is already running."); return; }
            SessionState.SetInt(PrevTargetKey, (int)EditorUserBuildSettings.activeBuildTarget);
            SessionState.SetInt(PrevSubtargetKey, (int)EditorUserBuildSettings.standaloneBuildSubtarget);
            SessionState.SetInt(PrevBackendKey, (int)PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server));
            SessionState.SetBool(PrevOptimizationsKey, ServerOptimizations);
            SessionState.SetInt(PrevStrippingKey, (int)PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Server));
            SessionState.SetInt(PrevCodeGenKey, (int)PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.Server));
            SessionState.SetString(ResultKey, "");
            SessionState.SetString(JobKey, zip ? "zip" : "build");

            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64 &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                SessionState.EraseString(JobKey);
                Restore();
                EditorUtility.DisplayDialog("Linux Dedicated Server",
                    "Couldn't switch to the Linux server target. Install the Unity Hub modules \"Linux Build Support (IL2CPP)\" and " +
                    "\"Linux Dedicated Server Build Support\" for this Editor version, then try again.", "OK");
                return;
            }
            Debug.Log("GoF2: Linux dedicated server: the scripts compile for Linux, then the build starts.");
            Wait();
        }

        [InitializeOnLoadMethod]
        static void ResumeAfterReload()
        {
            if (SessionState.GetString(JobKey, "") != "") Wait();
        }

        static void Wait()
        {
            waitStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update -= WaitThenBuild;
            EditorApplication.update += WaitThenBuild;
        }

        /// <summary>Builds once the Editor has finished compiling and its scripts are the Linux target's (the domain before the
        /// switch's reload still runs this, compiled for the old target: it keeps waiting).</summary>
        static void WaitThenBuild()
        {
            if (running) return;
            string job = SessionState.GetString(JobKey, "");
            if (job == "") { EditorApplication.update -= WaitThenBuild; return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!CompiledForLinux)
            {
                if (EditorApplication.timeSinceStartup - waitStarted < CompileTimeoutSeconds) return;
                EditorApplication.update -= WaitThenBuild;
                SessionState.EraseString(JobKey);
                Debug.LogError("GoF2: the scripts never compiled for the Linux target (UNITY_STANDALONE_LINUX_API), so IL2CPP has no " +
                    "toolchain; the Linux dedicated server wasn't built.");
                Restore();
                return;
            }
            EditorApplication.update -= WaitThenBuild;
            running = true;
            string result = null;
            try { result = BuildNow(job == "zip"); }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                running = false;
                SessionState.SetString(ResultKey, result ?? "");
                SessionState.EraseString(JobKey);
                Restore();
            }
        }

        /// <summary>The build on the active Linux target, and with 'zip' the release zip: its path, or null when it failed.</summary>
        static string BuildNow(bool zip)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.IL2CPP);   // like the Linux player
            ServerOptimizations = true;
            // The game builds' stripping (Minimal: the engine and .NET libraries' unused code, never the game's), and IL2CPP's
            // shared generic code: a smaller GameAssembly.so for slightly slower generic code, which an idle server never feels.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Server, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Server, Il2CppCodeGeneration.OptimizeSize);
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { FirstScene },
                locationPathName = Output,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None,
            };
            BuildSummary s;
            ServerAudioStrip.Strip();
            ServerAudioStrip.Active = true;
            try { s = BuildPipeline.BuildPlayer(options).summary; }
            finally
            {
                ServerAudioStrip.Active = false;
                ServerAudioStrip.Restore();
            }
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError($"GoF2: the Linux dedicated server build {s.result} ({s.totalErrors} errors; see the Console).");
                return null;
            }
            Debug.Log($"GoF2: Linux dedicated server built: {Path.GetFullPath(Output)} ({s.totalSize / (1024 * 1024)} MB, {s.totalTime:mm\\:ss}).");
            if (!zip) return Path.GetFullPath(Output);

            string version = Environment.GetEnvironmentVariable(BuildVersionStamp.OverrideVariable);
            string tag = !string.IsNullOrWhiteSpace(version) && version.Trim().Length >= 10
                ? version.Trim().Substring(0, 10)
                : DateTime.UtcNow.ToString("yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture);
            string zipPath = Path.GetFullPath($"Build/Release/GoF2Remake-LinuxServer-{tag}.zip");
            Zip(Path.GetDirectoryName(Path.GetFullPath(Output)), zipPath, "GoF2Server");
            Debug.Log($"GoF2: Linux dedicated server zipped: {zipPath} ({new FileInfo(zipPath).Length / (1024 * 1024)} MB).");
            return zipPath;
        }

        /// <summary>The settings from before the job, and the build target it found (another script compile).</summary>
        static void Restore()
        {
            ServerOptimizations = SessionState.GetBool(PrevOptimizationsKey, ServerOptimizations);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Server,
                (ManagedStrippingLevel)SessionState.GetInt(PrevStrippingKey, (int)PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Server)));
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Server,
                (Il2CppCodeGeneration)SessionState.GetInt(PrevCodeGenKey, (int)PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.Server)));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server,
                (ScriptingImplementation)SessionState.GetInt(PrevBackendKey, (int)PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server)));
            EditorUserBuildSettings.standaloneBuildSubtarget =
                (StandaloneBuildSubtarget)SessionState.GetInt(PrevSubtargetKey, (int)EditorUserBuildSettings.standaloneBuildSubtarget);
            var previous = (BuildTarget)SessionState.GetInt(PrevTargetKey, (int)EditorUserBuildSettings.activeBuildTarget);
            if (EditorUserBuildSettings.activeBuildTarget != previous)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previous), previous);
        }

        /// <summary>'folder' into 'zipPath' under 'top', without IL2CPP's and Burst's debug folders; Unix modes in the entries
        /// (rwxr-xr-x for the executables, .so libraries and scripts, rw-r--r-- else), which unzip keeps on Linux.</summary>
        static void Zip(string folder, string zipPath, string top)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            if (File.Exists(zipPath)) File.Delete(zipPath);
            var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (rel.Contains("_BackUpThisFolder_ButDontShipItWithYourGame") || rel.Contains("_DoNotShip")) continue;
                var entry = archive.CreateEntryFromFile(file, top + "/" + rel, System.IO.Compression.CompressionLevel.Optimal);
                bool exec = rel.EndsWith(".x86_64") || rel.EndsWith(".so") || rel.EndsWith(".sh");
                entry.ExternalAttributes = (exec ? 0x81ED : 0x81A4) << 16;   // S_IFREG | 0755 / 0644
            }
            archive.Dispose();
            MarkMadeOnUnix(zipPath);
        }

        /// <summary>Info-ZIP's unzip applies an entry's Unix mode only when its "version made by" names Unix (3); .NET writes
        /// the platform it runs on (0 on Windows) and has no setting for it, so each central directory entry's host byte is
        /// set afterwards. A zip64 archive (over 4 GB) is left as it is.</summary>
        static void MarkMadeOnUnix(string zipPath)
        {
            using var file = new FileStream(zipPath, FileMode.Open, FileAccess.ReadWrite);
            int tailLength = (int)Math.Min(file.Length, 22 + 65535);
            var tail = new byte[tailLength];
            file.Seek(-tailLength, SeekOrigin.End);
            ReadFully(file, tail);
            int eocd = -1;
            for (int i = tailLength - 22; i >= 0; i--)
                if (BitConverter.ToUInt32(tail, i) == 0x06054b50) { eocd = i; break; }
            if (eocd < 0) { Debug.LogWarning("GoF2: server zip without an end record; its Unix modes need chmod after unzip."); return; }
            int count = BitConverter.ToUInt16(tail, eocd + 10);
            uint size = BitConverter.ToUInt32(tail, eocd + 12), offset = BitConverter.ToUInt32(tail, eocd + 16);
            if (offset == 0xFFFFFFFF || size == 0xFFFFFFFF || count == 0xFFFF) { Debug.LogWarning("GoF2: zip64 server zip; its Unix modes need chmod after unzip."); return; }
            var directory = new byte[size];
            file.Seek(offset, SeekOrigin.Begin);
            ReadFully(file, directory);
            int p = 0;
            for (int n = 0; n < count && p + 46 <= directory.Length && BitConverter.ToUInt32(directory, p) == 0x02014b50; n++)
            {
                directory[p + 5] = 3;   // version made by: high byte = host system, 3 = Unix
                p += 46 + BitConverter.ToUInt16(directory, p + 28) + BitConverter.ToUInt16(directory, p + 30) + BitConverter.ToUInt16(directory, p + 32);
            }
            file.Seek(offset, SeekOrigin.Begin);
            file.Write(directory);
        }

        static void ReadFully(Stream s, byte[] buffer)
        {
            for (int read = 0, n; read < buffer.Length; read += n)
                if ((n = s.Read(buffer, read, buffer.Length - read)) == 0) throw new EndOfStreamException();
        }

        /// <summary>Player Settings > Dedicated Server > "Enable Dedicated Server optimizations"
        /// (PlayerSettings.dedicatedServerOptimizations), read and written by reflection so an Editor without it still
        /// compiles (it then just builds without them).</summary>
        static bool ServerOptimizations
        {
            get
            {
                var p = typeof(PlayerSettings).GetProperty("dedicatedServerOptimizations");
                return p != null && p.GetValue(null) is bool b && b;
            }
            set
            {
                var p = typeof(PlayerSettings).GetProperty("dedicatedServerOptimizations");
                if (p != null && p.CanWrite) p.SetValue(null, value);
                else if (value) Debug.LogWarning("GoF2: this Editor has no dedicated server optimizations setting; the server build keeps the textures and audio.");
            }
        }
    }
}
