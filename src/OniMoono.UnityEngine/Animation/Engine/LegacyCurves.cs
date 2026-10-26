using System;
using System.Collections.Generic;
using System.IO;
using OniMoono.Assets.Files;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Assets;
using OniMoono.Engine.Particles;
using UnityEngine;

namespace OniMoono.Engine.Mecanim
{
    /// The editor curves of a legacy clip, which the legacy Animation component samples: one
    /// AnimationCurve per animated component, grouped by binding (a Transform's position,
    /// rotation, euler angles or scale; a float property of a component). Bindings use the same
    /// form as mecanim clips (CRC32 path and attribute), so one binder resolves both.
    internal sealed class LegacyCurves
    {
        public static readonly LegacyCurves Empty = new(Array.Empty<ClipBinding>(), Array.Empty<AnimationCurve[]>());

        public readonly ClipBinding[] Bindings;
        /// Per binding, its component curves (3 for vectors, 4 for quaternions, 1 for floats).
        public readonly AnimationCurve[][] Curves;

        LegacyCurves(ClipBinding[] bindings, AnimationCurve[][] curves)
        {
            Bindings = bindings;
            Curves = curves;
        }

        public int Count => Bindings.Length;

        const int PositionKind = 1, RotationKind = 2, ScaleKind = 3, EulerKind = 4;
        const int RectTransformClassId = 224;
        const byte RectTransformCustomType = 28;

        /// Reads m_RotationCurves, m_EulerCurves, m_PositionCurves, m_ScaleCurves and
        /// m_FloatCurves. PPtr curves and compressed rotation curves are not implemented.
        public static LegacyCurves Read(Fields clip, string clipName, SerializedFile file)
        {
            if (clip.Array("m_CompressedRotationCurves").Length > 0)
                throw new InvalidDataException($"AnimationClip {clipName}: compressed legacy rotation curves are not implemented.");
            if (clip.Array("m_PPtrCurves").Length > 0)
                throw new InvalidDataException($"AnimationClip {clipName}: legacy object reference curves are not implemented.");
            var bindings = new List<ClipBinding>();
            var curves = new List<AnimationCurve[]>();

            void Transform(string field, int kind, int dimension)
            {
                foreach (Fields c in clip.Array(field))
                {
                    var curve = c.Struct("curve");
                    bindings.Add(new ClipBinding
                    {
                        Path = Crc32.Of(c.String("path")),
                        Attribute = (uint)kind,
                        ClassId = ClipBinding.TransformClassId,
                        // Euler bindings carry the rotation order, as in mecanim clips.
                        CustomType = kind == EulerKind ? (byte)curve.Int("m_RotationOrder") : (byte)0,
                        Dimension = dimension,
                    });
                    curves.Add(Components(curve, dimension));
                }
            }
            Transform("m_RotationCurves", RotationKind, 4);
            Transform("m_EulerCurves", EulerKind, 3);
            Transform("m_PositionCurves", PositionKind, 3);
            Transform("m_ScaleCurves", ScaleKind, 3);

            foreach (Fields c in clip.Array("m_FloatCurves"))
            {
                int classId = c.Int("classID");
                var attribute = c.String("attribute");
                bindings.Add(new ClipBinding
                {
                    Path = Crc32.Of(c.String("path")),
                    Attribute = Crc32.Of(attribute),
                    ClassId = classId,
                    CustomType = classId == RectTransformClassId && IsRectProperty(attribute) ? RectTransformCustomType : (byte)0,
                    Script = file == null ? null : ScriptTable.Resolve(file, c.PPtr("script")),
                    Dimension = 1,
                });
                curves.Add(new[] { ParticleFields.Curve(c.Struct("curve")) });
            }
            return new LegacyCurves(bindings.ToArray(), curves.ToArray());
        }

        static bool IsRectProperty(string attribute)
            => attribute.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal) || attribute.StartsWith("m_SizeDelta.", StringComparison.Ordinal)
               || attribute.StartsWith("m_AnchorMin.", StringComparison.Ordinal) || attribute.StartsWith("m_AnchorMax.", StringComparison.Ordinal)
               || attribute.StartsWith("m_Pivot.", StringComparison.Ordinal);

        /// A vector or quaternion curve (keys with Vector3/Quaternion values, slopes and weights)
        /// as one AnimationCurve per component.
        static AnimationCurve[] Components(Fields curve, int dimension)
        {
            var keys = curve.Array("m_Curve");
            var result = new AnimationCurve[dimension];
            string[] names = dimension == 4 ? ["x", "y", "z", "w"] : ["x", "y", "z"];
            for (int d = 0; d < dimension; d++)
            {
                var frames = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = (Fields)keys[i];
                    frames[i] = Keyframe.FromSerialized(k.Float("time"), k.Struct("value").Float(names[d]),
                        k.Struct("inSlope").Float(names[d]), k.Struct("outSlope").Float(names[d]), k.Int("weightedMode"),
                        k.Struct("inWeight").Float(names[d]), k.Struct("outWeight").Float(names[d]));
                }
                result[d] = new AnimationCurve(frames) { preWrapMode = Wrap(curve.Int("m_PreInfinity")), postWrapMode = Wrap(curve.Int("m_PostInfinity")) };
            }
            return result;
        }

        static WrapMode Wrap(int infinity) => infinity switch
        {
            0 => WrapMode.PingPong,
            1 => WrapMode.Loop,
            _ => WrapMode.ClampForever,
        };

        /// The first and last key time over all curves; (0, 0) without keys.
        public (float First, float Last) KeyRange()
        {
            float first = float.PositiveInfinity, last = float.NegativeInfinity;
            foreach (var group in Curves)
                foreach (var c in group)
                    if (c.length > 0)
                    {
                        first = Math.Min(first, c[0].time);
                        last = Math.Max(last, c[c.length - 1].time);
                    }
            return first <= last ? (first, last) : (0f, 0f);
        }
    }
}
