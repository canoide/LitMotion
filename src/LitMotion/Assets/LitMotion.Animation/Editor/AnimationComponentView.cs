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
        readonly ProgressBar progressBar;

        public Foldout Foldout => foldout;
        public Toggle EnabledToggle => enabledToggle;
        public VisualElement ContextMenuButton => contextMenuButton;

        public string Text
        {
            get => enabledToggle.text;
            set => enabledToggle.text = value;
        }

        public float Progress
        {
            get => progressBar.value;
            set
            {
                // Only trigger layout/paint changes if value actually changes
                if (!Mathf.Approximately(progressBar.value, value))
                {
                    progressBar.value = value;
                    // Hide if 0 to clean up UI when not playing, or keep visible?
                    // Original code showed it. Let's keep it simple.
                    // Optimally, we could hide the 'progress' bar fill if 0.
                    var progressElement = progressBar.Q(className: AbstractProgressBar.progressUssClassName);
                    if (progressElement != null)
                    {
                        var shouldDisplay = value > 0 && value < 1;
                        // Use style.display to hide completely or just rely on value.
                        // Standard ProgressBar handles value=0 well (empty).
                        // But let's replicate the "active" feel if needed.
                        // For now, standard behavior is fine.
                    }
                }
            }
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

            // One-off schedule for initialization is fine
            enabledToggle.schedule.Execute(() =>
            {
                enabledToggle.pickingMode = PickingMode.Ignore;
                enabledToggle.Q(className: Toggle.inputUssClassName).pickingMode = PickingMode.Ignore;
                enabledToggle.Q(className: Toggle.textUssClassName).pickingMode = PickingMode.Ignore;
                enabledToggle.Q(className: Toggle.checkmarkUssClassName).pickingMode = PickingMode.Position;
            });
            foldoutCheck.parent.Add(enabledToggle);

            // Re-added ProgressBar
            progressBar = new ProgressBar
            {
                lowValue = 0f,
                highValue = 1f,
                value = 0f,
                style = {
                    height = 2.5f,
                    position = Position.Absolute,
                    top = 22f,
                    left = 24f,
                    right = 2f,
                    alignSelf = Align.Stretch,
                    display = DisplayStyle.Flex // Always present in layout
                }
            };

            // Styling to make it look like a thin line
            var background = progressBar.Q(className: AbstractProgressBar.backgroundUssClassName);
            if (background != null)
            {
                background.style.borderTopWidth = 0f;
                background.style.borderBottomWidth = 0f;
                background.style.borderLeftWidth = 0f;
                background.style.borderRightWidth = 0f;
                background.style.backgroundColor = new Color(0, 0, 0, 0.1f); // Faint background
            }

            var progress = progressBar.Q(className: AbstractProgressBar.progressUssClassName);
            if (progress != null)
            {
                progress.style.backgroundColor = new Color(0.4f, 0.8f, 1f, 0.8f); // Blue-ish
                progress.style.minWidth = 0f;
            }

            root.Add(progressBar);

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
            progressBar.SetEnabled(enabled);
        }
    }
}
