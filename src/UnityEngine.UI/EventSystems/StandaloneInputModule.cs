namespace UnityEngine.EventSystems
{
    /// Mouse, touch, keyboard and controller input for the UI: pointer presses, clicks, drags and
    /// scrolling, and navigation, submit and cancel for the selected object.
    public partial class StandaloneInputModule
    {
        /// The double-click window for clickCount, in seconds.
        const float ClickInterval = 0.3f;

        float lastMoveTime;
        Vector2 lastMove;
        int repeatedMoves;
        Vector2 mousePositionNow;
        Vector2 mousePositionBefore;
        GameObject focused;
        PointerEventData lastPointerEvent;

        protected StandaloneInputModule()
        {
            m_HorizontalAxis = "Horizontal";
            m_VerticalAxis = "Vertical";
            m_SubmitButton = "Submit";
            m_CancelButton = "Cancel";
            m_InputActionsPerSecond = 10f;
            m_RepeatDelay = 0.5f;
        }

        [System.Obsolete("Mode is no longer needed on input module as it handles both mouse and keyboard simultaneously.", false)]
        public enum InputMode
        {
            Mouse = 0,
            Buttons = 1,
        }

        [System.Obsolete("Mode is no longer needed on input module as it handles both mouse and keyboard simultaneously.", false)]
        public InputMode inputMode => InputMode.Mouse;

        [System.Obsolete("allowActivationOnMobileDevice has been deprecated. Use forceModuleActive instead (UnityUpgradable) -> forceModuleActive")]
        public bool allowActivationOnMobileDevice
        {
            get => m_ForceModuleActive;
            set => m_ForceModuleActive = value;
        }

        public bool forceModuleActive
        {
            get => m_ForceModuleActive;
            set => m_ForceModuleActive = value;
        }

        public float inputActionsPerSecond
        {
            get => m_InputActionsPerSecond;
            set => m_InputActionsPerSecond = value;
        }

        public float repeatDelay
        {
            get => m_RepeatDelay;
            set => m_RepeatDelay = value;
        }

        public string horizontalAxis
        {
            get => m_HorizontalAxis;
            set => m_HorizontalAxis = value;
        }

        public string verticalAxis
        {
            get => m_VerticalAxis;
            set => m_VerticalAxis = value;
        }

        public string submitButton
        {
            get => m_SubmitButton;
            set => m_SubmitButton = value;
        }

        public string cancelButton
        {
            get => m_CancelButton;
            set => m_CancelButton = value;
        }

        /// A desktop player ignores input while the application is not focused.
        bool IgnoringInput => !eventSystem.isFocused;

        public override void UpdateModule()
        {
            if (IgnoringInput)
            {
                // Losing focus mid-drag ends the drag.
                if (lastPointerEvent != null && lastPointerEvent.pointerDrag != null && lastPointerEvent.dragging)
                    ReleaseMouse(lastPointerEvent, lastPointerEvent.pointerCurrentRaycast.gameObject);
                lastPointerEvent = null;
                return;
            }
            mousePositionBefore = mousePositionNow;
            mousePositionNow = input.mousePosition;
        }

        public override bool IsModuleSupported() => m_ForceModuleActive || input.mousePresent || input.touchSupported;

        public override bool ShouldActivateModule()
        {
            if (!base.ShouldActivateModule()) return false;
            return m_ForceModuleActive
                   || input.GetButtonDown(m_SubmitButton)
                   || input.GetButtonDown(m_CancelButton)
                   || !Mathf.Approximately(input.GetAxisRaw(m_HorizontalAxis), 0f)
                   || !Mathf.Approximately(input.GetAxisRaw(m_VerticalAxis), 0f)
                   || (mousePositionNow - mousePositionBefore).sqrMagnitude > 0f
                   || input.GetMouseButtonDown(0)
                   || input.touchCount > 0;
        }

        /// Activation selects the current selection again, or the first selected object.
        public override void ActivateModule()
        {
            if (IgnoringInput) return;
            base.ActivateModule();
            mousePositionNow = input.mousePosition;
            mousePositionBefore = mousePositionNow;
            var toSelect = eventSystem.currentSelectedGameObject;
            if (toSelect == null) toSelect = eventSystem.firstSelectedGameObject;
            eventSystem.SetSelectedGameObject(toSelect, GetBaseEventData());
        }

        public override void DeactivateModule()
        {
            base.DeactivateModule();
            ClearSelection();
        }

        public override void Process()
        {
            if (IgnoringInput) return;
            bool used = SendUpdateEventToSelectedObject();
            if (!ProcessTouchEvents() && input.mousePresent) ProcessMouseEvent();
            if (!eventSystem.sendNavigationEvents) return;
            if (!used) used |= SendMoveEventToSelectedObject();
            if (!used) SendSubmitEventToSelectedObject();
        }

        bool ProcessTouchEvents()
        {
            int count = input.touchCount;
            for (int i = 0; i < count; i++)
            {
                var touch = input.GetTouch(i);
                if (touch.type == TouchType.Indirect) continue;
                var pointer = GetTouchPointerEventData(touch, out bool pressed, out bool released);
                ProcessTouchPress(pointer, pressed, released);
                if (!released)
                {
                    ProcessMove(pointer);
                    ProcessDrag(pointer);
                }
                else RemovePointerData(pointer);
            }
            return count > 0;
        }

        protected void ProcessTouchPress(PointerEventData pointerEvent, bool pressed, bool released)
        {
            var over = pointerEvent.pointerCurrentRaycast.gameObject;
            if (pressed)
            {
                Press(pointerEvent, over);
                // A touch enters what it touches as it lands.
                if (pointerEvent.pointerEnter != over)
                {
                    HandlePointerExitAndEnter(pointerEvent, over);
                    pointerEvent.pointerEnter = over;
                }
            }
            if (!released) return;
            ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
            if (pointerEvent.pointerPress == clickHandler && pointerEvent.eligibleForClick)
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
            else if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.ExecuteHierarchy(over, pointerEvent, ExecuteEvents.dropHandler);
            if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);
            pointerEvent.eligibleForClick = false;
            pointerEvent.pointerPress = null;
            pointerEvent.rawPointerPress = null;
            pointerEvent.pointerClick = null;
            pointerEvent.dragging = false;
            pointerEvent.pointerDrag = null;
            // A lifted touch hovers nothing.
            ExecuteEvents.ExecuteHierarchy(pointerEvent.pointerEnter, pointerEvent, ExecuteEvents.pointerExitHandler);
            pointerEvent.pointerEnter = null;
            lastPointerEvent = pointerEvent;
        }

        /// The press half shared by touches and mouse buttons.
        void Press(PointerEventData pointerEvent, GameObject over)
        {
            pointerEvent.eligibleForClick = true;
            pointerEvent.delta = Vector2.zero;
            pointerEvent.dragging = false;
            pointerEvent.useDragThreshold = true;
            pointerEvent.pressPosition = pointerEvent.position;
            pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;

            DeselectIfSelectionChanged(over, pointerEvent);

            var pressed = ExecuteEvents.ExecuteHierarchy(over, pointerEvent, ExecuteEvents.pointerDownHandler);
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
            if (pressed == null) pressed = clickHandler;

            float now = Time.unscaledTime;
            if (pressed == pointerEvent.lastPress && now - pointerEvent.clickTime < ClickInterval) pointerEvent.clickCount++;
            else pointerEvent.clickCount = 1;

            pointerEvent.pointerPress = pressed;
            pointerEvent.rawPointerPress = over;
            pointerEvent.pointerClick = clickHandler;
            pointerEvent.clickTime = now;

            pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(over);
            if (pointerEvent.pointerDrag != null)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
        }

        protected bool SendSubmitEventToSelectedObject()
        {
            if (eventSystem.currentSelectedGameObject == null) return false;
            var data = GetBaseEventData();
            if (input.GetButtonDown(m_SubmitButton)) ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
            if (input.GetButtonDown(m_CancelButton)) ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.cancelHandler);
            return data.used;
        }

        /// The navigation axes, at full deflection on the frame an axis button goes down.
        Vector2 RawMove()
        {
            var move = new Vector2(input.GetAxisRaw(m_HorizontalAxis), input.GetAxisRaw(m_VerticalAxis));
            if (input.GetButtonDown(m_HorizontalAxis)) move.x = move.x < 0f ? -1f : move.x > 0f ? 1f : 0f;
            if (input.GetButtonDown(m_VerticalAxis)) move.y = move.y < 0f ? -1f : move.y > 0f ? 1f : 0f;
            return move;
        }

        /// A held direction moves once, again after repeatDelay, then inputActionsPerSecond times
        /// a second; a new direction moves at once (within the action rate).
        protected bool SendMoveEventToSelectedObject()
        {
            float now = Time.unscaledTime;
            var move = RawMove();
            if (Mathf.Approximately(move.x, 0f) && Mathf.Approximately(move.y, 0f))
            {
                repeatedMoves = 0;
                return false;
            }
            bool sameDirection = Vector2.Dot(move, lastMove) > 0f;
            float wait = sameDirection && repeatedMoves == 1 ? m_RepeatDelay : 1f / m_InputActionsPerSecond;
            if (now <= lastMoveTime + wait) return false;

            var data = GetAxisEventData(move.x, move.y, 0.6f);
            if (data.moveDir == MoveDirection.None)
            {
                repeatedMoves = 0;
                return data.used;
            }
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.moveHandler);
            if (!sameDirection) repeatedMoves = 0;
            repeatedMoves++;
            lastMoveTime = now;
            lastMove = move;
            return data.used;
        }

        protected void ProcessMouseEvent() => ProcessMouseEvent(0);

        [System.Obsolete("This method is no longer checked, overriding it with return true does nothing!")]
        protected virtual bool ForceAutoSelect() => false;

        /// Left: press and release, move, drag; right and middle: press and release, drag; then
        /// scrolling to the handler under the pointer.
        protected void ProcessMouseEvent(int id)
        {
            var state = GetMousePointerEventData(id);
            var left = state.GetButtonState(PointerEventData.InputButton.Left).eventData;
            focused = left.buttonData.pointerCurrentRaycast.gameObject;

            ProcessMousePress(left);
            ProcessMove(left.buttonData);
            ProcessDrag(left.buttonData);

            var right = state.GetButtonState(PointerEventData.InputButton.Right).eventData;
            ProcessMousePress(right);
            ProcessDrag(right.buttonData);
            var middle = state.GetButtonState(PointerEventData.InputButton.Middle).eventData;
            ProcessMousePress(middle);
            ProcessDrag(middle.buttonData);

            if (!Mathf.Approximately(left.buttonData.scrollDelta.sqrMagnitude, 0f))
            {
                var handler = ExecuteEvents.GetEventHandler<IScrollHandler>(left.buttonData.pointerCurrentRaycast.gameObject);
                ExecuteEvents.ExecuteHierarchy(handler, left.buttonData, ExecuteEvents.scrollHandler);
            }
        }

        protected bool SendUpdateEventToSelectedObject()
        {
            if (eventSystem.currentSelectedGameObject == null) return false;
            var data = GetBaseEventData();
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.updateSelectedHandler);
            return data.used;
        }

        protected void ProcessMousePress(MouseButtonEventData data)
        {
            var pointerEvent = data.buttonData;
            var over = pointerEvent.pointerCurrentRaycast.gameObject;
            if (data.PressedThisFrame())
            {
                Press(pointerEvent, over);
                lastPointerEvent = pointerEvent;
            }
            if (data.ReleasedThisFrame()) ReleaseMouse(pointerEvent, over);
        }

        /// Up to the press target, click when released over its click handler (otherwise drop
        /// when dragging), end of drag; then hover catches up with the pointer.
        void ReleaseMouse(PointerEventData pointerEvent, GameObject over)
        {
            ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
            if (pointerEvent.pointerPress == clickHandler && pointerEvent.eligibleForClick)
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
            else if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.ExecuteHierarchy(over, pointerEvent, ExecuteEvents.dropHandler);

            if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);

            pointerEvent.eligibleForClick = false;
            pointerEvent.pointerPress = null;
            pointerEvent.rawPointerPress = null;
            pointerEvent.pointerClick = null;
            pointerEvent.dragging = false;
            pointerEvent.pointerDrag = null;

            if (over != pointerEvent.pointerEnter)
            {
                HandlePointerExitAndEnter(pointerEvent, null);
                HandlePointerExitAndEnter(pointerEvent, over);
            }
            lastPointerEvent = pointerEvent;
        }

        protected GameObject GetCurrentFocusedGameObject() => focused;
    }
}
