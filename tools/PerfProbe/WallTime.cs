using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: wall time of game methods inside the scripts' updates, which sampling
    /// profilers miss where the main thread waits (job workers, the native sim step). Enabled by a
    /// file `walltime.txt` in the mod's folder; the engine's --frame-stats gives the scripts' own
    /// totals. A patched method is recompiled without the runtime's profile-guided optimization,
    /// which slows a method with a hot loop of its own (U57 dropped from 54 to 38 fps with every
    /// entry point patched), so only thin methods and ones the tweaks patch already are timed.
    /// Once the colony is up, every 600 frames the probe logs per method its calls and wall time
    /// a frame (`PERF walltime NAME: CALLS a frame, MS ms a frame`), and the window's frame time.
    static class WallTime
    {
        static readonly (string Type, string Method)[] Points =
        {
            ("Game", "SimEveryTick"), ("Game", "UnsafeSim200ms"), ("Game", "StepTheSim"), ("World", "UpdateCellInfo"),
            ("Pathfinding", "UpdateNavGrids"), ("BrainScheduler", "RenderEveryTick"), ("AsyncPathProber+Manager", "TickFrame"),
            ("FetchManager", "UpdatePickups"), ("JobManager", "Run"), ("AnimEventManager", "Update"),
        };

        static readonly Dictionary<string, long[]> totals = new Dictionary<string, long[]>();

        /// walltime.txt itself: further methods to time, one `Type Method` a line (nested types as
        /// `Outer+Inner`; every overload of the name), as the overlays' entry points.
        public static string Extra;

        static void Begin(out long __state) => __state = Stopwatch.GetTimestamp();

        static void End(MethodBase __originalMethod, long __state)
        {
            if (!Game.IsOnMainThread()) return;
            string key = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name;
            if (!totals.TryGetValue(key, out var t)) totals[key] = t = new long[2];
            t[0]++;
            t[1] += Stopwatch.GetTimestamp() - __state;
        }

        public static IEnumerator Run(int windows)
        {
            var harmony = new Harmony("PerfProbe.WallTime");
            var begin = new HarmonyMethod(typeof(WallTime), nameof(Begin));
            var end = new HarmonyMethod(typeof(WallTime), nameof(End));
            var points = new List<(string Type, string Method)>(Points);
            if (Extra != null && System.IO.File.Exists(Extra))
                foreach (var line in System.IO.File.ReadAllLines(Extra))
                {
                    var words = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length == 2 && !words[0].StartsWith("#")) points.Add((words[0], words[1]));
                }
            foreach (var (type, method) in points)
            {
                var declaring = AccessTools.TypeByName(type);
                if (declaring == null) { UnityEngine.Debug.Log($"PERF walltime: no type {type}"); continue; }
                int patched = 0;
                foreach (var target in AccessTools.GetDeclaredMethods(declaring))
                    if (target.Name == method && target.GetMethodBody() != null && !target.ContainsGenericParameters)
                    {
                        try { harmony.Patch(target, prefix: begin, postfix: end); patched++; }
                        catch (System.Exception e) { UnityEngine.Debug.Log($"PERF walltime: {type}.{method} not patched: {e.Message}"); }
                    }
                if (patched == 0) UnityEngine.Debug.Log($"PERF walltime: no method {type}.{method}");
            }
            double ms = 1000.0 / Stopwatch.Frequency;
            for (int window = 0; window < windows; window++)
            {
                totals.Clear();
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 600; i++) yield return null;
                UnityEngine.Debug.Log($"PERF walltime window {window} f{Time.frameCount}: {(Stopwatch.GetTimestamp() - start) * ms / 600:F2} ms a frame");
                foreach (var (key, t) in totals)
                    UnityEngine.Debug.Log($"PERF walltime {key}: {t[0] / 600.0:F2} a frame, {t[1] * ms / 600:F3} ms a frame");
            }
            harmony.UnpatchAll("PerfProbe.WallTime");
        }
    }
}
