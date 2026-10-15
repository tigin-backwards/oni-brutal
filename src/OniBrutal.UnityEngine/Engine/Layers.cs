namespace OniBrutal.Engine
{
    /// The 32 layer names from the project's TagManager (globalgamemanagers). Installed once at
    /// startup by the asset layer, read from any thread afterwards.
    internal static class Layers
    {
        static string[] names = new string[32];

        public static void Install(string[] layerNames)
        {
            var copy = new string[32];
            for (int i = 0; i < 32 && i < layerNames.Length; i++) copy[i] = layerNames[i] ?? "";
            names = copy;
        }

        public static string NameOf(int layer) => layer >= 0 && layer < 32 ? names[layer] ?? "" : "";

        public static int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            var n = names;
            for (int i = 0; i < n.Length; i++)
                if (n[i] == name) return i;
            return -1;
        }
    }
}
