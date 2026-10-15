using System;
using System.Collections.Generic;
using OniBrutal.Engine.Platform;
using OniBrutal.Engine.Rendering;
using UnityEngine;

namespace OniBrutal.Engine.UI
{
    /// Unity's canvas manager: drives root canvas RectTransforms, runs the will-render callbacks,
    /// builds each canvas's draw list (element order and depth) and draws canvases.
    ///
    /// Per frame (PostLateUpdate): root canvas transforms, Canvas.preWillRenderCanvases,
    /// Canvas.willRenderCanvases (uGUI layout and graphic rebuilds), then the build. The build
    /// runs whether or not frames are rendered (depth feeds uGUI raycasting).
    internal static class CanvasSystem
    {
        static readonly List<CanvasNative> enabled = new();
        static int sequence;
        static bool installed;

        static Material defaultMaterial, etc1Material;

        public static Material DefaultMaterial => defaultMaterial != null ? defaultMaterial : defaultMaterial = DefaultFor("UI/Default", "Default UI Material");
        public static Material Etc1Material => etc1Material != null ? etc1Material : etc1Material = DefaultFor("UI/DefaultETC1", "ETC1 Supported UI Material");

        static Material DefaultFor(string shader, string name)
        {
            var s = Shader.Find(shader);
            return s == null ? null : new Material(s) { name = name, hideFlags = HideFlags.HideAndDontSave };
        }

        /// The sort roots built last, in render order.
        static readonly List<CanvasBatch> batches = new();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            PlayerLoop.PostLateUpdate += Update;
            FrameRenderer.RenderOverlays += RenderOverlays;
            FrameRenderer.CollectDraws += CollectCameraDraws;
        }

        public static void Enable(Canvas canvas)
        {
            var c = canvas.Data;
            if (!enabled.Contains(c)) enabled.Add(c);
            c.EnableSequence = ++sequence;
            UpdateRootTransform(c);
        }

        public static void Disable(Canvas canvas) => enabled.Remove(canvas.Data);

        static bool IsLive(CanvasNative c) => c.IsActiveAndEnabledState && !c.Destroyed;

        static CanvasNative CanvasOn(GameObjectNative go)
        {
            foreach (var comp in go.Components)
                if (comp.Native is CanvasNative c && IsLive(c)) return c;
            return null;
        }

        /// The topmost live canvas at or above this canvas.
        public static CanvasNative RootOf(CanvasNative canvas)
        {
            var root = canvas;
            for (var t = canvas.GameObject.Transform.Parent; t != null; t = t.Parent)
                if (CanvasOn(t.GameObject) is { } c) root = c;
            return root;
        }

        /// The nearest live canvas at or above a GameObject.
        public static CanvasNative NearestCanvas(GameObjectNative go)
        {
            for (var t = go.Transform; t != null; t = t.Parent)
                if (CanvasOn(t.GameObject) is { } c) return c;
            return null;
        }

        public static Matrix4x4 ToCanvas(CanvasRendererNative r)
        {
            var canvas = NearestCanvas(r.GameObject);
            var local = r.GameObject.Transform.LocalToWorld;
            return canvas == null ? local : canvas.GameObject.Transform.LocalToWorld.inverse * local;
        }

        static Vector2 ScreenSize
        {
            get
            {
                var p = PlatformServices.Current;
                return new Vector2(p.FramebufferWidth, p.FramebufferHeight);
            }
        }

        public static Vector2 DisplaySize(CanvasNative root)
        {
            if (root.RenderMode == RenderMode.ScreenSpaceCamera && root.WorldCamera != null)
            {
                var cam = (CameraNative)root.WorldCamera.NativeOrThrow;
                return new Vector2(cam.PixelWidth, cam.PixelHeight);
            }
            return ScreenSize;
        }

