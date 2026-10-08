// ModGlbImages.cs
// Takes the embedded PNG / JPG images out of a GLB before glTFast reads it (ModGltf), so the mods' own texture loader
// (ModMaterials.PreloadImage) makes them instead: each different image once per mod (by its content hash), compressed and
// cached on disk. glTFast decodes every image of every model into an uncompressed RGBA32 texture of its own; the GoF3 Ships
// mod's 79 GLBs hold 1452 images (251 different ones, the guns' and the paint variants' shared), which came to over 3 GB
// and ran the phones out of memory (players' reports, 2026-10).
// Each image taken out points at one shared 1 x 1 PNG instead (its bufferView is kept, so every index in the file stays
// as it was), the other bufferViews are packed into a new binary chunk. Which role each image has comes from the
// materials: base colour and emission (sRGB), normal map, metallic-roughness map. GLBs this doesn't handle (several or
// external buffers, meshopt compression, no embedded images) are loaded as they are (null).
// Any thread.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GoF2Remake.Modding
{
    public static class ModGlbImages
    {
        /// <summary>What a material uses an image for (flags: one image may have several).</summary>
        [Flags]
        public enum Role { None = 0, Color = 1, Normal = 2, MetallicRoughness = 4 }

        public sealed class Image
        {
            public string hash, ext;
            public byte[] bytes;   // the file's bytes, until the texture is made
            public Role roles;
        }

        public sealed class Result
        {
            /// <summary>The GLB without its images.</summary>
            public byte[] glb;
            /// <summary>glTF texture index -> image index.</summary>
            public readonly Dictionary<int, int> textureImage = new Dictionary<int, int>();
            /// <summary>The images taken out, by image index.</summary>
            public readonly Dictionary<int, Image> images = new Dictionary<int, Image>();
        }

        const uint Magic = 0x46546C67, ChunkJson = 0x4E4F534A, ChunkBin = 0x004E4942;

        // A 1 x 1 PNG (opaque white) every image taken out points at.
        static readonly byte[] StandIn = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==");

        /// <summary>The GLB without its embedded images, or null (not a GLB, nothing to take out, or a layout this doesn't
        /// handle: then load it as it is).</summary>
        public static Result Strip(byte[] glb)
        {
            if (glb == null || glb.Length < 28 || BitConverter.ToUInt32(glb, 0) != Magic || BitConverter.ToUInt32(glb, 4) != 2) return null;
            int end = (int)Math.Min(BitConverter.ToUInt32(glb, 8), (uint)glb.Length);
            int pos = 12, jsonStart = -1, jsonLength = 0, binStart = -1, binLength = 0;
            while (pos + 8 <= end)
            {
                int length = (int)BitConverter.ToUInt32(glb, pos);
                uint type = BitConverter.ToUInt32(glb, pos + 4);
                if (length < 0 || pos + 8 + length > end) return null;
                if (type == ChunkJson && jsonStart < 0) { jsonStart = pos + 8; jsonLength = length; }
                else if (type == ChunkBin && binStart < 0) { binStart = pos + 8; binLength = length; }
                pos += 8 + length;
            }
            if (jsonStart < 0 || binStart < 0) return null;
            var root = JObject.Parse(Encoding.UTF8.GetString(glb, jsonStart, jsonLength));
            var images = root["images"] as JArray;
            var views = root["bufferViews"] as JArray;
            var buffers = root["buffers"] as JArray;
            if (images == null || images.Count == 0 || views == null || buffers == null || buffers.Count != 1 || buffers[0]["uri"] != null) return null;
            if (Mentions(root, "meshopt")) return null;   // its bufferViews point into a buffer of their own

            // The bufferViews anything but an image reads (accessors, Draco...): never taken out.
            var used = new HashSet<int>();
            foreach (var p in root.Properties()) if (p.Name != "images") CollectViews(p.Value, used);

            var result = new Result();
            var strip = new HashSet<int>();
            using (var md5 = MD5.Create())
                for (int i = 0; i < images.Count; i++)
                {
                    if (!(images[i] is JObject img) || img["uri"] != null || !(img["bufferView"] is JValue bv) || bv.Type != JTokenType.Integer) continue;
                    int v = (int)bv;
                    if (v < 0 || v >= views.Count || used.Contains(v) || !(views[v] is JObject view)) continue;
                    if ((int?)view["buffer"] != 0) continue;
                    int off = (int?)view["byteOffset"] ?? 0, len = (int?)view["byteLength"] ?? 0;
                    if (off < 0 || len <= 8 || off + len > binLength) continue;
                    int at = binStart + off;
                    string ext = glb[at] == 0x89 && glb[at + 1] == (byte)'P' && glb[at + 2] == (byte)'N' && glb[at + 3] == (byte)'G' ? "png"
                               : glb[at] == 0xFF && glb[at + 1] == 0xD8 ? "jpg" : null;
                    if (ext == null) continue;   // KTX, WebP...: glTFast's
                    var bytes = new byte[len];
                    Buffer.BlockCopy(glb, at, bytes, 0, len);
                    var h = md5.ComputeHash(bytes);
                    var sb = new StringBuilder(32);
                    foreach (var b in h) sb.Append(b.ToString("x2"));
                    result.images[i] = new Image { hash = sb.ToString(), ext = ext, bytes = bytes };
                    strip.Add(v);
                    img["mimeType"] = "image/png";
                }
            if (result.images.Count == 0) return null;

            if (root["textures"] is JArray textures)
                for (int t = 0; t < textures.Count; t++)
                    if (textures[t]?["source"] is JValue s && s.Type == JTokenType.Integer) result.textureImage[t] = (int)s;
            if (root["materials"] is JArray materials)
                foreach (var m in materials)
                {
                    var pbr = m?["pbrMetallicRoughness"];
                    Mark(result, pbr?["baseColorTexture"], Role.Color);
                    Mark(result, m?["emissiveTexture"], Role.Color);
                    Mark(result, m?["normalTexture"], Role.Normal);
                    Mark(result, pbr?["metallicRoughnessTexture"], Role.MetallicRoughness);
                }

            // The new binary chunk: every other bufferView packed (16-byte aligned), then the stand-in PNG.
            var bin = new MemoryStream(binLength / 4 + StandIn.Length + 64);
            for (int v = 0; v < views.Count; v++)
            {
                if (strip.Contains(v) || !(views[v] is JObject view) || (int?)view["buffer"] != 0) continue;
                int off = (int?)view["byteOffset"] ?? 0, len = (int?)view["byteLength"] ?? 0;
                if (off < 0 || len < 0 || off + len > binLength) return null;
                Pad(bin, 16, 0);
                view["byteOffset"] = (int)bin.Length;
                bin.Write(glb, binStart + off, len);
            }
            Pad(bin, 16, 0);
            int standIn = (int)bin.Length;
            bin.Write(StandIn, 0, StandIn.Length);
            foreach (int v in strip)
            {
                var view = (JObject)views[v];
                view["byteOffset"] = standIn;
                view["byteLength"] = StandIn.Length;
                view.Remove("byteStride");
            }
            Pad(bin, 4, 0);
            buffers[0]["byteLength"] = (int)bin.Length;

            var json = new MemoryStream();
            var text = Encoding.UTF8.GetBytes(root.ToString(Formatting.None));
            json.Write(text, 0, text.Length);
            Pad(json, 4, 0x20);

            var o = new MemoryStream(28 + (int)json.Length + (int)bin.Length);
            void U32(long x) { var b = BitConverter.GetBytes((uint)x); o.Write(b, 0, 4); }
            U32(Magic); U32(2); U32(12 + 8 + json.Length + 8 + bin.Length);
            U32(json.Length); U32(ChunkJson); json.WriteTo(o);
            U32(bin.Length); U32(ChunkBin); bin.WriteTo(o);
            result.glb = o.ToArray();
            return result;
        }

        static void Mark(Result r, JToken info, Role role)
        {
            if (info?["index"] is JValue i && i.Type == JTokenType.Integer && r.textureImage.TryGetValue((int)i, out int image)
                && r.images.TryGetValue(image, out var img))
                img.roles |= role;
        }

        static void CollectViews(JToken t, HashSet<int> into)
        {
            if (t is JObject o)
                foreach (var p in o.Properties())
                {
                    if (p.Name == "bufferView" && p.Value is JValue v && v.Type == JTokenType.Integer) into.Add((int)v);
                    else CollectViews(p.Value, into);
                }
            else if (t is JArray a)
                foreach (var e in a) CollectViews(e, into);
        }

        static bool Mentions(JObject root, string text)
        {
            foreach (var key in new[] { "extensionsUsed", "extensionsRequired" })
                if (root[key] is JArray a)
                    foreach (var e in a)
                        if (((string)e)?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void Pad(MemoryStream s, int to, byte with)
        {
            while (s.Length % to != 0) s.WriteByte(with);
        }
    }
}
