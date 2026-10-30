namespace UnityEngine.Assertions
{
    /// Assertions of development builds (U60's public test build calls them). A release build
    /// compiles them out, so they do nothing here.
    public static partial class Assert
    {
        public static void IsTrue(bool condition) { }
    }
}
