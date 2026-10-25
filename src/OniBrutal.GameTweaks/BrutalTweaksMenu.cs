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
    /// gives the rows), each with an on/off toggle, saved to the player's settings file and applied
    /// by the game's own restart (App.Restart, through the player's Restarter).
    ///
    /// Everything on it is the game's: the button is made as MainMenu makes its own, and the
    /// screen is an instance of the Mods screen's prefab (its window, list, rows, toggles and
    /// buttons) whose ModsScreen behaviour is swapped for BrutalTweaksScreen before it wakes.
    public static class BrutalTweaksMenu
    {
        internal static string[] Ids, Titles, Descriptions;
        internal static bool[] ChangesGameplay, Running, Saved, Locked;
        internal static string LockedNote, SettingsPath;
        internal static Func<string[], bool[], string> Save;

        /// `running`: each option as this run has it; `locked`: set by the command line (shown, not
        /// changeable; `lockedNote` names those arguments). `save(ids, values)` writes the
        /// changeable rows to the settings file, returning why it could not or null.
        public static void Install(Harmony harmony, string[] ids, string[] titles, string[] descriptions, bool[] changesGameplay,
            bool[] running, bool[] locked, string lockedNote, string settingsPath, Func<string[], bool[], string> save)
        {
            Ids = ids;
            Titles = titles;
            Descriptions = descriptions;
            ChangesGameplay = changesGameplay;
            Running = running;
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

    /// The Moono Tweaks screen, on the Mods screen's prefab (BrutalTweaksMenu.Open).
    public class BrutalTweaksScreen : KModalScreen
    {
        const string Title = "MOONO TWEAKS";
        /// The warning colour of the CHANGES GAMEPLAY label (amber).
        const string Warning = "FFB040";

        KButton closeTitle, close, apply, workshop;
        GameObject entryPrefab;
        Transform entryParent;
        LocText windowTitle;
        bool[] shown;

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
            workshop = parts.Field("workshopButton").GetValue<KButton>();
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
            workshop.gameObject.SetActive(false);
            apply.GetComponentInChildren<LocText>().text = "RESTART TO APPLY";
            apply.onClick += Apply;
            // Choices saved earlier in this run (closed with "Later") show as chosen.
            shown = (bool[])BrutalTweaksMenu.Saved.Clone();
            if (BrutalTweaksMenu.LockedNote.Length > 0)
                Note($"<b>Set by the launch options:</b> {BrutalTweaksMenu.LockedNote}\n<size=12>Those rows can't be changed here. Remove the options from the game's launch options to choose them here.</size>");
            Note("Changes apply when the game restarts.\n<size=12>Those marked <b>CHANGES GAMEPLAY</b> trade a little exactness for speed; the rest keep the game's own results.</size>");
            for (int i = 0; i < BrutalTweaksMenu.Ids.Length; i++) Row(i);
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

        void Row(int i)
        {
            var entry = Entry(BrutalTweaksMenu.Ids[i]);
            bool locked = BrutalTweaksMenu.Locked[i];
            var title = entry.GetReference<LocText>("Title");
            title.text = $"{BrutalTweaksMenu.Titles[i]}\n<size=12>{BrutalTweaksMenu.Descriptions[i]}</size>";
            string argument = BrutalTweaksMenu.Ids[i] switch
            {
                "splash" => "--splash moo",
                "frame-stats" => "--frame-stats",
                var name => "--game-tweak " + name,
            };
            entry.GetReference<ToolTip>("Description").toolTip = $"Launch option: {argument}";
            // The label beside the toggle: the warning for tweaks that change gameplay, and who
            // set a row the launch options set.
            var label = entry.GetReference<LocText>("Version");
            var labels = new List<string>();
            if (BrutalTweaksMenu.ChangesGameplay[i]) labels.Add($"<color=#{Warning}><b>CHANGES GAMEPLAY</b></color>");
            if (locked) labels.Add("LAUNCH OPTION");
            if (labels.Count > 0)
            {
                label.text = string.Join("\n", labels);
                label.fontSize = 12;
                var layout = label.GetComponent<LayoutElement>();
                if (layout != null) layout.flexibleWidth = 0f;
            }
            else label.gameObject.SetActive(false);
            var toggle = entry.GetReference<MultiToggle>("EnabledToggle");
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

        static bool Differ(bool[] a, bool[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return true;
            return false;
        }

        /// "Restart to apply" shows once the choices differ from what this run has.
        void Refresh() => apply.gameObject.SetActive(Differ(shown, BrutalTweaksMenu.Running));

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
