using System;
using System.Collections.Generic;
using OniMoono.Engine.IMGUI;

namespace UnityEngine
{
    /// The editing state of a text field: the text, the cursor and selection (string indices),
    /// and the editing operations with Windows and Linux key bindings. Text fields keep one per
    /// control as a GUIUtility state object; scripts also use it directly for the clipboard.
    public partial class TextEditor
    {
        public TouchScreenKeyboard keyboardOnScreen;
        public int controlID;
        public GUIStyle style = GUIStyle.none;
        public bool multiline;
        public bool hasHorizontalCursorPos;
        public bool isPasswordField;
        public Vector2 scrollOffset;

        string value = "";
        int cursor, anchor;
        Rect area;
        // The column Up and Down keep when lines are shorter.
        int column = -1;
        string backupText;
        int backupCursor, backupAnchor;
        bool hasBackup;

        public TextEditor() { }

        public string text
        {
            get => value;
            set
            {
                this.value = value ?? "";
                cursor = Mathf.Clamp(cursor, 0, this.value.Length);
                anchor = Mathf.Clamp(anchor, 0, this.value.Length);
            }
        }

        /// The rect the text is edited in (the text field's rect).
        public Rect position
        {
            get => area;
            set => area = value;
        }

        /// Where the cursor is: the string index it stands before.
        public int cursorIndex
        {
            get => cursor;
            set => cursor = Mathf.Clamp(value, 0, this.value.Length);
        }

        /// The other end of the selection; equal to cursorIndex when nothing is selected.
        public int selectIndex
        {
            get => anchor;
            set => anchor = Mathf.Clamp(value, 0, this.value.Length);
        }

        public bool hasSelection => cursor != anchor;

        public string SelectedText => hasSelection ? value.Substring(SelectionStart, SelectionEnd - SelectionStart) : "";

        int SelectionStart => Mathf.Min(cursor, anchor);
        int SelectionEnd => Mathf.Max(cursor, anchor);

        void Place(int index)
        {
            cursor = anchor = Mathf.Clamp(index, 0, value.Length);
            column = -1;
        }

        void Extend(int index)
        {
            cursor = Mathf.Clamp(index, 0, value.Length);
            column = -1;
        }

        // ----- Positions -----

        /// A word is a run of letters and digits.
        static bool IsWord(char c) => char.IsLetterOrDigit(c);

        /// The end of the next word: spaces and punctuation are skipped, then the word.
        int WordRightOf(int i)
        {
            while (i < value.Length && !IsWord(value[i])) i++;
            while (i < value.Length && IsWord(value[i])) i++;
            return i;
        }

        /// The start of the previous word.
        int WordLeftOf(int i)
        {
            while (i > 0 && !IsWord(value[i - 1])) i--;
            while (i > 0 && IsWord(value[i - 1])) i--;
            return i;
        }

        int LineStartOf(int i) => i <= 0 ? 0 : value.LastIndexOf('\n', i - 1) + 1;

        int LineEndOf(int i)
        {
            int n = value.IndexOf('\n', Mathf.Clamp(i, 0, value.Length));
            return n < 0 ? value.Length : n;
        }

        /// The column Up and Down keep: the one the first of a run of them started from.
        int KeptColumn() => column >= 0 ? column : cursor - LineStartOf(cursor);

        /// The index on the line above (below) at `col`; the text's start (end) from the first
        /// (last) line.
        int Vertical(bool up, int col)
        {
            int start = LineStartOf(cursor);
            if (up)
            {
                if (start == 0) return 0;
                int previous = LineStartOf(start - 1);
                return Mathf.Min(previous + col, start - 1);
            }
            int end = LineEndOf(cursor);
            if (end >= value.Length) return value.Length;
            int next = end + 1;
            return Mathf.Min(next + col, LineEndOf(next));
        }

        void MoveVertically(bool up, bool select)
        {
            int col = KeptColumn();
            int target = Vertical(up, col);
            if (select) Extend(target);
            else Place(target);
            column = col;
        }

