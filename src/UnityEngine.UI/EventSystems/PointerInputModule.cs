using System.Collections.Generic;
using System.Text;

namespace UnityEngine.EventSystems
{
    /// Pointer bookkeeping for input modules: one PointerEventData per pointer id, the mouse's
    /// three buttons, and the move and drag steps every pointer goes through.
    public abstract partial class PointerInputModule
    {
        public const int kMouseLeftId = -1;
        public const int kMouseRightId = -2;
        public const int kMouseMiddleId = -3;
        public const int kFakeTouchesId = -4;

        readonly Dictionary<int, PointerEventData> pointers = new();
        readonly MouseState mouse = new();

        protected PointerInputModule() { }

        /// The pointer's data; with create, a new one is made when there is none. True when it was
        /// created by this call.
        protected bool GetPointerData(int id, out PointerEventData data, bool create)
        {
            if (!pointers.TryGetValue(id, out data) && create)
            {
                data = new PointerEventData(eventSystem) { pointerId = id };
                pointers.Add(id, data);
                return true;
            }
            return false;
        }

        protected void RemovePointerData(PointerEventData data) => pointers.Remove(data.pointerId);

        protected PointerEventData GetTouchPointerEventData(Touch input, out bool pressed, out bool released)
        {
            bool created = GetPointerData(input.fingerId, out var data, true);
            data.Reset();
            pressed = created || input.phase == TouchPhase.Began;
            released = input.phase == TouchPhase.Canceled || input.phase == TouchPhase.Ended;
            if (created) data.position = input.position;
            data.delta = pressed ? Vector2.zero : input.position - data.position;
            data.position = input.position;
            data.button = PointerEventData.InputButton.Left;
            if (input.phase == TouchPhase.Canceled)
                data.pointerCurrentRaycast = new RaycastResult();
            else
            {
                eventSystem.RaycastAll(data, m_RaycastResultCache);
                data.pointerCurrentRaycast = FindFirstRaycast(m_RaycastResultCache);
                m_RaycastResultCache.Clear();
            }
            data.pressure = input.pressure;
            data.altitudeAngle = input.altitudeAngle;
            data.azimuthAngle = input.azimuthAngle;
            data.radius = Vector2.one * input.radius;
            data.radiusVariance = Vector2.one * input.radiusVariance;
            return data;
        }

        /// The pointer state the other mouse buttons share with the left one.
        protected void CopyFromTo(PointerEventData from, PointerEventData to)
        {
            to.position = from.position;
            to.delta = from.delta;
            to.scrollDelta = from.scrollDelta;
            to.pointerCurrentRaycast = from.pointerCurrentRaycast;
            to.pointerEnter = from.pointerEnter;
            to.pressure = from.pressure;
            to.tangentialPressure = from.tangentialPressure;
            to.altitudeAngle = from.altitudeAngle;
            to.azimuthAngle = from.azimuthAngle;
            to.twist = from.twist;
            to.radius = from.radius;
            to.radiusVariance = from.radiusVariance;
        }

        protected PointerEventData.FramePressState StateForMouseButton(int buttonId)
        {
            bool down = input.GetMouseButtonDown(buttonId);
            bool up = input.GetMouseButtonUp(buttonId);
            if (down && up) return PointerEventData.FramePressState.PressedAndReleased;
            if (down) return PointerEventData.FramePressState.Pressed;
            if (up) return PointerEventData.FramePressState.Released;
            return PointerEventData.FramePressState.NotChanged;
        }

        protected virtual MouseState GetMousePointerEventData() => GetMousePointerEventData(0);

        /// Updates the three mouse pointers from the input (position, delta, scroll, the frame's
        /// button transitions) and raycasts again.
        protected virtual MouseState GetMousePointerEventData(int id)
        {
            bool created = GetPointerData(kMouseLeftId, out var left, true);
            left.Reset();
            Vector2 position = input.mousePosition;
            if (created) left.position = position;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // A locked cursor has no position.
                left.position = new Vector2(-1f, -1f);
                left.delta = Vector2.zero;
            }
            else
            {
                left.delta = position - left.position;
                left.position = position;
            }
            left.scrollDelta = input.mouseScrollDelta;
            left.button = PointerEventData.InputButton.Left;
            eventSystem.RaycastAll(left, m_RaycastResultCache);
            left.pointerCurrentRaycast = FindFirstRaycast(m_RaycastResultCache);
            m_RaycastResultCache.Clear();

            GetPointerData(kMouseRightId, out var right, true);
            right.Reset();
            CopyFromTo(left, right);
            right.button = PointerEventData.InputButton.Right;

            GetPointerData(kMouseMiddleId, out var middle, true);
            middle.Reset();
            CopyFromTo(left, middle);
            middle.button = PointerEventData.InputButton.Middle;

