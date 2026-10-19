using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    // The graphic an input field draws its caret and selection with: a child of the field's
    // viewport, masked and clipped like the text. The field writes its mesh.
    internal sealed class TMP_SelectionCaret : MaskableGraphic
    {
        Mesh m_WorkMesh;

        internal TMP_InputField Owner { get; set; }

        internal Mesh WorkMesh => m_WorkMesh ??= new Mesh { name = "TMP Caret Mesh" };

        // The input field generates the geometry.
        protected override void UpdateGeometry() { }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_WorkMesh != null) Destroy(m_WorkMesh);
        }
    }
}
