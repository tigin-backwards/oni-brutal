// The order of transparent Graphics.DrawMesh draws that sort at (nearly) the same distance
// ("PROBE ... DO ..."), in a colony (120 frames after Game.Instance exists). With
// PROBE_SCENARIO=draworder only this probe runs; PROBE_CAPTURE_DIR receives the screenshots,
// which do-check.py reads.
//
// As KAnimBatchManager.Render draws its batches: one DrawMesh per quad, at z = -23.5 plus a small
// offset, with the material in queue 3500, ZWrite off. Each case is a pair of 0.5 x 0.5 quads, P (red)
// and Q (green), overlapping by 0.25; the pixel in the overlap shows which drew last. Cases
// vary the z difference (0, about one float step at that depth, 1e-4), which of the two is
// submitted first, which material was created first, and the mesh's bounds (a unit quad's, or
// float.MaxValue as Klei's batch meshes have).
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SemanticsProbe
{
    public class DrawOrder : MonoBehaviour
    {
        static void L(string s) => Rec.Log("DO " + s);

        class Case
        {
            public string Name;
            public float DzP, DzQ;
            public bool QFirst;
            public Material P, Q;
            public Mesh MeshP, MeshQ;
            public Vector3 At;
        }

        readonly List<Case> cases = new List<Case>();
        bool started, drawing;
        int wait = -1;
        const float Z = -23.5f;

        void Update()
        {
            if (drawing) Draw();
            // Started once the camera is on the open cave the spec points it at (orthographic size 4).
            if (started || Game.Instance == null || Camera.main == null || Mathf.Abs(Camera.main.orthographicSize - 4f) > 0.01f) return;
            if (wait < 0) wait = Time.frameCount + 120;
            if (Time.frameCount < wait) return;
            started = true;
            StartCoroutine(Run());
        }

        static Mesh Quad(bool huge)
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            if (huge) m.bounds = new Bounds(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue));
            return m;
        }

        static Shader shader;

        static Material Mat(Color c)
        {
            var m = new Material(shader);
            m.color = c;
            m.renderQueue = 3500;
            return m;
        }

        void Draw()
        {
            foreach (var c in cases)
            {
                var p = Matrix4x4.TRS(new Vector3(c.At.x, c.At.y, Z + c.DzP), Quaternion.identity, Vector3.one);
                var q = Matrix4x4.TRS(new Vector3(c.At.x + 0.25f, c.At.y, Z + c.DzQ), Quaternion.identity, Vector3.one);
                if (c.QFirst)
                {
                    Graphics.DrawMesh(c.MeshQ, q, c.Q, 0);
                    Graphics.DrawMesh(c.MeshP, p, c.P, 0);
                }
                else
                {
                    Graphics.DrawMesh(c.MeshP, p, c.P, 0);
                    Graphics.DrawMesh(c.MeshQ, q, c.Q, 0);
                }
            }
        }

        IEnumerator Run()
        {
            shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            var cam = Camera.main;
            L("=== start shader " + (shader == null ? "null" : shader.name) + " camera " + cam.name + " pos " + cam.transform.position.ToString("R")
              + " ortho " + cam.orthographicSize.ToString("R", CultureInfo.InvariantCulture) + " sort " + cam.transparencySortMode + " screen " + Screen.width + "x" + Screen.height);
            var normal = Quad(false);
            var normal2 = Quad(false);
            var huge = Quad(true);
            var huge2 = Quad(true);
            float step = Mathf.Abs((Z + 9.174312e-05f) - (Z + 8.928571e-05f));
            float[] dzs = { 0f, step, -step, 1e-4f, -1e-4f };
            string[] dzNames = { "same", "P-farther-1ulp", "Q-farther-1ulp", "P-farther-1e-4", "Q-farther-1e-4" };
            var origin = cam.transform.position;
            int k = 0;
            foreach (bool hugeBounds in new[] { false, true })
                foreach (bool matQFirst in new[] { false, true })
                    foreach (bool qFirst in new[] { false, true })
                        for (int d = 0; d < dzs.Length; d++)
                        {
                            Material mp, mq;
                            if (matQFirst) { mq = Mat(Color.green); mp = Mat(Color.red); }
                            else { mp = Mat(Color.red); mq = Mat(Color.green); }
                            int col = k % 8, row = k / 8;
                            var c = new Case
                            {
                                Name = (hugeBounds ? "huge" : "unit") + "-" + (matQFirst ? "matQ1st" : "matP1st") + "-" + (qFirst ? "submitQP" : "submitPQ") + "-" + dzNames[d],
                                DzP = dzs[d] > 0 ? dzs[d] : 0f,
                                DzQ = dzs[d] < 0 ? -dzs[d] : 0f,
                                QFirst = qFirst, P = mp, Q = mq,
                                MeshP = hugeBounds ? huge : normal, MeshQ = hugeBounds ? huge2 : normal2,
                                At = new Vector3(origin.x - 4f + 1f * col, origin.y + 1.5f - 0.75f * row, 0f),
                            };
                            cases.Add(c);
                            var sp = cam.WorldToScreenPoint(new Vector3(c.At.x + 0.375f, c.At.y + 0.25f, Z));
                            L("case " + k + " " + c.Name + " zP " + (Z + c.DzP).ToString("R", CultureInfo.InvariantCulture) + " zQ " + (Z + c.DzQ).ToString("R", CultureInfo.InvariantCulture)
                              + " matP " + mp.GetInstanceID() + " matQ " + mq.GetInstanceID() + " pixel " + Mathf.RoundToInt(sp.x) + "," + Mathf.RoundToInt(sp.y));
                            k++;
                        }
            drawing = true;
            for (int i = 0; i < 5; i++) yield return null;
            var dir = System.Environment.GetEnvironmentVariable("PROBE_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(Path.Combine(dir, "do-cases.png"));
            for (int i = 0; i < 10; i++) yield return null;
            drawing = false;
            var done = new GameObject("DrawOrderProbeDone", typeof(RectTransform));
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
