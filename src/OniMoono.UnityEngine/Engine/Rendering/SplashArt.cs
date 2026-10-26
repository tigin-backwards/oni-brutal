using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OniMoono.Assets.Files;
using OniMoono.Assets.IO;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Assets;
using UnityEngine;

namespace OniMoono.Engine.Rendering
{
    /// The art of the player's own loading splash (--splash moo): the game's Gassy Moo comet
    /// (the meteor_gassymoo kanim's fall_loop), read straight from the game's data on a worker
    /// thread at start-up, without loading it as engine objects. Each frame is a list of
    /// textured quads in the kanim's screen space (pixels, y down), painted in order.
    internal sealed class SplashArt
    {
        public readonly struct Quad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, float alpha)
        {
            // Corners in order: top-left, top-right, bottom-right, bottom-left of the sprite.
            public readonly Vector2 P0 = p0, P1 = p1, P2 = p2, P3 = p3;
            // GL texture coordinates (v up: Unity's texel rows are stored bottom first).
            public readonly Vector2 Uv0 = uv0, Uv1 = uv1, Uv2 = uv2, Uv3 = uv3;
            public readonly float Alpha = alpha;
        }

        public int Width, Height;
        /// Mip 0 as RGBA8, rows bottom first.
        public byte[] Rgba;
        public float Rate;
        public Quad[][] Frames;
        public Rect Bounds;

        const string Kanim = "meteor_gassymoo";
        const string Bank = "fall_loop";

        /// The fall_loop seen from behind hides the face: these symbols are painted last, as
        /// onimaxxing.com's moo rain does.
        static readonly HashSet<string> FaceSymbols = new(StringComparer.OrdinalIgnoreCase) { "eye_left", "eye_right", "brow_one", "brow_2", "mouthal_area" };

        /// Loads the art; null when the game data has no such kanim (any thread).
        public static SplashArt Load()
        {
            foreach (var fileName in new[] { "sharedassets0.assets", "resources.assets" })
            {
                SerializedFile file;
                try
                {
                    file = AssetSystem.Get(fileName);
                }
                catch (IOException)
                {
                    continue;
                }
                byte[] anim = null, build = null;
                SerializedFile.ObjectInfo texture = null;
                foreach (var info in file.Objects)
                {
                    int classId = file.TypeOf(info).ClassId;
                    if (classId != 49 && classId != 28) continue;
                    var r = file.OpenObject(info);
                    int length = r.ReadInt32();
                    if (length != Kanim.Length + 5 && length != Kanim.Length + 6 && length != Kanim.Length + 2) continue;
                    var name = Encoding.UTF8.GetString(r.ReadSpan(length));
                    if (classId == 28 && name == Kanim + "_0") texture = info;
                    else if (classId == 49 && (name == Kanim + "_anim" || name == Kanim + "_build"))
                    {
                        r.Align(4);
                        var bytes = r.ReadBytes(r.ReadInt32());
                        if (name.EndsWith("_anim", StringComparison.Ordinal)) anim = bytes;
                        else build = bytes;
                    }
                }
                if (anim == null || build == null || texture == null) continue;
                var art = new SplashArt();
                if (!art.ReadTexture(file, texture)) return null;
                art.Build(anim, build);
                return art.Frames.Length > 0 ? art : null;
            }
            return null;
        }

        bool ReadTexture(SerializedFile file, SerializedFile.ObjectInfo info)
        {
            var type = file.TypeOf(info);
            var f = type.Tree != null ? TypeTreeReader.ReadObject(type.Tree, file.OpenObject(info)) : TypeTreeReader.ReadNative(28, file.OpenObject(info));
            Width = f.Int("m_Width");
            Height = f.Int("m_Height");
            var format = (TextureFormat)f.Int("m_TextureFormat");
            var texels = TexelSource.From(f).ReadMemory();
            try
            {
                Rgba = new byte[Width * Height * 4];
                unsafe
                {
                    fixed (byte* d = Rgba)
                    {
                        long mip0 = TextureFormats.MipSize(format, Width, Height);
                        if (format == TextureFormat.RGBA32) new ReadOnlySpan<byte>(texels.Pointer, Rgba.Length).CopyTo(Rgba);
                        else if (format == TextureFormat.ARGB32)
                            for (int i = 0; i < Rgba.Length; i += 4)
                            {
                                d[i] = texels.Pointer[i + 1]; d[i + 1] = texels.Pointer[i + 2]; d[i + 2] = texels.Pointer[i + 3]; d[i + 3] = texels.Pointer[i];
                            }
                        else if (TextureFormats.IsBlockCompressed(format) && TextureFormats.CanDecode(format))
                            TextureFormats.DecodeLevel(format, texels.Pointer, mip0, Width, Height, d, halfFloat: false);
                        else return false;
                    }
                }
                return true;
            }
            finally
            {
                texels.Free();
            }
        }

        static string KleiString(DataReader r) => Encoding.UTF8.GetString(r.ReadSpan(r.ReadInt32()));

