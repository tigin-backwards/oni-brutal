using System;
using System.IO;
using System.Text;

namespace OniMoono.Assets.Shaders
{
    /// A vertex input of a Metal vertex program: the mesh channel (ShaderChannel: 0 position,
    /// 1 normal, 2 tangent, 3 colour, 4 + n texcoord n) feeding `[[ attribute(Attribute) ]]`.
    public readonly record struct MetalVertexInput(int Channel, int Attribute);

    /// One Metal program entry (platform 14): a single stage, as MSL source. Its layout, inferred
    /// from the data (every Metal program of the game decodes to its end):
    /// - the 24-byte entry header GLCore entries have (magic 0x0C0A75BA, GPU program type, four
    ///   statistics);
    /// - the stage's keywords (a count, then length-prefixed strings padded to four bytes);
    /// - a length-prefixed payload: a header of 4-byte fields (magic 0xF00DCAFE, the entry point
    ///   name's offset, a field that is 7, the source's offset, then fields this reader keeps
    ///   uninterpreted), the entry point's name (NUL-terminated, `xlatMtlMain`) and the MSL text up
    ///   to the payload's end;
    /// - padding to four bytes, the vertex channel mask (as GLCore's, 0 for fragment programs), and
    ///   a count of (channel, vertex component) pairs; vertex component 13 + n is
    ///   `[[ attribute(n) ]]`.
    public sealed class MetalProgram
    {
        public const uint EntryMagic = 0x0C0A75BA;
        public const uint PayloadMagic = 0xF00DCAFE;
        /// The vertex component of `[[ attribute(0) ]]` in the attribute table.
        public const int FirstAttributeComponent = 13;

        public int GpuProgramType { get; private init; }
        public string[] Keywords { get; private init; } = [];
        public string EntryPoint { get; private init; } = "";
        public string Source { get; private init; } = "";
        public uint ChannelMask { get; private init; }
        public MetalVertexInput[] Inputs { get; private init; } = [];
        /// The payload header's 4-byte fields (the first is its magic).
        public int[] PayloadHeader { get; private init; } = [];

        public bool IsVertex => GpuProgramType == GpuProgramTypes.MetalVertex;

        public static MetalProgram Read(ReadOnlySpan<byte> entry)
        {
            int pos = 0;
            int Int(ReadOnlySpan<byte> d)
            {
                if (pos + 4 > d.Length) throw new InvalidDataException("Metal program ends early.");
                int v = BitConverter.ToInt32(d[pos..]);
                pos += 4;
                return v;
            }
            if ((uint)Int(entry) != EntryMagic) throw new InvalidDataException("Not a shader program entry.");
            int type = Int(entry);
            if (!GpuProgramTypes.IsMetal(type)) throw new InvalidDataException($"Shader program entry of GPU program type {type} is not a Metal program.");
            pos = 24;
            int keywordCount = Int(entry);
            if (keywordCount < 0 || keywordCount > entry.Length / 4) throw new InvalidDataException($"Metal program: bad keyword count {keywordCount}.");
            var keywords = new string[keywordCount];
            for (int i = 0; i < keywordCount; i++)
            {
                int len = Int(entry);
                if (len < 0 || pos + len > entry.Length) throw new InvalidDataException("Metal program: bad keyword.");
                keywords[i] = Encoding.UTF8.GetString(entry.Slice(pos, len));
                pos += (len + 3) & ~3;
            }
            int payloadLength = Int(entry);
            if (payloadLength < 16 || pos + payloadLength > entry.Length) throw new InvalidDataException($"Metal program: bad payload length {payloadLength}.");
            var payload = entry.Slice(pos, payloadLength);
            pos += (payloadLength + 3) & ~3;
            if ((uint)BitConverter.ToInt32(payload) != PayloadMagic) throw new InvalidDataException("Metal program: payload without its magic.");
            int nameOffset = BitConverter.ToInt32(payload[4..]), sourceOffset = BitConverter.ToInt32(payload[12..]);
            if (nameOffset < 16 || nameOffset % 4 != 0 || sourceOffset <= nameOffset || sourceOffset > payload.Length)
                throw new InvalidDataException($"Metal program: entry point at {nameOffset}, source at {sourceOffset} in a payload of {payload.Length}.");
            var header = new int[nameOffset / 4];
            for (int i = 0; i < header.Length; i++) header[i] = BitConverter.ToInt32(payload[(4 * i)..]);
            var name = payload[nameOffset..sourceOffset];
            int nul = name.IndexOf((byte)0);
            if (nul < 0) throw new InvalidDataException("Metal program: unterminated entry point name.");
            uint channels = (uint)Int(entry);
            int inputCount = Int(entry);
            if (inputCount < 0 || inputCount > 32) throw new InvalidDataException($"Metal program: bad vertex input count {inputCount}.");
            var inputs = new MetalVertexInput[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                int channel = Int(entry), component = Int(entry);
                inputs[i] = new MetalVertexInput(channel, component - FirstAttributeComponent);
            }
            if (pos != entry.Length) throw new InvalidDataException($"Metal program: {entry.Length - pos} trailing bytes.");
            return new MetalProgram
            {
                GpuProgramType = type,
                Keywords = keywords,
                EntryPoint = Encoding.ASCII.GetString(name[..nul]),
                Source = Encoding.UTF8.GetString(payload[sourceOffset..]),
                ChannelMask = channels,
                Inputs = inputs,
                PayloadHeader = header,
            };
        }
    }
}
