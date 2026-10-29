using System.Diagnostics;
using System.Threading;

namespace OniMoono.Desktop.Gl
{
    /// Diagnostics (--frame-stats): GL-thread time by kind of work since the last frame counters,
    /// in Stopwatch ticks.
    internal static class GlTimings
    {
        public static long Busy, Draws, TextureUploads, MeshUploads, Targets, Present;

        public static long Now => Stopwatch.GetTimestamp();

        public static string Take(int frames)
        {
            static string Ms(long ticks, int frames) => (ticks * 1000.0 / Stopwatch.Frequency / frames).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var text = $"gl thread busy {Ms(Busy, frames)}: draws {Ms(Draws, frames)}, texture uploads {Ms(TextureUploads, frames)}, mesh uploads {Ms(MeshUploads, frames)}, targets {Ms(Targets, frames)}, present {Ms(Present, frames)}";
            Busy = Draws = TextureUploads = MeshUploads = Targets = Present = 0;
            return text;
        }
    }

    /// Diagnostics (--frame-stats): the presenter thread's time per picture it showed, in
    /// Stopwatch ticks, by step: waiting for the frame's commands, getting the target ready
    /// (DXGI: resizing, the interop lock), the copy into it, showing it (SwapBuffers; DXGI: the
    /// unlock, the copy into the back buffer and Present) and, with vsync, waiting until it is on
    /// its way to the display. Also the time between pictures shown: the mean and the mean of
    /// the slowest 1%. The presenter thread adds; the GL thread takes.
    internal static class PresenterTimings
    {
        public static long Pictures, FrameWait, Begin, Copy, Show, Wait;
        static readonly object gate = new();
        static readonly System.Collections.Generic.List<double> intervals = new();
        static long lastShown;

        /// Counted only with frame stats on (nothing reads them otherwise).
        static bool On => OniMoono.Engine.PlayerLoop.FrameStats;

        public static void Add(ref long counter, long ticks)
        {
            if (On) Interlocked.Add(ref counter, ticks);
        }

        /// A picture was shown (presenter thread).
        public static void Shown()
        {
            if (!On) return;
            Interlocked.Increment(ref Pictures);
            long now = Stopwatch.GetTimestamp();
            lock (gate)
            {
                if (lastShown != 0 && intervals.Count < 100_000) intervals.Add((now - lastShown) * 1000.0 / Stopwatch.Frequency);
                lastShown = now;
            }
        }

        public static string Take()
        {
            long n = Interlocked.Exchange(ref Pictures, 0);
            long frameWait = Interlocked.Exchange(ref FrameWait, 0), begin = Interlocked.Exchange(ref Begin, 0),
                copy = Interlocked.Exchange(ref Copy, 0), show = Interlocked.Exchange(ref Show, 0), wait = Interlocked.Exchange(ref Wait, 0);
            double[] sorted;
            lock (gate)
            {
                sorted = intervals.ToArray();
                intervals.Clear();
            }
            if (n == 0) return "presenter: nothing shown";
            static string F(double v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            string Ms(long ticks) => F(ticks * 1000.0 / Stopwatch.Frequency / n);
            var (mean, slowest) = MeanAndSlowest(sorted);
            return $"presenter per picture ({n}): frame wait {Ms(frameWait)}, begin {Ms(begin)}, copy {Ms(copy)}, show {Ms(show)}, vsync wait {Ms(wait)}; interval mean {F(mean)}, slowest 1% {F(slowest)}";
        }

        /// The mean of `values`, and the mean of their slowest 1% (at least one value); sorts
        /// them.
        internal static (double Mean, double Slowest) MeanAndSlowest(double[] values)
        {
            if (values.Length == 0) return (0, 0);
            System.Array.Sort(values);
            double sum = 0, worst = 0;
            foreach (var v in values) sum += v;
            int k = System.Math.Max(1, values.Length / 100);
            for (int i = values.Length - k; i < values.Length; i++) worst += values[i];
            return (sum / values.Length, worst / k);
        }
    }
}
