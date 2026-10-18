using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: the same frame-rate measurement in both players. Once a colony is
    /// loaded it keeps the game running at the fastest speed (when autosaves or events have
    /// paused it, closing dialogs and unpausing, checked every 60 frames) and logs, every 600
    /// frames, the mean frame rate over them from the real-time clock, with the cycle. Mods.json
    /// stamp: -2037174645 (the Mono string hash of the folder name PerfProbe, ARCHITECTURE D3).
    public class Mod : KMod.UserMod2
    {
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            Worldgen.Install(harmony, path);
            var keeper = new GameObject("PerfProbe");
            Object.DontDestroyOnLoad(keeper);
            keeper.AddComponent<Runner>();
        }
    }

    public class Runner : MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            if (Worldgen.Mode == "run") yield return Worldgen.Run();
            while (Game.Instance == null || SpeedControlScreen.Instance == null) yield return null;
            Debug.Log($"PERF colony f{Time.frameCount} cycle {GameClock.Instance.GetCycle()}");
            float windowStart = Time.realtimeSinceStartup;
            int frames = 0;
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
                Debug.Log($"PERF f{Time.frameCount} fps {frames / (now - windowStart):F2} cycle {GameClock.Instance.GetCycle()} paused {SpeedControlScreen.Instance.IsPaused}");
                windowStart = now;
                frames = 0;
            }
        }
    }
}
