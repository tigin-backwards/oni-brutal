using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests
{
    [DisallowMultipleComponent] public class RefusalBase : MonoBehaviour { }
    public class RefusalA : RefusalBase { }
    public class RefusalB : RefusalBase { }
    [DisallowMultipleComponent] public class RefusalOwn : RefusalA { }
    public class RefusalPlain : MonoBehaviour { }
    [DisallowMultipleComponent] public class RefusalMid : RefusalPlain { }
    public class RefusalLeaf : RefusalMid { }

    /// AddComponent's refusals as UnityProbe measured them in the original player: a
    /// LogType.Log message with no stack trace, to both log callbacks, and null back. Favorite
    /// Resources adds a second VerticalLayoutGroup; the game's crash reporter opens only on
    /// errors, so an error here showed its crash screen.
    public class AddComponentRefusalTests : EngineTest
    {
        static List<(string message, string stack, LogType type, bool threaded)> Logged(Action action)
        {
            var logged = new List<(string, string, LogType, bool)>();
            Application.LogCallback main = (m, s, t) => logged.Add((m, s, t, false));
            Application.LogCallback threaded = (m, s, t) => logged.Add((m, s, t, true));
            Application.logMessageReceived += main;
            Application.logMessageReceivedThreaded += threaded;
            try { action(); }
            finally
            {
                Application.logMessageReceived -= main;
                Application.logMessageReceivedThreaded -= threaded;
            }
            return logged;
        }

        static void AssertRefused(GameObject go, Type added, string message, params Type[] left)
        {
            Component got = null;
            var logged = Logged(() => got = go.AddComponent(added));
            Assert.True(ReferenceEquals(got, null));
            Assert.Equal([(message, "", LogType.Log, true), (message, "", LogType.Log, false)], logged);
            Assert.Equal(left, Array.ConvertAll(go.GetComponents<Component>(), c => c.GetType()));
        }

        static GameObject With(string name, params Type[] types)
        {
            var go = new GameObject(name);
            foreach (var t in types) go.AddComponent(t);
            return go;
        }

        [Fact]
        public void ASecondLayoutGroupIsRefusedNamingTheAttributesClass()
        {
            AssertRefused(With("EntryContainer", typeof(VerticalLayoutGroup)), typeof(VerticalLayoutGroup),
                "Can't add 'VerticalLayoutGroup' to EntryContainer because a 'VerticalLayoutGroup' is already added to the game object!\n" +
                "A GameObject can only contain one 'LayoutGroup' component.",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            AssertRefused(With("H", typeof(HorizontalLayoutGroup)), typeof(VerticalLayoutGroup),
                "Can't add 'VerticalLayoutGroup' to H because a 'HorizontalLayoutGroup' is already added to the game object!\n" +
                "A GameObject can only contain one 'LayoutGroup' component.",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
        }

        [Fact]
        public void TheSecondLineAppearsWhenTheComponentInTheWayIsNotTheDeclaringClass()
        {
            AssertRefused(With("a", typeof(RefusalA)), typeof(RefusalB),
                "Can't add 'RefusalB' to a because a 'RefusalA' is already added to the game object!\nA GameObject can only contain one 'RefusalBase' component.",
                typeof(Transform), typeof(RefusalA));
            AssertRefused(With("base", typeof(RefusalBase)), typeof(RefusalA),
                "Can't add 'RefusalA' to base because a 'RefusalBase' is already added to the game object!",
                typeof(Transform), typeof(RefusalBase));
            AssertRefused(With("leaf", typeof(RefusalLeaf)), typeof(RefusalMid),
                "Can't add 'RefusalMid' to leaf because a 'RefusalLeaf' is already added to the game object!\nA GameObject can only contain one 'RefusalMid' component.",
                typeof(Transform), typeof(RefusalLeaf));
            // With the attribute on two classes of the chain, the most basic one is named.
            AssertRefused(With("own", typeof(RefusalOwn)), typeof(RefusalOwn),
                "Can't add 'RefusalOwn' to own because a 'RefusalOwn' is already added to the game object!\nA GameObject can only contain one 'RefusalBase' component.",
                typeof(Transform), typeof(RefusalOwn));
        }

        [Fact]
        public void ClassesOutsideTheAttributesReachAreAddedSilently()
        {
            var go = With("p", typeof(RefusalMid));
            var logged = Logged(() =>
            {
                Assert.NotNull(go.AddComponent<RefusalPlain>());
                Assert.NotNull(go.AddComponent<RefusalPlain>());
                Assert.NotNull(go.AddComponent<BoxCollider>());
                Assert.NotNull(go.AddComponent<BoxCollider>());
            });
            Assert.Empty(logged);
            Assert.Equal(6, go.GetComponents<Component>().Length);
        }

        [Fact]
        public void TheSameTransformOrSingleEngineComponentAgainIsRefused()
        {
            AssertRefused(new GameObject("t"), typeof(Transform),
                "Can't add component 'Transform' to t because such a component is already added to the game object!", typeof(Transform));
            AssertRefused(new GameObject("r", typeof(RectTransform)), typeof(RectTransform),
                "Can't add component 'RectTransform' to r because such a component is already added to the game object!", typeof(RectTransform));
            AssertRefused(With("c", typeof(RectTransform), typeof(Canvas)), typeof(Canvas),
                "Can't add component 'Canvas' to c because such a component is already added to the game object!", typeof(RectTransform), typeof(Canvas));
        }

        [Fact]
        public void ATransformReplacesARectTransformInPlace()
        {
            var go = new GameObject("g", typeof(RectTransform));
            var child = new GameObject("child");
            child.transform.SetParent(go.transform, false);
            go.transform.localPosition = new Vector3(1, 2, 3);
            var image = go.AddComponent<Image>();
            var rect = go.transform;
            Transform got = null;
            Assert.Empty(Logged(() => got = go.AddComponent<Transform>()));
            Assert.Equal(typeof(Transform), got.GetType());
            Assert.Same(got, go.transform);
            Assert.True(rect == null);
            Assert.Same(got, image.transform);
            Assert.Same(got, child.transform.parent);
            Assert.Equal(new Vector3(1, 2, 3), got.localPosition);
            Assert.Equal([typeof(Transform), typeof(CanvasRenderer), typeof(Image)], Array.ConvertAll(go.GetComponents<Component>(), c => c.GetType()));
        }
    }
}
