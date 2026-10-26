// ProbeGone as the probe is built against: Gone exists, and Keeper's members name it. The mod
// folder gets v2's build instead (../v2), where Gone is gone, as the game's Worker is gone for a
// mod built before U52 (MissingTypes.cs).
namespace ProbeGone
{
    public class Gone { }

    public class GoneException : System.Exception { }

    public class Keeper
    {
        public static Gone Field;
        public static Gone Get() => null;
        public static void Removed() { }
        public static void Take(Gone g) { }
    }
}
