using OniBrutal.Engine;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class RenderingDataTests : EngineTest
    {
        [Theory]
        [InlineData(TextureFormat.RGBA32)]
        [InlineData(TextureFormat.ARGB32)]
        [InlineData(TextureFormat.BGRA32)]
        [InlineData(TextureFormat.RGBAHalf)]
        [InlineData(TextureFormat.RGBAFloat)]
        public void RgbaFormatsRoundTrip(TextureFormat format)
        {
            TextureFormats.TryGetBlock(format, out _, out _, out int bpp);
            var texel = new byte[bpp];
            var c = new Color(0.2f, 0.4f, 0.6f, 0.8f);
            TextureFormats.Encode(format, texel, 1, 0, 0, c);
            var back = TextureFormats.Decode(format, texel, 1, 0, 0);
            Assert.Equal(c.r, back.r, 2);
            Assert.Equal(c.g, back.g, 2);
            Assert.Equal(c.b, back.b, 2);
            Assert.Equal(c.a, back.a, 2);
        }

        [Fact]
        public void SingleChannelFormatsReadLikeAShader()
        {
            var texel = new byte[1];
            TextureFormats.Encode(TextureFormat.Alpha8, texel, 1, 0, 0, new Color(0f, 0f, 0f, 0.5f));
            Assert.Equal(new Color(1f, 1f, 1f, 128 / 255f), TextureFormats.Decode(TextureFormat.Alpha8, texel, 1, 0, 0));
            TextureFormats.Encode(TextureFormat.R8, texel, 1, 0, 0, new Color(1f, 0.3f, 0.3f, 0.3f));
            Assert.Equal(new Color(1f, 0f, 0f, 1f), TextureFormats.Decode(TextureFormat.R8, texel, 1, 0, 0));
        }

        [Fact]
        public void Dxt1BlockDecodes()
        {
            // color0 = pure red (0xF800) > color1 = pure blue (0x001F): four-colour mode.
            // Texel indices (2 bits each, row-major from the low bits): 0, 1, 2, 3, then zeros.
            byte[] block = [0x00, 0xF8, 0x1F, 0x00, 0b11_10_01_00, 0, 0, 0];
            Assert.Equal(new Color(1f, 0f, 0f, 1f), TextureFormats.Decode(TextureFormat.DXT1, block, 4, 0, 0));
            Assert.Equal(new Color(0f, 0f, 1f, 1f), TextureFormats.Decode(TextureFormat.DXT1, block, 4, 1, 0));
            var twoThirdsRed = TextureFormats.Decode(TextureFormat.DXT1, block, 4, 2, 0);
            Assert.Equal(2f / 3f, twoThirdsRed.r, 4);
            Assert.Equal(1f / 3f, twoThirdsRed.b, 4);
        }

        [Fact]
        public void Texture2DLayoutPixelsAndMips()
        {
            var t = new Texture2D(4, 4);
            Assert.Equal(3, t.mipmapCount);
            Assert.Equal(16 * 4 + 4 * 4 + 4, t.GetRawTextureData().Length);

            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32((byte)(i * 16), 0, 0, 255);
            t.SetPixels32(pixels);
            // Repeat wrapping by default.
            Assert.Equal(t.GetPixel(3, 0), t.GetPixel(-1, 0));
            t.wrapMode = TextureWrapMode.Clamp;
            Assert.Equal(t.GetPixel(0, 0), t.GetPixel(-5, 0));

            t.Apply();
            // Level 1 texel (0, 0) averages texels (0..1, 0..1) of level 0: (0 + 16 + 64 + 80) / 4.
            Assert.Equal(40, ((Color32)t.GetPixel(0, 0, 1)).r);
        }

        [Fact]
        public void RawTextureDataAliasesTexels()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var raw = t.GetRawTextureData<byte>();
            raw[0] = 255;
            raw[3] = 255;
            Assert.Equal(1f, t.GetPixel(0, 0).r);
            Assert.Equal(4, t.GetPixelData<int>(0).Length);
        }

        [Fact]
        public void BilinearSamplesBetweenTexelCentres()
        {
            var t = new Texture2D(2, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32([new Color32(0, 0, 0, 255), new Color32(255, 0, 0, 255)]);
            Assert.Equal(0.5f, t.GetPixelBilinear(0.5f, 0.5f).r, 3);
            Assert.Equal(0f, t.GetPixelBilinear(0.25f, 0.5f).r, 3);
        }

        [Fact]
        public void NonReadableTexturesRejectPixelAccess()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.Apply(false, true);
            Assert.False(t.isReadable);
            Assert.Throws<UnityException>(() => t.GetPixel(0, 0));
        }

        [Fact]
        public void MeshValidatesIndicesAgainstVertices()
        {
            var mesh = new Mesh();
            mesh.vertices = [Vector3.zero, Vector3.right, Vector3.up];
            mesh.triangles = [0, 1, 2];
            Assert.Equal(new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f)), mesh.bounds);

            // Indices beyond the vertices are rejected and the mesh keeps its triangles.
            mesh.triangles = [0, 1, 3];
            Assert.Equal(new[] { 0, 1, 2 }, mesh.triangles);

            // So is shrinking the vertices below what the triangles reference.
            mesh.vertices = [Vector3.zero];
            Assert.Equal(3, mesh.vertexCount);

            // Other channels must match the vertex count.
            mesh.colors32 = [new Color32(1, 2, 3, 4)];
            Assert.Empty(mesh.colors32);
            mesh.colors32 = [new Color32(1, 2, 3, 4), new Color32(5, 6, 7, 8), new Color32(9, 10, 11, 12)];
            Assert.Equal(new Color32(5, 6, 7, 8), mesh.colors32[1]);

            mesh.Clear();
            Assert.Equal(0, mesh.vertexCount);
            Assert.Equal(1, mesh.subMeshCount);
        }

        [Fact]
        public void PropertySheetHashIgnoresInsertionOrder()
        {
            var a = new PropertySheet();
            a.SetFloat(Shader.PropertyToID("_A"), 1f);
            a.SetVector(Shader.PropertyToID("_B"), Vector4.one);
            var b = new PropertySheet();
            b.SetVector(Shader.PropertyToID("_B"), Vector4.one);
            b.SetFloat(Shader.PropertyToID("_A"), 1f);
            var ca = new Crc32();
            a.Hash(ca);
            var cb = new Crc32();
            b.Hash(cb);
            Assert.Equal(ca.Current, cb.Current);
            b.SetFloat(Shader.PropertyToID("_A"), 2f);
            var cc = new Crc32();
            b.Hash(cc);
            Assert.NotEqual(ca.Current, cc.Current);
        }

        [Fact]
        public void Crc32MatchesTheStandardCheckValue()
            => Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

        [Fact]
        public void PropertyBlockStoresColoursAsVectors()
        {
            var block = new MaterialPropertyBlock();
            Assert.True(block.isEmpty);
            int id = Shader.PropertyToID("_Tint");
            block.SetColor(id, new Color(1f, 0.5f, 0.25f, 1f));
            Assert.Equal(new Vector4(1f, 0.5f, 0.25f, 1f), block.GetVector(id));
            block.SetInt("_Count", 3);
            Assert.Equal(3f, block.GetFloat("_Count"));
        }
    }
}
