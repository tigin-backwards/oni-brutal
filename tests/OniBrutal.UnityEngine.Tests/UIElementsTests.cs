using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Xunit;

namespace OniBrutal.Tests
{
    public class UIElementsTests : EngineTest
    {
        public sealed class BareEvent : EventBase
        {
            public override void Dispose()
            {
            }
        }

        // Event types of their own, so the pools these tests count are theirs alone.
        public sealed class ProbeEvent : EventBase<ProbeEvent>
        {
        }

        public sealed class CappedEvent : EventBase<CappedEvent>
        {
        }

        public sealed class PenEvent : PointerEventBase<PenEvent>
        {
            public void Set(string type, Vector2 tiltAngles)
            {
                pointerType = type;
                tilt = tiltAngles;
            }

            public void SetAngles(string type, float altitude, float azimuth)
            {
                pointerType = type;
                altitudeAngle = altitude;
                azimuthAngle = azimuth;
            }

            public void SetPointer(int id, int pressed)
            {
                pointerId = id;
                pressedButtons = pressed;
                position = new Vector3(3f, 4f, 0f);
                modifiers = EventModifiers.Shift;
            }
        }

        static void Near(float expected, float actual, float tolerance = 1e-4f) => Assert.InRange(actual, expected - tolerance, expected + tolerance);

        [Fact]
        public void TypeIdsAreDistinctAndTheBaseReportsMinusOne()
        {
            long probe = ProbeEvent.TypeId();
            Assert.True(probe >= 1);
            Assert.Equal(probe, ProbeEvent.TypeId());
            Assert.NotEqual(probe, CappedEvent.TypeId());
            Assert.Equal(probe, new ProbeEvent().eventTypeId);
            Assert.Equal(-1, new BareEvent().eventTypeId);
        }

        [Fact]
        public void PooledEventsAreReusedAndReset()
        {
            var e = ProbeEvent.GetPooled();
            ulong firstSerial = e.InstanceSerial;
            Assert.True(e.FromPool);
            Assert.Equal(1, e.References);
            e.StopImmediatePropagation();
            e.Dispose();
            Assert.False(e.FromPool);

            var again = ProbeEvent.GetPooled();
            Assert.Same(e, again);
            Assert.True(again.InstanceSerial > firstSerial);
            Assert.False(again.isPropagationStopped);
            Assert.False(again.isImmediatePropagationStopped);
            Assert.Null(again.target);
            Assert.Equal(0, again.timestamp);
            again.Dispose();

            var trigger = ProbeEvent.GetPooled();
            var caused = ProbeEvent.GetPooled(trigger);
            Assert.Equal(trigger.InstanceSerial, caused.TriggerSerial);
            caused.Dispose();
            trigger.Dispose();
        }

        [Fact]
        public void TheLastReferenceReturnsTheEvent()
        {
            var e = ProbeEvent.GetPooled();
            e.Hold();
            e.Dispose();
            Assert.True(e.FromPool);
            Assert.Equal(1, e.References);
            e.Dispose();
            Assert.False(e.FromPool);
            Assert.Equal(0, e.References);
            // Released twice: nothing more happens, and the pool holds it once.
            e.Dispose();
            Assert.Same(e, ProbeEvent.GetPooled());
            Assert.NotSame(e, ProbeEvent.GetPooled());
        }

        [Fact]
        public void EventsCreatedWithNewAreNeverPooled()
        {
            var e = new ProbeEvent();
            Assert.False(e.FromPool);
            e.Dispose();
            Assert.NotSame(e, ProbeEvent.GetPooled());
        }

        [Fact]
        public void ResettingAHeldEventReportsIt()
        {
            var messages = new List<string>();
            Application.LogCallback record = (message, _, _) => messages.Add(message);
            Application.logMessageReceived += record;
            try
            {
                var e = ProbeEvent.GetPooled();
                e.ResetForPool();
                Assert.Contains("Event improperly released.", messages);
                Assert.Equal(0, e.References);
            }
            finally
            {
                Application.logMessageReceived -= record;
            }
        }

        [Fact]
        public void ThePoolKeepsAtMostAHundredEvents()
        {
            var events = new List<CappedEvent>();
            for (int i = 0; i < 101; i++) events.Add(CappedEvent.GetPooled());
            foreach (var e in events) e.Dispose();
            var reused = new HashSet<CappedEvent>(events);
            int fromPool = 0;
            for (int i = 0; i < 101; i++)
                if (reused.Contains(CappedEvent.GetPooled())) fromPool++;
            Assert.Equal(100, fromPool);
        }

