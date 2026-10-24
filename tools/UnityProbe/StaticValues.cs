// Static values mods read (lines "PROBE ... SV ..."): every public static property and field of
// Color and Color32, found by reflection, so the same DLL lists what each player has. Each line
// gives the name, the components' bits (hex) and their "R" form, and the colour as Color32.
// Then GarbageCollector.isIncremental, and a new TextMesh's alignment and what reads back after
// each value is set. Checked with sv-check.py (diff of two players, and
// the C# for the engine's table from a log).
//
// Run alone with PROBE_SCENARIO=statics (at mod load).
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SemanticsProbe
{
    public static class StaticValues
    {
        static void L(string s) => Rec.Log("SV " + s);

        static string F(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("X8") + "/" + f.ToString("R", CultureInfo.InvariantCulture);

        static string Describe(object value)
        {
            if (value is Color c)
            {
                Color32 b = c;
                return "Color " + F(c.r) + " " + F(c.g) + " " + F(c.b) + " " + F(c.a) + " c32 " + b.r + "," + b.g + "," + b.b + "," + b.a;
            }
            if (value is Color32 d) return "Color32 " + d.r + "," + d.g + "," + d.b + "," + d.a;
            return (value == null ? "null" : value.GetType().FullName + " " + value);
        }

        static void Dump(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var props = type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            var fields = type.GetFields(flags).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
            L("count " + type.Name + " properties " + props.Count + " fields " + fields.Count);
            foreach (var p in props)
            {
                string text;
                try
                {
                    var first = p.GetValue(null);
                    var second = p.GetValue(null);
                    text = Describe(first) + (Equals(first, second) ? "" : " SECOND " + Describe(second));
                }
                catch (Exception e) { text = "EXCEPTION " + (e.InnerException ?? e).GetType().Name; }
                L("prop " + type.Name + "." + p.Name + " " + text);
            }
            foreach (var f in fields) L("field " + type.Name + "." + f.Name + " " + Describe(f.GetValue(null)));
        }

        public static void Run()
        {
            Dump(typeof(Color));
            Dump(typeof(Color32));
            string gc;
            // In its own method: where the member is missing, only that method fails to compile.
            try { gc = Incremental(); }
            catch (Exception e) { gc = "EXCEPTION " + e.GetType().Name; }
            L("GarbageCollector.isIncremental " + gc);
            string mesh;
            try { mesh = MeshAlignment(); }
            catch (Exception e) { mesh = "EXCEPTION " + e.GetType().Name; }
            L("TextMesh.alignment " + mesh);
            L("done");
        }

        /// A new TextMesh's alignment, and what reads back after each value is set.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static string MeshAlignment()
        {
            var go = new GameObject("sv-textmesh");
            var mesh = go.AddComponent<TextMesh>();
            string s = "default " + mesh.alignment + "(" + (int)mesh.alignment + ")";
            foreach (var value in new[] { TextAlignment.Right, TextAlignment.Center, TextAlignment.Left, (TextAlignment)7 })
            {
                mesh.alignment = value;
                s += " set " + (int)value + " -> " + (int)mesh.alignment;
            }
            UnityEngine.Object.DestroyImmediate(go);
            return s;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static string Incremental() => UnityEngine.Scripting.GarbageCollector.isIncremental.ToString();
    }
}
