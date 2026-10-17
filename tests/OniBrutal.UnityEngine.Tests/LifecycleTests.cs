using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class Recorder : MonoBehaviour
    {
        public static readonly List<string> Log = new();
        public string label = "";
        void Awake() => Log.Add(label + ":Awake");
        void OnEnable() => Log.Add(label + ":OnEnable");
        void Start() => Log.Add(label + ":Start");
        void Update() => Log.Add(label + ":Update");
        void OnDisable() => Log.Add(label + ":OnDisable");
        void OnDestroy() => Log.Add(label + ":OnDestroy");
    }

    /// Looks up a sibling from OnDestroy, like the game's SaveLoadRoot (KPrefabID).
    public class SiblingChecker : MonoBehaviour
    {
        public static bool SawSibling;
        void OnDestroy() => SawSibling = GetComponent<Recorder>() != null && GetComponentInChildren<Base>(true) != null;
    }

    /// Records whether another object is still alive in its own OnDisable and OnDestroy.
    public class PeerWatcher : MonoBehaviour
    {
        public string label = "";
        public Object peer;
        void OnDisable() => Recorder.Log.Add(label + ":OnDisable peer " + (peer == null ? "gone" : "alive"));
        void OnDestroy() => Recorder.Log.Add(label + ":OnDestroy peer " + (peer == null ? "gone" : "alive"));
    }

    public class LateRecorder : MonoBehaviour
    {
        void Start() => Recorder.Log.Add("late:Start");
        void LateUpdate() => Recorder.Log.Add("late:LateUpdate");
    }

    /// Instantiates a LateRecorder from its Update, once.
    public class Spawner : MonoBehaviour
    {
        bool done;
        void Update()
        {
            if (done) return;
            done = true;
            new GameObject("spawned").AddComponent<LateRecorder>();
            Recorder.Log.Add("spawned");
        }
    }

    /// A Klei-style tag: only the name is serialized; the hash is rebuilt after deserialization.
    [System.Serializable]
    public struct HashedName : ISerializationCallbackReceiver
    {
        [SerializeField] string name;
        int hash;
        public HashedName(string n) { name = n; hash = n.GetHashCode(); }
        public int Hash => hash;
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => hash = name?.GetHashCode() ?? 0;
    }

    public class HashedNameHolder : MonoBehaviour
    {
        public HashedName single;
        public System.Collections.Generic.List<HashedName> more = new();
    }

    /// Awakes before default-order scripts (script execution order -5, set by the test).
    public class EarlyRecorder : MonoBehaviour
    {
        void Awake() => Recorder.Log.Add("early:Awake");
    }

    public class LateStarter : MonoBehaviour
    {
        void Start() => Recorder.Log.Add("late:Start");
    }

    public class EarlyStarter : MonoBehaviour
    {
        void Start() => Recorder.Log.Add("early:Start");
    }

    public class Base : MonoBehaviour
    {
        public static readonly List<string> Log = new();
        protected void Awake() => Log.Add("Base.Awake");
    }

    public class Derived : Base
    {
        new void Awake() => Log.Add("Derived.Awake");
    }

    public class LifecycleTests : EngineTest
    {
        public LifecycleTests() => Recorder.Log.Clear();

        [Fact]
        public void AddComponentAwakensThenStartsBeforeFirstUpdate()
        {
            var go = new GameObject("a");
            var r = go.AddComponent<Recorder>();
            Assert.Equal(["Awake", "OnEnable"], Recorder.Log.ConvertAll(s => s.Split(':')[1]));
            Frame();
            Assert.Equal(["Awake", "OnEnable", "Start", "Update"], Recorder.Log.ConvertAll(s => s.Split(':')[1]));
        }

        [Fact]
        public void OnlyTheMostDerivedMessageRuns()
        {
            Base.Log.Clear();
            new GameObject("d").AddComponent<Derived>();
            Assert.Equal(["Derived.Awake"], Base.Log);
        }

        [Fact]
        public void InactiveObjectsAreNotAwakenedUntilActivated()
        {
            var go = new GameObject("a");
            go.SetActive(false);
            go.AddComponent<Recorder>();
            Assert.Empty(Recorder.Log);
            go.SetActive(true);
            Assert.Equal([":Awake", ":OnEnable"], Recorder.Log);
            go.SetActive(false);
            Assert.Equal(":OnDisable", Recorder.Log[^1]);
        }

        [Fact]
        public void DisabledBehaviourIsAwakenedButNotEnabled()
        {
            var go = new GameObject("a");
            go.SetActive(false);
            var r = go.AddComponent<Recorder>();
            r.enabled = false;
            go.SetActive(true);
            Assert.Equal([":Awake"], Recorder.Log);
            Frame();
            Assert.Equal([":Awake"], Recorder.Log); // no Start or Update while disabled
            r.enabled = true;
            Frame();
            Assert.Equal([":Awake", ":OnEnable", ":Start", ":Update"], Recorder.Log);
        }

        [Fact]
        public void ChildActivationFollowsParent()
        {
            var parent = new GameObject("p");
            var child = new GameObject("c");
            child.transform.SetParent(parent.transform);
            child.AddComponent<Recorder>().label = "c";
            parent.SetActive(false);
            Assert.False(child.activeInHierarchy);
            Assert.True(child.activeSelf);
            Assert.Equal("c:OnDisable", Recorder.Log[^1]);
            parent.SetActive(true);
            Assert.Equal("c:OnEnable", Recorder.Log[^1]);
        }

        [Fact]
        public void DestroyIsDeferredAndFakeNull()
        {
            var go = new GameObject("a");
            go.AddComponent<Recorder>();
            Object.Destroy(go);
            Assert.True(go != null); // still alive until the end of the frame
            Frame();
            Assert.True(go == null);
            Assert.False(go);
            Assert.Equal(0, go.GetInstanceID());
            Assert.Contains(":OnDestroy", Recorder.Log);
        }

        // Destroy's timing as observed in Unity's player (a probe mod in the original game):
        // behaviours are disabled at the call, destroyed at the end of the frame in request order.

        [Fact]
        public void DestroyDisablesTheObjectAndItsChildrenAtOnceAndDeeperOnesAtDestruction()
        {
            Recorder Make(string label, Transform parent)
            {
                var go = new GameObject(label);
                go.transform.SetParent(parent);
                var r = go.AddComponent<Recorder>();
                r.label = label;
                return r;
            }
            var d0 = Make("d0", null);
            var d1 = Make("d1", d0.transform);
            var d2 = Make("d2", d1.transform);
            var d3 = Make("d3", d2.transform);
            Recorder.Log.Clear();
            Object.Destroy(d0.gameObject);
            Assert.Equal(["d0:OnDisable", "d1:OnDisable"], Recorder.Log);
            Assert.True(d0.gameObject.activeSelf);
            Assert.True(d1.gameObject.activeInHierarchy);
            Assert.False(d0.enabled);
            Assert.False(d1.isActiveAndEnabled);
            Assert.True(d3.isActiveAndEnabled);
            Frame();
            // d2 and d3 stay enabled for the rest of the frame (Start, Update).
            Assert.Equal(["d0:OnDisable", "d1:OnDisable", "d3:OnDisable", "d2:OnDisable", "d0:OnDestroy", "d1:OnDestroy", "d2:OnDestroy", "d3:OnDestroy"],
                Recorder.Log.FindAll(e => e.EndsWith(":OnDisable") || e.EndsWith(":OnDestroy")));
        }

        [Fact]
        public void ObjectsAwaitingDestructionAreNotEnabledAgain()
        {
            var parent = new GameObject("p");
            var child = new GameObject("x");
            child.transform.SetParent(parent.transform);
            var x = child.AddComponent<Recorder>(); x.label = "x";
            Recorder.Log.Clear();
            Object.Destroy(child);
            parent.SetActive(false);
            parent.SetActive(true);
            x.enabled = true;
            Assert.Equal(["x:OnDisable"], Recorder.Log);
            Assert.False(x.isActiveAndEnabled);
            Frame();
            Assert.Equal(["x:OnDisable", "x:OnDestroy"], Recorder.Log);
        }

        [Fact]
        public void DestroyingAComponentDisablesItAtOnce()
        {
            var go = new GameObject("a");
            var r = go.AddComponent<Recorder>();
            Recorder.Log.Clear();
            Object.Destroy(r);
            Assert.Equal([":OnDisable"], Recorder.Log);
            Assert.False(r.enabled);
            Frame();
            Assert.Equal([":OnDisable", ":OnDestroy"], Recorder.Log);
            Assert.True(r == null);
            Assert.True(go != null);
        }

        [Fact]
        public void PendingDestructionsRunInRequestOrderOneAfterAnother()
        {
            var b = new GameObject("b").AddComponent<PeerWatcher>(); b.label = "b";
            var c = new GameObject("c").AddComponent<PeerWatcher>(); c.label = "c";
            b.peer = c; c.peer = b;
            Object.Destroy(b.gameObject);
            Object.Destroy(c.gameObject);
            Frame();
            Assert.Equal(["b:OnDisable peer alive", "c:OnDisable peer alive", "b:OnDestroy peer alive", "c:OnDestroy peer gone"], Recorder.Log);
        }

        [Fact]
        public void DestroyImmediateRunsDisableThenDestroy()
        {
            var go = new GameObject("a");
            go.AddComponent<Recorder>();
            Object.DestroyImmediate(go);
            Assert.Equal([":Awake", ":OnEnable", ":OnDisable", ":OnDestroy"], Recorder.Log);
            Assert.True(go == null);
        }

        [Fact]
        public void GetComponentMatchesInterfacesAndSkipsInactiveChildren()
        {
            var parent = new GameObject("p");
            var child = new GameObject("c", typeof(Recorder));
            child.transform.SetParent(parent.transform);
            Assert.NotNull(parent.GetComponentInChildren<Recorder>());
            child.SetActive(false);
            Assert.Null(parent.GetComponentInChildren<Recorder>());
            Assert.NotNull(parent.GetComponentInChildren<Recorder>(true));
            Assert.NotNull(child.GetComponent<System.IDisposable>() == null ? child.transform : null);
        }

        [Fact]
        public void OnDestroySeesTheWholeSubtreeAlive()
        {
            var go = new GameObject("root", typeof(Recorder), typeof(SiblingChecker));
            var child = new GameObject("child", typeof(Base));
            child.transform.SetParent(go.transform);
            SiblingChecker.SawSibling = false;
            Object.DestroyImmediate(go);
            Assert.True(SiblingChecker.SawSibling);
            Assert.True(go == null);
            Assert.True(child == null);
        }

        [Fact]
        public void BehaviourCreatedDuringUpdateStartsBeforeLateUpdate()
        {
            Recorder.Log.Clear();
            var go = new GameObject("spawner", typeof(Spawner));
            Frame();
            // Unity's delayed-start pass after Update: Start, then the same frame's LateUpdate.
            Assert.Equal(["spawned", "late:Start", "late:LateUpdate"], Recorder.Log);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(GameObject.Find("spawned"));
        }

        [Fact]
        public void InstantiateCallsCallbacksOnNestedSerializedValues()
        {
            var go = new GameObject("holder");
            var h = go.AddComponent<HashedNameHolder>();
            h.single = new HashedName("Minion");
            h.more.Add(new HashedName("BaseMinion"));
            var copy = Object.Instantiate(go).GetComponent<HashedNameHolder>();
            Assert.Equal(h.single.Hash, copy.single.Hash);
            Assert.Equal(h.more[0].Hash, copy.more[0].Hash);
            Object.DestroyImmediate(copy.gameObject);
            Object.DestroyImmediate(go);
        }

        [Fact]
        public void ActivationAwakesScriptsInExecutionOrder()
        {
            OniBrutal.Engine.ScriptInfo.SetExecutionOrder(typeof(EarlyRecorder).FullName!, -5);
            Recorder.Log.Clear();
            var root = new GameObject("root");
            root.SetActive(false);
            var child = new GameObject("child");
            child.transform.SetParent(root.transform);
            child.AddComponent<Recorder>().label = "a";
            child.AddComponent<EarlyRecorder>();
            root.SetActive(true);
            // The later component with the lower order awakes first, across the whole batch.
            Assert.Equal(["early:Awake", "a:Awake", "a:OnEnable"], Recorder.Log);
            Object.DestroyImmediate(root);
        }

        [Fact]
        public void PendingStartsRunInTheOrderTheyWereScheduled()
        {
            // As in the original player loading a save: World (order 6) is enabled before Game
            // (order -10) and starts first; execution order does not reorder pending Starts.
            OniBrutal.Engine.ScriptInfo.SetExecutionOrder(typeof(LateStarter).FullName!, 6);
            OniBrutal.Engine.ScriptInfo.SetExecutionOrder(typeof(EarlyStarter).FullName!, -10);
            Recorder.Log.Clear();
            var late = new GameObject("late", typeof(LateStarter));
            var early = new GameObject("early", typeof(EarlyStarter));
            OniBrutal.Engine.BehaviourManager.RunStarts();
            Assert.Equal(["late:Start", "early:Start"], Recorder.Log);
            Object.DestroyImmediate(late);
            Object.DestroyImmediate(early);
        }

        [Fact]
        public void ChildSearchJudgesActivityRelativeToTheStartingObject()
        {
            // root (inactive) → a (active) → b (Recorder); root → c (inactive) → d (Recorder).
            var root = new GameObject("root");
            var a = new GameObject("a");
            a.transform.SetParent(root.transform);
            var b = new GameObject("b", typeof(Recorder));
            b.transform.SetParent(a.transform);
            var c = new GameObject("c");
            c.transform.SetParent(root.transform);
            var d = new GameObject("d", typeof(Recorder));
            d.transform.SetParent(c.transform);
            c.SetActive(false);
            root.SetActive(false);
            Assert.Same(b.GetComponent<Recorder>(), root.GetComponentInChildren<Recorder>());
            Assert.Single(root.GetComponentsInChildren<Recorder>());
            Assert.Equal(2, root.GetComponentsInChildren<Recorder>(true).Length);
            // The starting object is searched even when it is inactive itself.
            Assert.NotNull(d.GetComponentInChildren<Recorder>());
            Object.DestroyImmediate(root);
        }

        [Fact]
        public void SetParentKeepsWorldPosition()
        {
            var parent = new GameObject("p");
            parent.transform.position = new Vector3(10, 0, 0);
            parent.transform.localScale = new Vector3(2, 2, 2);
            var child = new GameObject("c");
            child.transform.position = new Vector3(4, 0, 0);
            child.transform.SetParent(parent.transform, true);
            Assert.Equal(new Vector3(-3, 0, 0), child.transform.localPosition);
            Assert.True(child.transform.position == new Vector3(4, 0, 0));
            Assert.Equal(child.transform, parent.transform.Find("c"));
        }
    }

    public class CoroutineTests : EngineTest
    {
        public class Runner : MonoBehaviour
        {
            public readonly List<string> Log = new();

            public IEnumerator Steps()
            {
                Log.Add("start");
                yield return null;
                Log.Add("after null");
                yield return Nested();
                Log.Add("after nested");
            }

            IEnumerator Nested()
            {
                Log.Add("nested");
                yield return null;
                Log.Add("nested resumed");
            }
        }

        [Fact]
        public void CoroutinesRunSynchronouslyToFirstYieldAndNest()
        {
            var r = new GameObject("r").AddComponent<Runner>();
            r.StartCoroutine(r.Steps());
            Assert.Equal(["start"], r.Log);
            Frame();
            Assert.Equal(["start", "after null", "nested"], r.Log);
            Frame();
            Assert.Equal(["start", "after null", "nested", "nested resumed", "after nested"], r.Log);
        }

        public class UpdateStarter : MonoBehaviour
        {
            public readonly List<string> Log = new();
            bool started;

            void Update()
            {
                Log.Add("update " + Time.frameCount);
                if (started) return;
                started = true;
                StartCoroutine(Co());
            }

            IEnumerator Co()
            {
                Log.Add("co start " + Time.frameCount);
                yield return null;
                Log.Add("co resumed " + Time.frameCount);
            }
        }

        [Fact]
        public void YieldNullFromACoroutineStartedInUpdateResumesNextFrame()
        {
            // Observed in Unity's player (tools/UnityProbe): the coroutine pass after Update does
            // not resume a wait that began earlier in the same frame.
            var r = new GameObject("r").AddComponent<UpdateStarter>();
            Frame();
            int f = Time.frameCount;
            Frame();
            Assert.Equal([$"update {f}", $"co start {f}", $"update {f + 1}", $"co resumed {f + 1}"], r.Log);
        }

        public class CoroutineSpawner : MonoBehaviour
        {
            public IEnumerator Spawn()
            {
                yield return null;
                new GameObject("late").AddComponent<LateRecorder>();
                Recorder.Log.Add("spawned " + Time.frameCount);
            }
        }

        [Fact]
        public void BehavioursCreatedByCoroutinesStartAndLateUpdateThatFrame()
        {
            Recorder.Log.Clear();
            var s = new GameObject("s").AddComponent<CoroutineSpawner>();
            s.StartCoroutine(s.Spawn());
            Frame();
            Assert.Equal([$"spawned {Time.frameCount}", "late:Start", "late:LateUpdate"], Recorder.Log);
        }

        [Fact]
        public void DeactivationStopsCoroutines()
        {
            var r = new GameObject("r").AddComponent<Runner>();
            r.StartCoroutine(r.Steps());
            r.gameObject.SetActive(false);
            r.gameObject.SetActive(true);
            Frame();
            Assert.Equal(["start"], r.Log);
        }

        [Fact]
        public void SharedWaitForSecondsRealtimeInstanceWaits()
        {
            var wait = new WaitForSecondsRealtime(10f);
            Assert.True(wait.keepWaiting);
            Assert.True(wait.MoveNext());
        }
    }

    public class CloneTests : EngineTest
    {
        [System.Serializable]
        public class Settings
        {
            public int value = 1;
            public List<string> names;
        }

        public class Holder : MonoBehaviour
        {
            public int number;
            [SerializeField] private string secret;
            public Settings settings;
            public Settings nullSettings;
            public GameObject child;
            public GameObject outside;
            [System.NonSerialized] public int transientValue = 7;
            public int notSerializedProperty { get; set; }
            public string Secret { get => secret; set => secret = value; }
        }

        [Fact]
        public void InstantiateCopiesSerializedStateAndRemapsInternalReferences()
        {
            var outside = new GameObject("outside");
            var root = new GameObject("root");
            var child = new GameObject("child");
            child.transform.SetParent(root.transform);
            var h = root.AddComponent<Holder>();
            h.number = 5;
            h.Secret = "s";
            h.settings = new Settings { value = 9, names = new List<string> { "x" } };
            h.child = child;
            h.outside = outside;
            h.transientValue = 99;
            h.notSerializedProperty = 3;

            var clone = Object.Instantiate(h);
            Assert.NotSame(h, clone);
            Assert.Equal("root(Clone)", clone.name);
            Assert.Equal(5, clone.number);
            Assert.Equal("s", clone.Secret);
            Assert.NotSame(h.settings, clone.settings);
            Assert.Equal(9, clone.settings.value);
            Assert.Equal(["x"], clone.settings.names);
            Assert.NotNull(clone.nullSettings);            // Unity serialization has no null for classes
            Assert.Equal(1, clone.nullSettings.value);
            Assert.NotSame(child, clone.child);
            Assert.Equal(clone.transform.GetChild(0).gameObject, clone.child);
            Assert.Same(outside, clone.outside);
            Assert.Equal(7, clone.transientValue);         // initializer value, not copied
            Assert.Equal(0, clone.notSerializedProperty);
        }

        [Fact]
        public void InstantiateUnderParentWithLocalValues()
        {
            var parent = new GameObject("p");
            parent.transform.position = new Vector3(100, 0, 0);
            var prefab = new GameObject("prefab");
            prefab.transform.localPosition = new Vector3(1, 2, 3);
            var clone = Object.Instantiate(prefab, parent.transform, false);
            Assert.Equal(parent.transform, clone.transform.parent);
            Assert.Equal(new Vector3(1, 2, 3), clone.transform.localPosition);
        }
    }
}