        [Fact]
        public void PropagationFlagsFollowTheEventFamily()
        {
            var pointer = PointerDownEvent.GetPooled();
            Assert.True(pointer.bubbles);
            Assert.True(pointer.tricklesDown);
            Assert.Equal(-1, pointer.button);
            Assert.Equal("", pointer.pointerType);
            pointer.StopPropagation();
            Assert.True(pointer.isPropagationStopped);
            Assert.False(pointer.isImmediatePropagationStopped);
            pointer.skipDisabledElements = true;
            Assert.True(pointer.bubbles);
            pointer.Dispose();

            var focus = FocusEvent.GetPooled();
            Assert.False(focus.bubbles);
            Assert.True(focus.tricklesDown);
            focus.Dispose();
        }

        [Theory]
        [InlineData(0f, 0f, NavigationMoveEvent.Direction.None)]
        [InlineData(0.5f, 0.3f, NavigationMoveEvent.Direction.None)]
        [InlineData(1f, 0.5f, NavigationMoveEvent.Direction.Right)]
        [InlineData(-1f, 0.5f, NavigationMoveEvent.Direction.Left)]
        [InlineData(0.5f, 1f, NavigationMoveEvent.Direction.Up)]
        [InlineData(0.5f, -1f, NavigationMoveEvent.Direction.Down)]
        [InlineData(0.7f, 0.7f, NavigationMoveEvent.Direction.Up)]
        public void NavigationDirectionComesFromTheDominantAxis(float x, float y, NavigationMoveEvent.Direction expected)
        {
            var e = NavigationMoveEvent.GetPooled(new Vector2(x, y), EventModifiers.Shift);
            Assert.Equal(expected, e.direction);
            Assert.Equal(new Vector2(x, y), e.move);
            Assert.True(e.shiftKey);
            e.Dispose();
        }

        [Fact]
        public void NavigationByDirectionHasNoMove()
        {
            var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Left);
            Assert.Equal(NavigationMoveEvent.Direction.Left, e.direction);
            Assert.Equal(Vector2.zero, e.move);
            e.Dispose();
        }

        [Fact]
        public void NavigationKeysExcludeMeaningChangingModifiers()
        {
            Event Key(KeyCode key, EventModifiers modifiers) => new Event { keyCode = key, modifiers = modifiers };
            Assert.True(Key(KeyCode.LeftArrow, EventModifiers.FunctionKey).ShouldSendNavigationMoveEventRuntime());
            Assert.False(Key(KeyCode.LeftArrow, EventModifiers.Control).ShouldSendNavigationMoveEventRuntime());
            Assert.True(Key(KeyCode.Tab, EventModifiers.None).ShouldSendNavigationMoveEventRuntime());
            Assert.True(Key(KeyCode.Tab, EventModifiers.Shift).ShouldSendNavigationMoveEventRuntime());
            Assert.False(Key(KeyCode.Tab, EventModifiers.Alt).ShouldSendNavigationMoveEventRuntime());
            Assert.False(Key(KeyCode.A, EventModifiers.None).ShouldSendNavigationMoveEventRuntime());
        }

        [Fact]
        public void AltitudeAndAzimuthDeriveFromTilt()
        {
            var e = PenEvent.GetPooled();
            // Untilted: perpendicular, azimuth 0.
            Near(Mathf.PI / 2f, e.altitudeAngle);
            Near(0f, e.azimuthAngle);

            float tx = 0.3f, ty = 0.2f;
            e.Set(PointerType.pen, new Vector2(tx, ty));
            Near(Mathf.PI / 2f - Mathf.Acos(Mathf.Cos(tx) * Mathf.Cos(ty)), e.altitudeAngle);
            float azimuth = Mathf.PI / 2f - Mathf.Atan2(-Mathf.Cos(tx) * Mathf.Sin(ty), Mathf.Cos(ty) * Mathf.Sin(tx));
            azimuth = Mathf.Repeat(Mathf.Repeat(azimuth, 2f * Mathf.PI) - Mathf.PI / 2f, 2f * Mathf.PI);
            Near(azimuth, e.azimuthAngle);
            Assert.InRange(e.azimuthAngle, 0f, 2f * Mathf.PI);

            // Values set directly are returned as set, until the tilt changes.
            e.SetAngles(PointerType.pen, 0.25f, 1.5f);
            Near(0.25f, e.altitudeAngle);
            Near(1.5f, e.azimuthAngle);
            Assert.Equal(new Vector2(tx, ty), e.tilt);
            e.Set(PointerType.pen, Vector2.zero);
            Near(Mathf.PI / 2f, e.altitudeAngle);
            e.Dispose();
        }

