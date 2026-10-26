// ProbeGone as the mod folder ships it: Gone and Keeper.Removed are gone, and Keeper's members
// that named Gone name object (as Workable.worker went from Worker to WorkerBase).
namespace ProbeGone
{
    public class Keeper
    {
        public static object Field;
        public static object Get() => null;
        public static void Take(object g) { }
    }
}
