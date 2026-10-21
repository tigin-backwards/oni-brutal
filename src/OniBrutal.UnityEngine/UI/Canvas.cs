using System;
using System.Collections.Generic;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine;
using OniBrutal.Engine.Rendering;
using OniBrutal.Engine.Serialization;
using OniBrutal.Engine.UI;

namespace UnityEngine
{
    public partial class Canvas
    {
        public static event WillRenderCanvases preWillRenderCanvases;
        public static event WillRenderCanvases willRenderCanvases;

        public Canvas() { }

        internal CanvasNative Data => (CanvasNative)NativeOrThrow;

        internal override void EngineOnEnable() => CanvasSystem.Enable(this);
        internal override void EngineOnDisable() => CanvasSystem.Disable(this);

        /// Nested canvases report their root canvas's mode.
        public RenderMode renderMode
        {
            get => RootData.RenderMode;
            set
            {
                var d = Data;
                d.RenderMode = value;
                CanvasSystem.UpdateRootTransform(d);
            }
        }

        CanvasNative RootData => CanvasSystem.RootOf(Data);

        public bool isRootCanvas => CanvasSystem.RootOf(Data) == Data;

        public Canvas rootCanvas => (Canvas)CanvasSystem.RootOf(Data).Managed;

        /// The camera of a Screen Space - Camera or World Space root canvas.
        public Camera worldCamera
        {
            get => RootData.WorldCamera;
            set
            {
                var d = Data;
                d.WorldCamera = value;
                CanvasSystem.UpdateRootTransform(d);
            }
        }

        public float planeDistance
        {
            get => Data.PlaneDistance;
            set
            {
                var d = Data;
                d.PlaneDistance = value;
                CanvasSystem.UpdateRootTransform(d);
            }
        }

        public float scaleFactor
        {
            get => Data.ScaleFactor;
            set
            {
                var d = Data;
                // Unity rejects non-positive factors.
                if (value <= 0f) return;
                d.ScaleFactor = value;
                CanvasSystem.UpdateRootTransform(d);
            }
        }

        public float referencePixelsPerUnit
        {
            get => Data.ReferencePixelsPerUnit;
            set => Data.ReferencePixelsPerUnit = value;
        }

        /// Nested canvases inherit pixel-perfect rendering unless they override it.
        public bool pixelPerfect
        {
            get
            {
                var d = Data;
                return d.OverridePixelPerfect || CanvasSystem.RootOf(d) == d ? d.PixelPerfect : CanvasSystem.RootOf(d).PixelPerfect;
            }
            set => Data.PixelPerfect = value;
        }

        public bool overrideSorting
        {
            get => Data.OverrideSorting;
            set
            {
                if (Data.OverrideSorting == value) return;
                Data.OverrideSorting = value;
                Hierarchy.Touch(Data.GameObject?.Transform);
            }
        }

        /// Nested canvases without overrideSorting sort with the nearest enclosing canvas that
        /// overrides sorting, or their root.
        public int sortingOrder
        {
            get => SortingSource.SortingOrder;
            set => Data.SortingOrder = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }

        public int sortingLayerID
        {
            get => SortingSource.SortingLayerId;
            set => Data.SortingLayerId = value;
        }

        public string sortingLayerName
        {
            get => SortingLayers.NameOf(SortingSource.SortingLayerId);
            set => Data.SortingLayerId = SortingLayers.IdOf(value);
        }

        /// The canvas whose sorting this one uses: itself when it overrides sorting, otherwise the
        /// nearest enclosing canvas that does, or the root (the unit it is drawn and raycast with).
        CanvasNative SortingSource => CanvasSystem.SortingCanvasOf(Data);

        /// Position of the canvas in the frame's canvas render order.
        public int renderOrder => Data.RenderOrder;

        public int targetDisplay => RootData.TargetDisplay;

        public AdditionalCanvasShaderChannels additionalShaderChannels
        {
            get => Data.AdditionalShaderChannels;
            set => Data.AdditionalShaderChannels = value;
        }

        /// The size the canvas renders at: the screen, or its camera's pixel size.
        public Vector2 renderingDisplaySize => CanvasSystem.DisplaySize(RootData);

