using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: `screens.txt` beside the probe: the management screens opened and
    /// closed as their buttons do, to see whether a screen once visited keeps costing frames after
    /// it closes. One command a line: `hold N LABEL` holds N frames (a multiple of 600, starting
    /// on a frame that is one, as --frame-stats' windows) and logs each 600-frame window,
    /// `PERF screens LABEL window K f#: MS ms a frame`; `open NAME` clicks the management menu's
    /// button for NAME (its field name without `Info`: consumables, vitals, jobs, research,
    /// schedule, skills, reports, codex, starmap, clusterMap) and waits 300 frames; `close`
    /// closes the open one (as clicking its button again) and waits 60; `wait overlays` waits
    /// for the overlay sweep (Overlays) to end, as opening a screen closes the overlay.
    static class Screens
    {
        public static IEnumerator Run(string path)
        {
            for (int i = 0; i < 300; i++) yield return null;
            foreach (var d in Object.FindObjectsByType<KModalScreen>(FindObjectsSortMode.None)) d.Deactivate();
            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                var words = raw.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0 || words[0].StartsWith("#")) continue;
                switch (words[0])
                {
                    case "hold":
                        while (Time.frameCount % 600 != 0) yield return null;
                        int hold = int.Parse(words[1]);
                        string label = words.Length > 2 ? words[2] : "hold";
                        Debug.Log($"PERF screens {label} from f{Time.frameCount} to f{Time.frameCount + hold}");
                        float start = Time.realtimeSinceStartup;
                        for (int frame = 1; frame <= hold; frame++)
                        {
                            yield return null;
                            if (frame % 600 != 0) continue;
                            float now = Time.realtimeSinceStartup;
                            Debug.Log($"PERF screens {label} window {frame / 600 - 1} f{Time.frameCount}: {(now - start) * 1000f / 600f:F2} ms a frame");
                            start = now;
                        }
                        break;
                    case "open":
                        var info = AccessTools.Field(typeof(ManagementMenu), words[1] + "Info")?.GetValue(ManagementMenu.Instance) as KIconToggleMenu.ToggleInfo;
                        if (info == null) { Debug.Log($"PERF screens: no button {words[1]}"); break; }
                        try { ManagementMenu.Instance.OnButtonClick(info); }
                        catch (System.Exception e) { Debug.Log($"PERF screens: {words[1]} did not open: {e.GetType().Name} {e.Message}"); }
                        Debug.Log($"PERF screens opened {words[1]} f{Time.frameCount}");
                        for (int i = 0; i < 300; i++) yield return null;
                        break;
                    case "wait":
                        while (Overlays.Started && !Overlays.Done) yield return null;
                        break;
                    case "close":
                        ManagementMenu.Instance.CloseAll();
                        Debug.Log($"PERF screens closed f{Time.frameCount}");
                        for (int i = 0; i < 60; i++) yield return null;
                        break;
                }
            }
            Debug.Log("PERF screens done");
        }
    }
}
