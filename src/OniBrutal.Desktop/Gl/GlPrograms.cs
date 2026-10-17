using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Brutal.OpenGlApi;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using N = Brutal.OpenGlApi.OpenGl.Native;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Desktop.Gl
{
    /// Where a uniform's value comes from.
    internal enum UniformSource
    {
        Property,
        TextureScaleOffset,
        TextureTexelSize,
        TextureHdr,
        ObjectToWorld, WorldToObject, MatrixV, MatrixInvV, MatrixP, MatrixVP, MatrixInvVP, MatrixMVP, CameraProjection, CameraInvProjection,
        CameraToWorld, WorldToCamera,
        Time, SinTime, CosTime, DeltaTime, ScreenParams, ProjectionParams, ZBufferParams, OrthoParams, WorldSpaceCameraPos, WorldTransformParams,
    }

    internal sealed class UniformBinding
    {
        public string Name;
        public int Location = -1;
        public uint Type;
        /// Array length in GLSL elements (vec4 elements for HLSLcc matrices).
        public int Size;
        public bool IsMatrix;
        public UniformSource Source;
        public int PropertyId;
        // Members of uniform blocks.
        public int Block = -1;
        public int Offset;
        public int ArrayStride;
    }

    internal sealed class SamplerBinding
    {
        public int Location;
        public int Unit;
        public uint Target;
        public int PropertyId;
    }

    internal sealed class DeviceProgram
    {
        public uint Id;
        public int[] Attributes = new int[(int)Channel.Count];
        public readonly List<UniformBinding> Uniforms = new();
        public readonly List<SamplerBinding> Samplers = new();
        public int[] BlockSizes = Array.Empty<int>();
        /// Floats of the default-block uniforms' values, recorded per draw in uniform order.
        public int LooseLength;
        /// Bytes of the uniform blocks recorded per draw, one after another (each at least 16).
        public int BlocksLength;
        /// Programs with the same attribute locations share a layout id (vertex arrays are
        /// built per mesh and layout).
        public int LayoutId;
        /// The values last given to each default-block uniform (indexed like Uniforms): a
        /// program keeps its uniform state, so an unchanged value is not sent again.
        public float[][] Sent;
    }

    /// A shader's compiled programs by variant blob index.
    internal sealed unsafe class DeviceProgramSet
    {
        public readonly Dictionary<int, DeviceProgram> Programs = new();

        public void Delete()
        {
            foreach (var p in Programs.Values)
                if (p.Id != 0)
                {
                    GlBindings.ForgetProgram(p.Id);
                    N.glDeleteProgram(new GlProgram(p.Id));
                }
            Programs.Clear();
        }
    }

    /// Compiles Unity's GLSL variants and binds their inputs by name, as the program text
    /// declares them (HLSLcc names: `hlslcc_mtx4x4X` for matrix X as four column vectors,
    /// `in_POSITION0`… for vertex inputs).
    internal static unsafe class GlPrograms
    {
        static readonly string[] attributeNames =
        [
            "in_POSITION0", "in_NORMAL0", "in_TANGENT0", "in_COLOR0",
            "in_TEXCOORD0", "in_TEXCOORD1", "in_TEXCOORD2", "in_TEXCOORD3", "in_TEXCOORD4", "in_TEXCOORD5", "in_TEXCOORD6", "in_TEXCOORD7",
        ];

        /// The shader's program for a variant blob, built on first use and kept in the shader's
        /// Gpu slot.
        public static DeviceProgram Get(ShaderNative shader, int blobIndex)
        {
            if (shader.Gpu is not DeviceProgramSet set)
            {
                set = new DeviceProgramSet();
                shader.Gpu = set;
                GraphicsDevice.Adopt(shader, set);
            }
            if (set.Programs.TryGetValue(blobIndex, out var p)) return p;
            p = Build(shader, shader.Programs.Variant(blobIndex));
            set.Programs[blobIndex] = p;
            return p;
        }

        /// GLSL 1.50 needs extensions for the explicit uniform locations and block bindings
        /// Unity's program text uses (UNITY_LOCATION / UNITY_BINDING).
        static string Prepare(string section)
        {
            int versionEnd = section.IndexOf('\n') + 1;
            var extensions = "";
            if (section.Contains("UNITY_LOCATION(", StringComparison.Ordinal)) extensions += "#extension GL_ARB_explicit_uniform_location : require\n";
            if (section.Contains("UNITY_BINDING(", StringComparison.Ordinal)) extensions += "#extension GL_ARB_shading_language_420pack : require\n";
            return section[..versionEnd] + extensions + section[versionEnd..];
        }

        /// Extensions the context offers (GL_EXTENSIONS).
        public static HashSet<string> Extensions = new(StringComparer.Ordinal);

        /// A variant is supported when every extension its stages require is available (VR
        /// multiview variants need GL_OVR_multiview2, for instance).
        public static bool Supported(ShaderVariant variant)
        {
            foreach (var source in new[] { variant.VertexSource, variant.FragmentSource, variant.GeometrySource })
            {
                if (source == null) continue;
                foreach (var line in source.Split('\n'))
                {
                    var t = line.Trim();
                    if (!t.StartsWith("#extension ", StringComparison.Ordinal) || !t.EndsWith(": require", StringComparison.Ordinal)) continue;
                    var name = t["#extension ".Length..t.IndexOf(':')].Trim();
                    if (!Extensions.Contains(name)) return false;
                }
            }
            return true;
        }

        static DeviceProgram Build(ShaderNative shader, ShaderVariant variant)
        {
            var program = new DeviceProgram();
            if (!Supported(variant)) return program;
            uint vs = GlApi.CompileShader(VertexShader, Prepare(variant.VertexSource), out var vlog);
            uint fs = GlApi.CompileShader(FragmentShader, Prepare(variant.FragmentSource), out var flog);
            uint gs = 0;
            string glog = "";
            if (variant.GeometrySource != null) gs = GlApi.CompileShader(GeometryShader, Prepare(variant.GeometrySource), out glog);
            if (vs == 0 || fs == 0 || (variant.GeometrySource != null && gs == 0))
            {
                Debug.LogError($"Shader '{shader.ShaderName}' variant {variant.BlobIndex} [{string.Join(" ", variant.Keywords)}] failed to compile:\n{vlog}\n{flog}\n{glog}");
                foreach (var id in new[] { vs, fs, gs })
                    if (id != 0) N.glDeleteShader(new GlShader(id));
                return program;
            }
            program.Id = GlApi.LinkProgram(vs, fs, gs, out var llog);
            foreach (var id in new[] { vs, fs, gs })
                if (id != 0) N.glDeleteShader(new GlShader(id));
            if (program.Id == 0)
            {
                Debug.LogError($"Shader '{shader.ShaderName}' variant {variant.BlobIndex} failed to link:\n{llog}");
                return program;
            }
            for (int i = 0; i < attributeNames.Length; i++) program.Attributes[i] = GlApi.AttribLocation(program.Id, attributeNames[i]);
            var layout = string.Join(",", program.Attributes);
            if (!layouts.TryGetValue(layout, out program.LayoutId)) layouts[layout] = program.LayoutId = layouts.Count;
            Reflect(program);
            foreach (var u in program.Uniforms)
                if (u.Block < 0) program.LooseLength += GlUniforms.Length(u);
            foreach (var size in program.BlockSizes) program.BlocksLength += Math.Max(size, 16);
            return program;
        }

        static readonly Dictionary<string, int> layouts = new();

        static void Reflect(DeviceProgram p)
        {
            GlBindings.UseProgram(p.Id);
            int blocks = GlApi.GetProgram(p.Id, ActiveUniformBlocks);
            p.BlockSizes = new int[blocks];
            for (uint b = 0; b < blocks; b++)
            {
                p.BlockSizes[b] = GlApi.UniformBlockParameter(p.Id, b, UniformBlockDataSize);
                N.glUniformBlockBinding(new GlProgram(p.Id), b, b);
            }
            int count = GlApi.GetProgram(p.Id, ActiveUniforms);
            int unit = 0;
            for (uint i = 0; i < count; i++)
            {
                var (rawName, type, size) = GlApi.ActiveUniform(p.Id, i);
                var name = rawName.EndsWith("[0]", StringComparison.Ordinal) ? rawName[..^3] : rawName;
                if (IsSampler(type))
                {
                    int location = GlApi.UniformLocation(p.Id, rawName);
                    var s = new SamplerBinding { Location = location, Unit = unit++, Target = SamplerTarget(type), PropertyId = ShaderPropertyIds.Get(name) };
                    N.glUniform1i(location, s.Unit);
                    p.Samplers.Add(s);
                    continue;
                }
                var u = new UniformBinding { Name = name, Type = type, Size = size };
                u.Block = GlApi.ActiveUniformParameter(p.Id, i, UniformBlockIndex);
                if (u.Block >= 0)
                {
                    u.Offset = GlApi.ActiveUniformParameter(p.Id, i, UniformOffset);
                    u.ArrayStride = GlApi.ActiveUniformParameter(p.Id, i, UniformArrayStride);
                }
                else u.Location = GlApi.UniformLocation(p.Id, rawName);
                Classify(u, name);
                p.Uniforms.Add(u);
            }
        }

        static void Classify(UniformBinding u, string name)
        {
            const string matrixPrefix = "hlslcc_mtx4x4";
            if (name.StartsWith(matrixPrefix, StringComparison.Ordinal))
            {
                u.IsMatrix = true;
                name = name[matrixPrefix.Length..];
            }
            u.Source = name switch
            {
                "unity_ObjectToWorld" => UniformSource.ObjectToWorld,
                "unity_WorldToObject" => UniformSource.WorldToObject,
                "unity_MatrixV" => UniformSource.MatrixV,
                "unity_MatrixInvV" => UniformSource.MatrixInvV,
                "glstate_matrix_projection" => UniformSource.MatrixP,
                "unity_MatrixVP" => UniformSource.MatrixVP,
                "unity_MatrixInvVP" => UniformSource.MatrixInvVP,
                "glstate_matrix_mvp" or "unity_MatrixMVP" => UniformSource.MatrixMVP,
                "unity_CameraProjection" => UniformSource.CameraProjection,
                "unity_CameraInvProjection" => UniformSource.CameraInvProjection,
                "unity_CameraToWorld" => UniformSource.CameraToWorld,
                "unity_WorldToCamera" => UniformSource.WorldToCamera,
                "_Time" => UniformSource.Time,
                "_SinTime" => UniformSource.SinTime,
                "_CosTime" => UniformSource.CosTime,
                "unity_DeltaTime" => UniformSource.DeltaTime,
                "_ScreenParams" => UniformSource.ScreenParams,
                "_ProjectionParams" => UniformSource.ProjectionParams,
                "_ZBufferParams" => UniformSource.ZBufferParams,
                "unity_OrthoParams" => UniformSource.OrthoParams,
                "_WorldSpaceCameraPos" => UniformSource.WorldSpaceCameraPos,
                "unity_WorldTransformParams" => UniformSource.WorldTransformParams,
                _ => UniformSource.Property,
            };
            if (u.Source != UniformSource.Property) return;
            // Texture-derived vectors: X_ST, X_TexelSize, X_HDR (an explicit X_ST property wins).
            if (name.EndsWith("_ST", StringComparison.Ordinal)) { u.Source = UniformSource.TextureScaleOffset; u.PropertyId = ShaderPropertyIds.Get(name[..^3]); }
            else if (name.EndsWith("_TexelSize", StringComparison.Ordinal)) { u.Source = UniformSource.TextureTexelSize; u.PropertyId = ShaderPropertyIds.Get(name[..^10]); }
            else if (name.EndsWith("_HDR", StringComparison.Ordinal)) { u.Source = UniformSource.TextureHdr; u.PropertyId = ShaderPropertyIds.Get(name[..^4]); }
            else u.PropertyId = ShaderPropertyIds.Get(name);
            u.Name = name;
        }

        static bool IsSampler(uint type) => type is Sampler2D or Sampler3D or SamplerCube or Sampler2DShadow or Sampler2DArray or Sampler2DMultisample or IntSampler2D or UnsignedIntSampler2D;

        static uint SamplerTarget(uint type) => type switch
        {
            Sampler3D => TexTarget3D,
            Sampler2DArray => TexTarget2DArray,
            Sampler2DMultisample => TexTarget2DMultisample,
            _ => TexTarget2D,
        };
    }

    /// Everything a draw's uniforms can depend on.
    internal sealed class DrawInputs
    {
        public Matrix4x4 ObjectToWorld = Matrix4x4.identity;
        public CameraConstants Camera;
        public PropertySheet Block;
        public MaterialNative Material;
        public ShaderNative Shader;

        /// Property lookup order: the renderer's block, the material, the globals.
        public bool TryFloat(int id, out float v)
        {
            if (Block != null && Block.Floats.TryGetValue(id, out v)) return true;
            if (Material != null && Material.Properties.Floats.TryGetValue(id, out v)) return true;
            if (ShaderGlobals.Properties.Floats.TryGetValue(id, out v)) return true;
            if (Block != null && Block.Ints.TryGetValue(id, out var i)) { v = i; return true; }
            if (Material != null && Material.Properties.Ints.TryGetValue(id, out i)) { v = i; return true; }
            if (ShaderGlobals.Properties.Ints.TryGetValue(id, out i)) { v = i; return true; }
            v = 0f;
            return false;
        }

        public bool TryVector(int id, out Vector4 v)
        {
            if (Block != null && Block.Vectors.TryGetValue(id, out v)) return true;
            if (Material != null && Material.Properties.Vectors.TryGetValue(id, out v)) return true;
            if (ShaderGlobals.Properties.Vectors.TryGetValue(id, out v)) return true;
            v = default;
            return false;
        }

        public bool TryMatrix(int id, out Matrix4x4 m)
        {
            if (Block != null && Block.Matrices.TryGetValue(id, out m)) return true;
            if (Material != null && Material.Properties.Matrices.TryGetValue(id, out m)) return true;
            if (ShaderGlobals.Properties.Matrices.TryGetValue(id, out m)) return true;
            m = Matrix4x4.zero;
            return false;
        }

        public bool TryTexture(int id, out TextureBinding t)
        {
            if (Block != null && Block.Textures.TryGetValue(id, out t)) return true;
            if (Material != null && Material.Properties.Textures.TryGetValue(id, out t)) return true;
            if (ShaderGlobals.Properties.Textures.TryGetValue(id, out t)) return true;
            t = default;
            return false;
        }

        public T[] Array<T>(int id, Func<PropertySheet, Dictionary<int, T[]>> select)
        {
            if (Block != null && select(Block).TryGetValue(id, out var a)) return a;
            if (Material != null && select(Material.Properties).TryGetValue(id, out a)) return a;
            return select(ShaderGlobals.Properties).TryGetValue(id, out a) ? a : null;
        }
    }

    /// Writes uniform values: loose uniforms through glUniform*, block members into std140
    /// buffers.
    internal static unsafe class GlUniforms
    {
        static readonly float[] scratch = new float[4 * 1024];
        /// Array uniforms tightly packed for glUniform*fv.
        static readonly float[] packedScratch = new float[4 * 1024];

        static int Components(uint type) => type switch
        {
            FloatType or IntType or UnsignedIntType or BoolType => 1,
            FloatVec2 or IntVec2 => 2,
            FloatVec3 or IntVec3 => 3,
            FloatMat4 => 16,
            _ => 4,
        };

        static bool IsInt(uint type) => type is IntType or IntVec2 or IntVec3 or IntVec4 or BoolType;
        static bool IsUint(uint type) => type is UnsignedIntType or UintVec4;

        /// Values as floats, `count` elements of `stride` floats (vec4 slots).
        static int Resolve(UniformBinding u, DrawInputs d, Span<float> o)
        {
            var cam = d.Camera;
            if (u.IsMatrix)
            {
                int matrices = Math.Max(1, u.Size / 4);
                if (u.Source == UniformSource.Property && matrices > 1)
                {
                    var arr = d.Array(u.PropertyId, s => s.MatrixArrays);
                    for (int i = 0; i < matrices; i++) WriteMatrix(arr != null && i < arr.Length ? arr[i] : Matrix4x4.zero, o[(16 * i)..]);
                    return matrices * 16;
                }
                Matrix4x4 m = u.Source switch
                {
                    UniformSource.ObjectToWorld => d.ObjectToWorld,
                    UniformSource.WorldToObject => d.ObjectToWorld.inverse,
                    UniformSource.MatrixV => cam.View,
                    UniformSource.MatrixInvV => cam.View.inverse,
                    UniformSource.MatrixP or UniformSource.CameraProjection => cam.Projection,
                    UniformSource.CameraInvProjection => cam.Projection.inverse,
                    UniformSource.MatrixVP => cam.Projection * cam.View,
                    UniformSource.MatrixInvVP => (cam.Projection * cam.View).inverse,
                    UniformSource.MatrixMVP => cam.Projection * cam.View * d.ObjectToWorld,
                    UniformSource.CameraToWorld => cam.View.inverse * Matrix4x4.Scale(new Vector3(1f, 1f, -1f)),
                    UniformSource.WorldToCamera => Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * cam.View,
                    _ => d.TryMatrix(u.PropertyId, out var pm) ? pm : Matrix4x4.zero,
                };
                WriteMatrix(m, o);
                return 16;
            }
            Vector4 v;
            switch (u.Source)
            {
                case UniformSource.Time:
                {
                    float t = Time.timeSinceLevelLoad;
                    v = new Vector4(t / 20f, t, t * 2f, t * 3f);
                    break;
                }
                case UniformSource.SinTime:
                {
                    float t = Time.timeSinceLevelLoad;
                    v = new Vector4(Mathf.Sin(t / 8f), Mathf.Sin(t / 4f), Mathf.Sin(t / 2f), Mathf.Sin(t));
                    break;
                }
                case UniformSource.CosTime:
                {
                    float t = Time.timeSinceLevelLoad;
                    v = new Vector4(Mathf.Cos(t / 8f), Mathf.Cos(t / 4f), Mathf.Cos(t / 2f), Mathf.Cos(t));
                    break;
                }
                case UniformSource.DeltaTime:
                {
                    float dt = Time.deltaTime, sdt = Time.smoothDeltaTime;
                    v = new Vector4(dt, dt == 0f ? 0f : 1f / dt, sdt, sdt == 0f ? 0f : 1f / sdt);
                    break;
                }
                case UniformSource.ScreenParams:
                    v = new Vector4(cam.TargetSize.x, cam.TargetSize.y, 1f + 1f / cam.TargetSize.x, 1f + 1f / cam.TargetSize.y);
                    break;
                case UniformSource.ProjectionParams:
                    // OpenGL renders textures upright: no projection flip.
                    v = new Vector4(1f, cam.Near, cam.Far, 1f / cam.Far);
                    break;
                case UniformSource.ZBufferParams:
                {
                    float x = 1f - cam.Far / cam.Near, y = cam.Far / cam.Near;
                    v = new Vector4(x, y, x / cam.Far, y / cam.Far);
                    break;
                }
                case UniformSource.OrthoParams:
                {
                    float h = 1f / cam.Projection.m11, w = 1f / cam.Projection.m00;
                    v = new Vector4(w, h, 0f, cam.Orthographic ? 1f : 0f);
                    break;
                }
                case UniformSource.WorldSpaceCameraPos:
                    v = new Vector4(cam.Position.x, cam.Position.y, cam.Position.z, 0f);
                    break;
                case UniformSource.WorldTransformParams:
                    v = new Vector4(0f, 0f, 0f, d.ObjectToWorld.determinant < 0f ? -1f : 1f);
                    break;
                case UniformSource.TextureScaleOffset:
                    v = d.TryVector(ShaderPropertyIds.Get(ShaderPropertyIds.NameOf(u.PropertyId) + "_ST"), out var st) ? st
                        : d.TryTexture(u.PropertyId, out var tb) ? tb.ScaleOffset : TextureBinding.IdentityScaleOffset;
                    break;
                case UniformSource.TextureTexelSize:
                {
                    var tex = d.TryTexture(u.PropertyId, out var tb2) ? tb2.Texture : null;
                    v = tex != null ? new Vector4(1f / tex.width, 1f / tex.height, tex.width, tex.height) : new Vector4(1f / 4f, 1f / 4f, 4f, 4f);
                    break;
                }
                case UniformSource.TextureHdr:
                    v = new Vector4(1f, 1f, 0f, 0f);
                    break;
                default:
                    return ResolveProperty(u, d, o);
            }
            o[0] = v.x;
            o[1] = v.y;
            o[2] = v.z;
            o[3] = v.w;
            return 4;
        }

        static int ResolveProperty(UniformBinding u, DrawInputs d, Span<float> o)
        {
            int components = Components(u.Type);
            if (u.Size > 1)
            {
                if (components == 1)
                {
                    var arr = d.Array(u.PropertyId, s => s.FloatArrays);
                    for (int i = 0; i < u.Size; i++) o[i * 4] = arr != null && i < arr.Length ? arr[i] : 0f;
                }
                else
                {
                    var arr = d.Array(u.PropertyId, s => s.VectorArrays);
                    for (int i = 0; i < u.Size; i++)
                    {
                        var e = arr != null && i < arr.Length ? arr[i] : default;
                        o[i * 4] = e.x;
                        o[i * 4 + 1] = e.y;
                        o[i * 4 + 2] = e.z;
                        o[i * 4 + 3] = e.w;
                    }
                }
                return u.Size * 4;
            }
            if (components == 1)
            {
                d.TryFloat(u.PropertyId, out var f);
                o[0] = f;
                return 1;
            }
            d.TryVector(u.PropertyId, out var vec);
            o[0] = vec.x;
            o[1] = vec.y;
            o[2] = vec.z;
            o[3] = vec.w;
            return 4;
        }

        static void WriteMatrix(Matrix4x4 m, Span<float> o)
        {
            MemoryMarshal.Cast<Matrix4x4, float>(new ReadOnlySpan<Matrix4x4>(in m)).CopyTo(o);
        }

        /// The number of floats Resolve gives for a uniform.
        public static int Length(UniformBinding u)
        {
            if (u.IsMatrix)
            {
                int matrices = Math.Max(1, u.Size / 4);
                return u.Source == UniformSource.Property && matrices > 1 ? matrices * 16 : 16;
            }
            if (u.Source != UniformSource.Property) return 4;
            if (u.Size > 1) return u.Size * 4;
            return Components(u.Type) == 1 ? 1 : 4;
        }

        /// Default-block uniforms sent and skipped since the last frame counters (--frame-stats).
        public static int UniformsSent, UniformsSkipped;

        /// Resolves a draw's uniforms (main thread): default-block values into `values` in
        /// uniform order, block members into `blocks` (laid out as DeviceProgram.BlocksLength).
        public static void Record(DeviceProgram p, DrawInputs d, Span<float> values, Span<byte> blocks)
        {
            Span<float> o = scratch;
            Span<int> blockStarts = stackalloc int[p.BlockSizes.Length];
            for (int b = 0, at = 0; b < blockStarts.Length; b++)
            {
                blockStarts[b] = at;
                at += Math.Max(p.BlockSizes[b], 16);
            }
            int next = 0;
            foreach (var u in p.Uniforms)
            {
                int length = Length(u);
                if (u.Block >= 0)
                {
                    int n = Resolve(u, d, o);
                    if (n != length) throw new InvalidOperationException($"Uniform '{u.Name}': {n} values resolved, {length} expected.");
                    WriteBlock(blocks.Slice(blockStarts[u.Block], p.BlockSizes[u.Block]), u, o[..n]);
                    continue;
                }
                var target = values.Slice(next, length);
                next += length;
                // Slots a value leaves unwritten (padding of float arrays) compare equal from draw to draw.
                target.Clear();
                int written = Resolve(u, d, target);
                if (written != length) throw new InvalidOperationException($"Uniform '{u.Name}': {written} values resolved, {length} expected.");
            }
        }

        /// Sets the default-block uniforms recorded for a draw (GL thread), skipping values the
        /// program already holds.
        public static void Send(DeviceProgram p, ReadOnlySpan<float> values)
        {
            Span<int> ints = stackalloc int[4];
            var sent = p.Sent ??= new float[p.Uniforms.Count][];
            int next = 0;
            for (int index = 0; index < p.Uniforms.Count; index++)
            {
                var u = p.Uniforms[index];
                if (u.Block >= 0) continue;
                int n = Length(u);
                var o = values.Slice(next, n);
                next += n;
                if (u.Location < 0) continue;
                var last = sent[index];
                if (last != null && MemoryMarshal.Cast<float, int>(last.AsSpan()).SequenceEqual(MemoryMarshal.Cast<float, int>(o))) // bitwise: -0 and NaNs are values too
                {
                    UniformsSkipped++;
                    continue;
                }
                UniformsSent++;
                if (last == null || last.Length != n) sent[index] = last = new float[n];
                o.CopyTo(last);
                fixed (float* f = o)
                {
                    if (u.IsMatrix || u.Size > 1 && Components(u.Type) == 4) N.glUniform4fv(u.Location, n / 4, f);
                    else if (IsInt(u.Type))
                    {
                        int count = Math.Min(4, Components(u.Type));
                        for (int i = 0; i < count; i++) ints[i] = (int)o[i];
                        fixed (int* ip = ints)
                        {
                            if (count == 1) N.glUniform1iv(u.Location, 1, ip);
                            else N.glUniform4iv(u.Location, 1, ip);
                        }
                    }
                    else if (IsUint(u.Type))
                    {
                        uint ui = (uint)o[0];
                        N.glUniform1uiv(u.Location, 1, &ui);
                    }
                    else if (u.Size > 1)
                    {
                        // An array in one call from its base location, elements tightly packed:
                        // GL gives no other element a location that is safe to assume.
                        int comps = Components(u.Type);
                        var packed = packedScratch.AsSpan(0, u.Size * comps);
                        for (int i = 0; i < u.Size; i++)
                            for (int c = 0; c < comps; c++) packed[i * comps + c] = o[i * 4 + c];
                        fixed (float* pp = packed)
                        {
                            switch (comps)
                            {
                                case 1: N.glUniform1fv(u.Location, u.Size, pp); break;
                                case 2: N.glUniform2fv(u.Location, u.Size, pp); break;
                                default: N.glUniform3fv(u.Location, u.Size, pp); break;
                            }
                        }
                    }
                    else switch (Components(u.Type))
                    {
                        case 1: N.glUniform1fv(u.Location, 1, f); break;
                        case 2: N.glUniform2fv(u.Location, 1, f); break;
                        case 3: N.glUniform3fv(u.Location, 1, f); break;
                        default: N.glUniform4fv(u.Location, 1, f); break;
                    }
                }
            }
        }

        /// std140: element i of an array at offset + i * stride; matrices are four columns.
        static void WriteBlock(Span<byte> block, UniformBinding u, ReadOnlySpan<float> values)
        {
            var target = MemoryMarshal.Cast<byte, float>(block);
            int components = u.IsMatrix ? 4 : Math.Min(4, Components(u.Type));
            int elements = u.IsMatrix ? values.Length / 4 : Math.Max(1, u.Size);
            int stride = u.ArrayStride > 0 ? u.ArrayStride / 4 : 4;
            int start = u.Offset / 4;
            for (int e = 0; e < elements; e++)
            {
                int src = (u.IsMatrix || u.Size > 1) ? e * 4 : 0;
                for (int c = 0; c < components && src + c < values.Length; c++)
                {
                    int dst = start + e * stride + c;
                    if (dst >= target.Length) return;
                    if (IsInt(u.Type)) target[dst] = BitConverter.Int32BitsToSingle((int)values[src + c]);
                    else target[dst] = values[src + c];
                }
            }
        }
    }
}