        /// Screen-space root canvases fill the screen (or their camera's view at planeDistance):
        /// size = screen / scaleFactor, scale = scaleFactor (overlay), anchored at the centre.
        public static void UpdateRootTransform(CanvasNative canvas)
        {
            if (!IsLive(canvas) || RootOf(canvas) != canvas) return;
            if (canvas.GameObject.Transform is not RectTransformNative rt) return;
            if (canvas.RenderMode == RenderMode.WorldSpace) return;
            var size = DisplaySize(canvas);
            float scale = canvas.ScaleFactor;
            var sizeDelta = size / scale;
            Vector3 position;
            Quaternion rotation;
            Vector3 localScale;
            if (canvas.RenderMode == RenderMode.ScreenSpaceCamera && canvas.WorldCamera != null)
            {
                var cam = (CameraNative)canvas.WorldCamera.NativeOrThrow;
                var camTransform = cam.GameObject.Transform;
                float distance = canvas.PlaneDistance;
                rotation = camTransform.WorldRotation;
                position = Hierarchy.Position(camTransform) + rotation * new Vector3(0f, 0f, distance);
                float worldHeight = cam.Orthographic ? cam.OrthographicSize * 2f : 2f * distance * Mathf.Tan(cam.FieldOfView * 0.5f * Mathf.Deg2Rad);
                float s = worldHeight / sizeDelta.y;
                localScale = new Vector3(s, s, s);
            }
            else
            {
                position = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
                rotation = Quaternion.identity;
                localScale = new Vector3(scale, scale, scale);
            }
            bool changed = rt.SizeDelta != sizeDelta || rt.LocalScale != localScale || rt.LocalRotation != rotation
                           || rt.AnchorMin != Vector2.zero || rt.AnchorMax != Vector2.zero || rt.Pivot != new Vector2(0.5f, 0.5f)
                           || new Vector2(rt.LocalPosition.x, rt.LocalPosition.y) != new Vector2(position.x, position.y) || rt.LocalPosition.z != position.z;
            if (!changed) return;
            rt.AnchorMin = Vector2.zero;
            rt.AnchorMax = Vector2.zero;
            rt.Pivot = new Vector2(0.5f, 0.5f);
            rt.SizeDelta = sizeDelta;
            rt.LocalScale = localScale;
            rt.LocalRotation = rotation;
            rt.AnchoredPosition = new Vector2(position.x, position.y);
            rt.LocalPosition = position;
            rt.MarkDirty();
            rt.Resolve();
        }

        /// Whether elements of the canvas snap to whole screen pixels, and the camera that
        /// projects them (null: overlay, where world space is screen pixels).
        public static bool SnapsToPixels(Canvas canvas, out Camera camera)
        {
            camera = null;
            if (canvas == null || !canvas.pixelPerfect) return false;
            var root = RootOf(canvas.Data);
            if (root.RenderMode == RenderMode.WorldSpace) return false;
            if (root.RenderMode == RenderMode.ScreenSpaceCamera) camera = root.WorldCamera;
            return true;
        }

        // ----- Per-frame update -----

        static void Update()
        {
            foreach (var c in enabled.ToArray()) UpdateRootTransform(c);
            Canvas.SendPreWillRenderCanvases();
            Canvas.SendWillRenderCanvases();
            Build();
        }

        /// A root canvas, or a nested canvas that overrides sorting: drawn and depth-counted as
        /// a unit.
        internal sealed class CanvasBatch
        {
            public CanvasNative Canvas;
            public CanvasNative Root;
            public readonly List<CanvasDraw> Draws = new();
        }

        internal readonly struct CanvasDraw(CanvasRendererNative renderer, CanvasNative space, Material material, bool pop)
        {
            public readonly CanvasRendererNative Renderer = renderer;
            public readonly CanvasNative Space = space;
            public readonly Material Material = material;
            public readonly bool Pop = pop;
        }

        static void Build()
        {
            batches.Clear();
            foreach (var c in enabled)
                if (IsLive(c) && c.GameObject.ActiveInHierarchy && (RootOf(c) == c || c.OverrideSorting))
                    batches.Add(new CanvasBatch { Canvas = c, Root = RootOf(c) });
            // Geometry is in the root canvas's space: RectMask2D (shipped uGUI) computes its clip
            // rects in the root-most canvas's space, nested canvases included.
            foreach (var b in batches) Collect(b, b.Canvas.GameObject, b.Root, isBatchRoot: true, new Counter());
            // Render order: by sorting layer, sorting order, then enable order.
            batches.Sort((a, b) =>
            {
                int c = SortingLayers.ValueOf(SortingOf(a).SortingLayerId).CompareTo(SortingLayers.ValueOf(SortingOf(b).SortingLayerId));
                if (c != 0) return c;
                if ((c = SortingOf(a).SortingOrder.CompareTo(SortingOf(b).SortingOrder)) != 0) return c;
                return a.Canvas.EnableSequence.CompareTo(b.Canvas.EnableSequence);
            });
            for (int i = 0; i < batches.Count; i++) batches[i].Canvas.RenderOrder = i;
        }

        static CanvasNative SortingOf(CanvasBatch b) => b.Canvas.OverrideSorting || b.Canvas == b.Root ? b.Canvas : b.Root;

