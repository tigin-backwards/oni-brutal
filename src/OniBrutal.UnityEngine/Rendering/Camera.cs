using System;
using System.Collections.Generic;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine;
using OniBrutal.Engine.Platform;
using OniBrutal.Engine.Rendering;
using OniBrutal.Engine.Serialization;
using UnityEngine.Rendering;

namespace UnityEngine
{
    public partial class Camera
    {
        public delegate void CameraCallback(Camera cam);

        public static CameraCallback onPreCull;
        public static CameraCallback onPreRender;
        public static CameraCallback onPostRender;

        public Camera() { }

        internal CameraNative Data => (CameraNative)NativeOrThrow;

        internal override void EngineOnEnable() => Cameras.Add(this);
        internal override void EngineOnDisable() => Cameras.Remove(this);

        // ----- Settings -----

        public float nearClipPlane { get => Data.Near; set => Data.Near = value; }
        public float farClipPlane { get => Data.Far; set => Data.Far = value; }

        /// Vertical field of view in degrees, clamped to [1e-5, 179].
        public float fieldOfView
        {
            get => Data.FieldOfView;
            set => Data.FieldOfView = Mathf.Clamp(value, 1e-5f, 179f);
        }

        public bool orthographic { get => Data.Orthographic; set => Data.Orthographic = value; }
        public float orthographicSize { get => Data.OrthographicSize; set => Data.OrthographicSize = value; }
        public float depth { get => Data.Depth; set => Data.Depth = value; }
        public int cullingMask { get => Data.CullingMask; set => Data.CullingMask = value; }
        public int eventMask { get => Data.EventMask; set => Data.EventMask = value; }
        public Color backgroundColor { get => Data.BackgroundColor; set => Data.BackgroundColor = value; }
        public CameraClearFlags clearFlags { get => Data.ClearFlags; set => Data.ClearFlags = value; }
        public DepthTextureMode depthTextureMode { get => Data.DepthTextureMode; set => Data.DepthTextureMode = value; }
        public RenderingPath renderingPath { get => Data.RenderingPath; set => Data.RenderingPath = value; }
        public bool allowHDR { get => Data.AllowHDR; set => Data.AllowHDR = value; }
        public bool allowMSAA { get => Data.AllowMSAA; set => Data.AllowMSAA = value; }
        public bool useOcclusionCulling { get => Data.OcclusionCulling; set => Data.OcclusionCulling = value; }
        public TransparencySortMode transparencySortMode { get => Data.TransparencySortMode; set => Data.TransparencySortMode = value; }
        public Vector3 transparencySortAxis { get => Data.TransparencySortAxis; set => Data.TransparencySortAxis = value; }

        public void ResetTransparencySortSettings()
        {
            var d = Data;
            d.TransparencySortMode = TransparencySortMode.Default;
            d.TransparencySortAxis = new Vector3(0f, 0f, 1f);
        }

        public int targetDisplay { get => Data.TargetDisplay; set => Data.TargetDisplay = value; }

        /// Rendering into a texture; assigning one clears target buffers set with SetTargetBuffers.
        public RenderTexture targetTexture
        {
            get => Data.TargetTexture;
            set
            {
                var d = Data;
                d.TargetTexture = value;
                d.TargetColorBuffers = null;
            }
        }

        public void SetTargetBuffers(RenderBuffer colorBuffer, RenderBuffer depthBuffer) => SetTargetBuffers([colorBuffer], depthBuffer);

        /// Renders into the given surfaces (multiple render targets) instead of targetTexture.
        public void SetTargetBuffers(RenderBuffer[] colorBuffer, RenderBuffer depthBuffer)
        {
            if (colorBuffer == null) throw new ArgumentNullException(nameof(colorBuffer));
            var d = Data;
            d.TargetColorBuffers = (RenderBuffer[])colorBuffer.Clone();
            d.TargetDepthBuffer = depthBuffer;
        }

        // ----- Viewport -----

        /// Normalized viewport rectangle, clamped to [0, 1].
        public Rect rect
        {
            get => Data.Rect;
            set => Data.Rect = CameraNative.ClampRect(value);
        }

        public Rect pixelRect
        {
            get => Data.PixelRect;
            set
            {
                var size = Data.TargetSize;
                rect = new Rect(value.x / size.x, value.y / size.y, value.width / size.x, value.height / size.y);
            }
        }

