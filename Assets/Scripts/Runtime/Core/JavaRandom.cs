// JavaRandom.cs
// The original's random generator (AbyssEngine::AERandom 0x7acde..0x7ae6a) is java.util.Random: a 48-bit LCG
// (multiplier 0x5DEECE66D, addend 0xB). Level layout code seeds it with the station index, so porting it
// exactly reproduces the original's stable layouts (jumpgates, sky rotation, sun/planets, asteroid field).
// Check: new JavaRandom(0).NextInt(100) == 60 (Java).

namespace GoF2Remake.Data
{
    public class JavaRandom
    {
        const long Multiplier = 0x5DEECE66DL;
        const long Addend = 0xBL;
        const long Mask = (1L << 48) - 1;

        long seed;

        public JavaRandom(long seed) => SetSeed(seed);

        /// <summary>AERandom::setSeed 0x7ad04.</summary>
        public void SetSeed(long s) => seed = (s ^ Multiplier) & Mask;

        int Next(int bits)
        {
            seed = (seed * Multiplier + Addend) & Mask;
            return (int)((ulong)seed >> (48 - bits));
        }

        /// <summary>AERandom::nextInt(int) 0x7add2: uniform 0..n-1 (Java algorithm, including its rejection loop).</summary>
        public int NextInt(int n)
        {
            if (n <= 0) return 0;
            if ((n & -n) == n) return (int)((n * (long)Next(31)) >> 31);
            int bits, val;
            do
            {
                bits = Next(31);
                val = bits % n;
            } while (bits - val + (n - 1) < 0);
            return val;
        }
    }
}
