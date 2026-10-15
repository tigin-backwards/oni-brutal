using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace OniBrutal.Host
{
    /// Where every assembly the game asks for comes from (ARCHITECTURE §2):
    ///
    /// 1. The .NET framework (the trusted platform assemblies) answers first, so Mono's BCL copies
    ///    in Managed/ (mscorlib, System.*, Mono.Security, I18N…) are never loaded.
    /// 2. Unity engine assemblies (UnityEngine, UnityEngine.*Module) resolve to our facades in
    ///    engine/, which forward to OniBrutal.UnityEngine; the engine bridge ships with the host.
    /// 3. Everything else — the game and its third-party libraries, including Unity's package
    ///    code (UnityEngine.UI, TextMeshPro, Timeline) — loads from Managed/.
    ///
    /// The game runs in the default load context, so its Type.GetType, Assembly.Load and
    /// AppDomain queries behave as they did under Unity.
    internal static class AssemblyPolicy
    {
        /// Unity's own support packages listed in ScriptingAssemblies.json. They implement engine
        /// features (Burst, native collections, profiler integration) that our engine replaces, and
        /// no game assembly references them, so they are not loaded.
        static readonly HashSet<string> unitySupportPackages = new(StringComparer.Ordinal)
        {
            "Unity.Burst", "Unity.Burst.Unsafe", "Unity.Collections", "Unity.Collections.LowLevel.ILSupport",
            "Unity.Mathematics", "Unity.MemoryProfiler", "Unity.Multiplayer.Center.Common", "Unity.Profiling.Core",
        };

        /// Mono class libraries shipped in Managed/ that the framework does not carry. They target
        /// Mono's runtime internals; their roles are filled by .NET (code pages: the host registers
        /// CodePagesEncodingProvider). Never loaded.
        static readonly HashSet<string> monoClassLibraries = new(StringComparer.Ordinal)
        {
            "I18N", "I18N.West", "Mono.Security", "System.ComponentModel.Composition",
            "System.ServiceModel.Internals", "System.EnterpriseServices",
        };

        static string managedDir = "";
        static string pluginsDir = "";
        static string engineDir = "";

        public static void Install(string gameDataDir, string hostDir)
        {
            managedDir = Path.Combine(gameDataDir, "Managed");
            pluginsDir = Path.Combine(gameDataDir, "Plugins");
            engineDir = Path.Combine(hostDir, "engine");
            AssemblyLoadContext.Default.Resolving += Resolve;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveNative;
        }

        static bool IsEngineAssembly(string name) => File.Exists(Path.Combine(engineDir, name + ".dll"));

        /// Unity's engine modules: only our implementation may provide them. A module we have no
        /// facade for is one nothing references, so it is never loaded.
        static bool IsUnityEngineModule(string name) => name == "UnityEngine" || (name.StartsWith("UnityEngine.", StringComparison.Ordinal) && name.EndsWith("Module", StringComparison.Ordinal));

        static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            var simple = name.Name;
            if (string.IsNullOrEmpty(simple)) return null;
            if (IsEngineAssembly(simple)) return context.LoadFromAssemblyPath(Path.Combine(engineDir, simple + ".dll"));
            if (IsUnityEngineModule(simple)) return null;
            if (unitySupportPackages.Contains(simple) || monoClassLibraries.Contains(simple)) return null;
            var path = Path.Combine(managedDir, simple + ".dll");
            if (!File.Exists(path)) return null;
            var image = MonoFieldSemantics.Adjust(path);
            return image == null ? context.LoadFromAssemblyPath(path) : context.LoadFromStream(new MemoryStream(image));
        }

        /// The game's native plugins live in Plugins/ (the Unity player searches there).
        static IntPtr ResolveNative(Assembly requester, string name)
        {
            foreach (var candidate in new[] { $"lib{name}.so", $"{name}.so", name })
            {
                var path = Path.Combine(pluginsDir, candidate);
                if (File.Exists(path)) return NativeLibrary.Load(path);
            }
            return IntPtr.Zero;
        }

        /// Loads the scripting assemblies up front, as the Unity player does: the game snapshots
        /// AppDomain.GetAssemblies() early and resolves saved type names against that snapshot.
        public static List<Assembly> PreloadScriptingAssemblies(string gameDataDir)
        {
            var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDir, "ScriptingAssemblies.json")));
            var loaded = new List<Assembly>();
            foreach (var entry in json.RootElement.GetProperty("names").EnumerateArray())
            {
                var simple = Path.GetFileNameWithoutExtension(entry.GetString()!);
                if (unitySupportPackages.Contains(simple) || monoClassLibraries.Contains(simple)) continue;
                if (IsFrameworkAssembly(simple)) continue;
                if (IsUnityEngineModule(simple)) continue; // ours load on demand through the facades
                if (!IsEngineAssembly(simple) && !File.Exists(Path.Combine(managedDir, simple + ".dll"))) continue;
                loaded.Add(Assembly.Load(new AssemblyName(simple)));
            }
            return loaded;
        }

        /// Assemblies of the shared framework (the trusted platform assemblies that live in the
        /// runtime directory or in packages the host ships, as opposed to the host's own projects).
        static readonly HashSet<string> framework = new(
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(p => !Path.GetDirectoryName(p)!.Equals(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)
                            || Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal))
                .Select(p => Path.GetFileNameWithoutExtension(p)!),
            StringComparer.OrdinalIgnoreCase);

        static bool IsFrameworkAssembly(string simple) => framework.Contains(simple);
    }
}
