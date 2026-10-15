namespace Gameplay.UI
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using Core;
    using Domain.Grid;
    using Systems.Interaction;

    /// <summary>
    /// UI Toolkit controller for the 9-slot Hotbar HUD.
    /// Drives UXML layout and USS stylesheet classes dynamically.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("3D2DWorld/UI/Hotbar HUD (UI Toolkit)")]
    public class HotbarHUD : MonoBehaviour
    {
        private const int SlotCount = 9;

        [Header("Assets")]
        [SerializeField] private VisualTreeAsset _hotbarUxml;
        [SerializeField] private StyleSheet _hotbarUss;

        private UIDocument _uiDocument;
        private Label _activeBlockLabel;
        private readonly VisualElement[] _slots = new VisualElement[SlotCount];
        private readonly VisualElement[] _icons = new VisualElement[SlotCount];

        private int _currentSelectedIndex = 0;

        private const string SelectedClass = "hotbar-slot--selected";

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            SetupUIDocument();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<HotbarStateChangedEvent>(OnHotbarStateChanged);

            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
            {
                BindElements();
            }

            if (ServiceLocator.TryGet<HotbarService>(out var hotbar))
            {
                UpdateDisplay(hotbar.SelectedSlotIndex, hotbar.SelectedBlock);
            }
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<HotbarStateChangedEvent>(OnHotbarStateChanged);
        }

        private void SetupUIDocument()
        {
            if (_uiDocument == null)
                return;

            if (_hotbarUxml == null)
            {
                _hotbarUxml = Resources.Load<VisualTreeAsset>("UI/Hotbar");
#if UNITY_EDITOR
                if (_hotbarUxml == null)
                {
                    _hotbarUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Hotbar.uxml");
                }
#endif
            }

            if (_hotbarUss == null)
            {
                _hotbarUss = Resources.Load<StyleSheet>("UI/Hotbar");
#if UNITY_EDITOR
                if (_hotbarUss == null)
                {
                    _hotbarUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Hotbar.uss");
                }
#endif
            }

            if (_uiDocument.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
                if (panelSettings == null)
                {
                    panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
                }
#endif
                if (panelSettings != null)
                {
                    _uiDocument.panelSettings = panelSettings;
                }
            }

            if (_hotbarUxml != null)
            {
                _uiDocument.visualTreeAsset = _hotbarUxml;
            }

            var root = _uiDocument.rootVisualElement;
            if (root != null && _hotbarUss != null && !root.styleSheets.Contains(_hotbarUss))
            {
                root.styleSheets.Add(_hotbarUss);
            }

            BindElements();
        }

        private void BindElements()
        {
            var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            if (root == null) return;

            _activeBlockLabel = root.Q<Label>("ActiveBlockLabel");

            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = root.Q<VisualElement>($"Slot_{i}");
                _icons[i] = root.Q<VisualElement>($"Icon_{i}");

                int slotIndex = i;
                if (_slots[i] != null)
                {
                    // Allow clicking slots directly with mouse
                    _slots[i].RegisterCallback<ClickEvent>(evt =>
                    {
                        if (ServiceLocator.TryGet<HotbarService>(out var hotbar))
                        {
                            hotbar.SelectSlot(slotIndex);
                        }
                    });
                }
            }
        }

        private void OnHotbarStateChanged(HotbarStateChangedEvent evt)
        {
            UpdateDisplay(evt.SelectedSlotIndex, evt.SelectedBlock, evt.Slots);
        }

        private void UpdateDisplay(int selectedIndex, BlockDefinition selectedBlock, BlockDefinition[] slots = null)
        {
            _currentSelectedIndex = selectedIndex;

            if (_activeBlockLabel != null)
            {
                _activeBlockLabel.text = selectedBlock != null
                    ? $"{selectedBlock.DisplayName} [ID: {selectedBlock.RuntimeId}]"
                    : "Empty Slot";
            }

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = _slots[i];
                if (slot != null)
                {
                    if (i == selectedIndex)
                    {
                        slot.AddToClassList(SelectedClass);
                    }
                    else
                    {
                        slot.RemoveFromClassList(SelectedClass);
                    }
                }

                if (slots != null && i < slots.Length && _icons[i] != null)
                {
                    var block = slots[i];
                    if (block != null && block.BlockMaterial != null && block.BlockMaterial.mainTexture != null)
                    {
                        _icons[i].style.backgroundImage = new StyleBackground((Texture2D)block.BlockMaterial.mainTexture);
                        _icons[i].style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        _icons[i].style.backgroundImage = null;
                        _icons[i].style.display = DisplayStyle.None;
                    }
                }
            }
        }
    }
}
