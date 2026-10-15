using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniBrutal.Assets.Files;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Serialization;

namespace OniBrutal.Host
{
    /// M1 gate: every object in every data file parses to exactly its byte size — natives with
    /// the release type trees, script payloads with the layouts derived from the game's types.
    internal static class AssetCheck
    {
        const int MonoBehaviourId = 114, MonoScriptId = 115;

        static readonly string[] files =
        [
            "globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
            "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets",
            "Resources/unity default resources", "Resources/unity_builtin_extra",
        ];

        /// Traces the field layout of one script object ("file:pathId").
        public static int Dump(string dataDir, string spec)
        {
            var parts = spec.Split(':');
            var file = SerializedFile.Open(Path.Combine(dataDir, parts[0]));
            var obj = file.ObjectsById[long.Parse(parts[1])];
            var r = file.OpenObject(obj);
            var baseFields = TypeTreeReader.ReadFields(TypeTreeDatabase.Get(MonoBehaviourId).Tree!, r);
            var script = ResolveScript(file, baseFields.PPtr("m_Script"), LoadMonoScripts(Path.Combine(dataDir, "globalgamemanagers.assets")));
            var type = FindType(script)!;
            Console.WriteLine($"{type.FullName}: {r.Length} bytes, script data from {r.Position}");
            ScriptData.Trace = Console.Out;
            try { ScriptData.SkipFields(SerializedLayout.ScriptFields(type), r); }
            catch (Exception e) { Console.WriteLine("stopped: " + e.Message); }
            Console.WriteLine($"remaining {r.Remaining}");
            return 0;
        }

        public static int Run(string dataDir)
        {
            var scripts = LoadMonoScripts(Path.Combine(dataDir, "globalgamemanagers.assets"));
            var typeCache = new Dictionary<long, Type?>();
            var failures = new List<string>();
            var unresolved = new SortedSet<string>(StringComparer.Ordinal);
            int natives = 0, behaviours = 0;
            var perClass = new Dictionary<string, int>();
            long lastScript = 0;

            var opened = files.Select(f => SerializedFile.Open(Path.Combine(dataDir, f))).ToList();
            var bundle = UnityFsArchive.Open(Path.Combine(dataDir, "StreamingAssets", "hires_base_bundle"));
            foreach (var node in bundle.Nodes.Where(n => !n.Path.EndsWith(".resS")))
                opened.Add(SerializedFile.Parse(node.Path, bundle.Read(node)));

            foreach (var file in opened)
            {
                foreach (var obj in file.Objects)
                {
                    var type = file.TypeOf(obj);
                    var r = file.OpenObject(obj);
                    try
                    {
                        if (type.ClassId != MonoBehaviourId)
                        {
                            if (type.Tree != null) TypeTreeReader.ReadObject(type.Tree, r);
                            else TypeTreeReader.ReadNative(type.ClassId, r);
                            natives++;
                            continue;
                        }
                        var baseFields = TypeTreeReader.ReadFields(TypeTreeDatabase.Get(MonoBehaviourId).Tree!, r);
                        var scriptRef = baseFields.PPtr("m_Script");
                        if (scriptRef.IsNull)
                        {
                            if (r.Remaining != 0) failures.Add($"{file.Name}:{obj.PathId} MonoBehaviour without script has {r.Remaining} bytes of data");
                            behaviours++;
                            continue;
                        }
                        lastScript = scriptRef.PathId;
                        var script = ResolveScript(file, scriptRef, scripts);
                        if (!typeCache.TryGetValue(scriptRef.PathId, out var managed))
                            typeCache[scriptRef.PathId] = managed = FindType(script);
                        if (managed == null)
                        {
                            unresolved.Add($"{script.Assembly}:{script.FullName}");
                            continue;
                        }
                        ScriptData.SkipFields(SerializedLayout.ScriptFields(managed), r);
                        if (r.Remaining != 0) throw new InvalidDataException($"{r.Remaining} bytes left of {r.Length}");
                        behaviours++;
                        perClass[managed.FullName!] = perClass.GetValueOrDefault(managed.FullName!) + 1;
                    }
                    catch (Exception e)
                    {
                        var name = type.ClassId == MonoBehaviourId ? "script" : TypeTreeDatabase.TryGet(type.ClassId, out var ci) ? ci.Name : type.ClassId.ToString();
                        if (type.ClassId == MonoBehaviourId && typeCache.TryGetValue(lastScript, out var t) && t != null) name = t.FullName!;
                        failures.Add($"{file.Name}:{obj.PathId} {name}: {e.Message}");
                    }
                }
            }

            Console.WriteLine($"native objects: {natives}, script objects: {behaviours} ({perClass.Count} classes)");
            var classIds = opened.SelectMany(f => f.Objects.Select(o => f.TypeOf(o).ClassId)).Distinct().OrderBy(i => i);
            foreach (var id in classIds)
            {
                if (id is MonoBehaviourId or MonoScriptId or 1 or 4 or 224 or 104 or 157 or 196) continue;
                var name = TypeTreeDatabase.Get(id).Name;
                if (OniBrutal.Engine.Assets.Binders.EngineTypeOf(name) == null) Console.WriteLine($"NO ENGINE TYPE {name} ({id})");
            }
            foreach (var u in unresolved) Console.WriteLine("UNRESOLVED script " + u);
            foreach (var f in failures.Take(60)) Console.WriteLine("FAIL " + f);
            Console.WriteLine($"{failures.Count} failures, {unresolved.Count} unresolved scripts");
            return failures.Count == 0 && unresolved.Count == 0 ? 0 : 1;
        }

