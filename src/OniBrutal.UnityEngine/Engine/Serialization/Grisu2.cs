using System;
using System.Numerics;

namespace OniBrutal.Engine.Serialization
{
    /// The digit generation of RapidJSON's Writer::Double (internal/dtoa.h, diyfp.h; MIT): Florian
    /// Loitsch's Grisu2, which Unity's JsonUtility uses for floats (widened to double) and
    /// doubles. Its digits round-trip but are not always the shortest, nor the closest, so they
    /// differ from .NET's "R" in the last place for some values (0.2f → 0.20000000298023225).
    internal static class Grisu2
    {
        /// The digits of |value| (> 0, finite) and the decimal exponent: value ≈ digits × 10^k.
        public static (string Digits, int K) Digits(double value)
        {
            var v = DiyFp.Of(value);
            v.NormalizedBoundaries(out var wMinus, out var wPlus);
            var cmk = CachedPower(wPlus.E, out int k);
            var w = v.Normalize() * cmk;
            var wp = wPlus * cmk;
            var wm = wMinus * cmk;
            wm.F++;
            wp.F--;
            Span<char> buffer = stackalloc char[32];
            int length = DigitGen(w, wp, wp.F - wm.F, buffer, ref k);
            return (new string(buffer[..length]), k);
        }

        static readonly ulong[] Pow10 =
        [
            1UL, 10UL, 100UL, 1000UL, 10000UL, 100000UL, 1000000UL, 10000000UL, 100000000UL,
            1000000000UL, 10000000000UL, 100000000000UL, 1000000000000UL, 10000000000000UL,
            100000000000000UL, 1000000000000000UL, 10000000000000000UL, 100000000000000000UL,
            1000000000000000000UL, 10000000000000000000UL,
        ];

        static int CountDecimalDigit32(uint n) => n switch
        {
            < 10 => 1,
            < 100 => 2,
            < 1000 => 3,
            < 10000 => 4,
            < 100000 => 5,
            < 1000000 => 6,
            < 10000000 => 7,
            < 100000000 => 8,
            _ => 9, // DigitGen never reaches 10 digits
        };

        static void Round(Span<char> buffer, int length, ulong delta, ulong rest, ulong tenKappa, ulong wpW)
        {
            while (rest < wpW && delta - rest >= tenKappa && (rest + tenKappa < wpW || wpW - rest > rest + tenKappa - wpW))
            {
                buffer[length - 1]--;
                rest += tenKappa;
            }
        }

        static int DigitGen(DiyFp w, DiyFp mp, ulong delta, Span<char> buffer, ref int k)
        {
            var one = new DiyFp(1UL << -mp.E, mp.E);
            var wpW = mp - w;
            uint p1 = (uint)(mp.F >> -one.E);
            ulong p2 = mp.F & (one.F - 1);
            int kappa = CountDecimalDigit32(p1);
            int length = 0;
            while (kappa > 0)
            {
                uint divisor = (uint)Pow10[kappa - 1];
                uint d = p1 / divisor;
                p1 %= divisor;
                if (d != 0 || length != 0) buffer[length++] = (char)('0' + d);
                kappa--;
                ulong tmp = ((ulong)p1 << -one.E) + p2;
                if (tmp <= delta)
                {
                    k += kappa;
                    Round(buffer, length, delta, tmp, Pow10[kappa] << -one.E, wpW.F);
                    return length;
                }
            }
            while (true)
            {
                p2 *= 10;
                delta *= 10;
                char d = (char)(p2 >> -one.E);
                if (d != 0 || length != 0) buffer[length++] = (char)('0' + d);
                p2 &= one.F - 1;
                kappa--;
                if (p2 < delta)
                {
                    k += kappa;
                    int index = -kappa;
                    Round(buffer, length, delta, p2, one.F, wpW.F * (index < 20 ? Pow10[index] : 0));
                    return length;
                }
            }
        }

        // ---- Cached powers 10^-348, 10^-340, …, 10^340 as normalized 64-bit significands
        // (rounded to nearest) with binary exponents; identical to RapidJSON's table. ----

        static readonly (ulong F, int E)[] CachedPowers = BuildCachedPowers();

