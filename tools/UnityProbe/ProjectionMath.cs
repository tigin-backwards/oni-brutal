// Matrix4x4's projection, look-at, affine-inverse and rotation builders and Camera's conversions
// to the bit (lines "PROBE ... PJ <kind> ..."), at mod load. Matrix4x4.inverse turned out to be
// Mesa's invert_matrix_general bit for bit; these lines test whether more of the native matrix
// and camera math follows Mesa's m_matrix.c or GLU's project.c (tools/UnityProbe/pj-check.py).
//
// Kinds, each case's inputs and outputs as float bits in hex:
//   PP Perspective(fov, aspect, near, far)     PO Ortho(l, r, b, t, near, far)
//   PF Frustum(l, r, b, t, near, far)          PL LookAt(from, to, up)
//   PA Inverse3DAffine(m, ref r): the result's bool and r (r starts as all 7s)
//   PR Rotate(q), TRS(p, q, s), AngleAxis(angle, axis) and Rotate of that
//   PC a camera (ortho or perspective, size, fov, clip planes, rect, aspect, position, rotation,
//      scale): its pixelRect, projectionMatrix, worldToCameraMatrix, cameraToWorldMatrix and
//      transform matrices; then PCP lines, one per point: WorldToScreenPoint,
//      WorldToViewportPoint, ScreenToWorldPoint, ViewportToWorldPoint, ScreenToViewportPoint,
//      ViewportToScreenPoint, ScreenPointToRay and ViewportPointToRay.
//   PK camera rect rules: rects and pixel rects out of range and at half pixels, a target
//      texture, the aspect kept or recomputed, worldToCameraMatrix's parts (CameraRules)
// Random cases come from a seeded System.Random, then degenerate ones (equal planes, zero and
// negative sizes, 0 and 180 degree fields of view, NaN, infinity). Each kind runs in its own
// method so that a player lacking one of these APIs logs "PJ <kind> missing" and goes on. Run
// alone with PROBE_SCENARIO=projection.
using System;
using System.Text;
using UnityEngine;

namespace SemanticsProbe
{
    public static class ProjectionMath
    {
        static string H(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("x8");
        static string H(Vector3 v) => H(v.x) + "," + H(v.y) + "," + H(v.z);
        static string H(Quaternion q) => H(q.x) + "," + H(q.y) + "," + H(q.z) + "," + H(q.w);
        static string H(Rect r) => H(r.x) + "," + H(r.y) + "," + H(r.width) + "," + H(r.height);
        static string H(Ray r) => H(r.origin) + "," + H(r.direction);

        static string H(Matrix4x4 m)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    sb.Append(r == 0 && c == 0 ? "" : ",").Append(H(m[r, c]));
            return sb.ToString();
        }

        static float Draw(System.Random rng)
        {
            double mag = Math.Pow(10, rng.Next(-2, 4));
            return (float)((rng.NextDouble() * 2 - 1) * mag);
        }

        static float Pos(System.Random rng, double lo, double hi) => (float)(lo * Math.Pow(hi / lo, rng.NextDouble()));

        static Vector3 DrawV(System.Random rng) => new Vector3(Draw(rng), Draw(rng), Draw(rng));

        static Quaternion DrawQ(System.Random rng)
        {
            switch (rng.Next(4))
            {
                case 0: return Quaternion.Euler(90 * rng.Next(4), 90 * rng.Next(4), 90 * rng.Next(4));
                case 1: return Quaternion.Normalize(new Quaternion((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)));
                default: return Quaternion.Euler((float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360));
            }
        }

        static void Guard(string kind, System.Action a)
        {
            try { a(); }
            catch (Exception e) { Rec.Log("PJ " + kind + " missing " + e.GetType().Name + ": " + e.Message); }
        }

        const float Nan = float.NaN, Inf = float.PositiveInfinity;

