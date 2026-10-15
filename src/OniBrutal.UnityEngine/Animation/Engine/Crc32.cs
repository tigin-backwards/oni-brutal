using System.Text;

namespace OniBrutal.Engine.Mecanim
{
    /// The CRC-32 (IEEE 802.3, reflected, as zlib) Unity uses for animation name hashes:
    /// Animator.StringToHash, state/parameter/layer IDs, binding paths and attributes.
    internal static class Crc32
    {
        static readonly uint[] table = Build();

        static uint[] Build()
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

        /// The hash of a string's UTF-8 bytes; "" hashes to 0.
        public static uint Of(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }
    }
}