        sealed class Counter
        {
            public int Value;
        }

        /// Depth-first in hierarchy order; nested sort roots are skipped (they are batches of
        /// their own); an element's pop materials draw after its children.
        static void Collect(CanvasBatch batch, GameObjectNative go, CanvasNative space, bool isBatchRoot, Counter depth)
        {
            if (!go.ActiveInHierarchy) return;
            var canvasHere = CanvasOn(go);
            if (!isBatchRoot && canvasHere != null && canvasHere.OverrideSorting) return;
            var pops = new List<CanvasRendererNative>();
            foreach (var comp in go.Components)
            {
                if (comp.Native is not CanvasRendererNative r || r.Destroyed) continue;
                r.AbsoluteDepth = -1;
                r.RelativeDepth = -1;
                r.BuiltToCanvas = ToCanvas(r);
                if (r.Cull || r.Geometry == null || r.Geometry.Indices.Length == 0) continue;
                r.AbsoluteDepth = depth.Value;
                r.RelativeDepth = depth.Value;
                depth.Value++;
                foreach (var m in r.Materials) batch.Draws.Add(new CanvasDraw(r, space, m, pop: false));
                if (r.HasPopInstruction) pops.Add(r);
            }
            foreach (var child in go.Transform.Children) Collect(batch, child.GameObject, space, isBatchRoot: false, depth);
            foreach (var r in pops)
                foreach (var m in r.PopMaterials) batch.Draws.Add(new CanvasDraw(r, space, m, pop: true));
        }

        // ----- Drawing -----

        static readonly int MainTex = ShaderPropertyIds.Get("_MainTex");
        static readonly int AlphaTex = ShaderPropertyIds.Get("_AlphaTex");
        static readonly int ClipRectId = ShaderPropertyIds.Get("_ClipRect");
        static readonly int SoftnessX = ShaderPropertyIds.Get("_UIMaskSoftnessX");
        static readonly int SoftnessY = ShaderPropertyIds.Get("_UIMaskSoftnessY");
        static readonly int ZTestMode = ShaderPropertyIds.Get("unity_GUIZTestMode");
        static readonly int TextureSampleAdd = ShaderPropertyIds.Get("_TextureSampleAdd");

        const float CompareAlways = 8f, CompareLessEqual = 4f;

        /// Group alpha inherited through CanvasGroups up to (and including) one that ignores its
        /// parents.
        static float InheritedAlpha(GameObjectNative go)
        {
            float alpha = 1f;
            for (var t = go.Transform; t != null; t = t.Parent)
                foreach (var c in t.GameObject.Components)
                    if (c.Native is CanvasGroupNative { IsActiveAndEnabledState: true } g)
                    {
                        alpha *= g.Alpha;
                        if (g.IgnoreParentGroups) return alpha;
                    }
            return alpha;
        }

        static DrawItem Item(in CanvasDraw d, bool overlay)
        {
            var r = d.Renderer;
            float alpha = r.Color.a * InheritedAlpha(r.GameObject);
            if (d.Material == null || (r.CullTransparentMesh && alpha <= 0f)) return null;
            var spaceToWorld = d.Space.GameObject.Transform.LocalToWorld;
            var mesh = BuildMesh(r, spaceToWorld.inverse * r.GameObject.Transform.LocalToWorld, new Color(r.Color.r, r.Color.g, r.Color.b, alpha));
            var props = new PropertySheet();
            var texture = r.Texture ?? d.Material.mainTexture;
            if (!d.Pop && r.Texture != null) props.SetTexture(MainTex, r.Texture);
            if (r.AlphaTexture != null) props.SetTexture(AlphaTex, r.AlphaTexture);
            foreach (var (name, tex) in r.SecondaryTextures)
                if (name != null) props.SetTexture(ShaderPropertyIds.Get(name), tex);
            props.SetVector(TextureSampleAdd, texture is Texture2D t2 && t2.format == TextureFormat.Alpha8 ? new Vector4(1f, 1f, 1f, 0f) : Vector4.zero);
            if (r.ClipRect is { } clip)
            {
                props.SetVector(ClipRectId, new Vector4(clip.xMin, clip.yMin, clip.xMax, clip.yMax));
                props.SetFloat(SoftnessX, r.ClippingSoftness.x);
                props.SetFloat(SoftnessY, r.ClippingSoftness.y);
            }
            else props.SetVector(ClipRectId, new Vector4(-32767f, -32767f, 32767f, 32767f));
            props.SetFloat(ZTestMode, overlay ? CompareAlways : CompareLessEqual);
            return new DrawItem
            {
                Mesh = mesh,
                SubMesh = 0,
                ObjectToWorld = spaceToWorld,
                Material = d.Material,
                Properties = props,
                Keywords = r.ClipRect != null ? ClipKeywords : null,
                Queue = d.Material.renderQueue,
            };
        }

