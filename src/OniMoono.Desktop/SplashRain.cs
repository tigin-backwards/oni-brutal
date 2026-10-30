using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using OniMoono.Engine;
using OniMoono.Engine.Rendering;

namespace OniMoono.Desktop
{
    /// The player's own loading splash (--splash moo), as each device's presenter draws it
    /// (GlSplash, VkSplash): Gassy Moo comets falling over whatever the window last showed, so
    /// they keep moving while the main thread and the device thread are busy loading. Game
    /// frames are not changed: the comets are drawn into the window after a frame is copied
    /// there. The rain fades out once frames arrive steadily (the main menu), and does not come
    /// back.
    ///
    /// The comets follow onimaxxing.com's moo rain, after the game's GassyMooComet: one of two
    /// trajectories 15° off vertical (mirrored for the leftward one), the fall_loop at its
    /// authored rate, each comet at its own phase.
    internal sealed class SplashRain
    {
        readonly Task<SplashArt> art;
        readonly Stopwatch clock = Stopwatch.StartNew();
        bool failed, started;

        // Steady frames: this many in a row, each within SteadyGap of the one before.
        const int SteadyFrames = 30;
        const double SteadyGap = 0.1, FadeSeconds = 0.6, MaxSeconds = 300;
        double lastFrame = -1, fadeStart = -1;
        int steady;

        public SplashRain(Task<SplashArt> art) => this.art = art;

        /// The rain is still to be drawn (it has not finished fading out).
        public bool Active => !failed && (fadeStart < 0 || Now - fadeStart < FadeSeconds) && Now < MaxSeconds;

        double Now => clock.Elapsed.TotalSeconds;

        /// A game frame was presented.
        public void GameFrame()
        {
            double now = Now;
            steady = lastFrame >= 0 && now - lastFrame < SteadyGap ? steady + 1 : 0;
            lastFrame = now;
            if (steady >= SteadyFrames && fadeStart < 0)
            {
                fadeStart = now;
                EngineLog.Engine($"splash: fading out after {now:F1} s");
            }
        }

        struct Comet
        {
            public float X, Size, Delay, Duration;
            public int Phase;
            public bool FlipX;
        }

        Comet[] comets;

        static Comet[] Roll(int count)
        {
            var random = new Random();
            var result = new Comet[count];
            for (int i = 0; i < count; i++)
                result[i] = new Comet
                {
                    X = (float)(random.NextDouble() * 1.1 - 0.05),
                    Size = (float)(0.3 + random.NextDouble() * 0.25),
                    // The first comets come in at once; the rest staggered.
                    Delay = i < 2 ? (float)(random.NextDouble() * 0.5) : (float)(random.NextDouble() * 4),
                    Duration = (float)(3.5 + random.NextDouble() * 2.5),
                    Phase = random.Next(1000),
                    FlipX = random.Next(2) == 0,
                };
            return result;
        }

        /// The art, once loaded (null until then, or when the game data has none: the rain is
        /// then over).
        public SplashArt Art
        {
            get
            {
                if (failed || !art.IsCompleted) return null;
                if (art.IsFaulted || art.Result == null)
                {
                    failed = true;
                    EngineLog.Engine("splash: no Gassy Moo art in the game data" + (art.Exception != null ? ": " + art.Exception.InnerException?.Message : ""));
                    return null;
                }
                return art.Result;
            }
        }

        /// The renderer could not draw it: the rain is over.
        public void Fail(string why)
        {
            failed = true;
            EngineLog.Engine("splash: " + why);
        }

        /// Starts the rain once the renderer has made its resources for the art.
        public void Start()
        {
            if (started) return;
            started = true;
            comets = Roll(8);
            clock.Restart();
            lastFrame = fadeStart = -1;
            steady = 0;
            EngineLog.Engine($"splash: Gassy Moo rain ({art.Result.Frames.Length} frames at {art.Result.Rate} fps)");
        }

