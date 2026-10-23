using System.Collections.Generic;
using System.Linq;
using OniBrutal.Tests.UGui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

namespace OniBrutal.Tests
{
    /// Sub-meshes as raycast targets, and the values the text gives them (textmeshpro.md, "Sub-meshes
    /// and raycasts", measured with UnityProbe's submesh scenario, `SM` lines). A sub-mesh is a
    /// raycast target only after its raycastTarget changes to true; the text copies its own
    /// raycastTarget into every sub-mesh it fills.
    public class TmpSubMeshTests : UGuiTest
    {
        // "中" and "文" come from a global fallback: the text makes one sub-mesh.
        const string Fallback = "AB中文";

        EventSystem m_System;
        Canvas m_Canvas;

        Canvas NewRaycastCanvas()
        {
            TmpTestSupport.LoadGameData();
            m_System ??= new GameObject("EventSystem").AddComponent<EventSystem>();
            m_Canvas = NewCanvas();
            m_Canvas.gameObject.AddComponent<GraphicRaycaster>();
            return m_Canvas;
        }

        /// A 400 x 100 GRAYSTROKE 12 pt text centred in a raycast canvas, laid out and drawn.
        TextMeshProUGUI NewText(bool raycastTarget, string text = Fallback, bool maskable = true)
        {
            var canvas = NewRaycastCanvas();
            var rt = NewRect("Text", canvas.transform, 400f, 100f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = TmpTestSupport.Graystroke;
            t.fontSize = 12f;
            t.raycastTarget = raycastTarget;
            t.maskable = maskable;
            t.text = text;
            Frame();
            Frame();
            return t;
        }

        static TMP_SubMeshUI[] SubMeshes(TMP_Text t) => t.GetComponentsInChildren<TMP_SubMeshUI>(true);

        static TMP_SubMeshUI SubMesh(TMP_Text t) => Assert.Single(SubMeshes(t));

        /// Whether a raycast at the screen's centre (the text's and the sub-mesh's rect centre)
        /// reaches the graphic.
        bool Hit(Graphic g)
        {
            Frame();
            var hits = new List<RaycastResult>();
            m_System.RaycastAll(new PointerEventData(m_System) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, hits);
            return hits.Any(h => h.gameObject == g.gameObject);
        }

        static void Cycle(Behaviour b)
        {
            b.enabled = false;
            b.enabled = true;
        }

        static void Cycle(GameObject go)
        {
            go.SetActive(false);
            go.SetActive(true);
        }

        void Relayout(TMP_Text t)
        {
            t.ForceMeshUpdate();
            Frame();
        }

        // Rows 1 and 2: made by the layout, the sub-mesh has the text's value and is not hit.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ALaidOutSubMeshHasTheTextsValueAndIsNotHit(bool text)
        {
            var t = NewText(text);
            var sub = SubMesh(t);
            Assert.Equal(text, sub.raycastTarget);
            Assert.True(sub.mesh.vertexCount > 0);
            Assert.True(sub.depth >= 0);
            Assert.False(Hit(sub));
            Frame();
            Assert.False(Hit(sub));
            Assert.Equal(text, Hit(t));
        }

        // Row 3: enabling and activity cycles of the sub-mesh or the text do not list it.
        [Theory]
        [InlineData("sub enabled")]
        [InlineData("sub active")]
        [InlineData("text enabled")]
        [InlineData("text active")]
        [InlineData("text enabled, a frame apart")]
        [InlineData("reparent")]
        public void CyclesDoNotListIt(string cycle)
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            switch (cycle)
            {
                case "sub enabled": Cycle(sub); break;
                case "sub active": Cycle(sub.gameObject); break;
                case "text enabled": Cycle(t); break;
                case "text active": Cycle(t.gameObject); break;
                case "text enabled, a frame apart":
                    t.enabled = false;
                    Frame();
                    t.enabled = true;
                    break;
                case "reparent":
                    var other = NewRect("Other", m_Canvas.transform, 400f, 100f);
                    t.transform.SetParent(other, false);
                    break;
            }
            Frame();
            Assert.True(sub.raycastTarget);
            Assert.True(sub.isActiveAndEnabled);
            Assert.False(Hit(sub));
        }

        // Row 4, and setting the value it already has.
        [Fact]
        public void AChangeToTrueListsIt()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            sub.raycastTarget = true;
            Assert.False(Hit(sub));
            sub.raycastTarget = false;
            Assert.False(Hit(sub));
            sub.raycastTarget = true;
            Assert.True(Hit(sub));
            Frame();
            Assert.True(Hit(sub));
            // Ahead of its text.
            var hits = new List<RaycastResult>();
            m_System.RaycastAll(new PointerEventData(m_System) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, hits);
            Assert.Same(sub.gameObject, hits[0].gameObject);
            sub.raycastTarget = false;
            Assert.False(Hit(sub));
        }

