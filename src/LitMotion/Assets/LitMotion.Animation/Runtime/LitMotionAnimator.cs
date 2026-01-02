using System;
using System.Collections.Generic;
using LitMotion.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LitMotion.Animation
{
    [Serializable]
    public class LitMotionAnimationEntry
    {
        public string id;
        public AnimationMode mode;
        [SerializeReference] public LitMotionAnimationComponent[] components;
        public UnityEvent onComplete;

        internal Queue<LitMotionAnimationComponent> queue = new();
        internal FastListCore<LitMotionAnimationComponent> playingComponents;
        internal int activeParallelCount;
    }

    [AddComponentMenu("LitMotion Animator")]
    public sealed class LitMotionAnimator : MonoBehaviour
    {
        [SerializeField] List<LitMotionAnimationEntry> animations = new();

        public void Play()
        {
            if (animations.Count > 0)
            {
                PlayAnimation(animations[0]);
            }
        }

        public void Play(string id)
        {
            var anim = animations.Find(x => x.id == id);
            if (anim != null)
            {
                PlayAnimation(anim);
            }
            else
            {
                Debug.LogWarning($"[LitMotionAnimator] Animation '{id}' not found on {gameObject.name}");
            }
        }

        public void Pause(string id)
        {
            var anim = animations.Find(x => x.id == id);
            if (anim != null)
            {
                PauseAnimation(anim);
            }
        }

        public void Stop(string id)
        {
            var anim = animations.Find(x => x.id == id);
            if (anim != null)
            {
                StopAnimation(anim);
            }
        }

        public void StopAll()
        {
            foreach (var anim in animations)
            {
                StopAnimation(anim);
            }
        }

        void PlayAnimation(LitMotionAnimationEntry entry)
        {
            // Resume if active? Or Restart?
            // "Play" usually implies restart if finished, or resume if paused?
            // For simplicity, let's assume Restart if it was stopped/finished, or just ensure it runs.
            // But if it is running, do we restart it?
            // If we follow LitMotionAnimation logic: Play() checks handles. If active, resume. If not, restart.

            var isPlaying = false;
            foreach (var component in entry.playingComponents.AsSpan())
            {
                var handle = component.TrackedHandle;
                if (handle.IsActive())
                {
                    handle.PlaybackSpeed = 1f;
                    isPlaying = true;
                    component.OnResume();
                }
            }

            if (isPlaying) return;

            // Clear previous state
            entry.playingComponents.Clear();
            entry.queue.Clear();

            switch (entry.mode)
            {
                case AnimationMode.Sequential:
                    if (entry.components != null)
                    {
                        foreach (var component in entry.components)
                        {
                            if (component == null || !component.Enabled) continue;
                            entry.queue.Enqueue(component);
                        }
                    }
                    MoveNextMotion(entry);
                    break;

                case AnimationMode.Parallel:
                    entry.activeParallelCount = 0;
                    if (entry.components != null)
                    {
                        foreach (var component in entry.components)
                        {
                            if (component == null || !component.Enabled) continue;

                            try
                            {
                                var handle = component.Play();
                                component.TrackedHandle = handle;

                                if (handle.IsActive())
                                {
                                    handle.Preserve();
                                    entry.activeParallelCount++;
                                    // We need to capture 'entry' for the callback
                                    MotionManager.GetManagedDataRef(handle, false).OnCompleteAction += () => OnParallelComponentComplete(entry);
                                }

                                entry.playingComponents.Add(component);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);
                            }
                        }
                    }

                    if (entry.activeParallelCount == 0)
                    {
                        entry.onComplete?.Invoke();
                        entry.playingComponents.Clear();
                    }
                    break;
            }
        }

        void MoveNextMotion(LitMotionAnimationEntry entry)
        {
            if (entry.queue.TryDequeue(out var queuedComponent))
            {
                try
                {
                    var handle = queuedComponent.Play();
                    var isActive = handle.IsActive();

                    if (isActive)
                    {
                        handle.Preserve();
                        MotionManager.GetManagedDataRef(handle, false).OnCompleteAction += () => MoveNextMotion(entry);
                    }

                    queuedComponent.TrackedHandle = handle;
                    entry.playingComponents.Add(queuedComponent);

                    if (!isActive)
                    {
                        MoveNextMotion(entry);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            else
            {
                // Sequence complete
                entry.onComplete?.Invoke();
                entry.playingComponents.Clear();
            }
        }

        void OnParallelComponentComplete(LitMotionAnimationEntry entry)
        {
            entry.activeParallelCount--;
            if (entry.activeParallelCount <= 0)
            {
                entry.onComplete?.Invoke();
                entry.playingComponents.Clear();
            }
        }

        void PauseAnimation(LitMotionAnimationEntry entry)
        {
            foreach (var component in entry.playingComponents.AsSpan())
            {
                var handle = component.TrackedHandle;
                if (handle.IsActive())
                {
                    handle.PlaybackSpeed = 0f;
                    component.OnPause();
                }
            }
        }

        void StopAnimation(LitMotionAnimationEntry entry)
        {
            var span = entry.playingComponents.AsSpan();
            span.Reverse();
            foreach (var component in span)
            {
                var handle = component.TrackedHandle;
                handle.TryCancel();
                component.OnStop();
                component.TrackedHandle = handle;
            }
            entry.playingComponents.Clear();
            entry.queue.Clear();
        }

        void OnDestroy()
        {
            StopAll();
        }
    }
}
