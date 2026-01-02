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

        // Track which animation entry is currently requesting an add
        SerializedProperty pendingComponentsProp;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            animationsProp = serializedObject.FindProperty("animations");

            dropdown = new AddAnimationComponentDropdown(new());
            dropdown.OnTypeSelected += type =>
            {
                if (pendingComponentsProp != null)
                {
                    pendingComponentsProp.InsertArrayElementAtIndex(pendingComponentsProp.arraySize);
                    var property = pendingComponentsProp.GetArrayElementAtIndex(pendingComponentsProp.arraySize - 1);
                    property.managedReferenceValue = ReflectionHelper.CreateDefaultInstance(type);
                    serializedObject.ApplyModifiedProperties();
                    RefreshAnimationsList();
                    pendingComponentsProp = null;
                }
            };

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
            // Use explicit TextField to ensure visibility
            var idField = new TextField { bindingPath = idProp.propertyPath };
            idField.style.flexGrow = 1;
            idField.Bind(entryProp.serializedObject);

            header.Add(new Label($"#{index} "));
            header.Add(idField);

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
            var foldout = new Foldout { text = "Settings & Actions", value = false };
            foldout.Add(new PropertyField(entryProp.FindPropertyRelative("mode")));
            foldout.Add(new PropertyField(entryProp.FindPropertyRelative("onComplete")));

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
                var view = CreateComponentGUI(compProp);

                // Add explicit Remove button to row (header of view)
                var removeActionBtn = new Button(() =>
                {
                    componentsProp.DeleteArrayElementAtIndex(compIndex);
                    serializedObject.ApplyModifiedProperties();
                    RefreshAnimationsList();
                })
                {
                    style = {
                        backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_TreeEditor.Trash").image,
                        width = 18,
                        height = 18,
                        position = Position.Absolute,
                        right = 25, // Place next to context menu
                        top = 2,
                        backgroundColor = new Color(0.8f, 0.3f, 0.3f)
                    },
                    tooltip = "Remove Action"
                };

                // Insert into view header
                view.Add(removeActionBtn);

                componentsBox.Add(view);
            }

            var addCompBtn = new Button();
            addCompBtn.text = "Add Action...";
            addCompBtn.clicked += () =>
            {
                pendingComponentsProp = componentsProp;
                dropdown.Show(addCompBtn.worldBound);
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

                view.Foldout.BindProperty(property);

                var endProperty = property.GetEndProperty();
                var isFirst = true;
                while (property.NextVisible(isFirst))
                {
                    if (SerializedProperty.EqualContents(property, endProperty)) break;
                    if (property.name == "enabled") continue;

                    // Custom drawing for CompositeAnimation children
                    if (property.name == "children")
                    {
                        DrawChildrenList(property, view.Foldout.contentContainer);
                        continue;
                    }

                    isFirst = false;

                    view.Add(new PropertyField(property));
                }

                var enabledProperty = property.FindPropertyRelative("enabled");
                if (enabledProperty != null)
                {
                    view.EnabledToggle.BindProperty(enabledProperty);
                }
            }

            return view;
        }

        void DrawChildrenList(SerializedProperty componentsProp, VisualElement container)
        {
            var componentsBox = new Box();
            componentsBox.style.marginLeft = 10;
            componentsBox.style.marginTop = 5;
            componentsBox.Add(new Label("Children:"));

            for (int j = 0; j < componentsProp.arraySize; j++)
            {
                var compIndex = j; // Capture loop variable
                var compProp = componentsProp.GetArrayElementAtIndex(j);
                var view = CreateComponentGUI(compProp);

                // Add explicit Remove button to row (header of view)
                var removeActionBtn = new Button(() =>
                {
                    componentsProp.DeleteArrayElementAtIndex(compIndex);
                    serializedObject.ApplyModifiedProperties();
                    RefreshAnimationsList(); // Full refresh to handle nested structure updates
                })
                {
                    style = {
                        backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_TreeEditor.Trash").image,
                        width = 18,
                        height = 18,
                        position = Position.Absolute,
                        right = 25,
                        top = 2,
                        backgroundColor = new Color(0.8f, 0.3f, 0.3f)
                    },
                    tooltip = "Remove Action"
                };

                view.Add(removeActionBtn);
                componentsBox.Add(view);
            }

            var addCompBtn = new Button();
            addCompBtn.text = "Add Action to Composite...";
            addCompBtn.clicked += () =>
            {
                pendingComponentsProp = componentsProp;
                dropdown.Show(addCompBtn.worldBound);
            };
            componentsBox.Add(addCompBtn);

            container.Add(componentsBox);
        }
    }
}
