using System;
using UnityEngine.Playables;

namespace OniMoono.Engine.Playables
{
    /// Unity's ScriptPlayableOutput (an output that only drives script playables). The consumers
    /// never name the type, so it is engine-internal; it is the type
    /// `PlayableOutput.GetPlayableOutputType` reports for outputs made by ScriptPlayableBinding.
    internal struct ScriptPlayableOutput : IPlayableOutput
    {
        private PlayableOutputHandle m_Handle;

        internal ScriptPlayableOutput(PlayableOutputHandle handle)
        {
            if (handle.IsValid() && !handle.IsPlayableOutputOfType<ScriptPlayableOutput>())
                throw new InvalidCastException("Can't set handle: the playable is not a ScriptPlayableOutput.");
            m_Handle = handle;
        }

        public static ScriptPlayableOutput Create(PlayableGraph graph, string name)
            => new ScriptPlayableOutput(PlayableGraphState.Get(graph).CreateOutput(typeof(ScriptPlayableOutput), name).Handle);

        public PlayableOutputHandle GetHandle() => m_Handle;

        public static implicit operator PlayableOutput(ScriptPlayableOutput output) => new PlayableOutput(output.GetHandle());
    }
}
