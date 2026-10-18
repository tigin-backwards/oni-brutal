using System;
using System.Collections.Generic;
using System.Threading;

namespace UnityEngine.UIElements
{
    /// The data every UI Toolkit event carries: identity, propagation flags and target. Events
    /// are never dispatched in this runtime (no panel exists, see README-scope.cs); they are data
    /// holders with Unity's pooling and reference counting.
    public abstract partial class EventBase
    {
        const int BubblesBit = 1;
        const int TricklesDownBit = 2;
        const int SkipDisabledBit = 4;

        static long lastEventSerial;

        int propagationBits;
        bool stopped;
        bool stoppedImmediately;
        IEventHandler eventTarget;

        /// Whether the event came from an event pool (GetPooled) and goes back to it on release.
        internal bool FromPool;

        /// Holders of the event; a pooled event returns to its pool when the last one releases it.
        internal int References;

        /// Adds a holder; each must release the event with Dispose.
        internal void Hold() => References++;

        protected EventBase()
        {
            Init();
        }

        /// The type's id; EventBase&lt;T&gt; gives each event type its own. -1 for the base.
        public virtual long eventTypeId => -1;

        /// A fresh id each time the event is initialised, from one increasing counter.
        internal ulong InstanceSerial { get; private set; }

        /// The InstanceSerial of the event that caused this one, or 0.
        internal ulong TriggerSerial { get; set; }

        public long timestamp { get; protected set; }

        public bool bubbles => (propagationBits & BubblesBit) != 0;

        public bool tricklesDown => (propagationBits & TricklesDownBit) != 0;

        internal bool skipDisabledElements
        {
            get => (propagationBits & SkipDisabledBit) != 0;
            set => propagationBits = value ? propagationBits | SkipDisabledBit : propagationBits & ~SkipDisabledBit;
        }

        /// Sets whether the event bubbles up and trickles down, keeping skipDisabledElements.
        internal void SetPropagation(bool bubblesUp, bool trickleDown)
        {
            propagationBits = (propagationBits & SkipDisabledBit) | (bubblesUp ? BubblesBit : 0) | (trickleDown ? TricklesDownBit : 0);
        }

        /// The event's target. Only VisualElements can be targets and none exist here, so this
        /// stays null in practice.
        public IEventHandler target
        {
            get => eventTarget;
            set => eventTarget = value;
        }

        internal VisualElement elementTarget => eventTarget as VisualElement;

        public bool isPropagationStopped => stopped;

        public void StopPropagation() => stopped = true;

        public bool isImmediatePropagationStopped => stoppedImmediately;

        public void StopImmediatePropagation()
        {
            stopped = true;
            stoppedImmediately = true;
        }

        /// Resets the event to its initial state with a fresh id. An event reset while still
        /// held is reported and its holders are forgotten.
        protected virtual void Init()
        {
            if (References != 0)
            {
                Debug.Log("Event improperly released.");
                References = 0;
            }
            InstanceSerial = (ulong)Interlocked.Increment(ref lastEventSerial);
            TriggerSerial = 0;
            timestamp = 0;
            propagationBits = 0;
            stopped = false;
            stoppedImmediately = false;
            eventTarget = null;
            FromPool = false;
        }

        internal void ResetForPool() => Init();
    }

    public abstract partial class EventBase<T>
    {
        static long typeId;

        /// The id of event type T: ids are handed out in order of first use, starting at 1.
        public static long TypeId()
        {
            long id = Volatile.Read(ref typeId);
            if (id == 0)
            {
                Interlocked.CompareExchange(ref typeId, OniBrutal.Engine.UIEventTypeIds.Next(), 0);
                id = Volatile.Read(ref typeId);
            }
            return id;
        }

        public override long eventTypeId => TypeId();

        static readonly OniBrutal.Engine.UIEventPool<T> pool = new OniBrutal.Engine.UIEventPool<T>();

        /// Replaces the factory the pool uses when it is empty.
        protected static void SetCreateFunction(Func<T> createMethod) => pool.Factory = createMethod;

        /// An event of type T in its initial state, taken from the pool (or created) and held once;
        /// Dispose releases it.
        public static T GetPooled()
        {
            T e = pool.Take();
            e.ResetForPool();
            e.FromPool = true;
            e.References = 1;
            return e;
        }

        /// As GetPooled(), recording e as the event that caused the new one.
        internal static T GetPooled(EventBase e)
        {
            T evt = GetPooled();
            if (e != null)
                evt.TriggerSerial = e.InstanceSerial;
            return evt;
        }

        /// Drops one reference; a pooled event with none left is reset and returned to its pool.
        /// Events created with new are never put in a pool.
        public sealed override void Dispose()
        {
            if (References > 0)
                References--;
            if (References == 0 && FromPool)
            {
                ResetForPool();
                pool.Return((T)this);
            }
        }
    }
}

namespace OniBrutal.Engine
{
    using UnityEngine.UIElements;

    /// The counter behind EventBase&lt;T&gt;.TypeId(): one id per event type, from 1.
    internal static class UIEventTypeIds
    {
        static long last;

        public static long Next() => Interlocked.Increment(ref last);
    }

    /// A per-type stack of released UI Toolkit events, holding at most Capacity; releases
    /// beyond that drop the event.
    internal sealed class UIEventPool<T> where T : EventBase<T>, new()
    {
        public const int Capacity = 100;

        readonly Stack<T> idle = new Stack<T>();

        public Func<T> Factory = () => new T();

        public int Count => idle.Count;

        public T Take() => idle.Count > 0 ? idle.Pop() : Factory();

        public void Return(T e)
        {
            if (idle.Count < Capacity)
                idle.Push(e);
        }
    }
}