        public int pixelWidth => Data.PixelWidth;
        public int pixelHeight => Data.PixelHeight;
        public int scaledPixelWidth => Data.PixelWidth;
        public int scaledPixelHeight => Data.PixelHeight;

        /// Width over height of the viewport unless set explicitly (until ResetAspect).
        public float aspect
        {
            get => Data.Aspect;
            set => Data.AspectOverride = value;
        }

        public void ResetAspect() => Data.AspectOverride = null;

        // ----- Matrices -----

        /// OpenGL convention: the camera looks down -Z of its view space.
        public Matrix4x4 worldToCameraMatrix
        {
            get => Data.WorldToCamera;
            set => Data.WorldToCameraOverride = value;
        }

        public Matrix4x4 cameraToWorldMatrix => Data.WorldToCamera.inverse;

        public Matrix4x4 projectionMatrix
        {
            get => Data.Projection;
            set => Data.ProjectionOverride = value;
        }

        public void ResetWorldToCameraMatrix() => Data.WorldToCameraOverride = null;
        public void ResetProjectionMatrix() => Data.ProjectionOverride = null;

        // ----- Coordinate conversions -----

        /// Screen pixels to viewport (0..1 across the camera's pixel rect); z unchanged.
        public Vector3 ScreenToViewportPoint(Vector3 position)
        {
            var r = Data.PixelRect;
            return new Vector3((position.x - r.x) / r.width, (position.y - r.y) / r.height, position.z);
        }

        public Vector3 ViewportToScreenPoint(Vector3 position)
        {
            var r = Data.PixelRect;
            return new Vector3(r.x + position.x * r.width, r.y + position.y * r.height, position.z);
        }

        /// Viewport xy, z = distance in front of the camera along its view axis.
        public Vector3 WorldToViewportPoint(Vector3 position)
        {
            var d = Data;
            var view = d.WorldToCamera.MultiplyPoint3x4(position);
            var clip = d.Projection * new Vector4(view.x, view.y, view.z, 1f);
            float w = clip.w == 0f ? 1f : clip.w;
            return new Vector3((clip.x / w + 1f) * 0.5f, (clip.y / w + 1f) * 0.5f, -view.z);
        }

        public Vector3 WorldToScreenPoint(Vector3 position) => ViewportToScreenPoint(WorldToViewportPoint(position));

        /// The world point at viewport xy whose distance in front of the camera is z.
        public Vector3 ViewportToWorldPoint(Vector3 position)
        {
            var (near, far) = Data.ViewportSegment(position.x, position.y);
            var d = Data;
            float dn = DistanceInFront(d, near), df = DistanceInFront(d, far);
            float t = df == dn ? 0f : (position.z - dn) / (df - dn);
            return near + (far - near) * t;
        }

        static float DistanceInFront(CameraNative d, Vector3 world) => -d.WorldToCamera.MultiplyPoint3x4(world).z;

        public Vector3 ScreenToWorldPoint(Vector3 position) => ViewportToWorldPoint(ScreenToViewportPoint(position));

        /// From the near-plane point under the viewport position, away from the camera.
        public Ray ViewportPointToRay(Vector3 pos)
        {
            var (near, far) = Data.ViewportSegment(pos.x, pos.y);
            return new Ray(near, (far - near).normalized);
        }

        public Ray ScreenPointToRay(Vector3 pos) => ViewportPointToRay(ScreenToViewportPoint(pos));

        // ----- Cameras -----

        /// The first enabled camera tagged "MainCamera".
        public static Camera main => Cameras.Main;

        /// The camera being rendered (null outside rendering).
        public static Camera current => FrameRenderer.Current;

        public static int allCamerasCount => Cameras.Enabled.Count;

        public static Camera[] allCameras => Cameras.Enabled.ToArray();

        public static int GetAllCameras(Camera[] cameras)
        {
            if (cameras == null) throw new NullReferenceException();
            if (cameras.Length < allCamerasCount) throw new ArgumentException("Passed in array to fill with cameras is to small to hold the number of cameras. Use Camera.allCamerasCount to get the needed size.");
            Cameras.Enabled.CopyTo(cameras);
            return allCamerasCount;
        }

        /// Copies every camera setting, the transform's position/rotation/scale and the layer.
        public void CopyFrom(Camera other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Data.CopySettings(other.Data);
            var t = transform;
            var o = other.transform;
            t.position = o.position;
            t.rotation = o.rotation;
            t.localScale = o.localScale;
            gameObject.layer = other.gameObject.layer;
        }

