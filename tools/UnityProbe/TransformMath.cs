// Transform matrices to the bit (lines "PROBE ... TM ..."), at mod load. Found with Resize Dup UI
// (Workshop 2549295369): the viewport's worldToLocalMatrix under the in-game canvas (scale 0.86)
// had a translation of -367.6443 in the original, which is -(316.1741 x (1 / 0.86)) rounded once,
// where ours inverted the whole world matrix (-367.64432).
//
// Each case builds a chain of 1 to 4 transforms from a seeded System.Random: positions of mixed
// magnitudes, scales of 0.25 to 4 (uniform in half the levels), and in half the cases a rotation
// at some levels. It logs, as float bits in hex, every level's local position, rotation and scale,
// and the leaf's localToWorldMatrix, worldToLocalMatrix, position, TransformPoint and
// InverseTransformPoint of a drawn point. tools/UnityProbe/tm-check.py compares them with
// candidate formulas. Run alone with PROBE_SCENARIO=transform.
using System;
using System.Text;
using UnityEngine;

namespace SemanticsProbe
{
    public static class TransformMath
    {
        static string H(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("x8");
        static string H(Vector3 v) => H(v.x) + "," + H(v.y) + "," + H(v.z);
        static string H(Quaternion q) => H(q.x) + "," + H(q.y) + "," + H(q.z) + "," + H(q.w);

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
            double mag = Math.Pow(10, rng.Next(-1, 4));
            return (float)((rng.NextDouble() * 2 - 1) * mag);
        }

        static float Scale(System.Random rng) => (float)Math.Pow(2, rng.NextDouble() * 4 - 2);

        public static void Run()
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
            Rec.Log("TM done");
        }
    }
}
