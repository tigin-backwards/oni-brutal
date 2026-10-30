using Newtonsoft.Json.Linq;
using UnityEngine;

namespace E2eDriver
{
    /// An IMGUI scroll view for specs (`imgui.scrollview`, README.md): 300 by 300 pixels centred on
    /// the screen over content 3000 high, scrolled by the wheel. A spec reads its scroll position
    /// as state (`type:E2eDriver.ImguiProbe::ScrollY`), so the wheel's IMGUI scrolling compares
    /// like any other value.
    public static class ImguiProbe
    {
        static GameObject view;

        public static Vector2 Scroll;
        public static float ScrollY => Scroll.y;

        public static JToken Start(JObject a)
        {
            bool on = (bool?)a["on"] ?? true;
            Scroll = Vector2.zero;
            if (on && view == null)
            {
                view = new GameObject("E2eDriverImguiProbe");
                Object.DontDestroyOnLoad(view);
                view.AddComponent<ImguiProbeGui>();
            }
            else if (!on && view != null)
            {
                Object.Destroy(view);
                view = null;
            }
            return new JObject { ["on"] = view != null };
        }

        internal static void Gui()
        {
            var position = new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 150f, 300f, 300f);
            Scroll = GUI.BeginScrollView(position, Scroll, new Rect(0f, 0f, 280f, 3000f));
            for (int i = 0; i < 100; i++) GUI.Label(new Rect(0f, i * 30f, 280f, 30f), "Row " + i);
            GUI.EndScrollView();
        }
    }

    sealed class ImguiProbeGui : MonoBehaviour
    {
        void OnGUI() => ImguiProbe.Gui();
    }
}
