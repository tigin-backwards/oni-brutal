using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: world generation timed the same way in both players. Enabled by a
    /// file `worldgen.txt` in the mod's folder; its first line is
    /// - `trace`: time the generation steps of whatever world the game generates, and log how
    ///   generation was started (the cluster's constructor and Generate's arguments, with stacks);
    /// - `run CLUSTER SEED`: also start that cluster's generation at the main menu.
    /// Every timed step logs `WG <step> <ms> ms` (thread id included: worlds may generate in
    /// parallel), and the run logs its wall time.
    static class Worldgen
    {
        public static string Mode, Cluster;
        public static int Seed;

        static readonly (string Type, string Method)[] steps =
        {
            ("ProcGenGame.Cluster", "Generate"),
            ("ProcGenGame.WorldGen", "GenerateOffline"),
            ("ProcGenGame.WorldGen", "GenerateNoiseData"),
            ("ProcGenGame.WorldGen", "GenerateLayout"),
            ("ProcGenGame.WorldGen", "RenderOffline"),
            ("ProcGenGame.WorldGen", "RenderToMap"),
            ("ProcGenGame.WorldGen", "ProcessByTerrainCell"),
            ("ProcGenGame.WorldGen", "DrawWorldBorder"),
            ("ProcGenGame.WorldGenSimUtil", "DoSettleSim"),
        };

        public static void Install(Harmony harmony, string modPath)
        {
            var file = Path.Combine(modPath, "worldgen.txt");
            if (!File.Exists(file)) return;
            var words = File.ReadAllText(file).Trim().Split(' ');
            Mode = words[0];
            if (Mode == "run") { Cluster = words[1]; Seed = int.Parse(words[2]); }
            var timed = new HarmonyMethod(typeof(Worldgen), nameof(Begin));
            var done = new HarmonyMethod(typeof(Worldgen), nameof(End));
            foreach (var (type, method) in steps)
                foreach (var m in AccessTools.TypeByName(type).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    if (m.Name == method) harmony.Patch(m, prefix: timed, postfix: done);
            var cluster = AccessTools.TypeByName("ProcGenGame.Cluster");
            foreach (var c in cluster.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                harmony.Patch(c, prefix: new HarmonyMethod(typeof(Worldgen), nameof(Trace)));
            foreach (var name in new[] { "Generate", "BeginGeneration", "InitializeWorlds" })
                harmony.Patch(AccessTools.Method(cluster, name), prefix: new HarmonyMethod(typeof(Worldgen), nameof(Trace)));
            UnityEngine.Debug.Log($"WG mode {Mode} {Cluster} {Seed}");
        }

        /// `run` mode: generates the cluster as the game's new-game flow does (constructor, which
        /// initializes the worlds, then Generate with every seed the same and the settle sim on; observed with
        /// `trace`), once the main menu is up, and logs the wall time when the worker reports
        /// completion.
        public static System.Collections.IEnumerator Run()
        {
            while (MainMenu.Instance == null) yield return null;
            for (int i = 0; i < 120; i++) yield return null;
            // The settings the new-game screens set; the generator logs their coordinate, which
            // must read as the UI flow's (`trace`) for the same cluster and seed.
            var settings = CustomGameSettings.Instance;
            settings.LoadClusters();
            settings.SetSurvivalDefaults();
            settings.SetQualitySetting(Klei.CustomSettings.CustomGameSettingConfigs.ClusterLayout, Cluster);
            settings.SetQualitySetting(Klei.CustomSettings.CustomGameSettingConfigs.WorldgenSeed, Seed.ToString());
            var cluster = new ProcGenGame.Cluster(Cluster, Seed, new List<string>(), true, false, false);
            var wall = Stopwatch.StartNew();
            bool complete = false;
            cluster.Generate((key, percent, stage) =>
            {
                if (stage.ToString() == "Complete") complete = true;
                return true;
            }, error => UnityEngine.Debug.Log("WG error " + error), Seed, Seed, Seed, Seed, true, false, false);
            while (!complete && !cluster.HasGenerationStopped) yield return null;
            UnityEngine.Debug.Log($"WG run wall {wall.Elapsed.TotalMilliseconds:F0} ms complete {complete}");
        }

        static void Begin(out Stopwatch __state) => __state = Stopwatch.StartNew();

        static void End(Stopwatch __state, MethodBase __originalMethod)
            => UnityEngine.Debug.Log($"WG {__originalMethod.DeclaringType.Name}.{__originalMethod.Name} {__state.Elapsed.TotalMilliseconds:F0} ms thread {Thread.CurrentThread.ManagedThreadId}");

        static void Trace(MethodBase __originalMethod, object[] __args)
        {
            var args = new List<string>();
            foreach (var a in __args) args.Add(a is System.Collections.IEnumerable e && !(a is string) ? "[" + string.Join(",", Enumerate(e)) + "]" : a?.ToString() ?? "null");
            UnityEngine.Debug.Log($"WG call {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}({string.Join(", ", args)}) thread {Thread.CurrentThread.ManagedThreadId}\n{new StackTrace()}");
        }

        static IEnumerable<string> Enumerate(System.Collections.IEnumerable e)
        {
            foreach (var x in e) yield return x?.ToString() ?? "null";
        }
    }
}
