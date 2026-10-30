using System;
using System.Collections.Generic;
using System.Text;

namespace OniMoono.Desktop.Vulkan
{
    /// The uniform blocks of a SPIR-V module, read from its decorations: each block's binding
    /// and its members' offsets, strides and types, as glGetActiveUniform* reports them for the
    /// same declarations on OpenGL. Only what VkGlsl's output declares is understood: blocks of
    /// scalars, vectors, matrices and arrays of them.
    internal static class SpirvReflection
    {
        public enum Scalar { Float, Int, Uint, Bool }

        public sealed record Member(string Name, int Offset, Scalar Type, int Components, int Columns, int ArrayLength, int ArrayStride, int MatrixStride)
        {
            /// Bytes the member spans.
            public int Size
            {
                get
                {
                    int element = Columns > 1 ? MatrixStride * Columns : 4 * Components;
                    return ArrayLength > 0 ? ArrayStride * (ArrayLength - 1) + element : element;
                }
            }
        }

        public sealed record Block(string Name, int Binding, int Size, IReadOnlyList<Member> Members);

        const uint Magic = 0x07230203;
        const int OpName = 5, OpMemberName = 6, OpTypeBool = 20, OpTypeInt = 21, OpTypeFloat = 22, OpTypeVector = 23, OpTypeMatrix = 24,
            OpTypeArray = 28, OpTypeStruct = 30, OpTypePointer = 32, OpConstant = 43, OpVariable = 59, OpDecorate = 71, OpMemberDecorate = 72;
        const int DecorationBlock = 2, DecorationArrayStride = 6, DecorationMatrixStride = 7, DecorationBinding = 33, DecorationOffset = 35;
        const int StorageUniform = 2;

        sealed class TypeInfo
        {
            public int Op;
            public int[] Operands;
        }

        public static List<Block> UniformBlocks(ReadOnlySpan<byte> module)
        {
            if (module.Length < 20 || module.Length % 4 != 0) throw new FormatException("not a SPIR-V module");
            var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(module);
            if (words[0] != Magic) throw new FormatException("not a SPIR-V module (magic)");
            var names = new Dictionary<int, string>();
            var memberNames = new Dictionary<(int, int), string>();
            var decorations = new Dictionary<(int, int), int>();
            var memberDecorations = new Dictionary<(int, int, int), int>();
            var blocks = new HashSet<int>();
            var types = new Dictionary<int, TypeInfo>();
            var constants = new Dictionary<int, uint>();
            var variables = new List<(int Type, int Id)>();
            for (int i = 5; i < words.Length;)
            {
                int count = (int)(words[i] >> 16), op = (int)(words[i] & 0xFFFF);
                if (count == 0 || i + count > words.Length) throw new FormatException("truncated SPIR-V instruction");
                var w = words.Slice(i + 1, count - 1);
                switch (op)
                {
                    case OpName: names[(int)w[0]] = String(w[1..]); break;
                    case OpMemberName: memberNames[((int)w[0], (int)w[1])] = String(w[2..]); break;
                    case OpDecorate:
                        if ((int)w[1] == DecorationBlock) blocks.Add((int)w[0]);
                        else if (w.Length > 2) decorations[((int)w[0], (int)w[1])] = (int)w[2];
                        break;
                    case OpMemberDecorate:
                        if (w.Length > 3) memberDecorations[((int)w[0], (int)w[1], (int)w[2])] = (int)w[3];
                        break;
                    case OpTypeBool or OpTypeInt or OpTypeFloat or OpTypeVector or OpTypeMatrix or OpTypeArray or OpTypeStruct or OpTypePointer:
                    {
                        var operands = new int[w.Length - 1];
                        for (int k = 1; k < w.Length; k++) operands[k - 1] = (int)w[k];
                        types[(int)w[0]] = new TypeInfo { Op = op, Operands = operands };
                        break;
                    }
                    case OpConstant: if (w.Length >= 3) constants[(int)w[1]] = w[2]; break;
                    case OpVariable: if ((int)w[2] == StorageUniform) variables.Add(((int)w[0], (int)w[1])); break;
                }
                i += count;
            }
            var result = new List<Block>();
            foreach (var (pointerType, id) in variables)
            {
                if (!types.TryGetValue(pointerType, out var pointer) || pointer.Op != OpTypePointer) continue;
                int structId = pointer.Operands[1];
                if (!blocks.Contains(structId) || !types.TryGetValue(structId, out var st) || st.Op != OpTypeStruct) continue;
                var members = new List<Member>();
                int size = 0;
                for (int m = 0; m < st.Operands.Length; m++)
                {
                    var member = MemberOf(types, constants, memberNames.GetValueOrDefault((structId, m), $"member{m}"), st.Operands[m],
                        memberDecorations.GetValueOrDefault((structId, m, DecorationOffset)),
                        memberDecorations.GetValueOrDefault((structId, m, DecorationMatrixStride), 16), decorations);
                    members.Add(member);
                    size = Math.Max(size, member.Offset + member.Size);
                }
                // As GL_UNIFORM_BLOCK_DATA_SIZE for std140: rounded up to a vec4.
                size = (size + 15) & ~15;
                result.Add(new Block(names.GetValueOrDefault(structId, ""), decorations.GetValueOrDefault((id, DecorationBinding), -1), size, members));
            }
            return result;
        }

