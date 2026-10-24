using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OniBrutal.Assets.Shaders;
using OniBrutal.Desktop.Gl;
using OniBrutal.Engine.Rendering;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Desktop.Metal
{
    /// The scalar type of a constant's components.
    internal enum ConstantKind
    {
        Float,
        Int,
        UInt,
    }

    /// A member of a Metal constant buffer: where Metal reads it and where its value comes from.
    internal sealed class MetalConstant
    {
        /// As the parameter blob records it (offset in the buffer, Metal's layout).
        public ShaderConstant Constant;
        /// Its value source, as the GL device classifies the uniform of the same name
        /// (GlPrograms.Classify): HLSLcc's names, `hlslcc_mtx4x4X` for a matrix X.
        public UniformBinding Binding;
        public ConstantKind Kind;
        /// Components per element: a vector's columns, four per matrix column.
        public int Components;
        /// Elements: an array's, four columns per matrix.
        public int Elements;
        /// Bytes between elements: the element's MSL size rounded up to its alignment (a vector
        /// of three takes 16 bytes, scalars of an array 4).
        public int Stride;
    }

    /// A constant buffer of a Metal stage program (`VGlobals`, `FGlobals`, Unity's built-in
    /// `UnityPerDraw`, `UnityPerFrame`…): its `[[ buffer(n) ]]` slot, the bytes the blob records
    /// and its members.
    internal sealed class MetalConstantBuffer
    {
        public string Name;
        public int Slot;
        public int Size;
        public MetalConstant[] Members;
    }

    /// Packs the values a draw gives a Metal program into the layout its parameter blob records.
    ///
    /// Values are resolved as the GL device resolves the uniforms of the same names
    /// (GlUniforms.Resolve: the renderer's block, then the material, then the globals; built-in
    /// matrices and constants from the draw and the camera), so a Metal draw sees what the GL draw
    /// sees. Offsets are the blob's; strides are MSL's, which the blob's offsets follow (an array of
    /// floats is tight, unlike GLCore's and D3D's one 16-byte register per element). The camera's
    /// clip-space conventions are the caller's: the device gives DrawInputs the camera constants it
    /// renders with (docs/research/metal-backend-design.md §5).
    internal static class MetalConstantBuffers
    {
        const string MatrixPrefix = "hlslcc_mtx4x4";

        /// A stage's bound constant buffers (those with a constant-buffer binding), in binding order.
        public static MetalConstantBuffer[] Layout(ShaderParameters parameters)
        {
            var result = new List<MetalConstantBuffer>();
            foreach (var binding in parameters.ConstantBufferBindings)
            {
                var cb = parameters.Buffer(binding.Name) ?? throw new InvalidOperationException($"Constant buffer binding '{binding.Name}' has no buffer.");
                var members = new MetalConstant[cb.Members.Length];
                for (int i = 0; i < members.Length; i++) members[i] = Member(cb.Members[i]);
                result.Add(new MetalConstantBuffer { Name = cb.Name, Slot = binding.Index, Size = cb.Size, Members = members });
            }
            return result.ToArray();
        }

        static MetalConstant Member(ShaderConstant c)
        {
            var kind = c.Type switch
            {
                0 => ConstantKind.Float,
                1 => ConstantKind.Int,
                5 => ConstantKind.UInt,
                _ => throw new NotSupportedException($"Constant '{c.Name}' of ShaderParamType {c.Type} (no game program has one)."),
            };
            if (c.IsMatrix)
            {
                if (c.Rows != 4 || c.Columns != 4 || kind != ConstantKind.Float) throw new NotSupportedException($"Constant '{c.Name}': a {c.Rows}x{c.Columns} matrix (game programs have float4x4 only).");
                var m = new UniformBinding { Name = MatrixPrefix + c.Name, Type = FloatVec4, Size = 4 * c.Elements };
                GlPrograms.Classify(m, MatrixPrefix + c.Name);
                return new MetalConstant { Constant = c, Binding = m, Kind = kind, Components = 4, Elements = 4 * c.Elements, Stride = 16 };
            }
            if (c.Rows != 1 || c.Columns is < 1 or > 4) throw new NotSupportedException($"Constant '{c.Name}': {c.Rows}x{c.Columns}.");
            var u = new UniformBinding { Name = c.Name, Type = GlType(kind, c.Columns), Size = c.Elements };
            GlPrograms.Classify(u, c.Name);
            int size = 4 * (c.Columns == 3 ? 4 : c.Columns);
            return new MetalConstant { Constant = c, Binding = u, Kind = kind, Components = c.Columns, Elements = c.Elements, Stride = c.Elements > 1 ? size : 4 * c.Columns };
        }

        /// The GL uniform type of the same shape (what GL reflection reports for it).
        static uint GlType(ConstantKind kind, int columns) => (kind, columns) switch
        {
            (ConstantKind.Float, 1) => FloatType,
            (ConstantKind.Float, 2) => FloatVec2,
            (ConstantKind.Float, 3) => FloatVec3,
            (ConstantKind.Float, _) => FloatVec4,
            (ConstantKind.Int, 1) => IntType,
            (ConstantKind.Int, 2) => IntVec2,
            (ConstantKind.Int, 3) => IntVec3,
            (ConstantKind.Int, _) => IntVec4,
            (ConstantKind.UInt, 1) => UnsignedIntType,
            (ConstantKind.UInt, 2) => UintVec2,
            (ConstantKind.UInt, 3) => UintVec3,
            _ => UintVec4,
        };

        /// Resolve's output per thread (draw queues are recorded on worker threads).
        [ThreadStatic] static float[] scratch;

        /// Writes a buffer's values for a draw into `target` (at least Size bytes): every member at
        /// its offset, bytes no member covers zero.
        public static void Pack(MetalConstantBuffer buffer, DrawInputs d, Span<byte> target)
        {
            if (target.Length < buffer.Size) throw new ArgumentException($"Constant buffer '{buffer.Name}' needs {buffer.Size} bytes.", nameof(target));
            target[..buffer.Size].Clear();
            var o = scratch ??= new float[4 * 1024];
            foreach (var m in buffer.Members)
            {
                int n = GlUniforms.Resolve(m.Binding, d, o);
                Write(m, o.AsSpan(0, n), target);
            }
        }

        /// One member's resolved values (GlUniforms.Resolve's layout: elements of an array or a
        /// matrix's columns in vec4 slots, a single scalar or vector from slot 0) at its offset.
        static void Write(MetalConstant m, ReadOnlySpan<float> values, Span<byte> target)
        {
            var words = MemoryMarshal.Cast<byte, int>(target);
            bool slotted = m.Elements > 1 || m.Constant.IsMatrix;
            for (int e = 0; e < m.Elements; e++)
            {
                int src = slotted ? e * 4 : 0;
                for (int c = 0; c < m.Components; c++)
                {
                    int at = m.Constant.Offset + e * m.Stride + 4 * c;
                    if (src + c >= values.Length || at + 4 > m.Constant.Offset + Size(m) || at + 4 > target.Length) break;
                    float v = values[src + c];
                    words[at / 4] = m.Kind switch
                    {
                        ConstantKind.Int => (int)v,
                        ConstantKind.UInt => (int)(uint)v,
                        _ => BitConverter.SingleToInt32Bits(v),
                    };
                }
            }
        }

        /// The bytes a member occupies: its elements' strides, the last one only its components.
        public static int Size(MetalConstant m) => (m.Elements - 1) * m.Stride + 4 * m.Components;
    }
}
