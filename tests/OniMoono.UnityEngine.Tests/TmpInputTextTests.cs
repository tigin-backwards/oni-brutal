using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Xunit;

namespace OniMoono.Tests
{
    // Text set from code on the input field (textmeshpro.md, "Text set from code"): text = and
    // SetTextWithoutNotify keep the value whole, whatever the limit, line type and content
    // type, and nothing applies them later; only typed characters are limited and validated.
    public class TmpInputTextTests : EngineTest
    {
        const string Marker = "\u200B";

        public enum State { Inactive, Active, Focused }

        public enum Setter { Text, WithoutNotify }

        // A field set up as Geyser Control's slider field is (line type, content type and limit
        // first), in the given state, reporting its events.
        sealed class Probe
        {
            public TMP_InputField Field;
            public readonly List<string> Changed = new();
            public readonly List<string> Ended = new();
            public readonly List<string> Submitted = new();

            public void ClearEvents()
            {
                Changed.Clear();
                Ended.Clear();
                Submitted.Clear();
            }
        }

        static Probe NewProbe(State state, TMP_InputField.ContentType? content = null, int limit = 0,
            TMP_InputField.LineType? line = null, float width = 200f)
        {
            var field = TmpInputTests.NewField(width);
            field.onFocusSelectAll = false;
            if (line is { } l) field.lineType = l;
            if (content is { } c) field.contentType = c;
            field.characterLimit = limit;
            var probe = new Probe { Field = field };
            field.onValueChanged.AddListener(probe.Changed.Add);
            field.onEndEdit.AddListener(probe.Ended.Add);
            field.onSubmit.AddListener(probe.Submitted.Add);
            if (state == State.Inactive) field.gameObject.SetActive(false);
            if (state == State.Focused) Focus(field);
            probe.ClearEvents();
            return probe;
        }

        // Selected and activated, two frames.
        static void Focus(TMP_InputField field)
        {
            EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            Frame();
            Frame();
            Assert.True(field.isFocused);
        }

        static void Deselect(TMP_InputField field)
        {
            EventSystem.current.SetSelectedGameObject(null);
            Frame();
        }

        static void Set(TMP_InputField field, Setter setter, string value)
        {
            if (setter == Setter.Text) field.text = value;
            else field.SetTextWithoutNotify(value);
        }

        static Event Key(KeyCode key, char character = '\0') =>
            new() { type = EventType.KeyDown, keyCode = key, character = character };

        static void End(TMP_InputField field) => field.ProcessEvent(Key(KeyCode.End));

        static void TypeChar(TMP_InputField field, char c) => field.ProcessEvent(Key(KeyCode.None, c));

        static void Backspace(TMP_InputField field) => field.ProcessEvent(Key(KeyCode.Backspace));

        // Follows the field through a frame, enabling, activation and deselection: the value
        // stays whole and no event is sent.
        static void AssertKeptThroughTheSteps(Probe p, string value)
        {
            var f = p.Field;
            Frame();
            Assert.Equal(value, f.text);
            if (!f.gameObject.activeSelf)
            {
                f.gameObject.SetActive(true);
                Assert.Equal(value, f.text);
                Frame();
                Assert.Equal(value, f.text);
            }
            if (!f.isFocused) Focus(f);
            Assert.Equal(value, f.text);
            Assert.Empty(p.Changed);
            Assert.Empty(p.Ended);
        }

        public static IEnumerable<object[]> KeptWhole()
        {
            var dec = TMP_InputField.ContentType.DecimalNumber;
            var cases = new (TMP_InputField.ContentType? content, int limit, TMP_InputField.LineType? line, string value)[]
            {
                // Geyser Control's values at limit 4.
                (dec, 4, null, "507.2"),
                (dec, 4, null, "63.6"),
                (dec, 4, null, "65208.3"),
                (dec, 4, null, "61.3"),
                (null, 1, null, "abcdefg"),
                (null, 3, TMP_InputField.LineType.MultiLineNewline, "a\nbcd"),
                (null, 0, TMP_InputField.LineType.SingleLine, "ab\ncd"),
                (null, 0, TMP_InputField.LineType.SingleLine, "a\tb"),
                (null, 0, TMP_InputField.LineType.SingleLine, "ab\rcd"),
                (TMP_InputField.ContentType.IntegerNumber, 0, null, "ab c!1"),
                (TMP_InputField.ContentType.Name, 0, null, "-12.5e3"),
                (TMP_InputField.ContentType.Alphanumeric, 0, null, "x\ny"),
                (TMP_InputField.ContentType.EmailAddress, 0, null, "ab c!1"),
                (TMP_InputField.ContentType.Pin, 0, null, "-12.5e3"),
            };
            foreach (var c in cases)
                foreach (State state in System.Enum.GetValues(typeof(State)))
                    foreach (Setter setter in System.Enum.GetValues(typeof(Setter)))
                        yield return new object[] { c.content, c.limit, c.line, c.value, state, setter };
        }

