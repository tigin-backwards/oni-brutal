using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

namespace OniMoono.Tests.UGui
{
    /// Records the events it gets.
    public class EventLog : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, ISelectHandler, IDeselectHandler
    {
        public static readonly List<string> Log = new();
        public bool Throw;

        void Add(string e)
        {
            Log.Add(name + ":" + e);
            if (Throw) throw new InvalidOperationException("handler failed");
        }

        public void OnPointerEnter(PointerEventData eventData) => Add("enter");
        public void OnPointerExit(PointerEventData eventData) => Add("exit");
        public void OnPointerDown(PointerEventData eventData) => Add("down");
        public void OnPointerUp(PointerEventData eventData) => Add("up");
        public void OnPointerClick(PointerEventData eventData) => Add("click" + eventData.clickCount);
        public void OnBeginDrag(PointerEventData eventData) => Add("beginDrag");
        public void OnDrag(PointerEventData eventData) => Add("drag");
        public void OnEndDrag(PointerEventData eventData) => Add("endDrag");
        public void OnScroll(PointerEventData eventData) => Add("scroll");
        public void OnSelect(BaseEventData eventData) => Add("select");
        public void OnDeselect(BaseEventData eventData) => Add("deselect");
    }

    public class PressLog : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public void OnPointerDown(PointerEventData eventData) => EventLog.Log.Add(name + ":down");
        public void OnPointerUp(PointerEventData eventData) => EventLog.Log.Add(name + ":up");
        public void OnPointerClick(PointerEventData eventData) => EventLog.Log.Add(name + ":click");
    }

    public class DragLog : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public void OnBeginDrag(PointerEventData eventData) => EventLog.Log.Add(name + ":beginDrag");
        public void OnDrag(PointerEventData eventData) => EventLog.Log.Add(name + ":drag");
        public void OnEndDrag(PointerEventData eventData) => EventLog.Log.Add(name + ":endDrag");
    }

    /// Pointer input set by the test.
    public class TestInput : BaseInput
    {
        public Vector2 Mouse;
        public Vector2 Scroll;
        public bool Down, Up, Held;

        public override bool mousePresent => true;
        public override Vector2 mousePosition => Mouse;
        public override Vector2 mouseScrollDelta => Scroll;
        public override bool GetMouseButtonDown(int button) => button == 0 && Down;
        public override bool GetMouseButtonUp(int button) => button == 0 && Up;
        public override bool GetMouseButton(int button) => button == 0 && Held;
        public override float GetAxisRaw(string axisName) => 0f;
        public override bool GetButtonDown(string buttonName) => false;
        public override bool touchSupported => false;
        public override int touchCount => 0;
    }

    public class ExecuteEventsTests : UGuiTest
    {
        [Fact]
        public void HandlersRunInComponentOrderSkippingDisabledAndSurvivingExceptions()
        {
            EventLog.Log.Clear();
            var go = new GameObject("Target");
            var a = go.AddComponent<EventLog>();
            var b = go.AddComponent<EventLog>();
            var c = go.AddComponent<EventLog>();
            a.Throw = true;
            b.enabled = false;
            int errors = 0;
            Application.LogCallback count = (_, _, type) => { if (type == LogType.Exception) errors++; };
            Application.logMessageReceived += count;
            try
            {
                Assert.True(ExecuteEvents.Execute(go, new PointerEventData(null), ExecuteEvents.pointerDownHandler));
            }
            finally { Application.logMessageReceived -= count; }
            Assert.Equal(new[] { "Target:down", "Target:down" }, EventLog.Log);
            Assert.Equal(1, errors);
            Assert.False(ExecuteEvents.Execute(go, new PointerEventData(null), ExecuteEvents.submitHandler));
        }

        [Fact]
        public void HierarchyGoesUpToTheFirstHandler()
        {
            EventLog.Log.Clear();
            var root = new GameObject("Root");
            root.AddComponent<EventLog>();
            var middle = new GameObject("Middle");
            middle.transform.SetParent(root.transform);
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(middle.transform);
            Assert.Same(root, ExecuteEvents.ExecuteHierarchy(leaf, new PointerEventData(null), ExecuteEvents.pointerClickHandler));
            Assert.Equal(new[] { "Root:click0" }, EventLog.Log);
            Assert.Same(root, ExecuteEvents.GetEventHandler<IScrollHandler>(leaf));
            Assert.Null(ExecuteEvents.GetEventHandler<ISubmitHandler>(leaf));
            Assert.False(ExecuteEvents.CanHandleEvent<IPointerClickHandler>(middle));
            Assert.Throws<ArgumentException>(() => ExecuteEvents.ValidateEventData<PointerEventData>(new BaseEventData(null)));
        }
    }

    public class PointerTests : UGuiTest
    {
        TestInput input;
        EventSystem system;

        GameObject Setup()
        {
            EventLog.Log.Clear();
            var esGo = new GameObject("EventSystem");
            system = esGo.AddComponent<EventSystem>();
            var module = esGo.AddComponent<StandaloneInputModule>();
            input = esGo.AddComponent<TestInput>();
            module.inputOverride = input;
            var canvas = NewCanvas();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            // A panel holding a button at the screen's centre (512, 384).
            var panel = NewRect("Panel", canvas.transform, 400, 400, typeof(Image), typeof(EventLog));
            var button = NewRect("Button", panel, 100, 50, typeof(Image), typeof(EventLog));
            Frame();
            return button.gameObject;
        }

        void Step(Vector2 mouse, bool down = false, bool up = false)
        {
            input.Mouse = mouse;
            input.Down = down;
            input.Up = up;
            input.Held = down || (input.Held && !up);
            Frame();
        }

        [Fact]
        public void HoverPressReleaseClick()
        {
            Setup();
            Step(new Vector2(100, 100));
            Assert.Empty(EventLog.Log);
            Step(new Vector2(510, 380));
            Assert.Equal(new[] { "Button:enter", "Panel:enter" }, EventLog.Log);
            EventLog.Log.Clear();
            Step(new Vector2(510, 380), down: true);
            Assert.Equal(new[] { "Button:down" }, EventLog.Log);
            EventLog.Log.Clear();
            Step(new Vector2(510, 380), up: true);
            Assert.Equal(new[] { "Button:up", "Button:click1" }, EventLog.Log);
            EventLog.Log.Clear();
            // Into the panel only: the button is left, the panel still hovered.
            Step(new Vector2(400, 300));
            Assert.Equal(new[] { "Button:exit" }, EventLog.Log);
            Assert.True(system.IsPointerOverGameObject());
        }

        [Fact]
        public void DraggingPastTheThresholdCancelsTheClick()
        {
            Setup();
            var panel = GameObject.Find("Panel");
            var button = GameObject.Find("Button");
            Object.DestroyImmediate(panel.GetComponent<EventLog>());
            Object.DestroyImmediate(button.GetComponent<EventLog>());
            panel.AddComponent<DragLog>();
            button.AddComponent<PressLog>();
            Step(new Vector2(510, 380));
            Step(new Vector2(510, 380), down: true);
            Assert.Equal(new[] { "Button:down" }, EventLog.Log);
            EventLog.Log.Clear();
            // 5 pixels: within the 10 pixel threshold.
            Step(new Vector2(515, 380));
            Assert.Empty(EventLog.Log);
            // The drag starts; the press is released without a click.
            Step(new Vector2(530, 380));
            Assert.Equal(new[] { "Panel:beginDrag", "Button:up", "Panel:drag" }, EventLog.Log);
            EventLog.Log.Clear();
            Step(new Vector2(530, 380), up: true);
            Assert.Equal(new[] { "Panel:endDrag" }, EventLog.Log);
        }

        [Fact]
        public void ScrollGoesToTheHandlerUnderThePointer()
        {
            Setup();
            input.Scroll = new Vector2(0, 1);
            Step(new Vector2(510, 380));
            Assert.Contains("Button:scroll", EventLog.Log);
        }

        [Fact]
        public void RaycastHitsAreOrderedFrontFirst()
        {
            Setup();
            var other = NewCanvas("Front");
            other.sortingOrder = 5;
            other.gameObject.AddComponent<GraphicRaycaster>();
            NewRect("Cover", other.transform, 50, 50, typeof(Image));
            Frame();
            var results = new List<RaycastResult>();
            system.RaycastAll(new PointerEventData(system) { position = new Vector2(512, 384) }, results);
            Assert.Equal(new[] { "Cover", "Button", "Panel" }, results.ConvertAll(r => r.gameObject.name));
            Assert.True(results[1].depth > results[2].depth);
        }

        [Fact]
        public void SelectionSendsDeselectAndSelect()
        {
            var button = Setup();
            system.SetSelectedGameObject(button);
            system.SetSelectedGameObject(button);
            var other = new GameObject("Other");
            other.AddComponent<EventLog>();
            system.SetSelectedGameObject(other);
            Assert.Equal(new[] { "Button:select", "Button:deselect", "Other:select" }, EventLog.Log);
            Assert.Same(other, system.currentSelectedGameObject);
            Assert.Same(system, EventSystem.current);
        }
    }
}
