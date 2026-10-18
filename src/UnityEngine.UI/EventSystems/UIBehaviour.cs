namespace UnityEngine.EventSystems
{
    /// The base of every uGUI component. The engine sends the messages; subclasses override them
    /// and call the base implementation.
    public abstract partial class UIBehaviour
    {
        protected UIBehaviour() { }

        protected virtual void Awake() { }
        protected virtual void OnEnable() { }
        protected virtual void Start() { }
        protected virtual void OnDisable() { }
        protected virtual void OnDestroy() { }

        public virtual bool IsActive() => isActiveAndEnabled;

        // Editor-only messages: a player never sends them.
        protected virtual void OnValidate() { }
        protected virtual void Reset() { }

        protected virtual void OnRectTransformDimensionsChange() { }
        protected virtual void OnBeforeTransformParentChanged() { }
        protected virtual void OnTransformParentChanged() { }
        protected virtual void OnDidApplyAnimationProperties() { }
        protected virtual void OnCanvasGroupChanged() { }
        protected virtual void OnCanvasHierarchyChanged() { }

        /// True once the native object is gone (destroyed), while the managed object lives on.
        public bool IsDestroyed() => this == null;
    }
}
