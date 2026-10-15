using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OniBrutal.Engine.Video
{
    /// A track of a WebM file.
    internal sealed class WebmTrack
    {
        public ulong Number;
        /// Matroska TrackType: 1 video, 2 audio, 17 subtitle…
        public int Type;
        public string CodecId = "";
        public int PixelWidth, PixelHeight;
        /// DefaultDuration in nanoseconds, 0 if absent.
        public ulong DefaultDuration;
        public double SamplingFrequency;
        public int Channels;

        public bool IsVideo => Type == 1;
        public bool IsAudio => Type == 2;
    }

    /// One compressed frame of a track: where its bytes are in the file and when it is presented.
    internal readonly record struct WebmFrame(int Offset, int Size, long TimestampNs, bool Keyframe);

    /// A demuxed WebM file: its tracks and, for each, the frames in file (decode) order.
    internal sealed class WebmFile
    {
        public ulong TimecodeScale = 1_000_000;
        /// Segment duration in nanoseconds (0 if the file does not state it).
        public double DurationNs;
        public readonly List<WebmTrack> Tracks = new();
        public readonly Dictionary<ulong, List<WebmFrame>> Frames = new();

        public WebmTrack FirstVideoTrack => Tracks.Find(t => t.IsVideo);
        public bool HasAudio => Tracks.Exists(t => t.IsAudio);

        public IReadOnlyList<WebmFrame> FramesOf(WebmTrack track)
            => Frames.TryGetValue(track.Number, out var list) ? list : Array.Empty<WebmFrame>();
    }

    /// Our EBML/Matroska reader for the subset WebM uses in the game's clips: the EBML header,
    /// one Segment (known or unknown size), Info (TimecodeScale, Duration), Tracks (number, type,
    /// codec, video size, audio format) and Clusters (Timecode, SimpleBlock, BlockGroup with
    /// Block and ReferenceBlock). Seek heads, cues, tags, chapters, attachments and Void elements
    /// are skipped. Laced blocks (used only for audio in WebM) are rejected.
    internal static class WebmDemuxer
    {
        const uint IdEbml = 0x1A45DFA3, IdDocType = 0x4282, IdSegment = 0x18538067;
        const uint IdInfo = 0x1549A966, IdTimecodeScale = 0x2AD7B1, IdDuration = 0x4489;
        const uint IdTracks = 0x1654AE6B, IdTrackEntry = 0xAE, IdTrackNumber = 0xD7, IdTrackType = 0x83, IdCodecId = 0x86, IdDefaultDuration = 0x23E383;
        const uint IdVideo = 0xE0, IdPixelWidth = 0xB0, IdPixelHeight = 0xBA;
        const uint IdAudio = 0xE1, IdSamplingFrequency = 0xB5, IdChannels = 0x9F;
        const uint IdCluster = 0x1F43B675, IdTimecode = 0xE7, IdSimpleBlock = 0xA3, IdBlockGroup = 0xA0, IdBlock = 0xA1, IdReferenceBlock = 0xFB;
        const ulong UnknownSize = ulong.MaxValue;

        public static WebmFile Parse(ReadOnlySpan<byte> data)
        {
            var file = new WebmFile();
            int pos = 0;
            bool sawHeader = false, sawSegment = false;
            while (pos < data.Length)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                int end = End(data, body, size);
                switch (id)
                {
                    case IdEbml:
                        CheckHeader(data[body..end]);
                        sawHeader = true;
                        break;
                    case IdSegment:
                        if (!sawHeader) throw new InvalidDataException("WebM: Segment before the EBML header.");
                        ParseSegment(data, body, end, file);
                        sawSegment = true;
                        break;
                }
                pos = end;
            }
            if (!sawSegment) throw new InvalidDataException("WebM: no Segment.");
            return file;
        }

        static void CheckHeader(ReadOnlySpan<byte> header)
        {
            int pos = 0;
            while (pos < header.Length)
            {
                var (id, size, body) = ReadElementHeader(header, pos);
                int end = End(header, body, size);
                if (id == IdDocType)
                {
                    var docType = Encoding.ASCII.GetString(header[body..end]).TrimEnd('\0');
                    if (docType != "webm" && docType != "matroska") throw new InvalidDataException($"WebM: unsupported DocType '{docType}'.");
                }
                pos = end;
            }
        }

        static void ParseSegment(ReadOnlySpan<byte> data, int pos, int end, WebmFile file)
        {
            while (pos < end)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                // An unknown-size Cluster (live-style muxing) runs until the next top-level element.
                int elementEnd = size == UnknownSize && id == IdCluster ? ClusterEnd(data, body, end) : End(data, body, size, end);
                switch (id)
                {
                    case IdInfo: ParseInfo(data, body, elementEnd, file); break;
                    case IdTracks: ParseTracks(data, body, elementEnd, file); break;
                    case IdCluster: ParseCluster(data, body, elementEnd, file); break;
                }
                pos = elementEnd;
            }
        }

        static void ParseInfo(ReadOnlySpan<byte> data, int pos, int end, WebmFile file)
        {
            while (pos < end)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                int elementEnd = End(data, body, size, end);
                if (id == IdTimecodeScale) file.TimecodeScale = ReadUInt(data[body..elementEnd]);
                else if (id == IdDuration) file.DurationNs = ReadFloat(data[body..elementEnd]);
                pos = elementEnd;
            }
            // Duration is expressed in TimecodeScale units.
            file.DurationNs *= file.TimecodeScale;
        }

        static void ParseTracks(ReadOnlySpan<byte> data, int pos, int end, WebmFile file)
        {
            while (pos < end)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                int elementEnd = End(data, body, size, end);
                if (id == IdTrackEntry)
                {
                    var track = new WebmTrack();
                    ParseTrackEntry(data, body, elementEnd, track);
                    file.Tracks.Add(track);
                    file.Frames[track.Number] = new List<WebmFrame>();
                }
                pos = elementEnd;
            }
        }

        static void ParseTrackEntry(ReadOnlySpan<byte> data, int pos, int end, WebmTrack track)
        {
            while (pos < end)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                int elementEnd = End(data, body, size, end);
                var value = data[body..elementEnd];
                switch (id)
                {
                    case IdTrackNumber: track.Number = ReadUInt(value); break;
                    case IdTrackType: track.Type = (int)ReadUInt(value); break;
                    case IdCodecId: track.CodecId = Encoding.ASCII.GetString(value).TrimEnd('\0'); break;
                    case IdDefaultDuration: track.DefaultDuration = ReadUInt(value); break;
                    case IdVideo:
                        for (int p = body; p < elementEnd;)
                        {
                            var (vid, vsize, vbody) = ReadElementHeader(data, p);
                            int vend = End(data, vbody, vsize, elementEnd);
                            if (vid == IdPixelWidth) track.PixelWidth = (int)ReadUInt(data[vbody..vend]);
                            else if (vid == IdPixelHeight) track.PixelHeight = (int)ReadUInt(data[vbody..vend]);
                            p = vend;
                        }
                        break;
                    case IdAudio:
                        for (int p = body; p < elementEnd;)
                        {
                            var (aid, asize, abody) = ReadElementHeader(data, p);
                            int aend = End(data, abody, asize, elementEnd);
                            if (aid == IdSamplingFrequency) track.SamplingFrequency = ReadFloat(data[abody..aend]);
                            else if (aid == IdChannels) track.Channels = (int)ReadUInt(data[abody..aend]);
                            p = aend;
                        }
                        break;
                }
                pos = elementEnd;
            }
        }

        static void ParseCluster(ReadOnlySpan<byte> data, int pos, int end, WebmFile file)
        {
            long clusterTimecode = 0;
            while (pos < end)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                int elementEnd = End(data, body, size, end);
                switch (id)
                {
                    case IdTimecode:
                        clusterTimecode = (long)ReadUInt(data[body..elementEnd]);
                        break;
                    case IdSimpleBlock:
                        AddBlock(data, body, elementEnd, clusterTimecode, simple: true, hasReference: false, file);
                        break;
                    case IdBlockGroup:
                    {
                        int blockStart = -1, blockEnd = -1;
                        bool hasReference = false;
                        for (int p = body; p < elementEnd;)
                        {
                            var (gid, gsize, gbody) = ReadElementHeader(data, p);
                            int gend = End(data, gbody, gsize, elementEnd);
                            if (gid == IdBlock)
                            {
                                blockStart = gbody;
                                blockEnd = gend;
                            }
                            else if (gid == IdReferenceBlock) hasReference = true;
                            p = gend;
                        }
                        if (blockStart < 0) throw new InvalidDataException("WebM: BlockGroup without a Block.");
                        AddBlock(data, blockStart, blockEnd, clusterTimecode, simple: false, hasReference, file);
                        break;
                    }
                }
                pos = elementEnd;
            }
        }

        /// SimpleBlock/Block body: track number (EBML vint), signed 16-bit timecode relative to the
        /// cluster, flags (SimpleBlock bit 7: keyframe; bits 1-2: lacing), then the frame. A Block
        /// in a BlockGroup is a keyframe when the group has no ReferenceBlock.
        static void AddBlock(ReadOnlySpan<byte> data, int pos, int end, long clusterTimecode, bool simple, bool hasReference, WebmFile file)
        {
            var (track, length) = ReadVint(data, pos, keepMarker: false);
            pos += length;
            if (pos + 3 > end) throw new InvalidDataException("WebM: truncated block header.");
            short relative = (short)((data[pos] << 8) | data[pos + 1]);
            byte flags = data[pos + 2];
            pos += 3;
            if ((flags & 0x06) != 0) throw new InvalidDataException("WebM: laced blocks are not supported.");
            if (!file.Frames.TryGetValue(track, out var frames)) throw new InvalidDataException($"WebM: block for undeclared track {track}.");
            bool key = simple ? (flags & 0x80) != 0 : !hasReference;
            long timestamp = (clusterTimecode + relative) * (long)file.TimecodeScale;
            frames.Add(new WebmFrame(pos, end - pos, timestamp, key));
        }

        // Scans an unknown-size cluster for the first element that cannot be a cluster child.
        static int ClusterEnd(ReadOnlySpan<byte> data, int pos, int end)
        {
            while (pos < end)
            {
                var (id, size, body) = ReadElementHeader(data, pos);
                if (id is IdCluster or IdCues or 0x1254C367 or 0x1043A770 or 0x1941A469 or IdInfo or IdTracks or 0x114D9B74) return pos;
                pos = End(data, body, size, end);
            }
            return end;
        }

        const uint IdCues = 0x1C53BB6B;

        static int End(ReadOnlySpan<byte> data, int body, ulong size, int limit = -1)
        {
            if (limit < 0) limit = data.Length;
            if (size == UnknownSize) return limit;
            long end = body + (long)size;
            if (end > limit) throw new InvalidDataException("WebM: element extends past its parent.");
            return (int)end;
        }

        static (uint id, ulong size, int body) ReadElementHeader(ReadOnlySpan<byte> data, int pos)
        {
            var (id, idLength) = ReadVint(data, pos, keepMarker: true);
            if (idLength > 4) throw new InvalidDataException("WebM: invalid element ID.");
            var (size, sizeLength) = ReadVint(data, pos + idLength, keepMarker: false);
            // All value bits set means "unknown size".
            if (size == (1UL << (7 * sizeLength)) - 1) size = UnknownSize;
            return ((uint)id, size, pos + idLength + sizeLength);
        }

        /// An EBML variable-length integer: the count of leading zero bits of the first byte gives
        /// the length. IDs keep the length marker; sizes and track numbers drop it.
        static (ulong value, int length) ReadVint(ReadOnlySpan<byte> data, int pos, bool keepMarker)
        {
            if (pos >= data.Length) throw new InvalidDataException("WebM: unexpected end of data.");
            byte first = data[pos];
            int length = 1;
            byte mask = 0x80;
            while (length <= 8 && (first & mask) == 0)
            {
                length++;
                mask >>= 1;
            }
            if (length > 8) throw new InvalidDataException("WebM: invalid variable-length integer.");
            if (pos + length > data.Length) throw new InvalidDataException("WebM: unexpected end of data.");
            ulong value = keepMarker ? first : (ulong)(first & (mask - 1));
            for (int i = 1; i < length; i++) value = (value << 8) | data[pos + i];
            return (value, length);
        }

        static ulong ReadUInt(ReadOnlySpan<byte> value)
        {
            if (value.Length > 8) throw new InvalidDataException("WebM: integer element longer than 8 bytes.");
            ulong v = 0;
            foreach (var b in value) v = (v << 8) | b;
            return v;
        }

        static double ReadFloat(ReadOnlySpan<byte> value) => value.Length switch
        {
            0 => 0,
            4 => BitConverter.Int32BitsToSingle((int)ReadUInt(value)),
            8 => BitConverter.Int64BitsToDouble((long)ReadUInt(value)),
            _ => throw new InvalidDataException("WebM: float element must be 4 or 8 bytes."),
        };
    }
}
