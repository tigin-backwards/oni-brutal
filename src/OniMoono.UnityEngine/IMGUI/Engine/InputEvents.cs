using System.Collections.Generic;
using UnityEngine;

namespace OniMoono.Engine.IMGUI
{
    /// The frame's input events. Each queued event goes to two independent places: the queue
    /// that Event.PopEvent reads (uGUI's and TextMeshPro's input fields), and the list the
    /// frame's OnGUI pass sends to the behaviours. Popping does not take an event from OnGUI.
    internal static class InputEvents
    {
        static readonly Queue<Event> pending = new();
        static List<Event> forGUI = new();
        static List<Event> spare = new();

        public static void Add(Event e)
        {
            Threads.AssertMainThread("Event.Enqueue");
            pending.Enqueue(new Event(e));
            forGUI.Add(new Event(e));
        }

        public static Event Pop() => pending.Count > 0 ? pending.Dequeue() : null;

        public static int PendingCount => pending.Count;

        /// The input events for this frame's OnGUI pass; the list is reused by the next call.
        public static List<Event> TakeForGUI()
        {
            (forGUI, spare) = (spare, forGUI);
            forGUI.Clear();
            return spare;
        }

        /// Events nobody popped this frame are dropped: the next frame starts empty.
        public static void EndFrame() => pending.Clear();

        public static void Reset()
        {
            pending.Clear();
            forGUI.Clear();
            spare.Clear();
        }
    }
}
