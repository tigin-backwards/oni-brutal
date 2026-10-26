using System;

namespace OniMoono.Engine.Text
{
    /// Signed distance fields for the SDF glyph render modes.
    ///
    /// The distance transform is Gustavson's anti-aliased Euclidean distance transform
    /// ("edtaa3": 8-neighbour sequential sweeps propagating closest-edge vectors, with the
    /// sub-pixel edge position estimated from the coverage value and its gradient). On a
    /// binary (mono) raster it reduces to the classic 8SSEDT.
    ///
    /// Encoding follows what TextMeshPro's SDF shaders expect: the field is 0.5 on the outline,
    /// decreasing outwards by 1/(2·spread) per texel, with spread = atlas padding + 1 (the
    /// _GradientScale TMP writes for SDF atlases), clamped to [0, 1].
    internal static class DistanceField
    {
        const double Far = 1000000.0;

        /// Converts coverage (0..1, `width`×`height`, row-major, any row order) sampled at
        /// `scale`× the output resolution into an 8-bit field of (width/scale)×(height/scale):
        /// each output texel is the mean signed distance of its scale×scale block, in output
        /// texels. Both dimensions must be multiples of `scale`.
        public static byte[] Generate(double[] coverage, int width, int height, int scale, float spread)
        {
            var outside = Transform(coverage, width, height, invert: false);
            var inside = Transform(coverage, width, height, invert: true);
            int ow = width / scale, oh = height / scale;
            var result = new byte[ow * oh];
            double norm = 1.0 / (scale * scale);
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    double sum = 0;
                    for (int j = 0; j < scale; j++)
                    {
                        int row = (y * scale + j) * width + x * scale;
                        for (int i = 0; i < scale; i++)
                        {
                            double o = Math.Max(outside[row + i], 0.0), n = Math.Max(inside[row + i], 0.0);
                            sum += o - n;
                        }
                    }
                    double d = sum * norm / scale;
                    double v = 0.5 - d / (2.0 * spread);
                    result[y * ow + x] = (byte)Math.Clamp((int)Math.Round(v * 255.0), 0, 255);
                }
            return result;
        }

