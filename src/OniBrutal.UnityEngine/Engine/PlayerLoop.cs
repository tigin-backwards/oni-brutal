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
    ///   PostLateUpdate  destroy queue; canvases and cameras render; WaitForEndOfFrame; destroy
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
        public static event Action Render;              // cameras, overlay canvases
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

        /// Runs one frame. realDelta is the wall time since the previous frame.
        public static void RunFrame(float realDelta)
        {
            if (!started) throw new InvalidOperationException("PlayerLoop.Initialize must run first");
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

            BehaviourManager.RunStarts();
            BehaviourManager.RunUpdate();
            Coroutines.RunUpdatePhase();
            DirectorUpdate?.Invoke();

            AnimationUpdate?.Invoke();
            BehaviourManager.RunLateUpdate();

            Lifecycle.ProcessDestroyQueue();
            PostLateUpdate?.Invoke();
            Render?.Invoke();
            Time.FrameRendered();
            Coroutines.RunEndOfFramePhase();
            Lifecycle.ProcessDestroyQueue();
            EngineLog.FlushToMainThread();
            Present?.Invoke();
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
