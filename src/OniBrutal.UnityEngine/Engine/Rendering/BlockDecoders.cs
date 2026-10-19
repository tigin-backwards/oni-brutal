using System;
using System.Buffers.Binary;
using UnityEngine;

namespace OniBrutal.Engine.Rendering
{
    /// Texel reads from BC6H, BC7, ETC1, ETC2 and EAC blocks (GetPixel on compressed readable
    /// textures, which Unity decodes on the CPU), written from the formats' specifications (the
    /// Direct3D 11 functional specification for BC6H and BC7; the Khronos Data Format
    /// Specification for ETC and EAC). Each call decodes the texel's 4×4 block.
    internal static class BlockDecoders
    {
        // ----- Shared by BC6H and BC7 -----

        /// The 128-bit block, read from its least significant bit up.
        ref struct Bits(ReadOnlySpan<byte> block)
        {
            readonly ulong lo = BinaryPrimitives.ReadUInt64LittleEndian(block), hi = BinaryPrimitives.ReadUInt64LittleEndian(block[8..]);
            public int Position;

            public int Read(int count)
            {
                int value = 0;
                for (int i = 0; i < count; i++, Position++)
                {
                    ulong word = Position < 64 ? lo >> Position : hi >> (Position - 64);
                    value |= (int)(word & 1) << i;
                }
                return value;
            }

            public int Bit() => Read(1);
        }

        static readonly int[] Weights2 = [0, 21, 43, 64];
        static readonly int[] Weights3 = [0, 9, 18, 27, 37, 46, 55, 64];
        static readonly int[] Weights4 = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

        static int[] WeightsFor(int bits) => bits switch { 2 => Weights2, 3 => Weights3, _ => Weights4 };

        static int Interpolate(int e0, int e1, int weight) => ((64 - weight) * e0 + weight * e1 + 32) >> 6;

        /// Two-subset partitions: bit i set when texel i (row-major) belongs to subset 1.
        static readonly ushort[] Partitions2 =
        [
            0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80, 0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000,
            0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE, 0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C,
            0xAAAA, 0xF0F0, 0x5A5A, 0x33CC, 0x3C3C, 0x55AA, 0x9696, 0xA55A, 0x73CE, 0x13C8, 0x324C, 0x3BDC, 0x6996, 0xC33C, 0x9966, 0x0660,
            0x0272, 0x04E4, 0x4E40, 0x2720, 0xC936, 0x936C, 0x39C6, 0x639C, 0x9336, 0x9CC6, 0x817E, 0xE718, 0xCCF0, 0x0FCC, 0x7744, 0xEE22,
        ];

        /// Three-subset partitions: texel i's subset, row-major.
        static readonly string[] Partitions3 =
        [
            "0011001102212222", "0001001122112221", "0000200122112211", "0222002200110111", "0000000011221122", "0011001100220022", "0022002211111111", "0011001122112211",
            "0000000011112222", "0000111111112222", "0000111122222222", "0012001200120012", "0112011201120112", "0122012201220122", "0011011211221222", "0011200122002220",
            "0001001101121122", "0111001120012200", "0000112211221122", "0022002200221111", "0111011102220222", "0001000122212221", "0000001101220122", "0000110022102210",
            "0122012200110000", "0012001211222222", "0110122112210110", "0000011012211221", "0022110211020022", "0110011020022222", "0011012201220011", "0000200022112221",
            "0000000211221222", "0222002200120011", "0011001200220222", "0120012001200120", "0000111122220000", "0120120120120120", "0120201212010120", "0011220011220011",
            "0011112222000011", "0101010122222222", "0000000021212121", "0022112200221122", "0022001100220011", "0220122102201221", "0101222222220101", "0000212121212121",
            "0101010101012222", "0222011102220111", "0002111200021112", "0000211221122112", "0222011101110222", "0002111211120002", "0110011001102222", "0000000021122112",
            "0110011022222222", "0022001100110022", "0022112211220022", "0000000000002112", "0002000100020001", "0222122202221222", "0101222222222222", "0111201122012220",
        ];

        /// The anchor texel of subset 1 in two-subset partitions, and of subsets 1 and 2 in
        /// three-subset partitions (subset 0's is texel 0).
        static readonly byte[] Anchor2 =
        [
            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2,
            15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6, 6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15,
        ];

