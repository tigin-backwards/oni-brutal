// Delegates serialized (lines "DJ ..."): what the game's Newtonsoft.Json 7.0.1 writes for an
// object holding a delegate, as OniExtract's export does (issue #22), and what a delegate's
// ISerializable.GetObjectData puts in a SerializationInfo. Newtonsoft serializes an ISerializable
// value through GetObjectData; on CoreCLR, MulticastDelegate.GetObjectData throws.
//
// Cases: a static method's delegate, an instance method's, a multicast one (static + instance +
// static again), a closure (a lambda capturing a local), a lambda capturing nothing and a generic
// method's (Same<int>). For each:
// "DJ json <case> <settings> = <json>" (or "! <exception>"), for the default settings, Indented,
// ReferenceLoopHandling.Ignore and TypeNameHandling.All; "DJ info <case> ..." for GetObjectData's
// entries (name, declared type, value's type, value), the info's FullTypeName and AssemblyName,
// and each nested DelegateEntry's fields; "DJ method ..." for MethodInfos' own GetObjectData
// and JSON (generic, by-ref, array, nested and generic-type cases, for Signature2's format);
// "DJ back <case> ..." for JsonConvert.DeserializeObject of the JSON and a BinaryFormatter round
// trip (and the delegate's result when called).
//
// Run alone with PROBE_SCENARIO=delegatejson (at mod load).
//
// Measured 2026-10-29: see docs/ARCHITECTURE.md D48.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

namespace SemanticsProbe
{
    public class DelegateTarget
    {
        public int Add = 3;
        public string Label = "target";
        public int Plus(int x) { return x + Add; }
    }

    public class DelegateHolder
    {
        public string Name = "holder";
        public Func<int, int> Fn;
    }

    public static class DelegateJson
    {
        public static int Twice(int x) { return x * 2; }
        public static T Same<T>(T x) { return x; }
        public static List<string> Mixed(List<int> a, ref int b, int[] c, string d, Dictionary<string, int[]> e, int[,] f) { return null; }
        public class Inner { public static int Nested(Inner x, float y) { return 0; } }
        public class Box<T> { public T Get(T x, List<T> y) { return x; } }

        static void Log(string s) { Rec.Log("DJ " + s); }

        static string Show(object v)
        {
            if (v == null) return "null";
            try { return v.ToString().Replace("\n", "\\n"); }
            catch (Exception e) { return "!" + e.GetType().Name; }
        }

