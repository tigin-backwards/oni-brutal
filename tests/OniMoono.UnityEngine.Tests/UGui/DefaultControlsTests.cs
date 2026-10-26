using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace OniMoono.Tests.UGui
{
    /// DefaultControls' hierarchies, as measured in the original player (docs/cleanroom/ugui.md).
    public class DefaultControlsTests : UGuiTest
    {
        public DefaultControlsTests() => UseGameAssets();

        /// Each sprite named after its field, so a test can see which one landed where.
        static DefaultControls.Resources Named()
        {
            Sprite S(string name) { var s = NewSprite(8, 8); s.name = name; return s; }
            return new DefaultControls.Resources
            {
                standard = S("standard"), background = S("background"), inputField = S("inputField"), knob = S("knob"),
                checkmark = S("checkmark"), dropdown = S("dropdown"), mask = S("mask"),
            };
        }

        static Transform Child(Transform t, params string[] path)
        {
            foreach (var name in path)
            {
                var next = t.Find(name);
                Assert.True(next != null, $"{t.name} has no child {name}");
                t = next;
            }
            return t;
        }

        static string[] Children(Transform t) => Enumerable.Range(0, t.childCount).Select(i => t.GetChild(i).name).ToArray();

        static Type[] Components(Transform t) => t.GetComponents<Component>().Select(c => c.GetType()).ToArray();

        static void Rect(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            var rt = (RectTransform)t;
            Assert.Equal(anchorMin, rt.anchorMin);
            Assert.Equal(anchorMax, rt.anchorMax);
            Assert.Equal(pivot, rt.pivot);
            Assert.Equal(size, rt.sizeDelta);
            Assert.Equal(position, rt.anchoredPosition);
        }

        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        static void Drawn(Transform t, Type graphic, params Type[] more)
        {
            Assert.Equal(new[] { typeof(RectTransform), typeof(CanvasRenderer), graphic }.Concat(more).ToArray(), Components(t));
            Assert.True(t.GetComponent<CanvasRenderer>().cullTransparentMesh);
        }

        static void Painted(Transform t, string sprite, Image.Type type)
        {
            var image = t.GetComponent<Image>();
            Assert.Equal(sprite, image.sprite == null ? null : image.sprite.name);
            Assert.Equal(type, image.type);
        }

        static void Written(Transform t, string text, TextAnchor alignment)
        {
            var label = t.GetComponent<Text>();
            Assert.Equal(text, label.text);
            Assert.Equal(alignment, label.alignment);
            Assert.Equal((Color)new Color32(50, 50, 50, 255), label.color);
            Assert.Equal(14, label.fontSize);
            Assert.NotNull(label.font);
            Assert.Same(UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), label.font);
        }

        [Fact]
        public void FactoryAddsTheComponentsInOrder()
        {
            var go = DefaultControls.factory.CreateGameObject("Thing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Assert.Equal("Thing", go.name);
            Assert.Equal(new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) }, Components(go.transform));
        }

        [Fact]
        public void DropdownHierarchyAndSettings()
        {
            var root = DefaultControls.CreateDropdown(Named()).transform;
            Assert.Equal("Dropdown (Legacy)", root.name);
            Assert.Null(root.parent);
            Assert.Equal(0, root.gameObject.layer);
            Drawn(root, typeof(Image), typeof(Dropdown));
            Rect(root, Mid, Mid, Mid, new Vector2(160, 30), Vector2.zero);
            Painted(root, "standard", Image.Type.Sliced);
            Assert.Equal(new[] { "Label", "Arrow", "Template" }, Children(root));

            var label = Child(root, "Label");
            Drawn(label, typeof(Text));
            Rect(label, Vector2.zero, Vector2.one, Mid, new Vector2(-35, -13), new Vector2(-7.5f, -0.5f));
            Written(label, "Option A", TextAnchor.MiddleLeft);

            var arrow = Child(root, "Arrow");
            Rect(arrow, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Mid, new Vector2(20, 20), new Vector2(-15, 0));
            Painted(arrow, "dropdown", Image.Type.Simple);

            var template = Child(root, "Template");
            Assert.False(template.gameObject.activeSelf);
            Drawn(template, typeof(Image), typeof(ScrollRect));
            Rect(template, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, 150), new Vector2(0, 2));
            Painted(template, "standard", Image.Type.Sliced);
            Assert.Equal(new[] { "Viewport", "Scrollbar" }, Children(template));

            var viewport = Child(template, "Viewport");
            Drawn(viewport, typeof(Image), typeof(Mask));
            Rect(viewport, Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(-18, 0), Vector2.zero);
            Painted(viewport, "mask", Image.Type.Sliced);
            Assert.False(viewport.GetComponent<Mask>().showMaskGraphic);

            var content = Child(viewport, "Content");
            Assert.Equal(new[] { typeof(RectTransform) }, Components(content));
            Rect(content, new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), new Vector2(0, 28), Vector2.zero);

            var item = Child(content, "Item");
            Assert.Equal(new[] { typeof(RectTransform), typeof(Toggle) }, Components(item));
            Rect(item, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Mid, new Vector2(0, 20), Vector2.zero);
            Assert.Equal(new[] { "Item Background", "Item Checkmark", "Item Label" }, Children(item));
            var itemBackground = Child(item, "Item Background");
            Painted(itemBackground, null, Image.Type.Simple);
            Assert.Equal((Color)new Color32(245, 245, 245, 255), itemBackground.GetComponent<Image>().color);
            var itemCheckmark = Child(item, "Item Checkmark");
            Rect(itemCheckmark, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Mid, new Vector2(20, 20), new Vector2(10, 0));
            Painted(itemCheckmark, "checkmark", Image.Type.Simple);
            var itemLabel = Child(item, "Item Label");
            Rect(itemLabel, Vector2.zero, Vector2.one, Mid, new Vector2(-30, -3), new Vector2(5, -0.5f));
            Written(itemLabel, "Option A", TextAnchor.MiddleLeft);
            var toggle = item.GetComponent<Toggle>();
            Assert.True(toggle.isOn);
            Assert.Same(itemCheckmark.GetComponent<Image>(), toggle.graphic);
            Assert.Same(itemBackground.GetComponent<Image>(), toggle.targetGraphic);
            Assert.Equal(Toggle.ToggleTransition.Fade, toggle.toggleTransition);

            var bar = Child(template, "Scrollbar");
            Drawn(bar, typeof(Image), typeof(Scrollbar));
            Rect(bar, new Vector2(1, 0), Vector2.one, Vector2.one, new Vector2(20, 0), Vector2.zero);
            Painted(bar, "background", Image.Type.Sliced);
            var handle = Child(bar, "Sliding Area", "Handle");
            Rect(Child(bar, "Sliding Area"), Vector2.zero, Vector2.one, Mid, new Vector2(-20, -20), Vector2.zero);
            Rect(handle, Vector2.zero, new Vector2(1, 0.2f), Mid, new Vector2(20, 20), Vector2.zero);
            Painted(handle, "standard", Image.Type.Sliced);
            var scrollbar = bar.GetComponent<Scrollbar>();
            Assert.Equal(Scrollbar.Direction.BottomToTop, scrollbar.direction);
            Assert.Equal(0.2f, scrollbar.size);
            Assert.Same(handle, scrollbar.handleRect);

            var scroll = template.GetComponent<ScrollRect>();
            Assert.Same(content, scroll.content);
            Assert.Same(viewport, scroll.viewport);
            Assert.False(scroll.horizontal);
            Assert.True(scroll.vertical);
            Assert.Same(scrollbar, scroll.verticalScrollbar);
            Assert.Null(scroll.horizontalScrollbar);
            Assert.Equal(ScrollRect.ScrollbarVisibility.Permanent, scroll.horizontalScrollbarVisibility);
            Assert.Equal(ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport, scroll.verticalScrollbarVisibility);
            Assert.Equal(-3f, scroll.verticalScrollbarSpacing);
            Assert.Equal(ScrollRect.MovementType.Clamped, scroll.movementType);

            var dropdown = root.GetComponent<Dropdown>();
            Assert.Same(template, dropdown.template);
            Assert.Same(label.GetComponent<Text>(), dropdown.captionText);
            Assert.Same(itemLabel.GetComponent<Text>(), dropdown.itemText);
            Assert.Null(dropdown.captionImage);
            Assert.Null(dropdown.itemImage);
            Assert.Equal(new[] { "Option A", "Option B", "Option C" }, dropdown.options.Select(o => o.text).ToArray());
            Assert.Equal(0, dropdown.value);
            Assert.Same(root.GetComponent<Image>(), dropdown.targetGraphic);
            Assert.Equal(0.15f, dropdown.alphaFadeSpeed);
        }

        [Fact]
        public void ScrollViewHierarchyAndSettings()
        {
            var root = DefaultControls.CreateScrollView(Named()).transform;
            Assert.Equal("Scroll View", root.name);
            Drawn(root, typeof(Image), typeof(ScrollRect));
            Rect(root, Mid, Mid, Mid, new Vector2(200, 200), Vector2.zero);
            Painted(root, "background", Image.Type.Sliced);
            Assert.Equal((Color)new Color32(255, 255, 255, 100), root.GetComponent<Image>().color);
            Assert.Equal(new[] { "Viewport", "Scrollbar Horizontal", "Scrollbar Vertical" }, Children(root));

            var viewport = Child(root, "Viewport");
            Drawn(viewport, typeof(Image), typeof(Mask));
            Rect(viewport, Vector2.zero, Vector2.one, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            Painted(viewport, "mask", Image.Type.Sliced);
            var content = Child(viewport, "Content");
            Rect(content, new Vector2(0, 1), Vector2.one, new Vector2(0, 1), new Vector2(0, 300), Vector2.zero);

            var horizontal = Child(root, "Scrollbar Horizontal");
            Rect(horizontal, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 20), Vector2.zero);
            Painted(horizontal, "background", Image.Type.Sliced);
            Rect(Child(horizontal, "Sliding Area", "Handle"), Vector2.zero, new Vector2(0.2f, 1), Mid, new Vector2(20, 20), Vector2.zero);
            Painted(Child(horizontal, "Sliding Area", "Handle"), "standard", Image.Type.Sliced);
            Assert.Equal(Scrollbar.Direction.LeftToRight, horizontal.GetComponent<Scrollbar>().direction);

            var vertical = Child(root, "Scrollbar Vertical");
            Rect(vertical, new Vector2(1, 0), Vector2.one, Vector2.one, new Vector2(20, 0), Vector2.zero);
            Rect(Child(vertical, "Sliding Area", "Handle"), Vector2.zero, new Vector2(1, 0.2f), Mid, new Vector2(20, 20), Vector2.zero);
            Assert.Equal(Scrollbar.Direction.BottomToTop, vertical.GetComponent<Scrollbar>().direction);

            var scroll = root.GetComponent<ScrollRect>();
            Assert.Same(content, scroll.content);
            Assert.Same(viewport, scroll.viewport);
            Assert.True(scroll.horizontal && scroll.vertical);
            Assert.Same(horizontal.GetComponent<Scrollbar>(), scroll.horizontalScrollbar);
            Assert.Same(vertical.GetComponent<Scrollbar>(), scroll.verticalScrollbar);
            Assert.Equal(ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport, scroll.horizontalScrollbarVisibility);
            Assert.Equal(ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport, scroll.verticalScrollbarVisibility);
            Assert.Equal(-3f, scroll.horizontalScrollbarSpacing);
            Assert.Equal(-3f, scroll.verticalScrollbarSpacing);
            Assert.Equal(ScrollRect.MovementType.Elastic, scroll.movementType);
            Assert.Equal(0.1f, scroll.elasticity);
            Assert.True(scroll.inertia);
            Assert.Equal(0.135f, scroll.decelerationRate);
            Assert.Equal(1f, scroll.scrollSensitivity);
        }

        [Fact]
        public void SliderHasNoGraphicOnItsRoot()
        {
            var root = DefaultControls.CreateSlider(Named()).transform;
            Assert.Equal(new[] { typeof(RectTransform), typeof(Slider) }, Components(root));
            Rect(root, Mid, Mid, Mid, new Vector2(160, 20), Vector2.zero);
            Assert.Equal(new[] { "Background", "Fill Area", "Handle Slide Area" }, Children(root));
            Rect(Child(root, "Fill Area"), new Vector2(0, 0.25f), new Vector2(1, 0.75f), Mid, new Vector2(-20, 0), new Vector2(-5, 0));
            Assert.Equal(new[] { typeof(RectTransform) }, Components(Child(root, "Fill Area")));
            var fill = Child(root, "Fill Area", "Fill");
            Rect(fill, Vector2.zero, new Vector2(0, 1), Mid, new Vector2(10, 0), Vector2.zero);
            Painted(fill, "standard", Image.Type.Sliced);
            var handle = Child(root, "Handle Slide Area", "Handle");
            Painted(handle, "knob", Image.Type.Simple);
            var slider = root.GetComponent<Slider>();
            Assert.Same(fill, slider.fillRect);
            Assert.Same(handle, slider.handleRect);
            Assert.Same(handle.GetComponent<Image>(), slider.targetGraphic);
            Assert.Equal(0f, slider.value);
            Assert.Equal(1f, slider.maxValue);
            Assert.False(slider.wholeNumbers);
        }

        [Fact]
        public void ToggleButtonAndPanel()
        {
            var toggle = DefaultControls.CreateToggle(Named()).transform;
            Assert.Equal(new[] { typeof(RectTransform), typeof(Toggle) }, Components(toggle));
            Assert.Equal(new[] { "Background", "Label" }, Children(toggle));
            var background = Child(toggle, "Background");
            Rect(background, new Vector2(0, 1), new Vector2(0, 1), Mid, new Vector2(20, 20), new Vector2(10, -10));
            Painted(background, "standard", Image.Type.Sliced);
            Painted(Child(background, "Checkmark"), "checkmark", Image.Type.Simple);
            Rect(Child(toggle, "Label"), Vector2.zero, Vector2.one, Mid, new Vector2(-28, -3), new Vector2(9, -0.5f));
            Written(Child(toggle, "Label"), "Toggle", TextAnchor.UpperLeft);
            var t = toggle.GetComponent<Toggle>();
            Assert.True(t.isOn);
            Assert.Same(Child(background, "Checkmark").GetComponent<Image>(), t.graphic);
            Assert.Same(background.GetComponent<Image>(), t.targetGraphic);
            Assert.Null(t.group);

            var button = DefaultControls.CreateButton(Named()).transform;
            Assert.Equal("Button (Legacy)", button.name);
            Drawn(button, typeof(Image), typeof(Button));
            Painted(button, "standard", Image.Type.Sliced);
            Assert.Same(button.GetComponent<Image>(), button.GetComponent<Button>().targetGraphic);
            Assert.Equal(Selectable.Transition.ColorTint, button.GetComponent<Button>().transition);
            var text = Child(button, "Text (Legacy)");
            Drawn(text, typeof(Text));
            Rect(text, Vector2.zero, Vector2.one, Mid, Vector2.zero, Vector2.zero);
            Written(text, "Button", TextAnchor.MiddleCenter);

            var panel = DefaultControls.CreatePanel(Named()).transform;
            Rect(panel, Vector2.zero, Vector2.one, Mid, Vector2.zero, Vector2.zero);
            Painted(panel, "background", Image.Type.Sliced);
            Assert.Equal((Color)new Color32(255, 255, 255, 100), panel.GetComponent<Image>().color);
        }

        [Fact]
        public void InputFieldPlaceholderHasNoFont()
        {
            var root = DefaultControls.CreateInputField(Named()).transform;
            Drawn(root, typeof(Image), typeof(InputField));
            Painted(root, "inputField", Image.Type.Sliced);
            Assert.Equal(new[] { "Placeholder", "Text (Legacy)" }, Children(root));
            var placeholder = Child(root, "Placeholder").GetComponent<Text>();
            Assert.Null(placeholder.font);
            Assert.Equal(FontStyle.Italic, placeholder.fontStyle);
            Assert.Equal("Enter text...", placeholder.text);
            Assert.Equal((Color)new Color32(50, 50, 50, 128), placeholder.color);
            Rect(placeholder.transform, Vector2.zero, Vector2.one, Mid, new Vector2(-20, -13), new Vector2(0, -0.5f));
            var text = Child(root, "Text (Legacy)").GetComponent<Text>();
            Assert.NotNull(text.font);
            Assert.False(text.supportRichText);
            Assert.Equal(HorizontalWrapMode.Overflow, text.horizontalOverflow);
            Assert.Equal("", text.text);
            var field = root.GetComponent<InputField>();
            Assert.Same(text, field.textComponent);
            Assert.Same(placeholder, field.placeholder);
            Assert.Same(root.GetComponent<Image>(), field.targetGraphic);
            Assert.Equal(InputField.LineType.SingleLine, field.lineType);
        }

        [Fact]
        public void EmptyResourcesLeaveEverySpriteNull()
        {
            var root = DefaultControls.CreateScrollView(default).transform;
            foreach (var image in root.GetComponentsInChildren<Image>(true)) Assert.Null(image.sprite);
            var raw = DefaultControls.CreateRawImage(default).transform;
            Drawn(raw, typeof(RawImage));
            Assert.Equal(new Rect(0, 0, 1, 1), raw.GetComponent<RawImage>().uvRect);
            var image2 = DefaultControls.CreateImage(default).transform;
            Rect(image2, Mid, Mid, Mid, new Vector2(100, 100), Vector2.zero);
            var bar = DefaultControls.CreateScrollbar(default).transform;
            Rect(bar, Mid, Mid, Mid, new Vector2(160, 20), Vector2.zero);
            Assert.Equal(Scrollbar.Direction.LeftToRight, bar.GetComponent<Scrollbar>().direction);
            var text = DefaultControls.CreateText(default).transform;
            Drawn(text, typeof(Text));
            Written(text, "New Text", TextAnchor.UpperLeft);
        }
    }
}