        /// Renders the camera now, even when disabled.
        public void Render()
        {
            Threads.AssertMainThread("Render");
            FrameRenderer.RenderCamera(this);
        }

        // ----- Command buffers -----

        public void AddCommandBuffer(CameraEvent evt, CommandBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            var d = Data;
            if (!d.CommandBuffers.TryGetValue(evt, out var list)) d.CommandBuffers[evt] = list = new List<CommandBuffer>();
            list.Add(buffer);
        }

        public void RemoveCommandBuffer(CameraEvent evt, CommandBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (Data.CommandBuffers.TryGetValue(evt, out var list)) list.RemoveAll(b => b == buffer);
        }

        public void RemoveCommandBuffers(CameraEvent evt) => Data.CommandBuffers.Remove(evt);

        public void RemoveAllCommandBuffers() => Data.CommandBuffers.Clear();

        public CommandBuffer[] GetCommandBuffers(CameraEvent evt)
            => Data.CommandBuffers.TryGetValue(evt, out var list) ? list.ToArray() : Array.Empty<CommandBuffer>();

        public int commandBufferCount
        {
            get
            {
                int n = 0;
                foreach (var l in Data.CommandBuffers.Values) n += l.Count;
                return n;
            }
        }
    }
}

namespace OniBrutal.Engine.Rendering
{
    using UnityEngine;

    internal sealed class CameraNative : BehaviourNative
    {
        public CameraClearFlags ClearFlags = CameraClearFlags.Skybox;
        public Color BackgroundColor = new(0.19215687f, 0.3019608f, 0.4745098f, 0f);
        public Rect Rect = new(0f, 0f, 1f, 1f);
        public float Near = 0.3f;
        public float Far = 1000f;
        public float FieldOfView = 60f;
        public bool Orthographic;
        public float OrthographicSize = 5f;
        public float Depth;
        public int CullingMask = -1;
        public int EventMask = -1;
        public RenderingPath RenderingPath = RenderingPath.UsePlayerSettings;
        public RenderTexture TargetTexture;
        public int TargetDisplay;
        public bool AllowHDR = true;
        public bool AllowMSAA = true;
        public bool OcclusionCulling = true;
        public DepthTextureMode DepthTextureMode;
        public TransparencySortMode TransparencySortMode;
        public Vector3 TransparencySortAxis = new(0f, 0f, 1f);
        public float? AspectOverride;
        public Matrix4x4? WorldToCameraOverride;
        public Matrix4x4? ProjectionOverride;
        public RenderBuffer[] TargetColorBuffers;
        public RenderBuffer TargetDepthBuffer;
        public readonly Dictionary<UnityEngine.Rendering.CameraEvent, List<UnityEngine.Rendering.CommandBuffer>> CommandBuffers = new();

        public static Rect ClampRect(Rect r)
        {
            float xMin = Mathf.Clamp01(r.xMin), yMin = Mathf.Clamp01(r.yMin), xMax = Mathf.Clamp01(r.xMax), yMax = Mathf.Clamp01(r.yMax);
            return Rect.MinMaxRect(xMin, yMin, Math.Max(xMin, xMax), Math.Max(yMin, yMax));
        }

        /// Size of what the camera renders into: its target texture, else the screen.
        public Vector2 TargetSize
        {
            get
            {
                if (TargetTexture != null) return new Vector2(TargetTexture.width, TargetTexture.height);
                var p = PlatformServices.Current;
                return new Vector2(p.FramebufferWidth, p.FramebufferHeight);
            }
        }

        public Rect PixelRect
        {
            get
            {
                var s = TargetSize;
                return new Rect(Rect.x * s.x, Rect.y * s.y, Rect.width * s.x, Rect.height * s.y);
            }
        }

        public int PixelWidth => Mathf.RoundToInt(PixelRect.width);
        public int PixelHeight => Mathf.RoundToInt(PixelRect.height);

        public float Aspect
        {
            get
            {
                if (AspectOverride is { } a) return a;
                var r = PixelRect;
                return r.height == 0f ? 1f : r.width / r.height;
            }
        }

        /// The transform without scale, Z flipped (view space looks down -Z).
        public Matrix4x4 WorldToCamera
        {
            get
            {
                if (WorldToCameraOverride is { } m) return m;
                var t = GameObject.Transform;
                var noScale = Matrix4x4.TRS(Hierarchy.Position(t), t.WorldRotation, Vector3.one).inverse;
                return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * noScale;
            }
        }