        /// Distance (in input pixels) from each pixel to the shape (coverage > 0); `invert`
        /// measures to the background instead.
        static double[] Transform(double[] coverage, int w, int h, bool invert)
        {
            int n = w * h;
            var img = new double[n];
            for (int i = 0; i < n; i++) img[i] = invert ? 1.0 - coverage[i] : coverage[i];
            var gx = new double[n];
            var gy = new double[n];
            ComputeGradient(img, w, h, gx, gy);
            var dist = new double[n];
            var distX = new int[n];
            var distY = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (img[i] <= 0.0) dist[i] = Far;
                else if (img[i] < 1.0) dist[i] = EdgeDistance(gx[i], gy[i], img[i]);
                else dist[i] = 0.0;
            }
            Sweep(img, gx, gy, w, h, distX, distY, dist);
            return dist;
        }

        static void ComputeGradient(double[] img, int w, int h, double[] gx, double[] gy)
        {
            const double Sqrt2 = 1.4142136;
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int k = y * w + x;
                    if (img[k] <= 0.0 || img[k] >= 1.0) continue;
                    double ax = -img[k - w - 1] - Sqrt2 * img[k - 1] - img[k + w - 1] + img[k - w + 1] + Sqrt2 * img[k + 1] + img[k + w + 1];
                    double ay = -img[k - w - 1] - Sqrt2 * img[k - w] - img[k - w + 1] + img[k + w - 1] + Sqrt2 * img[k + w] + img[k + w + 1];
                    double length = ax * ax + ay * ay;
                    if (length > 0.0)
                    {
                        length = Math.Sqrt(length);
                        ax /= length;
                        ay /= length;
                    }
                    gx[k] = ax;
                    gy[k] = ay;
                }
        }

        /// Distance from a pixel centre to the edge through it, from its coverage `a` and the
        /// edge normal (gx, gy).
        static double EdgeDistance(double gx, double gy, double a)
        {
            if (gx == 0 || gy == 0) return 0.5 - a;
            double length = Math.Sqrt(gx * gx + gy * gy);
            if (length > 0)
            {
                gx /= length;
                gy /= length;
            }
            gx = Math.Abs(gx);
            gy = Math.Abs(gy);
            if (gx < gy) (gx, gy) = (gy, gx);
            double a1 = 0.5 * gy / gx;
            if (a < a1) return 0.5 * (gx + gy) - Math.Sqrt(2.0 * gx * gy * a);
            if (a < 1.0 - a1) return (0.5 - a) * gx;
            return -0.5 * (gx + gy) + Math.Sqrt(2.0 * gx * gy * (1.0 - a));
        }

        const double Epsilon = 1e-3;

        // A candidate is at least its vector's length minus this: EdgeDistance is never below
        // -(gx + gy) / 2 for a unit normal, -√2/2.
        const double MaxEdgeShortening = 0.75;

        /// Sequential sweeps until no pixel changes: down the rows (each row left to right
        /// against its left, upper-left, upper and upper-right neighbours, then right to left
        /// against its right one), then up the rows mirrored. A neighbour proposes its closest
        /// edge pixel; it is taken when it is closer by more than Epsilon.
        ///
        /// A pixel's step depends only on its own state and its neighbours' in that pass, and
        /// taking it twice in a row changes nothing (each candidate the second time is no
        /// closer by more than Epsilon than the result). So from the second sweep on, a pixel
        /// whose state and neighbours have not changed since its step one sweep earlier is
        /// skipped: every step has a time, and each pixel the time of its last change.
        static unsafe void Sweep(double[] img, double[] gx, double[] gy, int w, int h, int[] distX, int[] distY, double[] dist)
        {
            var changedAt = new int[img.Length];
            fixed (double* pImg = img, pGx = gx, pGy = gy, pDist = dist)
            fixed (int* pX = distX, pY = distY, at = changedAt)
            {
                var s = new State { Img = pImg, Gx = pGx, Gy = pGy, Dist = pDist, X = pX, Y = pY, W = w, N = img.Length };
                // The current step's time; `before` is the time of the same step a sweep earlier
                // (none in the first sweep, nor once the times would overflow).
                int t = 0, stepsPerSweep = 0;
                bool skip = false, changed;
                do
                {
                    changed = false;
                    for (int y = 1; y < h; y++)
                    {
                        int row = y * w;
                        for (int x = 0; x < w; x++)
                        {
                            int i = row + x, before = ++t - stepsPerSweep;
                            if (s.Dist[i] <= 0) continue;
                            if (skip && at[i] <= before && at[i - w] <= before && (x == 0 || (at[i - 1] <= before && at[i - w - 1] <= before)) && (x == w - 1 || at[i - w + 1] <= before)) continue;
                            bool any = false;
                            if (x > 0) any |= s.Relax(i, i - 1, -1, 0);
                            if (x > 0) any |= s.Relax(i, i - w - 1, -1, -1);
                            any |= s.Relax(i, i - w, 0, -1);
                            if (x < w - 1) any |= s.Relax(i, i - w + 1, 1, -1);
                            if (any) { at[i] = t; changed = true; }
                        }
                        for (int x = w - 2; x >= 0; x--)
                        {
                            int i = row + x, before = ++t - stepsPerSweep;
                            if (s.Dist[i] <= 0 || (skip && at[i] <= before && at[i + 1] <= before)) continue;
                            if (s.Relax(i, i + 1, 1, 0)) { at[i] = t; changed = true; }
                        }
                    }
                    for (int y = h - 2; y >= 0; y--)
                    {
                        int row = y * w;
                        for (int x = w - 1; x >= 0; x--)
                        {
                            int i = row + x, before = ++t - stepsPerSweep;
                            if (s.Dist[i] <= 0) continue;
                            if (skip && at[i] <= before && at[i + w] <= before && (x == w - 1 || (at[i + 1] <= before && at[i + w + 1] <= before)) && (x == 0 || at[i + w - 1] <= before)) continue;
                            bool any = false;
                            if (x < w - 1) any |= s.Relax(i, i + 1, 1, 0);
                            if (x < w - 1) any |= s.Relax(i, i + w + 1, 1, 1);
                            any |= s.Relax(i, i + w, 0, 1);
                            if (x > 0) any |= s.Relax(i, i + w - 1, -1, 1);
                            if (any) { at[i] = t; changed = true; }
                        }
                        for (int x = 1; x < w; x++)
                        {
                            int i = row + x, before = ++t - stepsPerSweep;
                            if (s.Dist[i] <= 0 || (skip && at[i] <= before && at[i - 1] <= before)) continue;
                            if (s.Relax(i, i - 1, -1, 0)) { at[i] = t; changed = true; }
                        }
                    }
                    if (stepsPerSweep == 0)
                    {
                        stepsPerSweep = t;
                        skip = true;
                    }
                    // Times stay below int.MaxValue through the next sweep, or skipping stops.
                    skip &= (long)t + stepsPerSweep < int.MaxValue;
                }
                while (changed);
            }
        }

        unsafe struct State
        {
            public double* Img, Gx, Gy, Dist;
            public int* X, Y;
            public int W, N;

            /// Pixel i against neighbour c at offset (nx, ny): the vector from i to c's closest
            /// edge pixel, taken when it gives a distance shorter by more than Epsilon. Far
            /// candidates (no edge pixel) are never taken, and one whose length alone rules it
            /// out is not evaluated further.
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Relax(int i, int c, int nx, int ny)
            {
                int cx = X[c], cy = Y[c];
                int closest = c - cx - cy * W;
                if ((uint)closest >= (uint)N) return false;
                double a = Math.Clamp(Img[closest], 0.0, 1.0);
                if (a == 0.0) return false;
                int newX = cx - nx, newY = cy - ny;
                double limit = Dist[i] - Epsilon;
                double di = Math.Sqrt((double)newX * newX + (double)newY * newY);
                if (di - MaxEdgeShortening >= limit) return false;
                double df = di == 0 ? EdgeDistance(Gx[closest], Gy[closest], a) : EdgeDistance(newX, newY, a);
                double d = di + df;
                if (!(d < limit)) return false;
                X[i] = newX;
                Y[i] = newY;
                Dist[i] = d;
                return true;
            }
        }
    }
}
