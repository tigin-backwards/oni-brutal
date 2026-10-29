// Stack frames a mod's Harmony postfix sees (lines "SD ..."): No Minimum Temperature's postfix on
// the ElementConverter.OutputElement constructor reads new StackTrace(2).GetFrame(0) and expects
// the building config's ConfigureBuildingTemplate there. This probe has a postfix of its own on
// the same constructor and logs, for the first calls from a ConfigureBuildingTemplate, each frame
// of new StackTrace() (index, method name, declaring type), and how many calls in all have
// ConfigureBuildingTemplate at the index the mod reads.
//
// Run alone with PROBE_SCENARIO=stackdepth (patched at mod load; the configs run before the main
// menu). The summary line "SD summary" comes at the main menu.
using System.Diagnostics;
using HarmonyLib;

namespace SemanticsProbe
{
    public static class StackDepth
    {
        static int logged, calls, atTwo;

        public static void Run(Harmony harmony)
        {
            foreach (var ctor in typeof(ElementConverter.OutputElement).GetConstructors())
            {
                Rec.Log("SD patch " + ctor);
                harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(StackDepth), nameof(Postfix)));
            }
        }

        public static void Postfix()
        {
            calls++;
            var two = new StackTrace(2).GetFrame(0)?.GetMethod();
            if (two != null && two.Name == "ConfigureBuildingTemplate") atTwo++;
            var trace = new StackTrace();
            int config = -1;
            for (int i = 0; i < trace.FrameCount && i < 12; i++)
                if (trace.GetFrame(i).GetMethod()?.Name == "ConfigureBuildingTemplate") { config = i; break; }
            if (config < 0 || logged >= 3) return;
            logged++;
            Rec.Log("SD call " + calls + " config at " + config + " depth2 " + (two == null ? "null" : two.Name));
            for (int i = 0; i < trace.FrameCount && i <= config + 1; i++)
            {
                var m = trace.GetFrame(i).GetMethod();
                Rec.Log("SD frame " + i + " " + (m == null ? "null" : m.Name + " | " + (m.DeclaringType == null ? "null" : m.DeclaringType.FullName)));
            }
        }

        public static void Summary() => Rec.Log("SD summary calls " + calls + " depth2-config " + atTwo);
    }
}
