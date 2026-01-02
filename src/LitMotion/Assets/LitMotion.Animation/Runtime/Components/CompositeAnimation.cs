using System;
using UnityEngine;

namespace LitMotion.Animation.Components
{
    [Serializable]
    [LitMotionAnimationComponentMenu("General/Composite")]
    public sealed class CompositeAnimation : LitMotionAnimationComponent
    {
        public AnimationMode mode;
        [SerializeReference] public LitMotionAnimationComponent[] children;

        public override MotionHandle Play()
        {
            var builder = LSequence.Create();
            var hasChildren = false;

            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child == null || !child.Enabled) continue;

                    var handle = child.Play();

                    // Note: LSequence takes ownership of the handle.
                    // If the child returns an empty/default handle (e.g. invalid target), LSequence might complain if it's not active.
                    // We should check if handle is active.
                    if (!handle.IsActive())
                    {
                        // If immediate completion (e.g. SetActive), we might want to AppendInterval(0)?
                        // But LSequence doesn't support "Append Finished Handle".
                        // Logic in LitMotionAnimation handles this by ignoring non-active handles in Parallel?
                        // Or treating them as done.
                        // For LSequence, we might need to skip?
                        // But if it was an instant action (SetActive), we want it to happen.
                        // If child.Play() already executed the logic (for instant actions), then it's done.
                        // We don't need to append it to sequence if it's already done.
                        continue;
                    }

                    hasChildren = true;

                    if (mode == AnimationMode.Sequential)
                    {
                        builder.Append(handle);
                    }
                    else
                    {
                        // In Parallel mode, we want all children to start at the beginning of this sequence.
                        // Join(handle) inserts at `lastTail`. Since we never call Append/AppendInterval
                        // in this loop for Parallel mode, `lastTail` remains at 0 (start of this sequence).
                        // So simply calling Join() for all items works correctly for a parallel block.
                        builder.Join(handle);
                    }
                }
            }

            // If no children or all instant, return empty.
            // LSequence.Run() with empty creates a dummy.
            return builder.Run();
        }
    }
}
