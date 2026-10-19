using System;
using System.Buffers.Binary;
using UnityEngine;

namespace OniBrutal.Engine.Rendering
{
    /// Texel reads from ASTC blocks (GetPixel on compressed readable textures, which Unity decodes
    /// on the CPU), written from the Khronos Data Format Specification's ASTC chapter.
    ///
    /// LDR formats use the LDR profile: channels are the top 8 bits of the 16-bit interpolation,
    /// as in decoding to an 8-bit format, and blocks it cannot decode (reserved encodings, HDR
    /// endpoint modes or HDR constant colours) read as the error colour, magenta. HDR formats use
    /// the HDR profile: channels are half floats, from logarithmic (LNS) values for HDR endpoints
    /// and from 16-bit unsigned normalized values for LDR ones; errors read as NaN.
    internal static class AstcDecoder
    {
        static readonly Color ErrorColor = new(1f, 0f, 1f, 1f);

        /// The 128-bit block as bits, lowest first.
        readonly ref struct Block(ReadOnlySpan<byte> data)
        {
            readonly ulong lo = BinaryPrimitives.ReadUInt64LittleEndian(data), hi = BinaryPrimitives.ReadUInt64LittleEndian(data[8..]);

            public int Bit(int i) => i < 0 || i > 127 ? 0 : (int)(((i < 64 ? lo >> i : hi >> (i - 64))) & 1);

            public int Bits(int start, int count)
            {
                int v = 0;
                for (int i = 0; i < count; i++) v |= Bit(start + i) << i;
                return v;
            }

            /// Bits read from the top of the block down (where the weights are stored).
            public int ReversedBits(int start, int count)
            {
                int v = 0;
                for (int i = 0; i < count; i++) v |= Bit(127 - (start + i)) << i;
                return v;
            }
        }

        static readonly Color HdrErrorColor = new(float.NaN, float.NaN, float.NaN, float.NaN);

        public static Color Decode(ReadOnlySpan<byte> data, int blockWidth, int blockHeight, int px, int py, bool hdr = false)
        {
            var color = DecodeTexel(data, blockWidth, blockHeight, px, py, hdr);
            return color ?? (hdr ? HdrErrorColor : ErrorColor);
        }

