// ImageImport.cs
// Remake-only: the image formats a mod may use, wherever the game reads a mod's image (ModMaterials: skins, portraits,
// icons, hull textures, planets, suns, skies; ModInfo's preview) and in the Mod Builder's pickers (which store a picked image
// as a PNG in the project):
//   PNG, JPG      Unity's own decoder;
//   TGA           uncompressed or RLE, 8-bit grey / 24 / 32-bit, either row order;
//   BMP           8-bit with a palette, 24 / 32-bit, either row order;
//   DDS           DXT1 / DXT3 / DXT5 and uncompressed 24 / 32-bit (not the DX10 header's BC7 and others);
//   AEI           the game's own "AEimage" files: raw RGBA, the phone's ETC1 (types 0x14 / 0x16 / 0x17 / 0x40 / 0x42, as
//                 Reference/tools/asset_conversion/aei.py), else DXT1 / DXT5 by the data's size (the PC version's).
// The format comes from the file's first bytes (TGA, which has none, from its extension). Only the top mip level is kept.
// PNG / JPG go to Unity's own decoders; DecodePixels reads the rest on any thread (ModMaterials decodes off the main thread).
// Plain C#.

using System;
using System.IO;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public static class ImageImport
    {
        /// <summary>The extensions the builder's image pickers offer (without the dot).</summary>
        public static readonly string[] Extensions = { "png", "jpg", "jpeg", "tga", "bmp", "dds", "aei" };

        /// <summary>The same with the dot, for the mods' folders (ModTextures).</summary>
        public static readonly string[] FileExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".dds", ".aei" };

        /// <summary>A PNG or JPG: Unity's own decoders read it (LoadImage, UnityWebRequestTexture).</summary>
        public static bool IsNative(byte[] b) => b != null && (Starts(b, 0x89, (byte)'P', (byte)'N', (byte)'G') || Starts(b, 0xFF, 0xD8));

        /// <summary>The picked file as a PNG in the temporary folder (the caller copies it into the project and deletes it),
        /// null with the reason when it can't be read.</summary>
        public static string ToPng(string path, string originalName, out string error)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception e) { error = e.Message; return null; }
            var tex = Decode(bytes, originalName ?? path, false, false, out error);
            if (tex == null) return null;
            try
            {
                string dir = Path.Combine(Application.temporaryCachePath, "ModBuilderImages");
                Directory.CreateDirectory(dir);
                string png = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".png");
                File.WriteAllBytes(png, tex.EncodeToPNG());
                return png;
            }
            catch (Exception e) { error = e.Message; return null; }
            finally { UnityEngine.Object.Destroy(tex); }
        }

        /// <summary>An image file's bytes as a readable RGBA32 texture (main thread; null with the reason when it can't be
        /// read): PNG / JPG by Unity's decoder, the rest by DecodePixels. 'name' gives a TGA away by its extension.</summary>
        public static Texture2D Decode(byte[] bytes, string name, bool linear, bool mipmaps, out string error)
        {
            error = null;
            if (IsNative(bytes))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps, linear);
                if (t.LoadImage(bytes, false)) return t;
                UnityEngine.Object.Destroy(t);
                error = "The PNG / JPG couldn't be read.";
                return null;
            }
            return DecodePixels(bytes, name, out int w, out int h, out var px, out error) ? MakeTexture(w, h, px, linear, mipmaps) : null;
        }

        /// <summary>A texture from DecodePixels' result (main thread).</summary>
        public static Texture2D MakeTexture(int w, int h, Color32[] bottomUp, bool linear, bool mipmaps)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipmaps, linear);
            tex.SetPixels32(bottomUp);
            tex.Apply(mipmaps);
            return tex;
        }

        /// <summary>The pixels of a TGA, BMP, DDS or .aei (not PNG / JPG), bottom row first like Unity's textures; false with
        /// the reason when it can't be read. Touches no Unity object: safe on a worker thread.</summary>
        public static bool DecodePixels(byte[] b, string name, out int w, out int h, out Color32[] bottomUp, out string error)
        {
            w = h = 0;
            bottomUp = null;
            error = null;
            try
            {
                Color32[] px;   // top row first
                if (b == null || b.Length < 18) { error = "The file is empty or cut short."; return false; }
                if (Starts(b, (byte)'A', (byte)'E', (byte)'i', (byte)'m', (byte)'a', (byte)'g', (byte)'e')) px = Aei(b, out w, out h, out error);
                else if (Starts(b, (byte)'D', (byte)'D', (byte)'S', (byte)' ')) px = Dds(b, out w, out h, out error);
                else if (Starts(b, (byte)'B', (byte)'M')) px = Bmp(b, out w, out h, out error);
                else if (string.Equals(Path.GetExtension(name ?? ""), ".tga", StringComparison.OrdinalIgnoreCase)) px = Tga(b, out w, out h, out error);
                else { error = "Not an image the game reads (PNG, JPG, TGA, BMP, DDS or the game's .aei)."; return false; }
                if (px == null) return false;
                bottomUp = new Color32[px.Length];
                for (int y = 0; y < h; y++) Array.Copy(px, (h - 1 - y) * w, bottomUp, y * w, w);
                return true;
            }
            catch (Exception e) { error = "The image couldn't be read: " + e.Message; return false; }
        }

        static bool Starts(byte[] b, params byte[] magic)
        {
            if (b.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++) if (b[i] != magic[i]) return false;
            return true;
        }

        static int U16(byte[] b, int o) => b[o] | b[o + 1] << 8;
        static int I32(byte[] b, int o) => b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24;

        // ---- AEI (the game's "AEimage") ----------------------------------------------------------------------

        static Color32[] Aei(byte[] b, out int w, out int h, out string error)
        {
            error = null;
            int type = b[8];
            w = U16(b, 9); h = U16(b, 11);
            int o = 15 + 8 * U16(b, 13);   // after the regions
            if (type == 1 || type == 3 || type == 0x81)
            {
                var px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++, o += 4) px[i] = new Color32(b[o], b[o + 1], b[o + 2], b[o + 3]);
                return px;
            }
            int size = I32(b, o);
            o += 4;
            if (type == 0x14 || type == 0x16 || type == 0x17 || type == 0x40 || type == 0x42) return Etc1(b, o, w, h);
            if (size >= w * h) return Bc(b, o, w, h, 5);
            if (size >= w * h / 2) return Bc(b, o, w, h, 1);
            error = $"This .aei's kind (0x{type:x}) isn't known.";
            return null;
        }

        // ---- DDS ----------------------------------------------------------------------------------------------

        static Color32[] Dds(byte[] b, out int w, out int h, out string error)
        {
            error = null;
            h = I32(b, 12); w = I32(b, 16);
            int flags = I32(b, 80);
            string fourCC = System.Text.Encoding.ASCII.GetString(b, 84, 4);
            if ((flags & 0x4) != 0)
            {
                if (fourCC == "DXT1") return Bc(b, 128, w, h, 1);
                if (fourCC == "DXT3") return Bc(b, 128, w, h, 3);
                if (fourCC == "DXT5") return Bc(b, 128, w, h, 5);
                error = $"This DDS is {fourCC.Trim('\0')}: save it as PNG (the builder reads DXT1, DXT3, DXT5 and uncompressed DDS).";
                return null;
            }
            int bits = I32(b, 88), rMask = I32(b, 92), gMask = I32(b, 96), bMask = I32(b, 100), aMask = I32(b, 104);
            if (bits != 24 && bits != 32) { error = $"This DDS has {bits} bits per pixel: save it as PNG."; return null; }
            var px = new Color32[w * h];
            int step = bits / 8, o = 128;
            byte Channel(int v, int mask)
            {
                if (mask == 0) return 255;
                int shift = 0;
                while ((mask >> shift & 1) == 0) shift++;
                int max = (int)((uint)mask >> shift);
                return (byte)(((int)((uint)v >> shift) & max) * 255 / max);
            }
            for (int i = 0; i < px.Length; i++, o += step)
            {
                int v = b[o] | b[o + 1] << 8 | b[o + 2] << 16 | (step == 4 ? b[o + 3] << 24 : 0);
                px[i] = new Color32(Channel(v, rMask), Channel(v, gMask), Channel(v, bMask), (flags & 0x1) != 0 ? Channel(v, aMask) : (byte)255);
            }
            return px;
        }

        // ---- TGA ----------------------------------------------------------------------------------------------

        static Color32[] Tga(byte[] b, out int w, out int h, out string error)
        {
            error = null;
            int idLength = b[0], colorMapType = b[1], type = b[2];
            w = U16(b, 12); h = U16(b, 14);
            int bpp = b[16], desc = b[17];
            bool topFirst = (desc & 0x20) != 0;
            if (colorMapType != 0 || (type != 2 && type != 3 && type != 10 && type != 11) || (bpp != 8 && bpp != 24 && bpp != 32))
            {
                error = "This TGA's kind isn't read here (uncompressed or RLE, grey / 24 / 32-bit): save it as PNG.";
                return null;
            }
            int o = 18 + idLength, step = bpp / 8, n = w * h;
            var raw = new Color32[n];
            Color32 Read(ref int p)
            {
                Color32 c = step == 1 ? new Color32(b[p], b[p], b[p], 255)
                          : new Color32(b[p + 2], b[p + 1], b[p], step == 4 ? b[p + 3] : (byte)255);
                p += step;
                return c;
            }
            if (type == 2 || type == 3)
                for (int i = 0; i < n; i++) raw[i] = Read(ref o);
            else
                for (int i = 0; i < n;)
                {
                    int head = b[o++], count = (head & 0x7F) + 1;
                    if ((head & 0x80) != 0) { var c = Read(ref o); for (int k = 0; k < count && i < n; k++) raw[i++] = c; }
                    else for (int k = 0; k < count && i < n; k++) raw[i++] = Read(ref o);
                }
            if (topFirst) return raw;
            var px = new Color32[n];
            for (int y = 0; y < h; y++) Array.Copy(raw, (h - 1 - y) * w, px, y * w, w);
            return px;
        }

        // ---- BMP ----------------------------------------------------------------------------------------------

        static Color32[] Bmp(byte[] b, out int w, out int h, out string error)
        {
            error = null;
            int dataAt = I32(b, 10), dib = I32(b, 14);
            w = I32(b, 18);
            int rawH = I32(b, 22);
            h = Math.Abs(rawH);
            int bpp = U16(b, 28), compression = I32(b, 30);
            if ((bpp != 8 && bpp != 24 && bpp != 32) || (compression != 0 && compression != 3))
            {
                error = "This BMP's kind isn't read here (8-bit with a palette, 24 / 32-bit, uncompressed): save it as PNG.";
                return null;
            }
            Color32[] palette = null;
            if (bpp == 8)
            {
                int colours = dib >= 36 && I32(b, 46) > 0 ? I32(b, 46) : 256;
                palette = new Color32[colours];
                for (int i = 0; i < colours; i++) { int p = 14 + dib + 4 * i; palette[i] = new Color32(b[p + 2], b[p + 1], b[p], 255); }
            }
            int rowBytes = (w * bpp / 8 + 3) & ~3;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int src = dataAt + (rawH > 0 ? h - 1 - y : y) * rowBytes;   // positive height: the bottom row first
                for (int x = 0; x < w; x++)
                {
                    int p = src + x * bpp / 8;
                    px[y * w + x] = bpp == 8 ? palette[b[p]] : new Color32(b[p + 2], b[p + 1], b[p], bpp == 32 && compression == 0 ? (byte)255 : bpp == 32 ? b[p + 3] : (byte)255);
                }
            }
            return px;
        }

        // ---- block compression ---------------------------------------------------------------------------------

        /// <summary>DXT1 (kind 1), DXT3 (3) or DXT5 (5) blocks, top row first.</summary>
        static Color32[] Bc(byte[] b, int o, int w, int h, int kind)
        {
            var px = new Color32[w * h];
            int bw = (w + 3) / 4, bh = (h + 3) / 4, blockBytes = kind == 1 ? 8 : 16;
            var colours = new Color32[4];
            var alphas = new byte[8];
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++, o += blockBytes)
                {
                    int c = kind == 1 ? o : o + 8;
                    int c0 = U16(b, c), c1 = U16(b, c + 2);
                    colours[0] = Rgb565(c0);
                    colours[1] = Rgb565(c1);
                    if (kind != 1 || c0 > c1)
                    {
                        colours[2] = Mix(colours[0], colours[1], 2, 1, 3);
                        colours[3] = Mix(colours[0], colours[1], 1, 2, 3);
                    }
                    else
                    {
                        colours[2] = Mix(colours[0], colours[1], 1, 1, 2);
                        colours[3] = new Color32(0, 0, 0, 0);
                    }
                    uint bits = (uint)I32(b, c + 4);
                    if (kind == 5)
                    {
                        int a0 = b[o], a1 = b[o + 1];
                        alphas[0] = (byte)a0; alphas[1] = (byte)a1;
                        if (a0 > a1) for (int i = 1; i < 7; i++) alphas[1 + i] = (byte)(((7 - i) * a0 + i * a1) / 7);
                        else
                        {
                            for (int i = 1; i < 5; i++) alphas[1 + i] = (byte)(((5 - i) * a0 + i * a1) / 5);
                            alphas[6] = 0; alphas[7] = 255;
                        }
                    }
                    ulong abits = 0;
                    if (kind == 5) for (int i = 0; i < 6; i++) abits |= (ulong)b[o + 2 + i] << (8 * i);
                    for (int p = 0; p < 16; p++)
                    {
                        int x = bx * 4 + (p & 3), y = by * 4 + (p >> 2);
                        if (x >= w || y >= h) continue;
                        var col = colours[(bits >> (2 * p)) & 3];
                        if (kind == 5) col.a = alphas[(abits >> (3 * p)) & 7];
                        else if (kind == 3) col.a = (byte)(((b[o + p / 2] >> (4 * (p & 1))) & 15) * 17);
                        px[y * w + x] = col;
                    }
                }
            return px;
        }

        static Color32 Rgb565(int c) => new Color32((byte)((c >> 11 & 31) * 255 / 31), (byte)((c >> 5 & 63) * 255 / 63), (byte)((c & 31) * 255 / 31), 255);

        static Color32 Mix(Color32 a, Color32 b, int wa, int wb, int div) =>
            new Color32((byte)((a.r * wa + b.r * wb) / div), (byte)((a.g * wa + b.g * wb) / div), (byte)((a.b * wa + b.b * wb) / div), 255);

        static readonly int[,] Etc1Table =
        {
            { 2, 8 }, { 5, 17 }, { 9, 29 }, { 13, 42 }, { 18, 60 }, { 24, 80 }, { 33, 106 }, { 47, 183 },
        };

        /// <summary>ETC1 blocks (the phone version's textures), top row first.</summary>
        static Color32[] Etc1(byte[] b, int o, int w, int h)
        {
            var px = new Color32[w * h];
            int bw = (w + 3) / 4, bh = (h + 3) / 4;
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++, o += 8)
                {
                    uint hi = (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
                    uint lo = (uint)(b[o + 4] << 24 | b[o + 5] << 16 | b[o + 6] << 8 | b[o + 7]);
                    bool diff = (hi & 2) != 0, flip = (hi & 1) != 0;
                    int r1, g1, b1, r2, g2, b2;
                    if (diff)
                    {
                        int R = (int)(hi >> 27 & 31), G = (int)(hi >> 19 & 31), B = (int)(hi >> 11 & 31);
                        int dR = (int)(hi >> 24 & 7), dG = (int)(hi >> 16 & 7), dB = (int)(hi >> 8 & 7);
                        if (dR >= 4) dR -= 8;
                        if (dG >= 4) dG -= 8;
                        if (dB >= 4) dB -= 8;
                        int E5(int v) => v << 3 | v >> 2;
                        r1 = E5(R); g1 = E5(G); b1 = E5(B);
                        r2 = E5((R + dR) & 31); g2 = E5((G + dG) & 31); b2 = E5((B + dB) & 31);
                    }
                    else
                    {
                        r1 = (int)(hi >> 28 & 15) * 17; r2 = (int)(hi >> 24 & 15) * 17;
                        g1 = (int)(hi >> 20 & 15) * 17; g2 = (int)(hi >> 16 & 15) * 17;
                        b1 = (int)(hi >> 12 & 15) * 17; b2 = (int)(hi >> 8 & 15) * 17;
                    }
                    int t1 = (int)(hi >> 5 & 7), t2 = (int)(hi >> 2 & 7);
                    for (int x = 0; x < 4; x++)
                        for (int y = 0; y < 4; y++)
                        {
                            int i = x * 4 + y;   // the pixel indices run down the columns
                            bool second = flip ? y >= 2 : x >= 2;
                            int table = second ? t2 : t1;
                            int msb = (int)(lo >> (16 + i) & 1), lsb = (int)(lo >> i & 1);
                            int mod = Etc1Table[table, lsb] * (msb != 0 ? -1 : 1);
                            int px0 = bx * 4 + x, py0 = by * 4 + y;
                            if (px0 >= w || py0 >= h) continue;
                            int r = second ? r2 : r1, g = second ? g2 : g1, bb = second ? b2 : b1;
                            px[py0 * w + px0] = new Color32((byte)Mathf.Clamp(r + mod, 0, 255), (byte)Mathf.Clamp(g + mod, 0, 255), (byte)Mathf.Clamp(bb + mod, 0, 255), 255);
                        }
                }
            return px;
        }
    }
}
