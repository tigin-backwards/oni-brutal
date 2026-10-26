using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OniMoono.Engine;
using Cecil = Mono.Cecil;
using Cil = Mono.Cecil.Cil;

namespace OniMoono.Host
{
    /// ARCHITECTURE D30. Unity's Mono hands out a folder's entries sorted by their UTF-8 bytes
    /// on Linux and macOS (its libmono-native sorts each folder it reads); CoreLib hands them
    /// out in readdir order. True Tiles finds its texture packs with Directory.GetDirectories,
    /// and packs of one Order are kept in the order found: the settings list and the texture
    /// that wins differed. The game's assemblies and mods call these instead of Directory's and
    /// DirectoryInfo's Get* and Enumerate* (each overload; a DirectoryInfo method's instance is
    /// the first parameter), which list as .NET does and reorder as Mono does
    /// (OniMoono.Engine.MonoDirectoryOrder). The rewrite shares MonoConversions' pass over an
    /// assembly (and its cache of the game's).
    public static class MonoDirectoryListing
    {
        const SearchOption All = SearchOption.AllDirectories;

        static bool Recursive(SearchOption option) => option == All;
        // A depth limit is CoreLib's alone (Mono's options have none): such a listing stays .NET's.
        static bool Recursive(EnumerationOptions options) => options is { RecurseSubdirectories: true, MaxRecursionDepth: int.MaxValue };

        static T[] Array<T>(IEnumerable<T> entries) => new List<T>(entries).ToArray();

        // Directory: paths as .NET reports them.
        public static string[] GetFiles(string path) => MonoDirectoryOrder.Sorted(Directory.GetFiles(path));
        public static string[] GetFiles(string path, string searchPattern) => MonoDirectoryOrder.Sorted(Directory.GetFiles(path, searchPattern));
        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
            Recursive(searchOption) ? Array(EnumerateFiles(path, searchPattern, searchOption)) : MonoDirectoryOrder.Sorted(Directory.GetFiles(path, searchPattern, searchOption));
        public static string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) =>
            Recursive(enumerationOptions) ? Array(EnumerateFiles(path, searchPattern, enumerationOptions)) : MonoDirectoryOrder.Sorted(Directory.GetFiles(path, searchPattern, enumerationOptions));

