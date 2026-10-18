using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// A source of pointer hits; the event system asks every enabled raycaster.
    public abstract partial class BaseRaycaster
    {
        BaseRaycaster topmost;

        protected BaseRaycaster() { }

        [System.Obsolete("Please use sortOrderPriority and renderOrderPriority", false)]
        public virtual int priority => 0;

        /// Ranks hits of different raycasters before their own order; int.MinValue: no rank.
        public virtual int sortOrderPriority => int.MinValue;

        public virtual int renderOrderPriority => int.MinValue;

        /// The outermost raycaster on this object or its ancestors.
        public BaseRaycaster rootRaycaster
        {
            get
            {
                if (topmost == null)
                {
                    var all = GetComponentsInParent<BaseRaycaster>();
                    if (all.Length != 0) topmost = all[all.Length - 1];
                }
                return topmost;
            }
        }

        public override string ToString()
            => "Name: " + gameObject + "\n" +
               "eventCamera: " + eventCamera + "\n" +
               "sortOrderPriority: " + sortOrderPriority + "\n" +
               "renderOrderPriority: " + renderOrderPriority;

        protected override void OnEnable()
        {
            base.OnEnable();
            RaycasterRegistry.Add(this);
        }

        protected override void OnDisable()
        {
            RaycasterRegistry.Remove(this);
            base.OnDisable();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            topmost = null;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            topmost = null;
        }
    }
}