        static Color? DecodeTexel(ReadOnlySpan<byte> data, int blockWidth, int blockHeight, int px, int py, bool hdr)
        {
            var b = new Block(data);
            int mode = b.Bits(0, 11);

            // Void-extent block: one constant colour, half floats when flagged HDR.
            if ((mode & 0x1FF) == 0x1FC)
            {
                if (b.Bits(10, 2) != 3) return null; // reserved bits not set
                if ((mode & 0x200) != 0)
                {
                    if (!hdr) return null;
                    return new Color(HalfBits(b.Bits(64, 16)), HalfBits(b.Bits(80, 16)), HalfBits(b.Bits(96, 16)), HalfBits(b.Bits(112, 16)));
                }
                if (hdr) return new Color(Unorm16ToHalf(b.Bits(64, 16)), Unorm16ToHalf(b.Bits(80, 16)), Unorm16ToHalf(b.Bits(96, 16)), Unorm16ToHalf(b.Bits(112, 16)));
                return new Color((b.Bits(64, 16) >> 8) / 255f, (b.Bits(80, 16) >> 8) / 255f, (b.Bits(96, 16) >> 8) / 255f, (b.Bits(112, 16) >> 8) / 255f);
            }

            if (!BlockMode(mode, out int gridW, out int gridH, out int weightRange, out bool dualPlane)) return null;
            if (gridW > blockWidth || gridH > blockHeight) return null;
            int weightCount = gridW * gridH * (dualPlane ? 2 : 1);
            if (weightCount > 64) return null;
            int weightBits = IseBits(weightRange, weightCount);
            if (weightBits < 24 || weightBits > 96) return null;

            int partitions = b.Bits(11, 2) + 1;
            if (dualPlane && partitions == 4) return null;
            int belowWeights = 128 - weightBits;
            Span<int> cem = stackalloc int[4];
            int colorStart;
            int partitionIndex = 0;
            if (partitions == 1)
            {
                cem[0] = b.Bits(13, 4);
                colorStart = 17;
            }
            else
            {
                partitionIndex = b.Bits(13, 10);
                int encoded = b.Bits(23, 6);
                colorStart = 29;
                if ((encoded & 3) == 0)
                    for (int i = 0; i < partitions; i++) cem[i] = encoded >> 2;
                else
                {
                    int extraBits = 3 * partitions - 4;
                    belowWeights -= extraBits;
                    encoded |= b.Bits(belowWeights, extraBits) << 6;
                    int baseClass = (encoded & 3) - 1;
                    for (int i = 0; i < partitions; i++)
                        cem[i] = ((((encoded >> (2 + i)) & 1) + baseClass) << 2) | ((encoded >> (2 + partitions + 2 * i)) & 3);
                }
            }
            int planeTwoComponent = -1;
            if (dualPlane)
            {
                belowWeights -= 2;
                planeTwoComponent = b.Bits(belowWeights, 2);
            }

            int colorValues = 0;
            for (int i = 0; i < partitions; i++)
            {
                // The LDR profile decodes only LDR endpoint modes.
                if (!hdr && IsHdrMode(cem[i])) return null;
                colorValues += ((cem[i] >> 2) + 1) * 2;
            }
            if (colorValues > 18) return null;
            int colorBits = belowWeights - colorStart;
            int colorRange = -1;
            for (int r = Ranges.Length - 1; r >= 0; r--)
                if (IseBits(r, colorValues) <= colorBits) { colorRange = r; break; }
            if (colorRange < RangeIndexOf6) return null;

            // Endpoints of the texel's partition.
            int texelCount = blockWidth * blockHeight;
            int partition = partitions == 1 ? 0 : SelectPartition(partitionIndex, px, py, partitions, texelCount < 31);
            Span<int> values = stackalloc int[18];
            DecodeIse(b, colorStart, colorRange, colorValues, values, reversed: false);
            for (int i = 0; i < colorValues; i++) values[i] = UnquantizeColor(values[i], colorRange);
            int offset = 0;
            for (int i = 0; i < partition; i++) offset += ((cem[i] >> 2) + 1) * 2;
            // Endpoints as 16-bit values: LDR ones widened from 8 bits, HDR ones logarithmic.
            Span<int> e0 = stackalloc int[4], e1 = stackalloc int[4];
            bool rgbHdr = false, alphaHdr = false;
            if (IsHdrMode(cem[partition])) HdrEndpoints(cem[partition], values[offset..], e0, e1, out rgbHdr, out alphaHdr);
            else
            {
                Endpoints(cem[partition], values[offset..], e0, e1);
                for (int c = 0; c < 4; c++) { e0[c] *= 257; e1[c] *= 257; }
            }

            // The texel's weights, infilled from the weight grid.
            Span<int> weights = stackalloc int[64];
            DecodeIse(b, 0, weightRange, weightCount, weights, reversed: true);
            for (int i = 0; i < weightCount; i++) weights[i] = UnquantizeWeight(weights[i], weightRange);
            int plane0 = Infill(weights, gridW, gridH, blockWidth, blockHeight, px, py, dualPlane ? 2 : 1, 0);
            int plane1 = dualPlane ? Infill(weights, gridW, gridH, blockWidth, blockHeight, px, py, 2, 1) : plane0;

            Span<float> rgba = stackalloc float[4];
            for (int c = 0; c < 4; c++)
            {
                int w = c == planeTwoComponent ? plane1 : plane0;
                int v = (e0[c] * (64 - w) + e1[c] * w + 32) >> 6;
                if (!hdr) rgba[c] = (v >> 8) / 255f;
                else rgba[c] = (c == 3 ? alphaHdr : rgbHdr) ? LnsToHalf(v) : Unorm16ToHalf(v);
            }
            return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
        }

        static bool IsHdrMode(int cem) => cem is 2 or 3 or 7 or 11 or 14 or 15;

        static float HalfBits(int bits) => (float)BitConverter.UInt16BitsToHalf((ushort)bits);

        /// A 16-bit unsigned normalized value as a half float, rounded toward zero (as astcenc).
        static float Unorm16ToHalf(int v)
        {
            if (v == 0xFFFF) return 1f;
            float f = v / 65535f;
            float h = (float)(Half)f;
            if (h > f) h = (float)BitConverter.UInt16BitsToHalf((ushort)(BitConverter.HalfToUInt16Bits((Half)h) - 1));
            return h;
        }

        /// An interpolated logarithmic value as a half float: the top five bits are the exponent,
        /// the mantissa is mapped piecewise linearly; capped at the largest finite half.
        static float LnsToHalf(int v)
        {
            int mantissa = v & 0x7FF, exponent = v >> 11;
            int mt = mantissa < 512 ? mantissa * 3 : mantissa < 1536 ? mantissa * 4 - 512 : mantissa * 5 - 2048;
            return HalfBits(Math.Min((exponent << 10) | (mt >> 3), 0x7BFF));
        }

