namespace Gameplay.UI
{
    using Core;
    using Domain.Museum;
    using Systems.Museum;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>
    /// UI Toolkit runtime overlay controller for the Museum Curator inspection panel.
    /// Lets developers evaluate assets in real-time (Keep/Refine/Discard) and export a report.
    /// Toggle via F2 or walk near an exhibit pedestal.
    /// </summary>
    [AddComponentMenu("3D 2D World/UI/Curator HUD")]
    [RequireComponent(typeof(UIDocument))]
    public class CuratorHUD : MonoBehaviour
    {
        #region Inspector Fields

        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset _curatorUxml;
        [SerializeField] private StyleSheet _curatorUss;

        #endregion

        #region UI Element References

        private VisualElement _root;
        private Label _categoryLabel;
        private Label _idLabel;
        private Label _nameLabel;
        private Label _originLabel;
        private Label _descriptionLabel;
        private Label _statusLabel;
        private Button _btnKeep;
        private Button _btnRefine;
        private Button _btnDiscard;
        private TextField _notesField;
        private Button _btnExport;
        private Button _btnClose;

        #endregion

        #region Private State

        private MuseumExhibitDefinitionSO _currentExhibit;
        private CuratorRating _currentRating = CuratorRating.Unrated;
        private bool _isVisible;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            SetupUIDocument();
            BindUIElements();
            SetVisible(false);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<ExhibitInspectedEvent>(OnExhibitInspected);
            GameEventManager.AddListener<ExhibitInspectionClosedEvent>(OnExhibitInspectionClosed);
            GameEventManager.AddListener<ToggleCuratorHudEvent>(OnToggleCuratorHud);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<ExhibitInspectedEvent>(OnExhibitInspected);
            GameEventManager.RemoveListener<ExhibitInspectionClosedEvent>(OnExhibitInspectionClosed);
            GameEventManager.RemoveListener<ToggleCuratorHudEvent>(OnToggleCuratorHud);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                ToggleVisible();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame && IsVisible())
            {
                SetVisible(false);
            }
        }

        #endregion

        #region UI Setup & Binding

        private void SetupUIDocument()
        {
            if (uiDocument == null) return;

            ResolvePanelSettings();
            ResolveUxmlAndUss();

            uiDocument.sortingOrder = 100;

            var root = uiDocument.rootVisualElement;
            if (root != null && _curatorUss != null && !root.styleSheets.Contains(_curatorUss))
            {
                root.styleSheets.Add(_curatorUss);
            }
        }

        private void ResolveUxmlAndUss()
        {
            if (_curatorUxml == null)
            {
                _curatorUxml = Resources.Load<VisualTreeAsset>("UI/CuratorOverlay");
#if UNITY_EDITOR
                if (_curatorUxml == null)
                    _curatorUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Curator/CuratorOverlay.uxml");
#endif
            }

            if (_curatorUss == null)
            {
                _curatorUss = Resources.Load<StyleSheet>("UI/CuratorOverlay");
#if UNITY_EDITOR
                if (_curatorUss == null)
                    _curatorUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Curator/CuratorOverlay.uss");
#endif
            }

            if (_curatorUxml != null && uiDocument.visualTreeAsset == null)
            {
                uiDocument.visualTreeAsset = _curatorUxml;
            }
        }

        private void ResolvePanelSettings()
        {
            if (uiDocument.panelSettings != null) return;

            var ps = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
            if (ps == null)
                ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
#endif
            if (ps != null) uiDocument.panelSettings = ps;
        }

        private void BindUIElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            _root = uiDocument.rootVisualElement.Q<VisualElement>("CuratorRoot");
            if (_root == null) return;

            _categoryLabel = _root.Q<Label>("ExhibitCategory");
            _idLabel = _root.Q<Label>("ExhibitId");
            _nameLabel = _root.Q<Label>("ExhibitName");
            _originLabel = _root.Q<Label>("ExhibitOrigin");
            _descriptionLabel = _root.Q<Label>("ExhibitDescription");
            _statusLabel = _root.Q<Label>("RatingStatusLabel");

            _btnKeep = _root.Q<Button>("BtnKeep");
            _btnRefine = _root.Q<Button>("BtnRefine");
            _btnDiscard = _root.Q<Button>("BtnDiscard");
            _notesField = _root.Q<TextField>("CuratorNotesField");
            _btnExport = _root.Q<Button>("BtnExportReport");
            _btnClose = _root.Q<Button>("CuratorClose");

            _btnKeep?.RegisterCallback<ClickEvent>(_ => SetRating(CuratorRating.Keep));
            _btnRefine?.RegisterCallback<ClickEvent>(_ => SetRating(CuratorRating.Refine));
            _btnDiscard?.RegisterCallback<ClickEvent>(_ => SetRating(CuratorRating.Discard));
            _btnExport?.RegisterCallback<ClickEvent>(_ => ExportReport());
            _btnClose?.RegisterCallback<ClickEvent>(_ => SetVisible(false));

            _notesField?.RegisterValueChangedCallback(evt =>
            {
                if (_currentExhibit != null)
                {
                    GameEventManager.Publish(new ExhibitEvaluatedEvent(_currentExhibit.ExhibitId, _currentRating, evt.newValue));
                }
            });
        }

        #endregion

        #region Event Handlers

        private void OnExhibitInspected(ExhibitInspectedEvent evt)
        {
            if (evt == null || evt.Exhibit == null) return;

            _currentExhibit = evt.Exhibit;
            _currentRating = evt.CurrentEvaluation != null ? evt.CurrentEvaluation.Rating : CuratorRating.Unrated;

            UpdateDisplayValues(evt.Exhibit, evt.CurrentEvaluation);
            SetVisible(true);
        }

        private void OnExhibitInspectionClosed(ExhibitInspectionClosedEvent evt)
        {
            SetVisible(false);
            _currentExhibit = null;
        }

        private void OnToggleCuratorHud(ToggleCuratorHudEvent evt)
        {
            ToggleVisible();
        }

        #endregion

        #region Evaluation Actions

        private void SetRating(CuratorRating rating)
        {
            if (_currentExhibit == null) return;

            _currentRating = rating;
            string notes = _notesField != null ? _notesField.value : string.Empty;

            GameEventManager.Publish(new ExhibitEvaluatedEvent(_currentExhibit.ExhibitId, rating, notes));
            UpdateRatingButtonsVisual();
        }

        private void ExportReport()
        {
            if (ServiceLocator.TryGet<CuratorService>(out var curator))
            {
                curator.ExportReport();
                if (_statusLabel != null)
                {
                    _statusLabel.text = "Report exported to Assets/Data/MuseumEvaluationReport.json!";
                }
            }
        }

        private void UpdateDisplayValues(MuseumExhibitDefinitionSO exhibit, CuratorEvaluationRecord eval)
        {
            if (_categoryLabel != null) _categoryLabel.text = exhibit.Category.ToString().ToUpperInvariant();
            if (_idLabel != null) _idLabel.text = exhibit.ExhibitId;
            if (_nameLabel != null) _nameLabel.text = exhibit.DisplayName;
            if (_originLabel != null) _originLabel.text = $"Source: {exhibit.PackageOrigin}";
            if (_descriptionLabel != null) _descriptionLabel.text = exhibit.Description;

            if (_notesField != null)
            {
                _notesField.SetValueWithoutNotify(eval != null ? eval.Notes : string.Empty);
            }

            UpdateRatingButtonsVisual();
        }

        private void UpdateRatingButtonsVisual()
        {
            _btnKeep?.RemoveFromClassList("active");
            _btnRefine?.RemoveFromClassList("active");
            _btnDiscard?.RemoveFromClassList("active");

            switch (_currentRating)
            {
                case CuratorRating.Keep:
                    _btnKeep?.AddToClassList("active");
                    if (_statusLabel != null) _statusLabel.text = "Rating: ⭐ KEEP (Approved for Core Game)";
                    break;
                case CuratorRating.Refine:
                    _btnRefine?.AddToClassList("active");
                    if (_statusLabel != null) _statusLabel.text = "Rating: 🔧 NEEDS REFINEMENT";
                    break;
                case CuratorRating.Discard:
                    _btnDiscard?.AddToClassList("active");
                    if (_statusLabel != null) _statusLabel.text = "Rating: ❌ DISCARD (Do not include)";
                    break;
                default:
                    if (_statusLabel != null) _statusLabel.text = "Rating: Unrated";
                    break;
            }
        }

        #endregion

        #region Visibility & Cursor State

        public void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (_root == null)
            {
                BindUIElements();
            }

            if (_root != null)
            {
                _root.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;

                if (_isVisible && _currentExhibit == null)
                {
                    if (_categoryLabel != null) _categoryLabel.text = "CURATOR MODE";
                    if (_idLabel != null) _idLabel.text = "OVERLAY";
                    if (_nameLabel != null) _nameLabel.text = "Testing Museum Showroom";
                    if (_originLabel != null) _originLabel.text = "Status: Ready";
                    if (_descriptionLabel != null) _descriptionLabel.text = "Curator HUD active. Walk near any exhibit pedestal or aim at it and press [E] to evaluate the asset.";
                    if (_statusLabel != null) _statusLabel.text = "Press F2 or [X] to close overlay";
                }
            }

            // Manage cursor lock so player can click buttons when HUD is open
            if (_isVisible)
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

        public bool IsVisible() => _isVisible;

        public void ToggleVisible() => SetVisible(!IsVisible());

        #endregion
    }
}
