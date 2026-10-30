using System;
using System.Text;
using Brutal.ShaderCApi;

namespace OniMoono.Desktop.Vulkan
{
    /// Compiles a program's two GLCore stages to SPIR-V for Vulkan 1.1 (shaderc, at run time, as
    /// the GL driver compiles the same text): preprocessed, rewritten by VkGlsl, compiled.
    internal sealed unsafe class VkShaderCompiler : IDisposable
    {
        readonly Compiler compiler = ShaderC.CreateCompiler();

        public sealed class Compiled
        {
            public VkGlsl.Result Program;
            public byte[] Vertex, Fragment;
        }

        CompileOptions Options()
        {
            var options = ShaderC.CreateCompilerOptions();
            options.SetSourceLanguage(SourceLanguage.Glsl);
            options.SetTargetEnv(TargetEnv.Vulkan, (uint)EnvVersion.Vulkan_1_1);
            options.SetTargetSpirv(SpirvVersion._1_3);
            // The driver optimizes, as GL drivers do with the GLSL; spirv-opt would only cost time.
            options.SetOptimizationLevel(OptimizationLevel.Zero);
            options.SetSuppressWarnings();
            return options;
        }

        /// Throws NotSupportedException (VkGlsl) or ShaderException with the compiler's message.
        public Compiled Compile(string vertexSource, string fragmentSource, string name)
        {
            using var options = Options();
            string vertex = Preprocess(VkGlsl.BeforePreprocessing(vertexSource), ShaderKind.VertexShader, name, options);
            string fragment = Preprocess(VkGlsl.BeforePreprocessing(fragmentSource), ShaderKind.FragmentShader, name, options);
            var program = VkGlsl.Translate(vertex, fragment);
            return new Compiled
            {
                Program = program,
                Vertex = Spirv(program.Vertex, ShaderKind.VertexShader, name, options),
                Fragment = Spirv(program.Fragment, ShaderKind.FragmentShader, name, options),
            };
        }

        /// SPIR-V of GLSL written for this device (the presenter's own programs).
        public byte[] CompileOwn(string source, bool vertex, string name)
        {
            using var options = Options();
            return Spirv(source, vertex ? ShaderKind.VertexShader : ShaderKind.FragmentShader, name, options);
        }

        string Preprocess(string source, ShaderKind kind, string name, CompileOptions options)
        {
            using var result = Run(source, kind, name, options, preprocess: true);
            return Encoding.UTF8.GetString(result.GetBytes(), checked((int)result.GetLength().Value));
        }

        byte[] Spirv(string source, ShaderKind kind, string name, CompileOptions options)
        {
            using var result = Run(source, kind, name, options, preprocess: false);
            return new ReadOnlySpan<byte>(result.GetBytes(), checked((int)result.GetLength().Value)).ToArray();
        }

        CompilationResult Run(string source, ShaderKind kind, string name, CompileOptions options, bool preprocess)
        {
            var bytes = Encoding.UTF8.GetBytes(source);
            var file = Encoding.UTF8.GetBytes((kind == ShaderKind.VertexShader ? name + ".vert" : name + ".frag") + "\0");
            ReadOnlySpan<byte> main = "main\0"u8;
            var result = preprocess
                ? compiler.CompileIntoPreprocessedText(bytes, kind, file.AsSpan(), main, options)
                : compiler.CompileIntoSpv(bytes, kind, file.AsSpan(), main, options);
            if (result.GetCompilationStatus() == CompilationStatus.Success) return result;
            var message = result.GetErrorMessage();
            result.Dispose();
            throw new InvalidOperationException(message);
        }

        public void Dispose() => compiler.Dispose();
    }
}
