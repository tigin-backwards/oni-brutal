using System;
using System.Collections.Generic;
using System.Text;

namespace OniMoono.Assets.Tests
{
    /// Writes small shader blobs in the shipped data's layout (ShaderParameters, MetalProgram,
    /// ShaderProgramTable), for hand-written fixtures.
    sealed class ShaderBlobWriter
    {
        readonly List<byte> bytes = new();

        public ShaderBlobWriter Int(int v)
        {
            bytes.AddRange(BitConverter.GetBytes(v));
            return this;
        }

        public ShaderBlobWriter UInt(uint v) => Int((int)v);

        /// Length-prefixed, padded to four bytes.
        public ShaderBlobWriter String(string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            Int(b.Length);
            bytes.AddRange(b);
            while (bytes.Count % 4 != 0) bytes.Add(0);
            return this;
        }

        public ShaderBlobWriter Bytes(byte[] b)
        {
            bytes.AddRange(b);
            return this;
        }

        public ShaderBlobWriter Pad()
        {
            while (bytes.Count % 4 != 0) bytes.Add(0);
            return this;
        }

        public int Length => bytes.Count;

        public byte[] ToArray() => bytes.ToArray();

        /// A constant-buffer header: name, size and member count (members follow with Member).
        public ShaderBlobWriter Buffer(string name, int size, int members) => String(name).Int(size).Int(members);

        public ShaderBlobWriter Member(string name, int type, int rows, int columns, bool matrix, int arraySize, int offset)
            => String(name).Int(type).Int(rows).Int(columns).Int(matrix ? 1 : 0).Int(arraySize).Int(offset);

        /// A texture binding; `dimension` is TextureDimension, written as the blob does
        /// (dimension << 1 | multisampled).
        public ShaderBlobWriter Texture(string name, int index, int sampler, int dimension, bool multisampled = false)
            => String(name).Int(0).Int(index).Int(sampler).Int(dimension << 1 | (multisampled ? 1 : 0));

        public ShaderBlobWriter ConstantBufferBinding(string name, int index) => String(name).Int(1).Int(index).Int(0);

        public ShaderBlobWriter StructuredBuffer(string name, int index) => String(name).Int(2).Int(index).Int(1);

        /// A Metal program entry around `source`.
        public static byte[] MetalEntry(int gpuProgramType, string[] keywords, string source, uint channels, (int Channel, int Attribute)[] inputs)
        {
            var w = new ShaderBlobWriter().UInt(0x0C0A75BA).Int(gpuProgramType).Int(0).Int(0).Int(0).Int(0);
            w.Int(keywords.Length);
            foreach (var k in keywords) w.String(k);
            // Payload: magic, entry point offset 48, 7, source offset; fields up to the name.
            var name = Encoding.ASCII.GetBytes("xlatMtlMain\0");
            var text = Encoding.UTF8.GetBytes(source);
            var payload = new ShaderBlobWriter().UInt(0xF00DCAFE).Int(48).Int(7).Int(48 + name.Length).Int(0).Int(0).Int(0).Int(0).Int(0).Int(37).Int(-1).Int(0);
            payload.Bytes(name).Bytes(text);
            w.Int(payload.Length).Bytes(payload.ToArray()).Pad();
            w.UInt(channels).Int(inputs.Length);
            foreach (var (channel, attribute) in inputs) w.Int(channel).Int(13 + attribute);
            return w.ToArray();
        }

        /// One segment holding a blob table and the blobs after it.
        public static byte[] Table(params byte[][] blobs)
        {
            int at = 4 + 12 * blobs.Length;
            var w = new ShaderBlobWriter().Int(blobs.Length);
            foreach (var b in blobs)
            {
                w.Int(at).Int(b.Length).Int(0);
                at += b.Length;
            }
            foreach (var b in blobs) w.Bytes(b);
            return w.ToArray();
        }
    }
}
