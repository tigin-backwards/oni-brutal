// Directory listings (lines "DL ..."): the order in which Directory's and DirectoryInfo's
// Get*/Enumerate* return a folder's entries, with and without search patterns, recursively, and
// through EnumerationOptions. The probe makes a tree of files and folders whose names tell the
// candidate orders apart (case, '_' against letters, digits, a space, "ä", U+FB00 "ﬀ" against
// U+1F600 "😀": UTF-8 byte order puts "ﬀ" first, UTF-16 code unit order the emoji), in a creation
// order unlike any of them, and folders two levels deep that tell breadth-first from
// depth-first. The tree stays (its path is logged) so that `ls -U` can show the folder's own
// order.
//
// Each line is "DL <call> = <entry>|<entry>|...", each entry the result with the probe's root
// written as "R" (FileSystemInfo results by FullName).
//
// Run alone with PROBE_SCENARIO=dirorder (at mod load).
//
// Measured 2026-10-24 (U57 original on Unity's MonoBleedingEdge, Linux amd64, ext4; ours on
// .NET 10 CoreCLR): see docs/ARCHITECTURE.md D30.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SemanticsProbe
{
    public static class DirectoryOrder
    {
        // Creation order: none of the candidate orders.
        static readonly string[] Files =
        {
            "z.txt", "b.txt", "\U0001F600.txt", "B.txt", "10.txt", "a b.txt", "ﬀ.txt", "_u.txt", "A.txt", "9.txt",
            "ä.txt", "a.txt", "-dash.txt", "~tilde.txt", "Z.txt", "c.log", ".hidden", "a.TXT", "ab.txt", "a.txt2",
        };

        static readonly string[] Folders = { "dB", "d\U0001F600", "da", "d_", "dA", "dä", "dﬀ", "d0" };

        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "dirorder-probe-" + System.DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(root);
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
            Rec.Log("DL root " + root);

            string R(string s) => s.StartsWith(root, StringComparison.Ordinal) ? "R" + s.Substring(root.Length) : s;
            void Line(string call, Func<IEnumerable<string>> list)
            {
                string r;
                try { r = string.Join("|", list().Select(R).ToArray()); }
                catch (Exception e) { r = "!" + e.GetType().Name; }
                Rec.Log("DL " + call + " = " + r);
            }
            IEnumerable<string> Infos(IEnumerable<FileSystemInfo> infos) => infos.Select(i => i.FullName);
            var all = SearchOption.AllDirectories;
            var top = SearchOption.TopDirectoryOnly;
            var info = new DirectoryInfo(root);
            var recurse = new EnumerationOptions { RecurseSubdirectories = true };

            Line("GetFiles(R)", () => Directory.GetFiles(root));
            Line("GetFiles(R,*.txt)", () => Directory.GetFiles(root, "*.txt"));
            Line("GetFiles(R,a*,top)", () => Directory.GetFiles(root, "a*", top));
            Line("GetFiles(R,*,all)", () => Directory.GetFiles(root, "*", all));
            Line("GetFiles(R,*.txt,all)", () => Directory.GetFiles(root, "*.txt", all));
            Line("GetFiles(R,deep.txt,all)", () => Directory.GetFiles(root, "deep.txt", all));
            Line("GetFiles(R/,*,all)", () => Directory.GetFiles(root + "/", "*", all));
            Line("GetFiles(R,da/*.txt)", () => Directory.GetFiles(root, "da/*.txt"));
            Line("GetFiles(R,da/*,all)", () => Directory.GetFiles(root, "da/*", all));
            Line("GetFiles(R,*,opts)", () => Directory.GetFiles(root, "*", new EnumerationOptions()));
            Line("GetFiles(R,*,opts-recurse)", () => Directory.GetFiles(root, "*", recurse));
            Line("GetDirectories(R)", () => Directory.GetDirectories(root));
            Line("GetDirectories(R,d?)", () => Directory.GetDirectories(root, "d?"));
            Line("GetDirectories(R,*,all)", () => Directory.GetDirectories(root, "*", all));
            Line("GetDirectories(R,s*,all)", () => Directory.GetDirectories(root, "s*", all));
            Line("GetFileSystemEntries(R)", () => Directory.GetFileSystemEntries(root));
            Line("GetFileSystemEntries(R,*a*)", () => Directory.GetFileSystemEntries(root, "*a*"));
            Line("GetFileSystemEntries(R,*,all)", () => Directory.GetFileSystemEntries(root, "*", all));
            Line("EnumerateFiles(R)", () => Directory.EnumerateFiles(root));
            Line("EnumerateFiles(R,*.txt)", () => Directory.EnumerateFiles(root, "*.txt"));
            Line("EnumerateFiles(R,*,all)", () => Directory.EnumerateFiles(root, "*", all));
            Line("EnumerateFiles(R,*,opts-recurse)", () => Directory.EnumerateFiles(root, "*", recurse));
            Line("EnumerateDirectories(R)", () => Directory.EnumerateDirectories(root));
            Line("EnumerateDirectories(R,*,all)", () => Directory.EnumerateDirectories(root, "*", all));
            Line("EnumerateFileSystemEntries(R)", () => Directory.EnumerateFileSystemEntries(root));
            Line("EnumerateFileSystemEntries(R,*,all)", () => Directory.EnumerateFileSystemEntries(root, "*", all));
            Line("Info.GetFiles()", () => Infos(info.GetFiles()));
            Line("Info.GetFiles(*.txt,all)", () => Infos(info.GetFiles("*.txt", all)));
            Line("Info.GetDirectories()", () => Infos(info.GetDirectories()));
            Line("Info.GetDirectories(*,all)", () => Infos(info.GetDirectories("*", all)));
            Line("Info.GetFileSystemInfos()", () => Infos(info.GetFileSystemInfos()));
            Line("Info.GetFileSystemInfos(*,all)", () => Infos(info.GetFileSystemInfos("*", all)));
            Line("Info.EnumerateFiles()", () => Infos(info.EnumerateFiles()));
            Line("Info.EnumerateFiles(*,all)", () => Infos(info.EnumerateFiles("*", all)));
            Line("Info.EnumerateDirectories()", () => Infos(info.EnumerateDirectories()));
            Line("Info.EnumerateDirectories(*,all)", () => Infos(info.EnumerateDirectories("*", all)));
            Line("Info.EnumerateFileSystemInfos()", () => Infos(info.EnumerateFileSystemInfos()));
            Line("Info.EnumerateFileSystemInfos(*,opts-recurse)", () => Infos(info.EnumerateFileSystemInfos("*", recurse)));
            // An enumeration started before a file is added and read after: Mono reads the whole
            // folder on the first MoveNext.
            Line("EnumerateFiles(R/da) added after the call", () =>
            {
                var e = Directory.EnumerateFiles(Path.Combine(root, "da"));
                File.WriteAllText(Path.Combine(root, "da", "added.txt"), "added");
                return e.ToList();
            });
            Line("EnumerateFiles(R/dB) added after the first entry", () =>
            {
                var result = new List<string>();
                foreach (var f in Directory.EnumerateFiles(Path.Combine(root, "dB")))
                {
                    if (result.Count == 0) File.WriteAllText(Path.Combine(root, "dB", "0added.txt"), "added");
                    result.Add(f);
                }
                return result;
            });
            Line("GetFiles(missing)", () => Directory.GetFiles(Path.Combine(root, "missing")));
            Line("EnumerateFiles(missing) call", () => { Directory.EnumerateFiles(Path.Combine(root, "missing")); return new[] { "no exception" }; });
            Rec.Log("DL done");
        }
    }
}
