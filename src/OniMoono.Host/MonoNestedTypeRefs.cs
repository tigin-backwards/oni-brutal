using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using OniMoono.Engine;
using Cecil = Mono.Cecil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D32. A mod built for an older game can name a nested type of a game class that
    /// the game no longer has (STRINGS.ROOMS.CRITERIA.DECOR20). Mono resolves such a TypeRef in
    /// mono_class_from_typeref_checked (metadata/class.c, MONO_RESOLUTION_SCOPE_TYPEREF): when the
    /// enclosing class's nested types are already loaded (nested_classes_inited), it scans that
    /// list for the name and, finding none, leaves its loop variable on the list's last class,
    /// logs "TypeRef ResolutionScope not yet handled" and returns that class with no error; when
    /// they are not loaded yet, it scans the metadata, returns nothing, and the reference fails
    /// with a TypeLoadException, as on CoreCLR. A class's nested types are loaded by enumerating
    /// them (Type.GetNestedTypes and GetNestedType, a nested type name parsed by Type.GetType), so
    /// which result a mod gets depends on what ran before its code: in general not known when the
    /// mod loads. The game's strings are the exception: Global.Awake calls
    /// Localization.RegisterForTranslation, whose LocString.CreateLocStringKeys enumerates the
    /// nested types (public and not) of every class of the STRINGS namespace in Assembly-CSharp,
    /// recursively, before the mods load, in every language. So a mod's reference to a missing
    /// nested type of a class in that tree is bound here, as it loads, to the class Mono binds:
    /// the enclosing class's last nested type in the game's NestedClass table (U&F-Translate Fix's
    /// DECOR20 becomes POWERBUILDING, as in the original). Every other missing nested type is left
    /// to fail as before (TUNING.DUPLICANTSTATS.MOVEMENT: the original fails it too).
    public static class MonoNestedTypeRefs
    {
        /// The game's Assembly-CSharp metadata, read once (null when the file is not there).
        static readonly Lazy<MetadataReader?> Game = new(() =>
        {
            var path = Path.Combine(AssemblyPolicy.ReferenceDirectories[0], "Assembly-CSharp.dll");
            if (!File.Exists(path)) return null;
            var pe = new PEReader(File.OpenRead(path), PEStreamOptions.PrefetchMetadata);
            return pe.HasMetadata ? pe.GetMetadataReader() : null;
        });

        /// The image with its references to missing nested types of the game's strings bound as
        /// Mono binds them, or null when it has none. `symbols` (portable PDB bytes or null) are
        /// rewritten to match.
        internal static byte[]? Adjust(byte[] image, ref byte[]? symbols, string path)
        {
            Dictionary<int, (string Namespace, string Name, string Missing)> bindings;
            try
            {
                using var pe = new PEReader(new MemoryStream(image));
                if (!pe.HasMetadata) return null;
                var mod = pe.GetMetadataReader();
                if (!NamesStrings(mod) || Game.Value is not { } game) return null;
                bindings = Bindings(mod, game);
            }
            catch (BadImageFormatException)
            {
                // Not an assembly: the load reports it.
                return null;
            }
            if (bindings.Count == 0) return null;
            try
            {
                return Rewrite(image, ref symbols, path, bindings);
            }
            catch (Exception e) when (e is Cecil.AssemblyResolutionException or BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                // The mod loads as it is: the references fail as CoreCLR fails them.
                EngineLog.Engine($"mod nested types: {Path.GetFileName(path)} not rewritten ({e.GetType().Name}: {e.Message}); it loads unchanged");
                return null;
            }
        }

        /// Whether the mod has a nested TypeRef into a STRINGS class of Assembly-CSharp (from its
        /// metadata alone; the game's is read only then).
        static bool NamesStrings(MetadataReader mod)
        {
            foreach (var handle in mod.TypeReferences)
            {
                var reference = mod.GetTypeReference(handle);
                if (reference.ResolutionScope.Kind == HandleKind.TypeReference && Root(mod, reference) is var (scope, root)
                    && mod.StringComparer.Equals(root.Namespace, "STRINGS")
                    && mod.StringComparer.Equals(mod.GetAssemblyReference(scope).Name, "Assembly-CSharp")) return true;
            }
            return false;
        }

        /// A nested TypeRef's outermost enclosing TypeRef and its assembly, or null when the chain
        /// does not end in an assembly reference.
        static (AssemblyReferenceHandle, TypeReference)? Root(MetadataReader mod, TypeReference reference)
        {
            for (int depth = 0; reference.ResolutionScope.Kind == HandleKind.TypeReference; depth++)
            {
                if (depth > 64) return null;
                reference = mod.GetTypeReference((TypeReferenceHandle)reference.ResolutionScope);
            }
            return reference.ResolutionScope.Kind == HandleKind.AssemblyReference ? ((AssemblyReferenceHandle)reference.ResolutionScope, reference) : null;
        }

        /// The mod's TypeRef rows (by row number) to rename, each with the namespace and name of the
        /// game's nested type Mono binds it to and the name it had: every nested TypeRef into a class
        /// of the STRINGS tree of `game` (Assembly-CSharp) whose name that class lacks, when the class
        /// has nested types. Rows within a reference bound this way are looked up in the class it
        /// is bound to, as Mono looks them up.
        internal static Dictionary<int, (string Namespace, string Name, string Missing)> Bindings(MetadataReader mod, MetadataReader game)
        {
            var bindings = new Dictionary<int, (string, string, string)>();
            var resolved = new Dictionary<TypeReferenceHandle, TypeDefinitionHandle?>();
            Dictionary<string, TypeDefinitionHandle>? roots = null;

            // The game class a TypeRef binds to on Mono, or null when it fails or is not in the tree.
            TypeDefinitionHandle? Resolve(TypeReferenceHandle handle, int depth)
            {
                if (resolved.TryGetValue(handle, out var known)) return known;
                resolved[handle] = null;
                var reference = mod.GetTypeReference(handle);
                TypeDefinitionHandle? result = null;
                if (reference.ResolutionScope.Kind == HandleKind.AssemblyReference)
                {
                    if (mod.StringComparer.Equals(reference.Namespace, "STRINGS")
                        && mod.StringComparer.Equals(mod.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name, "Assembly-CSharp"))
                    {
                        roots ??= StringsRoots(game);
                        if (roots.TryGetValue(mod.GetString(reference.Name), out var root)) result = root;
                    }
                }
                else if (reference.ResolutionScope.Kind == HandleKind.TypeReference && depth < 64
                    && Resolve((TypeReferenceHandle)reference.ResolutionScope, depth + 1) is { } enclosing)
                {
                    var name = mod.GetString(reference.Name);
                    var nested = game.GetTypeDefinition(enclosing).GetNestedTypes();
                    foreach (var candidate in nested)
                        if (game.StringComparer.Equals(game.GetTypeDefinition(candidate).Name, name)) { result = candidate; break; }
                    if (result == null && nested.Length > 0)
                    {
                        // Mono's scan of the loaded list ends on its last class: the NestedClass
                        // table's order (mono_class_setup_nested_types keeps it).
                        var last = nested[^1];
                        var definition = game.GetTypeDefinition(last);
                        bindings[MetadataTokens.GetRowNumber(handle)] = (game.GetString(definition.Namespace), game.GetString(definition.Name), name);
                        result = last;
                    }
                }
                return resolved[handle] = result;
            }

            foreach (var handle in mod.TypeReferences)
                if (mod.GetTypeReference(handle).ResolutionScope.Kind == HandleKind.TypeReference) Resolve(handle, 0);
            return bindings;
        }

        /// The top-level classes of the STRINGS namespace, by name: those
        /// Localization.CollectLocStringTreeRoots gives CreateLocStringKeys (`IsClass`).
        static Dictionary<string, TypeDefinitionHandle> StringsRoots(MetadataReader game)
        {
            var roots = new Dictionary<string, TypeDefinitionHandle>(StringComparer.Ordinal);
            foreach (var handle in game.TypeDefinitions)
            {
                var type = game.GetTypeDefinition(handle);
                if (!type.GetDeclaringType().IsNil || !game.StringComparer.Equals(type.Namespace, "STRINGS")
                    || (type.Attributes & TypeAttributes.Interface) != 0 || IsValueType(game, type.BaseType)) continue;
                roots.TryAdd(game.GetString(type.Name), handle);
            }
            return roots;
        }

        static bool IsValueType(MetadataReader game, EntityHandle baseType)
        {
            if (baseType.Kind != HandleKind.TypeReference) return false;
            var reference = game.GetTypeReference((TypeReferenceHandle)baseType);
            return game.StringComparer.Equals(reference.Namespace, "System")
                && (game.StringComparer.Equals(reference.Name, "ValueType") || game.StringComparer.Equals(reference.Name, "Enum"));
        }

        static byte[]? Rewrite(byte[] image, ref byte[]? symbols, string path, Dictionary<int, (string Namespace, string Name, string Missing)> bindings)
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
            var renamed = module.GetTypeReferences()
                .Where(r => r.MetadataToken.TokenType == Cecil.TokenType.TypeRef && bindings.ContainsKey((int)r.MetadataToken.RID))
                .Select(r => (Reference: r, Target: bindings[(int)r.MetadataToken.RID]))
                .ToList();
            if (renamed.Count == 0) return null;
            // Named as the mod names them, before any is renamed.
            var bound = renamed.Select(r => $"{r.Reference.FullName} as {r.Target.Name}").ToList();
            foreach (var (reference, target) in renamed)
            {
                reference.Namespace = target.Namespace;
                reference.Name = target.Name;
            }
            using var output = new MemoryStream();
            using var symbolOutput = new MemoryStream();
            module.Write(output, new Cecil.WriterParameters { WriteSymbols = withSymbols, SymbolStream = withSymbols ? symbolOutput : null, SymbolWriterProvider = withSymbols ? new Cecil.Cil.PortablePdbWriterProvider() : null });
            symbols = withSymbols ? symbolOutput.ToArray() : null;
            EngineLog.Engine($"mod nested types: {Path.GetFileName(path)}: names nested types the game no longer has, bound as Mono binds them (\"TypeRef ResolutionScope not yet handled\"): {string.Join(", ", bound)}");
            return output.ToArray();
        }
    }
}
