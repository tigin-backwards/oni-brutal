using System;
using System.Collections.Generic;
using OniBrutal.Engine.Rendering;
using UnityEngine;

namespace OniBrutal.Engine.IMGUI
{
    /// A behaviour that takes part in IMGUI (its script declares OnGUI), with the state kept for
    /// it across events: its control IDs, its GUILayout entries, its windows and its depth.
    internal sealed class GUIParticipant(BehaviourNative behaviour, long order)
    {
        public readonly BehaviourNative Behaviour = behaviour;
        /// When it was enabled: the tie-break between equal depths.
        public readonly long Order = order;
        /// GUI.depth as last set by its OnGUI: lower is in front.
        public int Depth;
        public readonly ControlScope Scope = new();
        public readonly LayoutPass Layout = new();
        /// Its windows, the front one first.
        public readonly List<WindowRecord> Windows = new();
        public WindowRecord FocusedWindow;

        public bool UsesLayout => Behaviour.UseGUILayout;

        public bool Alive => !Behaviour.Destroyed && Behaviour.IsActiveAndEnabledState && Behaviour.Managed is MonoBehaviour;

        public WindowRecord FindWindow(int id)
        {
            foreach (var w in Windows)
                if (w.Id == id) return w;
            return null;
        }

        public void BringToFront(WindowRecord w)
        {
            if (Windows.Remove(w)) Windows.Insert(0, w);
        }

        public void SendToBack(WindowRecord w)
        {
            if (Windows.Remove(w)) Windows.Add(w);
        }

        /// Windows its OnGUI did not call since the last prune are gone.
        public void PruneWindows()
        {
            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                var w = Windows[i];
                if (w.Touched) continue;
                Windows.RemoveAt(i);
                w.Scope.Release();
                if (IMGUI.Windows.ActiveModal == w) IMGUI.Windows.ActiveModal = null;
                if (FocusedWindow == w) FocusedWindow = null;
            }
        }

        public void UntouchWindows()
        {
            foreach (var w in Windows) w.Touched = false;
        }

