// ImageConversion's JPG and TGA encoders (lines "JPG ..."): for textures of each uncompressed,
// HDR, packed and block-compressed format, readable or not, at qualities in and out of 1..100,
// whether EncodeToJPG / EncodeToTGA return data (its length and the JPEG frame's components and
// sampling factors, or the TGA header) or null, and what they log. Each result is written to
// PROBE_OUT (else the temporary folder) as jpg-<runtime>/<case>.jpg or .tga, with EncodeToPNG of
// the same texture beside it (<case>.png) for the texels the player decodes. The textures'
// rows hold gradients that tell top from bottom and alpha that runs from 0 to 255, so a decode
// of both players' files shows orientation, alpha handling and the encoders' error.
//
// Run alone with PROBE_SCENARIO=jpg (at mod load). Compare with tools/UnityProbe/jpg-check.py
// ORIG_DIR OURS_DIR.
using System;
using System.IO;
using UnityEngine;

namespace SemanticsProbe
{
    public static class JpgEncode
    {
        static string dir;

        public static void Run()
        {
            string root = Environment.GetEnvironmentVariable("PROBE_OUT");
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            string runtime = Type.GetType("Mono.Runtime") != null ? "mono" : "coreclr";
            dir = Path.Combine(root, "jpg-" + runtime);
            Directory.CreateDirectory(dir);
            Rec.Log("JPG dir " + dir);

            var rgba = Gradient(64, 48, TextureFormat.RGBA32, false);
            Case("rgba32", rgba);
            foreach (int q in new[] { -5, 0, 1, 2, 10, 50, 75, 90, 91, 100, 101, 1000 }) Case("rgba32-q" + q, rgba, q);
            Case("rgb24", Gradient(33, 17, TextureFormat.RGB24, false));
            Case("argb32", Gradient(20, 20, TextureFormat.ARGB32, false));
            Case("bgra32", Gradient(20, 20, TextureFormat.BGRA32, false));
            Case("rgba32-mips", Gradient(32, 32, TextureFormat.RGBA32, true));
            Case("r8", Gradient(16, 16, TextureFormat.R8, false));
            Case("alpha8", Gradient(16, 16, TextureFormat.Alpha8, false));
            Case("rg16", Gradient(16, 16, TextureFormat.RG16, false));
            Case("r16", Gradient(16, 16, TextureFormat.R16, false));
            Case("rhalf", Gradient(16, 16, TextureFormat.RHalf, false));
            Case("rfloat", Gradient(16, 16, TextureFormat.RFloat, false));
            Case("rgb565", Gradient(16, 16, TextureFormat.RGB565, false));
            Case("rgba4444", Gradient(16, 16, TextureFormat.RGBA4444, false));
            Case("rgbahalf", Gradient(16, 16, TextureFormat.RGBAHalf, false, 2f));
            Case("rgbafloat", Gradient(16, 16, TextureFormat.RGBAFloat, false, 2f));
            Case("dxt1", Block(TextureFormat.DXT1, 8));
            Case("dxt5", Block(TextureFormat.DXT5, 16));
            var unreadable = Gradient(16, 16, TextureFormat.RGBA32, false);
            unreadable.Apply(false, true);
            Case("unreadable", unreadable);
            Case("one", Gradient(1, 1, TextureFormat.RGB24, false));
            Case("large", Smooth(1024, 768));
            Case("noise", Noise(256, 256));
            Rec.Log("JPG done");
        }

        // Red runs with x, green with y (so the bottom row, Unity's first, is darkest green), blue
        // is high in the top quarter, alpha runs with x + y from 0 to 255.
        static Texture2D Gradient(int w, int h, TextureFormat format, bool mips, float scale = 1f)
        {
            var t = new Texture2D(w, h, format, mips);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(scale * x / Mathf.Max(1, w - 1), scale * y / Mathf.Max(1, h - 1),
                        y >= h * 3 / 4 ? 0.9f : 0.1f, (float)(x + y) / Mathf.Max(1, w + h - 2));
            t.SetPixels(px);
            t.Apply(mips);
            return t;
        }

