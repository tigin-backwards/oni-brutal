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
            // A root screen-space canvas is laid out the moment it becomes enabled (activation,
            // the component's enable, an instantiated active copy), as measured in the original
            // player; the canvas update keeps it current after that. A world-space or nested
            // canvas is not, so a Dropdown's template canvas (world space, as AddComponent leaves
            // it) cloned at the root and nested again keeps its own anchoring.
            if (c.RenderMode != RenderMode.WorldSpace) UpdateRootTransform(c);
            Rehomed(c);
            HierarchyChanged(c);
        }

        public static void Disable(Canvas canvas)
        {
            enabled.Remove(canvas.Data);
            Rehomed(canvas.Data);
            HierarchyChanged(canvas.Data);
        }

        /// overrideSorting changed: the canvas's elements move to another sort root.
        public static void SortingChanged(CanvasNative c) => Rehomed(c);

        // ----- Depth numbering -----
        //
        // As measured in the original (UnityProbe UiSemantics, `UIS depth`): a sort root numbers
        // the CanvasRenderers of the active objects it draws from 0, depth first in hierarchy
        // order, when it renders (after Canvas.willRenderCanvases), and only when its structure
        // changed since it last did: a renderer became active, was added or destroyed, a sibling
        // order changed, a nested canvas or a Mask was enabled or disabled, or the canvas itself
        // was. Nothing else renumbers it: an object deactivated keeps its number (and its gap)
        // until the next renumbering, which leaves it -1. A canvas enabled or disabled, or its
        // overrideSorting changed, leaves the elements at and below it -1 at once.

        /// A structural change at or below a transform: its sort root renumbers at the next build.
        /// The nearest canvas that overrides sorting (or is disabled: a root of its own) holds the
        /// change; a nested canvas that sorts with its parent passes it up.
        public static void Restructured(TransformNative t)
        {
            for (; t != null; t = t.Parent)
            {
                if (PartsOf(t.GameObject).Canvas is not { Destroyed: false } c) continue;
                c.DepthDirty = true;
                if (c.OverrideSorting || !IsLive(c)) return;
            }
        }

        /// A canvas enabled or disabled (or one that stopped or started sorting on its own): the
        /// elements at and below it read -1 until numbered again, and the sort roots among them
        /// and the one above renumber. Measured: disabling a nested canvas leaves its elements -1
        /// at once; disabling and enabling the root leaves every element -1 until it renders.
        static void Rehomed(CanvasNative c)
        {
            if (c.GameObject?.Transform is not { } t) return;
            Reset(t);
            Restructured(t.Parent);
        }

        static void Reset(TransformNative t)
        {
            var parts = PartsOf(t.GameObject);
            if (parts.Canvas is { Destroyed: false } c) c.DepthDirty = true;
            foreach (var r in parts.Renderers) r.DepthBatch = null;
            foreach (var child in t.Children) Reset(child);
        }

        /// Unity tells the scripts on the canvas's object and below (uGUI's graphics, masks and
        /// raycasters listen) that the canvas they belong to may have changed.
        static void HierarchyChanged(CanvasNative c) => Messages.SendToSubtree(c.GameObject, s => s.OnCanvasHierarchyChanged);

        static bool IsLive(CanvasNative c) => c.IsActiveAndEnabledState && !c.Destroyed;

        static CanvasNative CanvasOn(GameObjectNative go) => PartsOf(go).Canvas is { } c && IsLive(c) ? c : null;

        /// A GameObject's canvas, enabled or not.
        static CanvasNative AnyCanvasOn(GameObjectNative go) => PartsOf(go).Canvas is { } c && !c.Destroyed ? c : null;

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

        /// A canvas's root: the topmost canvas above it, up to and including the first disabled
        /// one. As measured in the original (UnityProbe CanvasProbe): a disabled canvas, like one
        /// on an inactive object, is a root of its own (isRootCanvas, its own render mode and
        /// scale factor), but it is still the root of the live canvases below it, which keep its
        /// render mode, scale factor and sorting. Its override-sorting descendants draw and take
        /// raycasts in its space; nothing else under it draws.
        public static CanvasNative RootOf(CanvasNative canvas)
        {
            var root = canvas;
            if (!IsLive(canvas)) return root;
            for (var t = canvas.GameObject.Transform.Parent; t != null; t = t.Parent)
                if (AnyCanvasOn(t.GameObject) is { } c)
                {
                    root = c;
                    if (!IsLive(c)) break;
                }
            return root;
        }

        /// The canvas whose sorting layer and order a canvas uses: the nearest canvas at or above
        /// it that overrides sorting, or its root (RootOf).
        public static CanvasNative SortingCanvasOf(CanvasNative canvas)
        {
            if (canvas.OverrideSorting || !IsLive(canvas)) return canvas;
            var result = canvas;
            for (var t = canvas.GameObject.Transform.Parent; t != null; t = t.Parent)
                if (AnyCanvasOn(t.GameObject) is { } c)
                {
                    result = c;
                    if (c.OverrideSorting || !IsLive(c)) return c;
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

        public static Rect PixelRect(CanvasNative root)
        {
            if (root.RenderMode == RenderMode.ScreenSpaceCamera && root.WorldCamera != null)
                return root.WorldCamera.pixelRect;
            var size = ScreenSize;
            return new Rect(0f, 0f, size.x, size.y);
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

        /// Canvas.preWillRenderCanvases and willRenderCanvases (uGUI's layout and graphic
        /// rebuilds). Nothing is sent to the rects changed while they run once they are over: a
        /// RectTransform hears of a change of its size from the change itself, and of a move not
        /// at all (measured in the original player, docs/cleanroom/ugui.md "Moved children of
        /// layout groups"), except on pixel-perfect canvases (SendMovesOnPixelPerfectCanvases,
        /// after it in the frame).
        public static void RunCanvasUpdate()
        {
            Canvas.SendPreWillRenderCanvases();
            Canvas.SendWillRenderCanvases();
        }

        static readonly List<RectTransformNative> movedRects = new();
        static readonly HashSet<RectTransformNative> movedSet = new();

        /// Counts SendMovesOnPixelPerfectCanvases's passes (CanvasRendererNative.BornBeforePass).
        public static int MovePass { get; private set; }

        /// A RectTransform's place in the world changed (a position, rotation, scale or parent
        /// set, its own or an ancestor's). Kept when it has a CanvasRenderer, for the next pass.
        public static void NoteMoved(RectTransformNative rect)
        {
            var go = rect.GameObject;
            if (go == null || go.Components.Length < 2 || PartsOf(go).Renderers.Length == 0) return;
            if (movedSet.Add(rect)) movedRects.Add(rect);
        }

        /// After the canvas update: every active GameObject with a CanvasRenderer that moved since
        /// the last pass gets OnRectTransformDimensionsChange once, when its canvas is pixel
        /// perfect. As measured in the original (docs/cleanroom/ugui.md, "Moves on pixel-perfect
        /// canvases"): any position, rotation, scale or parent change counts, even one undone in
        /// the same frame, and so does an ancestor's; a size change alone, a sibling index, an
        /// activation and an object made or cloned since the last pass do not. On other canvases
        /// a move sends nothing.
        static void SendMovesOnPixelPerfectCanvases()
        {
            int pass = MovePass++;
            if (movedRects.Count == 0) return;
            var moved = movedRects.ToArray();
            movedRects.Clear();
            movedSet.Clear();
            foreach (var rect in moved)
            {
                var go = rect.GameObject;
                if (rect.Destroyed || go == null || go.Destroyed || !go.ActiveInHierarchy) continue;
                bool established = false;
                foreach (var r in PartsOf(go).Renderers)
                    if (!r.Destroyed && r.BornBeforePass != pass) established = true;
                if (!established || NearestCanvas(go) is not { } canvas || !IsPixelPerfect(canvas)) continue;
                Messages.SendToComponents(go, s => s.OnRectTransformDimensionsChange);
            }
        }

        /// Canvas.pixelPerfect: the canvas's own setting when it overrides it or is a root,
        /// else its root's; never in world space.
        static bool IsPixelPerfect(CanvasNative canvas)
        {
            var root = RootOf(canvas);
            if (root.RenderMode == RenderMode.WorldSpace) return false;
            return canvas.OverridePixelPerfect || root == canvas ? canvas.PixelPerfect : root.PixelPerfect;
        }

        static void Update()
        {
            foreach (var c in enabled.ToArray()) UpdateRootTransform(c);
            RunCanvasUpdate();
            SendMovesOnPixelPerfectCanvases();
            if (stats)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                Build();
                statsTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                if (++statsFrames == 600)
                {
                    EngineLog.Engine($"canvas build: {statsTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / statsFrames:F3} ms a frame, {statsVisits / statsFrames} objects walked a frame, {statsRenumbered / (double)statsFrames:F2} sort roots renumbered");
                    statsFrames = 0; statsTicks = 0; statsVisits = 0; statsRenumbered = 0;
                }
            }
            else Build();
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

        internal readonly struct CanvasDraw(CanvasRendererNative renderer, CanvasNative space, Material material, bool pop, float groupAlpha,
            CanvasNative nearest = null, Matrix4x4 toNearest = default)
        {
            public readonly CanvasRendererNative Renderer = renderer;
            public readonly CanvasNative Space = space;
            public readonly Material Material = material;
            public readonly bool Pop = pop;
            /// Alpha inherited from CanvasGroups.
            public readonly float GroupAlpha = groupAlpha;
            /// The nested canvas the element is in, when it is not the root canvas, and the
            /// element's transform in that canvas's space.
            public readonly CanvasNative Nearest = nearest;
            public readonly Matrix4x4 ToNearest = toNearest;
        }

        /// The space an element's geometry is baked in: the element to that space, and that space
        /// to the world. An element in a nested canvas is baked in the nested canvas's space and
        /// drawn through the canvas's transform, so moving or scaling that canvas leaves its
        /// elements' geometry as it is. Unity's player batches a nested canvas the same way:
        /// observed in it, the research screen's tree (a nested canvas of about 5,000 elements)
        /// zooms at the frame time of a still screen, where rebaking and uploading every element
        /// each frame took 30 to 130 ms more, and the screen's zoom, which steps by the frame
        /// time, overshot its target. A rect-clipped element (RectMask2D) stays in the root
        /// canvas's space, where uGUI gives its clip rect. So does an element whose shader reads
        /// no vertex position: such a program places its geometry from unity_ObjectToWorld
        /// alone, which a UI draw sets to the root canvas's transform in the original. Klei's
        /// BatchedAnimationUI is one; its game code computes the Duplicant portraits' positions
        /// around the root canvas's centre, and in the Consumables table (a nested canvas
        /// whose pivot is its top right corner) they were drawn off the screen's corner.
        static (Matrix4x4 ToSpace, Matrix4x4 SpaceToWorld) SpaceOf(CanvasBatch b, in CanvasDraw d)
        {
            if (NestedSpace(d) is { } nested) return (d.ToNearest, nested.GameObject.Transform.LocalToWorld);
            return (b.WorldToSpace * d.Renderer.GameObject.Transform.LocalToWorld, b.SpaceToWorld);
        }

        /// The nested canvas a draw's geometry is baked in, or null for the root canvas's space.
        static CanvasNative NestedSpace(in CanvasDraw d)
            => d.Nearest != null && d.Renderer.ClipRect == null && !d.Nearest.Destroyed && ReadsVertexPosition(d.Material) ? d.Nearest : null;

        static bool ReadsVertexPosition(Material material)
        {
            var shader = material == null ? null : material.shader;
            return shader == null || shader.Native is not ShaderNative native || native.ReadsVertexPosition;
        }

        static void Build()
        {
            buildNumber++;
            lastSortRoots.Clear();
            foreach (var b in batches) lastSortRoots.Add(b.Canvas);
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
                var (alpha, blocks) = above == null ? (1f, true) : InheritedGroups(above.GameObject);
                var c = b.Canvas;
                var depth = counter;
                depth.Value = 0;
                depth.RelativeBase = 0;
                depth.IgnoringGroups = 0;
                // A canvas that was not a sort root at the last build (one moved to the top level)
                // numbers its elements, whatever numbered them before.
                depth.Renumber = c.DepthDirty || c.SortRootBuild != buildNumber - 1;
                c.SortRootBuild = buildNumber;
                if (depth.Renumber)
                {
                    c.DepthDirty = false;
                    c.DepthEpoch++;
                    if (stats) statsRenumbered++;
                }
                Collect(b, c.GameObject, b.Root, isBatchRoot: true, depth, c, c.GameObject.Transform.LocalToWorld.inverse, Matrix4x4.identity, alpha, blocks);
            }
            // A canvas no longer a sort root (one moved under a canvas that draws it, or under a
            // disabled one) takes its numbers back, so uGUI does not raycast into it.
            foreach (var c in lastSortRoots)
                if (c.SortRootBuild != buildNumber)
                {
                    c.DepthEpoch++;
                    c.DepthDirty = true;
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
        static readonly List<CanvasNative> lastSortRoots = new();
        static int buildNumber;

        static CanvasNative SortingOf(CanvasBatch b) => b.Canvas.OverrideSorting || b.Canvas == b.Root ? b.Canvas : b.Root;

        sealed class Counter
        {
            /// Renumbering the batch (its canvas's DepthDirty), and the next number.
            public bool Renumber;
            public int Value;
            /// Value where the nearest canvas began: relativeDepth counts from there.
            public int RelativeBase;
            /// Live CanvasGroups that ignore their parents' groups, met so far in the walk.
            public int IgnoringGroups;
        }

        static readonly Counter counter = new();

        /// Depth-first in hierarchy order; nested sort roots are skipped (they are batches of
        /// their own); an element's pop materials draw after its children. When renumbering
        /// (depth.Renumber), every element on an active object is numbered, as in Unity
        /// (measured: one without a Graphic or geometry, a disabled Graphic, alpha 0 by colour or
        /// through a CanvasGroup whether it blocks raycasts or not, culled by a RectMask2D,
        /// off-screen, zero-size, empty text); an element whose renderer has a pop instruction (a
        /// Mask's) takes one more number after its subtree. A nested canvas that sorts with its
        /// parent continues the parent's numbering, and its relativeDepth starts at 0. Only
        /// unculled elements with geometry that are not fully transparent through their
        /// CanvasGroups are drawn; a group below that ignores its parents' groups can make part of
        /// a transparent subtree visible again.
        ///
        /// A hidden subtree whose groups also stop raycasts can never be drawn or hit, so unless
        /// the batch renumbers there is nothing to do in it. Its top object records when it was
        /// walked so (CanvasHiddenAt) if no group in it ignores its parents; while nothing at or
        /// below it changes (SubtreeVersion), the walk skips it. Most of a large colony's UI is
        /// screens hidden this way (about 8,900 of 11,500 objects visited a frame in U57). Test
        /// runs (ONI_VERIFY_CANVAS_SKIPS=1) check each skipped subtree as the walk would have seen
        /// it and log differences.
        static void Collect(CanvasBatch batch, GameObjectNative go, CanvasNative space, bool isBatchRoot, Counter depth,
            CanvasNative nearest, Matrix4x4 worldToNearest, Matrix4x4 parentToNearest, float groupAlpha, bool blocksRaycasts)
        {
            if (!go.ActiveInHierarchy) return;
            if (stats) statsVisits++;
            var parts = PartsOf(go);
            var canvasHere = parts.Canvas is { } here && !here.Destroyed ? here : null;
            // A nested sort root is a batch of its own; a disabled canvas draws nothing (its
            // override-sorting descendants are batches rooted at it).
            if (!isBatchRoot && canvasHere != null && (canvasHere.OverrideSorting || !IsLive(canvasHere))) return;
            // CanvasGroups multiply down the hierarchy; one that ignores its parents restarts it.
            bool skippedAbove = groupAlpha <= 0f && !blocksRaycasts;
            foreach (var g in parts.Groups)
                if (g.IsActiveAndEnabledState)
                {
                    if (g.IgnoreParentGroups) depth.IgnoringGroups++;
                    groupAlpha = g.IgnoreParentGroups ? g.Alpha : groupAlpha * g.Alpha;
                    blocksRaycasts = g.IgnoreParentGroups ? g.BlocksRaycasts : blocksRaycasts && g.BlocksRaycasts;
                }
            bool hidden = groupAlpha <= 0f;
            // The top of a hidden subtree that stops raycasts: skipped while unchanged since a walk
            // found it all hidden (only a group ignoring its parents could let raycasts in again,
            // and a subtree with one is never skipped).
            bool hiddenTop = hidden && !blocksRaycasts && !skippedAbove;
            if (hiddenTop && !depth.Renumber && go.CanvasHiddenAt == go.SubtreeVersion)
            {
                if (verifySkips) VerifySkipped(batch.Canvas, go, isTop: true);
                return;
            }
            go.CanvasHiddenAt = -1;
            long version = go.SubtreeVersion;
            int ignoringBefore = depth.IgnoringGroups;
            int relativeBefore = depth.RelativeBase;
            if (canvasHere != null && canvasHere != nearest)
            {
                nearest = canvasHere;
                worldToNearest = go.Transform.LocalToWorld.inverse;
                depth.RelativeBase = depth.Value;
            }
            // Below a canvas other than the root, the object's placement in that canvas's space:
            // the product of the local transforms from the canvas down, so it stays exactly the
            // same while the canvas itself moves or scales (SpaceOf).
            var own = nearest != space ? nearest : null;
            var toNearest = own == null || go == own.GameObject ? Matrix4x4.identity
                : parentToNearest * Matrix4x4.TRS(go.Transform.LocalPosition, go.Transform.LocalRotation, go.Transform.LocalScale);
            List<CanvasRendererNative> pops = null;
            bool numbersPop = false;
            foreach (var r in parts.Renderers)
            {
                if (r.Destroyed) continue;
                if (depth.Renumber)
                {
                    r.DepthBatch = batch.Canvas;
                    r.DepthEpoch = batch.Canvas.DepthEpoch;
                    r.AbsoluteDepth = depth.Value;
                    r.RelativeDepth = depth.Value - depth.RelativeBase;
                    depth.Value++;
                    numbersPop |= r.HasPopInstruction;
                }
                if (hidden && !blocksRaycasts) continue;
                r.BuiltToCanvas = worldToNearest * r.GameObject.Transform.LocalToWorld;
                if (r.Cull || hidden || r.Geometry == null || r.Geometry.Indices.Length == 0) continue;
                foreach (var m in r.Materials) batch.Draws.Add(new CanvasDraw(r, space, m, pop: false, groupAlpha, own, toNearest));
                if (r.HasPopInstruction) (pops ??= new()).Add(r);
            }
            foreach (var child in go.Transform.Children)
                Collect(batch, child.GameObject, space, isBatchRoot: false, depth, nearest, worldToNearest, toNearest, groupAlpha, blocksRaycasts);
            if (pops != null)
                foreach (var r in pops)
                    foreach (var m in r.PopMaterials) batch.Draws.Add(new CanvasDraw(r, space, m, pop: true, groupAlpha, own, toNearest));
            if (numbersPop)
                foreach (var r in parts.Renderers)
                    if (!r.Destroyed && r.HasPopInstruction) depth.Value++;
            depth.RelativeBase = relativeBefore;
            if (hiddenTop && depth.IgnoringGroups == ignoringBefore) go.CanvasHiddenAt = version;
        }

        static readonly bool verifySkips = Environment.GetEnvironmentVariable("ONI_VERIFY_CANVAS_SKIPS") == "1";
        /// ONI_CANVAS_STATS=1: the build's time and the objects it walks, a frame, every 600 frames.
        static readonly bool stats = Environment.GetEnvironmentVariable("ONI_CANVAS_STATS") == "1";
        static int statsFrames;
        static long statsTicks, statsVisits, statsRenumbered;
        static long skipsVerified, skipsWrong;

        /// A skipped hidden subtree, as the walk would visit it: every element numbered by the
        /// batch's last renumbering and no live group that ignores its parents.
        static void VerifySkipped(CanvasNative batch, GameObjectNative go, bool isTop)
        {
            if (isTop && ++skipsVerified % 10000 == 0) EngineLog.Engine($"canvas skips: {skipsVerified} verified, {skipsWrong} wrong");
            if (!go.ActiveInHierarchy) return;
            if (stats) statsVisits++;
            var parts = PartsOf(go);
            if (!isTop && parts.Canvas is { } c && !c.Destroyed && (c.OverrideSorting || !IsLive(c))) return;
            bool wrong = false;
            foreach (var g in parts.Groups) wrong |= g.IsActiveAndEnabledState && g.IgnoreParentGroups;
            foreach (var r in parts.Renderers) wrong |= !r.Destroyed && (r.DepthBatch != batch || !r.HasDepth);
            if (wrong) EngineLog.Engine($"canvas skip wrong ({++skipsWrong}) at {go.Name}");
            foreach (var child in go.Transform.Children) VerifySkipped(batch, child.GameObject, isTop: false);
        }

        /// Diagnostics (`--probe-ui`): the elements of the last build drawn at a screen point
        /// (pixels from the bottom left), in drawing order, with what they draw.
        public static string Probe(Vector2 screen)
        {
            var text = new System.Text.StringBuilder($"canvas elements at ({screen.x}, {screen.y}):");
            foreach (var b in batches)
            {
                if (!IsOverlay(b.Root)) continue;
                foreach (var d in b.Draws)
                {
                    var p = SpaceOf(b, d).SpaceToWorld.inverse.MultiplyPoint3x4(new Vector3(screen.x, screen.y, 0f));
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
        /// The CanvasGroups' alpha at an object, and whether they let raycasts through.
        internal static (float Alpha, bool BlocksRaycasts) InheritedGroups(GameObjectNative go)
        {
            float alpha = 1f;
            bool blocks = true;
            for (var t = go.Transform; t != null; t = t.Parent)
                foreach (var g in PartsOf(t.GameObject).Groups)
                    if (g.IsActiveAndEnabledState)
                    {
                        alpha *= g.Alpha;
                        blocks &= g.BlocksRaycasts;
                        if (g.IgnoreParentGroups) return (alpha, blocks);
                    }
            return (alpha, blocks);
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
            var (toSpace, spaceToWorld) = SpaceOf(b, d);
            var mesh = BuildMesh(r, toSpace, new Color(r.Color.r, r.Color.g, r.Color.b, alpha));
            var texture = r.Texture ?? d.Material.mainTexture;
            var state = new UiDrawState(
                d.Pop ? null : r.Texture,
                r.AlphaTexture,
                r.SecondaryTextures,
                texture is Texture2D t2 && t2.format == TextureFormat.Alpha8 ? new Vector4(1f, 1f, 1f, 0f) : Vector4.zero,
                r.ClipRect is { } clip ? new Vector4(clip.xMin, clip.yMin, clip.xMax, clip.yMax) : new Vector4(-32767f, -32767f, 32767f, 32767f),
                r.ClipRect != null ? r.ClippingSoftness : Vector2.zero,
                r.ClipRect != null,
                overlay ? CompareAlways : CompareLessEqual);
            var props = SheetFor(r, d.Material, d.Pop, state);
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

        /// What an element's draw puts in its property sheet: the sheet's whole content, so an
        /// unchanged state leaves the sheet as it is, and equal states mean equal sheets.
        internal readonly record struct UiDrawState(Texture MainTexture, Texture AlphaTexture, (string Name, Texture2D Texture)[] Secondary,
                                           Vector4 SampleAdd, Vector4 Clip, Vector2 Softness, bool Clipped, float ZTest)
        {
            public void Fill(PropertySheet props)
            {
                props.Clear();
                if (MainTexture != null) props.SetTexture(MainTex, MainTexture);
                if (AlphaTexture != null) props.SetTexture(AlphaTex, AlphaTexture);
                foreach (var (name, tex) in Secondary)
                    if (name != null) props.SetTexture(ShaderPropertyIds.Get(name), tex);
                props.SetVector(TextureSampleAdd, SampleAdd);
                props.SetVector(ClipRectId, Clip);
                if (Clipped)
                {
                    props.SetFloat(SoftnessX, Softness.x);
                    props.SetFloat(SoftnessY, Softness.y);
                }
                props.SetFloat(ZTestMode, ZTest);
            }

            /// Same sheet content. Secondary texture lists are compared by reference; lists equal
            /// only in content are compared as sheets (SameState).
            public bool Same(in UiDrawState o)
                => ReferenceEquals(MainTexture, o.MainTexture) && ReferenceEquals(AlphaTexture, o.AlphaTexture) && ReferenceEquals(Secondary, o.Secondary)
                   && SampleAdd == o.SampleAdd && Clip == o.Clip && Clipped == o.Clipped && (!Clipped || Softness == o.Softness) && ZTest == o.ZTest;
        }

        /// The property sheet an element's draw with a material fills, kept on the element: the
        /// frame's draws are recorded before the next frame refills it. Refilled only when the
        /// draw's state changed: over 700 elements are drawn each frame in a large colony, nearly
        /// all of them unchanged.
        static PropertySheet SheetFor(CanvasRendererNative r, Material material, bool pop, in UiDrawState state)
        {
            var sheets = r.DrawSheets ??= new();
            for (int i = 0; i < sheets.Count; i++)
            {
                var (m, p, sheet, filled) = sheets[i];
                if (!ReferenceEquals(m, material) || p != pop) continue;
                if (!filled.Same(state))
                {
                    state.Fill(sheet);
                    sheets[i] = (m, p, sheet, state);
                }
                return sheet;
            }
            // Materials an element no longer uses are dropped once a few have gathered.
            if (sheets.Count >= 8) sheets.Clear();
            var created = new PropertySheet();
            state.Fill(created);
            sheets.Add((material, pop, created, state));
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
            UiDrawState runState = default;
            CanvasNative runSpace = null;
            nestedDraws.Clear();
            void Flush()
            {
                if (run.Count == 0) return;
                var item = run.Count == 1 ? run[0].Item : Merge(run);
                items.Add(item);
                if (runSpace != null) nestedDraws.Add((runSpace, item));
                run.Clear();
            }
            foreach (var d in b.Draws)
            {
                if (Item(b, d, overlay) is not { } item) continue;
                var state = StateOf(d.Renderer, item);
                var space = NestedSpace(d);
                // Elements baked in different spaces (a nested canvas's, the root's) draw apart.
                if (run.Count > 0 && (!SameState(run[0].Item, runState, item, state) || runSpace != space)) Flush();
                if (run.Count == 0)
                {
                    runState = state;
                    runSpace = space;
                }
                run.Add((item, d.Renderer));
            }
            Flush();
            if (nestedDraws.Count > 0) ShareNestedMeshes();
            return items;
        }

        static readonly List<(CanvasNative Space, DrawItem Item)> nestedDraws = new();

        sealed class NestedMesh
        {
            public MeshNative[] Parts = Array.Empty<MeshNative>();
            public int[] Versions = Array.Empty<int>();
            public readonly MeshNative Mesh = new() { Dynamic = true };
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CanvasNative, NestedMesh> nestedMeshes = new();

        /// A nested canvas's draws take their geometry from one mesh, a submesh each, as Unity
        /// keeps a canvas's batch in one buffer: the device then makes and fills a few buffers
        /// for the canvas rather than a few for each of its elements. The research screen's tree
        /// (about 3,800 draws) took 25,000 buffers when first shown, 370 to 470 ms on the GL
        /// thread where the original shows it within a frame of 84 ms. The mesh is rebuilt when
        /// one of its parts changed; a nested canvas that moves or scales does not change them.
        static void ShareNestedMeshes()
        {
            var group = new List<DrawItem>();
            while (nestedDraws.Count > 0)
            {
                // Each nested canvas's draws, in drawing order (they need not be consecutive:
                // a canvas nested in another one draws between its parent's elements).
                var space = nestedDraws[0].Space;
                group.Clear();
                foreach (var (s, item) in nestedDraws)
                    if (s == space) group.Add(item);
                nestedDraws.RemoveAll(e => e.Space == space);
                var shared = nestedMeshes.GetValue(space, _ => new NestedMesh());
                bool same = shared.Parts.Length == group.Count;
                for (int i = 0; same && i < group.Count; i++)
                    same = ReferenceEquals(shared.Parts[i], group[i].Mesh) && shared.Versions[i] == group[i].Mesh.Version;
                if (!same)
                {
                    shared.Parts = new MeshNative[group.Count];
                    shared.Versions = new int[group.Count];
                    for (int i = 0; i < group.Count; i++)
                    {
                        shared.Parts[i] = group[i].Mesh;
                        shared.Versions[i] = group[i].Mesh.Version;
                    }
                    Concatenate(shared.Parts, shared.Mesh, subMeshEach: true);
                }
                for (int i = 0; i < group.Count; i++)
                {
                    group[i].Mesh = shared.Mesh;
                    group[i].SubMesh = i;
                }
            }
        }

        static UiDrawState StateOf(CanvasRendererNative r, DrawItem item)
        {
            foreach (var (_, _, sheet, state) in r.DrawSheets)
                if (ReferenceEquals(sheet, item.Properties)) return state;
            throw new InvalidOperationException("A canvas draw's sheet is not the element's.");
        }

        static bool SameState(DrawItem a, in UiDrawState sa, DrawItem b, in UiDrawState sb)
        {
            if (!ReferenceEquals(a.Material, b.Material) || !ReferenceEquals(a.Keywords, b.Keywords)) return false;
            if (sa.Same(sb)) return true;
            // Different states mean different sheets, unless secondary textures are involved
            // (one may name a texture the state also sets): then compare the sheets themselves.
            if (sa.Secondary.Length == 0 && sb.Secondary.Length == 0) return false;
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

        /// The parts' geometry in one mesh: one submesh, or one submesh for each part.
        static void Concatenate(MeshNative[] parts, MeshNative mesh, bool subMeshEach = false)
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
            var indexData = subMeshEach ? null : new int[indices];
            var subMeshes = new List<SubMeshData>(subMeshEach ? parts.Length : 1);
            int v = 0, k = 0;
            foreach (var p in parts)
            {
                if (subMeshEach)
                {
                    var part = new int[p.SubMeshes[0].Indices.Length];
                    for (int i = 0; i < part.Length; i++) part[i] = p.SubMeshes[0].Indices[i] + v;
                    subMeshes.Add(new SubMeshData { Indices = part });
                }
                else
                    foreach (int index in p.SubMeshes[0].Indices) indexData[k++] = index + v;
                int n = p.VertexCount;
                Array.Copy(p.Positions, 0, positions, v, n);
                Array.Copy(p.Colors, 0, colors, v, n);
                if (normalData != null)
                    for (int i = 0; i < n; i++) normalData[v + i] = p.Normals != null ? p.Normals[i] : new Vector3(0f, 0f, -1f);
                if (tangentData != null)
                    for (int i = 0; i < n; i++) tangentData[v + i] = p.Tangents != null ? p.Tangents[i] : new Vector4(1f, 0f, 0f, -1f);
                for (int u = 0; u < 4; u++)
                    if (uvData[u] != null && p.Uvs[u] != null) Array.Copy(p.Uvs[u], 0, uvData[u], v, n);
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
            if (!subMeshEach) subMeshes.Add(new SubMeshData { Indices = indexData });
            mesh.SubMeshes = subMeshes;
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
                // Canvas items name no pass (-1).
                FrameRenderer.DrawQueue(device, Batched(b, overlay: true));
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
