#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OniMoono.Host;
using Xunit;

namespace OniMoono.Tests
{
    /// An assembly's [Debuggable(DebuggingModes)] loses DisableOptimizations, as Mono ignores it
    /// (src/OniMoono.Host/MonoJitOptimizer.cs, compiled in); the two-boolean form, which Mono
    /// reads, and an assembly without the bit are left alone.
    public class MonoJitOptimizerTests
    {
        static byte[] Image(CustomAttributeBuilder? debuggable)
        {
            var builder = new PersistedAssemblyBuilder(new AssemblyName("JitProbe"), typeof(object).Assembly);
            if (debuggable != null) builder.SetCustomAttribute(debuggable);
            builder.DefineDynamicModule("JitProbe").DefineType("Probe", TypeAttributes.Public).CreateType();
            var stream = new MemoryStream();
            builder.Save(stream);
            return stream.ToArray();
        }

        static CustomAttributeBuilder Modes(DebuggableAttribute.DebuggingModes modes) =>
            new(typeof(DebuggableAttribute).GetConstructor([typeof(DebuggableAttribute.DebuggingModes)])!, [modes]);

        /// The attribute's arguments as stored: the int32 modes, or the two booleans.
        static string Stored(byte[] image)
        {
            using var pe = new PEReader(new MemoryStream(image));
            var md = pe.GetMetadataReader();
            foreach (var handle in md.GetAssemblyDefinition().GetCustomAttributes())
            {
                var value = md.GetBlobBytes(md.GetCustomAttribute(handle).Value);
                return value.Length == 8 ? $"0x{BitConverter.ToInt32(value, 2):x}" : $"{value[2]} {value[3]}";
            }
            return "none";
        }

        [Fact]
        public void ClearsDisableOptimizationsOnly()
        {
            // U60's development build: Default | DisableOptimizations | IgnoreSymbolStoreSequencePoints | EnableEditAndContinue.
            var image = Image(Modes((DebuggableAttribute.DebuggingModes)0x107));
            Assert.Equal("0x107", Stored(image));
            var before = (byte[])image.Clone();
            Assert.Same(image, MonoJitOptimizer.Adjust(image, "probe"));
            Assert.Equal("0x7", Stored(image));
            int changed = 0;
            for (int i = 0; i < image.Length; i++) if (image[i] != before[i]) changed++;
            Assert.Equal(1, changed);
        }

        [Fact]
        public void LeavesOtherAssembliesAlone()
        {
            // U59's release build.
            Assert.Null(MonoJitOptimizer.Adjust(Image(Modes(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)), "probe"));
            Assert.Null(MonoJitOptimizer.Adjust(Image(null), "probe"));
            // The two-boolean form is the one Mono reads (it then stops inlining only); it stays.
            var booleans = new CustomAttributeBuilder(typeof(DebuggableAttribute).GetConstructor([typeof(bool), typeof(bool)])!, [true, true]);
            var image = Image(booleans);
            Assert.Null(MonoJitOptimizer.Adjust(image, "probe"));
            Assert.Equal("1 1", Stored(image));
        }
    }
}
