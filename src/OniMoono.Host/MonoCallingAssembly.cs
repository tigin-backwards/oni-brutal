using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D41. Mono's Assembly.GetCallingAssembly walks past wrapper frames, dynamic
    /// methods among them (get_caller_no_reflection, icall.c), and past CoreLib's
    /// System.Reflection. A Harmony replacement is a dynamic method, so a prefix or postfix that
    /// asks is told the assembly of the code that called the patched method (measured in the
    /// original player). CoreCLR stops at the replacement, whose owner, MonoMod's
    /// DynamicMethodDefinition, is in 0Harmony: Mod Origin Info's postfixes on ComplexRecipe's
    /// constructors found no mod there and recorded no modded recipe. A postfix on CoreLib's
    /// GetCallingAssembly walks the stack as Mono does whenever CoreCLR's answer is 0Harmony, for
    /// the whole process as on Mono.
    internal static class MonoCallingAssembly
    {
        static Harmony harmony = null!;
        static readonly Assembly HarmonyAssembly = typeof(Harmony).Assembly;
        static readonly Assembly CoreLib = typeof(object).Assembly;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("OniMoono.Host.MonoCallingAssembly");
            var method = typeof(Assembly).GetMethod(nameof(Assembly.GetCallingAssembly), BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
                ?? throw new MissingMethodException(nameof(Assembly), nameof(Assembly.GetCallingAssembly));
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(MonoCallingAssembly), nameof(Postfix)));
        }

        // Not inlined: its frame is the first MonoCaller counts past.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Postfix(ref Assembly __result)
        {
            // Harmony's replacements are owned by 0Harmony, so CoreCLR names 0Harmony exactly when
            // the asking method's caller is one of them (or 0Harmony's own code).
            if (__result == HarmonyAssembly) __result = MonoCaller() ?? __result;
        }

        /// Mono's answer: past the asking method, the first frame that is neither a dynamic method
        /// (a wrapper on Mono) nor in CoreLib's System.Reflection or RuntimeMethodHandle; else the
        /// asking method's assembly.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Assembly? MonoCaller()
        {
            // Frames: this, Postfix, GetCallingAssembly's replacement, the asking method, its callers.
            var frames = new StackTrace(3, false).GetFrames();
            if (frames.Length == 0) return null;
            for (int i = 1; i < frames.Length; i++)
            {
                var method = frames[i].GetMethod();
                if (method == null || method is DynamicMethod) continue;
                var assembly = method.Module.Assembly;
                // CoreCLR's RuntimeMethodHandle.InvokeMethod calls a method invoked by reflection,
                // where Mono's runtime-invoke wrapper does.
                if (assembly == CoreLib && (method.DeclaringType?.Namespace == "System.Reflection" || method.DeclaringType == typeof(RuntimeMethodHandle))) continue;
                return assembly;
            }
            return frames[0].GetMethod()?.Module.Assembly;
        }
    }
}