        static bool BlockMode(int mode, out int w, out int h, out int range, out bool dualPlane)
        {
            w = h = range = 0;
            dualPlane = false;
            int r, precision;
            if ((mode & 3) != 0)
            {
                r = ((mode & 3) << 1) | ((mode >> 4) & 1);
                precision = (mode >> 9) & 1;
                dualPlane = ((mode >> 10) & 1) != 0;
                int a = (mode >> 5) & 3, bb = (mode >> 7) & 3;
                switch ((mode >> 2) & 3)
                {
                    case 0: w = bb + 4; h = a + 2; break;
                    case 1: w = bb + 8; h = a + 2; break;
                    case 2: w = a + 2; h = bb + 8; break;
                    default:
                        if (((mode >> 8) & 1) == 0) { w = a + 2; h = ((mode >> 7) & 1) + 6; }
                        else { w = ((mode >> 7) & 1) + 2; h = a + 2; }
                        break;
                }
            }
            else
            {
                r = (((mode >> 2) & 3) << 1) | ((mode >> 4) & 1);
                if (r == 0) return false; // reserved
                precision = (mode >> 9) & 1;
                dualPlane = ((mode >> 10) & 1) != 0;
                int a = (mode >> 5) & 3;
                switch ((mode >> 7) & 3)
                {
                    case 0: w = 12; h = a + 2; break;
                    case 1: w = a + 2; h = 12; break;
                    case 2:
                        w = a + 6; h = ((mode >> 9) & 3) + 6;
                        precision = 0; dualPlane = false;
                        break;
                    default:
                        if (a == 0) { w = 6; h = 10; }
                        else if (a == 1) { w = 10; h = 6; }
                        else return false;
                        break;
                }
            }
            if (r < 2) return false;
            // Weight ranges 2, 3, 4, 5, 6, 8 (low precision) and 10, 12, 16, 20, 24, 32 (high).
            int levels = precision == 0 ? new[] { 2, 3, 4, 5, 6, 8 }[r - 2] : new[] { 10, 12, 16, 20, 24, 32 }[r - 2];
            range = Array.IndexOf(Ranges, levels);
            return true;
        }

        // ----- Integer sequence encoding -----

        /// The quantization ranges, and how each is encoded: trits, quints and plain bits.
        static readonly int[] Ranges = [2, 3, 4, 5, 6, 8, 10, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96, 128, 160, 192, 256];
        static readonly (int Trits, int Quints, int Bits)[] Encodings =
        [
            (0, 0, 1), (1, 0, 0), (0, 0, 2), (0, 1, 0), (1, 0, 1), (0, 0, 3), (0, 1, 1), (1, 0, 2), (0, 0, 4), (0, 1, 2), (1, 0, 3),
            (0, 0, 5), (0, 1, 3), (1, 0, 4), (0, 0, 6), (0, 1, 4), (1, 0, 5), (0, 0, 7), (0, 1, 5), (1, 0, 6), (0, 0, 8),
        ];
        const int RangeIndexOf6 = 4;

        static int IseBits(int range, int count)
        {
            var (trits, quints, bits) = Encodings[range];
            return count * bits + (trits != 0 ? (count * 8 + 4) / 5 : 0) + (quints != 0 ? (count * 7 + 2) / 3 : 0);
        }

        static void DecodeIse(in Block b, int start, int range, int count, Span<int> output, bool reversed)
        {
            var (trits, quints, bits) = Encodings[range];
            int end = start + IseBits(range, count);
            int pos = start;
            int Read(ref int p, int n, in Block blk, int limit, bool rev)
            {
                int v = 0;
                for (int i = 0; i < n; i++, p++)
                    if (p < limit) v |= (rev ? blk.ReversedBits(p, 1) : blk.Bit(p)) << i;
                return v;
            }
            if (trits != 0)
            {
                Span<int> m = stackalloc int[5];
                Span<int> tv = stackalloc int[5];
                for (int i = 0; i < count; i += 5)
                {
                    int t = 0;
                    m[0] = Read(ref pos, bits, b, end, reversed); t |= Read(ref pos, 2, b, end, reversed);
                    m[1] = Read(ref pos, bits, b, end, reversed); t |= Read(ref pos, 2, b, end, reversed) << 2;
                    m[2] = Read(ref pos, bits, b, end, reversed); t |= Read(ref pos, 1, b, end, reversed) << 4;
                    m[3] = Read(ref pos, bits, b, end, reversed); t |= Read(ref pos, 2, b, end, reversed) << 5;
                    m[4] = Read(ref pos, bits, b, end, reversed); t |= Read(ref pos, 1, b, end, reversed) << 7;
                    Trits(t, tv);
                    for (int k = 0; k < 5 && i + k < count; k++) output[i + k] = (tv[k] << bits) | m[k];
                }
            }
            else if (quints != 0)
            {
                Span<int> m = stackalloc int[3];
                Span<int> qv = stackalloc int[3];
                for (int i = 0; i < count; i += 3)
                {
                    int q = 0;
                    m[0] = Read(ref pos, bits, b, end, reversed); q |= Read(ref pos, 3, b, end, reversed);
                    m[1] = Read(ref pos, bits, b, end, reversed); q |= Read(ref pos, 2, b, end, reversed) << 3;
                    m[2] = Read(ref pos, bits, b, end, reversed); q |= Read(ref pos, 2, b, end, reversed) << 5;
                    Quints(q, qv);
                    for (int k = 0; k < 3 && i + k < count; k++) output[i + k] = (qv[k] << bits) | m[k];
                }
            }
            else
                for (int i = 0; i < count; i++) output[i] = Read(ref pos, bits, b, end, reversed);
        }

