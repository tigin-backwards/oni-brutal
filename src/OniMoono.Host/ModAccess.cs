using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using HarmonyLib;
using Cecil = Mono.Cecil;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D11. Many mods are built against "publicized" copies of the game's
    /// assemblies and use the game's private members directly. Mono does not check member
    /// accessibility at run time; CoreCLR does, and throws FieldAccessException or
    /// MethodAccessException when such a mod's method is compiled (BlueprintsV2's tools reading
    /// DragTool's private fields, for example).
    ///
    /// The game loads mods' assemblies in KMod.DLLLoader.LoadDLLs with Assembly.LoadFrom; the host
    /// replaces that one call. A mod assembly that names a game member or type its code could not
    /// reach is given CoreCLR's IgnoresAccessChecksTo attribute for the game's assemblies and loaded
    /// from memory; the game's assemblies are untouched, so serialization and reflection over them
    /// stay as they were. The mod keeps behaving as one loaded from its file: its Location is its
    /// file's path (mods find their folder from it) and its folder is searched for the assemblies it
    /// references. Every other mod assembly loads as before.
    internal static class ModAccess
    {
        /// The game's assemblies whose non-public members mods use: its code, and the Harmony it
        /// ships (mods built against a publicized 0Harmony read its private fields, as the
        /// Multiplayer Mod's loader reads PatchClassProcessor.containerAttributes).
        static readonly string[] gameAssemblies = ["Assembly-CSharp", "Assembly-CSharp-firstpass", "0Harmony"];

        /// Rewritten mod assemblies and the files they were read from.
        static readonly ConcurrentDictionary<Assembly, string> locations = new();
        static readonly ConcurrentDictionary<string, Assembly> byPath = new(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> directories = new();

        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony("OniMoono.Host.ModAccess");
            var loadDlls = AccessTools.Method("KMod.DLLLoader:LoadDLLs")
                ?? throw new MissingMethodException("KMod.DLLLoader", "LoadDLLs");
            harmony.Patch(loadDlls, transpiler: new HarmonyMethod(typeof(ModAccess), nameof(Transpiler)));
            var location = AccessTools.PropertyGetter(Type.GetType("System.Reflection.RuntimeAssembly"), nameof(Assembly.Location))
                ?? throw new MissingMethodException("System.Reflection.RuntimeAssembly", "get_Location");
            harmony.Patch(location, postfix: new HarmonyMethod(typeof(ModAccess), nameof(LocationPostfix)));
            AssemblyLoadContext.Default.Resolving += Resolve;
        }

        /// `Assembly.LoadFrom(path)` in LoadDLLs becomes `ModAccess.LoadFrom(path)`.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var loadFrom = typeof(Assembly).GetMethod(nameof(Assembly.LoadFrom), [typeof(string)])!;
            var replacement = typeof(ModAccess).GetMethod(nameof(LoadFrom), BindingFlags.Static | BindingFlags.NonPublic)!;
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(loadFrom))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException($"ModAccess: expected one Assembly.LoadFrom in LoadDLLs, found {replaced}");
        }

        static void LocationPostfix(Assembly __instance, ref string __result)
        {
            if (__result.Length == 0 && locations.TryGetValue(__instance, out var path)) __result = path;
        }

        /// A mod's assembly, rewritten when it reaches into the game's non-public members, with its
        /// static readonly fields writable as Mono's are, with its references to nested types of the
        /// game's strings that the game no longer has bound as Mono binds them, with any class that
        /// misses a method of an interface it lists still loadable, with a patched method's stack
        /// frame reporting its owner type, with its string-keyed concurrent dictionaries in Mono's
        /// order, with a stack overflow in a method that can recurse thrown as an exception, with
        /// its conversions of floats to integers giving Mono's values, and with its directory
        /// listings in Mono's order, as on Mono.
        internal static Assembly LoadFrom(string path)
        {
            path = Path.GetFullPath(path);
            if (byPath.TryGetValue(path, out var loaded)) return loaded;
            if (AlreadyLoaded(path) is { } existing) return existing;
            var reason = Unreachable(path);
            byte[]? image, symbols = null;
            if (reason != null)
            {
                image = AddIgnoresAccessChecks(path, out symbols);
                // Its static readonly fields stay writable, as the game's are (MonoFieldSemantics).
                MonoFieldSemantics.Adjust(image, path);
                EngineLog.Engine($"mod access: {Path.GetFileName(path)} uses the game's non-public {reason}; loaded with access checks to the game's assemblies off");
            }
            else
            {
                // Mono lets reflection write a mod's static readonly fields too: the game's
                // LocString.CreateLocStringKeys writes back every LocString field of a mod's strings
                // class (PLib's PLocalization.Register), which mods often declare static readonly.
                image = MonoFieldSemantics.Adjust(path);
                var pdb = Path.ChangeExtension(path, ".pdb");
                if (File.Exists(pdb)) symbols = File.ReadAllBytes(pdb);
            }
            // A nested type of the game's strings that the game no longer has is bound as Mono binds
            // it (MonoNestedTypeRefs).
            if (MonoNestedTypeRefs.Adjust(image ?? File.ReadAllBytes(path), ref symbols, path) is { } bound) image = bound;
            // A class missing a method of a game interface it lists loads, as on Mono
            // (MissingInterfaceMethods).
            if (MissingInterfaceMethods.Adjust(image ?? File.ReadAllBytes(path), ref symbols, path) is { } completed) image = completed;
            // A frame of a patched method reports its owner type, as on Mono (MonoStackFrames).
            if (MonoStackFrames.Adjust(image ?? File.ReadAllBytes(path), ref symbols, path) is { } rewritten) image = rewritten;
            // Its string-keyed concurrent dictionaries enumerate in Mono's order (MonoConcurrentDictionaries).
            if (MonoConcurrentDictionaries.Adjust(image ?? File.ReadAllBytes(path), ref symbols, path) is { } ordered) image = ordered;
            // A stack overflow in a method that can recurse throws, as on Mono (MonoStackOverflow).
            if (MonoStackOverflow.Adjust(image ?? File.ReadAllBytes(path), ref symbols, path) is { } probed) image = probed;
            // Its conversions of floats to integers give Mono's values out of range (MonoConversions),
            // and its directory listings come in Mono's order (MonoDirectoryListing, in that pass).
            if (MonoConversions.Adjust(image ?? File.ReadAllBytes(path), ref symbols, path) is { } converted) image = converted;
            if (image == null) return Assembly.LoadFrom(path);
            Assembly assembly;
            try
            {
                assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(image), symbols == null ? null : new MemoryStream(symbols));
            }
            catch (BadImageFormatException) when (symbols != null)
            {
                // Symbols the runtime cannot read (a Windows PDB): the assembly loads without them.
                assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(image));
            }
            locations[assembly] = path;
            byPath[path] = assembly;
            lock (directories)
                if (!directories.Contains(Path.GetDirectoryName(path)!)) directories.Add(Path.GetDirectoryName(path)!);
            return assembly;
        }

        /// Mono loads a file whose assembly has no strong name as the assembly already loaded with
        /// its simple name, whatever their versions (mono_domain_assembly_search: names compared
        /// ignoring version, key and case); CoreCLR refuses a second version, and the game then
        /// drops the mod (a mod shipping its own copy of a library another mod loaded first).
        static Assembly? AlreadyLoaded(string path)
        {
            AssemblyName name;
            try
            {
                name = AssemblyName.GetAssemblyName(path);
            }
            catch (BadImageFormatException)
            {
                return null;
            }
            if (name.GetPublicKeyToken() is { Length: > 0 } || Loaded(name.Name) is not { } assembly) return null;
            if (assembly.GetName().Version != name.Version)
                EngineLog.Engine($"mod access: {path} is {name.Name} {name.Version}; {assembly.GetName().Version}, already loaded, stands in for it (as in Mono)");
            return assembly;
        }

        /// The assembly loaded in the default context under this simple name (compared ignoring
        /// case, as Mono compares), or null.
        static Assembly? Loaded(string? simpleName)
        {
            foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
                if (!assembly.IsDynamic && string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)) return assembly;
            return null;
        }

        /// A reference CoreCLR could not bind: one to an assembly without a strong name binds, as in
        /// Mono, to the assembly loaded under its simple name whatever their versions (a mod built
        /// against PLib 4.22 when another mod's older PLib is the one loaded); then the folders of
        /// rewritten mods are searched as Assembly.LoadFrom's would be.
        static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            if (name.GetPublicKeyToken() is not { Length: > 0 } && Loaded(name.Name) is { } loaded)
            {
                if (loaded.GetName().Version != name.Version)
                    EngineLog.Engine($"mod access: a reference to {name.Name} {name.Version} binds to {loaded.GetName().Version}, already loaded (as in Mono)");
                return loaded;
            }
            string[] dirs;
            lock (directories) dirs = directories.ToArray();
            foreach (var dir in dirs)
            {
                var candidate = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(candidate)) return LoadFrom(candidate);
            }
            return null;
        }

        /// The first game member or type the assembly references that is not public, or null.
        static string? Unreachable(string path)
        {
            try
            {
                using var pe = new PEReader(File.OpenRead(path));
                if (!pe.HasMetadata) return null;
                var md = pe.GetMetadataReader();
                foreach (var handle in md.TypeReferences)
                    if (GameType(md, handle) is { } type && !type.IsVisible) return type.FullName;
                foreach (var handle in md.MemberReferences)
                {
                    var member = md.GetMemberReference(handle);
                    var parent = member.Parent.Kind switch
                    {
                        HandleKind.TypeReference => GameType(md, (TypeReferenceHandle)member.Parent),
                        HandleKind.TypeSpecification => GameType(md, GenericDefinition(md, (TypeSpecificationHandle)member.Parent)),
                        _ => null,
                    };
                    if (parent == null) continue;
                    var name = md.GetString(member.Name);
                    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                    // A method is matched by its parameter count (overloads of one name can differ in access).
                    int parameters = member.GetKind() == MemberReferenceKind.Method ? ParameterCount(md, member.Signature) : -1;
                    foreach (var candidate in parent.GetMember(name, all))
                    {
                        if (parameters >= 0 && candidate is MethodBase m && m.GetParameters().Length != parameters) continue;
                        if (!IsReachable(candidate)) return $"{parent.FullName}.{name}";
                    }
                }
            }
            catch (Exception e) when (e is BadImageFormatException or IOException or UnauthorizedAccessException)
            {
            }
            return null;
        }

        static int ParameterCount(MetadataReader md, BlobHandle signature)
        {
            var blob = md.GetBlobReader(signature);
            var header = blob.ReadSignatureHeader();
            if (header.IsGeneric) blob.ReadCompressedInteger();
            return blob.ReadCompressedInteger();
        }

        /// Public members, and protected ones (a mod's subclass reaches them as any subclass does).
        static bool IsReachable(MemberInfo member) => member switch
        {
            FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
            MethodBase m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
            _ => true,
        };

        /// A type reference into a loaded game assembly, or null.
        static Type? GameType(MetadataReader md, TypeReferenceHandle handle)
        {
            if (handle.IsNil) return null;
            var t = md.GetTypeReference(handle);
            var name = md.GetString(t.Name);
            if (t.ResolutionScope.Kind == HandleKind.TypeReference)
                return GameType(md, (TypeReferenceHandle)t.ResolutionScope)?.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
            if (t.ResolutionScope.Kind != HandleKind.AssemblyReference) return null;
            var assemblyName = md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope).Name);
            if (Array.IndexOf(gameAssemblies, assemblyName) < 0) return null;
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            var ns = md.GetString(t.Namespace);
            return assembly?.GetType(ns.Length == 0 ? name : ns + "." + name, throwOnError: false);
        }

        /// The generic type a member of a generic instantiation belongs to (GENERICINST signatures).
        static TypeReferenceHandle GenericDefinition(MetadataReader md, TypeSpecificationHandle handle)
        {
            var blob = md.GetBlobReader(md.GetTypeSpecification(handle).Signature);
            if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return default;
            blob.ReadByte(); // CLASS or VALUETYPE
            var definition = blob.ReadTypeHandle();
            return definition.Kind == HandleKind.TypeReference ? (TypeReferenceHandle)definition : default;
        }

        /// Writing an assembly can need its references (attribute arguments of their enum types):
        /// the mod's folder, the game's assemblies and our engine facades.
        static Cecil.DefaultAssemblyResolver Resolver(string path)
        {
            var resolver = new Cecil.DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(path)!);
            foreach (var dir in AssemblyPolicy.ReferenceDirectories) resolver.AddSearchDirectory(dir);
            return resolver;
        }

        /// The assembly with [IgnoresAccessChecksTo] for each game assembly (the attribute type
        /// defined in it when it has none; CoreCLR recognizes it by name), and its symbols.
        static byte[] AddIgnoresAccessChecks(string path, out byte[]? symbols)
        {
            var pdb = Path.ChangeExtension(path, ".pdb");
            bool withSymbols = File.Exists(pdb);
            using var resolver = Resolver(path);
            Cecil.ModuleDefinition module;
            try
            {
                module = Cecil.ModuleDefinition.ReadModule(path, new Cecil.ReaderParameters { ReadSymbols = withSymbols, InMemory = true, AssemblyResolver = resolver });
            }
            catch (Exception e) when (withSymbols && e is Cecil.Cil.SymbolsNotMatchingException or Cecil.Cil.SymbolsNotFoundException or BadImageFormatException or InvalidOperationException)
            {
                // Symbols the reader cannot use (a Windows PDB, or stale): the assembly without them.
                withSymbols = false;
                module = Cecil.ModuleDefinition.ReadModule(path, new Cecil.ReaderParameters { InMemory = true, AssemblyResolver = resolver });
            }
            using var owned = module;
            const string ns = "System.Runtime.CompilerServices", name = "IgnoresAccessChecksToAttribute";
            var attribute = module.Types.FirstOrDefault(t => t.Namespace == ns && t.Name == name);
            if (attribute == null)
            {
                attribute = new Cecil.TypeDefinition(ns, name, Mono.Cecil.TypeAttributes.NotPublic | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.BeforeFieldInit,
                    module.ImportReference(typeof(Attribute)));
                var baseCtor = module.ImportReference(typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!);
                var ctor = new Cecil.MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName,
                    module.TypeSystem.Void);
                ctor.Parameters.Add(new Cecil.ParameterDefinition("assemblyName", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.String));
                var il = ctor.Body.GetILProcessor();
                il.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
                il.Emit(Mono.Cecil.Cil.OpCodes.Call, baseCtor);
                il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
                attribute.Methods.Add(ctor);
                module.Types.Add(attribute);
            }
            var constructor = attribute.Methods.First(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
            var existing = new HashSet<string>(module.Assembly.CustomAttributes
                .Where(a => a.AttributeType.FullName == ns + "." + name && a.ConstructorArguments.Count == 1)
                .Select(a => (string)a.ConstructorArguments[0].Value), StringComparer.Ordinal);
            foreach (var game in gameAssemblies)
            {
                if (existing.Contains(game)) continue;
                var applied = new Cecil.CustomAttribute(constructor);
                applied.ConstructorArguments.Add(new Cecil.CustomAttributeArgument(module.TypeSystem.String, game));
                module.Assembly.CustomAttributes.Add(applied);
            }
            using var image = new MemoryStream();
            using var symbolStream = new MemoryStream();
            module.Write(image, new Cecil.WriterParameters { WriteSymbols = withSymbols, SymbolStream = withSymbols ? symbolStream : null, SymbolWriterProvider = withSymbols ? new Cecil.Cil.PortablePdbWriterProvider() : null });
            symbols = withSymbols ? symbolStream.ToArray() : null;
            return image.ToArray();
        }
    }
}
