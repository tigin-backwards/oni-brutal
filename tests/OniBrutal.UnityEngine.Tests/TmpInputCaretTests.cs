using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests
{
    // The caret after an edit ends, and the caret graphic's cull (textmeshpro.md, "The caret
    // after an edit ends" and "The caret graphic and RectMask2D", measured with UnityProbe's
    // inputcaret scenario). Keys sent through the event queue reach the field through its
    // OnUpdateSelected, as real keys do; ProcessEvent is called directly.
    public class TmpInputCaretTests : OniBrutal.Tests.UGui.UGuiTest
    {
        sealed class Probe
        {
            public TMP_InputField Field;
            public readonly List<string> Events = new();

            public string Pos => Field.caretPosition + "/" + Field.selectionAnchorPosition + "/" + Field.selectionFocusPosition + "/" + Field.stringPosition;

            // Caret and anchor, as the table gives them.
            public string Caret => Field.caretPosition + "/" + Field.selectionAnchorPosition;

            public string Take()
            {
                var s = string.Join(",", Events);
                Events.Clear();
                return s;
            }
        }

        static Probe NewProbe(bool reset, bool selectAll = false, string text = null)
        {
            var p = new Probe { Field = TmpInputTests.NewField(120f) };
            var f = p.Field;
            f.resetOnDeActivation = reset;
            f.onFocusSelectAll = selectAll;
            f.onValueChanged.AddListener(v => p.Events.Add("changed:" + v + "@" + p.Pos));
            f.onEndEdit.AddListener(v => p.Events.Add("endEdit:" + v + "@" + p.Pos));
            f.onSubmit.AddListener(v => p.Events.Add("submit:" + v + "@" + p.Pos));
            f.onDeselect.AddListener(v => p.Events.Add("deselect:" + v));
            Frame();
            if (text != null) f.text = text;
            Focus(f);
            p.Events.Clear();
            return p;
        }

        static void Focus(TMP_InputField f)
        {
            EventSystem.current.SetSelectedGameObject(f.gameObject);
            f.ActivateInputField();
            Frame();
            Frame();
        }

        static Event Key(KeyCode key, char character = '\0', EventModifiers modifiers = 0) =>
            new() { type = EventType.KeyDown, keyCode = key, character = character, modifiers = modifiers };

        // Keys through the event system, a frame each.
        static void Keys(params Event[] events)
        {
            foreach (var e in events)
            {
                Event.Enqueue(e);
                Frame();
            }
        }

        static void KeysTyped(string s)
        {
            foreach (char c in s) Keys(Key(KeyCode.None, c));
        }

        static void Typed(TMP_InputField f, string s)
        {
            foreach (char c in s) f.ProcessEvent(Key(KeyCode.None, c));
        }

        static void Enter() => Keys(Key(KeyCode.Return, '\n'));

        static void ClearAndType(Probe p, string value)
        {
            Keys(Key(KeyCode.A, 'a', EventModifiers.Control), Key(KeyCode.Backspace));
            KeysTyped(value);
            p.Events.Clear();
        }

        [Theory]
        [InlineData(true, "500", "3/3")]
        [InlineData(true, "50000", "5/5")]
        [InlineData(false, "500", "3/3")]
        public void EnterAfterCtrlABackspaceAndTypingKeepsTheCaret(bool reset, string value, string caret)
        {
            var p = NewProbe(reset, text: "1000");
            ClearAndType(p, value);
            Enter();
            Assert.Equal(caret, p.Caret);
            Assert.Equal(value, p.Field.text);
            Assert.False(p.Field.isFocused);
            int n = value.Length;
            // onSubmit while focused, then onEndEdit with the caret as it was, then onDeselect.
            Assert.Equal($"submit:{value}@{n}/{n}/{n}/{n},endEdit:{value}@{n}/{n}/{n}/{n},deselect:{value}", p.Take());
            Assert.Null(EventSystem.current.currentSelectedGameObject);
        }

        // Klei's KInputField: onEndEdit sets the parsed value again, and the field is
        // deactivated at the end of the frame.
        [Theory]
        [InlineData("500", "3/3")]
        [InlineData("50000", "5/5")]
        public void KleisEndEditKeepsTheCaret(string value, string caret)
        {
            var p = NewProbe(true, text: "1000");
            bool deactivate = false;
            p.Field.onEndEdit.AddListener(v =>
            {
                p.Field.text = float.Parse(v + ".0", System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
                deactivate = true;
            });
            ClearAndType(p, value);
            Enter();
            Assert.True(deactivate);
            p.Field.DeactivateInputField();
            Frame();
            Assert.Equal(caret, p.Caret);
            Assert.Equal(value, p.Field.text);
        }

        [Theory]
        [InlineData(true, "endEdit:500@3/3/3/3,deselect:500")]
        [InlineData(false, "deselect:500")]
        public void DeselectionKeepsTheCaretAndSendsEndEditOnlyWithReset(bool reset, string events)
        {
            var p = NewProbe(reset);
            Typed(p.Field, "500");
            p.Events.Clear();
            EventSystem.current.SetSelectedGameObject(null);
            Assert.False(p.Field.isFocused);
            Assert.Equal("3/3", p.Caret);
            Assert.Equal(events, p.Take());
        }

        [Theory]
        [InlineData(true, "endEdit:500@3/3/3/3,deselect:500")]
        [InlineData(false, "deselect:500")]
        public void DeactivateInputFieldSendsEndEditOnlyWithReset(bool reset, string events)
        {
            var p = NewProbe(reset);
            Typed(p.Field, "500");
            p.Events.Clear();
            p.Field.DeactivateInputField();
            Assert.Equal("3/3", p.Caret);
            Assert.Equal(events, p.Take());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DeactivateWithClearSelectionKeepsTheCaretAndSendsEndEdit(bool reset)
        {
            var p = NewProbe(reset);
            Typed(p.Field, "500");
            p.Events.Clear();
            p.Field.DeactivateInputField(true);
            Assert.Equal("3/3", p.Caret);
            Assert.Equal("endEdit:500@3/3/3/3,deselect:500", p.Take());
        }

        [Fact]
        public void ASelectionStaysThroughEnter()
        {
            var p = NewProbe(true);
            KeysTyped("500");
            Keys(Key(KeyCode.Home, '\0', EventModifiers.Shift));
            Assert.Equal("0/3", p.Caret);
            Enter();
            Assert.False(p.Field.isFocused);
            Assert.Equal("0/3", p.Caret);
            Assert.Equal(0, p.Field.stringPosition);
            Assert.Equal(3, p.Field.selectionStringAnchorPosition);
        }

        [Theory]
        [InlineData(false, "")]
        [InlineData(true, "endEdit:500@3/3/3/3")]
        public void DisablingKeepsTheCaretAndSendsEndEditOnlyWithReset(bool reset, string events)
        {
            var p = NewProbe(reset);
            Typed(p.Field, "500");
            p.Events.Clear();
            p.Field.gameObject.SetActive(false);
            Assert.False(p.Field.isFocused);
            Assert.Equal("3/3", p.Caret);
            Assert.Equal(events, p.Take());
        }

        // A shorter text cuts the caret and the anchor to its length; they stay there when the
        // text grows again. A longer text keeps them.
        [Theory]
        [InlineData("5", "1/1")]
        [InlineData("500123", "3/3")]
        public void AListenersTextCutsTheCaretOnlyWhenShorter(string set, string caret)
        {
            var p = NewProbe(true);
            p.Field.onEndEdit.AddListener(_ => p.Field.text = set);
            Typed(p.Field, "500");
            EventSystem.current.SetSelectedGameObject(null);
            Assert.Equal(caret, p.Caret);
            p.Field.text = "500123";
            Assert.Equal(caret, p.Caret);
        }

        [Fact]
        public void EscapeInAnEmptyFieldRestoresTheEmptyText()
        {
            var p = NewProbe(true);
            KeysTyped("500");
            p.Events.Clear();
            Keys(Key(KeyCode.Escape));
            Assert.Equal("", p.Field.text);
            Assert.Equal("0/0", p.Caret);
            Assert.False(p.Field.isFocused);
            Assert.Equal("changed:@0/0/0/0,endEdit:@0/0/0/0,deselect:", p.Take());
        }

        [Fact]
        public void EscapeOverALongerTextKeepsTheCaret()
        {
            var p = NewProbe(true, text: "1000");
            ClearAndType(p, "500");
            Keys(Key(KeyCode.Escape));
            Assert.Equal("1000", p.Field.text);
            Assert.Equal("3/3", p.Caret);
        }

        [Fact]
        public void EscapeThenAListenersShorterTextCutsTheCaret()
        {
            var p = NewProbe(true);
            p.Field.onEndEdit.AddListener(_ => p.Field.text = "0");
            KeysTyped("500");
            Keys(Key(KeyCode.Escape));
            Assert.Equal("0", p.Field.text);
            Assert.Equal("0/0", p.Caret);
        }

        [Theory]
        [InlineData(true, "submit:500@3/3/3/3,endEdit:500@3/3/3/3,deselect:500")]
        [InlineData(false, "submit:500@3/3/3/3,deselect:500")]
        public void OnSubmitOnAFocusedFieldSubmitsAndEndsTheEdit(bool reset, string events)
        {
            var p = NewProbe(reset);
            Typed(p.Field, "500");
            p.Events.Clear();
            p.Field.OnSubmit(new BaseEventData(EventSystem.current));
            Assert.False(p.Field.isFocused);
            Assert.Null(EventSystem.current.currentSelectedGameObject);
            Assert.Equal("3/3", p.Caret);
            Assert.Equal(events, p.Take());
        }

        [Fact]
        public void ProcessEventOfReturnDoesNothing()
        {
            var p = NewProbe(true);
            Typed(p.Field, "500");
            p.Events.Clear();
            p.Field.ProcessEvent(Key(KeyCode.Return, '\n'));
            Frame();
            Assert.True(p.Field.isFocused);
            Assert.Equal("500", p.Field.text);
            Assert.Equal("3/3", p.Caret);
            Assert.Equal("", p.Take());
        }

        [Fact]
        public void SelectAllOnFocusPutsTheCaretAtTheStart()
        {
            var p = NewProbe(false, selectAll: true, text: "500");
            Assert.Equal(0, p.Field.caretPosition);
            Assert.Equal(3, p.Field.selectionAnchorPosition);
            Assert.Equal(0, p.Field.stringPosition);
            Assert.Equal(3, p.Field.selectionStringAnchorPosition);
        }

        [Fact]
        public void TextSetOnAFocusedFieldKeepsTheCaret()
        {
            var p = NewProbe(true, selectAll: true);
            p.Field.text = "500";
            Assert.Equal("0/0", p.Caret);
            Assert.Equal("changed:500@0/0/0/0", p.Take());
        }

        // Typing again after the end goes in where the caret was.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ActivatingAgainStartsWhereTheCaretWas(bool reset)
        {
            var p = NewProbe(reset);
            Typed(p.Field, "500");
            Frame();
            p.Field.ProcessEvent(Key(KeyCode.Return, '\n'));
            EventSystem.current.SetSelectedGameObject(null);
            Frame();
            Focus(p.Field);
            Typed(p.Field, "9");
            Assert.Equal("5009", p.Field.text);
            Assert.Equal("4/4", p.Caret);
        }

        [Fact]
        public void ATypedCharactersValueChangedSeesTheCaretAfterIt()
        {
            var p = NewProbe(true);
            Typed(p.Field, "500");
            Assert.Equal("changed:5@1/1/1/1,changed:50@2/2/2/2,changed:500@3/3/3/3", p.Take());
            var q = NewProbe(true);
            KeysTyped("500");
            Assert.Equal("changed:5@1/1/1/1,changed:50@2/2/2/2,changed:500@3/3/3/3", q.Take());
        }

        // After Ctrl+A in ProcessEvent the string positions move at once, the character
        // positions a frame later.
        [Fact]
        public void CtrlAMovesTheCharacterPositionsAFrameLate()
        {
            var p = NewProbe(true, text: "1000");
            p.Field.ProcessEvent(Key(KeyCode.A, 'a', EventModifiers.Control));
            Assert.Equal(0, p.Field.stringPosition);
            Assert.Equal(4, p.Field.selectionStringAnchorPosition);
            Assert.Equal(0, p.Field.selectionAnchorPosition);
            Assert.Equal(0, p.Field.caretPosition);
            Frame();
            Assert.Equal(4, p.Field.selectionAnchorPosition);
            Assert.Equal(0, p.Field.caretPosition);
            // Backspace deletes the selection; the next characters go in from 0.
            p.Field.ProcessEvent(Key(KeyCode.A, 'a', EventModifiers.Control));
            p.Field.ProcessEvent(Key(KeyCode.Backspace));
            Assert.Equal("", p.Field.text);
            Assert.Equal("0/0", p.Caret);
        }

        [Fact]
        public void ANewFieldResetsOnDeactivation()
        {
            var p = NewProbe(true);
            var go = new GameObject("New", typeof(RectTransform));
            Assert.True(go.AddComponent<TMP_InputField>().resetOnDeActivation);
            Assert.True(p.Field.resetOnDeActivation);
        }

        // ----- The caret graphic's cull -----

        static MaskableGraphic CaretOf(TMP_InputField f) => f.textViewport.GetChild(0).GetComponent<MaskableGraphic>();

        // An Image with the caret's rect, its sibling.
        static Image Control(MaskableGraphic caret)
        {
            var go = new GameObject("Control", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(caret.transform.parent, false);
            var c = caret.rectTransform;
            rt.anchorMin = c.anchorMin; rt.anchorMax = c.anchorMax; rt.pivot = c.pivot; rt.sizeDelta = c.sizeDelta;
            rt.position = c.position;
            return go.AddComponent<Image>();
        }

        // The caret moved wholly outside its viewport's clip, as at its defaults.
        static (MaskableGraphic caret, Image control) CaretOutside(TMP_InputField f)
        {
            var caret = CaretOf(f);
            var rt = caret.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100, 100);
            rt.position = f.transform.position + new Vector3(-300, -300, 0);
            var control = Control(caret);
            Frame();
            Frame();
            return (caret, control);
        }

        [Fact]
        public void TheCaretOutsideAValidClipIsNotCulled()
        {
            var f = TmpInputTests.NewField(120f);
            Frame();
            var (caret, control) = CaretOutside(f);
            Assert.True(caret.canvasRenderer.hasRectClipping);
            Assert.False(caret.canvasRenderer.cull);
            Assert.True(control.canvasRenderer.cull);
            Assert.True(caret.maskable);
            // Focused, with a quad outside the clip; then ended.
            Focus(f);
            Typed(f, "12");
            Frame();
            Assert.False(caret.canvasRenderer.cull);
            EventSystem.current.SetSelectedGameObject(null);
            Frame();
            Frame();
            Assert.False(caret.canvasRenderer.cull);
            // The mask moved and back, the caret moved.
            f.textViewport.anchoredPosition += new Vector2(0, 1);
            Frame();
            Assert.False(caret.canvasRenderer.cull);
            Assert.True(control.canvasRenderer.cull);
            caret.rectTransform.anchoredPosition += new Vector2(0, 1);
            Frame();
            Assert.False(caret.canvasRenderer.cull);
        }

        [Fact]
        public void TheCaretsCullFollowsOnlyTheValidFlag()
        {
            var f = TmpInputTests.NewField(120f);
            Frame();
            var caret = CaretOf(f);
            var far = new Rect(5000, 5000, 10, 10);
            var all = new Rect(-5000, -5000, 10000, 10000);
            bool? changed = null;
            caret.onCullStateChanged.AddListener(c => changed = c);
            ((IClippable)caret).Cull(far, true);
            Assert.False(caret.canvasRenderer.cull);
            Assert.Null(changed);
            ((IClippable)caret).Cull(far, false);
            Assert.True(caret.canvasRenderer.cull);
            Assert.True(changed);
            ((IClippable)caret).Cull(all, true);
            Assert.False(caret.canvasRenderer.cull);
            Assert.False(changed);
        }

        // Clips that do not intersect: the clip rect is invalid and the caret is culled with
        // everything else under the mask.
        [Fact]
        public void TheCaretUnderAnInvalidClipIsCulled()
        {
            TmpTestSupport.LoadGameData();
            var f = TmpInputTests.NewField(120f);
            var canvas = f.transform.parent;
            var outer = new GameObject("Outer", typeof(RectTransform));
            var ort = (RectTransform)outer.transform;
            ort.SetParent(canvas, false);
            ort.sizeDelta = new Vector2(300, 100);
            ort.anchoredPosition = new Vector2(100, 50);
            outer.AddComponent<RectMask2D>();
            f.transform.SetParent(ort, false);
            ((RectTransform)f.transform).anchoredPosition = new Vector2(0, 200);
            Frame();
            Frame();
            var caret = CaretOf(f);
            Assert.True(caret.canvasRenderer.cull);
            Assert.False(caret.canvasRenderer.hasRectClipping);
            // A direct call un-culls it; the mask's next pass culls it again.
            ((IClippable)caret).Cull(new Rect(-5000, -5000, 10000, 10000), true);
            Assert.False(caret.canvasRenderer.cull);
            Frame();
            Assert.True(caret.canvasRenderer.cull);
        }

        // ugui.md: a direct Cull call that un-culled an Image the mask culls is undone by the
        // mask's next pass, with nothing moved.
        [Fact]
        public void TheMasksNextPassUndoesADirectCull()
        {
            var f = TmpInputTests.NewField(120f);
            Frame();
            var (_, control) = CaretOutside(f);
            Assert.True(control.canvasRenderer.cull);
            ((IClippable)control).Cull(new Rect(-5000, -5000, 10000, 10000), true);
            Assert.False(control.canvasRenderer.cull);
            Frame();
            Assert.True(control.canvasRenderer.cull);
            Frame();
            Assert.True(control.canvasRenderer.cull);
        }
    }
}
