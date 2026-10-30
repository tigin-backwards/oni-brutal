using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OniMoono.Assets.Shaders
{
    /// A stage of a Vulkan program: where its SMOL-V stream is in the payload, and its SPIR-V.
    public sealed record VulkanStage(int Index, int Offset, int Size, byte[] Spirv);

    /// One Vulkan program entry (platform 18): every stage of a variant, as SMOL-V-compressed
    /// SPIR-V. Its layout, inferred from the data:
    /// - the 24-byte entry header GLCore and Metal entries have (magic 0x0C0A75BA, GPU program
    ///   type 25, four statistics) and the variant's keywords (a count, then length-prefixed
    ///   strings padded to four bytes);
    /// - a length-prefixed payload: a flags word, then (offset, size) pairs into the payload, one
    ///   per stage slot (empty slots are zero), then a stream of SMOL-V per present stage;
    /// - padding to four bytes, the vertex channel mask, and a count of (channel, vertex
    ///   component) pairs, as Metal entries end.
    public sealed class VulkanProgram
    {
        public string[] Keywords { get; private init; } = [];
        public uint Flags { get; private init; }
        public VulkanStage[] Stages { get; private init; } = [];
        public uint ChannelMask { get; private init; }
        public (int Channel, int Component)[] Inputs { get; private init; } = [];
        /// The payload's header words (flags and stage slots), uninterpreted beyond the slots.
        public uint[] PayloadHeader { get; private init; } = [];
        public byte[] Trailing { get; private init; } = [];

        const int StageSlots = 6;

        public static VulkanProgram Read(ReadOnlySpan<byte> entry)
        {
            int pos = 0;
            int Int(ReadOnlySpan<byte> d)
            {
                if (pos + 4 > d.Length) throw new InvalidDataException("Vulkan program ends early.");
                int v = BitConverter.ToInt32(d[pos..]);
                pos += 4;
                return v;
            }
            if ((uint)Int(entry) != MetalProgram.EntryMagic) throw new InvalidDataException("Not a shader program entry.");
            int type = Int(entry);
            if (type != GpuProgramTypes.SpirV) throw new InvalidDataException($"Shader program entry of GPU program type {type} is not a Vulkan program.");
            pos = 24;
            int keywordCount = Int(entry);
            if (keywordCount < 0 || keywordCount > entry.Length / 4) throw new InvalidDataException($"Vulkan program: bad keyword count {keywordCount}.");
            var keywords = new string[keywordCount];
            for (int i = 0; i < keywordCount; i++)
            {
                int len = Int(entry);
                if (len < 0 || pos + len > entry.Length) throw new InvalidDataException("Vulkan program: bad keyword.");
                keywords[i] = Encoding.UTF8.GetString(entry.Slice(pos, len));
                pos += (len + 3) & ~3;
            }
            int payloadLength = Int(entry);
            if (payloadLength < 8 || pos + payloadLength > entry.Length) throw new InvalidDataException($"Vulkan program: bad payload length {payloadLength}.");
            var payload = entry.Slice(pos, payloadLength);
            pos += (payloadLength + 3) & ~3;
            // The stage slots: (offset, size) pairs after the flags word, up to the first stream.
            uint flags = BitConverter.ToUInt32(payload);
            var stages = new List<VulkanStage>();
            int firstStream = payloadLength;
            var header = new List<uint> { flags };
            for (int slot = 0; slot < StageSlots; slot++)
            {
                int offset = BitConverter.ToInt32(payload[(4 + slot * 8)..]), size = BitConverter.ToInt32(payload[(8 + slot * 8)..]);
                header.Add((uint)offset);
                header.Add((uint)size);
                if (size == 0) continue;
                if (offset < 0 || size < 0 || offset + size > payloadLength) throw new InvalidDataException($"Vulkan program: stage slot {slot} at {offset} size {size} outside its payload of {payloadLength}.");
                var stream = payload.Slice(offset, size);
                if (!SmolV.IsSmolV(stream)) throw new InvalidDataException($"Vulkan program: stage slot {slot} is not SMOL-V.");
                stages.Add(new VulkanStage(slot, offset, size, SmolV.Decode(stream)));
                firstStream = Math.Min(firstStream, offset);
            }
            uint channels = (uint)Int(entry);
            int inputCount = Int(entry);
            if (inputCount < 0 || inputCount > 32) throw new InvalidDataException($"Vulkan program: bad vertex input count {inputCount}.");
            var inputs = new (int, int)[inputCount];
            for (int i = 0; i < inputCount; i++) inputs[i] = (Int(entry), Int(entry));
            return new VulkanProgram
            {
                Keywords = keywords,
                Flags = flags,
                Stages = stages.ToArray(),
                ChannelMask = channels,
                Inputs = inputs,
                PayloadHeader = header.ToArray(),
                Trailing = entry[pos..].ToArray(),
            };
        }
    }
}