        static Member MemberOf(Dictionary<int, TypeInfo> types, Dictionary<int, uint> constants, string name, int typeId, int offset, int matrixStride, Dictionary<(int, int), int> decorations)
        {
            int arrayLength = 0, arrayStride = 0;
            var t = types[typeId];
            if (t.Op == OpTypeArray)
            {
                arrayLength = (int)constants[t.Operands[1]];
                arrayStride = decorations.GetValueOrDefault((typeId, DecorationArrayStride));
                t = types[t.Operands[0]];
            }
            int columns = 1, components = 1;
            if (t.Op == OpTypeMatrix)
            {
                columns = t.Operands[1];
                t = types[t.Operands[0]];
            }
            if (t.Op == OpTypeVector)
            {
                components = t.Operands[1];
                t = types[t.Operands[0]];
            }
            var scalar = t.Op switch
            {
                OpTypeFloat => Scalar.Float,
                OpTypeInt => t.Operands[1] != 0 ? Scalar.Int : Scalar.Uint,
                OpTypeBool => Scalar.Bool,
                _ => throw new NotSupportedException($"uniform block member '{name}' of a kind not supported (op {t.Op})"),
            };
            return new Member(name, offset, scalar, components, columns, arrayLength, arrayStride, matrixStride);
        }

        public enum DescriptorKind { UniformBuffer, StorageBuffer, CombinedImageSampler, SampledImage, Sampler, Other }

        /// A resource variable's descriptor: set, binding, kind, and for images the
        /// dimensionality (SPIR-V Dim: 1 2D, 2 3D, 3 cube), arrayed and multisampled flags.
        public readonly record struct Descriptor(int Set, int Binding, DescriptorKind Kind, int Dim, bool Arrayed, bool Multisampled);

        /// A stage input: its location and whether it reads integers ('i', 'u') or floats ('f').
        public readonly record struct Input(int Location, char Kind);

