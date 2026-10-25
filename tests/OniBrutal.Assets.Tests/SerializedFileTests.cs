using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OniBrutal.Assets.Files;
using Xunit;

namespace OniBrutal.Assets.Tests
{
    /// SerializedFile metadata in the layouts of the format versions mods' bundles use: 17
    /// (Unity 2018.3, Japanese Font Patch's fonts) has no reference type hash per type tree node,
    /// no type dependencies and no reference types; 21 (2020) has all three.
    public class SerializedFileTests
    {
        static byte[] File(int version)
        {
            var meta = new List<byte>();
            void Bytes(IEnumerable<byte> b) => meta.AddRange(b);
            void I32(int v) => Bytes(BitConverter.GetBytes(v));
            void U32(uint v) => Bytes(BitConverter.GetBytes(v));
            void I64(long v) => Bytes(BitConverter.GetBytes(v));
            void I16(short v) => Bytes(BitConverter.GetBytes(v));
            void Str(string s) { Bytes(Encoding.UTF8.GetBytes(s)); meta.Add(0); }
            void Align() { while ((20 + meta.Count) % 4 != 0) meta.Add(0); }

            Str(version == 17 ? "2018.3.0f1" : "2020.3.48f1");
            I32(24);                 // target platform
            meta.Add(1);             // type trees
            I32(1);                  // one type: Texture2D, with a two-node tree
            I32(28); meta.Add(0); I16(-1);
            Bytes(new byte[16]);     // old type hash
            var strings = Encoding.UTF8.GetBytes("Texture2D\0Base\0int\0m_Width\0");
            I32(2); I32(strings.Length);
            void Node(ushort ver, byte level, uint type, uint name, int size, int index)
            {
                Bytes(BitConverter.GetBytes(ver)); meta.Add(level); meta.Add(0);
                U32(type); U32(name); I32(size); I32(index); U32(0);
                if (version >= 19) I64(0x1234); // reference type hash
            }
            Node(3, 0, 0, 10, -1, 0);
            Node(1, 1, 15, 19, 4, 1);
            Bytes(strings);
            if (version >= 21) { I32(1); I32(7); } // type dependencies
            I32(2);                  // two objects
            foreach (var (id, start) in new[] { (1L, 0u), (-7L, 16u) })
            {
                Align();
                I64(id); U32(start); U32(4); I32(0);
            }
            I32(1); I32(0); Align(); I64(11); // one script type
            I32(1); Str(""); Bytes(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef").ToByteArray()); I32(2);
            Str("library/unity default resources");
            if (version >= 20) I32(0); // reference types
            Str("");                 // user information

            int dataOffset = (20 + meta.Count + 15) / 16 * 16;
            var file = new byte[dataOffset + 32];
            void BigEndian(int at, int v) { file[at] = (byte)(v >> 24); file[at + 1] = (byte)(v >> 16); file[at + 2] = (byte)(v >> 8); file[at + 3] = (byte)v; }
            BigEndian(0, meta.Count); BigEndian(4, file.Length); BigEndian(8, version); BigEndian(12, dataOffset);
            meta.CopyTo(file, 20);   // byte 16 (endianness) 0: little-endian data
            BitConverter.GetBytes(256).CopyTo(file, dataOffset);
            BitConverter.GetBytes(512).CopyTo(file, dataOffset + 16);
            return file;
        }

        [Theory]
        [InlineData(17)]
        [InlineData(19)]
        [InlineData(21)]
        public void MetadataReadsTheSameInEachVersion(int version)
        {
            var f = SerializedFile.Parse("CAB-test", File(version));
            Assert.Equal(version, f.Version);
            Assert.Equal(version == 17 ? "2018.3.0f1" : "2020.3.48f1", f.UnityVersion);
            var type = Assert.Single(f.Types);
            Assert.Equal(28, type.ClassId);
            Assert.Equal("Texture2D", type.Tree!.TypeName);
            Assert.Equal("m_Width", Assert.Single(type.Tree.Children).Name);
            Assert.Equal(new[] { 1L, -7L }, f.Objects.Select(o => o.PathId));
            Assert.Equal(new[] { 0L, 16L }, f.Objects.Select(o => o.ByteStart));
            Assert.Equal(11L, Assert.Single(f.ScriptTypes).PathId);
            Assert.Equal("library/unity default resources", Assert.Single(f.Externals).Path);
            Assert.Equal(512, f.OpenObject(f.ObjectsById[-7]).ReadInt32());
        }

        [Fact]
        public void VersionsBefore17AreRefused()
        {
            var bytes = File(17);
            bytes[11] = 15;
            var e = Assert.Throws<InvalidDataException>(() => SerializedFile.Parse("CAB-old", bytes));
            Assert.Contains("version 15 is not supported (17+)", e.Message);
        }
    }
}
