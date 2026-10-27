namespace Gameplay.UI
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using Core;
    using Data.Config;
    using Systems.Interaction;

    /// <summary>
    /// UI Toolkit controller for the 9-slot item quickbar HUD.
    /// Reflects the player's inventory quickbar slots and selected item.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("3D2DWorld/UI/Hotbar HUD (UI Toolkit)")]
    public class HotbarHUD : MonoBehaviour
    {
        #region Constants
        private const int SlotCount = 9;
        private const string SelectedClass = "hotbar-slot--selected";
        #endregion

        #region Serialized Fields
        [Header("Assets")]
        [SerializeField] private VisualTreeAsset _hotbarUxml;
        [SerializeField] private StyleSheet _hotbarUss;
        #endregion

        #region Private Fields
        private UIDocument _uiDocument;
        private Label _activeBlockLabel;
        private readonly VisualElement[] _slots = new VisualElement[SlotCount];
        private readonly VisualElement[] _icons = new VisualElement[SlotCount];
        private int _currentSelectedIndex;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            SetupUIDocument();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<HotbarStateChangedEvent>(OnHotbarStateChanged);

            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
                BindElements();

            if (ServiceLocator.TryGet<HotbarService>(out var hotbar))
                UpdateDisplay(hotbar.SelectedSlotIndex, hotbar.SelectedItem);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<HotbarStateChangedEvent>(OnHotbarStateChanged);
        }
        #endregion

        #region Setup
        private void SetupUIDocument()
        {
            if (_uiDocument == null) return;
            _uiDocument.sortingOrder = 20;
            ResolveUxmlAndUss();
            ResolvePanelSettings();

            if (_hotbarUxml != null)
                _uiDocument.visualTreeAsset = _hotbarUxml;

            var root = _uiDocument.rootVisualElement;
            if (root != null && _hotbarUss != null && !root.styleSheets.Contains(_hotbarUss))
                root.styleSheets.Add(_hotbarUss);

            BindElements();
        }

        private void ResolveUxmlAndUss()
        {
            if (_hotbarUxml == null)
            {
                _hotbarUxml = Resources.Load<VisualTreeAsset>("UI/Hotbar");
#if UNITY_EDITOR
                if (_hotbarUxml == null)
                    _hotbarUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Hotbar.uxml");
#endif
            }

            if (_hotbarUss == null)
            {
                _hotbarUss = Resources.Load<StyleSheet>("UI/Hotbar");
#if UNITY_EDITOR
                if (_hotbarUss == null)
                    _hotbarUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Hotbar.uss");
#endif
            }
        }

        private void ResolvePanelSettings()
        {
            if (_uiDocument.panelSettings != null) return;
            var ps = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
            if (ps == null)
                ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
#endif
            if (ps != null) _uiDocument.panelSettings = ps;
        }

        private void BindElements()
        {
            var root = _uiDocument?.rootVisualElement;
            if (root == null) return;

            _activeBlockLabel = root.Q<Label>("ActiveBlockLabel");

            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = root.Q<VisualElement>($"Slot_{i}");
                _icons[i]  = root.Q<VisualElement>($"Icon_{i}");

                int slotIndex = i;
                _slots[i]?.RegisterCallback<ClickEvent>(_ => SelectSlot(slotIndex));
            }
        }
        #endregion

        #region Event Handlers
        private void OnHotbarStateChanged(HotbarStateChangedEvent evt)
        {
            UpdateDisplay(evt.SelectedSlotIndex, evt.SelectedItem, evt.Slots);
        }
        #endregion

        #region Display
        private void SelectSlot(int slotIndex)
        {
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                audio.PlayClick();

            if (ServiceLocator.TryGet<HotbarService>(out var hotbar))
                hotbar.SelectSlot(slotIndex);
        }

        private void UpdateDisplay(int selectedIndex, ItemData selectedItem, ItemData[] slots = null)
        {
            _currentSelectedIndex = selectedIndex;
            UpdateActiveLabel(selectedItem);
            UpdateSlotHighlights(selectedIndex);
            if (slots != null) UpdateSlotIcons(slots);
        }

        private void UpdateActiveLabel(ItemData item)
        {
            if (_activeBlockLabel == null) return;
            _activeBlockLabel.text = item != null ? item.DisplayName : "Empty";
        }

        private void UpdateSlotHighlights(int selectedIndex)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null) continue;
                if (i == selectedIndex)
                    _slots[i].AddToClassList(SelectedClass);
                else
                    _slots[i].RemoveFromClassList(SelectedClass);
            }
        }

        private void UpdateSlotIcons(ItemData[] slots)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_icons[i] == null) continue;
                var item = i < slots.Length ? slots[i] : null;
                if (item?.Icon != null)
                {
                    _icons[i].style.backgroundImage = new StyleBackground(item.Icon);
                    _icons[i].style.display = DisplayStyle.Flex;
                }
                else
                {
                    _icons[i].style.backgroundImage = null;
                    _icons[i].style.display = item != null ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }
        #endregion
    }
}
