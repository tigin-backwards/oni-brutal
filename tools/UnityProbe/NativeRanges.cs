// NativeArray ranges (lines "NR ..."): what the player does with indices and ranges outside a
// NativeArray: the indexer, Copy's overloads, CopyFrom and CopyTo with other lengths, and
// GetSubArray past the end or with a negative start or length. Each case logs "try" before it
// runs (a crash shows which), then the exception's type and message or what the arrays hold.
//
// Out-of-range access goes through views (GetSubArray) of a larger backing array, so what an
// unchecked access reads or writes is the backing array's own memory, which is logged. The cases
// that would write past a managed array, and a negative length, run last.
//
// Run alone with PROBE_SCENARIO=nativerange (at mod load), through scripts/e2e.py with a spec
// `from: main-menu` and SemanticsProbe.dll in a copy of OniMcp's mod folder; the original player
// only (ours has no NativeArray constructor). ARCHITECTURE D46 records what it measured;
// NativeArrayRangeTests encodes it.
//
// Measured 2026-10-27 in the original player: no case throws. Reads and writes go to the
// memory before or past the array; GetSubArray(-1, 2) views from the element before, and
// GetSubArray(0, -1) has Length -1; the two-array Copy, CopyFrom and CopyTo copy the source's
// length whatever the destination's (past a managed array's end too), the five-argument Copy
// the length given. A negative length to a NativeArray destination writes garbage over it;
// into a managed array (`Copy(v, 0, m3, 0, -1)`) it crashed the player, so that case is left
// out.
using System;
using System.Linq;
using Unity.Collections;

namespace SemanticsProbe
{
    public static class NativeRanges
    {
        static void L(string s) => Rec.Log("NR " + s);

        static NativeArray<int> backing;

        static string Ints(NativeArray<int> a) => string.Join(",", a.ToArray());
        static string Ints(int[] a) => string.Join(",", a);

        /// A backing array of 32 ints (1000 + index); views of 8 at 8 and at 20.
        static void Reset()
        {
            for (int i = 0; i < 32; i++) backing[i] = 1000 + i;
        }

        static NativeArray<int> V => backing.GetSubArray(8, 8);
        static NativeArray<int> W => backing.GetSubArray(20, 8);

        static void Case(string name, Func<string> run)
        {
            L("try " + name);
            Reset();
            string result;
            try { result = "ok " + run(); }
            catch (Exception e) { result = "EXCEPTION " + e.GetType().FullName + " | " + e.Message.Replace("\n", "\\n"); }
            L("case " + name + " -> " + result + " | backing " + Ints(backing));
        }

