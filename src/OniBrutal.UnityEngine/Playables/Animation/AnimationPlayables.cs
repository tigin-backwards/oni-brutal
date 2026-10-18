using System;
using OniBrutal.Engine.Playables;
using UnityEngine.Playables;

namespace UnityEngine.Animations
{
    // Typed wrappers over playable handles. Each constructor rejects a valid handle of another
    // playable kind with InvalidCastException, as Unity does on explicit conversion.

    public partial struct AnimationClipPlayable
    {
        private PlayableHandle m_Handle;

        public static AnimationClipPlayable Create(PlayableGraph graph, AnimationClip clip)
            => new AnimationClipPlayable(PlayableGraphState.Get(graph)
                .CreatePlayable(typeof(AnimationClipPlayable), new AnimationClipPayload { Clip = clip }).Handle);

        internal AnimationClipPlayable(PlayableHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOfType<AnimationClipPlayable>())
                throw new InvalidCastException("Can't set handle: the playable is not an AnimationClipPlayable.");
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public static implicit operator Playable(AnimationClipPlayable playable) => new Playable(playable.GetHandle());

        public static explicit operator AnimationClipPlayable(Playable playable) => new AnimationClipPlayable(playable.GetHandle());

        public bool Equals(AnimationClipPlayable other) => m_Handle == other.m_Handle;

        AnimationClipPayload Payload => (AnimationClipPayload)PlayableGraphState.Get(m_Handle).Payload;

        public AnimationClip GetAnimationClip() => Payload.Clip;

        public void SetApplyFootIK(bool value) => Payload.ApplyFootIK = value;

        public void SetApplyPlayableIK(bool value) => Payload.ApplyPlayableIK = value;

        internal void SetRemoveStartOffset(bool value) => Payload.RemoveStartOffset = value;

        internal void SetOverrideLoopTime(bool value) => Payload.OverrideLoopTime = value;

        internal void SetLoopTime(bool value) => Payload.LoopTime = value;
    }

    public partial struct AnimationMixerPlayable
    {
        private PlayableHandle m_Handle;

        private static readonly AnimationMixerPlayable NullInstance = new AnimationMixerPlayable(PlayableHandle.Null);

        public static AnimationMixerPlayable Null => NullInstance;

        public static AnimationMixerPlayable Create(PlayableGraph graph, int inputCount = 0)
        {
            var handle = PlayableGraphState.Get(graph).CreatePlayable(typeof(AnimationMixerPlayable)).Handle;
            handle.SetInputCount(inputCount);
            return new AnimationMixerPlayable(handle);
        }

        internal AnimationMixerPlayable(PlayableHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOfType<AnimationMixerPlayable>())
                throw new InvalidCastException("Can't set handle: the playable is not an AnimationMixerPlayable.");
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public static implicit operator Playable(AnimationMixerPlayable playable) => new Playable(playable.GetHandle());

        public static explicit operator AnimationMixerPlayable(Playable playable) => new AnimationMixerPlayable(playable.GetHandle());

        public bool Equals(AnimationMixerPlayable other) => m_Handle == other.m_Handle;
    }

    public partial struct AnimationLayerMixerPlayable
    {
        private PlayableHandle m_Handle;

        private static readonly AnimationLayerMixerPlayable NullInstance = new AnimationLayerMixerPlayable(PlayableHandle.Null);

        public static AnimationLayerMixerPlayable Null => NullInstance;

        public static AnimationLayerMixerPlayable Create(PlayableGraph graph, int inputCount = 0)
            => Create(graph, inputCount, singleLayerOptimization: true);

        public static AnimationLayerMixerPlayable Create(PlayableGraph graph, int inputCount, bool singleLayerOptimization)
        {
            var handle = PlayableGraphState.Get(graph)
                .CreatePlayable(typeof(AnimationLayerMixerPlayable), new AnimationLayerMixerPayload()).Handle;
            handle.SetInputCount(inputCount);
            return new AnimationLayerMixerPlayable(handle, singleLayerOptimization);
        }

        internal AnimationLayerMixerPlayable(PlayableHandle handle, bool singleLayerOptimization = true)
        {
            if (handle.IsValid())
            {
                if (!handle.IsPlayableOfType<AnimationLayerMixerPlayable>())
                    throw new InvalidCastException("Can't set handle: the playable is not an AnimationLayerMixerPlayable.");
                ((AnimationLayerMixerPayload)PlayableGraphState.Get(handle).Payload).SingleLayerOptimization = singleLayerOptimization;
            }
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public static implicit operator Playable(AnimationLayerMixerPlayable playable) => new Playable(playable.GetHandle());

        public static explicit operator AnimationLayerMixerPlayable(Playable playable) => new AnimationLayerMixerPlayable(playable.GetHandle());

        public bool Equals(AnimationLayerMixerPlayable other) => m_Handle == other.m_Handle;

        /// Restricts layer `layerIndex` to the body parts / transforms enabled in `mask`.
        public void SetLayerMaskFromAvatarMask(uint layerIndex, AvatarMask mask)
        {
            int count = m_Handle.GetInputCount();
            if (layerIndex >= count)
                throw new ArgumentOutOfRangeException(nameof(layerIndex), $"layerIndex {layerIndex} must be in the range of 0 to {count - 1}.");
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            ((AnimationLayerMixerPayload)PlayableGraphState.Get(m_Handle).Payload).LayerMasks[layerIndex] = mask;
        }
    }

