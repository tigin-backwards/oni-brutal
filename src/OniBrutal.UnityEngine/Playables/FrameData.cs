using System;

namespace UnityEngine.Playables
{
    public partial struct FrameData
    {
        [Flags]
        internal enum Flags
        {
            Evaluate = 1,
            SeekOccured = 2,
            Loop = 4,
            Hold = 8,
            EffectivePlayStateDelayed = 16,
            EffectivePlayStatePlaying = 32,
        }

        // Unity's field layout (Timeline reads m_DeltaTime and m_EffectiveSpeed directly).
        internal ulong m_FrameID;
        internal double m_DeltaTime;
        internal float m_Weight;
        internal float m_EffectiveWeight;
        internal double m_EffectiveParentDelay;
        internal float m_EffectiveParentSpeed;
        internal float m_EffectiveSpeed;
        internal Flags m_Flags;
        internal PlayableOutput m_Output;

        public ulong frameId => m_FrameID;

        public float deltaTime => (float)m_DeltaTime;

        public float weight => m_Weight;

        public float effectiveWeight => m_EffectiveWeight;

        public float effectiveParentSpeed => m_EffectiveParentSpeed;

        public float effectiveSpeed => m_EffectiveSpeed;

        public EvaluationType evaluationType => HasFlags(Flags.Evaluate) ? EvaluationType.Evaluate : EvaluationType.Playback;

        public bool seekOccurred => HasFlags(Flags.SeekOccured);

        public bool timeLooped => HasFlags(Flags.Loop);

        public bool timeHeld => HasFlags(Flags.Hold);

        public PlayableOutput output => m_Output;

        public PlayState effectivePlayState
            => HasFlags(Flags.EffectivePlayStateDelayed) ? PlayState.Delayed
             : HasFlags(Flags.EffectivePlayStatePlaying) ? PlayState.Playing
             : PlayState.Paused;

        bool HasFlags(Flags flag) => (m_Flags & flag) == flag;
    }
}
