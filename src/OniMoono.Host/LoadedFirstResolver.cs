using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using Cecil = Mono.Cecil;

namespace OniMoono.Host
{
    /// Resolves a reference of a mod being rewritten to the assembly the runtime bound it to (an
    /// assembly already loaded with that name: the game's current types), then from the search
    /// folders.
    internal sealed class LoadedFirstResolver : Cecil.DefaultAssemblyResolver
    {
        readonly Dictionary<string, Cecil.AssemblyDefinition> loaded = new(StringComparer.OrdinalIgnoreCase);

        /// A resolver for the mod's assembly at `path`: loaded assemblies first, then the mod's
        /// folder and the game's reference folders.
        public static LoadedFirstResolver ForMod(string path)
        {
            var resolver = new LoadedFirstResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(path)!);
            foreach (var dir in AssemblyPolicy.ReferenceDirectories) resolver.AddSearchDirectory(dir);
            return resolver;
        }

        public override Cecil.AssemblyDefinition Resolve(Cecil.AssemblyNameReference name, Cecil.ReaderParameters parameters)
        {
            if (loaded.TryGetValue(name.Name, out var cached)) return cached;
            foreach (var a in AssemblyLoadContext.Default.Assemblies)
                if (string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase) && a.Location is { Length: > 0 } location && File.Exists(location))
                    return loaded[name.Name] = Cecil.AssemblyDefinition.ReadAssembly(location, new Cecil.ReaderParameters { AssemblyResolver = this });
            return base.Resolve(name, parameters);
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var a in loaded.Values) a.Dispose();
            loaded.Clear();
            base.Dispose(disposing);
        }
    }
}
