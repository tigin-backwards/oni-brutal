using System;

namespace OniBrutal.Engine
{
    /// CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320), incremental.
    internal sealed class Crc32
    {
        static readonly uint[] table = BuildTable();

        uint state = 0xFFFFFFFFu;

        static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            return t;
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            uint c = state;
            foreach (byte b in data) c = table[(c ^ b) & 0xFF] ^ (c >> 8);
            state = c;
        }

        public uint Current => ~state;

        /// The hash of a string's UTF-8 bytes, as Unity's name hashes (Animator.StringToHash,
        /// animation binding paths); "" hashes to 0.
        public static uint Of(string s)
            => string.IsNullOrEmpty(s) ? 0 : Compute(System.Text.Encoding.UTF8.GetBytes(s));

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = new Crc32();
            crc.Append(data);
            return crc.Current;
        }
    }
}
