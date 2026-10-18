using System;
using System.Collections;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// A value between minValue and maxValue, shown by a fill rect and a handle and set by
    /// dragging, clicking the track or the arrow keys.
    public partial class Slider
    {
        public enum Axis { Horizontal = 0, Vertical = 1 }

        [NonSerialized] Image fillImage;
        [NonSerialized] RectTransform fillContainer;
        [NonSerialized] RectTransform handleContainer;
        [NonSerialized] Vector2 grab;
        DrivenRectTransformTracker tracker;

        protected Slider()
        {
            m_Direction = Direction.LeftToRight;
            m_MinValue = 0f;
            m_MaxValue = 1f;
            m_OnValueChanged = new SliderEvent();
        }

        public RectTransform fillRect
        {
            get => m_FillRect;
            set { if (m_FillRect == value) return; m_FillRect = value; UpdateCachedReferences(); UpdateVisuals(); }
        }

        public RectTransform handleRect
        {
            get => m_HandleRect;
            set { if (m_HandleRect == value) return; m_HandleRect = value; UpdateCachedReferences(); UpdateVisuals(); }
        }

        public Direction direction
        {
            get => m_Direction;
            set { if (m_Direction == value) return; m_Direction = value; UpdateVisuals(); }
        }

        public float minValue
        {
            get => m_MinValue;
            set { if (m_MinValue == value) return; m_MinValue = value; Set(m_Value); UpdateVisuals(); }
        }

        public float maxValue
        {
            get => m_MaxValue;
            set { if (m_MaxValue == value) return; m_MaxValue = value; Set(m_Value); UpdateVisuals(); }
        }

        public bool wholeNumbers
        {
            get => m_WholeNumbers;
            set { if (m_WholeNumbers == value) return; m_WholeNumbers = value; Set(m_Value); UpdateVisuals(); }
        }

        public virtual float value
        {
            get => wholeNumbers ? Mathf.Round(m_Value) : m_Value;
            set => Set(value);
        }

        public virtual void SetValueWithoutNotify(float input) => Set(input, false);

        /// The value as a fraction of the range (0 when the range is empty).
        public float normalizedValue
        {
            get => Mathf.Approximately(minValue, maxValue) ? 0f : Mathf.InverseLerp(minValue, maxValue, value);
            set => this.value = Mathf.Lerp(minValue, maxValue, value);
        }

        public SliderEvent onValueChanged
        {
            get => m_OnValueChanged;
            set => m_OnValueChanged = value;
        }

        /// One arrow-key step: 1 for whole numbers, else a tenth of the range.
        float stepSize => wholeNumbers ? 1f : (maxValue - minValue) * 0.1f;

        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateCachedReferences();
            Set(m_Value, false);
            UpdateVisuals();
        }

        protected override void OnDisable()
        {
            tracker.Clear();
            base.OnDisable();
        }

        /// An animated fill or handle drives the value.
        protected override void OnDidApplyAnimationProperties()
        {
            m_Value = Clamped(m_Value);
            float before = normalizedValue;
            int a = (int)axis;
            if (fillContainer != null)
            {
                if (fillImage != null && fillImage.type == Image.Type.Filled) before = fillImage.fillAmount;
                else before = reverseValue ? 1f - m_FillRect.anchorMin[a] : m_FillRect.anchorMax[a];
            }
            else if (handleContainer != null) before = reverseValue ? 1f - m_HandleRect.anchorMin[a] : m_HandleRect.anchorMin[a];
            UpdateVisuals();
            if (before != normalizedValue)
            {
                UISystemProfilerApi.AddMarker("Slider.value", this);
                onValueChanged.Invoke(m_Value);
            }
            base.OnDidApplyAnimationProperties();
        }

        /// The fill and handle are used only when they are not the slider itself; their parents are
        /// the tracks.
        void UpdateCachedReferences()
        {
            if (m_FillRect != null && m_FillRect != (RectTransform)transform)
            {
                fillImage = m_FillRect.GetComponent<Image>();
                fillContainer = m_FillRect.parent != null ? m_FillRect.parent.GetComponent<RectTransform>() : null;
            }
            else
            {
                m_FillRect = null;
                fillContainer = null;
                fillImage = null;
            }
            if (m_HandleRect != null && m_HandleRect != (RectTransform)transform)
                handleContainer = m_HandleRect.parent != null ? m_HandleRect.parent.GetComponent<RectTransform>() : null;
            else
            {
                m_HandleRect = null;
                handleContainer = null;
            }
        }

        float Clamped(float input)
        {
            float v = Mathf.Clamp(input, minValue, maxValue);
            return wholeNumbers ? Mathf.Round(v) : v;
        }

        /// Sets the value (clamped, rounded for whole numbers); the event fires when it changed.
        protected virtual void Set(float input, bool sendCallback = true)
        {
            float v = Clamped(input);
            if (m_Value == v) return;
            m_Value = v;
            UpdateVisuals();
            if (!sendCallback) return;
            UISystemProfilerApi.AddMarker("Slider.value", this);
            m_OnValueChanged.Invoke(v);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!IsActive()) return;
            UpdateVisuals();
        }

        Axis axis => m_Direction == Direction.LeftToRight || m_Direction == Direction.RightToLeft ? Axis.Horizontal : Axis.Vertical;

        bool reverseValue => m_Direction == Direction.RightToLeft || m_Direction == Direction.TopToBottom;

        /// The fill spans the track from its start to the value (a Filled image's fillAmount
        /// instead); the handle sits at the value.
        void UpdateVisuals()
        {
            tracker.Clear();
            int a = (int)axis;
            if (fillContainer != null)
            {
                tracker.Add(this, m_FillRect, DrivenTransformProperties.Anchors);
                var min = Vector2.zero;
                var max = Vector2.one;
                if (fillImage != null && fillImage.type == Image.Type.Filled) fillImage.fillAmount = normalizedValue;
                else if (reverseValue) min[a] = 1f - normalizedValue;
                else max[a] = normalizedValue;
                m_FillRect.anchorMin = min;
                m_FillRect.anchorMax = max;
            }
            if (handleContainer != null)
            {
                tracker.Add(this, m_HandleRect, DrivenTransformProperties.Anchors);
                var min = Vector2.zero;
                var max = Vector2.one;
                min[a] = max[a] = reverseValue ? 1f - normalizedValue : normalizedValue;
                m_HandleRect.anchorMin = min;
                m_HandleRect.anchorMax = max;
            }
        }

        /// The value under the pointer, less the offset at which the handle was grabbed.
        void UpdateDrag(PointerEventData eventData, Camera cam)
        {
            var track = handleContainer != null ? handleContainer : fillContainer;
            int a = (int)axis;
            if (track == null || track.rect.size[a] <= 0f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, cam, out var local)) return;
            local -= track.rect.position;
            float t = Mathf.Clamp01((local - grab)[a] / track.rect.size[a]);
            normalizedValue = reverseValue ? 1f - t : t;
        }

        bool MayDrag(PointerEventData eventData) => IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;

        /// Grabbing the handle keeps the grab offset; pressing the track jumps there.
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            base.OnPointerDown(eventData);
            grab = Vector2.zero;
            var at = eventData.pointerPressRaycast.screenPosition;
            if (handleContainer != null && RectTransformUtility.RectangleContainsScreenPoint(m_HandleRect, at, eventData.enterEventCamera))
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandleRect, at, eventData.pressEventCamera, out var local)) grab = local;
            }
            else UpdateDrag(eventData, eventData.pressEventCamera);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            UpdateDrag(eventData, eventData.pressEventCamera);
        }

        /// The arrow keys along the slider's axis step the value (unless explicit navigation leads
        /// elsewhere); others navigate.
        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (axis == Axis.Horizontal && FindSelectableOnLeft() == null) Set(reverseValue ? value + stepSize : value - stepSize);
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Right:
                    if (axis == Axis.Horizontal && FindSelectableOnRight() == null) Set(reverseValue ? value - stepSize : value + stepSize);
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Up:
                    if (axis == Axis.Vertical && FindSelectableOnUp() == null) Set(reverseValue ? value - stepSize : value + stepSize);
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Down:
                    if (axis == Axis.Vertical && FindSelectableOnDown() == null) Set(reverseValue ? value + stepSize : value - stepSize);
                    else base.OnMove(eventData);
                    break;
            }
        }

        /// Automatic navigation does not leave the slider along its own axis.
        public override Selectable FindSelectableOnLeft()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Horizontal ? null : base.FindSelectableOnLeft();

        public override Selectable FindSelectableOnRight()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Horizontal ? null : base.FindSelectableOnRight();

        public override Selectable FindSelectableOnUp()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Vertical ? null : base.FindSelectableOnUp();

        public override Selectable FindSelectableOnDown()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Vertical ? null : base.FindSelectableOnDown();

        public virtual void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        /// Changes the direction; with includeRectLayouts the slider's layout is flipped to match.
        public void SetDirection(Direction direction, bool includeRectLayouts)
        {
            var oldAxis = axis;
            bool oldReverse = reverseValue;
            this.direction = direction;
            if (!includeRectLayouts) return;
            if (axis != oldAxis) RectTransformUtility.FlipLayoutAxes(transform as RectTransform, true, true);
            if (reverseValue != oldReverse) RectTransformUtility.FlipLayoutOnAxis(transform as RectTransform, (int)axis, true, true);
        }
    }

    /// A scroll position (0 to 1) with a handle of `size`, set by dragging the handle or pressing
    /// the track.
    public partial class Scrollbar
    {
        public enum Axis { Horizontal = 0, Vertical = 1 }

        [NonSerialized] RectTransform container;
        [NonSerialized] Vector2 grab;
        [NonSerialized] bool pressedOnTrack;
        [NonSerialized] Coroutine repeat;
        DrivenRectTransformTracker tracker;

        protected Scrollbar()
        {
            m_Direction = Direction.LeftToRight;
            m_Size = 0.2f;
            m_OnValueChanged = new ScrollEvent();
        }

        public RectTransform handleRect
        {
            get => m_HandleRect;
            set { if (m_HandleRect == value) return; m_HandleRect = value; UpdateCachedReferences(); UpdateVisuals(); }
        }

        public Direction direction
        {
            get => m_Direction;
            set { if (m_Direction == value) return; m_Direction = value; UpdateVisuals(); }
        }

        /// The position, snapped to numberOfSteps when there are steps. It is not clamped: a
        /// ScrollRect's overshoot reaches it.
        public float value
        {
            get
            {
                float v = m_Value;
                if (m_NumberOfSteps > 1) v = Mathf.Round(v * (m_NumberOfSteps - 1)) / (m_NumberOfSteps - 1);
                return v;
            }
            set => Set(value);
        }

        public virtual void SetValueWithoutNotify(float input) => Set(input, false);

        public float size
        {
            get => m_Size;
            set
            {
                float v = Mathf.Clamp01(value);
                if (m_Size == v) return;
                m_Size = v;
                UpdateVisuals();
            }
        }

        public int numberOfSteps
        {
            get => m_NumberOfSteps;
            set { if (m_NumberOfSteps == value) return; m_NumberOfSteps = value; Set(m_Value); UpdateVisuals(); }
        }

        public ScrollEvent onValueChanged
        {
            get => m_OnValueChanged;
            set => m_OnValueChanged = value;
        }

        float stepSize => m_NumberOfSteps > 1 ? 1f / (m_NumberOfSteps - 1) : 0.1f;

        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateCachedReferences();
            Set(m_Value, false);
            UpdateVisuals();
        }

        protected override void OnDisable()
        {
            tracker.Clear();
            base.OnDisable();
        }

        void UpdateCachedReferences()
        {
            container = m_HandleRect != null && m_HandleRect.parent != null ? m_HandleRect.parent.GetComponent<RectTransform>() : null;
        }

        void Set(float input, bool sendCallback = true)
        {
            float before = value;
            m_Value = input;
            if (before == value) return;
            UpdateVisuals();
            if (!sendCallback) return;
            UISystemProfilerApi.AddMarker("Scrollbar.value", this);
            m_OnValueChanged.Invoke(value);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!IsActive()) return;
            UpdateVisuals();
        }

        Axis axis => m_Direction == Direction.LeftToRight || m_Direction == Direction.RightToLeft ? Axis.Horizontal : Axis.Vertical;

        bool reverseValue => m_Direction == Direction.RightToLeft || m_Direction == Direction.TopToBottom;

        /// The handle spans `size` of the track, at `value` of the rest.
        void UpdateVisuals()
        {
            tracker.Clear();
            if (container == null) return;
            tracker.Add(this, m_HandleRect, DrivenTransformProperties.Anchors);
            var min = Vector2.zero;
            var max = Vector2.one;
            int a = (int)axis;
            float start = Mathf.Clamp01(value) * (1f - size);
            if (reverseValue)
            {
                min[a] = 1f - start - size;
                max[a] = 1f - start;
            }
            else
            {
                min[a] = start;
                max[a] = start + size;
            }
            m_HandleRect.anchorMin = min;
            m_HandleRect.anchorMax = max;
        }

        /// The value that puts the handle's grabbed point under the pointer.
        void UpdateDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || container == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, eventData.pressEventCamera, out var local)) return;
            var centre = local - grab - container.rect.position;
            var corner = centre - (m_HandleRect.rect.size - m_HandleRect.sizeDelta) * 0.5f;
            float track = axis == Axis.Horizontal ? container.rect.width : container.rect.height;
            float free = track * (1f - size);
            if (free <= 0f) return;
            switch (m_Direction)
            {
                case Direction.LeftToRight: Set(Mathf.Clamp01(corner.x / free)); break;
                case Direction.RightToLeft: Set(Mathf.Clamp01(1f - corner.x / free)); break;
                case Direction.BottomToTop: Set(Mathf.Clamp01(corner.y / free)); break;
                case Direction.TopToBottom: Set(Mathf.Clamp01(1f - corner.y / free)); break;
            }
        }

        bool MayDrag(PointerEventData eventData) => IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;

        /// A drag grabs the handle where it was pressed (its centre when pressed on the track).
        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            pressedOnTrack = false;
            if (!MayDrag(eventData) || container == null) return;
            grab = Vector2.zero;
            var at = eventData.pointerPressRaycast.screenPosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(m_HandleRect, at, eventData.enterEventCamera)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandleRect, at, eventData.pressEventCamera, out var local))
                grab = local - m_HandleRect.rect.center;
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            if (container != null) UpdateDrag(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            base.OnPointerDown(eventData);
            pressedOnTrack = true;
            repeat = StartCoroutine(ClickRepeat(eventData.pointerPressRaycast.screenPosition, eventData.enterEventCamera));
        }

        protected IEnumerator ClickRepeat(PointerEventData eventData) => ClickRepeat(eventData.pointerPressRaycast.screenPosition, eventData.enterEventCamera);

        /// While the press on the track lasts, the handle moves a page (its size) towards the
        /// press each frame until it covers it.
        protected IEnumerator ClickRepeat(Vector2 screenPosition, Camera camera)
        {
            while (pressedOnTrack)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(m_HandleRect, screenPosition, camera)
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandleRect, screenPosition, camera, out var local))
                {
                    float along = axis == Axis.Horizontal ? local.x : local.y;
                    float page = along < 0f ? size : -size;
                    float v = Mathf.Clamp01(value + (reverseValue ? page : -page));
                    value = Mathf.Round(v * 10000f) / 10000f;
                }
                yield return null;
            }
            repeat = null;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            pressedOnTrack = false;
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (axis == Axis.Horizontal && FindSelectableOnLeft() == null) Set(Mathf.Clamp01(reverseValue ? value + stepSize : value - stepSize));
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Right:
                    if (axis == Axis.Horizontal && FindSelectableOnRight() == null) Set(Mathf.Clamp01(reverseValue ? value - stepSize : value + stepSize));
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Up:
                    if (axis == Axis.Vertical && FindSelectableOnUp() == null) Set(Mathf.Clamp01(reverseValue ? value - stepSize : value + stepSize));
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Down:
                    if (axis == Axis.Vertical && FindSelectableOnDown() == null) Set(Mathf.Clamp01(reverseValue ? value + stepSize : value - stepSize));
                    else base.OnMove(eventData);
                    break;
            }
        }

        public override Selectable FindSelectableOnLeft()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Horizontal ? null : base.FindSelectableOnLeft();

        public override Selectable FindSelectableOnRight()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Horizontal ? null : base.FindSelectableOnRight();

        public override Selectable FindSelectableOnUp()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Vertical ? null : base.FindSelectableOnUp();

        public override Selectable FindSelectableOnDown()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Vertical ? null : base.FindSelectableOnDown();

        public virtual void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void SetDirection(Direction direction, bool includeRectLayouts)
        {
            var oldAxis = axis;
            bool oldReverse = reverseValue;
            this.direction = direction;
            if (!includeRectLayouts) return;
            if (axis != oldAxis) RectTransformUtility.FlipLayoutAxes(transform as RectTransform, true, true);
            if (reverseValue != oldReverse) RectTransformUtility.FlipLayoutOnAxis(transform as RectTransform, (int)axis, true, true);
        }
    }
}
