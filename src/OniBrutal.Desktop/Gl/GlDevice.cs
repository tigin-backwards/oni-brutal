using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.OpenGlApi;
using OniBrutal.Engine;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using N = Brutal.OpenGlApi.OpenGl.Native;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Desktop.Gl
{
    internal sealed class GlCaps : GraphicsCaps
    {
        public string Name = "", Vendor = "", VersionString = "";
        public int MaxTexture = 16384, MaxTargets = 8, MemoryMB;
        public bool S3tc, Bptc, Rgtc;

        public override GraphicsDeviceType DeviceType => GraphicsDeviceType.OpenGLCore;
        public override string DeviceName => Name;
        public override string DeviceVendor => Vendor;
        public override string DeviceVersion => "OpenGL " + VersionString;
        public override int DeviceId => 0;
        public override int VendorId => 0;
        public override int MemorySizeMB => MemoryMB;
        public override int ShaderLevel => 50;
        public override int MaxTextureSize => MaxTexture;
        public override int MaxRenderTargets => MaxTargets;
        public override bool Supports2DArrayTextures => true;
        public override bool Supports3DTextures => true;
        public override bool SupportsInstancing => true;
        public override bool SupportsComputeShaders => false;

        public override bool SupportsTexture(TextureFormat format) => format switch
        {
            TextureFormat.DXT1 or TextureFormat.DXT5 => S3tc,
            TextureFormat.BC7 or TextureFormat.BC6H => Bptc,
            TextureFormat.BC4 or TextureFormat.BC5 => Rgtc,
            _ => TextureFormats.CanEncode(format),
        };

        public override bool SupportsRenderTexture(RenderTextureFormat format)
        {
            try
            {
                var g = RenderTextureFormats.ToGraphicsFormat(format, false);
                if (g == UnityEngine.Experimental.Rendering.GraphicsFormat.None) return true;
                GlTextures.InternalOf(g);
                return true;
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }

    /// Unity's OpenGLCore device: executes the game's GLSL programs with Unity's conventions
    /// (clip space and texture origin as OpenGL, clockwise front faces, no projection flip).
    internal sealed unsafe class GlDevice : RenderDevice
    {
        public static float MaxAnisotropy;
        static bool framebufferValid;

        readonly GlfwPlatform platform;
        readonly GlCaps caps = new();
        readonly uint immediateBuffer, immediateIndices;
        /// The vertex array immediate geometry is drawn with (mesh draws use the mesh's own).
        readonly uint immediateVertexArray;
        /// Every draw's uniform blocks are written to a stream buffer and bound by range.
        const int UniformStreamSize = 16 << 20;
        GlStreamBuffer uniformStream;

        void BindUniformBlock(uint index, byte[] data)
        {
            uniformStream ??= new GlStreamBuffer(UniformBuffer, UniformStreamSize, GlApi.GetInteger(UniformBufferOffsetAlignment));
            int size = Math.Max(data.Length, 16);
            int offset;
            if (data.Length >= size) offset = uniformStream.Write(data);
            else
            {
                Span<byte> padded = stackalloc byte[size];
                data.CopyTo(padded);
                offset = uniformStream.Write(padded);
            }
            N.glBindBufferRange(UniformBuffer, index, new GlBuffer(uniformStream.Id), offset, size);
        }
        readonly Dictionary<string, DeviceTexture> defaults = new();
        readonly Dictionary<ShaderPass, PassState> passStates = new();
        readonly Dictionary<string, uint> mrtFramebuffers = new();
        readonly bool[] attributeEnabled = new bool[16];
        RenderTargetBinding target = RenderTargetBinding.BackBuffer;
        CameraConstants camera;
        Material passMaterial;
        int passIndex;
        static readonly GlGetProcAddressFunc loader = name => Glfw.GetProcAddress(new ReadOnlySpan<byte>(name, int.MaxValue)[..new ReadOnlySpan<byte>(name, int.MaxValue).IndexOf((byte)0)]);

        public GlDevice(GlfwPlatform platform)
        {
            this.platform = platform;
            platform.RenderWindow.MakeContextCurrent();
            if (!Glad.Initialize(loader)) throw new InvalidOperationException("OpenGL entry points could not be loaded.");
            caps.Name = GlApi.GetString(RendererName);
            caps.Vendor = GlApi.GetString(Vendor);
            caps.VersionString = GlApi.GetString(GlConst.Version);
            caps.MaxTexture = GlApi.GetInteger(MaxTextureSize);
            caps.MaxTargets = GlApi.GetInteger(MaxDrawBuffers);
            int extensions = GlApi.GetInteger(NumExtensions);
            for (uint i = 0; i < extensions; i++)
            {
                var e = GlApi.GetStringi(Extensions, i);
                GlPrograms.Extensions.Add(e);
                if (e == "GL_EXT_texture_compression_s3tc") caps.S3tc = true;
                if (e == "GL_ARB_texture_compression_bptc") caps.Bptc = true;
                if (e == "GL_ARB_texture_compression_rgtc") caps.Rgtc = true;
                if (e is "GL_EXT_texture_filter_anisotropic" or "GL_ARB_texture_filter_anisotropic")
                {
                    float max = 0f;
                    N.glGetFloatv(0x84FF, &max);
                    MaxAnisotropy = max;
                }
            }
            caps.Rgtc |= true; // core since OpenGL 3.0
            caps.MemoryMB = QueryVideoMemoryMB();
            EngineLog.Engine($"OpenGL: {caps.VersionString} / {caps.Name} / {caps.Vendor}");

            GlVertexArray vao = default;
            N.glGenVertexArrays(1, &vao);
            N.glBindVertexArray(vao);
            immediateVertexArray = vao.Id;
            N.glFrontFace((GlFrontFace)Cw);
            immediateBuffer = GlApi.GenBuffer();
            immediateIndices = GlApi.GenBuffer();
            GlBindings.Invalidate();
            CreateDefaults();
            GlStateCache.Invalidate();
            presenter = new GlPresenter(platform.Window);
            platform.Closing += presenter.Dispose;
        }

        readonly GlPresenter presenter;

        /// The framebuffer drawing to the screen goes to this frame.
        uint BackBuffer => presenter.BackBuffer(platform.FramebufferWidth, platform.FramebufferHeight);

        /// Video memory in MB (SystemInfo.graphicsMemorySize): the dedicated memory the NVIDIA or
        /// AMD memory-info extension reports, else Mesa's renderer query; 0 when the driver
        /// reports none. (The extensions come first: Mesa's figure can include shared memory.)
        static int QueryVideoMemoryMB()
        {
            if (GlPrograms.Extensions.Contains("GL_NVX_gpu_memory_info"))
            {
                int kb = 0;
                N.glGetIntegerv(0x9047, &kb); // GPU_MEMORY_INFO_DEDICATED_VIDMEM_NVX
                if (kb > 0) return kb / 1024;
            }
            if (GlPrograms.Extensions.Contains("GL_ATI_meminfo"))
            {
                int* info = stackalloc int[4];
                N.glGetIntegerv(0x87FC, info); // TEXTURE_FREE_MEMORY_ATI
                if (info[0] > 0) return info[0] / 1024;
            }
            const int GlxRendererVideoMemoryMesa = 0x8187;
            var query = (delegate* unmanaged[Cdecl]<int, uint*, int>)Glfw.GetProcAddress("glXQueryCurrentRendererIntegerMESA"u8);
            if (query != null)
            {
                uint mb = 0;
                if (query(GlxRendererVideoMemoryMesa, &mb) != 0 && mb > 0) return (int)mb;
            }
            return 0;
        }

        public override GraphicsCaps Caps => caps;
        public override bool RendersFrames => true;

        public static void InvalidateFramebufferBinding() => framebufferValid = false;

        // ----- Default textures (shader property defaults) -----

        void CreateDefaults()
        {
            Make("white", 255, 255, 255, 255);
            Make("black", 0, 0, 0, 0);
            Make("gray", 128, 128, 128, 128);
            Make("grey", 128, 128, 128, 128);
            Make("", 128, 128, 128, 128);
            Make("bump", 128, 128, 255, 128);
            Make("red", 255, 0, 0, 255);
            Make(Unbound, 0, 0, 0, 255);
        }

        void Make(string name, byte r, byte g, byte b, byte a)
        {
            var t = new DeviceTexture { Id = GlApi.GenTexture(), Target = TexTarget2D, Width = 1, Height = 1 };
            byte* px = stackalloc byte[4] { r, g, b, a };
            GlBindings.BindTexture(TexTarget2D, t.Id);
            N.glTexImage2D(TexTarget2D, 0, (int)Rgba8, 1, 1, 0, Rgba, UnsignedByte, px);
            N.glTexParameteri(TexTarget2D, TextureMinFilter, (int)Linear);
            N.glTexParameteri(TexTarget2D, TextureMaxLevel, 0);
            defaults[name] = t;
            // Array and 3D defaults with the same colour.
            var array = new DeviceTexture { Id = GlApi.GenTexture(), Target = TexTarget2DArray };
            GlBindings.BindTexture(TexTarget2DArray, array.Id);
            N.glTexImage3D(TexTarget2DArray, 0, (int)Rgba8, 1, 1, 1, 0, Rgba, UnsignedByte, px);
            N.glTexParameteri(TexTarget2DArray, TextureMaxLevel, 0);
            defaults["array:" + name] = array;
            var volume = new DeviceTexture { Id = GlApi.GenTexture(), Target = TexTarget3D };
            GlBindings.BindTexture(TexTarget3D, volume.Id);
            N.glTexImage3D(TexTarget3D, 0, (int)Rgba8, 1, 1, 1, 0, Rgba, UnsignedByte, px);
            N.glTexParameteri(TexTarget3D, TextureMaxLevel, 0);
            defaults["3d:" + name] = volume;
        }

        const string Unbound = "<unbound>";

        DeviceTexture DefaultTexture(string name, uint target)
        {
            name ??= "";
            string prefix = target == TexTarget2DArray ? "array:" : target == TexTarget3D ? "3d:" : "";
            return defaults.TryGetValue(prefix + name, out var t) ? t : defaults[prefix + "white"];
        }

        // ----- Targets -----

        public override void SetRenderTarget(in RenderTargetBinding binding)
        {
            target = binding;
            framebufferValid = false;
            BindTarget();
        }

        void BindTarget()
        {
            if (framebufferValid) return;
            framebufferValid = true;
            var t = target;
            if (t.Colors == null || t.Colors.Length == 0 || t.Colors[0] == null)
            {
                GlApi.BindFramebuffer(Framebuffer, BackBuffer);
                return;
            }
            if (t.Colors.Length == 1 && (t.Depth == null || t.Depth == t.Colors[0]))
            {
                var g = GlTextures.Ensure(t.Colors[0]);
                g.Dirty = true;
                GlApi.BindFramebuffer(Framebuffer, GlTextures.RenderFramebuffer(g));
                return;
            }
            // Multiple render targets: a framebuffer combining the colour textures with the depth
            // buffer of the depth target.
            var key = new System.Text.StringBuilder();
            var colors = new DeviceTexture[t.Colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = GlTextures.Ensure(t.Colors[i]);
                colors[i].Dirty = true;
                key.Append(colors[i].Id).Append(',');
            }
            var depth = t.Depth != null ? GlTextures.Ensure(t.Depth) : null;
            key.Append(depth?.DepthRenderbuffer ?? 0);
            if (!mrtFramebuffers.TryGetValue(key.ToString(), out var fb))
            {
                fb = GlApi.GenFramebuffer();
                GlApi.BindFramebuffer(Framebuffer, fb);
                var buffers = stackalloc GlEnum[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    N.glFramebufferTexture2D(Framebuffer, ColorAttachment0 + (uint)i, TexTarget2D, colors[i].Id, 0);
                    buffers[i] = ColorAttachment0 + (uint)i;
                }
                N.glDrawBuffers(colors.Length, buffers);
                if (depth is { DepthRenderbuffer: not 0 })
                    N.glFramebufferRenderbuffer(Framebuffer, DepthStencilAttachment, Renderbuffer, new GlRenderbuffer(depth.DepthRenderbuffer));
                mrtFramebuffers[key.ToString()] = fb;
            }
            GlApi.BindFramebuffer(Framebuffer, fb);
        }

        public override void SetViewport(UnityEngine.Rect pixels)
        {
            BindTarget();
            N.glViewport((int)pixels.x, (int)pixels.y, (int)pixels.width, (int)pixels.height);
        }

        /// The scissor rectangle in effect (CommandBuffer.EnableScissorRect), or null.
        static Rect? scissor;

        public static bool ScissorActive => scissor != null;

        public override void SetScissor(Rect? pixels)
        {
            scissor = pixels;
            if (pixels is { } r)
            {
                GlApi.Enable(ScissorTest, true);
                N.glScissor((int)r.x, (int)r.y, Math.Max(0, (int)r.width), Math.Max(0, (int)r.height));
            }
            else GlApi.Enable(ScissorTest, false);
        }

        public override void Clear(bool color, bool depth, Color backgroundColor, float depthValue)
        {
            BindTarget();
            // Clears fill the whole target.
            if (scissor != null) GlApi.Enable(ScissorTest, false);
            uint mask = 0;
            if (color)
            {
                N.glColorMask(true, true, true, true);
                N.glClearColor(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundColor.a);
                mask |= ColorBufferBit;
            }
            if (depth)
            {
                N.glDepthMask(true);
                N.glStencilMask(0xFF);
                N.glClearDepth(depthValue);
                N.glClearStencil(0);
                mask |= DepthBufferBit | StencilBufferBit;
            }
            if (mask != 0) N.glClear(mask);
            if (scissor != null) GlApi.Enable(ScissorTest, true);
            GlStateCache.Invalidate();
        }

        public override void SetCamera(in CameraConstants c) => camera = c;

        // ----- Drawing -----

        /// Forward rendering draws the passes for unlit and base lighting; others serve other
        /// rendering paths, shadows or lightmapping.
        static bool IsForwardPass(ShaderPass pass, SubShader sub)
        {
            if (!pass.Tags.TryGetValue("LightMode", out var mode) && !sub.Tags.TryGetValue("LightMode", out mode)) return true;
            return mode is "" or "Always" or "ForwardBase" or "Vertex" or "SRPDefaultUnlit";
        }

        IEnumerable<(ShaderNative Shader, ShaderPass Pass)> Passes(ShaderNative shader, MaterialNative material, int pass, bool allPasses)
        {
            var sub = shader.ActiveSubShader;
            if (sub == null) yield break;
            for (int i = 0; i < sub.Passes.Length; i++)
            {
                if (pass >= 0 && i != pass) continue;
                var p = sub.Passes[i];
                if (pass < 0 && !allPasses && !IsForwardPass(p, sub)) continue;
                if (p.Tags.TryGetValue("LightMode", out var lm) && material.DisabledPasses.Contains(lm)) continue;
                if (p.Type == 1)
                {
                    var used = ResolveUsePass(p.UseName);
                    if (used.Pass != null) yield return used;
                    continue;
                }
                if (p.Type != 0) continue; // GrabPass: no shader in this build uses one.
                yield return (shader, p);
            }
        }

        /// UsePass "Shader/PASSNAME": the named pass of another shader (names compare in upper case).
        static (ShaderNative Shader, ShaderPass Pass) ResolveUsePass(string useName)
        {
            int slash = useName.LastIndexOf('/');
            if (slash <= 0) return (null, null);
            var shader = ShaderRegistry.Find(useName[..slash]);
            if (shader == null) return (null, null);
            var native = (ShaderNative)shader.NativeOrThrow;
            var passName = useName[(slash + 1)..];
            foreach (var s in native.SubShaders)
                foreach (var p in s.Passes)
                    if (string.Equals(p.Name, passName, StringComparison.OrdinalIgnoreCase)) return (native, p);
            return (null, null);
        }

        PassState StateOf(ShaderPass pass)
        {
            if (!passStates.TryGetValue(pass, out var s))
            {
                s = new PassState(pass.Data.Struct("m_State"));
                passStates[pass] = s;
            }
            return s;
        }

        static bool KeywordEnabled(string keyword, MaterialNative material, string[] extra)
            => material.Keywords.Contains(keyword) || ShaderGlobals.Keywords.Contains(keyword) || (extra != null && Array.IndexOf(extra, keyword) >= 0);

        public override void Draw(in DrawRequest draw)
        {
            if (draw.Material == null || draw.Mesh == null) return;
            var material = (MaterialNative)draw.Material.NativeOrThrow;
            if (material.Shader == null) return;
            var shader = (ShaderNative)material.Shader.NativeOrThrow;
            if (!shader.HasOpenGLCorePrograms) return;
            if (draw.SubMesh < 0 || draw.SubMesh >= draw.Mesh.SubMeshes.Count) return;
            BindTarget();
            var mesh = GlMeshes.Ensure(draw.Mesh);
            var inputs = new DrawInputs { ObjectToWorld = draw.ObjectToWorld, Camera = camera, Block = draw.Properties, Material = material };
            var keywords = draw.Keywords;
            foreach (var (passShader, pass) in Passes(shader, material, draw.Pass, draw.AllPasses))
            {
                int blob = ShaderPrograms.SelectVariant(pass.Data, passShader.KeywordNames, k => KeywordEnabled(k, material, keywords));
                if (blob < 0) continue;
                var program = GlPrograms.Get(passShader, blob);
                if (program.Id == 0) continue;
                inputs.Shader = passShader;
                GlStateCache.Apply(StateOf(pass), inputs);
                Bind(program, inputs);
                var sm = mesh.SubMeshes[draw.SubMesh];
                if (sm.Count == 0) continue;
                GlBindings.BindVertexArray(GlMeshes.VertexArray(mesh, program));
                SetAbsentChannels(program, mesh);
                draws++;
                N.glDrawElementsBaseVertex(sm.Mode, sm.Count, UnsignedInt, (void*)(sm.Offset * 4L), sm.BaseVertex);
            }
        }

        void Bind(DeviceProgram program, DrawInputs inputs)
        {
            GlBindings.UseProgram(program.Id);
            var blocks = program.BlockScratch ??= program.BlockSizes.Select(n => new byte[n]).ToArray();
            foreach (var b in blocks) Array.Clear(b);
            GlUniforms.Apply(program, inputs, blocks);
            for (int i = 0; i < blocks.Length; i++) BindUniformBlock((uint)i, blocks[i]);
            // Render textures are made ready for sampling first (MSAA resolve, mip generation):
            // that work binds framebuffers and textures of its own.
            var devices = new DeviceTexture[program.Samplers.Count];
            for (int i = 0; i < devices.Length; i++)
            {
                var s = program.Samplers[i];
                DeviceTexture device = null;
                if (inputs.TryTexture(s.PropertyId, out var binding) && binding.Texture != null && binding.Texture.Native is TextureNative tn)
                {
                    device = GlTextures.Ensure(tn);
                    if (tn is RenderTextureNative rt) GlTextures.PrepareForSampling(rt, device);
                    if (device.Target != s.Target) device = null;
                }
                if (device == null)
                {
                    // A declared property falls back to its default texture. A sampler the shader
                    // does not declare, with no global set, has no texture bound in Unity's GLCore
                    // player and reads (0, 0, 0, 1) as GL defines for a missing texture (the
                    // overlay compositor relies on an unset _MRT2 reading black).
                    string name = inputs.Shader.PropertiesById.TryGetValue(s.PropertyId, out var prop) ? prop.DefaultTexture : Unbound;
                    device = DefaultTexture(name, s.Target);
                }
                devices[i] = device;
            }
            for (int i = 0; i < devices.Length; i++)
            {
                GlBindings.BindTexture(program.Samplers[i].Unit, devices[i].Target, devices[i].Id);
            }
            // Drawing goes to the current target even if preparing a texture rebound it.
            BindTarget();
        }

        void SetAttribute(int location, bool enabled)
        {
            if (location < 0 || location >= attributeEnabled.Length || attributeEnabled[location] == enabled) return;
            attributeEnabled[location] = enabled;
            if (enabled) N.glEnableVertexAttribArray((uint)location);
            else N.glDisableVertexAttribArray((uint)location);
        }

        /// Channels a mesh lacks read Unity's defaults (white colour, position (0,0,0,1), zero
        /// normal/tangent/uv) from the context's constant attribute values, which the vertex
        /// array leaves disabled.
        void SetAbsentChannels(DeviceProgram program, DeviceMesh mesh)
        {
            for (int c = 0; c < (int)Channel.Count; c++)
            {
                int location = program.Attributes[c];
                if (location < 0 || mesh.Components[c] != 0) continue;
                SetConstant(location, c == (int)Channel.Color ? ConstantWhite : c == (int)Channel.Position ? ConstantPoint : ConstantZero);
            }
        }

        const byte ConstantZero = 1, ConstantWhite = 2, ConstantPoint = 3;
        /// The constant value each attribute location holds (0: unknown).
        readonly byte[] constants = new byte[16];

        void SetConstant(int location, byte value)
        {
            if (location < constants.Length && constants[location] == value) return;
            if (location < constants.Length) constants[location] = value;
            switch (value)
            {
                case ConstantWhite: N.glVertexAttrib4f((uint)location, 1f, 1f, 1f, 1f); break;
                case ConstantPoint: N.glVertexAttrib4f((uint)location, 0f, 0f, 0f, 1f); break;
                default: N.glVertexAttrib4f((uint)location, 0f, 0f, 0f, 0f); break;
            }
        }

        public override bool SetPass(Material material, int pass)
        {
            passMaterial = material;
            passIndex = pass;
            return true;
        }

        /// GL immediate geometry with the pass set by SetPass: the model-view matrix acts as
        /// the object transform under an identity view.
        public override void DrawImmediate(ImmediatePrimitive primitive, ReadOnlySpan<ImmediateVertex> vertices, Matrix4x4 modelView, Matrix4x4 projection)
        {
            if (passMaterial == null || vertices.Length == 0) return;
            var material = (MaterialNative)passMaterial.NativeOrThrow;
            if (material.Shader == null) return;
            var shader = (ShaderNative)material.Shader.NativeOrThrow;
            var sub = shader.ActiveSubShader;
            if (sub == null || passIndex >= sub.Passes.Length) return;
            var pass = sub.Passes[passIndex];
            int blob = ShaderPrograms.SelectVariant(pass.Data, shader.KeywordNames, k => KeywordEnabled(k, material, null));
            if (blob < 0) return;
            var program = GlPrograms.Get(shader, blob);
            if (program.Id == 0) return;
            BindTarget();
            var inputs = new DrawInputs
            {
                ObjectToWorld = modelView,
                Camera = camera with { View = Matrix4x4.identity, Projection = projection },
                Material = material,
                Shader = shader,
            };
            GlStateCache.Apply(StateOf(pass), inputs);
            Bind(program, inputs);
            GlBindings.BindVertexArray(immediateVertexArray);

            // Interleaved: position, colour, eight texture coordinates.
            const int stride = sizeof(float) * (3 + 4 + 32);
            var data = new float[vertices.Length * (stride / 4)];
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                int o = i * (stride / 4);
                data[o] = v.Position.x; data[o + 1] = v.Position.y; data[o + 2] = v.Position.z;
                data[o + 3] = v.Color.r; data[o + 4] = v.Color.g; data[o + 5] = v.Color.b; data[o + 6] = v.Color.a;
                Vector4[] uvs = [v.Uv0, v.Uv1, v.Uv2, v.Uv3, v.Uv4, v.Uv5, v.Uv6, v.Uv7];
                for (int u = 0; u < 8; u++)
                {
                    data[o + 7 + u * 4] = uvs[u].x;
                    data[o + 8 + u * 4] = uvs[u].y;
                    data[o + 9 + u * 4] = uvs[u].z;
                    data[o + 10 + u * 4] = uvs[u].w;
                }
            }
            GlBindings.BindBuffer(ArrayBuffer, immediateBuffer);
            GlApi.BufferData<float>(ArrayBuffer, data, StreamDraw);
            for (int c = 0; c < (int)Channel.Count; c++)
            {
                int location = program.Attributes[c];
                if (location < 0) continue;
                if (c == (int)Channel.Normal || c == (int)Channel.Tangent)
                {
                    SetAttribute(location, false);
                    SetConstant(location, ConstantZero);
                    continue;
                }
                SetAttribute(location, true);
                int offset = c == (int)Channel.Position ? 0 : c == (int)Channel.Color ? 12 : 28 + (c - (int)Channel.Uv0) * 16;
                int size = c == (int)Channel.Position ? 3 : 4;
                N.glVertexAttribPointer((uint)location, size, Float, false, stride, (void*)offset);
            }
            if (primitive == ImmediatePrimitive.Quads)
            {
                int quads = vertices.Length / 4;
                var indices = new int[quads * 6];
                for (int q = 0; q < quads; q++)
                {
                    int b = q * 4;
                    indices.AsSpan(q * 6, 6).SetValues(b, b + 1, b + 2, b, b + 2, b + 3);
                }
                GlBindings.BindBuffer(ElementArrayBuffer, immediateIndices);
                GlApi.BufferData<int>(ElementArrayBuffer, indices, StreamDraw);
                draws++;
                N.glDrawElements(Triangles, indices.Length, UnsignedInt, null);
            }
            else
            {
                uint mode = primitive switch
                {
                    ImmediatePrimitive.Lines => Lines,
                    ImmediatePrimitive.LineStrip => LineStrip,
                    ImmediatePrimitive.TriangleStrip => TriangleStrip,
                    _ => Triangles,
                };
                draws++;
                N.glDrawArrays(mode, 0, vertices.Length);
            }
        }

        // ----- Read-back, resources, presentation -----

        public override void ReadPixels(RenderTextureNative source, RectInt rect, Texture2DNative destination, int destX, int destY)
        {
            uint framebuffer = BackBuffer;
            if (source != null)
            {
                var g = GlTextures.Ensure(source);
                GlTextures.PrepareForSampling(source, g);
                framebuffer = g.Framebuffer;
            }
            GlApi.BindFramebuffer(ReadFramebuffer, framebuffer);
            int w = Math.Min(rect.width, destination.Width - destX), h = Math.Min(rect.height, destination.Height - destY);
            if (w <= 0 || h <= 0 || !TextureFormats.CanEncode(destination.Format) || destination.Memory == null) return;
            var pixels = new float[w * h * 4];
            N.glPixelStorei(PackAlignment, 4);
            fixed (float* p = pixels) N.glReadPixels(rect.x, rect.y, w, h, Rgba, Float, p);
            var mip = destination.Memory.Slice(destination.MipOffset(0), destination.MipSize(0));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4;
                    TextureFormats.Encode(destination.Format, mip, destination.Width, destX + x, destY + y,
                        new Color(pixels[o], pixels[o + 1], pixels[o + 2], pixels[o + 3]));
                }
            framebufferValid = false;
        }

        public override void Upload(TextureNative texture) => GlTextures.Ensure(texture);

        public override void ReleaseResource(object gpu)
        {
            switch (gpu)
            {
                case DeviceTexture t: t.Delete(); break;
                case DeviceMesh m: m.Delete(); break;
                case DeviceProgramSet p: p.Delete(); break;
            }
        }

        /// Compiles every variant of every loaded shader.
        public override void WarmupShaders()
        {
            foreach (var native in ObjectTable.Snapshot())
            {
                if (native is not ShaderNative shader || !shader.HasOpenGLCorePrograms) continue;
                foreach (var sub in shader.SubShaders)
                    foreach (var pass in sub.Passes)
                    {
                        if (pass.Type != 0) continue;
                        foreach (var list in pass.Data.Struct("progVertex").Array("m_PlayerSubPrograms"))
                            foreach (OniBrutal.Assets.TypeTrees.Fields sub2 in (object[])list)
                                if (ShaderPrograms.IsGlCore(sub2.Get<sbyte>("m_GpuProgramType")))
                                    GlPrograms.Get(shader, (int)sub2.Get<uint>("m_BlobIndex"));
                    }
            }
        }

        /// Hands the frame to the presenter, which swaps with QualitySettings.vSyncCount as the
        /// swap interval.
        public override void Present()
        {
            if (gpuTiming) EndGpuTiming();
            uniformStream?.FenceFrame();
            GlTextures.FenceFrame();
            presenter.Present(QualitySettings.vSyncCount);
            framebufferValid = false;
            if (gpuTiming) BeginGpuTiming();
        }

        // ----- GPU frame timing (diagnostics) -----

        const uint TimeElapsed = 0x88BF, QueryResult = 0x8866, QueryResultAvailable = 0x8867;
        const int TimingSlots = 4;
        readonly uint[] timingQueries = new uint[TimingSlots];
        readonly bool[] timingPending = new bool[TimingSlots];
        int timingSlot = -1;
        int draws;

        public override string TakeFrameCounters(int frames)
        {
            string text = $"draws {draws / frames}, program binds {GlBindings.ProgramBinds / frames}, texture binds {GlBindings.TextureBinds / frames}, buffer binds {GlBindings.BufferBinds / frames}, vertex array binds {GlBindings.VertexArrayBinds / frames}, skipped binds {GlBindings.Skipped / frames}, uniforms sent {GlUniforms.UniformsSent / frames}, unchanged uniforms {GlUniforms.UniformsSkipped / frames}, texture uploads {GlTextures.Uploads / (double)frames:F1} ({GlTextures.UploadBytes / frames / 1024} KiB)";
            draws = 0;
            GlUniforms.UniformsSent = GlUniforms.UniformsSkipped = 0;
            GlTextures.Uploads = GlTextures.UploadBytes = 0;
            GlBindings.ProgramBinds = GlBindings.TextureBinds = GlBindings.BufferBinds = GlBindings.VertexArrayBinds = GlBindings.Skipped = 0;
            return text;
        }

        bool gpuTiming;
        double gpuTotal;
        int gpuFrames;

        /// GPU time of the commands between consecutive presents, averaged over the frames
        /// whose results arrived since the last call; -1 before any.
        public override double TakeGpuFrameMilliseconds()
        {
            if (!gpuTiming)
            {
                gpuTiming = true;
                fixed (uint* q = timingQueries) N.glGenQueries(TimingSlots, (GlQuery*)q);
                BeginGpuTiming();
                return -1;
            }
            double ms = gpuFrames > 0 ? gpuTotal / gpuFrames : -1;
            gpuTotal = 0;
            gpuFrames = 0;
            return ms;
        }

        void BeginGpuTiming()
        {
            timingSlot = (timingSlot + 1) % TimingSlots;
            if (timingPending[timingSlot]) CollectGpuTiming(timingSlot);
            N.glBeginQuery(TimeElapsed, new GlQuery(timingQueries[timingSlot]));
        }

        void EndGpuTiming()
        {
            N.glEndQuery(TimeElapsed);
            timingPending[timingSlot] = true;
            for (int i = 0; i < TimingSlots; i++)
                if (i != timingSlot && timingPending[i])
                {
                    uint available = 0;
                    N.glGetQueryObjectuiv(timingQueries[i], QueryResultAvailable, &available);
                    if (available != 0) CollectGpuTiming(i);
                }
        }

        void CollectGpuTiming(int slot)
        {
            ulong ns = 0;
            N.glGetQueryObjectui64v(timingQueries[slot], QueryResult, &ns);
            timingPending[slot] = false;
            gpuTotal += ns / 1e6;
            gpuFrames++;
        }
    }

    internal static class SpanExtensions
    {
        public static void SetValues(this Span<int> span, params ReadOnlySpan<int> values) => values.CopyTo(span);
    }
}
