using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OniMoono.Engine;
using Cecil = Mono.Cecil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D51. Obfuscators nest types in the module's global type, <Module> (the
    /// NestedClass table names TypeDef 1 as their enclosing class). Mono loads them as any nested
    /// type (mono_class_create_from_typedef finds the enclosing class through the NestedClass
    /// table, and <Module> is a class like the others). CoreCLR's table of the classes an
    /// assembly declares leaves <Module> out, so a type nested in it, and every type nested in
    /// that one, fails: "Enclosing type(s) not found for type … in assembly …"
    /// (BadImageFormatException), and the game's GetTypes over the mod drops it (Abyssalite,
    /// Refrigerator). Such a mod is rewritten as it loads: the types nested in <Module> become
    /// top-level types (public ones public, the others internal), keeping their own nested types,
    /// and the members of <Module> they could reach as nested types (private and protected
    /// ones) become internal so they still can. The types' names lose their "<Module>+" prefix and
    /// their DeclaringType is null where Mono's is <Module>; obfuscated code does not look itself
    /// up by name.
    internal static class ModuleNestedTypes
    {
        internal static byte[]? Adjust(byte[] image, ref byte[]? symbols, string path)
        {
            if (!NestsInModule(image)) return null;
            try
            {
                return Rewrite(image, ref symbols, path);
            }
            catch (Exception e) when (e is Cecil.AssemblyResolutionException or BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                EngineLog.Engine($"mod global types: {Path.GetFileName(path)} not rewritten ({e.GetType().Name}: {e.Message}); it loads unchanged");
                return null;
            }
        }

        /// Whether a type of the image is nested in <Module>, read from its metadata alone.
        static bool NestsInModule(byte[] image)
        {
            try
            {
                using var pe = new PEReader(new MemoryStream(image, writable: false));
                if (!pe.HasMetadata) return false;
                var md = pe.GetMetadataReader();
                foreach (var handle in md.TypeDefinitions)
                {
                    var declaring = md.GetTypeDefinition(handle).GetDeclaringType();
                    if (!declaring.IsNil && System.Reflection.Metadata.Ecma335.MetadataTokens.GetRowNumber(declaring) == 1) return true;
                }
            }
            catch (BadImageFormatException)
            {
            }
            return false;
        }

        static byte[]? Rewrite(byte[] image, ref byte[]? symbols, string path)
        {
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
            var global = module.Types.FirstOrDefault(t => t.Name == "<Module>");
            if (global == null || !global.HasNestedTypes) return null;
            var nested = global.NestedTypes.ToList();
            foreach (var type in nested)
            {
                global.NestedTypes.Remove(type);
                var visibility = type.Attributes & Cecil.TypeAttributes.VisibilityMask;
                type.Attributes = type.Attributes & ~Cecil.TypeAttributes.VisibilityMask
                    | (visibility == Cecil.TypeAttributes.NestedPublic ? Cecil.TypeAttributes.Public : Cecil.TypeAttributes.NotPublic);
                module.Types.Add(type);
            }
            int opened = 0;
            foreach (var field in global.Fields)
                if (field.IsPrivate || field.IsFamily || field.IsFamilyAndAssembly)
                {
                    field.Attributes = field.Attributes & ~Cecil.FieldAttributes.FieldAccessMask | Cecil.FieldAttributes.Assembly;
                    opened++;
                }
            foreach (var method in global.Methods)
                if (method.IsPrivate || method.IsFamily || method.IsFamilyAndAssembly)
                {
                    method.Attributes = method.Attributes & ~Cecil.MethodAttributes.MemberAccessMask | Cecil.MethodAttributes.Assembly;
                    opened++;
                }
            using var output = new MemoryStream();
            using var symbolOutput = new MemoryStream();
            module.Write(output, new Cecil.WriterParameters { WriteSymbols = withSymbols, SymbolStream = withSymbols ? symbolOutput : null, SymbolWriterProvider = withSymbols ? new Cecil.Cil.PortablePdbWriterProvider() : null });
            symbols = withSymbols ? symbolOutput.ToArray() : null;
            EngineLog.Engine($"mod global types: {Path.GetFileName(path)}: {nested.Count} type(s) nested in <Module> made top-level ({opened} member(s) of <Module> made internal), as CoreCLR cannot load them nested there and Mono can");
            return output.ToArray();
        }
    }
}
