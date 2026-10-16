// OriginalSaveTransferTests.cs
// Feature: CLAUDE.md "UI and platforms" > Original game saves (OriginalSaveConvert, OriginalSaveTransfer): a remake save
// exported as the original's files (gof2_save_game_N + gof2_save_game_preview_N, signed with the community builds' key, or
// unsigned like the PC Full HD version's) reads back as an original save with the same credits, step, ship and station, and
// importing such a file gives a save that passes SaveGame.TryParse, written only into an empty slot. The real phone / PC
// saves this was checked against (967 of them) aren't in the repository (no licence): these use files the game writes itself.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoF2Remake.Data;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class OriginalSaveTransferTests : SavesModsTest
    {
        string ExportParent => Path.Combine(Sandbox, "Export");

        /// <summary>Saves the session into 'slot' and exports every slot; the export's folder.</summary>
        string ExportSlot(int slot, string key = OriginalSave.ModdedKey)
        {
            Assert.IsTrue(SaveGame.Save(slot));
            var r = OriginalSaveTransfer.Export(key, ExportParent);
            Assert.IsNull(r.problem, r.problem);
            Assert.AreEqual(1, r.slots);
            return r.folder;
        }

        [Test]
        public void AnExportedSlotIsASignedOriginalSaveWithTheSameState()
        {
            Session.Credits = 54321;
            string folder = ExportSlot(3);
            string file = Path.Combine(folder, "gof2_save_game_3");
            Assert.IsTrue(File.Exists(file));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "gof2_save_game_preview_3")));
            var bytes = File.ReadAllBytes(file);
            Assert.IsTrue(OriginalSave.SignatureValid(bytes));
            var rec = OriginalSave.Read(bytes, OriginalSave.IngredientCounts(Database.Load()));
            Assert.AreEqual(0, rec.trailing.Length, "nothing left over after the last field");
            Assert.AreEqual(54321, rec.credits);
            Assert.AreEqual(Session.ShipIndex, rec.ship);
            Assert.AreEqual(Session.CampaignMission, rec.campaignMission);
            var preview = OriginalSave.ReadPreview(File.ReadAllBytes(Path.Combine(folder, "gof2_save_game_preview_3")));
            Assert.AreEqual(54321, preview.credits);
        }

        [Test]
        public void AnUnsignedExportIsThePcVersionsFile()
        {
            string folder = ExportSlot(2, key: null);
            var bytes = File.ReadAllBytes(Path.Combine(folder, "gof2_save_game_2"));
            var read = OriginalSaveConvert.Read(bytes, OriginalSave.IngredientCounts(Database.Load()), new[] { 0 });
            Assert.IsNotNull(read.record, read.problem);
            Assert.IsFalse(read.signedFile);
        }

        [Test]
        public void AnExportedSaveImportsBackIntoAFreeSlot()
        {
            Session.Credits = 777;
            int station = Session.StationIndex;
            string folder = ExportSlot(1);
            var imported = OriginalSaveTransfer.Inspect(Path.Combine(folder, "gof2_save_game_1"));
            Assert.IsNull(imported.problem, imported.problem);
            Assert.AreEqual(777, imported.save.credits);
            Assert.AreEqual(station, imported.save.station);
            Assert.AreEqual(Session.ShipIndex, imported.save.ship);
            StringAssert.Contains("777", imported.summary);

            Assert.AreEqual(2, OriginalSaveTransfer.FreeSlot(), "slot 1 is taken, the auto-save slot is never offered");
            Assert.IsNotNull(OriginalSaveTransfer.Import(imported, 1), "a used slot is refused");
            Assert.IsNull(OriginalSaveTransfer.Import(imported, 2));
            var back = SaveGame.Preview(2);
            Assert.IsNotNull(back);
            Assert.AreEqual(777, back.credits);
            Assert.IsFalse(File.Exists(SaveGame.SlotPath(2) + ".tmp"));
        }

        [Test]
        public void AFileThatIsNoSaveIsRefused()
        {
            Directory.CreateDirectory(ExportParent);
            string path = Path.Combine(ExportParent, "gof2_save_game_0");
            File.WriteAllBytes(path, Enumerable.Range(0, 3000).Select(i => (byte)(i * 7)).ToArray());
            var r = OriginalSaveTransfer.Inspect(path);
            Assert.IsNull(r.save);
            Assert.IsNotNull(r.problem);
            Assert.IsNotNull(OriginalSaveTransfer.Import(r, 3), "nothing to import");
            Assert.IsFalse(SaveGame.Exists(3));
        }

        [Test]
        public void ExportingWithoutSavesExportsNothing()
        {
            var r = OriginalSaveTransfer.Export(OriginalSave.ModdedKey, ExportParent);
            Assert.AreEqual(0, r.slots);
            Assert.IsNotNull(r.problem);
        }

        [Test]
        public void EquipmentPastTheShipsSlotsGoesToTheHold()
        {
            var db = Database.Load();
            var ship = db.Ship(Session.ShipIndex);
            var gun = db.Items.First(i => i.TypeId == 0 && !i.modded);
            var save = new SaveData
            {
                ship = Session.ShipIndex, station = 78,
                equipment = Enumerable.Range(0, ship.slots.primary + 2).Select(_ => new ItemStack(gun.index, 1)).ToList(),
                cargo = new List<ItemStack>(),
            };
            var notes = new List<string>();
            var rec = OriginalSaveConvert.ToRecord(save, db, notes);
            Assert.AreEqual(ship.slots.primary, rec.equipment.Count(e => e.item == gun.index));
            Assert.AreEqual(2, rec.cargo.Where(c => c.item == gun.index).Sum(c => c.amount));
            Assert.IsTrue(notes.Any(n => n.Contains("hold")));
        }
    }
}
