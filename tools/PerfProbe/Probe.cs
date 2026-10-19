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
            Debug.Log($"PERF colony f{Time.frameCount} cycle {GameClock.Instance.GetCycle()}");
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
        public static long GcPauseMs() => pause == null ? -1 : (long)((System.TimeSpan)pause.Invoke(null, null)).TotalMilliseconds;
    }
}
