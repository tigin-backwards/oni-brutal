using System.Runtime.CompilerServices;

// The host creates the platform and device.
[assembly: InternalsVisibleTo("OniMoono")]
// Unit tests of the device's main-thread recording (no GL context needed).
[assembly: InternalsVisibleTo("OniBrutal.UnityEngine.Tests")]
