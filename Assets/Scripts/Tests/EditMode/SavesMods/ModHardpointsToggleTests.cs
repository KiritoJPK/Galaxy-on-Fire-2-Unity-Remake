// ModHardpointsToggleTests.cs
// Feature: CLAUDE.md "Mods" > Customizable ships, weapon models: a kit whose "hardpoints" have "toggle": true lets the player
// hide its weapon models, pylons and turret model (Build.weapons "weapons=off", the Customize screen's Weapon models row;
// ModHardpoints.Toggles / Hidden); ships.json's "build" may start a ship hidden ("weapons": false); a kit without the toggle
// never hides them; the choice isn't part of the model's variant (ModShipKits.VariantAssembly).
// Expected values: the format in Modding/README.md "Customizable ships".

using GoF2Remake.Data;
using GoF2Remake.Modding;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ModHardpointsToggleTests : SavesModsTest
    {
        static string Kits(bool toggle) => @"[ { 'id': 'hulls', 'slots': ['body'],
            'parts': [ { 'id': 'b', 'slot': 'body', 'model': 'b.glb' } ],
            'hardpoints': { 'turret': 'turret.glb', " + (toggle ? "'toggle': true, " : "") + @"'weapons': [ { 'slot': 'primary', 'model': 'gun.glb' } ] } } ]";

        int KitShipMod(string id, bool toggle, string build)
        {
            string dir = WriteMod(id, id, "1.0", "", ("shipkits.json", Kits(toggle)),
                ("ships.json", "[ { 'id': 'raptor', 'name': 'Raptor', 'kit': 'hulls', 'kitType': 'fighter', 'build': " + build + " } ]"));
            foreach (var f in new[] { "b.glb", "turret.glb", "gun.glb" }) WriteFile(dir, f, Dummy);
            ScanAndEnable(id);
            Assert.IsFalse(Installed(id).Broken, AllMessages(Installed(id)));
            Database.Load();
            int ship = ModContent.ShipIndexOf(id + ":raptor");
            Assert.GreaterOrEqual(ship, OriginalShips);
            return ship;
        }

        [Test]
        public void TheBuildTextCarriesTheWeaponModelsChoice()
        {
            var b = ModShipKits.Build.Parse("body=b;weapons=OFF;tier=0");
            Assert.IsTrue(b.WeaponsHidden);
            StringAssert.Contains("weapons=off", b.ToString());
            Assert.IsTrue(ModShipKits.Build.Parse(b.ToString()).WeaponsHidden, "round trip");
            Assert.IsTrue(b.Clone().WeaponsHidden);
            Assert.IsFalse(ModShipKits.Build.Parse("body=b;tier=0").WeaponsHidden, "none = shown");
            Assert.IsFalse(b.parts.ContainsKey("weapons"), "not a slot");
            Assert.IsTrue(ModShipKits.Build.Reserved("weapons"));
        }

        [Test]
        public void AKitWithTheToggleLetsThePlayerHideTheWeaponModels()
        {
            int ship = KitShipMod("t_tog", true, "{ 'body': 'b' }");
            Assert.IsTrue(ModHardpoints.Toggles(ship));
            Assert.IsFalse(ModHardpoints.Hidden(ship), "shown by default");
            try
            {
                Session.ShipBuilds[ModShipKits.BuildKey(ship)] = "body=b;weapons=off;tier=0";
                Assert.IsTrue(ModShipKits.PlayerBuild(ship).WeaponsHidden);
                Assert.IsTrue(ModHardpoints.Hidden(ship), "the player's build hides them");
                Assert.AreEqual(UnityEngine.Vector3.zero, ModHardpoints.MuzzleOffset(ship, Database.Shared.Item(0), false, new WeaponMount()),
                                "the shots leave from the mount");
                Assert.IsTrue(ModHardpoints.Hidden(ship, ModShipKits.Build.Parse("weapons=off")), "another player's shown build");
                Assert.IsFalse(ModHardpoints.Hidden(ship, ModShipKits.Build.Parse("weapons=on")));
                var plain = ModShipKits.VariantAssembly(ship, ModShipKits.Build.Parse("body=b;tier=0"));
                var hidden = ModShipKits.VariantAssembly(ship, ModShipKits.PlayerBuild(ship));
                Assert.AreEqual(plain?.name, hidden?.name, "the same model: the toggle works on the instance");
            }
            finally { Session.ShipBuilds.Remove(ModShipKits.BuildKey(ship)); }
        }

        [Test]
        public void ADefaultBuildMayStartHiddenAndThePlayerShowThem()
        {
            int ship = KitShipMod("t_togd", true, "{ 'body': 'b', 'weapons': false }");
            Assert.IsTrue(ModHardpoints.Hidden(ship), "the default build hides them");
            try
            {
                Session.ShipBuilds[ModShipKits.BuildKey(ship)] = "body=b;weapons=on;tier=0";
                var b = ModShipKits.PlayerBuild(ship);
                Assert.AreEqual(ModShipKits.Build.WeaponsOn, b.weapons, "shown over the default's hidden is written out");
                Assert.IsFalse(ModHardpoints.Hidden(ship));
            }
            finally { Session.ShipBuilds.Remove(ModShipKits.BuildKey(ship)); }
        }

        [Test]
        public void AKitWithoutTheToggleNeverHidesThem()
        {
            int ship = KitShipMod("t_notog", false, "{ 'body': 'b', 'weapons': false }");
            Assert.IsFalse(ModHardpoints.Toggles(ship));
            try
            {
                Session.ShipBuilds[ModShipKits.BuildKey(ship)] = "body=b;weapons=off;tier=0";
                Assert.IsNull(ModShipKits.PlayerBuild(ship).weapons, "dropped by Normalize");
                Assert.IsFalse(ModHardpoints.Hidden(ship));
            }
            finally { Session.ShipBuilds.Remove(ModShipKits.BuildKey(ship)); }
        }
    }
}
