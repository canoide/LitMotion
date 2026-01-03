using System;
using System.Reflection;
using UnityEngine;

namespace LitMotion.Animation.Components
{
    [Serializable]
    [LitMotionAnimationComponentMenu("General/Preset")]
    public sealed class PresetAnimation : LitMotionAnimationComponent
    {
        public LitMotionAnimationPreset preset;
        public GameObject target;

        public override MotionHandle Play()
        {
            if (preset == null) return default;
            if (target == null) return default;

            // Clone the preset to ensure unique component instances (stateful)
            var clone = UnityEngine.Object.Instantiate(preset);

            var builder = LSequence.Create();

            if (clone.components != null)
            {
                foreach (var component in clone.components)
                {
                    if (component == null || !component.Enabled) continue;

                    BindTarget(component, target);

                    var handle = component.Play();

                    if (!handle.IsActive()) continue;

                    if (clone.mode == AnimationMode.Sequential)
                    {
                        builder.Append(handle);
                    }
                    else
                    {
                        builder.Join(handle);
                    }
                }
            }

            return builder.Run();
        }

        void BindTarget(LitMotionAnimationComponent component, GameObject root)
        {
            var type = component.GetType();
            var field = GetField(type, "target");

            if (field != null)
            {
                var targetType = field.FieldType;

                if (typeof(GameObject).IsAssignableFrom(targetType))
                {
                    field.SetValue(component, root);
                }
                else if (typeof(UnityEngine.Component).IsAssignableFrom(targetType))
                {
                    var comp = root.GetComponent(targetType);
                    if (comp != null)
                    {
                        field.SetValue(component, comp);
                    }
                    // If comp is missing, we leave it as null (or whatever was in preset),
                    // which will likely fail/return default in Play(), which is fine.
                }
            }
        }

        FieldInfo GetField(Type type, string name)
        {
            while (type != null && type != typeof(object))
            {
                var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }
    }
}
