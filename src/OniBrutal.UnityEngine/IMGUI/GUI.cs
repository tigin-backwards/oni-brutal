using OniBrutal.Engine.IMGUI;

namespace UnityEngine
{
    /// Unity's immediate-mode GUI: controls placed at explicit rects, drawn on Repaint and
    /// reacting to the input events OnGUI receives.
    public partial class GUI
    {
        // ----- State -----

        /// The skin controls draw with; null restores the default skin.
        public static GUISkin skin
        {
            get
            {
                GUIStateData.RequireGUI();
                return GUIStateData.Skin;
            }
            set
            {
                GUIStateData.RequireGUI();
                GUIStateData.Skin = value != null ? value : DefaultSkin.Get();
            }
        }

        /// Tints everything the GUI draws.
        public static Color color
        {
            get => GUIStateData.Color;
            set => GUIStateData.Color = value;
        }

        /// Tints the backgrounds the GUI draws.
        public static Color backgroundColor
        {
            get => GUIStateData.BackgroundColor;
            set => GUIStateData.BackgroundColor = value;
        }

        /// Tints the text and images the GUI draws.
        public static Color contentColor
        {
            get => GUIStateData.ContentColor;
            set => GUIStateData.ContentColor = value;
        }

        /// While false, controls draw semi-transparent and take no input.
        public static bool enabled
        {
            get => GUIStateData.Enabled;
            set => GUIStateData.Enabled = value;
        }

        /// Whether a control changed its value since the call started (or since it was reset).
        public static bool changed
        {
            get => GUIStateData.Changed;
            set => GUIStateData.Changed = value;
        }

        /// The transform from GUI space to the screen. It must be invertible.
        public static Matrix4x4 matrix
        {
            get => GUIStateData.Matrix;
            set
            {
                if (value.determinant == 0f)
                {
                    Debug.LogError("GUI.matrix must be invertible; the assigned matrix is ignored.");
                    return;
                }
                GUIStateData.SetMatrix(value);
            }
        }

        /// The calling behaviour's depth: behaviours with lower depths are in front.
        public static int depth
        {
            get => GUIStateData.Participant?.Depth ?? 0;
            set
            {
                if (GUIStateData.Participant != null) GUIStateData.Participant.Depth = value;
            }
        }

        // ----- Control names and focus -----

        /// Names the next control (the next GetControlID call).
        public static void SetNextControlName(string name) => GUIStateData.NextControlName = name;

        /// The name of the control with keyboard focus, or "".
        public static string GetNameOfFocusedControl()
        {
            int id = GUIStateData.KeyboardControl;
            return ControlScope.OwnerOf(id)?.NameOf(id) ?? "";
        }

        /// Gives keyboard focus to the control of that name in the calling container.
        public static void FocusControl(string name)
        {
            int id = GUIStateData.Scope?.Named(name) ?? 0;
            GUIStateData.KeyboardControl = id;
        }

        // ----- Labels, boxes, textures -----

        public static void Label(Rect position, string text) => Label(position, GUIContent.Scratch(text), skin.label);
        public static void Label(Rect position, Texture image) => Label(position, GUIContent.Scratch(image), skin.label);
        public static void Label(Rect position, GUIContent content) => Label(position, content, skin.label);
        public static void Label(Rect position, string text, GUIStyle style) => Label(position, GUIContent.Scratch(text), style);
        public static void Label(Rect position, Texture image, GUIStyle style) => Label(position, GUIContent.Scratch(image), style);
        public static void Label(Rect position, GUIContent content, GUIStyle style) => Controls.Label(position, content, style);

        public static void Box(Rect position, string text) => Box(position, GUIContent.Scratch(text), skin.box);
        public static void Box(Rect position, Texture image) => Box(position, GUIContent.Scratch(image), skin.box);
        public static void Box(Rect position, GUIContent content) => Box(position, content, skin.box);
        public static void Box(Rect position, string text, GUIStyle style) => Box(position, GUIContent.Scratch(text), style);
        public static void Box(Rect position, Texture image, GUIStyle style) => Box(position, GUIContent.Scratch(image), style);
        public static void Box(Rect position, GUIContent content, GUIStyle style) => Controls.Label(position, content, style);

