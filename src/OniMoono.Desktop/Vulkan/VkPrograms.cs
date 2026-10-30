using System;
using System.Collections.Generic;
using Brutal.VulkanApi;
using OniMoono.Desktop.Gl;
using OniMoono.Engine;
using OniMoono.Engine.Rendering;
using static OniMoono.Desktop.Gl.GlConst;

namespace OniMoono.Desktop.Vulkan
{
    /// What a draw's descriptor set holds at one binding.
    internal readonly record struct VkBindingSlot(int Binding, VkDescriptorType Type, int Index);

    /// A program on Vulkan (DeviceProgram.Vk): its shader modules, descriptor set layout and
    /// pipeline layout, what each binding takes from a recorded draw (a uniform block by its
    /// index in DeviceProgram.BlockSizes, a sampler by its index in DeviceProgram.Samplers),
    /// and the vertex inputs. Pipelines are made per state, vertex layout, topology and target
    /// formats (VkPipelines).
    internal sealed class VkProgram
    {
        public VkShaderModule Vertex, Fragment;
        public VkDescriptorSetLayout SetLayout;
        public VkPipelineLayout Layout;
        public VkBindingSlot[] Slots;
        /// Descriptors are pushed (VK_KHR_push_descriptor) rather than allocated.
        public bool Push;
        /// Vertex inputs: location, and whether the shader reads integers (then a constant
        /// input is bound with an integer format).
        public (int Location, char Kind)[] Inputs;
        public string Name;
        public int Id;

        public void Destroy(VkContext vk)
        {
            if (!Layout.IsNull()) vk.Device.DestroyPipelineLayout(Layout, null);
            if (!SetLayout.IsNull()) vk.Device.DestroyDescriptorSetLayout(SetLayout, null);
            if (!Vertex.IsNull()) vk.Device.DestroyShaderModule(Vertex, null);
            if (!Fragment.IsNull()) vk.Device.DestroyShaderModule(Fragment, null);
            Layout = default;
            SetLayout = default;
            Vertex = Fragment = default;
        }
    }

    /// Builds DevicePrograms from Unity's GLCore variants for the Vulkan device (device
    /// thread): compiled by VkShaderCompiler, bound as SpirvReflection reads the result, so
    /// that GlUniforms writes a draw's values (every loose uniform now a block member) and
    /// GlDrawRecorder resolves its textures exactly as for OpenGL.
    internal sealed unsafe class VkPrograms : IDisposable
    {
        readonly VkContext vk;
        readonly VkShaderCompiler compiler = new();
        static readonly Dictionary<string, int> layouts = new();
        static int nextId = 1;
        readonly HashSet<string> reported = new();

        /// Programs built and their compile time (diagnostics).
        public int Built;
        public long BuildTicks;

        public VkPrograms(VkContext vk) => this.vk = vk;

        public DeviceProgram Get(ShaderNative shader, int blobIndex)
        {
            if (shader.Gpu is not DeviceProgramSet set)
            {
                set = new DeviceProgramSet();
                shader.Gpu = set;
                GraphicsDevice.Adopt(shader, set);
            }
            if (set.Programs.TryGetValue(blobIndex, out var p)) return p;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            p = Build(shader, shader.Programs.Variant(blobIndex));
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            BuildTicks += ticks;
            Built++;
            System.Threading.Interlocked.Add(ref StartupStats.ProgramTicks, ticks);
            System.Threading.Interlocked.Increment(ref StartupStats.Programs);
            set.Programs[blobIndex] = p;
            return p;
        }

        void NotDrawn(ShaderNative shader, ShaderVariant variant, string why)
        {
            // Once per shader and reason (the warm-up meets every variant).
            if (reported.Add($"{shader.ShaderName}\n{why}"))
                EngineLog.Engine($"vulkan: shader '{shader.ShaderName}' variants with {why} are not drawn (first: variant {variant.BlobIndex} [{string.Join(" ", variant.Keywords)}])");
        }

