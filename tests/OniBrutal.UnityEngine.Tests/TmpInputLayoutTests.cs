#pragma warning disable CS0618 // enableKerning, as the game's SetTextStyleSetting sets it
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests
{
    // TMP_InputField's layout element values (docs/cleanroom/textmeshpro.md, "The input field's
    // layout values"): the text component's preferred size plus the viewport's own offsets,
    // never the field's or the text's rect. Fields are built as the probe builds them: a field
    // with an Image, its child "Text Area" (RectMask2D) as the viewport and its grandchild
    // "Text" (NotoSans-Regular 14 pt, no wrapping) as the text component. "1915" is
    // 32.09 x 19.04.
    public class TmpInputLayoutTests : EngineTest
    {
        sealed class Cfg
        {
            public Vector2 Size = new Vector2(64, 24);
            public Vector2 VpOffsetMin, VpOffsetMax;
            public Vector2 VpAnchorMin = Vector2.zero, VpAnchorMax = Vector2.one;
            public Vector2? VpSize;
            public Vector4 MaskPadding;
            public bool NoViewport, NoTextComponent;
            public Vector2 TextAnchorMin = Vector2.zero, TextAnchorMax = Vector2.one;
            public Vector2 TextSizeDelta, TextPos;
            public Vector4 TextMargin;
            public string Text = "1915";
            public string Placeholder;
            public TMP_InputField.LineType Line = TMP_InputField.LineType.SingleLine;
        }

        static Cfg Geyser(Vector2 size) => new Cfg
        {
            Size = size,
            TextAnchorMin = new Vector2(0, 1),
            TextAnchorMax = new Vector2(1, 1),
            TextSizeDelta = new Vector2(-3, -1),
            TextPos = new Vector2(1.5f, -0.5f),
        };

        static RectTransform Canvas()
        {
            TmpTestSupport.LoadGameData();
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>("NotoSans-Regular");
            t.fontSize = size;
            t.enableKerning = true;
            t.extraPadding = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        static TMP_InputField Make(Transform parent, Cfg c)
        {
            var go = new GameObject("Field", typeof(RectTransform));
            go.SetActive(false);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = c.Size;
            go.AddComponent<Image>();
            var f = go.AddComponent<TMP_InputField>();
            var vp = new GameObject("Text Area", typeof(RectTransform));
            var vrt = (RectTransform)vp.transform;
            vrt.SetParent(rt, false);
            vrt.anchorMin = c.VpAnchorMin;
            vrt.anchorMax = c.VpAnchorMax;
            vrt.offsetMin = c.VpOffsetMin;
            vrt.offsetMax = c.VpOffsetMax;
            if (c.VpSize.HasValue) { vrt.sizeDelta = c.VpSize.Value; vrt.anchoredPosition = c.VpOffsetMin; }
            vp.AddComponent<RectMask2D>().padding = c.MaskPadding;
            if (!c.NoViewport) f.textViewport = vrt;
            if (!c.NoTextComponent)
            {
                var t = MakeText(vrt, "Text", 14);
                var trt = t.rectTransform;
                trt.anchorMin = c.TextAnchorMin;
                trt.anchorMax = c.TextAnchorMax;
                trt.sizeDelta = c.TextSizeDelta;
                trt.anchoredPosition = c.TextPos;
                t.margin = c.TextMargin;
                f.textComponent = t;
            }
            if (c.Placeholder != null)
            {
                var p = MakeText(vrt, "Placeholder", 20);
                p.rectTransform.anchorMin = Vector2.zero;
                p.rectTransform.anchorMax = Vector2.one;
                p.rectTransform.sizeDelta = Vector2.zero;
                p.text = c.Placeholder;
                f.placeholder = p;
            }
            f.lineType = c.Line;
            f.text = c.Text;
            go.SetActive(true);
            return f;
        }

        static void AssertPreferred(float w, float h, TMP_InputField f)
        {
            Assert.Equal(w, f.preferredWidth, 2);
            Assert.Equal(h, f.preferredHeight, 2);
        }

        static void AssertFixed(TMP_InputField f)
        {
            f.CalculateLayoutInputHorizontal();
            f.CalculateLayoutInputVertical();
            Assert.Equal(0f, f.minWidth);
            Assert.Equal(0f, f.minHeight);
            Assert.Equal(-1f, f.flexibleWidth);
            Assert.Equal(-1f, f.flexibleHeight);
            Assert.Equal(1, f.layoutPriority);
        }

        // Each value is the same at once, after a forced rebuild and a frame later.
        void AssertEveryRead(float w, float h, TMP_InputField f)
        {
            AssertPreferred(w, h, f);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)f.transform);
            AssertPreferred(w, h, f);
            Frame();
            AssertPreferred(w, h, f);
            AssertFixed(f);
        }

        public static TheoryData<string, float, float> Table() => new()
        {
            { "geyser 64x24", 32.09f, 19.04f },
            { "geyser 200x100", 32.09f, 19.04f },
            { "stretch", 32.09f, 19.04f },
            { "stretch 200x100", 32.09f, 19.04f },
            { "stretch 10x5", 32.09f, 19.04f },
            { "text inset", 32.09f, 19.04f },
            { "text inset 10x5", 32.09f, 19.04f },
            { "text fixed 50x10", 32.09f, 19.04f },
            { "vp offsets", 44.09f, 26.04f },
            { "vp offsets text inset", 44.09f, 26.04f },
            { "vp part with offsets", 37.09f, 24.04f },
            { "vp part", 32.09f, 19.04f },
            { "vp fixed 40x10", -7.91f, 9.04f },
            { "vp fixed 40x10 200x100", -7.91f, 9.04f },
            { "vp fixed 40x10 moved", -7.91f, 9.04f },
            { "vp larger", 20.09f, 12.04f },
            { "no vp text inset", 32.09f, 19.04f },
            { "mask padding", 32.09f, 19.04f },
            { "margins", 42.09f, 24.04f },
            { "empty geyser", 0.01f, 19.04f },
            { "empty placeholder", 0.01f, 19.04f },
            { "empty no placeholder", 0.01f, 19.04f },
            { "multiline", 124.8f, 57.17f },
            { "no text component", 0f, 0f },
            { "no text component placeholder", 0f, 0f },
        };

        static Cfg Case(string name)
        {
            var c5 = new Vector2(0.5f, 0.5f);
            switch (name)
            {
                case "geyser 64x24": return Geyser(new Vector2(64, 24));
                case "geyser 200x100": return Geyser(new Vector2(200, 100));
                case "stretch": return new Cfg();
                case "stretch 200x100": return new Cfg { Size = new Vector2(200, 100) };
                case "stretch 10x5": return new Cfg { Size = new Vector2(10, 5) };
                case "text inset": return new Cfg { TextSizeDelta = new Vector2(-20, -12) };
                case "text inset 10x5": return new Cfg { Size = new Vector2(10, 5), TextSizeDelta = new Vector2(-20, -12) };
                case "text fixed 50x10": return new Cfg { TextAnchorMin = c5, TextAnchorMax = c5, TextSizeDelta = new Vector2(50, 10) };
                case "vp offsets": return new Cfg { VpOffsetMin = new Vector2(5, 3), VpOffsetMax = new Vector2(-7, -4) };
                case "vp offsets text inset": return new Cfg { VpOffsetMin = new Vector2(5, 3), VpOffsetMax = new Vector2(-7, -4), TextSizeDelta = new Vector2(-20, -12) };
                case "vp part with offsets": return new Cfg { VpAnchorMin = new Vector2(0.25f, 0.5f), VpAnchorMax = new Vector2(0.75f, 1), VpOffsetMin = new Vector2(2, 1), VpOffsetMax = new Vector2(-3, -4) };
                case "vp part": return new Cfg { VpAnchorMin = new Vector2(0.25f, 0), VpAnchorMax = new Vector2(0.75f, 1) };
                case "vp fixed 40x10": return new Cfg { VpAnchorMin = c5, VpAnchorMax = c5, VpSize = new Vector2(40, 10) };
                case "vp fixed 40x10 200x100": return new Cfg { Size = new Vector2(200, 100), VpAnchorMin = c5, VpAnchorMax = c5, VpSize = new Vector2(40, 10) };
                case "vp fixed 40x10 moved": return new Cfg { VpAnchorMin = c5, VpAnchorMax = c5, VpSize = new Vector2(40, 10), VpOffsetMin = new Vector2(6, 2) };
                case "vp larger": return new Cfg { VpOffsetMin = new Vector2(-5, -3), VpOffsetMax = new Vector2(7, 4) };
                case "no vp text inset": return new Cfg { NoViewport = true, VpOffsetMin = new Vector2(5, 3), VpOffsetMax = new Vector2(-7, -4) };
                case "mask padding": return new Cfg { MaskPadding = new Vector4(4, 3, 2, 1) };
                case "margins": return new Cfg { TextMargin = new Vector4(4, 2, 6, 3) };
                case "empty geyser": { var c = Geyser(new Vector2(64, 24)); c.Text = ""; return c; }
                case "empty placeholder": return new Cfg { Text = "", Placeholder = "a much longer placeholder text" };
                case "empty no placeholder": return new Cfg { Text = "" };
                case "multiline": return new Cfg { Line = TMP_InputField.LineType.MultiLineNewline, Text = "abc def ghi jkl mno" };
                case "no text component": return new Cfg { NoTextComponent = true };
                case "no text component placeholder": return new Cfg { NoTextComponent = true, Placeholder = "Enter text..." };
            }
            throw new System.ArgumentException(name);
        }

        [Theory]
        [MemberData(nameof(Table))]
        public void ThePreferredSizeIsTheTextsPlusTheViewportsOffsets(string name, float w, float h)
        {
            var f = Make(Canvas(), Case(name));
            AssertEveryRead(w, h, f);
        }

        [Fact]
        public void TheRuleHoldsForTheTextComponentsOwnPreferredSize()
        {
            var c = new Cfg { VpOffsetMin = new Vector2(5, 3), VpOffsetMax = new Vector2(-7, -4), Text = "abc def ghi jkl", Size = new Vector2(10, 5) };
            var f = Make(Canvas(), c);
            var t = f.textComponent;
            var vp = f.textViewport;
            Assert.Equal(t.preferredWidth + vp.offsetMin.x - vp.offsetMax.x, f.preferredWidth, 3);
            Assert.Equal(t.preferredHeight + vp.offsetMin.y - vp.offsetMax.y, f.preferredHeight, 3);
        }

        [Fact]
        public void ANegativeValueIsNotClampedButLayoutUtilityRaisesItToTheMinimum()
        {
            var c5 = new Vector2(0.5f, 0.5f);
            var f = Make(Canvas(), new Cfg { VpAnchorMin = c5, VpAnchorMax = c5, VpSize = new Vector2(40, 10) });
            Assert.Equal(-7.91f, f.preferredWidth, 2);
            Assert.Equal(0f, LayoutUtility.GetPreferredWidth((RectTransform)f.transform));
        }

        [Fact]
        public void TheMultiLineTextIsWrappedAsItsTextComponentReports()
        {
            var f = Make(Canvas(), new Cfg { Line = TMP_InputField.LineType.MultiLineNewline, Text = "abc def ghi jkl mno" });
            Assert.Equal(f.textComponent.preferredWidth, f.preferredWidth, 3);
            Assert.Equal(f.textComponent.preferredHeight, f.preferredHeight, 3);
        }

        // Geyser Control's slider row and the other layouts the probe ran: every pass reports
        // the same preferred size as the first.
        RectTransform Row(string layout, Cfg c, out TMP_InputField f)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            var rrt = (RectTransform)row.transform;
            rrt.SetParent(Canvas(), false);
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(256, 24);
            if (layout == "csf")
            {
                f = Make(rrt, c);
                var csf = f.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                return rrt;
            }
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = layout != "hlg-noctrl";
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = layout == "hlg-ctrl-expand";
            hlg.childAlignment = TextAnchor.MiddleLeft;
            if (layout == "geyserrow")
            {
                var lab = MakeText(rrt, "TargetLabel", 14);
                lab.text = "Mass Per Cycle";
                var lle = lab.gameObject.AddComponent<LayoutElement>();
                lle.minWidth = 96; lle.minHeight = 15; lle.preferredWidth = 96; lle.flexibleWidth = 1;
            }
            f = Make(rrt, c);
            if (layout == "geyserrow")
            {
                var le = f.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 64; le.minHeight = 24; le.preferredWidth = 64; le.flexibleWidth = 0;
                f.characterLimit = 4;
                f.contentType = TMP_InputField.ContentType.DecimalNumber;
                var sp = new GameObject("Spacer", typeof(RectTransform));
                sp.transform.SetParent(rrt, false);
                sp.AddComponent<LayoutElement>().minWidth = 8;
                var units = MakeText(rrt, "UnitsLabel", 14);
                units.text = "kg";
                var ule = units.gameObject.AddComponent<LayoutElement>();
                ule.minWidth = 38; ule.minHeight = 16; ule.preferredWidth = 38; ule.flexibleWidth = 0;
            }
            if (layout == "csf-in-hlg")
                rrt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rrt;
        }

        [Theory]
        [InlineData("geyserrow")]
        [InlineData("hlg-ctrl")]
        [InlineData("hlg-noctrl")]
        [InlineData("hlg-ctrl-expand")]
        [InlineData("csf")]
        [InlineData("csf-in-hlg")]
        public void LayingTheFieldOutNeverChangesItsPreferredSize(string layout)
        {
            var rrt = Row(layout, Geyser(new Vector2(64, 24)), out var f);
            AssertPreferred(32.09f, 19.04f, f);
            for (int pass = 1; pass <= 4; pass++)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
                AssertPreferred(32.09f, 19.04f, f);
            }
            for (int frame = 1; frame <= 3; frame++)
            {
                Frame();
                AssertPreferred(32.09f, 19.04f, f);
            }
            AssertFixed(f);
        }

        [Fact]
        public void GeyserControlsRowIs24High()
        {
            var rrt = Row("geyserrow", Geyser(new Vector2(64, 24)), out var f);
            for (int pass = 1; pass <= 4; pass++)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
                Assert.Equal(24f, rrt.GetComponent<HorizontalLayoutGroup>().preferredHeight, 2);
            }
            var frt = (RectTransform)f.transform;
            Assert.Equal(64f, LayoutUtility.GetPreferredWidth(frt), 2);
            Assert.Equal(24f, LayoutUtility.GetPreferredHeight(frt), 2);
            Assert.Equal(64f, frt.rect.width, 2);
            Assert.Equal(24f, frt.rect.height, 2);
        }

        [Fact]
        public void TheFittedRowKeepsTheTextsHeight()
        {
            var rrt = Row("csf-in-hlg", Geyser(new Vector2(64, 24)), out var f);
            for (int pass = 1; pass <= 4; pass++)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
                Assert.Equal(19.04f, rrt.GetComponent<HorizontalLayoutGroup>().preferredHeight, 2);
                Assert.Equal(19.04f, ((RectTransform)f.transform).rect.height, 2);
            }
        }

        [Fact]
        public void HeightControlOffKeepsTheFieldsHeight()
        {
            var rrt = Row("hlg-noctrl", Geyser(new Vector2(64, 24)), out var f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
            var frt = (RectTransform)f.transform;
            Assert.Equal(32.09f, frt.rect.width, 2);
            Assert.Equal(24f, frt.rect.height, 2);
        }

        [Fact]
        public void AContentSizeFitterOnTheFieldGivesItTheTextsSize()
        {
            Row("csf", Geyser(new Vector2(64, 24)), out var f);
            Frame();
            var frt = (RectTransform)f.transform;
            Assert.Equal(32.09f, frt.rect.width, 2);
            Assert.Equal(19.04f, frt.rect.height, 2);
        }
    }
}
