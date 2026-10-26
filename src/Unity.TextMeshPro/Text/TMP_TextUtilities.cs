using UnityEngine;

namespace TMPro
{
    // Hit tests of screen points against a text's characters, words, lines and links, through
    // RectTransformUtility with the canvas's camera (null for an overlay canvas).
    public static partial class TMP_TextUtilities
    {
        // The point in the text's local space, when the screen point lies on its plane.
        static bool ToLocal(TMP_Text text, Vector3 position, Camera camera, out Vector3 local)
        {
            local = default;
            if (text == null) return false;
            var rt = text.rectTransform;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, position, camera, out var world)) return false;
            local = rt.InverseTransformPoint(world);
            return true;
        }

        static bool InQuad(in TMP_CharacterInfo c, Vector3 p)
        {
            // The quad may be sheared (italic): test the point against its two triangles.
            return InTriangle(p, c.bottomLeft, c.topLeft, c.topRight) || InTriangle(p, c.topRight, c.bottomRight, c.bottomLeft);
        }

        static bool InTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        static float Cross(Vector3 p, Vector3 a, Vector3 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        // The character whose quad contains the point, or -1.
        public static int FindIntersectingCharacter(TMP_Text text, Vector3 position, Camera camera, bool visibleOnly)
        {
            if (!ToLocal(text, position, camera, out var p)) return -1;
            var info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                ref var c = ref info.characterInfo[i];
                if (visibleOnly && !c.isVisible) continue;
                if (InQuad(c, p)) return i;
            }
            return -1;
        }

        // The word with a character whose quad contains the point, or -1.
        public static int FindIntersectingWord(TMP_Text text, Vector3 position, Camera camera)
        {
            if (!ToLocal(text, position, camera, out var p)) return -1;
            var info = text.textInfo;
            for (int w = 0; w < info.wordCount; w++)
            {
                ref var word = ref info.wordInfo[w];
                for (int i = word.firstCharacterIndex; i <= word.lastCharacterIndex && i < info.characterCount; i++)
                {
                    ref var c = ref info.characterInfo[i];
                    if (!c.isVisible) continue;
                    // A word's area spans its line's ascender to descender.
                    ref var line = ref info.lineInfo[c.lineNumber];
                    if (p.x >= c.origin && p.x <= c.xAdvance && p.y >= line.descender && p.y <= line.ascender) return w;
                }
            }
            return -1;
        }

        // The line whose ascender-to-descender band contains the point's height, or -1.
        public static int FindIntersectingLine(TMP_Text text, Vector3 position, Camera camera)
        {
            if (!ToLocal(text, position, camera, out var p)) return -1;
            var info = text.textInfo;
            for (int k = 0; k < info.lineCount; k++)
            {
                ref var line = ref info.lineInfo[k];
                if (p.y <= line.ascender && p.y >= line.descender) return k;
            }
            return -1;
        }

        // The link with a character quad containing the point, or -1.
        public static int FindIntersectingLink(TMP_Text text, Vector3 position, Camera camera)
        {
            if (!ToLocal(text, position, camera, out var p)) return -1;
            var info = text.textInfo;
            for (int l = 0; l < info.linkCount; l++)
            {
                ref var link = ref info.linkInfo[l];
                for (int i = link.linkTextfirstCharacterIndex; i < link.linkTextfirstCharacterIndex + link.linkTextLength && i < info.characterCount; i++)
                    if (InQuad(info.characterInfo[i], p)) return l;
            }
            return -1;
        }

        // Whether the screen point falls within the rect transform.
        public static bool IsIntersectingRectTransform(RectTransform rectTransform, Vector3 position, Camera camera)
        {
            if (rectTransform == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, position, camera);
        }

        // The character whose quad is nearest to the point (for the input field's caret).
        internal static int FindNearestCharacterOnLine(TMP_Text text, Vector3 localPoint, int line)
        {
            var info = text.textInfo;
            if (line < 0 || line >= info.lineCount) return -1;
            ref var li = ref info.lineInfo[line];
            int best = li.firstCharacterIndex;
            float bestDistance = float.MaxValue;
            for (int i = li.firstCharacterIndex; i <= li.lastCharacterIndex && i < info.characterCount; i++)
            {
                ref var c = ref info.characterInfo[i];
                float mid = (c.origin + c.xAdvance) * 0.5f;
                float d = Mathf.Abs(localPoint.x - mid);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        internal static bool ScreenToLocal(TMP_Text text, Vector3 position, Camera camera, out Vector3 local) => ToLocal(text, position, camera, out local);

        // ----- Name hashes -----

        // The hash rich-text tags and the assets' hashCode use: over the UTF-16 code units from
        // 0, hash = (hash << 5) + hash ^ c with ASCII a-z taken as A-Z (nothing else changes
        // case), in 32-bit wrapping arithmetic. Null hashes to 0.
        public static int GetHashCode(string s)
        {
            if (s == null) return 0;
            int hash = 0;
            unchecked
            {
                foreach (char c in s) hash = ((hash << 5) + hash) ^ AsciiUpper(c);
            }
            return hash;
        }

        // The same over the code units as they are (null throws).
        public static int GetSimpleHashCode(string s)
        {
            int hash = 0;
            unchecked
            {
                for (int i = 0; i < s.Length; i++) hash = ((hash << 5) + hash) ^ s[i];
            }
            return hash;
        }

        // From 5381, with ASCII A-Z taken as a-z (null throws).
        public static uint GetSimpleHashCodeLowercase(string s)
        {
            uint hash = 5381;
            unchecked
            {
                for (int i = 0; i < s.Length; i++) hash = ((hash << 5) + hash) ^ AsciiLower(s[i]);
            }
            return hash;
        }

        // GetHashCode's bits as unsigned (null throws, measured).
        public static uint GetHashCodeCaseInSensitive(string s)
        {
            uint hash = 0;
            unchecked
            {
                for (int i = 0; i < s.Length; i++) hash = ((hash << 5) + hash) ^ AsciiUpper(s[i]);
            }
            return hash;
        }

        static char AsciiUpper(char c) => c >= 'a' && c <= 'z' ? (char)(c - 32) : c;

        static char AsciiLower(char c) => c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
    }
}
