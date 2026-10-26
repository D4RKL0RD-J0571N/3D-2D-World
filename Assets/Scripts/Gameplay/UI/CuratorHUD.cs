namespace Gameplay.UI
{
    using Core;
    using Domain.Museum;
    using Systems.Museum;
    using UnityEngine;
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

        [Header("Keybindings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F2;

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

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            SetupUIDocument();
            BuildUI();
            SetVisible(false);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<ExhibitInspectedEvent>(OnExhibitInspected);
            GameEventManager.AddListener<ExhibitInspectionClosedEvent>(OnExhibitInspectionClosed);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<ExhibitInspectedEvent>(OnExhibitInspected);
            GameEventManager.RemoveListener<ExhibitInspectionClosedEvent>(OnExhibitInspectionClosed);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleVisible();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && IsVisible())
            {
                SetVisible(false);
            }
        }

        #endregion

        #region UI Setup & Binding

        private void SetupUIDocument()
        {
            if (uiDocument == null) return;
            if (uiDocument.panelSettings != null) return;

            var ps = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
            if (ps == null)
            {
                ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
            }
#endif
            if (ps != null) uiDocument.panelSettings = ps;
        }

        private void BuildUI()
        {
            if (uiDocument == null) return;
            var rootVisual = uiDocument.rootVisualElement;
            if (rootVisual == null) return;

            var uxml = Resources.Load<VisualTreeAsset>("UI/CuratorOverlay");
#if UNITY_EDITOR
            if (uxml == null)
            {
                uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Curator/CuratorOverlay.uxml");
            }
#endif
            if (uxml != null)
            {
                uxml.CloneTree(rootVisual);
            }

            var uss = Resources.Load<StyleSheet>("UI/CuratorOverlay");
#if UNITY_EDITOR
            if (uss == null)
            {
                uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Curator/CuratorOverlay.uss");
            }
#endif
            if (uss != null && !rootVisual.styleSheets.Contains(uss))
            {
                rootVisual.styleSheets.Add(uss);
            }

            _root = rootVisual.Q<VisualElement>("CuratorRoot");
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
                    if (_statusLabel != null) _statusLabel.text = "Rating: Unrated (Walk near pedestal to rate)";
                    break;
            }
        }

        #endregion

        #region Visibility Helpers

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public bool IsVisible()
        {
            return _root != null && _root.style.display == DisplayStyle.Flex;
        }

        public void ToggleVisible()
        {
            SetVisible(!IsVisible());
        }

        #endregion
    }
}