        static int B(int v, int bit) => (v >> bit) & 1;

        static void Trits(int t, Span<int> o)
        {
            int c;
            if (((t >> 2) & 7) == 7)
            {
                c = (((t >> 5) & 7) << 2) | (t & 3);
                o[4] = 2; o[3] = 2;
            }
            else
            {
                c = t & 31;
                if (((t >> 5) & 3) == 3) { o[4] = 2; o[3] = B(t, 7); }
                else { o[4] = B(t, 7); o[3] = (t >> 5) & 3; }
            }
            if ((c & 3) == 3) { o[2] = 2; o[1] = B(c, 4); o[0] = (B(c, 3) << 1) | (B(c, 2) & ~B(c, 3) & 1); }
            else if (((c >> 2) & 3) == 3) { o[2] = 2; o[1] = 2; o[0] = c & 3; }
            else { o[2] = B(c, 4); o[1] = (c >> 2) & 3; o[0] = (B(c, 1) << 1) | (B(c, 0) & ~B(c, 1) & 1); }
        }

        static void Quints(int q, Span<int> o)
        {
            if (((q >> 1) & 3) == 3 && ((q >> 5) & 3) == 0)
            {
                o[2] = (B(q, 0) << 2) | ((B(q, 4) & ~B(q, 0) & 1) << 1) | (B(q, 3) & ~B(q, 0) & 1);
                o[1] = 4; o[0] = 4;
                return;
            }
            int c;
            if (((q >> 1) & 3) == 3)
            {
                o[2] = 4;
                c = (((q >> 3) & 3) << 3) | ((~(q >> 5) & 3) << 1) | (q & 1);
            }
            else
            {
                o[2] = (q >> 5) & 3;
                c = q & 31;
            }
            if ((c & 7) == 5) { o[1] = 4; o[0] = (c >> 3) & 3; }
            else { o[1] = (c >> 3) & 3; o[0] = c & 7; }
        }

        // ----- Unquantization -----

        static int UnquantizeColor(int value, int range)
        {
            var (trits, quints, bits) = Encodings[range];
            if (trits == 0 && quints == 0) return Replicate(value, bits, 8);
            int d = value >> bits, m = value & ((1 << bits) - 1);
            int a = (m & 1) != 0 ? 0x1FF : 0;
            int bb, c;
            int b1 = B(m, 1);
            if (trits != 0)
                switch (bits)
                {
                    case 1: bb = 0; c = 204; break;
                    case 2: bb = (b1 << 8) | (b1 << 4) | (b1 << 2) | (b1 << 1); c = 93; break;
                    case 3: { int cb = (m >> 1) & 3; bb = (cb << 7) | (cb << 2) | cb; c = 44; break; }
                    case 4: { int dcb = (m >> 1) & 7; bb = (dcb << 6) | dcb; c = 22; break; }
                    case 5: { int edcb = (m >> 1) & 15; bb = (edcb << 5) | (edcb >> 2); c = 11; break; }
                    default: { int fedcb = (m >> 1) & 31; bb = (fedcb << 4) | (fedcb >> 4); c = 5; break; }
                }
            else
                switch (bits)
                {
                    case 1: bb = 0; c = 113; break;
                    case 2: bb = (b1 << 8) | (b1 << 3) | (b1 << 2); c = 54; break;
                    case 3: { int cb = (m >> 1) & 3; bb = (cb << 7) | (cb << 1) | (cb >> 1); c = 26; break; }
                    case 4: { int dcb = (m >> 1) & 7; bb = (dcb << 6) | (dcb >> 1); c = 13; break; }
                    default: { int edcb = (m >> 1) & 15; bb = (edcb << 5) | (edcb >> 3); c = 6; break; }
                }
            int t = d * c + bb;
            t ^= a;
            return (a & 0x80) | (t >> 2);
        }

