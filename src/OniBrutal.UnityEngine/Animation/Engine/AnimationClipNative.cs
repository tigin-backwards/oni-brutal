using System;
using System.IO;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniBrutal.Engine.Mecanim
{
    /// Engine side of an AnimationClip. Player builds store mecanim clips as m_MuscleClip
    /// (ClipCurves + the GenericBinding table); legacy clips keep editor curves, which only the
    /// legacy Animation component plays (here they contribute their key range and curve count).
    internal sealed class AnimationClipNative : NativeObject
    {
        public float SampleRate = 60f;
        public WrapMode WrapMode;
        public bool Legacy;
        public float StartTime, StopTime;
        public bool LoopTime;
        public float CycleOffset;
        public bool HasGenericRootTransform, HasMotionFloatCurves;
        public ClipCurves Curves = ClipCurves.Empty;
        /// Legacy editor curves: count and key range.
        public int LegacyCurveCount;
        public float LegacyFirstKey, LegacyLastKey;

        public float Length => Legacy ? (LegacyCurveCount == 0 ? 0f : LegacyLastKey - LegacyFirstKey) : StopTime - StartTime;

        public bool IsEmpty => Curves.CurveCount == 0 && LegacyCurveCount == 0;

        /// Whether the clip has animator-muscle curves with an index in [first, end).
        public bool HasMuscleCurves(uint first, uint end)
        {
            foreach (var b in Curves.Bindings)
                if (b.CustomType == ClipBinding.AnimatorMuscle && b.Attribute >= first && b.Attribute < end) return true;
            return false;
        }

        /// Clip time for a state's normalized time: looping clips wrap, others clamp.
        public float ClipTime(float normalizedTime, bool loop)
        {
            float n = loop ? normalizedTime - MathF.Floor(normalizedTime) : Math.Clamp(normalizedTime, 0f, 1f);
            return StartTime + n * (StopTime - StartTime);
        }
    }

    /// AnimationClip (class 74). MonoBehaviour bindings name their script by PPtr to a MonoScript,
    /// which the script table resolves against the clip's file.
    internal sealed class AnimationClipBinder : Binder
    {
        public override Object Create(LoadRequest request)
        {
            var f = TypeTreeReader.ReadNative(request.ClassId, request.Open());
            var native = new AnimationClipNative
            {
                Name = f.String("m_Name"),
                Legacy = f.Bool("m_Legacy"),
                SampleRate = f.Float("m_SampleRate"),
                WrapMode = (WrapMode)f.Int("m_WrapMode"),
                HasGenericRootTransform = f.Bool("m_HasGenericRootTransform"),
                HasMotionFloatCurves = f.Bool("m_HasMotionFloatCurves"),
            };
            if (f.Array("m_Events").Length > 0 && !native.Legacy)
                throw new InvalidDataException($"AnimationClip {native.Name}: animation events are not implemented.");

            var muscle = f.Struct("m_MuscleClip");
            native.StartTime = muscle.Float("m_StartTime");
            native.StopTime = muscle.Float("m_StopTime");
            native.LoopTime = muscle.Bool("m_LoopTime");
            native.CycleOffset = muscle.Float("m_CycleOffset");

            var bindingFields = f.Struct("m_ClipBindingConstant").Array("genericBindings");
            var bindings = new ClipBinding[bindingFields.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = (Fields)bindingFields[i];
                bindings[i] = new ClipBinding
                {
                    Path = Convert.ToUInt32(b["path"]),
                    Attribute = Convert.ToUInt32(b["attribute"]),
                    ClassId = b.Int("typeID"),
                    CustomType = Convert.ToByte(b["customType"]),
                    IsPPtrCurve = Convert.ToInt32(b["isPPtrCurve"]) != 0,
                    IsIntCurve = Convert.ToInt32(b["isIntCurve"]) != 0,
                    Script = ScriptTable.Resolve(request.File, b.PPtr("script")),
                };
            }
            native.Curves = ClipCurves.Read(muscle.Struct("m_Clip").Struct("data"), bindings);

            if (native.Legacy) ReadLegacyRange(f, native);
            return ObjectLoader.CreateNative<AnimationClip>(typeof(AnimationClip), native);
        }

        public override void Fill(Object obj, LoadRequest request) { }

        static void ReadLegacyRange(Fields f, AnimationClipNative native)
        {
            if (f.Array("m_CompressedRotationCurves").Length > 0)
                throw new InvalidDataException($"AnimationClip {native.Name}: compressed legacy rotation curves are not implemented.");
            float first = float.PositiveInfinity, last = float.NegativeInfinity;
            int count = 0;
            void Key(float time)
            {
                first = Math.Min(first, time);
                last = Math.Max(last, time);
            }
            foreach (var name in new[] { "m_RotationCurves", "m_EulerCurves", "m_PositionCurves", "m_ScaleCurves", "m_FloatCurves" })
                foreach (Fields c in f.Array(name))
                {
                    count++;
                    foreach (Fields key in c.Struct("curve").Array("m_Curve")) Key(key.Float("time"));
                }
            foreach (Fields c in f.Array("m_PPtrCurves"))
            {
                count++;
                foreach (Fields key in c.Array("curve")) Key(key.Float("time"));
            }
            native.LegacyCurveCount = count;
            if (first <= last)
            {
                native.LegacyFirstKey = first;
                native.LegacyLastKey = last;
            }
        }
    }
}
