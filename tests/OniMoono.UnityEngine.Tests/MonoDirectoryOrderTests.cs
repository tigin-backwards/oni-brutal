using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniMoono.Engine;
using Xunit;

namespace OniMoono.Tests
{
    /// Directory listings in Mono's order (ARCHITECTURE D30): each folder's entries by their
    /// UTF-8 bytes, subfolders walked breadth first in that order. The expected orders are the
    /// original player's (tools/UnityProbe, `PROBE_SCENARIO=dirorder`, on the probe's tree).
    public sealed class MonoDirectoryOrderTests : IDisposable
    {
        readonly string root = Directory.CreateTempSubdirectory("oni-moono-dirorder-").FullName;

        // The probe's tree, made in the probe's order (none of the candidate orders).
        static readonly string[] Files =
        {
            "z.txt", "b.txt", "\U0001F600.txt", "B.txt", "10.txt", "a b.txt", "ﬀ.txt", "_u.txt", "A.txt", "9.txt",
            "ä.txt", "a.txt", "-dash.txt", "~tilde.txt", "Z.txt", "c.log", ".hidden", "a.TXT", "ab.txt", "a.txt2",
        };
        static readonly string[] Folders = { "dB", "d\U0001F600", "da", "d_", "dA", "dä", "dﬀ", "d0" };

        public MonoDirectoryOrderTests()
        {
            foreach (var name in Files) File.WriteAllText(Path.Combine(root, name), name);
            foreach (var folder in Folders)
            {
                var d = Path.Combine(root, folder);
                Directory.CreateDirectory(d);
                foreach (var name in new[] { "y.txt", "X.txt", "x.txt" }) File.WriteAllText(Path.Combine(d, name), name);
                foreach (var sub in new[] { "s2", "S1", "s1" })
                {
                    Directory.CreateDirectory(Path.Combine(d, sub));
                    File.WriteAllText(Path.Combine(d, sub, "deep.txt"), sub);
                }
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }

        // The original's results with the root written as "R".
        const string RootFiles = "R/-dash.txt|R/.hidden|R/10.txt|R/9.txt|R/A.txt|R/B.txt|R/Z.txt|R/_u.txt|R/a b.txt|R/a.TXT|R/a.txt|R/a.txt2|R/ab.txt|R/b.txt|R/c.log|R/z.txt|R/~tilde.txt|R/ä.txt|R/ﬀ.txt|R/\U0001F600.txt";
        const string RootFolders = "R/d0|R/dA|R/dB|R/d_|R/da|R/dä|R/dﬀ|R/d\U0001F600";

        static readonly string[] SortedFolders = { "d0", "dA", "dB", "d_", "da", "dä", "dﬀ", "d\U0001F600" };

        /// Every file, breadth first: the root's, then each folder's three, then each folder's
        /// subfolders' deep.txt (S1, s1, s2) in the folders' order.
        static string AllFiles() => string.Join("|",
            RootFiles.Split('|')
                .Concat(SortedFolders.SelectMany(f => new[] { $"R/{f}/X.txt", $"R/{f}/x.txt", $"R/{f}/y.txt" }))
                .Concat(SortedFolders.SelectMany(f => new[] { $"R/{f}/S1/deep.txt", $"R/{f}/s1/deep.txt", $"R/{f}/s2/deep.txt" })));

        string R(IEnumerable<string> paths) => string.Join("|", paths.Select(p => p.StartsWith(root, StringComparison.Ordinal) ? "R" + p.Substring(root.Length) : p));

        IEnumerable<string> Recursive(string path, string pattern) =>
            MonoDirectoryOrder.Breadth(Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories), pattern,
                (folder, expression) => Directory.EnumerateFiles(folder, expression, SearchOption.AllDirectories), infos: false);

        [Fact]
        public void NamesCompareByTheirUtf8Bytes()
        {
            Assert.True(MonoDirectoryOrder.Compare("B", "_") < 0);
            Assert.True(MonoDirectoryOrder.Compare("_", "a") < 0);
            Assert.True(MonoDirectoryOrder.Compare("a", "a b") < 0);
            Assert.True(MonoDirectoryOrder.Compare("a b", "a.txt") < 0);
            Assert.True(MonoDirectoryOrder.Compare("~", "ä") < 0);
            // UTF-8: U+FB00 is EF AC 80, U+1F600 F0 9F 98 80; UTF-16 puts the emoji's D83D first.
            Assert.True(MonoDirectoryOrder.Compare("ﬀ", "\U0001F600") < 0);
            Assert.True(MonoDirectoryOrder.Compare("\U0001F600", "ﬀ") > 0);
            Assert.True(MonoDirectoryOrder.Compare("퟿", "\U0001F600") < 0);
            Assert.True(MonoDirectoryOrder.Compare("\U0001F600", "\U0001F601") < 0);
            Assert.Equal(0, MonoDirectoryOrder.Compare("same", "same"));
        }