        public static string[] GetDirectories(string path) => MonoDirectoryOrder.Sorted(Directory.GetDirectories(path));
        public static string[] GetDirectories(string path, string searchPattern) => MonoDirectoryOrder.Sorted(Directory.GetDirectories(path, searchPattern));
        public static string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) =>
            Recursive(searchOption) ? Array(EnumerateDirectories(path, searchPattern, searchOption)) : MonoDirectoryOrder.Sorted(Directory.GetDirectories(path, searchPattern, searchOption));
        public static string[] GetDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) =>
            Recursive(enumerationOptions) ? Array(EnumerateDirectories(path, searchPattern, enumerationOptions)) : MonoDirectoryOrder.Sorted(Directory.GetDirectories(path, searchPattern, enumerationOptions));

        public static string[] GetFileSystemEntries(string path) => MonoDirectoryOrder.Sorted(Directory.GetFileSystemEntries(path));
        public static string[] GetFileSystemEntries(string path, string searchPattern) => MonoDirectoryOrder.Sorted(Directory.GetFileSystemEntries(path, searchPattern));
        public static string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) =>
            Recursive(searchOption) ? Array(EnumerateFileSystemEntries(path, searchPattern, searchOption)) : MonoDirectoryOrder.Sorted(Directory.GetFileSystemEntries(path, searchPattern, searchOption));
        public static string[] GetFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) =>
            Recursive(enumerationOptions) ? Array(EnumerateFileSystemEntries(path, searchPattern, enumerationOptions)) : MonoDirectoryOrder.Sorted(Directory.GetFileSystemEntries(path, searchPattern, enumerationOptions));

        public static IEnumerable<string> EnumerateFiles(string path) => MonoDirectoryOrder.SortedLazily(Directory.EnumerateFiles(path));
        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern) => MonoDirectoryOrder.SortedLazily(Directory.EnumerateFiles(path, searchPattern));
        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var listing = Directory.EnumerateFiles(path, searchPattern, searchOption);
            return !Recursive(searchOption) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => Directory.EnumerateFiles(folder, expression, All), infos: false) ?? listing;
        }
        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
            var listing = Directory.EnumerateFiles(path, searchPattern, enumerationOptions);
            return !Recursive(enumerationOptions) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => Directory.EnumerateFiles(folder, expression, enumerationOptions), infos: false) ?? listing;
        }

        public static IEnumerable<string> EnumerateDirectories(string path) => MonoDirectoryOrder.SortedLazily(Directory.EnumerateDirectories(path));
        public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern) => MonoDirectoryOrder.SortedLazily(Directory.EnumerateDirectories(path, searchPattern));
        public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            var listing = Directory.EnumerateDirectories(path, searchPattern, searchOption);
            return !Recursive(searchOption) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => Directory.EnumerateDirectories(folder, expression, All), infos: false) ?? listing;
        }
        public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
            var listing = Directory.EnumerateDirectories(path, searchPattern, enumerationOptions);
            return !Recursive(enumerationOptions) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => Directory.EnumerateDirectories(folder, expression, enumerationOptions), infos: false) ?? listing;
        }

        public static IEnumerable<string> EnumerateFileSystemEntries(string path) => MonoDirectoryOrder.SortedLazily(Directory.EnumerateFileSystemEntries(path));
        public static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) => MonoDirectoryOrder.SortedLazily(Directory.EnumerateFileSystemEntries(path, searchPattern));
        public static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            var listing = Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            return !Recursive(searchOption) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => Directory.EnumerateFileSystemEntries(folder, expression, All), infos: false) ?? listing;
        }
        public static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
            var listing = Directory.EnumerateFileSystemEntries(path, searchPattern, enumerationOptions);
            return !Recursive(enumerationOptions) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => Directory.EnumerateFileSystemEntries(folder, expression, enumerationOptions), infos: false) ?? listing;
        }

        // DirectoryInfo: infos of the entries' full paths.
        public static FileInfo[] InfoGetFiles(DirectoryInfo self) => MonoDirectoryOrder.Sorted(self.GetFiles());
        public static FileInfo[] InfoGetFiles(DirectoryInfo self, string searchPattern) => MonoDirectoryOrder.Sorted(self.GetFiles(searchPattern));
        public static FileInfo[] InfoGetFiles(DirectoryInfo self, string searchPattern, SearchOption searchOption) =>
            Recursive(searchOption) ? Array(InfoEnumerateFiles(self, searchPattern, searchOption)) : MonoDirectoryOrder.Sorted(self.GetFiles(searchPattern, searchOption));
        public static FileInfo[] InfoGetFiles(DirectoryInfo self, string searchPattern, EnumerationOptions enumerationOptions) =>
            Recursive(enumerationOptions) ? Array(InfoEnumerateFiles(self, searchPattern, enumerationOptions)) : MonoDirectoryOrder.Sorted(self.GetFiles(searchPattern, enumerationOptions));

        public static DirectoryInfo[] InfoGetDirectories(DirectoryInfo self) => MonoDirectoryOrder.Sorted(self.GetDirectories());
        public static DirectoryInfo[] InfoGetDirectories(DirectoryInfo self, string searchPattern) => MonoDirectoryOrder.Sorted(self.GetDirectories(searchPattern));
        public static DirectoryInfo[] InfoGetDirectories(DirectoryInfo self, string searchPattern, SearchOption searchOption) =>
            Recursive(searchOption) ? Array(InfoEnumerateDirectories(self, searchPattern, searchOption)) : MonoDirectoryOrder.Sorted(self.GetDirectories(searchPattern, searchOption));
        public static DirectoryInfo[] InfoGetDirectories(DirectoryInfo self, string searchPattern, EnumerationOptions enumerationOptions) =>
            Recursive(enumerationOptions) ? Array(InfoEnumerateDirectories(self, searchPattern, enumerationOptions)) : MonoDirectoryOrder.Sorted(self.GetDirectories(searchPattern, enumerationOptions));

        public static FileSystemInfo[] InfoGetFileSystemInfos(DirectoryInfo self) => MonoDirectoryOrder.Sorted(self.GetFileSystemInfos());
        public static FileSystemInfo[] InfoGetFileSystemInfos(DirectoryInfo self, string searchPattern) => MonoDirectoryOrder.Sorted(self.GetFileSystemInfos(searchPattern));
        public static FileSystemInfo[] InfoGetFileSystemInfos(DirectoryInfo self, string searchPattern, SearchOption searchOption) =>
            Recursive(searchOption) ? Array(InfoEnumerateFileSystemInfos(self, searchPattern, searchOption)) : MonoDirectoryOrder.Sorted(self.GetFileSystemInfos(searchPattern, searchOption));
        public static FileSystemInfo[] InfoGetFileSystemInfos(DirectoryInfo self, string searchPattern, EnumerationOptions enumerationOptions) =>
            Recursive(enumerationOptions) ? Array(InfoEnumerateFileSystemInfos(self, searchPattern, enumerationOptions)) : MonoDirectoryOrder.Sorted(self.GetFileSystemInfos(searchPattern, enumerationOptions));

        public static IEnumerable<FileInfo> InfoEnumerateFiles(DirectoryInfo self) => MonoDirectoryOrder.SortedLazily(self.EnumerateFiles());
        public static IEnumerable<FileInfo> InfoEnumerateFiles(DirectoryInfo self, string searchPattern) => MonoDirectoryOrder.SortedLazily(self.EnumerateFiles(searchPattern));
        public static IEnumerable<FileInfo> InfoEnumerateFiles(DirectoryInfo self, string searchPattern, SearchOption searchOption)
        {
            var listing = self.EnumerateFiles(searchPattern, searchOption);
            return !Recursive(searchOption) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => new DirectoryInfo(folder).EnumerateFiles(expression, All), infos: true) ?? listing;
        }
        public static IEnumerable<FileInfo> InfoEnumerateFiles(DirectoryInfo self, string searchPattern, EnumerationOptions enumerationOptions)
        {
            var listing = self.EnumerateFiles(searchPattern, enumerationOptions);
            return !Recursive(enumerationOptions) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => new DirectoryInfo(folder).EnumerateFiles(expression, enumerationOptions), infos: true) ?? listing;
        }

        public static IEnumerable<DirectoryInfo> InfoEnumerateDirectories(DirectoryInfo self) => MonoDirectoryOrder.SortedLazily(self.EnumerateDirectories());
        public static IEnumerable<DirectoryInfo> InfoEnumerateDirectories(DirectoryInfo self, string searchPattern) => MonoDirectoryOrder.SortedLazily(self.EnumerateDirectories(searchPattern));
        public static IEnumerable<DirectoryInfo> InfoEnumerateDirectories(DirectoryInfo self, string searchPattern, SearchOption searchOption)
        {
            var listing = self.EnumerateDirectories(searchPattern, searchOption);
            return !Recursive(searchOption) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => new DirectoryInfo(folder).EnumerateDirectories(expression, All), infos: true) ?? listing;
        }
        public static IEnumerable<DirectoryInfo> InfoEnumerateDirectories(DirectoryInfo self, string searchPattern, EnumerationOptions enumerationOptions)
        {
            var listing = self.EnumerateDirectories(searchPattern, enumerationOptions);
            return !Recursive(enumerationOptions) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => new DirectoryInfo(folder).EnumerateDirectories(expression, enumerationOptions), infos: true) ?? listing;
        }

        public static IEnumerable<FileSystemInfo> InfoEnumerateFileSystemInfos(DirectoryInfo self) => MonoDirectoryOrder.SortedLazily(self.EnumerateFileSystemInfos());
        public static IEnumerable<FileSystemInfo> InfoEnumerateFileSystemInfos(DirectoryInfo self, string searchPattern) => MonoDirectoryOrder.SortedLazily(self.EnumerateFileSystemInfos(searchPattern));
        public static IEnumerable<FileSystemInfo> InfoEnumerateFileSystemInfos(DirectoryInfo self, string searchPattern, SearchOption searchOption)
        {
            var listing = self.EnumerateFileSystemInfos(searchPattern, searchOption);
            return !Recursive(searchOption) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => new DirectoryInfo(folder).EnumerateFileSystemInfos(expression, All), infos: true) ?? listing;
        }
        public static IEnumerable<FileSystemInfo> InfoEnumerateFileSystemInfos(DirectoryInfo self, string searchPattern, EnumerationOptions enumerationOptions)
        {
            var listing = self.EnumerateFileSystemInfos(searchPattern, enumerationOptions);
            return !Recursive(enumerationOptions) ? MonoDirectoryOrder.SortedLazily(listing)
                : MonoDirectoryOrder.Breadth(listing, searchPattern, (folder, expression) => new DirectoryInfo(folder).EnumerateFileSystemInfos(expression, enumerationOptions), infos: true) ?? listing;
        }

        /// The listing methods by declaring type ("Directory", "DirectoryInfo").
        static readonly HashSet<string> Listings = new(StringComparer.Ordinal)
        {
            "GetFiles", "GetDirectories", "GetFileSystemEntries", "EnumerateFiles", "EnumerateDirectories", "EnumerateFileSystemEntries",
            "GetFileSystemInfos", "EnumerateFileSystemInfos",
        };

        /// The replacement of a call to Directory's or DirectoryInfo's `method`, or null for any
        /// other method.
        internal static MethodInfo? Replacement(string declaringType, string method, IReadOnlyList<string> parameterTypes, bool hasThis)
        {
            if (!Listings.Contains(method)) return null;
            var types = new List<Type>();
            string name;
            if (declaringType == "System.IO.Directory" && !hasThis) name = method;
            else if (declaringType == "System.IO.DirectoryInfo" && hasThis)
            {
                name = "Info" + method;
                types.Add(typeof(DirectoryInfo));
            }
            else return null;
            foreach (var parameter in parameterTypes)
            {
                var type = parameter switch
                {
                    "System.String" => typeof(string),
                    "System.IO.SearchOption" => typeof(SearchOption),
                    "System.IO.EnumerationOptions" => typeof(EnumerationOptions),
                    _ => null,
                };
                if (type == null) return null;
                types.Add(type);
            }
            return typeof(MonoDirectoryListing).GetMethod(name, BindingFlags.Public | BindingFlags.Static, types.ToArray());
        }

        /// Sends the module's listing calls here; returns the calls replaced. A no-op on Windows.
        internal static int RewriteModule(Cecil.ModuleDefinition module)
        {
            if (!MonoDirectoryOrder.Sorts) return 0;
            var references = new Dictionary<MethodInfo, Cecil.MethodReference>();
            int replaced = 0;
            foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    foreach (var instruction in method.Body.Instructions)
                    {
                        var code = instruction.OpCode.Code;
                        if (code is not (Cil.Code.Call or Cil.Code.Callvirt or Cil.Code.Ldftn) || instruction.Operand is not Cecil.MethodReference callee
                            || callee is Cecil.GenericInstanceMethod || !Listings.Contains(callee.Name)) continue;
                        var parameters = new string[callee.Parameters.Count];
                        for (int i = 0; i < parameters.Length; i++) parameters[i] = callee.Parameters[i].ParameterType.FullName;
                        // A delegate of an instance method would take the instance otherwise: only
                        // Directory's are replaced in ldftn.
                        if (code == Cil.Code.Ldftn && callee.HasThis) continue;
                        if (Replacement(callee.DeclaringType.FullName, callee.Name, parameters, callee.HasThis) is not { } replacement) continue;
                        if (!references.TryGetValue(replacement, out var reference)) references[replacement] = reference = module.ImportReference(replacement);
                        if (code != Cil.Code.Ldftn) instruction.OpCode = Cil.OpCodes.Call;
                        instruction.Operand = reference;
                        replaced++;
                    }
                }
            return replaced;
        }

        /// Whether the image references a listing method of Directory or DirectoryInfo (read from
        /// its metadata alone).
        internal static bool MayList(byte[] image)
        {
            if (!MonoDirectoryOrder.Sorts) return false;
            try
            {
                using var pe = new PEReader(new MemoryStream(image));
                if (!pe.HasMetadata) return false;
                var reader = pe.GetMetadataReader();
                foreach (var handle in reader.MemberReferences)
                {
                    var member = reader.GetMemberReference(handle);
                    if (member.Parent.Kind != HandleKind.TypeReference) continue;
                    var parent = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                    if (reader.StringComparer.Equals(parent.Namespace, "System.IO")
                        && (reader.StringComparer.Equals(parent.Name, "Directory") || reader.StringComparer.Equals(parent.Name, "DirectoryInfo"))
                        && Listings.Contains(reader.GetString(member.Name))) return true;
                }
            }
            catch (BadImageFormatException)
            {
                // Not an assembly: the load reports it.
            }
            return false;
        }
    }
}
