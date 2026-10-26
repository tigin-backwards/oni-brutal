using System.Collections.Generic;

namespace UnityEngine.Playables
{
    public partial class PlayableAsset
    {
        /// Infinite unless the asset says otherwise.
        public virtual double duration => PlayableBinding.DefaultDuration;

        public virtual IEnumerable<PlayableBinding> outputs => PlayableBinding.None;
    }

    public partial class PlayableBehaviour
    {
        public PlayableBehaviour() { }

        // Every callback is empty in the base class; the graph calls them on the script instance.
        public virtual void OnGraphStart(Playable playable) { }

        public virtual void OnGraphStop(Playable playable) { }

        public virtual void OnPlayableCreate(Playable playable) { }

        public virtual void OnPlayableDestroy(Playable playable) { }

        [System.Obsolete("OnBehaviourDelay is obsolete; use a custom ScriptPlayable to implement this feature", false)]
        public virtual void OnBehaviourDelay(Playable playable, FrameData info) { }

        public virtual void OnBehaviourPlay(Playable playable, FrameData info) { }

        public virtual void OnBehaviourPause(Playable playable, FrameData info) { }

        public virtual void PrepareData(Playable playable, FrameData info) { }

        public virtual void PrepareFrame(Playable playable, FrameData info) { }

        public virtual void ProcessFrame(Playable playable, FrameData info, object playerData) { }

        /// A shallow copy; ScriptPlayable<T>.Create(graph, template) clones templates through this.
        public virtual object Clone() => MemberwiseClone();
    }
}
