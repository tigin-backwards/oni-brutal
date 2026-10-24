// Transform matrices to the bit (lines "PROBE ... TM ...", "TN ...", "TR ..."), at mod load.
// Found with Resize Dup UI (Workshop 2549295369): the viewport's worldToLocalMatrix under the
// in-game canvas (scale 0.86) had a translation of -367.6443 in the original, which is
// -(316.1741 x (1 / 0.86)) rounded once, where ours inverted the whole world matrix (-367.64432).
//
// TM: each case builds a chain of 1 to 4 transforms from a seeded System.Random: positions of
// mixed magnitudes, scales of 0.25 to 4 (uniform in half the levels), and in half the cases a
// rotation at some levels. It logs, as float bits in hex, every level's local position, rotation
// and scale, and the leaf's localToWorldMatrix, worldToLocalMatrix, position, TransformPoint and
// InverseTransformPoint of a drawn point.
//
// TN: 1200 chains of 1 to 6 transforms in ten kinds (translation only, uniform scale, non-uniform
// and negative scale, rotation only, rotation with translation, with uniform scale, with
// non-uniform scale, z rotations with (s, s, 1) scales as UI uses, a mix of these, and a scale
// with a zero axis). For every level, one line: the local values read back and the level's
// localToWorldMatrix, worldToLocalMatrix, position, rotation, lossyScale, and TransformPoint,
// InverseTransformPoint, TransformDirection, InverseTransformDirection, TransformVector and
// InverseTransformVector of the case's drawn vector. Then "TNS": the leaf's localRotation set to
// an unnormalized quaternion and read back, its position and rotation set in world space and
// the local values read back, and the leaf re-parented with worldPositionStays under a new root.
//
// TR: 400 chains of 1 to 6 RectTransforms with drawn anchors, pivots, sizes, anchored positions,
// and in some levels rotations and scales. For every level: the inputs, localPosition, rect,
// GetLocalCorners, GetWorldCorners and the matrices; again ("TRB") after the root is resized.
//
// tools/UnityProbe/tm-check.py compares the TM lines with candidate formulas. Run alone with
// PROBE_SCENARIO=transform.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SemanticsProbe
{
    public static class TransformMath
    {
        static string H(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("x8");
        static string H(Vector2 v) => H(v.x) + "," + H(v.y);
        static string H(Vector3 v) => H(v.x) + "," + H(v.y) + "," + H(v.z);
        static string H(Quaternion q) => H(q.x) + "," + H(q.y) + "," + H(q.z) + "," + H(q.w);
        static string H(Rect r) => H(r.x) + "," + H(r.y) + "," + H(r.width) + "," + H(r.height);

        static string H(Matrix4x4 m)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    sb.Append(r == 0 && c == 0 ? "" : ",").Append(H(m[r, c]));
            return sb.ToString();
        }

        static string H(Vector3[] vs)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < vs.Length; i++) sb.Append(i == 0 ? "" : ",").Append(H(vs[i]));
            return sb.ToString();
        }

        static float Draw(System.Random rng)
        {
            double mag = Math.Pow(10, rng.Next(-1, 4));
            return (float)((rng.NextDouble() * 2 - 1) * mag);
        }

        static float Scale(System.Random rng) => (float)Math.Pow(2, rng.NextDouble() * 4 - 2);

        static float Signed(System.Random rng) => rng.Next(3) == 0 ? -Scale(rng) : Scale(rng);

        static Vector3 DrawV(System.Random rng) => new Vector3(Draw(rng), Draw(rng), Draw(rng));

        static Quaternion DrawQ(System.Random rng)
        {
            switch (rng.Next(4))
            {
                case 0:
                    return Quaternion.Euler(90 * rng.Next(4), 90 * rng.Next(4), 90 * rng.Next(4));
                case 1:
                {
                    // A normalized random quaternion from managed arithmetic.
                    var q = new Quaternion((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1));
                    return Quaternion.Normalize(q);
                }
                default:
                    return Quaternion.Euler((float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360));
            }
        }

        static float Ui(System.Random rng) => (float)(Math.Round((rng.NextDouble() * 2 - 1) * 1000) / 2);

        static void Level(System.Random rng, int kind, int d, Transform t)
        {
            if (kind == 8) kind = rng.Next(8);
            Vector3 p = DrawV(rng);
            Quaternion q = Quaternion.identity;
            float s = Scale(rng);
            Vector3 sc = Vector3.one;
            switch (kind)
            {
                case 0: break;
                case 1: sc = new Vector3(s, s, s); break;
                case 2: sc = new Vector3(Signed(rng), Signed(rng), Signed(rng)); break;
                case 3: p = Vector3.zero; q = DrawQ(rng); break;
                case 4: q = DrawQ(rng); break;
                case 5: q = DrawQ(rng); sc = new Vector3(s, s, s); break;
                case 6: q = DrawQ(rng); sc = new Vector3(Signed(rng), Signed(rng), Signed(rng)); break;
                case 7:
                    p = new Vector3(Ui(rng), Ui(rng), rng.Next(3) == 0 ? Ui(rng) : 0f);
                    if (rng.Next(2) == 0) q = Quaternion.Euler(0, 0, rng.Next(2) == 0 ? 90 * rng.Next(4) : (float)(rng.NextDouble() * 360));
                    sc = rng.Next(2) == 0 ? Vector3.one : new Vector3(s, s, 1);
                    break;
                case 9:
                    if (rng.Next(2) == 0) q = DrawQ(rng);
                    sc = new Vector3(Signed(rng), Signed(rng), Signed(rng));
                    if (rng.Next(2) == 0) sc[rng.Next(3)] = 0f;
                    break;
            }
            t.localPosition = p;
            t.localRotation = q;
            t.localScale = sc;
        }

        static string Ops(Transform t, Vector3 v)
            => " lp=" + H(t.localPosition) + " lq=" + H(t.localRotation) + " ls=" + H(t.localScale)
               + " l2w=" + H(t.localToWorldMatrix) + " w2l=" + H(t.worldToLocalMatrix)
               + " pos=" + H(t.position) + " rot=" + H(t.rotation) + " lossy=" + H(t.lossyScale)
               + " tp=" + H(t.TransformPoint(v)) + " itp=" + H(t.InverseTransformPoint(v))
               + " td=" + H(t.TransformDirection(v)) + " itd=" + H(t.InverseTransformDirection(v))
               + " tv=" + H(t.TransformVector(v)) + " itv=" + H(t.InverseTransformVector(v));

        // TQ: one scale value s in several contexts (other axes, translations, a parent), to see
        // whether the inverse's 1/s depends on anything but s.
        static void Reciprocals()
        {
            var rng = new System.Random(8192);
            var a = new GameObject("tq_a").transform;
            var b = new GameObject("tq_b").transform;
            b.SetParent(a, false);
            for (int i = 0; i < 300; i++)
            {
                float s = i < 100 ? Scale(rng) : Signed(rng) * (float)Math.Pow(10, rng.Next(-3, 4));
                var ctx = new StringBuilder();
                Vector3[] scales = { new Vector3(s, 1, 1), new Vector3(1, s, 1), new Vector3(s, s, s), new Vector3(s, 2, 3), new Vector3(3, 2, s) };
                Vector3[] positions = { Vector3.zero, new Vector3(1, 2, 3), new Vector3(Draw(rng), Draw(rng), Draw(rng)) };
                foreach (var sc in scales)
                    foreach (var p in positions)
                    {
                        a.localPosition = p;
                        a.localRotation = Quaternion.identity;
                        a.localScale = sc;
                        ctx.Append(" " + H(a.worldToLocalMatrix.m00) + "," + H(a.worldToLocalMatrix.m11) + "," + H(a.worldToLocalMatrix.m22) + "," + H(b.worldToLocalMatrix.m00));
                    }
                a.localPosition = Vector3.zero;
                a.localScale = Vector3.one;
                b.localScale = new Vector3(s, s, s);
                ctx.Append(" child=" + H(b.worldToLocalMatrix.m00));
                b.localScale = Vector3.one;
                Rec.Log("TQ " + i + " s=" + H(s) + " div=" + H(1f / s) + " minv=" + H(Matrix4x4.Scale(new Vector3(s, s, s)).inverse.m00) + ctx);
            }
            UnityEngine.Object.DestroyImmediate(a.gameObject);

            // TQS: 1/s through worldToLocalMatrix for structured values: consecutive floats, the
            // same mantissa at other exponents and signs, and short decimals.
            var t = new GameObject("tq_s").transform;
            var values = new List<float>();
            foreach (float c in new[] { 1.12026942f, 1.5f, 1.7f, 0.9035225f })
            {
                int cb = BitConverter.ToInt32(BitConverter.GetBytes(c), 0);
                for (int k = -64; k <= 64; k++) values.Add(BitConverter.ToSingle(BitConverter.GetBytes(cb + k), 0));
            }
            for (int m = 0; m < 128; m++)
                foreach (int e in new[] { -3, 0, 1, 5 })
                {
                    float v = (1f + m / 128f + 1f / 4096f) * (float)Math.Pow(2, e);
                    values.Add(v);
                    values.Add(-v);
                }
            foreach (float v in new[] { 3f, 5f, 7f, 9f, 10f, 100f, 0.1f, 0.3f, 0.7f, 1.1f, 1.3f, 2.5f, 0.86f, 1.25f, 0.001f, 1000f, 1e-6f, 1e6f, 1e-20f, 1e20f, 1e-38f, 1e38f, 3e-39f })
            {
                values.Add(v);
                values.Add(-v);
            }
            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                t.localScale = new Vector3(values[i], 1, 1);
                sb.Append(" ").Append(H(values[i])).Append(":").Append(H(t.worldToLocalMatrix.m00));
                if (i % 64 == 63 || i == values.Count - 1)
                {
                    Rec.Log("TQS" + sb);
                    sb.Length = 0;
                }
            }
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        // TD: TransformDirection of basis and other vectors under one rotation, at several scales,
        // for the formula that rotates a vector by a quaternion.
        static void Directions()
        {
            var rng = new System.Random(16384);
            var t = new GameObject("td").transform;
            Vector3[] scales = { Vector3.one, new Vector3(2, 2, 2), new Vector3(1, 2, 3) };
            for (int i = 0; i < 300; i++)
            {
                var q = DrawQ(rng);
                t.localRotation = q;
                Vector3[] vs = { Vector3.right, Vector3.up, Vector3.forward, new Vector3(0, 68.1501617f, -0.0708870143f), new Vector3(1, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 0, 1), DrawV(rng), DrawV(rng) };
                var sb = new StringBuilder("TD " + i + " q=" + H(t.localRotation));
                foreach (var sc in scales)
                {
                    t.localScale = sc;
                    sb.Append(" s=" + H(sc));
                    foreach (var v in vs) sb.Append(" " + H(v) + ":" + H(t.TransformDirection(v)) + ":" + H(t.TransformVector(v)));
                }
                Rec.Log(sb.ToString());
            }
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        // TQN: localRotation set to a unit quaternion scaled by 1 + k·2^-24 (k from -64 to 64,
        // and a few larger factors), read back: which lengths the setter normalizes.
        static void Normalization()
        {
            var rng = new System.Random(32768);
            var t = new GameObject("tqn").transform;
            for (int i = 0; i < 40; i++)
            {
                var u = i < 8 ? Quaternion.Euler(90 * (i % 4), 90 * (i / 4), 0) : DrawQ(rng);
                var sb = new StringBuilder("TQN " + i + " u=" + H(u));
                var factors = new List<double>();
                for (int k = -64; k <= 64; k++) factors.Add(1 + k * Math.Pow(2, -24));
                foreach (double f in new[] { 0.999, 0.9999, 0.99999, 1.00001, 1.0001, 1.001, 0.5, 2.0 }) factors.Add(f);
                foreach (double f in factors)
                {
                    var q = new Quaternion((float)(u.x * f), (float)(u.y * f), (float)(u.z * f), (float)(u.w * f));
                    t.localRotation = q;
                    sb.Append(" " + H(q) + ":" + H(t.localRotation));
                }
                Rec.Log(sb.ToString());
            }
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        public static void Run()
        {
            Normalization();
            Directions();
            Reciprocals();
            Chains();
            Hierarchies();
            Rects();
            Rec.Log("TM done");
        }

        static void Chains()
        {
            var rng = new System.Random(1024);
            for (int i = 0; i < 300; i++)
            {
                int depth = 1 + i % 4;
                bool rotate = i % 2 == 1;
                Transform parent = null, leaf = null;
                var levels = new StringBuilder();
                GameObject root = null;
                for (int d = 0; d < depth; d++)
                {
                    var go = new GameObject("tm" + i + "_" + d);
                    if (root == null) root = go;
                    var t = go.transform;
                    if (parent != null) t.SetParent(parent, false);
                    t.localPosition = new Vector3(Draw(rng), Draw(rng), Draw(rng));
                    if (rotate && rng.Next(2) == 0)
                        t.localRotation = Quaternion.Euler((float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 360));
                    float s = Scale(rng);
                    t.localScale = rng.Next(2) == 0 ? new Vector3(s, s, s) : new Vector3(s, Scale(rng), Scale(rng));
                    levels.Append(" L" + d + "=" + H(t.localPosition) + ";" + H(t.localRotation) + ";" + H(t.localScale));
                    parent = t;
                    leaf = t;
                }
                var p = new Vector3(Draw(rng), Draw(rng), Draw(rng));
                Rec.Log("TM " + i + levels + " p=" + H(p) + " l2w=" + H(leaf.localToWorldMatrix) + " w2l=" + H(leaf.worldToLocalMatrix)
                    + " pos=" + H(leaf.position) + " tp=" + H(leaf.TransformPoint(p)) + " itp=" + H(leaf.InverseTransformPoint(p)));
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void Hierarchies()
        {
            var rng = new System.Random(2048);
            for (int i = 0; i < 1200; i++)
            {
                int kind = i % 10, depth = 1 + (i / 10) % 6;
                var v = DrawV(rng);
                Rec.Log("TNC " + i + " kind=" + kind + " depth=" + depth + " v=" + H(v));
                Transform parent = null;
                var chain = new List<Transform>();
                for (int d = 0; d < depth; d++)
                {
                    var t = new GameObject("tn" + i + "_" + d).transform;
                    if (parent != null) t.SetParent(parent, false);
                    Level(rng, kind, d, t);
                    chain.Add(t);
                    parent = t;
                }
                for (int d = 0; d < depth; d++) Rec.Log("TN " + i + " " + d + Ops(chain[d], v));

                var leaf = chain[depth - 1];
                var qn = new Quaternion((float)(rng.NextDouble() * 4 - 2), (float)(rng.NextDouble() * 4 - 2), (float)(rng.NextDouble() * 4 - 2), (float)(rng.NextDouble() * 4 - 2));
                leaf.localRotation = qn;
                var lqn = leaf.localRotation;
                var wp = DrawV(rng);
                leaf.position = wp;
                var lpw = leaf.localPosition;
                var wq = DrawQ(rng);
                leaf.rotation = wq;
                var lqw = leaf.localRotation;
                var nr = new GameObject("tn" + i + "_r").transform;
                Level(rng, kind == 9 ? 6 : kind, 0, nr);
                leaf.SetParent(nr, true);
                Rec.Log("TNS " + i + " qn=" + H(qn) + " lqn=" + H(lqn) + " wp=" + H(wp) + " lpw=" + H(lpw) + " wq=" + H(wq) + " lqw=" + H(lqw)
                    + " nr=" + H(nr.localPosition) + ";" + H(nr.localRotation) + ";" + H(nr.localScale)
                    + " rp=" + H(leaf.localPosition) + " rq=" + H(leaf.localRotation) + " rs=" + H(leaf.localScale));
                // The leaf is under the new root now; with one level it is chain[0].
                if (depth > 1) UnityEngine.Object.DestroyImmediate(chain[0].gameObject);
                UnityEngine.Object.DestroyImmediate(nr.gameObject);
            }
        }

        static float Anchor(System.Random rng)
        {
            switch (rng.Next(4))
            {
                case 0: return 0f;
                case 1: return 0.5f;
                case 2: return 1f;
                default: return (float)rng.NextDouble();
            }
        }

        static string RectOps(RectTransform t, Vector3 v)
        {
            var lc = new Vector3[4];
            var wc = new Vector3[4];
            t.GetLocalCorners(lc);
            t.GetWorldCorners(wc);
            return " amin=" + H(t.anchorMin) + " amax=" + H(t.anchorMax) + " piv=" + H(t.pivot) + " sd=" + H(t.sizeDelta) + " ap=" + H(t.anchoredPosition)
                   + " rect=" + H(t.rect) + " lc=" + H(lc) + " wc=" + H(wc) + Ops(t, v);
        }

        static void Rects()
        {
            var rng = new System.Random(4096);
            for (int i = 0; i < 400; i++)
            {
                int depth = 1 + i % 6;
                bool rotate = (i / 6) % 2 == 1;
                var v = DrawV(rng);
                Rec.Log("TRC " + i + " depth=" + depth + " v=" + H(v));
                RectTransform parent = null;
                var chain = new List<RectTransform>();
                for (int d = 0; d < depth; d++)
                {
                    var t = (RectTransform)new GameObject("tr" + i + "_" + d, typeof(RectTransform)).transform;
                    if (parent != null) t.SetParent(parent, false);
                    float a0 = Anchor(rng), a1 = Anchor(rng), b0 = Anchor(rng), b1 = Anchor(rng);
                    t.anchorMin = new Vector2(Math.Min(a0, a1), Math.Min(b0, b1));
                    t.anchorMax = new Vector2(Math.Max(a0, a1), Math.Max(b0, b1));
                    t.pivot = new Vector2(Anchor(rng), Anchor(rng));
                    t.sizeDelta = new Vector2(rng.Next(2) == 0 ? Ui(rng) : Draw(rng), rng.Next(2) == 0 ? Ui(rng) : Draw(rng));
                    t.anchoredPosition = new Vector2(rng.Next(2) == 0 ? Ui(rng) : Draw(rng), rng.Next(2) == 0 ? Ui(rng) : Draw(rng));
                    if (rng.Next(3) == 0) t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, Draw(rng));
                    if (rotate && rng.Next(2) == 0) t.localRotation = rng.Next(2) == 0 ? Quaternion.Euler(0, 0, (float)(rng.NextDouble() * 360)) : DrawQ(rng);
                    if (rng.Next(2) == 0)
                    {
                        float s = Scale(rng);
                        t.localScale = rng.Next(2) == 0 ? new Vector3(s, s, 1) : new Vector3(Signed(rng), Signed(rng), Signed(rng));
                    }
                    chain.Add(t);
                    parent = t;
                }
                for (int d = 0; d < depth; d++) Rec.Log("TR " + i + " " + d + RectOps(chain[d], v));
                chain[0].sizeDelta = new Vector2(Draw(rng), Draw(rng));
                for (int d = 0; d < depth; d++) Rec.Log("TRB " + i + " " + d + RectOps(chain[d], v));
                UnityEngine.Object.DestroyImmediate(chain[0].gameObject);
            }
        }
    }
}
