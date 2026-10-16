using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// One frame in Unity's order (ARCHITECTURE §3.3):
    ///
    ///   Initialization  time, pending scene load completion
    ///   EarlyUpdate     platform events, input, main-thread log callbacks
    ///   FixedUpdate     Start; 0..n fixed steps (FixedUpdate, WaitForFixedUpdate)
    ///   Update          Start; Update; yield null / WaitForSeconds / AsyncOperation
    ///   PreLateUpdate   Animators; LateUpdate
    ///   PostLateUpdate  destroy queue; canvases; particle systems; cameras render; OnGUI; WaitForEndOfFrame; destroy
    ///                   queue; present
    ///
    /// Subsystems attach to the phases through the hooks below, so this class stays the single
    /// definition of ordering.
    internal static class PlayerLoop
    {
        public static event Action FrameStart;          // pending scene loads complete here
        public static event Action EarlyUpdate;         // platform events, input state
        public static event Action DirectorUpdate;      // PlayableDirectors (Update.DirectorUpdate, after scripts and coroutines)
        public static event Action AnimationUpdate;     // Animators (PreLateUpdate.DirectorUpdateAnimationBegin/End, before LateUpdate)
        public static event Action FixedAnimationUpdate; // Fixed-mode Animators (FixedUpdate.DirectorFixedUpdate, after script FixedUpdate)
        public static event Action PostLateUpdate;      // e.g. canvas update (willRenderCanvases)
        public static event Action ParticleUpdate;      // ParticleSystems (PostLateUpdate.ParticleSystemEndUpdateAll, after LateUpdate, before rendering)
        public static event Action Render;              // cameras, overlay canvases
        public static event Action GUI;                 // OnGUI: the frame's input events, then Layout and Repaint (drawn on top)
        public static event Action Present;             // swap buffers

        public static bool HasFocus { get; set; } = true;
        public static int TargetFrameRate { get; set; } = -1;
        public static bool QuitRequested { get; private set; }
        public static int ExitCode { get; private set; }

        static bool started;

        public static void RequestQuit(int exitCode)
        {
            if (QuitRequested) return;
            QuitRequested = true;
            ExitCode = exitCode;
        }

        public static void Initialize()
        {
            Threads.SetMainThread();
            started = true;
        }

        /// Diagnostics (`--frame-stats`): average phase times logged every 60 frames.
        public static bool FrameStats;
        static readonly System.Diagnostics.Stopwatch statClock = new();
        static readonly double[] statTotals = new double[9]; // render, gui, end of frame, present, early+fixed, scripts' Update (+starts, coroutines, director), animation+LateUpdate, post-late update (canvases…), particles
        static int statFrames;

        static readonly double[] framePhases = new double[9];

        static void Mark(int phase)
        {
            if (!FrameStats) return;
            double ms = statClock.Elapsed.TotalMilliseconds;
            statTotals[phase] += ms;
            framePhases[phase] += ms;
            statClock.Restart();
        }

        static void EndStats()
        {
            if (!FrameStats) return;
            // Single slow frames (hitches) are logged with their own breakdown.
            double total = 0;
            foreach (var v in framePhases) total += v;
            if (total > 250)
                EngineLog.Engine($"slow frame {Time.frameCount} ({total:F0} ms): early+fixed {framePhases[4]:F0}, update {framePhases[5]:F0}, late {framePhases[6]:F0}, post-late {framePhases[7]:F0}, render {framePhases[0]:F0}, gui {framePhases[1]:F0}, end of frame {framePhases[2]:F0}, present {framePhases[3]:F0}");
            Array.Clear(framePhases);
            if (++statFrames < 60) return;
            double T(int i) => statTotals[i] / 60;
            EngineLog.Engine($"frame stats (ms/frame over 60): early+fixed {T(4):F2}, update {T(5):F2}, late {T(6):F2}, post-late {T(7):F2}, particles {T(8):F2}, render {T(0):F2}, gui {T(1):F2}, end of frame {T(2):F2}, present {T(3):F2}, gpu {OniBrutal.Engine.Rendering.GraphicsDevice.Current.TakeGpuFrameMilliseconds():F2}");
            Array.Clear(statTotals);
            statFrames = 0;
        }

        /// Runs one frame. realDelta is the wall time since the previous frame.
        public static void RunFrame(float realDelta)
        {
            if (!started) throw new InvalidOperationException("PlayerLoop.Initialize must run first");
            if (FrameStats) statClock.Restart();
            Time.BeginFrame(realDelta);
            FrameStart?.Invoke();

            EngineLog.FlushToMainThread();
            EarlyUpdate?.Invoke();

            BehaviourManager.RunStarts();
            while (Time.BeginFixedStep())
            {
                try
                {
                    BehaviourManager.RunFixedUpdate();
                    FixedAnimationUpdate?.Invoke();
                    Coroutines.RunFixedUpdatePhase();
                }
                finally
                {
                    Time.EndFixedStep();
                }
            }

            Mark(4);
            BehaviourManager.RunStarts();
            BehaviourManager.RunUpdate();
            // Update.ScriptRunDelayedDynamicFrameRate: Start for behaviours enabled during
            // Update (so they start before LateUpdate), then coroutines resumed after Update.
            BehaviourManager.RunStarts();
            Coroutines.RunUpdatePhase();
            DirectorUpdate?.Invoke();

            Mark(5);
            AnimationUpdate?.Invoke();
            // Behaviours enabled after the Update pass (by coroutines, animation events) start
            // before LateUpdate and get it this frame (observed in Unity's player).
            BehaviourManager.RunStarts();
            BehaviourManager.RunLateUpdate();

            Lifecycle.ProcessDestroyQueue();
            Mark(6);
            PostLateUpdate?.Invoke();
            Mark(7);
            ParticleUpdate?.Invoke();
            Mark(8);
            Render?.Invoke();
            Mark(0);
            GUI?.Invoke();
            Mark(1);
            Coroutines.RunEndOfFramePhase();
            Lifecycle.ProcessDestroyQueue();
            EngineLog.FlushToMainThread();
            Mark(2);
            Present?.Invoke();
            Event.DiscardUnconsumed();
            Mark(3);
            EndStats();
        }

        /// Unity sends OnApplicationQuit to every active MonoBehaviour before shutting down.
        public static void SendApplicationQuit()
        {
            foreach (var go in Finder.AllSceneGameObjects())
                Messages.SendToComponents(go, s => s.OnApplicationQuit);
        }

        public static void SendApplicationFocus(bool focus)
        {
            HasFocus = focus;
            foreach (var go in Finder.AllSceneGameObjects())
            {
                if (!go.ActiveInHierarchy) continue;
                foreach (var c in go.Components)
                {
                    if (c is not MonoBehaviour mb || c.Native is not BehaviourNative bn || bn.Script?.OnApplicationFocus == null) continue;
                    try
                    {
                        bn.Script.OnApplicationFocus(mb, focus);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, mb);
                    }
                }
            }
        }
    }
}
