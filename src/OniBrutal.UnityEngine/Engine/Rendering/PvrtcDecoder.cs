using System;
using System.Buffers.Binary;
using UnityEngine;

namespace OniBrutal.Engine.Rendering
{
    /// Texel reads from PVRTC (version 1) textures, 2 and 4 bits per texel (GetPixel on
    /// compressed readable textures, which Unity decodes on the CPU), following the format's
    /// published decompression: words of 64 bits in Morton order, each holding two colours and
    /// per-texel modulation; a texel blends the two colours, each upscaled bilinearly from the
    /// four words around it. Unlike block formats a texel depends on its neighbours' words, so
    /// reads take the whole level.
    internal static class PvrtcDecoder
    {
        /// Words across and down a level: at least two each way (levels are padded to 16×8 at
        /// 2 bits per texel, 8×8 at 4).
        public static (int X, int Y) Words(int width, int height, bool twoBpp)
            => (Math.Max((width + (twoBpp ? 7 : 3)) / (twoBpp ? 8 : 4), 2), Math.Max((height + 3) / 4, 2));

        public static Color Decode(ReadOnlySpan<byte> level, int width, int x, int y, bool twoBpp, bool hasAlpha)
        {
            int ww = twoBpp ? 8 : 4, wh = 4;
            int wordsX = Words(width, 1, twoBpp).X;
            int wordsY = level.Length / 8 / wordsX;
            int texelsX = wordsX * ww, texelsY = wordsY * wh;

            // The 2×2 words whose colours this texel blends: the texel lies between their centres.
            int rx = Mod(x - ww / 2, texelsX), ry = Mod(y - wh / 2, texelsY);
            int px = rx / ww, py = ry / wh, lx = rx % ww, ly = ry % wh;
            uint cp = ColourData(level, px, py, wordsX, wordsY), cq = ColourData(level, px + 1, py, wordsX, wordsY);
            uint cr = ColourData(level, px, py + 1, wordsX, wordsY), cs = ColourData(level, px + 1, py + 1, wordsX, wordsY);
            Span<int> a = stackalloc int[4], b = stackalloc int[4];
            Upscale(ColourA(cp), ColourA(cq), ColourA(cr), ColourA(cs), lx, ly, ww, wh, a);
            Upscale(ColourB(cp), ColourB(cq), ColourB(cr), ColourB(cs), lx, ly, ww, wh, b);

            int modulation = Modulation(level, x, y, wordsX, wordsY, twoBpp);
            bool punchThrough = modulation > 10;
            if (punchThrough) modulation -= 10;
            Span<float> c = stackalloc float[4];
            for (int i = 0; i < 4; i++) c[i] = (a[i] * (8 - modulation) + b[i] * modulation) / 8 / 255f;
            float alpha = !hasAlpha ? 1f : punchThrough ? 0f : c[3];
            return new Color(c[0], c[1], c[2], alpha);
        }

        static int Mod(int v, int m) => ((v % m) + m) % m;

        /// A word's offset: Morton order over the smaller dimension (y in the even bits), the larger
        /// dimension's remaining bits above.
        static int WordOffset(int wx, int wy, int wordsX, int wordsY)
        {
            wx = Mod(wx, wordsX);
            wy = Mod(wy, wordsY);
            int minimum = Math.Min(wordsX, wordsY);
            int twiddled = 0, shift = 0;
            for (int bit = 1; bit < minimum; bit <<= 1, shift++)
            {
                if ((wy & bit) != 0) twiddled |= 1 << (2 * shift);
                if ((wx & bit) != 0) twiddled |= 2 << (2 * shift);
            }
            int rest = wordsX >= wordsY ? wx : wy;
            return (twiddled | ((rest >> shift) << (2 * shift))) * 8;
        }

        static uint ModulationData(ReadOnlySpan<byte> level, int wx, int wy, int wordsX, int wordsY)
            => BinaryPrimitives.ReadUInt32LittleEndian(level[WordOffset(wx, wy, wordsX, wordsY)..]);

        static uint ColourData(ReadOnlySpan<byte> level, int wx, int wy, int wordsX, int wordsY)
            => BinaryPrimitives.ReadUInt32LittleEndian(level[(WordOffset(wx, wy, wordsX, wordsY) + 4)..]);

        /// Colour A: RGB 5:5:4 when opaque, ARGB 3:4:4:3 when translucent; as 5-bit RGB, 4-bit alpha.
        static (int R, int G, int B, int A) ColourA(uint c)
        {
            if ((c & 0x8000) != 0)
                return ((int)((c & 0x7C00) >> 10), (int)((c & 0x3E0) >> 5), (int)((c & 0x1E) | ((c & 0x1E) >> 4)), 0xF);
            return ((int)(((c & 0xF00) >> 7) | ((c & 0xF00) >> 11)), (int)(((c & 0xF0) >> 3) | ((c & 0xF0) >> 7)),
                (int)(((c & 0xE) << 1) | ((c & 0xE) >> 2)), (int)((c & 0x7000) >> 11));
        }

