// What a method gives when its IL names a member its type no longer has (lines "PROBE ... MM
// ..."): built against ProbeGone v1 (Gone/v1), run with v2 (Gone/v2) in the mod folder, as
// Trapdoors & Floor Hatches' OnSpawn calls SimMessages.SetCellProperties(int, byte), which the
// game no longer has. Each case is a method of its own naming one missing member (static and
// instance methods, a constructor, a property, a changed parameter type, generic types and
// methods, nested types, by-ref and array parameters, fields), called twice: the exception's
// type, message, first stack frame and ToString(), and whether any of the method ran. Then the
// game's way of logging one (KMonoBehaviour.Spawn's catch and DebugUtil.LogExceptionCallstack),
// Debug.LogException and Debug.LogError(e.ToString()), each with what reaches the log callback.
// A `MissingMembersProbeDone` canvas appears at the end.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProbeGone;
using UnityEngine;

namespace SemanticsProbe
{
    public class SpawnLike
    {
        public virtual void OnSpawn() { }

        public void Spawn()
        {
            try { OnSpawn(); }
            catch (Exception e)
            {
                DebugUtil.LogExceptionCallstack(null, "Error in SpawnLike.OnSpawn at (1.00, 2.00, 3.00)", e.ToString(), e);
            }
        }
    }

    public class SpawnLikeHatch : SpawnLike
    {
        public override void OnSpawn()
        {
            MissingMembers.L("SpawnLikeHatch.OnSpawn ran");
            GlobalKeeper.SetCellProperties(1, 8);
        }
    }

    public class MissingMembers : MonoBehaviour
    {
        public static void L(string s) => Rec.Log("MM " + s);

        static string Esc(string s) => s == null ? "null" : s.Replace("\r", "\\r").Replace("\n", "\\n");

