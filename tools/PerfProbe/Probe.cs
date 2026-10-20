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
        /// `autosave.txt`: the game's own autosave (GameClock.DoAutoSave, as at a cycle's end) three
        /// times, 900 frames apart, each timed as the frames it stalls: the longest frame, and
        /// the total of frames over 100 ms in the 300 frames after it starts.
        System.Collections.IEnumerator AutosaveFreeze()
        {
            SpeedControlScreen.Instance.Unpause(false);
            var doAutoSave = HarmonyLib.AccessTools.Method(typeof(GameClock), "DoAutoSave");
            for (int n = 1; n <= 3; n++)
            {
                for (int i = 0; i < 900; i++) yield return null;
                doAutoSave.Invoke(GameClock.Instance, new object[] { GameClock.Instance.GetCycle() });
                float longest = 0f, stalled = 0f, last = Time.realtimeSinceStartup;
                for (int i = 0; i < 300; i++)
                {
                    yield return null;
                    float now = Time.realtimeSinceStartup, frame = (now - last) * 1000f;
                    last = now;
                    if (frame > longest) longest = frame;
                    if (frame > 100f) stalled += frame;
                }
                Debug.Log($"PERF autosave {n}: longest frame {longest:F0} ms, frames over 100 ms {stalled:F0} ms");
            }
            Debug.Log("PERF autosave done");
        }

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
        /// MonoBehaviour.Invoke targets: each call is logged with the frame and phase it arrived in.
        public class InvokeTarget : MonoBehaviour
        {
            public string Phase = "start";
            public System.Collections.Generic.List<string> Calls = new();
            void Update() => Phase = "update";
            void LateUpdate() => Phase = "late";
            void Hit() { Calls.Add($"f{Time.frameCount} after {Phase} at {Time.time:F2}"); }
            void Repeat() { Calls.Add($"repeat f{Time.frameCount}"); }
        }

        public class ChildWatcher : MonoBehaviour
        {
            public int Count;
            void OnTransformChildrenChanged() => Count++;
        }

        /// Behaviours the defect review asked about, logged as "PERF measure ...": which changes of
        /// a transform's children send OnTransformChildrenChanged, and how graphics that draw no
        /// geometry or sit under a CanvasGroup at alpha 0 are given depths and hit by raycasts.
        /// The hover card's drawn widgets: how many are active, and the first one's screen position.
        static string Widgets()
        {
            if (HoverTextScreen.Instance == null) return "none";
            var root = HoverTextScreen.Instance.transform.childCount > 0 ? HoverTextScreen.Instance.transform.GetChild(0) : null;
            int active = 0;
            string first = "";
            foreach (var t in HoverTextScreen.Instance.GetComponentsInChildren<RectTransform>(false))
            {
                if (t == HoverTextScreen.Instance.transform) continue;
                active++;
                if (first.Length == 0) first = $" first {t.name} at {t.position}";
            }
            return active + first + (root != null ? $" root {root.name} scale {root.localScale}" : "");
        }

        Vector2 guiPointer = new Vector2(-1f, -1f);

        /// IMGUI's pointer position, from whatever event reaches the probe (Repaint at least).
        void OnGUI()
        {
            if (Event.current != null && (Event.current.type == EventType.Repaint || Event.current.isMouse)) guiPointer = Event.current.mousePosition;
        }

        System.Collections.IEnumerator Pointer()
        {
            for (int n = 0; n < 6; n++)
            {
                for (int i = 0; i < 120; i++) yield return null;
                var events = UnityEngine.EventSystems.EventSystem.current;
                var tool = PlayerController.Instance != null ? PlayerController.Instance.ActiveTool : null;
                Debug.Log($"PERF pointer f{Time.frameCount} position {Input.mousePosition} gui {guiPointer} screen {Screen.width}x{Screen.height} present {Input.mousePresent} focused {Application.isFocused}"
                    + $" over-ui {(events != null ? events.IsPointerOverGameObject().ToString() : "no event system")}"
                    + $" module {(events != null && events.currentInputModule != null ? events.currentInputModule.GetType().Name : "none")}"
                    + $" tool {(tool != null ? tool.GetType().Name : "none")} hover-card {(HoverTextScreen.Instance != null && HoverTextScreen.Instance.gameObject.activeInHierarchy)}"
                    + $" show-hover {(tool != null && tool.ShowHoverUI())} widgets {Widgets()}");
            }
        }

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
            var current = Screen.currentResolution;
            Debug.Log($"PERF measure screen: current {current.width}x{current.height} @{current.refreshRateRatio.value:F2}, window {Screen.width}x{Screen.height}, modes {string.Join(" ", System.Array.ConvertAll(Screen.resolutions, r => $"{r.width}x{r.height}@{r.refreshRateRatio.value:F2}"))}");
            // SetParent keeping the world transform under a parent scaled to zero (HoverTextDrawer's
            // pools parent their roots so, under a canvas not yet scaled), and a new overlay
            // canvas's scale before and after its first frame.
            var zero = new GameObject("ZeroScaled", typeof(RectTransform));
            zero.transform.localScale = Vector3.zero;
            var plain = new GameObject("Plain");
            plain.transform.SetParent(zero.transform);
            var rect = new GameObject("Rect");
            rect.AddComponent<RectTransform>().SetParent(zero.transform);
            var scaled = new GameObject("Scaled");
            scaled.transform.localScale = new Vector3(2f, 3f, 4f);
            scaled.transform.SetParent(zero.transform);
            Debug.Log($"PERF measure zero parent: plain {plain.transform.localScale} rect {rect.transform.localScale} scaled {scaled.transform.localScale}, lossy {plain.transform.lossyScale}");
            var halfAxis = new GameObject("HalfZero");
            halfAxis.transform.localScale = new Vector3(2f, 0f, 1f);
            var under = new GameObject("UnderHalfZero");
            under.transform.SetParent(halfAxis.transform);
            Debug.Log($"PERF measure (2,0,1) parent: {under.transform.localScale}");
            Destroy(zero); Destroy(halfAxis);
            var fresh = new GameObject("FreshCanvas", typeof(RectTransform));
            var freshCanvas = fresh.AddComponent<Canvas>();
            freshCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.Log($"PERF measure new overlay canvas scale: at once {fresh.transform.localScale}");
            yield return null;
            Debug.Log($"PERF measure new overlay canvas scale: next frame {fresh.transform.localScale}");
            Destroy(fresh);
            // An overlay canvas saved at scale zero (as prefab canvases are): when its scale is applied.
            var saved = new GameObject("SavedCanvas", typeof(RectTransform));
            saved.SetActive(false);
            saved.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            saved.transform.localScale = Vector3.zero;
            var savedChild = new GameObject("SavedChild", typeof(RectTransform));
            savedChild.transform.SetParent(saved.transform, false);
            saved.SetActive(true);
            var keptChild = new GameObject("KeptChild");
            keptChild.AddComponent<RectTransform>().SetParent(savedChild.transform);
            Debug.Log($"PERF measure zero-saved overlay canvas: on activation {saved.transform.localScale}, a child's lossy {savedChild.transform.lossyScale}, kept-world child {keptChild.transform.localScale}");
            var prefabCanvas = Instantiate(saved);
            Debug.Log($"PERF measure zero-saved overlay canvas: instantiated {prefabCanvas.transform.localScale}");
            saved.transform.localScale = Vector3.zero;
            Debug.Log($"PERF measure zero-saved overlay canvas: zeroed while active {saved.transform.localScale}");
            yield return null;
            Debug.Log($"PERF measure zero-saved overlay canvas: next frame {saved.transform.localScale}, instantiated {prefabCanvas.transform.localScale}");
            Destroy(saved); Destroy(prefabCanvas);
            // What activation applies besides the scale, and a Dropdown-like clone: a nested,
            // anchored canvas-bearing template instantiated at the root, then nested again.
            string Rect(Transform t) { var r = (RectTransform)t; return $"anchors {r.anchorMin}-{r.anchorMax} pivot {r.pivot} size {r.sizeDelta} pos {r.anchoredPosition} scale {r.localScale}"; }
            var odd = new GameObject("OddCanvas", typeof(RectTransform));
            odd.SetActive(false);
            odd.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var oddRect = (RectTransform)odd.transform;
            oddRect.anchorMin = new Vector2(0.2f, 0.3f); oddRect.anchorMax = new Vector2(0.4f, 0.5f); oddRect.pivot = new Vector2(0f, 1f);
            oddRect.sizeDelta = new Vector2(10f, 20f); oddRect.anchoredPosition = new Vector2(5f, 6f); oddRect.localScale = Vector3.zero;
            odd.SetActive(true);
            Debug.Log($"PERF measure odd overlay canvas on activation: {Rect(odd.transform)}");
            var host = new GameObject("HostCanvas", typeof(RectTransform));
            host.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var template = new GameObject("Template", typeof(RectTransform));
            template.transform.SetParent(host.transform, false);
            var templateRect = (RectTransform)template.transform;
            templateRect.anchorMin = new Vector2(0f, 0f); templateRect.anchorMax = new Vector2(1f, 0f); templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0f, 150f); templateRect.anchoredPosition = new Vector2(0f, 2f);
            var nested = template.AddComponent<Canvas>();
            nested.overrideSorting = true;
            var clone = Instantiate(template);
            Debug.Log($"PERF measure clone at the root: {Rect(clone.transform)}");
            clone.transform.SetParent(host.transform, false);
            Debug.Log($"PERF measure clone nested again: {Rect(clone.transform)}");
            yield return null;
            Debug.Log($"PERF measure clone next frame: {Rect(clone.transform)}");
            Destroy(odd); Destroy(host); Destroy(clone);
            // Which enables lay a root canvas out at once.
            GameObject Zeroed(string name, bool active, bool setMode)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.SetActive(active);
                var c = go.AddComponent<Canvas>();
                if (setMode) c.renderMode = RenderMode.ScreenSpaceOverlay;
                go.transform.localScale = Vector3.zero;
                return go;
            }
            var defaultMode = Zeroed("DefaultMode", false, false);
            defaultMode.SetActive(true);
            Debug.Log($"PERF measure enable: activated, render mode never set {Rect(defaultMode.transform)}");
            var toggled = Zeroed("Toggled", true, true);
            toggled.GetComponent<Canvas>().enabled = false;
            toggled.GetComponent<Canvas>().enabled = true;
            Debug.Log($"PERF measure enable: component re-enabled {Rect(toggled.transform)}");
            var reactivated = Zeroed("Reactivated", true, true);
            reactivated.SetActive(false);
            reactivated.SetActive(true);
            Debug.Log($"PERF measure enable: object re-activated {Rect(reactivated.transform)}");
            var inactivePrefab = Zeroed("InactivePrefab", false, true);
            var fromInactive = Instantiate(inactivePrefab);
            Debug.Log($"PERF measure enable: instantiated inactive {Rect(fromInactive.transform)}");
            fromInactive.SetActive(true);
            Debug.Log($"PERF measure enable: instantiated inactive, then activated {Rect(fromInactive.transform)}");
            var activeZero = Zeroed("ActiveZero", true, true);
            var fromActive = Instantiate(activeZero);
            Debug.Log($"PERF measure enable: instantiated active zero-scaled {Rect(fromActive.transform)}");
            var nestedHost = new GameObject("NestedHost", typeof(RectTransform));
            nestedHost.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var underHost = Zeroed("UnderHost", false, true);
            underHost.transform.SetParent(nestedHost.transform, false);
            underHost.transform.localScale = Vector3.zero;
            underHost.SetActive(true);
            Debug.Log($"PERF measure enable: nested canvas activated {Rect(underHost.transform)}");
            var moved = Zeroed("MovedOut", true, true);
            moved.transform.SetParent(nestedHost.transform, false);
            moved.transform.localScale = Vector3.zero;
            moved.transform.SetParent(null, false);
            Debug.Log($"PERF measure enable: active nested canvas moved to the root {Rect(moved.transform)}");
            var probeDefault = new GameObject("DefaultCanvas", typeof(RectTransform));
            var defaultCanvas = probeDefault.AddComponent<Canvas>();
            Debug.Log($"PERF measure enable: AddComponent<Canvas> render mode {defaultCanvas.renderMode}, active default-mode canvas laid out {probeDefault.transform.localScale} size {((RectTransform)probeDefault.transform).sizeDelta}");
            Destroy(probeDefault);
            foreach (var g in new[] { defaultMode, toggled, reactivated, inactivePrefab, fromInactive, activeZero, fromActive, nestedHost, moved }) Destroy(g);
            // Serializable class fields of a component made by AddComponent (the warp conduit
            // receiver's prefab adds a ConduitSecondaryOutput and never sets its port info).
            var holder = new GameObject("SerializableHolder");
            var output = holder.AddComponent<ConduitSecondaryOutput>();
            Debug.Log($"PERF measure AddComponent serializable class field: active {(output.portInfo == null ? "null" : $"instance {output.portInfo.conduitType}")}");
            var inactiveHolder = new GameObject("InactiveHolder");
            inactiveHolder.SetActive(false);
            var inactiveOutput = inactiveHolder.AddComponent<ConduitSecondaryOutput>();
            Debug.Log($"PERF measure AddComponent serializable class field: inactive {(inactiveOutput.portInfo == null ? "null" : "instance")}");
            var copy = Instantiate(holder);
            Debug.Log($"PERF measure Instantiate serializable class field: source {(output.portInfo == null ? "null" : $"instance {output.portInfo.conduitType}")}, clone {(copy.GetComponent<ConduitSecondaryOutput>().portInfo == null ? "null" : "instance")}");
            var arrayHolder = new GameObject("ArrayHolder");
            var interacts = arrayHolder.AddComponent<Activatable>();
            Debug.Log($"PERF measure AddComponent array field: workAnims {(interacts.workAnims == null ? "null" : "length " + interacts.workAnims.Length)}");
            var arrayCopy = Instantiate(arrayHolder);
            Debug.Log($"PERF measure Instantiate array field: source {(interacts.workAnims == null ? "null" : "length " + interacts.workAnims.Length)}, clone {(arrayCopy.GetComponent<Activatable>().workAnims == null ? "null" : "length " + arrayCopy.GetComponent<Activatable>().workAnims.Length)}");
            // A null array field (inactive, so the component is not awoken).
            var tileHolder = new GameObject("TileHolder");
            tileHolder.SetActive(false);
            var tileable = tileHolder.AddComponent<AnimTileable>();
            var tileCopy = Instantiate(tileHolder);
            Debug.Log($"PERF measure Instantiate null array field: source {(tileable.tags == null ? "null" : "length " + tileable.tags.Length)}, clone {(tileCopy.GetComponent<AnimTileable>().tags == null ? "null" : "length " + tileCopy.GetComponent<AnimTileable>().tags.Length)}");
            Destroy(tileHolder); Destroy(tileCopy);
            Destroy(copy); Destroy(arrayHolder); Destroy(arrayCopy);
            Destroy(holder); Destroy(inactiveHolder);
            Debug.Log($"PERF measure colors: darkGray {Color.darkGray} orange {Color.orange} violet {Color.violet}");
            // MonoBehaviour.Invoke: when it calls, and on disabled or inactive targets.
            var enabledTarget = new GameObject("InvokeEnabled").AddComponent<InvokeTarget>();
            var disabledTarget = new GameObject("InvokeDisabled").AddComponent<InvokeTarget>();
            disabledTarget.enabled = false;
            var inactiveTarget = new GameObject("InvokeInactive").AddComponent<InvokeTarget>();
            var zeroTarget = new GameObject("InvokeZero").AddComponent<InvokeTarget>();
            var repeatTarget = new GameObject("InvokeRepeat").AddComponent<InvokeTarget>();
            var started = Time.time;
            enabledTarget.Invoke("Hit", 0.2f);
            disabledTarget.Invoke("Hit", 0.2f);
            inactiveTarget.Invoke("Hit", 0.2f);
            inactiveTarget.gameObject.SetActive(false);
            zeroTarget.Invoke("Hit", 0f);
            repeatTarget.InvokeRepeating("Repeat", 0f, 0.1f);
            Debug.Log($"PERF measure invoke: started f{Time.frameCount} at {started:F2}, IsInvoking {enabledTarget.IsInvoking("Hit")} {enabledTarget.IsInvoking()}, inactive IsInvoking {inactiveTarget.IsInvoking()}");
            enabledTarget.Invoke("Missing", 0f);
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until) yield return null;
            repeatTarget.CancelInvoke("Repeat");
            Debug.Log($"PERF measure invoke: enabled [{string.Join("; ", enabledTarget.Calls)}] disabled [{string.Join("; ", disabledTarget.Calls)}] inactive [{string.Join("; ", inactiveTarget.Calls)}] zero [{string.Join("; ", zeroTarget.Calls)}] repeats {repeatTarget.Calls.Count} after-cancel IsInvoking {repeatTarget.IsInvoking()} timeScale {Time.timeScale}");
            foreach (var t in new MonoBehaviour[] { enabledTarget, disabledTarget, inactiveTarget, zeroTarget, repeatTarget }) Destroy(t.gameObject);
            // measure-bundle beside the probe: an asset bundle with UI prefabs. Which of its loaded
            // objects DestroyImmediate removes without allowDestroyingAssets.
            var bundlePath = System.IO.Path.Combine(Path, "measure-bundle");
            if (System.IO.File.Exists(bundlePath))
            {
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                GameObject withText = null, other = null;
                foreach (var name in bundle.GetAllAssetNames())
                {
                    var asset = bundle.LoadAsset<GameObject>(name);
                    if (asset == null) continue;
                    if (withText == null && asset.GetComponentInChildren<UnityEngine.UI.Text>(true) != null) withText = asset;
                    else if (other == null) other = asset;
                }
                if (withText != null)
                {
                    var text = withText.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    var textHolder = text.gameObject;
                    DestroyImmediate(text);
                    Debug.Log($"PERF measure bundle prefab: component destroyed {text == null}, then AddComponent<Text> {(textHolder.AddComponent<UnityEngine.UI.Text>() != null ? "added" : "null")}");
                    var child = withText.transform.childCount > 0 ? withText.transform.GetChild(0).gameObject : null;
                    if (child != null)
                    {
                        DestroyImmediate(child);
                        Debug.Log($"PERF measure bundle prefab: child GameObject destroyed {child == null}");
                    }
                }
                if (other != null)
                {
                    DestroyImmediate(other);
                    Debug.Log($"PERF measure bundle prefab: root GameObject destroyed {other == null}");
                }
                var texture = new Texture2D(2, 2);
                Debug.Log("PERF measure bundle: done");
                Destroy(texture);
            }
            // A prefab from the game's own data (not a scene object, not from a bundle): its
            // Transform's sibling component destroyed without allowDestroyingAssets.
            GameObject dataPrefab = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (!go.scene.IsValid() && go.transform.parent == null && go.GetComponents<Component>().Length > 1 && go.name.Length > 0) { dataPrefab = go; break; }
            if (dataPrefab != null)
            {
                var victim = dataPrefab.GetComponents<Component>()[1];
                var victimType = victim.GetType().Name;
                DestroyImmediate(victim);
                Debug.Log($"PERF measure data prefab {dataPrefab.name}: {victimType} destroyed {victim == null}");
            }
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
            // `menuresize.txt` "W H": at the main menu the window goes to W×H and 120 frames later
            // the screen is captured (menuresized.png), the same in either player.
            var menuResize = System.IO.Path.Combine(Path, "menuresize.txt");
            if (System.IO.File.Exists(menuResize))
            {
                var size = System.Array.ConvertAll(System.IO.File.ReadAllText(menuResize).Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries), int.Parse);
                while (MainMenu.Instance == null) yield return null;
                for (int i = 0; i < 120; i++) yield return null;
                Screen.SetResolution(size[0], size[1], FullScreenMode.Windowed);
                for (int i = 0; i < (size.Length > 2 ? size[2] : 120); i++) yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Path, "menuresized.png"));
                for (int i = 0; i < 10; i++) yield return null;
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
            // `pointer.txt`: what the game's hover handling reads (PlayerController.UpdateHover, the
            // select tool's hover card) while the mouse has not moved, every 120 frames, 6 times.
            if (System.IO.File.Exists(System.IO.Path.Combine(Path, "pointer.txt"))) StartCoroutine(Pointer());
            if (System.IO.File.Exists(System.IO.Path.Combine(Path, "savetest.txt"))) { yield return SaveTest(); yield break; }
            if (System.IO.File.Exists(System.IO.Path.Combine(Path, "autosave.txt"))) { yield return AutosaveFreeze(); yield break; }
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
                Debug.Log($"PERF f{Time.frameCount} fps {frames / (now - windowStart):F2} cycle {GameClock.Instance.GetCycle()} time {GameClock.Instance.GetTime():F0} paused {SpeedControlScreen.Instance.IsPaused}{runtime}");
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