        static readonly byte[] Anchor3First =
        [
            3, 3, 15, 15, 8, 3, 15, 15, 8, 8, 6, 6, 6, 5, 3, 3, 3, 3, 8, 15, 3, 3, 6, 10, 5, 8, 8, 6, 8, 5, 15, 15,
            8, 15, 3, 5, 6, 10, 8, 15, 15, 3, 15, 5, 15, 15, 15, 15, 3, 15, 5, 5, 5, 8, 5, 10, 5, 10, 8, 13, 15, 12, 3, 3,
        ];

        static readonly byte[] Anchor3Second =
        [
            15, 8, 8, 3, 15, 15, 3, 8, 15, 15, 15, 15, 15, 15, 15, 8, 15, 8, 15, 3, 15, 8, 15, 8, 3, 15, 6, 10, 15, 15, 10, 8,
            15, 3, 15, 10, 10, 8, 9, 10, 6, 15, 8, 15, 3, 6, 6, 8, 15, 3, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 3, 15, 15, 8,
        ];

        static int SubsetOf(int subsets, int partition, int texel) => subsets switch
        {
            1 => 0,
            2 => (Partitions2[partition] >> texel) & 1,
            _ => Partitions3[partition][texel] - '0',
        };

        static bool IsAnchor(int subsets, int partition, int texel) => texel == 0 || subsets switch
        {
            2 => texel == Anchor2[partition],
            3 => texel == Anchor3First[partition] || texel == Anchor3Second[partition],
            _ => false,
        };

        // ----- BC7 -----

        /// Per mode: subsets, partition bits, rotation bits, index-selection bits, colour bits,
        /// alpha bits, per-endpoint p-bits, shared p-bits, index bits, secondary index bits.
        static readonly (int Subsets, int PartitionBits, int RotationBits, int SelectionBits, int ColorBits, int AlphaBits,
            bool EndpointPBits, bool SharedPBits, int IndexBits, int SecondaryIndexBits)[] Bc7Modes =
        [
            (3, 4, 0, 0, 4, 0, true, false, 3, 0),
            (2, 6, 0, 0, 6, 0, false, true, 3, 0),
            (3, 6, 0, 0, 5, 0, false, false, 2, 0),
            (2, 6, 0, 0, 7, 0, true, false, 2, 0),
            (1, 0, 2, 1, 5, 6, false, false, 2, 3),
            (1, 0, 2, 0, 7, 8, false, false, 2, 2),
            (1, 0, 0, 0, 7, 7, true, false, 4, 0),
            (2, 6, 0, 0, 5, 5, true, false, 2, 0),
        ];

