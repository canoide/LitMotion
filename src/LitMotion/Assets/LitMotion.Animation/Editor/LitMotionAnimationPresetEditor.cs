using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using System.Collections.Generic;

namespace LitMotion.Animation.Editor
{
    [CustomEditor(typeof(LitMotionAnimationPreset))]
    public sealed class LitMotionAnimationPresetEditor : UnityEditor.Editor
    {
        SerializedProperty componentsProp;
        AddAnimationComponentDropdown dropdown;
        string pendingComponentsPath;
        VisualElement root;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            componentsProp = serializedObject.FindProperty("components");

            dropdown = new AddAnimationComponentDropdown(new());
            dropdown.OnTypeSelected += type =>
            {
                if (!string.IsNullOrEmpty(pendingComponentsPath))
                {
                    serializedObject.Update();
                    var prop = serializedObject.FindProperty(pendingComponentsPath);
                    if (prop != null && prop.isArray)
                    {
                        prop.InsertArrayElementAtIndex(prop.arraySize);
                        var property = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                        property.managedReferenceValue = ReflectionHelper.CreateDefaultInstance(type);
                        serializedObject.ApplyModifiedProperties();
                        RefreshList();
                    }
                    pendingComponentsPath = null;
                }
            };

            root.Add(new PropertyField(serializedObject.FindProperty("mode")));

            var listContainer = new VisualElement();
            root.Add(listContainer);

            // Helper to refresh
            void RefreshList()
            {
                listContainer.Clear();
                DrawComponentsList(componentsProp, listContainer);
            }

            // Initial Draw
            RefreshList();

            return root;
        }

        void DrawComponentsList(SerializedProperty listProp, VisualElement container)
        {
            var componentsBox = new Box();
            componentsBox.style.marginTop = 10;
            componentsBox.Add(new Label("Actions:"));

            for (int i = 0; i < listProp.arraySize; i++)
            {
                int index = i;
                var prop = listProp.GetArrayElementAtIndex(i);

                var wrapper = new VisualElement();
                var view = CreateComponentGUI(prop);
                wrapper.Add(view);

                var removeBtn = new Button(() =>
                {
                    listProp.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    // Full redraw needed
                    container.Clear();
                    DrawComponentsList(listProp, container);
                })
                {
                    style = {
                        backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_TreeEditor.Trash").image,
                        width = 18,
                        height = 18,
                        position = Position.Absolute,
                        right = 25,
                        top = 4,
                        backgroundColor = new Color(0.8f, 0.3f, 0.3f)
                    },
                    tooltip = "Remove Action"
                };

                wrapper.Add(removeBtn);
                componentsBox.Add(wrapper);
            }

            var addBtn = new Button(() =>
            {
                pendingComponentsPath = listProp.propertyPath;
                dropdown.Show(addBtn.worldBound);
            })
            {
                text = "Add Action...",
                style = { height = 25, marginTop = 5 }
            };
            componentsBox.Add(addBtn);

            container.Add(componentsBox);
        }

        // Duplicate of LitMotionAnimatorEditor logic (ideally refactor to shared helper)
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
                    view.Text = "Action";
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

                    // Support recursion for nested composites even in presets!
                    if (property.name == "children")
                    {
                        DrawComponentsList(property, view.Foldout.contentContainer);
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
    }
}
