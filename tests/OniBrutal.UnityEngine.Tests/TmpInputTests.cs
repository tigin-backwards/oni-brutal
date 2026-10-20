using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests
{
    // The input field and dropdown, driven through the player loop and the event queue.
    public class TmpInputTests : EngineTest
    {
        // An input module that only sends the selected object its update event each frame (as
        // the game's input module does).
        sealed class SelectedUpdateModule : BaseInputModule
        {
            public override void Process()
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected != null) ExecuteEvents.Execute(selected, GetBaseEventData(), ExecuteEvents.updateSelectedHandler);
            }
        }

        sealed class UpperCaseOnly : TMP_InputValidator
        {
            // Inserts upper-case letters only (as the game's validators insert what they accept).
            public override char Validate(ref string text, ref int pos, char ch)
            {
                if (ch < 'A' || ch > 'Z') return (char)0;
                text = text.Insert(pos, ch.ToString());
                pos++;
                return ch;
            }
        }

        static TMP_InputField NewField()
        {
            TmpTestSupport.LoadGameData();
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(SelectedUpdateModule));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var fieldGo = new GameObject("Field", typeof(RectTransform));
            fieldGo.transform.SetParent(canvasGo.transform, false);
            ((RectTransform)fieldGo.transform).sizeDelta = new Vector2(200f, 30f);
            fieldGo.SetActive(false);
            var field = fieldGo.AddComponent<TMP_InputField>();
            var area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(fieldGo.transform, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.sizeDelta = Vector2.zero;
            area.AddComponent<RectMask2D>();
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(area.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = TmpTestSupport.Graystroke;
            text.fontSize = 14f;
            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(area.transform, false);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.font = TmpTestSupport.Graystroke;
            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            fieldGo.SetActive(true);
            return field;
        }

        static void Type(params Event[] events)
        {
            foreach (var e in events) Event.Enqueue(e);
            Frame();
        }

        static Event Key(KeyCode key, char character = '\0', EventModifiers modifiers = 0) =>
            new() { type = EventType.KeyDown, keyCode = key, character = character, modifiers = modifiers };

        static Event Char(char c) => Key(KeyCode.None, c);

        [Fact]
        public void ActivationStartsEditingInTheNextLateUpdate()
        {
            var field = NewField();
            bool focused = false;
            field.onFocus += () => focused = true;
            field.ActivateInputField();
            Assert.False(field.isFocused);
            Frame();
            Assert.True(field.isFocused);
            Assert.True(focused);
            Assert.Same(field.gameObject, EventSystem.current.currentSelectedGameObject);
            // The shown text carries the end marker for the caret.
            Assert.Equal("​", field.textComponent.text);
            // The caret is drawn by a child of the viewport, its first.
            Assert.Equal("Caret", field.textViewport.GetChild(0).name);
        }

        [Fact]
        public void TypingEditsTheTextAndRaisesTheEvents()
        {
            var field = NewField();
            var changes = new List<string>();
            field.onValueChanged.AddListener(changes.Add);
            field.ActivateInputField();
            Frame();
            Type(Char('a'), Char('b'), Char('c'));
            Assert.Equal("abc", field.text);
            Assert.Equal(new[] { "a", "ab", "abc" }, changes);
            Assert.Equal(3, field.caretPosition);
            Type(Key(KeyCode.LeftArrow), Key(KeyCode.Backspace));
            Assert.Equal("ac", field.text);
            Assert.Equal(1, field.caretPosition);
            Type(Key(KeyCode.Home), Key(KeyCode.Delete));
            Assert.Equal("c", field.text);
            Type(Key(KeyCode.End), Char('d'));
            Assert.Equal("cd", field.text);
            Assert.Equal("cd​", field.textComponent.text);
            // Select all and type over it.
            Type(Key(KeyCode.A, 'a', EventModifiers.Control), Char('z'));
            Assert.Equal("z", field.text);
        }

        [Fact]
        public void EnterSubmitsAndEndsEditingAndEscapeRestores()
        {
            var field = NewField();
            string submitted = null, ended = null;
            field.onSubmit.AddListener(s => submitted = s);
            field.onEndEdit.AddListener(s => ended = s);
            field.ActivateInputField();
            Frame();
            Type(Char('h'), Char('i'), Key(KeyCode.Return, '\n'));
            Assert.Equal("hi", submitted);
            Assert.Equal("hi", ended);
            Assert.False(field.isFocused);

            field.ActivateInputField();
            Frame();
            Type(Char('!'), Key(KeyCode.Escape));
            Assert.False(field.isFocused);
            Assert.Equal("hi", field.text);
        }

        [Fact]
        public void CharactersAreValidated()
        {
            var field = NewField();
            field.characterValidation = TMP_InputField.CharacterValidation.Integer;
            field.characterLimit = 4;
            field.ActivateInputField();
            Frame();
            Type(Char('x'), Char('-'), Char('1'), Char('-'), Char('2'), Char('3'), Char('4'), Char('5'));
            Assert.Equal("-123", field.text);

            var custom = NewField();
            custom.inputValidator = ScriptableObject.CreateInstance<UpperCaseOnly>();
            custom.characterValidation = TMP_InputField.CharacterValidation.CustomValidator;
            custom.ActivateInputField();
            Frame();
            Type(Char('a'), Char('B'), Char('1'), Char('C'));
            Assert.Equal("BC", custom.text);
        }

        [Fact]
        public void ARejectedCharacterLeavesTheSelection()
        {
            var field = NewField();
            field.characterValidation = TMP_InputField.CharacterValidation.Integer;
            field.text = "123";
            int changes = 0;
            field.onValueChanged.AddListener(_ => changes++);
            field.ActivateInputField();
            Frame();
            // Select all: the anchor at the end, the caret at the start.
            Type(Key(KeyCode.A, 'a', EventModifiers.Control));
            Assert.Equal(3, field.selectionAnchorPosition);
            Assert.Equal(0, field.caretPosition);
            Type(Char('x'));
            Assert.Equal("123", field.text);
            Assert.Equal(3, field.selectionAnchorPosition);
            Assert.Equal(0, field.selectionFocusPosition);
            Assert.Equal(0, changes);
            // A part of the text, selected backwards.
            Type(Key(KeyCode.End), Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift), Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift), Char('x'));
            Assert.Equal("123", field.text);
            Assert.Equal(3, field.selectionAnchorPosition);
            Assert.Equal(1, field.selectionFocusPosition);
            Assert.Equal(0, changes);
            // Validation sees the text with the selection removed: "-" typed over a selected "-"
            // is at the start of "12".
            field.text = "-12";
            Type(Key(KeyCode.Home), Key(KeyCode.RightArrow, '\0', EventModifiers.Shift), Char('-'));
            Assert.Equal("-12", field.text);
            Assert.Equal(1, field.caretPosition);

            // onValidateInput is given that text and the selection's start.
            var callback = NewField();
            callback.text = "abc";
            string seen = null;
            int seenIndex = -1;
            callback.onValidateInput = (t, i, c) => { seen = t; seenIndex = i; return (char)0; };
            callback.ActivateInputField();
            Frame();
            Type(Key(KeyCode.End), Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift), Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift), Char('x'));
            Assert.Equal("a", seen);
            Assert.Equal(1, seenIndex);
            Assert.Equal("abc", callback.text);
            Assert.Equal(3, callback.selectionAnchorPosition);
            Assert.Equal(1, callback.selectionFocusPosition);

            // A custom validator is handed the text after the selection is removed, which stays
            // removed when it rejects the character.
            var custom = NewField();
            custom.inputValidator = ScriptableObject.CreateInstance<UpperCaseOnly>();
            custom.characterValidation = TMP_InputField.CharacterValidation.CustomValidator;
            custom.text = "ABC";
            changes = 0;
            custom.onValueChanged.AddListener(_ => changes++);
            custom.ActivateInputField();
            Frame();
            Type(Key(KeyCode.End), Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift), Key(KeyCode.LeftArrow, '\0', EventModifiers.Shift), Char('x'));
            Assert.Equal("A", custom.text);
            Assert.Equal(1, custom.caretPosition);
            Assert.Equal(0, changes);
        }

        [Fact]
        public void DeselectingEndsEditing()
        {
            var field = NewField();
            string ended = null;
            field.onEndEdit.AddListener(s => ended = s);
            field.ActivateInputField();
            Frame();
            Type(Char('q'));
            field.OnDeselect(new BaseEventData(EventSystem.current));
            Assert.False(field.isFocused);
            Assert.Equal("q", ended);
            // The placeholder shows when the text is empty.
            var placeholder = (TextMeshProUGUI)field.placeholder;
            Assert.False(placeholder.enabled);
            field.text = "";
            Assert.True(placeholder.enabled);
        }

        [Fact]
        public void DropdownShowsItsOptionsAndSetsTheValue()
        {
            TmpTestSupport.LoadGameData();
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(SelectedUpdateModule));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = new GameObject("Dropdown", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(160f, 30f);
            root.SetActive(false);
            var dropdown = root.AddComponent<TMP_Dropdown>();
            var caption = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            caption.transform.SetParent(root.transform, false);
            caption.font = TmpTestSupport.Graystroke;
            // The template: a list with a content holding one toggle item with a text.
            var template = new GameObject("Template", typeof(RectTransform));
            template.transform.SetParent(root.transform, false);
            template.AddComponent<CanvasGroup>();
            var templateRect = (RectTransform)template.transform;
            templateRect.sizeDelta = new Vector2(160f, 150f);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(template.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 28f);
            var item = new GameObject("Item", typeof(RectTransform));
            item.transform.SetParent(content.transform, false);
            var itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 20f);
            var toggle = item.AddComponent<Toggle>();
            var itemLabel = new GameObject("Item Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            itemLabel.transform.SetParent(item.transform, false);
            itemLabel.font = TmpTestSupport.Graystroke;
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            template.SetActive(false);
            root.SetActive(true);

            int changed = -1;
            dropdown.onValueChanged.AddListener(v => changed = v);
            dropdown.options = new List<TMP_Dropdown.OptionData> { new("Low"), new("Medium"), new("High") };
            Assert.Equal("Low", caption.text);
            dropdown.value = 2;
            Assert.Equal(2, changed);
            Assert.Equal("High", caption.text);

            Frame();
            dropdown.Show();
            Assert.True(dropdown.IsExpanded);
            var list = root.transform.Find("Dropdown List");
            Assert.NotNull(list);
            Assert.NotNull(canvasGo.transform.Find("Blocker"));
            var items = list.GetComponentsInChildren<Toggle>();
            Assert.Equal(3, items.Length);
            Assert.Equal("Item 1: Medium", items[1].name);
            Assert.True(items[2].isOn);
            items[1].isOn = true;
            Assert.Equal(1, dropdown.value);
            Assert.Equal("Medium", caption.text);
            Frame();
            Assert.Null(canvasGo.transform.Find("Blocker"));
        }
    }
}