        public void Release()
        {
            Scope.Release();
            foreach (var w in Windows)
            {
                w.Scope.Release();
                if (IMGUI.Windows.ActiveModal == w) IMGUI.Windows.ActiveModal = null;
            }
            Windows.Clear();
            FocusedWindow = null;
        }
    }

    /// Runs IMGUI each frame, after the cameras and canvases have rendered: every input event
    /// of the frame, in order, then a Repaint, each sent to the participating behaviours.
    ///
    /// Each behaviour gets a Layout event before every event (unless it turned useGUILayout
    /// off), and its windows run after its own OnGUI. Repaint and Layout go back to front;
    /// input goes front to back and stops at the first behaviour that uses it.
    internal static class GUIManager
    {
        static bool installed;
        static readonly List<GUIParticipant> participants = new();
        static readonly Dictionary<BehaviourNative, GUIParticipant> byBehaviour = new();
        static readonly List<GUIParticipant> ordered = new();
        static long nextOrder;
        /// The '\t' character event that follows a Tab press which moved focus is used too.
        static bool swallowTabCharacter;
        /// The last mouse position input reported, for the Layout and Repaint events.
        static Vector2 lastMouse;
        // Layout and Repaint events are never used, so one of each serves every pass.
        static readonly Event layoutPass = new(), repaintPass = new();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            PlayerLoop.GUI += RunFrame;
            FrameRenderer.RenderScreenLate += RepaintCapture;
        }

        public static void Register(BehaviourNative b)
        {
            if (b.Script?.OnGUI == null || byBehaviour.ContainsKey(b)) return;
            var p = new GUIParticipant(b, nextOrder++);
            participants.Add(p);
            byBehaviour[b] = p;
        }

        public static void Unregister(BehaviourNative b)
        {
            if (!byBehaviour.Remove(b, out var p)) return;
            participants.Remove(p);
            p.Release();
        }

        /// Forgets the participants, their windows and the queued events (tests).
        public static void Reset()
        {
            foreach (var p in participants) p.Release();
            participants.Clear();
            byBehaviour.Clear();
            InputEvents.Reset();
            Windows.ActiveModal = null;
            swallowTabCharacter = false;
            lastMouse = Vector2.zero;
        }

        static void RunFrame()
        {
            var events = InputEvents.TakeForGUI();
            foreach (var e in events) Dispatch(e);
            events.Clear();
            Repaint();
        }

        static void Repaint()
        {
            bool draws = GraphicsDevice.Current.RendersFrames && participants.Count > 0;
            if (draws) GUIRenderer.BeginFrame();
            try
            {
                Dispatch(Pass(EventType.Repaint, null));
            }
            finally
            {
                if (draws) GUIRenderer.EndFrame();
            }
        }

        /// A super-sampled capture renders the screen again, OnGUI included.
        static void RepaintCapture()
        {
            if (GraphicsDevice.Current.RendersFrames) Repaint();
        }

        /// The Layout or Repaint event, at the mouse position of `source` or the last one known.
        static Event Pass(EventType type, Event source)
        {
            var e = type == EventType.Layout ? layoutPass : repaintPass;
            e.Prepare(type, source != null ? source.ScreenPosition : lastMouse, source?.modifiers ?? EventModifiers.None);
            return e;
        }

        static void Dispatch(Event e)
        {
            var type = e.rawType;
            if (e.isMouse || type == EventType.ScrollWheel) lastMouse = e.ScreenPosition;
            if (swallowTabCharacter)
            {
                swallowTabCharacter = false;
                if (type == EventType.KeyDown && e.keyCode == KeyCode.None && e.character == '\t') return;
            }
            Order();
            if (type is EventType.Repaint or EventType.Layout)
            {
                foreach (var p in ordered)
                    if (p.Alive) Send(p, e);
                // The modal window is drawn last, in front of every other window.
                if (type == EventType.Repaint && Modal() is { } shown) Windows.Call(shown, e);
                return;
            }
            if (Modal() is { } modal)
            {
                // While a modal window is shown, input goes to it alone.
                SendModal(modal, e);
                if (type == EventType.MouseDown && e.rawType != EventType.Used) GUIStateData.KeyboardControl = 0;
                return;
            }
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                var p = ordered[i];
                if (!p.Alive) continue;
                Send(p, e);
                if (e.rawType == EventType.Used) return;
            }
            // A click no control took ends keyboard focus (a click outside a text field).
            if (type == EventType.MouseDown) GUIStateData.KeyboardControl = 0;
        }

        /// Back to front: higher depth first, equal depths in the order they were enabled.
        static void Order()
        {
            ordered.Clear();
            ordered.AddRange(participants);
            ordered.Sort(static (a, b) => a.Depth != b.Depth ? b.Depth.CompareTo(a.Depth) : a.Order.CompareTo(b.Order));
        }

        /// One event to one behaviour: its Layout pass first, then the event, its windows
        /// after its OnGUI for Layout and Repaint, before it for input.
        static void Send(GUIParticipant p, Event e)
        {
            if (p.UsesLayout)
            {
                var layout = Pass(EventType.Layout, e);
                p.UntouchWindows();
                Call(p, layout);
                p.PruneWindows();
                RunWindows(p, layout);
            }
            if (e.rawType == EventType.Repaint)
            {
                if (!p.UsesLayout) p.UntouchWindows();
                Call(p, e);
                if (!p.UsesLayout) p.PruneWindows();
                RunWindows(p, e);
                return;
            }
            var (target, underMouse) = Target(p, e);
            if (target != null)
            {
                bool down = e.rawType == EventType.MouseDown && underMouse;
                Windows.Call(target, e);
                TabFocus(target.Scope, e);
                if (down)
                {
                    // A press in a window focuses it and brings it to the front.
                    p.FocusedWindow = target;
                    p.BringToFront(target);
                    if (e.rawType != EventType.Used) e.Use();
                }
                if (e.rawType == EventType.Used) return;
            }
            else if (e.rawType == EventType.MouseDown)
            {
                p.FocusedWindow = null;
            }
            Call(p, e);
            TabFocus(p.Scope, e);
        }

        /// The modal window shown, if any: the last one declared, while its behaviour is alive
        /// and keeps declaring it.
        static WindowRecord Modal()
        {
            var w = Windows.ActiveModal;
            if (w == null) return null;
            if (w.Owner.Alive && byBehaviour.TryGetValue(w.Owner.Behaviour, out var p) && p == w.Owner && p.Windows.Contains(w)) return w;
            return null;
        }

        /// GUIUtility.hasModalWindow.
        public static bool HasModalWindow => Modal() != null;

        /// An input event while a modal window is shown: its behaviour's Layout pass, then the
        /// modal window alone gets the event.
        static void SendModal(WindowRecord modal, Event e)
        {
            var p = modal.Owner;
            if (p.UsesLayout)
            {
                var layout = Pass(EventType.Layout, e);
                p.UntouchWindows();
                Call(p, layout);
                p.PruneWindows();
                RunWindows(p, layout);
                // Its behaviour stopped showing it.
                if (Modal() != modal) return;
            }
            bool down = e.rawType == EventType.MouseDown && modal.Contains(e.ScreenPosition);
            Windows.Call(modal, e);
            TabFocus(modal.Scope, e);
            if (down)
            {
                p.FocusedWindow = modal;
                p.BringToFront(modal);
                if (e.rawType != EventType.Used) e.Use();
            }
        }

        /// A behaviour's windows for one event, back to front. A modal window that is not the
        /// one shown is skipped; the shown one is drawn after every behaviour (Dispatch).
        static void RunWindows(GUIParticipant p, Event e)
        {
            bool repaint = e.rawType == EventType.Repaint;
            for (int i = p.Windows.Count - 1; i >= 0; i--)
            {
                if (i >= p.Windows.Count) continue;
                var w = p.Windows[i];
                if (w.Modal && (repaint || w != Windows.ActiveModal)) continue;
                Windows.Call(w, e);
            }
        }

        /// The window an input event goes to: for mouse events the window whose control holds
        /// the mouse, else the front window under it; for key events the window with the
        /// keyboard control, else (when no control has it) the focused window.
        static (WindowRecord Window, bool UnderMouse) Target(GUIParticipant p, Event e)
        {
            if (p.Windows.Count == 0) return (null, false);
            var type = e.rawType;
            if (Event.IsPointer(type))
            {
                int hot = GUIStateData.HotControl;
                if (hot != 0)
                {
                    var holder = ControlScope.OwnerOf(hot)?.Window;
                    return holder != null && holder.Owner == p ? (holder, false) : (null, false);
                }
                foreach (var w in p.Windows)
                    if (!w.Modal && w.Contains(e.ScreenPosition)) return (w, true);
                return (null, false);
            }
            if (type is EventType.KeyDown or EventType.KeyUp)
            {
                int keyboard = GUIStateData.KeyboardControl;
                if (keyboard != 0)
                {
                    var holder = ControlScope.OwnerOf(keyboard)?.Window;
                    return holder != null && holder.Owner == p ? (holder, false) : (null, false);
                }
                return (p.FocusedWindow, false);
            }
            return (null, false);
        }

        /// A Tab press nothing used moves keyboard focus to the container's next focusable
        /// control (Shift+Tab: the previous one), when the focus is in that container or
        /// nowhere.
        static void TabFocus(ControlScope scope, Event e)
        {
            if (e.rawType != EventType.KeyDown || e.keyCode != KeyCode.Tab) return;
            int current = GUIStateData.KeyboardControl;
            if (current != 0 && ControlScope.OwnerOf(current) != scope) return;
            int next = scope.NextFocus(current, e.shift);
            if (next == 0) return;
            GUIStateData.KeyboardControl = next;
            e.Use();
            swallowTabCharacter = true;
        }

        /// Runs a behaviour's OnGUI for one event with the state a call starts with.
        static void Call(GUIParticipant p, Event e)
        {
            var previous = Event.current;
            Event.current = e;
            GUIStateData.InGUI = true;
            GUIStateData.InWindow = false;
            GUIStateData.Participant = p;
            GUIStateData.Window = null;
            GUIStateData.Scope = p.Scope;
            GUIStateData.BeginCall();
            p.Scope.BeginPass();
            ClipStack.Clear();
            ScrollViews.Reset();
            bool layoutEvent = e.rawType == EventType.Layout;
            if (p.UsesLayout)
            {
                if (layoutEvent) p.Layout.BeginRecord(new Rect(0f, 0f, Screen.width, Screen.height), GUIStyle.none);
                else p.Layout.BeginReplay();
                GUIStateData.Layout = p.Layout;
            }
            else
            {
                GUIStateData.Layout = null;
            }
            bool completed = Invoke(p, null);
            try
            {
                if (completed && ClipStack.Count > 0)
                    Debug.LogError("GUI Error: You are pushing more GUIClips than you are popping. Make sure they are balanced.");
                if (p.UsesLayout && layoutEvent) p.Layout.Compute();
            }
            finally
            {
                EndCall(previous);
            }
        }

        /// Runs a behaviour's OnGUI, or one of its window functions; an exception is logged with
        /// the behaviour as context, ExitGUI ends the call silently. Returns whether the code
        /// ran to its end.
        public static bool Invoke(GUIParticipant p, WindowRecord window)
        {
            var context = (MonoBehaviour)p.Behaviour.Managed;
            try
            {
                if (window != null) window.Function(window.Id);
                else p.Behaviour.Script.OnGUI(context);
                return true;
            }
            catch (ExitGUIException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, context);
                return false;
            }
            finally
            {
                GUIStateData.Exiting = false;
            }
        }

        public static void EndCall(Event previous)
        {
            ClipStack.Clear();
            ScrollViews.Reset();
            GUIStateData.InGUI = false;
            GUIStateData.InWindow = false;
            GUIStateData.Participant = null;
            GUIStateData.Window = null;
            GUIStateData.Scope = null;
            GUIStateData.Layout = null;
            Event.current = previous;
        }
    }
}
