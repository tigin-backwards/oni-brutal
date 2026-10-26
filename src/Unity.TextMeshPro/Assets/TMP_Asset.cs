using UnityEngine;
using UnityEngine.TextCore;

namespace TMPro
{
    // Base of font and sprite assets: a face description and the material that renders it.
    public abstract partial class TMP_Asset
    {
        int m_HashCode;
        int m_MaterialHashCode;

        public string version { get => m_Version; internal set => m_Version = value; }

        public int instanceID => GetInstanceID();

        // The key MaterialReferenceManager and rich-text tags find the asset by: the hash of its
        // name (TMP_TextUtilities.GetHashCode), taken when first read while 0 and kept through
        // renames; a value set is kept.
        public int hashCode
        {
            get
            {
                if (m_HashCode == 0) m_HashCode = TMP_TextUtilities.GetHashCode(name);
                return m_HashCode;
            }
            set => m_HashCode = value;
        }

        public FaceInfo faceInfo { get => m_FaceInfo; set => m_FaceInfo = value; }

        public Material material { get => m_Material; set => m_Material = value; }

        // The material's key in MaterialReferenceManager, taken when first read while 0 as
        // GetSimpleHashCode(name + " Atlas Material"), which the shipped package reports for
        // most game fonts and for a font asset made at run time (with its name then). So fonts
        // that share one material have keys of their own (measured: GRAYSTROKE NAME SDF and
        // GRAYSTROKE OUTLINE SDF both add).
        public int materialHashCode
        {
            get
            {
                if (m_MaterialHashCode == 0) m_MaterialHashCode = TMP_TextUtilities.GetSimpleHashCode(name + " Atlas Material");
                return m_MaterialHashCode;
            }
            set => m_MaterialHashCode = value;
        }
    }
}
