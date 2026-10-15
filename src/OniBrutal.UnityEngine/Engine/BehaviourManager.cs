using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// Creates the native record for a component type. Engine components with their own
    /// native state register a factory; everything else gets the record for its category.
    internal static class ComponentNatives
    {
        static readonly Dictionary<Type, Func<ComponentNative>> factories = new()
        {
            // Engine components with their own native state. One list, so registration never
            // depends on type initialization order.
            [typeof(CanvasGroup)] = () => new CanvasGroupNative(),
            [typeof(Camera)] = () => new Rendering.CameraNative(),
            [typeof(Canvas)] = () => new UI.CanvasNative(),
            [typeof(CanvasRenderer)] = () => new UI.CanvasRendererNative(),
            [typeof(Renderer)] = () => new Rendering.RendererNative(),
            [typeof(MeshRenderer)] = () => new Rendering.MeshRendererNative(),
            [typeof(SpriteRenderer)] = () => new Rendering.SpriteRendererNative(),
            [typeof(MeshFilter)] = () => new Rendering.MeshFilterNative(),
            [typeof(Light)] = () => new Rendering.LightNative(),
            [typeof(UnityEngine.Rendering.SortingGroup)] = () => new Rendering.SortingGroupNative(),
            [typeof(SphereCollider)] = () => new SphereColliderNative(),
            [typeof(BoxCollider)] = () => new BoxColliderNative(),
            [typeof(CapsuleCollider)] = () => new CapsuleColliderNative(),
            [typeof(MeshCollider)] = () => new MeshColliderNative(),
            [typeof(Rigidbody)] = () => new RigidbodyNative(),
            [typeof(BoxCollider2D)] = () => new BoxCollider2DNative(),
            [typeof(Rigidbody2D)] = () => new Rigidbody2DNative(),
            [typeof(Animator)] = () => new Mecanim.AnimatorNative(),
            [typeof(ParticleSystem)] = () => new Particles.ParticleSystemNative(),
            [typeof(ParticleSystemRenderer)] = () => new Particles.ParticleSystemRendererNative(),
            [typeof(UnityEngine.Video.VideoPlayer)] = () => new Video.VideoPlayerNative(),
            [typeof(UnityEngine.Playables.PlayableDirector)] = () => new Playables.PlayableDirectorNative(),
            [typeof(TextMesh)] = () => new Text.TextMeshNative(),
        };

        public static void Register(Type componentType, Func<ComponentNative> factory)
        {
            lock (factories) factories[componentType] = factory;
        }

        public static ComponentNative Create(Type type)
        {
            lock (factories)
                for (var t = type; t != null && t != typeof(Component); t = t.BaseType)
                    if (factories.TryGetValue(t, out var f)) return f();
            if (typeof(RectTransform).IsAssignableFrom(type)) return new RectTransformNative();
            if (typeof(Transform).IsAssignableFrom(type)) return new TransformNative();
            if (typeof(Behaviour).IsAssignableFrom(type)) return new BehaviourNative();
            return new ComponentNative();
        }
    }

    /// The per-phase lists of active-and-enabled MonoBehaviours (Unity's behaviour managers),
    /// ordered by script execution order and, within an order, by the order in which they were
    /// enabled. Changes made while a phase is running take effect from the next phase.
    internal static class BehaviourManager
    {
        sealed class PhaseList(Func<ScriptInfo, Action<MonoBehaviour>> selector)
        {
            readonly SortedDictionary<int, List<BehaviourNative>> byOrder = new();
            readonly List<BehaviourNative> pendingAdd = new();
            readonly HashSet<BehaviourNative> members = new();
            readonly HashSet<BehaviourNative> pendingRemove = new();

            public void Add(BehaviourNative b)
            {
                if (selector(b.Script) == null) return;
                pendingRemove.Remove(b);
                if (members.Contains(b) || pendingAdd.Contains(b)) return;
                pendingAdd.Add(b);
            }

            public void Remove(BehaviourNative b)
            {
                if (pendingAdd.Remove(b)) return;
                if (members.Contains(b)) pendingRemove.Add(b);
            }

            void Integrate()
            {
                if (pendingRemove.Count > 0)
                {
                    foreach (var list in byOrder.Values) list.RemoveAll(pendingRemove.Contains);
                    foreach (var b in pendingRemove) members.Remove(b);
                    pendingRemove.Clear();
                }
                foreach (var b in pendingAdd)
                {
                    if (!byOrder.TryGetValue(b.ExecutionOrder, out var list)) byOrder[b.ExecutionOrder] = list = new();
                    list.Add(b);
                    members.Add(b);
                }
                pendingAdd.Clear();
            }

            public void Run()
            {
                Integrate();
                foreach (var list in byOrder.Values)
                    foreach (var b in list.ToArray())
                    {
                        // Disabled or destroyed earlier in this phase: skip. A behaviour whose Start
                        // has not run yet (enabled after this frame's Start pass) gets its update
                        // callbacks only after Start, as in Unity.
                        if (!b.IsActiveAndEnabledState || pendingRemove.Contains(b) || !b.StartCalled) continue;
                        Lifecycle.Invoke(selector(b.Script), (MonoBehaviour)b.Managed);
                    }
            }
        }

        static readonly PhaseList update = new(s => s.Update);
        static readonly PhaseList lateUpdate = new(s => s.LateUpdate);
        static readonly PhaseList fixedUpdate = new(s => s.FixedUpdate);
        static readonly List<BehaviourNative> startQueue = new();

        public static void Register(BehaviourNative b)
        {
            update.Add(b);
            lateUpdate.Add(b);
            fixedUpdate.Add(b);
            IMGUI.GUIManager.Register(b); // OnGUI behaviours
        }

        public static void Unregister(BehaviourNative b)
        {
            update.Remove(b);
            lateUpdate.Remove(b);
            fixedUpdate.Remove(b);
            IMGUI.GUIManager.Unregister(b);
        }

        public static void ScheduleStart(BehaviourNative b)
        {
            b.StartPending = true;
            startQueue.Add(b);
        }

        public static void CancelStart(BehaviourNative b)
        {
            if (!b.StartPending) return;
            b.StartPending = false;
            startQueue.Remove(b);
        }

        /// Runs Start for behaviours scheduled before this call, in execution order. A behaviour
        /// that is not active-and-enabled at that point keeps its Start for when it is enabled.
        public static void RunStarts()
        {
            while (startQueue.Count > 0)
            {
                var batch = startQueue.ToArray();
                startQueue.Clear();
                // Stable sort by execution order.
                var ordered = new List<BehaviourNative>(batch);
                var indexed = new Dictionary<BehaviourNative, int>();
                for (int i = 0; i < ordered.Count; i++) indexed[ordered[i]] = i;
                ordered.Sort((a, b) => a.ExecutionOrder != b.ExecutionOrder ? a.ExecutionOrder.CompareTo(b.ExecutionOrder) : indexed[a].CompareTo(indexed[b]));
                foreach (var b in ordered)
                {
                    b.StartPending = false;
                    if (b.StartCalled || b.Destroyed) continue;
                    if (!b.IsActiveAndEnabledState) continue; // rescheduled on the next OnEnable
                    b.StartCalled = true;
                    var mb = (MonoBehaviour)b.Managed;
                    if (b.Script.Start != null) Lifecycle.Invoke(b.Script.Start, mb);
                    else if (b.Script.StartCoroutine != null)
                    {
                        try
                        {
                            var routine = b.Script.StartCoroutine(mb);
                            if (routine != null) Coroutines.Start(mb, routine);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e, mb);
                        }
                    }
                }
            }
        }

        public static void RunUpdate() => update.Run();
        public static void RunLateUpdate() => lateUpdate.Run();
        public static void RunFixedUpdate() => fixedUpdate.Run();
    }
}
