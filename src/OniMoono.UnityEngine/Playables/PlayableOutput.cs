using System;

namespace UnityEngine.Playables
{
    public partial struct PlayableOutput
    {
        private PlayableOutputHandle m_Handle;

        private static readonly PlayableOutput NullInstance = new PlayableOutput(PlayableOutputHandle.Null);

        public static PlayableOutput Null => NullInstance;

        internal PlayableOutput(PlayableOutputHandle handle)
        {
            m_Handle = handle;
        }

        public PlayableOutputHandle GetHandle() => m_Handle;

        public bool IsPlayableOutputOfType<T>() where T : struct, IPlayableOutput => m_Handle.IsPlayableOutputOfType<T>();

        public Type GetPlayableOutputType() => m_Handle.GetPlayableOutputType();

        public bool Equals(PlayableOutput other) => m_Handle == other.m_Handle;
    }
}
