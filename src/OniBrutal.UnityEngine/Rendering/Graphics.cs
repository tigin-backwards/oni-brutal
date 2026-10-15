using System;
using System.Collections.Generic;
using OniBrutal.Engine;
using OniBrutal.Engine.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine
{
    public partial class Graphics
    {
        // ----- Blit -----

        /// Copies source into dest (null: the screen) with Unity's blit-copy material.
        public static void Blit(Texture source, RenderTexture dest) => Blits.Blit(source, dest, null, -1, Vector2.one, Vector2.zero);

        public static void Blit(Texture source, RenderTexture dest, Vector2 scale, Vector2 offset) => Blits.Blit(source, dest, null, -1, scale, offset);

        public static void Blit(Texture source, RenderTexture dest, Material mat) => Blit(source, dest, mat, -1);

        /// Draws a full-target quad with mat (_MainTex = source); pass -1 draws every pass.
        /// dest becomes RenderTexture.active.
        public static void Blit(Texture source, RenderTexture dest, Material mat, int pass)
        {
            if (mat == null) throw new ArgumentNullException(nameof(mat));
            Blits.Blit(source, dest, mat, pass, Vector2.one, Vector2.zero);
        }

        public static void Blit(Texture source, Material mat) => Blit(source, mat, -1);

        public static void Blit(Texture source, Material mat, int pass) => Blit(source, null, mat, pass);

        /// A blit whose quad carries one texture coordinate set per offset (in source pixels).
        public static void BlitMultiTap(Texture source, RenderTexture dest, Material mat, params Vector2[] offsets)
        {
            if (offsets.Length == 0) throw new ArgumentException("empty offsets list passed.", "offsets");
            if (mat == null) throw new ArgumentNullException(nameof(mat));
            Blits.MultiTap(source, dest, mat, offsets);
        }

        // ----- DrawMesh (drawn by this frame's cameras) -----

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer)
            => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, null, 0, null);

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera)
            => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, 0, null);

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera, int submeshIndex)
            => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, submeshIndex, null);

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties)
            => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, submeshIndex, properties);

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, bool castShadows, bool receiveShadows)
            => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, submeshIndex, properties);

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, ShadowCastingMode castShadows, bool receiveShadows, Transform probeAnchor, bool useLightProbes)
            => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, submeshIndex, properties);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer)
            => DrawMesh(mesh, matrix, material, layer, null, 0, null);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera)
            => DrawMesh(mesh, matrix, material, layer, camera, 0, null);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex)
            => DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, null);

        /// Queues the mesh for every camera of this frame (or only `camera`) whose culling mask
        /// includes `layer`; the property block is copied now. Lighting and shadow arguments
        /// have no effect in this project (no lights or shadows are rendered).
        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            PropertySheet copy = null;
            if (properties != null && !properties.isEmpty)
            {
                copy = new PropertySheet();
                copy.CopyFrom(properties.Sheet);
            }
            FrameRenderer.Queue(new QueuedMesh { Mesh = mesh, Matrix = matrix, Material = material, Layer = layer, Camera = camera, SubMesh = submeshIndex, Properties = copy });
        }

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, bool castShadows, bool receiveShadows)
            => DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, properties);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, ShadowCastingMode castShadows, bool receiveShadows, Transform probeAnchor, bool useLightProbes)
            => DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, properties);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, ShadowCastingMode castShadows, bool receiveShadows, Transform probeAnchor, LightProbeUsage lightProbeUsage)
            => DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, properties);

        // ----- DrawTexture (immediate, in the current GL matrices) -----

        public static void DrawTexture(Rect screenRect, Texture texture) => DrawTexture(screenRect, texture, null);

        /// Draws the texture over screenRect with GUI orientation (y down); a null material uses
        /// Unity's GUI texture shader.
        public static void DrawTexture(Rect screenRect, Texture texture, Material mat)
        {
            Threads.AssertMainThread("DrawTexture");
            Blits.DrawTexture(screenRect, texture, mat);
        }
    }

    public partial class GL
    {
        public const int TRIANGLES = 4;
        public const int TRIANGLE_STRIP = 5;
        public const int QUADS = 7;
        public const int LINES = 1;
        public const int LINE_STRIP = 2;

        public static void PushMatrix() => GLState.Push();
        public static void PopMatrix() => GLState.Pop();

        /// Orthographic projection mapping (0,0)-(1,1) to the viewport; identity model-view.
        public static void LoadOrtho()
        {
            GLState.Projection = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 100f);
            GLState.ModelView = Matrix4x4.identity;
        }

        /// Pixel coordinates of the current target, origin at the bottom left.
        public static void LoadPixelMatrix()
        {
            var size = GLState.TargetSize;
            LoadPixelMatrix(0f, size.x, 0f, size.y);
        }

        public static void LoadPixelMatrix(float left, float right, float bottom, float top)
        {
            GLState.Projection = Matrix4x4.Ortho(left, right, bottom, top, -1f, 100f);
            GLState.ModelView = Matrix4x4.identity;
        }

        public static void LoadIdentity() => GLState.ModelView = Matrix4x4.identity;

        public static void LoadProjectionMatrix(Matrix4x4 mat) => GLState.Projection = mat;

        public static void MultMatrix(Matrix4x4 m) => GLState.ModelView *= m;

        public static Matrix4x4 modelview
        {
            get => GLState.ModelView;
            set => GLState.ModelView = value;
        }

        public static void Begin(int mode) => GLState.Begin(mode);
        public static void End() => GLState.End();

        public static void Vertex3(float x, float y, float z) => GLState.Vertex(new Vector3(x, y, z));
        public static void Vertex(Vector3 v) => GLState.Vertex(v);
        public static void Color(Color c) => GLState.Current.Color = c;
        public static void TexCoord(Vector3 v) => GLState.Current.Uv0 = new Vector4(v.x, v.y, v.z, 0f);
        public static void TexCoord2(float x, float y) => GLState.Current.Uv0 = new Vector4(x, y, 0f, 0f);
        public static void TexCoord3(float x, float y, float z) => GLState.Current.Uv0 = new Vector4(x, y, z, 0f);
        public static void MultiTexCoord2(int unit, float x, float y) => GLState.SetUv(unit, new Vector4(x, y, 0f, 0f));
        public static void MultiTexCoord3(int unit, float x, float y, float z) => GLState.SetUv(unit, new Vector4(x, y, z, 0f));
        public static void MultiTexCoord(int unit, Vector3 v) => GLState.SetUv(unit, new Vector4(v.x, v.y, v.z, 0f));

        public static void Clear(bool clearDepth, bool clearColor, Color backgroundColor) => Clear(clearDepth, clearColor, backgroundColor, 1f);

        public static void Clear(bool clearDepth, bool clearColor, Color backgroundColor, float depth)
            => GraphicsDevice.Current.Clear(clearColor, clearDepth, backgroundColor, depth);

        public static void Viewport(Rect pixelRect) => GraphicsDevice.Current.SetViewport(pixelRect);
    }

    /// Gizmos are drawn only in the editor's scene and game views; in a player every drawing
    /// call is a no-op. The colour and matrix are kept, as Unity does.
    public partial class Gizmos
    {
        static Color s_Color = UnityEngine.Color.white;
        static Matrix4x4 s_Matrix = Matrix4x4.identity;

        public static Color color { get => s_Color; set => s_Color = value; }
        public static Matrix4x4 matrix { get => s_Matrix; set => s_Matrix = value; }

        public static void DrawLine(Vector3 from, Vector3 to) { }
        public static void DrawRay(Vector3 from, Vector3 direction) { }
        public static void DrawRay(Ray r) { }
        public static void DrawCube(Vector3 center, Vector3 size) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawSphere(Vector3 center, float radius) { }
        public static void DrawWireSphere(Vector3 center, float radius) { }
    }
}

