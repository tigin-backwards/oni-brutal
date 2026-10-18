using System;
using System.Collections;
using System.Collections.Generic;
using OniBrutal.Engine.IMGUI;

namespace UnityEngine
{
    /// The styles IMGUI controls draw with, a font and the text-field settings. The serialized
    /// field names and order are those of Unity's GUISkin assets (the built-in GameSkin
    /// carries no thumb-extent or mixed-slider styles).
    public sealed partial class GUISkin
    {
        [SerializeField] Font m_Font;
        [SerializeField] GUIStyle m_box = Named("box");
        [SerializeField] GUIStyle m_button = Named("button");
        [SerializeField] GUIStyle m_toggle = Named("toggle");
        [SerializeField] GUIStyle m_label = Named("label");
        [SerializeField] GUIStyle m_textField = Named("textfield");
        [SerializeField] GUIStyle m_textArea = Named("textarea");
        [SerializeField] GUIStyle m_window = Named("window");
        [SerializeField] GUIStyle m_horizontalSlider = Named("horizontalslider");
        [SerializeField] GUIStyle m_horizontalSliderThumb = Named("horizontalsliderthumb");
        [SerializeField] GUIStyle m_verticalSlider = Named("verticalslider");
        [SerializeField] GUIStyle m_verticalSliderThumb = Named("verticalsliderthumb");
        [SerializeField] GUIStyle m_horizontalScrollbar = Named("horizontalscrollbar");
        [SerializeField] GUIStyle m_horizontalScrollbarThumb = Named("horizontalscrollbarthumb");
        [SerializeField] GUIStyle m_horizontalScrollbarLeftButton = Named("horizontalscrollbarleftbutton");
        [SerializeField] GUIStyle m_horizontalScrollbarRightButton = Named("horizontalscrollbarrightbutton");
        [SerializeField] GUIStyle m_verticalScrollbar = Named("verticalscrollbar");
        [SerializeField] GUIStyle m_verticalScrollbarThumb = Named("verticalscrollbarthumb");
        [SerializeField] GUIStyle m_verticalScrollbarUpButton = Named("verticalscrollbarupbutton");
        [SerializeField] GUIStyle m_verticalScrollbarDownButton = Named("verticalscrollbardownbutton");
        [SerializeField] GUIStyle m_ScrollView = Named("scrollview");
        [SerializeField] GUIStyle[] m_CustomStyles = Array.Empty<GUIStyle>();
        [SerializeField] GUISettings m_Settings = new();

        static GUIStyle Named(string name) => new() { name = name };

        static GUIStyle errorStyle;

        /// What GetStyle returns for a missing name (and what copying a null style copies).
        internal static GUIStyle ErrorStyle => errorStyle ??= ErrorStyleTemplate();

        static GUIStyle ErrorStyleTemplate()
        {
            var s = new GUIStyle { name = "missing style" };
            s.normal.textColor = Color.red;
            return s;
        }

        public Font font { get => m_Font; set => m_Font = value; }

        public GUIStyle box { get => m_box; set => m_box = value; }
        public GUIStyle label { get => m_label; set => m_label = value; }
        public GUIStyle textField { get => m_textField; set => m_textField = value; }
        public GUIStyle textArea { get => m_textArea; set => m_textArea = value; }
        public GUIStyle button { get => m_button; set => m_button = value; }
        public GUIStyle toggle { get => m_toggle; set => m_toggle = value; }
        public GUIStyle window { get => m_window; set => m_window = value; }
        public GUIStyle horizontalSlider { get => m_horizontalSlider; set => m_horizontalSlider = value; }
        public GUIStyle horizontalSliderThumb { get => m_horizontalSliderThumb; set => m_horizontalSliderThumb = value; }
        public GUIStyle verticalSlider { get => m_verticalSlider; set => m_verticalSlider = value; }
        public GUIStyle verticalSliderThumb { get => m_verticalSliderThumb; set => m_verticalSliderThumb = value; }
        public GUIStyle horizontalScrollbar { get => m_horizontalScrollbar; set => m_horizontalScrollbar = value; }
        public GUIStyle horizontalScrollbarThumb { get => m_horizontalScrollbarThumb; set => m_horizontalScrollbarThumb = value; }
        public GUIStyle horizontalScrollbarLeftButton { get => m_horizontalScrollbarLeftButton; set => m_horizontalScrollbarLeftButton = value; }
        public GUIStyle horizontalScrollbarRightButton { get => m_horizontalScrollbarRightButton; set => m_horizontalScrollbarRightButton = value; }
        public GUIStyle verticalScrollbar { get => m_verticalScrollbar; set => m_verticalScrollbar = value; }
        public GUIStyle verticalScrollbarThumb { get => m_verticalScrollbarThumb; set => m_verticalScrollbarThumb = value; }
        public GUIStyle verticalScrollbarUpButton { get => m_verticalScrollbarUpButton; set => m_verticalScrollbarUpButton = value; }
        public GUIStyle verticalScrollbarDownButton { get => m_verticalScrollbarDownButton; set => m_verticalScrollbarDownButton = value; }
        public GUIStyle scrollView { get => m_ScrollView; set => m_ScrollView = value; }

        public GUIStyle[] customStyles
        {
            get => m_CustomStyles;
            set => m_CustomStyles = value;
        }

        public GUISettings settings => m_Settings ??= new GUISettings();

        const int StandardCount = 20;

        /// The standard styles, in field order.
        GUIStyle Standard(int i) => i switch
        {
            0 => m_box,
            1 => m_button,
            2 => m_toggle,
            3 => m_label,
            4 => m_textField,
            5 => m_textArea,
            6 => m_window,
            7 => m_horizontalSlider,
            8 => m_horizontalSliderThumb,
            9 => m_verticalSlider,
            10 => m_verticalSliderThumb,
            11 => m_horizontalScrollbar,
            12 => m_horizontalScrollbarThumb,
            13 => m_horizontalScrollbarLeftButton,
            14 => m_horizontalScrollbarRightButton,
            15 => m_verticalScrollbar,
            16 => m_verticalScrollbarThumb,
            17 => m_verticalScrollbarUpButton,
            18 => m_verticalScrollbarDownButton,
            19 => m_ScrollView,
            _ => null,
        };

        /// The standard or custom style of that name (ignoring case), or null.
        public GUIStyle FindStyle(string styleName)
        {
            if (styleName == null) return null;
            for (int i = 0; i < StandardCount; i++)
            {
                var s = Standard(i);
                if (s != null && string.Equals(s.name, styleName, StringComparison.OrdinalIgnoreCase)) return s;
            }
            if (m_CustomStyles != null)
                foreach (var s in m_CustomStyles)
                    if (s != null && string.Equals(s.name, styleName, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        /// The style of that name; a missing name logs a warning and gives the error style.
        public GUIStyle GetStyle(string styleName)
        {
            var s = FindStyle(styleName);
            if (s != null) return s;
            var during = Event.current != null ? EventTypeNames.Of(Event.current.rawType) : "";
            Debug.LogWarning($"Unable to find style '{styleName}' in skin '{name}' {during}");
            return ErrorStyle;
        }

        /// Every style: the standard ones, then the custom ones.
        public IEnumerator GetEnumerator() => AllStyles().GetEnumerator();

        IEnumerable<GUIStyle> AllStyles()
        {
            for (int i = 0; i < StandardCount; i++)
                if (Standard(i) is { } s) yield return s;
            if (m_CustomStyles == null) yield break;
            foreach (var s in m_CustomStyles)
                if (s != null) yield return s;
        }
    }
}
