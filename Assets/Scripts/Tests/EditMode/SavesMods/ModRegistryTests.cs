// ModRegistryTests.cs
// Feature: CLAUDE.md "Mods (remake-only)" > Numbers (ModRegistry.cs): single player gives each "mod:id" key the next free
// number after the original table (items from 233, ships from 64, systems from 34, stations from 135) for good, kept in
// <data>/mod_registry.json (an older Mods/registry.json is moved there); a number is never handed out twice; Adopt takes a
// save's number for a key this game doesn't know when that number is still free.

using System.IO;
using GoF2Remake.Modding;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ModRegistryTests : SavesModsTest
    {
        string RegistryFile => Path.Combine(Sandbox, "mod_registry.json");

        [Test]
        public void NewKeysGetTheNumbersAfterTheOriginalTable()
        {
            Assert.AreEqual(233, ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems));
            Assert.AreEqual(234, ModRegistry.Assign(ModRegistry.Kind.Item, "m:b", OriginalItems));
            Assert.AreEqual(233, ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems), "a key keeps its number");
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Item, "m:b", out int b));
            Assert.AreEqual(234, b);
            Assert.IsFalse(ModRegistry.TryGet(ModRegistry.Kind.Item, "m:c", out _));
            Assert.AreEqual(2, ModRegistry.All(ModRegistry.Kind.Item).Count);
        }

        [Test]
        public void EveryKindIsNumberedOnItsOwn()
        {
            Assert.AreEqual(233, ModRegistry.Assign(ModRegistry.Kind.Item, "m:x", OriginalItems));
            Assert.AreEqual(64, ModRegistry.Assign(ModRegistry.Kind.Ship, "m:x", OriginalShips));
            Assert.AreEqual(34, ModRegistry.Assign(ModRegistry.Kind.System, "m:x", OriginalSystems));
            Assert.AreEqual(135, ModRegistry.Assign(ModRegistry.Kind.Station, "m:x", OriginalStations));
            Assert.AreEqual(136, ModRegistry.Assign(ModRegistry.Kind.Station, "m:y", OriginalStations));
        }

        [Test]
        public void KeysAreCaseSensitive()
        {
            Assert.AreEqual(233, ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems));
            Assert.AreEqual(234, ModRegistry.Assign(ModRegistry.Kind.Item, "m:A", OriginalItems));
        }

        [Test]
        public void NumbersAreKeptInTheDataFolder()
        {
            ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems);
            ModRegistry.Assign(ModRegistry.Kind.Ship, "m:s", OriginalShips);
            Assert.IsTrue(File.Exists(RegistryFile));
            StringAssert.Contains("m:a", File.ReadAllText(RegistryFile));
            ReloadRegistry();
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Item, "m:a", out int a));
            Assert.AreEqual(233, a);
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Ship, "m:s", out int s));
            Assert.AreEqual(64, s);
            Assert.AreEqual(234, ModRegistry.Assign(ModRegistry.Kind.Item, "m:b", OriginalItems), "the next number survives a restart");
        }

        [Test]
        public void ANumberIsNeverHandedOutTwice()
        {
            ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems);       // 233
            Assert.IsTrue(ModRegistry.Adopt(ModRegistry.Kind.Item, "m:far", 300, OriginalItems));
            Assert.AreEqual(301, ModRegistry.Assign(ModRegistry.Kind.Item, "m:next", OriginalItems), "after the highest number given out");
        }

        [Test]
        public void AdoptTakesAFreeNumberOnly()
        {
            ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems);       // 233
            Assert.IsTrue(ModRegistry.Adopt(ModRegistry.Kind.Item, "other:x", 240, OriginalItems));
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Item, "other:x", out int x));
            Assert.AreEqual(240, x);
            Assert.IsFalse(ModRegistry.Adopt(ModRegistry.Kind.Item, "other:y", 233, OriginalItems), "taken");
            Assert.IsFalse(ModRegistry.Adopt(ModRegistry.Kind.Item, "other:z", 100, OriginalItems), "an original item's number");
            Assert.IsFalse(ModRegistry.TryGet(ModRegistry.Kind.Item, "other:y", out _));
            Assert.IsFalse(ModRegistry.TryGet(ModRegistry.Kind.Item, "other:z", out _));
            Assert.IsTrue(ModRegistry.Adopt(ModRegistry.Kind.Item, "m:a", 233, OriginalItems), "a known key at its own number");
            Assert.IsFalse(ModRegistry.Adopt(ModRegistry.Kind.Item, "m:a", 250, OriginalItems), "a known key keeps its number");
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Item, "m:a", out int a));
            Assert.AreEqual(233, a);
        }

        [Test]
        public void AnOldRegistryInTheModsFolderIsMovedOut()
        {
            Directory.CreateDirectory(ModsFolder);
            string old = Path.Combine(ModsFolder, "registry.json");
            File.WriteAllText(old, Q("{ 'items': { 'old:item': 245 }, 'ships': { 'old:ship': 70 } }"));
            ReloadRegistry();
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Item, "old:item", out int i));
            Assert.AreEqual(245, i);
            Assert.IsTrue(ModRegistry.TryGet(ModRegistry.Kind.Ship, "old:ship", out int s));
            Assert.AreEqual(70, s);
            Assert.IsFalse(File.Exists(old));
            Assert.IsTrue(File.Exists(RegistryFile));
        }

        [Test]
        public void AnUnreadableRegistryNumbersAnew()
        {
            File.WriteAllText(RegistryFile, "{ this isn't json");
            ReloadRegistry();
            Assert.AreEqual(0, ModRegistry.All(ModRegistry.Kind.Item).Count);
            Assert.AreEqual(233, ModRegistry.Assign(ModRegistry.Kind.Item, "m:a", OriginalItems));
        }
    }
}
