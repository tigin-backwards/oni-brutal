using System;
using System.Threading;

namespace OniBrutal.Engine
{
    /// The engine's "native" object table. A UnityEngine.Object stores only a handle into it
    /// (in m_CachedPtr), like Unity's managed wrappers store a pointer to the native object.
    ///
    /// A handle packs (slot index, generation). Lookups are lock-free and safe from any thread:
    /// a destroyed object's slot is cleared or reused with a new generation, so a stale handle
    /// resolves to null instead of to the wrong object. Mutation happens on the main thread
    /// (object creation may also happen on the loading thread under the table lock).
    internal static class ObjectTable
    {
        static readonly object gate = new();
        static NativeObject[] slots = new NativeObject[4096];
        static int[] freeSlots = new int[256];
        static int freeCount;
        static int highWater = 1; // slot 0 is never used, so a zero handle means "none"
        static int nextGeneration;
        static int nextRuntimeId;   // runtime-created objects: negative ids, like Unity
        static int nextPersistentId; // objects loaded from files: positive ids

        public static IntPtr Register(NativeObject obj)
        {
            lock (gate)
            {
                int index;
                if (freeCount > 0) index = freeSlots[--freeCount];
                else
                {
                    index = highWater++;
                    if (index >= slots.Length)
                    {
                        var grown = new NativeObject[slots.Length * 2];
                        Array.Copy(slots, grown, slots.Length);
                        Volatile.Write(ref slots, grown);
                    }
                }
                obj.Generation = ++nextGeneration;
                obj.Slot = index;
                Volatile.Write(ref slots[index], obj);
                return Pack(index, obj.Generation);
            }
        }

        public static void Unregister(NativeObject obj)
        {
            lock (gate)
            {
                var table = slots;
                if (obj.Slot <= 0 || !ReferenceEquals(table[obj.Slot], obj)) return;
                Volatile.Write(ref table[obj.Slot], null);
                if (freeCount == freeSlots.Length) Array.Resize(ref freeSlots, freeSlots.Length * 2);
                freeSlots[freeCount++] = obj.Slot;
                obj.Slot = 0;
            }
        }

        public static NativeObject Resolve(IntPtr handle)
        {
            long h = (long)handle;
            if (h == 0) return null;
            int index = (int)(h & 0xFFFFFFFF);
            int generation = (int)(h >> 32);
            var table = Volatile.Read(ref slots);
            if ((uint)index >= (uint)table.Length) return null;
            var obj = Volatile.Read(ref table[index]);
            return obj != null && obj.Generation == generation ? obj : null;
        }

        static IntPtr Pack(int index, int generation) => (IntPtr)(((long)generation << 32) | (uint)index);

        public static int NewInstanceId(bool persistent)
            => persistent ? Interlocked.Increment(ref nextPersistentId) : Interlocked.Decrement(ref nextRuntimeId);

        /// Snapshot of all live objects (for FindObjectsOfTypeAll and friends).
        public static NativeObject[] Snapshot()
        {
            lock (gate)
            {
                var result = new System.Collections.Generic.List<NativeObject>(highWater);
                for (int i = 1; i < highWater; i++)
                {
                    var o = slots[i];
                    if (o != null) result.Add(o);
                }
                return result.ToArray();
            }
        }
    }
}
