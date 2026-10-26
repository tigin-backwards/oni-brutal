// ProbeGone as the probe is built against: Gone exists, and Keeper's members name it. The mod
// folder gets v2's build instead (../v2), where Gone is gone, as the game's Worker is gone for a
// mod built before U52 (MissingTypes.cs), and the members MissingMembers.cs calls are gone or
// retyped, as SimMessages.SetCellProperties(int, byte) is for Trapdoors & Floor Hatches.
using System.Collections.Generic;

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

        public Keeper() { }
        public Keeper(int value) { }
        public static int RemovedStatic;
        public string RemovedField;
        public static int Retyped;
        public int InstanceRemoved(string s) => 0;
        public virtual void VirtualRemoved() { }
        public static void Changed(int value) { }
        public static T RemovedGeneric<T>(T value, List<T> list) => value;
        public static Dictionary<string, int[]> RemovedRefs(ref int a, out float b, int[] c, string[,] d, Keeper e, List<Keeper> f) { b = 0; return null; }
        public static int RemovedProp => 0;

        public class Inner
        {
            public static void Removed() { }
            public static int Field;
        }
    }

    public class Box<T>
    {
        public static void StaticRemoved() { }
        public void Removed(T value) { }
        public static U Map<U>(T value, U other) => other;
        public T Field;
        public static int StaticField;
    }

    public struct Pair
    {
        public void Removed() { }
    }
}

public class GlobalKeeper
{
    public static void SetCellProperties(int cell, byte flags) { }
    public static int Field;

    public class Nested
    {
        public static void Removed() { }
    }
}
