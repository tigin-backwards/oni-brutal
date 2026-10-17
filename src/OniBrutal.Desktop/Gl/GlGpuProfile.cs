using System.Collections.Generic;
using System.Linq;
using Brutal.OpenGlApi;
using N = Brutal.OpenGlApi.OpenGl.Native;

namespace OniBrutal.Desktop.Gl
{
    /// Diagnostics (--gpu-profile): GPU time by operation (draws by shader and pass, uploads,
    /// render texture work), from timestamp queries around each operation on the GL thread,
    /// read back three frames later and reported with the frame counters. Adds a query pair
    /// per operation; for measurement runs only.
    internal static unsafe class GlGpuProfile
    {
        public static bool Enabled;

        const uint Timestamp = 0x8E28, QueryResult = 0x8866;
        static readonly Stack<uint> free = new();
        static readonly Queue<(int Frame, List<(string Label, uint Start, uint End)> Ops)> pending = new();
        static List<(string Label, uint Start, uint End)> current = new();
        static readonly Dictionary<string, (double Ms, long Count)> totals = new();
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
            return q;
        }

        public static void End(string label, uint start)
        {
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

        public static string Take(int frames)
        {
            if (!Enabled || totals.Count == 0) return null;
            var top = totals.OrderByDescending(kv => kv.Value.Ms).Take(15)
                .Select(kv => $"{kv.Key} {kv.Value.Ms / frames:F2} ms ({kv.Value.Count / frames}/frame)").ToList();
            double all = totals.Sum(kv => kv.Value.Ms) / frames;
            totals.Clear();
            return $"gpu by operation (ms/frame over {frames}, total {all:F2}): " + string.Join("; ", top);
        }
    }
}
