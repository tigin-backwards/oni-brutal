using System;
using System.IO;
using OniBrutal.Assets.Compression;
using Xunit;

namespace OniBrutal.Assets.Tests
{
    public class CompressionTests
    {
        static byte[] Data(string name) => File.ReadAllBytes(Path.Combine(System.AppContext.BaseDirectory, "Data", name));

        [Theory]
        [InlineData("sample.lz4")]
        [InlineData("sample.lz4hc")]
        public void Lz4BlockDecodes(string file)
        {
            var expected = Data("sample.bin");
            var output = new byte[expected.Length];
            Lz4.Decode(Data(file), output);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void LzmaDecodes()
        {
            var expected = Data("sample.bin");
            var output = new byte[expected.Length];
            Lzma.Decode(Data("sample.lzma"), output);
            Assert.Equal(expected, output);
        }
    
        // Corrupt or truncated blocks (a damaged download) are invalid data, which the bundle
        // loader reports as an archive it cannot open, not another exception.
        [Theory]
        [InlineData("sample.lz4")]
        [InlineData("sample.lz4hc")]
        public void TruncatedLz4BlockIsInvalidData(string file)
        {
            var expected = Data("sample.bin");
            var input = Data(file);
            for (int cut = 1; cut < input.Length; cut += System.Math.Max(1, input.Length / 97))
                Assert.Throws<InvalidDataException>(() => Lz4.Decode(input.AsSpan(0, cut), new byte[expected.Length]));
        }

        [Fact]
        public void TruncatedOrDamagedLzmaIsInvalidData()
        {
            var expected = Data("sample.bin");
            var input = Data("sample.lzma");
            for (int cut = 6; cut < input.Length; cut += System.Math.Max(1, input.Length / 97))
                Assert.Throws<InvalidDataException>(() => Lzma.Decode(input.AsSpan(0, cut), new byte[expected.Length]));
            var random = new System.Random(5);
            for (int round = 0; round < 200; round++)
            {
                var damaged = (byte[])input.Clone();
                for (int k = 0; k < 4; k++) damaged[5 + random.Next(damaged.Length - 5)] ^= (byte)(1 << random.Next(8));
                try { Lzma.Decode(damaged, new byte[expected.Length]); }
                catch (InvalidDataException) { }
            }
        }
}
}
