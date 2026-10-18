using System;

namespace UnityEngine.UIElements
{
    /// Pointer ids: the mouse, then the touch, pen and tracked-device ranges.
    public static partial class PointerId
    {
        public static readonly int maxPointers = 32;
        public static readonly int invalidPointerId = -1;
        public static readonly int mousePointerId = 0;
        public static readonly int touchPointerIdBase = 1;
        public static readonly int touchPointerCount = 20;
        public static readonly int penPointerIdBase = touchPointerIdBase + touchPointerCount;
        public static readonly int penPointerCount = 2;
        public static readonly int trackedPointerIdBase = penPointerIdBase + penPointerCount;
        public static readonly int trackedPointerCount = 8;
    }

    public static partial class PointerType
    {
        public static readonly string mouse = "mouse";
        public static readonly string touch = "touch";
        public static readonly string pen = "pen";
        public static readonly string tracked = "tracked";
    }

    /// The data of a pointer event. Angles in radians: altitude and azimuth not set since the
    /// last tilt change are derived from the tilt when first read.
    public abstract partial class PointerEventBase<T>
    {
        float altitude;
        float azimuth;
        Vector2 tiltAngles;
        bool altitudeSet;
        bool azimuthSet;
        bool tiltDerived;

        public int pointerId { get; protected set; }

        public string pointerType { get; protected set; }

        public bool isPrimary { get; protected set; }

        public int button { get; protected set; }

        public int pressedButtons { get; protected set; }

        public Vector3 position { get; protected set; }

        public Vector3 localPosition { get; protected set; }

        public Vector3 deltaPosition { get; protected set; }

        public float deltaTime { get; protected set; }

        public int clickCount { get; protected set; }

        public float pressure { get; protected set; }

        public float tangentialPressure { get; protected set; }

        /// The pen's angle to the surface: 0 parallel, π/2 perpendicular.
        public float altitudeAngle
        {
            get
            {
                if (!altitudeSet)
                {
                    altitude = OniBrutal.Engine.PenAngles.AltitudeFromTilt(tiltAngles);
                    altitudeSet = true;
                }
                return altitude;
            }
            protected set
            {
                altitude = value;
                altitudeSet = true;
            }
        }

        /// The pen's direction around the surface normal.
        public float azimuthAngle
        {
            get
            {
                if (!azimuthSet)
                {
                    azimuth = OniBrutal.Engine.PenAngles.AzimuthFromTilt(tiltAngles);
                    azimuthSet = true;
                }
                return azimuth;
            }
            protected set
            {
                azimuth = value;
                azimuthSet = true;
            }
        }

        public float twist { get; protected set; }

        /// The pen's tilt about the X and Y axes. Off Windows, a touch pointer's tilt not set
        /// directly is derived once from its altitude and azimuth.
        public Vector2 tilt
        {
            get
            {
                if (!tiltDerived && pointerType == PointerType.touch && Application.platform != RuntimePlatform.WindowsPlayer
                    && Application.platform != RuntimePlatform.WindowsEditor)
                {
                    tiltAngles = OniBrutal.Engine.PenAngles.TiltFrom(altitude, azimuth);
                    tiltDerived = true;
                }
                return tiltAngles;
            }
            protected set
            {
                tiltAngles = value;
                tiltDerived = true;
                altitudeSet = false;
                azimuthSet = false;
            }
        }

        public PenStatus penStatus { get; protected set; }

        public Vector2 radius { get; protected set; }

        public Vector2 radiusVariance { get; protected set; }

        public EventModifiers modifiers { get; protected set; }

        public bool shiftKey => (modifiers & EventModifiers.Shift) != 0;

        public bool ctrlKey => (modifiers & EventModifiers.Control) != 0;

        public bool commandKey => (modifiers & EventModifiers.Command) != 0;

        public bool altKey => (modifiers & EventModifiers.Alt) != 0;

        /// Command on macOS, Control elsewhere.
        public bool actionKey => OniBrutal.Engine.UIEventModifiers.IsMacOS ? commandKey : ctrlKey;

        protected override void Init()
        {
            base.Init();
            SetPropagation(bubblesUp: true, trickleDown: true);
            pointerId = 0;
            pointerType = string.Empty;
            isPrimary = false;
            button = -1;
            pressedButtons = 0;
            position = Vector3.zero;
            localPosition = Vector3.zero;
            deltaPosition = Vector3.zero;
            deltaTime = 0f;
            clickCount = 0;
            pressure = 0f;
            tangentialPressure = 0f;
            altitude = 0f;
            azimuth = 0f;
            tiltAngles = Vector2.zero;
            altitudeSet = false;
            azimuthSet = false;
            tiltDerived = false;
            twist = 0f;
            penStatus = PenStatus.None;
            radius = Vector2.zero;
            radiusVariance = Vector2.zero;
            modifiers = EventModifiers.None;
        }

