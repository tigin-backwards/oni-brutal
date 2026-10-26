using System;
using OniMoono.Engine.IMGUI;
using OniMoono.Engine.Platform;

namespace UnityEngine
{
    /// IMGUI services: control IDs, mouse and keyboard control, state objects, the clipboard,
    /// coordinate conversion and GUI.matrix helpers.
    public partial class GUIUtility
    {
        // ----- Control IDs -----

        public static int GetControlID(FocusType focus) => GetControlID(0, focus);

        public static int GetControlID(GUIContent contents, FocusType focus) => GetControlID(HintOf(contents), focus);

        public static int GetControlID(FocusType focus, Rect position) => GetControlID(0, focus);

        public static int GetControlID(GUIContent contents, FocusType focus, Rect position) => GetControlID(HintOf(contents), focus);

        public static int GetControlID(int hint, FocusType focus, Rect position) => GetControlID(hint, focus);

        /// The ID of the next control with this hint in the calling container: the same control
        /// gets the same ID from one event to the next.
        public static int GetControlID(int hint, FocusType focus)
        {
            GUIStateData.RequireGUI();
            return GUIStateData.Scope.Next(hint, focus);
        }

        static int HintOf(GUIContent contents) => (contents?.text ?? "").GetHashCode();

        /// The control holding the mouse; 0 is none.
        public static int hotControl
        {
            get => GUIStateData.HotControl;
            set => GUIStateData.HotControl = value;
        }

        /// The control with keyboard focus; 0 is none.
        public static int keyboardControl
        {
            get => GUIStateData.KeyboardControl;
            set => GUIStateData.KeyboardControl = value;
        }

        /// The object kept for a control ID, created with the type's parameterless constructor
        /// when it is missing or of another type.
        public static object GetStateObject(Type t, int controlID) => GUIStateData.GetStateObject(t, controlID);

        /// The object kept for a control ID, or null.
        public static object QueryStateObject(Type t, int controlID) => GUIStateData.QueryStateObject(t, controlID);

        /// The platform clipboard.
        public static string systemCopyBuffer
        {
            get => PlatformServices.Current.Clipboard ?? "";
            set => PlatformServices.Current.Clipboard = value ?? "";
        }

        /// Leaves the current OnGUI call (or window function) at once; the rest of it is
        /// skipped.
        public static void ExitGUI()
        {
            GUIStateData.Exiting = true;
            throw new ExitGUIException();
        }

        /// ExitGUI was called and the call it leaves has not ended yet.
        public static bool guiIsExiting => GUIStateData.Exiting;

        /// The player has no modal windows.
        public static bool hasModalWindow => GUIManager.HasModalWindow;

        /// GUI points are screen pixels in the player.
        internal static float pixelsPerPoint => 1f;

        // ----- Coordinates -----

        /// A point in the current clip's coordinates → screen pixels (top left origin), through
        /// the clip stack and GUI.matrix.
        public static Vector2 GUIToScreenPoint(Vector2 guiPoint) => ClipStack.LocalToScreen(guiPoint);

        public static Rect GUIToScreenRect(Rect guiRect)
        {
            var a = GUIToScreenPoint(guiRect.min);
            var b = GUIToScreenPoint(guiRect.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public static Vector2 ScreenToGUIPoint(Vector2 screenPoint) => ClipStack.ScreenToLocal(screenPoint);

        public static Rect ScreenToGUIRect(Rect screenRect)
        {
            var a = ScreenToGUIPoint(screenRect.min);
            var b = ScreenToGUIPoint(screenRect.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// Rotates the GUI by `angle` degrees around a point of the current clip.
        public static void RotateAroundPivot(float angle, Vector2 pivotPoint)
            => AroundPivot(Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle)), pivotPoint);

        /// Scales the GUI around a point of the current clip.
        public static void ScaleAroundPivot(Vector2 scale, Vector2 pivotPoint)
            => AroundPivot(Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1f)), pivotPoint);

        /// GUI.matrix × (the transform around the pivot): the pivot keeps its place on screen.
        static void AroundPivot(Matrix4x4 transform, Vector2 pivotPoint)
        {
            Vector3 pivot = ClipStack.Unclip(pivotPoint);
            GUI.matrix = GUIStateData.Matrix * Matrix4x4.Translate(pivot) * transform * Matrix4x4.Translate(-pivot);
        }
    }
}