        /// The rect the canvas renders into, in pixels: its root's camera view, or the screen (the
        /// original reports the screen for overlay canvases, nested ones too, and for camera
        /// canvases whose camera fills it; PerfProbe's measure mode).
        public Rect pixelRect => CanvasSystem.PixelRect(RootData);

        public static void ForceUpdateCanvases()
        {
            CanvasSystem.RunCanvasUpdate();
        }

        internal static void SendPreWillRenderCanvases() => preWillRenderCanvases?.Invoke();
        internal static void SendWillRenderCanvases() => willRenderCanvases?.Invoke();

        /// The shared default UI material ("UI/Default").
        public static Material GetDefaultCanvasMaterial() => CanvasSystem.DefaultMaterial;

        /// The default material for ETC1 textures with separate alpha ("UI/DefaultETC1").
        public static Material GetETC1SupportedCanvasMaterial() => CanvasSystem.Etc1Material;
    }

    public partial class CanvasRenderer
    {
        public CanvasRenderer() { }

        CanvasRendererNative Data => (CanvasRendererNative)NativeOrThrow;

        internal override void EngineOnDestroy()
        {
            if (Data.Baked is { } baked) baked.ReleaseResources();
        }

        /// Takes a copy of the mesh's geometry (Unity copies it into the canvas batch).
        public void SetMesh(Mesh mesh)
        {
            var d = Data;
            d.Geometry = mesh == null ? null : CanvasGeometry.From((MeshNative)mesh.NativeOrThrow);
            d.Changed();
        }

        /// Removes the geometry and every material, texture and clipping state.
        public void Clear()
        {
            var d = Data;
            d.Geometry = null;
            d.Materials = Array.Empty<Material>();
            d.PopMaterials = Array.Empty<Material>();
            d.Texture = null;
            d.AlphaTexture = null;
            d.SecondaryTextures = Array.Empty<(string, Texture2D)>();
            d.Changed();
        }

        public void SetColor(Color color) { var d = Data; d.Color = color; d.Changed(); }
        public Color GetColor() => Data.Color;

        public float GetAlpha() => Data.Color.a;

        /// The alpha of the CanvasGroups above the renderer (up to one that ignores its parents),
        /// as Unity documents it; the renderer's own alpha is not part of it.
        public float GetInheritedAlpha() => CanvasSystem.InheritedGroups(Data.GameObject).Alpha;
        public void SetAlpha(float alpha) { var d = Data; d.Color.a = alpha; d.Changed(); }

        public int materialCount
        {
            get => Data.Materials.Length;
            set
            {
                var d = Data;
                var m = d.Materials;
                Array.Resize(ref m, Math.Max(0, value));
                d.Materials = m;
                d.Changed();
            }
        }

        public void SetMaterial(Material material, int index)
        {
            var d = Data;
            if (index < 0 || index >= d.Materials.Length) return;
            d.Materials[index] = material;
            d.Changed();
        }

        /// One material, with the texture replacing its main texture.
        public void SetMaterial(Material material, Texture texture)
        {
            var d = Data;
            d.Materials = [material];
            d.Texture = texture;
            d.Changed();
        }

        public Material GetMaterial() => GetMaterial(0);

        public Material GetMaterial(int index)
        {
            var m = Data.Materials;
            return index >= 0 && index < m.Length ? m[index] : null;
        }

        public int popMaterialCount
        {
            get => Data.PopMaterials.Length;
            set
            {
                var d = Data;
                var m = d.PopMaterials;
                Array.Resize(ref m, Math.Max(0, value));
                d.PopMaterials = m;
                d.Changed();
            }
        }

        /// Drawn after the element's children (uGUI's stencil masks restore the stencil with them).
        public void SetPopMaterial(Material material, int index)
        {
            var d = Data;
            if (index < 0 || index >= d.PopMaterials.Length) return;
            d.PopMaterials[index] = material;
            d.Changed();
        }

        public Material GetPopMaterial(int index)
        {
            var m = Data.PopMaterials;
            return index >= 0 && index < m.Length ? m[index] : null;
        }

