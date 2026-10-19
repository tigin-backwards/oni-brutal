using System;
using System.Buffers.Binary;
using UnityEngine;

namespace OniBrutal.Engine.Rendering
{
    /// Texel reads from ASTC blocks, LDR profile (GetPixel on compressed readable textures,
    /// which Unity decodes on the CPU to 8-bit channels), written from the Khronos Data Format
    /// Specification's ASTC chapter. Channels are the top 8 bits of the 16-bit interpolation, as
    /// in decoding to an 8-bit format. Blocks the LDR profile cannot decode (reserved encodings,
    /// HDR endpoint modes or HDR constant colours) read as the error colour, magenta.
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

        public static Color Decode(ReadOnlySpan<byte> data, int blockWidth, int blockHeight, int px, int py)
        {
            var b = new Block(data);
            int mode = b.Bits(0, 11);

            // Void-extent block: one constant colour.
            if ((mode & 0x1FF) == 0x1FC)
            {
                if ((mode & 0x200) != 0 || b.Bits(10, 2) != 3) return ErrorColor; // HDR, or reserved bits not set
                return new Color((b.Bits(64, 16) >> 8) / 255f, (b.Bits(80, 16) >> 8) / 255f, (b.Bits(96, 16) >> 8) / 255f, (b.Bits(112, 16) >> 8) / 255f);
            }

            if (!BlockMode(mode, out int gridW, out int gridH, out int weightRange, out bool dualPlane)) return ErrorColor;
            if (gridW > blockWidth || gridH > blockHeight) return ErrorColor;
            int weightCount = gridW * gridH * (dualPlane ? 2 : 1);
            if (weightCount > 64) return ErrorColor;
            int weightBits = IseBits(weightRange, weightCount);
            if (weightBits < 24 || weightBits > 96) return ErrorColor;

            int partitions = b.Bits(11, 2) + 1;
            if (dualPlane && partitions == 4) return ErrorColor;
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
                if (cem[i] is 2 or 3 or 7 or 11 or 14 or 15) return ErrorColor;
                colorValues += ((cem[i] >> 2) + 1) * 2;
            }
            if (colorValues > 18) return ErrorColor;
            int colorBits = belowWeights - colorStart;
            int colorRange = -1;
            for (int r = Ranges.Length - 1; r >= 0; r--)
                if (IseBits(r, colorValues) <= colorBits) { colorRange = r; break; }
            if (colorRange < RangeIndexOf6) return ErrorColor;

            // Endpoints of the texel's partition.
            int texelCount = blockWidth * blockHeight;
            int partition = partitions == 1 ? 0 : SelectPartition(partitionIndex, px, py, partitions, texelCount < 31);
            Span<int> values = stackalloc int[18];
            DecodeIse(b, colorStart, colorRange, colorValues, values, reversed: false);
            for (int i = 0; i < colorValues; i++) values[i] = UnquantizeColor(values[i], colorRange);
            int offset = 0;
            for (int i = 0; i < partition; i++) offset += ((cem[i] >> 2) + 1) * 2;
            Span<int> e0 = stackalloc int[4], e1 = stackalloc int[4];
            Endpoints(cem[partition], values[offset..], e0, e1);

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
                int c0 = e0[c] * 257, c1 = e1[c] * 257;
                rgba[c] = (((c0 * (64 - w) + c1 * w + 32) >> 6) >> 8) / 255f;
            }
            return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
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
