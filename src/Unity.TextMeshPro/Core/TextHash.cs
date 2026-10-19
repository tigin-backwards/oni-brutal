namespace TMPro
{
    // Hashes of names for this package's own lookups (assets by name, tags' values). Not
    // TextMeshPro's serialized hash codes: those are only ever compared with each other.
    internal static class TextHash
    {
        public static int Of(string s)
        {
            if (s == null) return 0;
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return (int)h;
            }
        }
    }
}
