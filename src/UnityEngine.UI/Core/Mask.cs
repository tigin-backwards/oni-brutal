using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace UnityEngine.UI
{
    /// A stencil mask: the object's graphic writes its shape into the stencil buffer (drawn or
    /// not), maskable graphics below test it, and the graphic's pop material undoes the write
    /// after its children.
    public partial class Mask
    {
        [NonSerialized] RectTransform cachedRect;
        [NonSerialized] Graphic cachedGraphic;
        [NonSerialized] Material maskMaterial;
        [NonSerialized] Material unmaskMaterial;

        protected Mask()
        {
            m_ShowMaskGraphic = true;
        }

        public RectTransform rectTransform => cachedRect ??= GetComponent<RectTransform>();

        public bool showMaskGraphic
        {
            get => m_ShowMaskGraphic;
            set
            {
                if (m_ShowMaskGraphic == value) return;
                m_ShowMaskGraphic = value;
                if (graphic != null) graphic.SetMaterialDirty();
            }
        }

        public Graphic graphic => cachedGraphic ??= GetComponent<Graphic>();

        public virtual bool MaskEnabled() => IsActive() && graphic != null;

        [Obsolete("Not used anymore.")]
        public virtual void OnSiblingGraphicEnabledDisabled() { }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (graphic != null)
            {
                graphic.canvasRenderer.hasPopInstruction = true;
                graphic.SetMaterialDirty();
                if (graphic is MaskableGraphic m) m.isMaskingGraphic = true;
            }
            MaskUtilities.NotifyStencilStateChanged(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
                graphic.canvasRenderer.hasPopInstruction = false;
                graphic.canvasRenderer.popMaterialCount = 0;
                if (graphic is MaskableGraphic m) m.isMaskingGraphic = false;
            }
            StencilMaterial.Remove(maskMaterial);
            maskMaterial = null;
            StencilMaterial.Remove(unmaskMaterial);
            unmaskMaterial = null;
            MaskUtilities.NotifyStencilStateChanged(this);
        }

        public virtual bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled) return true;
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, sp, eventCamera);
        }

        /// The mask's own stencil write. The outermost mask (bit 0) replaces the stencil
        /// unconditionally and zeroes it afterwards; a nested mask at depth d sets bit d where the
        /// bits of the masks above are set, and clears it again afterwards. At most 8 levels.
        public virtual Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!MaskEnabled()) return baseMaterial;
            var root = MaskUtilities.FindRootSortOverrideCanvas(transform);
            int depth = MaskUtilities.GetStencilDepth(transform, root);
            if (depth >= 8)
            {
                Debug.LogWarning("Attempting to use a stencil mask with depth > 8", gameObject);
                return baseMaterial;
            }
            int bit = 1 << depth;
            var colors = m_ShowMaskGraphic ? ColorWriteMask.All : 0;
            Material write, unwrite;
            if (bit == 1)
            {
                write = StencilMaterial.Add(baseMaterial, 1, StencilOp.Replace, CompareFunction.Always, colors);
                unwrite = StencilMaterial.Add(baseMaterial, 1, StencilOp.Zero, CompareFunction.Always, 0);
            }
            else
            {
                int above = bit - 1;
                write = StencilMaterial.Add(baseMaterial, bit | above, StencilOp.Replace, CompareFunction.Equal, colors, above, bit | above);
                unwrite = StencilMaterial.Add(baseMaterial, above, StencilOp.Replace, CompareFunction.Equal, 0, above, bit | above);
            }
            StencilMaterial.Remove(maskMaterial);
            maskMaterial = write;
            StencilMaterial.Remove(unmaskMaterial);
            unmaskMaterial = unwrite;
            graphic.canvasRenderer.popMaterialCount = 1;
            graphic.canvasRenderer.SetPopMaterial(unmaskMaterial, 0);
            return maskMaterial;
        }
    }

    /// Clips the maskable graphics below it to its rect (minus padding) without the stencil:
    /// the canvas renderer clips to a rect, and elements wholly outside are culled.
    public partial class RectMask2D
    {
        [NonSerialized] readonly HashSet<IClippable> clipTargets = new(ReferenceEqualityComparer.Instance);
        [NonSerialized] readonly HashSet<MaskableGraphic> maskableTargets = new(ReferenceEqualityComparer.Instance);
        [NonSerialized] readonly List<RectMask2D> clippers = new();
        [NonSerialized] bool clippersStale = true;
        [NonSerialized] bool forceClip;
        [NonSerialized] Rect lastClip;
        [NonSerialized] bool hasLastClip;
        [NonSerialized] Vector2Int lastSoftness;
        [NonSerialized] RectTransform cachedRect;
        [NonSerialized] Canvas cachedRoot;
        readonly Vector3[] corners = new Vector3[4];

        protected RectMask2D() { }

        /// Insets of the clip rect: left, bottom, right, top.
        public Vector4 padding
        {
            get => m_Padding;
            set
            {
                m_Padding = value;
                MaskUtilities.Notify2DMaskStateChanged(this);
            }
        }

        public Vector2Int softness
        {
            get => m_Softness;
            set
            {
                m_Softness.x = Mathf.Max(0, value.x);
                m_Softness.y = Mathf.Max(0, value.y);
                MaskUtilities.Notify2DMaskStateChanged(this);
            }
        }

        public RectTransform rectTransform => cachedRect ??= GetComponent<RectTransform>();

        /// The topmost canvas above the mask: clip rects are in its space.
        Canvas RootCanvas
        {
            get
            {
                if (cachedRoot == null)
                {
                    var all = GetComponentsInParent<Canvas>(false);
                    cachedRoot = all.Length > 0 ? all[all.Length - 1] : null;
                }
                return cachedRoot;
            }
        }

        /// The mask's rect in its root canvas's space.
        public Rect canvasRect => RectIn(rectTransform, RootCanvas);

        Rect RectIn(RectTransform rt, Canvas c)
        {
            rt.GetWorldCorners(corners);
            if (c != null)
            {
                var t = c.transform;
                for (int i = 0; i < 4; i++) corners[i] = t.InverseTransformPoint(corners[i]);
            }
            return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            clippersStale = true;
            ClipperRegistry.Register(this);
            MaskUtilities.Notify2DMaskStateChanged(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            clipTargets.Clear();
            maskableTargets.Clear();
            clippers.Clear();
            ClipperRegistry.Disable(this);
            MaskUtilities.Notify2DMaskStateChanged(this);
        }

        protected override void OnDestroy()
        {
            ClipperRegistry.Unregister(this);
            base.OnDestroy();
        }

        public virtual bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out var local)) return false;
            var r = rectTransform.rect;
            return local.x >= r.xMin + m_Padding.x && local.x <= r.xMax - m_Padding.z && local.y >= r.yMin + m_Padding.y && local.y <= r.yMax - m_Padding.w;
        }

        /// The clip of every enclosing RectMask2D, intersected, goes to the clippables; a mask
        /// wholly off its screen-space canvas clips everything. Clippables that moved are culled
        /// again even when the clip is unchanged.
        public virtual void PerformClipping()
        {
            if (ReferenceEquals(RootCanvas, null)) return;
            if (clippersStale)
            {
                MaskUtilities.GetRectMasksForClip(this, clippers);
                clippersStale = false;
            }
            var clip = Clipping.FindCullAndClipWorldRect(clippers, out bool valid);
            var root = RootCanvas;
            if (root != null)
            {
                var mode = root.rootCanvas.renderMode;
                if ((mode == RenderMode.ScreenSpaceCamera || mode == RenderMode.ScreenSpaceOverlay)
                    && !clip.Overlaps(RectIn((RectTransform)root.rootCanvas.transform, root), true))
                {
                    clip = Rect.zero;
                    valid = false;
                }
            }

            if (!hasLastClip || clip != lastClip || forceClip)
            {
                foreach (var c in clipTargets) c.SetClipRect(clip, valid);
                foreach (var m in maskableTargets)
                {
                    m.SetClipRect(clip, valid);
                    m.Cull(clip, valid);
                }
            }
            else
                foreach (var m in maskableTargets)
                    if (m.canvasRenderer.hasMoved) m.Cull(clip, valid);

            lastClip = clip;
            hasLastClip = true;
            forceClip = false;

            if (lastSoftness != m_Softness || forceSoftness)
            {
                foreach (var c in clipTargets) c.SetClipSoftness(m_Softness);
                foreach (var m in maskableTargets) m.SetClipSoftness(m_Softness);
                lastSoftness = m_Softness;
                forceSoftness = false;
            }
        }

        [NonSerialized] bool forceSoftness;

        public void AddClippable(IClippable clippable)
        {
            if (clippable == null) return;
            clippersStale = true;
            if (clippable is MaskableGraphic m) maskableTargets.Add(m);
            else clipTargets.Add(clippable);
            forceClip = true;
            forceSoftness = true;
        }

        public void RemoveClippable(IClippable clippable)
        {
            if (clippable == null) return;
            clippersStale = true;
            clippable.SetClipRect(new Rect(), false);
            if (clippable is MaskableGraphic m) maskableTargets.Remove(m);
            else clipTargets.Remove(clippable);
            forceClip = true;
        }

        protected override void OnTransformParentChanged()
        {
            cachedRoot = null;
            base.OnTransformParentChanged();
            clippersStale = true;
        }

        protected override void OnCanvasHierarchyChanged()
        {
            cachedRoot = null;
            base.OnCanvasHierarchyChanged();
            clippersStale = true;
        }
    }
}
