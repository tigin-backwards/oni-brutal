using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    /// Records enter, exit and move with the event data's hovered list, delta and pointerEnter.
    public class MoveLog : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public static readonly List<string> Log = new();
        public static readonly List<PointerEventData> Data = new();
        public static readonly List<string> Hovered = new();
        public static readonly List<string> Enter = new();

        void Add(string what, PointerEventData d)
        {
            Log.Add(name + ":" + what);
            Hovered.Add(string.Join(",", d.hovered.Select(h => h.name)));
            Data.Add(d);
            Enter.Add(d.pointerEnter != null ? d.pointerEnter.name : "null");
        }

        public void OnPointerEnter(PointerEventData eventData) => Add("enter", eventData);
        public void OnPointerExit(PointerEventData eventData) => Add("exit", eventData);
        public void OnPointerMove(PointerEventData eventData) => Add("move", eventData);

        public static void Clear()
        {
            Log.Clear();
            Data.Clear();
            Hovered.Clear();
            Enter.Clear();
        }
    }

    public class MoveOnlyLog : MonoBehaviour, IPointerMoveHandler
    {
        public void OnPointerMove(PointerEventData eventData)
        {
            MoveLog.Log.Add(name + ":move");
            MoveLog.Hovered.Add(string.Join(",", eventData.hovered.Select(h => h.name)));
            MoveLog.Data.Add(eventData);
            MoveLog.Enter.Add(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null");
        }
    }

    public class SecondMoveLog : MonoBehaviour, IPointerMoveHandler
    {
        public void OnPointerMove(PointerEventData eventData)
        {
            MoveLog.Log.Add(name + ":move2");
            MoveLog.Hovered.Add(string.Join(",", eventData.hovered.Select(h => h.name)));
            MoveLog.Data.Add(eventData);
            MoveLog.Enter.Add(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null");
        }
    }

    // Pointer move events (docs/cleanroom/ugui.md, "Pointer move events"), on the hierarchy of
    // tools/UnityProbe's pointermove scenario: P (400 x 300; enter, exit, move) holds A (enter,
    // exit, move and a second move handler) with its child A1 (no handler), B (move only), C (no
    // handler), D (move, not a raycast target) and E (a disabled move handler).
    public class PointerMoveTests : UGuiTest
    {
        TestInput input;
        GameObject p, a, b;
        Canvas canvas;
        static readonly Vector2 Centre = new(512f, 384f);

        static GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, bool raycast = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.AddComponent<Image>().raycastTarget = raycast;
            return go;
        }

        void Setup()
        {
            MoveLog.Clear();
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            var module = esGo.AddComponent<StandaloneInputModule>();
            input = esGo.AddComponent<TestInput>();
            module.inputOverride = input;
            canvas = NewCanvas("canvas");
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            p = Box("P", canvas.transform, Vector2.zero, new Vector2(400f, 300f));
            p.AddComponent<MoveLog>();
            a = Box("A", p.transform, new Vector2(-130f, 0f), new Vector2(100f, 100f));
            a.AddComponent<MoveLog>();
            a.AddComponent<SecondMoveLog>();
            Box("A1", a.transform, Vector2.zero, new Vector2(40f, 40f));
            b = Box("B", p.transform, new Vector2(130f, 0f), new Vector2(100f, 100f));
            b.AddComponent<MoveOnlyLog>();
            Box("C", p.transform, new Vector2(0f, -90f), new Vector2(100f, 100f));
            Box("D", p.transform, new Vector2(0f, 90f), new Vector2(100f, 100f), raycast: false).AddComponent<MoveOnlyLog>();
            Box("E", p.transform, Vector2.zero, new Vector2(60f, 60f)).AddComponent<MoveOnlyLog>().enabled = false;
            Frame();
        }

        // One frame with the pointer at the canvas centre + offset; returns what was sent.
        string[] Step(Vector2 offset, bool down = false, bool up = false, bool held = false)
        {
            MoveLog.Clear();
            input.Mouse = Centre + offset;
            input.Down = down;
            input.Up = up;
            input.Held = down || held;
            Frame();
            return MoveLog.Log.ToArray();
        }

        string[] StepAt(Vector2 screen)
        {
            MoveLog.Clear();
            input.Mouse = screen;
            input.Down = input.Up = input.Held = false;
            Frame();
            return MoveLog.Log.ToArray();
        }

        static readonly Vector2 OnA = new(-130f, 30f);
        static readonly Vector2 OnA1 = new(-130f, 0f);
        static readonly Vector2 OnPBare = new(-40f, 0f);
        static readonly Vector2 OnB = new(130f, 0f);

        [Fact]
        public void TheInterfaceAndItsEventFunctionExist()
        {
            Assert.NotNull(ExecuteEvents.pointerMoveHandler);
            Assert.True(typeof(IEventSystemHandler).IsAssignableFrom(typeof(IPointerMoveHandler)));
            var go = new GameObject("M");
            go.AddComponent<MoveOnlyLog>();
            MoveLog.Clear();
            Assert.True(ExecuteEvents.Execute(go, new PointerEventData(null), ExecuteEvents.pointerMoveHandler));
            Assert.Equal(new[] { "M:move" }, MoveLog.Log);
        }

        [Fact]
        public void NothingIsSentWithNoTargetAndNothingHovered()
        {
            Setup();
            Assert.Empty(Step(new Vector2(-600f, 0f)));
            Assert.Empty(Step(new Vector2(-600f, 0f)));
            Assert.Empty(Step(new Vector2(-610f, 0f)));
        }

        [Fact]
        public void EnteredObjectsGetEnterThenMoveAndMovesFollowTheHoveredList()
        {
            Setup();
            Step(new Vector2(-600f, 0f));
            Assert.Equal(new[] { "A:enter", "A:move", "A:move2", "P:enter", "P:move" }, Step(OnA));
            // Still: nothing.
            Assert.Empty(Step(OnA));
            Assert.Empty(Step(OnA));
            // Moving over the same target: each hovered object, in list order.
            Assert.Equal(new[] { "A:move", "A:move2", "P:move" }, Step(OnA + new Vector2(5f, 0f)));
            Assert.Equal(new Vector2(5f, 0f), MoveLog.Data[0].delta);
            Assert.Empty(Step(OnA + new Vector2(5f, 0f)));
        }

        [Fact]
        public void ObjectsAtOrAboveTheCommonRootGetNoMove()
        {
            Setup();
            Step(OnA);
            // From A to its child A1: nothing is sent to A or P.
            Assert.Empty(Step(OnA1));
            // On A1 (no handler) moving: A, then P; nothing bubbles from A1.
            Assert.Equal(new[] { "A:move", "A:move2", "P:move" }, Step(OnA1 + new Vector2(-1f, 0f)));
            Assert.Equal("A,P,canvas,A1", MoveLog.Hovered[0]);
            // From A1 to P's bare area: A gets move then exit; P gets nothing.
            Assert.Equal(new[] { "A:move", "A:move2", "A:exit" }, Step(OnPBare));
            Assert.All(MoveLog.Enter, e => Assert.Equal("A1", e));
            // During the enter walk pointerEnter is already the new target.
            Step(OnA);
            Assert.Equal(new[] { "A:enter", "A:move", "A:move2" }, MoveLog.Log);
            Assert.All(MoveLog.Enter, e => Assert.Equal("A", e));
        }

        [Fact]
        public void EnteredObjectsAreAppendedToTheHoveredList()
        {
            Setup();
            Step(OnPBare);
            Assert.Equal(new[] { "B:move" }, Step(OnB));
            Assert.Equal("B", MoveLog.Enter[0]);
            Assert.Equal(new[] { "P:move", "B:move" }, Step(OnB + new Vector2(1f, 0f)));
            Assert.Equal("P,canvas,B", MoveLog.Hovered[0]);
            // From B to A (common root P): B move, then A enter, then move.
            Assert.Equal(new[] { "B:move", "A:enter", "A:move", "A:move2" }, Step(OnA));
        }

        [Fact]
        public void LeavingEverythingSendsMoveThenExitToEachHoveredObject()
        {
            Setup();
            Step(OnPBare);
            Step(OnA);
            // From A (hovered [P, canvas, A]) to a point off the screen.
            Assert.Equal(new[] { "P:move", "P:exit", "A:move", "A:move2", "A:exit" }, StepAt(new Vector2(-50f, -50f)));
            Assert.All(MoveLog.Hovered, h => Assert.Equal("P,canvas,A", h));
            Assert.All(MoveLog.Data, d => Assert.False(d.used));
            Assert.Empty(StepAt(new Vector2(-50f, -50f)));
            Assert.Empty(StepAt(new Vector2(-60f, -60f)));
        }

        [Fact]
        public void MoveIsSentOnExitAndEnterEvenWhenThePointerIsStill()
        {
            Setup();
            Step(OnA);
            Step(OnA);
            // A moves away from under a still pointer, and back.
            ((RectTransform)a.transform).anchoredPosition = new Vector2(-130f, 400f);
            Assert.Equal(new[] { "A:move", "A:move2", "A:exit" }, Step(OnA));
            Assert.Equal(Vector2.zero, MoveLog.Data[0].delta);
            Assert.False(MoveLog.Data[0].IsPointerMoving());
            Assert.Empty(Step(OnA));
            ((RectTransform)a.transform).anchoredPosition = new Vector2(-130f, 0f);
            Assert.Equal(new[] { "A:enter", "A:move", "A:move2" }, Step(OnA));
            Assert.Equal(Vector2.zero, MoveLog.Data[1].delta);
        }

        [Fact]
        public void DisabledHandlersAndNonRaycastTargetsGetNothing()
        {
            Setup();
            Step(OnPBare);
            // E's handler is disabled: entering E sends nothing (P is the common root), moving on
            // it reaches P only.
            Assert.Empty(Step(new Vector2(10f, 0f)));
            Assert.Equal(new[] { "P:move" }, Step(new Vector2(11f, 0f)));
            // D is not a raycast target, so P is the target over it: leaving E for P sends
            // nothing, and D never gets a move.
            Assert.Empty(Step(new Vector2(0f, 90f)));
            Assert.Equal(new[] { "P:move" }, Step(new Vector2(2f, 90f)));
            // A disabled component gets nothing; the object's other handler still runs.
            Step(OnA);
            a.GetComponent<MoveLog>().enabled = false;
            // (hovered is [P, canvas, A]: P was entered first.)
            Assert.Equal(new[] { "P:move", "A:move2" }, Step(OnA + new Vector2(1f, 0f)));
        }

        [Fact]
        public void WithNoCommonRootEachIsRemovedAsItGoes()
        {
            Setup();
            var other = NewCanvas("other");
            other.sortingOrder = 1;
            other.gameObject.AddComponent<GraphicRaycaster>();
            Box("Q", other.transform, new Vector2(-300f, 0f), new Vector2(50f, 50f)).AddComponent<MoveLog>();
            Frame();
            Step(OnPBare);
            Step(OnA);
            Assert.Equal(new[] { "A:move", "A:move2", "A:exit", "P:move", "P:exit", "Q:enter", "Q:move" }, Step(new Vector2(-300f, 0f)));
            Assert.Equal("P,canvas", MoveLog.Hovered[3]);
            Assert.Equal("P,canvas", MoveLog.Hovered[4]);
        }

        [Fact]
        public void PressedButtonsChangeNothing()
        {
            Setup();
            Step(OnPBare);
            Step(OnA);
            var free = new List<string[]>
            {
                Step(OnA + new Vector2(2f, 0f)), Step(OnA + new Vector2(30f, 0f)), Step(OnPBare), Step(OnB),
            };
            Setup2();
            Step(OnPBare);
            Step(OnA, down: true);
            var held = new List<string[]>
            {
                Step(OnA + new Vector2(2f, 0f), held: true), Step(OnA + new Vector2(30f, 0f), held: true), Step(OnPBare, held: true), Step(OnB, held: true),
            };
            Assert.Equal(free, held);
            // Releasing without moving sends nothing more.
            Assert.Empty(Step(OnB, up: true));
        }

        // A fresh hierarchy (the old one destroyed).
        void Setup2()
        {
            Object.DestroyImmediate(canvas.gameObject);
            Object.DestroyImmediate(EventSystem.current.gameObject);
            Setup();
        }
    }
}
