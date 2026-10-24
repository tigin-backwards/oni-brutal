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

        // Culled only when the clip rect is invalid, whatever its rect, its mesh and the clip
        // rect are (measured: at its defaults wholly outside its viewport's clip it is drawn,
        // and Cull(far, true) un-culls it). The clip still cuts its pixels.
        public override void Cull(Rect clipRect, bool validRect)
        {
            bool cull = !validRect;
            if (canvasRenderer.cull == cull) return;
            canvasRenderer.cull = cull;
            onCullStateChanged.Invoke(cull);
            OnCullingChanged();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_WorkMesh != null) Destroy(m_WorkMesh);
        }
    }
}
