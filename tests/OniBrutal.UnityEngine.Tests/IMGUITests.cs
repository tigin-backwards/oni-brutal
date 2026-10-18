using System;
using System.Collections.Generic;
using System.IO;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.IMGUI;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    /// A behaviour whose OnGUI runs a test-supplied body.
    public class GUIProbe : MonoBehaviour
    {
        public static Action<GUIProbe> Body;
        public readonly List<EventType> Events = new();

        void OnGUI()
        {
            Events.Add(Event.current.type);
            Body?.Invoke(this);
        }
    }

    /// A graphics device that renders frames and records GL immediate draws.
    internal sealed class RecordingDevice : OniBrutal.Engine.Rendering.RenderDevice
    {
        public readonly List<(Material Material, OniBrutal.Engine.Rendering.ImmediateVertex[] Vertices, Matrix4x4 Projection)> Draws = new();
        Material pass;
        public override OniBrutal.Engine.Rendering.GraphicsCaps Caps { get; } = new OniBrutal.Engine.Rendering.NullCaps();
        public override bool RendersFrames => true;
        public override void ReadPixels(OniBrutal.Engine.Rendering.RenderTextureNative source, RectInt rect, OniBrutal.Engine.Rendering.Texture2DNative destination, int destX, int destY) { }
        public override void ReleaseResource(object gpu) { }
        public override bool SetPass(Material material, int p)
        {
            pass = material;
            return true;
        }
        public override void WarmupShaders() { }
        public override void SetRenderTarget(in OniBrutal.Engine.Rendering.RenderTargetBinding target) { }
        public override void SetViewport(Rect pixels) { }
        public override void SetScissor(Rect? pixels) { }
        public override void Clear(bool color, bool depth, Color backgroundColor, float depthValue) { }
        protected override void ApplyCamera(in OniBrutal.Engine.Rendering.CameraConstants camera) { }
        /// Mesh draws with the view and projection in effect.
        public readonly List<(Material Material, Matrix4x4 View, Matrix4x4 Projection)> MeshDraws = new();
        /// The property sheet each mesh draw was given.
        public readonly List<OniBrutal.Engine.Rendering.PropertySheet> MeshDrawProperties = new();
        public override void Draw(in OniBrutal.Engine.Rendering.DrawRequest draw)
        {
            MeshDraws.Add((draw.Material, Camera.View, Camera.Projection));
            MeshDrawProperties.Add(draw.Properties);
        }
        public override void DrawImmediate(OniBrutal.Engine.Rendering.ImmediatePrimitive primitive, ReadOnlySpan<OniBrutal.Engine.Rendering.ImmediateVertex> vertices, Matrix4x4 modelView, Matrix4x4 projection)
            => Draws.Add((pass, vertices.ToArray(), projection));
        public override void Present() { }
    }

    public class IMGUITests : EngineTest, IDisposable
    {
        static string DataDir
        {
            get
            {
                var game = Environment.GetEnvironmentVariable("ONI_GAME_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
                return Path.Combine(game, "OxygenNotIncluded_Data");
            }
        }

        readonly List<string> errors = new();

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(message);
        }

        void IDisposable.Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            base.Dispose();
            Assert.Empty(errors);
        }

        public IMGUITests()
        {
            Application.logMessageReceivedThreaded += OnLog;
            AssetSystem.Initialize(DataDir);
            GUIManager.Install();
            GUIManager.Reset();
            GUIStateData.Reset();
            GUIProbe.Body = null;
        }

        static GUIProbe MakeProbe()
        {
            var go = new GameObject("gui");
            return go.AddComponent<GUIProbe>();
        }

        static void Queue(EventType type, Vector2 position, int clickCount = 1)
        {
            var e = new Event { type = type, mousePosition = position, button = 0, clickCount = clickCount };
            Event.Enqueue(e);
        }

        static void QueueKey(KeyCode key, char character = '\0', EventModifiers modifiers = 0)
        {
            Event.Enqueue(new Event { type = EventType.KeyDown, keyCode = key, character = character, modifiers = modifiers });
        }

        [Fact]
        public void LayoutAndRepaintEveryFrameEachPrecededByLayout()
        {
            var probe = MakeProbe();
            Frame();
            Assert.Equal(new[] { EventType.Layout, EventType.Repaint }, probe.Events);
            probe.Events.Clear();
            Queue(EventType.MouseDown, new Vector2(5f, 5f));
            Frame();
            Assert.Equal(new[] { EventType.Layout, EventType.MouseDown, EventType.Layout, EventType.Repaint }, probe.Events);
        }

        [Fact]
        public void PopEventQueueIsIndependentOfOnGUI()
        {
            var probe = MakeProbe();
            while (Event.PopEvent(new Event())) { }
            Queue(EventType.MouseDown, new Vector2(5f, 5f));
            var popped = new Event();
            Assert.True(Event.PopEvent(popped));
            Assert.Equal(EventType.MouseDown, popped.type);
            Frame();
            Assert.Contains(EventType.MouseDown, probe.Events);
        }

        [Fact]
        public void UnpoppedEventsAreDroppedAtTheEndOfTheFrame()
        {
            while (Event.PopEvent(new Event())) { }
            Queue(EventType.MouseDown, new Vector2(5f, 5f));
            Assert.Equal(1, Event.GetEventCount());
            Frame();
            Assert.Equal(0, Event.GetEventCount());
            Assert.False(Event.PopEvent(new Event()));
        }

        [Fact]
        public void GUIFunctionsNeedOnGUI()
        {
            Assert.Throws<ArgumentException>(() => GUI.skin);
            Assert.Throws<ArgumentException>(() => GUIUtility.GetControlID(FocusType.Passive));
        }

        [Fact]
        public void NestedLayoutGroups()
        {
            var probe = MakeProbe();
            var rects = new Rect[4];
            Vector2 areaOrigin = default;
            GUIProbe.Body = _ =>
            {
                GUILayout.BeginArea(new Rect(10f, 20f, 300f, 200f));
                GUILayout.BeginHorizontal();
                rects[0] = GUILayoutUtility.GetRect(50f, 20f, GUILayout.Width(50f), GUILayout.Height(20f));
                rects[1] = GUILayoutUtility.GetRect(30f, 40f, GUILayout.Width(30f), GUILayout.Height(40f));
                GUILayout.EndHorizontal();
                GUILayout.BeginVertical();
                rects[2] = GUILayoutUtility.GetRect(100f, 10f, GUILayout.Width(100f), GUILayout.Height(10f));
                rects[3] = GUILayoutUtility.GetRect(100f, 10f, GUILayout.Width(100f), GUILayout.Height(10f));
                GUILayout.EndVertical();
                if (Event.current.type == EventType.Repaint) areaOrigin = GUIClip.Unclip(Vector2.zero);
                GUILayout.EndArea();
            };
            Frame();
            Assert.Equal(new Rect(0f, 0f, 50f, 20f), rects[0]);
            Assert.Equal(new Rect(50f, 0f, 30f, 40f), rects[1]);
            Assert.Equal(new Rect(0f, 40f, 100f, 10f), rects[2]);
            Assert.Equal(new Rect(0f, 50f, 100f, 10f), rects[3]);
            Assert.Equal(new Vector2(10f, 20f), areaOrigin);
        }

        [Fact]
        public void StretchingEntriesShareTheExtraSpace()
        {
            var probe = MakeProbe();
            Rect a = default, b = default;
            GUIProbe.Body = _ =>
            {
                GUILayout.BeginArea(new Rect(0f, 0f, 200f, 100f));
                GUILayout.BeginHorizontal();
                a = GUILayoutUtility.GetRect(20f, 10f);
                b = GUILayoutUtility.GetRect(40f, 10f);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            };
            Frame();
            // GUIStyle.none stretches horizontally: (200 - 60) / 2 extra each.
            Assert.Equal(new Rect(0f, 0f, 90f, 10f), a);
            Assert.Equal(new Rect(90f, 0f, 110f, 10f), b);
        }

        [Fact]
        public void ControlIdsAreStablePerPositionAndHint()
        {
            var probe = MakeProbe();
            var ids = new List<int[]>();
            GUIProbe.Body = _ =>
            {
                int a = GUIUtility.GetControlID(1, FocusType.Passive);
                int b = GUIUtility.GetControlID(2, FocusType.Keyboard);
                int c = GUIUtility.GetControlID(1, FocusType.Passive, new Rect(0f, 0f, 1f, 1f));
                ids.Add(new[] { a, b, c });
            };
            Frame();
            Frame();
            Assert.Equal(4, ids.Count);
            Assert.Equal(3, new HashSet<int>(ids[0]).Count);
            foreach (var set in ids) Assert.Equal(ids[0], set);
            // A skipped control keeps the IDs of the controls after it (matched by hint).
            int[] skipped = null;
            GUIProbe.Body = _ =>
            {
                int b = GUIUtility.GetControlID(2, FocusType.Keyboard);
                int c = GUIUtility.GetControlID(1, FocusType.Passive);
                skipped = new[] { b, c };
            };
            Frame();
            Assert.Equal(ids[0][1], skipped[0]);
            Assert.Equal(ids[0][2], skipped[1]);
        }

        [Fact]
        public void ButtonClickSequence()
        {
            var probe = MakeProbe();
            var rect = new Rect(10f, 10f, 100f, 30f);
            var results = new List<(EventType, bool, int)>();
            GUIProbe.Body = _ =>
            {
                var type = Event.current.type;
                bool clicked = GUI.Button(rect, "Press");
                results.Add((type, clicked, GUIUtility.hotControl));
            };
            Queue(EventType.MouseDown, new Vector2(20f, 20f));
            Queue(EventType.MouseUp, new Vector2(25f, 22f));
            Frame();
            Assert.DoesNotContain(results, r => r.Item1 == EventType.MouseDown && r.Item2);
            var down = results.Find(r => r.Item1 == EventType.MouseDown);
            Assert.NotEqual(0, down.Item3);
            var up = results.Find(r => r.Item1 == EventType.MouseUp);
            Assert.True(up.Item2);
            Assert.Equal(0, up.Item3);
            Assert.Equal(0, GUIUtility.hotControl);

            // Releasing outside the button does not click it.
            results.Clear();
            Queue(EventType.MouseDown, new Vector2(20f, 20f));
            Queue(EventType.MouseUp, new Vector2(300f, 300f));
            Frame();
            Assert.DoesNotContain(results, r => r.Item2);
        }

        [Fact]
        public void InputStopsAtTheFrontBehaviourThatUsesIt()
        {
            var back = MakeProbe();
            var front = MakeProbe();
            var log = new List<string>();
            GUIProbe.Body = p =>
            {
                if (p == front) GUI.depth = -1;
                if (Event.current.type == EventType.MouseDown)
                {
                    log.Add(p == front ? "front" : "back");
                    if (p == front) Event.current.Use();
                }
            };
            Frame();
            Queue(EventType.MouseDown, new Vector2(1f, 1f));
            Frame();
            Assert.Equal(new[] { "front" }, log);
        }

        [Fact]
        public void GroupsOffsetAndClipMousePosition()
        {
            var probe = MakeProbe();
            Vector2 inGroup = default, nested = default;
            Rect visible = default;
            GUIProbe.Body = _ =>
            {
                if (Event.current.type != EventType.MouseDown) return;
                GUI.BeginGroup(new Rect(100f, 50f, 200f, 100f));
                inGroup = Event.current.mousePosition;
                GUI.BeginGroup(new Rect(150f, 80f, 200f, 100f));
                nested = Event.current.mousePosition;
                visible = GUIClip.visibleRect;
                GUI.EndGroup();
                GUI.EndGroup();
            };
            Queue(EventType.MouseDown, new Vector2(130f, 70f));
            Frame();
            Assert.Equal(new Vector2(30f, 20f), inGroup);
            Assert.Equal(new Vector2(-120f, -60f), nested);
            // The inner group is clipped by the outer one: 50 x 20 visible.
            Assert.Equal(new Rect(0f, 0f, 50f, 20f), visible);
        }

        [Fact]
        public void MatrixScalesAroundPivot()
        {
            var probe = MakeProbe();
            Vector2 local = default, screen = default;
            GUIProbe.Body = _ =>
            {
                if (Event.current.type != EventType.MouseDown) return;
                GUIUtility.ScaleAroundPivot(new Vector2(2f, 2f), new Vector2(100f, 100f));
                local = Event.current.mousePosition;
                screen = GUIUtility.GUIToScreenPoint(new Vector2(200f, 200f));
            };
            Queue(EventType.MouseDown, new Vector2(140f, 160f));
            Frame();
            Assert.Equal(new Vector2(120f, 130f), local);
            Assert.Equal(new Vector2(300f, 300f), screen);
        }

        [Fact]
        public void GUIClipReflectionMatchesUnity()
        {
            var type = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip", true);
            Assert.Null(type.GetProperty("topmostRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
            Assert.NotNull(type.GetMethod("GetTopRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            Assert.NotNull(type.GetMethod("Clip", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, new[] { typeof(Rect) }, null));
        }

        [Fact]
        public void BuiltinSkinLoadsWithItsStyles()
        {
            var probe = MakeProbe();
            GUISkin skin = null;
            GUIProbe.Body = _ => skin = GUI.skin;
            Frame();
            Assert.NotNull(skin);
            Assert.Equal("GameSkin", skin.name);
            Assert.Same(skin.label, skin.GetStyle("Label"));
            Assert.NotNull(skin.button.normal.background);
            Assert.NotNull(skin.window.normal.background);
            Assert.True(skin.button.border.left > 0);
            Assert.True(skin.settings.doubleClickSelectsWord);
        }

        [Fact]
        public void CalcSizeWithTheLegacyFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var style = new GUIStyle { font = font, padding = new RectOffset(3, 5, 2, 4) };
            var empty = style.CalcSize(GUIContent.none);
            Assert.True(empty.y > 4f);
            Assert.Equal(8f, empty.x);
            var one = style.CalcSize(new GUIContent("Hello"));
            var two = style.CalcSize(new GUIContent("Hello Hello"));
            Assert.True(one.x > 8f);
            Assert.True(two.x > one.x * 1.5f);
            Assert.Equal(one.y, two.y);
            var lines = style.CalcSize(new GUIContent("Hello\nHello"));
            Assert.True(lines.y > one.y + 8f);
            style.fixedWidth = 77f;
            Assert.Equal(77f, style.CalcSize(new GUIContent("Hello")).x);
            style.fixedWidth = 0f;
            style.wordWrap = true;
            float wrapped = style.CalcHeight(new GUIContent("Hello Hello Hello Hello"), one.x + 2f);
            Assert.True(wrapped > one.y * 2f);
            style.CalcMinMaxWidth(new GUIContent("Hello Hello"), out float min, out float max);
            Assert.Equal(two.x, max);
            Assert.True(min < max);
        }

        [Fact]
        public void TextFieldEditing()
        {
            var probe = MakeProbe();
            string text = "abc";
            var rect = new Rect(10f, 10f, 200f, 24f);
            GUIProbe.Body = _ => text = GUI.TextField(rect, text);
            Frame();
            // Click to focus (the whole text is selected on focus), then edit.
            Queue(EventType.MouseDown, new Vector2(15f, 20f));
            Queue(EventType.MouseUp, new Vector2(15f, 20f));
            Frame();
            Assert.NotEqual(0, GUIUtility.keyboardControl);
            QueueKey(KeyCode.End, '\0', EventModifiers.FunctionKey);
            QueueKey(KeyCode.None, 'd');
            QueueKey(KeyCode.None, 'e');
            QueueKey(KeyCode.Backspace, '\0', EventModifiers.FunctionKey);
            QueueKey(KeyCode.Home, '\0', EventModifiers.FunctionKey);
            QueueKey(KeyCode.Delete, '\0', EventModifiers.FunctionKey);
            Frame();
            Assert.Equal("bcd", text);
            QueueKey(KeyCode.A, 'a', EventModifiers.Control);
            QueueKey(KeyCode.C, 'c', EventModifiers.Control);
            Frame();
            Assert.Equal("bcd", GUIUtility.systemCopyBuffer);
            QueueKey(KeyCode.None, 'x');
            Frame();
            Assert.Equal("x", text);
            QueueKey(KeyCode.V, 'v', EventModifiers.Control);
            Frame();
            Assert.Equal("xbcd", text);
            // A click no control takes ends editing.
            Queue(EventType.MouseDown, new Vector2(500f, 500f));
            Frame();
            Assert.Equal(0, GUIUtility.keyboardControl);
        }

        [Fact]
        public void DisabledControlsIgnoreInput()
        {
            var probe = MakeProbe();
            var rect = new Rect(10f, 10f, 100f, 30f);
            bool clicked = false;
            var raw = new List<EventType>();
            GUIProbe.Body = _ =>
            {
                GUI.enabled = false;
                if (Event.current.rawType == EventType.MouseDown) raw.Add(Event.current.type);
                clicked |= GUI.Button(rect, "Off");
                GUI.enabled = true;
            };
            Queue(EventType.MouseDown, new Vector2(20f, 20f));
            Queue(EventType.MouseUp, new Vector2(20f, 20f));
            Frame();
            Assert.False(clicked);
            Assert.Equal(0, GUIUtility.hotControl);
            Assert.Contains(EventType.Ignore, raw);
        }

        [Fact]
        public void TextEditorCopiesItsText()
        {
            var editor = new TextEditor { text = "copy me" };
            editor.SelectAll();
            editor.Copy();
            Assert.Equal("copy me", GUIUtility.systemCopyBuffer);
            Assert.Equal(7, editor.selectIndex);
            editor.MoveLeft();
            Assert.Equal(0, editor.cursorIndex);
            editor.MoveWordRight();
            Assert.Equal(4, editor.cursorIndex);
            editor.MoveLeft();
            editor.SelectCurrentWord();
            Assert.Equal("copy", editor.SelectedText);
        }

        [Fact]
        public void RepaintDrawsSlicedBackgroundsAndClippedText()
        {
            ProjectSettings.Load();
            var device = new RecordingDevice();
            var previous = OniBrutal.Engine.Rendering.GraphicsDevice.Current;
            OniBrutal.Engine.Rendering.GraphicsDevice.Install(device);
            try
            {
                var probe = MakeProbe();
                GUIProbe.Body = _ =>
                {
                    GUI.Box(new Rect(10f, 10f, 100f, 40f), GUIContent.none);
                    GUI.BeginGroup(new Rect(200f, 100f, 30f, 20f));
                    GUI.Label(new Rect(0f, 0f, 300f, 20f), "A long label clipped by its group");
                    GUI.EndGroup();
                };
                Frame();
                // The box: nine slices of the skin's box texture inside its rect.
                var box = device.Draws[0];
                Assert.Equal("Hidden/Internal-GUITextureClip", box.Material.shader.name);
                Assert.Equal(36, box.Vertices.Length);
                foreach (var v in box.Vertices)
                {
                    Assert.InRange(v.Position.x, 10f, 110f);
                    Assert.InRange(v.Position.y, 10f, 50f);
                }
                // Pixel space with the origin at the top left.
                var topLeft = box.Projection.MultiplyPoint(new Vector3(0f, 0f, 0f));
                Assert.Equal(-1f, topLeft.x, 3);
                Assert.Equal(1f, topLeft.y, 3);
                // The label's glyphs, clipped to the group.
                var text = device.Draws[1];
                Assert.Equal("Hidden/Internal-GUITextureClipText", text.Material.shader.name);
                Assert.NotEmpty(text.Vertices);
                foreach (var v in text.Vertices)
                {
                    Assert.InRange(v.Position.x, 200f, 230f);
                    Assert.InRange(v.Position.y, 100f, 120f);
                }
            }
            finally
            {
                OniBrutal.Engine.Rendering.GraphicsDevice.Install(previous);
            }
        }

        // EventType has lower-case aliases, so ToString is ambiguous.
        static string Name(EventType t) => t switch
        {
            EventType.Layout => "Layout",
            EventType.Repaint => "Repaint",
            EventType.Used => "Used",
            _ => ((int)t).ToString(),
        };

        [Fact]
        public void WindowsDrawAfterOnGUIAndCanBeDragged()
        {
            var probe = MakeProbe();
            var windowRect = new Rect(50f, 50f, 200f, 100f);
            var order = new List<string>();
            GUIProbe.Body = _ =>
            {
                order.Add("ongui:" + Name(Event.current.type));
                windowRect = GUI.Window(7, windowRect, id =>
                {
                    order.Add("window:" + Name(Event.current.type));
                    GUI.DragWindow();
                }, "Title", GUI.skin.window);
            };
            Frame();
            Assert.Equal(new[] { "ongui:Layout", "window:Layout", "ongui:Repaint", "window:Repaint" }, order);
            Queue(EventType.MouseDown, new Vector2(60f, 60f));
            Event.Enqueue(new Event { type = EventType.MouseDrag, mousePosition = new Vector2(90f, 75f), delta = new Vector2(30f, 15f) });
            Queue(EventType.MouseUp, new Vector2(90f, 75f));
            Frame();
            Frame();
            Assert.Equal(new Rect(80f, 65f, 200f, 100f), windowRect);
        }

        [Fact]
        public void PopEventLeavesOnGUIItsEventsAndTheRestIsDropped()
        {
            var probe = MakeProbe();
            while (Event.PopEvent(new Event())) { }
            Queue(EventType.MouseDown, new Vector2(5f, 5f));
            Queue(EventType.MouseUp, new Vector2(5f, 5f));
            Assert.Equal(2, Event.GetEventCount());
            var popped = new Event();
            Assert.True(Event.PopEvent(popped));
            Assert.Equal(EventType.MouseDown, popped.type);
            Assert.Equal(new Vector2(5f, 5f), popped.mousePosition);
            Assert.Equal(1, Event.GetEventCount());
            Frame();
            // OnGUI got both, and the unpopped MouseUp did not survive the frame.
            Assert.Equal(new[] { EventType.Layout, EventType.MouseDown, EventType.Layout, EventType.MouseUp, EventType.Layout, EventType.Repaint }, probe.Events);
            Assert.Equal(0, Event.GetEventCount());
            Frame();
            Assert.False(Event.PopEvent(new Event()));
        }

        [Fact]
        public void BehavioursWithoutGUILayoutGetNoLayoutEvents()
        {
            var probe = MakeProbe();
            probe.useGUILayout = false;
            Frame();
            Assert.Equal(new[] { EventType.Repaint }, probe.Events);
            probe.Events.Clear();
            Queue(EventType.MouseDown, new Vector2(5f, 5f));
            Frame();
            Assert.Equal(new[] { EventType.MouseDown, EventType.Repaint }, probe.Events);
            // GUILayout is an error there; GUI controls work.
            bool reached = false, clicked = false;
            GUIProbe.Body = _ =>
            {
                clicked |= GUI.Button(new Rect(0f, 0f, 50f, 20f), "b");
                GUILayout.Label("not allowed");
                reached = true;
            };
            Frame();
            Assert.False(reached);
            Assert.Single(errors);
            Assert.Contains("useGUILayout", errors[0]);
            errors.Clear();
        }

        [Fact]
        public void ExitGUILeavesTheCallSilently()
        {
            var first = MakeProbe();
            var second = MakeProbe();
            var log = new List<string>();
            bool exiting = false;
            GUIProbe.Body = p =>
            {
                if (p != first) { log.Add("second"); return; }
                GUI.BeginGroup(new Rect(0f, 0f, 10f, 10f));
                try
                {
                    GUIUtility.ExitGUI();
                }
                finally
                {
                    exiting = GUIUtility.guiIsExiting;
                }
                log.Add("after exit");
            };
            Frame();
            Assert.True(exiting);
            Assert.False(GUIUtility.guiIsExiting);
            Assert.DoesNotContain("after exit", log);
            Assert.Contains("second", log);
            // No error, not even for the group left open.
            Assert.Empty(errors);
        }

        [Fact]
        public void UnbalancedClipsAreReported()
        {
            var probe = MakeProbe();
            GUIProbe.Body = _ => GUI.BeginGroup(new Rect(0f, 0f, 10f, 10f));
            Frame();
            Assert.Contains("GUI Error: You are pushing more GUIClips than you are popping. Make sure they are balanced.", errors);
            errors.Clear();
        }

        [Fact]
        public void TabMovesKeyboardFocusThroughTheFields()
        {
            var probe = MakeProbe();
            string a = "one", b = "two";
            var focused = new List<string>();
            GUIProbe.Body = _ =>
            {
                GUI.SetNextControlName("a");
                a = GUI.TextField(new Rect(0f, 0f, 100f, 20f), a);
                GUI.SetNextControlName("b");
                b = GUI.TextField(new Rect(0f, 30f, 100f, 20f), b);
                if (Event.current.type == EventType.Repaint) focused.Add(GUI.GetNameOfFocusedControl());
            };
            Frame();
            Assert.Equal(0, GUIUtility.keyboardControl);
            // A press sends Tab, then its '\t' character; the character is used too.
            QueueKey(KeyCode.Tab);
            QueueKey(KeyCode.None, '\t');
            Frame();
            Assert.Equal("a", focused[^1]);
            QueueKey(KeyCode.Tab);
            QueueKey(KeyCode.None, '\t');
            Frame();
            Assert.Equal("b", focused[^1]);
            QueueKey(KeyCode.Tab, '\0', EventModifiers.Shift);
            QueueKey(KeyCode.None, '\t', EventModifiers.Shift);
            Frame();
            Assert.Equal("a", focused[^1]);
            Assert.Equal("one", a);
            Assert.Equal("two", b);
        }

        [Fact]
        public void ScrollViewsShowTheirContentAtTheScrollPosition()
        {
            var probe = MakeProbe();
            var position = new Rect(10f, 10f, 100f, 100f);
            var view = new Rect(0f, 0f, 80f, 400f);
            var scroll = new Vector2(0f, 50f);
            Vector2 mouse = default;
            Rect visible = default;
            GUIProbe.Body = _ =>
            {
                scroll = GUI.BeginScrollView(position, scroll, view);
                if (Event.current.type == EventType.MouseDown)
                {
                    mouse = Event.current.mousePosition;
                    visible = GUIClip.visibleRect;
                }
                GUI.EndScrollView();
            };
            Queue(EventType.MouseDown, new Vector2(20f, 20f));
            Frame();
            Assert.Equal(new Vector2(0f, 50f), scroll);
            Assert.Equal(new Vector2(10f, 60f), mouse);
            Assert.Equal(50f, visible.y);
            Assert.Equal(100f, visible.height);
            Assert.True(visible.width < 100f); // the vertical scrollbar takes room
            // The returned position is clamped to the content.
            scroll = new Vector2(30f, 1000f);
            Frame();
            Assert.Equal(new Vector2(0f, 300f), scroll);
            // The scroll wheel over the view scrolls it.
            scroll = new Vector2(0f, 50f);
            Frame();
            Event.Enqueue(new Event { type = EventType.ScrollWheel, mousePosition = new Vector2(50f, 50f), delta = new Vector2(0f, 3f) });
            Frame();
            Frame();
            Assert.True(scroll.y > 50f);
            // ScrollTo brings a rect of the content into view.
            GUIProbe.Body = _ =>
            {
                scroll = GUI.BeginScrollView(position, scroll, view);
                if (Event.current.type == EventType.Repaint) GUI.ScrollTo(new Rect(0f, 350f, 10f, 20f));
                GUI.EndScrollView();
            };
            Frame();
            Frame();
            Assert.Equal(270f, scroll.y);
        }

        [Fact]
        public void LayoutScrollViewsLayOutTheirContentAtItsOwnSize()
        {
            var probe = MakeProbe();
            var rects = new Rect[10];
            Rect viewArea = default;
            GUIProbe.Body = _ =>
            {
                GUILayout.BeginArea(new Rect(0f, 0f, 200f, 100f));
                GUILayout.BeginScrollView(Vector2.zero, GUIStyle.none, GUIStyle.none);
                for (int i = 0; i < rects.Length; i++)
                    rects[i] = GUILayoutUtility.GetRect(100f, 30f, GUILayout.Width(100f), GUILayout.Height(30f));
                GUILayout.EndScrollView();
                viewArea = GUILayoutUtility.GetLastRect();
                GUILayout.EndArea();
            };
            Frame();
            Assert.Equal(new Rect(0f, 90f, 100f, 30f), rects[3]);
            Assert.Equal(new Rect(0f, 270f, 100f, 30f), rects[9]);
            // The scroll view itself fills the area.
            Assert.Equal(new Rect(0f, 0f, 200f, 100f), viewArea);
        }

        [Fact]
        public void LayoutWindowsFitTheirContent()
        {
            var probe = MakeProbe();
            var style = new GUIStyle { padding = new RectOffset(5, 5, 20, 5) };
            var small = new Rect(10f, 10f, 0f, 0f);
            var large = new Rect(300f, 10f, 500f, 500f);
            var wide = new Rect(10f, 300f, 0f, 0f);
            Rect inner = default;
            GUIProbe.Body = _ =>
            {
                small = GUILayout.Window(1, small, id =>
                {
                    var r = GUILayoutUtility.GetRect(100f, 40f, GUILayout.Width(100f), GUILayout.Height(40f));
                    if (Event.current.type == EventType.Repaint) inner = r;
                }, "", style);
                large = GUILayout.Window(2, large, id => GUILayoutUtility.GetRect(100f, 40f, GUILayout.Width(100f), GUILayout.Height(40f)), "", style);
                wide = GUILayout.Window(3, wide, id => GUILayoutUtility.GetRect(100f, 40f, GUILayout.Width(100f), GUILayout.Height(40f)), "", style, GUILayout.Width(300f));
            };
            Frame();
            Frame();
            Assert.Equal(new Rect(10f, 10f, 110f, 65f), small);
            Assert.Equal(new Rect(300f, 10f, 110f, 65f), large);
            Assert.Equal(new Rect(10f, 300f, 300f, 65f), wide);
            Assert.Equal(new Rect(5f, 20f, 100f, 40f), inner);
        }

        [Fact]
        public void GetStyleFindsStylesByNameAndFallsBack()
        {
            var warnings = new List<string>();
            void OnWarning(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Warning) warnings.Add(message);
            }
            var skin = ScriptableObject.CreateInstance<GUISkin>();
            skin.name = "TestSkin";
            var mine = new GUIStyle { name = "Mine" };
            skin.customStyles = new[] { mine };
            Application.logMessageReceivedThreaded += OnWarning;
            try
            {
                Assert.Same(skin.button, skin.GetStyle("BUTTON"));
                Assert.Same(mine, skin.GetStyle("mine"));
                Assert.Same(mine, skin.FindStyle("MINE"));
                Assert.Null(skin.FindStyle("missing"));
                var fallback = skin.GetStyle("missing");
                Assert.NotNull(fallback);
                Assert.Contains(warnings, w => w.StartsWith("Unable to find style 'missing' in skin 'TestSkin'"));
                int count = 0;
                foreach (GUIStyle s in skin) count++;
                Assert.Equal(21, count);
            }
            finally
            {
                Application.logMessageReceivedThreaded -= OnWarning;
                UnityEngine.Object.DestroyImmediate(skin);
            }
            // A string converts to the current skin's style of that name.
            var probe = MakeProbe();
            GUIStyle converted = null, button = null;
            GUIProbe.Body = _ =>
            {
                converted = "Button";
                button = GUI.skin.button;
            };
            Frame();
            Assert.Same(button, converted);
        }

        [Fact]
        public void KeyboardEventParsesKeyStrings()
        {
            var e = Event.KeyboardEvent("^a");
            Assert.Equal(EventType.KeyDown, e.type);
            Assert.Equal(KeyCode.A, e.keyCode);
            Assert.True(e.control);
            Assert.False(e.shift);
            e = Event.KeyboardEvent("#home");
            Assert.Equal(KeyCode.Home, e.keyCode);
            Assert.True(e.shift);
            Assert.True(e.functionKey);
            e = Event.KeyboardEvent("&%f4");
            Assert.Equal(KeyCode.F4, e.keyCode);
            Assert.True(e.alt);
            Assert.True(e.command);
            e = Event.KeyboardEvent("[+]");
            Assert.Equal(KeyCode.KeypadPlus, e.keyCode);
            Assert.True(e.numeric);
            e = Event.KeyboardEvent("space");
            Assert.Equal(KeyCode.Space, e.keyCode);
            e = Event.KeyboardEvent("#");
            Assert.Equal(KeyCode.Hash, e.keyCode);
            Assert.Equal(EventModifiers.None, e.modifiers);
            Assert.Equal(Event.KeyboardEvent("^a"), Event.KeyboardEvent("^a"));
            Assert.NotEqual(Event.KeyboardEvent("^a"), Event.KeyboardEvent("a"));
        }
    }
}
