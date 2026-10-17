// SaveFolderTests.cs
// The save folder option's rules (Data.SaveFolder): the mirror between the game's own Saves folder and a chosen one,
// with a plain folder as the "other device" (the Android tree store isn't reachable in the Editor; it implements the
// same IStore).

using System;
using System.Collections.Generic;
using System.IO;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class SaveFolderTests : SandboxTest
    {
        string folder;

        [SetUp]
        public void MakeFolder()
        {
            folder = Path.Combine(Sandbox, "SyncedFolder");
            Directory.CreateDirectory(folder);
            SaveFolder.TestTarget = null;
            SaveFolder.StoreOverride = null;
        }

        [TearDown]
        public void Forget()
        {
            SaveFolder.TestTarget = null;
            SaveFolder.StoreOverride = null;
        }

        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        static string SaveJson(DateTime savedAt, string marker) =>
            "{\"version\":17,\"savedAtUtc\":\"" + savedAt.ToString("o") + "\",\"commander\":\"" + marker + "\"}";

        static void WriteFile(string path, string text, DateTime fileTime)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            File.SetLastWriteTimeUtc(path, fileTime);
        }

        void Local(int slot, DateTime savedAt, string marker) => WriteFile(SaveGame.SlotPath(slot), SaveJson(savedAt, marker), savedAt);
        void Remote(int slot, DateTime savedAt, string marker) => WriteFile(RemotePath(slot), SaveJson(savedAt, marker), savedAt);
        string RemotePath(int slot) => Path.Combine(folder, $"slot_{slot:00}.json");
        static string Marker(string path) => File.Exists(path) ? File.ReadAllText(path).Split(new[] { "\"commander\":\"" }, StringSplitOptions.None)[1].Split('"')[0] : null;
        string BinFolder => Path.Combine(SaveGame.Folder, "Deleted");
        int BinCount(string pattern) => Directory.Exists(BinFolder) ? Directory.GetFiles(BinFolder, pattern).Length : 0;

        [Test]
        public void WithoutAFolderNothingHappens()
        {
            Local(1, T0, "a");
            var r = SaveFolder.Sync();
            Assert.AreEqual(SaveFolder.Outcome.Off, r.outcome);
            Assert.IsFalse(SaveFolder.Chosen);
            Assert.AreEqual(0, Directory.GetFiles(folder).Length);
        }

        [Test]
        public void TheTestsNeverUseThePlayersChoice()
        {
            // The sandbox reads TestTarget only: a folder the player chose in the Editor never gets the tests' saves.
            Assert.AreEqual("", SaveFolder.Target);
        }

        [Test]
        public void ChoosingAnEmptyFolderCopiesThisDevicesSaves()
        {
            Local(0, T0, "auto");
            Local(4, T0.AddHours(1), "four");
            var r = SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            Assert.AreEqual(SaveFolder.Outcome.Done, r.outcome);
            Assert.AreEqual(2, r.pushed);
            Assert.AreEqual("auto", Marker(RemotePath(0)));
            Assert.AreEqual("four", Marker(RemotePath(4)));
            Assert.AreEqual(folder, SaveFolder.Target);
        }

        [Test]
        public void StartingEmptySetsThisDevicesSavesAside()
        {
            Local(2, T0, "two");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.FolderOnly);
            Assert.IsFalse(SaveGame.Exists(2));
            Assert.AreEqual(0, Directory.GetFiles(folder, "slot_*.json").Length);
            var aside = Directory.GetFiles(Path.Combine(SaveGame.Folder, "BeforeFolder"), "slot_02.json", SearchOption.AllDirectories);
            Assert.AreEqual(1, aside.Length, "set aside, not lost");
        }

        [Test]
        public void AGameSavedHereGoesToTheFolder()
        {
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            Session.PlaySeconds = 120f;
            Assert.IsTrue(SaveGame.Save(3));
            Assert.IsTrue(File.Exists(RemotePath(3)));
            Assert.AreEqual(File.ReadAllText(SaveGame.SlotPath(3)), File.ReadAllText(RemotePath(3)));
        }

        [Test]
        public void AnotherDevicesSaveComesIn()
        {
            Local(5, T0, "old");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            Remote(5, T0.AddDays(1), "phone");
            Remote(6, T0.AddDays(2), "new slot");
            var r = SaveFolder.Sync();
            Assert.AreEqual(2, r.pulled);
            Assert.AreEqual(0, r.conflicts, "only the folder changed slot 5");
            Assert.AreEqual("phone", Marker(SaveGame.SlotPath(5)));
            Assert.AreEqual("new slot", Marker(SaveGame.SlotPath(6)));
            // Resume picks the newest game, not the newest copy.
            Assert.AreEqual(T0.AddDays(2), File.GetLastWriteTimeUtc(SaveGame.SlotPath(6)));
            Assert.AreEqual(6, SaveGame.MostRecentSlot());
        }

        [Test]
        public void ADeletionHereDeletesItInTheFolder()
        {
            Local(7, T0, "seven");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            Assert.IsTrue(SaveGame.Delete(7));
            Assert.IsFalse(File.Exists(RemotePath(7)));
            Assert.AreEqual(1, BinCount("slot_07_*.json"), "SaveGame.Delete keeps it");
        }

        [Test]
        public void ADeletionInTheFolderKeepsThisDevicesCopyInTheBin()
        {
            Local(8, T0, "eight");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            File.Delete(RemotePath(8));
            var r = SaveFolder.Sync();
            Assert.AreEqual(1, r.removed);
            Assert.IsFalse(SaveGame.Exists(8));
            Assert.AreEqual(1, BinCount("slot_08_removed_*.json"));
        }

        [Test]
        public void BothChangedTheNewerSaveWinsAndTheOtherIsKept()
        {
            Local(9, T0, "base");
            Local(10, T0, "base");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            // Slot 9: the folder's is newer; slot 10: this device's is.
            Local(9, T0.AddHours(1), "pc");
            Remote(9, T0.AddHours(2), "phone");
            Local(10, T0.AddHours(3), "pc");
            Remote(10, T0.AddHours(2), "phone");
            var r = SaveFolder.Sync();
            Assert.AreEqual(2, r.conflicts);
            Assert.AreEqual("phone", Marker(SaveGame.SlotPath(9)));
            Assert.AreEqual("phone", Marker(RemotePath(9)));
            Assert.AreEqual("pc", Marker(SaveGame.SlotPath(10)));
            Assert.AreEqual("pc", Marker(RemotePath(10)));
            Assert.AreEqual(1, BinCount("slot_09_conflict_*.json"), "this device's slot 9 kept");
            Assert.AreEqual(1, BinCount("slot_10_conflict_*.json"), "the folder's slot 10 kept");
            Assert.AreEqual("pc", Marker(Directory.GetFiles(BinFolder, "slot_09_conflict_*.json")[0]));
            Assert.AreEqual("phone", Marker(Directory.GetFiles(BinFolder, "slot_10_conflict_*.json")[0]));
        }

        [Test]
        public void AFolderThatAlreadyHasSavesMergesByTheNewerSave()
        {
            Local(1, T0.AddHours(5), "pc newer");
            Local(2, T0, "pc older");
            Remote(1, T0, "folder older");
            Remote(2, T0.AddHours(5), "folder newer");
            Remote(3, T0, "folder only");
            var s = SaveFolder.Inspect(folder);
            Assert.AreEqual(3, s.folderSaves);
            Assert.AreEqual(2, s.localSaves);
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            Assert.AreEqual("pc newer", Marker(SaveGame.SlotPath(1)));
            Assert.AreEqual("pc newer", Marker(RemotePath(1)));
            Assert.AreEqual("folder newer", Marker(SaveGame.SlotPath(2)));
            Assert.AreEqual("folder only", Marker(SaveGame.SlotPath(3)));
            Assert.AreEqual(2, BinCount("slot_0*_conflict_*.json"));
        }

        [Test]
        public void OnlyTheFoldersSavesSetsThisDevicesAside()
        {
            Local(1, T0.AddHours(5), "pc");
            Remote(2, T0, "folder");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.FolderOnly);
            Assert.IsFalse(SaveGame.Exists(1));
            Assert.IsFalse(File.Exists(RemotePath(1)));
            Assert.AreEqual("folder", Marker(SaveGame.SlotPath(2)));
        }

        [Test]
        public void AHalfWrittenFileIsLeftAloneUntilItIsWhole()
        {
            Local(4, T0, "mine");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            WriteFile(RemotePath(4), "{\"version\":17,\"savedAtUtc\":\"" + T0.AddDays(1).ToString("o") + "\",\"comm", T0.AddDays(1));   // a sync tool mid-transfer
            var r = SaveFolder.Sync();
            Assert.AreEqual(0, r.pulled + r.conflicts);
            Assert.AreEqual("mine", Marker(SaveGame.SlotPath(4)));
            Remote(4, T0.AddDays(1), "phone");   // the transfer done
            Assert.AreEqual(1, SaveFolder.Sync().pulled);
            Assert.AreEqual("phone", Marker(SaveGame.SlotPath(4)));
        }

        [Test]
        public void AnUnreachableFolderNeverCostsASave()
        {
            Local(1, T0, "one");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            string moved = folder + "_away";
            Directory.Move(folder, moved);   // the drive unplugged
            Session.PlaySeconds = 60f;
            Assert.IsTrue(SaveGame.Save(2), "saving still works");
            var r = SaveFolder.Sync();
            Assert.AreEqual(SaveFolder.Outcome.Unreachable, r.outcome);
            Assert.IsNotNull(SaveFolder.LastProblem);
            Assert.IsTrue(SaveGame.Exists(1) && SaveGame.Exists(2));
            Assert.IsFalse(Directory.Exists(folder), "a missing folder is never made (a drive's mount point)");
            Directory.Move(moved, folder);   // back
            r = SaveFolder.Sync();
            Assert.AreEqual(SaveFolder.Outcome.Done, r.outcome);
            Assert.IsNull(SaveFolder.LastProblem);
            Assert.IsTrue(File.Exists(RemotePath(2)), "caught up");
            Assert.AreEqual("one", Marker(RemotePath(1)));
        }

        /// <summary>A plain folder that counts the files it reads.</summary>
        sealed class CountingStore : SaveFolder.IStore
        {
            readonly string dir;
            public int reads;
            public CountingStore(string dir) { this.dir = dir; }
            public string Check() => Directory.Exists(dir) ? null : "gone";
            public Dictionary<string, SaveFolder.RemoteFile> List()
            {
                var list = new Dictionary<string, SaveFolder.RemoteFile>();
                foreach (var f in new DirectoryInfo(dir).GetFiles("*.json"))
                    list[f.Name] = new SaveFolder.RemoteFile { size = f.Length, modified = f.LastWriteTimeUtc.Ticks / 10000 };
                return list;
            }
            public string Read(string name) { reads++; string p = Path.Combine(dir, name); return File.Exists(p) ? File.ReadAllText(p) : null; }
            public void Write(string name, string text) => File.WriteAllText(Path.Combine(dir, name), text);
            public void Delete(string name) => File.Delete(Path.Combine(dir, name));
        }

        [Test]
        public void UnchangedFilesAreNotReadAgain()
        {
            var store = new CountingStore(folder);
            SaveFolder.StoreOverride = _ => store;
            Local(1, T0, "one");
            Remote(2, T0, "two");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            store.reads = 0;
            var r = SaveFolder.Sync();
            Assert.IsFalse(r.Changed);
            Assert.AreEqual(0, store.reads);
        }

        [Test]
        public void ShipPresetsAreKeptToo()
        {
            WriteFile(SaveFolder.PresetsPath, "{\"slots\":[]}", T0);
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            Assert.IsTrue(File.Exists(Path.Combine(folder, SaveFolder.PresetsFile)));
            int before = SaveFolder.Revision;
            WriteFile(Path.Combine(folder, SaveFolder.PresetsFile), "{\"slots\":[{}]}", T0.AddHours(1));
            SaveFolder.Sync();
            Assert.AreEqual("{\"slots\":[{}]}", File.ReadAllText(SaveFolder.PresetsPath));
            Assert.Greater(SaveFolder.Revision, before, "ShipPresets reads its file again");
        }

        [Test]
        public void BackToThisDeviceKeepsEverySave()
        {
            Local(1, T0, "one");
            SaveFolder.Choose(folder, "Synced", SaveFolder.Start.Merge);
            SaveFolder.UseDefault();
            Assert.IsFalse(SaveFolder.Chosen);
            Assert.IsTrue(SaveGame.Exists(1));
            Assert.IsTrue(File.Exists(RemotePath(1)), "the folder keeps its copies");
            Assert.AreEqual(SaveFolder.Outcome.Off, SaveFolder.Sync().outcome);
        }
    }
}
