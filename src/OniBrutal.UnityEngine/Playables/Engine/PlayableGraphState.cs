using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace OniBrutal.Engine.Playables
{
    /// The native side of a PlayableGraph: owns its playables and outputs, connects them and
    /// evaluates them the way Unity's director runtime does:
    ///
    /// - An evaluation first runs a prepare pass over every output (in creation order), from the
    ///   output's source playable towards the leaves; then a process pass over every output.
    /// - Prepare visits each playable once per evaluation: it advances local time by
    ///   deltaTime × effective speed (the product of the speeds on the path), applies the time wrap
    ///   mode, delivers OnGraphStart (first visit), OnBehaviourPlay / OnBehaviourPause (when the
    ///   effective play state changes; a playable is effectively playing when it and every
    ///   playable on the path above it are playing), then PrepareFrame, then visits its inputs.
    ///   Paused subtrees do not advance and get no PrepareFrame/ProcessFrame.
    /// - Process calls ProcessFrame on effectively playing playables, inputs before the playable
    ///   that mixes them, once per output, with the output's user data as playerData.
    /// - A playable in Passthrough traversal mode reached through its output port n visits only its
    ///   input n; in Mix mode it visits every input.
    /// - Notifications pushed during an evaluation are delivered when it ends, in push order.
    ///
    /// Playables API calls are main-thread operations in Unity; the graph is not synchronized.
    internal sealed class PlayableGraphState
    {
        static readonly HandleTable<PlayableGraphState> graphs = new HandleTable<PlayableGraphState>();
        static readonly HandleTable<PlayableNode> nodes = new HandleTable<PlayableNode>();
        static readonly HandleTable<OutputNode> outputs = new HandleTable<OutputNode>();

        public readonly string Name;
        public IntPtr Ptr;
        public uint Version;

        readonly List<PlayableNode> playables = new List<PlayableNode>();
        readonly List<OutputNode> outputList = new List<OutputNode>();
        readonly List<(OutputNode output, PlayableHandle origin, INotification notification, object context)> pendingNotifications
            = new List<(OutputNode, PlayableHandle, INotification, object)>();

        public IExposedPropertyTable Resolver;
        public bool Playing;
        public bool MatchFrameRate;
        public FrameRate FrameRate;
        bool evaluating;
        ulong frameId;

        PlayableGraphState(string name) { Name = name; }

        // ---- handles -------------------------------------------------------------------------

        public static PlayableGraph Create(string name)
        {
            var state = new PlayableGraphState(name);
            graphs.Add(state, out state.Ptr, out state.Version);
            return state.AsGraph;
        }

        public PlayableGraph AsGraph => new PlayableGraph { m_Handle = Ptr, m_Version = Version };

        public static PlayableGraphState Resolve(PlayableGraph graph) => graphs.Resolve(graph.m_Handle, graph.m_Version);

        public static PlayableGraphState Get(PlayableGraph graph)
            => Resolve(graph) ?? throw new InvalidOperationException("The PlayableGraph is invalid. It has either been destroyed or was never created.");

        public static PlayableNode Resolve(PlayableHandle handle) => nodes.Resolve(handle.m_Handle, handle.m_Version);

        public static PlayableNode Get(PlayableHandle handle)
            => Resolve(handle) ?? throw new InvalidOperationException("This Playable is invalid. It has either been destroyed or was never created.");

        public static OutputNode Resolve(PlayableOutputHandle handle) => outputs.Resolve(handle.m_Handle, handle.m_Version);

        public static OutputNode Get(PlayableOutputHandle handle)
            => Resolve(handle) ?? throw new InvalidOperationException("This PlayableOutput is invalid. It has either been destroyed or was never created.");

        // ---- structure -----------------------------------------------------------------------

        public PlayableNode CreatePlayable(Type kind, PlayablePayload payload = null)
        {
            var node = new PlayableNode(this, kind) { Payload = payload };
            nodes.Add(node, out node.Ptr, out node.Version);
            playables.Add(node);
            return node;
        }

        public OutputNode CreateOutput(Type kind, string name)
        {
            var output = new OutputNode(this, kind, name);
            outputs.Add(output, out output.Ptr, out output.Version);
            outputList.Add(output);
            return output;
        }

        public int PlayableCount => playables.Count;

        public int RootCount
        {
            get
            {
                int n = 0;
                foreach (var p in playables) if (!p.HasParent) n++;
                return n;
            }
        }

        /// Root playables are those with no connected output port, in creation order.
        public PlayableNode GetRoot(int index)
        {
            foreach (var p in playables)
                if (!p.HasParent && index-- == 0) return p;
            return null;
        }

        public int OutputCount => outputList.Count;

        public OutputNode GetOutput(int index) => index >= 0 && index < outputList.Count ? outputList[index] : null;

        public int OutputCountOfKind(Type kind)
        {
            int n = 0;
            foreach (var o in outputList) if (o.Kind == kind) n++;
            return n;
        }

        public OutputNode GetOutputOfKind(Type kind, int index)
        {
            foreach (var o in outputList)
                if (o.Kind == kind && index-- == 0) return o;
            return null;
        }

        public static void SetInputCount(PlayableNode node, int count)
        {
            if (count < 0) throw new ArgumentException("Input count cannot be negative.", nameof(count));
            while (node.Inputs.Count > count)
            {
                Disconnect(node, node.Inputs.Count - 1);
                node.Inputs.RemoveAt(node.Inputs.Count - 1);
            }
            while (node.Inputs.Count < count) node.Inputs.Add(new InputPort());
        }

        public static void SetOutputCount(PlayableNode node, int count)
        {
            if (count < 0) throw new ArgumentException("Output count cannot be negative.", nameof(count));
            while (node.Outputs.Count > count)
            {
                var port = node.Outputs[node.Outputs.Count - 1];
                if (port.Destination != null) Disconnect(port.Destination, port.DestinationPort);
                node.Outputs.RemoveAt(node.Outputs.Count - 1);
            }
            while (node.Outputs.Count < count) node.Outputs.Add(new OutputPort());
        }

        public bool Connect(PlayableNode source, int sourcePort, PlayableNode destination, int destinationPort)
        {
            if (source.Graph != this || destination.Graph != this)
            {
                Debug.LogError("Cannot connect playables that belong to different PlayableGraphs.");
                return false;
            }
            if (sourcePort < 0 || sourcePort >= source.Outputs.Count)
            {
                Debug.LogError($"Cannot connect output port {sourcePort}: the source playable has {source.Outputs.Count} output port(s).");
                return false;
            }
            if (destinationPort < 0 || destinationPort >= destination.Inputs.Count)
            {
                Debug.LogError($"Cannot connect input port {destinationPort}: the destination playable has {destination.Inputs.Count} input port(s).");
                return false;
            }
            if (source.Outputs[sourcePort].Destination != null)
            {
                Debug.LogError($"Cannot connect output port {sourcePort}: it is already connected.");
                return false;
            }
            if (destination.Inputs[destinationPort].Source != null)
            {
                Debug.LogError($"Cannot connect input port {destinationPort}: it is already connected.");
                return false;
            }
            if (source == destination || IsUpstream(destination, source))
            {
                Debug.LogError("Cannot connect playables: the connection would create a cycle.");
                return false;
            }
            source.Outputs[sourcePort].Destination = destination;
            source.Outputs[sourcePort].DestinationPort = destinationPort;
            destination.Inputs[destinationPort].Source = source;
            destination.Inputs[destinationPort].SourcePort = sourcePort;
            return true;
        }

        // True if `candidate` is reachable from `node` by following inputs.
        static bool IsUpstream(PlayableNode candidate, PlayableNode node)
        {
            foreach (var input in node.Inputs)
                if (input.Source != null && (input.Source == candidate || IsUpstream(candidate, input.Source)))
                    return true;
            return false;
        }

        /// Disconnects whatever is connected to `inputPort` of `node`; the port and its weight remain.
        public static void Disconnect(PlayableNode node, int inputPort)
        {
            if (inputPort < 0 || inputPort >= node.Inputs.Count) return;
            var input = node.Inputs[inputPort];
            if (input.Source != null && input.SourcePort < input.Source.Outputs.Count)
            {
                var port = input.Source.Outputs[input.SourcePort];
                if (port.Destination == node && port.DestinationPort == inputPort)
                {
                    port.Destination = null;
                    port.DestinationPort = 0;
                }
            }
            input.Source = null;
            input.SourcePort = 0;
        }

        public void DestroyPlayable(PlayableNode node)
        {
            if (!node.Alive) return;
            if (node.Behaviour != null) Invoke(node, b => b.OnPlayableDestroy(node.AsPlayable));
            if (!node.Alive) return;
            for (int i = 0; i < node.Inputs.Count; i++) Disconnect(node, i);
            foreach (var port in node.Outputs)
                if (port.Destination != null) Disconnect(port.Destination, port.DestinationPort);
            foreach (var o in outputList)
                if (o.Source == node) o.Source = null;
            playables.Remove(node);
            node.Alive = false;
            nodes.Remove(node.Ptr, node.Version);
        }

        public void DestroySubgraph(PlayableNode node)
        {
            var inputs = new List<PlayableNode>();
            foreach (var input in node.Inputs)
                if (input.Source != null) inputs.Add(input.Source);
            DestroyPlayable(node);
            foreach (var input in inputs)
                if (input.Alive) DestroySubgraph(input);
        }

        public void DestroyOutput(OutputNode output)
        {
            if (!output.Alive) return;
            outputList.Remove(output);
            output.Alive = false;
            outputs.Remove(output.Ptr, output.Version);
        }

        public void Destroy()
        {
            if (evaluating) throw new InvalidOperationException("A PlayableGraph cannot be destroyed while it is being evaluated.");
            StopBehaviours();
            Playing = false;
            foreach (var node in playables.ToArray())
                DestroyPlayable(node);
            foreach (var output in outputList.ToArray())
                DestroyOutput(output);
            pendingNotifications.Clear();
            graphs.Remove(Ptr, Version);
        }

        // ---- play state ----------------------------------------------------------------------

        /// Starts playback. Playing graphs are advanced by `Tick` from the director update.
        public void Play() => Playing = true;

        public void Stop()
        {
            if (!Playing) return;
            Playing = false;
            StopBehaviours();
        }

        // Pauses every behaviour that is playing (effectivePlayState Paused), then delivers
        // OnGraphStop to every playable that saw OnGraphStart.
        void StopBehaviours()
        {
            foreach (var node in playables.ToArray())
            {
                if (!node.Alive || !node.BehaviourPlaying) continue;
                node.BehaviourPlaying = false;
                var info = node.Frame;
                info.m_Flags &= ~(FrameData.Flags.EffectivePlayStatePlaying | FrameData.Flags.EffectivePlayStateDelayed);
                if (node.Behaviour != null) Invoke(node, b => b.OnBehaviourPause(node.AsPlayable, info));
            }
            foreach (var node in playables.ToArray())
            {
                if (!node.Alive || !node.GraphStarted) continue;
                node.GraphStarted = false;
                if (node.Behaviour != null) Invoke(node, b => b.OnGraphStop(node.AsPlayable));
            }
        }

        // ---- notifications -------------------------------------------------------------------

        public void PushNotification(OutputNode output, PlayableHandle origin, INotification notification, object context)
        {
            if (evaluating)
            {
                pendingNotifications.Add((output, origin, notification, context));
                return;
            }
            Deliver(output, origin, notification, context);
        }

        static void Deliver(OutputNode output, PlayableHandle origin, INotification notification, object context)
        {
            foreach (var receiver in output.Receivers.ToArray())
            {
                try
                {
                    receiver.OnNotify(new Playable(origin), notification, context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // ---- evaluation ----------------------------------------------------------------------

        /// Advances a playing graph by one frame (evaluation type Playback).
        public void Tick(double deltaTime) => Evaluate(deltaTime, FrameData.EvaluationType.Playback);

        public void Evaluate(double deltaTime, FrameData.EvaluationType type)
        {
            if (evaluating) throw new InvalidOperationException("The PlayableGraph is already being evaluated.");
            evaluating = true;
            try
            {
                frameId++;
                var outputSnapshot = outputList.ToArray();
                foreach (var output in outputSnapshot)
                {
                    if (!output.Alive || output.Source == null || !output.Source.Alive) continue;
                    var ctx = new Context
                    {
                        DeltaTime = deltaTime,
                        Type = type,
                        Output = output,
                        ParentPlaying = true,
                        ParentSpeed = 1.0,
                        Weight = 1f,
                        EffectiveWeight = output.Weight,
                        Seek = false,
                        Port = output.SourcePort,
                    };
                    Prepare(output.Source, ctx);
                }
                foreach (var output in outputSnapshot)
                {
                    if (!output.Alive || output.Source == null || !output.Source.Alive) continue;
                    Process(output.Source, output.SourcePort, output, new HashSet<PlayableNode>());
                }
            }
            finally
            {
                evaluating = false;
            }
            while (pendingNotifications.Count > 0)
            {
                var batch = pendingNotifications.ToArray();
                pendingNotifications.Clear();
                foreach (var n in batch)
                    if (n.output.Alive) Deliver(n.output, n.origin, n.notification, n.context);
            }
        }

        struct Context
        {
            public double DeltaTime;
            public FrameData.EvaluationType Type;
            public OutputNode Output;
            public bool ParentPlaying;
            public double ParentSpeed;
            public float Weight;
            public float EffectiveWeight;
            public bool Seek;
            public int Port;
        }

        void Prepare(PlayableNode node, Context ctx)
        {
            if (!node.Alive || node.PreparedFrame == frameId) return;
            node.PreparedFrame = frameId;

            bool playing = ctx.ParentPlaying && node.PlayState == PlayState.Playing;
            double speed = ctx.ParentSpeed * node.Speed;
            bool seek = ctx.Seek || node.SeekPending;
            FrameData.Flags flags = 0;
            if (node.SeekPending)
            {
                // The time was set explicitly since the last evaluation: keep it.
                node.PreviousTime = node.TimeBeforeSeek;
                node.SeekPending = false;
            }
            else if (playing)
            {
                flags |= node.Advance(ctx.DeltaTime * speed);
            }
            else
            {
                node.PreviousTime = node.Time;
            }
            if (ctx.Type == FrameData.EvaluationType.Evaluate) flags |= FrameData.Flags.Evaluate;
            if (seek) flags |= FrameData.Flags.SeekOccured;
            if (playing) flags |= FrameData.Flags.EffectivePlayStatePlaying;

            node.Frame = new FrameData
            {
                m_FrameID = frameId,
                m_DeltaTime = ctx.DeltaTime,
                m_Weight = ctx.Weight,
                m_EffectiveWeight = ctx.EffectiveWeight,
                m_EffectiveParentDelay = 0.0,
                m_EffectiveParentSpeed = (float)ctx.ParentSpeed,
                m_EffectiveSpeed = (float)speed,
                m_Flags = flags,
                m_Output = new PlayableOutput(ctx.Output.Handle),
            };
            node.EffectivelyPlaying = playing;

            if (!node.GraphStarted)
            {
                node.GraphStarted = true;
                if (node.Behaviour != null) Invoke(node, b => b.OnGraphStart(node.AsPlayable));
            }
            if (playing && !node.BehaviourPlaying)
            {
                node.BehaviourPlaying = true;
                if (node.Behaviour != null) Invoke(node, b => b.OnBehaviourPlay(node.AsPlayable, node.Frame));
            }
            else if (!playing && node.BehaviourPlaying)
            {
                node.BehaviourPlaying = false;
                if (node.Behaviour != null) Invoke(node, b => b.OnBehaviourPause(node.AsPlayable, node.Frame));
            }
            if (playing)
            {
                node.Payload?.CheckEvaluable(node, ctx.Output, ctx.EffectiveWeight);
                if (node.Behaviour != null) Invoke(node, b => b.PrepareFrame(node.AsPlayable, node.Frame));
            }
            if (!node.Alive) return;

            ForEachTraversedInput(node, ctx.Port, input =>
            {
                Prepare(input.Source, new Context
                {
                    DeltaTime = ctx.DeltaTime,
                    Type = ctx.Type,
                    Output = ctx.Output,
                    ParentPlaying = playing,
                    ParentSpeed = speed,
                    Weight = input.Weight,
                    EffectiveWeight = ctx.EffectiveWeight * input.Weight,
                    Seek = seek,
                    Port = input.SourcePort,
                });
            });
        }

        void Process(PlayableNode node, int port, OutputNode output, HashSet<PlayableNode> visited)
        {
            if (!node.Alive || node.PreparedFrame != frameId || !node.EffectivelyPlaying || !visited.Add(node)) return;
            ForEachTraversedInput(node, port, input => Process(input.Source, input.SourcePort, output, visited));
            if (!node.Alive || node.Behaviour == null) return;
            var info = node.Frame;
            info.m_Output = new PlayableOutput(output.Handle);
            object playerData = output.UserData;
            Invoke(node, b => b.ProcessFrame(node.AsPlayable, info, playerData));
        }

        static void ForEachTraversedInput(PlayableNode node, int port, Action<InputPort> visit)
        {
            if (node.TraversalMode == PlayableTraversalMode.Passthrough)
            {
                if (port >= 0 && port < node.Inputs.Count && node.Inputs[port].Source != null)
                    visit(node.Inputs[port]);
                return;
            }
            foreach (var input in node.Inputs.ToArray())
                if (input.Source != null) visit(input);
        }

        // Exceptions thrown by behaviour callbacks are logged and do not stop the evaluation, as
        // with any script callback Unity makes; NotSupportedException from payload checks is not
        // caught because it is raised outside this wrapper.
        static void Invoke(PlayableNode node, Action<IPlayableBehaviour> callback)
        {
            var behaviour = node.Behaviour;
            if (behaviour == null) return;
            try
            {
                callback(behaviour);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
