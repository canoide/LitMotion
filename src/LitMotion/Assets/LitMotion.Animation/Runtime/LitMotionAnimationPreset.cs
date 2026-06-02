using LitMotion;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LitMotion.Animation
{
    [CreateAssetMenu(fileName = "New Animation Preset", menuName = "LitMotion/Animation Preset")]
    public sealed class LitMotionAnimationPreset : ScriptableObject
    {
        public AnimationMode mode;
        [SerializeReference] public LitMotionAnimationComponent[] components;
    }
}
