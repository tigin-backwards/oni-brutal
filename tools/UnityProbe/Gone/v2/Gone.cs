// ProbeGone as the mod folder ships it: Gone and Keeper.Removed are gone, and Keeper's members
// that named Gone name object (as Workable.worker went from Worker to WorkerBase). The members
// MissingMembers.cs calls are gone, or take or hold a long where v1's took an int.
namespace ProbeGone
{
    public class Keeper
    {
        public static object Field;
        public static object Get() => null;
        public static void Take(object g) { }

        public Keeper() { }
        public static long Retyped;
        public static void Changed(long value) { }

        public class Inner { }
    }

    public class Box<T> { }

    public struct Pair { }
}

public class GlobalKeeper
{
    public class Nested { }
}
