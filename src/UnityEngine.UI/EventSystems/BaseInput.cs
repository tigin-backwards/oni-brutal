namespace UnityEngine.EventSystems
{
    /// The input modules' view of Unity's Input; a module's inputOverride replaces it.
    public class BaseInput : UIBehaviour
    {
        public virtual string compositionString => Input.compositionString;

        public virtual IMECompositionMode imeCompositionMode
        {
            get => Input.imeCompositionMode;
            set => Input.imeCompositionMode = value;
        }

        public virtual Vector2 compositionCursorPos
        {
            get => Input.compositionCursorPos;
            set => Input.compositionCursorPos = value;
        }

        public virtual bool mousePresent => Input.mousePresent;

        public virtual bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);

        public virtual bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);

        public virtual bool GetMouseButton(int button) => Input.GetMouseButton(button);

        public virtual Vector2 mousePosition => Input.mousePosition;

        public virtual Vector2 mouseScrollDelta => Input.mouseScrollDelta;

        public virtual bool touchSupported => Input.touchSupported;

        public virtual int touchCount => Input.touchCount;

        public virtual Touch GetTouch(int index) => Input.GetTouch(index);

        public virtual float GetAxisRaw(string axisName) => Input.GetAxisRaw(axisName);

        public virtual bool GetButtonDown(string buttonName) => Input.GetButtonDown(buttonName);
    }
}
