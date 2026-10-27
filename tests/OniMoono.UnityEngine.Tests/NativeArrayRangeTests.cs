using System;
using System.IO;
using Unity.Collections;
using Xunit;

namespace OniMoono.Tests
{
    // Unity's release player range-checks nothing on NativeArray: the indexer, Copy, CopyFrom,
    // CopyTo and GetSubArray all reach the memory before or past an array without an exception
    // (measured with tools/UnityProbe/NativeRanges.cs, ARCHITECTURE D46), so the original carries
    // on where a game or mod does; e.g. RangeVisualizerEffect writes past an occlusion texture's
    // GetPixelData array with Zoned Auto Sweeper's zone mode. Ours drops the out-of-range part,
    // never throws for a range, and logs the first one in the process.
    [Collection("Console")]
    public class NativeArrayRangeTests
    {
        static string Logged(Action action)
        {
            var stdout = Console.Out;
            var writer = new StringWriter();
            NativeArrayRange.ResetForTests();
            try
            {
                Console.SetOut(writer);
                action();
            }
            finally
            {
                Console.SetOut(stdout);
            }
            return writer.ToString();
        }

        static NativeArray<int> Numbers(params int[] values)
        {
            var array = NativeArray<int>.Allocate(values.Length);
            array.CopyFrom(values);
            return array;
        }

        [Fact]
        public void InRangeAccessIsUnchanged()
        {
            var array = Numbers(1, 2, 3);
            var log = Logged(() =>
            {
                array[1] = 20;
                Assert.Equal(20, array[1]);
                Assert.Equal([1, 20, 3], array.ToArray());
            });
            Assert.DoesNotContain("NativeArray:", log);
            array.Dispose();
        }

        [Fact]
        public void OutOfRangeWritesAreDroppedAndReadsGiveDefault()
        {
            var array = Numbers(1, 2, 3);
            var log = Logged(() =>
            {
                array[3] = 9;
                array[-1] = 9;
                array[int.MaxValue] = 9;
                Assert.Equal(0, array[3]);
                Assert.Equal(0, array[-5]);
            });
            Assert.Equal([1, 2, 3], array.ToArray());
            Assert.Contains("write to index 3 in a NativeArray<Int32> of length 3 is dropped", log);
            Assert.Contains("Unity's release player doesn't check", log);
            Assert.Single(log.Split('\n'), l => l.StartsWith("[engine] NativeArray:"));
            array.Dispose();
        }

        [Fact]
        public void OnlyTheFirstInTheProcessIsLogged()
        {
            var array = Numbers(1);
            Assert.Contains("read of index 2", Logged(() => _ = array[2]));
            var stdout = Console.Out;
            var writer = new StringWriter();
            try
            {
                Console.SetOut(writer);
                array[5] = 1;
                _ = array[5];
            }
            finally
            {
                Console.SetOut(stdout);
            }
            Assert.DoesNotContain("NativeArray:", writer.ToString());
            array.Dispose();
        }

        [Fact]
        public void ADisposedArrayStillThrows()
        {
            var array = Numbers(1, 2);
            array.Dispose();
            Assert.Throws<IndexOutOfRangeException>(() => array[0]);
            Assert.Throws<IndexOutOfRangeException>(() => array[0] = 1);
            Assert.Throws<ObjectDisposedException>(() => array.Dispose());
        }

        [Fact]
        public void CopiesCopyOnlyTheInRangePart()
        {
            var array = Numbers(1, 2, 3, 4);
            var managed = new[] { 10, 20, 30 };
            var log = Logged(() =>
            {
                // Past the end of the destination: the first two land.
                NativeArray<int>.Copy(managed, 0, array, 2, 3);
                Assert.Equal([1, 2, 10, 20], array.ToArray());
                // Before the start of the source: the part from index 0 lands.
                NativeArray<int>.Copy(managed, -1, array, 0, 3);
                Assert.Equal([1, 10, 20, 20], array.ToArray());
                var back = new int[2];
                NativeArray<int>.Copy(array, 3, back, 0, 2);
                Assert.Equal([20, 0], back);
                NativeArray<int>.Copy(array, 0, array, 0, -1);
                Assert.Equal([1, 10, 20, 20], array.ToArray());
                // The two-array overloads copy the source's length, as in Unity's player.
                NativeArray<int>.Copy(new[] { 7, 8 }, array);
                Assert.Equal([7, 8, 20, 20], array.ToArray());
                var shorter = new int[3];
                NativeArray<int>.Copy(array, shorter);
                Assert.Equal([7, 8, 20], shorter);
            });
            Assert.Contains("copy of 3 elements from index 0 of 3 to index 2 of 4", log);
            Assert.Contains("copies only the 2 in range", log);
            array.Dispose();
        }

        [Fact]
        public void CopyFromAndCopyToWithOtherLengthsCopyTheOverlap()
        {
            var array = Numbers(1, 2, 3);
            Logged(() =>
            {
                array.CopyFrom([7, 8]);
                Assert.Equal([7, 8, 3], array.ToArray());
                array.CopyFrom([4, 5, 6, 7]);
                Assert.Equal([4, 5, 6], array.ToArray());
                var shorter = new int[2];
                array.CopyTo(shorter);
                Assert.Equal([4, 5], shorter);
                var longer = new int[4];
                array.CopyTo(longer);
                Assert.Equal([4, 5, 6, 0], longer);
            });
            Assert.Throws<ArgumentNullException>(() => array.CopyFrom(null));
            array.Dispose();
        }

        [Fact]
        public void ASubArrayPastTheEndIsCut()
        {
            var array = Numbers(1, 2, 3, 4);
            var log = Logged(() =>
            {
                var tail = array.GetSubArray(2, 5);
                Assert.Equal([3, 4], tail.ToArray());
                Assert.Equal(0, array.GetSubArray(9, 1).Length);
            });
            Assert.Contains("GetSubArray(2, 5) of a NativeArray<Int32> of length 4 is cut to length 2", log);
            Assert.Equal([2, 3], array.GetSubArray(1, 2).ToArray());
            // Unity's player views the memory before the array, or gives a negative Length.
            Logged(() =>
            {
                var before = array.GetSubArray(-1, 2);
                Assert.Equal(0, before.Length);
                before[0] = 9;
                Assert.Equal(0, before[1]);
                Assert.Equal(0, array.GetSubArray(0, -1).Length);
            });
            Assert.Equal([1, 2, 3, 4], array.ToArray());
            array.Dispose();
        }
    }
}
