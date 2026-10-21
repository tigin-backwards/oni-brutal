using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D16. An exception nothing catches on a thread other than the main one ends a
    /// CoreCLR process; Unity's player keeps running (measured in the original: a thread's
    /// exception is logged, with Debug.LogException from UnityEngine's AppDomain.UnhandledException
    /// handler; a thread-pool item's and an async void method's are dropped without a word, and
    /// Mono raises no UnhandledException for them). Mods' background work relies on it
    /// (Sgt_Imalas' UtilLibs checks for updates from an async void method, which throws when
    /// offline). The host takes such exceptions the same way: a thread's raises
    /// AppDomain.UnhandledException (IsTerminating set, as Mono sets it) and the thread ends, a
    /// thread pool's is dropped; the main thread's still ends the process.
    internal static class UnhandledExceptions
    {
        static bool installed;

        /// Called on the main thread, which runs the player loop.
        public static void Install()
        {
            if (installed) return;
            installed = true;
            int mainThread = Environment.CurrentManagedThreadId;
            // AppContext.OnUnhandledException raises AppDomain.UnhandledException as the runtime does.
            var raise = typeof(AppContext).GetMethod("OnUnhandledException", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(nameof(AppContext), "OnUnhandledException");
            // UnityEngine's handler (its RegisterUECatcher): the exception in the player's log.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception e) UnityEngine.Debug.LogException(e);
            };
            ExceptionHandling.SetUnhandledExceptionHandler(exception =>
            {
                if (Environment.CurrentManagedThreadId == mainThread) return false;
                if (!Thread.CurrentThread.IsThreadPoolThread) raise.Invoke(null, [exception]);
                return true;
            });
        }
    }
}
