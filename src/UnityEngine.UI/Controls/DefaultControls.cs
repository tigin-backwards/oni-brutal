using System;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// Builds the standard legacy controls from code: each Create method returns a new root
    /// GameObject with the control's hierarchy, components and settings below it, as measured in
    /// the original player (docs/cleanroom/ugui.md, DefaultControls). Every object is made through
    /// `factory`.
    public static partial class DefaultControls
    {
        /// Makes a plain GameObject with the given components added in order.
        sealed class PlainFactory : IFactoryControls
        {
            public GameObject CreateGameObject(string name, params Type[] components) => new GameObject(name, components);
        }

        static readonly IFactoryControls plainFactory = new PlainFactory();

        public static IFactoryControls factory => plainFactory;

        static readonly Color32 textColor = new Color32(50, 50, 50, 255);
        static readonly Color32 panelColor = new Color32(255, 255, 255, 100);

        static readonly Vector2 centre = new Vector2(0.5f, 0.5f);

        static Font builtinFont;

        static Font BuiltinFont
        {
            get
            {
                if (builtinFont == null) builtinFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return builtinFont;
            }
        }

        // ----- Building blocks -----

        /// An object with a RectTransform and the given components, under `parent` when there is one.
        static GameObject Node(string name, GameObject parent, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var go = factory.CreateGameObject(name, types);
            var renderer = go.GetComponent<CanvasRenderer>();
            if (renderer != null) renderer.cullTransparentMesh = true;
            if (parent != null) go.transform.SetParent(parent.transform, false);
            return go;
        }

        /// An object that draws an Image (with its CanvasRenderer), then the extra components.
        static GameObject ImageNode(string name, GameObject parent, params Type[] extra)
        {
            var types = new Type[extra.Length + 2];
            types[0] = typeof(CanvasRenderer);
            types[1] = typeof(Image);
            Array.Copy(extra, 0, types, 2, extra.Length);
            return Node(name, parent, types);
        }

        static RectTransform Place(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position = default, Vector2? pivot = null)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? centre;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        static RectTransform Centred(GameObject go, float width, float height) => Place(go, centre, centre, new Vector2(width, height));

        static RectTransform Stretched(GameObject go, Vector2 size, Vector2 position = default, Vector2? pivot = null)
            => Place(go, Vector2.zero, Vector2.one, size, position, pivot);

        static Image Paint(GameObject go, Sprite sprite, Image.Type type)
        {
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            return image;
        }

        static Text Label(GameObject parent, string name, string content, TextAnchor alignment)
        {
            var text = Node(name, parent, typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.font = BuiltinFont;
            text.color = textColor;
            text.text = content;
            text.alignment = alignment;
            return text;
        }

        /// A scrollbar with its sliding area and handle; the caller places the root.
        static Scrollbar Bar(string name, GameObject parent, Resources resources, Scrollbar.Direction direction)
        {
            var root = ImageNode(name, parent, typeof(Scrollbar));
            Paint(root, resources.background, Image.Type.Sliced);
            var area = Node("Sliding Area", root);
            Stretched(area, new Vector2(-20f, -20f));
            var handle = ImageNode("Handle", area);
            bool vertical = direction == Scrollbar.Direction.BottomToTop || direction == Scrollbar.Direction.TopToBottom;
            Place(handle, Vector2.zero, vertical ? new Vector2(1f, 0.2f) : new Vector2(0.2f, 1f), new Vector2(20f, 20f));
            var handleImage = Paint(handle, resources.standard, Image.Type.Sliced);

            var bar = root.GetComponent<Scrollbar>();
            bar.handleRect = (RectTransform)handle.transform;
            bar.targetGraphic = handleImage;
            bar.direction = direction;
            bar.size = 0.2f;
            bar.value = 0f;
            bar.numberOfSteps = 0;
            return bar;
        }

        // ----- Controls -----

        public static GameObject CreatePanel(Resources resources)
        {
            var root = ImageNode("Panel", null);
            Stretched(root, Vector2.zero);
            var image = Paint(root, resources.background, Image.Type.Sliced);
            image.color = panelColor;
            return root;
        }

        public static GameObject CreateButton(Resources resources)
        {
            var root = ImageNode("Button (Legacy)", null, typeof(Button));
            Centred(root, 160f, 30f);
            var image = Paint(root, resources.standard, Image.Type.Sliced);
            root.GetComponent<Button>().targetGraphic = image;

            var label = Label(root, "Text (Legacy)", "Button", TextAnchor.MiddleCenter);
            Stretched(label.gameObject, Vector2.zero);
            return root;
        }

        public static GameObject CreateText(Resources resources)
        {
            var root = Label(null, "Text (Legacy)", "New Text", TextAnchor.UpperLeft).gameObject;
            Centred(root, 160f, 30f);
            return root;
        }

        public static GameObject CreateImage(Resources resources)
        {
            var root = ImageNode("Image", null);
            Centred(root, 100f, 100f);
            return root;
        }

        public static GameObject CreateRawImage(Resources resources)
        {
            var root = Node("RawImage", null, typeof(CanvasRenderer), typeof(RawImage));
            Centred(root, 100f, 100f);
            return root;
        }

        public static GameObject CreateSlider(Resources resources)
        {
            var root = Node("Slider", null, typeof(Slider));
            Centred(root, 160f, 20f);

            var background = ImageNode("Background", root);
            Place(background, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Vector2.zero);
            Paint(background, resources.background, Image.Type.Sliced);

            var fillArea = Node("Fill Area", root);
            Place(fillArea, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(-20f, 0f), new Vector2(-5f, 0f));
            var fill = ImageNode("Fill", fillArea);
            Place(fill, Vector2.zero, new Vector2(0f, 1f), new Vector2(10f, 0f));
            Paint(fill, resources.standard, Image.Type.Sliced);

            var handleArea = Node("Handle Slide Area", root);
            Stretched(handleArea, new Vector2(-20f, 0f));
            var handle = ImageNode("Handle", handleArea);
            Place(handle, Vector2.zero, new Vector2(0f, 1f), new Vector2(20f, 0f));
            var handleImage = Paint(handle, resources.knob, Image.Type.Simple);

            var slider = root.GetComponent<Slider>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return root;
        }

        public static GameObject CreateScrollbar(Resources resources)
        {
            var bar = Bar("Scrollbar", null, resources, Scrollbar.Direction.LeftToRight);
            Centred(bar.gameObject, 160f, 20f);
            return bar.gameObject;
        }

        public static GameObject CreateToggle(Resources resources)
        {
            var root = Node("Toggle", null, typeof(Toggle));
            Centred(root, 160f, 20f);

            var background = ImageNode("Background", root);
            Place(background, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(10f, -10f));
            var backgroundImage = Paint(background, resources.standard, Image.Type.Sliced);

            var checkmark = ImageNode("Checkmark", background);
            Centred(checkmark, 20f, 20f);
            var checkmarkImage = Paint(checkmark, resources.checkmark, Image.Type.Simple);

            var label = Label(root, "Label", "Toggle", TextAnchor.UpperLeft);
            Stretched(label.gameObject, new Vector2(-28f, -3f), new Vector2(9f, -0.5f));

            var toggle = root.GetComponent<Toggle>();
            toggle.isOn = true;
            toggle.graphic = checkmarkImage;
            toggle.targetGraphic = backgroundImage;
            toggle.toggleTransition = Toggle.ToggleTransition.Fade;
            return root;
        }

        public static GameObject CreateInputField(Resources resources)
        {
            var root = ImageNode("InputField (Legacy)", null, typeof(InputField));
            Centred(root, 160f, 30f);
            var image = Paint(root, resources.inputField, Image.Type.Sliced);

            var placeholder = Label(root, "Placeholder", "Enter text...", TextAnchor.UpperLeft);
            Stretched(placeholder.gameObject, new Vector2(-20f, -13f), new Vector2(0f, -0.5f));
            placeholder.font = null;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color32(50, 50, 50, 128);

            var text = Label(root, "Text (Legacy)", "", TextAnchor.UpperLeft);
            Stretched(text.gameObject, new Vector2(-20f, -13f), new Vector2(0f, -0.5f));
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var field = root.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = image;
            return root;
        }

        public static GameObject CreateDropdown(Resources resources)
        {
            var root = ImageNode("Dropdown (Legacy)", null, typeof(Dropdown));
            Centred(root, 160f, 30f);
            var image = Paint(root, resources.standard, Image.Type.Sliced);

            var label = Label(root, "Label", "Option A", TextAnchor.MiddleLeft);
            Stretched(label.gameObject, new Vector2(-35f, -13f), new Vector2(-7.5f, -0.5f));

            var arrow = ImageNode("Arrow", root);
            Place(arrow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, 20f), new Vector2(-15f, 0f));
            Paint(arrow, resources.dropdown, Image.Type.Simple);

            // The list template: a vertical scroll view of one item.
            var template = ImageNode("Template", root, typeof(ScrollRect));
            Place(template, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 150f), new Vector2(0f, 2f), new Vector2(0.5f, 1f));
            Paint(template, resources.standard, Image.Type.Sliced);

            var viewport = ImageNode("Viewport", template, typeof(Mask));
            Stretched(viewport, new Vector2(-18f, 0f), default, new Vector2(0f, 1f));
            Paint(viewport, resources.mask, Image.Type.Sliced);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = Node("Content", viewport);
            Place(content, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 28f), default, new Vector2(0.5f, 1f));

            var item = Node("Item", content, typeof(Toggle));
            Place(item, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 20f));

            var itemBackground = ImageNode("Item Background", item);
            Stretched(itemBackground, Vector2.zero);
            var itemBackgroundImage = Paint(itemBackground, null, Image.Type.Simple);
            itemBackgroundImage.color = new Color32(245, 245, 245, 255);

            var itemCheckmark = ImageNode("Item Checkmark", item);
            Place(itemCheckmark, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 20f), new Vector2(10f, 0f));
            var itemCheckmarkImage = Paint(itemCheckmark, resources.checkmark, Image.Type.Simple);

            var itemLabel = Label(item, "Item Label", "Option A", TextAnchor.MiddleLeft);
            Stretched(itemLabel.gameObject, new Vector2(-30f, -3f), new Vector2(5f, -0.5f));

            var itemToggle = item.GetComponent<Toggle>();
            itemToggle.isOn = true;
            itemToggle.graphic = itemCheckmarkImage;
            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.toggleTransition = Toggle.ToggleTransition.Fade;

            var bar = Bar("Scrollbar", template, resources, Scrollbar.Direction.BottomToTop);
            Place(bar.gameObject, new Vector2(1f, 0f), Vector2.one, new Vector2(20f, 0f), default, Vector2.one);

            var scroll = template.GetComponent<ScrollRect>();
            scroll.content = (RectTransform)content.transform;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalScrollbar = bar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = -3f;
            scroll.horizontalScrollbarSpacing = 0f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 1f;

            template.SetActive(false);

            var dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.template = (RectTransform)template.transform;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Option A"),
                new Dropdown.OptionData("Option B"),
                new Dropdown.OptionData("Option C"),
            };
            dropdown.value = 0;
            dropdown.alphaFadeSpeed = 0.15f;
            return root;
        }

        public static GameObject CreateScrollView(Resources resources)
        {
            var root = ImageNode("Scroll View", null, typeof(ScrollRect));
            Centred(root, 200f, 200f);
            var image = Paint(root, resources.background, Image.Type.Sliced);
            image.color = panelColor;

            var viewport = ImageNode("Viewport", root, typeof(Mask));
            Stretched(viewport, Vector2.zero, default, new Vector2(0f, 1f));
            Paint(viewport, resources.mask, Image.Type.Sliced);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = Node("Content", viewport);
            Place(content, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 300f), default, new Vector2(0f, 1f));

            var horizontal = Bar("Scrollbar Horizontal", root, resources, Scrollbar.Direction.LeftToRight);
            Place(horizontal.gameObject, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 20f), default, Vector2.zero);

            var vertical = Bar("Scrollbar Vertical", root, resources, Scrollbar.Direction.BottomToTop);
            Place(vertical.gameObject, new Vector2(1f, 0f), Vector2.one, new Vector2(20f, 0f), default, Vector2.one);

            var scroll = root.GetComponent<ScrollRect>();
            scroll.content = (RectTransform)content.transform;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.horizontalScrollbar = horizontal;
            scroll.verticalScrollbar = vertical;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.horizontalScrollbarSpacing = -3f;
            scroll.verticalScrollbarSpacing = -3f;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 1f;
            return root;
        }
    }
}