        public static void DrawTexture(Rect position, Texture image) => DrawTexture(position, image, ScaleMode.StretchToFill, true, 0f);
        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode) => DrawTexture(position, image, scaleMode, true, 0f);
        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend) => DrawTexture(position, image, scaleMode, alphaBlend, 0f);

        /// Draws a texture placed by `scaleMode`, tinted by GUI.color.
        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect)
            => Controls.DrawTexture(position, image, scaleMode, alphaBlend, imageAspect, GUIStateData.Color);

        public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords) => DrawTextureWithTexCoords(position, image, texCoords, true);

        /// Draws the part `texCoords` (normalized, origin bottom left) of a texture.
        public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords, bool alphaBlend)
            => Controls.DrawTextureWithTexCoords(position, image, texCoords, alphaBlend, GUIStateData.Color);

        // ----- Buttons and toggles -----

        public static bool Button(Rect position, string text) => Button(position, GUIContent.Scratch(text), skin.button);
        public static bool Button(Rect position, Texture image) => Button(position, GUIContent.Scratch(image), skin.button);
        public static bool Button(Rect position, GUIContent content) => Button(position, content, skin.button);
        public static bool Button(Rect position, string text, GUIStyle style) => Button(position, GUIContent.Scratch(text), style);
        public static bool Button(Rect position, Texture image, GUIStyle style) => Button(position, GUIContent.Scratch(image), style);
        public static bool Button(Rect position, GUIContent content, GUIStyle style) => Controls.Button(position, content, style);

        public static bool RepeatButton(Rect position, string text) => RepeatButton(position, GUIContent.Scratch(text), skin.button);
        public static bool RepeatButton(Rect position, Texture image) => RepeatButton(position, GUIContent.Scratch(image), skin.button);
        public static bool RepeatButton(Rect position, GUIContent content) => RepeatButton(position, content, skin.button);
        public static bool RepeatButton(Rect position, string text, GUIStyle style) => RepeatButton(position, GUIContent.Scratch(text), style);
        public static bool RepeatButton(Rect position, Texture image, GUIStyle style) => RepeatButton(position, GUIContent.Scratch(image), style);
        public static bool RepeatButton(Rect position, GUIContent content, GUIStyle style) => Controls.RepeatButton(position, content, style);

        public static bool Toggle(Rect position, bool value, string text) => Toggle(position, value, GUIContent.Scratch(text), skin.toggle);
        public static bool Toggle(Rect position, bool value, Texture image) => Toggle(position, value, GUIContent.Scratch(image), skin.toggle);
        public static bool Toggle(Rect position, bool value, GUIContent content) => Toggle(position, value, content, skin.toggle);
        public static bool Toggle(Rect position, bool value, string text, GUIStyle style) => Toggle(position, value, GUIContent.Scratch(text), style);
        public static bool Toggle(Rect position, bool value, Texture image, GUIStyle style) => Toggle(position, value, GUIContent.Scratch(image), style);
        public static bool Toggle(Rect position, bool value, GUIContent content, GUIStyle style) => Controls.Toggle(position, value, content, style);

        // ----- Text -----

        public static string TextField(Rect position, string text) => TextFields.Edit(position, text, -1, skin.textField, false);
        public static string TextField(Rect position, string text, int maxLength) => TextFields.Edit(position, text, maxLength, skin.textField, false);
        public static string TextField(Rect position, string text, GUIStyle style) => TextFields.Edit(position, text, -1, style, false);
        public static string TextField(Rect position, string text, int maxLength, GUIStyle style) => TextFields.Edit(position, text, maxLength, style, false);

        public static string TextArea(Rect position, string text) => TextFields.Edit(position, text, -1, skin.textArea, true);
        public static string TextArea(Rect position, string text, int maxLength) => TextFields.Edit(position, text, maxLength, skin.textArea, true);
        public static string TextArea(Rect position, string text, GUIStyle style) => TextFields.Edit(position, text, -1, style, true);
        public static string TextArea(Rect position, string text, int maxLength, GUIStyle style) => TextFields.Edit(position, text, maxLength, style, true);

        // ----- Sliders and scrollbars -----

        public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue)
            => HorizontalSlider(position, value, leftValue, rightValue, skin.horizontalSlider, skin.horizontalSliderThumb);

        public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb)
        {
            GUIStateData.RequireGUI();
            int id = GUIUtility.GetControlID(ControlHints.Slider, FocusType.Passive, position);
            return Controls.Slider(position, value, 0f, leftValue, rightValue, slider, thumb, true, id);
        }

        public static float VerticalSlider(Rect position, float value, float topValue, float bottomValue)
            => VerticalSlider(position, value, topValue, bottomValue, skin.verticalSlider, skin.verticalSliderThumb);

        public static float VerticalSlider(Rect position, float value, float topValue, float bottomValue, GUIStyle slider, GUIStyle thumb)
        {
            GUIStateData.RequireGUI();
            int id = GUIUtility.GetControlID(ControlHints.Slider, FocusType.Passive, position);
            return Controls.Slider(position, value, 0f, topValue, bottomValue, slider, thumb, false, id);
        }

        public static float HorizontalScrollbar(Rect position, float value, float size, float leftValue, float rightValue)
            => HorizontalScrollbar(position, value, size, leftValue, rightValue, skin.horizontalScrollbar);

        public static float HorizontalScrollbar(Rect position, float value, float size, float leftValue, float rightValue, GUIStyle style)
            => Controls.Scrollbar(position, value, size, leftValue, rightValue, style, true);

        public static float VerticalScrollbar(Rect position, float value, float size, float topValue, float bottomValue)
            => VerticalScrollbar(position, value, size, topValue, bottomValue, skin.verticalScrollbar);

        public static float VerticalScrollbar(Rect position, float value, float size, float topValue, float bottomValue, GUIStyle style)
            => Controls.Scrollbar(position, value, size, topValue, bottomValue, style, false);
    }
}
