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

        // The hash of the asset's name, as rich-text tags refer to assets by name.
        public int hashCode
        {
            get
            {
                if (m_HashCode == 0) m_HashCode = TextHash.Of(name);
                return m_HashCode;
            }
            set => m_HashCode = value;
        }

        public FaceInfo faceInfo { get => m_FaceInfo; set => m_FaceInfo = value; }

        public Material material { get => m_Material; set => m_Material = value; }

        public int materialHashCode
        {
            get
            {
                if (m_MaterialHashCode == 0 && m_Material != null) m_MaterialHashCode = TextHash.Of(m_Material.name);
                return m_MaterialHashCode;
            }
            set => m_MaterialHashCode = value;
        }
    }
}
