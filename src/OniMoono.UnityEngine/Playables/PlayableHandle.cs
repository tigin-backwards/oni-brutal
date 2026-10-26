using System;
using OniMoono.Engine.Playables;

namespace UnityEngine.Playables
{
    /// A versioned reference to a playable owned by a PlayableGraph. It stops being valid when the
    /// playable or its graph is destroyed. Operations other than IsValid on an invalid handle throw.
    public partial struct PlayableHandle
    {
        internal IntPtr m_Handle;
        internal uint m_Version;

        private static readonly PlayableHandle m_Null = default(PlayableHandle);

        public static PlayableHandle Null => m_Null;

        public static bool operator ==(PlayableHandle x, PlayableHandle y) => CompareVersion(x, y);

        public static bool operator !=(PlayableHandle x, PlayableHandle y) => !CompareVersion(x, y);

        public override bool Equals(object p) => p is PlayableHandle other && Equals(other);

        public bool Equals(PlayableHandle other) => CompareVersion(this, other);

        public override int GetHashCode() => m_Handle.GetHashCode() ^ m_Version.GetHashCode();

        internal static bool CompareVersion(PlayableHandle lhs, PlayableHandle rhs)
            => lhs.m_Handle == rhs.m_Handle && lhs.m_Version == rhs.m_Version;

        PlayableNode Node => PlayableGraphState.Get(this);

        internal bool IsValid() => PlayableGraphState.Resolve(this) != null;

        /// The script instance as T, or null if the handle is invalid or has no script instance.
        internal T GetObject<T>() where T : class, IPlayableBehaviour
        {
            if (!IsValid()) return null;
            object instance = Node.ScriptInstance;
            return instance == null ? null : (T)instance;
        }

        /// Attaches a script object to the playable; an IPlayableBehaviour receives
        /// OnPlayableCreate immediately and the graph callbacks from then on.
        internal void SetScriptInstance(object scriptInstance)
        {
            var node = Node;
            node.ScriptInstance = scriptInstance;
            if (scriptInstance is IPlayableBehaviour behaviour)
            {
                try
                {
                    behaviour.OnPlayableCreate(new Playable(this));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// The behaviour type for script playables, otherwise the playable's struct type; null for
        /// an invalid handle.
        internal Type GetPlayableType() => PlayableGraphState.Resolve(this)?.PlayableType;

        internal bool IsPlayableOfType<T>() => GetPlayableType() == typeof(T);

        internal PlayableGraph GetGraph() => Node.Graph.AsGraph;

        internal PlayState GetPlayState() => Node.PlayState;

        internal void Play() => Node.PlayState = PlayState.Playing;

        internal void Pause() => Node.PlayState = PlayState.Paused;

        internal double GetSpeed() => Node.Speed;

        internal void SetSpeed(double value) => Node.Speed = value;

        internal double GetTime() => Node.Time;

        internal void SetTime(double value) => Node.SetTime(value);

        internal double GetPreviousTime() => Node.PreviousTime;

        internal bool IsDone() => Node.Done;

        internal void SetDone(bool value) => Node.Done = value;

        internal double GetDuration() => Node.Duration;

        internal void SetDuration(double value) => Node.Duration = value;

        internal bool GetPropagateSetTime() => Node.PropagateSetTime;

        internal void SetPropagateSetTime(bool value) => Node.PropagateSetTime = value;

        internal PlayableTraversalMode GetTraversalMode() => Node.TraversalMode;

        internal void SetTraversalMode(PlayableTraversalMode mode) => Node.TraversalMode = mode;

        internal DirectorWrapMode GetTimeWrapMode() => Node.WrapMode;

        internal void SetTimeWrapMode(DirectorWrapMode mode) => Node.WrapMode = mode;

        internal int GetInputCount() => Node.Inputs.Count;

        internal void SetInputCount(int value) => PlayableGraphState.SetInputCount(Node, value);

        internal int GetOutputCount() => Node.Outputs.Count;

        internal void SetOutputCount(int value) => PlayableGraphState.SetOutputCount(Node, value);

        /// The playable connected to `inputPort`, or Playable.Null if the port is unconnected or
        /// out of range.
        internal Playable GetInput(int inputPort)
        {
            var node = Node;
            if (inputPort < 0 || inputPort >= node.Inputs.Count) return Playable.Null;
            var source = node.Inputs[inputPort].Source;
            return source == null ? Playable.Null : source.AsPlayable;
        }

        internal Playable GetOutput(int outputPort)
        {
            var node = Node;
            if (outputPort < 0 || outputPort >= node.Outputs.Count) return Playable.Null;
            var destination = node.Outputs[outputPort].Destination;
            return destination == null ? Playable.Null : destination.AsPlayable;
        }

        /// The output port of the playable connected to `inputPort`, or -1.
        internal int GetOutputPortFromInputConnection(int inputPort)
        {
            var node = Node;
            if (inputPort < 0 || inputPort >= node.Inputs.Count || node.Inputs[inputPort].Source == null) return -1;
            return node.Inputs[inputPort].SourcePort;
        }

        /// The input port of the playable connected to `outputPort`, or -1.
        internal int GetInputPortFromOutputConnection(int outputPort)
        {
            var node = Node;
            if (outputPort < 0 || outputPort >= node.Outputs.Count || node.Outputs[outputPort].Destination == null) return -1;
            return node.Outputs[outputPort].DestinationPort;
        }

        internal bool SetInputWeight(int inputIndex, float weight)
        {
            if (!CheckInputBounds(inputIndex)) return false;
            Node.Inputs[inputIndex].Weight = weight;
            return true;
        }

        /// Sets the weight of the input port that `input` is connected to; an error if `input` is
        /// not an input of this playable.
        internal void SetInputWeight(PlayableHandle input, float weight)
        {
            var node = Node;
            var source = PlayableGraphState.Get(input);
            foreach (var port in node.Inputs)
            {
                if (port.Source == source)
                {
                    port.Weight = weight;
                    return;
                }
            }
            throw new ArgumentException("The playable is not connected as an input of this playable.", nameof(input));
        }

        internal float GetInputWeight(int inputIndex)
            => CheckInputBounds(inputIndex) ? Node.Inputs[inputIndex].Weight : 0f;

        internal void Destroy() => GetGraph().DestroyPlayable(new Playable(this));

        internal bool CheckInputBounds(int inputIndex) => CheckInputBounds(inputIndex, acceptAny: false);

        internal bool CheckInputBounds(int inputIndex, bool acceptAny)
        {
            if (inputIndex == -1 && acceptAny) return true;
            if (inputIndex < 0) throw new IndexOutOfRangeException("Index must be greater than 0");
            int count = GetInputCount();
            if (count <= inputIndex)
                throw new IndexOutOfRangeException("inputIndex " + inputIndex + " is greater than the number of available inputs (" + count + ").");
            return true;
        }
    }
}
