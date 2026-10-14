// SaveTransferTests.cs
// Feature: CLAUDE.md "UI and platforms" > Save export / import (SaveTransfer.cs): a .gof2saves file is JSON with the format
// tag "gof2remake-saves", format version 1, the game version and every slot's file unchanged; importing reads and checks the
// whole file first (format, at most 12 unique slots 0-11, each save through SaveGame.TryParse) and only then replaces every
// slot: the new slots go in, the old ones are moved to Saves/BeforeImport (the previous backup replaced). The export itself
// asks for its file with the system dialog in the Editor, so the files here are written in that format by the test.

using System;
using System.Collections.Generic;
using System.IO;
using GoF2Remake.Data;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class SaveTransferTests : SavesModsTest
    {
        [Serializable]
        class Bundle
        {
            public string format = "gof2remake-saves";
            public int formatVersion = 1;
            public string gameVersion = "test";
            public string exportedUtc = "2026-10-14T00:00:00Z";
            public List<Slot> slots = new List<Slot>();
        }

        [Serializable]
        class Slot
        {
            public int slot;
            public string json;
        }

        string ExportPath => Path.Combine(Sandbox, "export." + SaveTransfer.Extension);
        string BackupFolder => Path.Combine(Sandbox, "Saves", "BeforeImport");

        /// <summary>A valid save's JSON with these credits (written by the game into a scratch slot, which is then removed).</summary>
        string SaveJson(int credits)
        {
            Session.Credits = credits;
            Assert.IsTrue(SaveGame.Save(11));
            string json = File.ReadAllText(SaveGame.SlotPath(11));
            File.Delete(SaveGame.SlotPath(11));
            return json;
        }

        string Write(Bundle b)
        {
            File.WriteAllText(ExportPath, JsonUtility.ToJson(b, true));
            return ExportPath;
        }

        Bundle With(params (int slot, string json)[] slots)
        {
            var b = new Bundle();
            foreach (var (s, j) in slots) b.slots.Add(new Slot { slot = s, json = j });
            return b;
        }

        [Test]
        public void TheExtensionIsGof2saves()
        {
            Assert.AreEqual("gof2saves", SaveTransfer.Extension);
        }

        [Test]
        public void ExportingWithoutSavesHasNothingToExport()
        {
            Assert.AreEqual(SaveTransfer.Outcome.NothingToExport, SaveTransfer.Export().outcome);
        }

        [Test]
        public void AValidFileIsCheckedWithoutChangingAnything()
        {
            Session.Credits = 1;
            SaveGame.Save(1);
            var r = SaveTransfer.Check(Write(With((0, SaveJson(10)), (4, SaveJson(40)))));
            Assert.AreEqual(SaveTransfer.Outcome.Done, r.outcome, r.problem);
            Assert.AreEqual(2, r.slots);
            Assert.IsTrue(SaveGame.Exists(1));
            Assert.IsFalse(SaveGame.Exists(0));
            Assert.IsFalse(SaveGame.Exists(4));
            Assert.IsFalse(Directory.Exists(BackupFolder));
        }

        [Test]
        public void ImportReplacesEverySlotAndBacksUpTheOldOnes()
        {
            Session.Credits = 111;
            SaveGame.Save(1);
            Session.Credits = 222;
            SaveGame.Save(2);
            string s0 = SaveJson(1000), s4 = SaveJson(4000);
            var r = SaveTransfer.Import(Write(With((0, s0), (4, s4))));
            Assert.AreEqual(SaveTransfer.Outcome.Done, r.outcome, r.problem);
            Assert.AreEqual(2, r.slots);
            Assert.IsTrue(SaveGame.Exists(0));
            Assert.IsTrue(SaveGame.Exists(4));
            Assert.IsFalse(SaveGame.Exists(1), "every slot is replaced, also those the file has none for");
            Assert.IsFalse(SaveGame.Exists(2));
            Assert.AreEqual(s4, File.ReadAllText(SaveGame.SlotPath(4)), "the slot's file as it was exported");
            Assert.AreEqual(1000, SaveGame.Preview(0).credits);
            Assert.AreEqual(111, JsonUtility.FromJson<SaveData>(File.ReadAllText(Path.Combine(BackupFolder, "slot_01.json"))).credits);
            Assert.AreEqual(222, JsonUtility.FromJson<SaveData>(File.ReadAllText(Path.Combine(BackupFolder, "slot_02.json"))).credits);
            Assert.AreEqual(2, Directory.GetFiles(BackupFolder).Length);
            foreach (var f in Directory.GetFiles(Path.Combine(Sandbox, "Saves"))) Assert.IsFalse(f.EndsWith(".import"), f);
        }

        [Test]
        public void AnImportedSaveLoads()
        {
            string s3 = SaveJson(3333);
            Session.Credits = 0;
            Assert.AreEqual(SaveTransfer.Outcome.Done, SaveTransfer.Import(Write(With((3, s3)))).outcome);
            Assert.IsTrue(SaveGame.Load(3));
            Assert.AreEqual(3333, Session.Credits);
        }

        [Test]
        public void ASecondImportReplacesThePreviousBackup()
        {
            Session.Credits = 1;
            SaveGame.Save(1);
            SaveTransfer.Import(Write(With((5, SaveJson(5)))));
            Assert.IsTrue(File.Exists(Path.Combine(BackupFolder, "slot_01.json")));
            SaveTransfer.Import(Write(With((6, SaveJson(6)))));
            Assert.IsFalse(File.Exists(Path.Combine(BackupFolder, "slot_01.json")));
            Assert.IsTrue(File.Exists(Path.Combine(BackupFolder, "slot_05.json")));
            Assert.IsTrue(SaveGame.Exists(6));
            Assert.IsFalse(SaveGame.Exists(5));
        }

        [Test]
        public void ARefusedImportChangesNothing()
        {
            Session.Credits = 77;
            SaveGame.Save(1);
            var b = With((0, SaveJson(5)), (2, "{ \"version\": 17, \"ship\": 10, \"station\": 999 }"));
            var r = SaveTransfer.Import(Write(b));
            Assert.AreEqual(SaveTransfer.Outcome.Invalid, r.outcome);
            StringAssert.Contains("save 2", r.problem);
            StringAssert.Contains("unknown station", r.problem);
            Assert.IsTrue(SaveGame.Exists(1));
            Assert.AreEqual(77, SaveGame.Preview(1).credits);
            Assert.IsFalse(SaveGame.Exists(0));
            Assert.IsFalse(Directory.Exists(BackupFolder));
        }

        [Test]
        public void TheAutoSaveIsNamedInARefusal()
        {
            var r = SaveTransfer.Check(Write(With((0, "{ \"version\": 99, \"ship\": 10, \"station\": 78 }"))));
            Assert.AreEqual(SaveTransfer.Outcome.Invalid, r.outcome);
            StringAssert.StartsWith("the auto-save: ", r.problem);
            StringAssert.Contains("newer version", r.problem);
        }

        [Test]
        public void NoImportDuringAMultiplayerSession()
        {
            var path = Write(With((0, SaveJson(5))));
            var old = NetSession.ActiveProvider;
            try
            {
                NetSession.ActiveProvider = () => true;
                Assert.AreEqual(SaveTransfer.Outcome.Failed, SaveTransfer.Import(path).outcome);
                Assert.IsFalse(SaveGame.Exists(0));
            }
            finally { NetSession.ActiveProvider = old; }
        }

        [Test]
        public void AMissingFileFails()
        {
            var r = SaveTransfer.Check(Path.Combine(Sandbox, "nothing.gof2saves"));
            Assert.AreEqual(SaveTransfer.Outcome.Failed, r.outcome);
            StringAssert.Contains("gone", r.problem);
        }

        [Test]
        public void NoPathIsCancelledWithAFileDialog()
        {
            var r = SaveTransfer.Check(null);
            Assert.AreEqual(SaveTransfer.HasFileDialog ? SaveTransfer.Outcome.Cancelled : SaveTransfer.Outcome.NoFile, r.outcome);
        }

        static IEnumerable<TestCaseData> BadBundles()
        {
            TestCaseData C(string name, Func<SaveTransferTests, string> text, string expected) => new TestCaseData(text, expected).SetName("RefusesExport_" + name);
            yield return C("NotJson", t => "this is not json", "not a");
            yield return C("WrongFormatTag", t => { var b = t.With((0, t.SaveJson(1))); b.format = "gof2-saves"; return JsonUtility.ToJson(b); }, "not a Galaxy on Fire 2 Remake save export");
            yield return C("NewerFormat", t => { var b = t.With((0, t.SaveJson(1))); b.formatVersion = 2; return JsonUtility.ToJson(b); }, "newer version");
            yield return C("FormatVersion0", t => { var b = t.With((0, t.SaveJson(1))); b.formatVersion = 0; return JsonUtility.ToJson(b); }, "newer version");
            yield return C("NoSaves", t => JsonUtility.ToJson(t.With()), "no saves");
            yield return C("ThirteenSaves", t =>
            {
                string j = t.SaveJson(1);
                var b = t.With();
                for (int i = 0; i < 13; i++) b.slots.Add(new Slot { slot = i % 12, json = j });
                return JsonUtility.ToJson(b);
            }, "more saves than there are slots");
            yield return C("Slot12", t => JsonUtility.ToJson(t.With((12, t.SaveJson(1)))), "slot that doesn't exist");
            yield return C("NegativeSlot", t => JsonUtility.ToJson(t.With((-1, t.SaveJson(1)))), "slot that doesn't exist");
            yield return C("TwiceTheSameSlot", t => { string j = t.SaveJson(1); return JsonUtility.ToJson(t.With((3, j), (3, j))); }, "slot 3 is in the file twice");
            yield return C("ABrokenSave", t => JsonUtility.ToJson(t.With((7, "{}"))), "save 7: not a save");
        }

        [TestCaseSource(nameof(BadBundles))]
        public void BadExportFilesAreRefused(Func<SaveTransferTests, string> text, string expected)
        {
            File.WriteAllText(ExportPath, text(this));
            var r = SaveTransfer.Check(ExportPath);
            Assert.AreEqual(SaveTransfer.Outcome.Invalid, r.outcome, r.problem);
            StringAssert.Contains(expected, r.problem);
            Assert.AreEqual(SaveTransfer.Outcome.Invalid, SaveTransfer.Import(ExportPath).outcome);
            for (int i = 0; i < SaveGame.SlotCount; i++) Assert.IsFalse(SaveGame.Exists(i), "slot " + i);
        }
    }
}