        /// A pooled event carrying every pointer property of triggerEvent (none when it is null).
        public static T GetPooled(IPointerEvent triggerEvent)
        {
            T e = GetPooled();
            if (triggerEvent != null)
                e.CopyPointer(triggerEvent);
            return e;
        }

        /// As GetPooled(triggerEvent) when there is a trigger; otherwise an event at position
        /// (also its local position) for pointerId, with the type that id belongs to.
        internal static T GetPooled(IPointerEvent triggerEvent, Vector2 position, int pointerId)
        {
            if (triggerEvent != null)
                return GetPooled(triggerEvent);
            T e = GetPooled();
            e.position = position;
            e.localPosition = position;
            e.pointerId = pointerId;
            e.pointerType = OniBrutal.Engine.PointerIds.TypeOf(pointerId);
            return e;
        }

        void CopyPointer(IPointerEvent source)
        {
            pointerId = source.pointerId;
            pointerType = source.pointerType;
            isPrimary = source.isPrimary;
            button = source.button;
            pressedButtons = source.pressedButtons;
            position = source.position;
            localPosition = source.localPosition;
            deltaPosition = source.deltaPosition;
            deltaTime = source.deltaTime;
            clickCount = source.clickCount;
            pressure = source.pressure;
            tangentialPressure = source.tangentialPressure;
            // The tilt first: setting it forgets angles that are not set explicitly.
            tilt = source.tilt;
            altitudeAngle = source.altitudeAngle;
            azimuthAngle = source.azimuthAngle;
            twist = source.twist;
            penStatus = source.penStatus;
            radius = source.radius;
            radiusVariance = source.radiusVariance;
            modifiers = source.modifiers;
        }
    }

    public sealed partial class PointerDownEvent
    {
    }

    public sealed partial class PointerUpEvent
    {
    }

    public sealed partial class PointerMoveEvent
    {
    }

    public sealed partial class PointerCancelEvent
    {
    }
}

namespace OniBrutal.Engine
{
    using UnityEngine;
    using UnityEngine.UIElements;

    internal static class PointerIds
    {
        /// The pointer type of an id's range; "" outside every range.
        public static string TypeOf(int pointerId)
        {
            if (pointerId == PointerId.mousePointerId)
                return PointerType.mouse;
            if (InRange(pointerId, PointerId.touchPointerIdBase, PointerId.touchPointerCount))
                return PointerType.touch;
            if (InRange(pointerId, PointerId.penPointerIdBase, PointerId.penPointerCount))
                return PointerType.pen;
            if (InRange(pointerId, PointerId.trackedPointerIdBase, PointerId.trackedPointerCount))
                return PointerType.tracked;
            return string.Empty;
        }

        static bool InRange(int id, int first, int count) => id >= first && id < first + count;
    }

    internal static class UIEventModifiers
    {
        public static bool IsMacOS => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
    }

    /// Spherical relations between a pen's tilt (about X and Y) and its altitude and azimuth.
    internal static class PenAngles
    {
        const float HalfPi = Mathf.PI * 0.5f;
        const float TwoPi = Mathf.PI * 2f;

        public static float AltitudeFromTilt(Vector2 tilt) => HalfPi - Mathf.Acos(Mathf.Cos(tilt.x) * Mathf.Cos(tilt.y));

        public static float AzimuthFromTilt(Vector2 tilt)
        {
            if (tilt.x == 0f)
                return 0f;
            float angle = HalfPi - Mathf.Atan2(-Mathf.Cos(tilt.x) * Mathf.Sin(tilt.y), Mathf.Cos(tilt.y) * Mathf.Sin(tilt.x));
            return Wrap(Wrap(angle) - HalfPi);
        }

        public static Vector2 TiltFrom(float altitude, float azimuth)
        {
            float cosAzimuth = Mathf.Cos(azimuth), sinAzimuth = Mathf.Sin(azimuth);
            return new Vector2(
                Mathf.Atan(cosAzimuth * Mathf.Cos(altitude) / sinAzimuth),
                Mathf.Atan(cosAzimuth * Mathf.Sin(altitude) / sinAzimuth));
        }

        /// Into [0, 2π).
        static float Wrap(float angle)
        {
            angle %= TwoPi;
            return angle < 0f ? angle + TwoPi : angle;
        }
    }
}
