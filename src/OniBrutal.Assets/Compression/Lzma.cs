using System;
using System.IO;

namespace OniBrutal.Assets.Compression
{
    /// LZMA decoder (the algorithm of Igor Pavlov's public-domain LZMA SDK specification).
    /// Input is the 5-byte properties header followed by the raw range-coded stream; the decoded
    /// size is known from the container (UnityFS does not store an end marker reliably).
    public sealed class Lzma
    {
        const int NumStates = 12;
        const int NumPosBitsMax = 4;
        const int NumLenToPosStates = 4;
        const int NumAlignBits = 4;
        const int StartPosModelIndex = 4;
        const int EndPosModelIndex = 14;
        const int NumFullDistances = 1 << (EndPosModelIndex >> 1);
        const int MatchMinLen = 2;

        public static void Decode(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (input.Length < 5) throw new InvalidDataException("LZMA: missing properties.");
            int d = input[0];
            if (d >= 9 * 5 * 5) throw new InvalidDataException("LZMA: bad properties.");
            int lc = d % 9, lp = d / 9 % 5, pb = d / 45;
            var decoder = new Lzma(lc, lp, pb);
            try
            {
                decoder.Run(input.Slice(5), output);
            }
            // Anything past the checks above is a damaged stream too.
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
            {
                throw new InvalidDataException("LZMA: corrupt stream.", e);
            }
        }

        readonly int lc, lp, pb;
        readonly ushort[] literalProbs;
        readonly ushort[] isMatch = new ushort[NumStates << NumPosBitsMax];
        readonly ushort[] isRep = new ushort[NumStates];
        readonly ushort[] isRepG0 = new ushort[NumStates];
        readonly ushort[] isRepG1 = new ushort[NumStates];
        readonly ushort[] isRepG2 = new ushort[NumStates];
        readonly ushort[] isRep0Long = new ushort[NumStates << NumPosBitsMax];
        readonly ushort[][] posSlot = new ushort[NumLenToPosStates][];
        readonly ushort[] posDecoders = new ushort[1 + NumFullDistances - EndPosModelIndex];
        readonly ushort[] alignDecoder = new ushort[1 << NumAlignBits];
        readonly LenDecoder lenDecoder = new();
        readonly LenDecoder repLenDecoder = new();

        Lzma(int lc, int lp, int pb)
        {
            this.lc = lc;
            this.lp = lp;
            this.pb = pb;
            literalProbs = new ushort[0x300 << (lc + lp)];
            for (int i = 0; i < NumLenToPosStates; i++) posSlot[i] = new ushort[1 << 6];
            Init(literalProbs); Init(isMatch); Init(isRep); Init(isRepG0); Init(isRepG1); Init(isRepG2);
            Init(isRep0Long); Init(posDecoders); Init(alignDecoder);
            foreach (var p in posSlot) Init(p);
        }

        static void Init(ushort[] probs) => Array.Fill(probs, (ushort)1024);

        ref struct RangeDecoder
        {
            ReadOnlySpan<byte> input;
            int pos;
            uint range, code;

            public RangeDecoder(ReadOnlySpan<byte> input)
            {
                this.input = input;
                pos = 0;
                range = 0xFFFFFFFF;
                code = 0;
                if (NextByte() != 0) throw new InvalidDataException("LZMA: bad stream start.");
                for (int i = 0; i < 4; i++) code = code << 8 | NextByte();
            }

            byte NextByte() => pos < input.Length ? input[pos++] : throw new InvalidDataException("LZMA: input truncated.");

            void Normalize()
            {
                if (range < 1u << 24)
                {
                    range <<= 8;
                    code = code << 8 | NextByte();
                }
            }

            public int DecodeBit(ref ushort prob)
            {
                uint bound = (range >> 11) * prob;
                int bit;
                if (code < bound)
                {
                    prob = (ushort)(prob + ((2048 - prob) >> 5));
                    range = bound;
                    bit = 0;
                }
                else
                {
                    prob = (ushort)(prob - (prob >> 5));
                    code -= bound;
                    range -= bound;
                    bit = 1;
                }
                Normalize();
                return bit;
            }

            public uint DecodeDirectBits(int numBits)
            {
                uint result = 0;
                for (; numBits > 0; numBits--)
                {
                    range >>= 1;
                    code -= range;
                    uint t = 0u - (code >> 31);
                    code += range & t;
                    Normalize();
                    result = (result << 1) + (t + 1);
                }
                return result;
            }

            public int BitTree(ushort[] probs, int offset, int numBits)
            {
                int m = 1;
                for (int i = 0; i < numBits; i++) m = (m << 1) + DecodeBit(ref probs[offset + m]);
                return m - (1 << numBits);
            }

            public int BitTreeReverse(ushort[] probs, int offset, int numBits)
            {
                int m = 1, symbol = 0;
                for (int i = 0; i < numBits; i++)
                {
                    int bit = DecodeBit(ref probs[offset + m]);
                    m = (m << 1) + bit;
                    symbol |= bit << i;
                }
                return symbol;
            }
        }

