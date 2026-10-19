using System;
using System.Collections.Generic;

namespace TMPro
{
    // An ordered set of delegates: each delegate once, called in the order it was added.
    public partial class FastAction<A>
    {
        readonly LinkedList<Action<A>> m_Delegates = new();
        readonly Dictionary<Action<A>, LinkedListNode<Action<A>>> m_Nodes = new();

        public void Add(Action<A> rhs)
        {
            if (rhs == null || m_Nodes.ContainsKey(rhs)) return;
            m_Nodes[rhs] = m_Delegates.AddLast(rhs);
        }

        public void Remove(Action<A> rhs)
        {
            if (rhs == null || !m_Nodes.Remove(rhs, out var node)) return;
            m_Delegates.Remove(node);
        }

        public void Call(A a)
        {
            // A delegate may remove itself (or others) while it runs.
            var node = m_Delegates.First;
            while (node != null)
            {
                var next = node.Next;
                node.Value(a);
                node = next;
            }
        }
    }
}
