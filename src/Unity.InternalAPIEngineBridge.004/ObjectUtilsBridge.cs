using System.ComponentModel;
using UnityEngine;

namespace TMPro
{
    public static class ObjectUtilsBridge
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkDirty(this Object obj) => obj.MarkDirty();
    }
}
