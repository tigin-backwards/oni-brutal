using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// The base of the interactive controls: interactability (with CanvasGroups), the selection
    /// state and its transition (tint, sprite swap or animation), and navigation.
    public partial class Selectable
    {
        /// The enabled selectables; each knows its slot, and removal moves the last one into it.
        static Selectable[] registered = new Selectable[10];
        static int registeredCount;

        [NonSerialized] int slot = -1;
        [NonSerialized] bool registeredSelf;
        [NonSerialized] bool groupsAllowInteraction = true;
        readonly List<CanvasGroup> groupScratch = new();

        protected Selectable()
        {
            m_Navigation = Navigation.defaultNavigation;
            m_Transition = Transition.ColorTint;
            m_Colors = ColorBlock.defaultColorBlock;
            m_AnimationTriggers = new AnimationTriggers();
            m_Interactable = true;
        }

        protected enum SelectionState
        {
            Normal = 0,
            Highlighted = 1,
            Pressed = 2,
            Selected = 3,
            Disabled = 4,
        }

        public static Selectable[] allSelectablesArray
        {
            get
            {
                var copy = new Selectable[registeredCount];
                Array.Copy(registered, copy, registeredCount);
                return copy;
            }
        }

        public static int allSelectableCount => registeredCount;

        [Obsolete("Replaced with allSelectablesArray to have better performance when disabling a element", false)]
        public static List<Selectable> allSelectables => new List<Selectable>(allSelectablesArray);

        /// Copies as many enabled selectables as fit; returns how many.
        public static int AllSelectablesNoAlloc(Selectable[] selectables)
        {
            int n = Math.Min(selectables.Length, registeredCount);
            Array.Copy(registered, selectables, n);
            return n;
        }

        public Navigation navigation
        {
            get => m_Navigation;
            set { if (m_Navigation.Equals(value) && m_Navigation.wrapAround == value.wrapAround) return; m_Navigation = value; OnSetProperty(); }
        }

        public Transition transition
        {
            get => m_Transition;
            set { if (m_Transition == value) return; m_Transition = value; OnSetProperty(); }
        }

        public ColorBlock colors
        {
            get => m_Colors;
            set { if (m_Colors.Equals(value)) return; m_Colors = value; OnSetProperty(); }
        }

        public SpriteState spriteState
        {
            get => m_SpriteState;
            set { if (m_SpriteState.Equals(value)) return; m_SpriteState = value; OnSetProperty(); }
        }

        public AnimationTriggers animationTriggers
        {
            get => m_AnimationTriggers;
            set { if (m_AnimationTriggers == value) return; m_AnimationTriggers = value; OnSetProperty(); }
        }

        public Graphic targetGraphic
        {
            get => m_TargetGraphic;
            set { if (m_TargetGraphic == value) return; m_TargetGraphic = value; OnSetProperty(); }
        }

        /// Turning interaction off drops the selection.
        public bool interactable
        {
            get => m_Interactable;
            set
            {
                if (m_Interactable == value) return;
                m_Interactable = value;
                if (!m_Interactable && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                OnSetProperty();
            }
        }

        protected bool isPointerInside { get; set; }
        protected bool isPointerDown { get; set; }
        protected bool hasSelection { get; set; }

        public Image image
        {
            get => m_TargetGraphic as Image;
            set => m_TargetGraphic = value;
        }

        public Animator animator => GetComponent<Animator>();

        protected override void Awake()
        {
            base.Awake();
            if (m_TargetGraphic == null) m_TargetGraphic = GetComponent<Graphic>();
        }

        protected override void OnCanvasGroupChanged()
        {
            bool allowed = ParentGroupAllowsInteraction();
            if (allowed == groupsAllowInteraction) return;
            groupsAllowInteraction = allowed;
            OnSetProperty();
        }

        /// Every enabled CanvasGroup above, up to one that ignores its parents, allows interaction.
        bool ParentGroupAllowsInteraction()
        {
            for (var t = transform; t != null; t = t.parent)
            {
                t.GetComponents(groupScratch);
                bool stop = false;
                foreach (var g in groupScratch)
                {
                    if (g.enabled && !g.interactable)
                    {
                        groupScratch.Clear();
                        return false;
                    }
                    if (g.ignoreParentGroups) stop = true;
                }
                groupScratch.Clear();
                if (stop) return true;
            }
            return true;
        }

        public virtual bool IsInteractable() => groupsAllowInteraction && m_Interactable;

        protected override void OnDidApplyAnimationProperties() => OnSetProperty();

        protected override void OnEnable()
        {
            if (registeredSelf) return;
            base.OnEnable();
            if (registeredCount == registered.Length) Array.Resize(ref registered, registered.Length * 2);
            slot = registeredCount;
            registered[registeredCount++] = this;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject) hasSelection = true;
            isPointerDown = false;
            groupsAllowInteraction = ParentGroupAllowsInteraction();
            DoStateTransition(currentSelectionState, true);
            registeredSelf = true;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            OnCanvasGroupChanged();
        }

        void OnSetProperty() => DoStateTransition(currentSelectionState, false);

        protected override void OnDisable()
        {
            if (!registeredSelf) return;
            registeredCount--;
            var last = registered[registeredCount];
            last.slot = slot;
            registered[slot] = last;
            registered[registeredCount] = null;
            InstantClearState();
            base.OnDisable();
            registeredSelf = false;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && IsPressed()) InstantClearState();
        }

        /// Disabled, then pressed, selected, highlighted, normal: the first that applies.
        protected SelectionState currentSelectionState
        {
            get
            {
                if (!IsInteractable()) return SelectionState.Disabled;
                if (isPointerDown) return SelectionState.Pressed;
                if (hasSelection) return SelectionState.Selected;
                if (isPointerInside) return SelectionState.Highlighted;
                return SelectionState.Normal;
            }
        }

        /// Forgets pointer and selection and shows the plain look at once.
        protected virtual void InstantClearState()
        {
            string trigger = m_AnimationTriggers.normalTrigger;
            isPointerInside = false;
            isPointerDown = false;
            hasSelection = false;
            switch (m_Transition)
            {
                case Transition.ColorTint: StartColorTween(Color.white, true); break;
                case Transition.SpriteSwap: DoSpriteSwap(null); break;
                case Transition.Animation: TriggerAnimation(trigger); break;
            }
        }

        protected virtual void DoStateTransition(SelectionState state, bool instant)
        {
            Color tint;
            Sprite sprite;
            string trigger;
            switch (state)
            {
                case SelectionState.Highlighted:
                    tint = m_Colors.highlightedColor; sprite = m_SpriteState.highlightedSprite; trigger = m_AnimationTriggers.highlightedTrigger;
                    break;
                case SelectionState.Pressed:
                    tint = m_Colors.pressedColor; sprite = m_SpriteState.pressedSprite; trigger = m_AnimationTriggers.pressedTrigger;
                    break;
                case SelectionState.Selected:
                    tint = m_Colors.selectedColor; sprite = m_SpriteState.selectedSprite; trigger = m_AnimationTriggers.selectedTrigger;
                    break;
                case SelectionState.Disabled:
                    tint = m_Colors.disabledColor; sprite = m_SpriteState.disabledSprite; trigger = m_AnimationTriggers.disabledTrigger;
                    break;
                default:
                    tint = m_Colors.normalColor; sprite = null; trigger = m_AnimationTriggers.normalTrigger;
                    break;
            }
            switch (m_Transition)
            {
                case Transition.ColorTint: StartColorTween(tint * m_Colors.colorMultiplier, instant); break;
                case Transition.SpriteSwap: DoSpriteSwap(sprite); break;
                case Transition.Animation: TriggerAnimation(trigger); break;
            }
        }

        void StartColorTween(Color target, bool instant)
        {
            if (m_TargetGraphic == null) return;
            m_TargetGraphic.CrossFadeColor(target, instant ? 0f : m_Colors.fadeDuration, true, true);
        }

        void DoSpriteSwap(Sprite sprite)
        {
            if (image == null) return;
            image.overrideSprite = sprite;
        }

        void TriggerAnimation(string trigger)
        {
            var a = animator;
            if (m_Transition != Transition.Animation || a == null || !a.isActiveAndEnabled || !a.hasBoundPlayables || string.IsNullOrEmpty(trigger)) return;
            a.ResetTrigger(m_AnimationTriggers.normalTrigger);
            a.ResetTrigger(m_AnimationTriggers.highlightedTrigger);
            a.ResetTrigger(m_AnimationTriggers.pressedTrigger);
            a.ResetTrigger(m_AnimationTriggers.selectedTrigger);
            a.ResetTrigger(m_AnimationTriggers.disabledTrigger);
            a.SetTrigger(trigger);
        }

        protected bool IsHighlighted()
        {
            if (!IsActive() || !IsInteractable()) return false;
            return isPointerInside && !isPointerDown && !hasSelection;
        }

        [Obsolete("Is Pressed no longer requires eventData", false)]
        protected bool IsPressed(BaseEventData eventData) => IsPressed();

        protected bool IsPressed()
        {
            if (!IsActive() || !IsInteractable()) return false;
            return isPointerDown;
        }

        void EvaluateAndTransitionToSelectionState()
        {
            if (!IsActive() || !IsInteractable()) return;
            DoStateTransition(currentSelectionState, false);
        }

        // ----- Navigation -----

        /// The enabled, interactable, navigable selectable that best lies in the direction: the
        /// largest cosine of the angle over the distance, from the edge of this rect towards the
        /// centre of the other. With wrap-around on a one-axis navigation, the farthest one
        /// behind when there is none ahead.
        public Selectable FindSelectable(Vector3 dir)
        {
            dir = dir.normalized;
            var localDir = Quaternion.Inverse(transform.rotation) * dir;
            var from = transform.TransformPoint(EdgePoint(transform as RectTransform, localDir));
            bool wrap = m_Navigation.wrapAround && (m_Navigation.mode == Navigation.Mode.Vertical || m_Navigation.mode == Navigation.Mode.Horizontal);
            float best = float.NegativeInfinity, bestBehind = float.NegativeInfinity;
            Selectable pick = null, pickBehind = null;
            for (int i = 0; i < registeredCount; i++)
            {
                var s = registered[i];
                if (s == this || s == null) continue;
                if (!s.IsInteractable() || s.navigation.mode == Navigation.Mode.None) continue;
                var r = s.transform as RectTransform;
                Vector3 centre = r != null ? (Vector3)r.rect.center : Vector3.zero;
                var to = s.transform.TransformPoint(centre) - from;
                float along = Vector3.Dot(dir, to);
                if (wrap && along < 0f)
                {
                    float behind = -along * to.sqrMagnitude;
                    if (behind > bestBehind)
                    {
                        bestBehind = behind;
                        pickBehind = s;
                    }
                    continue;
                }
                if (along <= 0f) continue;
                float score = along / to.sqrMagnitude;
                if (score > best)
                {
                    best = score;
                    pick = s;
                }
            }
            return wrap && pick == null ? pickBehind : pick;
        }

        /// The point where a ray from the rect's centre in the direction leaves the rect.
        static Vector3 EdgePoint(RectTransform rect, Vector2 dir)
        {
            if (rect == null) return Vector3.zero;
            if (dir != Vector2.zero) dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            var r = rect.rect;
            return r.center + Vector2.Scale(r.size, dir * 0.5f);
        }

        void Navigate(AxisEventData eventData, Selectable target)
        {
            if (target != null && target.IsActive()) eventData.selectedObject = target.gameObject;
        }

        public virtual Selectable FindSelectableOnLeft()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnLeft;
            if ((m_Navigation.mode & Navigation.Mode.Horizontal) != 0) return FindSelectable(transform.rotation * Vector3.left);
            return null;
        }

        public virtual Selectable FindSelectableOnRight()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnRight;
            if ((m_Navigation.mode & Navigation.Mode.Horizontal) != 0) return FindSelectable(transform.rotation * Vector3.right);
            return null;
        }

        public virtual Selectable FindSelectableOnUp()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnUp;
            if ((m_Navigation.mode & Navigation.Mode.Vertical) != 0) return FindSelectable(transform.rotation * Vector3.up);
            return null;
        }

        public virtual Selectable FindSelectableOnDown()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnDown;
            if ((m_Navigation.mode & Navigation.Mode.Vertical) != 0) return FindSelectable(transform.rotation * Vector3.down);
            return null;
        }

        public virtual void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Right: Navigate(eventData, FindSelectableOnRight()); break;
                case MoveDirection.Up: Navigate(eventData, FindSelectableOnUp()); break;
                case MoveDirection.Left: Navigate(eventData, FindSelectableOnLeft()); break;
                case MoveDirection.Down: Navigate(eventData, FindSelectableOnDown()); break;
            }
        }

        // ----- Pointer and selection -----

        /// A left press selects the selectable (when it navigates) and shows it pressed.
        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsInteractable() && navigation.mode != Navigation.Mode.None && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject, eventData);
            isPointerDown = true;
            EvaluateAndTransitionToSelectionState();
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            isPointerDown = false;
            EvaluateAndTransitionToSelectionState();
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            EvaluateAndTransitionToSelectionState();
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            EvaluateAndTransitionToSelectionState();
        }

        public virtual void OnSelect(BaseEventData eventData)
        {
            hasSelection = true;
            EvaluateAndTransitionToSelectionState();
        }

        public virtual void OnDeselect(BaseEventData eventData)
        {
            hasSelection = false;
            EvaluateAndTransitionToSelectionState();
        }

        public virtual void Select()
        {
            if (EventSystem.current == null || EventSystem.current.alreadySelecting) return;
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
