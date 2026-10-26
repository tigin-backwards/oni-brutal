using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D33. Harmony's MonoMod builds a patched method's replacement from the original's
    /// IL read with Cecil, and turns each member the IL names back into reflection's
    /// (ReflectionHelper._ResolveReflection), through a cache keyed by the member's name and the
    /// assembly of its declaring type. For a member of a generic type over a mod's type
    /// (IEnumerable<IOptionsEntry>.GetEnumerator) that assembly is the generic type's, mscorlib:
    /// the type arguments' assemblies are left out of the key. Mods that each merge their own copy
    /// of PLib have same-named types in different assemblies, and a mod's patch of its copy's
    /// method was given the member resolved for another copy before it. Advanced Mopping by Type and
    /// Ultimate Power Tweaks each patch their PLib's OptionsDialog.FillModOptions: the second
    /// replacement enumerated its own ICollection<IOptionsEntry> through the other copy's
    /// IEnumerable<IOptionsEntry>, CoreCLR's interface dispatch found no implementation
    /// (EntryPointNotFoundException), and the mod's options dialog did not open (issue #14). Mono's
    /// dispatch falls back to the instantiation the object implements for IEnumerable<T> and the
    /// other generic interfaces arrays implement, so the dialog opens in the original player.
    ///
    /// The host's transpiler of _ResolveReflection adds the assembly (and module) of every type a
    /// member's declaring type, or a type specification's element, is built from to the key, when
    /// a generic instantiation is among them; other keys stay as they were.
    internal static class GenericMemberScopes
    {
        static bool installed;
        static FieldInfo cacheField = null!;
        static Type memberReference = null!, typeReference = null!, typeSpecification = null!, genericInstance = null!,
            genericParameter = null!, assemblyNameReference = null!, moduleDefinition = null!;
        static Func<object, object?> declaringType = null!, elementType = null!, genericArguments = null!, scope = null!, module = null!,
            assembly = null!, assemblyName = null!, moduleName = null!;
        static Func<object, string> hashedName = null!;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var helper = AccessTools.TypeByName("MonoMod.Utils.ReflectionHelper") ?? throw new TypeLoadException("MonoMod.Utils.ReflectionHelper");
            var resolve = AccessTools.Method(helper, "_ResolveReflection") ?? throw new MissingMethodException(helper.FullName, "_ResolveReflection");
            cacheField = AccessTools.Field(helper, "ResolveReflectionCache") ?? throw new MissingFieldException(helper.FullName, "ResolveReflectionCache");
            var cecil = resolve.GetParameters()[0].ParameterType.Assembly;
            Type Cecil(string name) => cecil.GetType("Mono.Cecil." + name, throwOnError: true)!;
            memberReference = Cecil("MemberReference");
            typeReference = Cecil("TypeReference");
            typeSpecification = Cecil("TypeSpecification");
            genericInstance = Cecil("IGenericInstance");
            genericParameter = Cecil("GenericParameter");
            assemblyNameReference = Cecil("AssemblyNameReference");
            moduleDefinition = Cecil("ModuleDefinition");
            Func<object, object?> Property(Type type, string name) => Getter(type, type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetMethod
                ?? throw new MissingMemberException(type.FullName, name));
            declaringType = Property(memberReference, "DeclaringType");
            elementType = Property(typeSpecification, "ElementType");
            genericArguments = Property(genericInstance, "GenericArguments");
            scope = Property(typeReference, "Scope");
            module = Property(memberReference, "Module");
            assembly = Property(moduleDefinition, "Assembly");
            assemblyName = Property(Cecil("AssemblyDefinition"), "Name");
            moduleName = Property(moduleDefinition, "Name");
            // MonoMod's own name of an assembly reference: its full name, and the runtime hash of
            // the loaded assembly it was read from, if any (two loaded assemblies can share a name).
            var hashed = AccessTools.Method(helper, "GetRuntimeHashedFullName", [assemblyNameReference])
                ?? throw new MissingMethodException(helper.FullName, "GetRuntimeHashedFullName");
            var named = Getter(assemblyNameReference, hashed);
            hashedName = reference => (string)named(reference)!;
            new Harmony("OniMoono.Host.GenericMemberScopes").Patch(resolve, transpiler: new HarmonyMethod(typeof(GenericMemberScopes), nameof(Transpiler)));
        }

        /// A delegate calling `method` (an instance getter of `type`, or a static method taking one)
        /// on an object of the internal Cecil types.
        static Func<object, object?> Getter(Type type, MethodInfo method)
        {
            var stub = new DynamicMethod(method.Name, typeof(object), [typeof(object)], typeof(GenericMemberScopes).Module, skipVisibility: true);
            var il = stub.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, type);
            il.Emit(method.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method);
            if (method.ReturnType.IsValueType) il.Emit(OpCodes.Box, method.ReturnType);
            il.Emit(OpCodes.Ret);
            return stub.CreateDelegate<Func<object, object?>>();
        }

        /// Before the cache is first read (the lock on it), the key gets the scopes Scoped adds. The
        /// key is the string local handed to the cache's TryGetValue.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int first = code.FindIndex(c => c.LoadsField(cacheField));
            int lookup = code.FindIndex(c => c.opcode == OpCodes.Callvirt && c.operand is MethodInfo { Name: "TryGetValue" } m && m.DeclaringType == cacheField.FieldType);
            if (first < 0 || lookup < first + 3) throw new InvalidOperationException("ReflectionHelper._ResolveReflection: no cache lookup found");
            // ldsfld cache; ldloc key; ldloca value; callvirt TryGetValue
            var key = code[lookup - 2];
            if (!key.IsLdloc() || (code[lookup - 1].opcode != OpCodes.Ldloca_S && code[lookup - 1].opcode != OpCodes.Ldloca))
                throw new InvalidOperationException("ReflectionHelper._ResolveReflection: no cache key found");
            int local = key.LocalIndex();
            var load = CodeInstruction.LoadLocal(local);
            load.MoveLabelsFrom(code[first]);
            code.InsertRange(first, [load, new CodeInstruction(OpCodes.Ldarg_0), CodeInstruction.Call(typeof(GenericMemberScopes), nameof(Scoped)), CodeInstruction.StoreLocal(local)]);
            return code;
        }

        /// `key` with the scopes of the types `mref` is built from (a member's declaring type, a type
        /// specification's element, their type arguments), when a generic instantiation is among
        /// them. A generic instantiation itself is keyed by its arguments' scopes already.
        internal static string Scoped(string key, object? mref)
        {
            if (mref == null || genericInstance.IsInstanceOfType(mref)) return key;
            object? from = typeSpecification.IsInstanceOfType(mref) ? elementType(mref) : declaringType(mref);
            if (from == null) return key;
            var parts = new StringBuilder();
            bool generic = false;
            Add(from, parts, ref generic, 0);
            return generic ? key + parts.ToString() : key;
        }

        static void Add(object type, StringBuilder parts, ref bool generic, int depth)
        {
            if (depth > 64) return;
            if (genericParameter.IsInstanceOfType(type)) return;
            if (genericInstance.IsInstanceOfType(type))
            {
                generic = true;
                Add(elementType(type)!, parts, ref generic, depth + 1);
                foreach (var argument in (IEnumerable)genericArguments(type)!)
                    if (argument != null) Add(argument, parts, ref generic, depth + 1);
                return;
            }
            if (typeSpecification.IsInstanceOfType(type))
            {
                Add(elementType(type)!, parts, ref generic, depth + 1);
                return;
            }
            parts.Append(" | ").Append(ScopeName(type));
        }

        /// As MonoMod names a type's scope: the assembly reference, or the module's assembly and
        /// the module.
        static string ScopeName(object type)
        {
            var where = scope(type);
            if (where != null && assemblyNameReference.IsInstanceOfType(where)) return hashedName(where) + ", NOMODULE";
            var inModule = where != null && moduleDefinition.IsInstanceOfType(where) ? where : module(type);
            if (inModule == null) return "NOASSEMBLY, NOMODULE";
            var definition = assembly(inModule);
            var name = definition == null ? null : assemblyName(definition);
            return (name == null ? "NOASSEMBLY" : hashedName(name)) + ", " + moduleName(inModule);
        }
    }
}
