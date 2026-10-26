using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore;

namespace TMPro
{
    // A sprite sheet for <sprite> tags: sprite characters (name, unicode) over sprite glyphs (the
    // sheet rect and metrics). Sprites are found by name, index or unicode; fallback sprite
    // assets are searched depth-first.
    public partial class TMP_SpriteAsset
    {
        Dictionary<string, int> m_IndexByName;
        Dictionary<int, int> m_IndexByNameHash;
        Dictionary<uint, int> m_IndexByUnicode;
        Dictionary<uint, TMP_SpriteCharacter> m_CharacterLookup;
        Dictionary<uint, TMP_SpriteGlyph> m_GlyphLookup;

        // A new asset has empty tables, so that code building one at run time (as Storage Info
        // Plus does) can add its glyphs and characters; loaded data replaces them.
        public TMP_SpriteAsset()
        {
            m_SpriteCharacterTable = new List<TMP_SpriteCharacter>();
            m_GlyphTable = new List<TMP_SpriteGlyph>();
        }

        public List<TMP_SpriteCharacter> spriteCharacterTable
        {
            get
            {
                if (m_IndexByName == null) UpdateLookupTables();
                return m_SpriteCharacterTable;
            }
            internal set => m_SpriteCharacterTable = value;
        }

        public Dictionary<uint, TMP_SpriteCharacter> spriteCharacterLookupTable
        {
            get
            {
                if (m_CharacterLookup == null) UpdateLookupTables();
                return m_CharacterLookup;
            }
        }

        public List<TMP_SpriteGlyph> spriteGlyphTable { get => m_GlyphTable; internal set => m_GlyphTable = value; }

        // Rebuilds the lookups; assets in the older format (spriteInfoList) are converted to
        // characters and glyphs first.
        public void UpdateLookupTables()
        {
            if ((m_SpriteCharacterTable == null || m_SpriteCharacterTable.Count == 0) && spriteInfoList != null && spriteInfoList.Count > 0)
                ConvertLegacySprites();
            m_SpriteCharacterTable ??= new List<TMP_SpriteCharacter>();
            m_GlyphTable ??= new List<TMP_SpriteGlyph>();
            m_GlyphLookup = new Dictionary<uint, TMP_SpriteGlyph>();
            foreach (var g in m_GlyphTable)
                if (g != null) m_GlyphLookup.TryAdd(g.index, g);
            m_IndexByName = new Dictionary<string, int>(System.StringComparer.Ordinal);
            m_IndexByNameHash = new Dictionary<int, int>();
            m_IndexByUnicode = new Dictionary<uint, int>();
            m_CharacterLookup = new Dictionary<uint, TMP_SpriteCharacter>();
            for (int i = 0; i < m_SpriteCharacterTable.Count; i++)
            {
                var c = m_SpriteCharacterTable[i];
                if (c == null) continue;
                if (m_GlyphLookup.TryGetValue(c.glyphIndex, out var glyph)) c.glyph = glyph;
                c.textAsset = this;
                if (c.name != null)
                {
                    m_IndexByName.TryAdd(c.name, i);
                    m_IndexByNameHash.TryAdd(TextHash.Of(c.name.ToLowerInvariant()), i);
                }
                if (c.unicode != 0 && c.unicode != 0xFFFE)
                {
                    m_IndexByUnicode.TryAdd(c.unicode, i);
                    m_CharacterLookup.TryAdd(c.unicode, c);
                }
            }
        }

        void ConvertLegacySprites()
        {
            m_SpriteCharacterTable = new List<TMP_SpriteCharacter>(spriteInfoList.Count);
            m_GlyphTable = new List<TMP_SpriteGlyph>(spriteInfoList.Count);
            for (int i = 0; i < spriteInfoList.Count; i++)
            {
                var s = spriteInfoList[i];
                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    metrics = new GlyphMetrics(s.width, s.height, s.xOffset, s.yOffset, s.xAdvance),
                    glyphRect = new GlyphRect((int)s.x, (int)s.y, (int)s.width, (int)s.height),
                    scale = 1f,
                    atlasIndex = 0,
                    sprite = s.sprite,
                };
                m_GlyphTable.Add(glyph);
                var character = new TMP_SpriteCharacter((uint)s.unicode, glyph) { name = s.name, scale = s.scale };
                m_SpriteCharacterTable.Add(character);
            }
        }

