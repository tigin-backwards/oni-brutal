using System;
using System.IO;
using System.Linq;
using OniBrutal.Engine;
using Xunit;

namespace OniBrutal.Tests
{
    /// The log file's cap (CappedLogFile): the head kept as written, a banner where it is cut,
    /// the last lines after it, and the file's size bounded however much is written.
    public class CappedLogFileTests
    {
        static string TempLog() => Path.Combine(Path.GetTempPath(), "oni-brutal-log-" + Guid.NewGuid().ToString("N") + ".log");

        [Fact]
        public void ALogUnderTheHeadIsWrittenAsItIs()
        {
            string path = TempLog();
            try
            {
                using (var log = new CappedLogFile(path, headBytes: 4096, tailBytes: 1024))
                {
                    log.WriteLine("first");
                    log.Write("second, ");
                    log.Write('a');
                    log.WriteLine(" char");
                }
                Assert.Equal("first" + Environment.NewLine + "second, a char" + Environment.NewLine, File.ReadAllText(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void SpamKeepsTheHeadABannerAndTheLastLines()
        {
            string path = TempLog();
            try
            {
                const int head = 4096, tail = 2048;
                using (var log = new CappedLogFile(path, head, tail))
                {
                    log.WriteLine("start-up, the mod list");
                    for (int i = 0; i < 20000; i++) log.WriteLine($"spam {i:D6} the same exception again");
                    log.WriteLine("the last line");
                }
                string text = File.ReadAllText(path);
                long size = new FileInfo(path).Length;
                // Bounded: the head (to its line's end), the banner, at most twice the tail.
                Assert.InRange(size, head, head + 200 + 600 + 2 * tail);
                Assert.StartsWith("start-up, the mod list" + Environment.NewLine + "spam 000000", text);
                Assert.EndsWith("the last line" + Environment.NewLine, text);
                Assert.Contains("LOG TRUNCATED HERE", text);
                Assert.Single(text.Split(Environment.NewLine), l => l.Contains("LOG TRUNCATED HERE"));
                // Whole lines on both sides of the banner, in order, and the count of bytes cut
                // makes up the difference with what was written.
                var lines = text.Split(Environment.NewLine).Where(l => l.StartsWith("spam ")).ToList();
                Assert.All(lines, l => Assert.Matches(@"^spam \d{6} the same exception again$", l));
                var numbers = lines.Select(l => int.Parse(l.Substring(5, 6))).ToList();
                Assert.Equal(numbers.OrderBy(n => n), numbers);
                Assert.Equal(19999, numbers[^1]);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void TheBannerIsTheSameLengthWhateverItCounts()
        {
            int small = CappedLogFile.BannerText(0, 1024).Length;
            Assert.Equal(small, CappedLogFile.BannerText(123_456_789_012, 1024).Length);
            Assert.Contains(new string('-', 100), CappedLogFile.BannerText(5, 1024));
        }
    }
}
