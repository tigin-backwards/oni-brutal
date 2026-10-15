using System;
using System.Collections.Generic;
using OniBrutal.Desktop;
using UnityEngine;

namespace OniBrutal.Host
{
    /// `--input FRAME:ACTION[:ARGS]` (development and test runs): input delivered through the
    /// same feed as the window's, before the given frame. Positions are screen pixels with the
    /// origin at the top left (as in captures). Actions: `move:X,Y`, `click:X,Y` (left button
    /// down, up the next frame), `key:KEYCODE` (down, up the next frame), `text:STRING`.
    sealed class ScriptedInput
    {
        readonly SortedDictionary<int, List<Action>> byFrame = new();
        readonly InputStateFeed feed = new();

        public void Add(string spec)
        {
            var parts = spec.Split(':', 3);
            if (parts.Length < 2) throw new ArgumentException($"--input expects FRAME:ACTION[:ARGS], got '{spec}'");
            int frame = int.Parse(parts[0]);
            string arg = parts.Length > 2 ? parts[2] : "";
            switch (parts[1])
            {
                case "move": At(frame, () => Move(Point(arg))); break;
                case "click":
                    At(frame, () => { Move(Point(arg)); feed.MouseDown(0, 0, Gui(Point(arg))); });
                    At(frame + 1, () => feed.MouseUp(0, 0, Gui(Point(arg))));
                    break;
                case "key":
                    var key = Enum.Parse<KeyCode>(arg);
                    At(frame, () => feed.KeyDown(key, 0, repeat: false, Vector2.zero));
                    At(frame + 1, () => feed.KeyUp(key, 0, Vector2.zero));
                    break;
                case "text":
                    At(frame, () => { foreach (var r in arg.EnumerateRunes()) feed.Text((uint)r.Value, 0); });
                    break;
                default: throw new ArgumentException($"Unknown --input action '{parts[1]}'");
            }
        }

        void At(int frame, Action action)
        {
            if (!byFrame.TryGetValue(frame, out var list)) byFrame[frame] = list = new List<Action>();
            list.Add(action);
        }

        public void Apply(int frame)
        {
            if (byFrame.TryGetValue(frame, out var list))
                foreach (var a in list) a();
        }

        static Vector2 Point(string arg)
        {
            var xy = arg.Split(',');
            return new Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
        }

        static float Height => OniBrutal.Engine.Platform.PlatformServices.Current.FramebufferHeight;

        /// Top-left pixel position → the feed's screen position (origin bottom left).
        void Move(Vector2 topLeft) => feed.MouseMoved(new Vector2(topLeft.x, Height - topLeft.y));

        /// GUI positions have the origin at the top left, like the script's.
        static Vector2 Gui(Vector2 topLeft) => topLeft;
    }
}