        public bool hasPopInstruction
        {
            get => Data.HasPopInstruction;
            set { var d = Data; d.HasPopInstruction = value; d.Changed(); }
        }

        public void SetTexture(Texture texture) { var d = Data; d.Texture = texture; d.Changed(); }

        public void SetAlphaTexture(Texture texture) { var d = Data; d.AlphaTexture = texture; d.Changed(); }

        public void SetSecondaryTextureCount(int count)
        {
            var d = Data;
            var s = d.SecondaryTextures;
            Array.Resize(ref s, Math.Max(0, count));
            d.SecondaryTextures = s;
            d.Changed();
        }

        public void SetSecondaryTexture(int index, string shaderPropertyName, Texture2D secondaryTexture)
        {
            var d = Data;
            if (index < 0 || index >= d.SecondaryTextures.Length) return;
            d.SecondaryTextures[index] = (shaderPropertyName, secondaryTexture);
            d.Changed();
        }

        /// Clip to a rectangle in the root canvas's space.
        public void EnableRectClipping(Rect rect)
        {
            var d = Data;
            d.ClipRect = rect;
            d.Changed();
        }

        public void DisableRectClipping()
        {
            var d = Data;
            d.ClipRect = null;
            d.Changed();
        }

        public Vector2 clippingSoftness
        {
            get => Data.ClippingSoftness;
            set { var d = Data; d.ClippingSoftness = value; d.Changed(); }
        }

        /// Excluded from the canvas (not drawn) while set.
        public bool cull
        {
            get => Data.Cull;
            set { var d = Data; d.Cull = value; d.Changed(); }
        }

        /// Not drawn while its effective alpha is zero.
        public bool cullTransparentMesh
        {
            get => Data.CullTransparentMesh;
            set { var d = Data; d.CullTransparentMesh = value; d.Changed(); }
        }

        /// Whether the element moved relative to its canvas since the canvas was last built.
        public bool hasMoved => Data.HasMoved;

        /// Draw order within the root canvas; -1 when the element was not in the last build.
        public int absoluteDepth => Data.AbsoluteDepth;

        public int relativeDepth => Data.RelativeDepth;

        public bool hasRectClipping => Data.ClipRect != null;

        // ----- UIVertex streams -----

        /// Replaces the lists with the components of a triangle stream; indices are 0..n-1.
        public static void SplitUIVertexStreams(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector3> normals, List<Vector4> tangents, List<int> indices)
            => SplitUIVertexStreams(verts, positions, colors, uv0S, uv1S, new List<Vector4>(), new List<Vector4>(), normals, tangents, indices);

        public static void SplitUIVertexStreams(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S, List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents, List<int> indices)
        {
            positions.Clear(); colors.Clear(); uv0S.Clear(); uv1S.Clear(); uv2S.Clear(); uv3S.Clear(); normals.Clear(); tangents.Clear();
            AddUIVertexStream(verts, positions, colors, uv0S, uv1S, uv2S, uv3S, normals, tangents);
            indices.Clear();
            for (int i = 0; i < verts.Count; i++) indices.Add(i);
        }

        /// Expands indexed component lists into a UIVertex triangle stream (replacing verts).
        public static void CreateUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector3> normals, List<Vector4> tangents, List<int> indices)
            => CreateUIVertexStream(verts, positions, colors, uv0S, uv1S, new List<Vector4>(), new List<Vector4>(), normals, tangents, indices);

