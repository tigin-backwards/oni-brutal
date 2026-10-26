using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Cecil = Mono.Cecil;
using Cil = Mono.Cecil.Cil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D31. On Linux and macOS, Harmony's MonoMod writes its native exception helper
    /// to a temporary file it opens with mkstemp (`PosixNativeLibraryDrop.DropLibrary`, on its
    /// first patch). On CoreCLR it writes through `new FileStream(fd, FileAccess.Write)`, which
    /// owns the descriptor and closes it when disposed, and then closes the descriptor again
    /// itself. A file another thread opens between the two closes gets that descriptor number and
    /// loses it: the host makes its first patches while the main thread opens the window, and the
    /// main thread's load of Microsoft.Extensions.ObjectPool (BRUTAL's logging, in GLFW's type
    /// initializer) failed with FileNotFoundException. The game's 0Harmony (and a mod's copy of
    /// MonoMod) is rewritten as it loads, in MonoConversions' pass: the FileStream does not own the
    /// descriptor (`FileStream(fd, access, ownsHandle: false)`), so it is closed once, by MonoMod.
    internal static class MonoModHelperDrop
    {
        const string Namespace = "MonoMod.Core.Platforms.Systems", Name = "PosixNativeLibraryDrop";

        /// Whether the image defines MonoMod's PosixNativeLibraryDrop.
        internal static bool MayDrop(byte[] image)
        {
            try
            {
                using var pe = new PEReader(new MemoryStream(image));
                if (!pe.HasMetadata) return false;
                var reader = pe.GetMetadataReader();
                foreach (var handle in reader.TypeDefinitions)
                {
                    var type = reader.GetTypeDefinition(handle);
                    if (reader.StringComparer.Equals(type.Name, Name) && reader.StringComparer.Equals(type.Namespace, Namespace)) return true;
                }
            }
            catch (BadImageFormatException)
            {
                // Not an assembly: the load reports it.
            }
            return false;
        }

        /// Makes PosixNativeLibraryDrop's FileStreams leave the descriptor to it; returns the
        /// constructions changed.
        internal static int RewriteModule(Cecil.ModuleDefinition module)
        {
            var drop = module.GetType(Namespace, Name);
            if (drop == null) return 0;
            int changed = 0;
            foreach (var method in drop.Methods)
            {
                if (!method.HasBody) continue;
                var il = method.Body.GetILProcessor();
                foreach (var instruction in method.Body.Instructions.ToArray())
                {
                    if (instruction.OpCode.Code != Cil.Code.Newobj || instruction.Operand is not Cecil.MethodReference ctor
                        || ctor.DeclaringType.FullName != "System.IO.FileStream" || ctor.Parameters.Count != 2
                        || ctor.Parameters[0].ParameterType.FullName != "System.IntPtr" || ctor.Parameters[1].ParameterType.FullName != "System.IO.FileAccess") continue;
                    // The argument goes in just before: nothing may jump to the construction itself.
                    if (method.Body.Instructions.Any(i => i.Operand == instruction || i.Operand is Cil.Instruction[] targets && targets.Contains(instruction))
                        || method.Body.ExceptionHandlers.Any(h => h.TryStart == instruction || h.HandlerStart == instruction || h.FilterStart == instruction)) continue;
                    // FileStream(IntPtr handle, FileAccess access, bool ownsHandle), with false.
                    var notOwning = new Cecil.MethodReference(".ctor", ctor.ReturnType, ctor.DeclaringType) { HasThis = true };
                    notOwning.Parameters.Add(new Cecil.ParameterDefinition(ctor.Parameters[0].ParameterType));
                    notOwning.Parameters.Add(new Cecil.ParameterDefinition(ctor.Parameters[1].ParameterType));
                    notOwning.Parameters.Add(new Cecil.ParameterDefinition(module.TypeSystem.Boolean));
                    il.InsertBefore(instruction, il.Create(Cil.OpCodes.Ldc_I4_0));
                    instruction.Operand = notOwning;
                    changed++;
                }
            }
            return changed;
        }
    }
}
