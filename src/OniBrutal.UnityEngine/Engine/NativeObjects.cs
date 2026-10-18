using System.Collections.Generic;
using OniBrutal.Assets.TypeTrees;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniBrutal.Engine
{
    /// Engine-side record of a UnityEngine.Object (what Unity keeps in native memory).
    internal class NativeObject
    {
        public int Slot;
        public int Generation;
        public int InstanceId;
        public Object Managed;
        public string Name = "";
        public HideFlags HideFlags;
        /// Loaded from an asset file (scene objects excluded). Persistent objects are never
        /// Awakened and are released only by Resources.UnloadUnusedAssets / bundle unload.
        public bool IsPersistent;
        /// Where the object was loaded from, when it was.
        public AssetSource Source;
        /// Destroy has been requested (deferred destruction pending) or performed.
        public bool DestroyRequested;
        public bool Destroyed;

        /// Copies engine-side state for Instantiate (each record type adds its own state).
        public virtual void CopyStateFrom(NativeObject source) { }

        /// After Instantiate copied the state: references to objects of the cloned hierarchy
        /// are redirected to their clones (`map` returns its argument for anything else), as
        /// Unity does for every reference a component holds.
        public virtual void RemapReferences(System.Func<UnityEngine.Object, UnityEngine.Object> map) { }

        /// Serialized data of an engine object loaded from an asset file, kept until the owning
        /// subsystem's Bind consumes it.
        public OniBrutal.Assets.TypeTrees.Fields Serialized;

        /// Builds engine state from serialized data once references can be resolved. Record types
        /// of subsystems that bind their data override it and clear Serialized.
        public virtual void Bind(OniBrutal.Assets.TypeTrees.Fields data, OniBrutal.Engine.Serialization.IReferenceResolver resolver) { }
    }

    /// Identity of an object loaded from a file: the file and the object's pathID in it.
    internal readonly record struct AssetSource(string File, long PathId);

    internal sealed class GameObjectNative : NativeObject
    {
        /// Component order as in Unity; [0] is always the Transform. Copy-on-write so that
        /// worker threads can read it while the main thread mutates.
        public Component[] Components = System.Array.Empty<Component>();
        public int Layer;
        public string Tag = "Untagged";
        public bool ActiveSelf = true;
        /// Cached activeInHierarchy, maintained by activation changes.
        public bool ActiveInHierarchy;
        /// Owning scene; null for prefab assets and other objects that are not in a scene.
        public SceneRecord Scene;
        public bool IsStatic;

        /// The native side of Components[0].
        public TransformNative Transform;
        /// Slot in the owning scene's root list while a root, else -1 (SceneRoots).
        public int RootSlot = -1;
        /// Its canvas components, cached by CanvasSystem for the current Components array.
        public UI.UiParts UiParts;
    }

    internal class ComponentNative : NativeObject
    {
        public GameObjectNative GameObject;
        /// EngineAwake has run (components that are not Behaviours).
        public bool AwakeCalledForComponent;
        /// EngineOnEnable has run and EngineOnDisable has not run since (components that are not
        /// Behaviours).
        public bool EngineEnabled;
        /// Destroy was called on the component or its GameObject (or an ancestor) and it awaits
        /// destruction: as in Unity it is disabled at once and is not enabled again.
        public bool DestroyPending;
    }

    internal class BehaviourNative : ComponentNative
    {
        public bool Enabled = true;
        /// OnEnable has run and OnDisable has not run since (Unity's "is in enabled state").
        public bool IsActiveAndEnabledState;
        public bool AwakeCalled;
        public bool StartCalled;
        /// Scheduled for Start in the next Start phase.
        public bool StartPending;
        public ScriptInfo Script;
        public int ExecutionOrder;
        /// MonoBehaviour.useGUILayout: OnGUI gets Layout events and may use GUILayout.
        public bool UseGUILayout = true;

        public override void CopyStateFrom(NativeObject source)
        {
            var b = (BehaviourNative)source;
            Enabled = b.Enabled;
            UseGUILayout = b.UseGUILayout;
        }
        /// Per-behaviour coroutines (MonoBehaviour only).
        public List<CoroutineState> Coroutines;
    }

    internal partial class TransformNative : ComponentNative
    {
        public TransformNative Parent;
        /// Children in sibling order; copy-on-write.
        public TransformNative[] Children = System.Array.Empty<TransformNative>();
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
        public bool HasChanged = true;

        Matrix4x4 localToWorld;
        bool worldDirty = true;

        public override void CopyStateFrom(NativeObject source)
        {
            var t = (TransformNative)source;
            LocalPosition = t.LocalPosition;
            LocalRotation = t.LocalRotation;
            LocalScale = t.LocalScale;
        }

        public Matrix4x4 LocalToWorld
        {
            get
            {
                if (worldDirty)
                {
                    var local = Matrix4x4.TRS(LocalPosition, LocalRotation, LocalScale);
                    localToWorld = Parent != null ? Parent.LocalToWorld * local : local;
                    worldDirty = false;
                }
                return localToWorld;
            }
        }

        public Quaternion WorldRotation => Parent != null ? Parent.WorldRotation * LocalRotation : LocalRotation;

        /// Invalidates cached world data for this transform and its descendants, and marks
        /// them changed (Transform.hasChanged).
        public void MarkDirty()
        {
            worldDirty = true;
            HasChanged = true;
            foreach (var c in Children) c.MarkDirty();
            OnTransformChanged();
        }

        protected virtual void OnTransformChanged() { }

        /// Called after the parent changed (RectTransforms re-resolve their anchoring).
        public virtual void ParentChangedInternal(bool worldPositionStays) { }

        /// Called after the sibling index changed.
        public virtual void SiblingOrderChangedInternal() { }
    }
}
