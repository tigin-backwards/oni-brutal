using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D49. Type.GetType with a name that names no assembly looks in the caller's
    /// assembly and then CoreLib, on both runtimes, but they take the caller differently. Mono
    /// (type_from_parsed_name and get_caller_no_system_or_reflection, metadata/icall.c; Unity's
    /// fork, branch unity-6000.3-mbe) walks past unmanaged frames, wrappers (a dynamic method is
    /// one) and mscorlib's System and System.Reflection namespaces to the first other frame.
    /// CoreCLR takes the method that called GetType. In a Harmony replacement, a dynamic method
    /// owned by MonoMod's DynamicMethodDefinition, that is 0Harmony: the game's
    /// SaveLoadRoot.LoadInternal finds an optional component's type with Type.GetType("Name"),
    /// so once a mod patched it (Colony Cuisine, Auto Kitchen) every rocket interior's ports came
    /// back without their components ("AddComponent with a null type") and the colony's load
    /// failed in ours alone. A finalizer of the three name-only overloads looks the name up in the
    /// assembly Mono's walk gives when CoreCLR's lookup found nothing and that assembly is not the
    /// one CoreCLR searched; it costs a stack walk only on a failed lookup.
    internal static class MonoTypeByName
    {
        static Harmony harmony = null!;
        static readonly Assembly CoreLib = typeof(object).Assembly;
        static readonly Assembly Host = typeof(MonoTypeByName).Assembly;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("OniMoono.Host.MonoTypeByName");
            var finalizer = new HarmonyMethod(typeof(MonoTypeByName), nameof(Finalizer));
            foreach (var parameters in new[] { new[] { typeof(string) }, [typeof(string), typeof(bool)], [typeof(string), typeof(bool), typeof(bool)] })
            {
                var method = typeof(Type).GetMethod(nameof(Type.GetType), BindingFlags.Public | BindingFlags.Static, parameters)
                    ?? throw new MissingMethodException(nameof(Type), nameof(Type.GetType));
                harmony.Patch(method, finalizer: finalizer);
            }
        }

        // Not inlined: its frame is one MonoCaller counts past.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Exception? Finalizer(object[] __args, ref Type? __result, Exception? __exception)
        {
            if (__result != null) return __exception;
            // A failed lookup that throws throws a TypeLoadException; other exceptions (a null or
            // malformed name, a bad assembly) are the same on Mono's route.
            if (__exception != null && __exception is not TypeLoadException) return __exception;
            if (__args[0] is not string name) return __exception;
            bool ignoreCase = __args.Length > 2 && __args[2] is true;
            if (MonoCaller() is not { } assembly) return __exception;
            Type? type;
            try
            {
                // A name that names an assembly is not this route's (Assembly.GetType refuses it).
                type = assembly.GetType(name, false, ignoreCase);
            }
            catch (ArgumentException)
            {
                return __exception;
            }
            if (type == null) return __exception;
            __result = type;
            return null;
        }

        /// The assembly Mono searches when it is not the one CoreCLR searched, else null. Frames:
        /// this, Finalizer, GetType's replacement, then GetType's caller (CoreCLR's) and its callers.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Assembly? MonoCaller()
        {
            var frames = new StackTrace(3, false).GetFrames();
            if (frames.Length == 0) return null;
            var coreclr = frames[0].GetMethod()?.Module.Assembly;
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method == null || method is DynamicMethod) continue;
                var assembly = method.Module.Assembly;
                if (assembly == CoreLib && method.DeclaringType?.Namespace is "System" or "System.Reflection") continue;
                return assembly == coreclr || assembly == Host ? null : assembly;
            }
            return null;
        }
    }
}
