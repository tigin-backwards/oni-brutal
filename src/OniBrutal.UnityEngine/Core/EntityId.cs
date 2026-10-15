namespace UnityEngine
{
    /// Unity 6's object identifier; wraps the instance id.
    public partial struct EntityId
    {
        [SerializeField] private int m_Data;

        public static EntityId None => default;

        internal static EntityId From(int input) => new EntityId { m_Data = input };

        public bool IsValid() => m_Data != 0;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public bool Equals(EntityId other) => m_Data == other.m_Data;
        public bool Equals(int other) => m_Data == other;
        public int CompareTo(EntityId other) => m_Data.CompareTo(other.m_Data);

        /// Robert Jenkins' 32-bit integer mix, the hash Unity uses for EntityId.
        public override int GetHashCode()
        {
            uint a = (uint)m_Data;
            a = (a + 0x7ed55d16) + (a << 12);
            a = (a ^ 0xc761c23c) ^ (a >> 19);
            a = (a + 0x165667b1) + (a << 5);
            a = (a + 0xd3a2646c) ^ (a << 9);
            a = (a + 0xfd7046c5) + (a << 3);
            a = (a ^ 0xb55a4f09) ^ (a >> 16);
            return (int)a;
        }

        public static bool operator ==(EntityId left, EntityId right) => left.m_Data == right.m_Data;
        public static bool operator !=(EntityId left, EntityId right) => left.m_Data != right.m_Data;
        public static bool operator <(EntityId left, EntityId right) => left.m_Data < right.m_Data;
        public static bool operator >(EntityId left, EntityId right) => left.m_Data > right.m_Data;
        public static bool operator <=(EntityId left, EntityId right) => left.m_Data <= right.m_Data;
        public static bool operator >=(EntityId left, EntityId right) => left.m_Data >= right.m_Data;

        public static implicit operator int(EntityId entityId) => entityId.m_Data;
        public static implicit operator EntityId(int intValue) => new EntityId { m_Data = intValue };

        public override string ToString() => m_Data.ToString();
        public string ToString(string format) => m_Data.ToString(format);
    }
}