        // Rows 5 to 7: once listed, the sub-mesh's enabled cycle and the text's object's activity
        // cycle drop it. The shipped package keeps it listed through the text component's enabled
        // cycle, as it does not disable the listing with the sub-mesh; here the sub-mesh is dropped
        // on every disable (the section's "to implement").
        [Theory]
        [InlineData("sub enabled", false)]
        [InlineData("text active", false)]
        [InlineData("text enabled", false)]
        public void AListedSubMeshIsDroppedWhenDisabled(string cycle, bool hit)
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            sub.raycastTarget = false;
            sub.raycastTarget = true;
            Assert.True(Hit(sub));
            switch (cycle)
            {
                case "sub enabled": Cycle(sub); break;
                case "text active": Cycle(t.gameObject); break;
                case "text enabled": Cycle(t); break;
            }
            Frame();
            Assert.True(sub.raycastTarget);
            Assert.Equal(hit, Hit(sub));
        }

        // Row 8: a relayout gives the sub-mesh the text's true again, listing it.
        [Fact]
        public void ARelayoutCopiesTheTextsTrue()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            sub.raycastTarget = false;
            Relayout(t);
            Assert.Same(sub, SubMesh(t));
            Assert.True(sub.raycastTarget);
            Assert.True(Hit(sub));
        }

        // Rows 9 and 10: the text set false reaches the sub-mesh at the next generation only.
        [Fact]
        public void TheTextsFalseReachesTheSubMeshAtTheNextGeneration()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            t.raycastTarget = false;
            Frame();
            Assert.True(sub.raycastTarget);
            Assert.False(Hit(sub));
            Relayout(t);
            Assert.False(sub.raycastTarget);
            Assert.False(Hit(sub));
        }

        // Rows 11 and 12: glyphs taken away leave the sub-mesh's value; brought back, the same
        // sub-mesh is filled and gets the text's value.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FallbackGlyphsRemovedAndReturned(bool subSetFalse)
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            if (subSetFalse) sub.raycastTarget = false;
            t.text = "AB";
            Frame();
            Assert.Equal(!subSetFalse, sub.raycastTarget);
            t.text = Fallback;
            Frame();
            Assert.Same(sub, SubMesh(t));
            Assert.True(sub.raycastTarget);
            Assert.Equal(subSetFalse, Hit(sub));
        }

        // Row 13: a sub-mesh added by hand, before and after parenting.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ASubMeshAddedByHandIsNotHit(bool afterParenting)
        {
            var t = NewText(true, "AB");
            var go = new GameObject("Hand", typeof(RectTransform));
            if (afterParenting) go.transform.SetParent(t.transform, false);
            var sub = go.AddComponent<TMP_SubMeshUI>();
            if (!afterParenting) go.transform.SetParent(t.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            Frame();
            Assert.True(sub.raycastTarget);
            Assert.False(Hit(sub));
            sub.raycastTarget = false;
            sub.raycastTarget = true;
            Assert.True(Hit(sub));
        }

        // Row 14: an Image is listed on enable, through every cycle.
        [Fact]
        public void ImagesAreListedOnEnable()
        {
            NewRaycastCanvas();
            var image = NewRect("Image", m_Canvas.transform, 400f, 100f, typeof(Image)).GetComponent<Image>();
            Frame();
            Assert.True(Hit(image));
            Cycle(image);
            Assert.True(Hit(image));
            Cycle(image.gameObject);
            Assert.True(Hit(image));
            image.raycastTarget = false;
            Assert.False(Hit(image));
            image.raycastTarget = true;
            Assert.True(Hit(image));
        }

        /// A text with a sub-mesh saved with it (raycastTarget true, empty, no LayoutElement),
        /// as in the game's prefabs, instantiated into the canvas.
        TextMeshProUGUI SavedText(bool textRaycastTarget)
        {
            var canvas = NewRaycastCanvas();
            var template = new GameObject("Label", typeof(RectTransform));
            template.SetActive(false);
            ((RectTransform)template.transform).sizeDelta = new Vector2(400f, 100f);
            var t = template.AddComponent<TextMeshProUGUI>();
            t.font = TmpTestSupport.Graystroke;
            t.fontSize = 12f;
            t.raycastTarget = textRaycastTarget;
            t.text = "AB";
            var saved = new GameObject("TMP SubMeshUI [saved]", typeof(RectTransform));
            saved.transform.SetParent(template.transform, false);
            var rt = (RectTransform)saved.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            saved.AddComponent<TMP_SubMeshUI>().raycastTarget = true;
            var copy = Object.Instantiate(template, canvas.transform, false);
            copy.SetActive(true);
            Frame();
            Frame();
            return copy.GetComponent<TextMeshProUGUI>();
        }

        // Rows 15 to 17: saved sub-meshes keep their saved true and are not hit, until their own
        // false-and-true cycle.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SavedSubMeshesAreNotHitUntilTheirOwnCycle(bool text)
        {
            var t = SavedText(text);
            var sub = SubMesh(t);
            Assert.Null(sub.GetComponent<LayoutElement>());
            Assert.True(sub.raycastTarget);
            Assert.False(Hit(sub));
            sub.raycastTarget = false;
            sub.raycastTarget = true;
            Assert.True(Hit(sub));
        }

        // A saved sub-mesh is adopted: a fill gives it the text's value.
        [Fact]
        public void ASavedSubMeshIsFilledByItsText()
        {
            var t = SavedText(false);
            var sub = SubMesh(t);
            t.text = Fallback;
            Frame();
            Assert.Same(sub, SubMesh(t));
            Assert.True(sub.mesh.vertexCount > 0);
            Assert.False(sub.raycastTarget);
        }

        // Row 18, and the rect.
        [Fact]
        public void ThePivotFollowsTheTexts()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            var rt = sub.rectTransform;
            Assert.Equal(Vector2.zero, rt.anchorMin);
            Assert.Equal(Vector2.one, rt.anchorMax);
            Assert.Equal(Vector2.zero, rt.sizeDelta);
            Assert.Equal(new Vector2(0.5f, 0.5f), rt.pivot);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            Assert.Equal(new Vector2(0f, 1f), rt.pivot);
            Assert.Equal(Vector3.zero, rt.localPosition);
            Assert.Equal(t.rectTransform.rect, rt.rect);
            t.rectTransform.sizeDelta = new Vector2(300f, 60f);
            Assert.Equal(t.rectTransform.rect, rt.rect);
            Assert.Equal(Vector3.zero, rt.localPosition);
        }

        // Row 19: maskable is copied at creation, and not later.
        [Fact]
        public void MaskableIsCopiedAtCreation()
        {
            var t = NewText(true, maskable: false);
            var sub = SubMesh(t);
            Assert.False(sub.maskable);
            t.maskable = true;
            Frame();
            Assert.False(sub.maskable);
            Assert.True(SubMesh(NewText(true)).maskable);
        }

        // Row 20: the sub-meshes are disabled and enabled with the text.
        [Fact]
        public void SubMeshesAreDisabledWithTheText()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            t.enabled = false;
            Assert.False(sub.enabled);
            t.enabled = true;
            Assert.True(sub.enabled);
            t.gameObject.SetActive(false);
            Assert.False(sub.enabled);
            Frame();
            Assert.False(sub.enabled);
            t.gameObject.SetActive(true);
            Assert.True(sub.enabled);
            Frame();
            Assert.True(sub.mesh.vertexCount > 0);
        }

        // Its object and components.
        [Fact]
        public void ARunTimeSubMeshIsDontSaveWithItsComponentsInOrder()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            Assert.Equal("New Game Object", sub.gameObject.name);
            Assert.Equal(HideFlags.DontSave, sub.gameObject.hideFlags);
            Assert.Equal(t.gameObject.layer, sub.gameObject.layer);
            Assert.Equal(Color.white, sub.color);
            var components = sub.GetComponents<Component>().Select(c => c.GetType()).ToArray();
            Assert.Equal([typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer), typeof(TMP_SubMeshUI)], components);
            Assert.True(sub.GetComponent<LayoutElement>().ignoreLayout);
        }

        // Unused, the sub-mesh stays active and enabled and keeps its mesh, but draws nothing.
        [Theory]
        [InlineData("AB")]
        [InlineData("")]
        public void AnUnusedSubMeshKeepsItsMesh(string text)
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            int count = sub.mesh.vertexCount;
            var bounds = sub.mesh.bounds;
            t.text = text;
            Frame();
            Assert.True(sub.isActiveAndEnabled);
            Assert.Equal(count, sub.mesh.vertexCount);
            Assert.Equal(bounds, sub.mesh.bounds);
            Assert.True(GeometryOf(sub) == null || GeometryOf(sub).Positions.Length == 0);
        }

        // A clone leaves its copy of the sub-mesh empty and makes its own.
        [Fact]
        public void ACloneMakesASecondSubMesh()
        {
            var t = NewText(true);
            var copy = Object.Instantiate(t.gameObject, m_Canvas.transform, false).GetComponent<TextMeshProUGUI>();
            Frame();
            Frame();
            var subs = SubMeshes(copy);
            Assert.Equal(2, subs.Length);
            Assert.Single(subs, s => s.mesh.vertexCount == 0);
            Assert.Single(subs, s => s.mesh.vertexCount > 0);
        }

        // Unlike the shipped package's, a listed sub-mesh destroyed with its text leaves the list.
        [Fact]
        public void ADestroyedSubMeshLeavesTheList()
        {
            var t = NewText(true);
            var sub = SubMesh(t);
            sub.raycastTarget = false;
            sub.raycastTarget = true;
            Assert.True(Hit(sub));
            Object.DestroyImmediate(t.gameObject);
            Assert.DoesNotContain(GraphicRegistry.GetRaycastableGraphicsForCanvas(m_Canvas), g => g == null || g is TMP_SubMeshUI);
            Frame();
            var hits = new List<RaycastResult>();
            m_System.RaycastAll(new PointerEventData(m_System) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) }, hits);
            Assert.Empty(hits);
        }
    }
}