        static Texture2D Smooth(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool box = x > w / 4 && x < w / 2 && y > h / 2 && y < h * 7 / 8;
                    px[y * w + x] = box ? new Color32(240, 240, 240, 255)
                        : new Color32((byte)(x * 255 / w), (byte)(y * 255 / h), (byte)(128 + 100 * Mathf.Sin(x * 0.02f + y * 0.03f)), 255);
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static Texture2D Noise(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            uint s = 12345;
            for (int i = 0; i < px.Length; i++)
            {
                s = s * 1664525 + 1013904223;
                px[i] = new Color32((byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), (byte)s);
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        // An 8 x 8 block-compressed texture whose blocks hold two endpoint colours (red, blue).
        static Texture2D Block(TextureFormat format, int blockBytes)
        {
            var t = new Texture2D(8, 8, format, false);
            var data = new byte[4 * blockBytes];
            for (int b = 0; b < 4; b++)
            {
                int o = b * blockBytes + (blockBytes == 16 ? 8 : 0);
                if (blockBytes == 16) { data[b * 16] = 255; data[b * 16 + 1] = 0; }
                data[o] = 0x00; data[o + 1] = 0xF8; // c0 red (565)
                data[o + 2] = 0x1F; data[o + 3] = 0x00; // c1 blue
                data[o + 4] = 0x00; data[o + 5] = 0x55; data[o + 6] = 0xAA; data[o + 7] = 0xFF;
            }
            t.LoadRawTextureData(data);
            t.Apply();
            return t;
        }

        static void Case(string name, Texture2D t, int quality = int.MinValue)
        {
            Rec.Log("JPG case " + name + " " + t.format + " " + t.width + "x" + t.height + " readable=" + t.isReadable + (quality == int.MinValue ? "" : " q=" + quality));
            byte[] jpg = null;
            try { jpg = quality == int.MinValue ? t.EncodeToJPG() : t.EncodeToJPG(quality); }
            catch (Exception e) { Rec.Log("JPG " + name + " jpg EXCEPTION " + e.GetType().Name + ": " + e.Message); }
            Report(name, "jpg", jpg);
            if (quality != int.MinValue) return;
            byte[] tga = null;
            try { tga = t.EncodeToTGA(); }
            catch (Exception e) { Rec.Log("JPG " + name + " tga EXCEPTION " + e.GetType().Name + ": " + e.Message); }
            Report(name, "tga", tga);
            byte[] png = null;
            try { png = t.EncodeToPNG(); }
            catch (Exception e) { Rec.Log("JPG " + name + " png EXCEPTION " + e.GetType().Name + ": " + e.Message); }
            Report(name, "png", png);
        }

        static void Report(string name, string kind, byte[] data)
        {
            if (data == null) { Rec.Log("JPG " + name + " " + kind + " null"); return; }
            File.WriteAllBytes(Path.Combine(dir, name + "." + kind), data);
            string info = kind == "jpg" ? Frame(data) : kind == "tga" ? Hex(data, 0, Math.Min(18, data.Length)) : "";
            Rec.Log("JPG " + name + " " + kind + " " + data.Length + " bytes " + info);
        }

        static string Hex(byte[] d, int o, int n)
        {
            var s = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++) s.Append(d[o + i].ToString("x2"));
            return s.ToString();
        }

        // The markers up to the frame header, and the frame's precision, size and components
        // (id, sampling factors, quantisation table).
        static string Frame(byte[] d)
        {
            var s = new System.Text.StringBuilder();
            int i = 2;
            while (i + 4 <= d.Length && d[i] == 0xFF)
            {
                int marker = d[i + 1], len = (d[i + 2] << 8) | d[i + 3];
                s.Append(marker.ToString("x2")).Append(' ');
                if (marker == 0xC0 || marker == 0xC1 || marker == 0xC2)
                {
                    int n = d[i + 9];
                    s.Append("p=" + d[i + 4] + " " + ((d[i + 7] << 8) | d[i + 8]) + "x" + ((d[i + 5] << 8) | d[i + 6]) + " n=" + n);
                    for (int c = 0; c < n; c++)
                        s.Append(" [" + d[i + 10 + c * 3] + " " + (d[i + 11 + c * 3] >> 4) + "x" + (d[i + 11 + c * 3] & 15) + " q" + d[i + 12 + c * 3] + "]");
                    break;
                }
                if (marker == 0xDB) s.Append("q0[0]=" + d[i + 5] + " ");
                i += 2 + len;
            }
            return s.ToString();
        }
    }
}
