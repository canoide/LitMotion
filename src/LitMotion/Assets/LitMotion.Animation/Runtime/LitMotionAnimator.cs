using LitMotion;
using System;
using System.Collections.Generic;
using LitMotion.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LitMotion.Animation
{
    public enum AnimationFinishMode
    {
        Keep,
        Reset
    }

    [Serializable]
    public class LitMotionAnimationEntry
    {
        public string id;
        public bool autoPlay;
        public AnimationFinishMode finishMode;
        public AnimationMode mode;
        [SerializeReference] public LitMotionAnimationComponent[] components;
        public UnityEvent onComplete;

        internal Queue<LitMotionAnimationComponent> queue;
        internal FastListCore<LitMotionAnimationComponent> playingComponents = new();
        internal int activeParallelCount;
        internal Action runtimeOnComplete; // For runtime callbacks

        [NonSerialized] public float currentTime;
        [NonSerialized] public float totalDuration;
        [NonSerialized] internal float completedDuration;
    }

    [AddComponentMenu("LitMotion Animator")]
    public sealed class LitMotionAnimator : MonoBehaviour
    {
        [SerializeField] List<LitMotionAnimationEntry> animations = new();

        void Start()
        {
            foreach (var anim in animations)
            {
                if (anim.autoPlay) PlayAnimation(anim, null);
            }
        }

        public void Play()
        {
            if (animations.Count > 0)
            {
                PlayAnimation(animations[0], null);
            }
        }

        public void Play(string id)
        {
            Play(id, null);
        }

        public void Play(string id, Action onComplete)
        {
            var anim = animations.Find(x => x.id == id);
            if (anim != null)
            {
                PlayAnimation(anim, onComplete);
            }
            else
            {
                Debug.LogWarning($"[LitMotionAnimator] Animation '{id}' not found on {gameObject.name}");
            }
        }

        public bool IsPlaying(string id)
        {
            var anim = animations.Find(x => x.id == id);
            if (anim != null)
            {
                if (anim.playingComponents.AsArray() == null) return false;
                foreach (var component in anim.playingComponents.AsSpan())
                {
                    if (component.TrackedHandle.IsActive()) return true;
                }
            }
            return false;
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

        void Update()
        {
            foreach (var anim in animations)
            {
                bool active = (anim.queue != null && anim.queue.Count > 0);
                float currentOffset = 0f;

                if (anim.playingComponents.AsArray() != null)
                {
                    var span = anim.playingComponents.AsSpan();
                    if (anim.mode == AnimationMode.Sequential)
                    {
                        if (span.Length > 0)
                        {
                            var handle = span[span.Length - 1].TrackedHandle;
                            if (handle.IsActive())
                            {
                                active = true;
                                currentOffset = (float)handle.Time;
                            }
                        }
                    }
                    else // Parallel
                    {
                        foreach (var component in span)
                        {
                            var handle = component.TrackedHandle;
                            if (handle.IsActive())
                            {
                                active = true;
                                float time = (float)handle.Time;
                                if (time > currentOffset) currentOffset = time;
                            }
                        }
                    }
                }

                if (active)
                {
                    float targetTime = anim.completedDuration + currentOffset;
                    if (targetTime > anim.currentTime) anim.currentTime = targetTime;
                }

                if (anim.totalDuration > 0 && anim.currentTime > anim.totalDuration)
                {
                    anim.currentTime = anim.totalDuration;
                }
            }
        }

        void CalculateDuration(LitMotionAnimationEntry entry)
        {
            entry.totalDuration = 0f;
            if (entry.components == null) return;

            if (entry.mode == AnimationMode.Sequential)
            {
                foreach (var c in entry.components)
                {
                    if (c != null && c.Enabled) entry.totalDuration += (c.Duration + c.Delay);
                }
            }
            else // Parallel
            {
                foreach (var c in entry.components)
                {
                    if (c != null && c.Enabled)
                    {
                        var d = c.Duration + c.Delay;
                        if (d > entry.totalDuration) entry.totalDuration = d;
                    }
                }
            }
        }

        void PlayAnimation(LitMotionAnimationEntry entry, Action onComplete = null)
        {
            if (entry.queue == null) entry.queue = new();
            if (entry.playingComponents.AsArray() == null) entry.playingComponents = new();

            var isPlaying = false;
            if (entry.playingComponents.AsArray() != null)
            {
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
            }

            entry.runtimeOnComplete = onComplete;

            if (isPlaying) return;

            CalculateDuration(entry);
            entry.currentTime = 0f;
            entry.completedDuration = 0f;

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
                        OnAnimationComplete(entry);
                    }
                    break;
            }
        }

        void OnAnimationComplete(LitMotionAnimationEntry entry)
        {
            CalculateDuration(entry);
            entry.onComplete?.Invoke();
            entry.runtimeOnComplete?.Invoke();
            entry.runtimeOnComplete = null;
            entry.currentTime = entry.totalDuration;

            if (entry.playingComponents.AsArray() != null)
            {
                var span = entry.playingComponents.AsSpan();
                span.Reverse();
                foreach (var component in span)
                {
                    var handle = component.TrackedHandle;
                    if (handle.IsActive()) handle.TryCancel();

                    if (entry.finishMode == AnimationFinishMode.Reset)
                    {
                        component.ResetValue();
                    }

                    component.TrackedHandle = default;
                }
                entry.playingComponents.Clear();
            }
            entry.queue?.Clear();
        }

        void MoveNextMotion(LitMotionAnimationEntry entry)
        {
            if (entry.queue != null && entry.queue.TryDequeue(out var queuedComponent))
            {
                try
                {
                    var handle = queuedComponent.Play();
                    var isActive = handle.IsActive();

                    if (isActive)
                    {
                        handle.Preserve();
                        MotionManager.GetManagedDataRef(handle, false).OnCompleteAction += () =>
                        {
                            entry.completedDuration += (queuedComponent.Duration + queuedComponent.Delay);
                            MoveNextMotion(entry);
                        };
                    }

                    queuedComponent.TrackedHandle = handle;
                    entry.playingComponents.Add(queuedComponent);

                    if (!isActive)
                    {
                        entry.completedDuration += (queuedComponent.Duration + queuedComponent.Delay);
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
                OnAnimationComplete(entry);
            }
        }

        void OnParallelComponentComplete(LitMotionAnimationEntry entry)
        {
            entry.activeParallelCount--;
            if (entry.activeParallelCount <= 0)
            {
                OnAnimationComplete(entry);
            }
        }

        void PauseAnimation(LitMotionAnimationEntry entry)
        {
            if (entry.playingComponents.AsArray() == null) return;
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
            if (entry.playingComponents.AsArray() != null)
            {
                var span = entry.playingComponents.AsSpan();
                span.Reverse();
                foreach (var component in span)
                {
                    var handle = component.TrackedHandle;
                    handle.TryCancel();
                    component.OnStop();
                    component.TrackedHandle = default;
                }
                entry.playingComponents.Clear();
            }
            entry.queue?.Clear();
            entry.currentTime = 0f;
            entry.completedDuration = 0f;
        }

        void OnDestroy()
        {
            StopAll();
        }
    }
}
