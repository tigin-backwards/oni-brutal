using UnityEngine;

namespace OniBrutal.Engine.IMGUI
{
    /// A window a behaviour's OnGUI declared with GUI.Window or GUILayout.Window. It keeps the
    /// state in effect at the call (skin, colours, enabled, matrix), its own control IDs and
    /// GUILayout entries, and its rect, which DragWindow moves.
    internal sealed class WindowRecord
    {
        public readonly int Id;
        public readonly GUIParticipant Owner;
        public readonly ControlScope Scope;
        public readonly LayoutPass Layout = new();
        public Rect Rect;
        /// DragWindow moved it since the last GUI.Window call returned its rect.
        public bool Moved;
        /// Its behaviour called GUI.Window for it since the last prune.
        public bool Touched;
        public GUI.WindowFunction Function;
        public GUIContent Title = new();
        public GUIStyle Style = GUIStyle.none;
        /// GUILayout.Window: sized to its content.
        public bool Fitted;
        public GUILayoutOption[] Options;
        /// The size the last GUILayout.Window call asked for.
        public Vector2 Requested;
        public GUISkin Skin;
        public Color Color, BackgroundColor, ContentColor;
        public bool Enabled;
        public Matrix4x4 Matrix = Matrix4x4.identity;

        public WindowRecord(int id, GUIParticipant owner)
        {
            Id = id;
            Owner = owner;
            Scope = new ControlScope(this);
        }

        /// Whether a point in screen GUI space is inside the window.
        public bool Contains(Vector2 screen) => Rect.Contains(Matrix.isIdentity ? screen : (Vector2)Matrix.inverse.MultiplyPoint3x4(screen));
    }

    /// GUI.Window and the window functions.
    internal static class Windows
    {
        /// GUI.Window / GUILayout.Window: records the call and returns the window's rect, moved
        /// by dragging since the last call, otherwise `rect` (a fitted window gets the size its
        /// content allows).
        public static Rect Declare(int id, Rect rect, GUI.WindowFunction func, GUIContent title, GUIStyle style, bool fitted, GUILayoutOption[] options)
        {
            GUIStateData.RequireGUI();
            var p = GUIStateData.Participant;
            if (func == null) throw new System.ArgumentNullException(nameof(func));
            if (p == null) return rect;
            var w = p.FindWindow(id);
            if (w == null)
            {
                w = new WindowRecord(id, p);
                p.Windows.Insert(0, w);
                w.Rect = rect;
            }
            w.Touched = true;
            w.Function = func;
            w.Title.CopyFrom(title ?? GUIContent.none);
            w.Style = style ?? GUIStyle.none;
            w.Fitted = fitted;
            w.Options = options;
            w.Skin = GUIStateData.Skin;
            w.Color = GUIStateData.Color;
            w.BackgroundColor = GUIStateData.BackgroundColor;
            w.ContentColor = GUIStateData.ContentColor;
            w.Enabled = GUIStateData.Enabled;
            w.Matrix = GUIStateData.Matrix;
            if (fitted) w.Requested = rect.size;
            if (w.Moved)
            {
                w.Moved = false;
                return w.Rect;
            }
            // A fitted window gets the size its content allowed at its last layout.
            w.Rect = fitted ? new Rect(rect.position, w.Layout.Fit(rect.size)) : rect;
            return w.Rect;
        }

        /// Runs a window's function for one event: the background (on Repaint), then the
        /// function inside a clip at the window's rect, with the state of its GUI.Window call.
        public static void Call(WindowRecord w, Event e)
        {
            var p = w.Owner;
            var previous = Event.current;
            Event.current = e;
            GUIStateData.InGUI = true;
            GUIStateData.InWindow = true;
            GUIStateData.Participant = p;
            GUIStateData.Window = w;
            GUIStateData.Scope = w.Scope;
            GUIStateData.BeginCall();
            if (w.Skin != null) GUIStateData.Skin = w.Skin;
            GUIStateData.Color = w.Color;
            GUIStateData.BackgroundColor = w.BackgroundColor;
            GUIStateData.ContentColor = w.ContentColor;
            GUIStateData.Enabled = w.Enabled;
            GUIStateData.SetMatrix(w.Matrix);
            w.Scope.BeginPass();
            ClipStack.Clear();
            ScrollViews.Reset();
            try
            {
                if (e.rawType == EventType.Repaint)
                    w.Style.Draw(w.Rect, w.Title, w.Rect.Contains(e.mousePosition), false, p.FocusedWindow == w, false);
                ClipStack.PushAbsolute(w.Rect);
                bool layoutEvent = e.rawType == EventType.Layout;
                if (p.UsesLayout)
                {
                    if (layoutEvent) w.Layout.BeginRecord(new Rect(0f, 0f, w.Rect.width, w.Rect.height), w.Style, w.Fitted ? w.Options : null);
                    else w.Layout.BeginReplay();
                    GUIStateData.Layout = w.Layout;
                }
                else
                {
                    GUIStateData.Layout = null;
                }
                bool completed = GUIManager.Invoke(p, w);
                if (completed && ClipStack.Count > 1)
                    Debug.LogError("GUI Error: You are pushing more GUIClips than you are popping. Make sure they are balanced.");
                if (p.UsesLayout && layoutEvent)
                {
                    if (w.Fitted) w.Rect.size = w.Layout.ComputeFitted(w.Requested);
                    else w.Layout.Compute();
                }
            }
            finally
            {
                GUIManager.EndCall(previous);
            }
        }

        /// GUI.DragWindow: a press in `position` (window coordinates) and a drag move the
        /// window by the mouse movement.
        public static void Drag(Rect position)
        {
            var w = GUIStateData.Window;
            if (w == null)
            {
                Debug.LogError("Dragwindow can only be called within a window callback");
                return;
            }
            var e = Event.current;
            int id = GUIUtility.GetControlID(ControlHints.DragWindow, FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (position.Contains(e.mousePosition))
                    {
                        GUIStateData.HotControl = id;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIStateData.HotControl == id)
                    {
                        // The window's rect is in GUI space; its matrix scales the movement.
                        var move = w.Matrix.isIdentity ? e.ScreenDelta : (Vector2)w.Matrix.inverse.MultiplyVector(e.ScreenDelta);
                        w.Rect.position += move;
                        w.Moved = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIStateData.HotControl == id)
                    {
                        GUIStateData.HotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }
    }
}
