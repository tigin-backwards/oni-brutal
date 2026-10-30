namespace UnityEngine.Profiling
{
    /// Profiler samples of development builds (U60's public test build makes them). Nothing records
    /// them: a player without an attached profiler.
    public static partial class Profiler
    {
        public static void BeginSample(string name) { }
        public static void EndSample() { }
    }
}
