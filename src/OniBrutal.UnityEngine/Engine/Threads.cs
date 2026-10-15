using System;
using System.Threading;

namespace OniBrutal.Engine
{
    /// The engine's main thread: the thread that runs the player loop.
    internal static class Threads
    {
        static int mainThreadId = -1;

        /// Called once by the player loop before any game code runs.
        public static void SetMainThread() => mainThreadId = Environment.CurrentManagedThreadId;

        public static bool IsMainThread => Environment.CurrentManagedThreadId == mainThreadId;

        /// Unity raises this error for main-thread-only engine APIs called from other threads.
        public static void AssertMainThread(string api)
        {
            if (mainThreadId != -1 && Environment.CurrentManagedThreadId != mainThreadId)
                throw new UnityEngine.UnityException(api + " can only be called from the main thread.\n"
                    + "Constructors and field initializers will be executed from the loading thread when loading a scene.\n"
                    + "Don't use this function in the constructor or field initializers, instead move initialization code to the Awake or Start function.");
        }
    }
}
