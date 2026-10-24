using System;
using UnityEngine.Rendering;

namespace UnityEngine.UI
{
    /// A graphic that stencil masks (Mask) and rect masks (RectMask2D) above it act on.
    public abstract partial class MaskableGraphic
    {
        [NonSerialized] protected bool m_ShouldRecalculateStencil = true;
        [NonSerialized] protected Material m_MaskMaterial;
        [NonSerialized] protected int m_StencilValue;

        RectMask2D clipper;
        bool masking;
        /// The size of its rect when its RectMask2D last culled it: a new size culls it again.
        [NonSerialized] internal Vector2 CulledSize;
        readonly Vector3[] corners = new Vector3[4];

        protected MaskableGraphic()
        {
            m_Maskable = true;
            m_OnCullStateChanged = new CullStateChangedEvent();
        }

        public CullStateChangedEvent onCullStateChanged
        {
            get => m_OnCullStateChanged;
            set => m_OnCullStateChanged = value;
        }

        public bool maskable
        {
            get => m_Maskable;
            set
            {
                if (value == m_Maskable) return;
                m_Maskable = value;
                m_ShouldRecalculateStencil = true;
                SetMaterialDirty();
            }
        }

        /// Set by a Mask on the same object: this graphic draws the mask's shape.
        public bool isMaskingGraphic
        {
            get => masking;
            set => masking = value;
        }

        /// Wraps the material in the stencil test of the masks above (their count is the stencil
        /// value); the masking graphic gets its stencil from its Mask instead.
        public virtual Material GetModifiedMaterial(Material baseMaterial)
        {
            var result = baseMaterial;
            if (m_ShouldRecalculateStencil)
            {
                if (maskable)
                {
                    var root = MaskUtilities.FindRootSortOverrideCanvas(transform);
                    m_StencilValue = MaskUtilities.GetStencilDepth(transform, root);
                }
                else m_StencilValue = 0;
                m_ShouldRecalculateStencil = false;
            }
            if (m_StencilValue > 0 && !isMaskingGraphic)
            {
                int bits = (1 << m_StencilValue) - 1;
                var stencil = StencilMaterial.Add(result, bits, StencilOp.Keep, CompareFunction.Equal, ColorWriteMask.All, bits, 0);
                StencilMaterial.Remove(m_MaskMaterial);
                m_MaskMaterial = stencil;
                result = m_MaskMaterial;
            }
            return result;
        }

        /// Culled when its rect (in root canvas space) misses the clip rect, or the clip is invalid.
        public virtual void Cull(Rect clipRect, bool validRect)
        {
            bool cull = !validRect || !clipRect.Overlaps(RootCanvasRect(), true);
            UpdateCull(cull);
        }

        /// The cull its RectMask2D's last pass left: a cull changed since then (a direct Cull
        /// call) makes the next pass cull it again.
        [NonSerialized] internal bool PassCull;

        void UpdateCull(bool cull)
        {
            if (canvasRenderer.cull == cull) return;
            canvasRenderer.cull = cull;
            onCullStateChanged.Invoke(cull);
            OnCullingChanged();
        }

        public virtual void SetClipRect(Rect clipRect, bool validRect)
        {
            if (validRect) canvasRenderer.EnableRectClipping(clipRect);
            else canvasRenderer.DisableRectClipping();
        }

        public virtual void SetClipSoftness(Vector2 clipSoftness) => canvasRenderer.clippingSoftness = clipSoftness;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_ShouldRecalculateStencil = true;
            UpdateClipper();
            SetMaterialDirty();
            if (isMaskingGraphic) MaskUtilities.NotifyStencilStateChanged(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_ShouldRecalculateStencil = true;
            SetMaterialDirty();
            UpdateClipper();
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;
            if (isMaskingGraphic) MaskUtilities.NotifyStencilStateChanged(this);
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (!isActiveAndEnabled) return;
            m_ShouldRecalculateStencil = true;
            UpdateClipper();
            SetMaterialDirty();
        }

        [Obsolete("Not used anymore.", true)]
        public virtual void ParentMaskStateChanged() { }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            if (!isActiveAndEnabled) return;
            m_ShouldRecalculateStencil = true;
            UpdateClipper();
            SetMaterialDirty();
        }

        /// The graphic's rect in its root canvas's space.
        Rect RootCanvasRect()
        {
            rectTransform.GetWorldCorners(corners);
            var c = canvas;
            if (c != null)
            {
                var toRoot = c.rootCanvas.transform.worldToLocalMatrix;
                for (int i = 0; i < 4; i++) corners[i] = toRoot.MultiplyPoint(corners[i]);
            }
            // The corners of a rotated or mirrored element: the bounding rect.
            float xMin = corners[0].x, xMax = corners[0].x, yMin = corners[0].y, yMax = corners[0].y;
            for (int i = 1; i < 4; i++)
            {
                xMin = Mathf.Min(xMin, corners[i].x);
                xMax = Mathf.Max(xMax, corners[i].x);
                yMin = Mathf.Min(yMin, corners[i].y);
                yMax = Mathf.Max(yMax, corners[i].y);
            }
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// Joins the nearest RectMask2D above (when maskable and active), leaving the previous one.
        void UpdateClipper()
        {
            var next = maskable && IsActive() ? MaskUtilities.GetRectMaskForClippable(this) : null;
            if (clipper != null && (next != clipper || !next.IsActive()))
            {
                clipper.RemoveClippable(this);
                UpdateCull(false);
            }
            if (next != null && next.IsActive()) next.AddClippable(this);
            clipper = next;
        }

        public virtual void RecalculateClipping() => UpdateClipper();

        public virtual void RecalculateMasking()
        {
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;
            m_ShouldRecalculateStencil = true;
            SetMaterialDirty();
        }
    }
}
