using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniBrutal.Engine.Serialization
{
    /// Object.Instantiate: a copy shaped by serialization, as in Unity.
    ///
    /// - Only serialized fields are copied (SerializedLayout); other fields of the clone keep the
    ///   values its constructor and field initializers gave them.
    /// - References to objects inside the cloned hierarchy point to their clones; other references
    ///   are kept.
    /// - Values are copied by their declared type: a null serializable class becomes a new
    ///   instance, null arrays and lists become empty ones, and a subclass instance in a field of a
    ///   base type becomes an instance of the base type (no polymorphism without
    ///   [SerializeReference]).
    /// - ISerializationCallbackReceiver: OnBeforeSerialize on the originals, OnAfterDeserialize on
    ///   the clones.
    /// - The cloned hierarchy gets Awake/OnEnable before Instantiate returns if it is active.
    internal static class Cloner
    {
        public static Object Instantiate(Object original, Transform parent, bool worldPositionStays, Vector3? position, Quaternion? rotation)
        {
            if (original == null) throw new ArgumentException("The Object you want to instantiate is null.");
            Threads.AssertMainThread("Instantiate");
            if (original is GameObject || original is Component) return InstantiateHierarchy(original, parent, worldPositionStays, position, rotation);
            if (original is ScriptableObject so) return CloneScriptableObject(so);
            var clone = original.EngineCloneAsset();
            if (clone == null) throw new InvalidOperationException($"Instantiate is not available for {original.GetType().FullName} objects in this engine.");
            clone.NativeOrThrow.Name = original.name + "(Clone)";
            return clone;
        }

        static Object CloneScriptableObject(ScriptableObject original)
        {
            var native = new NativeObject { Name = original.name + "(Clone)", HideFlags = original.hideFlags };
            var clone = Object.ConstructBound<ScriptableObject>(original.GetType(), native);
            var remap = new Dictionary<Object, Object>(ReferenceEqualityComparer.Instance) { [original] = clone };
            CopyScript(original, clone, remap);
            ScriptableObjectLifecycle.Created(clone);
            return clone;
        }

        static Object InstantiateHierarchy(Object original, Transform parent, bool worldPositionStays, Vector3? position, Quaternion? rotation)
        {
            var rootOriginal = original as GameObject ?? ((Component)original).gameObject;
            var rootNative = (GameObjectNative)rootOriginal.NativeOrThrow;
            var remap = new Dictionary<Object, Object>(ReferenceEqualityComparer.Instance);

            // 1. Structure: GameObjects, transforms and components, without lifecycle calls.
            var rootClone = CloneStructure(rootNative, null, remap);
            rootClone.Name = rootNative.Name + "(Clone)";

            // 2. Serialized state of scripts, with references remapped into the clone.
            foreach (var (src, dst) in remap)
                if (src is MonoBehaviour && dst is MonoBehaviour) CopyScript(src, dst, remap);

            // 3. Placement.
            var t = rootClone.Transform;
            var originalTransform = rootNative.Transform;
            if (position.HasValue)
            {
                t.LocalPosition = position.Value;
                t.LocalRotation = rotation ?? Quaternion.identity;
            }
            if (parent != null)
            {
                var p = parent.TransformNative;
                bool keepWorld = position.HasValue || worldPositionStays;
                if (keepWorld)
                {
                    var worldPos = position ?? Hierarchy.Position(originalTransform);
                    var worldRot = rotation ?? originalTransform.WorldRotation;
                    var worldScale = Hierarchy.LossyScale(originalTransform);
                    t.LocalPosition = p.LocalToWorld.inverse.MultiplyPoint3x4(worldPos);
                    t.LocalRotation = Quaternion.Inverse(p.WorldRotation) * worldRot;
                    var ps = Hierarchy.LossyScale(p);
                    t.LocalScale = new Vector3(ps.x == 0 ? 0 : worldScale.x / ps.x, ps.y == 0 ? 0 : worldScale.y / ps.y, ps.z == 0 ? 0 : worldScale.z / ps.z);
                }
                AttachUnder(t, p, keepWorld);
                Scenes.SetSceneRecursive(rootClone, p.GameObject.Scene);
            }
            else
            {
                var scene = Scenes.Active;
                if (scene != null)
                {
                    Scenes.AddRoot(scene, rootClone);
                    Scenes.SetSceneRecursive(rootClone, scene);
                }
            }
            t.MarkDirty();
            if (t is RectTransformNative rt) rt.ParentChangedInternal(position.HasValue || (parent != null && worldPositionStays));

            // 4. Lifecycle.
            Lifecycle.UpdateActiveInHierarchy(rootClone);

            return remap[original];
        }

        static void AttachUnder(TransformNative child, TransformNative parent, bool keepWorld)
        {
            var children = parent.Children;
            var grown = new TransformNative[children.Length + 1];
            Array.Copy(children, grown, children.Length);
            grown[^1] = child;
            parent.Children = grown;
            child.Parent = parent;
        }

        static GameObjectNative CloneStructure(GameObjectNative src, TransformNative parentClone, Dictionary<Object, Object> remap)
        {
            var go = new GameObjectNative
            {
                Name = src.Name,
                HideFlags = src.HideFlags,
                Layer = src.Layer,
                Tag = src.Tag,
                ActiveSelf = src.ActiveSelf,
                IsStatic = src.IsStatic,
            };
            var goManaged = new GameObject(go);
            remap[src.Managed] = goManaged;

            var components = src.Components;
            var clones = new Component[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                var srcNative = (ComponentNative)c.NativeOrThrow;
                var native = ComponentNatives.Create(c.GetType());
                native.GameObject = go;
                native.HideFlags = srcNative.HideFlags;
                native.CopyStateFrom(srcNative);
                if (native is BehaviourNative bn && c is MonoBehaviour)
                {
                    bn.Script = ScriptInfo.Get(c.GetType());
                    bn.ExecutionOrder = bn.Script.ExecutionOrder;
                }
                clones[i] = Object.ConstructBound<Component>(c.GetType(), native);
                remap[c] = clones[i];
            }
            go.Components = clones;
            go.Transform = (TransformNative)clones[0].NativeOrThrow;
            go.Transform.Parent = parentClone;

            var srcChildren = src.Transform.Children;
            var childClones = new TransformNative[srcChildren.Length];
            for (int i = 0; i < srcChildren.Length; i++)
                childClones[i] = CloneStructure(srcChildren[i].GameObject, go.Transform, remap).Transform;
            go.Transform.Children = childClones;
            return go;
        }

        /// Copies the serialized fields of a MonoBehaviour or ScriptableObject.
        static void CopyScript(Object src, Object dst, Dictionary<Object, Object> remap)
        {
            if (src is ISerializationCallbackReceiver before) Invoke(() => before.OnBeforeSerialize(), src);
            foreach (var f in SerializedLayout.ScriptFields(src.GetType()))
                f.Field.SetValue(dst, CopyValue(f.Field.GetValue(src), f.Shape, remap, 1));
            if (dst is ISerializationCallbackReceiver after) Invoke(() => after.OnAfterDeserialize(), dst);
        }

        static void Invoke(Action a, Object context)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                Debug.LogException(e, context);
            }
        }

        static object CopyValue(object value, ValueShape shape, Dictionary<Object, Object> remap, int depth)
        {
            switch (shape.Kind)
            {
                case ValueKind.ObjectReference:
                    if (value is Object o && remap.TryGetValue(o, out var mapped)) return mapped;
                    return value;
                case ValueKind.BuiltinClass:
                    return CopyBuiltinClass(value, shape.Type);
                case ValueKind.Composite:
                    return CopyComposite(value, shape, remap, depth);
                case ValueKind.Array:
                {
                    var src = (Array)value;
                    int n = src?.Length ?? 0;
                    var dst = Array.CreateInstance(shape.Element.Type, n);
                    for (int i = 0; i < n; i++) dst.SetValue(CopyValue(src.GetValue(i), shape.Element, remap, depth), i);
                    return dst;
                }
                case ValueKind.List:
                {
                    var src = (IList)value;
                    var dst = (IList)Activator.CreateInstance(shape.Type);
                    if (src != null)
                        foreach (var item in src) dst.Add(CopyValue(item, shape.Element, remap, depth));
                    return dst;
                }
                default:
                    // Primitives, strings, enums and built-in structs are values.
                    return value;
            }
        }

        static object CopyComposite(object value, ValueShape shape, Dictionary<Object, Object> remap, int depth)
        {
            if (depth > SerializedLayout.MaxDepth) return shape.IsValueType ? Activator.CreateInstance(shape.Type) : null;
            var dst = NewInstance(shape.Type);
            foreach (var f in SerializedLayout.CompositeFields(shape))
            {
                var fieldValue = value == null ? null : f.Field.GetValue(value);
                if (value == null && f.Shape.IsValueType) fieldValue = f.Field.GetValue(dst); // keep the new instance's default
                f.Field.SetValue(dst, CopyValue(fieldValue, f.Shape, remap, depth + 1));
            }
            return dst;
        }

        /// A new instance as Unity's serializer creates it: through the parameterless constructor
        /// when there is one (field initializers run), otherwise uninitialized.
        public static object NewInstance(Type type)
        {
            if (type.IsValueType) return Activator.CreateInstance(type);
            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            return ctor != null ? ctor.Invoke(null) : RuntimeHelpers.GetUninitializedObject(type);
        }

        static object CopyBuiltinClass(object value, Type type)
        {
            if (type == typeof(AnimationCurve))
            {
                var src = value as AnimationCurve;
                var dst = new AnimationCurve();
                if (src != null)
                {
                    dst.keys = src.keys;
                    dst.preWrapMode = src.preWrapMode;
                    dst.postWrapMode = src.postWrapMode;
                }
                return dst;
            }
            if (type == typeof(Gradient))
            {
                var dst = new Gradient();
                if (value is Gradient src) dst.CopyFrom(src);
                return dst;
            }
            if (type == typeof(RectOffset))
            {
                var src = value as RectOffset;
                return src == null ? new RectOffset() : new RectOffset(src.left, src.right, src.top, src.bottom);
            }
            if (type == typeof(GUIStyle)) return value is GUIStyle style ? new GUIStyle(style) : new GUIStyle();
            throw new InvalidOperationException("Unknown built-in serialized class " + type);
        }
    }
}
