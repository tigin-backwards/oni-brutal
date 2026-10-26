namespace UnityEngine.Playables
{
    /// The PlayableOutput API, usable on every output struct through its handle.
    public static partial class PlayableOutputExtensions
    {
        public static bool IsOutputValid<U>(this U output) where U : struct, IPlayableOutput
            => output.GetHandle().IsValid();

        public static Object GetReferenceObject<U>(this U output) where U : struct, IPlayableOutput
            => output.GetHandle().GetReferenceObject();

        public static void SetReferenceObject<U>(this U output, Object value) where U : struct, IPlayableOutput
            => output.GetHandle().SetReferenceObject(value);

        public static Object GetUserData<U>(this U output) where U : struct, IPlayableOutput
            => output.GetHandle().GetUserData();

        /// The user data is passed as playerData to ProcessFrame of the playables this output evaluates.
        public static void SetUserData<U>(this U output, Object value) where U : struct, IPlayableOutput
            => output.GetHandle().SetUserData(value);

        public static Playable GetSourcePlayable<U>(this U output) where U : struct, IPlayableOutput
            => new Playable(output.GetHandle().GetSourcePlayable());

        public static void SetSourcePlayable<U, V>(this U output, V value)
            where U : struct, IPlayableOutput
            where V : struct, IPlayable
            => output.GetHandle().SetSourcePlayable(value.GetHandle(), output.GetSourceOutputPort());

        public static void SetSourcePlayable<U, V>(this U output, V value, int port)
            where U : struct, IPlayableOutput
            where V : struct, IPlayable
            => output.GetHandle().SetSourcePlayable(value.GetHandle(), port);

        public static int GetSourceOutputPort<U>(this U output) where U : struct, IPlayableOutput
            => output.GetHandle().GetSourceOutputPort();

        public static float GetWeight<U>(this U output) where U : struct, IPlayableOutput
            => output.GetHandle().GetWeight();

        public static void SetWeight<U>(this U output, float value) where U : struct, IPlayableOutput
            => output.GetHandle().SetWeight(value);

        /// Sends `notification` to the output's receivers: at the end of the evaluation when pushed
        /// during one, immediately otherwise.
        public static void PushNotification<U>(this U output, Playable origin, INotification notification, object context = null)
            where U : struct, IPlayableOutput
            => output.GetHandle().PushNotification(origin.GetHandle(), notification, context);

        public static INotificationReceiver[] GetNotificationReceivers<U>(this U output) where U : struct, IPlayableOutput
            => output.GetHandle().GetNotificationReceivers();

        public static void AddNotificationReceiver<U>(this U output, INotificationReceiver receiver) where U : struct, IPlayableOutput
            => output.GetHandle().AddNotificationReceiver(receiver);

        public static void RemoveNotificationReceiver<U>(this U output, INotificationReceiver receiver) where U : struct, IPlayableOutput
            => output.GetHandle().RemoveNotificationReceiver(receiver);
    }
}
