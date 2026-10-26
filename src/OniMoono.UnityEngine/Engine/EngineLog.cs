using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OniMoono.Engine
{
    /// The player's log: what Unity's default log handler does natively. Writes Player.log and
    /// stdout, and calls the log callbacks on the logging thread: logMessageReceived only for a
    /// message logged on the main thread, logMessageReceivedThreaded for every message.
    internal static class EngineLog
    {
        static readonly object fileLock = new();
        static CappedLogFile file;

        static TextWriter console;

        /// Opens the log file (overwriting it, as Unity does; capped, unlike Unity's: CappedLogFile).
        /// Called once by the host. Like the
        /// original player's log, it also takes the process's console output: the game's own
        /// logger writes its [INFO] lines with Console.WriteLine. With `keepPrevious` (the
        /// player's default log, not one named on the command line), the last session's log is
        /// kept beside it as NAME-prev.EXT (Player-prev.log), replacing the one before, as
        /// Unity's player does.
        public static void Open(string path, bool keepPrevious = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (keepPrevious && File.Exists(path))
            {
                var previous = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-prev" + Path.GetExtension(path));
                // Another running player may hold it open; the session's log is then written
                // over it, as before.
                try { File.Move(path, previous, overwrite: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            lock (fileLock)
            {
                file = new CappedLogFile(path);
                console ??= Console.Out;
                Console.SetOut(new LogWriter(console));
            }
        }

        public static void Close()
        {
            lock (fileLock)
            {
                if (console != null) Console.SetOut(console);
                console = null;
                file?.Dispose();
                file = null;
            }
        }

        /// The console's writer while the log is open: each write goes to the original console and
        /// the log file.
        sealed class LogWriter(TextWriter console) : TextWriter
        {
            public override Encoding Encoding => console.Encoding;

            public override void Write(char value)
            {
                lock (fileLock)
                {
                    console.Write(value);
                    file?.Write(value);
                }
            }

            public override void Write(string value)
            {
                lock (fileLock)
                {
                    console.Write(value);
                    file?.Write(value);
                }
            }

            public override void WriteLine(string value)
            {
                // The game's own log lines (Klei's Debug writes them here) mark its start-up.
                if (!StartupProgress.Done && Threads.IsMainThread) StartupProgress.LogLine(value);
                Platform.MainThreadPump.Maybe();
                lock (fileLock)
                {
                    console.WriteLine(value);
                    file?.WriteLine(value);
                }
            }

            public override void Flush()
            {
                lock (fileLock)
                {
                    console.Flush();
                    file?.Flush();
                }
            }
        }

        /// Engine diagnostics (not game logging): written to the log only, tagged.
        public static void Engine(string message) => WriteRaw("[engine] " + message);

        static void WriteRaw(string text)
        {
            // To the original console and the log file while the log is open.
            lock (fileLock)
            {
                if (file != null && console != null)
                {
                    console.WriteLine(text);
                    file.WriteLine(text);
                }
                else Console.Out.WriteLine(text);
            }
        }

        public static void Message(LogType type, string message, string stackTrace) => Message(type, message, stackTrace, stackTrace);

        /// A message whose log text has another stack trace than its callbacks get (an exception's:
        /// LogTraces.Exception). A trace ends with a line break, so the text then ends with an empty
        /// line; a message without one is a line of its own.
        ///
        /// As in the original (UnityProbe's logthreads scenario), the callbacks run first, on the
        /// logging thread (Application.CallLogCallback), and the text is written after them. A
        /// message logged from inside a callback is written but reaches no callback, and a
        /// callback's exception is written as a logged exception's text, before the message's,
        /// and reaches no callback either.
        public static void Message(LogType type, string message, string logTrace, string callbackTrace)
        {
            Platform.MainThreadPump.Maybe();
            if (!inCallback)
            {
                Exception thrown;
                inCallback = true;
                try { thrown = Application.CallLogCallback(message, callbackTrace, type, Threads.IsMainThread); }
                finally { inCallback = false; }
                if (thrown != null)
                {
                    var report = LogTraces.Exception(thrown, new StackTrace(1, true));
                    WriteRaw(report.Condition + "\n" + report.LogTrace);
                }
            }
            WriteRaw(logTrace.Length > 0 ? message + "\n" + logTrace : message);
        }

        /// This thread is inside the log callbacks.
        [ThreadStatic] static bool inCallback;

        /// The managed stack of the caller in Unity's format ("Type:Method(ArgTypes)" per line), the
        /// engine's frames included, with their source lines. For engine diagnostics.
        public static string CaptureStackTrace() => LogTraces.Calls(new StackTrace(1, true), engineFrames: true);
    }
}

namespace OniMoono.Engine.Logging
{
    /// Unity's Logger: filters by type and forwards to a replaceable handler.
    internal sealed class UnityLogger : ILogger
    {
        public UnityLogger(ILogHandler handler)
        {
            logHandler = handler;
            logEnabled = true;
            filterLogType = LogType.Log;
        }

        public ILogHandler logHandler { get; set; }
        public bool logEnabled { get; set; }
        public LogType filterLogType { get; set; }

        /// Exceptions always pass; otherwise a type passes if it is at least as severe as the filter
        /// (Error is the most severe, Log the least).
        public bool IsLogTypeAllowed(LogType logType)
        {
            if (!logEnabled) return false;
            if (logType == LogType.Exception) return true;
            if (filterLogType != LogType.Exception) return logType <= filterLogType;
            return false;
        }

        static string GetString(object message)
        {
            if (message == null) return "Null";
            return message is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : message.ToString();
        }

        public void Log(LogType logType, object message)
        {
            if (IsLogTypeAllowed(logType)) logHandler.LogFormat(logType, null, "{0}", GetString(message));
        }

        public void Log(LogType logType, object message, UnityEngine.Object context)
        {
            if (IsLogTypeAllowed(logType)) logHandler.LogFormat(logType, context, "{0}", GetString(message));
        }

        public void Log(LogType logType, string tag, object message)
        {
            if (IsLogTypeAllowed(logType)) logHandler.LogFormat(logType, null, "{0}: {1}", tag, GetString(message));
        }

        public void Log(LogType logType, string tag, object message, UnityEngine.Object context)
        {
            if (IsLogTypeAllowed(logType)) logHandler.LogFormat(logType, context, "{0}: {1}", tag, GetString(message));
        }

        public void Log(object message) => Log(LogType.Log, message);
        public void Log(string tag, object message) => Log(LogType.Log, tag, message);
        public void Log(string tag, object message, UnityEngine.Object context) => Log(LogType.Log, tag, message, context);
        public void LogWarning(string tag, object message) => Log(LogType.Warning, tag, message);
        public void LogWarning(string tag, object message, UnityEngine.Object context) => Log(LogType.Warning, tag, message, context);
        public void LogError(string tag, object message) => Log(LogType.Error, tag, message);
        public void LogError(string tag, object message, UnityEngine.Object context) => Log(LogType.Error, tag, message, context);

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType)) logHandler.LogFormat(logType, null, format, args);
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType)) logHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception) => LogException(exception, null);

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (logEnabled) logHandler.LogException(exception, context);
        }
    }

    /// Unity's default handler: formats and hands the message to the engine log.
    internal sealed class DefaultLogHandler : ILogHandler
    {
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            // A release player writes no stack trace for a message of any type, although
            // Application.GetStackTraceLogType says ScriptOnly for each, and its callbacks get an
            // empty one (measured: UnityProbe's logging scenario).
            string message = args == null || args.Length == 0 ? format : string.Format(format, args);
            EngineLog.Message(logType, message, "");
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            var report = LogTraces.Exception(exception, new StackTrace(1, true));
            EngineLog.Message(LogType.Exception, report.Condition, report.LogTrace, report.CallbackTrace);
        }
    }
}
