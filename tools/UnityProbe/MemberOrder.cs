// Reflection member order (lines "MO ..."): the order in which Type's GetFields, GetMethods,
// GetProperties, GetEvents, GetNestedTypes, GetConstructors, GetInterfaces, GetMembers and
// GetCustomAttributes return a type's members, for types of the probe whose metadata order
// tells the candidate rules apart (instance, static and constant fields interleaved; a generic
// base class and its base; overrides, new slots and statics among methods; properties hiding by
// name; several attributes), and for CritterCondoInteractMontior (the state machine whose
// parameter order differed, issue #16).
//
// Each line is "MO <type> <call> = <member>|<member>|...", a member as Declaring.Name (methods
// with their parameter count). At the main menu, once the mods have made their patches, the same
// lists for every type of Assembly-CSharp and Assembly-CSharp-firstpass, and the fields
// MyAttributeManager injects (MyCmpAdd/Get/Req, "MyCmp"), go to a file, "<type>\t<call>\t<list>"
// per line, in PROBE_OUT (else the temporary folder) as memberorder-mono.tsv or
// memberorder-coreclr.tsv; its path is logged. mo-check.py compares two such files.
//
// Run alone with PROBE_SCENARIO=memberorder (at mod load, the file at the main menu). With
// PROBE_SCENARIO=smorder it logs instead each state machine the game makes ("MO SM <type> =
// <idx>:<parameter>|... fields <Parameter fields as GetFields lists them>"), in a colony.
//
// Measured 2026-10-26 (U57 original on Unity's MonoBleedingEdge, Linux amd64; ours on .NET 10
// CoreCLR), with the same DLL in both players: see docs/ARCHITECTURE.md D39.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#pragma warning disable CS0649, CS0169, CS0067
namespace SemanticsProbe
{
    public static class MemberOrder
    {
        const BindingFlags Every = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        const BindingFlags States = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        static string Name(MemberInfo m)
        {
            string owner = m.DeclaringType == null ? "" : m.DeclaringType.Name + ".";
            if (m is MethodBase b) return owner + m.Name + "/" + b.GetParameters().Length;
            if (m is Type t) return t.Name;
            return owner + m.Name;
        }

        static IEnumerable<(string, Func<Type, IEnumerable<string>>)> Calls()
        {
            yield return ("GetFields(every)", t => t.GetFields(Every).Select(Name));
            yield return ("GetFields(states)", t => t.GetFields(States).Select(Name));
            yield return ("GetFields()", t => t.GetFields().Select(Name));
            yield return ("GetFields(declared)", t => t.GetFields(Declared).Select(Name));
            yield return ("GetMethods(every)", t => t.GetMethods(Every).Select(Name));
            yield return ("GetMethods(declared)", t => t.GetMethods(Declared).Select(Name));
            yield return ("GetProperties(every)", t => t.GetProperties(Every).Select(Name));
            yield return ("GetEvents(every)", t => t.GetEvents(Every).Select(Name));
            yield return ("GetNestedTypes(every)", t => t.GetNestedTypes(Every).Select(Name));
            yield return ("GetConstructors(every)", t => t.GetConstructors(Every).Select(Name));
            yield return ("GetInterfaces()", t => t.GetInterfaces().Select(i => i.Name));
            yield return ("GetMembers(every)", t => t.GetMembers(Every).Select(m => m.MemberType.ToString()[0] + ":" + Name(m)));
            yield return ("GetCustomAttributes(true)", t => t.GetCustomAttributes(true).Select(a => a.GetType().Name));
            yield return ("MyCmp", t => MyCmpFields(t).Select(Name));
        }

        // The fields MyAttributeManager.GetFieldDatas finds for a component type, in its order:
        // the order MyCmp's FindOrAddComponent and Util.SpawnComponent run in.
        static List<FieldInfo> MyCmpFields(Type type)
        {
            var found = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(KMonoBehaviour) && t != typeof(object); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (f.GetCustomAttributes(false).Any(a => a is MyCmpAdd || a is MyCmpGet || a is MyCmpReq) && !found.Any(g => g.Name == f.Name))
                        found.Add(f);
            return found;
        }

        static string Join(Func<IEnumerable<string>> list)
        {
            try { return string.Join("|", list().ToArray()); }
            catch (Exception e) { return "!" + e.GetType().Name; }
        }

