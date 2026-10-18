using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    public class SelectableTests : UGuiTest
    {
        /// The tint reached through the colour fade (a lerp to its end), within float rounding.
        static void Tinted(Color expected, Image image)
        {
            var c = image.canvasRenderer.GetColor();
            Assert.True(Mathf.Abs(c.r - expected.r) < 1e-5f && Mathf.Abs(c.g - expected.g) < 1e-5f && Mathf.Abs(c.b - expected.b) < 1e-5f && Mathf.Abs(c.a - expected.a) < 1e-5f, $"{c} is not {expected}");
        }

        static ColorBlock Instant()
        {
            var c = ColorBlock.defaultColorBlock;
            c.fadeDuration = 0f;
            c.highlightedColor = Color.yellow;
            c.pressedColor = Color.red;
            c.selectedColor = Color.blue;
            c.disabledColor = Color.gray;
            return c;
        }

        [Fact]
        public void StateFollowsPointerSelectionAndInteractability()
        {
            var es = new GameObject("EventSystem").AddComponent<EventSystem>();
            var canvas = NewCanvas();
            var rect = NewRect("Button", canvas.transform, 100, 50, typeof(Image), typeof(Button));
            var button = rect.GetComponent<Button>();
            var image = rect.GetComponent<Image>();
            button.colors = Instant();
            Assert.Same(image, button.targetGraphic);
            Tinted(Color.white, image);

            var pointer = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
            button.OnPointerEnter(pointer);
            Tinted(Color.yellow, image);
            button.OnPointerDown(pointer);
            Tinted(Color.red, image);
            // A left press selects it.
            Assert.Same(rect.gameObject, es.currentSelectedGameObject);
            button.OnPointerUp(pointer);
            Tinted(Color.blue, image);
            es.SetSelectedGameObject(null);
            Tinted(Color.yellow, image);
            button.OnPointerExit(pointer);
            Tinted(Color.white, image);

            button.interactable = false;
            Tinted(Color.gray, image);
            Assert.False(button.IsInteractable());
            button.interactable = true;

            // An enclosing CanvasGroup that is not interactable disables it, unless a group in
            // between ignores its parents.
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            Assert.False(button.IsInteractable());
            Tinted(Color.gray, image);
            rect.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            Assert.True(button.IsInteractable());
        }

        [Fact]
        public void ButtonClicksOnLeftClickAndSubmit()
        {
            var canvas = NewCanvas();
            var button = NewRect("Button", canvas.transform, 100, 50, typeof(Image), typeof(Button)).GetComponent<Button>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            button.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.Equal(0, clicks);
            button.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            button.OnSubmit(null);
            Assert.Equal(2, clicks);
            button.interactable = false;
            button.OnSubmit(null);
            Assert.Equal(2, clicks);
        }

        [Fact]
        public void AutomaticNavigationFindsTheNearestInDirection()
        {
            var canvas = NewCanvas();
            var a = NewRect("A", canvas.transform, 50, 50, typeof(Image), typeof(Button)).GetComponent<Button>();
            var right = NewRect("Right", canvas.transform, 50, 50, typeof(Image), typeof(Button)).GetComponent<Button>();
            var farRight = NewRect("FarRight", canvas.transform, 50, 50, typeof(Image), typeof(Button)).GetComponent<Button>();
            var up = NewRect("Up", canvas.transform, 50, 50, typeof(Image), typeof(Button)).GetComponent<Button>();
            right.transform.localPosition = new Vector3(100, 0);
            farRight.transform.localPosition = new Vector3(300, 0);
            up.transform.localPosition = new Vector3(0, 100);
            Assert.Same(right, a.FindSelectableOnRight());
            Assert.Same(up, a.FindSelectableOnUp());
            Assert.Null(a.FindSelectableOnLeft());
            var nav = a.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnRight = farRight;
            a.navigation = nav;
            Assert.Same(farRight, a.FindSelectableOnRight());
            Assert.Null(a.FindSelectableOnUp());
        }
    }

    public class ToggleTests : UGuiTest
    {
        [Fact]
        public void AGroupKeepsOneToggleOn()
        {
            var canvas = NewCanvas();
            var group = canvas.gameObject.AddComponent<ToggleGroup>();
            var toggles = new Toggle[3];
            for (int i = 0; i < 3; i++)
            {
                toggles[i] = NewRect("Toggle" + i, canvas.transform, 20, 20, typeof(Image), typeof(Toggle)).GetComponent<Toggle>();
                toggles[i].isOn = false;
                toggles[i].group = group;
            }
            int changes = 0;
            toggles[1].onValueChanged.AddListener(_ => changes++);
            toggles[1].isOn = true;
            toggles[2].isOn = true;
            Assert.False(toggles[1].isOn);
            Assert.True(toggles[2].isOn);
            Assert.Equal(2, changes);
            Assert.Same(toggles[2], group.GetFirstActiveToggle());

            // The only toggle on cannot be turned off.
            toggles[2].isOn = false;
            Assert.True(toggles[2].isOn);
            group.allowSwitchOff = true;
            toggles[2].isOn = false;
            Assert.False(group.AnyTogglesOn());

            group.allowSwitchOff = false;
            toggles[0].isOn = true;
            group.SetAllTogglesOff();
            Assert.False(group.AnyTogglesOn());
            Assert.False(group.allowSwitchOff);

            toggles[0].SetIsOnWithoutNotify(true);
            Assert.Single(group.ActiveToggles());
        }

        [Fact]
        public void TheGraphicShowsTheState()
        {
            var canvas = NewCanvas();
            var toggle = NewRect("Toggle", canvas.transform, 20, 20, typeof(Image), typeof(Toggle)).GetComponent<Toggle>();
            var check = NewRect("Check", toggle.transform, 10, 10, typeof(Image)).GetComponent<Image>();
            toggle.graphic = check;
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.isOn = true;
            toggle.isOn = false;
            Assert.Equal(0f, check.canvasRenderer.GetAlpha());
            toggle.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            Assert.True(toggle.isOn);
            Assert.Equal(1f, check.canvasRenderer.GetAlpha());
        }
    }

    public class SliderTests : UGuiTest
    {
        [Fact]
        public void ValueIsClampedRoundedAndShownByFillAndHandle()
        {
            var canvas = NewCanvas();
            var slider = NewRect("Slider", canvas.transform, 200, 20, typeof(Slider)).GetComponent<Slider>();
            var fillArea = NewRect("Fill Area", slider.transform, 200, 20);
            var fill = NewRect("Fill", fillArea, 0, 0, typeof(Image));
            var handleArea = NewRect("Handle Area", slider.transform, 200, 20);
            var handle = NewRect("Handle", handleArea, 10, 0, typeof(Image));
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.minValue = 10;
            slider.maxValue = 20;
            float last = float.NaN;
            slider.onValueChanged.AddListener(v => last = v);
            slider.value = 12.5f;
            Assert.Equal(12.5f, last);
            Assert.Equal(0.25f, slider.normalizedValue, 4);
            Assert.Equal(new Vector2(0.25f, 1f), fill.anchorMax);
            Assert.Equal(new Vector2(0.25f, 0f), handle.anchorMin);
            slider.value = 99;
            Assert.Equal(20f, slider.value);
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(13.4f);
            Assert.Equal(13f, slider.value);
            Assert.Equal(20f, last);
            slider.direction = Slider.Direction.RightToLeft;
            Assert.Equal(new Vector2(0.7f, 0f), new Vector2(Mathf.Round(fill.anchorMin.x * 100) / 100, fill.anchorMin.y));

            // The arrow keys step by 1 for whole numbers.
            slider.direction = Slider.Direction.LeftToRight;
            slider.OnMove(new AxisEventData(null) { moveDir = MoveDirection.Right });
            Assert.Equal(14f, slider.value);
        }

        [Fact]
        public void ScrollbarHandleSpansTheSizeAtTheValue()
        {
            var canvas = NewCanvas();
            var bar = NewRect("Scrollbar", canvas.transform, 20, 200, typeof(Scrollbar)).GetComponent<Scrollbar>();
            var area = NewRect("Sliding Area", bar.transform, 20, 200);
            var handle = NewRect("Handle", area, 0, 0, typeof(Image));
            bar.handleRect = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.size = 0.25f;
            bar.value = 0.5f;
            Assert.Equal(0.375f, handle.anchorMin.y, 4);
            Assert.Equal(0.625f, handle.anchorMax.y, 4);
            bar.numberOfSteps = 3;
            bar.value = 0.3f;
            Assert.Equal(0.5f, bar.value);
            bar.size = 2f;
            Assert.Equal(1f, bar.size);
        }
    }

    public class ScrollRectTests : UGuiTest
    {
        (ScrollRect Scroll, RectTransform Content) Setup(ScrollRect.MovementType movement)
        {
            var canvas = NewCanvas();
            var scroll = NewRect("Scroll", canvas.transform, 100, 100, typeof(Image), typeof(ScrollRect)).GetComponent<ScrollRect>();
            var content = NewRect("Content", scroll.transform, 100, 400);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = movement;
            scroll.inertia = false;
            Canvas.ForceUpdateCanvases();
            return (scroll, content);
        }

        static PointerEventData At(Vector2 screen) => new PointerEventData(null) { button = PointerEventData.InputButton.Left, position = screen };

        [Fact]
        public void DraggingMovesTheContentWithinItsBoundsWhenClamped()
        {
            var (scroll, content) = Setup(ScrollRect.MovementType.Clamped);
            Assert.Equal(1f, scroll.verticalNormalizedPosition, 3);
            scroll.OnBeginDrag(At(new Vector2(512, 384)));
            scroll.OnDrag(At(new Vector2(512, 484)));
            Assert.Equal(100f, content.anchoredPosition.y, 3);
            Assert.Equal(2f / 3f, scroll.verticalNormalizedPosition, 3);
            // Beyond the end it stops at the bounds.
            scroll.OnDrag(At(new Vector2(512, 1384)));
            Assert.Equal(300f, content.anchoredPosition.y, 3);
            scroll.OnEndDrag(At(new Vector2(512, 1384)));
            Assert.Equal(0f, scroll.verticalNormalizedPosition, 3);
        }

        [Fact]
        public void ElasticOvershootIsDampedAndSpringsBack()
        {
            var (scroll, content) = Setup(ScrollRect.MovementType.Elastic);
            scroll.OnBeginDrag(At(new Vector2(512, 384)));
            scroll.OnDrag(At(new Vector2(512, 284)));
            float y = content.anchoredPosition.y;
            Assert.True(y < 0f && y > -100f);
            scroll.OnEndDrag(At(new Vector2(512, 284)));
            for (int i = 0; i < 120; i++) Frame(1f / 60f);
            Assert.Equal(0f, content.anchoredPosition.y, 1);
        }

        [Fact]
        public void TheWheelScrollsBySensitivityAndNormalizedPositionMovesTheContent()
        {
            var (scroll, content) = Setup(ScrollRect.MovementType.Clamped);
            scroll.scrollSensitivity = 10;
            scroll.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0, -2) });
            Assert.Equal(20f, content.anchoredPosition.y, 3);
            scroll.verticalNormalizedPosition = 0f;
            Assert.Equal(300f, content.anchoredPosition.y, 3);
            Assert.Equal(new Vector2(0f, 0f), scroll.normalizedPosition);
        }
    }
}
