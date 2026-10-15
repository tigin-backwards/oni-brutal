using System.Diagnostics;

namespace UnityEngine.TextCore.LowLevel
{
    // Internal in Unity; TextMeshPro uses it (system font lookup) as an InternalsVisibleTo friend.
    // Not serialized.
    [DebuggerDisplay("{familyName} - {styleName}")]
    partial struct FontReference
    {
        // Written by the font engine's system-font lookup and read by TextMeshPro; nothing in
        // this assembly assigns them yet.
#pragma warning disable CS0649
        public string familyName;
        public string styleName;
        public int faceIndex;
        public string filePath;
#pragma warning restore CS0649
    }
}
