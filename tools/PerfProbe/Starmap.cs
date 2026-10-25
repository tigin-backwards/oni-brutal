using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PerfProbe
{
    /// TEST INFRASTRUCTURE: `starmap.txt` beside the probe: the Spaced Out starmap
    /// (ClusterMapScreen) opened and closed again and again, as its button does, to see whether
    /// its frames get slower over a session. The file's words: `CYCLES OPEN CLOSED` (frames;
    /// default 20 600 1200), then the flags `quit` (quit when done) and `pan` (each open
    /// zooms to a level of its own and pans to a place of its own, 30 frames in, as a player looking
    /// around does). Each cycle logs the open window's frames
    /// (`PERF starmap K open f#: first MS ms, mean MS ms, max MS ms`), then a census of what the
    /// starmap holds and of the scene (`PERF starmap K census …`), then the closed window's mean.
    static class Starmap
    {
        static readonly System.Type[] GlobalCounts =
        {
            typeof(GameObject), typeof(KBatchedAnimController), typeof(LoopingSounds), typeof(ClusterMapVisualizer),
            typeof(ClusterMapPath), typeof(UnityEngine.UI.Graphic), typeof(Canvas), typeof(Material), typeof(Mesh),
        };

        static int Count(object o, string field) =>
            Traverse.Create(o).Field(field).GetValue() is ICollection c ? c.Count : -1;

        /// Subscriptions on a game object's event system: all of them, and for each given hash.
        static string Subscriptions(GameObject go, params int[] hashes)
        {
            var ko = KObjectManager.Instance.Get(go);
            if (ko == null || !ko.GetEventSystem(out var events)) return "none";
            var map = Traverse.Create(events).Field("entryMap").GetValue() as IDictionary;
            if (map == null) return "?";
            int total = 0;
            foreach (var v in map.Values) total += ((ICollection)v).Count;
            var parts = new List<string> { $"{total} in all" };
            foreach (int h in hashes)
                parts.Add($"{h}: {(map.Contains(h) ? ((ICollection)map[h]).Count : 0)}");
            return string.Join(", ", parts);
        }

        static string ChildCensus(Component root)
        {
            if (root == null) return "none";
            var counts = new Dictionary<string, int>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                string n = c.GetType().Name;
                counts[n] = counts.TryGetValue(n, out var k) ? k + 1 : 1;
            }
            return string.Join(" ", counts.OrderByDescending(kv => kv.Value).Take(14).Select(kv => kv.Key + "×" + kv.Value));
        }

        static void Census(int cycle)
        {
            var map = ClusterMapScreen.Instance;
            var names = ClusterNameDisplayScreen.Instance;
            int entities = 0;
            foreach (var list in ClusterGrid.Instance.cellContents.Values) entities += list.Count;
            var path = Traverse.Create(map).Field("pathDrawer").GetValue() as ClusterMapPathDrawer;
            Debug.Log($"PERF starmap {cycle} census f{Time.frameCount}: vis {Count(map, "m_gridEntityVis")} anims {Count(map, "m_gridEntityAnims")} cells {Count(map, "m_cellVisByLocation")}"
                + $" names {Count(names, "m_entries")} paths {(path != null && path.pathContainer != null ? path.pathContainer.childCount : -1)} grid entities {entities}"
                + $" hex inventories {StarmapHexCellInventory.AllInventories.Count}");
            Debug.Log($"PERF starmap {cycle} global: " + string.Join(" ", GlobalCounts.Select(t => t.Name + " " + Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None).Length))
                + $" | materials (all) {Resources.FindObjectsOfTypeAll<Material>().Length} meshes (all) {Resources.FindObjectsOfTypeAll<Mesh>().Length} textures (all) {Resources.FindObjectsOfTypeAll<Texture>().Length}{RuntimeCounters.Gc()}");
            var batches = KAnimBatchManager.Instance();
            int uiBatches = 0;
            if (Traverse.Create(batches).Field("uiBatchSets").GetValue() is IList ui)
                foreach (var info in ui) uiBatches += ((BatchSet)Traverse.Create(info).Field("batchSet").GetValue()).batchCount;
            Debug.Log($"PERF starmap {cycle} batches: sets {Count(batches, "batchSets")} ui sets {Count(batches, "uiBatchSets")} ui batches {uiBatches} culled sets {Count(batches, "culledBatchSetInfos")} active sets {Count(batches, "activeBatchSets")} groups {Count(batches, "batchGroups")}");
            Debug.Log($"PERF starmap {cycle} subs: game {Subscriptions(Game.Instance.gameObject, -1991583975, -1554423969, -1298331547)}; map {Subscriptions(map.gameObject, 1980521255)}");
            Debug.Log($"PERF starmap {cycle} map children: {ChildCensus(map)}");
            Debug.Log($"PERF starmap {cycle} name children: {ChildCensus(names)}");
        }

        static IEnumerator Window(int cycle, string label, int frames)
        {
            var times = new List<float>(frames);
            long bytes = RuntimeCounters.Allocated();
            float last = Time.realtimeSinceStartup;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                times.Add((now - last) * 1000f);
                last = now;
            }
            var steady = times.Skip(System.Math.Min(30, times.Count / 2)).ToList();
            steady.Sort();
            Debug.Log($"PERF starmap {cycle} {label} f{Time.frameCount}: first {times[0]:F1} ms, mean {steady.Average():F2} ms, median {steady[steady.Count / 2]:F2} ms, max {times.Max():F1} ms{(bytes < 0 ? "" : $", allocated {(RuntimeCounters.Allocated() - bytes) / 1024.0 / frames:F0} KiB a frame")}");
        }

        public static IEnumerator Run(string file)
        {
            var words = System.IO.File.ReadAllText(file).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            int cycles = words.Length > 0 ? int.Parse(words[0]) : 20, open = words.Length > 1 ? int.Parse(words[1]) : 600, closed = words.Length > 2 ? int.Parse(words[2]) : 1200;
            bool pan = System.Array.IndexOf(words, "pan") >= 0;
            for (int i = 0; i < 300; i++) yield return null;
            Screens.DismissDialogs();
            var info = AccessTools.Field(typeof(ManagementMenu), "clusterMapInfo").GetValue(ManagementMenu.Instance) as KIconToggleMenu.ToggleInfo;
            yield return Window(0, "before", closed);
            Census(0);
            for (int cycle = 1; cycle <= cycles; cycle++)
            {
                ManagementMenu.Instance.OnButtonClick(info);
                if (pan)
                {
                    for (int i = 0; i < 30; i++) yield return null;
                    var random = new System.Random(cycle);
                    Traverse.Create(ClusterMapScreen.Instance).Field("m_targetZoomScale").SetValue(50f + random.Next(0, 5) * 25f);
                    var content = (Traverse.Create(ClusterMapScreen.Instance).Field("mapScrollRect").GetValue() as KScrollRect).content;
                    content.localPosition = new Vector3(random.Next(-3000, 3000), random.Next(-2000, 2000), 0f);
                }
                yield return Window(cycle, "open", open);
                Census(cycle);
                ManagementMenu.Instance.CloseAll();
                yield return Window(cycle, "closed", closed);
            }
            Debug.Log("PERF starmap done");
            // `quit`: the player then quits.
            if (System.Array.IndexOf(words, "quit") >= 0) Application.Quit();
        }
    }
}