        // PROBE_SCENARIO=smorder: each state machine the game makes, its parameters as
        // CreateStates numbered them and its fields as GetFields lists them at that point.
        public static void Hook(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(StateMachineManager), nameof(StateMachineManager.CreateStateMachine), new[] { typeof(Type) }),
                postfix: new HarmonyLib.HarmonyMethod(typeof(MemberOrder), nameof(CreatedPostfix)));
        }

        static readonly HashSet<Type> logged = new HashSet<Type>();

        static void CreatedPostfix(Type type, StateMachine __result)
        {
            if (__result == null || !logged.Add(type)) return;
            var parameters = (StateMachine.Parameter[])typeof(StateMachine).GetField("parameters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__result);
            Rec.Log("MO SM " + type.FullName + " = " + string.Join("|", parameters.Select(p => p.idx + ":" + p.name).ToArray())
                + " fields " + Join(() => type.GetFields(States).Where(f => f.FieldType.IsSubclassOf(typeof(StateMachine.Parameter))).Select(f => f.Name)));
        }

        public static void Run()
        {
            var types = new[]
            {
                typeof(Mixed), typeof(Leaf), typeof(Middle<int>), typeof(Root<int>), typeof(Methods), typeof(MethodsBase),
                typeof(Props), typeof(PropsBase), typeof(Evented), typeof(Attributed), typeof(Implementer),
                typeof(CritterCondoInteractMontior), typeof(GameStateMachine<CritterCondoInteractMontior, CritterCondoInteractMontior.Instance, IStateMachineTarget, CritterCondoInteractMontior.Def>),
            };
            foreach (var t in types)
                foreach (var (call, list) in Calls())
                    Rec.Log("MO " + t.Name + " " + call + " = " + Join(() => list(t)));
            // The parameters StateMachine.CreateStates numbers, as StateMachineManager makes a
            // state machine.
            Rec.Log("MO CritterCondoInteractMontior CreateStates = " + Join(() =>
            {
                var sm = (StateMachine)Activator.CreateInstance(typeof(CritterCondoInteractMontior));
                sm.CreateStates(sm);
                var parameters = (StateMachine.Parameter[])typeof(StateMachine).GetField("parameters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sm);
                return parameters.Select(p => p.idx + ":" + p.name);
            }));
            // A member looked up by name before the full list.
            Rec.Log("MO Late GetField(b)+GetFields(every) = " + (typeof(Late).GetField("b") != null) + " " + Join(() => typeof(Late).GetFields(Every).Select(Name)));
            Rec.Log("MO Late GetMethod(B)+GetMethods(declared) = " + (typeof(Late).GetMethod("B") != null) + " " + Join(() => typeof(Late).GetMethods(Declared).Select(Name)));
        }

        // At the main menu, once every mod has made its patches: the game's types.
        public static void HookDump(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(MainMenu), "OnSpawn"),
                postfix: new HarmonyLib.HarmonyMethod(typeof(MemberOrder), nameof(Dump)));
        }

        static bool dumped;

        static void Dump()
        {
            if (dumped) return;
            dumped = true;
            string dir = System.Environment.GetEnvironmentVariable("PROBE_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
            string runtime = Type.GetType("Mono.Runtime") != null ? "mono" : "coreclr";
            string path = Path.Combine(dir, "memberorder-" + runtime + ".tsv");
            int count = 0;
            using (var w = new StreamWriter(path))
            {
                foreach (var assembly in new[] { typeof(Game).Assembly, typeof(KMonoBehaviour).Assembly })
                {
                    Type[] all;
                    try { all = assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { all = e.Types.Where(t => t != null).ToArray(); }
                    foreach (var t in all)
                    {
                        count++;
                        foreach (var (call, list) in Calls())
                            w.Write(t.FullName + "\t" + call + "\t" + Join(() => list(t)) + "\n");
                    }
                }
            }
            Rec.Log("MO file " + path + " types " + count);
            Rec.Log("MO done");
            var done = new UnityEngine.GameObject("MemberOrderProbeDone", typeof(UnityEngine.RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(done);
            done.AddComponent<UnityEngine.Canvas>().renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
        }

        // Instance, static and constant fields interleaved in metadata order.
        public class Mixed
        {
            public int i0;
            public static int s0;
            public const int c0 = 0;
            public int i1;
            public static readonly int r0 = 1;
            public const int c1 = 1;
            private int i2;
            private static int s1;
            public int I2 => i2 + s1;
        }

        public class Root<T>
        {
            public int rootA;
            public static int rootStatic;
            public object masterTarget;
            public const int RootConst = 3;
            protected int rootB;
            private int rootPrivate;
            public int P => rootPrivate;
        }

        public class Middle<T> : Root<T>
        {
            public static int middleStatic;
            public object middleField;
            public new int rootA;
        }

        public class Leaf : Middle<Leaf>
        {
            public object state0;
            public static int leafStatic;
            public const int LeafConst = 2;
            private object remaining;
            public object state1;
            public object R => remaining;
        }

        public class MethodsBase
        {
            public virtual void V0() { }
            public void N0() { }
            public virtual void V1() { }
            public static void S0() { }
            public virtual void V2() { }
            private void P0() { }
            public void Hidden() { P0(); }
        }

        public class Methods : MethodsBase
        {
            public static void S1() { }
            public override void V1() { }
            public void N1() { }
            public new virtual void V2() { }
            public override void V0() { }
            public new void Hidden() { }
            public void G<T>() { }
            static Methods() { }
            public Methods() { }
            public Methods(int x) { }
        }

        public class PropsBase
        {
            public int A { get; set; }
            public virtual int B { get; set; }
            public static int S { get; set; }
            public int C { get; set; }
        }

        public class Props : PropsBase
        {
            public new string C { get; set; }
            public static int T { get; set; }
            public override int B { get; set; }
            public int D { get; set; }
            public int this[int i] => i;
        }

        public class Evented : EventBase
        {
            public event System.Action E1;
            public static event System.Action S1;
            public event System.Action E0;
            public void Raise() { E1?.Invoke(); S1?.Invoke(); E0?.Invoke(); }
            public class NB { }
            private class NA { }
            public interface NI { }
            public struct NS { }
        }

        public class EventBase
        {
            public event System.Action Base0;
            public virtual event System.Action VirtualBase;
            public void RaiseBase() { Base0?.Invoke(); VirtualBase?.Invoke(); }
        }

        [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
        public class ZAttribute : Attribute { }
        [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
        public class AAttribute : Attribute { }
        [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
        public class MAttribute : Attribute { }

        [M, Z]
        public class AttributedBase { }

        [Z, A, M, A]
        public class Attributed : AttributedBase { }

        public interface IB { }
        public interface IA : IB { }
        public interface IC { }
        public interface ID<T> { }
        public class ImplementerBase : IC, ID<int> { }
        public class Implementer : ImplementerBase, IA, IComparable, ID<string> { public int CompareTo(object o) => 0; }

        public class Late
        {
            public int a;
            public int b;
            public int c;
            public void A() { }
            public void B() { }
            public void C() { }
        }
    }
}