        public static void CreateUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S, List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents, List<int> indices)
        {
            verts.Clear();
            foreach (int i in indices)
            {
                var v = UIVertex.simpleVert;
                if (i < positions.Count) v.position = positions[i];
                if (i < colors.Count) v.color = colors[i];
                if (i < uv0S.Count) v.uv0 = uv0S[i];
                if (i < uv1S.Count) v.uv1 = uv1S[i];
                if (i < uv2S.Count) v.uv2 = uv2S[i];
                if (i < uv3S.Count) v.uv3 = uv3S[i];
                if (i < normals.Count) v.normal = normals[i];
                if (i < tangents.Count) v.tangent = tangents[i];
                verts.Add(v);
            }
        }

        /// Appends the components of the vertices to the lists.
        public static void AddUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector3> normals, List<Vector4> tangents)
            => AddUIVertexStream(verts, positions, colors, uv0S, uv1S, new List<Vector4>(), new List<Vector4>(), normals, tangents);

        public static void AddUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S, List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents)
        {
            foreach (var v in verts)
            {
                positions.Add(v.position);
                colors.Add(v.color);
                uv0S.Add(v.uv0);
                uv1S.Add(v.uv1);
                uv2S.Add(v.uv2);
                uv3S.Add(v.uv3);
                normals.Add(v.normal);
                tangents.Add(v.tangent);
            }
        }
    }

    public partial class RectTransformUtility
    {
        static readonly Vector3[] s_Corners = new Vector3[4];

        RectTransformUtility() { }

        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint) => RectangleContainsScreenPoint(rect, screenPoint, null);

        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => RectangleContainsScreenPoint(rect, screenPoint, cam, Vector4.zero);

        /// The point projected onto the rect's plane lies inside the rect grown by offset
        /// (left, bottom, right, top).
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam, Vector4 offset)
        {
            if (rect == null) return false;
            if (!ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out var local)) return false;
            var r = rect.rect;
            return local.x >= r.xMin - offset.x && local.x <= r.xMax + offset.z && local.y >= r.yMin - offset.y && local.y <= r.yMax + offset.w;
        }

        public static bool ScreenPointToWorldPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector3 worldPoint)
        {
            worldPoint = Vector2.zero;
            var ray = ScreenPointToRay(cam, screenPoint);
            var plane = new Plane(rect.rotation * Vector3.back, rect.position);
            float enter = 0f;
            float num = Vector3.Dot(Vector3.Normalize(rect.position - ray.origin), plane.normal);
            if (num != 0f && !plane.Raycast(ray, out enter)) return false;
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (ScreenPointToWorldPointInRectangle(rect, screenPoint, cam, out var worldPoint))
            {
                localPoint = rect.InverseTransformPoint(worldPoint);
                return true;
            }
            return false;
        }

        public static Ray ScreenPointToRay(Camera cam, Vector2 screenPos)
        {
            if (cam != null) return cam.ScreenPointToRay(screenPos);
            Vector3 origin = screenPos;
            origin.z -= 100f;
            return new Ray(origin, Vector3.forward);
        }

        public static Vector2 WorldToScreenPoint(Camera cam, Vector3 worldPoint)
        {
            if (cam == null) return new Vector2(worldPoint.x, worldPoint.y);
            return cam.WorldToScreenPoint(worldPoint);
        }

        public static Bounds CalculateRelativeRectTransformBounds(Transform root, Transform child)
        {
            var rects = child.GetComponentsInChildren<RectTransform>(false);
            if (rects.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var toLocal = root.worldToLocalMatrix;
            foreach (var r in rects)
            {
                r.GetWorldCorners(s_Corners);
                for (int j = 0; j < 4; j++)
                {
                    var p = toLocal.MultiplyPoint3x4(s_Corners[j]);
                    min = Vector3.Min(p, min);
                    max = Vector3.Max(p, max);
                }
            }
            var result = new Bounds(min, Vector3.zero);
            result.Encapsulate(max);
            return result;
        }

        public static Bounds CalculateRelativeRectTransformBounds(Transform trans) => CalculateRelativeRectTransformBounds(trans, trans);

        public static void FlipLayoutOnAxis(RectTransform rect, int axis, bool keepPositioning, bool recursive)
        {
            if (rect == null) return;
            if (recursive)
                for (int i = 0; i < rect.childCount; i++)
                    if (rect.GetChild(i) is RectTransform child) FlipLayoutOnAxis(child, axis, false, true);
            var pivot = rect.pivot;
            pivot[axis] = 1f - pivot[axis];
            rect.pivot = pivot;
            if (keepPositioning) return;
            var anchoredPosition = rect.anchoredPosition;
            anchoredPosition[axis] = -anchoredPosition[axis];
            rect.anchoredPosition = anchoredPosition;
            var anchorMin = rect.anchorMin;
            var anchorMax = rect.anchorMax;
            float min = anchorMin[axis];
            anchorMin[axis] = 1f - anchorMax[axis];
            anchorMax[axis] = 1f - min;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
        }

        public static void FlipLayoutAxes(RectTransform rect, bool keepPositioning, bool recursive)
        {
            if (rect == null) return;
            if (recursive)
                for (int i = 0; i < rect.childCount; i++)
                    if (rect.GetChild(i) is RectTransform child) FlipLayoutAxes(child, false, true);
            rect.pivot = Transposed(rect.pivot);
            rect.sizeDelta = Transposed(rect.sizeDelta);
            if (keepPositioning) return;
            rect.anchoredPosition = Transposed(rect.anchoredPosition);
            rect.anchorMin = Transposed(rect.anchorMin);
            rect.anchorMax = Transposed(rect.anchorMax);
        }

        static Vector2 Transposed(Vector2 v) => new(v.y, v.x);

        /// The point rounded to whole screen pixels when the canvas renders pixel perfect in
        /// screen space; otherwise unchanged.
        public static Vector2 PixelAdjustPoint(Vector2 point, Transform elementTransform, Canvas canvas)
        {
            if (!CanvasSystem.SnapsToPixels(canvas, out var camera)) return point;
            var world = elementTransform.TransformPoint(point);
            var screen = camera != null ? camera.WorldToScreenPoint(world) : world;
            screen.x = Mathf.Round(screen.x);
            screen.y = Mathf.Round(screen.y);
            var snapped = camera != null ? camera.ScreenToWorldPoint(screen) : screen;
            return elementTransform.InverseTransformPoint(snapped);
        }

        /// The rect with its corners rounded to whole screen pixels for pixel-perfect canvases.
        public static Rect PixelAdjustRect(RectTransform rectTransform, Canvas canvas)
        {
            var r = rectTransform.rect;
            if (!CanvasSystem.SnapsToPixels(canvas, out _)) return r;
            var min = PixelAdjustPoint(r.min, rectTransform, canvas);
            var max = PixelAdjustPoint(r.max, rectTransform, canvas);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }

    public partial class UISystemProfilerApi
    {
        // Profiler markers for the editor's UI profiler; a player without the profiler records
        // nothing.
        public static void BeginSample(SampleType type) { }
        public static void EndSample(SampleType type) { }
        public static void AddMarker(string name, Object obj) { }
    }
}

