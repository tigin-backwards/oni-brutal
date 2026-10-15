using System;
using System.Collections.Generic;
using OniBrutal.Engine;

namespace UnityEngine
{
    public partial class Object
    {
        /// Engine hook: the object is being destroyed (release engine resources).
        internal virtual void EngineOnDestroy() { }
    }

    public partial class Component
    {
        internal ComponentNative ComponentNative => (ComponentNative)NativeOrThrow;

        /// Engine hooks for engine-implemented components (cameras, renderers, canvases…).
        internal virtual void EngineAwake() { }
        internal virtual void EngineOnEnable() { }
        internal virtual void EngineOnDisable() { }

        public GameObject gameObject => (GameObject)ComponentNative.GameObject.Managed;
        public Transform transform => (Transform)ComponentNative.GameObject.Components[0];

        public string tag
        {
            get => gameObject.tag;
            set => gameObject.tag = value;
        }

        public bool CompareTag(string tag) => gameObject.CompareTag(tag);

        public Component GetComponent(Type type) => gameObject.GetComponent(type);
        public T GetComponent<T>() => (T)(object)Components.First(ComponentNative.GameObject, typeof(T));
        public Component GetComponent(string type) => gameObject.GetComponent(type);

        public bool TryGetComponent(Type type, out Component component) => gameObject.TryGetComponent(type, out component);
        public bool TryGetComponent<T>(out T component) => gameObject.TryGetComponent(out component);

        public Component GetComponentInChildren(Type t, bool includeInactive) => gameObject.GetComponentInChildren(t, includeInactive);
        public Component GetComponentInChildren(Type t) => gameObject.GetComponentInChildren(t, false);
        public T GetComponentInChildren<T>(bool includeInactive) => gameObject.GetComponentInChildren<T>(includeInactive);
        public T GetComponentInChildren<T>() => gameObject.GetComponentInChildren<T>(false);

        public Component[] GetComponentsInChildren(Type t, bool includeInactive) => gameObject.GetComponentsInChildren(t, includeInactive);
        public Component[] GetComponentsInChildren(Type t) => gameObject.GetComponentsInChildren(t, false);
        public T[] GetComponentsInChildren<T>(bool includeInactive) => gameObject.GetComponentsInChildren<T>(includeInactive);
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> result) => gameObject.GetComponentsInChildren(includeInactive, result);
        public T[] GetComponentsInChildren<T>() => gameObject.GetComponentsInChildren<T>(false);
        public void GetComponentsInChildren<T>(List<T> results) => gameObject.GetComponentsInChildren(false, results);

        public Component GetComponentInParent(Type t, bool includeInactive) => gameObject.GetComponentInParent(t, includeInactive);
        public Component GetComponentInParent(Type t) => gameObject.GetComponentInParent(t, false);
        public T GetComponentInParent<T>(bool includeInactive) => gameObject.GetComponentInParent<T>(includeInactive);
        public T GetComponentInParent<T>() => gameObject.GetComponentInParent<T>(false);

        public Component[] GetComponentsInParent(Type t, bool includeInactive) => gameObject.GetComponentsInParent(t, includeInactive);
        public Component[] GetComponentsInParent(Type t) => gameObject.GetComponentsInParent(t, false);
        public T[] GetComponentsInParent<T>(bool includeInactive) => gameObject.GetComponentsInParent<T>(includeInactive);
        public void GetComponentsInParent<T>(bool includeInactive, List<T> results) => gameObject.GetComponentsInParent(includeInactive, results);
        public T[] GetComponentsInParent<T>() => gameObject.GetComponentsInParent<T>(false);

        public Component[] GetComponents(Type type) => gameObject.GetComponents(type);
        public void GetComponents(Type type, List<Component> results) => gameObject.GetComponents(type, results);
        public void GetComponents<T>(List<T> results) => gameObject.GetComponents(results);
        public T[] GetComponents<T>() => gameObject.GetComponents<T>();

        public int GetComponentIndex() => gameObject.GetComponentIndex(this);

        public void SendMessage(string methodName, object value, SendMessageOptions options) => gameObject.SendMessage(methodName, value, options);
        public void SendMessage(string methodName) => gameObject.SendMessage(methodName, null, SendMessageOptions.RequireReceiver);
        public void SendMessage(string methodName, object value) => gameObject.SendMessage(methodName, value, SendMessageOptions.RequireReceiver);
        public void SendMessage(string methodName, SendMessageOptions options) => gameObject.SendMessage(methodName, options);
        public void BroadcastMessage(string methodName, object parameter, SendMessageOptions options) => gameObject.BroadcastMessage(methodName, parameter, options);
        public void BroadcastMessage(string methodName, SendMessageOptions options) => gameObject.BroadcastMessage(methodName, options);
    }
}