        static readonly string[] ClipKeywords = ["UNITY_UI_CLIP_RECT"];

        /// The element's geometry in its canvas's space with its colour applied; rebuilt only
        /// when the geometry, its placement or its colour changed.
        static MeshNative BuildMesh(CanvasRendererNative r, Matrix4x4 toSpace, Color color)
        {
            var g = r.Geometry;
            if (r.Baked != null && ReferenceEquals(r.BakedGeometry, g) && r.BakedToSpace.Equals(toSpace) && r.BakedColor.Equals(color))
                return r.Baked;
            int n = g.Positions.Length;
            var mesh = r.Baked ??= new MeshNative { Dynamic = true };
            mesh.VertexCount = n;
            mesh.Positions = new Vector3[n];
            mesh.Colors = new Color[n];
            r.BakedGeometry = g;
            r.BakedToSpace = toSpace;
            r.BakedColor = color;
            for (int i = 0; i < n; i++)
            {
                mesh.Positions[i] = toSpace.MultiplyPoint3x4(g.Positions[i]);
                var c = g.Colors != null && i < g.Colors.Length ? g.Colors[i] : Color.white;
                mesh.Colors[i] = c * color;
            }
            for (int u = 0; u < 4; u++)
            {
                mesh.Uvs[u] = g.Uvs[u];
                mesh.UvDimensions[u] = g.Uvs[u] != null ? 4 : 0;
            }
            mesh.Normals = g.Normals;
            mesh.Tangents = g.Tangents;
            mesh.SubMeshes = new List<SubMeshData> { new SubMeshData { Indices = g.Indices } };
            mesh.RecalculateBounds();
            mesh.Changed();
            return mesh;
        }

        static bool IsOverlay(CanvasNative root) => root.RenderMode == RenderMode.ScreenSpaceOverlay
                                                     || (root.RenderMode == RenderMode.ScreenSpaceCamera && root.WorldCamera == null);

        /// Overlay canvases draw after every camera, in render order, in screen pixels.
        static void RenderOverlays()
        {
            var device = GraphicsDevice.Current;
            var size = ScreenSize;
            bool bound = false;
            foreach (var b in batches)
            {
                if (!IsOverlay(b.Root) || b.Draws.Count == 0) continue;
                if (!bound)
                {
                    FrameRenderer.Bind(device, FrameRenderer.ScreenBinding);
                    device.SetViewport(new Rect(0f, 0f, size.x * FrameRenderer.ScreenScale, size.y * FrameRenderer.ScreenScale));
                    device.SetCamera(new CameraConstants(Matrix4x4.identity, Matrix4x4.Ortho(0f, size.x, 0f, size.y, -1000f, 1000f), Vector3.zero, -1000f, 1000f, true, size));
                    bound = true;
                }
                foreach (var d in b.Draws)
                    if (Item(d, overlay: true) is { } item)
                        device.Draw(new DrawRequest(item.Mesh, 0, item.ObjectToWorld, item.Material, -1, item.Properties, item.Keywords));
            }
        }

        /// Screen Space - Camera canvases draw with their camera; World Space canvases with
        /// every camera that sees their layer. Each canvas sorts as one unit.
        static void CollectCameraDraws(Camera camera, CameraNative cam, List<DrawItem> draws)
        {
            foreach (var b in batches)
            {
                var root = b.Root;
                if (IsOverlay(root)) continue;
                if (root.RenderMode == RenderMode.ScreenSpaceCamera && root.WorldCamera != camera) continue;
                if (root.RenderMode == RenderMode.WorldSpace && (cam.CullingMask & (1 << b.Canvas.GameObject.Layer)) == 0) continue;
                var sorting = SortingOf(b);
                var center = Hierarchy.Position(b.Canvas.GameObject.Transform);
                foreach (var d in b.Draws)
                {
                    if (Item(d, overlay: false) is not { } item) continue;
                    item.SortingLayerValue = SortingLayers.ValueOf(sorting.SortingLayerId);
                    item.SortingOrder = sorting.SortingOrder;
                    item.WorldBounds = new Bounds(center, Vector3.zero);
                    draws.Add(item);
                }
            }
        }
    }
}
