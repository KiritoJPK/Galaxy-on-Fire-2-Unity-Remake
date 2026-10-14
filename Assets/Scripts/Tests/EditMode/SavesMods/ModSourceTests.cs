// ModSourceTests.cs
// Feature: CLAUDE.md "Mods (remake-only)" > A mod (a folder or a .zip, files at its root or in one top folder) and
// "Unreadable mod files" (ZipSource checks each entry's length and CRC-32 and throws a ModFileException for a damaged one),
// plus ModInfo's content hash (SHA-256 of every path and file, first 12 hex digits) and Crc32 (the zip format's CRC-32,
// IEEE reflected 0xEDB88320). Expected CRC values: the standard check value CRC-32("123456789") = 0xCBF43926 and other
// published values, a plain bit-by-bit CRC written here, and the CRC .NET's ZipArchive stores in the entries it writes.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GoF2Remake.Modding;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class ModSourceTests : SavesModsTest
    {
        // ---- Crc32 ----------------------------------------------------------------------------------------------------

        [TestCase("", 0x00000000u)]
        [TestCase("a", 0xE8B7BE43u)]
        [TestCase("abc", 0x352441C2u)]
        [TestCase("123456789", 0xCBF43926u)]
        [TestCase("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
        public void Crc32KnownValues(string text, uint crc)
        {
            Assert.AreEqual(crc, Crc32.Of(Encoding.ASCII.GetBytes(text)));
        }

        static uint BitwiseCrc(byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            foreach (byte b in data)
            {
                c ^= b;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320u : c >> 1;
            }
            return ~c;
        }

        [Test]
        public void Crc32MatchesABitwiseCrcAtEveryLength()
        {
            // The sliced loop takes 8 bytes at a time and the rest one by one: every length 0..70 and a long buffer.
            var rnd = new System.Random(1234);
            for (int n = 0; n <= 70; n++)
            {
                var data = new byte[n];
                rnd.NextBytes(data);
                Assert.AreEqual(BitwiseCrc(data), Crc32.Of(data), "length " + n);
            }
            var big = new byte[100003];
            rnd.NextBytes(big);
            Assert.AreEqual(BitwiseCrc(big), Crc32.Of(big));
        }

        [Test]
        public void Crc32MatchesTheZipArchivesOwn()
        {
            var rnd = new System.Random(99);
            var data = new byte[5000];
            rnd.NextBytes(data);
            string path = WriteZip(Path.Combine(Sandbox, "crc.zip"), false, ("a.bin", data), ("b.txt", Bytes("hello")));
            using var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
            Assert.AreEqual(zip.GetEntry("a.bin").Crc32, Crc32.Of(data));
            Assert.AreEqual(zip.GetEntry("b.txt").Crc32, Crc32.Of(Encoding.UTF8.GetBytes("hello")));
        }

        // ---- folders ------------------------------------------------------------------------------------------------

        string FolderMod()
        {
            string dir = Path.Combine(Sandbox, "src", "folder_mod");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "mod.json"), Q("{ 'id': 'folder_mod' }"));
            WriteFile(dir, "items.json", Bytes("[]"));
            WriteFile(dir, "text/en.json", Bytes("{}"));
            WriteFile(dir, "text/de.json", Bytes("{}"));
            WriteFile(dir, "text/old/fr.json", Bytes("{}"));
            WriteFile(dir, "Models/Ship.GLB", Dummy);
            return dir;
        }

        [Test]
        public void AFolderListsItsFilesWithForwardSlashes()
        {
            using var src = ModSource.Open(FolderMod());
            Assert.IsInstanceOf<FolderSource>(src);
            CollectionAssert.AreEqual(new[] { "items.json", "mod.json", "Models/Ship.GLB", "text/de.json", "text/en.json", "text/old/fr.json" }, src.Files);
        }

        [Test]
        public void PathsAreMatchedWithoutCase()
        {
            using var src = ModSource.Open(FolderMod());
            Assert.IsTrue(src.Exists("models/ship.glb"));
            Assert.IsTrue(src.Exists("MODELS\\SHIP.glb"), "backslashes too");
            Assert.IsTrue(src.Exists("/items.json"));
            Assert.IsFalse(src.Exists("models/other.glb"));
            Assert.AreEqual(Dummy, src.ReadBytes("models/ship.glb"));
            Assert.IsNull(src.ReadBytes("nothing.png"));
            Assert.IsNull(src.ReadText("nothing.json"));
        }

        [Test]
        public void FilesInListsOneFolderWithoutSubfolders()
        {
            using var src = ModSource.Open(FolderMod());
            CollectionAssert.AreEquivalent(new[] { "text/de.json", "text/en.json" }, src.FilesIn("text", ".json").ToList());
            CollectionAssert.AreEquivalent(new[] { "text/de.json", "text/en.json" }, src.FilesIn("TEXT/", ".JSON").ToList());
            Assert.AreEqual(0, src.FilesIn("text", ".png").Count());
            CollectionAssert.AreEquivalent(new[] { "Models/Ship.GLB" }, src.FilesIn("models").ToList());
        }

        [Test]
        public void NormaliseUsesForwardSlashesWithoutALeadingOne()
        {
            Assert.AreEqual("a/b/c.png", ModSource.Normalise("\\a\\b/c.png"));
            Assert.AreEqual("", ModSource.Normalise(null));
        }

        [Test]
        public void OnlyAFolderOrZipWithAModJsonIsAMod()
        {
            string empty = Path.Combine(Sandbox, "src", "empty");
            Directory.CreateDirectory(empty);
            File.WriteAllText(Path.Combine(empty, "items.json"), "[]");
            Assert.IsNull(ModSource.Open(empty));
            Assert.IsNull(ModSource.Open(Path.Combine(Sandbox, "src", "missing")));
            string txt = Path.Combine(Sandbox, "src", "mod.txt");
            File.WriteAllText(txt, "{}");
            Assert.IsNull(ModSource.Open(txt));
            string zip = WriteZip(Path.Combine(Sandbox, "src", "nomod.zip"), false, ("items.json", Bytes("[]")));
            Assert.IsNull(ModSource.Open(zip));
            // a mod.json two folders down doesn't count
            string deep = WriteZip(Path.Combine(Sandbox, "src", "deep.zip"), false, ("a/b/mod.json", Bytes("{ 'id': 'deep' }")));
            Assert.IsNull(ModSource.Open(deep));
        }

        // ---- zips ---------------------------------------------------------------------------------------------------

        [Test]
        public void AZipIsReadLikeAFolder()
        {
            string zip = WriteZip(Path.Combine(Sandbox, "src", "z.zip"), false,
                ("mod.json", Bytes("{ 'id': 'zipped' }")), ("text/en.json", Bytes("{ 'a': 'b' }")), ("Models/Ship.glb", Dummy));
            using var src = ModSource.Open(zip);
            Assert.IsInstanceOf<ZipSource>(src);
            Assert.AreEqual(zip, src.Location);
            CollectionAssert.AreEqual(new[] { "mod.json", "Models/Ship.glb", "text/en.json" }, src.Files);
            Assert.AreEqual(Dummy, src.ReadBytes("models/SHIP.glb"));
            Assert.AreEqual(Q("{ 'a': 'b' }"), src.ReadText("text/en.json"));
            Assert.AreEqual("zipped", ModManifest.Read(src).id);
        }

        [Test]
        public void AZipWithOneTopFolderIsReadFromInsideIt()
        {
            string zip = WriteZip(Path.Combine(Sandbox, "src", "top.zip"), false,
                ("my_mod/mod.json", Bytes("{ 'id': 'topped' }")), ("my_mod/items.json", Bytes("[]")), ("my_mod/fx/a.png", Dummy),
                ("elsewhere.txt", Bytes("not part of the mod")));
            using var src = ModSource.Open(zip);
            Assert.IsNotNull(src);
            CollectionAssert.AreEqual(new[] { "fx/a.png", "items.json", "mod.json" }, src.Files);
            Assert.AreEqual("topped", ModManifest.Read(src).id);
            Assert.AreEqual(Dummy, src.ReadBytes("fx/a.png"));
        }

        [Test]
        public void ABomInAZippedFileIsIgnored()
        {
            var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes("{ 'id': 'zbom' }")).ToArray();
            using var src = ModSource.Open(WriteZip(Path.Combine(Sandbox, "src", "bom.zip"), false, ("mod.json", bom)));
            Assert.AreEqual("zbom", ModManifest.Read(src).id);
        }

        /// <summary>A zip whose entry 'damaged' has one byte of its stored data changed (its CRC no longer fits).</summary>
        string DamagedZip(string damaged)
        {
            const string marker = "PAYLOAD-0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ-PAYLOAD";
            string path = WriteZip(Path.Combine(Sandbox, "src", "damaged.zip"), true,
                ("mod.json", damaged == "mod.json" ? Bytes("{ 'id': 'damaged', 'x': '" + marker + "' }") : Bytes("{ 'id': 'damaged' }")),
                ("good.bin", Bytes("fine")),
                ("data.bin", Encoding.ASCII.GetBytes(marker)));
            var bytes = File.ReadAllBytes(path);
            var m = Encoding.ASCII.GetBytes(marker);
            int at = -1;
            for (int i = 0; i + m.Length <= bytes.Length && at < 0; i++)
                if (bytes.Skip(i).Take(m.Length).SequenceEqual(m)) at = i;
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "the stored data wasn't found in the archive");
            bytes[at + 20] ^= 0x5A;
            File.WriteAllBytes(path, bytes);
            return path;
        }

        [Test]
        public void ADamagedEntryThrowsAModFileException()
        {
            using var src = ModSource.Open(DamagedZip("data.bin"));
            Assert.IsNotNull(src);
            Assert.AreEqual(Encoding.UTF8.GetBytes("fine"), src.ReadBytes("good.bin"), "the other entries still read");
            var e = Assert.Throws<ModFileException>(() => src.ReadBytes("data.bin"));
            StringAssert.Contains("data.bin is damaged in damaged.zip", e.Message);
            Assert.IsTrue(ModFileException.IsFileError(e));
        }

        [Test]
        public void ADamagedManifestMakesTheModBroken()
        {
            string path = DamagedZip("mod.json");
            Directory.CreateDirectory(ModsFolder);
            File.Move(path, Path.Combine(ModsFolder, "damaged.zip"));
            ModManager.Scan();
            var m = ModManager.Installed.Single();
            Assert.IsTrue(m.Broken);
            StringAssert.Contains("damaged", string.Join(" ", m.Errors));
        }

        [Test]
        public void TheHashOfADamagedModDoesntThrow()
        {
            using var src = ModSource.Open(DamagedZip("data.bin"));
            var info = new ModInfo(ModManifest.Read(src), src);
            Assert.AreEqual(12, info.Hash.Length);
        }

        [Test]
        public void AClosedZipThrowsAModFileException()
        {
            var src = ModSource.Open(WriteZip(Path.Combine(Sandbox, "src", "closed.zip"), false, ("mod.json", Bytes("{ 'id': 'c' }"))));
            src.Dispose();
            src.Dispose();   // twice is fine
            Assert.Throws<ModFileException>(() => src.ReadBytes("mod.json"));
        }

        [Test]
        public void FileErrorsAreTheReadingKinds()
        {
            Assert.IsTrue(ModFileException.IsFileError(new IOException("x")));
            Assert.IsTrue(ModFileException.IsFileError(new InvalidDataException("x")));
            Assert.IsTrue(ModFileException.IsFileError(new ObjectDisposedException("x")));
            Assert.IsTrue(ModFileException.IsFileError(new ModFileException("x")));
            Assert.IsFalse(ModFileException.IsFileError(new InvalidOperationException("x")));
            Assert.IsFalse(ModFileException.IsFileError(new NullReferenceException()));
        }

        // ---- the content hash ----------------------------------------------------------------------------------------

        [Test]
        public void TheHashIsTwelveHexDigitsAndTheSameForAFolderAndItsZip()
        {
            string dir = FolderMod();
            var entries = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => (f.Substring(dir.Length + 1).Replace('\\', '/'), File.ReadAllBytes(f))).ToArray();
            string zip = WriteZip(Path.Combine(Sandbox, "src", "same.zip"), false, entries);
            using var a = ModSource.Open(dir);
            using var b = ModSource.Open(zip);
            var ha = new ModInfo(ModManifest.Read(a), a).Hash;
            var hb = new ModInfo(ModManifest.Read(b), b).Hash;
            StringAssert.IsMatch("^[0-9a-f]{12}$", ha);
            Assert.AreEqual(ha, hb);
        }

        [Test]
        public void TheHashChangesWithAFilesContentOrName()
        {
            string dir = FolderMod();
            string Hash()
            {
                using var s = ModSource.Open(dir);
                return new ModInfo(ModManifest.Read(s), s).Hash;
            }
            string h0 = Hash();
            Assert.AreEqual(h0, Hash(), "deterministic");
            File.WriteAllText(Path.Combine(dir, "items.json"), "[ ]");
            string h1 = Hash();
            Assert.AreNotEqual(h0, h1);
            File.Move(Path.Combine(dir, "items.json"), Path.Combine(dir, "items2.json"));
            Assert.AreNotEqual(h1, Hash());
        }

        [Test]
        public void SignatureIsIdVersionAndHash()
        {
            string dir = Path.Combine(Sandbox, "src", "sig");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "mod.json"), Q("{ 'id': 'sig', 'version': '3.2' }"));
            using var s = ModSource.Open(dir);
            var info = new ModInfo(ModManifest.Read(s), s);
            Assert.AreEqual("sig@3.2#" + info.Hash, info.Signature);
            Assert.IsFalse(info.Broken);
            info.Errors.Add("x");
            Assert.IsTrue(info.Broken);
            var none = new ModInfo(null, s);
            Assert.IsTrue(none.Broken);
            Assert.AreEqual("sig", none.Id, "without a manifest: the folder's name");
            Assert.AreEqual("?", none.Version);
        }
    }
}
