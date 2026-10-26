using System;
using OniMoono.Engine.Playables;

namespace UnityEngine.Playables
{
    /// A versioned reference to an output owned by a PlayableGraph. Operations other than IsValid
    /// on an invalid handle throw.
    public partial struct PlayableOutputHandle
    {
        internal IntPtr m_Handle;
        internal uint m_Version;

        private static readonly PlayableOutputHandle m_Null = default(PlayableOutputHandle);

        public static PlayableOutputHandle Null => m_Null;

        public override int GetHashCode() => m_Handle.GetHashCode() ^ m_Version.GetHashCode();

        public static bool operator ==(PlayableOutputHandle lhs, PlayableOutputHandle rhs) => CompareVersion(lhs, rhs);

        public static bool operator !=(PlayableOutputHandle lhs, PlayableOutputHandle rhs) => !CompareVersion(lhs, rhs);

        public override bool Equals(object p) => p is PlayableOutputHandle other && Equals(other);

        public bool Equals(PlayableOutputHandle other) => CompareVersion(this, other);

        internal static bool CompareVersion(PlayableOutputHandle lhs, PlayableOutputHandle rhs)
            => lhs.m_Handle == rhs.m_Handle && lhs.m_Version == rhs.m_Version;

        OutputNode Node => PlayableGraphState.Get(this);

        internal bool IsValid() => PlayableGraphState.Resolve(this) != null;

        /// The output's struct type (AnimationPlayableOutput, AudioPlayableOutput, …); null for an
        /// invalid handle.
        internal Type GetPlayableOutputType() => PlayableGraphState.Resolve(this)?.Kind;

        internal bool IsPlayableOutputOfType<T>() => GetPlayableOutputType() == typeof(T);

        internal Object GetReferenceObject() => Node.ReferenceObject;

        internal void SetReferenceObject(Object target) => Node.ReferenceObject = target;

        internal Object GetUserData() => Node.UserData;

        internal void SetUserData(Object target) => Node.UserData = target;

        internal PlayableHandle GetSourcePlayable()
        {
            var source = Node.Source;
            return source == null || !source.Alive ? PlayableHandle.Null : source.Handle;
        }

        /// Makes `target` the root this output evaluates; `port` selects the input a Passthrough
        /// root forwards to.
        internal void SetSourcePlayable(PlayableHandle target, int port)
        {
            var node = Node;
            if (target == PlayableHandle.Null)
            {
                node.Source = null;
                node.SourcePort = port;
                return;
            }
            var source = PlayableGraphState.Get(target);
            if (source.Graph != node.Graph) throw new ArgumentException("The Playable does not belong to the PlayableGraph of this output.", nameof(target));
            node.Source = source;
            node.SourcePort = port;
        }

        internal int GetSourceOutputPort() => Node.SourcePort;

        internal float GetWeight() => Node.Weight;

        internal void SetWeight(float weight) => Node.Weight = weight;

        internal void PushNotification(PlayableHandle origin, INotification notification, object context)
        {
            var node = Node;
            node.Graph.PushNotification(node, origin, notification, context);
        }

        internal INotificationReceiver[] GetNotificationReceivers() => Node.Receivers.ToArray();

        internal void AddNotificationReceiver(INotificationReceiver receiver)
        {
            var node = Node;
            if (receiver != null && !node.Receivers.Contains(receiver)) node.Receivers.Add(receiver);
        }

        internal void RemoveNotificationReceiver(INotificationReceiver receiver) => Node.Receivers.Remove(receiver);
    }
}
