using System;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // TMP_Text.SetCharArray(ReadOnlySpan<char>), which U60's LocText.SetContent calls: it behaves
    // as the char[] overload given the span's characters (docs/cleanroom/textmeshpro.md, "U60's
    // package (Unity 6000.3.17f1, game build 761015)").
    public class TmpSetCharArraySpanTests : EngineTest
    {
        static TextMeshProUGUI NewText() =>
            TmpTestSupport.NewText("start", TmpTestSupport.Graystroke, 16f, new Vector2(200f, 50f));

        [Theory]
        [InlineData("")]
        [InlineData("Oxygen")]
        [InlineData("Super Coolant Gas, and a line long enough to wrap in the rect")]
        [InlineData("<b>Bold</b> and <color=#FF0000>red</color> <size=20>big</size>")]
        [InlineData("<link=\"x\">Link</link>\nsecond line")]
        public void TheSpanGivesWhatTheArrayGives(string text)
        {
            var fromArray = NewText();
            var fromSpan = NewText();
            fromArray.SetCharArray(text.ToCharArray());
            // The span is a slice of a larger buffer, as the game's builder hands it.
            char[] buffer = ("[" + text + "]").ToCharArray();
            fromSpan.SetCharArray(new ReadOnlySpan<char>(buffer, 1, text.Length));
            fromArray.ForceMeshUpdate(true);
            fromSpan.ForceMeshUpdate(true);

            Assert.Equal(text, fromSpan.text);
            Assert.Equal(fromArray.text, fromSpan.text);
            Assert.Equal(fromArray.GetParsedText(), fromSpan.GetParsedText());

            var a = fromArray.textInfo;
            var b = fromSpan.textInfo;
            Assert.Equal(a.characterCount, b.characterCount);
            Assert.Equal(a.lineCount, b.lineCount);
            Assert.Equal(a.wordCount, b.wordCount);
            Assert.Equal(a.linkCount, b.linkCount);
            for (int i = 0; i < a.characterCount; i++)
            {
                var ca = a.characterInfo[i];
                var cb = b.characterInfo[i];
                Assert.Equal(ca.character, cb.character);
                Assert.Equal(ca.index, cb.index);
                Assert.Equal(ca.isVisible, cb.isVisible);
                Assert.Equal(ca.lineNumber, cb.lineNumber);
                Assert.Equal(ca.pointSize, cb.pointSize);
                Assert.Equal(ca.style, cb.style);
                Assert.Equal(ca.color, cb.color);
                Assert.Equal(ca.origin, cb.origin);
                Assert.Equal(ca.xAdvance, cb.xAdvance);
                Assert.Equal(ca.bottomLeft, cb.bottomLeft);
                Assert.Equal(ca.topRight, cb.topRight);
            }

            Assert.Equal(fromArray.preferredWidth, fromSpan.preferredWidth);
            Assert.Equal(fromArray.preferredHeight, fromSpan.preferredHeight);
            Assert.Equal(fromArray.GetPreferredValues(150f, 0f), fromSpan.GetPreferredValues(150f, 0f));
        }

        [Fact]
        public void TheSameCharactersAgainMakeNoString()
        {
            var t = NewText();
            char[] buffer = "Carbon Dioxide".ToCharArray();
            t.SetCharArray(new ReadOnlySpan<char>(buffer));
            string first = t.text;
            t.ForceMeshUpdate(true);

            long before = GC.GetAllocatedBytesForCurrentThread();
            t.SetCharArray(new ReadOnlySpan<char>(buffer));
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0, after - before);
            Assert.Same(first, t.text);
        }

        [Fact]
        public void AnEmptySpanClearsTheText()
        {
            var t = NewText();
            t.SetCharArray(ReadOnlySpan<char>.Empty);
            t.ForceMeshUpdate(true);
            Assert.Equal("", t.text);
            Assert.Equal(0, t.textInfo.characterCount);
        }
    }
}