        DeviceProgram Build(ShaderNative shader, ShaderVariant variant)
        {
            var program = new DeviceProgram();
            if (variant.GeometrySource != null)
            {
                NotDrawn(shader, variant, "a geometry stage");
                return program;
            }
            VkShaderCompiler.Compiled compiled;
            try
            {
                compiled = compiler.Compile(variant.VertexSource, variant.FragmentSource, shader.ShaderName);
            }
            catch (NotSupportedException e)
            {
                NotDrawn(shader, variant, e.Message);
                return program;
            }
            catch (InvalidOperationException e)
            {
                UnityEngine.Debug.LogError($"Shader '{shader.ShaderName}' variant {variant.BlobIndex} [{string.Join(" ", variant.Keywords)}] failed to compile for Vulkan:\n{e.Message}");
                return program;
            }
            var t = compiled.Program;
            foreach (var r in t.Resources)
                if (r.Kind == VkGlsl.ResourceKind.Sampler && SamplerTarget(r.Type) == 0)
                {
                    NotDrawn(shader, variant, $"sampler '{r.Name}' of type {r.Type}");
                    return program;
                }

            var vertexBlocks = SpirvReflection.UniformBlocks(compiled.Vertex);
            var fragmentBlocks = SpirvReflection.UniformBlocks(compiled.Fragment);
            var vkProgram = new VkProgram { Name = shader.ShaderName };
            var slots = new List<VkBindingSlot>();
            var blockSizes = new List<int>();
            // The blocks in binding order: each stage's globals, then the named blocks.
            void AddBlock(SpirvReflection.Block b)
            {
                int index = blockSizes.Count;
                blockSizes.Add(b.Size);
                slots.Add(new VkBindingSlot(b.Binding, VkDescriptorType.UniformBuffer, index));
                foreach (var m in b.Members)
                {
                    var u = new UniformBinding
                    {
                        Name = m.Name, Type = GlType(m), Size = m.ArrayLength > 0 ? m.ArrayLength : 1, Block = index, Offset = m.Offset,
                        ArrayStride = m.ArrayLength > 0 ? m.ArrayStride : 0,
                    };
                    GlPrograms.Classify(u, m.Name);
                    program.Uniforms.Add(u);
                }
            }
            SpirvReflection.Block Find(List<SpirvReflection.Block> blocks, int binding) => blocks.Find(b => b.Binding == binding);
            if (Find(vertexBlocks, VkGlsl.VertexGlobalsBinding) is { } vg) AddBlock(vg);
            if (Find(fragmentBlocks, VkGlsl.FragmentGlobalsBinding) is { } fg) AddBlock(fg);
            foreach (var r in t.Resources)
            {
                switch (r.Kind)
                {
                    case VkGlsl.ResourceKind.UniformBlock:
                        if ((Find(vertexBlocks, r.Binding) ?? Find(fragmentBlocks, r.Binding)) is { } b) AddBlock(b);
                        break;
                    case VkGlsl.ResourceKind.StorageBuffer:
                        // Read from an empty buffer, as OpenGL's device binds none.
                        slots.Add(new VkBindingSlot(r.Binding, VkDescriptorType.StorageBuffer, -1));
                        break;
                    case VkGlsl.ResourceKind.Sampler:
                        slots.Add(new VkBindingSlot(r.Binding, VkDescriptorType.CombinedImageSampler, program.Samplers.Count));
                        program.Samplers.Add(new SamplerBinding
                        {
                            Location = r.Binding, Unit = program.Samplers.Count, Target = SamplerTarget(r.Type), PropertyId = ShaderPropertyIds.Get(r.Name),
                        });
                        break;
                }
            }
            program.BlockSizes = blockSizes.ToArray();
            foreach (var size in program.BlockSizes) program.BlocksLength += Math.Max(size, 16);
            for (int c = 0; c < program.Attributes.Length; c++) program.Attributes[c] = t.Inputs.ContainsKey(c) ? c : -1;
            var inputs = new List<(int, char)>();
            foreach (var (location, (_, type)) in t.Inputs) inputs.Add((location, type.StartsWith('i') ? 'i' : type.StartsWith('u') ? 'u' : 'f'));
            vkProgram.Inputs = inputs.ToArray();
            vkProgram.Slots = slots.ToArray();
            var layoutKey = string.Join(",", vkProgram.Inputs);
            lock (layouts)
            {
                if (!layouts.TryGetValue(layoutKey, out program.LayoutId)) layouts[layoutKey] = program.LayoutId = layouts.Count;
                program.Id = (uint)nextId++;
            }
            vkProgram.Id = (int)program.Id;
            try
            {
                CreateObjects(vkProgram, compiled);
            }
            catch
            {
                vkProgram.Destroy(vk);
                throw;
            }
            program.Vk = vkProgram;
            return program;
        }

