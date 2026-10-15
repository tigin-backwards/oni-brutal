using System;
using System.Globalization;

namespace UnityEngine.Playables
{
    partial struct FrameRate
    {
        public static readonly FrameRate k_24Fps = new FrameRate(24u);
        public static readonly FrameRate k_23_976Fps = new FrameRate(24u, drop: true);
        public static readonly FrameRate k_25Fps = new FrameRate(25u);
        public static readonly FrameRate k_30Fps = new FrameRate(30u);
        public static readonly FrameRate k_29_97Fps = new FrameRate(30u, drop: true);
        public static readonly FrameRate k_50Fps = new FrameRate(50u);
        public static readonly FrameRate k_60Fps = new FrameRate(60u);
        public static readonly FrameRate k_59_94Fps = new FrameRate(60u, drop: true);

        // Integer frame rate; negative for NTSC drop-frame rates (rate × 1000/1001). 0 is invalid.
        [SerializeField]
        private int m_Rate;

        public FrameRate(uint frameRate = 0u, bool drop = false)
        {
            m_Rate = (drop ? -1 : 1) * (int)frameRate;
        }

        public bool dropFrame => m_Rate < 0;

        public double rate => dropFrame ? -m_Rate * 0.999000999000999 : m_Rate;

        public bool IsValid() => m_Rate != 0;

        public bool Equals(FrameRate other) => m_Rate == other.m_Rate;

        public override bool Equals(object obj) => obj is FrameRate other && Equals(other);

        public override int GetHashCode() => m_Rate;

        public static bool operator ==(FrameRate a, FrameRate b) => a.Equals(b);

        public static bool operator !=(FrameRate a, FrameRate b) => !a.Equals(b);

        public static bool operator <(FrameRate a, FrameRate b) => a.rate < b.rate;

        public static bool operator <=(FrameRate a, FrameRate b) => a.rate <= b.rate;

        public static bool operator >(FrameRate a, FrameRate b) => a.rate > b.rate;

        // Unity's operator compares with <= (a known quirk of its implementation); reproduced as is.
        public static bool operator >=(FrameRate a, FrameRate b) => a.rate <= b.rate;

        public override string ToString() => ToString(null, null);

        public string ToString(string format) => ToString(format, null);

        // "<rate> Fps"; drop-frame rates default to two decimals, others to none. Invariant culture by default.
        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = dropFrame ? "F2" : "F0";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return rate.ToString(format, formatProvider) + " Fps";
        }

        internal static int FrameRateToInt(FrameRate framerate) => framerate.m_Rate;

        /// The frame rate closest to `framerate`: the integer rate ⌈framerate⌉ or its drop-frame
        /// variant, whichever is nearer (ties go to the integer rate); 1 fps for rates ≤ 0.
        internal static FrameRate DoubleToFrameRate(double framerate)
        {
            uint ceiling = (uint)Math.Ceiling(framerate);
            if (ceiling == 0) return new FrameRate(1u);
            var drop = new FrameRate(ceiling, drop: true);
            return Math.Abs(framerate - drop.rate) < Math.Abs(framerate - ceiling) ? drop : new FrameRate(ceiling);
        }
    }
}
