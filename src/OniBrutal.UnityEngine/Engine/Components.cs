using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// Component queries with Unity's matching and traversal rules. Read-only over copy-on-write
    /// arrays, so safe from worker threads.
    internal static class Components
    {
        /// First component, in component order, assignable to `type` (classes or interfaces).
        public static object First(GameObjectNative go, Type type)
        {
            foreach (var c in go.Components)
                if (type.IsInstanceOfType(c)) return c;
            return null;
        }

        /// First<T> and All<T>: the generic queries the game makes constantly (GetComponent<T> is
        /// among its most frequent engine calls), as a compiled type test instead of
        /// Type.IsInstanceOfType per component; the same matching.
        public static T First<T>(GameObjectNative go)
        {
            foreach (var c in go.Components)
                if (c is T match) return match;
            return default;
        }

        public static void All<T>(GameObjectNative go, List<T> results)
        {
            foreach (var c in go.Components)
                if (c is T match) results.Add(match);
        }

        /// Unity matches the component's class name (script or engine class).
        public static Component FirstByName(GameObjectNative go, string typeName)
        {
            foreach (var c in go.Components)
            {
                var t = c.GetType();
                if (t.Name == typeName || t.FullName == typeName) return c;
            }
            return null;
        }

        public static void All(GameObjectNative go, Type type, IList results)
        {
            foreach (var c in go.Components)
                if (type.IsInstanceOfType(c)) results.Add(c);
        }

        /// Depth-first pre-order over this object and its descendants. The object the search
        /// starts from is always searched; without includeInactive, a descendant that is not
        /// active itself is skipped with its subtree, so activity is judged relative to the
        /// starting object (Unity's rule: the shipped MainMenu deactivates its DLC ad and then
        /// finds the ad's KButton with GetComponentInChildren).
        public static object InChildren(GameObjectNative go, Type type, bool includeInactive)
        {
            var found = First(go, type);
            return found ?? InDescendants(go, type, includeInactive);
        }

        static object InDescendants(GameObjectNative go, Type type, bool includeInactive)
        {
            foreach (var child in go.Transform.Children)
            {
                var g = child.GameObject;
                if (!includeInactive && !g.ActiveSelf) continue;
                var found = First(g, type) ?? InDescendants(g, type, includeInactive);
                if (found != null) return found;
            }
            return null;
        }

        public static void AllInChildren(GameObjectNative go, Type type, bool includeInactive, IList results)
        {
            All(go, type, results);
            AllInDescendants(go, type, includeInactive, results);
        }

        static void AllInDescendants(GameObjectNative go, Type type, bool includeInactive, IList results)
        {
            foreach (var child in go.Transform.Children)
            {
                var g = child.GameObject;
                if (!includeInactive && !g.ActiveSelf) continue;
                All(g, type, results);
                AllInDescendants(g, type, includeInactive, results);
            }
        }

        /// This object, then its ancestors.
        public static object InParent(GameObjectNative go, Type type, bool includeInactive)
        {
            for (var t = go.Transform; t != null; t = t.Parent)
            {
                var g = t.GameObject;
                if (!includeInactive && !g.ActiveInHierarchy) continue;
                var found = First(g, type);
                if (found != null) return found;
            }
            return null;
        }

        public static void AllInParent(GameObjectNative go, Type type, bool includeInactive, IList results)
        {
            for (var t = go.Transform; t != null; t = t.Parent)
            {
                var g = t.GameObject;
                if (!includeInactive && !g.ActiveInHierarchy) continue;
                All(g, type, results);
            }
        }
    }

    /// Tags known to the project: Unity's built-ins plus TagManager's custom tags.
    internal static class Tags
    {
        static readonly HashSet<string> builtIn = new(StringComparer.Ordinal)
        {
            "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController",
        };

        static HashSet<string> custom = new(StringComparer.Ordinal);

        public static void Install(IEnumerable<string> customTags) => custom = new HashSet<string>(customTags, StringComparer.Ordinal);

        public static bool IsDefined(string tag) => tag != null && (builtIn.Contains(tag) || custom.Contains(tag));

        static string[] customList = Array.Empty<string>();

        public static void InstallList(string[] customTags)
        {
            customList = customTags;
            Install(customTags);
        }

        /// GameObject.m_Tag: Unity's built-in tag indices, then custom tags from 20000.
        public static string FromIndex(ushort index) => index switch
        {
            0 => "Untagged",
            1 => "Respawn",
            2 => "Finish",
            3 => "EditorOnly",
            5 => "MainCamera",
            6 => "Player",
            7 => "GameController",
            >= 20000 when index - 20000 < customList.Length => customList[index - 20000],
            _ => throw new System.IO.InvalidDataException($"Unknown tag index {index}."),
        };
    }

    /// SendMessage and BroadcastMessage.
    internal static class Messaging
    {
        public static void Send(GameObjectNative go, string methodName, object value, bool hasValue, SendMessageOptions options)
        {
            Threads.AssertMainThread("SendMessage");
            bool received = false;
            foreach (var c in go.Components)
            {
                if (c is not MonoBehaviour b) continue;
                var m = ScriptInfo.FindByName(b.GetType(), methodName, value, hasValue);
                if (m == null) continue;
                received = true;
                Invoke(b, m, value);
            }
            if (!received && options == SendMessageOptions.RequireReceiver)
                Debug.LogError($"SendMessage {methodName} has no receiver!", (UnityEngine.Object)go.Managed);
        }

        public static void Broadcast(GameObjectNative go, string methodName, object value, bool hasValue, SendMessageOptions options)
        {
            bool received = false;
            void Visit(GameObjectNative g)
            {
                if (!g.ActiveInHierarchy) return;
                foreach (var c in g.Components)
                {
                    if (c is not MonoBehaviour b) continue;
                    var m = ScriptInfo.FindByName(b.GetType(), methodName, value, hasValue);
                    if (m == null) continue;
                    received = true;
                    Invoke(b, m, value);
                }
                foreach (var child in g.Transform.Children) Visit(child.GameObject);
            }
            Visit(go);
            if (!received && options == SendMessageOptions.RequireReceiver)
                Debug.LogError($"BroadcastMessage {methodName} has no receiver!", (UnityEngine.Object)go.Managed);
        }

        static void Invoke(MonoBehaviour b, MethodInfo m, object value)
        {
            try
            {
                m.Invoke(b, m.GetParameters().Length == 0 ? null : new[] { value });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                Debug.LogException(e.InnerException, b);
            }
        }
    }
}
