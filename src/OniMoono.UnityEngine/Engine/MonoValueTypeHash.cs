using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace OniMoono.Engine
{
    /// `ValueType.GetHashCode` as Unity's Mono computes it, for a struct that does not override
    /// it (ARCHITECTURE D26). Mono (`ves_icall_System_ValueType_InternalGetHashCode` and
    /// `System.ValueType.GetHashCode` in its corlib) XORs every instance field into the hash: an
    /// `int` field's value, a string field's `mono_string_hash_internal` (h = h * 31 + c over its
    /// UTF-16 units), a pointer field's `mono_aligned_addr_hash`, and for any other field the
    /// `GetHashCode` of its boxed value, null skipped. So a nested struct without an override
    /// contributes its own Mono hash. CoreCLR hashes such a struct by its first field alone when
    /// it holds floats or references, so structs that differ in a later field collide.
    ///
    /// Mono starts from the address of its `int32_class` (`mono_defaults.int32_class`), which
    /// differs between processes; here the start is 0. Only the fields' contribution is Mono's,
    /// so hashes are equal to the original player's up to that one constant, and two values'
    /// hashes differ exactly where Mono's do.
    internal static class MonoValueTypeHash
    {
        static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();

        public static int Compute(object boxed)
        {
            ArgumentNullException.ThrowIfNull(boxed);
            var type = boxed.GetType();
            if (!type.IsValueType)
                throw new ArgumentException($"{type} is not a value type.", nameof(boxed));
            int result = 0;
            foreach (var field in Fields.GetOrAdd(type, InstanceFields))
            {
                var fieldType = field.FieldType;
                if (fieldType == typeof(int))
                    result ^= (int)field.GetValue(boxed)!;
                else if (fieldType == typeof(string))
                {
                    if (field.GetValue(boxed) is string text) result ^= (int)StringHash(text);
                }
                else if (fieldType.IsPointer)
                    result ^= (int)AlignedAddressHash(PointerValue(field, boxed));
                else if (field.GetValue(boxed) is { } value)
                    result ^= HashOfBoxed(value);
            }
            return result;
        }

        /// A boxed field value's virtual `GetHashCode`, as Mono's managed loop calls it: a struct
        /// without its own override is hashed the Mono way again.
        static int HashOfBoxed(object value)
        {
            var type = value.GetType();
            if (type.IsValueType && !type.IsPrimitive && !type.IsEnum && !OverridesGetHashCode(type))
                return Compute(value);
            return value.GetHashCode();
        }

        static bool OverridesGetHashCode(Type type) =>
            type.GetMethod(nameof(GetHashCode), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)!.DeclaringType != typeof(ValueType);

        static FieldInfo[] InstanceFields(Type type) =>
            type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        /// `mono_string_hash_internal`.
        internal static uint StringHash(string text)
        {
            uint h = 0;
            foreach (char c in text)
                h = (h << 5) - h + c;
            return h;
        }

        /// `mono_aligned_addr_hash`: the pointer truncated to 32 bits, shifted past its alignment,
        /// times Knuth's multiplicative constant.
        internal static uint AlignedAddressHash(nuint pointer) => ((uint)pointer >> 3) * 2654435761u;

        static unsafe nuint PointerValue(FieldInfo field, object boxed) =>
            (nuint)Pointer.Unbox(field.GetValue(boxed)!);
    }
}