        [Fact]
        public void OneFolderIsSortedByName()
        {
            Assert.Equal(RootFiles, R(MonoDirectoryOrder.Sorted(Directory.GetFiles(root))));
            Assert.Equal(RootFolders, R(MonoDirectoryOrder.Sorted(Directory.GetDirectories(root))));
            Assert.Equal(RootFolders, R(MonoDirectoryOrder.SortedLazily(Directory.EnumerateDirectories(root))));
            Assert.Equal(RootFolders, R(MonoDirectoryOrder.Sorted(new DirectoryInfo(root).GetDirectories()).Select(d => d.FullName)));
            // Patterns match case-sensitively, as in the original.
            Assert.Equal("R/a b.txt|R/a.TXT|R/a.txt|R/a.txt2|R/ab.txt", R(MonoDirectoryOrder.Sorted(Directory.GetFiles(root, "a*"))));
        }

        [Fact]
        public void AnEnumerationReadsTheFolderOnItsFirstStep()
        {
            var listing = MonoDirectoryOrder.SortedLazily(Directory.EnumerateFiles(Path.Combine(root, "da")));
            File.WriteAllText(Path.Combine(root, "da", "added.txt"), "");
            Assert.Equal("R/da/X.txt|R/da/added.txt|R/da/x.txt|R/da/y.txt", R(listing));
            // Enumerated again, it lists the folder again.
            File.WriteAllText(Path.Combine(root, "da", "0.txt"), "");
            Assert.Equal("R/da/0.txt|R/da/X.txt|R/da/added.txt|R/da/x.txt|R/da/y.txt", R(listing));
        }

        [Fact]
        public void ARecursiveListingWalksBreadthFirstInSortedOrder()
        {
            Assert.Equal(AllFiles(), R(Recursive(root, "*")));
            Assert.Equal(string.Join("|", SortedFolders.SelectMany(f => new[] { $"R/{f}/S1/deep.txt", $"R/{f}/s1/deep.txt", $"R/{f}/s2/deep.txt" })),
                R(Recursive(root, "deep.txt")));
        }

        [Fact]
        public void ARecursiveListingHasNetsEntriesAndPaths()
        {
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, root);
            foreach (var (path, pattern) in new[] { (root, "*"), (root + "/", "*"), (root + "//", "*.txt"), (relative, "*"), (root, "da/*"), (root, "da/s?"), (root, "*.*") })
            {
                var net = Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories).ToList();
                var ours = Recursive(path, pattern).ToList();
                Assert.Equal(net.OrderBy(p => p, StringComparer.Ordinal), ours.OrderBy(p => p, StringComparer.Ordinal));
            }
            Assert.Equal("R/da/X.txt|R/da/x.txt|R/da/y.txt|R/da/S1/deep.txt|R/da/s1/deep.txt|R/da/s2/deep.txt", R(Recursive(root, "da/*")));
        }

        [Fact]
        public void ARecursiveListingOfInfosAndOptions()
        {
            var info = new DirectoryInfo(root);
            var options = new EnumerationOptions { RecurseSubdirectories = true };
            var infos = MonoDirectoryOrder.Breadth(info.EnumerateFileSystemInfos("*", options), "*",
                (folder, expression) => new DirectoryInfo(folder).EnumerateFileSystemInfos(expression, options), infos: true).ToList();
            // The default options skip hidden entries (.hidden).
            var expected = RootFiles.Split('|').Where(p => p != "R/.hidden").Concat(RootFolders.Split('|')).OrderBy(p => p.Substring(2), Comparer<string>.Create((a, b) => MonoDirectoryOrder.Compare(a, b)))
                .Concat(SortedFolders.SelectMany(f => new[] { $"R/{f}/S1", $"R/{f}/X.txt", $"R/{f}/s1", $"R/{f}/s2", $"R/{f}/x.txt", $"R/{f}/y.txt" }))
                .Concat(SortedFolders.SelectMany(f => new[] { $"R/{f}/S1/deep.txt", $"R/{f}/s1/deep.txt", $"R/{f}/s2/deep.txt" }));
            Assert.Equal(string.Join("|", expected), R(infos.Select(i => i.FullName)));
            Assert.Equal(Directory.EnumerateFileSystemEntries(root, "*", options).Count(), infos.Count);
        }

        [Fact]
        public void ASubfolderGoneBeforeItIsReadIsSkipped()
        {
            var listing = Recursive(root, "*");
            var seen = new List<string>();
            foreach (var path in listing)
            {
                // The root's folders are queued once its entries are read: dB goes before its turn.
                if (seen.Count == 0) Directory.Delete(Path.Combine(root, "dB"), true);
                seen.Add(path);
            }
            Assert.DoesNotContain(seen, p => p.Contains("/dB/", StringComparison.Ordinal));
            Assert.Contains(seen, p => p.EndsWith("/d_/s2/deep.txt", StringComparison.Ordinal));
        }

        [Fact]
        public void AListingThatIsNotAFileSystemEnumerableIsLeftAlone()
        {
            Assert.Null(MonoDirectoryOrder.Breadth(new[] { "a" }, "*", (_, _) => Array.Empty<string>(), infos: false));
        }
    }
}
