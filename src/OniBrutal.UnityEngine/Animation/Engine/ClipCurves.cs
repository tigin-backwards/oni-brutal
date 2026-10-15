using System;
using System.Collections.Generic;
using System.IO;
using OniBrutal.Assets.TypeTrees;

namespace OniBrutal.Engine.Mecanim
{
    /// One GenericBinding of a clip's binding constant: what a group of curves animates.
    internal sealed class ClipBinding
    {
        /// CRC32 of the transform path relative to the Animator ("" = the Animator's own object).
        public uint Path;
        /// For Transform bindings the kind (1 position, 2 rotation, 3 scale, 4 euler); otherwise
        /// the CRC32 of the property path ("m_IsActive", "m_AnchoredPosition.x", "m_fontColor.a").
        public uint Attribute;
        /// Native class ID of the bound component (1 GameObject, 4 Transform, 114 MonoBehaviour…).
        public int ClassId;
        /// Unity's BindingCustomType (0 none, 8 animator muscle, 28 RectTransform, …); for euler
        /// Transform bindings, the rotation order.
        public byte CustomType;
        public bool IsPPtrCurve;
        public bool IsIntCurve;
        /// The script class of MonoBehaviour bindings (null when the script cannot be resolved).
        public Type Script;
        /// First curve of this binding in the clip's curve order, and its curve count.
        public int FirstCurve;
        public int Dimension;

        public const int TransformClassId = 4;
        public const byte AnimatorMuscle = 8;

        public static int DimensionOf(int classId, uint attribute)
            => classId != TransformClassId ? 1 : attribute switch
            {
                1 => 3, // position
                2 => 4, // rotation (quaternion)
                3 => 3, // scale
                4 => 3, // euler angles
                _ => throw new InvalidDataException($"Unknown Transform binding kind {attribute}."),
            };
    }

    /// The curves of a player-build clip (Unity's mecanim "Clip"): streamed curves (cubic segments
    /// keyed per frame), then dense curves (uniformly sampled, linearly interpolated), then constant
    /// curves. Curve i belongs to the binding whose curve range contains i.
    internal sealed class ClipCurves
    {
        public static readonly ClipCurves Empty = new() { Bindings = Array.Empty<ClipBinding>() };

        public ClipBinding[] Bindings;
        public int CurveCount;

        // Streamed: per curve, key times and cubic coefficients (a, b, c, d) with
        // value(t) = ((a·dt + b)·dt + c)·dt + d, dt = t - keyTime, valid until the next key.
        float[][] streamedTimes = Array.Empty<float[]>();
        float[][] streamedCoefficients = Array.Empty<float[]>();

        int denseFrameCount, denseCurveCount;
        float denseSampleRate, denseBeginTime;
        float[] denseSamples = Array.Empty<float>();

        float[] constants = Array.Empty<float>();

        int StreamedCount => streamedTimes.Length;

        /// Decodes m_MuscleClip.m_Clip.data and m_ClipBindingConstant.genericBindings.
        public static ClipCurves Read(Fields clip, ClipBinding[] bindings)
        {
            var c = new ClipCurves { Bindings = bindings };
            var streamed = clip.Struct("m_StreamedClip");
            int streamedCount = Convert.ToInt32(streamed["curveCount"]);
            c.ReadStreamed(streamed.Get<uint[]>("data"), streamedCount);

            var dense = clip.Struct("m_DenseClip");
            c.denseFrameCount = dense.Int("m_FrameCount");
            c.denseCurveCount = Convert.ToInt32(dense["m_CurveCount"]);
            c.denseSampleRate = dense.Float("m_SampleRate");
            c.denseBeginTime = dense.Float("m_BeginTime");
            c.denseSamples = dense.Get<float[]>("m_SampleArray");
            if (c.denseCurveCount > 0 && (c.denseFrameCount <= 0 || c.denseSamples.Length < c.denseCurveCount * c.denseFrameCount))
                throw new InvalidDataException("Dense clip has fewer samples than frames × curves.");

            c.constants = clip.Struct("m_ConstantClip").Get<float[]>("data");
            c.CurveCount = streamedCount + c.denseCurveCount + c.constants.Length;

            int next = 0;
            foreach (var b in bindings)
            {
                b.FirstCurve = next;
                b.Dimension = ClipBinding.DimensionOf(b.ClassId, b.Attribute);
                next += b.Dimension;
            }
            if (next != c.CurveCount)
                throw new InvalidDataException($"Clip bindings describe {next} curves but the clip has {c.CurveCount}.");
            return c;
        }