namespace OniBrutal.Engine.UI
{
    using UnityEngine;

    internal sealed class CanvasNative : BehaviourNative
    {
        // AddComponent<Canvas> starts in world space (measured in the original player).
        public RenderMode RenderMode = RenderMode.WorldSpace;
        public Camera WorldCamera;
        public float PlaneDistance = 100f;
        public bool PixelPerfect;
        public bool OverridePixelPerfect;
        public bool OverrideSorting;
        public int SortingLayerId;
        public short SortingOrder;
        public int TargetDisplay;
        public float ScaleFactor = 1f;
        public float ReferencePixelsPerUnit = 100f;
        public AdditionalCanvasShaderChannels AdditionalShaderChannels;
        public int RenderOrder;
        public int EnableSequence;

        public override void RemapReferences(Func<Object, Object> map) => WorldCamera = (Camera)map(WorldCamera);

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            var c = (CanvasNative)source;
            RenderMode = c.RenderMode;
            WorldCamera = c.WorldCamera;
            PlaneDistance = c.PlaneDistance;
            PixelPerfect = c.PixelPerfect;
            OverridePixelPerfect = c.OverridePixelPerfect;
            OverrideSorting = c.OverrideSorting;
            SortingLayerId = c.SortingLayerId;
            SortingOrder = c.SortingOrder;
            TargetDisplay = c.TargetDisplay;
            ScaleFactor = c.ScaleFactor;
            ReferencePixelsPerUnit = c.ReferencePixelsPerUnit;
            AdditionalShaderChannels = c.AdditionalShaderChannels;
        }

