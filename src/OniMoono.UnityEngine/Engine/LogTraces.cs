using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace OniMoono.Engine
{
    /// The stack traces the original (release, Mono) player writes for a logged exception, measured
    /// with UnityProbe's logging scenario. Messages (Log, Warning, Error, Assert) get none.
    ///
    /// The condition is the innermost exception's "Type: message". The log's text continues with
    /// each exception's own frames, innermost first, in Mono's format ("  at A.B (System.String
    /// name) [0x0001f] in &lt;mvid&gt;:0 "), each outer one introduced by "Rethrow as Type: message",
    /// and then the managed frames of the LogException call in Unity's format ("A:B(String, Int32)"),
    /// and an empty line. The callbacks get the exception frames as "A.B (…) (at &lt;mvid&gt;:0)" and
    /// the call's frames from UnityEngine.Debug's on (without the log handler's).
    ///
    /// The engine's frames stand for the original's native code, which shows in neither: they are
    /// left out of the calls, and so is a UnityEngine.Debug frame the engine itself called (an
    /// exception from Update or a coroutine is reported with its own frames alone), as are the
    /// engine frames at the bottom of an exception's frames (where the engine caught it).
    internal static class LogTraces
    {
        internal readonly record struct ExceptionReport(string Condition, string LogTrace, string CallbackTrace);

        public static ExceptionReport Exception(Exception exception, StackTrace calls)
        {
            var chain = new List<Exception>();
            for (var e = exception; e != null; e = e.InnerException) chain.Add(e);
            chain.Reverse();
            var log = new StringBuilder();
            var callback = new StringBuilder();
            for (int i = 0; i < chain.Count; i++)
            {
                var e = chain[i];
                if (i > 0)
                {
                    string rethrow = "Rethrow as " + Condition(e) + "\n";
                    log.Append(rethrow);
                    callback.Append(rethrow);
                }
                var frames = new StackTrace(e, true).GetFrames();
                int end = frames.Length;
                while (end > 0 && IsEngine(frames[end - 1].GetMethod())) end--;
                for (int f = 0; f < end; f++)
                {
                    var method = frames[f].GetMethod();
                    if (method == null) continue;
                    string name = MonoMethod(method);
                    string file = frames[f].GetFileName();
                    string location = file != null ? file + ":" + frames[f].GetFileLineNumber() : "<" + ModuleId(method) + ">:0";
                    int il = Math.Max(0, frames[f].GetILOffset());
                    log.Append("  at ").Append(name).Append(" [0x").Append(il.ToString("x5")).Append("] in ").Append(location).Append(" \n");
                    callback.Append(name).Append(" (at ").Append(location).Append(")\n");
                }
            }
            var callFrames = CallFrames(calls, engineFrames: false);
            int debug = callFrames.FindIndex(c => c.method.DeclaringType == typeof(UnityEngine.Debug));
            for (int i = 0; i < callFrames.Count; i++)
            {
                string line = UnityFrame(callFrames[i].method) + "\n";
                log.Append(line);
                if (debug >= 0 && i >= debug) callback.Append(line);
            }
            return new ExceptionReport(Condition(chain[0]), log.ToString(), callback.ToString());
        }

        /// The frames of CALLS in Unity's format, one per line. With ENGINEFRAMES the engine's frames
        /// stay, with their source lines (engine diagnostics).
        public static string Calls(StackTrace calls, bool engineFrames)
        {
            var sb = new StringBuilder();
            foreach (var (method, frame) in CallFrames(calls, engineFrames))
            {
                sb.Append(UnityFrame(method));
                var file = engineFrames ? frame.GetFileName() : null;
                if (file != null) sb.Append(" (at ").Append(file).Append(':').Append(frame.GetFileLineNumber()).Append(')');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static string Condition(Exception e) => e.GetType().Name + ": " + e.Message;

        static List<(MethodBase method, StackFrame frame)> CallFrames(StackTrace calls, bool engineFrames)
        {
            var list = new List<(MethodBase, StackFrame)>();
            var frames = calls.GetFrames();
            for (int i = 0; i < frames.Length; i++)
            {
                var method = frames[i].GetMethod();
                if (method == null || IsLogging(method)) continue;
                if (!engineFrames)
                {
                    if (IsEngine(method)) continue;
                    if (method.DeclaringType == typeof(UnityEngine.Debug) && CalledByEngine(frames, i)) continue;
                }
                list.Add((method, frames[i]));
            }
            return list;
        }

        // The caller of frame I (the next frame with a method) is the engine's, or there is none.
        static bool CalledByEngine(StackFrame[] frames, int i)
        {
            for (int j = i + 1; j < frames.Length; j++)
            {
                var caller = frames[j].GetMethod();
                if (caller != null) return IsEngine(caller);
            }
            return true;
        }

        static bool IsLogging(MethodBase m)
        {
            var t = m.DeclaringType;
            return t == typeof(EngineLog) || t == typeof(LogTraces) || t?.Namespace == "OniMoono.Engine.Logging";
        }

        static bool IsEngine(MethodBase m)
        {
            var ns = m?.DeclaringType?.Namespace;
            return ns != null && (ns == "OniMoono" || ns.StartsWith("OniMoono.", StringComparison.Ordinal));
        }

        static string ModuleId(MethodBase m)
        {
            try { return m.Module.ModuleVersionId.ToString("N"); }
            catch (InvalidOperationException) { return "00000000000000000000000000000000"; }
        }

        // Unity's frame: "Namespace.Type:Method(ArgType, ArgType)", a nested type named by its own
        // name after the namespace ("SemanticsProbe.<>c:<All>b__7_4()").
        static string UnityFrame(MethodBase m)
        {
            var sb = new StringBuilder();
            var t = m.DeclaringType;
            if (t == null) sb.Append("(wrapper dynamic-method) ");
            else
            {
                if (!string.IsNullOrEmpty(t.Namespace)) sb.Append(t.Namespace).Append('.');
                sb.Append(t.Name).Append(':');
            }
            sb.Append(m.Name).Append('(');
            var ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ps[i].ParameterType.Name);
            }
            return sb.Append(')').ToString();
        }

        // Mono's frame: "Namespace.Outer+Nested.Method (System.String name, …)", a generic type or
        // method named by its definition, its arguments (the outer types' too) on the innermost
        // type: "StateMachine`4+GenericInstance[StateMachineType,…].ExecuteActions ()".
        static string MonoMethod(MethodBase m)
        {
            if (m.DeclaringType is { IsGenericType: true } || m.IsGenericMethod)
            {
                try { m = m.Module.ResolveMethod(m.MetadataToken) ?? m; }
                catch (ArgumentException) { }
            }
            var sb = new StringBuilder();
            if (m.DeclaringType == null) sb.Append("(wrapper dynamic-method) ");
            else sb.Append(MonoType(m.DeclaringType)).Append('.');
            sb.Append(m.Name);
            if (m.IsGenericMethod) AppendArguments(sb, m.GetGenericArguments());
            sb.Append(" (");
            var ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(MonoType(ps[i].ParameterType)).Append(' ').Append(ps[i].Name);
            }
            return sb.Append(')').ToString();
        }

        static string MonoType(Type t)
        {
            if (t.IsByRef) return MonoType(t.GetElementType()) + "&";
            if (t.IsPointer) return MonoType(t.GetElementType()) + "*";
            if (t.IsArray) return MonoType(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            if (t.IsGenericParameter) return t.Name;
            var sb = new StringBuilder();
            AppendPath(sb, t);
            if (t.IsGenericType) AppendArguments(sb, t.GetGenericArguments());
            return sb.ToString();
        }

        static void AppendPath(StringBuilder sb, Type t)
        {
            if (t.IsNested)
            {
                AppendPath(sb, t.DeclaringType);
                sb.Append('+');
            }
            else if (!string.IsNullOrEmpty(t.Namespace)) sb.Append(t.Namespace).Append('.');
            sb.Append(t.Name);
        }

        static void AppendArguments(StringBuilder sb, Type[] arguments)
        {
            sb.Append('[');
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(MonoType(arguments[i]));
            }
            sb.Append(']');
        }
    }
}
