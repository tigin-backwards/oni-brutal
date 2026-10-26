using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace OniMoono.Engine
{
    /// The log file's writer, with a cap the original player's log does not have: a mod throwing
    /// on every frame filled one player's log to 2 GB (issue #15). The first `headBytes` are kept
    /// as written (start-up, the mod list, the first errors); past them, only the last `tailBytes`
    /// or so, behind a banner of dashes saying how much was cut there. Each kept line is as
    /// written. The tail grows to twice its size before the file is cut back, so a log is at most
    /// head + 2 × tail (and the banner) long. ONI_LOG_FULL=1 turns the cap off.
    ///
    /// A write costs what the log's StreamWriter (AutoFlush) cost: one encode and one write call.
    /// The file's length is kept here, not asked of the file system; cutting the tail, a copy of
    /// `tailBytes`, happens once per `tailBytes` written past the head.
    internal sealed class CappedLogFile : IDisposable
    {
        public const long DefaultHeadBytes = 20L << 20, DefaultTailBytes = 20L << 20;

        static readonly UTF8Encoding utf8 = new(false);

        readonly FileStream stream;
        readonly long headBytes, tailBytes;
        byte[] buffer = new byte[4096];
        long length;
        // Where the tail starts (past the head and the banner), once the head is full.
        long tailStart = -1;
        long cut;
        int bannerLength;

        public CappedLogFile(string path, long headBytes = DefaultHeadBytes, long tailBytes = DefaultTailBytes)
        {
            stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 1);
            bool full = Environment.GetEnvironmentVariable("ONI_LOG_FULL") == "1";
            this.headBytes = full ? long.MaxValue : headBytes;
            this.tailBytes = tailBytes;
        }

        public void Write(char value)
        {
            Span<char> one = [value];
            Write(one, endsLine: value == '\n');
        }

        public void Write(string value) => Write(value.AsSpan(), endsLine: value != null && value.EndsWith('\n'));

        public void WriteLine(string value) => Write(value.AsSpan(), endsLine: true, Environment.NewLine.AsSpan());

        /// `text` and then `then`, encoded together and written in one call.
        void Write(ReadOnlySpan<char> text, bool endsLine, ReadOnlySpan<char> then = default)
        {
            if (text.IsEmpty && then.IsEmpty) return;
            int most = utf8.GetMaxByteCount(text.Length + then.Length);
            if (most > buffer.Length) buffer = new byte[Math.Max(most, buffer.Length * 2)];
            int n = utf8.GetBytes(text, buffer);
            n += utf8.GetBytes(then, buffer.AsSpan(n));
            stream.Write(buffer, 0, n);
            length += n;
            if (tailStart < 0)
            {
                // The head ends at the first line break past its size.
                if (length >= headBytes && endsLine) tailStart = Banner(length);
            }
            else if (length - tailStart > 2 * tailBytes) CutTail();
        }

        /// Writes the banner at `at` and returns where the tail starts after it. Its length is
        /// fixed, so it is written again in place as the count of bytes cut grows.
        long Banner(long at)
        {
            var banner = utf8.GetBytes(BannerText(cut, tailBytes));
            stream.Position = at;
            stream.Write(banner, 0, banner.Length);
            bannerLength = banner.Length;
            long end = stream.Position;
            if (end > length) length = end;
            stream.Position = length;
            return end;
        }

        const int BannerWidth = 100;

        /// Three lines: dashes, what was cut, dashes.
        internal static string BannerText(long cut, long tailBytes)
        {
            string rule = new string('-', BannerWidth);
            string text = string.Create(CultureInfo.InvariantCulture,
                $"------ LOG TRUNCATED HERE: {cut,15:N0} bytes cut; below are the last {tailBytes,12:N0} bytes or so ").PadRight(BannerWidth, '-');
            string nl = Environment.NewLine;
            return nl + rule + nl + text + nl + rule + nl + nl;
        }

        /// Keeps the last `tailBytes` after the banner, from their first line break on.
        void CutTail()
        {
            long from = length - tailBytes;
            var kept = new byte[tailBytes];
            stream.Position = from;
            stream.ReadExactly(kept, 0, kept.Length);
            int start = Array.IndexOf(kept, (byte)'\n') + 1;
            cut += from + start - tailStart;
            Banner(tailStart - bannerLength);
            stream.Position = tailStart;
            stream.Write(kept, start, kept.Length - start);
            length = stream.Position;
            stream.SetLength(length);
        }

        public void Flush() => stream.Flush();

        public void Dispose() => stream.Dispose();
    }
}
