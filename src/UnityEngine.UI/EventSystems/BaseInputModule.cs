using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// An input source driven by its EventSystem: the system ticks every module, activates one and
    /// lets it process the frame's input.
    public abstract partial class BaseInputModule
    {
        [NonSerialized]
        protected List<RaycastResult> m_RaycastResultCache = new List<RaycastResult>();

        AxisEventData axisData;
        BaseEventData baseData;
        EventSystem system;
        BaseInput defaultInput;
        BaseInput overrideInput;

        protected BaseInputModule()
        {
            m_SendPointerHoverToParent = true;
        }

        /// The input the module reads: the override, or a BaseInput on the GameObject (added when
        /// there is none).
        public BaseInput input
        {
            get
            {
                if (overrideInput != null) return overrideInput;
                if (defaultInput == null)
                {
                    foreach (var candidate in GetComponents<BaseInput>())
                        if (candidate != null && candidate.GetType() == typeof(BaseInput))
                        {
                            defaultInput = candidate;
                            break;
                        }
                    if (defaultInput == null) defaultInput = gameObject.AddComponent<BaseInput>();
                }
                return defaultInput;
            }
        }

        public BaseInput inputOverride
        {
            get => overrideInput;
            set => overrideInput = value;
        }

        public bool sendPointerHoverToParent
        {
            get => m_SendPointerHoverToParent;
            set => m_SendPointerHoverToParent = value;
        }

        protected EventSystem eventSystem => system;

        protected override void OnEnable()
        {
            base.OnEnable();
            system = GetComponent<EventSystem>();
            system.UpdateModules();
        }

        protected override void OnDisable()
        {
            system.UpdateModules();
            base.OnDisable();
        }

        /// The first result that hit a GameObject, or an empty result.
        protected static RaycastResult FindFirstRaycast(List<RaycastResult> candidates)
        {
            int n = candidates.Count;
            for (int i = 0; i < n; i++)
                if (candidates[i].gameObject != null) return candidates[i];
            return new RaycastResult();
        }

        protected static MoveDirection DetermineMoveDirection(float x, float y) => DetermineMoveDirection(x, y, 0.6f);

        /// The dominant axis of (x, y), or None inside the dead zone.
        protected static MoveDirection DetermineMoveDirection(float x, float y, float deadZone)
        {
            if (new Vector2(x, y).sqrMagnitude < deadZone * deadZone) return MoveDirection.None;
            if (Mathf.Abs(x) > Mathf.Abs(y)) return x > 0f ? MoveDirection.Right : MoveDirection.Left;
            return y > 0f ? MoveDirection.Up : MoveDirection.Down;
        }

        /// The nearest object that is g1 or one of its ancestors and also g2 or one of its ancestors.
        protected static GameObject FindCommonRoot(GameObject g1, GameObject g2)
        {
            if (g1 == null || g2 == null) return null;
            for (var a = g1.transform; a != null; a = a.parent)
                for (var b = g2.transform; b != null; b = b.parent)
                    if (a == b) return a.gameObject;
            return null;
        }

        /// Sends exit to the objects the pointer left and enter to those it entered, below their
        /// common ancestor, keeping the pointer's hovered list and pointerEnter up to date. Pointer
        /// move goes to each object before its exit and after its enter (whether or not the
        /// pointer moved), and to the hovered objects while the pointer moves over the same
        /// target. Each event goes to the object itself (Execute, not ExecuteHierarchy).
        protected void HandlePointerExitAndEnter(PointerEventData currentPointerData, GameObject newEnterTarget)
        {
            var hovered = currentPointerData.hovered;
            if (newEnterTarget == null || currentPointerData.pointerEnter == null)
            {
                // Everything hovered is left; the list is cleared once all have been told.
                for (int i = 0; i < hovered.Count; i++)
                {
                    ExecuteEvents.Execute(hovered[i], currentPointerData, ExecuteEvents.pointerMoveHandler);
                    ExecuteEvents.Execute(hovered[i], currentPointerData, ExecuteEvents.pointerExitHandler);
                }
                hovered.Clear();
                if (newEnterTarget == null)
                {
                    currentPointerData.pointerEnter = null;
                    return;
                }
            }

            if (currentPointerData.pointerEnter == newEnterTarget)
            {
                // The same target: moves only, and only while the pointer moves.
                if (currentPointerData.IsPointerMoving())
                    for (int i = 0; i < hovered.Count; i++)
                        ExecuteEvents.Execute(hovered[i], currentPointerData, ExecuteEvents.pointerMoveHandler);
                return;
            }

            var common = FindCommonRoot(currentPointerData.pointerEnter, newEnterTarget);
            var commonTransform = common == null ? null : common.transform;

            // Objects at or above the common root get nothing in this frame.
            if (currentPointerData.pointerEnter != null)
                for (var t = currentPointerData.pointerEnter.transform; t != null && t != commonTransform; t = t.parent)
                {
                    ExecuteEvents.Execute(t.gameObject, currentPointerData, ExecuteEvents.pointerMoveHandler);
                    ExecuteEvents.Execute(t.gameObject, currentPointerData, ExecuteEvents.pointerExitHandler);
                    hovered.Remove(t.gameObject);
                }

            currentPointerData.pointerEnter = newEnterTarget;
            for (var t = newEnterTarget.transform; t != null && t != commonTransform; t = t.parent)
            {
                ExecuteEvents.Execute(t.gameObject, currentPointerData, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(t.gameObject, currentPointerData, ExecuteEvents.pointerMoveHandler);
                hovered.Add(t.gameObject);
            }
        }

        protected virtual AxisEventData GetAxisEventData(float x, float y, float moveDeadZone)
        {
            axisData ??= new AxisEventData(eventSystem);
            axisData.Reset();
            axisData.moveVector = new Vector2(x, y);
            axisData.moveDir = DetermineMoveDirection(x, y, moveDeadZone);
            return axisData;
        }

        protected virtual BaseEventData GetBaseEventData()
        {
            baseData ??= new BaseEventData(eventSystem);
            baseData.Reset();
            return baseData;
        }

        public virtual bool IsPointerOverGameObject(int pointerId) => false;

        public virtual bool ShouldActivateModule() => enabled && gameObject.activeInHierarchy;

        public virtual void DeactivateModule() { }

        public virtual void ActivateModule() { }

        public virtual void UpdateModule() { }

        public virtual bool IsModuleSupported() => true;
    }
}
