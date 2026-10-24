using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    /// Children of layout groups activated or deactivated (UnityProbe's childactive scenario,
    /// `CA` lines, measured in the original): SetActive raises RectTransform.reapplyDrivenProperties
    /// for every RectTransform whose activeInHierarchy changed, the child before its parent and
    /// before OnEnable/OnDisable, and sends no OnRectTransformDimensionsChange. uGUI's
    /// LayoutRebuilder marks the layout around each, so a group re-lays out in that frame even
    /// when the child has no Graphic and no layout element on its root (PlanScreen's building
    /// buttons, filtered by the build menu's search).
    public class ActivatedLayoutChildTests : UGuiTest, System.IDisposable
    {
        sealed class Recorder : UIBehaviour
        {
            public static List<string> Log;
            protected override void OnEnable() => Log?.Add(name + " OnEnable");
            protected override void OnDisable() => Log?.Add(name + " OnDisable");
            protected override void OnRectTransformDimensionsChange() => Log?.Add(name + " OnRectTransformDimensionsChange");
        }

        readonly List<string> log = new();
        readonly RectTransform.ReapplyDrivenProperties listener;

        public ActivatedLayoutChildTests()
        {
            Recorder.Log = log;
            listener = rt => log.Add(rt.name + " reapplyDrivenProperties");
            RectTransform.reapplyDrivenProperties += listener;
        }

        public new void Dispose()
        {
            RectTransform.reapplyDrivenProperties -= listener;
            Recorder.Log = null;
            base.Dispose();
        }

        GridLayoutGroup grid;
        readonly RectTransform[] t = new RectTransform[9];

        /// G: a grid of three columns (cells 80 x 100, spacing 4) at the canvas's upper left,
        /// holding T0..T8: bare 40 x 30 rects, each with a stretched Image child BG. T5..T8 start
        /// inactive and have never been laid out.
        void Build()
        {
            var canvas = NewCanvas();
            var g = NewRect("G", canvas.transform, 248, 100);
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(0, 1);
            grid = g.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(80, 100);
            grid.spacing = new Vector2(4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            for (int i = 0; i < 9; i++)
            {
                t[i] = NewRect("T" + i, g, 40, 30);
                t[i].anchoredPosition = new Vector2(7, -7);
                var bg = NewRect("BG" + i, t[i], 0, 0, typeof(Image));
                bg.anchorMin = Vector2.zero;
                bg.anchorMax = Vector2.one;
                t[i].gameObject.AddComponent<Recorder>();
                if (i >= 5) t[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < 3; i++) Frame();
            log.Clear();
        }

        [Fact]
        public void SetActiveRaisesReapplyDrivenPropertiesChildFirstBeforeTheCallbacks()
        {
            Build();
            t[5].gameObject.SetActive(true);
            Assert.Equal(new[] { "BG5 reapplyDrivenProperties", "T5 reapplyDrivenProperties", "T5 OnEnable" }, log);
            log.Clear();
            t[0].gameObject.SetActive(false);
            Assert.Equal(new[] { "BG0 reapplyDrivenProperties", "T0 reapplyDrivenProperties", "T0 OnDisable" }, log);
            log.Clear();
            t[0].gameObject.SetActive(false);
            Assert.Empty(log);
        }

        [Fact]
        public void ABareChildThatAppearsIsLaidOutInThatFrame()
        {
            Build();
            t[5].gameObject.SetActive(true);
            Frame();
            Assert.Equal(new Vector2(208, -154), t[5].anchoredPosition);
            Assert.Equal(new Vector2(80, 100), t[5].sizeDelta);
        }

        [Fact]
        public void HiddenAndShownChildrenSwappedAfterAReorderAreLaidOut()
        {
            Build();
            for (int i = 8; i >= 0; i--) t[i].SetAsFirstSibling();
            Frame();
            for (int i = 0; i < 9; i++) t[i].gameObject.SetActive(i >= 5);
            Frame();
            Assert.Equal(new Vector2(40, -50), t[5].anchoredPosition);
            Assert.Equal(new Vector2(124, -50), t[6].anchoredPosition);
            Assert.Equal(new Vector2(208, -50), t[7].anchoredPosition);
            Assert.Equal(new Vector2(40, -154), t[8].anchoredPosition);
            for (int i = 5; i < 9; i++) Assert.Equal(new Vector2(80, 100), t[i].sizeDelta);
            Assert.Equal(204f, grid.preferredHeight);
        }

        [Fact]
        public void ActivationSendsNoDimensionsMessage()
        {
            Build();
            grid.enabled = false;
            t[5].gameObject.SetActive(true);
            t[0].gameObject.SetActive(false);
            Frame();
            Assert.DoesNotContain(log, l => l.EndsWith("OnRectTransformDimensionsChange"));
        }
    }
}
