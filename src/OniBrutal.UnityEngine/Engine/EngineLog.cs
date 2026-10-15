using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// The player's log: what Unity's default log handler does natively. Writes Player.log and
    /// stdout, and raises Application.logMessageReceivedThreaded on the calling thread and
    /// Application.logMessageReceived on the main thread (queued when logging from elsewhere).
    internal static class EngineLog
    {
        static readonly object fileLock = new();
        static StreamWriter file;
        static readonly ConcurrentQueue<(string condition, string stackTrace, LogType type)> pendingMainThread = new();

        /// Opens the log file (overwriting it, as Unity does). Called once by the host.
        public static void Open(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            lock (fileLock)
            {
                file = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
            }
        }

        public static void Close()
        {
            lock (fileLock)
            {
                file?.Dispose();
                file = null;
            }
        }

        /// Engine diagnostics (not game logging): written to the log only, tagged.
        public static void Engine(string message) => WriteRaw("[engine] " + message);

        static void WriteRaw(string text)
        {
            lock (fileLock)
            {
                file?.WriteLine(text);
                Console.Out.WriteLine(text);
            }
        }

        public static void Message(LogType type, string message, string stackTrace)
        {
            var text = stackTrace.Length > 0 ? message + "\n" + stackTrace : message;
            WriteRaw(text);
            Dispatch(message, stackTrace, type);
        }

        static void Dispatch(string condition, string stackTrace, LogType type)
        {
            Application.RaiseLogMessageThreaded(condition, stackTrace, type);
            if (Threads.IsMainThread) Application.RaiseLogMessage(condition, stackTrace, type);
            else pendingMainThread.Enqueue((condition, stackTrace, type));
        }

        /// Delivers messages logged from other threads to main-thread subscribers (player loop).
        public static void FlushToMainThread()
        {
            while (pendingMainThread.TryDequeue(out var m)) Application.RaiseLogMessage(m.condition, m.stackTrace, m.type);
        }

        /// A managed stack trace in Unity's format ("Type:Method (ArgTypes)" per line), starting at
        /// the first frame outside the logging machinery.
        public static string CaptureStackTrace()
        {
            var trace = new StackTrace(1, true);
            var sb = new StringBuilder();
            foreach (var frame in trace.GetFrames())
            {
                var m = frame.GetMethod();
                if (m == null) continue;
                var t = m.DeclaringType;
                if (t == typeof(EngineLog) || t?.Namespace == "OniBrutal.Engine.Logging") continue;
                AppendFrame(sb, m, frame);
            }
            return sb.ToString();
        }

        public static string FormatException(Exception e)
        {
            var sb = new StringBuilder();
            for (var ex = e; ex != null; ex = ex.InnerException)
            {
                var trace = new StackTrace(ex, true);
                foreach (var frame in trace.GetFrames())
                {
                    var m = frame.GetMethod();
                    if (m != null) AppendFrame(sb, m, frame);
                }
                if (ex.InnerException != null) sb.Append("Rethrow as ").Append(ex.GetType().Name).Append(": ").Append(ex.Message).Append('\n');
            }
            return sb.ToString();
        }

        static void AppendFrame(StringBuilder sb, MethodBase m, StackFrame frame)
        {
            var t = m.DeclaringType;
            sb.Append(t != null ? t.FullName : "<unknown>").Append(':').Append(m.Name).Append(" (");
            var ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ps[i].ParameterType.Name);
            }
            sb.Append(')');
            var file = frame.GetFileName();
            if (file != null) sb.Append(" (at ").Append(file).Append(':').Append(frame.GetFileLineNumber()).Append(')');
            sb.Append('\n');
        }
    }
}

namespace OniBrutal.Engine.Logging
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
            string message = args == null || args.Length == 0 ? format : string.Format(format, args);
            EngineLog.Message(logType, message, EngineLog.CaptureStackTrace());
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            EngineLog.Message(LogType.Exception, exception.GetType().Name + ": " + exception.Message, EngineLog.FormatException(exception));
        }
    }
}
