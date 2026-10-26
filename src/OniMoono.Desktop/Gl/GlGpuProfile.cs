using System.Collections.Generic;
using System.Linq;
using Brutal.OpenGlApi;
using N = Brutal.OpenGlApi.OpenGl.Native;

namespace OniMoono.Desktop.Gl
{
    /// Diagnostics (--gpu-profile): time by operation (draws by shader and pass, uploads, render
    /// texture work) on the GL thread's CPU, measured directly, and on the GPU, from timestamp
    /// queries around each operation read back three frames later (some drivers return equal
    /// timestamps within a batch: Mesa's D3D12 backend under WSL), reported with the frame
    /// counters, by GL thread time. Adds a query pair per operation; for measurement runs only.
    internal static unsafe class GlGpuProfile
    {
        public static bool Enabled;

        const uint Timestamp = 0x8E28, QueryResult = 0x8866;
        static readonly Stack<uint> free = new();
        static readonly Queue<(int Frame, List<(string Label, uint Start, uint End)> Ops)> pending = new();
        static List<(string Label, uint Start, uint End)> current = new();
        static readonly Dictionary<string, (double Ms, long Count)> totals = new();
        static readonly Dictionary<string, double> cpuTotals = new();
        static readonly Stack<long> started = new();
        static int frame;

        static uint Query()
        {
            if (free.Count > 0) return free.Pop();
            uint q;
            N.glGenQueries(1, (GlQuery*)&q);
            return q;
        }

        /// Marks the start of an operation; End with the returned token.
        public static uint Begin()
        {
            uint q = Query();
            N.glQueryCounter(q, Timestamp);
            started.Push(System.Diagnostics.Stopwatch.GetTimestamp());
            return q;
        }

        public static void End(string label, uint start)
        {
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - started.Pop();
            cpuTotals[label] = cpuTotals.GetValueOrDefault(label) + ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            uint q = Query();
            N.glQueryCounter(q, Timestamp);
            current.Add((label, start, q));
        }

        /// At present: queues this frame's queries and collects those of three frames ago.
        public static void FrameEnd()
        {
            pending.Enqueue((frame++, current));
            current = new();
            while (pending.Count > 3)
            {
                var (_, ops) = pending.Dequeue();
                foreach (var (label, s, e) in ops)
                {
                    ulong t0 = 0, t1 = 0;
                    N.glGetQueryObjectui64v(s, QueryResult, &t0);
                    N.glGetQueryObjectui64v(e, QueryResult, &t1);
                    var (ms, count) = totals.GetValueOrDefault(label);
                    totals[label] = (ms + (t1 - t0) / 1e6, count + 1);
                    free.Push(s);
                    free.Push(e);
                }
            }
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, byte[]> lastBytes = new();
        static readonly Dictionary<string, (long Same, long All)> repeats = new();

        /// Counts a 2D upload whose texels equal the texture's previous upload exactly.
        public static void CountRepeat(object texture, string label, System.ReadOnlySpan<byte> texels)
        {
            bool had = lastBytes.TryGetValue(texture, out var last);
            bool same = had && System.MemoryExtensions.SequenceEqual(texels, (System.ReadOnlySpan<byte>)last);
            // Rows of 4 KiB that changed since the texture's previous upload, and the span from the
            // first to the last changed one.
            if (had && !same && last.Length == texels.Length)
            {
                const int Row = 4096;
                int rows = (texels.Length + Row - 1) / Row, changed = 0, first = -1, lastRow = -1;
                for (int r = 0; r < rows; r++)
                {
                    int o = r * Row, n = System.Math.Min(Row, texels.Length - o);
                    if (!System.MemoryExtensions.SequenceEqual(texels.Slice(o, n), new System.ReadOnlySpan<byte>(last, o, n)))
                    {
                        changed++;
                        if (first < 0) first = r;
                        lastRow = r;
                    }
                }
                var (c, sp, t) = rowChanges.GetValueOrDefault(label);
                rowChanges[label] = (c + changed, sp + (lastRow - first + 1), t + rows);
            }
            if (!same) lastBytes.AddOrUpdate(texture, texels.ToArray());
            var (s, a) = repeats.GetValueOrDefault(label);
            repeats[label] = (s + (same ? 1 : 0), a + 1);
        }

        static readonly Dictionary<string, (long Changed, long Span, long Rows)> rowChanges = new();

        public static string Take(int frames)
        {
            if (!Enabled || totals.Count == 0) return null;
            var top = totals.OrderByDescending(kv => cpuTotals.GetValueOrDefault(kv.Key)).Take(15)
                .Select(kv => $"{kv.Key} {cpuTotals.GetValueOrDefault(kv.Key) / frames:F2}/{kv.Value.Ms / frames:F2} ms ({kv.Value.Count / frames}/frame)").ToList();
            double all = totals.Sum(kv => kv.Value.Ms) / frames, allCpu = cpuTotals.Values.Sum() / frames;
            totals.Clear();
            cpuTotals.Clear();
            var repeated = repeats.Where(kv => kv.Value.All >= frames).OrderByDescending(kv => kv.Value.All)
                .Select(kv => $"{kv.Key} {kv.Value.Same}/{kv.Value.All}").ToList();
            repeats.Clear();
            var rowsChanged = rowChanges.Where(kv => kv.Value.Rows >= 64 * frames).Select(kv => $"{kv.Key} {100.0 * kv.Value.Changed / kv.Value.Rows:F0}% changed, {100.0 * kv.Value.Span / kv.Value.Rows:F0}% first to last").ToList();
            rowChanges.Clear();
            if (rowsChanged.Count > 0) top.Add("4 KiB rows of changed uploads: " + string.Join(", ", rowsChanged));
            if (repeated.Count > 0) top.Add("uploads repeating the previous texels: " + string.Join(", ", repeated));
            return $"by operation (GL thread/GPU ms a frame over {frames}, totals {allCpu:F2}/{all:F2}): " + string.Join("; ", top);
        }
    }
}
