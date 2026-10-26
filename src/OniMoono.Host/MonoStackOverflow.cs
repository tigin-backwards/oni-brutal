using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using OniMoono.Engine;
using Cecil = Mono.Cecil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D28. Mono turns a stack overflow into a StackOverflowException that the code
    /// below can catch: a mod whose OnSpawn recursed without end had the game log "Error in
    /// HeadquartersComplete.Telepad.OnSpawn … System.StackOverflowException" (KMonoBehaviour.Spawn
    /// catches every exception) and play on (measured in the original player). CoreCLR ends the
    /// process instead, writing "Stack overflow." to stderr alone, so the player's log just stops.
    /// A mod's methods that can recurse (a call cycle among its own methods, found in its IL) are
    /// given a probe at their start that throws StackOverflowException, with Mono's message, while
    /// some stack is still left (RuntimeHelpers.TryEnsureSufficientExecutionStack), so the code
    /// that catches it on Mono catches it here. A mod that cannot be rewritten loads unchanged.
    public static class MonoStackOverflow
    {
        /// What Mono's StackOverflowException says.
        const string MonoMessage = "The requested operation caused a stack overflow.";

        /// Called at the start of a mod's method that can recurse. Hidden from stack traces, which
        /// start at the mod's method, as Mono's do.
        [System.Diagnostics.StackTraceHidden]
        public static void Probe()
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack() && !SmallStackHasRoom()) throw new StackOverflowException(MonoMessage);
        }

        /// TryEnsureSufficientExecutionStack wants 128 KB left, so on a thread whose whole stack
        /// is 128 KB or less it always fails: the game's JobManager workers (and fast-jobs')
        /// have 128 KB, and a probed method they ran threw at once (issue #15) where Mono ran
        /// it. On a stack of 256 KB or less, the limit is half the stack instead (64 KB left
        /// on a 128 KB worker: enough to throw, and the handler runs where the catch is).
        [ThreadStatic] static nuint smallStackLimit;  // 0: not read yet; MaxValue: no small stack

        static unsafe bool SmallStackHasRoom()
        {
            nuint limit = smallStackLimit;
            if (limit == 0) smallStackLimit = limit = SmallStackLimit();
            byte here = 0;
            return (nuint)(&here) > limit;
        }

        internal const int SmallStack = 256 * 1024;

        static unsafe nuint SmallStackLimit()
        {
            nuint low = 0, size = 0;
            if (OperatingSystem.IsWindows())
            {
                GetCurrentThreadStackLimits(out low, out var high);
                size = high - low;
            }
            else if (OperatingSystem.IsLinux())
            {
                byte* attr = stackalloc byte[128];  // pthread_attr_t: 56 bytes on x64 glibc
                if (pthread_getattr_np(pthread_self(), attr) == 0)
                {
                    nint address, bytes;
                    if (pthread_attr_getstack(attr, &address, &bytes) == 0) { low = (nuint)address; size = (nuint)bytes; }
                    pthread_attr_destroy(attr);
                }
            }
            return size == 0 || size > SmallStack ? nuint.MaxValue : low + size / 2;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern void GetCurrentThreadStackLimits(out nuint low, out nuint high);
        [System.Runtime.InteropServices.DllImport("libc")]
        static extern unsafe int pthread_getattr_np(nint thread, byte* attr);
        [System.Runtime.InteropServices.DllImport("libc")]
        static extern unsafe int pthread_attr_getstack(byte* attr, nint* address, nint* size);
        [System.Runtime.InteropServices.DllImport("libc")]
        static extern unsafe int pthread_attr_destroy(byte* attr);
        [System.Runtime.InteropServices.DllImport("libc")]
        static extern nint pthread_self();

        /// The image with probes in its methods that can recurse, or null when it has none.
        /// `symbols` (portable PDB bytes or null) are rewritten to match.
        internal static byte[]? Adjust(byte[] image, ref byte[]? symbols, string path)
        {
            HashSet<int> recursive;
            try
            {
                recursive = RecursiveMethods(image);
            }
            catch (BadImageFormatException)
            {
                return null;  // not an assembly: the load reports it
            }
            if (recursive.Count == 0) return null;
            try
            {
                return Rewrite(image, ref symbols, path, recursive);
            }
            catch (Exception e) when (e is Cecil.AssemblyResolutionException or BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                EngineLog.Engine($"mod stack probes: {Path.GetFileName(path)} not rewritten ({e.GetType().Name}: {e.Message}); it loads unchanged");
                return null;
            }
        }

        static byte[]? Rewrite(byte[] image, ref byte[]? symbols, string path, HashSet<int> recursive)
        {
            using var resolver = LoadedFirstResolver.ForMod(path);
            bool withSymbols = symbols != null;
            Cecil.ModuleDefinition module;
            try
            {
                module = Cecil.ModuleDefinition.ReadModule(new MemoryStream(image), new Cecil.ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = withSymbols,
                    SymbolStream = withSymbols ? new MemoryStream(symbols!) : null,
                    SymbolReaderProvider = withSymbols ? new Cecil.Cil.PortablePdbReaderProvider() : null,
                });
            }
            catch (Exception e) when (withSymbols && e is Cecil.Cil.SymbolsNotMatchingException or Cecil.Cil.SymbolsNotFoundException or BadImageFormatException or InvalidOperationException)
            {
                withSymbols = false;
                module = Cecil.ModuleDefinition.ReadModule(new MemoryStream(image), new Cecil.ReaderParameters { AssemblyResolver = resolver });
            }
            using var owned = module;
            var probe = module.ImportReference(typeof(MonoStackOverflow).GetMethod(nameof(Probe))!);
            int probed = 0;
            foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody || !recursive.Contains(method.MetadataToken.ToInt32())) continue;
                    var body = method.Body;
                    var first = body.Instructions[0];
                    body.GetILProcessor().InsertBefore(first, Cecil.Cil.Instruction.Create(Cecil.Cil.OpCodes.Call, probe));
                    probed++;
                }
            if (probed == 0) return null;
            using var output = new MemoryStream();
            using var symbolOutput = new MemoryStream();
            module.Write(output, new Cecil.WriterParameters { WriteSymbols = withSymbols, SymbolStream = withSymbols ? symbolOutput : null, SymbolWriterProvider = withSymbols ? new Cecil.Cil.PortablePdbWriterProvider() : null });
            symbols = withSymbols ? symbolOutput.ToArray() : null;
            EngineLog.Engine($"mod stack probes: {Path.GetFileName(path)}: {probed} method(s) that can recurse raise a catchable stack overflow, as on Mono");
            return output.ToArray();
        }

        /// The metadata tokens of the image's methods on a call cycle among its own methods, read
        /// from metadata and IL alone. A call is followed to its target in this module: a method
        /// definition, a generic instantiation of one, or a member reference into one of this
        /// module's generic types (by name). A virtual call goes to every virtual method of the
        /// module with that name too (its overrides). Extra edges only add probes.
        internal static HashSet<int> RecursiveMethods(byte[] image)
        {
            using var pe = new PEReader(new MemoryStream(image));
            var result = new HashSet<int>();
            if (!pe.HasMetadata) return result;
            var reader = pe.GetMetadataReader();
            var virtualsByName = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var methodsByTypeAndName = new Dictionary<(int Type, string Name), List<int>>();
            foreach (var typeHandle in reader.TypeDefinitions)
                foreach (var methodHandle in reader.GetTypeDefinition(typeHandle).GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    string name = reader.GetString(method.Name);
                    int row = MetadataTokens.GetRowNumber(methodHandle);
                    var key = (MetadataTokens.GetRowNumber(typeHandle), name);
                    if (!methodsByTypeAndName.TryGetValue(key, out var same)) methodsByTypeAndName[key] = same = new List<int>();
                    same.Add(row);
                    if ((method.Attributes & System.Reflection.MethodAttributes.Virtual) != 0)
                    {
                        if (!virtualsByName.TryGetValue(name, out var list)) virtualsByName[name] = list = new List<int>();
                        list.Add(row);
                    }
                }
            int count = reader.MethodDefinitions.Count;
            var edges = new List<int>[count + 1];
            foreach (var methodHandle in reader.MethodDefinitions)
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0) continue;
                int from = MetadataTokens.GetRowNumber(methodHandle);
                var targets = edges[from] = new List<int>();
                try
                {
                    var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
                    while (il.RemainingBytes > 0)
                    {
                        var op = ReadOpCode(ref il);
                        if (op is ILOpCode.Call or ILOpCode.Callvirt or ILOpCode.Newobj)
                        {
                            var handle = MetadataTokens.EntityHandle(il.ReadInt32());
                            foreach (int target in Targets(reader, handle, methodsByTypeAndName))
                            {
                                if (target < 1 || target > count) continue;
                                targets.Add(target);
                                if (op == ILOpCode.Callvirt && virtualsByName.TryGetValue(reader.GetString(reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(target)).Name), out var overrides))
                                    targets.AddRange(overrides);
                            }
                        }
                        else SkipOperand(ref il, op);
                    }
                }
                catch (Exception e) when (e is BadImageFormatException or ArgumentException or InvalidOperationException)
                {
                    // IL this reader does not follow (obfuscated): the calls read so far count.
                }
            }
            foreach (int row in Cycles(edges, count)) result.Add(MetadataTokens.GetToken(MetadataTokens.MethodDefinitionHandle(row)));
            return result;
        }

        static IEnumerable<int> Targets(MetadataReader reader, EntityHandle handle, Dictionary<(int Type, string Name), List<int>> methodsByTypeAndName)
        {
            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                    yield return MetadataTokens.GetRowNumber(handle);
                    break;
                case HandleKind.MethodSpecification:
                    foreach (int t in Targets(reader, reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method, methodsByTypeAndName)) yield return t;
                    break;
                case HandleKind.MemberReference:
                {
                    var member = reader.GetMemberReference((MemberReferenceHandle)handle);
                    if (member.Parent.Kind != HandleKind.TypeSpecification) break;
                    // A method of a generic type of this module, through its instantiation.
                    var blob = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)member.Parent).Signature);
                    if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) break;
                    blob.ReadSignatureTypeCode();  // class or valuetype
                    var generic = blob.ReadTypeHandle();
                    if (generic.Kind != HandleKind.TypeDefinition) break;
                    if (methodsByTypeAndName.TryGetValue((MetadataTokens.GetRowNumber(generic), reader.GetString(member.Name)), out var same))
                        foreach (int t in same) yield return t;
                    break;
                }
            }
        }

        /// The nodes on a cycle (a strongly connected component of more than one node, or a node
        /// that calls itself), by Tarjan's algorithm without recursion.
        static List<int> Cycles(List<int>[] edges, int count)
        {
            var onCycle = new List<int>();
            var index = new int[count + 1];
            var low = new int[count + 1];
            var onStack = new bool[count + 1];
            var stack = new Stack<int>();
            int next = 1;
            var work = new Stack<(int Node, int Edge)>();
            for (int root = 1; root <= count; root++)
            {
                if (index[root] != 0 || edges[root] == null) continue;
                work.Push((root, 0));
                while (work.Count > 0)
                {
                    var (node, edge) = work.Pop();
                    if (edge == 0)
                    {
                        index[node] = low[node] = next++;
                        stack.Push(node);
                        onStack[node] = true;
                    }
                    var targets = edges[node];
                    bool descended = false;
                    for (int i = edge; targets != null && i < targets.Count; i++)
                    {
                        int to = targets[i];
                        if (index[to] == 0)
                        {
                            work.Push((node, i + 1));
                            work.Push((to, 0));
                            descended = true;
                            break;
                        }
                        if (onStack[to]) low[node] = Math.Min(low[node], index[to]);
                    }
                    if (descended) continue;
                    if (low[node] == index[node])
                    {
                        var component = new List<int>();
                        int member;
                        do
                        {
                            member = stack.Pop();
                            onStack[member] = false;
                            component.Add(member);
                        } while (member != node);
                        if (component.Count > 1 || (targets != null && targets.Contains(node))) onCycle.AddRange(component);
                    }
                    if (work.Count > 0)
                    {
                        var (parent, _) = work.Peek();
                        low[parent] = Math.Min(low[parent], low[node]);
                    }
                }
            }
            return onCycle;
        }

        static ILOpCode ReadOpCode(ref BlobReader il)
        {
            byte b = il.ReadByte();
            return b == 0xFE ? (ILOpCode)(0xFE00 | il.ReadByte()) : (ILOpCode)b;
        }

        static void SkipOperand(ref BlobReader il, ILOpCode op)
        {
            switch (op)
            {
                case ILOpCode.Switch:
                    il.Offset += 4 * il.ReadInt32();
                    return;
                case ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s or ILOpCode.Ldloc_s or ILOpCode.Ldloca_s or ILOpCode.Stloc_s
                    or ILOpCode.Ldc_i4_s or ILOpCode.Unaligned
                    or ILOpCode.Br_s or ILOpCode.Brfalse_s or ILOpCode.Brtrue_s or ILOpCode.Beq_s or ILOpCode.Bge_s or ILOpCode.Bgt_s
                    or ILOpCode.Ble_s or ILOpCode.Blt_s or ILOpCode.Bne_un_s or ILOpCode.Bge_un_s or ILOpCode.Bgt_un_s or ILOpCode.Ble_un_s
                    or ILOpCode.Blt_un_s or ILOpCode.Leave_s:
                    il.Offset += 1;
                    return;
                case ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca or ILOpCode.Stloc:
                    il.Offset += 2;
                    return;
                case ILOpCode.Ldc_i8 or ILOpCode.Ldc_r8:
                    il.Offset += 8;
                    return;
                case ILOpCode.Ldc_i4 or ILOpCode.Ldc_r4
                    or ILOpCode.Br or ILOpCode.Brfalse or ILOpCode.Brtrue or ILOpCode.Beq or ILOpCode.Bge or ILOpCode.Bgt or ILOpCode.Ble
                    or ILOpCode.Blt or ILOpCode.Bne_un or ILOpCode.Bge_un or ILOpCode.Bgt_un or ILOpCode.Ble_un or ILOpCode.Blt_un
                    or ILOpCode.Leave
                    or ILOpCode.Jmp or ILOpCode.Calli or ILOpCode.Ldftn or ILOpCode.Ldvirtftn
                    or ILOpCode.Ldstr or ILOpCode.Ldtoken or ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Stfld or ILOpCode.Ldsfld
                    or ILOpCode.Ldsflda or ILOpCode.Stsfld or ILOpCode.Box or ILOpCode.Unbox or ILOpCode.Unbox_any or ILOpCode.Castclass
                    or ILOpCode.Isinst or ILOpCode.Newarr or ILOpCode.Ldelema or ILOpCode.Ldelem or ILOpCode.Stelem or ILOpCode.Ldobj
                    or ILOpCode.Stobj or ILOpCode.Cpobj or ILOpCode.Initobj or ILOpCode.Mkrefany or ILOpCode.Refanyval or ILOpCode.Sizeof
                    or ILOpCode.Constrained:
                    il.Offset += 4;
                    return;
            }
        }
    }
}
