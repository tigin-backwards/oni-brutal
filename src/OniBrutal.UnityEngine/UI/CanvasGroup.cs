using OniBrutal.Engine;

namespace UnityEngine
{
    public partial class CanvasGroup
    {
        public CanvasGroup() { }

        internal CanvasGroupNative Group => (CanvasGroupNative)NativeOrThrow;

        public float alpha
        {
            get => Group.Alpha;
            set
            {
                var g = Group;
                if (g.Alpha == value) return;
                g.Alpha = value;
                g.Changed();
            }
        }

        public bool interactable
        {
            get => Group.Interactable;
            set
            {
                var g = Group;
                if (g.Interactable == value) return;
                g.Interactable = value;
                g.Changed();
            }
        }

        public bool blocksRaycasts
        {
            get => Group.BlocksRaycasts;
            set
            {
                var g = Group;
                if (g.BlocksRaycasts == value) return;
                g.BlocksRaycasts = value;
                g.Changed();
            }
        }

        public bool ignoreParentGroups
        {
            get => Group.IgnoreParentGroups;
            set
            {
                var g = Group;
                if (g.IgnoreParentGroups == value) return;
                g.IgnoreParentGroups = value;
                g.Changed();
            }
        }

        /// A group lets raycasts through to its graphics only when it blocks raycasts.
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera) => blocksRaycasts;

        internal override void EngineOnEnable() => Group.Changed();
        internal override void EngineOnDisable() => Group.Changed();
    }
}

namespace OniBrutal.Engine
{
    internal sealed class CanvasGroupNative : BehaviourNative
    {
        public float Alpha = 1f;
        public bool Interactable = true;
        public bool BlocksRaycasts = true;
        public bool IgnoreParentGroups;

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            var g = (CanvasGroupNative)source;
            Alpha = g.Alpha;
            Interactable = g.Interactable;
            BlocksRaycasts = g.BlocksRaycasts;
            IgnoreParentGroups = g.IgnoreParentGroups;
        }

        /// Unity tells the scripts on this object and its descendants (uGUI's Selectables and
        /// Graphics listen) that the effective group state changed.
        public void Changed() => Messages.SendToSubtree(GameObject, s => s.OnCanvasGroupChanged);
    }
}
