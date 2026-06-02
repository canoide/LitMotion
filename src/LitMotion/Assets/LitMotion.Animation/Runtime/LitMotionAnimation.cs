using LitMotion;
using System;
using System.Collections.Generic;
using LitMotion.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LitMotion.Animation
{
    [AddComponentMenu("LitMotion Animation")]
    public sealed class LitMotionAnimation : MonoBehaviour, ISerializationCallbackReceiver
    {
        public string id;

        [SerializeField] AutoPlayMode autoPlayMode = AutoPlayMode.OnStart;
        [SerializeField] AnimationMode animationMode;

        [SerializeReference]
        LitMotionAnimationComponent[] components;

        [Space]
        public UnityEvent onComplete;

        readonly Queue<LitMotionAnimationComponent> queue = new();
        FastListCore<LitMotionAnimationComponent> playingComponents = new();
        int activeParallelCount;

        [HideInInspector, SerializeField] bool playOnAwake = true;
        [HideInInspector, SerializeField] int version;

        public IReadOnlyList<LitMotionAnimationComponent> Components => components;

        void OnEnable()
        {
            if (autoPlayMode == AutoPlayMode.OnEnable)
                Play();
        }

        void Start()
        {
            if (autoPlayMode == AutoPlayMode.OnStart)
                Play();
        }

        void MoveNextMotion()
        {
            if (queue.TryDequeue(out var queuedComponent))
            {
                try
                {
                    var handle = queuedComponent.Play();
                    var isActive = handle.IsActive();

                    if (isActive)
                    {
                        handle.Preserve();
                        MotionManager.GetManagedDataRef(handle, false).OnCompleteAction += MoveNextMotion;
                    }

                    queuedComponent.TrackedHandle = handle;
                    playingComponents.Add(queuedComponent);

                    if (!isActive)
                    {
                        MoveNextMotion();
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
                onComplete?.Invoke();
                playingComponents.Clear();
            }
        }

        public void Play()
        {
            if (playingComponents.AsArray() == null) playingComponents = new();
            var isPlaying = false;

            foreach (var component in playingComponents.AsSpan())
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

            playingComponents.Clear();

            switch (animationMode)
            {
                case AnimationMode.Sequential:
                    foreach (var component in components)
                    {
                        if (component == null) continue;
                        if (!component.Enabled) continue;
                        queue.Enqueue(component);
                    }

                    MoveNextMotion();
                    break;
                case AnimationMode.Parallel:
                    activeParallelCount = 0;
                    foreach (var component in components)
                    {
                        if (component == null) continue;
                        if (!component.Enabled) continue;

                        try
                        {
                            var handle = component.Play();
                            component.TrackedHandle = handle;

                            if (handle.IsActive())
                            {
                                handle.Preserve();
                                activeParallelCount++;
                                MotionManager.GetManagedDataRef(handle, false).OnCompleteAction += OnParallelComponentComplete;
                            }

                            playingComponents.Add(component);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }

                    if (activeParallelCount == 0)
                    {
                        onComplete?.Invoke();
                    }
                    break;
            }
        }

        void OnParallelComponentComplete()
        {
            activeParallelCount--;
            if (activeParallelCount <= 0)
            {
                onComplete?.Invoke();
                playingComponents.Clear();
            }
        }

        public void Pause()
        {
            if (playingComponents.AsArray() == null) return;
            foreach (var component in playingComponents.AsSpan())
            {
                var handle = component.TrackedHandle;
                if (handle.IsActive())
                {
                    handle.PlaybackSpeed = 0f;
                    component.OnPause();
                }
            }
        }

        public void Stop()
        {
            if (playingComponents.AsArray() != null)
            {
                var span = playingComponents.AsSpan();
                span.Reverse();
                foreach (var component in span)
                {
                    var handle = component.TrackedHandle;
                    handle.TryCancel();
                    component.OnStop();
                    component.TrackedHandle = handle;
                }

                playingComponents.Clear();
            }
            queue.Clear();
        }

        public void Restart()
        {
            Stop();
            Play();
        }

        public bool IsActive
        {
            get
            {
                if (queue.Count > 0) return true;
                if (playingComponents.AsArray() == null) return false;

                foreach (var component in playingComponents.AsSpan())
                {
                    var handle = component.TrackedHandle;
                    if (handle.IsActive()) return true;
                }

                return false;
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (queue.Count > 0) return true;
                if (playingComponents.AsArray() == null) return false;

                foreach (var component in playingComponents.AsSpan())
                {
                    var handle = component.TrackedHandle;
                    if (handle.IsPlaying()) return true;
                }

                return false;
            }
        }

        void OnDisable()
        {
            if (autoPlayMode == AutoPlayMode.OnEnable)
                Stop();
        }

        void OnDestroy()
        {
            Stop();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (version < 1)
            {
                autoPlayMode = playOnAwake ? AutoPlayMode.OnStart : AutoPlayMode.None;
                version = 1;
            }
        }
    }
}