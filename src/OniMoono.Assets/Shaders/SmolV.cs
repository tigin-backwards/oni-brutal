// A port of the decoder of smol-v (https://github.com/aras-p/smol-v), used under the MIT licence:
//
// Copyright (c) 2016-2024 Aras Pranckevicius
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
// BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
using System;
using System.IO;

namespace OniMoono.Assets.Shaders
{
    /// SMOL-V decoding: Unity stores its Vulkan programs' SPIR-V in this compacted form. A port of
    /// the decoder of Aras Pranckevičius' smol-v (https://github.com/aras-p/smol-v,
    /// source/smolv.cpp, MIT; docs/LICENSING.md): the same steps, the same opcode table.
    ///
    /// The format: a header like SPIR-V's ("SMOL" magic, version with the SMOL-V encoding version
    /// in its top byte, generator, bound, schema) and the decoded size; then each instruction as a
    /// varint of its length and remapped opcode, its type and result IDs (results delta-encoded
    /// from the previous result), decorations relative to the previous target, member decorations
    /// grouped, a compact form for 4-component vector shuffles, and the remaining operands as
    /// varints or raw words as the opcode's table entry says.
    public static class SmolV
    {
        const uint SpirvMagic = 0x07230203, SmolMagic = 0x534D4F4C;
        const int OpUndef = 1, OpSourceContinued = 2, OpSource = 3, OpSourceExtension = 4, OpString = 7, OpLine = 8, OpExtension = 10,
            OpExtInstImport = 11, OpVectorShuffleCompact = 13, OpMemoryModel = 14, OpEntryPoint = 15, OpTypePointer = 32, OpVariable = 59,
            OpLoad = 61, OpStore = 62, OpAccessChain = 65, OpDecorate = 71, OpMemberDecorate = 72, OpVectorShuffle = 79, OpFNegate = 127,
            OpFAdd = 129, OpFMul = 133, OpLabel = 248, OpModuleProcessed = 330, OpGroupNonUniformQuadSwap = 366;

        /// Per opcode (index), four digits: has a result ID, has a type ID, how many IDs after them
        /// are written as deltas from the result, whether the rest is varint-encoded (smol-v's
        /// kSpirvOpData, version 1).
        const string OpTable =
            "0000110000000001000000000000000000011100000010001101112100010001000100011100100110011001100110011001100110011001100110011001100110011001100110011001100110010001110011001100110011901101110011001100110011001190110011001101110000001190110011011100111100210000" +
            "0000110111001100110011001100000100011000000000001100111111211121119011111121111011001100110011211121113111311121112111311131112111311131112100311110111011101120111011201110111011001110111011101110111011101110111011101110111011101110111011111110110011101110" +
            "1120112011201120112011201120112011201120112011201120112011201120112011201120112011201120112011201120110011101110111011101110111011101120112011201120112011201120111011301120112011201120112011201120112011201120112011201120112011201120112011201120112011201120" +
            "1100110011201120112011201120112011101140113011301110111011001100110011001100110011001100110011001100110000000000000000001100110000300020110011000000110011001100110011001100110011001100110011001100110011001100110011000021001110000010003100000000000000000000" +
            "0000000011001100000011001100110011001100110011001100110011001100110011001100110011001100110011000000000011001100110011001100000000001100110011001100110011001100110000000000110011000000000011001100112111211131113111211121113111311121113111311110000011000000" +
            "11001100110011001100110011001100110100211100000100011111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111";

        static bool Flag(int op, int count, int field) => op >= 0 && op < count && OpTable[op * 4 + field] != '0';
        static int Digit(int op, int count, int field) => op >= 0 && op < count ? OpTable[op * 4 + field] - '0' : 0;

        static int KnownOps(int version) => version switch
        {
            0 => OpModuleProcessed + 1,
            1 => OpGroupNonUniformQuadSwap + 1,
            _ => 0,
        };

        /// Whether the bytes start with a SMOL-V header this decoder reads.
        public static bool IsSmolV(ReadOnlySpan<byte> data)
        {
            if (data.Length < 24 || BitConverter.ToUInt32(data) != SmolMagic) return false;
            uint version = BitConverter.ToUInt32(data[4..]);
            uint spirv = version & 0x00FFFFFF;
            return spirv >= 0x00010000 && spirv <= 0x00010600 && version >> 24 <= 1;
        }

        /// The SPIR-V size the header records.
        public static int DecodedSize(ReadOnlySpan<byte> data) => IsSmolV(data) ? (int)BitConverter.ToUInt32(data[20..]) : throw new InvalidDataException("Not SMOL-V data.");

