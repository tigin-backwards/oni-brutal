using System;
using System.IO;

namespace OniMoono.Assets.Compression
{
    /// LZ4 block format decoder (used by UnityFS for LZ4 and LZ4HC blocks).
    public static class Lz4
    {
        /// Decodes `source` into exactly `destination.Length` bytes.
        public static void Decode(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            try
            {
                DecodeChecked(source, destination);
            }
            // A sequence running past either end: a truncated or damaged block.
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
            {
                throw new InvalidDataException("LZ4: truncated or corrupt block.", e);
            }
        }

        static void DecodeChecked(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int src = 0, dst = 0;
            while (src < source.Length)
            {
                int token = source[src++];
                int literalLength = token >> 4;
                if (literalLength == 15)
                {
                    int b;
                    do
                    {
                        b = source[src++];
                        literalLength += b;
                    } while (b == 255);
                }
                source.Slice(src, literalLength).CopyTo(destination.Slice(dst));
                src += literalLength;
                dst += literalLength;
                if (src >= source.Length) break; // the last sequence has literals only

                int offset = source[src] | source[src + 1] << 8;
                src += 2;
                if (offset == 0 || offset > dst) throw new InvalidDataException("LZ4: invalid match offset.");
                int matchLength = (token & 0xF) + 4;
                if ((token & 0xF) == 15)
                {
                    int b;
                    do
                    {
                        b = source[src++];
                        matchLength += b;
                    } while (b == 255);
                }
                int from = dst - offset;
                if (matchLength <= offset)
                {
                    destination.Slice(from, matchLength).CopyTo(destination.Slice(dst));
                }
                else
                {
                    // Overlapping copy repeats the pattern byte by byte.
                    for (int i = 0; i < matchLength; i++) destination[dst + i] = destination[from + i];
                }
                dst += matchLength;
            }
            if (dst != destination.Length) throw new InvalidDataException($"LZ4: decoded {dst} bytes, expected {destination.Length}.");
        }
    }

}
