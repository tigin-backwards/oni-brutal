using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// Sizes its rect to the object's minimum or preferred layout size, per axis, keeping the pivot.
    public partial class ContentSizeFitter
    {
        [System.NonSerialized] RectTransform cachedRect;
        DrivenRectTransformTracker tracker;

        protected ContentSizeFitter() { }

        public FitMode horizontalFit
        {
            get => m_HorizontalFit;
            set { if (m_HorizontalFit == value) return; m_HorizontalFit = value; SetDirty(); }
        }

        public FitMode verticalFit
        {
            get => m_VerticalFit;
            set { if (m_VerticalFit == value) return; m_VerticalFit = value; SetDirty(); }
        }

        RectTransform rectTransform => cachedRect ??= GetComponent<RectTransform>();

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

        protected override void OnDisable()
        {
            tracker.Clear();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange() => SetDirty();

        void Fit(int axis)
        {
            var mode = axis == 0 ? m_HorizontalFit : m_VerticalFit;
            if (mode == FitMode.Unconstrained)
            {
                tracker.Add(this, rectTransform, DrivenTransformProperties.None);
                return;
            }
            tracker.Add(this, rectTransform, axis == 0 ? DrivenTransformProperties.SizeDeltaX : DrivenTransformProperties.SizeDeltaY);
            float size = mode == FitMode.MinSize ? LayoutUtility.GetMinSize(rectTransform, axis) : LayoutUtility.GetPreferredSize(rectTransform, axis);
            rectTransform.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, size);
        }

        public virtual void SetLayoutHorizontal()
        {
            tracker.Clear();
            Fit(0);
        }

        public virtual void SetLayoutVertical() => Fit(1);

        protected void SetDirty()
        {
            if (!IsActive()) return;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }
    }

    /// Keeps its rect at an aspect ratio: one side follows the other, or the rect fits inside or
    /// covers its parent.
    public partial class AspectRatioFitter
    {
        [System.NonSerialized] RectTransform cachedRect;
        DrivenRectTransformTracker tracker;
        bool updateNextFrame;
        bool hasParent = true;

        protected AspectRatioFitter()
        {
            m_AspectMode = AspectMode.None;
            m_AspectRatio = 1f;
        }

        public AspectMode aspectMode
        {
            get => m_AspectMode;
            set { if (m_AspectMode == value) return; m_AspectMode = value; SetDirty(); }
        }

        public float aspectRatio
        {
            get => m_AspectRatio;
            set { if (m_AspectRatio == value) return; m_AspectRatio = value; SetDirty(); }
        }

        RectTransform rectTransform => cachedRect ??= GetComponent<RectTransform>();

        protected override void OnEnable()
        {
            base.OnEnable();
            hasParent = rectTransform.parent != null;
            updateNextFrame = true;
            SetDirty();
        }

        /// A screen-space root canvas is sized by its canvas, and fitting to a parent needs one:
        /// the fitter turns itself off in either case.
        protected override void Start()
        {
            base.Start();
            if (!ValidOnObject() || !ModeValid()) enabled = false;
        }

        protected override void OnDisable()
        {
            tracker.Clear();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            hasParent = rectTransform.parent != null;
            SetDirty();
        }

        protected virtual void Update()
        {
            if (!updateNextFrame) return;
            updateNextFrame = false;
            SetDirty();
        }

        protected override void OnRectTransformDimensionsChange() => Apply();

        bool ValidOnObject()
        {
            var c = GetComponent<Canvas>();
            return !(c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);
        }

        bool ModeValid() => hasParent || (m_AspectMode != AspectMode.EnvelopeParent && m_AspectMode != AspectMode.FitInParent);

        Vector2 ParentSize => rectTransform.parent is RectTransform p ? p.rect.size : Vector2.zero;

        /// The sizeDelta that gives the size on the axis with the current anchors.
        float SizeDeltaFor(float size, int axis) => size - ParentSize[axis] * (rectTransform.anchorMax[axis] - rectTransform.anchorMin[axis]);

        void Apply()
        {
            if (!IsActive() || !ValidOnObject()) return;
            tracker.Clear();
            var rt = rectTransform;
            switch (m_AspectMode)
            {
                case AspectMode.HeightControlsWidth:
                    tracker.Add(this, rt, DrivenTransformProperties.SizeDeltaX);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.height * m_AspectRatio);
                    break;
                case AspectMode.WidthControlsHeight:
                    tracker.Add(this, rt, DrivenTransformProperties.SizeDeltaY);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rt.rect.width / m_AspectRatio);
                    break;
                case AspectMode.FitInParent:
                case AspectMode.EnvelopeParent:
                    if (!hasParent) break;
                    tracker.Add(this, rt, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.SizeDeltaY);
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.anchoredPosition = Vector2.zero;
                    var parent = ParentSize;
                    var delta = Vector2.zero;
                    // The parent is wider than the ratio: fitting is limited by its height,
                    // enveloping by its width.
                    bool wider = parent.y * m_AspectRatio < parent.x;
                    if (wider ^ (m_AspectMode == AspectMode.FitInParent)) delta.y = SizeDeltaFor(parent.x / m_AspectRatio, 1);
                    else delta.x = SizeDeltaFor(parent.y * m_AspectRatio, 0);
                    rt.sizeDelta = delta;
                    break;
            }
        }

        public virtual void SetLayoutHorizontal() { }

        public virtual void SetLayoutVertical() { }

        protected void SetDirty() => Apply();

        public bool IsComponentValidOnObject() => ValidOnObject();

        public bool IsAspectModeValid() => ModeValid();
    }

    /// Sets its root canvas's scale factor and reference pixels per unit: a constant scale, a
    /// scale from the screen size against a reference resolution, or one from the screen's DPI.
    public partial class CanvasScaler
    {
        const float MinimumResolution = 0.00001f;

        [System.NonSerialized] Canvas cachedCanvas;
        [System.NonSerialized] float appliedScale = 1f;
        [System.NonSerialized] float appliedPixelsPerUnit = 100f;

        protected CanvasScaler()
        {
            m_UiScaleMode = ScaleMode.ConstantPixelSize;
            m_ReferencePixelsPerUnit = 100f;
            m_ScaleFactor = 1f;
            m_ReferenceResolution = new Vector2(800f, 600f);
            m_ScreenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
            m_MatchWidthOrHeight = 0f;
            m_PhysicalUnit = Unit.Points;
            m_FallbackScreenDPI = 96f;
            m_DefaultSpriteDPI = 96f;
            m_DynamicPixelsPerUnit = 1f;
        }

        public ScaleMode uiScaleMode { get => m_UiScaleMode; set => m_UiScaleMode = value; }

        public float referencePixelsPerUnit { get => m_ReferencePixelsPerUnit; set => m_ReferencePixelsPerUnit = value; }

        public float scaleFactor { get => m_ScaleFactor; set => m_ScaleFactor = Mathf.Max(0.01f, value); }

        public Vector2 referenceResolution
        {
            get => m_ReferenceResolution;
            set
            {
                m_ReferenceResolution = value;
                if (m_ReferenceResolution.x > -MinimumResolution && m_ReferenceResolution.x < MinimumResolution) m_ReferenceResolution.x = MinimumResolution * Mathf.Sign(m_ReferenceResolution.x);
                if (m_ReferenceResolution.y > -MinimumResolution && m_ReferenceResolution.y < MinimumResolution) m_ReferenceResolution.y = MinimumResolution * Mathf.Sign(m_ReferenceResolution.y);
            }
        }

        public ScreenMatchMode screenMatchMode { get => m_ScreenMatchMode; set => m_ScreenMatchMode = value; }
        public float matchWidthOrHeight { get => m_MatchWidthOrHeight; set => m_MatchWidthOrHeight = value; }
        public Unit physicalUnit { get => m_PhysicalUnit; set => m_PhysicalUnit = value; }
        public float fallbackScreenDPI { get => m_FallbackScreenDPI; set => m_FallbackScreenDPI = value; }
        public float defaultSpriteDPI { get => m_DefaultSpriteDPI; set => m_DefaultSpriteDPI = Mathf.Max(1f, value); }
        public float dynamicPixelsPerUnit { get => m_DynamicPixelsPerUnit; set => m_DynamicPixelsPerUnit = value; }

        protected override void OnEnable()
        {
            base.OnEnable();
            cachedCanvas = GetComponent<Canvas>();
            Handle();
            Canvas.preWillRenderCanvases += Handle;
        }

        protected override void OnDisable()
        {
            SetScaleFactor(1f);
            SetReferencePixelsPerUnit(100f);
            Canvas.preWillRenderCanvases -= Handle;
            base.OnDisable();
        }

        /// Applies the scaling to a root canvas: every frame before the canvases update.
        protected virtual void Handle()
        {
            if (cachedCanvas == null || !cachedCanvas.isRootCanvas) return;
            if (cachedCanvas.renderMode == RenderMode.WorldSpace)
            {
                HandleWorldCanvas();
                return;
            }
            switch (m_UiScaleMode)
            {
                case ScaleMode.ConstantPixelSize: HandleConstantPixelSize(); break;
                case ScaleMode.ScaleWithScreenSize: HandleScaleWithScreenSize(); break;
                case ScaleMode.ConstantPhysicalSize: HandleConstantPhysicalSize(); break;
            }
        }

        protected virtual void HandleWorldCanvas()
        {
            SetScaleFactor(m_DynamicPixelsPerUnit);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }

        protected virtual void HandleConstantPixelSize()
        {
            SetScaleFactor(m_ScaleFactor);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }

        /// MatchWidthOrHeight blends the width and height ratios in log2 space; Expand takes the
        /// smaller ratio, Shrink the larger.
        protected virtual void HandleScaleWithScreenSize()
        {
            var screen = cachedCanvas.renderingDisplaySize;
            int display = cachedCanvas.targetDisplay;
            if (Screen.fullScreen && display > 0 && display < Display.displays.Length)
            {
                var d = Display.displays[display];
                screen = new Vector2(d.renderingWidth, d.renderingHeight);
            }
            float scale = 1f;
            switch (m_ScreenMatchMode)
            {
                case ScreenMatchMode.MatchWidthOrHeight:
                    float w = Mathf.Log(screen.x / m_ReferenceResolution.x, 2f);
                    float h = Mathf.Log(screen.y / m_ReferenceResolution.y, 2f);
                    scale = Mathf.Pow(2f, Mathf.Lerp(w, h, m_MatchWidthOrHeight));
                    break;
                case ScreenMatchMode.Expand:
                    scale = Mathf.Min(screen.x / m_ReferenceResolution.x, screen.y / m_ReferenceResolution.y);
                    break;
                case ScreenMatchMode.Shrink:
                    scale = Mathf.Max(screen.x / m_ReferenceResolution.x, screen.y / m_ReferenceResolution.y);
                    break;
            }
            SetScaleFactor(scale);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }

        protected virtual void HandleConstantPhysicalSize()
        {
            float dpi = Screen.dpi;
            if (dpi == 0f) dpi = m_FallbackScreenDPI;
            float target = m_PhysicalUnit switch
            {
                Unit.Centimeters => 2.54f,
                Unit.Millimeters => 25.4f,
                Unit.Inches => 1f,
                Unit.Points => 72f,
                Unit.Picas => 6f,
                _ => 1f,
            };
            SetScaleFactor(dpi / target);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit * target / m_DefaultSpriteDPI);
        }

        protected void SetScaleFactor(float scaleFactor)
        {
            if (scaleFactor == appliedScale || cachedCanvas == null) return;
            cachedCanvas.scaleFactor = scaleFactor;
            appliedScale = scaleFactor;
        }

        protected void SetReferencePixelsPerUnit(float referencePixelsPerUnit)
        {
            if (referencePixelsPerUnit == appliedPixelsPerUnit || cachedCanvas == null) return;
            cachedCanvas.referencePixelsPerUnit = referencePixelsPerUnit;
            appliedPixelsPerUnit = referencePixelsPerUnit;
        }
    }
}