        // ----- Moving -----

        /// With a selection, Left collapses it to its start.
        public void MoveLeft()
        {
            if (hasSelection) Place(SelectionStart);
            else Place(cursor - 1);
        }

        /// With a selection, Right collapses it to its end.
        public void MoveRight()
        {
            if (hasSelection) Place(SelectionEnd);
            else Place(cursor + 1);
        }

        public void MoveUp() => MoveVertically(true, false);

        public void MoveDown() => MoveVertically(false, false);

        public void MoveWordLeft() => Place(WordLeftOf(hasSelection ? SelectionStart : cursor));

        public void MoveWordRight() => Place(WordRightOf(hasSelection ? SelectionEnd : cursor));

        public void MoveLineStart() => Place(LineStartOf(hasSelection ? SelectionStart : cursor));

        public void MoveLineEnd() => Place(LineEndOf(hasSelection ? SelectionEnd : cursor));

        public void MoveTextStart() => Place(0);

        public void MoveTextEnd() => Place(value.Length);

        /// Puts the cursor at the character nearest to a point in the text field.
        public void MoveCursorToPosition(Vector2 cursorPosition) => Place(IndexAt(cursorPosition));

        /// Moves the cursor end of the selection to the character nearest to a point.
        public void SelectToPosition(Vector2 cursorPosition) => Extend(IndexAt(cursorPosition));

        int IndexAt(Vector2 point)
        {
            var s = style ?? GUIStyle.none;
            return s.GetCursorStringIndex(area, Content(), point + scrollOffset);
        }

        // ----- Selecting -----

        public void SelectLeft() => Extend(cursor - 1);

        public void SelectRight() => Extend(cursor + 1);

        public void SelectUp() => MoveVertically(true, true);

        public void SelectDown() => MoveVertically(false, true);

        public void SelectWordLeft() => Extend(WordLeftOf(cursor));

        public void SelectWordRight() => Extend(WordRightOf(cursor));

        public void SelectTextStart() => Extend(0);

        public void SelectTextEnd() => Extend(value.Length);

        void SelectLineStart() => Extend(LineStartOf(cursor));

        void SelectLineEnd() => Extend(LineEndOf(cursor));

        public void SelectAll()
        {
            anchor = value.Length;
            cursor = 0;
            column = -1;
        }

        public void SelectNone() => Place(cursor);

        /// Selects the word at the cursor (or the run of spaces or punctuation there).
        public void SelectCurrentWord()
        {
            if (value.Length == 0)
            {
                Place(0);
                return;
            }
            int at = cursor < value.Length && (IsWord(value[cursor]) || cursor == 0 || !IsWord(value[cursor - 1])) ? cursor : cursor - 1;
            at = Mathf.Clamp(at, 0, value.Length - 1);
            bool word = IsWord(value[at]);
            int start = at, end = at + 1;
            while (start > 0 && IsWord(value[start - 1]) == word && value[start - 1] != '\n') start--;
            while (end < value.Length && IsWord(value[end]) == word && value[end] != '\n') end++;
            anchor = start;
            cursor = end;
            column = -1;
        }

        /// Selects the line (between new lines) the cursor is on.
        public void SelectCurrentParagraph()
        {
            anchor = LineStartOf(cursor);
            cursor = LineEndOf(cursor);
            column = -1;
        }

        // ----- Editing -----

        /// Replaces the selection (or inserts at the cursor) and puts the cursor after it.
        public void ReplaceSelection(string replace)
        {
            replace ??= "";
            int start = SelectionStart;
            value = value.Remove(start, SelectionEnd - start).Insert(start, replace);
            Place(start + replace.Length);
        }

        public void Insert(char c) => ReplaceSelection(c.ToString());

        bool DeleteSelection()
        {
            if (!hasSelection) return false;
            ReplaceSelection("");
            return true;
        }