namespace OniBrutal.Engine.Rendering
{
    using UnityEngine;

    /// GL immediate-mode state: matrices and the primitive being built.
    internal static class GLState
    {
        public static Matrix4x4 ModelView = Matrix4x4.identity;
        public static Matrix4x4 Projection = Matrix4x4.identity;
        static readonly Stack<(Matrix4x4, Matrix4x4)> stack = new();
        public static ImmediateVertex Current = new() { Color = Color.white };
        static readonly List<ImmediateVertex> vertices = new();
        static ImmediatePrimitive? mode;

        /// Size of what drawing currently goes to (the active render texture or the screen).
        public static Vector2 TargetSize
        {
            get
            {
                var rt = RenderTexture.active;
                if (rt != null) return new Vector2(rt.width, rt.height);
                var p = OniBrutal.Engine.Platform.PlatformServices.Current;
                return new Vector2(p.FramebufferWidth, p.FramebufferHeight);
            }
        }

        /// While a camera renders, GL starts from its view and projection.
        public static void BeginCamera(CameraNative cam)
        {
            ModelView = cam.WorldToCamera;
            Projection = cam.Projection;
        }

        public static void Push() => stack.Push((ModelView, Projection));

        public static void Pop()
        {
            if (stack.Count == 0)
            {
                Debug.LogError("Matrix stack empty");
                return;
            }
            (ModelView, Projection) = stack.Pop();
        }