        [Theory]
        [MemberData(nameof(KeptWhole))]
        public void TextSetFromCodeIsKeptWhole(TMP_InputField.ContentType? content, int limit, TMP_InputField.LineType? line,
            string value, State state, Setter setter)
        {
            var p = NewProbe(state, content, limit, line);
            var f = p.Field;
            // Password and Pin show one asterisk per character, a line feed included.
            string label = (content is TMP_InputField.ContentType.Password or TMP_InputField.ContentType.Pin
                ? new string('*', value.Length) : value) + Marker;
            Set(f, setter, value);
            Assert.Equal(value, f.text);
            // text = reports the whole value once, at once, in every state; SetTextWithoutNotify
            // reports nothing.
            Assert.Equal(setter == Setter.Text ? new[] { value } : new string[0], p.Changed);
            if (state != State.Inactive) Assert.Equal(label, f.textComponent.text);
            p.ClearEvents();
            AssertKeptThroughTheSteps(p, value);
            Assert.Equal(label, f.textComponent.text);
            Deselect(f);
            Assert.Equal(value, f.text);
            Assert.Equal(new[] { value }, p.Ended);
            Assert.Empty(p.Changed);
        }

        [Fact]
        public void APasswordLabelShowsAnAsteriskPerCharacter()
        {
            foreach (var content in new[] { TMP_InputField.ContentType.Password, TMP_InputField.ContentType.Pin })
            {
                var p = NewProbe(State.Active, content);
                p.Field.text = "x\ny";
                Assert.Equal("x\ny", p.Field.text);
                Assert.Equal("***" + Marker, p.Field.textComponent.text);
            }
        }

        [Fact]
        public void SettingsMadeAfterTheTextChangeNothing()
        {
            foreach (State state in System.Enum.GetValues(typeof(State)))
            {
                var p = NewProbe(state);
                var f = p.Field;
                f.text = "507.2";
                p.ClearEvents();
                f.characterLimit = 4;
                Assert.Equal("507.2", f.text);
                f.contentType = TMP_InputField.ContentType.IntegerNumber;
                Assert.Equal("507.2", f.text);
                Assert.Empty(p.Changed);

                p = NewProbe(state, line: TMP_InputField.LineType.MultiLineNewline);
                f = p.Field;
                f.text = "ab\ncd\te";
                p.ClearEvents();
                f.lineType = TMP_InputField.LineType.SingleLine;
                Assert.Equal("ab\ncd\te", f.text);
                f.contentType = TMP_InputField.ContentType.Name;
                f.characterLimit = 1;
                Assert.Equal("ab\ncd\te", f.text);
                Assert.Empty(p.Changed);
                AssertKeptThroughTheSteps(p, "ab\ncd\te");
            }
        }

        [Fact]
        public void OnlyATypedCharacterIsLimited()
        {
            var dec = TMP_InputField.ContentType.DecimalNumber;
            foreach (State state in System.Enum.GetValues(typeof(State)))
            {
                // At or over the limit a typed character is refused, with no event; Backspace is
                // not limited and the rest is not cut; deselecting reports the text as it is.
                var p = NewProbe(state, dec, 4);
                var f = p.Field;
                f.text = "507.2";
                if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
                if (!f.isFocused) Focus(f);
                p.ClearEvents();
                End(f);
                TypeChar(f, '9');
                Assert.Equal("507.2", f.text);
                Assert.Empty(p.Changed);
                Backspace(f);
                Assert.Equal("507.", f.text);
                Assert.Equal(new[] { "507." }, p.Changed);
                Deselect(f);
                Assert.Equal(new[] { "507." }, p.Ended);
                Assert.Empty(p.Submitted);

                // Under the limit it is taken.
                p = NewProbe(state, dec, 4);
                f = p.Field;
                f.text = "12";
                if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
                if (!f.isFocused) Focus(f);
                p.ClearEvents();
                End(f);
                TypeChar(f, '9');
                Assert.Equal("129", f.text);
                Assert.Equal(new[] { "129" }, p.Changed);
            }
        }