        public int GetSpriteIndexFromName(string name)
        {
            if (m_IndexByName == null) UpdateLookupTables();
            if (name != null && m_IndexByName.TryGetValue(name, out int index)) return index;
            return name != null && m_IndexByNameHash.TryGetValue(TextHash.Of(name.ToLowerInvariant()), out index) ? index : -1;
        }

        public int GetSpriteIndexFromUnicode(uint unicode)
        {
            if (m_IndexByUnicode == null) UpdateLookupTables();
            return m_IndexByUnicode.TryGetValue(unicode, out int index) ? index : -1;
        }

        // The sprite asset (this or a fallback) holding a sprite of that name.
        public static TMP_SpriteAsset SearchForSpriteByName(TMP_SpriteAsset spriteAsset, string name, bool includeFallbacks, out int spriteIndex)
        {
            spriteIndex = -1;
            if (spriteAsset == null) return null;
            var visited = new HashSet<int>();
            return Search(spriteAsset, a => a.GetSpriteIndexFromName(name), includeFallbacks, visited, out spriteIndex);
        }

        public static TMP_SpriteAsset SearchForSpriteByUnicode(TMP_SpriteAsset spriteAsset, uint unicode, bool includeFallbacks, out int spriteIndex)
        {
            spriteIndex = -1;
            if (spriteAsset == null) return null;
            var visited = new HashSet<int>();
            return Search(spriteAsset, a => a.GetSpriteIndexFromUnicode(unicode), includeFallbacks, visited, out spriteIndex);
        }

        static TMP_SpriteAsset Search(TMP_SpriteAsset asset, System.Func<TMP_SpriteAsset, int> find, bool includeFallbacks, HashSet<int> visited, out int spriteIndex)
        {
            spriteIndex = -1;
            if (asset == null || !visited.Add(asset.GetInstanceID())) return null;
            spriteIndex = find(asset);
            if (spriteIndex != -1) return asset;
            if (!includeFallbacks || asset.fallbackSpriteAssets == null) return null;
            foreach (var f in asset.fallbackSpriteAssets)
            {
                var found = Search(f, find, true, visited, out spriteIndex);
                if (found != null) return found;
            }
            spriteIndex = -1;
            return null;
        }

        public void SortGlyphTable() => m_GlyphTable?.Sort((a, b) => a.index.CompareTo(b.index));
    }

    public partial class TMP_SpriteCharacter
    {
        public TMP_SpriteCharacter()
        {
            m_ElementType = TextElementType.Sprite;
            m_Scale = 1f;
        }

        public TMP_SpriteCharacter(uint unicode, TMP_SpriteGlyph glyph)
        {
            m_ElementType = TextElementType.Sprite;
            m_Unicode = unicode;
            this.glyph = glyph;
            m_GlyphIndex = glyph.index;
            m_Scale = 1f;
        }

        public string name { get => m_Name; set => m_Name = value; }

        public int hashCode => TextHash.Of(m_Name);
    }

    public partial class TMP_SpriteGlyph
    {
        public TMP_SpriteGlyph() { }

        public TMP_SpriteGlyph(uint index, GlyphMetrics metrics, GlyphRect glyphRect, float scale, int atlasIndex)
            : base(index, metrics, glyphRect, scale, atlasIndex)
        {
        }

        public TMP_SpriteGlyph(uint index, GlyphMetrics metrics, GlyphRect glyphRect, float scale, int atlasIndex, Sprite sprite)
            : base(index, metrics, glyphRect, scale, atlasIndex)
        {
            this.sprite = sprite;
        }
    }
}
