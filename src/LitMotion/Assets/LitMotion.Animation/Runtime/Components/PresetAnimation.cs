using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LitMotion.Animation.Components
{
    [Serializable]
    public struct AnimationBinding
    {
        public string id;
        public UnityEngine.Object target;
    }

    [Serializable]
    [LitMotionAnimationComponentMenu("General/Preset")]
    public sealed class PresetAnimation : LitMotionAnimationComponent
    {
        public LitMotionAnimationPreset preset;
        public GameObject target;
        public List<AnimationBinding> bindings;

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
            GameObject resolvedRoot = root;

            // 1. Try Binding ID (Robust Slot System)
            var bindingIdField = GetField(type, "bindingId");
            if (bindingIdField != null)
            {
                var id = bindingIdField.GetValue(component) as string;
                if (!string.IsNullOrEmpty(id) && bindings != null)
                {
                    // Find binding in local list
                    var binding = bindings.Find(x => x.id == id);
                    if (binding.target != null)
                    {
                        // Use the bound object directly!
                        // Need to check if it matches target type (GameObject vs Component)

                        // We set it to resolvedRoot logic?
                        // No, if binding target is a Component, we might need to extract GameObject if the animation wants GameObject?
                        // Or if animation wants Component, and binding is GameObject?
                        // Let's handle it at assignment time below.

                        // For simplicity, let's assume the binding target IS what we want to inject.
                        // But PropertyAnimationComponent expects TObject.
                        // We use reflection to set 'target' field directly.

                        var targetField = GetField(type, "target");
                        if (targetField != null)
                        {
                            var destType = targetField.FieldType;
                            var srcObj = binding.target;

                            // Compatibility check
                            if (destType.IsInstanceOfType(srcObj))
                            {
                                targetField.SetValue(component, srcObj);
                                return; // Done! Explicit binding wins.
                            }
                            else if (srcObj is GameObject go && typeof(Component).IsAssignableFrom(destType))
                            {
                                // If bound object is GO, but we need Component, try GetComponent
                                var comp = go.GetComponent(destType);
                                if (comp != null)
                                {
                                    targetField.SetValue(component, comp);
                                    return;
                                }
                            }
                            else if (srcObj is Component compSource && destType == typeof(GameObject))
                            {
                                targetField.SetValue(component, compSource.gameObject);
                                return;
                            }
                        }
                    }
                }
            }

            // 2. Try Target Name (Hierarchy Path - Fallback)
            var nameField = GetField(type, "targetName");
            if (nameField != null)
            {
                var targetName = nameField.GetValue(component) as string;
                if (!string.IsNullOrEmpty(targetName))
                {
                    var child = root.transform.Find(targetName);
                    if (child != null) resolvedRoot = child.gameObject;
                    // else warn?
                }
            }

            // 3. Auto-Bind to Root (Default behavior)
            var field = GetField(type, "target");

            if (field != null)
            {
                var targetType = field.FieldType;

                if (typeof(GameObject).IsAssignableFrom(targetType))
                {
                    field.SetValue(component, resolvedRoot);
                }
                else if (typeof(UnityEngine.Component).IsAssignableFrom(targetType))
                {
                    var comp = resolvedRoot.GetComponent(targetType);
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
