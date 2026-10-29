using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Threading;
using OniMoono.Engine;
using Cecil = Mono.Cecil;
using Cil = Mono.Cecil.Cil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D47. Mono's Thread.Abort raises ThreadAbortException in the target thread at
    /// its next safe point (a blocking wait is woken for it), raises it again at the end of every
    /// catch that takes it unless the thread calls ResetAbort, and a thread it ends ends quietly.
    /// CoreCLR has no abort: Thread.Abort and ResetAbort throw PlatformNotSupportedException on the
    /// caller. KleiMetrics' StartSession ends the previous session's HTTP thread with
    /// ThreadedHttps.End, which aborts it when it has not stopped within 2 s: with Klei's servers
    /// out of reach that thread is waiting to retry, so the main thread threw from Global.Update
    /// and the game went to the crash reporter (issue #28). The calls of Thread.Abort and
    /// ResetAbort in the game's assemblies and in mods come here instead (rewritten as they load,
    /// in MonoConversions' pass). An abort marks the thread and interrupts it (Thread.Interrupt):
    /// its next blocking wait throws ThreadInterruptedException. In an assembly that calls
    /// Thread.Abort, every catch that could take the abort (of object, Exception, SystemException,
    /// ThreadAbortException, ThreadInterruptedException, or with a filter) calls CatchEnd before it
    /// leaves, which throws ThreadAbortException while the thread is marked, as Mono's re-raise.
    /// The thread's last exception then ends it quietly (UnhandledExceptions).
    public static class MonoThreadAbort
    {
        /// One abort of a thread: Active until the thread calls ResetAbort or ends.
        sealed class Request
        {
            public volatile bool Active = true;
            public readonly ThreadAbortException Exception = NewAbortException();
        }

        static readonly ConditionalWeakTable<Thread, Request> requests = new();
        /// Aborts requested so far: CatchEnd looks a thread up only after the first.
        static int requested;
        /// The thread running the player loop: an abort of it raises the exception at the call,
        /// and does not follow it (D47).
        static int mainThread = -1;

        internal static void SetMainThread() => mainThread = Environment.CurrentManagedThreadId;

        /// What Mono's ThreadAbortException says (the original's log: "System.Threading.
        /// ThreadAbortException: Thread was being aborted"); CoreCLR's says "System error.".
        const string MonoMessage = "Thread was being aborted";

        static readonly FieldInfo? message = typeof(Exception).GetField("_message", BindingFlags.NonPublic | BindingFlags.Instance);

        /// A ThreadAbortException with Mono's message. Its constructors are not public.
        static ThreadAbortException NewAbortException()
        {
            var exception = typeof(ThreadAbortException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)?.Invoke(null) as ThreadAbortException
                            ?? (ThreadAbortException)RuntimeHelpers.GetUninitializedObject(typeof(ThreadAbortException));
            message?.SetValue(exception, MonoMessage);
            return exception;
        }

        /// Thread.Abort().
        public static void Abort(Thread thread) => Abort(thread, null);

        /// Thread.Abort(object). The state is not kept: CoreCLR's ExceptionState is always null.
        public static void Abort(Thread thread, object? stateInfo)
        {
            if (thread is null) throw new NullReferenceException();
            if (thread == Thread.CurrentThread)
            {
                // Mono raises it at once, with a new exception.
                if (Environment.CurrentManagedThreadId == mainThread) throw NewAbortException();
                var request = new Request();
                lock (requests) requests.AddOrUpdate(thread, request);
                Interlocked.Increment(ref requested);
                throw request.Exception;
            }
            lock (requests)
            {
                // Mono ignores an abort of a thread that has ended or is being aborted already.
                if ((thread.ThreadState & ThreadState.Stopped) != 0) return;
                if (requests.TryGetValue(thread, out var pending) && pending.Active) return;
                requests.AddOrUpdate(thread, new Request());
            }
            Interlocked.Increment(ref requested);
            thread.Interrupt();
            EngineLog.Engine($"Thread.Abort: thread {thread.ManagedThreadId} ({thread.Name ?? "unnamed"}) is interrupted at its next wait (D47)");
        }

        /// Thread.ResetAbort(): the current thread's abort is cancelled, as on Mono, and an
        /// interrupt that has not been raised yet is taken back.
        public static void ResetAbort()
        {
            if (!requests.TryGetValue(Thread.CurrentThread, out var request) || !request.Active)
                throw new ThreadStateException("Unable to reset abort because no abort was requested");
            request.Active = false;
            try
            {
                Thread.Sleep(0);
            }
            catch (ThreadInterruptedException)
            {
                // The abort's interrupt, not raised before.
            }
        }

        /// Called before a catch that could take the abort leaves: Mono raises the abort again
        /// there. Hidden from stack traces, which start at the catch, as Mono's do.
        [System.Diagnostics.StackTraceHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CatchEnd()
        {
            if (Volatile.Read(ref requested) != 0) Reraise();
        }

        [System.Diagnostics.StackTraceHidden, MethodImpl(MethodImplOptions.NoInlining)]
        static void Reraise()
        {
            if (requests.TryGetValue(Thread.CurrentThread, out var request) && request.Active) throw request.Exception;
        }

        /// An exception nothing caught is ending the current thread: true when Mono ends the
        /// thread quietly, for ThreadAbortException (whose abort ends with it) and for the
        /// interrupt of an abort.
        internal static bool EndsQuietly(Exception exception)
        {
            bool aborting = requests.TryGetValue(Thread.CurrentThread, out var request) && request.Active;
            if (request != null) request.Active = false;
            bool quiet = exception is ThreadAbortException || (aborting && exception is ThreadInterruptedException);
            if (quiet) EngineLog.Engine($"Thread.Abort: thread {Environment.CurrentManagedThreadId} ended by its abort ({exception.GetType().Name})");
            return quiet;
        }

        // --- The rewrite of game and mod IL -------------------------------------------------

        static readonly HashSet<string> Members = new(StringComparer.Ordinal) { "Abort", "ResetAbort" };

        /// The catch types that take ThreadAbortException or the abort's interrupt.
        static readonly HashSet<string> Catching = new(StringComparer.Ordinal)
        {
            "System.Object", "System.Exception", "System.SystemException", "System.Threading.ThreadAbortException",
            "System.Threading.ThreadInterruptedException",
        };

        static MethodInfo? Replacement(Cecil.MethodReference callee)
        {
            if (callee.DeclaringType.FullName != "System.Threading.Thread" || !Members.Contains(callee.Name)) return null;
            if (callee.Name == "ResetAbort")
                return !callee.HasThis && callee.Parameters.Count == 0 ? typeof(MonoThreadAbort).GetMethod(nameof(ResetAbort)) : null;
            if (!callee.HasThis) return null;
            if (callee.Parameters.Count == 0) return typeof(MonoThreadAbort).GetMethod(nameof(Abort), [typeof(Thread)]);
            if (callee.Parameters.Count == 1 && callee.Parameters[0].ParameterType.FullName == "System.Object")
                return typeof(MonoThreadAbort).GetMethod(nameof(Abort), [typeof(Thread), typeof(object)]);
            return null;
        }

        /// Sends the module's calls of Thread.Abort and ResetAbort here and, when it has a call of
        /// Thread.Abort, has its catches raise the abort again. Returns the calls replaced.
        internal static int RewriteModule(Cecil.ModuleDefinition module, out int catchEnds)
        {
            catchEnds = 0;
            var references = new Dictionary<MethodInfo, Cecil.MethodReference>();
            int replaced = 0;
            bool aborts = false;
            foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    foreach (var instruction in method.Body.Instructions)
                    {
                        // A delegate of Thread.Abort (ldftn, ldvirtftn) keeps the runtime's.
                        if (instruction.OpCode.Code is not (Cil.Code.Call or Cil.Code.Callvirt) || instruction.Operand is not Cecil.MethodReference callee
                            || !Members.Contains(callee.Name) || Replacement(callee) is not { } replacement) continue;
                        if (!references.TryGetValue(replacement, out var reference)) references[replacement] = reference = module.ImportReference(replacement);
                        instruction.OpCode = Cil.OpCodes.Call;
                        instruction.Operand = reference;
                        aborts |= replacement.Name == nameof(Abort);
                        replaced++;
                    }
                }
            if (!aborts) return replaced;
            var catchEnd = module.ImportReference(typeof(MonoThreadAbort).GetMethod(nameof(CatchEnd)));
            foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody || !method.Body.HasExceptionHandlers) continue;
                    // A leave out of nested handlers is changed once.
                    var leaves = new HashSet<Cil.Instruction>();
                    foreach (var handler in method.Body.ExceptionHandlers)
                    {
                        if (handler.HandlerType == Cil.ExceptionHandlerType.Catch ? !Catching.Contains(handler.CatchType.FullName)
                            : handler.HandlerType != Cil.ExceptionHandlerType.Filter) continue;
                        LeavesOut(handler, leaves);
                    }
                    if (leaves.Count == 0) continue;
                    var il = method.Body.GetILProcessor();
                    foreach (var leave in leaves)
                    {
                        // The leave itself becomes the call, followed by a copy of it, so branches
                        // to it and the handlers' bounds stay where they were.
                        var copy = il.Create(leave.OpCode, (Cil.Instruction)leave.Operand);
                        leave.OpCode = Cil.OpCodes.Call;
                        leave.Operand = catchEnd;
                        il.InsertAfter(leave, copy);
                    }
                    catchEnds += leaves.Count;
                    MonoConversions.WidenBranches(method.Body);
                }
            return replaced;
        }

        /// Adds the handler's leaves that go out of it.
        static void LeavesOut(Cil.ExceptionHandler handler, HashSet<Cil.Instruction> leaves)
        {
            var inside = new HashSet<Cil.Instruction>();
            for (var instruction = handler.HandlerStart; instruction != null && instruction != handler.HandlerEnd; instruction = instruction.Next)
                inside.Add(instruction);
            foreach (var instruction in inside)
                if (instruction.OpCode.Code is Cil.Code.Leave or Cil.Code.Leave_S && !inside.Contains((Cil.Instruction)instruction.Operand))
                    leaves.Add(instruction);
        }

        /// Whether the image references Thread.Abort or Thread.ResetAbort (read from its metadata
        /// alone).
        internal static bool MayAbort(byte[] image)
        {
            try
            {
                using var pe = new PEReader(new MemoryStream(image));
                if (!pe.HasMetadata) return false;
                var reader = pe.GetMetadataReader();
                foreach (var handle in reader.MemberReferences)
                {
                    var member = reader.GetMemberReference(handle);
                    if (member.Parent.Kind != HandleKind.TypeReference) continue;
                    var parent = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                    if (reader.StringComparer.Equals(parent.Namespace, "System.Threading") && reader.StringComparer.Equals(parent.Name, "Thread")
                        && Members.Contains(reader.GetString(member.Name))) return true;
                }
            }
            catch (BadImageFormatException)
            {
                // Not an assembly: the load reports it.
            }
            return false;
        }
    }
}