        /// The SPIR-V module. Throws InvalidDataException for malformed data, and when the decoded
        /// size is not the one the header records.
        public static byte[] Decode(ReadOnlySpan<byte> smolv)
        {
            int size = DecodedSize(smolv);
            var data = smolv.ToArray();
            if (size % 4 != 0) throw new InvalidDataException($"SMOL-V decoded size {size} is not whole words.");
            var output = new uint[size / 4];
            int o = 0, p = 0;
            void Write(uint v)
            {
                if (o >= output.Length) throw new InvalidDataException("SMOL-V data decodes past its recorded size.");
                output[o++] = v;
            }
            uint Read4()
            {
                if (p + 4 > data.Length) throw new InvalidDataException("SMOL-V data ends inside a word.");
                uint v = BitConverter.ToUInt32(data, p);
                p += 4;
                return v;
            }
            uint Varint(byte[] d)
            {
                uint v = 0;
                int shift = 0;
                while (p < d.Length)
                {
                    byte b = d[p++];
                    v |= (uint)(b & 127) << shift;
                    shift += 7;
                    if ((b & 128) == 0) break;
                }
                return v;
            }
            static int ZigDecode(uint u) => (u & 1) != 0 ? (int)((u >> 1) ^ ~0u) : (int)(u >> 1);

            Write(SpirvMagic);
            p = 4;
            uint version = Read4();
            int smolVersion = (int)(version >> 24);
            Write(version & 0x00FFFFFF);
            Write(Read4()); // generator
            Write(Read4()); // bound
            Write(Read4()); // schema
            p += 4; // decoded size
            int known = KnownOps(smolVersion);
            uint prevResult = 0, prevDecorate = 0;
            while (p < data.Length)
            {
                uint lengthOp = Varint(data);
                uint length = ((lengthOp >> 20) << 4) | ((lengthOp >> 4) & 0xF);
                int op = Remap((int)(((lengthOp >> 4) & 0xFFF0) | (lengthOp & 0xF)));
                length = DecodeLength(op, length);
                bool wasSwizzle = op == OpVectorShuffleCompact;
                if (wasSwizzle) op = OpVectorShuffle;
                Write((length << 16) | (uint)op);
                uint written = 1;
                if (Flag(op, known, 1))
                {
                    Write(Varint(data));
                    written++;
                }
                if (Flag(op, known, 0))
                {
                    prevResult = (uint)(prevResult + ZigDecode(Varint(data)));
                    Write(prevResult);
                    written++;
                }
                if (op is OpDecorate or OpMemberDecorate)
                {
                    prevDecorate = (uint)(prevDecorate + ZigDecode(Varint(data)));
                    Write(prevDecorate);
                    written++;
                }
                if (op == OpMemberDecorate)
                {
                    if (p >= data.Length) throw new InvalidDataException("SMOL-V member decorations cut short.");
                    int count = data[p++];
                    uint prevIndex = 0, prevOffset = 0;
                    for (int m = 0; m < count; m++)
                    {
                        uint index = Varint(data) + prevIndex;
                        prevIndex = index;
                        uint decoration = Varint(data);
                        int extra = DecorationExtraOps((int)decoration);
                        uint memberLength = extra == -1 ? Varint(data) + 4 : 4 + (uint)extra;
                        // The first member's op and target were written above.
                        if (m != 0)
                        {
                            Write((memberLength << 16) | (uint)op);
                            Write(prevDecorate);
                        }
                        Write(index);
                        Write(decoration);
                        if (decoration == 35) // Offset
                        {
                            if (memberLength != 5) throw new InvalidDataException("SMOL-V Offset member decoration of another length.");
                            prevOffset = Varint(data) + prevOffset;
                            Write(prevOffset);
                        }
                        else
                            for (uint i = 4; i < memberLength; i++) Write(Varint(data));
                    }
                    continue;
                }
                int relative = Digit(op, known, 2);
                for (int i = 0; i < relative && written < length; i++, written++)
                    Write((uint)(prevResult - ZigDecode(Varint(data))));
                if (wasSwizzle && length <= 9)
                {
                    if (p >= data.Length) throw new InvalidDataException("SMOL-V shuffle cut short.");
                    uint swizzle = data[p++];
                    if (length > 5) Write((swizzle >> 6) & 3);
                    if (length > 6) Write((swizzle >> 4) & 3);
                    if (length > 7) Write((swizzle >> 2) & 3);
                    if (length > 8) Write(swizzle & 3);
                }
                else if (Flag(op, known, 3))
                    for (; written < length; written++) Write(Varint(data));
                else
                    for (; written < length; written++) Write(Read4());
            }
            if (o != output.Length) throw new InvalidDataException($"SMOL-V data decodes to {o * 4} bytes, the header records {size}.");
            var bytes = new byte[size];
            Buffer.BlockCopy(output, 0, bytes, 0, size);
            return bytes;
        }

        /// The most common opcodes swapped into small values for one-byte varints (pairs; the
        /// mapping is its own inverse).
        static readonly (int, int)[] swaps =
        [
            (OpDecorate, 0), (OpLoad, OpUndef), (OpStore, OpSourceContinued), (OpAccessChain, OpSource), (OpVectorShuffle, OpSourceExtension),
            (OpMemberDecorate, OpString), (OpLabel, OpLine), (OpVariable, 9), (OpFMul, OpExtension), (OpFAdd, OpExtInstImport),
            (OpTypePointer, OpMemoryModel), (OpFNegate, OpEntryPoint),
        ];

        static int Remap(int op)
        {
            foreach (var (a, b) in swaps)
            {
                if (op == a) return b;
                if (op == b) return a;
            }
            return op;
        }

        static uint DecodeLength(int op, uint length)
        {
            length++;
            if (op is OpVectorShuffle or OpVectorShuffleCompact) length += 4;
            if (op == OpDecorate) length += 2;
            if (op is OpLoad or OpAccessChain) length += 3;
            return length;
        }

        static int DecorationExtraOps(int decoration)
        {
            if (decoration == 0 || decoration is >= 2 and <= 5) return 0; // RelaxedPrecision, Block..ColMajor
            if (decoration is >= 29 and <= 37) return 1; // Stream..XfbStride
            return -1; // unknown: the length is encoded
        }
    }
}