        /// The atlas's texels premultiplied by alpha: its transparent texels are white, which
        /// filtering and the smaller mips would otherwise bleed into the sprites' soft edges as
        /// a pale fringe. Rows bottom first, as SplashArt has them.
        public static byte[] PremultipliedTexels(SplashArt a)
        {
            var texels = (byte[])a.Rgba.Clone();
            for (int i = 0; i < texels.Length; i += 4)
            {
                int alpha = texels[i + 3];
                texels[i] = (byte)((texels[i] * alpha + 127) / 255);
                texels[i + 1] = (byte)((texels[i + 1] * alpha + 127) / 255);
                texels[i + 2] = (byte)((texels[i + 2] * alpha + 127) / 255);
            }
            return texels;
        }

        /// The comets' triangles for a `width`×`height` window: per vertex its position in
        /// pixels from the top left, its atlas coordinates (OpenGL's, v up) and its opacity.
        /// Returns the fade (1 until the rain fades out).
        public float Vertices(int width, int height, List<float> vertices)
        {
            vertices.Clear();
            var a = art.Result;
            double now = Now;
            float fade = fadeStart < 0 ? 1f : (float)Math.Max(0, 1 - (now - fadeStart) / FadeSeconds);
            float bw = a.Bounds.width, bh = a.Bounds.height, cx = a.Bounds.center.x, cy = a.Bounds.center.y;
            float drift = MathF.Tan(15f * MathF.PI / 180f);
            foreach (var c in comets)
            {
                double local = now - c.Delay;
                if (local < 0) continue;
                float progress = (float)(local % c.Duration / c.Duration);
                float alpha = fade * (progress < 0.08f ? progress / 0.08f : progress > 0.92f ? (1 - progress) / 0.08f : 1);
                float s = c.Size * height / Math.Max(bw, bh);
                float cometH = bh * s;
                float startY = -cometH / 2 - 20, endY = height + cometH / 2;
                float y = startY + (endY - startY) * progress;
                float dir = c.FlipX ? -1 : 1;
                float x = c.X * width + drift * (endY - startY) * progress * dir;
                var frame = a.Frames[(int)((long)(local * a.Rate) + c.Phase) % a.Frames.Length];
                foreach (var q in frame)
                {
                    void V(UnityEngine.Vector2 p, UnityEngine.Vector2 uv)
                    {
                        vertices.Add(x + s * dir * (p.x - cx));
                        vertices.Add(y + s * (p.y - cy));
                        vertices.Add(uv.x);
                        vertices.Add(uv.y);
                        vertices.Add(alpha * q.Alpha);
                    }
                    V(q.P0, q.Uv0); V(q.P1, q.Uv1); V(q.P2, q.Uv2);
                    V(q.P0, q.Uv0); V(q.P2, q.Uv2); V(q.P3, q.Uv3);
                }
            }
            return fade;
        }

        // The bar's colours, from the game's menus: its dark purple panels and pink highlights.
        static readonly (float R, float G, float B) Track = (0.20f, 0.18f, 0.27f), Fill = (0.83f, 0.35f, 0.55f);

        /// A rectangle of the progress bar, in pixels from the bottom left (OpenGL's window
        /// coordinates), with its colour.
        public readonly record struct BarRect(int X, int Y, int Width, int Height, float R, float G, float B);

        /// Start-up done so far (StartupProgress), as a thin bar low in the window, over a dark
        /// track; gone once the rain fades.
        public static void Bar(int width, int height, float fade, List<BarRect> rects)
        {
            rects.Clear();
            if (fade < 1f) return;
            float progress = StartupProgress.Value;
            int barWidth = Math.Max(120, width * 2 / 5), barHeight = Math.Max(4, height / 160);
            int x = (width - barWidth) / 2, y = Math.Max(barHeight * 2, height / 14);
            rects.Add(new BarRect(x - 2, y - 2, barWidth + 4, barHeight + 4, Track.R, Track.G, Track.B));
            // The fill's edge column is part-covered, so the edge moves on smoothly even when
            // slower than a pixel a frame.
            float edge = barWidth * progress;
            int filled = (int)edge;
            if (filled > 0) rects.Add(new BarRect(x, y, filled, barHeight, Fill.R, Fill.G, Fill.B));
            float cover = edge - filled;
            if (filled < barWidth && cover > 0)
                rects.Add(new BarRect(x + filled, y, 1, barHeight, Track.R + (Fill.R - Track.R) * cover, Track.G + (Fill.G - Track.G) * cover, Track.B + (Fill.B - Track.B) * cover));
        }
    }
}
