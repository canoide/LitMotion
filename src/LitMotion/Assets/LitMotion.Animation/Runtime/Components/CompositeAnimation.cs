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
                        // For the first item, Append vs Join matters?
                        // If builder is empty, Append and Join behave similarly (start at 0).
                        // But usually we Append the first, Join the rest?
                        // LSequence implementation of Join uses `lastTail`.
                        // If we iterate:
                        // 1. Append(h1).
                        // 2. Join(h2) -> inserts at start of h1.
                        // This works for "Parallel group".

                        // Wait, if I have [h1, h2, h3] Parallel.
                        // i=0: Append(h1).
                        // i=1: Join(h2).
                        // i=2: Join(h3).
                        // This puts them all starting at the same time. Correct.

                        // BUT if I mix?
                        // If this Composite is Sequential, and inside I have Parallel.
                        // The usage is: builder.Append(compositeHandle).

                        // Here inside CompositeAnimation, we are building the sequence for *this* composite.
                        // So for the children:
                        if (builder.TryGetVersion(out _)) // Check if empty? LSequence doesn't expose Count.
                        {
                             // Actually, logic:
                             // Sequential: Always Append.
                             // Parallel: Always Join?
                             // If I Join on an empty sequence, lastTail is 0. Insert at 0. Correct.
                             // But Join updates lastTail? No. Join uses lastTail.
                             // If I do Join(h1), Join(h2)...
                             // All insert at 0.
                             // But AppendInterval updates lastTail.
                             // Append updates lastTail.
                             // If I strictly use Join, I never update lastTail?
                             // Append(h1) updates tail.
                             // Join(h2) uses lastTail (start of h1).

                             // If I simply use Append for the first one?
                             // If mode is Parallel:
                             // Loop children:
                             //   If first: Append(h).
                             //   Else: Join(h).
                             // This seems safer.
                        }

                        // However, keeping track of "first" is easy.
                        builder.Join(handle);
                        // Wait, if I Join the FIRST one, lastTail is 0. Insert at 0.
                        // Tail becomes duration of h1.
                        // Next Join: lastTail is still 0 (Join doesn't move lastTail?).
                        // Let's check source code again.
                        // Join(handle) -> Insert(lastTail, handle).
                        // Insert(...) -> AddItem..., duration = Max(duration, ...).
                        // It does NOT update lastTail.
                        // So multiple Joins will all stack at the same point. Correct.
                        // But we need at least one Append to advance the sequence if we were continuing?
                        // But here we are building a *new* sequence just for these children.
                        // So stacking at 0 is correct.
                        // And the *result* handle will have the total duration.

                        // Exception: If I use `builder.Append(handle)` for the first one, it sets `lastTail`?
                        // `Append` calls `AppendInterval`.
                        // `AppendInterval`: lastTail = tail; tail += interval.
                        // So Append sets lastTail to 0 (initially 0), then tail to duration.
                        // So next Join will use 0.
                        // So Append then Join is fine.
                        // Join then Join?
                        // Join(h1): Insert at 0. lastTail 0.
                        // Join(h2): Insert at 0. lastTail 0.
                        // Seems fine too.

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
