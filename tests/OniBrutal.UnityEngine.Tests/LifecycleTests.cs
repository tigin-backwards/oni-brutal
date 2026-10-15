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
