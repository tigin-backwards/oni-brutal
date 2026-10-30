namespace UnityEngine.Assertions
{
    /// Assertions of development builds; U60's public test build is one and calls them. A failed
    /// assertion is logged (LogType.Assert) and execution goes on: Unity's raiseExceptions is off.
    public static partial class Assert
    {
        public static void IsTrue(bool condition)
        {
            if (!condition) Debug.LogAssertion("Assertion failure. Value was False\nExpected: True");
        }
    }
}
