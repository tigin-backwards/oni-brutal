// What the player writes for each kind of log message (lines "PROBE ... LOG ..."): every
// LogType through UnityEngine.Debug, through Debug.unityLogger and through Klei's Debug, each
// between "LOG case NAME begin" and "LOG case NAME end" markers so the Player.log text the
// player writes for it can be read in between. Each case also logs what
// Application.logMessageReceived received (type, condition, stack trace, escaped). Then
// Application.GetStackTraceLogType per type, the same calls with the stack trace type set to
// None and to Full, and exceptions left uncaught in an Update and in a coroutine.
//
// Run alone with PROBE_SCENARIO=logging (10 s into the main menu).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SemanticsProbe
{
    public class Logging : MonoBehaviour
    {
        static void L(string s) => Rec.Log("LOG " + s);

        static string Esc(string s) => s == null ? "null" : s.Replace("\r", "\\r").Replace("\n", "\\n");

        static void Case(string name, System.Action log)
        {
            var got = new List<string>();
            Application.LogCallback on = (c, st, ty) => got.Add(ty + " condition \"" + Esc(c) + "\" stack \"" + Esc(st) + "\"");
            L("case " + name + " begin");
            Application.logMessageReceived += on;
            try { log(); }
            catch (Exception e) { got.Add("THROWS " + e.GetType().Name); }
            finally { Application.logMessageReceived -= on; }
            L("case " + name + " end");
            L("case " + name + " received " + got.Count);
            foreach (var g in got) L("case " + name + " got " + g);
        }

        static Exception Thrown()
        {
            try { Nested(); }
            catch (Exception e) { return e; }
            return null;
        }

        static void Nested() => throw new InvalidOperationException("probe thrown");

        static Exception WithInner()
        {
            try
            {
                try { Nested(); }
                catch (Exception e) { throw new ArgumentException("probe outer", e); }
            }
            catch (Exception e) { return e; }
            return null;
        }

        static void Types(string when)
        {
            foreach (LogType t in Enum.GetValues(typeof(LogType)))
                L(when + " stack trace type " + t + " = " + Application.GetStackTraceLogType(t));
        }

        static void All(string prefix)
        {
            Case(prefix + "unity-log", () => UnityEngine.Debug.Log("probe unity log"));
            Case(prefix + "unity-warning", () => UnityEngine.Debug.LogWarning("probe unity warning"));
            Case(prefix + "unity-error", () => UnityEngine.Debug.LogError("probe unity error"));
            Case(prefix + "unity-assertion", () => UnityEngine.Debug.LogAssertion("probe unity assertion"));
            Case(prefix + "unity-exception", () => UnityEngine.Debug.LogException(Thrown()));
            Case(prefix + "klei-error", () => Debug.LogError("probe klei error"));
            Case(prefix + "klei-exception", () => Debug.LogException(Thrown()));
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(10f);
            L("begin");
            Types("start");
            All("");
            Case("unity-error-context", () => UnityEngine.Debug.LogError("probe unity error context", gameObject));
            Case("unity-errorformat", () => UnityEngine.Debug.LogErrorFormat("probe unity {0}", "errorformat"));
            Case("unity-logformat", () => UnityEngine.Debug.LogFormat("probe unity {0}", "logformat"));
            Case("unity-exception-unthrown", () => UnityEngine.Debug.LogException(new InvalidOperationException("probe unthrown")));
            Case("unity-exception-inner", () => UnityEngine.Debug.LogException(WithInner()));
            Case("unity-exception-context", () => UnityEngine.Debug.LogException(Thrown(), gameObject));
            Case("logger-error", () => UnityEngine.Debug.unityLogger.Log(LogType.Error, "probe logger error"));
            Case("logger-exception-type", () => UnityEngine.Debug.unityLogger.Log(LogType.Exception, "probe logger exception type"));
            Case("logger-error-tag", () => UnityEngine.Debug.unityLogger.LogError("tag", "probe logger error tag"));
            Case("klei-log", () => Debug.Log("probe klei log"));
            Case("klei-warning", () => Debug.LogWarning("probe klei warning"));
            Case("klei-errorformat", () => Debug.LogErrorFormat("probe klei {0}", "errorformat"));
            Case("klei-assert", () => Debug.Assert(false, "probe klei assert"));
            Case("stacktraceutility", () => L("extract \"" + Esc(StackTraceUtility.ExtractStackTrace()) + "\""));
            Case("stacktraceutility-exception", () => L("extract \"" + Esc(StackTraceUtility.ExtractStringFromException(Thrown())) + "\""));
            Case("exception-tostring", () => L("tostring \"" + Esc(Thrown().ToString()) + "\""));
            var saved = new Dictionary<LogType, StackTraceLogType>();
            foreach (LogType t in Enum.GetValues(typeof(LogType))) saved[t] = Application.GetStackTraceLogType(t);
            foreach (var mode in new[] { StackTraceLogType.None, StackTraceLogType.Full })
            {
                foreach (LogType t in Enum.GetValues(typeof(LogType))) Application.SetStackTraceLogType(t, mode);
                Types("set " + mode);
                All(mode + "-");
            }
            foreach (var kv in saved) Application.SetStackTraceLogType(kv.Key, kv.Value);
            Types("restored");
            L("uncaught update next");
            throwInUpdate = true;
            yield return null;
            yield return null;
            L("uncaught coroutine next");
            StartCoroutine(Throwing());
            yield return null;
            L("end");
        }

        bool throwInUpdate;

        void Update()
        {
            if (!throwInUpdate) return;
            throwInUpdate = false;
            Nested();
        }

        IEnumerator Throwing()
        {
            yield return null;
            Nested();
        }
    }
}
