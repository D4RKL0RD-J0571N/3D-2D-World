namespace Gameplay.UI
{
    using System.Collections.Generic;
    using Core;
    using Domain.Creator;
    using Systems.Creator;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>
    /// UI Toolkit runtime overlay controller for the In-Game Creator Studio.
    /// Provides catalog palette browsing, category filtering, search, and mode toggling.
    /// </summary>
    [AddComponentMenu("3D 2D World/UI/Creator Studio HUD")]
    [RequireComponent(typeof(UIDocument))]
    public class CreatorStudioHUD : MonoBehaviour
    {
        #region Inspector Fields

        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private VisualTreeAsset _creatorUxml;
        [SerializeField] private StyleSheet _creatorUss;

        [Header("Catalog Reference")]
        [SerializeField] private CreatorCatalogSO _catalog;

        #endregion

        #region Private Fields & State

        public bool IsInputFocused { get; private set; }

        private VisualElement _root;
        private VisualElement _creatorBody;
        private VisualElement _headerRight;
        private Button _btnToggleMode;
        private Button _btnSaveLevel;
        private Button _btnLoadLevel;
        private Button _btnClearAll;
        private TextField _searchField;
        private VisualElement _paletteGrid;
        private Label _activeItemLabel;

        private Button _tabAll;
        private Button _tabWeapons;
        private Button _tabCombatants;
        private Button _tabStructures;
        private Button _tabProps;
        private Button _tabItems;

        private CreatorStudioService _service;
        private CreatorCategory? _activeCategoryFilter = null;
        private string _searchFilter = "";
        private readonly List<VisualElement> _cardElements = new List<VisualElement>();
        private VisualElement _selectedCard = null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            SetupUIDocument();
            BindUIElements();
        }

        private void Start()
        {
            _service = ServiceLocator.Get<CreatorStudioService>();
            if (_service != null && _catalog != null)
            {
                _service.SetCatalog(_catalog);
            }

            RefreshPalette();
            UpdateModeDisplay(false);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<CreatorModeChangedEvent>(OnModeChanged);
            GameEventManager.AddListener<CreatorItemSelectedEvent>(OnItemSelected);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<CreatorModeChangedEvent>(OnModeChanged);
            GameEventManager.RemoveListener<CreatorItemSelectedEvent>(OnItemSelected);
        }

        #endregion

        #region UI Initialization

        private void SetupUIDocument()
        {
            if (_uiDocument == null) return;

            _uiDocument.sortingOrder = 80;

            if (_uiDocument.panelSettings == null)
            {
                var ps = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
                if (ps == null)
                    ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
#endif
                if (ps != null) _uiDocument.panelSettings = ps;
            }

            if (_creatorUxml != null)
            {
                _uiDocument.visualTreeAsset = _creatorUxml;
            }

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            if (_creatorUss != null && !_root.styleSheets.Contains(_creatorUss))
            {
                _root.styleSheets.Add(_creatorUss);
            }
        }

        private void BindUIElements()
        {
            if (_root == null) return;

            _creatorBody = _root.Q<VisualElement>("CreatorStudioBody");
            _headerRight = _root.Q<VisualElement>("HeaderRight");
            _btnToggleMode = _root.Q<Button>("BtnToggleMode");
            _btnSaveLevel = _root.Q<Button>("BtnSaveLevel");
            _btnLoadLevel = _root.Q<Button>("BtnLoadLevel");
            _btnClearAll = _root.Q<Button>("BtnClearAll");
            _searchField = _root.Q<TextField>("SearchField");
            _paletteGrid = _root.Q<VisualElement>("PaletteItemsGrid");
            _activeItemLabel = _root.Q<Label>("ActiveItemLabel");

            if (_searchField != null)
            {
                _searchField.value = string.Empty;
                _searchField.RegisterCallback<FocusInEvent>(_ => IsInputFocused = true);
                _searchField.RegisterCallback<FocusOutEvent>(_ => IsInputFocused = false);
            }

            _tabAll = _root.Q<Button>("TabAll");
            _tabWeapons = _root.Q<Button>("TabWeapons");
            _tabCombatants = _root.Q<Button>("TabCombatants");
            _tabStructures = _root.Q<Button>("TabStructures");
            _tabProps = _root.Q<Button>("TabProps");
            _tabItems = _root.Q<Button>("TabItems");

            // Event bindings
            _btnToggleMode?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); _service?.ToggleBuildMode(); });
            _btnSaveLevel?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); _service?.SaveLevel("CustomShowroom"); });
            _btnLoadLevel?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); _service?.LoadLevel("CustomShowroom"); });
            _btnClearAll?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); _service?.ClearAllSpawnedObjects(); });

            _searchField?.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue ?? "";
                RefreshPalette();
            });

            _tabAll?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); SelectCategoryTab(null, _tabAll); });
            _tabWeapons?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); SelectCategoryTab(CreatorCategory.Weapons, _tabWeapons); });
            _tabCombatants?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); SelectCategoryTab(CreatorCategory.Combatants, _tabCombatants); });
            _tabStructures?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); SelectCategoryTab(CreatorCategory.Structures, _tabStructures); });
            _tabProps?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); SelectCategoryTab(CreatorCategory.Props, _tabProps); });
            _tabItems?.RegisterCallback<ClickEvent>(_ => { PlayClickSound(); SelectCategoryTab(CreatorCategory.Items, _tabItems); });
        }

        private static void PlayClickSound()
        {
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                audio.PlayClick();
        }

        #endregion

        #region Palette & Categories

        private void SelectCategoryTab(CreatorCategory? cat, Button selectedTab)
        {
            _activeCategoryFilter = cat;

            // Update tab styles
            Button[] allTabs = { _tabAll, _tabWeapons, _tabCombatants, _tabStructures, _tabProps, _tabItems };
            foreach (var tab in allTabs)
            {
                if (tab == null) continue;
                if (tab == selectedTab)
                {
                    tab.AddToClassList("category-tab-active");
                }
                else
                {
                    tab.RemoveFromClassList("category-tab-active");
                }
            }

            RefreshPalette();
        }

        public void RefreshPalette()
        {
            if (_paletteGrid == null || _catalog == null) return;

            _paletteGrid.Clear();
            _cardElements.Clear();
            _selectedCard = null;

            foreach (var item in _catalog.Items)
            {
                if (item == null) continue;

                // Category filter
                if (_activeCategoryFilter.HasValue && item.category != _activeCategoryFilter.Value)
                {
                    continue;
                }

                // Search query filter
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    bool match = (item.displayName != null && item.displayName.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (item.description != null && item.description.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) continue;
                }

                var card = CreatePaletteCard(item);
                _paletteGrid.Add(card);
                _cardElements.Add(card);
            }
        }

        private VisualElement CreatePaletteCard(CreatorItemDefinition item)
        {
            var card = new VisualElement();
            card.AddToClassList("palette-card");

            var title = new Label(item.displayName ?? item.id);
            title.AddToClassList("palette-card-title");
            card.Add(title);

            var category = new Label(item.category.ToString().ToUpper());
            category.AddToClassList("palette-card-category");
            card.Add(category);

            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (_selectedCard != null)
                {
                    _selectedCard.RemoveFromClassList("palette-card-active");
                }

                _selectedCard = card;
                card.AddToClassList("palette-card-active");

                _service?.SelectItem(item);

                // Auto switch to build mode if in play mode
                if (_service != null && !_service.IsBuildMode)
                {
                    _service.SetBuildMode(true);
                }
            });

            return card;
        }

        #endregion

        #region Event Reactions & Display

        private void OnModeChanged(CreatorModeChangedEvent evt)
        {
            UpdateModeDisplay(evt.IsBuildMode);
        }

        private void OnItemSelected(CreatorItemSelectedEvent evt)
        {
            if (_activeItemLabel == null) return;

            if (evt.Item != null)
            {
                _activeItemLabel.text = $"Armed: {evt.Item.displayName} [{evt.Item.category}]";
            }
            else
            {
                _activeItemLabel.text = "Selected: None (Click an item to arm placement ghost)";
            }
        }

        private void UpdateModeDisplay(bool isBuildMode)
        {
            if (_root != null)
            {
                if (isBuildMode)
                {
                    _root.AddToClassList("creator-studio-root--expanded");
                }
                else
                {
                    _root.RemoveFromClassList("creator-studio-root--expanded");
                }
            }

            if (_creatorBody != null)
            {
                _creatorBody.style.display = isBuildMode ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_headerRight != null)
            {
                _headerRight.style.display = isBuildMode ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_btnToggleMode != null)
            {
                _btnToggleMode.text = isBuildMode ? "⚔️ PLAY MODE [B]" : "🔨 BUILD MODE [B]";
                if (isBuildMode)
                {
                    _btnToggleMode.style.backgroundColor = new StyleColor(new Color(0.85f, 0.35f, 0.15f, 0.95f));
                }
                else
                {
                    _btnToggleMode.style.backgroundColor = new StyleColor(new Color(0.31f, 0.27f, 0.9f, 0.85f));
                }
            }

            // Adjust cursor lock when switching modes
            if (isBuildMode)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        #endregion
    }
}
