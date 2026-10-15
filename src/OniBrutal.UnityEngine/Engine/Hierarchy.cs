using System;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// Transform parenting and sibling order. Main thread only.
    internal static class Hierarchy
    {
        /// Subscribers notified after a transform's parent changed (canvas hierarchy tracking).
        public static event Action<TransformNative> ParentChanged;

        public static void RemoveChild(TransformNative parent, TransformNative child)
        {
            var children = parent.Children;
            int index = Array.IndexOf(children, child);
            if (index < 0) return;
            var shrunk = new TransformNative[children.Length - 1];
            Array.Copy(children, 0, shrunk, 0, index);
            Array.Copy(children, index + 1, shrunk, index, children.Length - index - 1);
            parent.Children = shrunk;
            child.Parent = null;
        }

        static void InsertChild(TransformNative parent, TransformNative child, int index)
        {
            var children = parent.Children;
            index = Math.Clamp(index, 0, children.Length);
            var grown = new TransformNative[children.Length + 1];
            Array.Copy(children, 0, grown, 0, index);
            grown[index] = child;
            Array.Copy(children, index, grown, index + 1, children.Length - index);
            parent.Children = grown;
            child.Parent = parent;
        }

        public static bool IsAncestorOrSelf(TransformNative ancestor, TransformNative t)
        {
            for (var p = t; p != null; p = p.Parent)
                if (p == ancestor) return true;
            return false;
        }

        public static void SetParent(TransformNative t, TransformNative newParent, bool worldPositionStays)
        {
            if (t.Parent == newParent) return;
            var go = t.GameObject;
            if (newParent != null && IsAncestorOrSelf(t, newParent))
            {
                Debug.LogError("Cannot set the parent of the GameObject '" + go.Name + "' to itself or one of its children.", go.Managed);
                return;
            }
            if (newParent != null && newParent.GameObject.IsPersistent != go.IsPersistent)
            {
                Debug.LogError("Setting the parent of a transform which resides in a Prefab Asset is disabled to prevent data corruption (GameObject: '" + go.Name + "').", go.Managed);
                return;
            }

            Vector3 worldPos = default, worldScale = default;
            Quaternion worldRot = default;
            if (worldPositionStays)
            {
                worldPos = Position(t);
                worldRot = t.WorldRotation;
                worldScale = LossyScale(t);
            }

            Messages.SendToSubtree(go, s => s.OnBeforeTransformParentChanged);

            var oldParent = t.Parent;
            if (oldParent != null) RemoveChild(oldParent, t);
            else if (go.Scene != null) Scenes.RemoveRoot(go.Scene, go);

            if (newParent != null)
            {
                InsertChild(newParent, t, newParent.Children.Length);
                if (newParent.GameObject.Scene != go.Scene) Scenes.SetSceneRecursive(go, newParent.GameObject.Scene);
            }
            else if (go.Scene != null)
            {
                Scenes.AddRoot(go.Scene, go);
            }

            if (worldPositionStays)
            {
                if (newParent == null)
                {
                    t.LocalPosition = worldPos;
                    t.LocalRotation = worldRot;
                    t.LocalScale = worldScale;
                }
                else
                {
                    t.LocalPosition = newParent.LocalToWorld.inverse.MultiplyPoint3x4(worldPos);
                    t.LocalRotation = Quaternion.Inverse(newParent.WorldRotation) * worldRot;
                    var parentScale = LossyScale(newParent);
                    t.LocalScale = new Vector3(Divide(worldScale.x, parentScale.x), Divide(worldScale.y, parentScale.y), Divide(worldScale.z, parentScale.z));
                }
            }
            t.ParentChangedInternal(worldPositionStays);
            t.MarkDirty();

            Messages.SendToSubtree(go, s => s.OnTransformParentChanged);
            if (oldParent != null) Messages.SendToComponents(oldParent.GameObject, s => s.OnTransformChildrenChanged);
            if (newParent != null) Messages.SendToComponents(newParent.GameObject, s => s.OnTransformChildrenChanged);
            ParentChanged?.Invoke(t);
            Lifecycle.UpdateActiveInHierarchy(go);
        }

        static float Divide(float a, float b) => b == 0f ? 0f : a / b;

        public static Vector3 Position(TransformNative t)
        {
            var m = t.LocalToWorld;
            return new Vector3(m.m03, m.m13, m.m23);
        }

        /// World scale with the world rotation removed (exact without skew).
        public static Vector3 LossyScale(TransformNative t)
        {
            var m = t.LocalToWorld;
            var inv = Matrix4x4.Rotate(Quaternion.Inverse(t.WorldRotation));
            var rs = inv * m;
            return new Vector3(rs.m00, rs.m11, rs.m22);
        }

        public static int GetSiblingIndex(TransformNative t)
        {
            if (t.Parent != null) return Array.IndexOf(t.Parent.Children, t);
            var scene = t.GameObject.Scene;
            return scene != null ? scene.Roots.IndexOf(t.GameObject) : 0;
        }

        public static void SetSiblingIndex(TransformNative t, int index)
        {
            if (t.Parent != null)
            {
                var parent = t.Parent;
                int current = Array.IndexOf(parent.Children, t);
                index = Math.Clamp(index, 0, parent.Children.Length - 1);
                if (current == index) return;
                RemoveChild(parent, t);
                InsertChild(parent, t, index);
                t.SiblingOrderChangedInternal();
                Messages.SendToComponents(parent.GameObject, s => s.OnTransformChildrenChanged);
                return;
            }
            var scene = t.GameObject.Scene;
            if (scene == null) return;
            int at = scene.Roots.IndexOf(t.GameObject);
            index = Math.Clamp(index, 0, scene.Roots.Count - 1);
            if (at == index) return;
            scene.Roots.RemoveAt(at);
            scene.Roots.Insert(index, t.GameObject);
        }
    }

    /// Engine messages sent to the MonoBehaviours of a GameObject or subtree.
    internal static class Messages
    {
        /// Script messages reach only GameObjects that are active in the hierarchy.
        public static void SendToComponents(GameObjectNative go, Func<ScriptInfo, Action<MonoBehaviour>> select)
        {
            if (!go.ActiveInHierarchy) return;
            foreach (var c in go.Components)
            {
                if (c is not MonoBehaviour mb || c.Native is not BehaviourNative bn || bn.Script == null) continue;
                var m = select(bn.Script);
                if (m != null) Lifecycle.Invoke(m, mb);
            }
        }

        public static void SendToSubtree(GameObjectNative go, Func<ScriptInfo, Action<MonoBehaviour>> select)
        {
            SendToComponents(go, select);
            foreach (var child in go.Transform.Children) SendToSubtree(child.GameObject, select);
        }
    }
}
