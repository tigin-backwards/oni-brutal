using System.Collections.Generic;
using System.Text;

namespace UnityEngine.EventSystems
{
    public enum MoveDirection
    {
        Left = 0,
        Up = 1,
        Right = 2,
        Down = 3,
        None = 4,
    }

    public abstract partial class AbstractEventData
    {
        bool consumed;

        protected AbstractEventData() { }

        public virtual void Reset() => consumed = false;

        /// Marks the event as handled: input modules stop passing it on.
        public virtual void Use() => consumed = true;

        public virtual bool used => consumed;
    }

    public partial class BaseEventData
    {
        readonly EventSystem system;

        public BaseEventData(EventSystem eventSystem) => system = eventSystem;

        public BaseInputModule currentInputModule => system.currentInputModule;

        public GameObject selectedObject
        {
            get => system.currentSelectedGameObject;
            set => system.SetSelectedGameObject(value, this);
        }
    }

    public partial class AxisEventData
    {
        public AxisEventData(EventSystem eventSystem) : base(eventSystem)
        {
            moveVector = Vector2.zero;
            moveDir = MoveDirection.None;
        }

        public Vector2 moveVector { get; set; }
        public MoveDirection moveDir { get; set; }
    }

    public partial class PointerEventData
    {
        GameObject press;

        public PointerEventData(EventSystem eventSystem) : base(eventSystem)
        {
            eligibleForClick = false;
            pointerId = -1;
            position = Vector2.zero;
            delta = Vector2.zero;
            pressPosition = Vector2.zero;
            clickTime = 0f;
            clickCount = 0;
            scrollDelta = Vector2.zero;
            useDragThreshold = true;
            dragging = false;
            button = InputButton.Left;
            pressure = 0f;
            tangentialPressure = 0f;
            altitudeAngle = 0f;
            azimuthAngle = 0f;
            twist = 0f;
            radius = Vector2.zero;
            radiusVariance = Vector2.zero;
            hovered = new List<GameObject>();
        }

        public GameObject pointerEnter { get; set; }

        /// The object that received the last press, whichever press this is.
        public GameObject lastPress { get; private set; }

        public GameObject rawPointerPress { get; set; }
        public GameObject pointerDrag { get; set; }
        public GameObject pointerClick { get; set; }
        public RaycastResult pointerCurrentRaycast { get; set; }
        public RaycastResult pointerPressRaycast { get; set; }
        public bool eligibleForClick { get; set; }
        public int displayIndex { get; set; }
        public int pointerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }

        [System.Obsolete("Use either pointerCurrentRaycast.worldPosition or pointerPressRaycast.worldPosition")]
        public Vector3 worldPosition { get; set; }

        [System.Obsolete("Use either pointerCurrentRaycast.worldNormal or pointerPressRaycast.worldNormal")]
        public Vector3 worldNormal { get; set; }

        public float clickTime { get; set; }
        public int clickCount { get; set; }
        public Vector2 scrollDelta { get; set; }
        public bool useDragThreshold { get; set; }
        public bool dragging { get; set; }
        public InputButton button { get; set; }
        public float pressure { get; set; }
        public float tangentialPressure { get; set; }
        public float altitudeAngle { get; set; }
        public float azimuthAngle { get; set; }
        public float twist { get; set; }
        public Vector2 radius { get; set; }
        public Vector2 radiusVariance { get; set; }
        public bool fullyExited { get; set; }
        public bool reentered { get; set; }

        public bool IsPointerMoving() => delta.sqrMagnitude > 0f;

        public bool IsScrolling() => scrollDelta.sqrMagnitude > 0f;

        public Camera enterEventCamera => pointerCurrentRaycast.module == null ? null : pointerCurrentRaycast.module.eventCamera;

        public Camera pressEventCamera => pointerPressRaycast.module == null ? null : pointerPressRaycast.module.eventCamera;

        /// Setting a new press target remembers the previous one as lastPress.
        public GameObject pointerPress
        {
            get => press;
            set
            {
                if (press == value) return;
                lastPress = press;
                press = value;
            }
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.AppendLine("<b>Position</b>: " + position);
            s.AppendLine("<b>delta</b>: " + delta);
            s.AppendLine("<b>eligibleForClick</b>: " + eligibleForClick);
            s.AppendLine("<b>pointerEnter</b>: " + pointerEnter);
            s.AppendLine("<b>pointerPress</b>: " + pointerPress);
            s.AppendLine("<b>lastPointerPress</b>: " + lastPress);
            s.AppendLine("<b>pointerDrag</b>: " + pointerDrag);
            s.AppendLine("<b>Use Drag Threshold</b>: " + useDragThreshold);
            s.AppendLine("<b>Current Raycast:</b>");
            s.AppendLine(pointerCurrentRaycast.ToString());
            s.AppendLine("<b>Press Raycast:</b>");
            s.AppendLine(pointerPressRaycast.ToString());
            s.AppendLine("<b>Display Index:</b>");
            s.AppendLine(displayIndex.ToString());
            s.AppendLine("<b>pressure</b>: " + pressure);
            s.AppendLine("<b>tangentialPressure</b>: " + tangentialPressure);
            s.AppendLine("<b>altitudeAngle</b>: " + altitudeAngle);
            s.AppendLine("<b>azimuthAngle</b>: " + azimuthAngle);
            s.AppendLine("<b>twist</b>: " + twist);
            s.AppendLine("<b>radius</b>: " + radius);
            s.AppendLine("<b>radiusVariance</b>: " + radiusVariance);
            return s.ToString();
        }
    }

    // The hit object is the struct's one private field; field order across the partial
    // declarations is irrelevant (the struct is never marshalled).
#pragma warning disable CS0282
    public partial struct RaycastResult
#pragma warning restore CS0282
    {
        GameObject target;

        public GameObject gameObject
        {
            get => target;
            set => target = value;
        }

        public bool isValid => module != null && gameObject != null;

        public void Clear()
        {
            this = default;
        }

        public override string ToString()
        {
            if (!isValid) return "";
            return "Name: " + gameObject + "\n" +
                   "module: " + module + "\n" +
                   "distance: " + distance + "\n" +
                   "index: " + index + "\n" +
                   "depth: " + depth + "\n" +
                   "worldNormal: " + worldNormal + "\n" +
                   "worldPosition: " + worldPosition + "\n" +
                   "screenPosition: " + screenPosition + "\n" +
                   "module.sortOrderPriority: " + module.sortOrderPriority + "\n" +
                   "module.renderOrderPriority: " + module.renderOrderPriority + "\n" +
                   "sortingLayer: " + sortingLayer + "\n" +
                   "sortingOrder: " + sortingOrder;
        }
    }
}
