using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D1. `StateMachineSerializer` stores each state-machine parameter under its
    /// context type's `Type.FullName` and matches saved parameters by comparing those names. For
    /// the generic context types the name lists every type argument's assembly, which on this
    /// runtime is not the original player's (System.Private.CoreLib for mscorlib, the engine for
    /// UnityEngine modules), so no saved parameter matched and all loaded at their defaults: in a
    /// large colony, suits, doors and food reachability broke and the Duplicants starved. The
    /// host has the serializer use the names the original player writes (MonoTypeNames), so
    /// saves from either player load fully in both. This and ModVersionStamp are the host's only
    /// changes to game code.
    internal static class StateMachineSaveNames
    {
        static string managedDir = "", engineDir = "";

        public static void Install(string gameDataDir, string hostDir)
        {
            managedDir = Path.Combine(gameDataDir, "Managed");
            engineDir = Path.Combine(hostDir, "engine");
            var serializer = AccessTools.TypeByName("StateMachineSerializer")
                ?? throw new TypeLoadException("StateMachineSerializer");
            var fullName = typeof(Type).GetProperty(nameof(Type.FullName))!.GetGetMethod()!;
            var harmony = new Harmony("OniBrutal.Host.StateMachineSaveNames");
            var transpiler = new HarmonyMethod(typeof(StateMachineSaveNames), nameof(Transpiler));
            int patched = 0;
            foreach (var type in new[] { serializer }.Concat(serializer.GetNestedTypes(AccessTools.all)))
                foreach (var method in type.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters) continue;
                    if (!PatchProcessor.GetOriginalInstructions(method).Any(i => i.Calls(fullName))) continue;
                    harmony.Patch(method, transpiler: transpiler);
                    patched++;
                }
            if (patched == 0) throw new InvalidOperationException("StateMachineSerializer: no Type.FullName use found to patch.");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var fullName = typeof(Type).GetProperty(nameof(Type.FullName))!.GetGetMethod()!;
            var mono = typeof(MonoNames).GetMethod(nameof(MonoNames.FullName))!;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(fullName))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = mono;
                }
                yield return instruction;
            }
        }

        // ----- The original player's assembly of a type -----

        static Dictionary<string, string>? frameworkTypes, engineTypes;
        static readonly HashSet<Assembly> framework = new();

        internal static string AssemblyOf(Type type)
        {
            var assembly = type.Assembly;
            string name = type.FullName ?? type.Name;
            if (IsFramework(assembly))
            {
                frameworkTypes ??= DefinedTypes(MonoClassLibraries());
                return frameworkTypes.TryGetValue(name, out var mono) ? mono : MscorlibName;
            }
            if (assembly == typeof(UnityEngine.Object).Assembly)
            {
                engineTypes ??= ForwardedTypes(Directory.GetFiles(engineDir, "UnityEngine*.dll"));
                if (engineTypes.TryGetValue(name, out var module)) return module;
            }
            return assembly.FullName!;
        }

        const string MscorlibName = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

        static bool IsFramework(Assembly assembly)
        {
            if (framework.Contains(assembly)) return true;
            if (assembly == typeof(object).Assembly || AssemblyPolicy.IsFrameworkAssembly(assembly.GetName().Name!))
            {
                framework.Add(assembly);
                return true;
            }
            return false;
        }

        /// Mono's class libraries shipped with the game, mscorlib first (a type it defines wins).
        static IEnumerable<string> MonoClassLibraries()
        {
            var all = Directory.GetFiles(managedDir, "*.dll")
                .Where(p => Path.GetFileName(p) is var f && (f == "mscorlib.dll" || f.StartsWith("System", StringComparison.Ordinal) || f.StartsWith("Mono.", StringComparison.Ordinal)))
                .OrderBy(p => Path.GetFileName(p) == "mscorlib.dll" ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal);
            return all;
        }

        /// Public type definitions (nested types joined with '+') → the defining assembly's display name.
        static Dictionary<string, string> DefinedTypes(IEnumerable<string> paths)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                using var pe = new PEReader(File.OpenRead(path));
                if (!pe.HasMetadata) continue;
                var md = pe.GetMetadataReader();
                if (!md.IsAssembly) continue;
                string display = md.GetAssemblyDefinition().GetAssemblyName().FullName;
                foreach (var handle in md.TypeDefinitions)
                    map.TryAdd(TypeName(md, handle), display);
            }
            return map;
        }

        static string TypeName(MetadataReader md, TypeDefinitionHandle handle)
        {
            var t = md.GetTypeDefinition(handle);
            var declaring = t.GetDeclaringType();
            if (!declaring.IsNil) return TypeName(md, declaring) + "+" + md.GetString(t.Name);
            var ns = md.GetString(t.Namespace);
            return ns.Length > 0 ? ns + "." + md.GetString(t.Name) : md.GetString(t.Name);
        }

        /// Type forwarders of the engine facades → the facade (Unity module) display name.
        static Dictionary<string, string> ForwardedTypes(IEnumerable<string> paths)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                using var pe = new PEReader(File.OpenRead(path));
                if (!pe.HasMetadata) continue;
                var md = pe.GetMetadataReader();
                string display = md.GetAssemblyDefinition().GetAssemblyName().FullName;
                foreach (var handle in md.ExportedTypes)
                {
                    var e = md.GetExportedType(handle);
                    string name = md.GetString(e.Name);
                    if (e.Implementation.Kind == HandleKind.ExportedType)
                    {
                        // Nested forwarder: prefix the declaring exported type's name.
                        var parts = new List<string> { name };
                        var impl = e.Implementation;
                        while (impl.Kind == HandleKind.ExportedType)
                        {
                            var outer = md.GetExportedType((ExportedTypeHandle)impl);
                            parts.Insert(0, md.GetString(outer.Name));
                            if (outer.Implementation.Kind != HandleKind.ExportedType)
                            {
                                var ns = md.GetString(outer.Namespace);
                                if (ns.Length > 0) parts[0] = ns + "." + parts[0];
                            }
                            impl = outer.Implementation;
                        }
                        map.TryAdd(string.Join("+", parts), display);
                        continue;
                    }
                    var space = md.GetString(e.Namespace);
                    map.TryAdd(space.Length > 0 ? space + "." + name : name, display);
                }
            }
            return map;
        }
    }

    /// The call target the patched serializer uses; public so the patched IL may call it.
    public static class MonoNames
    {
        static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, string> names = new();

        /// Cached: a large colony saves hundreds of thousands of parameters of a few hundred types.
        public static string FullName(Type type) => names.GetOrAdd(type, t => MonoTypeNames.FullName(t, StateMachineSaveNames.AssemblyOf));
    }
}
