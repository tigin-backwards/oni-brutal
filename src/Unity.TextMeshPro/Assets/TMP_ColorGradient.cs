using UnityEngine;

namespace TMPro
{
    // A colour preset for the vertex gradient: four corner colours and how they apply.
    public partial class TMP_ColorGradient
    {
        public TMP_ColorGradient()
        {
            colorMode = ColorMode.FourCornersGradient;
            topLeft = topRight = bottomLeft = bottomRight = Color.white;
        }

        public TMP_ColorGradient(Color color)
        {
            colorMode = ColorMode.FourCornersGradient;
            topLeft = topRight = bottomLeft = bottomRight = color;
        }

        public TMP_ColorGradient(Color color0, Color color1, Color color2, Color color3)
        {
            colorMode = ColorMode.FourCornersGradient;
            topLeft = color0;
            topRight = color1;
            bottomLeft = color2;
            bottomRight = color3;
        }
    }

    public partial struct VertexGradient
    {
        public VertexGradient(Color color)
        {
            topLeft = topRight = bottomLeft = bottomRight = color;
        }

        public VertexGradient(Color color0, Color color1, Color color2, Color color3)
        {
            topLeft = color0;
            topRight = color1;
            bottomLeft = color2;
            bottomRight = color3;
        }
    }
}
