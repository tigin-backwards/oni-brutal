// Which thread and frame Application.logMessageReceived and logMessageReceivedThreaded run on for
// messages logged from the main thread, a managed Thread, a ThreadPool item and a Task (lines
// "PROBE ... LT ..."). Each origin logs every LogType through UnityEngine.Debug, through
// Debug.unityLogger and through Klei's Debug; the handlers record the thread, the frame and the
// message, and the records are written from the main thread a few frames later. Then: handlers
// on only one of the events, the main thread blocked in Join while a thread logs, handlers that
// throw, an exception left uncaught on a thread, and whether KCrashReporter opened its dialog
// for the workers' errors (before the main thread logs any).
//
// Run alone with PROBE_SCENARIO=logthreads (10 s into the main menu).
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SemanticsProbe
{
    public class LogThreads : MonoBehaviour
    {
        static void L(string s) => Rec.Log("LT " + s);

        static int mainId;
        static volatile int frame;
        static readonly ConcurrentQueue<string> got = new ConcurrentQueue<string>();
        static volatile string throwOn;

        static string Who()
        {
            var t = Thread.CurrentThread;
            return t.ManagedThreadId == mainId ? "main" : "worker(" + (t.IsThreadPoolThread ? "pool" : "thread") + ")";
        }

        static string First(string s)
        {
            if (s == null) return "null";
            int n = s.IndexOf('\n');
            return (n < 0 ? s : s.Substring(0, n)).Replace("\r", "\\r");
        }

        static void Record(string ev, string c, string st, LogType t)
        {
            if (c == null || !c.Contains("LTM")) return;
            got.Enqueue(ev + " on=" + Who() + " f" + frame + " " + t + " \"" + First(c) + "\" stack=" + (string.IsNullOrEmpty(st) ? "empty" : "\"" + First(st) + "\""));
        }

        static void OnMain(string c, string st, LogType t) => Record("received", c, st, t);
        static void OnThreaded(string c, string st, LogType t) => Record("threaded", c, st, t);
        static void OnMainAfter(string c, string st, LogType t) => Record("received-after", c, st, t);
        static void OnThreadedAfter(string c, string st, LogType t) => Record("threaded-after", c, st, t);

        static void Thrower(string c, string st, LogType t)
        {
            var w = throwOn;
            if (w != null && c != null && c.StartsWith(w)) throw new InvalidOperationException("LT handler threw for " + First(c));
        }

        static void Nester(string c, string st, LogType t)
        {
            if (c == null || !c.StartsWith("LTM nest") || c.Contains("nested")) return;
            UnityEngine.Debug.LogWarning("LTM nested in a handler on=" + Who() + " for " + c);
        }

        static Exception Thrown(string m)
        {
            try { throw new InvalidOperationException(m); }
            catch (Exception e) { return e; }
        }

        static void Emit(string origin)
        {
            string at = " logged on=" + Who() + " f" + frame;
            UnityEngine.Debug.Log("LTM " + origin + " log" + at);
            UnityEngine.Debug.LogWarning("LTM " + origin + " warning" + at);
            UnityEngine.Debug.LogError("LTM " + origin + " error" + at);
            UnityEngine.Debug.LogAssertion("LTM " + origin + " assertion" + at);
            UnityEngine.Debug.LogException(Thrown("LTM " + origin + " exception" + at));
            UnityEngine.Debug.unityLogger.Log(LogType.Error, "LTM " + origin + " logger-error" + at);
            UnityEngine.Debug.unityLogger.Log(LogType.Log, "LTM " + origin + " logger-log" + at);
            Debug.LogError("LTM " + origin + " klei-error" + at);
            got.Enqueue("emitted " + origin + " on=" + Who() + " f" + frame);
        }

        static void Flush(string phase)
        {
            while (got.TryDequeue(out var s)) L(phase + " " + s);
        }

        static void Crash(string when)
        {
            L("crash " + when + " ErrorCanvas=" + (GameObject.Find("ErrorCanvas") != null) + " hasCrash=" + KCrashReporter.hasCrash + " ignoreAll=" + KCrashReporter.ignoreAll + " App.isLoading=" + App.isLoading);
        }

        void Awake() => mainId = Thread.CurrentThread.ManagedThreadId;

        void Update() => frame = Time.frameCount;

        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Worker(string phase, string origin, System.Action<string> emit)
        {
            var done = new ManualResetEvent(false);
            L(phase + " " + origin + " start f" + Time.frameCount);
            System.Action body = () => { try { emit(origin); } finally { done.Set(); } };
            if (origin == "thread") new Thread(() => body()).Start();
            else if (origin == "pool") ThreadPool.QueueUserWorkItem(_ => body());
            else Task.Run(body);
            while (!done.WaitOne(0)) yield return null;
            L(phase + " " + origin + " done f" + Time.frameCount);
            yield return Frames(5);
            L(phase + " " + origin + " flush f" + Time.frameCount);
            Flush(phase + " " + origin);
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(10f);
            L("begin main=" + mainId);
            Crash("begin");

            // Both events have a handler.
            Application.logMessageReceived += OnMain;
            Application.logMessageReceivedThreaded += OnThreaded;
            foreach (var origin in new[] { "thread", "pool", "task" })
                yield return Worker("both", origin, Emit);
            yield return Frames(30);
            Flush("both late");
            Crash("after workers");

            // The main thread blocked in Join while a thread logs (as SaveLoader waits for
            // WorldGen's settings task).
            L("join start f" + Time.frameCount);
            var joined = new Thread(() => Emit("joined"));
            joined.Start();
            joined.Join();
            L("join done f" + Time.frameCount);
            Flush("join immediate");
            yield return Frames(5);
            L("join flush f" + Time.frameCount);
            Flush("join");

            // An exception nothing catches on a thread.
            L("unhandled start f" + Time.frameCount);
            var unhandled = new Thread(() => throw new InvalidOperationException("LTM unhandled thread exception"));
            unhandled.Start();
            unhandled.Join();
            yield return Frames(5);
            L("unhandled flush f" + Time.frameCount);
            Flush("unhandled");
            Crash("after unhandled");

            // Only logMessageReceived has a handler.
            Application.logMessageReceivedThreaded -= OnThreaded;
            yield return Worker("mainonly", "thread", o => UnityEngine.Debug.LogError("LTM " + o + " mainonly error"));
            L("mainonly main");
            UnityEngine.Debug.LogWarning("LTM main mainonly warning");
            yield return Frames(2);
            Flush("mainonly main");

            // Only logMessageReceivedThreaded has a handler.
            Application.logMessageReceived -= OnMain;
            Application.logMessageReceivedThreaded += OnThreaded;
            yield return Worker("threadedonly", "thread", o => UnityEngine.Debug.LogWarning("LTM " + o + " threadedonly warning"));
            L("threadedonly main");
            UnityEngine.Debug.LogWarning("LTM main threadedonly warning");
            yield return Frames(2);
            Flush("threadedonly main");

            // Throwing handlers, between two recording ones: on logMessageReceived only, on
            // logMessageReceivedThreaded only (each logged from the main thread), and on
            // logMessageReceivedThreaded logged from a thread.
            Application.logMessageReceived += OnMain;
            Application.logMessageReceived += Thrower;
            Application.logMessageReceived += OnMainAfter;
            Application.logMessageReceivedThreaded += OnThreadedAfter;
            throwOn = "LTM thrower";
            L("thrower received begin");
            UnityEngine.Debug.LogWarning("LTM thrower received warning");
            L("thrower received end");
            Flush("thrower received immediate");
            yield return Frames(2);
            Flush("thrower received");
            Application.logMessageReceived -= Thrower;
            Application.logMessageReceivedThreaded -= OnThreadedAfter;
            Application.logMessageReceivedThreaded += Thrower;
            Application.logMessageReceivedThreaded += OnThreadedAfter;
            L("thrower threaded begin");
            UnityEngine.Debug.LogWarning("LTM thrower threaded warning");
            L("thrower threaded end");
            Flush("thrower threaded immediate");
            yield return Frames(2);
            Flush("thrower threaded");
            yield return Worker("thrower", "thread", o => UnityEngine.Debug.LogWarning("LTM thrower " + o + " warning"));
            throwOn = null;
            Application.logMessageReceived -= OnMainAfter;
            Application.logMessageReceivedThreaded -= Thrower;
            Application.logMessageReceivedThreaded -= OnThreadedAfter;

            // A handler that logs (once per message): the order of the texts and callbacks.
            Application.logMessageReceived += Nester;
            Application.logMessageReceivedThreaded += Nester;
            L("nest main begin");
            UnityEngine.Debug.LogWarning("LTM nest main");
            L("nest main end");
            Flush("nest main");
            yield return Worker("nest", "thread", o => UnityEngine.Debug.LogWarning("LTM nest " + o));
            Application.logMessageReceived -= Nester;
            Application.logMessageReceivedThreaded -= Nester;

            // The main thread last (KCrashReporter opens its dialog for this one's errors).
            Crash("before main");
            L("main begin f" + Time.frameCount);
            Emit("main");
            L("main end f" + Time.frameCount);
            Flush("main immediate");
            yield return Frames(5);
            Flush("main");
            Crash("after main");
            Application.logMessageReceived -= OnMain;
            Application.logMessageReceivedThreaded -= OnThreaded;
            L("end");
        }
    }
}
