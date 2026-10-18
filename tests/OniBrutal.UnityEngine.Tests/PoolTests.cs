using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Xunit;

namespace OniBrutal.Tests
{
    public class PoolTests
    {
        sealed class Item
        {
        }

        [Fact]
        public void CountsTrackCreatedKeptAndActiveObjects()
        {
            int gets = 0, releases = 0;
            var pool = new ObjectPool<Item>(() => new Item(), _ => gets++, _ => releases++);
            var a = pool.Get();
            var b = pool.Get();
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(2, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);

            pool.Release(a);
            pool.Release(b);
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(2, pool.CountInactive);

            // The most recently released object comes back first, without creating another.
            Assert.Same(b, pool.Get());
            Assert.Same(a, pool.Get());
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(4, gets);
            Assert.Equal(2, releases);
        }

        [Fact]
        public void ReleasingTwiceThrowsWithTheCollectionCheck()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            var item = pool.Get();
            pool.Release(item);
            var e = Assert.Throws<InvalidOperationException>(() => pool.Release(item));
            Assert.Equal("Trying to release an object that has already been released to the pool.", e.Message);

            // Without the check the object is simply kept again.
            var lenient = new ObjectPool<Item>(() => new Item(), collectionCheck: false);
            var other = lenient.Get();
            lenient.Release(other);
            lenient.Release(other);
            Assert.Equal(2, lenient.CountInactive);
        }

        [Fact]
        public void ReleasesBeyondMaxSizeAreDestroyed()
        {
            var destroyed = new List<Item>();
            var pool = new ObjectPool<Item>(() => new Item(), actionOnDestroy: destroyed.Add, maxSize: 2);
            var items = new[] { pool.Get(), pool.Get(), pool.Get() };
            Assert.Equal(3, pool.CountAll);
            foreach (var item in items) pool.Release(item);
            Assert.Equal(2, pool.CountInactive);
            Assert.Equal(2, pool.CountAll);
            Assert.Equal([items[2]], destroyed);

            pool.Clear();
            Assert.Equal(3, destroyed.Count);
            Assert.Equal(0, pool.CountAll);
            Assert.Equal(0, pool.CountInactive);
        }

        [Fact]
        public void ConstructorValidatesItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<Item>(null));
            var e = Assert.Throws<ArgumentException>(() => new ObjectPool<Item>(() => new Item(), maxSize: 0));
            Assert.StartsWith("Max Size must be greater than 0", e.Message);
        }

        [Fact]
        public void PooledObjectReleasesOnDispose()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            Item held;
            using (pool.Get(out held))
            {
                Assert.NotNull(held);
                Assert.Equal(1, pool.CountActive);
            }
            Assert.Equal(0, pool.CountActive);
            Assert.Same(held, pool.Get());
        }

        [Fact]
        public void CollectionPoolClearsReleasedCollections()
        {
            var list = CollectionPool<List<int>, int>.Get();
            list.Add(5);
            CollectionPool<List<int>, int>.Release(list);
            var again = CollectionPool<List<int>, int>.Get();
            Assert.Same(list, again);
            Assert.Empty(again);
            using (CollectionPool<List<int>, int>.Get(out var scoped))
                scoped.Add(1);
            CollectionPool<List<int>, int>.Release(again);
        }
    }
}
