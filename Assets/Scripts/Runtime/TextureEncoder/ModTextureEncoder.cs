// ModTextureEncoder.cs
// Remake mods: compresses a mod's texture to DXT1 (BC1, opaque) or DXT5 (BC3, with alpha) with its mipmaps in Burst jobs
// on Unity's worker threads, so a ship's 2048 px textures don't stall the main thread (Texture2D.Compress takes ~110-145 ms
// each there, and the main menu stuttered while the mods loaded) and make no garbage (a managed encoder's ~25 MB per
// texture brought a 2 s garbage collection). The encoder is the classic real-time one (J.M.P. van Waveren, "Real-Time DXT
// Compression", 2006): per 4 x 4 block the colour bounding box inset by a sixteenth, the end points in 565, each pixel the
// nearest of the four palette colours; DXT5's alpha the same with its eight levels. Mipmaps by 2 x 2 box filtering.
// Unity's raw row order (bottom row first) in and out, so LoadRawTextureData takes the result.
// The work is spread over the job workers (one texture used to be one job on one core, ~40 ms for 2048 px in the Editor,
// while the rest of a 24-thread CPU waited): a role's conversion (a glTF metallic-roughness map to URP's metallic /
// smoothness, a normal map's X into alpha) per pixel row, the opacity scan, each mip level per row (one level after the
// other), then every block row of every level at once. The conversion ran on the main thread before (ModMaterials.Convert).
// ETC2 for phones (Target.Etc2: ETC2_RGB, or ETC2_RGBA8 with alpha; every OpenGL ES 3 / Vulkan device reads it): the
// colour in ETC1's individual / differential modes (also valid ETC2, its T / H / planar modes left out), per sub-block
// pair (2 x 4 side by side or 4 x 2 stacked, the better) the average colour as the base and the intensity table with the
// least error; the alpha as EAC (base, multiplier and table with the least error). Phones compressed every texture with
// Texture2D.Compress on the main thread, one a frame (ETC2 high quality took several times as long as the fast mode).
// ModMaterials.PreloadTexture uses it where the GPU reads DXT (desktop) or ETC2 (phones); elsewhere Texture2D.Compress stays.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace GoF2Remake.Modding
{
    public static class ModTextureEncoder
    {
        /// <summary>A role's conversion of the top level before it is compressed (ModMaterials' Remap).</summary>
        public enum Remap { None = 0, NormalToAlpha = 1, MetallicRoughness = 2 }

        /// <summary>What the GPU reads: DXT1 / DXT5 (desktop) or ETC2_RGB / ETC2_RGBA8 (phones).</summary>
        public enum Target { Dxt, Etc2 }

        /// <summary>A running encode: Done once the jobs finished; then Take makes the texture (main thread).</summary>
        public sealed class Work
        {
            internal JobHandle handle;
            internal NativeArray<byte> source, mips, output;
            internal NativeArray<int> rows, flags;   // rows: per block row its level and row; flags[0] 1 = some pixel isn't opaque
            internal int width, height, mipCount, blocks;
            internal bool linear;
            internal Target target;

            public bool Done => handle.IsCompleted;

            int Bytes => blocks * (flags[0] == 1 ? 16 : 8);

            /// <summary>The compressed data with its mipmaps, as Upload hands it to the texture (ModTextureCache writes it from
            /// here, on a worker thread, without a managed copy); valid until Dispose.</summary>
            public NativeArray<byte> Data
            {
                get { handle.Complete(); return output.GetSubArray(0, Bytes); }
            }

            /// <summary>The compressed texture; the buffers stay (Data) until Dispose.</summary>
            public Texture2D Upload(string name)
            {
                handle.Complete();
                bool alpha = flags[0] == 1;
                var format = target == Target.Etc2 ? (alpha ? TextureFormat.ETC2_RGBA8 : TextureFormat.ETC2_RGB)
                           : alpha ? TextureFormat.DXT5 : TextureFormat.DXT1;
                var t = new Texture2D(width, height, format, mipCount, linear) { name = name };
                t.LoadRawTextureData(output.GetSubArray(0, Bytes));
                t.Apply(false, true);
                return t;
            }

            /// <summary>The compressed texture (frees the buffers).</summary>
            public Texture2D Take(string name)
            {
                var t = Upload(name);
                Dispose();
                return t;
            }

            public void Dispose()
            {
                handle.Complete();
                if (source.IsCreated) source.Dispose();
                if (mips.IsCreated) mips.Dispose();
                if (output.IsCreated) output.Dispose();
                if (rows.IsCreated) rows.Dispose();
                if (flags.IsCreated) flags.Dispose();
            }
        }

        /// <summary>Starts compressing the decoded texture's top level (its width and height multiples of 4, RGBA32 / ARGB32 /
        /// RGB24), converted for its role first ('remap', with the metallic-roughness factors); null for another format. The
        /// texture can go once this returns (its pixels are copied).</summary>
        public static Work Start(Texture2D t, bool linear, Remap remap = Remap.None, float metal = 1f, float rough = 1f, Target target = Target.Dxt)
        {
            int format = t.format == TextureFormat.RGBA32 ? 0 : t.format == TextureFormat.ARGB32 ? 1 : t.format == TextureFormat.RGB24 ? 2 : -1;
            int w = t.width, h = t.height;
            if (format < 0 || w % 4 != 0 || h % 4 != 0) return null;
            var pixels = t.GetPixelData<byte>(0);
            int bytes = w * h * (format == 2 ? 3 : 4);
            if (pixels.Length < bytes) return null;

            // The levels below the top one, RGBA, one after the other; the block rows of every level.
            int mipCount = 1, mipBytes = 0, blocks = 0, blockRows = 0;
            for (int lw = w, lh = h; ; lw = Math.Max(1, lw / 2), lh = Math.Max(1, lh / 2))
            {
                blocks += Math.Max(1, (lw + 3) / 4) * Math.Max(1, (lh + 3) / 4);
                blockRows += Math.Max(1, (lh + 3) / 4);
                if (mipCount > 1) mipBytes += lw * lh * 4;
                if (lw == 1 && lh == 1) break;
                mipCount++;
            }
            var work = new Work
            {
                width = w, height = h, linear = linear, mipCount = mipCount, blocks = blocks, target = target,
                source = new NativeArray<byte>(pixels.GetSubArray(0, bytes), Allocator.Persistent),
                mips = new NativeArray<byte>(Math.Max(4, mipBytes), Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                output = new NativeArray<byte>(blocks * 16, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                rows = new NativeArray<int>(blockRows * 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                flags = new NativeArray<int>(1, Allocator.Persistent),
            };
            int r = 0;
            for (int level = 0, lh = h; level < mipCount; level++, lh = Math.Max(1, lh / 2))
                for (int by = 0; by < Math.Max(1, (lh + 3) / 4); by++) { work.rows[r++] = level; work.rows[r++] = by; }

            JobHandle top = default;
            if (remap != Remap.None)
            {
                // The conversion writes RGBA: an RGB24 source gets a buffer of its own.
                if (format == 2)
                {
                    var rgba = new NativeArray<byte>(w * h * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    top = new RemapJob { source = work.source, target = rgba, format = 2, width = w, remap = (int)remap, metal = metal, rough = rough }.Schedule(h, 16);
                    top = work.source.Dispose(top);
                    work.source = rgba;
                }
                else top = new RemapJob { source = work.source, target = work.source, format = format, width = w, remap = (int)remap, metal = metal, rough = rough }.Schedule(h, 16);
                format = 0;
            }
            var scan = new OpacityJob { source = work.source, format = format, flags = work.flags }.Schedule(top);
            var mipsDone = top;
            for (int level = 1, lw = w, lh = h, from = -1, to = 0; level < mipCount; level++)
            {
                int nw = Math.Max(1, lw / 2), nh = Math.Max(1, lh / 2);
                mipsDone = new MipJob
                {
                    source = work.source, mips = work.mips, fromLevel0 = from < 0, format = format, from = Math.Max(0, from), to = to,
                    lw = lw, lh = lh, nw = nw,
                }.Schedule(nh, 32, mipsDone);
                from = to;
                to += nw * nh * 4;
                lw = nw;
                lh = nh;
            }
            work.handle = new EncodeJob
            {
                source = work.source, mips = work.mips, output = work.output, rows = work.rows, flags = work.flags,
                format = format, width = w, height = h, etc = target == Target.Etc2,
            }.Schedule(blockRows, 4, JobHandle.CombineDependencies(scan, mipsDone));
            JobHandle.ScheduleBatchedJobs();
            return work;
        }

        static void Pixel(NativeArray<byte> px, int fmt, int i, out byte r, out byte g, out byte b, out byte a)
        {
            if (fmt == 0) { int s = i * 4; r = px[s]; g = px[s + 1]; b = px[s + 2]; a = px[s + 3]; }
            else if (fmt == 1) { int s = i * 4; a = px[s]; r = px[s + 1]; g = px[s + 2]; b = px[s + 3]; }
            else { int s = i * 3; r = px[s]; g = px[s + 1]; b = px[s + 2]; a = 255; }
        }

        /// <summary>One pixel row converted for its role, RGBA out: NormalToAlpha copies X (red) into alpha; MetallicRoughness
        /// makes URP Lit's map, R metallic = metal x B, A smoothness = 1 - rough x G (ModMaterials.Convert's rule).</summary>
        [BurstCompile]
        struct RemapJob : IJobParallelFor
        {
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<byte> source;
            [NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<byte> target;
            public int format, width, remap;
            public float metal, rough;

            public void Execute(int y)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    Pixel(source, format, i, out byte r, out byte g, out byte b, out byte a);
                    int d = i * 4;
                    if (remap == (int)Remap.NormalToAlpha) { target[d] = r; target[d + 1] = g; target[d + 2] = b; target[d + 3] = r; }
                    else
                    {
                        target[d] = (byte)(b * metal + 0.5f);
                        target[d + 1] = 0;
                        target[d + 2] = 0;
                        target[d + 3] = (byte)(255 - (int)(g * rough + 0.5f));
                    }
                }
            }
        }

        /// <summary>DXT5 when some pixel of the top level isn't opaque.</summary>
        [BurstCompile]
        struct OpacityJob : IJob
        {
            [ReadOnly] public NativeArray<byte> source;
            public int format;
            public NativeArray<int> flags;

            public void Execute()
            {
                flags[0] = 0;
                if (format == 2) return;
                int offset = format == 0 ? 3 : 0;
                for (int i = offset; i < source.Length; i += 4) if (source[i] < 250) { flags[0] = 1; return; }
            }
        }

        /// <summary>One row of the next mip level (2 x 2 box filter), from the top level or the level before in 'mips'.</summary>
        [BurstCompile]
        struct MipJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> source;
            [NativeDisableParallelForRestriction] public NativeArray<byte> mips;
            public bool fromLevel0;
            public int format, from, to, lw, lh, nw;

            byte Get(int i, int c)
            {
                if (!fromLevel0) return mips[from + i * 4 + c];
                Pixel(source, format, i, out byte r, out byte g, out byte b, out byte a);
                return c == 0 ? r : c == 1 ? g : c == 2 ? b : a;
            }

            public void Execute(int y)
            {
                int y0 = Math.Min(y * 2, lh - 1), y1 = Math.Min(y * 2 + 1, lh - 1);
                for (int x = 0; x < nw; x++)
                {
                    int x0 = Math.Min(x * 2, lw - 1), x1 = Math.Min(x * 2 + 1, lw - 1);
                    int i0 = y0 * lw + x0, i1 = y0 * lw + x1, i2 = y1 * lw + x0, i3 = y1 * lw + x1;
                    int d = to + (y * nw + x) * 4;
                    for (int c = 0; c < 4; c++)
                        mips[d + c] = (byte)((Get(i0, c) + Get(i1, c) + Get(i2, c) + Get(i3, c) + 2) >> 2);
                }
            }
        }

        /// <summary>One block row of one level: its blocks at their place in the output (level by level, row by row).</summary>
        [BurstCompile]
        struct EncodeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> source, mips;
            [ReadOnly] public NativeArray<int> rows, flags;
            [NativeDisableParallelForRestriction] public NativeArray<byte> output;
            public int format, width, height;
            public bool etc;

            public void Execute(int index)
            {
                int level = rows[index * 2], by = rows[index * 2 + 1];
                bool alpha = flags[0] == 1;
                int size = alpha ? 16 : 8;
                // Where this level starts: in the output (its blocks before it) and in 'mips'.
                int lw = width, lh = height, blocksBefore = 0, mipOffset = 0;
                for (int l = 0; l < level; l++)
                {
                    blocksBefore += Math.Max(1, (lw + 3) / 4) * Math.Max(1, (lh + 3) / 4);
                    if (l > 0) mipOffset += lw * lh * 4;
                    lw = Math.Max(1, lw / 2);
                    lh = Math.Max(1, lh / 2);
                }
                int bw = Math.Max(1, (lw + 3) / 4);
                int o = (blocksBefore + by * bw) * size;
                var block = new NativeArray<byte>(64, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int bx = 0; bx < bw; bx++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        int sy = Math.Min(by * 4 + y, lh - 1);
                        for (int x = 0; x < 4; x++)
                        {
                            int sx = Math.Min(bx * 4 + x, lw - 1);
                            byte r, g, b, a;
                            if (level == 0) Pixel(source, format, sy * lw + sx, out r, out g, out b, out a);
                            else { int s = mipOffset + (sy * lw + sx) * 4; r = mips[s]; g = mips[s + 1]; b = mips[s + 2]; a = mips[s + 3]; }
                            int d = (y * 4 + x) * 4;
                            block[d] = r; block[d + 1] = g; block[d + 2] = b; block[d + 3] = a;
                        }
                    }
                    if (etc)
                    {
                        if (alpha) { EacAlphaBlock(block, output, o); o += 8; }
                        EtcColorBlock(block, output, o);
                    }
                    else
                    {
                        if (alpha) { AlphaBlock(block, output, o); o += 8; }
                        ColorBlock(block, output, o);
                    }
                    o += 8;
                }
                block.Dispose();
            }

            static int To565(int r, int g, int b) => ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);

            static void From565(int c, out int r, out int g, out int b)
            {
                r = (c >> 11) & 31; g = (c >> 5) & 63; b = c & 31;
                r = (r << 3) | (r >> 2); g = (g << 2) | (g >> 4); b = (b << 3) | (b >> 2);
            }

            static int Sq(int v) => v * v;

            /// <summary>The colour block: 2 x 565 end points (c0 > c1: four colours, also how DXT5 reads it), 2 bits per pixel.</summary>
            static void ColorBlock(NativeArray<byte> p, NativeArray<byte> output, int o)
            {
                int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
                for (int i = 0; i < 64; i += 4)
                {
                    int r = p[i], g = p[i + 1], b = p[i + 2];
                    minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                    minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
                    minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
                }
                // The box inset by a sixteenth: the palette's ends a little inside the extremes (less error overall).
                int ir = (maxR - minR) >> 4, ig = (maxG - minG) >> 4, ib = (maxB - minB) >> 4;
                minR = Math.Min(255, minR + ir); minG = Math.Min(255, minG + ig); minB = Math.Min(255, minB + ib);
                maxR = Math.Max(0, maxR - ir); maxG = Math.Max(0, maxG - ig); maxB = Math.Max(0, maxB - ib);
                int c0 = To565(maxR, maxG, maxB), c1 = To565(minR, minG, minB);
                uint indices = 0;
                if (c0 != c1)
                {
                    if (c0 < c1) { int t = c0; c0 = c1; c1 = t; }
                    From565(c0, out int r0, out int g0, out int b0);
                    From565(c1, out int r1, out int g1, out int b1);
                    int r2 = (2 * r0 + r1) / 3, g2 = (2 * g0 + g1) / 3, b2 = (2 * b0 + b1) / 3;
                    int r3 = (r0 + 2 * r1) / 3, g3 = (g0 + 2 * g1) / 3, b3 = (b0 + 2 * b1) / 3;
                    for (int i = 0; i < 16; i++)
                    {
                        int r = p[i * 4], g = p[i * 4 + 1], b = p[i * 4 + 2];
                        int d0 = Sq(r - r0) + Sq(g - g0) + Sq(b - b0);
                        int d1 = Sq(r - r1) + Sq(g - g1) + Sq(b - b1);
                        int d2 = Sq(r - r2) + Sq(g - g2) + Sq(b - b2);
                        int d3 = Sq(r - r3) + Sq(g - g3) + Sq(b - b3);
                        uint best = 0; int bd = d0;
                        if (d1 < bd) { bd = d1; best = 1; }
                        if (d2 < bd) { bd = d2; best = 2; }
                        if (d3 < bd) best = 3;
                        indices |= best << (i * 2);
                    }
                }
                output[o] = (byte)c0; output[o + 1] = (byte)(c0 >> 8);
                output[o + 2] = (byte)c1; output[o + 3] = (byte)(c1 >> 8);
                output[o + 4] = (byte)indices; output[o + 5] = (byte)(indices >> 8);
                output[o + 6] = (byte)(indices >> 16); output[o + 7] = (byte)(indices >> 24);
            }

            // ---- ETC2 (phones) ----

            // ETC1 / ETC2 intensity tables: each codeword's small and large modifier (pixel indices 0..3 = +a, +b, -a, -b).
            static readonly int[] EtcTables = { 2, 8, 5, 17, 9, 29, 13, 42, 18, 60, 24, 80, 33, 106, 47, 183 };

            // EAC alpha modifier tables (16 x 8, ETC2 spec 3.9).
            static readonly int[] EacTables =
            {
                -3, -6, -9, -15, 2, 5, 8, 14,   -3, -7, -10, -13, 2, 6, 9, 12,   -2, -5, -8, -13, 1, 4, 7, 12,
                -2, -4, -6, -13, 1, 3, 5, 12,   -3, -6, -8, -12, 2, 5, 7, 11,    -3, -7, -9, -11, 2, 6, 8, 10,
                -4, -7, -8, -11, 3, 6, 7, 10,   -3, -5, -8, -11, 2, 4, 7, 10,    -2, -6, -8, -10, 1, 5, 7, 9,
                -2, -5, -8, -10, 1, 4, 7, 9,    -2, -4, -8, -10, 1, 3, 7, 9,     -2, -5, -7, -10, 1, 4, 6, 9,
                -3, -4, -7, -10, 2, 3, 6, 9,    -1, -2, -3, -10, 0, 1, 2, 9,     -4, -6, -8, -9, 3, 5, 7, 8,
                -3, -5, -7, -9, 2, 4, 6, 8,
            };

            static int Clamp255(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
            static int Q5(int c) => (c * 31 + 127) / 255;
            static int E5(int q) => (q << 3) | (q >> 2);
            static int Q4(int c) => (c * 15 + 127) / 255;

            /// <summary>Pixel (x, y) of the block is in the second sub-block: the right half (flip 0) or the bottom half (flip 1).</summary>
            static bool Second(int flip, int x, int y) => flip == 0 ? x >= 2 : y >= 2;

            /// <summary>The ETC colour block (ETC1's modes, valid ETC2): for both splits the sub-blocks' average colours as the
            /// bases (differential 555 + 333 when the second is within -4..3 of the first, else individual 444, so the decoder
            /// never sees ETC2's T / H / planar overflow) and each sub-block's intensity table; the split with the least error.
            /// A modifier moves all three channels alike, so a pixel's index follows from its brightness offset from the base
            /// (the sum of its channel differences, D): the table is chosen on that (error (D - 3m)^2 / 3 per pixel), the exact
            /// clamped error only decides the split (a brute-force search over every modifier and channel took ~60 times
            /// Texture2D.Compress's fast mode's time). 64 bits big-endian: bases, tables (2 x 3), diff and flip bits, then the
            /// pixel indices column by column (pixel (x, y) at bit x * 4 + y: its index's low bit there, its high bit 16 above).</summary>
            static void EtcColorBlock(NativeArray<byte> p, NativeArray<byte> output, int o)
            {
                ulong best = 0;
                long bestErr = long.MaxValue;
                for (int flip = 0; flip < 2; flip++)
                {
                    int r0 = 0, g0 = 0, b0 = 0, r1 = 0, g1 = 0, b1 = 0;
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            int i = (y * 4 + x) * 4;
                            if (Second(flip, x, y)) { r1 += p[i]; g1 += p[i + 1]; b1 += p[i + 2]; }
                            else { r0 += p[i]; g0 += p[i + 1]; b0 += p[i + 2]; }
                        }
                    r0 = (r0 + 4) >> 3; g0 = (g0 + 4) >> 3; b0 = (b0 + 4) >> 3;
                    r1 = (r1 + 4) >> 3; g1 = (g1 + 4) >> 3; b1 = (b1 + 4) >> 3;
                    int qr0 = Q5(r0), qg0 = Q5(g0), qb0 = Q5(b0), qr1 = Q5(r1), qg1 = Q5(g1), qb1 = Q5(b1);
                    int dr = qr1 - qr0, dg = qg1 - qg0, db = qb1 - qb0;
                    ulong bits;
                    int er0, eg0, eb0, er1, eg1, eb1;
                    if (dr >= -4 && dr <= 3 && dg >= -4 && dg <= 3 && db >= -4 && db <= 3)
                    {
                        er0 = E5(qr0); eg0 = E5(qg0); eb0 = E5(qb0);
                        er1 = E5(qr1); eg1 = E5(qg1); eb1 = E5(qb1);
                        bits = ((ulong)qr0 << 59) | ((ulong)(dr & 7) << 56) | ((ulong)qg0 << 51) | ((ulong)(dg & 7) << 48)
                             | ((ulong)qb0 << 43) | ((ulong)(db & 7) << 40) | (1UL << 33);
                    }
                    else
                    {
                        int ir0 = Q4(r0), ig0 = Q4(g0), ib0 = Q4(b0), ir1 = Q4(r1), ig1 = Q4(g1), ib1 = Q4(b1);
                        er0 = ir0 * 17; eg0 = ig0 * 17; eb0 = ib0 * 17;
                        er1 = ir1 * 17; eg1 = ig1 * 17; eb1 = ib1 * 17;
                        bits = ((ulong)ir0 << 60) | ((ulong)ir1 << 56) | ((ulong)ig0 << 52) | ((ulong)ig1 << 48)
                             | ((ulong)ib0 << 44) | ((ulong)ib1 << 40);
                    }
                    bits |= (ulong)flip << 32;
                    long err = 0;
                    for (int sub = 0; sub < 2; sub++)
                    {
                        int br = sub == 0 ? er0 : er1, bg = sub == 0 ? eg0 : eg1, bb = sub == 0 ? eb0 : eb1, bsum = br + bg + bb;
                        // The table: the least brightness error over the sub-block's eight pixels.
                        int table = 0;
                        long tableErr = long.MaxValue;
                        for (int cw = 0; cw < 8; cw++)
                        {
                            int a3 = EtcTables[cw * 2] * 3, b3 = EtcTables[cw * 2 + 1] * 3, mid = a3 + b3;
                            long e = 0;
                            for (int y = 0; y < 4; y++)
                                for (int x = 0; x < 4; x++)
                                {
                                    if (Second(flip, x, y) != (sub == 1)) continue;
                                    int i = (y * 4 + x) * 4;
                                    int d = Math.Abs(p[i] + p[i + 1] + p[i + 2] - bsum);
                                    int m = 2 * d < mid ? a3 : b3;
                                    e += (long)(d - m) * (d - m);
                                }
                            if (e < tableErr) { tableErr = e; table = cw; }
                        }
                        // Its indices, and the exact (clamped) error for the choice of split.
                        int ma = EtcTables[table * 2], mb = EtcTables[table * 2 + 1];
                        for (int y = 0; y < 4; y++)
                            for (int x = 0; x < 4; x++)
                            {
                                if (Second(flip, x, y) != (sub == 1)) continue;
                                int i = (y * 4 + x) * 4;
                                int pr = p[i], pg = p[i + 1], pb = p[i + 2];
                                int d = pr + pg + pb - bsum;
                                int k = (2 * Math.Abs(d) < 3 * (ma + mb) ? 0 : 1) + (d < 0 ? 2 : 0);
                                int m = (k & 1) == 0 ? ma : mb;
                                if (k >= 2) m = -m;
                                err += Sq(Clamp255(br + m) - pr) + Sq(Clamp255(bg + m) - pg) + Sq(Clamp255(bb + m) - pb);
                                int pos = x * 4 + y;
                                bits |= ((ulong)(k >> 1) << (pos + 16)) | ((ulong)(k & 1) << pos);
                            }
                        bits |= (ulong)table << (sub == 0 ? 37 : 34);
                    }
                    if (err < bestErr) { bestErr = err; best = bits; }
                }
                for (int k = 0; k < 8; k++) output[o + k] = (byte)(best >> (56 - 8 * k));
            }

            // Each EAC table's indices in the order of their modifiers (the four negative ones, then the positive ones).
            static readonly int[] EacOrder = { 3, 2, 1, 0, 4, 5, 6, 7 };

            /// <summary>The EAC alpha block of ETC2_RGBA8 (before the colour block): per table the multiplier its range needs
            /// (1..15) and the base centring that range on the block's, the table with the least error, then that table's
            /// multiplier one either side; each pixel's index by the midpoints between the sorted modifiers. 64 bits
            /// big-endian: base (8), multiplier (4), table (4), then 3-bit indices column by column (pixel (x, y) at bits
            /// 45 - 3 (x * 4 + y)).</summary>
            static void EacAlphaBlock(NativeArray<byte> p, NativeArray<byte> output, int o)
            {
                int min = 255, max = 0;
                for (int i = 3; i < 64; i += 4) { int a = p[i]; min = Math.Min(min, a); max = Math.Max(max, a); }
                ulong best;
                if (min == max)
                {
                    // One value: table 13's modifier 0 (index 4) everywhere.
                    best = ((ulong)min << 56) | (1UL << 52) | (13UL << 48);
                    for (int pos = 0; pos < 16; pos++) best |= 4UL << (45 - 3 * pos);
                }
                else
                {
                    best = 0;
                    long bestErr = long.MaxValue;
                    int bestTable = 0, bestMult = 1;
                    for (int t = 0; t < 16; t++)
                    {
                        int range = EacTables[t * 8 + 7] - EacTables[t * 8 + 3];
                        int mult = Math.Max(1, Math.Min(15, (max - min + range / 2) / range));
                        ulong bits = EacBlock(p, t, mult, min, max, out long e);
                        if (e < bestErr) { bestErr = e; best = bits; bestTable = t; bestMult = mult; }
                    }
                    for (int mult = Math.Max(1, bestMult - 1); mult <= Math.Min(15, bestMult + 1); mult += 2)
                    {
                        ulong bits = EacBlock(p, bestTable, mult, min, max, out long e);
                        if (e < bestErr) { bestErr = e; best = bits; }
                    }
                }
                for (int k = 0; k < 8; k++) output[o + k] = (byte)(best >> (56 - 8 * k));
            }

            /// <summary>The EAC block for one table and multiplier ('error' its squared error).</summary>
            static ulong EacBlock(NativeArray<byte> p, int t, int mult, int min, int max, out long error)
            {
                int row = t * 8;
                int baseA = Clamp255((min + max - (EacTables[row + 3] + EacTables[row + 7]) * mult + 1) >> 1);
                ulong bits = ((ulong)baseA << 56) | ((ulong)mult << 52) | ((ulong)t << 48);
                error = 0;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        int a = p[(y * 4 + x) * 4 + 3], x2 = 2 * (a - baseA), j = 0;
                        while (j < 7 && x2 > (EacTables[row + EacOrder[j]] + EacTables[row + EacOrder[j + 1]]) * mult) j++;
                        int k = EacOrder[j];
                        int d = Clamp255(baseA + EacTables[row + k] * mult) - a;
                        error += d * d;
                        bits |= (ulong)k << (45 - 3 * (x * 4 + y));
                    }
                return bits;
            }

            /// <summary>DXT5's alpha block: two end points (a0 > a1: eight levels), 3 bits per pixel.</summary>
            static void AlphaBlock(NativeArray<byte> p, NativeArray<byte> output, int o)
            {
                int min = 255, max = 0;
                for (int i = 3; i < 64; i += 4) { int a = p[i]; min = Math.Min(min, a); max = Math.Max(max, a); }
                output[o] = (byte)max;
                output[o + 1] = (byte)min;
                ulong bits = 0;
                if (max != min)
                    for (int i = 0; i < 16; i++)
                    {
                        int a = p[i * 4 + 3], best = 0, bd = int.MaxValue;
                        for (int k = 0; k < 8; k++)
                        {
                            int level = k == 0 ? max : k == 1 ? min : ((8 - k) * max + (k - 1) * min) / 7;
                            int d = Math.Abs(a - level);
                            if (d < bd) { bd = d; best = k; }
                        }
                        bits |= (ulong)best << (i * 3);
                    }
                for (int k = 0; k < 6; k++) output[o + 2 + k] = (byte)(bits >> (k * 8));
            }
        }
    }
}