        static void Perspective(System.Random rng)
        {
            int i = 0;
            void Log(float fov, float a, float n, float f)
                => Rec.Log("PJ PP " + i++ + " in=" + H(fov) + "," + H(a) + "," + H(n) + "," + H(f) + " m=" + H(Matrix4x4.Perspective(fov, a, n, f)));
            for (int k = 0; k < 400; k++)
            {
                float fov = k % 4 == 0 ? rng.Next(1, 180) : (float)(rng.NextDouble() * 179 + 0.5);
                float a = k % 3 == 0 ? (float)rng.Next(1, 5) / rng.Next(1, 5) : Pos(rng, 0.2, 5);
                float n = Pos(rng, 1e-3, 100), f = n + Pos(rng, 1e-2, 1e5);
                Log(fov, a, n, f);
            }
            float[] fovs = { 0, 180, -60, 360, 1e-3f, 179.99f, 90, 60, 45, 1, 179, 270, -0f, Nan, Inf };
            foreach (var fov in fovs) Log(fov, 1.5f, 0.3f, 1000);
            float[] aspects = { 0, -0f, -1.5f, 1e-30f, 1e30f, Inf, Nan, 1 };
            foreach (var a in aspects) Log(60, a, 0.3f, 1000);
            float[][] planes =
            {
                new[] { 1f, 1f }, new[] { 0.3f, 0.3f }, new[] { 0f, 0f }, new[] { 0f, 1000f }, new[] { -1f, 1f },
                new[] { 1000f, 0.3f }, new[] { -0f, 10f }, new[] { 0.3f, Inf }, new[] { Inf, Inf }, new[] { 0.3f, Nan },
                new[] { 1e-30f, 1e30f }, new[] { 1e30f, 1e30f }, new[] { 5f, -5f }, new[] { -0.3f, -1000f }, new[] { 1e-7f, 1e-7f },
                new[] { 1f, 1.0000001f }, new[] { 1e20f, 3e20f },
            };
            foreach (var p in planes) Log(60, 1.5f, p[0], p[1]);
        }

        static float[][] Boxes(System.Random rng, int count)
        {
            var list = new System.Collections.Generic.List<float[]>();
            for (int k = 0; k < count; k++)
            {
                var b = new float[6];
                switch (k % 4)
                {
                    case 0: // symmetric, as cameras use
                        float w = Pos(rng, 1e-2, 1e4), h = Pos(rng, 1e-2, 1e4);
                        b[0] = -w; b[1] = w; b[2] = -h; b[3] = h;
                        break;
                    case 1: // pixel boxes
                        b[0] = 0; b[1] = rng.Next(1, 4000); b[2] = 0; b[3] = rng.Next(1, 3000);
                        break;
                    default:
                        b[0] = Draw(rng); b[1] = b[0] + Pos(rng, 1e-2, 1e4); b[2] = Draw(rng); b[3] = b[2] + Pos(rng, 1e-2, 1e4);
                        break;
                }
                if (rng.Next(3) == 0) { b[4] = Draw(rng); b[5] = Draw(rng); }
                else { b[4] = Pos(rng, 1e-3, 100); b[5] = b[4] + Pos(rng, 1e-2, 1e5); }
                list.Add(b);
            }
            list.Add(new float[] { 1, 1, -1, 1, 0.3f, 1000 });
            list.Add(new float[] { -1, 1, 2, 2, 0.3f, 1000 });
            list.Add(new float[] { -1, 1, -1, 1, 5, 5 });
            list.Add(new float[] { 0, 0, 0, 0, 0, 0 });
            list.Add(new float[] { 1, -1, 1, -1, 1000, 0.3f });
            list.Add(new float[] { -0f, 0, -0f, 0, -0f, 0 });
            list.Add(new float[] { -1, 1, -1, 1, 0, 1000 });
            list.Add(new float[] { -1, 1, -1, 1, -1, 1 });
            list.Add(new float[] { -Inf, Inf, -1, 1, 0.3f, 1000 });
            list.Add(new float[] { -1, Inf, -1, 1, 0.3f, Inf });
            list.Add(new float[] { Nan, 1, -1, 1, 0.3f, 1000 });
            list.Add(new float[] { -1e-30f, 1e-30f, -1e-30f, 1e-30f, 1e-30f, 2e-30f });
            list.Add(new float[] { -3e38f, 3e38f, -3e38f, 3e38f, -3e38f, 3e38f });
            list.Add(new float[] { 1, 1.0000001f, 1, 1.0000001f, 1, 1.0000001f });
            list.Add(new float[] { -16f / 9f * 5, 16f / 9f * 5, -5, 5, -100, 100 });
            list.Add(new float[] { 0, 1024, 0, 768, -1, 1 });
            list.Add(new float[] { 0, 1024, 768, 0, -1, 1 });
            return list.ToArray();
        }

        static void Ortho(System.Random rng)
        {
            var boxes = Boxes(rng, 400);
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                Rec.Log("PJ PO " + i + " in=" + H(b[0]) + "," + H(b[1]) + "," + H(b[2]) + "," + H(b[3]) + "," + H(b[4]) + "," + H(b[5])
                    + " m=" + H(Matrix4x4.Ortho(b[0], b[1], b[2], b[3], b[4], b[5])));
            }
        }

