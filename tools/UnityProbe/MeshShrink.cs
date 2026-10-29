// Mesh channels set smaller than the triangles in use ("PROBE ... MS ..."), at the main menu. A
// mesh gets 8 vertices and the triangles of two quads (indices up to 7), then fewer vertices
// through the vertices setter, SetVertices (array and list) and after Clear(); also normals,
// uv and colors of the wrong length, and triangles that index past the vertices. Each step logs
// what the player logged (type and message) and the mesh after it: vertexCount, the length of
// every channel, the triangles and the bounds. Then TextMeshPro's TMP_MeshInfo(mesh, size) on a
// mesh with more vertices and triangles than the new buffers hold (as BuildGeometry does on a
// reused mesh), and ResizeMeshInfo to a smaller size. With PROBE_SCENARIO=meshshrink only this
// probe runs; a MeshShrinkProbeDone object appears when it is done.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class MeshShrink : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly List<string> logs = new List<string>();

        static void L(string s) => Rec.Log("MS " + s);
        static string F(float v) => v.ToString("R", Inv);

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("PROBE")) return;
            logs.Add(type + ":" + (condition ?? "null").Replace("\n", "\\n"));
        }

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

        static string State(Mesh m)
        {
            int[] tris;
            try { tris = m.triangles; } catch (Exception e) { return "triangles !" + e.GetType().Name; }
            var b = m.bounds;
            return "vc=" + m.vertexCount + " v=" + m.vertices.Length + " n=" + m.normals.Length + " uv=" + m.uv.Length
                + " c=" + m.colors32.Length + " t=" + tris.Length + " tmax=" + (tris.Length == 0 ? -1 : tris.Max())
                + " sub=" + m.subMeshCount + " ic=" + (m.subMeshCount > 0 ? m.GetTriangles(0).Length : 0)
                + " b=" + F(b.center.x) + "," + F(b.center.y) + "/" + F(b.size.x) + "," + F(b.size.y)
                + " v0=" + (m.vertices.Length > 0 ? F(m.vertices[0].x) + "," + F(m.vertices[0].y) : "-");
        }

        static void Step(string name, Mesh m, System.Action a)
        {
            logs.Clear();
            string ex = "";
            try { a(); } catch (Exception e) { ex = " !" + e.GetType().Name + ":" + e.Message.Replace("\n", "\\n"); }
            L(name + ex + " logs=[" + string.Join(" | ", logs) + "] " + State(m));
        }

        IEnumerator Run()
        {
            Application.logMessageReceived += OnLog;
            L("start");
            Mesh m;
            m = Two(); Step("base", m, () => { });
            m = Two(); Step("vertices4", m, () => m.vertices = Quads(1));
            m = Two(); Step("vertices0", m, () => m.vertices = new Vector3[0]);
            m = Two(); Step("vertices12", m, () => m.vertices = Quads(3));
            m = Two(); Step("setVertices4.array", m, () => m.SetVertices(Quads(1)));
            m = Two(); Step("setVertices4.list", m, () => m.SetVertices(Quads(1).ToList()));
            m = Two(); Step("setVertices4.range", m, () => m.SetVertices(Quads(2), 0, 4));
            m = Two(); Step("setVertices4.flags", m, () => m.SetVertices(Quads(2), 0, 4, UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices));
            m = Two(); Step("clear.vertices4", m, () => { m.Clear(); m.vertices = Quads(1); });
            m = Two(); Step("clearKeep.vertices4", m, () => { m.Clear(true); m.vertices = Quads(1); });
            m = Two(); Step("clearNoKeep.vertices4", m, () => { m.Clear(false); m.vertices = Quads(1); });
            m = Two(); Step("triangles.then.vertices4", m, () => { m.triangles = Tris(1); m.vertices = Quads(1); });
            m = Two(); Step("vertices4.then.normals4", m, () => { m.vertices = Quads(1); m.normals = new Vector3[4]; });
            m = Two(); Step("normals4", m, () => m.normals = new Vector3[4]);
            m = Two(); Step("normals12", m, () => m.normals = new Vector3[12]);
            m = Two(); Step("uv4", m, () => m.uv = new Vector2[4]);
            m = Two(); Step("colors4", m, () => m.colors32 = new Color32[4]);
            m = Two(); Step("uvs0.v4.4", m, () => m.SetUVs(0, new Vector4[4]));
            m = Two(); Step("triangles.past", m, () => m.triangles = Tris(3));
            m = Two(); Step("setTriangles.past", m, () => m.SetTriangles(Tris(3), 0));
            m = Two(); Step("vertices4.twice", m, () => { m.vertices = Quads(1); m.vertices = Quads(1); });
            m = Two(); Step("vertices4.then.vertices8", m, () => { m.vertices = Quads(1); m.vertices = Quads(2); });

            // TextMeshPro's mesh buffers on a mesh with more geometry than they hold.
            m = Two(); TMP_MeshInfo mi = default;
            Step("tmp.ctor1", m, () => mi = new TMP_MeshInfo(m, 1));
            L("tmp.ctor1 info v=" + mi.vertices.Length + " t=" + mi.triangles.Length + " same=" + ReferenceEquals(mi.mesh, m));
            m = Two();
            Step("tmp.ctor2", m, () => mi = new TMP_MeshInfo(m, 2));
            m = new Mesh(); m.vertices = Quads(5); m.triangles = Tris(5);
            Step("tmp.ctor1.from5", m, () => mi = new TMP_MeshInfo(m, 1));
            m = new Mesh();
            Step("tmp.ctor1.empty", m, () => mi = new TMP_MeshInfo(m, 1));
            Step("tmp.ctor4.after1", m, () => mi = new TMP_MeshInfo(m, 4));
            Step("tmp.ctor1.after4", m, () => mi = new TMP_MeshInfo(m, 1));
            m = new Mesh();
            mi = new TMP_MeshInfo(m, 4);
            Step("tmp.resize4to1", m, () => mi.ResizeMeshInfo(1));
            Step("tmp.resize1to8", m, () => mi.ResizeMeshInfo(8));
            Step("tmp.clear", m, () => mi.Clear());
            Step("tmp.clearUpload", m, () => mi.Clear(true));

            yield return null;
            Application.logMessageReceived -= OnLog;
            L("done");
            new GameObject("MeshShrinkProbeDone");
        }
    }
}
