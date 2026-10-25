using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using STRINGS;
using UnityEngine;
using UnityEngine.UI;

namespace OniBrutal.GameTweaks
{
    /// The main menu's "Moono Tweaks" button, directly below Mods (above Options when there is no
    /// Mods button), and the screen it opens: the player's options (OniBrutal.Host.BrutalTweaksMenu
    /// gives the rows, from the one catalog GameTweakNames.Rows), in sections, each with an on/off
    /// toggle, saved to the player's settings file and applied by the game's own restart
    /// (App.Restart, through the player's Restarter).
    ///
    /// Everything on it is the game's: the button is made as MainMenu makes its own, and the
    /// screen is an instance of the Mods screen's prefab (its window, list, rows, toggles and
    /// buttons) whose ModsScreen behaviour is swapped for BrutalTweaksScreen before it wakes.
    public static class BrutalTweaksMenu
    {
        internal static string[] Ids, Groups, GroupGains, Titles, Effects, Gains, Tooltips;
        internal static bool[] ChangesGameplay, Running, Defaults, Saved, Locked;
        internal static string LockedNote, SettingsPath;
        internal static Func<string[], bool[], string> Save;

        /// One entry per row, in order: its section (`groups`; `groupGains` the gain the section's
        /// rows share, "" for none), title, one-line effect, measured gain ("" for none) and
        /// tooltip. `running`: each option as this run has it; `defaults`: as Reset to defaults
        /// sets it; `locked`: set by the command line (shown, not changeable; `lockedNote` names
        /// those arguments). `save(ids, values)` writes the changeable rows to the settings file,
        /// returning why it could not or null.
        public static void Install(Harmony harmony, string[] ids, string[] groups, string[] groupGains, string[] titles, string[] effects,
            string[] gains, string[] tooltips, bool[] changesGameplay, bool[] running, bool[] defaults, bool[] locked,
            string lockedNote, string settingsPath, Func<string[], bool[], string> save)
        {
            Ids = ids;
            Groups = groups;
            GroupGains = groupGains;
            Titles = titles;
            Effects = effects;
            Gains = gains;
            Tooltips = tooltips;
            ChangesGameplay = changesGameplay;
            Running = running;
            Defaults = defaults;
            Saved = (bool[])running.Clone();
            Locked = locked;
            LockedNote = lockedNote;
            SettingsPath = settingsPath;
            Save = save;
            harmony.Patch(AccessTools.Method(typeof(MainMenu), "OnPrefabInit"), postfix: new HarmonyMethod(typeof(BrutalTweaksMenu), nameof(AddButton)));
        }

        /// After MainMenu.OnPrefabInit has made its buttons: one more, as its MakeButton makes them
        /// (the menu's button prefab, normal style, 14 pt).
        static void AddButton(MainMenu __instance)
        {
            try
            {
                var menu = Traverse.Create(__instance);
                var prefab = menu.Field("buttonPrefab").GetValue<KButton>();
                var parent = menu.Field("buttonParent").GetValue<GameObject>();
                var style = menu.Field("normalButtonStyle").GetValue<ColorStyleSetting>();
                var button = Util.KInstantiateUI<KButton>(prefab.gameObject, parent, force_active: true);
                button.name = "BrutalTweaksButton";
                button.onClick += () => Open(__instance);
                var image = button.GetComponent<KImage>();
                image.colorStyleSetting = style;
                image.ApplyColorStyleSetting();
                var label = button.GetComponentInChildren<LocText>();
                label.text = "MOONO TWEAKS";
                label.fontSize = 14;
                int index = -1;
                foreach (Transform child in parent.transform)
                {
                    var text = child == button.transform ? null : child.GetComponentInChildren<LocText>()?.text;
                    if (text == (string)UI.FRONTEND.MODS.TITLE) index = child.GetSiblingIndex() + 1;
                    else if (index < 0 && text == (string)UI.FRONTEND.MAINMENU.OPTIONS) index = child.GetSiblingIndex();
                }
                if (index >= 0) button.transform.SetSiblingIndex(index);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Moono] the Moono Tweaks button could not be added: " + e);
            }
        }

