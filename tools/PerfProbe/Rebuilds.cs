using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: `rebuild.txt` beside the probe: the pipe and conveyor networks'
    /// rebuilds timed, from the load on. Every rebuild of the conveyor rails' flow
    /// (SolidConduitFlow.OnUtilityNetworksRebuilt) and of the pipes' (ConduitFlow's) logs its
    /// wall time, and for the rails the part spent keeping the paths (TryAdd) and how many there
    /// are: `PERF rebuild NAME f# MS ms (TryAdd CALLS calls MS ms, PATHS paths)`. Once the colony
    /// is up, each network kind is rebuilt again `N` times (the file's first word; 5 by default),
    /// as an edit to it does.
    static class Rebuilds
    {
        static readonly Stopwatch tryAdd = new Stopwatch();
        static int tryAddCalls;
        static readonly AccessTools.FieldRef<SolidConduitFlow, System.Collections.Generic.List<System.Collections.Generic.List<SolidConduitFlow.Conduit>>> paths =
            AccessTools.FieldRefAccess<SolidConduitFlow, System.Collections.Generic.List<System.Collections.Generic.List<SolidConduitFlow.Conduit>>>("pathList");

        public static void Install(Harmony harmony, string path)
        {
            if (!System.IO.File.Exists(System.IO.Path.Combine(path, "rebuild.txt"))) return;
            harmony.Patch(AccessTools.Method(typeof(SolidConduitFlow), "OnUtilityNetworksRebuilt"),
                prefix: new HarmonyMethod(typeof(Rebuilds), nameof(Begin)), postfix: new HarmonyMethod(typeof(Rebuilds), nameof(SolidEnd)));
            harmony.Patch(AccessTools.Method(typeof(ConduitFlow), "OnUtilityNetworksRebuilt"),
                prefix: new HarmonyMethod(typeof(Rebuilds), nameof(Begin)), postfix: new HarmonyMethod(typeof(Rebuilds), nameof(FlowEnd)));
            harmony.Patch(AccessTools.Method(typeof(ConduitFlow), "RebuildConnections"),
                prefix: new HarmonyMethod(typeof(Rebuilds), nameof(ConnectBegin)), postfix: new HarmonyMethod(typeof(Rebuilds), nameof(ConnectEnd)));
            harmony.Patch(AccessTools.Method(AccessTools.Inner(typeof(ConduitFlow), "BuildNetworkTask"), "Run"),
                prefix: new HarmonyMethod(typeof(Rebuilds), nameof(TaskBegin)), postfix: new HarmonyMethod(typeof(Rebuilds), nameof(TaskEnd)));
            harmony.Patch(AccessTools.Method(typeof(SolidConduitFlow), "TryAdd"),
                prefix: new HarmonyMethod(typeof(Rebuilds), nameof(TryAddBegin)), postfix: new HarmonyMethod(typeof(Rebuilds), nameof(TryAddEnd)));
        }

        static void Begin(out long __state)
        {
            tryAdd.Reset();
            tryAddCalls = 0;
            __state = Stopwatch.GetTimestamp();
        }

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        static void SolidEnd(SolidConduitFlow __instance, long __state) =>
            UnityEngine.Debug.Log($"PERF rebuild conveyor f{Time.frameCount} {Ms(Stopwatch.GetTimestamp() - __state):F2} ms (TryAdd {tryAddCalls} calls {tryAdd.Elapsed.TotalMilliseconds:F2} ms, {paths(__instance).Count} paths)");

        static void FlowEnd(ConduitFlow __instance, long __state)
        {
            UnityEngine.Debug.Log($"PERF rebuild {__instance.conduitType} f{Time.frameCount} {Ms(Stopwatch.GetTimestamp() - __state):F2} ms "
                + $"(connections {Ms(connect):F2} ms; {tasks} networks, {Ms(System.Threading.Interlocked.Read(ref taskSum)):F2} ms in all, longest {Ms(taskMax):F2} ms)");
            connect = 0; tasks = 0; taskSum = 0; taskMax = 0;
        }

        static long connect, taskSum, taskMax;
        static int tasks;

        static void ConnectBegin(out long __state) => __state = Stopwatch.GetTimestamp();

        static void ConnectEnd(long __state) => connect += Stopwatch.GetTimestamp() - __state;

        static void TaskBegin(out long __state) => __state = Stopwatch.GetTimestamp();

        static void TaskEnd(long __state)
        {
            long t = Stopwatch.GetTimestamp() - __state;
            System.Threading.Interlocked.Increment(ref tasks);
            System.Threading.Interlocked.Add(ref taskSum, t);
            long max;
            while ((max = System.Threading.Interlocked.Read(ref taskMax)) < t && System.Threading.Interlocked.CompareExchange(ref taskMax, t, max) != max) { }
        }

        static void TryAddBegin() { tryAddCalls++; tryAdd.Start(); }

        static void TryAddEnd() => tryAdd.Stop();

        public static IEnumerator Run(string path)
        {
            var words = System.IO.File.ReadAllText(path).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            int times = words.Length > 0 ? int.Parse(words[0]) : 5;
            for (int i = 0; i < 300; i++) yield return null;
            for (int i = 0; i < times; i++)
            {
                Game.Instance.solidConduitSystem.ForceRebuildNetworks();
                for (int f = 0; f < 30; f++) yield return null;
                Game.Instance.liquidConduitSystem.ForceRebuildNetworks();
                for (int f = 0; f < 30; f++) yield return null;
                Game.Instance.gasConduitSystem.ForceRebuildNetworks();
                for (int f = 0; f < 30; f++) yield return null;
            }
            UnityEngine.Debug.Log("PERF rebuild done");
        }
    }
}
