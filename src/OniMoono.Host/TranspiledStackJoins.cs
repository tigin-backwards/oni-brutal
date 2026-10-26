using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D43. ECMA-335 requires every path into an instruction to bring the same stack
    /// depth; CoreCLR's JIT refuses a method where one does not (InvalidProgramException when
    /// Harmony applies the patch). Mono's JIT checks only paths that bring values: a block left
    /// with an empty stack hands its successors nothing (mono/mini/method-to-ir.c,
    /// handle_stack_args returns at once for no values), so a join that one path reaches with
    /// values and another with none compiles, and code after the join, valid with the empty
    /// stack, never takes them. Transpilers that insert a test branching back with the tested
    /// value still on the stack produce such methods (Fixes and Tweaks' patch of
    /// Timelapser.SetPostionAndOrtho jumps to its loop's head with a BuildingComplete left over;
    /// the mod failed to load). Before D17's pass over the instructions Harmony's transpilers
    /// produce (CodeTranspiler.GetResult), each path that brings values to a join reached with an
    /// empty stack elsewhere pops them on its way there. A method whose depths differ otherwise,
    /// or whose code after such a join would take the values, is left as it is.
    internal static class TranspiledStackJoins
    {
        /// A path into a join: from the instruction at Source (by its branch, or falling through)
        /// with Count values the join does not take.
        readonly record struct Excess(int Source, bool Branch, int Count);

        public static void Even(List<CodeInstruction> code, ILGenerator generator, MethodBase method)
        {
            var excess = Find(code, method);
            if (excess == null || excess.Count == 0) return;
            // A conditional branch both of whose paths bring values, or a switch, is left alone.
            if (excess.GroupBy(e => e.Source).Any(g => g.Count() > 1)) return;
            foreach (var e in excess)
            {
                var opcode = code[e.Source].opcode;
                if (e.Branch && (opcode == OpCodes.Switch || code[e.Source].blocks.Count > 0)) return;
            }
            // From the last, so that the earlier sources keep their indices.
            foreach (var e in excess.OrderByDescending(e => e.Source))
            {
                var source = code[e.Source];
                if (!e.Branch)
                    code.InsertRange(e.Source + 1, Pops(e.Count));
                else if (source.opcode.FlowControl == FlowControl.Branch)
                {
                    var pops = Pops(e.Count);
                    pops[0].labels.AddRange(source.labels);
                    source.labels.Clear();
                    code.InsertRange(e.Source, pops);
                }
                else
                {
                    // bcond T  becomes  bcond Lt; br Lc; Lt: pop...; br T; Lc: (the next instruction)
                    var target = (Label)source.operand;
                    Label taken = generator.DefineLabel(), next = generator.DefineLabel();
                    source.operand = taken;
                    var pops = Pops(e.Count);
                    pops[0].labels.Add(taken);
                    var trampoline = new List<CodeInstruction> { new(OpCodes.Br, next) };
                    trampoline.AddRange(pops);
                    trampoline.Add(new CodeInstruction(OpCodes.Br, target));
                    code[e.Source + 1].labels.Add(next);
                    code.InsertRange(e.Source + 1, trampoline);
                }
            }
            EngineLog.Engine($"transpiled stack joins: {string.Join(", ", excess.Select(e => e.Count))} value(s) brought to a join reached with an empty stack elsewhere popped in {method.DeclaringType?.FullName}.{method.Name}");
        }

        static List<CodeInstruction> Pops(int count) => Enumerable.Range(0, count).Select(_ => new CodeInstruction(OpCodes.Pop)).ToList();

        /// The paths that bring values to a join some other path reaches with an empty stack, or
        /// null when the depths differ otherwise or the code cannot be followed. An empty list when
        /// the depths agree everywhere.
        static List<Excess>? Find(List<CodeInstruction> code, MethodBase method)
        {
            var index = new Dictionary<Label, int>();
            for (int i = 0; i < code.Count; i++)
                foreach (var label in code[i].labels) index[label] = i;
            // Joins taken at an empty stack; each pass that finds another starts again.
            var empty = new HashSet<int>();
            for (int pass = 0; pass < 64; pass++)
            {
                var result = Walk(code, method, index, empty, out int join);
                if (join < 0) return result;
                if (!empty.Add(join)) return null;
            }
            return null;
        }

        /// One walk of the code, joins in `empty` entered at depth 0. `join` is a join found to be
        /// reached at depth 0 and at another depth that is not yet in `empty` (-1 when none).
        static List<Excess>? Walk(List<CodeInstruction> code, MethodBase method, Dictionary<Label, int> index, HashSet<int> empty, out int join)
        {
            join = -1;
            var excess = new List<Excess>();
            var depths = Enumerable.Repeat(-1, code.Count).ToArray();
            var work = new Stack<(int At, int Depth, int Source, bool Branch)>();
            work.Push((0, 0, -1, false));
            for (int i = 0; i < code.Count; i++)
            {
                var handler = code[i].blocks.FirstOrDefault(b => TranspiledTryEntries.IsHandler(b.blockType));
                if (handler != null)
                    work.Push((i, handler.blockType is ExceptionBlockType.BeginCatchBlock or ExceptionBlockType.BeginExceptFilterBlock ? 1 : 0, -1, false));
            }
            while (work.Count > 0)
            {
                var (i, depth, source, branch) = work.Pop();
                while (i < code.Count)
                {
                    if (empty.Contains(i) && depth > 0)
                    {
                        if (source < 0) return null;
                        excess.Add(new Excess(source, branch, depth));
                        depth = 0;
                    }
                    if (depths[i] >= 0)
                    {
                        if (depths[i] != depth)
                        {
                            if (depths[i] != 0 && depth != 0) return null;
                            join = i;
                            return null;
                        }
                        break;
                    }
                    depths[i] = depth;
                    var instruction = code[i];
                    int pops = TranspiledTryEntries.Pops(instruction, method), pushes = TranspiledTryEntries.Pushes(instruction);
                    if (pops < 0 || pushes < 0 || depth < pops) return null;
                    depth += pushes - pops;
                    var opcode = instruction.opcode;
                    foreach (var label in TranspiledTryEntries.Targets(instruction))
                    {
                        if (!index.TryGetValue(label, out int target)) return null;
                        bool leave = opcode == OpCodes.Leave || opcode == OpCodes.Leave_S;
                        work.Push((target, leave ? 0 : depth, i, true));
                    }
                    if (opcode.FlowControl is FlowControl.Branch or FlowControl.Return or FlowControl.Throw) break;
                    if (opcode == OpCodes.Leave || opcode == OpCodes.Leave_S || opcode == OpCodes.Endfinally || opcode == OpCodes.Endfilter) break;
                    source = i;
                    branch = false;
                    i++;
                    // A handler is entered from its block's exception, not by falling into it.
                    if (i < code.Count && code[i].blocks.Any(b => TranspiledTryEntries.IsHandler(b.blockType))) break;
                }
            }
            return excess;
        }
    }
}
