using OniMoono.Engine.Playables;

namespace UnityEngine
{
    public partial struct PropertyName
    {
        // Index in the global property-name table; 0 is the null/empty name.
        internal int id;

        public PropertyName(string name) : this(PropertyNames.Intern(name)) { }

        public PropertyName(PropertyName other) { id = other.id; }

        public PropertyName(int id) { this.id = id; }

        public static bool IsNullOrEmpty(PropertyName prop) => prop.id == 0;

        public static bool operator ==(PropertyName lhs, PropertyName rhs) => lhs.id == rhs.id;

        public static bool operator !=(PropertyName lhs, PropertyName rhs) => lhs.id != rhs.id;

        public override int GetHashCode() => id;

        public override bool Equals(object other) => other is PropertyName name && Equals(name);

        public bool Equals(PropertyName other) => id == other.id;

        public static implicit operator PropertyName(string name) => new PropertyName(name);

        public static implicit operator PropertyName(int id) => new PropertyName(id);

        // A player build keeps no reverse string table for ToString, so Unity prints "Unknown:<id>".
        public override string ToString() => $"Unknown:{id}";
    }
}
