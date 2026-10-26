using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// Texture2D.PackTextures against the original player's layouts (PerfProbe's measure mode:
    /// "measure pack"): the atlas size and where each texture lands.
    public class TexturePackingTests : EngineTest
    {
        static (int, int)[] Many()
        {
            var sizes = new (int, int)[24];
            for (int i = 0; i < sizes.Length; i++) sizes[i] = (16 + i * 11 % 97, 16 + i * 29 % 113);
            return sizes;
        }

        static void Check((int W, int H)[] sizes, int padding, int width, int height, (int X, int Y)[] places)
        {
            var textures = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                textures[i] = new Texture2D(sizes[i].W, sizes[i].H, TextureFormat.RGBA32, false);
                var px = new Color32[sizes[i].W * sizes[i].H];
                for (int k = 0; k < px.Length; k++) px[k] = new Color32((byte)(10 + i * 9), 50, 90, 255);
                textures[i].SetPixels32(px);
                textures[i].Apply();
            }
            var atlas = new Texture2D(2, 2);
            var rects = atlas.PackTextures(textures, padding, 2048, false);
            Assert.Equal((width, height), (atlas.width, atlas.height));
            Assert.Equal(TextureFormat.RGBA32, atlas.format);
            Assert.Equal(1, atlas.mipmapCount);
            for (int i = 0; i < sizes.Length; i++)
            {
                Assert.Equal(new Rect((float)places[i].X / width, (float)places[i].Y / height, (float)sizes[i].W / width, (float)sizes[i].H / height), rects[i]);
                Assert.Equal(new Color32((byte)(10 + i * 9), 50, 90, 255), atlas.GetPixel(places[i].X + sizes[i].W / 2, places[i].Y + sizes[i].H / 2) is var c ? (Color32)c : default);
            }
            Assert.Equal(new Color(0, 0, 0, 0), atlas.GetPixel(width - 1, height - 1));
        }

        [Fact]
        public void PacksFourAsTheOriginal() => Check(new[] { (64, 64), (32, 32), (128, 16), (16, 128) }, 2, 128, 256, new[] { (0, 0), (66, 0), (0, 66), (0, 84) });
        [Fact]
        public void PacksFourNopadAsTheOriginal() => Check(new[] { (64, 64), (32, 32), (128, 16), (16, 128) }, 0, 128, 256, new[] { (0, 0), (64, 0), (0, 64), (0, 80) });
        [Fact]
        public void PacksOneAsTheOriginal() => Check(new[] { (100, 60) }, 2, 128, 64, new[] { (0, 0) });
        [Fact]
        public void PacksOddAsTheOriginal() => Check(new[] { (100, 60), (37, 81), (250, 13), (9, 9), (64, 200) }, 4, 256, 512, new[] { (68, 0), (68, 64), (0, 204), (172, 0), (0, 0) });
        [Fact]
        public void PacksManyAsTheOriginal() => Check(Many(), 2, 512, 256, new[] { (44, 222), (461, 0), (256, 164), (256, 59), (364, 30), (95, 205), (172, 0), (0, 0), (382, 121), (236, 170), (307, 170), (307, 59), (172, 226), (172, 170), (95, 120), (0, 108), (364, 0), (256, 0), (382, 145), (349, 59), (0, 222), (382, 59), (172, 79), (95, 0) });
        [Fact]
        public void PacksSameAsTheOriginal() => Check(new[] { (64, 64), (64, 64), (64, 64), (64, 64), (64, 64) }, 1, 256, 256, new[] { (0, 0), (65, 0), (130, 0), (0, 65), (0, 130) });
    }
}
