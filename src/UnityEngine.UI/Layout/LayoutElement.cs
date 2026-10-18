namespace UnityEngine.UI
{
    /// Overrides the layout sizes of its object (priority 1 by default, above the graphics'
    /// 0); -1 leaves a size unset.
    public partial class LayoutElement
    {
        protected LayoutElement()
        {
            m_MinWidth = -1f;
            m_MinHeight = -1f;
            m_PreferredWidth = -1f;
            m_PreferredHeight = -1f;
            m_FlexibleWidth = -1f;
            m_FlexibleHeight = -1f;
            m_LayoutPriority = 1;
        }

        void Set(ref float field, float value)
        {
            if (field == value) return;
            field = value;
            SetDirty();
        }

        public virtual bool ignoreLayout
        {
            get => m_IgnoreLayout;
            set
            {
                if (m_IgnoreLayout == value) return;
                m_IgnoreLayout = value;
                SetDirty();
            }
        }

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        public virtual float minWidth { get => m_MinWidth; set => Set(ref m_MinWidth, value); }
        public virtual float minHeight { get => m_MinHeight; set => Set(ref m_MinHeight, value); }
        public virtual float preferredWidth { get => m_PreferredWidth; set => Set(ref m_PreferredWidth, value); }
        public virtual float preferredHeight { get => m_PreferredHeight; set => Set(ref m_PreferredHeight, value); }
        public virtual float flexibleWidth { get => m_FlexibleWidth; set => Set(ref m_FlexibleWidth, value); }
        public virtual float flexibleHeight { get => m_FlexibleHeight; set => Set(ref m_FlexibleHeight, value); }

        public virtual int layoutPriority
        {
            get => m_LayoutPriority;
            set
            {
                if (m_LayoutPriority == value) return;
                m_LayoutPriority = value;
                SetDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

        protected override void OnTransformParentChanged() => SetDirty();

        protected override void OnDisable()
        {
            SetDirty();
            base.OnDisable();
        }

        protected override void OnDidApplyAnimationProperties() => SetDirty();

        protected override void OnBeforeTransformParentChanged() => SetDirty();

        protected void SetDirty()
        {
            if (!IsActive()) return;
            LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
        }
    }
}