        static int UnquantizeWeight(int value, int range)
        {
            var (trits, quints, bits) = Encodings[range];
            int w;
            if (trits == 0 && quints == 0) w = Replicate(value, bits, 6);
            // Without extra bits the levels are the final weights: 0, 32, 64 or 0, 16, 32, 48, 64.
            else if (bits == 0) return trits != 0 ? value * 32 : value * 16;
            else
            {
                int d = value >> bits, m = value & ((1 << bits) - 1);
                int a = (m & 1) != 0 ? 0x7F : 0;
                int bb, c;
                if (trits != 0)
                    switch (bits)
                    {
                        case 1: bb = 0; c = 50; break;
                        case 2: { int b1 = B(m, 1); bb = (b1 << 6) | (b1 << 2) | b1; c = 23; break; }
                        default: { int cb = (m >> 1) & 3; bb = (cb << 5) | cb; c = 11; break; }
                    }
                else
                    switch (bits)
                    {
                        case 1: bb = 0; c = 28; break;
                        default: { int b1 = B(m, 1); bb = (b1 << 6) | (b1 << 1); c = 13; break; }
                    }
                int t = d * c + bb;
                t ^= a;
                w = (a & 0x20) | (t >> 2);
            }
            return w > 32 ? w + 1 : w;
        }

        static int Replicate(int value, int bits, int to)
        {
            if (bits == 0) return 0;
            int result = 0, filled = 0;
            while (filled < to)
            {
                int shift = to - filled - bits;
                result |= shift >= 0 ? value << shift : value >> -shift;
                filled += bits;
            }
            return result & ((1 << to) - 1);
        }

        // ----- Endpoints -----

        static void BitTransferSigned(ref int a, ref int b)
        {
            b >>= 1;
            b |= a & 0x80;
            a >>= 1;
            a &= 0x3F;
            if ((a & 0x20) != 0) a -= 0x40;
        }

        static void Set(Span<int> e, int r, int g, int b, int a)
        {
            e[0] = Math.Clamp(r, 0, 255); e[1] = Math.Clamp(g, 0, 255); e[2] = Math.Clamp(b, 0, 255); e[3] = Math.Clamp(a, 0, 255);
        }

        static void BlueContract(Span<int> e, int r, int g, int b, int a) => Set(e, (r + b) >> 1, (g + b) >> 1, b, a);

        static void SetHdr(Span<int> e, int r, int g, int b, int a)
        {
            e[0] = Math.Clamp(r, 0, 0xFFF) << 4; e[1] = Math.Clamp(g, 0, 0xFFF) << 4; e[2] = Math.Clamp(b, 0, 0xFFF) << 4; e[3] = a;
        }

        /// HDR endpoint modes: 12-bit logarithmic values (as 16 bits); alpha 1.0 (0x7800 in LNS)
        /// unless the mode has its own.
        static void HdrEndpoints(int cem, Span<int> v, Span<int> e0, Span<int> e1, out bool rgbHdr, out bool alphaHdr)
        {
            rgbHdr = alphaHdr = true;
            switch (cem)
            {
                case 2:
                {
                    int y0, y1;
                    if (v[1] >= v[0]) { y0 = v[0] << 4; y1 = v[1] << 4; }
                    else { y0 = (v[1] << 4) + 8; y1 = (v[0] << 4) - 8; }
                    SetHdr(e0, y0, y0, y0, 0x7800); SetHdr(e1, y1, y1, y1, 0x7800);
                    break;
                }
                case 3:
                {
                    int y0, y1;
                    if ((v[0] & 0x80) != 0) { y0 = ((v[1] & 0xE0) << 4) | ((v[0] & 0x7F) << 2); y1 = (v[1] & 0x1F) << 2; }
                    else { y0 = ((v[1] & 0xF0) << 4) | ((v[0] & 0x7F) << 1); y1 = (v[1] & 0xF) << 1; }
                    y1 = Math.Min(y1 + y0, 0xFFF);
                    SetHdr(e0, y0, y0, y0, 0x7800); SetHdr(e1, y1, y1, y1, 0x7800);
                    break;
                }
                case 7: RgbScale(v, e0, e1); break;
                case 11: Rgb(v, e0, e1); break;
                case 14:
                    Rgb(v, e0, e1);
                    e0[3] = v[6] * 257; e1[3] = v[7] * 257;
                    alphaHdr = false;
                    break;
                case 15:
                {
                    Rgb(v, e0, e1);
                    int v6 = v[6], v7 = v[7];
                    int selector = ((v6 >> 7) & 1) | ((v7 >> 6) & 2);
                    v6 &= 0x7F; v7 &= 0x7F;
                    int a0, a1;
                    if (selector == 3) { a0 = v6 << 5; a1 = v7 << 5; }
                    else
                    {
                        v6 |= (v7 << (selector + 1)) & 0x780;
                        v7 &= 0x3F >> selector;
                        v7 ^= 32 >> selector;
                        v7 -= 32 >> selector;
                        v6 <<= 4 - selector;
                        v7 <<= 4 - selector;
                        v7 = Math.Clamp(v7 + v6, 0, 0xFFF);
                        a0 = v6; a1 = v7;
                    }
                    e0[3] = a0 << 4; e1[3] = a1 << 4;
                    break;
                }
            }
        }

