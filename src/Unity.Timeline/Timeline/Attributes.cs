using System;
using UnityEngine;

namespace UnityEngine.Timeline
{
    // A field or class whose values Timeline does not animate.
    public partial class NotKeyableAttribute
    {
        public NotKeyableAttribute() { }
    }

    // The type of object a track binds to.
    public partial class TrackBindingTypeAttribute
    {
        public readonly Type type;

        public TrackBindingTypeAttribute(Type type)
        {
            this.type = type;
        }
    }

    // A type of clip a track accepts.
    public partial class TrackClipTypeAttribute
    {
        public readonly Type inspectedType;
        public readonly bool allowAutoCreate;

        public TrackClipTypeAttribute(Type clipClass)
        {
            inspectedType = clipClass;
            allowAutoCreate = true;
        }

        public TrackClipTypeAttribute(Type clipClass, bool allowAutoCreate)
        {
            inspectedType = clipClass;
            this.allowAutoCreate = allowAutoCreate;
        }
    }

    // The colour of a track in the Timeline editor (alpha 1).
    public partial class TrackColorAttribute
    {
        Color m_Color;

        public Color color => m_Color;

        public TrackColorAttribute(float r, float g, float b)
        {
            m_Color = new Color(r, g, b);
        }
    }
}
