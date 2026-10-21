using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using OniBrutal.Engine;
using Cecil = Mono.Cecil;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D22. A mod built against an older game can have a class that no longer
    /// implements every method of a game interface it lists (the game changed the method's
    /// signature). Mono loads such a class and fails only when it is used through the interface
    /// (as measured in the original player: "VTable setup of type … failed" from an IsAssignableFrom
    /// with an interface, while KMod's Assembly.GetTypes and its UserMod2 check pass). CoreCLR
    /// refuses the class when it loads, so GetTypes throws and KMod drops the whole mod (Wireless
    /// Power, whose side screen lacks the new ISliderControl.GetSliderTooltip). Before a mod's
    /// assembly is loaded, the host loads it once more in a collectible context that sees only the
    /// assemblies already loaded, collects the methods CoreCLR finds missing, and gives each such
    /// class an explicit implementation that throws the TypeLoadException CoreCLR would have: the
    /// class loads as on Mono, and using the missing method fails.
    internal static class MissingInterfaceMethods
    {
        static readonly Regex Missing = new(@"^Method '(?<method>[^']+)' in type '(?<type>[^']+)' from assembly '[^']*' does not have an implementation\.$", RegexOptions.CultureInvariant);

        /// The image completed with the missing methods, or null when nothing is missing.
        /// `symbols` (portable PDB bytes or null) are rewritten to match.
        public static byte[]? Adjust(byte[] image, ref byte[]? symbols, string path)
        {
            var missing = Probe(image, out var probe);
            Collect(probe);
            if (missing.Count == 0) return null;
            var (completed, newSymbols, added) = Complete(image, symbols, path, missing);
            if (added.Count == 0) return null;
            symbols = newSymbols;
            foreach (var line in added)
                EngineLog.Engine($"mod types: {Path.GetFileName(path)}: {line} (missing; calling it throws, as using the class does on Mono)");
            return completed;
        }

        /// Type full name (CLR form, nested with '+') → the method names CoreCLR finds missing.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static Dictionary<string, (string Message, HashSet<string> Methods)> Probe(byte[] image, out WeakReference probe)
        {
            var result = new Dictionary<string, (string Message, HashSet<string> Methods)>(StringComparer.Ordinal);
            var context = new ProbeContext();
            probe = new WeakReference(context);
            try
            {
                var assembly = context.LoadFromStream(new MemoryStream(image));
                try
                {
                    assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var loader in e.LoaderExceptions)
                    {
                        if (loader is not TypeLoadException t || Missing.Match(t.Message) is not { Success: true } m) continue;
                        var type = m.Groups["type"].Value;
                        if (!result.TryGetValue(type, out var entry)) result[type] = entry = (t.Message, new HashSet<string>(StringComparer.Ordinal));
                        entry.Methods.Add(m.Groups["method"].Value);
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // Not an assembly the probe can load: the real load reports it.
            }
            finally
            {
                context.Unload();
            }
            return result;
        }

        /// The probe's copy of the assembly is gone before the real one loads: until its context is
        /// collected, AppDomain.GetAssemblies lists it too, and a mod library that picks its
        /// instance from there (PLib) would run the probe's copy, whose Location is empty.
        static void Collect(WeakReference probe)
        {
            for (int i = 0; probe.IsAlive && i < 20; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            if (probe.IsAlive) EngineLog.Engine("mod types: a probe's load context was not collected; its copy of the mod stays listed");
        }

        /// Binds a probed mod's references only to assemblies already loaded, so probing loads
        /// nothing else (an unresolved reference fails only the probe's types that need it).
        sealed class ProbeContext() : AssemblyLoadContext("OniBrutal mod type probe", isCollectible: true)
        {
            protected override Assembly? Load(AssemblyName name)
            {
                foreach (var a in Default.Assemblies)
                    if (string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase)) return a;
                throw new FileNotFoundException($"{name.Name} is not loaded yet (type probe)");
            }
        }

        /// Resolves a reference to the assembly the runtime bound it to (an assembly already
        /// loaded with that name: the game's current interfaces), then from the search folders.
        sealed class LoadedFirstResolver : Cecil.DefaultAssemblyResolver
        {
            readonly Dictionary<string, Cecil.AssemblyDefinition> loaded = new(StringComparer.OrdinalIgnoreCase);

            public override Cecil.AssemblyDefinition Resolve(Cecil.AssemblyNameReference name, Cecil.ReaderParameters parameters)
            {
                if (loaded.TryGetValue(name.Name, out var cached)) return cached;
                foreach (var a in AssemblyLoadContext.Default.Assemblies)
                    if (string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase) && a.Location is { Length: > 0 } location && File.Exists(location))
                        return loaded[name.Name] = Cecil.AssemblyDefinition.ReadAssembly(location, new Cecil.ReaderParameters { AssemblyResolver = this });
                return base.Resolve(name, parameters);
            }

            protected override void Dispose(bool disposing)
            {
                foreach (var a in loaded.Values) a.Dispose();
                loaded.Clear();
                base.Dispose(disposing);
            }
        }

        static (byte[] Image, byte[]? Symbols, List<string> Added) Complete(byte[] image, byte[]? symbols, string path,
            Dictionary<string, (string Message, HashSet<string> Methods)> missing)
        {
            var resolver = new LoadedFirstResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(path)!);
            foreach (var dir in AssemblyPolicy.ReferenceDirectories) resolver.AddSearchDirectory(dir);
            using var owned = resolver;
            Cecil.ModuleDefinition module;
            bool withSymbols = symbols != null;
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
            using var ownedModule = module;
            var added = new List<string>();
            var exceptionCtor = module.ImportReference(typeof(TypeLoadException).GetConstructor([typeof(string)])!);
            foreach (var (typeName, (message, methods)) in missing)
            {
                var type = module.GetType(typeName.Replace('+', '/'));
                if (type == null) continue;
                foreach (var (interfaceType, method) in InterfaceMethods(type))
                {
                    if (!methods.Contains(method.Name) || method.HasGenericParameters) continue;
                    if (Implemented(type, interfaceType, method)) continue;
                    var reference = new Cecil.MethodReference(method.Name, method.ReturnType, interfaceType) { HasThis = true };
                    foreach (var p in method.Parameters) reference.Parameters.Add(new Cecil.ParameterDefinition(p.ParameterType));
                    var stub = new Cecil.MethodDefinition($"{interfaceType.FullName}.{method.Name}",
                        Cecil.MethodAttributes.Private | Cecil.MethodAttributes.Final | Cecil.MethodAttributes.Virtual | Cecil.MethodAttributes.HideBySig | Cecil.MethodAttributes.NewSlot,
                        module.ImportReference(Substitute(method.ReturnType, interfaceType)));
                    foreach (var p in method.Parameters)
                        stub.Parameters.Add(new Cecil.ParameterDefinition(p.Name, p.Attributes, module.ImportReference(Substitute(p.ParameterType, interfaceType))));
                    stub.Overrides.Add(module.ImportReference(reference));
                    var il = stub.Body.GetILProcessor();
                    il.Emit(Cecil.Cil.OpCodes.Ldstr, message);
                    il.Emit(Cecil.Cil.OpCodes.Newobj, exceptionCtor);
                    il.Emit(Cecil.Cil.OpCodes.Throw);
                    type.Methods.Add(stub);
                    added.Add($"{type.FullName} gets {interfaceType.FullName}.{method.Name}");
                }
            }
            using var output = new MemoryStream();
            using var symbolOutput = new MemoryStream();
            module.Write(output, new Cecil.WriterParameters { WriteSymbols = withSymbols, SymbolStream = withSymbols ? symbolOutput : null, SymbolWriterProvider = withSymbols ? new Cecil.Cil.PortablePdbWriterProvider() : null });
            return (output.ToArray(), withSymbols ? symbolOutput.ToArray() : null, added);
        }

        /// Every interface the type lists, directly, through its base classes or through other
        /// interfaces, with each of its methods.
        static IEnumerable<(Cecil.TypeReference Interface, Cecil.MethodDefinition Method)> InterfaceMethods(Cecil.TypeDefinition type)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<Cecil.TypeReference>();
            for (var t = type; t != null; t = t.BaseType?.Resolve())
                foreach (var i in t.Interfaces) pending.Push(i.InterfaceType);
            while (pending.Count > 0)
            {
                var reference = pending.Pop();
                if (!seen.Add(reference.FullName)) continue;
                var definition = reference.Resolve();
                if (definition == null) continue;
                foreach (var i in definition.Interfaces) pending.Push(i.InterfaceType);
                foreach (var m in definition.Methods)
                    if (m.IsAbstract && !m.IsStatic) yield return (reference, m);
            }
        }

        /// A public virtual method of the same name and signature in the type or a base class,
        /// or an explicit implementation of this method.
        static bool Implemented(Cecil.TypeDefinition type, Cecil.TypeReference interfaceType, Cecil.MethodDefinition method)
        {
            var signature = Signature(method, interfaceType);
            for (var t = type; t != null; t = t.BaseType?.Resolve())
                foreach (var m in t.Methods)
                {
                    if (m.Overrides.Any(o => o.Name == method.Name && o.DeclaringType.FullName == interfaceType.FullName)) return true;
                    if (m.Name == method.Name && m.IsVirtual && m.IsPublic && Signature(m, null) == signature) return true;
                }
            return false;
        }

        static string Signature(Cecil.MethodDefinition m, Cecil.TypeReference? interfaceType)
            => Substitute(m.ReturnType, interfaceType).FullName + "(" + string.Join(",", m.Parameters.Select(p => Substitute(p.ParameterType, interfaceType).FullName)) + ")";

        /// An interface method's type with the interface's generic arguments put in.
        static Cecil.TypeReference Substitute(Cecil.TypeReference type, Cecil.TypeReference? interfaceType)
        {
            if (interfaceType is not Cecil.GenericInstanceType instance) return type;
            return type switch
            {
                Cecil.GenericParameter { Type: Cecil.GenericParameterType.Type } g => instance.GenericArguments[g.Position],
                Cecil.ByReferenceType r => new Cecil.ByReferenceType(Substitute(r.ElementType, interfaceType)),
                Cecil.ArrayType a => new Cecil.ArrayType(Substitute(a.ElementType, interfaceType), a.Rank),
                Cecil.GenericInstanceType gi => Instance(gi, interfaceType),
                _ => type,
            };
        }

        static Cecil.GenericInstanceType Instance(Cecil.GenericInstanceType gi, Cecil.TypeReference interfaceType)
        {
            var result = new Cecil.GenericInstanceType(gi.ElementType);
            foreach (var a in gi.GenericArguments) result.GenericArguments.Add(Substitute(a, interfaceType));
            return result;
        }
    }
}
