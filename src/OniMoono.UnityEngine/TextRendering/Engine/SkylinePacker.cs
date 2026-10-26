using System.Collections.Generic;

namespace OniMoono.Engine.Text
{
    /// Places a legacy font's glyph rects in its texture: a skyline, bottom-left first (the
    /// lowest top, then the leftmost). Each placement costs the number of skyline segments, where
    /// the TextCore packer's MaxRects pays for all its free rects (seconds for a texture of a
    /// few thousand glyphs). Where a glyph lands is not the original's (its texture coordinates
    /// are not matched), only that it is in the texture.
    internal sealed class SkylinePacker
    {
        struct Segment
        {
            public int X, Y, Width;
        }

        readonly List<Segment> skyline = new();
        int width, height;

        public void Reset(int binWidth, int binHeight)
        {
            width = binWidth;
            height = binHeight;
            skyline.Clear();
            skyline.Add(new Segment { X = 0, Y = 0, Width = binWidth });
        }

        /// The bottom-left corner of room for a w × h rect, which is then taken; false when none.
        public bool TryPack(int w, int h, out int x, out int y)
        {
            x = y = 0;
            if (w <= 0 || h <= 0 || w > width || h > height) return false;
            int best = -1, bestTop = int.MaxValue, bestX = int.MaxValue;
            for (int i = 0; i < skyline.Count; i++)
            {
                int top = Fit(i, w, h);
                if (top < 0) continue;
                if (top + h < bestTop || (top + h == bestTop && skyline[i].X < bestX))
                {
                    best = i;
                    bestTop = top + h;
                    bestX = skyline[i].X;
                }
            }
            if (best < 0) return false;
            x = skyline[best].X;
            y = bestTop - h;
            Take(best, x, bestTop, w);
            return true;
        }

        /// The height a w × h rect starting at segment i would rest at; −1 when it does not fit.
        int Fit(int i, int w, int h)
        {
            int x = skyline[i].X;
            if (x + w > width) return -1;
            int left = w, y = 0;
            for (int j = i; left > 0; j++)
            {
                if (j == skyline.Count) return -1;
                if (skyline[j].Y > y) y = skyline[j].Y;
                if (y + h > height) return -1;
                left -= skyline[j].Width;
            }
            return y;
        }

        void Take(int i, int x, int top, int w)
        {
            skyline.Insert(i, new Segment { X = x, Y = top, Width = w });
            int end = x + w;
            for (int j = i + 1; j < skyline.Count;)
            {
                var s = skyline[j];
                if (s.X >= end) break;
                int cut = end - s.X;
                if (cut >= s.Width)
                {
                    skyline.RemoveAt(j);
                    continue;
                }
                s.X += cut;
                s.Width -= cut;
                skyline[j] = s;
                break;
            }
            // Merge neighbours at the same height.
            for (int j = 0; j + 1 < skyline.Count;)
            {
                if (skyline[j].Y == skyline[j + 1].Y)
                {
                    var s = skyline[j];
                    s.Width += skyline[j + 1].Width;
                    skyline[j] = s;
                    skyline.RemoveAt(j + 1);
                }
                else j++;
            }
        }
    }
}