        /// Deletes the selection, else the character after the cursor.
        public bool Delete()
        {
            if (DeleteSelection()) return true;
            if (cursor >= value.Length) return false;
            value = value.Remove(cursor, 1);
            Place(cursor);
            return true;
        }

        /// Deletes the selection, else the character before the cursor.
        public bool Backspace()
        {
            if (DeleteSelection()) return true;
            if (cursor <= 0) return false;
            value = value.Remove(cursor - 1, 1);
            Place(cursor - 1);
            return true;
        }

        public bool DeleteWordBack()
        {
            if (DeleteSelection()) return true;
            int start = WordLeftOf(cursor);
            if (start == cursor) return false;
            value = value.Remove(start, cursor - start);
            Place(start);
            return true;
        }

        public bool DeleteWordForward()
        {
            if (DeleteSelection()) return true;
            int end = WordRightOf(cursor);
            if (end == cursor) return false;
            value = value.Remove(cursor, end - cursor);
            Place(cursor);
            return true;
        }

        /// Puts the selection, or the whole text when nothing is selected, on the clipboard.
        public void Copy()
        {
            if (isPasswordField) return;
            GUIUtility.systemCopyBuffer = hasSelection ? SelectedText : value;
        }

        /// Copies the selection to the clipboard and deletes it.
        public bool Cut()
        {
            if (isPasswordField || !hasSelection) return false;
            Copy();
            return DeleteSelection();
        }

