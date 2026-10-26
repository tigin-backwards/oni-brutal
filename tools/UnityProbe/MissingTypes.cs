// What a method gives when its IL names a type its assembly no longer has (lines "PROBE ... MT
// ..."): built against ProbeGone v1 (Gone/v1), run with v2 (Gone/v2) in the mod folder, as a mod
// built before U52 runs where the game's Worker is gone (Endless Science fix's prefix calls
// `Worker Workable.get_worker()`). Each case is a method of its own, called twice: the exception's
// type, message and first stack frame, and whether any of the method's body ran. Then a
// component and a ScriptableObject without a parameterless constructor (Trash Cans' storage
// components have only `AutoFluidTrashCans(State)`): whether AddComponent, CreateInstance and
// Instantiate make one, what their field initializers left, and which messages run. A
// `MissingTypesProbeDone` canvas appears at the end.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProbeGone;
using UnityEngine;

namespace SemanticsProbe
{
    public class NoCtorMb : MonoBehaviour
    {
        public int Init = 7;
        public string Text = "init";
        [NonSerialized] public int Hidden = 9;
        public NoCtorMb(int value) { Init = value; MissingTypes.L("NoCtorMb ctor ran"); }
        void Awake() => MissingTypes.L("NoCtorMb Awake Init=" + Init + " Text=" + (Text ?? "null") + " Hidden=" + Hidden);
        void OnEnable() => MissingTypes.L("NoCtorMb OnEnable");
        void Start() => MissingTypes.L("NoCtorMb Start");
    }

    public class InitBase : MonoBehaviour
    {
        public int BaseInit = 3;
        public InitBase() { MissingTypes.L("InitBase ctor ran"); }
    }

    public class NoCtorDerived : InitBase
    {
        public int Own = 4;
        public NoCtorDerived(int value) { Own = value; }
        void Awake() => MissingTypes.L("NoCtorDerived Awake BaseInit=" + BaseInit + " Own=" + Own);
    }

    public class NoCtorSo : ScriptableObject
    {
        public int Init = 7;
        public NoCtorSo(int value) { Init = value; }
        void Awake() => MissingTypes.L("NoCtorSo Awake Init=" + Init);
        void OnEnable() => MissingTypes.L("NoCtorSo OnEnable");
    }

    public class MissingTypes : MonoBehaviour
    {
        public static void L(string s) => Rec.Log("MT " + s);

        static string Esc(string s) => s == null ? "null" : s.Replace("\r", "\\r").Replace("\n", "\\n");

