using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: `cameras.txt` beside the probe (its first word: how many 600-frame
    /// windows, 3 by default): the main thread's time in each camera's render, from its
    /// Camera.onPreCull to its Camera.onPostRender (culling, its draws' recording and the
    /// scripts' camera callbacks; image effects after it are left out), by camera name:
    /// `PERF camera NAME: CALLS a frame, MS ms a frame`.
    static class Cameras
    {
        static readonly Dictionary<Camera, long> started = new Dictionary<Camera, long>();
        static readonly Dictionary<string, long[]> totals = new Dictionary<string, long[]>();

        static void PreCull(Camera camera) => started[camera] = Stopwatch.GetTimestamp();

        static void PostRender(Camera camera)
        {
            if (!started.TryGetValue(camera, out long start)) return;
            started.Remove(camera);
            if (!totals.TryGetValue(camera.name, out var t)) totals[camera.name] = t = new long[2];
            t[0]++;
            t[1] += Stopwatch.GetTimestamp() - start;
        }

        public static IEnumerator Run(string path)
        {
            var words = System.IO.File.ReadAllText(path).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            int windows = words.Length > 0 ? int.Parse(words[0]) : 3;
            for (int i = 0; i < 600; i++) yield return null;
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
            double ms = 1000.0 / Stopwatch.Frequency;
            for (int w = 0; w < windows; w++)
            {
                totals.Clear();
                for (int i = 0; i < 600; i++) yield return null;
                foreach (var (name, t) in totals)
                    UnityEngine.Debug.Log($"PERF camera {name} window {w}: {t[0] / 600.0:F2} a frame, {t[1] * ms / 600:F3} ms a frame");
            }
            Camera.onPreCull -= PreCull;
            Camera.onPostRender -= PostRender;
        }
    }
}
