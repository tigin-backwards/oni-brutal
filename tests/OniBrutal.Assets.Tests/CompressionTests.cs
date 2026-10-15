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
    }
}
