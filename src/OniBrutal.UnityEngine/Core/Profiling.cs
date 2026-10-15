using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Unity.Profiling
{
    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public partial struct ProfilerCategory
    {
        [FieldOffset(0)] private readonly ushort m_CategoryId;

        internal ProfilerCategory(ushort category) => m_CategoryId = category;

        // Unity's built-in category ids.
        public static ProfilerCategory Render => new(0);
        public static ProfilerCategory Scripts => new(1);
        public static ProfilerCategory Gui => new(4);
        public static ProfilerCategory Physics => new(5);
        public static ProfilerCategory Animation => new(6);
        public static ProfilerCategory Ai => new(7);
        public static ProfilerCategory Audio => new(8);
        public static ProfilerCategory Video => new(11);
        public static ProfilerCategory Particles => new(12);
        public static ProfilerCategory Lighting => new(13);
        public static ProfilerCategory Network => new(14);
        public static ProfilerCategory Loading => new(15);
        public static ProfilerCategory Vr => new(22);
        public static ProfilerCategory Memory => new(23);
        public static ProfilerCategory Internal => new(24);
        public static ProfilerCategory FileIO => new(25);
        public static ProfilerCategory Input => new(30);
        public static ProfilerCategory VirtualTexturing => new(31);
        public static ProfilerCategory Physics2D => new(33);

        internal ushort Id => m_CategoryId;
    }

    /// Instrumentation scopes. Nothing records them: a player without an attached profiler.
    public partial struct ProfilerMarker
    {
        internal readonly IntPtr m_Ptr;

        public ProfilerMarker(string name) => m_Ptr = IntPtr.Zero;
        public ProfilerMarker(ProfilerCategory category, string name) => m_Ptr = IntPtr.Zero;

        public IntPtr Handle => m_Ptr;

        [Conditional("ENABLE_PROFILER")] public void Begin() { }
        [Conditional("ENABLE_PROFILER")] public void Begin(UnityEngine.Object contextUnityObject) { }
        [Conditional("ENABLE_PROFILER")] public void End() { }
    }

    /// Counters the engine can actually report; other stat names yield an invalid recorder, as in
    /// Unity when a counter does not exist.
    public partial struct ProfilerRecorder
    {
        internal ulong handle;

        enum Stat : ulong { None = 0, SystemUsedMemory, GcUsedMemory, GcReservedMemory }

        public static ProfilerRecorder StartNew(ProfilerCategory category, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
            => new ProfilerRecorder { handle = (ulong)Lookup(statName) };

        static Stat Lookup(string statName) => statName switch
        {
            "System Used Memory" => Stat.SystemUsedMemory,
            "GC Used Memory" => Stat.GcUsedMemory,
            "GC Reserved Memory" => Stat.GcReservedMemory,
            _ => Stat.None,
        };

        public bool Valid => handle != 0;

        public long CurrentValue => (Stat)handle switch
        {
            Stat.SystemUsedMemory => Process.GetCurrentProcess().WorkingSet64,
            Stat.GcUsedMemory => GC.GetTotalMemory(false),
            Stat.GcReservedMemory => GC.GetGCMemoryInfo().HeapSizeBytes,
            _ => 0,
        };

        public void Dispose() => handle = 0;
    }
}
