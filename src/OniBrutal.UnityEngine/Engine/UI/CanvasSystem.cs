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
            // A root canvas's rect is driven by the canvas update (before willRenderCanvases), not
            // on enable: a canvas enabled at the root and reparented in the same call (uGUI's
            // Dropdown clones its canvas-bearing template, then nests it) keeps its own anchoring.
            HierarchyChanged(c);
        }

        public static void Disable(Canvas canvas)
        {
            enabled.Remove(canvas.Data);
            HierarchyChanged(canvas.Data);
        }

        /// Unity tells the scripts on the canvas's object and below (uGUI's graphics, masks and
        /// raycasters listen) that the canvas they belong to may have changed.
        static void HierarchyChanged(CanvasNative c) => Messages.SendToSubtree(c.GameObject, s => s.OnCanvasHierarchyChanged);

        static bool IsLive(CanvasNative c) => c.IsActiveAndEnabledState && !c.Destroyed;

        static CanvasNative CanvasOn(GameObjectNative go) => PartsOf(go).Canvas is { } c && IsLive(c) ? c : null;

        /// A GameObject's canvas components, found once per component list: GameObject.Components
        /// is copy-on-write, so the list is the same array until a component is added or removed.
        /// The canvas walks run every frame over the whole UI; their state (enabled, destroyed,
        /// alpha) is read from the components each time.
        static UiParts PartsOf(GameObjectNative go)
        {
            var parts = go.UiParts;
            var components = go.Components;
            if (parts != null && parts.Source == components) return parts;
            parts = new UiParts { Source = components };
            List<CanvasGroupNative> groups = null;
            List<CanvasRendererNative> renderers = null;
            foreach (var comp in components)
                switch (comp)
                {
                    case Canvas when comp.Native is CanvasNative c:
                        parts.Canvas ??= c;
                        break;
                    case CanvasGroup when comp.Native is CanvasGroupNative g:
                        (groups ??= new()).Add(g);
                        break;
                    case CanvasRenderer when comp.Native is CanvasRendererNative r:
                        (renderers ??= new()).Add(r);
                        break;
                }
            parts.Groups = groups?.ToArray() ?? Array.Empty<CanvasGroupNative>();
            parts.Renderers = renderers?.ToArray() ?? Array.Empty<CanvasRendererNative>();
            go.UiParts = parts;
            return parts;
        }

        /// The topmost live canvas at or above this canvas.
        public static CanvasNative RootOf(CanvasNative canvas)
        {
            var root = canvas;
            for (var t = canvas.GameObject.Transform.Parent; t != null; t = t.Parent)
                if (CanvasOn(t.GameObject) is { } c) root = c;
            return root;
        }

        /// The canvas whose sorting layer and order a canvas uses: the nearest live canvas at or
        /// above it that overrides sorting, or its root.
        public static CanvasNative SortingCanvasOf(CanvasNative canvas)
        {
            if (canvas.OverrideSorting) return canvas;
            var result = canvas;
            for (var t = canvas.GameObject.Transform.Parent; t != null; t = t.Parent)
                if (CanvasOn(t.GameObject) is { } c)
                {
                    result = c;
                    if (c.OverrideSorting) return c;
                }
            return result;
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
            /// The root canvas's local-to-world matrix and its inverse (geometry space).
            public Matrix4x4 SpaceToWorld, WorldToSpace;
        }

        internal readonly struct CanvasDraw(CanvasRendererNative renderer, CanvasNative space, Material material, bool pop, float groupAlpha)
        {
            public readonly CanvasRendererNative Renderer = renderer;
            public readonly CanvasNative Space = space;
            public readonly Material Material = material;
            public readonly bool Pop = pop;
            /// Alpha inherited from CanvasGroups.
            public readonly float GroupAlpha = groupAlpha;
        }

        static void Build()
        {
            batches.Clear();
            int reused = 0;
            foreach (var c in enabled)
                if (IsLive(c) && c.GameObject.ActiveInHierarchy && (RootOf(c) == c || c.OverrideSorting))
                {
                    // Batch objects (and their draw lists) are reused from build to build.
                    CanvasBatch batch;
                    if (reused < batchPool.Count) batch = batchPool[reused];
                    else batchPool.Add(batch = new CanvasBatch());
                    reused++;
                    batch.Canvas = c;
                    batch.Root = RootOf(c);
                    batch.Draws.Clear();
                    batches.Add(batch);
                }
            // Geometry is in the root canvas's space: RectMask2D (shipped uGUI) computes its clip
            // rects in the root-most canvas's space, nested canvases included.
            foreach (var b in batches)
            {
                b.SpaceToWorld = b.Root.GameObject.Transform.LocalToWorld;
                b.WorldToSpace = b.SpaceToWorld.inverse;
                var above = b.Canvas.GameObject.Transform.Parent;
                float alpha = above == null ? 1f : InheritedAlpha(above.GameObject);
                Collect(b, b.Canvas.GameObject, b.Root, isBatchRoot: true, new Counter(), b.Canvas, b.Canvas.GameObject.Transform.LocalToWorld.inverse, alpha);
            }
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

        static readonly List<CanvasBatch> batchPool = new();

        static CanvasNative SortingOf(CanvasBatch b) => b.Canvas.OverrideSorting || b.Canvas == b.Root ? b.Canvas : b.Root;

        sealed class Counter
        {
            public int Value;
        }

        /// Depth-first in hierarchy order; nested sort roots are skipped (they are batches of
        /// their own); an element's pop materials draw after its children. Elements fully
        /// transparent through their CanvasGroups are not rendered (depth -1); a group below
        /// that ignores its parents' groups can make part of such a subtree visible again.
        static void Collect(CanvasBatch batch, GameObjectNative go, CanvasNative space, bool isBatchRoot, Counter depth,
            CanvasNative nearest, Matrix4x4 worldToNearest, float groupAlpha)
        {
            if (!go.ActiveInHierarchy) return;
            var parts = PartsOf(go);
            var canvasHere = parts.Canvas is { } here && IsLive(here) ? here : null;
            if (!isBatchRoot && canvasHere != null && canvasHere.OverrideSorting) return;
            // CanvasGroups multiply down the hierarchy; one that ignores its parents restarts it.
            foreach (var g in parts.Groups)
                if (g.IsActiveAndEnabledState)
                    groupAlpha = g.IgnoreParentGroups ? g.Alpha : groupAlpha * g.Alpha;
            bool hidden = groupAlpha <= 0f;
            if (canvasHere != null && canvasHere != nearest)
            {
                nearest = canvasHere;
                worldToNearest = go.Transform.LocalToWorld.inverse;
            }
            List<CanvasRendererNative> pops = null;
            foreach (var r in parts.Renderers)
            {
                if (r.Destroyed) continue;
                r.AbsoluteDepth = -1;
                r.RelativeDepth = -1;
                if (hidden) continue;
                r.BuiltToCanvas = worldToNearest * r.GameObject.Transform.LocalToWorld;
                if (r.Cull || r.Geometry == null || r.Geometry.Indices.Length == 0) continue;
                r.AbsoluteDepth = depth.Value;
                r.RelativeDepth = depth.Value;
                depth.Value++;
                foreach (var m in r.Materials) batch.Draws.Add(new CanvasDraw(r, space, m, pop: false, groupAlpha));
                if (r.HasPopInstruction) (pops ??= new()).Add(r);
            }
            foreach (var child in go.Transform.Children)
                Collect(batch, child.GameObject, space, isBatchRoot: false, depth, nearest, worldToNearest, groupAlpha);
            if (pops != null)
                foreach (var r in pops)
                    foreach (var m in r.PopMaterials) batch.Draws.Add(new CanvasDraw(r, space, m, pop: true, groupAlpha));
        }

        /// Diagnostics (`--probe-ui`): the elements of the last build drawn at a screen point
        /// (pixels from the bottom left), in drawing order, with what they draw.
        public static string Probe(Vector2 screen)
        {
            var text = new System.Text.StringBuilder($"canvas elements at ({screen.x}, {screen.y}):");
            foreach (var b in batches)
            {
                if (!IsOverlay(b.Root)) continue;
                var p = b.WorldToSpace.MultiplyPoint3x4(new Vector3(screen.x, screen.y, 0f));
                foreach (var d in b.Draws)
                {
                    var mesh = d.Renderer.Baked;
                    if (mesh == null || mesh.VertexCount == 0) continue;
                    var bounds = mesh.Bounds;
                    if (p.x < bounds.min.x || p.x > bounds.max.x || p.y < bounds.min.y || p.y > bounds.max.y) continue;
                    var path = new System.Text.StringBuilder();
                    for (var t = d.Renderer.GameObject.Transform; t != null; t = t.Parent) path.Insert(0, "/" + t.GameObject.Name);
                    var texture = d.Renderer.Texture ?? d.Material?.mainTexture;
                    var tnat = texture == null ? null : texture.Native as TextureNative;
                    text.Append($"\n  {path} depth {d.Renderer.AbsoluteDepth} material {d.Material?.name} ({d.Material?.shader?.name}) texture {texture?.name ?? "none"}{(texture == null ? "" : $" [native {(tnat == null ? "null" : $"#{tnat.InstanceId} v{tnat.ContentVersion} gpu {tnat.Gpu?.GetType().Name ?? "none"}")}]")} colour {d.Renderer.Color} group alpha {d.GroupAlpha}{(d.Pop ? " pop" : "")}");
                }
            }
            return text.ToString();
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
                foreach (var g in PartsOf(t.GameObject).Groups)
                    if (g.IsActiveAndEnabledState)
                    {
                        alpha *= g.Alpha;
                        if (g.IgnoreParentGroups) return alpha;
                    }
            return alpha;
        }

        static DrawItem Item(CanvasBatch b, in CanvasDraw d, bool overlay)
        {
            var r = d.Renderer;
            // An element destroyed, culled or cleared after the build (later in PostLateUpdate:
            // the game's video screen clears and tears itself down when its video ends) is not
            // drawn, as the build would not have collected it.
            if (r.Destroyed || r.Cull || r.Geometry == null || r.Geometry.Indices.Length == 0) return null;
            float alpha = r.Color.a * d.GroupAlpha;
            // An element whose inherited alpha (renderer alpha × CanvasGroups) is zero is not
            // drawn, as in Unity (screens hidden through a CanvasGroup stay active).
            if (d.Material == null || alpha <= 0f) return null;
            var spaceToWorld = b.SpaceToWorld;
            var mesh = BuildMesh(r, b.WorldToSpace * r.GameObject.Transform.LocalToWorld, new Color(r.Color.r, r.Color.g, r.Color.b, alpha));
            var props = SheetFor(r, d.Material, d.Pop);
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
            var item = FrameRenderer.NewDrawItem();
            item.Mesh = mesh;
            item.ObjectToWorld = spaceToWorld;
            item.Material = d.Material;
            item.Properties = props;
            item.Keywords = r.ClipRect != null ? ClipKeywords : null;
            item.Queue = d.Material.renderQueue;
            return item;
        }

        static readonly string[] ClipKeywords = ["UNITY_UI_CLIP_RECT"];

        /// The property sheet an element's draw with a material fills each frame, kept on the
        /// element: the frame's draws are recorded before the next frame refills it.
        static PropertySheet SheetFor(CanvasRendererNative r, Material material, bool pop)
        {
            var sheets = r.DrawSheets ??= new();
            foreach (var (m, p, sheet) in sheets)
                if (ReferenceEquals(m, material) && p == pop)
                {
                    sheet.Clear();
                    return sheet;
                }
            // Materials an element no longer uses are dropped once a few have gathered.
            if (sheets.Count >= 8) sheets.Clear();
            var created = new PropertySheet();
            sheets.Add((material, pop, created));
            return created;
        }

        // ----- Batching -----

        /// The batch's draws with consecutive draws of identical state (material, keywords,
        /// property values) merged into one mesh, as Unity's canvas batching does; the drawing
        /// order is unchanged.
        static List<DrawItem> Batched(CanvasBatch b, bool overlay)
        {
            var items = FrameRenderer.NewDrawList();
            var run = new List<(DrawItem Item, CanvasRendererNative Renderer)>();
            void Flush()
            {
                if (run.Count == 1) items.Add(run[0].Item);
                else if (run.Count > 1) items.Add(Merge(run));
                run.Clear();
            }
            foreach (var d in b.Draws)
            {
                if (Item(b, d, overlay) is not { } item) continue;
                if (run.Count > 0 && !SameState(run[0].Item, item)) Flush();
                run.Add((item, d.Renderer));
            }
            Flush();
            return items;
        }

        static bool SameState(DrawItem a, DrawItem b)
        {
            if (a.Material != b.Material || !ReferenceEquals(a.Keywords, b.Keywords)) return false;
            var x = a.Properties;
            var y = b.Properties;
            if (x.Textures.Count != y.Textures.Count || x.Vectors.Count != y.Vectors.Count || x.Floats.Count != y.Floats.Count) return false;
            foreach (var (id, t) in x.Textures)
                if (!y.Textures.TryGetValue(id, out var u) || !ReferenceEquals(t.Texture, u.Texture) || t.ScaleOffset != u.ScaleOffset) return false;
            foreach (var (id, v) in x.Vectors)
                if (!y.Vectors.TryGetValue(id, out var w) || v != w) return false;
            foreach (var (id, f) in x.Floats)
                if (!y.Floats.TryGetValue(id, out var g) || f != g) return false;
            return true;
        }

        sealed class MergedRun
        {
            public MeshNative[] Parts = Array.Empty<MeshNative>();
            public int[] Versions = Array.Empty<int>();
            public readonly MeshNative Mesh = new() { Dynamic = true };
        }

        /// Merged meshes by the run's first renderer and material, reused while the parts are
        /// the same meshes at the same versions.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CanvasRendererNative, Dictionary<Material, MergedRun>> merged = new();

        static DrawItem Merge(List<(DrawItem Item, CanvasRendererNative Renderer)> run)
        {
            var first = run[0];
            var runs = merged.GetValue(first.Renderer, _ => new Dictionary<Material, MergedRun>());
            if (!runs.TryGetValue(first.Item.Material, out var m)) runs[first.Item.Material] = m = new MergedRun();
            bool same = m.Parts.Length == run.Count;
            for (int i = 0; same && i < run.Count; i++)
                same = ReferenceEquals(m.Parts[i], run[i].Item.Mesh) && m.Versions[i] == run[i].Item.Mesh.Version;
            if (!same)
            {
                m.Parts = new MeshNative[run.Count];
                m.Versions = new int[run.Count];
                for (int i = 0; i < run.Count; i++)
                {
                    m.Parts[i] = run[i].Item.Mesh;
                    m.Versions[i] = run[i].Item.Mesh.Version;
                }
                Concatenate(m.Parts, m.Mesh);
            }
            var item = first.Item;
            item.Mesh = m.Mesh;
            return item;
        }

        static void Concatenate(MeshNative[] parts, MeshNative mesh)
        {
            int vertices = 0, indices = 0;
            bool normals = false, tangents = false;
            var uvs = new bool[4];
            foreach (var p in parts)
            {
                vertices += p.VertexCount;
                indices += p.SubMeshes[0].Indices.Length;
                normals |= p.Normals != null;
                tangents |= p.Tangents != null;
                for (int u = 0; u < 4; u++) uvs[u] |= p.Uvs[u] != null;
            }
            var positions = new Vector3[vertices];
            var colors = new Color[vertices];
            var normalData = normals ? new Vector3[vertices] : null;
            var tangentData = tangents ? new Vector4[vertices] : null;
            var uvData = new Vector4[4][];
            for (int u = 0; u < 4; u++) uvData[u] = uvs[u] ? new Vector4[vertices] : null;
            var indexData = new int[indices];
            int v = 0, k = 0;
            foreach (var p in parts)
            {
                int n = p.VertexCount;
                Array.Copy(p.Positions, 0, positions, v, n);
                Array.Copy(p.Colors, 0, colors, v, n);
                if (normalData != null)
                    for (int i = 0; i < n; i++) normalData[v + i] = p.Normals != null ? p.Normals[i] : new Vector3(0f, 0f, -1f);
                if (tangentData != null)
                    for (int i = 0; i < n; i++) tangentData[v + i] = p.Tangents != null ? p.Tangents[i] : new Vector4(1f, 0f, 0f, -1f);
                for (int u = 0; u < 4; u++)
                    if (uvData[u] != null && p.Uvs[u] != null) Array.Copy(p.Uvs[u], 0, uvData[u], v, n);
                foreach (int index in p.SubMeshes[0].Indices) indexData[k++] = index + v;
                v += n;
            }
            mesh.Clear();
            mesh.VertexCount = vertices;
            mesh.Positions = positions;
            mesh.Colors = colors;
            mesh.Normals = normalData;
            mesh.Tangents = tangentData;
            for (int u = 0; u < 4; u++)
            {
                mesh.Uvs[u] = uvData[u];
                mesh.UvDimensions[u] = uvData[u] != null ? 4 : 0;
            }
            mesh.SubMeshes = new List<SubMeshData> { new SubMeshData { Indices = indexData } };
            mesh.RecalculateBounds();
            mesh.Changed();
        }

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
                foreach (var item in Batched(b, overlay: true))
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
                foreach (var item in Batched(b, overlay: false))
                {
                    item.SortingLayerValue = SortingLayers.ValueOf(sorting.SortingLayerId);
                    item.SortingOrder = sorting.SortingOrder;
                    item.WorldBounds = new Bounds(center, Vector3.zero);
                    draws.Add(item);
                }
            }
        }
    }

    /// The canvas components of a GameObject's component list (Source), in component order.
    internal sealed class UiParts
    {
        public Component[] Source;
        public CanvasNative Canvas;
        public CanvasGroupNative[] Groups;
        public CanvasRendererNative[] Renderers;
    }
}
