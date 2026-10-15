using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// Object searches over the loaded scenes (FindObjectsByType, GameObject.Find, …).
    internal static class Finder
    {
        /// Every GameObject in loaded scenes and DontDestroyOnLoad, in hierarchy order.
        public static IEnumerable<GameObjectNative> AllSceneGameObjects()
        {
            foreach (var scene in Scenes.WithDontDestroyOnLoad())
                foreach (var root in scene.Roots.ToArray())
                    foreach (var go in Walk(root))
                        yield return go;
        }

        public static IEnumerable<GameObjectNative> ActiveGameObjects()
        {
            foreach (var go in AllSceneGameObjects())
                if (go.ActiveInHierarchy) yield return go;
        }

        static IEnumerable<GameObjectNative> Walk(GameObjectNative go)
        {
            yield return go;
            foreach (var child in go.Transform.Children)
                foreach (var g in Walk(child.GameObject))
                    yield return g;
        }

        /// Objects of `type` that live in scenes: GameObjects and their components. Inactive ones
        /// are included only when asked; a disabled Behaviour counts as inactive, as in Unity.
        public static IEnumerable<UnityEngine.Object> SceneObjects(Type type, bool includeInactive, FindObjectsSortMode sortMode)
        {
            var results = new List<UnityEngine.Object>();
            foreach (var go in AllSceneGameObjects())
            {
                if (!includeInactive && !go.ActiveInHierarchy) continue;
                if (type.IsInstanceOfType(go.Managed)) results.Add(go.Managed);
                foreach (var c in go.Components)
                {
                    if (!type.IsInstanceOfType(c)) continue;
                    if (!includeInactive && c is Behaviour b && !b.enabled) continue;
                    results.Add(c);
                }
            }
            if (sortMode == FindObjectsSortMode.InstanceID)
                results.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            return results;
        }

        /// GameObject.Find: by name among active objects, or by '/'-separated path from a root.
        public static GameObject FindGameObject(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name.Contains('/'))
            {
                var parts = name.TrimStart('/').Split('/');
                bool fromRoot = name.StartsWith('/');
                foreach (var go in ActiveGameObjects())
                {
                    if (fromRoot && go.Transform.Parent != null) continue;
                    if (go.Name != parts[0]) continue;
                    var t = FindPath(go.Transform, parts, 1);
                    if (t != null && t.GameObject.ActiveInHierarchy) return (GameObject)t.GameObject.Managed;
                }
                return null;
            }
            foreach (var go in ActiveGameObjects())
                if (go.Name == name) return (GameObject)go.Managed;
            return null;
        }

        /// Transform.Find semantics: each path segment names a direct child.
        public static TransformNative FindPath(TransformNative from, string[] parts, int index)
        {
            if (index == parts.Length) return from;
            foreach (var child in from.Children)
            {
                if (child.Name != parts[index] && child.GameObject.Name != parts[index]) continue;
                var found = FindPath(child, parts, index + 1);
                if (found != null) return found;
            }
            return null;
        }
    }
}
