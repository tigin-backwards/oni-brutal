using System.Diagnostics;

namespace UnityEngine.TextCore.LowLevel
{
    // Internal in Unity; TextMeshPro uses it (system font lookup) as an InternalsVisibleTo friend.
    // Not serialized. Filled by the font engine's system-font lookup.
    [DebuggerDisplay("{familyName} - {styleName}")]
    partial struct FontReference
    {
        public string familyName;
        public string styleName;
        public int faceIndex;
        public string filePath;
    }
}
