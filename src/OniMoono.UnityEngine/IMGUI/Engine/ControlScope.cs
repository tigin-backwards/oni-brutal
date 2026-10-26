using System.Collections.Generic;
using UnityEngine;

namespace OniMoono.Engine.IMGUI
{
    /// The control IDs of one container (a behaviour's OnGUI, or one window's function), kept
    /// across events so that the same control gets the same ID each time.
    ///
    /// Each pass walks the list from the start: a call whose hint matches the entry at the
    /// cursor takes it; otherwise it takes the next later entry with its hint (the entries in
    /// between were skipped this pass and keep their IDs); otherwise a new ID is inserted at
    /// the cursor. IDs are unique across all containers.
    internal sealed class ControlScope
    {
        static int lastId;
        /// Which container each ID belongs to (input routing to windows).
        static readonly Dictionary<int, ControlScope> owners = new();

        readonly List<(int Hint, int Id)> entries = new();
        int cursor;

        /// The keyboard-focusable controls of the current pass, in call order (Tab moves
        /// through them), and of the last complete pass.
        List<int> focusable = new();
        List<int> lastFocusable = new();

        /// GUI.SetNextControlName names, by ID.
        readonly Dictionary<int, string> names = new();

        /// The window this container belongs to, if any.
        public readonly WindowRecord Window;

        public ControlScope(WindowRecord window = null)
        {
            Window = window;
        }

        public static ControlScope OwnerOf(int id) => id != 0 && owners.TryGetValue(id, out var s) ? s : null;

        public void BeginPass()
        {
            cursor = 0;
            (lastFocusable, focusable) = (focusable, lastFocusable);
            focusable.Clear();
        }

        public int Next(int hint, FocusType focus)
        {
            int id = 0;
            if (cursor < entries.Count && entries[cursor].Hint == hint)
            {
                id = entries[cursor++].Id;
            }
            else
            {
                for (int j = cursor + 1; j < entries.Count; j++)
                {
                    if (entries[j].Hint != hint) continue;
                    id = entries[j].Id;
                    cursor = j + 1;
                    break;
                }
                if (id == 0)
                {
                    id = ++lastId;
                    entries.Insert(cursor++, (hint, id));
                    owners[id] = this;
                }
            }
            if (focus == FocusType.Keyboard) focusable.Add(id);
            var name = GUIStateData.NextControlName;
            if (name != null)
            {
                names[id] = name;
                GUIStateData.NextControlName = null;
            }
            return id;
        }

        /// The control after (or before) `current` in focus order; the first (last) when
        /// `current` is not one of them. 0 when there are none.
        public int NextFocus(int current, bool backwards)
        {
            var list = focusable.Count > 0 ? focusable : lastFocusable;
            if (list.Count == 0) return 0;
            int i = list.IndexOf(current);
            if (i < 0) return backwards ? list[^1] : list[0];
            return list[(i + (backwards ? list.Count - 1 : 1)) % list.Count];
        }

        public string NameOf(int id) => names.TryGetValue(id, out var n) ? n : null;

        public int Named(string name)
        {
            foreach (var (id, n) in names)
                if (n == name) return id;
            return 0;
        }

        /// The container is gone (its behaviour or window): its IDs no longer route anywhere.
        public void Release()
        {
            foreach (var (_, id) in entries) owners.Remove(id);
            entries.Clear();
            names.Clear();
        }
    }
}
