using System;
using System.Collections.Generic;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace OniMoono.Engine.Text
{
    /// Unity's atlas packer: the MaxRects bin packer (Jylänki), operating directly on the
    /// caller's free and used rectangle lists (TMP keeps them in the font asset between calls).
    /// Rotation is not used. Coordinates are texels with y up (texture rows bottom-up).
    internal static class GlyphPacker
    {
        /// Finds room for a width×height rectangle, records it as used and splits the free
        /// rectangles around it. False (lists untouched) when nothing fits.
        public static bool TryPack(int width, int height, GlyphPackingMode mode, int binWidth, int binHeight,
            List<GlyphRect> free, List<GlyphRect> used, out GlyphRect placed)
        {
            placed = FindPosition(width, height, mode, binWidth, binHeight, free, used);
            if (placed.height == 0) return false;
            Place(placed, free, used);
            return true;
        }

        static GlyphRect FindPosition(int width, int height, GlyphPackingMode mode, int binWidth, int binHeight, List<GlyphRect> free, List<GlyphRect> used)
        {
            var best = default(GlyphRect);
            int best1 = int.MaxValue, best2 = int.MaxValue;
            int bestContact = -1;
            foreach (var r in free)
            {
                if (r.width < width || r.height < height) continue;
                int leftoverH = Math.Abs(r.width - width), leftoverV = Math.Abs(r.height - height);
                int shortSide = Math.Min(leftoverH, leftoverV), longSide = Math.Max(leftoverH, leftoverV);
                switch (mode)
                {
                    case GlyphPackingMode.BestShortSideFit:
                        if (shortSide < best1 || (shortSide == best1 && longSide < best2))
                        {
                            best = new GlyphRect(r.x, r.y, width, height);
                            best1 = shortSide;
                            best2 = longSide;
                        }
                        break;
                    case GlyphPackingMode.BestLongSideFit:
                        if (longSide < best1 || (longSide == best1 && shortSide < best2))
                        {
                            best = new GlyphRect(r.x, r.y, width, height);
                            best1 = longSide;
                            best2 = shortSide;
                        }
                        break;
                    case GlyphPackingMode.BestAreaFit:
                    {
                        int area = r.width * r.height - width * height;
                        if (area < best1 || (area == best1 && shortSide < best2))
                        {
                            best = new GlyphRect(r.x, r.y, width, height);
                            best1 = area;
                            best2 = shortSide;
                        }
                        break;
                    }
                    case GlyphPackingMode.BottomLeftRule:
                    {
                        int top = r.y + height;
                        if (top < best1 || (top == best1 && r.x < best2))
                        {
                            best = new GlyphRect(r.x, r.y, width, height);
                            best1 = top;
                            best2 = r.x;
                        }
                        break;
                    }
                    case GlyphPackingMode.ContactPointRule:
                    {
                        int score = ContactScore(r.x, r.y, width, height, binWidth, binHeight, used);
                        if (score > bestContact)
                        {
                            best = new GlyphRect(r.x, r.y, width, height);
                            bestContact = score;
                        }
                        break;
                    }
                }
            }
            return best;
        }

        static int ContactScore(int x, int y, int width, int height, int binWidth, int binHeight, List<GlyphRect> used)
        {
            int score = 0;
            if (x == 0 || x + width == binWidth) score += height;
            if (y == 0 || y + height == binHeight) score += width;
            foreach (var u in used)
            {
                if (u.x == x + width || u.x + u.width == x) score += CommonInterval(u.y, u.y + u.height, y, y + height);
                if (u.y == y + height || u.y + u.height == y) score += CommonInterval(u.x, u.x + u.width, x, x + width);
            }
            return score;
        }

        static int CommonInterval(int a0, int a1, int b0, int b1) => a1 < b0 || b1 < a0 ? 0 : Math.Min(a1, b1) - Math.Max(a0, b0);

        static void Place(GlyphRect node, List<GlyphRect> free, List<GlyphRect> used)
        {
            int count = free.Count;
            for (int i = 0; i < count; i++)
            {
                if (Split(free[i], node, free))
                {
                    free.RemoveAt(i);
                    i--;
                    count--;
                }
            }
            Prune(free);
            used.Add(node);
        }

        static bool Split(GlyphRect f, GlyphRect u, List<GlyphRect> free)
        {
            if (u.x >= f.x + f.width || u.x + u.width <= f.x || u.y >= f.y + f.height || u.y + u.height <= f.y) return false;
            if (u.x < f.x + f.width && u.x + u.width > f.x)
            {
                if (u.y > f.y && u.y < f.y + f.height)
                    free.Add(new GlyphRect(f.x, f.y, f.width, u.y - f.y));
                if (u.y + u.height < f.y + f.height)
                    free.Add(new GlyphRect(f.x, u.y + u.height, f.width, f.y + f.height - (u.y + u.height)));
            }
            if (u.y < f.y + f.height && u.y + u.height > f.y)
            {
                if (u.x > f.x && u.x < f.x + f.width)
                    free.Add(new GlyphRect(f.x, f.y, u.x - f.x, f.height));
                if (u.x + u.width < f.x + f.width)
                    free.Add(new GlyphRect(u.x + u.width, f.y, f.x + f.width - (u.x + u.width), f.height));
            }
            return true;
        }

        static void Prune(List<GlyphRect> free)
        {
            for (int i = 0; i < free.Count; i++)
                for (int j = i + 1; j < free.Count; j++)
                {
                    if (Contains(free[j], free[i]))
                    {
                        free.RemoveAt(i);
                        i--;
                        break;
                    }
                    if (Contains(free[i], free[j]))
                    {
                        free.RemoveAt(j);
                        j--;
                    }
                }
        }

        static bool Contains(GlyphRect outer, GlyphRect inner) =>
            inner.x >= outer.x && inner.y >= outer.y
            && inner.x + inner.width <= outer.x + outer.width
            && inner.y + inner.height <= outer.y + outer.height;
    }
}
