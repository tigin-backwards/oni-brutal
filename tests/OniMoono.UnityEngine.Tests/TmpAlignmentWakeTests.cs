using System.Reflection;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // An alignment set before a text first wakes (textmeshpro.md, "Alignment set before a text
    // first wakes", measured with UnityProbe's alignment scenario, `AL` lines).
    public class TmpAlignmentWakeTests : EngineTest
    {
        static readonly FieldInfo StoredField = typeof(TMP_Text).GetField("m_textAlignment", BindingFlags.Instance | BindingFlags.NonPublic);

        static int Stored(TMP_Text t) => (int)(TextAlignmentOptions)StoredField.GetValue(t);

        // A text that has never woken, its data storing `stored`, as a loaded LocText's: made
        // under an inactive parent, so it does not wake yet.
        static (TMP_Text text, GameObject parent) Unwoken(int stored)
        {
            TmpTestSupport.LoadGameData();
            var parent = new GameObject("Hidden", typeof(RectTransform));
            parent.SetActive(false);
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            StoredField.SetValue(t, (TextAlignmentOptions)stored);
            return (t, parent);
        }

        [Theory]
        [InlineData(257, TextAlignmentOptions.TopLeft)]
        [InlineData(5, TextAlignmentOptions.Center)]
        [InlineData(514, TextAlignmentOptions.Center)]
        [InlineData(513, TextAlignmentOptions.Left)]
        [InlineData(516, TextAlignmentOptions.Right)]
        [InlineData(0, TextAlignmentOptions.TopLeft)]
        [InlineData(8200, (TextAlignmentOptions)8200)]
        public void TheStoredValueReplacesASetBeforeTheFirstWake(int stored, TextAlignmentOptions shown)
        {
            var (t, parent) = Unwoken(stored);
            // The getters report the serialized halves: the defaults, Left and Top.
            Assert.Equal(TextAlignmentOptions.TopLeft, t.alignment);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            // The setter changes the halves at once and leaves the stored value.
            Assert.Equal(TextAlignmentOptions.MidlineLeft, t.alignment);
            Assert.Equal(HorizontalAlignmentOptions.Left, t.horizontalAlignment);
            Assert.Equal(VerticalAlignmentOptions.Geometry, t.verticalAlignment);
            Assert.Equal(stored, Stored(t));
            parent.SetActive(true);
            Assert.Equal(shown, t.alignment);
            Assert.Equal(65535, Stored(t));
        }

        [Fact]
        public void AHalfSetBeforeTheFirstWakeIsLostTheSameWay()
        {
            var (h, hp) = Unwoken(514);
            h.horizontalAlignment = HorizontalAlignmentOptions.Right;
            Assert.Equal(HorizontalAlignmentOptions.Right, h.horizontalAlignment);
            Assert.Equal(514, Stored(h));
            hp.SetActive(true);
            Assert.Equal(TextAlignmentOptions.Center, h.alignment);
            var (v, vp) = Unwoken(257);
            v.verticalAlignment = VerticalAlignmentOptions.Bottom;
            vp.SetActive(true);
            Assert.Equal(TextAlignmentOptions.TopLeft, v.alignment);
        }

        [Fact]
        public void AValueSetAfterTheFirstWakeStays()
        {
            var (t, parent) = Unwoken(257);
            parent.SetActive(true);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            Assert.Equal(65535, Stored(t));
            parent.SetActive(false);
            parent.SetActive(true);
            Frame();
            Assert.Equal(TextAlignmentOptions.MidlineLeft, t.alignment);
        }

        [Fact]
        public void AConvertedTextKeepsAValueSetBeforeItWakes()
        {
            var (t, parent) = Unwoken(65535);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            parent.SetActive(true);
            Assert.Equal(TextAlignmentOptions.MidlineLeft, t.alignment);
        }

        [Fact]
        public void ANewComponentStoresConvertedAndKeepsWhatIsSet()
        {
            TmpTestSupport.LoadGameData();
            var parent = new GameObject("Hidden", typeof(RectTransform));
            parent.SetActive(false);
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            Assert.Equal(65535, Stored(t));
            Assert.Equal(TextAlignmentOptions.TopLeft, t.alignment);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            parent.SetActive(true);
            Assert.Equal(TextAlignmentOptions.MidlineLeft, t.alignment);
            var awake = new GameObject("Awake", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            awake.alignment = TextAlignmentOptions.MidlineLeft;
            Assert.Equal(65535, Stored(awake));
            Assert.Equal(TextAlignmentOptions.MidlineLeft, awake.alignment);
        }

        // A copy of a text not yet woken carries the stored value, and shows it when it wakes,
        // under an active parent too.
        [Fact]
        public void ACopyCarriesTheStoredValue()
        {
            var (t, parent) = Unwoken(257);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            var shown = new GameObject("Shown", typeof(RectTransform));
            var copy = Object.Instantiate(t.gameObject, shown.transform, false).GetComponent<TMP_Text>();
            Assert.Equal(TextAlignmentOptions.TopLeft, copy.alignment);
            Assert.Equal(65535, Stored(copy));
            // Moved under an active parent, it wakes there and loses what was set.
            t.transform.SetParent(shown.transform, false);
            Assert.Equal(TextAlignmentOptions.TopLeft, t.alignment);
            Object.Destroy(parent);
        }
    }
}
