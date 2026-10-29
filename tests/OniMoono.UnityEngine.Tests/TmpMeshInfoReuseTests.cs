using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // New mesh buffers on a mesh that holds more quads (docs/cleanroom/textmeshpro.md, "Mesh
    // buffers made on a mesh that holds more"): the constructor leaves the mesh holding the new
    // buffer's quads without a log, with positions, normals, tangents and triangles, zero bounds,
    // and no uv or colours of its own making.
    public class TmpMeshInfoReuseTests : EngineTest
    {
        static Vector3[] Quads(int n)
        {
            var v = new Vector3[n * 4];
            for (int q = 0; q < n; q++)
            {
                v[q * 4] = new Vector3(q, 0, 0); v[q * 4 + 1] = new Vector3(q, 1, 0);
                v[q * 4 + 2] = new Vector3(q + 1, 1, 0); v[q * 4 + 3] = new Vector3(q + 1, 0, 0);
            }
            return v;
        }

        static int[] Tris(int n)
        {
            var t = new int[n * 6];
            for (int q = 0; q < n; q++)
            {
                int v = q * 4, i = q * 6;
                t[i] = v; t[i + 1] = v + 1; t[i + 2] = v + 2; t[i + 3] = v + 2; t[i + 4] = v + 3; t[i + 5] = v;
            }
            return t;
        }

        // 8 vertices with normals, uv and colours, and the triangles of two quads.
        static Mesh Two()
        {
            var m = new Mesh();
            m.vertices = Quads(2);
            m.normals = Enumerable.Repeat(new Vector3(0, 0, -1), 8).ToArray();
            m.uv = new Vector2[8];
            m.colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 255), 8).ToArray();
            m.triangles = Tris(2);
            return m;
        }

        static List<string> CaptureLogs(Action action)
        {
            var logs = new List<string>();
            Application.LogCallback log = (message, _, _) => logs.Add(message);
            Application.logMessageReceived += log;
            try { action(); }
            finally { Application.logMessageReceived -= log; }
            return logs;
        }

        static void AssertHolds(Mesh m, int quads)
        {
            Assert.Equal(quads * 4, m.vertexCount);
            Assert.Equal(quads * 4, m.vertices.Length);
            Assert.Equal(quads * 4, m.normals.Length);
            Assert.Equal(quads * 4, m.tangents.Length);
            var tris = m.triangles;
            Assert.Equal(quads * 6, tris.Length);
            Assert.Equal(quads * 4 - 1, tris.Max());
            Assert.Equal(Vector3.zero, m.bounds.center);
            Assert.Equal(Vector3.zero, m.bounds.size);
        }

        [Fact]
        public void OneQuadOnATwoQuadMeshLogsNothing()
        {
            var m = Two();
            TMP_MeshInfo mi = default;
            Assert.Empty(CaptureLogs(() => mi = new TMP_MeshInfo(m, 1)));
            Assert.Same(m, mi.mesh);
            AssertHolds(m, 1);
            Assert.Equal(new[] { 0, 1, 2, 2, 3, 0 }, m.triangles);
            // The shipped player keeps this mesh's uv and colours at 4 entries (its Mesh.Clear()
            // keeps the vertex layout); this engine's Clear drops them, so they are not checked
            // here. The constructor sets neither: see the meshes without them below.
        }

        [Fact]
        public void AMeshWithoutUvOrColoursGetsNone()
        {
            var m = new Mesh();
            m.vertices = Quads(5);
            m.triangles = Tris(5);
            Assert.Empty(CaptureLogs(() => new TMP_MeshInfo(m, 1)));
            AssertHolds(m, 1);
            Assert.Empty(m.uv);
            Assert.Empty(m.colors32);

            var empty = new Mesh();
            Assert.Empty(CaptureLogs(() => new TMP_MeshInfo(empty, 1)));
            AssertHolds(empty, 1);
            Assert.Empty(empty.uv);
            Assert.Empty(empty.colors32);
        }

        [Fact]
        public void BuffersInARowAndResizesLogNothing()
        {
            var m = new Mesh();
            var counts = new List<int>();
            Assert.Empty(CaptureLogs(() =>
            {
                new TMP_MeshInfo(m, 1); counts.Add(m.vertexCount);
                new TMP_MeshInfo(m, 4); counts.Add(m.vertexCount);
                new TMP_MeshInfo(m, 1); counts.Add(m.vertexCount);
            }));
            var other = new Mesh();
            var mi = new TMP_MeshInfo(other, 4);
            Assert.Empty(CaptureLogs(() =>
            {
                mi.ResizeMeshInfo(1); counts.Add(other.vertexCount);
                mi.ResizeMeshInfo(8); counts.Add(other.vertexCount);
            }));
            Assert.Equal(new[] { 4, 16, 4, 4, 32 }, counts);
        }
    }
}
