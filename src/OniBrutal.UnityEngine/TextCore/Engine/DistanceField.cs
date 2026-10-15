using System;

namespace OniBrutal.Engine.Text
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

        static double Candidate(double[] img, double[] gxImg, double[] gyImg, int w, int c, int xc, int yc, int xi, int yi)
        {
            int closest = c - xc - yc * w;
            if ((uint)closest >= (uint)img.Length) return Far;
            double a = Math.Clamp(img[closest], 0.0, 1.0);
            if (a == 0.0) return Far;
            double di = Math.Sqrt((double)xi * xi + (double)yi * yi);
            double df = di == 0 ? EdgeDistance(gxImg[closest], gyImg[closest], a) : EdgeDistance(xi, yi, a);
            return di + df;
        }

        // Neighbour offsets tested by each pass, as (dx, dy) to the neighbour.
        static readonly (int X, int Y)[] DownRight = { (-1, 0), (-1, -1), (0, -1), (1, -1) };
        static readonly (int X, int Y)[] DownLeft = { (1, 0) };
        static readonly (int X, int Y)[] UpLeft = { (1, 0), (1, 1), (0, 1), (-1, 1) };
        static readonly (int X, int Y)[] UpRight = { (-1, 0) };

        static void Sweep(double[] img, double[] gx, double[] gy, int w, int h, int[] distX, int[] distY, double[] dist)
        {
            const double Epsilon = 1e-3;
            bool changed;
            do
            {
                changed = false;
                for (int y = 1; y < h; y++)
                {
                    for (int x = 0; x < w; x++) changed |= Relax(x, y, DownRight);
                    for (int x = w - 2; x >= 0; x--) changed |= Relax(x, y, DownLeft);
                }
                for (int y = h - 2; y >= 0; y--)
                {
                    for (int x = w - 1; x >= 0; x--) changed |= Relax(x, y, UpLeft);
                    for (int x = 1; x < w; x++) changed |= Relax(x, y, UpRight);
                }
            }
            while (changed);

            bool Relax(int x, int y, (int X, int Y)[] neighbours)
            {
                int i = y * w + x;
                double old = dist[i];
                if (old <= 0) return false;
                bool any = false;
                foreach (var (nx, ny) in neighbours)
                {
                    int px = x + nx, py = y + ny;
                    if (px < 0 || px >= w || py < 0 || py >= h) continue;
                    int c = py * w + px;
                    int cx = distX[c], cy = distY[c];
                    // The candidate vector from this pixel to the neighbour's closest edge pixel.
                    int newX = cx - nx, newY = cy - ny;
                    double d = Candidate(img, gx, gy, w, c, cx, cy, newX, newY);
                    if (d < old - Epsilon)
                    {
                        distX[i] = newX;
                        distY[i] = newY;
                        dist[i] = d;
                        old = d;
                        any = true;
                    }
                }
                return any;
            }
        }
    }
}
