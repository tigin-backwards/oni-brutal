using System;
using OniBrutal.Engine.Playables;

namespace UnityEngine.Playables
{
    /// Describes one output a PlayableAsset needs: a stream name, the key object it is bound by,
    /// the target type, and how to create the PlayableOutput.
    public partial struct PlayableBinding
    {
        internal delegate PlayableOutput CreateOutputMethod(PlayableGraph graph, string name);

        private string m_StreamName;
        private Object m_SourceObject;
        private Type m_SourceBindingType;
        private CreateOutputMethod m_CreateOutputMethod;

        public static readonly PlayableBinding[] None = new PlayableBinding[0];

        public static readonly double DefaultDuration = double.PositiveInfinity;

        public string streamName
        {
            get => m_StreamName;
            set => m_StreamName = value;
        }

        public Object sourceObject
        {
            get => m_SourceObject;
            set => m_SourceObject = value;
        }

        public Type outputTargetType => m_SourceBindingType;

        /// Creates this binding's output in `graph`, or PlayableOutput.Null for a default binding.
        internal PlayableOutput CreateOutput(PlayableGraph graph)
            => m_CreateOutputMethod != null ? m_CreateOutputMethod(graph, m_StreamName) : PlayableOutput.Null;

        internal static PlayableBinding CreateInternal(string name, Object sourceObject, Type sourceType, CreateOutputMethod createFunction)
            => new PlayableBinding
            {
                m_StreamName = name,
                m_SourceObject = sourceObject,
                m_SourceBindingType = sourceType,
                m_CreateOutputMethod = createFunction,
            };
    }

    public static partial class ScriptPlayableBinding
    {
        /// A binding whose output is a script output (drives script playables only).
        public static PlayableBinding Create(string name, Object key, Type type)
            => PlayableBinding.CreateInternal(name, key, type, CreateScriptOutput);

        private static PlayableOutput CreateScriptOutput(PlayableGraph graph, string name)
            => ScriptPlayableOutput.Create(graph, name);
    }
}
