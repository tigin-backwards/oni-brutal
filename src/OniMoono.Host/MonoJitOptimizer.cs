#nullable enable
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace OniMoono.Host
{
    /// An assembly's `[Debuggable(DebuggingModes)]` with `DisableOptimizations` is ignored, as
    /// Mono ignores it: Mono's JIT reads only the two-boolean constructor's
    /// `isJITOptimizerDisabled` (method-to-ir.c, is_jit_optimizer_disabled), and even then only
    /// stops inlining. CoreCLR honours the DebuggingModes form and compiles every method of such
    /// an assembly without optimisation and without tiering.
    ///
    /// U60's public test is a development build: its Assembly-CSharp and -firstpass carry
    /// DebuggingModes 0x107 (U59: 0x2), and so do its FMOD, ImGui, LibNoiseDotNet and
    /// ArabicSupport assemblies. Under CoreCLR the game's code ran unoptimised, while Unity's Mono
    /// player compiles it as before. Debug builds of mods carry the same attribute.
    ///
    /// The attribute's value blob keeps its size: only the DisableOptimizations bit of its int32
    /// changes, and the rest of the image is byte-identical.
    internal static class MonoJitOptimizer
    {
        const int DisableOptimizations = 0x100;

        /// `ONI_KEEP_DEBUGGABLE=1` leaves the attribute as it is, for measuring what it costs.
        static readonly bool Keep = Environment.GetEnvironmentVariable("ONI_KEEP_DEBUGGABLE") == "1";

        /// The image (changed in place) with the DebuggingModes attribute's DisableOptimizations
        /// cleared, or null when it has none to clear. `path` names the image in errors.
        public static byte[]? Adjust(byte[] image, string path)
        {
            if (Keep) return null;
            int at = -1, modes = 0;
            using (var pe = new PEReader(new MemoryStream(image, writable: false)))
            {
                if (!pe.HasMetadata) return null;
                var md = pe.GetMetadataReader();
                if (!md.IsAssembly) return null;
                if (!pe.PEHeaders.TryGetDirectoryOffset(pe.PEHeaders.CorHeader!.MetadataDirectory, out int metadataStart))
                    throw new BadImageFormatException($"{path}: metadata is not mapped in the file.");
                foreach (var handle in md.GetAssemblyDefinition().GetCustomAttributes())
                {
                    var attribute = md.GetCustomAttribute(handle);
                    if (!IsDebuggableModesConstructor(md, attribute.Constructor)) continue;
                    var value = md.GetBlobReader(attribute.Value);
                    // Prolog 0x0001, the int32 DebuggingModes, then the named arguments' count.
                    if (value.Length < 6 || value.ReadUInt16() != 1) continue;
                    modes = value.ReadInt32();
                    if ((modes & DisableOptimizations) == 0) return null;
                    int heap = metadataStart + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(attribute.Value);
                    byte first = image[heap];
                    int prefix = (first & 0x80) == 0 ? 1 : (first & 0xC0) == 0x80 ? 2 : 4;
                    at = heap + prefix + 2;
                    break;
                }
            }
            if (at < 0) return null;
            if (BitConverter.ToInt32(image, at) != modes) throw new BadImageFormatException($"{path}: DebuggableAttribute blob mismatch.");
            BitConverter.TryWriteBytes(image.AsSpan(at, 4), modes & ~DisableOptimizations);
            return image;
        }

        /// System.Diagnostics.DebuggableAttribute's constructor taking one argument (DebuggingModes).
        static bool IsDebuggableModesConstructor(MetadataReader md, EntityHandle constructor)
        {
            if (constructor.Kind != HandleKind.MemberReference) return false;
            var member = md.GetMemberReference((MemberReferenceHandle)constructor);
            if (member.Parent.Kind != HandleKind.TypeReference) return false;
            var type = md.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (!md.StringComparer.Equals(type.Name, "DebuggableAttribute") || !md.StringComparer.Equals(type.Namespace, "System.Diagnostics")) return false;
            var signature = md.GetBlobReader(member.Signature);
            signature.ReadSignatureHeader();
            return signature.ReadCompressedInteger() == 1;
        }
    }
}
