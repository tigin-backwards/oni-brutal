using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// Forwards every event-system event it receives to the UnityEvents registered for its type.
    public partial class EventTrigger
    {
        protected EventTrigger() { }

        public List<Entry> triggers
        {
            get => m_Delegates ??= new List<Entry>();
            set => m_Delegates = value;
        }

        [System.Obsolete("Please use triggers instead (UnityUpgradable) -> triggers", true)]
        public List<Entry> delegates
        {
            get => triggers;
            set => triggers = value;
        }

        void Raise(EventTriggerType id, BaseEventData eventData)
        {
            var entries = triggers;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.eventID == id && e.callback != null) e.callback.Invoke(eventData);
            }
        }

        public virtual void OnPointerEnter(PointerEventData eventData) => Raise(EventTriggerType.PointerEnter, eventData);
        public virtual void OnPointerExit(PointerEventData eventData) => Raise(EventTriggerType.PointerExit, eventData);
        public virtual void OnDrag(PointerEventData eventData) => Raise(EventTriggerType.Drag, eventData);
        public virtual void OnDrop(PointerEventData eventData) => Raise(EventTriggerType.Drop, eventData);
        public virtual void OnPointerDown(PointerEventData eventData) => Raise(EventTriggerType.PointerDown, eventData);
        public virtual void OnPointerUp(PointerEventData eventData) => Raise(EventTriggerType.PointerUp, eventData);
        public virtual void OnPointerClick(PointerEventData eventData) => Raise(EventTriggerType.PointerClick, eventData);
        public virtual void OnSelect(BaseEventData eventData) => Raise(EventTriggerType.Select, eventData);
        public virtual void OnDeselect(BaseEventData eventData) => Raise(EventTriggerType.Deselect, eventData);
        public virtual void OnScroll(PointerEventData eventData) => Raise(EventTriggerType.Scroll, eventData);
        public virtual void OnMove(AxisEventData eventData) => Raise(EventTriggerType.Move, eventData);
        public virtual void OnUpdateSelected(BaseEventData eventData) => Raise(EventTriggerType.UpdateSelected, eventData);
        public virtual void OnInitializePotentialDrag(PointerEventData eventData) => Raise(EventTriggerType.InitializePotentialDrag, eventData);
        public virtual void OnBeginDrag(PointerEventData eventData) => Raise(EventTriggerType.BeginDrag, eventData);
        public virtual void OnEndDrag(PointerEventData eventData) => Raise(EventTriggerType.EndDrag, eventData);
        public virtual void OnSubmit(BaseEventData eventData) => Raise(EventTriggerType.Submit, eventData);
        public virtual void OnCancel(BaseEventData eventData) => Raise(EventTriggerType.Cancel, eventData);

        public partial class Entry
        {
            public Entry()
            {
                eventID = EventTriggerType.PointerClick;
                callback = new TriggerEvent();
            }
        }

        public partial class TriggerEvent
        {
            public TriggerEvent() { }
        }
    }
}
