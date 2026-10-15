using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OniBrutal.Engine.Rendering
{
    /// One thing to draw for a camera: a submesh with a material, plus what it sorts by.
    internal sealed class DrawItem
    {
        public RendererNative Renderer;
        public MeshNative Mesh;
        public int SubMesh;
        public Matrix4x4 ObjectToWorld;
        public Material Material;
        public int Pass = -1;
        public PropertySheet Properties;
        public int Queue;
        public int SortingLayerValue;
        public int SortingOrder;
        public float Distance;
        public Bounds WorldBounds;
        public int Sequence;
    }

    /// A Graphics.DrawMesh request, drawn by the cameras of the frame it was made in.
    internal sealed class QueuedMesh
    {
        public Mesh Mesh;
        public int SubMesh;
        public Matrix4x4 Matrix;
        public Material Material;
        public int Layer;
        public Camera Camera;
        public PropertySheet Properties;
    }

    /// Unity's built-in forward renderer, independent of the graphics device (ARCHITECTURE §3.5).
    ///
    /// Per frame (when the device renders frames): cameras that render into textures, then
    /// cameras that render to the screen, each set in ascending depth (enable order on ties);
    /// then screen-space overlay canvases. Per camera: OnPreCull, culling, OnPreRender, target
    /// and clear, command buffers at their CameraEvents around the opaque and transparent
    /// passes, OnPostRender, then the OnRenderImage chain.
    internal static class FrameRenderer
    {
        public static Camera Current { get; private set; }

        /// Renderers whose GameObject has been active (registered on first activation).
        static readonly HashSet<RendererNative> renderers = new();
        static readonly List<QueuedMesh> queued = new();
        static readonly HashSet<RendererNative> visibleThisFrame = new();
        static readonly HashSet<RendererNative> visibleLastFrame = new();

        /// Additional draws for a camera (world-space and camera-space canvases, particles…).
        public static event Action<Camera, CameraNative, List<DrawItem>> CollectDraws;

        /// Screen-space overlay canvases, after every camera.
        public static event Action RenderOverlays;

        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            PlayerLoop.Render += RenderFrame;
            PlayerLoop.Present += EndFrame;
        }

        public static void Register(RendererNative r) => renderers.Add(r);

        public static void Unregister(RendererNative r)
        {
            renderers.Remove(r);
            visibleThisFrame.Remove(r);
            visibleLastFrame.Remove(r);
        }

        public static void Queue(QueuedMesh m) => queued.Add(m);

        static void RenderFrame()
        {
            if (GraphicsDevice.Current.RendersFrames)
            {
                var cameras = Cameras.Enabled.ToArray();
                // Stable sort: offscreen first, then by depth.
                var order = new List<(int Key, float Depth, int Index, Camera Camera)>();
                for (int i = 0; i < cameras.Length; i++)
                {
                    var n = (CameraNative)cameras[i].Native;
                    if (n == null || !n.GameObject.ActiveInHierarchy) continue;
                    bool offscreen = n.TargetTexture != null || n.TargetColorBuffers != null;
                    order.Add((offscreen ? 0 : 1, n.Depth, i, cameras[i]));
                }
                order.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : a.Index.CompareTo(b.Index));
                foreach (var entry in order)
                    if (entry.Camera != null) RenderCamera(entry.Camera);
                RenderOverlays?.Invoke();
            }
            queued.Clear();
            UpdateVisibility();
        }

        static void EndFrame()
        {
            TemporaryRenderTextures.EndFrame();
            GraphicsDevice.Current.Present();
        }

        /// Renderers that no camera saw this frame become invisible.
        static void UpdateVisibility()
        {
            foreach (var r in visibleLastFrame)
            {
                if (visibleThisFrame.Contains(r) || r.Destroyed) continue;
                r.IsVisible = false;
                SendToRendererObject(r, s => s.OnBecameInvisible);
            }
            visibleLastFrame.Clear();
            visibleLastFrame.UnionWith(visibleThisFrame);
            visibleThisFrame.Clear();
        }

        static void SendToRendererObject(RendererNative r, Func<ScriptInfo, Action<MonoBehaviour>> select) => SendToEnabled(r.GameObject, select);

        /// Camera and render messages go to the enabled behaviours of an active GameObject.
        static void SendToEnabled(GameObjectNative go, Func<ScriptInfo, Action<MonoBehaviour>> select)
        {
            if (!go.ActiveInHierarchy) return;
            foreach (var c in go.Components)
            {
                if (c is not MonoBehaviour mb || c.Native is not BehaviourNative { IsActiveAndEnabledState: true } bn || bn.Script == null) continue;
                var m = select(bn.Script);
                if (m != null) Lifecycle.Invoke(m, mb);
            }
        }

        public static void RenderCamera(Camera camera)
        {
            var cam = (CameraNative)camera.NativeOrThrow;
            var previous = Current;
            Current = camera;
            var previousActive = RenderTexture.active;
            try
            {
                RenderCameraCore(camera, cam);
            }
            finally
            {
                Current = previous;
                RenderTexture.active = previousActive;
            }
        }

        static void RenderCameraCore(Camera camera, CameraNative cam)
        {
            var device = GraphicsDevice.Current;
            SendToEnabled(cam.GameObject, s => s.OnPreCull);
            if (cam.Destroyed) return;
            Camera.onPreCull?.Invoke(camera);

            var draws = Cull(camera, cam);

            SendToEnabled(cam.GameObject, s => s.OnPreRender);
            if (cam.Destroyed) return;
            Camera.onPreRender?.Invoke(camera);

            // Image effects render the camera into a temporary target first.
            var effects = new List<(MonoBehaviour Behaviour, Action<MonoBehaviour, RenderTexture, RenderTexture> Effect)>();
            foreach (var c in cam.GameObject.Components)
                if (c is MonoBehaviour mb && c.Native is BehaviourNative { IsActiveAndEnabledState: true } bn && bn.Script?.OnRenderImage is { } effect)
                    effects.Add((mb, effect));

            RenderTexture source = null;
            var size = cam.TargetSize;
            if (effects.Count > 0)
            {
                source = RenderTexture.GetTemporary(Math.Max(1, (int)size.x), Math.Max(1, (int)size.y), 24,
                    cam.AllowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                Bind(device, RenderTargetBinding.Of((RenderTextureNative)source.NativeOrThrow));
            }
            else Bind(device, CameraTarget(cam));
            device.SetViewport(cam.PixelRect);
            var constants = Constants(cam);
            device.SetCamera(constants);
            GLState.BeginCamera(cam);

            switch (cam.ClearFlags)
            {
                case CameraClearFlags.Skybox:
                case CameraClearFlags.SolidColor:
                    // No skybox material is set in this project's RenderSettings: the background
                    // colour fills the target, as Unity does in that case.
                    device.Clear(true, true, cam.BackgroundColor, 1f);
                    break;
                case CameraClearFlags.Depth:
                    device.Clear(false, true, cam.BackgroundColor, 1f);
                    break;
            }

            Execute(camera, cam, CameraEvent.BeforeForwardOpaque);
            DrawQueue(device, draws, opaque: true);
            Execute(camera, cam, CameraEvent.AfterForwardOpaque);
            Execute(camera, cam, CameraEvent.BeforeSkybox);
            Execute(camera, cam, CameraEvent.AfterSkybox);
            Execute(camera, cam, CameraEvent.BeforeImageEffectsOpaque);
            Execute(camera, cam, CameraEvent.AfterImageEffectsOpaque);
            Execute(camera, cam, CameraEvent.BeforeForwardAlpha);
            DrawQueue(device, draws, opaque: false);
            Execute(camera, cam, CameraEvent.AfterForwardAlpha);

            SendToEnabled(cam.GameObject, s => s.OnPostRender);
            Camera.onPostRender?.Invoke(camera);

            if (effects.Count > 0 && source != null)
            {
                Execute(camera, cam, CameraEvent.BeforeImageEffects);
                var finalTarget = cam.TargetTexture;
                for (int i = 0; i < effects.Count; i++)
                {
                    bool last = i == effects.Count - 1;
                    var destination = last ? finalTarget : RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                    RenderTexture.active = destination;
                    Lifecycle.Invoke(mb => effects[i].Effect(mb, source, destination), effects[i].Behaviour);
                    RenderTexture.ReleaseTemporary(source);
                    source = destination;
                }
                Execute(camera, cam, CameraEvent.AfterImageEffects);
            }
            Execute(camera, cam, CameraEvent.AfterEverything);
        }

        static RenderTargetBinding CameraTarget(CameraNative cam)
        {
            if (cam.TargetColorBuffers != null)
            {
                var colors = new RenderTextureNative[cam.TargetColorBuffers.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = ObjectTable.FindByInstanceId(cam.TargetColorBuffers[i].m_RenderTextureInstanceID) as RenderTextureNative;
                var depth = ObjectTable.FindByInstanceId(cam.TargetDepthBuffer.m_RenderTextureInstanceID) as RenderTextureNative;
                return new RenderTargetBinding(colors, depth, 0);
            }
            return RenderTargetBinding.Of(cam.TargetTexture != null ? (RenderTextureNative)cam.TargetTexture.NativeOrThrow : null);
        }

        /// Binds a target, creating render textures on first use.
        internal static void Bind(RenderDevice device, in RenderTargetBinding target)
        {
            if (target.Colors != null)
                foreach (var t in target.Colors)
                    if (t != null) t.Created = true;
            if (target.Depth != null) target.Depth.Created = true;
            device.SetRenderTarget(target);
        }

        static CameraConstants Constants(CameraNative cam)
            => new(cam.WorldToCamera, cam.Projection, Hierarchy.Position(cam.GameObject.Transform), cam.Near, cam.Far, cam.Orthographic, cam.TargetSize);

        static void Execute(Camera camera, CameraNative cam, CameraEvent evt)
        {
            if (!cam.CommandBuffers.TryGetValue(evt, out var list)) return;
            foreach (var buffer in list.ToArray()) CommandExecution.Run(buffer, camera);
        }

        // ----- Culling and draw lists -----

        static List<DrawItem> Cull(Camera camera, CameraNative cam)
        {
            var draws = new List<DrawItem>();
            var frustum = new Frustum(cam.Projection * cam.WorldToCamera);
            int sequence = 0;
            foreach (var r in renderers)
            {
                if (r.Destroyed || !r.Enabled || !r.GameObject.ActiveInHierarchy) continue;
                if ((cam.CullingMask & (1 << r.GameObject.Layer)) == 0) continue;
                var bounds = r.WorldBounds;
                if (!frustum.Intersects(bounds)) continue;
                int before = draws.Count;
                RendererDraws.Collect(r, camera, draws);
                if (draws.Count == before) continue;
                MarkVisible(r);
                for (int i = before; i < draws.Count; i++) draws[i].Sequence = sequence++;
            }
            foreach (var q in queued)
            {
                if (q.Camera != null && q.Camera != camera) continue;
                if ((cam.CullingMask & (1 << q.Layer)) == 0 || q.Mesh == null || q.Material == null) continue;
                var mesh = (MeshNative)q.Mesh.NativeOrThrow;
                var bounds = RendererNative.Transform(mesh.Bounds, q.Matrix);
                if (!frustum.Intersects(bounds)) continue;
                draws.Add(new DrawItem
                {
                    Mesh = mesh,
                    SubMesh = Math.Clamp(q.SubMesh, 0, Math.Max(0, mesh.SubMeshes.Count - 1)),
                    ObjectToWorld = q.Matrix,
                    Material = q.Material,
                    Properties = q.Properties,
                    Queue = q.Material.renderQueue,
                    WorldBounds = bounds,
                    Sequence = sequence++,
                });
            }
            int collected = draws.Count;
            CollectDraws?.Invoke(camera, cam, draws);
            for (int i = collected; i < draws.Count; i++) draws[i].Sequence = sequence++;

            var position = Hierarchy.Position(cam.GameObject.Transform);
            var forward = cam.GameObject.Transform.WorldRotation * Vector3.forward;
            var mode = cam.TransparencySortMode == TransparencySortMode.Default
                ? (cam.Orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective)
                : cam.TransparencySortMode;
            foreach (var d in draws)
            {
                var center = d.WorldBounds.center;
                d.Distance = mode switch
                {
                    TransparencySortMode.Orthographic => Vector3.Dot(center - position, forward),
                    TransparencySortMode.CustomAxis => Vector3.Dot(center, cam.TransparencySortAxis),
                    _ => (center - position).sqrMagnitude,
                };
            }
            return draws;
        }

        static void MarkVisible(RendererNative r)
        {
            if (!visibleThisFrame.Add(r)) return;
            if (r.IsVisible) return;
            r.IsVisible = true;
            SendToRendererObject(r, s => s.OnBecameVisible);
        }

        /// Opaque (queue ≤ 2500): queue, then sorting layer/order, then front to back.
        /// Transparent: sorting layer/order, queue, then back to front. Ties keep submission order.
        static void DrawQueue(RenderDevice device, List<DrawItem> all, bool opaque)
        {
            var list = all.FindAll(d => opaque ? d.Queue <= 2500 : d.Queue > 2500);
            list.Sort((a, b) =>
            {
                int c;
                if (opaque)
                {
                    if ((c = a.Queue.CompareTo(b.Queue)) != 0) return c;
                    if ((c = a.SortingLayerValue.CompareTo(b.SortingLayerValue)) != 0) return c;
                    if ((c = a.SortingOrder.CompareTo(b.SortingOrder)) != 0) return c;
                    if ((c = a.Distance.CompareTo(b.Distance)) != 0) return c;
                }
                else
                {
                    if ((c = a.SortingLayerValue.CompareTo(b.SortingLayerValue)) != 0) return c;
                    if ((c = a.SortingOrder.CompareTo(b.SortingOrder)) != 0) return c;
                    if ((c = a.Queue.CompareTo(b.Queue)) != 0) return c;
                    if ((c = b.Distance.CompareTo(a.Distance)) != 0) return c;
                }
                return a.Sequence.CompareTo(b.Sequence);
            });
            foreach (var d in list)
                device.Draw(new DrawRequest(d.Mesh, d.SubMesh, d.ObjectToWorld, d.Material, d.Pass, d.Properties));
        }
    }

    /// The draws each renderer class submits.
    internal static class RendererDraws
    {
        static readonly int MainTex = ShaderPropertyIds.Get("_MainTex");
        static readonly int AlphaTex = ShaderPropertyIds.Get("_AlphaTex");
        static readonly int EnableExternalAlpha = ShaderPropertyIds.Get("_EnableExternalAlpha");
        static readonly int RendererColor = ShaderPropertyIds.Get("_RendererColor");
        static readonly int Flip = ShaderPropertyIds.Get("_Flip");

        /// Per-renderer geometry for renderer classes outside this file (particles…).
        public static Func<RendererNative, Camera, List<DrawItem>, bool> External;

        public static void Collect(RendererNative r, Camera camera, List<DrawItem> draws)
        {
            switch (r)
            {
                case MeshRendererNative m:
                    CollectMesh(m, draws);
                    break;
                case SpriteRendererNative s:
                    CollectSprite(s, draws);
                    break;
                default:
                    External?.Invoke(r, camera, draws);
                    break;
            }
        }

        static DrawItem Item(RendererNative r, MeshNative mesh, int subMesh, Material material, PropertySheet properties) => new()
        {
            Renderer = r,
            Mesh = mesh,
            SubMesh = subMesh,
            ObjectToWorld = r.GameObject.Transform.LocalToWorld,
            Material = material,
            Properties = properties,
            Queue = material.renderQueue,
            SortingLayerValue = SortingLayers.ValueOf(r.SortingLayerId),
            SortingOrder = r.SortingOrder,
            WorldBounds = r.WorldBounds,
        };

        /// Material i draws submesh i; extra materials draw the last submesh again.
        static void CollectMesh(MeshRendererNative r, List<DrawItem> draws)
        {
            Mesh mesh = null;
            foreach (var c in r.GameObject.Components)
                if (c is MeshFilter f && f.Native is MeshFilterNative fn) { mesh = fn.Mesh; break; }
            if (mesh == null) return;
            var m = (MeshNative)mesh.NativeOrThrow;
            int subMeshes = m.SubMeshes.Count;
            for (int i = 0; i < r.Materials.Length; i++)
            {
                var material = r.Materials[i];
                if (material == null || subMeshes == 0) continue;
                draws.Add(Item(r, m, Math.Min(i, subMeshes - 1), material, r.PropertyBlock));
            }
        }

        /// The sprite's mesh with the renderer colour baked into vertex colours and flips into
        /// positions; _RendererColor and _Flip are neutral so shaders using either path agree.
        static void CollectSprite(SpriteRendererNative r, List<DrawItem> draws)
        {
            var sprite = r.Sprite;
            if (sprite == null || r.Materials.Length == 0) return;
            var s = (SpriteNative)sprite.NativeOrThrow;
            var render = s.Render;
            if (render.Texture == null) return;
            var mesh = r.GeometryMesh(s);
            var props = new PropertySheet();
            if (r.PropertyBlock != null) props.CopyFrom(r.PropertyBlock);
            props.SetTexture(MainTex, render.Texture);
            if (render.AlphaTexture != null)
            {
                props.SetTexture(AlphaTex, render.AlphaTexture);
                props.SetFloat(EnableExternalAlpha, 1f);
            }
            props.SetVector(RendererColor, Vector4.one);
            props.SetVector(Flip, new Vector4(1f, 1f, 0f, 0f));
            foreach (var material in r.Materials)
                if (material != null) draws.Add(Item(r, mesh, 0, material, props));
        }
    }
}
