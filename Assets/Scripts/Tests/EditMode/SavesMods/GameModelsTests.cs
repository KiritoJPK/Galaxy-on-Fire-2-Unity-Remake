// GameModelsTests.cs
// Feature: Modding/README.md "The game's own models" (Modding.GameModels): a mod's "model" (and "icon") may name one of the
// game's own models ("gof2:<name>") instead of a file in the mod, for ships (new or changed: the game's assembly handed out,
// that ship's hardpoints, hangar height and icon with it), stations (as made unless "modelSize"), turrets (any game model,
// or a game turret's by "item") and weapon effect parts; and the Mod Builder's entries that write them.

using System.IO;
using System.Linq;
using GoF2Remake.Data;
using GoF2Remake.Modding;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class GameModelsTests : SavesModsTest
    {
        [TearDown]
        public void CloseProject() => ModManager.SetBuilderProject(null);

        [Test]
        public void TheNameForms()
        {
            Assert.IsTrue(GameModels.Is("gof2:ship_005_terran", out var name));
            Assert.AreEqual("ship_005_terran", name);
            Assert.IsTrue(GameModels.Is("GOF2: turret_002_ship_mounted", out name), "any case, spaces trimmed");
            Assert.AreEqual("turret_002_ship_mounted", name);
            Assert.IsFalse(GameModels.Is("models/ship.glb"));
            Assert.AreEqual("gof2:x", GameModels.Ref("x"));
            Assert.AreEqual(5, GameModels.ShipOf("ship_005_terran"));
            Assert.AreEqual(39, GameModels.ShipOf("v_ship_039_vossk"));
            Assert.AreEqual(-1, GameModels.ShipOf("battleship_terran"), "a capital ship is no ship of the table");
            Assert.IsNotNull(GameModels.Find(Database.Load(), "ship_005_terran"));
            Assert.IsNull(GameModels.Find(Database.Load(), "no_such_model"));
            Assert.IsNotNull(GameModels.Icon("gof2:item_046"), "a game shop icon");
            Assert.IsNull(GameModels.Icon("icons/x.png"));
        }

        [Test]
        public void ANewShipOnAGameModel()
        {
            WriteMod("s", "t_gm", "1.0", "", ("ships.json", "[ { 'id': 'twin', 'name': 'Twin', 'model': 'gof2:ship_005_terran', 'armor': 300, " +
                                                            "'slots': { 'primary': 2, 'secondary': 1, 'turret': 0, 'equipment': 6 } } ]"));
            ScanAndEnable("t_gm");
            Assert.IsEmpty(Installed("t_gm").Errors, AllMessages(Installed("t_gm")));
            var db = Database.Load();
            int ship = ModContent.ShipIndexOf("t_gm:twin");
            Assert.GreaterOrEqual(ship, 64);
            Assert.AreEqual("ship_005_terran", db.ShipAssembly(ship)?.name, "the game's model, nothing to build");
            Assert.IsFalse(db.Assemblies.Any(a => a.name == $"ship_{ship:000}_mod"), "no mod model to build");
            CollectionAssert.IsNotEmpty(db.MountsOf(ship, 0), "the Inflict's hardpoints came with it");
            Assert.AreEqual(Database.GameMounts(5).Count, db.WeaponMounts.Find(w => w.ship == ship).mounts.Count);
            var c = CustomShips.Get(ship);
            Assert.AreEqual(5, c.gameModelShip);
            Assert.AreEqual(-1, c.hangarHeight, "the Inflict's own height on the pads");
            Assert.IsFalse(ModContent.ModelOverrides().Any(e => e.ship.index == ship));
        }

        [Test]
        public void AnUnknownGameModelIsReported()
        {
            WriteMod("s", "t_gm", "1.0", "", ("ships.json", "[ { 'id': 'twin', 'name': 'Twin', 'model': 'gof2:nothing_here' } ]"));
            ScanAndEnable("t_gm");
            Database.Load();
            StringAssert.Contains("the game has no model \"nothing_here\"", AllMessages(Installed("t_gm")));
        }

        [Test]
        public void AnOriginalShipOnAnotherShipsModel()
        {
            WriteMod("s", "t_gm", "1.0", "", ("ships.json", "[ { 'override': 10, 'model': 'gof2:ship_005_terran' } ]"));
            ScanAndEnable("t_gm");
            var db = Database.Load();
            Assert.AreEqual("ship_005_terran", db.ShipAssembly(10)?.name);
            Assert.AreNotEqual("ship_005_terran", db.ShipAssembly(10, original: true)?.name, "the stand-in stays the Phantom's own");
            Assert.AreEqual(Database.GameMounts(5).Count, db.WeaponMounts.Find(w => w.ship == 10).mounts.Count, "the model's hardpoints");
            Assert.IsEmpty(ModContent.ModelOverrides(), "nothing to build");
        }

        [Test]
        public void TurretsAndPartsTakeGameModels()
        {
            WriteMod("fx", "t_gfx", "1.0", "", ("items.json",
                "[ { 'id': 'a', 'base': 182, 'name': 'A', 'fx': { 'turret': 'gof2:turret_002_ship_mounted', 'projectile': { 'model': 'gof2:rocket_001', 'size': 300 } } }," +
                "  { 'id': 'b', 'base': 182, 'name': 'B', 'fx': { 'turret': { 'item': 48, 'scale': 1.5 } } } ]"));
            ScanAndEnable("t_gfx");
            var mod = Installed("t_gfx");
            Assert.IsEmpty(mod.Errors, AllMessages(mod));
            var defs = ModWeapons.Defs();
            var a = defs.Single(d => d.turretModel != null);
            Assert.AreEqual("gof2:turret_002_ship_mounted", a.turretModel);
            Assert.AreEqual("gof2:rocket_001", a.projectile.model);
            var b = defs.Single(d => d.turretItem >= 0);
            Assert.AreEqual(48, b.turretItem);
            Assert.AreEqual(1.5f, b.turretScale);
            Assert.IsNull(b.turretModel);
        }

        [Test]
        public void ATurretTakesAModelOrAnItemNotBoth()
        {
            WriteMod("fx", "t_gfx", "1.0", "", ("items.json",
                "[ { 'id': 'a', 'base': 182, 'name': 'A', 'fx': { 'turret': { 'model': 'gof2:x', 'item': 48 } } } ]"));
            ScanAndEnable("t_gfx");
            Assert.IsEmpty(ModWeapons.Defs());
            StringAssert.Contains("both \"model\" and \"item\"", AllMessages(Installed("t_gfx")));
        }

        [Test]
        public void AStationOnAGameModelKeepsItsSize()
        {
            WriteMod("w", "t_gst", "1.0", "", ("stations.json",
                "[ { 'override': 3, 'model': 'gof2:station_078_midorian' }, { 'override': 4, 'model': 'gof2:station_078_midorian', 'modelSize': 30000 } ]"));
            ScanAndEnable("t_gst");
            Database.Load();
            Assert.IsEmpty(Installed("t_gst").Errors, AllMessages(Installed("t_gst")));
            Assert.AreEqual(0f, ModWorld.ModelOf(3).size, "as made");
            Assert.AreEqual(30000f, ModWorld.ModelOf(4).size);
        }

        [Test]
        public void TheEndingsPropsAreGameModelsToo()
        {
            // "gof2:bra" / "gof2:beer": single meshes with no assembly (Level::createScene's 0x37d1 / 0x37d0), from
            // StoryAssets.menuStatics; the community's exploding bra (Modding/Examples/exploding_bra) flies the bra.
            Assert.IsNotNull(GameModels.Prop("bra"));
            Assert.IsNotNull(GameModels.Prop("Beer"));
            Assert.IsNull(GameModels.Prop("ship_028_terran"), "assemblies go through Find, not Prop");
            Assert.IsNull(GameModels.Prop(""));

            CopyExample("exploding_bra");
            ScanAndEnable("exploding_bra");
            var m = Installed("exploding_bra");
            Assert.IsFalse(m.Broken, AllMessages(m));
            Assert.IsEmpty(m.Warnings, AllMessages(m));
            var bra = Database.Load().Item(ModContent.ItemIndexOf("exploding_bra:exploding_bra"));
            Assert.AreEqual(232, bra.Look, "the Fireworks: its trail, burst and second-press detonation");
            Assert.AreEqual(160, bra.Attr(9));
            Assert.AreEqual(20000, bra.Attr(14));
            Assert.AreEqual(78, bra.Attr(61));
        }

        [Test]
        public void AGameIconForAnItem()
        {
            WriteMod("i", "t_gic", "1.0", "", ("items.json", "[ { 'id': 'a', 'base': 0, 'name': 'A', 'icon': 'gof2:item_046' } ]"));
            ScanAndEnable("t_gic");
            Assert.IsEmpty(Installed("t_gic").Warnings, AllMessages(Installed("t_gic")));
            int item = ModContent.ItemIndexOf("t_gic:a");
            Assert.AreSame(GameModels.Icon("gof2:item_046"), ModContent.ItemIcon(item));
        }

        [Test]
        public void TheBuilderPutsAShipOnAGameModel()
        {
            var p = ModProject.Create("Game ship", "tests");
            var s = p.AddNewShip("twin", "Twin", "models/x.glb");
            var db = Database.Load();
            s.UseGameModel("ship_005_terran", db.Ship(5), Database.GameMounts(5), Shop.RaceOfShip(5));
            Assert.IsTrue(s.HasGameModel);
            Assert.AreEqual("gof2:ship_005_terran", s.Model);
            Assert.AreEqual(db.Ship(5).armor, s.Armor);
            Assert.AreEqual(Database.GameMounts(5).Count, s.Mounts.Count);
            Assert.AreNotSame(Database.GameMounts(5)[0], s.Mounts[0], "copies, not the game's own");
            Assert.IsFalse(s.NeedsLayout, "no first guess from the model's box over the ship's own hardpoints");
            p.Save();
            string json = File.ReadAllText(Path.Combine(p.Folder, ModContent.ShipsFile));
            StringAssert.DoesNotContain("modelLength", json);
            StringAssert.DoesNotContain("hangarHeight", json, "the game ship's height on the pads");
        }

        [Test]
        public void TheBuilderBorrowsPartsAndSounds()
        {
            var p = ModProject.Create("Look", "tests");
            var it = p.AddItem(0, "Laser");
            it.SetFxPartItem("projectile", 46);
            it.SetFxPartScale("projectile", 0.5f);
            it.SetSoundItem("shot", 46);
            it.TurretItem = 48;
            Assert.AreEqual(46, it.FxPartItem("projectile"));
            Assert.AreEqual(0.5f, it.FxPartScale("projectile"));
            Assert.AreEqual(46, it.SoundItem("shot"));
            Assert.AreEqual(48, it.TurretItem);
            it.TurretModel = "gof2:turret_001_ship_mounted";
            Assert.IsNull(it.TurretItem, "a model takes the item's place");
            it.SetFxPartItem("projectile", null);
            it.SetFxPartScale("projectile", 1f);
            Assert.IsNull(it.FxPartItem("projectile"));
            Assert.IsNull(it.FxPartScale("projectile"), "an empty part is dropped");
            p.Save();
            ModManager.SetBuilderProject(p.Folder);
            Assert.IsEmpty(ModManager.BuilderProject.Errors, AllMessages(ModManager.BuilderProject));
            var d = ModWeapons.Defs().Single();
            Assert.AreEqual(46, d.shotItem);
            Assert.AreEqual("gof2:turret_001_ship_mounted", d.turretModel);
        }
    }
}
