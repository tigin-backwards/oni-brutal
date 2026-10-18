using System.Collections.Generic;
using System.Text;

namespace UnityEngine.EventSystems
{
    /// Routes input to the UI: owns the input modules on its GameObject, runs the active one each
    /// frame, keeps the selection and raycasts through every active raycaster.
    public partial class EventSystem
    {
        /// Enabled systems, the most recently enabled (current) first.
        static readonly List<EventSystem> enabledSystems = new();

        readonly List<BaseInputModule> modules = new();
        BaseInputModule activeModule;
        GameObject selection;
        bool selecting;
        bool hasFocus = true;
        BaseEventData selectionEventData;

        protected EventSystem()
        {
            m_sendNavigationEvents = true;
            m_DragThreshold = 10;
        }

        public static EventSystem current
        {
            get => enabledSystems.Count > 0 ? enabledSystems[0] : null;
            set
            {
                int i = enabledSystems.IndexOf(value);
                if (i <= 0) return;
                enabledSystems.RemoveAt(i);
                enabledSystems.Insert(0, value);
            }
        }

        public bool sendNavigationEvents
        {
            get => m_sendNavigationEvents;
            set => m_sendNavigationEvents = value;
        }

        public int pixelDragThreshold
        {
            get => m_DragThreshold;
            set => m_DragThreshold = value;
        }

        public BaseInputModule currentInputModule => activeModule;

        public GameObject firstSelectedGameObject
        {
            get => m_FirstSelected;
            set => m_FirstSelected = value;
        }

        public GameObject currentSelectedGameObject => selection;

        [System.Obsolete("lastSelectedGameObject is no longer supported")]
        public GameObject lastSelectedGameObject => null;

        public bool isFocused => hasFocus;

        /// True while SetSelectedGameObject is sending its deselect and select events.
        public bool alreadySelecting => selecting;

        /// Re-reads the input modules on the GameObject, in component order, keeping the active ones.
        public void UpdateModules()
        {
            GetComponents(modules);
            for (int i = modules.Count - 1; i >= 0; i--)
                if (modules[i] == null || !modules[i].IsActive()) modules.RemoveAt(i);
        }

        BaseEventData SelectionEventData
        {
            get
            {
                selectionEventData ??= new BaseEventData(this);
                selectionEventData.Reset();
                return selectionEventData;
            }
        }

        /// Deselects the current object and selects the new one; selecting the current object again
        /// does nothing.
        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer)
        {
            if (selecting)
            {
                Debug.LogError("Attempting to select " + selected + "while already selecting an object.");
                return;
            }
            selecting = true;
            try
            {
                if (selected == selection) return;
                ExecuteEvents.Execute(selection, pointer, ExecuteEvents.deselectHandler);
                selection = selected;
                ExecuteEvents.Execute(selection, pointer, ExecuteEvents.selectHandler);
            }
            finally { selecting = false; }
        }

        public void SetSelectedGameObject(GameObject selected) => SetSelectedGameObject(selected, SelectionEventData);

        /// Hits from every active raycaster, best first.
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults)
        {
            raycastResults.Clear();
            var raycasters = RaycasterRegistry.Active;
            for (int i = 0; i < raycasters.Count; i++)
            {
                var r = raycasters[i];
                if (r == null || !r.IsActive()) continue;
                r.Raycast(eventData, raycastResults);
            }
            raycastResults.Sort(CompareHits);
        }

        static int CompareHits(RaycastResult lhs, RaycastResult rhs)
        {
            if (lhs.module != rhs.module)
            {
                var lc = lhs.module.eventCamera;
                var rc = rhs.module.eventCamera;
                if (lc != null && rc != null && lc.depth != rc.depth) return rc.depth.CompareTo(lc.depth);
                if (lhs.module.sortOrderPriority != rhs.module.sortOrderPriority) return rhs.module.sortOrderPriority.CompareTo(lhs.module.sortOrderPriority);
                if (lhs.module.renderOrderPriority != rhs.module.renderOrderPriority) return rhs.module.renderOrderPriority.CompareTo(lhs.module.renderOrderPriority);
            }
            if (lhs.sortingLayer != rhs.sortingLayer)
            {
                int l = SortingLayer.GetLayerValueFromID(lhs.sortingLayer);
                int r = SortingLayer.GetLayerValueFromID(rhs.sortingLayer);
                if (l != r) return r.CompareTo(l);
            }
            if (lhs.sortingOrder != rhs.sortingOrder) return rhs.sortingOrder.CompareTo(lhs.sortingOrder);
            if (lhs.depth != rhs.depth) return rhs.depth.CompareTo(lhs.depth);
            if (lhs.distance != rhs.distance) return lhs.distance.CompareTo(rhs.distance);
            return lhs.index.CompareTo(rhs.index);
        }

        public bool IsPointerOverGameObject() => IsPointerOverGameObject(PointerInputModule.kMouseLeftId);

        public bool IsPointerOverGameObject(int pointerId) => activeModule != null && activeModule.IsPointerOverGameObject(pointerId);

        protected override void OnEnable()
        {
            base.OnEnable();
            enabledSystems.Remove(this);
            enabledSystems.Insert(0, this);
        }

        protected override void OnDisable()
        {
            if (activeModule != null)
            {
                activeModule.DeactivateModule();
                activeModule = null;
            }
            enabledSystems.Remove(this);
            base.OnDisable();
        }

        protected override void Start()
        {
            base.Start();
            if (m_FirstSelected != null) SetSelectedGameObject(m_FirstSelected);
        }

        protected virtual void OnApplicationFocus(bool hasFocus) => this.hasFocus = hasFocus;

        /// Every module ticks; the first supported module that wants to run becomes active, and the
        /// active module processes the frame's input. Only the current system processes.
        protected virtual void Update()
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] != null) modules[i].UpdateModule();
            if (current != this) return;

            bool changed = false;
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                if (m == null || !m.IsModuleSupported() || !m.ShouldActivateModule()) continue;
                if (activeModule != m) ChangeActiveModule(m);
                changed = true;
                break;
            }
            // With no module asking to run, the first supported one is taken.
            if (!changed && activeModule == null)
                for (int i = 0; i < modules.Count; i++)
                {
                    var m = modules[i];
                    if (m == null || !m.IsModuleSupported()) continue;
                    ChangeActiveModule(m);
                    break;
                }

            if (activeModule != null) activeModule.Process();
        }

        void ChangeActiveModule(BaseInputModule module)
        {
            if (activeModule == module) return;
            if (activeModule != null) activeModule.DeactivateModule();
            if (module != null) module.ActivateModule();
            activeModule = module;
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.AppendLine("<b>Selected:</b>" + currentSelectedGameObject);
            s.AppendLine();
            s.AppendLine();
            s.AppendLine(activeModule != null ? activeModule.ToString() : "No module");
            return s.ToString();
        }
    }

    /// The enabled raycasters, in the order they were enabled.
    internal static class RaycasterRegistry
    {
        static readonly List<BaseRaycaster> raycasters = new();

        public static List<BaseRaycaster> Active => raycasters;

        public static void Add(BaseRaycaster raycaster)
        {
            if (!raycasters.Contains(raycaster)) raycasters.Add(raycaster);
        }

        public static void Remove(BaseRaycaster raycaster) => raycasters.Remove(raycaster);
    }
}