        public static void Run()
        {
            int captured = 7;
            var target = new DelegateTarget();
            var cases = new List<KeyValuePair<string, Func<int, int>>>
            {
                new KeyValuePair<string, Func<int, int>>("static", Twice),
                new KeyValuePair<string, Func<int, int>>("instance", target.Plus),
                new KeyValuePair<string, Func<int, int>>("multicast", (Func<int, int>)Delegate.Combine(new Func<int, int>(Twice), new Func<int, int>(target.Plus), new Func<int, int>(Twice))),
                new KeyValuePair<string, Func<int, int>>("closure", x => x + captured),
                new KeyValuePair<string, Func<int, int>>("lambda", x => x - 1),
                new KeyValuePair<string, Func<int, int>>("generic", Same<int>),
            };
            var settings = new List<KeyValuePair<string, JsonSerializerSettings>>
            {
                new KeyValuePair<string, JsonSerializerSettings>("default", null),
                new KeyValuePair<string, JsonSerializerSettings>("indented", new JsonSerializerSettings { Formatting = Formatting.Indented }),
                new KeyValuePair<string, JsonSerializerSettings>("loopignore", new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }),
                new KeyValuePair<string, JsonSerializerSettings>("typenames", new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }),
            };
            Log("runtime " + Show(Type.GetType("Mono.Runtime") != null ? "mono" : "other") + " newtonsoft " + typeof(JsonConvert).Assembly.FullName);
            foreach (var c in cases)
            {
                var d = c.Value;
                Log("case " + c.Key + " targets " + string.Join(",", Array.ConvertAll(d.GetInvocationList(), x => x.Target == null ? "null" : x.Target.GetType().FullName))
                    + " methods " + string.Join(",", Array.ConvertAll(d.GetInvocationList(), x => x.Method.DeclaringType.FullName + "::" + x.Method.Name)));
                foreach (var s in settings)
                {
                    string json = null;
                    try
                    {
                        json = s.Value == null ? JsonConvert.SerializeObject(new DelegateHolder { Fn = d }) : JsonConvert.SerializeObject(new DelegateHolder { Fn = d }, s.Value);
                        Log("json " + c.Key + " " + s.Key + " = " + json.Replace("\r", "").Replace("\n", "\\n"));
                    }
                    catch (Exception e) { Log("json " + c.Key + " " + s.Key + " ! " + e.GetType().FullName + ": " + e.Message); }
                    try
                    {
                        json = s.Value == null ? JsonConvert.SerializeObject(d) : JsonConvert.SerializeObject(d, s.Value);
                        Log("bare " + c.Key + " " + s.Key + " = " + json.Replace("\r", "").Replace("\n", "\\n"));
                    }
                    catch (Exception e) { Log("bare " + c.Key + " " + s.Key + " ! " + e.GetType().FullName + ": " + e.Message); continue; }
                    if (s.Key != "default" && s.Key != "typenames") continue;
                    try
                    {
                        var back = s.Value == null ? JsonConvert.DeserializeObject<Func<int, int>>(json) : JsonConvert.DeserializeObject<Func<int, int>>(json, s.Value);
                        Log("back json " + c.Key + " " + s.Key + " = " + (back == null ? "null" : back.GetType().FullName + " (5)=" + back(5)));
                    }
                    catch (Exception e) { Log("back json " + c.Key + " " + s.Key + " ! " + e.GetType().FullName + ": " + e.Message); }
                }
                Info(c.Key, d);
                try
                {
                    var stream = new MemoryStream();
                    new BinaryFormatter().Serialize(stream, d);
                    stream.Position = 0;
                    var back = (Func<int, int>)new BinaryFormatter().Deserialize(stream);
                    Log("back binary " + c.Key + " " + stream.Length + " bytes = " + back.GetType().FullName + " (5)=" + back(5) + " invocation " + back.GetInvocationList().Length);
                }
                catch (Exception e) { Log("back binary " + c.Key + " ! " + e.GetType().FullName + ": " + e.Message); }
            }
            var methods = new List<KeyValuePair<string, MethodInfo>>
            {
                new KeyValuePair<string, MethodInfo>("twice", typeof(DelegateJson).GetMethod("Twice")),
                new KeyValuePair<string, MethodInfo>("same", typeof(DelegateJson).GetMethod("Same").MakeGenericMethod(typeof(int))),
                new KeyValuePair<string, MethodInfo>("samedef", typeof(DelegateJson).GetMethod("Same")),
                new KeyValuePair<string, MethodInfo>("mixed", typeof(DelegateJson).GetMethod("Mixed")),
                new KeyValuePair<string, MethodInfo>("nested", typeof(Inner).GetMethod("Nested")),
                new KeyValuePair<string, MethodInfo>("boxget", typeof(Box<int>).GetMethod("Get")),
                new KeyValuePair<string, MethodInfo>("inherited", typeof(DelegateTarget).GetMethod("ToString")),
            };
            foreach (var m in methods)
            {
                var method = m.Value;
                Log("method " + m.Key + " isserializable " + (method is ISerializable) + " type " + method.GetType().FullName + " reflected " + method.ReflectedType.FullName + " tostring " + method);
                try
                {
                    var info = new SerializationInfo(method.GetType(), new FormatterConverter());
                    ((ISerializable)method).GetObjectData(info, new StreamingContext(StreamingContextStates.All));
                    Entries("method " + m.Key, info);
                }
                catch (Exception e) { Log("method " + m.Key + " ! " + e.GetType().FullName + ": " + e.Message); }
                try { Log("method " + m.Key + " json = " + JsonConvert.SerializeObject(method)); }
                catch (Exception e) { Log("method " + m.Key + " json ! " + e.GetType().FullName + ": " + e.Message); }
            }
            System.Action none = null;
            Log("json nulldelegate = " + JsonConvert.SerializeObject(new { A = none }));
            Log("done");
        }

        static void Info(string name, Delegate d)
        {
            try
            {
                var info = new SerializationInfo(d.GetType(), new FormatterConverter());
                d.GetObjectData(info, new StreamingContext(StreamingContextStates.All));
                Entries("info " + name, info);
            }
            catch (Exception e) { Log("info " + name + " ! " + e.GetType().FullName + ": " + e.Message); }
        }

        static void Entries(string prefix, SerializationInfo info)
        {
            Log(prefix + " fulltypename " + info.FullTypeName + " assembly " + info.AssemblyName + " count " + info.MemberCount);
            foreach (SerializationEntry e in info)
            {
                Log(prefix + " entry " + e.Name + " : " + e.ObjectType.FullName + " = " + (e.Value == null ? "null" : e.Value.GetType().FullName) + " " + Show(e.Value));
                var entry = e.Value;
                int depth = 0;
                while (entry != null && entry.GetType().Name == "DelegateEntry" && depth++ < 8)
                {
                    object next = null;
                    foreach (var f in entry.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var v = f.GetValue(entry);
                        Log(prefix + "   " + depth + " " + (f.IsPublic ? "public " : "private ") + f.FieldType.FullName + " " + f.Name + " = " + Show(v));
                        if (f.Name == "delegateEntry") next = v;
                    }
                    entry = next;
                }
            }
        }
    }
}
