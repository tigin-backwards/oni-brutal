using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OniBrutal.Engine;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    /// Which log callbacks a message reaches, on which thread and when, as UnityProbe's
    /// logthreads scenario measured in the original player: a message logged on the main thread
    /// reaches logMessageReceived and then logMessageReceivedThreaded at once; one logged on any
    /// other thread reaches logMessageReceivedThreaded alone, on that thread, and never
    /// logMessageReceived (the game's crash reporter listens there, so worldgen's errors on
    /// its threads open no dialog). A handler's exception ends the call and is written to the
    /// log; a message logged from inside a handler is written and reaches no handler.
    [Collection("Console")]
    public class LogCallbackTests : EngineTest
    {
        readonly List<string> got = new();

        Application.LogCallback Recorder(string name) => (c, s, t) =>
        {
            if (!c.Contains("lcb")) return;
            lock (got) got.Add(name + (Threads.IsMainThread ? " main " : " worker ") + t + " " + c);
        };

        void With(Action action, params (bool threaded, Application.LogCallback handler)[] handlers)
        {
            foreach (var (threaded, h) in handlers)
                if (threaded) Application.logMessageReceivedThreaded += h; else Application.logMessageReceived += h;
            try { action(); }
            finally
            {
                foreach (var (threaded, h) in handlers)
                    if (threaded) Application.logMessageReceivedThreaded -= h; else Application.logMessageReceived -= h;
            }
        }

        static string LogText(Action action)
        {
            var path = Path.Combine(Path.GetTempPath(), $"logcallback-{Guid.NewGuid():N}", "Player.log");
            var stdout = Console.Out;
            try
            {
                EngineLog.Open(path);
                action();
            }
            finally
            {
                EngineLog.Close();
                Console.SetOut(stdout);
            }
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            Directory.Delete(Path.GetDirectoryName(path)!, true);
            return text;
        }

        [Fact]
        public void AMainThreadMessageReachesBothEventsAtOnceReceivedFirst()
        {
            With(() =>
            {
                Debug.LogError("lcb main error");
                Assert.Equal(["received main Error lcb main error", "threaded main Error lcb main error"], got);
            }, (false, Recorder("received")), (true, Recorder("threaded")));
        }

        [Fact]
        public void AnotherThreadsMessagesReachOnlyTheThreadedEventOnThatThread()
        {
            With(() =>
            {
                var thread = new Thread(() =>
                {
                    Debug.LogError("lcb thread error");
                    Debug.LogException(new InvalidOperationException("lcb thread exception"));
                    Debug.unityLogger.Log(LogType.Warning, "lcb thread warning");
                });
                thread.Start();
                thread.Join();
                ThreadPool.QueueUserWorkItem(_ => Debug.LogError("lcb pool error"));
                Task.Run(() => Debug.LogError("lcb task error")); // not waited for: Wait could run it here
                SpinWait.SpinUntil(() => { lock (got) return got.Count == 5; }, 5000);
                // Neither later frames nor the main thread's own logging deliver them.
                Frame();
                Frame();
                lock (got)
                {
                    Assert.True(got.Count == 5, string.Join("\n", got));
                    Assert.All(got, g => Assert.StartsWith("threaded worker ", g));
                    Assert.Contains("threaded worker Exception InvalidOperationException: lcb thread exception", got);
                }
            }, (false, Recorder("received")), (true, Recorder("threaded")));
        }

        [Fact]
        public void AReceivedHandlersExceptionEndsTheCallAndIsWrittenFirst()
        {
            Application.LogCallback thrower = (c, s, t) => { if (c == "lcb thrower") throw new InvalidOperationException("lcb handler threw"); };
            var text = LogText(() => With(() => Debug.LogWarning("lcb thrower"),
                (false, Recorder("before")), (false, thrower), (false, Recorder("after")), (true, Recorder("threaded"))));
            Assert.Equal(["before main Warning lcb thrower"], got);
            int exception = text.IndexOf("InvalidOperationException: lcb handler threw\n", StringComparison.Ordinal);
            int message = text.IndexOf("\nlcb thrower\n", StringComparison.Ordinal);
            Assert.True(exception >= 0 && message > exception, text);
        }

        [Fact]
        public void AThreadedHandlersExceptionEndsOnlyTheThreadedHandlersAfterIt()
        {
            Application.LogCallback thrower = (c, s, t) => { if (c == "lcb thrower") throw new InvalidOperationException("lcb handler threw"); };
            LogText(() => With(() => Debug.LogWarning("lcb thrower"),
                (false, Recorder("received")), (true, Recorder("before")), (true, thrower), (true, Recorder("after"))));
            Assert.Equal(["received main Warning lcb thrower", "before main Warning lcb thrower"], got);
        }

        [Fact]
        public void AMessageLoggedInAHandlerIsWrittenFirstAndReachesNoHandler()
        {
            Application.LogCallback nester = (c, s, t) => { if (c == "lcb nest") Debug.LogWarning("lcb nested"); };
            var text = LogText(() => With(() => Debug.LogWarning("lcb nest"),
                (false, Recorder("received")), (false, nester), (true, Recorder("threaded"))));
            Assert.Equal(["received main Warning lcb nest", "threaded main Warning lcb nest"], got);
            Assert.Contains("lcb nested\nlcb nest\n", text);
        }
    }
}