        static int ran;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CallReturning(bool flag) { ran++; if (flag) L("Get " + (Keeper.Get() == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void FieldOfType(bool flag) { ran++; if (flag) L("Field " + (Keeper.Field == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void IsInst(object o) { ran++; L("isinst " + (o is Gone)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void NewObj(bool flag) { ran++; if (flag) L("new " + new Gone()); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Generic(bool flag) { ran++; if (flag) L("list " + new List<Gone>().Count); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TypeOf() { ran++; L("typeof " + typeof(Gone).Name); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ArrayLocal(bool flag) { ran++; Gone[] items = flag ? new Gone[1] : null; L("array " + (items == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void RemovedMethod(bool flag) { ran++; if (flag) Keeper.Removed(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TakesGone(Gone g) { ran++; L("param " + (g == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CallsTakesGone() { ran++; TakesGone(null); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CastClass(object o) { ran++; L("castclass " + ((Gone)o == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void NewArr(bool flag) { ran++; if (flag) L("newarr " + new Gone[0].Length); }

        static T Id<T>(T value) => value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericMethod(bool flag) { ran++; if (flag) L("Id " + (Id<Gone>(null) == null)); }

        static Gone MakeGone() => null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CallsMakeGone(bool flag) { ran++; if (flag) L("MakeGone " + (MakeGone() == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TakeParam(bool flag) { ran++; if (flag) Keeper.Take(null); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CatchGone(bool flag)
        {
            ran++;
            try { if (flag) L("try"); }
            catch (GoneException) { L("caught"); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void FieldThenCall(bool flag) { ran++; if (flag) L("both " + (Keeper.Field == null) + (Keeper.Get() == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CallThenField(bool flag) { ran++; if (flag) L("both " + (Keeper.Get() == null) + (Keeper.Field == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void IsInstThenCall(object o) { ran++; L("both " + (o is Gone) + (Keeper.Get() == null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CaughtInside(bool flag)
        {
            ran++;
            try { if (flag) L("Get " + (Keeper.Get() == null)); }
            catch (Exception e) { L("caught inside " + e.GetType().Name); }
        }

        static void Case(string name, System.Action call)
        {
            for (int i = 1; i <= 2; i++)
            {
                ran = 0;
                try
                {
                    call();
                    L(name + " call " + i + " returned ran=" + ran);
                }
                catch (Exception e)
                {
                    var trace = (e.StackTrace ?? "").Split('\n');
                    L(name + " call " + i + " ran=" + ran + " " + e.GetType().FullName + " \"" + Esc(e.Message) + "\" first frame \""
                        + Esc(trace[0].Trim()) + "\" frames " + trace.Length);
                }
            }
        }

        static T Capture<T>(string label, Func<T> make)
        {
            var main = new List<string>();
            Application.LogCallback on = (c, st, ty) => { if (!c.StartsWith("PROBE")) main.Add(ty + " \"" + Esc(c) + "\""); };
            Application.logMessageReceived += on;
            T got = default;
            try { got = make(); }
            catch (Exception e) { L(label + " threw " + e.GetType().FullName + " \"" + Esc(e.Message) + "\""); }
            finally { Application.logMessageReceived -= on; }
            L(label + " log " + main.Count + (main.Count > 0 ? " " + string.Join(" | ", main) : ""));
            return got;
        }

        int frame;
        GameObject holder;

        void Start()
        {
            L("=== missing types");
            Case("CallReturning", () => CallReturning(false));
            Case("FieldOfType", () => FieldOfType(false));
            Case("IsInst", () => IsInst(new object()));
            Case("NewObj", () => NewObj(false));
            Case("Generic", () => Generic(false));
            Case("TypeOf", TypeOf);
            Case("ArrayLocal", () => ArrayLocal(false));
            Case("RemovedMethod", () => RemovedMethod(false));
            Case("CallsTakesGone", CallsTakesGone);
            Case("CaughtInside", () => CaughtInside(true));
            Case("CastClass", () => CastClass(null));
            Case("NewArr", () => NewArr(false));
            Case("GenericMethod", () => GenericMethod(false));
            Case("CallsMakeGone", () => CallsMakeGone(false));
            Case("TakeParam", () => TakeParam(false));
            Case("CatchGone", () => CatchGone(false));
            Case("FieldThenCall", () => FieldThenCall(false));
            Case("CallThenField", () => CallThenField(false));
            Case("IsInstThenCall", () => IsInstThenCall(null));

            L("=== no parameterless constructor");
            holder = new GameObject("NoCtorHolder");
            var mb = Capture("AddComponent NoCtorMb", () => holder.AddComponent<NoCtorMb>());
            L("NoCtorMb result refnull=" + ((object)mb == null) + " ==null=" + (mb == null)
                + (mb is null ? "" : " Init=" + mb.Init + " Text=" + (mb.Text ?? "null") + " Hidden=" + mb.Hidden + " enabled=" + mb.enabled
                   + " components=" + holder.GetComponents<Component>().Length));
            var derived = Capture("AddComponent NoCtorDerived", () => holder.AddComponent<NoCtorDerived>());
            L("NoCtorDerived result ==null=" + (derived == null) + (derived is null ? "" : " BaseInit=" + derived.BaseInit + " Own=" + derived.Own));
            var byType = Capture("AddComponent(Type) NoCtorMb", () => holder.AddComponent(typeof(NoCtorMb)));
            L("AddComponent(Type) result ==null=" + (byType == null));
            if (mb != null)
            {
                mb.Init = 5;
                mb.Text = "set";
                mb.Hidden = 6;
                var clone = Capture("Instantiate", () => Instantiate(holder));
                var copy = clone == null ? null : clone.GetComponent<NoCtorMb>();
                L("Instantiate copy ==null=" + (copy == null) + (copy is null ? "" : " Init=" + copy.Init + " Text=" + (copy.Text ?? "null") + " Hidden=" + copy.Hidden));
            }
            var so = Capture("CreateInstance NoCtorSo", () => ScriptableObject.CreateInstance<NoCtorSo>());
            L("NoCtorSo result refnull=" + ((object)so == null) + " ==null=" + (so == null) + (so is null ? "" : " Init=" + so.Init));
            var soByType = Capture("CreateInstance(Type) NoCtorSo", () => ScriptableObject.CreateInstance(typeof(NoCtorSo)));
            L("CreateInstance(Type) result ==null=" + (soByType == null));
            var inactive = new GameObject("NoCtorInactive");
            inactive.SetActive(false);
            var off = Capture("AddComponent NoCtorMb inactive", () => inactive.AddComponent<NoCtorMb>());
            L("inactive result ==null=" + (off == null));
            Capture("activate inactive", () => { inactive.SetActive(true); return 0; });
        }

        void Update()
        {
            if (++frame == 3)
            {
                L("=== done");
                var done = new GameObject("MissingTypesProbeDone", typeof(RectTransform));
                DontDestroyOnLoad(done);
                done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
