using System;
using System.Linq;

namespace OniMoono.Engine
{
    /// `Type.FullName` as Unity's Mono player produces it. For a constructed generic type FullName
    /// names each type argument's assembly, and on this runtime those differ from the original
    /// player's: framework types live in System.Private.CoreLib (Mono: mscorlib, System, …) and
    /// engine types in OniMoono.UnityEngine (Unity: UnityEngine.CoreModule, …). The game persists
    /// such names (ARCHITECTURE D1), so the host formats them with the original assemblies, given
    /// by `assemblyOf` (the display name the original player's assembly has for a type).
    internal static class MonoTypeNames
    {
        public static string FullName(Type type, Func<Type, string> assemblyOf)
        {
            if (type.IsArray)
            {
                var element = FullName(type.GetElementType()!, assemblyOf);
                int rank = type.GetArrayRank();
                return element + (rank == 1 && type.IsSZArray ? "[]" : "[" + new string(',', rank - 1) + "]");
            }
            if (!type.IsGenericType || type.IsGenericTypeDefinition) return type.FullName;
            var arguments = type.GetGenericArguments().Select(a => "[" + FullName(a, assemblyOf) + ", " + assemblyOf(a) + "]");
            return type.GetGenericTypeDefinition().FullName + "[" + string.Join(",", arguments) + "]";
        }
    }
}
