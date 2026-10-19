using UnityEngine;

namespace TMPro
{
    // Events raised by text objects. The game listens to TEXT_CHANGED_EVENT, called with the text
    // object after each layout.
    public static partial class TMPro_EventManager
    {
        public static readonly FastAction<Object> TEXT_CHANGED_EVENT = new();

        public static void ON_TEXT_CHANGED(Object obj) => TEXT_CHANGED_EVENT.Call(obj);
    }
}
