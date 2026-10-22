using System;
using System.Collections.Generic;
using OniBrutal.Engine;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    /// Component queries through shared component layouts (Components, ComponentLayout).
    public class ComponentQueryTests : EngineTest
    {
        interface IMarked { }
        class First : MonoBehaviour, IMarked { }
        class Second : MonoBehaviour { }
        class SecondChild : Second, IMarked { }

        [Fact]
        public void QueriesMatchInComponentOrderAndFollowChanges()
        {
            var go = new GameObject("a");
            var first = go.AddComponent<First>();
            var child = go.AddComponent<SecondChild>();
            Assert.Same(first, go.GetComponent<IMarked>());
            Assert.Same(child, go.GetComponent<Second>());
            Assert.Same(child, go.GetComponent(typeof(Second)));
            var all = new List<IMarked>();
            go.GetComponents(all);
            Assert.Equal(new IMarked[] { first, child }, all);
            Assert.Null(go.GetComponent<Light>());

            // A new component list (added, removed) gets its own layout.
            var second = go.AddComponent<Second>();
            var seconds = new List<Second>();
            go.GetComponents(seconds);
            Assert.Equal(new Second[] { child, second }, seconds);
            UnityEngine.Object.DestroyImmediate(first);
            Assert.Same(child, go.GetComponent<IMarked>());
        }

        [Fact]
        public void ObjectsWithTheSameComponentTypesShareMatches()
        {
            var a = new GameObject("a");
            a.AddComponent<First>();
            var b = new GameObject("b");
            var bFirst = b.AddComponent<First>();
            a.GetComponent<First>();
            Assert.Same(bFirst, b.GetComponent<First>());
        }

        [Fact]
        public void TheMatchTableGrowsWithQueryIdsNotWithQueries()
        {
            var go = new GameObject("a");
            go.AddComponent<First>();
            // Many distinct query types on one layout.
            var types = new List<Type>();
            foreach (var t in typeof(Component).Assembly.GetTypes())
                if (typeof(Component).IsAssignableFrom(t) && !t.IsGenericTypeDefinition) types.Add(t);
            foreach (var t in types) go.GetComponent(t);
            var native = (GameObjectNative)go.NativeOrThrow;
            // Ids are global: every query type seen so far in the run has one.
            int ids = QueryIds.Count;
            Assert.True(native.Layout.Layout.TableLength <= 2 * Math.Max(ids, types.Count) + 2,
                $"{native.Layout.Layout.TableLength} entries for {types.Count} query types");
        }
    }
}
