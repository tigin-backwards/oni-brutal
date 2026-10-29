using System;
using System.IO;
using System.IO.Compression;

namespace OniMoono.Engine
{
    /// The reciprocal of a scale component as the original player's worldToLocalMatrix and
    /// InverseTransform* use it. It is not 1/s rounded: within 1.4 ulp of 1/s, the same as it in
    /// 65% of values, a function of the mantissa alone (the same for every exponent and both
    /// signs; tools/UnityProbe `invscale`, every mantissa of [1, 2) and 26,414 drawn scales of
    /// exponents -8 to 8). No formula found reproduces it (docs/cleanroom/ugui.md, "Transform
    /// hierarchy to the bit"), so it is 1/s corrected by the measured difference in units in the
    /// last place: InverseScale.bin, the 2^23 mantissas' differences (-1, 0 or +1) plus one, two
    /// bits each, four to a byte with the first in the high bits, raw deflate.
    internal static class InverseScale
    {
        static class Table
        {
            public static readonly byte[] Bits = Load();

            static byte[] Load()
            {
                using var stream = typeof(InverseScale).Assembly.GetManifestResourceStream("OniMoono.Engine.InverseScale.bin")
                    ?? throw new InvalidOperationException("InverseScale.bin is not embedded");
                using var inflate = new DeflateStream(stream, CompressionMode.Decompress);
                var table = new byte[1 << 21];
                inflate.ReadExactly(table);
                return table;
            }
        }

        /// Measured edges (`invscale` IS6): below 1e-9 in magnitude (zero, subnormals and -0
        /// included) the inverse is +0; from 2^126 up (infinity included) it is 0 with the scale's
        /// sign, though 1/2^126 is a normal float; NaN stays NaN.
        public static float Of(float s)
        {
            int bits = BitConverter.SingleToInt32Bits(s);
            int magnitude = bits & 0x7fffffff;
            if (magnitude >= 0x7e800000)
                return magnitude > 0x7f800000 ? s : (bits < 0 ? -0f : 0f);
            if (magnitude < 0x3089705f) return 0f;
            int rb = BitConverter.SingleToInt32Bits(1f / s);
            int k = bits & 0x7fffff;
            int d = (Table.Bits[k >> 2] >> (6 - 2 * (k & 3))) & 3;
            return BitConverter.Int32BitsToSingle(rb + d - 1);
        }
    }
}
