// Stack walks from Harmony patch methods (lines "CA ..."): what Assembly.GetCallingAssembly(),
// GetExecutingAssembly() and new StackTrace() report inside prefixes and postfixes of patched
// constructors and methods, which run from Harmony's replacement (a dynamic method). The patched
// members are this probe's; their callers are in an emitted assembly, "CallerAsm", so the answer
// tells the caller's assembly from the probe's and from 0Harmony's.
//
// Each line is "CA <case> = <assembly name>" (or a list for several calls), and "CA frames ..."
// lists the frames a postfix's new StackTrace() has: the method's name, its DeclaringType and the
// assembly of its module.
//
// Run alone with PROBE_SCENARIO=callingasm (at mod load).
//
// Measured 2026-10-26 (U57 original on Unity's MonoBleedingEdge, Linux amd64; ours on .NET 10
// CoreCLR): see docs/ARCHITECTURE.md D41.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace SemanticsProbe
{
    public class CallingTarget
    {
        public int X;
        public CallingTarget() { X = 1; }
        public CallingTarget(int v) : this() { X += v; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Run(int v) { return v * 3 + 1; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Frames(int v) { return v * 5 + 2; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly Body() { return Assembly.GetCallingAssembly(); }
    }

    public class CallingChain
    {
        public int X;
        public CallingChain() { X = 1; }
        public CallingChain(int v) : this() { X += v; }
    }

    public static class CallingHelper
    {
        // Small enough for Mono to inline (under 20 bytes of IL).
        public static Assembly Who() { return Assembly.GetCallingAssembly(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly WhoNoInline() { return Assembly.GetCallingAssembly(); }
    }

    public static class CallingAssembly
    {
        public static Assembly CtorSeen, CtorExec, CtorNoInlineSeen, RunPostSeen, RunPreSeen;
        public static readonly List<string> Chain = new List<string>();
        public static readonly List<string> FrameLines = new List<string>();

        public static void CtorPostfix() { CtorSeen = Assembly.GetCallingAssembly(); }
        public static void CtorExecPostfix() { CtorExec = Assembly.GetExecutingAssembly(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CtorNoInlinePostfix() { CtorNoInlineSeen = Assembly.GetCallingAssembly(); }
        public static void RunPostfix() { RunPostSeen = Assembly.GetCallingAssembly(); }
        public static void RunPrefix() { RunPreSeen = Assembly.GetCallingAssembly(); }
        public static void ChainPostfix(CallingChain __instance) { Chain.Add(__instance.X + ":" + N(Assembly.GetCallingAssembly())); }
        public static void Empty() { }

        public static void FramesPostfix()
        {
            var trace = new StackTrace(0, false);
            for (int i = 0; i < trace.FrameCount && i < 10; i++)
            {
                var m = trace.GetFrame(i).GetMethod();
                FrameLines.Add(i + " " + (m == null ? "null" : m.Name + " | " + (m.DeclaringType == null ? "null" : m.DeclaringType.FullName)
                    + " | " + N(m.Module.Assembly) + " | " + m.GetType().Name));
            }
        }

        static string N(Assembly a) => a == null ? "null" : a.GetName().Name;

        public static void Run(Harmony harmony)
        {
            var t = typeof(CallingTarget);
            var self = typeof(CallingAssembly);
            HarmonyMethod H(string name) => new HarmonyMethod(self.GetMethod(name));
            harmony.Patch(t.GetConstructor(Type.EmptyTypes), postfix: H(nameof(CtorPostfix)));
            harmony.Patch(t.GetConstructor(Type.EmptyTypes), postfix: H(nameof(CtorExecPostfix)));
            harmony.Patch(t.GetConstructor(new[] { typeof(int) }), postfix: H(nameof(CtorNoInlinePostfix)));
            harmony.Patch(t.GetMethod("Run"), prefix: H(nameof(RunPrefix)), postfix: H(nameof(RunPostfix)));
            harmony.Patch(t.GetMethod("Frames"), postfix: H(nameof(FramesPostfix)));
            harmony.Patch(t.GetMethod("Body"), prefix: H(nameof(Empty)));
            harmony.Patch(typeof(CallingChain).GetConstructor(Type.EmptyTypes), postfix: H(nameof(ChainPostfix)));
            harmony.Patch(typeof(CallingChain).GetConstructor(new[] { typeof(int) }), postfix: H(nameof(ChainPostfix)));

            // CallerAsm: Caller.Make() = new CallingTarget(); MakeWith() = new CallingTarget(4);
            // CallRun() = CallingTarget.Run(2); CallFrames(); CallBody() = CallingTarget.Body();
            // CallWho() = CallingHelper.Who(); CallWhoNoInline(); MakeChain() = new CallingChain(5);
            // InvokeRun() and InvokeFrames() call Run and Frames through MethodInfo.Invoke; Activate()
            // makes a CallingTarget through Activator.
            var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CallerAsm"), AssemblyBuilderAccess.Run).DefineDynamicModule("CallerAsm");
            var caller = module.DefineType("Caller", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            void Define(string name, Type ret, Action<ILGenerator> body)
            {
                var m = caller.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, ret, Type.EmptyTypes);
                m.SetImplementationFlags(MethodImplAttributes.NoInlining);
                var il = m.GetILGenerator();
                body(il);
                il.Emit(OpCodes.Ret);
            }
            Define("Make", typeof(object), il => il.Emit(OpCodes.Newobj, t.GetConstructor(Type.EmptyTypes)));
            Define("MakeWith", typeof(object), il => { il.Emit(OpCodes.Ldc_I4_4); il.Emit(OpCodes.Newobj, t.GetConstructor(new[] { typeof(int) })); });
            Define("CallRun", typeof(int), il => { il.Emit(OpCodes.Ldc_I4_2); il.Emit(OpCodes.Call, t.GetMethod("Run")); });
            Define("CallFrames", typeof(int), il => { il.Emit(OpCodes.Ldc_I4_2); il.Emit(OpCodes.Call, t.GetMethod("Frames")); });
            Define("CallBody", typeof(Assembly), il => il.Emit(OpCodes.Call, t.GetMethod("Body")));
            Define("CallWho", typeof(Assembly), il => il.Emit(OpCodes.Call, typeof(CallingHelper).GetMethod("Who")));
            Define("CallWhoNoInline", typeof(Assembly), il => il.Emit(OpCodes.Call, typeof(CallingHelper).GetMethod("WhoNoInline")));
            Define("MakeChain", typeof(object), il => { il.Emit(OpCodes.Ldc_I4_5); il.Emit(OpCodes.Newobj, typeof(CallingChain).GetConstructor(new[] { typeof(int) })); });
            void DefineInvoke(string name, string target) => Define(name, typeof(object), il =>
            {
                il.Emit(OpCodes.Ldtoken, t);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                il.Emit(OpCodes.Ldstr, target);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetMethod", new[] { typeof(string) }));
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Newarr, typeof(object));
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ldc_I4_7);
                il.Emit(OpCodes.Box, typeof(int));
                il.Emit(OpCodes.Stelem_Ref);
                il.Emit(OpCodes.Callvirt, typeof(MethodBase).GetMethod("Invoke", new[] { typeof(object), typeof(object[]) }));
            });
            DefineInvoke("InvokeRun", "Run");
            DefineInvoke("InvokeFrames", "Frames");
            Define("Activate", typeof(object), il =>
            {
                il.Emit(OpCodes.Ldtoken, t);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                il.Emit(OpCodes.Call, typeof(Activator).GetMethod("CreateInstance", new[] { typeof(Type) }));
            });
            var type = caller.CreateType();

            void Case(string name, Func<string> run)
            {
                string r;
                try { r = run(); }
                catch (Exception e) { r = "!" + e.GetType().Name + " " + e.Message; }
                Rec.Log("CA " + name + " = " + r);
            }
            Func<object> D(string name) => (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), type.GetMethod(name));
            Func<int> I(string name) => (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), type.GetMethod(name));
            Func<Assembly> A(string name) => (Func<Assembly>)Delegate.CreateDelegate(typeof(Func<Assembly>), type.GetMethod(name));

            Case("ctor.postfix.calling", () => { CtorSeen = null; D("Make")(); return N(CtorSeen); });
            Case("ctor.postfix.executing", () => { CtorExec = null; D("Make")(); return N(CtorExec); });
            Case("ctor.chained.noinline.postfix.calling", () => { CtorNoInlineSeen = CtorSeen = null; D("MakeWith")(); return N(CtorNoInlineSeen) + " (inner ctor's postfix: " + N(CtorSeen) + ")"; });
            Case("method.postfix.calling", () => { RunPostSeen = null; I("CallRun")(); return N(RunPostSeen); });
            Case("method.prefix.calling", () => { RunPreSeen = null; I("CallRun")(); return N(RunPreSeen); });
            Case("method.reflection.postfix.calling", () => { RunPostSeen = null; D("InvokeRun")(); return N(RunPostSeen); });
            Case("ctor.activator.postfix.calling", () => { CtorSeen = null; D("Activate")(); return N(CtorSeen); });
            Case("chain.postfixes", () => { Chain.Clear(); D("MakeChain")(); return string.Join(", ", Chain.ToArray()); });
            Case("patched.body.calling", () => N(A("CallBody")()));
            Case("unpatched.small.calling", () => N(A("CallWho")()));
            Case("unpatched.noinline.calling", () => N(A("CallWhoNoInline")()));
            Case("frames", () => { FrameLines.Clear(); I("CallFrames")(); return FrameLines.Count.ToString(); });
            foreach (var line in FrameLines) Rec.Log("CA frames " + line);
            // The same through MethodInfo.Invoke, twice: the first call and a later one may take
            // different paths.
            for (int round = 0; round < 3; round++)
            {
                FrameLines.Clear();
                RunPostSeen = null;
                D("InvokeFrames")();
                D("InvokeRun")();
                Rec.Log("CA reflection " + round + " postfix.calling = " + N(RunPostSeen));
                foreach (var line in FrameLines) Rec.Log("CA rframes " + round + " " + line);
            }
            Rec.Log("CA done");
        }
    }
}
