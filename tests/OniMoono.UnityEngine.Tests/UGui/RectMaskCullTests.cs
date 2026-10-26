using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace OniMoono.Tests.UGui
{
    /// Culling under a RectMask2D (ugui.md and textmeshpro.md, "RectMask2D culling" and "Culling
    /// under a RectMask2D", measured with UnityProbe's layoutcull scenario, `LC cull` lines). A
    /// 600 x 200 mask at the canvas's centre; each subject a 200 x 30 rect (or 100 high) whose
    /// centre is placed at the mask's bottom (or top) edge plus d.
    public class RectMaskCullTests : UGuiTest
    {
        RectTransform NewMask(Vector4 padding = default, Vector2Int softness = default)
        {
            var canvas = NewCanvas();
            var m = NewRect("Mask", canvas.transform, 600, 200);
            var rm = m.gameObject.AddComponent<RectMask2D>();
            rm.padding = padding;
            rm.softness = softness;
            return m;
        }

        static void Place(RectTransform rt, float edge, float d)
        {
            var h = rt.sizeDelta.y;
            rt.anchoredPosition = new Vector2(-100 + rt.pivot.x * 200, edge + d + (rt.pivot.y - 0.5f) * h);
        }

        static Image NewImage(Transform parent, Vector2 pivot, float edge, float d)
        {
            var rt = NewRect("Image", parent, 200, 30);
            rt.pivot = pivot;
            var img = rt.gameObject.AddComponent<Image>();
            Place(rt, edge, d);
            return img;
        }

        static TextMeshProUGUI NewText(Transform parent, string content, TextAlignmentOptions align, float edge, float d, float height = 30f, Vector2? pivot = null)
        {
            var rt = NewRect("Text", parent, 200, height);
            rt.pivot = pivot ?? new Vector2(0f, 0.5f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableKerning = true;
            t.extraPadding = true;
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>("NotoSans-Regular");
            t.fontSize = 14;
            t.alignment = align;
            t.text = content;
            Place(rt, edge, d);
            return t;
        }

        static void Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) Frame();
        }

        // Exclusive edges: touching is not overlapping. An Image culls with its rect, whatever
        // its pivot.
        [Theory]
        [InlineData(0f, 0.5f)]
        [InlineData(0.5f, 0.5f)]
        [InlineData(0.5f, 1f)]
        public void AnImageTouchingTheClipIsCulled(float px, float py)
        {
            var mask = NewMask();
            var pivot = new Vector2(px, py);
            var touchingBottom = NewImage(mask, pivot, -100, -15);
            var inBottom = NewImage(mask, pivot, -100, -14.5f);
            var touchingTop = NewImage(mask, pivot, 100, 15);
            var inTop = NewImage(mask, pivot, 100, 14.5f);
            Frames();
            Assert.True(touchingBottom.canvasRenderer.cull);
            Assert.False(inBottom.canvasRenderer.cull);
            Assert.True(touchingTop.canvasRenderer.cull);
            Assert.False(inTop.canvasRenderer.cull);
        }

        [Fact]
        public void PaddingShrinksTheClipAndSoftnessDoesNot()
        {
            var padded = NewMask(new Vector4(0, 10, 0, 0));
            var culled = NewImage(padded, new Vector2(0f, 0.5f), -100, -5);
            var kept = NewImage(padded, new Vector2(0f, 0.5f), -100, -4.5f);
            var soft = NewMask(softness: new Vector2Int(20, 20));
            var softCulled = NewImage(soft, new Vector2(0f, 0.5f), -100, -15);
            var softKept = NewImage(soft, new Vector2(0f, 0.5f), -100, -14.5f);
            Frames();
            Assert.True(culled.canvasRenderer.cull);
            Assert.False(kept.canvasRenderer.cull);
            Assert.True(softCulled.canvasRenderer.cull);
            Assert.False(softKept.canvasRenderer.cull);
        }

        // A size change culls again, in that frame (time img size in / size out).
        [Fact]
        public void AnImageResizedAcrossTheEdgeIsCulledAgainInThatFrame()
        {
            var mask = NewMask();
            var grown = NewImage(mask, new Vector2(0f, 0.5f), -100, -40);
            var shrunk = NewImage(mask, new Vector2(0f, 0.5f), -100, -14);
            Frames(2);
            Assert.True(grown.canvasRenderer.cull);
            Assert.False(shrunk.canvasRenderer.cull);
            grown.rectTransform.sizeDelta += new Vector2(0, 100);
            shrunk.rectTransform.sizeDelta -= new Vector2(0, 20);
            Frame();
            Assert.False(grown.canvasRenderer.cull);
            Assert.True(shrunk.canvasRenderer.cull);
        }

        [Fact]
        public void AMovedOrScaledImageIsCulledAgainInThatFrame()
        {
            var mask = NewMask();
            var moved = NewImage(mask, new Vector2(0f, 0.5f), -100, 0);
            var scaled = NewImage(mask, new Vector2(0f, 0.5f), -100, -10);
            Frames(2);
            Assert.False(moved.canvasRenderer.cull);
            Assert.False(scaled.canvasRenderer.cull);
            moved.rectTransform.localPosition += new Vector3(0, -300, 0);
            scaled.rectTransform.localScale = new Vector3(1, 0.1f, 1);
            Frame();
            Assert.True(moved.canvasRenderer.cull);
            Assert.True(scaled.canvasRenderer.cull);
        }

        // A text culls with its mesh's bounds: "Care Packages" (left, pivot (0, 0.5)) has a box
        // from -12.29 to 8.71 about its pivot, so it is culled from d = -9 at the bottom edge and
        // from 12.5 at the top, where its rect would be from -15 and 15.
        [Fact]
        public void ATextCullsWithItsMeshBounds()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask();
            var culled = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -9);
            var kept = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -8.5f);
            var topCulled = NewText(mask, "Care Packages", TextAlignmentOptions.Left, 100, 12.5f);
            var topKept = NewText(mask, "Care Packages", TextAlignmentOptions.Left, 100, 12f);
            var aaaCulled = NewText(mask, "AAA", TextAlignmentOptions.Left, -100, -8.5f);
            var aaaKept = NewText(mask, "AAA", TextAlignmentOptions.Left, -100, -8f);
            Frames();
            Assert.True(culled.canvasRenderer.cull);
            Assert.False(kept.canvasRenderer.cull);
            Assert.True(topCulled.canvasRenderer.cull);
            Assert.False(topKept.canvasRenderer.cull);
            Assert.True(aaaCulled.canvasRenderer.cull);
            Assert.False(aaaKept.canvasRenderer.cull);
        }

        // The box includes the pivot (unused vertices sit at the local origin): an empty text is
        // a point at its pivot, and a text aligned to the top of a 100-high rect has a box from 0
        // up, so at the top edge it is culled from d = 0.
        [Fact]
        public void ATextsBoxIncludesItsPivot()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask();
            var emptyCulled = NewText(mask, "", TextAlignmentOptions.Left, -100, 0);
            var emptyKept = NewText(mask, "", TextAlignmentOptions.Left, -100, 0.25f);
            var spaceCulled = NewText(mask, " ", TextAlignmentOptions.Left, 100, 0);
            var spaceKept = NewText(mask, " ", TextAlignmentOptions.Left, 100, -0.25f);
            var tallCulled = NewText(mask, "Care Packages", TextAlignmentOptions.TopLeft, 100, 0, 100);
            var tallKept = NewText(mask, "Care Packages", TextAlignmentOptions.TopLeft, 100, -0.5f, 100);
            Frames();
            Assert.True(emptyCulled.canvasRenderer.cull);
            Assert.False(emptyKept.canvasRenderer.cull);
            Assert.True(spaceCulled.canvasRenderer.cull);
            Assert.False(spaceKept.canvasRenderer.cull);
            Assert.True(tallCulled.canvasRenderer.cull);
            Assert.False(tallKept.canvasRenderer.cull);
        }

        [Fact]
        public void ATextsMeshBoundsFollowPaddingToo()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask(new Vector4(0, 10, 0, 0));
            var culled = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, 1);
            var kept = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, 1.5f);
            Frames();
            Assert.True(culled.canvasRenderer.cull);
            Assert.False(kept.canvasRenderer.cull);
        }

        // TMP_Text.bounds is the mesh's bounds, the pivot included (the probe's bounds and mesh
        // boxes are equal in the original).
        [Fact]
        public void BoundsAreTheMeshsBounds()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask();
            var t = NewText(mask, "Care Packages", TextAlignmentOptions.TopLeft, 0, 0, 100);
            Frames();
            Assert.Equal(t.mesh.bounds, t.bounds);
            Assert.True(t.bounds.min.y <= 0f);
            Assert.True(t.textBounds.min.y > 0f);
        }

        // When the cull is evaluated (cull time ...): after a text change, with the new mesh in
        // that frame's update; after an alignment change, one frame later; a culled text whose
        // mesh stays put stays culled.
        [Fact]
        public void ATextChangeCullsWithTheNewMeshInThatFrame()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask();
            var t = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -30, 100);
            var nothing = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -30, 100);
            var bigger = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -30, 100);
            Frames(2);
            Assert.True(t.canvasRenderer.cull);
            Assert.True(nothing.canvasRenderer.cull);
            Assert.True(bigger.canvasRenderer.cull);
            t.text = "Care Packages\nsecond\nthird\nfourth";
            bigger.fontSize = 60;
            Frame();
            Assert.False(t.canvasRenderer.cull);
            Assert.False(bigger.canvasRenderer.cull);
            Frames();
            Assert.True(nothing.canvasRenderer.cull);
        }

        [Fact]
        public void AnAlignmentChangeCullsOneFrameLater()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask();
            var t = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -30, 100);
            Frames(2);
            Assert.True(t.canvasRenderer.cull);
            t.alignment = TextAlignmentOptions.TopLeft;
            Frame();
            Assert.True(t.canvasRenderer.cull);
            Frame();
            Assert.False(t.canvasRenderer.cull);
        }

        // A text made in place is culled with its first mesh, from its first canvas update: its
        // pivot 0.5 below the clip, its glyphs reaching into it.
        [Fact]
        public void ANewTextIsCulledWithItsFirstMesh()
        {
            TmpTestSupport.LoadGameData();
            var mask = NewMask();
            var kept = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -0.5f);
            var culled = NewText(mask, "Care Packages", TextAlignmentOptions.Left, -100, -14);
            Frame();
            Assert.False(kept.canvasRenderer.cull);
            Assert.True(culled.canvasRenderer.cull);
        }
    }
}