        public Matrix4x4 Projection
        {
            get
            {
                if (ProjectionOverride is { } m) return m;
                float aspect = Aspect;
                return Orthographic
                    ? Matrix4x4.Ortho(-OrthographicSize * aspect, OrthographicSize * aspect, -OrthographicSize, OrthographicSize, Near, Far)
                    : Matrix4x4.Perspective(FieldOfView, aspect, Near, Far);
            }
        }

        /// World points on the near and far planes under a viewport position.
        public (Vector3 Near, Vector3 Far) ViewportSegment(float vx, float vy)
        {
            var inverse = (Projection * WorldToCamera).inverse;
            float x = vx * 2f - 1f, y = vy * 2f - 1f;
            return (Unproject(inverse, x, y, -1f), Unproject(inverse, x, y, 1f));
        }

        static Vector3 Unproject(Matrix4x4 inverseViewProjection, float x, float y, float z)
        {
            var p = inverseViewProjection * new Vector4(x, y, z, 1f);
            return new Vector3(p.x, p.y, p.z) / p.w;
        }

        public void CopySettings(CameraNative o)
        {
            ClearFlags = o.ClearFlags;
            BackgroundColor = o.BackgroundColor;
            Rect = o.Rect;
            Near = o.Near;
            Far = o.Far;
            FieldOfView = o.FieldOfView;
            Orthographic = o.Orthographic;
            OrthographicSize = o.OrthographicSize;
            Depth = o.Depth;
            CullingMask = o.CullingMask;
            EventMask = o.EventMask;
            RenderingPath = o.RenderingPath;
            TargetTexture = o.TargetTexture;
            TargetDisplay = o.TargetDisplay;
            AllowHDR = o.AllowHDR;
            AllowMSAA = o.AllowMSAA;
            OcclusionCulling = o.OcclusionCulling;
            DepthTextureMode = o.DepthTextureMode;
            TransparencySortMode = o.TransparencySortMode;
            TransparencySortAxis = o.TransparencySortAxis;
            AspectOverride = o.AspectOverride;
            WorldToCameraOverride = o.WorldToCameraOverride;
            ProjectionOverride = o.ProjectionOverride;
            TargetColorBuffers = (RenderBuffer[])o.TargetColorBuffers?.Clone();
            TargetDepthBuffer = o.TargetDepthBuffer;
        }

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            CopySettings((CameraNative)source);
        }

        /// Camera (class 20).
        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            ClearFlags = (CameraClearFlags)f.Get<uint>("m_ClearFlags");
            var c = f.Struct("m_BackGroundColor");
            BackgroundColor = new Color(c.Float("r"), c.Float("g"), c.Float("b"), c.Float("a"));
            var r = f.Struct("m_NormalizedViewPortRect");
            Rect = ClampRect(new Rect(r.Float("x"), r.Float("y"), r.Float("width"), r.Float("height")));
            Near = f.Float("near clip plane");
            Far = f.Float("far clip plane");
            FieldOfView = f.Float("field of view");
            Orthographic = f.Bool("orthographic");
            OrthographicSize = f.Float("orthographic size");
            Depth = f.Float("m_Depth");
            CullingMask = (int)f.Struct("m_CullingMask").Get<uint>("m_Bits");
            RenderingPath = (RenderingPath)f.Int("m_RenderingPath");
            TargetTexture = resolver.Resolve(f.PPtr("m_TargetTexture")) as RenderTexture;
            TargetDisplay = f.Int("m_TargetDisplay");
            AllowHDR = f.Bool("m_HDR");
            AllowMSAA = f.Bool("m_AllowMSAA");
            OcclusionCulling = f.Bool("m_OcclusionCulling");
        }
    }

    /// Enabled cameras in the order they were enabled.
    internal static class Cameras
    {
        public static readonly List<Camera> Enabled = new();

        public static void Add(Camera c)
        {
            if (!Enabled.Contains(c)) Enabled.Add(c);
        }

        public static void Remove(Camera c) => Enabled.Remove(c);

        public static Camera Main
        {
            get
            {
                foreach (var c in Enabled)
                    if (c.Native is CameraNative n && n.GameObject.Tag == "MainCamera") return c;
                return null;
            }
        }
    }
}
