using System.Collections;
using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE for comparison videos, the same in both players.
    /// `fpsclock.txt`: every quarter second of real time `PERF clock MS FRAMES` (MS the Unix time in
    /// milliseconds, FRAMES the frames since the last line), a frame over half a second as
    /// `PERF stall FROM TO` (Unix ms), and `PERF menu MS` once the main menu is up: the recorder
    /// lines these up with the capture for the frame-rate readout.
    /// `noautosave.txt`: the game's own autosave is off (GenericGameSettings.disableAutosave), so
    /// none freezes a recording unasked.
    /// `actions.txt`: once the colony is up and `go.txt` appears beside the probe (the recorder
    /// writes it when the capture has started), its lines run in turn: `wait S` (real seconds),
    /// `overlay NAME` (as Overlays; `Temperature` with the absolute filter), `pan DX DY S` (the
    /// view moves DX, DY cells over S real seconds), `autosave` (as at the end of a cycle).
    static class Video
    {
        static long Now() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static IEnumerator Clock()
        {
            long last = Now();
            int frames = 0;
            bool menu = false;
            while (true)
            {
                long before = Now();
                yield return null;
                long now = Now();
                frames++;
                if (now - before > 500) Debug.Log($"PERF stall {before} {now}");
                if (!menu && MainMenu.Instance != null) { menu = true; Debug.Log($"PERF menu {now} f{Time.frameCount} intro {MainMenu.Instance.IntroShortName}"); }
                if (now - last < 250) continue;
                Debug.Log($"PERF clock {now} {frames}");
                last = now;
                frames = 0;
            }
        }

        public static void NoAutosave(bool off)
        {
            var p = typeof(Klei.GenericGameSettings).GetProperty("disableAutosave");
            if (p != null) p.GetSetMethod(true).Invoke(Klei.GenericGameSettings.instance, new object[] { off });
            else typeof(Klei.GenericGameSettings).GetField("disableAutosave").SetValue(Klei.GenericGameSettings.instance, off);
        }

        public static IEnumerator Actions(Runner runner, string dir)
        {
            var go = System.IO.Path.Combine(dir, "go.txt");
            while (!System.IO.File.Exists(go))
            {
                for (int i = 0; i < 15; i++) yield return null;
                if (Game.Instance == null) yield break;
            }
            Debug.Log($"PERF action go {Now()}");
            foreach (var raw in System.IO.File.ReadAllLines(System.IO.Path.Combine(dir, "actions.txt")))
            {
                var w = raw.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (w.Length == 0 || w[0].StartsWith("#")) continue;
                Debug.Log($"PERF action {raw.Trim()} {Now()}");
                switch (w[0])
                {
                    case "wait":
                    {
                        float end = Time.realtimeSinceStartup + F(w[1]);
                        while (Time.realtimeSinceStartup < end) yield return null;
                        break;
                    }
                    case "overlay":
                    {
                        HashedString id = w[1] == "None" ? OverlayModes.None.ID : new HashedString(w[1]);
                        OverlayScreen.Instance.ToggleOverlay(id, false);
                        if (w[1] == "Temperature") Overlays.SetTemperatureFilter("AbsoluteTemperature");
                        break;
                    }
                    case "pan":
                    {
                        var cam = CameraController.Instance;
                        Vector3 from = cam.transform.position;
                        float size = Camera.main.orthographicSize;
                        Vector3 d = new Vector3(F(w[1]), F(w[2]), 0f);
                        float secs = F(w[3]), start = Time.realtimeSinceStartup;
                        runner.view = null;
                        while (true)
                        {
                            float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / secs);
                            // Eased, as a hand on the keys would start and stop.
                            float e = t * t * (3f - 2f * t);
                            cam.SnapTo(from + d * e, size);
                            if (t >= 1f) break;
                            yield return null;
                        }
                        runner.view = (from + d, size);
                        break;
                    }
                    case "autosave":
                        NoAutosave(false);
                        HarmonyLib.AccessTools.Method(typeof(GameClock), "DoAutoSave").Invoke(GameClock.Instance, new object[] { GameClock.Instance.GetCycle() });
                        // Off again: the cycle's own autosave arriving while this one is under
                        // way breaks the save indicator (SaveActive.DoCallBack), in either player.
                        if (System.IO.File.Exists(System.IO.Path.Combine(dir, "noautosave.txt"))) NoAutosave(true);
                        break;
                }
            }
            Debug.Log($"PERF action done {Now()}");
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }
}
