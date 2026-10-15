using System;

namespace UnityEngine.Playables
{
    public partial struct Playable
    {
        private PlayableHandle m_Handle;

        private static readonly Playable m_NullPlayable = new Playable(PlayableHandle.Null);

        public static Playable Null => m_NullPlayable;

        /// A generic playable (no behaviour) with `inputCount` unconnected inputs.
        public static Playable Create(PlayableGraph graph, int inputCount = 0)
        {
            var playable = new Playable(graph.CreatePlayableHandle());
            playable.SetInputCount(inputCount);
            return playable;
        }

        internal Playable(PlayableHandle handle)
        {
            m_Handle = handle;
        }

        public PlayableHandle GetHandle() => m_Handle;

        public bool IsPlayableOfType<T>() where T : struct, IPlayable => m_Handle.IsPlayableOfType<T>();

        public Type GetPlayableType() => m_Handle.GetPlayableType();

        public bool Equals(Playable other) => m_Handle == other.m_Handle;
    }
}
