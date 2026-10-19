using UnityEngine;
using UnityEngine.EventSystems;

namespace TMPro
{
    // Tracks whether an input field's scrollbar is selected (clicked or focused).
    public partial class TMP_ScrollbarEventHandler
    {
        // A click needs no handling of its own: the selection events track the state.
        public void OnPointerClick(PointerEventData eventData) { }

        public void OnSelect(BaseEventData eventData) => isSelected = true;

        public void OnDeselect(BaseEventData eventData) => isSelected = false;
    }
}
