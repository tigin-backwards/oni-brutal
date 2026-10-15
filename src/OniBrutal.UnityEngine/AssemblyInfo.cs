using System.Runtime.CompilerServices;

// Unity's engine modules grant these assemblies access to their internals, and the shipped
// package code uses that access (e.g. uGUI's UI Toolkit bridge, TextMeshPro's TextCore records).
// Our implementation of those modules must grant the same.
[assembly: InternalsVisibleTo("UnityEngine.UI")]
[assembly: InternalsVisibleTo("Unity.TextMeshPro")]
[assembly: InternalsVisibleTo("Unity.Timeline")]
// Engine tests exercise engine internals directly.
[assembly: InternalsVisibleTo("OniBrutal.UnityEngine.Tests")]