        static Dictionary<int, string> HashNames(DataReader r)
        {
            var names = new Dictionary<int, string>();
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int hash = r.ReadInt32();
                names[hash] = KleiString(r);
            }
            return names;
        }

        sealed record SymbolFrame(int Start, int Duration, float PivotX, float PivotY, float PivotW, float PivotH, float U1, float V1, float U2, float V2);

        /// Klei's BILD (symbols and their frames' rectangles) and ANIM (per frame, each element's
        /// symbol frame and 2×3 transform) formats, as the game's KGlobalAnimParser reads them.
        void Build(byte[] animBytes, byte[] buildBytes)
        {
            var b = new DataReader(buildBytes);
            if (Encoding.ASCII.GetString(b.ReadSpan(4)) != "BILD") throw new InvalidDataException("not a BILD");
            int version = b.ReadInt32();
            int symbolCount = b.ReadInt32();
            b.ReadInt32();
            KleiString(b);
            var symbols = new Dictionary<int, List<SymbolFrame>>();
            for (int i = 0; i < symbolCount; i++)
            {
                int hash = b.ReadInt32();
                if (version > 9) b.ReadInt32();
                b.ReadInt32();
                b.ReadInt32();
                int frames = b.ReadInt32();
                var list = new List<SymbolFrame>();
                for (int j = 0; j < frames; j++)
                {
                    int start = b.ReadInt32(), duration = b.ReadInt32();
                    b.ReadInt32(); // image index (one page here)
                    list.Add(new SymbolFrame(start, duration, b.ReadSingle(), b.ReadSingle(), b.ReadSingle(), b.ReadSingle(), b.ReadSingle(), b.ReadSingle(), b.ReadSingle(), b.ReadSingle()));
                }
                symbols[hash] = list;
            }

            var a = new DataReader(animBytes);
            if (Encoding.ASCII.GetString(a.ReadSpan(4)) != "ANIM") throw new InvalidDataException("not an ANIM");
            a.ReadUInt32();
            a.ReadInt32();
            a.ReadInt32();
            int bankCount = a.ReadInt32();
            var banks = new List<(string Name, float Rate, List<List<(int Symbol, int Frame, float Alpha, float[] M)>> Frames)>();
            for (int i = 0; i < bankCount; i++)
            {
                var name = KleiString(a);
                a.ReadInt32();
                float rate = a.ReadSingle();
                int frameCount = a.ReadInt32();
                var frames = new List<List<(int, int, float, float[])>>();
                for (int j = 0; j < frameCount; j++)
                {
                    a.Skip(16);
                    int elements = a.ReadInt32();
                    var list = new List<(int, int, float, float[])>();
                    for (int k = 0; k < elements; k++)
                    {
                        int symbol = a.ReadInt32(), frame = a.ReadInt32();
                        a.ReadInt32();
                        a.ReadInt32();
                        float alpha = a.ReadSingle();
                        a.ReadSingle(); a.ReadSingle();
                        float tint = a.ReadSingle();
                        // m00, m10, m01, m11, m02, m12
                        var m = new[] { a.ReadSingle(), a.ReadSingle(), a.ReadSingle(), a.ReadSingle(), a.ReadSingle(), a.ReadSingle() };
                        a.ReadSingle();
                        list.Add((symbol, frame, alpha * tint, m));
                    }
                    frames.Add(list);
                }
                banks.Add((name, rate, frames));
            }
            a.ReadInt32();
            var names = HashNames(a);

            var bank = banks.Find(x => x.Name == Bank);
            if (bank.Frames == null) { Frames = []; return; }
            Rate = bank.Rate > 0 ? bank.Rate : 30;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var result = new List<Quad[]>();
            foreach (var frame in bank.Frames)
            {
                var back = new List<Quad>();
                var face = new List<Quad>();
                // The last element is the deepest.
                for (int i = frame.Count - 1; i >= 0; i--)
                {
                    var (symbol, index, alpha, m) = frame[i];
                    if (names.TryGetValue(symbol, out var symbolName) && symbolName.StartsWith("snapto", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!symbols.TryGetValue(symbol, out var spans)) continue;
                    var s = spans.Find(x => index >= x.Start && index < x.Start + Math.Max(1, x.Duration));
                    if (s == null || s.U2 <= s.U1 || s.V2 <= s.V1) continue;
                    Vector2 P(float x, float y) => new(m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]);
                    float x0 = s.PivotX - s.PivotW / 2, x1 = s.PivotX + s.PivotW / 2, y0 = s.PivotY - s.PivotH / 2, y1 = s.PivotY + s.PivotH / 2;
                    var q = new Quad(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1),
                        new Vector2(s.U1, 1 - s.V1), new Vector2(s.U2, 1 - s.V1), new Vector2(s.U2, 1 - s.V2), new Vector2(s.U1, 1 - s.V2), alpha);
                    foreach (var p in new[] { q.P0, q.P1, q.P2, q.P3 })
                    {
                        minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x);
                        minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y);
                    }
                    (symbolName != null && FaceSymbols.Contains(symbolName) ? face : back).Add(q);
                }
                back.AddRange(face);
                result.Add(back.ToArray());
            }
            Frames = result.ToArray();
            Bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }
}
