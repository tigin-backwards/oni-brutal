using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace OniBrutal.Engine.Playables
{
    /// Module-specific state of a native playable or output kind (animation clip, audio clip, …).
    internal abstract class PlayablePayload
    {
        /// Called when an evaluation reaches the node through `output` while it is effectively
        /// playing. Payloads whose Unity behaviour needs a media engine we do not have throw here,
        /// so a graph that would animate or play audio fails loudly instead of doing nothing.
        public virtual void CheckEvaluable(PlayableNode node, OutputNode output, float effectiveWeight) { }
    }

    internal sealed class InputPort
    {
        public PlayableNode Source;
        public int SourcePort;
        public float Weight;
    }

    internal sealed class OutputPort
    {
        public PlayableNode Destination;
        public int DestinationPort;
    }

    /// The native side of one playable.
    internal sealed class PlayableNode
    {
        public readonly PlayableGraphState Graph;
        /// What `PlayableHandle.GetPlayableType` reports for a node without a script instance:
        /// `typeof(Playable)` for generic playables, the wrapper struct type for native kinds.
        public readonly Type Kind;
        public IntPtr Ptr;
        public uint Version;
        public bool Alive = true;

        public object ScriptInstance;
        public PlayablePayload Payload;
        /// The clip set through `AnimationPlayableExtensions.SetAnimatedProperties`.
        public UnityEngine.AnimationClip AnimatedProperties;

        public readonly List<InputPort> Inputs = new List<InputPort>();
        // A playable has one output port unless told otherwise.
        public readonly List<OutputPort> Outputs = new List<OutputPort> { new OutputPort() };

        public PlayState PlayState = PlayState.Playing;
        public double Speed = 1.0;
        public double Time;
        public double PreviousTime;
        // An unbounded duration until one is set.
        public double Duration = double.MaxValue;
        public bool Done;
        public bool PropagateSetTime;
        public PlayableTraversalMode TraversalMode = PlayableTraversalMode.Mix;
        public DirectorWrapMode WrapMode = DirectorWrapMode.None;

        // A SetTime since the node was last prepared: the next prepare reports seekOccurred and
        // does not advance the time that was set.
        public bool SeekPending;
        public double TimeBeforeSeek;

        // Per-evaluation state.
        public ulong PreparedFrame;
        public bool EffectivelyPlaying;
        public FrameData Frame;
        public bool GraphStarted;
        public bool BehaviourPlaying;

        public PlayableNode(PlayableGraphState graph, Type kind)
        {
            Graph = graph;
            Kind = kind;
        }

        public PlayableHandle Handle => new PlayableHandle { m_Handle = Ptr, m_Version = Version };

        public Playable AsPlayable => new Playable(Handle);

        public IPlayableBehaviour Behaviour => ScriptInstance as IPlayableBehaviour;

        public Type PlayableType
            => Kind == typeof(Playable) && ScriptInstance != null ? ScriptInstance.GetType() : Kind;

        public bool HasParent
        {
            get
            {
                foreach (var o in Outputs)
                    if (o.Destination != null) return true;
                return false;
            }
        }

        public void SetTime(double value)
        {
            if (!SeekPending)
            {
                SeekPending = true;
                TimeBeforeSeek = Time;
            }
            Time = value;
            if (!PropagateSetTime) return;
            foreach (var input in Inputs)
                input.Source?.SetTime(value);
        }

        // Reaching the duration marks the playable done (outside Loop mode); only SetDone clears it.
        void UpdateDone()
        {
            if (WrapMode != DirectorWrapMode.Loop && Duration != double.MaxValue && Time >= Duration)
                Done = true;
        }

        /// Advances local time by `delta` and applies the time wrap mode. Returns the FrameData
        /// flags describing what happened.
        public FrameData.Flags Advance(double delta)
        {
            PreviousTime = Time;
            Time += delta;
            FrameData.Flags flags = 0;
            if (Duration <= 0 || Duration == double.MaxValue) return flags;
            switch (WrapMode)
            {
                case DirectorWrapMode.Loop:
                    if (Time >= Duration)
                    {
                        Time %= Duration;
                        flags |= FrameData.Flags.Loop;
                    }
                    else if (Time < 0)
                    {
                        Time = Duration + Time % Duration;
                        flags |= FrameData.Flags.Loop;
                    }
                    break;
                case DirectorWrapMode.Hold:
                    if (Time >= Duration)
                    {
                        Time = Duration;
                        flags |= FrameData.Flags.Hold;
                    }
                    break;
            }
            UpdateDone();
            return flags;
        }
    }

    /// The native side of one playable output.
    internal sealed class OutputNode
    {
        public readonly PlayableGraphState Graph;
        /// What `PlayableOutputHandle.GetPlayableOutputType` reports.
        public readonly Type Kind;
        public readonly string Name;
        public IntPtr Ptr;
        public uint Version;
        public bool Alive = true;

        public PlayableNode Source;
        public int SourcePort;
        public float Weight = 1f;
        public UnityEngine.Object ReferenceObject;
        public UnityEngine.Object UserData;
        /// The animation output's Animator or the audio output's AudioSource.
        public UnityEngine.Object Target;
        public bool EvaluateOnSeek = true;
        public readonly List<INotificationReceiver> Receivers = new List<INotificationReceiver>();

        public OutputNode(PlayableGraphState graph, Type kind, string name)
        {
            Graph = graph;
            Kind = kind;
            Name = name;
        }

        public PlayableOutputHandle Handle => new PlayableOutputHandle { m_Handle = Ptr, m_Version = Version };
    }
}