        public static Color Bc7(ReadOnlySpan<byte> block, int px, int py)
        {
            var bits = new Bits(block);
            int mode = 0;
            while (mode < 8 && bits.Bit() == 0) mode++;
            // A block without a mode bit is reserved: it decodes to transparent black.
            if (mode == 8) return new Color(0f, 0f, 0f, 0f);
            var m = Bc7Modes[mode];
            int partition = bits.Read(m.PartitionBits);
            int rotation = bits.Read(m.RotationBits);
            int selection = bits.Read(m.SelectionBits);
            int endpoints = m.Subsets * 2;
            Span<int> e = stackalloc int[6 * 4];
            for (int c = 0; c < 3; c++)
                for (int i = 0; i < endpoints; i++)
                    e[i * 4 + c] = bits.Read(m.ColorBits);
            if (m.AlphaBits > 0)
                for (int i = 0; i < endpoints; i++)
                    e[i * 4 + 3] = bits.Read(m.AlphaBits);
            int colorPrecision = m.ColorBits, alphaPrecision = m.AlphaBits;
            if (m.EndpointPBits || m.SharedPBits)
            {
                Span<int> p = stackalloc int[6];
                if (m.EndpointPBits)
                    for (int i = 0; i < endpoints; i++) p[i] = bits.Bit();
                else
                    for (int s = 0; s < m.Subsets; s++) p[2 * s] = p[2 * s + 1] = bits.Bit();
                for (int i = 0; i < endpoints; i++)
                {
                    for (int c = 0; c < 3; c++) e[i * 4 + c] = (e[i * 4 + c] << 1) | p[i];
                    if (m.AlphaBits > 0) e[i * 4 + 3] = (e[i * 4 + 3] << 1) | p[i];
                }
                colorPrecision++;
                if (m.AlphaBits > 0) alphaPrecision++;
            }
            for (int i = 0; i < endpoints; i++)
            {
                for (int c = 0; c < 3; c++) e[i * 4 + c] = Expand(e[i * 4 + c], colorPrecision);
                e[i * 4 + 3] = m.AlphaBits > 0 ? Expand(e[i * 4 + 3], alphaPrecision) : 255;
            }

            int texel = py * 4 + px;
            int index = 0, secondary = 0;
            for (int i = 0; i < 16; i++)
            {
                int value = bits.Read(m.IndexBits - (IsAnchor(m.Subsets, partition, i) ? 1 : 0));
                if (i == texel) index = value;
            }
            if (m.SecondaryIndexBits > 0)
                for (int i = 0; i < 16; i++)
                {
                    int value = bits.Read(m.SecondaryIndexBits - (i == 0 ? 1 : 0));
                    if (i == texel) secondary = value;
                }

            int subset = SubsetOf(m.Subsets, partition, texel);
            int e0 = subset * 2 * 4, e1 = e0 + 4;
            int colorWeight, alphaWeight;
            if (m.SecondaryIndexBits == 0) colorWeight = alphaWeight = WeightsFor(m.IndexBits)[index];
            else if (selection == 0)
            {
                colorWeight = WeightsFor(m.IndexBits)[index];
                alphaWeight = WeightsFor(m.SecondaryIndexBits)[secondary];
            }
            else
            {
                colorWeight = WeightsFor(m.SecondaryIndexBits)[secondary];
                alphaWeight = WeightsFor(m.IndexBits)[index];
            }
            int r = Interpolate(e[e0], e[e1], colorWeight);
            int g = Interpolate(e[e0 + 1], e[e1 + 1], colorWeight);
            int b = Interpolate(e[e0 + 2], e[e1 + 2], colorWeight);
            int a = Interpolate(e[e0 + 3], e[e1 + 3], alphaWeight);
            switch (rotation)
            {
                case 1: (a, r) = (r, a); break;
                case 2: (a, g) = (g, a); break;
                case 3: (a, b) = (b, a); break;
            }
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        /// A value of the given precision widened to 8 bits by repeating its high bits.
        static int Expand(int value, int precision)
        {
            value <<= 8 - precision;
            return value | (value >> precision);
        }

        // ----- BC6H (unsigned) -----

        /// Endpoint fields of a BC6H header: w, x (subset 0) and y, z (subset 1), per channel.
        enum F : byte { RW, GW, BW, RX, GX, BX, RY, GY, BY, RZ, GZ, BZ, D }

        /// A header bit: which field and which of its bits.
        readonly record struct HeaderBit(F Field, int Bit);

        sealed record Bc6Mode(int Subsets, bool Transformed, int EndpointBits, int DeltaR, int DeltaG, int DeltaB, HeaderBit[] Header);

        /// The modes by their mode bits (2 bits for the first two modes, 5 for the others); null:
        /// reserved. Header layouts list the bits after the mode bits, lowest first.
        static readonly Bc6Mode[] Bc6Modes = BuildBc6Modes();

        /// A header layout in the specification's notation: fields with bit ranges, in storage
        /// order ("rw9:0 gy4 ... d4:0"). A range written high:low stores its low bit first; one
        /// written low:high (reversed, as rw10:11) stores its high bit first.
        static HeaderBit[] Layout(string spec)
        {
            var list = new System.Collections.Generic.List<HeaderBit>();
            foreach (var part in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int digits = part.IndexOfAny("0123456789".ToCharArray());
                var field = Enum.Parse<F>(part[..digits], ignoreCase: true);
                var range = part[digits..];
                int colon = range.IndexOf(':');
                int first = colon < 0 ? int.Parse(range) : int.Parse(range[..colon]);
                int last = colon < 0 ? first : int.Parse(range[(colon + 1)..]);
                if (first >= last) for (int b = last; b <= first; b++) list.Add(new HeaderBit(field, b));
                else for (int b = last; b >= first; b--) list.Add(new HeaderBit(field, b));
            }
            return list.ToArray();
        }

        static Bc6Mode[] BuildBc6Modes()
        {
            var modes = new Bc6Mode[32];
            // Two-subset modes end with the partition number.
            void Two(int code, bool transformed, int w, int dr, int dg, int db, string layout)
                => modes[code] = new Bc6Mode(2, transformed, w, dr, dg, db, Layout(layout + " d4:0"));
            Two(0x00, true, 10, 5, 5, 5, "gy4 by4 bz4 rw9:0 gw9:0 bw9:0 rx4:0 gz4 gy3:0 gx4:0 bz0 gz3:0 bx4:0 bz1 by3:0 ry4:0 bz2 rz4:0 bz3");
            Two(0x01, true, 7, 6, 6, 6, "gy5 gz4 gz5 rw6:0 bz0 bz1 by4 gw6:0 by5 bz2 gy4 bw6:0 bz3 bz5 bz4 rx5:0 gy3:0 gx5:0 gz3:0 bx5:0 by3:0 ry5:0 rz5:0");
            Two(0x02, true, 11, 5, 4, 4, "rw9:0 gw9:0 bw9:0 rx4:0 rw10 gy3:0 gx3:0 gw10 bz0 gz3:0 bx3:0 bw10 bz1 by3:0 ry4:0 bz2 rz4:0 bz3");
            Two(0x06, true, 11, 4, 5, 4, "rw9:0 gw9:0 bw9:0 rx3:0 rw10 gz4 gy3:0 gx4:0 gw10 gz3:0 bx3:0 bw10 bz1 by3:0 ry3:0 bz0 bz2 rz3:0 gy4 bz3");
            Two(0x0A, true, 11, 4, 4, 5, "rw9:0 gw9:0 bw9:0 rx3:0 rw10 by4 gy3:0 gx3:0 gw10 bz0 gz3:0 bx4:0 bw10 by3:0 ry3:0 bz1 bz2 rz3:0 bz4 bz3");
            Two(0x0E, true, 9, 5, 5, 5, "rw8:0 by4 gw8:0 gy4 bw8:0 bz4 rx4:0 gz4 gy3:0 gx4:0 bz0 gz3:0 bx4:0 bz1 by3:0 ry4:0 bz2 rz4:0 bz3");
            Two(0x12, true, 8, 6, 5, 5, "rw7:0 gz4 by4 gw7:0 bz2 gy4 bw7:0 bz3 bz4 rx5:0 gy3:0 gx4:0 bz0 gz3:0 bx4:0 bz1 by3:0 ry5:0 rz5:0");
            Two(0x16, true, 8, 5, 6, 5, "rw7:0 bz0 by4 gw7:0 gy5 gy4 bw7:0 gz5 bz4 rx4:0 gz4 gy3:0 gx5:0 gz3:0 bx4:0 bz1 by3:0 ry4:0 bz2 rz4:0 bz3");
            Two(0x1A, true, 8, 5, 5, 6, "rw7:0 bz1 by4 gw7:0 by5 gy4 bw7:0 bz5 bz4 rx4:0 gz4 gy3:0 gx4:0 bz0 gz3:0 bx5:0 by3:0 ry4:0 bz2 rz4:0 bz3");
            Two(0x1E, false, 6, 6, 6, 6, "rw5:0 gz4 bz0 bz1 by4 gw5:0 gy5 by5 bz2 gy4 bw5:0 gz5 bz3 bz5 bz4 rx5:0 gy3:0 gx5:0 gz3:0 bx5:0 by3:0 ry5:0 rz5:0");
            modes[0x03] = new Bc6Mode(1, false, 10, 10, 10, 10, Layout("rw9:0 gw9:0 bw9:0 rx9:0 gx9:0 bx9:0"));
            modes[0x07] = new Bc6Mode(1, true, 11, 9, 9, 9, Layout("rw9:0 gw9:0 bw9:0 rx8:0 rw10 gx8:0 gw10 bx8:0 bw10"));
            modes[0x0B] = new Bc6Mode(1, true, 12, 8, 8, 8, Layout("rw9:0 gw9:0 bw9:0 rx7:0 rw10:11 gx7:0 gw10:11 bx7:0 bw10:11"));
            modes[0x0F] = new Bc6Mode(1, true, 16, 4, 4, 4, Layout("rw9:0 gw9:0 bw9:0 rx3:0 rw10:15 gx3:0 gw10:15 bx3:0 bw10:15"));
            return modes;
        }

        public static Color Bc6hUnsigned(ReadOnlySpan<byte> block, int px, int py)
        {
            var bits = new Bits(block);
            int low = bits.Read(2);
            int code = low < 2 ? low : low | (bits.Read(3) << 2);
            var mode = Bc6Modes[code];
            // Reserved modes decode to black.
            if (mode == null) return new Color(0f, 0f, 0f, 1f);
            Span<int> f = stackalloc int[13];
            foreach (var hb in mode.Header) f[(int)hb.Field] |= bits.Bit() << hb.Bit;
            int partition = mode.Subsets == 2 ? f[(int)F.D] : 0;

            // Endpoints: subset 0 (w, x), subset 1 (y, z), as r, g, b.
            Span<int> e = stackalloc int[12];
            for (int c = 0; c < 3; c++)
            {
                e[c] = f[(int)F.RW + c];
                e[3 + c] = f[(int)F.RX + c];
                e[6 + c] = f[(int)F.RY + c];
                e[9 + c] = f[(int)F.RZ + c];
            }
            int wMask = (1 << mode.EndpointBits) - 1;
            for (int c = 0; c < 3; c++)
            {
                int delta = c == 0 ? mode.DeltaR : c == 1 ? mode.DeltaG : mode.DeltaB;
                for (int k = 1; k < mode.Subsets * 2; k++)
                {
                    if (!mode.Transformed) continue;
                    int d = SignExtend(e[k * 3 + c], delta);
                    e[k * 3 + c] = (e[c] + d) & wMask;
                }
            }
            for (int i = 0; i < mode.Subsets * 6; i++) e[i] = UnquantizeUnsigned(e[i], mode.EndpointBits);

            int texel = py * 4 + px;
            int indexBits = mode.Subsets == 2 ? 3 : 4;
            int index = 0;
            for (int i = 0; i < 16; i++)
            {
                int value = bits.Read(indexBits - (IsAnchor(mode.Subsets, partition, i) ? 1 : 0));
                if (i == texel) index = value;
            }
            int subset = SubsetOf(mode.Subsets, partition, texel);
            int weight = WeightsFor(indexBits)[index];
            int b0 = subset * 6;
            Span<float> rgb = stackalloc float[3];
            for (int c = 0; c < 3; c++)
            {
                int v = Interpolate(e[b0 + c], e[b0 + 3 + c], weight);
                rgb[c] = (float)BitConverter.UInt16BitsToHalf((ushort)((v * 31) >> 6));
            }
            return new Color(rgb[0], rgb[1], rgb[2], 1f);
        }

        static int SignExtend(int value, int bits) => (value << (32 - bits)) >> (32 - bits);

        static int UnquantizeUnsigned(int value, int bits)
        {
            if (bits >= 15) return value;
            if (value == 0) return 0;
            if (value == (1 << bits) - 1) return 0xFFFF;
            return ((value << 16) + 0x8000) >> bits;
        }

        // ----- ETC1, ETC2 (RGB, punch-through alpha), EAC -----

        static readonly int[,] EtcModifiers =
        {
            { 2, 8 }, { 5, 17 }, { 9, 29 }, { 13, 42 }, { 18, 60 }, { 24, 80 }, { 33, 106 }, { 47, 183 },
        };

        static readonly int[] EtcDistances = [3, 6, 11, 16, 23, 32, 41, 64];

        public enum EtcKind { Etc1, Etc2, Etc2PunchThrough }

        /// Texel (px, py) of an 8-byte ETC1/ETC2 colour block, as 8-bit channels.
        public static (int R, int G, int B, int A) Etc(ReadOnlySpan<byte> b, int px, int py, EtcKind kind)
        {
            uint indices = BinaryPrimitives.ReadUInt32BigEndian(b[4..]);
            int i = px * 4 + py; // pixel indices are stored column by column
            int index = (int)(((indices >> (16 + i)) & 1) << 1 | ((indices >> i) & 1));
            bool differential = (b[3] & 2) != 0;
            bool punchThrough = kind == EtcKind.Etc2PunchThrough;
            // In punch-through blocks this bit says whether the block is opaque, and every block
            // uses the differential layout.
            bool opaque = !punchThrough || differential;
            if (punchThrough) differential = true;
            bool flip = (b[3] & 1) != 0;
            int r1, g1, b1, r2, g2, b2;
            if (!differential)
            {
                r1 = Expand4(b[0] >> 4); r2 = Expand4(b[0] & 15);
                g1 = Expand4(b[1] >> 4); g2 = Expand4(b[1] & 15);
                b1 = Expand4(b[2] >> 4); b2 = Expand4(b[2] & 15);
            }
            else
            {
                int rb = b[0] >> 3, gb = b[1] >> 3, bb = b[2] >> 3;
                int rd = rb + SignExtend(b[0] & 7, 3), gd = gb + SignExtend(b[1] & 7, 3), bd = bb + SignExtend(b[2] & 7, 3);
                if (kind != EtcKind.Etc1)
                {
                    if (rd is < 0 or > 31) return TMode(b, index, opaque);
                    if (gd is < 0 or > 31) return HMode(b, index, opaque);
                    if (bd is < 0 or > 31) return Planar(b, px, py);
                }
                r1 = Expand5(rb); g1 = Expand5(gb); b1 = Expand5(bb);
                r2 = Expand5(rd & 31); g2 = Expand5(gd & 31); b2 = Expand5(bd & 31);
            }
            bool second = flip ? py >= 2 : px >= 2;
            int table = second ? (b[3] >> 2) & 7 : b[3] >> 5;
            if (!opaque && index == 2) return (0, 0, 0, 0);
            int modifier = index switch
            {
                0 => opaque ? EtcModifiers[table, 0] : 0,
                1 => EtcModifiers[table, 1],
                2 => -EtcModifiers[table, 0],
                _ => -EtcModifiers[table, 1],
            };
            return second
                ? (Clamp255(r2 + modifier), Clamp255(g2 + modifier), Clamp255(b2 + modifier), 255)
                : (Clamp255(r1 + modifier), Clamp255(g1 + modifier), Clamp255(b1 + modifier), 255);
        }

        static (int, int, int, int) TMode(ReadOnlySpan<byte> b, int index, bool opaque)
        {
            int r1 = Expand4(((b[0] >> 1) & 0xC) | (b[0] & 3)), g1 = Expand4(b[1] >> 4), b1 = Expand4(b[1] & 15);
            int r2 = Expand4(b[2] >> 4), g2 = Expand4(b[2] & 15), b2 = Expand4(b[3] >> 4);
            int d = EtcDistances[((b[3] >> 1) & 6) | (b[3] & 1)];
            if (!opaque && index == 2) return (0, 0, 0, 0);
            return index switch
            {
                0 => (r1, g1, b1, 255),
                1 => (Clamp255(r2 + d), Clamp255(g2 + d), Clamp255(b2 + d), 255),
                2 => (r2, g2, b2, 255),
                _ => (Clamp255(r2 - d), Clamp255(g2 - d), Clamp255(b2 - d), 255),
            };
        }

        static (int, int, int, int) HMode(ReadOnlySpan<byte> b, int index, bool opaque)
        {
            int r1 = (b[0] >> 3) & 15, g1 = ((b[0] & 7) << 1) | ((b[1] >> 4) & 1), b1 = (b[1] & 8) | ((b[1] & 3) << 1) | (b[2] >> 7);
            int r2 = (b[2] >> 3) & 15, g2 = ((b[2] & 7) << 1) | (b[3] >> 7), b2 = (b[3] >> 3) & 15;
            int order = ((r1 << 8) | (g1 << 4) | b1) >= ((r2 << 8) | (g2 << 4) | b2) ? 1 : 0;
            int d = EtcDistances[(b[3] & 4) | ((b[3] & 1) << 1) | order];
            r1 = Expand4(r1); g1 = Expand4(g1); b1 = Expand4(b1);
            r2 = Expand4(r2); g2 = Expand4(g2); b2 = Expand4(b2);
            if (!opaque && index == 2) return (0, 0, 0, 0);
            return index switch
            {
                0 => (Clamp255(r1 + d), Clamp255(g1 + d), Clamp255(b1 + d), 255),
                1 => (Clamp255(r1 - d), Clamp255(g1 - d), Clamp255(b1 - d), 255),
                2 => (Clamp255(r2 + d), Clamp255(g2 + d), Clamp255(b2 + d), 255),
                _ => (Clamp255(r2 - d), Clamp255(g2 - d), Clamp255(b2 - d), 255),
            };
        }

        static (int, int, int, int) Planar(ReadOnlySpan<byte> b, int px, int py)
        {
            int ro = Expand6((b[0] >> 1) & 0x3F);
            int go = Expand7(((b[0] & 1) << 6) | ((b[1] >> 1) & 0x3F));
            int bo = Expand6(((b[1] & 1) << 5) | (b[2] & 0x18) | ((b[2] & 3) << 1) | (b[3] >> 7));
            int rh = Expand6(((b[3] >> 1) & 0x3E) | (b[3] & 1));
            int gh = Expand7(b[4] >> 1);
            int bh = Expand6(((b[4] & 1) << 5) | (b[5] >> 3));
            int rv = Expand6(((b[5] & 7) << 3) | (b[6] >> 5));
            int gv = Expand7(((b[6] & 0x1F) << 2) | (b[7] >> 6));
            int bv = Expand6(b[7] & 0x3F);
            int C(int o, int h, int v) => Clamp255((px * (h - o) + py * (v - o) + 4 * o + 2) >> 2);
            return (C(ro, rh, rv), C(go, gh, gv), C(bo, bh, bv), 255);
        }

        static readonly int[,] EacModifiers =
        {
            { -3, -6, -9, -15, 2, 5, 8, 14 }, { -3, -7, -10, -13, 2, 6, 9, 12 }, { -2, -5, -8, -13, 1, 4, 7, 12 }, { -2, -4, -6, -13, 1, 3, 5, 12 },
            { -3, -6, -8, -12, 2, 5, 7, 11 }, { -3, -7, -9, -11, 2, 6, 8, 10 }, { -4, -7, -8, -11, 3, 6, 7, 10 }, { -3, -5, -8, -11, 2, 4, 7, 10 },
            { -2, -6, -8, -10, 1, 5, 7, 9 }, { -2, -5, -8, -10, 1, 4, 7, 9 }, { -2, -4, -8, -10, 1, 3, 7, 9 }, { -2, -5, -7, -10, 1, 4, 6, 9 },
            { -3, -4, -7, -10, 2, 3, 6, 9 }, { -1, -2, -3, -10, 0, 1, 2, 9 }, { -4, -6, -8, -9, 3, 5, 7, 8 }, { -3, -5, -7, -9, 2, 4, 6, 8 },
        };

        static (int Base, int Multiplier, int Modifier) EacTexel(ReadOnlySpan<byte> b, int px, int py)
        {
            ulong indices = 0;
            for (int k = 2; k < 8; k++) indices = (indices << 8) | b[k];
            int i = px * 4 + py;
            int index = (int)((indices >> (45 - 3 * i)) & 7);
            return (b[0], b[1] >> 4, EacModifiers[b[1] & 15, index]);
        }

        /// The 8-bit alpha of an ETC2 RGBA8 block's first half.
        public static int EacAlpha(ReadOnlySpan<byte> b, int px, int py)
        {
            var (baseValue, multiplier, modifier) = EacTexel(b, px, py);
            return Clamp255(baseValue + multiplier * modifier);
        }

        /// An 11-bit EAC channel: unsigned in [0, 1], signed in [-1, 1].
        public static float Eac11(ReadOnlySpan<byte> b, int px, int py, bool signed)
        {
            var (baseValue, multiplier, modifier) = EacTexel(b, px, py);
            int step = multiplier == 0 ? modifier : modifier * multiplier * 8;
            if (!signed) return Math.Clamp(baseValue * 8 + 4 + step, 0, 2047) / 2047f;
            int s = (sbyte)baseValue;
            if (s == -128) s = -127;
            return Math.Clamp(s * 8 + step, -1023, 1023) / 1023f;
        }

        static int Expand4(int v) => v * 17;
        static int Expand5(int v) => (v << 3) | (v >> 2);
        static int Expand6(int v) => (v << 2) | (v >> 4);
        static int Expand7(int v) => (v << 1) | (v >> 6);
        static int Clamp255(int v) => Math.Clamp(v, 0, 255);
    }
}
