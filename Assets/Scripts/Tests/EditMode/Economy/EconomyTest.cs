// EconomyTest.cs
// The base of the economy tests (Shop, Hangar, Blueprints, Kaamo Club, Cheats): the sandbox plus a fixed economy. Every
// test runs on the Android tables (the data files = the Android OBB's items.bin / ships.bin, which the Python reference
// tools read), Normal difficulty, the free-shopping cheat off, a fresh pinned-price docking, and campaign step 30 (past the
// tutorial's free gear at Var Hastra: Story.AdjustPrice). Session.Economy / Difficulty aren't reset by ResetNewGame, so
// they are put back afterwards, as is the cheat's PlayerPrefs value.

using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public abstract class EconomyTest : SandboxTest
    {
        Economy savedEconomy;
        float savedDifficulty;
        bool savedFreeShopping;

        [SetUp]
        public void EnterEconomy()
        {
            savedEconomy = Session.Economy;
            savedDifficulty = Session.Difficulty;
            Session.Economy = Economy.Android;
            Session.Difficulty = Session.DifficultyNormal;
            savedFreeShopping = Cheats.FreeShopping;
            if (savedFreeShopping) Cheats.FreeShopping = false;
            Hangar.NewDocking();
            Session.CampaignMission = 30;
        }

        [TearDown]
        public void LeaveEconomy()
        {
            Hangar.NewDocking();
            if (savedFreeShopping) Cheats.FreeShopping = true;
            Session.Economy = savedEconomy;
            Session.Difficulty = savedDifficulty;
        }

        /// <summary>A station stock with these rows (item, amount pairs), in item order.</summary>
        protected static StationStock Stock(int station, params int[] itemAmountPairs)
        {
            var s = new StationStock { station = station };
            for (int i = 0; i + 1 < itemAmountPairs.Length; i += 2) s.items.Add(new ItemStack(itemAmountPairs[i], itemAmountPairs[i + 1]));
            return s;
        }

        protected static int Mounted(int item)
        {
            int n = 0;
            foreach (var e in Session.Equipment) if (e.item == item) n++;
            return n;
        }

        protected static int AmountMounted(int item)
        {
            int n = 0;
            foreach (var e in Session.Equipment) if (e.item == item) n += e.amount;
            return n;
        }
    }
}
