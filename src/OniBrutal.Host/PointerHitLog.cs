using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using OniBrutal.Engine;
using UnityEngine;

namespace OniBrutal.Host
{
    /// Diagnostics (`--log-pointer-hits`): each time the UI element under the mouse changes, the
    /// log gets the frame, the mouse position and what the event system's raycast finds there
    /// (topmost first). Tools hide their cursor icon and hover card while the pointer is over a UI
    /// element, so this names an element that catches the pointer unseen. It uses only uGUI's
    /// public API, through reflection: the host does not reference the UI package it loads.
    internal static class PointerHitLog
    {
        static Type? eventSystemType, pointerDataType, resultType;
        static MethodInfo? raycastAll;
        static PropertyInfo? current, position;
        static PropertyInfo? resultObject;
        static bool resolved, missing;
        static string? last;

        public static void Frame(int frame)
        {
            if (!Resolve()) return;
            var system = current!.GetValue(null);
            if (system == null || system is UnityEngine.Object o && o == null) return;
            var data = Activator.CreateInstance(pointerDataType!, system);
            var mouse = Input.mousePosition;
            position!.SetValue(data, new Vector2(mouse.x, mouse.y));
            var results = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(resultType!))!;
            raycastAll!.Invoke(system, new[] { data, results });
            var hits = results.Cast<object>().Take(4).Select(r => resultObject!.GetValue(r) as GameObject)
                .OfType<GameObject>().Select(Path).ToList();
            string now = hits.Count == 0 ? "nothing" : string.Join(" | ", hits);
            if (now == last) return;
            last = now;
            EngineLog.Engine($"pointer f{frame} ({mouse.x:F0},{mouse.y:F0}): {now}");
        }

        static bool Resolve()
        {
            if (resolved || missing) return resolved;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                eventSystemType ??= assembly.GetType("UnityEngine.EventSystems.EventSystem");
                pointerDataType ??= assembly.GetType("UnityEngine.EventSystems.PointerEventData");
                resultType ??= assembly.GetType("UnityEngine.EventSystems.RaycastResult");
            }
            if (eventSystemType == null || pointerDataType == null || resultType == null) return false; // not loaded yet
            current = eventSystemType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            position = pointerDataType.GetProperty("position");
            resultObject = resultType.GetProperty("gameObject");
            raycastAll = eventSystemType.GetMethod("RaycastAll");
            resolved = raycastAll != null && current != null && position != null && resultObject != null;
            missing = !resolved;
            if (missing) EngineLog.Engine("--log-pointer-hits: uGUI's EventSystem API not found; not logging");
            return resolved;
        }

        static string Path(GameObject g)
        {
            var path = g.name;
            for (var t = g.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            var rect = g.transform as RectTransform;
            return rect != null ? $"{path} {rect.rect}" : path;
        }
    }
}