        sealed class LenDecoder
        {
            ushort choice = 1024, choice2 = 1024;
            readonly ushort[][] low = new ushort[1 << NumPosBitsMax][];
            readonly ushort[][] mid = new ushort[1 << NumPosBitsMax][];
            readonly ushort[] high = new ushort[1 << 8];

            public LenDecoder()
            {
                for (int i = 0; i < low.Length; i++)
                {
                    low[i] = new ushort[1 << 3];
                    mid[i] = new ushort[1 << 3];
                    Init(low[i]);
                    Init(mid[i]);
                }
                Init(high);
            }

            public int Decode(ref RangeDecoder rc, int posState)
            {
                if (rc.DecodeBit(ref choice) == 0) return rc.BitTree(low[posState], 0, 3);
                if (rc.DecodeBit(ref choice2) == 0) return 8 + rc.BitTree(mid[posState], 0, 3);
                return 16 + rc.BitTree(high, 0, 8);
            }
        }

        void Run(ReadOnlySpan<byte> stream, Span<byte> output)
        {
            var rc = new RangeDecoder(stream);
            int outPos = 0;
            int state = 0;
            uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;
            int pbMask = (1 << pb) - 1, lpMask = (1 << lp) - 1;

            while (outPos < output.Length)
            {
                int posState = outPos & pbMask;
                if (rc.DecodeBit(ref isMatch[(state << NumPosBitsMax) + posState]) == 0)
                {
                    // Literal.
                    int prevByte = outPos > 0 ? output[outPos - 1] : 0;
                    int litState = ((outPos & lpMask) << lc) + (prevByte >> (8 - lc));
                    int baseIndex = 0x300 * litState;
                    int symbol = 1;
                    if (state >= 7)
                    {
                        int matchByte = output[outPos - (int)rep0 - 1];
                        do
                        {
                            int matchBit = (matchByte >> 7) & 1;
                            matchByte <<= 1;
                            int bit = rc.DecodeBit(ref literalProbs[baseIndex + ((1 + matchBit) << 8) + symbol]);
                            symbol = (symbol << 1) | bit;
                            if (matchBit != bit) break;
                        } while (symbol < 0x100);
                    }
                    while (symbol < 0x100) symbol = (symbol << 1) | rc.DecodeBit(ref literalProbs[baseIndex + symbol]);
                    output[outPos++] = (byte)symbol;
                    state = state < 4 ? 0 : state < 10 ? state - 3 : state - 6;
                    continue;
                }

                int len;
                if (rc.DecodeBit(ref isRep[state]) != 0)
                {
                    if (outPos == 0) throw new InvalidDataException("LZMA: rep match at start.");
                    if (rc.DecodeBit(ref isRepG0[state]) == 0)
                    {
                        if (rc.DecodeBit(ref isRep0Long[(state << NumPosBitsMax) + posState]) == 0)
                        {
                            // Short rep: one byte from rep0.
                            state = state < 7 ? 9 : 11;
                            if (rep0 >= outPos) throw new InvalidDataException("LZMA: rep distance out of range.");
                            output[outPos] = output[outPos - (int)rep0 - 1];
                            outPos++;
                            continue;
                        }
                    }
                    else
                    {
                        uint dist;
                        if (rc.DecodeBit(ref isRepG1[state]) == 0) dist = rep1;
                        else
                        {
                            if (rc.DecodeBit(ref isRepG2[state]) == 0) dist = rep2;
                            else
                            {
                                dist = rep3;
                                rep3 = rep2;
                            }
                            rep2 = rep1;
                        }
                        rep1 = rep0;
                        rep0 = dist;
                    }
                    len = repLenDecoder.Decode(ref rc, posState);
                    state = state < 7 ? 8 : 11;
                }
                else
                {
                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;
                    len = lenDecoder.Decode(ref rc, posState);
                    state = state < 7 ? 7 : 10;
                    rep0 = DecodeDistance(ref rc, len);
                    if (rep0 == 0xFFFFFFFF) break; // end marker
                }
                len += MatchMinLen;
                if (rep0 >= outPos) throw new InvalidDataException("LZMA: match distance out of range.");
                int from = outPos - (int)rep0 - 1;
                int copy = Math.Min(len, output.Length - outPos);
                for (int i = 0; i < copy; i++) output[outPos + i] = output[from + i];
                outPos += copy;
            }
            if (outPos != output.Length) throw new InvalidDataException($"LZMA: decoded {outPos} bytes, expected {output.Length}.");
        }

        uint DecodeDistance(ref RangeDecoder rc, int len)
        {
            int lenState = Math.Min(len, NumLenToPosStates - 1);
            int slot = rc.BitTree(posSlot[lenState], 0, 6);
            if (slot < 4) return (uint)slot;
            int numDirectBits = (slot >> 1) - 1;
            uint dist = (uint)((2 | (slot & 1)) << numDirectBits);
            if (slot < EndPosModelIndex)
                return dist + (uint)rc.BitTreeReverse(posDecoders, (int)dist - slot, numDirectBits);
            dist += rc.DecodeDirectBits(numDirectBits - NumAlignBits) << NumAlignBits;
            return dist + (uint)rc.BitTreeReverse(alignDecoder, 0, NumAlignBits);
        }
    }
}
