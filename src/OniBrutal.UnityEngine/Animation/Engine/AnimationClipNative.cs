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
    /// legacy Animation component plays.
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
        /// Legacy editor curves and their key range.
        public LegacyCurves LegacyCurves = LegacyCurves.Empty;
        public float LegacyFirstKey, LegacyLastKey;

        public float Length => Legacy ? LegacyLastKey - LegacyFirstKey : StopTime - StartTime;

        public bool IsEmpty => Curves.CurveCount == 0 && LegacyCurves.Count == 0;

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
            var f = request.ReadNative();
            var native = new AnimationClipNative
            {
                Name = f.String("m_Name"),
                Legacy = f.Bool("m_Legacy"),
                SampleRate = f.Float("m_SampleRate"),
                WrapMode = (WrapMode)f.Int("m_WrapMode"),
                HasGenericRootTransform = f.Bool("m_HasGenericRootTransform"),
                HasMotionFloatCurves = f.Bool("m_HasMotionFloatCurves"),
            };
            if (f.Array("m_Events").Length > 0)
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

            if (native.Legacy)
            {
                native.LegacyCurves = LegacyCurves.Read(f, native.Name, request.File);
                (native.LegacyFirstKey, native.LegacyLastKey) = native.LegacyCurves.KeyRange();
            }
            return ObjectLoader.CreateNative<AnimationClip>(typeof(AnimationClip), native);
        }

        public override void Fill(Object obj, LoadRequest request) { }
    }
}
