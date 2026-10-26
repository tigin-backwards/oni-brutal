using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D20. Before detouring a method, Harmony's MonoMod prepares it. For a P/Invoke,
    /// CoreCLR then binds the native export, and a missing one fails the patch
    /// (EntryPointNotFoundException). Mono binds a P/Invoke at its first call, so a mod can patch one
    /// whose export the platform lacks and keep it from being called: Debug Buttons and ONI Together
    /// skip `KImGuiUtil.SetKAssertCB`, absent from the game's Linux cimgui, and load in the original
    /// player. The host lets the preparation of a P/Invoke go without its export; the detour is
    /// placed on the method's own entry, and calling the original still fails as on Mono.
    ///
    /// CoreCLR's JIT also inlines the transition of a P/Invoke that needs no marshalling into
    /// optimized callers (all game and mod code; not debug builds), which then call the native
    /// target directly and never reach the detour: the prefix is skipped and a missing export
    /// throws. The host marks a P/Invoke it prepares for a patch as needing marshalling, so code
    /// compiled later calls its entry, as MonoMod marks other patched methods not to be inlined.
    internal static class PInvokePatches
    {
        // CoreCLR 10's PInvokeMethodDesc (src/coreclr/vm/method.hpp), after the MethodDesc:
        // m_pszEntrypointName, m_pszLibName, m_pPInvokeTarget, m_pImportThunkGlue (pointers), ULONG
        // m_DefaultDllImportSearchPathsAttributeValue, WORD m_wPInvokeFlags. The MethodDesc's size
        // differs between builds of the runtime, so the entry point's name locates the fields.
        const int FirstField = 8, LastField = 24, FlagsAfterName = 4 * 8 + 4;
        const int MarshalingRequiredCached = 0x0010, CachedMarshalingRequired = 0x0020, Populated = 0x8000;

        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var compile = AccessTools.Method("MonoMod.Core.Platforms.PlatformTriple:Compile")
                ?? throw new MissingMethodException("MonoMod.Core.Platforms.PlatformTriple", "Compile");
            new Harmony("OniMoono.Host.PInvokePatches").Patch(compile, finalizer: new HarmonyMethod(typeof(PInvokePatches), nameof(Finalizer)));
        }

        static Exception? Finalizer(Exception? __exception, MethodBase method)
        {
            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0) return __exception;
            if (!MarkMarshalingRequired(method))
                EngineLog.Engine($"pinvoke patches: no P/Invoke fields found for {method.DeclaringType?.FullName}.{method.Name}; optimized callers may skip its patches");
            return __exception is EntryPointNotFoundException or DllNotFoundException ? null : __exception;
        }

        /// Sets the cached "marshalling required" result on a prepared P/Invoke's MethodDesc, so the
        /// JIT does not inline its transition. False when the fields do not match the method's
        /// DllImport (entry point and library names, populated flags).
        internal static unsafe bool MarkMarshalingRequired(MethodBase method)
        {
            var import = method.GetCustomAttribute<DllImportAttribute>();
            if (import is null) return false;
            byte* md = (byte*)method.MethodHandle.Value;
            for (int offset = FirstField; offset <= LastField; offset += IntPtr.Size)
            {
                byte* name = *(byte**)(md + offset);
                if (!Utf8Equals(name, import.EntryPoint ?? method.Name) || !Utf8Equals(*(byte**)(md + offset + IntPtr.Size), import.Value)) continue;
                int* word = (int*)(md + offset + FlagsAfterName);
                if (((nint)word & 3) != 0 || (Volatile.Read(ref *word) & Populated) == 0) return false;
                // The flags are the low half of an aligned 32-bit word the runtime also updates
                // with interlocked operations (PInvokeMethodDesc::InterlockedSetPInvokeFlags).
                Interlocked.Or(ref *word, MarshalingRequiredCached | CachedMarshalingRequired);
                return true;
            }
            return false;
        }

        static unsafe bool Utf8Equals(byte* s, string expected)
        {
            if (s == null) return false;
            int i = 0;
            for (; i < expected.Length; i++)
                if (s[i] != expected[i] || expected[i] > 0x7F) return false;
            return s[i] == 0;
        }
    }
}
