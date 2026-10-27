// How a mod's assembly references bind when the assembly is not beside the mod but one of that
// name is loaded already (lines "AB ..."): with and without a strong name, at other versions,
// with several versions offered, a reference in other case, a copy beside the user. The
// assemblies are ProbeBindGen's (tools/UnityProbe/Binding), in the probe's mod folder's binding/,
// one folder per case. Each folder is loaded as KMod loads a mod's (DLLLoader.LoadDLLs, which
// ours replaces the Assembly.LoadFrom of), or the user alone with Assembly.LoadFrom where a copy
// beside it must not be loaded first. A user's Call() returns its library's Who() ("BindA 1.0
// A1": name, version, folder), so each answer names the copy its reference bound to; a failure
// is "!" with the exception's type and message.
//
// Lines: "AB <case> = <answer>"; "AB load <folder> = <assembly: name version folder, ...>";
// "AB event <name> from <requesting>" for each AppDomain.AssemblyResolve raised.
//
// Run alone with PROBE_SCENARIO=asmbind (at mod load).
//
// Measured 2026-10-26 (U57 original on Unity's MonoBleedingEdge, Linux amd64; ours on .NET 10
// CoreCLR): see docs/ARCHITECTURE.md D45.
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SemanticsProbe
{
    public static class AssemblyBinding
    {
        static string root;

        static void L(string s) => Rec.Log("AB " + s);

        public static void Run(KMod.Mod owner, string modPath)
        {
            root = Path.Combine(modPath, "binding");
            if (!Directory.Exists(root))
            {
                L("no " + root + " (dotnet run --project tools/UnityProbe/Binding -- DIR)");
                return;
            }
            Handlers();
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                L("event " + e.Name + " from " + (e.RequestingAssembly == null ? "null" : e.RequestingAssembly.GetName().Name) + " | " + Frames());
                return null;
            };

            // Unsigned BindA: 1.0 loaded first, then 2.0 as another mod's copy.
            Load(owner, "A1");
            Load(owner, "A2");
            Case("A3.LoadFrom", () => Describe(Assembly.LoadFrom(Path.Combine(root, "A3", "BindA.dll"))));
            Case("A3.LoadBytes", () => Describe(Assembly.Load(File.ReadAllBytes(Path.Combine(root, "A3", "BindA.dll")))));
            Case("A.LoadName2", () => Describe(Assembly.Load("BindA, Version=2.0.0.0")));
            Case("A.LoadName", () => Describe(Assembly.Load("BindA")));
            Case("A.loaded", () => Loaded("BindA"));
            Call(owner, "UA2", "UseA2");
            Call(owner, "UA3", "UseA3");
            Call(owner, "UA0", "UseA0");
            Call(owner, "UAcase", "UseAcase");
            // UE ships BindA 2.0 beside its user of 2.0 (KMod loads both).
            Call(owner, "UE", "UseE");
            Case("A.loaded.after", () => Loaded("BindA"));

            // Unsigned BindB, loaded nowhere: UB's user of 2.0 alone, with 1.0 beside it; then UB3's
            // user of 3.0, without a copy.
            CallAlone("UB", "UseB");
            Case("B.loaded", () => Loaded("BindB"));
            Call(owner, "UB3", "UseB3");
            // BindN is nowhere.
            Call(owner, "UN", "UseN");

            // BindC, strong-named: 1.0 loaded; users of 2.0, 0.5, 1.0 without a key and 1.0 with
            // another key's token; then 2.0 loaded as another mod's copy, and a user of 2.0.
            Load(owner, "C1");
            Call(owner, "UC2", "UseC2");
            Call(owner, "UC05", "UseC05");
            Call(owner, "UCnk", "UseCnk");
            Call(owner, "UCbad", "UseCbad");
            Call(owner, "UC1", "UseC1");
            Case("C.LoadName", () => Describe(Assembly.Load("BindC")));
            Case("C.LoadName2", () => Describe(Assembly.Load("BindC, Version=2.0.0.0, Culture=neutral, PublicKeyToken=" + Token("BindC"))));
            Load(owner, "C1b");
            Load(owner, "C2");
            Case("C.loaded", () => Loaded("BindC"));
            Call(owner, "UC2b", "UseC2b");
            Case("C2.LoadFrom", () => Describe(Assembly.LoadFrom(Path.Combine(root, "C2", "BindC.dll"))));
            Case("C.loaded.after", () => Loaded("BindC"));

            // BindD, strong-named, loaded nowhere: a user of 2.0 alone with 1.0 beside it; a user of
            // 1.0 alone with 1.0 beside it.
            CallAlone("UD", "UseD");
            CallAlone("UD1", "UseD1");
            Case("D.loaded", () => Loaded("BindD"));
            L("done");
        }

        /// The AssemblyResolve handlers registered before the probe's, in invocation order (Mono's
        /// AppDomain keeps them in its field-like event; .NET's AppDomain forwards to
        /// AssemblyLoadContext's).
        static void Handlers()
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var field = typeof(AppDomain).GetField("AssemblyResolve", any);
            object owner = AppDomain.CurrentDomain;
            if (field == null)
            {
                field = Type.GetType("System.Runtime.Loader.AssemblyLoadContext")?.GetField("AssemblyResolve", any);
                owner = null;
            }
            var del = field?.GetValue(owner) as Delegate;
            var list = del == null ? new Delegate[0] : del.GetInvocationList();
            L("handlers = " + list.Length + (field == null ? " (no field)" : ""));
            foreach (var d in list)
                L("handler " + (d.Method.DeclaringType == null ? "?" : d.Method.DeclaringType.FullName) + "." + d.Method.Name + " in " + d.Method.Module.Assembly.GetName().Name);
        }

        /// The frames above an AssemblyResolve handler, up to the probe's own code.
        static string Frames()
        {
            var trace = new System.Diagnostics.StackTrace(2, false);
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < trace.FrameCount && names.Count < 10; i++)
            {
                var m = trace.GetFrame(i).GetMethod();
                if (m == null) { names.Add("?"); continue; }
                if (m.DeclaringType == typeof(AssemblyBinding)) break;
                names.Add((m.DeclaringType == null ? "" : m.DeclaringType.Name + ".") + m.Name);
            }
            return string.Join(" < ", names.ToArray());
        }

        /// The public key token of the loaded assembly of that simple name, in hex.
        static string Token(string name)
        {
            var a = AppDomain.CurrentDomain.GetAssemblies().First(x => x.GetName().Name == name);
            return string.Concat(a.GetName().GetPublicKeyToken().Select(b => b.ToString("x2")).ToArray());
        }

        static void Case(string name, Func<string> run)
        {
            string r;
            try { r = run(); }
            catch (Exception e) { r = Failure(e); }
            L(name + " = " + r);
        }

        static string Failure(Exception e)
        {
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return "!" + e.GetType().Name + " " + e.Message.Replace(root, "BINDING").Replace('\n', ' ');
        }

        /// KMod's load of a folder: the assemblies it returns.
        static Assembly[] Load(KMod.Mod owner, string folder)
        {
            // KMod.DLLLoader is internal.
            var loadDlls = HarmonyLib.AccessTools.Method("KMod.DLLLoader:LoadDLLs");
            var data = loadDlls.Invoke(null, new object[] { owner, "SemanticsProbe.binding." + folder, Path.Combine(root, folder), false });
            var set = data == null ? null : HarmonyLib.Traverse.Create(data).Field("dlls").GetValue<System.Collections.Generic.HashSet<Assembly>>();
            var dlls = set == null ? new Assembly[0] : set.OrderBy(a => a.GetName().Name, StringComparer.Ordinal).ToArray();
            L("load " + folder + " = " + (data == null ? "null (KMod dropped it)" : string.Join(", ", dlls.Select(Describe).ToArray())));
            return dlls;
        }

        /// A user's Call(), its folder loaded by KMod.
        static void Call(KMod.Mod owner, string folder, string user)
        {
            var dlls = Load(owner, folder);
            Case(folder + "." + user, () =>
            {
                var assembly = dlls.FirstOrDefault(a => a.GetName().Name == user);
                if (assembly == null) return "!not loaded";
                return (string)assembly.GetType(user).GetMethod("Call").Invoke(null, null);
            });
        }

        /// A user's Call(), the user alone loaded with Assembly.LoadFrom.
        static void CallAlone(string folder, string user) => Case(folder + "." + user + ".alone", () =>
            (string)Assembly.LoadFrom(Path.Combine(root, folder, user + ".dll")).GetType(user).GetMethod("Call").Invoke(null, null));

        /// Name, version and folder of an assembly (its Location's folder under binding/).
        static string Describe(Assembly a)
        {
            string where;
            try
            {
                var location = a.Location;
                where = string.IsNullOrEmpty(location) ? "(no location)" : location.StartsWith(root) ? Path.GetFileName(Path.GetDirectoryName(location)) : location;
            }
            catch (Exception e) { where = Failure(e); }
            var n = a.GetName();
            var token = n.GetPublicKeyToken();
            return n.Name + " " + n.Version + (token != null && token.Length > 0 ? " signed" : "") + " " + where;
        }

        /// The assemblies of that simple name AppDomain.GetAssemblies lists, in its order.
        static string Loaded(string name)
        {
            var list = AppDomain.CurrentDomain.GetAssemblies().Where(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            return list.Length + ": " + string.Join(", ", list.Select(Describe).ToArray());
        }
    }
}
