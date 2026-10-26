namespace OniMoono.Engine
{
    /// `string.GetHashCode` as Unity's Mono computes it on 64-bit players: the .NET Framework
    /// reference-source algorithm without randomization. Two djb2-style lanes over alternating
    /// characters, stopping at the first NUL, combined as hash1 + hash2 * 1566083941. CoreCLR's
    /// own string hash is randomized per process; the game persists this value (ARCHITECTURE D3),
    /// so the host computes it with Mono's algorithm there. Verified against the original player
    /// with tools/UnityProbe (VAL strhash).
    internal static class MonoStringHash
    {
        public static int Compute(string text)
        {
            int hash1 = 5381;
            int hash2 = hash1;
            for (int i = 0; i < text.Length; i += 2)
            {
                int c = text[i];
                if (c == 0) break;
                hash1 = ((hash1 << 5) + hash1) ^ c;
                if (i + 1 >= text.Length) break;
                c = text[i + 1];
                if (c == 0) break;
                hash2 = ((hash2 << 5) + hash2) ^ c;
            }
            return hash1 + hash2 * 1566083941;
        }
    }
}
