using System.Diagnostics;

namespace OniBrutal.Desktop.Gl
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
}
