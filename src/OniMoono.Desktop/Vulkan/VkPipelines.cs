using System;
using System.Collections.Generic;
using Brutal.VulkanApi;
using OniMoono.Desktop.Gl;

namespace OniMoono.Desktop.Vulkan
{
    /// The attachment formats a pipeline renders into (up to eight colours, as OpenGL's
    /// draw buffers here), and whether the target is a render texture, which Unity's Vulkan
    /// programs render into upside down (RenderConventions), so front faces wind the other way.
    internal readonly record struct VkTargetFormats(int Count, VkFormat C0, VkFormat C1, VkFormat C2, VkFormat C3, VkFormat C4, VkFormat C5, VkFormat C6, VkFormat C7,
        VkFormat Depth, VkFormat Stencil, int Samples, bool Flipped)
    {
        public VkFormat Color(int i) => i switch { 0 => C0, 1 => C1, 2 => C2, 3 => C3, 4 => C4, 5 => C5, 6 => C6, _ => C7 };

        public static VkTargetFormats Of(ReadOnlySpan<VkFormat> colors, VkFormat depth, VkFormat stencil, int samples, bool flipped)
        {
            Span<VkFormat> f = stackalloc VkFormat[8];
            colors.CopyTo(f);
            return new(colors.Length, f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], depth, stencil, samples, flipped);
        }
    }

    /// A pipeline's identity: program, resolved pass state, vertex layout, topology and targets.
    internal readonly record struct VkPipelineKey(VkProgram Program, ResolvedState State, long VertexLayout, VkPrimitiveTopology Topology, VkTargetFormats Targets);

    /// Pipelines made on first use and kept (with the driver's pipeline cache across launches),
    /// keyed by everything OpenGL sets as context state for a draw: the pass state ShaderLab
    /// resolved (blend, colour mask, depth, cull, offset, stencil), the vertex layout and the
    /// target formats. Unity's Vulkan conventions: depth is reversed (comparisons and the depth
    /// offset turned around, far at 0); front faces wind clockwise as seen on the screen, which
    /// is clockwise in Vulkan's framebuffer (y down) for the back buffer and counter-clockwise
    /// in a render texture, which is rendered upside down.
    internal sealed unsafe class VkPipelines
    {
        readonly VkContext vk;
        readonly Dictionary<VkPipelineKey, VkPipeline> pipelines = new();

        /// Pipelines created (diagnostics).
        public int Created;
        public long CreateTicks;

        public VkPipelines(VkContext vk) => this.vk = vk;

        /// The interleaved layout of immediate geometry (GL.Begin): position, colour, eight
        /// texture coordinates, as GlDevice.DrawImmediate writes them.
        public const long ImmediateLayout = -1;
        public const int ImmediateStride = sizeof(float) * (3 + 4 + 32);

        /// A mesh layout as a pipeline key: per channel its components (4 bits each) and the
        /// colours' kind.
        public static long LayoutKey(MeshLayout layout)
        {
            long key = layout.ByteColors ? 1L << 48 : 0;
            for (int c = 0; c < (int)Channel.Count; c++) key |= (long)(layout.Components[c] + 8 & 15) << (c * 4);
            return key;
        }

        /// The pipeline for a draw; a null handle when the driver refused it (logged once per
        /// program, and remembered, so later draws with the key are left out without trying).
        public VkPipeline Get(in VkPipelineKey key, MeshLayout layout)
        {
            if (pipelines.TryGetValue(key, out var p)) return p;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                p = Create(key, layout);
            }
            catch (Exception e) when (!VkDevice.IsDeviceLost(e))
            {
                System.Threading.Interlocked.Increment(ref Refused);
                if (refused.Add(key.Program))
                    OniMoono.Engine.EngineLog.Engine($"vulkan: a pipeline for shader '{key.Program.Name}' was refused, its draws with that state are left out: {e.GetType().Name}: {e.Message}");
                p = default;
            }
            CreateTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            Created++;
            pipelines[key] = p;
            return p;
        }

        readonly HashSet<VkProgram> refused = new();

        /// Pipelines the driver refused (--check-shaders).
        public static int Refused;

        /// A channel's attribute format and stride as the mesh stores it (GlMeshes.VertexArray).
        public static (VkFormat Format, int Stride) ChannelFormat(MeshLayout layout, int c)
        {
            int components = layout.Components[c];
            if (c == (int)Channel.Color && layout.ByteColors) return (VkFormat.R8G8B8A8UNorm, 4);
            int size = Math.Abs(components);
            var format = size switch { 1 => VkFormat.R32SFloat, 2 => VkFormat.R32G32SFloat, 3 => VkFormat.R32G32B32SFloat, _ => VkFormat.R32G32B32A32SFloat };
            return (format, components < 0 ? 16 : size * 4);
        }

        VkPipeline Create(in VkPipelineKey key, MeshLayout layout)
        {
            var program = key.Program;
            var s = key.State;
            var t = key.Targets;
            int inputs = program.Inputs.Length;
            var bindings = stackalloc VkVertexInputBindingDescription[Math.Max(1, inputs)];
            var attributes = stackalloc VkVertexInputAttributeDescription[Math.Max(1, inputs)];
            for (int i = 0; i < inputs; i++)
            {
                var (location, channel, kind) = program.Inputs[i];
                VkFormat format;
                int stride;
                if (key.VertexLayout == ImmediateLayout)
                {
                    bool constant = channel < 0 || channel == (int)Channel.Normal || channel == (int)Channel.Tangent;
                    (format, stride) = constant ? (ConstantFormat(kind), 0)
                        : (channel == (int)Channel.Position ? VkFormat.R32G32B32SFloat : VkFormat.R32G32B32A32SFloat, ImmediateStride);
                }
                else if (channel >= 0 && layout.Components[channel] != 0) (format, stride) = ChannelFormat(layout, channel);
                else (format, stride) = (ConstantFormat(kind), 0);
                bindings[i] = new VkVertexInputBindingDescription { Binding = location, Stride = (Brutal.ByteSize32)stride, InputRate = VkVertexInputRate.Vertex };
                attributes[i] = new VkVertexInputAttributeDescription { Location = location, Binding = location, Format = format, Offset = (Brutal.ByteSize32)0 };
            }
            var vertexInput = new VkPipelineVertexInputStateCreateInfo
            {
                VertexBindingDescriptionCount = inputs, VertexBindingDescriptions = bindings,
                VertexAttributeDescriptionCount = inputs, VertexAttributeDescriptions = attributes,
            };
            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo { Topology = key.Topology };
            var viewport = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
            bool offset = s.OffsetFactor != 0f || s.OffsetUnits != 0f;
            var raster = new VkPipelineRasterizationStateCreateInfo
            {
                PolygonMode = VkPolygonMode.Fill,
                CullMode = s.Cull == 1 ? VkCullModeFlags.FrontBit : s.Cull == 2 ? VkCullModeFlags.BackBit : VkCullModeFlags.None,
                FrontFace = t.Flipped ? VkFrontFace.CounterClockwise : VkFrontFace.Clockwise,
                // Reversed depth: an offset towards the camera is a positive one.
                DepthBiasEnable = offset,
                DepthBiasConstantFactor = -s.OffsetUnits,
                DepthBiasSlopeFactor = -s.OffsetFactor,
                LineWidth = 1f,
            };
            var multisample = new VkPipelineMultisampleStateCreateInfo
            {
                RasterizationSamples = (VkSampleCountFlags)Math.Max(1, t.Samples),
                AlphaToCoverageEnable = s.AlphaToMask != 0,
            };
            bool depth = t.Depth != VkFormat.Undefined, stencil = t.Stencil != VkFormat.Undefined && (s.FComp != 0 || s.BComp != 0);
            var depthStencil = new VkPipelineDepthStencilStateCreateInfo
            {
                DepthTestEnable = depth && s.ZTest != 0,
                DepthWriteEnable = depth && s.ZTest != 0 && s.ZWrite != 0,
                DepthCompareOp = Compare(Reversed(s.ZTest)),
                StencilTestEnable = stencil,
                Front = StencilFace(s.FComp, s.FPass, s.FFail, s.FZFail, s),
                Back = StencilFace(s.BComp, s.BPass, s.BFail, s.BZFail, s),
                MaxDepthBounds = 1f,
            };
            // Blending: One/Zero for colour and alpha with Add is off. OpenGL's one blend state
            // applies to every draw buffer.
            bool blend = !(s.Src == 1 && s.Dst == 0 && s.SrcA == 1 && s.DstA == 0 && s.Op == 0 && s.OpA == 0);
            var mask = ((s.Mask & 8) != 0 ? VkColorComponentFlags.RBit : 0) | ((s.Mask & 4) != 0 ? VkColorComponentFlags.GBit : 0)
                       | ((s.Mask & 2) != 0 ? VkColorComponentFlags.BBit : 0) | ((s.Mask & 1) != 0 ? VkColorComponentFlags.ABit : 0);
            var attachment = new VkPipelineColorBlendAttachmentState
            {
                BlendEnable = blend,
                SrcColorBlendFactor = Factor(s.Src), DstColorBlendFactor = Factor(s.Dst), ColorBlendOp = Op(s.Op),
                SrcAlphaBlendFactor = Factor(s.SrcA), DstAlphaBlendFactor = Factor(s.DstA), AlphaBlendOp = Op(s.OpA),
                ColorWriteMask = mask,
            };
            var attachments = stackalloc VkPipelineColorBlendAttachmentState[Math.Max(1, t.Count)];
            var formats = stackalloc VkFormat[Math.Max(1, t.Count)];
            for (int i = 0; i < t.Count; i++)
            {
                attachments[i] = attachment;
                formats[i] = t.Color(i);
            }
            var colorBlend = new VkPipelineColorBlendStateCreateInfo { AttachmentCount = t.Count, Attachments = attachments };
            var dynamicStates = stackalloc VkDynamicState[] { VkDynamicState.Viewport, VkDynamicState.Scissor };
            var dynamic = new VkPipelineDynamicStateCreateInfo { DynamicStateCount = 2, DynamicStates = dynamicStates };
            var rendering = new VkPipelineRenderingCreateInfo
            {
                ColorAttachmentCount = t.Count, ColorAttachmentFormats = formats, DepthAttachmentFormat = t.Depth, StencilAttachmentFormat = t.Stencil,
            };
            var stages = stackalloc VkPipelineShaderStageCreateInfo[2];
            fixed (byte* main = "main\0"u8)
            {
                stages[0] = new VkPipelineShaderStageCreateInfo { Stage = VkShaderStageFlags.VertexBit, Module = program.Vertex, Name = main };
                stages[1] = new VkPipelineShaderStageCreateInfo { Stage = VkShaderStageFlags.FragmentBit, Module = program.Fragment, Name = main };
                var info = new VkGraphicsPipelineCreateInfo
                {
                    Next = &rendering,
                    StageCount = 2, Stages = stages,
                    VertexInputState = &vertexInput, InputAssemblyState = &inputAssembly, ViewportState = &viewport,
                    RasterizationState = &raster, MultisampleState = &multisample, DepthStencilState = &depthStencil,
                    ColorBlendState = &colorBlend, DynamicState = &dynamic, Layout = program.Layout,
                };
                VkPipeline pipeline = default;
                VkCheck.Ok(vk.Device.PInvoke.vkCreateGraphicsPipelines.Invoke(vk.Device.Handle, vk.PipelineCache, 1, &info, null, &pipeline), $"vkCreateGraphicsPipelines ({program.Name})");
                return pipeline;
            }
        }

        static VkFormat ConstantFormat(char kind) => kind switch
        {
            'i' => VkFormat.R32G32B32A32SInt,
            'u' => VkFormat.R32G32B32A32UInt,
            _ => VkFormat.R32G32B32A32SFloat,
        };

        static VkStencilOpState StencilFace(int comp, int pass, int fail, int zFail, in ResolvedState s) => new()
        {
            CompareOp = Compare(comp == 0 ? 8 : comp),
            PassOp = StencilOp(pass),
            FailOp = StencilOp(fail),
            DepthFailOp = StencilOp(zFail),
            CompareMask = s.ReadMask,
            WriteMask = s.WriteMask,
            Reference = s.StencilRef,
        };

        /// Unity BlendMode → Vulkan blend factor.
        static VkBlendFactor Factor(int m) => m switch
        {
            0 => VkBlendFactor.Zero,
            1 => VkBlendFactor.One,
            2 => VkBlendFactor.DstColor,
            3 => VkBlendFactor.SrcColor,
            4 => VkBlendFactor.OneMinusDstColor,
            5 => VkBlendFactor.SrcAlpha,
            6 => VkBlendFactor.OneMinusSrcColor,
            7 => VkBlendFactor.DstAlpha,
            8 => VkBlendFactor.OneMinusDstAlpha,
            9 => VkBlendFactor.SrcAlphaSaturate,
            10 => VkBlendFactor.OneMinusSrcAlpha,
            _ => VkBlendFactor.One,
        };

        static VkBlendOp Op(int op) => op switch
        {
            1 => VkBlendOp.Subtract,
            2 => VkBlendOp.ReverseSubtract,
            3 => VkBlendOp.Min,
            4 => VkBlendOp.Max,
            _ => VkBlendOp.Add,
        };

        /// A depth comparison for reversed depth (UNITY_REVERSED_Z): Less and Greater, LessEqual
        /// and GreaterEqual swap.
        public static int Reversed(int c) => c switch { 2 => 5, 5 => 2, 4 => 7, 7 => 4, _ => c };

        /// Unity CompareFunction → Vulkan.
        public static VkCompareOp Compare(int c) => c switch
        {
            1 => VkCompareOp.Never,
            2 => VkCompareOp.Less,
            3 => VkCompareOp.Equal,
            4 => VkCompareOp.LessOrEqual,
            5 => VkCompareOp.Greater,
            6 => VkCompareOp.NotEqual,
            7 => VkCompareOp.GreaterOrEqual,
            _ => VkCompareOp.Always,
        };

        static VkStencilOp StencilOp(int op) => op switch
        {
            1 => VkStencilOp.Zero,
            2 => VkStencilOp.Replace,
            3 => VkStencilOp.IncrementAndClamp,
            4 => VkStencilOp.DecrementAndClamp,
            5 => VkStencilOp.Invert,
            6 => VkStencilOp.IncrementAndWrap,
            7 => VkStencilOp.DecrementAndWrap,
            _ => VkStencilOp.Keep,
        };

        /// Destroys the pipelines of a program being released (device thread, its frames done).
        public void Forget(VkProgram program, List<VkPipeline> into)
        {
            List<VkPipelineKey> keys = null;
            foreach (var (k, p) in pipelines)
                if (k.Program == program)
                {
                    (keys ??= new()).Add(k);
                    into.Add(p);
                }
            if (keys != null) foreach (var k in keys) pipelines.Remove(k);
        }

        public void Dispose()
        {
            foreach (var p in pipelines.Values) vk.Device.DestroyPipeline(p, null);
            pipelines.Clear();
        }
    }
}
