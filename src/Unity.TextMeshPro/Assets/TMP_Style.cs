using System.Collections.Generic;
using UnityEngine;

namespace TMPro
{
    // A named style: rich text inserted for <style="name"> (the opening definition) and for
    // </style> (the closing one).
    public partial class TMP_Style
    {
        internal TMP_Style(string styleName, int styleHashCode, string styleOpeningDefinition, string styleClosingDefinition)
        {
            m_Name = styleName;
            m_HashCode = styleHashCode;
            m_OpeningDefinition = styleOpeningDefinition;
            m_ClosingDefinition = styleClosingDefinition;
            RefreshStyle();
        }

        public TMP_Style(string styleName, string styleOpeningDefinition, string styleClosingDefinition)
            : this(styleName, TextHash.Of(styleName), styleOpeningDefinition, styleClosingDefinition)
        {
        }

        public string name { get => m_Name; set { if (value != m_Name) m_Name = value; } }

        public int hashCode { get => m_HashCode; set { if (value != m_HashCode) m_HashCode = value; } }

        public string styleOpeningDefinition => m_OpeningDefinition;

        public string styleClosingDefinition => m_ClosingDefinition;

        public uint[] styleOpeningTagArray => m_OpeningTagArray;

        public uint[] styleClosingTagArray => m_ClosingTagArray;

        // Rebuilds the code point arrays from the definitions.
        public void RefreshStyle()
        {
            m_OpeningTagArray = CodePoints(m_OpeningDefinition);
            m_ClosingTagArray = CodePoints(m_ClosingDefinition);
        }

        static uint[] CodePoints(string s)
        {
            if (string.IsNullOrEmpty(s)) return new uint[0];
            var list = new List<uint>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    list.Add((uint)char.ConvertToUtf32(s[i], s[i + 1]));
                    i++;
                }
                else list.Add(s[i]);
            }
            return list.ToArray();
        }
    }

    // Named styles. The default sheet is the one TMP_Settings names.
    public partial class TMP_StyleSheet
    {
        Dictionary<int, TMP_Style> m_ByHash;
        Dictionary<string, TMP_Style> m_ByName;

        public static TMP_StyleSheet instance => TMP_Settings.defaultStyleSheet;

        public List<TMP_Style> styles => m_StyleList ??= new List<TMP_Style>();

        public TMP_Style GetStyle(int hashCode)
        {
            if (m_ByHash == null) BuildLookups();
            return m_ByHash.TryGetValue(hashCode, out var style) ? style : null;
        }

        public TMP_Style GetStyle(string name)
        {
            if (name == null) return null;
            if (m_ByName == null) BuildLookups();
            if (m_ByName.TryGetValue(name, out var style)) return style;
            foreach (var s in styles)
                if (s != null && string.Equals(s.name, name, System.StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        // Adds a style; a style of the same name or hash added later wins in the lookups.
        public void AddStyle(TMP_Style style)
        {
            if (style == null) return;
            styles.Add(style);
            if (m_ByHash == null) return;
            m_ByHash[style.hashCode] = style;
            if (style.name != null) m_ByName[style.name] = style;
        }

        public void RefreshStyles()
        {
            foreach (var s in styles) s?.RefreshStyle();
            BuildLookups();
        }

        void BuildLookups()
        {
            m_ByHash = new Dictionary<int, TMP_Style>();
            m_ByName = new Dictionary<string, TMP_Style>(System.StringComparer.Ordinal);
            foreach (var s in styles)
            {
                if (s == null) continue;
                m_ByHash[s.hashCode] = s;
                if (s.name != null) m_ByName[s.name] = s;
            }
        }
    }
}
