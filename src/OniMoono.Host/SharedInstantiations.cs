using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D38. CoreCLR shares a generic class's code among the instantiations whose
    /// reference type arguments differ (`Parameter<__Canon, …, float>.Set` for every state
    /// machine's `Parameter<float>.Set`); Mono's JIT shares it only when every argument is a
    /// reference type, and compiles an instantiation with a value type argument on its own
    /// (`mono_method_is_generic_sharable_full`: partial sharing is for AOT only). A Harmony
    /// replacement is built for the instantiation patched, with its types: run for another one,
    /// its casts fail (Critter Condo Save Fix patches the critter condo monitor's
    /// `Parameter<float>.Set`, and `Rottable.Instance`'s constructor threw InvalidCastException
    /// from it while a save loaded, issue #16).
    ///
    /// The shared code of such a method is detoured to a dispatcher instead: an object of the
    /// patched instantiation (or a class derived from it) runs its replacement, any other its own
    /// instantiation's, the replacement Harmony made when that one is patched too, else a copy of
    /// its own IL (as D34 makes one), made the first time it is called.
    internal static class SharedInstantiations
    {
        static bool installed;
        static readonly object gate = new();
        // Per shared code (its method handle): the instantiations' code; the dispatcher's fast path
        // reads its first patched instantiation's from `primaries`.
        static readonly Dictionary<IntPtr, Shared> byHandle = new();
        static Shared[] shared = [];
        static IntPtr[] primaries = [];
        static MethodInfo? methodDescriptor;
        static object locker = null!;
        [ThreadStatic] static Shared? creating;

        sealed class Shared
        {
            public int Index;
            public MethodBase Primary = null!;
            public Type Definition = null!;
            // The declaring instantiation → its replacement's code; the code by an object's type.
            public readonly Dictionary<Type, IntPtr> Code = new();
            public readonly ConcurrentDictionary<Type, IntPtr> ByType = new();
            // Every replacement made, kept alive: a caller may hold the code of one replaced since.
            public readonly List<MethodBase> Kept = new();
        }

        public static void Install()
        {
            if (installed) return;
            installed = true;
            methodDescriptor = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(DynamicMethod), "GetMethodDescriptor");
            locker = AccessTools.Field("HarmonyLib.PatchProcessor:locker")?.GetValue(null)
                ?? throw new MissingFieldException("HarmonyLib.PatchProcessor", "locker");
            var detour = AccessTools.Method("HarmonyLib.PatchTools:DetourMethod")
                ?? throw new MissingMethodException("HarmonyLib.PatchTools", "DetourMethod");
            new Harmony("OniMoono.Host.SharedInstantiations").Patch(detour,
                prefix: new HarmonyMethod(typeof(SharedInstantiations), nameof(Detouring)),
                finalizer: new HarmonyMethod(typeof(SharedInstantiations), nameof(Detoured)));
        }

        /// Whether `method` runs code CoreCLR shares with other instantiations and Mono does not:
        /// an instance method of a class instantiated over a value type and a type CoreCLR shares
        /// code over (a reference type, or a struct instantiated over one). A struct's method, a
        /// static or a generic method takes its instantiation as a hidden argument, which a
        /// dispatcher cannot read: they are left as they were.
        static bool Applies(MethodBase method)
        {
            if (method is DynamicMethod) return false;
            var arguments = new List<Type>();
            if (method.DeclaringType is { IsConstructedGenericType: true } type) arguments.AddRange(type.GetGenericArguments());
            if (method.IsGenericMethod) arguments.AddRange(method.GetGenericArguments());
            if (!arguments.Exists(a => a.IsValueType) || !arguments.Exists(Shares)) return false;
            if (!method.IsStatic && !method.IsGenericMethod && method.DeclaringType is { IsValueType: false, IsInterface: false }) return true;
            EngineLog.Engine($"shared instantiations: {method.DeclaringType}.{method.Name} shares its code with other instantiations, and its patches run for them too: it takes its instantiation as a hidden argument");
            return false;
        }

        static bool Shares(Type type) => !type.IsValueType || (type.IsGenericType && Array.Exists(type.GetGenericArguments(), Shares));

        /// Prefix of Harmony's PatchTools.DetourMethod (under Harmony's lock): the first patched
        /// instantiation of shared code detours it to a dispatcher; each instantiation's
        /// replacement (a patch, a rebuild, a copy) goes into the dispatcher's table.
        static bool Detouring(MethodBase method, ref MethodBase replacement)
        {
            if (!Applies(method)) return true;
            var handle = method.MethodHandle.Value;
            var code = CodeOf(replacement);
            Shared? state;
            bool first;
            lock (gate)
            {
                first = !byHandle.TryGetValue(handle, out state);
                if (first)
                {
                    state = new Shared { Index = shared.Length, Primary = method, Definition = method.DeclaringType!.GetGenericTypeDefinition() };
                    var grown = new IntPtr[primaries.Length + 1];
                    primaries.CopyTo(grown, 0);
                    primaries = grown;
                    var all = new Shared[shared.Length + 1];
                    shared.CopyTo(all, 0);
                    all[state.Index] = state;
                    shared = all;
                    byHandle[handle] = state;
                }
                state!.Kept.Add(replacement);
                state.Code[method.DeclaringType!] = code;
                state.ByType.Clear();
                if (method.DeclaringType == state.Primary.DeclaringType) primaries[state.Index] = code;
            }
            if (!first) return false;
            creating = state;
            replacement = Dispatcher(state);
            EngineLog.Engine($"shared instantiations: {method.DeclaringType}.{method.Name} shares its code with other instantiations; its patches run for its own, the others run their own code");
            return true;
        }

        /// A failed detour of new shared code leaves no table behind.
        static Exception? Detoured(Exception? __exception)
        {
            var state = creating;
            creating = null;
            if (__exception != null && state != null)
                lock (gate)
                    foreach (var (handle, entry) in byHandle)
                        if (entry == state)
                        {
                            byHandle.Remove(handle);
                            break;
                        }
            return __exception;
        }

        static IntPtr CodeOf(MethodBase method) =>
            method is DynamicMethod && methodDescriptor!.Invoke(method, null) is RuntimeMethodHandle handle ? handle.GetFunctionPointer()
                : method.MethodHandle.GetFunctionPointer();

        /// (this, arguments…) → an object of the first patched instantiation runs its code; any
        /// other the code `Resolve` gives for its type. Tail calls, so that the code runs in the
        /// dispatcher's frame, as it runs in the method's own when detoured directly.
        static DynamicMethod Dispatcher(Shared state)
        {
            var method = state.Primary;
            var declaring = method.DeclaringType!;
            var parameters = method.GetParameters();
            var types = new Type[parameters.Length + 1];
            // `this` as an object: typed as the instantiation, the JIT would take the type check for granted.
            types[0] = typeof(object);
            for (int i = 0; i < parameters.Length; i++) types[i + 1] = parameters[i].ParameterType;
            var returns = method is MethodInfo info ? info.ReturnType : typeof(void);
            // Owned by the declaring type, as Harmony owns a replacement: compiled as the
            // original's module is (optimized unless it is a debug build).
            var dispatcher = new DynamicMethod($"{declaring.FullName}.{method.Name}_Instantiations", returns, types, declaring, skipVisibility: true);
            var il = dispatcher.GetILGenerator();
            var other = il.DefineLabel();
            void Arguments()
            {
                for (int i = 0; i < types.Length; i++) il.Emit(OpCodes.Ldarg, (short)i);
            }
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Isinst, declaring);
            il.Emit(OpCodes.Brfalse, other);
            Arguments();
            il.Emit(OpCodes.Ldsfld, typeof(SharedInstantiations).GetField(nameof(primaries), BindingFlags.NonPublic | BindingFlags.Static)!);
            il.Emit(OpCodes.Ldc_I4, state.Index);
            il.Emit(OpCodes.Ldelem_I);
            il.Emit(OpCodes.Tailcall);
            il.EmitCalli(OpCodes.Calli, CallingConventions.Standard, returns, types, null);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(other);
            Arguments();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, state.Index);
            il.Emit(OpCodes.Call, typeof(SharedInstantiations).GetMethod(nameof(Resolve), BindingFlags.NonPublic | BindingFlags.Static)!);
            il.Emit(OpCodes.Tailcall);
            il.EmitCalli(OpCodes.Calli, CallingConventions.Standard, returns, types, null);
            il.Emit(OpCodes.Ret);
            return dispatcher;
        }

        /// The code `self`'s instantiation runs: its replacement, or a copy of its own IL, made
        /// the first time (null runs the first patched instantiation's, as a direct detour does).
        static IntPtr Resolve(object? self, int index)
        {
            var state = shared[index];
            if (self == null) return primaries[index];
            var type = self.GetType();
            if (state.ByType.TryGetValue(type, out var code)) return code;
            var declaring = type;
            while (declaring != null && !(declaring.IsConstructedGenericType && declaring.GetGenericTypeDefinition() == state.Definition))
                declaring = declaring.BaseType;
            if (declaring == null) throw new InvalidOperationException($"{type.FullName} is no {state.Definition.FullName}, though it runs {state.Primary.Name}'s code");
            bool found;
            lock (gate) found = state.Code.TryGetValue(declaring, out code);
            if (!found)
            {
                var method = (MethodBase)declaring.GetMemberWithSameMetadataDefinitionAs(state.Primary);
                lock (locker)
                {
                    lock (gate) found = state.Code.ContainsKey(declaring);
                    if (!found) PatchDependents.Rebuild([method], $"it shares its code with {state.Primary.DeclaringType}.{state.Primary.Name}, patched", anyCopy: true);
                }
                lock (gate) found = state.Code.TryGetValue(declaring, out code);
                if (!found) throw new InvalidOperationException($"{declaring.FullName}.{method.Name} shares the code of a patched instantiation, and no copy of its own could be made");
            }
            // Cached only while it is the instantiation's code (a replacement made meanwhile
            // clears the cache).
            lock (gate)
                if (state.Code.TryGetValue(declaring, out var current) && current == code) state.ByType[type] = code;
            return code;
        }
    }
}
