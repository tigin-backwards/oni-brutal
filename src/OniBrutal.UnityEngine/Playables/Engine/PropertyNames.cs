using System.Collections.Generic;

namespace OniBrutal.Engine.Playables
{
    /// Unity's global property-name table: every distinct string gets one id for the lifetime of
    /// the process; null and "" map to 0 (`PropertyName.IsNullOrEmpty`). Ids are positive.
    /// Thread-safe: asset loading creates PropertyNames off the main thread.
    internal static class PropertyNames
    {
        static readonly object gate = new object();
        static readonly Dictionary<string, int> ids = new Dictionary<string, int>(System.StringComparer.Ordinal);
        static readonly List<string> names = new List<string> { "" };

        public static int Intern(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            lock (gate)
            {
                if (ids.TryGetValue(name, out int id)) return id;
                id = names.Count;
                names.Add(name);
                ids.Add(name, id);
                return id;
            }
        }

        /// The string an id was interned from, or null for ids not created from a string.
        public static string NameOf(int id)
        {
            lock (gate)
                return id >= 0 && id < names.Count ? names[id] : null;
        }
    }
}
