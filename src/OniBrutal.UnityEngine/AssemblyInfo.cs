using System.Runtime.CompilerServices;

// Unity's engine modules grant these assemblies access to their internals, and the shipped
// package code uses that access (e.g. uGUI's UI Toolkit bridge, TextMeshPro's TextCore records).
// Our implementation of those modules must grant the same.
[assembly: InternalsVisibleTo("UnityEngine.UI")]
[assembly: InternalsVisibleTo("Unity.TextMeshPro")]
[assembly: InternalsVisibleTo("Unity.Timeline")]
[assembly: InternalsVisibleTo("Unity.InternalAPIEngineBridge.004")]
// Engine tests exercise engine internals directly.
[assembly: InternalsVisibleTo("OniBrutal.UnityEngine.Tests")]

// The desktop platform implements the engine's platform and render device.
[assembly: InternalsVisibleTo("OniBrutal.Desktop")]

// The host configures the engine (paths, log, player loop).
[assembly: InternalsVisibleTo("OniBrutal")]
