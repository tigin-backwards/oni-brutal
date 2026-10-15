using System;
using System.Collections.Generic;

namespace OniBrutal.Engine.Playables
{
    /// Gives engine records the (pointer, version) identity Unity's playable handles carry
    /// (`m_Handle`, `m_Version`). The pointer is a 1-based slot index, so `default` handles never
    /// resolve; removing a record bumps the slot's version, so stale copies of a handle stop
    /// resolving even after the slot is reused.
    internal sealed class HandleTable<T> where T : class
    {
        readonly object gate = new object();
        T[] items = new T[16];
        uint[] versions = new uint[16];
        readonly Stack<int> freeSlots = new Stack<int>();
        int used;

        public void Add(T item, out IntPtr ptr, out uint version)
        {
            lock (gate)
            {
                int slot;
                if (freeSlots.Count > 0) slot = freeSlots.Pop();
                else
                {
                    if (used == items.Length)
                    {
                        Array.Resize(ref items, used * 2);
                        Array.Resize(ref versions, used * 2);
                    }
                    slot = used++;
                }
                items[slot] = item;
                versions[slot]++;
                ptr = (IntPtr)(slot + 1);
                version = versions[slot];
            }
        }

        public T Resolve(IntPtr ptr, uint version)
        {
            long slot = (long)ptr - 1;
            lock (gate)
            {
                if (slot < 0 || slot >= used || versions[slot] != version) return null;
                return items[slot];
            }
        }

        public void Remove(IntPtr ptr, uint version)
        {
            long slot = (long)ptr - 1;
            lock (gate)
            {
                if (slot < 0 || slot >= used || versions[slot] != version || items[slot] == null) return;
                items[slot] = null;
                versions[slot]++;
                freeSlots.Push((int)slot);
            }
        }
    }
}
