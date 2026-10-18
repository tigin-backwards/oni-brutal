using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// Scrolls its content inside the viewport by dragging, the scroll wheel and the scrollbars,
    /// with elastic or clamped edges and inertia.
    public partial class ScrollRect
    {
        /// Overshoot along an axis is ignored up to this (float error).
        const float Slack = 0.001f;

        protected Vector2 m_ContentStartPosition = Vector2.zero;
        protected Bounds m_ContentBounds;
        protected Bounds m_ViewBounds;

        [NonSerialized] RectTransform view;
        [NonSerialized] RectTransform cachedRect;
        [NonSerialized] RectTransform horizontalBarRect, verticalBarRect;
        Vector2 dragStartCursor = Vector2.zero;
        Vector2 velocityNow;
        bool dragging;
        bool scrolling;
        Vector2 previousPosition = Vector2.zero;
        Bounds previousContentBounds, previousViewBounds;
        [NonSerialized] bool layoutRebuilt;
        bool expandForHorizontalBar, expandForVerticalBar;
        float horizontalBarHeight, verticalBarWidth;
        DrivenRectTransformTracker tracker;
        readonly Vector3[] corners = new Vector3[4];

        protected ScrollRect()
        {
            m_Horizontal = true;
            m_Vertical = true;
            m_MovementType = MovementType.Elastic;
            m_Elasticity = 0.1f;
            m_Inertia = true;
            m_DecelerationRate = 0.135f;
            m_ScrollSensitivity = 1f;
            m_OnValueChanged = new ScrollRectEvent();
        }

        public RectTransform content { get => m_Content; set => m_Content = value; }
        public bool horizontal { get => m_Horizontal; set => m_Horizontal = value; }
        public bool vertical { get => m_Vertical; set => m_Vertical = value; }
        public MovementType movementType { get => m_MovementType; set => m_MovementType = value; }
        public float elasticity { get => m_Elasticity; set => m_Elasticity = value; }
        public bool inertia { get => m_Inertia; set => m_Inertia = value; }
        public float decelerationRate { get => m_DecelerationRate; set => m_DecelerationRate = value; }
        public float scrollSensitivity { get => m_ScrollSensitivity; set => m_ScrollSensitivity = value; }

        public RectTransform viewport
        {
            get => m_Viewport;
            set { m_Viewport = value; SetDirtyCaching(); }
        }

        public Scrollbar horizontalScrollbar
        {
            get => m_HorizontalScrollbar;
            set
            {
                if (m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
                m_HorizontalScrollbar = value;
                if (m_Horizontal && m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
                SetDirtyCaching();
            }
        }

        public Scrollbar verticalScrollbar
        {
            get => m_VerticalScrollbar;
            set
            {
                if (m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);
                m_VerticalScrollbar = value;
                if (m_Vertical && m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
                SetDirtyCaching();
            }
        }

        public ScrollbarVisibility horizontalScrollbarVisibility { get => m_HorizontalScrollbarVisibility; set { m_HorizontalScrollbarVisibility = value; SetDirtyCaching(); } }
        public ScrollbarVisibility verticalScrollbarVisibility { get => m_VerticalScrollbarVisibility; set { m_VerticalScrollbarVisibility = value; SetDirtyCaching(); } }
        public float horizontalScrollbarSpacing { get => m_HorizontalScrollbarSpacing; set { m_HorizontalScrollbarSpacing = value; SetDirty(); } }
        public float verticalScrollbarSpacing { get => m_VerticalScrollbarSpacing; set { m_VerticalScrollbarSpacing = value; SetDirty(); } }

        public ScrollRectEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }

        /// The viewport, or the scroll rect itself without one.
        protected RectTransform viewRect
        {
            get
            {
                if (view == null) view = m_Viewport;
                if (view == null) view = (RectTransform)transform;
                return view;
            }
        }

        RectTransform rectTransform => cachedRect ??= (RectTransform)transform;

        public Vector2 velocity { get => velocityNow; set => velocityNow = value; }

        // ----- Canvas element: caches before layout, settles the bounds after it -----

        public virtual void Rebuild(CanvasUpdate executing)
        {
            if (executing == CanvasUpdate.Prelayout) UpdateCachedData();
            if (executing == CanvasUpdate.PostLayout)
            {
                UpdateBounds();
                UpdateScrollbars(Vector2.zero);
                UpdatePrevData();
                layoutRebuilt = true;
            }
        }

        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }

        /// Whether the viewport makes room for each scrollbar: only when the viewport and the
        /// scrollbars are all children of the scroll rect (or absent).
        void UpdateCachedData()
        {
            horizontalBarRect = m_HorizontalScrollbar == null ? null : m_HorizontalScrollbar.transform as RectTransform;
            verticalBarRect = m_VerticalScrollbar == null ? null : m_VerticalScrollbar.transform as RectTransform;
            var t = transform;
            bool childrenOnly = viewRect.parent == t
                                && (!horizontalBarRect || horizontalBarRect.parent == t)
                                && (!verticalBarRect || verticalBarRect.parent == t);
            expandForHorizontalBar = childrenOnly && horizontalBarRect && m_HorizontalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
            expandForVerticalBar = childrenOnly && verticalBarRect && m_VerticalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
            horizontalBarHeight = horizontalBarRect == null ? 0f : horizontalBarRect.rect.height;
            verticalBarWidth = verticalBarRect == null ? 0f : verticalBarRect.rect.width;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_Horizontal && m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
            if (m_Vertical && m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            SetDirty();
        }

        protected override void OnDisable()
        {
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            if (m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
            if (m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);
            dragging = false;
            scrolling = false;
            layoutRebuilt = false;
            tracker.Clear();
            velocityNow = Vector2.zero;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }

        public override bool IsActive() => base.IsActive() && m_Content != null;

        /// Bounds read before the first layout would be wrong: the canvases update first.
        void EnsureLayoutHasRebuilt()
        {
            if (!layoutRebuilt && !CanvasUpdateRegistry.IsRebuildingLayout()) Canvas.ForceUpdateCanvases();
        }

        public virtual void StopMovement() => velocityNow = Vector2.zero;

        // ----- Input -----

        /// The wheel moves the content by its delta times scrollSensitivity (up scrolls towards
        /// the top). A one-axis scroll rect takes the delta it can use.
        public virtual void OnScroll(PointerEventData data)
        {
            if (!IsActive()) return;
            EnsureLayoutHasRebuilt();
            UpdateBounds();
            var delta = data.scrollDelta;
            delta.y *= -1f;
            if (m_Vertical && !m_Horizontal) delta.x = 0f;
            if (m_Horizontal && !m_Vertical)
            {
                if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x)) delta.x = delta.y;
                delta.y = 0f;
            }
            if (data.IsScrolling()) scrolling = true;
            var position = m_Content.anchoredPosition + delta * m_ScrollSensitivity;
            if (m_MovementType == MovementType.Clamped) position += CalculateOffset(position - m_Content.anchoredPosition);
            SetContentAnchoredPosition(position);
            UpdateBounds();
        }

        public virtual void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            velocityNow = Vector2.zero;
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive()) return;
            UpdateBounds();
            dragStartCursor = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out dragStartCursor);
            m_ContentStartPosition = m_Content.anchoredPosition;
            dragging = true;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            dragging = false;
        }

        /// The content follows the pointer; past its bounds an elastic rect gives way less and less.
        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.button != PointerEventData.InputButton.Left || !IsActive()) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out var cursor)) return;
            UpdateBounds();
            var position = m_ContentStartPosition + (cursor - dragStartCursor);
            var offset = CalculateOffset(position - m_Content.anchoredPosition);
            position += offset;
            if (m_MovementType == MovementType.Elastic)
            {
                if (offset.x != 0f) position.x -= RubberDelta(offset.x, m_ViewBounds.size.x);
                if (offset.y != 0f) position.y -= RubberDelta(offset.y, m_ViewBounds.size.y);
            }
            SetContentAnchoredPosition(position);
        }

        /// Moves the content on the axes that scroll.
        protected virtual void SetContentAnchoredPosition(Vector2 position)
        {
            if (!m_Horizontal) position.x = m_Content.anchoredPosition.x;
            if (!m_Vertical) position.y = m_Content.anchoredPosition.y;
            if (position == m_Content.anchoredPosition) return;
            m_Content.anchoredPosition = position;
            UpdateBounds();
        }

        /// Each frame (unscaled time): released content springs back into its bounds (elastic)
        /// or coasts with decaying velocity (inertia); while dragging the velocity follows the
        /// drag. Changes reach the scrollbars and onValueChanged.
        protected virtual void LateUpdate()
        {
            if (!m_Content) return;
            EnsureLayoutHasRebuilt();
            UpdateBounds();
            float dt = Time.unscaledDeltaTime;
            var offset = CalculateOffset(Vector2.zero);
            if (dt > 0f)
            {
                if (!dragging && (offset != Vector2.zero || velocityNow != Vector2.zero))
                {
                    var position = m_Content.anchoredPosition;
                    for (int axis = 0; axis < 2; axis++)
                    {
                        if (m_MovementType == MovementType.Elastic && offset[axis] != 0f)
                        {
                            float speed = velocityNow[axis];
                            // A wheel scroll springs back more slowly.
                            float smoothTime = scrolling ? m_Elasticity * 3f : m_Elasticity;
                            position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis], m_Content.anchoredPosition[axis] + offset[axis], ref speed, smoothTime, Mathf.Infinity, dt);
                            if (Mathf.Abs(speed) < 1f) speed = 0f;
                            velocityNow[axis] = speed;
                        }
                        else if (m_Inertia)
                        {
                            velocityNow[axis] *= Mathf.Pow(m_DecelerationRate, dt);
                            if (Mathf.Abs(velocityNow[axis]) < 1f) velocityNow[axis] = 0f;
                            position[axis] += velocityNow[axis] * dt;
                        }
                        else velocityNow[axis] = 0f;
                    }
                    if (m_MovementType == MovementType.Clamped) position += CalculateOffset(position - m_Content.anchoredPosition);
                    SetContentAnchoredPosition(position);
                }
                if (dragging && m_Inertia)
                {
                    Vector2 moved = (m_Content.anchoredPosition - previousPosition) / dt;
                    velocityNow = Vector2.Lerp(velocityNow, moved, dt * 10f);
                }
            }
            if (m_ViewBounds != previousViewBounds || m_ContentBounds != previousContentBounds || m_Content.anchoredPosition != previousPosition)
            {
                UpdateScrollbars(offset);
                UISystemProfilerApi.AddMarker("ScrollRect.value", this);
                m_OnValueChanged.Invoke(normalizedPosition);
                UpdatePrevData();
            }
            UpdateScrollbarVisibility();
            scrolling = false;
        }

        protected void UpdatePrevData()
        {
            previousPosition = m_Content == null ? Vector2.zero : m_Content.anchoredPosition;
            previousViewBounds = m_ViewBounds;
            previousContentBounds = m_ContentBounds;
        }

        /// Each scrollbar shows the visible fraction of the content (less any overshoot) and the
        /// position.
        void UpdateScrollbars(Vector2 offset)
        {
            if (m_HorizontalScrollbar)
            {
                m_HorizontalScrollbar.size = m_ContentBounds.size.x > 0f ? Mathf.Clamp01((m_ViewBounds.size.x - Mathf.Abs(offset.x)) / m_ContentBounds.size.x) : 1f;
                m_HorizontalScrollbar.value = horizontalNormalizedPosition;
            }
            if (m_VerticalScrollbar)
            {
                m_VerticalScrollbar.size = m_ContentBounds.size.y > 0f ? Mathf.Clamp01((m_ViewBounds.size.y - Mathf.Abs(offset.y)) / m_ContentBounds.size.y) : 1f;
                m_VerticalScrollbar.value = verticalNormalizedPosition;
            }
        }

        // ----- Normalized position: 0 at the left/bottom of the content, 1 at the right/top -----

        public Vector2 normalizedPosition
        {
            get => new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition);
            set
            {
                SetNormalizedPosition(value.x, 0);
                SetNormalizedPosition(value.y, 1);
            }
        }

        public float horizontalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                return NormalizedOn(0);
            }
            set => SetNormalizedPosition(value, 0);
        }

        public float verticalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                return NormalizedOn(1);
            }
            set => SetNormalizedPosition(value, 1);
        }

        /// Where the view is over the content's scrollable range; content no larger than the view
        /// is at 0 or 1, by which side sticks out.
        float NormalizedOn(int axis)
        {
            float content = m_ContentBounds.size[axis], viewSize = m_ViewBounds.size[axis];
            if (content <= viewSize || Mathf.Approximately(content, viewSize)) return m_ViewBounds.min[axis] > m_ContentBounds.min[axis] ? 1f : 0f;
            return (m_ViewBounds.min[axis] - m_ContentBounds.min[axis]) / (content - viewSize);
        }

        void SetHorizontalNormalizedPosition(float value) => SetNormalizedPosition(value, 0);
        void SetVerticalNormalizedPosition(float value) => SetNormalizedPosition(value, 1);

        /// Moves the content so that the view is at value of its scrollable range on the axis
        /// (moves under 0.01 are ignored), stopping its motion on that axis.
        protected virtual void SetNormalizedPosition(float value, int axis)
        {
            EnsureLayoutHasRebuilt();
            UpdateBounds();
            float hidden = m_ContentBounds.size[axis] - m_ViewBounds.size[axis];
            float contentMin = m_ViewBounds.min[axis] - value * hidden;
            float target = m_Content.anchoredPosition[axis] + contentMin - m_ContentBounds.min[axis];
            var position = m_Content.anchoredPosition;
            if (Mathf.Abs(position[axis] - target) <= 0.01f) return;
            position[axis] = target;
            m_Content.anchoredPosition = position;
            velocityNow[axis] = 0f;
            UpdateBounds();
        }

        /// How far an overshoot of `overStretching` beyond the bounds actually moves the content:
        /// less and less, approaching the view size.
        static float RubberDelta(float overStretching, float viewSize)
            => (1f - 1f / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1f)) * viewSize * Mathf.Sign(overStretching);

        protected override void OnRectTransformDimensionsChange() => SetDirty();

        bool hScrollingNeeded => !Application.isPlaying || m_ContentBounds.size.x > m_ViewBounds.size.x + 0.01f;
        bool vScrollingNeeded => !Application.isPlaying || m_ContentBounds.size.y > m_ViewBounds.size.y + 0.01f;

        // ----- Layout: the scroll rect lays out its viewport and scrollbars -----

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual float minWidth => -1f;
        public virtual float preferredWidth => -1f;
        public virtual float flexibleWidth => -1f;
        public virtual float minHeight => -1f;
        public virtual float preferredHeight => -1f;
        public virtual float flexibleHeight => -1f;
        public virtual int layoutPriority => -1;

        /// With expanding scrollbars, the viewport fills the scroll rect, then shrinks for each
        /// scrollbar the content needs, the content laid out again at each size.
        public virtual void SetLayoutHorizontal()
        {
            tracker.Clear();
            UpdateCachedData();
            var v = viewRect;
            if (expandForHorizontalBar || expandForVerticalBar)
            {
                tracker.Add(this, v, DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);
                v.anchorMin = Vector2.zero;
                v.anchorMax = Vector2.one;
                v.sizeDelta = Vector2.zero;
                v.anchoredPosition = Vector2.zero;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                MeasureBounds();
            }
            if (expandForVerticalBar && vScrollingNeeded)
            {
                v.sizeDelta = new Vector2(-(verticalBarWidth + m_VerticalScrollbarSpacing), v.sizeDelta.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                MeasureBounds();
            }
            if (expandForHorizontalBar && hScrollingNeeded)
            {
                v.sizeDelta = new Vector2(v.sizeDelta.x, -(horizontalBarHeight + m_HorizontalScrollbarSpacing));
                MeasureBounds();
            }
            // The horizontal bar can make the content need the vertical one after all.
            if (expandForVerticalBar && vScrollingNeeded && v.sizeDelta.x == 0f && v.sizeDelta.y < 0f)
                v.sizeDelta = new Vector2(-(verticalBarWidth + m_VerticalScrollbarSpacing), v.sizeDelta.y);
        }

        public virtual void SetLayoutVertical()
        {
            UpdateScrollbarLayout();
            MeasureBounds();
        }

        void MeasureBounds()
        {
            var r = viewRect.rect;
            m_ViewBounds = new Bounds(r.center, r.size);
            m_ContentBounds = GetBounds();
        }

        /// A permanent scrollbar shows while its axis scrolls; an auto-hiding one while the
        /// content needs it.
        void UpdateScrollbarVisibility()
        {
            ShowBar(m_VerticalScrollbar, m_VerticalScrollbarVisibility, vScrollingNeeded, m_Vertical);
            ShowBar(m_HorizontalScrollbar, m_HorizontalScrollbarVisibility, hScrollingNeeded, m_Horizontal);
        }

        static void ShowBar(Scrollbar bar, ScrollbarVisibility visibility, bool needed, bool axisEnabled)
        {
            if (!bar) return;
            bool show = visibility == ScrollbarVisibility.Permanent ? axisEnabled : needed;
            if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
        }

        /// Expanding scrollbars span the scroll rect, leaving room for the other one when shown.
        void UpdateScrollbarLayout()
        {
            if (expandForVerticalBar && m_HorizontalScrollbar)
            {
                tracker.Add(this, horizontalBarRect, DrivenTransformProperties.AnchorMinX | DrivenTransformProperties.AnchorMaxX | DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.AnchoredPositionX);
                var r = horizontalBarRect;
                r.anchorMin = new Vector2(0f, r.anchorMin.y);
                r.anchorMax = new Vector2(1f, r.anchorMax.y);
                r.anchoredPosition = new Vector2(0f, r.anchoredPosition.y);
                r.sizeDelta = new Vector2(vScrollingNeeded ? -(verticalBarWidth + m_VerticalScrollbarSpacing) : 0f, r.sizeDelta.y);
            }
            if (expandForHorizontalBar && m_VerticalScrollbar)
            {
                tracker.Add(this, verticalBarRect, DrivenTransformProperties.AnchorMinY | DrivenTransformProperties.AnchorMaxY | DrivenTransformProperties.SizeDeltaY | DrivenTransformProperties.AnchoredPositionY);
                var r = verticalBarRect;
                r.anchorMin = new Vector2(r.anchorMin.x, 0f);
                r.anchorMax = new Vector2(r.anchorMax.x, 1f);
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, 0f);
                r.sizeDelta = new Vector2(r.sizeDelta.x, hScrollingNeeded ? -(horizontalBarHeight + m_HorizontalScrollbarSpacing) : 0f);
            }
        }

        // ----- Bounds, in the view's space -----

        /// The view and content bounds. Content smaller than the view is grown to the view's size
        /// around its pivot; a clamped rect pulls content that shrank back inside the view.
        protected void UpdateBounds()
        {
            var r = viewRect.rect;
            m_ViewBounds = new Bounds(r.center, r.size);
            m_ContentBounds = GetBounds();
            if (m_Content == null) return;
            Vector3 size = m_ContentBounds.size;
            Vector3 centre = m_ContentBounds.center;
            var pivot = m_Content.pivot;
            GrowToView(m_ViewBounds, pivot, ref size, ref centre);
            m_ContentBounds.size = size;
            m_ContentBounds.center = centre;
            if (m_MovementType != MovementType.Clamped) return;
            var delta = Vector2.zero;
            for (int a = 0; a < 2; a++)
            {
                if (m_ViewBounds.max[a] > m_ContentBounds.max[a])
                    delta[a] = Math.Min(m_ViewBounds.min[a] - m_ContentBounds.min[a], m_ViewBounds.max[a] - m_ContentBounds.max[a]);
                else if (m_ViewBounds.min[a] < m_ContentBounds.min[a])
                    delta[a] = Math.Max(m_ViewBounds.min[a] - m_ContentBounds.min[a], m_ViewBounds.max[a] - m_ContentBounds.max[a]);
            }
            if (delta.sqrMagnitude <= float.Epsilon) return;
            centre = m_Content.anchoredPosition + delta;
            if (!m_Horizontal) centre.x = m_Content.anchoredPosition.x;
            if (!m_Vertical) centre.y = m_Content.anchoredPosition.y;
            GrowToView(m_ViewBounds, pivot, ref size, ref centre);
        }

        static void GrowToView(Bounds viewBounds, Vector2 pivot, ref Vector3 size, ref Vector3 centre)
        {
            var excess = viewBounds.size - size;
            if (excess.x > 0f)
            {
                centre.x -= excess.x * (pivot.x - 0.5f);
                size.x = viewBounds.size.x;
            }
            if (excess.y > 0f)
            {
                centre.y -= excess.y * (pivot.y - 0.5f);
                size.y = viewBounds.size.y;
            }
        }

        /// The content's corners in the view's space.
        Bounds GetBounds()
        {
            if (m_Content == null) return new Bounds();
            m_Content.GetWorldCorners(corners);
            var toView = viewRect.worldToLocalMatrix;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var p = toView.MultiplyPoint3x4(corners[i]);
                min = Vector3.Min(p, min);
                max = Vector3.Max(p, max);
            }
            var b = new Bounds(min, Vector3.zero);
            b.Encapsulate(max);
            return b;
        }

        /// How far the content (moved by delta) must move to sit inside its bounds on each axis
        /// that scrolls; nothing for unrestricted movement.
        Vector2 CalculateOffset(Vector2 delta)
        {
            var offset = Vector2.zero;
            if (m_MovementType == MovementType.Unrestricted) return offset;
            Vector2 min = m_ContentBounds.min, max = m_ContentBounds.max;
            if (m_Horizontal)
            {
                float toMin = m_ViewBounds.min.x - (min.x + delta.x);
                float toMax = m_ViewBounds.max.x - (max.x + delta.x);
                if (toMin < -Slack) offset.x = toMin;
                else if (toMax > Slack) offset.x = toMax;
            }
            if (m_Vertical)
            {
                float toMin = m_ViewBounds.min.y - (min.y + delta.y);
                float toMax = m_ViewBounds.max.y - (max.y + delta.y);
                if (toMax > Slack) offset.y = toMax;
                else if (toMin < -Slack) offset.y = toMin;
            }
            return offset;
        }

        protected void SetDirty()
        {
            if (!IsActive()) return;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected void SetDirtyCaching()
        {
            if (!IsActive()) return;
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            view = null;
        }
    }
}