        void CreateObjects(VkProgram p, VkShaderCompiler.Compiled compiled)
        {
            p.Vertex = Module(compiled.Vertex);
            p.Fragment = Module(compiled.Fragment);
            p.Push = vk.PushDescriptors && p.Slots.Length <= vk.MaxPushDescriptors;
            var bindings = stackalloc VkDescriptorSetLayoutBinding[Math.Max(1, p.Slots.Length)];
            for (int i = 0; i < p.Slots.Length; i++)
                bindings[i] = new VkDescriptorSetLayoutBinding
                {
                    Binding = p.Slots[i].Binding, DescriptorType = p.Slots[i].Type, DescriptorCount = 1,
                    StageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit,
                };
            p.SetLayout = vk.Device.CreateDescriptorSetLayout(new VkDescriptorSetLayoutCreateInfo
            {
                BindingCount = p.Slots.Length, Bindings = bindings,
                Flags = p.Push ? VkDescriptorSetLayoutCreateFlags.PushDescriptorBitKHR : 0,
            }, null);
            var setLayout = p.SetLayout;
            p.Layout = vk.Device.CreatePipelineLayout(new VkPipelineLayoutCreateInfo { SetLayoutCount = 1, SetLayouts = &setLayout }, null);
        }

        VkShaderModule Module(byte[] spirv)
        {
            fixed (byte* code = spirv)
                return vk.Device.CreateShaderModule(new VkShaderModuleCreateInfo { CodeSize = (Brutal.ByteSize64)spirv.Length, Code = (int*)code }, null);
        }

        /// SPIR-V for the presenter's own GLSL.
        public VkShaderModule Own(string source, bool vertex, string name) => Module(compiler.CompileOwn(source, vertex, name));

        /// The OpenGL uniform type a block member has (GlUniforms writes values by it).
        static uint GlType(SpirvReflection.Member m)
        {
            if (m.Columns == 4 && m.Components == 4) return FloatMat4;
            int n = m.Components;
            return m.Type switch
            {
                SpirvReflection.Scalar.Float => n switch { 1 => FloatType, 2 => FloatVec2, 3 => FloatVec3, _ => FloatVec4 },
                SpirvReflection.Scalar.Int => n switch { 1 => IntType, 2 => IntVec2, 3 => IntVec3, _ => IntVec4 },
                SpirvReflection.Scalar.Uint => n switch { 1 => UnsignedIntType, 2 => UintVec2, 3 => UintVec3, _ => UintVec4 },
                _ => n switch { 1 => BoolType, 2 => BoolVec2, 3 => BoolVec3, _ => BoolVec4 },
            };
        }

        /// The texture target a sampler reads (OpenGL's names, as DeviceTexture records them);
        /// 0 for samplers this device does not bind (shadow comparison and multisample
        /// samplers, which only shadow and frame-debugger variants declare).
        static uint SamplerTarget(string type) => type switch
        {
            "sampler2D" or "isampler2D" or "usampler2D" => TexTarget2D,
            "sampler3D" => TexTarget3D,
            "sampler2DArray" => TexTarget2DArray,
            "samplerCube" => TexTargetCubeMap,
            _ => 0,
        };

        public void Dispose() => compiler.Dispose();
    }
}
