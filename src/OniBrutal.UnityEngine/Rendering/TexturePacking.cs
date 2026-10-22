using System;
using System.Collections.Generic;
using OniBrutal.Engine;

namespace UnityEngine
{
    public partial class Texture2D
    {
        public Rect[] PackTextures(Texture2D[] textures, int padding) => PackTextures(textures, padding, 2048, false);

        public Rect[] PackTextures(Texture2D[] textures, int padding, int maximumAtlasSize) => PackTextures(textures, padding, maximumAtlasSize, false);

        /// Packs the textures into this one, as measured in the original player (LocalCanvas packs
        /// the player's images with it): largest area first, each with its padding, into a binary
        /// tree whose root is the atlas plus the padding (none is needed past the last column or
        /// row). The atlas starts at the smallest power-of-two size, wide before tall, whose area
        /// covers the padded textures; while they do not fit, its height doubles if it is at
        /// least as wide as tall, else its width. The atlas becomes RGBA32 with one level, readable
        /// unless asked otherwise, transparent black where no texture lands; the rects are in
        /// texture coordinates. Over the maximum size the textures are shrunk by 0.75 at a time
        /// until they fit (the first measured case; not every one: a 300x150 texture came out
        /// 112x112 where this gives 112x56), and that is logged.
        public Rect[] PackTextures(Texture2D[] textures, int padding, int maximumAtlasSize, bool makeNoLongerReadable)
        {
            if (textures == null) throw new ArgumentNullException(nameof(textures));
            var sizes = new (int W, int H)[textures.Length];
            for (int i = 0; i < textures.Length; i++) sizes[i] = (textures[i].width, textures[i].height);
            var order = new int[textures.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = ((long)sizes[b].W * sizes[b].H).CompareTo((long)sizes[a].W * sizes[a].H);
                return c != 0 ? c : a.CompareTo(b);
            });
            int width, height;
            (int X, int Y)[] places;
            bool shrunk = false;
            while (!TryPack(sizes, order, padding, maximumAtlasSize, out width, out height, out places))
            {
                shrunk = true;
                for (int i = 0; i < sizes.Length; i++) sizes[i] = (Math.Max(1, (int)(sizes[i].W * 0.75f)), Math.Max(1, (int)(sizes[i].H * 0.75f)));
            }
            if (shrunk) EngineLog.Engine($"PackTextures: {textures.Length} textures over {maximumAtlasSize} shrunk to fit (an approximation of Unity's scaling)");
            Reinitialize(width, height, TextureFormat.RGBA32, false);
            var atlas = new Color32[width * height];
            var rects = new Rect[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                var (w, h) = sizes[i];
                var (x, y) = places[i];
                var source = textures[i].GetPixels32(0);
                int sw = textures[i].width, sh = textures[i].height;
                for (int row = 0; row < h; row++)
                    for (int col = 0; col < w; col++)
                        atlas[(y + row) * width + x + col] = w == sw && h == sh ? source[row * sw + col] : Sample(source, sw, sh, (col + 0.5f) * sw / w - 0.5f, (row + 0.5f) * sh / h - 0.5f);
                rects[i] = new Rect((float)x / width, (float)y / height, (float)w / width, (float)h / height);
            }
            SetPixels32(atlas, 0);
            Apply(false, makeNoLongerReadable);
            return rects;
        }

        /// Bilinear sample at texel coordinates (measured: halving averages each pair).
        static Color32 Sample(Color32[] texels, int w, int h, float u, float v)
        {
            u = Mathf.Clamp(u, 0, w - 1);
            v = Mathf.Clamp(v, 0, h - 1);
            int x0 = (int)u, y0 = (int)v, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
            float fx = u - x0, fy = v - y0;
            Color32 Lerp(Color32 a, Color32 b, float t) => new(
                (byte)(a.r + (b.r - a.r) * t + 0.5f), (byte)(a.g + (b.g - a.g) * t + 0.5f), (byte)(a.b + (b.b - a.b) * t + 0.5f), (byte)(a.a + (b.a - a.a) * t + 0.5f));
            return Lerp(Lerp(texels[y0 * w + x0], texels[y0 * w + x1], fx), Lerp(texels[y1 * w + x0], texels[y1 * w + x1], fx), fy);
        }

        static bool TryPack((int W, int H)[] sizes, int[] order, int padding, int maximum, out int width, out int height, out (int X, int Y)[] places)
        {
            long area = 0;
            foreach (var (w, h) in sizes) area += (long)(w + padding) * (h + padding);
            width = 1;
            height = 1;
            while ((long)width * height < area)
            {
                if (width <= height) width *= 2;
                else height *= 2;
            }
            while (width <= maximum && height <= maximum)
            {
                places = new (int, int)[sizes.Length];
                var root = new PackNode(0, 0, width + padding, height + padding);
                bool all = true;
                foreach (int i in order)
                {
                    var node = root.Insert(sizes[i].W + padding, sizes[i].H + padding);
                    if (node == null) { all = false; break; }
                    places[i] = (node.X, node.Y);
                }
                if (all) return true;
                if (width >= height) height *= 2;
                else width *= 2;
            }
            places = null;
            return false;
        }

        /// A node of the packing tree (a free rectangle, or one split in two).
        sealed class PackNode(int x, int y, int w, int h)
        {
            public readonly int X = x, Y = y, W = w, H = h;
            PackNode first, second;
            bool used;

            public PackNode Insert(int w, int h)
            {
                if (first != null) return first.Insert(w, h) ?? second.Insert(w, h);
                if (used || w > W || h > H) return null;
                if (w == W && h == H)
                {
                    used = true;
                    return this;
                }
                int dw = W - w, dh = H - h;
                if (dw > dh)
                {
                    first = new PackNode(X, Y, w, H);
                    second = new PackNode(X + w, Y, dw, H);
                }
                else
                {
                    first = new PackNode(X, Y, W, h);
                    second = new PackNode(X, Y + h, W, dh);
                }
                return first.Insert(w, h);
            }
        }
    }
}