        /// Replaces the selection with the clipboard; a single-line editor turns new lines
        /// into spaces.
        public bool Paste()
        {
            var clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clip)) return false;
            if (!multiline) clip = clip.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
            ReplaceSelection(clip);
            return true;
        }

        public void SaveBackup()
        {
            backupText = value;
            backupCursor = cursor;
            backupAnchor = anchor;
            hasBackup = true;
        }

        public void Undo()
        {
            if (!hasBackup) return;
            value = backupText;
            cursor = Mathf.Clamp(backupCursor, 0, value.Length);
            anchor = Mathf.Clamp(backupAnchor, 0, value.Length);
            column = -1;
        }

        // ----- Keys -----

        const EventModifiers Ctrl = EventModifiers.Control, Shift = EventModifiers.Shift;

        static readonly Dictionary<(KeyCode, EventModifiers), Func<TextEditor, bool>> bindings = new()
        {
            [(KeyCode.LeftArrow, 0)] = t => Do(t.MoveLeft),
            [(KeyCode.RightArrow, 0)] = t => Do(t.MoveRight),
            [(KeyCode.UpArrow, 0)] = t => Do(t.MoveUp),
            [(KeyCode.DownArrow, 0)] = t => Do(t.MoveDown),
            [(KeyCode.LeftArrow, Shift)] = t => Do(t.SelectLeft),
            [(KeyCode.RightArrow, Shift)] = t => Do(t.SelectRight),
            [(KeyCode.UpArrow, Shift)] = t => Do(t.SelectUp),
            [(KeyCode.DownArrow, Shift)] = t => Do(t.SelectDown),
            [(KeyCode.LeftArrow, Ctrl)] = t => Do(t.MoveWordLeft),
            [(KeyCode.RightArrow, Ctrl)] = t => Do(t.MoveWordRight),
            [(KeyCode.LeftArrow, Ctrl | Shift)] = t => Do(t.SelectWordLeft),
            [(KeyCode.RightArrow, Ctrl | Shift)] = t => Do(t.SelectWordRight),
            [(KeyCode.Home, 0)] = t => Do(t.MoveLineStart),
            [(KeyCode.End, 0)] = t => Do(t.MoveLineEnd),
            [(KeyCode.Home, Shift)] = t => Do(t.SelectLineStart),
            [(KeyCode.End, Shift)] = t => Do(t.SelectLineEnd),
            [(KeyCode.Home, Ctrl)] = t => Do(t.MoveTextStart),
            [(KeyCode.End, Ctrl)] = t => Do(t.MoveTextEnd),
            [(KeyCode.Home, Ctrl | Shift)] = t => Do(t.SelectTextStart),
            [(KeyCode.End, Ctrl | Shift)] = t => Do(t.SelectTextEnd),
            [(KeyCode.Backspace, 0)] = t => t.Edit(t.Backspace),
            [(KeyCode.Backspace, Shift)] = t => t.Edit(t.Backspace),
            [(KeyCode.Delete, 0)] = t => t.Edit(t.Delete),
            [(KeyCode.Backspace, Ctrl)] = t => t.Edit(t.DeleteWordBack),
            [(KeyCode.Delete, Ctrl)] = t => t.Edit(t.DeleteWordForward),
            [(KeyCode.A, Ctrl)] = t => Do(t.SelectAll),
            [(KeyCode.C, Ctrl)] = t => Do(t.Copy),
            [(KeyCode.Insert, Ctrl)] = t => Do(t.Copy),
            [(KeyCode.X, Ctrl)] = t => t.Edit(t.Cut),
            [(KeyCode.Delete, Shift)] = t => t.Edit(t.Cut),
            [(KeyCode.V, Ctrl)] = t => t.Edit(t.Paste),
            [(KeyCode.Insert, Shift)] = t => t.Edit(t.Paste),
            [(KeyCode.Z, Ctrl)] = t => Do(t.Undo),
        };

        static bool Do(Action action)
        {
            action();
            return true;
        }

        /// An editing key: the state before it is what Undo restores.
        bool Edit(Func<bool> action)
        {
            var before = (value, cursor, anchor);
            if (action())
            {
                backupText = before.value;
                backupCursor = before.cursor;
                backupAnchor = before.anchor;
                hasBackup = true;
            }
            return true;
        }

        /// Performs the editing action bound to a key press; true when the key is bound. The
        /// platform's function-key, numeric and caps-lock flags do not take part in matching.
        public bool HandleKeyEvent(Event e)
        {
            if (e == null || e.rawType != EventType.KeyDown || e.keyCode == KeyCode.None) return false;
            var mods = e.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command);
            return bindings.TryGetValue((e.keyCode, mods), out var action) && action(this);
        }

        // ----- Focus and drawing -----

        /// Gaining focus selects the whole text.
        public void OnFocus()
        {
            SelectAll();
            scrollOffset = Vector2.zero;
        }

        public void OnLostFocus()
        {
            hasHorizontalCursorPos = false;
            scrollOffset = Vector2.zero;
            column = -1;
        }

        GUIContent Content() => GUIContent.Scratch(value);

        /// Draws `newText` with the selection and cursor, scrolled so the cursor is visible.
        public void DrawCursor(string newText)
        {
            var s = style ?? GUIStyle.none;
            var shown = newText ?? value;
            KeepCursorVisible(s, shown);
            s.DrawEditing(area, GUIContent.Scratch(shown), controlID, cursor, anchor, scrollOffset, true);
        }

        void KeepCursorVisible(GUIStyle s, string shown)
        {
            var carets = s.CaretsIn(area, shown, out float lineHeight);
            var caret = carets[Mathf.Clamp(cursor, 0, carets.Count - 1)].Position;
            var view = s.ContentRect(area).size;
            if (caret.x - scrollOffset.x > view.x - 1f) scrollOffset.x = caret.x - view.x + 1f;
            if (caret.x - scrollOffset.x < 0f) scrollOffset.x = caret.x;
            if (multiline)
            {
                if (caret.y + lineHeight - scrollOffset.y > view.y) scrollOffset.y = caret.y + lineHeight - view.y;
                if (caret.y - scrollOffset.y < 0f) scrollOffset.y = caret.y;
            }
            else
            {
                scrollOffset.y = 0f;
            }
            scrollOffset.x = Mathf.Max(0f, scrollOffset.x);
            scrollOffset.y = Mathf.Max(0f, scrollOffset.y);
        }
    }
}