        /// HDR RGB, base and scale (mode 7): bit fields placed by a mode selector, the major
        /// component swapped into red.
        static void RgbScale(Span<int> v, Span<int> e0, Span<int> e1)
        {
            int modeval = ((v[0] & 0xC0) >> 6) | (((v[1] & 0x80) >> 7) << 2) | (((v[2] & 0x80) >> 7) << 3);
            int major, mode;
            if ((modeval & 0xC) != 0xC) { major = modeval >> 2; mode = modeval & 3; }
            else if (modeval != 0xF) { major = modeval & 3; mode = 4; }
            else { major = 0; mode = 5; }
            int red = v[0] & 0x3F, green = v[1] & 0x1F, blue = v[2] & 0x1F, scale = v[3] & 0x1F;
            int bit0 = (v[1] >> 6) & 1, bit1 = (v[1] >> 5) & 1, bit2 = (v[2] >> 6) & 1, bit3 = (v[2] >> 5) & 1;
            int bit4 = (v[3] >> 7) & 1, bit5 = (v[3] >> 6) & 1, bit6 = (v[3] >> 5) & 1;
            int oh = 1 << mode;
            if ((oh & 0x30) != 0) green |= bit0 << 6;
            if ((oh & 0x3A) != 0) green |= bit1 << 5;
            if ((oh & 0x30) != 0) blue |= bit2 << 6;
            if ((oh & 0x3A) != 0) blue |= bit3 << 5;
            if ((oh & 0x3D) != 0) scale |= bit6 << 5;
            if ((oh & 0x2D) != 0) scale |= bit5 << 6;
            if ((oh & 0x04) != 0) scale |= bit4 << 7;
            if ((oh & 0x3B) != 0) red |= bit4 << 6;
            if ((oh & 0x04) != 0) red |= bit3 << 6;
            if ((oh & 0x10) != 0) red |= bit5 << 7;
            if ((oh & 0x0F) != 0) red |= bit2 << 7;
            if ((oh & 0x05) != 0) red |= bit1 << 8;
            if ((oh & 0x0A) != 0) red |= bit0 << 8;
            if ((oh & 0x05) != 0) red |= bit0 << 9;
            if ((oh & 0x02) != 0) red |= bit6 << 9;
            if ((oh & 0x01) != 0) red |= bit3 << 10;
            if ((oh & 0x02) != 0) red |= bit5 << 10;
            int shift = mode switch { 0 or 1 => 1, 2 => 2, 3 => 3, 4 => 4, _ => 5 };
            red <<= shift; green <<= shift; blue <<= shift; scale <<= shift;
            if (mode != 5) { green = red - green; blue = red - blue; }
            if (major == 1) (red, green) = (green, red);
            else if (major == 2) (red, blue) = (blue, red);
            SetHdr(e1, red, green, blue, 0x7800);
            SetHdr(e0, red - scale, green - scale, blue - scale, 0x7800);
        }

