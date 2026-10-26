using OniMoono.Engine;
using OniMoono.Engine.Mecanim;

namespace UnityEngine
{
    public partial class Motion
    {
        protected Motion() { }

        /// Whether the motion loops: an AnimationClip's loop-time setting.
        public bool isLooping => NativeOrThrow is AnimationClipNative clip && clip.LoopTime;

        public bool legacy => NativeOrThrow is AnimationClipNative clip && clip.Legacy;
    }

    public partial class AnimationClip
    {
        public AnimationClip() => Bind(new AnimationClipNative());

        internal AnimationClipNative Clip => (AnimationClipNative)NativeOrThrow;

        /// Seconds: the muscle clip's stop - start for mecanim clips, the key range of the curves
        /// for legacy clips.
        public float length => Clip.Length;

        public float frameRate
        {
            get => Clip.SampleRate;
            set => Clip.SampleRate = value;
        }

        public WrapMode wrapMode
        {
            get => Clip.WrapMode;
            set => Clip.WrapMode = value;
        }

        public new bool legacy
        {
            get => Clip.Legacy;
            set => Clip.Legacy = value;
        }

        /// No curves and no events.
        public bool empty => Clip.IsEmpty;

        public bool hasGenericRootTransform => Clip.HasGenericRootTransform;

        public bool hasMotionFloatCurves => Clip.HasMotionFloatCurves;

        /// Root motion curves (the MotionT/MotionQ muscles).
        public bool hasMotionCurves => Clip.HasMuscleCurves(0, 7);

        /// Root curves (the RootT/RootQ muscles).
        public bool hasRootCurves => Clip.HasMuscleCurves(7, 14);

        internal bool hasRootMotion => hasMotionCurves || hasRootCurves || hasGenericRootTransform;
    }
}
