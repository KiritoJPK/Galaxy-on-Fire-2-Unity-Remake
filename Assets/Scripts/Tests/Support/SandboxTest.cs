// SandboxTest.cs
// The base of the tests that touch the game's state: every test runs with GamePaths pointed at an empty temporary folder
// (so saves, the mod registry, presets and caches never reach the player's own) and a fresh new game (Session), and the
// folder is deleted afterwards. Tests that change a Settings value (PlayerPrefs, shared with the Editor's Play mode) must
// put it back themselves (SettingsScope).

using System;
using System.IO;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public abstract class SandboxTest
    {
        /// <summary>This test's own data folder (GamePaths.Data while it runs).</summary>
        protected string Sandbox { get; private set; }

        /// <summary>The game's tables (read only: Database.Shared; take Database.Load() to change them).</summary>
        protected static Database Db => Database.Shared;

        [SetUp]
        public void EnterSandbox()
        {
            Sandbox = Path.Combine(Path.GetTempPath(), "GoF2Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Sandbox);
            GamePaths.Override = Sandbox;
            Session.ResetNewGame();
        }

        [TearDown]
        public void LeaveSandbox()
        {
            Session.ResetNewGame();
            GamePaths.Override = null;
            try { if (Directory.Exists(Sandbox)) Directory.Delete(Sandbox, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Puts a Settings value back when disposed: using (new SettingsScope(() => Settings.X, v => Settings.X = v)) { ... }</summary>
    public sealed class SettingsScope<T> : IDisposable
    {
        readonly Action<T> set;
        readonly T saved;
        public SettingsScope(Func<T> get, Action<T> set) { this.set = set; saved = get(); }
        public void Dispose() => set(saved);
    }
}
