using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LitMotion.Animation.Editor
{
    public class AnimationComponentView : VisualElement
    {
        VisualElement container;
        readonly VisualElement contextMenuButton;
        readonly Foldout foldout;
        readonly VisualElement icon;
        readonly Toggle enabledToggle;

        public Foldout Foldout => foldout;
        public Toggle EnabledToggle => enabledToggle;
        public VisualElement ContextMenuButton => contextMenuButton;

        public string Text
        {
            get => enabledToggle.text;
            set => enabledToggle.text = value;
        }

        public StyleBackground Icon
        {
            get => icon.style.backgroundImage;
            set => icon.style.backgroundImage = value;
        }

        public override VisualElement contentContainer => container;

        public AnimationComponentView()
        {
            container = this;

            var root = new HelpBox
            {
                style = {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1f,
                }
            };
            root.Clear();
            Add(root);

            foldout = new Foldout
            {
                style = {
                    marginLeft = 15f,
                    paddingRight = 3f,
                    alignSelf = Align.Stretch,
                }
            };
            root.Add(foldout);
            foldout.Add(new VisualElement() { style = { height = 5f } });
            var foldoutCheck = foldout.Q(className: Foldout.checkmarkUssClassName);
            icon = new VisualElement
            {
                style = {
                    width = 16f,
                    height = 16f,
                    marginTop = 0.5f,
                    marginRight = 2f,
                    backgroundImage = (Texture2D)EditorGUIUtility.IconContent("ScriptableObject Icon").image,
                }
            };
            foldoutCheck.parent.Add(icon);
            enabledToggle = new Toggle
            {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                }
            };
            enabledToggle.Q(className: Toggle.checkmarkUssClassName).style.marginRight = 6f;

            // Only schedule the one-off layout fix, no polling loop.
            enabledToggle.schedule.Execute(() =>
            {
                enabledToggle.pickingMode = PickingMode.Ignore;
                enabledToggle.Q(className: Toggle.inputUssClassName).pickingMode = PickingMode.Ignore;
                enabledToggle.Q(className: Toggle.textUssClassName).pickingMode = PickingMode.Ignore;
                enabledToggle.Q(className: Toggle.checkmarkUssClassName).pickingMode = PickingMode.Position;
            });
            foldoutCheck.parent.Add(enabledToggle);

            // Removed ProgressBar to improve Editor performance (lag issue).
            // Progress tracking requires efficient runtime binding which was causing overhead.

            contextMenuButton = new VisualElement
            {
                style = {
                    height = 15f,
                    width = 15f,
                    top = 4f,
                    right = 4f,
                    position = Position.Absolute,
                    backgroundImage = (Texture2D)EditorGUIUtility.IconContent("_Menu").image,
                }
            };
            root.Add(contextMenuButton);

            container = foldout.contentContainer;
        }

        public new void SetEnabled(bool enabled)
        {
            Foldout.contentContainer.SetEnabled(enabled && EnabledToggle.value);
            EnabledToggle.SetEnabled(enabled);
            icon.SetEnabled(enabled);
            contextMenuButton.SetEnabled(enabled);
        }
    }
}
