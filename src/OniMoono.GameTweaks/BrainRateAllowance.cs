using System;

namespace OniMoono.GameTweaks
{
    /// The `brain-rate` tweak's allowance for one brain group (BrainRate): how many brains it may
    /// update this frame, for at most `rate` a second of real time and never more than the game's
    /// own count a frame (`perFrame`). Each frame earns `rate × dt`; whole updates are spent and
    /// the fraction carries over, so the updates spread evenly over frames (at 120 fps and 30 a
    /// second, one every fourth frame). Up to a second's worth of credit is carried over, so
    /// frames of uneven length still average the rate; as no frame gets more than `perFrame`, at
    /// `rate / perFrame` frames a second or fewer the game's own count runs every frame. Plain .NET
    /// (no game types): the unit tests compile it in.
    public sealed class BrainRateAllowance
    {
        readonly double rate;
        double credit;
        bool started;

        public BrainRateAllowance(double ratePerSecond)
        {
            if (!(ratePerSecond > 0)) throw new ArgumentOutOfRangeException(nameof(ratePerSecond));
            rate = ratePerSecond;
        }

        /// Brains this frame may update, 0 to `perFrame` (the game's own count, returned as it is
        /// when not positive). `realDt`: the frame's real time in seconds. The first frame gets
        /// `perFrame`.
        public int Take(double realDt, int perFrame)
        {
            if (perFrame <= 0) return perFrame;
            credit = started ? credit + rate * Math.Max(0, realDt) : perFrame;
            started = true;
            // A frame of exactly 1 / rate earns a whole update despite rounding.
            int n = Math.Min(perFrame, (int)(credit + 1e-6));
            // What is left over is kept, up to a second's worth: frames of uneven length (a slow
            // one, then fast ones) still average the rate, and no frame gets more than the game's.
            credit = Math.Min(Math.Max(rate, perFrame), Math.Max(0, credit - n));
            return n;
        }
    }
}