        /// The module's descriptors and its stage inputs (built-ins left out), and whether it
        /// samples with depth comparison (shadow samplers).
        public static (List<Descriptor> Descriptors, List<Input> Inputs, bool DepthCompare) Interface(ReadOnlySpan<byte> module)
        {
            var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(module);
            if (words.Length < 5 || words[0] != Magic) throw new FormatException("not a SPIR-V module");
            var types = new Dictionary<int, (int Op, uint[] Operands)>();
            var decorations = new Dictionary<(int, int), int>();
            var blocks = new HashSet<int>();
            var bufferBlocks = new HashSet<int>();
            var builtIns = new HashSet<int>();
            var variables = new List<(int Type, int Id, int Storage)>();
            bool depthCompare = false;
            for (int i = 5; i < words.Length;)
            {
                int count = (int)(words[i] >> 16), op = (int)(words[i] & 0xFFFF);
                if (count == 0 || i + count > words.Length) throw new FormatException("truncated SPIR-V instruction");
                var w = words.Slice(i + 1, count - 1);
                switch (op)
                {
                    case OpDecorate:
                        if ((int)w[1] == DecorationBlock) blocks.Add((int)w[0]);
                        else if ((int)w[1] == 3) bufferBlocks.Add((int)w[0]);
                        else if ((int)w[1] == 11) builtIns.Add((int)w[0]);
                        else if (w.Length > 2) decorations[((int)w[0], (int)w[1])] = (int)w[2];
                        break;
                    case OpMemberDecorate:
                        if (w.Length > 2 && (int)w[2] == 11) builtIns.Add((int)w[0]);
                        break;
                    case OpVariable: variables.Add(((int)w[0], (int)w[1], (int)w[2])); break;
                    // OpImageSample(Proj)Dref{Implicit,Explicit}Lod, OpImageDrefGather.
                    case 89 or 90 or 93 or 94 or 97: depthCompare = true; break;
                    default:
                        // Types: OpTypeBool..OpTypeSampledImage, OpTypeArray, OpTypeStruct, OpTypePointer.
                        if (op is >= 20 and <= 27 or 28 or 30 or 32)
                        {
                            var operands = new uint[w.Length - 1];
                            for (int k = 1; k < w.Length; k++) operands[k - 1] = w[k];
                            types[(int)w[0]] = (op, operands);
                        }
                        break;
                }
                i += count;
            }
            var descriptors = new List<Descriptor>();
            var inputs = new List<Input>();
            foreach (var (pointerType, id, storage) in variables)
            {
                if (!types.TryGetValue(pointerType, out var pointer) || pointer.Op != OpTypePointer) continue;
                int typeId = (int)pointer.Operands[1];
                if (storage == 1) // Input
                {
                    if (builtIns.Contains(id) || builtIns.Contains(typeId) || !decorations.TryGetValue((id, 30), out int location)) continue;
                    var t = types[typeId];
                    if (t.Op == OpTypeVector) t = types[(int)t.Operands[0]];
                    inputs.Add(new Input(location, t.Op == OpTypeInt ? (t.Operands[1] != 0 ? 'i' : 'u') : 'f'));
                    continue;
                }
                if (storage is not (0 or 2 or 12)) continue; // UniformConstant, Uniform, StorageBuffer
                int set = decorations.GetValueOrDefault((id, 34)), binding = decorations.GetValueOrDefault((id, DecorationBinding), -1);
                if (binding < 0) continue;
                var type = types[typeId];
                while (type.Op == OpTypeArray) type = types[(int)type.Operands[0]];
                int dim = 0;
                bool arrayed = false, multisampled = false;
                DescriptorKind kind;
                if (storage == 12 || bufferBlocks.Contains(typeId)) kind = DescriptorKind.StorageBuffer;
                else if (storage == 2) kind = DescriptorKind.UniformBuffer;
                else
                {
                    var image = type;
                    kind = type.Op switch
                    {
                        27 => DescriptorKind.CombinedImageSampler,
                        25 => DescriptorKind.SampledImage,
                        26 => DescriptorKind.Sampler,
                        _ => DescriptorKind.Other,
                    };
                    if (type.Op == 27) image = types[(int)type.Operands[0]];
                    if (image.Op == 25)
                    {
                        dim = (int)image.Operands[1];
                        arrayed = image.Operands[3] != 0;
                        multisampled = image.Operands[4] != 0;
                    }
                }
                descriptors.Add(new Descriptor(set, binding, kind, dim, arrayed, multisampled));
            }
            return (descriptors, inputs, depthCompare);
        }

        static string String(ReadOnlySpan<uint> words)
        {
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(words);
            int end = bytes.IndexOf((byte)0);
            return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
        }
    }
}
