using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace LitMotion.Animation.Editor
{
    [CustomEditor(typeof(LitMotionAnimator))]
    public sealed class LitMotionAnimatorEditor : UnityEditor.Editor
    {
        SerializedProperty animationsProp;
        VisualElement root;
        VisualElement animationsListContainer;
        AddAnimationComponentDropdown dropdown;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            animationsProp = serializedObject.FindProperty("animations");

            var settingsBox = new Box();
            settingsBox.style.paddingBottom = 10;
            settingsBox.Add(new Label("LitMotion Animator") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            // Buttons to control
            var debugBox = new Box();
            debugBox.style.flexDirection = FlexDirection.Row;
            debugBox.Add(new Button(() => ((LitMotionAnimator)target).Play()) { text = "Play Default" });
            debugBox.Add(new Button(() => ((LitMotionAnimator)target).StopAll()) { text = "Stop All" });
            root.Add(debugBox);

            animationsListContainer = new VisualElement();
            root.Add(animationsListContainer);

            var addAnimBtn = new Button(() =>
            {
                animationsProp.InsertArrayElementAtIndex(animationsProp.arraySize);
                var element = animationsProp.GetArrayElementAtIndex(animationsProp.arraySize - 1);
                // Initialize defaults if needed
                element.FindPropertyRelative("id").stringValue = "New Animation";
                element.FindPropertyRelative("components").ClearArray();
                serializedObject.ApplyModifiedProperties();
                RefreshAnimationsList();
            })
            {
                text = "Add New Animation",
                style = { height = 30, marginTop = 10 }
            };
            root.Add(addAnimBtn);

            RefreshAnimationsList();

            return root;
        }

        void RefreshAnimationsList()
        {
            animationsListContainer.Clear();

            // Check duplicates
            var ids = new HashSet<string>();
            var duplicates = new HashSet<string>();
            for (int i = 0; i < animationsProp.arraySize; i++)
            {
                 var id = animationsProp.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                 if (!string.IsNullOrEmpty(id) && !ids.Add(id)) duplicates.Add(id);
            }

            if (duplicates.Count > 0)
            {
                 animationsListContainer.Add(new HelpBox($"Duplicate Animation IDs found: {string.Join(", ", duplicates)}", HelpBoxMessageType.Warning));
            }

            for (int i = 0; i < animationsProp.arraySize; i++)
            {
                var entryProp = animationsProp.GetArrayElementAtIndex(i);
                animationsListContainer.Add(CreateAnimationEntryGUI(entryProp, i));
            }
        }

        VisualElement CreateAnimationEntryGUI(SerializedProperty entryProp, int index)
        {
            var box = new Box();
            box.style.marginTop = 5;
            box.style.paddingLeft = 5;
            box.style.paddingRight = 5;
            box.style.paddingTop = 5;
            box.style.paddingBottom = 5;
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;

            var borderColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            box.style.borderTopColor = borderColor;
            box.style.borderBottomColor = borderColor;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var idProp = entryProp.FindPropertyRelative("id");

            var idField = new TextField { bindingPath = idProp.propertyPath };
            idField.style.flexGrow = 1;
            idField.Bind(entryProp.serializedObject);

            header.Add(new Label($"#{index} "));
            header.Add(idField);

            var autoPlayProp = entryProp.FindPropertyRelative("autoPlay");
            var autoPlayToggle = new Toggle("AutoPlay") { bindingPath = autoPlayProp.propertyPath };
            autoPlayToggle.style.marginLeft = 5;
            autoPlayToggle.Bind(entryProp.serializedObject);
            header.Add(autoPlayToggle);

            // Per-animation controls
            var animId = idProp.stringValue; // Initial value

            var playBtn = new Button(() => {
                if (!string.IsNullOrEmpty(animId)) ((LitMotionAnimator)target).Play(animId);
            })
            {
                style = {
                    backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_PlayButton").image,
                    width = 24, height = 24
                },
                tooltip = "Play"
            };
            header.Add(playBtn);

            var pauseBtn = new Button(() => {
                if (!string.IsNullOrEmpty(animId)) ((LitMotionAnimator)target).Pause(animId);
            })
            {
                style = {
                    backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_PauseButton").image,
                    width = 24, height = 24
                },
                tooltip = "Pause"
            };
            header.Add(pauseBtn);

            var stopBtn = new Button(() => {
                if (!string.IsNullOrEmpty(animId)) ((LitMotionAnimator)target).Stop(animId);
            })
            {
                style = {
                    backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_PreMatQuad").image,
                    width = 24, height = 24
                },
                tooltip = "Stop"
            };
            header.Add(stopBtn);

            var removeBtn = new Button(() => {
                animationsProp.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                RefreshAnimationsList();
            })
            {
                style = {
                    backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_TreeEditor.Trash").image,
                    width = 24, height = 24,
                    backgroundColor = new Color(0.8f, 0.3f, 0.3f)
                },
                tooltip = "Remove Animation"
            };
            header.Add(removeBtn);

            box.Add(header);

            // Update ID local var when field changes so buttons work
            idField.RegisterValueChangedCallback(evt => animId = evt.newValue);

            // Foldout for details
            var foldout = new Foldout { text = "Settings & Actions" };
            foldout.viewDataKey = $"LitMotionAnim_{target.GetInstanceID()}_Entry_{index}";
            foldout.value = entryProp.isExpanded;
            foldout.RegisterValueChangedCallback(evt =>
            {
                entryProp.isExpanded = evt.newValue;
                entryProp.serializedObject.ApplyModifiedProperties(); // Save expansion state
            });

            // Settings Box (Wrap to prevent layout issues)
            var settingsBox = new Box { style = { marginBottom = 5 } };
            settingsBox.Add(new PropertyField(entryProp.FindPropertyRelative("mode")));
            settingsBox.Add(new PropertyField(entryProp.FindPropertyRelative("onComplete")));
            foldout.Add(settingsBox);

            // Components List
            var componentsProp = entryProp.FindPropertyRelative("components");
            var componentsBox = new Box();
            componentsBox.style.marginLeft = 10;
            componentsBox.style.marginTop = 5;
            componentsBox.Add(new Label("Actions:"));

            for (int j = 0; j < componentsProp.arraySize; j++)
            {
                var compIndex = j; // Capture loop variable
                var compProp = componentsProp.GetArrayElementAtIndex(j);

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

                var view = CreateComponentGUI(compProp);
                view.style.flexGrow = 1;
                row.Add(view);

                // Controls Container
                var controls = new VisualElement { style = { flexDirection = FlexDirection.Row, alignSelf = Align.FlexStart } };

                // Up Button
                var upBtn = new Button(() =>
                {
                    componentsProp.MoveArrayElement(compIndex, compIndex - 1);
                    serializedObject.ApplyModifiedProperties();
                    RefreshAnimationsList();
                }) { text = "↑", style = { width = 20 } };
                if (j == 0) upBtn.SetEnabled(false);
                controls.Add(upBtn);

                // Down Button
                var downBtn = new Button(() =>
                {
                    componentsProp.MoveArrayElement(compIndex, compIndex + 1);
                    serializedObject.ApplyModifiedProperties();
                    RefreshAnimationsList();
                }) { text = "↓", style = { width = 20 } };
                if (j == componentsProp.arraySize - 1) downBtn.SetEnabled(false);
                controls.Add(downBtn);

                // Remove Button
                var removeActionBtn = new Button(() =>
                {
                    componentsProp.DeleteArrayElementAtIndex(compIndex);
                    serializedObject.ApplyModifiedProperties();
                    RefreshAnimationsList();
                })
                {
                    text = "X",
                    style = {
                        width = 20,
                        backgroundColor = new Color(0.8f, 0.3f, 0.3f)
                    },
                    tooltip = "Remove Action"
                };
                controls.Add(removeActionBtn);

                row.Add(controls);
                componentsBox.Add(row);
            }

            var addCompBtn = new Button();
            addCompBtn.text = "Add Action...";
            addCompBtn.clicked += () =>
            {
                var localDropdown = new AddAnimationComponentDropdown(new UnityEditor.IMGUI.Controls.AdvancedDropdownState());
                var path = componentsProp.propertyPath;
                localDropdown.OnTypeSelected += type =>
                {
                    serializedObject.Update();
                    var prop = serializedObject.FindProperty(path);
                    if (prop != null && prop.isArray)
                    {
                        prop.InsertArrayElementAtIndex(prop.arraySize);
                        var property = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                        property.managedReferenceValue = ReflectionHelper.CreateDefaultInstance(type);
                        serializedObject.ApplyModifiedProperties();
                        RefreshAnimationsList();
                    }
                };
                localDropdown.Show(addCompBtn.worldBound);
            };
            componentsBox.Add(addCompBtn);

            foldout.Add(componentsBox);
            box.Add(foldout);

            return box;
        }

        // Reuse logic from LitMotionAnimationEditor for the Component View
        AnimationComponentView CreateComponentGUI(SerializedProperty property)
        {
            var view = new AnimationComponentView();

            if (string.IsNullOrEmpty(property.managedReferenceFullTypename))
            {
                view.Text = "(Missing)";
                view.Icon = (Texture2D)EditorGUIUtility.IconContent("Error").image;
                view.EnabledToggle.value = true;
                view.SetEnabled(true);
                view.EnabledToggle.Q("unity-checkmark").style.visibility = Visibility.Hidden;
                view.Add(new HelpBox("Missing type", HelpBoxMessageType.Error));
            }
            else
            {
                var displayNameProp = property.FindPropertyRelative("displayName");
                if (displayNameProp != null)
                {
                    view.Text = displayNameProp.stringValue;
                    view.TrackPropertyValue(displayNameProp, x => view.Text = x.stringValue);
                }
                else
                {
                    view.Text = "Composite";
                }

                var targetProperty = property.FindPropertyRelative("target");
                if (targetProperty != null)
                {
                    view.Icon = GUIHelper.GetComponentIcon(targetProperty.GetPropertyType());
                }
                else if (property.managedReferenceFullTypename.Contains("CompositeAnimation"))
                {
                    view.Icon = (Texture2D)EditorGUIUtility.IconContent("d_Folder Icon").image;
                }
                else if (property.managedReferenceFullTypename.Contains("PresetAnimation"))
                {
                    view.Icon = (Texture2D)EditorGUIUtility.IconContent("d_ScriptableObject Icon").image;
                }

                view.Foldout.BindProperty(property);

                var p = property.Copy();
                var endProperty = p.GetEndProperty();
                var isFirst = true;
                var isPresetEditor = p.serializedObject.targetObject is LitMotionAnimationPreset;

                while (p.NextVisible(isFirst))
                {
                    if (SerializedProperty.EqualContents(p, endProperty)) break;
                    if (p.name == "enabled") continue;
                    if (p.name == "bindings") continue; // Hide raw bindings list

                    // Hide 'target' field if we are editing a Preset (it uses bindings)
                    if (isPresetEditor && p.name == "target") continue;

                    // Custom drawing for CompositeAnimation children
                    if (p.name == "children")
                    {
                        DrawChildrenList(p.serializedObject, p.Copy(), view.Foldout.contentContainer);
                        continue;
                    }

                    isFirst = false;

                    if (p.name == "preset")
                    {
                        var field = new PropertyField(p);
                        field.RegisterCallback<ChangeEvent<UnityEngine.Object>>((evt) =>
                        {
                            root.schedule.Execute(() => RefreshAnimationsList());
                        });
                        view.Add(field);
                    }
                    else
                    {
                        view.Add(new PropertyField(p));
                    }

                    // Custom drawing for PresetAnimation embedded inspector and Bindings
                    if (p.name == "preset" && p.objectReferenceValue != null)
                    {
                        DrawPresetBindings(p.objectReferenceValue as LitMotionAnimationPreset, property.FindPropertyRelative("bindings"), view.Foldout.contentContainer);

                        var so = new SerializedObject(p.objectReferenceValue);
                        so.Update();

                        var modeProp = so.FindProperty("mode");
                        if (modeProp != null)
                        {
                            var modeBox = new Box { style = { marginTop = 2, marginBottom = 2, paddingLeft = 5 } };
                            modeBox.Add(new PropertyField(modeProp));
                            view.Foldout.contentContainer.Add(modeBox);
                        }

                        var comps = so.FindProperty("components");
                        if (comps != null)
                        {
                            DrawChildrenList(so, comps, view.Foldout.contentContainer);
                        }
                    }
                }

                var enabledProperty = property.FindPropertyRelative("enabled");
                if (enabledProperty != null)
                {
                    view.EnabledToggle.BindProperty(enabledProperty);
                }
            }

            return view;
        }

        void DrawChildrenList(SerializedObject targetObject, SerializedProperty listProp, VisualElement container)
        {
            var componentsBox = new Box();
            componentsBox.style.marginLeft = 10;
            componentsBox.style.marginTop = 5;

            // Distinguish between Composite and Preset labeling logic if desired, but "Actions" works for both.
            componentsBox.Add(new Label("Actions:") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            for (int j = 0; j < listProp.arraySize; j++)
            {
                var compIndex = j; // Capture loop variable
                var compProp = listProp.GetArrayElementAtIndex(j);

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

                var view = CreateComponentGUI(compProp);
                view.style.flexGrow = 1;
                row.Add(view);

                var removeActionBtn = new Button(() =>
                {
                    listProp.DeleteArrayElementAtIndex(compIndex);
                    targetObject.ApplyModifiedProperties();
                    RefreshAnimationsList(); // Full refresh to handle nested structure updates
                })
                {
                    text = "X",
                    style = {
                        width = 20,
                        backgroundColor = new Color(0.8f, 0.3f, 0.3f),
                        alignSelf = Align.FlexStart
                    },
                    tooltip = "Remove Action"
                };

                row.Add(removeActionBtn);
                componentsBox.Add(row);
            }

            var addCompBtn = new Button();
            addCompBtn.text = "Add Action...";
            addCompBtn.clicked += () =>
            {
                var localDropdown = new AddAnimationComponentDropdown(new UnityEditor.IMGUI.Controls.AdvancedDropdownState());
                var path = listProp.propertyPath;
                localDropdown.OnTypeSelected += type =>
                {
                    targetObject.Update();
                    var prop = targetObject.FindProperty(path);
                    if (prop != null && prop.isArray)
                    {
                        prop.InsertArrayElementAtIndex(prop.arraySize);
                        var property = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                        property.managedReferenceValue = ReflectionHelper.CreateDefaultInstance(type);
                        targetObject.ApplyModifiedProperties();
                        RefreshAnimationsList();
                    }
                    else
                    {
                        Debug.LogError($"[LitMotionAnimator] Failed to find valid array property at path: {path}");
                    }
                };
                localDropdown.Show(addCompBtn.worldBound);
            };
            componentsBox.Add(addCompBtn);

            container.Add(componentsBox);
        }

        void DrawPresetBindings(LitMotionAnimationPreset preset, SerializedProperty bindingsProp, VisualElement container)
        {
            if (preset == null || preset.components == null) return;

            var requiredBindings = new List<(string key, Type type)>();

            foreach (var c in preset.components)
            {
                if (c == null) continue;
                var t = c.GetType();
                var targetField = GetField(t, "target");
                if (targetField != null)
                {
                    string key = c.DisplayName;
                    var bindField = GetField(t, "bindingId");
                    if (bindField != null)
                    {
                        var bId = bindField.GetValue(c) as string;
                        if (!string.IsNullOrEmpty(bId)) key = bId;
                    }
                    requiredBindings.Add((key, targetField.FieldType));
                }
            }

            if (requiredBindings.Count > 0)
            {
                var box = new Box { style = { marginTop = 5, marginBottom = 5, paddingLeft = 5, paddingRight = 5, paddingTop = 5, paddingBottom = 5 } };
                box.Add(new Label("Required Bindings:") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

                foreach (var req in requiredBindings)
                {
                    SerializedProperty match = null;
                    for (int i = 0; i < bindingsProp.arraySize; i++)
                    {
                        var el = bindingsProp.GetArrayElementAtIndex(i);
                        if (el.FindPropertyRelative("id").stringValue == req.key)
                        {
                            match = el;
                            break;
                        }
                    }

                    var field = new ObjectField(req.key) { objectType = req.type, allowSceneObjects = true };
                    if (match != null)
                    {
                        field.value = match.FindPropertyRelative("target").objectReferenceValue;
                    }

                    field.RegisterValueChangedCallback(evt =>
                    {
                        bindingsProp.serializedObject.Update();
                        if (match == null) // Check again in case it was added by another callback? No, UI is synchronous usually.
                        {
                            // Need to find again because arraySize might changed? No, simplistic approach.
                            // Re-finding is safer if list changed.
                            match = null;
                            for (int i = 0; i < bindingsProp.arraySize; i++)
                            {
                                var el = bindingsProp.GetArrayElementAtIndex(i);
                                if (el.FindPropertyRelative("id").stringValue == req.key)
                                {
                                    match = el;
                                    break;
                                }
                            }
                        }

                        if (match == null)
                        {
                            bindingsProp.InsertArrayElementAtIndex(bindingsProp.arraySize);
                            match = bindingsProp.GetArrayElementAtIndex(bindingsProp.arraySize - 1);
                            match.FindPropertyRelative("id").stringValue = req.key;
                        }

                        match.FindPropertyRelative("target").objectReferenceValue = evt.newValue;
                        bindingsProp.serializedObject.ApplyModifiedProperties();
                    });

                    box.Add(field);
                }
                container.Add(box);
            }
        }

        System.Reflection.FieldInfo GetField(Type type, string name)
        {
            while (type != null && type != typeof(object))
            {
                var f = type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }
    }
}