        static void Open(MainMenu menu)
        {
            // The Mods screen's prefab, instantiated inactive so that ModsScreen never wakes.
            var prefab = ScreenPrefabs.Instance.modsMenu.gameObject;
            bool active = prefab.activeSelf;
            prefab.SetActive(false);
            GameObject go;
            try
            {
                go = UnityEngine.Object.Instantiate(prefab, menu.transform.parent, worldPositionStays: false);
            }
            finally
            {
                prefab.SetActive(active);
            }
            go.name = "BrutalTweaksScreen";
            var mods = go.GetComponent<ModsScreen>();
            var screen = go.AddComponent<BrutalTweaksScreen>();
            screen.TakeOver(mods);
            UnityEngine.Object.DestroyImmediate(mods);
            go.SetActive(true);
        }
    }

    /// The Moono Tweaks screen, on the Mods screen's prefab (BrutalTweaksMenu.Open): a section
    /// heading per group, then its rows (title, one-line effect, measured gain on the right,
    /// toggle); the footer says when changes apply beside Reset to defaults, Restart to apply and
    /// Close.
    public class BrutalTweaksScreen : KModalScreen
    {
        const string Title = "MOONO TWEAKS";
        /// The warning colour of the gameplay-changing section and its rows' label (amber).
        const string Warning = "FFB040";
        /// Rows tighter than the Mods screen's (10 px padding and spacing): more of them fit.
        const int RowPadding = 4, RowSpacing = 4;

        KButton closeTitle, close, apply, reset;
        GameObject entryPrefab;
        Transform entryParent;
        LocText windowTitle;
        bool[] shown;
        MultiToggle[] toggles;

        /// The Mods screen's serialized state: its KScreen settings and its parts.
        internal void TakeOver(ModsScreen mods)
        {
            for (var type = typeof(KModalScreen); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    field.SetValue(this, field.GetValue(mods));
            var parts = Traverse.Create(mods);
            closeTitle = parts.Field("closeButtonTitle").GetValue<KButton>();
            close = parts.Field("closeButton").GetValue<KButton>();
            apply = parts.Field("toggleAllButton").GetValue<KButton>();
            reset = parts.Field("workshopButton").GetValue<KButton>();
            entryPrefab = parts.Field("entryPrefab").GetValue<GameObject>();
            entryParent = parts.Field("entryParent").GetValue<Transform>();
            windowTitle = parts.Field("windowTitle").GetValue<LocText>();
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            windowTitle.text = Title;
            windowTitle.ApplySettings();
            closeTitle.onClick += Close;
            close.onClick += Close;
            apply.GetComponentInChildren<LocText>().text = "RESTART TO APPLY";
            apply.onClick += Apply;
            // The Mods screen's Workshop button, as Reset to defaults (wider for its label).
            reset.GetComponentInChildren<LocText>().text = "RESET TO DEFAULTS";
            var resetRect = reset.GetComponent<RectTransform>();
            resetRect.sizeDelta = new Vector2(166f, resetRect.sizeDelta.y);
            reset.onClick += ResetToDefaults;
            reset.gameObject.SetActive(true);
            FooterNote("Changes apply when the game restarts.");
            var list = entryParent.GetComponent<VerticalLayoutGroup>();
            if (list != null)
            {
                list.spacing = RowSpacing;
                list.padding = new RectOffset(list.padding.left, list.padding.right, RowSpacing, RowSpacing);
            }
            // Choices saved earlier in this run (closed with "Later") show as chosen.
            shown = (bool[])BrutalTweaksMenu.Saved.Clone();
            toggles = new MultiToggle[shown.Length];
            if (BrutalTweaksMenu.LockedNote.Length > 0)
                Note($"<b>Set by the launch options:</b> {BrutalTweaksMenu.LockedNote}\n<size=11>Those rows can't be changed here. Remove the options from the game's launch options to choose them here.</size>");
            string group = null;
            for (int i = 0; i < BrutalTweaksMenu.Ids.Length; i++)
            {
                if (BrutalTweaksMenu.Groups[i] != group)
                {
                    group = BrutalTweaksMenu.Groups[i];
                    Heading(group, BrutalTweaksMenu.GroupGains[i], BrutalTweaksMenu.ChangesGameplay[i]);
                }
                Row(i);
            }
            Refresh();
        }

        HierarchyReferences Entry(string name)
        {
            var entry = Util.KInstantiateUI<HierarchyReferences>(entryPrefab, entryParent.gameObject);
            entry.name = name;
            // The rows keep their order: no dragging.
            var drag = entry.GetComponent<DragMe>();
            if (drag != null) Destroy(drag);
            entry.transform.Find("DragReorderIndicator")?.gameObject.SetActive(false);
            entry.GetReference<KButton>("ManageButton").gameObject.SetActive(false);
            var bg = entry.GetReference<KImage>("BG");
            bg.defaultState = KImage.ColorSelector.Inactive;
            bg.ColorState = KImage.ColorSelector.Inactive;
            var layout = entry.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.padding = new RectOffset(layout.padding.left, layout.padding.right, RowPadding, RowPadding);
            return entry;
        }

        void Note(string text)
        {
            var entry = Entry("Note");
            entry.GetReference<LocText>("Title").text = text;
            entry.GetReference<LocText>("Version").gameObject.SetActive(false);
            entry.GetReference<MultiToggle>("EnabledToggle").gameObject.SetActive(false);
            entry.gameObject.SetActive(true);
        }

        /// A section's heading: its name in capitals on the list's background (no row panel),
        /// and the gain its rows share on the right.
        void Heading(string group, string gain, bool changesGameplay)
        {
            var entry = Entry("Heading " + group);
            entry.GetReference<KImage>("BG").gameObject.SetActive(false);
            var layout = entry.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.padding = new RectOffset(layout.padding.left, layout.padding.right, 6, 0);
            var title = entry.GetReference<LocText>("Title");
            title.text = changesGameplay ? $"<color=#{Warning}><b>{group.ToUpperInvariant()}</b></color>  <size=11>off by default: these trade a little exactness for speed</size>"
                : $"<b>{group.ToUpperInvariant()}</b>";
            title.fontSize = 14;
            entry.GetReference<MultiToggle>("EnabledToggle").gameObject.SetActive(false);
            var label = entry.GetReference<LocText>("Version");
            if (gain.Length > 0) SideLabel(label, $"<color={Constants.POSITIVE_COLOR_STR}>{gain}</color>", 0f);
            else label.gameObject.SetActive(false);
            entry.gameObject.SetActive(true);
        }

        /// The label left of the toggle (the Mods screen's version text): sized to its text, or
        /// `width` wide and wrapped.
        static void SideLabel(LocText label, string text, float width)
        {
            label.text = text;
            label.fontSize = 12;
            label.alignment = TMPro.TextAlignmentOptions.MidlineRight;
            var layout = label.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.flexibleWidth = 0f;
                if (width > 0f) layout.preferredWidth = width;
            }
        }

        void Row(int i)
        {
            var entry = Entry(BrutalTweaksMenu.Ids[i]);
            bool locked = BrutalTweaksMenu.Locked[i];
            var title = entry.GetReference<LocText>("Title");
            title.text = $"{BrutalTweaksMenu.Titles[i]}\n<size=11>{BrutalTweaksMenu.Effects[i]}</size>";
            entry.GetReference<ToolTip>("Description").toolTip = BrutalTweaksMenu.Tooltips[i];
            // The label beside the toggle: the measured gain, the warning for tweaks that change
            // gameplay, and who set a row the launch options set.
            var label = entry.GetReference<LocText>("Version");
            var labels = new List<string>();
            if (BrutalTweaksMenu.Gains[i].Length > 0) labels.Add($"<color={Constants.POSITIVE_COLOR_STR}>{BrutalTweaksMenu.Gains[i]}</color>");
            if (BrutalTweaksMenu.ChangesGameplay[i]) labels.Add($"<color=#{Warning}><b>CHANGES GAMEPLAY</b></color>");
            if (locked) labels.Add("LAUNCH OPTION");
            if (labels.Count > 0) SideLabel(label, string.Join("\n", labels), BrutalTweaksMenu.Gains[i].Length > 0 ? 240f : 0f);
            else label.gameObject.SetActive(false);
            var toggle = entry.GetReference<MultiToggle>("EnabledToggle");
            toggles[i] = toggle;
            toggle.ChangeState(shown[i] ? 1 : 0);
            var tip = toggle.GetComponent<ToolTip>();
            if (locked)
            {
                // Dimmed, and the toggle does nothing.
                var bg = entry.GetReference<KImage>("BG");
                bg.defaultState = KImage.ColorSelector.Disabled;
                bg.ColorState = KImage.ColorSelector.Disabled;
                title.color = new Color(title.color.r, title.color.g, title.color.b, 0.55f);
                var dim = toggle.gameObject.AddComponent<CanvasGroup>();
                dim.alpha = 0.45f;
                if (tip != null) tip.OnToolTip = () => $"Set by the launch options ({(shown[i] ? "on" : "off")})";
            }
            else
            {
                toggle.onClick += () =>
                {
                    shown[i] = !shown[i];
                    toggle.ChangeState(shown[i] ? 1 : 0);
                    Refresh();
                };
                if (tip != null) tip.OnToolTip = () => shown[i] ? UI.FRONTEND.MODS.TOOLTIPS.ENABLED : UI.FRONTEND.MODS.TOOLTIPS.DISABLED;
            }
            entry.gameObject.SetActive(true);
        }

        /// A line of text at the footer's left, beside its buttons (a copy of the rows' label).
        void FooterNote(string text)
        {
            var source = entryPrefab.GetComponent<HierarchyReferences>().GetReference<LocText>("Version");
            var note = Instantiate(source.gameObject, close.transform.parent, worldPositionStays: false);
            note.name = "RestartNote";
            note.SetActive(true);
            var layout = note.GetComponent<LayoutElement>();
            if (layout == null) layout = note.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var rect = note.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(14f, 0f);
            rect.sizeDelta = new Vector2(200f, 0f);
            var label = note.GetComponent<LocText>();
            label.text = text;
            label.fontSize = 13;
            label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
        }

        static bool Differ(bool[] a, bool[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return true;
            return false;
        }

        /// "Restart to apply" shows once the choices differ from what this run has; Reset to
        /// defaults is available while a changeable row is off its default.
        void Refresh()
        {
            apply.gameObject.SetActive(Differ(shown, BrutalTweaksMenu.Running));
            bool offDefault = false;
            for (int i = 0; i < shown.Length; i++)
                if (!BrutalTweaksMenu.Locked[i] && shown[i] != BrutalTweaksMenu.Defaults[i]) offDefault = true;
            reset.isInteractable = offDefault;
        }

        /// Every row the launch options did not set back to its default, as if toggled.
        void ResetToDefaults()
        {
            for (int i = 0; i < shown.Length; i++)
            {
                if (BrutalTweaksMenu.Locked[i]) continue;
                shown[i] = BrutalTweaksMenu.Defaults[i];
                toggles[i].ChangeState(shown[i] ? 1 : 0);
            }
            Refresh();
        }

        /// The changeable rows to the settings file; false (after saying why) when it failed.
        bool SaveShown()
        {
            var ids = new List<string>();
            var values = new List<bool>();
            for (int i = 0; i < shown.Length; i++)
            {
                if (BrutalTweaksMenu.Locked[i]) continue;
                ids.Add(BrutalTweaksMenu.Ids[i]);
                values.Add(shown[i]);
            }
            var error = BrutalTweaksMenu.Save(ids.ToArray(), values.ToArray());
            if (error != null)
            {
                KMod.Manager.Dialog(gameObject, Title, $"The settings could not be saved to {BrutalTweaksMenu.SettingsPath}:\n{error}");
                return false;
            }
            for (int i = 0; i < shown.Length; i++)
                if (!BrutalTweaksMenu.Locked[i]) BrutalTweaksMenu.Saved[i] = shown[i];
            return true;
        }

        void Apply()
        {
            if (SaveShown()) App.instance.Restart();
        }

        /// Close (or Escape): the choices are kept; when they differ from this run's, the game's
        /// confirm dialog offers the restart that applies them.
        void Close()
        {
            if (Differ(shown, BrutalTweaksMenu.Saved) && !SaveShown()) return;
            if (Differ(BrutalTweaksMenu.Saved, BrutalTweaksMenu.Running))
                KMod.Manager.Dialog(gameObject, Title, "Your changes are saved and apply when the game restarts.",
                    UI.FRONTEND.MOD_DIALOGS.RESTART.OK, App.instance.Restart, "LATER", Deactivate);
            else Deactivate();
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (!e.Consumed && e.TryConsume(global::Action.Escape)) Close();
            base.OnKeyDown(e);
        }
    }
}
