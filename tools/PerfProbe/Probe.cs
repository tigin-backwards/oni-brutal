using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: the same frame-rate measurement in both players. Once a colony is
    /// loaded it keeps the game running at the fastest speed (when autosaves or events have
    /// paused it, closing dialogs and unpausing, checked every 60 frames) and logs, every 600
    /// frames, the mean frame rate over them from the real-time clock, with the cycle, and (where the
    /// runtime has them: .NET, not Unity's Mono) the window's lock contentions and GC pause. Mods.json
    /// stamp: -2037174645 (the Mono string hash of the folder name PerfProbe, ARCHITECTURE D3).
    public class Mod : KMod.UserMod2
    {
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            Worldgen.Install(harmony, path);
            var keeper = new GameObject("PerfProbe");
            Object.DontDestroyOnLoad(keeper);
            keeper.AddComponent<Runner>().Path = path;
        }
    }

    public class Runner : MonoBehaviour
    {
        /// `savetest.txt` in the mod's folder (our player with the fast-serialization game tweak):
        /// once the colony is up, pauses and saves in the order the file lists, each with the
        /// game's serializer (g) or the tweak's (f), for comparing the files; consecutive saves with
        /// the game's own show what changes between saves by itself. The tweak is switched through
        /// its test flag by reflection.
        System.Collections.IEnumerator SaveTest()
        {
            SpeedControlScreen.Instance.Pause(false);
            for (int i = 0; i < 120; i++) yield return null;
            var flag = System.Type.GetType("OniBrutal.Host.GameTweaks.FastSerialization, OniBrutal")?.GetField("Enabled");
            if (flag == null) { Debug.Log("PERF savetest: fast-serialization not present"); yield break; }
            var dir = System.IO.Path.GetDirectoryName(SaveLoader.GetActiveSaveFilePath());
            // savetest.txt lists the saves to make, g (the game's serializer) or f (the tweak's).
            var order = System.IO.File.ReadAllText(System.IO.Path.Combine(Path, "savetest.txt")).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (order.Length == 0) order = new[] { "g", "g", "f" };
            for (int n = 0; n < order.Length; n++)
            {
                bool fast = order[n] == "f";
                string name = $"savetest-{n + 1}-{(fast ? "fast" : "game")}";
                flag.SetValue(null, fast);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                SaveLoader.Instance.Save(System.IO.Path.Combine(dir, name + ".sav"), false, false);
                Debug.Log($"PERF savetest {name} {watch.Elapsed.TotalMilliseconds:F0} ms");
                for (int i = 0; i < 30; i++) yield return null;
            }
            flag.SetValue(null, true);
            Debug.Log("PERF savetest done");
        }

        /// The mod's folder.
        public string Path;

        System.Collections.IEnumerator Start()
        {
            if (Worldgen.Mode == "run") yield return Worldgen.Run();
            // `load.txt` in the mod's folder names a save (full path) to load from the main menu,
            // as the load screen does: for loading a given save in either player.
            var load = System.IO.Path.Combine(Path, "load.txt");
            if (System.IO.File.Exists(load))
            {
                while (MainMenu.Instance == null) yield return null;
                for (int i = 0; i < 120; i++) yield return null;
                var save = System.IO.File.ReadAllText(load).Trim();
                Debug.Log("PERF loading " + save);
                LoadScreen.DoLoad(save);
            }
            while (Game.Instance == null || SpeedControlScreen.Instance == null) yield return null;
            Debug.Log($"PERF colony f{Time.frameCount} cycle {GameClock.Instance.GetCycle()}{RuntimeCounters.Jit()}");
            if (System.IO.File.Exists(System.IO.Path.Combine(Path, "savetest.txt"))) { yield return SaveTest(); yield break; }
            // `camera.txt`: where the view stays, as world cells `x y` with an optional orthographic
            // size, or `top` (the active world's top centre: space, for comparing a quiet view).
            var cameraFile = System.IO.Path.Combine(Path, "camera.txt");
            if (System.IO.File.Exists(cameraFile))
            {
                var words = System.IO.File.ReadAllText(cameraFile).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                var world = ClusterManager.Instance.activeWorld;
                Vector3 at = words[0] == "top"
                    ? new Vector3(world.WorldOffset.x + world.WorldSize.x * 0.5f, world.WorldOffset.y + world.WorldSize.y - 12f, 0f)
                    : new Vector3(float.Parse(words[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(words[1], System.Globalization.CultureInfo.InvariantCulture), 0f);
                float size = words.Length > (words[0] == "top" ? 1 : 2) ? float.Parse(words[^1], System.Globalization.CultureInfo.InvariantCulture) : 12f;
                at.z = CameraController.Instance.transform.position.z;
                CameraController.Instance.SnapTo(at, size);
                Debug.Log($"PERF camera at {at} size {size}");
            }
            // `counts.txt` lists component type names (Assembly-CSharp first, then -firstpass): how
            // many of each exist, active or not, logged once the colony is up.
            var counts = System.IO.Path.Combine(Path, "counts.txt");
            if (System.IO.File.Exists(counts))
                foreach (var name in System.IO.File.ReadAllLines(counts))
                {
                    if (name.Trim().Length == 0) continue;
                    var type = System.Type.GetType(name.Trim() + ", Assembly-CSharp") ?? System.Type.GetType(name.Trim() + ", Assembly-CSharp-firstpass");
                    if (type == null || !typeof(Object).IsAssignableFrom(type)) Debug.Log($"PERF count {name}: not a component type");
                    else Debug.Log($"PERF count {name} {FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
                }
            float windowStart = Time.realtimeSinceStartup;
            int frames = 0;
            long contentions = RuntimeCounters.LockContentions(), paused = RuntimeCounters.GcPauseMs();
            while (true)
            {
                yield return null;
                if (Game.Instance == null) yield break;
                // Only when the game has stopped: the search for dialogs walks the whole scene, a
                // cost the measurement must not carry.
                if (Time.frameCount % 60 == 0 && (SpeedControlScreen.Instance.IsPaused || SpeedControlScreen.Instance.GetSpeed() != 2))
                {
                    foreach (var d in FindObjectsByType<KModalScreen>(FindObjectsSortMode.None)) d.Deactivate();
                    SpeedControlScreen.Instance.SetSpeed(2);
                    SpeedControlScreen.Instance.Unpause(false);
                }
                if (++frames < 600) continue;
                float now = Time.realtimeSinceStartup;
                long c = RuntimeCounters.LockContentions(), g = RuntimeCounters.GcPauseMs();
                string runtime = c < 0 ? "" : $" contentions {c - contentions} gc-pause {g - paused} ms";
                contentions = c; paused = g;
                Debug.Log($"PERF f{Time.frameCount} fps {frames / (now - windowStart):F2} cycle {GameClock.Instance.GetCycle()} paused {SpeedControlScreen.Instance.IsPaused}{runtime}");
                windowStart = now;
                frames = 0;
            }
        }
    }

    /// .NET's runtime counters, read by reflection: the probe builds against the game's Mono
    /// libraries, which lack them (-1 there).
    static class RuntimeCounters
    {
        static readonly System.Reflection.PropertyInfo contention = typeof(System.Threading.Monitor).GetProperty("LockContentionCount");
        static readonly System.Reflection.MethodInfo pause = typeof(System.GC).GetMethod("GetTotalPauseDuration", System.Type.EmptyTypes);

        public static long LockContentions() => contention == null ? -1 : (long)contention.GetValue(null);
        static readonly System.Type jitInfo = System.Type.GetType("System.Runtime.JitInfo");

        /// Time spent compiling and methods compiled so far (.NET; empty on Mono).
        public static string Jit()
        {
            if (jitInfo == null) return "";
            var time = (System.TimeSpan)jitInfo.GetMethod("GetCompilationTime").Invoke(null, new object[] { false });
            var count = (long)jitInfo.GetMethod("GetCompiledMethodCount").Invoke(null, new object[] { false });
            return $" jit {time.TotalMilliseconds:F0} ms over {count} methods";
        }

        public static long GcPauseMs() => pause == null ? -1 : (long)((System.TimeSpan)pause.Invoke(null, null)).TotalMilliseconds;
    }
}
