using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace OniBrutal.Engine.Playables
{
    /// The native side of a PlayableDirector (its serialized m_PlayableAsset, m_WrapMode,
    /// m_SceneBindings and m_ExposedReferences). Kept in a side table because engine classes
    /// derived from UnityEngine.Object carry no managed reference fields. The asset binder fills it
    /// when a director is loaded.
    internal sealed class DirectorState
    {
        static readonly ConditionalWeakTable<PlayableDirector, DirectorState> states = new ConditionalWeakTable<PlayableDirector, DirectorState>();

        public PlayableAsset PlayableAsset;
        // A new director's serialized default.
        public DirectorWrapMode WrapMode = DirectorWrapMode.Hold;
        public readonly Dictionary<PropertyName, Object> ExposedReferences = new Dictionary<PropertyName, Object>();
        /// Generic bindings keyed by the binding key object (a track asset), compared by identity
        /// like Unity's instance-ID keyed table.
        public readonly Dictionary<Object, Object> SceneBindings = new Dictionary<Object, Object>(ReferenceEqualityComparer.Instance);

        public static DirectorState For(PlayableDirector director) => states.GetValue(director, _ => new DirectorState());
    }
}