        /// Canvas (class 223).
        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            RenderMode = (RenderMode)f.Int("m_RenderMode");
            WorldCamera = resolver.Resolve(f.PPtr("m_Camera")) as Camera;
            PlaneDistance = f.Float("m_PlaneDistance");
            PixelPerfect = f.Bool("m_PixelPerfect");
            OverrideSorting = f.Bool("m_OverrideSorting");
            OverridePixelPerfect = f.Bool("m_OverridePixelPerfect");
            AdditionalShaderChannels = (AdditionalCanvasShaderChannels)f.Int("m_AdditionalShaderChannelsFlag");
            SortingLayerId = f.Int("m_SortingLayerID");
            SortingOrder = f.Get<short>("m_SortingOrder");
            TargetDisplay = f.Get<sbyte>("m_TargetDisplay");
        }
    }

    /// A CanvasRenderer's geometry, copied by SetMesh (positions in the element's local space).
    internal sealed class CanvasGeometry
    {
        public Vector3[] Positions;
        public Color[] Colors;
        public Vector4[][] Uvs = new Vector4[4][];
        public Vector3[] Normals;
        public Vector4[] Tangents;
        public int[] Indices;

        public static CanvasGeometry From(MeshNative m)
        {
            var g = new CanvasGeometry
            {
                Positions = (Vector3[])m.Positions?.Clone() ?? Array.Empty<Vector3>(),
                Colors = (Color[])m.Colors?.Clone(),
                Normals = (Vector3[])m.Normals?.Clone(),
                Tangents = (Vector4[])m.Tangents?.Clone(),
            };
            for (int i = 0; i < 4; i++) g.Uvs[i] = (Vector4[])m.Uvs[i]?.Clone();
            // uGUI and TextMeshPro set their meshes on every rebuild: one array, filled in place.
            int count = 0;
            foreach (var s in m.SubMeshes)
                if (s.Topology == MeshTopology.Triangles) count += s.Indices.Length;
            var indices = count == 0 ? Array.Empty<int>() : new int[count];
            int at = 0;
            foreach (var s in m.SubMeshes)
            {
                if (s.Topology != MeshTopology.Triangles) continue;
                var source = s.Indices;
                int baseVertex = s.BaseVertex;
                if (baseVertex == 0) source.CopyTo(indices, at);
                else for (int i = 0; i < source.Length; i++) indices[at + i] = source[i] + baseVertex;
                at += source.Length;
            }
            g.Indices = indices;
            return g;
        }
    }

    internal sealed class CanvasRendererNative : ComponentNative
    {
        public CanvasGeometry Geometry;
        /// Property sheets of the element's draws by material and pop, with the state each was
        /// filled for (CanvasSystem.SheetFor).
        public List<(Material Material, bool Pop, PropertySheet Sheet, CanvasSystem.UiDrawState State)> DrawSheets;
        /// The geometry baked into its canvas's space with its colour (CanvasSystem), reused
        /// while geometry, placement and colour are unchanged.
        public MeshNative Baked;
        public CanvasGeometry BakedGeometry;
        public Matrix4x4 BakedToSpace;
        public Color BakedColor;
        public Material[] Materials = Array.Empty<Material>();
        public Material[] PopMaterials = Array.Empty<Material>();
        public bool HasPopInstruction;
        public Texture Texture;
        public Texture AlphaTexture;
        public (string Name, Texture2D Texture)[] SecondaryTextures = Array.Empty<(string, Texture2D)>();
        public Color Color = Color.white;
        public Rect? ClipRect;
        public Vector2 ClippingSoftness;
        public bool Cull;
        public bool CullTransparentMesh;
        public int AbsoluteDepth = -1;
        public int RelativeDepth = -1;
        public int Version;

        /// The element's transform relative to its root canvas at the last build.
        public Matrix4x4? BuiltToCanvas;
        public Matrix4x4 CurrentToCanvas;

        public bool HasMoved => BuiltToCanvas is not { } built || !built.Equals(CanvasSystem.ToCanvas(this));

        public void Changed() => Version++;

        public override void CopyStateFrom(NativeObject source)
            => CullTransparentMesh = ((CanvasRendererNative)source).CullTransparentMesh;

        /// CanvasRenderer (class 222).
        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            CullTransparentMesh = f.Bool("m_CullTransparentMesh");
        }
    }
}