        /// HDR RGB direct (mode 11, and the colour of modes 14 and 15).
        static void Rgb(Span<int> v, Span<int> e0, Span<int> e1)
        {
            int modeval = ((v[1] & 0x80) >> 7) | (((v[2] & 0x80) >> 7) << 1) | (((v[3] & 0x80) >> 7) << 2);
            int major = ((v[4] & 0x80) >> 7) | (((v[5] & 0x80) >> 7) << 1);
            if (major == 3)
            {
                SetHdr(e0, v[0] << 4, v[2] << 4, (v[4] & 0x7F) << 5, 0x7800);
                SetHdr(e1, v[1] << 4, v[3] << 4, (v[5] & 0x7F) << 5, 0x7800);
                return;
            }
            int a = v[0] | ((v[1] & 0x40) << 2), b0 = v[2] & 0x3F, b1 = v[3] & 0x3F, c = v[1] & 0x3F, d0 = v[4] & 0x7F, d1 = v[5] & 0x7F;
            int dbits = modeval switch { 0 or 2 => 7, 1 or 3 or 5 or 7 => 6, _ => 5 };
            int bit0 = (v[2] >> 6) & 1, bit1 = (v[3] >> 6) & 1, bit2 = (v[4] >> 6) & 1, bit3 = (v[5] >> 6) & 1, bit4 = (v[4] >> 5) & 1, bit5 = (v[5] >> 5) & 1;
            int oh = 1 << modeval;
            if ((oh & 0xA4) != 0) a |= bit0 << 9;
            if ((oh & 0x8) != 0) a |= bit2 << 9;
            if ((oh & 0x50) != 0) a |= bit4 << 9;
            if ((oh & 0x50) != 0) a |= bit5 << 10;
            if ((oh & 0xA0) != 0) a |= bit1 << 10;
            if ((oh & 0xC0) != 0) a |= bit2 << 11;
            if ((oh & 0x4) != 0) c |= bit1 << 6;
            if ((oh & 0xE8) != 0) c |= bit3 << 6;
            if ((oh & 0x20) != 0) c |= bit2 << 7;
            if ((oh & 0x5B) != 0) { b0 |= bit0 << 6; b1 |= bit1 << 6; }
            if ((oh & 0x12) != 0) { b0 |= bit2 << 7; b1 |= bit3 << 7; }
            if ((oh & 0xAF) != 0) { d0 |= bit4 << 5; d1 |= bit5 << 5; }
            if ((oh & 0x5) != 0) { d0 |= bit2 << 6; d1 |= bit3 << 6; }
            d0 = SignExtend(d0, dbits);
            d1 = SignExtend(d1, dbits);
            int shift = (modeval >> 1) ^ 3;
            a <<= shift; b0 <<= shift; b1 <<= shift; c <<= shift; d0 <<= shift; d1 <<= shift;
            int red1 = a, green1 = a - b0, blue1 = a - b1, red0 = a - c, green0 = a - b0 - c - d0, blue0 = a - b1 - c - d1;
            if (major == 1) { (red0, green0) = (green0, red0); (red1, green1) = (green1, red1); }
            else if (major == 2) { (red0, blue0) = (blue0, red0); (red1, blue1) = (blue1, red1); }
            SetHdr(e0, red0, green0, blue0, 0x7800);
            SetHdr(e1, red1, green1, blue1, 0x7800);
        }

        static int SignExtend(int value, int bits) => (value << (32 - bits)) >> (32 - bits);