        [Fact]
        public void TheContentTypeChecksOnlyTheTypedCharacter()
        {
            // IntegerNumber takes "9" at the end of "-12.5e3"; Name refuses it.
            var p = NewProbe(State.Focused, TMP_InputField.ContentType.IntegerNumber);
            p.Field.text = "-12.5e3";
            End(p.Field);
            TypeChar(p.Field, '9');
            Assert.Equal("-12.5e39", p.Field.text);

            p = NewProbe(State.Focused, TMP_InputField.ContentType.Name);
            p.Field.text = "-12.5e3";
            End(p.Field);
            p.ClearEvents();
            TypeChar(p.Field, '9');
            Assert.Equal("-12.5e3", p.Field.text);
            Assert.Empty(p.Changed);

            // The rest is never checked again: a line feed stays in a single-line field through
            // every edit.
            p = NewProbe(State.Focused, TMP_InputField.ContentType.Standard);
            p.Field.text = "x\ny";
            End(p.Field);
            TypeChar(p.Field, '9');
            Assert.Equal("x9\ny", p.Field.text);
            Backspace(p.Field);
            Assert.Equal("x\ny", p.Field.text);
            Deselect(p.Field);
            Assert.Equal("x\ny", p.Field.text);
        }

        [Fact]
        public void BackspaceInAnEmptyFieldStillReports()
        {
            var p = NewProbe(State.Focused, TMP_InputField.ContentType.Name);
            TypeChar(p.Field, '9');
            Assert.Empty(p.Changed);
            Backspace(p.Field);
            Assert.Equal("", p.Field.text);
            Assert.Equal(new[] { "" }, p.Changed);
        }

        [Fact]
        public void TheTextComponentsOwnTextIsPutBackOnAnEnabledField()
        {
            foreach (var state in new[] { State.Active, State.Focused })
            {
                var p = NewProbe(state, TMP_InputField.ContentType.DecimalNumber, 4);
                var f = p.Field;
                f.textComponent.text = "507.2";
                Assert.Equal(Marker, f.textComponent.text);
                Assert.Equal("", f.text);
                Assert.Empty(p.Changed);

                // With text, the field's text and the marker.
                f.SetTextWithoutNotify("12");
                f.textComponent.text = "507.2";
                Assert.Equal("12" + Marker, f.textComponent.text);
                Assert.Equal("12", f.text);
                Assert.Empty(p.Changed);
            }

            // On an inactive field it keeps the value until the field is enabled.
            var q = NewProbe(State.Inactive, TMP_InputField.ContentType.DecimalNumber, 4);
            q.Field.textComponent.text = "507.2";
            Assert.Equal("507.2", q.Field.textComponent.text);
            Frame();
            Assert.Equal("507.2", q.Field.textComponent.text);
            q.Field.gameObject.SetActive(true);
            Assert.Equal(Marker, q.Field.textComponent.text);
            Assert.Equal("", q.Field.text);
            Assert.Empty(q.Changed);
            // Edits work on the field's text: "9" typed afterwards gives "9".
            Focus(q.Field);
            End(q.Field);
            TypeChar(q.Field, '9');
            Assert.Equal("9", q.Field.text);
            Assert.Equal("9" + Marker, q.Field.textComponent.text);

            // Setting the limit writes the label again, also on an inactive field.
            q = NewProbe(State.Inactive, TMP_InputField.ContentType.DecimalNumber);
            q.Field.textComponent.text = "507.2";
            q.Field.characterLimit = 4;
            Assert.Equal(Marker, q.Field.textComponent.text);
            Assert.Equal("", q.Field.text);
            Assert.Empty(q.Changed);
        }

        [Fact]
        public void ActivationLeavesTheCaretWhereItWas()
        {
            var p = NewProbe(State.Active);
            p.Field.text = "abcdefg";
            Focus(p.Field);
            Assert.Equal(0, p.Field.caretPosition);
            Assert.Equal(0, p.Field.selectionAnchorPosition);
            End(p.Field);
            Assert.Equal(7, p.Field.caretPosition);
        }

        [Fact]
        public void EndGoesToTheEndOfTheCaretsLine()
        {
            // A line feed in a single-line field ends a line: "9" goes in after "ab".
            var p = NewProbe(State.Focused);
            p.Field.text = "ab\ncd";
            Canvas.ForceUpdateCanvases();
            End(p.Field);
            TypeChar(p.Field, '9');
            Assert.Equal("ab9\ncd", p.Field.text);
            // End again stays before the line feed; on the last line it goes to the text's end.
            End(p.Field);
            Assert.Equal(3, p.Field.stringPosition);
            p.Field.ProcessEvent(Key(KeyCode.DownArrow));
            End(p.Field);
            Assert.Equal(6, p.Field.stringPosition);

            // In a 64-wide multi-line field "a\tb" wraps after the tab: "9" goes in after "a".
            p = NewProbe(State.Focused, line: TMP_InputField.LineType.MultiLineNewline, width: 64f);
            p.Field.text = "a\tb";
            Canvas.ForceUpdateCanvases();
            Assert.Equal(2, p.Field.textComponent.textInfo.lineCount);
            End(p.Field);
            TypeChar(p.Field, '9');
            Assert.Equal("a9\tb", p.Field.text);
        }
    }
}
