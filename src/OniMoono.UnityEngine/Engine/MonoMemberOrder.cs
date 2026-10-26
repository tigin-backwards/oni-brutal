using System;
using System.Collections.Generic;
using System.Reflection;

namespace OniMoono.Engine
{
    /// Fields and properties in the order Unity's Mono lists them (ARCHITECTURE D39). Mono's
    /// `ves_icall_RuntimeType_GetFields_native` and `GetPropertiesByName_native` (icall.c; Unity's
    /// fork, branch `unity-6000.3-mbe`) walk the type and then each base class, and list each
    /// class's members in its metadata order (`mono_class_get_fields_lazy`,
    /// `mono_class_get_properties`). CoreCLR also walks from the type to its bases, but lists a
    /// class's instance fields, then its static fields, then its constants
    /// (`RuntimeTypeCache.PopulateFields`); and on CoreCLR Harmony's MonoMod sorts the cached
    /// field and property lists of every type a patched method's IL names by metadata token, across
    /// the hierarchy and across assemblies (`ReflectionHelper.FixReflectionCache`), so a base
    /// class's members may come first, depending on what has been patched. Measured in both
    /// players (tools/UnityProbe, `PROBE_SCENARIO=memberorder` and `smorder`).
    ///
    /// Only the order changes: the members are CoreCLR's.
    internal static class MonoMemberOrder
    {
        /// Reorders `members`, which `type`'s GetFields or GetProperties returned, into Mono's
        /// order: the type's own members, then each base class's, each class's by metadata token
        /// (its metadata order). Members of a type outside the class chain (an interface's
        /// constants, which Mono does not list) keep their order after them.
        internal static void Sort<T>(Type type, T[] members) where T : MemberInfo
        {
            if (members.Length < 2 || InOrder(members)) return;
            var chain = new List<Type>();
            for (var t = type; t != null; t = t.BaseType) chain.Add(t);
            var keys = new long[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                int depth = members[i].DeclaringType is { } declaring ? chain.IndexOf(declaring) : -1;
                if (depth < 0) depth = chain.Count;
                // Depth, then the token's row (unique within a class), then the position.
                keys[i] = (long)depth << 48 | (long)(members[i].MetadataToken & 0xFFFFFF) << 24 | (uint)i;
            }
            Array.Sort(keys, members);
        }

        /// Whether the members are in Mono's order already: each class's members contiguous and
        /// ascending by token, each class followed only by a base class of it.
        static bool InOrder<T>(T[] members) where T : MemberInfo
        {
            var previous = members[0];
            for (int i = 1; i < members.Length; i++)
            {
                var member = members[i];
                Type a = previous.DeclaringType, b = member.DeclaringType;
                if (a == b)
                {
                    if (previous.MetadataToken >= member.MetadataToken) return false;
                }
                else if (a == null || b == null || b.IsInterface || !a.IsSubclassOf(b))
                {
                    return false;
                }
                previous = member;
            }
            return true;
        }
    }
}
