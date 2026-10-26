using System.Threading;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    public class TransformConcurrentReadTests : EngineTest
    {
        /// The world data a transform computes on first read after a change (position through
        /// the world matrix, rotation, lossy scale) is the same when many threads read it at once
        /// while nothing changes the hierarchy, as fast-anim-batches does from its workers: every
        /// reader gets what a single thread reading a twin hierarchy gets.
        [Fact]
        public void ConcurrentReadersAfterAChangeSeeTheSerialValues()
        {
            const int Leaves = 48, Depth = 6, Rounds = 200;
            Transform[] Build(out Transform root)
            {
                root = new GameObject("root").transform;
                var parent = root;
                for (int d = 0; d < Depth; d++)
                {
                    var t = new GameObject("level" + d).transform;
                    t.SetParent(parent, false);
                    t.localPosition = new Vector3(d + 0.25f, -d * 0.5f, 0.125f * d);
                    t.localRotation = Quaternion.Euler(3f * d, 7f * d, 11f * d);
                    t.localScale = new Vector3(1f + 0.1f * d, 1f - 0.05f * d, 1f);
                    parent = t;
                }
                var leaves = new Transform[Leaves];
                for (int i = 0; i < Leaves; i++)
                {
                    leaves[i] = new GameObject("leaf" + i).transform;
                    leaves[i].SetParent(parent, false);
                    leaves[i].localPosition = new Vector3(i, i * 0.5f, -i);
                    leaves[i].localRotation = Quaternion.Euler(0f, 0f, i * 5f);
                }
                return leaves;
            }
            var shared = Build(out var sharedRoot);
            var twin = Build(out var twinRoot);
            var positions = new Vector3[Leaves];
            var rotations = new Quaternion[Leaves];
            var scales = new Vector3[Leaves];
            int wrong = 0;
            for (int round = 0; round < Rounds; round++)
            {
                var move = new Vector3(round * 0.37f, -round * 0.11f, round * 0.05f);
                var turn = Quaternion.Euler(round, round * 2f, round * 3f);
                foreach (var root in new[] { sharedRoot, twinRoot })
                {
                    root.localPosition = move;
                    root.localRotation = turn;
                }
                for (int i = 0; i < Leaves; i++)
                {
                    positions[i] = twin[i].position;
                    rotations[i] = twin[i].rotation;
                    scales[i] = twin[i].lossyScale;
                }
                using var start = new Barrier(8);
                var threads = new Thread[8];
                for (int w = 0; w < threads.Length; w++)
                {
                    int worker = w;
                    threads[w] = new Thread(() =>
                    {
                        start.SignalAndWait();
                        for (int k = 0; k < Leaves; k++)
                        {
                            // Each worker walks the leaves from a different start, so first reads collide.
                            int i = (k + worker * 7) % Leaves;
                            var t = shared[i];
                            if (!Same(t.position, positions[i]) || !Same(t.rotation, rotations[i]) || !Same(t.lossyScale, scales[i]))
                                Interlocked.Increment(ref wrong);
                        }
                    });
                    threads[w].Start();
                }
                foreach (var t in threads) t.Join();
            }
            Assert.Equal(0, wrong);
        }

        static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        static bool Same(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
    }
}
