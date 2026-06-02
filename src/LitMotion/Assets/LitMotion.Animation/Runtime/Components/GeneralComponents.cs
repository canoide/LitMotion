using System;
using UnityEngine;
using UnityEngine.Events;

namespace LitMotion.Animation.Components
{
    [Serializable]
    [LitMotionAnimationComponentMenu("General/Set Active")]
    public sealed class GameObjectSetActiveAnimation : LitMotionAnimationComponent
    {
        [SerializeField] GameObject target;
        [SerializeField] bool active;
        [SerializeField] float delay;

        public override MotionHandle Play()
        {
            if (target == null) return default;

            if (delay > 0f)
            {
                // Create a motion that waits for 'delay' then executes SetActive.
                // We check if target is still valid before executing.
                return LMotion.Create(0f, 0f, delay)
                    .WithOnComplete(() =>
                    {
                        if (target != null) target.SetActive(active);
                    })
                    .RunWithoutBinding();
            }
            else
            {
                target.SetActive(active);
                // Return an immediate completed motion so the sequence continues
                return LMotion.Create(0f, 0f, 0f).RunWithoutBinding();
            }
        }
    }

    [Serializable]
    [LitMotionAnimationComponentMenu("General/Unity Event")]
    public sealed class UnityEventAnimation : LitMotionAnimationComponent
    {
        [SerializeField] UnityEvent onInvoke;
        [SerializeField] float delay;

        public override MotionHandle Play()
        {
            if (delay > 0f)
            {
                return LMotion.Create(0f, 0f, delay)
                    .WithOnComplete(() => onInvoke?.Invoke())
                    .RunWithoutBinding();
            }
            else
            {
                onInvoke?.Invoke();
                return LMotion.Create(0f, 0f, 0f).RunWithoutBinding();
            }
        }
    }

    [Serializable]
    [LitMotionAnimationComponentMenu("General/Delay")]
    public sealed class DelayAnimation : LitMotionAnimationComponent
    {
        [SerializeField] float duration;

        public override MotionHandle Play()
        {
            return LMotion.Create(0f, 0f, duration).RunWithoutBinding();
        }
    }
}