        static void Frustum(System.Random rng)
        {
            var boxes = Boxes(rng, 400);
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                Rec.Log("PJ PF " + i + " in=" + H(b[0]) + "," + H(b[1]) + "," + H(b[2]) + "," + H(b[3]) + "," + H(b[4]) + "," + H(b[5])
                    + " m=" + H(Matrix4x4.Frustum(b[0], b[1], b[2], b[3], b[4], b[5])));
            }
        }

        static void LookAt(System.Random rng)
        {
            int i = 0;
            void Log(Vector3 from, Vector3 to, Vector3 up)
                => Rec.Log("PJ PL " + i++ + " from=" + H(from) + " to=" + H(to) + " up=" + H(up) + " m=" + H(Matrix4x4.LookAt(from, to, up)));
            for (int k = 0; k < 400; k++)
            {
                var from = DrawV(rng);
                var to = k % 5 == 0 ? from + new Vector3(0, 0, Pos(rng, 0.1, 100)) : DrawV(rng);
                var up = k % 2 == 0 ? Vector3.up : k % 4 == 1 ? DrawV(rng) : new Vector3((float)rng.NextDouble(), 1, (float)rng.NextDouble());
                Log(from, to, up);
            }
            Log(Vector3.zero, Vector3.zero, Vector3.up);
            Log(Vector3.zero, Vector3.up, Vector3.up);
            Log(Vector3.zero, Vector3.down * 3, Vector3.up);
            Log(Vector3.zero, Vector3.forward, Vector3.zero);
            Log(Vector3.one, Vector3.one * 2, Vector3.one);
            Log(new Vector3(1, 2, 3), new Vector3(1, 2, 3.0000002f), Vector3.up);
            Log(Vector3.zero, new Vector3(Nan, 0, 1), Vector3.up);
            Log(Vector3.zero, new Vector3(Inf, 0, 1), Vector3.up);
            Log(new Vector3(-0f, -0f, -0f), Vector3.forward, Vector3.up);
            Log(Vector3.zero, new Vector3(1e-30f, 0, 1e-30f), Vector3.up);
            Log(Vector3.zero, new Vector3(3e38f, 3e38f, 3e38f), Vector3.up);
            Log(Vector3.zero, Vector3.back, Vector3.up);
            Log(Vector3.zero, Vector3.right, new Vector3(0, 1e-30f, 0));
        }

        static Matrix4x4 Rows(params float[] v)
        {
            var m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i / 4, i % 4] = v[i];
            return m;
        }

        static void Affine(System.Random rng)
        {
            int i = 0;
            void Log(Matrix4x4 m)
            {
                var r = Rows(7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7);
                bool ok = Matrix4x4.Inverse3DAffine(m, ref r);
                Rec.Log("PJ PA " + i++ + " m=" + H(m) + " ok=" + (ok ? 1 : 0) + " r=" + H(r));
            }
            for (int k = 0; k < 600; k++)
            {
                Matrix4x4 m;
                switch (k % 6)
                {
                    case 0: m = Matrix4x4.TRS(DrawV(rng), DrawQ(rng), new Vector3(Pos(rng, 0.25, 4), Pos(rng, 0.25, 4), Pos(rng, 0.25, 4))); break;
                    case 1: { float s = Pos(rng, 0.25, 4); m = Matrix4x4.TRS(DrawV(rng), DrawQ(rng), new Vector3(s, s, s)); break; }
                    case 2: m = Matrix4x4.TRS(DrawV(rng), Quaternion.identity, new Vector3(Pos(rng, 0.25, 4), -Pos(rng, 0.25, 4), Pos(rng, 0.25, 4))); break;
                    case 3: m = Matrix4x4.TRS(DrawV(rng), Quaternion.identity, Vector3.one); break;
                    case 4: m = Matrix4x4.TRS(DrawV(rng), DrawQ(rng), Vector3.one); break;
                    default:
                        m = new Matrix4x4();
                        for (int a = 0; a < 3; a++) for (int b = 0; b < 4; b++) m[a, b] = Draw(rng);
                        m[3, 3] = 1;
                        if (k % 12 == 11) { m[3, 0] = Draw(rng); m[3, 2] = Draw(rng); m[3, 3] = Draw(rng); }
                        break;
                }
                Log(m);
            }
            Log(Matrix4x4.zero);
            Log(Matrix4x4.identity);
            Log(Matrix4x4.Scale(new Vector3(0, 1, 1)));
            Log(Matrix4x4.Scale(new Vector3(1e-9f, 1e-9f, 1e-9f)));
            Log(Matrix4x4.Scale(new Vector3(1e-20f, 1, 1)));
            Log(Matrix4x4.Scale(new Vector3(1e-8f, 1e-8f, 1e-8f)));
            Log(Matrix4x4.Scale(new Vector3(1e-5f, 1e-5f, 1e-5f)));
            Log(Matrix4x4.Scale(new Vector3(1e20f, 1e20f, 1e20f)));
            Log(Matrix4x4.Scale(new Vector3(-0f, 1, 1)));
            Log(Rows(1, 2, 3, 4, 2, 4, 6, 8, 1, 0, 1, 0, 0, 0, 0, 1));
            Log(Rows(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0, 1));
            Log(Rows(Nan, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1));
            Log(Rows(1, 0, 0, Inf, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1));
            Log(Matrix4x4.Perspective(60, 1.5f, 0.3f, 1000));
            Log(Matrix4x4.Ortho(-10, 10, -5, 5, 0.3f, 1000));
            Log(Rows(1e-3f, 0, 0, 0, 0, 1e-3f, 0, 0, 0, 0, 1e-3f, 0, 0, 0, 0, 1));
            Log(Rows(1e-4f, 0, 0, 0, 0, 1e-4f, 0, 0, 0, 0, 1e-4f, 0, 0, 0, 0, 1));
            Log(Rows(1e-6f, 0, 0, 5, 0, 1e-6f, 0, 6, 0, 0, 1e-6f, 7, 0, 0, 0, 1));
            Log(Rows(1e-7f, 0, 0, 5, 0, 1e-7f, 0, 6, 0, 0, 1e-7f, 7, 0, 0, 0, 1));
            Log(Rows(1e-9f, 0, 0, 5, 0, 1e-9f, 0, 6, 0, 0, 1e-9f, 7, 0, 0, 0, 1));
        }

        static void Rotations(System.Random rng)
        {
            for (int i = 0; i < 400; i++)
            {
                var q = i % 5 == 4 ? new Quaternion(Draw(rng), Draw(rng), Draw(rng), Draw(rng)) : DrawQ(rng);
                var p = DrawV(rng);
                var s = new Vector3(Draw(rng), Draw(rng), Draw(rng));
                float angle = i % 3 == 0 ? rng.Next(-720, 720) : (float)(rng.NextDouble() * 720 - 360);
                var axis = i % 4 == 0 ? new Vector3(rng.Next(-1, 2), rng.Next(-1, 2), rng.Next(-1, 2)) : DrawV(rng);
                var aa = Quaternion.AngleAxis(angle, axis);
                Rec.Log("PJ PR " + i + " q=" + H(q) + " p=" + H(p) + " s=" + H(s) + " a=" + H(angle) + " axis=" + H(axis)
                    + " rot=" + H(Matrix4x4.Rotate(q)) + " trs=" + H(Matrix4x4.TRS(p, q, s)) + " aa=" + H(aa) + " rotaa=" + H(Matrix4x4.Rotate(aa)));
            }
        }

        static void Cameras(System.Random rng)
        {
            Rec.Log("PJ screen " + Screen.width + "x" + Screen.height);
            var go = new GameObject("ProjectionProbeCamera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            var t = go.transform;
            for (int i = 0; i < 240; i++)
            {
                bool ortho = i % 2 == 0;
                cam.orthographic = ortho;
                cam.orthographicSize = i % 6 == 0 ? rng.Next(1, 40) : Pos(rng, 0.5, 200);
                cam.fieldOfView = i % 6 == 1 ? 60 : (float)(rng.NextDouble() * 120 + 10);
                cam.nearClipPlane = i % 4 < 2 ? (ortho ? -100 : 0.3f) : Pos(rng, 0.01, 10);
                cam.farClipPlane = i % 4 < 2 ? 1000 : cam.nearClipPlane + Pos(rng, 1, 1e4);
                cam.rect = i % 5 == 0 ? new Rect(0, 0, 1, 1)
                    : i % 5 == 1 ? new Rect(0.25f, 0.1f, 0.5f, 0.7f)
                    : new Rect((float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() * 0.5f + 0.1f, (float)rng.NextDouble() * 0.5f + 0.1f);
                if (i % 7 == 3) cam.aspect = Pos(rng, 0.5, 3);
                else cam.ResetAspect();
                t.position = i % 8 == 0 ? new Vector3(rng.Next(-300, 300), rng.Next(-300, 300), -100) : DrawV(rng);
                t.rotation = i % 3 == 0 ? Quaternion.identity : DrawQ(rng);
                t.localScale = i % 9 == 4 ? new Vector3(2, 0.5f, 3) : Vector3.one;
                Rec.Log("PJ PC " + i + " ortho=" + (ortho ? 1 : 0) + " size=" + H(cam.orthographicSize) + " fov=" + H(cam.fieldOfView)
                    + " near=" + H(cam.nearClipPlane) + " far=" + H(cam.farClipPlane) + " aspect=" + H(cam.aspect) + " rect=" + H(cam.rect)
                    + " prect=" + H(cam.pixelRect) + " pw=" + cam.pixelWidth + " ph=" + cam.pixelHeight
                    + " pos=" + H(t.position) + " rot=" + H(t.rotation) + " scale=" + H(t.localScale)
                    + " proj=" + H(cam.projectionMatrix) + " w2c=" + H(cam.worldToCameraMatrix) + " c2w=" + H(cam.cameraToWorldMatrix)
                    + " l2w=" + H(t.localToWorldMatrix) + " w2l=" + H(t.worldToLocalMatrix));
                for (int k = 0; k < 6; k++)
                {
                    Vector3 w = k == 0 ? t.position + t.forward * Pos(rng, 0.5, 500) : t.position + DrawV(rng);
                    var pr = cam.pixelRect;
                    Vector3 sp = k == 1 ? new Vector3(pr.x + pr.width * 0.5f, pr.y + pr.height * 0.5f, 10)
                        : k == 2 ? new Vector3(rng.Next(0, 1024), rng.Next(0, 768), rng.Next(-5, 5) == 0 ? 0 : Pos(rng, 0.1, 1000))
                        : new Vector3((float)(rng.NextDouble() * 1200 - 100), (float)(rng.NextDouble() * 900 - 60), k == 3 ? cam.nearClipPlane : k == 4 ? -Pos(rng, 0.1, 50) : Pos(rng, 0.1, 1000));
                    Vector3 vp = new Vector3((float)(rng.NextDouble() * 1.4 - 0.2), (float)(rng.NextDouble() * 1.4 - 0.2), sp.z);
                    Rec.Log("PJ PCP " + i + " " + k + " w=" + H(w) + " sp=" + H(sp) + " vp=" + H(vp)
                        + " w2s=" + H(cam.WorldToScreenPoint(w)) + " w2v=" + H(cam.WorldToViewportPoint(w))
                        + " s2w=" + H(cam.ScreenToWorldPoint(sp)) + " v2w=" + H(cam.ViewportToWorldPoint(vp))
                        + " s2v=" + H(cam.ScreenToViewportPoint(sp)) + " v2s=" + H(cam.ViewportToScreenPoint(vp))
                        + " sray=" + H(cam.ScreenPointToRay(sp)) + " vray=" + H(cam.ViewportPointToRay(vp)));
                }
            }
            UnityEngine.Object.Destroy(go);
        }

        // The smallest positive float s at which pred(s) is false, given pred(lo) and !pred(hi),
        // by bisection over the bit patterns.
        static float Boundary(System.Func<float, bool> pred, float lo, float hi)
        {
            int a = BitConverter.ToInt32(BitConverter.GetBytes(lo), 0), b = BitConverter.ToInt32(BitConverter.GetBytes(hi), 0);
            while (b - a > 1)
            {
                int mid = a + (b - a) / 2;
                if (pred(BitConverter.ToSingle(BitConverter.GetBytes(mid), 0))) a = mid; else b = mid;
            }
            return BitConverter.ToSingle(BitConverter.GetBytes(b), 0);
        }

        // Where LookAt and AngleAxis fall back to the identity (lines "PT"): the direction's,
        // the right vector's and the axis's sizes, found by bisection, then the cases on either
        // side of each boundary.
        static void Thresholds()
        {
            int i = 0;
            void Look(string what, Vector3 from, Vector3 to, Vector3 up)
                => Rec.Log("PJ PL t" + i++ + " from=" + H(from) + " to=" + H(to) + " up=" + H(up) + " m=" + H(Matrix4x4.LookAt(from, to, up)) + " what=" + what);
            bool Fallback(Matrix4x4 m) => m.m00 == 1f && m.m11 == 1f && m.m22 == 1f;
            var dirs = new[] { new Vector3(1, 1, 1), new Vector3(1, 0, 0), new Vector3(3, -2, 5) };
            foreach (var dv in dirs)
            {
                float s = Boundary(x => Fallback(Matrix4x4.LookAt(Vector3.zero, dv * x, Vector3.up)), 1e-38f, 1f);
                Rec.Log("PJ PT dir " + H(dv) + " boundary=" + H(s));
                foreach (float x in new[] { s, BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(s), 0) - 1), 0) })
                {
                    Look("dir", Vector3.zero, dv * x, Vector3.up);
                    Look("dir", new Vector3(1, 2, 3), new Vector3(1, 2, 3) + dv * x, Vector3.up);
                }
                float big = Boundary(x => !Fallback(Matrix4x4.LookAt(Vector3.zero, dv * x, Vector3.up)), 1f, 3e38f);
                Rec.Log("PJ PT dirbig " + H(dv) + " boundary=" + H(big));
                Look("dirbig", Vector3.zero, dv * big, Vector3.up);
            }
            foreach (float k in new[] { 1f, 1000f, 1e-3f })
            {
                // up = (k, k*s, 0) against the direction (1, 0, 0): right = up x dir = (0, 0, -k*s)
                float s = Boundary(x => Fallback(Matrix4x4.LookAt(Vector3.zero, Vector3.right, new Vector3(k, k * x, 0))), 1e-38f, 1f);
                Rec.Log("PJ PT up k=" + H(k) + " boundary=" + H(s));
                Look("up", Vector3.zero, Vector3.right, new Vector3(k, k * s, 0));
                Look("up", Vector3.zero, Vector3.right * 7, new Vector3(k, k * s, 0));
            }
            for (int e = -1; e > -40; e -= 3)
            {
                float x = (float)Math.Pow(10, e);
                Look("sweep", Vector3.zero, new Vector3(x, 2 * x, -x), Vector3.up);
                Look("sweep", Vector3.zero, Vector3.right, new Vector3(0.3f, x, 0.2f));
                Look("sweep", Vector3.zero, Vector3.forward, new Vector3(x, 0, 0));
            }
            int j = 0;
            void Axis(float angle, Vector3 axis)
                => Rec.Log("PJ PR t" + j++ + " a=" + H(angle) + " axis=" + H(axis) + " aa=" + H(Quaternion.AngleAxis(angle, axis)));
            foreach (var av in new[] { new Vector3(1, 0, 0), new Vector3(1, 2, 3) })
            {
                float s = Boundary(x => Quaternion.AngleAxis(90, av * x) == new Quaternion(0, 0, 0, 1) && Quaternion.AngleAxis(90, av * x).w == 1f, 1e-38f, 1f);
                Rec.Log("PJ PT axis " + H(av) + " boundary=" + H(s));
                Axis(90, av * s);
                Axis(90, av * BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(s), 0) - 1), 0));
                Axis(90, av * 1e30f);
                Axis(90, av * 3e38f);
            }
            foreach (float a in new[] { 0f, -0f, 180f, -180f, 360f, 720f, 1e-30f, 1e10f, float.NaN, float.PositiveInfinity })
                Axis(a, new Vector3(0.3f, -0.4f, 0.5f));
            Axis(90, new Vector3(float.NaN, 0, 1));
            Axis(90, new Vector3(float.PositiveInfinity, 0, 1));
        }

        // Where Inverse3DAffine gives up (lines "PJ PT affine"): the smallest scale it inverts,
        // found by bisection for (x, 1, 1), (x, x, x) and (-x, 1, 1), and the cases on either side.
        static void AffineThresholds()
        {
            var r = Matrix4x4.identity;
            bool Fails(Matrix4x4 m) => !Matrix4x4.Inverse3DAffine(m, ref r);
            int i = 0;
            void Log(string what, Matrix4x4 m)
            {
                var o = Rows(7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7);
                bool ok = Matrix4x4.Inverse3DAffine(m, ref o);
                Rec.Log("PJ PA t" + i++ + " m=" + H(m) + " ok=" + (ok ? 1 : 0) + " r=" + H(o) + " what=" + what);
            }
            System.Func<float, Matrix4x4>[] shapes =
            {
                x => Matrix4x4.Scale(new Vector3(x, 1, 1)),
                x => Matrix4x4.Scale(new Vector3(x, x, x)),
                x => Matrix4x4.Scale(new Vector3(-x, 1, 1)),
            };
            string[] names = { "x11", "xxx", "-x11" };
            for (int k = 0; k < shapes.Length; k++)
            {
                var shape = shapes[k];
                float s = Boundary(x => Fails(shape(x)), 1e-38f, 1f);
                Rec.Log("PJ PT affine " + names[k] + " boundary=" + H(s));
                Log(names[k], shape(s));
                Log(names[k], shape(BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(s), 0) - 1), 0)));
            }
            Log("rows", Rows(1e-5f, 0, 0, 0, 0, 1e5f, 0, 0, 0, 0, 1e-5f, 0, 0, 0, 0, 1));
            Log("rows", Rows(0, 1e-3f, 0, 0, 1e-3f, 0, 0, 0, 0, 0, 1e-3f, 0, 0, 0, 0, 1));
            Log("rows", Rows(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        // Camera rect rules (lines "PJ PK <case> <step>"): rects and pixel rects set out of range,
        // at halves of a pixel and on a target texture, the aspect set and then the rect changed,
        // and worldToCameraMatrix of cameras at the origin, rotated, and under a rotated and
        // scaled parent. Each step logs rect, pixelRect, pixelWidth/Height, aspect,
        // projectionMatrix's m00 and m11, worldToCameraMatrix and WorldToScreenPoint of a point
        // 10 in front of the camera (which shows the viewport the conversions use).
        static void CameraRules()
        {
            var go = new GameObject("CameraRulesProbe");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            var t = go.transform;
            int i = 0, step = 0;
            void Log(string what)
            {
                var c = t.position + t.forward * 10;
                Rec.Log("PJ PK " + i + " " + step++ + " what=" + what + " rect=" + H(cam.rect) + " prect=" + H(cam.pixelRect)
                    + " pw=" + cam.pixelWidth + " ph=" + cam.pixelHeight + " aspect=" + H(cam.aspect)
                    + " p00=" + H(cam.projectionMatrix.m00) + " p11=" + H(cam.projectionMatrix.m11)
                    + " pos=" + H(t.position) + " rot=" + H(t.rotation)
                    + " w2c=" + H(cam.worldToCameraMatrix) + " c2w=" + H(cam.cameraToWorldMatrix)
                    + " w2s=" + H(cam.WorldToScreenPoint(c)) + " s2w=" + H(cam.ScreenToWorldPoint(new Vector3(100.5f, 200.5f, 7)))
                    + " sray=" + H(cam.ScreenPointToRay(new Vector3(100.5f, 200.5f, 7))) + " vray=" + H(cam.ViewportPointToRay(new Vector3(0.3f, 0.6f, 7))));
            }
            void Next() { i++; step = 0; }
            var rects = new[]
            {
                new Rect(0, 0, 1, 1), new Rect(-0.5f, 0, 1, 1), new Rect(0.5f, 0, 1, 1), new Rect(1.5f, 0, 0.5f, 1),
                new Rect(0.2f, 0.2f, -0.1f, 0.5f), new Rect(0.2f, 0.2f, 0, 0.5f), new Rect(0, 0, 2, 2), new Rect(-1, -1, 3, 3),
                new Rect(0.3f, 0.3f, 0.5f, 0), new Rect(0.9f, 0.9f, 0.5f, 0.5f), new Rect(-0.2f, -0.2f, 0.5f, 0.5f),
                new Rect(0.5f, 0.5f, -1, -1), new Rect(0.5f, -2, 0.25f, 1), new Rect(-0f, -0f, 1, 1), new Rect(0.25f, 0.25f, 1e-7f, 1e-7f),
                new Rect(Nan, 0, 1, 1), new Rect(0, 0, Inf, 1), new Rect(0, 0, 1, Nan),
            };
            foreach (bool ortho in new[] { false, true })
            {
                cam.orthographic = ortho;
                foreach (var r in rects) { cam.rect = r; Log("rect " + H(r)); Next(); }
            }
            cam.orthographic = false;
            var prects = new[]
            {
                new Rect(10.5f, 20.5f, 100.5f, 50.5f), new Rect(11.5f, 21.5f, 100, 50), new Rect(0.5f, 0.5f, 1, 1), new Rect(1.5f, 2.5f, 3, 4),
                new Rect(2.5f, 3.5f, 4.5f, 5.5f), new Rect(-10, -10, 100, 100), new Rect(1000, 700, 100, 100), new Rect(0, 0, 2048, 1536),
                new Rect(100.25f, 100.75f, 300.5f, 200.5f), new Rect(0.4999f, 0.5001f, 10.4999f, 10.5001f), new Rect(0, 0, 0, 0),
                new Rect(1023.5f, 767.5f, 1, 1),
            };
            foreach (var r in prects) { cam.pixelRect = r; Log("prect " + H(r)); Next(); }
            // the aspect set, then the rect, pixel rect, projection kind and screen-independent settings changed
            cam.rect = new Rect(0, 0, 1, 1); cam.ResetAspect();
            Log("reset"); cam.aspect = 2; Log("aspect 2"); cam.rect = new Rect(0.1f, 0.1f, 0.5f, 0.5f); Log("rect after aspect");
            cam.pixelRect = new Rect(10, 10, 300, 100); Log("prect after aspect"); cam.orthographic = true; Log("ortho after aspect");
            cam.orthographic = false; cam.fieldOfView = 50; Log("fov after aspect"); cam.ResetAspect(); Log("reset");
            cam.aspect = 0; Log("aspect 0"); cam.aspect = -1.5f; Log("aspect -1.5"); cam.aspect = Nan; Log("aspect NaN");
            cam.aspect = Inf; Log("aspect inf"); cam.ResetAspect(); Log("reset"); cam.fieldOfView = 60; Next();
            cam.rect = new Rect(0.3f, 0.3f, 0.5f, 0); Log("zero height"); cam.rect = new Rect(0.3f, 0.3f, 0, 0.5f); Log("zero width");
            cam.rect = new Rect(0, 0, 1, 1); Next();
            // a target texture
            var rt = new RenderTexture(300, 200, 0);
            cam.targetTexture = rt; Log("texture 300x200"); cam.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f); Log("texture rect");
            cam.pixelRect = new Rect(10.5f, 20.5f, 100.5f, 50.5f); Log("texture prect"); cam.targetTexture = null; Log("texture removed");
            rt.Release(); UnityEngine.Object.Destroy(rt);
            cam.rect = new Rect(0, 0, 1, 1); Next();
            // worldToCameraMatrix's signs of zero and its parts
            var posePositions = new[]
            {
                Vector3.zero, new Vector3(1, 2, 0), new Vector3(-0f, 0, -0f), new Vector3(3, -4, 5), new Vector3(3, -4, 5), Vector3.zero, Vector3.one,
            };
            var poseRotations = new[]
            {
                Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.Euler(0, 180, 0), Quaternion.Euler(90, 0, 0),
                new Quaternion(0, 0, 0, -1), Quaternion.Euler(30, 45, 60),
            };
            for (int k = 0; k < posePositions.Length; k++) { t.position = posePositions[k]; t.rotation = poseRotations[k]; Log("pose"); Next(); }
            var parent = new GameObject("CameraRulesParent").transform;
            parent.position = new Vector3(5, -3, 2); parent.rotation = Quaternion.Euler(10, 20, 30); parent.localScale = new Vector3(2, 0.5f, -3);
            t.SetParent(parent, false);
            t.localPosition = new Vector3(1, 2, 3); t.localRotation = Quaternion.Euler(-40, 15, 70); t.localScale = new Vector3(1.5f, 1, 1);
            Log("child"); Next();
            t.SetParent(null, false);
            UnityEngine.Object.Destroy(parent.gameObject);
            t.localScale = Vector3.one;

            // Where ScreenToWorldPoint gives up (it returns zero): the clip planes at which the
            // depth-0.95 point comes too close to the camera, by bisection ("PJ PQ bound").
            var center = new Vector3(512, 384, 5);
            t.position = Vector3.zero; t.rotation = Quaternion.identity;
            cam.rect = new Rect(0, 0, 1, 1); cam.ResetAspect();
            bool Zero(Vector3 v) => v.x == 0 && v.y == 0 && v.z == 0;
            cam.orthographic = false; cam.fieldOfView = 60;
            float pb = Boundary(n => { cam.nearClipPlane = n; cam.farClipPlane = 3 * n; return Zero(cam.ScreenToWorldPoint(center)); }, 1e-30f, 1f);
            Rec.Log("PJ PQ bound perspective near=" + H(pb));
            foreach (float n in new[] { pb, BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(pb), 0) - 1), 0) })
            {
                cam.nearClipPlane = n; cam.farClipPlane = 3 * n;
                Log("bound perspective"); Next();
            }
            cam.orthographic = true; cam.orthographicSize = 5;
            float ob = Boundary(n => { cam.nearClipPlane = -n; cam.farClipPlane = n; return Zero(cam.ScreenToWorldPoint(center)); }, 1e-30f, 1f);
            Rec.Log("PJ PQ bound ortho n=" + H(ob));
            foreach (float n in new[] { ob, BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(ob), 0) - 1), 0) })
            {
                cam.nearClipPlane = -n; cam.farClipPlane = n;
                Log("bound ortho"); Next();
            }
            // rays of simple cameras, with the screen points at several distances ("PJ PQ ray")
            cam.orthographic = false; cam.nearClipPlane = 0.3f; cam.farClipPlane = 1000;
            var poses2 = new[] { Vector3.zero, new Vector3(10, 20, 30), new Vector3(10, 20, 30) };
            var rots2 = new[] { Quaternion.identity, Quaternion.identity, Quaternion.Euler(0, 30, 0) };
            var sps = new[] { new Vector3(0, 0, 0), new Vector3(512, 384, 0), new Vector3(100.5f, 200.5f, 0), new Vector3(1000, 700, 0), new Vector3(300, 600, 0), new Vector3(513, 384, 0) };
            for (int k = 0; k < poses2.Length; k++)
            {
                t.position = poses2[k]; t.rotation = rots2[k];
                foreach (var sp in sps)
                {
                    var sb = new StringBuilder("PJ PQ ray " + k + " sp=" + H(sp) + " sray=" + H(cam.ScreenPointToRay(sp)) + " c2w=" + H(cam.cameraToWorldMatrix));
                    foreach (float z in new[] { 0.3f, 1f, 10f, 1000f, 1e6f })
                        sb.Append(" s2w").Append(z).Append('=').Append(H(cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, z))));
                    Rec.Log(sb.ToString());
                }
            }
            // a ray of a rotated camera whose viewport is empty
            t.position = new Vector3(1, 2, 3); t.rotation = Quaternion.Euler(0, 90, 0);
            cam.rect = new Rect(0.3f, 0.3f, 0, 0.5f); Log("empty rotated"); Next();
            cam.orthographic = true; Log("empty rotated ortho"); Next();
            UnityEngine.Object.Destroy(go);
        }

        public static void Run()
        {
            Guard("PK", CameraRules);
            Guard("PAT", AffineThresholds);
            Guard("PT", Thresholds);
            Guard("PP", () => Perspective(new System.Random(1)));
            Guard("PO", () => Ortho(new System.Random(2)));
            Guard("PF", () => Frustum(new System.Random(2)));
            Guard("PL", () => LookAt(new System.Random(3)));
            Guard("PA", () => Affine(new System.Random(4)));
            Guard("PR", () => Rotations(new System.Random(5)));
            Guard("PC", () => Cameras(new System.Random(6)));
            Rec.Log("PJ done");
        }
    }
}
