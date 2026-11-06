namespace Gameplay.UI
{
    using System.Collections.Generic;
    using Core;
    using Domain.Lab;
    using Systems.Lab;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// UI Toolkit panel for the Lab runtime parameter editor.
    /// Displays all LabParameterGroups with collapsible sections and live sliders.
    /// Toggle via ToggleLabHudEvent (default binding: F1).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("3D2DWorld/UI/Lab HUD (UI Toolkit)")]
    public sealed class LabHUD : MonoBehaviour
    {
        #region Fields
        private UIDocument _uiDocument;
        public static bool IsOpen { get; private set; }

        private LabService _labService;
        private VisualElement _groupsContainer;
        private bool _isVisible;
        private readonly Dictionary<string, bool> _groupExpanded = new();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            SetupUIDocument();
            BuildUI();
        }

        private void Start()
        {
            ServiceLocator.TryGet<LabService>(out _labService);
            if (_labService != null)
                PopulateGroups();
            SetVisible(false);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<ToggleLabHudEvent>(OnToggleLab);
            GameEventManager.AddListener<LabParameterChangedEvent>(OnParamChanged);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<ToggleLabHudEvent>(OnToggleLab);
            GameEventManager.RemoveListener<LabParameterChangedEvent>(OnParamChanged);
            IsOpen = false;
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                SetVisible(!_isVisible);
            }
            else if (keyboard.escapeKey.wasPressedThisFrame && _isVisible)
            {
                SetVisible(false);
            }
        }
        #endregion

        #region Setup
        private void SetupUIDocument()
        {
            if (_uiDocument == null) return;
            _uiDocument.sortingOrder = 120;
            if (_uiDocument.panelSettings != null) return;

            var ps = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
            if (ps == null)
                ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
#endif
            if (ps != null) _uiDocument.panelSettings = ps;
        }

        private void BuildUI()
        {
            var root = _uiDocument?.rootVisualElement;
            if (root == null) return;

            var uxml = Resources.Load<VisualTreeAsset>("UI/Lab");
#if UNITY_EDITOR
            if (uxml == null)
                uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Lab.uxml");
#endif
            if (uxml != null) uxml.CloneTree(root);

            var uss = Resources.Load<StyleSheet>("UI/Lab");
#if UNITY_EDITOR
            if (uss == null)
                uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Lab.uss");
#endif
            if (uss != null && !root.styleSheets.Contains(uss)) root.styleSheets.Add(uss);

            BindStaticElements(root);
        }

        private void BindStaticElements(VisualElement root)
        {
            _groupsContainer = root.Q<VisualElement>("LabGroupsContainer");

            root.Q<Button>("LabClose")?.RegisterCallback<ClickEvent>(_ => SetVisible(false));
            root.Q<Button>("LabResetAll")?.RegisterCallback<ClickEvent>(_ => ResetAll());
        }
        #endregion

        #region Group Population
        private void PopulateGroups()
        {
            _groupsContainer?.Clear();
            if (_labService == null || _groupsContainer == null) return;

            foreach (var group in _labService.Groups)
                _groupsContainer.Add(BuildGroupElement(group));
        }

        private VisualElement BuildGroupElement(LabParameterGroup group)
        {
            var container = new VisualElement();
            container.AddToClassList("lab-group");

            bool expanded = _groupExpanded.GetValueOrDefault(group.GroupId, true);
            var body = BuildGroupBody(group);
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            var header = BuildGroupHeader(group, body);
            container.Add(header);
            container.Add(body);
            return container;
        }

        private VisualElement BuildGroupHeader(LabParameterGroup group, VisualElement body)
        {
            var header = new VisualElement();
            header.AddToClassList("lab-group-header");

            var title = new Label(group.DisplayName);
            title.AddToClassList("lab-group-title");
            header.Add(title);

            var arrow = new Label("▼");
            arrow.AddToClassList("lab-group-toggle-arrow");
            header.Add(arrow);

            header.RegisterCallback<ClickEvent>(_ => ToggleGroup(group.GroupId, body, arrow));
            return header;
        }

        private VisualElement BuildGroupBody(LabParameterGroup group)
        {
            var body = new VisualElement();
            body.AddToClassList("lab-group-body");

            foreach (var param in group.Parameters)
                body.Add(BuildParamRow(group.GroupId, param));

            return body;
        }

        private void ToggleGroup(string groupId, VisualElement body, Label arrow)
        {
            bool expanded = !_groupExpanded.GetValueOrDefault(groupId, true);
            _groupExpanded[groupId] = expanded;
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            arrow.text = expanded ? "▼" : "▶";
        }
        #endregion

        #region Parameter Controls
        private VisualElement BuildParamRow(string groupId, LabParameter param)
        {
            var row = new VisualElement();
            row.AddToClassList("lab-param-row");
            row.name = $"Param_{groupId}_{param.Id}";

            var label = new Label(param.DisplayName);
            label.AddToClassList("lab-param-label");
            row.Add(label);

            if (param.Type == LabParamType.Bool)
                row.Add(BuildBoolControl(groupId, param));
            else
                row.Add(BuildNumericControl(groupId, param));

            return row;
        }

        private VisualElement BuildNumericControl(string groupId, LabParameter param)
        {
            var sliderRow = new VisualElement();
            sliderRow.AddToClassList("lab-slider-row");

            var slider = new Slider(param.MinValue, param.MaxValue, SliderDirection.Horizontal)
            {
                value = param.CurrentValue
            };
            slider.AddToClassList("lab-slider");

            var valueLabel = new Label(FormatValue(param, param.CurrentValue));
            valueLabel.AddToClassList("lab-param-value");

            slider.RegisterValueChangedCallback(evt =>
            {
                _labService?.SetValue(groupId, param.Id, evt.newValue);
                valueLabel.text = FormatValue(param, evt.newValue);
            });

            sliderRow.Add(slider);
            sliderRow.Add(valueLabel);
            return sliderRow;
        }

        private VisualElement BuildBoolControl(string groupId, LabParameter param)
        {
            var toggle = new Toggle
            {
                value = param.AsBool()
            };
            toggle.AddToClassList("lab-toggle");
            toggle.RegisterValueChangedCallback(evt =>
                _labService?.SetValue(groupId, param.Id, evt.newValue ? 1f : 0f));
            return toggle;
        }

        private static string FormatValue(LabParameter param, float value)
        {
            return param.Type == LabParamType.Int
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("F2");
        }
        #endregion

        #region Visibility
        private void OnToggleLab(ToggleLabHudEvent evt) => SetVisible(!_isVisible);

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            IsOpen = _isVisible;
            var root = _uiDocument?.rootVisualElement?.Q<VisualElement>("LabRoot");
            if (root != null)
                root.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_isVisible)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else if (!CuratorHUD.IsOpen && !WorldMapHUD.IsOpen)
            {
                var creatorService = ServiceLocator.Get<Systems.Creator.CreatorStudioService>();
                bool isBuildMode = creatorService != null && creatorService.IsBuildMode;
                if (!isBuildMode)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }
        #endregion

        #region Reset
        private void ResetAll()
        {
            _labService?.ResetAll();
            PopulateGroups(); // Rebuild to reflect reset values on sliders
        }
        #endregion

        #region Event Handlers
        private void OnParamChanged(LabParameterChangedEvent evt)
        {
            // Live-update the value label for the changed parameter without full rebuild
            UpdateParamLabel(evt.GroupId, evt.ParameterId, evt.NewValue);
        }

        private void UpdateParamLabel(string groupId, string parameterId, float newValue)
        {
            var root = _uiDocument?.rootVisualElement;
            var row = root?.Q<VisualElement>($"Param_{groupId}_{parameterId}");
            var valueLabel = row?.Q<Label>(null, "lab-param-value");
            if (valueLabel == null) return;

            if (!_labService.TryGetValue(groupId, parameterId, out _)) return;
            // Find the parameter to format the value properly
            foreach (var group in _labService.Groups)
            {
                if (group.GroupId != groupId) continue;
                if (!group.TryGet(parameterId, out var param)) continue;
                valueLabel.text = FormatValue(param, newValue);
                break;
            }
        }
        #endregion
    }
}
