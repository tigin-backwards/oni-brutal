using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// docs/cleanroom/imgui.md, "GUILayout, measured", part 5: too little width, and word wrap
    /// (ModTools' Runtime Console), with the values of the probe's `GL wrap` lines.
    public partial class GUILayoutMeasuredTests
    {
        const string Long1 = "[17:12:36.557] [1] [WARNING] baby_snail_iron_kanim iron_ui placeSymbol [ iron_ui ] is missing";
        const string Long2 = "[17:12:37.970] [1] [WARNING] world Asteroid has invalid season VanillaSandstoneDefaultMeteorShowers";
        const string Long3 = "[17:12:40.206] [1] [INFO] Screen resolution updated, saving values to prefs: 1280x720 @ 0, fullscreen: Windowed";
        const string Short1 = "[OniMcp] Queued client request notifications/message for 1 session(s).";
        const string Line3 = "[17:12:37.866] [1] [INFO] Checking which steam branch we're on. Got: []";

        static string Text(string name) => name switch
        {
            "Long1" => Long1,
            "Long3" => Long3,
            _ => name,
        };

        /// The character indices whose cursor starts a new line, as the probe reads them ("-":
        /// one line).
        static string Breaks(GUIStyle style, Rect r, string text)
        {
            var c = new GUIContent(text);
            var parts = new StringBuilder();
            float y = float.NaN;
            for (int i = 0; i < text.Length; i++)
            {
                var p = style.GetCursorPixelPosition(r, c, i);
                if (!float.IsNaN(y) && Mathf.Abs(p.y - y) > 1f) parts.Append(parts.Length == 0 ? "" : ",").Append(i);
                y = p.y;
            }
            return parts.Length == 0 ? "-" : parts.ToString();
        }

        /// Runs `body` once inside OnGUI (Repaint) with the game's skin.
        void InGui(System.Action body)
        {
            MakeProbe();
            bool ran = false;
            GUIProbe.Body = _ =>
            {
                if (!Repaint || ran) return;
                ran = true;
                body();
            };
            Frame();
            Assert.True(ran);
        }

        // Rule 1: CalcMinMaxWidth of a word-wrapping style is min(32, max), whatever the words;
        // text widths are fractional.
        [Theory]
        [InlineData("Count: 59", 32f, 54.72f)]
        [InlineData("Count:", 32f, 37.02f)]
        [InlineData("X - Close Window", 32f, 100.55f)]
        [InlineData("Show when Start", 32f, 96.06f)]
        [InlineData("Clear", 30.31f, 30.31f)]
        public void AWrappingStylesMinimumWidthIsAtMost32(string text, float min, float max)
        {
            float mn = 0f, mx = 0f;
            Vector2 size = default;
            InGui(() =>
            {
                GUI.skin.label.CalcMinMaxWidth(new GUIContent(text), out mn, out mx);
                size = GUI.skin.label.CalcSize(new GUIContent(text));
            });
            Assert.Equal(min, mn);
            Assert.Equal(max, mx);
            Assert.Equal(new Vector2(max, 19.01f), size);
        }

        [Theory]
        [InlineData(Long1, 535.33f)]
        [InlineData(Long3, 639.7f)]
        public void LongLinesMinimumIs32(string text, float max)
        {
            float mn = 0f, mx = 0f;
            InGui(() => GUI.skin.label.CalcMinMaxWidth(new GUIContent(text), out mn, out mx));
            Assert.Equal(32f, mn);
            // Measured 535.33 and 639.7; the font's linear advances give these within 0.05.
            Assert.Equal(max, mx, 0.05f);
        }

        // Rule 2: wrapped text breaks at a space when the line up to it fits, and a word wider
        // than the line after its last character that fits (a space that does not fit starts
        // the next line). The probe's measure lines, CalcHeight and the breaks at each width.
        [Theory]
        [InlineData("Count: 59", 10f, 119.11f, "1,2,3,4,6,7,8")]
        [InlineData("Count: 59", 20f, 61.91f, "2,5,7")]
        [InlineData("Count: 59", 25f, 47.61f, "3,7")]
        [InlineData("Count: 59", 30f, 47.61f, "3,7")]
        [InlineData("Count: 59", 32f, 33.3099976f, "4")]
        [InlineData("Count: 59", 35f, 33.3099976f, "5")]
        [InlineData("Count: 59", 40f, 33.3099976f, "6")]
        [InlineData("Count: 59", 45f, 33.3099976f, "7")]
        [InlineData("Count: 59", 50f, 33.3099976f, "7")]
        [InlineData("Count: 59", 60f, 19.01f, "-")]
        [InlineData("Count: 59", 600f, 19.01f, "-")]
        [InlineData("Count:", 10f, 76.21f, "1,2,3,4")]
        [InlineData("Count:", 20f, 47.61f, "2,5")]
        [InlineData("Count:", 25f, 33.3099976f, "3")]
        [InlineData("Count:", 35f, 33.3099976f, "5")]
        [InlineData("Count:", 40f, 19.01f, "-")]
        [InlineData("X - Close Window", 10f, 190.61f, "1,2,4,5,7,8,9,10,11,13,14,15")]
        [InlineData("X - Close Window", 20f, 90.51f, "4,7,10,12,14")]
        [InlineData("X - Close Window", 25f, 76.21f, "4,7,10,13")]
        [InlineData("X - Close Window", 30f, 76.21f, "4,8,10,14")]
        [InlineData("X - Close Window", 35f, 76.21f, "4,9,10,14")]
        [InlineData("X - Close Window", 40f, 61.91f, "4,10,15")]
        [InlineData("X - Close Window", 50f, 47.61f, "4,10")]
        [InlineData("X - Close Window", 60f, 33.3099976f, "10")]
        [InlineData("X - Close Window", 80f, 33.3099976f, "10")]
        [InlineData("X - Close Window", 120f, 19.01f, "-")]
        [InlineData("Clear", 10f, 61.91f, "1,3,4")]
        [InlineData("Clear", 20f, 33.3099976f, "3")]
        [InlineData("Clear", 25f, 33.3099976f, "3")]
        [InlineData("Clear", 30f, 33.3099976f, "4")]
        [InlineData("Clear", 35f, 19.01f, "-")]
        [InlineData("Show when Start", 10f, 204.91f, "1,2,3,4,5,6,7,8,9,10,11,12,13")]
        [InlineData("Show when Start", 20f, 90.51f, "2,5,7,10,13")]
        [InlineData("Show when Start", 25f, 90.51f, "3,5,8,10,14")]
        [InlineData("Show when Start", 30f, 76.21f, "3,5,8,10")]
        [InlineData("Show when Start", 35f, 61.91f, "4,5,10")]
        [InlineData("Show when Start", 40f, 47.61f, "5,10")]
        [InlineData("Show when Start", 60f, 47.61f, "5,10")]
        [InlineData("Show when Start", 80f, 33.3099976f, "10")]
        [InlineData("Show when Start", 120f, 19.01f, "-")]
        [InlineData("Long1", 300f, 47.61f, "29,81")]
        [InlineData("Long1", 400f, 33.3099976f, "59")]
        [InlineData("Long1", 564f, 19.01f, "-")]
        [InlineData("Long1", 600f, 19.01f, "-")]
        [InlineData("Long3", 300f, 47.61f, "53,103")]
        [InlineData("Long3", 400f, 33.3099976f, "70")]
        [InlineData("Long3", 600f, 33.3099976f, "103")]
        public void WrappedTextBreaksInsideAWordWiderThanTheLine(string name, float width, float height, string breaks)
        {
            string text = Text(name);
            float h = 0f;
            string b = null;
            InGui(() =>
            {
                h = GUI.skin.label.CalcHeight(new GUIContent(text), width);
                b = Breaks(GUI.skin.label, new Rect(0f, 0f, width, 200f), text);
            });
            Assert.Equal(breaks, b);
            Assert.Equal(height, h);
        }

        // Rule 3: a horizontal group narrower than its entries' minimums gives each its minimum
        // from its left edge; the row runs past the edge and keeps the width it was given.
        [Theory]
        [InlineData(200f)]
        [InlineData(120f)]
        public void ATooNarrowRowKeepsEveryEntryAtItsMinimum(float width)
        {
            Rect row = default;
            var r = InArea(at =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.Button("X - Close Window"); at("close");
                GUILayout.Button("Clear"); at("clear");
                GUILayout.Label("Count: 59"); at("count");
                GUILayout.TextField("", GUILayout.MinWidth(80f)); at("tf");
                GUILayout.EndHorizontal();
                if (Repaint) row = GUILayoutUtility.GetLastRect();
            }, width, 100f);
            Assert.Equal(new Rect(0f, 0f, 113f, 20f), r["close"]);
            Assert.Equal(new Rect(117f, 0f, 43f, 20f), r["clear"]);
            Assert.Equal(new Rect(164f, 0f, 32f, 33.3099976f), r["count"]);
            Assert.Equal(new Rect(200f, 0f, 80f, 20f), r["tf"]);
            Assert.Equal(new Rect(0f, 0f, width, 33f), row);

            var b = InArea(at =>
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 6; i++) { GUILayout.Button("button " + i); at("b" + i); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 6; i++) { GUILayout.Label("label " + i); at("l" + i); }
                GUILayout.EndHorizontal();
            }, width, 100f);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(new Rect(62f * i, 0f, 58f, 20f), b["b" + i]);
                Assert.Equal(36f * i, b["l" + i].x);
                Assert.Equal(32f, b["l" + i].width);
                Assert.Equal(33.3099976f, b["l" + i].height);
            }
        }

        static void Toolbar(Action<string> at)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Button("X - Close Window"); at("close");
            GUILayout.Button("Clear"); at("clear");
            GUILayout.Label("Count: 59"); at("count");
            GUILayout.TextField("", GUILayout.MinWidth(220f)); at("tf");
            GUILayout.Toggle(true, "Show when Start"); at("t1");
            GUILayout.Toggle(true, "Show Window when Error"); at("t2");
            GUILayout.Space(10f); at("sp1");
            GUILayout.Toggle(true, "Auto scroll"); at("t3");
            GUILayout.Space(10f); at("sp2");
            GUILayout.Toggle(true, "Error"); at("t4");
            GUILayout.Toggle(true, "Warning"); at("t5");
            GUILayout.Toggle(true, "Log"); at("t6");
            GUILayout.EndHorizontal();
            at("row");
        }

        [Theory]
        [InlineData(630f)]
        [InlineData(400f)]
        public void TheConsolesToolbarRunsPastANarrowArea(float width)
        {
            var r = InArea(Toolbar, width, 100f);
            Assert.Equal(new Rect(0f, 0f, 113f, 20f), r["close"]);
            Assert.Equal(new Rect(117f, 0f, 43f, 20f), r["clear"]);
            Assert.Equal(new Rect(164f, 0f, 32f, 33.3099976f), r["count"]);
            Assert.Equal(new Rect(200f, 0f, 220f, 20f), r["tf"]);
            Assert.Equal(new Rect(424f, 0f, 112f, 17f), r["t1"]);
            Assert.Equal(new Rect(540f, 0f, 161f, 17f), r["t2"]);
            Assert.Equal(new Rect(701f, -4f, 10f, 0f), r["sp1"]);
            Assert.Equal(new Rect(715f, 0f, 75f, 17f), r["t3"]);
            Assert.Equal(new Rect(790f, -4f, 10f, 0f), r["sp2"]);
            Assert.Equal(new Rect(804f, 0f, 44f, 17f), r["t4"]);
            Assert.Equal(new Rect(852f, 0f, 63f, 17f), r["t5"]);
            Assert.Equal(new Rect(919f, 0f, 37f, 17f), r["t6"]);
            Assert.Equal(new Rect(0f, 0f, width, 33f), r["row"]);
        }

        // Rule 4: with room to spare the label first grows to its maximum, then the rest is
        // shared equally.
        [Fact]
        public void WithRoomToSpareALabelGrowsToItsWidthFirst()
        {
            var r = InArea(at =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.Button("X - Close Window"); at("close");
                GUILayout.Button("Clear"); at("clear");
                GUILayout.Label("Count: 59"); at("count");
                GUILayout.TextField("", GUILayout.MinWidth(80f)); at("tf");
                GUILayout.EndHorizontal();
            }, 400f, 100f);
            Assert.Equal(new Rect(0f, 0f, 137f, 20f), r["close"]);
            Assert.Equal(new Rect(141f, 0f, 67f, 20f), r["clear"]);
            Assert.Equal(new Rect(212f, 0f, 79f, 19.01f), r["count"]);
            Assert.Equal(new Rect(296f, 0f, 104f, 20f), r["tf"]);

            var t = InArea(Toolbar, 1000f, 100f);
            Assert.Equal(new Rect(0f, 0f, 115f, 20f), t["close"]);
            Assert.Equal(new Rect(119f, 0f, 45f, 20f), t["clear"]);
            Assert.Equal(new Rect(168f, 0f, 57f, 19.01f), t["count"]);
            Assert.Equal(new Rect(229f, 0f, 222f, 20f), t["tf"]);
            Assert.Equal(new Rect(455f, 0f, 114f, 17f), t["t1"]);
            Assert.Equal(new Rect(574f, 0f, 163f, 17f), t["t2"]);
            Assert.Equal(new Rect(751f, 0f, 77f, 17f), t["t3"]);
            Assert.Equal(new Rect(842f, 0f, 46f, 17f), t["t4"]);
            Assert.Equal(new Rect(892f, 0f, 65f, 17f), t["t5"]);
            Assert.Equal(new Rect(961f, 0f, 39f, 17f), t["t6"]);
            Assert.Equal(new Rect(0f, 0f, 1000f, 20f), t["row"]);
        }

        // Rules 5 and 6: a scroll view of a fixed width lays its content out at that width, so
        // wrapping labels wrap in it and nothing overflows; Height(h) fixes a wrapping label's
        // height.
        [Fact]
        public void AFixedWidthScrollViewWrapsItsLabels()
        {
            var breaks = new Dictionary<string, string>();
            Vector2 pos = default, posTall = default, posNoOpt = default;
            Rect view = default, viewTall = default, viewNoOpt = default;
            void Lab(System.Action<string> at, string name, string text, params GUILayoutOption[] o)
            {
                GUILayout.Label(text, o);
                at(name);
                if (Repaint) breaks[name] = Breaks(GUI.skin.label, GUILayoutUtility.GetLastRect(), text);
            }
            var r = InArea(at =>
            {
                pos = GUILayout.BeginScrollView(new Vector2(100000f, 100000f), GUILayout.Width(380f), GUILayout.Height(150f));
                GUILayout.BeginHorizontal();
                GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)); at("copy");
                Lab(at, "l", Long1, GUILayout.Height(24f));
                GUILayout.EndHorizontal();
                Lab(at, "alone", Long2);
                GUILayout.EndScrollView();
                if (Repaint) view = GUILayoutUtility.GetLastRect();
            }, 400f, 300f);
            Assert.Equal(new Rect(4f, 4f, 44f, 24f), r["copy"]);
            Assert.Equal(new Rect(52f, 4f, 324f, 24f), r["l"]);
            Assert.Equal("51", breaks["l"]);
            Assert.Equal(new Rect(4f, 32f, 372f, 33f), r["alone"]);
            Assert.Equal("63", breaks["alone"]);
            Assert.Equal(new Rect(0f, 0f, 380f, 150f), view);
            Assert.Equal(Vector2.zero, pos);

            var n = InArea(at =>
            {
                posNoOpt = GUILayout.BeginScrollView(new Vector2(100000f, 100000f));
                GUILayout.BeginHorizontal();
                GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)); at("copy");
                Lab(at, "l", Long1, GUILayout.Height(24f));
                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
                if (Repaint) viewNoOpt = GUILayoutUtility.GetLastRect();
            }, 400f, 300f);
            Assert.Equal(new Rect(52f, 4f, 344f, 24f), n["l"]);
            Assert.Equal(new Rect(0f, 0f, 400f, 300f), viewNoOpt);
            Assert.Equal(Vector2.zero, posNoOpt);

            var t = InArea(at =>
            {
                posTall = GUILayout.BeginScrollView(new Vector2(100000f, 100000f), GUILayout.Width(380f), GUILayout.Height(120f));
                for (int i = 0; i < 8; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)); at("copy" + i);
                    Lab(at, "l" + i, i % 2 == 0 ? Long1 : Short1, GUILayout.Height(24f));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                if (Repaint) viewTall = GUILayoutUtility.GetLastRect();
            }, 400f, 300f);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(new Rect(4f, 4f + 28f * i, 44f, 24f), t["copy" + i]);
                Assert.Equal(new Rect(52f, 4f + 28f * i, 308f, 24f), t["l" + i]);
                Assert.Equal(i % 2 == 0 ? "51" : "31", breaks["l" + i]);
            }
            Assert.Equal(new Rect(0f, 0f, 380f, 120f), viewTall);
            Assert.Equal(new Vector2(0f, 108f), posTall);
        }

        // Rule 7: ModTools' Runtime Console window, as the mod lays it out.
        [Fact]
        public void TheRuntimeConsoleWindow()
        {
            MakeProbe();
            var r = new Dictionary<string, Rect>();
            var breaks = new Dictionary<string, string>();
            Vector2 pos = default;
            string[] lines = { Long1, Long2, Line3, Long3, Short1, Short1 };
            void At(string name)
            {
                if (Repaint) r[name] = GUILayoutUtility.GetLastRect();
            }
            GUIProbe.Body = _ => GUI.Window(951, new Rect(620f, 120f, 650f, 500f), id =>
            {
                GUI.DragWindow(new Rect(0f, 0f, 2000f, 20f));
                GUILayout.BeginVertical();
                Toolbar(At);
                pos = GUILayout.BeginScrollView(new Vector2(100000f, 100000f), GUILayout.Width(650f - 30f));
                for (int i = 0; i < lines.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)); At("copy" + i);
                    GUILayout.Label(lines[i], GUILayout.Height(24f)); At("l" + i);
                    if (Repaint) breaks["l" + i] = Breaks(GUI.skin.label, GUILayoutUtility.GetLastRect(), lines[i]);
                    GUILayout.EndHorizontal(); At("row" + i);
                }
                GUILayout.EndScrollView(); At("view");
                GUILayout.EndVertical(); At("vert");
            }, "Runtime Console");
            Frame();
            Assert.Equal(new Rect(14f, 20f, 113f, 20f), r["close"]);
            Assert.Equal(new Rect(131f, 20f, 43f, 20f), r["clear"]);
            Assert.Equal(new Rect(178f, 20f, 32f, 33.3099976f), r["count"]);
            Assert.Equal(new Rect(214f, 20f, 220f, 20f), r["tf"]);
            Assert.Equal(new Rect(438f, 20f, 112f, 17f), r["t1"]);
            Assert.Equal(new Rect(933f, 20f, 37f, 17f), r["t6"]);
            Assert.Equal(new Rect(14f, 20f, 622f, 33f), r["row"]);
            Assert.Equal(new Rect(10f, 57f, 620f, 433f), r["view"]);
            Assert.Equal(new Rect(10f, 20f, 630f, 470f), r["vert"]);
            for (int i = 0; i < lines.Length; i++)
            {
                Assert.Equal(new Rect(4f, 4f + 28f * i, 612f, 24f), r["row" + i]);
                Assert.Equal(new Rect(4f, 4f + 28f * i, 44f, 24f), r["copy" + i]);
                Assert.Equal(new Rect(52f, 4f + 28f * i, 564f, 24f), r["l" + i]);
            }
            Assert.Equal(new[] { "-", "63", "-", "91", "-", "-" }, new[] { breaks["l0"], breaks["l1"], breaks["l2"], breaks["l3"], breaks["l4"], breaks["l5"] });
            Assert.Equal(Vector2.zero, pos);
        }
    }
}