            mouse.SetButtonState(PointerEventData.InputButton.Left, StateForMouseButton(0), left);
            mouse.SetButtonState(PointerEventData.InputButton.Right, StateForMouseButton(1), right);
            mouse.SetButtonState(PointerEventData.InputButton.Middle, StateForMouseButton(2), middle);
            return mouse;
        }

        protected PointerEventData GetLastPointerEventData(int id)
        {
            GetPointerData(id, out var data, false);
            return data;
        }

        /// Hover follows the raycast; a locked cursor hovers nothing.
        protected virtual void ProcessMove(PointerEventData pointerEvent)
        {
            var target = Cursor.lockState == CursorLockMode.Locked ? null : pointerEvent.pointerCurrentRaycast.gameObject;
            HandlePointerExitAndEnter(pointerEvent, target);
        }

        /// Starts a drag once the pointer moved beyond the threshold from the press (or at once
        /// without the threshold), then sends drag for each move. A press that turns into a drag
        /// of another object is released without a click.
        protected virtual void ProcessDrag(PointerEventData pointerEvent)
        {
            if (!pointerEvent.IsPointerMoving() || Cursor.lockState == CursorLockMode.Locked || pointerEvent.pointerDrag == null) return;

            if (!pointerEvent.dragging && BeyondThreshold(pointerEvent))
            {
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.beginDragHandler);
                pointerEvent.dragging = true;
            }

            if (!pointerEvent.dragging) return;
            if (pointerEvent.pointerPress != pointerEvent.pointerDrag)
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
                pointerEvent.eligibleForClick = false;
                pointerEvent.pointerPress = null;
                pointerEvent.rawPointerPress = null;
            }
            ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.dragHandler);
        }

        bool BeyondThreshold(PointerEventData e)
        {
            if (!e.useDragThreshold) return true;
            float threshold = eventSystem.pixelDragThreshold;
            return (e.pressPosition - e.position).sqrMagnitude >= threshold * threshold;
        }

        public override bool IsPointerOverGameObject(int pointerId)
        {
            var last = GetLastPointerEventData(pointerId);
            return last != null && last.pointerEnter != null;
        }

        /// Every pointer leaves what it hovers, the pointers are forgotten and nothing stays
        /// selected.
        protected void ClearSelection()
        {
            var data = GetBaseEventData();
            foreach (var pointer in pointers.Values) HandlePointerExitAndEnter(pointer, null);
            pointers.Clear();
            eventSystem.SetSelectedGameObject(null, data);
        }

        /// Pressing outside the selection deselects it.
        protected void DeselectIfSelectionChanged(GameObject currentOverGo, BaseEventData pointerEvent)
        {
            var selectable = ExecuteEvents.GetEventHandler<ISelectHandler>(currentOverGo);
            if (selectable != eventSystem.currentSelectedGameObject) eventSystem.SetSelectedGameObject(null, pointerEvent);
        }

        public override string ToString()
        {
            var s = new StringBuilder("<b>Pointer Input Module of type: </b>" + GetType());
            s.AppendLine();
            foreach (var pointer in pointers)
            {
                if (pointer.Value == null) continue;
                s.AppendLine("<B>Pointer:</b> " + pointer.Key);
                s.AppendLine(pointer.Value.ToString());
            }
            return s.ToString();
        }

        protected partial class ButtonState
        {
            PointerEventData.InputButton which = PointerEventData.InputButton.Left;
            MouseButtonEventData data;

            public MouseButtonEventData eventData
            {
                get => data;
                set => data = value;
            }

            public PointerEventData.InputButton button
            {
                get => which;
                set => which = value;
            }
        }

        protected partial class MouseState
        {
            readonly List<ButtonState> buttons = new();

            public bool AnyPressesThisFrame()
            {
                foreach (var b in buttons)
                    if (b.eventData.PressedThisFrame()) return true;
                return false;
            }

            public bool AnyReleasesThisFrame()
            {
                foreach (var b in buttons)
                    if (b.eventData.ReleasedThisFrame()) return true;
                return false;
            }

            public ButtonState GetButtonState(PointerEventData.InputButton button)
            {
                foreach (var b in buttons)
                    if (b.button == button) return b;
                var created = new ButtonState { button = button, eventData = new MouseButtonEventData() };
                buttons.Add(created);
                return created;
            }

            public void SetButtonState(PointerEventData.InputButton button, PointerEventData.FramePressState stateForMouseButton, PointerEventData data)
            {
                var b = GetButtonState(button);
                b.eventData.buttonState = stateForMouseButton;
                b.eventData.buttonData = data;
            }
        }

        public partial class MouseButtonEventData
        {
            public bool PressedThisFrame() => buttonState == PointerEventData.FramePressState.Pressed || buttonState == PointerEventData.FramePressState.PressedAndReleased;

            public bool ReleasedThisFrame() => buttonState == PointerEventData.FramePressState.Released || buttonState == PointerEventData.FramePressState.PressedAndReleased;
        }
    }
}
