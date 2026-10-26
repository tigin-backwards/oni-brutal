using System;
using OniMoono.Engine.Playables;

namespace UnityEngine.Playables
{
    /// A versioned reference to a graph (OniMoono.Engine.Playables.PlayableGraphState documents
    /// the evaluation rules). Every method except IsValid throws on an invalid graph.
    ///
    /// Play/Stop are deliberately absent: only graphs owned by a PlayableDirector are advanced
    /// by the director update (PlayableDirectorNative).
    public partial struct PlayableGraph
    {
        internal IntPtr m_Handle;
        internal uint m_Version;

        PlayableGraphState State => PlayableGraphState.Get(this);

        public static PlayableGraph Create() => Create(null);

        public static PlayableGraph Create(string name) => PlayableGraphState.Create(name);

        /// Stops the graph (OnBehaviourPause, OnGraphStop), destroys every playable
        /// (OnPlayableDestroy) and output, and invalidates the graph.
        public void Destroy() => State.Destroy();

        public bool IsValid() => PlayableGraphState.Resolve(this) != null;

        public bool IsPlaying() => State.Playing;

        public void Evaluate() => Evaluate(0f);

        /// Evaluates the graph once, advancing playing playables by `deltaTime` seconds
        /// (FrameData.evaluationType is Evaluate).
        public void Evaluate(float deltaTime) => State.Evaluate(deltaTime, FrameData.EvaluationType.Evaluate);

        /// Timeline's nested directors: after evaluating this graph from within `playable`'s
        /// evaluation, this graph's evaluation belongs to that graph, so this graph's own
        /// director stops advancing it while the synchronization is renewed every frame.
        internal void SynchronizeEvaluation(PlayableGraph playable)
        {
            var state = State;
            PlayableGraphState.Get(playable);
            state.SynchronizedFrame = Time.frameCount;
        }

        public IExposedPropertyTable GetResolver() => State.Resolver;

        public void SetResolver(IExposedPropertyTable value) => State.Resolver = value;

        public int GetPlayableCount() => State.PlayableCount;

        public int GetRootPlayableCount() => State.RootCount;

        /// The index-th playable with no connected output port, in creation order; Playable.Null
        /// if out of range.
        public Playable GetRootPlayable(int index)
        {
            var root = State.GetRoot(index);
            return root == null ? Playable.Null : root.AsPlayable;
        }

        /// Connects output port `sourceOutputPort` of `source` to input port
        /// `destinationInputPort` of `destination`. Logs an error and returns false if a port is
        /// out of range or already connected, or the connection would create a cycle.
        public bool Connect<U, V>(U source, int sourceOutputPort, V destination, int destinationInputPort)
            where U : struct, IPlayable
            where V : struct, IPlayable
        {
            var state = State;
            return state.Connect(PlayableGraphState.Get(source.GetHandle()), sourceOutputPort,
                PlayableGraphState.Get(destination.GetHandle()), destinationInputPort);
        }

        public void Disconnect<U>(U input, int inputPort) where U : struct, IPlayable
        {
            _ = State;
            PlayableGraphState.Disconnect(PlayableGraphState.Get(input.GetHandle()), inputPort);
        }

        public void DestroyPlayable<U>(U playable) where U : struct, IPlayable
            => State.DestroyPlayable(OwnNode(playable.GetHandle()));

        public void DestroySubgraph<U>(U playable) where U : struct, IPlayable
            => State.DestroySubgraph(OwnNode(playable.GetHandle()));

        public void DestroyOutput<U>(U output) where U : struct, IPlayableOutput
            => DestroyOutputInternal(output.GetHandle());

        public int GetOutputCount() => State.OutputCount;

        public PlayableOutput GetOutput(int index)
        {
            var output = State.GetOutput(index);
            return output == null ? PlayableOutput.Null : new PlayableOutput(output.Handle);
        }

        public int GetOutputCountByType<T>() where T : struct, IPlayableOutput => State.OutputCountOfKind(typeof(T));

        public PlayableOutput GetOutputByType<T>(int index) where T : struct, IPlayableOutput
        {
            var output = State.GetOutputOfKind(typeof(T), index);
            return output == null ? PlayableOutput.Null : new PlayableOutput(output.Handle);
        }

        internal PlayableHandle CreatePlayableHandle() => State.CreatePlayable(typeof(Playable)).Handle;

        internal void DestroyOutputInternal(PlayableOutputHandle handle)
        {
            var state = State;
            var output = PlayableGraphState.Get(handle);
            if (output.Graph != state) throw new ArgumentException("The PlayableOutput does not belong to this PlayableGraph.");
            state.DestroyOutput(output);
        }

        internal bool IsMatchFrameRateEnabled() => State.MatchFrameRate;

        internal void EnableMatchFrameRate(FrameRate frameRate)
        {
            var state = State;
            state.MatchFrameRate = true;
            state.FrameRate = frameRate;
        }

        internal void DisableMatchFrameRate() => State.MatchFrameRate = false;

        internal FrameRate GetFrameRate() => State.FrameRate;

        PlayableNode OwnNode(PlayableHandle handle)
        {
            var state = State;
            var node = PlayableGraphState.Get(handle);
            if (node.Graph != state) throw new ArgumentException("The Playable does not belong to this PlayableGraph.");
            return node;
        }
    }
}
