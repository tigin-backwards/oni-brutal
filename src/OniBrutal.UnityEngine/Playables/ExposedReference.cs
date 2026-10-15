namespace UnityEngine
{
    public partial struct ExposedReference<T>
    {
        [SerializeField]
        public PropertyName exposedName;

        [SerializeField]
        public Object defaultValue;

        /// The resolver's value for `exposedName` if the resolver knows the name; otherwise the
        /// default value. Either is returned only if it is a T (`as` cast).
        public T Resolve(IExposedPropertyTable resolver)
        {
            if (resolver != null)
            {
                Object value = resolver.GetReferenceValue(exposedName, out bool idValid);
                if (idValid) return value as T;
            }
            return defaultValue as T;
        }
    }
}
