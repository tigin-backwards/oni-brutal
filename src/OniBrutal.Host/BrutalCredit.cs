using System;
using System.Reflection;
using UnityEngine;

namespace OniBrutal.Host
{
    /// The BRUTAL License (§2.2) asks for BRUTAL's logo, unaltered except for scaling, to be shown
    /// before gameplay and no less prominently than other parties' logos; its maintainers note a
    /// main menu placement meets that. The logo (ahwootech/brutal-branding) is drawn on the game's
    /// main menu, about the size of the menu's Klei banner, with a caption. Only while the main
    /// menu is up; nothing in the game is changed.
    internal sealed class BrutalCredit : MonoBehaviour
    {
        const string Caption = "This logo is only here because the people who made BRUTAL require it to be here. "
                               + "(BRUTAL License, section 2.2. Hi, Ahwoo.)";

        Texture2D logo = null!;
        GUIStyle? headerStyle, captionStyle;
        Func<object?> mainMenu = () => null;

        public static void Install()
        {
            var go = new GameObject("BrutalCredit");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<BrutalCredit>();
        }

        void Awake()
        {
            using var stream = typeof(BrutalCredit).Assembly.GetManifestResourceStream("LOGO_BRUTAL_WHT.png")!;
            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            logo = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            logo.LoadImage(data);
            // The game's MainMenu.Instance, read without referencing the game's assembly.
            var instance = Type.GetType("MainMenu, Assembly-CSharp")?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            mainMenu = instance == null ? () => null : () => instance.GetValue(null);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || mainMenu() is not MonoBehaviour menu || menu == null || !menu.isActiveAndEnabled) return;
            // A tile like the menu's news tiles ("It's Klei Fest!", "Out Now!"), as wide as they are,
            // in the free strip of cave art between them and the game's title.
            float unit = Screen.height / 768f;
            float width = 250f * unit, pad = 8f * unit;
            headerStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            captionStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperCenter };
            headerStyle.fontSize = Mathf.RoundToInt(15f * unit);
            headerStyle.normal.textColor = new Color(0.94f, 0.47f, 0.47f);
            captionStyle.fontSize = Mathf.RoundToInt(11f * unit);
            captionStyle.normal.textColor = new Color(0.9f, 0.9f, 0.92f);
            float inner = width - 2f * pad;
            float headerHeight = 24f * unit;
            float logoHeight = inner * logo.height / logo.width;
            float captionHeight = captionStyle.CalcHeight(new GUIContent(Caption), inner);
            var tile = new Rect(Screen.width * 0.5f - width * 0.5f - 60f * unit, 20f * unit, width, headerHeight + logoHeight + captionHeight + 4f * pad);
            var tint = GUI.color;
            GUI.color = new Color(0.17f, 0.19f, 0.26f, 0.92f);
            GUI.DrawTexture(tile, Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.Label(new Rect(tile.x, tile.y + pad * 0.5f, width, headerHeight), "Legally Required!", headerStyle);
            float y = tile.y + headerHeight + pad;
            GUI.DrawTexture(new Rect(tile.x + pad, y, inner, logoHeight), logo, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(tile.x + pad, y + logoHeight + pad, inner, captionHeight), Caption, captionStyle);
        }
    }
}