        static void Endpoints(int cem, Span<int> v, Span<int> e0, Span<int> e1)
        {
            switch (cem)
            {
                case 0: Set(e0, v[0], v[0], v[0], 255); Set(e1, v[1], v[1], v[1], 255); break;
                case 1:
                {
                    int l0 = (v[0] >> 2) | (v[1] & 0xC0);
                    int l1 = Math.Min(l0 + (v[1] & 0x3F), 255);
                    Set(e0, l0, l0, l0, 255); Set(e1, l1, l1, l1, 255);
                    break;
                }
                case 4: Set(e0, v[0], v[0], v[0], v[2]); Set(e1, v[1], v[1], v[1], v[3]); break;
                case 5:
                {
                    int v0 = v[0], v1 = v[1], v2 = v[2], v3 = v[3];
                    BitTransferSigned(ref v1, ref v0);
                    BitTransferSigned(ref v3, ref v2);
                    Set(e0, v0, v0, v0, v2); Set(e1, v0 + v1, v0 + v1, v0 + v1, v2 + v3);
                    break;
                }
                case 6:
                    Set(e0, (v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, 255);
                    Set(e1, v[0], v[1], v[2], 255);
                    break;
                case 8:
                case 12:
                {
                    int a0 = cem == 12 ? v[6] : 255, a1 = cem == 12 ? v[7] : 255;
                    if (v[1] + v[3] + v[5] >= v[0] + v[2] + v[4]) { Set(e0, v[0], v[2], v[4], a0); Set(e1, v[1], v[3], v[5], a1); }
                    else { BlueContract(e0, v[1], v[3], v[5], a1); BlueContract(e1, v[0], v[2], v[4], a0); }
                    break;
                }
                case 9:
                case 13:
                {
                    int v0 = v[0], v1 = v[1], v2 = v[2], v3 = v[3], v4 = v[4], v5 = v[5];
                    int v6 = cem == 13 ? v[6] : 255, v7 = cem == 13 ? v[7] : 0;
                    BitTransferSigned(ref v1, ref v0);
                    BitTransferSigned(ref v3, ref v2);
                    BitTransferSigned(ref v5, ref v4);
                    if (cem == 13) BitTransferSigned(ref v7, ref v6);
                    if (v1 + v3 + v5 >= 0) { Set(e0, v0, v2, v4, v6); Set(e1, v0 + v1, v2 + v3, v4 + v5, v6 + v7); }
                    else { BlueContract(e0, v0 + v1, v2 + v3, v4 + v5, v6 + v7); BlueContract(e1, v0, v2, v4, v6); }
                    break;
                }
                case 10:
                    Set(e0, (v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, v[4]);
                    Set(e1, v[0], v[1], v[2], v[5]);
                    break;
            }
        }

        // ----- Partitions and weight infill -----

        static uint Hash52(uint p)
        {
            p ^= p >> 15; p -= p << 17; p += p << 7; p += p << 4;
            p ^= p >> 5; p += p << 16; p ^= p >> 7; p ^= p >> 3;
            p ^= p << 6; p ^= p >> 17;
            return p;
        }

        static int SelectPartition(int seed, int x, int y, int partitionCount, bool smallBlock)
        {
            if (smallBlock) { x <<= 1; y <<= 1; }
            seed += (partitionCount - 1) * 1024;
            uint rnum = Hash52((uint)seed);
            int s1 = (int)(rnum & 0xF), s2 = (int)((rnum >> 4) & 0xF), s3 = (int)((rnum >> 8) & 0xF), s4 = (int)((rnum >> 12) & 0xF);
            int s5 = (int)((rnum >> 16) & 0xF), s6 = (int)((rnum >> 20) & 0xF), s7 = (int)((rnum >> 24) & 0xF), s8 = (int)((rnum >> 28) & 0xF);
            s1 *= s1; s2 *= s2; s3 *= s3; s4 *= s4; s5 *= s5; s6 *= s6; s7 *= s7; s8 *= s8;
            int sh1, sh2;
            if ((seed & 1) != 0) { sh1 = (seed & 2) != 0 ? 4 : 5; sh2 = partitionCount == 3 ? 6 : 5; }
            else { sh1 = partitionCount == 3 ? 6 : 5; sh2 = (seed & 2) != 0 ? 4 : 5; }
            s1 >>= sh1; s2 >>= sh2; s3 >>= sh1; s4 >>= sh2; s5 >>= sh1; s6 >>= sh2; s7 >>= sh1; s8 >>= sh2;
            // z is 0 in 2D blocks, so the z seeds (9 to 12) drop out of a to d.
            int a = (int)((s1 * x + s2 * y + (rnum >> 14)) & 0x3F);
            int b = (int)((s3 * x + s4 * y + (rnum >> 10)) & 0x3F);
            int c = (int)((s5 * x + s6 * y + (rnum >> 6)) & 0x3F);
            int d = (int)((s7 * x + s8 * y + (rnum >> 2)) & 0x3F);
            if (partitionCount < 4) d = 0;
            if (partitionCount < 3) c = 0;
            if (a >= b && a >= c && a >= d) return 0;
            if (b >= c && b >= d) return 1;
            if (c >= d) return 2;
            return 3;
        }

        static int Infill(Span<int> weights, int gridW, int gridH, int blockW, int blockH, int s, int t, int planes, int plane)
        {
            int ds = (1024 + blockW / 2) / (blockW - 1);
            int dt = (1024 + blockH / 2) / (blockH - 1);
            int gs = (ds * s * (gridW - 1) + 32) >> 6;
            int gt = (dt * t * (gridH - 1) + 32) >> 6;
            int js = gs >> 4, fs = gs & 15, jt = gt >> 4, ft = gt & 15;
            int w11 = (fs * ft + 8) >> 4, w10 = ft - w11, w01 = fs - w11, w00 = 16 - fs - ft + w11;
            return (GridWeight(weights, gridW, gridH, js, jt, planes, plane) * w00 + GridWeight(weights, gridW, gridH, js + 1, jt, planes, plane) * w01
                    + GridWeight(weights, gridW, gridH, js, jt + 1, planes, plane) * w10 + GridWeight(weights, gridW, gridH, js + 1, jt + 1, planes, plane) * w11 + 8) >> 4;
        }

        /// A grid weight; points past the grid's edge only ever get a zero factor.
        static int GridWeight(Span<int> weights, int gridW, int gridH, int gx, int gy, int planes, int plane)
            => gx < gridW && gy < gridH ? weights[(gy * gridW + gx) * planes + plane] : 0;
    }
}
