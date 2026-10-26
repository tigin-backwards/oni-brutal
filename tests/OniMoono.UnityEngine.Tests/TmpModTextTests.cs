using TMPro;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // Texts as three Workshop mods build them (docs/cleanroom/textmeshpro.md, "A font set to
    // null", "The ellipsis glyph in Bold and Italic texts", "Line gaps and preferred heights with
    // fallback glyphs"). The values are the shipped package's, from tools/UnityProbe's modtext
    // probe.
    public class TmpModTextTests : EngineTest
    {
        static Transform Canvas()
        {
            TmpTestSupport.LoadGameData();
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return cgo.transform;
        }

        static TextMeshProUGUI NewText(Vector2 size, bool active = true)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            if (!active) go.SetActive(false);
            go.transform.SetParent(Canvas(), false);
            ((RectTransform)go.transform).sizeDelta = size;
            return go.AddComponent<TextMeshProUGUI>();
        }

        static TMP_FontAsset Default => TMP_Settings.defaultFontAsset;

        // ----- A font set to null (Smart Coolers) -----

        [Fact]
        public void NullOnAWokenTextIsTheDefaultFont()
        {
            var t = NewText(new Vector2(400f, 50f));
            t.fontSize = 36f;
            t.text = "Cool Contents";
            t.font = null;
            Assert.Equal("LiberationSans SDF", Default.name);
            Assert.Same(Default, t.font);
            Assert.Same(Default.material, t.fontSharedMaterial);
            Assert.Equal(228.56f, t.preferredWidth);
            t.ForceMeshUpdate();
            Assert.Equal(13, t.textInfo.characterCount);
        }

        [Fact]
        public void NullAfterAnotherFontTakesTheDefaultAtOnce()
        {
            var t = NewText(new Vector2(400f, 50f));
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>("NotoSans-Regular");
            t.fontSize = 36f;
            t.text = "Cool Contents";
            t.ForceMeshUpdate();
            Assert.Equal(237.57f, t.preferredWidth);
            t.font = null;
            Assert.Same(Default, t.font);
            Assert.Same(Default.material, t.fontSharedMaterial);
            Assert.Equal(228.56f, t.preferredWidth);
        }

        // PLib's checkbox label: added to an inactive object, its font set to null before it
        // wakes; it takes the default font when it is activated.
        [Fact]
        public void NullBeforeWakingStaysNullUntilTheTextWakes()
        {
            var t = NewText(new Vector2(200f, 50f), active: false);
            t.font = null;
            Assert.Null(t.font);
            t.fontSize = 14f;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.gameObject.SetActive(true);
            Assert.Same(Default, t.font);
            t.alignment = TextAlignmentOptions.Center;
            t.text = "Cool Contents";
            t.ForceMeshUpdate();
            Assert.Equal(13, t.textInfo.characterCount);
            Assert.Equal(100.65f, t.preferredWidth);
            Assert.Equal(15.65f, t.preferredHeight);
        }
    }
}