        sealed record ScriptInfo(string Assembly, string Namespace, string ClassName)
        {
            public string FullName => Namespace.Length > 0 ? Namespace + "." + ClassName : ClassName;
        }

        static Dictionary<long, ScriptInfo> LoadMonoScripts(string path)
        {
            var file = SerializedFile.Open(path);
            var result = new Dictionary<long, ScriptInfo>();
            foreach (var obj in file.Objects)
            {
                if (file.TypeOf(obj).ClassId != MonoScriptId) continue;
                var f = TypeTreeReader.ReadNative(MonoScriptId, file.OpenObject(obj));
                result[obj.PathId] = new ScriptInfo(f.String("m_AssemblyName"), f.String("m_Namespace"), f.String("m_ClassName"));
            }
            return result;
        }

        static ScriptInfo ResolveScript(SerializedFile file, PPtr pptr, Dictionary<long, ScriptInfo> scripts)
        {
            // Player files reference MonoScripts in globalgamemanagers.assets; a bundle carries its own.
            if (pptr.FileId == 0)
            {
                var obj = file.ObjectsById[pptr.PathId];
                var f = file.TypeOf(obj).Tree != null
                    ? TypeTreeReader.ReadObject(file.TypeOf(obj).Tree!, file.OpenObject(obj))
                    : TypeTreeReader.ReadNative(MonoScriptId, file.OpenObject(obj));
                return new ScriptInfo(f.String("m_AssemblyName"), f.String("m_Namespace"), f.String("m_ClassName"));
            }
            var external = file.Externals[pptr.FileId - 1].Path;
            if (!external.EndsWith("globalgamemanagers.assets", StringComparison.Ordinal))
                throw new InvalidDataException("MonoScript outside globalgamemanagers.assets: " + external);
            return scripts[pptr.PathId];
        }

        static Type? FindType(ScriptInfo s)
        {
            // MonoScripts name assemblies with or without ".dll" (UnityEngine.UI has no suffix).
            var asmName = s.Assembly.EndsWith(".dll", StringComparison.Ordinal) ? s.Assembly[..^4] : s.Assembly;
            try
            {
                return System.Reflection.Assembly.Load(asmName).GetType(s.FullName, false);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
