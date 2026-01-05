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

        LitMotionAnimationPreset runtimePreset;

        public override MotionHandle Play()
        {
            if (preset == null) return default;
            if (target == null) return default;

            // Clone the preset to ensure unique component instances (stateful)
            runtimePreset = UnityEngine.Object.Instantiate(preset);
            Debug.Log($"[PresetAnimation] Playing '{preset.name}' in mode: {runtimePreset.mode}");

            var builder = LSequence.Create();

            if (runtimePreset.components != null)
            {
                foreach (var component in runtimePreset.components)
                {
                    if (component == null || !component.Enabled) continue;

                    BindTarget(component, target);

                    var handle = component.Play();

                    if (!handle.IsActive()) continue;

                    if (runtimePreset.mode == AnimationMode.Sequential)
                    {
                        builder.Append(handle);
                    }
                    else
                    {
                        builder.Insert(0, handle);
                    }
                }
            }

            return builder.Run();
        }

        public override void OnStop()
        {
            if (runtimePreset != null && runtimePreset.components != null)
            {
                foreach (var component in runtimePreset.components)
                {
                    if (component != null) component.OnStop();
                }
                runtimePreset = null;
            }
        }

        void BindTarget(LitMotionAnimationComponent component, GameObject root)
        {
            var type = component.GetType();
            GameObject resolvedRoot = root;

            // Determine Binding Key (Explicit ID or DisplayName)
            string bindingKey = component.DisplayName;
            var bindingIdField = GetField(type, "bindingId");
            if (bindingIdField != null)
            {
                var explicitId = bindingIdField.GetValue(component) as string;
                if (!string.IsNullOrEmpty(explicitId)) bindingKey = explicitId;
            }

            // 1. Try Binding by Key
            if (bindings != null)
            {
                var binding = bindings.Find(x => x.id == bindingKey);
                if (binding.target != null)
                {
                    var targetField = GetField(type, "target");
                    if (targetField != null)
                    {
                        var destType = targetField.FieldType;
                        var srcObj = binding.target;

                        if (destType.IsInstanceOfType(srcObj))
                        {
                            targetField.SetValue(component, srcObj);
                            return;
                        }
                        else if (srcObj is GameObject go && typeof(Component).IsAssignableFrom(destType))
                        {
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

            // 2. Auto-Bind to Root (Default behavior if no binding found)
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