        [Fact]
        public void TouchTiltDerivesFromAltitudeAndAzimuthOffWindows()
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer) return;
            var e = PenEvent.GetPooled();
            float a = 0.8f, z = 1.1f;
            e.SetAngles(PointerType.touch, a, z);
            var tilt = e.tilt;
            Near(Mathf.Atan(Mathf.Cos(z) * Mathf.Cos(a) / Mathf.Sin(z)), tilt.x);
            Near(Mathf.Atan(Mathf.Cos(z) * Mathf.Sin(a) / Mathf.Sin(z)), tilt.y);
            e.Dispose();
        }

        [Fact]
        public void PointerEventsCopyTheirTrigger()
        {
            var source = PenEvent.GetPooled();
            source.SetPointer(PointerId.penPointerIdBase, 1);
            source.SetAngles(PointerType.pen, 0.4f, 0.6f);
            var copy = PointerMoveEvent.GetPooled(source);
            Assert.Equal(PointerId.penPointerIdBase, copy.pointerId);
            Assert.Equal(PointerType.pen, copy.pointerType);
            Assert.Equal(1, copy.pressedButtons);
            Assert.Equal(new Vector3(3f, 4f, 0f), copy.position);
            Assert.True(copy.shiftKey);
            Near(0.4f, copy.altitudeAngle);
            Near(0.6f, copy.azimuthAngle);
            copy.Dispose();

            var none = PointerMoveEvent.GetPooled((IPointerEvent)null);
            Assert.Equal(-1, none.button);
            none.Dispose();

            var placed = PointerMoveEvent.GetPooled(null, new Vector2(7f, 8f), PointerId.touchPointerIdBase + 3);
            Assert.Equal(new Vector3(7f, 8f, 0f), placed.position);
            Assert.Equal(new Vector3(7f, 8f, 0f), placed.localPosition);
            Assert.Equal(PointerType.touch, placed.pointerType);
            placed.Dispose();

            var wheel = WheelEvent.GetPooled(new Vector3(0f, 2f, 0f), source);
            Assert.Equal(new Vector2(3f, 4f), wheel.mousePosition);
            Assert.Equal(new Vector3(0f, 2f, 0f), wheel.delta);
            wheel.Dispose();
            source.Dispose();
        }

        [Fact]
        public void PointerDeviceStateTracksButtonsPositionsAndPanels()
        {
            int id = PointerId.touchPointerIdBase + 5;
            PointerDeviceState.ReleaseAllButtons(id);
            PointerDeviceState.PressButton(id, 0);
            PointerDeviceState.PressButton(id, 2);
            Assert.Equal(0b101, PointerDeviceState.GetPressedButtons(id));
            Assert.True(PointerDeviceState.OtherButtonsPressed(id, 0));
            PointerDeviceState.ReleaseButton(id, 2);
            Assert.False(PointerDeviceState.OtherButtonsPressed(id, 0));
            PointerDeviceState.ReleaseAllButtons(id);
            Assert.Equal(0, PointerDeviceState.GetPressedButtons(id));

            // With no panel recorded, there is no delta.
            PointerDeviceState.RecordPosition(id, new Vector3(1f, 1f, 0f), null, ContextType.Player);
            Assert.Equal(Vector3.zero, PointerDeviceState.GetPointerDeltaPosition(id, ContextType.Player, new Vector3(5f, 5f, 0f)));
        }

        [Fact]
        public void EventSystemRegistrationKeepsOneActive()
        {
            var first = new GameObject("first");
            var second = new GameObject("second");
            var warnings = new List<string>();
            Application.LogCallback record = (message, _, type) => { if (type == LogType.Warning) warnings.Add(message); };
            Application.logMessageReceived += record;
            try
            {
                UIElementsRuntimeUtility.RegisterEventSystem(first);
                Assert.Same(first, UIElementsRuntimeUtility.activeEventSystem);
                UIElementsRuntimeUtility.RegisterEventSystem(second);
                Assert.Equal(["There can be only one active Event System."], warnings);
                UIElementsRuntimeUtility.UnregisterEventSystem(first);
                Assert.Same(second, UIElementsRuntimeUtility.activeEventSystem);
                UIElementsRuntimeUtility.UnregisterEventSystem(second);
                Assert.Null(UIElementsRuntimeUtility.activeEventSystem);
                Assert.Empty(UIElementsRuntimeUtility.GetSortedPlayerPanels());
            }
            finally
            {
                Application.logMessageReceived -= record;
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