        static int ran;
        static int sink;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GlobalStatic(bool flag) { ran++; if (flag) GlobalKeeper.SetCellProperties(1, 8); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GlobalNested(bool flag) { ran++; if (flag) GlobalKeeper.Nested.Removed(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GlobalField(bool flag) { ran++; if (flag) sink = GlobalKeeper.Field; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void StaticRemoved(bool flag) { ran++; if (flag) Keeper.Removed(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Instance(Keeper k) { ran++; if (k != null) sink = k.InstanceRemoved("x"); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Virtual(Keeper k) { ran++; if (k != null) k.VirtualRemoved(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Ctor(bool flag) { ran++; if (flag) L("ctor " + new Keeper(5)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Property(bool flag) { ran++; if (flag) sink = Keeper.RemovedProp; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ChangedParam(bool flag) { ran++; if (flag) Keeper.Changed(3); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericMethod(bool flag) { ran++; if (flag) sink = Keeper.RemovedGeneric(1, null); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericMethodRef(bool flag) { ran++; if (flag) L("g " + Keeper.RemovedGeneric<string>("a", null)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Refs(bool flag)
        {
            ran++;
            if (flag) { int a = 0; L("refs " + Keeper.RemovedRefs(ref a, out float b, null, null, null, null) + b); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericTypeStatic(bool flag) { ran++; if (flag) Box<int>.StaticRemoved(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericTypeInstance(Box<string> box) { ran++; if (box != null) box.Removed("x"); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericTypeGenericMethod(bool flag) { ran++; if (flag) sink = Box<string>.Map("a", 1); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Nested(bool flag) { ran++; if (flag) Keeper.Inner.Removed(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ValueType(bool flag) { ran++; if (flag) { var p = new Pair(); p.Removed(); } }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void StaticField(bool flag) { ran++; if (flag) sink = Keeper.RemovedStatic; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void StaticFieldStore(bool flag) { ran++; if (flag) Keeper.RemovedStatic = 2; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void InstanceField(Keeper k) { ran++; if (k != null) L("f " + k.RemovedField); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void RetypedField(bool flag) { ran++; if (flag) sink = Keeper.Retyped; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void NestedField(bool flag) { ran++; if (flag) sink = Keeper.Inner.Field; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericTypeField(Box<string> box) { ran++; if (box != null) L("gf " + box.Field); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericTypeStaticField(bool flag) { ran++; if (flag) sink = Box<int>.StaticField; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void FieldThenCall(bool flag) { ran++; if (flag) { sink = Keeper.RemovedStatic; Keeper.Removed(); } }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CallThenField(bool flag) { ran++; if (flag) { Keeper.Removed(); sink = Keeper.RemovedStatic; } }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TypeThenMember(object o) { ran++; L("both " + (o is Gone)); Keeper.Removed(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MemberThenType(object o) { ran++; Keeper.Removed(); L("both " + (o is Gone)); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CaughtInside(bool flag)
        {
            ran++;
            try { if (flag) Keeper.Removed(); }
            catch (Exception e) { L("caught inside " + e.GetType().Name); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Outer() { ran++; GlobalStatic(true); }

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
                        + Esc(trace[0].Trim()) + "\" frames " + trace.Length + (i == 1 ? " tostring \"" + Esc(e.ToString()) + "\"" : ""));
                }
            }
        }

        static void Logged(string label, System.Action log)
        {
            var main = new List<string>();
            Application.LogCallback on = (c, st, ty) => { if (!c.StartsWith("PROBE")) main.Add(ty + " \"" + Esc(c) + "\" stack \"" + Esc(st) + "\""); };
            Application.logMessageReceived += on;
            try { log(); }
            catch (Exception e) { L(label + " threw " + e.GetType().FullName + " \"" + Esc(e.Message) + "\""); }
            finally { Application.logMessageReceived -= on; }
            L(label + " log " + main.Count + (main.Count > 0 ? " " + string.Join(" | ", main) : ""));
        }

        static Exception Caught(System.Action call)
        {
            try { call(); }
            catch (Exception e) { return e; }
            return null;
        }

        int frame;

        void Start()
        {
            L("=== missing members");
            Case("GlobalStatic", () => GlobalStatic(false));
            Case("GlobalNested", () => GlobalNested(false));
            Case("GlobalField", () => GlobalField(false));
            Case("StaticRemoved", () => StaticRemoved(false));
            Case("Instance", () => Instance(null));
            Case("Virtual", () => Virtual(null));
            Case("Ctor", () => Ctor(false));
            Case("Property", () => Property(false));
            Case("ChangedParam", () => ChangedParam(false));
            Case("GenericMethod", () => GenericMethod(false));
            Case("GenericMethodRef", () => GenericMethodRef(false));
            Case("Refs", () => Refs(false));
            Case("GenericTypeStatic", () => GenericTypeStatic(false));
            Case("GenericTypeInstance", () => GenericTypeInstance(null));
            Case("GenericTypeGenericMethod", () => GenericTypeGenericMethod(false));
            Case("Nested", () => Nested(false));
            Case("ValueType", () => ValueType(false));
            Case("StaticField", () => StaticField(false));
            Case("StaticFieldStore", () => StaticFieldStore(false));
            Case("InstanceField", () => InstanceField(null));
            Case("RetypedField", () => RetypedField(false));
            Case("NestedField", () => NestedField(false));
            Case("GenericTypeField", () => GenericTypeField(null));
            Case("GenericTypeStaticField", () => GenericTypeStaticField(false));
            Case("FieldThenCall", () => FieldThenCall(false));
            Case("CallThenField", () => CallThenField(false));
            Case("TypeThenMember", () => TypeThenMember(null));
            Case("MemberThenType", () => MemberThenType(null));
            Case("CaughtInside", () => CaughtInside(false));
            Case("Outer", Outer);

            L("=== logging");
            Logged("Spawn", () => new SpawnLikeHatch().Spawn());
            Logged("Spawn again", () => new SpawnLikeHatch().Spawn());
            var e = Caught(() => GlobalStatic(false));
            Logged("LogException", () => Debug.LogException(e));
            Logged("LogError ToString", () => Debug.LogError(e.ToString()));
            Logged("LogExceptionCallstack", () => DebugUtil.LogExceptionCallstack(null, "Error in probe", e.ToString(), e));
        }

        void Update()
        {
            if (++frame == 3)
            {
                L("=== done");
                var done = new GameObject("MissingMembersProbeDone", typeof(RectTransform));
                DontDestroyOnLoad(done);
                done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
