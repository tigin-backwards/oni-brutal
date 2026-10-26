using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniMoono.Engine.Serialization
{
    /// How a serialized value is represented.
    internal enum ValueKind
    {
        Bool, SByte, Byte, Short, UShort, Int, UInt, Long, ULong, Float, Double, Char,
        String,
        Enum,
        ObjectReference,   // UnityEngine.Object subclass, stored as a PPtr
        BuiltinStruct,     // Vector3, Color, Rect, … (engine-defined layout)
        BuiltinClass,      // AnimationCurve, Gradient, RectOffset, GUIStyle (engine-defined layout)
        Composite,         // [Serializable] class or struct with its own serialized fields
        Array,             // T[]
        List,              // List<T>
    }

    /// The serialized shape of one type: what Unity's serializer writes for it.
    internal sealed class ValueShape
    {
        public Type Type;
        public ValueKind Kind;
        public ValueShape Element;       // Array/List
        public ValueKind EnumUnderlying; // Enum
        public FieldShape[] Fields;      // Composite (resolved lazily to allow recursive types)
        public bool IsValueType;
    }

    internal sealed class FieldShape
    {
        public FieldInfo Field;
        public string Name;
        public ValueShape Shape;
    }

    /// Unity's serialization rules, shared by asset deserialization, Instantiate and JsonUtility.
    ///
    /// A field is serialized when it is an instance field that is not readonly/const and is
    /// public (and not [NonSerialized]) or marked [SerializeField], and its type is serializable:
    /// a primitive, string, enum, a UnityEngine.Object subclass (as a reference), one of Unity's
    /// built-in serializable value types, a [Serializable] non-abstract class or struct from game
    /// or engine code (closed generics included; BCL types excluded), or a T[] / List&lt;T&gt; of
    /// those (no nested collections). Base-class fields come first. A composite field whose type can
    /// reach the type declaring it (a composition cycle) is not serialized at all; other nested
    /// composites stop at a depth of 10.
    internal static class SerializedLayout
    {
        /// Unity calls ISerializationCallbackReceiver on every serialized class or struct value
        /// that implements it, nested ones included: OnBeforeSerialize before its fields are
        /// read, OnAfterDeserialize once its fields are written. Exceptions are logged.
        public static void BeforeSerialize(object value)
        {
            if (value is not UnityEngine.ISerializationCallbackReceiver r) return;
            try { r.OnBeforeSerialize(); }
            catch (System.Exception e) { UnityEngine.Debug.LogException(e); }
        }

        public static void AfterDeserialize(object value)
        {
            if (value is not UnityEngine.ISerializationCallbackReceiver r) return;
            try { r.OnAfterDeserialize(); }
            catch (System.Exception e) { UnityEngine.Debug.LogException(e); }
        }

        public const int MaxDepth = 10;

        static readonly ConcurrentDictionary<Type, ValueShape> shapes = new();
        static readonly ConcurrentDictionary<Type, FieldShape[]> objectFields = new();

        static readonly HashSet<Type> builtinStructs =
        [
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Vector2Int), typeof(Vector3Int),
            typeof(Quaternion), typeof(Matrix4x4), typeof(Color), typeof(Color32), typeof(Rect), typeof(RectInt),
            typeof(Bounds), typeof(LayerMask),
        ];

        static readonly HashSet<Type> builtinClasses =
        [
            typeof(AnimationCurve), typeof(Gradient), typeof(RectOffset), typeof(GUIStyle),
        ];

        /// The serialized fields of a MonoBehaviour/ScriptableObject type, base classes first,
        /// excluding engine base classes (UnityEngine.Object, Component, Behaviour, …), whose
        /// state is native.
        public static FieldShape[] ScriptFields(Type type) => objectFields.GetOrAdd(type, static t => CollectFields(t, stopAtEngine: true));

        /// The shape of a field type, or null when Unity does not serialize it.
        public static ValueShape ShapeOf(Type type)
        {
            if (shapes.TryGetValue(type, out var s)) return s;
            s = Build(type);
            if (s == null) return null;
            return shapes.GetOrAdd(type, s);
        }

        public static FieldShape[] CompositeFields(ValueShape shape)
        {
            if (shape.Fields == null) shape.Fields = CollectFields(shape.Type, stopAtEngine: false);
            return shape.Fields;
        }

        static readonly ConcurrentDictionary<Type, HashSet<Type>> reachable = new();

        /// Composite types reachable from `type` through serialized fields (including itself).
        static HashSet<Type> ReachableComposites(Type type)
        {
            if (reachable.TryGetValue(type, out var set)) return set;
            set = new HashSet<Type>();
            var stack = new Stack<Type>();
            stack.Push(type);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (!set.Add(t)) continue;
                foreach (var f in CandidateFields(t))
                {
                    var c = CompositeTypeOf(f.FieldType);
                    if (c != null) stack.Push(c);
                }
            }
            reachable[type] = set;
            return set;
        }

        /// The composite type a field stores (directly or as array/list elements), if any.
        static Type CompositeTypeOf(Type fieldType)
        {
            var shape = ShapeOf(fieldType);
            if (shape == null) return null;
            if (shape.Kind is ValueKind.Array or ValueKind.List) shape = shape.Element;
            return shape.Kind == ValueKind.Composite ? shape.Type : null;
        }

        static IEnumerable<FieldInfo> CandidateFields(Type type)
        {
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (IsSerializedField(f)) yield return f;
        }

        static ValueShape Build(Type type)
        {
            if (type.IsPrimitive)
            {
                var kind = PrimitiveKind(type);
                return kind == null ? null : new ValueShape { Type = type, Kind = kind.Value, IsValueType = true };
            }
            if (type == typeof(string)) return new ValueShape { Type = type, Kind = ValueKind.String };
            if (type.IsEnum)
            {
                var under = PrimitiveKind(Enum.GetUnderlyingType(type));
                return under == null ? null : new ValueShape { Type = type, Kind = ValueKind.Enum, EnumUnderlying = under.Value, IsValueType = true };
            }
            if (typeof(Object).IsAssignableFrom(type)) return new ValueShape { Type = type, Kind = ValueKind.ObjectReference };
            if (builtinStructs.Contains(type)) return new ValueShape { Type = type, Kind = ValueKind.BuiltinStruct, IsValueType = true };
            if (builtinClasses.Contains(type)) return new ValueShape { Type = type, Kind = ValueKind.BuiltinClass };
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1) return null;
                var element = ElementShape(type.GetElementType());
                return element == null ? null : new ValueShape { Type = type, Kind = ValueKind.Array, Element = element };
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var element = ElementShape(type.GetGenericArguments()[0]);
                return element == null ? null : new ValueShape { Type = type, Kind = ValueKind.List, Element = element };
            }
            if (IsSerializableComposite(type)) return new ValueShape { Type = type, Kind = ValueKind.Composite, IsValueType = type.IsValueType };
            return null;
        }

        /// Collections may not nest.
        static ValueShape ElementShape(Type elementType)
        {
            var element = ShapeOf(elementType);
            if (element == null || element.Kind is ValueKind.Array or ValueKind.List) return null;
            return element;
        }

        static bool IsSerializableComposite(Type type)
        {
            if (type.IsAbstract || type.IsInterface || type.IsPointer || type.ContainsGenericParameters) return false;
            if (typeof(Delegate).IsAssignableFrom(type)) return false;
#pragma warning disable SYSLIB0050 // Unity keys serializability on [Serializable].
            if (!type.IsSerializable) return false;
#pragma warning restore SYSLIB0050
            // Framework types are not Unity-serializable even when marked [Serializable].
            var asm = type.Assembly;
            if (asm == typeof(object).Assembly || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true) return false;
            return true;
        }

        static ValueKind? PrimitiveKind(Type t) => Type.GetTypeCode(t) switch
        {
            TypeCode.Boolean => ValueKind.Bool,
            TypeCode.SByte => ValueKind.SByte,
            TypeCode.Byte => ValueKind.Byte,
            TypeCode.Int16 => ValueKind.Short,
            TypeCode.UInt16 => ValueKind.UShort,
            TypeCode.Int32 => ValueKind.Int,
            TypeCode.UInt32 => ValueKind.UInt,
            TypeCode.Int64 => ValueKind.Long,
            TypeCode.UInt64 => ValueKind.ULong,
            TypeCode.Single => ValueKind.Float,
            TypeCode.Double => ValueKind.Double,
            TypeCode.Char => ValueKind.Char,
            _ => null,
        };

        static FieldShape[] CollectFields(Type type, bool stopAtEngine)
        {
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                if (stopAtEngine && IsEngineBase(t)) break;
                chain.Add(t);
            }
            chain.Reverse();
            var result = new List<FieldShape>();
            foreach (var t in chain)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!IsSerializedField(f)) continue;
                    var shape = ShapeOf(f.FieldType);
                    if (shape == null) continue;
                    // Infinite recursion disqualifies a field: its composite type leads back to
                    // the type being laid out.
                    if (!stopAtEngine && CompositeTypeOf(f.FieldType) is { } composite && ReachableComposites(composite).Contains(type)) continue;
                    result.Add(new FieldShape { Field = f, Name = f.Name, Shape = shape });
                }
            }
            return result.ToArray();
        }

        /// UnityEngine's own MonoBehaviour/ScriptableObject ancestry: their state is native, not
        /// serialized fields. Engine types that are serialized as scripts (uGUI lives in
        /// UnityEngine.UI.dll, not here) are not affected.
        static bool IsEngineBase(Type t)
            => t == typeof(Object) || t == typeof(Component) || t == typeof(Behaviour) || t == typeof(MonoBehaviour) || t == typeof(ScriptableObject);

        static bool IsSerializedField(FieldInfo f)
        {
            if (f.IsStatic || f.IsLiteral || f.IsInitOnly) return false;
            if (f.IsDefined(typeof(NonSerializedAttribute), false)) return false;
            return f.IsPublic || f.IsDefined(typeof(SerializeField), false);
        }
    }
}
