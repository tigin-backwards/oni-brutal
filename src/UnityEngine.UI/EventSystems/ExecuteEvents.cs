using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    /// Sends an event to the handlers on a GameObject, or on the first GameObject up its
    /// hierarchy that handles it.
    public static partial class ExecuteEvents
    {
        public static T ValidateEventData<T>(BaseEventData data) where T : class
        {
            if (data is not T typed) throw new ArgumentException($"Invalid type: {data.GetType()} passed to event expecting {typeof(T)}");
            return typed;
        }

        static readonly EventFunction<IPointerEnterHandler> pointerEnter = (h, d) => h.OnPointerEnter(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IPointerExitHandler> pointerExit = (h, d) => h.OnPointerExit(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IPointerMoveHandler> pointerMove = (h, d) => h.OnPointerMove(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IPointerDownHandler> pointerDown = (h, d) => h.OnPointerDown(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IPointerUpHandler> pointerUp = (h, d) => h.OnPointerUp(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IPointerClickHandler> pointerClick = (h, d) => h.OnPointerClick(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IInitializePotentialDragHandler> potentialDrag = (h, d) => h.OnInitializePotentialDrag(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IBeginDragHandler> beginDrag = (h, d) => h.OnBeginDrag(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IDragHandler> drag = (h, d) => h.OnDrag(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IEndDragHandler> endDrag = (h, d) => h.OnEndDrag(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IDropHandler> drop = (h, d) => h.OnDrop(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IScrollHandler> scroll = (h, d) => h.OnScroll(ValidateEventData<PointerEventData>(d));
        static readonly EventFunction<IUpdateSelectedHandler> updateSelected = (h, d) => h.OnUpdateSelected(d);
        static readonly EventFunction<ISelectHandler> select = (h, d) => h.OnSelect(d);
        static readonly EventFunction<IDeselectHandler> deselect = (h, d) => h.OnDeselect(d);
        static readonly EventFunction<IMoveHandler> move = (h, d) => h.OnMove(ValidateEventData<AxisEventData>(d));
        static readonly EventFunction<ISubmitHandler> submit = (h, d) => h.OnSubmit(d);
        static readonly EventFunction<ICancelHandler> cancel = (h, d) => h.OnCancel(d);

        public static EventFunction<IPointerEnterHandler> pointerEnterHandler => pointerEnter;
        public static EventFunction<IPointerExitHandler> pointerExitHandler => pointerExit;
        public static EventFunction<IPointerDownHandler> pointerDownHandler => pointerDown;
        public static EventFunction<IPointerUpHandler> pointerUpHandler => pointerUp;
        public static EventFunction<IPointerClickHandler> pointerClickHandler => pointerClick;
        public static EventFunction<IInitializePotentialDragHandler> initializePotentialDrag => potentialDrag;
        public static EventFunction<IBeginDragHandler> beginDragHandler => beginDrag;
        public static EventFunction<IDragHandler> dragHandler => drag;
        public static EventFunction<IEndDragHandler> endDragHandler => endDrag;
        public static EventFunction<IDropHandler> dropHandler => drop;
        public static EventFunction<IScrollHandler> scrollHandler => scroll;
        public static EventFunction<IUpdateSelectedHandler> updateSelectedHandler => updateSelected;
        public static EventFunction<ISelectHandler> selectHandler => select;
        public static EventFunction<IDeselectHandler> deselectHandler => deselect;
        public static EventFunction<IMoveHandler> moveHandler => move;
        public static EventFunction<IPointerMoveHandler> pointerMoveHandler => pointerMove;
        public static EventFunction<ISubmitHandler> submitHandler => submit;
        public static EventFunction<ICancelHandler> cancelHandler => cancel;

        /// Lists reused across calls; a handler may send further events, so each nesting level
        /// takes its own list.
        static readonly Stack<List<Component>> spareComponentLists = new();
        static readonly Stack<List<Transform>> spareTransformLists = new();

        /// A handler counts when it is a component other than a Behaviour, or an active and
        /// enabled Behaviour.
        static bool Receives(Component c) => c is not Behaviour b || b.isActiveAndEnabled;

        static List<Component> Handlers<T>(GameObject go) where T : IEventSystemHandler
        {
            var list = spareComponentLists.Count > 0 ? spareComponentLists.Pop() : new List<Component>();
            list.Clear();
            if (go == null || !go.activeInHierarchy) return list;
            var all = spareComponentLists.Count > 0 ? spareComponentLists.Pop() : new List<Component>();
            go.GetComponents(all);
            foreach (var c in all)
                if (c is T && Receives(c)) list.Add(c);
            all.Clear();
            spareComponentLists.Push(all);
            return list;
        }

        static void Release(List<Component> list)
        {
            list.Clear();
            spareComponentLists.Push(list);
        }

        /// Calls the function on every handler of T on the object, in component order; a handler
        /// that throws is logged and the rest still run. True when any handler ran.
        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor) where T : IEventSystemHandler
        {
            var handlers = Handlers<T>(target);
            int count = handlers.Count;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    T handler;
                    try { handler = (T)(object)handlers[i]; }
                    catch (Exception e) { Debug.LogException(e); continue; }
                    try { functor(handler, eventData); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            finally { Release(handlers); }
            return count > 0;
        }

        /// The event goes to the first object from the root upwards that handles it; returns
        /// that object, or null.
        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> callbackFunction) where T : IEventSystemHandler
        {
            if (root == null) return null;
            var chain = spareTransformLists.Count > 0 ? spareTransformLists.Pop() : new List<Transform>();
            chain.Clear();
            for (var t = root.transform; t != null; t = t.parent) chain.Add(t);
            try
            {
                foreach (var t in chain)
                {
                    var go = t.gameObject;
                    if (Execute(go, eventData, callbackFunction)) return go;
                }
                return null;
            }
            finally
            {
                chain.Clear();
                spareTransformLists.Push(chain);
            }
        }

        public static bool CanHandleEvent<T>(GameObject go) where T : IEventSystemHandler
        {
            var handlers = Handlers<T>(go);
            bool any = handlers.Count > 0;
            Release(handlers);
            return any;
        }

        /// The first object from root upwards that has a handler of T.
        public static GameObject GetEventHandler<T>(GameObject root) where T : IEventSystemHandler
        {
            if (root == null) return null;
            for (var t = root.transform; t != null; t = t.parent)
                if (CanHandleEvent<T>(t.gameObject)) return t.gameObject;
            return null;
        }
    }
}
