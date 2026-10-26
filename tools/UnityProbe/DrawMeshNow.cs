// Graphics.DrawMeshNow ("PROBE ... DMN ..."), at the main menu (10 s in). Found with Project
// Expedition (Workshop 3803418607), whose prospecting scan draws its mesh with DrawMeshNow after
// SetPass, from NavPathDrawer.OnPostRender, under the camera's matrices loaded into GL.
//
// A 16 x 16 render texture is drawn into with pass 0 of a "Lines/Colored Blended" material
// (vertex colours) and read back. Mesh "two" has two sub-meshes, a red quad (0,0)-(4,4) and a
// green one (8,0)-(12,4); "bar" one blue quad (0,0)-(4,2); "big" a red one (-1000,-1000)-
// (1000,1000); "unit" a red one (0,0)-(0.25,0.25); each quad has both windings. A case's line
// lists the map's rows top to bottom (R, G, B: that channel above 0.5 alone; '.': black; '?':
// other), the log messages the draw raised and the exception it threw.
//
// Outside rendering, each case clears the texture to black and loads
// GL.LoadPixelMatrix(0, 16, 0, 16) first:
//   control-gl, control-blit, control-gl2: a red GL quad at (0,0)-(4,4) and a white blit;
//   all, sub0, sub1, sub-1, sub2: (two, identity) and with those sub-mesh indices;
//   mult: GL.MultMatrix(translate (0, 8)) before (two, translate (4, 0), 0);
//   trs, trs0: (bar, (8, 8, 0), 90 degrees about z) and with index 0;
//   modelview: GL.modelview after a draw (unchanged or not);
//   null: a null mesh;
//   after-set: (bar) after SetPass of a second material, on "Hidden/BlitCopy";
//   big, big-white: the large quad in vertex colours and in BlitCopy's white;
//   ortho, clip: (unit) under GL.LoadOrtho, and a quad under identity GL matrices.
// From Camera.onPostRender of a camera on the texture (orthographic, size 8, at (8, 8, -10), so
// world (0,0)-(16,16) fills it), camera-* draws (two) with: asis, the matrices as the camera
// leaves them; mod, the camera's loaded into GL as Project Expedition does; split, the view
// given as DrawMeshNow's matrix under an identity GL.modelview; mv-only, GL.modelview alone set
// to the view; pixel and pixel-z, the pixel matrix with the mesh at z 0 and z -10; pixel-gl and
// gl, the GL quad under the pixel matrix and the camera's; mv-mult, the view and a move up by 8
// as the matrix; mv-pixelproj, the pixel matrix's projection with the view; ident-z, the
// camera's projection, an identity GL.modelview and a move by z -10; mv-z and mv-z-scale,
// GL.modelview a move by z -10, and that with z mirrored; srt-ortho, ortho, ortho-z, srt-pixel,
// (unit) under GL.LoadOrtho and the pixel matrix, after Graphics.SetRenderTarget(texture) or
// not; proj-ortho-view, proj-pixel-view, GL.LoadProjectionMatrix of Matrix4x4.Ortho matrices
// with a view; huge, an orthographic projection 2e5 wide with (big). effect-* draws the same
// from OnRenderImage after a blit and SetRenderTarget(destination), as Project Expedition's
// deposit coverage does. after-render and out-* draw outside rendering after the camera
// rendered; "matrices" logs GL.modelview after it is set and after GL.LoadPixelMatrix; "state"
// logs GL.sRGBWrite (at first, and after it is set) and RenderTexture.active after
// Graphics.SetRenderTarget, which Project Expedition's deposit coverage uses too.
//
// Measured: outside rendering DrawMeshNow draws nothing (GL immediate drawing does).
// Inside, the matrix places the mesh in the world, which the rendering camera's view and GL's
// projection bring to the target; GL.modelview plays no part (mv-z draws as asis, split draws
// nothing), though GL.modelview reads back what was set. The overload without an index draws
// every sub-mesh (asis). A null mesh throws ArgumentNullException. GL.sRGBWrite is false at first
// (gamma colour space) and keeps what is set; SetRenderTarget makes the texture (or null)
// RenderTexture.active.
// With PROBE_SCENARIO=drawmeshnow only this probe runs; it ends with a DrawMeshNowProbeDone
// canvas.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class DmnImageEffect : MonoBehaviour
    {
        public Material Lines;
        public System.Action Draw;
        public string Note = "";

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(source, destination);
            GL.PushMatrix();
            Graphics.SetRenderTarget(destination);
            Note = "destination=" + (destination == null ? "null" : destination.name) + " modelview-identity=" + (GL.modelview == Matrix4x4.identity);
            Lines.SetPass(0);
            Draw();
            GL.PopMatrix();
        }
    }

    public class DrawMeshNow : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        RenderTexture rt;
        Texture2D read;
        Material lines;
        readonly List<string> messages = new List<string>();

        static void L(string s) => Rec.Log("DMN " + s);
        static string M(Matrix4x4 m)
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++) sb.Append(m[r, c].ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(r == 3 && c == 3 ? "]" : c == 3 ? "; " : " ");
            return sb.ToString();
        }

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static Mesh Quads(params (Rect r, Color c)[] subs)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var col = new List<Color>();
            foreach (var (r, c) in subs)
            {
                v.Add(new Vector3(r.xMin, r.yMin, 0f)); v.Add(new Vector3(r.xMax, r.yMin, 0f));
                v.Add(new Vector3(r.xMax, r.yMax, 0f)); v.Add(new Vector3(r.xMin, r.yMax, 0f));
                for (int i = 0; i < 4; i++) col.Add(c);
            }
            m.SetVertices(v);
            m.SetColors(col);
            m.subMeshCount = subs.Length;
            for (int s = 0; s < subs.Length; s++)
            {
                int b = s * 4;
                // Both windings, so culling plays no part.
                m.SetTriangles(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 }, s);
            }
            return m;
        }

        void Log(string condition, string stack, LogType type) => messages.Add(type + ": " + condition.Replace('\n', ' '));

        string Map()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            var sb = new StringBuilder();
            for (int y = 15; y >= 0; y--)
            {
                if (y < 15) sb.Append('/');
                for (int x = 0; x < 16; x++)
                {
                    var p = read.GetPixel(x, y);
                    bool r = p.r > 0.5f, g = p.g > 0.5f, b = p.b > 0.5f;
                    sb.Append(!r && !g && !b && p.r + p.g + p.b < 0.05f ? '.' : r && !g && !b ? 'R' : g && !r && !b ? 'G' : b && !r && !g ? 'B' : '?');
                }
            }
            return sb.ToString();
        }

        void Case(string name, System.Action draw)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, 16f, 0f, 16f);
            messages.Clear();
            string ex = "none";
            Application.logMessageReceived += Log;
            try
            {
                bool ok = lines.SetPass(0);
                if (!ok) messages.Add("SetPass false");
                draw();
            }
            catch (Exception e) { ex = e.GetType().Name + " '" + e.Message.Replace('\n', ' ') + "'"; }
            finally
            {
                Application.logMessageReceived -= Log;
                GL.PopMatrix();
                RenderTexture.active = prev;
            }
            L(name + " map=" + Map() + " messages=[" + string.Join(" | ", messages) + "] exception=" + ex);
        }

        IEnumerator Run()
        {
            yield return new WaitForEndOfFrame();
            rt = new RenderTexture(16, 16, 24, RenderTextureFormat.ARGB32) { name = "DmnTarget" };
            rt.Create();
            read = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            lines = new Material(Shader.Find("Lines/Colored Blended"));
            var two = Quads((new Rect(0, 0, 4, 4), Color.red), (new Rect(8, 0, 4, 4), Color.green));
            var bar = Quads((new Rect(0, 0, 4, 2), Color.blue));
            L("=== start shader=" + (lines.shader != null ? lines.shader.name : "null") + " passes=" + lines.passCount);
            {
                bool srgb = GL.sRGBWrite;
                GL.sRGBWrite = !srgb;
                bool flipped = GL.sRGBWrite;
                GL.sRGBWrite = srgb;
                var before = RenderTexture.active;
                Graphics.SetRenderTarget(rt);
                var set = RenderTexture.active;
                Graphics.SetRenderTarget(null);
                L("state sRGBWrite=" + srgb + " after set to " + !srgb + "=" + flipped + " colorSpace=" + QualitySettings.activeColorSpace
                  + " active before=" + (before == null ? "null" : before.name) + " after SetRenderTarget(texture)=" + (set == null ? "null" : set.name)
                  + " after SetRenderTarget(null)=" + (RenderTexture.active == null ? "null" : RenderTexture.active.name));
                RenderTexture.active = before;
            }
            System.Action glQuad = () =>
            {
                GL.Begin(GL.QUADS);
                GL.Color(Color.red);
                GL.Vertex3(0f, 0f, 0f); GL.Vertex3(4f, 0f, 0f); GL.Vertex3(4f, 4f, 0f); GL.Vertex3(0f, 4f, 0f);
                GL.Vertex3(0f, 0f, 0f); GL.Vertex3(0f, 4f, 0f); GL.Vertex3(4f, 4f, 0f); GL.Vertex3(4f, 0f, 0f);
                GL.End();
            };
            Case("control-gl", glQuad);
            Case("control-blit", () => Graphics.Blit(Texture2D.whiteTexture, rt));
            Case("all", () => Graphics.DrawMeshNow(two, Matrix4x4.identity));
            Case("sub0", () => Graphics.DrawMeshNow(two, Matrix4x4.identity, 0));
            Case("sub1", () => Graphics.DrawMeshNow(two, Matrix4x4.identity, 1));
            Case("sub-1", () => Graphics.DrawMeshNow(two, Matrix4x4.identity, -1));
            Case("sub2", () => Graphics.DrawMeshNow(two, Matrix4x4.identity, 2));
            Case("mult", () =>
            {
                GL.MultMatrix(Matrix4x4.Translate(new Vector3(0f, 8f, 0f)));
                Graphics.DrawMeshNow(two, Matrix4x4.Translate(new Vector3(4f, 0f, 0f)), 0);
            });
            Case("trs", () => Graphics.DrawMeshNow(bar, new Vector3(8f, 8f, 0f), Quaternion.Euler(0f, 0f, 90f)));
            Case("trs0", () => Graphics.DrawMeshNow(bar, new Vector3(8f, 8f, 0f), Quaternion.Euler(0f, 0f, 90f), 0));
            Case("modelview", () =>
            {
                var before = GL.modelview;
                Graphics.DrawMeshNow(two, Matrix4x4.Translate(new Vector3(1f, 1f, 0f)));
                messages.Add("modelview " + (GL.modelview == before ? "unchanged" : "changed to " + GL.modelview.ToString().Replace('\n', ' ')));
            });
            Case("null", () => Graphics.DrawMeshNow(null, Matrix4x4.identity));
            Case("after-set", () =>
            {
                var other = new Material(Shader.Find("Hidden/BlitCopy"));
                other.mainTexture = Texture2D.whiteTexture;
                messages.Add("other SetPass " + other.SetPass(0));
                Graphics.DrawMeshNow(bar, Matrix4x4.identity);
            });
            Case("control-gl2", glQuad);
            var big = Quads((new Rect(-1000, -1000, 2000, 2000), Color.red));
            Case("big", () => Graphics.DrawMeshNow(big, Matrix4x4.identity));
            Case("big-white", () =>
            {
                var white = new Material(Shader.Find("Hidden/BlitCopy")) { mainTexture = Texture2D.whiteTexture };
                white.SetPass(0);
                Graphics.DrawMeshNow(big, Matrix4x4.identity);
            });
            var unit = Quads((new Rect(0f, 0f, 0.25f, 0.25f), Color.red));
            Case("ortho", () =>
            {
                GL.LoadOrtho();
                Graphics.DrawMeshNow(unit, Matrix4x4.identity);
            });
            var clip = Quads((new Rect(-1f, -1f, 0.5f, 0.5f), Color.red));
            Case("clip", () =>
            {
                GL.LoadProjectionMatrix(Matrix4x4.identity);
                GL.modelview = Matrix4x4.identity;
                Graphics.DrawMeshNow(clip, Matrix4x4.identity);
            });
            // Inside a camera's rendering, as Project Expedition draws: a camera on the texture,
            // orthographic, centred on (8, 8) with size 8, so world (0, 0)-(16, 16) fills it.
            var cgo = new GameObject("DmnCamera");
            var cam = cgo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.aspect = 1f;
            cam.transform.position = new Vector3(8f, 8f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.targetTexture = rt;
            var z10 = Matrix4x4.Translate(new Vector3(0f, 0f, -10f));
            var view = cam.worldToCameraMatrix;
            var proj = cam.projectionMatrix;
            {
                GL.PushMatrix();
                L("matrices camera view=" + M(view) + " proj=" + M(proj));
                GL.modelview = view;
                L("matrices modelview after set=" + M(GL.modelview));
                GL.LoadPixelMatrix(0f, 16f, 0f, 16f);
                L("matrices modelview after LoadPixelMatrix=" + M(GL.modelview));
                GL.PopMatrix();
            }
            var drawCases = new List<(string, System.Action)>
            {
                ("asis", () => Graphics.DrawMeshNow(two, Matrix4x4.identity)),
                ("mod", () => { GL.LoadProjectionMatrix(proj); GL.modelview = view; Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("split", () => { GL.LoadProjectionMatrix(proj); GL.modelview = Matrix4x4.identity; Graphics.DrawMeshNow(two, view); }),
                ("mv-only", () => { GL.modelview = view; Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("pixel", () => { GL.LoadPixelMatrix(0f, 16f, 0f, 16f); Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("pixel-z", () => { GL.LoadPixelMatrix(0f, 16f, 0f, 16f); Graphics.DrawMeshNow(two, z10); }),
                ("pixel-gl", () => { GL.LoadPixelMatrix(0f, 16f, 0f, 16f); glQuad(); }),
                ("gl", () => glQuad()),
                ("mv-mult", () => { GL.modelview = view; Graphics.DrawMeshNow(two, Matrix4x4.Translate(new Vector3(0f, 8f, 0f))); }),
                ("mv-pixelproj", () => { GL.LoadPixelMatrix(0f, 16f, 0f, 16f); GL.modelview = view; Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("ident-z", () => { GL.LoadProjectionMatrix(proj); GL.modelview = Matrix4x4.identity; Graphics.DrawMeshNow(two, z10); }),
                ("mv-z", () => { GL.modelview = z10; Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("srt-ortho", () => { Graphics.SetRenderTarget(rt); GL.LoadOrtho(); Graphics.DrawMeshNow(unit, Matrix4x4.identity); }),
                ("ortho", () => { GL.LoadOrtho(); Graphics.DrawMeshNow(unit, Matrix4x4.identity); }),
                ("ortho-z", () => { GL.LoadOrtho(); Graphics.DrawMeshNow(unit, Matrix4x4.Translate(new Vector3(0f, 0f, -0.5f))); }),
                ("srt-pixel", () => { Graphics.SetRenderTarget(rt); GL.LoadPixelMatrix(0f, 16f, 0f, 16f); Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("proj-ortho-view", () => { GL.LoadProjectionMatrix(Matrix4x4.Ortho(-8f, 8f, -8f, 8f, 0.3f, 1000f)); GL.modelview = view; Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("proj-pixel-view", () => { GL.LoadProjectionMatrix(Matrix4x4.Ortho(0f, 16f, 0f, 16f, -100f, 100f)); GL.modelview = Matrix4x4.Translate(new Vector3(0f, 0f, -10f)); Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
                ("huge", () => { GL.LoadProjectionMatrix(Matrix4x4.Ortho(-1e5f, 1e5f, -1e5f, 1e5f, -1e5f, 1e5f)); Graphics.DrawMeshNow(big, Matrix4x4.identity); }),
                ("mv-z-scale", () => { GL.modelview = z10 * Matrix4x4.Scale(new Vector3(1f, 1f, -1f)); Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
            };
            System.Action current = null;
            Camera.CameraCallback post = c =>
            {
                if (c != cam || current == null) return;
                GL.PushMatrix();
                lines.SetPass(0);
                current();
                GL.PopMatrix();
            };
            Camera.onPostRender += post;
            foreach (var (name, draw) in drawCases)
            {
                current = draw;
                messages.Clear();
                Application.logMessageReceived += Log;
                string ex = "none";
                try { cam.Render(); }
                catch (Exception e) { ex = e.GetType().Name + " '" + e.Message.Replace('\n', ' ') + "'"; }
                finally { Application.logMessageReceived -= Log; }
                L("camera-" + name + " map=" + Map() + " messages=[" + string.Join(" | ", messages) + "] exception=" + ex);
            }
            Camera.onPostRender -= post;
            current = null;
            // From OnRenderImage, as Project Expedition's deposit coverage draws (a postfix on an
            // image effect's OnRenderImage): blit, then SetRenderTarget(destination), LoadOrtho.
            var effect = cgo.AddComponent<DmnImageEffect>();
            effect.Lines = lines;
            foreach (var (name, draw) in new (string, System.Action)[]
            {
                ("srt-ortho", () => { GL.LoadOrtho(); Graphics.DrawMeshNow(unit, Matrix4x4.identity); }),
                ("asis", () => Graphics.DrawMeshNow(two, Matrix4x4.identity)),
                ("mod", () => { GL.LoadProjectionMatrix(proj); GL.modelview = view; Graphics.DrawMeshNow(two, Matrix4x4.identity); }),
            })
            {
                effect.Draw = draw;
                cam.Render();
                L("effect-" + name + " map=" + Map() + " " + effect.Note);
            }
            Destroy(effect);
            // Outside rendering straight after the camera rendered, without clearing.
            cam.Render();
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                GL.PushMatrix();
                lines.SetPass(0);
                GL.LoadProjectionMatrix(proj);
                GL.modelview = view;
                Graphics.DrawMeshNow(two, Matrix4x4.identity);
                GL.PopMatrix();
                RenderTexture.active = prev;
                L("after-render map=" + Map());
            }
            Case("out-huge-only", () => { GL.LoadProjectionMatrix(Matrix4x4.Ortho(-1e5f, 1e5f, -1e5f, 1e5f, -1e5f, 1e5f)); GL.modelview = Matrix4x4.identity; Graphics.DrawMeshNow(big, Matrix4x4.identity); });
            // The same draws outside rendering, after the camera rendered.
            foreach (var (name, draw) in drawCases)
                if (name != "asis") Case("out-" + name, draw);
            Destroy(cgo);
            var done = new GameObject("DrawMeshNowProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