    partial struct AnimationMotionXToDeltaPlayable
    {
        private PlayableHandle m_Handle;

        private static readonly AnimationMotionXToDeltaPlayable NullInstance = new AnimationMotionXToDeltaPlayable(PlayableHandle.Null);

        public static AnimationMotionXToDeltaPlayable Null => NullInstance;

        /// A one-input playable converting its input's root motion to deltas.
        public static AnimationMotionXToDeltaPlayable Create(PlayableGraph graph)
        {
            var handle = PlayableGraphState.Get(graph)
                .CreatePlayable(typeof(AnimationMotionXToDeltaPlayable), new AnimationMotionXToDeltaPayload()).Handle;
            handle.SetInputCount(1);
            return new AnimationMotionXToDeltaPlayable(handle);
        }

        private AnimationMotionXToDeltaPlayable(PlayableHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOfType<AnimationMotionXToDeltaPlayable>())
                throw new InvalidCastException("Can't set handle: the playable is not an AnimationMotionXToDeltaPlayable.");
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public static implicit operator Playable(AnimationMotionXToDeltaPlayable playable) => new Playable(playable.GetHandle());

        public static explicit operator AnimationMotionXToDeltaPlayable(Playable playable) => new AnimationMotionXToDeltaPlayable(playable.GetHandle());

        public bool Equals(AnimationMotionXToDeltaPlayable other) => m_Handle == other.m_Handle;

        public void SetAbsoluteMotion(bool value)
            => ((AnimationMotionXToDeltaPayload)PlayableGraphState.Get(m_Handle).Payload).AbsoluteMotion = value;
    }

    partial struct AnimationOffsetPlayable
    {
        private PlayableHandle m_Handle;

        private static readonly AnimationOffsetPlayable NullInstance = new AnimationOffsetPlayable(PlayableHandle.Null);

        public static AnimationOffsetPlayable Null => NullInstance;

        public static AnimationOffsetPlayable Create(PlayableGraph graph, Vector3 position, Quaternion rotation, int inputCount)
        {
            var handle = PlayableGraphState.Get(graph).CreatePlayable(typeof(AnimationOffsetPlayable),
                new AnimationOffsetPayload { Position = position, Rotation = rotation }).Handle;
            handle.SetInputCount(inputCount);
            return new AnimationOffsetPlayable(handle);
        }

        internal AnimationOffsetPlayable(PlayableHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOfType<AnimationOffsetPlayable>())
                throw new InvalidCastException("Can't set handle: the playable is not an AnimationOffsetPlayable.");
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public static implicit operator Playable(AnimationOffsetPlayable playable) => new Playable(playable.GetHandle());

        public static explicit operator AnimationOffsetPlayable(Playable playable) => new AnimationOffsetPlayable(playable.GetHandle());

        public bool Equals(AnimationOffsetPlayable other) => m_Handle == other.m_Handle;

        public Vector3 GetPosition() => ((AnimationOffsetPayload)PlayableGraphState.Get(m_Handle).Payload).Position;

        public void SetPosition(Vector3 value) => ((AnimationOffsetPayload)PlayableGraphState.Get(m_Handle).Payload).Position = value;

        public Quaternion GetRotation() => ((AnimationOffsetPayload)PlayableGraphState.Get(m_Handle).Payload).Rotation;

        public void SetRotation(Quaternion value) => ((AnimationOffsetPayload)PlayableGraphState.Get(m_Handle).Payload).Rotation = value;
    }

    partial struct AnimationRemoveScalePlayable
    {
        private PlayableHandle m_Handle;

        private static readonly AnimationRemoveScalePlayable NullInstance = new AnimationRemoveScalePlayable(PlayableHandle.Null);

        public static AnimationRemoveScalePlayable Null => NullInstance;

        public static AnimationRemoveScalePlayable Create(PlayableGraph graph, int inputCount)
        {
            var handle = PlayableGraphState.Get(graph).CreatePlayable(typeof(AnimationRemoveScalePlayable)).Handle;
            handle.SetInputCount(inputCount);
            return new AnimationRemoveScalePlayable(handle);
        }

        internal AnimationRemoveScalePlayable(PlayableHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOfType<AnimationRemoveScalePlayable>())
                throw new InvalidCastException("Can't set handle: the playable is not an AnimationRemoveScalePlayable.");
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public static implicit operator Playable(AnimationRemoveScalePlayable playable) => new Playable(playable.GetHandle());

        public static explicit operator AnimationRemoveScalePlayable(Playable playable) => new AnimationRemoveScalePlayable(playable.GetHandle());

        public bool Equals(AnimationRemoveScalePlayable other) => m_Handle == other.m_Handle;
    }

    public static partial class AnimationPlayableExtensions
    {
        /// Declares which properties the playable's subgraph animates (the curves of `clip`).
        public static void SetAnimatedProperties<U>(this U playable, AnimationClip clip) where U : struct, IPlayable
            => PlayableGraphState.Get(playable.GetHandle()).AnimatedProperties = clip;
    }
}
