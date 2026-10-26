using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D13. Before detouring a method, Harmony's MonoMod marks it not to be inlined
    /// (its MethodDesc's NotInline flag), so code compiled later calls the patched method instead
    /// of inlining the original's body. For a virtual method of a value type (an override of
    /// GetHashCode, Equals, ToString), reflection's handle is the method's unboxing stub; CoreCLR's
    /// JIT inlines the unboxed MethodDesc that follows it, which MonoMod never marks, so code tiered
    /// up after the patch runs the original (Fast Track's AxialI.GetHashCode: lookups in
    /// dictionaries filled at tier 0 missed, ClusterGrid.RegisterEntity threw). The host marks the
    /// unboxed MethodDesc as well. Mono has no unboxing stubs, so the original player never needs to.
    internal static class UnboxedMethods
    {
        // CoreCLR 10's MethodDesc (src/coreclr/vm/method.hpp): UINT16 m_wFlags3AndTokenRemainder,
        // BYTE m_chunkIndex, BYTE m_bFlags4, WORD m_wSlotNumber, WORD m_wFlags; 8-byte aligned.
        const int Flags3 = 0, ChunkIndex = 2, Flags = 6, Alignment = 8;
        const ushort TokenRemainderMask = 0x0FFF, IsUnboxingStub = 0x4000;
        const ushort HasNonVtableSlot = 0x0008, NotInline = 0x2000;
        // MethodTableBuilder puts a value type's unboxed MethodDesc right after its unboxing stub;
        // the stub's size is its base size and optional slots, at most this.
        const int MaxStubSize = 64;

        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var tryDisable = AccessTools.Method("MonoMod.Core.Platforms.PlatformTriple:TryDisableInlining")
                ?? throw new MissingMethodException("MonoMod.Core.Platforms.PlatformTriple", "TryDisableInlining");
            new Harmony("OniMoono.Host.UnboxedMethods").Patch(tryDisable, postfix: new HarmonyMethod(typeof(UnboxedMethods), nameof(Postfix)));
        }

        static void Postfix(MethodBase method, bool __result)
        {
            if (!__result || method.DeclaringType is not { IsValueType: true } || method.IsStatic || !method.IsVirtual) return;
            if (!MarkUnboxed(method.MethodHandle.Value))
                EngineLog.Engine($"unboxed methods: no unboxed MethodDesc after {method.DeclaringType.FullName}.{method.Name}'s unboxing stub; a patch of it may be inlined past");
        }

        /// Sets NotInline on the unboxed MethodDesc following an unboxing stub. False when the handle
        /// is no unboxing stub or no MethodDesc after it matches (same chunk, same token, not a stub,
        /// in a non-vtable slot).
        internal static unsafe bool MarkUnboxed(IntPtr stub)
        {
            var unboxed = Unboxed(stub);
            if (unboxed == IntPtr.Zero) return false;
            SetFlag((byte*)unboxed, NotInline);
            return true;
        }

        /// The unboxed MethodDesc following an unboxing stub, or zero.
        internal static unsafe IntPtr Unboxed(IntPtr stub)
        {
            byte* p = (byte*)stub;
            ushort flags3 = *(ushort*)(p + Flags3);
            if ((flags3 & IsUnboxingStub) == 0) return IntPtr.Zero;
            for (int offset = Alignment; offset <= MaxStubSize; offset += Alignment)
            {
                byte* q = p + offset;
                ushort qFlags3 = *(ushort*)(q + Flags3);
                if (q[ChunkIndex] != p[ChunkIndex] + offset / Alignment) continue;
                if ((qFlags3 & TokenRemainderMask) != (flags3 & TokenRemainderMask) || (qFlags3 & IsUnboxingStub) != 0) continue;
                if ((*(ushort*)(q + Flags) & HasNonVtableSlot) == 0) continue;
                return (IntPtr)q;
            }
            return IntPtr.Zero;
        }

        /// m_wFlags is the high half of the aligned 32-bit word at offset 4 (little-endian); the
        /// runtime updates it with interlocked operations too.
        static unsafe void SetFlag(byte* methodDesc, ushort flag)
        {
            ref int word = ref *(int*)(methodDesc + Flags - 2);
            int seen, wanted;
            do
            {
                seen = Volatile.Read(ref word);
                wanted = seen | (flag << 16);
            }
            while (seen != wanted && Interlocked.CompareExchange(ref word, wanted, seen) != seen);
        }
    }
}
