using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements
{
    /// The base of every panel. Panels are created only by the panel system from PanelSettings
    /// and UIDocuments, which this runtime does not have (see README-scope.cs): no constructor is
    /// reachable, and the members answer as a panel without content would.
    internal abstract partial class BaseVisualElementPanel
    {
        private protected BaseVisualElementPanel()
        {
        }

        public abstract VisualElement visualTree { get; }

        public abstract EventDispatcher dispatcher { get; }

        public abstract ContextType contextType { get; }

        public abstract FocusController focusController { get; }

        public virtual ContextualMenuManager contextualMenuManager => null;

        public bool isDirty => false;

        /// Pixels per point at the panel's scale: 1, the unscaled default.
        public float scaledPixelsPerPoint => 1f;

        /// Whether the panel is drawn flat in screen space (not in the world).
        internal virtual bool isFlat => true;

        internal bool disposed { get; private set; }

        /// No element can be under a point: the panel has no content.
        public VisualElement Pick(Vector2 point) => null;

        /// As Pick; picked is left untouched.
        public VisualElement PickAll(Vector2 point, List<VisualElement> picked) => null;

        public virtual void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            PointerDeviceState.ForgetPanel(this);
        }
    }

    internal partial class Panel
    {
        private protected Panel()
        {
        }

        public override VisualElement visualTree => null;

        public override EventDispatcher dispatcher => RuntimePanel.s_EventDispatcher;

        public override ContextType contextType => ContextType.Player;

        /// Focus controllers are created with their panel's content; there is none.
        public override FocusController focusController => null;

        internal string name { get; private protected set; } = string.Empty;

        // Focus returns focus to the panel's last focused element through its focus controller;
        // a panel without content has neither, so there is nothing to restore.
        internal void Focus()
        {
        }
    }

    internal abstract partial class BaseRuntimePanel
    {
        Action destroyedHandlers;

        private protected BaseRuntimePanel()
        {
        }

        /// The GameObject uGUI selects while this panel has focus.
        internal GameObject selectableGameObject { get; set; }

        /// The sorting index among the player panels, resolved from the sorting priorities.
        internal int resolvedSortingIndex = 0;

        internal float sortingPriority { get; private protected set; }

        internal int targetDisplay { get; private protected set; }

        internal event Action destroyed
        {
            add => destroyedHandlers += value;
            remove => destroyedHandlers -= value;
        }

        /// Records the pointer at position over this panel.
        internal void PointerEntersPanel(int pointerId, Vector3 position)
        {
            PointerDeviceState.RecordPosition(pointerId, position, this, contextType);
        }

        public override void Dispose()
        {
            if (disposed)
                return;
            base.Dispose();
            destroyedHandlers?.Invoke();
        }
    }

    internal partial class RuntimePanel
    {
        /// The dispatcher shared by every runtime panel.
        internal static readonly EventDispatcher s_EventDispatcher = new EventDispatcher();

        private RuntimePanel()
        {
        }
    }

    /// Routes events to panels. Nothing is dispatched in this runtime; only its pointer state
    /// is read.
    public sealed partial class EventDispatcher
    {
        readonly PointerDispatchState pointers = new PointerDispatchState();

        internal EventDispatcher()
        {
        }

        internal PointerDispatchState pointerState => pointers;
    }

    /// Pointer capture per pointer. Only elements capture pointers and none exist, so no pointer
    /// is ever captured.
    internal partial class PointerDispatchState
    {
        internal IEventHandler GetCapturingElement(int pointerId) => null;
    }
}