        public static void SetUv(int unit, Vector4 uv)
        {
            switch (unit)
            {
                case 0: Current.Uv0 = uv; break;
                case 1: Current.Uv1 = uv; break;
                case 2: Current.Uv2 = uv; break;
                case 3: Current.Uv3 = uv; break;
                case 4: Current.Uv4 = uv; break;
                case 5: Current.Uv5 = uv; break;
                case 6: Current.Uv6 = uv; break;
                case 7: Current.Uv7 = uv; break;
            }
        }

        public static void Begin(int glMode)
        {
            Threads.AssertMainThread("GL.Begin");
            if (glMode is not (1 or 2 or 4 or 5 or 7))
            {
                Debug.LogError("Unsupported GL.Begin mode " + glMode);
                return;
            }
            mode = (ImmediatePrimitive)glMode;
            vertices.Clear();
        }

        public static void Vertex(Vector3 position)
        {
            if (mode == null) return;
            var v = Current;
            v.Position = position;
            vertices.Add(v);
        }

        public static void End()
        {
            if (mode is not { } m) return;
            mode = null;
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices);
            GraphicsDevice.Current.DrawImmediate(m, span, ModelView, Projection);
            vertices.Clear();
        }
    }

    /// Full-target quads (Blit family) and textured screen rects.
    internal static class Blits
    {
        static readonly int MainTex = ShaderPropertyIds.Get("_MainTex");
        static Material blitCopy, guiTexture;
        static MeshNative quad;

        static Material BlitCopy => blitCopy != null ? blitCopy : blitCopy = new Material(Shader.Find("Hidden/BlitCopy"));
        static Material GuiTexture => guiTexture != null ? guiTexture : guiTexture = new Material(Shader.Find("Hidden/Internal-GUITexture"));

        /// The unit quad (0,0)-(1,1) with matching UVs.
        static MeshNative Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new MeshNative
                {
                    VertexCount = 4,
                    Positions = [new(0f, 0f, 0f), new(0f, 1f, 0f), new(1f, 1f, 0f), new(1f, 0f, 0f)],
                    SubMeshes = new List<SubMeshData> { new SubMeshData { Indices = [0, 1, 2, 0, 2, 3] } },
                };
                quad.Uvs[0] = [new(0f, 0f, 0f, 0f), new(0f, 1f, 0f, 0f), new(1f, 1f, 0f, 0f), new(1f, 0f, 0f, 0f)];
                quad.UvDimensions[0] = 2;
                quad.RecalculateBounds();
                return quad;
            }
        }

        static void BeginFullTarget(RenderTexture dest)
        {
            RenderTexture.active = dest;
            var device = GraphicsDevice.Current;
            FrameRenderer.Bind(device, dest == null ? FrameRenderer.ScreenBinding : RenderTargetBinding.Of((RenderTextureNative)dest.NativeOrThrow));
            var size = dest == null ? GLState.TargetSize * FrameRenderer.ScreenScale : GLState.TargetSize;
            device.SetViewport(new Rect(0f, 0f, size.x, size.y));
            device.SetCamera(new CameraConstants(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f), Vector3.zero, -1f, 1f, true, size));
        }

        public static void Blit(Texture source, RenderTexture dest, Material mat, int pass, Vector2 scale, Vector2 offset)
        {
            Threads.AssertMainThread("Blit");
            mat ??= BlitCopy;
            BeginFullTarget(dest);
            var props = new PropertySheet();
            if (source != null)
            {
                props.SetTexture(MainTex, source);
                props.SetTextureScaleOffset(MainTex, new Vector4(scale.x, scale.y, offset.x, offset.y));
            }
            GraphicsDevice.Current.Draw(new DrawRequest(Quad, 0, Matrix4x4.identity, mat, pass, props, AllPasses: true));
        }

        public static void MultiTap(Texture source, RenderTexture dest, Material mat, Vector2[] offsets)
        {
            Threads.AssertMainThread("BlitMultiTap");
            BeginFullTarget(dest);
            mat.SetTexture(MainTex, source);
            if (!mat.SetPass(0)) return;
            var texel = source != null ? new Vector2(1f / source.width, 1f / source.height) : Vector2.zero;
            Span<ImmediateVertex> v = stackalloc ImmediateVertex[4];
            Vector2[] corners = [new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f)];
            for (int i = 0; i < 4; i++)
            {
                v[i].Position = corners[i];
                v[i].Color = Color.white;
                for (int t = 0; t < offsets.Length && t < 8; t++)
                {
                    var uv = new Vector4(corners[i].x + offsets[t].x * texel.x, corners[i].y + offsets[t].y * texel.y, 0f, 0f);
                    switch (t)
                    {
                        case 0: v[i].Uv0 = uv; break;
                        case 1: v[i].Uv1 = uv; break;
                        case 2: v[i].Uv2 = uv; break;
                        case 3: v[i].Uv3 = uv; break;
                        case 4: v[i].Uv4 = uv; break;
                        case 5: v[i].Uv5 = uv; break;
                        case 6: v[i].Uv6 = uv; break;
                        case 7: v[i].Uv7 = uv; break;
                    }
                }
            }
            GraphicsDevice.Current.DrawImmediate(ImmediatePrimitive.Quads, v, Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
        }

        public static void DrawTexture(Rect r, Texture texture, Material mat)
        {
            mat ??= GuiTexture;
            mat.SetTexture(MainTex, texture);
            if (!mat.SetPass(0)) return;
            Span<ImmediateVertex> v = stackalloc ImmediateVertex[4];
            v[0] = new ImmediateVertex { Position = new Vector3(r.xMin, r.yMin, 0f), Uv0 = new Vector4(0f, 1f, 0f, 0f), Color = Color.white };
            v[1] = new ImmediateVertex { Position = new Vector3(r.xMax, r.yMin, 0f), Uv0 = new Vector4(1f, 1f, 0f, 0f), Color = Color.white };
            v[2] = new ImmediateVertex { Position = new Vector3(r.xMax, r.yMax, 0f), Uv0 = new Vector4(1f, 0f, 0f, 0f), Color = Color.white };
            v[3] = new ImmediateVertex { Position = new Vector3(r.xMin, r.yMax, 0f), Uv0 = new Vector4(0f, 0f, 0f, 0f), Color = Color.white };
            GraphicsDevice.Current.DrawImmediate(ImmediatePrimitive.Quads, v, GLState.ModelView, GLState.Projection);
        }
    }
}