        /// Colour B: RGB 5:5:5 when opaque, ARGB 3:4:4:4 when translucent.
        static (int R, int G, int B, int A) ColourB(uint c)
        {
            if ((c & 0x80000000) != 0)
                return ((int)((c & 0x7C000000) >> 26), (int)((c & 0x3E00000) >> 21), (int)((c & 0x1F0000) >> 16), 0xF);
            return ((int)(((c & 0xF000000) >> 23) | ((c & 0xF000000) >> 27)), (int)(((c & 0xF00000) >> 19) | ((c & 0xF00000) >> 23)),
                (int)(((c & 0xF0000) >> 15) | ((c & 0xF0000) >> 19)), (int)((c & 0x70000000) >> 27));
        }

        /// Bilinear blend of the four words' colours at (lx, ly) from P's centre, widened to 8 bits.
        static void Upscale((int R, int G, int B, int A) p, (int R, int G, int B, int A) q, (int R, int G, int B, int A) r, (int R, int G, int B, int A) s,
            int lx, int ly, int ww, int wh, Span<int> output)
        {
            int Blend(int pv, int qv, int rv, int sv)
            {
                int top = ww * pv + lx * (qv - pv), bottom = ww * rv + lx * (sv - rv);
                return wh * top + ly * (bottom - top);
            }
            // The blend is the value scaled by ww·wh (16 or 32); 5-bit colour and 4-bit alpha widen to 8 bits.
            int colourShift = ww * wh == 32 ? 7 : 6, colourShift2 = ww * wh == 32 ? 2 : 1;
            int alphaShift = ww * wh == 32 ? 5 : 4, alphaShift2 = ww * wh == 32 ? 1 : 0;
            int red = Blend(p.R, q.R, r.R, s.R), green = Blend(p.G, q.G, r.G, s.G), blue = Blend(p.B, q.B, r.B, s.B), alpha = Blend(p.A, q.A, r.A, s.A);
            output[0] = (red >> colourShift) + (red >> colourShift2);
            output[1] = (green >> colourShift) + (green >> colourShift2);
            output[2] = (blue >> colourShift) + (blue >> colourShift2);
            output[3] = (alpha >> alphaShift) + (alpha >> alphaShift2);
        }

        static readonly int[] TwoBitValues = [0, 3, 5, 8];

        /// The texel's modulation, 0 to 8 (8: all colour B); 10 is added for punch-through texels.
        static int Modulation(ReadOnlySpan<byte> level, int x, int y, int wordsX, int wordsY, bool twoBpp)
        {
            if (!twoBpp)
            {
                int wx = x / 4, wy = y / 4, i = (y & 3) * 4 + (x & 3);
                int bits = (int)((ModulationData(level, wx, wy, wordsX, wordsY) >> (2 * i)) & 3);
                if ((ColourData(level, wx, wy, wordsX, wordsY) & 1) != 0) return bits switch { 0 => 0, 1 => 4, 2 => 14, _ => 8 };
                return bits switch { 0 => 0, 1 => 3, 2 => 5, _ => 8 };
            }
            var (mode, value) = TwoBppTexel(level, x, y, wordsX, wordsY);
            if (mode == 0 || ((x ^ y) & 1) == 0) return TwoBitValues[value];
            int up = Neighbour(level, x, y - 1, wordsX, wordsY), down = Neighbour(level, x, y + 1, wordsX, wordsY);
            int left = Neighbour(level, x - 1, y, wordsX, wordsY), right = Neighbour(level, x + 1, y, wordsX, wordsY);
            return mode switch
            {
                1 => (up + down + left + right + 2) / 4,
                2 => (left + right + 1) / 2,
                _ => (up + down + 1) / 2,
            };
        }

        /// A neighbouring stored texel's modulation (the level wraps).
        static int Neighbour(ReadOnlySpan<byte> level, int x, int y, int wordsX, int wordsY)
            => TwoBitValues[TwoBppTexel(level, Mod(x, wordsX * 8), Mod(y, wordsY * 4), wordsX, wordsY).Value];

        /// A 2-bit-per-texel word's mode for its texels (0: one bit each; 1: stored texels in a
        /// checkerboard, the others averaged from all four neighbours; 2: from horizontal ones;
        /// 3: from vertical ones) and the texel's stored value (an index into TwoBitValues).
        static (int Mode, int Value) TwoBppTexel(ReadOnlySpan<byte> level, int x, int y, int wordsX, int wordsY)
        {
            int wx = x / 8, wy = y / 4, lx = x & 7, ly = y & 3;
            uint bits = ModulationData(level, wx, wy, wordsX, wordsY);
            if ((ColourData(level, wx, wy, wordsX, wordsY) & 1) == 0)
                return (0, ((bits >> (ly * 8 + lx)) & 1) != 0 ? 3 : 0);
            int mode = 1;
            if ((bits & 1) != 0)
            {
                mode = (bits & (1u << 20)) != 0 ? 3 : 2;
                // The centre texel's second bit doubles as the mode bit; its value repeats bit 21.
                bits = (bits & (1u << 21)) != 0 ? bits | (1u << 20) : bits & ~(1u << 20);
            }
            bits = (bits & 2) != 0 ? bits | 1 : bits & ~1u;
            // Stored texels, in row order, two bits each.
            int index = 0;
            for (int ty = 0; ty < 4; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    if (((tx ^ ty) & 1) != 0) continue;
                    if (tx == lx && ty == ly) return (mode, (int)((bits >> (2 * index)) & 3));
                    index++;
                }
            return (mode, 0);
        }
    }
}
