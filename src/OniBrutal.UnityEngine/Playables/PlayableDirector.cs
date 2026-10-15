using OniBrutal.Engine.Playables;

namespace UnityEngine.Playables
{
    /// Only the director's data is implemented: its exposed-reference table, generic bindings,
    /// asset and wrap mode (OniBrutal.Engine.Playables.DirectorState, filled by the asset binder).
    /// Playback (Play/Pause/Stop/Evaluate, time, state, playableGraph) needs the Timeline director
    /// runtime and is deliberately absent.
    public partial class PlayableDirector
    {
        DirectorState State
        {
            get
            {
                _ = NativeOrThrow;
                return DirectorState.For(this);
            }
        }

        public PlayableAsset playableAsset => State.PlayableAsset;

        public DirectorWrapMode extrapolationMode => State.WrapMode;

        /// The object bound to `key` (a track), or null.
        public Object GetGenericBinding(Object key)
        {
            var state = State;
            if ((object)key == null) return null;
            return state.SceneBindings.TryGetValue(key, out var value) ? value : null;
        }

        public void SetReferenceValue(PropertyName id, Object value) => State.ExposedReferences[id] = value;

        /// The value stored for `id`; idValid is false (and the result null) if none is stored.
        public Object GetReferenceValue(PropertyName id, out bool idValid)
        {
            idValid = State.ExposedReferences.TryGetValue(id, out var value);
            return value;
        }

        public void ClearReferenceValue(PropertyName id) => State.ExposedReferences.Remove(id);
    }
}
