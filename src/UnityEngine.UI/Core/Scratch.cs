using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// Reusable lists for the per-frame component queries (layout, mesh and material modifiers,
    /// raycast filters). Calls may nest (a layout element's size can read other layouts), so each
    /// taker gets its own list and gives it back.
    internal static class Scratch<T>
    {
        static readonly Stack<List<T>> spare = new();

        public static List<T> Take() => spare.Count > 0 ? spare.Pop() : new List<T>();

        public static void Return(List<T> list)
        {
            list.Clear();
            spare.Push(list);
        }
    }
}
