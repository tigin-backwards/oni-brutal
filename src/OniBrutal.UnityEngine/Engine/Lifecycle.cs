using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniBrutal.Engine
{
    /// GameObject/component creation, activation and destruction with Unity's player semantics.
    ///
    /// - Awake runs once per behaviour, when its GameObject first becomes active in the
    ///   hierarchy (also for disabled behaviours). Objects outside scenes (prefab assets) never
    ///   become active in the hierarchy and are never Awakened.
    /// - OnEnable/OnDisable bracket the "active and enabled" state: GameObject active in the
    ///   hierarchy and Behaviour.enabled.
    /// - Start runs once, before the behaviour's first Update, in the next Start phase.
    /// - Activation sets the active flags of the whole subtree first, then runs Awake/OnEnable
    ///   per component in hierarchy pre-order; deactivation clears the flags, then runs OnDisable
    ///   in the same order.
    /// - Destroy is deferred to the end of the frame; DestroyImmediate is synchronous.
    ///   OnDestroy runs only for behaviours whose Awake ran.
    internal static class Lifecycle
    {
        // ---- Creation ----

        public static void CreateGameObject(GameObject managed, string name, Type[] components)
        {
            var native = new GameObjectNative { Name = name ?? "" };
            managed.Bind(native);
            AttachTransform(native, typeof(Transform));
            var scene = Scenes.Active;
            if (scene != null)
            {
                native.Scene = scene;
                Scenes.AddRoot(scene, native);
                native.ActiveInHierarchy = true;
            }
            if (components != null)
                foreach (var t in components) AddComponent(native, t);
        }

        /// Creates the GameObject's Transform (or RectTransform) as component 0.
        static Transform AttachTransform(GameObjectNative go, Type transformType)
        {
            var native = (TransformNative)ComponentNatives.Create(transformType);
            native.GameObject = go;
            var t = Object.ConstructBound<Transform>(transformType, native);
            go.Transform = native;
            go.Components = [t];
            return t;
        }

        public static Component AddComponent(GameObjectNative go, Type type)
        {
            if (type == null)
            {
                Debug.LogError("AddComponent with a null type");
                return null;
            }
            if (!typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogError($"AddComponent asking for invalid type \"{type.FullName}\"");
                return null;
            }
            if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            {
                Debug.LogError($"Cannot add component of type '{type.Name}' because it is abstract.");
                return null;
            }
            if (typeof(Transform).IsAssignableFrom(type))
            {
                // A RectTransform replaces the plain Transform in place; any other Transform request fails.
                if (type == typeof(RectTransform) && go.Components[0].GetType() == typeof(Transform))
                    return ConvertToRectTransform(go);
                Debug.LogError($"Can't add component '{type.Name}' to {go.Name} because such a component is already added to the game object!");
                return null;
            }
            if (IsDisallowedDuplicate(go, type))
            {
                Debug.LogError($"Can't add component '{type.Name}' to {go.Name} because such a component is already added to the game object!");
                return null;
            }

            // Required components first, recursively.
            foreach (RequireComponent rc in type.GetCustomAttributes(typeof(RequireComponent), true))
                foreach (var req in new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 })
                    if (req != null && Components.First(go, req) == null)
                    {
                        if (typeof(Transform).IsAssignableFrom(req)) AddComponent(go, req);
                        else if (AddComponent(go, req) == null) return null;
                    }

            var component = CreateComponent(go, type);
            component.EngineReset();
            if (go.ActiveInHierarchy) AwakeAndEnable(component);
            return component;
        }

        static bool IsDisallowedDuplicate(GameObjectNative go, Type type)
        {
            // The attribute applies to the class that declares it and its subclasses.
            for (var t = type; t != null && t != typeof(Component); t = t.BaseType)
            {
                if (!t.IsDefined(typeof(DisallowMultipleComponent), false)) continue;
                foreach (var c in go.Components)
                    if (t.IsInstanceOfType(c)) return true;
            }
            return false;
        }

        /// Constructs a component and appends it to the GameObject (no lifecycle calls).
        public static Component CreateComponent(GameObjectNative go, Type type)
        {
            var native = ComponentNatives.Create(type);
            native.GameObject = go;
            if (native is BehaviourNative bn && typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                bn.Script = ScriptInfo.Get(type);
                bn.ExecutionOrder = bn.Script.ExecutionOrder;
            }
            var component = Object.ConstructBound<Component>(type, native);
            var comps = go.Components;
            var grown = new Component[comps.Length + 1];
            Array.Copy(comps, grown, comps.Length);
            grown[^1] = component;
            go.Components = grown;
            return component;
        }

        static Component ConvertToRectTransform(GameObjectNative go)
        {
            var old = (Transform)go.Components[0];
            var oldNative = (TransformNative)old.NativeOrThrow;
            var native = (TransformNative)ComponentNatives.Create(typeof(RectTransform));
            native.GameObject = go;
            native.LocalPosition = oldNative.LocalPosition;
            native.LocalRotation = oldNative.LocalRotation;
            native.LocalScale = oldNative.LocalScale;
            native.Parent = oldNative.Parent;
            native.Children = oldNative.Children;
            foreach (var child in native.Children) child.Parent = native;
            if (native.Parent != null)
            {
                var siblings = (TransformNative[])native.Parent.Children.Clone();
                siblings[Array.IndexOf(siblings, oldNative)] = native;
                native.Parent.Children = siblings;
            }
            var rect = Object.ConstructBound<RectTransform>(typeof(RectTransform), native);
            var comps = (Component[])go.Components.Clone();
            comps[0] = rect;
            go.Transform = native;
            go.Components = comps;
            old.Unbind();
            native.MarkDirty();
            return rect;
        }

        // ---- Activation ----

        public static bool ParentActive(GameObjectNative go)
        {
            var parent = go.Transform.Parent;
            return parent != null ? parent.GameObject.ActiveInHierarchy : go.Scene != null;
        }

        public static void SetActive(GameObjectNative go, bool value)
        {
            if (go.ActiveSelf == value) return;
            go.ActiveSelf = value;
            UpdateActiveInHierarchy(go);
        }

        /// Recomputes activeInHierarchy for a subtree after its activeSelf, parent or scene
        /// changed, and runs the resulting lifecycle callbacks.
        public static void UpdateActiveInHierarchy(GameObjectNative go) => ApplyActive(go, go.ActiveSelf && ParentActive(go));

        static void ApplyActive(GameObjectNative go, bool now)
        {
            if (now == go.ActiveInHierarchy) return;
            var changed = new List<GameObjectNative>();
            SetFlags(go, now, changed);
            if (now)
            {
                foreach (var g in changed)
                    foreach (var c in g.Components)
                    {
                        if (!g.ActiveInHierarchy) break; // deactivated by an earlier callback
                        if (c.Native != null) AwakeAndEnable(c);
                    }
            }
            else
            {
                foreach (var g in changed)
                {
                    foreach (var c in g.Components)
                        if (c.Native != null) Disable(c);
                    StopCoroutinesOn(g);
                }
            }
        }

        static void SetFlags(GameObjectNative go, bool active, List<GameObjectNative> changed)
        {
            bool now = active && go.ActiveSelf;
            if (now == go.ActiveInHierarchy) return;
            go.ActiveInHierarchy = now;
            changed.Add(go);
            foreach (var child in go.Transform.Children) SetFlags(child.GameObject, now, changed);
        }

        /// Awake (once) and OnEnable (if enabled) for a component whose GameObject is active.
        public static void AwakeAndEnable(Component c)
        {
            var native = c.Native as ComponentNative;
            if (native == null) return;
            if (native is BehaviourNative bn)
            {
                if (!bn.AwakeCalled)
                {
                    bn.AwakeCalled = true;
                    c.EngineAwake();
                    if (bn.Script?.Awake != null) Invoke(bn.Script.Awake, (MonoBehaviour)c);
                    if (c.Native == null || !native.GameObject.ActiveInHierarchy) return;
                }
                if (bn.Enabled) Enable(c, bn);
            }
            else
            {
                if (!native.AwakeCalledForComponent)
                {
                    native.AwakeCalledForComponent = true;
                    c.EngineAwake();
                }
                c.EngineOnEnable(); // components that are not Behaviours: each activation
            }
        }

        public static void Enable(Component c, BehaviourNative bn)
        {
            if (bn.IsActiveAndEnabledState) return;
            bn.IsActiveAndEnabledState = true;
            c.EngineOnEnable();
            var script = bn.Script;
            if (script != null)
            {
                if (script.OnEnable != null) Invoke(script.OnEnable, (MonoBehaviour)c);
                if (!bn.IsActiveAndEnabledState) return; // disabled again by OnEnable
                BehaviourManager.Register(bn);
                if (!bn.StartCalled && !bn.StartPending) BehaviourManager.ScheduleStart(bn);
            }
        }

        public static void Disable(Component c)
        {
            if (c.Native is ComponentNative and not BehaviourNative) c.EngineOnDisable(); // non-Behaviours: deactivation
            if (c.Native is not BehaviourNative bn || !bn.IsActiveAndEnabledState) return;
            bn.IsActiveAndEnabledState = false;
            if (bn.Script != null) BehaviourManager.Unregister(bn);
            c.EngineOnDisable();
            if (bn.Script?.OnDisable != null) Invoke(bn.Script.OnDisable, (MonoBehaviour)c);
        }

        /// Coroutines stop when their GameObject is deactivated (not when the behaviour is disabled).
        static void StopCoroutinesOn(GameObjectNative go)
        {
            foreach (var c in go.Components)
                if (c is MonoBehaviour mb && c.Native is BehaviourNative bn && bn.Coroutines is { Count: > 0 })
                    Coroutines.StopAll(mb, bn);
        }

        public static void Invoke(Action<MonoBehaviour> message, MonoBehaviour target)
        {
            try
            {
                message(target);
            }
            catch (Exception e)
            {
                Debug.LogException(e, target);
            }
        }

        // ---- Destruction ----

        static readonly List<Object> pendingDestroy = new();
        static readonly List<(Object obj, float time)> timedDestroy = new();

        public static void ScheduleDestroy(Object obj, float delay)
        {
            var native = obj.Native;
            if (native == null) return;
            if (delay > 0f)
            {
                timedDestroy.Add((obj, Time.time + delay));
                return;
            }
            if (native.DestroyRequested) return;
            native.DestroyRequested = true;
            pendingDestroy.Add(obj);
        }

        /// End-of-frame processing of Destroy requests (player loop).
        public static void ProcessDestroyQueue()
        {
            float now = Time.time;
            for (int i = timedDestroy.Count - 1; i >= 0; i--)
            {
                if (timedDestroy[i].time > now) continue;
                var obj = timedDestroy[i].obj;
                timedDestroy.RemoveAt(i);
                ScheduleDestroy(obj, 0f);
            }
            // Destroy callbacks may request more destruction; drain until empty.
            while (pendingDestroy.Count > 0)
            {
                var batch = pendingDestroy.ToArray();
                pendingDestroy.Clear();
                foreach (var obj in batch) DestroyNow(obj);
            }
        }

        public static void DestroyNow(Object obj)
        {
            var native = obj.Native;
            if (native == null || native.Destroyed) return;
            switch (obj)
            {
                case GameObject go:
                    DestroyGameObject((GameObjectNative)native);
                    break;
                case Transform:
                    Debug.LogError("Destroying the transform component is not permitted. Destroy the GameObject instead.");
                    native.DestroyRequested = false;
                    break;
                case Component c:
                    DestroyComponent(c, (ComponentNative)native, detach: true);
                    break;
                case ScriptableObject so:
                    native.Destroyed = true;
                    ScriptableObjectLifecycle.Destroy(so);
                    obj.EngineOnDestroy();
                    obj.Unbind();
                    break;
                default:
                    native.Destroyed = true;
                    obj.EngineOnDestroy();
                    obj.Unbind();
                    break;
            }
        }

        static void DestroyGameObject(GameObjectNative go)
        {
            var t = go.Transform;
            // 1. Leave the active state (OnDisable for the whole subtree).
            if (go.ActiveInHierarchy) ApplyActive(go, false);
            // 2. Detach from parent or scene.
            if (t.Parent != null) Hierarchy.RemoveChild(t.Parent, t);
            else if (go.Scene != null) Scenes.RemoveRoot(go.Scene, go);
            // 3. Destroy the subtree.
            DestroySubtree(go);
        }

        static void DestroySubtree(GameObjectNative go)
        {
            if (go.Destroyed) return;
            go.Destroyed = true;
            foreach (var c in go.Components)
                if (c.Native is ComponentNative cn && !cn.Destroyed) DestroyComponent(c, cn, detach: false);
            foreach (var child in go.Transform.Children) DestroySubtree(child.GameObject);
            go.Managed.Unbind();
        }

        static void DestroyComponent(Component c, ComponentNative native, bool detach)
        {
            native.Destroyed = true;
            if (native is BehaviourNative bn)
            {
                Disable(c);
                if (c is MonoBehaviour mb)
                {
                    if (bn.Coroutines is { Count: > 0 }) Coroutines.StopAll(mb, bn);
                    BehaviourManager.CancelStart(bn);
                    if (bn.AwakeCalled && bn.Script?.OnDestroy != null) Invoke(bn.Script.OnDestroy, mb);
                }
            }
            c.EngineOnDestroy();
            if (detach)
            {
                var go = native.GameObject;
                var comps = go.Components;
                int index = Array.IndexOf(comps, c);
                if (index >= 0)
                {
                    var shrunk = new Component[comps.Length - 1];
                    Array.Copy(comps, 0, shrunk, 0, index);
                    Array.Copy(comps, index + 1, shrunk, index, comps.Length - index - 1);
                    go.Components = shrunk;
                }
            }
            c.Unbind();
        }
    }
}
