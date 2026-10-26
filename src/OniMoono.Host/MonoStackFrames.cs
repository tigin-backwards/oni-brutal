using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using OniMoono.Engine;
using Cecil = Mono.Cecil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D23. A Harmony-patched method runs as a dynamic method that MonoMod creates with
    /// an owner type (MonoMod.Utils.DynamicMethodDefinition). On Mono, a stack frame of it reports a
    /// method whose DeclaringType and ReflectedType are that owner (measured in the original player);
    /// CoreCLR's StackFrame.GetMethod returns the DynamicMethod itself, whose DeclaringType and
    /// ReflectedType are null. A mod that walks the stack for the class that called it then fails on
    /// the null (Customize Plants, looking for the CreatePrefab that made a plant, ended the game's
    /// asset setup whenever another mod had patched one). A mod's calls to StackFrame.GetMethod are
    /// sent here, and a dynamic method with an owner is reported as one the owner declares.
    public static class MonoStackFrames
    {
        static readonly FieldInfo? OwnerField = typeof(DynamicMethod).GetField("_typeOwner", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly ConditionalWeakTable<DynamicMethod, MethodInfo> Owned = new();

        /// What Mono's StackFrame.GetMethod reports for the frame.
        public static MethodBase? GetMethod(StackFrame frame)
        {
            var method = frame.GetMethod();
            if (method is not DynamicMethod dynamic || OwnerField?.GetValue(dynamic) is not Type owner) return method;
            return Owned.GetValue(dynamic, d => new OwnedDynamicMethod(d, owner));
        }

        /// The image with its calls to StackFrame.GetMethod sent to GetMethod above, or null when it
        /// makes none. `symbols` (portable PDB bytes or null) are rewritten to match.
        internal static byte[]? Adjust(byte[] image, ref byte[]? symbols, string path)
        {
            if (!CallsGetMethod(image)) return null;
            try
            {
                return Rewrite(image, ref symbols, path);
            }
            catch (Exception e) when (e is Cecil.AssemblyResolutionException or BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                // The mod loads as it is: only its stack walks differ from Mono's.
                EngineLog.Engine($"mod stack frames: {Path.GetFileName(path)} not rewritten ({e.GetType().Name}: {e.Message}); it loads unchanged");
                return null;
            }
        }

        static byte[]? Rewrite(byte[] image, ref byte[]? symbols, string path)
        {
            // Writing the module back resolves types of its references (an enum default value's).
            using var resolver = LoadedFirstResolver.ForMod(path);
            bool withSymbols = symbols != null;
            Cecil.ModuleDefinition module;
            try
            {
                module = Cecil.ModuleDefinition.ReadModule(new MemoryStream(image), new Cecil.ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = withSymbols,
                    SymbolStream = withSymbols ? new MemoryStream(symbols!) : null,
                    SymbolReaderProvider = withSymbols ? new Cecil.Cil.PortablePdbReaderProvider() : null,
                });
            }
            catch (Exception e) when (withSymbols && e is Cecil.Cil.SymbolsNotMatchingException or Cecil.Cil.SymbolsNotFoundException or BadImageFormatException or InvalidOperationException)
            {
                withSymbols = false;
                module = Cecil.ModuleDefinition.ReadModule(new MemoryStream(image), new Cecil.ReaderParameters { AssemblyResolver = resolver });
            }
            using var owned = module;
            var replacement = module.ImportReference(typeof(MonoStackFrames).GetMethod(nameof(GetMethod))!);
            int calls = 0;
            foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    foreach (var instruction in method.Body.Instructions)
                        if ((instruction.OpCode == Cecil.Cil.OpCodes.Callvirt || instruction.OpCode == Cecil.Cil.OpCodes.Call)
                            && instruction.Operand is Cecil.MethodReference { Name: "GetMethod", Parameters.Count: 0, DeclaringType.FullName: "System.Diagnostics.StackFrame" })
                        {
                            instruction.OpCode = Cecil.Cil.OpCodes.Call;
                            instruction.Operand = replacement;
                            calls++;
                        }
                }
            if (calls == 0) return null;
            using var output = new MemoryStream();
            using var symbolOutput = new MemoryStream();
            module.Write(output, new Cecil.WriterParameters { WriteSymbols = withSymbols, SymbolStream = withSymbols ? symbolOutput : null, SymbolWriterProvider = withSymbols ? new Cecil.Cil.PortablePdbWriterProvider() : null });
            symbols = withSymbols ? symbolOutput.ToArray() : null;
            EngineLog.Engine($"mod stack frames: {Path.GetFileName(path)}: {calls} StackFrame.GetMethod call(s) report a patched method's owner, as on Mono");
            return output.ToArray();
        }

        /// Whether the image references StackFrame.GetMethod (read from its metadata alone).
        static bool CallsGetMethod(byte[] image)
        {
            try
            {
                using var pe = new PEReader(new MemoryStream(image));
                if (!pe.HasMetadata) return false;
                var reader = pe.GetMetadataReader();
                foreach (var handle in reader.MemberReferences)
                {
                    var member = reader.GetMemberReference(handle);
                    if (!reader.StringComparer.Equals(member.Name, "GetMethod") || member.Parent.Kind != HandleKind.TypeReference) continue;
                    var parent = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                    if (reader.StringComparer.Equals(parent.Name, "StackFrame") && reader.StringComparer.Equals(parent.Namespace, "System.Diagnostics")) return true;
                }
            }
            catch (BadImageFormatException)
            {
                // Not an assembly: the load reports it.
            }
            return false;
        }

        /// A dynamic method as Mono reports a frame of it: declared by its owner.
        sealed class OwnedDynamicMethod(DynamicMethod method, Type owner) : MethodInfo
        {
            public override string Name => method.Name;
            public override Type DeclaringType => owner;
            public override Type ReflectedType => owner;
            public override Module Module => owner.Module;
            public override MethodAttributes Attributes => method.Attributes;
            public override CallingConventions CallingConvention => method.CallingConvention;
            public override RuntimeMethodHandle MethodHandle => method.MethodHandle;
            public override Type ReturnType => method.ReturnType;
            public override ICustomAttributeProvider ReturnTypeCustomAttributes => method.ReturnTypeCustomAttributes;
            public override ParameterInfo ReturnParameter => method.ReturnParameter;
            public override MethodImplAttributes GetMethodImplementationFlags() => method.GetMethodImplementationFlags();
            public override ParameterInfo[] GetParameters() => method.GetParameters();
            public override MethodInfo GetBaseDefinition() => this;
            public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
                => method.Invoke(obj, invokeAttr, binder, parameters, culture);
            public override object[] GetCustomAttributes(bool inherit) => method.GetCustomAttributes(inherit);
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => method.GetCustomAttributes(attributeType, inherit);
            public override bool IsDefined(Type attributeType, bool inherit) => method.IsDefined(attributeType, inherit);
            public override string ToString() => method.ToString();
        }
    }
}
