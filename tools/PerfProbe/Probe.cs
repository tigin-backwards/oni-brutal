using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: the same frame-rate measurement in both players. Once a colony is
    /// loaded it keeps the game running at the fastest speed (or `speed.txt`'s; -1 keeps it paused) (when autosaves or events have
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
        /// The centre of the 48×28-cell window (about the view at orthographic size 12) of a world
        /// holding the fewest building cells, searched in steps of 8 cells.
        static int BuildingCells(int x, int y, int w, int h)
        {
            int count = 0;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    if (Grid.Objects[Grid.XYToCell(x + i, y + j), (int)ObjectLayer.Building] != null) count++;
            return count;
        }

        /// The view camera.txt asked for, held there.
        (Vector3 At, float Size)? view;

        static Vector3 QuietestView(WorldContainer world)
        {
            const int w = 48, h = 28;
            int bestCount = int.MaxValue, bestX = 0, bestY = 0;
            for (int y = world.WorldOffset.y; y + h <= world.WorldOffset.y + world.WorldSize.y; y += 8)
                for (int x = world.WorldOffset.x; x + w <= world.WorldOffset.x + world.WorldSize.x; x += 8)
                {
                    int count = BuildingCells(x, y, w, h);
                    if (count < bestCount) { bestCount = count; bestX = x; bestY = y; }
                }
            Debug.Log($"PERF quietest view at cells {bestX},{bestY}: {bestCount} building cells");
            return new Vector3(bestX + w * 0.5f, bestY + h * 0.5f, 0f);
        }

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

        /// Counts OnTransformChildrenChanged.
        public class ChildWatcher : MonoBehaviour
        {
            public int Count;
            void OnTransformChildrenChanged() => Count++;
        }

        /// Behaviours the defect review asked about, logged as "PERF measure ...": which changes of
        /// a transform's children send OnTransformChildrenChanged, and how graphics that draw no
        /// geometry or sit under a CanvasGroup at alpha 0 are given depths and hit by raycasts.
        System.Collections.IEnumerator Measure()
        {
            var parent = new GameObject("MeasureParent");
            var watcher = parent.AddComponent<ChildWatcher>();
            var prefab = new GameObject("MeasurePrefab");
            int before = watcher.Count;
            var a = Instantiate(prefab, parent.transform);
            Debug.Log($"PERF measure children: Instantiate(original, parent) {watcher.Count - before}");
            before = watcher.Count;
            Instantiate(prefab, Vector3.zero, Quaternion.identity, parent.transform);
            Debug.Log($"PERF measure children: Instantiate(original, position, rotation, parent) {watcher.Count - before}");
            yield return null;
            before = watcher.Count;
            Destroy(a);
            Debug.Log($"PERF measure children: Destroy(child) same frame {watcher.Count - before}");
            yield return null;
            Debug.Log($"PERF measure children: Destroy(child) by the next frame {watcher.Count - before}");
            before = watcher.Count;
            var b = new GameObject("B");
            b.transform.SetParent(parent.transform);
            Debug.Log($"PERF measure children: SetParent {watcher.Count - before}");
            before = watcher.Count;
            DestroyImmediate(b);
            Debug.Log($"PERF measure children: DestroyImmediate(child) {watcher.Count - before}");
            before = watcher.Count;
            var inactive = new GameObject("Inactive");
            inactive.SetActive(false);
            inactive.transform.SetParent(parent.transform);
            Debug.Log($"PERF measure children: SetParent of an inactive child {watcher.Count - before}");
            before = watcher.Count;
            Destroy(inactive);
            yield return null;
            Debug.Log($"PERF measure children: Destroy(inactive child) {watcher.Count - before}");
            Destroy(parent);
            Destroy(prefab);

            var canvasGo = new GameObject("MeasureCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            UnityEngine.UI.Graphic Make(string name, Transform under, Vector2 at, System.Type type)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(under, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(80f, 80f);
                rt.anchoredPosition = at;
                return (UnityEngine.UI.Graphic)go.AddComponent(type);
            }
            var empty = Make("EmptyGraphic", canvasGo.transform, new Vector2(-200f, 0f), typeof(NonDrawingGraphic));
            var groupGo = new GameObject("AlphaZeroGroup", typeof(RectTransform));
            groupGo.transform.SetParent(canvasGo.transform, false);
            groupGo.AddComponent<CanvasGroup>().alpha = 0f;
            var hidden = Make("UnderAlphaZero", groupGo.transform, new Vector2(200f, 0f), typeof(UnityEngine.UI.Image));
            var shown = Make("Shown", canvasGo.transform, new Vector2(0f, 200f), typeof(UnityEngine.UI.Image));
            for (int i = 0; i < 3; i++) yield return null;
            Debug.Log($"PERF measure depth: empty {empty.depth}, under alpha 0 {hidden.depth}, shown {shown.depth}");
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            foreach (var g in new[] { empty, hidden, shown })
            {
                var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    { position = RectTransformUtility.WorldToScreenPoint(null, g.rectTransform.position) };
                hits.Clear();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, hits);
                Debug.Log($"PERF measure raycast at {g.name}: first {(hits.Count > 0 ? hits[0].gameObject.name : "none")}, hits {string.Join(",", hits.ConvertAll(h => h.gameObject.name))}");
            }
            Destroy(canvasGo);
            Debug.Log("PERF measure done");
        }

        System.Collections.IEnumerator Start()
        {
            if (Worldgen.Mode == "run") yield return Worldgen.Run();
            // `measure.txt`: engine behaviours measured at the main menu, the same in either player
            // (Measure).
            if (System.IO.File.Exists(System.IO.Path.Combine(Path, "measure.txt")))
            {
                while (MainMenu.Instance == null) yield return null;
                for (int i = 0; i < 60; i++) yield return null;
                yield return Measure();
            }
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
            // size, `top` (the active world's top centre) or `quiet` (the discovered planetoid with
            // the fewest building cells made active, and its 48×28-cell window with the fewest, for
            // comparing a quiet view). The view is put back if the game moves it.
            var cameraFile = System.IO.Path.Combine(Path, "camera.txt");
            if (System.IO.File.Exists(cameraFile))
            {
                // After the load has placed the camera at the save's position.
                for (int i = 0; i < 200; i++) yield return null;
                var words = System.IO.File.ReadAllText(cameraFile).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                var world = ClusterManager.Instance.activeWorld;
                if (words[0] == "quiet")
                {
                    WorldContainer emptiest = null;
                    int fewest = int.MaxValue;
                    foreach (var candidate in ClusterManager.Instance.WorldContainers)
                    {
                        if (!candidate.IsDiscovered || candidate.IsModuleInterior) continue;
                        int count = BuildingCells(candidate.WorldOffset.x, candidate.WorldOffset.y, candidate.WorldSize.x, candidate.WorldSize.y);
                        Debug.Log($"PERF world {candidate.id} {candidate.GetComponent<ClusterGridEntity>()?.Name}: {count} building cells");
                        if (count < fewest) { fewest = count; emptiest = candidate; }
                    }
                    if (emptiest != world)
                    {
                        ClusterManager.Instance.SetActiveWorld(emptiest.id);
                        for (int i = 0; i < 120; i++) yield return null;
                        world = emptiest;
                    }
                }
                bool named = words[0] == "top" || words[0] == "quiet";
                Vector3 at = words[0] == "top" ? new Vector3(world.WorldOffset.x + world.WorldSize.x * 0.5f, world.WorldOffset.y + world.WorldSize.y - 12f, 0f)
                    : words[0] == "quiet" ? QuietestView(world)
                    : new Vector3(float.Parse(words[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(words[1], System.Globalization.CultureInfo.InvariantCulture), 0f);
                float size = words.Length > (named ? 1 : 2) ? float.Parse(words[^1], System.Globalization.CultureInfo.InvariantCulture) : 12f;
                at.z = CameraController.Instance.transform.position.z;
                CameraController.Instance.SnapTo(at, size);
                Debug.Log($"PERF camera at {at} size {size}");
                view = (at, size);
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
            // `speed.txt`: the game speed kept, 0 (normal) to 2 (fastest, the default), or -1: paused.
            var speedFile = System.IO.Path.Combine(Path, "speed.txt");
            int speed = System.IO.File.Exists(speedFile) ? int.Parse(System.IO.File.ReadAllText(speedFile).Trim()) : 2;
            // `resize.txt` "FRAMES W H": that many frames into the colony the window goes to W×H
            // (Screen.SetResolution, windowed); the game's error screen it raises is closed 60
            // frames later and the screen captured at 120 (resized.png beside the probe), the
            // same in either player.
            var resizeFile = System.IO.Path.Combine(Path, "resize.txt");
            int[] resize = System.IO.File.Exists(resizeFile) ? System.Array.ConvertAll(System.IO.File.ReadAllText(resizeFile).Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries), int.Parse) : null;
            int colonyFrames = 0;
            float windowStart = Time.realtimeSinceStartup;
            int frames = 0;
            long contentions = RuntimeCounters.LockContentions(), paused = RuntimeCounters.GcPauseMs();
            while (true)
            {
                yield return null;
                if (Game.Instance == null) yield break;
                // Only when the game has stopped: the search for dialogs walks the whole scene, a
                // cost the measurement must not carry.
                if (speed < 0 && Time.frameCount % 60 == 0 && !SpeedControlScreen.Instance.IsPaused) SpeedControlScreen.Instance.Pause(false);
                else if (speed >= 0 && Time.frameCount % 60 == 0 && (SpeedControlScreen.Instance.IsPaused || SpeedControlScreen.Instance.GetSpeed() != speed))
                {
                    foreach (var d in FindObjectsByType<KModalScreen>(FindObjectsSortMode.None)) d.Deactivate();
                    SpeedControlScreen.Instance.SetSpeed(speed);
                    SpeedControlScreen.Instance.Unpause(false);
                }
                if (Time.frameCount % 60 == 0 && view is { } v && Vector2.Distance(CameraController.Instance.transform.position, v.At) > 1f)
                {
                    CameraController.Instance.SnapTo(v.At, v.Size);
                    Debug.Log($"PERF camera moved by the game; put back at {v.At}");
                }
                colonyFrames++;
                if (resize != null && colonyFrames == resize[0])
                {
                    Debug.Log($"PERF resize to {resize[1]}x{resize[2]}");
                    Screen.SetResolution(resize[1], resize[2], FullScreenMode.Windowed);
                }
                // The game's error screen for the resize's exception (KCanvasScaler, the same in
                // the original player) closes, so the capture shows the world at the new size.
                if (resize != null && colonyFrames == resize[0] + 60)
                {
                    KCrashReporter.terminateOnError = false;
                    var errorCanvas = GameObject.Find(KCrashReporter.error_canvas_name);
                    if (errorCanvas != null) Destroy(errorCanvas);
                }
                if (resize != null && colonyFrames == resize[0] + 120)
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Path, "resized.png"));
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
