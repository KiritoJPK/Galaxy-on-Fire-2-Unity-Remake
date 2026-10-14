// MultiplayerSessionTests.cs
// A local multiplayer session runs: hosting (NetGame.StartHost on the local network) docks at Dis with the hangar's
// network side (NetHangar), launching builds the shared orbit (NetOrbit, the layer hooks SpaceHooks.SharedOrbit / Peers),
// docking back works, and the session shuts down cleanly (CLAUDE.md "Multiplayer").

using System.Collections;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Multiplayer;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoF2Remake.Tests
{
    [Category("Smoke")]
    public class MultiplayerSessionTests : PlayModeTest
    {
        string savedName;

        [SetUp]
        public void LocalHostSettings()
        {
            savedName = PlayerPrefs.GetString("mp_name", "");
            PlayerPrefs.SetString("mp_name", "Tester");
        }

        [TearDown]
        public void RestoreSettings()
        {
            PlayerPrefs.SetString("mp_name", savedName);
        }

        [UnityTest]
        public IEnumerator HostLaunchAndDock()
        {
            yield return Load("MainMenu");
            Assert.IsTrue(NetGame.CanHost(), "the port is free");
            Assert.IsTrue(NetGame.StartHost(), NetGame.Status);
            yield return WaitFor(() => Find<StationLevel>() != null, 60f, "docked at the session's station");
            Assert.IsTrue(NetSession.Active, "the session runs");
            Assert.AreEqual(NetSession.StartStation, Session.StationIndex);
            yield return Run(3f);
            Assert.IsNotNull(Find<NetHangar>(), "the hangar's network side (SpaceHooks.StationBuilt)");

            Find<StationLevel>().Launch();
            yield return WaitFor(() => Find<SpaceLevel>() != null && Find<SpaceLevel>().Player != null, 60f, "the shared orbit");
            yield return Run(4f);
            Assert.IsNotNull(SpaceHooks.SharedOrbit, "NetOrbit through SpaceHooks");
            Assert.AreEqual(1, SpaceHooks.Peers.Count(), "this game's player");
            Assert.IsNotNull(SpaceHooks.LocalPeer);

            Find<SpaceLevel>().Dock();
            yield return WaitFor(() => Find<StationLevel>() != null, 30f, "docked again");
            yield return Run(2f);

            NetGame.ShutdownNow();
            yield return Run(1f);
            Assert.IsFalse(NetSession.Active, "the session ended");
        }
    }
}
