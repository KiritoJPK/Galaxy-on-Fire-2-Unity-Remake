// ModBuilderKitsTests.cs
// Feature: CLAUDE.md "Mods" > Mod Builder's Ship kits: a project keeps its kits of ship parts in the mods' own
// shipkits.json (ProjectKit, ProjectKitPart: written, read back, in the undo steps) and its ships fly them with "kit",
// "kitType" and the default "build" (ProjectShip); the game reads what the builder writes (ModShipKits, ModContent); a kit
// still without parts is left out with a warning, not an error that breaks the mod; a hardpoint keeps the remake's mod
// fields (builtIn, rotation, nodes, outline, direction, fxScale) through the builder (ProjectShip.MountJson).
// Expected values: the format in Modding/README.md "Customizable ships".

using System.IO;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ModBuilderKitsTests : SavesModsTest
    {
        /// <summary>A project with one kit (two slots, three parts) and a ship that flies it.</summary>
        static ModProject MakeProject(string name)
        {
            var p = ModProject.Create(name, "tests");
            var kit = p.AddKit("Hulls");
            kit.Slots = new System.Collections.Generic.List<string> { "body", "wings" };
            var body = kit.AddPart(kit.PartIdFor("Body A"), "body", "Body A", "models/body_a.glb");
            body.SetStat("armor", 10);
            body.SetStat("equipment", 2);
            body.SetSlotCount("primary", 3);
            body.Mounts.Add(new WeaponMount { slotType = 2, position_engine = new[] { 0, 120, -40 }, builtIn = true });
            body.Mounts.Add(new WeaponMount { slotType = 0, position_engine = new[] { -60, 0, 300 }, rotation = new[] { 0f, 0f, 15f }, nodes = new[] { "pylon_l" } });
            var wingsA = kit.AddPart(kit.PartIdFor("Wings A"), "wings", "Wings A", "models/wings_a.glb");
            wingsA.Types = new System.Collections.Generic.List<string> { "fighter" };
            wingsA.Tiers = new System.Collections.Generic.List<string> { "models/wings_a_t1.glb" };
            kit.AddPart(kit.PartIdFor("Wings B"), "wings", "Wings B", "models/wings_b.glb");
            kit.AddColorSlot("hull", "Hull", "paint", false);
            var ship = p.AddKitShip(p.ShipIdFor("Raptor"), "Raptor", kit, "fighter");
            ship.SetBuildPart("wings", "wings_b");
            ship.BuildTier = 1;
            return p;
        }

        [Test]
        public void TheKitIsWrittenAndReadBack()
        {
            var p = MakeProject("Kit test");
            p.Save();
            Assert.IsTrue(File.Exists(Path.Combine(p.Folder, ProjectKit.File)));

            var back = ModProject.Load(p.Folder);
            Assert.IsNull(back.LoadError);
            var kit = back.Kits.Single();
            Assert.AreEqual("hulls", kit.Id);
            CollectionAssert.AreEqual(new[] { "body", "wings" }, kit.Slots);
            Assert.AreEqual(3, kit.Parts.Count);
            var body = kit.Find("body_a");
            Assert.AreEqual(10, body.Stat("armor"));
            Assert.AreEqual(2, body.Stat("equipment"));
            Assert.AreEqual(3, body.SlotCount("primary"));
            Assert.AreEqual(-1, body.SlotCount("secondary"), "a slot type the part doesn't give");
            Assert.AreEqual(2, body.Mounts.Count);
            Assert.IsTrue(body.Mounts[0].builtIn);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 15f }, body.Mounts[1].rotation);
            CollectionAssert.AreEqual(new[] { "pylon_l" }, body.Mounts[1].nodes);
            CollectionAssert.AreEqual(new[] { "fighter" }, kit.Find("wings_a").Types);
            Assert.AreEqual(1, kit.MaxTier);
            Assert.AreEqual("hull", kit.ColorSlots.Single().id);

            var ship = back.Ships.Single();
            Assert.IsTrue(ship.IsKitShip);
            Assert.AreEqual("hulls", ship.Kit);
            Assert.AreEqual("fighter", ship.KitType);
            Assert.AreEqual("wings_b", ship.BuildPart("wings"));
            Assert.AreEqual(1, ship.BuildTier);
            Assert.AreEqual("wings=wings_b;tier=1", ship.BuildText());
            Assert.IsNull(ship.Model, "a kit ship has no model of its own");
        }

        [Test]
        public void UndoTakesTheKitsBack()
        {
            var p = ModProject.Create("Kit undo", "tests");
            using var history = new ModProjectHistory(p);
            var kit = p.AddKit("Hulls");
            kit.AddPart("body", "body", "Body", "models/body.glb").Mounts.Add(new WeaponMount { slotType = 3, position_engine = new[] { 0, 0, -500 } });
            history.Commit();
            history.Undo();
            Assert.IsEmpty(p.Kits);
            history.Redo();
            Assert.AreEqual(1, p.Kits.Single().Parts.Single().Mounts.Count, "the part's hardpoints come back with it");
        }

        [Test]
        public void TheGameReadsTheBuildersKit()
        {
            var p = MakeProject("Kit game");
            p.Save();
            // The project's files as an installed mod of the same id.
            string Read(string f) => File.ReadAllText(Path.Combine(p.Folder, f));
            string dir = WriteMod("kitgame", p.Id, "1.0", "", (ProjectKit.File, Read(ProjectKit.File)), ("ships.json", Read("ships.json")));
            foreach (var f in new[] { "models/body_a.glb", "models/wings_a.glb", "models/wings_a_t1.glb", "models/wings_b.glb" }) WriteFile(dir, f, Dummy);
            ScanAndEnable(p.Id);
            var mod = Installed(p.Id);
            Assert.IsFalse(mod.Broken, AllMessages(mod));

            var kit = ModShipKits.All().Single(k => k.mod == mod);
            CollectionAssert.AreEqual(new[] { "body", "wings" }, kit.slots);
            Assert.AreEqual(3, kit.parts.Count);
            Assert.AreEqual(10f, kit.Find("body_a").armor);
            Assert.AreEqual(3, kit.Find("body_a").primarySlots);
            Assert.IsTrue(kit.Find("body_a").mounts[0].builtIn);
            Assert.AreEqual(1, kit.colorSlots.Count);

            int index = ModContent.ShipIndexOf(p.Key("raptor"));
            Assert.GreaterOrEqual(index, OriginalShips);
            var data = ModShipKits.KitShip(index);
            Assert.IsNotNull(data, "a ship of the project's kit");
            Assert.AreEqual(p.Id + ":hulls", data.kit);
            var build = ModShipKits.DefaultBuild(data, kit);
            Assert.AreEqual("body_a", build.parts["body"], "a slot left out takes its first part");
            Assert.AreEqual("wings_b", build.parts["wings"]);
            Assert.AreEqual(1, build.tier);
        }

        [Test]
        public void AKitWithoutPartsIsLeftOutNotAnError()
        {
            string dir = WriteMod("emptykit", "t_emptykit", "1.0", "",
                (ProjectKit.File, "[ { 'id': 'later', 'name': 'Later', 'parts': [] }, { 'id': 'now', 'parts': [ { 'id': 'b', 'slot': 'body', 'model': 'b.glb' } ] } ]"));
            WriteFile(dir, "b.glb", Dummy);
            ScanAndEnable("t_emptykit");
            var mod = Installed("t_emptykit");
            Assert.IsFalse(mod.Broken, AllMessages(mod));
            var kits = ModShipKits.All().Where(k => k.mod == mod).ToList();
            Assert.AreEqual(1, kits.Count);
            Assert.AreEqual("now", kits[0].localId);
            Assert.IsTrue(mod.Warnings.Any(w => w.Contains("no parts yet")), AllMessages(mod));
        }

        [Test]
        public void AHardpointKeepsTheModFields()
        {
            var m = new WeaponMount
            {
                slotType = 3, position_engine = new[] { 10, 20, -300 }, outline = new[] { 1f, 2f, 3f, 4f, 5f, 6f }, fxScale = 1.62f,
                rotation = new[] { 0f, 8.5f, 0f },
            };
            var jet = new WeaponMount { slotType = 4, position_engine = new[] { 50, 0, 0 }, direction = new[] { 1f, 0f, 0f } };
            var o = ProjectShip.MountJson(m);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, o["outline"].ToObject<float[]>());
            Assert.AreEqual(1.62f, (float)o["fxScale"], 1e-4f);
            CollectionAssert.AreEqual(new[] { 0f, 8.5f, 0f }, o["rotation"].ToObject<float[]>());
            Assert.IsNull(o["builtIn"], "only written when set");
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f }, ProjectShip.MountJson(jet)["direction"].ToObject<float[]>());
            // And through a ship of the builder.
            var p = ModProject.Create("Kit mounts", "tests");
            var s = p.AddNewShip("s", "S", "models/s.glb");
            s.Mounts.Add(m);
            var copy = ProjectShip.Copy(s.Mounts[0]);
            Assert.AreEqual(m.fxScale, copy.fxScale);
            CollectionAssert.AreEqual(m.outline, copy.outline);
            var written = (JArray)s.ToJson()["mounts"];
            Assert.AreEqual(JsonConvert.SerializeObject(o), JsonConvert.SerializeObject(written[0]));
        }
    }
}