        public static void Run()
        {
            backing = new NativeArray<int>(32, Allocator.Persistent);

            // The indexer.
            Case("index read v[8]", () => V[8].ToString());
            Case("index read v[-1]", () => V[-1].ToString());
            Case("index read v[23]", () => V[23].ToString());
            Case("index write v[8]=-1", () => { var v = V; v[8] = -1; return ""; });
            Case("index write v[-1]=-2", () => { var v = V; v[-1] = -2; return ""; });
            var small = new NativeArray<int>(4, Allocator.Persistent);
            Case("index read standalone a[4] of 4", () => small[4].ToString());
            Case("index read standalone a[-1] of 4", () => small[-1].ToString());
            small.Dispose();

            // GetSubArray.
            Case("GetSubArray(4,8) of v", () => { var s = V.GetSubArray(4, 8); return "len " + s.Length + " " + Ints(s); });
            Case("GetSubArray(8,0) of v", () => { var s = V.GetSubArray(8, 0); return "len " + s.Length; });
            Case("GetSubArray(9,0) of v", () => { var s = V.GetSubArray(9, 0); return "len " + s.Length; });
            Case("GetSubArray(-1,2) of v", () => { var s = V.GetSubArray(-1, 2); return "len " + s.Length + " " + Ints(s); });
            Case("GetSubArray(0,-1) of v", () => { var s = V.GetSubArray(0, -1); return "len " + s.Length; });

            // Copy between NativeArrays.
            Case("Copy(v,6,w,0,4) src past end", () => { NativeArray<int>.Copy(V, 6, W, 0, 4); return ""; });
            Case("Copy(v,0,w,6,4) dst past end", () => { NativeArray<int>.Copy(V, 0, W, 6, 4); return ""; });
            Case("Copy(v,-1,w,0,2) src negative", () => { NativeArray<int>.Copy(V, -1, W, 0, 2); return ""; });
            Case("Copy(v,0,w,-1,2) dst negative", () => { NativeArray<int>.Copy(V, 0, W, -1, 2); return ""; });
            Case("Copy(v,0,w,0,9) length past both", () => { NativeArray<int>.Copy(V, 0, W, 0, 9); return ""; });
            Case("Copy(v,w) same length", () => { NativeArray<int>.Copy(V, W); return ""; });
            Case("Copy(v,sub4) longer src", () => { NativeArray<int>.Copy(V, W.GetSubArray(0, 4)); return ""; });
            Case("Copy(sub4,w) shorter src", () => { NativeArray<int>.Copy(V.GetSubArray(0, 4), W); return ""; });

            // Copy from a managed array into a NativeArray.
            var m3 = new[] { 1, 2, 3 };
            Case("Copy(m3,0,w,6,3) dst past end", () => { NativeArray<int>.Copy(m3, 0, W, 6, 3); return ""; });
            Case("Copy(m3,1,w,0,3) src past end", () => { NativeArray<int>.Copy(m3, 1, W, 0, 3); return ""; });
            Case("Copy(m3,-1,w,0,2) src negative", () => { NativeArray<int>.Copy(m3, -1, W, 0, 2); return ""; });
            Case("Copy(m3,0,w,-1,2) dst negative", () => { NativeArray<int>.Copy(m3, 0, W, -1, 2); return ""; });
            Case("Copy(m10,w) longer managed src", () => { NativeArray<int>.Copy(Enumerable.Range(1, 10).ToArray(), W); return ""; });
            Case("Copy(m3,w) shorter managed src", () => { NativeArray<int>.Copy(m3, W); return ""; });

            // CopyFrom and CopyTo with other lengths.
            Case("v.CopyFrom(m10) longer", () => { V.CopyFrom(Enumerable.Range(1, 10).ToArray()); return ""; });
            Case("v.CopyFrom(m3) shorter", () => { V.CopyFrom(m3); return ""; });
            Case("v.CopyFrom(sub4) shorter native", () => { V.CopyFrom(W.GetSubArray(0, 4)); return ""; });
            Case("sub4.CopyFrom(v) longer native", () => { W.GetSubArray(0, 4).CopyFrom(V); return ""; });
            Case("v.CopyTo(m10) longer", () => { var m = new int[10]; V.CopyTo(m); return Ints(m); });
            Case("v.CopyTo(sub4) shorter native", () => { V.CopyTo(W.GetSubArray(0, 4)); return ""; });
            Case("sub4.CopyTo(v) longer native", () => { W.GetSubArray(0, 4).CopyTo(V); return ""; });

            // Into managed arrays past their end, then a negative length.
            Case("Copy(v,0,m3,0,3) in range", () => { var m = new int[3]; NativeArray<int>.Copy(V, 0, m, 0, 3); return Ints(m); });
            Case("Copy(v,6,m3,0,3) src past end", () => { var m = new int[3]; NativeArray<int>.Copy(V, 6, m, 0, 3); return Ints(m); });
            Case("Copy(v,-1,m3,0,2) src negative", () => { var m = new int[3]; NativeArray<int>.Copy(V, -1, m, 0, 2); return Ints(m); });
            Case("Copy(v,0,m3,1,3) dst past end", () => { var m = new int[3]; NativeArray<int>.Copy(V, 0, m, 1, 3); return Ints(m); });
            Case("Copy(v,0,m3,-1,2) dst negative", () => { var m = new int[3]; NativeArray<int>.Copy(V, 0, m, -1, 2); return Ints(m); });
            Case("Copy(v,m3) shorter managed dst", () => { var m = new int[3]; NativeArray<int>.Copy(V, m); return Ints(m); });
            Case("v.CopyTo(m3) shorter", () => { var m = new int[3]; V.CopyTo(m); return Ints(m); });
            Case("Copy(v,0,w,0,-1) negative length", () => { NativeArray<int>.Copy(V, 0, W, 0, -1); return ""; });
            Case("Copy(m3,0,w,0,-1) negative length", () => { NativeArray<int>.Copy(m3, 0, W, 0, -1); return ""; });

            backing.Dispose();
            L("done");
        }
    }
}