        static (ulong, int)[] BuildCachedPowers()
        {
            var table = new (ulong, int)[87];
            for (int i = 0; i < table.Length; i++)
            {
                int k = -348 + 8 * i;
                BigInteger num = k >= 0 ? BigInteger.Pow(10, k) : BigInteger.One;
                BigInteger den = k >= 0 ? BigInteger.One : BigInteger.Pow(10, -k);
                // value = num / den; find e with 2^63 <= value / 2^e < 2^64.
                int e = (int)(num.GetBitLength() - den.GetBitLength()) - 64;
                while (true)
                {
                    var (qn, qd) = Scale(num, den, e);
                    if (qn < (BigInteger.One << 63) * qd) { e--; continue; }
                    if (qn >= (BigInteger.One << 64) * qd) { e++; continue; }
                    // Round to nearest (ties away from zero, as Python's round on these values).
                    var f = (2 * qn + qd) / (2 * qd);
                    if (f == BigInteger.One << 64) { f >>= 1; e++; }
                    table[i] = ((ulong)f, e);
                    break;
                }
            }
            return table;
        }

        static (BigInteger Num, BigInteger Den) Scale(BigInteger num, BigInteger den, int e)
            => e >= 0 ? (num, den << e) : (num << -e, den);

        static DiyFp CachedPower(int e, out int k)
        {
            double dk = (-61 - e) * 0.30102999566398114 + 347;
            int ik = (int)dk;
            if (dk - ik > 0.0) ik++;
            int index = (ik >> 3) + 1;
            k = -(-348 + (index << 3));
            var (f, be) = CachedPowers[index];
            return new DiyFp(f, be);
        }

        struct DiyFp(ulong f, int e)
        {
            public ulong F = f;
            public int E = e;

            const int SignificandSize = 52;
            const int ExponentBias = 0x3FF + SignificandSize;
            const int MinExponent = -ExponentBias;
            const ulong ExponentMask = 0x7FF0000000000000, SignificandMask = 0x000FFFFFFFFFFFFF, HiddenBit = 0x0010000000000000;

            public static DiyFp Of(double d)
            {
                ulong u = (ulong)BitConverter.DoubleToInt64Bits(d);
                int biased = (int)((u & ExponentMask) >> SignificandSize);
                ulong significand = u & SignificandMask;
                return biased != 0 ? new DiyFp(significand + HiddenBit, biased - ExponentBias) : new DiyFp(significand, MinExponent + 1);
            }

            public static DiyFp operator -(DiyFp a, DiyFp b) => new(a.F - b.F, a.E);

            /// 64×64 → upper 64 bits, rounded.
            public static DiyFp operator *(DiyFp x, DiyFp y)
            {
                const ulong M32 = 0xFFFFFFFF;
                ulong a = x.F >> 32, b = x.F & M32, c = y.F >> 32, d = y.F & M32;
                ulong ac = a * c, bc = b * c, ad = a * d, bd = b * d;
                ulong tmp = (bd >> 32) + (ad & M32) + (bc & M32);
                tmp += 1UL << 31;
                return new DiyFp(ac + (ad >> 32) + (bc >> 32) + (tmp >> 32), x.E + y.E + 64);
            }

            public readonly DiyFp Normalize()
            {
                int s = BitOperations.LeadingZeroCount(F);
                return new DiyFp(F << s, E - s);
            }

            readonly DiyFp NormalizeBoundary()
            {
                var res = this;
                while ((res.F & (HiddenBit << 1)) == 0)
                {
                    res.F <<= 1;
                    res.E--;
                }
                res.F <<= 64 - SignificandSize - 2;
                res.E -= 64 - SignificandSize - 2;
                return res;
            }

            public readonly void NormalizedBoundaries(out DiyFp minus, out DiyFp plus)
            {
                var pl = new DiyFp((F << 1) + 1, E - 1).NormalizeBoundary();
                var mi = F == HiddenBit ? new DiyFp((F << 2) - 1, E - 2) : new DiyFp((F << 1) - 1, E - 1);
                mi.F <<= mi.E - pl.E;
                mi.E = pl.E;
                plus = pl;
                minus = mi;
            }
        }
    }
}
