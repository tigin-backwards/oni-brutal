using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements
{
    public abstract partial class CallbackEventHandler
    {
        internal OniBrutal.Engine.UICallbackRegistry m_CallbackRegistry;

        /// Registering the same callback again for the same event type and phase keeps a single
        /// registration (Unity rule).
        public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            if (callback == null)
                throw new ArgumentException("callback parameter is null");
            (m_CallbackRegistry ??= new OniBrutal.Engine.UICallbackRegistry()).Register(EventBase<TEventType>.TypeId(), callback, useTrickleDown);
        }

        public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            if (callback == null)
                throw new ArgumentException("callback parameter is null");
            m_CallbackRegistry?.Unregister(EventBase<TEventType>.TypeId(), callback, useTrickleDown);
        }

        internal abstract void SendEvent(EventBase e, DispatchMode dispatchMode);

        internal abstract void HandleEvent(EventBase e);

        void IEventHandler.HandleEvent(EventBase evt)
        {
            if (evt != null)
                HandleEvent(evt);
        }

        public bool HasTrickleDownHandlers() => m_CallbackRegistry != null && m_CallbackRegistry.HasTrickleDownHandlers();

        public bool HasBubbleUpHandlers() => m_CallbackRegistry != null && m_CallbackRegistry.HasBubbleUpHandlers();
    }

    public partial class VisualElement
    {
        // Unity's public constructor builds style, layout and hierarchy state that this runtime
        // does not model; consumers never construct elements (only UI Toolkit content does), so it
        // is left out and no VisualElement instance exists here.
        internal VisualElement()
        {
        }

        /// The panel this element belongs to. Only the panel system attaches elements to a panel,
        /// and no panel exists in this runtime (see README-scope.cs), so this is always null.
        public IPanel panel => null;

        public override FocusController focusController => panel?.focusController;

        // Unity forwards to the element's panel (elementPanel?.SendEvent(e)); with no panel the
        // event is not dispatched.
        public sealed override void SendEvent(EventBase e)
        {
        }

        // As SendEvent: forwarded to the (always absent) panel.
        internal sealed override void SendEvent(EventBase e, DispatchMode dispatchMode)
        {
        }

        // Unity invokes this element's callbacks only for an event in its TrickleDown or BubbleUp
        // propagation phase, which the panel dispatcher sets; outside a dispatch the phase is
        // None and nothing runs.
        internal sealed override void HandleEvent(EventBase e)
        {
        }
    }

    public static partial class VisualElementExtensions
    {
        /// The ray with origin transformed as a point and direction as a vector (re-normalized by
        /// the Ray constructor).
        internal static Ray TransformRay(this Matrix4x4 m, Ray ray)
        {
            return new Ray(m.MultiplyPoint3x4(ray.origin), m.MultiplyVector(ray.direction));
        }
    }

    public partial class FocusController
    {
        // Unity's only constructor takes an IFocusRing, a type outside our surface; controllers are
        // created by panels, and none exists here.
        private FocusController()
        {
        }

        // The focused-element chain is filled only by focus changes, which require a panel.
        internal Focusable GetLeafFocusedElement() => null;
    }
}

namespace OniBrutal.Engine
{
    /// Per-handler callback registrations of UI Toolkit, split by phase. Entries are matched by
    /// event type id and delegate equality.
    internal sealed class UICallbackRegistry
    {
        private readonly List<(long typeId, Delegate callback)> m_TrickleDown = new List<(long, Delegate)>();

        private readonly List<(long typeId, Delegate callback)> m_BubbleUp = new List<(long, Delegate)>();

        private List<(long typeId, Delegate callback)> ListFor(UnityEngine.UIElements.TrickleDown useTrickleDown) =>
            useTrickleDown == UnityEngine.UIElements.TrickleDown.TrickleDown ? m_TrickleDown : m_BubbleUp;

        public void Register(long typeId, Delegate callback, UnityEngine.UIElements.TrickleDown useTrickleDown)
        {
            var list = ListFor(useTrickleDown);
            if (IndexOf(list, typeId, callback) < 0)
                list.Add((typeId, callback));
        }

        public bool Unregister(long typeId, Delegate callback, UnityEngine.UIElements.TrickleDown useTrickleDown)
        {
            var list = ListFor(useTrickleDown);
            int index = IndexOf(list, typeId, callback);
            if (index < 0)
                return false;
            list.RemoveAt(index);
            return true;
        }

        public bool HasTrickleDownHandlers() => m_TrickleDown.Count > 0;

        public bool HasBubbleUpHandlers() => m_BubbleUp.Count > 0;

        private static int IndexOf(List<(long typeId, Delegate callback)> list, long typeId, Delegate callback)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].typeId == typeId && list[i].callback == callback)
                    return i;
            }
            return -1;
        }
    }
}
