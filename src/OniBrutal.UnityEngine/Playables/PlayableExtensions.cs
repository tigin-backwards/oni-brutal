namespace UnityEngine.Playables
{
    /// The Playable API, usable on every playable struct (Playable, ScriptPlayable<T>,
    /// AnimationMixerPlayable, …) through its handle.
    public static partial class PlayableExtensions
    {
        public static bool IsValid<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().IsValid();

        public static void Destroy<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().Destroy();

        public static PlayableGraph GetGraph<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetGraph();

        public static PlayState GetPlayState<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetPlayState();

        public static void Play<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().Play();

        public static void Pause<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().Pause();

        public static void SetSpeed<U>(this U playable, double value) where U : struct, IPlayable
            => playable.GetHandle().SetSpeed(value);

        public static double GetSpeed<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetSpeed();

        public static void SetDuration<U>(this U playable, double value) where U : struct, IPlayable
            => playable.GetHandle().SetDuration(value);

        public static double GetDuration<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetDuration();

        /// Sets the local time (and, with SetPropagateSetTime(true), the time of the inputs). The
        /// next evaluation reports seekOccurred and does not advance past the time set.
        public static void SetTime<U>(this U playable, double value) where U : struct, IPlayable
            => playable.GetHandle().SetTime(value);

        public static double GetTime<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetTime();

        /// The local time before the last evaluation advanced it (or before the last SetTime).
        public static double GetPreviousTime<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetPreviousTime();

        public static void SetDone<U>(this U playable, bool value) where U : struct, IPlayable
            => playable.GetHandle().SetDone(value);

        /// Set once the local time reaches the duration (except in Loop wrap mode), or by SetDone.
        public static bool IsDone<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().IsDone();

        public static void SetPropagateSetTime<U>(this U playable, bool value) where U : struct, IPlayable
            => playable.GetHandle().SetPropagateSetTime(value);

        public static bool GetPropagateSetTime<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetPropagateSetTime();

        /// Resizes the input ports; ports removed are disconnected, ports added are unconnected
        /// with weight 0.
        public static void SetInputCount<U>(this U playable, int value) where U : struct, IPlayable
            => playable.GetHandle().SetInputCount(value);

        public static int GetInputCount<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetInputCount();

        public static void SetOutputCount<U>(this U playable, int value) where U : struct, IPlayable
            => playable.GetHandle().SetOutputCount(value);

        public static int GetOutputCount<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetOutputCount();

        public static Playable GetInput<U>(this U playable, int inputPort) where U : struct, IPlayable
            => playable.GetHandle().GetInput(inputPort);

        public static Playable GetOutput<U>(this U playable, int outputPort) where U : struct, IPlayable
            => playable.GetHandle().GetOutput(outputPort);

        /// Throws IndexOutOfRangeException if `inputIndex` is not an input port.
        public static void SetInputWeight<U>(this U playable, int inputIndex, float weight) where U : struct, IPlayable
            => playable.GetHandle().SetInputWeight(inputIndex, weight);

        public static void SetInputWeight<U, V>(this U playable, V input, float weight)
            where U : struct, IPlayable
            where V : struct, IPlayable
            => playable.GetHandle().SetInputWeight(input.GetHandle(), weight);

        /// Throws IndexOutOfRangeException if `inputIndex` is not an input port.
        public static float GetInputWeight<U>(this U playable, int inputIndex) where U : struct, IPlayable
            => playable.GetHandle().GetInputWeight(inputIndex);

        public static void ConnectInput<U, V>(this U playable, int inputIndex, V sourcePlayable, int sourceOutputIndex)
            where U : struct, IPlayable
            where V : struct, IPlayable
            => playable.ConnectInput(inputIndex, sourcePlayable, sourceOutputIndex, 0f);

        public static void ConnectInput<U, V>(this U playable, int inputIndex, V sourcePlayable, int sourceOutputIndex, float weight)
            where U : struct, IPlayable
            where V : struct, IPlayable
        {
            playable.GetGraph().Connect(sourcePlayable, sourceOutputIndex, playable, inputIndex);
            playable.SetInputWeight(inputIndex, weight);
        }

        public static void DisconnectInput<U>(this U playable, int inputPort) where U : struct, IPlayable
            => playable.GetGraph().Disconnect(playable, inputPort);

        /// Appends an input port, connects `sourcePlayable` to it and returns its index.
        public static int AddInput<U, V>(this U playable, V sourcePlayable, int sourceOutputIndex, float weight = 0f)
            where U : struct, IPlayable
            where V : struct, IPlayable
        {
            int index = playable.GetInputCount();
            playable.SetInputCount(index + 1);
            playable.ConnectInput(index, sourcePlayable, sourceOutputIndex, weight);
            return index;
        }

        public static PlayableTraversalMode GetTraversalMode<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetTraversalMode();

        public static void SetTraversalMode<U>(this U playable, PlayableTraversalMode mode) where U : struct, IPlayable
            => playable.GetHandle().SetTraversalMode(mode);

        internal static DirectorWrapMode GetTimeWrapMode<U>(this U playable) where U : struct, IPlayable
            => playable.GetHandle().GetTimeWrapMode();

        internal static void SetTimeWrapMode<U>(this U playable, DirectorWrapMode value) where U : struct, IPlayable
            => playable.GetHandle().SetTimeWrapMode(value);

        internal static int GetOutputPortFromInputConnection<U>(this U playable, int inputIndex) where U : struct, IPlayable
            => playable.GetHandle().GetOutputPortFromInputConnection(inputIndex);

        internal static int GetInputPortFromOutputConnection<U>(this U playable, int outputIndex) where U : struct, IPlayable
            => playable.GetHandle().GetInputPortFromOutputConnection(outputIndex);
    }
}
