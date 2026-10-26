using System;
using OniMoono.Engine.Playables;
using UnityEngine.Playables;

namespace UnityEngine.Animations
{
    public partial struct AnimationPlayableOutput
    {
        private PlayableOutputHandle m_Handle;

        public static AnimationPlayableOutput Null => new AnimationPlayableOutput(PlayableOutputHandle.Null);

        public static AnimationPlayableOutput Create(PlayableGraph graph, string name, Animator target)
        {
            var output = new AnimationPlayableOutput(PlayableGraphState.Get(graph).CreateOutput(typeof(AnimationPlayableOutput), name).Handle);
            output.SetTarget(target);
            return output;
        }

        internal AnimationPlayableOutput(PlayableOutputHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOutputOfType<AnimationPlayableOutput>())
                throw new InvalidCastException("Can't set handle: the playable is not an AnimationPlayableOutput.");
            m_Handle = handle;
        }

        public PlayableOutputHandle GetHandle() => m_Handle;

        public static implicit operator PlayableOutput(AnimationPlayableOutput output) => new PlayableOutput(output.GetHandle());

        public static explicit operator AnimationPlayableOutput(PlayableOutput output) => new AnimationPlayableOutput(output.GetHandle());

        public Animator GetTarget() => (Animator)PlayableGraphState.Get(m_Handle).Target;

        public void SetTarget(Animator value) => PlayableGraphState.Get(m_Handle).Target = value;
    }

    public static partial class AnimationPlayableBinding
    {
        /// A binding whose output is an AnimationPlayableOutput targeting an Animator.
        public static PlayableBinding Create(string name, Object key)
            => PlayableBinding.CreateInternal(name, key, typeof(Animator), CreateAnimationOutput);

        private static PlayableOutput CreateAnimationOutput(PlayableGraph graph, string name)
            => AnimationPlayableOutput.Create(graph, name, null);
    }
}
