using System.Collections;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// Raises onClick for a left click or a submit.
    public partial class Button
    {
        protected Button()
        {
            m_OnClick = new ButtonClickedEvent();
        }

        public ButtonClickedEvent onClick
        {
            get => m_OnClick;
            set => m_OnClick = value;
        }

        void Press()
        {
            if (!IsActive() || !IsInteractable()) return;
            UISystemProfilerApi.AddMarker("Button.onClick", this);
            m_OnClick.Invoke();
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        /// Clicks, and shows the pressed state for one fade before returning to the current one.
        public virtual void OnSubmit(BaseEventData eventData)
        {
            Press();
            if (!IsActive() || !IsInteractable()) return;
            DoStateTransition(SelectionState.Pressed, false);
            StartCoroutine(FinishSubmit());
        }

        IEnumerator FinishSubmit()
        {
            float duration = colors.fadeDuration, elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            DoStateTransition(currentSelectionState, false);
        }

        public partial class ButtonClickedEvent
        {
            public ButtonClickedEvent() { }
        }
    }
}