        /// The streamed data is a sequence of frames: float time, int keyCount, then keyCount keys
        /// of (int curveIndex, float a, b, c, d). The first frame (time -FLT_MAX) holds every curve's
        /// initial value; the last frame (time +inf) is a terminator. A curve listed twice in one
        /// frame keeps its last key.
        void ReadStreamed(uint[] words, int curveCount)
        {
            var times = new List<float>[curveCount];
            var coefficients = new List<float>[curveCount];
            for (int i = 0; i < curveCount; i++)
            {
                times[i] = new List<float>();
                coefficients[i] = new List<float>();
            }
            int p = 0;
            float Float(int index) => BitConverter.UInt32BitsToSingle(words[index]);
            while (p < words.Length)
            {
                if (p + 2 > words.Length) throw new InvalidDataException("Truncated streamed clip frame.");
                float time = Float(p);
                int count = (int)words[p + 1];
                p += 2;
                if (count < 0 || p + count * 5 > words.Length) throw new InvalidDataException("Truncated streamed clip keys.");
                for (int k = 0; k < count; k++, p += 5)
                {
                    int curve = (int)words[p];
                    if ((uint)curve >= (uint)curveCount) throw new InvalidDataException($"Streamed key for curve {curve} of {curveCount}.");
                    var t = times[curve];
                    var co = coefficients[curve];
                    if (t.Count > 0 && t[^1] == time)
                    {
                        co.RemoveRange(co.Count - 4, 4);
                        t.RemoveAt(t.Count - 1);
                    }
                    t.Add(time);
                    co.Add(Float(p + 1));
                    co.Add(Float(p + 2));
                    co.Add(Float(p + 3));
                    co.Add(Float(p + 4));
                }
            }
            streamedTimes = new float[curveCount][];
            streamedCoefficients = new float[curveCount][];
            for (int i = 0; i < curveCount; i++)
            {
                if (times[i].Count == 0) throw new InvalidDataException($"Streamed curve {i} has no keys.");
                streamedTimes[i] = times[i].ToArray();
                streamedCoefficients[i] = coefficients[i].ToArray();
            }
        }

        /// Samples every curve at clip time `time` into output[0..CurveCount).
        public void Sample(float time, float[] output)
        {
            int s = StreamedCount;
            for (int i = 0; i < s; i++) output[i] = SampleStreamed(i, time);
            if (denseCurveCount > 0) SampleDense(time, output, s);
            int k = s + denseCurveCount;
            for (int i = 0; i < constants.Length; i++) output[k + i] = constants[i];
        }

        float SampleStreamed(int curve, float time)
        {
            var times = streamedTimes[curve];
            int lo = 0, hi = times.Length - 1;
            // Last key with keyTime <= time (the first key starts at -FLT_MAX).
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (times[mid] <= time) lo = mid;
                else hi = mid - 1;
            }
            var co = streamedCoefficients[curve];
            int b = lo * 4;
            if (times[lo] == float.MinValue) return co[b + 3];
            float dt = time - times[lo];
            return ((co[b] * dt + co[b + 1]) * dt + co[b + 2]) * dt + co[b + 3];
        }

        void SampleDense(float time, float[] output, int offset)
        {
            float frame = (time - denseBeginTime) * denseSampleRate;
            int last = denseFrameCount - 1;
            int f0;
            float frac;
            if (last <= 0 || frame <= 0f)
            {
                f0 = 0;
                frac = 0f;
            }
            else if (frame >= last)
            {
                f0 = last;
                frac = 0f;
            }
            else
            {
                f0 = (int)MathF.Floor(frame);
                frac = frame - f0;
            }
            int f1 = Math.Min(f0 + 1, Math.Max(last, 0));
            for (int i = 0; i < denseCurveCount; i++)
            {
                float a = denseSamples[f0 * denseCurveCount + i];
                float b = denseSamples[f1 * denseCurveCount + i];
                output[offset + i] = a + (b - a) * frac;
            }
        }
    }
}
