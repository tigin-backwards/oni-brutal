using System;
using System.Collections;
using System.Runtime.InteropServices;
using OniBrutal.Engine;

namespace UnityEngine
{
    public partial class Behaviour
    {
        internal BehaviourNative BehaviourNative => (BehaviourNative)NativeOrThrow;

        public bool enabled
        {
            get => BehaviourNative is { DestroyPending: false } bn && bn.Enabled;
            set
            {
                Threads.AssertMainThread("set_enabled");
                var bn = BehaviourNative;
                if (bn.DestroyPending || bn.Enabled == value) return;
                bn.Enabled = value;
                if (!bn.GameObject.ActiveInHierarchy) return;
                if (value) Lifecycle.AwakeAndEnable(this);
                else Lifecycle.Disable(this);
            }
        }

        public bool isActiveAndEnabled
        {
            get
            {
                var bn = Native as BehaviourNative;
                return bn != null && bn.IsActiveAndEnabledState;
            }
        }
    }

    public partial class MonoBehaviour
    {
        public bool didAwake => BehaviourNative.AwakeCalled;
        public bool didStart => BehaviourNative.StartCalled;

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            Threads.AssertMainThread("StartCoroutine");
            return Coroutines.Start(this, routine);
        }

        public Coroutine StartCoroutine(string methodName) => StartCoroutine(methodName, null, false);
        public Coroutine StartCoroutine(string methodName, object value) => StartCoroutine(methodName, value, true);

        Coroutine StartCoroutine(string methodName, object value, bool hasValue)
        {
            Threads.AssertMainThread("StartCoroutine");
            if (string.IsNullOrEmpty(methodName)) throw new NullReferenceException("methodName is null or empty");
            var method = ScriptInfo.FindByName(GetType(), methodName, value, hasValue);
            if (method == null || !typeof(IEnumerator).IsAssignableFrom(method.ReturnType))
            {
                Debug.LogError($"Coroutine '{methodName}' couldn't be started!", this);
                return null;
            }
            var routine = (IEnumerator)method.Invoke(this, method.GetParameters().Length == 0 ? null : new[] { value });
            return Coroutines.Start(this, routine, methodName);
        }

        public void StopCoroutine(IEnumerator routine)
        {
            if (routine == null) throw new NullReferenceException("routine is null");
            Coroutines.StopByEnumerator(BehaviourNative, routine);
        }

        public void StopCoroutine(Coroutine routine)
        {
            if (routine == null) throw new NullReferenceException("routine is null");
            Coroutines.Stop(routine.State);
        }

        public void StopCoroutine(string methodName) => Coroutines.StopByName(BehaviourNative, methodName);

        public void StopAllCoroutines() => Coroutines.StopAll(this, BehaviourNative);

        public static void print(object message) => Debug.Log(message);
    }

    public partial class YieldInstruction
    {
    }

    public partial class Coroutine
    {
        // Unity's field: a reference to the engine-side coroutine. Here a weak GCHandle: the
        // scheduler keeps a running coroutine alive; a finished one may be collected, after which
        // the handle reads as finished.
        internal IntPtr m_Ptr;

        internal Coroutine(CoroutineState state) => m_Ptr = GCHandle.ToIntPtr(GCHandle.Alloc(state, GCHandleType.Weak));

        internal CoroutineState State => m_Ptr == IntPtr.Zero ? null : (CoroutineState)GCHandle.FromIntPtr(m_Ptr).Target;

        ~Coroutine()
        {
            if (m_Ptr != IntPtr.Zero) GCHandle.FromIntPtr(m_Ptr).Free();
        }
    }

    public partial class WaitForSeconds
    {
        internal float m_Seconds;
        public WaitForSeconds(float seconds) => m_Seconds = seconds;
    }

    public partial class WaitForFixedUpdate
    {
    }

    public partial class WaitForEndOfFrame
    {
    }

    /// A yield instruction driven by polling keepWaiting each frame. When yielded it runs as a
    /// nested enumerator: MoveNext returns keepWaiting and Current is null.
    public partial class CustomYieldInstruction
    {
        protected CustomYieldInstruction() { }

        public object Current => null;
        public bool MoveNext() => keepWaiting;
        public virtual void Reset() { }
    }


}
